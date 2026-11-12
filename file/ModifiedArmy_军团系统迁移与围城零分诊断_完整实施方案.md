# ModifiedArmy 军团系统整体迁移与围城目标零分精确诊断

> **施工版 / 交付 Codex**  
> Git 仓库：`GuihuaZhou/bannerlord`  
> 审查基线：`main`，提交 `47a201ef55fc95a9bdb587fae652e9ebd978908f`（2026-11-10 核实）  
> 范围：`ModifiedPolitics` → `ModifiedArmy` 的**军团运行系统**迁移；以 Model override 进行围城评分零分的**实际分支诊断**。  
> 性质：实施规范。**本文档不是已完成的代码变更，也不表示已经编译或部署。**

## 0. 目标及不得更改的规则

### 本次必须完成

1. 所有**军团组建、资格筛选、成员选择、目标生成与评分、影响力和凝聚力消耗、军团解散操作、军团诊断**统一位于 `ModifiedArmy`。`ModifiedPolitics` 仅保留**大元帅官职数据、官职任免与资格、官职规则提供者**。
2. 保持 **`ModifiedPolitics → ModifiedArmy` 单向程序集/项目依赖**；`ModifiedArmy` 不可 `using ModifiedPolitics`、通过反射加载 Politics 类型，或新增 Politics 项目引用。
3. **Model 能覆盖的方法全部使用 Campaign Model override，不能同时使用 Harmony Patch。** 所有 `DefaultArmyManagementCalculationModel` / `DefaultTargetScoreCalculatingModel` 相关 Harmony 补丁均须核查并清理。
4. 新增**围城目标评分零分原因**诊断：区分初始兵力硬门槛、评分末端乘数清零、负值裁剪，以及目标未进入计算路径；提供实际计算中间值及可复现的 `traceId`。
5. 保留**当前游戏行为、参数、排序、加成、事件与存档兼容性**，迁移不等于调整军团平衡。**不降低 `1000` 资格门槛、不降低原版围城 `2.0` 安全系数、不强制给零分目标加分。**

### 非目标

- 不修改 `WarDisposition`、领主动员或兵粮参数。
- 不处理与军团无关的政治/外交/建设系统 Model 和 Patch；例如 `OfficeInfluencePatches`、`CourtStewardRelationPatch` 仍归 `ModifiedPolitics`，不得为了“清理 Patch”而误迁入 Army。
- `MilitaryOfficeRecruitmentPatches.cs` 属于**军事官职影响职业兵与俘虏招募**，不是军团组建系统。建议后续用供方接口替代跨 Mod Harmony，但不要与本次核心改造混在一个未经验证的改动中；如果同批处理，必须独立回归并单独报告。

## 1. 最新仓库代码审计（已经核实）

| 文件（路径相对于项目 `ModifiedPolitics/ModifiedPolitics`） | 当前作用 | 迁移要求 |
|---|---|---|
| `HeroOffices/Behaviors/MarshalArmyAiBehavior.cs`（461 行） | `AiHourlyTickEvent` 中判断统帅权限和创建资格；调用原版 `AiMilitaryBehavior` 生成军团目标，并比较行为评分；监听 `ArmyCreated` | **迁移**为 `ModifiedArmy/Army/Behaviors/MarshalArmyAiBehavior.cs` |
| `HeroOffices/Models/DiagnosticArmyManagementCalculationModel.cs`（261 行） | 复制/诊断 `CanLordCreateArmy` 候选筛选；覆写招集影响力费、凝聚力费、玩家权限判断；底层 `BaseModel` 委托 | **迁移**为 Army Model，替换其中 Politics 直接类型调用 |
| `HeroOffices/Diagnostics/MarshalArmyDebugTrace.cs`（243 行） | 军团决策上下文、`TARGET_NATIVE`、分数池、采样限流、`traceId` | **迁移**为 Army Diagnostics，扩展零分原因及日志链 |
| `HeroOffices/Diagnostics/MarshalArmyEligibilityTrace.cs`（175 行） | 资格、候选和分数归因；运行时 Model 链审计 | **迁移**为 Army Diagnostics，替换 Politics 类型识别 |
| `HeroOffices/Patches/MarshalArmyPatches.cs`（122 行） | `Kingdom.CreateArmy` 的最终军权保护及 `AiMilitaryBehavior.FindBestTargetAndItsValueForFaction` 的权限过滤 | **迁移**为 Army 的仅限非 Model Patch；查询 Provider |
| `HeroOffices/Patches/MarshalArmyDebugPatches.cs`（48 行） | 对 `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` 的 Postfix；对 `CampaignEventDispatcher.AiHourlyTick` 的只读 Postfix | **拆分**：删除评分 Model Postfix；后者只在确有必要时迁入 Army |
| `HeroOffices/Services/OfficeArmyService.cs`（51 行） | 在大元帅卸任时调用 `DisbandArmyAction`，其中包含版本兼容反射 | **迁移**为 Army 公共解散服务，Politics 只调用入口 |
| `HeroOffices/Behaviors/HeroOfficeBehavior.cs` | 持久化任命数据；`RemoveAssignment` 在大元帅卸任时调用 `OfficeArmyService` | **保留在 Politics**；将军团解散调用替换为 Army 服务 |
| `ModifiedPolitics/Main.cs` | 注册 `DiagnosticArmyManagementCalculationModel` 和 `MarshalArmyAiBehavior` | **删除这两项注册**，只注册政治侧军权 Provider |
| `ModifiedArmy/Main.cs` | 当前清理外部 Provider 并注册大部分军事 Model/Behavior | 注册军团相关 Model、Behavior；清理 Provider；在加载完成后核对实际模型链 |

