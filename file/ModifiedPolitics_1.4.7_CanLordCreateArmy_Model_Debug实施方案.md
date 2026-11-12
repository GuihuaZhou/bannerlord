# ModifiedPolitics — 1.4.7 `CanLordCreateArmy` 诊断 Model 实施方案

> **交付类型：供 Codex / 编码 AI 直接执行的实施任务书（不是已完成的代码修改）**  
> **目标版本：**《骑马与砍杀 II：霸主》1.4.7；以用户提供的 `DefaultArmyManagementCalculationModel(1).cs`（本次第三份源码）为唯一复制基准。  
> **目标项目：** `GuihuaZhou/bannerlord` → `ModifiedPolitics/ModifiedPolitics`。  
> **目标：** 新建继承 `DefaultArmyManagementCalculationModel` 的 Mod Model，`override CanLordCreateArmy`，**保留原版计算及现有大元帅身份适配效果，只增加有明确原因码的 Debug 日志**。  
> **不得扩展任务：** 不修复军团创建、不调整阈值、不改 `ModifiedArmy`、不替换战帆规则、不修改存档格式。

## 0. AI 执行前须核实的源码

1. 本任务的原版基准：`DefaultArmyManagementCalculationModel(1).cs`，`CanLordCreateArmy` 在 **186–270 行**。注意另一份同名不带 `(1)` 的反编译文件没有该方法，不是这次的实现依据。
2. DLC：`NavalDLCArmyManagementCalculationModel.cs` 第 123–126 行：
   ```csharp
   public override bool CanLordCreateArmy(MobileParty leaderParty,
       out MBList<MobileParty> possibleArmyMembers)
   {
       return base.BaseModel.CanLordCreateArmy(leaderParty, out possibleArmyMembers);
   }
   ```
   **重要：** DLC 在本方法中只是委托 `BaseModel`，但其其他方法（例如 `CalculateDailyCohesionChange`）含真实战帆业务规则。
3. 当前 Mod（GitHub `main`）：
   - `ModifiedPolitics/ModifiedPolitics/Main.cs` — `OnGameStart` 注册其他 Models、Behaviors。
   - `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs` — 对 `DefaultArmyManagementCalculationModel.CanLordCreateArmy` 有 Prefix、Postfix、Finalizer、Transpiler；Transpiler 替换 **两处 `Clan.Leader`**，用于大元帅身份适配。
   - `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs` — 在 `OnAiHourlyTick` 中调用当前模型的 `CanLordCreateArmy`。
   - `ModifiedPolitics/ModifiedPolitics/Utils/ModLogger.cs` — 命名空间 `ModifiedPolitics.Tool`；提供 `ModLogger.FileDebug(string)`；仅当 **启用文件日志**且**文件最低日志等级为 Debug** 时输出。
   - `ModifiedPolitics/ModifiedPolitics/ModifiedPolitics.csproj` — 已引用当前游戏 `TaleWorlds.CampaignSystem.dll` 与 `TaleWorlds.LinQuick.dll`；无需对战帆程序集增加静态引用。
4. `code/` 下反编译代码仅作版本对照，不要修改；如其版本与本次上传版不一致，不允许用仓库旧源码覆盖 1.4.7 行为。

### 关键既有事实

过去日志显示西帝国大元帅戈里戈斯 `CanLordCreateArmy` 连续返回 `false` 且对外 `candidates=0`。1.4.7 新方法在 **最终集结攻城战斗力 `<1000` 时，先 `possibleArmyMembers.Clear()` 再返回 `false`**，因此“返回后候选 0”不代表筛选阶段一定没有候选。

---

## 1. 强制的不变式 / 非目标

编码 AI 必须遵守以下全部要求：

