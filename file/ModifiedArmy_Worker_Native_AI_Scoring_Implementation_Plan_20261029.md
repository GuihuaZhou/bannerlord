# ModifiedArmy：普通 AI 领主补员与战时休整改用原版 AI 评分——Worker 实施方案

> **任务性质：** 对现有 Git 仓库实施最小范围代码修改、完成编译/游戏内验证、提交独立分支并创建 PR。
>
> **仓库：** `GuihuaZhou/bannerlord`
>
> **分析基线：** `bf57e52f87081c3db07a4173dac273816d3ff004`（2026-10-29 讨论时的 `main`）。开始执行时先 `git fetch` 并检查最新 `main`，若已有差异，按现状移植，不要机械覆盖。
>
> **需求来源：** 已确定采用方案 A。ModifiedArmy 负责决定补员或战时休整目的地，原版 `AiPartyThinkBehavior` 负责比较评分、选出行动并执行移动。Mod 在任务有效期间，每次原版触发 `AiHourlyTickEvent` 时重复提交同一个高分候选；业务任务不是每小时重新建立。
>
> **现有材料：** 本交付包还附 `apply_modifiedarmy_native_ai_scoring.py`（参考实现/可应用脚本）和 `ModifiedArmy_native_AI_scoring_PR_notes.md`（早期 PR 摘要）。**脚本只是实现起点，必须由 Worker 编译、审查、实测后再提交。**

---

## 1. 明确的改动边界（不得扩大）

### 必须改动

1. 普通独立 AI 领主的 **`Recruit` 补员任务**。
2. 同一个规划器中已存在的 **`WarRest` 战时休整、封邑兵返乡任务**。
3. 将这两类任务的行军控制由 Mod 直接 `SetMoveGoToSettlement()`，改为在原版 `PartyThinkParams` 中提交高评分 `AiBehavior.GoToSettlement` 候选。
4. 调整现有任务生命周期：原版当前移动目标改变，不再等价于 Mod 任务失效；读档后也不能以“当前原版移动路线是否一致”判定任务有效。

### 不得改动

- 不更改 `AiForcePlanningModel` 既有补员/战时休整需求、兵源选择和每日规划的业务规则。
- **不启用**当前硬编码 `WarRestPlanningService.BuildContext().Settings.Enabled = false` 的战时休整；本次只完成该路线通道，保持原有开关与默认行为。
- 不启用、更改 `PeaceDemobilization` / `DemobilizationBehavior` 的和平自动复员和主动返乡路线。
- 不修改征粮队 `SupplyPartyBehavior`、救济粮队或 `ReinforcementTransportBehavior` 的专用 Party 路线。
- 不改职业兵、封邑兵、雇佣兵的实际批准与扣减逻辑；不改 `PartyVisitExecutionService`、`PartyExchangeService` 的事务语义。
- 不修改游戏本体 DLL，不新增 Harmony Patch 去拦截 `AiPartyThinkBehavior`、`SetPartyAiAction` 或 `MobileParty` 的原版决策/移动。
- 不设计新的“统一业务规划器”，不为不同类型任务强制集中到同一个文件；现有补员与 WarRest 已经在 `AiForcePlanningModel` 中统一日常决策。
- **本次不扩展新的安全条件**；沿用原有业务筛选及 Mod 管理的 Party 范围，不开展额外的安全策略重构。

## 2. 已从原版反编译源码确认的事实

仓库 `code/` 目录中包含此次上传的 `PartyThinkParams.cs`、`AiPartyThinkBehavior.cs`、`SetPartyAiAction.cs`、`CampaignPeriodicEventManager.cs` 以及此前的 `AiMilitaryBehavior.cs`、`AiVisitSettlementBehavior.cs`。

