# Bannerlord Mod：Clan 财政状态统一与 AI 税率决策重构
## Codex 施工方案 V2.1（2026-11-15）

> **施工对象**：`GuihuaZhou/bannerlord`，以实际施工时仓库最新 `main` 为准。  
> **涉及模块**：`ModifiedArmy`（暴露既有财富分档）、`ModifiedPolitics`（AI 税率决策、当日民生评估）。  
> **任务类型**：可直接执行的代码修改任务；本文件是设计与验收约束，**不是已完成的代码修改**。  
> **核心要求**：两阈值三财政状态；正常情况下普通税；缺钱时倾向增税；只有真实忠诚度/繁荣度恢复需求才减税；保留保护机制；绝不修改用户的税率 UI。

---

## 0. 已确认的产品规则（高于旧设计文档）

1. **仅使用现有的两个财富阈值判定 Clan 的三个财政状态**：30,000 与 90,000，不再建立基于净收入、现金覆盖天数、储备天数或募兵预算的第二套财政状态判定。
2. `Clan.Leader.Gold <= 30,000`：`Critical`，财政困难；`30,000 < Gold <= 90,000`：`Tight`，财政紧张；`Gold > 90,000`：`Normal`，财政正常。分界关系必须与当前 `MobilePartyWageLimitCalculator` 完全一致。
3. **普通税率是无特殊需求时的默认稳定政策**。财政正常不意味着应该减税。
4. 财政状态只决定**加税方向和强度**：`Tight` 倾向较高税率，`Critical` 倾向极高税率；不是忽略民生的无条件加税命令。
5. **只有封邑真的需要恢复忠诚度和/或繁荣度，才产生降税动机**；已达到 Hearth 支撑繁荣度上限、或经济正常增长，不得单凭低税附赠的正面数值而降税。
6. 财政与民生需求同时存在时权衡；严重叛乱风险优先于增税。
7. 保留每日评估、现有 7 天安全解除保护、紧急降税跨档、其他变更逐档、旧存档兼容；需求消失后逐档回归 `Normal`。
8. **不引入新的“财政阈值”**；允许民生判断使用现有本体动态阈值与少量独立的民生平衡参数。
9. 不修改五档税率效果、不修改用户的税率 UI、不改变金币结算、工资、招募、裁减开支及人口机制。
10. Codex 应先按 `AGENTS.md` 检查实际最新分支代码；本方案中的文件路径和代码片段是已核查的起点，不能覆盖施工时发现的更新实现。

## 1. 当前源码定位（2026-11-15 仓库核查）

| 文件 | 当前事实 | 本次动作 |
| --- | --- | --- |
| `ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/MobilePartyWageLimitCalculator.cs` | `private const int PoorClanGold = 30000;`、`RichClanGold = 90000;`；使用 `clan.Leader.Gold`，三段工资上限曲线 | 抽取公共财富状态和共享阈值，**保留工资算法行为** |
| `ModifiedArmy/ModifiedArmy/Recruitment/Finance/ClanRecruitmentBudgetManager.cs` | 使用日净收支、预期储备和招募承诺预算 | **不作为税率 AI 的财政状态源，不修改** |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementTaxPolicyBehavior.cs` | 旧 `Score()` 无条件奖励低税的繁荣度与忠诚度；`OnDailyTick` 持有存档、保护、执行 | 替换候选评分/目标选择；原存档和行为入口保持 |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/OfficeSettlementModels.cs` | `OfficeSettlementLoyaltyModel` 在 `CalculateLoyaltyChange` 后加入税率忠诚度增量 | 提取对指定候选税率的**只读当日忠诚度试算**，实际结果共用公式 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs` | 在本体日繁荣度结果上加税率效果，并按 Hearth 支撑量约束实际变化 | 提取指定税率的**只读当日繁荣度试算**，与真实结算共用上限算法 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs` | XML 解析 `safeDays`、`switchThreshold`、4 个权重 | 仅必要时增加民生需求参数与校验，旧属性兼容 |
| `ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml` | `<Ai safeDays="7" switchThreshold="0.08" fiscalWeight="1.20" economyWeight="0.75" recruitmentWeight="0.65" rebellionWeight="1.50" />` | 保留已有参数，不让旧权重继续造成无条件减税；解释停用/重用的语义 |
| `ModifiedPolitics/ModifiedPolitics/ModifiedPolitics.csproj` | 已通过 `ProjectReference` 依赖 `ModifiedArmy.csproj` | **不需要**新增反向依赖或 DLL 引用 |
| 仓库根 `AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md` | 要求业务领域归属、唯一权威状态源、测试和兼容、禁止无授权推送 | Codex 执行前必须阅读并遵守 |

