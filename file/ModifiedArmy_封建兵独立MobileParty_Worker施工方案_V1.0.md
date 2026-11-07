# ModifiedArmy — 村庄封建兵独立 MobileParty 存储改造

**Worker 可施工实现方案 V1.0**  
**编制日期：2026-11-05**  
**目标仓库：`GuihuaZhou/bannerlord`；源码：`ModifiedArmy/ModifiedArmy/`；原版反编译参考：`code/`**

> **明确约束**：不考虑任何旧存档兼容、旧数据迁移或旧版 Mod 存档升级；但**必须**支持本方案实施后创建的新游戏正常保存、读档、继续运行。不要把“无需兼容旧档”理解为“无需序列化与读档恢复”。
>
> 本文是施工规格，不是已验证的提交、可直接编译的最终补丁。代码片段为结构示意，最终签名、事件时序和引擎行为以仓库当前源码及编译、游戏内测试为准。

## 0. 任务目的和定案

当前 `VillageFiefData.GetReadyRoster()` 直接返回村庄原版 `MilitiaPartyComponent.MobileParty.MemberRoster`，导致原版民兵和待役封建兵共用同一队伍名册。现改造为：**每个有效村庄一支独立的、持久化的 `MobileParty`，其 `MemberRoster` 是该村封建兵的唯一“待役兵员库存”。**

- 组件：新增 `FiefReservePartyComponent : PartyComponent`，**不得**继承或伪装为 `MilitiaPartyComponent`、`GarrisonPartyComponent`、`LordPartyComponent`、`CustomPartyComponent`。
- 归属：严格“一村庄 → 至多一支活动的封建兵预备队”，由 `FiefPartyManager` 负责创建、定位、索引、修复关联、监控异常移除。
- 兵员：**Ready** 存于独立预备队 `MemberRoster`；**Serving** 仍以 `VillageFiefData.ServiceBatches` + 领主/出征队伍真实名册共同表示；**Cooling** 仍在 `ReturnBatches`。维持当前的 Hearth 动员记账、免薪期、每日对账及周升级/归还机制。
- 自然民兵：保持原版民兵自身的生成、驻村、防御和损耗规则；从此**绝不在民兵名册中写入封建兵**。
- **V1 默认语义：仓库型/待役型预备队，不主动出征，不按普通野战部队消耗粮食/工资，不因士气触发原版每日逃兵；不主动把它作为新的村庄守军。** 如果原版机制使它被动参与战斗，必须进行专项修复和验证，不可仅靠 `Ai.DisableAi()` 声称已经排除。
- 不引入第二套兵员池、数量镜像或冗余账本；不顺带重做封邑军事制度和征召评分。

### 0.1 已核对的关键事实（仓库源码）

1. `code/MobileParty.cs`：`MobileParty.CreateParty(id, component)` 依次设置组件、调用 `Create` 与 `Initialize`、注册 CampaignObjectManager 并触发 `MobilePartyCreated`。它会通过 `FindNextUniqueStringId` 自动避重，故**不能**用“调用相同 ID”代替“创建前检查”。`UpdatePartyComponentFlags()` 基于**组件真实类型**决定 `IsLordParty/IsCaravan/IsGarrison/IsMilitia`。
2. `code/DesertionCampaignBehavior.cs`：原版每日逃兵仅检查满足 `IsLordParty || IsCaravan || IsGarrison` 的队伍；自定义 `FiefReservePartyComponent` 不满足这个条件，故**不会触发这一条原版逃兵路径**。注意其他 Mod 和直接调用 `PartyDesertionModel.GetTroopsToDesert()` 的代码仍须审计。
3. `code/DefaultPartyMoraleModel.cs`：有效士气基础 50；饥饿、欠薪、超编、食物多样性可能扣分。**没有领队不会自动导致士气为零**，但不等于可以忽略其他系统。
4. `code/DefaultMobilePartyFoodConsumptionModel.cs`：无领队的自定义活动 Party 默认可能参与日常食物消耗；目前 Mod 的 `NewMobilePartyFoodConsumptionModel` 只额外处理 Garrison，因此须显式排除预备队。
5. `code/DefaultPartySizeLimitModel.cs`：无领队的普通 MobileParty 容量默认 20；预备队必须单独处理容量，以免出现超编士气及其他副作用。
6. `code/MilitiaPartyComponent.cs`：民兵建队后执行 `EnterSettlementAction.ApplyForParty`、关闭 AI，并在 `OnInitialize` 把自身登记到 `Settlement.MilitiaPartyComponent`。新组件**不得覆盖该原版字段**。
7. 当前 `FiefPartyManager.OnSessionLaunched` 流程为 `InitializeFiefData` → 新游戏才 `InitializeNewGameFiefTroops` → `RebuildServingDetachmentIndex` → `ReconcileMobilization`。新预备队必须在**初始化封建兵库存前**创建/重建索引。
8. 当前 `FiefSaveDefiner : SaveableTypeDefiner(20251116)` 已占用 class ID 1～12；新类型可追加 ID **13**，不得复用或重排现有 ID。

