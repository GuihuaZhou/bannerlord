# ModifiedFamily 联姻问题——纯诊断日志实施任务书

> 目标：给开发 AI / Codex 直接执行。**只增加日志和诊断开关，不改联姻资格、评分常量、AI 策略、候选顺序、婚约状态机或存档格式；无需提交 PR。**
>
> 代码基线：`GuihuaZhou/bannerlord` GitHub 当前 `main` 分支（2026-11-12 读取）；实际修改前须重新检查工作区差异和相关文件。GitHub: https://github.com/GuihuaZhou/bannerlord

## 一、用户场景与待验证假设

玩家主角选择**瓦兰迪亚文化**，玩家 Clan 已**加入西帝国**。按设计，联姻可以面向：

1. 与玩家 Clan **同文化**的贵族家族（即使不同王国）；
2. 与玩家 Clan **同王国**的贵族家族（即使文化不同）；
3. 还须满足战争、家族类型、候选人物、婚约锁、婚姻方向等原有规则。

现实表现：列表只看见很少的西帝国家族、瓦兰迪亚家族未出现；可发出的婚约在 AI 方似乎很容易接受。不要擅自将“角色文化=瓦兰迪亚”推断为“玩家 Clan.Culture=瓦兰迪亚”。不要将“发送了提议”“AI 接受”“婚礼完成”混为一种成功。

## 二、已确认的源码事实（链接均为 main 当前路径）

1. [`MarriageProposalModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageProposalModel.cs)：`CultureAndKingdomAllowed(Hero a, Hero b)` 使用 **`a.Clan.Culture == b.Clan.Culture`** 或 `sameKingdom && AllowSameKingdomDifferentCulture`，不是比较 `Hero.Culture`。
2. [`MarriagePolicyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriagePolicyModel.cs)：`AllowSameKingdomDifferentCulture = true`。
3. [`MarriagePoliticalValidationModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriagePoliticalValidationModel.cs)：先检查 Clan 合法，再检查双方战争、文化/王国、所属 Clan 是否变化。
4. [`MarriageEligibilityModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageEligibilityModel.cs)：`CanUseClan` 会拒绝无王国、被消灭、叛军、强盗等；NPC 小派系与雇佣兵也被拒绝。`CanNegotiateHero` 拒绝已有配偶、非贵族、未成年等，但囚禁、失踪、战斗、军团等是**临时阻碍**，不是自动 `IsAllowed=false`。
5. [`MarriageNavigationVM.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageNavigationVM.cs)：`LoadClans()` 扫描 `CampaignClan.All`，只展示至少一名人物使 `CanCreateMarriageProposal` 的某个方向允许的家族；已输出总数与 `rejectionReasons`，但不能解释具体哪个 Clan/人被拒。
6. [`FamilyProposalService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs)：`CanCreateMarriageProposal` 只读检查；包含血亲、婚约锁 `HeroLocked`、两人冷却 `PairCooldown` 等。`CreateMarriageProposalPreview` 会构造临时 proposal，新增日志不能写入提议列表。
7. [`ClanMarriageDecisionService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs)：`Respond()` 用 `MarriageScoreTuning.AcceptanceThreshold` 作确定性比较；低分尝试反提议然后拒绝。`ConsiderClan()` AI 搜索只取每 Clan 前3人、前8目标 Clan、各目标前3人（**不可因增加日志改这个范围**）。
8. [`ClanHeroValueModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs)：分项为 Inflow、Outflow、Succession、Alliance、Relation、Culture、Kingdom、Gift。
9. [`MarriageScoreTuning.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageScoreTuning.cs)：`AcceptanceThreshold=0`、`AllianceBenefit=55`、`SameCultureBenefit=20`、`SameKingdomBenefit=20`、`FemaleOutflowMultiplier=1`、`MaleOutflowMultiplier=1.5`、`SuccessionPenalty=80` 等。**本任务不得调参**。
10. [`FamilyCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs)：全局每日处理到期提议、执行婚姻；同意但尚未执行的状态必须和最终 `Completed` 区分。
11. [`FamilyDiagnostics.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Diagnostics/FamilyDiagnostics.cs) + [`ModLogger.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Tool/ModLogger.cs) + [`Settings.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Utils/Settings.cs)：已有双目标日志系统，`FileMinLogLevel` 默认 Info，`Screen MinLogLevel` 默认 Notice；`DebugLog` 不会默认写入文件。日志输出 `Documents/Mount and Blade II Bannerlord/Configs/ModLogs/ModifiedFamily.log`。
12. [`FamilyLogText.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/Localization/FamilyLogText.cs)：已有 `MarriageEvaluation()` 的详细评分和 `MarriageDecision()` 的状态说明；不要简单重复这些长日志。

