# ModifiedArmy｜玩家封邑兵返乡诊断与安置独立化｜Codex / Worker 施工指导 V1.0

> **项目**：`GuihuaZhou/bannerlord` · `ModifiedArmy`  
> **核对基线**：`main`，`1faadfca92c407220a6b9de455885d54760744f6`（2026-11-12 核对）。执行前必须 `git fetch` / 核对真实 HEAD；若源码有新提交，先重新定位对应方法，不能机械套用行号。  
> **用途**：直接交给 Codex / Worker 实施代码修改、诊断与验证。本文是施工规范，**不是已完成代码、测试报告或运行结果**。  
> **适用范围**：返乡冷却结算、玩家封邑兵限定诊断、玩家“返乡+安置”入口解耦、安置候选村庄故障解释。**不处理**兵种权重重平衡、全部 AI 征募规则、其他无关模组。

## 0. 用户已确认的硬性决定（不可自行改变）

1. **新增的封邑兵生命周期/安置诊断日志只能打印“玩家当前拥有的封邑及其附属村庄”的相关信息**。判断依据是封邑/村庄来源归属，**不是** `party == MobileParty.MainParty`，也不是“某个兵种属于瓦兰迪亚文化”。玩家家族以 `Clan.PlayerClan` 为准；非玩家领地不得产生新加的返乡/安置诊断日志，即使该领地的 AI Party 出现异常。
2. **正常返乡和余兵安置是两个独立业务**。本次真实返乡人数为 0，也应允许安置；没有任何在役批次也应能安置合法多余封邑兵。
3. **复用既有按钮/菜单和现有安置事务服务**；不额外创建一个“安置窗口”，除非源码核实已有入口无法实现目标且明确报告。不得将“点击空白选兵窗口的完成/关闭”擅自变成安置确认。
4. 不能为了“修好返乡”直接清除 `ReturnBatches`、将所有冷却士兵强行加入 Ready、篡改 `WaitWeeks`、绕过恢复冻结、凭空造兵或修改 Hearth/`MobilizedHearth`。每次转移人数守恒。
5. 安置仍受合法地点、Clan 所有权、模板升级树、全部在役批次优先、剩余合法封建供养额度、恢复阻断和事务回滚保护约束。**返乡数量=0 不等于跳过安置；安置资格不成立也不等于强制安置。**

## 1. 已核实的源码事实与问题范围

下列路径相对 `ModifiedArmy/ModifiedArmy/`：

| 文件 | 目前相关行为 | 施工重点 |
|---|---|---|
| `Fief/Models/Data/FiefReturnBatch.cs` | 持久化 `Troops`、`WaitWeeks`；`Tick()` 每次减 1 周；`IsReady()` 判断到期 | 不新增 Saveable 类型，不重置旧批次时间 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | `WeeklyUpdate()` 中：若 `!IsRuntimeReady` 返回；`RecoveryRequired` 村庄直接跳过；对 ReturnBatches 执行 Tick；到期但 `GetReadyRoster()==null` 时不迁出；成功才从 ReturnBatches 移除 | 必须补“每周为何停住”的玩家限定诊断；安全解决名册暂缺后已到期批次的迟迟不迁出 |
| `Fief/Models/VillageFiefData.cs` | `GetReadyRoster()` 依赖村庄民兵 Party 处于 Active；`RecoveryRequired/RecoveryReason` 记录冻结 | 诊断只读状态，禁止静默解除冻结 |
| `Fief/Models/FiefPartyManager.cs` | 注册 `WeeklyTickEvent`；`OnMobilePartyCreated` 可修复村庄民兵绑定；`ReturnSelectedFiefTroops()` 含 **`if (returned > 0)` 才调用安置**；整体 `ReturnTroopsAndPlace()` 已无此限制 | 取消 `returned > 0` 耦合；让已有有效入口可独立安置；如需在民兵重建时补齐到期迁出，谨慎复用服务 |
| `Fief/Presentation/FiefManagementPresentationContext.cs` | `Commit()` 仅在非空选中时执行；`Serving` 选中调用 `ReturnSelectedFiefTroops()`；`Cooling` 不可操作 | 不把“空提交/关闭”变成隐式安置；保留已选中但返乡 0 人仍运行安置 |
| `Fief/Models/FiefMenuBehavior.cs` | 现有整体解散/返乡菜单调用 `ReturnTroopsToSettlement()` | 整体入口始终先正常返乡，再独立尝试安置，即使返乡 0 人 |
| `Fief/Services/FiefTroopPlacementService.cs` | 已实现未分配兵力快照、模板过滤、各村供养预算、逐兵按剩余额度最大分配和事务；`NoEligiblePlacementVillage` 在容量匹配**之前**返回 | 只对玩家拥有的封邑打印逐村候选被排除原因；不因诊断放宽安置门槛 |
| `ModuleData/militaryConfig.xml` | 瓦兰迪亚 `returnCooldownWeeks="2"` | 不修改此数值来掩盖返乡停滞 |

