# ModifiedArmy — FiefParty 双入口征召/解散与精确来源批次 Worker 修改方案 V1.1

> **用途**：交付给 AI Worker / Codex 的源码级实施指令。不是已完成实现报告。  
> **仓库**：`GuihuaZhou/bannerlord`  
> **审核基准 HEAD**：`e74f20a4d9ab65282c3f527b1bcc459cfab012b4`（相对上一轮设计基准 `4610e3a1` 的村庄化提交）  
> **目标模块**：`ModifiedArmy`  
> **日期**：2026-11-01  
> **适用存档**：用户没有需要兼容的旧存档；新存档的可靠保存、加载、运行时索引重建仍为强制要求。

## 1. 用户已确定的业务规则（必须原样遵守）

1. **在役批次来源必须明确。** 保留来源城镇/城堡（`SourceFief`），并明确持久化实际提供士兵的村庄（`SourceVillage`）；一个批次只能属于一个来源村庄。
2. **允许封邑级征召和解散。** 领主位于城镇/城堡时，可以统一征召、解散该封邑下合法附属村庄的封建兵。
3. **允许村庄级征召和解散。** 领主位于村庄时，只能征召该村的就绪封建兵；只允许该村来源的在役批次返乡（解散）。
4. **征召地点不改变原籍。** 即使从城堡统一征召，士兵仍属于来源村庄；正常返乡及冷却结束后回到原村庄民兵 `MemberRoster`。
5. **不发生“村庄之间调兵”。** 城镇/城堡统一操作仅是多村庄协调，禁止将A村兵员先移到B村、跨村代扣/代还 Hearth，或把不同村庄的在役批次直接合并成失去来源的记录。
6. **坚持现有两层结构。** `VillageFiefData` 持有村庄封建兵生命周期权威状态；`FiefPartyData` 绑定城镇/城堡并负责封邑级汇总、审批与协调；`FiefPartyManager` 提供统一外部入口。不能把村庄批次重新迁回外壳。
7. **继续保留野战Party级统一对账。** 原版 `MobileParty.MemberRoster` 不记录逐兵村庄原籍，同兵种混编时仍用现有全Party `FiefServiceReconciler` 将生存数/升级结果按可重复的批次分配规则归回各村。这里的“统一”是对同一个野战Party做一次匹配，**不意味着跨村兵员迁移**。

### 1.1 可操作范围矩阵

| 实际驻扎/交互地点 | 征召候选 | 正常返乡/解散候选 | 不允许 |
|---|---|---|---|
| 城镇X | 当前合法附属村庄A、B、C的就绪封建兵 | 来源封邑为X且来源村庄属于本封邑正常合法范围的批次 | 别的城镇/城堡的兵源 |
| 城堡X | 当前合法附属村庄A、B、C的就绪封建兵 | 来源封邑为X且来源村庄属于本封邑正常合法范围的批次 | 别的城镇/城堡的兵源 |
| 村庄A | **仅A村**就绪封建兵 | **仅 `SourceVillage == A`** 的批次 | B、C村兵员与批次 |
| 村庄B | **仅B村**就绪封建兵 | **仅 `SourceVillage == B`** 的批次 | A、C村兵员与批次 |

“正常解散/返乡”指 `FiefReturn` 和普通玩家归乡行为；**正式解散野战Party**的 `PartyExchangePurpose.PartyDisbandment` 是现有代码的特殊收尾流程，不要因增加驻地限制就使原有强制收尾、死亡结算功能失效。特殊收尾规则必须由明确的Purpose/业务Service授权，不得暴露一个可任意绕过地点校验的布尔参数。

## 2. 实际源码确认：新提交已做了什么、还缺什么

基于最新提交 `e74f20a4` 实际代码：

