# ModifiedArmy：AI 新建领主 Party 前的统一调兵 DryRun 施工方案

> **交付性质**：给 Codex 的逐文件实施说明；本文不代表代码已修改。
>
> **审查基线**：`GuihuaZhou/bannerlord`，GitHub `main` 分支；审查日期 2026-11-15。检索到的最新提交：`e971dfba9cdf44e1233c94d57c5aca9f4911e338`（Codex 施工前请核对 HEAD；若变更，先做差异检查）。
>
> **目标模块**：`ModifiedArmy`，仅针对 **Mod 管理的 AI 领主新建 Party**；普通 AI 后续补员调用原有入口，但共享改造后的底层计算。**禁止直接修改玩家主队、商队、村民、原版未接管的 Party、原有雇佣兵交易规则。**
>
> **用户确定的目标**：不允许以「先创建 Party，再尝试调兵；0 人就解散」作为常规建军逻辑。统一的调兵系统应支持 *DryRun*，从候选、模板、编制、工资、预算、驻军安全、村庄 Hearth 直到具体兵种与来源村庄，运行一次不扣资源的完整预演。预演确认能获得足够士兵后才创建真实 Party；实际提交必须复用同一套规则。

---

## 0. Codex 必须遵守的工作方式

1. 先阅读本方案中列明的**实际生产代码**，不得直接按照旧 Markdown 设计覆盖现在的实现。`ModifiedArmy/ARCHITECTURE.md` 日期为 2026-08-29，存在旧结构描述，只作历史参考。
2. 先完成纯模型和单兵源预演，再接入「封邑兵 + 驻军」组合，最后改创建入口；每阶段单独编译并做测试。
3. **保持现有架构边界**：Model 算计划、Service 做实际资源事务、Behavior 管事件与持久化。不要把所有业务塞入 `PartyCreationCoordinator` 或大范围新增 Harmony Patch。
4. 保留现有 `PartyExchangeService.Execute(PartyExchangePlan)`、`RecruitmentModelManager.Model.BuildPlan(MobileParty, ...)`、正常 AI 补员、封邑兵回乡/战争休整等入口的兼容适配。
5. 不要通过创建临时 `MobileParty`、创建再销毁真实 Party、给虚构 Party 注册 Clan 战队等方式完成 DryRun。
6. DryRun 严禁对真实 `TroopRoster`、`Village.Hearth`、Clan 预算余额、封邑兵批次、Party 绑定、调兵 journal、Hero 归属进行写入。可以使用**仅内存中的临时模拟名册和预算拷贝**。基地预留 token 是外层创建流程管理的短期互斥，不应由纯 DryRun 暗中创建。
7. DryRun 不保证资源锁定；创建后正式提交前**必须重新校验**。若发生不可验证的部分提交，保留恢复状态，不能清除 Party 导致士兵/服役记录孤儿化。
8. 不要添加改变强度平衡的新 XML 配置；保留现有 `50` 名普通兵建军门槛、封邑最小批次 `MinimumFiefRecruitBatch`（当前默认 25，读取实际 XML）、工资与经济规则。
9. 完工须提交**逐文件改动摘要、构建结果、回归测试记录、成功和失败各一次的日志示例**。禁止用“编译通过”代替运行时验证。

## 1. 已确认的现状与问题（以仓库代码为准）

### 1.1 创建链

- `Recruitment/Patches/HeroSpawnSettlementPatch.cs`：Harmony 拦截 `HeroSpawnCampaignBehavior.SpawnLordParty`，仅 `ManagedPartyPolicy.IsModManagedAiLeader` 进入 `new PartyCreationCoordinator().TryCreateManagedLordParty(...)`。
- `PartyManagement/Models/PartyCreationSettlementModel.cs`：`FindEligibleBase(leader, clan, 50)`，逐领地只读检查绑定 + `EstimatedGarrisonRegulars + EstimatedFiefRegulars >= 50`，按估算人数优先、距离次之选择。`GetEligibleFiefRegulars` 使用 `element.Number` 且仅检查 `template.IsEnableTroop`；没有扣伤兵/模板正权重、工资与 Hearth 审批。
- `PartyManagement/Services/PartyCreationCoordinator.cs`：`TryReserveForCreation` → 可能移除总督 → `PartyCreationScope.Begin` + `MobilePartyHelper.SpawnLordParty` → 实际 `PartySizeLimit >= 51` → `TryCommitCreation` → 注册初始招募 Task → `EnterSettlementAction.ApplyForParty` 触发事件式调兵。若初次封邑兵 0 人，尝试驻军 fallback；封邑兵成功但不足 50 时并不会补足。最终**仅 `TotalRegulars > 0` 即接受新 Party**，这与预检的 50 人不一致。
- `PartyManagement/Models/NewPartySizeLimitModel.cs`：`FindAppropriateInitialRosterForMobileParty` 在 `PartyCreationScope` 作用域中返回空初始普通兵名册，避免原版直接发兵。
- `ForceManagement/Behaviors/PartyExchangeBehavior.cs`：进入城镇与每日驻城触发 `EvaluateVisit`，有 party+settlement+day 去重；`OnSettlementEntered` 不是可靠的“创建事务提交入口”。
- `ForceManagement/Missions/PartyForceTaskCoordinator.cs`：初始建军 Task 一次仅一个 `SourceKind`（`Fief` 或 `Professional`），且 `SelectAvailableSource` 仍根据旧估算数量判断。

### 1.2 当前实际调兵链

```
AiForcePlanningBehavior.ExecuteAtSettlement
  -> PartyVisitExecutionService.Execute
    -> UnifiedRecruitmentExecutionService.Execute(MobileParty, Settlement, SourceKind, ...)
      -> ForceManagementFacade.GetPartyTarget(party).GapCount
      -> Fief 或 Garrison 分支
      -> PartyExchangeService.PrepareContext(context)  [有副作用]
      -> PartyExchangeModel.BuildPlan(context)
      -> PartyExchangeService.Execute(plan)
```

- `UnifiedRecruitmentExecutionService.Execute` 要求 `party.CurrentSettlement == settlement`，真实 `party.ActualClan != null`；只执行一个来源。
- `PartyExchangeModel.BuildRosterPlan`：`GarrisonDefense.Evaluate` + `GetSafeTransferOutCount`、扣除 `GetServingTroopCounts` 保护人数；`FillSoftGarrisonWithdrawalManifest` 基于双方角色比例及 `RecruitmentSimulationState.Create(destination)`，**逐兵**调用 `EvaluateRecruitment` 决定兵种/人数。
- `PartyExchangeModel.BuildFiefMobilizationPlan`：`TryResolveOperationScope` + `PreviewFiefRecruitmentApprovals`，返回按 `CharacterObject` 聚合的批准清单。
- `Fief/Models/PartyData/FiefPartyRecruitment.cs`：AI 封邑兵 `BuildTemplateWeightedRecruitmentCandidates` 只使用健康人数、模板正权重根与升级树匹配；`BuildAiFiefRecruitment` 调用统一 `RecruitmentModelManager.Model.BuildPlan`；审批不足最小批次时整个封邑兵结果为 0。
- `Recruitment/Models/DefaultAIRecruitmentModel.cs`：`EvaluateRecruitment` 校验 `party.IsActive`、兵种是否合法、有效编制、目标兵力、90% 工资上限、即时购买成本、30 日财政。`Fief` 和 `Transfer` 购买费为 0；特别注意 `Transfer` 还**跳过购买和长期维持资金限制**，但不跳过工资上限/编制。
- `Recruitment/Models/RecruitmentSimulationState.cs` 已有内存模拟 `Commit`，可以改造复用，而不是重复造一套审批器。
- `ForceManagement/Services/PartyExchangeService.cs` 已有 `DryRun(PartyExchangePlan)`，**仅 `Validate(plan)`**，并不从零完成建军预演；更不支持不存在的目标 Party。`PrepareContext` 会调用 `FiefPartyManager.ReconcilePartyFiefTroops`，不能在新 DryRun 中使用。

### 1.3 村庄 Hearth 与跨来源事务的实际风险

