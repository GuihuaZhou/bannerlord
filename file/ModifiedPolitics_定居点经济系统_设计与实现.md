# ModifiedPolitics：定居点人口与经济系统——设计及实现方案

> **状态：设计定稿；尚未实施。** 2026-10-14  
> **目标仓库：** `GuihuaZhou/bannerlord`  
> **实施归属：** `ModifiedPolitics` 负责人口增长、农业生产与民用粮食供需规则；`ModifiedArmy` 继续负责兵源及驻军后勤。  
> 本文合并功能设计、跨模块架构、实现步骤与验收要求。文中的代码均为**待实现示意**，不代表当前仓库已有相应接口。

## 一、目标与边界

1. 村庄 `Hearth` 的**自然增长上限为 1200 户**。接近上限时，新增负向解释项“自然衰减”，使人口自然增长逐渐减慢。**不对旧存档、征兵返乡及其他来源的直接人口改动执行强制截断。**
2. `Hearth` 对**商品生产和村庄直接供粮**采用严格线性函数；**兵源保留现有的边际收益递增公式**。不再对农业生产施加渐近产量上限。
3. `FoodStocks` 代表城市粮食安全与供需平衡，不要求等价于实体粮仓库存。
4. 城镇和城堡统一提高繁荣度对应的民用粮食需求，采用 `Prosperity / 34`；**除数通过 ModifiedPolitics 的 XML 可配置**。
5. 居民市场食品消费计入 `FoodStocks` 的比例保持 **1:1**，不采用此前讨论的 `×0.8` 备选方案。
6. 玩家与 AI 使用相同经济规则；不重做商品消费、兵员征召或军事后勤系统。

## 二、确定的数学设计

### 2.1 村庄有效人口和原有基础增长

| 符号 | 说明 |
|---|---|
| `H` | 村庄当前实际 `Hearth` |
| `M` | `ModifiedArmy.FiefPartyManager.GetMobilizedHearth(village)`，在外服役封建兵对应的暂离人口 |
| `E = max(0, H + M)` | 用于判断自然增长空间的有效人口 |
| `Hmax = 1200` | 自然增长承载上限 |
| `R` | 当前 Mod 基础增长替换、以及本体技能／政策／建筑等修正计算完成后的每日户数变化 |
| `D` | 新增的“自然衰减”负项 |

保留现有的基础增长公式：

```text
BaseGrowth(E) = 4 / (1 + (E / 225)^3)
```

该公式用于替换本体的基础 Hearth 增长值；其他本体修正原则上继续生效。必须先完成此替换，再计算自然衰减。

### 2.2 自然衰减（四次方，最终结果修正）

```text
G(E) = max(0, 1 - (min(E, 1200) / 1200)^4)

if R <= 0:
    R_expected = R
else:
    R_expected = min(R * G(E), max(0, 1200 - E))

D = min(0, R_expected - R)
R_final = R + D
```

**适用条件：** 正常状态村庄参与此自然增长修正；非正常状态（例如劫掠）保留现有行为。仅 `D < 0` 时将其作为单独的 `ExplainedNumber` 条目，显示名称为**自然衰减**。

| 有效户数 E | 保留原正增长的比例 `G(E)` |
|---:|---:|
| 300 | 99.61% |
| 600 | 93.75% |
| 900 | 68.36% |
| 1000 | 51.77% |
| 1100 | 29.39% |
| 1200 及以上 | 0% |

含义：低人口时几乎不受影响，高人口时增长逐渐变慢；当 `E >= 1200` 时，不再产生每日**自然正增长**，但原本已经为负的增长仍然保持负值。若 `E` 已超限，**不自动扣除超额 Hearth**。

**为什么使用 `H + M`：** 在外服役的封建兵属于暂时离村人口；计算自然承载时计入他们，可以减少返乡时的重复人口增长。实际生产则不计入 `M`，使征兵产生经济损失。应验证征兵和返乡过程中 `H`、`M` 的维护是否一致。

