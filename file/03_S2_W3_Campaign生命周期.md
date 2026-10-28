# S2｜按 Campaign 管理运行时状态，定义加载恢复屏障

## 公共约束（逐条执行）

- 仓库 `GuihuaZhou/bannerlord`，参考基线 `a3d46c90c9328bc776a5d480b7997d6d7c09613d`。开始先 `git fetch`、记录 `BASE_SHA`、`git status --short`，不得硬性 reset 到旧 SHA；如果上游变化需出对比。
- 首先阅读同包的 `00_总体方案与实施顺序.md`，本文件是**唯一可实施阶段**，不要顺手执行后续阶段。
- 不触碰 `ModifiedArmy/ARCHITECTURE.md`、`ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`；不改变 `Main.cs` 的 99000 金币及装备、新游戏奖励触发行为。
- 不改变 SaveDefiner/Saveable 类型及字段编号、原 SyncData 键，尤其旧增援 8 / 新运输 9。保留 Harmony 目标/拦截返回、兵源数值、文化规则、征粮/封邑规则和授权。
- 只报告实测过的内容；未运行 Bannerlord 就不能声称游戏测试通过。末尾交付必须有 Base/New SHA、文件差异、每条工作项、PASS/FAIL/BLOCKED、日志/复现证据、Rollback、NEXT_PHASE_ALLOWED。

### 重点文件/方法

- `ForceManagement/Services/ForceManagementFacade.cs::Current`（**已有按 Campaign 切换实现，务必保留**）。
- `Main.cs` 的 `CampaignReadyBehavior`、新开局事件（**奖励代码绝对不改**）。
- `ForceManagement/Behaviors/AiForcePlanningBehavior.cs::SyncData/OnGameLoadFinished`；`Fief/Models/FiefPartyManager.cs::SyncData/OnSessionLaunched`；`ForceManagement/Transport/ReinforcementTransportBehavior.cs::OnGameLoadFinished`；征粮 Behavior `SyncData/OnGameLoadFinished`。

### 按顺序实施

1. 基于 S0 状态表区分运行时/当前日期/日志节流/需要存档/仅重建四类。
2. 在现有 `ForceManagementFacade.Current` 基础上补显式 Lifecycle 协调：启动构建→ `SyncData` 恢复→依赖索引重建→验证任务/运输/征粮/封邑→ Ready。必须查实际游戏事件时序。**不以新建 Bootstrap 目录为验收项**。
3. 保持原有在役批次、封邑动员户数、当前任务存档键和征粮冷却字段不变；不能让 Facade getter 随意重置从存档恢复的权威状态。
4. Campaign A→B→A 同进程测试，检查已提交/冻结 OperationId、每日休整、WarningSuppress、路线节流没有串档；读档后无双事件订阅。
5. 保留旧 `ReinforcementMissionBehavior` 兼容读取和 `FiefSaveDefiner` 8/9 注册。

### 验收

T12、T13、T18、T19；含 SaveLoad 本地样本/截图/日志。无数据对象遗留另一存档、无重复奖励。若运行环境不支持，报告 Blocked，不能宣称通过。

## 停止与交接规则

- 一旦发现上游改动覆盖本阶段修改文件，先做差异对照再更改。
- 阶段中遇到存档载入失败、士兵/金钱/物资不守恒、Harmony 原版双执行，立即停止进一步提交到主分支并记录复现。
- 最终独立提交或 PR，不将下一阶段代码打包进本阶段 Commit。输出 `NEXT_PHASE_ALLOWED=YES/NO` 和证据。
