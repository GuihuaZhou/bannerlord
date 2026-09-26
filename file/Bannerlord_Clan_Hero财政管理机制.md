# Bannerlord Clan / Hero 财政管理机制

## 1. 核心结论

Bannerlord 的 Clan 财政并不是每个 Hero 完全独立核算。

对于领主 Clan，可以近似理解为：

```text
Clan.Leader.Gold
= Clan 中央财政

非领袖 Hero.Gold
= 该 Hero 所率 LordParty 的前线周转资金

PartyTradeGold
= Party 当前可直接使用的流动资金
```

其中对于正常 LordParty：

```text
PartyTradeGold == LeaderHero.Gold
```

因此，非领袖 Hero 的个人 Gold 实际上同时承担了其 Party 的日常流动资金功能。

---

## 2. Clan.Gold

本体中很多 Clan 财政判断直接使用：

```text
clan.Gold
```

其经济意义基本等同于：

```text
clan.Leader.Gold
```

因此 Clan 领袖通常拥有远高于其他成员的 Gold。

可以把：

```text
Clan.Leader.Gold
```

理解为：

```text
Clan 中央金库
```

Clan 的：

```text
领地收入
Party收入
商队收入
驻军支出
Party工资
贡金
雇佣兵费用
其他Clan级收入与支出
```

最终都会汇总进 Clan 的每日财政变化。

---

## 3. 非领袖 Hero.Gold

每个 Hero 都有自己的：

```text
Hero.Gold
```

但是对于一个正在统领 LordParty 的非领袖 Hero：

```text
Hero.Gold
```

不仅是“个人财富”，同时还是这支 Party 的周转资金。

本体 `MobileParty.PartyTradeGold`：

```csharp
public int PartyTradeGold
{
    get
    {
        if (this.IsLordParty && this.LeaderHero != null)
        {
            return this.LeaderHero.Gold;
        }

        return this._partyTradeGold;
    }

    set
    {
        if (this.IsLordParty && this.LeaderHero != null)
        {
            this.LeaderHero.Gold = MathF.Max(value, 0);
            return;
        }

        this._partyTradeGold = MathF.Max(value, 0);
    }
}
```

所以对 LordParty：

```text
PartyTradeGold
==
LeaderHero.Gold
```

---

## 4. 非领袖 Party 的工资结算

非领袖 WarParty 的工资通过：

```text
DefaultClanFinanceModel
→ AddExpensesFromPartiesAndGarrisons()
→ AddPartyExpense()
→ CalculatePartyWage()
```

进行结算。

`CalculatePartyWage()`：

```csharp
private int CalculatePartyWage(
    MobileParty mobileParty,
    int budget,
    bool applyWithdrawals)
{
    int totalWage = mobileParty.TotalWage;
    int num = totalWage;

    if (applyWithdrawals)
    {
        num = MathF.Min(totalWage, budget);
        ApplyMoraleEffect(mobileParty, totalWage, num);
    }

    return num;
}
```

因此实际能够支付的工资：

```text
PaidWage
=
min(
    Party.TotalWage,
    AvailableClanBudget
)
```

这里决定能否支付工资的关键，并不是非领袖 Hero 自己有多少钱，而是 Clan 当前可用财政。

---

## 5. 工资首先作用于非领袖 Hero.Gold

`AddPartyExpense()` 中：

```csharp
party.PartyTradeGold -= num3;
```

而正常 LordParty：

```text
PartyTradeGold == LeaderHero.Gold
```

因此等价于：

```text
Party.LeaderHero.Gold
-= PaidWage
```

例如：

```text
非领袖Hero.Gold = 1000
Party.TotalWage = 3000
```

如果 Clan 财政足够支付完整工资：

```text
PaidWage = 3000
```

那么 Hero.Gold 会被扣到 0。

由于 `PartyTradeGold` setter 使用：

```text
MathF.Max(value, 0)
```

所以 Hero.Gold 不会变成负数。

---

## 6. Clan 会补充非领袖 Party 的资金

本体存在：

```text
PartyGoldLowerThreshold = 5000
```

工资扣除以后，如果：

```text
PartyTradeGold < 5000
```

Clan 财政会尝试给 Party 补充流动资金。

大致逻辑：

```text
需要补充金额
=
5000
-
工资支付后的PartyTradeGold
```

然后：

```csharp
party.PartyTradeGold += num5;
```

由于 LordParty：

```text
PartyTradeGold == LeaderHero.Gold
```

因此相当于：

```text
非领袖Hero.Gold += Clan补贴
```

---

## 7. Clan 补贴不是凭空生成

`AddPartyExpense()` 本身不会直接写：

```text
Clan.Leader.Gold -= 补贴
```

而是把补贴计入 Clan 当日财政支出。

最终由：

```text
ClanVariablesCampaignBehavior.DailyTickClan()
```

统一结算。

核心流程：

