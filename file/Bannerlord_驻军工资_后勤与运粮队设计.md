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

> 当前实现：允许驻军走 Party 食物消费，并通过 `NewSettlementFoodModel` 从 `FoodStocks` 变化中移除驻军消耗。

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

## 7. 本地产出优先分配

不再直接从 `FoodStocks` 数值中征收粮食。

原因：

```text
FoodStocks 是定居点民生粮食状态值
并不等价于市场或仓库中的真实 Food Item
```

驻军粮仓改为优先接收定居点当日产生的虚拟粮食来源：

```text
城镇周边土地产出
城堡周边土地产出
城堡 Farmlands 建筑产出
```

分配顺序：

```text
当日虚拟粮食产出
→ 优先以 1:1 转化为真实 Food Item 存入驻军粮仓
→ 驻军粮仓达到45天目标后
→ 剩余产出继续进入 FoodStocks 计算
```

分配比例：

```text
周边土地 = 100%
城堡 Farmlands = 100%
```

原则：

```text
驻军粮仓保存真实物品
FoodStocks 继续表示定居点民生粮食
不得通过直接扣除 FoodStocks 凭空生成真实粮食
```

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
本地产出分配
→ 本地市场购买
→ 运粮队
```

城堡没有市场：

```text
本地产出分配
→ 运粮队
```

城镇本地市场每日最多购买：

```text
GarrisonDailyFoodConsumption × 5
```

即每日最多补充5天口粮，避免城镇在一天内瞬间填满45天粮仓。

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

固定护卫数量：

```text
SupplyEscortSize = 50
```

例如：

```text
驻军300
→ 驻军250
→ 运粮队50
```

不凭空增加兵力。

抽调顺序：

```text
优先抽调低阶、健康、非 Hero 士兵
```

运粮队人数上限设置为50，避免本体将其按默认20人上限计算，并施加超员速度惩罚。

运粮队速度不使用固定值或人为最低速度，完全使用本体 `PartySpeedModel` 计算。

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

城镇交易必须满足：

```text
实际扣除目标城镇 Settlement.ItemRoster 中的 Food Item
按目标城镇实时价格扣除 HomeSettlement.OwnerClan.Leader 的 Gold
受市场库存、领袖资金和运粮队负重上限约束
```

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

城堡调粮规则：

```text
只允许同 Clan 城堡之间调拨
不向同王国其他 Clan 的城堡无偿抽粮
```

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

同 Clan 来源获得额外优先级：

```text
SameClanMultiplier = 1.5
```

有效来源范围：

```text
同 Clan 城镇或城堡
同王国其他 Clan 的城镇
```

无效来源：

```text
敌对定居点
其他 Clan 的城堡
被围攻或已不再满足外交关系的目标
```

多支运粮队选择同一来源时，已经被其他任务预订的粮食需要从可用量中扣除，避免重复预订。

单次任务最大计划运量：

```text
MaximumRequestedFood = 500
```

---

## 15. 驮畜与骑乘马

### 驮畜

目标数量：

```text
PackAnimals = EscortSize × 0.5
```

50名护卫对应25匹驮畜。

创建运粮队时：

```text
优先从 Home GarrisonParty.ItemRoster 提取现有驮畜
→ 数量不足时选择最便宜的可交易驮畜
→ 从 Clan 领袖财富中支付基础价值
→ 生成不足数量
```

驮畜作为真实物品参与负重计算。运粮队被击败时可成为战利品；正常返回时进入驻军粮仓。

### 骑乘马

创建运粮队时：

```text
优先从 Home GarrisonParty.ItemRoster 提取普通骑乘马
数量以徒步护卫人数为上限
骑兵不重复配马
```

到达来源城镇购买粮食时：

```text
从目标城镇真实市场库存购买缺少的普通骑乘马
使用目标城镇实时价格
由 HomeSettlement.OwnerClan.Leader 支付
不凭空生成骑乘马
```

普通骑乘马交由本体速度模型计算“徒步士兵骑马”加成。正常返回后，骑乘马进入驻军粮仓，供后续任务复用。

---

## 16. 返回与解散

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

全部物品：

```text
粮食
驮畜
骑乘马
其他物品
→ Home GarrisonParty.ItemRoster
```

随后销毁运粮队。

运粮队属于临时任务 Party，不长期存在。

---

## 17. 运粮队被消灭

如果运粮队途中被敌军击败：

```text
士兵正常战死/被俘
粮食正常被缴获
```

这些兵力不返还驻军。

如果定居点之后仍然缺粮，AI 可以再次创建新的运粮队。

运粮队被消灭后：

```text
HomeSettlement 进入10天派遣冷却
```

正常完成任务不使用该战败冷却。

---

## 18. 所有权、存档与任务恢复

运粮任务保存：

```text
HomeSettlement
SourceSettlement
MissionState
RequestedFood
派遣冷却时间
```

游戏读档后恢复目标并继续任务。

HomeSettlement 所有权变化时：

```text
运粮队 ActualClan 立即同步为新的 OwnerClan
名称、旗帜和地图视觉刷新
```

SourceSettlement 易主或失效时：

```text
重新选择有效来源
→ 没有替代来源时返回 HomeSettlement
```

---

## 19. 地图视觉与玩家对话

运粮队保持独立 `SupplyPartyComponent`，但地图图标参考商队：

```text
Aserai / Khuzait = camel
其他文化 = mule
```

不得将运粮队标记为 `IsCaravan`，避免本体商队贸易、财务和 AI 行为接管。

玩家遭遇己方运粮队：

```text
显示任务说明
允许放行
不得直接进入战斗
```

玩家遭遇外国运粮队：

```text
允许威胁并发动攻击
和平时期提示该行为可能引发战争
允许取消攻击并放行
```

---

## 20. 总体流程

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
本地产出优先分配给驻军粮仓
↓
城镇再从本地市场购买
↓
仍不足
↓
创建运粮队
↓
其他城镇购买 / 其他城堡调粮
↓
购买缺少的普通骑乘马
↓
返回补充驻军粮仓
↓
护卫、驮畜和骑乘马返回驻军
↓
运粮队解散
```

核心原则：

```text
驻军目标人数 = 固定战略需求
Gold = 财政限制
粮仓与运粮队 = 后勤限制
粮食消耗 = 10人 / 1粮食 / 天
速度 = 本体 PartySpeedModel
```

---

## 21. 当前实现参数

```text
SupplyEscortSize = 50
MaximumRequestedFood = 500
PackAnimalsPerEscort = 0.5
SourceCastleReserveDays = 30
EmergencySupplyDays = 15
ReplenishmentThresholdDays = 30
TargetSupplyDays = 45
MaximumPurchaseSupplyDaysPerDay = 5
DestroyedPartyCooldownDays = 10
```