- `Fief/Models/VillageFiefData.cs::TryMobilize(count)`：要求非 RecoveryRequired、村庄存在、`floor(village.Hearth) >= count`，然后调用 `SettlementPopulationServiceRegistry.Current.TryConsume(villageSettlement, count, operationId)`；即**调走 1 名就绪封邑兵耗 1 Hearth**，不能将训练/供养的文化 `FeudalSupplyCost` 当作本次扣除比例。
- `ModifiedArmy/Population/Contracts/PopulationContracts.cs::ISettlementPopulationService` **已经有** `EvaluateConsumption(settlement, amount)`，`ModifiedPolitics/.../SettlementPopulationBehavior.cs` 实现该只读检查；新代码应复用，不得调用 `TryConsume` 预演。
- `FiefPartyRecruitment.TryCommitVillageRecruitment` 会按村庄分配兵源，做 Hearth 前置校验，然后依次移除村庄就绪兵、扣 Hearth、创建/登记服役批次、加入目标 Party；异常时尝试恢复这些对象，恢复不完整则 `RequireRecovery`。
- **预算边界**：`FiefPartyRecruitment` 在村庄动员事务成功后另行调用 `ClanRecruitmentBudgetManager.CommitRecruitment` 记账；需要检查如果记账/后续日志阶段异常如何保留准确恢复状态，不可假定村庄事务成功就意味着预算记账必然完成。
- **关键缺口**：当前 `PreviewFiefRecruitmentApprovals` 只返回全封邑按兵种汇总，不返回“具体从哪一村庄拿哪种兵”。最终提交时再按照村庄顺序分配，如果某个村庄 Hearth 不足，虽然全封邑兵够，仍可能整笔失败。因此必须抽取**按村庄的只读分配构造器**，预演及真实提交复用同一个分配结果与校验。
- `PartyExchangeService` 的单次驻军名册转移有回滚，但**不是“驻军 + 封邑兵”两次提交的跨来源原子事务**。整个建军事务必须建立额外的补偿/恢复协议。

### 1.4 创建前已经存在的原版接口（不要重写）

- `Campaign.Current.Models.PartySizeLimitModel.GetAssumedPartySizeForLordParty(leader, clan.MapFaction, clan)`：原版 `code/DefaultPartySizeLimitModel.cs` 有真实实现，无需创建 Party。注意它与创建后实际 `Party.PartySizeLimit` 可能有技能应用场景差异，所以后者仍需重验。
- `code/LordPartyComponent.cs::InitializationArgs.InitializeLordPartyProperties`：**先将领主本人 `AddElementToMemberRoster(..., 1, true)`**，再初始化普通兵名册。当前 Scope 清空普通兵初始名单，因此预演起点应为**1 个英雄占编制、0 名普通士兵**，不是总人数 0。
- `code/LordPartyComponent.cs`：新 Party `_wagePaymentLimit` 初始值为 `PartyWageModel.MaxWagePaymentLimit`；`ModifiedArmy/PartyManagement/Finance/Services/ClanFinancialEvaluationService.cs` 后续调用 `MobilePartyWageLimitCalculator.Calculate` 和 `party.SetWagePaymentLimit`。**正式新建时必须在首次调兵前应用与预演一致的工资上限**，否则 DryRun 和 Commit 不同口径。
- `PartyManagement/Finance/Models/MobilePartyWageLimitCalculator.cs`：`Calculate(Clan, MobileParty)` 的核心条件是 Clan 领袖金币、指挥官是否 Clan Leader、战争工资倍率。应抽取不依赖 Party 的核心算法，保留原签名做适配。

## 2. 功能范围与最终成功条件

定义建军成功：**一个合法的 AI 领主 Party，首次初始化后拥有至少 50 名普通士兵（`MemberRoster.TotalRegulars >= 50`），其士兵来自一个或两个合法来源，所有源/目标名册、Hearth、服役批次、预算与事务账簿一致。**领主本人不计入“50 名普通士兵”，但占 `NumberOfAllMembers` 的编制空间。

- 50 人是**建军门槛**，不是“本次招募总量上限”；预演按当前 `PartyForceTargetModel` 求新 Party 目标人数，但必须保证 `ApprovedRegularCount >= 50`；若 `TargetCount < 50` 或真实编制无 50 个普通兵空位，直接不创建。
- 同一建军尝试：默认**优先封邑兵**（只有批准量达到最小批次才接纳），不足时按剩余 Party 空位、工资限额与驻军可调人数补足；驻军转移不是市场购买，保持其现行财务政策。
- 本方案**不要求玩家/日常 AI 补员一次同时招两个来源**；第一期多来源组合仅用于初始 Party 创建，未来可以复用组合规划器。
- 预演得不到 50：`TryCreateManagedLordParty` 返回 `null`，不能调用 `SpawnLordParty`，不能移除总督，不能消费资源。
- 预演通过但建军前快照改变：重新预演**一次**或本次返回拒绝（建议只重新预演 1 次），绝不可贸然提交旧清单。
- 创建后仍出错：若全部资源已核实恢复，可安全清理空 Party；若出现残余兵力/未恢复服役记录/事务 RecoveryRequired，**保留 Party + 绑定 + 恢复标记**，禁止调用 `DestroyPartyAction`。

## 3. 新增的合同与接口（代码示意，Codex 可按项目命名调整）

建议放在 `ForceManagement/Contracts/` 和 `Recruitment/Models/`，**不要修改现有 `PartyExchangePlan` 的语义来伪装一个不存在的 MobileParty**。

```csharp
public enum RecruitmentSubjectKind { ExistingParty, PlannedLordParty }

// 每次计算的“部队状态”，非持久化、不能持有可变 live roster 作为目标。
public sealed class RecruitmentSubject
{
    public RecruitmentSubjectKind Kind { get; init; }
    public MobileParty LiveParty { get; init; } // DryRun 新建时为 null
    public Hero Leader { get; init; }
    public Clan Clan { get; init; }
    public Settlement Location { get; init; }
    public bool IsLordParty { get; init; }
    public bool IsGarrison { get; init; }
    public bool IsMainParty { get; init; }
    public bool IsActiveForAdmission { get; init; }
    public bool IsAtFormalWar { get; init; }
    public int PartySizeLimit { get; init; }
    public int TargetCount { get; init; }
    public int PaymentLimit { get; init; }
    public int CurrentMemberCount { get; init; } // 新建默认 1，含领主
    public TroopRoster ProjectedRoster { get; init; } // 本地拷贝/虚拟对象
    public RecruitmentBudget Budget { get; init; } // 财务快照，不是 live 可变余额
}

public sealed class PlannedVillageWithdrawal
{
    public Settlement Village { get; init; }
    public CharacterObject Troop { get; init; }
    public int Count { get; init; }             // AI 封邑兵：仅健康兵
    public int WoundedCount { get; init; }      // 应为 0
    public int HearthCost { get; init; }        // 本次 = Count
}

public sealed class InitialMobilizationPreview
{
    public string AttemptId { get; init; }
    public Hero Leader { get; init; }
    public Clan Clan { get; init; }
    public Settlement BaseSettlement { get; init; }
    public int RequiredRegulars { get; init; }
    public int TargetCount { get; init; }
    public int ApprovedFiefCount { get; init; }
    public int ApprovedGarrisonCount { get; init; }
    public int ApprovedTotal => ApprovedFiefCount + ApprovedGarrisonCount;
    public IReadOnlyList<PlannedVillageWithdrawal> FiefByVillage { get; init; }
    public TroopExchangeManifest GarrisonManifest { get; init; }
    public CreationSourceSnapshot SourceSnapshot { get; init; }
    public RecruitmentFinancialSnapshot FinancialSnapshot { get; init; }
    public string RejectReason { get; init; }
    public bool Approved => RejectReason == null && ApprovedTotal >= RequiredRegulars;
}

public interface IInitialPartyMobilizationPlanner
{
    InitialMobilizationPreview DryRun(
        Hero leader, Clan clan, Settlement settlement,
        PendingPartyCreation creationReservation,
        string attemptId, int requiredRegulars);
}

public interface IInitialPartyMobilizationCommitter
{
    InitialMobilizationCommitResult Commit(
        MobileParty party,
        PendingPartyCreation reservation,
        InitialMobilizationPreview preview);
}
```

**设计规则**：

1. `RecruitmentSubject.FromLiveParty()` 与 `RecruitmentSubject.ForPlannedLordParty()` 负责读 Party 或预演数据；均调用**相同** `DefaultAiTroopRecruitmentModel` 内部核心函数。现有 `BuildPlan(MobileParty, ...)` / `EvaluateRecruitment(MobileParty, ...)` override 继续保留兼容。
2. `RecruitmentBudget` 与 `RecruitmentSimulationState` 的构造器扩展为以 `RecruitmentSubject` 或只读财务上下文初始化。`state.Commit(...)` **只修改本地模拟数据**，同一个 `state` 顺序经过封邑和驻军，防止同一工资空间被重复批准。
3. `InitialMobilizationPreview` 保存实际兵种数量、封邑来源村庄、驻军伤兵数量、名册指纹、村庄 Hearth、Clan 预算余额/版本、编制/工资上限、创建授权与规则版本。预演结果不要被误认为真实可提交的 `PartyExchangePlan`。
4. `CreationSourceSnapshot` 以**真实资源对象**为粒度保存状态，包括各村庄就绪名册 `{troop,count,wounded}`、`floor(Hearth)`、`RecoveryRequired`、服役占用、真实驻军名册、保护兵数、驻军安全下限、城堡围攻和 OwnerClan。一个普通的总数 fingerprint 不足以验证这些约束。
5. 整体预演的计划清单必须具有**稳定排序**（Settlements、Village.StringId、Troop.StringId 等）以确保相同输入得出相同结果。
6. 不要在核心 Model 中调用 `Guid.NewGuid()`、`TryConsume`、`CommitRecruitment`、`TryCommitCreation`、`ReconcilePartyFiefTroops`、`EnterSettlementAction`、`AddToCounts`（真实名册）、`Record` 等有状态修改的函数。