已确认 `ModifiedPolitics.csproj` 通过 `ProjectReference` 引用 `ModifiedArmy.csproj`，并且 `ModifiedPolitics/SubModule.xml` 声明依赖 `ModifiedArmy`。**不可反向引用。**

### 特别注意：当前实现不是“全靠 Patch”

`DiagnosticArmyManagementCalculationModel` 已经直接 `override`：

- `CanLordCreateArmy`：影响力、粮食、领主 60% 比例、候选筛选、距离、费用排序、`1000` 战力阈值；
- `CanPlayerCreateArmy`：大元帅存在时的权限提示和判定；
- `CalculatePartyInfluenceCost`：授权统帅 `0.75` 的花费系数；
- `GetCohesionBoostInfluenceCost`：授权统帅 `0.80` 的维护系数。

**不能再增加与它们等价的 Harmony Postfix/Transpiler；尤其不能二次折扣。** 当前 `MarshalArmyPatches` 源码没有这些 Model Postfix，迁移时继续保持这一状态。

## 2. 建议目标目录

```text
ModifiedArmy/ModifiedArmy/
└─ Army/
   ├─ Contracts/
   │  └─ IArmyAuthorityProvider.cs
   ├─ Services/
   │  ├─ ArmyAuthorityProviderRegistry.cs
   │  └─ ArmyDisbandService.cs
   ├─ Behaviors/
   │  └─ MarshalArmyAiBehavior.cs
   ├─ Models/
   │  ├─ DiagnosticArmyManagementCalculationModel.cs
   │  └─ DiagnosticTargetScoreCalculatingModel.cs      # 新增
   ├─ Diagnostics/
   │  ├─ MarshalArmyDebugTrace.cs
   │  ├─ MarshalArmyEligibilityTrace.cs
   │  ├─ SiegeTargetScoreTrace.cs                     # 新增
   │  └─ ArmyModelChainAudit.cs                      # 新增或合并
   └─ Patches/
      ├─ ArmyCreationAuthorityPatch.cs              # 仅 Kingdom.CreateArmy
      ├─ ArmyGatheringAuthorityPatch.cs             # 仅 AiMilitaryBehavior 非 Model
      └─ ArmyFinalDecisionTracePatch.cs             # 可选、只读的事件池观测

ModifiedPolitics/ModifiedPolitics/
└─ HeroOffices/
   ├─ Behaviors/HeroOfficeBehavior.cs               # 保留官职存档、任免
   └─ Integration/MarshalArmyAuthorityProvider.cs   # 新增
```

> `MilitaryOfficeRecruitmentPatches`（职业兵/驻军加成）、`OfficeInfluencePatches` 和非军团官职服务不属于上述迁移清单。旧军团文件迁完应删除，避免两个程序集同时注册事件或 Harmony 补丁。

## 3. 政治模块与军团模块的权限接口

由 `ModifiedArmy` 定义公共的中立接口，`ModifiedPolitics` 实现。示意契约：

```csharp
public interface IArmyAuthorityProvider
{
    bool HasAppointedMarshal(Kingdom kingdom);
    bool IsAppointedMarshal(Hero hero, Kingdom kingdom);
    bool IsEligibleMarshal(Hero hero, Kingdom kingdom);
}
```

