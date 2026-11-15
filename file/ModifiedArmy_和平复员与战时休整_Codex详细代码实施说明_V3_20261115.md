# ModifiedArmy｜和平复员与战时休整：Codex 代码级实施说明 V3

> **交付对象**：Codex，按本文件逐项修改源码，不需要重新推演业务方案。正文的“推荐修改”均为拟实施方案，**不代表已经写入仓库**。源代码事实来自 2026-11-15 实时读取的 `GuihuaZhou/bannerlord` 默认分支 HEAD `79998373c90204634b80f3a6582be82ea3723656`。开工先检查本地 HEAD、`git status --short` 和适用的 `AGENTS.md`；若 HEAD 已改变，以新的真实代码定位修改点。
>
> **唯一交付目标**：让和平复员和战时休整从配置、计划、AI导航、到达执行、实际人数统计、读档恢复形成完整闭环。**不要顺手“重构所有征召、创建Party、全局事务、财政或封邑系统”。**
>
> **用户决策**：所有兵员移动同步执行；合法性检查完先生成确定的迁移清单；健康兵和伤兵分别处理；先向目标写入再从来源扣除；信任本体 API 的明确返回语义；**禁止新增回滚、补偿、冻结、人工解冻、重放补救状态机**。既有共享代码中有旧的这类处理，不得为启用两个功能擅自大范围修改；凡会实际阻塞本次功能者，以准确调用链说明并在最小范围解决，不能暗中扩范围。

## A. Codex 快速施工单（先执行这里，再参考下方方法细节）

| 顺序 | 要修改的准确位置 | 最小目标 | 完成标志 |
|---|---|---|---|
| A1 | `ModuleData/militaryConfig.xml`、`common/MilitaryConfig.cs`、`WarRestPlanningService.BuildContext` | 开启和平两个开关 + XML 驱动 WarRest | 运行时能读到 `true/true/true`，战时审批不再永远报 `AutomaticWarRestDisabled` |
| A2 | `UnifiedForceContracts.cs`、`ForceManagementTypes.cs`（一般无需改后者）、`PartyForceCandidateService.BuildContext` | 增加和平复员决策与候选来源/目的地 | 真正产生有接收能力的合法目标，批次ID不丢 |
| A3 | `AiForcePlanningModel.BuildDailyPlan / BuildWarRestPlan` | 新和平任务；WarRest/和平任务续用稳定 TaskId | 同一有效目的地连续多天不会重建任务 |
| A4 | `AiForcePlanningBehavior.OnAiHourlyTick / RevalidateLoadedTask` | 和平任务走已有原生 AI 评分导航 | 合法、非军情时AI能主动前往；遇战/危险可让本体AI接管 |
| A5 | `PartyVisitExecutionService.Execute`、`DemobilizationBehavior.OnNaturalVisit` | 新增和平任务到达执行；普通访问时先试复员、后普通捐赠 | 每日每队每据点只执行一次，普通捐赠不再永久抢先 |
| A6 | `DemobilizationService.TryExecuteAtSettlement`、`TroopTransferService.TransferFieldToGarrison` | 只处理合法真实来源封邑；普通兵按迁移清单目标先加后减 | 数量、伤兵、额外安置、日额度正确 |
| A7 | `WarRestPlanningService`、`AiForcePlanningBehavior.SyncData` | 保留 WarRest 实际执行链并保存当天完成数 | 当天存读档不能清空休整30人的额度 |
| A8 | 行为验证、编译/脚本与游戏记录 | 覆盖平战切换、跨来源、军情、伤兵和最新创建Party | `PASS/FAIL/BLOCKED` 实证报告，无未经测试的“已验收” |

**禁止触碰的主要新功能**：`PartyCreationCoordinator`、`InitialPartyMobilizationCommitter`、`InitialPartyMobilizationPlanner`、`PartyCreationInitializationScope`、`FiefPartyRecruitment` 中的初始部队创建提交逻辑。最新 `7999837` 已让创建方使用 `PartyExchangeService.Execute`，所以**不要把 `PartyExchangeService.ExecuteRosterTransfer` 当作本次普通复员迁移的重构场所**。

## B. 实际调用链与设计切入点

### B1. 当前现有链：和平自然访问

```text
PartyExchangeBehavior.RegisterEvents
  -> SettlementEntered / DailyTickPartyEvent
  -> EvaluateVisit（每日队伍+地点去重；创建期间 IsInitializing 直接 return）
  -> AiForcePlanningBehavior.ExecuteAtSettlement
  -> PartyVisitExecutionService.Execute
     -> 没有目的地匹配的Task
        -> ExecuteRoutineDonation（若成功提前 return，挡住复员）
        -> TryPeaceDemobilization
           -> DemobilizationBehavior.OnNaturalVisit
              -> 强制 IsBoundSettlement
              -> DemobilizationService.TryExecuteAtSettlement
                 -> 真实封邑批次返乡
                 -> 未绑定封邑兵安置（可选）
                 -> 普通兵转驻军
```

**本次修改**：当 Task 是新 `PeaceDemobilize` 且目的地匹配时直接调用复员；没有匹配Task的自然访问仍可复员，但**先尝试复员、再尝试普通捐赠**。原生特殊占领捐赠仍由 `PartyExchangeBehavior` 当前更上层逻辑优先处理，不改那条顺序。和平复员只对 Mod 管理 AI 领主 Party 生效。

### B2. 当前现有链：战时休整

```text
AiForcePlanningBehavior.OnDailyTick
  -> PartyForceTaskCoordinator.EvaluateParty
  -> PartyForceCandidateService.BuildContext
  -> WarRestPlanningService.BuildContext  [Enabled=false 硬编码]
  -> WarRestPolicyModel.Evaluate
  -> AiForcePlanningModel.BuildDailyPlan
  -> BuildWarRestPlan [每次重建任务]
  -> AiForcePlanningBehavior.OnAiHourlyTick [已能给 WarRest 目的地加分]
  -> 目标封邑 -> PartyVisitExecutionService.ExecuteWarRest
  -> PartyExchangeRunner.Execute(FiefReturn)
     -> PartyExchangeService.ExecuteFiefReturn
     -> FiefPartyManager.TryReturnFiefTroops
     -> RecordCompleted(真实返回数) [已经存在]
  -> 可选 FiefTroopPlacementService.TryPlaceAfterReturn
     -> RecordCompleted(额外安置数) [另一条已存在]
```

**本次修改**：读配置开启；复用同一有效 Task；真实批次与目的地相配；每日已返乡数量可以跨存读档；到达重新审批并严格按战时当前 `TargetCount` 限制；**不能重复计入 `ExecuteFiefReturn` 已记录的实际人数**。

## C. 逐文件“能照着写”的实现说明

### C1. `ModuleData/militaryConfig.xml`

**精确位置**：`<AiRecruitment><ForceManagement>` 内第150行 `<Demobilization>` 附近；保持和平目标（`PeaceTargetRatio=0.35`、`PeaceMinimumCount=50`）和驻军上下限全部不变。

```xml
<Demobilization>
  <EnableAutomaticDemobilization value="true"/>
  <EnableDemobilizationRouting value="true"/>
  <PeaceStabilityDays value="3"/>
  <MaxDailyDemobilizationRatio value="0.20"/>
  <MaxDailyDemobilizationCount value="30"/>
</Demobilization>
<WarRest>
  <Enabled value="true"/>
  <MaximumDailyCount value="30"/>
  <MinimumUrgencyScore value="0.75"/>
  <DispositionWeight value="0.30"/>
  <FinanceWeight value="0.30"/>
  <ServiceWeight value="0.40"/>
</WarRest>
```

**阈值 0.75 是源代码默认值，不改 0.65**。若部分战争场景评分一直低于0.75，先日志区分“配置已开启但业务不批准”，不要偷偷调参。

### C2. `common/MilitaryConfig.cs`

现有层级：`MilitaryConfig.AiRecruitment.ForceManagement`（`ForceManagementConfig` 在约150行），已有 `DemobilizationConfig`；XML 解析位于 `ParseForceManagement` 附近的 `node.SelectSingleNode("Demobilization")` 后。

**添加一个且仅一个配置对象**：

```csharp
public sealed class WarRestConfig
{
    // 旧 XML 缺少 WarRest 时关闭，防止意外启用旧配置。
    public bool Enabled { get; internal set; } = false;
    public int MaximumDailyCount { get; internal set; } = 30;
    public float MinimumUrgencyScore { get; internal set; } = 0.75f;
    public float DispositionWeight { get; internal set; } = 0.30f;
    public float FinanceWeight { get; internal set; } = 0.30f;
    public float ServiceWeight { get; internal set; } = 0.40f;
}

// 追加到 ForceManagementConfig，不修改已有 Demobilization 属性：
public WarRestConfig WarRest { get; } = new WarRestConfig();
```

**XML解析位置**：紧接现有 `<Demobilization>` 的解析代码，在相同的 `ForceManagement` 节点调用已存在的 `BooleanValue`、`NonNegativeValue`、`BoundedFloatValue`。以下是接近可直接复制的逻辑（局部变量 `value` 是现有 `ForceManagementConfig` 参数，不要新建第二份配置）：

```csharp
XmlNode rest = node.SelectSingleNode("WarRest");
if (rest != null)
{
    WarRestConfig w = value.WarRest;
    w.Enabled = BooleanValue(rest, "Enabled", false);
    w.MaximumDailyCount = NonNegativeValue(
        rest, "MaximumDailyCount", w.MaximumDailyCount);
    w.MinimumUrgencyScore = BoundedFloatValue(
        rest, "MinimumUrgencyScore", w.MinimumUrgencyScore, 0f, 1f);
    w.DispositionWeight = BoundedFloatValue(
        rest, "DispositionWeight", w.DispositionWeight, 0f, 1f);
    w.FinanceWeight = BoundedFloatValue(
        rest, "FinanceWeight", w.FinanceWeight, 0f, 1f);
    w.ServiceWeight = BoundedFloatValue(
        rest, "ServiceWeight", w.ServiceWeight, 0f, 1f);
    if (w.Enabled && (w.MaximumDailyCount <= 0 ||
        w.DispositionWeight + w.FinanceWeight + w.ServiceWeight <= 0f))
        throw new InvalidOperationException("Invalid enabled WarRest configuration.");
}
```

