# W6：大型 Behavior 拆分与领域边界

## 核心原则

**不能用 `partial` 拆文件冒充拆架构**。当前 `SupplyPartyBehavior` 被拆为 `SupplyPartyBehavior.cs`（约 1670 行）、`SupplyPartyProvisioning.cs`（约 791 行）和 `SupplyPartyTransaction.cs`（约 555 行），但三者的实际 C# 类型都是 `partial class SupplyPartyBehavior`，共享内部字段和私有函数。这个机制是合理的临时分包，但不等于独立服务。

重构重点应是：`Behavior` 只处理原版 Campaign 事件和保存钩子；Model 负责纯计算；Service 控制提交和回滚；状态对象为任务记录；UI/日志委托独立格式化器。**不改变已发布行为**，尤其不重写刚完成的征粮补给和路线兜底算法。

## 目标结构建议

```text
ForceManagement/
  Behaviors/AiForcePlanningBehavior.cs      # 事件转发、存档调用
  Planning/PartyForceTargetModel.cs
  Planning/PartyForceCandidateService.cs
  Missions/PartyForceTaskCoordinator.cs     # 计划执行、任务状态流转
  Missions/PartyForceTaskPersistence.cs     # 持久化适配，不另注册重复键
  Transfers/PartyExchangeModel.cs
  Transfers/PartyExchangeService.cs
  Policies/PartyTransferOwnershipPolicy.cs

Garrison/Supply/
  Behaviors/SupplyPartyBehavior.cs          # 保留游戏接入与 SyncData
  Models/SupplyProvisionModel.cs            # 估算行程/耗粮/配额，无实际扣粮
  Services/SupplyDispatchService.cs         # 本地或跨城派遣计划
  Services/SupplyRouteController.cs         # 路线所有权与敌情暂停
  Services/SupplyCargoService.cs            # 采购、装载、交付、回收
  Services/SupplyRecoveryService.cs         # 失败/被摧毁/读档恢复
  Data/SupplyPartyComponent.cs              # 现有 Saveable 类型，慎重移动

Fief/
  Behaviors/FiefPartyManager.cs             # 封邑全局入口
  Services/FiefManpowerLedger.cs            # 动员户数守恒
  Services/FiefServiceLedger.cs             # 批次身份
  Services/FiefServiceReconciler.cs         # 在役批次和真实名册核对
  Services/FiefReturnService.cs             # 实际返回与结果
  Models/PartyData/*                        # 保留现有领域对象

Recruitment/
  Behaviors/GarrisonRecruitFromPrisonersBehavior.cs
  Services/ProfessionalRecruitmentService.cs
  Services/PrisonerGarrisonRecruitmentService.cs
  Models/*                                  # 保留现有模型契约
  Pools/Models/*                            # 职业兵候选与生产台账
```

命名只是建议；优先保留命名空间和游戏存档类型的身份。不强求空目录全部落地。

## W6-A：征粮系统（优先在现有修复稳定后）

### 现有实现分工及目标

| 现有代码 | 应移交的职责 | 重构边界 |
|---|---|---|
| `SupplyPartyBehavior.OnDailySettlementTick` | 派遣候选选择与冷却 | `SupplyDispatchService` 负责决策；Behavior 收事件 |
| `SupplyPartyBehavior.OnHourlyPartyTick` | 航路任务推进与威胁暂停 | `SupplyRouteController` 负责路线；不能越过原版逃跑状态强制移动 |
| `SupplyPartyProvisioning.TryLoadOutboundProvisions` | 路程、食物需求、粮仓保留量、承载上限 | 先纯 `SupplyProvisionModel.Plan`，再 `SupplyCargoService.CommitLoad` |
| `SupplyPartyProvisioning.LogDailySupplyHealth` | 伤员/断粮诊断 | `SupplyDiagnostics`（不改变日志含义和等级） |
| `SupplyPartyProvisioning.HandleLocalMissionFoodRisk` | 粮食风险调度 | 分成风险决策（Model）和路线切换（Service） |
| `SupplyPartyTransaction` | 市场购买、物品转移、牲畜与金币 | `SupplyCargoService` 执行真实资源变更 |
| `SupplyPartyBehavior.OnGameLoadFinished` | 恢复任务、原版 AI 状态 | `SupplyRecoveryService` 协调，保留事件入口 |

### 需保留的最新修复