- Registry 位于 `ModifiedArmy.Army.Services`，提供 `Register` / `Clear` / `Try...` 查询；生命周期和容错方式参考现有 `WarDispositionProviderRegistry`，不持有跨存档 `Hero/Kingdom` 引用。
- `ModifiedPolitics.HeroOffices.Integration.MarshalArmyAuthorityProvider` 读取 `HeroOfficeBehavior.Current.GetAssignments(kingdom, OfficeType.Marshal)`、`OfficeRules.IsEligible`；Politics 的 `Main.OnGameStart` 注册。
- `ModifiedArmy/Main.OnGameStart` 首先清理旧 Registry；Politics 完成其官职初始化后注册；在 `OnSessionLaunched` 记录注册状态。仔细验证模块加载顺序，**不可因 Army 清理调用过晚而清空 Politics 刚注册的 Provider**。
- Provider 缺失时使用**无官职限制**的兼容回退：`HasAppointedMarshal=false`；统治者和原版家族领袖仍按原版规则行动。不得因为 Politics 暂时未注册就禁止所有 AI 创建军团。
- `MarshalArmyAiBehavior` 在判断领袖是否为“在任且合格的大元帅”时读取 Provider；统治者不需要大元帅身份；玩家继续手动控制。
- `DiagnosticArmyManagementCalculationModel` 的 `IsArmyAuthority/HasAnyMarshal` 改用 Registry，不能直接读取 Politics 的类型。
- `HeroOfficeBehavior.RemoveAssignment` 中的大元帅解散请求调用 `ModifiedArmy.Army.Services.ArmyDisbandService.DisbandLedArmy(hero)`；具体 `DisbandArmyAction` 兼容逻辑只在 Army 实现。保留原有卸任语义和安全检查。
- 玩家端 `MP_MarshalRequired` 文本可保留 **ID 不变**，但其资源需按运行时加载策略迁移到 Army 的 `modifiedarmy-zho-CN.xml`；避免同 ID 双重定义。所有军团调试和 UI 文本从 Politics 移出后，验证本地化没有缺失。

## 4. 迁移 Model、Behavior、非 Model Patch 的边界

### 4.1 军团管理 Model

- 在 Army 中注册**唯一** `DiagnosticArmyManagementCalculationModel`，并删除 Politics 中同名注册。
- 完整保留 `BaseModel` 委托和当前 1000 资格战力阈值、候选列表排序/截断规则。**特别保留当前候选 `OrderByQ` 升序行为**（虽然它值得另行审计，本次禁止顺手修正）。
- 将政治官职判断替换为 Registry。不能把本次迁移变成原版筛选逻辑或战斗力公式的重写。
- 输出 `MODEL_CHAIN`、`MODEL_ACTIVE`、`CanLordCreateArmy.RESULT` 和身份适配信息；升级日志字段，但不影响 AI 结果。

### 4.2 军团 AI Behavior

- 原 `MarshalArmyAiBehavior` 迁入 Army；在 `AiHourlyTick` 中保持当前真实行为：
  1. 主力必须是王国统治者或有资格的大元帅；
  2. 领主不在海上、不在战斗、不已经隶属于军团；统帅规模比例大于 `0.6`；
  3. 调用实际 `ArmyManagementCalculationModel.CanLordCreateArmy` 取得成员；
  4. 使用候选部队 `GetCustomStrength(Attacker, Siege)` 合计预计集结战力；
  5. `thinkParams.SetArmyMembers(candidates)` 和 `WillGatherAnArmy`；
  6. 调用原版 `AiMilitaryBehavior.FindBestTargetAndItsValueForFaction`，跳过 `Raider`；
  7. 对追加的军团候选评分应用现有权重（军团数量、影响力、规模比例）；
  8. 按原版 AI 行为池比较，**不强制创建军团**。
- 保留 `try/finally` 恢复 `thinkParams.WillGatherAnArmy`，禁止提前返回时留下错误上下文。
- 注册 `AiHourlyTickEvent` 和 `ArmyCreated` 监听**各一次**。

### 4.3 Patch 删除/保留清单