1. `AiPartyThinkBehavior.PartyHourlyAiTick()` 在实际进入战略决策时，先对 `ThinkParamsCache` 执行 `Reset(mobileParty)`，然后通过 `CampaignEventDispatcher.Instance.AiHourlyTick(mobileParty, thinkParamsCache)` 广播所有评分监听器。
2. **`PartyThinkParams.Reset()` 清空 `_aiBehaviorScores`**，而 `Initialization()` 只刷新军力统计，**不会清空候选**。因此不可只在任务建立时投递一次候选；要在每轮 `AiHourlyTickEvent` 重新提交。
3. `AiPartyThinkBehavior` 遍历候选，使用 `score > bestScore` 选择最大分；同分时先进入集合的候选胜出。它还有 `WillGatherArmy` 的备选候选、行动切换门槛等机制，所以“最高候选分”不等于无视所有原版执行条件。
4. 常规领主并非每个游戏小时都发生一次完整战略评估：原版按 `HourCounter` 和状态决定频率；**监听 `AiHourlyTickEvent`，不要另设定时器每小时主动提交到一个旧的 `PartyThinkParams` 对象**。
5. 原版 `AiVisitSettlementBehavior.AddBehaviorTupleWithScore()` 使用 `TryGetBehaviorScore` / `SetBehaviorScore` / `AddBehaviorScore` 合并同一行动候选；`PartyThinkParams.AddBehaviorScore()` 本身只负责追加，不去重。
6. 原版选中 `GoToSettlement` 后调用 `SetPartyAiAction.GetActionForVisitingSettlement()`；其 `ApplyInternal()` 对相同目的地、相同导航参数的行动避免重复调用 `SetMoveGoToSettlement()`。**每轮重复评分不等于每轮重复下达移动命令。**

源文件（基线）：

- `code/PartyThinkParams.cs`
- `code/AiPartyThinkBehavior.cs`
- `code/AiVisitSettlementBehavior.cs`
- `code/SetPartyAiAction.cs`
- `code/CampaignPeriodicEventManager.cs`

## 3. 当前 Mod 调用链、冲突根源

```text
AiForcePlanningBehavior.OnDailyTick()
  -> PartyForceTaskCoordinator.EvaluateParty()
    -> PartyForceCandidateService.BuildContext()
    -> AiForcePlanningModel.BuildDailyPlan()
       -> 先评估 WarRest（当前配置禁用）
       -> 否则评估 Recruit 和各兵源/目标
    -> PartyForceTaskCoordinator.ApplyLivePlan()
       -> 当前直接 party.SetMoveGoToSettlement(target)  [要移除]
       -> 设置 OwnsMovementOrder = true                 [语义要消除]

原版 AiPartyThinkBehavior.PartyHourlyAiTick()
  -> Reset(PartyThinkParams)
  -> AiHourlyTickEvent：各原版监听器贡献分数
  -> 最高候选 -> SetPartyAiAction -> 原版移动

下一日 ModifiedArmy 又检查 TargetSettlement，不一致即 Cancelled   [要移除]
```

已诊断的核心问题：Mod 每日直接设置定居点目标，而原版独立战略评分又选择其他目标；旧协调器因“原版路线不一致”取消仍然有业务价值的补员任务。不要继续采用 Mod 与原版反复重发移动命令的方式解决。

**本次目标调用链：**

```text
每日：BuildDailyPlan() 只负责建立/更新业务任务
           |
           v
保存 PartyForceTask：Party、Intent、TargetSettlement、SourceKind、SourceBatchId 等
           |
           +---- 原版每轮 AiHourlyTickEvent ----> 查询当前有效任务
           |                                      |
           |                                      v
           |                             提交高分 GoToSettlement
           |                                      |
           |                               原版选择并移动
           |                                      |
           +----------------------------- 抵达原目的地
                                                  |
                                        PartyExchangeBehavior
                                                  |
                                      PartyVisitExecutionService
                                                  |
                                       执行既有 Recruit / WarRest 事务
```

## 4. Worker 具体修改清单

### 4.1 `ForceManagement/Behaviors/AiForcePlanningBehavior.cs`

**A. 注册评分事件**

在原有 `RegisterEvents()` 中额外监听：

```csharp
CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(
    this, OnAiHourlyTick);
```

保留原 `DailyTickEvent` 与 `OnGameLoadFinishedEvent`；不要新增第二轮每日规划，也不要在该回调中改变士兵名册、驻军名册或移动指令。

**B. 增加评分回调（推荐签名）**

