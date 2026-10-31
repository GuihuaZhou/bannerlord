# ModifiedArmy · Clan 主动储备与驻军一次性招募订单实施方案

- 方案版本：V1.0 · 2026-10-30
- 仓库：`GuihuaZhou/bannerlord`
- 核验基线：`main`，Git HEAD `fdf5a22febcdbb738db3adc35599cd74c5b992bc`（2026-10-29 15:55:42 UTC，提交 `feat(ModifiedFamily): add marriage negotiation background assets`）
- 交付类型：**设计与 Worker 执行方案；本次没有修改仓库代码、提交 PR 或进行游戏内验收。**
- 核心决策：**按照剩余驻军库存和 Clan 财政富裕度主动招募，而非等待战损或其他事件；职业兵和俘虏实际加入驻军均扣减同一份一次性订单的剩余数量。**

## 0. 适用边界和明确规则

1. 范围：**AI Clan 的城镇/城堡驻军新增士兵**。玩家 Clan 驻军原有 UI 与自动招募语义在 V1 中保持不变；若后续想为玩家增加手动订单，独立开发 V2。
2. 招募源：当天的合法职业兵候选与合法、已满足驯服度的俘虏；**两者共同完成同一份数量订单**。只有实际增加驻军并完成成功事务才计数；仅生成候选不计数。
3. 无订单则 AI 驻军不新增职业兵，也不转化俘虏；**封邑兵每天补充、俘虏驯服度累积、俘虏保留与合法出售、粮食和后勤等独立逻辑继续运行**。
4. AI 每天基于驻军剩余人数、财富、净收入、现有费用、训练能力、编制与工资容量判断是否创建/调整订单。**不得引入“战损事件”“Party 缺额事件”作为订单生成前提**。可以在计算规模时参考当前 Clan 兵力结构，但不能以某个军事事件触发。
5. “一次性”的定义：订单设定明确的本轮授权人数；累计实际招募达到授权数后删除。下一轮仅当库存重新跌至启动线、冷却期结束且财政允许时，重新下达**新订单 ID**；不是复活旧订单。
6. 正常驻军调出保留线采用此前讨论的**真实留守士兵75名**，不是75%或“征粮护卫占位凑齐75”。这个改动属于需要独立验收的配套规则。返回的伤员、征粮护卫预留仍参与硬容量，不能把不存在的驻军当作可调出兵员。
7. 绑定 Party 只允许从自己的绑定城镇/城堡取得封邑兵与职业驻军兵，保留**前往其他友好/中立城镇寻找雇佣兵**的现状；不更改原版 AI 移动指令所有权。
8. 小派系、雇佣兵 Clan 不纳入本次 AI Clan 驻军储备调度；按现行符合资格的正规 Clan 和有合法驻军的定居点进行。不要据此修改小派系的 Party 绑定代码。
9. 禁止建立新的可累积虚拟士兵池、金币账本或封邑服役账本；不追溯撤销实际已经招募的士兵。

## 1. GitHub HEAD 实际代码核对

### 1.1 财政：现有共享预算必须原样复用

`ModifiedArmy/ModifiedArmy/Recruitment/Finance/ClanRecruitmentBudgetManager.cs`

- Clan 日净收入：`ClanFinanceModel.CalculateClanGoldChange(clan,false,false,true)`。
- Clan 日支出：`abs(CalculateClanExpenses(clan,false,false,true))`。
- 储备：`reserve = max(30000, 14 * dailyExpenses, 0.4 * clan.Gold)`。
- 收入预测：若净收入正，`net * 0.10 * 7`；否则 `net * 1.50 * 30`。
- 安全余力：`safeCapacity = max(0, clan.Gold + projectedIncome - reserve)`。
- 日共享**承诺预算**：`0.10 * safeCapacity`；日共享**购买预算**：`min(承诺预算,max(0,clan.Gold-reserve))`。
- 招募成功后，`CommitRecruitment` 扣除 `unitRecruitmentCost * count` 和 `unitDailyWage * 30 * count` 的承诺预算。不同 AI Party、驻军共用同一份日缓存；跨日重算；规划本身不消费。
- AI 驻军的实际购买费用同时进入 `Clan.AutoRecruitmentExpenses`；玩家驻军走实际金币特殊分支。**不得把订单数量转换成新的金币预留或重复扣费**。

`Recruitment/Models/RecruitmentBudget.cs`：已有 `90% * party.PaymentLimit` 的工资上限、30日维护预算及即时购买资金审批。领主实际购买可受领主持有金币约束；驻军使用 Clan 中央支出。

`PartyManagement/Finance/Models/AiRecruitmentFinancialModel.cs`：长期工资使用`GetTotalWageWithoutFiefExemption`，会考虑封邑兵免薪期结束后实际工资，战争期 WarParty 还有1.5倍工资；**不要使用当前免薪账面工资代替长期工资**。

### 1.2 驻军新增兵员现状

`Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` 当前订阅 `DailyTickSettlementEvent`，每座定居点单独执行：

