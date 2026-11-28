# ModifiedArmy 海寇数量异常修复及旧存档清理 — Codex 施工任务书

## 1. 目标与范围

本任务分为永久模型修复和一次性旧存档清理两个部分。修复海上海寇 MobileParty 数量（不是单支部队人数）异常，不改变海寇原版生成公式、巡逻算法、兵员/船只模板。保留此前陆地野怪生态调整。清理只能销毁安全状态的超额 Party，不能结束战斗以绕过保护；必须提供中文日志。

分析基准 Git commit：`90d1e2a35d6dbb4a169aa925788a2d7da3020305`。施工前核对实际 HEAD、工作区和游戏程序集版本；若后续 commit 不影响本任务可在当前版本施工，保留用户已有修改。不自动提交 commit、PR、部署，除非用户另外授权。

## 2. 源码依据与根因

仓库参考：`code/DefaultBanditDensityModel.cs`、`code/BanditSpawnCampaignBehavior.cs`、`code/CampaignGameStarter.cs`、`code/MobilePartyAi.cs`；用户补充反编译源码：`NavalDLCBanditDensityModel.cs`、`PiratesCampaignBehavior.cs`。若本地未保存补充源码，可按下述方法名在相同游戏版本 DLL 中核实。

- `BanditSpawnCampaignBehavior.IsBanditFaction`、`IsLooterFaction` 明确排除拥有海上航行能力的 Clan。
- DLC `NavalDLCBanditDensityModel.GetMaxSupportedNumberOfLootersForClan` 对海上 Clan 返回 `NavalDLCManager.Instance.NavalMapSceneWrapper.GetSpawnPoints(clan.StringId).Count`；剧情限制期间返回 0。
- `PiratesCampaignBehavior.GetPirateData`（376–381 行）使用 `clan.WarPartyComponents.Count` 统计现有 Party 数，以当前 `Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(clan)` 作为生成目标。
- `TrySpawnPirateParties`（349–374 行）每次按 `Floor(Pow(target-current, 0.66f))` 计算生成量；`DailyTickClan` 每日调用，开局/跳过教程也可能额外调用。`CanSpawnPiratePartyInZone`（463–468 行）仅检查巡逻区域是否属于 Clan，没有容量硬限制。
- `NewBanditDensityModel : DefaultBanditDensityModel` 注册于 `Main.cs`，未保留 DLC 模型委托链；海上 Clan 落入默认 120 的预算分支。若该模型最终生效，则每个海寇 Clan 的预算被放大至 120。
- 基础模型 `IsPositionInsideNavalSafeZone` 恒为 false；DLC 实现对村庄港口和其他港口采用 7/15 的导航距离。该接口影响 `GetSpawnPosition`（395–424 行）与 `MobilePartyAi.GetNavalPatrolBehavior` 的选点；失效造成分布异常。
- `TryRemoveWeakPirate`（182–202 行）只在满足弱小条件、未交战且远离玩家时清理，不检查 Clan 超额数量。读档仅重建/调整巡逻区域绑定，不主动裁减。因此必须处理历史存量。

静态证据已经明确错误路径；运行时实际模型链、各 Clan 生成点数量和销毁效果仍需验证。

## 3. 永久修复

### 3.1 文件与注册

修改 `ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewBanditDensityModel.cs`；必要时调整 `ModifiedArmy/ModifiedArmy/Main.cs` 模型注册。

将 `NewBanditDensityModel` 从 `DefaultBanditDensityModel` 改为继承 `TaleWorlds.CampaignSystem.ComponentInterfaces.BanditDensityModel`，使用游戏原生 `BaseModel` 委托链。参考现有 `DiagnosticArmyManagementCalculationModel`、`DiagnosticTargetScoreCalculatingModel`。核实 `CampaignGameStarter.AddModel<T>(MBGameModel<T>)` 的初始化路径，确保确实初始化 BaseModel，而非走不初始化的非泛型重载。核实运行时模型注册顺序及实际生效模型，不能跳过 DLC 模型；不直接继承 DLC 类型，不新增对 NavalDLC.dll 的强制依赖。若模型链不符合预期，报告问题，不启动破坏性清理。

### 3.2 海上预算与陆地预算

参考实现（按实际 API 编译适配）：

