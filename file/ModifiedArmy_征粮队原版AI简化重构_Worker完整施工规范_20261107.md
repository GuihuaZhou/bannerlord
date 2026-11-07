# ModifiedArmy 征粮队：基于原版村民／商队机制的简化重构 — Worker 完整施工规范

> 文档性质：**可直接交给 AI coding worker 的施工任务书**，不是代码完成报告。  
> 仓库：`GuihuaZhou/bannerlord`；本次审计基线 `main@4016b788075344bb531f31c0215a1a7aa5725b22`（2026-11-06 UTC）；**施工前必须重新确认本地 HEAD、分支、工作区改动**。  
> 工作范围：`ModifiedArmy` 的**本地征粮 `LocalForaging`**，兼顾现有 `CrossSettlementAid` 的行为及存档兼容，但**不重构跨城援粮业务规则**。  
> 目标：消除“征粮队待在 B 不采购／待在 C 不解散／不再执行任务”，采用原版 Party 创建、AI 移动及事件驱动，保障真实士兵、粮食、金钱和存档安全。  
> 约束：**只施工用户授权的工作范围；禁止擅自 commit、push、开 PR；禁止声称未做的实机验证为 PASS。**

---

## 0. Worker 首次响应与硬性施工流程

先执行并记录：

```powershell
pwd
git branch --show-current
git rev-parse HEAD
git status --short
Get-Content .\AGENTS.md
Get-Content .\ModifiedArmy\AGENTS.md
Get-Content .\ModifiedArmy\CODE_ORGANIZATION_RULES.md
```

发现 HEAD 已高于本文基线时，以**本地实际代码**优先；凡本任务涉及的函数已经变更，重新审计并在报告中标出差异。若工作区存在无关未提交修改，不得覆盖或重置。

施工前输出简要“基线摘要”：本地 SHA、受影响文件、已存在业务逻辑、原版反编译证据、存档字段、资源写入点、当前已知阻塞。每阶段结束检查 `git diff --check`、关键路径编译、受影响测试，再继续下一阶段。

**强制遵守**根目录 `AGENTS.md`、`ModifiedArmy/AGENTS.md` 和 `ModifiedArmy/CODE_ORGANIZATION_RULES.md`。Model 做纯计算；Behavior 拥有 Campaign 事件／持久化；Service 拥有资源变更；Diagnostics 只读取／记录；禁止第二套货物、士兵、金钱、任务台账。

## 1. 用户确认的业务需求和不可改变边界

1. 征粮队仅在驻军需要外部粮食且来源、护卫、预算、口粮均满足时派出。
2. 采购地点是 **`VillageType.PrimaryProduction.IsFood == true` 的村庄**，或有可购买食物的 `Town`。使用真实库存、真实价格和可达距离；不凭空生成粮食。
3. 每次**一个采购目的地、一次采购、一次返乡、一次归还、解散 Party**。目标失效则空载／带现有货物返乡；本轮不无限改道、不串访多个村庄。
4. 尽量使用原版村民／商队的队伍创建、移动命令和短期逃跑机制；**不自写寻路与持续抢占 AI 的路由控制器**。但原版 AI 对“自定义组件”的避敌能力必须以实际版本证据和游戏内测试证实，不能根据名称假设。
5. 创建护卫仍从真实驻军 `TroopRoster` 提取，原则上维持原来的 **50 名健康护卫**。所有合法调出后**实际驻军不得低于 75 人**；不得抽走在役封邑兵；人数不够就不派。
6. 家乡同一时间最多一支活动本地征粮队；不得让已完成、可恢复或残留旧 Party 永久占住派遣名额。
7. `CrossSettlementAid`（驻军间运粮）是另一个业务；本阶段保持旧任务可载入、可执行，不把它与本地征粮简化状态混用。不得为了简化 Local 破坏 Aid。
8. 必须兼容已存档 `SupplyPartyComponent`、状态值、ID、原有 `SyncData`；特别是已经停在其他友方城镇的旧征粮队。
9. 不调整已有经济参数、军队战略逻辑、封邑兵生产、驻军征兵比例（已确定内陆自动上限 75%、边境 85%，警戒线 50%／75%）或其他模块。

### 1.1 本次不做的事

- 不重写原版商队贸易行为；不继承或伪装为 `CaravanPartyComponent`／`VillagerPartyComponent`。
- 不新建“凭空生成 50 人”的 PartyTemplate；若需要视觉模型，仅沿用现有图标实现。
- 不发明更复杂的战场 AI、不自定义 A* 寻路或每小时无条件覆盖移动指令。
- 不重构 `ModifiedPolitics`、领主 Party 绑定或军队编制系统。
- 不把“征粮队正常结束”和“资源异常待恢复”混作一个状态，更不能在资源未交付时强制解散。

---

## 2. 已经核对的代码与反编译证据

下面是当前 GitHub 基线的**实际入口**。Worker 应在本地用 `rg -n` 复查；所列行数仅用于定位，后续提交可能变化。

| 文件 | 已确认事实／方法 | 本次处理 |
|---|---|---|
| `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs` | 1394 行；`OnDailySettlementTick`、`OnHourlyPartyTick`、`OnSettlementEntered`、`OnGameLoadFinished`、`TryCreateSupplyParty`、`FindBestSource`、`EnsureMovingTo`、`MoveToSettlement` | 拆离本地任务流与选择逻辑，保留事件入口和 Aid 兼容分发 |
| `.../SupplyPartyComponent.cs` | `PartyComponent`，`[SaveableRootClass(1)]`；`SaveableProperty(1..10)`；本地队伍 ID 为 `modifiedarmy_supply_{home.StringId}`；`AvoidHostileActions => false`；`SetDoNotMakeNewDecisions(false)` | 保留类型／序列化身份；新增少量持久化信息；修改本地任务的所有权策略／AI 属性（先验证） |
| `.../SupplyPartyMissionState.cs` | 0 去采购、1 返乡、2 完成、3 援粮去程、4 援粮返程、5 恢复、6 友方休息；模式 0 Local、1 Aid | **原数字不得更改**；新 Local 正常流只用 0/1/2 |
| `.../SupplyPartyTransaction.cs` | `AcquireFoodAtSource`、`PurchaseFoodFromVillage`、`PurchaseFoodFromTown` 在 `partial SupplyPartyBehavior`；Town 还会买骑乘动物 | 把写入职责迁至实际事务 Service；可保持定价／现有成本规则 |
| `.../Services/SupplyCargoService.cs` | `ExtractHealthyEscort`、`TransferTroopsLimited`、`RestoreTroops`、`TransferAllItems`、`PurchaseFood` | 保留低层搬运，明确区别**仅拷贝**和**从源扣除的真实转移** |
| `.../Services/SupplyRecoveryService.cs` | `RestoreLoadedMissions`；`CompleteReturn`：先 `CompleteMission`，后 `RestoreTroops`、`TransferAllItems`，再 `DestroyPartyAction.Apply` | 必须改为验收安全的归还事务；不得完成标记先于交付成功 |
| `.../Services/SupplyRouteController.cs` | 6 小时威胁暂停、路线命令时戳、检测默认 AI 目标偏移 | 从 Local 任务流删除使用；Aid 使用情况先复查 |
| `.../Services/SupplyDispatchService.cs` | 每日派遣、冷却、检测家乡活动任务 | 保留为调度入口，补齐 75 人与恢复占位逻辑 |
| `.../SupplyPartyProvisioning.cs` | 口粮计算、健康风险、友方休整、临时改道等 | 保留必要口粮算法；停用 Local 的反复换目标和永久休整 |
| `.../SupplyPartyConversation.cs` | 征粮队与玩家互动 | 确认重构后交互是否仍能读取组件，不能遗漏 |
| `.../Garrison/Models/GarrisonRecruitmentCapacity.cs` | `GetActiveSupplyEscortCount` 把未完成的外出护卫计入预留编制；`MinimumResidentGarrisonCount = 75` | 保持预留计算真实；完成时不重算两次／不得占用永久虚额 |
| `.../Recruitment/Diagnostics/RecruitmentLogFilter.cs` | `ShouldLog(party/clan)` 只保留玩家王国细节日志 | 复用，不在全图逐小时刷日志 |