| 原 Patch | 处置 | 理由 |
|---|---|---|
| `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` Postfix | **删除** | 有对应 Campaign Model；由新的诊断 Model 单次评分 |
| `DefaultArmyManagementCalculationModel.CanLordCreateArmy` / 影响力费用 / 凝聚力费用（若其他分支有旧补丁） | **彻底删除/不允许新增** | 当前 ArmyManagement Model 已重写 |
| `Kingdom.CreateArmy` Prefix | **迁入 Army 并保留** | 最终创建操作不是 Model；确保无权限者不能绕过 UI/AI |
| `AiMilitaryBehavior.FindBestTargetAndItsValueForFaction` Prefix | **迁入 Army 并保留** | 非 Model 的原版行为权限边界；禁止普通领主绕过官职规则 |
| `CampaignEventDispatcher.AiHourlyTick` Postfix | **可以保留为只读观测** | 不是 Model；记录所有监听器结束后的评分池，用于分辨“零分”和“有分却未选中”。应注明为何需要 Postfix，且严禁改结果 |
| `MilitaryOfficeRecruitmentPatches` | **不在本次强行迁移** | 军职招募生产的独立接口整治；后续替代为 Army 直接调用军事官职加成 Provider，不在本次军团零分诊断里混改 |

## 5. 围城目标评分为 0：必须实施的精确诊断

### 5.1 已确认的原版公式位置与含义

依据项目 `code/DefaultTargetScoreCalculatingModel.cs` 及用户提供的 `DefaultTargetScoreCalculatingModel(1).cs`，核心入口：

```csharp
public override float GetTargetScoreForFaction(
    Settlement targetSettlement,
    Army.ArmyTypes missionType,
    MobileParty mobileParty,
    float ourStrength)
```

原版存在至少两个性质不同的零分路径：

**A. 初始攻城兵力硬门槛：**

```csharp
float initialDefense = localStrength * (wallFactor - randomFactor)
                     + initialKingdomSupport + 0.1f;
// 目标无现存围城时，Besieger 通常采用安全系数 2.0
if (targetSettlement.SiegeEvent == null &&
    ourStrength < initialDefense * siegeSafetyFactor)
    return 0f;
```

原版公式中 `localStrength` 来自目标城镇内部各部队的 `Party.EstimatedStrength`，包括有效驻军、民兵与其他激进部队；`initialKingdomSupport` 与**敌国总兵力、敌国定居点数量**有关，并受城镇/城堡倍数影响；`wallFactor` 与城墙等级及粮仓状态有关；`randomFactor` 来自统帅英雄随机修正。不要将“驻军人数”当作“EstimatedStrength”。

**B. 最终分数被乘数归零或负值裁剪：**

```csharp
float score = relationFactor * relativeStrengthFactor * valueFactor
            * strategyFactor * behaviorFactor * distanceFactor
            * ...;
return score < 0f ? 0f : score;
```

实际代码使用 `num29 * num6 * num25 * num26 * num27 * num30 * num31 * num32 * num23` 等变量；相关的 `num32` 在友军已有围城且目标无法计算相关防守方战力时可能为零。这一分支和初始兵力不足必须**明确区分**。

**重要：** 后续“预计敌方支援”和“敌国总体军事力量”还会更新防御估计与**最终相对强度分数**；不要错误地将所有中间值都纳入前面的 `2 × initialDefense` 门槛。日志必须分别记录**初始判定**和**后续最终评分**。

### 5.2 Model-only 的实施方式（禁止评分 Patch）

新增 `ModifiedArmy.Army.Models.DiagnosticTargetScoreCalculatingModel : TargetScoreCalculatingModel`：

