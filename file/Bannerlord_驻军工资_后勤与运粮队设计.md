# 驻军工资、后勤与运粮队设计

## 1. DesiredGarrisonSize

驻军目标人数使用固定值：

```text
城堡：
腹地 = 100
边境 = 250

城镇：
腹地 = 200
边境 = 400
```

即：

```text
CastleBaseGarrison = 100
CastleBorderBonus = 150

TownBaseGarrison = 200
TownBorderBonus = 200
```

公式：

```text
DesiredGarrisonSize
=
BaseGarrisonSize
+ BorderBonus
```

---

## 2. 驻军工资上限

驻军工资上限：

```text
GarrisonWagePaymentLimit
=
DesiredGarrisonSize
× ExpectedAverageTroopWage
× ClanLeaderGoldFactor
```

其中 `ClanLeaderGoldFactor` 继续使用本体基于 Clan 领袖 `Gold` 的修正。

不再使用：

```text
SettlementProsperityFactor
FoodPotentialFactor
```

原则：

```text
DesiredGarrisonSize = 战略需求
ClanLeader.Gold = 财政约束
粮食 = 后勤约束
```

> 本体修改：`ClanVariablesCampaignBehavior.UpdateClanSettlementsPaymentLimit()`。  
> 保留 `FactionHelper.OwnerClanEconomyEffectOnGarrisonSizeConstant()`，移除繁荣和 FoodPotential 对工资上限的影响。

---

## 3. 驻军粮仓

直接使用：

```text
GarrisonParty.ItemRoster
```

作为定居点军事粮仓，不新增独立粮仓数据。

粮仓保存真实 Food Item。

驻军不再消耗 `FoodStocks`，而是消耗：

```text
GarrisonParty.ItemRoster
```

中的真实食物。

---

## 4. 全局粮食消耗修改

将本体：

```text
NumberOfMenOnMapToEatOneFood = 20
```

修改为：

```text
NumberOfMenOnMapToEatOneFood = 10
```

因此基础粮耗公式变成：

```text
DailyFoodConsumption
=
(NumberOfAllMembers + NumberOfPrisoners / 2) / 10
```

300 人部队：

```text
30 粮食/天
```

对应：

```text
15天 = 450粮食
30天 = 900粮食
45天 = 1350粮食
```

该修改适用于所有正常使用 `MobilePartyFoodConsumptionModel` 的部队。

> 本体修改：`DefaultMobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood`，由 `20` 改为 `10`。

---

## 5. 驻军与 FoodStocks 分离

驻军不再参与 `FoodStocks` 的粮食消耗。

```text
FoodStocks
= 定居点民生粮食

GarrisonParty.ItemRoster
= 驻军军事粮仓
```

驻军每日直接消耗军事粮仓中的真实食物。

> 本体修改：`DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood()`，允许驻军走 Party 食物消费。  
> 同时从 `SettlementFoodModel.CalculateTownFoodStocksChange()` 中移除驻军对 `FoodStocks` 的影响。该具体实现仍需从 dnSpy 补出。

---

## 6. 后勤状态

定义：

```text
FoodSupplyDays
=
CurrentGarrisonFood
/
GarrisonDailyFoodConsumption
```

阈值：

```text
15天 = 紧急
30天 = 补给阈值
45天 = 目标库存
```

目标粮食：

```text
TargetFoodAmount
=
GarrisonDailyFoodConsumption × 45
```

缺口：

```text
MilitaryFoodDeficit
=
max(
    0,
    TargetFoodAmount - CurrentGarrisonFood
)
```

---

## 7. 本地征粮

当：

```text
FoodSupplyDays < 30
```

驻军首先从本地 `FoodStocks` 强制征粮。

```text
MilitaryFoodTransfer
=
min(
    FoodStocks,
    MilitaryFoodDeficit
)
```

征粮不保留民生底线：

```text
FoodStocks 可以被征至 0
```

军队缺粮时优先保证驻军，民生后果继续由本体粮食系统承担。

---

