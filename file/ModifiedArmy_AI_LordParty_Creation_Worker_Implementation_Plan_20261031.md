# ModifiedArmy：AI 领主 Party 创建、基地绑定与首次补员实施方案（Worker 交付版）

> **文档性质**：基于 GitHub 源码的实施任务书；不是已实现功能，也不是测试报告。  
> **仓库**：`GuihuaZhou/bannerlord`  
> **本次静态审查分支/HEAD**：`main` / `58de2e6657957ba27889878e7fe8bef2a58ee7af`  
> **日期**：2026-10-31  
> **改动范围**：`ModifiedArmy` 的普通 AI 领主 Party 创建、出生点兵源查询、Party—定居点绑定、首次补员登记，以及原讨论的低于 50 人定居点驻留评分。  
> **状态**：待 Worker 实施、编译、游戏内验证；不得把本设计描述为已经提交或已通过测试。

## 0. 给 Worker 的优先阅读及执行纪律

1. 开始工作前执行 `git status --short`，记录当前分支和 HEAD；如 HEAD 已不是 `58de2e6`，按当前源码重新核对所有入口与行号，**以当前代码和用户本次要求优先**。保留无关工作树修改。阅读仓库根 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`。
2. 只修改 AI 的普通 Lord Party 自动创建路径；**玩家主部队、玩家 Clan 分队、叛军/次要派系、商队、征粮队、运输队等不得被连带修改**。必须保持现有 `ManagedPartyPolicy` 的启用开关语义：关闭统一 AI 兵力规划时应能回退到原版路径。
3. 保留 `NewClanTierModel` 的 Party 数量上限（含 TalentMagnet 加成）、本体指挥官筛选和创建评分，不重写 `ConsiderSpawningLordParties`。
4. **创建前只做只读兵源与基地资格查询**，不运行完整补员任务、`BuildDailyPlan`、`EvaluateParty` 或真实调兵事务；不创建临时 Party 冒充预评估对象。
5. **创建顺序固定为：选基地 → 临时预留 → 创建无免费正规兵的 Party → 正式绑定 → 登记第一次 `Recruit` 任务 → 入城/触发原有访问执行。** 先绑定、后登记任务，不能颠倒。
6. 50 人是选址门槛及已有的最低出征讨论门槛，**不是任务一次成功数量、不是编制上限、不是强制同步塞满 Party 的条件**。首次任务失败/只补到部分兵员时，后续继续使用正常任务规划及驻留行为，不增加失败专用状态机、冷却、重试计时器。
7. 真实士兵必须经已有 `UnifiedRecruitmentExecutionService` / `PartyExchangeModel` / `FiefPartyManager` 与相关财政审批转移；**绝不使用直接 `Roster.AddToCounts()` 来凭空制造初始正规兵**，也不放宽 `CurrentSettlement` 校验。
8. 仅分析/规划时不写仓库；Worker 获实施授权后先分阶段做小改动，记录测试结果。无明确授权不得提交/推送/开 PR。

## 1. 已核实的现有源码调用链

### 1.1 本体的自动创建链路

```text
HeroSpawnCampaignBehavior.ConsiderSpawningLordParties(clan, isNewGame)
  ├─ ClanTierModel.GetPartyLimitForTier()
  ├─ GetBestAvailableCommander()
  ├─ CalculateScoreToCreateParty() + GetHeroPartyCommandScore() > 100
  └─ SpawnLordParty(hero, isNewGame)
       ├─ 原版可能先解除 GovernorOf
       ├─ SettlementHelper.GetBestSettlementToSpawnAround(hero)
       ├─ MobilePartyHelper.SpawnLordParty(hero, settlement.GatePosition, spawnRadius)
       │    └─ LordPartyComponent.CreateLordParty(..., spawnSettlement: null, ...)
       │         └─ LordPartyComponent.InitializationArgs.InitializeLordPartyProperties()
       │              ├─ 将领队英雄加入 Party
       │              ├─ AI Party: InitializeMobilePartyAroundPosition(defaultPartyTemplate, ...)
       │              │    └─ PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty()
       │              │         → 生成默认模板正规兵（需拦截）
       │              └─ 初始粮食
       ├─ 若 isNewGame，再按模板追加一批正规兵（也需拦截）
       └─ 返回 Party
  └─ GiveInitialItemsToParty(party) // 原版发放周边村庄物品，不等于生成正规兵