**注意**：`NonNegativeValue` 允许零；所以启用而最大数为0时，在这里直接拒绝配置，比进入游戏后永远不触发更容易定位。`BoundedFloatValue` 已存在，别重造解析助手。只在加载时输出一次生效参数，不按每天每Party打印配置日志。

### C3. `ForceManagement/Services/WarRestPlanningService.cs`

**精确修改**：`BuildContext` 第61-64行原来写死：

```csharp
Settings = new WarRestPolicySettings { Enabled = false },
```

改为从**同一份现有配置**投影成策略只读输入（不要改 `WarRestPolicyModel` 的签名）：

```csharp
WarRestConfig w = MilitaryConfigManager.Instance
    .RequireActiveConfig().AiRecruitment.ForceManagement.WarRest;

// WarRestContext 的 Settings 赋值：
Settings = new WarRestPolicySettings
{
    Enabled = w.Enabled,
    MaximumDailyCount = w.MaximumDailyCount,
    MinimumUrgencyScore = w.MinimumUrgencyScore,
    DispositionWeight = w.DispositionWeight,
    FinanceWeight = w.FinanceWeight,
    ServiceWeight = w.ServiceWeight
},
```

补充 `using ModifiedArmy.common;`（若当前文件已有则不重复）。`BuildBatchCandidate` 已经从权威 `FiefPartyManager.GetServingDetachments` 读取 `SourceFief/SourceVillage/BatchId/ServiceDays/FreeWageDays`，**不要重写这个来源模型**。

`WarRestPolicyModel.Evaluate` 已经限制：正式战争、无军事优先行动、真实批次、超战时 `TargetCount`、每日剩余、紧迫度阈值，保留这些条件；只在真实测试发现错误条件时修 bug，不重新设计战争评分。

### C4. `ForceManagement/Contracts/UnifiedForceContracts.cs`

为和平复员任务接入既有统一持久化任务；**不更改旧枚举数值**。现有 `PartyForceIntent` 是 `Maintain=0`、`Recruit=1`、`WarRest=2`、`Suspended=3`。

```csharp
public enum PartyForceIntent
{
    Maintain = 0,
    Recruit = 1,
    WarRest = 2,
    Suspended = 3,
    PeaceDemobilize = 4   // 只在末尾追加
}
```

`PartyForceCandidate` 是**运行时评估对象**，新增 `SourceBatchId` 无需改存档格式，用于定位封邑候选；普通驻军候选留空：

```csharp
public string SourceBatchId { get; set; }
```

在 `PartyForcePlanningContext` 新增三个只读输入属性，Codex应照此分层而不是从 `AiForcePlanningModel` 中直接查询 Campaign：

```csharp
public PeaceDemobilizationDecision PeaceDemobilization { get; set; }
public IReadOnlyList<PartyForceCandidate> PeaceDestinations { get; set; }
    = Array.Empty<PartyForceCandidate>();
public bool IsExistingPeaceDemobilizationRoute { get; set; }
```

`PeaceDemobilizationDecision` 已存在于 `ForceManagement/Contracts/ForceManagementTypes.cs`，字段包括 `CanDemobilize / TargetCount / CurrentCount / SurplusCount / ApprovedToday / ReasonCode`，本轮无须复制定义。`PartyForceTask` 本来就含 `SourceBatchId`，**不用新增Task存档字段**。

**复员驻军候选使用现有 `PartyForceSourceKind.Professional` 表示“普通兵归驻军”**，封邑候选使用 `PartyForceSourceKind.Fief`，绝不借候选的 `SourceKind` 推断招募来源。这是新 Intent 下的目的地标签复用，避免为了显示名字新添一个存档枚举。若认为名称易误导，可在本地日志/显示层以 Intent 区分，而不要改旧枚举数字。

### C5. `ForceManagement/Services/PartyForceCandidateService.cs`

这是本次**新增“和平主动寻路”候选的最佳位置**。现有 `BuildContext` 在第31行；当前 `EnumerateCandidates` 第86行有 `if (target?.GapCount <= 0) return`，所以**绝不可重用招募候选列表**：和平超编时 `GapCount=0` 是正常状态，复员目的地要单独枚举。

**新增一个只读方法（建议签名）**：

```csharp
private IReadOnlyList<PartyForceCandidate> EnumeratePeaceDestinations(
    MobileParty party,
    PeaceDemobilizationDecision decision,
    PartyForceTask currentTask)
```

实现顺序：

1. `party/decision` 非空，`decision.CanDemobilize` 且 `decision.ApprovedToday>0`；否则返回空数组。
2. 读真实 `FiefPartyManager.GetServingDetachments(party)`，筛选每个 `Detachment != null / !IsEmpty()`、`SourceFief?.OwnerClan == party.ActualClan`、没有围城、非威胁路径、可达（**复用本类 `TryGetTravelDistance`，不要自写寻路**）。候选 `Settlement=item.SourceFief`、`SourceKind=Fief`、`SourceBatchId=item.Detachment.BatchId`，`AvailableCount=min(ApprovedToday, batch.GetTotalCount())`。排序以超期免薪天数、可归还人数、距离综合为主；来源批次绝不合并成“某封邑可以返回所有兵”的单条虚假数据。
3. 读取本Clan实际据点 `party.ActualClan.Settlements`（明确过滤城镇/城堡、`Town?.GarrisonParty` 存在、非围城），使用现有 `GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison)` 和 `PartyTransferOwnershipPolicy.IsAuthorizedRosterPair(...)` 核验普通兵可转入；只有**普通非在役士兵**可作为驻军候选数量。普通兵总额计算 `party.MemberRoster.TotalRegulars - manager.GetServingTroopCounts(party).Values.Sum()`，同时按 `GetPartyTarget` 的实时超额上限裁剪。不可把封邑在役兵直接当普通兵捐赠。
4. 两种候选都按 `min(当前超额, 已批准剩余额度, 接收容量, 可转人数)` 填 `AvailableCount`，空则不列入。
5. 生成已知可达的 `PartyForceCandidate`，推荐先按能够当日实际接收的人数降序、再按服务超期优先级、最后距离升序和 `Settlement.StringId` 稳定排序。**不要每日报酬随机目的地；不要预留兵员**。

给 Codex 的核心骨架（游戏API名均沿用现有代码；涉及封邑保护、威胁、目的地建模的帮助函数须使用仓库现有组件，不要直接照搬省略部分）：

```csharp
var available = new List<PartyForceCandidate>();
if (party == null || decision?.CanDemobilize != true ||
    decision.ApprovedToday <= 0) return available;

var manager = Campaign.Current?.GetCampaignBehavior<FiefPartyManager>();
foreach (var item in manager?.GetServingDetachments(party) ??
         Enumerable.Empty<ServingDetachmentRef>())
{
    if (item.Detachment == null || item.Detachment.IsEmpty() ||
        item.SourceFief?.OwnerClan != party.ActualClan ||
        item.SourceFief.IsUnderSiege ||
        !TryGetTravelDistance(party, item.SourceFief, out float distance))
        continue;
    int amount = Math.Min(decision.ApprovedToday,
        item.Detachment.GetTotalCount());
    if (amount <= 0) continue;
    available.Add(new PartyForceCandidate
    {
        Settlement = item.SourceFief,
        SourceKind = PartyForceSourceKind.Fief,
        SourceBatchId = item.Detachment.BatchId,
        AvailableCount = amount,
        GapCovered = amount, // 此处代表可复员量，不代表招募缺口
        Distance = distance,
        IsSafe = true,
        ReasonCode = "PeaceFiefReturnCandidate",
        Score = /* 超免薪期限优先 + 可接收数 - 距离惩罚 */ 0f
    });
}
// 继续增加本Clan真实驻军候选，必须经过原有驻军容量/权限/威胁判定。
return available;
```

**注意**：以上 `Score = 0f` 是需要Codex按上方规则填入的**占位示意**，不能交付时保留全零；同样不能仅用 `TryGetTravelDistance` 代替军事威胁判定。建议直接复用 `WarRestPlanningService.BuildBatchCandidate` 的真实来源/威胁判断思路，别引入平行路线权威。

**`BuildContext` 组装**：当 Party 当前任务是 `PeaceDemobilize` 时，用 `DemobilizationBehavior.EvaluateRouting(party, isExistingRoute:true)`，否则传false。其返回值内有 `ApprovedToday`，用于生成 `PeaceDestinations`；有军情保护时返回空，防止候选污染其它意图。旧 `Recruit` `Candidates` 保持原逻辑不变；`WarRest` 仍走自己的原审批。

```csharp
var demob = Campaign.Current?.GetCampaignBehavior<DemobilizationBehavior>();
bool continuingPeace = currentTask?.Intent == PartyForceIntent.PeaceDemobilize;
PeaceDemobilizationDecision peace = !atWar && demob != null
    ? demob.EvaluateRouting(party, continuingPeace)
    : new PeaceDemobilizationDecision { ReasonCode = "AtFormalWar" };
var peaceCandidates = !IsProtected(party) && peace.CanDemobilize
    ? EnumeratePeaceDestinations(party, peace, currentTask)
    : Array.Empty<PartyForceCandidate>();
```

**实现提醒**：这段代码需在 `BuildContext` 中先计算局部变量，再填对象初始化器；`IsProtected` 不应被调用多次造成不同结果。`IReadOnlyList`/数组的类型对齐按编译器提示修正。