1. `SettlementManpowerProductionBehavior.EnsureDailyProduction(settlement)`；
2. 可用时创建驻军、检查自动招募开关、围城/战斗状态；
3. `SelectAndPreparePrisonerCandidates`：保留合法俘虏、增加驯服进度、按现有政策出售多余俘虏；
4. 读取当日职业兵候选，与俘虏候选合并；
5. 使用 `RecruitmentModelManager.Model.BuildPlan(garrison,candidates)`；
6. `GarrisonRecruitmentExecutionService.Execute` 实际增加驻军、写入 `AutoRecruitmentExpenses`、扣 Clan 当日预算；
7. `GetMaximumDailyAutoRecruitmentCount(town)` 继续控制单日名额。

`Recruitment/Services/GarrisonRecruitmentExecutionService.cs` 当前统一执行职业兵、俘虏两种来源，已经分别输出 `professionalRecruited`、`prisonerRecruited`；可用作订单实际完成量唯一来源。

**两处拦截旧软目标**：

- `Recruitment/Models/DefaultAIRecruitmentModel.cs`：`GetRecruitmentTarget` 对驻军调用 `GarrisonRecruitmentCapacity.GetAutomaticRecruitmentTarget()`。
- `Recruitment/Services/GarrisonRecruitmentExecutionService.cs`：实际执行时再调用 `GetSoftRecruitmentFreeSlots()`。

因此，**仅在 Daily Behavior 外层加 `remaining` 不够**，仍会被旧的80%/95%软目标拦下。必须仅针对“有订单的 AI 驻军新增兵员”替换这两道约束；不应直接删除所有 `GetUpperTarget`，因为其他驻军转入、解散、运输逻辑可能使用该方法。

### 1.3 同一个每日生产行为还负责封邑兵

`Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` 的 `EnsureDailyProduction()`：

- 使用每日幂等键 `_professionalOfferDays`；
- 在一次调用中同时计算专业训练进度及封邑兵日补充；
- 专业训练得到的是**当日临时可用候选 offer**，非永久新增到驻军；
- `TryConsumeProfessionalOffer` 才消耗真实生产资源；失败有 `RollbackProfessionalOffer`；
- `BuildDailyProfessionalOffers` 在候选生成时更新加权生产分布账本；这本来就用于分配候选，不等于“订单完成”，不要将候选数记作实际招募。

因此没有订单仍可以调用 `EnsureDailyProduction()` 来推进封邑兵生产；职业兵 offer 到日终失效，无存储池、不加入驻军、不扣订单。**不建议本阶段重写职业兵加权历史账本**，若日后要让“订单先限制兵种候选生成”，应作为独立兵种订单 V2 实现。

### 1.4 驻军容量、防务与 Party 绑定

- `GarrisonRecruitmentCapacity` 有真实驻军数、征粮队返程预留、硬空位、软招募上限、转入上限。当前配置有**内陆上限80%、边境上限95%、下限50%/75%**，均为比例。
- `ForceManagementModels.cs` 中 `GarrisonDefenseRequirementModel.Evaluate` 用有效守军（真实驻军+征粮护卫预留）和下限比例计算驻军需求，`GetSafeTransferOutCount()` 也使用该值。
- `PartySettlementBindingBehavior` 已存档、恢复绑定、维护每个基地最多一个 Party，Party 最多两个基地；旧存档可有 LegacyUnbound Party。新招募订单以**定居点**为库存单位，不依赖 Party 一定已绑定。
- `AiForcePlanningBehavior` 将 Mod 补员意图提交给原版 AI 评分。不要让招募订单驱动 Party 移动。
- `GarrisonWageLimitModel` 已根据镇/堡、边境、平均工资、Clan 领袖财富设定工资上限。它的100/250（城堡）、200/400（城镇）是**工资计算的战略人数基准**，不等于新系统一定要填满的人数；可以只作为战略上限参考。

### 1.5 事件与存档

- `Main.cs` 注册 `SettlementManpowerProductionBehavior`、`GarrisonRecruitFromPrisonersBehavior`，`CampaignRecoveryCoordinator` 在最后。
- `CampaignRecoveryCoordinator.FinalizeRecovery` 顺序：封邑服役批次索引 -> Party 基地绑定 -> AI 任务验证 -> journal -> Ready。
- `FiefSaveDefiner` = Save ID `20251116`，当前 class ID `1..10`（10 为 `PartySettlementBindingRecord`），存在若干容器注册。
- 本次新订单只能在恢复完成后执行写操作，须增加旧档缺失键默认、读档重建、无重复注册与同日幂等检查。
- 仓库根 `AGENTS.md` 可用；其提到的 `ModifiedArmy/AGENTS.md` 与 `ModifiedArmy/CODE_ORGANIZATION_RULES.md` **在该 HEAD 树中不存在**，不得凭空声称已读。实施时若工作目录另有这些本地文件，应读取并遵守。

## 2. 决策模型：主动库存 `(s, S)`，不是事件驱动

每天对每个 AI Clan 评估旗下合法城镇/城堡，不需要战损、围城、缺兵事件：

