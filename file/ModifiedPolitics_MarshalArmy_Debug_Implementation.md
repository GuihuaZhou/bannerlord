# ModifiedPolitics：大元帅军团 AI Debug 诊断实施文档

> **实施性质：仅增日志（诊断），不得修改 AI 行为**  
> 基准：2026-11-08 读取 `GuihuaZhou/bannerlord` GitHub `main` 分支。  
> 目标：定位“玩家加入西帝国、已有大元帅但长期不组军团”，并形成下一次游戏日志足以判断具体阻断层级的证据。  
> 本文件是**交给编码 AI 执行的实施规格**，不是已完成的代码改动；本轮没有修改仓库、DLL、游戏配置或存档。

## 1. 任务边界（强制要求）

**唯一允许的游戏运行时变更：追加 Debug 诊断输出。**

- 不改变 `CanLordCreateArmy` 的返回值、候选列表、原版权限门槛、职业兵/封邑兵招募或运输任务保护。
- 不调整军团创建权（有大元帅时仅大元帅及统治者可创建）、目标评分、军团权重、最低兵力比例、粮食门槛、目标/部队选择、AI 随机数和执行行动。
- 不调用原版评分函数进行“额外试算”：这些函数可能使用随机数、访问缓存或成本高，重复调用会改变行为或性能。只观察**游戏自然发生的调用**及其真实返回结果。
- 不在 `code/` 下的反编译本体参考源码中实施代码变更；所有新增逻辑放在 `ModifiedPolitics/ModifiedPolitics/` 内，用现有行为或 Harmony 的只读 Prefix/Postfix 取证。
- 不引入独立游戏机制、额外日常任务、存档字段、MCM 新开关、UI、跨模块依赖或 `ModifiedArmy` 改动。
- 所有新日志使用 `ModifiedPolitics.Tool.ModLogger.FileDebug(...)`，仅写文件，避免游戏内刷屏；原有 Info/Notice 日志保持不变。
- 调试失败不得吞掉或改变原方法的异常，Harmony Postfix 只能记录不得修改 `__result` 或入参。

### 诊断只覆盖玩家所在王国

优先判断 `Clan.PlayerClan?.Kingdom == kingdom`，然后仅追踪该王国现任大元帅和作为当然组军团指挥者的统治者；不跟踪全地图所有 AI 领主。日志写入时包含：王国 `StringId` / 名称、Hero `StringId` / 名称、Party `StringId` / 名称、战役日/小时、一次决策追踪 ID。

**注意**：`MarshalArmyAiBehavior` 已存在 `_lastStates` 字典，`SetDiagnosticState` 在状态没有变化时不再输出；这只是现有状态日志的节流，不应由它控制新的、单独采样的 Debug 明细。

## 2. 当前实证与待回答问题

现有 `ModifiedPolitics.log` 的西帝国/加里俄斯记录包括：

- `可召集部队=5～6`，`集结兵力≈1012～1142`；
- `平均粮食≈20～24天`，日志中的围城粮食阈值为 `12.0天`；
- `新增目标数=5`，`正分目标数=0`，`最佳军团分数=0.000000`；
- 另一些时刻出现 `未通过本体军团条件`，尽管影响力约 `3400`、单队粮食约 `29天`、兵力比例 `83%`；
- 官职限制会拒绝非大元帅/非统治者组军团，但还不知道是否有直接 `CreateArmy` 调用被拦截。

现有事实**不能直接认定**“全部零分必然是敌军兵力过强”。必须用新日志区分：

1. 是否在 `CanLordCreateArmy` 阶段就失败？失败时可观察到哪些状态，是否由额外保护/资格限制影响？
2. 已通过资格后，哪些 ArmyType、哪些目标产生了真实原版评分调用？
3. 如果原版 `GetTargetScoreForFaction` 返回 0，是原版内部返回的 0；如果原版返回正值，后续何处归零？
4. 原版最终候选集中，军团候选是否为正分、是否输给其他行为，还是根本没有参与？
5. 实际是否调用 `Kingdom.CreateArmy`？如果调用，权限 Prefix 是否拒绝？如果放行，`ArmyCreated` 事件是否确认创建成功？

