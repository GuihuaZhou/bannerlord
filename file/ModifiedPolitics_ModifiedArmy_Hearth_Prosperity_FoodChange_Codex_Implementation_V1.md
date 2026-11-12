# ModifiedPolitics × ModifiedArmy：文化 Hearth 容量、粮食—繁荣度—征兵人口闭环
## Codex 可执行实施方案 V1.0（基于 Git 仓库最新审查基线）

> **施工基线**：`GuihuaZhou/bannerlord`，默认分支审查 HEAD `5dfdc81d04cf556b7ab9df4483486e0542598f1b`，审查日期 2026-11-12。  
> **文档性质**：可直接交给 Codex/AI Worker 的代码实施任务书；此文档本身**不代表已经修改、编译或在游戏中验收**。  
> **规则优先级**：当前工作树代码及 Git 状态 > 用户已经确认的规则 > 本文候选平衡参数 > 旧设计文档。若 Codex 本地 HEAD 更新，必须重审受影响文件，禁止按旧行号盲改。
>
> **关键事实**：最新仓库**已经实现** RecruitmentBurden 原子招募事务、职业兵可用兵源 UI、逐村封邑供养显示，并**已经移除**历史 SharedSupply / ProfessionalPopulationSupply 对实际生产速度的乘算。本次只改仍待实施的经济人口规则，不重做已完成功能。

---

## 0. Codex 首次执行指令（必须先做）

在 Windows PowerShell、仓库根目录：

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
Get-Content .\AGENTS.md
Get-Content .\ModifiedArmy\AGENTS.md
Get-Content .\ModifiedArmy\CODE_ORGANIZATION_RULES.md
```

1. 保存 branch/HEAD/工作树状态，不覆盖未提交改动；检查 `ModifiedPolitics` 目录是否有新增的 `AGENTS.md` 或更近层级规则（审查基线中该路径不存在）。
2. 对照本文“源码事实清单”确认方法和文件仍存在，沿真实调用链阅读，不依据旧 Worker 报告推断功能未完成。
3. 先记录 **修改前** 的新档 / 旧档人口、经济、职业兵、封邑兵行为及现有测试基础。不得自动 commit、push 或创建 PR。
4. 不改变战争倾向、AI 决策、财政、驻军人数策略、征粮队、粮食消耗除数 `34`、职业兵 Tier 负担表、封邑兵 `FeudalSupply` 及兵役逻辑，除非确有本任务所必需的最小接口兼容修改。
5. 按下文 P0→P6 分阶段实施、逐阶段静态检查；游戏内证据不足时如实报告 `BLOCKED`，不能声明 `PASS`。

## 1. 已确认设计：目标、概念和边界

### 1.1 不做什么

- 不做商业型 / 农业型城市新分类，不做移民系统，不造村庄食品库存，不引入人口职业细分。
- 不以 FoodStocks 现存库存、粮仓上限、仓储百分比或 `IsStarving` 判断 Hearth 的粮食项。**只读取 `Town.FoodChange`**；原版自己的 FoodStocks 消耗、饥荒、繁荣度计算保持原状。
- 不将 FoodChange、繁荣度、征兵压力设计为其他人口增长项的**倍率**；四项加减结果分别求值并汇总。
- 不新增一项“征兵压力直接扣 Prosperity”的并行惩罚；仍通过有效户数下调繁荣度支持线。
- 不把职业兵 B 施加到村庄封邑兵供养、MobilizedHearth、返乡、服役、逐村预算。
- 不重新引入旧“基础兵源池”或重复实现已经落地的职业兵／封邑兵 UI。

### 1.2 数据定义（正式约束）

| 符号 | 含义 | 规则 |
|---|---|---|
| `H` | 城镇／城堡实际 Hearth | 人口增长基数、成功职业兵征募的实际扣款资源 |
| `K` | 文化与定居点类型决定的 Hearth 容量 | 由 **`Settlement.Culture` 原生文化**、村／城镇／城堡、城墙等级决定；不随统治者更换 |
| `B` | `RecruitmentBurden` 征兵压力 | 新职业兵增加；按原有文化规则独立日恢复（当前各文化 2/日） |
| `E_signed` | 带符号有效户数 | **`H - B`**，用于征兵资格／余额判定，可为负 |
| `E_econ` | 经济有效户数 | `max(0, H - B)`，用于繁荣度支持线；不得使用旧 50% 折减 |
| `P` | 原版 Prosperity | 保留本体所有变化因子 |
| `F` | `Town.FoodChange` | 每日粮食净变化，可正可负，**不是** FoodStocks |
| `C` | 每 Hearth 繁荣度支撑 | 城镇 `5`，城堡 `3`（保留原配置项） |

**三个不同的繁荣度基准，绝不混用：**

```text
P_support = C × max(0, H - B)              经济实际支持线（软衰退基准）
P_dynamic = C × H                           实际户数动态线（繁荣促进阈值）
P_final   = C × K                           文化容量最终参考上限
```

- `P_support` 在 `B>=H` 时为 **0**，不隐藏该设计后果；超额衰退仍按现有 `84` 天软回落，不在征兵瞬间强制清零。
- `P_dynamic` **只能用实际 H**，不允许用 `H-B`；否则征兵压力突升会虚假触发繁荣促进。
- `P_final` 是人口达到文化容量 K 后的繁荣度参考上限。非正常旧档 `H>K` 情况不能即刻裁剪 Hearth/Prosperity；软调整策略见 §6。

### 1.3 城镇/城堡 Hearth 每日四项独立计算（固定结构）

```text
G_natural = NaturalGrowth(H, K, typeConfig)
G_food    = FoodContribution(Town.FoodChange, config)
G_prosp   = ProsperityContribution(H, K, P, C, config)
G_burden  = RecruitmentBurdenContribution(H, K, B, config)

G_raw = G_natural + G_food + G_prosp + G_burden
H_next = ApplyCapacityBoundary(H, K, G_raw)     // 最后统一约束
B_next = max(0, B - DailyRecovery(culture))    // 独立结算
```

每次成功**新征募**职业兵另行由现存 `TryRecruitProfessional` 事务扣除每人 1 H 并增加 Tier 负担，不再作为每日公式的第五项；训练候选生成本身不扣费。积分日志和 UI 应显示 4 个贡献及**实际应用后的**总变化。

### 1.4 四项具体计算：先落地候选值，保留 XML 可调

**① 自然增长项**：保留现有非线性曲线及“低户数恢复”的原实现，在其内部允许低人口加成，但不作用于其他三个项。

```text
G0 = r_type × (H + 100) × (1 - H/K)
if G0 > 0:
    m_low = 1 + (2.0 - 1) × max(0, 1 - H/(0.5×K))
    G_natural = G0 × m_low
