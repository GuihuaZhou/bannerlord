# 《骑马与砍杀 II：霸主》本体经济机制分析

> 目的：整理原版经济系统的主要计算关系及当前 `ModifiedArmy` 已经改变的部分，为后续 Mod 设计提供依据。  
> 范围：Hearth（户数）、村庄生产与贸易、粮食库存、Prosperity（繁荣度）、Loyalty（忠诚度）、Security（安全度）、税收、民兵。  
> 说明：以下以项目仓库中保存的本体反编译代码为依据；原版数值和当前 Mod 行为分别标明。游戏版本或其他 Mod 覆盖相关模型时，实际结果可能有所不同。

## 1. 经济关系概览

```text
村庄 Hearth（户数 / 生产能力）
    ├─→ 村庄商品产出 → 村民商队 → 城镇市场库存
    │                              ├─→ 居民消费粮食商品 → FoodChange
    │                              ├─→ 居民消费繁荣商品 → Prosperity 增长
    │                              └─→ 贸易税 / 村庄收入 / 城镇关税
    ├─→ 附属村庄直接供粮 → FoodChange → FoodStocks（粮食库存）
    │                                  ├─→ 满仓溢出 → Prosperity 增长
    │                                  └─→ 粮仓耗尽、持续赤字 → 饥饿
    │                                                    ├─→ Prosperity 下降
    │                                                    └─→ Loyalty 下降
    ├─→ 村庄民兵增长
    └─→ 部分建筑的附属村庄户数收入

Prosperity → 居民粮食消耗、城镇/城堡基础税收、城镇/城堡民兵增长、市场需求
Loyalty / Security / 政策 / 建筑 / Perk / Issue → 影响增长、收入及社会秩序
```

上述为传导关系，**并非每一条箭头都意味着立即、必然、线性的变化**。市场库存、贸易运输、粮食库存和各类阈值会延迟或抵消局部冲击。

## 2. Hearth：村庄户数

### 2.1 本体每日变化

`DefaultSettlementProsperityModel.CalculateHearthChange()` 的正常村庄**基础**增长为：

$$
BaseHearthGrowth(H)=
\begin{cases}
4,&H<300\\
1.2,&300\le H<600\\
0.2,&H\ge600
\end{cases}
$$

其他修正：

| 来源 | 本体影响 |
|---|---|
| 村庄遭劫掠后处于 `Looted` 状态 | 不再获得正常状态的基础增长，另有每日 `−1` |
| `Grazing Rights` 政策 | 每日 `−0.25` |
| 灌溉项目、建筑 `VillageHeartsPerDay` | 增加每日 Hearth 变化 |
| `BushDoctor`、`Energetic`、`AidCorps` 等 Perk | 根据具体生效条件修正增长 |
| 帝国村庄 Hearth 文化特性 | 满足条件时对结果添加比例修正 |
| 村庄事务（Issue） | 按具体事务效果修正 |
| 村民商队补员、征兵或其他直接操作 | 可能直接改变 `Hearth`，不经过日增长公式 |

`Village.DailyTick()` 每天将 `HearthChange` 加到 Hearth 上；本体在每日更新时将 Hearth 最低限制为 **10**。本体不设统一的 Hearth 最大上限。

### 2.2 原版 HearthLevel

| Hearth | `GetHearthLevel()` |
|---|---:|
| `H < 200` | 0 |
| `200 ≤ H < 600` | 1 |
| `H ≥ 600` | 2 |

### 2.3 Hearth 影响哪些机制

- 村庄各类商品生产（原版采用 HearthLevel 分档系数）。
- 附属村庄提供给城镇或城堡的直接粮食（原版采用 HearthLevel 分档）。
- 村庄民兵自然增长（包含 `H / 400`）。
- 村庄贸易收入、税收（通过真实产出和销售**间接**影响）。
- 部分建筑的 `DenarByBoundVillageHeartPerDay` 收入。
- 村民商队的补员条件与供货活动。
- **不直接进入**原版每日 Prosperity 变化公式。

**来源**：[`DefaultSettlementProsperityModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementProsperityModel.cs)、[`Village.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/Village.cs)。

## 3. 村庄商品生产与贸易

### 3.1 原版商品生产

`DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount()` 的关键系数：

$$
Production_i=BaseProduction_i\times\frac{HearthLevel+1}{2}\times OtherModifiers_i
$$

| Hearth | 原版生产系数 |
|---|---:|
| 小于 200 | 0.5 |
| 200–599 | 1.0 |
| 600 及以上 | 1.5 |

`OtherModifiers_i` 概括文化、相关 Perk、建筑生产加成、事务等；实际代码中可能包含加法修正，并非所有因素都简单相乘。

### 3.2 商品流入市场

典型链路：

```text
Village 生产商品 → 村庄库存 → VillagerCampaignBehavior
→ 村民商队装货 → TradeBound 城镇出售 → 城镇市场库存
→ ItemConsumptionBehavior 按需求、价格及实际库存消费商品
```

必须区分：

- `Village.Bound`：村庄在行政上隶属的城镇或城堡。
- `Village.TradeBound`：村民商队销售商品的目标城镇。

因此，**城堡附属村庄也可以向其他城镇市场供货**。玩家与其他商队也可能改变实际库存；道路受阻、商队被劫掠会降低实际到货。

### 3.3 市场供需

本体 `ItemConsumptionBehavior` 先取得类别需求与预算，再根据商品价格、库存进行实际消费，并记录到 `Town.SoldItems`。因此：

- 市场**库存**不等于每日实际**消费**。
- 村庄减产不保证当天市场消费立刻下降；库存与其他商队可以缓冲。
- 市场消费受 Prosperity、类别需求、物价、库存等因素共同影响。

**来源**：[`DefaultVillageProductionCalculatorModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultVillageProductionCalculatorModel.cs)、[`VillagerCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/VillagerCampaignBehavior.cs)、[`ItemConsumptionBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/ItemConsumptionBehavior.cs)。

## 4. FoodChange 与 FoodStocks

### 4.1 本体粮食模型

基本关系：

$$
FoodChange=FoodProduction-FoodConsumption
$$

本体主要项目：

| 项目 | 数值 / 机制 |
|---|---|
| 本地土地供粮 | Town `+15` / Castle `+10`（未被围城时） |
| 附属村庄直接供粮 | 正常状态的每座村庄 `(HearthLevel + 1) × 6` |
| 市场供粮 | `Town.SoldItems` 中实际消费、且类别具有 `BonusToFoodStores` 属性的商品数量 |
| 居民消耗 | `Prosperity / 40` |
| 本体驻军消耗 | 驻军人数 `/ 20`，另可受 Perk 等修正 |
| 建筑、政策、事务等 | 可增减粮食产出或消耗 |

**当前 `ModifiedArmy` 已改变这一点**：启用 `NewSettlementFoodModel`，民用 `FoodStocks` 不再扣除驻军的粮食消耗。军队改由自身物资体系供粮；被划给军事粮仓的部分本地粮食产量从民用产量中扣除。

故当前 Mod 的结构可概括为：

$$
\begin{aligned}
FoodChange={}&LocalFood+BoundVillageFood+MarketFood\\
&+BuildingFood+PolicyAndOtherFood\\
&-MilitaryAllocation-ProsperityFoodConsumption
\end{aligned}
$$

### 4.2 库存上限与饥饿

- Town 默认粮食库存上限：**300**。
- Castle 默认上限：**450**（300 + 150）。
- 建筑可以增加上限。
- 每日 `FoodStocks += FoodChange`，结果限制在 `[0, FoodStocksUpperLimit]`。
- `Settlement.IsStarving` 依照 `Town.FoodStocks <= 0` 判断。

要区分两种缓冲：

1. **市场实物库存**：尚未被消费的粮食商品。
2. **FoodStocks**：定居点自身粮食储备。

市场中存在大量粮食商品，不意味着它们已经全部加入 FoodStocks。

**来源**：[`DefaultSettlementFoodModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementFoodModel.cs)、[`NewSettlementFoodModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Models/NewSettlementFoodModel.cs)、[`Town.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/Town.cs)、[`Settlement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/Settlement.cs)。

## 5. Prosperity：城镇 / 城堡繁荣度

### 5.1 日增长组成

`DefaultSettlementProsperityModel.CalculateProsperityChange()` 包含：

$$
\begin{aligned}
\Delta P={}&Housing+FoodSurplus+FoodShortage\\
&+MarketGoods+LoyaltyEffect+BuildingEffect\\
&+PerkEffect+PolicyEffect+IssueEffect
\end{aligned}
$$

说明：某些 Perk 使用 `AddFactor`，是对当时累积结果的比例调整，不严格等同于所有项互相独立加总。

### 5.2 Housing Costs（仅 Town）

| 当前 Town Prosperity | 每日修正 |
|---|---:|
| `P < 250` | +6 |
| `250 ≤ P < 500` | +5 |
| `500 ≤ P < 750` | +4 |
| `750 ≤ P < 1000` | +3 |
| `1000 ≤ P < 1250` | +2 |
| `1250 ≤ P < 1500` | +1 |
| `1500 ≤ P ≤ 6000` | 0 |
| `6000 < P ≤ 9000` | −1 |
| `9000 < P ≤ 12000` | −2 |
| `12000 < P ≤ 15000` | −3 |
| `15000 < P ≤ 18000` | −4 |
| `18000 < P ≤ 21000` | −5 |
| `P > 21000` | −6 |

**Castle 不使用这段 Town 专属的 Housing Costs 分档修正。**

### 5.3 粮食溢出

$$
FoodSurplusEffect=
0.1\max\left(0,\operatorname{Trunc}(FoodStocks+FoodChange)-FoodCapacity\right)
$$

`FoodCapacity` 为 `FoodStocksUpperLimit()`；`Trunc` 表示 C# 转 `int` 的向零截断。只有**计算中的粮食超出库存上限**，才获得这项 Prosperity 增长。

### 5.4 饥饿损失

在定居点**已处于饥饿状态**且 `FoodChange < 0` 时：

$$
StarvationEffect=0.5\times\operatorname{Trunc}(FoodChange)
$$

否则此项为 0。`HelpingHands` 等 Perk 可能减轻损失。

**粮食赤字但库存尚未耗尽时，不会直接触发这项饥饿惩罚。**

### 5.5 市场商品（仅 Town）

$$
MarketProsperityEffect=0.1\times GoodsConsumed_{BonusToProsperity}
$$

`GoodsConsumed` 为当日实际消费、带 `BonusToProsperity` 属性的商品数量；**不是所有村庄产量，也不是全部市场库存**。Castle 无此项。

### 5.6 Loyalty 与其他修正

| 条件 | 每日 Prosperity 影响 |
|---|---:|
| `Loyalty > 75` 且 `FoodChange > 0` | +0.5 |
| `Loyalty ≤ 25` | −1 |
| 日常 Housing 建筑项目 | +2 |
| 总督 `Pristine Streets` Perk | +1 |
| 总督 `Trickle Down`，满足建设条件 | +2 |
| `Clean Infrastructure` | 对符合条件的建筑循环应用加成 |
| `Apprenticeship` | 满足条件且当前累计 Prosperity 变化为正时，按建筑数量添加比例因子 |
| Town 的 `Road Tolls` | −0.2 |
| Town 的 `Imperial Towns`，且由统治家族持有 | +1 |
| Town 的 `Crown Duty` | −1 |
| Town 的 `War Tax` | −1 |
| Issue | 根据当前事务类型和状态修正 |

### 5.7 ProsperityLevel

Town 与 Castle 均使用以下分档：

| Prosperity | `ProsperityLevel` |
|---|---|
| `P < 2000` | Low |
| `2000 ≤ P < 5000` | Mid |
| `P ≥ 5000` | High |

**结论**：Hearth 不直接改变 Prosperity；必须经由生产、粮食、市场消费、饥饿等实际经济渠道发生作用。

**来源**：[`DefaultSettlementProsperityModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementProsperityModel.cs)、[`Town.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/Town.cs)。

## 6. Loyalty：忠诚度

影响因素：饥饿、领主与总督文化、Security、政策、建筑与项目、Perk、Issue、重要人物关系、自动回归机制。

### 6.1 饥饿惩罚

$$
StarvationLoyaltyEffect=
\begin{cases}
0,&未饥饿\\
-1,&饥饿且持续时间\le14天\\
-2,&饥饿持续时间>14天
\end{cases}
$$

### 6.2 向 50 回归

$$
LoyaltyDrift=-0.1(Loyalty-50)
$$

例如 Loyalty=30 时回归修正为 `+2/天`。所以饥饿会提供负修正，但**不保证忠诚度一定降至25以下**；结果取决于其他修正的合计。

**来源**：[`DefaultSettlementLoyaltyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementLoyaltyModel.cs)。