- **唯一允许的运行时新行为是写诊断日志。** 所有 `if` 判定、比较符号（尤其 `>`、`<`）、执行顺序、短路顺序、枚举顺序、排序方向、随机数调用次数、调用成本次数、候选集及返回语义与 1.4.7 一致。
- 保留既有 `MarshalArmyPatches` 中的大元帅身份适配；这是既有 Mod 行为，不能因新 Model 失效。不得额外允许普通领主组军团。
- **不改变**影响力 `>100f`、领队兵力 `>AIMobilePartySizeRatioToCallToArmy`、候选兵力 `>` 同一阈值、候选粮食 `>MinimumNeededFoodInDaysToCallToArmy`（默认 15）、70% 王国容量计算、最后 1000 集结攻城战力门槛。
- 保留 `list.OrderByQ(x => x.Item2).ToListQ()` **升序**及 `list.RemoveRange(num5, list.Count - num5)`；即使看起来值得优化，也严禁顺手改成降序或 LINQ 的其他排序。
- 必须保留 `GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege)` 的上下文和调用次数。
- 不为诊断而再次调用 `CanLordCreateArmy`、`GetMobilePartiesToCallToArmy`、`CalculatePartyInfluenceCost`、`GetCustomStrength`、`GetNumDaysForFoodToLast` 或任何可能消耗随机数的函数。**只记录本次自然计算得到的局部变量。**
- 不能将日志的开关、采样结果、缓存写入存档；不能添加 `CampaignBehaviorBase` 保存字段、增加新依赖或更改 MCM 配置结构。调试数据保存在内存，战役退出后销毁。
- 不能在游戏初始化时预先“测试调用”资格方法；不要用真实游戏状态进行副作用不明的 A/B 双执行。
- 日志格式使用英文固定原因码和单行 `key=value`，玩家/王国中文名称可辅助展示；必须同时输出稳定的 StringId，便于关联日志。
- 原方法产生的异常仍按原路径传播，不能由于诊断而吞异常、转换为 `false` 或改变 `out` 参数。

---

## 2. 新增的 Model 类及继承链（P0）

**新增文件：**

`ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/DiagnosticArmyManagementCalculationModel.cs`

**类：**

```csharp
namespace ModifiedPolitics.HeroOffices.Models
{
    public sealed class DiagnosticArmyManagementCalculationModel
        : DefaultArmyManagementCalculationModel
    {
        public override bool CanLordCreateArmy(
            MobileParty mobileParty,
            out MBList<MobileParty> possibleArmyMembers)
        {
            // 按第三份上传文件完整逐句移植 1.4.7 的方法体。
            // 分支上只加文件 Debug，详见第 3 节。
        }

        // 其他每个公开的 Model override 都应透明委托 BaseModel。
        // 严禁无意识继承 Default 的旧业务实现覆盖 DLC。
    }
}
```

### 2.1 为什么必须透明委托非目标方法

1. 战帆模型继承 `ArmyManagementCalculationModel`，其多数方法转发 `BaseModel`，但至少 `CalculateDailyCohesionChange` 有诺德文化加成；简单让新派生类成为最顶层并继承 Default 的其他实现，会绕开 DLC 方法。
2. **不推荐直接继承 `NavalDLCArmyManagementCalculationModel` 再注册：**如果新实例的 `BaseModel` 已是 NavalDLC 实例，继承的 DLC `CalculateDailyCohesionChange` 会委托 DLC 后又施加一次诺德加成，可能叠加两次；并且会引入 NavalDLC 编译依赖。
3. 方案采用 `Diagnostic... : DefaultArmyManagementCalculationModel`，并在 **除 `CanLordCreateArmy` 以外的所有可覆盖军团管理成员**显式委托 `BaseModel`；通过运行时模型链核对保证没有循环、重复或丢失 DLC 行为。

### 2.2 需要逐项代理的 8 个属性

下面写法为伪代码级别的签名示范，实际 API/可访问性以本次游戏 DLL 为准：

```csharp
public override float AIMobilePartySizeRatioToCallToArmy =>
    BaseModel.AIMobilePartySizeRatioToCallToArmy;
public override float PlayerMobilePartySizeRatioToCallToArmy =>
    BaseModel.PlayerMobilePartySizeRatioToCallToArmy;
public override float MinimumNeededFoodInDaysToCallToArmy =>
    BaseModel.MinimumNeededFoodInDaysToCallToArmy;
public override float MaximumDistanceToCallToArmy =>
    BaseModel.MaximumDistanceToCallToArmy;
public override int InfluenceValuePerGold => BaseModel.InfluenceValuePerGold;
public override int AverageCallToArmyCost => BaseModel.AverageCallToArmyCost;
public override int CohesionThresholdForDispersion =>
    BaseModel.CohesionThresholdForDispersion;
public override float MaximumWaitTime => BaseModel.MaximumWaitTime;
```

