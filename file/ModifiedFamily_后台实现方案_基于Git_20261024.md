# ModifiedFamily 后台实现方案（基于现有 Git 仓库）

> **类型：实现方案，不是设计讨论，也不是代码提交/PR。**  
> **仓库：** `GuihuaZhou/bannerlord`（`main`），核对基线提交 `28f87c824ab07b4230e821052ae62e47e46ccf78`，2026-10-24。  
> **范围：** 后台工程、原版拦截、Model、Clan AI、提议生命周期、反提议、礼金、收养、存档、异常、测试；UI 仅定义接口。  
> **事实与建议区分：** 下面标记为“仓库确认”的，是直接阅读当前代码得出的结论；标记为“新增实现”的，是建议新建的代码；“需运行验证”表示不能仅凭反编译源码保证运行行为。

## 0. 当前仓库的真实落点

| 已有文件（仓库确认） | 代码事实 | 本 Mod 的处理 |
|---|---|---|
| `code/RomanceCampaignBehavior.cs` | `DailyTickClan()` 调用私有 `CheckNpcMarriages(Clan)`；它随机选 `Clan.All` 并最终调用 `MarriageAction.Apply`；同时该 Behavior 注册原版对话 | 只拦截 `CheckNpcMarriages`，不禁用整个 Behavior |
| `code/MarriageOfferCampaignBehavior.cs` | 私有 `DailyTickClan()` 自动查找玩家家族候选；`HourlyTick()` 仍处理旧活跃请求和已接受但待可用的婚姻 | 拦截自动搜索入口；额外处理既有存档的原版未决婚约 |
| `code/DefaultMarriageModel.cs` | `IsCoupleSuitableForMarriage` 复用 `Hero.CanMarry()`；`IsSuitableForMarriage` 要求 Hero 活跃、不在战斗/军团；`GetClanAfterMarriage` 原版倾向男方 Clan，领袖/玩家特例 | 继承 `DefaultMarriageModel`，在约定婚姻执行上下文中覆盖目的 Clan；不全局改坏原版资格逻辑 |
| `code/MarriageAction.cs` | 首先校验资格，**先设置双方 Spouse，再调用 `GetClanAfterMarriage`**；含 governor、party、army 清理，返回 `void` | 必须复用，不修改/重写；执行后验证结果；需要预设作用域上下文 |
| `code/GiveGoldAction.cs` | `ApplyBetweenCharacters(giver,recipient,amount)`；内部将支付额限制到支付 Hero 的当前 Gold | 支付前全额余额检查；支付后确认金额；不可默许部分支付 |
| `code/AdoptHeroAction.cs` | 只提供 `Apply(Hero)`，把父/母指向 `Hero.MainHero` 并将 Clan 设置为 `Clan.PlayerClan` | 不能用于 AI 或跨 Clan 的通用收养；另写服务 |
| `code/Hero.cs` | `Hero.Clan` 的 getter 优先返回 `CompanionOf`；改变 `_clan` 和 `CompanionOf` 有不同名单/事件效果；父母 setter 只会向新父母的 `Children` 添加成员 | 同伴晋升与贵族转 Clan 分两条执行路径；不得随意覆盖已有亲生父母 |
| `ModifiedDiplomacy/ModifiedDiplomacy/Main.cs` | `MBSubModuleBase` 中 `OnSubModuleLoad` 统一 `Harmony.PatchAll` 与 UIExtender、`OnGameStart` 注册 Model/Behavior | 复用相同模块装配范式 |
| `ModifiedDiplomacy/.../Persistence/KingdomDiplomacyManager.cs` | 通过 `CampaignBehaviorBase.SyncData` 保存记录；会对未完成外交执行进度进行恢复 | 借鉴持久化步骤和幂等性；不耦合外交模块 |
| `ModifiedDiplomacy/.../Persistence/KingdomDiplomacySaveDefiner.cs` | `SaveableTypeDefiner` 注册 `SaveableRootClass`、枚举和列表容器 | 创建全新 Definer，核对全项目类型/容器注册冲突 |
| `ModifiedPolitics/.../GovernorNotificationRegistrationPatch.cs` | Patch `MapNotificationVM.PopulateTypeDictionary` 注册自定义通知 | 后续 UI 阶段复用，此期不强制引入 UI |

**当前 main 中未检索到 `ModifiedFamily`、`FamilyProposal` 或 `ModifiedMarriageModel`。以下为新增文件计划，不声称已经存在。**

## 1. 已冻结业务规则（实现输入）