- `Fief/Models/VillageFiefData.cs`：新类保存村庄、`ServiceBatches`、`ReturnBatches`、`MobilizedHearth`、`RecoveryRequired`，动态访问原版村庄民兵Roster；结构基本符合需求。
- `Fief/Models/PartyData/FiefPartyData.cs`：`[SaveableRootClass(2)]` 的封邑外壳保存 `Dictionary<Settlement,VillageFiefData> Villages`（`[SaveableProperty(8)]`）；仍以城镇/城堡为 `_settlement`。旧 `_fiefParty` 已变为汇总快照属性；不应恢复为独立权威库存。
- `Fief/Models/FiefPartyManager.cs`：`_fiefDataMap` 只登记城镇/城堡，`GetVillageFiefData()` 是内部查找；`RecruitFiefTroopsFromSettlement()`、`GetFiefTroopRoster()`、`ReturnTroopsToSettlement()`、`ManualRecruitFromFief()` 等仍仅用 `_fiefDataMap.TryGetValue(settlement,...)`，村庄传入会失败。
- `Fief/Models/Data/FiefServiceBatch.cs`：已有 `[SaveableRootClass(6)]`；持久化属性ID 1—6依次为 `Troops`、`ServingPartyId`、`ServiceDays`、`FreeWageDays`、`LastServiceUpdateDay`、`BatchId`。**尚无 `SourceFief` / `SourceVillage` 属性。**
- `Fief/Models/FiefServiceReconciliation.cs`：`ServingDetachmentRef` 已有运行时 `SourceFief`、`SourceVillage`、`Detachment`；现有匹配算法能在野战Party级处理多来源，应复用。
- `Fief/Services/FiefServiceLedger.cs`：已从每个 `VillageFiefData.ServiceBatches` 重建运行时索引，正确方向；需增加来源字段一致性校验。
- `Fief/Services/FiefReturnService.cs`：当前 `TryReturn(manager,party,sourceFief,batchId,max)` 仅按 `SourceFief + BatchId` 定位，再据选中记录找到Village；未提供按**操作村庄**限制范围的入口。
- `ForceManagement/Models/PartyExchangeModel.cs`：`BuildFiefMobilizationPlan()` 使用 `context.Source.Settlement`，要求 `destination.CurrentSettlement == sourceFief`；`BuildFiefReturnPlan()` 以目标Settlement为`fief`，按 `SourceFief` 找批次；村庄作为交互地点尚未正确路由。
- `ForceManagement/Services/PartyExchangeService.cs`：`ExecuteFiefMobilization()` 和 `ExecuteFiefReturn()` 仍按“封邑=交互地点”验证与提交，未处理来源村庄范围。
- `ForceManagement/Contracts/UnifiedForceContracts.cs`：`FiefBatchTransferApproval` 目前只有 `SourceFief`、`BatchId`、`ApprovedCount`、`Troops`，尚无 `SourceVillage` 或交互范围信息；`PartyTroopSource` 已有 `Settlement` 和 `BatchId` 可承载交互地点。
- `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`：从 `manager.GetFiefTroopRoster(settlement)` 构建来源；需要原接口能按Settlement范围工作。
- `Fief/Models/FiefMenuBehavior.cs`：`TOWN_MENU_ID="town_keep"`、`CASTLE_MENU_ID="castle"`；文件注释以及 `AddGameMenus()` 均明确排除村庄。`CanSelectTroop()` / `OnRecruitDone()` / `ConfigureFiefModificationOption()` / `ReturnToSettlementAdministration()` 等也假设城镇/城堡。
- `ForceManagement/Services/WarRestPlanningService.cs`：目前按 `SourceFief` 为战时休整目标，不应改变城镇/城堡默认休整规划。村庄操作属于增加的合法入口，不等于要求AI改为优先访问村庄。
- `ForceManagement/Services/DisbandSettlementService.cs`：有专门的Party解散清算路径，必须保持其既有豁免和战损兜底语义。

**结论：当前提交已实现村庄持有数据，但“来源身份持久化 + 双操作地点路由 + 预览/执行/菜单/归乡闭环”尚未完成。**

## 3. 数据结构修改：FiefServiceBatch 精确来源（P0）

### 3.1 修改文件

`ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefServiceBatch.cs`

保留类型 `FiefServiceBatch`、现有 `BatchId`、`SplitTo()`、服务天数及工资豁免逻辑。新增两个明确、稳定且随批次持久化的身份字段。建议在目前1—6已占用的属性号之后使用7和8；Worker执行前再次检查实际Definer与占号情况：

```csharp
[SaveableProperty(7)]
public Settlement SourceFief { get; private set; }

[SaveableProperty(8)]
public Settlement SourceVillage { get; private set; }
```

`Create()` 建议改为接收 `(Settlement sourceFief, Settlement sourceVillage, MobileParty party, int freeWageDays, IReadOnlyDictionary<CharacterObject,int> troops)`；必要时使用来源对象表达，但不要默默在默认构造器中猜来源。

**创建前校验：**

- `SourceFief` 是Town或Castle。
- `SourceVillage` 是Village。
- `SourceVillage` 对应一个实际合法的 `VillageFiefData`。
- 常规新征召时，来源村庄在当前合法附属关系中，且领主具有现有权限。
- 同一个 `BatchId` 不得被两座村庄同时登记。

`SplitTo(targetParty,movedTroops)` 必须**原样继承** `SourceFief` 和 `SourceVillage`，连同 `ServiceDays`、`FreeWageDays`、`LastServiceUpdateDay`。拆分必须生成新 `BatchId`，不能生成新的动员Hearth。

