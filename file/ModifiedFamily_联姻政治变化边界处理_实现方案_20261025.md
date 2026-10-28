# ModifiedFamily 联姻政治变化边界处理：设计落地与代码实现方案

> 项目：`GuihuaZhou/bannerlord` / `ModifiedFamily`  
> 文档日期：2026-10-25  
> 代码依据：本轮讨论中核对的仓库版本 `66c75728172c057af22256b8ccd69d3186617dd3`（2026-10-24）。本文为**实现方案**，不表示代码已修改或 PR 已提交。  
> 范围：联姻提议的协商期、反提议期、双方接受后的延迟执行期发生战争、Clan 换国、退出王国、转为不合格 Clan 的处理。**不修改已经成功执行的婚姻。**

## 1. 目标和既定业务约束

本方案沿用先前确认的联姻机制：

1. 联姻由各个 Clan 自主提出并由接收方独立评估；双方对实际利益分别打分。
2. 原提议七天内回应；每条谈判链最多一次反提议；反提议有新的七天回应期。拒绝或正常谈判超时后，同一对 Hero 双向冷却 60 天。
3. 已接受的协议如因被俘、逃亡、参战、在军团等**临时无法执行**，等待恢复，最长 30 天；协议条件（目标 Clan、婚姻方向、礼金）不因等待而被单方更改。
4. 当前联姻范围：**同王国同文化优先，异王国同文化次之，同王国异文化在配置允许时为第三层级，异王国异文化禁止**。小派系、雇佣兵家族、叛军、强盗家族、已灭亡家族等不合格 Clan 不参与。
5. 两个 Clan 所属王国开战时，**尚未完成的婚约立即失效**；停战不自动恢复。失效由外部政治变化引起，不算一方主观拒绝，也不触发 60 天配对冷却。
6. Clan 改换王国后，如果联姻仍合法，按新的王国、文化、关系和双方家族收益重新评估。已同意的协议**不自动更改婚后归属或礼金**，如不能继续履行则终止并重新发起，不将旧协议偷偷改为新条件。

### 1.1 阶段定义

| 阶段 | 现有状态 | 政治变化后需要的行为 |
| --- | --- | --- |
| 原始提议等待答复 | `PendingResponse`, `CounterCount=0` | 必要时失效；否则等待接收方基于最新状况作决策 |
| 反提议等待答复 | `PendingResponse`, `CounterCount=1` | 必要时**整条谈判链**失效；否则仅可接受或拒绝 |
| 双方已接受，等待实际结婚 | `AcceptedPendingExecution` | 战争/硬性不合法：立即失效；仅政治利益变化：双方重新评估是否履约 |
| 婚姻 Action/礼金执行中 | `Executing` 及其 `ExecutionStep` | **不得在 Action 中途任意回滚/重复执行**；根据执行阶段进行终态确认及恢复 |
| 婚姻已完成 | `Completed` | 不拆散夫妻、不回收礼金、不撤销 Hero 迁族 |

## 2. 事件—行为矩阵

| 事件 | 协商期间 | 已接受、延迟执行 | 冷却 |
| --- | --- | --- | --- |
| 双方所属王国宣战 | 立即 `Invalidated(AtWar)` | 立即 `Invalidated(AtWar)`，若尚未进入执行 | 无 |
| 战争结束（此前已失效） | **不恢复**，必须发新请求 | **不恢复** | 无 |
| 一方改换王国，双方仍同文化、未开战 | 保留，接收方重新评分 | 双方重新评估；仍愿履约则继续等待执行 | 无 |
| 一方改换王国，从同国异文化变为异国异文化 | 立即 `Invalidated(CultureForbidden)` | 立即失效 | 无 |
| 一方加入敌对王国 | 立即 `Invalidated(AtWar)` | 立即失效 | 无 |
| 一方离开王国、`Clan.Kingdom == null` | 立即 `Invalidated(InvalidClan)` | 立即失效 | 无 |
| 一方变成雇佣兵/小派系/叛军/强盗等 | 立即 `Invalidated(InvalidClan)` | 立即失效 | 无 |
| 两个 Clan 同时改换到同一新王国 | 按变化完成后的最终状态重新验证；合法则保留 | 同左并重新评估 | 无 |
| 一方普通拒绝或正常谈判超时 | `Rejected`／既有超时终态 | 不适用 | 60 天（依先前规则） |
| 任一 Hero 被俘、参战、进入军团 | 继续协商 | 继续等待临时状态恢复，不超过 30 天 | 无 |
| 婚姻已完成后两国宣战 | 不适用 | 不撤销已完成婚姻 | 无 |