```

重要：现有自动创建调用的是 `MobilePartyHelper.SpawnLordParty(Hero, CampaignVec2, float)`，最终 **`spawnSettlement` 为 null**；仅在城门附近生成坐标，不代表 `party.CurrentSettlement == settlement`。另一重载 `SpawnLordParty(Hero, Settlement)` 会向 `LordPartyComponent` 传入选定定居点，但依旧需显式验证是否真正入城。

### 1.2 ModifiedArmy 已有入口

- `Recruitment/Patches/HeroSpawnSettlementPatch.cs`：`[ThreadStatic] _spawningHero` 限定原版 `SpawnLordParty` 上下文，在 `SettlementHelper.GetBestSettlementToSpawnAround` 的 Postfix 中把出生地点改成本 Clan 未被围攻、**按驻军总人数优先**的城镇/城堡；`GetTransferableCount()` 使用城镇125、城堡75的局部硬编码。**缺点**：没有检查是否已绑定，未调用真实调兵安全余量及招募审批，未执行≥50门槛，候选为空还会回退原版地点。
- `PartyManagement/Bindings/PartySettlementBindingBehavior.cs`：已有 `TryReserveForCreation(leader, clan, primary, out token)`、`TryCommitCreation(token, createdParty)`、`CancelCreationReservation(token)`、`IsBoundSettlement()`、`GetBoundSettlements()`、`ReleasePartyBindings()`；其 `IsLegalBase` 已要求同 Clan、`IsFortification`、非围攻，`IsAvailable` 已检查反向索引 `_settlementOwners` 与 `_reservations`。**可直接复用，不另建绑定账本**。
- 该行为监听 `MobilePartyCreated`：新普通 Lord Party 会先写入 `LegacyUnbound=true` 的基础记录；随后成功调用 `TryCommitCreation()` 会以正式主基地记录覆盖。处理失败与回滚时必须注意这个事件顺序。
- `PartyManagement/Patches/LordPartyCreationSequenceProbe.cs`：`LordPartyComponent.CreateLordParty` 及 `HeroSpawnCampaignBehavior.SpawnLordParty` 前后只读诊断补丁。保留探针，不将其当作已实现的创建事务。
- `PartyManagement/Models/NewPartySizeLimitModel.cs`：已经是注册到 `Main.cs` 的 PartySizeLimitModel 子类，目前仅覆写队伍上限、驻军上限等；**尚未覆写** `FindAppropriateInitialRosterForMobileParty()`。
- `ForceManagement/Services/PartyForceCandidateService.cs`：`EnumerateCandidates(MobileParty party, ...)` 先取得 `bindings.GetBoundSettlements(party)`，对绑定的本 Clan 要塞调用 `AddFiefCandidate` 与 `AddProfessionalCandidate`；单个候选均需要已存在的 `MobileParty`。创建前不能直接调用整个服务。
- 驻军安全可调量由 `GarrisonTransferPolicyModel.GetTransferableGarrisonTroopCount(settlement)` 提供，`FiefPartyManager.GetAvailableTroopCount(settlement)` 提供当前就绪封邑人数；**后者不是最终批准数**，还需检查模板、最小批次、财政等。
- `ForceManagement/Missions/PartyForceTaskCoordinator.cs`：`EvaluateParty()` 运行日常规划并由私有 `ApplyLivePlan()` 注册任务；`FindTask()` 可查当前未终结任务。**没有供创建后直接登记固定目标任务的接口**。
- `ForceManagement/Models/AiForcePlanningModel.cs`：`BuildDailyPlan()` 受目标缺口、需求阈值、随机门槛、战时休整和其他命令保护制约；不适合用来强行建立指定首次目标。其普通 `Recruit` 任务结构可以复用。
- `ForceManagement/Behaviors/AiForcePlanningBehavior.cs`：`EvaluateParty()` 是日度规划入口；`ExecuteAtSettlement()` 调用原有访问服务；`OnAiHourlyTick()` 按当前任务为 `GoToSettlement` 候选加分。**目前 `party.CurrentSettlement == task.TargetSettlement` 时提前返回**。
- `ForceManagement/Behaviors/PartyExchangeBehavior.cs`：`SettlementEntered`、`DailyTickPartyEvent` 进入 `EvaluateVisit()`，并用 `_lastEvaluationDay[PartyId|SettlementId]` 防止同一天重复执行；它目前把日期写入发生在实际任务执行之前。
- `ForceManagement/Missions/PartyVisitExecutionService.cs`：只有 `task.TargetSettlement == settlement` 才走定向任务；`Recruit` 还必须证明此地点为已绑定的基地（雇佣兵除外）。
- `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`：`Execute()` 需要 `party.CurrentSettlement == settlement`，按单个 `SourceKind` 调用既有封邑、驻军或雇佣兵真实事务；执行时重新计算 `ForceTarget.GapCount`。**不需要新写招募执行器**。
- `ForceManagement/Models/PartyExchangeModel.cs`：驻军调拨及封邑动员同样要求新 Party 已实际在目标定居点；严禁在预审中假造当前城镇字段。
- `ForceManagement/Contracts/UnifiedForceContracts.cs`：`PartyForceTask` 已具有 `Party/PartyId/ClanId/OwnerKey/Version/CreatedDay/Intent/SourceKind/TargetSettlement/Status/...` 字段；不引入新的 Task 枚举或存档身份。
- `ForceManagement/Services/ManagedPartyPolicy.cs`：受管对象仅限启用开关、活跃 AI 普通领主 Party；排除玩家 Clan、次要派系、解散 Party 等。创建前尚无 Party，需实现与此**语义一致的 Hero/Clan 判定**，不能用 `IsModManagedAiParty(null)`。

## 2. 用户已确认的目标行为（实施基线）

1. 原版 AI 照常判断 Clan 是否有创建名额、是否有合适领队、创建评分是否足够。
2. 一旦本体决定尝试创建受管 AI Party，**只调用现有补员系统中必要的只读兵源查询能力**，搜索该 Clan **未绑定、未被其他创建预留**、不在围攻中的城镇/城堡，选出预计当前可合法提供**至少50名非英雄正规兵**的一个基地。**不遍历整个正式任务规划流程，不生成招募任务，不修改任何士兵或财政数据。**
3. 如果不存在符合条件的基地，当前这一次 Party 创建返回失败/`null`；**不回退到原版免费造兵的创建路径**。本体以后有新的创建机会时可再检查。不能将此记为日常招募任务失败。
4. 找到基地后，复用 `TryReserveForCreation()`，创建 Party；创建过程保留领队及必要原生结构/物品，但不能生成免费**正规士兵**，并应使用选定基地作为出生地点。
5. 创建成功后**先** `TryCommitCreation()` 建立 Party—定居点主基地绑定；**随后**立即创建一条既有类型的 `Recruit` 任务，`TargetSettlement` **只能是这个已绑定定居点**。首次任务**不经**日常随机门槛、候选目的地重选和需求评分；它仍保留日后执行时的实时招募合法性检查。
6. 将新 Party 真实置于这个定居点（以 `CurrentSettlement` 为准），让现有访问事务处理首次任务；**不直接下达地图上来回进出的强制命令来触发招募**。
7. 由于创建前只是预估，实际补员可能低于50或者失败。**Party 不因此被自动当作创建失败撤销；后续可以正常重试**。在定居点内，人数低于50时通过高分候选行为尽可能驻留；达到50后不再提交特殊驻留候选。50**不是**日常兵力编制目标或上限。
8. 原任务仍按**单一** `PartyForceSourceKind` 工作；不把本需求扩大为多来源一次提交的大型事务重构。

### 2.1 “可提供50人”的选址统计口径

- 按**非英雄正规士兵**统计，不以领主本人充数；**Party 的实际规模上限必须有空间容纳领主及50名正规兵**，具体使用引擎当前人数/上限语义判定，不能直接写死 `PartySizeLimit >= 50` 就认为合法。
- 只统计**同一个候选基地**当前真实可用的驻军普通兵及该基地可征召封邑兵。可以组合两类真实兵源，例如职业兵30、封邑兵25，预计供给55；**不得把两个不同城镇的士兵相加后在一个地点出生**。
- 驻军：以 `GarrisonTransferPolicyModel.GetTransferableGarrisonTroopCount()` 的安全限制为起点，排除 `FiefPartyManager.GetServingTroopCounts(garrison)` 所保护的现役封邑兵，按现有有效名册口径避免把不符合调拨条件的兵算入供给；不得使用旧补丁 `GetTransferableCount()` 的“城125/堡75”硬编码。
- 封邑：使用 `GetAvailableTroopCount()`/`GetFiefTroopRoster()` 等**只读**库存信息，遵守现有启用兵种筛选和 `MinimumFiefRecruitBatch`。当前正式 `PreviewFiefRecruitmentApprovals()` 必须传入 `MobileParty`，不能在创建前伪造对象直接调用。
- 财政、工资与兵种组成的最终审批依赖真实 `MobileParty` 的属性、日常目标及 Clan 预算；创建前应做**保守的轻量可行性判断**，不要伪称与真实任务100%一致，不要进行完整 BuildPlan/交易模拟，也不要提前扣费、预留士兵或创建财政/封邑台账。若无法从既有公开只读 API 精确算出可通过人数，记录 `EstimatedCount` 和实际最终 `TransferredCount` 的差异供验收。
- 若两个来源的预算/名额彼此竞争，不能在同一份可用财政上把两次独立批准人数简单相加。可使用共享的**只读、保守**财政余量做累加裁剪；必要时本次宁可拒绝边缘基地，也不要虚报50。**不得为让预览达到50而放宽真实招募规则。**
- **组合来源时必须考虑补员顺序**：正式封邑兵征召有 `MinimumFiefRecruitBatch` 下限。例如驻军可调30、封邑兵可召25、和平目标50，若首次取驻军30，剩余缺口20可能因小于封邑兵最小批次而导致后续封邑兵无法获批；此例首次任务应优先取封邑兵25，再由驻军补25。创建前的50人可行性不能只看无序总和，还必须找到至少一个符合现有单来源任务规则的可行顺序。若没有这种顺序，判定基地不合格，而不是悄悄改动最小批次规则。
- 选址排序：第一优先满足准入条件，再按预估可补员人数降序、距领队最近可用地点升序、`Settlement.StringId` 词典序做平分；这是推荐实现细节，不改变门槛。

### 2.2 创建前仅查询哪些函数

| 现有函数/数据 | 是否调用 | 用法/边界 |
|---|---|---|
| `PartySettlementBindingBehavior` 的合法性/可用性判断 | **是** | 新增**只读** `CanReserveForCreation(Hero, Clan, Settlement)` 或等价方法复用内部 `IsLegalBase`、`IsAvailable`；`TryReserve...` 仅在最终选中后调用 |
| `GarrisonTransferPolicyModel.GetTransferableGarrisonTroopCount(settlement)` | **是** | 驻军调出基线；继续排除受保护兵 |
| `FiefPartyManager.GetAvailableTroopCount(settlement)` / `GetFiefTroopRoster(settlement)` | **是** | 当前就绪名册与封邑兵种；它们并非可直接批准人数 |
| `MilitaryConfig` 的 `MinimumFiefRecruitBatch` | **是** | 维持最小批次，不改 XML 数值 |
| 现有工资/预算**只读快照**或提取的只读辅助 | **按需** | 保守可行性过滤，不落账、不进行正式招募 |
| `PartyForceCandidateService.EnumerateCandidates(party, ...)` | **否** | 依赖现有 Party、绑定和完整上下文 |
| `FiefPartyManager.PreviewFiefRecruitmentApprovals(settlement, party, ...)` | **否（创建前）** | 需要真实 Party，创建后执行时仍使用 |
| `RecruitmentModelManager.Model.BuildPlan(party, ...)` | **否（创建前）** | 无真实 Party；不能通过假对象规避 |
| `AiForcePlanningModel.BuildDailyPlan` / `PartyForceTaskCoordinator.EvaluateParty` | **否（创建前）** | 不运行任务计划、随机门槛、策略评分 |
| `PartyExchangeService.Execute` / `UnifiedRecruitmentExecutionService.Execute` | **否（创建前）** | 只在后续正式任务中执行真实调兵 |

## 3. 推荐实现：最小修改，不重造系统

### A. `PartyManagement/Models/PartyCreationSettlementModel.cs`（**新增轻量只读模型**）

职责：面向**尚未创建 Party** 的 `Hero + Clan`，查询是否有至少一个满足 50 人供给估算的空闲基地。内部只复用上述只读函数，不依赖不存在的 `MobileParty`。

建议接口（示意，实际命名按模块约定统一）：

```csharp
public sealed class PartyCreationSettlementModel
{
    public PartyCreationSiteResult FindEligibleBase(Hero leader, Clan clan,
        int minimumRegulars = 50);
}