## 3. 代码定位（均为当前仓库 main，行号为此次读取时快照）

| 优先级 | 文件、当前位置 | 源码事实 | 新增诊断 |
|---|---|---|---|
| P0 | `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs` 第 42～127 行 | `OnAiHourlyTick` 早退条件及 `CanLordCreateArmy` | 入口、早退原因、原版资格返回和候选数 |
| P0 | 同文件第 130～213 行 | 集结兵力、粮食、调用 `FindBestTargetAndItsValueForFaction`、乘权重 | 每军团类型评分前后、候选列表、原始/加权分数 |
| P0 | `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs` 第 102～160 行 | `CanLordCreateArmy` Prefix/Postfix/Finalizer 及 `Clan.Leader` 替代 | 本体资格检查结果、身份补丁启用状态 |
| P0 | 同文件第 29～49 行 | `Kingdom.CreateArmy` 的权限 Prefix | 实际组军团调用是否发起、被谁放行/阻止 |
| P0 | 新增只读 Harmony Postfix 于 `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction(...)` | 捕获真实本体目标评分 `__result` | 逐目标原始评分 0/正/异常值 |
| P1 | 新增只读 Harmony Postfix 于 `CampaignEventDispatcher.AiHourlyTick(MobileParty, PartyThinkParams)` | 事件接收者回调后、原版最终筛选前的候选分数列表 | 最终候选池里军团/非军团最高分与差距 |
| P0 | `MarshalArmyAiBehavior.RegisterEvents()` 第 27～31 行 | 现有事件注册 | 添加 `CampaignEvents.ArmyCreated` 监听，确认创建成功 |
| 仅作参考 | `code/AiMilitaryBehavior.cs` 第 102～187、373～447、489～545 行 | 原版类型目标评分、粮食与兵力等检查 | **不可直接修改该文件** |
| 仅作参考 | `code/DefaultTargetScoreCalculatingModel.cs` 第 169～440 行 | 原版目标打分，其中第 286～289 行有“集结战力不足直接返回 0”路径 | **不可直接修改该文件** |
| 仅作参考 | `code/AiPartyThinkBehavior.cs` 第 70～146 行 | AI 候选评分与最终 `CreateArmy` 调用 | **不可直接修改该文件** |
| 仅作参考 | `code/Kingdom.cs` 第 975～988 行 | `CreateArmy` 成功时调用 `CampaignEventDispatcher.OnArmyCreated` | **不可直接修改该文件** |

仓库参考链接：

- [MarshalArmyAiBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs)
- [MarshalArmyPatches.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs)
- [AiMilitaryBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/AiMilitaryBehavior.cs)
- [DefaultTargetScoreCalculatingModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultTargetScoreCalculatingModel.cs)
- [AiPartyThinkBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/AiPartyThinkBehavior.cs)
- [CampaignEventDispatcher.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/CampaignEventDispatcher.cs)
- [CampaignEvents.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/CampaignEvents.cs)

注：`code/` 为仓库中的本体**反编译参考代码**，其版本未必与当前安装的 DLL 完全一致。例如此次参考 `DefaultArmyManagementCalculationModel.cs` 中无法直接定位 `CanLordCreateArmy` 的完整方法体，而模组引用的已安装游戏 API 确实存在该方法。**不要按参考源码猜测未通过资格的确切原生分支，也不要臆造该方法内部局部变量。**

## 4. 实施设计：最小只读追踪器

建议在 `ModifiedPolitics/ModifiedPolitics/HeroOffices/Diagnostics/` 下新增 `MarshalArmyDebugTrace.cs`（内部静态类）。**职责只是选定样本、关联日志和收集只读观察值**。

