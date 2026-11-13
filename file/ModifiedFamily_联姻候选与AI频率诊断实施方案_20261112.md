# ModifiedFamily：联姻候选20人过滤与 AI 婚姻过频诊断实施方案

基线：`GuihuaZhou/bannerlord` 的 GitHub `main` 分支，2026-11-12 核对。只提供 Codex 执行说明，不提交 PR。原则：P0 只改日志，维持「同 Clan 文化或同王国」的联姻硬约束。P1 再基于数据调整 AI 婚姻频率/接受机制，不把调参和日志补丁混为一体。

## 观察到的症状与统计口径

当前 UI 日志：`examinedClans=111, usableClans=87, oppositeGenderHeroes=709, negotiableHeroes=20, displayedClans=0, displayedHeroes=0; UnderAge=428, AlreadyMarried=246, InvalidClan=24, CultureForbidden=20, Dead=15`。

- `709` 是 **针对当前玩家所选 Hero 的异性人选**，并非全大陆贵族人数；`20` 是经过 `CanNegotiateHero()` 的基础协商资格筛选且同样满足异性条件者，并非全地图总未婚人数。
- 按本次互斥首要拒绝原因，排除未成年/死亡者后，246 已婚、20 可协商，即在当前筛选到的 266 名存活且适龄的异性人物中约 92.5% 已婚。这是强烈的耗尽信号，但还不能分辨预设婚姻、原版其他机制、Mod 成婚和玩家行为。
- `467` 是存档的提议记录总数（包括反提议、失效等），不等于 467 次婚姻。

## P0-1：为每个基础资格通过的 Hero 写最终结果日志（优先级最高）

### 已存在代码与真实盲区

文件：`ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageNavigationVM.cs` 的 `LoadClans()`。

现有 `UI_BEGIN`、`CLAN_CHECK`、`HERO_CHECK`、`DIRECTION_CHECK`、`UI_SUMMARY` 可以追踪部分候选，但细项日志受以下条件限制：

```csharp
bool detailHeroes = diagnostics && (sameClanCulture || sameKingdom || sameHeroCulture);
```

后续 `WriteHeroCheck` 和 `WriteDirectionCheck` 均仅在 `detailHeroes` 下调用。这使得处于 `other` 分组、同时也不同 Hero 文化的家族，即使有通过 `CanNegotiateHero` 的异性 Hero，仍可能只见 `CLAN_CHECK`，**看不到那20名最终 `CultureForbidden` 者的姓名、身份及两个方向的拒绝链**。

### 最小修改要求

在 `foreach (Hero target in clan.Heroes)` 中，当 `genderMatch && heroResult.IsAllowed` 时，沿用当前真实的 `normal` 和 `matrilocal` 计算结果，无论 `detailHeroes` 是否为 true，都记录一条 **`BASE_ELIGIBLE_FINAL`** 日志：

- `uiTraceId`、`ownHeroId`、`ownHeroCultureId`、`playerClanId`、`playerClanCultureId`、`playerKingdomId`
- `targetHeroId`、`targetHeroName`、`targetAge`、`targetIsFemale`、`targetHeroCultureId`
- `targetClanId`、`targetClanName`、`targetClanCultureId`、`targetKingdomId`
- `sameClanCulture`、`sameKingdom`、`sameHeroCulture`、`atWar`
- `normalAllowed`、`normalReason`、`matrilocalAllowed`、`matrilocalReason`
- `normalDestinationClanId`、`matrilocalDestinationClanId`
- `included` = `normal.IsAllowed || matrilocal.IsAllowed`
- `finalReason`：如果两个方向均被拒绝，记录两者的 reason 及最先触发的 reason；**不要只归类为 CultureForbidden**，尽量保留两方向完整结果。
- `activeProposalId` / `pairCooldown` 若有既有只读 API，可选增加；不要通过新的检测逻辑修改锁/冷却状态。

同一个 UI 刷新必须以 `uiTraceId` 关联；该事件理论上应有 **20 条**（当前存档下），逐人解释 20→0。复用已经算好的 `normal/matrilocal`，不要额外重复调用 `CanCreateMarriageProposal()` 造成不必要负担。保留现有详细追踪的原行为。`MarriageDiagnosticTrace.Enabled` 关时，不增加昂贵全量计算。

在 `UI_SUMMARY` 追加：`baseEligibleCount`、`baseEligibleIncluded`、`baseEligibleExcluded`、`baseEligibleReasonCounts`，以及按同国同文化/异国同文化/同国异文化/其余四组汇总基础合格人数与最终人数。**禁止为了日志修改过滤或联姻方向判断。**