public sealed class PartyCreationSiteResult
{
    public Settlement Settlement { get; init; }
    public int EstimatedGarrisonRegulars { get; init; }
    public int EstimatedFiefRegulars { get; init; }
    public int EstimatedCombinedRegulars { get; init; }
    public PartyForceSourceKind PreferredInitialSource { get; init; }
    public string ReasonCode { get; init; }
}
```

`PartyCreationSiteResult` **仅运行时局部值**，不要加 SaveId 或新的账本。若工程语言版本不支持 `init`，使用既有风格 `get; set;`。模型只计算，不写入兵源/Party/金币/任务。

`PartySettlementBindingBehavior` 内已有私有 `IsLegalBase`、`IsAvailable`，可以增加**只读** `CanReserveForCreation()` 或 `GetAvailableCreationBases()`；不能逐个候选调用 `TryReserveForCreation()` 导致无意义的预留/取消事件及日志刷屏。

从 `PartyForceCandidateService` 提取/复用名册中“可安全调拨、不包含已服役封邑兵”的读数辅助；**不能在 PartyCreationSettlementModel 内复制一套与实际审批渐行渐远的驻军保留算法**。必要时复用 `GarrisonDefense` 快照。对财政无法精确预评估的部分，以文档约定的保守策略处理并记录预估差额，不要把查询设计成正式招募。

### B. `Recruitment/Patches/HeroSpawnSettlementPatch.cs`（**修改/重构既有创建补丁**）

现有补丁只替换 `SettlementHelper` 结果，不足以同时“无候选时禁止创建、无免费兵、创建后绑定/建任务、真实入城”。**建议将受管 AI 的创建路径接到 `HeroSpawnCampaignBehavior.SpawnLordParty(Hero,bool)` 的定向 Harmony Prefix，必要时由一个很薄的 Patch 转调 Coordinator**。保留当前 `LordPartyCreationSequenceProbe` 做时序诊断，不叠加多个相互抢写的旧选址 Postfix。

- 仅对启用统一规划且 `Hero/Clan` 符合“受管 AI 普通领主”范围的调用生效（创建前没有 Party），其余调用走原版。
- **不要替换** `ConsiderSpawningLordParties`，以免丢掉本体 Clan 资格、评分、领队选择、`GiveInitialItemsToParty`。
- 如果无合格基地，设置 `__result = null`，针对该受管调用返回 `false` 跳过原版 `SpawnLordParty`；其他调用不受影响。检查 Prefix 与现有诊断 Patch 的优先级和调用行为。
- 若有合格基地，Coordinator 先 `TryReserveForCreation()`；通过后在局部受控创建作用域内执行 `MobilePartyHelper.SpawnLordParty(hero, selectedSettlement)`（注意是 **Hero, Settlement** 重载；它会把 `spawnSettlement` 传入 `LordPartyComponent`）。该重载本身**不等于入城**，后面仍需要实际 `EnterSettlementAction`。
- 由于跳过原版 `SpawnLordParty` 的方法体，也跳过了 `isNewGame` 时的额外模板赠兵循环；但需保证本体的总督移除等必要副作用被**有条件地**保留：只有合格基地已预留、准备真正创建时才解除 `GovernorOf`，避免查询失败导致总督身份被无故移除。其他本体副作用逐一审查，不能直接无脑省略。
- 不要无条件拦截 `LordPartyComponent.CreateLordParty` 或 `MobileParty.CreateParty`，否则会影响玩家 Clan、叛军、剧情或其他 Mod 创建路径。
- 若改用 Transpiler 等更窄方法也可以，但必须同样满足创建失败不回退原版、首次全链路顺序、两处模板兵都不出现的验收条件。要求 Worker 写清选择原因。

### C. `PartyManagement/Models/NewPartySizeLimitModel.cs`（**修改**）

新增 `FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)` 覆写：仅当**当前受控 AI LordParty 创建作用域与当前领队精确匹配**时，返回空正规兵 `TroopRoster`，否则委托 `base`。必须保留 `LordPartyComponent` 另行加入的领队英雄及基本初始化流程；不能在创建完成后清空 Party 名册，以免误删领队/真实调兵结果。

建议使用**临时作用域**（例如调用栈可控的 `PartyCreationScope`，异常时 `finally` 清理），而不是永远生效的全局 `_isSpawningAi` 开关。禁止会跨 Campaign 保存或泄漏的静态 Party/Clan/Settlement 缓存。特别审查 `MobileParty.CreateParty` 的事件发射时序：`MobilePartyCreated` 在 Component `Initialize` 完成后触发。

本次仅明确**不免费赠送正规士兵**；原版初始粮食及 `ConsiderSpawningLordParties` 的 `GiveInitialItemsToParty` 不在本任务范围，除非实际游戏验证发现与“纯真实兵源”另有冲突。

### D. `PartyManagement/Services/PartyCreationCoordinator.cs`（**新增轻量协调器**）

职责仅为执行创建编排，**不是新的补员器或事务账本**。建议关键伪代码：

```csharp
MobileParty TryCreateManagedLordParty(Hero hero, bool isNewGame)
{
    var site = creationSiteModel.FindEligibleBase(hero, hero.Clan, 50);
    if (site?.Settlement == null) return null;

    if (!bindings.TryReserveForCreation(hero, hero.Clan,
            site.Settlement, out var token)) return null;

    MobileParty party = null;
    try
    {
        // 受控创建作用域仅屏蔽初始“正规兵”模板名册。
        using (PartyCreationScope.Begin(hero))
        {
            // 具体调用时机须遵守原版总督移除等初始化前置条件。
            party = MobilePartyHelper.SpawnLordParty(hero, site.Settlement);
        }
        if (party == null) return null;
        if (!bindings.TryCommitCreation(token, party))
            return HandleStructuralCreationFailure(party, token);

        // 这一步必须发生在正式绑定之后、实际入城访问之前。
        if (!planning.TryRegisterInitialRecruitTask(
                party, site.Settlement, site.PreferredInitialSource))
            return HandleTaskRegistrationFailure(party, site.Settlement);

        // 必须让 CurrentSettlement == site.Settlement，
        // 不能仅停留在城门坐标或者伪造属性。
        EnterSettlementAction.ApplyForParty(party, site.Settlement);
        return party;
    }
    finally
    {
        // 只取消仍未提交的 token；成功提交的 token 已被消耗。
        bindings.CancelCreationReservation(token);
    }
}
```

**上面是调用顺序示意，不是直接可编译代码。** 必须审查 `EnterSettlementAction` 是否允许仅有领队的 Party 入城，以及 `MobilePartyHelper.SpawnLordParty(hero, settlement)` 内部是否先设置移动目标；如需先入城后登记任务才符合引擎事件时序，则不能直接颠倒顺序，必须在访问层提供安全的一次性首次评估入口，使“正式绑定→任务登记”这个业务顺序保持不变。

**失败边界分开处理：**

- 创建前找不到基地、预留失败：不创建 Party，不扣兵，不设任务。
- 生成 Party 之前异常：取消临时预留，清理临时作用域，无持久副作用。
- Party 已由引擎创建，但正式绑定或任务登记失败：这是**结构初始化失败**，不是普通“补员失败”。Worker 必须查证可安全回收部分创建对象的引擎 API 及事件副作用；不能默默遗留没有绑定/任务的孤儿 Party，也不能把创建失败当成正常解散，触发原版士兵返还与资源副作用。若无法安全回收，停止交付这一阶段并标记 `BLOCKED`，给出真实日志和修订方案。**不要求因为实际补员不足50而销毁已经绑定、已获任务的 Party**。
- 任务已建立、真实招募被拒/少于50：保持已创建且绑定的 Party，保留现有任务和后续正常重试逻辑，不增新任务状态。
- 不在 Preflight 预扣工资、Hearth、兵员或任务库存；`TryReserveForCreation` 仅预留基地槽位，不能被误当成兵员锁定事务。

### E. `ForceManagement/Missions/PartyForceTaskCoordinator.cs`（**新增首次任务登记入口**）

示例：

```csharp
public bool TryRegisterInitialRecruitTask(MobileParty party,
    Settlement boundSettlement, PartyForceSourceKind preferredSource)
