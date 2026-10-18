# ModifiedArmy：征粮队外出导致驻军超编的修复计划

> 类型：设计与实现计划（尚未实施代码修改）  
> 仓库：[`GuihuaZhou/bannerlord`](https://github.com/GuihuaZhou/bannerlord)  
> 基准：`main` / `0236335b6bed2cf88b75f7fab2df63a6e1dc7f24`（2026-10-18 核对）  
> 涉及：`ModifiedArmy`、`ModifiedPolitics`  
> 原则：最小改动、共享统一容量口径、保留现有补员模型、不改变存档结构

## 1. 问题确认

当前代码的征粮队出发、补员、返程相互独立：

1. `SupplyPartyBehavior.ExtractEscortRoster()` 从驻军 `MemberRoster` 真正移走最多 50 名健康士兵。
2. `GarrisonRecruitFromPrisonersBehavior.GetDailyRecruitmentLimit()` 使用 `GarrisonParty.Party.NumberOfAllMembers` 计算可用人数，不包括外出征粮队。
3. `RecruitmentSimulationState.Create()` 也只读取 `GarrisonParty.Party.NumberOfAllMembers`，因此 `DefaultAIRecruitmentModel` 的 95% 编制限制同样忽略征粮队。
4. `SupplyPartyBehavior.CompleteMission()` 直接调用 `RestoreTroops()`，将所有幸存士兵加回驻军，无容量检查。
5. AI Party 捐兵路径 `GarrisonPartyPriorityPatch` 的 `GetAllowedDonationCount()` 只对捐出方做限制，可能占用本该留给征粮队返程的编制。
6. `ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs` 对每日补员额度做 Harmony Postfix 加成，并使用**未扣征粮队预留人数**的实际空位裁剪；若只修复 ModifiedArmy 的日额计算，这里可能重新放宽额度。

**确定存在容量管理缺陷**。但“此前每一条超编逃兵日志”是否都由征粮队造成，仍需要补充归队事件日志确认，不能凭现有逃兵记录逐条归因。

例如上限 `L=300`、原驻军 `280`，征粮队带走 `50` 后驻军显示 `230`。若错误地允许职业兵补至当前名册的 95% 上限 `285`，征粮队原样返回后将显示 `335/300`。

## 2. 修复目标与统一公式

只影响**容量判断**，不改变实际 `MemberRoster`、工资、粮食消费、守城参战人数，也不修改 `PartySizeLimit`。

定义：

- `G`：驻军当前实际人数，`garrison.Party.NumberOfAllMembers`（包含伤兵及英雄）。
- `E`：**所有存活且任务未完成**、归属于该定居点的征粮队，当前 `party.Party.NumberOfAllMembers` 合计。不能固定使用 50。
- `L`：驻军当前动态上限，`garrison.Party.PartySizeLimit`。
- `C = G + E`：已占用或已预留的有效编制人数。
- `D`：兵营与军事官职计算得到的每日补员限额。

```text
EffectiveCount = G + E
HardFreeSlots = max(0, L - EffectiveCount)
SoftRecruitmentFreeSlots = max(0, floor(0.95 * L) - EffectiveCount)
ActualProfessionalAndPrisonerRecruitment
    <= min(D, HardFreeSlots, SoftRecruitmentFreeSlots, 兵源与财政等原有限制)
```

**特殊说明：**
- `95%` 仅适用于现有统一自动招募逻辑；正常 AI Party 捐兵等其他转移路径遵守 `HardFreeSlots`，不额外引入 95% 目标。
- 外出人数 `E` 随征粮队战损即时减少：幸存 20 人就预留 20 人，不永久锁死 50 个名额。
- 如上限在途中下降，可能出现 `C > L`；此时停止新增士兵并记录异常，但**不自动杀兵、解散征粮队或丢弃归来的士兵**。

## 3. 改动文件与具体实现

### 3.1 建立统一征粮队预留编制计算

**新增建议：**`ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs`

建议公开方法：

```csharp
public static int GetActiveSupplyEscortCount(Settlement home);
public static int GetEffectiveMemberCount(MobileParty garrison);
public static int GetHardFreeSlots(MobileParty garrison);
public static int GetSoftRecruitmentFreeSlots(MobileParty garrison);
```

`GetActiveSupplyEscortCount(home)` 遍历 `MobileParty.All`，仅累加同时满足：

```text
party != null && party.IsActive
party.PartyComponent is SupplyPartyComponent component
component.SupplyHomeSettlement == home
component.MissionState != SupplyPartyMissionState.Completed
```

人数使用该 Party **实时** `Party.NumberOfAllMembers`；不能使用初始 50、`RequestedFood`、静态列表或征粮队货物数量。

不得为预留人数新建 SaveData；由真实队伍与已有 `SupplyPartyComponent` 在加载后直接重算。即便异常情况下同一驻军出现不止一支活动征粮队，也应累加所有符合条件的队伍，并记录异常，不能简单只取第一支。

**性能：**仅在实际需要计算补员计划、AI 捐赠限额、预测显示或诊断时读取；一次招募事务只统计一次，必要时向下传入快照，不要在每名候选兵的比例循环中反复扫描 `MobileParty.All`。不建议新增依赖同步时序的长期缓存。

### 3.2 修复驻军每日自动补员的硬额度

**修改：**`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

`GetDailyRecruitmentLimit(Town town)` 将原有：

```csharp
town.GarrisonParty.Party.PartySizeLimit
    - town.GarrisonParty.Party.NumberOfAllMembers
```

改为共享接口：

```csharp
GarrisonRecruitmentCapacity.GetHardFreeSlots(town.GarrisonParty)
```

保留 `GetMaximumDailyAutoRecruitmentCount(town)`、兵营训练上限、财政和兵源约束。

`GetPredictedDailyRecruitmentCount()` 应通过同一接口/模型取得预测值，不能出现实际补员已停但 UI 仍显示正数的情况。

### 3.3 修复统一招募模型的 95% 安全阈值

**修改：**
- `ModifiedArmy/ModifiedArmy/Recruitment/Models/RecruitmentSimulationState.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs`

建议在模拟状态中加入**运行时只读、不持久化**字段：

```csharp
public int ReservedReturningMembers { get; private set; }
public int EffectiveProjectedMemberCount =>
    ProjectedMemberCount + ReservedReturningMembers;
```

`RecruitmentSimulationState.Create(party)`：

- 若 `party.IsGarrison`，设置 `ReservedReturningMembers = GetActiveSupplyEscortCount(party.CurrentSettlement)`；
- 若为野战 Party，保持 `ReservedReturningMembers = 0`；
- `ProjectedMemberCount` 仍代表真实驻军成员 + 本次模拟批准的新兵；
- `Commit()` 仍只增加新招募人数，不动预留人数；
- 质量、兵种分类及现有工资计算不人为添加“虚拟征粮队士兵”。

把 `DefaultAIRecruitmentModel` 中涉及 `95%` 停止招募与 `AllowedByPartySize` 的人数基准从 `ProjectedMemberCount` 改为 `EffectiveProjectedMemberCount`。这样自动补员不会误用征粮队离开后腾出的配额；又不会影响普通野战 Party 的招募政策。

### 3.4 实际入队前做最终容量复核

**修改：**`GarrisonRecruitFromPrisonersBehavior.ExecuteUnifiedPlan()`

每次执行候选兵转入前重新取当前的剩余安全空位，最终数量为：

```text
ApprovedCount = min(
    evaluation.RecruitableCount,
    dailyLimit - alreadyRecruited,
    currentHardFreeSlots,
    currentSoftRecruitmentFreeSlots)
```

取 `ApprovedCount` 后再消耗职业兵 offer 或转移俘虏，实际扣除与入队数量必须严格一致；不能先消费 5 个兵源、实际只加入 2 人。保留现有 `ClanRecruitmentBudgetManager` 的费用和工资预算流程。

### 3.5 同步修复军事官职补员倍率

**修改：**`ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs`

现有 `IncreaseGarrisonRecruitment()` 使用：

```csharp
Math.Max(0, PartySizeLimit - NumberOfAllMembers)
```

作为 Postfix 后的最终上限，忽略征粮队预留人数。

改用 `GarrisonRecruitmentCapacity.GetHardFreeSlots(...)`，保留官职加成原有的倍率和取整方式。由于官职 Postfix 在 `ModifiedArmy.GetDailyRecruitmentLimit` 之后执行，此修改是必要的，不能仅依赖补员入口最初的容量裁剪。

### 3.6 AI 捐兵与军团捐兵必须保护返程名额

**修改：**`ModifiedArmy/ModifiedArmy/Garrison/Patches/GarrisonTroopDonationPatch.cs`

在 `GarrisonPartyPriorityPatch.Prefix()` 对 `numberOfTroopsToLeave` 叠加接收方限制：

```text
AllowedDonation = min(
    RequestedDonation,
    GetAllowedDonationCount(sourceParty, RequestedDonation),
    GetHardFreeSlots(destinationGarrison))
```

当驻军尚未创建时，通过 `Settlement` 对应的 `PartySizeLimitModel.CalculateGarrisonPartySizeLimit()` 获取容量，外出征粮队数量也使用相同定居点口径。

必要时在原版 `LeaveTroopsToGarrison` 的执行路径外层做后置异常日志，确认军团多个 Party 独立随机取整后也不会突破预留容量。多支 Party 连续捐赠时必须逐次读取最新人数。

### 3.7 征粮队归队时增加保护与诊断

**修改：**`ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs`

在 `CompleteMission()` 返还士兵前后记录：

```text
[SupplyEscortCapacity]
SettlementId, SupplyPartyId,
GarrisonBefore, EscortReturning,
ReservedEscortBefore, CurrentLimit,
GarrisonAfter, ReservedEscortAfter,
ExcessAfter, MissionState
```

预留名额从**有效成员计算**中解除，与真实士兵恢复必须保证逻辑上只计算一次，避免在“士兵已加回驻军但任务状态仍为 ReturningHome”的瞬间双重计数。现有代码按 `RestoreTroops()` → `component.CompleteMission()` → `DestroyPartyAction` 的顺序执行，实施时应特别检查是否有同一调用链内的同步事件读取中间状态。

若回归时当前上限已经低于在外兵力和驻军的合计，**允许幸存士兵返回原驻军**并记录一次 `ReturnOverflow`，不直接裁撤；上限发生变化的问题应由独立日志帮助定位。

另外在创建征粮队失败、catch 恢复士兵的分支记录异常性超编，防止恢复路径漏检。

### 3.8 解散 Party 并入驻军的边界处理

**检查/按需修改：**`Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs`

普通士兵走 `BuildPlan()`，应通过第 3.3 节自然使用预留编制；但 `TransferHeroes()` 直接向驻军加英雄，未经过容量检查。

本轮不删除或强行遣散英雄；若该路径造成超编，记录 `MergeOverflow`（士兵数、英雄数、驻军上限、预留人数），并单独确认英雄的原版去向，再决定处理。避免为了修复征粮队而破坏领主解散机制。

## 4. 非目标与限制

- 不把征粮队成员实际放回驻军名册中“虚占人数”。
- 不调整粮食、士气、军饷、驻军守城兵力与地图移动速度。
- 不改变征粮队的 `EscortSize=50`、补给路线、出发条件和返程行为。
- 不修改现有 95% 自动招募阈值或军事官职加成百分比。
- 不修改驻军总人数上限计算模型，不新增持久化存档字段。
- 不直接开启“超编就裁兵”的新机制。
- 这次改动解决**外出征粮队所造成的编制重复占用**，但不会自动修复因行政人员变化、存档旧状态、其他 Mod 直接加兵等原因导致的所有超编。

## 5. 测试与验收

| 场景 | 初始条件 | 预期 |
|---|---|---|
| A：典型征粮往返 | `L=300,G=280`，派出 50 人 | `G=230,E=50,C=280`；普通自动补员最多额外 5 人；全员归队最多 285/300 |
| B：原先驻军已满 | `L=300,G=300`，派出 50 人 | `G=250,E=50,C=300`；自动补员为 0 |
| C：征粮队损失 30 人 | 原来外出 50，现幸存 20 | `E` 即时变为 20；允许按实际空位补员，幸存者回归仍不突破当前硬上限 |
| D：征粮队尚未返回时 AI 捐兵 | `L=300,G=250,E=50` | AI 捐兵为 0，不占用返程预留名额 |
| E：军事官职加成 | 军事官职提升每日补员上限 | `ModifiedPolitics` 加成不能突破预留编制硬上限和 95% 模型阈值 |
| F：无征粮队 | `E=0` | 补员结果与原代码完全一致 |
| G：旧存档加载，征粮队在途 | 存档存在 `SupplyPartyComponent` | 加载后即时能算出当前幸存人数，不需要 SaveData 迁移 |
| H：上限动态降低 | 征粮队在途，`L` 下降到 `C` 以下 | 停止新增人数，日志说明超额；不丢弃返程士兵 |
| I：返程、销毁、异常恢复 | 正常回城、创建失败或被摧毁 | `E` 最终正确清零或减少，不出现“幽灵预留” |
| J：非驻军 Party | 正常 AI 野战 Party | `ReservedReturningMembers=0`，现有 95% 和预算行为不变 |

重点在测试环境中观察：**征粮队外出至返回期间，所有自动新兵与 AI 捐兵交易之后，始终满足 `G + E <= L`（除已有超编或动态降额等既有异常外）**。

## 6. 日志与交付标准

仅写 `ModifiedArmy.log`（新增诊断日志不使用 `ModLogger.Notice`）；如现有 `ModLogger.Info` 可能因 UI 日志级别配置显示在屏幕，应使用独立 `FileInfo` 或同等只写文件的方法。

日志尽量采用状态变化、异常和征粮队实际往返事件，避免每个游戏 Tick 或每个候选士兵都刷大量日志。

交付应包含：

1. 完整源码改动与 `ModifiedPolitics` 交叉适配；
2. 编译通过，且无 Harmony 参数绑定或方法重载错误；
3. A–J 场景的实际验证记录；
4. 征粮队返程和异常恢复前后的驻军容量日志；
5. 修改文件清单与 commit，**不要只调整每周裁兵来掩盖问题**。

**执行顺序：**先实现统一预留人数服务 → 修复自动补员计划与执行 → 同步军事官职 Postfix → 修复 AI 捐兵额度 → 补充返程和异常日志 → 编译及实测。
