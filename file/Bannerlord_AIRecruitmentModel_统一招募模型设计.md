# AIRecruitmentModel 统一招募模型设计

## 1. 目标

新增统一的 `AIRecruitmentModel`，负责决定 AI Party 面对候选士兵时：

- 是否应该招募；
- 最多可以招募多少；
- 多个候选兵种中应该优先招募哪一种；
- 招募后是否仍能承担招募费用和一段时间的工资；
- 招募后部队的兵种比例和质量比例是否合理。

所有兵源在真正加入 Party 前都调用同一个模型，包括志愿兵、酒馆雇佣兵、采邑兵以及以后新增的特殊兵源。模型只管理 AI 招募，不限制玩家招募。

## 2. 第一阶段范围

第一阶段只处理 Party 剩余空位、兵种比例、士兵质量比例、工资上限、招募费用以及现有资金可以维持工资的天数。

第一阶段明确不处理：

- 通过 `MilitaryPowerModel` 计算士兵战斗力；
- 战时和和平时期使用不同模板；
- 为某个兵种配置绝对人数上限；
- 替代已经存在的采邑部队生成模板。

士兵质量只通过 Tier 判断。

## 3. 代码结构

```text
ModifiedArmy/
└─ Recruitment/
   ├─ Models/
   │  ├─ AIRecruitmentModel.cs
   │  ├─ DefaultAIRecruitmentModel.cs
   │  ├─ ArmyCompositionTemplate.cs
   │  ├─ CultureCompositionTemplateSet.cs
   │  ├─ RecruitmentTemplateRepository.cs
   │  ├─ RecruitmentBudget.cs
   │  ├─ RecruitmentCandidate.cs
   │  ├─ RecruitmentEvaluationResult.cs
   │  └─ RecruitmentPlan.cs
   ├─ Classification/
   │  ├─ CombatRole.cs
   │  ├─ TroopQuality.cs
   │  └─ RecruitmentTroopClassifier.cs
   ├─ Integration/
   │  ├─ VolunteerRecruitmentIntegration.cs
   │  ├─ MercenaryRecruitmentIntegration.cs
   │  └─ FiefRecruitmentIntegration.cs
   └─ RecruitmentModelManager.cs
```

如果 Bannerlord 的 `CampaignModels` 无法增加自定义模型属性，则由 `RecruitmentModelManager.Model` 提供唯一的模型入口。

## 4. 招募来源

```csharp
public enum RecruitmentSource
{
    Volunteer,
    Fief,
    Mercenary,
    Special
}
```

`RecruitmentSource` 只描述兵源，不负责判断士兵兵种或质量。

## 5. Party 模板

第一阶段只提供两种目标部队模板：

```csharp
public enum RecruitmentPartyType
{
    MobileParty,
    Garrison
}
```

- `MobileParty`：领主机动部队；
- `Garrison`：城镇和城堡驻军。

每种文化分别拥有一套 `MobileParty` 模板和一套 `Garrison` 模板。因此实际模板键为：

```text
Culture.StringId + RecruitmentPartyType
```

例如：

```text
khuzait + MobileParty
khuzait + Garrison
nord + MobileParty
nord + Garrison
```

库塞特模板可以提高骑兵和骑射手比例，诺德模板可以提高步兵比例。文化差异仍然只通过兵种比例和质量比例表达，不增加第三个模板维度。

商队、村民、土匪和其他特殊 Party 暂不接入该模型。

采邑部队自身已经拥有生成模板，不在这里重新定义。采邑兵被征召进入领主部队或驻军时，按照目标 Party 的 `MobileParty` 或 `Garrison` 模板评估。

### 5.1 目标文化选择

`MobileParty` 按以下顺序确定模板文化：

```text
party.ActualClan.Culture
-> party.LeaderHero.Culture
-> Default
```

`Garrison` 按以下顺序确定模板文化：

```text
settlement.OwnerClan.Culture
-> settlement.Culture
-> Default
```

这样定居点易主后，驻军会立即采用新领主文化的编制倾向；如果没有有效领主文化，再采用当地文化。

### 5.2 默认模板与自定义文化

必须为 `MobileParty` 和 `Garrison` 各提供一套 `Default` 模板。遇到原版小文化、其他 Mod 新增文化或无法识别的 `Culture.StringId` 时，自动回退到对应 Party 类型的默认模板，不阻止招募。