else:
    G_natural = G0
```

候选 `r_town = r_castle = 0.002`、`r_village = 0.004`；低人口参数继续沿用 `seed=100`, `lowPopulationMultiplier=2.0`, `lowPopulationThreshold=0.5`。上述为**建议测试值**，非既有提交参数。

**② 粮食项：直接加减 Hearth**：

```text
G_food = clamp(FoodChange × 0.05, -2, +2)
```

`FoodChange=-20` → `-1.00 Hearth/日`；`+10` → `+0.50`。若粮食项为负，而其余三项合计为正，**最终 H 仍可增长**。不读取粮仓现存粮量。候选系数 `0.05`、下限 `-2`、上限 `+2` 必须可配且验收时通过数据模拟复核。

**③ 繁荣度促进项：用实际 Hearth 的动态线上限，而非有效 Hearth**：

```text
P_dynamic = C × H
P_final   = C × K
if H < K AND P_dynamic < P AND P < P_final:
    G_prosp = min(0.6, 0.6 × (P - P_dynamic)/500)
else:
    G_prosp = 0
```

这个**严格开区间** `CH < P < CK` 是用户明确选择的简化规则；不可替换为 P 接近 `P_support` 的判定。`+0.6/日`、超出 500 Prosperity 达到上限为**候选平衡参数**，可配置。阈值 `P==CH`、`P==CK` 均为 0。`P==CK` 附近有可能出现阶跃，**先按已约定逻辑实现并以测试记录，未经确认不要偷偷平滑或改成倍率**。

**④ 征兵压力项：独立负数**：

```text
R = clamp(B/max(H,1), 0, 1)
G_burden = -0.0003 × K × R
```

候选系数 `0.0003`，仅为每日人口变化中的负贡献，不取代当前职业兵实际消耗 H、增加 B 和 B 每日消退事务；这里允许 H=0 时按 `max(H,1)` 安全计算。`B/H` 饱和到 1 后负值有下界；不计算正数。

### 1.5 封邑兵和村庄（强制边界）

- 村庄只配置文化专属容量与村庄原有自然增长路径，不套用城镇 `FoodChange`、Prosperity、职业兵 `B` 四项模型；村庄遭劫掠及原版其它变化继续生效。
- 计算村庄自然增长时保留已有 `village.Hearth + MobilizedHearth` 的**人口有效基数**语义，避免已经动员的封邑兵被视为消失、造成额外低人口增长；村庄实际 Hearth 写入仍由本体/现有事务拥有。
- 逐村封邑兵供养、`FeudalSupply`、`GetEffectiveSupplyHearth`、`GetFeudalBudgetHearth`、`GetOccupiedSupplyHearth`、`MobilizedHearth` 不使用 `B`。不能因为名称都叫 `Effective...Hearth` 而误用城镇经济 E。

---

## 2. 七文化容量配置（第一轮候选，非最终实测平衡）

| 原生文化 ID | 文化 | 村庄 K | 三级城堡 K | 三级城镇 K |
|---|---|---:|---:|---:|
| `Culture.empire` | 帝国 | 700 | 800 | 2800 |
| `Culture.aserai` | 阿塞莱 | 550 | 650 | 2600 |
| `Culture.vlandia` | 瓦兰迪亚 | 900 | 750 | 1850 |
| `Culture.sturgia` | 斯特吉亚 | 650 | 600 | 1650 |
| `Culture.khuzait` | 库赛特 | 550 | 500 | 1500 |
| `Culture.battania` | 巴旦尼亚 | 650 | 450 | 1200 |
| `Culture.nord` | 诺德 | 550 | 400 | 1000 |

设计排序：帝国/阿塞莱城镇人口大；瓦兰迪亚村庄最多；巴旦尼亚/诺德农村占比大但绝对人口不高。**请使用 `settlement.Culture.StringId`，不是 owner clan/kingdom 文化**。

- 城堡／城镇按城墙等级计算 K：**一级 40%、二级 70%、三级 100%**；现有 `Town.GetWallLevel()` 的 1～3 兼容映射保留。
- **“初始 Hearth”与“K 容量”不同。** 候选新局初始 `initial = round(K/3)`（尽量沿用旧配置初始约为容量 1/3 的结构），存 XML 中而不运行时每次计算；初始化只能在该城镇/城堡原来没有 `_hearth` 存档项时使用。
- 村庄没有城墙等级，也不能在加载旧存档时重写 `Village.Hearth`。
- 不因攻城改变本地文化容量；若游戏另有文化转换，须明确该转换由哪个游戏事件触发，然后动态应用新容量，不能仅以领主文化代替定居点文化。

**建议新 XML 结构**（结构可按当前 XML 解析约定微调，但必须单一配置来源、七文化严格验证）：

```xml
<Population>
  <NaturalGrowth seed="100" lowPopulationMultiplier="2.0"
                 lowPopulationThreshold="0.5" />
  <PopulationRates village="0.004" town="0.002" castle="0.002" />
  <DailyFactors foodPerChange="0.05" foodMin="-2" foodMax="2"
                prosperityTriggerSpan="500" prosperityMax="0.6"
                burdenPerCapacity="0.0003" />
  <Cultures>
    <Culture id="Culture.empire" villageCapacity="700"
             townCapacity="2800" castleCapacity="800" />
    <Culture id="Culture.aserai" villageCapacity="550"
             townCapacity="2600" castleCapacity="650" />
    <Culture id="Culture.vlandia" villageCapacity="900"
             townCapacity="1850" castleCapacity="750" />
    <Culture id="Culture.sturgia" villageCapacity="650"
             townCapacity="1650" castleCapacity="600" />
    <Culture id="Culture.khuzait" villageCapacity="550"
             townCapacity="1500" castleCapacity="500" />
    <Culture id="Culture.battania" villageCapacity="650"
             townCapacity="1200" castleCapacity="450" />
    <Culture id="Culture.nord" villageCapacity="550"
             townCapacity="1000" castleCapacity="400" />
  </Cultures>
  <WallCapacityFactors level1="0.40" level2="0.70" level3="1.00"
                       initialToCapacity="0.3333333333" />
  <ProsperitySupport townPerHearth="5" castlePerHearth="3"
                     excessDecayDays="84" />