**注意：** 一次“离开旧王国 → 加入新王国”在引擎层可能经过多个事件或中间态。初版规则将持久有效的无王国归属视为非法；实现时应在事件结束后读最终状态，必要时对该次复合变更做延迟再验证，避免将切换过程中的短暂 `null` 误认为最终状态。

## 3. 当前 Git 代码核对及明确缺口

以下结论以已经查阅的仓库文件为依据：

| 文件 | 现状 | 缺口/应改动 |
| --- | --- | --- |
| [`MarriageProposalModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageProposalModel.cs) | `CanProposeMarriage()` 调用 `OriginClanA.IsAtWarWith(OriginClanB)`、`CultureAllowed()`；`CanRespond()` 复用合法性校验；`CanExecuteMarriage()` 复用回应校验 | 有合法性判断，但仅**被动调用**；并未解决宣战时立刻终止、签约后重评分 |
| [`MarriageEligibilityModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageEligibilityModel.cs) | `CanUseClan()` 排除 `Kingdom == null`、雇佣兵、小派系、叛军、强盗、灭亡 Clan | 保留统一硬性资格校验，避免各入口重复条件 |
| [`FamilyCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs) | 已注册 `DailyTickClanEvent`、`DailyTickEvent`、`BeforeHeroesMarried`、`OnHeroChangedClanEvent` | **未注册 `WarDeclared` 和 `OnClanChangedKingdomEvent`**；待执行协议的每日循环也未将不可恢复的政治失效统一终结 |
| [`MarriageExecutionService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Execution/MarriageExecutionService.cs) | `CanExecuteMarriage()` 返回不允许或临时阻碍时，统一写等待日志然后返回 | `AtWar` / `CultureForbidden` / `InvalidClan` 等永久错误可能被错误当作等待处理，直至 30 天截止 |
| [`FamilyProposalService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs) | `Invalidate(p, reason, false)` 可终止、释放 Hero 锁、不加冷却；`TryReject()` 会加冷却 | 政治失效必须走 `Invalidate(..., false)`，**不能调用 `TryReject()`**；检查反提议链关联记录一致性 |
| [`ClanMarriageDecisionService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs) | `Respond()` 在 `CanRespond()` 失败时调用 `TryReject()` | **需改为区分客观非法与主观利益拒绝**；否则宣战导致的失效会产生错误冷却 |
| [`ClanHeroValueModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs) | 使用 Clan relation、文化、同王国加分；此前发现成员流入/流出计分方向错误 | 签约后政治重评分必须先基于修正后的双方收益模型，不能沿用已知偏差公式 |

### 3.1 本体事件入口（已在仓库 `code/CampaignEvents.cs` 查证）

```csharp
// 本体声明的泛型签名，使用时按实际引用程序集补齐 using
CampaignEvents.WarDeclared
// IMbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>

CampaignEvents.OnClanChangedKingdomEvent
// IMbEvent<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>

CampaignEvents.OnClanDestroyedEvent
// IMbEvent<Clan>
```

> `WarDeclared` 和 `OnClanChangedKingdomEvent` 已核验存在；监听器回调方法的具体参数应严格匹配当前编译使用的游戏 DLL，不应仅从此文复制后忽略版本差异。必要时也应订阅 `OnClanDestroyedEvent`，统一处理家族解散。

## 4. 代码变更明细（推荐的最小安全补丁）

### 4.1 `FamilyCampaignBehavior.RegisterEvents()` 增加监听

```csharp
CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, OnClanDestroyed);
```

监听器**只做范围筛选和调度**，不直接写 `p.Status = ...`。对相关活跃提议调用 `FamilyProposalService` 暴露的统一状态转移方法。应以 `OriginClanA`、`OriginClanB`、`ProposerClan`、`ReceiverClan` 的当前王国作范围过滤，避免每次事件扫描无关数据；初期可先扫描活跃提议，数据量大后引入 Clan→提议索引。

### 4.2 统一实现 `RevalidatePoliticalConditions(...)`

建议新建 `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriagePoliticalValidationModel.cs`，或者作为 `MarriageProposalModel` 的单独公开方法；推荐独立的纯校验函数以便测试。

伪代码：

```csharp
PoliticalValidationResult RevalidatePoliticalConditions(FamilyProposal p)
{
    // 若 p 已结束，则不再触发状态改变。
    // Executing / PaymentReconcileRequired 要走执行恢复，不走普通撤销。

    var a = p.OriginClanA;
    var b = p.OriginClanB;
    if (!MarriageEligibilityModel.CanUseClan(a).IsAllowed ||
        !MarriageEligibilityModel.CanUseClan(b).IsAllowed)
        return Invalid(InvalidClan);

    // 先检查战争：对于双方当前所属王国的敌对状态立即作废。
    if (a.IsAtWarWith(b))
        return Invalid(AtWar);

    // 由现有 MarriageProposalModel 的统一文化规则判断，
    // 不要复制一个可能与 CanRespond 不一致的实现。
    if (!CultureAndKingdomAllowed(p.HeroA, p.HeroB))
        return Invalid(CultureForbidden);

    // 检查两个参与 Hero 仍属于约定 Clan 等其他协议硬约束。
    if (p.HeroA.Clan != p.OriginClanA ||
        p.HeroB.Clan != p.OriginClanB)
        return Invalid(ClanChanged);

    return Allowed(PoliticalContextFingerprint(a, b));
}
```

`PoliticalValidationResult`、`PoliticalContextFingerprint` 为**建议新增的抽象结构**，不是仓库已存在 API。`CultureAndKingdomAllowed` 也应通过重构现有私有 `CultureAllowed()` 共享逻辑；不要为它复制一套规则。

### 4.3 `FamilyProposalService` 集中完成失效处理

建议新增类似 `TryInvalidateDueToPoliticalChange(...)`：

- 校验请求仍是活跃且未进入原版婚姻 Action 的可撤销阶段。
- 幂等设置 `Invalidated` 和 `ReasonCode = AtWar | InvalidClan | CultureForbidden | PoliticalInterestChanged`。
- **释放整条 ChainId 的 Hero 锁**；检查原请求／反提议只有一份活跃记录。
- 不增加 `_cooldowns`，不产生“拒绝”事件。
- 保存请求终态、具体事件时间和双方 Kingdom 信息（需要留历史时添加 `SaveableProperty`，遵守已有存档 ID 分配规则）。
- 输出 `Info/Notice` 级别的原因日志，不要让业务代码误将失效当作失败后重试。

政治收益降低而**合法性仍成立**时，不直接由纯校验函数结束；转入签约后重新评估（见下节）。

### 4.4 在协商阶段和待执行阶段分别处理

**`PendingResponse`（包括反提议）：**

1. 接到政治事件，若硬性条件无效，立即 `Invalidate(..., false)`。
2. 如果仍合法，保留同一个 `ProposalId`、`ReplyDeadline`、`CounterCount`，不重置七天期限。
3. 到原定答复日，AI 以最新双方王国、relation、礼金、成员流动情况评分；玩家 Clan 的答复仍由玩家操作。
4. 不因换国而额外赠送一次反提议机会：一条链最多一次。

**`AcceptedPendingExecution`：**

1. 宣战、身份非法、异国异文化 → 立即 `Invalidated`，不得等 30 天。
2. 仍合法但政治上下文改变 → 按**当前固定条款**重新评估双方 Clause 的履约意愿（AI Clan）；若任一 AI Clan 明确不愿继续，记政治变化失效，无冷却。
3. 已接受协议不允许修改入赘方向或礼金；修改只能终止旧协议并发起新谈判。
4. 如果未来允许玩家 Clan 参与 AI 联姻，**不得由 AI 自动替玩家撤销已同意婚约**：应使用 UI 明确确认，或保留协议直至玩家操作；这属于玩家协商 UI 的扩展任务。仓库现有 `EnableAiToPlayer = false`，第一版暂不触及。
5. Hero 被俘、逃亡、战斗、军团仍只是**临时执行阻碍**，不与政治失效混淆。

**特别约束：** 签约后利益重评分须以已修正的 `ClanHeroValueModel` 为基础，并只在“王国/效忠对象等重要政治背景发生实质变化”时进行；不能因每天微小 relation 或评分波动，在无政治事件时反复取消已达成协议。

### 4.5 修复 `MarriageExecutionService` 的永久错误分流

当前代码在 `CanExecuteMarriage()` 返回不允许时通常只写等待日志。应将判断放到执行服务之外的生命周期控制中：

```csharp
// 示意：由 FamilyCampaignBehavior / ProposalService 做状态迁移
var validity = politicalValidator.Validate(p);
if (!validity.IsAllowed)
{
    proposalService.Invalidate(p, validity.Reason, cooldown: false);
    return;
}