```csharp
public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
{
    if (clan == null)
        return SharedLooterAndDeserterBudget;

    int baseLimit = BaseModel.GetMaxSupportedNumberOfLootersForClan(clan);
    if (clan.HasNavalNavigationCapability)
        return baseLimit;
    if (baseLimit <= 0)
        return 0;

    Clan deserters = GetDeserterClan();
    if (clan == deserters || clan.StringId == "deserters")
        return Math.Min(baseLimit, DeserterBudget);
    if (clan.StringId == "looters")
    {
        int deserterCount = deserters?.WarPartyComponents.Count ?? 0;
        int budget = Math.Max(0, SharedLooterAndDeserterBudget - deserterCount);
        return Math.Min(baseLimit, budget);
    }
    return Math.Min(baseLimit, SharedLooterAndDeserterBudget);
}
```

常量保留 `SharedLooterAndDeserterBudget = 120`、`DeserterBudget = 50`。海上 Clan 完全委托 DLC，禁止硬编码海寇数量或在 Mod 中复制海上生成点算法。保留剧情限制和基础模型更严格的预算。共享预算只用于陆地劫匪/逃兵等原有分支。

### 3.3 安全区

```csharp
public override bool IsPositionInsideNavalSafeZone(CampaignVec2 position)
    => BaseModel.IsPositionInsideNavalSafeZone(position);
```

不得自行复制 DLC 港口判定；恢复其原生生成、巡逻位置约束。

### 3.4 陆地匪巢参数

| 参数 | 目标 |
|---|---:|
| 激活匪巢所需 Party | 2 |
| 每匪巢内部 Party | 2 |
| 每匪巢外围 Party | 3 |
| 每阵营最大活跃匪巢 | 7 |
| 每阵营初始匪巢 | 5 |
| 劫匪/逃兵共享预算 | 120 |
| 逃兵独立预算 | 50 |

初始与最大匪巢数量必须尊重剧情限制：

```csharp
public override int NumberOfInitialHideoutsAtEachBanditFaction
    => Math.Min(5, BaseModel.NumberOfInitialHideoutsAtEachBanditFaction);
public override int NumberOfMaximumHideoutsAtEachBanditFaction
    => Math.Min(7, BaseModel.NumberOfMaximumHideoutsAtEachBanditFaction);
```

普通强盗最大生成目标仍为 `7 * (2 + 3) = 35` 支（不是硬上限）。其他未主动修改的接口全部显式委托 BaseModel，包括匪巢任务人数、Boss 参数、生成比例等。按实际抽象基类完整接口编译。

## 4. 临时迁移 Behavior

新增 `ModifiedArmy/ModifiedArmy/PartyManagement/Behaviors/PirateOverpopulationCleanupBehavior.cs`，在 Main.cs 注册 `campaignStarter.AddBehavior(new PirateOverpopulationCleanupBehavior());`。不增加 MCN 配置或玩家菜单。

### 4.1 生命周期

- 仅针对旧存档，新增持久化完成标记；新游戏应标记已完成或明确不启动迁移。
- `OnGameLoaded` 不直接销毁对象；等待游戏完成初始化，使用 `DailyTickEvent` 进行安全检查与清理。
- 已完成时直接返回，不扫描 Clan/Party；所有海寇 Clan 均符合恢复后的有效预算才可完成。仍有超额但没有安全候选则继续等待。
- 模型链异常、预算无法确认、剧情限制期间预算为 0 时暂停，禁止误删。对于零预算但仍有 Party 的 Clan，本次迁移保守暂停；若无法消除歧义需报告，不擅自判定完成。
- 不启用 DLC 的环境不得启动破坏性迁移；无海寇 Clan 且确认环境已初始化时，可安全结束。

### 4.2 识别及计数

严格遵循 DLC `IsPirateClan`（470–474 行）：

```csharp
private static bool IsPirateClan(Clan clan)
    => clan != null
       && clan.Culture != null
       && !clan.Culture.CanHaveSettlement
       && clan.HasNavalNavigationCapability
       && clan.IsBanditFaction;
```

不得依据中文名称或文化名称识别海寇，不误删陆地 `sea_raiders`。

```csharp
int currentCount = clan.WarPartyComponents.Count;
int targetCount = Campaign.Current.Models.BanditDensityModel
    .GetMaxSupportedNumberOfLootersForClan(clan);
int excessCount = Math.Max(0, currentCount - targetCount);
```

只在正确 DLC 委托链和有效预算已确认后计算清理目标。统计使用整个 Clan 的 `WarPartyComponents.Count`，交战、任务等受保护对象仍然计入总数，不因不可删除而从存量扣除。候选集合只包括安全海寇 Party。

### 4.3 安全条件（强制）

参考实现：

