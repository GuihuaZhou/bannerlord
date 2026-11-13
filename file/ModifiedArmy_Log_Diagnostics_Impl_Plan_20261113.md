# ModifiedArmy｜基于最新仓库的失败诊断日志补强实施任务书

> **用途**：交给 Codex / 编码 AI 直接执行。此文档是**实施说明，不代表已修改仓库代码**。  
> **仓库**：`GuihuaZhou/bannerlord`  
> **基线**：`main@8def87a0ee1493bff3b1446d2e651fcbcce13a74`（2026-11-13 检查时 `main` HEAD）  
> **参考日志**：`ModifiedArmy(20261112-170131).log`（21,341 行）  
> **主要目标**：同一个 AI 建军 / 封邑征兵 / 驻军招募 / 军团资格失败，能凭 **关联 ID + 明确的失败阶段 + 数值与条件**判断根因。**只补诊断，不修业务逻辑。**

## 0. 编码 AI 首先必须遵守的边界

1. **不得调整** AI 建军门槛、招募目标、最低封邑批次、兵种模板或升级树、军团资格、驻军容量、职业兵消耗 / 人口负担公式、调兵优先级、部队移动规则、文化制度配置。
2. **不得触碰**已解决的封邑兵冻结问题。历史 `MissingServingParty`、`Fief population workflow frozen` 不作为本次待修复缺陷。封邑供养“超预算”可由**士兵服役期间升级**导致，不改变现有预算或升级规则。
3. 不添加新持久化数据、Saveable 字段、战役恢复状态、Harmony patch、独立日志平台或重构原有调兵架构。复用当前 `ModLogger`、`RecruitmentLogFilter`、`PartyCreationDiagnostics`、`ForceRecruitmentDiagnostics`、`MarshalArmyEligibilityTrace`。
4. **只读诊断**：不能为了打印日志重新执行 `BuildPlan`、`Preview...`、`PrepareContext`、`Reconcile...`、`TryConsume...`、`GetCustomStrength`、随机判定、移动指令、财务提交等可能影响结果的流程。优先复用本次正常业务执行中**已经计算出的局部变量和快照**。
5. 原有事务回滚、失败保护、异常传播与游戏存档兼容性保持不变。若某诊断字段获取风险较大，允许标记 `unavailable`，不允许为获取字段重复运行有副作用的逻辑。
6. 失败时打印足够定位的信息；成功时保留原摘要。避免每小时、每个领主、每个候选兵种反复打印相同全量信息。
7. **不改仓库中已有文档提到的旧实现为“事实”**：以当前基线代码为准，历史文档仅供对照。

## 1. 从日志和代码确认的事实（请勿误诊）

| 日志现象 | 本次扫描 | 代码核对后的正确解释 |
|---|---:|---|
| `[部队创建] 阶段=Failed; 原因=InitialRecruitmentCommittedZeroTroops` | **923 次** | `PartyCreationCoordinator.TryCreateManagedLordParty` 在创建、进入定居点及可能的职业兵回退后，发现 `party.MemberRoster.TotalRegulars <= 0`；**不能仅凭此说明哪个前置环节失败**。|
| `[初始征兵审批] ... 模板合法=0` | **171 次** | 现有日志无法区分 `IsEnableTroop` 过滤、无健康库存、正权重根缺失、升级树不匹配等不同阶段；不要直接改诺德兵种表。|
| `Professional recruitment capacity rejected` | **592 次** | `GarrisonRecruitmentExecutionService` 在 `pools.GetMaxAffordableProfessionalCount` 返回 0 时输出；这是**Hearth/RecruitmentBurden 供养审批**，**并非驻军编制上限不足**。|
| `ELIGIBILITY_FAIL_NATIVE_UNKNOWN` | **711 次** | `MarshalArmyAiBehavior` 对 `CanLordCreateArmy(false)` 固定写入该泛化字符串；具体原因可能已在诊断模型中求出，但**详细跟踪仅限玩家王国特定领袖**。|
| `NoEligibleCreationBase` | **大量重复** | `PartyCreationCoordinator` 在最终拒绝分支把候选的数量/理由替换成全 0；丢失了已完成的 `evaluations` 详情。|
| `SourceBoundToSameParty` | 多次 | **允许访问**：`PartySettlementBindingBehavior.EvaluateFiefSourceAccess` 对相同部队返回 `FiefSourceAccessDecision.Allow(...)`。不能当成拒绝原因。|