### 3.2 权威归属与重复字段的关系

批次仍然**只**保存在来源 `VillageFiefData.ServiceBatches`。`SourceFief`/`SourceVillage` 是不可变的历史身份属性，不是另一份可写兵员库存。运行时 `ServingDetachmentRef` 可继续携带两者，但必须从批次字段创建/校验，严禁默默以当前村庄 `Bound` 覆盖历史 `SourceFief`。

若保存的批次来源与其持有者Village不符，或 `BatchId` 冲突：记录错误、标记 `RecoveryRequired` 并停止危险操作，不能把批次自动迁移至另一个村庄来“修复”。

### 3.3 不改变 FiefReturnBatch 的身份存储方式

目前每座 `VillageFiefData` 持有自己的 `ReturnBatches`，其归属由持有者直接确定。只要冷却批次绝不跨村移动，本次**不要求**额外在 `FiefReturnBatch` 上复制 `SourceFief`/`SourceVillage`；对冷却提交过程仍须确保来源一致。

## 4. 操作范围解析：统一城镇/城堡与村庄入口（P0）

### 4.1 修改文件

`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`，必要时在Fief领域新增仅运行时使用的 `FiefOperationScope` / `FiefOperationResolver`。

**不要**向 `_fiefDataMap` 添加一份 `Village -> FiefPartyData` 映射；继续保持旧公开 `GetFiefData(Settlement)` 语义（仅Town/Castle）。保留当前 `GetVillageFiefData()`，可将其适度封装为对外只读查询入口。

内部统一解析操作地点（示意，不是强制编译签名）：

```csharp
// Context: interaction settlement is the party's actual current settlement.
ResolveScope(Settlement interaction, MobileParty party)
    -> { SourceFief, InteractionSettlement, VillageFilter? }
```

- `interaction.IsTown || interaction.IsCastle`：`SourceFief = interaction`，`VillageFilter = null`，候选范围为当前合法附属村庄。
- `interaction.IsVillage`：`SourceFief = interaction.Village.Bound`，`VillageFilter = interaction`，候选范围只有本村。
- 其他类型、失效对象、归属不明、领主未实际驻扎于交互地点：拒绝。
- 历史批次的 `SourceFief` 是创建时来源；**不能**用当前 `Bound` 反向改写已存在的来源记录。
- 对正常征召、正常返乡检查 `party.CurrentSettlement == interaction`、在现有战斗状态限制内、所有权与允许范围符合既有规则。异常的正式Party解散路径走原有特殊Purpose，由该业务Service显式授权。

`FiefPartyData` 应有“封邑全部合法村庄”和“某一个合法村庄”两个**内部选择范围**。建议使用同一执行路径加村庄过滤条件，不复制一整套村庄征召算法。

### 4.2 统一聚合查询

`GetFiefTroopRoster(Settlement interaction)`：

- Town/Castle：返回封邑全部合法附属村庄就绪兵员的**新建快照**。
- Village：返回该村就绪封建兵快照。
- 不返回任何原版Roster的可修改引用。

同样调整用于预览的：

- `PreviewFiefRecruitmentCandidates(interaction, party, count)`
- `PreviewFiefRecruitmentApprovals(interaction, party, count)`
- `GetAvailableTroopCount(interaction)`（若被村庄UI使用）
- `ManualRecruitFromFief(interaction, selectedRoster, targetParty)`

未要求修改经济计算的其他封邑总量API（如 `GetFiefTroopCount(fief)`、`GetFiefTroopLimit(fief)`）应保留原有封邑语义。可以另增村庄只读查询，勿让旧调用意外变为“仅当前村庄”而破坏其他模块。

### 4.3 统一征召 API

当前 `RecruitFiefTroopsFromSettlement(settlement,party,max,expectedApprovals)` 保留签名，传入 `settlement` 解释为**实际征召地点**：

- Town/Castle：外壳从全部当前合法附属村庄统一规划和审批，实际从对应村庄扣兵。
- Village：只允许该村候选，使用相同兵种模板、AI/玩家审批与村庄动员事务。

不允许为村庄单独再创建一份 `FiefPartyData` 或用 `FiefPartyData` 的 `_settlement` 临时改成村庄来兼容。

## 5. 征召事务：保持单一实现，来源与审批精确对应（P0）

### 5.1 修改文件

`Fief/Models/PartyData/FiefPartyRecruitment.cs`，按组织规则可将执行逻辑抽取至 `Fief/Services` 下的事务Service；`FiefPartyData` 只负责封邑级协调。

