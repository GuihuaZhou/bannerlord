# ModifiedArmy Workers 联合施工方案 V1.0

**主题：封邑兵源排他、兵役对账修复、AI 建军零审批诊断**  
**仓库：** `GuihuaZhou/bannerlord`  
**参考提交：** `9d6d100fba631eb996595019f2f55e7866402d86`  
**参考日志：** `ModifiedArmy(20261108-062505).log`  
**状态：** 供 Codex Workers 编码施工；本文不是已修改代码或已通过测试的声明。

> 施工前须检查实际 Git HEAD、工作区状态和现有实现，不得假定参考提交仍是最新版本。以仓库真实调用关系为准；保留既有架构、Saveable 类型及编号、SyncData 键、人口事务、现有安置规则。不得创建第二套兵役账本，不得绕开审批系统，不得修改文化军事制度和数值配置。

## 一、总体业务规则

### 1.1 封邑兵源排他

**一个城镇或城堡作为唯一兵源单位，其驻军与全部附属村庄封建兵，在同一时间只能向同一支绑定领主 Party 提供兵力。**

```text
城堡 A
 ├─ 城堡驻军
 ├─ 附属村庄 V1 的封建兵
 ├─ 附属村庄 V2 的封建兵
 └─ 附属村庄 V3 的封建兵
              │
              ▼
          绑定 Party A
```

Party B 即使同属一个 Clan，也不得在同时期使用上述兵源。Party A 在合法审批范围内可从其绑定城堡的驻军及附属村庄征兵。排他绑定的粒度是城镇/城堡，不是各村庄或兵种。

一个村庄允许保留多个批次记录，但**不得将同一个封邑的兵源并行供给不同 Party**。若历史记录违背这一规则，必须诊断，不得擅自销毁士兵。

### 1.2 数据权威

| 数据 | 权威职责 |
|---|---|
| `ServiceBatches` | 在役士兵身份、兵种、人数、批次、来源、服役 Party |
| `MobileParty.MemberRoster` | Party 真实名册，供战损与在役身份匹配 |
| `MobilizedHearth` | 已动员的人口占用标记，须与有效在役批次一致 |
| `PartySettlementBindingBehavior` | 封邑兵源与 Party 的绑定、创建预留管理 |

`MobilizedHearth` 不能反过来证明士兵是否存在。兵役归属和人数以**有效的** `ServiceBatches` 为准，并通过 Party 名册确认战损；实际 Hearth 扣减和返还仍须遵守人口事务证据。

### 1.3 保护条件

正常战损和可安全修复的数字差异，不应永久冻结。真实批次 ID 重复、来源/Party 引用损坏、兵源排他冲突、人口事务或在役回滚失败、无法证明的人口动员赤字仍须保留保护机制。

---

## 二、Worker A：封建兵役对账与动员记录修复（P0）

### A.1 目标与复现

日志中出现：

```text
Fief mobilization mismatch: village=castle_village_N7_2, tracked=50, serving=49
Fief population workflow frozen: reason=InvariantMismatch
ServicePartyValidationFailed
```

正常战损应结算批次并同步动员占用，不得因每日 Tick 的检查顺序而提前冻结。

### A.2 重点文件

```text
ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs
ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs
ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs
ModifiedArmy/ModifiedArmy/Fief/Services/FiefServiceLedger.cs
```

### A.3 原始在役人数

在 `VillageFiefData` 提供只读方法（建议 `GetRawServingCount()`），直接从结构有效的 `ServiceBatches[*].Troops` 汇总人数；不得使用按模板过滤的 `GetServingFiefTroopCount(template)` 校验动员人口。批次为空、负数、重复、来源错误、Party 引用失效等应分别作为结构问题报告，不得简单忽略。

### A.4 调整每日 Tick

当前执行顺序：

```text
FiefPartyData.DailyUpdate
→ ReconcileMobilization
→ AdvanceServiceDays
→ ReconcilePartyFiefTroops
```

目标：

```text
原有每日更新
→ 在役批次/索引结构校验
→ 生成 Party 在役对账计划
→ 结算真实战损及动员记录
→ 用原始 ServiceBatches 人数完成一致性核验
→ 安全修复或仅在无法安全处理时冻结
```

`FiefPartyData.DailyUpdate()` 当前是空实现，保留即可；不进行无关生产系统重构。战后事件及读档路径也须复用正确的账目规则。健康优先、升级链匹配、在役全局身份保护必须保持。

### A.5 动员账目结算

定义：`S0` = Party 对账前原始有效在役人数；`S1` = 对账后有效在役人数；`M0` = 原始 `MobilizedHearth`。