**样本**：日志 2026-11-12 21:30:19，法芬（西比尔）：就绪兵 82、模板合法 82、申请 46、实际 0，短时间重复失败。21:30:35，诺德哈尔达尔（特龙德拉格）：就绪 81、模板合法 0。21:37:07，于桑克堡：职业兵候选 2、模型批准 2、编制仍有 232 空位，却实际入营 0。样本只能说明**需要诊断**，不是证明某个业务判断一定错误。

### 关键源码（所有路径以 `ModifiedArmy/ModifiedArmy/` 为根，另有跨 Mod 文件）

- `PartyManagement/Services/PartyCreationCoordinator.cs`：初始建军生命周期、回退及最终 `InitialRecruitmentCommittedZeroTroops`。
- `PartyManagement/Models/PartyCreationSettlementModel.cs`：候选基地、`BindingEligible`、兵源预估。
- `PartyManagement/Diagnostics/PartyCreationDiagnostics.cs`：当前最终输出入口。
- `PartyManagement/Bindings/PartySettlementBindingBehavior.cs`：封邑来源授权（同部队绑定时为 Allow）。
- `ForceManagement/Behaviors/AiForcePlanningBehavior.cs`、`ForceManagement/Missions/PartyVisitExecutionService.cs`：初始任务、到场、事务 ID、状态。
- `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`：初始/日常从 Fief、Garrison、Mercenary 招募。
- `ForceManagement/Models/PartyExchangeModel.cs`：预览、审批、正权重模板和 `NoApprovedTroops`。
- `ForceManagement/Services/PartyExchangeService.cs`：验证快照、二次校验、实际提交、回滚。
- `ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs`：已有 `LogLiveApproval/LogExecution`。
- `Fief/Models/PartyData/FiefPartyRecruitment.cs`：封邑模板资格、候选、最低批次、按村庄事务提交。
- `Recruitment/Services/GarrisonRecruitmentExecutionService.cs`、`Recruitment/Services/ProfessionalRecruitmentService.cs`、`Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`：日常驻军职业兵计划及提交。
- `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`：职业兵 Offer 和人口负担接口。
- `Garrison/Models/GarrisonRecruitmentCapacity.cs`：驻军硬空位、软目标、征粮队预留。
- `Recruitment/Patches/GarrisonRecruitmentPatch.cs`：**独立的原版基础驻军增长路径**，不能与每日职业 Offer 路径混为一谈。
- `Population/Contracts/PopulationContracts.cs`：`RecruitmentBurdenMath` 及服务接口。
- `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs`：实际的 `GetMaxAffordableProfessionalCount`/`TryRecruitProfessional` 与 `Hearth/Burden` 计算。
- `Army/Behaviors/MarshalArmyAiBehavior.cs`、`Army/Models/DiagnosticArmyManagementCalculationModel.cs`、`Army/Diagnostics/MarshalArmyEligibilityTrace.cs`：军团资格调用和详细原因跟踪。

---

## 2. P0｜领主初始建军“实际0人”完整因果链

### 2.1 当前确切执行路径

```text
PartyCreationSettlementModel.FindEligibleBase (可用兵源估算)
  -> PartySettlementBindingBehavior.TryReserveForCreation
  -> PartyCreationCoordinator.TryCreateManagedLordParty
      -> SpawnLordParty
      -> TryCommitCreation
      -> AiForcePlanningBehavior.TryRegisterInitialRecruitTask
      -> EnterSettlementAction.ApplyForParty
           -> PartyVisitExecutionService.Execute
           -> UnifiedRecruitmentExecutionService.Execute
               -> PartyExchangeModel.BuildPlan
               -> PartyExchangeService.Execute
                   -> FiefPartyManager.RecruitFiefTroopsFromSettlement
                      -> FiefPartyRecruitment.TryCommitVillageRecruitment
      -> 若初始Fief后仍0人，尝试Professional备用任务
      -> 检查最终TotalRegulars
      -> FailAfterCreation(..., "InitialRecruitmentCommittedZeroTroops")
```