### 2.3 村庄商品生产

取消现有渐近倍率：

```text
旧：ProductionMultiplier(H) = 2H / (H + 300)
新：ProductionMultiplier(H) = H / 300
```

```text
GoodsProduction = VillageTypeBaseProduction
                × ProductionMultiplier(H)
                × ExistingOtherModifiers
```

以上是**目标效果**，不是对 `ExplainedNumber` 具体因子叠加顺序的假定。建筑、技能、文化、村庄事务等原有增减益需要保留，且在相同额外条件下，商品产量应随实际 `Hearth` 成正比。

### 2.4 村庄粮食日产量与直接供粮

村庄粮食日产量所用的人口倍率，同样改为：

```text
ProductionMultiplier(H) = H / 300
```

附属村庄对城镇／城堡 `FoodStocks` 的直接供应为：

```text
VillageDirectFood(H) = 12 × (H / 300) = H / 25
```

两者是**不同的计算入口**：前者经 `VillageProductionCalculatorModel.CalculateDailyFoodProductionAmount()` 等机制影响村庄经济／仓储；后者在 `NewSettlementFoodModel.CalculateTownFoodStocksChange()` 中直接增加城镇／城堡的粮食平衡。实现时应同步修改、分别测试。

| 实际 Hearth | 商品人口倍率 | 每日直接供粮 |
|---:|---:|---:|
| 100 | 0.3333 | 4 |
| 300 | 1 | 12 |
| 600 | 2 | 24 |
| 900 | 3 | 36 |
| 1000 | 3.3333 | 40 |
| 1200 | 4 | 48 |

上述生产公式只使用实际 `H`，不使用 `M`。1200是**自然增长上限**而不是任意来源的人口硬上限，因此极端情况下实际 `H > 1200` 时，线性生产公式仍按实际 `H` 计算，不额外截断。

### 2.5 兵源供应：保留 ModifiedArmy 现有实现

当前仓库 `SettlementRecruitmentPoolBehavior.CalculateVillageSupply()` 使用：

```text
X = max(0, H + M - AssignedFiefCount)

Supply(X) = 0,                                      X <= 100
          = 0.03 × X × (X - 100) / (X + 50),        X > 100
```

`AssignedFiefCount` 表示现有系统分配给该村庄的封建兵占用量，按当前代码的分配方式计算。该公式在有效人数大于100的区间随 `X` 增加且边际收益递增；**本轮不修改它，也不额外乘以商品生产倍率**。

### 2.6 城镇与城堡的民用粮食需求：方案 B

```text
旧：ProsperityFoodConsumption = Prosperity / 40
新：ProsperityFoodConsumption = Prosperity / ProsperityConsumptionDivisor
默认：ProsperityConsumptionDivisor = 34
```

- **Town 和 Castle 统一采用除数34**，通过同一个 XML 参数管理。
- 对相同繁荣度，基础粮食需求相较除以40增加约 **17.65%**。
- 总督技能、水利工程等粮食消费修正继续保留；驻军消耗仍由军事后勤处理。
- 除数越小，基础消费越高；XML 数值不合法或 `<= 0` 时回退34。

| 繁荣度 | 旧基础消耗 ÷40 | 新基础消耗 ÷34 |
|---:|---:|---:|
| 2000 | 50 | 58.82 |
| 4000 | 100 | 117.65 |
| 6800 | 170 | 200 |
| 9489 | 237.23 | 279.09 |
| 10000 | 250 | 294.12 |

### 2.7 市场食品消费与粮食结算

`ItemConsumptionBehavior` 保留原有逻辑：根据需求、预算、价格和库存执行城镇居民消费，写入 `Town.SoldItems`。

```text
MarketFoodContribution = Σ SoldItems[类别具有 BonusToFoodStores 标记].Number
```

**维持每消费1单位食品即向 `FoodStocks` 贡献+1**，不做 `×0.8` 折算。不改变市场价格、库存扣减、贸易、围城紧急提粮或普通市场消费。