模板仓库按字符串 ID 注册文化，不把所有文化写死在选择逻辑中：

```csharp
public sealed class RecruitmentTemplateRepository
{
    public ArmyCompositionTemplate GetTemplate(
        string cultureId,
        RecruitmentPartyType partyType);
}
```

以后兼容新文化时，只需注册模板，不需要修改招募模型。

### 5.3 MobileParty 兵种模板

以下数值均为 `MinimumRatio-MaximumRatio`。最低比例用于招募优先级，最高比例是硬限制。

| 文化 | 步兵 | 射手 | 骑兵 | 骑射手 |
|---|---:|---:|---:|---:|
| Default | 25%-55% | 15%-40% | 10%-35% | 0%-20% |
| Empire | 25%-50% | 15%-35% | 20%-40% | 0%-10% |
| Vlandia | 25%-50% | 15%-35% | 20%-45% | 0%-5% |
| Sturgia/Nord | 45%-70% | 15%-35% | 5%-20% | 0%-5% |
| Battania | 30%-55% | 25%-50% | 5%-20% | 0%-10% |
| Aserai | 25%-50% | 15%-35% | 15%-35% | 5%-25% |
| Khuzait | 15%-40% | 5%-25% | 20%-45% | 20%-50% |

设计目的：

- 帝国保持均衡，并拥有较多重骑兵；
- 瓦兰迪亚强调骑兵，同时保留弩手；
- 斯特吉亚或诺德以步兵为核心；
- 巴旦尼亚提高射手比例；
- 阿塞莱保持步兵、骑兵和骑射手混合；
- 库塞特以骑兵和骑射手为核心。

### 5.4 Garrison 兵种模板

驻军以守城需要的步兵和射手为主。骑兵仍可存在，但优先级和最高比例明显低于机动部队。

| 文化 | 步兵 | 射手 | 骑兵 | 骑射手 |
|---|---:|---:|---:|---:|
| Default | 45%-75% | 25%-55% | 0%-15% | 0%-10% |
| Empire | 45%-70% | 25%-50% | 5%-15% | 0%-5% |
| Vlandia | 40%-70% | 30%-55% | 0%-15% | 0%-5% |
| Sturgia/Nord | 55%-80% | 20%-45% | 0%-10% | 0%-5% |
| Battania | 40%-65% | 35%-60% | 0%-10% | 0%-5% |
| Aserai | 45%-70% | 25%-50% | 0%-15% | 5%-15% |
| Khuzait | 35%-60% | 15%-40% | 10%-25% | 15%-35% |

库塞特驻军仍保留文化特色，但不会像机动部队一样让骑兵和骑射手占据大多数名额。

### 5.5 MobileParty 质量模板

所有文化第一版共用以下质量范围：

| 质量 | Tier | 最低比例 | 最高比例 |
|---|---:|---:|---:|
| LowTier | T1-T3 | 15% | 50% |
| MiddleTier | T4-T5 | 30% | 70% |
| TopTier | T6+ | 5% | 30% |

机动部队至少会优先追求 35% 的 T4 以上士兵。低阶兵达到 50% 后，即使 Party 仍有空位，也会等待中高阶兵源或现有士兵升级。

### 5.6 Garrison 质量模板

所有文化第一版共用以下质量范围：

| 质量 | Tier | 最低比例 | 最高比例 |
|---|---:|---:|---:|
| LowTier | T1-T3 | 40% | 50% |
| MiddleTier | T4-T5 | 35% | 55% |
| TopTier | T6+ | 10% | 25% |

驻军仍以低阶士兵作为最大的单一基础群体，但 T4 以上士兵的最低目标合计为 45%。低阶兵达到 50% 后会停止继续招募，因此缺少中高阶兵源时，驻军可能暂时无法填满。T6 保持明显低于 T4-T5，避免最高工资精锐占据过多驻军名额。

## 6. 模板维度

每个文化的每套 Party 模板仍然只包含两个维度。

兵种比例：

```text
Infantry
Ranged
Cavalry
HorseArcher
```

质量比例：

```text
LowTier     = T1-T3
MiddleTier  = T4-T5
TopTier     = T6
```

为了避免特殊单位绕过限制，T0 归入 `LowTier`，T6 以上归入 `TopTier`，Hero 不参与士兵比例统计。

每个分类只定义最低比例和最高比例，不定义绝对人数：

```csharp
public sealed class RatioRange
{
    public float MinimumRatio { get; set; }
    public float MaximumRatio { get; set; }
}
```