1. Clan 自主决策；无中央配婚器；Model 负责资格/双方评分，Service 负责请求与执行。
2. 正常嫁娶：女方迁入男方 Clan；入赘：男方迁入女方 Clan。禁止无效领袖迁移；保留玩家主角个人婚姻的原版特例。
3. 搜索的同国同文化为首选；异国同文化其次；异国异文化禁止。**同国异文化第三档为先前建议但尚未得到明确确认，因此必须作为独立配置，推荐缺省允许、低优先；不可将其与前两档硬编码在一起。**
4. 不允许普通 AI 联姻涉及 `IsMinorFaction`、`IsClanTypeMercenary`、`IsUnderMercenaryService`、`IsBanditFaction`、`IsRebelClan`、`IsEliminated`；不能将 `Tier` 较低的正常贵族误当小派系。
5. 一次请求七天回应；最多一次反提议（同两名 Hero、可更换归属与礼金，反向新建请求，再七天）；拒绝或应答超时后，这对 Hero 双向冷却 60 天。
6. 仅一次性单向礼金，独立记录 `PayerClan`、`ReceiverClan`、金额，可为零；婚姻完成才支付，失败不提前没收。
7. 临时被俘、参加战斗、军团或逃亡不直接废弃协议；已同意但暂不可执行保留最多建议 30 天；死亡、另婚、原 Clan 变化或硬性资格不满足导致失效。
8. 收养与联姻为不同事务，共用人员转移评分。收养自己的同伴在 UI 上从“同伴”入口；也允许查看其他 Clan 成员并提出收养申请。真实亲属关系语义及原版父母字段兼容必须有专门测试。

## 2. 项目布局和新增文件表

新增目录（**不改其他 Mod 的 Main.cs，不直接引用其 DLL**）：

```text
ModifiedFamily/
├── ModifiedFamily.sln
├── SubModule.xml
├── ModifiedFamily/
│   ├── ModifiedFamily.csproj
│   ├── Main.cs
│   ├── Core/
│   │   ├── FamilyEligibility.cs
│   │   ├── FamilyResult.cs
│   │   └── FamilyConfig.cs
│   ├── Models/
│   │   ├── ClanHeroValueModel.cs
│   │   ├── ModifiedMarriageModel.cs
│   │   └── AdoptionModel.cs
│   ├── AI/
│   │   ├── ClanMarriageDecisionService.cs
│   │   └── ClanAdoptionDecisionService.cs
│   ├── Proposals/
│   │   ├── FamilyProposal.cs
│   │   ├── FamilyProposalService.cs
│   │   ├── FamilyProposalValidator.cs
│   │   └── FamilyProposalEnums.cs
│   ├── Campaign/
│   │   └── FamilyCampaignBehavior.cs
│   ├── Execution/
│   │   ├── MarriageExecutionService.cs
│   │   ├── MarriageExecutionScope.cs
│   │   └── AdoptionExecutionService.cs
│   ├── Kinship/
│   │   └── AdoptiveKinshipService.cs
│   ├── Persistence/
│   │   ├── FamilySaveDefiner.cs
│   │   └── FamilyRecordTypes.cs
│   ├── Patches/
│   │   ├── VanillaNpcMarriageSearchPatch.cs
│   │   ├── VanillaPlayerOfferSearchPatch.cs
│   │   └── VanillaPlayerBarterCompatibilityPatch.cs (后期按实际验证决定)
│   ├── Diagnostics/
│   │   └── FamilyDiagnostics.cs
│   └── UI/ (第二阶段才添加正式界面)
└── ModuleData/ (如需可调配置与本地化再建)
```

最低依赖：游戏 TaleWorlds 必需 DLL、`Lib.Harmony`；第一版后台不需要 UIExtenderEx，可与现有模块 csproj 一样采用 `netstandard2.0`、`Bannerlord.BuildResources`。复制 `ModifiedDiplomacy/ModifiedDiplomacy.csproj` 为模板时**移除 `ModifiedPolitics` 引用与 GUI 构建项**，只保留实际需要的 TaleWorlds 引用、SaveSystem 等。`SubModule.xml` Id=`ModifiedFamily`，自定义 DLL、Main 类型；不得因为同仓库就添加其他三个 Mod 的强依赖。

### Main.cs 装配（新增实现）

- `OnSubModuleLoad()`：`Harmony("...ModifiedFamily...").PatchAll(Assembly.GetExecutingAssembly())`；只在游戏有对应程序集版本时启用，错误需写日志。
- `OnGameStart()` 且 Campaign 模式：`campaignStarter.AddModel(new ModifiedMarriageModel())`，`campaignStarter.AddBehavior(new FamilyCampaignBehavior())`；一份行为负责持久化及事件调度，不为每个评分模型都注册 Behavior。
- 依赖注入：普通纯业务类显式接受 Hero/Clan/数据上下文；避免跨存档静态业务记录。进入或退出战役时清空暂存 `MarriageExecutionScope`。
- 运行检查：验证 `Campaign.Current.Models.MarriageModel` 最终实际是 `ModifiedMarriageModel`；若其他 Mod 后注册同类 Model，应明确输出冲突诊断，不能悄悄按错误归属执行。

## 3. 禁止本体自动找对象：精确 Patch

### P1：只禁用 NPC 随机婚姻匹配（**优先实施**）