1. 非被采样的情况和非 `Besieger` 模式，直接委托 `BaseModel`，**不要更改战帆 DLC 的海军巡逻、原版目标评分或故事模式评分**。
2. 在正在记录的军团围城目标计算时，采用**一次真正执行的原版等价代码路径**，在原版每个判定位置写日志，并**返回本次计算所得的真实分数**。
3. `GetTargetScoreForFaction` 的等价路径必须以**当前安装的 Bannerlord/War Sails DLL**、项目上传反编译文件为依据逐项核验，严格保持调用次序、随机数消费、浮点公式、早返回、集合枚举和边界行为。此前在其他分支或其他版本查到的反编译实现只可作为比对，不能无条件假设等价。
4. **严禁**先 `BaseModel.GetTargetScoreForFaction(...)` 取得 0，再执行第二次含 `RandomFloat` 的公式推导，并把第二次结果称为“精确原因”。也不允许“直接包装 BaseModel，然后根据静态城防猜测原因”并标记为精确。
5. 只有实际运行 `BaseModel` 继承链明确且与单次诊断代码等价时，才启用精确分支；如检测到未知模型/版本不匹配，应**直接委托 BaseModel**、写 `MODEL_CHAIN_UNVERIFIED` / `ZERO_REASON_UNCONFIRMED`，不破坏其他 Mod。
6. 其余抽象属性和方法必须完整委托 `BaseModel`（包含巡逻/当前目标价值）：`TravelingToAssignmentFactor`、`BesiegingFactor`、`AssaultingTownFactor`、`RaidingFactor`、`DefendingFactor`、`GetDefensivePatrollingFactor`、`GetOffensivePatrollingFactor`、`CalculateDefensivePatrollingScoreForSettlement`、`CalculateOffensivePatrollingScoreForSettlement`、`CurrentObjectiveValue`。不得从 `DefaultTargetScoreCalculatingModel` 直接继承并覆盖 NavalDLC 行为。
7. 在 `ModifiedArmy/Main.cs` 注册诊断 Model，删除相关 Harmony Postfix。**检查最终活动 Model 是否为新增诊断类，并且 BaseModel 链仍包含 NavalDLC/Default（及实际存在的 StoryMode）**。若加载顺序导致 Army 注册的 Model 被 DLC 或其他模块覆盖，应解决 Mod 加载/模型注册次序；不得通过新 Harmony Patch 强制覆盖。

> **实现可行性限制：** “仅靠 BaseModel 包装并记录返回值”不可能直接得到内部早返回的精确局部变量。要满足本任务的“精确零分原因”，只能在受控、版本一致的单次执行路径中插桩。没有完成等价性审计时，必须承认日志仅为推断而不是精确诊断。

### 5.3 必须输出的诊断字段

每次被采样的围城目标至少包含：

| 类别 | 字段 | 意义 |
|---|---|---|
| 关联 | `traceId`, `campaignDay`, `hour`, `kingdom`, `leaderPartyId`, `armyType`, `targetId` | 一次军团评估、一座目标的唯一追踪 |
| 模型 | `activeScoreModel`, `baseModelChain`, `calculationMode` | 真实 Model 链与是否精确单次诊断 |
| 攻方 | `ourSiegeStrength`, `candidateCount`, `gatheringStrength` | 本次目标评分实际输入的攻城战斗力 |
| 城内兵力 | `localStrength`, `garrisonStrength`, `militiaStrength`, `otherLocalStrength` | 以 `EstimatedStrength` 为准，避免将人数相加误算 |
| 王国数据 | `enemyKingdomStrength`, `enemySettlementCount`, `initialKingdomSupport`, `subsequentKingdomSupport` | 解释敌国整体军力怎样影响原版预估 |
| 城防 | `wallLevel`, `wallFactor`, `foodStocks`, `randomFactor`, `targetHasActiveSiege` | 城墙、粮仓及随机修正的**本次真实取值** |
| 初次硬门槛 | `initialDefenseStrength`, `safetyFactor`, `requiredSiegeStrength`, `strengthShortfall`, `passedInitialGate` | 尤其用于验证 `2 × initialDefense` 是否挡住戈里戈斯 |
| 后续强度 | `nearbyEnemyStrength`, `projectedFinalDefense`, `relativeStrengthFactor` | 避免将后续支援与初始门槛混为一谈 |
| 最终乘数 | `relationFactor`, `valueFactor`, `objectiveFactor`, `distanceFactor`, `siegeOverlapFactor`, `otherFinalFactors` | 当初始门槛通过但最终变成 0 时定位因子 |
| 结果 | `rawScore`, `returnedScore`, `zeroReasonCode`, `zeroFactorName`, `decisionStage` | 可以由程序做聚合统计 |

`otherFinalFactors` 可按原始 `num*` 与语义名同时打印，以便逐项和反编译源码核对。所有因子数值应在**实际计算发生时**保存到局部诊断快照，不得重复调用随机方法。

### 5.4 零分原因枚举（必须可机器归类）