## 4. 预演从创建前状态构建 RecruitmentSubject

在 `PartyManagement/Services/` 增加 `PlannedLordPartySubjectFactory`（或等价 Factory）：

```csharp
public RecruitmentSubject Create(Hero leader, Clan clan, Settlement site)
{
    // 基本合法性/绑定预留已在外层检查。
    int assumed = Campaign.Current.Models.PartySizeLimitModel
        .GetAssumedPartySizeForLordParty(leader, clan.MapFaction, clan);
    int paymentLimit = MobilePartyWageLimitCalculator
        .CalculateForLeader(clan, leader, isLordParty: true);
    // 新建时 1 名 Hero 占位，普通士兵 0。
    // TargetCount 由 PartyForceTargetModel 的共享核心算出。
    // Budget 从 ClanRecruitmentBudgetManager.GetSnapshot(clan)
    // 读取“当前剩余”并复制，不对原对象写回。
    ...
}
```

**重要**：上述签名和 `CalculateForLeader` 是**拟新增接口**，不是现有 API。不能直接编译粘贴，需要 Codex 落实。

- 编制：优先原版 `GetAssumedPartySizeForLordParty`；实际出生后比对 `party.Party.PartySizeLimit`，任何偏差都应记录，且必须重新运行实时审批，不能强塞士兵。
- 初始占位：源于 `LordPartyComponent.InitializeLordPartyProperties` 将 Hero 加入名册；使用 `CurrentMemberCount=1`，`ProjectedRoster` 在实现时应与 Hero 计数一致。`TotalRegulars=0`，两者不能混淆。
- 战争：从 `ForceManagementFacade.IsAtFormalWar(clan)` / `PartyForceTargetModel` 现有 provider 得到；按同一 `GetWarTargetRatio` 与当前 XML 计算 `TargetCount`。
- 工资：扩展 `MobilePartyWageLimitCalculator` 为纯核心 `Calculate(clan, leader, isAtFormalWar)`；现有 `Calculate(Clan, MobileParty)` 包装此方法。使用同样的领袖财产档位、Clan Leader 1.5 倍、战争工资系数。
- 新 Party 创建后**立即** `party.SetWagePaymentLimit(preview.PaymentLimit)`，保证与 `ClanFinancialEvaluationService.EvaluateWarPartyWageLimits` 同一算法，再比较实际读取的 `party.PaymentLimit`，若不一致先修正/重算。
- 单兵工资：抽取 `AiRecruitmentFinancialModel.EstimateUnitDailyWage` 的单位工资核心为 `(CharacterObject troop, bool formalWarLordParty)`，复用 `Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop)` 以及 `NewPartyWageModel` 正式战争倍率；已有 `MobileParty` 入口做兼容。
- 现有 Party 的长期工资仍经 `NewPartyWageModel.GetTotalWageWithoutFiefExemption(party, roster)`；新 Party 起始长期工资按含 1 名 Hero 的初始名册计算/验证（一般 Hero 不产生普通士兵工资）；**不能把原版关于 Party 技能、加成的工资路径换成不等价近似**。
- 财政：保留 `RecruitmentBudget.Create(MobileParty)`，另加 `Create(RecruitmentSubject)`；逐次模拟批准写到自己的 `RecruitmentSimulationState`。`ClanRecruitmentBudgetManager.GetSnapshot(clan)` 返回当天共享余额的实时对象，**只读其 RemainingCommitmentBudget/RemainingPurchaseBudget，并复制到模拟预算**，不能在 DryRun 调 `CommitRecruitment`。
- `RecruitmentBudget.GetAllocatedClanFunds()` 按当前 `clan.WarPartyComponents.Count` 分摊的辅助 API **不是当前 AI `DefaultAiTroopRecruitmentModel.BuildPlan` 的直接审批入口**；仅当所重构路径实际用到它（例如其它 `GetAvailableAdditionalDailyWage` 分支）时才需把未来新 Party 计入分摊，不能凭假设改动所有财政规则。

## 5. 抽取真正共用的审批内核

### 5.1 `DefaultAIRecruitmentModel.cs`

现有：`BuildPlan(MobileParty, candidates)` 和 `EvaluateRecruitment(MobileParty, troop, available, source, state)`。

施工：

```csharp
// 新增纯内核；被旧 MobileParty API 和新建军 DryRun 共用。
internal RecruitmentPlan BuildPlanCore(
    RecruitmentSubject subject,
    IReadOnlyList<RecruitmentCandidate> candidates,
    RecruitmentSimulationState state,
    int requestLimit);

internal RecruitmentEvaluationResult EvaluateRecruitmentCore(
    RecruitmentSubject subject,
    CharacterObject troop,
    int availableCount,
    RecruitmentSource source,
    RecruitmentSimulationState state);

// 原有 override 保留：
public override RecruitmentPlan BuildPlan(MobileParty p, IReadOnlyList<RecruitmentCandidate> cs)
    => BuildPlanCore(RecruitmentSubject.FromLive(p), cs,
        RecruitmentSimulationState.Create(p), int.MaxValue);
```

- 先保留原有候选排序：`GetPriority` → Tier → source；不要顺手调整部队质量组成。
- 用 `subject.IsActiveForAdmission` 替代 `party.IsActive`，并把 `RecruitmentTroopEligibilityPolicy.ShouldExcludeBanditForAiRecruitment` 扩展为共享的 `subject` 判断；旧入口必须同结果。
- `GetRecruitmentTarget` 共享 `PartyForceTargetModel` 内核的 `TargetCount`；`RecruitmentAdmission.Evaluate` 保持原样，输入 `HardLimit`、`EffectiveCurrentCount`、`TargetCount`。
- `GetAllowedByWageLimit` 的 90% 上限、`GetAllowedByRecruitmentCost`、`GetAllowedByMaintenance` 原样保留；`Fief`/`Transfer` 成本特例原样保留。
- 保留 `PrimaryLimit` 等审批诊断；每次候选批准后只 `state.Commit(...)`。跨兵源持有**同一个模拟 state**，防止第二来源重新使用已批准的 Party 空位和工资空间。
- 普通 `MobileParty` 招募路径写等价性测试：给定同一个真实 Party、候选、预算快照，重构前后批准兵种/人数、PrimaryLimit 完全相同。

### 5.2 `RecruitmentSimulationState.cs` / `RecruitmentBudget.cs`

- 新增 `Create(RecruitmentSubject)`，不要修改现有 `Create(MobileParty)` 对老场景的语义。
- 需要区分 `ProjectedMemberCount`（包含 Hero）与 `ProjectedRegularCount`（不包含 Hero），`EffectiveProjectedMemberCount` 扣留驻军外出编制的行为仅适用于驻军，创建的领主 Party 不应继承驻军的 `ReservedReturningMembers`。
- 计划封邑 35 + 驻军 15 时第二次模拟的 `ProjectedMemberCount` 已包含前 35 名，`ProjectedDailyWage` 已包含它们的长期工资；不能重新 `Create()`。
- 每次 `BuildPlanCore` 使用复制的 `RecruitmentBudget`，模拟版绝对不写实时 `ClanRecruitmentBudgetSnapshot.Remaining*`。

### 5.3 `PartyForceTargetModel` 与 `ForceManagementFacade`

- 在 `ForceManagement/Contracts/ForceManagementTypes.cs` 新建纯 `PartyForceEvaluationInput { Clan, PartyLimit, CurrentCount, IsOwnFiefDefense, IsActive, IsInMapEvent, IsStarving, IsInArmy, ... }`。
- `PartyForceTargetModel.Evaluate(PartyForceContext)` 先把真实 Party 转换为输入，再调用 `EvaluateCore(input)`；建军 Factory 同样调用 `EvaluateCore`。
- 当前 `CanStartRecruitmentVisit` 包含 `party.IsActive`、`MapEvent`、`Army`、饥饿等运行条件；**创建前只需要 `TargetCount/GapCount`**，不能把没有真实 Party 的状态强行判为“已访问可招募”。
- 维持少数派、和平最低人数、战争意愿区间和自有封邑防御参数的当前逻辑。

