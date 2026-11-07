# ModifiedArmy — Party 创建、AI 补员诊断与征粮队生存机制施工方案

**仓库**：`GuihuaZhou/bannerlord`  
**目标模块**：`ModifiedArmy`  
**代码基线**：`main @ 3c31aea436054552396d22f9fe782e3b4c736519`（2026-11-03）  
**任务性质**：问题修复 + 可观测性完善  
**执行要求**：分阶段实施、逐阶段验证、禁止无关重构

> 本方案根据上述 GitHub 提交中的代码整理；Worker 必须以本地最新代码、`git status` 和仓库规范复核后施工。本文不是已实施或已验证报告。

## 一、施工总原则

Worker 开工前必须：

1. 读取仓库根目录 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`。
2. 检查 `git status --short`、当前分支及 HEAD，保护已有未提交修改。远端代码不代表本地工作区相同。
3. 遵循现有分层：Model 负责判断，Service 负责执行，Behavior 负责事件与持久化，Coordinator 负责协调，Patch 只做最小适配。
4. 不引入第二套兵源、士兵、粮食、金币、任务或驻军编制账本。
5. 保持 Harmony 行为、存档 ID、`SyncData` 键、跨模块依赖方向以及原版 AI 避险决策权。
6. 修改前记录各资源写入顺序、失败补偿方法及读档恢复方式。

**本次不修改**：军事制度权重、AI 理想兵力计算、封建兵供养规则、职业兵生产速度、驻军基础编制和无关经济模型。

## 二、阶段 P1：Party 创建全流程日志

### 2.1 当前代码入口

- `ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs`
- `ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs`
- `ModifiedArmy/ModifiedArmy/PartyManagement/Diagnostics/PartyCreationDiagnostics.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Patches/HeroSpawnSettlementPatch.cs`
- `ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Transport/ReinforcementTransferService.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/Services/SupplyDispatchService.cs`

另外检查 `ModifiedPolitics/ModifiedPolitics/Governor/Patches/GovernorWarPartyPatch.cs`：该文件也给 `HeroSpawnCampaignBehavior.SpawnLordParty` 设置 Prefix，可能因领主为总督而提前拒绝创建。核实 Harmony 执行顺序，不得把跨模块拒绝误报为 ModifiedArmy 决策。

### 2.2 已确认的日志缺口

- `PartyCreationCoordinator.TryCreateManagedLordParty()` 对不符合 `ManagedPartyPolicy` 的情况直接返回。
- `FindEligibleBase()` 失败时只有笼统的 `NoEligibleCreationBase`。
- `SpawnLordParty()` 返回 `null` 时没有明确失败日志。
- `TryRegisterInitialRecruitTask()` 返回 `false` 时没有细粒度拒绝原因。
- `PartyCreationSettlementModel.FindEligibleBase()` 过滤候选封邑时不保留每个候选失败原因。
- `SupplyDispatchService.EvaluateDaily()` 对资格不符、已有任务、冷却等条件直接退出。
- `ReinforcementTransferService.TryCreate()` 存在返回 `null` 而缺少拒绝原因的路径。

### 2.3 实施要求

给 Mod 实际负责创建的 Party 统一生命周期诊断，覆盖 LordParty、LocalForaging SupplyParty、CrossSettlementAid SupplyParty、ReinforcementParty。

结构化字段：`AttemptId / PartyId / PartyType / ClanId / LeaderId / SourceSettlementId / DestinationSettlementId / Phase / ReasonCode`。

每次真实创建尝试必须有一个最终状态：`Created`、`Rejected`、`Failed` 或 `RolledBack`。在创建入口前的资格评估也必须有拒绝原因；避免调用方过滤后“未尝试创建”而完全没有记录。

建议原因码（按真实代码路径落地，不得伪造）：

| ReasonCode | 含义 |
| --- | --- |
| `NoEligibleClanSettlement` | 没有可用家族封邑 |
| `SettlementReservationRejected` | 封邑绑定或创建预留失败 |
| `InsufficientCombinedTroops` | 驻军与封邑兵合计不足 |
| `InsufficientHealthyEscort` | 健康护卫不足 |
| `InsufficientTravelProvisions` | 出发口粮不足 |
| `InsufficientCargoCapacity` | 运输容量不足 |
| `SpawnReturnedNull` | 原版 Party 创建返回 null |
| `InitialRecruitTaskRejected` | 初始补员任务创建失败 |
| `CreationException` | 创建期间发生异常 |

对候选封邑记录 `GarrisonAvailable / FiefAvailable / MinimumRequired / MinimumFiefBatch / BindingEligible`；不得修改原有候选排序与选择结果。

创建成功、失败、回滚输出 `Info/Warn/Error`。高频每日检查使用 `Debug` 并按游戏日、定居点、原因汇总。保留玩家王国过滤，必要时增设指定 Party/Clan 诊断开关，不要全图刷屏。

**验收**：只看日志即可知道谁尝试创建何种 Party、在哪里创建、因何成功或失败。

## 三、阶段 P2：AI 补员全过程日志

### 3.1 真实代码入口

- `ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/AiForcePlanningBehavior.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Models/AiForcePlanningModel.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs`

已有任务状态、原因码、执行记录和 `OperationId`，要扩展原实现，不建立平行任务系统。

### 3.2 补员决策诊断

对实际进入评估的 AI Party 记录：当前健康/总兵力、目标兵力、缺口、战略行动、更高优先级 AI 行动保护状态、需求评分与阈值、随机门槛、候选兵源数量、决策结果与原因。

复用已有原因：`ForceTargetSatisfied`、`RecruitmentNeedBelowThreshold`、`RecruitmentRandomGateClosed`、`HigherPriorityOrder`、`NoFeasibleRecruitmentSource` 等。

**不要把“目标兵力已满足”误判为补员失败。**

### 3.3 补员任务生命周期日志

在 `PartyForceTaskCoordinator` 中记录状态转换：

| 事件 | 日志内容 |
| --- | --- |
| `TaskCreated` | Party、任务版本、兵源、目标定居点 |
| `TaskContinued` | 延续原任务原因 |
| `TaskReplaced` | 原任务、新任务及替换原因 |
| `TaskRejected` | 创建失败的具体判断 |
| `TaskSuspended` | 暂停原因及恢复域冻结状态 |
| `TaskCancelled` | 取消原因 |
| `TaskCompleted` | 实际调兵结果 |

用 `PartyId + CreatedDay + Version` 关联任务，不随意更改持久化格式。`TryRegisterInitialRecruitTask()` 各个 `false` 退出分支必须能诊断。`ApplyLivePlan()` 应在真正提交/替换任务后记录成功，而不是仅根据 Model 生成计划就声称创建成功。

### 3.4 实际调兵日志

在 `PartyVisitExecutionService.Execute()` 和 `UnifiedRecruitmentExecutionService.Execute()` 的现有边界补齐早退日志（尤其 `RecruitmentSettlementBindingInvalid`）。

通用字段：`OperationId / SourceKind / RequestedCount / ApprovedCount / TransferredCount / ReasonCode / Status`。

驻军取兵还应记录：驻军原人数、最低保留/预留人数、可调人数、驻军调兵后人数、领主调兵前后人数。

**任务创建成功 ≠ 到达目标 ≠ 实际调兵成功。** 不得破坏 `TransferOperationJournalBehavior` 的恢复与幂等机制；诊断代码不可进行资源写入。

## 四、阶段 P3：征粮队出发口粮及途中补给

### 4.1 真实现状

- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyProvisioning.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/Models/SupplyProvisionModel.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/Services/SupplyCargoService.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/Services/SupplyRouteController.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyTransaction.cs`