```text
CalculateClanGoldChange(
    clan,
    applyWithdrawals: true
)
↓
得到 dailyGoldChange
↓
GiveGoldAction.ApplyBetweenCharacters(
    null,
    clan.Leader,
    dailyGoldChange,
    true
)
```

因此如果非领袖 Party 需要 Clan 补贴：

```text
Clan财政支出增加
↓
dailyGoldChange降低
↓
最终从Clan.Leader.Gold扣除
```

所以：

```text
非领袖Hero缺少的Party资金
最终由Clan.Leader.Gold承担
```

---

## 8. 示例

初始：

```text
Clan.Leader.Gold = 50000

HeroA.Gold = 1000
HeroA Party.TotalWage = 3000
```

Clan 财政正常。

### 第一步：计算工资

```text
PaidWage = 3000
```

### 第二步：从 Party 资金支付

因为：

```text
HeroA.Gold
==
PartyTradeGold
```

所以 HeroA.Gold 会被扣到 0。

### 第三步：补充 Party 周转资金

因为：

```text
PartyTradeGold < 5000
```

Clan 会给 HeroA 的 Party 注入资金。

### 第四步：Clan 日结

这笔补贴被计入 Clan 支出。

最终：

```text
Clan.Leader.Gold
↓
```

也就是说：

```text
Clan中央财政
→ 非领袖Hero
→ 非领袖Party
```

形成实际的资金支持关系。

---

## 9. Party 有钱时也会向 Clan 上缴

资金流并不是单向的。

对于非领袖 Party，如果：

```text
PartyTradeGold > 10000
```

`AddIncomeFromParty()` 会抽取部分盈余：

```text
PartyIncome
=
(PartyTradeGold - 10000) / 10
```

因此：

```text
Party资金过低
→ Clan补贴

Party资金过高
→ 部分盈余上缴Clan
```

形成简单的中央财政调节机制。

---

## 10. Clan 贫困时的限制

对于普通非领袖 WarParty，如果 Clan 当前可用资金过低：

```text
AvailableClanGold < 4000
```

本体会停止正常的 Party 财政支持。

如果此时：

```text
Party.LeaderHero != null
且
PartyTradeGold < 500
```

则最多只给一个很小的工资预算：

```text
<= 250
```

否则可能：

```text
WageBudget = 0
```

因此真正导致非领袖 Party 无法发工资的，并不是 Hero.Gold 很少，而是 Clan 整体财政已经接近枯竭。

---

## 11. 欠薪

如果：

```text
PaidWage < Party.TotalWage
```

本体调用：

```text
ApplyMoraleEffect()
```

并设置：

```text
MobileParty.HasUnpaidWages
```

欠薪比例：

```text
UnpaidWageRatio
=
1
-
PaidWage / TotalWage
```

随后产生欠薪士气惩罚。

因此：

```text
Clan财政正常
→ 非领袖Hero缺钱
→ Clan补贴
→ Party正常发工资

Clan财政枯竭
→ 无法补贴
→ 工资不足
→ HasUnpaidWages
→ 士气惩罚
```

---

## 12. 财政结构总结

可以将本体 Clan 财政理解为：

```text
                 Clan.Leader.Gold
                    Clan中央金库
                         │
            ┌────────────┼────────────┐
            ↓            ↓            ↓
       领袖Party     Hero A Party   Hero B Party
                        │             │
                   HeroA.Gold     HeroB.Gold
                  PartyTradeGold  PartyTradeGold
                        │             │
                    支付工资       支付工资
                        │             │
             资金不足时由Clan补贴
```

反方向：

```text
非领袖Party资金过高
↓
部分盈余
↓
Clan财政
↓
Clan.Leader.Gold
```

---

## 13. 对 Mod 设计的意义

因此在判断某个非领袖 Hero 是否有能力维持军队时，不应该只看：

```text
Hero.Gold
```

更合理的是区分三个概念：

```text
Hero.Gold / PartyTradeGold
= 当前Party周转资金

Clan.Leader.Gold
= Clan长期财政能力

Party.PaymentLimit
= AI允许该Party承担的工资规模
```

对于统一招募 Model：

```text
AIRecruitmentModel
```

财政判断也应该考虑：

```text
短期招募能力
+
Party工资上限
+
Clan长期财政能力
```

而不是简单使用 Hero.Gold 作为唯一财政指标。

---

## 14. 主要本体代码位置

```text
code/MobileParty.cs
```

关键：

```text
PartyTradeGold
```

用于确认：

```text
LordParty.PartyTradeGold
==
LeaderHero.Gold
```

```text
code/DefaultClanFinanceModel.cs
```

关键：

```text
PartyGoldLowerThreshold
AddExpensesFromPartiesAndGarrisons()
AddPartyExpense()
CalculatePartyWage()
ApplyMoraleEffect()
AddIncomeFromParty()
```

```text
code/ClanVariablesCampaignBehavior.cs
```

关键：

```text
DailyTickClan()
```

用于最终将：

```text
CalculateClanGoldChange()
```

结果统一结算到：

```text
Clan.Leader.Gold
```