## 6. 驻军来源：提取纯计划，不复制兵种算法

生产代码入口：`PartyExchangeModel.BuildRosterPlan(..., fieldToGarrison:false)` 和 `FillSoftGarrisonWithdrawalManifest`。

- 增加 `BuildGarrisonWithdrawalPreview(RecruitmentSubject subject, Settlement site, RecruitmentSimulationState sharedState, int maximum)`，返回带健康/受伤人数的 `TroopExchangeManifest`。
- **真实来源**必须仍是 `site.Town.GarrisonParty`，且 `site.OwnerClan == subject.Clan`、无围城、无冲突；新 Party 权限依据有效 `PendingPartyCreation`，不要构造一个 `IsActive=true` 的虚假 Party 来绕过 `PartyTransferOwnershipPolicy`。
- 仍使用 `GarrisonDefense.Evaluate(site)`、`GetSafeTransferOutCount(defense, garrison.MemberRoster)`，并排除 `FiefPartyManager.GetServingTroopCounts(garrison)`；不要硬编码“驻军留 75”覆盖现有防御模型。
- 复用现有 `FillSoftGarrisonWithdrawalManifest` 中 `CountRoles / GetWithdrawalScore / AddAmount` 的兵种选择和 `RecruitmentModelManager.Model.EvaluateRecruitment`；把目标的 `MobileParty` 与目标 `TroopRoster` 分离为 `RecruitmentSubject + ProjectedRoster + sharedState`。源驻军依然读取真实对象。
- 驻军可调兵包含原代码允许的伤兵（`AddAmount` 为 `WoundedCount` 计算值），不能擅自改成只转移健康兵；`ManifestAvailable` 最终仍要检查真实健康/受伤/保护人数。
- Garrison 的全局驻军安全下限和 `PartyTransferOwnershipPolicy` 现有执行检查**不能被预演的授权替代**，提交前照常检查。
- 新旧计算应共享核心：现有 `BuildRosterPlan` 只是适配器；不能留下两套独立评分和选择算法。

## 7. 封邑兵来源：按村庄精确分配 + Hearth 预演

生产代码：`FiefPartyManager.TryResolveOperationScope`、`FiefPartyRecruitment.PreviewFiefRecruitmentCandidates / PreviewFiefRecruitmentApprovals / TryCommitVillageRecruitment`、`VillageFiefData.TryMobilize`。

**必须做到两层复用**：

1. **兵种审批复用**：沿用 `BuildTemplateWeightedRecruitmentCandidates` 的权重根、升级树、健康人数、可招兵种及 `MinimumFiefRecruitBatch`；接入 `RecruitmentSubject` 和共享 `RecruitmentSimulationState`。
2. **来源村庄复用**：从 `TryCommitVillageRecruitment` 内部抽取纯 `BuildVillageAllocationPlan(approvedTroops, villageFilter, healthyOnly, limits)`，统一处理 `OrderedVillages`、健康名册、村庄 RecoveryRequired、Hearth 与计划上限。该纯方法不得修改名册/Hearth/服役批次。

具体实现建议：

- 用 `FiefPartyManager` 新增 `EvaluatePlannedCreationScope(site, leader, clan, reservation)`，共享 `TryResolveOperationScope` 的合法性谓词，允许持有匹配的 `PendingPartyCreation` 通过，但**不得放松** OwnerClan、围城、村庄归属、已服役 Party 冲突等检查。现有 Party 仍走 `EvaluateFiefSourceAccess`。
- 在**按村庄分配**时先计算 `maxHearth = floor(village.Hearth)`，减去本次预演中已为该村庄计划的扣除量；调用 `SettlementPopulationServiceRegistry.Current.EvaluateConsumption(villageSettlement, plannedVillageCount)` 做只读复核。`EvaluateConsumption` 当前已实现，不用扩展 `ModifiedPolitics` 公共合同。
- 不能简单用“封邑总 Hearth ≥ 合计人数”，必须**逐村庄**匹配所抽调兵种和 Hearth。若 A 村 50 士兵/10 Hearth、B 村 40 士兵/50 Hearth，不能预演从 A 村拿 35 人。允许按已有 `OrderedVillages` 顺序从 B 村补足，但不能无来源凭空调兵。
- 封邑兵本次消耗应与 `TryMobilize(count)` 一致，**1 人 = 1 Hearth；无额外最低 Hearth 保留线**。不要误用文化 FiefWeight、`CanIncreaseSupply` 的生产/供养占用额度作为征召消费条件。
- 输出 `PlannedVillageWithdrawal`（村庄 + Troop + Count + Wounded=0 + HearthCost），并保存每个真实就绪名册 / Hearth 快照、模板 ID/权重或稳定规则 fingerprint。
- **必须改提交入口**：提供接收 `IReadOnlyList<PlannedVillageWithdrawal>` 的内部重载（可用内部类型名称）以执行**指定的村庄/兵种清单**，不能提交时仅使用 `{CharacterObject -> count}` 再随意从村庄重分配。调用前重读同一来源名册/Hearth/权限并验证；不符拒绝整笔事务。
- 原有 `RecruitFiefTroopsFromSettlement` 及普通 Party `PreviewFiefRecruitmentApprovals` 对外签名保留；可以让旧入口调用新纯 `BuildVillageAllocationPlan` 来保证规则一致。
- 原有 `TryCommitVillageRecruitment` 的快照、`RemoveReadyTroops`、`TryMobilize`、`CreateServingDetachment`、`RegisterServiceBatch`、`AddPartyTroops`、`ValidateCommit`、Rollback/RequireRecovery 顺序保留；提取计划构建，**不得重新写一套脆弱事务**。
- 模拟批准低于 `MinimumFiefRecruitBatch` 时弃用整笔封邑选项，**不要把这 24 人写入共享模拟状态**。实现上可用 state clone / checkpoint，只在整批合法且 Hearth 可满足时 commit 模拟。
- Hearth 不满足时：按村庄合法供给重新计算可行批次；实在不足最低批次则封邑批准数为0，可继续使用驻军，不要把其混成“已有 25 可调用”。

## 8. 双兵源组合算法（只用于初始建军）

### 8.1 固定优先级和共享模拟状态

```
TryReserveForCreation(...)
  -> PlannedLordPartySubjectFactory.Create(...)
  -> shared SimulationState (contains hero 1; 0 regulars)
  -> FiefCandidate + TemplateWeight + FiefBudget + village/Hearth preview
  -> IF (fief count >= MinimumFiefRecruitBatch): accept fief;
     ELSE: restore simulation checkpoint and use fief=0
  -> GarrisonCandidate + safe floor + protected counts
  -> Garrison role-based withdrawal using the SAME simulated member/wage state
  -> Final per-source manifests, fingerprints, total wage, approved count
  -> IF total regulars < 50: Reject, release creation reservation
```

- 如果封邑兵候选充足但审批只有 22，不能为了满足 50 绕过最低批次；驻军仍可单独达到 50。
- 如果封邑批次 35 + 驻军批准 15 → 共 50，通过。
- 如果封邑兵 35 + 驻军 10 → 共45，拒绝创建，即使初始 `EstimatedCombinedRegulars` 为 200。
- 如果封邑兵单独 60，预算/编制目标仅批50 → 封邑50，无驻军。
- 不允许第一兵源预演消耗所有工资额度后，第二兵源从一个新的 `RecruitmentSimulationState.Create` 重新“批准”同一额度。
- 对于 `Transfer`，按当前实现保留其**跳过采购与长期维持资金审批**的策略，但它的工资仍写入模拟 `ProjectedDailyWage`，之后封邑兵不能忽略这一工资。反方向（先模拟封邑）同理。
- `InitialMobilizationPreview.ApprovedTotal` 必须等于其 `FiefByVillage` + `GarrisonManifest` 逐项求和，且不超过 `TargetCount - 0`、编制减 1 Hero 的空位；校验任何重复兵种只按实际来源分别记账。

### 8.2 为什么不能把两个 PartyExchangePlan 简单相加

原 `PartyExchangePlan` 的 `Destination` 需要真实 `MobileParty`；每份计划都持有独立目标名册快照和财务模拟状态。两个各自成功的 Plan **不保证**在同一个新 Party 中合起来成立；后一个 Plan 的目标 fingerprint、目标人数和工资必须包含前一个计划的模拟效果。因此使用单个 `InitialMobilizationPreview` 作组合外壳，来源保持各自独立事务清单。

## 9. 创建/提交时序：从“到城事件招兵”改为直接同步初始化