```csharp
private static bool CanCleanupPirateParty(MobileParty party, Clan clan)
{
    if (party == null || !party.IsActive || party.IsMainParty)
        return false;
    if (party.ActualClan != clan || !party.IsBandit || !IsPirateClan(clan))
        return false;
    if (!party.HasNavalNavigationCapability || !party.IsCurrentlyAtSea)
        return false;
    if (party.MapEvent != null || party.SiegeEvent != null)
        return false;
    if (party.IsCurrentlyUsedByAQuest || party.IsTransitionInProgress)
        return false;
    if (party.Army != null || party.AttachedTo != null)
        return false;
    if (MobileParty.MainParty == null)
        return false;
    float safeDistance = MobileParty.MainParty.SeeingRange * 2f;
    if (party.Position.DistanceSquared(MobileParty.MainParty.Position)
        < safeDistance * safeDistance)
        return false;
    return true;
}
```

以上为参考，须按当前 DLL 确认属性名称和可访问性；还需核实玩家遭遇/任务/封锁等保护状态的实际 API，不能删除正在遭遇、交战或正在销毁的 Party。若缺少可靠状态判定，保守跳过。禁止主动结束战斗、解除 MapEvent 或强制脱离军团来获得清理资格。正常海上巡逻、移动且满足全部保护条件才允许清理。玩家距离保护参考 DLC 原版弱小海寇清理机制；不要求候选达到弱小阈值。

### 4.4 清理批次与顺序

每 Clan 每日最多清理 20 支（建议值），实际数量不超过超额和安全候选数。按距离玩家从远到近、健康兵员从少到多、Party StringId 稳定排序。先对 Clan 及 WarPartyComponents 建立快照，禁止遍历原集合时销毁。每次删除之前重新检查 Party 安全状态、归属、实时超额、模型/预算有效性；当达到正确目标或当日上限时停止。销毁失败不得算成功，剩余留待下一日。

```csharp
const int MaxCleanupPerClanPerDay = 20;
int cleanupCount = Math.Min(excessCount,
    Math.Min(eligibleParties.Count, MaxCleanupPerClanPerDay));
```

此处为本轮计划数量；成功数量必须由实际销毁结果确认。例：120/15 时理论清理 20、20、20、20、20、5，至少 6 个游戏日（不考虑其他数量变化和保护状态）。

## 5. 销毁接口与数据一致性

原版 `PiratesCampaignBehavior.TryRemoveWeakPirate` 使用：

```csharp
DestroyPartyAction.ApplyForDisbanding(
    pirateParty, Settlement.FindFirst(t => t.IsHideout));
```

优先复用同一接口，调用前核实有效的匪巢参数及销毁前置条件；缺失或不满足则跳过并记录，不强制执行。验证原版销毁动作触发 `CampaignEvents.MobilePartyDestroyed`，DLC 的 `OnMobilePartyDestroyed`（99–107 行）随后调用 `UnassignPartyFromZone`（341–347 行），减少 Density、删除绑定。检查 Clan 的 WarPartyComponents、Party 活动状态和地图对象是否正常注销。若销毁是异步生命周期，则按实际生命周期确认成功，避免重复处理尚未完成的 Party。

不得直接清空 MemberRoster、从 Clan 或 MobileParty 集合删除对象、设置 IsActive=false、反射修改 `_assignedZones` 或手工注销游戏对象。保留原版事件链和清理过程。

## 6. 持久化与完成条件

```csharp
private bool _cleanupCompleted;
public override void SyncData(IDataStore dataStore)
{
    dataStore.SyncData("_pirateOverpopulationCleanupCompleted", ref _cleanupCompleted);
}
```

确认旧档缺失字段时默认 false，新游戏禁用迁移；保存/读档中途能继续，完成后读档不重启。只有模型链验证通过、所有海寇 Clan 均已检查、预算有效且实际计数全部不超标，才标记完成。超额但受保护、达到当日上限、预算暂为 0、模型链异常均不能误标完成。完成后仅保留布尔短路，后续版本移除 Behavior 前验证存档兼容性。

## 7. 中文日志

使用现有 `ModLogger`，统一标签 `【海寇清理】`（4 字）。专项日志不受玩家王国过滤，否则野怪 Clan 日志可能不可见。普通级别汇总，Debug 才打印每支明细。无操作且原因不变不重复刷屏，异常首次或变化时记录，完成只打印一次。

模型及迁移初始化：

```text
【海寇清理】密度模型检查通过，当前模型=NewBanditDensityModel，基础模型=NavalDLCBanditDensityModel
【海寇清理】发现旧存档，启动超额海寇清理任务
【海寇清理】密度模型调用链异常，暂停超额清理，原因=未保留DLC密度模型
```

实际清理汇总（示例）：