示例（期望格式）：

```text
[MF_DIAG][BASE_ELIGIBLE_FINAL] uiTraceId=... | targetHeroId=... | targetClanId=... | targetClanCultureId=empire | targetKingdomId=... | playerClanCultureId=vlandia | playerKingdomId=western_empire | sameClanCulture=false | sameKingdom=false | atWar=false | normalAllowed=false | normalReason=CultureForbidden | matrilocalAllowed=false | matrilocalReason=CultureForbidden | included=false
```

### 验收

- 在同一存档、同一个玩家 Hero 打开候选界面，`BASE_ELIGIBLE_FINAL` 行数与 `negotiableHeroes` 精确一致（当前应为20）；`included=true` 行数与 `displayedHeroes` 一致（当前应为0）。
- 每条都带 `uiTraceId + heroId + clanId + 2个方向`；相同 `uiTraceId` 的汇总计数可与细项核对。
- 明确展示玩家 `Clan.Culture` 与 `Hero.Culture` 的差异：硬约束使用 **Clan.Culture**。
- 诊断开启/关闭前后，候选列表及联姻合法性完全一致。

## P0-2：把“全地图已婚耗尽”转为可量化数据

文件：`ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs`，建议新增独立无副作用 `MarriagePopulationDiagnostics` 或聚合静态函数，由 CampaignBehavior 统一触发。

触发：读取存档完成后在 `OnSessionLaunched` 输出一次 `WORLD_MARRIAGE_SNAPSHOT`；诊断开关开启时，每个**游戏月/约定间隔**输出一次即可，避免每个 NPC Clan 日更刷屏。所有统计只读。使用 `Clan.All.SelectMany(c => c.Heroes).Distinct()` 并限定 `h.Clan == c`、`h.IsLord`、非死亡、非 template、非 minor NPC；记录总成年人数、成年已婚/未婚、男女分布、不同年龄段（例如18–25/26–35/36+）。注意对 `Spouse != null && Spouse.IsDead` 单独统计：现有 `CanNegotiateHero()` 会将其视为已婚，**此阶段仅记录，暂不改规则**。

要求按 **王国和 Clan 文化** 分组，单列西帝国、瓦兰迪亚的成年未婚男女及与玩家 Hero 异性人选；同时区分 `CanNegotiateHero()` 的真实资格与一般 `Spouse == null` 的未婚人数。

追加存档提议统计 `PROPOSAL_SNAPSHOT`：

- 历史记录 `recordCount`，婚约数量按 `ChainId` 去重；反提议 (`SupersededByCounter`) **不能当成新婚姻**。
- 每种 `FamilyProposalStatus` 的记录数，包括 `Completed`、`Rejected`、`TimedOut`、`Invalidated`、`AcceptedPendingExecution`、`ExecutionFailure` 等。
- `completedUniqueChains` 用每条 Chain 中是否包含 `Completed` 记录判定；对仍处于主动中的 Chain 不计为完成。
- `initiatedByAiUniqueChains`；按 `CreatedAt` 分别统计过去1个游戏年、过去3个游戏年 AI 发起的独立 Chain 数。`CreatedAt` 可计算**发起时间**，不可当作婚姻**完成时间**。
- 将历次统计中的真实完成事件（已有 `MARRIAGE_EXECUTED`）增加非玩家相关的聚合计数，供**从现在开始**记录近一个游戏年完成数量，或者每隔一个游戏月输出 `MARRIAGE_EVENTS_PERIOD`。旧数据缺少 `CompletedAt`，**不要伪造历史完成时间**。

样例：

```text
[MF_DIAG][WORLD_MARRIAGE_SNAPSHOT] totalAdult=... | marriedAdult=... | singleAdult=... | singleMale=... | singleFemale=... | eligibleSingle=... | deadSpousePointer=... | westernEmpireSingle=... | vlandiaSingle=...
[MF_DIAG][PROPOSAL_SNAPSHOT] records=467 | uniqueChains=... | completedUniqueChains=... | pendingChains=... | rejectedChains=... | aiInitiatedChains=... | createdLastYear=...
```

## P0-3：记录全大陆 AI 决策漏斗（不能只记录玩家相关）

文件：`ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs`。

现有 `MarriageDiagnosticTrace.IsPlayerRelated(...)` 把 `AI_DECISION`、`AI_ACCEPT_RESULT`、`AI_COUNTER_RESULT` 等限制为涉及玩家家族的婚约，**不足以分析 AI↔AI 的高婚姻频率**。

启用诊断时应只做**聚合**：每游戏月/年输出 `AI_MARRIAGE_FUNNEL`，至少包括：