**推荐真实运行顺序**（保持 `HeroSpawnSettlementPatch` 的外层拦截不变）：

```
HeroSpawnSettlementPatch.CreateAtBoundClanBase(hero,...)
  └─ PartyCreationCoordinator.TryCreateManagedLordParty
       1. validate leader/clan/managed policy/campaign recovery
       2. enumerate legal clan bases (cheap checks only)
       3. for each legal base:
            TryReserveForCreation(hero, clan, site) -> token
            InitialMobilizationPlanner.DryRun(hero, clan, site, token)
            if rejected: CancelCreationReservation(token), next site
       4. select best approved preview
          (ApprovedTotal desc, distance asc, site StringId asc)
          IMPORTANT: release every rejected or non-selected reservation!
          either retain only the best token, or release all temporary tokens
          and re-acquire/re-preview the selected site before Spawn
       5. revalidate selected valid token + preview source snapshots
          if changed: re-DryRun once; if <50 release token and return null
       6. ChangeGovernorAction.RemoveGovernorOf(leader) [only NOW, after DryRun]
       7. using PartyCreationScope.Begin(leader):
              SpawnLordParty(leader, site)
          -> expect hero=1, regulars=0
       8. enforce actual PartySizeLimit, CurrentMemberCount, PaymentLimit parity
          party.SetWagePaymentLimit(preview.PaymentLimit)
          IF mismatched: rebuild real-state approval; no resource writes until accepted
       9. bindings.TryCommitCreation(token, party)
      10. using CreationInitializationGuard.Begin(party, attemptId):
              EnterSettlementAction.ApplyForParty(party, site)
              // guard suppresses normal visit-task execution for this one entry
      11. InitialPartyMobilizationCommitter.Commit(party, token, preview)
          11a. revalidate all snapshots, auth, budget, Hearth; ensure min50
          11b. commit garrison first (easier exact compensation)
          11c. commit fief second using approved per-village manifest
          11d. verify final 50+, accounting, journal, bindings
      12. commit result:
          Committed       -> finished, party enters normal AI pipeline
          Rejected/RolledBack and fully restored -> safe failed-creation cleanup
          RecoveryRequired/PartiallyCommitted   -> retain party, binding,
                                                   recovery marker; never destroy
```

### 9.1 初始入城事件的重入保护

`PartyExchangeBehavior.OnSettlementEntered` 目前会立即 `AiForcePlanningBehavior.ExecuteAtSettlement`，并提前写入“本日已评估”字典。如果直接在初始化时使用 `EnterSettlementAction.ApplyForParty`，可能出现**事件先于建军提交跑完任务**或当日后续任务被误判重复。

建议在 `PartyManagement/Models/PartyCreationScope.cs` 的相邻位置增加**独立** `PartyCreationInitializationScope`（`[ThreadStatic]` 或同一线程同步栈，按 `party.StringId + attemptId`），仅用于阻止这一特定 Party 的 `PartyExchangeBehavior.EvaluateVisit` 处理，其他 Party 不受影响。**不要粗暴关闭整个行为、清空所有 `_lastEvaluationDay` 或把初始化提交塞进正常 Task。**

`PartyExchangeBehavior.EvaluateVisit` 开头（在创建访问去重登记前）增加：

```csharp
if (PartyCreationInitializationScope.IsInitializing(party))
{
    // 初始兵力由 PartyCreationCoordinator 直接 Commit；本事件只做进入城镇。
    return;
}
```

Guard 最终在 `finally/using` 中解除。之后正常访问/驻城事件仍由现有行为负责，不重复执行刚刚的建军清单；必要时以创建事务 journal 的 operationId 防重。

### 9.2 为什么创建后仍要复核

没有“永久锁定所有士兵/村庄 Hearth/Clan 财政”的机制；单线程内的其他事件/回调也可能改变状态。`DryRun` 是**真实审批结果的快照**而不是未来的成功承诺。将创建后失败降为异常分支，并通过详细日志说明具体哪项属性变化。不得在创建后把不匹配的原计划硬提交。

### 9.3 非法清理与总督行为

原 `PartyCreationCoordinator` 的 `catch` 在 `party.IsActive` 时直接 `CancelTask`、`ReleasePartyBindings`、`DestroyPartyAction`；新代码必须改成**依据 Commit 状态/补偿验证结果**分支。若存在任何可能已扣 Hearth/调兵/服役批次的阶段，不能仅因为 Catch 异常就销毁 Party。

`ChangeGovernorAction.RemoveGovernorOf(leader)` 只有预演成功才允许发生；如果后续创建失败，应检查原有总督职位能否安全恢复，若不属于本期实现，至少避免前置失败造成无意义总督撤任，并记录异常。不要在未核对原版任命条件时简单强行恢复总督。

## 10. 真实提交与跨来源补偿

### 10.1 提交策略：计划上“封邑优先”，资源提交上“驻军优先”

**预演决策**先给封邑兵分配优先配额，驻军补缺；**真正写入**建议先提交驻军、后提交封邑兵。原因：驻军转移本质是两端名册的可逆迁移；封邑兵则同时涉及村庄 Hearth、服役批次、人口事务，很难在跨来源失败时在外层准确地“反向招募”。

推荐流程：

1. 在任何写入前构建**完整、实际可接受的双来源清单**，验证总数 `>= 50`、工资、编制、村庄 Hearth、驻军安全限制、Clan 预算。模拟顺序可以封邑优先，但要验证正式提交倒序不会导致不同的批准集合。
2. 为本次建军建立 `AttemptId`，两个子事务使用不冲突的稳定 operationId，例如 `party-create:{attemptId}:garrison`、`party-create:{attemptId}:fief`；不要沿用可能重复的旧 `force:{party}:{day}:...`。
3. 第一步：执行**经过真实目标 Party 验证的**驻军转移 `PartyExchangeService.Execute`；如果失败且已完整回滚，安全终止；若 `RecoveryRequired`，保留 Party。
4. 第二步：执行**按村庄精确约束**的封邑兵征召；重验此时真实目标人数、工资/余额及村庄条件。务必保证“先有驻军士兵后，封邑审批仍与组合预演相同”。若不相同则不消耗 Hearth，走驻军补偿。
5. 第三步：核对 `MemberRoster.TotalRegulars`、驻军减少量、村庄就绪人数、Hearth 减少量、注册的 serving batches、Clan 已承诺预算、journal 中子 operationId。
6. 全部成功后才标记建军完成。提交成功之后的日志/通知异常**不能触发销毁已建好的部队**。

**说明**：这是需要 Codex 实作/验证的新跨来源协调器，并非现有 `PartyExchangeService` 已具有的跨来源原子保证。

### 10.2 驻军补偿必须是专用受保护的逆操作

如果驻军提交已成功而后续封邑征召 `Rejected` 或 `RolledBack`，需要把原驻军清单原样从新 Party 退回原驻军，并验证：

- 新 Party 当前对应兵种/伤兵仍等于先前转入数量（没有被其它行为挪走）。
- 原驻军仍是相同实例、Clan/Settlement/容量仍合法。
- 补偿后两个名册的**完整指纹**恢复到驻军提交前，且没有新增 fief batch/Hearth 消费。
- 两个子事务的日志/journal 都有明确对应关系；**已成功的 operationId 不得重复执行**。

**不要用普通 `FieldToGarrison` 任务直接充当逆操作**：其 `GetSafeFieldSurplus`、正常驻军空位/防御规则可能阻止一个刚建立、人数很少的新 Party 退回这批士兵。应在 `PartyExchangeService` 的受保护内部接口增加 `CompensateCreationGarrisonWithdrawal`（或专用 `CreationMobilizationRollbackService`），只接受本次已提交的原始清单和 `AttemptId`，而不是公用的任意士兵回退 API。

如果补偿中途失败或指纹不一致：返回 `RecoveryRequired`，把对应域冻结，不能删 Party；旧 `TransferOperationJournalBehavior` 的 `RecoveryRequired` 记录必须得到保留。

### 10.3 封邑单笔失败处理

`FiefPartyRecruitment.TryCommitVillageRecruitment` 本身已有：

- `RemoveReadyTroops` / `MobilizeHearth` / `CreateServiceBatch` / `RegisterServiceBatch` / `AddPartyTroops`
- 失败时尝试逐名册、服务批次、人口事务补偿，失败则村庄 `RequireRecovery`。

新的协调器不能认为 `returned 0` 就等于**完全没有发生变化**；必须从详细结果/回滚验证结果确认安全。建议将封邑提交 API 的结果从单纯 `int/bool` 扩展为带 `CommitStatus`（`Rejected`、`RolledBack`、`Committed`、`RecoveryRequired`）与 `FailureStage` 的结果，保留旧方法包装，避免破坏 UI/其他来源调用。