目标：`TaleWorlds.CampaignSystem.CampaignBehaviors.RomanceCampaignBehavior.CheckNpcMarriages(Clan)`，源码路径 `code/RomanceCampaignBehavior.cs`。该方法为 `private`，Harmony 按字符串方法名定位；prefix 返回 false 跳过原方法。它仅负责每日随机寻找、概率命中、直接 Apply；**保留 `RomanceCampaignBehavior` 的对话及事件注册**。不 patch `MarriageAction.Apply`。

### P2：只禁用原版 AI 对玩家 Clan 自动求婚

目标：`MarriageOfferCampaignBehavior.DailyTickClan(Clan)`，源码路径 `code/MarriageOfferCampaignBehavior.cs`。prefix 返回 false；不要整个跳过其 `RegisterEvents` / `HourlyTick`，因为原版还有旧婚约、婚姻场景及其他状态回收。对已有旧存档中 `HourlyTick` 可继续执行的原版待结婚条目，另做一次性迁移处理（见 P3）。

### P3：老存档迁移及玩家手动婚姻入口核对

**必须单独处理，不能只打 P1+P2 就声称所有原版婚姻路径被接管。**

- `MarriageOfferCampaignBehavior.SyncData` 保存 `_currentOfferedPlayerClanHero` / `_currentOfferedOtherClanHero` 和 `_acceptedMarriageOffersThatWaitingForAvailability`。旧存档可能留下活跃或已答应的原版协议，由 `HourlyTick` 自动执行。运行时需要在 `OnSessionLaunched` 识别并安全清理或隔离这些旧状态；接口不够时使用版本受控的 Harmony `AccessTools` 访问字段，并提供迁移日志。避免破坏玩家主角的个人求爱记录。
- `RomanceCampaignBehavior.conversation_lord_propose_marriage_to_clan_leader_confirm_consequences()` 会创建 `MarriageBarterable`，它的 `Apply` 可以直接调用 `MarriageAction.Apply`。第一版后台无玩家 UI 时，建议保留 **主角自己的原版求爱路线**，但暂时关闭“替家族成员通过原版对话即时安排婚姻”的入口；有 UI 后改为提交 `FamilyProposalService`，不能绕过七天请求。关闭应优先在对话可见条件或触发处完成，而非全局阻止 `MarriageBarterable.Apply`/`MarriageAction.Apply`。
- 启动自检 Harmony Patch 是否真实生效；本体更新导致私有方法更名/签名变化时输出错误并禁止不完整的 AI 接管，不能默默同时运行两套婚姻 AI。

## 4. 资格检查分层：修复“战斗中就不能提议”的原版耦合

`DefaultMarriageModel.IsCoupleSuitableForMarriage()` 调用 `Hero.CanMarry()`；`Hero.CanMarry()` 最终依赖 `IsSuitableForMarriage()`，后者要求 Hero 为 `IsActive`，且不处于 `MapEvent`、`Army`。所以**提议资格绝不能仅调用 `base.IsCoupleSuitableForMarriage`**，否则被俘、逃亡及正在军团中的人根本不能谈判。

新增 `FamilyEligibility` / `FamilyProposalValidator` 返回结构化 `FamilyResult { IsAllowed, ReasonCode, TemporaryBlockers }`，明确分三个入口：

1. `CanProposeMarriage(proposal, world)`：永久资格：Hero 存在且未死、年龄及身份允许、未婚、性别及近亲符合本体、合法两 Clan、文化王国限制、不允许领袖迁出、无冲突未决请求、关系/战争、礼金协议结构合法；**对被俘/军团/逃亡返回暂时阻碍，不直接判永久失败**。近亲检查原则上继承本体语义并叠加收养亲属检查；若反编译的近亲检查为 private，不能在此直接复用，须实现等价纯检查并加入回归测试。
2. `CanRespond(proposal)`：刷新死亡、已婚、Clan 变更/灭亡、宣战等硬约束；重新评估两 Clan 收益和财力；存在临时行动阻碍时可以合法接受、继续待执行。
3. `CanExecuteMarriage(proposal)`：校验已同意、到期未取消、付款 Clan 全额资金、双方此刻满足 `base.IsCoupleSuitableForMarriage`、目标 Clan 与约定保持一致、未被其他系统提前结婚，再调用 `MarriageAction`。

**冲突锁不加入全局 `IsSuitableForMarriage()`**：否则自己已接受但被锁定的 Hero，在调用 `MarriageAction` 内部资格检查时也会被自己拒绝。锁在 `FamilyProposalService` 的创建、响应与执行预检中完成。

身份过滤建议：`Clan.Kingdom != null` 的正常贵族 Clan 参与 AI 自动选婚；`IsMinorFaction || IsClanTypeMercenary || IsUnderMercenaryService || IsBanditFaction || IsRebelClan || IsEliminated` 任一为真即不可；正常贵族的低 Tier 不受此限制。文化比较应明确当前定为 **Hero.Culture**，而王国使用 `Clan.Kingdom`；如要改为 Clan.Culture，须单独迁移评分语义，不能混用。