### 1.1 当前五档税率效果：不可改

| 枚举 | 税收倍率 | 每日忠诚度影响 | 每日繁荣度影响 |
| --- | ---: | ---: | ---: |
| `VeryLow` | `0.50` | `+1.0` | `+1.0` |
| `Low` | `0.75` | `+0.5` | `+0.5` |
| `Normal` | `1.00` | `0` | `0` |
| `High` | `1.25` | `-1.5` | `-1.5` |
| `VeryHigh` | `1.50` | `-3.0` | `-3.0` |

以上由 `SettlementTaxPolicyBehavior.Effects()` 定义。**禁止**调五档数值来掩盖 AI 问题。

---

## 2. ModifiedArmy：提取唯一、只读的 Clan 财政状态

### 2.1 设计约束

当前工资模型通过 `clan.Leader.Gold` 与两条阈值区分财富段，但没有可被其他模块调用的正式状态。新增一个**公共三态计算接口**，把这两条阈值作为唯一权威定义。

建议新增同领域文件（具体目录遵循现有组织规则）：

`ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/ClanFiscalStateModel.cs`

建议接口如下（实际名称可按现有命名规范微调）：

```csharp
namespace ModifiedArmy.PartyManagement.Finance.Models
{
    public enum ClanFiscalState
    {
        Critical,  // <= 30,000
        Tight,     // >30,000 && <=90,000
        Normal     // >90,000
    }

    public static class ClanFiscalStateModel
    {
        public const int PoorClanGold = 30000;
        public const int RichClanGold = 90000;

        // 没有可用的领袖时返回 false，不能凭空将其判为 Critical。
        public static bool TryGetState(Clan clan, out ClanFiscalState state)
        {
            state = ClanFiscalState.Normal;
            if (clan?.Leader == null) return false;
            int gold = Math.Max(0, clan.Leader.Gold);
            state = gold <= PoorClanGold ? ClanFiscalState.Critical
                : gold <= RichClanGold ? ClanFiscalState.Tight
                : ClanFiscalState.Normal;
            return true;
        }
    }
}
```

- 示例中的 `state = Normal` **只是失败时的 out 占位值**，调用方必须首先检查 `bool`，不可把失败视为财政正常。
- 不增加 `Unknown` 第四枚举值；无可用领袖时，税率 AI 应跳过当次决策并记录受控诊断。
- 不持久化、不缓存 `ClanFiscalState`。它是已有 `clan.Leader.Gold` 的只读派生值；日更税率行为每次读取一次即可。
- **不要使用 `clan.Gold` 替换当前 `clan.Leader.Gold` 语义**，除非先发现它们在实际代码中的等价契约，并通过测试证明不改变工资行为。当前权威口径是领袖金币。
- 不将 `ClanRecruitmentBudgetSnapshot.RemainingCommitmentBudget` 用于判定财政状态：它是当日会被招募消耗的运行余额，不代表财富段。

### 2.2 工资模型的最小接入

从 `MobilePartyWageLimitCalculator.cs` 去掉自身重复的两条 `private const`，改为引用 `ClanFiscalStateModel.PoorClanGold / RichClanGold`。如有必要可在现有计算中读取 `TryGetState()` 决定三个分支，但不能改动以下行为：

1. `Gold > 90,000` 使用 `PartyWageModel.MaxWagePaymentLimit`；
2. `30,000 < Gold <= 90,000` 原有线性公式；
3. `Gold <= 30,000` 原有平方曲线；
4. 家族领袖、战时工资、最高上限等现有后处理原样保留；
5. 空参数和异常边界原本返回上限的逻辑保持一致。

**重要**：`code/ClanVariablesCampaignBehavior.cs`（本体源码）的小派系曾使用 `10,000 / 30,000` 特例，但当前 Mod 工资模型固定使用 `30,000 / 90,000`。本次按照**当前 Mod**执行；不要顺手恢复小派系特例。

### 2.3 边界验收

| `clan.Leader.Gold` | 状态 |
| ---: | --- |
| 0 / 29,999 / **30,000** | `Critical` |
| **30,001** / 70,000 / **90,000** | `Tight` |
| **90,001** / 150,000 | `Normal` |