- `H`：实际驻军名册人数（含在场伤员，不含尚在外出的征粮护卫）。**驻军实际保留75人的判断只看现场人数**。
- `I`：已经确认要返回本据点的士兵数（只在未来库存预测中可用，不能当现场守军）；不得把普通在外 Party 的总兵力预留给驻军。
- `Q`：现存有效订单未完成数量。
- `L`：驻军物理硬编制上限。
- `E`：已保留的外出征粮护卫/确认返乡占位；用于容量，防止超编。
- `W`：驻军工资及预计新增兵种工资（使用当前 `RecruitmentModel` 和工资模型；不能仅以人数做工资审批）。
- `F`：Clan 的非负 `safeCapacity`，从现有共享预算计算中读取，**建议仅在 snapshot 中暴露只读 `SafeCapacity`**，不复刻整套财务计算。
- `StrategicCeiling`：本定居点理论上值得储备的战略规模；建议调用 `GarrisonWageLimitModel.CalculateWagePaymentLimit` 已返回的 `desiredGarrisonSize` 作为参考，不以其作为必须达到的人数。

### 2.1 建议的可配置目标

```
ResidentFloor = min(75, L)
WealthFactor = clamp(F / (F + WealthSaturationGold), 0, 1)
GrossGoal = ResidentFloor + floor(
    (StrategicCeiling - ResidentFloor) * WealthFactor)
WageSafeGoal = 按预计新兵工资、0.9 * GarrisonPaymentLimit
               与当前长期工资计算的最大合法驻军规模
CapacityGoal = max(0, L - E)
S = clamp(GrossGoal, 0, min(CapacityGoal, WageSafeGoal))
s = ResidentFloor + floor(max(0, S - ResidentFloor) * ReorderFraction)
```

- 建议初始配置 `WealthSaturationGold=80000`、`ReorderFraction=0.50`、`MinimumNewOrderCount=5`、`OrderCooldownDays=3`、`ResidentFloor=75`。**这些是待实测调优的建议默认值，不是当前代码的已知平衡参数。**
- `DailyNetIncome` 已经进入 `F` 的保守预测，不需要再按盈亏重复乘以一个惩罚系数。
- `S` 是本轮仓储目标，`s` 是再次下单的库存线；`S` 不是永久补员编制，也不要求总是满编。
- `s` 与 `S` 每天都由当前真实财政重新计算。配置错误或没有工资容量时 `S` 可以低于75，**只能暂停招募，不得因此解除真实守军75人的正常调出限制**。
- 如果需要严格的“贫穷 Clan 仍尽量补到75人”，只是在订单生成中保留最低防御需求，**真正财政和工资审批仍不得越过**。
- 以上是一个容易落地的建议公式；Worker 可以复核 `StrategicCeiling` 的来源和现有工资限制 API，但不能临时另造未经评审的财富档位规则。

### 2.2 Clan 级预算规划约束（不是资金锁定）

同一个 Clan 有多个据点，不能每座都按相同 `F` 独自创建不可负担的大订单。

建议**在规划阶段**用候选订单的估算新兵招募成本及30日工资承诺求和，形成 `ProjectedOutstandingCommitment`；与 `PlanningCommitmentShare * F` 比较。建议初始 `PlanningCommitmentShare=0.35`（可配置、待实测），按优先级逐步批准订单数量。

此约束只决定**是否允许将多少未来需求写入订单**，**绝不从 `RemainingCommitmentBudget` 或 Clan 金币中实际预扣**。当天执行仍以共享预算实时审批为准；跨日重新计算，不能把今天的未用财政额度当明天存款。

价格和工资估算以本地当前合法生产模板及实际 `PartyWageModel` 为基础；拿不到可信兵种权重均价时，使用保守的高估值，而不是默认“每人免费”。预计成本只是规划参考，不替代 `BuildPlan()` 的逐 Troop 实时审批。

### 2.3 下单条件与实例

对无有效订单的定居点，当且仅当：

- `H + 已确认近期归队且不参与最低现场守备的 I` 低于 `s`；
- `S - H - I >= MinimumNewOrderCount`，或 `H < ResidentFloor`（允许小额防务订单）；
- 上次本轮订单结束已满 `OrderCooldownDays`，紧急缺少现场75人时可以跳过冷却；
- 有合法 Clan、合法驻军、开关允许、无围城/战斗阻断、支付能力及规划份额可行；

则创建 `AuthorizedTotal=min(S-H-I, 本 Clan 尚可授权的估算人数)` 的一次性订单，`RecruitedTotal=0`。

**举例**：`H=90, s=100, S=160`，创建70人订单。训练成功10、俘虏实际转化5，剩余55；此时 Party 调走20名已有驻军兵，**本轮订单不会因为转出而加回20人**，但下一轮在订单完成后达到新库存启动线仍可新下单。

### 2.4 每天调整现存订单（不复活历史）

记录本轮目标 `AuthorizedGoalAtLastRevision`、`AuthorizedTotal`、`RecruitedTotal`、`LastReviewedDay`。