### 已有日志可以证实的事实（与根因推测分开）

源日志：`ModifiedArmy(20261112-095441).log`。沙拉斯 `town_V7` 的玩家操作出现三次：

```text
Fief placement snapshot accepted: ... partyId=player_party, settlement=沙拉斯,
  purpose=PlayerReturn, protected=0, unassigned=35, woundedUnassigned=0.
Fief placement rejected: ... reason=NoEligiblePlacementVillage.
```

- 这证明安置入口执行到了 **候选村庄集合为空**，但**不证明**“没有供养额度”：原因发生在逐兵容量分配前。
- 观察期间沙拉斯反复出现 `NoEligibleDailyAllocation`，但它也**不能直接证明** `RecoveryRequired=true`。
- `village_V7_1` 多次出现村庄民兵 Party 移除/重建日志，且 `lostReadyFiefTroops=0`；这证明名册有阶段性缺失的可能，**不证明**那 21 名返乡士兵当时就在该村，也不证明 21 人因它卡住。
- 现有日志没有该 21 人的每批 `WaitWeeks`、周结算状态、逐村 `RecoveryReason`；目前不可声称已经确诊。
- 视觉现象：待役 91、返乡 21、在役 0；瓦兰迪亚正常返乡冷却应约为 2 次每周 Tick；是否曾被反复新增新批次/冻结，需要新诊断验证。

## 2. 阶段 A：实现“仅玩家拥有的封邑兵”的统一诊断门禁（先做）

### 2.1 单一判定函数

在现有封邑诊断工具/合适的内部静态 helper 中建立例如：

```csharp
// 示意；具体命名与可见性按项目约定。
internal static bool ShouldTracePlayerOwnedFief(
    Settlement sourceFief,
    Settlement village = null)
{
    Clan playerClan = Clan.PlayerClan;
    if (playerClan == null || sourceFief?.IsFortification != true ||
        sourceFief.OwnerClan != playerClan) return false;

    if (village == null) return true;
    return village.IsVillage &&
        village.Village?.Bound == sourceFief &&
        sourceFief.BoundVillages?.Any(x => x?.Settlement == village) == true;
}
```

**解释**：只按**当前**归属判断；不是玩家历史曾经拥有的领地，不是玩家当前野战部队所在地的所有 AI 封邑，不是文化筛选。可以直接复用 `FiefPartyData.IsCurrentVillage()` 来防止规则重复，但当诊断目的恰恰是排查绑定异常时，应对玩家封邑原始 `Villages` 数据进行只读诊断，明确输出 `IsCurrentVillage=false`，不要因为它不合规就一开始把信息过滤掉。

**严守**：门禁加在**新增的诊断事件**最外层，并在组装大量日志参数之前判断。不要依赖仅按 Party 的 `RecruitmentLogFilter.ShouldLog(party)`；它不等同于玩家封邑归属。异常处理日志/既有全局错误日志不作额外更改，除非直接属于本任务新增日志。