- `ZERO_INITIAL_SIEGE_STRENGTH_GATE`：首次 `ourSiegeStrength < requiredSiegeStrength`，记录不足量。
- `ZERO_FINAL_MULTIPLIER`：初始硬门槛通过，但最终某因子为 0；输出 `zeroFactorName` / 各乘数；若多个同时为 0，全部列出。
- `ZERO_FINAL_NEGATIVE_CLAMP`：原始 `rawScore < 0` 被原版裁剪为 0，记录负值来源相关因子。
- `ZERO_FINAL_SCORE_OTHER`：确实返回 0 但还无法归入上述类，不得捏造原因。
- `NONFINITE_SCORE`：`NaN` / 正负无穷；**不能改写成 0**，必须注明它不是普通零分。
- `MODEL_CHAIN_UNVERIFIED`：运行时模型链不符合单次等价诊断前提，需委托原版；其零分原因只能写 `ZERO_REASON_UNCONFIRMED`。
- `TARGET_NOT_SCORED`：目标在 `AiMilitaryBehavior` 前置过滤或无导航方案等情况下没有进入目标评分 Model；**不是 Model 返回 0**，应在前后 `TYPE_START/TYPE_SUMMARY` 中区分。
- `POSITIVE_SCORE`：原始目标为正分，记录对照样本，证明诊断没有将全部目标压成 0。

### 5.5 日志示例（数值是格式示例，非真实游戏事实）

```text
[Army Target Score] phase=TARGET_RESULT
traceId=empire_w:CharacterObject_3158:day91725:tick404
leader=戈里戈斯 armyType=Besieger target=肖农(town_B...) 
calculationMode=INSTRUMENTED_SINGLE_PASS
ourSiegeStrength=1887.297 localStrength=520.000
initialKingdomSupport=610.000 wallFactor=1.660
randomFactor=0.045 initialDefenseStrength=1450.000
safetyFactor=2.000 requiredSiegeStrength=2900.000
strengthShortfall=1012.703 passedInitialGate=false
returnedScore=0.000000 zeroReasonCode=ZERO_INITIAL_SIEGE_STRENGTH_GATE
```

```text
[Army Target Score] phase=TARGET_RESULT
traceId=... target=... calculationMode=INSTRUMENTED_SINGLE_PASS
ourSiegeStrength=... initialDefenseStrength=...
passedInitialGate=true relationFactor=1.000 valueFactor=0.842
relativeStrengthFactor=1.930 distanceFactor=0.690 siegeOverlapFactor=0.000
rawScore=0.000000 returnedScore=0.000000
zeroReasonCode=ZERO_FINAL_MULTIPLIER zeroFactorName=siegeOverlapFactor
```

实际日志必须使用**本次评分的真实变量值**，不要简单复制这些示例值。

### 5.6 上下文、采样与不变性

- `MarshalArmyDebugTrace.PushTargetContext` 只覆盖大元帅/王国统治者执行军团创建目标评估时的 `Besieger` 计算；沿用 `traceId` 与 `TargetScoreContext`，通过 `try/finally` / `IDisposable` 恢复 ThreadStatic 状态。
- 一名统帅每天至少一次完整目标明细；额外同日调用可只记录分布统计。**不要因为采样限额漏掉连续零分目标**：每个被评分为零的目标应至少在日汇总中列明原因和次数；提供 `omittedCount`。
- 同一 `traceId` 要连接 `ELIGIBILITY_RESULT → TYPE_START → TARGET_RESULT → TYPE_SUMMARY → FINAL_POOL_AFTER_EVENT → CREATE_ARMY_ATTEMPT/CREATE_ARMY_SUCCESS_EVENT`。
- 对于 `TARGET_NOT_SCORED`，若原版过滤函数无法无侵入地记录每个前置分支，至少从入口目标集合与 Model 实际调用计数区分“未入评分”“评分为零”，不能把二者混写。
- 日志必须异常隔离、仅输出文件、不弹窗；失败时不使 AI 评分失败。禁止对评分列表、行为、候选成员或随机数状态做诊断性修改。

## 6. 原版、战帆 DLC、其他 Mod 的继承链检查

当前旧日志确认过 `ArmyManagementCalculationModel` 的活动链：

```text
ModifiedPolitics.HeroOffices.Models.DiagnosticArmyManagementCalculationModel
→ NavalDLC.GameComponents.NavalDLCArmyManagementCalculationModel
→ TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel
```

**迁移后应是**：

```text
ModifiedArmy.Army.Models.DiagnosticArmyManagementCalculationModel
→ NavalDLC.GameComponents.NavalDLCArmyManagementCalculationModel
→ TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel
```

目标评分 `TargetScoreCalculatingModel` 应相应呈现：