### C6. `ForceManagement/Models/AiForcePlanningModel.cs`

**当前真实顺序**：检查当前 WarRest 是否因和平取消 → 军事高优先保护 → 规划 WarRest → 判断招募 `GapCount` → 正常 Recruit。现需插入 Peace **且保持 WarRest 较高的战时优先级**。

推荐 `BuildDailyPlan` 主体顺序：

```csharp
if (context?.Party == null || !context.Party.IsActive)
    return PartyForcePlan.NoPlan("InvalidParty");

// 和平与战争的旧任务立刻失效。
if (context.CurrentTask?.Intent == PartyForceIntent.WarRest &&
    !context.IsAtFormalWar)
    return BuildMaintainPlan(context, "WarRestCancelledByPeace");
if (context.CurrentTask?.Intent == PartyForceIntent.PeaceDemobilize &&
    context.IsAtFormalWar)
    return BuildMaintainPlan(context, "PeaceDemobilizationCancelledByWar");

if (context.IsProtectedByHigherPriorityOrder)
    return PreserveOrSuspend(context, "HigherPriorityOrder");

if (context.IsAtFormalWar && context.WarRest?.IsApproved == true)
    return BuildWarRestPlan(context);

// 只有稳定和平超编时计划复员；不经过随机招募门槛。
if (!context.IsAtFormalWar &&
    context.PeaceDemobilization?.CanDemobilize == true &&
    context.PeaceDestinations?.Any(item => item.IsSafe &&
        item.AvailableCount > 0) == true)
    return BuildPeaceDemobilizationPlan(context);

// 下面保留原版：缺口/阈值/随机门槛/正常Recruit规划。
```

**`BuildPeaceDemobilizationPlan`**：候选列表由 CandidateService 提供；选出目标后，若原任务 `Intent=PeaceDemobilize`、`TargetSettlement` 同一、`SourceKind` 同一、`SourceBatchId` 同一、状态非Cancelled/Completed，**直接 `Task=current`**；否则调用原有 `CreateTask(...)` 并把 `SourceBatchId` 填入新Task。

**`BuildWarRestPlan` 同样修复重建问题**：当前每次都 `CreateTask`，随后 `ApplyLivePlan` 会取消旧任务、生成新 `Guid`，造成目标航路反复变化和无法稳定完成。复用原有 `IsSameLiveTask` 概念，但 **比对必须包含 WarRest 的 `SourceBatchId`**；不能只按封邑对比，否则同城不同来源批次会被错认。

```csharp
private static bool IsSameLiveTask(
    PartyForceTask current,
    PartyForceIntent intent,
    PartyForceCandidate selected,
    string batchId,
    int today)
{
    return current != null &&
        current.Intent == intent &&
        current.SourceKind == selected.SourceKind &&
        current.TargetSettlement == selected.Settlement &&
        string.Equals(current.SourceBatchId ?? "",
                      batchId ?? "", StringComparison.Ordinal) &&
        current.Status != PartyForceTaskStatus.Cancelled &&
        current.Status != PartyForceTaskStatus.Completed &&
        current.CreatedDay <= today;
}
```

原来 Recruit 的 `IsSameLiveTask` 可以继续保留签名、调用这个共用判断（Recruit 的BatchId一般为空）。**不要破坏当前 Recruit 正常任务续用**。若 WarRest 策略选中与当前任务相同的批次和目标，且 `WarRestDecision` 仍批准，把 `Task=current` 原样返回。

**避免每日颠簸**：在候选仍合法且现有目标能够执行时优先续用当前目标，不要仅因另一个来源评分轻微变高就取消半路上的任务；如原来源丢失、归属变化、军情高优先或目标围城，再取消重选。具体可先查当前任务是否仍出现在本日批准候选中，再进行分数排名。

**战时休整必须额外修复“候选与批准批次不一致”**：当前 `WarRestPlanningService.BuildContext` 会将所有安全来源批次放进 `DestinationOptions`；但 `WarRestPolicyModel.Evaluate` 可能只批准其中一部分批次（受30人日限额和战备下限约束）。原 `BuildWarRestPlan` 按所有 `DestinationOptions` 选最高分，再在 `CandidateFiefBatches` 中找同封邑的第一个批准批次，可能得到 `SourceBatchId=null` 或错误批次。**Codex必须改成以 `CandidateFiefBatches` 中 `ApprovedCount>0` 的批次作为真实候选集合，再与相同 `BatchId/SourceFief` 的安全目的地匹配，不能从尚未批准的批次规划任务。**

```csharp
// 精确骨架：先构建批准批次+安全目的地的配对，不要先随便选一个地点。
var allowed = (context.WarRest.CandidateFiefBatches ??
    Array.Empty<FiefBatchTransferApproval>())
    .Where(a => a != null && a.ApprovedCount > 0 &&
        a.SourceFief != null && !string.IsNullOrEmpty(a.BatchId))
    .Select(a => new
    {
        Approval = a,
        Destination = (context.WarRest.DestinationOptions ??
            Array.Empty<PartyForceCandidate>())
            .FirstOrDefault(c => c?.IsSafe == true &&
                c.Settlement == a.SourceFief)
    })
    .Where(x => x.Destination != null)
    .ToList();
// 如果当前WarRest任务的 BatchId/SourceFief 仍在 allowed 中，优先续用。
// 否则在 allowed 中依距离/超期/数量等现有排序确定批准批次，
// task.SourceBatchId = selected.Approval.BatchId。
```

以上只是将**现有已批准的批次**和**现有安全目的地**关联起来；不要另建预留账本或重跑随机审批。

### C7. `ForceManagement/Behaviors/AiForcePlanningBehavior.cs`

**现有** `OnAiHourlyTick` 已为 `Recruit` 和 `WarRest` 添加 `GoToSettlement` 的 `AIBehaviorData` 评分，且使用原生 `GetBestNavigationData`；新增 `PeaceDemobilize` 到允许的意图判断，不新增第二个 `AiHourlyTick` 监听。

```csharp
bool ownsTravelTask = task?.Intent == PartyForceIntent.Recruit ||
    task?.Intent == PartyForceIntent.WarRest ||
    task?.Intent == PartyForceIntent.PeaceDemobilize;
if (!ownsTravelTask || task.Status == PartyForceTaskStatus.Completed ||
    task.Status == PartyForceTaskStatus.Cancelled ||
    task.Status == PartyForceTaskStatus.Suspended ||
    task.TargetSettlement == null ||
    party.CurrentSettlement == task.TargetSettlement)
    return;
```

但不能只加一个 `||` 就完工。**按 Intent 分开复核**：

- Recruit：保留原 `IsRecruitmentDestinationAuthorized` 与绑定逻辑，不放宽。
- WarRest：确认 `WarRest` 配置启用、正式交战、没有军情高优先、真实 `SourceBatchId` 仍在，目标未围城；否则不给高分并让原生AI重评。
- PeaceDemobilize：再调 `DemobilizationBehavior.EvaluateRouting(party, true)`，检查稳定和平、仍超编、还有当日额度，来源批次/驻军资格仍可用；否则终止此任务并 `RethinkAtNextHourlyTick=true`。
- 三者共同检查敌情/军团/战斗/围城/原生避敌；军情优先时不向AI注入比原生行动更高的目的地评分。**不要调用 `SetMoveGoToSettlement` 或每Tick `SetMovePatrolAroundPoint`**。

**读档**：在已有 `RevalidateLoadedTask` 增加 `PeaceDemobilize` 分支。校验仍和平、和平连续天数、`SurplusCount>0`、目标的同Clan实际归属、源 `BatchId` 或真实驻军容量、非围城和可达；失败标 `Cancelled`，而非 `Suspended` 冻结。WarRest 现有分支还要加 `rest.Settings.Enabled` 和同一批次仍被当前策略允许；不要把过去的任务自动当成功执行。旧 `Save` 9段/12段记录保持不动（任务存档机制见 C8）。

**日志/UI**：现有 `LogPlan` / `GetIntentText` / `GetPlanReasonText` switch 需覆盖新 `PeaceDemobilize`，可复用已有通用模板但须设置目的地变量；新增玩家可见词条必须在对应本地化资源中添加中英文，不在 C# 直接拼中文字。详细日志只跟随现有 `RecruitmentLogFilter`，避免每日每Party大量输出。

### C8. `ForceManagement/Missions/PartyForceTaskPersistence.cs` 与 `PartyForceTaskCoordinator.cs`

`PartyForceTaskPersistence.Capture` 用固定的 `\u001f` 12段字符串存 `partyId,targetId,batchId,intent,sourceKind,version,createdDay,status,progress,taskId,creationAttemptId,legacyFlag`，`Restore` 接受旧9段和新12段。**不要增加第13段，不改已有 `modifiedarmy_force_task_records_v1` key**。

因为恢复时 `Enum.IsDefined(typeof(PartyForceIntent), intentValue)`，在旧枚举末尾追加 `PeaceDemobilize=4` 后，新增任务就能自动通过现有持久化，不需要重写解析器。旧枚举值必须不变。

`PartyForceTaskCoordinator.ApplyLivePlan` 在 `next==current` 时自然保留 `TaskId` 和 `Version`；否则新建Guid。所以所有“相同任务续用”的逻辑应该放在 `AiForcePlanningModel`，**不要为了稳定ID重写 Coordinator 的 Save/Guid 规则**。

此外，Coordinator 当前 `EvaluateParty` 会查询已存在的 `TransferOperationJournalBehavior.IsDomainFrozen(...)`，属于旧全局交易保护。**本轮不引入任何新冻结/恢复机制；不能因为开启复员而把冻结状态扩散到新的业务状态。** 若测试发现旧存档已有冻结导致复员/休整卡死，只对经确认属于本功能的阻塞调用做最小修正并在交付报告列明；不得直接清空全部旧Journal或更改初始动员依赖。