</Population>
```

**仅为目标结构片段，不要直接覆盖 XML 根文件**：保留已有 `<Food><ProsperityConsumptionDivisor value="34" /></Food>` 和其他无关配置。Codex 可选择沿用原有 `<Town><Wall.../></Town>` 风格扩展文化条目，但**不要同时维护“文化表 + 旧通用容量表”两份运行期真相**。必须验证 XML 对象类型注册、XML XPath、标签是否唯一、完整七文化加载、`initial>0且initial<=K`、壁垒 1/2/3 单调、无负系数、所有浮点有限。

---

## 3. 已核实的仓库代码事实（避免重复劳动）

| 文件／模块 | HEAD 现状 | 本任务动作 |
|---|---|---|
| `ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml` | 统一村庄 K=1200，城镇三级 K=4500，城堡三级 K=1800；统一自然增长 `0.004`；支持系数 5/3；84 天 | **改**为文化和定居点类型容量、分类自然率、独立因子参数 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs` | `VillageCapacity` 单数、`TownWalls`/`CastleWalls` 共用三级、`NaturalGrowthRate` 全局 | **改**为七文化表、类型增长率、因子系数，提供只读查询与校验 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/PopulationGrowthModel.cs` | `Calculate(effectiveHearth, capacity, config)` 仅返回自然增长（含低人口恢复倍率） | **扩**：自然增长纯函数保留；增加可纯测的四项 Forecast/Math，不再以该函数直接代表“每日全部增长” |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs` | `_hearth`、`_recruitmentBurden` 由此持久化；每天只算一项自然增长并恢复 B；`GetEffectiveHearth` **仍为 `H - 0.5*min(H,B)`**；`GetNaturalCapacity()` 走统一 K | **主改**：文化 K、四项独立每日结算、有效户数改为 `max(0,H-B)`、同日一次更新及日志 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs` | 原版繁荣度 + 经济支持线的 84 天软衰退；村庄 Hearth 走该 Model | **小改**：城镇支持线走新 E；村庄 K 按文化解析，维持原版非自然修正 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyFoodModel.cs` | 原版/Army 计算 FoodChange、村庄供粮、`prosperityConsumptionDivisor=34` | **不改**；只在 Hearth Forecast 读取 `Town.FoodChange` |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Presentation/SettlementPopulationVMMixin.cs` | 城镇/城堡 overlay 的每日变化仍仅显示旧自然增长预测；Tooltip 支持度 `H×C`，未包含 B | **改**为复用四项 Forecast，显示 Hearth/K、四项、应用后总数、三个繁荣度参考线（适度呈现） |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Persistence/SettlementPopulationSaveDefiner.cs` | type definer `2026110701`、record class 1、现有字典容器 | **原则不改**；避免增加新持久字段；若必要新增 ID 需严格审计 |
| `ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs` | 已有 `GetNaturalCapacity/GetRecruitmentBurden/GetRecruitmentCapacity/GetEffectiveHearth` 与可回滚职业征兵事务 | **原则不改签名**；让 Politics 保持接口语义，Army 不得依赖 Politics |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | 已移除 SharedSupply/ProfessionalPopulationSupply 的日生产乘数；职业候选来自训练能力；封邑来自 `dailyRate` | **保持不变**（仅做回归测试） |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs` | 已显示职业兵 Available manpower、逐村封邑供养情况 | **保持现状**，不要重复编写；若要显示新的 Hearth 增长明细，应放到 Politics overlay，而非修改兵源菜单 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs` | 村庄 `Hearth+MobilizedHearth`、逐村供养、文化 FiefWeight 预算 | **完全保留**，仅关注文化 K 缩小后的供养回归 |
| `code/Town.cs`、`code/DefaultSettlementFoodModel.cs`、`code/DefaultSettlementProsperityModel.cs` | 仓库保存本体参考代码；`Town.FoodChange` 调现有 `SettlementFoodModel`，本体繁荣度也受到粮食和市场影响 | **只读 API 参考**，绝不改写解编译参考文件；部署前用实际游戏 DLL 验证 API |

**存档不变式：** `SettlementPopulationBehavior.SyncData` 的 `...Hearth.v1`、`...Transactions.v1`、`...Recovery.v1`、`...LastDailyTick.v1`、`...RecruitmentBurden.v1` 不改名；`PopulationTransactionRecord` 已存在 `[SaveableProperty(1..10)]`，不得重编号。`ModifiedArmy` 原有 `[SaveableRootClass]` 和封邑兵 SaveId 8/9 不触碰。

**依赖方向**：`ModifiedPolitics` 可引用 `ModifiedArmy` 契约，反过来禁止；现有 `Test-ArchitectureReleaseGate.ps1` 有此检查。

---

## 4. 建议的代码架构与 API（精确施工）

### 4.1 配置责任：ModifiedPolitics（P1）

**编辑** `SettlementEconomyConfig.cs` + `settlement_economy.xml`：

- 新增明确实体（命名可调整）：
  - `CulturePopulationCapacityConfig { CultureId, VillageCapacity, TownCapacityAtWall3, CastleCapacityAtWall3 }`
  - `PopulationGrowthRateConfig { Village, Town, Castle, Seed, LowPopulationMultiplier, LowPopulationThreshold }`
  - `HearthDailyFactorConfig { FoodPerChange, FoodMin, FoodMax, ProsperityTriggerSpan, ProsperityMax, BurdenPerCapacity }`
  - `SettlementCapacityResolver.GetCapacity(settlement)` 或 `config.GetCapacity(Settlement settlement)`（只读，没有状态副本）。