1. 始终先用 `remaining=AuthorizedTotal-RecruitedTotal`，不能用 `S-当前驻军` 直接覆盖，避免当日转兵后重复补单。
2. Clan 财政下降、外部合法士兵进入驻军、驻军剩余容量下降时，允许减少未完成额度：`remaining=min(remaining,max(0,S - H - I))`；最小不能小于0。
3. 当财富和合法工资容量增加，且 `S` 比**上次授权时**明确提高时，允许按 `max(0,S-new/old goal 差额)` 追加本轮授权；不能将 Party 转出造成的 `H` 下降作为追加理由。
4. 当日共享额度为0只将状态标记为暂停，不直接删单；长期 `F=0` 或合法工资容量/所有权丧失时可以收缩或取消。
5. 一旦 `remaining=0`，立即归档最小结束信息（如定居点、关闭日期、订单代数），删除活动订单。**仅当新一次 `(H < s)` 判断和冷却再次满足才生成新订单 ID**。
6. 活动订单数量按定居点最多一份，AI V1 不做兵种分类订单。没有军事事件 ID、没有按战损事件触发。

**实现时建议写为纯函数** `ReviewOrder(snapshot, previousOrder) => NewOrderDecision`；不通过修改 `AuthorizedTotal` 隐藏“取消部分人数”历史，日志输出 `oldRemaining/newRemaining/reason`。

## 3. Clan 内排序与公平执行

**现实难点**：当前每座定居点各自触发 `DailyTickSettlementEvent`，同 Clan 的共享招募预算被事件顺序先到先得。

V1 新增 `ClanGarrisonRecruitmentCoordinator` 统一调度**AI Clan 的驻军订单**：

1. 在每日 Campaign 阶段，等恢复 Ready，先采集所有相关 Clan/据点快照；`GetSnapshot(clan)` 是只读日快照初始化，不预扣。
2. 按先 Clan 后据点的稳定顺序计算每个 `(s,S)`；以 **真实守军是否低于75、当前库存缺口比例、战略边境需求、订单等待日数、定居点 StringId** 排序。注意这些是“主动库存评分”，不是战损事件优先级。
3. 一个 Clan 的全部有效订单先修订、后按顺序执行；第二个城堡只能消费第一个城堡实际成功招募后剩余的 Clan 预算。
4. 无有效订单的据点不能新增职业兵/俘虏；但可以继续俘虏整理、驯服/出售、封邑兵补充。
5. 必须有**同日唯一协调入口**。在 AI 路径中取消旧 `GarrisonRecruitFromPrisonersBehavior.OnDailySettlementTick` 的独立招募写入，不能让协调器和旧事件双双招募。
6. 不要求用 Coordinator 控制领主 Party 的异地雇佣兵购买，保持现有“抵达后即时交易”逻辑；因此同一天的临时领主招募仍可能先消费共享预算。**V1 保证 AI 驻军间排序，不保证压过当日所有移动领主事务**。
7. 保留 `GetMaximumDailyAutoRecruitmentCount` 的**定居点每日总名额**，职业兵+俘虏共享；不要偷偷增加训练速度或修改文化军事制度。
8. `EnsureDailyProduction` 有每日幂等保证，协调器可主动调用，不能依赖事件监听器恰好先执行。务必先检查生产调用对封邑补员的时序及读取数据完整性。

### 3.1 建议事件接线

- 新增 `GarrisonRecruitmentOrderBehavior`：唯一持久化订单状态及日戳，提供读取/修订/完成 API。
- 新增 `ClanGarrisonRecruitmentCoordinator`：**单一每日调度行为**，注册一次 `DailyTickEvent`，先遍历所有 AI Clan，再按分数排列其辖下据点；每个据点最多招募一次。
- 原 `GarrisonRecruitFromPrisonersBehavior`：保留 `NormalizeGarrisonNow` / 周期清理、既有日志/俘虏准备 helper；把其 AI 驻军日招募主体抽成可被 Coordinator 调用的 `RecruitSettlementUnderOrder(...)`，避免复制代码。
- 原玩家 Clan 驻军分支仍可保留旧 `DailyTickSettlementEvent` 入口，但增加明确 `Clan.PlayerClan` 互斥判断，确保 AI 只有协调器负责。
- 具体 `DailyTickEvent` 在原生财政结算与 `DailyTickSettlementEvent` 前后的真实顺序须通过源码/实机诊断确认；不要以 `Main.cs` 注册顺序猜测。所需生产由 `EnsureDailyProduction` 幂等提供，并对工资上限在当天执行前复验。

## 4. 审批与事务：两源合单扣减

### 4.1 统一合法数量上限

对每座 AI 驻军、每个订单：

```
maxIntake = min(
    OrderRemaining,
    GetMaximumDailyAutoRecruitmentCount(town),
    HardFreeSlots(garrison, activeEscortReservations),
    WageAndFinanceModelApprovedCount
)
```

其中 `WageAndFinanceModelApprovedCount` 必须来自**原有** `RecruitmentModelManager.Model.BuildPlan()` 的逐 Troop 评价，不能凭 Clan 总金币自创审批数。