```csharp
private void OnAiHourlyTick(MobileParty party, PartyThinkParams p)
```

步骤：

1. 只从现有任务表读取属于该 Party 的当前有效 `PartyForceTask`；复用 `FindTask(party)` / `Coordinator.FindTask(party)`，不要在这里重新调用 `BuildDailyPlan()` / `BuildContext()` / 完整兵源枚举。
2. 仅处理 `Intent == Recruit` 或 `Intent == WarRest`，且任务非完成、非取消、非暂停；`TargetSettlement` 必须存在；若部队已经驻留在任务目标，交给现有抵达事务处理，不再提交前往同地点的路线候选。
3. 使用原版 `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty()` 获取导航类型和 `isFromPort`，构造与原版访问定居点语义一致的 `AIBehaviorData(target, AiBehavior.GoToSettlement, navigationType, false, isFromPort, isTargetingPort)`；当前参考脚本 `isTargetingPort = false`，Worker 需根据该版本原版行为与目的地选择逻辑核对字段取值。
4. 设置高评分：以 `1000f` 作为参考下限，扫描**当前已经存在**的 `p.AIBehaviorScores`，若存在更高的有限值则提升至其上（如 `max + 1`）。避免 NaN/Infinity/溢出，并检查分值是否超出原版期望范围。不要复用补员候选内部的经济/距离排名分数作为最终战略行为分数。
5. 同一个 `AIBehaviorData` 已存在时使用 `TryGetBehaviorScore` + `SetBehaviorScore` 更新，不存在时使用 `AddBehaviorScore`。**不要重复追加完全相同的行动候选，也不要把同目标分数无界累加。**
6. 事件回调只提供候选；**禁止**调用 `SetMoveGoToSettlement`、`SetPartyAiAction.GetActionForVisitingSettlement`、`SetMoveModeHold`。

示意（非完整替换代码）：

```csharp
// 由日常计划决定“去哪儿”，此处只参与原版决策。
var task = FindTask(party);
if (task == null ||
    (task.Intent != PartyForceIntent.Recruit &&
     task.Intent != PartyForceIntent.WarRest) ||
    !IsActiveTask(task) || task.TargetSettlement == null)
    return;

// 使用原版 AiHelper 求导航参数。以下仅示意评分和 upsert：
var candidate = new AIBehaviorData(task.TargetSettlement,
    AiBehavior.GoToSettlement, navigationType, false,
    isFromPort, isTargetingPort);
float score = CalculateHighScore(p.AIBehaviorScores);
if (p.TryGetBehaviorScore(candidate, out float old))
    p.SetBehaviorScore(candidate, Math.Max(old, score));
else
    p.AddBehaviorScore((candidate, score));
```

**评分保证的边界：** `1000f` + “高于目前集合”只能确保不低于**回调执行时**已提交的候选；若后续其他事件监听器加入更高评分，该候选仍可被超越。不要把它写成“绝对最高指令优先级”。本次禁止为解决此边界而打原版 Harmony Patch；通过日志和验收确认该分数足以胜过原版正常战略候选即可。

### 4.2 `ForceManagement/Missions/PartyForceTaskCoordinator.cs`

**A. 停止直接移动**

在 `ApplyLivePlan()` 中：

- 保留现有 `PartyForcePlan -> PartyForceTask` 接纳/替换、PartyId/ClanId、版本、日期、任务保存逻辑。
- 新任务状态设为 `Planned`（待原版选择），不再把“已直接下达移动”当作 `Traveling` 的必要条件。
- **删除** `party.SetMoveGoToSettlement(...)`。
- 删除 `ReleaseOwnedMovementOrder()` 中的 `party.SetMoveModeHold()` 路线释放操作；模块已不拥有这条移动命令，也无权在取消 Mod 任务时强制让原版部队 Hold。
- 保留字段 `OwnsMovementOrder` 的兼容性，但新路径始终设 `false`；本次不要改现有序列化契约和枚举数值。
- 在新建/替换/结束有效任务时设置 `party.Ai.RethinkAtNextHourlyTick = true`，让原版尽快重新评估；**这是请求原版决策，不是发送移动命令**。