**严禁**在异常时把已经入伍的封邑兵用 `MemberRoster.Clear()` 清掉来“回滚”；会导致 Hearth 和 serving batch 与真实士兵不一致。

### 10.4 可接受的最小保证与后续改进

若第一期确实无法保证双来源的跨事务补偿安全，应分阶段交付：

- 先实现**新建 Party 前双来源只读预演**、单来源执行和预检拦截；但**禁止**宣称双来源完整上线。
- 后续完成并通过故障注入测试后，才接通双来源真实提交。
- 不接受“失败时保留 1~49 士兵 Party 并视为创建成功”的无声明降级，因为会重新引入本次要解决的行为偏差。

## 11. 与现有 Task / 调兵事务 API 的兼容

1. `PartyForceTaskCoordinator.TryRegisterInitialRecruitTask` 是**一个任务、一个来源**；组合建军不应该伪装成两个靠入城触发的普通 Task。建议把“初始建军”从该方法解绑，仅保留其对旧路径/非创建补员的兼容；`AiForcePlanningBehavior` 继续管理出生后的普通行动/访问。
2. `PartyVisitExecutionService.BuildOperationId()` 当前已采用 `force-task:{TaskId}:{Intent}:{Source}:{Settlement}`（兼容旧 `UsesLegacyOperationId`）；新的创建 operationId 使用独立命名空间，不与补员 Task 混用。
3. 保留 `PartyExchangeService.DryRun(PartyExchangePlan)` 现有**“仅验证已成计划”**的 API（如需重命名可新增 `ValidateApprovedPlan` 并包装旧签名）；增加的建军 DryRun 是**从 Hero/Clan/Settlement 创建候选 → 构建 manifest → 校验所有资源**的更高层 API，不要把两者混为一谈。
4. 正常 `UnifiedRecruitmentExecutionService.Execute(MobileParty, ...)` 继续使用现有 Party 输入，但其内核与新预演复用；旧城镇到访继续通过 `PartyExchangeBehavior`。
5. 不修改 `FiefPartyManager.ReconcilePartyFiefTroops` 对普通真实 Party 的需要；只是不允许**纯预演**调用 `PrepareContext` 从而触发此方法。创建后的正式事务可以在适当阶段用原有恢复/同步机制。
6. `TransferOperationJournalBehavior` 已有持久化 completed/recovery；创建协调器的子操作必须沿用并扩展可诊断性。若需要新增跨来源恢复元数据，使用**独立的、最小化 save key**，不得改坏既有 journal 保存格式或 legacy operationId 读取。
7. `CampaignRecoveryCoordinator.CanMutateCampaignState()` 为实际创建/提交的硬门槛；新建军预演也应拒绝在未恢复完成的战役状态中作“可创建”正向决策，不能因为只读而绕开后续必然失败的恢复屏障。

## 12. 逐文件施工清单（严格按优先级）

| 优先级 | 文件（均相对 `ModifiedArmy/ModifiedArmy/`） | Codex 修改目标 |
|---|---|---|
| P0 | `Recruitment/Models/DefaultAIRecruitmentModel.cs` | 提取单一 `BuildPlanCore/EvaluateRecruitmentCore`，原 MobileParty 接口适配 |
| P0 | `Recruitment/Models/RecruitmentSimulationState.cs` | 构造预演 state、clone/checkpoint、累计跨来源工资和人数 |
| P0 | `Recruitment/Models/RecruitmentBudget.cs` | 只读 Clan 财政快照到预演预算、无 live Remaining 写入 |
| P0 | `ForceManagement/Models/ForceManagementModels.cs` | `PartyForceTargetModel` 使用可由新建上下文构造的纯输入 |
| P0 | `ForceManagement/Contracts/ForceManagementTypes.cs` | 新增目标兵力纯输入，不破坏旧 `PartyForceContext` |
| P0 | `PartyManagement/Finance/Models/MobilePartyWageLimitCalculator.cs` | 抽取 `Clan+Hero+正式战争` 版本，旧接口包装 |
| P0 | `PartyManagement/Finance/Models/AiRecruitmentFinancialModel.cs` | 单兵长期工资成本抽取为纯上下文计算，保留旧入口 |
| P0 | `Recruitment/Models/RecruitmentTroopEligibilityPolicy.cs` | 兵种合法性共享 context 判定 |
| P1 | **新文件** `ForceManagement/Contracts/InitialMobilizationContracts.cs` | 预演上下文、按村庄明细、指纹、综合结果、提交状态 |
| P1 | **新文件** `PartyManagement/Services/PlannedLordPartySubjectFactory.cs` | 从 Hero/Clan/Settlement 建预演 Party 属性（hero 占 1） |
| P1 | `Fief/Models/PartyData/FiefPartyRecruitment.cs` | 抽取纯村庄分配/健康兵/模板/Hearth 内核，Commit 接受相同具体村庄清单 |
| P1 | `Fief/Models/FiefPartyManager.cs` | 创建预留授权 Scope；增加 Preview/Commit 的组合入口，保留旧方法 |
| P1 | `Fief/Models/VillageFiefData.cs` | 新增只读可动员人数/消耗校验，复用现有 `EvaluateConsumption` 和原 `TryMobilize` |
| P1 | `PartyManagement/Bindings/PartySettlementBindingBehavior.cs` | 匹配预留 token 的只读创建授权，不放松旧绑定规则 |
| P1 | `ForceManagement/Models/PartyExchangeModel.cs` | 现有软驻军选兵内核改为 Subject+shared state；旧 BuildRosterPlan 适配 |
| P1 | **新文件** `PartyManagement/Services/InitialPartyMobilizationPlanner.cs` | 串联两种来源，返回真正的双来源 manifest |
| P2 | `ForceManagement/Services/PartyExchangeService.cs` | 保留现有 Execute/DryRun；增加受保护的建军驻军补偿/真实清单复核入口 |
| P2 | **新文件** `PartyManagement/Services/InitialPartyMobilizationCommitter.cs` | 组合提交、复核、补偿、RecoveryRequired、最终一致性检查 |
| P2 | **新文件** `PartyManagement/Models/PartyCreationInitializationScope.cs` | 防入城事件重复/抢先执行初始补员 |
| P2 | `ForceManagement/Behaviors/PartyExchangeBehavior.cs` | 入城事件创建初始化期间短路，避免日去重污染 |
| P2 | `PartyManagement/Models/PartyCreationSettlementModel.cs` | 从“估算兵力排序”改为“真实预演批准人数排序”；保留 cheap filter |
| P2 | `PartyManagement/Services/PartyCreationCoordinator.cs` | 预留→DryRun→spawn→应用工资→直接组合 Commit→状态驱动清理 |
| P2 | `ForceManagement/Behaviors/TransferOperationJournalBehavior.cs` | 若需要，增补跨来源恢复日志/子事务关联；不破坏旧 key |
| P2 | `ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs`、`PartyManagement/Bindings/Diagnostics/*` | 新预演/提交分阶段诊断字段 |
| P3 | `ForceManagement/Missions/PartyForceTaskCoordinator.cs`、`ForceManagement/Behaviors/AiForcePlanningBehavior.cs` | 让新建军不再依赖旧单来源初始 Task；保留普通补员任务 |
| 不变 | `Recruitment/Patches/HeroSpawnSettlementPatch.cs` | 原 Harmony 入口应保留，无需另增拦截 |
| 不变 | `PartyManagement/Models/NewPartySizeLimitModel.cs`、`PartyCreationScope.cs` | 保留 Scope 空初始普通兵名册；如有必要仅加诊断 |
| 不变 | `ModifiedPolitics/.../SettlementPopulationBehavior.cs` | 已有 `EvaluateConsumption`，正常情况下无须改跨模组接口 |

**说明**：表中 `InitialMobilization*` / `PlannedLordPartySubjectFactory` 等为**建议新增文件名**，不是仓库当前已存在的类型；Codex 施工可合并过细的数据类，但不能丢失职责和验收条件。

## 13. Codex 最优分批实施顺序

### 阶段 A：只读基础设施及兼容重构

1. 从旧 API 提取 `RecruitmentSubject`、`RecruitmentSimulationState.Create(subject)`、`RecruitmentBudget.Create(subject)`，确保**现有真实 Party 的 `BuildPlan` 结果不变**。
2. 提取 `PartyForceTargetModel.EvaluateCore` 与 `MobilePartyWageLimitCalculator` / 单兵工资共享核心。
3. 实作 `PlannedLordPartySubjectFactory`：原版预计编制、英雄占位、预计自定义工资上限、战争情况、Clan 预算。不能创建 Party。
4. 增加基础等价性测试。此阶段**不修改 `PartyCreationCoordinator` 生产行为**。