当前已有 `OutboundProvisionDays = 5`、`ProvisionSafetyDays = 2`、动态往返路程/日消耗规划、`HandleLocalMissionFoodRisk()` 和实际市场购粮路径；不得重新造粮食系统。

### 4.2 出发粮食规则（第一版建议值）

| 参数 | 初始值 |
| --- | --- |
| 最低出发口粮 | 20 天 |
| 普通补给触发 | 剩余不足 10 天 |
| 紧急缺粮阈值 | 剩余不足 3 天 |
| 路程安全缓冲 | 8 天 |

计算：

```
RequiredDays = max(20, EstimatedRoundTripDays + 8)
RequiredFood = ceil(DailyFoodDemand * RequiredDays)
```

`DailyFoodDemand` 依现有 `party.FoodChange`，无有效值时沿用士兵人数及食物模型回退。不得把 20 天写成固定 20 件；不得创造免费粮食。粮仓、市场、金币和载重不足时必须记录拒绝原因；若新增出发地市场购粮，需要先确认真实货物扣减、资金支付与回滚序列。

### 4.3 避险与缺粮处理顺序

当前 `OnHourlyPartyTick()` 在 `ShouldPauseForThreat()` 后才运行本地征粮 `HandleLocalMissionFoodRisk()`，导致原版避险活跃时缺粮风险处理长期跳过。