新增公共接口后，对相同 clan、hero、战争状态，原工资上限应**逐项等于**改动前算法结果；既有消费、招募、裁军不受影响。

---

## 3. ModifiedPolitics：民生恢复需求的判断

**本次只新增忠诚度与繁荣度两类民生需求**，不把 `garrisonNeed`、`populationRatio`、`loyaltyEfficiency` 等旧征兵因子继续列为独立降税理由。财政来自第 2 章公共接口。

建议纯计算输出：

```csharp
internal enum CivilRecoveryNeed { None, Moderate, Severe }

internal sealed class TaxPolicyDecisionSnapshot
{
    public ClanFiscalState FiscalState { get; init; }
    public CivilRecoveryNeed CivilNeed { get; init; }
    public bool RebellionDanger { get; init; }
    public float CurrentLoyalty { get; init; }
    public float NormalLoyaltyDailyChange { get; init; }
    public float CurrentProsperity { get; init; }
    public float SupportedProsperity { get; init; }
    public float NormalProsperityDailyChange { get; init; }
    public float LowProsperityDailyChange { get; init; }
    public float VeryLowProsperityDailyChange { get; init; }
    public string CivilReasonCode { get; init; }
}
```

示例为语义约定，不要求为这些对象增加 `Saveable` 类型。**用现有 C# 语言版本支持的可编译语法**；`init` 等用法以项目实际环境为准。可拆为两个独立民生强度，再使用 `max()` 合并，不必创建第二套存档。

### 3.1 忠诚度：只在“正常税率下确需恢复”时降税

参考来源：
- `Campaign.Current.Models.SettlementLoyaltyModel.RebelliousStateStartLoyaltyThreshold`，记为 `r`；
- `LoyaltyDriftMedium`，记为 `m`；
- `settlement.InRebelliousState`；
- `OfficeSettlementLoyaltyModel.CalculateLoyaltyChange()`。

**禁止**写死本体忠诚度 25/50，优先使用当前运行模型实际阈值。对于 `m <= r` 的异常配置保守处理并记录一次受控诊断。

必须**以 Normal 税率计算当日忠诚度变化**，不能使用已经开启 Low/VeryLow 的当前变化作为恢复能力（否则出现“减税已生效 → AI 误认为不需减税 → 反复切换”）。

建议直接在现有 `OfficeSettlementLoyaltyModel` 提取共用只读方法，例如：

```csharp
public ExplainedNumber CalculateLoyaltyChangeForTaxLevel(
    Town town, SettlementTaxLevel candidate, bool includeDescriptions = false)
```

实际 `CalculateLoyaltyChange()` 仍使用注册表中的当前税率，并复用同一套方法。候选方法不能改注册表、不能调用 `TrySetLevel()`、不能写入游戏状态。**优先共用完整计算路径**；不要简单把“当前日变化减去当前税率效果”当作精确结果，除非确认所有 ExplainedNumber 修正的先后顺序和系数无影响。

建议第一版按下述确定性条件实现（优先沿用现有 `danger`）：

- `RebellionDanger=true`：满足 `settlement.InRebelliousState`，或者 `m>r` 且 `loyalty <= r + 0.25*(m-r)`；属于**紧急危机**。
- `Severe`（非紧急）：`loyalty < r + 0.50*(m-r)`，且 Normal 下每日忠诚度恢复不足（建议 `< +0.5`）；体现明显偏低且恢复较慢。
- `Moderate`：`loyalty < m` 且 Normal 下每日变化 `< +0.25`；或已经低于正常中心且在 Normal 下继续下降。
- `None`：正常中心附近，或者低忠诚度但在 Normal 下已能充足恢复、无需额外补贴。

**先判断 `RebellionDanger`，再按 Severe / Moderate / None。** 以上 `0.25 / 0.50` 的阈值跨度复用动态 `r,m`；`+0.25/+0.5` 是**建议初版民生恢复速度边界**，须作为可检验的常量或 XML 民生参数，而不是隐形魔法数字。严重到接近叛乱的封邑，即使当前日增长为正，也必须保留紧急保护。

### 3.2 繁荣度：只在低税能产生实际边际恢复时降税

当前模型：`SettlementEconomyProsperityModel.CalculateProsperityChange(Town)`。其重要约束：