### 2.1 原版反编译源码（不是外网猜测）

原版反编译文件在仓库 `code/`，worker 必须**读取其具体调用流程**：

1. `code/VillagerPartyComponent.cs`：`CreateVillagerParty()` → `MobileParty.CreateParty(...)`；`PartyOwner`／`HomeSettlement`；`OnMobilePartySetOnCreation()`、`OnInitialize()` 和 `OnFinalize()`。
2. `code/VillagerCampaignBehavior.cs`：`RegisterEvents()` 注册 `HourlyTickPartyEvent` 和 `SettlementEntered`；`MoveVillagersToSettlementWithBestNavigationType()` 使用 `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty()` 与 `SetPartyAiAction.GetActionForVisitingSettlement()`。
3. `code/CaravanPartyComponent.cs`：创建过程、低攻击性、原版 `PartyTemplateObject` 用于真正商队队伍配置。**不能照搬模板并伪装成商队**。
4. `code/CaravansCampaignBehavior.cs`：`HourlyTickParty()` 管目的地；`OnSettlementEntered()` 管贸易。商队是被 `IsCaravan` 过滤的专用业务，不能自动接管 Supply Party。
5. `code/MobileParty.cs`：`IsCaravan`、`IsVillager` 来自具体组件类型，`AvoidHostileActions` 转发组件属性。**SupplyPartyComponent 不会被原版自动当成商队或村民。**
6. `code/PartyComponent.cs`：`AvoidHostileActions` 默认为 `false`。该属性名字不能证明所有避敌行为均受它控制。
7. `code/AiPartyThinkBehavior.cs`：监听 `CampaignEvents.TickPartialHourlyAiEvent`；在重估时触发 `CampaignEventDispatcher.Instance.AiHourlyTick(party, thinkParams)`；采用长期行为之前检查 `thinkParams.DoNotChangeBehavior`。
8. `code/PartyThinkParams.cs`：存在 `public bool DoNotChangeBehavior`，`Reset()` 会把它重置，因此应在 **AiHourlyTick 事件**里按当前 Party 设置。
9. `code/MobilePartyAi.cs`：`GetBehaviors()` 从默认长期行为出发，经 `MobilePartyAIModel.GetBestInitiativeBehavior()` 处理临时主动／逃跑行为；`SetDoNotMakeNewDecisions()` 影响主动行为选择，**禁止简单设为 true 锁死**。
10. `code/SetPartyAiAction.cs`：`GetActionForVisitingSettlement()` → `SetMoveGoToSettlement()`；目标未变化时避免冗余命令，兼容陆海导航。
11. `code/AiMilitaryBehavior.cs`、`code/AiVisitSettlementBehavior.cs`：原版区分 `IsVillager`／`IsCaravan`，自定义 Supply Party 不一定被默认排除，须对**长期 AI 抢占**做隔离。
12. `code/DestroyPartyAction.cs`：`DestroyPartyAction.Apply()` 触发事件再移除 Party；未完成资源转移前不可销毁。

**反编译缺口：** 本仓库 `code/` 不包含 `MobilePartyAIModel` 的完整实现。Worker 必须利用本地匹配版本 DLL、反编译工具或可执行的实机实验确认：自定义 Party 是否受原版威胁判断保护、`AvoidHostileActions` 的实际效果、被包围时能否正常逃跑。未确认前不得删除旧防护并宣称避敌已完成。

### 2.2 当前明确的错误／失效路径

- **B 城停滞**：Local 采购只由 `OnSettlementEntered()` 执行；`OnHourlyPartyTick()` 的 `TravelingToSource` 仅 `EnsureMovingTo()`，若 `party.CurrentSettlement == component.SourceSettlement` 则无需命令也不采购。保存／读取、改道、事件遗漏时可能永久卡住。
- **家乡 C 不解散**：返乡结算要求 `ReturningHome` 且刚好触发事件；现有小时补偿只覆盖这个状态。`TravelingToSource`、`RestingAtFriendlySettlement`、`Recovering` 与实际已经在家乡不一致时有滞留风险。
- **友方 B 永久休息**：`HandleLocalEscortHealthRisk()` 可使 `RestingAtFriendlySettlement` 等待健康人数／口粮门槛，没有可靠期限；`RestoreLoadedMissions()` 对该状态直接 `continue`。
- **路线争抢**：Local 每小时依次尝试威胁暂停、粮食风险改道、护卫健康恢复、当前路由复核；原版长期／短期 AI 也管理队伍，双方可能重复更改行动。
- **目标评价不合理**：`FindBestSource()` 以家乡为距离起点；村庄 `priority = 2`、城镇 `priority = 1`，无条件优先村庄；连续跨地点会扩大行程与失败面。
- **超额抽调**：本地 `TryCreateSupplyParty()` 直接调用 `ExtractEscortRoster(garrison)`，没有先核实扣出 50 人之后保留至少 75，亦未把受保护的在役封邑兵排除。
- **返还非原子**：`SupplyRecoveryService.CompleteReturn()` 先写 `Completed`；`SupplyCargoService.RestoreTroops()` 只向驻军 `AddToCounts`，**不从仍存活的征粮队扣除**。若后续转移、日志或销毁抛异常，会有重复计兵风险。
- **家乡被占领**：Local `SynchronizeOwnership()` 默认将 Party `ActualClan` 改为家乡当前拥有 Clan；对被攻占后的原护卫及货物权属不安全。
- **固定 Party StringId**：本地创建 ID 为 `modifiedarmy_supply_{homeId}`；必须审计旧队伍残留与重复创建时的识别、冲突及唯一性。
- **持续占位**：`FindSupplyPartyForHome()` 扫描未完成任务，卡住的一支阻塞家乡新任务；必须提供可观测、可恢复的终止路径。