| 情况 | 示例 | 正确结果 |
|---|---|---|
| 普通战损 | S0=50、M0=50、实际49 | S1=49、M1=49；Hearth 不返还 |
| 动员标记多1 | S0=49、M0=50、实际49 | 证据允许时仅释放多余1标记，M1=49；Hearth 不返还 |
| 标记少1且有1战损 | S0=50、M0=49、实际49 | S1=49、M1=49；不重复释放标记 |
| 标记少1但无战损 | S0=50、M0=49、实际50 | 保留50名士兵；核实人口事务，证据不足时保护；不得凭空加标记 |
| 模板过滤漏算1 | 原始 S0=50、过滤后49、M0=50 | 无动员差异，不冻结 |

`M0 >= S1` 时可在证据与事务安全校验通过后结算应释放的 `M0-S1` 标记；对账提交后应验证 `MobilizedHearth == ServiceBatches` 原始有效总人数。不得无条件 `MobilizedHearth = S1`。因为现有 `GetEffectiveSupplyHearth()` 计算中包含 `Village.Hearth + MobilizedHearth`，凭空加标记会虚增供养能力。

### A.6 不把多 Party 共享封邑当正常业务

检查同一来源封邑的在役批次归属是否满足排他规则；同一封邑指向不同 Party 时产生精确诊断（建议 `FiefSourceMultipleServingParties`），不按“合法多人共享兵源”设计常态联合调度。不得据此删除历史在役批次；若需要处理历史异常，保留数据、阻止不安全的后续写入，并交由 Worker B 的绑定校验确认。

一个 Party 可能保留不同来源的在役身份记录；对账始终保护其所有合法批次，不以当前访问村庄替代全局身份匹配。

### A.7 读档与恢复

调整 `OnSessionLaunched()` 的相关顺序，先重建/验证批次索引、校正可安全修复的账目，然后进行最终验证。历史 `InvariantMismatch` 或 `ServicePartyValidationFailed` 只能在结构、绑定、账目均验证通过且排除真实事务损坏后自动解除。`PopulationRollbackFailed`、`ReconciliationRollbackUnverified`、`ReturnRollbackUnverified` 等须保留真实保护。不能改变 Saveable ID 或同步键。

### A.8 验收

- 50/50 战损1名→49/49，不冻结；战损10名→40/40，不冻结。
- 49/50 且 Party49名→合法修为49/49，不返还 Hearth。
- 50/49 且 Party49名→结算后49/49，不重复释放。
- 50/49 且 Party50名→不能伪造人口，不删除士兵，清晰记录未证实赤字。
- 模板过滤后49、原始批次50→不误判为动员冲突。
- 重复 BatchId、损坏 Party 引用、回滚不可验证→仍保护。
- 战后即时对账、保存/读档、连续每日 Tick 均无重复结算。

---

## 三、Worker B：封邑兵源排他性统一约束（P0）

### B.1 目标

城镇/城堡驻军与其全部附属村庄封建兵，**只对绑定的同一支领主 Party 供给**；统一约束基地预选、创建预留、真实征召、驻军转移及可能绕过主入口的批次转移。

### B.2 重点文件

```text
ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs
ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs
ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs
ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs
ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs
```

重点审查范围并不意味着必须全部修改。复用现有绑定管理，不创建第二个持久排他表。

### B.3 统一判定

提供可复用的只读判定：输入 `SourceSettlement`（村庄应归一化到所属城镇/城堡）和 `RequestingParty`，输出 `Allowed`、`BoundPartyId`、`ReservationOwner`、`ReasonCode`。

建议原因码：

```text
SourceAvailable
SourceBoundToSameParty
SourceBoundToOtherParty
SourceReservedForOtherCreation
SourceBindingInvalid
SourceHasConflictingServingParties
```

有正式绑定或尚未完成的创建预留时，不得让第二支 Party 抢占。创建基地预估必须先排除别人占用的兵源，再计算该候选对本 Party 的可用兵力；归属冲突不能伪装成模板或财政审批失败。

### B.4 最终提交二次校验

源地预选通过后，提交前重新验证所有权、请求 Party、正式绑定、创建预留、兵源排他状态，以及原有容量/财政/驻军安全底线。防止两个 Party 并发预检均通过而都成功转兵。实际征召、驻军调拨和通过封邑/村庄入口的征兵应使用同一规则，不可仅在创建流程拦截。

### B.5 在役转移、释放与历史冲突

审查 `FiefServiceBatch.SplitTo()`、`FiefPartyManager` 在役转移、`PartyExchangeService` 相关入口。禁止通过跨 Party 拆分绕过来源排他规则，但不能为此直接删除现有转移方法；须追踪真实调用点。旧存档若已存在冲突，保留在役记录并输出异常，禁止擅自销毁或重新归属。