**B. 不因短期路线偏离取消业务任务**

删除 `EvaluateParty()` 中：

```csharp
if (current?.OwnsMovementOrder == true &&
    party.TargetSettlement != current.TargetSettlement)
{
    // MovementOrderChangedExternally -> Cancelled
}
```

保留原有任务终止原因，包括任务被新的每日业务计划替换、业务失效、转移事务完成等。既有暂停逻辑将 `OwnsMovementOrder = false`，不要调用 `SetMoveModeHold()`。

**C. 对已存在同一业务任务保持稳定**

当前 `AiForcePlanningModel.IsSameLiveTask()` 已经保持同目标、同来源的补员任务；保留。

注意当前 `BuildWarRestPlan()` 每次批准时可能重新构建 `WarRest` 任务，即使目标未变；因 WarRest 目前禁用，本 PR 不应擅自放开策略。**至少在代码审查或 PR 说明中记录其未来启用后的重复替换风险**；如果 Worker 发现本次路线迁移实际导致该风险，应只增加狭义的同一 WarRest 任务延续判断，不能修改休整政策或选址算法。

### 4.3 `ForceManagement/Behaviors/AiForcePlanningBehavior.cs` 读档任务复审

当前 `RevalidateLoadedTask()` 有如下错误依赖：

```csharp
bool ownsRoute = party?.DefaultBehavior == AiBehavior.GoToSettlement &&
                 party.TargetSettlement == task.TargetSettlement;
// valid = ... && ownsRoute;
```

修改为：以**任务业务资格和目标来源**复审为准，删除 `ownsRoute` 作为 `valid` 的必要条件。继续保留已有 Clan/Owner/目标/兵源验证逻辑。

复审通过后：

- `Status = Planned`（让原版重新选择目的地）。
- `OwnsMovementOrder = false`。
- `party.Ai.RethinkAtNextHourlyTick = true`。
- **不要**尝试按旧存档中的原版 `DefaultBehavior` 重建“路线所有权”。

继续使用原有 `PartyForceTaskPersistence`，不得新增需要破坏旧存档的保存字段。

### 4.4 不应修改的其他文件

- `AiForcePlanningModel.cs`：现有 `WarRest` 优先于 `Recruit` 的业务先后顺序保持不变。
- `PartyForceCandidateService.cs`：仍仅日常规划构造候选；不把每轮原版评分变成完整兵源重算。
- `PartyVisitExecutionService.cs` / `PartyExchangeBehavior.cs`：抵达后保持原事务管道、同日去重机制及操作幂等性。
- `WarRestPlanningService.cs`：`Enabled=false` 保持。
- `DemobilizationBehavior.cs`、`PeaceDemobilizationModel.cs`：本次完全不碰。
- `SupplyPartyBehavior.cs`、`ReinforcementTransportBehavior.cs`：专用运输不参与普通领主的这次改动。
- `code/*.cs`：反编译游戏源码仅用于核对，不写回。

## 5. Worker 实施流程（按顺序执行）

1. `git fetch origin`，核对主分支提交与工作区状态；如主分支已更新，先对照本方案判断需要重新定位的代码。
2. 新建独立分支，例如 `fix/modifiedarmy-native-ai-settlement-scoring`。
3. **先阅读** `PartyThinkParams.cs`、`AiPartyThinkBehavior.cs`、`AiVisitSettlementBehavior.cs`、`SetPartyAiAction.cs` 及目标两个 Mod 文件，确认实际版本 API 一致。
4. 使用附带 `apply_modifiedarmy_native_ai_scoring.py` 作为参考实现。可先运行 `--check --patch` 生成差异但不改源码；若最新仓库修改导致锚点失效，**手工按设计移植，不要去掉防护强制应用**。
5. 对代码做逐行 review，重点检查：事件回调的任务状态、`AIBehaviorData` 字段、导航类型、候选 score upsert、没有同步重跑业务规划、没有直接移动方法残留。
6. `git diff --check`、`git diff`，并搜索 `PartyForceTaskCoordinator` 中是否残留 `SetMoveGoToSettlement` / `SetMoveModeHold` / `MovementOrderChangedExternally`。
7. 在具备游戏 DLL、NuGet 包和适当 .NET SDK 的环境编译 `ModifiedArmy/ModifiedArmy.sln`。项目为 `netstandard2.0`，`.csproj` 中包含指向 `D:\Steam\...` 的本机 Bannerlord 引用路径；若 Worker 本机安装位置不同，**通过本地开发环境调整引用，不应为了此 PR 迁移整个项目依赖结构**。
8. 用实际游戏测试以下验收矩阵，截图/日志留证；发现问题只修本次边界内的缺陷。
9. 提交分支并创建 PR，描述设计边界、静态检查、构建结果、游戏验收记录及未解决限制。**不要直接合并到 main**。