- 修复 **审批层**：`DefaultAiTroopRecruitmentModel.GetRecruitmentTarget()` 在 `party.IsGarrison && AIOrderContext.IsAuthorized` 时，目标不能继续是 `GetAutomaticRecruitmentTarget()` 百分比上限，而应以订单剩余授权与硬编制约束为边界；最好使用新的**调用上下文/明确参数**（而非可变 static 当前订单变量）把订单数量传递给模型。
- 修复 **执行层**：`GarrisonRecruitmentExecutionService` 对有有效 AI 订单的新兵招募**不再执行 `GetSoftRecruitmentFreeSlots()`**，改为订单剩余额度与硬容量的交集。**仅修改这一来源路径**，不得影响其他驻军转入/复员容量政策。
- 模型中 `RecruitmentSimulationState` 已跟踪实际驻军与护卫占位；要确保多候选同一次模拟中，批准量不能超订单授权值。建议把“订单额度”放入 simulated intake budget（和 projected member count 同步递减），而不是在模拟后仅 `Math.Min` 全部审批合计。
- 两种来源继续按照当前模型 `GetPriority` 排序；**本期不重新引入兵种配比/指定兵种/高阶质量门槛**。

### 4.2 一个订单，两类成功量

`GarrisonRecruitmentExecutionService` 现已返回两个计数：

- `professionalRecruited`：实际 `ProfessionalRecruitmentService.TryCommit` 成功；
- `prisonerRecruited`：实际 `PrisonerGarrisonRecruitmentService.TryCommit` 成功；

因此 `TotalCommitted = professionalRecruited + prisonerRecruited`，在**实际新增名册成功、费用入账、共享预算提交均成功后**应用：

```
Order.RecruitedProfessional += professionalRecruited;
Order.RecruitedPrisoner += prisonerRecruited;
Order.RecruitedTotal += TotalCommitted;
Order.Remaining = max(0, Order.AuthorizedTotal - Order.RecruitedTotal);
```

在真实事务成功前，任何候选 offer、归化成熟、`BuildPlan` 审批、预测值、调兵/归还都**不扣减订单**。

建议 `Execute` 返回结构化 `GarrisonRecruitmentExecutionResult`（已成功数+实际来源+操作 ID+失败原因），而非由 Coordinator 猜测 `MemberRoster.TotalManCount` 前后差（同日可能有其他转兵、伤员变化、接收流程）。

### 4.3 资源写入和失败补偿必须复核

当前成功路径顺序大致：职业兵 offer 扣除/村庄资源消耗 -> 驻军名册新增 -> `AutoRecruitmentExpenses` -> `CommitRecruitment` -> 返回计数。

**现有 `ProfessionalRecruitmentService.TryCommit` 已对自己的 offer+roster 变动实现 rollback；但对“之后预算或费用入账可能失败”的整个跨组件事务并未证明完全原子化。** 本阶段必须将“订单完成数 + 入驻士兵 + 资源支付 + Clan 预算”作为一个有明确定义的最终成功边界。

Worker 应先审计具体写入及异常边界：记录前置 roster/offer/村庄兵源/`AutoRecruitmentExpenses`/共享 budget，失败优先在安全补偿路径撤销；无法确认全量恢复时，记录恢复冻结状态而不能默默删掉订单。参考现有 `TransferOperationJournalBehavior` 的永久操作 ID/冻结思想，但不要直接将不兼容的转兵 journal 强行当订单账本。

在最小改动方案里，可以将每日 AI 驻军招募拆成**单次有 operation ID 的有界提交**，避免成功入驻后又重复扣单；跨存档重复执行前检查已提交事实。结构化日志报告 `offerConsumed/rosterChanged/expenseCharged/budgetCommitted/orderDebited`；日志不可重复执行任何事务。

### 4.4 俘虏预处理和玩家兼容

- `PrisonerGarrisonRecruitmentService.SelectAndPrepare` 负责合法保留、驯服度提升及卖出，**无订单依然可以执行这部分**；只禁止真正 `TryCommit` 归化入驻。
- 保留现有合法兵种、俘虏等级下限（至少 T3 等）、成本、工资的模型规则。
- 玩家 Clan 驻军执行入口 V1 原样保留，不能让新增 AI 订单互斥误关玩家自动招募；玩家手动指定 Troop 属于 V2。

## 5. 订单持久化和读档迁移

推荐 `Recruitment/Orders/` 或 `Garrison/Recruitment/` 作为单一业务目录，按 `Models/`, `Behaviors/`, `Services/`, `Diagnostics/` 分职责；不要把订单塞入 `FiefPartyManager` 作为第二职责。

保存记录可用：

```
GarrisonRecruitmentOrderRecord {
  OrderId, SettlementId, OwnerClanId,
  AuthorizedTotal, RecruitedProfessional, RecruitedPrisoner,
  AuthorizedGoalAtLastRevision,
  CreatedDay, LastReviewedDay, LastExecutionDay,
  Revision, State, LastReasonCode
}
```

