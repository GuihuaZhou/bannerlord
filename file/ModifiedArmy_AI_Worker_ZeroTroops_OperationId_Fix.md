# ModifiedArmy AI Worker 施工任务：修复零兵力建军与补员事务 ID 冲突

> 源码仓库：<https://github.com/GuihuaZhou/bannerlord>  
> 参考 `main` 提交：`4e599898dac0428c0ad444bb9b35479c2512c883`（2026-11-07）  
> 模块：`ModifiedArmy`  
> 关联最新日志：`ModifiedArmy(20261107-093226).log`  
> 优先级：零兵力建军 P0；事务 ID 冲突 P1  
> 本文是交付给 **AI Worker 的实际编码任务**，不是已完成代码修改的声明。

## 1. 目标与约束

必须完成两个问题的源码修复：

1. 领主部队创建报告 `Completed`，但实际没有获得任何士兵。
2. 初始封邑兵征召和后续日常补员使用相同 `OperationId`，第二次合法征兵被 `OperationAlreadyCompleted` 拦截。

对于目前无法从日志唯一确定的 `NoApprovedTroops`，通过**分阶段、结构化、可对账的诊断日志**定位；在证据不足前不修改招募规则。

**必须执行代码修改、构建验证并报告结果，不能只写设计或调查文档。** 保留原有 Model/Service/Behavior 分工、事务提交和恢复保护；不要凭空产生士兵，不要关闭幂等保护，也不要擅自调整文化、工资、经济、生产和最低批次配置。

## 2. 已确认的代码根因

### 2.1 OperationId 冲突

涉及代码：

- `ModifiedArmy/ModifiedArmy/ForceManagement/Models/AiForcePlanningModel.cs`：`CreateTask()` 使用 `(context.CurrentTask?.Version ?? 0) + 1`。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs`：`FindTask()` 排除已完成与已取消任务；`PruneTerminalTasks()` 删除终态任务。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs`：`Execute()` 采用 `force:{party.StringId}:{task.CreatedDay}:{task.Version}:{task.Intent}` 生成事务 ID。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/TransferOperationJournalBehavior.cs`：已提交操作 ID 的去重逻辑正常发挥作用，**不能删除**。

当初始征兵于同一游戏日完成后，`FindTask()` 不再返回它；日常规划从 `Version=1` 开始创建新的 `Recruit` 任务，产生相同操作 ID。例如日志中的：

```text
force:lord_1_22_party_1:91412:1:Recruit
```

同日初始封邑招募已提交 39 人，后续职业兵计划批准 13 人，却因同一 ID 被判定 `OperationAlreadyCompleted`，实际转移 0 人。

### 2.2 建军完成条件未校验真实士兵到账

- `ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs` 的 `TryCreateManagedLordParty()` 在 `SpawnLordParty`、绑定、`TryRegisterInitialRecruitTask` 后，调用 `EnterSettlementAction.ApplyForParty()`，随后**不检查实际征兵数量**就记录 `Completed` 并返回。
- `ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs` 的 `FindEligibleBase()` 用的是**创建基地预估可用兵源 >= 50**，不是“初始化后必须实际得到 50 名士兵”的已实现规则。不得混淆。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Services/UnifiedRecruitmentExecutionService.cs` 在实际招募时使用 `GetPartyTarget(party).GapCount`，并会走实时审批流程。

案例：雷索斯堡预估有约 58 名封邑兵，但波法利奥斯初始申请 31 人、阿法倪斯初始申请 26 人，均批准 0、实际转移 0，却仍记录 `Completed`。

### 2.3 `NoApprovedTroops` 尚不能唯一定位

预检与实际征兵的口径不同：

- `PartyCreationSettlementModel.GetEligibleFiefRegulars()` 统计符合模板且 `Number > 0` 的就绪兵。
- `Fief/Models/PartyData/FiefPartyRecruitment.cs` 的 `BuildTemplateWeightedRecruitmentCandidates()` 还只纳入**健康兵**，并要求匹配正权重模板根兵种。
- `PreviewFiefRecruitmentApprovals()` 使用 AI 审批模型并检查 `MinimumFiefRecruitBatch=25`；实际批准为 1～24 人时可能归零。
- `Recruitment/Models/DefaultAIRecruitmentModel.cs` 对目标人数、容量、工资、购买成本、长期维持经费等进行二次约束。
- `ForceManagement/Models/PartyExchangeModel.cs` 的 `BuildFiefMobilizationPlan()` 最终构建实际审批名册，空名册经 `Finish()` 返回 `NoApprovedTroops`。

**不得仅因缺少 `AI fief weighted candidates` 日志就断言没有生成候选：那条日志在正式招募方法中，不覆盖所有预览审批路径。**