## 7. 兵种分类

兵种分类必须互斥，判断顺序如下：

```text
骑射手 -> HorseArcher
其他骑乘单位 -> Cavalry
其他远程单位 -> Ranged
其余士兵 -> Infantry
```

同一名士兵只能计入一种兵种，避免骑射手同时占用射手和骑兵比例。

## 8. 比例转换为人数

所有比例都基于目标 Party 的人数上限，而不是当前人数：

```text
MinimumCount = floor(PartySizeLimit × MinimumRatio)
MaximumCount = floor(PartySizeLimit × MaximumRatio)
```

例如 Party 人数上限为 200，射手比例为 20%-40%，则射手最低人数为 40，最高人数为 80。Party 扩编或缩编时，模板人数会自然变化，不需要保存固定数量。

## 9. 最低比例与最高比例

`MinimumRatio` 是软目标，只影响招募优先级：

```text
Shortage = max(0, MinimumCount - CurrentCount)
```

缺口越大，该类候选兵优先级越高。没有对应兵源时允许暂时低于最低比例，模型不会凭空生成士兵。

`MaximumRatio` 是硬限制：

```text
AllowedByCategory = MaximumCount - CurrentCount
```

达到最高比例后，禁止继续招募该类士兵。同一个候选士兵必须同时满足对应兵种和对应质量的最高比例。

## 10. 模板有效性

每套模板必须满足：

```text
每个 MinimumRatio >= 0
每个 MaximumRatio <= 1
每个 MinimumRatio <= MaximumRatio
同维度所有 MinimumRatio 之和 <= 1
同维度所有 MaximumRatio 之和 >= 1
```

最高比例之和可以大于 100%，因为它们是各分类的独立硬上限，不代表最终目标构成。第一版比例作为代码常量集中放在 `ArmyCompositionTemplate` 中，实测稳定后再考虑开放配置。

## 11. Party 空位

```text
AllowedByPartySize = PartySizeLimit - NumberOfAllMembers
```

伤兵计入 Party 人数，俘虏不计入成员结构。Hero 占用 Party 空位，但不计入兵种和质量比例。如果没有剩余空位，直接返回不可招募。

## 12. 工资上限

当前长期工资必须使用不包含临时封邑兵工资减免的数值，避免减免到期后 Party 立刻超支：

```text
CurrentLongTermWage
= NewPartyWageModel.GetTotalWageWithoutFiefExemption(...)
```

候选士兵实际日薪：

```text
UnitDailyWage
= PartyWageModel.GetCharacterWage(troop)
× NewPartyWageModel.GetWarWageMultiplier(party)
```

第一阶段仍保留已有战争工资倍率，但不根据战争状态切换招募模板。

工资预算保留 10% 空间：

```text
UsableWageLimit = PaymentLimit × 0.90
AvailableDailyWage = UsableWageLimit - CurrentLongTermWage
AllowedByWageLimit = floor(AvailableDailyWage / UnitDailyWage)
```

## 13. 招募费用与维持天数

招募费用和未来工资必须合并判断，不能各自独立通过后再同时消耗资金。

```text
UnitRecruitmentCost
= PartyWageModel.GetTroopRecruitmentCost(...)
```

模型首先检查实际付款人的当前金币是否足以立即支付招募费用。领主 Party 的雇佣兵和志愿兵由 `LeaderHero.Gold` 支付；采邑兵招募费用为 0。即时付款限制通过后，再检查下面的长期维持资金。

可分配资金：

```text
PartyFunds = PartyTradeGold
ClanFundsPerParty = ClanGold / ClanMobilePartyCount
LiquidFunds = PartyFunds + ClanFundsPerParty
EmergencyReserve = LiquidFunds × 0.20
SpendableFunds = LiquidFunds - EmergencyReserve
```

默认安全期为 30 天。批准 `N` 名候选兵后必须满足：

```text
N × UnitRecruitmentCost
+ (CurrentLongTermWage + N × UnitDailyWage) × MaintenanceDays
<= SpendableFunds
```

采邑兵没有招募费用时，`UnitRecruitmentCost` 为 0，但仍然检查长期工资。

## 14. 候选兵优先级

第一阶段不计算本体战斗力，只根据等级和比例缺口排序。

```text
RoleNeed
= RoleShortage / max(1, RoleMinimumCount)

QualityNeed
= QualityShortage / max(1, QualityMinimumCount)
```