```text
【海寇清理】阵营=北海海寇，ID=xxx，当前=120，目标=15，超额=105，安全候选=92，交战保护=15，其他保护=13，本次清理=20，剩余=100
【海寇清理】阵营=北海海寇，当前=22，目标=15，超额=7，安全候选=0，交战保护=7，本次暂缓，后续继续检查
```

字段：阵营名称及 ID、清理前真实数量、目标、超额、安全候选数、交战保护数、其他保护数、实际成功数、清理后真实数量。保护原因互斥分类，避免重复计数；可补充失败数量/原因。不要把计划值当作成功值。

Debug 明细和完成日志：

```text
【海寇清理】已清理部队，阵营=北海海寇，PartyId=xxx，兵员=28，船只=2，交战=False
【海寇清理】旧存档海寇数量修复完成，全部海寇阵营均已恢复至原版数量预算，临时清理任务停止运行
```

## 8. 测试验收

### 8.1 永久模型

| 场景 | 预期 |
|---|---|
| 海寇 Clan 有 15 或 30 个生成点 | 分别返回 15 或 30，而非 120 |
| 普通强盗阵营 | 最大生成目标 35 |
| 逃兵 20/50 | 劫匪模型预算 100/70（若基础模型更严格则取更小值） |
| 逃兵独立预算 | 50，尊重基础模型更严格限制 |
| DLC 剧情禁止生成 | 保留返回 0 的限制 |
| 港口安全区 | 委托原版 DLC，恢复生成及巡逻判断 |
| 无 DLC | 无新增程序集依赖导致的启动失败 |

### 8.2 清理迁移

| 场景 | 预期 |
|---|---|
| 120/15 且全部可清理 | 首日成功清理至多 20，后续逐日处理 |
| 18/15 且可清理 | 只删除 3 |
| 15/15 或 10/15 | 不删除；原版按需生成 |
| 超额 10，安全候选仅 3 | 最多删除 3，其余等待 |
| 所有超额对象正在交战 | 不删除，任务不完成 |
| 任务、玩家附近、转换、军团、附属或围城/封锁状态 | 不删除相关 Party |
| 候选在销毁前状态变化 | 复查并跳过 |
| 预算暂为 0、模型链异常、匪巢参数无效 | 暂停/跳过，不误删 |
| 实际销毁失败 | 不计入成功数 |
| 正常销毁 | 正常派发事件，解除 DLC 巡逻绑定、减少 Clan 数量 |
| 中途保存并重载 | 继续迁移 |
| 完成后重载 | 不重启、不扫描 |
| 新游戏 | 不执行旧档清理 |
| 日志 | 中文、真实结果、汇总为主、不刷屏 |

必须至少完成本地编译、静态调用链核实及可执行测试；不能进行游戏实测时明确说明未实测项目，禁止宣称已通过。旧档测试建议先备份存档，避免不可逆的实际销毁影响原档。

## 9. 禁止修改及预计文件

原则仅修改：

1. `ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewBanditDensityModel.cs`（重构）
2. `ModifiedArmy/ModifiedArmy/PartyManagement/Behaviors/PirateOverpopulationCleanupBehavior.cs`（新增）
3. `ModifiedArmy/ModifiedArmy/Main.cs`（模型及 Behavior 注册）

必要项目/测试文件可按实际工程补充并说明。不得修改原版反编译代码、DLC 生成算法、海寇巡逻区域选择、海寇兵员船只模板、陆地野怪模板、逃兵生成 Behavior、领主征召/补员、驻军、封建兵、职业兵及其他无关模型。不要扩大施工范围，不新增 MCN。

## 10. 施工顺序及交付

- [ ] 核对 HEAD、工作区、程序集及源码依据。
- [ ] 重构模型，保证 BaseModel 初始化及 DLC 委托链正确。
- [ ] 保留陆地野怪预算、剧情限制，恢复海寇预算和安全区。
- [ ] 实现独立临时迁移 Behavior 及一次性存档标记。
- [ ] 实现严格状态保护、快照、每次销毁前复查和每日限额。
- [ ] 接入原版销毁路径、核实事件与数据一致性。
- [ ] 接入中文日志并注册 Behavior。
- [ ] 编译、回归、验证旧档/新档及无 DLC 场景。
- [ ] 输出改动文件、实际测试结果、模型链及未验证事项。

设计默认值（并非用户明确指定或原版强制限制）：每 Clan 每日最多 20 支、玩家两倍视野保护、一次性持久化完成标记；采用保守零预算暂停策略。以上用于降低批量销毁风险。实际模型链、API 可用性、销毁事件以及存档兼容性须由 Codex 在本地核实。