## 3. 工作准备

1. 在本地工作目录 `D:/workspace/bannerlord` 执行 `git status`、`git rev-parse HEAD`，记录工作树与当前提交。
2. 检查当前代码是否晚于参考提交；**以用户实际工作树和待部署构建为准**，不要盲目套用旧行号。
3. 保留用户已有未提交修改；禁止 `git reset --hard` 等破坏性命令。
4. 建议先做范围收敛的修改，按阶段验收，再进行其他阶段。
5. 未经用户要求，不要修改 ModifiedPolitics 与无关模块，不进行全项目随意格式化。

## 4. 任务 A：修复 OperationId 冲突（必须直接修复）

### 重点文件

```text
ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/UnifiedForceContracts.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Models/AiForcePlanningModel.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskPersistence.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/TransferOperationJournalBehavior.cs
```

### 施工要求

1. 每个**独立业务任务**拥有稳定唯一的 `TaskId`，初始任务和日常任务各自不同。
2. 同一个任务被同日重复调用、定居点再次进入或读档恢复时，保持相同 `TaskId`。
3. `OperationId` 按 `TaskId + 稳定执行步骤` 生成，确保不同任务不同、同任务同一步骤相同。多来源补员需对不同执行步骤使用不同 ID。
4. `TaskId` 应由任务管理/协调层分配并纳入持久化，不应让纯规划 Model 随机生成产生副作用的身份。
5. `PartyForceTaskPersistence` 目前保存九段分隔的任务记录。新格式应兼容旧九段记录的读取；保留现有 `SyncData` 键与 Saveable ID。
6. 对**旧存档中尚未完成的任务**，必须设计确定性的旧 OperationId 兼容方案，避免其在恢复后重新提交一笔已经成功的事务；不能简单为所有旧任务随机赋新 ID 后直接执行。
7. 保留 `TransferOperationJournalBehavior` 对 `Committed`、`PartiallyCommitted`、`RecoveryRequired` 的保护；冻结域、安全恢复流程不弱化。
8. `OperationAlreadyCompleted` 应继续拦截**完全相同业务执行步骤的重复提交**，不能拦截新任务。
9. 对计划版本的生成和 `FindTask` 终态过滤进行交叉核查，但不要把单纯增大 `Version` 当成唯一修复；任务身份需要可持久化。

### 验收

- 同一游戏日两项不同招募任务的 OperationId 不同。
- 同一任务重复触发 OperationId 不变，第二次不能重复扣兵。
- 存档/读档后新格式 TaskId 不变。
- 旧格式记录可读取，已提交事务不被错误重放。
- 初始征兵提交成功后，当日日常职业兵补员可以通过自己的独立事务正常审批执行。

## 5. 任务 B：修复零兵力建军的业务完成条件

### 重点文件

```text
ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs
ModifiedArmy/ModifiedArmy/PartyManagement/Services/PartyCreationCoordinator.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/PartyExchangeBehavior.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Services/UnifiedRecruitmentExecutionService.cs
```

### 施工要求

1. 确认 `SpawnLordParty`、`EnterSettlementAction` 与 `SettlementEntered` 的真实同步时序；不要假定所有路径都同步提交。
2. 区分“MobileParty 已生成”与“初始编组已完成”的状态与日志。
3. 用真实非英雄正规兵数量及征兵事务结果判定建军最终是否成功。**实际取得 0 人不得记录为成功组建。**
4. 当前 `minimumRegulars=50` 是基地预估兵源阈值；Worker 应从现有设计/实现厘清实际最小建军兵力，不得擅自改成“未达到 50 人立即销毁”。
5. 首选封邑兵审批失败时，评估是否能沿现有合法审批路径申请基地驻军职业兵；不允许绕开驻军防御底线、工资、财政或服役账本。
6. 若使用两种来源分别提交，应保证事务步骤标识不同，且不会绕过同日访问保护；设计明确的初始化执行顺序和原子/部分成功边界。
7. 若无实际可用兵源，输出真实的创建失败原因，安全取消任务、释放预留与绑定，使用游戏支持的生命周期清理方式处理空队伍。
8. **不要在 SettlementEntered 事件仍在迭代/处理 Party 时冒险同步销毁 Party。** 如需延期安全清理，需有明确的失败暂存状态和下一安全时点处理流程。
9. 若已提交部分士兵转移，清理时必须一致地处理封邑在役批次、Hearth 占用、驻军来源名册与目标名册，防止兵力丢失或重复。
10. 成功日志包含 `attemptId`、初始目标、实际招募总人数、各来源实际招募数、最终 Regulars、最终状态与原因码。

### 验收