### 2.3 需要逐项代理的 10 个方法

以下必须全部使用 `BaseModel.同名方法(...)`，不是 `base.同名方法(...)`（后者会进入 `DefaultArmyManagementCalculationModel` 的实现，可能绕过 DLC）：

```text
DailyBeingAtArmyInfluenceAward(MobileParty)
CalculatePartyInfluenceCost(MobileParty, MobileParty)
CalculateTotalInfluenceCost(Army, float)
GetPartySizeScore(MobileParty)
CalculateDailyCohesionChange(Army, bool)
CalculateNewCohesion(Army, PartyBase, int, int)
GetCohesionBoostInfluenceCost(Army, int)
GetPartyRelation(Hero)
CanPlayerCreateArmy(out TextObject)
CheckPartyEligibility(MobileParty, out TextObject)
```

保留 `bool includeDescriptions = false`、`int percentageToBoost = 100` 等可选参数定义（如有），保证默认调用语义一致。

**注册前核对 `BaseModel` 的实际赋值：**这一方案依赖 Campaign 的模型链机制；AI 必须实际检查游戏 DLL/运行时最终链条，确认 `Diagnostic` 能委托到真实旧模型而不会自引用。若无法证实，**不得强行注册不完整的派生模型**，应报告阻塞原因，而不是牺牲 DLC 行为换取日志。

### 2.4 注册位置与运行时自检

在 `ModifiedPolitics/ModifiedPolitics/Main.cs` 的 Campaign 分支中、其他 `AddModel` 附近：

```csharp
campaignStarter.AddModel(new DiagnosticArmyManagementCalculationModel());
```

使用项目已有 `HeroOffices.Models` 命名空间。根据真实装载顺序、Model 链构造顺序验证两种情况：

- **战帆在外：**`NavalDLCModel -> DiagnosticModel -> Default/previousModel`。DLC 的 `CanLordCreateArmy` 应委托进 Diagnostic；其他方法由 Diagnostic 向下转发，DLC 特例仅执行一次。
- **诊断在外：**`DiagnosticModel -> NavalDLCModel -> Default/previousModel`。诊断本方法直接执行，其他方法委托到 NavalDLC，DLC 特例仅执行一次。

到 `OnSessionLaunched` 后通过只读属性或反射（仅在启动自检阶段）输出 `MODEL_CHAIN`：当前有效模型完整类名、`BaseModel` 类名、链深（限制 8 层）、是否检测到自引用、诊断实例位置及战帆实例位置；尽可能在回调中读取 `Campaign.Current.Models.ArmyManagementCalculationModel`，不可仅凭 `AddModel` 成功认定生效。

**通过标准：**最终有效方法实际能命中 Diagnostic（在调用时记录一条 `MODEL_ACTIVE`），且独立观察诺德凝聚力加成没有翻倍或消失。若其他 Mod 覆盖 Model，输出 `MODEL_NOT_ACTIVE` 并将任务标为未通过，不允许假装诊断已生效。

---

## 3. `CanLordCreateArmy` 逐分支日志（P0）

### 3.1 总体规则

以用户上传的 1.4.7 源码 **第 188 行至 269 行** 完整移植；不要使用旧版 `GetMobilePartiesToCallToArmy()` 作为替代。本方法有自己的一套逐队资格筛选、排序、军团容量与战力检查。

新建一个仅供此 Model 使用的内部静态 `MarshalArmyEligibilityTrace`（可以与 Model 放同目录）：

```text
EnabledForParty(MobileParty):
  仅玩家所在王国、该国正式大元帅或统治者;
  仅决定是否输出日志，不允许影响任何资格分支。

TraceId:
  [kingdomId]:[heroId]:[gameDay]:[monotonicCallIndex]

LogSummary / LogCandidate / LogGate:
  只写 ModLogger.FileDebug；用异常隔离包装日志构造，不包装原生资格逻辑。
```