- 保留项目既有注册路径：`ModifiedPolitics/Main.cs` 中的 `MBObjectManager.LoadXML("SettlementEconomyConfigs")`，不新增第二份 XML 加载系统。
- 若不引入 `CultureObject` 编译期依赖，可使用 7 个精确字符串 ID 做字典索引，加载时验证全部必需 ID 与唯一性；严禁静默 fallback 到错误文化。
- 墙级系数仅针对 Town/Castle；对无效墙级维持既有 clamp 1..3 和有限日志，不能报错导致整局中断。
- XML 中 `capacity` 是**上限**，`initial` 是新城镇/城堡**第一次创建存档**的初始 Hearth。层级容量更新不代表立刻写入或截断存档。
- 若选择沿用 `PopulationWallConfig`，允许增补按 culture 的配置，但要删除/废弃不再使用的统一只读路径，禁止 UI 和游戏计算分别从不同配置取 K。

### 4.2 数学模型责任：ModifiedPolitics（P2）

在 `SettlementEconomy/Models` 中引入**无副作用**的每日贡献模型，例如：

```csharp
// 仅为目标签名；避免为了签名而复制 Campaign 状态到 Model。
public sealed class DailyHearthForecast
{
    public float Natural { get; init; }
    public float Food { get; init; }
    public float Prosperity { get; init; }
    public float RecruitmentBurden { get; init; }
    public float RawTotal { get; init; }
    public float AppliedTotal { get; init; }        // 结算边界后的真实预测值
    public float CapacityAdjustment { get; init; }  // AppliedTotal - RawTotal
    public float Capacity { get; init; }
    public float SupportProsperity { get; init; }
    public float DynamicProsperity { get; init; }
    public float FinalProsperity { get; init; }
}

public static DailyHearthForecast Calculate(
    float hearth, float capacity, float burden,
    float prosperity, float foodChange, float perHearth,
    PopulationGrowthSettings config);
```

**说明**：`init` 语法仅示意；若当前 C# 语言版本不支持，使用项目兼容的 `private set`/构造器或结构类型，不要为了该示例升级语言版本。

- `PopulationGrowthModel` 可以保留现有 `Calculate` 作为自然项，但引入新的 `CalculateDailyForecast` 或 `HearthGrowthContributionModel` 统一协调四项；原始增长模型不要持有 `Settlement`, `Town` 等可变引用。
- 输入使用 *同一快照* 的 `H,K,B,P,F,C`，所有项基于相同起始值；严禁先算粮食修改 H、再用新 H 算繁荣加成。
- 检查 `float.IsFinite` 替代能力（若运行时不支持，用 `!float.IsNaN && !float.IsInfinity`）；不得允许 NaN/Infinity 进入存档。
- 不根据 `FoodChange` 推导粮仓库存状况；把粮食项定为独立的 `float`，允许是负的。
- `P_dynamic`、`P_final` 必须使用 **实际 H / K**；`P_support` 使用 **`max(0,H-B)`**。候选中的 `P_support` 如需额外 `min(K,...)` 以保证非正常超容量旧档不突破最终支持上限，见 §6；不能悄悄改变常态规则。
- 该 Forecast 是预测/说明数据，**不得在 UI 或配置解析时改变 Hearth、Burden、粮食或 Prosperity**。

### 4.3 持久与每日结算责任：SettlementPopulationBehavior（P3）

**改动** `EnsureDailyPopulationUpdate(settlement, day)`：

1. 沿用 `_hearth`、`_recruitmentBurden`、`_lastDailyTick`，这些仍是唯一 authoritative 存档状态。
2. 进入后检查日期 once-only；在会话中初始化 K 和 H；若 `RecoveryRequired` 则按现有机制冻结，不自动重置。
3. 从 `settlement.Town` 读 `Prosperity`、`FoodChange`，从同一个 `_hearth` / `_recruitmentBurden` 快照读 H/B，从新的 config/resolver 读 K 与 C。
4. 调用纯模型算出四项 + 边界后的 `AppliedTotal`，不写其它资源。
5. 获取原有 `RecruitmentBurdenPolicyRegistry.Current.GetDailyRecovery(settlement)`（当前文化配置为 2）；先基于当日 **B_before** 计算人口贡献，再计算 `B_after=max(0,B_before-recovery)`；两者在同一次成功结算内提交。
6. **只提交一次** `_hearth[settlement]`、`_recruitmentBurden[settlement]`、`_lastDailyTick[settlement]`；过程失败回滚 H/B 并保留恢复冻结语义。不要破坏现存 `TryRecruitProfessional()` 调用 `EnsureDailyPopulationUpdate` 的先决流程。
7. 每日诊断可在 player kingdom 范围输出 `HBefore, K, BBefore, P, FoodChange, GNatural, GFood, GProsperity, GBurden, RawTotal, CapacityAdjustment, AppliedTotal, HAfter, BRecovered, P_support`，避免每次 UI 查询都刷日志。

**强制调查：事件顺序。** 当前 `EnsureDailyPopulationUpdate()` 除每日 Settlement Tick 外，还会在 `TryRecruitProfessional()` 事务开始前触发，避免同日事务使用过期人口。`Town.FoodChange` 是动态计算属性、`Town.Prosperity` 可能当天也变化；先在实际游戏 API / 事件中确认哪一次调用应成为每日快照边界：

- 优先维持“**每天第一笔人口/职业招募事务前，确保当日人口更新完成**”，不准拿不存在的上一日字段伪称稳定。
- 对同日所有后续调用复用 `_lastDailyTick` 保障不二次更新；调用 `GetDailyHearthForecast()` 仅作只读预测，不能触发 `EnsureDailyPopulationUpdate()`。
- 如实测同一天不同事件先后能明显改变 `FoodChange/P`，应先修订统一事件时序或明确快照采样时点；**不得**未经验证叠加第二个 Tick 来补偿。若需要跨天持久快照，应单独设计 SaveKey/类型兼容方案，先报告风险，不能随意加一个 Dictionary 并称已解决。

**`GetNaturalCapacity()`**：村庄取自身 `settlement.Culture` 的 Village K；城镇/城堡按同文化墙级 K；同时向 `GetEffectiveHearth()` 返回 `max(0,H-B)`（经济意义），`GetRecruitmentCapacity()` 必须仍返回带符号 `H-B`。`GetRecruitableHearth()` 目前单纯返回实际 H 的 floor 且在恢复冻结时为 0，不可为了 UI 名称直接更改它而破坏消费者；先核对所有调用位置。

### 4.4 繁荣度支持模型：SettlementEconomyProsperityModel（P4）