## 7. Security：安全度

Security 是独立的结算指标，主要通过下列渠道参与经济：

- 对 Loyalty 的每日变化产生正负修正：本体 Loyalty 模型以 `Security=50` 为中点，低安全度为负，高安全度为正。
- 在达到相应高、低安全度阈值时，通过税收模型影响税收奖励或腐败损失。
- 还可影响其他定居点机制，但**Security 不直接出现在上述 Prosperity 日增长主公式中**。

其实际变动还会受驻军、政策、总督 Perk、建筑和其他游戏行为影响。该部分不是本次 Hearth/征兵模型的直接修改目标。

**来源**：[`DefaultSettlementLoyaltyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementLoyaltyModel.cs)、[`DefaultSettlementTaxModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementTaxModel.cs)。

## 8. 税收、关税、家族收入

### 8.1 城镇 / 城堡基础税收

`DefaultSettlementTaxModel` 计算的基础值：

$$
BaseTownTax=0.35\times Prosperity
$$

| Prosperity | 基础税收 / 天 |
|---|---:|
| 1000 | 350 |
| 4000 | 1400 |
| 8000 | 2800 |

之后叠加政策、文化、Security、Loyalty、建筑、Perk、Issue 等修正。

- 高忠诚、高安全度可能增加收益。
- 中低忠诚或安全度可能导致腐败性损失。
- 当 `Loyalty < 25`，税收模型会对**当时累计值**增加 `−100%` 因子；之后仍有建筑相关收入项，因此不能概括为“最终所有收入绝对为零”。
- 本体 `SettlementCommissionRateTown` 为 `0.7`，用于特定税收比例计算，**不可与 `0.35 × Prosperity` 的基础税值混为一谈**。

### 8.2 村庄收入

村民商队出售商品取得收入，返回村庄时按村庄税收比例形成 `Village.TradeTaxAccumulated`。本体按平滑系数提取：

$$
VillageIncome_{base}=
\left\lfloor\frac{TradeTaxAccumulated}{5}\right\rfloor
$$

劫掠状态、政策、总督 Perk 等仍可修正实际收入；计算中还存在整数截断。

因此 **Hearth 下降 → 商品减产 → 实际贸易收入可能下降 → 村庄税收可能下降**，无需 Prosperity 先下降。

### 8.3 城镇关税和建筑收入

- 城镇关税来自 `Town.TradeTaxAccumulated`，同样按 `RevenueSmoothenFraction() = 5` 进行平滑提取并应用修正。
- 建筑项目可能产生 `TaxPerDay`、`TariffIncome` 等收益。
- 部分建筑的 `DenarByBoundVillageHeartPerDay` 直接与附属村庄 Hearth 有关。
- 家族收入还包括非领地来源，不能把总家族收入等同于城镇基础税收。

