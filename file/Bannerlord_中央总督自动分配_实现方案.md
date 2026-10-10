# Bannerlord 中央总督自动分配——实现方案

> 基于 GitHub 仓库 `GuihuaZhou/bannerlord` 的 `main` 分支、`ModifiedPolitics` 当前代码（核对日期：2026-10-09）。对应业务规则见配套《设计方案》。
>
> 实现状态（2026-10-10）：核心规划、执行、关系结算、手动入口与玩家家族地图通知已经落地；等待用户编译和游戏内验证。

## 1. 当前代码与改造范围

| 现有位置（以 `ModifiedPolitics/ModifiedPolitics/` 为根） | 当前职责 | 改造决定 |
|---|---|---|
| `Governor/Config/GovernorPolicyManager.cs` | 解析 `centralizedGovernorAssignment`；缺省为 false | **保留** |
| `ModuleData/culture_governor_policies.xml` | Empire、Aserai 启用中央制 | **保留** |
| `Governor/Patches/ClanGovernorAssignmentPatch.cs` | 中央制屏蔽本体 `UpdateGovernorsOfClan` | **保留** |
| `Governor/Patches/GovernorWarPartyPatch.cs` | Governor 不得被选为战团指挥官 | **保留** |
| `Governor/Behaviors/KingdomGovernorAssignmentBehavior.cs` | WeeklyTick 补空缺；新游戏立即分配；排除 PlayerClan | **主要重构**：延迟首轮、全局规划、包括现任和 PlayerClan、执行差异 |
| `Governor/Models/GovernorAssignmentModel.cs` | `GetSettlementPriority()` 与单一 `GetCandidateScore()` | **替换核心评分**：Context、Profile/能力门槛、分层排序 |
| `KingdomFief/UI/KingdomSettlementGovernorVMMixin.cs` | 君主手动任命、解除总督 | **衔接共同执行/日志/通知**，保留界面与权限 |
| `HeroOffices/Behaviors/HeroOfficeBehavior.cs` | 官职存储、有效性检查 | **增加 Clan 官职计数 API** |
| `Utils/ModLogger.cs`（namespace `ModifiedPolitics.Tool`） | 文件与屏幕日志，各自阈值 | **复用**，不新造日志器 |
| `Main.cs` | 注册 `KingdomGovernorAssignmentBehavior`、Harmony、UIExtender | **沿用注册流程**，补上必要的新组件装配 |

**当前明确问题**：`OnNewGameCreated()` 直接分配；`AssignVacancies()` 仅处理 `Governor == null`；候选 Clan 有 `clan != Clan.PlayerClan`；Hero 候选有 `GovernorOf == null`；最佳候选为一个综合分；`best == null` 时 `break`。这些均需按新规划流程调整。

## 2. 建议代码组织

根目录：`ModifiedPolitics/ModifiedPolitics/`

```text
Governor/
├─ Behaviors/
│  └─ KingdomGovernorAssignmentBehavior.cs          [修改]
├─ Config/
│  └─ GovernorPolicyManager.cs                      [保留]
├─ Models/
│  ├─ GovernorAssignmentModel.cs                    [修改]
│  ├─ GovernorSettlementContext.cs                  [新增，运行时对象]
│  └─ BorderSettlementModel.cs                     [新增]
├─ Services/
│  ├─ GovernorCandidateSelector.cs                  [新增]
│  ├─ GovernorAssignmentPlanner.cs                  [新增]
│  ├─ GovernorAssignmentExecutor.cs                 [新增]
│  ├─ GovernorRelationService.cs                    [新增]
│  └─ GovernorNotificationService.cs                [新增]
├─ Notifications/
│  ├─ GovernorAppointmentMapNotification.cs         [新增]
│  ├─ GovernorAppointmentMapNotificationVM.cs       [新增]
│  └─ GovernorNotificationRegistrationPatch.cs      [新增]
├─ Persistence/
│  └─ GovernorNotificationSaveDefiner.cs            [新增，仅保存通知类型]
└─ Patches/
   ├─ ClanGovernorAssignmentPatch.cs               [保留]
   └─ GovernorWarPartyPatch.cs                      [保留]
HeroOffices/Behaviors/HeroOfficeBehavior.cs          [修改]
KingdomFief/UI/KingdomSettlementGovernorVMMixin.cs   [修改]
ModuleData/Languages/CNs/modifiedpolitics-zho-CN.xml [追加文本]
GUI/Brushes/...                                     [按通知图标资源追加]
```