绑定 Party 解散或释放时，先按现有生命周期完成在役结算、返乡、安置或损失处理，确认没有未决事务，才释放绑定及预留。Party 引用暂时无法解析时不得随意判定其兵力消失。

### B.6 验收

- Castle A 绑定 Party A：A 可以依法使用驻军与各附属村庄。
- Party B（同 Clan）从 A 驻军或任何附属村庄征兵：全部拒绝。
- 已创建但仍在初始化的 Party A 保留兵源，Party B 不得抢占。
- 两次建军竞争同一封邑：只能一个成功绑定。
- 原绑定 Party 合法结束并完全结算：兵源可重新分配。
- 违规跨 Party 转移：精确拒绝；旧存档保留可审计数据。

---

## 四、Worker C：AI 建军零审批诊断（P1，第一轮只增日志）

### C.1 目标

最新日志案例：雷姆托伊尔堡显示可见健康封建兵26名但批准0；萨哥特显示可见健康封建兵135名但批准0。另有新建 Party 最后只有8人、34人。必须能够仅凭日志回答：**预估多少 → 实际候选多少 → 审批多少 → 哪个规则首次归零 → 最终到账多少**。

### C.2 重点文件

```text
ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs
ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs
ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs
ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs
ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Services/UnifiedRecruitmentExecutionService.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs
```

### C.3 统一关联 ID

同一次建军贯穿 `AttemptId`、`TaskId`、`OperationId`、`PartyId`、`SettlementId`、`SourceKind`，串联：基地筛选→Party 生成→任务注册→到访→候选构造→RecruitmentModel 审批→PartyExchange 计划→执行→最终建军结果。

### C.4 候选生成日志

在 `BuildTemplateWeightedRecruitmentCandidates()` 复用本次真实候选计算结果，记录：

```text
TemplateId
TemplateRoots（ID、正权重和根数量）
ReadyTroopsTotal
ReadyHealthy / ReadyWounded
TemplateEligible
RootMatched / RootUnmatched
UnmatchedTroopIdsAndCounts
CandidateCount
WeightedAllocatedCount
```

目前 `PartyExchangeModel.LogInitialFiefApprovalFailure()` 将 `TEMPLATE_ROOTS`、`TEMPLATE_ELIGIBLE`、`UNMATCHED` 直接硬编码为 `Unavailable`；必须在能从本次实际计算中得到这些值时填入真实数值。不得重新执行带副作用的构造/审批来生成日志；确实不可用的字段须说明具体原因。

### C.5 财政、编制及最低批次日志

每个候选输出：

```text
TroopId
RequestedCount
RecruitableCount
AllowedByPartySize
AllowedByWageLimit
AllowedByRecruitmentCost
AllowedByMaintenance
PrimaryLimit
```

并记录 `Requested`、`Candidates`、`ApprovedBeforeMinimumBatch`、`MinimumFiefRecruitBatch`、`ApprovedAfterMinimumBatch`、计划名册人数、提交前重验证结果、实际转兵人数。

**不能把第一个候选的 PrimaryLimit 当成全体批准0的唯一理由。** 应统计所有候选，判定首次将正数归零的真实阶段（候选构造、Admission、最低批次、Manifest、执行前二次验证、提交）。

### C.6 兵源排他诊断

直接复用 Worker B 的统一排他判定，记录：

```text
SettlementId
RequestingPartyId
BoundPartyId
ReservationOwner
FiefSourceAllowed
GarrisonSourceAllowed
BindingRejectReason
```

来源已绑定别的 Party 时必须明确 `SourceBoundToOtherParty`，不能混称“无兵”、“模板无效”或“财政不足”。

### C.7 最终建军结果

记录：

```text
EstimatedFief / EstimatedGarrison / EstimatedCombined
RequestedFief / ApprovedFief / CommittedFief
RequestedProfessional / ApprovedProfessional / CommittedProfessional
FinalRegulars / InitialExpectedRegulars
FinalStatus / FinalReason
```

重点对 `0 < FinalRegulars < 50` 输出差额与失败原因。当前代码中的 50 是基地预计兵源门槛，不得由日志 Worker 擅自改成“所有少于50人必须销毁 Party”。

### C.8 低噪声策略

- 新建 Party 审批0、计划批准但实际到账0、最终正规兵显著低于预期：输出完整诊断。
- 成功时只保留简短摘要。
- 一个创建尝试一个终态；允许关联多阶段详情，不重复刷相同终态。
- 非玩家王国至少保留可定位的失败 Warn 摘要；详细调试可通过过滤/诊断开关控制。
- **日志只读**：不得为了诊断重复执行审批、事务、随机抽取或影响财政缓存。