每次被选中的调用输出 1 条 `ENTRY` 和 1 条 `RESULT`；全部早退写出**唯一主原因码**。候选详细记录限制每次最多 25 支，被省略的计数必须记录，不改变遍历行为。对于连续稳定状态，每次调用仍记录简短 `RESULT`；候选详细日志可以每个游戏日每个 Hero 取样一次，**出现新的失败原因时立刻输出完整明细**。

### 3.2 领队第一层资格（对应第 188–193 行）

严格按照原有短路顺序判定、逐个失败即记录相应原因：

| 阶段码 | 失败码 | 原代码语义 / 应记录数据 |
|---|---|---|
| `LEADER` | `AT_SEA` | `mobileParty.IsCurrentlyAtSea` |
| `LEADER` | `INFLUENCE_LE_100` | `Clan.Influence <= 100f`，输出精确数值 |
| `LEADER` | `MERCENARY_CLAN` | `Clan.IsUnderMercenaryService` |
| `LEADER` | `FOOD_LE_SIEGE_THRESHOLD` | `GetNumDaysForFoodToLast() <= MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege`，记录原始天数和阈值 |
| `LEADER` | `NO_WAR_ENEMY_TOWN` | `kingdom.FactionsAtWarWith.AnyQ(x => x.Fiefs.Any<Town>()) == false`；**这里要求敌方具有 Town，不能偷换为“任何 Fief”** |
| `LEADER` | `LEADER_RATIO_LE_AI_THRESHOLD` | `PartySizeRatio <= AIMobilePartySizeRatioToCallToArmy`，默认是严格 `>0.6` |
| `LEADER` | `NOT_ELIGIBLE_CLAN_LEADER` | 未满足“家族领袖本人，或者家族领袖没有 Party 且本队是家族第一支 WarParty” |

**大元帅身份适配：**既有 Harmony 会替换本层两个 `Clan.Leader` 读取。重写的方法必须保留两个清晰的 `Clan.Leader` 调用点（第 4 节说明 Patch 如何迁移）；不得把所有领主直接视为家族领袖。

**注意：**`MarshalArmyAiBehavior` 当前自己的战争过滤是 `Fiefs.Any()`，而 1.4.7 Model 是 `Fiefs.Any<Town>()`。两者不同。不要修改任一条件，日志应能区分因此发生的后续拒绝。

### 3.3 原版 `GetInfluenceBudgetWhileCreatingArmy` 私有方法兼容问题

源文件第 194 行无条件调用一个 **`private`** 方法：

```csharp
this.GetInfluenceBudgetWhileCreatingArmy(mobileParty);
```

它的定义在第 392–395 行：

```csharp
private float GetInfluenceBudgetWhileCreatingArmy(MobileParty mobileParty)
{
    return mobileParty.LeaderHero.Clan.Influence * 0.7f;
}
```

**重写类无法直接调用父类的 private 方法**。请就地保留等价表达式求值，例如：

```csharp
float unusedInfluenceBudget = mobileParty.LeaderHero.Clan.Influence * 0.7f;
```

原方法未使用返回值，故不能擅自将 `unusedInfluenceBudget` 用作预算检查、候选截断依据或者“新增军团创建条件”。禁止反射调用私有方法作为每小时常态方案。确认现行 DLL 私有方法确与上传文件一致后再实施。

### 3.4 候选逐队筛选（对应第 195–235 行）

按原版本的 `foreach (WarPartyComponent warPartyComponent in mobileParty.MapFaction.WarPartyComponents)` 逐队计算，不能另起第二轮过滤或补充筛选。各条件只在原版原本会访问时访问（必须保留短路语义）。

每队记录：`candidatePartyId`、`leaderHeroId`、`clanId`、`partySizeRatio`、`foodDays`（仅当原版走到食品检查后才取已有局部值）、`inArmy`、`distance`（仅走到距离检查才有）、`influenceCost`（仅原版已算出时）、`estimatedStrength`、`woundedRatio`、`score`。

候选拒绝主原因码建议：