新增文件为职责拆分建议；以现有命名空间、编译项目和 Bannerlord 版本引用为准，避免重复创建已有服务。

## 3. 首次执行延迟 7 天

在 `KingdomGovernorAssignmentBehavior.RegisterEvents()` 中保留 `WeeklyTickEvent`；不再于新游戏创建事件中调用分配。

`OnNewGameCreated` 只登记本次新游戏的首轮允许时间，例如 `firstEligibleTime = CampaignTime.DaysFromNow(7f)`。在 `SyncData(IDataStore)` 保存该时间；WeeklyTick 检查当前时间不早于截止值才执行。**读取旧存档**若缺少此字段，不额外强制等待 7 天，而从下一个 WeeklyTick 继续运行（需区别字段缺失与合法默认值）。

验证“达到 7 个完整游戏日后首次符合条件的 WeeklyTick”而不是仅移除初始化事件。现有存档不会因此突然在加载帧立即重分配。

## 4. 建立候选池（玩家 Party 规则）

修改 `IsEligibleClan`，只排除 `null / IsEliminated / IsClanTypeMercenary`，**删除** `clan != Clan.PlayerClan`。

`IsEligibleHero` 拆成清晰的场景：

```csharp
bool IsGovernorCandidate(Hero hero, Kingdom kingdom)
{
    return hero != null
        && hero.IsAlive && hero.IsActive
        && hero.Clan?.Kingdom == kingdom
        && hero.PartyBelongedTo == null
        && !hero.IsPrisoner && !hero.IsTraveling
        && Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero);
}
```

关键：**不要用 `hero.GovernorOf == null` 拒绝现任**。`hero.PartyBelongedTo == null` 必须保留，确保在 `MobileParty.MainParty` 随行的玩家同伴被排除；这项判断不只是排除 Party Leader。

如有 `hero.GovernorOf` 指向其他王国的领地、已离国等异常状态，先做清理/合法性检查，不让其作为可跨王国再分配的有效现任。

## 5. BorderSettlementModel：与驻军系统保持一致

仓库已有：`ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonWageLimitModel.cs` 的私有 `IsBorderSettlement(Settlement)`；使用 `Town.AllFiefs`、距目标最近的 **5 座其他要塞**、是否存在 `candidate.Settlement.MapFaction != ownerFaction` 判断边境；不依赖交战状态。

在 `ModifiedPolitics` 新增可独立使用的 `BorderSettlementModel.IsBorderSettlement(Kingdom, Settlement)`，沿用**相同定义和邻居数 5**，避免不同模块表现不一致。不要为了调用私有方法给 Governor 引入对 `ModifiedArmy` 的运行时硬依赖。如果后续抽取共同模型，再统一这两个调用点。

## 6. Context、能力模型、Settlement 排序

`GovernorSettlementContext`（不保存）最少持有：

```csharp
Town Town;
bool IsTown;
bool IsBorder;
float Prosperity;
int ProsperityBand;
GovernorProfile Profile;  // Civil / Frontier
float RequiredAbility;
int EligibleCandidateCount;
```

`GovernorAssignmentModel` 改为纯函数式 API：