## 8. 城镇本地采购

如果是城镇，本地征粮后仍然缺粮，则从：

```text
Settlement.ItemRoster
```

购买真实 Food Item，补入：

```text
GarrisonParty.ItemRoster
```

城镇补给顺序：

```text
本地FoodStocks征粮
→ 本地市场购买
→ 运粮队
```

城堡没有市场：

```text
本地FoodStocks征粮
→ 运粮队
```

---

# 运粮队

## 9. 基本定义

统一名称：

```text
运粮队
```

代码：

```text
SupplyParty
SupplyPartyComponent
SupplyPartyBehavior
```

每个城镇或城堡最多同时存在一支运粮队。

平时不存在，只有缺粮时才创建。

---

## 10. 继承结构

正式确定：

```csharp
SupplyPartyComponent : PartyComponent
```

不继承：

```text
CaravanPartyComponent
VillagerPartyComponent
WarPartyComponent
```

实现时：

```text
Party结构
→ 参考村民队

市场购买
→ 参考商队

实际组件
→ 独立SupplyPartyComponent
```

---

## 11. 运粮队不需要 Hero

运粮队：

```text
Leader = null
PartyOwner = HomeSettlement.OwnerClan.Leader
```

因此：

```text
不需要Hero
不占Hero统兵数量
不属于Clan WarParty
不加入Army
不计入FieldTroops
```

---

## 12. 运粮队兵力

创建时从驻军抽调护卫：

```text
GarrisonParty.MemberRoster
→ SupplyParty.MemberRoster
```

第一版使用固定护卫数量：

```text
SupplyEscortSize = 30
```

例如：

```text
驻军300
→ 驻军270
→ 运粮队30
```

不凭空增加兵力。

---

## 13. 运粮来源

运粮队可以前往友方城镇或城堡。

### 城镇

从目标城镇市场购买：

```text
TargetTown.Settlement.ItemRoster
→ SupplyParty.ItemRoster
```

然后返回。

### 城堡

从目标城堡军事粮仓调拨盈余粮食：

```text
CastleFoodSurplus
=
CurrentFood
-
DailyFoodConsumption × ReserveDays
```

只有：

```text
CastleFoodSurplus > 0
```

时才能调粮。

---

## 14. 运粮目标选择

第一版使用：

```text
SupplyScore
=
AvailableFood / Distance
```

城镇：

```text
AvailableFood
=
Settlement.ItemRoster.TotalFood
```

城堡：

```text
AvailableFood
=
max(
    0,
    GarrisonFood
    -
    GarrisonDailyFoodConsumption × ReserveDays
)
```

优先选择粮食多、距离近的目标。

---

## 15. 返回与解散

运粮队返回 `HomeSettlement` 后：

```text
SupplyParty.ItemRoster中的Food
→ Home GarrisonParty.ItemRoster
```

护卫：

```text
SupplyParty.MemberRoster
→ Home GarrisonParty.MemberRoster
```

随后销毁运粮队。

运粮队属于临时任务 Party，不长期存在。

---

## 16. 运粮队被消灭

如果运粮队途中被敌军击败：

```text
士兵正常战死/被俘
粮食正常被缴获
```

这些兵力不返还驻军。

如果定居点之后仍然缺粮，AI 可以再次创建新的运粮队。

---

## 17. 总体流程

```text
确定DesiredGarrisonSize
↓
ClanLeader.Gold决定工资承受能力
↓
形成实际驻军
↓
驻军消耗GarrisonParty.ItemRoster
↓
FoodSupplyDays < 30
↓
先搜刮本地FoodStocks
↓
城镇再从本地市场购买
↓
仍不足
↓
创建运粮队
↓
其他城镇购买 / 其他城堡调粮
↓
返回补充驻军粮仓
↓
护卫返回驻军
↓
运粮队解散
```

核心原则：

```text
驻军目标人数 = 固定战略需求
Gold = 财政限制
粮仓与运粮队 = 后勤限制
粮食消耗 = 10人 / 1粮食 / 天
```