### 2.2 诊断级别与防刷屏

- 一般生命周期追踪用 `ModLogger.FileDebug`；明确数据不变量损坏用 `FileError`；允许业务拒绝用 `FileDebug` 或已有规范级别。**不得通知 UI 或每周弹窗**。
- 仅在**玩家当前拥有的封邑**且对应村庄含非空 `ReturnBatches` 时，写每周冷却诊断。无冷却且无异常不写。
- 单次操作的安置拒绝**只写一组**总览及每个候选村庄一条解释；不要按每个士兵循环记录。
- 不新增对所有 133 个封邑的逐周/逐日遍历日志，不新增 AI 普通部队调度日志。
- 日志键使用稳定的 `fiefId` / `villageId` / `operationId` 和 `week/day`，可以以队列索引 + 兵种摘要定位批次；`FiefReturnBatch` 当前没有永久 batch ID，不要为诊断引入新的 Saveable 字段。

## 3. 阶段 B：返乡冷却结算诊断与安全恢复

### 3.1 每周必须记录的节点

在 `FiefPartyData.WeeklyUpdate()` 执行冷却的真实分支中，仅玩家封邑记录：

1. `IsRuntimeReady=false`：若该封邑当前属于玩家且含可访问的冷却队列，记录 `FiefRuntimeNotReady`；不要默默结束。
2. 对每个**有冷却批次**的玩家村庄，在 `RecoveryRequired` 检查**之前**读取只读元数据；若冻结，记录 `BlockedByRecovery`、`RecoveryReason`、总人数及每批等待周数，不调用 `Tick()`、不自动解冻。
3. `GetReadyRoster()==null` 时，应记录 `ReadyRosterUnavailable`（包含 `MilitiaPartyComponent`/`MobileParty.IsActive` 等可安全读取字段）。可继续原有的周递减，但**不得丢弃已到期批次**。
4. 对正常批次记录 `WaitWeeksBefore`、`WaitWeeksAfter`、`IsReady`、`troopCount`、`result`，成功转移时记录 `MovedToReady`、`movedCount`、`readyBefore`、`readyAfter`、`coolingAfter`。
5. 批次到期而 Ready 不存在：明确记录 `MaturedWaitingForRoster`；正常等待倒计时：`CoolingProgress`。
6. 执行异常：记录 `CoolingCommitFailed`、异常类型、是否成功回滚；不能吞兵、不能仅日志宣称成功。

参考**实际期望格式**（只是格式规范，非现有日志）：

```text
[PlayerFiefCooling] fief=沙拉斯 fiefId=town_V7 villageId=village_V7_2
 week=... troops=21 waitBefore=0 waitAfter=0 recovery=false
 rosterActive=false result=MaturedWaitingForRoster

[PlayerFiefCooling] fiefId=town_V7 villageId=village_V7_2
 week=... troops=21 waitBefore=2 waitAfter=2 recovery=true
 recoveryReason=... result=BlockedByRecovery
```

### 3.2 修复“名册暂缺时已到期的返乡兵”

**建议重构成单一结算 helper**，而不是在各事件里复制迁移逻辑。例如：

```csharp
// 语义示意，需由 Worker 结合仓库类型实现。
ProcessVillageCooling(village, advanceWeeklyClock: true);  // WeeklyTick
ProcessVillageCooling(village, advanceWeeklyClock: false); // 名册重建/会话恢复
```

约束：