---

## 3. 最终架构：业务状态少，安全事务不简化

### 3.1 新旧文件与真正职责

| 文件 | 操作 | 施工边界 |
|---|---|---|
| `Garrison/Supply/SupplyPartyComponent.cs` | **保留、窄改** | Saveable 身份、家乡／目的地、任务阶段、创建／图标；可追加少数持久化字段；不得承接采购和 AI 评分 |
| `Garrison/Supply/SupplyPartyMissionState.cs` | **保留值** | 本地正常任务只使用 0／1／2；历史 3–6 不得删改数值 |
| `Garrison/Supply/Models/SupplyDestinationModel.cs` | **新增** | 只读筛选 Food 村庄／Town、真实库存、价格可承受性、可达距离、单目标评分、失败原因 |
| `Garrison/Supply/Models/SupplyEscortAdmissionModel.cs`（可选；优先复用现有共享计算） | **必要时新增** | 只读计算驻军真实留守 75、健康普通兵和封邑保护后的可抽调人数；不修改 Roster |
| `Garrison/Supply/Services/SupplyPartyMissionService.cs` | **新增** | 本地任务推进、到达判定、去程→返程、目标失效、超时及异常转恢复；只能通过 Cargo/Recovery 变更资源 |
| `Garrison/Supply/Services/SupplyCargoService.cs` | **保留并加固** | 唯一低层士兵／ItemRoster／资金写入；真实转移＋事务补偿；不持有第二套库存 |
| `Garrison/Supply/Services/SupplyRecoveryService.cs` | **修改** | 读档、旧状态迁移、家乡不合法时的资源安全恢复、完成归还；Aid 原路径不得误改 |
| `Garrison/Supply/Services/SupplyDispatchService.cs` | **保留** | 是否派遣、冷却、家乡活动任务数；派遣实际扣兵前二次审批 |
| `Garrison/Supply/SupplyPartyBehavior.cs` | **显著瘦身** | CampaignEvents 注册、持久化 `_modifiedArmySupplyNextDispatchTimes`、委派本地 MissionService；Aid 保持原路径或薄转发 |
| `Garrison/Supply/SupplyPartyTransaction.cs` | **迁移实现** | `partial Behavior` 中本地采购写入迁往 Cargo/Transaction Service；不要复制两份活跃入口 |
| `Garrison/Supply/SupplyPartyProvisioning.cs` | **窄改** | 口粮、载重及出发预算保留；本地“反复转向紧急食物源／在友城休养”终止使用；Aid 所用逻辑按调用链保留 |
| `Garrison/Supply/Services/SupplyRouteController.cs` | **条件清理** | 移除 Local 的每小时威胁暂停和重复重发；查清 Aid 和其他调用后决定删文件还是保留 Aid 专用功能 |
| `Garrison/Supply/SupplyPartyConversation.cs` | **核对并适配** | 保留玩家对征粮队的合法互动，不能意外中断任务或重复交易 |
| `Garrison/Models/GarrisonRecruitmentCapacity.cs` | **尽量不改公式** | 保留返回护卫占用编制机制；修复脏占位只能从任务结算正确性入手 |

不要把 `SupplyPartyBehavior` 再拆成若干 `partial` 就算完成架构；重构后要把**真正的数据变更**移动到 Service，且老入口移除或明确失活。

### 3.2 三个正常状态与失败单独处理

现有枚举数值：

```text
LocalForaging:
  TravelingToSource = 0
  ReturningHome    = 1
  Completed        = 2
历史 / Aid:
  DeliveringAid = 3; ReturningFromAid = 4;
  Recovering = 5; RestingAtFriendlySettlement = 6
```

正常新 Local 只经历 `0 -> 1 -> 2 -> DestroyPartyAction.Apply`。采购失败也 `0 -> 1`；**不得留在目的地无限重试**。资源异常不强行标记 2，可另设 `RecoveryRequired` 持久化标记隔离；不要扩大正常状态机。

**业务事实优先顺序**：实际 `party.CurrentSettlement`、现有 `MemberRoster`／`ItemRoster`、原本事务提交标记 > 自定义 `MissionStateValue` > 原版长期 `DefaultBehavior`／`TargetSettlement`。这样才不会因仅有地图路由状态不一致卡死。

### 3.3 至少新增的持久化能力

本轮必须显式解决旧 Save 和资源所有权；建议在**现有 `SupplyPartyComponent`** 追加新编号、保留旧字段：

- `OriginalDispatchClan`（创建时 Clan；不与 Aid 的历史 `DispatchClan` 混用）。
- `RecoveryRequired`、`RecoveryReason`（资源不一致冻结与人工可诊断）。
- `LastProgressTime`／任务有效截止时间（低频卡住检测，旧 Save 缺失须安全初始化）。
- 必要时采购／交付提交阶段标记；仅作为当前 Party 的**持久化事务记录**，不创建单独 Campaign 任务总账。

具体字段类型、编号从 `SaveableProperty(11)` 以后递增（**先搜索实际最新声明是否已被占用**），与 `SupplyPartySaveDefiner : SaveableTypeDefiner(20260919)` 保持兼容。新增字段须根据本地游戏的 Save 定义运行验证；旧 Save 缺值要有明确默认行为。若现存字段已经足以提供严格幂等性，可不添加重复字段，但需给出证据。

**特殊风险**：`Completed=2` 不意味着可以无条件销毁残余 Party；若仍有士兵／货物，应先进入可信资源恢复流程。

---

## 4. 原版 AI 所有权：必须先做并单独实测

### 4.1 长期目标的原版事件隔离

在 `SupplyPartyBehavior.RegisterEvents()` 额外监听 `CampaignEvents.AiHourlyTickEvent`，回调仅对**非 Aid、活动且非 Completed 的 Supply Party**执行：

```csharp
private void OnAiHourlyTick(MobileParty party, PartyThinkParams parameters)
{
    if (party?.PartyComponent is SupplyPartyComponent component &&
        !component.IsAidMission &&
        component.MissionState != SupplyPartyMissionState.Completed)
    {
        parameters.DoNotChangeBehavior = true;
    }
}
```