**来源**：[`DefaultSettlementTaxModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementTaxModel.cs)、[`DefaultClanFinanceModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultClanFinanceModel.cs)、[`VillagerCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/VillagerCampaignBehavior.cs)。

## 9. 民兵

本体主要人口相关项：

$$
VillageMilitiaGrowth\supseteq H/400
$$

$$
FortificationMilitiaGrowth\supseteq Prosperity/1000
$$

还存在基础增长、现有民兵的自然衰减、政策、建筑、市场消费等因素。当前 `ModifiedArmy` 已使用 `NewSettlementMilitiaModel`，其中仍保留上述 Hearth 和 Prosperity 分项，但对封邑部队与民兵之间的计算关系另有修改。

**来源**：[`DefaultSettlementMilitiaModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementMilitiaModel.cs)、[`NewSettlementMilitiaModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/NewSettlementMilitiaModel.cs)。

## 10. 经济系统的主要反馈与结论

### 10.1 粮食承载能力

居民日消耗约为：

$$
FoodConsumption=P/40
$$

若每日可持续粮食供应为 $S$，则忽略其他变量时的**静态供需平衡规模**为：

$$
P_{food\ balance}\approx40S
$$

例：每日稳定供粮由100降至90，则粮食对应的平衡规模从约4000降至约3600。**这不是实际 Prosperity 必然停在3600的预测**；住房、建筑、市场需求、Perk 等仍影响动态结果。

### 10.2 两类经济冲击

- **粮食冲击**：Hearth 减少 → 直接粮食和粮食商品供应减少 → 库存消耗 → 饥饿时 Prosperity、Loyalty 下降 → Prosperity 降低又减少粮食消耗。
- **贸易冲击**：Hearth 减少 → 商品供给和成交量可能下降 → 村庄收入、城镇关税，以及 Town 的部分 Prosperity 增长可能下降。

### 10.3 本体规则提供的合理差异

- 农业依赖型城镇更容易受到本地 Hearth 损失影响。
- 贸易发达型城镇可能通过外部供粮和库存度过局部减产。
- Castle 没有 Town 的市场繁荣商品加成，更依赖直接供粮、建筑等途径。
- **不需要强制每次征兵都使 Prosperity 下降**；是否发生粮食危机应由真实供需决定。

---

## 主要源码索引

| 文件 | 机制 |
|---|---|
| [`code/DefaultSettlementProsperityModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementProsperityModel.cs) | Hearth / Prosperity 每日变化 |
| [`code/Village.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/Village.cs) | Hearth、等级、日结算、Bound/TradeBound |
| [`code/Town.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/Town.cs) | Prosperity、FoodStocks、Loyalty 日结算 |
| [`code/DefaultVillageProductionCalculatorModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultVillageProductionCalculatorModel.cs) | 村庄商品与粮食生产 |
| [`code/DefaultSettlementFoodModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementFoodModel.cs) | 本体粮食变化 |
| [`ModifiedArmy/.../NewSettlementFoodModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Models/NewSettlementFoodModel.cs) | 当前 Mod 的民用粮食模型 |
| [`code/ItemConsumptionBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/ItemConsumptionBehavior.cs) | 市场消费、SoldItems |
| [`code/VillagerCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/VillagerCampaignBehavior.cs) | 村民商队运货、出售、村庄税额 |
| [`code/DefaultSettlementLoyaltyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementLoyaltyModel.cs) | Loyalty 与饥饿 |
| [`code/DefaultSettlementTaxModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementTaxModel.cs) | 城镇税收、政策和腐败 |
| [`code/DefaultClanFinanceModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultClanFinanceModel.cs) | 村庄、城镇关税、建筑及家族收入 |
| [`code/DefaultSettlementMilitiaModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementMilitiaModel.cs) | 民兵增长 |