建议基础优先级：

```text
Priority
= RoleNeed × RoleWeight
+ QualityNeed × QualityWeight
+ TierPreference
- WagePressure
- RecruitmentCostPressure
```

`TierPreference` 只基于 Tier，用于避免所有候选兵都是低阶兵时仍无条件填满 Party。

当 Party 严重缺员时可以招募低阶兵。当 Party 已达到一定规模且低阶兵达到最高比例时，应保留空位等待更高阶兵源。

## 15. 批次模拟

一次招募过程中必须先收集全部候选兵，再建立模拟状态：

```text
模拟Party人数
模拟兵种人数
模拟质量人数
模拟长期工资
模拟招募支出
模拟可用资金
```

处理流程：

```text
收集全部候选兵
-> 计算优先级
-> 按优先级从高到低排序
-> 评估可招数量
-> 更新模拟状态
-> 继续评估下一候选兵
-> 生成RecruitmentPlan
-> 最后统一执行
```

如果不维护模拟状态，同一次循环中的每个候选兵都会重复使用招募前预算，从而造成超额招募。

## 16. 核心接口

```csharp
public abstract class AIRecruitmentModel
{
    public abstract RecruitmentPlan BuildPlan(
        MobileParty party,
        IReadOnlyList<RecruitmentCandidate> candidates);

    public abstract RecruitmentEvaluationResult EvaluateRecruitment(
        MobileParty party,
        CharacterObject troop,
        int availableCount,
        RecruitmentSource source,
        RecruitmentSimulationState state);
}
```

调用方负责发现兵源和执行结果，模型负责全部数量、比例、质量和经济判断。

## 17. 返回结果

```csharp
public sealed class RecruitmentEvaluationResult
{
    public int RequestedCount { get; set; }
    public int RecruitableCount { get; set; }

    public int AllowedByPartySize { get; set; }
    public int AllowedByCombatRole { get; set; }
    public int AllowedByQuality { get; set; }
    public int AllowedByWageLimit { get; set; }
    public int AllowedByRecruitmentCost { get; set; }
    public int AllowedByMaintenance { get; set; }

    public int RecruitmentCost { get; set; }
    public float UnitDailyWage { get; set; }
    public int SustainableDays { get; set; }
    public float Priority { get; set; }

    public RecruitmentLimitReason PrimaryLimit { get; set; }
}
```

返回完整限制数据，便于通过 Notice 日志验证模型。

## 18. 接入顺序

### 第一批：模型基础

- 分类器；
- 各文化的 MobileParty/Garrison 模板及默认兜底模板；
- 预算快照；
- 模拟状态；
- 评估结果；
- Notice 诊断日志；
- 暂不改变实际招募。

### 第二批：雇佣兵

先接管 AI 领主的酒馆雇佣兵招募，验证昂贵士兵不会造成招募后资金崩溃。

### 第三批：志愿兵

一次收集全部可用志愿兵，按照兵种缺口和质量缺口排序后制定招募计划。

### 第四批：采邑兵

采邑部队继续使用已有生成模板。征召进入 MobileParty 或 Garrison 前，统一经过新模型的目标 Party 模板和经济判断。

### 第五批：驻军

把驻军获得新士兵的相关入口接入 `Garrison` 模板，并验证不会因工资上限立即解散。

## 19. 日志

开发阶段对实际批准或拒绝的招募打印 Notice 日志：

```text
[AIRecruitment]
Party=...
PartyType=MobileParty
Troop=...
Tier=5
Role=Ranged
Quality=MiddleTier
Source=Mercenary
Available=8
Approved=3
PrimaryLimit=Maintenance
RoleCount=20/30-70
QualityCount=25/40-110
RecruitmentCost=750
UnitDailyWage=30
SustainableDays=31
Priority=1.42
```

稳定后可将详细日志降为 Debug，只保留异常经济拒绝和配置错误。

## 20. 最终职责

```text
RecruitmentCampaignBehavior
= 发现志愿兵和雇佣兵，并执行RecruitmentPlan
```

```text
Fief recruitment integration
= 提供可征召的采邑兵，并执行RecruitmentPlan
```

```text
AIRecruitmentModel
= 决定优先招谁、最多招多少，以及招募后是否可持续
```

```text
ArmyCompositionTemplate
= 定义各文化MobileParty和Garrison合理的兵种与质量比例范围
```