**为什么有效**：`code/AiPartyThinkBehavior.cs` 在同一次 `AiHourlyTick` 事件回调后，检查 `DoNotChangeBehavior` 决定是否将评分最优长期动作写入 Party；而 `MobilePartyAi.GetBehaviors()` 仍单独计算短期行为。**这避免了用 `party.Ai.SetDoNotMakeNewDecisions(true)` 直接锁死短期避敌。**

需要 worker 校验事件订阅签名、执行顺序和本地实际版本。一旦证实其他模组会重复写相同参数或自身与 Vanilla 冲突，记录现象并做**范围最小**的隔离，不要用全局 Harmony Patch 解决一个 Supply Party 问题。

### 4.2 目的地命令只在业务改变时发布

原版导航调用链（项目中已有）：

```csharp
AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
    party, destination, false,
    out MobileParty.NavigationType navigationType,
    out _, out bool isFromPort);
SetPartyAiAction.GetActionForVisitingSettlement(
    party, destination, navigationType, isFromPort, false);
```

调用时机只能是：新任务合法创建后第一次出发；采购或取消后转返乡；读档恢复确认 `DefaultBehavior` 和 `TargetSettlement` 不符；目标失效；原版逃跑结束后原长期指令确实丢失。**不得每小时因 `ShortTermBehavior` 是 `FleeToPoint/FleeToGate/...` 就重发原目标**；允许短期逃跑。

路线恢复门禁：`MapEvent == null`、无港口／陆海转换中、非正在原版逃跑、不是已经到达目标、目标可达且合法，且实际长期目的地不正确。若原版指令正确，就完全不写。

### 4.3 避敌属性是候选手段而非完成保证

可以尝试 `SupplyPartyComponent.AvoidHostileActions => true`、`Aggressiveness = 0`、沿用 `SetDoNotMakeNewDecisions(false)`，但先确认相关属性对 **自定义 Party** 的作用。实际本地 DLL／可执行代码应查：

- `MobilePartyAIModel.ShouldPartyCheckInitiativeBehavior` 与 `GetBestInitiativeBehavior`；
- `AvoidHostileActions` 具体在哪条原版路径被使用；
- 原版商队／村民受到敌人追击时 `DefaultBehavior` 与 `ShortTermBehavior` 的区别；
- 自定义无 `LeaderHero` 的 50 人征粮队是否会触发正常威胁计算。

**门禁 AI-01**：实机观察优于己方的敌队靠近，征粮队短期逃跑、没有主动攻击，脱险后延续同一采购／家乡目标。如果不能证实，阶段 1 标为 `BLOCKED`，不要先移除所有旧避敌措施、另写一套新寻路或向所有 Party 注入补丁。

---

## 5. 单目的地选择模型：逐项实现

建议 `Models/SupplyDestinationModel.cs`：

```csharp
public sealed class SupplyDestinationModel
{
    public SupplyDestinationResult FindDestination(
        Settlement home, int requiredFood);
}
```

`SupplyDestinationResult` 至少包含 `Destination`、`AvailableFood`、`ExpectedPurchase`、`Distance`、`ReasonCode`；只读不写库存或钱。需要复用现有 `CalculateAvailableFood` / `IsValidSource` / `GetSourceOwnerClan`；迁移后仅一处权威定义。

**条件按顺序**：

1. `home` 是合法、未被围困的城堡或城市，有现存驻军；不能选择 `candidate == home`。
2. `candidate.IsTown`，或 `candidate.IsVillage && candidate.Village?.VillageType?.PrimaryProduction?.IsFood == true`。
3. 有合法所有者、可以进入、未被围困／劫掠到无法交易，且家乡所属派系与目标非战争敌对；核实贸易边界是否还需其他过滤。
4. `candidate.ItemRoster` 中确实有 `Item.IsFood` 商品。Town 扣除现有 `MinimumTownMarketFoodReserve = 100`，Village 沿现有规则；保持库存真实。
5. 从家乡用原版 `MapDistanceModel` 计算可达距离，陆海能力／港口可达性与实际 50 人 Party 相符；不使用直线距离代替可达性。
6. 考虑本地代码原有 `MaximumSourceDistance=150`、`MaximumRequestedFood=500`、`VillagePurchaseBatchSize=100`、`TownPurchaseBatchSize=75`、经济和容量约束。**本轮保留既有数值**，仅取消无条件村庄优先。
7. 按候选真实可购量、路程、所需往返口粮和可承受价格排序；同分用 `StringId` 固定顺序，避免每次 Tick 抖动。
8. `CalculateReservedFood` 应仅统计仍真实活动、指向该来源且尚未采购的本地征粮任务；Completed 或返乡不能预占；Aid 是否需要独立预占须复查调用链。
9. 没有合法目标时返回明确 `ReasonCode=NoValidFoodSource`，**不派遣**。

目标选择应保证负担得起至少一笔合理有效采购；禁止有货但钱不足仍派遣远行。不要选择“理论产粮村但此刻无食品库存”的地点。

**评分建议**：先过滤资格，再最大化有效购买数量／距离消耗，避免仅为 1 单位粮食跑远路。不要在首轮引入复杂动态敌情地图评分；敌情由原版 AI 负责短期行动，静态目的地筛选只排除明确敌对／不可达地点。

---

## 6. 本地征粮任务服务与事件表

### 6.1 单一事件入口

建议 `Services/SupplyPartyMissionService.cs` 提供：

```csharp
public void Reconcile(MobileParty party, string trigger);
public void OnSettlementEntered(MobileParty party, Settlement settlement);
public void RestoreAfterLoad(MobileParty party);
public bool TryBeginReturn(MobileParty party, string reasonCode);
public bool TryCompleteAtHome(MobileParty party);
```

在 `SupplyPartyBehavior` 中原有入口委托：`OnSettlementEntered`（及时）、`OnHourlyPartyTick`（低频健康检查，不控制正常路线）、`OnGameLoadFinished`（逐 Party 重建）、`OnSettlementOwnerChanged`（权属变更）、`OnMobilePartyDestroyed`（清理缓存与冷却）。Aid 保持旧入口。

### 6.2 精确判定流程

```text
ReconcileLocal(party):
  guard: party is active; component exists; mode==LocalForaging
  if in MapEvent or transition: record only; do not mutate and do not reissue route
  if RecoveryRequired: only recovery service can act; do not create another task
  if mission Completed:
      if roster or inventory not empty -> RecoveryRequired, log and reconcile
      else -> if party still active: destroy safely
      return
  if mission TravelingToSource:
      if purchase was already committed / visited current source:
          switch to ReturningHome; issue native home order only if needed; return
      if currently at source:
          run TryPurchaseOnce (real item & money transaction)
          switch to ReturningHome after commit or proven zero-purchase
          issue native home order only if needed; return
      if source no longer valid:
          switch to ReturningHome (no new source); return
      if currently at another friendly settlement:
          do NOT purchase there; if stranded and no native avoidance active -> recover original goal
      else if long-term goal incorrect and no native avoidance:
          issue native order once
      return
  if mission ReturningHome:
      if currently at authorized receiving home:
          execute committed transfer, verify, complete, destroy; return
      if home owner changed / receiver unavailable:
          recovery service find safe receiver for original clan or freeze with actionable reason
      else if long-term goal absent and safe to restore:
          issue native home order once
      return
```