- `IsRelevant(MobileParty party)`：玩家王国内、未淘汰、为统治者或本国正式大元帅；可以复用现有官职判断，不复制/篡改业务判断。
- `ShouldSample(MobileParty party, string state)`：每位 Hero 至少每个游戏日采样一次详细路径；首次、状态改变、零分转正、CreateArmy 被拒绝、创建成功立即输出摘要。连续正常轮询仅输出节流摘要。推荐记录少量内存中的最近一次状态/采样游戏日，不使用存档，不长期强引用已销毁 Party。
- `TraceId`：例如 `EW:hero_id:day91508:tick123`，在一次原版 AI 决策评估的日志中保持一致，字符串不能只用现实时间秒，否则容易跨多次调用串线。
- `[ThreadStatic]` 的 `CurrentTargetScoreContext`：仅在大元帅当前一次 `FindBestTargetAndItsValueForFaction` 调用期间有效，包含 Hero/Party、ArmyType、TraceId、是否取样。**用 `try/finally` 还原上一层 context**；不得把这个上下文写入存档，不允许依赖游戏线程永久保持同一上下文。
- 目标评分日志记录真实返回值，不根据其他数值额外调用目标评分函数；任何异常穿透保持原样。
- 一次决策最多记录：各 ArmyType 摘要 + 原生零分目标最多 12 个 + 正分目标最高 5 个 + 最终候选池前 5 个；超出时标注 `omitted=N`。注意不同军团类型应分别计数。
- 尽可能使用日志输出前的轻量级布尔判断，避免每帧创建大量字符串、枚举全世界部队或反复计算 `GetCustomStrength`。

> 由于尚未编译/反射核对安装 DLL，以下 Harmony 代码为**锚点和签名示意**。编码 AI 必须以项目当前引用 DLL 的实际成员及重载为准校验，不得因为示意调用签名不同就修改游戏逻辑。

## 5. 按点实施的 Debug 日志

### D01 — AI 入口/早退分支（P0）

**位置**：`MarshalArmyAiBehavior.OnAiHourlyTick`，在所有已有 `return` 分支旁附加只读、节流的文件 Debug。

**记录字段**：`traceId`、`kingdom`、`hero`、`party`、`isRuler`、`isMarshal`、`armyState`、`partySizeRatio`、`influence`、`foodDays`、`atSea`、`mapEvent`、`settlementSiege`、`warEnemyWithFief`、`reasonCode`。

固定原因码建议：`PLAYER_MANUAL`、`OFFICE_INELIGIBLE`、`ALREADY_LEADING_ARMY`、`ATTACHED_TO_ARMY`、`MISSING_NATIVE_AI`、`BUSY_SEA`、`BUSY_BATTLE`、`BUSY_SIEGE`、`LOW_PARTY_RATIO`、`NO_ENEMY_FIEF`、`READY_FOR_NATIVE_CHECK`。

**不记录**大量无关领主的 `not marshal` 退出；只记录被识别为正式大元帅/国王之后的路径。

### D02 — `CanLordCreateArmy` 原版资格检查（P0）

**位置 A**：`MarshalArmyAiBehavior` 第 109 行附近，在**已有** `CanLordCreateArmy(party, out candidates)` 调用之后输出 `result`、`candidatesCount`、当前状态（建议只把原调用改成一个 `bool canCreate` 本地变量，然后照旧按它分支；不得二次调用原版模型）。

**位置 B**：`MarshalArmyPatches.EndArmyEligibilityCheck` 现有 Postfix，在 `_armyEligibilityParty = null` **之前**记录 `__result`、入参 Party、身份补丁替代次数、当前领队是否大元帅。若要读取 `out` 候选，必须检查编译时真实参数签名；不要修改 `ref` / `out` 结果。需要注意此 Postfix 可能被游戏其他入口调用，务必过滤只记录目标 Hero。

