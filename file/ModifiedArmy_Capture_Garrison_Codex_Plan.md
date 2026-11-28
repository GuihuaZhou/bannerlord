# ModifiedArmy：军团攻占定居点后的驻军补充机制修复 — Codex 实施任务书

## 1. 任务与施工基线

- 仓库：`GuihuaZhou/bannerlord`；本地工作目录：`D:\workspace\bannerlord`。
- 分析基线：[90d1e2a35d6dbb4a169aa925788a2d7da3020305](https://github.com/GuihuaZhou/bannerlord/commit/90d1e2a35d6dbb4a169aa925788a2d7da3020305)。施工前检查当前 HEAD 和工作区；如基线之后有提交，以实际源码为准核对调用链，不覆盖无关修改。
- 修改模块：`ModifiedArmy`。除明确标注原版反编译参考代码外，下述源码路径均相对于 `ModifiedArmy/ModifiedArmy/`。
- 目标：军团成功攻占城镇或城堡后，由本次攻占军团的所有符合条件的 AI 领主部队共同向新驻军转移士兵；禁止自动从玩家主部队抽兵。军团长仅作为调度关联对象，不意味着必须贡献士兵。
- 本文件为施工方案，不代表已经修改、编译或实机验证代码。

## 2. 已确认的行为及参数

| 项目 | 规则 |
|---|---|
| 城镇初始驻军目标 | 125 名普通士兵 |
| 城堡初始驻军目标 | 75 名普通士兵 |
| 每支贡献部队最低保留兵力 | **75 名普通士兵**（仅攻占补兵；原为 30） |
| 玩家主部队贡献 | 0 |
| AI 军团长、AI 军团成员 | 符合条件则参加 |
| 玩家 Clan 内的 AI 领主部队 | 符合条件则参加，不排除整个玩家 Clan |
| 兵力不超过 75 的部队 | 不贡献 |
| 部队缺少合法可转移普通士兵 | 不贡献 |
| 独立非军团攻占 | 保持既有规则，不扩大本次范围 |

125/75 是定居点的总驻军目标，不是单支部队的贡献指标；驻军目标、容量和兵员安全约束优先于全员至少贡献一人的要求。缺口不足、部队无可转移兵力、审批不通过等情况下，允许个别 AI 部队贡献为零。不得突破 75 人保留限制，也不得通过其他入口对玩家自动抽兵。

## 3. 源码分析与根因

以下链接固定在分析基线，行号用于定位；施工前以当前源码为准。

| 源码及方法 | 已确认问题 |
|---|---|
| [code/GarrisonTroopsCampaignBehavior.cs:273–294](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/code/GarrisonTroopsCampaignBehavior.cs#L273-L294) `CalculateMobilePartiesIdealPartySizes()` | 原版明确排除 `MobileParty.MainParty`，无论军团长还是附属成员。 |
| [GarrisonCaptureSupplyBehavior.cs:285–316](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/Garrison/Behaviors/GarrisonCaptureSupplyBehavior.cs#L285-L316) `TryDonateArmyTroops()` | 遍历 `leader.Army.Parties` 时未排除玩家；按可贡献兵力降序依次填满缺口，第一支部队可能承担全部补兵。 |
| [GarrisonTransferPolicyModel.cs:112–126](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonTransferPolicyModel.cs#L112-L126) `GetAllowedCaptureContributionCount()` | 原保留兵力为 30；缺少玩家保护。 |
| [PartyVisitExecutionService.cs:111–159](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs#L111-L159) | 旧 `ExecuteCaptureDonation()` 在检查富余兵力前消费整个定居点许可；新 `ExecuteCaptureContribution()` 未排除玩家；现有 operation ID 仅包含部队、定居点和日期。 |
| [GarrisonCaptureSupplyBehavior.cs:145–191](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/Garrison/Behaviors/GarrisonCaptureSupplyBehavior.cs#L145-L191) | `TryConsumeTroopDonationPermit()` 直接删除 `_pendingCaptures[settlement]`，可能提前结束整个军团流程。 |
| [PartyExchangeBehavior.cs:110–136](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/PartyExchangeBehavior.cs#L110-L136) | 旧单部队攻占补兵入口与军团整体入口竞争。 |
| [GarrisonDonationAdapter.cs:30–99](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/Garrison/Services/GarrisonDonationAdapter.cs#L30-L99) | 原版入口允许部分非受管部队执行，并可能消费攻占许可，需要针对攻占场景统一拦截。 |
| [GarrisonCaptureSupplyBehavior.cs:419–441](https://github.com/GuihuaZhou/bannerlord/blob/90d1e2a35d6dbb4a169aa925788a2d7da3020305/ModifiedArmy/ModifiedArmy/Garrison/Behaviors/GarrisonCaptureSupplyBehavior.cs#L419-L441) | 日志以军团长为部队名，却输出军团贡献总量，可能误导定位。 |
| `ForceManagement/Services/ManagedPartyPolicy.cs:15–23` | `IsModManagedAiParty()` 排除整个玩家 Clan，不可作为此次攻占贡献资格。 |

结论：玩家可被选为贡献者是已确认代码缺陷；降序填充算法可导致仅抽一支部队。竞争入口和事件时序是需要施工及实机验证的额外风险，不能仅凭静态分析断言本次游戏中具体触发顺序。

## 4. 兵力分摊设计

### 4.1 计算需求及实际贡献能力

```csharp
int target = settlement.IsTown ? InitialTownGarrison : InitialCastleGarrison;
int deficit = Math.Max(0, target - garrison.MemberRoster.TotalRegulars);
int freeSlots = GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison);
int requested = Math.Min(deficit, freeSlots);
```

使用现有 `GetTransferFreeSlots()`，保留驻军软上限、硬容量和外出征粮队预留名额等规则。不要以民兵计入驻军人数。

单支部队的人数上限：`max(0, NumberOfRegularMembers - 75)`。实际候选容量还需结合非英雄、未被封建兵服务记录保护的可转移普通士兵，以及驻军兵种准入；不得仅因人数超过 75 就认定一定能成功转移。预估或 dry-run 不得写名册，不得反复产生副作用；实际审批必须使用实时状态。

### 4.2 确定性整数分摊

输入为实际需求 D、每支合格部队的容量 C[i]。令 T = min(D, sum(C[i]))，仅保留 C[i] > 0 的候选，N 为候选数量。

1. T = 0 时全部分配 0。
2. T >= N 时，每支候选先分配 1 人；从各自容量中扣除 1，剩余需求按剩余容量比例分摊。
3. T < N 时，只能选择 T 支部队各贡献 1 人。按容量降序、Party.StringId 序数升序稳定选择，避免在其他部队尚未贡献时让某支部队承担多人。
4. 比例分摊使用整数除法和最大余数法（建议使用 long 乘积避免溢出）；余数相同按 Party.StringId 序数升序。分摊量不得超过各自容量，合计必须等于 T。
5. 所有部队先按首轮名额执行；不允许一支部队提前填满其他部队的首轮名额。首轮结束后，若存在合法缺口且还有实际贡献能力，再计算新一轮；仅根据真实提交数量重新计算缺口。
6. 对永久拒绝或本轮无法贡献的部队停止本轮重复尝试；暂时未就绪的部队保留合理的贡献机会。设定有限轮次/无进展退出条件，不允许同一事件死循环。

示例：城镇驻军为 0、可接收 125 人；玩家 300 人、AI A/B/C/D/E 分别为 200/150/100/80/60 人。假定均有足够合法可转移普通士兵，且其余审批通过。

| 部队 | 初始普通士兵 | 可贡献上限 | 首轮分配 | 剩余 |
|---|---:|---:|---:|---:|
| 玩家 | 300 | 0 | 0 | 300 |
| AI A | 200 | 125 | 67 | 133 |
| AI B | 150 | 75 | 41 | 109 |
| AI C | 100 | 25 | 14 | 86 |
| AI D | 80 | 5 | 3 | 77 |
| AI E | 60 | 0 | 0 | 60 |
| 合计贡献 | | | **125** | |

注：AI E 原本不足 75 人，不需要凭空补至 75；本次不得从其抽兵。

## 5. P1 — 攻占贡献资格及 75 人保留限制

文件：`Garrison/Models/GarrisonTransferPolicyModel.cs`。

新增统一资格判定，例如 `IsEligibleCaptureDonor(MobileParty party, Settlement settlement)`。候选必须为本次攻占军团的有效领主成员、非玩家主部队、未解散、领主存活、同王国，并在执行时已抵达目标定居点且脱离战斗。调用方必须另外确认本次攻占身份及成员资格，不能仅凭同王国和在城内即允许调兵。暂未抵达或尚在战斗的原成员属于待就绪，而非立即剔除并重新分配全部名额。

```csharp
public static int GetAllowedCaptureContributionCount(
    MobileParty party, int requestedCount)
{
    if (party == null || party.IsMainParty || requestedCount <= 0)
        return 0;

    const int minimumFieldRegulars = 75;
    int available = Math.Max(0,
        party.Party.NumberOfRegularMembers - minimumFieldRegulars);
    return Math.Min(requestedCount, available);
}
```

建议提取 `MinimumCaptureFieldRegulars = 75` 供本场景规划、执行、诊断共用。将 `NoTroopsAboveThirtyMemberFloor` 更新为 `NoTroopsAboveCaptureMemberFloor`，同步相关日志、原因码和测试。不得全局替换其他普通驻军交换中的 30 人下限。

## 6. P2 — 独立分配模型

建议新增 `Garrison/Models/CaptureContributionAllocationModel.cs`，例如：

```csharp
public static Dictionary<string, int> Allocate(
    int requestedCount,
    IReadOnlyDictionary<string, int> donorCapacities);
```

模型仅计算整数分配，不直接访问或修改名册、台账、存档、任务状态；按照第 4 节实现，覆盖零容量、需求不足、容量不足、余数排序、稳定性和边界值测试。不要为本需求引入通用新框架。

## 7. P3 — 攻占军团整体执行

文件：`Garrison/Behaviors/GarrisonCaptureSupplyBehavior.cs`。

重构 `TryDonateArmyTroops()`：删除现有 `OrderByDescending(GetAllowedCaptureContributionCount)` 后逐个填满驻军的逻辑。执行流程：

1. 验证攻占任务有效、驻军真实存在、定居点归属正确、围城结束及恢复状态允许修改。
2. 读取本次攻占军团成员快照，排除玩家主部队和永久失效成员。
3. 等待成员进入安全状态，计算可贡献容量及实际驻军缺口。
4. 使用分配模型生成首轮名额；按稳定顺序执行各自名额，预留暂未就绪成员的机会。
5. 逐部队复用 `PartyVisitExecutionService.ExecuteCaptureContribution()`，只以实际 `TransferredCount` 汇总贡献，记录 `Status`、`ReasonCode`。
6. 首轮结束后按实时缺口及剩余可贡献能力处理合法剩余分配；必要时延迟重试，不重新执行已提交批次。
7. 驻军补兵完成或任务终止后关闭兵员流程；粮食流程独立结算。

对实际提交的每次操作检查来源、目的名册守恒；部分提交必须保留真实成果，不将未提交数量当作完成。

## 8. P4 — 事件时序与任务生命周期

`GarrisonCaptureSupplyBehavior` 为攻占补兵唯一调度者。保留 `AfterSiegeCompletedEvent`、`SettlementEntered`、`HourlyTickPartyEvent`，必要时增加 `OnSettlementOwnerChangedEvent` 以确认真实 BySiege 易主和失效处理。

- 攻占事件必须关联真实定居点易主和胜利军团；单纯击退围城、普通进城、封邑分封、和平交割不能注册新攻占任务。核对 `SallyOut` 的事件语义，不仅凭 attackerWon 判定。
- 以本次攻占时的成员身份快照为准，后加入军团者不能被纳入旧任务；军团长可为玩家，仅承担触发和关联作用。
- 同一攻占事件的不同回调合并为同一任务，不重复生成任务。不得假定事件到达顺序。参考原版 `code/MapEvent.cs` 的 `AfterSiegeCompleted` 调用链，以及 `code/MobileParty.cs:1227–1230` 的附属部队 `CurrentSettlement` 同步。战斗结束事件可能早于战斗标记清理。
- 不强制修改 `CurrentSettlement`、`MapEvent` 或军团附属关系。未到达者不远程抽兵；首轮未就绪者保留贡献机会，已就绪者不得抢占其分配份额。
- 初始尚未进城和已经离开目标定居点须区分；军团解散、离开、归属失效、定居点再次被敌方夺取或无合法贡献可能时结束剩余兵员任务，避免永久挂起。
- 建议使用 Pending / Ready / Executing / Completed / Cancelled 等语义；不强制引入新的复杂状态机类。
- 兵员任务与粮食任务分别标记完成。粮食不足不得导致士兵已经补足后再次抽兵；粮食补给算法、目标及玩家粮食贡献许可保持原状。
- 同一攻占入城期间的防重入保护必须覆盖普通驻军调拨，即使攻占兵员刚刚完成，也不能被随后到达的同一次 `SettlementEntered` 回调立即反向抽走。离开并再次正常访问后解除本场景保护。

## 9. P5 — 清理旧单部队入口

| 文件 | 要求 |
|---|---|
| `ForceManagement/Behaviors/PartyExchangeBehavior.cs` | 移除旧 `CanDonateTroopsAfterCapture()` → 单部队 `ExecuteCaptureDonation()` 的分支；改为识别本次攻占军团入城并让统一调度处理。不要误消耗普通访问的日级防重键。 |
| `ForceManagement/Behaviors/AiForcePlanningBehavior.cs` | 移除或调整旧 `ExecuteCaptureDonation()` 包装入口。 |
| `ForceManagement/Missions/PartyVisitExecutionService.cs` | 清理旧单部队 `ExecuteCaptureDonation()`，保留并强化 `ExecuteCaptureContribution()`。 |
| `Garrison/Behaviors/GarrisonCaptureSupplyBehavior.cs` | 移除/重构 `TryConsumeTroopDonationPermit()`；不能因为单支部队查询或尝试转移而删除整个攻占任务。 |

全仓库搜索上述符号，确保没有残余调用。普通 AI 兵力规划、任务和驻军交换不得被全局禁用。

## 10. P6 — 原版 Harmony 竞争路径隔离

文件：`Garrison/Patches/GarrisonTroopDonationPatch.cs`、`Garrison/Services/GarrisonDonationAdapter.cs`。

- 原版 `OnSettlementEntered()` → `ManageGarrisonForArmy()` 会按军团成员分配驻军调拨；原版已排除玩家，但与新 Mod 调度并存会产生重复调兵或回抽。
- `ShouldRunNativeEntry()` 应在攻占场景对应军团的入口拦截原版交换，而不是在已经执行完原版计算后才阻止底层转移。
- 移除 `PrepareDonation()` 对攻占许可的消费，避免绕过军团调度。
- 核对原版 `OnSettlementOwnerChanged` 对 `_newlyConqueredFortification` 的设置及 `SettlementEntered`、Mod 攻占事件注册顺序。若单靠 pending 状态拦截存在空窗，应采用经源码验证的窄范围辅助判定或提前注册机制。若涉及原版私有标记，须保证仅匹配实际攻占定居点、事件及军团，不能污染后续普通访问。
- 普通驻军调拨保留现有 Harmony 语义，不得全局关闭。验证 `EnableUnifiedAiForcePlanning` 开/关两种情况下攻占机制仍无双重执行；本次专用攻占补兵不得错误依赖排除玩家 Clan 的受管 AI 开关。
- 原版反编译参考目录 `code/` 不属于实际 Mod 修改目标，不直接修改原版文件。

## 11. P7 — 复用事务及执行层保护

`ForceManagement/Missions/PartyVisitExecutionService.cs` 的 `ExecuteCaptureContribution()` 增加非玩家主部队、真实攻占任务关联、成员资格、实际抵达、同王国及脱离战斗等校验；玩家被拒绝时建议使用 `PlayerMainPartyExcluded`。

继续以 `PartyExchangePurpose.NewlyCapturedSettlement` 调用现有 `ExecuteRosterPlan()`，并传入本次贡献批次 operation ID。`PartyExchangeModel.BuildRosterPlan()` 与 `PartyExchangeService` 执行层应沿用 `GetAllowedCaptureContributionCount()` 的 75 人限制；检查所有攻占用途的入口都不能向玩家发出或提交自动抽兵计划。

必须保留现有以下规则：

- `GarrisonRecruitmentCapacity.GetTransferFreeSlots()` 和征粮队预留容量；实时重新验证剩余驻军目标和容量。
- `PartyTransferOwnershipPolicy` 的同王国、攻占场景单向存入真实驻军例外；不得扩展为跨 Clan 随意提兵。
- `RecruitmentModelManager.Model.BuildPlan()` 的现有兵种准入。
- `FiefPartyManager.GetServingTroopCounts()` 对受保护封建兵身份的排除、英雄排除、伤兵数量和名册守恒。
- 既有执行前校验、恢复保护、事务记录及部分提交处理。

不得新增直接写 `MemberRoster.AddToCounts()` 的旁路，不得清除或伪造封建兵服务记录，不得强制修改驻军 ActualClan、绕过审批或放宽普通调兵限制。

## 12. P8 — 操作防重复与存档兼容

原 operation ID 为 `capture-army:{PartyId}:{SettlementId}:{CampaignDay}`。同日再次攻占或需要合法二次贡献时会冲突。建议变更为：

```text
capture-army:{CaptureId}:{PartyId}:{RoundIndex}
```

- `CaptureId` 是一次真实攻占的稳定唯一标识，须随任务保存，不能在每次事件/读档时重新生成；两次真实易主攻占须不同。
- `RoundIndex` 标识已分配的具体批次。同批重试沿用同一 ID；只有上一批结算并产生新的合法剩余名额时才生成下一批 ID。
- 复用 `TransferOperationJournalBehavior`，其 `Committed` 和 `PartiallyCommitted` 均有终态记录。读档时结合已持久化进度与事务记录核对，禁止因 `OperationAlreadyCompleted` 而生成新 ID 盲目重做；不能把操作记录裁剪后的缺失当作允许重复执行的证明。
- 保留并兼容 `_modifiedArmyPendingCaptureDonations` 旧存档键。旧格式仅记录定居点和军团长，不能可靠恢复历史成员快照与已贡献数量；仅在能证明仍是有效攻占任务且不会重复扣兵时迁移，否则安全关闭旧兵员待办并保留既有真实驻军/名册；记录迁移原因，不重新执行历史攻占。
- 若需增加持久化字段，使用仓库已有可保存类型及注册方式，并保证缺失字段安全默认、读档后不会重复贡献。无需新增 XML/MCN 配置。
- 同一次任务完成后保留足以隔离同一次入城回调的状态，随后清理，避免长期积累。

## 13. 日志与诊断

区分军团汇总和每支实际贡献部队；只以真实提交人数作为业务日志。示例（示例名称及数值非真实游戏数据）：

```text
【攻占】卢孔军团占领俄尼拉，驻军缺口125人
【调兵】卢孔部队向俄尼拉转移55名士兵
【调兵】梅里迪尔部队向俄尼拉转移38名士兵
【调兵】阿庇斯部队向俄尼拉转移32名士兵
【驻军】俄尼拉完成驻军补充，共转移125名士兵
```

Debug 诊断至少包括 CaptureId、SettlementId、军团关联 ID、PartyId、资格/排除原因、当前普通士兵数、75 人下限、合法可贡献数、分配数、实际转移数、事务状态与 ReasonCode、驻军剩余缺口。`contributors`、`available` 等汇总统计必须使用与实际贡献相同的候选口径，不能包含玩家或不合法来源。

沿用 `RecruitmentLogFilter`、中文本地化和既有日志级别，正常业务日志仅针对玩家所在王国；【】内不超过 5 个汉字；使用不同场景日志模板，状态不变不重复刷屏。更新相应 `ModuleData/submodule_strings.xml`、`ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml` 或仓库当前实际使用的本地化文件。

## 14. 修改范围与非目标

| 功能 | 处理 |
|---|---|
| 军团攻占后的驻军补兵 | 本次修改 |
| 普通 AI 驻军征调、日常进城补兵 | 保持原规则，包括非攻占场景的 30 人下限 |
| AI 兵力需求、封建兵征召/归还/裁减、职业兵生产 | 不修改 |
| 封建兵身份及工资台账 | 保持原事务规则 |
| 军团粮食贡献 | 保持现有规则，包括攻占时 `allowMainParty = true`；仅隔离兵员与粮食完成状态 |
| 城镇/城堡驻军目标 | 125/75 不变 |
| XML、MCN 配置 | 不新增 |
| 非军团独立攻占 | 不扩大范围 |
| 其他 Mod | 不修改 |

## 15. 验收清单

### 15.1 分配模型自动化测试

- [ ] 空候选、需求为零、容量为零、不足 75 人、恰好 75 人、76 人等边界。
- [ ] 需求足够时所有正容量候选至少 1 人；需求小于候选数量时尽量覆盖不同部队。
- [ ] 分配合计准确；单部队分配不超容量；整数余数确定性；相同输入不同枚举顺序产生相同输出。
- [ ] 驻军实际需求 125 人、75 人、已有驻军、容量不足、总贡献能力不足。
- [ ] 部队原本少于 75 人时零贡献；原本至少 75 人者转移后不低于 75 人。

### 15.2 调度、事务和实机验收

| 场景 | 预期 |
|---|---|
| 玩家率领军团攻占城镇 | 玩家不自动损失士兵；合格 AI 成员共同贡献 |
| AI 率领军团攻占城镇 | AI 军团长和合格成员共同贡献 |
| 玩家加入 AI 军团 | 玩家零贡献 |
| 玩家 Clan 的 AI 部队 | 正常参与 |
| 城堡攻占 | 目标 75 人，贡献部队各保留至少 75 人 |
| 部队 74/75/76 人 | 分别最多贡献 0/0/1 人 |
| 部队只有受保护封建兵 | 不得错误抽调封建兵 |
| 驻军已有部分兵力或容量不足 | 不超实际缺口与合法容量 |
| 某支部队转移失败/部分提交 | 记录真实数量，其他部队正常贡献；不重复已提交批次 |
| 成员暂未进城或 MapEvent 未清除 | 安全等待，保留贡献机会，不远程转移 |
| 攻占后加入军团、军团解散、再次失去定居点 | 不误抽兵；任务适时终止 |
| 多回调重复触发、先后顺序变化 | 唯一任务，无重复抽兵或反向回抽 |
| 攻占后粮食未补足 | 不重复发起已完成的兵员任务 |
| 同日真实再次攻占同一定居点 | 新 CaptureId，合法贡献不被旧操作 ID 阻断 |
| 旧存档及新任务中途读档 | 安全恢复/迁移；不重复已完成操作 |
| 非军团攻占、普通进城、日常驻军调拨 | 保持既有行为 |
| EnableUnifiedAiForcePlanning 开关两种状态 | 无重复执行、无玩家抽兵 |

核心不变量：单次成功转移中，来源普通士兵减少量等于驻军增加量；伤兵及身份保护一致；玩家自动贡献恒为零；任何本次攻占贡献不使原本至少 75 人的部队低于 75；不突破真实驻军需求和容量。

## 16. Codex 交付要求

1. 按 P1–P8 实施；先确认当前仓库实际代码、原版事件顺序和可保存类型，再落地方案。若存在与方案冲突的实现，说明原因并采用满足已确认行为的最小兼容改动。
2. 全局检查旧 `ExecuteCaptureDonation`、`TryConsumeTroopDonationPermit`、`CanDonateTroopsAfterCapture` 等调用；确认竞争入口已清理或转换为无副作用的任务查询。
3. 增加或使用仓库现有自动化测试项目，执行分配模型与相关回归测试；无法实机测试的项目标记为待验证，不声称通过。
4. 编译 `ModifiedArmy`，不得引入新的编译错误；报告警告及原因。按需执行部署，部署和实机验证分别报告。
5. 输出实际修改文件、关键变更、测试/编译结果、事件顺序验证结果、存档兼容方案及残余风险。
6. 不修改无关文件，不自动提交 Commit。

本地编译部署命令：

```powershell
cd D:\workspace\bannerlord
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Deploy-Mods.ps1 -Build -Configuration Release -Modules ModifiedArmy
```

## 17. 设计决策与待验证事项

用户已确认：仅攻占场景、排除玩家主部队、所有符合条件 AI 部队共同贡献、每支贡献部队最低保留 75 人。沿用现有城镇 125 / 城堡 75 驻军目标。比例分摊、首轮覆盖、最大余数、唯一攻占标识、任务生命周期、日志和迁移策略为施工设计决策；不属于已经实现的功能。真实事件先后、原版 Harmony 拦截空窗、读档事务进度恢复、各兵种审批结果仍需施工期间核实。