**事件缺失兜底**：处于目的地时，即使没有 `SettlementEntered`，也必须通过读档恢复／低频 Reconcile 执行采购；在家乡则相同原则完成归还。不要把“当前实际位置”和“期望位置”混淆。

### 6.3 何时重试、何时明确失败

- 每次采购最多调用一次，且一旦采购事件已记录，绝不再次对同一 Source 扣款。
- 若目的地被围困／敌对／无库存，直接返乡，允许零货物安全返还；不额外选择第二来源。
- 若长期无移动进展：先检查战争／战斗／短期避敌／陆海转换；不要对正在逃跑的 Party 强制设目的地。确定路线持久错误后恢复一次原版命令。
- 若任务截止时间已到：优先返乡，不再采购；返乡同样无进展且确实不可达，转资源恢复状态而非永久休息。
- 检查优先使用已有 `HourlyTickPartyEvent` 但限制必要阶段／条件；新状态只在变化时写，日志只在转移时打。避免新增多个 Timer、全局静态 Party 缓存。
- `OnMobilePartyDestroyed` 清理与该 Party 关联的运行时数据；非正常死亡时不能“原地返还被消灭士兵”，但必须按既有规则清理护卫预留和冷却。

---

## 7. 驻军 75 人底线与创建事务

### 7.1 来源审批必须在 **真实扣兵前** 复核

现有本地 `TryCreateSupplyParty()` 只 `ExtractEscortRoster(garrison)`，应增加独立只读审批：

```text
resident = source garrison's actual resident regular-member count
residentSurplus = max(0, resident - 75)
eligibleHealthyOrdinary = sum(max(0,
    perTroopTotal - wounded - protectedServingFiefCount))
approved = min(50, residentSurplus, eligibleHealthyOrdinary)
require approved >= 50  (本轮维持固定 50 名护卫)
```

`resident` 的类型与 `GarrisonDefenseRequirementModel.Evaluate`、`GarrisonRecruitmentCapacity.MinimumResidentGarrisonCount` 保持一致，**不要使用“实际驻军 + 已出征粮护卫”的 effective 数值来满足留守 75**。受保护名册用 `FiefPartyManager.GetServingTroopCounts(garrison)`，必要时遵循现有 reconcile 的身份安全规则。

| 实际留守 | 受保护兵情况 | 合法 50 人出发？ |
|---:|---|---|
| 74 | 任意 | 否 |
| 100 | 任意 | 否 |
| 124 | 即使 100 名普通健康兵 | 否 |
| 125 | 至少 50 名可抽调普通健康兵 | 是 |
| 150 | 只有 30 名可抽调普通健康兵 | 否 |
| 200 | 至少 50 名可抽调普通健康兵 | 是 |

### 7.2 出发事务步骤

1. 先评价粮食缺口及是否已有本地任务、资金、目标可用性和往返口粮。
2. 预先检查实际驻军可抽调人数；在最终扣兵前重新评估，防止同日其他 Party 已经征调。
3. **只从合格健康普通士兵中抽取 50**，保留逐兵种明细；当扣除不足 50 时原位归还并拒绝创建。
4. 创建 `SupplyPartyComponent`，绑定真实驻军归属 Clan（新本地任务应保存出发 Clan）；保留现有外观逻辑。
5. 创建成功且必要的驮兽／口粮采购或转移完成后，发一次原版去目的地命令。
6. 任一环节失败，回滚护卫、驮兽、载粮、出发费用，销毁已创建的临时 Party；回滚不完整则设置持久化 `RecoveryRequired` 并保留证据，绝不继续生成第二支。

注意 `Cargo.RollbackDeparture()` 在 Party 存活时直接 `RestoreTroops` 加兵然后销毁，要核对异常路径：建议统一使用**可验证的真实源→目标转移**（扣除征粮队士兵），成功后才销毁；重复执行不得增兵。日志抛异常不应触发把已提交业务逆向重做。

### 7.3 固定 Party ID 的处理

当前 `CreateSupplyParty()` 使用 `modifiedarmy_supply_{home.StringId}`，不是按任务生成唯一 ID。施工前全仓搜索 ID 依赖；如果不存在外部固定字符串引用，可给**新** Local Party 使用 `modifiedarmy_supply_{homeId}_{uniqueId}`，旧存档 Party 不改 ID。仍以 `component.HomeSettlement` 与 `component.IsAidMission` 判断活动任务，不能用 StringId 前缀充当业务主键。唯一任务限制在派遣 Service 端保留。

---

## 8. 采购事务（最小闭环，不乱造业务账本）

### 8.1 原实现可复用部分

`SupplyPartyTransaction.cs` 已有 `AcquireFoodAtSource()`，对应 Town／Food Village；`SupplyCargoService.PurchaseFood()` 使用 `source.ItemRoster`、`party.ItemRoster` 及 `GiveGoldAction.ApplyForCharacterToSettlement()`。**保留真实物品和真实支付路径**，不改实际商品价格公式。

### 8.2 需实施的幂等、补偿保护

- 购粮前验证 `SourceSettlement`、当前地点、任务阶段、家乡原 Clan 付款人、库存、最低市场储备、载重、实际金币、原版可进入性。
- 计算本次可实际交付数量和逐物品价格；预先生成源、队伍和付款的快照（只用作异常补偿，不创建独立永久库存）。
- 在事务所有步骤成功前，`MissionStateValue` 不许变成 `Completed`；不能先记录“已购 100”再失败实际只拿到 20。
- 若物品转移成功而付款失败，恢复物品源／目标库存；若不能完整恢复，持久化标记 `RecoveryRequired`，日志记录差额及 `OperationId`，停止正常业务。
- 当采购完成，无论得到 0、少量或满足需求，**最多一次尝试**。可复用现有 `VisitedSettlements` 作为旧 Save 的“已访问”证据，但必须核实它是否足以标识成功提交；必要时在组件追加稳定提交标记，不能只用易失字典。
- 更新 `RequestedFood`（不允许负数）及提交标记，转 `ReturningHome`；后续再触发 `OnSettlementEntered`／`Reconcile` 不执行第二次采购。
- Town 原有购买骑乘动物功能要么保持原流程并纳入同一补偿边界，要么作为单独已获授权的经济行为变更；本轮原则**保留现有业务效果**。
- 正常物流队伍消耗口粮，返程剩余量可能与购买量不同；日志分开列出 `purchasedFood`、`foodConsumedEnRoute`、`deliveredFood`，不得用采购数代替最终到仓数。