当前 `TryCommitVillageRecruitment()` 已建立按来源村庄的 `VillageRecruitmentSource`、逐村扣Roster、扣Hearth、创建批次和加兵到Party。这部分可以保留，不必重写业务模型，但必须：

1. 接受已解析的村庄范围：`villageFilter` 为空表示封邑全部合法附属村庄，非空表示仅指定村庄。
2. 在同一范围内运行 **AI/玩家预览、权重分配、审批和实际执行**。不得封邑级预览、村庄级执行，或反过来。
3. 创建 `FiefServiceBatch` 时传入精确 `SourceFief`、`SourceVillage`；从城堡征召也不能把 `SourceVillage` 写成城堡。
4. 征召事务开始前校验每座来源村庄的实际归属、民兵Roster、健康/伤员数、Hearth100保底、Party容量和原有预算条件。
5. 使用现有**封邑范围内只计算一次**的AI兵种权重（`BuildTemplateWeightedRecruitmentCandidates()`），然后将批准兵员分配到可提供该兵种的来源村庄。村庄单独招募则只按该村库存计算。**不要对每村分别再套用一次封邑总权重额度。**
6. 保留同一野战Party的多来源多个独立 `FiefServiceBatch`；不新建跨村“合并服役批次”。

### 5.2 计划/执行稳定性

当前 `PartyExchangePlan` 主体只记录按 `CharacterObject` 聚合的批准数量，未记录村庄分配清单。这样两村库存变化、聚合数量不变时，可能在执行时改从另一个村庄征召。

建议在**运行时**的征召计划中记录已选定的 `(SourceVillage,Troop,Count,Wounded)` 分配，或者记录等效的来源村庄指纹，在执行前验证；不要求将临时计划持久化成第二份兵员权威。失效时返回明确的 `SourceVillageManifestChanged` 一类错误并重新规划，不要悄悄换来源。

`expectedApprovals` 的按兵种聚合格式可以为兼容旧接口继续保留，但执行必须有额外的村庄维度再验证。

### 5.3 事务安全（本次新增入口前必须修）

当前 `TryCommitVillageRecruitment()` catch 分支对 `GetReadyRoster()` 使用空条件恢复，可能**跳过实际还兵**而仍返回失败；`CreateServingDetachment()` 可能在批次写入后、记入 `created` 列表前抛异常。

必须修正：

- 写入前生成逐村准确回滚日志/补偿快照，定义所有写点的前置条件及补偿顺序。
- 任何已部分写入的批次和索引必须能追溯（可将写入步骤拆为 prepare/commit，或通过明确事务记录追踪）。
- 若回滚无法恢复某村Roster、野战Party、动员Hearth或批次，设置持久化 `RecoveryRequired` 并阻断进一步征召/返乡；禁止单纯 `catch { return false; }`。
- 预算提交与兵员提交顺序要保持现有合同一致；部分成功不得伪报完全成功。

## 6. 解散/返乡：同一服务，地点决定可返还批次（P0）

### 6.1 修改文件

- `Fief/Models/FiefPartyManager.cs`
- `Fief/Services/FiefReturnService.cs`
- `Fief/Models/PartyData/FiefPartyReturn.cs`
- `ForceManagement/Models/PartyExchangeModel.cs`
- `ForceManagement/Services/PartyExchangeService.cs`
- `ForceManagement/Contracts/UnifiedForceContracts.cs`

### 6.2 普通正常返乡的批次选择

- 城镇/城堡X：允许属于 `SourceFief == X`、来源村庄仍处于合法可操作范围内的批次。对历史归属变更的批次，不得静默改写来源；应按当前权限策略拒绝或进入明确的历史来源处置流程。
- 村庄A：严格 `batch.SourceVillage == A`，并确认它属于实际驻扎Party的有效服役批次；**不能因 `SourceFief` 相同就把B村、C村的兵在A村解散**。
- 无论操作地点在哪里，成功返乡都恢复 **`SourceVillage`** 的动员Hearth，并在原村庄 `ReturnBatches` 中创建冷却记录；冷却结束后回原村民兵Roster。
- 已有 `FiefReturnResult.SourceFief` 等公开结果可以保留；如果 UI/日志需要来源村庄，可补充返回字段（先查实际结果类型及其存档/外部依赖）。

### 6.3 兼容现有返回接口

现有 `TryReturnFiefTroops(party,sourceFief,batchId,max)` 用于战时休整和解散Service，保留签名。要支持玩家在村庄操作，建议增加**显式交互地点**的内部调用/重载，并统一进入一个地点与权限验证点：