### C9. `ForceManagement/Behaviors/DemobilizationBehavior.cs`

已有权威持久状态：

```text
_clanPeaceSinceDay        -> modifiedarmy_demobilization_clan_peace_days
_partyLastDemobDay        -> modifiedarmy_demobilization_party_last_days
_partyDemobCountToday     -> modifiedarmy_demobilization_party_daily_counts
```

**不要复制和平日计数到 AI规划Behavior**，避免双账本。`EvaluateRouting` 已调用 `PeaceDemobilizationModel.Evaluate` 并读取同一日额度，这个入口继续使用。改动：

1. `OnNaturalVisit` 当前返回 `void`；允许最小修改为 `DemobilizationExecutionResult OnNaturalVisit(...)`，所有原 `return;` 返回一个明示未执行结果（也可保留 void，新增 `TryExecuteNaturalVisit` 返回结果，在原 `OnNaturalVisit` 中委托）。原有调用点只有 `PartyVisitExecutionService`，不用额外适配一堆消费者。
2. 将 `IsBoundSettlement(party,settlement)` **改为“属于本Clan且非围城的真实 fortification，Party现场存在”**；招募任务的绑定检查继续保持在 Recruit 分支，不得借复员扩大招募权限。
3. 调用现有 `DemobilizationRunner.TryExecuteAtSettlement`，把真实 `ActualCount` 写回三个已有Save字典，并向上返回含 `ActualCount/ReasonCode` 的结果。**同一业务访问只记一次人数**，普通捐赠不能重复记为复员。
4. 战争宣布时 `OnWarDeclared` 当前依赖 `_routingTargets` 并可能 `SetMovePatrolAroundPoint`；启用统一 `PeaceDemobilize` Task 后，改为**只取消由本Mod拥有的和平复员Task**，设置本体AI下小时重思考，不强行发出一个新移动命令。原 `_routingTargets` 因不再真正控制路线，可移除运行时引用/明确停用（它不是Save字段）。
5. 和平第一天计时、稳定 `PeaceStabilityDays=3`、每天 `30`/`20%` 均保持。

**注意**：天然访问的复员不一定来自新任务。第一次到本Clan城堡、没有复员Task，也要正确执行。`OnNaturalVisit` 本身的本Clan范围验证与 `PeaceDemobilizationModel.Evaluate` 的合法性校验应一致。

### C10. `ForceManagement/Missions/PartyVisitExecutionService.cs`

当前第33—51行：当没有匹配目的地的任务，先 `ExecuteRoutineDonation`，若 `donation.Executed` 立即 return，随后和平复员永远得不到机会。按以下**确定次序**修改：

```text
PartyExchangeBehavior 入口（保持上层的刚占领特殊捐赠优先）
  -> 若任务是 PeaceDemobilize 且地点匹配：
       调用和平复员（本Clan合法城堡/城市）；将 ActualCount 转换为
       PartyExchangeExecutionResult，按真正人数完成/拒绝Task。
  -> 若任务是 WarRest 且地点匹配：
       继续使用原 ExecuteWarRest；到达前重新审批。
  -> 若任务是 Recruit 且地点匹配：
       原有招募路径完全不变。
  -> 无匹配任务：
       如果自然访问符合和平条件，先尝试 OnNaturalVisit；
       ActualCount>0 则返回真实执行结果；
       否则才 ExecuteRoutineDonation。
```

**推荐把类型转换集中在一个小方法**，避免多条路径对“实际完成人数”的理解不一致：

```csharp
private static PartyExchangeExecutionResult ExecutePeaceDemobilization(
    MobileParty party, Settlement settlement)
{
    DemobilizationBehavior behavior = Campaign.Current?
        .GetCampaignBehavior<DemobilizationBehavior>();
    DemobilizationExecutionResult actual =
        behavior?.OnNaturalVisit(party, settlement);
    int count = Math.Max(0, actual?.ActualCount ?? 0);
    return new PartyExchangeExecutionResult
    {
        Status = count > 0
            ? TransferCommitStatus.Committed
            : TransferCommitStatus.Rejected,
        TransferredCount = count,
        ReasonCode = actual?.ReasonCode ?? "PeaceDemobilizationUnavailable"
    };
}
```

上例采用 C9 的“`OnNaturalVisit` 改返回结果”实现方案；若最终保留旧 void 签名，则从 `TryExecuteNaturalVisit` 取实际结果即可。**不要在访问信息已确认成功后再触发一个独立的复员执行**。

`ApplyResult` 针对新意图复用原有 `Committed/PartiallyCommitted -> Completed`；`Cancelled` 不生成冻结。如果返回成功少于申请额度，只完成本次有实际人数的操作，次日可生成新的复员任务，不能同一操作ID自动重复。

### C11. `ForceManagement/Services/DemobilizationService.cs`

**现有顺序**：`PeaceDemobilizationModel.Evaluate` → `manager.AdvanceServiceDays` / `ReconcilePartyFiefTroops` → 按服役时长遍历**所有**来源批次 → 返乡 → `FiefTroopPlacementService.TryPlaceAfterReturn` → `TroopTransferService.TransferFieldToGarrison`。

**精确修复**：当前循环在第57—82行仅判断 `item.SourceFief.OwnerClan`，没有把当前访问点和来源绑定，而深层 `FiefReturnService` 会拒绝异地返还。因此这里明确加过滤：

```csharp
// 只在当前真正源封邑返还所属的服务批次。
foreach (ServingDetachmentRef item in manager.GetServingDetachments(party)
    .Where(item => item.Detachment != null &&
        item.SourceFief == settlement &&
        item.SourceFief.OwnerClan == party.ActualClan)
    .OrderByDescending(item =>
        item.Detachment.ServiceDays >= item.Detachment.FreeWageDays)
    .ThenByDescending(item => item.Detachment.ServiceDays)
    .ThenBy(item => item.Detachment.BatchId)
    .ToList())
{
    if (remaining <= 0) break;
    FiefReturnResult result = manager.TryReturnFiefTroops(
        party, item.SourceFief, item.Detachment.BatchId, remaining);
    int actual = Math.Max(0, result?.ActualReturnedCount ?? 0);
    // 保存真实完成数，不能将申请数当完成数。
    returnedFief += actual;
    remaining -= actual;
}
```

如果要严格执行任务目标的 `SourceBatchId`，可以在该复员任务分支再按指定ID过滤；自然访问可以合法处理当前源封邑的多个批次，但**不能跨源封邑“顺便返乡”**。

后续两步必须反复按**实时安全超额**限额，而非最初预测值：

```csharp
var current = ForceManagementFacade.Current.GetPartyTarget(party);
int liveSurplus = current.Scenario == ForceScenario.Peace
    ? Math.Max(0, current.CurrentCount - current.TargetCount)
    : 0;
int canPlace = Math.Min(remaining, liveSurplus);
// 现有 TryPlaceAfterReturn 只有能够证明真实独立来源/身份时保留。
// 完成后重算 current 与 liveSurplus；普通兵只移交剩余额度。
```

**权限**：普通兵接收只使用真实 `settlement.Town.GarrisonParty`，必须同Clan归属且有容量；无合法接收就停在现有人数，不删除。

**技术边界**：执行封邑返乡底层仍涉及村庄Hearth、服役批次、冷却队列，它不是“两个Roster之间AddToCounts”。本轮优先调用权威 `manager.TryReturnFiefTroops` 保持批次身份。若确实必须改其旧事务细节，限制到本功能调用并单列问题，不能顺手改玩家手动返乡和初始动员。

### C12. `ForceManagement/Services/TroopTransferService.cs`：普通兵迁移清单

当前第93—174行，先从来源Roster减 `-count`，再给驻军加 `+count`。只对 `GarrisonDonationPurpose.PeaceDemobilization` 分支采用你确认的新规则；**普通捐赠沿用旧行为，避免无关更改**。

**数据结构可用局部临时清单，不需要持久保存：**

```csharp
private sealed class TroopMoveEntry
{
    public CharacterObject Troop { get; init; }
    public int Healthy { get; init; }
    public int Wounded { get; init; }
    public int Total => Healthy + Wounded;
}
```

先利用现有候选 `RecruitmentModelManager.Model.BuildPlan(...)` 的 `plan.Evaluations` 和 `GetServingTroopCounts`，一次性构造 `List<TroopMoveEntry>`；必须先核对：

- 迁移是合法的 Mod 管理 AI、现场本Clan驻军、没有围城/地图战斗。
- 当日/当前政策已批准人数、野战安全超额、目标驻军实际空位。
- 每个 `Troop` 的 `available = 当前兵种人数 - 真实封邑服役保留数`；只对 `available>0` 生成条目。
- 获取 `TroopRosterElement.Number` 与 `WoundedNumber`，`Healthy = Number - WoundedNumber`。优先取健康兵，再取伤兵（或按现有设定排序），**每项总数不超过当前非受保护人数**。
- 清单按 `CharacterObject.StringId` 稳定排序；所有数量、容量判断在真正写Roster前完成。保护封邑服役兵按现有按兵种计数规则实现，不凭空假设能识别每个士兵的独立身份。

**执行伪代码尽量贴近本体API**（这里的 `destination`/`source` 为 `TroopRoster`，`entry` 已经全量验证；省略外层日志和上下文）：