**已知诊断断点**：`PartyCreationDiagnostics.Log` 输出尝试、任务、事务及最终人数，但 **没有记录初始任务结果和回退任务结果各自的原因码**；`FailAfterCreation` 先 `CancelTask`、释放绑定和 `DestroyPartyAction`，**随后**才输出日志，容易失去失败前完整上下文。`EnterSettlementAction` 的同步流程也需要明确“是否实际触发本次招募”。

### 2.2 最小修改

**修改** `PartyCreationCoordinator.cs` 和 `PartyCreationDiagnostics.cs`：

- 在创建事务结束前、任何取消/销毁前，捕获只读失败快照（建议局部变量或已有 `Log` 的可选参数；不要新建持久实体）。包含 `attemptId`、leaderId、partyId、settlementId、`site.RejectionReason`、`site.BindingEligible`、`estimatedFief/estimatedGarrison`、`preferredSource`、`initialTaskId/operationId`、`initialTaskStatus/reason/progress`、`fallbackAttempted`、`fallbackTaskId/operationId/status/reason/progress`、`afterArrivalRegulars`、`beforeDestroyRegulars`、`finalFailureStage`。
- 对于 `EnterSettlementAction.ApplyForParty`：只记录到场前后实际人数与当前任务变化，**不得额外再调用执行器**来补日志。
- 最终失败必须明确“首选源是否有计划、是否进入执行、计划批准数、执行调兵数、执行返回原因、回退是否发生及为何失败”。若当前方法没有对应结果，须在正常调用链处自然捕获后传回；不得推测或重算。
- 对 `NoEligibleCreationBase`：不要再将代表候选强制写成0。输出 `evaluatedSiteCount`、`eligibleBindingSiteCount`、`eligibleSupplySiteCount`、按 `RejectionReason` 分组的计数，和 **1个**最接近成功的候选明细；无候选要写 `NoEligibleClanSettlement`。保留原来选择/拒绝逻辑。
- 成功依旧只需一条简明完成日志；失败加一次完整总结，不要多条重复的“结果=0”。

**示例（虚构字段值，用于验收输出结构，并非当前存档事实）**：

```text
[MA.Diag][CreateFinal] attempt=a1 leader=lord_2_18 party=lord_2_18_party_1 site=town_S6 result=Failed reason=InitialRecruitmentCommittedZeroTroops estimatedFief=82 estimatedGarrison=0 preferred=Fief initialTask=t1 initialOp=force-task:t1:Recruit:Fief:town_S6 initialStatus=Cancelled initialReason=NoApprovedTroops initialPlanApproved=0 initialMoved=0 fallbackAttempted=false fallbackReason=NotRegistered afterArrivalRegulars=0 beforeDestroyRegulars=0 failedStage=InitialRecruitment
```

### 2.3 验收要求

对同一 `attemptId` **必定找到一条终态**。第一次征兵返回0时，即使随后备用源成功，也应可查首选源的失败原因。`FailAfterCreation` 的失败快照必须来自销毁前。重复出现的无可用基地候选日志不再丢弃已评估的实际数字。

---

## 3. P0｜封邑来源“有兵但批准0 / 执行0”的分层原因

### 3.1 已有代码基础