```csharp
// 示意
TryReturnFiefTroopsAt(
    party, interactionSettlement, sourceFief,
    sourceVillage, batchId, maximumCount, purpose);
```

- 普通玩家或普通AI返乡：使用 `party.CurrentSettlement` 作为实际交互地点，并进行范围过滤。
- 战时休整：原有 `WarRestPlanningService` 仍可只选择 `SourceFief`（城镇/城堡），不强迫AI改为村庄寻路。
- `PartyDisbandment`：维持原有特殊强制清算路径；必须由现有解散Service授权，不得以一个公开开关跳过所有位置检查。
- `FiefReturnService` 先从运行时索引定位唯一 `(batchId,sourceFief,sourceVillage)`，验证后再执行原有 `TryCommitFiefReturn()`，不能通过村庄位置随意选一个同兵种批次替代。

### 6.4 PartyExchange 批次批准对象

`UnifiedForceContracts.cs` 中当前 `FiefBatchTransferApproval` 只记录 `SourceFief` 和 `BatchId`。建议新增运行时 `SourceVillage`，如有必要另提供 `InteractionSettlement`（也可直接使用既有 `plan.Destination.Settlement`）。

`BuildFiefReturnPlan()` 应按**真实操作地点**过滤：

- 城镇/城堡：找到该封邑符合权限的目标批次。
- 村庄：必须同时匹配来源村庄。

`ExecuteFiefReturn()` 再次核验相同来源与地点，向真实来源村庄事务提交，禁止预览时批准A村、执行时返回B村记录。

## 7. PartyExchange 和 AI 访问入口（P0）

### 7.1 PartyExchangeModel.cs

`BuildFiefMobilizationPlan()` 目前假设来源Settlement即封邑，条件包括 `destination.CurrentSettlement == sourceFief`。改为：

- 保留 `destination.CurrentSettlement == context.Source.Settlement` 的**实际驻留地点**校验。
- 通过统一 `ResolveScope()` 获得封邑及村庄过滤范围。
- 使用 `manager.GetFiefTroopRoster(interaction)` 和 `PreviewFiefRecruitmentApprovals(interaction,...)` 创建与当前地点一致的计划。
- 非法村庄归属、战斗中、围城状态及不符合原有所有权条件时拒绝。

`BuildFiefReturnPlan()` 同理：判断交互目标是城镇/城堡还是村庄；候选批次使用 `SourceFief + SourceVillage + BatchId`；与现有 `PartyDisbandment` 特殊分支兼容。

### 7.2 PartyExchangeService.cs

`ExecuteFiefMobilization()` 重新验证 `SourceScope`、实时村庄分配和Approval。若与预览不符，返回具体ReasonCode；否则调用统一Manager提交入口。

`ExecuteFiefReturn()` 重新验证实际驻扎地点、批次来源和合法Purpose，并调用真正的村庄返乡事务。保持现有完整成功、部分成功、拒绝三态结果契约。

### 7.3 UnifiedRecruitmentExecutionService.cs

`ExecuteFief()` 当前从 `manager.GetFiefTroopRoster(settlement)` 获得名册快照，只需保证这个Manager接口能按当前Settlement返回**正确范围**；`PartyTroopSource.Settlement` 必须保留当前实际交互地点，不应悄悄替换为父封邑，否则规划与执行会错误允许从其他村庄取兵。

### 7.4 WarRestPlanningService / DisbandSettlementService

目前战时休整以来源封邑 `SourceFief` 为目的地，这仍是合法且推荐的旧行为。不要因为新增村庄入口，强制AI每次去原村庄归乡。若以后要让AI主动到村庄归乡，应独立设计AI寻路策略，本次仅实现**已在村庄时可以合法操作**。

`DisbandSettlementService` 的正式Party解散、转入守军、不可安置兵员损失结算保留，重点核对拆分批次和 `SourceVillage` 一直不变。

## 8. FiefMenuBehavior：村庄菜单新增入口（P0）

### 8.1 当前实现

`Fief/Models/FiefMenuBehavior.cs` 目前 `TOWN_MENU_ID="town_keep"`、`CASTLE_MENU_ID="castle"`，原注释明确“Villages are explicitly excluded by design”。`CanModifyCurrentFief()`、`ConfigureFiefPoolViewOption()`、`SetFiefIntroductionText()`、`OnRecruitDone()`、`OpenReadOnlyFiefTroops()`、`ReturnToSettlementAdministration()` 均需核对当前Settlement假设。

### 8.2 改造方式