- `advanceWeeklyClock=true` 才对 `WaitWeeks>0` 减 1；`false` **只处理早已到期**的 `WaitWeeks<=0` 批次，不重复加速冷却。
- `RecoveryRequired=true` 时一律不结算，只记录玩家限定的阻断原因；不要为了修复卡死绕过恢复事务。
- `ReadyRoster==null` / 非 Active：保留 `ReturnBatches` 原样（周倒计时可以已递减），之后等待名册恢复。
- 在 `FiefPartyManager.OnMobilePartyCreated()` 完成原有 `RepairVillageMilitiaBinding(...)` **成功之后**，针对**该村庄**调用不递减时钟的到期结算；会话初始化过程中若事件顺序不可靠，可在 `OnSessionLaunched` 数据恢复就绪后有界触发一次“只处理到期”的检查。不得向其他村庄/城堡直接搬运。
- 从 `ReturnBatches` 迁移到 Ready 时：先对 troop/count 做校验；精确快照关联兵种的 Ready 数量；添加并验证 Ready 增量；**验证成功后**移除该批次，且仅移除一次；失败回滚 Ready 名册，保留原批次，若无法验证回滚则标记该玩家村庄恢复冻结并明确日志。避免因异常/重复事件复制或丢失兵员。
- 成功后刷新当前 `FiefPartyData` 兵源计数；不能修改 Hearth、`MobilizedHearth` 或在役台账。
- `ReturnBatches` 仍是持久化唯一来源；禁止给冷却批次增加新持久化 ID/新计时机制。

### 3.3 `RecoveryRequired` 的处置边界

不自动 `CompleteRecovery()`。如果玩家村庄实际冻结，则应先从**现有** `RecoveryReason` / `RecoveryOperationId` 和动员/账本一致性检查找出原因。可以实施**被严格证明为一致的**已有恢复流程；无法证明时保留冻结并输出原因、预期/实际计数及人工处理建议。不得以重置冻结标记作为修复验收结果。

## 4. 阶段 C：将返乡与安置彻底解耦

### 4.1 确切需要改动的代码缺陷

`FiefPartyManager.ReturnSelectedFiefTroops()` 当前包含：

```csharp
if (returned > 0)
    FiefTroopPlacementService.TryPlaceAfterReturn(this, party,
        interaction, FiefPlacementPurpose.PlayerReturn);
```

**删掉 `returned > 0` 这个条件**。对已通过合法地点、所有权、界面指纹、有效选择等前置检验的操作，在原返乡事务整轮结束后，**始终独立调用一次**现有安置服务；不以本次 `returned` 是否大于 0 作为门槛。

`ReturnTroopsAndPlace()` 已是“返乡后无条件调用安置”模型，保持这一行为；核对两个现有包装入口 `ReturnTroopsToSettlement` / `ReturnTroopsToSettlementWithResult` 都委托它。返回值继续表达**实际返乡人数**，不能悄悄改成“返乡+安置”总人数。

### 4.2 正确的业务边界

示意顺序：

```csharp
// 已通过必要的操作地点/调用者资格验证后：
int returned = ReturnTrackedFiefBatches(...);  // 允许 0
FiefPlacementResult placed = FiefTroopPlacementService.TryPlaceAfterReturn(
    manager, party, interaction, FiefPlacementPurpose.PlayerReturn);
// returned 与 placed.PlacedCount 分别报告；不互相控制。
```

- **零在役**、**本次返乡=0**、**安置成功>0**：必须支持（尤其是现有整体解散/返乡按钮）。
- 在“在役”页执行**有效选择**，但经过实时对账后实际返乡 0：仍尝试安置；安置服务再次对账保护所有来源在役批次。
- **空选中直接关闭**：维持无副作用；不要通过删除 `FiefManagementPresentationContext.Commit()` 的空名册校验，把“完成/关闭”改成自动安置。因此玩家零在役时仍使用现有整体返乡/解散菜单触发安置。
- `Interaction != Settlement.CurrentSettlement`、失效界面指纹、非法权限/地点、恢复冻结、不可用模板：依旧拒绝执行，没有“为了安置而绕过返乡安全条件”的特例。
- 不在 `FiefReturnService.TryReturn()` 或 `TryCommitFiefReturn()` 的**每批次**内调用安置；必须在完整一轮高层业务结尾调用**一次**，防止重复进账。
- 安置无需满足“单次征召至少 25 人”；合法且容量足够时可以安置 1 人。