- 继续调用原版 `base.CalculateProsperityChange`，保留原版粮食、忠诚、治安、建筑、市场、税务等原始因子；只替换人口支持线部分。
- 经济支持基准 `C * GetEffectiveHearth(settlement)`，这时该接口必须已改为 `max(0,H-B)`，不再使用 50% 折减公式。
- 保留原先：若 `P < P_support`，正增长不越过支持线；若 `P>=P_support`，原有正增长不能增加，按 `-(P-P_support)/84` 渐进衰退（按代码与模型 `min` 处理更强的原版负增长）。不能为它再加另一项 Burden 繁荣度直接扣款。
- **判断繁荣度促进人口的 `P_dynamic=CH` 时，不允许复用此处的 `P_support`**。支持线和促进门槛本来就是不同值，尤其征兵压力 B>0 时。
- 不修改原版 `Town.FoodChange` 的计算和 FoodStock 上限，不修改粮食民用消费 `Prosperity / 34`。避免因为新增人口粮食反馈而在粮食模型中再次加项。

### 4.5 村庄人口：SettlementEconomyProsperityModel.CalculateHearthChange（P4）

此路径**不能遗漏**：

- 已有村庄增长由 `CalculateHearthChange(Village village...)` 注入，而非 `_hearth` 城镇字典的 Daily Tick。
- 将 `config.VillageCapacity` 切换为 `config.GetCapacity(village.Settlement)`，并使用村庄 `r=0.004`；保持 `village.Hearth + MobilizedHearth` 参与自然增长。
- 保留对原版 `base.CalculateHearthChange` 的计算及 `natural - vanillaBase` 替换逻辑；特别检查实际原版是否有“劫掠/被围/建筑/文化/事件”附加项，不能将它们误覆盖掉。
- 当村庄 Hearth > 新 K，不强制改写旧档原值；通过自然项与原版其它因素慢慢衰退；避免“上限强制截断”在封邑兵服役／返乡状态下丢失人口或使现有供养账本失衡。
- 不把村庄绑定城镇的 FoodChange/P/B 套用到村庄；村庄人口减少影响村庄产出与封邑供养是现有系统的正常间接反馈，不新增额外扣费。

### 4.6 人口 Overlay UI：SettlementPopulationVMMixin（P5）

**当前 UI 是已发现的误导点**：`RefreshPopulation()` 与 `BuildPopulationTooltip()` 只调用 `PopulationGrowthModel.Calculate()`，在本方案实施后它将不再等于实际每日 Hearth 变化。

- 城镇/城堡 overlay 显示 `H / K` 与 **应用边界后的每日 Hearth 净变化**，不再显示仅自然项预测为总变化。
- Tooltip 建议如下（需英中 i18n，见 §8）：

```text
Hearth: 1200 / 2800
Recruitment burden: 200
Recruitment capacity (signed): 1000
Natural growth:             +1.70
Food change (FoodChange -20): -1.00
Prosperity contribution:    +0.60
Recruitment burden:         -0.14
Capacity boundary adjustment:+0.00   (only when nonzero)
Daily Hearth change:        +1.16
Prosperity support:          5000    [C × max(0,H-B)]
Population dynamic line:     6000    [C × H]
Culture final reference:    14000    [C × K]
```

- 这些是同一快照的只读数学结果；日显示小数 `+0.00;-0.00;0.00`，若 overlay 主数字只支持整数则遵循原生 VM 设计，但 Tooltip 保留 2 位。
- UI 在结算前的“预测值”和结算后的“今日已应用实际值”可能不同，**明确标注**；不要从实时 FoodChange 推算结果却称为今日已结算。若不引入持久日快照，就标注“当前预测”。
- 不更改已存在的 `ModifiedArmy/FiefMenuBehavior` 职业兵可用兵源与封邑供养内容；勿用 UI 读取造成持久更新或账本写入。

---

## 5. 具体施工阶段（每阶段给出可验证交付）

### P0 — 代码基线与差异核验（无代码改动）

- 核对 Git HEAD/branch/status，阅读 AGENTS 与组织规则。
- 跟踪 `Town.FoodChange` 的来源、本体 DailyTick 顺序、Population Behavior 何时 Ensure、Prosperity Model 何时被调用、Village Hearth 何时变化。
- 搜索 `GetEffectiveHearth/GetNaturalCapacity/GetRecruitableHearth/PopulationGrowthModel.Calculate` 所有调用者，形成调用表。
- 检查当前 XML 七文化 ID、`Settlement.Culture` 的实际值及特殊 WarSails/DLC 或缺文化定居点；所有七文化必须覆盖。
- 保存旧存档样本（高于新 K、B>H、封邑兵已动员）和新游戏样本；记录初始 H、B、P、FoodChange。

**交付**：一份 `P0-Trace.md`（或最终报告一节）记录实际调用/写入所有权，列出所有外部 API 与无法验证的地方；本阶段不修改代码。

### P1 — 容量和参数 XML（独立可编译）

- 改 XML + Config：支持七文化 × 三类定居点 + 城墙等级；拆分自然增长率；增加 Food/Prosperity/Burden 独立项系数。
- 读取已有统一 XML 需保持兼容与清晰迁移；若删除旧节点，确认所有旧消费者已改向新的 resolver。不得在此阶段改征兵事务。
- 验证所有 K>0，墙级单调，初始值合理，文化 ID 唯一，所有系数合法。

**阶段验收**：XML 加载正常；所有 7×(村、堡、镇三级) 输出匹配候选表；更换领主文化后 K 不变。

### P2 — 四项纯模型和容量边界

- 保留既有自然公式实现并改造为 type-specific `r`；创建纯模型返回 4 项、raw sum、capacity adjustment、applied sum、P_support/P_dynamic/P_final。
- 建立无游戏依赖的确定性测试入口（独立简易测试项目或仓库允许的静态脚本）；不以大规模集成测试替代纯函数验证。
- 下述边界规则要单独测试：
  - 常态 `0<=H<K`：`H_after=clamp(H+G_raw,0,K)`，若负增长仍允许减少。
  - `H==K`：禁止进一步正增长；负增长仍允许降低。
  - 旧档 `H>K`：**禁止新增超额人口**，又不能瞬时裁剪到 K；保证至少按原自然负增长方向回落。建议 `G_applied = min(G_raw, G_natural)`（此时 `G_natural<0`）作为旧档超容量安全限速，记录 `CapacityAdjustment`，而不是直接 `H=K`。该分支属于建议，施工前需用旧档验证单日回落是否平滑。
  - `H=0`、`B>H`、`P_dynamic=0`、`F` 极大/极小、浮点边界、`P==CH/CK`。