### C.9 目标格式（仅示例，数值运行时真实采集）

```text
[AI Initial Recruitment]
AttemptId=... TaskId=... OperationId=...
PartyId=... SettlementId=... Source=Fief

[Source Binding]
BoundParty=... RequestedParty=... SourceAllowed=true
Reason=SourceBoundToSameParty

[Source Preview]
EstimatedFief=135 EstimatedGarrison=8
ReadyHealthy=... ReadyWounded=...

[Fief Candidates]
Template=... RootCount=...
TemplateEligible=... RootMatched=... RootUnmatched=...
Offered=...

[Recruitment Admission]
Requested=... ApprovedBeforeMinBatch=...
MinimumBatch=25 ApprovedAfterMinBatch=0

[Limits]
PartySizeAllowed=... WageAllowed=... CostAllowed=...
MaintenanceAllowed=... PrimaryReasons=...

[Exchange]
Planned=... Committed=... Reason=NoApprovedTroops

[Final]
FinalRegulars=... FirstZeroStage=... FirstZeroReason=...
```

---

## 五、Workers 分工与合并顺序

| 顺序 | Worker | 任务 | 交付 |
|---|---|---|---|
| 1 | A | 在役账目与战损修复 | 合法战损/安全校正测试 |
| 2 | B | 封邑兵源排他 | 所有兵源入口统一绑定检查 |
| 3 | C | AI 零审批诊断 | 真实拒绝阶段和字段可定位 |
| 4 | 集成 | 合并、编译、端到端验收 | 发布门禁和实际游戏待验项 |

A、B 可能同时涉及 `FiefPartyManager.cs`；B、C 可能同时涉及建军与征召文件。建议独立分支、按顺序合并，避免覆盖冲突。B 提供的排他判定由 C 直接复用。

### 禁止事项

**Worker A：** 不改变 AI 建军目标/评分；不新建绑定表；不改安置；不删除恢复保护；不伪造 Hearth。

**Worker B：** 不重写匹配算法；不改兵源生产、工资、最低征召批次；不删历史批次；不在附属村庄分别建立排他绑定。

**Worker C：** 首轮只改日志，不放宽审批、不改最低批次、不重复执行审批、不修改50人的含义。

## 六、端到端验收矩阵

| ID | 场景 | 预期 |
|---|---|---|
| T01 | 50名在役战损1人 | 49/49，无冻结、无人口返还 |
| T02 | 比约伦比尔50/49 | 可安全修正则自动恢复，不重复报错 |
| T03 | 同一封邑驻军与村庄供 Party A | 正常批准与执行 |
| T04 | Party B 申请该封邑任意兵源 | 明确拒绝，日志显示绑定 Party |
| T05 | Party A 完整结算并释放绑定 | Party B 可重新合法绑定 |
| T06 | 可见健康26名封建兵、批准0 | 日志指出实际归零阶段 |
| T07 | 可见健康135名封建兵、批准0 | 日志明确模板、财务或真实拒绝原因 |
| T08 | 最终仅8名/34名正规兵 | 日志显示预估与实际差额 |
| T09 | 正常封建兵返乡及额外安置 | 在役保护、冷却、供养不回归 |
| T10 | 历史冻结旧存档 | 安全可修复状态恢复；真实事务损坏保留保护 |
| T11 | 重复日 Tick / 保存读档 | 不重复征兵、扣 Hearth、释放标记 |
| T12 | 竞争创建/预留同一封邑 | 最多一支 Party 获得兵源 |

## 七、编译、架构与交付

执行项目既有验证脚本（先核实当前路径）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File ModifiedArmy/tools/Invoke-ReleaseValidation.ps1
```

同时检查：

- ModifiedArmy / ModifiedPolitics Release 编译通过。
- 架构发布门禁通过，Army → Politics 无新增反向依赖。
- `git diff --check` 通过。
- 没有新增不必要的 Saveable 字段/类型/SyncData 键。
- 玩家征兵、返乡、安置与 AI 调兵无回归。

各 Worker 最终报告应包括：修改文件与关键方法、修复原因、日志字段/位置、自动测试结果、构建/架构结果、游戏内待验证事项和真实 Commit SHA。

**编译成功不代表游戏内验证成功；未实际执行的测试必须标注 BLOCKED。**

---

**最终施工原则：先保证在役数据可靠，再确保封邑兵源排他，最后使 AI 建军零审批在日志中可被直接定位。**