`FoodStocks` 的总体净变化继续由周边土地、附属村庄直接供粮、市场食品消费、建筑/政策/事务、繁荣度需求及军事粮仓划拨等原有机制组合产生；本次仅替换指定经济系数。

## 三、模块归属与依赖边界

### 3.1 责任划分

| 功能 | 主责模块 | 说明 |
|---|---|---|
| 自然人口增长、自然衰减、自然承载上限 | **ModifiedPolitics** | 定居点人口发展 |
| 商品生产和村庄直接供粮规则 | **ModifiedPolitics** | 农业与农村经济 |
| 城镇／城堡繁荣度粮食消耗除数及 XML | **ModifiedPolitics** | 民用粮食供需 |
| 在外封建兵的 `MobilizedHearth`、征召、返乡 | **ModifiedArmy** | 兵源与军事人口账目 |
| 军事粮仓、驻军库存、军事划拨 | **ModifiedArmy** | 军事后勤 |
| 唯一活跃的 `SettlementFoodModel` | **ModifiedArmy** 保留 | 避免重复注册与循环依赖 |

当前仓库已存在 `ModifiedPolitics -> ModifiedArmy` 的模块／程序集依赖；**不得新建反向的 `ModifiedArmy -> ModifiedPolitics` 引用**。

### 3.2 跨模块粮食模型接口（推荐实施方式）

现有 `ModifiedArmy.Garrison.Models.NewSettlementFoodModel` 不仅负责民用粮食结算，还接收 `GarrisonLocalProductionSupplyBehavior` 写入的军事粮仓划拨量。**不得简单整类迁走**。

建议将它保留为**唯一注册的 `SettlementFoodModel`**，在 `ModifiedArmy` 内新增极窄的、不依赖 `ModifiedPolitics` 类型的经济策略扩展点，由 `ModifiedPolitics` 在战役初始化时提供函数：

```csharp
// 位于 ModifiedArmy.NewSettlementFoodModel；示意接口，尚不存在。
public static Func<Village, float> VillageDirectFoodProvider { get; set; }
public static Func<int> ProsperityConsumptionDivisorProvider { get; set; }
```

使用方式：

```csharp
// ModifiedArmy 内部，旧规则作为 ModifiedPolitics 不存在时的后备。
public override int NumberOfProsperityToEatOneFood =>
    Math.Max(1, ProsperityConsumptionDivisorProvider?.Invoke() ?? 40);

// 正常村庄且非围城时：
float villageFood = VillageDirectFoodProvider?.Invoke(village)
    ?? (24f * hearth / (hearth + 300f));
```

`ModifiedPolitics` 初始化后提供：

```csharp
// ModifiedPolitics 内部：这些函数及配置管理器由本次实施创建。
NewSettlementFoodModel.VillageDirectFoodProvider =
    village => Math.Max(0f, village.Hearth) / 25f;

NewSettlementFoodModel.ProsperityConsumptionDivisorProvider =
    () => SettlementEconomyConfigManager.ActiveProsperityConsumptionDivisor;
```

实施要求：

1. **ModifiedArmy 无需引用 ModifiedPolitics**；经济规则可以独立置于 ModifiedPolitics。
2. 使用同一模型的 `NumberOfProsperityToEatOneFood` 属性供内部和其他游戏代码读取，避免仅在净粮食计算中临时扣差额导致模型内外不一致。
3. 保留既有村庄正常状态、围城、市场消费、建筑效果、事务效果、驻军划拨及执行顺序。
4. 在战役初始化／结束等适当时机重置静态提供者，防止跨战役残留；检查加载顺序（ModifiedArmy 先于 ModifiedPolitics），不得因未注册函数抛错。
5. 后续可以将 `Func` 封装为命名明确的 `RegisterEconomyProviders` / `ResetEconomyProviders` 方法，以减少任意覆写风险。上例仅表达边界与数据流。