- 新增 `TryLoadOutboundProvisions`：根据路线、速度、消耗、备用天数计算出发口粮，检查不足则拒绝派遣并回滚。
- 新增 `CanActuallyDispatchLocalForaging` / `HasActiveLocalForagingMission`：本地征粮不可出发时不得错误阻止跨城援粮。
- 新增路线变化/危险/健康记录、`HandleLocalMissionFoodRisk`。保留“遇敌时暂不重申路线，威胁解除后恢复”的原则。
- `SupplyPartyComponent` 的 SaveableProperty ID 和 `SupplyPartyMissionState` 的数值不变。

### 拆分顺序

1. 先将当前 public/private 方法及字段列成表，标明副作用与调用事件。
2. 提炼 `SupplyProvisionModel` 为无副作用输入/输出；计算结果与原实现逐项一致。
3. 提炼 Cargo 操作：承载计算、转物品/金币、回滚；不得重复转移或吞掉回滚异常。
4. 提炼 Route 控制：保持原版 AI 避敌优先级与任务重申间的原有行为；只用日志证明无折返和威胁处理不变。
5. 最后再让 `SupplyPartyBehavior` 保留事件转发、`SyncData` 和极少协调方法。
6. 未经特别验证不拆 `SupplyPartyComponent` 存档类型、不改移动部队 ID 规则。

## W6-B：兵力规划 Behavior

`AiForcePlanningBehavior`（约 788 行）同时持有任务集合、序列化、读档重建、每日规划、任务到达执行、临时捐兵、攻占捐兵、战时休整和日志。

- `PartyForceTaskCoordinator` 管 `EvaluateParty`、`ApplyLivePlan`、执行结果与任务状态；通过 W2 统一事务结果。
- `TaskPersistenceAdapter` 专管 `_savedTaskRecords` 与旧键兼容，**不改变 SyncData 键格式**。
- `PartyVisitExecutionService` 协调到达后 Fief/Garrison/Mercenary 来源执行，不再把具体资源操作放 Behavior 中。
- `ForceManagementFacade` 只暴露业务入口，而不是允许各业务随意修改内部任务集合。
- `PartyExchangeBehavior` 的自动事件触发与 `AiForcePlanningBehavior` 的主动任务处理需明确定义谁拥有同一访问操作，避免重复触发。

## W6-C：封邑管理器

`FiefPartyManager`（约 1055 行）管理 `_fiefDataMap`、`_mobilizedHearth`、 `_mobilizationOwners`、`_legacyUntrackedMobilization`、`_servingDetachmentIndex`，且承载 `FiefSaveDefiner`。建议保留 Manager 原有 `SyncData`、SaveDefiner 类型与键作为外部入口，先内部委托真实 Ledger/Service。

- 动员户数台账必须保证“动员时占用，合法归还时释放”，而不是仅根据当前兵种名推断。
- 服务批次保持同源封邑、原始 `BatchId`、服役日数、免费工资日数等字段语义；读档后由 Reconciler 重建运行时索引。
- 不把真实士兵名册复制成第二份权威人数；长期批次记录与 `MemberRoster` 对账。
- 保存 `FiefSaveDefiner` 的注册编号、容器定义以及 `FiefServiceBatch/FiefReturnBatch` 的类型身份。

## W6-D：驻军招募及生产

- `GarrisonRecruitFromPrisonersBehavior`（约 994 行）把每日招募、候选和限制、七日预测、驻军周清理区分开；可由 Behavior 调用 `ProfessionalRecruitmentService` 与 `PrisonerGarrisonRecruitmentService`。
- `SettlementManpowerProductionBehavior`（约 855 行）已维护职业兵生产台账和候选；拆“预测”和“提交”，保留 `TryConsumeProfessionalOffer` 的原语义与台账签名。
- 预测必须真正纯读取，不能前置消耗兵源/户数/金币；提交后才修改真实资源。

## 验收

1. 以同一存档、同一连续多日模拟对比：职业兵生成、封邑补员、AI 调拨、征粮派遣数量不变。
2. `CampaignEvents` 不得因为拆分类被重复订阅，按原本时序执行一次。
3. 兵员、伤兵、粮食、金币、牲畜、封邑动员户数守恒；可正常读档恢复。
4. 保留各 Saveable 类型 ID、字段 ID、SyncData 键及旧 DTO 格式。
5. 任意一个拆分 PR 无法独立编译/测回时不合并下一类。