1. **不要复制第二套菜单Behavior。** 尽量复用现有征召/解散/部分选择/查看士兵的选项与回调。
2. 在**原版村庄实际可用的菜单ID**添加入口；Worker需先确认所用游戏版本中原版村庄菜单标识、进入时机、返回菜单路径，不要凭猜测硬编码。
3. 城镇/城堡保留当前菜单位置、文案和全部封邑范围。
4. 在村庄打开菜单时，显示明确的“本村封建兵”范围：可征召、在役、冷却数据都来自对应 `VillageFiefData`。
5. `OnRecruitDone()` 将当前Settlement作为交互地点传给Manager，村庄选择不能转变为整个封邑兵种汇总。
6. `CanSelectTroop()` 应使用当前交互范围的只读Roster，避免旧 `GetFiefData(CurrentSettlement)` 对村庄返回null。
7. `ReturnToSettlementAdministration()` 在村庄时返回原村庄菜单，不得错误跳转到城堡菜单。
8. `ConfigureFiefModificationOption()` 在村庄仍核实玩家所有权、实际驻扎、是否可操作，外来家族的村庄不可凭所属封邑关系绕过限制。
9. 中文/英文显示遵循现有本地化键；新增可见字符串提供英文基础文本和中文翻译，不在C#硬编码中文。

### 8.3 展示建议

- 城镇/城堡界面：统一显示全部附属村庄就绪、在役、冷却总数（可选逐村查看明细）；
- 村庄界面：仅显示本村就绪、在役、冷却数量；
- 在役批次明细：可明确展示 `SourceFief`、`SourceVillage`、`BatchId`、所服役Party、兵种与数量，必要时支持只读诊断，避免误会“跨村混兵”。

## 9. 现有村庄状态与运行时索引的保持要求

### 9.1 VillageFiefData.cs

`VillageFiefData` 继续持有 `ServiceBatches`、`ReturnBatches`、`MobilizedHearth` 和实际村庄民兵Roster访问。必要的村庄单独征召/返乡操作应由村庄级Service执行，避免 `FiefPartyData` 直接做过多Roster/Hearth资源写入。**不新增第二份Roster、第二份Hearth账本、第二份封邑级ServiceBatches。**

### 9.2 FiefServiceLedger.cs / FiefServiceReconciliation.cs

`Rebuild()` 遍历 `FiefPartyData.Villages[*].ServiceBatches`；验证批次 `SourceFief` / `SourceVillage` 与实际持有者一致，再注册 `ServingDetachmentRef`。如果持有者或来源不符，必须进入恢复状态，不允许悄悄按当前Bound改写。

`FiefServiceReconciler` 保留全野战Party原有同兵种、升级树、按批次分配算法；它不是村庄间征兵操作。对账结果的战损、升级和拆分只落到对应来源村庄。

### 9.3 村庄易主与历史来源

当前 `FiefPartyData.InitialFeifPartyData()` 只往 `Villages` 中补充当前BoundVillages对象，没有清晰区分“当前可生产/征召村庄”和“仍持有旧在役批次的历史来源村庄”。

必须引入只读有效性判断：

- 当前村庄属于封邑且权限正常：允许生产、正常征召、按授权返乡。
- 已失去附属或所有权但仍保存历史记录：**保留在役与冷却批次，禁止继续当作当前兵源生产或征召**。
- 原村庄Party暂时不存在：不清空在役/冷却记录，不把它们自动写进邻村。
- 发生归属变化后，需要对正常返乡执行当前所有权/来源校验；不能借助 `GetVillageFiefData()` 全局回退来绕过操作权限。

## 10. 必须一并修复的现有 P1 安全问题

这些问题在最新提交 `e74f20a4` 的静态代码中已经存在；新增村庄操作入口会让触发面扩大。属于本功能的上线阻塞项，**不是请求重写其他系统**。

**10.1 征召回滚可能漏还兵**  
`FiefPartyRecruitment.cs` 的 `TryCommitVillageRecruitment()` catch 中若 `GetReadyRoster()` 为null，会跳过恢复。必须持久化标记恢复失败并冻结受影响村庄，不能返回普通失败当作无副作用。

**10.2 CreateServingDetachment 的中途异常窗口**  
目前在创建函数内部先把批次加进村庄/索引，调用方随后才 `created.Add(batch)`；中间失败可能不能从事务列表回滚。应统一提交/回滚顺序并记录每一步写点。

**10.3 RecoveryRequired 被简单计数相等自动解除**  
`FiefPartyManager.ReconcileMobilization()` 当前只要 `MobilizedHearth == ServingCount` 就 `CompleteRecovery()`。应区分普通计数校验和显式恢复确认；不能用两个数字相等证明Roster、来源、Hearth已全部恢复。