- `Remaining` **计算属性**：`max(0, AuthorizedTotal - RecruitedProfessional - RecruitedPrisoner)`，不要再保存可能不一致的第三份 Remaining。
- 每定居点至多一份有效订单，`OrderId` 永不复用。额外保存 `LastClosedDay`/`Generation` 等最小防抖信息，不保存已招兵的个人身份。
- 建议新 `SyncData` key，如 `modifiedarmy_garrison_recruit_orders_v1`，旧存档无该键时初始化为空，首次 Ready 后每日评估自动生成订单；**不把旧的“百分比目标差额”迁移成一次性订单**。
- `FiefSaveDefiner(20251116)` 的 ID `1..10` 保持不变；如注册新记录，**只分配新的未使用 ID 11+**，注册所需容器，检查 Saveable 属性 ID 不复用。若在其他 Definer 中注册，先确认其 ID 及生命周期不会冲突。
- 读档后校验 settlement/owner 是否仍存在且合法、数量是否非负、订单是否已完成、同据点是否重复、上次成功操作是否可核实；错误订单取消或冻结而不移动现有士兵。
- `CampaignRecoveryCoordinator` 在 Ready 前新增 `RevalidateRecruitmentOrdersAfterRecovery`，放在绑定/封邑批次恢复之后、允许每日协调写入之前；旧档加载不立即转兵，不额外生成模板士兵。
- 定居点易主/消失：旧 Clan 订单无效，丢弃未执行数量；新所有者下一日按自己财政和库存独立建新订单。不追溯已成功招募兵员。
- Clan 财政预算是**每日易失缓存**，**不能序列化到订单**；订单保存的是需求和执行进度。

## 6. 75人守军保护与战略库存的接口分离

当前代码使用 `GetLowerTarget()` 的比例型防务下限，`GarrisonDefenseRequirementModel` 又把征粮护卫预留算作 `EffectiveGarrisonCount`，可能发生“现场100、护卫50，判断有150，可转出至现场低于75”的语义冲突。

建议不要直接全局将所有比例设成75%，而是新增明确的**真实驻军调出保留线**：

```
safeTransferOut = min(
  availableRegularsExcludingProtectedFief,
  max(0, ResidentRegularCount - min(75, ActualHardLimit))
)
```

这条规则用在职业驻军兵派往绑定 Party、普通支援出兵等“主动离开驻军”业务中；不把返程护卫计入 ResidentRegularCount。

同时，将**战略防御需求**和**调出硬底线**分开：外部增援、预计七天自我补员的 `GarrisonDefenseRequirementModel` 如仍需要高于75的战时防御标准，可保留独立战略阈值，但不得再错误地替代真实调出75的判定。复员/征粮护卫返乡必须按真正可用硬容量和来源保护规则走，不因“驻军一次性订单存在”而拒收。

这一部分会影响现有 `GarrisonTransferPolicyModel`、`ForceManagementModels.cs`、`PartyExchangeModel`、`ReinforcementTransferService` 的准入链。**作为独立小阶段统一修复，而不是对一个路径加临时补丁。**

## 7. 配置建议

在 `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` 的 `AiRecruitment.ForceManagement` 下增加嵌套 `GarrisonRecruitmentOrders` 节点，配置归类遵循现有结构：

```xml
<GarrisonRecruitmentOrders enabled="true">
  <ResidentFloor value="75"/>
  <WealthSaturationGold value="80000"/>
  <ReorderFraction value="0.50"/>
  <PlanningCommitmentShare value="0.35"/>
  <MinimumNewOrderCount value="5"/>
  <OrderCooldownDays value="3"/>
</GarrisonRecruitmentOrders>
```

- 以上数字是**建议初值，不是已经在仓库中存在的值**。
- `enabled=false` 时回退到旧 AI 驻军自动招募机制；避免禁用新系统后驻军突然完全不招兵。玩家 Clan 行为不变。
- 保留 `MinimumFiefRecruitBatch=25`、现有文化军事制度和职业兵训练公式。
- 若 `ForceManagement` 节点缺失或旧配置没有新节点，使用可靠默认并正确输出配置加载诊断；旧配置不因新增可选节点启动失败。
- 对 `ResidentFloor/WealthSaturationGold/ReorderFraction/PlanningCommitmentShare` 做非负、上下界验证；非法配置明确报错或安全回退，不悄悄使用 NaN。

## 8. Worker 分阶段实施（每阶段单独验收）

### W0 基线核验（不改行为）

1. `git status --short`、`git rev-parse HEAD`、确认 `main`/工作分支；查看根 `AGENTS.md`，如工作树存在模块级 AGENTS/组织规则则读取。
2. 核对上节所有真实调用链及原生 `DailyTickEvent`/`DailyTickSettlementEvent` 的触发顺序。
3. 确定哪些路径使用 `GetUpperTarget/GetLowerTarget`，列成调用方表；确认玩家、俘虏、封邑、征粮独立行为。
4. 记录当前旧存档数据：驻军真实人数/护卫预留/Clan预算/单日招募/可调出/Party 绑定，不修改数据。

### W1 库存规划模型与只读快照