```text
ModifiedArmy.Army.Models.DiagnosticTargetScoreCalculatingModel
→ <实际运行时的 NavalDLC/StoryMode/其他合法模型>
→ TaleWorlds.CampaignSystem.GameComponents.DefaultTargetScoreCalculatingModel
```

不能**硬编码假定**每次有且只有这三层；应完整记录运行时链，并检查是否存在环、`BaseModel=null`、错误顺序、其他 Mod 覆盖、同一类型重复注册。特别注意：`ModifiedArmy` 比依赖它的 `ModifiedPolitics` 更早加载，**不能在移动注册后想当然认为 Army 的 Model 会继续处于最外层**。如果运行时不是预期链，此次改造验收不通过，不准用 Patch 隐藏问题。

## 7. 中文文本及日志归属

- 将 `MP_MarshalArmyAi*`、`MP_MarshalArmyDebugPrefix/FieldMap`、`MP_ArmyEligibilityModelPrefix/FieldMap`、`MP_MarshalRequired` 等由迁移后 Army 执行代码使用的本地化资源，按引用清单移动到 Army 的 `ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml`，不要重复定义同 ID。
- 只由 Politics 官职 UI 使用的文本继续留 Politics；保留已存在文本 ID 以兼容旧本地化。
- 日志前缀建议统一 `[ModifiedArmy][Army]`，原有阶段名如 `TARGET_NATIVE`、`FINAL_POOL_AFTER_EVENT` 能保留则保留；新增零分阶段 `TARGET_RESULT` 不应复用旧的 `NATIVE_TARGET_ZERO_REASON_UNKNOWN`。
- 限定调试输出规模：避免对所有普通领主的每次目标评分输出全部因子，导致持续高流量/大日志。

## 8. Codex 分阶段施工步骤

### 阶段 A：建立接口及迁移纯服务（不改运行行为）

1. 新建 Army 权限接口、Registry、Army 解散服务。
2. Politics 实现并注册 `MarshalArmyAuthorityProvider`，替换 `HeroOfficeBehavior.RemoveAssignment` 的解散服务调用。
3. 修正 Main 初始化/清理顺序；验证缺少 Provider 的回退，不留跨存档旧状态。
4. 编译并验证原官职任免/卸任解散路径。

### 阶段 B：迁移军团运行代码、删除重复注册

1. 原封不动迁移 `MarshalArmyAiBehavior`、`DiagnosticArmyManagementCalculationModel`、两份 Diagnostics；只替换 Politics 直接类型依赖为 Registry、统一 Logger 和 namespace。
2. Army 注册唯一 Model 与 Behavior；Politics 删除旧 `AddModel`、`AddBehavior`。
3. 迁移 `Kingdom.CreateArmy` 和 `AiMilitaryBehavior` 非 Model Patch；删除 Politics 旧 Patch 类。
4. 将 `CampaignEventDispatcher.AiHourlyTick` 的只读评分池追踪迁入 Army（若能通过事件/Behavior 顺序准确实现可进一步去 Patch，但**不能为了减少 Patch 改变观测语义**）。
5. 验证候选部队、费用、目标评分与迁移前相同。

### 阶段 C：精准围城零分诊断 Model

1. 对当前安装 DLL 与提交版本的目标评分源码逐分支比对，确认 NavalDLC 包装的 `GetTargetScoreForFaction` 委托行为和 StoryMode 例外范围。
2. 新建 `DiagnosticTargetScoreCalculatingModel`，委托所有无关分数/抽象接口；实现仅被采样 `Besieger` 的单次执行精确诊断。
3. 删除旧 `DefaultTargetScoreCalculatingModel` Harmony Postfix；启动时输出完整 Model 链及诊断激活状态。
4. 实现第 5 章全部 `reasonCode`、字段、采样统计。
5. 对同一存档做**迁移前后/诊断开关前后**的模拟比对，重点检查评分结果、随机数消费与目标排序；确认无差异后才允许开启详细日志。

### 阶段 D：整体验收与清理

1. 清理 Politics 旧 C# 文件、无用 using/namespace、翻译资源与旧单独日志入口。
2. 确认 `ModifiedArmy` **完全没有依赖** `ModifiedPolitics` 的项目引用、类型名、反射路径。
3. 检查 `Harmony.GetPatchInfo` 等运行时审计：军团相关 Patch 只剩明确的非 Model 入口；`DefaultTargetScoreCalculatingModel` 与 `DefaultArmyManagementCalculationModel` 没有本 Mod 的对应 Patch。
4. 报告所有未能实施或需手动验证的项目，不得以“编译成功”替代游戏内行为验证。