## 1. 设计模型和不可破坏的不变量

```text
Settlement (村庄)
    │
    │  FiefReservePartyComponent.VillageSettlement   [序列化关联]
    ▼
MobileParty (独立、禁 AI 的封建兵预备队)
    └── MemberRoster                  [Ready 唯一真实库存]

FiefPartyManager
    └── _reservePartyByVillage         [运行期索引；不单独保存]

FiefPartyData (城镇/城堡)
    └── Villages[Settlement]
          └── VillageFiefData
                ├── ServiceBatches     [Serving：保留]
                ├── ReturnBatches      [Cooling：保留]
                ├── MobilizedHearth    [动员 Hearth：保留]
                └── GetReadyRoster()   ─────► 上述预备队.MemberRoster
```

**强制不变量**：

- **R1**：任何时刻，每个 `Settlement.IsVillage` 的活动预备队数量 ≤ 1；对于纳入本 Mod 管理、初始化成功的有效村庄，正常状态恰好为 1。
- **R2**：生产/升级/冷却结束只改变对应村庄**独立预备队** `MemberRoster`；不修改 `MilitiaPartyComponent.MobileParty.MemberRoster`。
- **R3**：征召事务必须同时更新“预备队减少、目标队伍增加、Hearth 扣减、ServiceBatch 建立”；任一步失败的回滚依然完整。
- **R4**：从目标队伍归还时仍先进入 `ReturnBatches`，冷却完成后才进入预备队 Ready；不能因为新 Party 而绕过冷却。
- **R5**：预备队不得成为 `ServiceBatches` 的服役目标；不得被普通征募、AI 参军、商队/驻军/战团自动管理当成领主队伍。
- **R6**：已创建的新存档读档后，不多建、不清空、不双重计数；新游戏全量初始化**恰好运行一次**。
- **R7**：预备队一旦异常消失、重复、被战斗改动或名册无法访问，**禁止静默补兵以覆盖丢失**；记录可诊断原因、锁定受影响村庄生产/征召并走确定的恢复策略。
- **R8**：村庄易主后 `HomeSettlement` 仍指向同一村庄，`ActualClan` 与村庄所有者一致；任何服役和返还许可仍由现有合法性检查决定。

## 2. 精确修改清单（以当前仓库为准）

| 文件 | Worker 必须执行的修改 |
|---|---|
| **新增** `ModifiedArmy/ModifiedArmy/Fief/Models/FiefReservePartyComponent.cs` | 声明保存类型、绑定来源村庄、创建预备队、提供名称与 Owner/HomeSettlement/Banner；禁止 AI/自主行动；不登记到原版民兵/驻军组件。 |
| `Fief/Models/FiefPartyManager.cs` | `FiefSaveDefiner` 追加类型 ID 13；新增 `_reservePartyByVillage` 运行期索引及查找/重建/确保存在/异常处理；调整 `OnSessionLaunched` 顺序；替换民兵创建/销毁的封邑耦合；管理易主/相关事件。 |
| `Fief/Models/VillageFiefData.cs` | `GetReadyRoster()` 不再读民兵名册，改读 Manager 中当前村庄的独立预备队名册；无合法预备队时返回 null（fail closed）。 |
| `Fief/Models/PartyData/FiefPartyData.cs` | 删除 `NativeMilitiaIdentityOverlap` 限制（名册已物理隔离）；废弃/删除 `RepairVillageMilitiaBinding()`，将村庄数据修复与预备队修复分开；保证初始化不静默丢失旧状态。 |
| `Fief/Patches/SettlementPatch.cs` | 移除原本为保护混编民兵中的封邑兵而存在的 `Settlement.RemoveMilitiasFromParty` Harmony Prefix；整份文件若只包含此 Patch 即删除，不再拦截原版民兵损耗。 |
| `Logistics/Models/NewMobilePartyFoodConsumptionModel.cs` | `DoesPartyConsumeFood` 中自定义预备队直接返回 false。 |
| `PartyManagement/Finance/Models/NewPartyWageModel.cs` | `GetTotalWage` 及 `GetTotalWageWithoutFiefExemption` 遇到预备队均返回 0；其他部队现有战时工资与免薪机制不变。 |
| `PartyManagement/Models/NewPartySizeLimitModel.cs` | `GetPartyMemberSizeLimit` 新增预备队专用分支，容量至少覆盖当前真实库存，不受领队缺失导致的原版 20 人基数限制。 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | 不预期重写；审计 `InitializeFiefTroopsByTemplateWeights`、`PerformReinforcement`、`PerformAutoUpgrade`、`WeeklyUpdate` 是否完全通过 `GetReadyRoster()`；必要时增设 fail-closed 容错与日志。 |
| `Fief/Models/PartyData/FiefPartyRecruitment.cs` | 不预期重写业务逻辑；重点回归 `TryCommitVillageRecruitment` 的源名册身份检查、扣兵、Hearth、ServiceBatch、回滚。 |
| `Fief/Models/PartyData/FiefPartyReturn.cs` | 不预期重写业务逻辑；回归 `TryCommitFiefReturn` 归还/冷却/回滚，不允许直接插入民兵。 |
| `Fief/Services/FiefTroopConversionService.cs` | 审计 Feudal→Professional 读取 Ready 的路径；确保不再有民兵副作用。 |
| `ModifiedArmy/ModifiedArmy/Main.cs` | **原则上不需要新增 CampaignBehavior/Model**：Manager 与三个 Model 已注册；如实际需要新辅助服务，由 Manager 负责，不重复添加同类行为。 |