```text
NOT_LORD_PARTY, ALREADY_IN_ARMY, SELF_PARTY, NO_LEADER,
PLAYER_MAIN_PARTY, RULER_PARTY, DO_NOT_DECIDE,
SETTLEMENT_UNDER_SIEGE, IS_DISBANDING, FOOD_LE_15,
PARTY_RATIO_LE_0_6, HERO_CANNOT_LEAD, RAFT_STATE,
MAP_EVENT_ACTIVE, BESIEGED_SETTLEMENT, WAITING_FOR_DISBAND,
DISTANCE_TOO_FAR, DUPLICATE_PARTY
```

数字码中 `15` / `0_6` 仅是便于辨识的显示别名；日志正文必须使用**当前模型实时阈值**，不可硬编码诊断判定。

对于通过过滤的队伍记录 `candidateAccepted=true`、评分原变量 `item = estimatedStrength / (influenceCost+0.1f) * (1-wounded/totalMen)`。

### 3.5 排序、70% 容量和截断（对应第 237–247 行）

按原源码一模一样执行：

```csharp
list = list.OrderByQ(x => x.Item2)
           .ToListQ<ValueTuple<MobileParty, float, int>>();
int partyCount = kingdom.WarPartyComponents.Count;
int armiesPartyCount = kingdom.Armies.SumQ(x => x.Parties.Count);
int maxNewMembers = MathF.Ceiling((float)partyCount * 0.7f
                                   - (float)armiesPartyCount);
if (maxNewMembers > 0)
{
    if (maxNewMembers < list.Count)
        list.RemoveRange(maxNewMembers, list.Count - maxNewMembers);
    ...
}
```

要求日志字段：

`eligibleBeforeSort`、`sortedCandidateIds`（最多 25）及评分、`kingdomWarPartyCount`、`existingArmyPartyCount`、`rawCapacity`、`maxNewMembers`、`afterTruncationCount`、`removedCandidateIds`。

如果 `maxNewMembers <= 0`，日志 `reason=KINGDOM_ARMY_CAPACITY_EXHAUSTED`；如果截断后没有候选，日志 `reason=NO_CANDIDATES_AFTER_TRUNCATION`。**排序保持升序**，即原版本优先留下数值更低的评分项；不得作为本次修改顺便修正。

### 3.6 最终 1000 攻城战斗力校验（对应第 247–269 行）

记录：

- `candidateCountBeforeStrengthCheck` —— 必须在清空前记录。
- `kingdomSettlementsCount` —— 如果为 0，原方法在有候选的情况下直接 `return true`，**跳过战力计算**，日志为 `PASSED_NO_SETTLEMENT_STRENGTH_GATE`。
- `leaderSiegeStrength`、`candidatesSiegeStrength`（从原遍历过程中顺手累计）、`totalSiegeStrength`、`strengthThreshold=1000`，仅在原本需要计算的分支获得。
- 若 `totalSiegeStrength < 1000f`，先记录 `reason=TOTAL_SIEGE_STRENGTH_LT_1000`、`preClearCandidates`，**然后按原实现 `possibleArmyMembers.Clear()` 并 `return false`**。
- 若 `totalSiegeStrength >= 1000f`，记录 `result=true`、最终候选数并原样返回。
- 若从前面任一阶段直接返回 `false`，保持 `possibleArmyMembers` 原有输出列表/空列表状态，日志里同时输出 `preClearCount` 和 `finalCandidateCount`，不得为方便日志清空或补充列表。

## 4. Harmony 身份适配补丁迁移（P0，强制）

现有 `MarshalArmyPatches.cs` 对旧原版声明了 4 个 `CanLordCreateArmy` 补丁：

```text
BeginArmyEligibilityCheck   HarmonyPrefix
EndArmyEligibilityCheck     HarmonyPostfix
ClearArmyEligibilityCheck   HarmonyFinalizer
AllowMarshalToPassLeaderGate HarmonyTranspiler
```

原目标 `typeof(DefaultArmyManagementCalculationModel)`。**派生类的 `override` 是另一个 MethodInfo；旧目标上的 Harmony 不应被假定自动覆盖新方法。**

操作步骤：