## 三、架构及配置要求

- 仅在 `ModifiedFamily` 内工作。候选采集放在 UI 入口 `MarriageNavigationVM`；AI 评分采集放在 `ClanMarriageDecisionService`；生命期结果放在已有 `FamilyProposalService`/`FamilyCampaignBehavior`/`MarriageExecutionService` 的现有实际状态转换点。**不要**在 `MarriageEligibilityModel`、`MarriageProposalModel` 等共享纯校验的每次调用内直接打日志：它们被 UI 和 AI 高频调用。
- 在 `Utils/Settings.cs` 增加一个 MCM `bool EnableMarriageDiagnostics`，默认 `false`，`RequireRestart=false`，在 Logging 组；中文本地化到 `ModuleData/Languages/CNs/modifiedfamily-zho-CN.xml`。如有必要，可增加 `TraceAllAiMarriageSearch`（默认 false），但不是 P0；一般先只记录与玩家 Clan 有关的 AI 评分和结果。
- 新建轻量 `Diagnostics/MarriageDiagnosticTrace.cs`（内部类），采用 `if (!Enabled) return;` **先判开关，再格式化复杂字符串**。调用已有 `FamilyDiagnostics.DebugLog`/`ModLogger.Debug`，不另起文件、不引入新日志库、不改变现有 logger 默认行为。
- 验证时手动设置 **MCM 文件日志 Debug、屏幕日志 Notice**；否则文件中看不到 `DebugLog`。勿把海量跟踪提升到 Info/Notice/Warn，更不应默认在游戏 HUD 刷屏。
- 统一机读格式：`[MF_DIAG][EVENT] key=value | key=value`。键名英文稳定，枚举和 ID 优先，姓名仅作补充。字符串包含换行、竖线时转义。建议写 `gameTime`（仅使用项目/游戏 API 确认可用的表示）、`uiTraceId`（每次界面会话唯一，**不持久化**）或 `ProposalId`+`ChainId`（婚约事件关联）。
- 全部通过 `?.` 处理空引用；不要在日志计算中调用有副作用的游戏 Action。使用 `Clan.StringId`、`Hero.StringId`、`Culture.StringId`、`Kingdom.StringId`，同时保留可读 `Name`。
- 不允许将诊断结构加入 `SaveableProperty` 或 `SyncData`，避免存档兼容风险。

## 四、P0：候选名单完整诊断（最重要）

目标文件：`ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageNavigationVM.cs`

### 4.1 `LoadClans()` 起点

写一次 `UI_BEGIN`，至少包括：

- `uiTraceId`、`ownHeroId`、`ownHeroCultureId`、`mainHeroCultureId`；
- `playerClanId`、`playerClanCultureId`、`playerKingdomId`；
- 玩家 Clan 的 `Kingdom` 为空与否、`IsUnderMercenaryService` 等有用旗标；
- `activeProposalId`、`activeProposalStatus`（若现有锁提前结束全列表）；
- 是否 `AllowSameKingdomDifferentCulture`。

**判断目标**：直接回答玩家选了瓦兰迪亚文化、为何实际按不同 Clan 文化筛选。

### 4.2 遍历 `CampaignClan.All` 时打印 `CLAN_CHECK`

无论通过或拒绝，在打开名单时每个 Clan **最多一条汇总**，包括：