## 9. 验收与回归测试（必须逐项汇报）

### 编译与结构

- [ ] `ModifiedArmy` Release 编译 0 错误；`ModifiedPolitics` Release 编译 0 错误。
- [ ] Army → Politics 反向依赖为 0；Politics → Army 单向正常。
- [ ] 两个 Main 不再重复注册军团 Model / Behavior。
- [ ] 模块实际活动 `ArmyManagementCalculationModel` 和 `TargetScoreCalculatingModel` 链正确，NavalDLC 功能保留。
- [ ] Harmony 审计：没有任何对已接管 Model 方法的 Army/Politics Patch。
- [ ] 政治官职保存、读档、任命、撤职和卸任解散不丢失。

### 军团功能

- [ ] 统治者可创建军团；在任合格大元帅可创建；普通领主在该国有大元帅时不能绕过限制。
- [ ] 无大元帅或 Politics 未注册时，原版可用路径不会被无端封禁。
- [ ] 原有费用折扣 `0.75` / `0.80` **只生效一次**。
- [ ] 候选部队、粮食与距离筛选、战力 1000 门槛的前后行为一致。
- [ ] 海上/陆地 AI、正常攻城、巡逻、故事模式、战帆 DLC 的评分没有异常。

### 零分诊断专项

- [ ] 戈里戈斯评估巴旦尼亚目标时，输出各目标 `TARGET_RESULT`；至少含肖农、马鲁纳斯等实际在日志中出现的目标（如果当前存档仍包含）。
- [ ] `ZERO_INITIAL_SIEGE_STRENGTH_GATE` 包含真实 `initialDefenseStrength`、`safetyFactor`、`requiredSiegeStrength`、`strengthShortfall`，可验证 `ourStrength < requiredSiegeStrength`。
- [ ] 当首次门槛通过而仍评分为 0，能指出**哪个实际末端因子**导致零分；不得一律输出 `UNKNOWN`。
- [ ] `rawScore`、`returnedScore`、`AIBehaviorScores` 中的相关目标分数可以按链路对应，误差/舍入规则明示。
- [ ] 同存档、同输入、同随机状态条件下，诊断开关前后评分与决策一致；不能因为日志而多消耗 `RandomFloat`。
- [ ] 能区分“评分 Model 返回零”与“目标根本没有进入评分 Model”；能统计正分/零分/未评分数。
- [ ] 至少有一组正分对照样本（如其他王国的可攻目标），确认完整运行路径并非总零分。
- [ ] 日志不会无上限地输出；详细采样不妨碍按日统计零分原因。

## 10. Codex 最终交付格式

必须交付以下内容（**以实际执行结果为准，不要虚报**）：

1. 本次新建/迁移/删除文件的完整路径清单；每处原因。
2. 移除的 Model Patch 清单与保留非 Model Patch 的依据。
3. Army/Politics 的 Provider 接口与实际初始化顺序说明。
4. 实际活动 `ArmyManagementCalculationModel` 和 `TargetScoreCalculatingModel` 继承链日志。
5. 两项目 Release 编译结果，架构门禁结果与游戏内验证状态（PASS/BLOCKED）。
6. 至少两条同一轮戈里戈斯围城目标的完整分支日志（有真实 `reasonCode` 和中间值），以及一条正分对照。
7. 说明是否存在必须等待本机 DLL 反编译才能确认的差异；不能把尚未验证的零分原因定性为已修复。

---

## 参考代码索引（GitHub `main` @ `47a201ef`）

- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/DiagnosticArmyManagementCalculationModel.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Diagnostics/MarshalArmyDebugTrace.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Diagnostics/MarshalArmyEligibilityTrace.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyDebugPatches.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Services/OfficeArmyService.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/HeroOfficeBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/Main.cs`
- `ModifiedArmy/ModifiedArmy/Main.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Services/WarDispositionProviderRegistry.cs`（Provider 设计参考）
- `code/DefaultTargetScoreCalculatingModel.cs`（项目原版反编译参考）
- 用户上传：`DefaultTargetScoreCalculatingModel(1).cs`（与零分分支的逐行核验参考）

**最终原则：先保持行为不变地迁移，再以单次实际计算记录零分原因；在确认根因之前，绝不修改军团平衡参数。**