1. 核查新方法体中是否仍有可辨认的 **两处** `Clan.Leader` 读取，并确认 IL 调用目标等于现有 `AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.Leader))`。
2. 将上述四个补丁的目标**补充到或迁移到** `DiagnosticArmyManagementCalculationModel.CanLordCreateArmy`。优先为新方法做明确、独立的补丁目标，避免对 Base 与派生方法无差别重复打补丁；如保留原版目标用于其他模型，需用两组独立状态/计数验证每个目标是否命中。
3. 保留现有 `[ThreadStatic]` 入口 Party 注入、`GetEffectiveClanLeader` 的业务规则、Prefix/Finalizer 的清理。两处替换被调用时仍应只对**正式任命的大元帅**返回其本人；统治者和普通领主走旧值。
4. 在启动 `MODEL_PATCH_AUDIT` 中输出新方法的 IL 替换次数，期望为 `2`；同时可用 `Harmony.GetPatchInfo` 验证实际目标 `MethodInfo` 已被 patch。
5. **禁止**直接把家族领袖判定改成 `isMarshal || true`、删除领队身份门槛、在 AI 行为外围强行将 `CanLordCreateArmy=false` 改写为 true，或重新执行旧原版资格方法。
6. 对比既有 `MarshalArmyPatches.ApplyCallCostDiscount` / `ApplyMaintenanceDiscount`：它们仍针对原版 Default 的成本方法。新 Model 透明委托 `BaseModel` 后，须验证原有折扣触发次数及结果与改前一致，避免重复打折或丢失折扣。

**验收重点：**不止看 `LeaderGateReplacementCount=2`，还要检查真正执行时的大元帅 `leaderGatePassed`，因为替换成功不代表领队资格一定通过。

## 5. 日志结构、性能与开关（P0）

使用项目现有 `ModLogger.FileDebug(...)`。如果当前游戏开启了文件日志但文件阈值是 Info，则 **Debug 不会写入**；操作说明必须写明在 ModifiedPolitics 的 MCM 配置中将**文件日志最低等级设为 Debug**，并确认 `EnableFileLogging=true`。

文件位置（现有 Logger）：

```text
Documents\Mount and Blade II Bannerlord\Configs\ModLogs\ModifiedPolitics.log
```

日志统一前缀：`[HeroOffices][ArmyEligibilityModel]`。

**建议字段/事件：**

```text
MODEL_CHAIN
MODEL_PATCH_AUDIT
ENTRY
LEADER_GATE
CANDIDATE_REJECTED
CANDIDATE_ACCEPTED
CANDIDATES_SORTED
CAPACITY_CHECK
STRENGTH_CHECK
RESULT
```

示例（**虚构样例，不是现有游戏事实**）：

```text
[Debug] [HeroOffices][ArmyEligibilityModel] phase=ENTRY traceId=western_empire:CharacterObject_3158:91691:17 role=marshal model=DiagnosticArmyManagementCalculationModel party=CharacterObject_3158_party_1
[Debug] [HeroOffices][ArmyEligibilityModel] phase=LEADER_GATE traceId=... influence=5424 foodDays=18 thresholdSiegeFood=12 partyRatio=0.754 thresholdRatio=0.600 result=PASS
[Debug] [HeroOffices][ArmyEligibilityModel] phase=CANDIDATE_REJECTED traceId=... partyId=... ratio=0.53 limit=0.60 reason=PARTY_RATIO_LE_0_6
[Debug] [HeroOffices][ArmyEligibilityModel] phase=CAPACITY_CHECK traceId=... eligibleBeforeSort=4 kingdomWarPartyCount=15 existingArmyPartyCount=2 maxNewMembers=9 afterTruncationCount=4
[Debug] [HeroOffices][ArmyEligibilityModel] phase=STRENGTH_CHECK traceId=... preClearCandidates=4 leaderStrength=350 candidateStrength=610 totalStrength=960 threshold=1000 result=FAIL
[Debug] [HeroOffices][ArmyEligibilityModel] phase=RESULT traceId=... result=false reason=TOTAL_SIEGE_STRENGTH_LT_1000 candidatesBeforeClear=4 candidatesReturned=0
```

**限流：**仅玩家王国任命的大元帅和王国统治者进入明细；每次决策至少一条结果摘要，重复相同 `reason` 的候选明细可按“每名领队每天最多一次”节流；发生新的失败原因或条件通过时立即输出。禁止为了节流跳过真正的资格计算。

