# Bannerlord 中央总督分配机制实现设计

## 1. 适用范围

### 中央统一分配文化
默认：
- Empire
- Aserai

规则：
- 禁止 Clan 使用原版总督自动分配。
- 改由王国中央统一分配 Governor。
- 玩家作为该文化王国君主时，机制同样自动生效。
- 允许 Clan A Hero 担任 Clan B Settlement 的 Governor。

### 其他文化
- 完全保留 Vanilla `UpdateGovernorsOfClan()`。
- 不增加跨 Clan 借调机制。

---

## 2. 中央总督制 XML 开关

是否采用中央统一分配，由 XML 按 `Kingdom.Culture` 配置，不在代码中写死文化名单。

示例：

```xml
<CultureGovernorPolicy
    CultureId="empire"
    CentralizedGovernorAssignment="true" />

<CultureGovernorPolicy
    CultureId="aserai"
    CentralizedGovernorAssignment="true" />

<CultureGovernorPolicy
    CultureId="vlandia"
    CentralizedGovernorAssignment="false" />
```

默认配置：

```text
Empire   = true
Aserai   = true
其他文化 = false
```

未配置文化默认：

```text
CentralizedGovernorAssignment = false
```

代码统一通过类似：

```text
GovernorPolicyManager.IsCentralizedAssignment(kingdom.Culture)
```

判断制度。

---

## 3. 禁止原版 Clan 总督分配

Harmony Prefix：

`ClanVariablesCampaignBehavior.UpdateGovernorsOfClan(Clan clan)`

规则：

```text
if IsCentralizedAssignment(clan.Kingdom.Culture)
    跳过原方法
else
    执行 Vanilla
```

不 Patch `DailyTickClan()`。

---

## 4. 中央总督分配事件

新增：

`KingdomGovernorAssignmentBehavior`

监听：

`CampaignEvents.WeeklyTickEvent`

每周只处理：

```text
CentralizedGovernorAssignment == true
```

的王国。

新游戏初始化时额外执行一次空缺补充。

---

## 5. 只补空缺，不重排

仅处理：

`Town.Governor == null`

且没有 Hero 正在前往该 Settlement 上任的职位。

已有 Governor：

> 永不因为出现更优秀 Hero 而自动替换。

---

## 6. Hero 候选范围

候选来自当前 Kingdom 所有正常 Clan。

基本条件：

- Hero 存活；
- Hero 所属 Clan 属于当前 Kingdom；
- Clan 非雇佣兵；
- `CanHeroBeGovernor(hero) == true`；
- `hero.GovernorOf == null`；
- `hero.PartyBelongedTo == null`；
- 未被俘；
- 没有正在前往其他 Governor 岗位。

AI 自动分配暂不使用 `Clan.PlayerClan` Hero。

---

## 7. Governor 候选评分

```text
GovernorBaseScore
= Steward × 2.0
+ Trade × 1.2
+ Leadership × 1.0
+ Charm × 0.9
+ Engineering × 0.8
+ Tactics × 0.3
+ HonorBonus
```

```text
Honor > 0
→ HonorBonus = 100
```

不使用 Vanilla `GetHeroGoverningStrengthForClan()` 中的：

- Hero Gold；
- 与 Clan Leader 的亲属关系奖励。

---

## 8. 文化适配

```text
CultureFactor
= 1.20    Hero.Culture == Settlement.Culture
= 1.00    otherwise
```

最终：

```text
GovernorCandidateScore
= GovernorBaseScore × CultureFactor
```

文化只影响评分，不构成硬性限制。

---

## 9. Settlement 优先级

```text
SettlementPriority
= (Town ? 3 : 1)
+ sqrt(Prosperity / 1000)
+ BoundVillageCount
```

优先级高的 Settlement 先获得 Governor。

---

## 10. 分配流程

```text
1. 遍历所有 Kingdom
2. 仅处理 CentralizedGovernorAssignment == true 的 Kingdom
3. 收集所有真正空缺的 Town / Castle
4. 按 SettlementPriority 降序排序
5. 收集整个 Kingdom 的可用 Hero
6. 对每个 Settlement 计算所有候选 Hero 的 GovernorCandidateScore
7. 选择最高分 Hero
8. ChangeGovernorAction.Apply(town, hero)
9. 从本轮 Hero Pool 移除该 Hero
10. 继续处理下一个 Settlement
```

采用简单 Greedy 分配，不做全局重排。

---

## 11. Governor 与 WarParty Leader 互斥

当前 Governor：

> 禁止成为新的 WarParty Leader。

Patch：

`HeroSpawnCampaignBehavior.GetHeroPartyCommandScore(Hero hero)`

如果：

`hero.GovernorOf != null`

则返回极低评分，使其无法被 AI 选中。

同时对 `SpawnLordParty()` 增加防御性检查：

```text
GovernorOf != null
→ 禁止创建 WarParty
```

反方向：

当前 WarParty Leader 因 `PartyBelongedTo != null` 和 `CanHeroBeGovernor()` 限制，不进入 Governor 候选池。

---

## 12. 跨 Clan Governor 清理

如果跨 Clan Governor 的 Hero 所属 Clan 离开当前 Kingdom：

```text
hero.Clan.Kingdom
!=
hero.GovernorOf.OwnerClan.Kingdom
```

则：

`ChangeGovernorAction.RemoveGovernorOf(hero)`

随后等待下一次中央分配补缺。

Settlement 易主等原版已经能够处理的情况继续使用 Vanilla 行为。

---

## 13. 玩家君主

玩家如果是采用中央总督制的王国君主：

- 原版 Clan 总督分配仍然关闭；
- 中央总督分配机制仍然每周自动运行；
- 不要求玩家手动逐个任命；
- 不增加额外 UI。

---

## 14. 推荐代码结构

```text
ModifiedPolitics/
└─ Governor/
   ├─ Behaviors/
   │  └─ KingdomGovernorAssignmentBehavior.cs
   ├─ Patches/
   │  ├─ ClanGovernorAssignmentPatch.cs
   │  └─ GovernorWarPartyPatch.cs
   ├─ Models/
   │  └─ GovernorAssignmentModel.cs
   └─ Config/
      └─ GovernorPolicyManager.cs
```

---

## 15. 核心原则

- 中央总督制由 XML 按 `Kingdom.Culture` 配置。
- Empire / Aserai 默认开启。
- 其他文化默认关闭并保持 Vanilla Clan 总督机制。
- 中央系统只补空缺，不重排现任 Governor。
- 中央分配可跨 Clan 使用 Hero。
- Governor 与 WarParty Leader 完全互斥。
- 未配置文化默认回退 Vanilla 行为。