- `PartyExchangeModel.BuildFiefMobilizationPlan` 调用 `FiefPartyManager.PreviewFiefRecruitmentApprovals`，已有 `LogInitialFiefApprovalFailure`，**但仅在 manifest 总数为0时打印**。
- `FiefRecruitmentCandidateDiagnostics` 已有 `ReadyTotal/ReadyHealthy/TemplateEligible/RootMatched/RootUnmatched/CandidateCount/WeightedAllocatedCount`，避免重复造一份新结构。
- `PartyExchangeService.ExecuteFiefMobilization` 还可能拒绝：`UnsafeAtExecution`、`SourceVillageManifestChanged`、`AuthoritativeFiefManifestChanged`、`AuthoritativeFiefResultChanged`。
- `FiefPartyRecruitment.RecruitTroopsToParty` 能在预览后再次遇到 `expectedApprovals` 不符、最低批次不达标、`TryCommitVillageRecruitment=false` 等分支。
- `TryCommitVillageRecruitment` 已有 `RejectRecruitmentTransaction`，但**其对非玩家所属王国使用了 `RecruitmentLogFilter`，直接返回而不打印**。而且内部 `operationId=Guid.NewGuid()`，与外层 `force-task:...` ID 不是同一个值。

### 3.2 改动方式（优先复用现有日志）

**文件**：`ForceManagement/Models/PartyExchangeModel.cs`、`ForceManagement/Services/PartyExchangeService.cs`、`Fief/Models/PartyData/FiefPartyRecruitment.cs`，必要时 `Fief/Models/FiefPartyManager.cs`。

每个失败事务要区分：

1. **授权阶段**：`bindingAllowed=true/false` 与 `bindingReasonCode` 分开输出。`SourceBoundToSameParty` 只能显示为 `Allowed`，禁止转写为拒绝。
2. **候选阶段**：`readyTotal`、`healthy`、`templateEligible`、`rootMatched`、`weightedOffered`、`requested`；若为0，还要有分组统计：`disabledByTemplate`、`noPositiveWeightRoots`、`noUpgradePathToWeightedRoot`、`woundedOnly`、`noPartyFreeSlots`。只对真实经过的筛选生成准确理由。
3. **审批阶段**：`approvedBeforeMinimumBatch`、`minimumBatch`、`approvedAfterMinimumBatch`、`admissionPrimaryLimitCounts`，区分 `NoWeightedCandidates`、`AdmissionReturnedZero`、`BelowMinimumFiefRecruitBatch`、`ManifestConstructionZero`。
4. **执行前验证**：`planManifestCount`、`liveApprovalCount`、`fiefFingerprintMatch`、`sourceSnapshotMatch`、`destinationSnapshotMatch`、`firstFailedPredicate`、`executorReason`。只使用已经执行的验证结果或其安全的本地值。
5. **权威提交**：`approvedCount`、`committedCount`、`commitStage`、`commitReason`、`rollbackVerified`、`recoveryRequired`。`TryCommitVillageRecruitment` 内的 `InvalidRequest/ServicePartyBlocked/InsufficientReadySource/PartyCapacityInsufficient/VillageHearthInsufficient` 等已有原因码必须能和**外层创建尝试关联**。

**特别要求：** 不要把外层 `operationId` 替换掉内层已有 `Guid`，因为它可能具有独立事务语义。采用**只用于日志的父子关联**（例如 `parentOperationId` / `creationAttemptId` / `internalFiefTransactionId`），使用可选诊断参数或现有任务上下文传递，**不影响幂等键**。如为避免大范围签名变化，可先在外层捕获 `PartyExchangeExecutionResult.ReasonCode` 和分层零审批数；对原本会被 `RecruitmentLogFilter` 静默的**初始建军失败**再追加一条有条件的详细拒绝日志。

**Nord诊断的特别要求**：模板合法0时汇总**每个兵种ID、数量及被过滤的精确阶段**（最多10种，其余汇总 `omittedTypes`）；检查 `IsEnableTroop` 与 `GetUpgradeDistance` 的**实际**判定，不能直接把“诺德模板错误”写死为原因。

**验收**：对于“就绪82、模板合法82、实际0”，日志能指出到底是候选/预算审批/最低批次/manifest/执行前验证/村庄提交的**哪一阶段**。对于“就绪81、模板合法0”，能按兵种汇总为什么未进入合法集合。`SourceBoundToSameParty` 仍为授权成功。

---

## 4. P1｜职业驻军“候选批准>0，实际入营0”的真实原因

### 4.1 这不是单一的“驻军容量”

当前代码存在**至少三套不同限制**：