交付门槛：正常 AI Party / 玩家驻军与原版 Mod 招募输出没有偏差；全量编译成功。

### 阶段 B：两个来源的精确 DryRun

1. `FiefPartyRecruitment` 抽取逐村庄只读分配、Hearth `EvaluateConsumption` 及模板健康兵规则。
2. `PartyExchangeModel` 以同一 `RecruitmentSubject + SimulationState` 做驻军选兵。
3. `InitialPartyMobilizationPlanner.DryRun`：封邑优先 + 驻军补足 + 共用状态 + 50 人建军门槛 + 各类拒绝原因。
4. 用运行前后快照确认 **DryRun 没有写入**：目标/驻军/村庄名册、Village.Hearth、Clan 共享预算余额、服役批次、绑定和 journal。
5. 做单源 / 双源 / 无源等价性及边界测试。此阶段只可在诊断接口中调用 DryRun，**不允许已上线的创建流程消费尚未验证的预演计划**。

交付门槛：重复 DryRun 对同一状态生成完全相同的清单及 fingerprint，产生相同理由码，资源快照完全不变。

### 阶段 C：组合提交与故障补偿

1. 增加将预演清单绑定到真实 Party 的 adapter；真实 `PartySizeLimit/PaymentLimit/MemberRoster` 不同则拒绝或重新审批。
2. 按同一村庄分配清单完成封邑提交重载，保留现有事务实现和回滚。
3. 驻军提交使用现有 `PartyExchangeService.Execute`；增加严格限定的建军补偿逆操作。
4. `InitialPartyMobilizationCommitter` 串联双源并完成失败补偿、journal、RecoveryRequired 状态。
5. 故障注入测试：驻军成功/封邑失败、Hearth 变化、村庄 RecoveryRequired、驻军名册变化、角色/拥有权变化、补偿失败。

交付门槛：**无任何兵源丢失、重复、Hearth 多扣、服役批次孤儿**；不能提供该保障时应保留 RecoveryRequired，而不是删 Party。

### 阶段 D：接入创建入口

1. `PartyCreationSettlementModel` 改为 cheap candidates；对候选地点先临时预留、逐个 DryRun，按**真实批准人数**选择最佳基地。
2. `PartyCreationCoordinator` 将 DryRun 放到 `SpawnLordParty` **之前**；预演失败时不能调用 `Spawn`，不能提前移除 Governor。
3. 创建后立刻 `SetWagePaymentLimit`，重新校验实际属性、入城初始化事件 guard、同步调用 `InitialPartyMobilizationCommitter.Commit`。
4. 取消“初始 Task 一次只取一来源 + 首次入城事件驱动 + 0 人驻军 fallback”的建军路径；正常补员 Task 机制继续保留。
5. 异常清理由 Commit 状态决定，禁止无条件销毁可能已消费资源的 Party。
6. 比对 `HeroSpawn` 原版入口、`ManagedPartyPolicy.IsEnabled` 关闭情况、少数派/玩家/商队、读档任务恢复。

交付门槛：实际新建 AI Party 要么不生成，要么初始满足 `>=50` 普通兵；异常时进入可诊断/可恢复状态，不能静默制造空 Party。

### 阶段 E：诊断与历史兼容

1. 保留旧日志的重要字段；新增 `attemptId`、基地、`previewCount/committedCount`、`previewFief/previewGarrison`、逐村庄 Hearth、工资限额/投影工资、拒绝原因、快照差异与恢复状态。
2. 验证既有存档不因新增非持久临时类型读档崩溃；若新增持久化数据，注册适当 SaveableType/SaveableField，避免把原有 data key 改名。
3. 清理旧建军 fallback 死逻辑、重复 estimated checks，仅在功能等价后删掉。

## 14. 验收矩阵（必须记录实际结果）

| ID | 测试场景 | 预期 DryRun / Commit |
|---|---|---|
| T01 | 封邑兵健康且模板有效≥50、Hearth/工资充足 | 封邑兵成功，驻军0；初始 Party≥50 |
| T02 | 封邑审批35 + 驻军安全可调15 | 预演50；Commit 35+15，2种来源各自扣正确资源 |
| T03 | 封邑可批准24，最小批次25；驻军可调25 | 封邑整批0；总25，**不创建 Party** |
| T04 | 封邑24，不合批次；驻军可调≥50 | 仅驻军通过；封邑 Hearth 不变 |
| T05 | 封邑就绪82，大多数为伤兵 | 只按健康兵审批，不能按82判定足够 |
| T06 | 封邑名册有效，但模板没有正权重根/兵种未匹配升级树 | 预演识别候选为0，不能生成假批准 |
| T07 | A村 50兵/10 Hearth、B村40兵/50 Hearth | 逐村庄受 Hearth 限制，绝不从 A 村调35 |
| T08 | 全封邑 Hearth 总量够，但实际兵种只存在缺 Hearth 村庄 | 本村庄 Hearth 限制生效，不能跨村庄凭空调兵 |
| T09 | 村庄处于 RecoveryRequired | 该村庄不作为可动员来源；记录原因 |
| T10 | 驻军总人数200，但安全驻守量和 serving 保护后只有10可调 | 只批准最多10（再受工资/目标约束） |
| T11 | Clan 无可用长期招募预算、封邑兵需负担后续工资 | 封邑审批受 MaintenanceFunds 限制；驻军仍按现有 Transfer 财政规则执行 |
| T12 | Party 预计编制51，Hero占1，目标招50 | 允许50；预计编制50则拒绝，不能超编 |
| T13 | Clan Leader 与普通 Clan 成员分别建军 | 工资上限计算体现 leader 倍率；预演与出生后 PaymentLimit 一致 |
| T14 | 战争/和平切换 | 战争工资倍率及目标人数按现有模型变化，不新增硬编码 |
| T15 | 连续 DryRun 100 次 | 无资源写入、无 journal 新记录、无 source roster 变化，清单确定性一致 |
| T16 | DryRun 后、Spawn 前驻军被抽兵或村庄 Hearth 下降 | Snapshot 不匹配则重审/拒绝；不提交旧清单 |
| T17 | Spawn 后实际 PartySizeLimit 或 PaymentLimit 与预演不同 | 重审/拒绝；不按照旧容量强制提交 |
| T18 | 驻军转移成功，封邑按村庄提交之前 Hearth 变化 | 封邑拒绝，驻军原样补偿；安全清理或 RecoveryRequired |
| T19 | 驻军补偿故障（注入异常） | 保留 Party+绑定+冻结日志，状态 RecoveryRequired；不可销毁 |
| T20 | 封邑兵 `TryCommitVillageRecruitment` 中途异常且回滚成功 | Hearth/就绪名册/服役批次/Party 名册逐项恢复；失败清理安全 |
| T21 | 封邑兵补偿失败 | 村庄 RequireRecovery、建军 RecoveryRequired；不可销毁 |
| T22 | 正常 Party 到城补员（非创建） | 与重构前相同来源选择、审批数、journal；不受到创建 Guard 影响 |
| T23 | 玩家主队/驻军、商队、村民、少数派/非受管 Party | 旧行为不改变 |
| T24 | 读取已有存档，第一次 Tick 即创建建军 | 恢复屏障有效；不生成不完整 Party / 不重复执行 operationId |
| T25 | 重复创建调用/重复入城事件/同日驻城 Tick | 不重复动员同一份封邑兵、不重复扣 Hearth/共享预算 |
| T26 | 先成功建军，随后日志/通知回调发生异常 | 不能因 Catch 误删已经成功提交兵力的 Party |
| T27 | 50 人普通兵 +1 Hero、含伤兵驻军转移 | `NumberOfAllMembers`、`TotalRegulars`、伤病人数、工资口径保持一致 |
| T28 | Clan 无领地或各领地仅有不足50真实批准兵力 | 不中途 Spawn；不用销毁 Party 来掩盖失败 |

### 测试可观测指标

在 DryRun 前后采集下列快照并严格相等：

```
Clan.Gold / LeaderHero.Gold
ClanRecruitmentBudgetSnapshot.RemainingPurchaseBudget
ClanRecruitmentBudgetSnapshot.RemainingCommitmentBudget
每个来源：TroopRoster 全部 TroopId/Count/Wounded
每个村庄：Village.Hearth, MobilizedHearth, ServiceBatches 集合
FiefPartyManager serving index / recovery 状态
PartySettlementBindingBehavior 已提交绑定数
TransferOperationJournalBehavior completed / RecoveryRequired 记录
Hero.PartyBelongedTo / Hero.GovernorOf
```

**注意**：预演前外层可能已经创建了 `PendingPartyCreation` 预留，应单独验证此 token 在失败/异常时被移除；它不算士兵/Hearth/预算消耗，但不能泄漏成永久锁。