## 6. 可执行文件改动清单

| 文件 | 动作 | 必须完成的具体工作 |
|---|---|---|
| `HeroOffices/Models/DiagnosticArmyManagementCalculationModel.cs` | **新增** | 派生原版 1.4.7 Model；完整等价重写 `CanLordCreateArmy`；其他 8 属性 + 10 方法透明代理，逐分支日志 |
| `HeroOffices/Diagnostics/MarshalArmyEligibilityTrace.cs` | 建议新增 | 玩家王国/大元帅采样、固定日志格式、traceId、明细限流；禁止改变游戏业务 |
| `Main.cs` | **修改** | Campaign 时 `AddModel(new DiagnosticArmyManagementCalculationModel())` |
| `HeroOffices/Patches/MarshalArmyPatches.cs` | **修改** | 四个与 `CanLordCreateArmy` 有关的身份补丁兼容新 override，启动 IL/MethodInfo 验证；其他军团权限逻辑不变 |
| `HeroOffices/Behaviors/MarshalArmyAiBehavior.cs` | 建议小改 | OnSessionLaunched 输出最终 `MODEL_CHAIN`、`MODEL_PATCH_AUDIT`；保持实际 CanLord 调用仅一次，原决策评分不变 |
| `.csproj` | 原则不改 | SDK-style 自动编译新 `.cs`；除非 DLL 引用签名验证确有必要，不增加 NavalDLC 依赖 |
| `code/**`, `ModifiedArmy/**` | **不得修改** | 仅作为参考或回归对比 |

## 7. 验收及回归测试（全部满足才算完成）

### A. 静态与构建验证

- [ ] 使用用户上传 `DefaultArmyManagementCalculationModel(1).cs` 的 **1.4.7 CanLordCreateArmy**，逐逻辑语句核对移植后的方法；不从旧版 `code/DefaultArmyManagementCalculationModel.cs` 复制旧逻辑。
- [ ] 8 个可覆写属性和 10 个非目标方法已经完整转发 `BaseModel`；无 `base.CalculateDailyCohesionChange` 等直接绕过 DLC 的写法。
- [ ] 原始 `OrderByQ` 升序、70% 容量、1000 战力、候选 Clear、各 `>` / `<` 和 `AnyQ` 未改变。
- [ ] 不新增随机数调用，不重复计算成本、距离和攻城战力。
- [ ] `GetInfluenceBudgetWhileCreatingArmy` 私有调用问题已按本次 DLL 的原语义处理，未将未使用的预算作为新门槛。
- [ ] 原身份 Transpiler 在新的 override 方法上确实替换两处 `Clan.Leader`。
- [ ] `ModifiedPolitics` Release 编译 0 error；`ModifiedArmy` Release 编译 0 error；现有架构门禁通过。

### B. 游戏内验证

- [ ] 游戏加载及旧存档正常；无额外 Saveable 数据、无启动崩溃或 Harmony patch 异常。
- [ ] `MODEL_CHAIN` 显示最终有效模型链含 Diagnostic 且经由真实资格调用命中，且无自引用或连续重复的 DLC 包装。
- [ ] `MODEL_PATCH_AUDIT` 指向诊断类的 `CanLordCreateArmy`；`leaderGateReplacements=2`，大元帅的身份判定按既有规则生效。
- [ ] 戈里戈斯的下一次失败日志给出唯一明确 `reason`，不是 `ELIGIBILITY_FAIL_NATIVE_UNKNOWN`。
- [ ] `CANDIDATES_SORTED` 与 `CAPACITY_CHECK`、`STRENGTH_CHECK` 能恢复“筛选后→截断后→清空前→返回时”的候选数。
- [ ] 对符合条件的领主、统治者、普通家族领袖分别验证结果与旧 Mod 相同；当国家没有大元帅时，原版 AI 不受影响。
- [ ] 战帆诺德加成只计算一次，海军相关 `CanPlayerCreateArmy` 禁令正常。
- [ ] 已有大元帅创建权限和军团召集影响力折扣不变（特别检查只应用一次）。
- [ ] Debug 关闭时不大量构建诊断字符串或引入显著 CPU 开销。