```text
supported = GetHearth(settlement) *
    (IsCastle ? CastleProsperityPerHearth : TownProsperityPerHearth)

original = BaseProsperityDailyChange + CandidateTaxProsperityEffect

if currentProsperity < supported:
    constrained = min(original, supported - currentProsperity)
else:
    constrained = min(min(original, 0),
                      -(currentProsperity - supported) / ExcessProsperityDecayDays)
```

**必须复用原实现的约束**，不能只看 `effects.Prosperity`；已达/超过支撑上限时，不得因理论上的 `+0.5/+1` 仍给予低税奖励。

建议提取共用的、指定候选税率的只读方法：

```csharp
public ExplainedNumber CalculateProsperityChangeForTaxLevel(
    Town town, SettlementTaxLevel candidate, bool includeDescriptions = false)
```

实际 override 调用同一辅助方法并传入当前税率。辅助方法调用 `base.CalculateProsperityChange` 后加候选效果，最后应用**完全相同**的 Hearth 容量约束；不得调用自身 override 引起递归；不得临时更改税率。

一次封邑评估只计算 `Normal`、`Low`、`VeryLow` 候选的实际日变化。推荐初版需求判断：

- 若 `LowChange - NormalChange <= 0.10` 且 `VeryLowChange - NormalChange <= 0.10`：`None`，因为减税没有可观的实际边际效果。
- 正常税率下已在稳定增长（推荐 `NormalChange > +0.25`），且无严重民生危机：`None`；只因距 Hearth 上限尚有空间不应减税。
- `Moderate`：正常税率下持续衰退（建议 `NormalChange < -0.25`），或在距支撑上限明显不足时基本停滞（推荐容量缺口至少 10%，且 `NormalChange <= +0.10`），同时 `Low` 确实改善实际日变化。
- `Severe`：正常税率下明显衰退（建议 `NormalChange <= -1.5`），且 `VeryLow` 能明显改善；严重程度以真实增益为前提。
- `ProsperitySupported <= 0`、人口服务未准备好时：谨慎返回 `None`，并限制诊断；不能伪造任何低税收益。

这些日变化数值均为**待验证的初版民生平衡参数**。允许放在 `TaxPolicy/Ai` 中配置；**不得**改动财政 30k/90k 阈值。

### 3.3 合并民生强度

```text
CivilNeed = max(LoyaltyNeed, ProsperityNeed)
RebellionDanger = 本体实际叛乱状态 / 已有危险判断
```

保留忠诚度和繁荣度两项各自的原因码，以便日志区分为何降税。不能因 `CivilNeed` 取 max 就丢失第二个需求的说明。

---

## 4. 五档税率：采用“财政状态 × 民生需求”目标政策表

### 4.1 基础目标表（确定性、易测试）

**这是本次 Codex 第一版的明确默认决策表**，不再保留旧 `Score()` 五档无条件奖励模型。

| 财政状态 \ 民生需求 | None（无需恢复） | Moderate（一般恢复需求） | Severe（严重恢复需求、未触发叛乱危险） |
| --- | --- | --- | --- |
| `Normal`（>90k） | **Normal** | **Low** | **VeryLow** |
| `Tight`（>30k 至90k） | **High** | **Normal** | **Low** |
| `Critical`（<=30k） | **VeryHigh** | **High** | **Normal** |

**紧急覆盖：** `RebellionDanger=true` 时，目标为 `VeryLow`，无论 Clan 财政处于何档。叛乱风险优先级高于筹款。

规则解释：

- 经济、忠诚度都健康，财政也健康 → **Normal**。
- Clan 财政紧张而封邑健康 → **High**；财政危机而封邑健康 → **VeryHigh**。
- 民生需求与财政压力相互抵消时，不机械优先其中一方：`Tight+Moderate → Normal`，`Critical+Severe → Normal`。
- 并非所有恢复需求都要 VeryLow；只有财政正常而确实严重需要恢复，或者已经达到紧急叛乱风险，才优先 VeryLow。
- 这张表是**政策目标**，不代表一日内直接跳到目标档位；执行仍受第 5 章迟滞约束。
- 不增加按当前金币净增长率改变上述财政档位的隐藏判定。

### 4.2 极低/极高税率退出机制

每次都**重新计算当前需求**，不要因历史税率保留低税特权：

- 当前 `VeryLow`，目标 `Normal` → `Low` → `Normal`，只要没有保护阻止加税。
- 当前 `VeryHigh`，目标 `Normal` → `High` → `Normal`。
- 处于 `Tight` 与 `Critical` 的 Clan，恢复到 `Normal` 财富段且无民生需求后，目标为 `Normal`。
- 旧存档中大量持久化 `VeryLow` 不需要手工重置，不需要迁移存档文件。

---

## 5. OnDailyTick 执行算法与现有迟滞机制

### 5.1 保持既有职责

`SettlementTaxPolicyBehavior` 继续独占：

- `DailyTickSettlementEvent` 的税率评估订阅；
- `_levels` 的写入与读取；
- `_protection` / `_safeDays` / `_lastEvaluation`；
- `TrySetLevel` 政策实际变更；
- `OnOwnerChanged` 清理重置。

**保持现有 `SyncData` 的四个键及 `SettlementTaxLevel` 的枚举顺序**：

```text
ModifiedPolitics.SettlementTax.Levels.v1
ModifiedPolitics.SettlementTax.Protection.v1
ModifiedPolitics.SettlementTax.SafeDays.v1
ModifiedPolitics.SettlementTax.LastEvaluation.v1
```

不要新增无必要的持久化状态。旧 `Score()` 的 `income/expenses/deficit/lossRatio/runwayPressure/localTaxShare/garrisonNeed/recruitment` 路径从税率决策中移除，但**不改变**别的模块用到的财政或征兵计算。

### 5.2 建议决策伪代码

```text
OnDailyTick(settlement):
    if 非有效 AI 城镇/城堡、玩家拥有、当前日已评估: return
    更新现有 RebellionDanger、Protection、SafeDays
    if !ClanFiscalStateModel.TryGetState(ownerClan, out fiscal):
        记录受控诊断并 return
    civilSnapshot = CalculateCivilNeedsAtNormalTax(settlement)
    desired = ResolveTarget(fiscal, civilSnapshot.CivilNeed,
                            civilSnapshot.RebellionDanger)
    current = GetLevel(settlement)
    if desired == current: 记录受控维持原因并 return

    if desired > current AND Protection:
        记录 RebellionProtection 阻止加税并 return

    if desired < current AND RebellionDanger:
        next = desired  // 唯一允许的紧急跨档降税
    else if desired < current:
        next = current - 1
    else:
        next = current + 1

    应用原 switchThreshold 的非紧急决策差距门槛（见 5.3）
    if 不满足门槛: 记录不足原因并 return
    TrySetLevel(settlement, next, playerAction:false)
    输出一次结构化变更日志
```

需要保持 `_lastEvaluation` 的“一座封邑每日最多决策一次”语义。**先保存当前日评估标记，还是在依赖缺失后重新尝试**，以原行为和加载生命周期契约为准，不能引入重复决策。

### 5.3 旧 `switchThreshold=0.08` 的保留方式

原实现的 `switchThreshold` 是旧连续 `Score` 的分差门槛。本版改成离散目标政策表后，它**不能再直接套用旧公式**，否则极低税率正常化可能永远卡住。

建议提供一个轻量、可审计的目标一致性效用（**只作为迟滞门槛，不再是一套财政/民生评分模型**）：

```text
PolicyUtility(level, desired) = -0.25 * abs(levelIndex - desiredIndex)
nextStepAdvantage = PolicyUtility(next, desired)
                    - PolicyUtility(current, desired)
非紧急变更要求：nextStepAdvantage >= switchThreshold
```

- 默认步进改善固定为 `0.25`，现有 `0.08` 门槛允许逐步修正；达到 Normal 的正常化也会通过。
- 如采用其他更符合现有项目规范的简洁做法，也必须**明确调整 `switchThreshold` 的语义并更新注释/诊断**，不准保留“已使用”但实际死代码的 XML 参数。
- `RebellionDanger` 下的紧急降税跳过上述门槛，保持原有契约。
- 额外风险：30k/90k 的财富分类本身可能因金币日间波动而来回穿越。**不要为此引入第三个财政阈值**；先用每天最多一步、保护和实际运行日志观察，若边界横跳严重，另行提出明确的防抖方案，不得暗改两阈值规则。

### 5.4 保护状态

严格保持原语义：

- 处于 `RebellionDanger`，设 `protection=true` 且 `safeDays=0`；
- 脱离危险后，只有 `loyalty >= m` 才累计安全日数，否则清零；
- 连续 `TaxProtectionSafeDays`（当前 XML 为 7）才解除保护；
- 保护期间任何 `next > current` 都禁止，**包括低税向 Normal 的正常化**；
- 保护不阻止下降税率。

---

## 6. 配置修改原则

当前 XML：

```xml
<TaxPolicy>
  <Ai safeDays="7" switchThreshold="0.08"
      fiscalWeight="1.20" economyWeight="0.75"
      recruitmentWeight="0.65" rebellionWeight="1.50" />
</TaxPolicy>
```

- `safeDays=7`：继续使用；`switchThreshold=0.08`：按第 5.3 节保留明确门槛功能。
- `fiscalWeight / economyWeight / recruitmentWeight / rebellionWeight` 属于将被替换的旧 `Score()`。施工时检查配置是否有其他实际消费者；若仅为旧 `Score` 服务，**不要悄悄继续参与新算法**。为兼容已部署 XML，可暂保留解析但注明 Legacy / Unused，并另开清理任务；如要正式删除须同步处理所有配置与校验、确认老版本资源不受影响。
- **两条财富阈值仍是 `ModifiedArmy` 的 30,000 / 90,000 常量**，本次禁止把它们重新复制到 `ModifiedPolitics` XML。
- 为调整忠诚度/繁荣度需恢复的门槛，可以在 `Ai` 节点新增少量民生专用参数。建议候选：`loyaltySlowRecovery`（0.25）、`loyaltySevereRecovery`（0.50）、`prosperityMinTaxBenefit`（0.10）、`prosperityHealthyGrowth`（0.25）、`prosperityDecline`（-0.25）、`prosperitySevereDecline`（-1.5）、`prosperityCapacityGapRatio`（0.10）。**负数参数必须使用可解析负数的校验函数**，不得错误调用 `NonNegative`。
- 若少量参数足以使用已有动态本体阈值，不要求机械添加全部候选参数；但不能把未注释的数值撒在业务代码各处。
- 所有配置新增、默认值、范围验证和本地化/日志说明应一致，不为本任务引入新的独立配置文件。

---

## 7. 文件与模块职责边界

### 必须/预计修改

1. **ModifiedArmy**：
   - `.../PartyManagement/Finance/Models/ClanFiscalStateModel.cs`（新增）：三态公共判断及唯一财富阈值。
   - `.../PartyManagement/Finance/Models/MobilePartyWageLimitCalculator.cs`：复用同一组常量、保持原有工资算法。
2. **ModifiedPolitics**：
   - `.../SettlementEconomy/Behaviors/SettlementTaxPolicyBehavior.cs`：每日执行规则、目标政策表、旧档回归、保护、诊断；移除旧需求打分。
   - `.../HeroOffices/Models/OfficeSettlementModels.cs`：忠诚度候选只读计算及正式计算的共用实现；不能改税收计算代码。
   - `.../SettlementEconomy/Models/SettlementEconomyProsperityModel.cs`：繁荣度候选只读计算及正式计算共用 Hearth 上限约束。
   - `.../SettlementEconomy/Config/SettlementEconomyConfig.cs`、`.../ModuleData/settlement_economy.xml`：根据需要加民生平衡参数、注明旧权重语义。
3. 视代码实际复杂度，可在 `SettlementEconomy/Models` 下添加一个**纯计算**的 `SettlementTaxDecisionModel.cs`，将决策表和民生强度判断从 Behavior 剥离；不要让新模型持有第二套税率持久化字典。

### 明确禁止修改

- 用户已经调整的税率 UI（TownManagement、各级 ViewModel、XML prefab 等）。
- `SettlementTaxPolicyBehavior.Effects()` 五档数值。
- `OfficeSettlementTaxModel.CalculateTownTax()` 的实际税收计算与封邑兵减免算法。
- `ClanRecruitmentBudgetManager` 的日招募预算算法及现有缓存语义。
- `GarrisonWageLimitModel`、领主工资/战时倍率、裁军/换兵、驻军补员、Hearth 生产与消费。
- SaveableTypeDefiner ID、现有 SyncData key、已注册游戏模型、Harmony 目标、玩家税率写入权限。
- `ModifiedArmy` 不得添加对 `ModifiedPolitics` 的反向项目引用。

---

## 8. 可复现诊断与日志要求

上一次日志 `ModifiedPolitics(9).log` 中没有 `[TaxPolicy]` 变更记录；这只能说明观察区间内没有实际变更，**不能证明 AI 没有评估**。新日志需要能回答“为什么维持极低税率”。

**实际变更**（保留英文基础日志、结构化字段）：

```text
[TaxPolicy] changed settlement=<display name>, settlementId=<id>,
ownerClan=<display name>, day=<day>, gold=<gold>, fiscalState=<state>,
loyalty=<number>, loyaltyNormalDaily=<number>,
prosperity=<number>, prosperitySupported=<number>,
prosperityNormalDaily=<number>, prosperityLowDaily=<number>,
civilNeed=<level>, civilReason=<codes>, danger=<bool>,
protection=<bool>, from=<level>, target=<level>, to=<level>,
reason=<FiscalNeed|CivilRecovery|RebellionEmergency|ReturnToNormal|ConflictCompromise>
```

**未变更**：仅 `Debug` 级别且限玩家王国、特定封邑或采样状态改变时；至少区分：

- `AlreadyAtTarget`
- `RebellionProtection`
- `SwitchThresholdNotMet`
- `UnavailableFiscalState`
- `CivilRecoveryStillNeeded`
- `NoSpecialNeedReturnToNormal`

不允许每日打印全地图所有封邑的五档无关评分；不允许日志函数再次计算财政或写入任何状态；不允许破坏现有中文显示名称规则。

---

## 9. 验收用例（必须具备可复核的 PASS/FAIL/BLOCKED 结果）

### 9.1 三财富状态

- `Gold == 30,000` 必须是 `Critical`；`30,001` 为 `Tight`。
- `Gold == 90,000` 必须是 `Tight`；`90,001` 为 `Normal`。
- null clan / null leader 不引发异常，不默认为有效正常财政状态。
- 工资上限修改前后，在五个边界数值及普通金币下完全一致。

### 9.2 政策目标表（必须做纯计算表驱动验证）

| 财政 | 民生 | 紧急危险 | 目标税率 |
| --- | --- | --- | --- |
| Normal | None | 否 | Normal |
| Normal | Moderate | 否 | Low |
| Normal | Severe | 否 | VeryLow |
| Tight | None | 否 | High |
| Tight | Moderate | 否 | Normal |
| Tight | Severe | 否 | Low |
| Critical | None | 否 | VeryHigh |
| Critical | Moderate | 否 | High |
| Critical | Severe | 否 | Normal |
| 三态任一 | 任意 | **是** | **VeryLow** |

### 9.3 真实民生条件

- 富裕 Clan，忠诚度正常、繁荣度稳定增长：目标 Normal，不得 VeryLow。
- 富裕 Clan，忠诚度较低但在 Normal 下能够充足恢复：不因当前低税继续加 +0.5 而长期 VeryLow。
- 富裕 Clan，忠诚度正常但 Normal 下持续繁荣度衰退，Low 明显有效：目标 Low。
- 已达 Hearth 支撑上限，Low/VeryLow 没有实际额外增长：不因此降税。
- 严重忠诚度危机：目标 VeryLow，哪怕 Clan 金币 <=30k。
- 正常税率当日变化与 `OfficeSettlementLoyaltyModel`、`SettlementEconomyProsperityModel` 共用逻辑一致；同场景候选计算不得改变金币、忠诚、繁荣度、税率或 save 数据。

### 9.4 迟滞与存档

- High/VeryHigh 返回 Normal：非紧急每次只调整一档。
- VeryLow/Low 返回 Normal：在非保护状态逐档执行，不能因旧分数差异卡死。
- 危险时 High / VeryHigh 可直接降至 VeryLow。
- 保护期间 VeryLow → Low、Low → Normal 等任何税率提高都被阻止，安全连续7天后才能恢复。
- 同一个 settlement 同一 campaign day 不重复生效；保存/重读后仍只执行一次。
- 所有 `_levels/_protection/_safeDays/_lastEvaluation` 原存档正常读取；玩家封邑不被 AI 自动改税率；封邑易主行为保持。
- 检查 30k 和 90k 附近金币波动，记录是否出现税率日间反复；若出现，不允许自行加第三条财政门槛。

### 9.5 长期行为（游戏内验收）

在带有大量 VeryLow 的既有存档中，观察不少于 30 个游戏日，最好覆盖 60/120 天：

- 财政 `Normal` 且民生健康的封邑逐步回 Normal。
- `Tight/Critical` 且民生健康的封邑逐步转向 High/VeryHigh。
- 低忠诚度或繁荣度真实衰退的封邑合理减税，民生改善后能恢复目标政策。
- 不出现“全部极低”或“全部极高”的统一偏差。
- 不出现因单日计算失误导致的高频切换。

不可硬编码一个全图税率占比目标；真实地图财富与民生状态不同，应允许税率分布不同。

---

## 10. Codex 执行顺序与提交契约

**第一阶段：预检**

1. 进入仓库根目录，读取根 `AGENTS.md`、若存在的目录级 `AGENTS.md`，以及 `ModifiedArmy/CODE_ORGANIZATION_RULES.md`。
2. `git branch --show-current`、`git rev-parse HEAD`、`git status --short`。不要覆盖现有未提交改动。
3. 核对本方案中源码入口是否仍在最新版本；确认 `ModifiedPolitics -> ModifiedArmy` 引用和 `SubModule.xml` 依赖方向。
4. 列出将修改的文件及不可触碰边界。

**第二阶段：财务唯一状态源**

5. 新增 `ClanFiscalStateModel`，从工资模型复用同一组 30k/90k 常量，验证五个边界与工资算法逐项无回归。
6. 不修改 `ClanRecruitmentBudgetManager` 的额度生成、日消费或任何招募业务。

**第三阶段：只读民生模型**

7. 在现有忠诚度模型提取指定税率的候选日变化计算，共用正式计算路径。
8. 在现有繁荣度模型提取指定税率的候选日变化计算，共用 Hearth 约束与日志说明。
9. 验证候选调用没有注册表写入，也没有副作用，且 `Normal` 候选不被当前低税状态污染。

**第四阶段：税率决策与迟滞**

10. 在 `SettlementEconomy` 域使用纯计算模型实现第 4 章政策目标表。
11. Behavior 每天读取一次财政状态及民生快照，按第 5 章执行逐档调整和叛乱危机跨档。
12. 清理旧 `Score()` 的财政与征兵重复打分，不涉及正式税收计算；处理原 XML 参数兼容性。
13. 添加有限诊断，能从日志识别“为何不从 VeryLow 回 Normal”。

**第五阶段：测试与交付**

14. 编译 `ModifiedArmy`、`ModifiedPolitics`，并执行现有架构检查及可用静态测试。缺游戏程序集/Windows 环境则准确标记 `BLOCKED`，不得谎称完成。
15. 对照第 9 章做纯计算验收和运行时验收；没有游戏存档无法覆盖的项目标为 `BLOCKED`。
16. 检查 `git diff`，确认无税率 UI / 实际税率数值 / 财政预算 / 工资 / 人口 / 其他系统改动。
17. 输出：变更文件清单、设计与边界对应、关键测试的 PASS/FAIL/BLOCKED、旧档兼容风险、最终 HEAD SHA。
18. **除非用户明确另行要求，不要自动 commit、push、创建 PR、合并分支。**

## 11. Codex 可直接使用的执行指令

以下是这份方案的浓缩执行命令，可与本 Markdown 文件一起交给 Codex：

> 请阅读仓库根 `AGENTS.md`、适用的模块组织规则，以及《Bannerlord Mod：Clan 财政状态统一与 AI 税率决策重构—Codex 施工方案 V2.1》，以当前 `main` 实际代码为权威，直接按第 10 章分阶段施工。财政状态仅使用 `MobilePartyWageLimitCalculator` 既有的 30,000 / 90,000 两条金币阈值，提取公共三态接口，保持工资算法完全不变。ModifiedPolitics 仅新增忠诚度和繁荣度的实际恢复需求，以 Normal 为默认税率，按第 4 章目标表权衡加税和减税，叛乱危险优先，保留第 5 章迟滞及既有存档键。候选模型必须只读、共用真实忠诚度和繁荣度结算路径，不得临时更改政策。禁止改用户税率 UI、税收结算、五档税率效果、招募预算、工资、驻军、人口和其他功能。按第 9 章输出可审计的验收证据；失败或受环境限制必须明确标为 FAIL/BLOCKED。不要未经授权提交、推送或创建 PR。

---

**验收总原则：** Clan 30k/90k 财富分档是唯一财政状态源；普通税率是默认状态；降税只为真实民生恢复；加税只为现有财政紧张/危机；叛乱风险优先；旧存档不重置、不横跳；实际结算和用户 UI 不受影响。