- `clanId`、`clanName`、`cultureId`、`kingdomId`；
- `sameClanCulture`、`sameKingdom`、`sameHeroCulture`（仅诊断比较）、`atWar`；
- `isEliminated`、`isBanditFaction`、`isRebelClan`、`isMinorFaction`、`isClanTypeMercenary`、`isUnderMercenaryService`、`hasKingdom`；
- `canUseClan`、`canUseReason`；
- 可进一步填写 `oppositeGenderCount`、`negotiableCount`、`eligibleCount`、`displayed=true/false`、`primaryDropReason`。

现有 `CanUseClan` 很多失败只返回通用 `InvalidClan`，上述旗标必须打印；否则无法区分“无王国”与“雇佣兵”等。

### 4.3 对每个潜在候选人打印 `HERO_CHECK`

只在打开联姻列表时输出、不在每帧输出：

- `heroId`、`heroName`、`heroClanId`、`heroCultureId`、`age`、`isFemale`、`spouseId`、`spouseIsDead`（如果适用）；
- `isLord`、`isNotable`、`isMinorFactionHero`、`isTemplate`、`isActive`、`isPrisoner`、`isFugitive`、`inBattle`、`inArmy`；
- `genderMatch`、`negotiableAllowed`、`negotiableReason`、`temporaryBlockers`；
- 以及按当前路径得到的排除原因。特别注意 `IsAllowed=true` 仍可能有 temporary blockers。

可以只对与玩家 Clan 同文化或同王国的家族展开逐人日志，外加所有因资格校验被剔除的家族汇总，避免对无关家族写几千条。如果要完整重建列表，所有扫描过的人逐个记录，但只在 UI 构建时进行一次。

### 4.4 对每名合格异性候选记录两个方向 `DIRECTION_CHECK`

对应 `ValidateDraft(target, MarriageDirection.FemaleJoinsMaleClan)` 与 `MaleJoinsFemaleClan`：

- `direction`、`destinationClanId`、`allowed`、`reasonCode`、`temporaryBlockers`；
- `sameCulture`、`sameKingdom`、`atWar`，便于与合法性结果对照。

**重要：保留原有短路判断。** 当前代码中 `matrilocal = normal.IsAllowed ? normal : ValidateDraft(...Matrilocal)`，若正常方向已合法，实际没有评估另一方向。诊断开启时可做一次额外的**纯只读** `CanCreateMarriageProposal` 检查来补全另一方向的日志，但该结果不能参与 `candidateCount` 或列表排序，也不能创建实际婚约。日志关闭时仍走原先路径。

### 4.5 `LoadClans()` 结束输出 `UI_SUMMARY`

现有 `examinedClans, usableClans, oppositeGenderHeroes, negotiableHeroes, displayedClans, displayedHeroes, rejectionReasons` **保留不变**，另外分桶：

- `sameKingdomAndCulture`、`crossKingdomSameCulture`、`sameKingdomDifferentCulture`、`other` 的 **总家族数/通过家族数/有效人物数**；
- `cultureForbidden`、`atWar`、`invalidClan`、`alreadyMarried`、`underAge`、`heroLocked`、`pairCooldown`、`leaderCannotLeave` 等排除数量；
- 重点明确“全部瓦兰迪亚家族检查了多少，通过多少，具体为什么被过滤”。

不要只输出 `CultureForbidden=XX`，必须可追查到 `clanId + heroId + reason`。

## 五、P0：AI 评分、接受与婚姻完成的可追溯日志

### 5.1 `ClanMarriageDecisionService.Respond()`

已有 `MarriageEvaluation` 分项 Debug 日志。新增一条紧凑的 `AI_DECISION` **在执行选择之前**：

- `proposalId`、`chainId`、`parentProposalId`、`counterCount`；
- `proposerClanId`、`receiverClanId`、`heroAId`、`heroBId`、`destinationClanId`；
- `direction`、`goldPayerClanId`、`giftAmount`；
- `ownAdultCount`、`receivesHero`、`scoreTotal`、`acceptanceThreshold`、`scoreMinusThreshold`；
- `baseHeroValue`、`inflow`、`outflow`、`succession`、`alliance`、`relationRaw`、`relationValue`、`cultureValue`、`kingdomValue`、`giftValue`；
- `plannedDecision=Accept/TryCounter/Reject/Invalidated` 和 `reason`。

