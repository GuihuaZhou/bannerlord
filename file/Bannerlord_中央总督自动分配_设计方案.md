# Bannerlord 中央总督自动分配——设计方案

> 状态：讨论确定的规则汇总。代码已于 2026-10-10 按此设计实现，等待编译与游戏内验证；具体落地见配套《实现方案》。

## 1. 适用范围和时间

由 `Kingdom.Culture` 对应的 XML 属性 `centralizedGovernorAssignment` 决定。默认 **Empire、Aserai 启用**；其他文化未配置时为关闭。关闭时完整保留 Vanilla 的 Clan 总督机制。

启用中央制的王国由 Kingdom 统一分配总督。**新游戏开始时不立即任命；满 7 个游戏日后，在首次符合条件的 `WeeklyTick` 执行；此后每周重新规划。** 每次对王国全部 Town / Castle 重新分配，包含原有总督，允许空缺补充、调任、撤换或保留现任。

不设置 Governor 主动申请。君主现有的手动任命入口保留；每周中央规划仍可重新调整手动任命的结果。

## 2. Hero 候选资格

候选来源：所有属于该 Kingdom 的正常 Clan（含 `RulingClan`、AI Clan、`PlayerClan`），排除已消灭 Clan 和雇佣兵 Clan。

必须满足：

```csharp
hero != null
&& hero.IsAlive && hero.IsActive
&& hero.Clan?.Kingdom == kingdom
&& !hero.IsPrisoner
&& hero.PartyBelongedTo == null
&& !hero.IsTraveling
&& Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero)
```

**现任 Governor 可以进入候选池**，所以不使用 `GovernorOf == null` 过滤；正在赴任的 Hero 及对应目标 Settlement 暂时锁定，不重复调配。

玩家主队内的同伴即使没有统兵，其 `PartyBelongedTo == MobileParty.MainParty`，**不入选**；已离开所有 Party 的正常玩家 Clan Hero **可以入选**。Governor 与 WarParty Leader 保持互斥。

## 3. Settlement-first：分配顺序

先选择 Settlement，再为它寻找 Governor。每轮根据剩余可用 Hero 动态计算各 Settlement 的 `EligibleCandidateCount`。

按以下词典顺序排序（最后使用 `Settlement.StringId` 保证确定性）：

```text
Town > Castle
→ ProsperityBand DESC
→ IsBorder DESC
→ EligibleCandidateCount ASC（0 放最后）
→ Prosperity DESC
→ Settlement.StringId
```

`ProsperityBand = floor(max(0, Prosperity) / 1000)`。

`IsBorder` 采用稳定地理判定：按距离寻找最近 5 座**其他** Town/Castle，只要存在不同势力所属领地，即认定边境；与当前是否交战、敌军、围城和劫掠无关。该定义与现有驻军工资模型保持一致。

每确定一个 Hero，就从本轮可用池移除，再对剩余 Settlement 重算候选人数。候选为 0 的 Settlement 允许空缺，不阻塞其他岗位。

## 4. Governor 能力与门槛

| Settlement | GovernorProfile | 主要技能 |
|---|---|---|
| 内地 Town | Civil | Steward、Trade、Charm，兼顾 Leadership / Engineering |
| 边境 Town | Frontier | Steward、Leadership、Engineering、Tactics，兼顾 Charm / Trade |
| Castle | Frontier | 同 Frontier |

```text
CultureFactor = (hero.Culture == settlement.Culture) ? 1.20 : 1.00
EffectiveGovernorAbility = ProfileGovernorAbility × CultureFactor

RequiredAbility = BaseAbility + ProsperityRequirement + BorderRequirement
合格条件：EffectiveGovernorAbility >= RequiredAbility
```

`RequiredAbility` 由 Settlement 自身决定：Town 基础要求高于 Castle，繁荣度越高要求越高，边境额外提高要求；不能根据当前最佳 Hero 反推门槛。文化匹配仅提供加成，不是硬限制。无人达标时可留空。

## 5. Governor 候选顺序

对每座 Settlement **先调用** `GetBestRulingClanCandidate(...)`：从合格的 `RulingClan` Hero 中按 `EffectiveGovernorAbility DESC → 现任该 Settlement 者优先 → Hero.StringId` 排序。仅当没有合格统治 Clan Hero 时，才调用 `GetBestOtherClanCandidate(...)`。

其他 Clan 的 Hero 先满足能力门槛，再按以下词典顺序选择，不使用混合政治总分：

```text
ClanTier ASC
→ OfficeCountOfClan ASC
→ InfluenceBand ASC
→ ClanInfluence ASC
→ FiefWeight ASC
→ EffectiveGovernorAbility DESC
→ 现任该 Settlement 者优先
→ Hero.StringId
```

- `OfficeCountOfClan` = 该 Clan 已有中央官职数 + 地方官职数，**Governor 不计入**。
- `FiefWeight = TownCount × 3 + CastleCount`。
- `InfluenceBand`：0–99→0；100–299→1；300–599→2；600–999→3；1000+→4。