**10.4 战损结算未校验Hearth释放结果**  
`FiefServiceReconciler` 及 `FiefPartyManager.OnPartyRemoved()` 部分路径调用 `ReleaseVillageFiefService(...)` 后直接删除批次。失败时不得继续当作成功结算。

**10.5 保护普通民兵独立衰减**  
`Fief/Patches/SettlementPatch.cs` 已阻止错误移除封建兵，但原版 `Settlement.Militia` 的衰减基数仍可能包含封建兵。此项应纳入游戏验证；如果确实抑制普通民兵的正常增长，另做狭窄修复，避免扩大本次接口改造。

## 11. 不得改动的业务边界

- 不改现有兵种模板、文化军事制度、AI权重、最低征召批次、Party容量判定、工资豁免天数。
- 不改每日生产概率、升级概率、现有平时/战时AI策略及常备军征召。
- 不在此轮实现 T5=20 Hearth 等级供养、50%税收减免、职业兵与封建兵互转。
- 保持当前出征每名临时扣1 Hearth、正常返乡返1、战损不返的**现行阶段规则**。
- 保留游戏原版民兵Party是就绪兵员真实库存，`SettlementPatch` 继续保护封建兵；只有具体源码验证后才允许微调补丁。
- 不修改无关Module、存档ClassId、经济公式、XML、本地化键或日志级别。
- 未经用户要求，不提交、不推送、不创建PR。

## 12. 推荐实施顺序与阶段门禁

### M1 — 批次精确来源（先做）

- 给 `FiefServiceBatch` 增加保存字段与创建参数。
- 更新 `FiefPartyData.CreateServingDetachment()`、`FiefServiceBatch.SplitTo()`、`FiefServiceLedger.Rebuild()`、`FiefServiceReconciler`、`TransferFiefService`。
- 编译+新游戏存档、读档的基本检查。

**门禁**：任何在役批次都能准确给出持久化 `SourceFief`、`SourceVillage`、Party、BatchId；拆分继承来源；重新加载索引不重写来源。

### M2 — 统一地点解析及村庄范围查询

- 完成 `ResolveScope` 或等价单一规则。
- Manager外部征召、预览、只读Roster、玩家手动选择统一按地点路由。
- 保持旧城镇/城堡入口。

**门禁**：A村UI和预览绝不出现B村士兵；城堡仍能汇总A+B村。

### M3 — 征召计划与事务改造

- 复用当前权重规划和村庄实际执行。
- 明确来源村庄分配、前置校验、提交、回滚。
- 修复 P1 征召恢复问题。

**门禁**：每份在役批次恰好属于提供士兵的村庄；野战Party接收数=各村Roster扣人数=新批次人数；失败无隐性损失。

### M4 — 解散/返乡双入口

- 调整 `FiefReturnService`、`FiefPartyReturn.cs`、`FiefBatchTransferApproval`、`PartyExchangeModel`、`PartyExchangeService`。
- 正常返乡地点严格限制来源村庄范围。
- 保持 `WarRest` 城堡/城镇来源与 `PartyDisbandment` 现有特别处理。

**门禁**：城堡能解散A+B来源批次；在A村只退A来源批次；B批次及动员Hearth不变。

### M5 — 玩家村庄菜单与AI接口联调

- 在原版村庄可用的菜单加入入口，复用原征召、返乡与局部选择UI。
- 修复退出菜单、只读查看、权限、当前驻扎校验。
- 检查 UnifiedRecruitment、WarRest、Disband 和 PartyExchange 原有调用仍可用。

**门禁**：村庄与城堡玩家操作完整，AI即使只访问城堡仍能按旧行为征召/返乡。

### M6 — 状态一致性、构建与运行验收

- 完成 `RecoveryRequired` 和战损Hearth结算的P1修复。
- 检查历史归属处理、民兵保护、重复日志及错误码。
- 编译Mod，执行仓库指定的适用验证脚本，进行新存档读写和游戏运行测试。

## 13. 强制验收矩阵（AI Worker 不得以编译成功代替）