**必须说明**：Bannerlord Campaign 通常单线程处理事件，但不把“理论上不会存档到半次事务”作为无条件保障。Worker 应审阅可抛出事件／回调及当前 Save 语义，对可能的部分提交提供持久化操作阶段或严格补偿，并在技术报告注明边界。

---

## 9. 返乡事务与真正销毁 Party

### 9.1 修复现有重复计兵风险

当前实现：

```text
SupplyRecoveryService.CompleteReturn
  -> component.CompleteMission()           // 先写 Completed
  -> Cargo.RestoreTroops(party.MemberRoster, garrison.MemberRoster)
  -> Cargo.TransferAllItems(...)
  -> DestroyPartyAction.Apply(...)
```

`RestoreTroops()` 只是往目标名册 `AddToCounts`，没有从源名册扣除；一旦执行过程中抛出异常、回调异常、没有销毁完成，**下一次处理会保留源名册＋目标已增加数量**。这是必须修复的独立安全问题。

**新返回服务要使用真正消耗源库存的移动**（可改造 `SupplyCargoService.TransferTroopsLimited()` 或新增明确的 `TransferAllTroopsTransactional()`）：

1. 必须在家乡或明确的安全接收定居点；检查原出发 Clan，不把护卫交给占领方。
2. 获取接收驻军当前可用硬容量和护卫预留逻辑。**返乡护卫原本占用 `GetActiveSupplyEscortCount` 的预留编制，不能按普通外部新增士兵重复扣容量**。若实际因其他行为造成容量冲突，记录明确异常，避免删兵。
3. 快照士兵数量、逐兵种伤员数、货物清单和接收方数量；计算可完整交付的计划。
4. 真正从 `party.MemberRoster` 扣除并向接收 Roster 加入（包含伤员）；所有物品同样从 `party.ItemRoster` 扣除后放入接收方。
5. 校验目标得到的数 = 源实际减少的数；异常回滚。若部分异常无法补偿，`RecoveryRequired` 且不销毁 Party。
6. 所有士兵、粮食、驮兽、坐骑、非食品物品均已正确转交，Party 名册确认无应转移资产，再标记 `Completed`。
7. 最后调用 `DestroyPartyAction.Apply(null, party)`；先确保不会因为日志异常破坏提交。非活跃 Party 不重复销毁。

非资源日志可以在成功事务之后输出，日志失败不得二次执行事务。对于部分伤员、救援后 Roster 变动、能力下降的情况，明确是“部分可恢复”还是“完整阻塞”，不得偷偷删除超额士兵。

### 9.2 家乡失陷、接收方消失

**禁止沿用 Local `SynchronizeOwnership()` 直接换成家乡新拥有 Clan** 的逻辑。新任务需持久保存出发时 Clan。处理规则：

- 家乡仍属于原 Clan 且驻军存在：正常返乡。
- 家乡被占或不存在，原 Clan 有合法、友好且可进入的其他驻军：选择原 Clan 的安全接收方，记为恢复目的地；这是**异常回收，不是二次采购**。
- 无合法接收方或存档无法确认原归属：持久化 `RecoveryRequired`，保留在途实体和货物、打印一次稳定 `ReasonCode`，不交给敌军，不凭空转移至别的 Clan。运行时可以有限次重新评估安全接收方，但不得静默销毁。
- 原 Clan 消亡或在途 Party 被实际战斗摧毁：按原版死亡／战利品规则处理并清理预留；不得凭空复活 50 人。

如果旧 Save 的出发 Clan 尚未持久化，优先以可信的旧 `party.ActualClan` 及现有日志关系判断，**不能假定家乡当前 OwnerClan 就是原拥有者**。不可确认时进入受控恢复并报告。

---

## 10. 旧存档状态迁移矩阵

迁移必须发生在 `OnGameLoadFinished()` 的受控恢复中，且对于已在战斗、逃跑或陆海转换中的队伍先延期，只读取不修改。

| 旧任务状态 | 实际位置／名册 | 处理 |
|---|---|---|
| Local `TravelingToSource=0` | 已在 Source，未采购且可确认无提交 | 执行**一次**采购，转返乡 |
| Local `TravelingToSource=0` | 已在 Source，`VisitedSettlements` 已记录或有已提交证据 | 禁止重复采购，直接返乡 |
| Local `TravelingToSource=0` | 仍在 Home，尚未出发 | 如果任务有效，发布一次原版去 Source 命令；若已有货／访问证据，返乡结算 |
| Local `TravelingToSource=0` | 在其他友方 B | 未采购则恢复原 Source；目标已失效则返乡；不在 B 擅自采购 |
| Local `ReturningHome=1` | 在 Home | 完整资产交付后销毁 |
| Local `ReturningHome=1` | 在友方 B | 保持返乡，不进入无限 Resting |
| Local `RestingAtFriendlySettlement=6` | 在 Home | 不等健康门槛，按实际名册安全交付完成 |
| Local `RestingAtFriendlySettlement=6` | 在 B | 旧休整状态取消，尽可能返乡；无法抵达则资源恢复 |
| Local `Recovering=5`（若出现） | 任意 | 先鉴别历史业务模式，确认 Local 后走安全恢复，Aid 按旧逻辑 |
| Local `Completed=2` | Party 仍在，士兵或物品非空 | 禁止直接销毁；优先完成已提交结算或标记恢复 |
| Aid `3/4/5` | 任意 | 本阶段维持旧 Aid 任务服务与行为，不转换成 Local 三状态 |

注意加载后不能只比较 `MissionStateValue`，要对照真实 `CurrentSettlement`、`VisitedSettlements`、`ItemRoster`、`MemberRoster`，再判定下一步操作。**旧状态不是丢弃资源的理由**。

---

## 11. 回调、事件与所有权变更的集成检查

本地重构后以下事件必须各有一个有效监听入口（如仍复用同一个 Behavior，则不重复订阅）：

- `DailyTickSettlementEvent`：驻军缺粮与派遣候选评估。
- `AiHourlyTickEvent`：禁止原版长期 AI 评分覆盖 Local 业务目标；**不影响 Aid、其他自定义 Party 或领主**。
- `HourlyTickPartyEvent`：仅需要的低频健康检查和位置事实核对；不在无变化时改变 AI 命令。
- `SettlementEntered`：即时触发采购／交付；调用同一幂等入口。
- `MobilePartyDestroyed`：清理活动占位和运行时缓存、日志、冷却；处理战损，禁止自动返还真正死亡的兵。
- `OnSettlementOwnerChangedEvent`：来源／家乡失效及时返乡或进入安全接收方恢复；原 Clan 归属不随家乡易主而变。
- `OnGameLoadFinishedEvent`：恢复所有 Local 并保持 Aid 旧处理；已在地点的任务必须直接处理业务事件。
- `OnSessionLaunchedEvent`：既有谈话／交互兼容；只在确有需要时处理 UI。