相对路径表中省略的前缀统一为 `ModifiedArmy/ModifiedArmy/`。

## 3. 分步骤施工

### P0 — 先建独立组件，确认引擎创建链条

新增 `FiefReservePartyComponent`，推荐结构：

```csharp
namespace ModifiedArmy.Models.Fief
{
    [SaveableRootClass(13)]
    public sealed class FiefReservePartyComponent : PartyComponent
    {
        [SaveableProperty(1)]
        public Settlement VillageSettlement { get; private set; }

        // 根据本项目存档生成规范保留无参构造。
        public FiefReservePartyComponent() { }
        public FiefReservePartyComponent(Settlement village)
            => VillageSettlement = village;

        public override Settlement HomeSettlement => VillageSettlement;
        public override Hero PartyOwner => VillageSettlement?.OwnerClan?.Leader;
        public override Banner GetDefaultComponentBanner()
            => VillageSettlement?.Banner;
        public override TextObject Name
        {
            get
            {
                var text = new TextObject("{=modifiedarmy_fief_reserve}{VILLAGE}封建兵预备队");
                text.SetTextVariable("VILLAGE",
                    VillageSettlement?.Name ?? TextObject.GetEmpty());
                return text;
            }
        }
        public override bool AvoidHostileActions => true;
        public override bool CanHaveNavalNavigationCapability => false;

        protected override void OnMobilePartySetOnCreation()
        {
            var party = MobileParty;
            party.ActualClan = VillageSettlement?.OwnerClan;
            party.InitializeMobilePartyAtPosition(VillageSettlement.GatePosition);
            party.Ai.DisableAi();
            party.Aggressiveness = 0f;
            party.DesiredAiNavigationType = MobileParty.NavigationType.None;
        }
    }
}
```

**注意：以上是已核对原版抽象成员与创建接口后的“编译起点”，不是完整最终实现。** 要由 Worker 补充：

1. 前置条件校验：`village?.IsVillage == true`、Settlement 正常有效、避免空 Owner 导致的异常；异常不可吞掉。
2. 实际创建调用应在 Manager 工厂中进行：`MobileParty.CreateParty("modifiedarmy_fief_ready_" + village.StringId, new FiefReservePartyComponent(village))`。
3. 创建前按组件归属扫描确认没有现有预备队。因为 `CreateParty` 会自动给 ID 加后缀，**不能**用相同 StringId 作为防重手段。
4. 对“是否调用 `EnterSettlementAction.ApplyForParty(party, village)`”实施**明确的引擎验证**，见 P5。组件构造阶段可以先设置 GatePosition、Hold、禁 AI；只在验证通过后定稿驻留方式。
5. 不实现 `OnInitialize` 去写 `Settlement.MilitiaPartyComponent` 或 `Settlement.Town.GarrisonPartyComponent`；原版组件绑定仍归原版。
6. 如果实例与原版/Mod 保存框架需要额外 `SaveableTypeDefiner` 或 `AutoGeneratedInstanceCollectObjects` 处理，以实际编译/存档结果完善，不能直接照抄反编译生成代码。

### P1 — 由 FiefPartyManager 负责唯一性、建队、读档恢复

新增运行期字段（不加 `[SaveableField]`）：

```csharp
private readonly Dictionary<Settlement, MobileParty>
    _reservePartyByVillage = new();
```

