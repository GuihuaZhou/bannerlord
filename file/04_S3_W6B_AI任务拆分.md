# S3｜真正拆分 AI 任务与访问执行，不改变策略

## 公共约束（逐条执行）

- 仓库 `GuihuaZhou/bannerlord`，参考基线 `a3d46c90c9328bc776a5d480b7997d6d7c09613d`。开始先 `git fetch`、记录 `BASE_SHA`、`git status --short`，不得硬性 reset 到旧 SHA；如果上游变化需出对比。
- 首先阅读同包的 `00_总体方案与实施顺序.md`，本文件是**唯一可实施阶段**，不要顺手执行后续阶段。
- 不触碰 `ModifiedArmy/ARCHITECTURE.md`、`ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`；不改变 `Main.cs` 的 99000 金币及装备、新游戏奖励触发行为。
- 不改变 SaveDefiner/Saveable 类型及字段编号、原 SyncData 键，尤其旧增援 8 / 新运输 9。保留 Harmony 目标/拦截返回、兵源数值、文化规则、征粮/封邑规则和授权。
- 只报告实测过的内容；未运行 Bannerlord 就不能声称游戏测试通过。末尾交付必须有 Base/New SHA、文件差异、每条工作项、PASS/FAIL/BLOCKED、日志/复现证据、Rollback、NEXT_PHASE_ALLOWED。

### 重点文件

`ForceManagement/Behaviors/AiForcePlanningBehavior.cs`、`ForceManagement/Behaviors/PartyExchangeBehavior.cs`，新建 `ForceManagement/Missions/PartyForceTaskCoordinator.cs`、`PartyForceTaskPersistence.cs`、`PartyVisitExecutionService.cs`（命名参考，可按实际优化）。

### 按顺序实施

1. 列出 Behavior 私有方法/事件/保存字段、副作用和调用方，编写逐方法迁移表。
2. 提取任务协调：`EvaluateParty`、`ApplyLivePlan`、任务清理、行动状态机、路线所有权转移；Coordinator 只能操作当前 Campaign 的任务，不创建第二份持久化列表。
3. 原 SyncData 入口保留 `modifiedarmy_force_task_records_v1`，序列化/反序列化委托适配器；加载后不重放旧资源动作。
4. 提取到达执行：常规捐兵/攻占后捐兵/封邑与驻军征兵/佣兵购买/战时休整/和平复员各自调用既有领域 Service。
5. 确保 `PartyExchangeBehavior::EvaluateVisit` 与上方的 `ExecuteAtSettlement` 仅一个拥有某次访问提交；日门禁是事件去重，OperationId 是事务去重，两者不能混淆。
6. 保留公开旧 API 作为代理以减少调用方改动；迁移后删除没有职责的 partial 和重复的私有 helper。

### 验收

相同输入产生同一目的地、同一随机准入比例、同一招募结果；T01–T07/T14/T24。S1 五态冻结机制必须完整保留。

## 停止与交接规则

- 一旦发现上游改动覆盖本阶段修改文件，先做差异对照再更改。
- 阶段中遇到存档载入失败、士兵/金钱/物资不守恒、Harmony 原版双执行，立即停止进一步提交到主分支并记录复现。
- 最终独立提交或 PR，不将下一阶段代码打包进本阶段 Commit。输出 `NEXT_PHASE_ALLOWED=YES/NO` 和证据。