## 5. ModifiedMarriageModel 的必要改动

建议 `public sealed class ModifiedMarriageModel : DefaultMarriageModel`，仅覆盖确有需求的 `GetClanAfterMarriage(Hero,Hero)`（以及经测试必需的近亲约束）。普通婚姻调用 `base.GetClanAfterMarriage`，**在 Mod 的已同意婚姻执行作用域内**，根据同一对 Hero 与请求 ID 返回预批准 `DestinationClan`。

- `MarriageExecutionScope.Enter(proposal)` 建立同步、短生命周期的上下文，包含 `ProposalId`、两 Hero、目标 Clan；用 `try/finally` 清理。**不持久化这个临时作用域，不以单纯 `Hero.Spouse` 查找请求**。
- `MarriageAction.Apply(h1,h2)` 先设 Spouse，随后询问 `GetClanAfterMarriage`；此方法只检索已设好的作用域并返回匹配的目标 Clan，不在此重新判定未婚、不给钱、不改变任何 Hero 或 Clan 状态。
- 原版默认个人玩家、不可迁移的领袖特例必须在 `FamilyProposalValidator` 阶段排除错误提议；无匹配作用域的原版主角自行结婚维持 `base` 行为。
- 运行时无 UI 的 AI→AI 路径先通过服务建立作用域、调用 `Apply()`、读取两名 Hero 的 `Spouse` 与 `Clan` 确认一致，失败记录 `ExecutionFailure`，不能直接标成功。

## 6. 提议状态与持久化字段（最小完整 Schema）

新增 `FamilyProposal`（标记并注册 `SaveableRootClass`，使用稳定的 SaveableProperty 编号；**具体 Save ID 待实现前与现有所有 Definer 核对**）：

| 字段 | 作用 |
|---|---|
| `ProposalId`（string） | 唯一请求 ID；使用 GUID 文本，与存档独立 |
| `ChainId`, `ParentProposalId` | 谈判链与反提议关联 |
| `ProposalType`, `Status`, `CounterCount` | Marriage/Adoption；状态；只允许 0/1 次反提议 |
| `ProposerClan`, `ReceiverClan` | 当前轮次的发起与接收家族 |
| `HeroA`, `HeroB` | 婚姻双方，反提议不得换人 |
| `AdopterHero`, `AdopteeHero` | 收养时有效；不要用模糊的 HeroA/HeroB 解释家长和被收养者 |
| `OriginClanA`, `OriginClanB`, `DestinationClan` | 创建时来源快照及最终迁入目标；任何变化都要复检 |
| `GoldPayerClan`, `GoldReceiverClan`, `GiftAmount` | 一笔单向、非负礼金；不绑发起方 |
| `CreatedAt`, `ReplyDeadline`, `ExecutionDeadline` | 两轮最多各七天；已接受待执行上限建议 30 天 |
| `ExecutionStep`, `ReasonCode` | `NotStarted / MarriageApplied / GoldSettled / Completed`；失败原因可追踪 |

附加持久化：`FamilyCooldownRecord(无序HeroPairKey, Until)`、`AdoptiveKinshipRecord(Adopter, Adoptee, Date, SourceClan)`、历史请求列表（限量归档/清理而不抹掉真实已完成关系）。

**建议根状态只有一个持有者**：`FamilyCampaignBehavior` 保存 `_proposals : List<FamilyProposal>`、`_cooldowns : List<FamilyCooldownRecord>`、`_adoptions : List<AdoptiveKinshipRecord>`，通过唯一的新 `SyncData` 键保存。`FamilyProposalService` 访问 Behavior 的业务存储，不单独重复持久化同一集合；创建/修改只从 Service 入口进行。

`FamilySaveDefiner` 只注册自定义的 class、enum、必要容器。**重要仓库经验**：`ModifiedArmy/file/第五阶段启动崩溃Debug日志.md` 曾记录重复注册容器 `Dictionary<CharacterObject,int>` 导致启动问题。这里应首选 `List<自定义记录>`，Hero→请求索引、Clan→请求索引、无序配对冷却查询索引**只在运行时重建**，不要单独存储或重复注册全局字典泛型容器。

载入流程：`SyncData` 取回列表、空值修复 → `OnSessionLaunched` 重建索引与锁 → 清理无效旧数据、检查世界态与执行记录 → `DailyTick` 恢复到期响应或已批准待执行。避免在 `SyncData` 反序列化过程中立即执行会修改世界状态的 Action。

## 7. FamilyProposalService：唯一状态变更入口

公开服务接口建议：