新增职责明确的方法：

- `internal MobileParty GetReserveParty(Settlement village)`：仅返回 `IsActive` 且 `PartyComponent is FiefReservePartyComponent`、`component.VillageSettlement == village` 的队伍；无效时返回 null；**不得**为了“查询”隐式创建。
- `private void RebuildReservePartyIndex()`：从 `MobileParty.All` 扫描所有自定义预备队；过滤空村庄/失活队伍；按 `VillageSettlement` 建立索引；**发现重复就报错、阻断该村业务，绝不能随意选第一支并丢弃另一支**。
- `private bool EnsureReserveParty(Settlement village)`：先读取既有索引和扫描结果；仅当明确不存在且允许新建时创建。创建成功后校验 PartyComponent、HomeSettlement、MemberRoster、IsActive、unique 关联，再入索引。失败需要结构化日志。
- `private void EnsureReservePartiesForManagedVillages()`：对 `_fiefDataMap.Values` 中 `IsRuntimeReady` 的 `OrderedVillages()`，按村庄 StringId 排序执行；每个村庄只有一支队伍；不得对所有地图 Settlement 盲建无数据的空预备队。当前 `InitializeFiefData` 会为所有城堡/城镇建立 FiefPartyData，故预计覆盖其绑定村庄。
- `private void ValidateReservePartyLinks()`：验证 R1/R2/R8；将失败对应 `VillageFiefData.RequireRecovery("...")`，避免无来源写入。

**调整 `OnSessionLaunched` 为以下有序流程**：

```text
IsSessionReady = false
模板初始化 InitAllEnableTroops()
InitializeFiefData()                       // 创建/恢复 FiefPartyData 与 VillageFiefData
RebuildReservePartyIndex()                 // 读档恢复：从游戏真实 MobileParties 找组件
EnsureReservePartiesForManagedVillages()   // 新游戏创建；读档只补经证实安全的空缺
ValidateReservePartyLinks()
RefreshManpowerCounters()                  // 因为前面的 InitialFeifPartyData 可能早于 Party 关联
if (CampaignState.IsNewGame)
    InitializeNewGameFiefTroops()           // 严格只对新游戏执行，不得对加载档执行
RebuildServingDetachmentIndex()
ReconcileMobilization()
IsSessionReady = true （或在关键不变量失败时保持局部业务封锁）
```

**“补空缺”安全条件**：新游戏可以放心创建；加载新存档时，预备队本应随游戏对象序列化恢复，找不到是异常，不可直接创建并把消失前的兵员数量猜出来。只有确证该村从未有待役库存、也不存在数据不一致/重复的情况，才能新建空队伍；否则 `RequireRecovery("ReservePartyMissing")`，记录原因并阻止新增/征召。需要一次性判定时，应在当前版本保存的 `VillageFiefData` 中追加一个明确的“预备队已创建/登记”布尔标记（例如 `[SaveableProperty(8)]`），**不保存另一份名册、数量或 TroopRoster**；或采用等价、可证明的存在性恢复机制。该标记旨在**当前新存档内部的安全恢复**，不是旧档迁移。

**不要只通过 StringId 重新建立索引**；应通过真实组件类型与 `VillageSettlement` 识别，StringId 只作诊断信息。

### P2 — 切换唯一读入口，保持现有封邑事务

`VillageFiefData.GetReadyRoster()` 改为：

```csharp
public TroopRoster GetReadyRoster()
{
    var manager = Campaign.Current?
        .GetCampaignBehavior<FiefPartyManager>();
    var party = manager?.GetReserveParty(VillageSettlement);
    return party?.IsActive == true
        ? party.MemberRoster
        : null;
}
```

相应补齐 `using TaleWorlds.CampaignSystem.Party;` 等编译依赖。Manager 的 `GetReserveParty` 只查索引并校验类型，**不可通过这个 getter 再进入 `VillageFiefData` 或 `InitializeFiefData`**，避免循环调用。

已核对以下方法当前主要通过 `GetReadyRoster()` 操作库存，默认**保留既有流程**：