推荐命令：

```powershell
# 在本地克隆仓库的根目录

git fetch origin
git switch -c fix/modifiedarmy-native-ai-settlement-scoring origin/main

# 检查模式：不修改源文件，只生成对比补丁
python .\apply_modifiedarmy_native_ai_scoring.py . --check --patch .\ModifiedArmy_native_AI_scoring.patch

# 如果已审查确认可用，再实际应用
python .\apply_modifiedarmy_native_ai_scoring.py .

git diff --check
git diff -- ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/AiForcePlanningBehavior.cs ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs

dotnet build .\ModifiedArmy\ModifiedArmy.sln -c Release

# 本地验证后执行
git add ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/AiForcePlanningBehavior.cs ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs
git commit -m "fix(ModifiedArmy): route lord force tasks via native AI scoring"
git push -u origin fix/modifiedarmy-native-ai-settlement-scoring
# 随后通过 GitHub CLI 或网页对 main 创建 PR
```

> 注意：上面仅提供 Worker 执行指令，**本交付并没有声称以上 Git 操作、构建或游戏测试已完成**。此次会话的 GitHub 连接尝试 `create_branch` 返回 HTTP 403，因此远程 PR 必须由有写权限的 Worker 完成。

## 6. 验收用例与预期结果

| ID | 测试场景 | 必须观察到的结果 |
|---|---|---|
| R1 | 例如仅剩 18/102 名士兵的独立领主，附近驻军有可批准兵源 | 原有每日规划只创建一个 `Recruit` 任务，目标稳定；没有 Mod 直接 `SetMoveGoToSettlement` |
| R2 | 任意一个既有 `Recruit` 任务持续多个原版战略评估周期 | 每次 `AiHourlyTickEvent` 都贡献指向同一目标的高分候选；未重复新建任务 |
| R3 | 原版 AI 短暂切换到其他移动行为 / 目标 | 不能仅因 `TargetSettlement` 不匹配就取消 Mod 补员任务；再次战略评估仍可看到原补员候选 |
| R4 | 原版选中同一个定居点并持续行军 | 没有 Mod 每轮直接重发移动；原版相同目标的行动不会重复下达相同导航命令 |
| R5 | 抵达计划目标 | 执行原有 `PartyVisitExecutionService`，真实转移兵员且状态正确完成；没有重复转移 |
| R6 | 在任务途中保存、读档 | 即使读档时 `DefaultBehavior`/`TargetSettlement` 不等于 Mod 任务目的地，有效任务也会保留，并在原版重新评分后出现 |
| R7 | 业务任务被新日常计划合法替换，或达到完成/失效状态 | 原任务不再参与评分；不会为释放旧任务而调用 `SetMoveModeHold()` |
| R8 | 当前 `WarRest` 保持关闭 | 不出现新增战时休整移动任务；补员流程不受影响 |
| R9 | 使用只读/模拟方式使一个 `WarRest` 任务有效（或在单独测试配置下临时启用，**不得提交启用修改**） | 也走同一个 `AiHourlyTickEvent` 候选通道，不再直接下达路线 |
| R10 | 原有征粮、运粮援助、驻军增援运输 | 其现有独立移动逻辑未受本 PR 修改 |
| R11 | 连续多日、跨多次任务完成与取消 | 不发生任务无限增长、路线振荡或复员与补员事务重复执行 |
| R12 | 检查原版与 `ModifiedPolitics` 等评分监听器同时存在的情形 | 记录候选分数与最终原版选择，确认通常 Mod 正式任务胜过普通战略候选；如发现后续监听器超越，明确记录而不是新增侵入性 Patch |