- 单个自然增长项内部保留低人口恢复加成可接受，但**不可将其套到其他项**。

**阶段验收**：数学输出与 §7 示例一致；模型完全只读、可重入、同输入必同输出。

### P3 — 日结算与事务兼容

- 在 `SettlementPopulationBehavior` 中完成每日快照与一日一次提交；保留 B 恢复、人口交易、恢复冻结、回滚路径。
- 将经济 `GetEffectiveHearth` 改为 `max(0,H-B)`；保留带符号 `GetRecruitmentCapacity`。
- `GetNaturalCapacity` 改为定居点原生文化容量。
- 现有职业兵 `TryRecruitProfessional` 的 **“结算当日人口后再扣 H + 增 B”** 事务不得取消；不要调整 Tier 成本和每天恢复 2 的权威来源。
- 不新增第二套 `_hearth`/`_burden` 存储；无 SaveKey 改动优先。

**阶段验收**：一个 settlement 每天只发生一次自然更新；新兵事务 H/B 原子性、拒绝与补偿不变；旧存档保存再加载无丢失。

### P4 — Prosperity 与村庄路径

- 切换 `SettlementEconomyProsperityModel` 支持线；维持原版繁荣度变化、84 天超额衰退；不再直接依赖 50% 的旧有效户数。
- 在 `CalculateHearthChange(Village)` 通过文化 resolver 取得 Village K，并保留原版其它 village modifier。
- 确认墙级、村庄文化、村庄劫掠及村庄封邑服役情况会改变相应预测，而不会破坏占用计算。

**阶段验收**：`B=H` 时经济支持线为 0，P 不瞬间清零；`P_dynamic` 不因 B 变化；村庄不读取绑定城镇的 FoodChange。

### P5 — UI、中文本地化、日志

- Politics Overlay 显示 H/K、四项及合计与三条繁荣度线；按需要适配当前 UIExtender/VM binding。
- 检查 `ModifiedPolitics/ModifiedPolitics/ModuleData/Languages/CNs/modifiedpolitics-zho-CN.xml` 的键与 XML 内容是否匹配；新增 UI 标签使用 English fallback 与中文翻译，避免硬编码中文 C#。
- 更新必要的配置注释与旧名称说明；日志按玩家王国过滤 + 每日聚合，不记录每次 UI 查询。
- 再次确认 Army 已存在的「Available manpower / 村庄供养」UI 无回归，且 SharedSupply 不会重新出现。

**阶段验收**：UI 数值 = 模型预测；重新打开菜单不改变资源；中英文可读无未替换占位符。

### P6 — 完整验证、数值模拟与交付

- 运行 Release 编译与架构门禁；保存全部输出。
- 建立 84 天/1 年/3 年（1 游戏年按本体规则在实际环境确认）的模拟，覆盖七文化、高人口、低人口、缺粮、战时与战后场景；将趋势图或 CSV 作为独立 evidence，不直接改变数值方案。
- 校验新局、旧档、存取、失效/失败补偿、叛乱/易主、墙级升降、村庄劫掠、封邑兵外出与返乡。
- 输出源码 diff、文件清单、参数表、测试矩阵及 PASS/FAIL/BLOCKED；遇到问题按失败阶段回退窄修复，不做无关重构。

---

## 6. 迁移、边界及严禁错误处理

### 6.1 新旧存档

- 旧档现有 H/B 原值保持。新的文化 K 是新的“发展容量”，**不是加载存档时的裁剪器**。
- 新增 XML 参数不需要新的存档字段；旧 `...Hearth.v1` 和 `...RecruitmentBurden.v1` 原样序列化。
- 如果 H>K，允许慢慢回落；不能因为保存／加载或墙级下调突然删除 Hearth。特别是已动员封邑兵、严重征兵压力、负余额、城镇易主时。
- 对“村庄当前 H 和 MobilizedHearth 合计超过新 K”的情况，保留现有动员账本，限制非真实新人口增长，不能使返乡/兵役交易失去可恢复性。
- 对原生 culture 缺失/未知、严重损坏或 XML 缺项，首选启动阶段清晰报错/配置加载阻断，而非运行期默默使用帝国 K。

### 6.2 繁荣度是否受最终上限限制

一般 `H<=K` 时 `P_support<=P_dynamic<=P_final`（当 B>=0），现有支持线自然使繁荣度不能长期越过 P_final。旧档 `H>K` 时本关系可能暂时失效。

**施工默认**：不添加“繁荣度瞬间 hard cap”，保留旧 `84` 天软衰退；如发现旧档 H>K 导致 `P_support > P_final`，可以在仅用于经济承载的计算处选择 `P_support = C × max(0,min(K,H-B))` 来保持文化最终上限语义，但这不应改变 `GetEffectiveHearth()` 的定义 `max(0,H-B)`，且必须明确记录这项额外边界并测试旧档。如果不需这条额外边界，先保持严格 `C×max(0,H-B)` 并在报告中标明旧档过渡期例外，不能无报告擅加逻辑。

### 6.3 FoodChange 与原版自反馈

- `Town.FoodChange` 本身由当前军粮、村庄供给、市场及 **Prosperity/34 民用消耗**等影响，且原版 prosperity 已受到粮食变化影响。新 `G_food` 形成额外回路，必须以真实游戏数据观察振荡。
- 即使 `Town.FoodStocks` 为 0 或满仓，也**只按用户要求的 FoodChange 值**结算人口项；不推断库存状态。FoodChange 非有限值应拦截，不提交。
- 只对城镇/城堡读取 `FoodChange`，村庄没有独立该字段；不能把粮食项映射到所有所属村庄造成多重计算。
- FoodChange=-20 产生 `G_food=-1`，但 `G_raw` 可 >0；不允许在逻辑中写 `if (FoodChange<0) return negative...` 之类短路。

### 6.4 B≥H 压力边界

- `GetRecruitmentCapacity()=H-B` 可以是负值；职业兵征募资格仍按已有每名检查。
- `GetEffectiveHearth()` 经济为 `max(0,H-B)`，即 P_support 可能为 0；不再保留 `0.5*min(H,B)`。
- B 自然恢复保留，日人口结算的 B 负贡献不得因 B=H 或 H=0 导致除零。
- **不要**额外直接减少 Prosperity，也不要在 B 高时给人口增长暗加一层乘数。风险通过 B>=H 恢复模拟验收。