要同时记录**调用后的实际结果**：`TryAccept` / `TryReject` 返回的 `IsAllowed`、`ReasonCode`、proposal 更新后的 `Status`。当前 `Respond()` 调用 `TryAccept()` 后直接记录 `Accepted`，日志改造应明确“计划接受”和“动作结果”两回事。可通过保存返回值以供日志使用，但不能改变原有分支行为。

### 5.2 `ClanMarriageDecisionService.TryCounter()`

添加 `AI_COUNTER_RESULT`：`sourceProposalId`、`newProposalId`（若创建成功）、`chainId`、`directionsCompared`、`gift`、`ownScore`、`otherScore`、`counterThresholds`、`reason`、`newStatus`。不要对每个试探方案都打一整条长日志，最多记录候选方案汇总和最终选择。

### 5.3 `FamilyProposalService` / `FamilyCampaignBehavior` / `MarriageExecutionService`

尽量在实际已经存在的状态转换点记录一次事件：

- `PROPOSAL_CREATED`：创建是否成功、方向、礼金、`ProposalId`、`ChainId`、来源 Player/AI。
- `RESPONSE_DUE`：到期交给 AI 判定或等待玩家响应；区分临时阻碍与硬失效。
- `PROPOSAL_ACCEPTED`、`PROPOSAL_REJECTED`、`PROPOSAL_COUNTERED`、`PROPOSAL_INVALIDATED`、`PROPOSAL_TIMEOUT`：只在真实状态变化后记录，含前后状态、原因。
- `MARRIAGE_EXECUTED` / `MARRIAGE_EXECUTION_FAILED`：实际执行结果、夫妻最终 Clan、`ExecutionStep`、付款校验结果。

优先复用现有 `FamilyLogText.MarriageEvaluation`、`MarriageDecision`、`MarriageCompleted` 信息；新增日志补足“哪个实体、以什么评分门槛、结果到底是哪一个状态”。**不要把 `AcceptedPendingExecution` 统计为 `Completed`。**

### 5.4 成功率统计口径

诊断用于计算，不必在运行时维护全局计数：

- `PlayerInitiated` 仅以由玩家 Clan 发起的链 `ChainId` 去重，反提议 `ParentProposalId` 关联到同一链。
- **AI 接受率** = AI 对合法、已进入 `Respond` 的婚约所作 `Accepted` 数 / AI 实际作出的合法决策数（必须明确反提议计入何种类别）。
- **最终成婚率** = 最终 `Completed` 的链数 / 初始发起的链数；另列拒绝、反提议、到期、政治变化、执行失败。
- 当前判断是确定性门槛 `score >= 0`，不是随机概率。观测多个真实样本后才决定是否调整评分。玩家自己接受的提议不要计入“AI 接受率”。

## 六、日志示例（只是格式示例，字段值需在游戏内真实获取）

```text
[MF_DIAG][UI_BEGIN] uiTraceId=a13f9e20 | ownHeroId=main_hero | ownHeroCulture=vlandia | mainHeroCulture=vlandia | playerClanCulture=empire | playerKingdom=empire_west | activeProposalId=none
[MF_DIAG][CLAN_CHECK] uiTraceId=a13f9e20 | clanId=example_vlandia_1 | culture=vlandia | kingdom=vlandia | sameClanCulture=false | sameHeroCulture=true | sameKingdom=false | atWar=false | canUseClan=true | eligibleCount=0 | displayed=false
[MF_DIAG][DIRECTION_CHECK] uiTraceId=a13f9e20 | clanId=example_vlandia_1 | heroId=example_hero | direction=FemaleJoinsMaleClan | allowed=false | reason=CultureForbidden | blockers=none
[MF_DIAG][AI_DECISION] proposalId=example_proposal | chainId=example_chain | receiverClanId=example_clan | scoreTotal=30.00 | acceptanceThreshold=0.00 | plannedDecision=Accept | statusBefore=PendingResponse
[MF_DIAG][PROPOSAL_ACCEPTED] proposalId=example_proposal | chainId=example_chain | operationAllowed=true | statusAfter=AcceptedPendingExecution
[MF_DIAG][MARRIAGE_EXECUTED] proposalId=example_proposal | chainId=example_chain | statusAfter=Completed | destinationClanId=example_destination
```