`_modifiedArmySupplyNextDispatchTimes` 原 `SyncData` Key 不变。`SupplyPartySaveDefiner` type ID、class ID 及字段原编号不变。Any new data must use supported save types and be tested with old saves. Active escort reservations must drop to zero when Party truly completes, and continue to be reserved while safely recovering.

---

## 12. 分阶段施工门禁（不可跳步）

### P0 — 本地基线＋反编译核实

- 记录 `git status --short`、branch、SHA，读取 3 份 AGENTS／架构规定。
- 针对 `LocalForaging` 绘制现有创建→选源→扣兵→前往→采购→返程→归还→销毁完整调用链；列出资源写入点。
- 读 `code/` 指定原版文件，并检查**本机 DLL 版本匹配情况**及缺失的 AIModel 实现。
- 输出本地旧存档测试用例列表和原有 Aid 任务回归用例。

**P0 PASS**：Worker 能明确指出每一项读到的原版行为与源码位置；无凭空认定原版会管理自定义 Party。

### P1 — 先证明原版 AI 长期／短期分工

- 增加 Local 专用 `AiHourlyTickEvent` 的 `DoNotChangeBehavior` 隔离；核实原版持续路径。
- 合理设置非攻击属性，禁止全局禁用 AI；测试敌人靠近／脱离威胁。
- 暂不拆原采购业务，以最小改动证明“长期目标不会被替换、短期逃跑仍有效”。

**P1 失败不得推进**：记录 `BLOCKED` + 本机 DLL／实机证据。不能靠人工每小时抢路线解决。

### P2 — 单目标规划和最小本地任务闭环

- 新建只读 `SupplyDestinationModel`；保留现有数量／价格经济参数。
- 将 Local 正常状态缩减为 0/1/2，取消多地串访／常态反复改道。
- `ReconcileLocal` 实际位置优先、消费事件幂等；B 城进城事件缺失能恢复，Home 返还不依赖事件。
- Aid 原业务、原日志等级保持不变。

**P2 PASS**：正常两种采购来源各完成一次往返；不存在空采后永远滞留。

### P3 — 护卫审批、真实货物／金钱事务

- 75 人驻军留守底线、真实健康普通兵保护、固定 50 护卫；扣兵前二次审批。
- `SupplyCargoService` 归还必须从源减、向目标加；采购与返还异常可补偿且有 `RecoveryRequired`。
- 家乡原 Clan 身份持久化；不转交被占城堡新主人。
- 审查 `CompleteMission`、`DestroyPartyAction.Apply` 的严格顺序与异常路径。

**P3 PASS**：资源守恒、伤员数量守恒（扣除合法战斗伤亡与途中消耗）、无同一操作二次交付。

### P4 — Save 兼容／异常存档恢复

- 新旧存档加载，旧 `RestingAtFriendlySettlement` 和在 B 的 Local 都有明确迁移。
- `Completed` 但 Party 未销毁、家乡被占、驻军对象不存在、被袭击或无路可走都有可观察失败处理。
- 验证 Aid 旧模式不受影响。

**P4 PASS**：保存／读档前后资源、所有权、家乡预留、活动名额一致；无静默销毁。

### P5 — 删除真正失效旧逻辑、验证与交付

- 利用 `rg` 和真实调用链确认 Local 的威胁暂停／食物风险换目标／友城无限休整不再被调用；仅删除没有 Aid 依赖的代码。
- 检查所有本地化 Key、日志翻译占位符、编译、架构及依赖验证。
- 进行 S01–S22 运行时测试，形成日志／录像／存档证据；未执行的必须 `BLOCKED`。

---

## 13. Worker 运行与安全验收矩阵

| ID | 场景 | PASS 标准 |
|---|---|---|
| S01 | Food 主产村庄，有足够库存和资金 | 出发→进村→购买一次→返乡→交粮→解散 |
| S02 | Town 有可售食物 | 正确选择 Town；保留市场食物储备；按实际价付款、一次归还 |
| S03 | 村庄主要产物不是 Food | 不选为采购来源，即使村庄存在少量其他食物 |
| S04 | 目的地 Food Village 库存已空／不足 | 返回零或实际少量物品，**不在 B 休息／无穷换村** |
| S05 | 当前已经位于 B，但 `SettlementEntered` 没触发 | 低频事实检查发现 `CurrentSettlement==Source`，采购一次并返乡 |
| S06 | 当前已在家乡，但 `SettlementEntered` 未触发 | 交粮和护卫并销毁；不依赖状态刚好正确 |
| S07 | Local 在 B 友方城且为旧 `RestingAtFriendlySettlement` | 迁移返乡或明确恢复，不永久休整 |
| S08 | 实际驻军 124 | 50 人征粮队不可出发，兵数／货物／金钱不变 |
| S09 | 实际驻军 125，合格健康普通士兵 >=50 | 出发成功，驻军正好留守 >=75 |
| S10 | 驻军 180，健康普通兵仅 30，其他为在役封邑兵 | 不抽调受保护封邑兵，不派队 |
| S11 | 出发审批通过后、真正扣兵前被其他来源调兵 | 二次审批拦截，不突破 75 |
| S12 | 在路上靠近敌方强势 Party | 原版短期 AI 逃跑／避敌，不主动追击，模块不每小时抢命令 |
| S13 | 脱离威胁 | 恢复原采购／家乡长期目的地，不陷入 Hold |
| S14 | 采购目标被攻占／围困 | 不非法采购；安全返乡；不无限换目标 |
| S15 | 采购途中保存／读档 | 原有任务恢复，不新建重复 Party，不重扣护卫 |
| S16 | 采购完成后保存／读档 | 不二次扣钱或再次购买；仅返乡 |
| S17 | 家乡已有队伍且迟迟没有移动 | 限期健康检查／资源恢复，不永久占用名额；不会叠加创建 |
| S18 | 返乡途中保存／读档 | 实际到达后仅转移一次士兵和物品，正确销毁 |
| S19 | 已有 10 名伤员，返乡前后 | 在没有战斗改变的前提下，逐兵种 Total 与 Wounded 守恒 |
| S20 | 交还时模拟事件／日志异常 | 不出现驻军和 Party 双份士兵；失败可复现并可恢复 |
| S21 | 家乡易主／驻军消失 | 原护卫不交敌方；合法新接收方或持久恢复原因 |
| S22 | 旧 Aid 任务与 Local 共存／连续 30 游戏日 | Aid 完成且不被新 Local 改坏；无永久正常任务闲置；正常预留正确释放 |