```csharp
GovernorSettlementContext BuildContext(Kingdom kingdom, Town town);
float GetProfileGovernorAbility(Hero hero, GovernorProfile profile);
float GetEffectiveGovernorAbility(Hero hero, GovernorSettlementContext context);
float GetRequiredAbility(GovernorSettlementContext context);
bool MeetsAbilityRequirement(Hero hero, GovernorSettlementContext context);
int GetInfluenceBand(float influence);
int GetFiefWeight(Clan clan);
```

技能权重与 RequiredAbility 常数集中到单一代码配置，便于游戏内测试调整。**一套可运行的初始测试参数**（尚需平衡测试，不能当作本体固定值）：

```text
CivilAbility    = Steward*2.0 + Trade*1.5 + Charm*1.0 + Leadership*0.5 + Engineering*0.5
FrontierAbility = Steward*2.0 + Leadership*1.5 + Engineering*1.2 + Tactics*1.0 + Charm*0.5 + Trade*0.5
CultureFactor   = 同文化 1.20；其他 1.00
RequiredAbility = (Town ? 450 : 350) + min(250, Prosperity/40) + (IsBorder ? 100 : 0)
```

如不希望额外 XML 配置权重，可先保留代码常数；中央制开关继续使用现有 XML。

`GovernorAssignmentPlanner` 每次从尚未处理的 Settlement 中选择优先级最高者：

```text
Town 优先
→ ProsperityBand 大者
→ Border 优先
→ 非零 EligibleCandidateCount 小者（0 最后）
→ Prosperity 大者
→ Settlement.StringId
```

每确认一名目标 Hero，移出 `availableHeroes`，重新计算剩余岗位的 `EligibleCandidateCount`。无人达标时写入 `null` 并 `continue`，不能 `break`。

## 7. 两阶段候选选择 API

`GovernorCandidateSelector`：

```csharp
Hero GetBestRulingClanCandidate(
    Kingdom kingdom,
    GovernorSettlementContext context,
    IReadOnlyCollection<Hero> availableHeroes);

Hero GetBestOtherClanCandidate(
    Kingdom kingdom,
    GovernorSettlementContext context,
    IReadOnlyCollection<Hero> availableHeroes);
```

两者统一先判断 Hero 硬资格、赴任锁定、地方官职兼容和 `EffectiveGovernorAbility >= RequiredAbility`。

第一阶段只搜 `kingdom.RulingClan`，按 `Ability DESC → 原 Settlement 现任优先 → Hero.StringId`。

第二阶段搜其他 Clan（含非统治 `PlayerClan`），按：

```text
ClanTier ASC
→ OfficeCountOfClan ASC
→ InfluenceBand ASC
→ ClanInfluence ASC
→ FiefWeight ASC
→ Ability DESC
→ 原 Settlement 现任优先
→ Hero.StringId
```

`HeroOfficeBehavior` 增加：

```csharp
public int GetOfficeCount(Clan clan)
{
    if (clan == null) return 0;
    return _assignments.Count(x => x?.Hero?.Clan == clan);
}
```

返回中央+地方官职总数，不计入 Governor 本身；`_assignments` 以当前已保存的有效官职为准，必要时在读取前复用其现有清理逻辑。

## 8. 全局规划、赴任锁定与地方官职

计划输入为 Kingdom 本轮全部 Town/Castle、候选 Hero 和**固定的锁定集**：

- 若 `HasIncomingGovernor(town)` 为真，则锁住 `town` 及其正在赴任的 Hero；保留既有目的地，不重新选人。
- 已有 Governor **不是** Traveling 且仍合法者进入可调度池。
- 规划期生成 `Dictionary<Town, Hero> TargetAssignment`，所有 Hero 最多使用一次；不调用 `ChangeGovernorAction`。
- 差异计算之前验证：目标唯一、Kingdom 归属正确、赴任锁定未破坏；具地方官职的 Hero 不得被调到不兼容的城镇/城堡，也不得因此失去唯一有效 Governor 岗位。
- 若发现地方官职/赴任约束冲突：固定受约束的现任目标，移除冲突后重算剩余岗位；不靠执行阶段强行撤职制造无效 OfficeAssignment。