**为什么不优先使用 Harmony 在最后调整净粮食差值：** 原粮食模型先计算产量与消耗，再应用建筑、技能与事务修正。末尾硬加差额容易绕过修正顺序；策略注入能在原公式位置替换计算，同时保存原有流程。

### 3.3 Harmony 补丁迁移

现有 `ModifiedArmy/ModifiedArmy/Fief/Patches/VillageEconomyPatches.cs` 含三类补丁：

- `VillageHearthGrowthPatch`
- `VillageProductionPatch`
- `VillageFoodProductionPatch`

它们属于经济规则，建议迁移到 `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Patches/`。**必须在 ModifiedArmy 中删除或禁用原补丁**，避免两个程序集对相同方法重复替换与重复衰减。原兵源 `FiefPartyManager` 保持不变，ModifiedPolitics 直接读取其数据。

### 3.4 现有农业官职补丁需要兼容性核查

`ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/OfficeSettlementEffectPatches.cs` 当前补丁目标为 `DefaultSettlementFoodModel.CalculateTownFoodStocksChange`；而实际启用的是 `NewSettlementFoodModel`。这可能使农业官职的粮食效果没有作用于当前实际模型。

实施时核对并修正作用目标、倍率及执行顺序，但**不得无意改变官职原本的平衡设计**；应单列回归测试。

## 四、XML 配置与初始化

### 4.1 XML 文件归属

建议新增：

```text
ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml
```

示例结构：

```xml
<?xml version="1.0" encoding="utf-8"?>
<SettlementEconomyConfigs>
  <SettlementEconomyConfig id="default">
    <Food>
      <!-- 繁荣度粮食消耗 = Prosperity / 此值；原值为40 -->
      <ProsperityConsumptionDivisor value="34" />
    </Food>
  </SettlementEconomyConfig>
</SettlementEconomyConfigs>
```

在 `ModifiedPolitics/SubModule.xml` 注册 `SettlementEconomyConfigs` 对应 XML；在 `ModifiedPolitics.Main.OnGameStart()` 中通过 `MBObjectManager.RegisterType<SettlementEconomyConfig>()` 和 `LoadXML("SettlementEconomyConfigs", true)` 加载。实现与当前模块的其他 XML 对象注册风格保持一致。

### 4.2 配置类

建议新增：

```text
ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs
```

继承 `MBObjectBase`，在 `Deserialize()` 中解析 `Food/ProsperityConsumptionDivisor`，默认值为34。还需配置管理器或简洁的静态访问入口，保证 XML 未加载、节点缺失、非数字、零及负数时均回退34。

本轮**只要求繁荣度除数可配置**，暂不把自然上限、四次方指数或生产倍率等额外暴露为配置项，不新增 MCM UI。

> 注意：原 `ModifiedArmy/ModuleData/modConfigs.xml` 仍属于军事模块，本轮不向它添加新的经济配置节点。

## 五、各功能具体实现

### 5.1 增长补丁：ModifiedPolitics

建议文件：

```text
SettlementEconomy/Patches/VillageHearthGrowthPatch.cs
```

补丁目标仍为 `DefaultSettlementProsperityModel.CalculateHearthChange()`，按以下顺序执行：

1. 正常村庄状态检查。
2. 从 `ModifiedArmy.FiefPartyManager` 获取 `M`，计算 `E = max(0,H+M)`。
3. 用 `BaseGrowth(E)` 替换原本体基础增长项，保留当前 Mod 的其他叠加规则。
4. 从 `__result.ResultNumber` 读取完成基础替换与其余修正之后的 `R`。
5. 当 `R > 0` 时计算 `G(E)`、`R_expected` 和 `D`；只把 `D < 0` 作为 `ExplainedNumber` 的负项添加，文本为本地化的“自然衰减”。
6. `R <= 0` 时不增加自然衰减；当 `E >= 1200` 时正增长归零，不强制扣人口。

逻辑示意：