上面 `main_hero / empire_west / example_*` 都是**虚构示例**，不要在实现中硬编码游戏对象 ID。

## 七、性能与无行为修改约束

1. UI 全量候选详细日志只在玩家真正打开/重新生成名单时写，不能在每帧、每小时或全 Clan 每日 Tick 全量枚举。
2. AI 日常搜索日志只记录结果/少量摘要；默认限制到涉及玩家 Clan 的提议。需要全局统计时可通过诊断开关短期启用，但不得阻塞 Tick 或重排筛选。
3. 不修改 `MarriageScoreTuning` 常量、`MarriagePolicyModel` 规则、`CanUseClan` 条件、`CanNegotiateHero` 条件、`GetMatchTier`、`Take(3)/Take(8)`、`AddCandidate` 的筛选门槛。
4. 不改存档格式，不增加依赖，不修改 Harmony 拦截或其他 Module。日志不得调用 `MarriageAction`、`GiveGoldAction` 等有副作用的方法。
5. 确保诊断禁用时不增加候选遍历量/额外评分调用；打开诊断也不能使用诊断值修改显示或真实决策。
6. 文件日志为追加写入，无自动日志轮转。先关闭诊断开关，避免在长期存档中累积超大文件。

## 八、验证清单与交付

- [ ] ModifiedFamily Release 构建成功，0 错误；如修改中文 XML，检查 XML 格式。
- [ ] 诊断默认关闭，文件日志级别不被擅自修改；关闭时无新增 `[MF_DIAG]`。
- [ ] MCM 诊断开关打开，文件日志选择 Debug、屏幕 Notice，`ModifiedFamily.log` 有结构化新日志但不刷屏。
- [ ] 在“瓦兰迪亚文化主角 + 已加入西帝国”的已有存档打开名单一次，获得 `UI_BEGIN`、每个目标 Clan 的 `CLAN_CHECK`、必要 `HERO_CHECK`/`DIRECTION_CHECK` 和 `UI_SUMMARY`。
- [ ] 明确展示至少数个预期瓦兰迪亚家族为何未入选：比较主角文化和玩家 Clan 文化、战争状态、缺乏未婚候选、领袖不能离族、锁与冷却等；不得猜测原因。
- [ ] 向西帝国家族发送零礼金正常方向婚约，运行到 AI 决定（通常在回复截止时）。用 `ProposalId`/`ChainId` 串联 Proposal → AI Score → Decision → Accepted/Countered/Rejected → Completed/Invalidated。
- [ ] 对高关系、低关系各取真实样本，能计算收到新成员与失去成员时的评分差；不把 AI 签约与实际成婚混为一谈。
- [ ] 与原有构建对比：候选家族数量、排序、合法联姻方向、提议结果与存档行为一致；至少覆盖“禁用诊断”和“启用诊断”两组回归。
- [ ] 输出修改文件清单、关键日志样例、编译结果、未做游戏实测的部分。**不要创建分支、提交 commit 或 PR；如运行环境仅提供 GitHub 而无游戏，明确游戏内验证未执行。**

## 九、完成日志后的后续决策（不在本任务实施）

- 如果 `playerClanCulture` 与主角文化不同：先确认是符合游戏设定，还是建族文化问题；再单独决定是否应让联姻按 `Hero.Culture`/`Clan.Culture` 或混合规则判定。
- 如果 `CultureForbidden` 主导过滤：复核跨王国同文化逻辑的实际对象，不能直接放宽所有跨王国异文化联姻。
- 如果多数是 `AlreadyMarried`、`UnderAge`、`LeaderCannotLeave`、`HeroLocked`：问题在候选资格和人口分布，不是文化规则。
- 如果 AI 对绝大多数玩家提议评分都大幅高于 `AcceptanceThreshold`：另开**评分平衡方案**，重点比较 `AllianceBenefit=55`、同王国加成、入族收益和离族成本、关系及礼金；本任务不得直接调整。