日志码：`ELIGIBILITY_PASS`、`ELIGIBILITY_FAIL_NATIVE_UNKNOWN`、`ELIGIBILITY_PASS_NO_MEMBERS`。

**边界**：已知 `CanLordCreateArmy` 返回 `false` 不等于某个特定原因。不能在没有原生分支级证据时把 `false` 解释为“影响力不足”“粮食不足”“候选不够”或“身份补丁失败”。可以在日志中一并记录这些**现状快照**用于排除法。

**可附加**：被调用模型运行时类型 `Campaign.Current.Models.ArmyManagementCalculationModel.GetType().FullName`，以及 `MarshalArmyPatches.LeaderGateReplacementCount`；数值 `2` 表示 IL 读取点被替换了两处，**不证明每次本体资格检查都成功**。

### D03 — 可召集 Party 的组成（P0）

**位置**：`thinkParams.SetArmyMembers(candidates)` 前后、第 131～151 行的已有集结兵力与粮食汇总附近。

每个候选独立输出：`partyId`、`leaderId`、`clanId`、`memberCount`、`partySizeRatio`、`foodDays`、`inArmy`、`attachedTo`、`naval/atSea`、`customSiegeStrength`；应**复用已有集结强度计算中的值**，避免仅为日志重复调用昂贵模型。

总计：`candidateCount`、`leaderSiegeStrength`、`candidateStrengthSum`、`gatheringStrength`、`averageFoodDays`、`minimumFoodDays`、`siegeFoodThreshold`。

这一步主要验证“日志里说可召 5 支”到底是什么队伍、战力强弱如何，且不更改候选列表顺序和元素。

### D04 — 每种军团目标的前后评分及倍率（P0）

**位置**：`MarshalArmyAiBehavior` 第 155～212 行，原有 `for (int i=0; i<4; i++)` 循环和 `SetBehaviorScore` 倍率应用处。

对每个实际进入 `FindBestTargetAndItsValueForFaction` 的 `ArmyType`：

1. 调用前记录 `preCount`、`Objective`、`WillGatherAnArmy=true`、`gatheringStrength`、`avgFoodDays`、`siegeFoodThreshold`；
2. 开始本次 `CurrentTargetScoreContext`，**仅执行一次原本已有的评分调用**，`finally` 清理上下文；
3. 调用后读取 `postCount`、`addedCount`、新增候选中 `rawScore > 0 / = 0 / 非有限值` 的数量；
4. 不变更现有 `CalculateArmyCreationMultiplier` 与倍率应用；应用前后分别记录 `rawScore`、`multiplier`、`finalScore`，并明确 `ArmyCountFactor`、`InfluenceFactor`、`PartySizeFactor` 的现值（只对现有公式取值做同源输出；不额外执行评分模型）；
5. 将新增军团候选按 `targetId+ArmyType+AIBehavior` 记录，含 `WillGatherArmy` 和 `NavigationType`，用于区分新增的是围城、防守还是其他候选；
6. 对 `addedCount>0`、`positiveCount=0` 特别输出一次 `ALL_NEW_SCORES_ZERO` 摘要。

**关键**：当前调用是原版 `FindBestTargetAndItsValueForFaction(armyType, thinkParams, gatheringStrength)`，以及之后将新增分数乘倍率。新增日志不应切换至 4 参数重载、不改枚举范围、不改 `Raider` 跳过逻辑，也不改变 `thinkParams.WillGatherAnArmy` 的生命周期。

### D05 — 原版本体逐目标评分 `GetTargetScoreForFaction` 的真实返回（P0）

**新增只读 Harmony Postfix**，锚点：`DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction(Settlement, Army.ArmyTypes, MobileParty, float)`，返回 `float`。

示意（需要核对当前 DLL 实际签名和 Patch 成功情况）：