### 4.3 防止“余兵安置”触碰受保护的在役兵

继续复用 `FiefTroopPlacementService` 的 `ReconcilePartyFiefTroops(party, true)`、`PlacementSnapshotValid`、`UnassignedTroops`、模板升级树 `IsEnableTroop`、`TryGetFeudalSupplyCost`、`CanIncreaseSupply` 及事务回滚。严禁用 `party.MemberRoster` 全部封建兵直接入库，严禁利用“返乡=0”跳过在役身份保留。仅允许本目标封邑的合法村庄承接，不跨任意其他 Clan、也不跨无关封邑。

## 5. 阶段 D：安置被拒绝的**玩家限定**逐村解释

现有 `NoEligiblePlacementVillage` 是在下面筛选得到空集时抛出：

```csharp
scope.FiefData.OrderedVillages(scope.VillageFilter)
    .Where(v => v?.VillageSettlement != null &&
                !v.RecoveryRequired &&
                v.GetReadyRoster() != null)
```

这里尚未计算实际供养，因此日志不能笼统翻译为“没有剩余供养”。施工要求：

1. 当目标 **`scope.SourceFief.OwnerClan == Clan.PlayerClan`** 时，先只读枚举 `scope.FiefData.Villages` 的原始登记及 `OrderedVillages(...)` 的合法子集，逐村报告是否当前附属、是否被村庄筛选限制、RecoveryRequired/Reason、Ready 名册是否存在且 Active、有效 Hearth、占用 Hearth、`GetFreeSupplyHearth`、`GetFeudalBudgetRemainingHearth`、`max(0,min(...))`。
2. 按原因码区分 `VillageNoLongerBound`、`FilteredToCurrentVillage`、`RecoveryRequired`、`ReadyRosterUnavailable` 等；最终概要仍可沿用 `NoEligiblePlacementVillage` 以保持外部兼容。不可把容量不足归为“没有候选村庄”。
3. 在候选村庄存在但 `PlacedCount==0` 时，再输出按兵种统计的 `unassigned`、`templateEligible`、`costEligible`、`capacityEligible`、`reason=NoEligibleTroopsOrCapacity`。注意日志中 `unassigned=35` 表示**未关联在役批次**，不表示 35 人全部是合法可安置兵种。
4. 输出仅针对当前玩家封邑业务操作，不在每次野战部队访问或每个 AI 定居点上大规模打印。一次玩家点击一次诊断概要；重复点击可有独立 operationId。
5. 诊断只读，不能通过生成日志触发 `CanIncreaseSupply` 之外的有副作用事务，不允许改变村庄名册。

推荐摘要：

```text
[PlayerFiefPlacement] operation=... fiefId=town_V7 partyId=player_party
 returnCount=0 unassigned=35 eligibleTroops=... placed=0
 result=NoEligiblePlacementVillage
[PlayerFiefPlacementVillage] villageId=village_V7_1 currentBound=...
 recoveryRequired=... recoveryReason=... rosterActive=...
 supplyFree=... feudalBudgetRemaining=... rejection=...
```

**注意**：`returnCount` 不一定能在当前独立服务内拿到；若需展示，使用高层业务组合结果传入日志上下文，不能伪装安置结果本身有这个字段，更不能为日志改存档格式。

## 6. 施工顺序与修改范围

按以下顺序实施，并在每步复查编译：