```csharp
float growth = __result.ResultNumber;
if (growth > 0f)
{
    float effective = Math.Max(0f, village.Hearth + mobilized);
    float ratio = Math.Min(effective, 1200f) / 1200f;
    float retention = Math.Max(0f, 1f - MathF.Pow(ratio, 4f));
    float available = Math.Max(0f, 1200f - effective);
    float expected = Math.Min(growth * retention, available);
    float decay = Math.Min(0f, expected - growth);
    if (decay < 0f)
        __result.Add(decay, NaturalDecayText, null);
}
```

**已知实现细节：** 原补丁采用 `Postfix` 和 `Add(replacementBase - vanillaBase)`，这使本体百分比增益可能先作用于旧基础值，而不是新的基础增长值。本轮的自然衰减按**最终结果**计算；如果进一步要求基础增长百分比运算也严格符合新的基准，需单独设计前置替换或等价修正，不能未经验证就认定现有 `Postfix` 与理想数学模型完全一致。

### 5.2 商品／村庄粮食日产量补丁：ModifiedPolitics

建议文件：

```text
SettlementEconomy/Patches/VillageProductionPatches.cs
```

目标：

- `DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount()`：目标人口倍率 `H/300`。
- `DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount()`：目标人口倍率 `H/300`。

原版使用 `GetHearthLevel()`，其中商品的原倍率为 `(HearthLevel+1)×0.5`、村庄粮食日产量原倍率为 `HearthLevel+1`。迁移时应将其换为连续倍率，并保存其他技能、文化、建筑和事务效果。

**特别校验：** `ExplainedNumber.AddFactor()` 可能按加算百分比因子处理；直接沿用旧补丁“追加倍率差值”的方式，不一定能在其他生产增益存在时严格达到 `最终产量 ∝ H`。实施阶段要核验 `ExplainedNumber` 的运算语义，再选取在所有原有增益之后做精确比例修正或等价的方法，确保在相同其他条件下，300户／600户的产量比为1:2。不可直接覆盖掉其他生产增益或重复叠加。

### 5.3 粮食收支模型：ModifiedArmy 提供入口，ModifiedPolitics 提供规则

调整 `ModifiedArmy.Garrison.Models.NewSettlementFoodModel` 的两个**可注入**计算点：

1. 正常附属村庄的直接供粮：当 ModifiedPolitics 注入规则时使用 `H/25`；无注入时使用旧规则。
2. `NumberOfProsperityToEatOneFood` 除数：当 ModifiedPolitics 注入规则时取 XML 当前配置，默认34；无注入时恢复现有40。

其他全部保持：

- Town／Castle 均使用同一除数。
- `Town.SoldItems` 中食品消费保持 `+Number`，**不乘0.8**。
- `GarrisonLocalProductionSupplyBehavior` 与 `SetLocalProductionAllocation()` 的军事粮食划拨继续工作。
- 水利工程、总督技能、建筑粮食生产、事务及围城行为不改变。

### 5.4 本地化与说明

在 `ModifiedPolitics` 的简体中文语言文件中增加“自然衰减”及对应英文回退文本。确保 `CalculateHearthChange(village, includeDescriptions:false)` 和 `includeDescriptions:true` 的**数值相同**，只是后者有解释条目。

## 六、建议实施顺序

1. **确立边界：** 保留 ModifiedArmy 的唯一 `NewSettlementFoodModel`，设计并测试可注入经济策略接口；确保没有反向程序集引用。
2. **迁移已有经济补丁：** 将三类 `VillageEconomyPatches` 移至 ModifiedPolitics，并从 ModifiedArmy 移除原 Harmony 补丁，先验证现有行为不变且不会双重生效。
3. **增加 ModifiedPolitics XML：** 加载34，验证40、缺失、非法值及旧存档。
4. **应用村庄经济新公式：** 实现生产 `H/300`、直接供粮 `H/25`、自然衰减以及本地化。
5. **应用粮食方案B：** Town／Castle `Prosperity/34`，保持市场食品消费1:1及军事粮仓流程。
6. **检查跨模块兼容：** 核实农业官职 `OfficeSettlementEffectPatches` 与真实粮食模型的关联，测试效果与顺序。
7. **回归与连续观测：** 编译 `ModifiedArmy`、`ModifiedPolitics`，测试新旧存档以及玩家和 AI 定居点，使用达努斯缇卡存档做多日收支观察。