- 新建 `GarrisonStockPlanningModel`（纯判断，不写游戏状态）、`ClanGarrisonStockPlanningModel`（全 Clan 预算与排序）、决策结果 Contract。
- 为共享 `ClanRecruitmentBudgetSnapshot` 暴露 `SafeCapacity` **只读**，不更改公式和扣减方法。
- 引入 `(s,S)` 算法、规划占用承诺估值（不预扣）和订单冷却设计。
- 为普通无条件/财政不足/围城/工资容量不足生成确定的 `ReasonCode`，单元测试边界值。

### W2 订单存档与每日调度

- 新建 `GarrisonRecruitmentOrderBehavior` 持久化、校验、读取、修订、关闭。
- 新建 `ClanGarrisonRecruitmentCoordinator` 统一每日 AI 驻军执行顺序，单日幂等、先规划后提交。
- 在 `Main.cs` 按业务恢复顺序注册，`CampaignRecoveryCoordinator` 加入读档重建屏障。
- 保持 `GarrisonRecruitFromPrisonersBehavior` 每周清理/玩家支路，AI 日事务收归 Coordinator **唯一入口**。

### W3 共享审批与职业兵/俘虏共用订单

- 对 `DefaultAiTroopRecruitmentModel` 增加明确的 `AiGarrisonOrderRecruitmentContext` 或独立参数化 API，不能用 static 当前订单；新增订单剩余限制，并去除该路径上的旧80%/95%软限制。
- 对 `GarrisonRecruitmentExecutionService` 增加 `maxOrderCount` + 结构化真实执行回执，显式跳过旧软限制，仅保留硬容量/工资/财政/日限。
- 保留 `EnsureDailyProduction` 同时产生职业兵候选和封邑兵；无订单下不实际入驻职业兵或俘虏。
- **成功1名职业兵减1，成功1名俘虏减1**，同一份订单、同一日限，不额外按兵种开订单。
- 审计提交后异常/回滚/存档幂等边界，不能出现“名册+1但订单没扣”“订单扣了但名册没加”与双重记费。

### W4 75名现场最低留守 + 预测联动

- 引入真实现场75人调出硬底线；不要复用征粮返程预留当作现场守军。
- 保留硬容量对返回护卫与复员的保护。
- `GetPredictedDailyRecruitmentCount` 在 AI Clan **无订单时为0**；有订单时预测值不能超过订单剩余、本地训练/俘虏成熟能力、日限、预计财政与容量。理论训练能力须另提供不消费数据的 read-only 预测，避免预算/生产预览调用引起写入。
- 外部驻军增援规划不得把无订单的职业兵产能视为已经确定会新增的士兵。

### W5 配置、日志、旧档与发布验收

- XML 配置、配置类、合法化校验和中文本地化。
- 订单生命周期日志：`ClanId/SettlementId/OrderId/PlanDay/StockOnHand/TargetS/ReorderPoint/ClanSafeCapacity/OutstandingPlanCommitment/OrderBefore/OrderAfter/ProfessionalActual/PrisonerActual/BudgetRemaining/Reason`。
- 日志 `Debug` 为高频详述，`Info` 为实际完成与取消，按玩家王国过滤，所有 UI 文案使用本地化而非 C# 硬编码中文。
- 老存档没有订单时不重写任何历史兵源，首次调度正常建单。
- 执行已存在的 `ModifiedArmy/tools/Invoke-ReleaseValidation.ps1`、`Test-ArchitectureReleaseGate.ps1`、`Test-RuntimeAcceptanceEvidence.ps1`（先确认参数与仓库实际存在），再进入实机矩阵；编译通过不等于实机验收完成。

## 9. 必须通过的实机/单元测试清单