调整为：

1. 每小时先进行**只读**的健康、食物、任务安全评估；识别并记录缺粮/重伤风险。
2. 原版 AI 正在避险时不强制覆盖逃跑路线。
3. 避险结束后依任务状态决定补粮、返航、休整。
4. 避险途中进入友方安全定居点，也可以执行合法补给与伤兵安置。

不能为了解决缺粮重新引入“每小时反复覆盖移动命令”。

### 4.4 友方定居点补给

扩展 `OnSettlementEntered()`：除了原始目标和所属封邑，识别合法安全补给点。友方城镇使用实际市场购粮，按权限选择付款者，并记录购买件数、金币及补给后可用天数。同家族城堡可在保护驻军最低保留量后从粮仓调粮。禁止无偿挪用其他家族物资。

### 4.5 运粮与口粮守恒

当前实际货物都位于 `ItemRoster`，第一版不增加独立粮食账本。任务交付时动态扣除返程口粮保留量。途中吃掉的粮食不能再次作为已交付粮食计算。跨城援助任务要回归测试，不能破坏原本的交付规则。

## 五、阶段 P4：大量伤兵与紧急安置

### 5.1 现有可复用实现

- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyComponent.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyMissionState.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/Services/SupplyRecoveryService.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/Services/SupplyCargoService.cs`

当前 `SupplyCargoService.TransferTroopsLimited()` 会保留 `WoundedNumber`；`SupplyRecoveryService.CompleteReturn()` 已负责护卫回到驻军及销毁临时 Party。复用现有资源转移入口。

注意：已有 `Recovering = 5` 用于援助任务恢复，不可重定义为伤兵状态。

### 5.2 健康护卫人数阈值（50人标准队伍）

| 健康人数 | 处理 |
| --- | --- |
| ≥40 | 正常执行 |
| 25–39 | 避险并优先补给 |
| 10–24 | 终止征粮，寻找安全休整地点 |
| ≤9 | 紧急安置，禁止普通征粮 |

使用健康人数及健康比例，不可只看 `TotalManCount`。测试不同护卫规模和已经损失部分士兵的情况。

### 5.3 已到所属城镇

健康护卫不足 10 人时：终止任务、结算实际货物、按伤兵状态归还驻军、释放现有护卫预留编制、完成任务并销毁临时 Party。例：50 人全部受伤，归驻军 50 人（其中受伤 50 人），凭空治愈 0 人。

### 5.4 已到其他友方城镇

停止征粮、合法补粮，保留人员与归属，在安全地点休整。具备足够健康人数和返程口粮后安全返回原所属驻军再终止任务。不得把士兵永久转给其他家族，不能直接销毁携带伤兵和货物的 Party。若休整/补给不成功，应持续保留可恢复状态并记录原因，救援运输机制留待独立设计。

### 5.5 存档兼容

若确需新增任务状态，在枚举末尾**追加**值，保留原有数字；优先复用 `RecoverySettlement` 等持久化信息。新增 `SaveableProperty` 前核对 ID 与旧档兼容。更新 `SupplyRecoveryService.RestoreLoadedMissions()`，避免读档把休整任务当成普通返航。绝不可用任务取消或重建绕开 `RecoveryRequired`。

## 六、阶段 P5：日志规范与验收

### 6.1 统一诊断类别

`PartyCreation` / `AiRecruitmentDecision` / `ForceTaskTransition` / `ForceTransferExecution` / `SupplyProvision` / `SupplyFoodRisk` / `SupplyEmergencyRecovery`

英文稳定原因码，面向屏幕的消息走现有本地化并提供中文。`Debug` 用于高频评分和候选，`Info` 成功生命周期，`Warn` 真实拒绝或风险，`Error` 异常与需要介入的状态。按 Party、游戏日、状态、原因对不变化的重复诊断去重；不得丢失真正发生的转移与状态改变。

### 6.2 验收矩阵

| 编号 | 场景 | 预期 |
| --- | --- | --- |
| T01 | 无可用封邑创建领主 | 每个候选失败原因可查 |
| T02 | 驻军与封邑兵合计不足 50 | 拒绝创建，不损失兵员 |
| T03 | 总督被 ModifiedPolitics 拦截 | 记录真实拦截来源，不误报 |
| T04 | AI 补员随机门槛未通过 | 有原因码，无虚假成功任务 |
| T05 | AI 补员任务创建成功 | 任务标识可关联后续执行 |
| T06 | 到达驻军但无法调兵 | 记录拒绝原因和前后兵力 |
| T07 | 正常征粮出发 | 口粮满足新规则且扣粮一致 |
| T08 | 避险超过 5 天 | 安全优先，持续监测粮食风险 |
| T09 | 避险进入友方城镇 | 能按权限购买或调粮 |
| T10 | 50 人全部受伤，回所属城镇 | 50 名伤兵完整入驻军，不变健康兵 |
| T11 | 50 人全部受伤，滞留其他友方城镇 | 停止任务、人员不丢、休整正确 |
| T12 | 休整期间存档再读档 | 状态、护卫、粮食、路线正确 |
| T13 | 无法补给或创建失败 | 无凭空新增粮食、金币和士兵 |
| T14 | 回驻军时接近上限 | 护卫预留编制与伤兵转移一致 |
| T15 | 跨城援助队途中消耗粮食 | 不重复交付 |
| T16 | 原版 AI 避险 | Mod 不强制覆盖避险路线 |

资源转移中途异常需额外测试补偿失败及读档恢复。

### 6.3 编译与发布门禁

先查看参数，再运行适用的：

- `ModifiedArmy/tools/Invoke-ReleaseValidation.ps1`
- `ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1`
- `ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`
- `ModifiedArmy/tools/Test-RuntimeAcceptanceEvidence.ps1`

核验 Release 编译、架构依赖、Harmony 冲突、存档兼容。**编译 PASS 不代表游戏内验收 PASS**。每项仅报告真实 `PASS / FAIL / BLOCKED`。

## 七、Worker 施工顺序与交付

**P1 创建日志 → P2 AI 补员日志 → P3 征粮口粮和沿途补给 → P4 重伤安置 → P5 完整回归验证。**

前一阶段存在资源一致性问题时，不得掩盖并继续依赖该阶段结果。

最终实施报告包含：

1. 修改文件与方法清单；
2. 各阶段解决的问题及未实现的范围；
3. 原因码与真实日志样例；
4. 士兵、伤兵、驻军、粮食、金币的守恒检查；
5. 编译、架构与兼容性门禁结果；
6. 游戏内测试与阻塞项；
7. 最终 HEAD、未提交修改与遗留风险。

本任务不要求改变 ModifiedPolitics 军事逻辑。若发现跨模块 Harmony 冲突，应先提供真实调用顺序与影响，再实施最小兼容调整。

**最终目标**：任何一次 Party 创建失败、AI 补员失败、征粮队缺粮或重伤安置，都能从日志还原原因、任务状态和真实资源变化，且不破坏存档及原版 AI 避险。
