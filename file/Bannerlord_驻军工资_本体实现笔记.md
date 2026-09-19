# Bannerlord 驻军工资本体实现笔记

## 用途

本文记录驻军工资上限、实际工资支付和欠薪处理在游戏本体中的实现位置与结论。

后续修改相关功能时应优先查阅本文。只有在版本变化或信息不足时，才重新读取完整反编译源码。

---

## 1. 驻军工资上限更新

反编译源码：

```text
code/ClanVariablesCampaignBehavior.cs
```

关键方法：

```csharp
ClanVariablesCampaignBehavior.UpdateClanSettlementsPaymentLimit(Clan clan)
```

本体计算使用：

```text
FactionHelper.FindIdealGarrisonStrengthPerWalledCenter
FactionHelper.OwnerClanEconomyEffectOnGarrisonSizeConstant
FactionHelper.SettlementProsperityEffectOnGarrisonSizeConstant
FactionHelper.SettlementFoodPotentialEffectOnGarrisonSizeConstant
Campaign.Current.AverageWage
```

计算结果通过以下方法写入定居点：

```csharp
Settlement.SetGarrisonWagePaymentLimit(int limit)
```

当前 Mod 已移除繁荣度和粮食潜力对工资上限的影响，改用固定战略驻军人数。

---

## 2. Clan 金币与领袖金币

反编译源码：

```text
code/Clan.cs
```

关键属性：

```csharp
public int Gold
{
    get
    {
        return Leader?.Gold ?? 0;
    }
}
```

结论：

```text
Clan.Gold == Clan.Leader.Gold
```

Clan 没有独立于领袖的普通金币余额。涉及 Clan 财政承受能力时，本质上读取的是 Clan 领袖个人金币。

---

## 3. Clan 领袖金币系数

反编译源码：

```text
code/FactionHelper.cs
```

关键方法：

```csharp
FactionHelper.OwnerClanEconomyEffectOnGarrisonSizeConstant(Clan clan)
```

分段规则：

```text
Gold < 40000
Factor = 1 - 0.75 x (1 - Gold / 40000)

40000 <= Gold <= 80000
Factor = 1

80000 < Gold <= 160000
Factor = 1 + 0.5 x (Gold - 80000) / 80000

160000 < Gold < 320000
Factor = 1.5 + 0.5 x (Gold - 160000) / 160000

Gold >= 320000
Factor = 2
```

参考值：

| 领袖金币 | 系数 |
|---:|---:|
| 0 | 0.25 |
| 40000 | 1.00 |
| 80000 | 1.00 |
| 160000 | 1.50 |
| 320000 | 2.00 |

---

## 4. 驻军工资计入 Clan 支出

反编译源码：

```text
code/DefaultClanFinanceModel.cs
```

关键调用链：

```text
CalculateClanGoldChange
-> CalculateClanExpensesInternal
-> AddExpensesFromPartiesAndGarrisons
-> AddPartyExpense
-> CalculatePartyWage
```

`AddExpensesFromPartiesAndGarrisons` 会遍历：

```csharp
foreach (Town town in clan.Fiefs)
```

如果 `town.GarrisonParty` 存在且处于活动状态，其工资会作为 Clan 支出加入每日金币变化。

---

## 5. 每日财务最终结算对象

反编译源码：

```text
code/ClanVariablesCampaignBehavior.cs
```

关键逻辑：

```csharp
int dailyGoldChange = MathF.Round(
    Campaign.Current.Models.ClanFinanceModel
        .CalculateClanGoldChange(clan, false, true, false)
        .ResultNumber);

GiveGoldAction.ApplyBetweenCharacters(
    null,
    clan.Leader,
    dailyGoldChange,
    true);
```

结论：

```text
驻军工资最终通过 Clan 每日财务变化从 Clan 领袖金币中扣除。
```

`PartyTradeGold` 会参与内部转账流程，但 Clan 层面的最终承担者仍是领袖。

---

## 6. AI 驻军停付门槛

反编译源码：

```text
code/DefaultClanFinanceModel.cs
```

关键方法：

```csharp
DefaultClanFinanceModel.AddPartyExpense
```

本体包含以下判断：

```csharp
if (availableClanGold < (party.IsGarrison ? 8000 : 4000)
    && applyWithdrawals
    && clan != Clan.PlayerClan)
```

对于没有领队 Hero 的驻军，预算可能变为 `0`。

结论：

```text
AI Clan 可用金币低于 8000 时，可能停止支付驻军工资。
```

这与 `GarrisonWagePaymentLimit` 是两个不同机制：

```text
GarrisonWagePaymentLimit = 允许驻军维持的工资规模
Clan 实际金币 = 当天是否有能力支付工资
```

提高工资上限不会自动解决 Clan 金币低于 `8000` 时的停付问题。

---

## 7. 驻军工资不足与逃兵

反编译或调试参考：

```text
ModifiedArmy/ModifiedArmy/Models/DebugDefaultPartyDesertionModel.cs
```

关键状态：

```text
MobileParty.HasLimitedWage
MobileParty.PaymentLimit
MobileParty.TotalWage
MobileParty.HasUnpaidWages
```

当驻军总工资超过支付上限，或实际工资没有得到支付时，本体逃兵模型可能移除驻军士兵。

排查驻军异常解散时，需要同时检查：

```text
GarrisonWagePaymentLimit
GarrisonParty.TotalWage
Clan.Leader.Gold
GarrisonParty.HasUnpaidWages
```

---

## 8. 王国封地管理 UI

本体 prefab：

```text
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/Fiefs/FiefsPanel.xml
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/Fiefs/FiefTuple.xml
```

本体 ViewModel：

```text
code/KingdomSettlementItemVM.cs
```

右侧定居点信息来自：

```csharp
KingdomSettlementItemVM.ItemProperties
```

属性列表由以下方法重建：

```csharp
KingdomSettlementItemVM.UpdateProperties()
```

当前 Mod 使用 Harmony Postfix 在该方法完成后追加工资上限信息。

---

## 9. 当前 Mod 相关文件

```text
ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonWageLimitModel.cs
ModifiedArmy/ModifiedArmy/Garrison/Behaviors/GarrisonWageLimitBehavior.cs
ModifiedArmy/ModifiedArmy/Garrison/Patches/GarrisonWageLimitPatch.cs
ModifiedArmy/ModifiedArmy/Garrison/Patches/KingdomSettlementItemVMPatch.cs
ModifiedArmy/ModifiedArmy/Models/NewPartyWageModel.cs
```

当前共享平均工资：

```text
NewPartyWageModel.Tier4AndTier5AverageWage
= (Tier4TroopWage + Tier5TroopWage) / 2
= (16 + 30) / 2
= 23
```

---

## 10. 本体仓库入口与库存界面

反编译源码：

```text
code/PlayerTownVisitCampaignBehavior.cs
```

本体英文按钮：

```text
Open stash
```

菜单 ID：

```text
open_stash
```

关键调用：

```csharp
InventoryScreenHelper.OpenScreenAsStash(
    Settlement.CurrentSettlement.Stash);
```

`InventoryScreenHelper.OpenScreenAsStash` 接受 `ItemRoster`，因此军事粮仓可以直接传入：

```csharp
Settlement.CurrentSettlement
    .Town
    .GarrisonParty
    .ItemRoster
```

这会直接读写驻军 Party 的真实物品栏，不需要新增粮仓存档对象。