1. **最小入口修复**：移除 `ReturnSelectedFiefTroops()` 的 `returned > 0` 条件；验证整体解散入口仍能在返乡=0 时安置。
2. **统一日志门禁**：实现 `ShouldTracePlayerOwnedFief(...)`，所有新增 PlayerFiefCooling / PlayerFiefPlacement 诊断必须经过此门禁。不得无条件输出全地图周 Tick。
3. **冷却可观测性**：在 `WeeklyUpdate()` 增加玩家独有的周前/周后状态和拒绝原因；在读档就绪时仅对玩家拥有且存在 Cooling 的封邑输出一次状态快照，以便重现旧存档问题。
4. **候选村庄诊断**：当玩家触发安置且失败，逐村打印绑定、冻结、名册及供养的只读原因。
5. **有证据的结算修复**：实现安全的“到期批次补结算” helper、在民兵 Party 正常重建后调用；若确定另有冻结根因，保留阻断并报告/使用既有安全恢复机制，不扩大成无条件解冻。
6. **验证与清理**：清理临时无约束调试日志，留下用户要求的玩家限定诊断；更新必要的已有技术文档。

**预期可修改的文件**（以实际 HEAD 为准；能复用现有 helper 就不新建）：

- `Fief/Models/FiefPartyManager.cs`
- `Fief/Models/PartyData/FiefPartyReinforcement.cs`
- `Fief/Services/FiefTroopPlacementService.cs`
- `Fief/Models/VillageFiefData.cs`（仅在真正需要受控辅助时修改）
- `Fief/Presentation/FiefManagementPresentationContext.cs`（原则上保留空选中语义）
- 新增 **一个** 玩家封邑诊断 gate/helper（若已有适合位置可合并）
- 本仓库现有测试项目/验证说明（有则扩展，无则提供真实可执行复现方案）

**明确不修改**：`ModuleData/militaryConfig.xml` 的冷却数值、封邑兵模板权重、征召/AI 战略决策、职业兵配置、Hearth 衰减、人口事务和非封邑模块。

## 7. 必须覆盖的测试与验收矩阵

| 编号 | 用例 | 必须通过的验收条件 |
|---|---|---|
| T01 | 玩家城镇：无任何在役兵，本次返乡 0、部队含 10 名模板允许的非在役兵，附属村有容量 | 仍完成合法安置；Party -10、Cooling +10；返乡返回值仍为 0 |
| T02 | 玩家城镇：在役兵实际返乡 0（可由对账后失效导致），有合法余兵 | 若业务入口权限合法且非过期操作，仍独立尝试安置；不受 `returned > 0` 阻断 |
| T03 | 玩家城镇：选中 5 名在役返乡且额外 7 名非在役可安置 | 两类分别结算与报告；任何士兵不重复入账 |
| T04 | 管理界面：空选中直接关闭 | 不发生隐式安置，不改 Party 或村庄名册；原整体返乡/解散菜单仍可主动触发独立安置 |
| T05 | 玩家村庄：冷却 2 周，Ready 正常、未冻结 | 两次 WeeklyTick 后迁入 Ready，Cooling 清零；人数守恒 |
| T06 | 玩家村庄：`ReadyRoster==null`，冷却到期；后来民兵 Party 重建 | 冷却批次保留，重建后**不额外递减周数**即可安全补结算；不复制、不丢失 |
| T07 | 玩家村庄：`RecoveryRequired=true`，冷却非空 | 不递减、不迁移；玩家日志明确标出 `RecoveryReason`，不能自行解冻 |
| T08 | 玩家村庄：`WaitWeeks=0` 的旧存档批次，Ready 在读档后可用 | 安全处理到期迁入；不能反复迁入或额外等待整周 |
| T09 | 现有 PlayerReturn 安置因所有附属村庄均不可候选而拒绝 | 返回 `NoEligiblePlacementVillage`，逐村给出真实不可候选原因，而非假称缺供养 |
| T10 | 有可候选村庄但全部供养预算为 0；又有不在模板树中的余兵 | 明确区分 `capacity=0` 与 `troopNotEligible`，Party 不丢兵 |
| T11 | 玩家封邑 A 和 AI 封邑 B 同时有返乡批次，跨多周运行 | 新的 `[PlayerFief...]` 日志**只出现 A**；B 业务正常但没有新增诊断日志 |
| T12 | 玩家 Party 去 NPC/非玩家封邑，或角色不是玩家本人但其目标是玩家家族封邑 | 日志依据**封邑归属**而不是 Party 身份；非法操作仍安全拒绝 |
| T13 | 多批次同兵种、升级树关联、伤员、跨来源在役保护 | 只安置 `UnassignedTroops`，迁移前后总数及伤员处理符合现有事务契约 |
| T14 | 发生提交异常/名册状态突变 | 安全回滚；失败详情准确；不得同时留下 Party 兵和 Cooling 兵 |
| T15 | 连续保存/读档、反复触发 WeeklyTick 与 OnMobilePartyCreated | 不重复结算、不重置冷却、不新增存档不兼容字段 |

