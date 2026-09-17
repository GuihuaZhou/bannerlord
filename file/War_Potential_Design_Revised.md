# 战争潜力（War Potential）设计

战争潜力表示 Clan
**当前能打多强，以及战争能够持续多久**，由实际战力、持续战力和财政能力组成。

## 1. 实际战力

只有已经组成 Clan Party 的野战部队计算为当前实际战力：

``` text
FieldTroops = Σ Clan Party 当前士兵数量

CurrentMilitaryPower = FieldTroops
```

驻军和 Fief 军队不计入当前实际战力。

## 2. 持续战力

驻军与 Fief 军队作为 Clan 的**兵员池**：

``` text
TroopPool = GarrisonTroops + FiefTroops
```

兵员池能否转化为野战军，受可统兵 Hero 数量限制：

``` text
AvailablePartyHeroes =
    当前能够创建 / 指挥 Clan Party 的 Hero 数量
```

定义 Hero 的动员能力：

``` text
HeroMobilizationCapacity =
    AvailablePartyHeroes × AveragePartyCapacity
```

有效后备力量：

``` text
EffectiveReserve =
    min(TroopPool, HeroMobilizationCapacity)
```

因此，同样拥有 1000 兵员池，能够创建 3 支 Party 的
Clan，其持续战力明显高于只能创建 1 支 Party 的 Clan。

## 3. 财政持续能力

战争期间只将 **Clan Party 士兵工资 ×2**，驻军工资不变：

``` text
WarWageMultiplier = 2

WarDailyWage =
    PartyDailyWage × WarWageMultiplier

WarDailyBurn =
    max(0, WarDailyWage - DailyIncome)

FinancialEndurance =
    ClanWealth / WarDailyBurn
```

定义：

``` text
FinancialFactor =
    min(FinancialEndurance / ReferenceWarDays, 1)

ReferenceWarDays = 60
```

## 4. 最终战争潜力

持续战力：

``` text
SustainedMilitaryPower =
    EffectiveReserve × FinancialFactor
```

最终：

``` text
WarPotential =
    CurrentMilitaryPower
    + SustainedMilitaryPower
```

展开：

``` text
WarPotential =
    FieldTroops
    +
    min(
        GarrisonTroops + FiefTroops,
        AvailablePartyHeroes × AveragePartyCapacity
    )
    × FinancialFactor
```

核心逻辑：

> **野战兵力 = 实际战力；Fief/驻军 = 兵员池；可统兵 Hero =
> 兵员转化能力；财政 = 战争持续能力。**

`Prosperity`、`Households`
不直接计入公式，它们通过征兵消耗、收入下降和补员速度下降自然影响后续战争潜力。