建议优先使用可复现的战役存档及诊断注入开关验证真实 TaleWorlds API；如果仓库没有现成测试工程，可新增**纯 Core 单测项目**，但构建环境不能随意替换 Bannerlord 游戏依赖。`ModifiedArmy/ModifiedArmy/ModifiedArmy.csproj` 目标框架为 `netstandard2.0`，需要原有 NuGet / TaleWorlds 环境，不能仅凭没有游戏 DLL 的本机 `dotnet build` 成功与否判断逻辑正确。

## 15. 日志/诊断要求

尽量复用现有 `PartyCreationDiagnostics`、`ForceRecruitmentDiagnostics`，建议新增如下机器可检索阶段：

```
[PartyCreationDryRun]
  AttemptId, LeaderId, ClanId, SiteId, Day,
  AssumedLimit, TargetCount, PaymentLimit,
  FiefOffered, FiefApproved, GarrisonAvailable, GarrisonApproved,
  TotalApproved, MinimumRegulars, RejectReason,
  BudgetRemaining, ProjectedDailyWage, HearthByVillage,
  RosterFingerprint, PopulationFingerprint, BindingTokenId

[PartyCreationCommit]
  AttemptId, PartyId, SiteId, Phase,
  PreviewFief, ActualFief, PreviewGarrison, ActualGarrison,
  RegularsBefore, RegularsAfter, HeroCount,
  SnapshotMatch, BudgetMatch, PaymentLimitMatch,
  GarrisonOperationId, FiefOperationId,
  Status, Reason, CompensationStatus, RecoveryDomain
```

- 拒绝原因至少分：`NoCreationBinding`、`PartyLimitBelowMinimum`、`TargetBelowMinimum`、`NoHealthyFiefTroops`、`NoWeightedFiefCandidates`、`FiefBelowMinimumBatch`、`VillageHearthInsufficient`、`GarrisonSafeFloor`、`WageLimit`、`MaintenanceFunds`、`SourceSnapshotChanged`、`PartyInitializationMismatch`、`CreationCompensationFailed`。
- **不能把 `PreviewApproved=50` 等同实际成功**；最终必须同时记录 `ActualRegulars` 和事务状态。
- 避免每小时大量输出每村明细；普通成功一条汇总，失败才展开到村庄/兵种和指纹差异。仅记录必要 ID，不依赖中文文本解析逻辑。

## 16. 关键禁区与代码审查检查表

- [ ] 不采用 `bool dryRun` 包住同一个带状态副作用的 `Execute`，靠 `if(!dryRun)` 跳过写入；应抽共用纯 Model + 只在 Commit 写入。
- [ ] 不在预演调用 `PartyExchangeService.PrepareContext`（它会 `ReconcilePartyFiefTroops`）。
- [ ] 不在预演调用 `VillageFiefData.TryMobilize`、`ISettlementPopulationService.TryConsume`、`ClanRecruitmentBudgetManager.CommitRecruitment`。
- [ ] 不把封邑兵与驻军分别 `RecruitmentSimulationState.Create()` 后相加。
- [ ] 不将 `GetEligibleFiefRegulars` 旧估算、伤兵或无模板根兵直接作为已批准人数。
- [ ] 不将 `GetAssumedPartySizeForLordParty` 视为绝对等于真实 `PartySizeLimit`，出生后仍要比较。
- [ ] 不把 Hero 排除在 `NumberOfAllMembers` 之外，但必须把 Hero 排除在50普通兵门槛之外。
- [ ] 不将新建 Party 的原版 `MaxWagePaymentLimit` 作为实际首日可持续工资上限；应用统一自定义算法后再提交。
- [ ] 不因为封邑兵成功35人就宣布新建50人任务完成。
- [ ] 不在跨来源失败时通过正常补员 Task/FieldToGarrison 随意“退回”士兵。
- [ ] 不在 `RecoveryRequired` 或未经验证的部分提交后销毁 Party/解除其封邑绑定。
- [ ] 不重复使用相同 `OperationId` 给两个来源；不破坏现有 `UsesLegacyOperationId` 存档兼容。
- [ ] 不通过临时真实 Party 充当 Dummy Party 预演；也不要伪造 `MobileParty.IsActive` 绕过安全授权。
- [ ] 不改变普通 AI 后续补员、兵种权重、经济倍率、最小封邑批次或玩家行为。
- [ ] 遍历候选基地时释放所有落选/失败预留 token；若使用全部评估后再选址的策略，须在选择最终基地时重新获取有效 token 并重新 DryRun。
- [ ] 使用 `finally` 释放未提交的创建预留 token、注销初始化 Guard；真实已绑定且需恢复者不得错误释放绑定。
- [ ] 核对封邑村庄事务成功后的 `ClanRecruitmentBudgetManager.CommitRecruitment` 记账与异常安全，防止资源已写入但预算/协调器状态未确认。

## 17. 建议 Codex 的第一次行动与交付格式

**直接执行的指令：**

> 请基于当前仓库代码按本 MD 的阶段 A→B→C→D→E 分批施工，不要先重新设计一套相似架构。第一步先核对 HEAD 与本设计审查基线、检查上述逐文件位置，然后实现可复用的 `RecruitmentSubject` 和预演/真实 Party 共用 `BuildPlanCore`，优先完成兼容回归。之后抽取封邑逐村 Hearth 只读分配和驻军转移共享模拟状态，再实现组合事务、受保护补偿，最后接入 `PartyCreationCoordinator`。每阶段先编译、展示修改文件与测试结果，再继续。严禁用“先 Spawn，失败 Destroy”作为正常预检，严禁允许无从验证的 `RecoveryRequired` 直接 Destroy。若遇到原版引擎 API 无法准确预演某项属性，应显式报告差异并使用创建后“写入前重审”，不可偷偷近似并声称结果完全一致。完成后汇总具体修改、验证 T01~T28 结果、异常处理、剩余风险与测试用存档/日志位置。

建议每个阶段的 Codex 反馈：

1. **已读代码与真实入口**（简述关键 API、文件和差异）；
2. **改动文件与签名**（原/新调用链）；
3. **是否保持旧 API 等价**（具体测试记录）；
4. **财务/Hearth/名册/服役批次副作用证据**；
5. **失败补偿与恢复策略**（可注入故障的运行结果）；
6. **构建结果、未验证风险、下一阶段阻塞**。

---

## 18. 源码索引（主要依据）

> 以下是当前仓库实际存在的源码位置，可直接供 Codex 跳转。代码示意中的新类型及重载**不代表仓库已有实现**。

### 新 Party 创建与绑定

- [HeroSpawnSettlementPatch.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Patches/HeroSpawnSettlementPatch.cs)
- [PartyCreationSettlementModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs)
- [PartyCreationCoordinator.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs)
- [PartySettlementBindingBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs)
- [NewPartySizeLimitModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewPartySizeLimitModel.cs)
- [PartyCreationScope.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationScope.cs)
- [原版 DefaultPartySizeLimitModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultPartySizeLimitModel.cs)
- [原版 LordPartyComponent.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/LordPartyComponent.cs)

### 审批与财务

- [DefaultAIRecruitmentModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [RecruitmentSimulationState.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/RecruitmentSimulationState.cs)
- [RecruitmentBudget.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/RecruitmentBudget.cs)
- [ClanRecruitmentBudgetManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Finance/ClanRecruitmentBudgetManager.cs)
- [ForceManagementModels.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs)
- [MobilePartyWageLimitCalculator.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/MobilePartyWageLimitCalculator.cs)
- [ClanFinancialEvaluationService.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Services/ClanFinancialEvaluationService.cs)
- [NewPartyWageModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/NewPartyWageModel.cs)

### 兵源、Hearth、调兵执行

- [PartyExchangeModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs)
- [PartyExchangeService.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs)
- [UnifiedForceContracts.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/UnifiedForceContracts.cs)
- [FiefPartyRecruitment.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [FiefPartyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [VillageFiefData.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [PopulationContracts.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs)
- [ModifiedPolitics/SettlementPopulationBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs)

### 初始补员任务、事件与持久化

- [PartyExchangeBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/PartyExchangeBehavior.cs)
- [PartyForceTaskCoordinator.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs)
- [AiForcePlanningBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/AiForcePlanningBehavior.cs)
- [UnifiedRecruitmentExecutionService.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Services/UnifiedRecruitmentExecutionService.cs)
- [PartyVisitExecutionService.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs)
- [TransferOperationJournalBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/TransferOperationJournalBehavior.cs)
- [CampaignRecoveryCoordinator.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/CampaignRecoveryCoordinator.cs)