**现任稳定性**：按词典排序最后将“当前 Settlement 的现任 Hero”作为优先项；核心政治排序或能力顺序胜出的 Hero 仍可取代现任，不对所有任命一概上锁。

## 9. 差异执行及失败保护

`GovernorAssignmentExecutor` 将规划分成 `unchanged / newlyAppointed / moved / removed`，并在修改前记录每个 Settlement 的 `CurrentGovernor` 快照，供关系及通知计算。

执行次序：

1. 对所有实际发生变化的旧岗位先调用 `ChangeGovernorAction.RemoveGovernorOf...`。
2. 再对所有需要任命的目标调用 `ChangeGovernorAction.Apply(town, hero)`。
3. 每次变更后确认本体状态：`town.Governor == hero` 或 `hero.GovernorOf == town`（包括可能存在的赴任中状态）。
4. 部分失败时**不按尚未成功的计划结算关系/通知**；记录 Warn，并优先恢复受影响旧岗位，恢复失败则保留真实状态并交给下轮处理。
5. 成功结果集中传给关系服务和通知服务，避免从低层 Action Hook 里重复结算。

`RemoveInvalidBorrowedGovernors()` 继续负责清理跨王国的非法借用；此类撤销作为**自然/合法性失效**，不记为君主主动撤职。

**君主手动任命入口**：保留现有 `KingdomSettlementGovernorVMMixin` 的权限与选择 UI，但将最终动作改走统一的执行/通知服务。手动任命按实际变化立即结算，WeeklyTick 可以依既定算法重新优化；避免手动和自动两套日志与关系算法。

## 10. 关系结算服务

`GovernorRelationService` 在行动前取得快照、行动后取得真实结果，按 Clan 汇总。只影响 `kingdom.RulingClan` 与受影响 Clan；自己 Clan 不结算。

```csharp
int prosperityBonus = Math.Min(Math.Max(prosperityBand, 0), 5);
int borderBonus = isBorder ? 2 : 0;

int AppointmentRelation(Town town) =>
    (town.IsCastle ? 5 : 12) + prosperityBonus + borderBonus;

int DismissalRelation(Town town) =>
    -((town.IsCastle ? 8 : 18) + prosperityBonus + borderBonus);
```

注意取本轮已记录的 SettlementContext，保证一轮中基准一致。按以下顺序计算每个受影响 Clan：

1. 剔除相同 Settlement 且 Clan 未改变的保留岗位（同 Clan Hero 对换不产生关系变化）。
2. 将仍需计算的旧、新岗位按任命收益排序，依序一对一配对，配对岗位 `Δ = AppointmentRelation(new) - AppointmentRelation(old)`。
3. 多余新岗位按 `AppointmentRelation`；多余失去的旧岗位按 `DismissalRelation`。
4. `Δ` 求和，只对该 Clan **提交一次关系变化**；异常/死亡/离国等自然解除从“主动撤职”集合排除。

本体对 Clan 关系的读取可用 `FactionManager.GetRelationBetweenClans(rulingClan, clan)`；实现写入时沿用已有 `ChangeRelationAction.ApplyRelationChangeBetweenHeroes(rulingClan.Leader, clan.Leader, delta, true)`，但**业务接口始终按 Clan ↔ Clan** 表达（本体 `Clan.GetRelationWithClan` 也代理至 Clan Leader 关系）。

自然失效与主动撤职区别应以记录的事件原因/合法性为准，不应只看本轮 `CurrentGovernor != TargetGovernor` 就全部扣罚。

## 11. 日志实施

```csharp
bool ShouldLogForKingdom(Kingdom kingdom) =>
    kingdom != null && Clan.PlayerClan?.Kingdom == kingdom;
```