### 建议采用的最小日志

仅在测试构建中或现有诊断开关下记录：`PartyId`、`Task.Version`、`Intent`、`TargetSettlement`、进入 AI 评分回调的游戏时刻、同一候选的旧分值、新提交值、原版实际目标和任务完结结果。不要每小时全地图无条件刷详细日志；不要为了日志改变规划或移动。

## 7. 必须特别检查的技术风险

1. **事件注册顺序：** 在 `AiHourlyTickEvent` 中看到的原版评分列表，只代表此前监听器已提交的候选。后注册监听器仍可能改变结果。高分是本设计选择，不是绝对引擎权限。
2. **相同候选去重：** `AIBehaviorData.Equals()` 对目标、行为和导航字段的比较会影响 `TryGetBehaviorScore()` 命中。不要用不同导航字段意外创建同目标重复候选。
3. **定居点当前已到达：** 评分层不要提交向当前所在定居点移动的候选；现有 `OnDailyPartyTick` 能处理驻留中的任务检查。
4. **历史任务与保存格式：** `OwnsMovementOrder` 保留但恒为 `false`；不要误删旧任务可读性，也不要把“任务存在”与“原版当前行为相同”混为一谈。
5. **WarRest 未来启用：** `BuildWarRestPlan()` 尚没有完整的同一任务延续机制；不要在本 PR 里顺手重写政策，但需报告是否造成反复替换。
6. **数值范围：** 参考分数 `1000f` 只是高分下限，需要通过游戏日志确认没有 NaN/Infinity 或异常极值，确认不会触发原版其他分值相关副作用。
7. **任务执行身份：** 当前只涉及既有同一规划器输出的 `Recruit` / `WarRest`；抵达后通过保存的 `PartyForceTask.Intent` 执行事务。不要只靠 `TargetSettlement` 自行推断事务类型。

## 8. PR 交付要求

PR 标题建议：

`fix(ModifiedArmy): use native AI scoring for lord recruitment and war rest routes`

PR 正文必须包含：

- **Problem：** 原版 AI 的独立战略决策覆盖 Mod 每日直接移动，造成补员任务取消与目的地错乱。
- **Approach：** 日常规划保留，任务有效期间每轮原版 `AiHourlyTickEvent` 注入高分 `GoToSettlement`，移动交给 `AiPartyThinkBehavior`。
- **Changes：** 评分接入、协调器去直接移动、读档任务复审解绑路由、保持旧持久化格式。
- **Out of scope：** 和平退役、征粮队、增援运输、WarRest 的启用开关、兵员事务、原版 DLL/Harmony。
- **Evidence：** `git diff --check`、编译结果、R1–R12 至少核心用例的实际游戏日志与执行结果；未测项如实列明。
- **Limitations：** 事件监听顺序无法给出跨所有未来 Mod 的绝对最高分保证；本次不引入引擎级拦截。

## 9. Worker 完成定义（Definition of Done）

- [ ] 代码已提交到独立远程分支并创建 PR（提供 PR URL）。
- [ ] 所有普通领主的本次目标路径不再由 `PartyForceTaskCoordinator` 直接发移动命令。
- [ ] 有效 `Recruit` / `WarRest` 任务在每轮原版 AI 战略评分中持续提交同一候选。
- [ ] 不再因原版路线短期改变直接取消任务；读档同样不依赖旧移动路线一致。
- [ ] 原有日常规划、抵达事务、存档协议、WarRest 默认关闭均保持不变。
- [ ] 无新增原版 AI Harmony Patch，征粮、运粮和增援运输代码没有被改动。
- [ ] 完成编译与有证据的游戏内测试；未完成项目在 PR 中明确列出。

---

**最后强调：本方案是交付 Worker 的执行规范，不是已经完成的远程 PR。随包脚本的静态修改锚点曾针对基线提交校验，但 C# 构建和游戏运行尚须 Worker 验证。**