```csharp
FamilyResult TryCreateMarriageProposal(MarriageProposalDraft draft);
FamilyResult TryCreateAdoptionProposal(AdoptionProposalDraft draft);
FamilyResult TryAccept(string proposalId, Clan responder);
FamilyResult TryReject(string proposalId, Clan responder, ReasonCode reason);
FamilyResult TryCounter(string proposalId, Clan responder,
                        MarriageDirection direction,
                        Clan goldPayer, int giftAmount);
FamilyResult TryWithdraw(string proposalId, Clan proposer);
IReadOnlyList<FamilyProposal> GetIncoming(Clan clan);
IReadOnlyList<FamilyProposal> GetOutgoing(Clan clan);
```

实现锁与状态时必须在 Service 的一次同步调用中先验证、再修改，最后更新索引：

- 原提议 7 天：AI 接收方到期作 `Accept / Reject / Counter`；玩家可随时决定，7 天未处理自动拒绝。
- 反提议：原请求变 `SupersededByCounter`（终态）；新请求保留相同 `ChainId`，新 ID，新创建时间与 7 天截止，`CounterCount=1`；任何第二次 `TryCounter` 返回 `CounterLimitReached`。
- 英雄锁：以整个未完成 `ChainId` 为单位持有；创建反提议时**同一调用原子式地移交锁**，不可先解锁让另一 Clan 抢占。
- 拒绝或响应超时：释放锁、记录该 Hero 无序配对的双向 60 天冷却；因死亡/战争/Clan 灭亡等强制失效不施加谈判失败冷却。
- 同一 Hero 不能同时拥有两份活跃婚姻/收养；已有完成结婚、已被其他 Mod 迁移的旧请求须失效。
- 可选独立 Clan 发起冷却，避免同一个 Clan 天天尝试大量候选人。此冷却不与上述无序 HeroPair 60 天冷却混淆。

`Counter` 创建时必须重验新婚姻方向是否合法、礼金出资方是否为协议两 Clan 之一、余额是否可行；AI 反提议前应估计对方是否可能接受，避免纯粹形式上的循环。

## 8. AI Behavior：如何在当前 Campaign 中跑起来

采用一份 `FamilyCampaignBehavior : CampaignBehaviorBase`，注册本体 CampaignEvents。业务层仍按 Clan 分布式：

- `DailyTickClanEvent(Clan clan)`：对当前 Clan 运行独立的 `ClanMarriageDecisionService.ConsiderClan(clan)`（后续收养同理）。一次 Tick 最多产生有限请求，执行资格筛选在前；从预分类候选集合分层抽样，**不要在每个 Clan 的每一天遍历所有 Clan × 所有 Hero 全组合**。
- `DailyTickEvent`：统一推进七天回应、七天反提议回应、30 天待执行、重试与存档修复，**只对现有待处理请求**处理，而非替家族择偶。
- 相关事件（Hero 死亡、俘虏/获释、Clan 变化、换国、灭亡、宣战、婚姻事件、逃亡）在回调中标记相关链为 `Dirty`；永久状态可立即失效，其余等当前稳定 Tick 统一验证，避免嵌套 Action 重入。`Executing` 中识别本次婚姻产生的预期 `MakeHeroFugitiveAction`/`OnHeroChangedClanEvent`，不可自行取消。

**AI 搜索规则**：双方 Clan 都是合法普通贵族；双方 Hero 未婚、成年、非近亲，未被其他请求占用；(1) 同王国同文化；(2) 异王国同文化；(3) 同王国异文化（暂可配置）；其他排除。不能把 `Kingdom == null` 与 `Kingdom == null` 当同国。实际评分由 `ClanHeroValueModel` 评估流出方/流入方的成员、人口、继承、军事治理效益，再由 `ModifiedMarriageModel` 综合关系、年龄、距离、文化、礼金。

**重要**：Clan A 只根据自己的收益决定要不要提，不能提前以全局最优替 Clan B 接受；Clan B 在七天后按自己的当前收益决定 `Accept/Reject/Counter`。建议保留评分逐项日志，不在 UI 暴露对方精确内部评分。

**无 UI 阶段的玩家请求**：先正式实现 Service 的玩家 `TryAccept`/`TryReject`/`TryCounter` API 和调试调用入口，但**生产默认不让 AI 给玩家创建无法交互的请求**，以免自动超时而玩家从未收到通知；AI→AI 可独立验证。真正打开 AI→玩家功能时，必须同一个版本接入地图通知/可恢复收件箱。

## 9. MarriageExecutionService：复用原版结婚 + 礼金

计划独立入口：`TryExecuteMarriage(FamilyProposal proposal) -> ExecutionResult`，执行顺序：