```csharp
int moved = 0;
foreach (TroopMoveEntry entry in manifest)
{
    // 健康兵：目标先加，同步成功后来源才扣。
    if (entry.Healthy > 0)
    {
        int index = destination.AddToCounts(
            entry.Troop, entry.Healthy,
            false, 0, 0, true, -1);
        if (index < 0) break; // 原版返回索引；不是人数
        source.AddToCounts(entry.Troop, -entry.Healthy,
            false, 0, 0, true, -1);
        moved += entry.Healthy;
    }

    // 伤兵：单独加减，并正确携带 woundedCount。
    if (entry.Wounded > 0)
    {
        int index = destination.AddToCounts(
            entry.Troop, entry.Wounded,
            false, entry.Wounded, 0, true, -1);
        if (index < 0) break;
        source.AddToCounts(entry.Troop, -entry.Wounded,
            false, -entry.Wounded, 0, true, -1);
        moved += entry.Wounded;
    }
}
return moved;
```

**这是执行阶段骨架，不是可直接独立编译的完整方法**；Codex要将其插入现有服务的 `purpose == PeaceDemobilization` 分支，沿用原候选/权威批准与记录，遇异常停止并打印执行到哪一条、已完成多少人。**没有额外读取目标Roster差值、反向撤销、冻结或新接口 `TryAddTroops`**。

**最容易犯的错误**：

1. `TroopRoster.AddToCounts()` 返回**兵种索引**，哪怕成功添加20人也可能返回0。`index>=0` 表示API正常返回一个有效位置；该 `int` **不是成功人数**，不能直接用 `RemoveTroops(source, index)`。
2. 切勿混用“新增总数”和“新增伤兵数”：伤兵调用时 `count=+n,woundedCount=+n`，来源 `count=-n,woundedCount=-n`；健康兵 `woundedCount=0`。
3. 执行清单不得超出 `RemainingQuota` 与 `SafeFieldSurplus`；成功部分已计入真实人数，下一天重新审批。
4. 如果原版 `AddToCounts` 抛异常，捕获日志后结束，不继续后续条目；不装作成功，也不引入冻结/回滚。**同步调用仍可能部分成功，这个取舍是用户已明确接受的简化要求**。
5. 若同一兵种同时有封邑在役与普通士兵，来源排除仍以现有 `manager.GetServingTroopCounts` 为准，不引入逐士兵身份系统。

### C13. `FiefReturnBatch` / `FiefPartyReturn.cs`：何时必须动、何时不要动

当前 `FiefReturnBatch.AddTroops(IReadOnlyDictionary<CharacterObject,int>)` **返回实际接收数量**。与 `TroopRoster.AddToCounts` 不同，它的返回值可以直接用于确定从野战队伍移除多少。

当前 `FiefPartyReturn.TryCommitFiefReturn` 第174行开始是**先扣野战Roster**、然后修改在役批次、动员Hearth、冷却队列，尾部存在既有回滚及 `RecoveryRequired`。这说明封邑返乡的真实资源远超普通驻军调兵。

本轮的**最小可交付策略**：先保持权威 `manager.TryReturnFiefTroops` 的入口、真实来源校验及服务身份；不用新写任何回滚/冻结。如果实测能正常完成合法返乡，就不重写此共享方法。若旧方法确实直接阻挡启用或用户坚持本轮连旧实现也清理，则先把本次调用与玩家返乡、Party解散、初始动员等所有调用者列出，再另列精确修改范围，不能以“为开启复员”为借口直接删掉这整个共享链的异常处理。

**若仅在最小范围调整接收顺序**，原则如下（不是建议Codex直接全函数替换）：

```text
权威预检查：来源村庄/封邑、batchId、批次人数、原Party人数、追踪动员Hearth
  -> 本次返乡清单 {CharacterObject, Healthy, Wounded, Count}
  -> 找到或创建当前村庄对应的冷却批次 FiefReturnBatch
  -> cooling.AddTroops(该兵种数量)   // 这里真实返回 int accepted
  -> 仅按 accepted 扣除来源兵种人数及相应伤兵数
  -> 按 accepted 更新服务批次、解除该村庄动员占用、恢复对应Hearth
  -> 同步权威计数并返回 ActualReturnedCount
```

**存档结构重要事实**：`FiefReturnBatch.Troops` 保存的只有 `Dictionary<CharacterObject,int>`，**没有伤兵字段**。因此野战Roster上的健康/伤兵要分别正确扣除，但进入冷却状态后使用既有“兵种+数量”语义；不能未经许可新增 `SaveableProperty` 或宣称冷却队列保留了健康/伤兵比例。如果业务要求冷却后的伤兵状态仍然保持，需要单独做存档模型扩展与游戏规则设计，不属于本轮。

**不建议在本轮硬改封邑权威链的理由不是维护旧回滚，而是防止跨越核心启用目标，尤其不能影响最近更新的初始创建/征召。**

### C14. 战时休整每日进度持久化（`AiForcePlanningBehavior` + `WarRestPlanningService`）

当前 `WarRestPlanningService._progress` 是运行时字典，读档当日会重置30人额度；需要持久化，但**只使用一个存档权威拥有者**。最简单建议是在已有 `AiForcePlanningBehavior`（本来就是 Campaign Behavior 且有 SyncData）保存WarRest每日计数，而不是再建 `WarRestBehavior` 或全局日志账本。

**新字段**：

```csharp
// AiForcePlanningBehavior 中：party.StringId + "|" + day -> 实际休整人数
private Dictionary<string, int> _warRestDailyActual = new();
```

**现有 `SyncData` 内追加，不改旧键/旧Task格式**：

```csharp
dataStore.SyncData(
    "modifiedarmy_war_rest_daily_actual_v1",
    ref _warRestDailyActual);
_warRestDailyActual ??= new Dictionary<string, int>();
```

**给 `WarRestPlanningService` 使用的查询/记账入口**：

```csharp
internal int GetWarRestCompletedToday(MobileParty party, int today)
{
    if (party == null || string.IsNullOrEmpty(party.StringId)) return 0;
    string key = party.StringId + "|" + today;
    return _warRestDailyActual.TryGetValue(key, out int count)
        ? Math.Max(0, count) : 0;
}

internal void RecordWarRestCompleted(MobileParty party,
    int today, int actualCount)
{
    if (party == null || actualCount <= 0 ||
        string.IsNullOrEmpty(party.StringId)) return;
    string key = party.StringId + "|" + today;
    _warRestDailyActual.TryGetValue(key, out int oldCount);
    _warRestDailyActual[key] = checked(oldCount + actualCount);
}
```

`WarRestPlanningService.RecordCompleted` 改为调用上述 Behavior 的**唯一**记账入口；`GetCompletedToday` 改读此入口；删去旧 `_progress` 的权威地位，避免两个计数源。WarRest策略检查使用 `context.CompletedToday`，其来源必须是这个持久字典。每日清理过期键（例如只保留最近7天）即可，勿按战争结束/任务取消把**当天已发生的休整数**清零，否则同日再次宣战会绕过额度。

**确保调用一次**：

- `PartyExchangeService.ExecuteFiefReturn`（实际已存在）调用 `RecordCompleted(actualReturnedCount)`。
- `PartyVisitExecutionService.ExecuteWarRest` **不得重复再记这一份返回数**。
- 可选 `FiefTroopPlacementService.TryPlaceAfterReturn` 记录的 `placement.PlacedCount` 如果确实是**另外一批从野战Roster移走的未绑定兵员**、且没有超安全下限和剩余每日额度，才额外记；验证不了就先停止该可选分支，主功能只执行真实在役批次返乡。
- 即使一个 Party 当天已经到访两个不同来源封邑，两次返还量之和也不得超过30，存读档后仍然如此。

### C15. 优先级、战争切换和 AI导航保护

**绝不能因为新复员Intent而牺牲本体军情AI**。战略规划与每小时评分都要检查：

| 情况 | 和平复员 | 战时休整 |
|---|---|---|
| 正式和平但未稳定满3天 | 拒绝 | 拒绝 |
| 正式和平已稳定，Party超目标且空闲 | 审批、找合法目的地 | 拒绝 |
| 正式战争、存在真实封邑批次和足够超编 | 拒绝 | 按WarRestPolicyModel评分，允许才执行 |
| 处于军团/地图战斗/围城/追击/避敌 | 不注入高分；保留原生AI优先 | 不注入高分；保留原生AI优先 |
| 自有目的地被占、换Clan、围城或路线不可达 | 取消当前任务，下一次重新规划 | 同左 |
| 已到目标，但当日额度用完 | 不继续移动兵员 | 不继续移动兵员 |
| 任务走到半路保存读取 | 重新校验继续同一任务 | 重新校验真实SourceBatchId后继续 |
| 读档后同日已经休整20人 | 与和平额度独立 | 只能再休整10人 |

**不要让 `DemobilizationBehavior._routingTargets` 和统一 `PartyForceTask` 同时作为路线主人**；前者当前尚无外部实际调用，只需取消旧副作用，统一任务负责目的地，原生AI负责实际移动。

**不可把 WarRest 当作和平复员**：WarRest 只返还真在役封邑批次，而和平除在役兵返乡外，必须在合法且有容量的城镇/城堡将普通兵归驻军。

### C16. 任务日志、UI与本地化（低干扰实施）

增加/复用的日志事件应能直接说明：

```text
[PeaceDemobPlan] party=<显示名> clan=<显示名> target=<据点> kind=Fief|Garrison
  batch=<id仅次要诊断> current=160 targetCount=60 surplus=100
  approved=30 reason=...
[PeaceDemobArrive] ... actualFiefReturned=... actualPlaced=...
  actualGarrisonTransfer=... remainingDaily=... reason=...
[WarRestPlan] ... war=true urgency=0.78 threshold=0.75
  batch=<id> dest=<真实来源> safeFloor=70 approved=20
[WarRestArrive] ... returned=20 extraPlaced=0 remainingToday=10
```

显示名优先，原始 ID 仅作为第二字段。`Debug` 用于每日候选，`Info` 用于实际完成，`Warn` 用于资源条件变化，`Error` 用于异常。正常不触发不应全图每小时刷屏。若添加新的玩家可见 Intent 或 ReasonCode，使用现有 `GameTexts.FindText` + 中文资源，保持仓库本地化规则。