1. **硬编制**：`GarrisonRecruitmentCapacity.GetHardFreeSlots`。
2. **软自动补员目标**：`GetSoftRecruitmentFreeSlots`，包含征粮队预留人数。
3. **人口供养/招募负担**：`SettlementManpowerProductionBehavior.GetMaxAffordableProfessionalCount` 调用 `SettlementPopulationServiceRegistry.Current.GetMaxAffordableProfessionalCount`，再进入 ModifiedPolitics `SettlementPopulationBehavior` 和 `RecruitmentBurdenMath`。这是当前 `Professional recruitment capacity rejected` 的实际来源。

在第3条计算中，关键量是 `Hearth`、`RecruitmentBurden`、`GetProfessionalBurdenCost(troop)`；`GetMaxAffordableCount` 可能因供养空间不足得到0。**即使驻军还有几百个空位，这一结果也可能合理**；日志目前缺少能证明其合理性的 `Hearth/Burden/Cost/EffectiveCapacity`。

### 4.2 优先修改的文件与点位

- `Recruitment/Services/GarrisonRecruitmentExecutionService.cs`：`ExecuteDetailed` 逐候选执行处。保留当前模型批准、日配额、硬/软空位、供养评估的**先后顺序**，在`count`每次被 `Math.Min` 缩减后留下原因和数值。当前 `TryCommit` 返回false直接 `continue`，需要失败阶段与拒绝原因。
- `Recruitment/Services/ProfessionalRecruitmentService.cs`：`TryCommit` 失败时区分 `InvalidRequest`、`OfferConsumptionRejected`、`RosterCommitFailed`、`RollbackFailed`；只增加只读诊断（如可选 `out reasonCode` 重载），不改变 bool 返回值/事务。
- `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`：`TryConsumeProfessionalOffer` 已有内部失败日志，但缺乏关联ID/稳定原因及人数前后；`GetMaxAffordableProfessionalCount` 对服务缺失或异常最终返回0，**不应一概显示 `InsufficientRecruitmentCapacity`**。至少在诊断中区分 `PopulationProviderMissing`、`PolicyException`、`SettlementRecoveryRequired`、`BurdenCapacityInsufficient` 和 `OfferMismatch`，保持原拒绝结果不变。
- `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`：`GetProfessionalFinalLimit` 当前会把“已审批但实际0”的情况归为泛化 `execution_changed`，新增执行结果的**真实拒绝阶段**与 `failureReasonCounts`。不要重复调用 `RecruitmentModelManager.Model.BuildPlan`。
- `Recruitment/Services/GarrisonRecruitmentExecutionResult.cs`：**可选**增加**运行时**聚合字段，如 `Attempts/RejectedByBurden/RejectedByOffer/Committed` 或只读原因计数；既有方法仅返回总数，对最终摘要表达不足。不得序列化。
- `Recruitment/Patches/GarrisonRecruitmentPatch.cs`：`NativeBaseGarrisonPopulationPatch` 是另一条原版基础驻军增长路径，日志必须带 `path=NativeBaseGrowth`；日常 Offer 路径带 `path=ProfessionalOffer`。两条路径共享人口负担，但计划/名册提交机制不同，不能串成一笔事务。
- `ModifiedPolitics/.../SettlementPopulationBehavior.cs`：**第一轮尽量不修改跨 Mod 业务代码**。如需更具体的 `IsRecoveryRequired`/事务拒绝细节，只增诊断字段或对已有 `PopulationTransactionResult` 做只读输出，不能调整人口公式或缓存。

### 4.3 必需字段

每个失败的定居点/天/来源汇总至少输出：

```text
path, dayIndex, settlementId, troopId/tier, sourceOfferCount,
modelApproved, dailyQuotaRemaining, hardFree, softFree, reservedEscorts,
requestedBeforeBurden, hearthBefore, burdenBefore, effectiveCapacityBefore,
burdenCostPerTroop, affordableCount, offerCountBeforeCommit,
commitRequested, committed, firstFailedStage, reasonCode,
populationOperationId(if any), rosterBefore, rosterAfter, rollbackStatus
```