- 预估 58 名封邑兵但审批 0 的场景，不能继续报告 `Completed`。
- 封邑 0 人而职业驻军可安全补足的场景，根据已批准真实结果完成组建。
- 所有来源 0 人时，进入明确失败/安全清理状态，不长期遗留占用绑定名额的空部队。
- 部分成功时不出现重复提交、士兵丢失、封邑服役记录错位。

## 6. 任务 C：只读分阶段日志诊断 `NoApprovedTroops`

### 重点文件

```text
ModifiedArmy/ModifiedArmy/PartyManagement/Models/PartyCreationSettlementModel.cs
ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs
ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs
ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs
ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs
```

### 日志字段

只在初始建军失败、`NoApprovedTroops` 或最终批准数不足最低批次时输出详细诊断，至少包含：

1. 关联 ID：`AttemptId`、`PartyId`、`TaskId`、`OperationId`、`SettlementId`。
2. 封邑模板、正权重根兵种与根数量。
3. 源名册总数、模板合法数、健康数、伤员数。
4. 可以映射到模板根的兵种与人数；无法映射的兵种 ID 与人数。
5. 申请数、候选数、权重分配后的各兵种计划数。
6. 各候选 `RequestedCount`、`RecruitableCount`、`PrimaryLimit`。
7. 容量、工资上限、招募费用及长期维持资金的数量约束。
8. `MinimumFiefRecruitBatch` 判断前/后的批准总数。
9. 第一个真正导致从正数变为零的阶段及原因。
10. `PlanReason`、`ResultReason`、最终批准与实际提交人数、名册指纹。

示例（问号表示必须在运行时读取真实值，而非杜撰）：

```text
[InitialRecruitApproval]
attempt=...
party=lord_NE9_l_party_1
settlement=castle_EN3
source=Fief
rawReady=58
templateEligible=58
healthy=?
wounded=?
weightedMatched=?
weightedUnmatched=?
candidateOffered=?
approvedBeforeMinBatch=?
minimumBatch=25
approvedAfterMinBatch=0
rejectionStage=?
rejectionReason=?
```

严格要求：

- 诊断只读，不调用有副作用的审批提交方法重新计算；复用本次真实中间结果。
- 诊断字段可以与实际输入名册对账，不重复计算重叠排除数。
- 不能获得的字段用 `Unavailable`，不能猜测。
- 保持低噪声，避免所有 AI 队伍每日逐人刷屏。
- 若发现规则缺陷，需有真实代码证据和回归测试后才修复；否则只提交诊断，不改数值与兵种过滤。

## 7. 测试矩阵

| 场景 | 预期 |
|---|---|
| 同日初始封邑招募成功，再次规划职业补员 | 两个 OperationId 不同，第二次可正常提交 |
| 同一个任务重复定居点访问 | 不重复提交，也不重复扣兵 |
| 新存档保存并恢复待执行任务 | TaskId 保持不变 |
| 旧九段格式任务恢复 | 可读取，并保留旧幂等语义 |
| 初始封邑审批 0，职业来源可用 | 合规补员，不将审批 0 伪报成功 |
| 初始封邑和职业来源均审批 0 | 失败日志准确，绑定/任务安全清理 |
| 部分士兵到账后发生初始化失败 | 账本、名册与资金状态可恢复、无重复或丢兵 |
| 正常封邑兵升级、返乡与服役记录 | 不新增 mismatch |
| 玩家手动征召、军团、征粮队 | 行为不变 |

## 8. 自动验证与交付

执行项目既有验证流程（如果本地路径已变化，以仓库实际脚本为准）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ModifiedArmy/tools/Invoke-ReleaseValidation.ps1
```

同时检查：

- `ModifiedArmy` Release 编译。
- 相关 `ModifiedPolitics` 依赖与架构门禁。
- `git diff --check`。
- 无 Army → Politics 反向依赖。
- 没有新增第二个实际调兵入口。
- 未改动存档键和旧 Saveable ID。

最终提交报告至少包含：

1. 两个已确认根因和具体代码行。
2. 修改文件清单、关键 Diff 与原因码。
3. 失败阶段日志的真实样例（或明确标为游戏内尚未验证）。
4. 自动测试与 Release 构建结果，分别写明 PASS、FAIL、BLOCKED。
5. 可复现步骤与游戏内待验清单。
6. 真实 Commit SHA。
7. `ModifiedArmy.dll` 实际构建产物路径。

## 9. 实施顺序

1. **A：先修任务身份与幂等键**，保证同日连续补员安全。
2. **B：再修建军结果验收及失败清理**，避免空队伍虚假成功和绑定长期占用。
3. **C：对未知审批环节补齐日志**，等待下一份游戏内复现日志确认实际拒绝分支。
4. 完成验证并交付可部署构建；不得报告未执行的游戏内测试为已通过。