### 6.5 UI 的“可用兵源”与经济有效户数

- Army 已有 `Available manpower = max(0, floor(H-B))` 是**招募余额 UI**，使用带符号 `GetRecruitmentCapacity()` 再显示截断，不是 H/K 容量。
- Politics 新 Hearth UI 是**每日人口发展**，包含各项增长；两者不要混为一谈或复用互相错误的数据字段。
- 村庄 fief `GetEffectiveSupplyHearth()` 的含义是村庄 H+MobilizedHearth、服务于供养检查；不能修改成 `H-B`。

---

## 7. 验收案例矩阵（Codex 应逐项给出结果）

### 7.1 数学 / XML（建议自动化）

| ID | 输入 / 场景 | 必须通过的断言 |
|---|---|---|
| H01 | 七文化配置完整性 | 7 IDs 唯一、没有缺 Culture、数值合法 |
| H02 | 帝国三级城镇 | `K=2800`，城堡三级 800、村庄 700 |
| H03 | 瓦兰迪亚村庄 | `K=900`，高于其他 6 文化村庄 |
| H04 | 诺德三级城镇 | `K=1000`，巴旦尼亚三级城镇 1200 |
| H05 | 城墙 1/2/3 | 容量依 40%/70%/100% 单调增长，initial<=K |
| H06 | 异文化统治 | 更换 owner/kingdom 文化不改变本地 K |
| H07 | H=K、G_raw>0 | 人口不超过 K，`CapacityAdjustment` 准确 |
| H08 | H>K 旧存档 | 不立即裁剪 K，有限时间内逐渐回落 |
| H09 | H=0 / K>0 | 自然项有限，`B/H` 处理不除零 |
| H10 | FoodChange=-20 | `G_food=-1.00`；其余项不受其影响 |
| H11 | FoodChange=+10/+100/-100 | `+0.50 / +2.00 / -2.00`，正确裁剪粮食项 |
| H12 | FoodChange<0 & total>0 | Hearth 最终仍正增长，不受负粮食项短路 |
| H13 | P=CH | `G_prosp=0` |
| H14 | CH<P<CK，超出≥500 | `G_prosp=+0.6`（候选值） |
| H15 | P=CK 或 P>CK | `G_prosp=0`；报告阈值阶跃 |
| H16 | 固定 H,K,P，仅增加 B | `P_dynamic`、`P_final` 不变，`P_support` 下降 |
| H17 | H=1200, K=2800, B=200, P=6500, FoodChange=-20 | `P_support=5000`,`P_dynamic=6000`,`P_final=14000`；`G_natural≈1.698`,`G_food=-1`,`G_prosp=0.6`,`G_burden=-0.14`,`G_total≈+1.158` |
| H18 | B=H/B>H | 经济有效 Hearth/P_support 为 0；招募余额仍可为负 |
| H19 | 输入非有限 / 不合法 K | 不进入存档，明确失败原因 |
| H20 | 相同输入多次调用 Forecast | 幂等、零状态变化、相同结果 |

### 7.2 游戏集成 / 存档 / Army 回归（必须游戏实测）

| ID | 场景 | 必须通过的断言 |
|---|---|---|
| H21 | 新游戏全文化城镇/城堡 | 初始 H 匹配所属文化/墙级；初始化一次 |
| H22 | 旧档跨新版 | Hearth/B、PopulationTransactionRecord、封邑兵、军费/驻军等无丢失 |
| H23 | 同一天重复 Settlement Tick / 重复打开 UI | Hearth/B 仅每日更新一次；UI 不写资源 |
| H24 | 同日职业兵成功征募 | 每名 H-1、B+Cost，仅成功提交改变资源；每日自然增长不重复 |
| H25 | 职业兵被拒 / 回滚 | H/B/候选与财务恢复现有事务保证；新增长模型不破坏 |
| H26 | B 恰好等于 / 大于 H | UI 可用兵源显示 0，经济支持为 0，P 软衰退，B 继续恢复 |
| H27 | 城镇有粮赤字 | UI 粮食为负、其他因素为正时实际净增长可正 |
| H28 | 达到 P_dynamic & 突破至 P_final | 促进窗口准确，不因单独 B 上升虚假触发 |
| H29 | 村庄被劫掠 | 原版劫掠人口/产出/恢复逻辑保持，文化村庄 K 生效 |
| H30 | 封邑兵动员→返乡→战死 | 原村庄 H/Mobilized/供养余额与旧事务兼容，不用 B |
| H31 | 攻城易主 / 城墙升级或降级 | 容量按原生文化和墙级变化，无存档即时裁剪 |
| H32 | 本体食品波动 / 高繁荣、低繁荣 | FoodChange 系统保留 Prosperity/34，不因新增长叠加粮食二次扣除 |
| H33 | 停战后恢复 84/168+ 天 | B 每日按文化消退，H、P 有回升路径，不出现无法恢复冻结 |
| H34 | 玩家/AI 全地图多个聚落 | 不增加全图逐 UI 事件刷新，不因唯一文化缺项静默退化 |
| H35 | 7 文化 × 2/3 村庄 × 封邑兵服役 | 不出现系统性供养负余额、日补员异常或村庄占用错算 |
| H36 | 地图战役日志 | 仅玩家王国范围聚合高频日志，名称可读、reason code 明确 |

**重要**：静态检查/数学测试只可以给 H01–H20 PASS；H21–H36 需要实际游戏证据，否则标记 `BLOCKED (runtime not executed)`，不可伪造。

### 7.3 长期平衡模拟（数据证明，而非先行改变规则）

至少各模拟 84 / 168 / 336 个游戏日（若游戏 1 年天数由 DLL 或当前版本配置决定，先确认后报告）：