### C. 边界测试场景（不改变比较符号）

- 领队影响力刚好 `100`（失败）与 `100.1`（可继续检查）。
- 领队 Party 比例恰好 `0.60`（失败）与 `0.61`（可继续检查）。
- 领队粮食恰好 `NeededFoodsInDaysThresholdForSiege`（失败）。
- 候选 Party 比例恰好阈值（拒绝）、候选粮食恰好 15（拒绝）。
- 对手只剩非 Town 封邑：AI Behavior 前置 `Fiefs.Any()` 可能通过，而 1.4.7 Model `Fiefs.Any<Town>()` 不通过（不改，仅正确诊断）。
- 王国容量 `ceil(0.7 * WarPartyCount - ArmyPartyCount) <= 0`（失败）。
- 存在合格候选但集结战力 `999.9`（清空并失败）、`1000`（通过）。
- 王国 `Settlements.Count == 0` 且候选非空：原版跳过 1000 检查直接通过。
- 候选评分升序、仅保留前 `maxNewMembers` 后成功/失败的准确日志。

### D. 最终交付物

1. 改动文件列表、关键 diff、是否改变任何非日志业务、版本依据。
2. Release 编译和静态/架构测试结果（实际运行的命令与退出码）。
3. 游戏内 `MODEL_CHAIN` 和 `MODEL_PATCH_AUDIT` 的实际输出；没有实测须明确标记 **未验证**，不得虚构。
4. 一段戈里戈斯真实 `ENTRY → LEADER / CANDIDATE → CAPACITY → STRENGTH → RESULT` 日志，以及失败原因总结。无实机条件时交付预期日志格式与游戏内验收步骤。
5. 若只能修改部分代码，明确阻塞点（例如项目 DLL 不一致、Model 赋值链循环或 Harmony 目标不命中），不得声称已完成全部任务。

---

## 8. 可直接复制给 Codex 的执行指令

> 在 `GuihuaZhou/bannerlord` 的 `ModifiedPolitics` 中实施**诊断专用 ArmyManagement Model**。以附件 `DefaultArmyManagementCalculationModel(1).cs`（Bannerlord 1.4.7，第 186–269 行）为唯一方法实现基准：新增 `DiagnosticArmyManagementCalculationModel : DefaultArmyManagementCalculationModel`，完整等价 override `CanLordCreateArmy`，逐分支输出 Debug 原因码、候选逐队排除原因、排序/70% 截断、最终 1000 攻城战力和清空前候选数。**所有判断、顺序、原始候选结果、随机数和游戏行为不得变化。**其余 8 个公开属性与 10 个非目标方法透明委托 `BaseModel`，保护 NavalDLC 的 Model 行为；在 `Main.OnGameStart` 注册并在游戏运行时审计实际模型链。将既有 `MarshalArmyPatches` 针对 `CanLordCreateArmy` 的 Prefix/Postfix/Finalizer/`Clan.Leader` Transpiler 适配到新 override，保持大元帅的两处身份替换和原有军团权限、影响力折扣。使用现有 `ModLogger.FileDebug` 并限制范围/采样，不改其他模块。编译 `ModifiedPolitics`、`ModifiedArmy`，跑架构门禁，提供真实验证结果；未经游戏实测不得声称已验证。

## 9. 源码对应关系（可供 AI 核对）

- 本次附件：`/mnt/data/DefaultArmyManagementCalculationModel(1).cs` — **唯一 1.4.7 可复制方法**。
- 本次附件：`/mnt/data/NavalDLCArmyManagementCalculationModel.cs` — 战帆包装 Model 的委托和凝聚力加成。
- 参考历史上传：`/mnt/data/ModifiedPolitics(3).log` — 西帝国大元帅之前的失败现象，不是改动后验证结果。
- 现有仓库：[Main.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/Main.cs)。
- 现有仓库：[MarshalArmyPatches.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs)。
- 现有仓库：[MarshalArmyAiBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs)。
- 现有仓库：[ModLogger.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/Utils/ModLogger.cs)。

**实施范围总结：新 Model + 新诊断器 + 模型注册 + 身份补丁迁移 + 自检日志；绝不顺手修复军团创建条件。**