```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(DefaultTargetScoreCalculatingModel),
    nameof(DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction))]
private static void TraceOriginalTargetScore(
    Settlement targetSettlement,
    Army.ArmyTypes missionType,
    MobileParty mobileParty,
    float ourStrength,
    float __result)
{
    // 只读；若不在本次大元帅目标评分上下文，不记录。
    if (!MarshalArmyDebugTrace.MatchesActiveTargetScoring(mobileParty))
        return;

    MarshalArmyDebugTrace.RecordTargetScore(
        targetSettlement, missionType, mobileParty, ourStrength, __result);
}
```

每次真实调用可记录：`armyType`、`targetId`、`targetName`、`targetOwnerKingdom`、`isFortification`、`hasActiveSiege`、`ourStrength`、`nativeTargetScore`、`isZero`、`scoreModelType`、`traceId`。

若 `nativeTargetScore == 0`：只标记 `NATIVE_TARGET_ZERO_REASON_UNKNOWN`，**不得**在日志里武断标记 `INSUFFICIENT_STRENGTH`。本体参考代码确实包含兵力不足直接 `return 0f` 的路径（第 286～289 行），但也有其他零分可能；仅凭 Postfix 不能读取原方法内部的 `num16` 局部变量。

若 `nativeTargetScore > 0` 但最终新军团候选 `finalScore == 0`：重点定位 `GetFoodScoreForActionType`、`GetPartySizeScore`、距离得分、`CalculateArmyCreationMultiplier` 以及是否正确匹配同一目标。若连原版 Postfix 都没有被触发，先检查 `AiMilitaryBehavior` 提前返回/原版实例类型或补丁是否命中。

**版本风险**：运行时目标评分模型未必恰好是 `DefaultTargetScoreCalculatingModel`。首次日志同时记录实际 `Campaign.Current.Models.TargetScoreCalculatingModel.GetType().FullName`，如果是其他子类覆盖或替换，此 Postfix 可能不命中；此时应只调整只读观察点的具体目标方法，不替换评分模型。

### D06 — 全部回调执行后的行为候选池（P1）

原版 `AiPartyThinkBehavior.PartyHourlyAiTick` 先重置 `PartyThinkParams`，触发 `CampaignEventDispatcher.Instance.AiHourlyTick(mobileParty, thinkParamsCache)`，然后比较 `AIBehaviorScores` 的最大项，再考虑随机数、当前行为及门槛，最后在满足条件时调用 `Kingdom.CreateArmy`。

推荐对 `CampaignEventDispatcher.AiHourlyTick(MobileParty, PartyThinkParams)` 加**只读 Harmony Postfix**，从 `partyThinkParams.AIBehaviorScores` 快照读取最终候选池：

- `poolCount`、`maxArmyScore`、`maxNonArmyScore`；
- 最大军团候选的 `behavior / target / WillGatherArmy / score`；
- 最大非军团候选的对应信息；
- `DoNotChangeBehavior`、`CurrentObjectiveValue`、`party.DefaultBehavior`；
- 最高候选 Top 5（按分数显示，不修改原集合顺序），以及 `armyTopIsHighest`。

**这不是“最终选中的行为”**。本体 `AiPartyThinkBehavior` 还进行随机及行为变化检查；不读取它的私有局部变量，就不能宣称 Top 1 一定被执行。日志应标记 `FINAL_POOL_AFTER_EVENT`，而非 `SELECTED_ACTION`。

此只读 Postfix 比依赖普通事件订阅顺序更可靠，因为它在整轮 `AiHourlyTick` 分发完毕后执行，能够看到所有事件回调累计的候选分数。仍需在实机验证其目标方法/事件分发顺序，避免与其他 Harmony 插件冲突。

### D07 — 真正的 `Kingdom.CreateArmy` 调用与权限判断（P0）

**位置**：`MarshalArmyPatches.RequireMarshalForArmy` 第 32～49 行（已有 Prefix）。在不更改 `return allowed` 的前提下输出：