对该王国输出 `Info`：开始、结束汇总、任命、调任、撤职、关系变化；`Debug`：无人达标、被锁定原因；`Warn`：实际应用失败、恢复失败。**其他王国的总督分配日志不写入文件也不上屏。**

沿用 `ModifiedPolitics.Tool.ModLogger`（源文件 `Utils/ModLogger.cs`）。默认屏幕阈值为 Notice，文件阈值为 Info，因此普通 Info 不刷屏；不要额外使用 Notice/DisplayMessage 重复弹任命文字。单周只打印一条汇总，不展开每名 Hero 的技能计算、逐个未变职位。

`KingdomSettlementGovernorVMMixin` 现有手动任命 `ModLogger.Notice` 应整合为统一日志口径，避免与新地图事件重复。

## 12. 玩家 Clan 右侧圆形 MapNotification

**本体依据**：`code/MakePeaceKingdomDecision.cs` 的 `OnShowDecision()` 使用 `Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new PeaceOfferMapNotification(...))`。`code/GUI/SandBox/GUI/Prefabs/Map/MapNotificationUI.xml` 和 `MapNotificationItem.xml` 已具有右侧圆形事件容器与图标、移除、展开交互。

新增：

- `GovernorAppointmentMapNotification : InformationData`：保存任命类型、Hero、原 Settlement、目标 Settlement、描述文本；覆写标题/音效（与目标游戏版本的基类签名匹配）。
- `GovernorAppointmentMapNotificationVM : MapNotificationItemBaseVM`：指定 `NotificationIdentifier`、文本、图标；`_onInspect` 定位目标 Settlement（通过本体地图定位委托；当前版本委托参数 `Vec2`/`CampaignVec2` 需和项目 DLL 核对），支持关闭。
- `GovernorNotificationRegistrationPatch`：优先在 `MapNotificationVM.PopulateTypeDictionary` Postfix 使用公开 `RegisterMapNotificationType(typeof(data), typeof(vm))` 注册；与实际游戏版本核对 Harmony 目标是否存在。避免依赖私有 `_itemConstructors` 字段。
- `GovernorNotificationSaveDefiner`：为 `InformationData` 子类注册唯一存档类型 ID；检查与现有 `HeroOfficeSaveDefiner` 的模块 ID 不冲突，确保未读通知可存档。
- GUI：新增独立命名的 Brush/Sprite 或复用现成图标。避免直接覆盖本体同名 `MapNotification.xml`，否则会与别的 Mod 冲突。
- `GovernorNotificationService`：仅在执行结果真正成功且 `hero.Clan == Clan.PlayerClan`，并且是**新增任命或改变 Settlement 的调任**时调用：

```csharp
Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(
    new GovernorAppointmentMapNotification(...));
```

同 Hero 同一轮最多一条；留任或其他 Clan 任命不通知。点击圆形图标定位城镇/城堡，不打开全屏强制弹窗。文本放入中文本地化 XML；切换语言仍可使用英文回退文本。

**注意**：通知类是计划新增的 Mod 类型，本体只有 `PeaceOfferMapNotification` 等内置类型，并不存在现成 `GovernorAppointmentMapNotification`。

## 13. 落地步骤与验收用例

实施顺序：

1. 拆出 Context、Border、能力门槛与候选选择；补 `GetOfficeCount(Clan)`。
2. 新增 Planner，得到完整且可校验的目标映射；完成赴任/地方官职锁定。
3. 重构 WeeklyTick Behavior，取消立即分配并接入可保存的 7 天首轮时间。
4. 引入 Executor 和 RelationService，衔接君主手动任命入口。
5. 加仅玩家王国日志、PlayerClan MapNotification、存档类型/本地化/图标。
6. 编译并通过以下场景回归测试。