- `FiefPartyReinforcement.InitializeFiefTroopsByTemplateWeights()`：新游戏初始兵力写新队伍；`PerformReinforcement()`：日常产兵；`PerformAutoUpgrade()`：升级；`WeeklyUpdate()`：冷却结束入 Ready。
- `FiefPartyRecruitment.TryCommitVillageRecruitment()`：事务化来源扣兵 + Hearth + ServiceBatch + 目标队伍增兵；保留 `entry.Village.GetReadyRoster() == entry.SourceRoster` 身份校验与回滚。
- `FiefPartyReturn.TryCommitFiefReturn()`：仍把归还兵放进 `ReturnBatches`，由 `WeeklyUpdate` 再写新 Ready。
- `FiefTroopConversionService.TryFeudalToProfessional()` 及对应回滚：引用新 Ready，不允许误动原版民兵。
- 各查询、UI 和 AI 使用 `GetReadyFiefTroopCount` / `GetFiefTroopRoster` 等接口，原则上自动获得新库存；但 Worker 要全局检索所有对 `MilitiaPartyComponent`/村庄民兵 `MemberRoster` 的直接访问，确认没有第二条隐性写路径。

**隔离后应删除的限制**：`FiefPartyData.InitialFeifPartyData()` 中拒绝封建兵模板与原版 `MeleeMilitiaTroop/MeleeEliteMilitiaTroop/RangedMilitiaTroop/RangedEliteMilitiaTroop` 角色重叠的 `NativeMilitiaIdentityOverlap` 检查。以前是为了混用同一 TroopRoster；现在二者物理隔离，已经不需要这一全局禁止。注意新预备队本身仍只允许当前 `FiefPartyTemplate` 认可的兵种；升级分支需与既有模板规则一致。

`OnMobilePartyCreated` / `CleanupRemovedVillageMilitiaParty` 现有“原版民兵重建/删除意味着封建兵库存失效”的代码需要删除或改为完全独立的自然民兵日志。**原版民兵被击败、重建，不得引起 Ready 数量变化。**

删除过时 `SettlementPatch` 民兵保护 Harmony：不再重写原版民兵减员逻辑。

### P3 — 三个已注册 Model 的防副作用改造

**A. 粮食** — `Logistics/Models/NewMobilePartyFoodConsumptionModel.cs`：

```csharp
public override bool DoesPartyConsumeFood(MobileParty party)
{
    if (party?.PartyComponent is FiefReservePartyComponent)
        return false;
    if (party?.IsGarrison == true)
        return party.IsActive;
    return base.DoesPartyConsumeFood(party);
}
```

保留当前 Mod “每 10 人每日 1 食物”及驻军特殊规则；预备队应完全绕过粮食消费。不能通过强行往库存注入食品解决饥饿。

**B. 工资** — `PartyManagement/Finance/Models/NewPartyWageModel.cs`：在 `GetTotalWage` **最开始**增加预备队直接返回 0，避免执行免薪、战争倍率、一般工资计算；在 `GetTotalWageWithoutFiefExemption` 同样返回 0，保证所有通过该模型查询预备队长期工资的调用一致。真实出征方仍使用当前 `FiefServiceBatch`、`FreeWageDays` 等规则。

```csharp
if (mobileParty?.PartyComponent is FiefReservePartyComponent)
    return new ExplainedNumber(0f, includeDescriptions);
```

**C. 容量** — `PartyManagement/Models/NewPartySizeLimitModel.cs`：在现有 `SupplyPartyComponent`、`ReinforcementPartyComponent` 分支之前新增一个 `FiefReservePartyComponent` 分支。

**V1 建议采用纯库存语义**：容量下限为 `Math.Max(20, party.NumberOfAllMembers)`，并加准确说明文案；村庄真正允许生产多少兵由现有 Hearth、供养费用和 `VillageFiefData.CanIncreaseSupply()` 决定，**不要在队伍人数达到原版 20 时拦截或令其逃兵**。如未来需要表现理论供养容量，可返回 `Math.Max(20, 当前人数, 由现有供养模型计算的容量)`，但绝不能简单把当前人数当成新增产兵许可。

**D. 士气/逃兵**：V1 **不新增士气模型、不覆写逃兵模型、不写 Harmony 防逃兵 Prefix**。原因：原版 `DesertionCampaignBehavior.DailyTickParty` 仅对 `IsLordParty || IsCaravan || IsGarrison` 触发，独立组件不命中；基础士气本身是 50 左右。测试中必须确保 `party.IsLordParty/IsCaravan/IsGarrison/IsMilitia` 都为 false，且没有本 Mod 其他路径自行执行 `GetTroopsToDesert`。不将“士气为零也不被原版每日逃兵处理”等同于“战斗士气永远无作用”。

### P4 — 所有权、异常移除和状态一致性