**原则：能力决定是否胜任；统治 Clan 合格者优先；其他 Clan 优先扶持政治资源较少的家族。**

## 6. 完整规划与执行

先为所有可重新分配的 Settlement 生成 `TargetAssignment: Town → Hero/null`，在规划阶段不修改游戏状态。未变化的职位原样保留；变化的职位先解除旧任命，再集中应用新任命，以支持交换和链式调任。正在赴任的岗位与 Hero 锁定、保留原目标。

地方官职必须有效：已有地方官职的 Governor 只能被安排到与其官职兼容的 Settlement；不得因为自动调配导致官职失效。若目标规划无法满足这一条件，则保留相应现任并重新规划冲突部分。

## 7. 总督任免与 Clan 关系

关系结算主体只有 `Kingdom.RulingClan ↔ 受影响 Clan`；不为 Governor Hero 个人结算，也不对 RulingClan 自己结算。

关系数值由 Settlement 类型、繁荣度档位和边境属性决定：

```text
ProsperityBonus = min(max(0, ProsperityBand), 5)
BorderBonus = IsBorder ? 2 : 0

任命 Town   = +12 + ProsperityBonus + BorderBonus
任命 Castle =  +5 + ProsperityBonus + BorderBonus

主动撤职 Town   = -18 - ProsperityBonus - BorderBonus
主动撤职 Castle =  -8 - ProsperityBonus - BorderBonus
```

| 职位 | 任命 | 主动撤职 |
|---|---:|---:|
| 内地 Castle | +5～+10 | -8～-13 |
| 边境 Castle | +7～+12 | -10～-15 |
| 内地 Town | +12～+17 | -18～-23 |
| 边境 Town | +14～+19 | -20～-25 |

关系按每个 Clan 每轮**统一结算一次**：

1. 相同 Settlement 保留原职位，关系变化 0（即使同 Clan 内换人）。
2. 其余旧、新岗位按职位的**任命收益**配对，调任变化 = 新岗位任命收益 − 旧岗位任命收益。
3. 未配对的新增职位按任命收益计算；未配对的丢失职位按主动撤职惩罚计算。
4. 同一 Clan 所有变化汇总后对 `RulingClan ↔ 该 Clan` 应用一次关系变化。

示例：Castle（任命收益 +7）→ Town（+15），关系 **+8**；反向 **−8**；同等重要性的 Town → Town，**0**。Hero 死亡、Clan 离开王国等并非君主主动撤职的自然失效，不施加撤职惩罚。单纯繁荣度变化不触发关系变化。

## 8. 关键日志

**仅对玩家所在王国输出总督分配日志**；其他王国照常运算但不写本系统日志。沿用 `ModifiedPolitics.Tool.ModLogger`，以文件日志为主，避免屏幕刷屏。

| 事件 | 记录内容 | 等级 |
|---|---|---|
| 每周开始/结束 | Kingdom、Settlement 数、候选 Hero 数、任命/调任/撤职/保留/空缺数 | Info |
| 任命/调任/撤职 | Hero、Clan、旧/新 Settlement、原因 | Info |
| 无合格 Hero | Settlement、RequiredAbility、候选数 | Debug |
| Clan 关系变化 | RulingClan、目标 Clan、最终变化值 | Info |
| 执行失败 | 目标、原因、未执行的变更 | Warn |

不逐个记录未变化岗位、技能计算和排序细节；每个王国每周只输出一条汇总。

## 9. 玩家 Clan 地图事件通知

玩家 Clan Hero **实际任命或调任成功**后，使用与收到求和提议相同的右侧圆形 `MapNotification` 机制展示。其他 Clan 的任命、玩家 Hero 留任不通知。

- 首次任命：`总督任命：你的家族成员{HERO}已被任命为{SETTLEMENT}总督。`
- 调任：`总督调任：你的家族成员{HERO}已从{OLD}调任至{NEW}。`
- 悬停显示说明；点击定位目标 Settlement；可手动关闭。不自动打开大型弹窗。
- 每名 Hero 每次成功变更只生成一条事件，避免和普通文字日志重复。

## 10. 执行摘要

```text
每周（且新游戏已满 7 天）
→ 中央制 Kingdom 全部 Town/Castle
→ 锁定赴任中职位；收集闲置 Hero + 可调任现任 Governor
→ 计算 Settlement 能力要求、Profile、边境与稀缺度
→ 按 Settlement 优先级逐个选人
   → 先 RulingClan，后其他 Clan
→ 完成 TargetAssignment
→ 校验唯一性、地方官职和赴任锁定
→ 按“先解除旧任命、后应用新任命”执行差异
→ 按 Clan 结算关系
→ 玩家王国关键日志
→ PlayerClan Hero 任命/调任事件通知
```