1. `ValidateResponseAndWorld`: 状态 `AcceptedPendingExecution`，两 Hero 仍未婚、已到可执行阶段；非临时无法执行；Clan、婚后归属、关系、战争与礼金条件均有效。Hero 处于 `MapEvent`、`Army` 或非 `IsActive` 时返回 `TemporaryBlocked`，在 30 天窗口内重试。
2. 验证付款 Clan 的 `Leader` 有效，`Gold >= GiftAmount`，不能依赖 `GiveGoldAction` 自动裁剪金额；如果资金不足，可以限期等待，超过期限取消。
3. 建立 `MarriageExecutionScope`，标记 `Executing`，调用 **原版** `MarriageAction.Apply(heroA, heroB, ...)`，在 `finally` 释放上下文。不要调用自定义 `hero.Spouse = ...` 或手动转 Clan。
4. 读取实际世界状态：双向 `Spouse`，婚后两人 Clan 是否等于已批准 `DestinationClan`；仅全部正确才进入 `MarriageApplied`；失败不再直接重试 `MarriageAction`，先确认是否半执行。
5. 婚姻成功且未支付，使用 `GiveGoldAction.ApplyBetweenCharacters(payer.Leader,receiver.Leader,giftAmount,...)`；前后检查余额变化是否为约定金额，记录支付完成才进入 `Completed`；释放锁并归档。

**不可伪造原子事务保证。** 本体的婚姻和金币是两项不同的 `void` Action，后者缺少可供我们的提议 ID 直接去重的公开交易键；发生不可确认的中间态时无法凭本体 API 保证严格 exactly-once。因此：同一同步调用中尽可能连续完成；持久化执行步骤；任何已婚但款项状态不明的记录必须标记 `PaymentReconcileRequired`，禁止盲目重新结婚或再次付款，通过现存婚姻/金币事件及测试可确认时再结算；不要宣称任意进程崩溃都能无损自动回滚。

**其他系统触发婚姻**：通过 `BeforeHeroesMarried` 与逐日检查检测，区分本模块执行作用域和外部婚姻；外部已婚使涉及的未决请求失效。原版玩家主角个人结婚仍由 `base` 处理婚后归属。

## 10. 收养执行：单独实现两条路径

**为什么不能直接调本体 `AdoptHeroAction.Apply`？** 源码写死 `Hero.MainHero` 和 `Clan.PlayerClan`，不支持 AI 收养；此外 `Hero.Father`/`Mother` setter 添加新父母的 Children，却不从旧父母集合删除；直接覆盖贵族已有血缘会破坏原有族谱一致性。

- **路径 A：收养本 Clan 同伴**。验证目标 `CompanionOf == destinationClan`；执行时首先处理同伴身份和相关 party/任务/治理关系，借助 `Hero.CompanionOf = null` 转出同伴名单，再将 `Hero.Clan = destinationClan` 使其进入贵族名单。`Hero.Clan` getter 在 `CompanionOf` 不为 null 时优先返回同伴 Clan，因此**不能先只设置 `hero.Clan` 就认定完成**。最终验证不再计入 `Clan.Companions`、正式出现在 Clan Hero 名单、Party 状态无残留；源码未证明所有 party 转移细节已自动完成，必须在游戏里测试。
- **路径 B：其他贵族 Clan 成员被收养**。需双方 Clan 同意且提议仍合法；执行前检查 Hero 的领主部队、军团、治理职务，不能不加判断直接 `hero.Clan = ...`；可借鉴原版 `MarriageAction.HandleClanChangeAfterMarriageForHero` 的处理顺序（移除总督、party、结束敌对行动、更新定居点），**它是 private，不能直接调用，建议实现独立封装且按实际运行测试**。
- **亲属关系**：新增 `AdoptiveKinshipRecord` 保留“收养人→被收养人”的法定亲属记录并持久化；对已有亲生父母关系的 Hero **不覆写 `Father/Mother`**。在 `ModifiedMarriageModel` 近亲限制中联合检查法定收养关系；UI 后续可单独展示法定亲缘。对无父母记录的同伴若期望直接显示为原版的子女，只有经过 `Children` / `Father/Mother` 双向一致性及存档测试才允许写入本体字段。原版继承与百科血缘是否认可法定收养是额外适配点，**不能把自定义记录等同于已自动修改原版继承规则**。

`AdoptionModel` 不要直接复用联姻的文化/王国/雇佣兵限制；其对象许可和亲属校验应独立。新收养也需在 `FamilyProposalService` 中受同 Hero 锁定及响应期约束，内部同伴直接收养可按权限直接批准，无须虚构外部 Clan。

## 11. 异常路径与事件验证表