1. **易主**：`FiefPartyManager.OnSettlementOwnerChanged(...)` 当前只通过 `_fiefDataMap.TryGetValue(settlement,...)` 处理城堡/城镇，而 `_fiefDataMap` 键不是村庄。Worker 需在原代码之外加入村庄预备队的专门分支；城堡/城镇易主时还应刷新其绑定村庄。`party.ActualClan = village.OwnerClan`；同时确保 `PartyOwner`、`HomeSettlement`、`MapFaction` 等计算与现主人同步。不得清空 Ready 或重做新游戏初始兵。
2. **村庄归属/绑定变化**：村庄与上级城镇/城堡绑定变动时，仍应是同村的一支队伍；原 `VillageFiefData` 归属与模板、征召权限的同步须保持现有规则。不要仅凭村庄名称重建。
3. **被异常销毁**：`OnPartyRemoved(PartyBase)` 如果检测到预备队，立刻从运行期索引移除，并将对应村庄标记 `RequireRecovery("ReservePartyRemoved")`；记录 VillageId、PartyId、可取得的实际 Ready/受影响数量和当时是否在战斗。**不能走 `SettleRemainingFiefServiceAsLoss` 去把库存当服役批次处理；也不能静默重新生成原兵力。** 对纯空队伍若需要重建，必须明确证明且保证不重建旧 Ready 兵数。
4. **重复队伍**：发现同村多支 ReserveParty 时冻结该村生产/征召，输出所有 PartyId 和名册计数；不要用 `FirstOrDefault` 随意选一个，也不要把两支名册相加。
5. **民兵毁灭**：原 `CleanupRemovedVillageMilitiaParty` 不再修改封建兵状态。`OnMobilePartyCreated` 的民兵修复逻辑可移除；如新 Party 创建事件需要索引，用组件类型显式过滤且防重入。
6. **跨日/跨周**：`OnDailyTick` 与 `WeeklyUpdate` 先检查 ReadyParty 可用，不可出现 `GetReadyRoster() == null` 仍扣 Hearth、清 ReturnBatch 或记“生产成功”的情况；原有冷却、升级快照遍历/回滚逻辑继续保留。

### P5 — 必须实测并闭环的原版引擎问题（最高优先级）

以下尚无法从已取到的仓库 `code` 目录证明，**不能在施工报告中宣称已解决**：

- **进入村庄是否会自动参战**：`MilitiaPartyComponent.CreateMilitiaParty` 使用 `EnterSettlementAction.ApplyForParty`，但“普通自定义 Party 在村庄 `CurrentSettlement` 时是否自动被 `GetDefenderPartiesOfSettlement` 收集”需核实 EncounterModel 的真实实现及游戏行为。建议先按禁 AI、驻留村庄的最小实现验证；若变成守军，V1 应通过经过验证的可维护接入点排除“仓库型” Party 参与战斗；如必要才增加**最小且精准**的兼容补丁。不得通过 `IsVisible=false` 或 `AvoidHostileActions=true` 推断绝对安全。
- **空队伍原版自动清理**：让至少一支空 ReserveParty 连续运行 14 天，确认它不会被 Disband/Destroy 或定居点行为自动移除。如果会，则必须阻断对应原版清理路径或引入安全恢复机制；不能反复建队造成事件风暴。
- **驻村状态/移动状态**：`MobileParty.SetShortTermBehavior()` 可能把普通 `Hold + NavigationType.None` 的非民兵/非驻军 Party 的导航模式改回 Default。要验证新队伍真实位置、AI、移动/交互、地图可见性，不能认为一次 `Ai.DisableAi()` 和 `DesiredAiNavigationType=None` 就永久固定。
- **战败/洗劫/围村**：如果村庄遭劫，必须以 V1“库存型、不参战”为准，确认预备队不会异常损失、自动成为敌人目标、从村庄中被强制驱逐或进入地图战斗。是否另设“洗劫摧毁 Ready”属于独立游戏规则，本次不自作主张添加。
- **实际保存/读档**：组件 `VillageSettlement` 属性能恢复且 `MemberRoster` 可读，`OnInitialize` 不会覆盖原版民兵字段，读档 `MobileParty.All` 扫描找到同一队伍，无额外复制。

**阻断条件**：上述任一项失败且不能证明有安全修复时，不算施工完成；需要向用户报告确切的原版调用路径和一个最小修复方案，而不是宣称“已经稳定”。

## 4. 建议 Worker 施工顺序与提交粒度

| 阶段 | 交付点 | 可检验输出 |
|---|---|---|
| S1 | 新 Component + save ID + Manager 索引/唯一性 + session 初始化 | 创建一次每村一队；读档扫描不重复 |
| S2 | `GetReadyRoster` 切换 + 删除原民兵 Patch/绑定/模板重叠禁令 | 初始兵和每日兵只进入 ReserveParty；原版民兵独立 |
| S3 | Food / Wage / PartySize 三模型 | 无食耗、工资 0、无虚假超编；原版逃兵条件未命中 |
| S4 | 易主、创建销毁事件、异常恢复门禁 | 无误增兵、无重复 Party、失联冻结而不是造兵 |
| S5 | 战斗/驻村/空队伍生命周期专项验证 | 原版行为可复现、可解释并符合非战斗库存语义 |
| S6 | 新游戏→跨日→跨周→征召→归还→读档长跑 | 全部验收矩阵通过，双 Release 编译和架构门禁通过 |