### C17. 验收脚本与测试清单（要把预期当作**待测**，不是已经PASS）

**静态/编译**：

1. `git status --short` / `git log -1 --oneline`；比对工作区，不覆盖现存未提交修改。
2. 检查 `ModifiedArmy/AGENTS.md`（若本地存在）、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`，并列出本次真实编辑路径。
3. `rg -n 'EnableAutomaticDemobilization|EnableDemobilizationRouting|WarRest|PeaceDemobilize|RecordCompleted|OnNaturalVisit' ModifiedArmy/ModifiedArmy`，确认新路径能串起来，不能只有配置true。
4. `rg -n 'new WarRestPolicySettings|Enabled = false' ModifiedArmy/ModifiedArmy/ForceManagement`，确认WarRest不是仍然硬编码禁用。
5. `rg -n 'AddToCounts|CompensateCreationGarrisonWithdrawal|PartyCreationInitializationScope'` 检查新建Party相关调用**未因本次改动改变语义**。
6. 按项目 `.csproj` 和工具存在情况，运行适用的编译、`ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1`、`Invoke-ReleaseValidation.ps1` 等；没有游戏DLL时标 `BLOCKED` 而不是假称通过。

**行为矩阵**：

| Case | 给定条件 | 必须观察的结果 |
|---|---|---|
| PE-01 | 稳定和平第2天，160人/目标60 | 不提前复员 |
| PE-02 | 稳定和平第3天，160人/目标60，安全有容量 | 能规划有效目的地并实际减少至多30人 |
| PE-03 | 当天同一Party已复员20，又进入第二据点 | 总新增最多10（同时受当下20%和超编限制） |
| PE-04 | 两个不同来源A/B的真实封邑兵，在A到达 | 只能归还A来源，B不动 |
| PE-05 | 普通兵有12健康、8受伤，驻军空位20 | 目标12健康8伤，来源同步减相应人数 |
| PE-06 | 所有本Clan驻军满、来源封邑不安全 | 不自动删人、不产生虚假“完成” |
| PE-07 | 和平寻路途中突然宣战 | 当前复员Task取消，本体军事AI接管 |
| PE-08 | 任务在路上保存读取，归属/战况不变 | 同TaskId继续且不重复执行 |
| WR-01 | 正式战争、服务超期/资金紧迫、评分≥0.75、战时超编 | 真正创建WarRest Task并前往原来源 |
| WR-02 | 战时Party 90人/安全目标70，休整请求30 | 最多20，返还后不能低于70 |
| WR-03 | 战时当日已回20，保存读取后再到来源 | 最多再回10 |
| WR-04 | WarRest任务当天每日评估 | 不反复分配新Guid/新任务版本 |
| WR-05 | 战时任务途中结盟/停战或进入军团 | 取消/让位，不强占本体路线 |
| WR-06 | 已完成真实批次返乡20，又调用额外安置 | 只允许独立真实野战减员，不能把20重复计入 |
| REG-01 | 最新7999837建Party前预演和创建时同步动员 | 不受本轮改动破坏，`IsInitializing` 拦截保留 |
| REG-02 | 原有Recruit任务、普通驻军捐赠、刚占领后捐赠 | 继续按原规则运行，和平复员不扩大征兵许可 |
| REG-03 | 旧存档没有新WarRest计数键 | 可以载入，今天没有历史WarRest操作则计数为0 |
| REG-04 | 运行时异常 | 明确记录失败步骤和已完成实际数；本轮不新增冻结/回滚 |

**交付报告模板**：

```text
BASE_HEAD: <SHA>
CHANGED_FILES: <全部文件路径>
CODE_TRACE: 平和复员/战时休整的“规划->评分->到达->实际资源”路径
SAVE_COMPAT: 旧键/旧枚举值/任务格式是否保持
FEATURE_FLAGS: 和平执行true，和平路线true，WarRest true
TESTS: 每一项 PASS / FAIL / BLOCKED（原因与证据）
RUNTIME: 具体Party/城堡/人数/伤兵/任务ID日志
NON_GOALS_UNCHANGED: 创建Party、初始动员、正常Recruit、其他模块
KNOWN_RISKS: 真实发现的问题，不得隐瞒
GIT_ACTIONS: no commit/no push/no reset/no rollback
```

## D. 最容易让Codex实施失败的 12 条提醒

1. **不要仅把3个开关设为true**。和平仍没有主动任务，战时仍可能不断重建Task。
2. **不要把 `Recruit` 候选列表当“复员候选”**；超编时它本来就空。
3. **不要把原版 `TroopRoster.AddToCounts` 的 `int` 返回值当成功人数**；它是索引，0也是正常成功索引。
4. **不要虚构本体 `TryAddTroops`**；当前仓库没有该API；使用原有 `AddToCounts` / `FiefReturnBatch.AddTroops` 各自真实语义。
5. **不要自创另一个 `_routingTargets`**；用已可存档的 `PartyForceTask`。
6. **不要在每小时强制下达 `SetMove...`**；用现有 `AIBehaviorData/PartyThinkParams` 打分。
7. **不要让新增和平Intent绕过原有 Recruit 定居点绑定限制**。
8. **不要跨来源封邑返乡**；返回必须是原真实 `SourceFief` 的对应批次。
9. **不要把WarRest已记录的真实返乡数加两遍**；额外安置是另一批人才可另计。
10. **不要因读档/停战重置当天WarRest额度**；只在游戏日变化后自然换键。
11. **不要删除 `PartyCreationInitializationScope.IsInitializing` 和改写初始动员事务**；最新提交新增了这些关键路径。
12. **禁止新增回滚、补偿、持久冻结和人工解冻状态**；同时必须说明既有共享代码里的旧处理是否进入本功能调用链，不能掩盖风险。

## E. 可直接复制给 Codex 的最终任务描述

> 基于仓库最新本地HEAD实施 `ModifiedArmy` 的“和平复员+战时休整真实启用”，严格遵守本文件 A—D 及附录V2边界。你必须实改源码，而不是仅创建方案；按 A1—A8 逐段核验，先完成配置驱动，再接唯一AI任务规划/原生导航/到达执行，然后修普通兵同步清单迁移与战时每日额度持久化。禁止引入任何新回滚、反向补偿、冻结或人工解冻机制；不得改动 Party创建及初始动员、招募权重、财政、Hearth经济。原版 `TroopRoster.AddToCounts` 返回索引，`FiefReturnBatch.AddTroops` 返回成功加入人数，按各自真实语义使用，健康兵和伤兵分别处理。完成后按 C17 提供每项PASS/FAIL/BLOCKED和运行证据。不要git reset/revert，不commit、不push、不提PR。

---

# 附录：V2 方案原文（保留原有约束与业务背景）

# ModifiedArmy｜和平复员与战时休整启用方案（Codex V2）

> **唯一业务目标**：让AI领主部队在稳定和平时能主动复员超编兵力，在正式战争中能按照军事安全下限有条件地休整、将封邑服役兵返乡。**不能把本任务变成全局招募、初始创建、调兵事务、人口、财政或存档架构重构。**
>
> **代码基线**：`GuihuaZhou/bannerlord`，GitHub `main`，2026-11-15读取的最新提交 `79998373c90204634b80f3a6582be82ea3723656`（`feat(army): commit approved initial mobilization`）。Codex开工时先重新核对本地HEAD、工作树、适用AGENTS文件；如HEAD已有变化，先核实受影响调用链，以当前代码及本次用户要求为准。
>
> **交付性质**：源码实现指令，不是已实现或已测试的声明。未经用户批准，不提交、不推送、不创建PR、不做Git版本回退/重置。

## 0. 需求边界与最高优先级约束

1. **必须真正启用两项功能**。仅修改配置 `false → true` 不算完成；必须有AI选址、任务寻路、到达执行、实际兵力减少、每日限额、战和切换、读档后可继续的完整闭环。
2. **同步执行**：先完成归属、兵源、战和、目标兵力、伤兵、位置、容量等合法性检查，再建立本次确定的兵员迁移清单；然后逐条先接收目标，后扣除来源，健康兵与伤兵分别处理。使用现有本体API/既有业务对象，信任其明确返回语义；**不另建读取Roster差值的“验证框架”**。
3. 原版 `TroopRoster.AddToCounts(...)` 返回的是**兵种索引**（成功添加一般为非负索引），**不是新增人数**。迁移成功人数来自已经预检查且被执行的清单数量；对于 `FiefReturnBatch.AddTroops(...)` 等真正返回接收数量的业务接口，可按返回的接收数量扣来源。严禁把索引当人数，亦不虚构 `TryAddTroops` 为本体API。
4. **本轮不引入回滚、反向补偿、持久冻结、人工解冻或RecoveryRequired恢复状态机**。现有代码含这些机制，必须区分**既存实现**与**本次新增**；本轮只调整复员/休整实际涉及的最小执行路径，不做跨全模块的“清除回滚”专项。若既有冻结判定使复员/休整无法执行，应在这两条业务路径上解除阻塞并给出改动说明，不创建新冻结状态。共享代码修改前先核对调用者，保护最新初始部队动员功能。
5. 失效、容量不足、源兵变化为普通拒绝；有部分实际执行时据实报告已完成数量，**不假装全成功，不静默吞掉异常，也不凭空删除/生成士兵**。不增加为了追求理论全有或全无的第二套全局事务账本。
6. **保持原生AI移动控制**：使用已有 `AiForcePlanningBehavior` / `AiHourlyTickEvent` 的目标评分入口，不用每Tick覆盖 `SetMoveGoToSettlement`，军团、战斗、围城、追敌、原生避敌等高优先级事件必须优先。
7. **封邑权威状态**：真实 `FiefServiceBatch`、原来源封邑/村庄、Hearth动员占用、返乡冷却不能丢；普通兵不能冒充封邑兵，返乡不能跨来源封邑。**不新增兵源池、不改军事文化制度、基础训练或AI创建Party规则**。
8. **存档兼容**：旧 `SyncData` 键、Save IDs、枚举旧数值、现有任务持久格式保持兼容；可以在现有Behavior下增加必要的每日休整计数字典，不额外创建平行的路线/兵员账本。

## 1. 最新提交影响——明确保护边界

最新 `7999837` 相比 `c5eb213` 改了 `FiefPartyManager.cs`、`FiefPartyRecruitment.cs`、`PartyExchangeService.cs`、`PartyExchangeBehavior.cs`、`PartyCreationCoordinator.cs`，并新增 `InitialPartyMobilizationCommitter.cs` 和 `PartyCreationInitializationScope.cs`。

- **新建部队预演与实际动员是独立功能，不属于本轮**，不修改 `PartyCreationCoordinator`、`InitialPartyMobilizationCommitter`、`InitialPartyMobilizationPlanner`、`RecruitmentSubject`、动员预算、建Party门槛。尤其注意 `InitialPartyMobilizationCommitter.Commit()` 现在调用 `PartyExchangeService.Execute(...)` 来抽取驻军，以及 `CompensateCreationGarrisonWithdrawal(...)` 来处理建Party失败；**不能为了复员路径随手重写通用 `PartyExchangeService.ExecuteRosterTransfer`，否则可能破坏新代码**。
- `PartyExchangeBehavior.EvaluateVisit()` 新增 `PartyCreationInitializationScope.IsInitializing(party)` 防止Party尚在初始化时触发访问执行。必须保留。
- `PartyExchangeService` 的既有 `RecoveryRequired`/补偿仍在；这不是本次为了复员、休整新设计的机制。若两条业务经过共享方法遭其阻塞，最小范围处理业务入口并报告副作用；不要把所有现有事务系统一并重构。
- 当前配置仍是 `EnableAutomaticDemobilization=false`、`EnableDemobilizationRouting=false`；`WarRestPlanningService.BuildContext()` 仍硬编码 `new WarRestPolicySettings { Enabled = false }`。新提交并未修复。

## 2. 源码事实与问题（以最新7999837已读取的main为准）

| 文件/成员 | 已核对的现状 | 本次最小修改 |
|---|---|---|
| `ModuleData/militaryConfig.xml` | 两个和平复员开关均false，稳定和平3天、每日20%且最多30人 | 启用两个开关，保持现有限额 |
| `common/MilitaryConfig.cs` | 已有 `DemobilizationConfig` 和解析；尚无对应WarRest的XML解析 | 增加WarRest配置读取，替代硬编码，默认沿用原阈值 |
| `ForceManagement/Services/WarRestPlanningService.cs` | `BuildContext` 构造 `Enabled=false`；`RecordCompleted` 运行时字典 `_progress` | 启用，实际休整进度在既有Behavior中持久化，不能读档重置当天额度 |
| `ForceManagement/Models/WarRestPolicyModel.cs` | 正式战争、军事优先级、超目标兵力、服务批次、财政/免薪评分、每日额度逻辑已有 | 保留安全下限，接入新配置并确认审批实际可触发 |
| `ForceManagement/Behaviors/DemobilizationBehavior.cs` | `OnNaturalVisit()` 强制仅绑定据点；有 `EvaluateRouting`/`RecordRouting`，但尚没有真正发出主动复员任务的调用链 | 将和平复员接入唯一AI任务主人；到合法同Clan目的地可执行，不放宽招募权限 |
| `ForceManagement/Models/PeaceDemobilizationModel.cs` | 现有合法性审批、和平稳定天数、超编、日限额逻辑已在 | 复用，补上选址与到达执行路径，保持超编目标安全线 |
| `ForceManagement/Services/DemobilizationService.cs` | 遍历所有在役批次试返乡（未在上层按当前合法来源过滤），然后未绑定兵安置、普通兵转驻军 | 只在对应实际来源执行；按剩余额度/现场超编顺序处理，统计实际数 |
| `ForceManagement/Services/TroopTransferService.cs` | 普通兵移交驻军已使用自己的候选清单与容量判断，但顺序是**先减来源、后加驻军** | 仅对和平复员涉及的执行顺序改为目标先加、来源后减；保留受伤人数及封邑服务保护，不全局重写交易层 |
| `ForceManagement/Missions/PartyVisitExecutionService.cs` | 无匹配任务时先普通捐赠；若成功提前return，和平复员可能永远没机会执行；WarRest已有到达执行 | 建立和平复员到达分支及优先级；复员不能被普通捐赠永久抢先 |
| `ForceManagement/Models/AiForcePlanningModel.cs` | WarRest会每日新建Task，缺少像Recruit那样的同任务续用；尚无和平复员Intent规划 | 同目的地/批次继续使用原Task；加入和平复员战略计划，避免每日重建 |
| `ForceManagement/Behaviors/AiForcePlanningBehavior.cs` | AI每小时为Recruit/WarRest评分；加载已有休整任务复核来源批次 | 新增PeaceDemobilize评分、战争切换/来源失效复核；军情保护时不给复员/休整加分 |
| `ForceManagement/Missions/PartyForceTaskCoordinator.cs` | 每次新任务会替换旧TaskId；目前还包含旧事务冻结guard | 保持任务唯一主人及稳定Task；新两项不新建冻结状态；不动Party创建事务 |
| `ForceManagement/Behaviors/PartyExchangeBehavior.cs` | `SettlementEntered`+驻留每日重试；每日每队每据点只进入一次；最新新增Party初始化期间禁访 | 保持这些行为，协调到达执行，不增加另一条重复访问事件 |
| `Fief/Services/FiefReturnService.cs`及`Fief/Models/PartyData/FiefPartyReturn.cs` | 有真正来源、城镇/村庄归属校验、批次、Hearth与冷却维护，同时存在旧事务回滚/RecoveryRequired | 返乡仍只允许真实来源；不新增冻结或回滚。若改这条共享路径，先审计其他调用者并保持身份、Hearth/冷却一致；避免无关重写 |
| `Fief/Services/FiefTroopPlacementService.cs` | 处理未绑定兵员安置；冷却 `AddTroops` 可返回实际接收数量 | 若保留作为额外安置，必须与批次返乡区分并受同一兵力下限及额度约束 |

## 3. 业务规则：哪些部队、何时、去哪里

### 3.1 和平复员

- 仅Mod管理的AI领主Party，和平连续 `PeaceStabilityDays=3` 后，现有 `GetPartyTarget` 报告 `Scenario=Peace` 且 `CurrentCount > TargetCount` 才有复员资格。**没有超编不复员，不拆英雄，不低于目标**。
- 每天最多 `min(30, ceil(当前队伍人数 × 0.20), 当日剩余可复员额度, 当前实时超额)`。以实际从野战Roster减少数计算额度。已在 `DemobilizationBehavior.SyncData` 保存的每日和平复员数继续沿用。
- **封邑服役兵**只在他们所属的真实来源封邑或本体允许的源村庄返乡；优先按超 `FreeWageDays` 和服役时间排序，但免薪到期不等于强制退役。维护真实批次、Hearth及原村庄冷却。
- **普通/职业兵**在本Clan拥有且有容量、无围城的城镇/城堡交给真实驻军；不能直接删除或转为封邑兵。没有合法接收点时保持超编，待下次有条件再执行。
- 有多个封邑来源时不要一次瞬移全部返回；AI应对一个合法目标执行，下次再选择另一个。有合法驻军且足够接收剩余普通兵时可优先处理。
- 和平复员任务比非紧急的日常驻军捐赠更优先；战斗/军团/围城/救援/原生避敌及刚占领城堡的高优先级行为仍然优先。

### 3.2 战时休整

- **仅正式交战状态**，军事AI没有军团/战斗/围城/追击/避敌或其他紧急任务，且 `WarRestPolicyModel` 审批通过时执行。
- 仅返回**真实在役封邑批次**至其真实源封邑；返回后野战部队人数不低于实时 `TargetCount`。不允许财务压力迫使部队低于战时目标。
- `MaximumDailyCount=30`，`MinimumUrgencyScore` **先保持源代码现有默认 `0.75`**；权重保持源代码的 `0.30/0.30/0.40`。本轮仅启用功能，**不擅自调低评分阈值至0.65，不调整战争倾向模型**。如运行测试触发过少，再以证据讨论调参。
- 必须只记录一次真实返乡人数：`PartyExchangeService.ExecuteFiefReturn()` 现在已经为WarRest记录 `ActualReturnedCount`；`PartyVisitExecutionService.ExecuteWarRest()` 额外安置的 `PlacedCount` 如代表不同士兵且确实减少野战Roster，才能另外计入。无法确认独立性则首版先关闭额外安置，避免重复计数和下限突破。
- 当日实际休整后不再立刻正常补充同一批封邑兵；军情突然变紧急时允许高优先级行动取代休整。
- 战争结束立刻取消WarRest任务，重新进入和平计时；重新宣战立刻取消和平复员任务。

## 4. Codex实施顺序（每阶段单独核验）

### P0. 接通配置与真正启用

**文件**：`ModuleData/militaryConfig.xml`、`common/MilitaryConfig.cs`、`ForceManagement/Services/WarRestPlanningService.cs`、必要时 `UnifiedForceContracts.cs`。

- 设置 `<EnableAutomaticDemobilization value="true"/>`、`<EnableDemobilizationRouting value="true"/>`，保留 `PeaceStabilityDays=3`、`MaxDailyDemobilizationRatio=0.20`、`MaxDailyDemobilizationCount=30`。
- 在已有 `ForceManagement` XML区域增加：

```xml
<WarRest>
  <Enabled value="true"/>
  <MaximumDailyCount value="30"/>
  <MinimumUrgencyScore value="0.75"/>
  <DispositionWeight value="0.30"/>
  <FinanceWeight value="0.30"/>
  <ServiceWeight value="0.40"/>