| 事件 | `PendingResponse` | `AcceptedPendingExecution` | 监听/实现 |
|---|---|---|---|
| Hero 死亡、已嫁娶、双方变同 Clan、目标迁移 Clan 非法 | Invalidated，立即释放锁 | Invalidated，先看是否已经部分执行 | `HeroKilledEvent` / `BeforeHeroesMarried` / `OnHeroChangedClanEvent` + Tick |
| 被俘、落败逃亡、加入 Army、地图战斗 | 保持七天时钟；仍可按 Clan 意愿回复 | TemporaryBlocked，30 天窗口重试 | `HeroPrisonerTaken`、`HeroPrisonerReleased`、`CharacterBecameFugitiveEvent`、`ArmyCreated`、`MapEventStarted` + Tick |
| Clan 灭亡、成为小派系/雇佣兵或转为非法类型 | Invalidated | Invalidated | `OnClanDestroyedEvent`、换国事件、Tick 审计 |
| Clan 换国但仍在许可范围 | 重新评分，不重启七天 | 重验硬资格；可保留 | `OnClanChangedKingdomEvent` |
| 双方宣战、异国异文化 | Invalidated | Invalidated（未执行时） | `WarDeclared`、定期状态验证 |
| 双方领袖更替 | 保留，但重新判断出资能力、领袖迁出禁止等 | 重取支付人，重新校验执行前条件 | Tick + HeroKilledEvent |
| 礼金余额不足 | 收到方到期评分时反映 | 临时等待，执行窗口超时取消 | `Clan.Gold` / `Clan.Leader.Gold`，余额实测 |
| 旧存档有本体婚约 | 不允许与新链冲突 | 需迁移或有意识隔离 | OnSessionLaunched 一次性兼容操作 |
| 多重事件同一天 | 不在回调中直接重新配对；统一验证顺序 | 执行时捕获预期事件，防止重入 | DirtyQueue + Validate |
| Mod 被卸载或异常载入 | 保留旧存档原本人物，不假定新增记录可读 | 诊断缺失与失效 | SaveDefiner 类型 ID、载入回归 |

按严格顺序处理：`永久非法 > 临时不可执行 > 当前评分/支付能力 > 应答/执行`。不要让七天期限把已经死亡或正在被别人娶走的 Hero 直接送进原版 Action。

## 12. 逐阶段改动清单：每阶段独立验收

| 阶段 | 修改/新增文件 | 真正要交付的可验证结果 |
|---|---|---|
| **M0 工程骨架** | `ModifiedFamily.sln`、`SubModule.xml`、`csproj`、`Main.cs`、`Diagnostics` | Mod 能编译/加载；原三模块无新增强制依赖；Campaign 新/旧档能进入；模型注册顺序诊断输出 |
| **M1 本体阻断** | 两个 `Vanilla...SearchPatch`、老档兼容诊断 | 连续模拟若干年，原版 NPC 随机结婚和自动给玩家 Clan 求婚均不再生成；原版主角求爱及明确调用 `MarriageAction.Apply` 仍能正常工作 |
| **M2 Model / 资格** | `FamilyEligibility`、`ClanHeroValueModel`、`ModifiedMarriageModel`、`AdoptionModel`、`FamilyResult` | 成婚方向、领袖不可迁出、文化/Clan 过滤、全额礼金评分和临时状态分类可通过纯测试；特殊归属作用域生效 |
| **M3 请求与存档** | `FamilyProposal`、`Service`、`Validator`、`Enums`、`FamilyCampaignBehavior`、`SaveDefiner` | 7 天请求/唯一反提议/14 天最长谈判/60 天冷却、双 Hero 锁定、旧请求关联及存读一致；无执行时不修改世界态 |
| **M4 AI 婚姻** | `ClanMarriageDecisionService`、`CampaignBehavior` 事件 | 逐 Clan 自主发现、分档候选、双方独立评分；AI→AI 能在 7/14 天正确成婚，无异国异文化，无小派系/雇佣兵 |
| **M5 婚姻与礼金** | `MarriageExecutionScope`、`MarriageExecutionService`、付款恢复记录 | 正常嫁娶/入赘后 Clan 正确；`MarriageAction` 自己触发逃亡/换 Clan 事件不误取消；无少付款/重复付款；中途失败保留诊断状态 |
| **M6 收养** | `AdoptionExecutionService`、`AdoptiveKinshipService`、收养记录 | 同伴真实从同伴名册转为家族成员；跨 Clan 同意后正确转族；原有血缘不损坏，法定亲属参与近亲限制 |
| **M7 玩家 UI（后续）** | 后续新增 Clan UIExtender、独立弹窗和地图通知 | 玩家从成员安排联姻，从同伴发起收养；能浏览其他 Clan，接收请求、反提议、查看礼金、7 天内回复；复用相同 Service API |

**强烈建议前后次序：M0→M1→M2→M3→M4→M5→M6→M7。** M3 的状态和数据锁在 AI 接入前必须稳定。M5 的 AI 自动执行可以在开发阶段用调试调用验证，不能因为 M4 能发请求就宣称系统完成。

## 13. 具体验收场景（建议自动化/调试存档）

### 核心流程
1. 3 个同国同文化正常贵族家族互相联姻；高优先级优先，低 Tier 正常贵族不被误排除。
2. 异国同文化可以、异国异文化禁止；换国后在第六天重新分类；双方开战则请求失效。
3. 小派系、雇佣兵类型、正在受雇佣兵合同约束的 Clan 各测试一次，禁止发起与接收。
4. `A→B` 请求、`B→A` 唯一反提议，七天后 AI 接受/拒绝；第二次反提议被拒，拒绝后这对 Hero 60 天双向不能重提。
5. 礼金 0、资金充足、接受后缩水、领袖死亡、金币不足、余额临界分别测试；金币只在真正成婚后转一次。
6. 玩家主角正常原版婚姻可用；玩家替其他家族成员走旧对话不会直接绕过新的 Proposal 系统。