- `CREATE_ARMY_ATTEMPT`：调用者 Hero、Kingdom、ArmyType、目标 Settlement、`hasMarshal`、`isRuler`、`isAppointedMarshal`、`allowedByMarshalPatch`；
- 若权限拒绝，`CREATE_ARMY_REJECTED_BY_OFFICE_PATCH`；
- 若允许，`CREATE_ARMY_ALLOWED_BY_OFFICE_PATCH`，**不是创建成功**。

为了记录 `targetSettlement`、`selectedArmyType`，可以在 Prefix 形参中增加与已安装 `Kingdom.CreateArmy` 重载一致的**只读形参**，需验证实际签名；不得改写原始入参。

### D08 — 军团创建成功的权威事件（P0）

在 `MarshalArmyAiBehavior.RegisterEvents()` 增加：

```csharp
CampaignEvents.ArmyCreated.AddNonSerializedListener(this, OnArmyCreated);
```

新增 `OnArmyCreated(Army army)`：仅玩家王国、并且军团领导人为大元帅或王国统治者时，以 `ModLogger.FileDebug` 记录 `CREATE_ARMY_SUCCESS_EVENT`：领导 Hero / Party、ArmyType、军团目标、初始成员数量、时间。对照 `Kingdom.CreateArmy` 本体参考，第 975～988 行只有真正走到 `army.Gather(...)` 之后才调用 `OnArmyCreated(army)`。

若存在 `CREATE_ARMY_ALLOWED_BY_OFFICE_PATCH` 但没有对应成功事件，应标记为“发起并放行但未观察到成功事件”，后续查看领队是否 Active、是否仍在有效 Party、其他前后缀及异常；不得自动认定某个特定原因。无需修改 `Kingdom.CreateArmy` 的行为。

## 6. 统一日志格式（推荐）

统一前缀：`[王国官职][MarshalArmyDebug]`；所有字段采用单行 `key=value`，标识字符串尽量同时带 `StringId` 和人类可读名称。`traceId` 用于关联各阶段。

示例（**虚构格式样例，不代表已测到的数据**）：

```text
[Debug] [王国官职][MarshalArmyDebug] phase=ENTRY trace=western_empire:garios:91508:1 hero=加里俄斯 role=Marshal reason=READY_FOR_NATIVE_CHECK sizeRatio=0.83 influence=3407 foodDays=29
[Debug] [王国官职][MarshalArmyDebug] phase=ELIGIBILITY trace=... result=true candidates=5 nativeModel=DefaultArmyManagementCalculationModel leaderGateReplacements=2
[Debug] [王国官职][MarshalArmyDebug] phase=ARMY_CANDIDATE trace=... party=... sizeRatio=0.72 foodDays=21 siegeStrength=190
[Debug] [王国官职][MarshalArmyDebug] phase=TARGET_NATIVE trace=... type=Besieger target=town_x ourStrength=1014 nativeScore=0.000000 reason=NATIVE_TARGET_ZERO_REASON_UNKNOWN
[Debug] [王国官职][MarshalArmyDebug] phase=TYPE_SUMMARY trace=... type=Besieger nativeCalls=5 added=5 positive=0 zeros=5 multiplier=0.731
[Debug] [王国官职][MarshalArmyDebug] phase=FINAL_POOL trace=... topArmy=0.000000 topOther=0.421000 armyTopIsHighest=false
[Debug] [王国官职][MarshalArmyDebug] phase=CREATE_ATTEMPT trace=... leader=加里俄斯 allowedByMarshalPatch=true
[Debug] [王国官职][MarshalArmyDebug] phase=CREATE_SUCCESS trace=... armyLeader=加里俄斯 target=town_x
```

仅在实际发生时输出 `CREATE_ATTEMPT` 和 `CREATE_SUCCESS`。禁止为“没有事件”伪造对应事件行。

## 7. 一次游戏日志的诊断判读表