- 被轮询家族数/实际尝试家族数、因已有主动提议跳过数、可谈判家族数、有效候选数。
- 创建成功提议数、过期待响应数、AI 接受数、反提议数、拒绝数、作废数、婚姻实际完成数。
- 对接收方分开统计 `receivesHero=true/false`、`gift=0/>0`，对评分输出平均值及高于接受阈值的比例。
- 尽量同时给出按王国/文化分组的发起和完成数，便于判断西帝国和瓦兰迪亚是否已过度联姻。

必须区分：`TryAccept` 调用 vs 成功返回、`AcceptedPendingExecution` vs `Completed`。一个婚约链可能有反提议，统计婚姻数按 ChainId 去重。聚合日志不要逐日输出数百个家族的详细打分。

## 代码结论与后续 P1 设计（先诊断，再平衡）

- `FamilyCampaignBehavior.OnDailyClan()` 每日令 NPC 家族考虑联姻。
- `ConsiderClan()` 仅以家族当前有未完成主动提议来限制新提议；`FamilyConfig.PairCooldownDays=60` 是 **一对 Hero 的冷却**，不是家族级联姻间隔。
- `FamilyConfig.ReplyDays=7`；本次提议处理完后若还有可婚人物，家族又可重新尝试。
- `MarriageScoreTuning.AcceptanceThreshold=0`，而 `ClanHeroValueModel.Evaluate` 对**接收成员**的一方：有正值的人物收益 +55固定联盟收益 +20至少一种文化/王国加成，关系最多扣30，无礼金时**至少 +65**（`ValueToClan` 允许最小20）；所以**满足硬约束且无礼金、接收人选的一方会确定性接受**。这是确定性阈值，没有随机接受概率。
- 当前 AI 寻找候选时允许接收方分数仅 ≥`CandidateReceiverFloor=-40`，因此仍可能产生反提议与拒绝。不能把“发起提议”当成“必定结婚”。
- 现有原版随机 NPC 联姻 `RomanceCampaignBehavior.CheckNpcMarriages` 已被 `VanillaNpcMarriageSearchPatch` 截断，因此默认情况下主要依赖 ModifiedFamily 的 NPC 联姻搜索（须留意其他 Mod 影响）。

P1 修改方向（**本次先不做**）：

1. 把 AI 每日“有空就主动求婚”改为低频联姻意愿判断，使用**游戏年**为单位（可用 `CampaignTime.DaysInYear`/`CampaignTime.YearsFromNow`），不是现实天数。
2. 增加以 Clan 为单位、可跨存档保存的下一次尝试窗口与上次成功窗口；失败和成功分别限制，避免保存/载入刷新冷却。不要把政治资格从「同文化或同王国」改成其他规则。
3. 对 AI→AI 提议加入按家族规模、婚配稀缺程度、年龄、亲缘、继承、外交需求的动机评分；避免固定 `AllianceBenefit=55` 让所有婚姻都有高收益。
4. 改变接受逻辑：确定性权衡合法性和实际利益后，可以按分数映射接受几率/政治机会；只靠调高 `AcceptanceThreshold` 不够。是否使用随机要防重复存读档刷概率，决定要一次做出并落盘。
5. 确保玩家主动联姻不受 NPC 主动求婚的随机发起门槛误伤，婚后归属与原有子女不随迁机制不变。

## 测试清单

- 使用出现“20→0”旧存档：打开玩家 Hero 候选页，验证 `BASE_ELIGIBLE_FINAL` 精确20行、全字段、全部最终理由及 `UI_SUMMARY` 对账。
- 诊断开启前后候选人物列表完全一致，不引入二次校验副作用。
- 输出全图成年婚姻分布（含西帝国/瓦兰迪亚），按 `ChainId` 对467记录去重，统计真正完成数。
- 前进一个游戏月，验证 AI↔AI 发起/拒绝/接受/完成漏斗，日志体积可控；还要在保存/读档后检查聚合不重复累计。
- 最终给出“筛选规则真的异常 / 只是资源耗尽 / 同时存在两问题”的结论及源码证据，不得仅凭467记录直接推断467成功婚姻。

## 关联源码

- `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageNavigationVM.cs`
- `ModifiedFamily/ModifiedFamily/Diagnostics/MarriageDiagnosticTrace.cs`
- `ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs`
- `ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs`
- `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs`
- `ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs`
- `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageScoreTuning.cs`
- `ModifiedFamily/ModifiedFamily/Core/FamilyConfig.cs`
- `ModifiedFamily/ModifiedFamily/Marriage/Patches/VanillaNpcMarriageSearchPatch.cs`