所有 Sxx 附 `PASS/FAIL/BLOCKED`、实际验证方式、日志样本、相关存档或代码证据。**单纯编译通过不构成 S05、S12、S18、S20、S22 的 PASS。**

---

## 14. 编译、门禁、日志与本地化

先查看各工具参数再执行；按当前仓库现有脚本选择：

```powershell
git diff --check
.\ModifiedArmy\tools\Test-LocalizationPlaceholders.ps1
.\ModifiedArmy\tools\Test-ArchitectureReleaseGate.ps1
.\ModifiedArmy\tools\Invoke-ReleaseValidation.ps1
.\ModifiedArmy\tools\Test-RuntimeAcceptanceEvidence.ps1
```

如果执行环境没有游戏 DLL、运行版本不符或脚本依赖未齐，标 `BLOCKED` 并说明原因，不能伪造成功。Release 门禁按仓库要求包含 ModifiedArmy／ModifiedPolitics 双模块与依赖方向检查，不能增加 Army → Politics 环形引用。

建议结构化日志 `ReasonCode`（只提出名称，按现有本地化系统新增最终字符串）：

```text
LocalDispatchApproved / LocalDispatchRejected
NoValidFoodSource / InsufficientHealthyEscort / GarrisonResidentFloor
RouteIssued / NativeAvoidanceObserved / NativeRouteRestored
SourceReached / PurchaseCommitted / PurchaseSkipped / SourceInvalid
HomeReached / ReturnCommitted / ReturnDeferred
OwnerChanged / ReceiverUnavailable / RecoveryRequired / Recovered
PartyDestroyed / OldSaveMigration / DuplicateActiveMission
```

每个重要阶段记录 `PartyId`、`HomeId`、`SourceId`、`OriginalClanId`、`StateBefore/After`、`ResidentBefore/After`、`EscortCount`、`ItemBefore/After`、`GoldBefore/After` 和操作结果。每小时风险探针只 `Debug` 且限玩家王国及变化点，不输出全世界全量日志；高频使用 `RecruitmentLogFilter.ShouldLog`。基础文本英语、CNs 翻译中文，检查占位符完全一致。**日志不得重复调用采购、扣兵或更改状态。**

---

## 15. Worker 交付报告模板（必须填满）

```markdown
# 征粮队本地任务重构交付报告

## 仓库状态
- 基线 HEAD / 结束 HEAD：
- 分支 / git status --short：
- 原版 DLL 版本 / 仓库 code/ 反编译版本匹配情况：

## P0 原版机制审计
- Villager 创建与进城、返乡代码位置：
- Caravan 商贸与原版 AI 决策代码位置：
- AiPartyThinkBehavior 对 DoNotChangeBehavior 的消费点：
- MobilePartyAi 对自定义 Party 避敌的实际证据：
- 缺失原版 AIModel 的补充证据／阻塞：

## P1–P5 逐阶段变更
- 阶段结果 PASS/FAIL/BLOCKED：
- 变更文件和方法：
- 删除／失活的旧业务：
- `CrossSettlementAid` 未变部分：
- 没有新增独立台账的证据：

## 安全／存档
- Saveable 编号与 SyncData Key 的变更表：
- Old Save 状态迁移证据：
- 驻军 75 人底线与封邑保护证据：
- 采购资金／物品守恒证据：
- 归还士兵（含伤员）守恒证据：
- 异常补偿及 RecoveryRequired 证据：

## 验收矩阵
- S01–S22：逐项结果 + 日志／存档／操作步骤，未测标 BLOCKED
- Release 构建、架构、翻译占位符、RuntimeEvidence 门禁真实结果：

## 遗留风险
- 必须由用户决策的真实阻塞：
- 下一步仅与本任务直接相关的最小修复建议：

## Git 安全
- 是否存在未获授权 commit/push/PR：必须为否
- git diff --check、最终工作区变更汇总：
```

### 15.1 完成标准（Definition of Done）

只有下面全部成立，才可报告“重构完成”：

- **存在实际简化**：Local 的新任务仅一次目标、一次采购、一次返乡；无第二套控制 AI 的每小时改道系统。
- **长期／短期 AI 不冲突**：原版短期避敌通过实机；目标不会被原版长期军事行为覆盖。
- **不再卡 B、C**：现有在 B 和家乡的任务可由位置事实驱动完成，不依赖事件必达。
- **资源安全**：驻军 75 人底线、保护封邑兵，士兵和伤员、真实物品与资金不重复、不凭空丢失。
- **兼容旧档与 Aid**：所有 Saveable IDs、现有 key 和跨城援粮旧模式保持兼容；不能安全处理的资产以有诊断信息的恢复状态保留。
- **源码整洁**：删除失效逻辑必须依据实际引用，不能用大量新转发 Class 取代旧复杂度。
- **测试诚实**：静态门禁 PASS + 必要的实机／旧档验收 PASS；不能做的明确 BLOCKED。

---

## 16. 施工提示：最容易做错的八件事

1. **不能把 `DoNotMakeNewDecisions` 设为 true 当作防 AI 抢目标。** 应用 `AiHourlyTickEvent` 的 `PartyThinkParams.DoNotChangeBehavior` 隔离长期目标，验证短期避敌。
2. **不能继承 `CaravanPartyComponent` 试图让原版自动管理征粮队。** 原版商队／村民以具体类型判定，征粮队应保留自己的 Component。
3. **不能仅依赖 `SettlementEntered` 触发采购／归还。** 读档或重新定位已在目标内时不会必然重发事件。
4. **不能在 `CurrentSettlement != Source` 时任意在其他友方城采购。** B 是暂时停留点，不自动成为交易来源。
5. **不能用 `(实际驻军+外出护卫)-50 >=75` 作为派遣条件。** 只看目前真实留守，不得动在役封邑兵。
6. **不能把 `RestoreTroops` 当真正的名册转移。** 它仅复制，返回流程必须源头扣除且保证伤员一致。
7. **不能先 `Completed` 再做资源写入，也不能因 home 被占将 Party 归属改成敌方 Clan。**
8. **不能在局部重构中删除 Aid 的 Saveable、ID、状态与运行路径，或靠日志推测代码已经通过实机。**

> **本文件是经当前 GitHub 代码与 `code/` 反编译源码核对后的 worker 施工规范，但未执行仓库实际修改、编译及游戏运行测试。** 开工时以本地工作区真实源代码与相匹配的原版 DLL 为最终技术依据；对原版 AIModel 的完整避敌路径必须补充核实。