</WarRest>
```

- 用配置构造 `WarRestPolicySettings`，移除 `BuildContext` 中硬编码的 `Enabled=false`；对非法配置显式拒绝，旧XML缺失节点时采用安全缺省，不破坏读旧存档。
- 有结构化的、适度的配置生效日志（加载时一次即可）；**只做到P0不能宣布任务完成**。

### P1. 和平复员任务与路线真正打通

**文件**：`AiForcePlanningModel.cs`、`PartyForceCandidateService.cs`、`AiForcePlanningBehavior.cs`、`PartyForceTaskCoordinator.cs`、`UnifiedForceContracts.cs`、`DemobilizationBehavior.cs`、`PartyVisitExecutionService.cs`，必要时任务持久化与本地化文件。

- 在 `PartyForceIntent` 尾部追加 `PeaceDemobilize = 4`（旧 `Maintain=0, Recruit=1, WarRest=2, Suspended=3` 不变）。使用已有 `PartyForceTask`/Persistence，不另建 `_routingTargets` 控制系统；旧 `RecordRouting`/`ShouldCancelRouting` 统一成为非权威兼容辅助或从实际调用中移除，防止两个路由主人。
- `PartyForceCandidateService` 在稳定和平且超编、低优先级空闲时枚举候选：真实封邑服役来源、合法同Clan驻军接收点。必须结合对应接收能力、批次真实性及路径可达性；与正常Recruit的绑定限制**分开判断**，不要因为“和平复员能去本Clan城堡”就开放普通征兵权限。
- `AiForcePlanningModel` 在合适和平时机下发复员Task；WarRest仅在正式战争中规划。相同Intent+目的地+来源批次的有效Task续用旧ID；不每日重建。确保低优先级复员不会压过军情/原生撤退行为。
- `AiForcePlanningBehavior.OnAiHourlyTick` 为PeaceDemobilize加入原生 `GoToSettlement` 候选评分；任务一旦不再合法或军事优先级上升，不加分、取消/暂停并让原生AI重新思考。绝不在每Tick直接 `SetMove*`。
- `PartyVisitExecutionService.Execute` 增加 PeaceDemobilize 到达分支；无匹配任务的自然访问亦保留和平复员入口，且普通非紧急捐赠不能提前return而饿死复员。实际合法接收范围按复员目的判断，不再把一切入口都卡死于 `IsBoundSettlement`。
- 存档加载后对复员任务重新核验正式和平、持续天数、目标超编、来源/驻军归属、路线和危险状态。保留现有 `PartyCreationInitializationScope.IsInitializing` 拦截。

### P2. 同步迁移：只修改启用这两项功能需要的兵员路径

**文件**：`DemobilizationService.cs`、`TroopTransferService.cs`、`PartyVisitExecutionService.cs`；返乡只在有必要时调整 `FiefReturnService` / `FiefPartyReturn` 的最小范围。**不要重写通用 `PartyExchangeService.ExecuteRosterTransfer`，因为最新初始建Party逻辑也在使用它。**

- 预检查战和、队伍安全状态、目的地合法性、目标剩余容量、准确的来源Roster和封邑批次、伤兵/健康兵数量及实际安全超额。通过后生成本次只读的迁移清单 `CharacterObject + HealthyCount + WoundedCount`。
- **普通兵→驻军**：针对清单中的每一个兵种，先添加目标驻军，再扣除来源对应人数；健康兵与伤兵分开调用本体 `AddToCounts` 并保留正确的 `woundedCount` 参数。`AddToCounts` 返回的是索引，成功判断按其本体语义，不以返回索引当数量；从来源扣除的数量就是已批准且本次成功写入的清单数量。不新建 `TryAddTroops` API/整Roster差值重算/反向补偿链。
- **封邑服役兵返乡**：只处理当前真实合法来源及真正批次。目标是权威封邑返回冷却队列，不能将普通驻军的Roster迁移逻辑照搬到Hearth；源扣兵、服务批次解绑、Hearth占用释放与冷却队列进入都必须由当前封邑权威对象维护。若必要，使用目标接收接口本身返回的接收数决定扣除数；不建第二份批次账本。
- **未绑定封邑兵额外安置**：必须有独立真实来源、真实人员减少、剩余配额和安全下限才可启用；做不到则本轮不启用这条可选分支，以核心两功能先跑通为准。
- 不扩展招募 `FiefPartyRecruitment`、雇佣兵交易、物资、财政或初始Party动员的全局“无回滚”改造。旧代码中确实存在回滚和冻结：不得把它们当作本次新增功能，也不要新建此类机制；**若新两条功能会触发旧 `RecoveryRequired` 冻结，Codex必须在受影响的精确调用链中消除冻结依赖或列出明确阻塞，不能宣称已经满足用户的“禁止冻结”要求。**
- 遇到异常要记录具体部队/目的地/当前步骤/已执行人数并结束本轮，不进入冻结状态、不对已执行部分做自动反向补偿，也不重放同一交易。保持数据变更顺序容易审计。

### P3. 战时休整实际执行和进度

**文件**：`WarRestPlanningService.cs`、`AiForcePlanningModel.cs`、`PartyVisitExecutionService.cs`、已有可保存状态的 `AiForcePlanningBehavior`（如需要）、最小必要的 `PartyExchangeService` 相关接口适配。

- 保留现有 WarRest 策略审批和安全下限；配置启用后确保有真实批次时能生成并保存Task，不把当日同一任务重建成新TaskId。
- 到达真实来源封邑后，复核战争、军情、批次、Clan、`TargetCount`和日额度，执行返乡；按实际返回人数计入一次。`GetRemainingDailyQuota` 应读取跨存档仍准确的每日休整进度；推荐在现有AI规划Behavior的 `SyncData` 所有权下保存，而非增加独立数据源。
- 战时额外 `TryPlaceAfterReturn` 若参与执行，须明确已返回封邑批次与额外安置的是两个不重叠兵员集合；每一步不能让野战部队低于实时战备目标。无法静态证明时只保留真实批次返乡。
- 保持同日休整后抑制普通征兵，允许已存在的更高优先级军情覆盖；撤销任务不能误删返乡结果。

### P4. 集成验证、文档和交付

- 配置启用日志确认和平开关true、寻路true、WarRest true、限额30、阈值0.75。
- **和平场景**：例如现有160人、和平目标60人，在稳定和平第3天起，AI于非紧急状态寻找合法同Clan驻军/封邑，并实际降低至目标附近；不得一次超过当日额度；容量不足则保留剩余部队；战事重启立即放弃和平复员。
- **战时场景**：真实在役封邑兵、军事安全超额和评分合格，AI进入原来源执行；目标人数以下禁止休整；战争结束立即取消WarRest路径；同日不重复征回。
- **伤兵**：20健康+5伤兵的迁移清单，分别添加目标后从来源扣相应人数；不能把5名伤兵当健康兵、不能以 `AddToCounts` 返回的Roster索引当实际人数。
- **来源限制**：一支Party持有来自A、B两个封邑的服务批次，在A只能返回A来源，不能瞬移B的兵；普通兵按同Clan驻军权限转移。
- **任务存档**：走路途中保存/读取可恢复同一合法Task；WarRest当日已执行额度不会因为读档归零；旧Save键和旧枚举值不变；读档后若变更战争/归属则任务取消。
- **与最新7999837的集成**：新建AI Party、初始动员预演和实际提交、`PartyCreationInitializationScope.IsInitializing`、从驻军提兵、封邑兵初始征召等原有功能必须仍能正常运行。不得为简化复员而变更创建功能的公共接口和许可。
- **无冻结/回滚扩展**：在本轮修改的和平复员/战时休整路径中不得新写 `Rollback*`、`RecoveryRequired`、`IsDomainFrozen`、自动补偿、人工解冻等机制。对于共享文件已有分支，须提供调用链说明和是否会阻挡本功能的明确证据，而不是假定没有冲突。
- 静态检查、编译、相关单测及游戏实测分别报告 `PASS/FAIL/BLOCKED`；按仓库说明使用适合的 `ModifiedArmy/tools/Invoke-ReleaseValidation.ps1` 等脚本。没运行游戏不能声称游戏内行为已通过验收。
- 最终输出修改文件列表、关键diff、配置实际读取结果、路线与迁移执行证据、失败原因分布、编译/测试结果、旧存档兼容性及**未解决阻塞**。不提交/推送、不做Git回滚。

## 5. 非目标（明确禁止顺手扩展）

- 不重新设计 AI 创建Party的兵力评估、干跑规划、初始封邑动员、统一招募预算。
- 不全局删除或重写仓库所有事务恢复代码；这项若用户以后单独要求，需要另开设计任务。
- 不修改 `ModifiedPolitics` 有效户数、村庄Hearth自然变化公式、职业兵生产速度、军事制度权重、佣兵需求、粮食及外交。
- 不新增独立和平寻路Behavior、重复 `SettlementEntered` 监听者、另一个TroopRoster快照系统或兵源池。
- 不调整战时 `MinimumUrgencyScore`、和平目标人数计算公式，除非游戏实测后用户明确要求。

## 6. Codex完成定义

**成功只有一种定义：两条功能已在实际代码中开启且能够触发完整AI行为；兵员真实移动，封邑与驻军记录正确；同步迁移遵守“先目标、后来源，伤兵分开”；无新回滚/冻结；最新Party创建动员功能未被破坏；旧存档兼容。**

代码阅读参考：
- https://github.com/GuihuaZhou/bannerlord/commit/79998373c90204634b80f3a6582be82ea3723656
- https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Services/WarRestPlanningService.cs
- https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/DemobilizationBehavior.cs
- https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs
- https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Services/TroopTransferService.cs
- https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs

