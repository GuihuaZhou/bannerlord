# ModifiedPolitics：战争倾向数值调整实施方案

> **用途**：交给 Codex / 其他代码 AI 直接执行的修改说明。  
> **状态**：设计已确定，**本文档不是代码已修改的证明**。  
> **修改原则**：只修改已确认的事件基础影响值和每日战争消耗常量；不改变任何算法、触发条件、事件叠加、分发、阈值或存档结构。  
> **源码依据**：`GuihuaZhou/bannerlord` 仓库中 `ModifiedPolitics` 的当前实现，参见文末文件路径。

## 1. 任务与边界（AI 必须遵守）

任务：降低战争倾向数值变化幅度，减少 AI 因常规战斗、封地事件及长期战争消耗而快速进入负面战争倾向区间的概率。

**仅允许修改两个文件中的指定数字字面量：**

1. `ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Events/WarDispositionEventCatalog.cs`
2. `ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Calculation/WarDispositionDailyReturnCalculator.cs`

**禁止修改**：

- 战斗、英雄、定居点事件的监听器，以及触发条件、事件类型、去重逻辑。
- `WarDispositionEventDistributor` 的传播规则：事件所属 Clan 100%，同王国其他合格 Clan 20%。包括获封事件，不作例外。
- 战败、部队覆灭、英雄被俘等事件的累加方式；不增加单次战斗上限或防叠加机制。
- 人格特质（Trait）权重、人格修正范围、计算函数及最终战争倾向 `[-100, 100]` 截断。
- 和平恢复系数、长期基准、战争判断条件、每日 Tick 时机。
- `ModifiedArmy` 的 `-50` 阈值、兵力目标公式、征兵、解散、休整等任何逻辑。
- `ClanMemberKilled`、`ClanMemberExecuted`、`WeeklyWealthChange` 等未获批准修改的事件。
- 配置文件、日志格式、存档字段、公共 API、体系结构及无关重构。

**实现方式**：定点替换数值。不要基于主观设计另加修复，不要修改源文件中事件数值之外的 Trait 参数。若当前仓库与本表不一致，先报告差异，不要盲目搜索替换。

## 2. 战斗事件：修改基础影响值

**文件**：`Models/Events/WarDispositionEventCatalog.cs`（以上述 `ModifiedPolitics/ModifiedPolitics/WarDisposition/` 为根目录）

| 枚举名称 | 现有值 | 新值 | 是否修改 |
|---|---:|---:|---|
| `WarDispositionEventType.PartyVictory` | `3f` | **`2.5f`** | 是 |
| `WarDispositionEventType.PartyDefeat` | `-3f` | **`-2f`** | 是 |
| `WarDispositionEventType.PartyDestroyed` | `-6f` | **`-2f`** | 是 |
| `WarDispositionEventType.ClanMemberCaptured` | `-3f` | **`-1.5f`** | 是 |
| `WarDispositionEventType.EnemyCaptured` | `2f` | **`1.5f`** | 是 |
| `WarDispositionEventType.ClanMemberKilled` | `-4f` | `-4f` | 否 |
| `WarDispositionEventType.ClanMemberExecuted` | `-10f` | `-10f` | 否 |

**注意**：`PartyDefeat` 和 `PartyDestroyed` 仍是独立事件。对于一支既战败又被销毁的领主部队，两个事件均触发时，本 Clan 基础影响为 `-2 + (-2) = -4`；若其领主又被俘，则按实际英雄数量另行叠加。不得自动合并或互斥处理。

## 3. 领地事件：修改基础影响值

**文件**：同上，`WarDispositionEventCatalog.cs`

| 枚举名称 | 现有值 | 新值 |
|---|---:|---:|
| `WarDispositionEventType.VillageRaided` | `-3f` | **`-2f`** |
| `WarDispositionEventType.CastleLost` | `-6f` | **`-4f`** |
| `WarDispositionEventType.TownLost` | `-8f` | **`-6f`** |
| `WarDispositionEventType.CastleCaptured` | `4f` | **`3f`** |
| `WarDispositionEventType.TownCaptured` | `5f` | **`4f`** |
| `WarDispositionEventType.CastleGranted` | `10f` | **`4f`** |
| `WarDispositionEventType.TownGranted` | `15f` | **`6f`** |

**注意**：

- `CastleLost` / `TownLost` 仍由当前失守逻辑触发；`CastleCaptured` / `TownCaptured` 仍由当前攻城逻辑触发。
- `CastleGranted` / `TownGranted` 仍由当前 `ByKingDecision` 逻辑触发；不增加“必须近期战争征服”之类的条件。
- 攻占和获封仍可各自产生影响；获封仍按既有规则向同王国其他 Clan 传播 20%。

## 4. 每日战争消耗：只改常量，不改公式

**文件**：`Models/Calculation/WarDispositionDailyReturnCalculator.cs`

将：

```csharp
public const float WarWearinessRate = 0.01f;
```

改为：

```csharp
public const float WarWearinessRate = 0.004f;
```

**保留原函数实现**：

```csharp
public static float GetWarDailyChange(float currentDisposition)
{
    return (WarDispositionCalculator.MinimumDisposition - currentDisposition)
           * WarWearinessRate;
}
```

在 `MinimumDisposition = -100` 时，修改后公式仍是：

\[
\Delta D_{war} = (-100-D)\times 0.004
\]

示例（仅展示单日战争消耗，不含事件及人格影响）：

| 当日开始 D | 原每日变化（1%） | 新每日变化（0.4%） |
|---:|---:|---:|
| `0` | `-1.00` | `-0.40` |
| `-30` | `-0.70` | `-0.28` |
| `-50` | `-0.50` | `-0.20` |
| `-80` | `-0.20` | `-0.08` |

**下列和平恢复参数不变**：

```csharp
public const float InitialPeaceRecoveryRate = 0.005f;
public const float PeaceRecoveryRatePerDay = 0.002f;
public const float MaximumPeaceRecoveryRate = 0.06f;
```

## 5. 不变的影响计算机制

当前计算器：`Models/Calculation/WarDispositionCalculator.cs`

```text
单次事件的计算变化 = 事件基础值 × 家族关系倍率 × 人格倍率
```

- 本 Clan 关系倍率：`1.0`。
- 同王国其他符合条件的 Clan 关系倍率：`0.2`。
- 人格倍率依据各事件**现有** Trait 权重计算，并限制在 `[-0.5, 2.0]`；本次不得调整。
- 累计后的战争倾向依旧限制在 `[-100, 100]`。

本次修改只替换公式中的**事件基础值**及单独的**每日战争消耗系数**。

## 6. 计算验收案例：城镇失守 + 领主部队覆灭 + 12 名英雄被俘

假设：

- 城镇原属目标 Clan，因此目标 Clan 受到 `TownLost` 本 Clan 事件。
- 被消灭的 1 支领主部队也属于目标 Clan，且本体分别触发 `PartyDefeat`、`PartyDestroyed`。
- 4 名被俘英雄属于目标 Clan。
- 8 名被俘英雄属于与目标 Clan **同一王国**的其他 Clan，因分发机制对目标 Clan 各计 20%。
- 暂以人格倍率 `1.0` 演示；不计每日变化，不计 `[-100,100]` 边界截断。

| 影响来源 | 计算 | 对目标 Clan 的影响 |
|---|---|---:|
| 城镇失守 | `-6 × 1` | `-6.0` |
| 领主部队战败 | `-2 × 1` | `-2.0` |
| 领主部队覆灭 | `-2 × 1` | `-2.0` |
| 本 Clan 4 名英雄被俘 | `-1.5 × 4 × 1` | `-6.0` |
| 同王国其他 Clan 8 名英雄被俘 | `-1.5 × 8 × 0.2` | `-2.4` |
| **合计** | | **`-18.4`** |

实际游戏结果会受到各目标 Clan 领主的人格倍率和 `[-100,100]` 截断影响。若被俘的其他 Clan 不属于目标 Clan 所在王国，则不能按上述 20% 计算。

**旧值核算校正**：同样的假设按旧表计算应为 `-8 -3 -6 -(3×4) -(3×8×0.2) = -33.8`，而非先前文字中误写的 `-32.8`。新值 `-18.4` 不受该笔误影响。

## 7. Codex / 代码 AI 执行步骤

1. 检查工作区状态，确定当前分支和未提交文件；不得覆盖既有用户改动。
2. 阅读上述两个目标文件，并定位 `CreateDefinitions()` 中对应的 12 个事件定义及 `WarWearinessRate` 常量。
3. 核对文件现有值与本方案“现有值”列一致后，只替换各事件 `Define(...)` 的**第三个参数（基础值）**以及每日战争消耗常量。
4. 检查 Git Diff：**仅两个文件有修改**；只有 12 处事件基础数字和 1 处每日消耗数字发生变化。Trait 权重、方法、注释、命名、换行等均尽量保持原样。
5. 在具备项目环境时编译 `ModifiedPolitics` 的 Release 配置；必要时执行仓库现有架构检查。不要为通过检查而修改无关文件。
6. 检查运行时逻辑与验收案例：新值如表，`-18.4` 的示例逻辑成立；每日 D=`-50` 时变化 `-0.2`。
7. 返回**修改文件路径、diff 摘要、构建结果与不能验证的项目**；如果编译或游戏内验证未执行，应明确标记未验证，不得声称成功。
8. 不得自动修改 `ModifiedArmy` 或同步调整其他数值；任何额外修改建议单列报告，不进入本次提交。

## 8. 验收清单

- [ ] `PartyVictory` `+2.5`
- [ ] `PartyDefeat` `-2`
- [ ] `PartyDestroyed` `-2`
- [ ] `ClanMemberCaptured` `-1.5`
- [ ] `EnemyCaptured` `+1.5`
- [ ] `VillageRaided` `-2`
- [ ] `CastleLost` `-4`
- [ ] `TownLost` `-6`
- [ ] `CastleCaptured` `+3`
- [ ] `TownCaptured` `+4`
- [ ] `CastleGranted` `+4`
- [ ] `TownGranted` `+6`
- [ ] `WarWearinessRate` `0.004f`
- [ ] `ClanMemberKilled` 仍为 `-4`
- [ ] `ClanMemberExecuted` 仍为 `-10`
- [ ] 原 Trait 权重未变
- [ ] 原 100% / 20% 传播规则未变
- [ ] 原和平恢复参数未变
- [ ] 原事件监听与叠加未变
- [ ] 原 `ModifiedArmy` `-50` 阈值及相关逻辑未变
- [ ] 只有指定两个文件发生本任务的代码改动
- [ ] 编译结果、游戏内验证状态明确报告

## 9. 源文件定位

仓库：`GuihuaZhou/bannerlord`

- [事件基础值目录：WarDispositionEventCatalog.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Events/WarDispositionEventCatalog.cs)
- [每日消耗：WarDispositionDailyReturnCalculator.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Calculation/WarDispositionDailyReturnCalculator.cs)
- [影响计算器（只读）：WarDispositionCalculator.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Calculation/WarDispositionCalculator.cs)
- [事件分发器（只读）：WarDispositionEventDistributor.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Distribution/WarDispositionEventDistributor.cs)

> 如仓库默认分支不是 `main`，以实际检出的对应路径为准。本文档采用仓库读取时确认存在的文件路径。