请勿“先整体替换，再用一个大 catch 包住”，而应每阶段让编译及核心行为可测。若使用 Git 提交，推荐分阶段独立提交；**不得擅自改造外交/粮食/职业兵的无关系统**。

## 5. 游戏内验收矩阵（必须实际执行并记录数值）

| ID | 场景/步骤 | 成功判据 |
|---|---|---|
| T01 | **全新开局**，抽样玩家、AI 的 ≥6 个不同文化村庄 | 每个由 FiefData 管理的村庄恰好一支独立 ReserveParty；其来源 Settlement 正确，`IsMilitia/IsGarrison/IsLordParty/IsCaravan=false` |
| T02 | 对比村庄原生民兵名册与新预备队名册 | 原生民兵依原版维持，封邑生产/初始兵仅在新 Party；不会双算/混编 |
| T03 | 连续运行 14 天，包含完全空的 ReadyParty | 无自发移动、无自动清理循环、无食耗、无意外工资/欠薪、无原版逃兵；空队伍仍可访问 |
| T04 | 新开局记录某村 `Ready`，保存退出并加载 | 同一村恰好一支 Party、Ready 兵种/人数/伤兵保持一致；无再次初始产兵 |
| T05 | 每日封邑补员，记录前后 Ready、Hearth 与 Supply | 只在新 Party 追加正确兵种/数量；总供养不变量成立 |
| T06 | 从村庄手动征召一批兵 | 新 Party Ready -N，目标队伍 +N，村 Hearth -N，MobilizedHearth +N，ServiceBatch +N；无民兵变动 |
| T07 | AI 征召及失败注入（目标满编、Hearth 不足、来源名册突变） | 成功时同 T06；失败时所有库存、Hearth、批次、目标名册原样回滚，不多不少 |
| T08 | 在外服役期间升级、战斗伤亡，调用每日 reconciliation | ServingBatch 与实际成员按既有升级树关系对账，损失和免薪天数正确；Ready 不受影响 |
| T09 | 解散/返还封建兵，跨过 ReturnCooldownWeeks | 先进入 ReturnBatches；冷却到期后仅写入新 Ready Party，Hearth 返还规则不变 |
| T10 | 跨越每周 Tick，包含多个 ReturnBatch 和自动升级 | 无 `Collection was modified`、无重复升级、无兵数丢失；特别回归旧 WeeklyUpdate 崩溃点 |
| T11 | 正常发生原版民兵损耗、民兵 Party 被毁后重建 | 原版民兵遵循本体移除逻辑，封建兵 Ready Party/库存完全独立 |
| T12 | 村庄/所属城堡或城镇易主，之后继续每日刷新 | 新预备队所有者/派系同步；不生成第二支、不清空库存、不重复初始化；权限按现有逻辑执行 |
| T13 | 村庄受敌军袭击/洗劫/遭遇战、玩家在附近 | 新 Party 不作为独立战斗队伍、不被敌军当目标；若游戏自动将其列入守军，必须定位并修正才算通过 |
| T14 | 故意制造失活或重复 ReserveParty（调试构建） | 记录精确异常并禁止危险生产/征召；无默认悄悄补全、无重复记账 |
| T15 | Feudal↔Professional 转换、供养不足、UI 查询、AI 兵力判断 | 所有业务仍以唯一 Ready 为准；源库存正确、回滚正确、显示与真实人数一致 |
| T16 | 运行 30～100 游戏日；周期性保存/读档 | Party 数量稳定，预备队无无故丢失、无食耗工资、无大规模逃兵、无负 Hearth/负 roster、无连锁异常 |

**建议日志字段**：`villageId`、`reservePartyId`、`sourceFiefId`、`readyCount`、`servingCount`、`coolingCount`、`mobilizedHearth`、`event`（Created/Reused/Loaded/Removed/Duplicate/OwnerChanged/RecoveryRequired）、`reasonCode`。默认仅异常与玩家相关关键事件 Info；高频成功路径 Debug，避免日志爆炸。

## 6. 最终验收与交付规则

**静态验收：**