| ID | 场景 | 预期 |
|---|---|---|
| A01 | 城堡X有A/B/C三村，领主在城堡征召50人 | 可从A/B/C分配，批次逐村独立、记录X与原村 |
| A02 | 领主在A村征召10人 | 仅A村减员/扣Hearth/创建A来源批次 |
| A03 | A村只有5人，B村有100人；在A村请求10人 | 不能借B村兵补足；返回实际批准人数或合法拒绝 |
| A04 | 从城堡征召A村和B村同种骑兵各10人 | 野战Party总20；两个批次各有明确SourceVillage |
| A05 | A04发生5人战损 | 全Party只结算5损失，按现有算法归到各来源村庄 |
| A06 | 城堡X统一解散 | X的合法来源批次分别进入原村冷却 |
| A07 | 领主在A村解散 | 仅A来源批次返乡，B来源批次与Hearth不变 |
| A08 | 领主在B村尝试解散A批次 | 拒绝，绝不把A兵员放进B村 |
| A09 | A村同种兵员与原版普通民兵并存 | 村庄封建兵识别与民兵保护不混淆 |
| A10 | 在役T4升级至T5后返乡 | 来源村庄不变，兵种对账按原升级树进行 |
| A11 | 在役批次转移至另一野战Party | `SplitTo` 继承来源、服务天数；不再扣Hearth |
| A12 | 来源村庄民兵Party暂不可用，冷却结束 | 冷却记录保留，未重复发兵，恢复后再入本村Roster |
| A13 | A村合法村庄归属发生变化 | 旧批次保留，新征召按当前关系授权，不越权读取 |
| A14 | Party正式解散（PartyDisbandment） | 原有特殊收尾/转守军/强制损失语义不被普通地点约束破坏 |
| A15 | 战时休整在原城堡返乡 | 现有 WarRest 正常工作，不被强制要求去原村 |
| A16 | 相同兵种A/B村库存变化但封邑总数不变 | 预览/执行不会悄悄替换来源村庄 |
| A17 | 跨村征召中途异常，所有资源均可恢复 | 各村Roster、Party、Hearth、批次、索引全部回滚 |
| A18 | 跨村征召中途异常，某村Roster不可恢复 | 持久化RecoveryRequired，冻结该村危险操作并输出诊断 |
| A19 | Hearth释放失败 | 不删除相应在役批次作为“已结清” |
| A20 | 存档/重新加载 | 批次SourceFief/SourceVillage不变，索引重建与人数一致 |
| A21 | 玩家在城镇/城堡打开封邑菜单 | 原有统一征召、部分选择、查看与解散入口正常 |
| A22 | 玩家在村庄打开封建兵菜单 | 仅显示本村状态，返回菜单正确，权限正确 |
| A23 | AI在城镇/城堡自动招募 | 不因新增村庄菜单及范围逻辑改变原有AI决策权重 |
| A24 | 文化相异/模板与原版民兵可能重叠 | 正确拒绝身份不明操作，不默认为全体同兵种皆封建兵 |

## 14. 交付检查清单

Worker开始前必须检查 `git status --short`、当前HEAD，阅读根 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`。如HEAD已变，更改位置以实际源码为准并在交付说明中写清差异。

Worker交付时至少报告：

1. 变更文件及每个文件具体修改的方法。
2. `FiefServiceBatch` 新增字段及 `Create/SplitTo` 行为；Saveable ClassId和PropertyId分配情况。
3. 城镇/城堡与村庄两种地点如何路由到**同一个业务执行核心**。
4. 预览/执行是否携带并校验来源村庄分配信息。
5. 正常返乡与 `WarRest`、`PartyDisbandment` 的不同授权入口如何隔离。
6. 本次识别并解决了哪些事务安全问题，哪些仍存在风险。
7. 每个适用检查的真实结果：`PASS` / `FAIL` / `BLOCKED`；不能虚报没有实际运行的游戏场景。
8. 编译日志、适用ReleaseValidation及Architecture Gate结果，新游戏存档读档证据、最终HEAD SHA。

**最终标准：一个村庄级操作永远只修改这个村庄的兵员和来源批次；一个封邑级操作可以统一协调多个合法附属村庄，但所有实际士兵、在役身份、返乡冷却和动员Hearth始终归其原村庄。**

---

## 附录：本轮依据的固定源码链接

- [FiefServiceBatch.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefServiceBatch.cs)
- [VillageFiefData.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [FiefPartyData.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [FiefPartyRecruitment.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [FiefPartyReturn.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [FiefPartyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [FiefServiceReconciliation.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs)
- [FiefServiceLedger.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Services/FiefServiceLedger.cs)
- [FiefReturnService.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Services/FiefReturnService.cs)
- [PartyExchangeModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs)
- [PartyExchangeService.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs)
- [UnifiedForceContracts.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/UnifiedForceContracts.cs)
- [FiefMenuBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs)
- [WarRestPlanningService.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/ForceManagement/Services/WarRestPlanningService.cs)
- [DisbandSettlementService.cs](https://github.com/GuihuaZhou/bannerlord/blob/e74f20a4d9ab65282c3f527b1bcc459cfab012b4/ModifiedArmy/ModifiedArmy/ForceManagement/Services/DisbandSettlementService.cs)