**定量验收不变量**：

- 返乡到期迁移：`ReadyAfter - ReadyBefore == CoolingBefore - CoolingAfter`（按每兵种核算；升级或其他事件可影响总数时隔离在单次事务快照中验证）。
- 多余兵安置：`PartyBefore - PartyAfter == CoolingAfter - CoolingBefore == PlacedCount`（按兵种核对）。
- 正常安置不改变 `Hearth` / `MobilizedHearth` / `ServiceBatches`；正常到期迁移亦不改变这些账本。
- 日志门禁：**非玩家当前封邑**产生 0 条本次新增的 `[PlayerFiefCooling]` / `[PlayerFiefPlacement]` 日志。
- 若 21 人仍未结算，必须能从本次日志看到**具体村庄、批次、WaitWeeks、阻断条件**，否则诊断工作不得标为完成。

## 8. Worker 交付要求

施工完成时请提供：

1. 实际 `HEAD` SHA、是否遇到与本规范不一致的新代码，以及完整改动文件清单。
2. “正常返乡”和“安置”高层调用图，明确零返乡仍执行安置、异常/非法场景仍被保护。
3. 关键实现差异：玩家限定日志判定、每周冷却分支、名册恢复时的到期补结算、安置拒绝原因。
4. 构建命令和实际结果（如 `ModifiedArmy` Release；若环境不支持编译则写 **BLOCKED**，不得宣称通过）。
5. 每个 T01–T15 的 PASS / FAIL / BLOCKED，至少提供可复现日志证据；对需要游戏中才能验证的用例不得把静态推断写成 PASS。
6. 特别分析截图中沙拉斯 **Cooling 21 人**：修复后到底是 `BlockedByRecovery`、`ReadyRosterUnavailable`、过期未结算还是其它原因；如果无法由新日志和存档确定，必须如实写出剩余未知点。
7. 保证修改最小化，不加入新持久化 Saveable 类型，不顺带调整无关征兵/经济/军事制度。

---

## 9. 交给 Codex 的开工指令（可直接复制）

> 请按本文《ModifiedArmy｜玩家封邑兵返乡诊断与安置独立化｜Codex / Worker 施工指导 V1.0》施工。先更新并核对 `GuihuaZhou/bannerlord` 的 `main/HEAD`，逐个定位所列方法。优先删除 `ReturnSelectedFiefTroops` 中阻断零返乡安置的 `returned > 0` 条件，但保留 UI 空选中/非法操作无副作用。新增的返乡、安置诊断只允许打印玩家**当前拥有**的封邑和附属村庄，禁止全地图 AI 日志。为 `WeeklyUpdate()` 补齐玩家返乡批次的实际周数/冻结/名册缺失诊断，为安置拒绝补齐逐村原因；实现安全的到期补结算，绝不能凭空清除冷却或绕过恢复冻结。完成 Release 编译与测试矩阵，交付文件清单、诊断证据、明确的 PASS/FAIL/BLOCKED 和未解决问题。不要把猜测写成已验证根因，也不要为了通过测试修改军事配置或降低安全条件。