### 稳定性与存档
7. 同 Hero 同时两个 Clan 求婚，只能成功建立一条有效链；反提议期间不能释放锁。
8. 7 天回应前人物死亡、加入军团、被俘获释、战败逃亡，分别按永久失效/暂时等待；已同意后战斗超过 30 天取消。
9. 执行婚姻过程中原版调用 `MakeHeroFugitiveAction.Apply`、`OnHeroChangedClan`，本 Mod 不产生错误 `Invalidated`。
10. 保存于申请第六天、反提议第十天、婚姻已同意待执行三种存档，加载后时间、状态和 Hero 锁一致。
11. 使用安装 Mod 以前的旧档（原版活跃婚约/已接受待可用）验证迁移，不能出现重复婚姻；测试关闭/重新开启 Mod 的兼容策略。
12. 诱发婚姻成功但礼金异常或验证失败，中止重复 Apply，保留明确的人工/诊断恢复记录。
13. 收养本方同伴后，`CompanionOf==null`、正式 Clan 名册正确、Party 无残留；不同 Clan 的贵族保留原生父母列表一致性。
14. 连续模拟 5、10、20 年：请求/婚姻成功数、跨王国同文化比率、拒绝/反提议次数、超时率、无效原因频次和每日 AI耗时可追踪。

## 14. 需要预先声明而非掩盖的待证事项

- `RomanceCampaignBehavior` 的私有方法 Harmony Patch 是否在当前实际运行 DLL 版本上匹配：须编译与加载时确认，不能仅凭仓库反编译代码认定成立。
- 同国异文化婚姻作为“第三档允许”只是默认实现建议，并非已经获得用户确认的最终规则；必须隔离为一处可切换的 `FamilyConfig`。
- 旧存档的 `MarriageOfferCampaignBehavior` 活跃请求、`accepted...WaitingForAvailability` 迁移需要运行检验；**不应直接反射抹除原版字典而不保存兼容记录/状态**。
- AI→玩家请求在没有正式通知 UI 之前不应默默开启；需要调试入口或 M7 同步开放。
- 收养中“法定亲属”与本体生父母字段是不同事实；若要让原版族谱/继承系统完全承认继子女，应额外适配，不能认为保存 `AdoptiveKinshipRecord` 即已实现原版继承。
- 原生 `MarriageAction`、`GiveGoldAction` 不是统一事务；无法用普通存档标志完全保证极端进程崩溃下“严格恰好一次”，应设计不可确认付款状态和停机诊断，不可在异常时静默重复扣款。

## 15. 仓库源码索引（固定到分析基线，便于实现时逐文件打开）

- [Main：ModifiedDiplomacy](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedDiplomacy/ModifiedDiplomacy/Main.cs)；[csproj](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedDiplomacy/ModifiedDiplomacy/ModifiedDiplomacy.csproj)；[SubModule.xml](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedDiplomacy/SubModule.xml)
- [RomanceCampaignBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/RomanceCampaignBehavior.cs)
- [MarriageOfferCampaignBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/MarriageOfferCampaignBehavior.cs)
- [DefaultMarriageModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/DefaultMarriageModel.cs)；[MarriageAction.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/MarriageAction.cs)；[MarriageBarterable.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/MarriageBarterable.cs)
- [GiveGoldAction.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/GiveGoldAction.cs)；[AdoptHeroAction.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/AdoptHeroAction.cs)
- [Hero.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/Hero.cs)；[Clan.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/Clan.cs)；[CampaignEvents.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/code/CampaignEvents.cs)
- [KingdomDiplomacyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedDiplomacy/ModifiedDiplomacy/KingdomDiplomacy/Persistence/KingdomDiplomacyManager.cs)；[KingdomDiplomacySaveDefiner.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedDiplomacy/ModifiedDiplomacy/KingdomDiplomacy/Persistence/KingdomDiplomacySaveDefiner.cs)
- [GovernorNotificationRegistrationPatch.cs](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedPolitics/ModifiedPolitics/Governor/Notifications/GovernorNotificationRegistrationPatch.cs)
- [ModifiedArmy 存档容器重复注册问题记录](https://github.com/GuihuaZhou/bannerlord/blob/28f87c824ab07b4230e821052ae62e47e46ccf78/ModifiedArmy/file/%E7%AC%AC%E4%BA%94%E9%98%B6%E6%AE%B5%E5%90%AF%E5%8A%A8%E5%B4%A9%E6%BA%83Debug%E6%97%A5%E5%BF%97.md)

**交付说明：** 这是依据仓库当前代码制定的文件级实施方案。未提交 PR、未修改 Git、未声称已实现或已编译通过。后续执行应逐阶段编译与测试，并在真实游戏 DLL 上确认 Harmony 入口及 Saveable 类型兼容。