人口资源数值读取应优先使用执行时已有值；如需补读仅限**纯只读接口**，并避免因读取导致注册表状态初始化或日志异常。对已成功的日常小额补员保持原摘要。

**示例（虚构）**：

```text
[MA.Diag][GarrisonDailyFinal] path=ProfessionalOffer day=92072 settlement=castle_V7 offers=2 modelApproved=2 dailyLimit=4 hardFree=281 softFree=191 actual=0 failureCount=2 primary=BurdenCapacityInsufficient
[MA.Diag][GarrisonCandidateRejected] day=92072 settlement=castle_V7 troop=vlandian_x tier=4 modelApproved=1 requestedBeforeBurden=1 hearth=120 burden=120 effectiveCapacity=0 burdenCost=4 affordable=0 stage=PopulationAffordability reason=BurdenCapacityInsufficient
```

**验收**：看到“候选2、模型批准2、实际0”必须能证明是 `hard/soft/daily/financial/offer/population/roster` 哪种门槛；日志不再用笼统的“审核后名册、容量或候选库存发生变化”代替原因。尤其要能通过数字分辨“编制有空位但Hearth负担不足”与真正的提交失败。

---

## 5. P1｜军团创建资格：复用已有 Model 的明确结果

### 当前原因（代码已证实）

`Army/Behaviors/MarshalArmyAiBehavior.cs` 调用 `CanLordCreateArmy` 后，在 `false` 分支**固定**写 `ELIGIBILITY_FAIL_NATIVE_UNKNOWN`。然而 `Army/Models/DiagnosticArmyManagementCalculationModel.cs` 已经在每个重要分支调用 `trace.Result(false,"...")`，例：`INFLUENCE_LE_100`、`FOOD_LE_SIEGE_THRESHOLD`、`NO_WAR_ENEMY_TOWN`、`LEADER_RATIO_LE_AI_THRESHOLD`、`NOT_ELIGIBLE_CLAN_LEADER`、`NO_CANDIDATES_AFTER_TRUNCATION`、`TOTAL_SIEGE_STRENGTH_LT_1000` 等；另有候选剔除和王国容量细节。

**真正缺口**：`MarshalArmyEligibilityTrace.Begin()` 只有在“玩家所在王国的统治者/符合条件的大元帅”时 `Enabled=true`，且详细候选每天最多采样一次。其他王国就只能看到泛化的外层 `ELIGIBILITY_FAIL_NATIVE_UNKNOWN`。**不得重新复制、重跑、或修改 Model 的资格判断以获取原因。**

### 最小修改建议

- 在 `MarshalArmyEligibilityTrace` 中保留 **本次自然调用**的 `Result` 终态原因供调用方关联，或在可控范围内直接打印 `RESULT` 摘要；要求绑定 `partyId + traceId + dayIndex`，避免跨调用串号。不能简单维护一个全局 `lastReason`。
- `MarshalArmyAiBehavior` 的 `ELIGIBILITY` 输出优先使用当前调用的诊断模型实际终态 `reasonCode`，拿不到时才保留 `ELIGIBILITY_FAIL_NATIVE_UNKNOWN` 并记录 `reasonUnavailable=...`。
- 全王国**失败时**可输出少量 Leader gate/最终终态摘要，候选明细仍按指定目标/一次每英雄每天采样，避免每Tick输出数百条。保留现有 Player Kingdom 限制作为**详细候选列表**的默认过滤条件。
- 结果如 `TOTAL_SIEGE_STRENGTH_LT_1000`，必须同时显示 `beforeClear>0 / afterClear=0`，避免把“筛选有兵但强度不足导致清空”误判为“无可征召成员”。
- 不能更改 `OrderByQ` 排序、`1000` 强度门槛、大元帅身份适配、NavalDLC 模型链。不得为诊断再调用一次 `CanLordCreateArmy`。

**验收**：非玩家王国的军团资格失败也能找到**一条**具体 Leader / 容量 / 强度阶段的原因摘要；不能保证所有外部/原版模型都被诊断到时，要诚实输出 `UninstrumentedModel` 而非伪造理由。