## 七、验收标准

| 场景 | 预期 |
|---|---|
| 自然人口 `E=600` | 原每日正增长保留93.75%（尚未触及剩余容量限制） |
| 自然人口 `E=1000` | 原每日正增长保留约51.77% |
| `E >= 1200`，原增长 `R > 0` | 最终每日自然增长为0，明细出现负值“自然衰减” |
| 原增长 `R <= 0` | 不额外叠加自然衰减，不截断现有人口 |
| `H=300 / 600 / 1000 / 1200` | 直接供粮 `12 / 24 / 40 / 48` |
| 相同额外增益、`H=300 → 600` | 商品实际日产量随人口翻倍（需验证因子顺序） |
| `Prosperity=6800` | Town、Castle 基础粮食消耗皆为200（不计技能建筑） |
| XML 除数改为40 | Town、Castle 基础消耗恢复为170 |
| XML 缺失、零、负数或非法文本 | 使用除数34，不抛异常、不除零 |
| 市场食品消费100 | 对 FoodStocks 贡献仍为+100 |
| 未启用 ModifiedPolitics | ModifiedArmy 的现有粮食及后勤模型能使用旧规则独立运行 |
| 同时启用两个模块 | 经济公式只生效一次；仅注册一个 SettlementFoodModel；无程序集循环依赖 |
| 驻军粮仓／围城／被劫掠村庄 | 保留原有军事划拨与停产行为 |
| 农业官职加成 | 对当前实际模型的生效情况明确且可复核 |

### 连续性测试

使用现有达努斯缇卡存档，逐日记录 `H`、`M`、村庄商品产量、村庄直接供粮、`Town.SoldItems` 食品消费、繁荣度消耗、军事划拨、`FoodStocks` 及繁荣度变化。**不得使用单日静态表直接推断长期经济平衡**，因为商品供给和市场价格改变后，居民实际消费可能随之变化。

## 八、不在本轮实施的事项

- 不强制把任何途径产生的村庄人口裁剪到1200。
- 不修改征召士兵的 Hearth 消耗、返乡恢复或兵源供应公式。
- 不重构 `ItemConsumptionBehavior` 的消费预算、价格和库存系统。
- 不使用市场食品消费 `×0.8` 备选方案。
- 不新增 MCM 控制项，不修改 ModifiedArmy 原有平衡 XML 以承担新的经济配置。
- 不创建新的 `SettlementFoodModel` 与现有模型争夺注册顺序。

## 九、参考源码（仓库当前路径）

- [ModifiedArmy：村庄经济补丁](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Patches/VillageEconomyPatches.cs)
- [ModifiedArmy：民用粮食模型](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Models/NewSettlementFoodModel.cs)
- [ModifiedArmy：驻军本地产量划拨](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Behaviors/GarrisonLocalProductionSupplyBehavior.cs)
- [ModifiedArmy：封邑兵人口记录](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [ModifiedArmy：兵源供应](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementRecruitmentPoolBehavior.cs)
- [ModifiedPolitics：模块初始化](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/Main.cs)
- [ModifiedPolitics：农业官职补丁](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/OfficeSettlementEffectPatches.cs)
- [本体：村庄生产模型](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultVillageProductionCalculatorModel.cs)
- [本体：城镇市场消费](https://github.com/GuihuaZhou/bannerlord/blob/main/code/ItemConsumptionBehavior.cs)

---

**最终结论：** 经济公式与配置归 `ModifiedPolitics`；在役人口数据、兵源和军事后勤归 `ModifiedArmy`；`NewSettlementFoodModel` 仍是唯一有效的粮食模型，通过单向扩展点接收经济规则。**本文件是设计和实现指导，不代表已经修改代码。**