var execution = marriageModel.CanExecuteMarriage(p);
if (execution.TemporaryBlockers.Count > 0 ||
    execution.ReasonCode == FamilyReasonCode.InsufficientFunds)
{
    // 临时等待或按财务恢复机制处理；仍受 ExecutionDeadline 约束。
    return;
}
if (!execution.IsAllowed)
{
    // 其他永久错误不得无限期写“等待执行”。
    proposalService.Invalidate(p, execution.ReasonCode, cooldown: false);
    return;
}

executionService.TryExecuteMarriage(p);
```

不要在 `Executing`、`MarriageApplied`、`GoldSettled` 等已进入不可安全回滚的状态上重新调用普通 `Invalidate`；必须使用现有婚姻执行恢复/礼金结算流程，保证不重复结婚或扣款。

### 4.6 修复 `ClanMarriageDecisionService.Respond()` 的拒绝分类

```csharp
var valid = proposalModel.CanRespond(p);
if (!valid.IsAllowed)
{
    // 战争、非法王国、文化不允许、成员已转 Clan 等：客观失效
    service.Invalidate(p, valid.ReasonCode, cooldown: false);
    return;
}

// 合法但原方案家族收益不足：主观拒绝或唯一一次反提议
// 这类才走 TryReject / TryCounter，按原有冷却规则处理。
```

这样既解决“开战仍等待执行”，也解决“因战争无效却按拒绝增加 60 天冷却”。需要同步检查 `FamilyCampaignBehavior.OnDaily()` 中玩家超时、收养请求等分支，确保业务分类一致。

## 5. 事件响应顺序、重复触发与存档

1. **及时性：** `WarDeclared` 事件在正常提议等待期间立即触发失效；`OnClanChangedKingdomEvent` 标记/校验所有关联提议。不能只靠 `DailyTickEvent`，否则玩家在战争发生到下一次日结算之间仍可能点击接受。
2. **最终拦截：** 每次 `TryAccept()`、`TryCounter()` 以及真正调用 `MarriageAction.Apply()` 前，强制执行统一政治校验。事件负责及时通知，校验负责保证正确。
3. **顺序：** `ClanDestroyed/InvalidClan`、`AtWar`、`CultureForbidden`、`ClanChanged` 等永久失败优先；其次处理政治收益重新评估；最后才处理俘虏、战斗、军团等临时执行阻碍。
4. **幂等性：** 重复宣战/换国事件、同日 DailyTick、加载恢复不得重复写失效、重复释放锁、重复收取礼金、重复执行婚姻。
5. **中间态：** 对连续换国事件，读取事件后最终 `Clan.Kingdom`，必要时将同一变更链上的临时中间态延迟到安全的 campaign 更新点复核；一旦实际战争成立则无需等该延迟。
6. **存档恢复：** `SyncData()` 后现有 `RebuildIndexes()` 恢复锁定状态；下一次安全的 CampaignTick 再核对活跃请求的政治合法性，不在反序列化过程中改变世界。
7. **存档兼容：** 若增加 `SaveableProperty`，只追加新的编号，不复用或重排既有 ID；旧存档缺省新字段时按兼容路径恢复。
8. **日志：** 区分 `Invalidated[AtWar]`、`Invalidated[InvalidClan]`、`Invalidated[CultureForbidden]`、`Invalidated[PoliticalInterestChanged]`、`Rejected[LowBenefit]`、`Waiting[Prisoner/InArmy]` 等，便于检查真实比例。

## 6. 实施步骤（不修改 Git 仓库，仅供开发落地）

| 优先级 | 开发任务 | 必须完成的验收 |
| --- | --- | --- |
| P0 | `FamilyCampaignBehavior` 补事件监听与关联提议定位 | 宣战当日待回应/待执行协议立刻失效 |
| P0 | 统一政治硬约束校验和 `FamilyProposalService` 幂等终止 | 失效释放锁、不产生 60 天冷却；停战不恢复 |
| P0 | `Respond()` 与执行入口永久/临时失败分流 | 宣战不能转为普通拒绝；战争不能以“等待”混过去 |
| P1 | 换国后合法情形保留，并在回应期按新状态打分 | 同文化跨国变化不会被无条件取消 |
| P1 | 已签约后仅对实质政治变化重新评估双方意愿 | 不私自修改礼金/入赘；政治变更可终止 |
| P1 | 联姻评分修正（与成年成员数、外嫁/入赘和 relation 一并落地） | 不依赖先前已知偏差公式判断是否履约 |
| P2 | 存档兼容、最终执行前复核、异常日志 | 重载后失效不恢复；不重复结婚/付款 |

## 7. 验收用例

| ID | 初始状态 → 事件 | 预期 |
| --- | --- | --- |
| T01 | 原始提议第 3 天 → 两国宣战 | 立即 `Invalidated(AtWar)`，释放双方 Hero，冷却为 0 |
| T02 | 反提议第 5 天 → 两国宣战 | 唯一活跃反提议失效；原提议保持已取代状态；谈判链不恢复 |
| T03 | 双方已接受，Hero 被俘 → 两国宣战 | 立即失效，释放 Hero；获释后不自动成婚 |
| T04 | 两国宣战使协议失效 → 当月议和 | 不恢复旧 ProposalId，不自动结婚 |
| T05 | 同国同文化原始提议 → 一方改投其他和平王国，文化不变 | 合法，七天截止日按最新状态回应 |
| T06 | 同国异文化原始提议 → 一方改投其他王国 | 异国异文化非法，立即终止 |
| T07 | 已接受协议 → 一方改投和平王国，仍同文化，双方收益均可接受 | 保留条款，恢复 Hero 后执行 |
| T08 | 已接受协议 → 换国后政治收益下降，AI Clan 不愿履约 | 记录政治变化失效，无拒绝冷却 |
| T09 | Clan 离开王国成为独立 Clan | `Invalidated(InvalidClan)`；避免永久等待 |
| T10 | Clan 改换敌对王国 | `Invalidated(AtWar)` 优先于普通换国重评分 |
| T11 | Clan 变为雇佣兵/小派系 | `Invalidated(InvalidClan)` |
| T12 | 战争发生与答复截止同日 | 永久失效优先，不能先接受再结婚 |
| T13 | 结束协议后再次触发同一宣战事件 | 状态不变，不重复日志/释放锁 |
| T14 | 战争期间保存重载 | 旧婚约维持失效，不能被 DailyTick 再执行 |
| T15 | Hero 被俘但无政治变化 | 已接受协议继续等待，最长 30 天 |
| T16 | 已成功结婚并完成礼金 → 两国宣战 | 配偶、Clan 和已完成协议状态保持不变 |
| T17 | `MarriageApplied` 后处于支付异常恢复状态 → 战争发生 | 不重新执行婚姻，不简单清除已发生的付款义务 |
| T18 | 快速连续“离国→入国”事件 | 以最终政治状态验证，避免中间态误取消合法协议 |

## 8. 实施边界与未验证点

- 当前仓库 `ClanHeroValueModel` 存在已分析过的成员流动收益计算问题；**政治变化后的双方重新评分**必须在该公式修复后启用，否则会得到偏高的同意率。
- `OnClanChangedKingdomEvent` 及 `WarDeclared` 泛型签名以查阅的源文件为依据；编译仍须以实际引用的 Bannerlord 游戏 DLL 为准。
- “换国后仍合法但双方已签约的重新评分阈值”此前尚未做长周期数值标定；建议只按**明确的政治上下文变化**触发，避免已确认婚约被细微关系波动随机取消。
- AI 对玩家 Clan 的自动联姻在当前配置为关闭；若未来开放，应增加 UI 再确认，而不是让 AI 替玩家决定取消已接受协议。
- 本文未执行构建、游戏端测试或修改仓库，不宣称崩溃/逻辑问题已经修复。

---

**核心验收标准：** 未完成婚约遇到宣战立即失效；换国不一刀切，而是按现行政治合法性和已约定条款处理；临时 Hero 状态只延后执行；所有客观政治失效均不误算为普通拒绝，也不产生 60 天冷却。