---

## 6. P2｜补员任务移动状态（只在确实异常时补）

现有 `ForceRecruitmentDiagnostics.LogTaskContinuation` 已打印 `task.Status`、`OwnsMovementOrder`、`TargetMatches`、`party.DefaultBehavior`、最近移动事件；日志可能出现“任务 Planned 且未持有指令，但实际 GoToSettlement”的组合。这**不自动等于移动故障**。

如上一阶段完成后仍无法追溯某支部队：在 `AiForcePlanningBehavior` 状态变更点和 `PartyForceTaskCoordinator` 发出/撤销指令处增加 `oldStatus -> newStatus`、`oldOwner -> newOwner`、`targetId`、`movementEventId`、`taskId`、`reasonCode` 的单条变化日志；相同状态重复Tick只做计数，不复制日志。**不更改AI行动、移动目标及任务续期策略**。

---

## 7. 统一日志约定（最小化设计）

### 7.1 沿用现有入口，不另造系统

- `attemptId`：一次创建候选/建军生命周期；创建失败、回退都用同一个 `attemptId`。
- `taskId`：现有 `PartyForceTask.TaskId`；若回退职业兵生成新任务，额外打印 `parentAttemptId`，不要假装是同一个Task。
- `operationId`：现有 `PartyVisitExecutionService.BuildOperationId`；**不得改变其组成或生成规则**。
- `internalTransactionId`：封邑、人口等内部独立事务 ID，保留本来的幂等/恢复意义，通过 `parentOperationId` 关联。
- `dayIndex`：`CampaignTime.Now.ToDays`（游戏绝对天数），和现实时间戳分开。
- `stage`/`reasonCode`：稳定、大小写固定的英文；中文名称可以在旁补充，不能只靠翻译文本解析。
- `status`：明确 `Evaluated / Planned / Rejected / Committed / PartiallyCommitted / RolledBack / RecoveryRequired / Cancelled`，避免只写笼统 `Failed`。
- `before/after/requested/approved/committed` 明确它们各自计量口径，禁止把 `preview` 直接当作 `committed`。

### 7.2 输出量控制

1. 每个 `operationId` **只输出一次最终事务摘要**；关键分支可另打印一条带相同ID的首个失败谓词。不应在不同模块争相打印多条“最终结果=0”。
2. 当相同 `partyId+siteId+reasonCode+dayIndex` 一再失败时，保留首条详细记录，之后聚合 `repeatCount`，遇到理由变化才立刻打印。计数只保留内存，不存档。
3. 初始建军失败可绕过**玩家王国范围**的普通采样，但需专门限定 `attemptId != null && terminalFailure`；不得把 `RecruitmentLogFilter.ShouldLog` 直接改成“所有王国全天全量Debug”。
4. 每兵种明细在失败场景最多10条，余者按原因汇总；不要输出所有候选成员、完整名册、无界堆栈或长指纹。真实异常可打印一次完整异常栈，正常拒绝只打印原因码。
5. `ModLogger` 写日志失败不得影响交易；仅日志代码可吞自身格式化异常，不能吞业务本该传播的异常。
6. 日志必须标记 `path=...` 区分 `InitialFief / InitialProfessionalFallback / RoutineGarrisonWithdrawal / ProfessionalOffer / NativeBaseGrowth / ArmyEligibility`。

---

## 8. 交付拆分与实施顺序

| 批次 | 目标 | 主要文件 | 交付判据 |
|---|---|---|---|
| **W1（先做）** | 初始建军与封邑0人因果链 | `PartyCreationCoordinator`、`PartyCreationDiagnostics`、`PartyExchangeModel`、`PartyExchangeService`、`FiefPartyRecruitment` | 西比尔82合法兵/0调拨可以定位到具体首败环节；诺德模板合法0有兵种过滤摘要。|
| **W2** | 日常职业驻军0补员与人口负担 | `GarrisonRecruitmentExecutionService`、`ProfessionalRecruitmentService`、`SettlementManpowerProductionBehavior`、`GarrisonRecruitFromPrisonersBehavior` | 于桑克堡候选2/批准2/实得0能区分编制与Hearth负担。|
| **W3** | 军团资格外层原因与详细模型关联 | `MarshalArmyAiBehavior`、`MarshalArmyEligibilityTrace` | 非玩家阵营失败有原因码，原模型链与门槛完全不变。|
| **W4（可选）** | 移动任务状态转移日志 | `AiForcePlanningBehavior`、`PartyForceTaskCoordinator`、`ForceRecruitmentDiagnostics` | 状态冲突时知道由谁发出/解除移动指令。|