| ID | 场景 | 期望 |
|---|---|---|
| T01 | 财政富裕、驻军低于 `s`、无战斗或补员事件 | 自动创建一次性库存订单 |
| T02 | 财政良好、驻军处于 `s` 和 `S` 之间 | 不创建新订单 |
| T03 | 没订单、职业兵 offer 大于0 | AI 驻军职业兵实际新增0、封邑兵照常生产 |
| T04 | 没订单、可用俘虏已驯服 | 只处理驯服/保留/出售，不归化入驻 |
| T05 | 订单剩余10，今天职业兵成功2、俘虏成功3 | 剩余5，总新增5 |
| T06 | offer 3、Plan 审批0 | 订单不扣减，驻军不增加 |
| T07 | 订单剩余1，今天 offer4、俘虏3 | 真实新增最多1，不重复扣/超订单 |
| T08 | 订单剩余30，Party 从驻军调出15 | 订单仍剩余30，不因取兵补加旧订单 |
| T09 | 订单已完成，后续库存低于 `s` 但冷却未到 | 不立即重建旧单 |
| T10 | 订单已完成，后续库存低于 `s` 且冷却到 | 允许创建新的 OrderId |
| T11 | Clan 财政恶化但单日预算暂为0 | 暂停，不擅自扣订单或超额购买 |
| T12 | Clan 财政由紧张转富裕 | 经过明确目标修订才允许扩单 |
| T13 | 两座驻军同 Clan 同日招募 | 按统一顺序，共享同一财政快照/递减额度 |
| T14 | 高优先级据点无兵源 | 不锁住预算；低优先级合法据点可继续用款 |
| T15 | 老存档、订单键缺失 | 正常读取，首次日评估新建订单，无复制士兵 |
| T16 | 保存/读取进行中的订单 | 数量、ID、统计、修订代数准确 |
| T17 | 定居点易主 | 原 Clan 未完成订单失效，新 Clan 独立决策 |
| T18 | 驻军现场74、外出征粮护卫50 | 不允许以124“有效兵力”调出一人使现场更少 |
| T19 | 驻军现场76、护卫预留50 | 普通调兵最多1人，返程硬容量继续保护 |
| T20 | 驻军上限/工资上限紧缩 | 招募按真实约束缩减或暂停，绝不超限 |
| T21 | 玩家 Clan 开启原有驻军自动招募 | 玩家原逻辑不受影响 |
| T22 | Party 绑定基地取驻军兵 | 不受新订单数量限制；仍按绑定、守军与工资审批 |
| T23 | Party 离开基地到外城招募雇佣兵 | 仍可正常执行 |
| T24 | 单次招募业务部分失败或异常 | 成功提交与订单扣减一致；不一致时冻结/补偿并记录 |
| T25 | 读档恢复且同一游戏日多次 Tick | 无双重招募、无重复费用/扣单 |
| T26 | 全图多个 Clan、长时间运行 | 无失控日志、无逐日不断创建+删除零人订单 |
| T27 | 旧的 `GetUpperTarget` 相关返乡/运输 | 不因新订单开放而绕过各自软/硬容量规则 |
| T28 | 订单禁用（XML开关） | AI 驻军回退原有自动招募，封邑/玩家/Party 均稳定 |

## 10. 不能混入本 PR 的事项

- 不修改 Party 创建50人的资格、Native `LordPartyComponent` 初始模板兵或基地绑定策略；这些已有独立实施计划。
- 不改 `AiForcePlanningModel`、原版移动 AI 评分、敌人规避与外出雇佣兵策略。
- 不修改 `ClanRecruitmentBudgetManager` 10%日额度、14天储备、30天工资承诺或净收入正负权重公式，仅可暴露只读安全资金快照。
- 不改变职业兵按文化、兵营、模板、加权账本生产；不新增长期训练池。
- 不重写封邑兵批次账本、最低征召25人或免薪期；不增加兵种角色配额系统。
- 不在 V1 为玩家提供手动精准兵种订单 UI；不破坏玩家已有控制。
- 不删除原版/现有俘虏保留、驯服、出售逻辑；只把 **AI 俘虏正式入驻** 纳入订单授权。
- 不删除所有 `GetUpperTarget`/`GetLowerTarget`：按具体业务路径更改，覆盖回归测试。

## 11. Worker 交付报告必填

1. 起始与结束 `HEAD SHA`、`git status --short`，列出修改文件与关键方法。
2. 旧招募入口与新 Coordinator 是否互斥，明确玩家 Clan 分支。
3. 预算是否仍唯一扣减、订单是否只按实际成功数量扣减；提供至少一笔职业兵和俘虏日志。
4. `GetAutomaticRecruitmentTarget` 与 `GetSoftRecruitmentFreeSlots` 的旧调用是否仅在订单 AI 路径被替代，其他调用有无破坏。
5. 旧存档存储键和 SaveDefiner 注册的兼容审查。
6. T01–T28 每项 `PASS/FAIL/BLOCKED` 及证据；只编译通过不能声称全部通过。
7. 不要在未获明确授权的情况下提交 PR、推送或改写仓库历史。

## 12. 核验源码入口（锁定 SHA）

- [ClanRecruitmentBudgetManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Recruitment/Finance/ClanRecruitmentBudgetManager.cs)
- [RecruitmentBudget.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Recruitment/Models/RecruitmentBudget.cs)
- [DefaultAIRecruitmentModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [GarrisonRecruitFromPrisonersBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs)
- [GarrisonRecruitmentExecutionService.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs)
- [SettlementManpowerProductionBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [GarrisonRecruitmentCapacity.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs)
- [GarrisonWageLimitModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonWageLimitModel.cs)
- [ForceManagementModels.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs)
- [PartySettlementBindingBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs)
- [CampaignRecoveryCoordinator.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/CampaignRecoveryCoordinator.cs)
- [FiefPartyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [MilitaryConfig.cs](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [militaryConfig.xml](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
- [仓库根 AGENTS.md](https://github.com/GuihuaZhou/bannerlord/blob/fdf5a22febcdbb738db3adc35599cd74c5b992bc/AGENTS.md)

---

**结论**：本实施方案既不依靠战损事件，也不重建资源/财政模型。它改变的是“谁决定驻军主动招兵，以及本轮招多少”：Clan 根据真实库存与保守财政每日审视，定居点有明确的单次订单，职业兵与俘虏的实际新增数量共同扣减，完成后删除，未来只有库存重新触达启动线才产生下一份新订单。
