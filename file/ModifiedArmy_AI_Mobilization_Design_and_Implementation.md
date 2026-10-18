# ModifiedArmy：AI军事动员、潜在兵力与封邑兵轮换——设计与实现方案

> **范围**：第二阶段，新增/重构AI军事动员行为；以第一阶段已完成为前提。  
> **前置文档**：`ModifiedArmy_FiefService_Design_and_Implementation.md`（封邑兵服役机制修正）。  
> **仓库**：[`GuihuaZhou/bannerlord`](https://github.com/GuihuaZhou/bannerlord)，参考 `ModifiedArmy/ModifiedArmy/` 的Mod源码和 `code/` 的本体反编译源码。  
> **核对时间**：2026-10-17；以源码路径、类名、方法名定位，避免依赖行号。  
> **文档性质**：设计与实现指导；**未修改、编译或测试仓库代码**。  
> **状态标签**：“已确定”是讨论达成的约束；“首版建议”是可配置初始值；“待验证”需要源码调用链/游戏内测试确认。

---

## 1. 第二阶段的边界

### 1.1 必须实现

1. **场景理想编制**：根据和平/战争/战前动员/Army集结/劫掠/围城/防守救援，确定Party目标人数；重构 `AiRecruitmentBehavior.CalculateNeedScore()`，不再默认按满编缺口补员。
2. **潜在兵力**：评估Party在已有兵源、财政、容量、距离和征兵规则约束下真正能够动员形成的额外人数及战力；用于军事规划，**不能充当已存在的战斗力**。
3. **Army与战略AI衔接**：适当降低AI被Army召集的兵力比例；处理本体创建Army、挑选成员和军事任务评分的不同入口，避免“兵力不足→不能选任务→没有征兵资格”的循环依赖。
4. **三类兵源的统一决策**：区分“需不需要征兵”“哪些兵源可使用”“从哪里、征多少”；职业兵来自Clan可调驻军，封邑兵来自所属封邑，雇佣兵来自可访问城镇。
5. **封邑兵成规模征召**：根据 `fiefPartyTemplates.xml` 现有具体兵种模板和权重制定供应方案；**单次AI成功征召至少25人**，审批通过人数不足25时不能实施。玩家手动征召不受此门槛约束。
6. **续役、战时轮换及和平复员AI**：免费服役结束后允许继续付薪服役；AI按付薪服役时长、返乡人数、距离、Army状态和替换能力决定轮换；和平期默认返还全部或绝大部分在役封邑兵。
7. **端到端约束与测试**：防止无限绕路、重复兵源占用、目标人数与实际审批冲突、持续征召/遣返震荡及任务丢失。

### 1.2 本阶段不重新实现

- `FiefTroopDetachment` 的Party绑定、`ServiceDays`、文化 `FreeWageDays`、独立批次存档、每日对账、升级树双向匹配、批次间公平分配与工资免薪：**使用第一阶段接口**。
- Hearth恢复、返乡冷却、普通兵损、Party解散/驻军转移的底层结算：**使用第一阶段接口**，不得再建立第二份服役账。
- 不恢复“独立封邑兵小队Party”、职业兵池或封邑兵池；第一阶段已有真实名册是唯一权威兵员来源。
- 不重写本体所有AI战略逻辑；选择**限定范围的评分与动员插入点**。
- **不引入 `NormalServiceDays`**。服役是否免费只由文化免薪期限决定，之后可以持续付薪服役。

### 1.3 阶段依赖图

```text
第一阶段（既有机制修正）
  批次 / 来源Fief / ServingParty / ServiceDays / FreeWageDays
  对账 / 免薪 / 返乡结算 / 冷却 / 生命周期
                 │ 提供只读查询与实际返乡API
                 ▼
第二阶段（AI军事动员）
  场景目标人数 → 补员需求 → 潜在兵力 → 任务预评估
       → 三兵源征召计划 → 实际招兵 → 军事任务
       → 续役 / 轮换 / 战后复员
```

---

## 2. 仓库现状：为什么必须重构

| 文件/入口 | 已核实的当前实现 | 第二阶段要改什么 |
|---|---|---|
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` · `OnDailyTick()` | 每天跳过 `party.Army != null`；按随机概率、NeedScore、兵源偏好选征召地点，并以 `SetMoveGoToSettlement()` 重派移动 | 支持Army正在集结的成员，区分普通补员和动员目的，确保已存在的军事任务不被反复覆盖 |
| 同上 · `CalculateNeedScore()` | `manpowerGap = 1 - party.PartySizeRatio`；再加战争紧迫度与经济承受力加权 | 用**场景目标人数缺口**，保证无缺口时不因其他加分凭空触发招募 |
| 同上 · `CalculatePreferredSource()` / `ApplySituationalModifiers()` | 已有文化基础权重和peace/war/siege/broke/critical修正 | 保留文化偏好；在评分之前增加**硬性兵源许可**，禁止和平普通行为招封邑兵 |
| 同上 · `FindBestFiefSettlement()` | 看Clan封邑、可用人数、文化匹配及可用比例；未确保拟招兵额≥25，未计实际绕路成本 | 以可审批人数、目的地、路程及任务窗口综合择址 |
| 同上 · `FindBestMultiPurposeSettlement()` | `PartySizeRatio < 0.85`即可能因驻军/俘虏而访问Clan城防；Army成员被排除 | 军队补员需求改为场景目标缺口，不能因战争/和平目标之外的人数长期绕路 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` · `CheckRecruiting` Prefix | 完全接管领主到达定居点招募；再次查 `GetRecruitmentDecision()`；仍有Party上限95%资格；兵源分支带概率 | 到达时使用目的及剩余目标/计划核验，避免原路程目的丢失或绕过25人底线；协调95%限制 |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` | `RecruitmentFillPercentage = 95`；角色、质量、工资、购买成本、维护预算共同限制批准人数 | 把可达目标人数作为上下文上限；进攻动员可达100%，日常遵守场景目标，而非所有情况套固定95% |
| `Recruitment/Finance/ClanRecruitmentBudgetManager.cs` | Clan共享每日预算；`GetSnapshot()`查询，`CommitRecruitment()`实招才承诺扣减 | 潜在兵力评估只读；多个Party共同评估时增加临时份额/分配，禁止同一兵源重复占用 |
| `PartyFinance/Models/AiRecruitmentFinancialModel.cs` | 已有免薪结束后的长期工资估计、单位工资估计、可承担人数 | 复用，潜在兵力计算按实际兵种做逐项审批，不能把免费服役当成永久免薪 |
| `Fief/Models/FiefPartyTemplate.cs` + `ModuleData/fiefPartyTemplates.xml` | 按文化与town/town_port/castle定义具体兵种条目及权重 | 单次AI征召按具体条目分配；可用名册、军费、兵种质量限制仍有效 |
| `Fief/Models/PartyData/FiefPartyRecruitment.cs` · `RecruitTroopsToParty()` | 当前按 `SoldierType` 权重和剩余Party上限造候选，再让统一模型批准；未接收场景目标数量参数 | 增加AI招募数量上限/审批前检查与≥25原子门槛；玩家手动路径保持原语义 |
| `code/DefaultArmyManagementCalculationModel.cs` | `AIMobilePartySizeRatioToCallToArmy` 当前0.6；`GetMobilePartiesToCallToArmy()`按 `PartySizeRatio` 过滤 | 降低被召集门槛（首版候选0.4），另行验证被召集后动员路径 |
| `code/AiMilitaryBehavior.cs` | 主动创建Army的 `AiHourlyTick()` 另有硬编码 `PartySizeRatio > 0.6f`；劫掠/围城/防守评分受当前规模影响 | 创建Army门槛与被召集门槛分别处理；军事准备阶段用潜在兵力考虑候选任务 |
| `code/DefaultTargetScoreCalculatingModel.cs` | 目标评分与 `ourStrength` 及目标守军/可能增援有关，弱小Party某些任务的评分可直接为0 | **仅战略预评估**可以试算潜在战力，不得改动即时遭遇敌军的真实战力 |
| 本体 `MobileParty` | `Army`、`AttachedTo`、`DefaultBehavior`、`ShortTermBehavior`、`Objective`、`TargetSettlement` 等信息已可读 | 使用已有信息判断当前状态，另保存短期的**动员目的**，不创造第二套完整军事AI |

**两个重要事实**：

1. `AiBehavior.RaidSettlement` / `BesiegeSettlement` 等并非可靠的征兵前置条件；由于本体行为评分依赖当前实力，强制先进入行为再给征兵权限会造成循环依赖。
2. `AiRecruitmentBehavior` 发出 `SetMoveGoToSettlement()` 后，Party行为会呈现前往定居点。**仅靠新的 `DefaultBehavior` 不足以判断它原来为什么要征兵**，所以动员目的或任务必须保留到到达/过期/取消。

---

## 3. 场景理想人数：首版配置

### 3.1 目标比例（首版建议，可配置）

| 情境 (`RecruitmentScenario`) | 配置键（建议） | 目标比例 | 上限150时的目标人数 |
|---|---|---:|---:|
| 和平日常活动 | `PeaceTargetRatio` | **0.35** | 53 |
| 战争中无明确作战任务 | `WarIdleTargetRatio` | **0.50** | 75 |
| 战前动员/计划中的作战任务 | `WarMobilizationTargetRatio` | **0.80** | 120 |
| 已编入Army，尚在集结 | `ArmyGatheringTargetRatio` | **0.90** | 135 |
| 计划独立劫掠 | `RaidTargetRatio` | **1.00** | 150 |
| 计划围城 | `SiegeTargetRatio` | **1.00** | 150 |
| 防守/紧急救援 | `DefenseTargetRatio` | **0.80** | 120 |

**注意**：表中35%/50%/80%等是讨论认可的**第一版建议值**，不是本体值；须通过长期模拟调节。人数比例不代表“必须达到才允许执行”，尤其防守救援优先于补员。

### 3.2 人数与缺口公式

```text
PartyLimit = Party.PartySizeLimit
TargetCount = Clamp(Ceil(PartyLimit * TargetRatio), 0, PartyLimit)
CurrentCount = Party.Party.NumberOfAllMembers
GapCount = Max(0, TargetCount - CurrentCount)
ManpowerGap = GapCount / Max(1, TargetCount)
```

- 使用实际当前人数计算**是否还需招兵**；**不能用潜在兵力代替当前人数**，否则AI“理论可招到100人”就会误认为“已拥有100人”。
- 潜在兵力判断**能否补到目标/是否值得为任务动员**，不抵扣未到位的缺口。
- 如果Party已超过和平目标，不代表必须解散已经训练完成的职业兵；和平目标主要约束后续日常补员，封邑兵由复员机制另行处理。

**示例**：当前60/上限150：和平53→Gap0，战时日常75→Gap15，战前动员120→Gap60，Army集结135→Gap75，劫掠150→Gap90。

### 3.3 进入场景的优先级

推荐决策顺序：

1. 正在即时战斗、遭遇敌军、逃跑、围城不可中断阶段：**禁止普通重派**；目标编制可查询但不代表应当移动征兵。
2. 已有有效 `MobilizationIntent`：以其计划军事任务决定场景；到期、目标不可行或战争结束后失效。
3. `Army`成员正在集结：`ArmyGathering`；Army领队等待成员，按Army任务/指挥官角色评估，允许就地补员。
4. 已有明确劫掠/围城/防守救援行为：匹配相应场景，但紧急救援优先执行任务。
5. 战争状态而无有效任务：`WarIdle`；和平：`Peace`。

**不要求**AI已真正进入 `RaidSettlement` 才可按劫掠目标人数动员：可由**动员前的战略试算**产生有效的计划任务。

### 3.4 征兵启动迟滞和停止条件

常规补员使用首版 `RoutineRecruitmentTriggerRatio = 0.85`：

```text
NeedRoutineVisit = CurrentCount < Ceil(TargetCount * 0.85)
MaxRecruitCount = Max(0, TargetCount - CurrentCount)
```

一旦成功发起常规补员，目标是补至场景目标人数；**不是补至85%**。有效Army集结与军事动员计划可不受常规85%启动门槛限制；紧急救援可先行动后补员。

- 如果 `GapCount==0`，应明确 `ShouldRecruit=false`，不可仅靠战争紧迫度/经济能力的正值打出足够 `NeedScore` 而招兵。
- 设置**同一目标的重试冷却、同Party任务改派迟滞**，不能每天重新寻路去另一个差不多的定居点。
- 一次征兵不得超过**当前有效目标人数**，不允许为了达到封邑批次25人门槛而招过头。

### 3.5 `CalculateNeedScore()` 重构要求

建议明确区分**需求资格**、**紧迫度评分**与**实际可负担量**。

```csharp
// 接口草案；并非可直接编译的最终补丁。
public RecruitmentNeed EvaluateNeed(
    MobileParty party,
    RecruitmentScenario scenario,
    RecruitmentPurpose purpose)
{
    int target = CalculateTargetPartySize(party, scenario);
    int current = party.Party.NumberOfAllMembers;
    int gap = Math.Max(0, target - current);
    if (gap == 0)
        return RecruitmentNeed.None;

    float normalizedGap = (float)gap / Math.Max(1, target);
    float urgency = CalculateWarAndMissionUrgency(party, scenario);
    float capacity = CalculateEconomicCapacity(party);
    float score = w1 * normalizedGap + w2 * urgency + w3 * capacity;

    return new RecruitmentNeed(target, gap, score);
}
```

实际实现可继续使用现有权重 `AiNeedManpowerWeight`、`AiNeedWarUrgencyWeight`、`AiNeedEconomicWeight`，但**经济承受力不是新增人数的许可证**：只有经过统一招募模型和预算审查的兵员可进入计划。`GetRecruitmentDecision()` 与每日征兵决策必须调用同一套逻辑。

---

## 4. Army状态、AI行为与战略意图

### 4.1 Army成员状态

```csharp
// 查询函数示意（务必配合真实Army集结状态验证）。
public enum ArmyParticipationState
{
    Independent,
    Gathering,
    Attached,
    Leader
}

public static ArmyParticipationState GetArmyState(MobileParty party)
{
    if (party?.Army == null) return ArmyParticipationState.Independent;
    if (party.Army.LeaderParty == party) return ArmyParticipationState.Leader;
    if (party.AttachedTo == party.Army.LeaderParty)
        return ArmyParticipationState.Attached;
    return ArmyParticipationState.Gathering;
}
```

- `Army != null`：已编入军团，不等于实际汇合。
- `AttachedTo == Army.LeaderParty`：成员已随军团领队行动。
- `LeaderParty == party`：军团领队无需附着其他Party。
- `Gathering` 为关系级分类，还应结合 `Army.IsWaitingForArmyMembers()`、移动目标以及军团真实状态防止误判。

当前 `AiRecruitmentBehavior.OnDailyTick()` 的 `party.Army != null` 排除条件需细化：正在集结的Party可**有条件**征召；已经附着的成员**不可自行离队绕路**；Army领队是否可就地补员单独判断。

### 4.2 `AiBehavior` 的使用方法

重点行为：`PatrolAroundPoint`、`GoToSettlement`、`RaidSettlement`、`BesiegeSettlement`、`AssaultSettlement`、`DefendSettlement`、`EngageParty`、`EscortParty`、逃跑类行为。

分工：

- `DefaultBehavior`：当前相对长期的移动/军事行为，可以辅助确定情境。
- `ShortTermBehavior`：即时避险等行为，优先控制**是否允许中断**。
- `PartyObjective`：战略倾向（Neutral/Defensive/Aggressive），是背景信息，不能代替实际任务。
- `Army`和`AttachedTo`：优先用于军团集结/随军判别。
- Mod自定义的 `MobilizationIntent`：保存从战略试算产生、尚未实际执行的目标，防止改派到征兵点之后丢失原因。

**不能**简单以 `DefaultBehavior == AiBehavior.RaidSettlement` 作为封邑兵资格的唯一条件，因为当前人数不足时本体未必会选劫掠。

### 4.3 征兵目的（Mod内部）

```csharp
public enum RecruitmentPurpose
{
    Routine,
    WarMobilization,
    ArmyReinforcement
}
```

每次计划至少包含：`Party`、`Purpose`、`Scenario`、目标兵源/定居点、目标人数/所需战力、创建日、过期日、可选军事目标以及计划状态。

**持久化策略**：优先实现轻量、可重建的内存计划，避免保存对本体AI目标的长期强约束；若跨读档持续任务确有必要，再定义存档字段与无效目标清理规则。保存Party对象作为键必须处理销毁及停战失效。计划不能重复记账已承诺资金或已征兵员。

---

## 5. 潜在兵力：面向可执行动员的只读评估

### 5.1 两套人数和战力

```text
ActualCount       = 实际Party名册人数（用于补员目标缺口）
ActualStrength    = 实际可用战斗力（健康士兵，本体规则）
PotentialCount    = ActualCount + 可被合法、及时、负担得起的新增人数
PotentialStrength = ActualStrength + 可新增士兵的预计战力
```

潜在人数不能超过 `PartySizeLimit`；潜在战力要按**具体可新增兵种**估值，不允许单纯按人数乘固定平均战力。可使用本体 `Campaign.Current.Models.MilitaryPowerModel.GetTroopPower()` / `GetDefaultTroopPower()` 及相关情境；不可混用“伤员人数”与“可即时战斗人数”。

### 5.2 输入约束

- **兵源**：只计入该Party所属Clan实际有权使用、当下就绪的驻军兵与封邑兵；雇佣兵纳入潜在兵力与否按任务与场景开关，并以可确认的实际城镇招募供应为准。
- **驻军安全底线**：使用 `GarrisonTransferPolicyModel`/现有转移模型，扣除保卫定居点必须留下的士兵；不得直接把驻军全部当作可调动员。
- **封邑资源**：`FiefPartyManager.GetAvailableTroopCount()`只是起点，仍需按第一阶段统一兵源权威名册、`TryMobilize()`所需Hearth、封邑权限及征召审批校验。
- **经济**：复用 `AiRecruitmentFinancialModel.GetLongTermDailyWage()` / `GetAvailableAdditionalDailyWage()`、`RecruitmentBudget` 和 `ClanRecruitmentBudgetManager`；免薪期结束后的长期军饷必须负担得起。
- **兵种组成**：使用可招募的确切 `CharacterObject` 与 `DefaultAIRecruitmentModel.BuildPlan()` 审批结果，而非只将“还有N个人”当作可用战力。
- **征召下限**：每个封邑的一次**AI实际获批征召**少于25人则整次不做；不能把8处各有10人的封邑兵简单相加，虚构80名可征兵。
- **空间与时间**：目标点到Party的实际可到达距离、军团集结时限、军事任务时效；旅程过长的资源不计入本次任务的“可及时动员兵力”。
- **共享资源**：一个Clan有多支Party时，不能让每一支同时全额使用同一批驻军和封邑可征兵员。

### 5.3 输出数据结构（建议）

```csharp
public sealed class PartyPotentialStrength
{
    public int CurrentCount { get; init; }
    public int ReinforcementCount { get; init; }
    public int PotentialCount { get; init; }
    public float CurrentStrength { get; init; }
    public float PotentialStrength { get; init; }
    public float PotentialPartySizeRatio { get; init; }

    // 用于判断是否真正可实施：只读、不预扣兵额
    public IReadOnlyList<PlannedRecruitment> Reinforcements { get; init; }
}

public PartyPotentialStrength CalculatePartyPotentialStrength(
    MobileParty party,
    MobilizationContext context);
```

`MobilizationContext`至少包含征兵目的、目标军事行为、可接受动员时限、是否允许绕路、是否纳入雇佣兵，以及可选目标据点。**同一Party不同任务可以得到不同的潜在兵力**；不能建立一个无视时间和地点的永久“潜在满编值”。

### 5.4 计算流程

```text
冻结一次计算快照：当前Party/Clan/定居点/预算
       ↓
计算剩余Party容量和当前实际战力
       ↓
列举时间窗口内合法的Clan兵源和允许的雇佣兵
       ↓
逐来源生成真实兵种候选，按XML/驻军名册取数量
       ↓
统一招募模型只读模拟：质量/角色/军费/容量
       ↓
单封邑实际候选获批 < 25 → 删除该封邑候选
       ↓
按旅行时间和任务价值择址、分配Clan共享资源
       ↓
汇总可执行新增人数与预计战力
       ↓
返回Potential；不修改兵员、资金、Hearth或Party指令
```

**严格区分两种评估**：

1. **独立只读试算**：用于战略评分，不提交资源，结果是“在此快照、此动员时间内可行”。
2. **下发计划前的确认与锁定**：同一Clan的多Party共同竞争时，按优先级分配临时兵源额度；实际抵达定居点时再核验实际可用性。试算结果不能当作已经预约成功。

建议先实现**按Clan统一调度的一轮临时分配**，避免引入跨天持久化预扣兵额。若未来要实现长途多日的强预约，再单独设计失效、回滚与读档处理。

### 5.5 战力与人数的用途隔离

| 判断 | 使用什么 |
|---|---|
| 是否还需补员 | `TargetCount - CurrentCount` |
| Army成员现有编制比例门槛 | 当前 `PartySizeRatio`；必要时使用**仅限规划的**潜在比率 |
| 评估“动员后能否进行劫掠/围城” | `PotentialStrength`（战略预估） |
| 与迎面敌军交战、逃跑、追击、围城真实攻守判定 | **`ActualStrength`**；不得全局替换 |
| 执行完征兵后的任务复评 | 重新读取**实际**Party人数及战力 |
| 每日工资、Hearth、服役兵额 | 第一阶段的**实际**记录与账目 |

### 5.6 示例：避免双重统计

Party现有50/上限150；Clan可调驻军45、封邑就绪80；长期财力最多支持新增70名具体兵种。若所有资源都可及时抵达、通过审批，则 `PotentialCount=120`，而不是 `50+45+80=175`。

若另有同Clan Party也想动员这批兵源，双方计划不能都声明拥有全部70人的可兑现权利；应在下发实际计划时按军事优先级分配。

---

## 6. 让潜在兵力真正影响本体军事AI，而不造成虚假自信

### 6.1 必须保留“计划→征兵→执行”的阶段

```text
当前真实兵力不足，任务评分较低或为0
          ↓
只读计算潜在兵力
          ↓
对候选任务做战略预评估（虚拟动员后的可行性）
          ↓
产生 MobilizationIntent（例如拟劫掠A）
          ↓
寻找可及时到达且有足额兵源的补员点
          ↓
实际征兵（受当时审批、资金、兵额限制）
          ↓
用实际兵力重新运行军事任务评分
          ├─ 可行 → 允许本体选择/继续军事任务
          └─ 不可行 → 撤销或调整任务，不能凭空出击
```

这一方案**不等同于直接把 `MobileParty.Party.EstimatedStrength` 改大**。潜在战力只影响“值得先动员吗”，不能影响即时战斗AI的危险判断。

### 6.2 Army：两个60%门槛是不同逻辑

- `DefaultArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy = 0.6f`：AI候选成员是否可被军团召集；首版可测试 **0.40**，留作可配置值 `AiArmyCallMinimumRatio`。
- `AiMilitaryBehavior.AiHourlyTick()` 中另有 `party.PartySizeRatio > 0.6f`：可能创建军团的Party资格；**不可假设更改前一处会同时改变此处**。建议先维持更严格的主动创建门槛，避免大量弱小Party开始组Army；如测试显示军团数量异常，再单独设计 `AiArmyLeaderMinimumRatio`。

**实施建议**：先用自定义ArmyManagementCalculationModel覆盖候选成员门槛，验证原模型的其他影响力、食物、距离条件；再针对创建军团的硬编码条件制定限定Harmony补丁，而不是无差别改所有PartySizeRatio引用。是否允许“潜在比例过门槛立即被召集”须慎重——只有已规划且可在集结前补齐的人数才有资格参与。

### 6.3 军事行为：避免先有鸡还是先有蛋

本体 `AiMilitaryBehavior` 对劫掠、围城、防守等候选计算含兵力评分；`DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction()` 还会按 `ourStrength` 与敌方防御强度淘汰弱小方案。

推荐**最小入侵**的实现步骤：

1. 独立创建 `AiMobilizationBehavior` 或 `AiMobilizationPlanningModel`，仅对战争时期、缺员而且有兵源的领主Party运行低频预评估。
2. 读取本体目标与评分相关数据，尝试对`Army.ArmyTypes.Raider/Besieger/Defender`的有限候选目标使用潜在战力试算；必须复核 `GetTargetScoreForFaction()` 的其他输入及副作用。
3. 形成“目标任务 + 预计动员兵源 + 时限”的短期意图，而**不是立即设置Party的 `RaidSettlement` / `BesiegeSettlement`**。
4. 先完成征兵，再交由本体行为选择/评价是否可以执行原拟任务。
5. 任务不可行、军团召集、战争结束、来源失效、敌方守军剧变等情况自动取消或重算计划。

**待源码实测的关键点**：本体评分是在`AiHourlyTick`/相关ThinkParams流程中如何调用，以及是否可以安全从Mod的只读预评估入口复用。若不能安全复用，只对准确定义的战略评分调用点应用Harmony，而不要影响战斗/威胁评分。

### 6.4 军事任务优先级

建议基本顺序：已经发生的战斗/避险 ＞ 紧急救援 ＞ 已经加入Army的集结期限 ＞ 有效军事动员计划 ＞ 日常补员/军需访问 ＞ 自主轮换/常规返乡。

尤其救援：**目标80%仅用于判断是否值得快速补员，不是“未达到80%就不得出发”的硬门槛**。

---

## 7. 三兵源征召：先资格，再偏好，再执行

### 7.1 兵源职责

| 兵源 | 当前实际路径 | 战略定位 |
|---|---|---|
| **职业兵** | Clan城镇/城堡驻军，通过 `GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison()` 调拨 | 长期骨干部队，和平日常主要兵源 |
| **封邑兵** | 来源Clan封邑 `FiefPartyManager.RecruitFiefTroopsFromSettlement()` | 战时临时动员，拥有文化免薪与服役批次；不用于常规和平补员 |
| **雇佣兵** | 允许访问的城镇，现有 `TryRecruitLordMercenary()` | 临时快速补兵，需满足支付与长期经济规则 |

### 7.2 场景与兵源资格（建议矩阵）

| 场景 | 职业兵 | 封邑兵 | 雇佣兵 | 可否专门绕路 |
|---|---|---|---|---|
| 和平日常 | 允许 | **禁止新征召** | 允许但低优先级 | 仅经济且兵额价值足够 |
| 战时无任务日常 | 优先 | 原则上不主动大规模征召；真正动员有例外 | 允许 | 限制绕路 |
| 战前动员 | 允许 | **允许，≥25** | 允许 | 以任务收益/可接受动员时间为限 |
| 已编入Army、尚未汇合 | 允许 | **允许，≥25** | 允许 | 不能明显延误集结 |
| 已附着Army | 顺路/由领队管理 | 不自行离军团；有条件随军就地处理 | 顺路 | **禁止成员自行绕路** |
| Army领队等待集结 | 可在当前位置/顺路补员 | 有条件允许 | 允许 | 不延误领队集结 |
| 独立准备劫掠/围城 | 允许 | 可作为战前动员的主要兵源 | 允许 | 有时限并复评目标 |
| 防守/紧急救援 | 仅及时补员 | 原则不为其耽误救援 | 就近及时补员 | 优先赶往目标 |
| 战斗、逃跑、持续围城 | 不重派 | 不重派 | 不重派 | 禁止 |

AI文化偏好仍使用 `GetAiCulturalPreference()` 和情境修正，但**兵源许可应为硬过滤**：例如和平期封邑禁用，不能因文化Fief权重很高又被选中。

### 7.3 统一候选与地点评分

建议先过滤许可，然后评估真实可获得的兵员及目的地：

```text
CandidateScore = SourcePreference
               × RecruitableValue
               × MissionTimeliness
               × DistanceEfficiency
               × RosterFit
```

上式只描述评分维度，并非已确定公式；首版应优先建立明确的硬约束（可达、时间、>=25、预算、任务有效），再进行简易排序，避免过度调参。

现有 `FindBestFiefSettlement()` 主要依赖文化匹配及可用比例，必须补充*审批数量、旅行成本与目标剩余缺口*；现有 `FindBestMultiPurposeSettlement()` 的兵力条件也改为场景缺口，而不是永远以满编或固定85%Party上限要求为依据。

### 7.4 定居点实际执行必须二次校验

`AiSettlementRecruitmentPatch` 接管 `RecruitmentCampaignBehavior.CheckRecruiting()`，所以**不能只修改日常AI寻路**。Party因本体AI路过或普通原因访问定居点时，也可能触发招兵。

到达时重新检查：

1. Party是否仍有效、仍有此次动员目的或属于允许的日常补员场景。
2. 场景目标和人数缺口是否仍为正。
3. 当前来源兵种、Clan所有权、驻军安全、Hearth/兵额是否仍有效。
4. 实际审批人数是否达到该兵源的限制（封邑兵≥25）。
5. 不能为达到门槛而突破 `TargetCount`；可选择其他合法兵源回退。
6. 如果有任务计划，征兵结果及时写回计划，并在计划失效后清除，不无限重试。

需要处理现有 `CheckRecruiting()` 前置95%检查以及 `DefaultAIRecruitmentModel` 的95%限制，二者必须一起适配。`TargetCount=PartySizeLimit` 的劫掠/围城动员可批准至100%，平时最多补至对应场景目标。

---

## 8. 封邑兵XML模板与单次最少25人

### 8.1 XML是兵种构成依据，Party实际名册是兵源依据

当前 `ModuleData/fiefPartyTemplates.xml` 针对文化、城镇/港口城镇/城堡提供 `TroopComposition`，各具体 `<troop id="..." weight="..."/>` 定义权重。`FiefPartyTemplate.TroopEntries` 已解析具体条目和兵营条件。

**实现原则**：

- 模板定义“希望按什么兵种比例提供”；实际就绪名册 `_fiefParty` 决定“现在真有什么”；`RecruitmentModelManager.Model.BuildPlan()` 决定“当前Party还能合法接受哪些”。
- 不恢复独立兵种小队/专属池，不因为模板缺少某兵种就凭空生成士兵。
- 对缺货兵种，在模板允许的其他兵种中按权重重新分配剩余名额；受到`CombatRole`、`TroopQuality`、财力限制时也不能强行满足理论配比。
- 可以复用第一阶段已保留的 `RecruitedTroopDetachmentList`：**每次实际征召成功即新增独立批次，且由来源Fief持有**。

### 8.2 权重分配算法建议

输入：`targetRequest = Min(TargetGap, FreeSlots, AllowedByMission)`；读取实际Fief就绪名册与模板权重。

1. 按模板中**实际可用**的 `CharacterObject` 建立候选，初步按照具体权重分配 `targetRequest`，整数部分用最大余数法。
2. 若有兵种库存不足，将剩余需求按当前仍可提供的权重重新分配；不要超出模板启用范围。
3. 将候选提交统一招募模型，得到实际批准数量（工资、质量、角色结构、Party容量、Clan预算）。
4. 如果 `ApprovedTotal < MinimumAiFiefRecruitmentCount (25)`：**整个AI封邑征召失败，且不得先扣任何Hearth、兵额或Party士兵**。
5. 若获批≥25：一次性执行真实转移，并由第一阶段机制创建新的在役批次及占用标记。

**示例**：瓦兰迪亚模板骑士:步兵:弩兵权重为1:3:2。请求30人且货源、财力均足时，目标为5/15/10；缺货或受角色上限约束时允许偏离该配比。

### 8.3 25人下限适用范围

- **仅AI封邑兵征召**，对所有主动或顺路征召的实际交易一致生效；不允许从多处封邑分别招10人然后声称“累计30人满足门槛”。
- 玩家手动征召不受本规则影响。
- 旧存档里已经存在的5人或3人残余批次仍然合法在役，不能为达到下限自动清理。
- 返乡不受25人下限；Party顺路归还3人可以执行，禁止的是“**为了遣返3人专门跑远路**”。
- 例：目标75、当前65、缺10，即便封邑有100兵也不能强征25人超编；改选职业兵/雇佣兵或等待后续任务需求。

### 8.4 与第一阶段签名的衔接

当前 `FiefPartyManager.RecruitFiefTroopsFromSettlement(Settlement, MobileParty)` 未包含`requestedCount`。第二阶段建议增设带数量/意图约束的重载，例如：

```csharp
// 草案；原签名可保留给现有菜单和其他调用方。
int RecruitFiefTroopsFromSettlement(
    Settlement settlement,
    MobileParty party,
    int requestedCount,
    RecruitmentPurpose purpose);
```

第一阶段已经解决的批次持久化、Hearth占用与兵员转移职责保持不变；第二阶段只负责先给**受限的请求人数**并在审批后统一决定是否满足25。

---

## 9. AI续役、轮换、战后复员

### 9.1 服役与免薪：不再有“到期必须遣返”

- 第一阶段的批次字段包含 `ServingParty`、`ServiceDays` 和来源文化确定的 `FreeWageDays`。
- `PaidServiceDays = Max(0, ServiceDays - FreeWageDays)`；免费服役结束后**继续留在Party并按既有工资规则计薪**。
- **不存在 `NormalServiceDays`**，也不存在在役 `MaxServiceWeeks` 倒计时删除。
- 超过文化免费服役期越久，正常军事环境下AI的返乡/轮换倾向可以越高；倾向不等于强制下达返乡命令。

### 9.2 返乡需求 ≠ 返乡任务

每个在役批次都可以有`ReturnDemand`，但只有预计收益足够时才转为`ReturnMission`：

```text
ReturnPriority = f(PaidServiceDays, ReturnCount, TravelCost,
                   CurrentMilitaryNeed, ReplacementAvailability)
```

此公式表达设计因素，**未确定权重与概率曲线**。

**禁止**为来源Fief的3人而从远方专门返乡；允许继续付薪服役，或待途经来源封邑、Army解散后的集中复员再处理。返乡的实际操作只能委托第一阶段的 `FiefPartyManager.ReturnTroopsToSettlement()`；真实归还才恢复Hearth和进入冷却。

### 9.3 战时轮换：返乡+征召是一个事务性计划

```text
评估旧批次已进入付薪期，且附近有适合轮换的来源Fief
       ↓
确认军事任务可中断、替代兵源和Clan预算足够
       ↓
计划路程，避免因几人产生长途返乡
       ↓
抵达来源Fief，先由第一阶段执行真正返乡
       ↓
旧兵释放兵额、恢复Hearth、进入冷却（不能立刻重新征召）
       ↓
按第二阶段XML/25人规则征召**其他就绪兵员**
       ↓
实际兵力不足时按当下真实情况调整任务
```

**重要约束**：不得因为返乡释放兵额就把刚刚进入冷却的士兵当作“立即就绪”；不能把轮换设计为零距离、零成本的免费服役重置。

只有具备合理后勤收益或军事需要时才轮换。优先考虑顺路停留、Army休整或解散后的机会；绝不为了工资减免将军团拆散。

### 9.4 Army状态与返乡权限

| Party状态 | AI轮换/返乡 |
|---|---|
| 独立Party、无紧急任务 | 可按收益发起返乡或轮换 |
| 正前往Army集结 | 默认不能明显推迟集结；只允许不影响时限的顺路处理 |
| 已附着军团领队 | 不可自行离队专程返乡；跟随Army完成任务 |
| Army领队等待成员 | 仅允许集结地就地/顺路轮换，不牺牲军团目标 |
| 正在交战、围城、救援、逃跑 | 暂停轮换与非紧急返乡 |
| Army解散后 | 重新评估战时轮换或和平复员 |

### 9.5 和平复员

**目标**：和平时期默认返还全部或绝大部分仍在役的封邑兵；不要求停战当天全部办完，不自动裁掉超出35%目标的职业兵。

1. 识别Clan退出正式战争并且Party没有必须持续进行的紧急军事任务。
2. 停止新的普通封邑征召；允许已创建的动员计划失效。
3. 按来源Fief分组、可返乡人数、距离和路程组合生成返乡任务，尽可能集中处理。
4. Army成员等待军团适时解散或已独立行动后复员；允许边境紧急威胁推迟。
5. 真正进入来源Fief时执行第一阶段返乡接口；部分返乡失败时保留剩余在役批次并记录下次机会。

### 9.6 经济约束的定位

**不把“经济崩溃后紧急返乡”当作主要触发器**：本体欠薪/逃兵可能先发生。财政应在征兵前审查、轮换时评估成本。

尤其封邑兵前28天或按文化设置免薪，也不能让AI把整支Party永久视为低工资；采用免薪**结束后**长期维持成本作主要预算输入。

---

## 10. AI计划生命周期与防震荡

### 10.1 动员计划状态

建议状态：`Proposed → EnRoute → Recruiting → ReadyToReevaluate → Completed`，以及 `Cancelled/Expired`。不一定要定义为枚举，但必须有等价的生命周期处理。

- 每Party同一时刻最多一个**主动移动的**征兵/返乡任务；多个来源点可作为同一计划的有序停靠点。
- AI被召入Army、爆发战斗、定居点易主、战争结束、财力骤降、目标军情剧变、超出期限时，及时暂停/取消并清除失效目的。
- 每日 `OnDailyTick` 不应持续覆盖本体军事AI的紧急命令；沿用现有任务检查并扩展为**计划ID/目标/时限**，不只看 `TargetSettlement.OwnerClan == clan`。
- 返乡任务和征召任务不得互相无条件触发：先依据场景目标与轮换替代兵额判定是否值得移动。

### 10.2 短期与长期行为区别

- **短期遭遇/撤退/围城行动**立即阻止招募寻路。
- `DefaultBehavior` 若已变成 `GoToSettlement`，仍通过有效 `MobilizationIntent` 找回“为何前往此地”。
- 旧的任务失效后，不继续借用“战争动员”资格在任何城市乱征兵。

### 10.3 常见震荡案例及阻断

| 风险循环 | 必需的阻断 |
|---|---|
| 和平目标53，当前52就外出招1人 | 常规85%阈值 + 最小行动收益/冷却 |
| 战时目标75，当前65，封邑一次至少25 | 总缺口仅10，不允许征25超编；改其他兵源或不动 |
| 刚遣返50封邑兵，次日又去征回50 | 冷却仍有效 + 和平禁止封邑征召 + 同任务回访冷却 |
| 多个Party同时计划从同一驻军调拨40人 | 按Clan共享资源分配计划额度，抵达时再校验 |
| 成员已加入Army，但日常征兵派它远离Army领队 | `Attached`禁止主动绕路，`Gathering`限时顺路动员 |
| “潜在满编”让40人Party直接攻击强敌 | 只有实际征完兵且复评通过才能执行攻击 |
| 先按文化偏好选封邑，却因当日模型只批准18人 | 批次≥25原子门槛，审批不足则回退其他来源 |
| Army正集结时为最后3人反复换征兵目标 | 剩余效益阈值、集结时限、重试冷却 |

---

## 11. 模块和API实施草案

### 11.1 推荐分层

| 组件 | 修改/新增 | 职责 |
|---|---|---|
| `AiRecruitmentBehavior` | **修改** | 每日征兵状态机；从当前Party人数与场景目标得到缺口；选择目的与兵源；协调原有概率与权重 |
| `Recruitment/Models/PartyTargetSizeModel.cs` | **新增建议** | `ResolveRecruitmentScenario()`、`CalculateTargetPartySize()`；只读目标人数模型 |
| `Recruitment/Models/PartyPotentialStrengthModel.cs` | **新增建议** | `CalculatePartyPotentialStrength()`；基于真实兵源+预算的只读模拟 |
| `Recruitment/Models/MobilizationPlanningModel.cs` | **新增建议** | `BuildMobilizationPlan()`，管理多来源候选与任务预计收益，输出具体征兵计划 |
| `Recruitment/Models/MobilizationIntent.cs` | **新增建议** | Party短期动员目的、任务类型、目标、到期及状态；最初可仅内存 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | **修改** | `CheckRecruiting()` 到达时二次校验目的、场景目标、25人、95%限制及实际审批 |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` | **修改** | 审批支持外部可设置的目标人数上限，进攻动员可100%；其他质量/财政约束保持 |
| `Recruitment/Finance/ClanRecruitmentBudgetManager.cs` | **复用并扩展** | 只读评估下的Clan份额/临时计划竞争防重复；真实征兵后才调用 `CommitRecruitment()` |
| `PartyFinance/Models/AiRecruitmentFinancialModel.cs` | **复用** | 以免薪结束后长期军饷估计本次新增兵力可负担性 |
| `Fief/Models/PartyData/FiefPartyRecruitment.cs` | **修改** | 支持AI申请人数上限、XML具体兵种权重和审批后25人原子门槛；实际批次创建委托第一阶段 |
| `Fief/Models/FiefPartyManager.cs` | **扩展API调用方式** | 保留原征召签名；新增数量限制的AI征召重载/接口；供新AI查询在役批次并执行返乡 |
| `Fief/Models/FiefPartyTemplate.cs` | **复用** | 具体兵种、权重、兵营约束来源，不恢复新兵池 |
| `Recruitment/Behaviors/AiFiefServiceBehavior.cs` | **新增建议** | 续役倾向、战时轮换、战后复员的AI决策；实际返乡调用第一阶段 |
| `PartyFinance` 与Fief服役管理 | **仅调用第一阶段** | 保持原批次数据对账与文化免薪，不复制仓库中的第二套台账 |
| `ModuleData/modConfigs.xml` + `common/ModConfig.cs` | **扩展** | 场景比例、启动阈值、最低征召人数、军团门槛及动员时限等配置 |
| `Main.cs` | **适量注册** | 新CampaignBehavior和必要Models；避免双重事件订阅和旧逻辑重复执行 |
| 新的**限定作用域**Army/军事AI Harmony补丁 | **按实测决定** | 更改明确的战略资格/试算入口，不触碰即时战斗战力 |

### 11.2 建议公开接口

```csharp
RecruitmentScenario ResolveRecruitmentScenario(
    MobileParty party, MobilizationIntent intent);

int CalculateTargetPartySize(
    MobileParty party, RecruitmentScenario scenario);

RecruitmentNeed EvaluateRecruitmentNeed(
    MobileParty party, RecruitmentScenario scenario);

PartyPotentialStrength CalculatePartyPotentialStrength(
    MobileParty party, MobilizationContext context);

MobilizationPlan BuildMobilizationPlan(
    MobileParty party, RecruitmentPurpose purpose,
    float requiredStrength, int targetCount);

bool CanRecruitFromSource(
    MobileParty party, Settlement settlement,
    RecruitmentSource source, RecruitmentPurpose purpose);

ReturnPlan BuildFiefReturnOrRotationPlan(MobileParty party);
```

以上是API提议，不要求全部独立成新类；优先保持**请求查询只读、真正执行才修改世界状态**。如Mod已有成熟的 `RecruitmentPlan`，第二阶段的 `MobilizationPlan` 是“涉及多个定居点和任务阶段的外层计划”，不能取代统一模型在单次征兵中的战术审批。

### 11.3 配置XML示意

现有 `modConfigs.xml` 有 `<AiRecruitment>`，推荐在其中新增：

```xml
<AiRecruitment>
  <!-- 现有 NeedScore / CulturalPreferences / SituationalModifiers 保留 -->
  <TargetSizes
      peace="0.35"
      warIdle="0.50"
      warMobilization="0.80"
      armyGathering="0.90"
      raid="1.00"
      siege="1.00"
      defense="0.80"
      routineTrigger="0.85" />
  <FiefRecruitment minimumAiBatchSize="25" />
  <ArmyMobilization aiCallMinimumRatio="0.40" />
  <!-- 最大绕路天数/计划有效天数，待实际路径测试后配置 -->
</AiRecruitment>
```

**这段仅展示新增字段形式，不能直接替换现有 `<AiRecruitment>` 整段XML**。最终应根据 `ModConfig.DeserializeAiRecruitment()` 使用的节点解析风格调整，且新字段提供向后兼容默认值。`FreeWageDays` 属于第一阶段来源文化配置，**第二阶段不重复存一份**。

---

## 12. 文件级实施清单（按函数定位）

| 文件 | 具体步骤 | 完成判据 |
|---|---|---|
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` | 新增情境判断和目标人数；`CalculateNeedScore()`换成目标缺口；`GetRecruitmentDecision()`与 `OnDailyTick()`同源；`FindBestMultiPurposeSettlement()`不再用固定满编缺口；筛选Army状态；保存任务目的 | 和平35%达标不继续招兵；Army集结可以合法补员，Attached不绕路 |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` | 将写死的95%替换为针对计划目标/实际Party上限的合法上限；对现有其它调用方保留原有安全规则 | 进攻动员能招至100%，普通场景不超过自己的目标 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | 修前置Party上限95%检查；到达目标后重新查询有效情境与兵源许可；遵守计划人数及25人下限；其他兵源回退 | 不会旁路封邑门槛，也不会在计划取消后继续因旧权重征封邑 |
| `Recruitment/Models/RecruitmentSimulationState.cs` / `RecruitmentPlan.cs` | 如有必要提供受目标人数约束的只读模拟上下文，不直接改变真实状态 | 多候选模拟一致且不产生预算实际扣减 |
| `Recruitment/Finance/ClanRecruitmentBudgetManager.cs` | 使用现有Clan共享预算，在外层多Party计划层添加临时配额分配 | 多Party不重复预约同一Clan军费和驻军 |
| `Fief/Models/PartyData/FiefPartyRecruitment.cs` | 支持受场景目标限制的RequestedCount；改用具体模板兵种权重，筛选可用名册；审批总数<25则不交易；成功后调用第一阶段批次流程 | 实际征兵不超目标、遵守模板、不因不足25先扣Hearth |
| `Fief/Models/FiefPartyManager.cs` | 暴露带请求上限的AI入口、服役批次只读查询；使用第一阶段返乡API | 新AI不直接改写批次和Hearth |
| **新增** `Recruitment/Models/PartyTargetSizeModel.cs` | 实现场景表和公式 | 每种场景对上限150得到53/75/120/135/150/120 |
| **新增** `Recruitment/Models/PartyPotentialStrengthModel.cs` | 单次快照收集兵源，检查地理/财经/容量/25、按兵种算战力 | 实际可兑现的潜在兵力；查询不修改游戏 |
| **新增** `Recruitment/Models/MobilizationPlanningModel.cs` | 在战略候选评分与具体定居点任务之间建立计划状态；Army集结与救援优先 | 不因补员而直接把虚拟实力当实有实力 |
| **新增** `Recruitment/Behaviors/AiFiefServiceBehavior.cs` | 每日或低频评估继续服役、轮换/和平复员；发起顺路/集中返乡任务 | 在Army中不擅自离队；和平期大规模遣返 |
| `common/ModConfig.cs` + `ModuleData/modConfigs.xml` | 注册全部首版比例、25人下限、被Army召集比例和计划时限可配置值 | 不硬编码平衡参数；缺新字段时可加载旧XML |
| `Main.cs` | 注册第二阶段行为与必要模型 | 无双重监听/重复招募 |
| 本体Army/战略相关补丁（新建、限定） | 先确认修改的战略入口、调用顺序和副作用；只做点状兼容修改 | 不改变即时交战实力、不引入死循环或Army爆炸式创建 |

---

## 13. 建议开发顺序与发布里程碑

### M1. 场景目标 + NeedScore（先不改本体军事AI）

1. 新增场景判定和目标比例配置；先以现有 `AiRecruitmentBehavior` 接入。
2. 统一日常决策与 `GetRecruitmentDecision()`；缺口为0直接不招。
3. 日常85%启动门槛；任务中断规则；替换 `FindBestMultiPurposeSettlement()` 中的固定比例需求。
4. 两处95%限制按新目标模型调整，且保留统一兵种/财力约束。

**验收**：不实现潜在兵力，也能看到和平时期Party不再不断追求满编。

### M2. XML模板 + 25人门槛

1. 给AI封邑征召增加请求数量上限和真实审批下限。
2. 按具体模板权重从实际就绪名册生成候选；不足重新分配。
3. 到达入口二次校验，审批失败不扣Hearth、不生成批次。
4. 确保玩家手动征召不受25下限影响。

**验收**：无小批次滥招、不强征超目标，文化模板实际兵种构成合理。

### M3. 潜在兵力查询

1. 按Clan/Party/任务时间窗口收集真实兵源与财务数据。
2. 复用统一模型模拟候选；计算按兵种战力的潜在值。
3. 多Party临时分配，防止重复计数。
4. 压力测试缓存、事件一致性、调用耗时（AI低频执行，避免每小时所有Party×所有定居点反复大规模计算）。

**验收**：不同任务窗口的潜在值合理，查询完全只读，跨Clan/跨Party不重复占用。

### M4. Army与军事战略集成

1. 先单独测试降低被Army召集比例至0.40；保留创建Army门槛，观察军团规模和集结成功率。
2. 实现预动员任务选择，在真实人数不足时允许评估潜在可行性。
3. 建立任务目的生命周期；征兵后依实际战力重新判断军事任务。
4. 通过限定补丁修正无法从Mod安全接入的战略入口。

**验收**：弱Party有动员机会但不会以虚拟人数打架；Army不会陷入反复召集/中途退出。

### M5. 续役/轮换/复员

1. 读取第一阶段的 `ServiceDays`/`FreeWageDays`，不新增服务倒计时。
2. 返回任务评分与小人数远距离抑制；顺路返乡。
3. 战时轮换：先规划替补，然后返乡、征召、实际人数复评。
4. 和平战后复员：暂停封邑征召，按来源定居点集中归还。

**验收**：不因免薪到期打散Army，和平期封邑兵逐步回流且Hearth/冷却正常。

---

## 14. 关键验收测试

| 编号 | 场景 | 预期结果 |
|---|---|---|
| A01 | 150上限、和平当前60人 | 目标53，无日常补员任务 |
| A02 | 150上限、战时无任务当前60人 | 目标75，缺15；可职业兵补员，但不为25人封邑兵超编 |
| A03 | 150上限、Army集结当前60人 | 目标135，缺75；允许合法可及时完成的封邑/驻军/雇佣兵动员 |
| A04 | 150上限、准备劫掠当前60人 | 目标150，缺90；军事计划允许动员但实战判定仍用真实战力 |
| A05 | 目标已满、战争紧迫度高、资金充足 | `Gap=0`，不会单凭NeedScore其余因子继续招兵 |
| A06 | 常规目标75、当前70（超过85%阈值） | 不主动为小缺口中断当前行为寻找补员点 |
| A07 | 目标75、当前60（低于85%阈值） | 可启动合理日常补员任务，最多招至75 |
| A08 | Army成员 `AttachedTo==LeaderParty` | 不因征兵独自离开Army；集结途中成员可在时限内顺路补员 |
| A09 | Clan封邑A就绪兵80、AI预算只批准18 | 封邑本次实际征兵0、Hearth消耗0、无新批次，尝试其他合法兵源 |
| A10 | 封邑A就绪兵40、批准30且缺口≥30 | 实际招30、产生一个独立在役批次 |
| A11 | 封邑A/B/C各就绪10人 | 不把三个10虚构成一次≥25的封邑征召任务 |
| A12 | 玩家手动从封邑征召5人 | 继续允许，不受25AI门槛约束 |
| A13 | XML具体兵种权重为1:3:2，申请30且全充足 | 理论候选5/15/10，实际批准遵守组成、库存及经济限制 |
| A14 | 单一权重兵种库存不足 | 仅在模板允许的可用兵种中重新分配，绝不创造兵员 |
| A15 | 劫掠计划的Party当前40、潜在可增100 | 允许发起征兵任务；征完之前不会把140人用于迎战/逃跑判断 |
| A16 | 实际征召被抢占，仅补到70，原计划需120 | 用实际70人重评，不能沿用虚拟140人的军事评分发动攻击 |
| A17 | 同Clan的Party A/B同时看见驻军可调100人 | 实际下发计划时共享兵源，总预约/真实可用不超过100 |
| A18 | 来源封邑虽有100兵，但过远超过任务期限 | 本次任务潜在兵力不计该封邑远距离兵源 |
| A19 | 150上限、进攻目标100%，当前143人 | 配套移除95%硬限制；可在审批允许范围内补至150 |
| A20 | Party到定居点时军事目标已取消 | 不沿用旧 `MobilizationIntent` 擅自征召封邑兵 |
| A21 | 战时免费服役结束，Army正围城 | 继续付薪服役，不强制返乡，不让军团成员擅自离队 |
| A22 | 付薪旧兵30人、来源封邑顺路且其他就绪兵≥25 | 在军事允许条件下可轮换，刚归还旧兵不能立即再征 |
| A23 | 仅3名旧封邑兵、来源在地图远端 | 不专程返乡，继续服役等待顺路或战后集中处理 |
| A24 | 战争结束，独立Party有多来源封邑兵 | 停止正常封邑新征召，按来源集中复员；不自动裁撤高级职业兵 |
| A25 | 从战争转和平，日常目标降至35% | 不马上无差别解散职业兵；优先处理封邑兵返乡 |
| A26 | 急需救援、目标80%、当前只有60% | AI可先出发；不能因为未补到80%错过救援 |
| A27 | 同时反复查看潜在兵力与预算 | 不改变Party名册、Hearth、Clan资金、日常预算承诺或服役时间 |
| A28 | 存档/读档或Party被销毁、Army解散 | 清理失效动员/轮换任务，不影响第一阶段权威批次和结算 |

### 14.1 守恒/不变量

- `CurrentCount ≤ PotentialCount ≤ PartySizeLimit`，且仅在合法计划允许的前提下计入新增兵源。
- 实际可执行的封邑单笔AI招募要么 `ApprovedCount==0`，要么 `ApprovedCount>=25`，不许中途写入18人后“失败”。
- `RecruitCount ≤ TargetCount - CurrentCount`（单次计划执行前重新计算）；玩家手动特殊入口不适用AI目标限制。
- 潜在战力计算不得修改本体实时 `EstimatedStrength`、Party实际成员、Clan金钱或第一阶段Hearth/服役台账。
- 已在Army内的Party不得为了封邑兵轮换自行退出军团；真实战斗/围城/逃跑优先。
- 返乡只能通过第一阶段结算接口，Hearth恢复只发生在真实返回来源Fief时；归还冷却兵员不能立即重新被视为就绪兵源。
- 临时动员计划失效后，没有残留“伪造的战斗力”“预扣的士兵”或多日重复扣款。

---

## 15. 风险、待定项与发布条件

### 15.1 主要技术风险

1. **本体军事AI补丁范围**：战略评分和遭遇战威胁评估可能重用相似兵力数据，必须精准作用于“规划”，不得全局修改战斗力。
2. **Army门槛双入口**：降低 `AIMobilePartySizeRatioToCallToArmy` 不代表修改了 `AiMilitaryBehavior` 主动创建Army的硬编码门槛；先保守测试。
3. **定居点招募双入口**：日常AI选地点与 `CheckRecruiting()` 真正执行规则必须一致，否则出现“目标无需补员却仍在城内征兵”的旁路。
4. **两层95%限制**：仅修改一个入口不会真正实现计划劫掠/围城100%目标，必须统一。
5. **多Party共享兵源**：只读潜在兵力不能当作独占预约；多个Party并发计划需临时协调/到达时复查。
6. **任务变更与存档**：War/Army/Settlement状态瞬时变化会使计划失效；不可无限覆盖军事AI命令。
7. **返乡循环**：若按工资减免期结束立即强制轮换，会造成无意义的免费军饷套利和军团解散；以军事状态、距离及替代兵源作为硬约束。
8. **性能**：战略潜在兵力全量计算可能昂贵；建议Clan/Party/任务事件触发或低频快照，必要时加入失效规则而非无限缓存。

### 15.2 待定平衡参数

| 参数 | 当前状态 |
|---|---|
| 和平/战时无任务/战前动员/Army/劫掠/围城/防守比例 | **首版采用0.35/0.50/0.80/0.90/1.00/1.00/0.80** |
| 常规补员启动比例 | **首版0.85** |
| AI封邑兵单笔最低成功征召人数 | **已确定25** |
| 被Army召集最低当前兵力比例 | **首版候选0.40**，须测试 |
| 主动创建Army最低兵力比例 | 先暂不改变原生0.60；视测试再定 |
| 战略预动员的最大旅行天数 | **待定**；可按任务区分 |
| 临时动员计划最大有效天数、重派冷却 | **待定** |
| 不同来源间的最终评分权重 | 保留现有文化偏好为基础，**待实测调整** |
| 返乡倾向与 `PaidServiceDays` 的关系 | 已确定随付薪服役时间增长；**曲线/权重待定** |
| 返乡专项长途任务的最低人数/距离折衷 | **待定**；不强制达到25人，25只限制新征召 |
| 和平期特殊军情允许留役的比例 | 默认全部或绝大部分返还；具体例外**待定** |

### 15.3 第二阶段完成定义

- M1～M5全部通过对应测试。
- AI在和平时有较小的日常目标，战争时能为军事任务规模化动员。
- 缺兵Party可根据**真实可兑现的潜在兵力**考虑目标，并且只在补员真正发生后用实际战力执行任务。
- AI单次封邑征召严格≥25、按现有XML具体兵种模板且不超过场景目标。
- Army成员集结与随军状态不会被AI征兵和返乡拆散。
- 付薪续役、战时轮换和战后复员能消费第一阶段的批次数据及真实返乡接口，不新增权威兵员台账。
- 全部数据与工资/Hearth/冷却守恒逻辑在第一阶段验收的基础上继续保持正确。

---

## 16. 仓库参考索引

### Mod源码

- [`AiRecruitmentBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/AiRecruitmentBehavior.cs)
- [`AiSettlementRecruitmentPatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Patches/AiSettlementRecruitmentPatch.cs)
- [`DefaultAIRecruitmentModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [`RecruitmentSimulationState.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/RecruitmentSimulationState.cs)
- [`RecruitmentPlan.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/RecruitmentPlan.cs)
- [`ClanRecruitmentBudgetManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Finance/ClanRecruitmentBudgetManager.cs)
- [`AiRecruitmentFinancialModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyFinance/Models/AiRecruitmentFinancialModel.cs)
- [`GarrisonTransferPolicyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonTransferPolicyModel.cs)
- [`FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [`FiefPartyRecruitment.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [`FiefPartyTemplate.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyTemplate.cs)
- [`fiefPartyTemplates.xml`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ModuleData/fiefPartyTemplates.xml)
- [`ModConfig.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/common/ModConfig.cs) · [`modConfigs.xml`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ModuleData/modConfigs.xml)
- [`Main.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Main.cs)

### 本体反编译源码（`code/`）

- [`AiBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/AiBehavior.cs)
- [`MobileParty.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/MobileParty.cs)
- [`MobilePartyAi.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/MobilePartyAi.cs)
- [`AiMilitaryBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/AiMilitaryBehavior.cs)
- [`AiVisitSettlementBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/AiVisitSettlementBehavior.cs)
- [`DefaultArmyManagementCalculationModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultArmyManagementCalculationModel.cs)
- [`DefaultTargetScoreCalculatingModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultTargetScoreCalculatingModel.cs)
- [`DefaultMilitaryPowerModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultMilitaryPowerModel.cs)

---

**总结**：第二阶段的重点不是让所有AI Party持续满编，而是让AI按照场景维持合理常备人数，在评估到真实可兑现的潜在兵力时才筹划更大军事行动；封邑兵作为可追踪、可付薪续役、可轮换、可战后复员的临时兵源，而不是普通Party无限使用的免费士兵。