**建议执行顺序：W1 → W2 → W3；W4只在有实测必要时实施。**

## 9. 必须提供的验收与回归证据

1. **编译检查**：按仓库原项目进行可用的编译/静态检查；无法访问游戏 DLL 时写明未能编译的原因，不得声称测试通过。
2. **行为不变**：查看 Git diff，任何改变招募数、Hearth/RecruitmentBurden、服务批次、成员名册、任务调度、预算扣款、权限或随机行为的改动，均应视为越界。日志的可选参数/只读快照不改变分支优先级。
3. **创建失败**：构造/复用“封邑有合法兵、创建0人”场景；同一 `attemptId` 可以连接选址、初始任务、预览、执行、回退和最终销毁前人数。`sourceBoundToSameParty` 显示 `Allowed=true`。
4. **模板0**：已有81人、健康81人、模板合法0，必须按兵种汇总被拒条件；输出数量守恒，统计各阶段口径清楚。
5. **职业兵0**：Offer2/模型批准2/实际0，必须有 `Hearth`、`Burden`、每兵种负担成本和供养批准数；要能区别 `hardFree>0`、`softFree>0` 与 `affordable=0`。不得用“驻军满了”做推断。
6. **军团资格**：真实单次调用中得到终态原因；`candidateCountBeforeStrengthCheck` 和清空后的值都能关联；不能为了日志重复计算军团创建资格。
7. **日志体量**：比较同长度游戏时间的原始/修改后日志，确保普通成功路径没有成倍增量，正常重复拒绝有节流或按失败聚合。
8. **存档**：原有存档正常加载与保存，封邑冻结已修复逻辑不被重新开启，服役升级导致预算占用变化不被当作错误处理。

**编码 AI 最终回复必须提供**：改动文件清单、每个原有模糊原因对应的新增字段、编译/验证结果、至少三组实际运行日志片段（无法运行则标注**示例**）、任何未取得的原始原因及下一步获取方式。**不要提交未经验证的“业务修复”。**

---

## 10. 固定代码参考链接

所有链接都锁定本次审阅的提交，避免之后主分支更新造成行号和语义漂移：

- [PartyCreationCoordinator.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs)
- [PartyCreationDiagnostics.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/PartyManagement/Diagnostics/PartyCreationDiagnostics.cs)
- [PartyExchangeModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs)
- [PartyExchangeService.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs)
- [FiefPartyRecruitment.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [PartySettlementBindingBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs)
- [GarrisonRecruitmentExecutionService.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs)
- [SettlementManpowerProductionBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [SettlementPopulationBehavior.cs（ModifiedPolitics）](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs)
- [MarshalArmyAiBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/Army/Behaviors/MarshalArmyAiBehavior.cs)
- [DiagnosticArmyManagementCalculationModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/Army/Models/DiagnosticArmyManagementCalculationModel.cs)
- [MarshalArmyEligibilityTrace.cs](https://github.com/GuihuaZhou/bannerlord/blob/8def87a0ee1493bff3b1446d2e651fcbcce13a74/ModifiedArmy/ModifiedArmy/Army/Diagnostics/MarshalArmyEligibilityTrace.cs)

**最后一句任务要求（可直接给 Codex）**：以此任务书为准，仅实施失败诊断补强，并对每个模糊原因沿现有业务调用链补全“真实第一个失败条件 + 当次计算数据 + 事务关联ID”。不要猜测根因、不要顺手修复规则、不要增加第二套调兵或日志系统；若无法安全观测某个条件，明确报告缺口。