```

- 检查 Party 是有效受管 AI Lord Party、`boundSettlement` 为其**已正式绑定**的基地、没有活跃冲突任务、类型为 `Fief`/`Professional`；根据创建前的**可行补员顺序**确定第一次任务（例如30驻军+25封邑兵、最低封邑批次25时，先选Fief）。若首次目标来源暂时为零，可按预定顺序选另一个真实可查询来源，但**不得再选择另一座城**。
- 在唯一 Task 所有者 `_tasks` 中构造、登记现有 `PartyForceTask`：`Party`,`PartyId`,`ClanId`,`OwnerKey="ModifiedArmy.AiForcePlanning"`,`Intent=Recruit`,`SourceKind`,`TargetSettlement=boundSettlement`,`Status=Planned`,`CreatedDay`/`LastValidatedDay`/`Version` 正确填写；避免通过 `EvaluateParty` 调用日常 `BuildDailyPlan`。沿用当前版本与诊断约定，幂等，不重复增加同一首次任务。
- `AiForcePlanningBehavior` 只提供稳定公共委托入口 `TryRegisterInitialRecruitTask(...)`，避免创建补丁直接操作其私有 `_tasks`。
- 不新增 `InitialRecruitmentTask` 类型、不更改 `PartyForceTask` 存档字段或 `PartyForceTaskPersistence` 格式；`SourceKind` 仍单一。一次任务只处理一个真实来源，后续按原日常规划再找另一来源。
- 第一次任务的创建**不受** `AiRecruitRandomChance`、`AiNeedThreshold` 和目的地候选重选影响；**执行**时仍接受原有有效兵源/财政/派遣合法性校验。

### F. `ForceManagement/Behaviors/PartyExchangeBehavior.cs`（**核对并做最小修正**）

本体应先完成绑定，再登记任务，最后触发入城，以便 `SettlementEntered → EvaluateVisit → ExecuteAtSettlement → UnifiedRecruitmentExecutionService` 立即执行。

- 确保首次任务登记**先于** `SettlementEntered`；否则 `_lastEvaluationDay` 可能在没有任务时被写入，之后同一天收到任务时遭 `DuplicateDailyEvaluation` 拒绝。
- 首选依靠正确调用顺序，**不添加特权“每日跳过门槛”**。若引擎创建事件、入城事件顺序无法稳定保证，需要最小且幂等的首次访问复核入口；不得为求即时补员让同一任务在同日重复执行或绕过真实事务幂等检查。
- 进入任务时，`party.CurrentSettlement == task.TargetSettlement`、`bindings.IsBoundSettlement(party, target)==true` 均需记录并断言。不能仅用出生坐标作为成功判据。
- 现有 `PartyVisitExecutionService.ApplyResult` 对已提交/部分提交会标记 Completed；这与“失败可按正常策略重试”兼容。创建后日常是否因当前任务 `Completed`、随机门槛或一天一次门槛而延迟，属允许的正常补员行为；**不新加补员失败重试器**。

### G. `ForceManagement/Behaviors/AiForcePlanningBehavior.cs`（**低兵力驻留，独立小改动**）

之前已确认的规则：**只有 Party 已在定居点且正规兵人数 `< 50` 时**，向本体 `PartyThinkParams` 提交一个有足够评分的“留在当前定居点”候选；达到50停止加分，其他场景本体 AI 负责处理。这里不依赖补员任务是否成功，也不以“任务失败”作为驻留开关。不要增加强制禁止出城/直接重设地图导航的 Patch。

- 当前 `OnAiHourlyTick` 在 `task==null` 或 `party.CurrentSettlement==task.TargetSettlement` 时立即返回；驻留候选必须**独立于此早退条件**，否则新 Party 在任务目标基地时反而不会驻留。
- 仅受管 AI Lord Party；排除已在 Army、解散/重组中、进入战斗/围攻、明确更高优先级的保护状态。不要把驻留分数无条件压过关键本体防御/紧急行为。
- 复用 `PartyThinkParams` 的 `TryGetBehaviorScore/SetBehaviorScore/AddBehaviorScore` 与现有 `CalculateTaskBehaviorScore` 的高分策略，**不直接调用** `SetMove*` 强制改 AI 命令。
- **特别的源码风险/验收点**：本体 `AiPartyThinkBehavior` 对“GoToSettlement 且目标已是当前定居点”的候选可能只跳过重新下达访问命令，而不清除旧的出城目标；`AiBehavior.Hold` 在该候选动作分派分支也不等于自动调用 `SetMoveModeHold`。Worker 必须以原版 `AiPartyThinkBehavior.cs` 与实机日志验证选中候选后 Party **确实保持在城内**，不能把“评分最高”直接等同“驻留成功”。若候选方案无法实际留城，记录 `BLOCKED` 并给出**依然遵守本体 AI 决策所有权**的最小备选，不得擅自加入硬性移动禁令。
- 50 人口径用 `party.MemberRoster.TotalRegulars`，不得将领队算作一名正规兵。`PartySizeLimit` 不能实际容纳50正规兵的 Party 不应通过初建准入；已有老 Party 不因该规则被强制销毁。

## 4. 分阶段 Worker 实施顺序

| 阶段 | 必须完成 | 完成判据 |
|---|---|---|
| **W0：确认基线** | 记录 HEAD/工作树；读 AGENTS；核对引擎 Create/Initialize/Enter 事件与原版补兵来源；保留现有创建探针 | 真实调用顺序图与日志、无任何源码更改 |
| **W1：只读选址** | `PartyCreationSettlementModel`、绑定只读空闲判断、轻量50人兵源评估；取代硬编码125/75排序 | 无候选时严格拒绝；两个来源可保守合计；查询不改兵源/预算 |
| **W2：创建与绑定** | 定向拦截受管 `SpawnLordParty`、禁模板正规兵、临时预留、实际创建、`TryCommitCreation` | 新 Party 只有领主而无免费正规兵；基地唯一绑定；玩家与其他 Party 不受影响 |
| **W3：首次任务接线** | 新增 `TryRegisterInitialRecruitTask`，顺序必须在绑定后/入城前，复用入城与现有 `Recruit` 事务 | 创建后存在目标为出生基地的任务；能从源真实转移兵，不生成兵 |
| **W4：驻留评分** | `<50` 当前城内候选且与任务有无无关，确认选中候选后仍留城 | 真实游戏内不足50不离城；达到50让本体正常决策 |
| **W5：回归与交付** | 编译、静态门禁、新旧存档/新开局测试、事件/兵源/财政日志 | 报告 `PASS/FAIL/BLOCKED`，附证据、Diff、HEAD 与未完成项 |

除创建与招募关联的必要诊断外，不修改军制权重、封邑兵服役天数、驻军保留比例、AiRecruitRandomChance、财政计算公式或原有持久化结构。分阶段测试，不能为了绕过 W2 的创建结构问题就先进入 W3 的真实士兵资源变更。

## 5. 最小的验收矩阵（均需实际证据）

| ID | 场景 | 预期 |
|---|---|---|
| C01 | Clan 有空闲且可提供≥50兵基地 | 仅从其中一座基地出生；按受控创建/绑定/任务时序完成 |
| C02 | Clan 无空闲合法基地 | 本次返回null，不生成新 Party、不产生免费兵/悬空预留 |
| C03 | 唯一空闲城堡已被围攻 | 不选该城堡，不回退原版其他派系地点 |
| C04 | 驻军有200人，受保护/防御保留后仅可调35 | 不能仅以驻军总数判定≥50 |
| C05 | 单基地可调职业兵30 + 可召封邑兵25，最小封邑批次25且共享资格许可 | 查询预计≥50；**首次先Fief25**，下次Professional≥25，不可先取驻军30导致剩余20封邑批次不足 |
| C06 | 同 Clan 两基地分别只有30和25 | **不可跨基地合计**充当可提供50的新 Party 出生地点 |
| C07 | 两次创建尝试并发指向同基地 | `TryReserveForCreation` 阻止第二次；无重复绑定 |
| C08 | 新游戏首次创建 + 中途 AI 领主重建 | 均无模板免费正规兵；保留领队与必要合法物品 |
| C09 | `MobilePartyCreated` 先生成 `LegacyUnbound` 记录 | 后续 `TryCommitCreation` 正确替换，存档、读档均保留正式绑定 |
| C10 | 新 Party 已绑定，但首次真实调兵仅得25人 | 不销毁 Party；低于50留城，后续普通任务正常重试 |
| C11 | 首次到访与日度居留检查发生在同日 | 不出现无任务先占用 `_lastEvaluationDay`，无同日重复调兵 |
| C12 | 模拟绑定提交/任务登记的结构错误 | 不留无绑定的孤儿 Party；无资源损失，异常时清理预留；如无法安全补偿，报告BLOCKED |
| C13 | 正规兵49 / 50 / 51，均在定居点 | 49提交留城候选并实测不出城；50、51恢复本体行为评分 |
| C14 | 在城内49人、无补员任务/上次补员失败 | 驻留决策仍生效；无失败专用计时器/冷却 |
| C15 | Party 不在城、已经入 Army、战斗或高优先级保护行为 | 不对所有状态全局覆盖驻留行为 |
| C16 | 玩家 Clan 自建 Party、次要派系、反叛 Party、商队/运输队 | 创建及模板兵逻辑仍遵守各自原本规则 |
| C17 | 关闭 `EnableUnifiedAiForcePlanning` | 新增创建拦截、任务与驻留均回退到原版/既有关闭态 |
| C18 | 定居点易主、Party 死亡/解散后、读档恢复 | 绑定与预留无脏数据，恢复顺序不变 |
| C19 | 调兵前后统计正规兵、伤兵、封邑服役账本、驻军、Clan 金额 | 无凭空增兵、丢兵、重复征召、错误扣费/负库存 |
| C20 | Clan 新 Party 规模上限不足以容纳领主+50正规兵 | 不创建永远无法达标的初始 Party |

**建议诊断字段**（保持英文原始 reason code、中文本地化显示、玩家王国范围过滤）：`ClanId`, `LeaderId`, `ChosenSettlementId`, `EstimatedGarrison`, `EstimatedFief`, `EstimatedTotal`, `RejectedReason`, `ReservationTokenId`, `CreatedPartyId`, `BoundSettlementId`, `InitialTaskSource`, `InitialTaskTarget`, `PartyCurrentSettlement`, `BeforeRegulars`, `ApprovedCount`, `TransferredCount`, `AfterRegulars`, `StayCandidateScore`, `NativeSelectedBehavior`。不要把每小时全部 AI Party 的完整候选排序写成无上限日志。

## 6. 验证命令、门禁与交付清单

Worker 根据当前仓库实际脚本参数执行，优先核查并使用：

- `ModifiedArmy/tools/Invoke-ReleaseValidation.ps1`
- `ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1`
- `ModifiedArmy/tools/Test-RuntimeAcceptanceEvidence.ps1`

先读脚本，不要盲传参数。至少执行适用的构建与静态检查；**编译 PASS 不等于游戏内/存档验收 PASS**。C01–C20 中只能通过游戏或存档验证的条目，如无法实机启动，标记 `BLOCKED (no in-game evidence)`，不可标成 PASS。

Worker 完成后提供：

1. 修改文件路径+关键方法列表（新增/改动/不动），及为什么不重写全部 Party 创建/补员系统；
2. 当前 HEAD SHA、`git diff --stat`、适当的完整 Diff 说明；
3. 构建/脚本运行命令与 `PASS/FAIL/BLOCKED` 结果；
4. 创建链路事件日志证据（至少 Prefix→MobilePartyCreated→CommitBinding→RegisterRecruitTask→SettlementEntered→Execute→first AI tick）；
5. 兵源账本与财政前后值、选择/拒绝50人的实际案例；
6. 低于50人驻留实机结果，尤其是“高分候选已选择却仍出城”的验证；
7. 确认**未提交、未推送**的状态（除非用户随后明确要求）。

## 7. 关键源码索引（固定本次审查提交）

以下源码链接固定在 `58de2e6`，避免以后 `main` 移动导致 Worker 看见的文件与本报告不一致：

- [仓库总体规范](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/AGENTS.md)
- [ModifiedArmy 专项规范](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/AGENTS.md)
- [代码组织规范](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/CODE_ORGANIZATION_RULES.md)
- [本体 AI Clan 创建判断与 SpawnLordParty](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/code/HeroSpawnCampaignBehavior.cs#L194-L300)
- [本体 MobilePartyHelper 的两种 SpawnLordParty 重载](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/code/MobilePartyHelper.cs#L17-L33)
- [本体 LordPartyComponent 初始化与初始粮食](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/code/LordPartyComponent.cs#L211-L240)
- [本体初始模板兵计算](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/code/DefaultPartySizeLimitModel.cs#L420-L468)
- [本体 MobileParty 创建与事件](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/code/MobileParty.cs#L4590-L4612)
- [目前出生地点补丁](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/Recruitment/Patches/HeroSpawnSettlementPatch.cs)
- [创建时序探针](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/PartyManagement/Patches/LordPartyCreationSequenceProbe.cs)
- [基地绑定与预留生命周期](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs)
- [Party SizeLimit 模型](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewPartySizeLimitModel.cs)
- [创建/出征数量上限模型](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewClanTierModel.cs)
- [候选兵源及其审批查询](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs)
- [封邑兵 Manager：就绪数量、可批预览](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs#L617-L740)
- [封邑兵预览最低批次及构成](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs#L266-L320)
- [现有日度规划模型](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Models/AiForcePlanningModel.cs)
- [现有任务登记协调器](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyForceTaskCoordinator.cs)
- [AI 决策、任务与访问执行入口](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/AiForcePlanningBehavior.cs)
- [首次入城/每日访问去重](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/PartyExchangeBehavior.cs)
- [任务访问匹配及单来源执行](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Missions/PartyVisitExecutionService.cs)
- [真实招募服务](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Services/UnifiedRecruitmentExecutionService.cs)
- [实际调拨约束](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs)
- [现有任务数据结构](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/UnifiedForceContracts.cs#L99-L139)
- [受管 AI 范围和开关](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ForceManagement/Services/ManagedPartyPolicy.cs)
- [本体 AI 候选评分及动作分派（驻留需核对）](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/code/AiPartyThinkBehavior.cs#L74-L225)
- [现有军队配置与50人和平基线](https://github.com/GuihuaZhou/bannerlord/blob/58de2e6657957ba27889878e7fe8bef2a58ee7af/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml#L118-L145)

## 8. 明确不做的事情

- 不重写全本体 `ConsiderSpawningLordParties`；不改变新 Clan 名额、领队优先级和评分。
- 不实现“创建前完整跑一遍补员任务”或“先造一支假 Party 以便预演所有审批”。
- 不创建新的兵源池、基地/补员任务第二账本，也不引入存档字段。
- 不要求一次性给足50人，不强制补员失败立即退出定居点，不单独处理补员失败重试。
- 不在 Party 创建时使用免费正规兵、不跨城瞬移士兵、不绕过现有调兵/财政审批。
- 不大范围改动 Harmony 本体 AI 行为所有权，也不为了驻留直接禁止新 Party 出城。

**最终一句话：创建前只做“可供50人”的轻量查询；创建 Party 后必须“先绑定，再建任务”，让现有补员系统从该定居点的真实兵源逐步补员。**