| 看到的证据 | 可以可靠地得出的结论 | 仍不能推出的结论 |
|---|---|---|
| `ENTRY` 后 `ELIGIBILITY_FAIL_NATIVE_UNKNOWN` | 本体资格检查返回 false | 无法仅凭这一行断言是影响力/粮食/身份门槛 |
| `ELIGIBILITY_PASS` 但 `candidates=0` | 当前没有可用候选 | 不等于全王国没有其他部队 |
| `TYPE_SUMMARY` 没有 `TARGET_NATIVE` | 未进入已监测到的原版逐目标评分函数，或观察补丁未命中 | 不等于必定“无敌方目标” |
| 逐目标 `TARGET_NATIVE nativeScore=0` | 原版目标评分返回 0 | 未必是兵力不足，需进一步分支取证 |
| 原版评分为正，但新增军团分数为 0 | 归零发生在外层目标评分组合/倍率/候选处理 | 不能直接怪罪原版目标战力阈值 |
| `FINAL_POOL` 最大军团分低于非军团分 | 至少该轮最终候选池不是军团占优 | 不能声称“军团绝不可能选中”，还受随机/重新决策影响 |
| `CREATE_ARMY_REJECTED_BY_OFFICE_PATCH` | 本模组官职权限 Prefix 明确拒绝此次创建 | 与目标分数归零是不同阶段 |
| `CREATE_ARMY_ALLOWED...` 且 `CREATE_ARMY_SUCCESS_EVENT` | 本次已成功创建军团 | 不保证后续军团一定集结完或执行围城 |
| 有正分军团候选但没有 `CREATE_ARMY_ATTEMPT` | AI 评估过但该轮没有进入创建调用 | 具体是随机/行为切换/其他门槛，需结合行为链进一步缩小范围 |

## 8. 日志节流、性能和兼容要求

1. **只对玩家王国的大元帅与统治者记录**，否则原版全地图 AI 评估会使日志极大膨胀。
2. 详细抽样每 Hero 每游戏日一次，状态变化时额外摘要；一次跟踪下目标最多 12 个零分、5 个正分，单轮摘要不得吞掉“全部为零”的计数信息。
3. `FileDebug` 已存在；不增加 MCM 项。用户测试前在 ModifiedPolitics 的 MCM 中将 **Write logs to file = 开启**、**File log level = Debug**。默认 File log level 是 Info，若不改则 Debug 不落盘；游戏内 `Minimum log level` 可维持 Notice。
4. 当前文件路径由 `ModLogger.cs` 决定：`文档\Mount and Blade II Bannerlord\Configs\ModLogs\ModifiedPolitics.log`。已有日志为追加写入。
5. 原生评分截获在 `[ThreadStatic]` 标记的调用栈内，仅保留普通字符串/数值快照；`finally` 保证退出；不要持有集合枚举器跨回调，不修改 AIBehaviorScores。
6. Harmony 新补丁采用**精确目标和签名**，不要使用大范围模糊 `PatchAll` 方法搜索，不要自创 `CanLordCreateArmy` 的反编译方法体。原有 Harmony Patch 顺序和身份替换逻辑保持不变。
7. 反编译参考里 `AiMilitaryBehavior` 存在 4 参数版本并有缺省参数，但 Mod 当前用 3 个实参调用；不得以调试为由改为其他调用路径。若安装 DLL 与仓库 `code/` 不一致，先核对元数据再确定 Harmony 目标。
8. 诊断代码不得影响游戏流程：日志方法内的可选字段访问应 null-safe；任何耗时异常/格式化问题不要使 AI 评分崩溃；但**不得吞掉原版方法的异常**。
9. 成功事件和权限拦截保留完整实时记录，不受每日采样限制；这两个事件发生频率远低于每小时目标评分。

## 9. 推荐实施顺序与验收

### 实施顺序