| 用例 | 预期结果 |
|---|---|
| 新游戏第 0～6 天 | 不自动分配；第 7 天及以后首个 WeeklyTick 执行 |
| 非中央制文化 | 完全保留 Vanilla 行为，不调任 |
| 玩家主队有同伴（非队长） | `PartyBelongedTo != null`，不入候选 |
| 玩家同伴离开所有 Party | 其他资格合格即可入候选 |
| 玩家 Clan 不是 RulingClan | 可参加其他 Clan 竞选；不能越过政治排序 |
| 已有 Governor | 进入候选；符合优势时可调任/撤换 |
| A 城→B 城、B 城→A 城 | 规划不重复 Hero；先卸任后任命成功 |
| 正在赴任 | 原目的地与 Hero 锁定，不二次派遣 |
| 没人达到 RequiredAbility | 该 Settlement 空缺，不阻塞后续 |
| RulingClan 有合格 Hero | 先使用统治 Clan Hero |
| Town/Castle 地方官职限制 | 不调任到不兼容职位、不产生无效 Office |
| 新增、撤职、平级/升降调任 | 关系数值正确，按 Clan 一次结算 |
| Hero 死亡或 Clan 离国 | 自然失效不计主动撤职惩罚 |
| 其他 Kingdom WeeklyTick | 正常执行但不写总督日志、不弹玩家通知 |
| PlayerClan Hero 任命/调任 | 右侧圆形事件出现一次、点击可定位、可关闭 |
| 存档/读档 | 7 天首轮门槛与未读事件保持正确 |
| 任命动作失败 | 不伪造关系/通知，日志 Warn，状态可恢复 |

## 14. 当前仓库与参考代码

- 当前中央分配：[KingdomGovernorAssignmentBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/Governor/Behaviors/KingdomGovernorAssignmentBehavior.cs)
- 当前分数：[GovernorAssignmentModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/Governor/Models/GovernorAssignmentModel.cs)
- 配置：[GovernorPolicyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/Governor/Config/GovernorPolicyManager.cs)
- 手动任命：[KingdomSettlementGovernorVMMixin.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/KingdomFief/UI/KingdomSettlementGovernorVMMixin.cs)
- 官职记录：[HeroOfficeBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/HeroOfficeBehavior.cs)
- 边境定义：[GarrisonWageLimitModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonWageLimitModel.cs)
- 求和通知：[MakePeaceKingdomDecision.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/MakePeaceKingdomDecision.cs)
- 通知 UI：[MapNotificationItem.xml](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Prefabs/Map/MapNotificationItem.xml)
- 自定义通知实现参考：[Bannerlord Modding — Custom Round Popup](https://www.bannerlordmodding.lt/guides/custom_round_popup/)
- 通知类型注册公开 API：[MapNotificationVM (API)](https://apidoc.bannerlord.com/v/1.2.12/class_tale_worlds_1_1_campaign_system_1_1_view_model_collection_1_1_map_1_1_map_notification_v_m.html)

## 15. 实际落地说明

- 新增 Context、边境判定、候选选择、全局 Planner、两阶段 Executor、Clan 关系结算及通知服务。
- 新游戏首轮延迟时间通过 `CampaignTime` 保存；旧存档缺少字段时从下一次 WeeklyTick 开始执行。
- 正在赴任的 Hero/Settlement 与已有地方官职的现任岗位会被锁定，避免自动规划破坏有效官职。
- 自动与君主手动任命共用差异执行、关系结算和玩家家族通知。
- 通知复用本体已有的圆形军团图形标识，不覆盖本体 Prefab 或 TPAC。
- 异地任命通过 `ITeleportationCampaignBehavior.GetTargetOfTeleportingHero()` 验证；不能仅检查 `Town.Governor` 或 `Hero.GovernorOf`，因为赴任阶段这两个字段都可能尚未指向目标岗位。
- 本体没有公开的“撤销总督赴任”接口，因此执行阶段不再对已发出的旅行命令做伪事务回滚；真实拒绝项保留实际状态并交给下周重新规划。
- 当前按约定未由 Codex 编译；API 形状已对本机安装 DLL 做只读核对。