- 和平发展：帝国/阿塞莱城镇能否增长至稳定范围；诺德/巴旦尼亚人口是否因低 K 过早归零。
- 持续征兵：帝国城市保持接近训练上限征兵、瓦兰迪亚城镇低速征兵时 H/B/P 的差异。
- 缺粮但有自然/繁荣促进：FoodChange=-10/-20，是否允许正人口增长、但增速合理。
- B≥H：繁荣度支持 0 造成的最坏衰退是否过度；`recovery=2` 是否能实际恢复。
- 旧档容量缩减：H 远大于新 K 时是否平滑下降、不瞬间毁掉封邑供养。
- 文化人口结构：瓦兰迪亚农村最大，帝国/阿塞莱城市最强，诺德/巴旦尼亚农村比重高但总体有限。村庄实际 H 受原版游戏数量/开局 Hearth 影响，不能仅凭 K 表声称保证实际比例。

每个案例建议输出 CSV 列：`day,culture,type,hearth,capacity,burden,foodChange,prosperity,natural,foodEffect,prosperityEffect,burdenEffect,capacityAdjustment,appliedDelta,supportProsperity`，再根据实际游戏日志与模拟核对。

---

## 8. 编译、发布与门禁（Windows）

> 本文作者未在用户游戏环境中执行以下命令，不能宣称 PASS。下面是 Codex **需要真实执行并回报输出** 的命令。先确认脚本与本地仓库版本匹配。

```powershell
# 在仓库根目录
dotnet build .\ModifiedArmy\ModifiedArmy.sln -c Release --nologo
dotnet build .\ModifiedPolitics\ModifiedPolitics.sln -c Release --nologo

# 现有静态架构发布门禁（编译两个模块，并核对 Army -> Politics 反向依赖）
pwsh -NoProfile -File .\ModifiedArmy\tools\Test-ArchitectureReleaseGate.ps1

# 组合发布检查；运行时证据可能阻塞，需要按真实状态报告
pwsh -NoProfile -File .\ModifiedArmy\tools\Invoke-ReleaseValidation.ps1

# 最终差异、未提交修改
git diff --check
git diff --stat
git status --short
```

如果系统没有 `pwsh` 但有 Windows PowerShell 5.1，可用相应 `powershell -NoProfile -File ...`，与项目可用环境一致。

验收必须分别报告：**Build Army / Build Politics / Architecture Gate / Pure Math Tests / New Save / Existing Save / Runtime T01–T24** 为 `PASS` / `FAIL` / `BLOCKED`。静态门禁通过不代表游戏运行已通过。

### 日志与本地化

- 重要人口结算至少能按 settlement name + secondary ID 定位，并显示 4 个因素与前后值。每日/结算阶段聚合，避免为全图每个 Tick 高频输出；按仓库现有 `ModLogger` 和玩家王国过滤。
- 玩家可见的标签 English fallback + 中文翻译；复查 `.xml` 的占位符对应；不要把中文直接写在 C#。
- 不因本设计修改其他 Mod 日志级别、AI 行军、玩家征兵预算等无关事项。

---

## 9. Codex 最终交付格式（强制）

完成任务时输出：

1. **基线信息**：仓库 branch、实际施工 HEAD、初始/最终 `git status`；如果实际 HEAD 和本文不同，列出差异及处理结果。
2. **变更清单**：文件级路径、关键类/方法、调整前后行为、保留边界、是否新增 SaveKey。
3. **参数表**：7 文化村/堡/镇 K、墙级系数、初始 Hearth、自然率、粮食、繁荣度、征兵项参数；写明哪些是测试值。
4. **数学证据**：H01–H20 逐项结果，特别是 H17 精确数值、阈值与非有限值。
5. **游戏证据**：H21–H36 的每项 `PASS/FAIL/BLOCKED`、游戏日期、存档、日志位置；不运行游戏不可标通过。
6. **发布门禁**：Army/Politics Release、架构、纯模型测试、旧档兼容、UI、本体 API 兼容结果。
7. **剩余风险**：B≥H 造成 P_support=0、P 接近 CK 时正向奖励阶跃、FoodChange 自反馈、村庄人口缩减导致封邑兵供养变化、每日事件次序。
8. **无额外操作**：除非用户明确授权，不得自行 commit、push、创建 PR 或覆盖未提交文件；只交付 diff 和报告。

---

## 10. 给 Codex 的可复制施工任务（简明版）

> 在 `GuihuaZhou/bannerlord` 当前工作树实施 `ModifiedPolitics` 的文化差异化 Hearth 容量及四项独立人口增长，保持 `ModifiedArmy` 现有职业兵征募与封邑兵系统。先读根 AGENTS、Army AGENTS、Army CODE_ORGANIZATION_RULES，核对 branch/HEAD/status。审查基线 HEAD `5dfdc81d04cf556b7ab9df4483486e0542598f1b`，若已更新以最新代码为准。严格保留现有 Hearth/B 人口事务、存档键、Army→Politics 禁止反向依赖。具体完成：
>
> 1. 按原生 Culture、定居点类型/墙级提供 K（七文化候选值在第 2 节），村庄 r=0.004，城镇/城堡 r=0.002。旧档 H>K 不得瞬间截断。
> 2. 城镇城堡每天用同一快照算独立四项 `Natural + clamp(FoodChange*0.05,-2,2) + ProsperityBonus + RecruitmentBurdenPenalty`，最终统一边界；不能用 FoodStocks；不能把因子做乘数。
> 3. 繁荣促进只能在 `H<K && CH<P<CK` 时生效，候选 `min(0.6, 0.6*(P-CH)/500)`；征兵压力候选 `-0.0003*K*clamp(B/max(H,1),0,1)`。四项候选参数 XML 可配置。
> 4. 经济有效 Hearth 改为 `max(0,H-B)`，保持征兵余额有符号 `H-B`、每名职业兵成功招募扣 1 H + 增加 Tier B，B 每天独立消退；Prosperity 通过 `C*E_econ` 的现有软上限/84 天衰退处理，不再另加直接扣 Prosperity。
> 5. 村庄只改文化 K 与自然增长计算，不用城镇 FoodChange/P/B，保留原版修正及封邑兵 MobilizedHearth；城镇 Food 模型不动。
> 6. 更新 Politics 人口 Overlay 为四项及实际汇总，修复 UI 仍只预测自然增长的问题；Army 已有职业可用兵源+逐村供养 UI，勿重做。
> 7. 依 P0～P6 分阶段检查，运行真实 Release、架构门禁、纯函数案例，制作游戏验收矩阵与长期模拟，不可把 BLOCKED 写成 PASS；未经授权不得 commit/push。

**此文档为实施方案，非代码已修改声明。**