**阶段 A（优先完成）**：D01 / D02 / D03 / D04 / D07 / D08。只改 `MarshalArmyAiBehavior.cs`、`MarshalArmyPatches.cs`，新增少量诊断辅助类。应该能回答“资格还是评分还是创建调用”三个大问题。

**阶段 B（关键精细定位）**：D05，添加一个只读的原版目标评分 Harmony Postfix，证明原版零分还是模组组合评分零分。

**阶段 C（如仍有正分却不组军团）**：D06，添加一次事件后候选池 Postfix，确认军团候选最终分数与其他 AI 行为分数的竞争。此阶段不要尝试拦截/修改随机决策。

### 静态检查

- [ ] 所有原有 `return`、`if`、计算公式和原版方法调用次数完全保持不变。
- [ ] 未修改 `CanLordCreateArmy`、`Kingdom.CreateArmy`、`GetTargetScoreForFaction` 的返回值与参数。
- [ ] 未修改 `code/`、`ModifiedArmy`，没有 DLL 反向依赖或新增存档字段。
- [ ] 编译 `ModifiedPolitics Release`，0 错误；原有 Harmony 补丁仍注册成功。
- [ ] 只读 Patch 的 Postfix 编译签名与实际安装版 DLL 相符，记录 patch 目标方法的完整签名用于后续比对。

### 实机检查

- [ ] 玩家作为西帝国成员载入存档，文件日志等级设置 Debug；跳过 1～3 个游戏日。
- [ ] 至少对加里俄斯记录一次完整 TraceId：入口、资格、候选、各类型评分、逐目标原始分数、最终候选池。
- [ ] 若原有 Info 日志仍是 `新增目标数=5/正分=0`，新 Debug 应能明确区分“原版目标分为零”与“外层归零”。
- [ ] 若 `CanLordCreateArmy` 返回 false，日志明确写 `NATIVE_UNKNOWN` 并附当前资格快照；不伪造失败细目。
- [ ] 通过控制台/正常 AI 创建一支军团（若游戏流程允许），能看到 `CREATE_ARMY_ATTEMPT` 与 `CREATE_ARMY_SUCCESS_EVENT`；如本轮无法发生真实创建，标记此项 `NOT_OBSERVED`，不要伪造成功。
- [ ] 保证 Debug 与非 Debug 模式下 **AI 行为、候选数、创建结果和游戏运行稳定性没有因日志变更而出现差异**。随机性测试应在同一存档/类似环境中多次观察，避免对随机 AI 做严格逐帧完全一致的错误断言。
- [ ] 关闭文件 Debug 后，不持续输出逐目标明细；检查 1～2 日不会出现过量日志或报错。

### 编码 AI 最终交付

1. 修改文件列表和每处新增诊断的具体路径/行号；
2. Release 编译结果、Harmony 目标签名校验结果；
3. 单独列明 Debug 示例日志、证据对应的 TraceId；
4. 明确声明没有改变任何 AI 决策或机制；
5. 若存在版本差异导致无法观测内部原因，报告 `BLOCKED` 和实际差异，不进行侵入性重写或添加未经授权的规则。

## 10. 对编码 AI 的最终指令（可直接复制）

> 基于 Git 仓库 `GuihuaZhou/bannerlord` 当前 `main` 分支，仅实现本文件 D01～D08 的诊断日志。**不得修复或更改大元帅 AI 行为**。使用 ModifiedPolitics 已有 `ModLogger.FileDebug`，只记录玩家王国的正式大元帅和国王；对高频评分抽样节流。使用只读 Harmony Postfix 观察游戏本体真实评分和事件后候选池，不二次调用评分模型，不篡改返回值/候选/随机数。用 `ArmyCreated` 事件确认真正创建。不要修改 `code/` 本体参考文件、`ModifiedArmy`、MCM 设置、存档结构。完成后给出源码差异、编译结果及新日志的判读指南；若游戏当前 DLL 的签名不符，先核对反射信息并报告，禁止猜测或强行覆盖原版逻辑。