- [ ] `ModifiedArmy` Release 编译通过，0 errors；如果项目要求，同步 `ModifiedPolitics` Release 编译通过。
- [ ] 现有架构依赖门禁通过，未引入 Army → Politics 反向依赖。
- [ ] `GetReadyRoster()` 不再引用 `MilitiaPartyComponent`；封建兵写库存链上没有村庄民兵 `MemberRoster`。
- [ ] 过时民兵保护 Harmony 已删除；原版 `RemoveMilitiasFromParty` 逻辑恢复。
- [ ] `FiefSaveDefiner` 新增类型 ID 13；已有 ID 没有重排；新存档正常保存读档。
- [ ] 未新增冗余 `TroopRoster` 影子库存；未复制或直接清空 `ServiceBatches`、`ReturnBatches`、`MobilizedHearth`。
- [ ] 三个模型覆盖自定义预备队；不影响 Lord、Garrison、Caravan、Supply、Reinforcement 等其他队伍。
- [ ] 没有实现旧档迁移、旧民兵封建兵识别/搬运（本任务明确不需要）。

**动态验收：** T01～T16 的测试记录；至少 T01、T03、T04、T06、T09、T10、T11、T12、T13 必须在游戏内实际验证，不能用编译通过替代。没法运行游戏时标记 `BLOCKED: in-game validation`，不得写 PASS。

**Worker 最终汇报格式**：

1. 改动文件清单及每个文件的核心更改；
2. 实际编译与门禁结果；
3. 各测试项 PASS/FAIL/BLOCKED 和观察到的真实数值；
4. 新 Party 创建/加载/士气/食物/工资/容量/逃兵/守村参与的真实行为；
5. 未能确认的原版引擎调用点和风险；
6. 如果执行部署，交付实际生成的 `ModifiedArmy.dll`（按项目现有部署路径），不要只给本地伪链接；
7. 不要声称修复旧存档，因为明确不支持。

## 7. 可直接复制给 Worker / Codex 的任务指令

> 在 `GuihuaZhou/bannerlord` 仓库施工 **“ModifiedArmy 村庄封建兵独立 MobileParty 存储改造 V1.0”**。请先读取此文档并检查当前 `ModifiedArmy/ModifiedArmy/` 与 `code/` 的真实实现，按 S1～S6 顺序实施。每村建立独立持久的 `FiefReservePartyComponent` `MobileParty`，让 `VillageFiefData.GetReadyRoster` 唯一指向其 MemberRoster；保留现有 ServiceBatches/ReturnBatches/MobilizedHearth 的权威记账和事务回滚；撤销原版民兵保护 Patch；修改食物、工资、容量模型；原版逃兵系统无需特殊补丁。严格保障新游戏存档/读档不会重复建队或重复初始增兵。**不考虑任何旧存档兼容，也不执行旧数据迁移。** V1 是被动库存型 Party，不应自发移动或主动/被动参战；驻村是否被纳入战斗、空队伍是否被清理必须定位和游戏实测。优先维护既有架构，不能凭推测声称原版接口安全；无法游戏测试的事项如实标记 BLOCKED。提供 Release 编译、门禁与逐项测试结果。

## 8. 源码定位链接

**本体 `code`：**

- [`MobileParty.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/MobileParty.cs)
- [`PartyComponent.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/PartyComponent.cs)
- [`MilitiaPartyComponent.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/MilitiaPartyComponent.cs)
- [`GarrisonPartyComponent.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GarrisonPartyComponent.cs)
- [`DesertionCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DesertionCampaignBehavior.cs)
- [`DefaultPartyMoraleModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultPartyMoraleModel.cs)
- [`DefaultPartyDesertionModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultPartyDesertionModel.cs)
- [`DefaultMobilePartyFoodConsumptionModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultMobilePartyFoodConsumptionModel.cs)
- [`DefaultPartySizeLimitModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultPartySizeLimitModel.cs)

**当前 `ModifiedArmy`：**

- [`FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [`VillageFiefData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [`FiefPartyData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [`FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [`FiefPartyRecruitment.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [`FiefPartyReturn.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [`FiefTroopConversionService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Services/FiefTroopConversionService.cs)
- [`SettlementPatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Patches/SettlementPatch.cs)
- [`NewMobilePartyFoodConsumptionModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Logistics/Models/NewMobilePartyFoodConsumptionModel.cs)
- [`NewPartyWageModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/NewPartyWageModel.cs)
- [`NewPartySizeLimitModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewPartySizeLimitModel.cs)
- [`Main.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Main.cs)

---

**版本说明**：本设计根据 2026-11-05 访问到的仓库 `main` 路径及当前对话已确认的需求编写；如 Worker 开工时仓库代码已变化，应以实际接口为准记录差异。**未实际编译或进入游戏，不得把“设计上可行”表述为“已经测试通过”。**
