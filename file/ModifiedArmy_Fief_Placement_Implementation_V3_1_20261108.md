# ModifiedArmy｜封建兵返乡后安置机制 V3.1 —— Codex 施工规范

> **基准**：`GuihuaZhou/bannerlord`，默认分支 `main`，核对提交 `950168e4f5b56d0916af563b1a854525d9bd778c`（2026-11-07 18:07 UTC）。
> **性质**：基于当前 Git 源码制定的实施规范；本文件不代表代码已经改动、编译或游戏内验证。
> **执行前要求**：先 `git fetch`，核对当前 `HEAD`；若相关文件已有新提交，以最新源码为准重新定位，不得机械覆盖。

> **V3.1 更新**：确定城镇/城堡多村庄安置优先级为**动态最大剩余合法封建供养额度**。已替换 V3.0 中按村庄 StringId 固定排序填充的建议。

## 0. 任务及不可变设计决定

**目标**：Party 在原有合法返乡/复员事务之后，允许把未被该 Party *任何来源*在役批次占用、且属于目标封邑文化模板的多余封建兵安置到目标村庄的 `ReturnBatches`，无论健康或受伤。玩家和受 Mod 管理的 AI 共用执行服务。

**硬性约束**：

1. **不新增按钮/菜单/独立安置选择窗口**。复用玩家既有“解散士兵/返乡”调用；AI 用原有复员、战时休整、正式整队解散路径。
2. **先在役对账，再现有返乡，再安置**。已有 `FiefServiceReconciler.Reconcile()` 是唯一在役身份判定机制，不得另起服务登记、按来源猜兵种、伪造 `FiefServiceBatch`。只在当前对账结果中增加“未匹配兵力”的明确快照。
3. **所有在役批次优先**：按 Party 全局在役台账，而非仅本次返乡目标村庄；在能够满足在役的士兵中优先健康，无法满足时才保留受伤者；不能用余兵安置偷走其他封邑的在役配额。保留现有升级树兼容关系与战损处理。
4. **安置接受健康/受伤士兵，两者全部进入 `ReturnBatches` 冷却**。不直接入 `Ready`。冷却使用目标封邑 doctrine 的 `Service.ReturnCooldownWeeks`。现有 `FiefReturnBatch` 不持久化伤情，约定冷却期内恢复，期满进入 `Ready` 作为健康兵；不用新增伤兵存档字段。
5. **文化/模板与供养必须满足**：依目标封邑的 `FiefPartyTemplate.IsEnableTroop()` 识别兵种（含升级树）；逐村复用 `VillageFiefData.CanIncreaseSupply(template,doctrine,troop,count)`，容量占用已经包括 `Ready + Serving + Cooling`。不单凭 `troop.Culture`、玩家文化或同王国判断。
6. **安置不是返乡**：不增加/扣减村庄 Hearth，不修改 `MobilizedHearth`、`ServiceBatches`，不释放在役台账；只从 Party 的 `MemberRoster` 扣兵并向目标村庄 `ReturnBatches` 加兵。
7. **严格事务与复验**：所有来源/目标、健康伤员明细、容量和在役保护在提交前检查；失败时回滚 Party roster + cooling，无法证实回滚时 `RequireRecovery` 冻结涉事村庄；绝不默默吞人或复制兵。
8. **城镇/城堡安置分配优先级**：每次优先选择当前**实际可用封建供养额度最大**的合法附属村庄，不再按村庄 `StringId` 固定顺序填满。额度按 `min(GetFreeSupplyHearth, GetFeudalBudgetRemainingHearth)` 计算，逐笔成功安置后重算并重新排序；额度相同时按村庄 `StringId` 升序打破并列。村庄操作仍仅允许当前村庄。
9. **不扩大无关功能**：不改基础招募、职业兵生成、升级树定义、返乡 Hearth 交易、保存类型 ID、AI 原寻路任务及经济文化制度配置；不复用 `RecruitFromPrisoners()`。

## 1. 当前源码事实（已在上面的 commit 核对）

| 路径（相对 `ModifiedArmy/ModifiedArmy/`） | 已有行为 / 应注意的问题 |
|---|---|
| `Fief/Models/FiefServiceReconciliation.cs` | `Reconcile()` 读取 Party 全部 `GetServingDetachments`，精确兵种匹配、`CalculateMaximumTreeMatches`，更改各批次及确认战损；`FiefReconciliationResult` 只有 Matched/Lost/Blocked/Reason。**`refs.Count == 0` 提前返回、目前不生成余兵。**目前精确匹配先于树匹配，且完全不看伤情。 |
| `Fief/Models/FiefPartyManager.cs` | `ReconcilePartyFiefTroops`（约784行）；`GetServingTroopCounts`（约913行）；手选 `ReturnSelectedFiefTroops`（约1434行）；批量 `ReturnTroopsToSettlement[WithResult]`（约1564行）；`TryResolveOperationScope`（约1298行）；`TryReturnFiefTroops`（约800行）。 |
| `Fief/Services/FiefReturnService.cs` | 每次批次返乡内部会先 `AdvanceServiceDays` + `ReconcilePartyFiefTroops`，验证真正来源、所在地、在役、owner、动员记录后执行原事务；**不得把安置塞进此每批次服务内部**。 |
| `Fief/Models/PartyData/FiefPartyReturn.cs` | 原有返乡扣除健康优先、伤员其次；变更 `ServiceBatches`、`MobilizedHearth`、Hearth，并把兵加入目标村 `ReturnBatches`；处理回滚。安置不能复用它的 Hearth 副作用。 |
| `Fief/Models/VillageFiefData.cs` | `GetReadyRoster` 返回村庄民兵 Party 的名册；`GetOccupiedSupplyHearth` 汇总 Ready/Serving/Cooling；`CanIncreaseSupply` 使用有效 Hearth 与文化军事制度的封建预算双约束；`ReturnBatches` 已 Saveable。 |
| `Fief/Models/Data/FiefReturnBatch.cs` | 只有 `Troops: Dictionary<CharacterObject,int>` 与 `WaitWeeks`；无伤情；`Tick()`/`IsReady()` 已实现。 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | `WeeklyUpdate()` 先升级 Ready，然后对每村 cooling `Tick()`，到期后添加为健康 Ready 兵并移除批次。**安置只需接入现有冷却，不加新的周事件**。 |
| `Fief/Models/FiefMenuBehavior.cs` | 现有 `fief_disband` 调用 `manager.ReturnTroopsToSettlement(interaction,playerParty)`（约371行）；`fief_manage` 的 Serving 选择结果在 `FiefManagementPresentationContext.Commit` 调 `ReturnSelectedFiefTroops`。不新增入口。 |
| `ForceManagement/Services/DemobilizationService.cs` | 和平复员 `TryExecuteAtSettlement`：执行已批准的批次返乡，然后 `TransferFieldToGarrison`；有 `ApprovedToday` 与实时安全超额，**安置应插在两者之间**。 |
| `ForceManagement/Behaviors/DemobilizationBehavior.cs` | `OnNaturalVisit` 只对合法 Mod AI 主体、已绑定的家族城堡/城镇、和平复员策略成立才调用；实际数量计入当日复员台账。 |
| `ForceManagement/Missions/PartyVisitExecutionService.cs` | 战时休整 `ExecuteWarRest` 创建 FiefReturn 计划，交给 `PartyExchangeRunner` 执行；返回数量在 `PartyExchangeService.ExecuteFiefReturn` 中计入 `WarRestPlanning.RecordCompleted`。不要重复计数。 |
| `ForceManagement/Services/WarRestPlanningService.cs` / `Models/WarRestPolicyModel.cs` | 战时休整有军力下限、当日 quota、真实来源批次和目标城镇；安置不可绕过这些审批。 |
| `ForceManagement/Services/DisbandSettlementService.cs` | 正式整队解散先返乡，再尝试保留在役身份转驻军、普通兵转驻军，最终结算损失；**安置应插在返乡与驻军转移之间**，从而减少无谓损失。 |
| `ForceManagement/Services/DisbandSettlementCoordinator.cs` | 执行后清空剩余 Party；`AcceptedTroops` 用于驻军捐兵影响力，**安置不应进入这个字典或获得捐兵影响力**；需单独增加 placed 统计与本地化详情。 |

仓库文件浏览入口：`https://github.com/GuihuaZhou/bannerlord/tree/950168e4f5b56d0916af563b1a854525d9bd778c/ModifiedArmy/ModifiedArmy/`。

## 2. 在役对账补充：输出可安置余兵，不重做已有账本

### 2.1 结果模型

在 `FiefReconciliationResult` 追加运行时只读信息（不 Saveable）：

```csharp
// 方案接口；实际命名可依项目风格调整。
public sealed class FiefReconciliationResult
{
    // MatchedCount / LostCount / BlockedByRecovery / ReasonCode 保持已有语义。
    public bool PlacementSnapshotValid { get; internal set; }
    public IReadOnlyDictionary<CharacterObject, FiefTroopCount> UnassignedTroops
        { get; internal set; }
}
```

`FiefTroopCount` 已有 `Total/Wounded/Healthy`，可复用（不强制再发明 DTO）。数据必须是**拷贝/只读快照**，不能暴露实时 MemberRoster 或可修改字典。可以加可选 `includeUnassigned=false` 参数到 manager/reconciler 的现有方法，减少其他每日对账调用的额外分配；安置入口使用 `true`。不改旧调用点语义。

### 2.2 生成时机与重要分支

- 必须在 `plans` 已成功写入在役批次、战损与动员回滚验证结束后才宣布快照有效。`ReconciliationPrecheckFailed`、`ReconciliationRolledBackVerified`、`ReconciliationRollbackUnverified` 等结果均 **不可用于安置**；**不能只依靠 `BlockedByRecovery == false`**，因为回滚已验证的失败仍可能返回 false。
- **`refs.Count == 0`**：先检查 Party/ledger 恢复阻断；如果确无在役批次，成功返回 `PlacementSnapshotValid=true` 且 `UnassignedTroops` 为全部合法非英雄成员，`ReasonCode` 可以新增 `NoServingBatches`。这是“仅有战后获救兵”的必要用例。
- 若现有在役集合不空，则使用**更新后**的所有 `plans[batch]`，按 CharacterObject 聚合在役数量；从 Party 实际 `(total,wounded)` 扣除占用，生成剩余 `(freeHealthy,freeWounded)`。不要仅用 `MatchedCount` 作总数扣减，因为不同兵种/升级树不能混算。
- 同兵种健康优先的基础公式：`reservedHealthy=min(healthy,servingCount)`；`reservedWounded=servingCount-reservedHealthy`；`freeHealthy=healthy-reservedHealthy`；`freeWounded=wounded-reservedWounded`。断言各值非负、保留数<=实际人数、`freeTotal+reservedTotal==actualTotal`。
- **升级树健康优先的边界**：当前源码在计算 `plans` 前先执行“精确兵种匹配”，不考虑伤情。例：登记 T2×1，实际 T2 伤员×1 + 合法升级 T3 健康×1，当前精确优先会匹配伤 T2，健康 T3 变为余兵。若“健康优先”要求跨升级树严格成立，则必须在**现有 Reconcile 内的匹配优先级**做小范围扩展：先保持最大可匹配在役人数，再使匹配中的健康人数最大化，再以原精确匹配/稳定排序为同分优先。禁止简单贪心“先健康再伤员”导致总匹配数下降；可使用现有 `CanMatchUpgradeTree` 的边，通过最大流 + 固定流量最小费用（或等效的增广路径交换）实现。**不能一边声称严格跨树健康优先，一边只做每 CharacterObject 的数量相减。**
- 对账仍只管理兵种人数/升级关系，不增加逐士兵身份、持久化伤情归属，不创建第二本服役账。若同兵种不同历史来源，只能按合并名册的数量保护在役份额，这是现有模型的客观边界。

### 2.3 复验与快照生效期

当前 `FiefReturnService.TryReturn` **每个批次**还会重入现有对账，而且 `TryCommitFiefReturn` 会扣 Party roster、删减服务批次。因此不要把“返乡前一次” `UnassignedTroops` 盲目当作永久有效。

**可靠的首版路径**：完成一轮原有返乡后，由安置业务入口调用 **同一个** `manager.ReconcilePartyFiefTroops(party, includeUnassigned:true)` 一次，拿到已确认的最新余兵快照，然后完成目标校验及事务。此举是复用既有对账，不是重新实现；它同时覆盖“原本无在役批次”的情况。如需消除多余调用，可在后续版本传递有效操作快照并以清晰版本号复验，不作为首版要求。

## 3. 目标村庄选择、兵种过滤、供养

### 3.1 统一合法范围

- Party 必须是活跃、真实的非敌对交战 Party，`MemberRoster` 非 null，`MapEvent==null`；首版执行地点是**Party 当前所在**定居点，不允许远程传送士兵。可在正式整队解散仍有真实 `CurrentSettlement` 时进入；无真实所在地点则跳过安置并走原有回退路径。
- 仅安置到 `interaction.OwnerClan == party.ActualClan` 的领地，且对应 `sourceFief.OwnerClan == party.ActualClan`。不同家族、仅同王国、战后新占领未确权地不允许安置；这与既有驻军捐兵的跨家族例外无关。
- `interaction.IsVillage`：目标只能是**当前村庄**，且必须仍附属实际源封邑。
- `interaction.IsFortification`：目标为该城镇/城堡当前合法辖属 `FiefPartyData.OrderedVillages(null)` 的一个或多个村庄；AI 和玩家使用相同分配规则。
- 用 `manager.TryResolveOperationScope(interaction,party,true,...)` 作为安全底座，另验证 `party.IsActive`、不是被围攻等、`data.IsRuntimeReady`、`GetFiefPartyTemplate()!=null`、目标 `VillageFiefData.RecoveryRequired==false`、`GetReadyRoster()!=null`；如果 `TryResolveOperationScope` 对特殊终结调用不适用，**不要放宽物理所在地约束**，直接跳过安置。

### 3.2 候选兵种

- 唯一来源是 `party.MemberRoster` 的未分配 **非英雄**士兵，不包含俘虏名册。
- 对每个 `CharacterObject` 用 **目标封邑**模板 `IsEnableTroop(troop)`，且 `doctrine.TryGetFeudalSupplyCost(troop,out cost)` 成功并且成本为正；文化应以封邑有效模板为准，不以士兵单独文化标记替代。
- 保留合法升级兵，不要求新兵生产的兵营等级；安置是接收既存兵力，不是创造高级兵。
- 不引用全局普通兵或雇佣兵配置来扩大合法模板。最低征召批次（如25人）不是安置最低人数：合法且有供养额度时1人也能安置。

### 3.3 容量分配

- 使用同一 `CulturalMilitaryDoctrine` 及 `VillageFiefData.CanIncreaseSupply(template,doctrine,troop,count)`；其内部检查 `GetFreeSupplyHearth` 与 `GetFeudalBudgetRemainingHearth`，已将 Cooling 算入占用。
- 一次安置涉及多个村、多个兵种，必须**逐笔重新计算真实容量**，不能对每个兵种分别读取初始余量后再相加产生超发。兵种可按 `StringId` 升序确定稳定处理顺序，但**村庄不再按 `StringId` 固定顺序填充**。
- **城镇/城堡多村庄分配规则（V3.1 新增、已确认）**：对当前待安置兵种，在所有合法附属村庄中计算 `effectiveRemainingHearth = min(GetFreeSupplyHearth(template, doctrine), GetFeudalBudgetRemainingHearth(template, doctrine))`，只考虑 `CanIncreaseSupply(template, doctrine, troop, 1)` 为真的村庄；选择 `effectiveRemainingHearth` 最大者。若相同，按村庄 `StringId` 升序决定，保证稳定和可重放。安置成功后**重新计算所有候选村庄的实际剩余额度，并再次选择最大者**，直至本兵种申请量已完成或所有村庄均无法供养。不得把只剩非封建可用 Hearth 但封建预算不足的村庄误判为最充裕。
- 为降低循环成本，可以在证明与“逐兵重排”等价的前提下进行批量分配；在证明前以逐名确定目标、按村聚合事务为准，特别注意不同兵种 `GetFeudalSupplyCost(troop)` 不同，不能简单按人数平均。
- **村庄现场操作不参与跨村排序**，目标始终为当前村庄；在城镇和城堡操作时不使用驻军作安置目标。
- 局部供养不足可**部分安置**，超出者保留 Party 并交给原有后续流程；不允许为了凑足请求临时增加 Hearth、扩大预算、抹掉已有冷却。

## 4. 安置服务与事务设计

新增内部 `Fief/Services/FiefTroopPlacementService.cs`，使用单一、清晰的入口，供玩家与 AI 复用。建议接口（示意，最终按项目类型定义）：

```csharp
internal enum FiefPlacementPurpose
{
    PlayerReturn,
    PeaceDemobilization,
    WarRest,
    PartyDisbandment
}

internal sealed class FiefPlacementResult
{
    public int PlacedCount { get; init; }
    public IReadOnlyDictionary<CharacterObject, int> PlacedByTroop { get; init; }
    public string ReasonCode { get; init; }
    public bool RecoveryRequired { get; init; }
}

internal static FiefPlacementResult TryPlaceAfterReturn(
    FiefPartyManager manager,
    MobileParty party,
    Settlement interaction,
    FiefPlacementPurpose purpose,
    int maximumCount = int.MaxValue);
```

流程：

1. 验证真实访问地点、同Clan所有权、Party/封邑运行时状态。
2. 调用 **已有** `AdvanceServiceDays` / `ReconcilePartyFiefTroops(...,includeUnassigned:true)`；若快照无效或身份冻结，**整体拒绝安置**，不尝试从 Party 随意扣兵。
3. 将快照过滤至目标 `FiefPartyTemplate.IsEnableTroop`，按健康/伤员份额得到申请量；对 AI 按其原业务的 `maximumCount` 与当前安全超额裁剪。
4. 计算可放入每个目标村的明细（包括健康/伤员数、cost、冷却周期）；建议 AI 优先安置伤员、再安置健康余兵，以尽量保留健康战力；玩家无需借此改变在役优先规则。
5. 提交前重查 Party 真实名册、当下全局在役占用、当前地点/归属、村庄绑定、模板和容量；如果名册/账本不等于预期，**拒绝并结束本轮**，不要递归重试或自动增加人数。
6. 为每个受影响兵种和冷却批次拍**完整前态**快照；扣除 Party 中批准的士兵，**明确扣除对应 wounded count**：`AddToCounts(troop,-count,false,-wounded,0,true,-1)`；校验减少的人数及伤员数。然后为目标村找/建 `WaitWeeks==doctrine.Service.ReturnCooldownWeeks` 的 `FiefReturnBatch`，调用 `AddTroops`；逐项校验每次 `cooling` 增量与预算。严格核算 `ΣParty减少 = ΣCooling增加`。
7. **不触碰**任何 Hearth 服务、在役台账、`TryReleaseMobilizationExact`、`TryMobilize`、`ServiceBatches`。安置成功刷新 `FiefPartyData.RefreshManpowerCounters()`，单独记录 placed 数量与原因。
8. 异常：在反向顺序撤销本次改动、恢复所有 roster 与所有 Cooling 批次（包含已存在与本次新建的批次），按 `CharacterObject` 及 wounded 数精确验证；不能证实恢复则 `village.RequireRecovery("PlacementRollbackUnverified",operationId)`，并返回 `RecoveryRequired`；绝不返回假成功。

**事务策略**：建议按*村庄*逐批提交、每个村庄内部跨兵种全成或全退；成功村庄累计保留，失败村庄回滚并停止本次，`PlacedCount` 精确表示**已承诺成功的**村庄数量，避免一个村失败导致跨多个村大范围回滚。若现有代码结构易于实现全请求原子提交，也可使用全原子，但结果必须严格准确。不得只以异常被捕获作为回滚成功的证据。

**伤员语义**：现有 `FiefReturnBatch` 无 `Wounded`，`FiefPartyData.WeeklyUpdate()` 期满入 Ready 时全部作为健康。设计明确：受伤安置兵在冷却中恢复，冷却结束即恢复健康。不得悄悄把伤员直接放进 Ready；不新增伤情存档结构。

**幂等性**：Party 实际扣兵 + Cooling 实际入账是权威事实；重复触发同一调用后再次对账/查看 Party，绝不可重复安置已经离开 Party 的同一份人数。首版无需新增持久化安置 ID 台账；已有访问信号的一天一次门控继续保留。

## 5. 逐入口接线（必须全部覆盖）

### 5.1 玩家：已有 `fief_disband`

`FiefMenuBehavior.DisbandFiefTroops` -> `FiefPartyManager.ReturnTroopsToSettlement` -> `FiefPartyData.ReturnTroopsToSettlement` -> 每批次 `TryReturnFiefTroops`。

**修改建议**：在 `FiefPartyManager.ReturnTroopsToSettlement` 的高级调用末尾、现有返乡完成之后调用 `TryPlaceAfterReturn(...PlayerReturn)`，**不是**在 `FiefReturnService.TryReturn`、`TryCommitFiefReturn` 每批次内部。`ReturnTroopsToSettlementWithResult` 同样需要保证覆盖（可让两个包装器委托单一私有高阶方法），保留原返回值为**实际返乡人数**，不要暗改为返乡+安置，必要时另返回复合结果。

注意：即使 `FiefPartyData.ReturnTroopsToSettlement` 因“没有在役批次”返回0，只要玩家合法触发原解散入口，依然要执行安置；不能以 `returnedCount>0` 作为安置门槛。

### 5.2 玩家：现有管理界面选中“Serving”后返乡

`FiefManagementPresentationContext.Commit(State==Serving)` -> `FiefPartyManager.ReturnSelectedFiefTroops`。

建议在这个**高阶方法**结束整轮选中批次返乡后调用一次安置，沿用当前 `Interaction/Village`：玩家在村庄仅该村，在城堡/城镇可分配其辖村。不改变已有选择 UI、不将额外兵力添加至 Serving 的可选名册。**无效选择、指纹失效或非法地点**都不能触发安置。不要按选中人数把额外兵力错误当作已登记返乡兵。

### 5.3 AI：和平复员

`DemobilizationBehavior.OnNaturalVisit` -> `DemobilizationService.TryExecuteAtSettlement`。

当前实现：`remaining=ApprovedToday` -> 循环逐批返乡 -> 计算 `liveSurplus` -> `TransferFieldToGarrison`。

改为：循环逐批返乡 -> **新增安置** -> 原普通兵驻军移交。安置必须同时不超过 `remaining`、返乡后的实时 `liveSurplus`、合法封建兵余量、村庄供养量。安置后再计算当前真实 `liveSurplus`，把当日 `remaining` 减去实际 `PlacedCount`，剩余额度才可继续移交驻军。

`DemobilizationExecutionResult` 添加 `PlacedFiefCount` 并将其计入 `ActualCount`，这样 `DemobilizationBehavior` 原有 `_partyDemobCountToday` 精确把三项（returned + placed + garrison-transferred）计入当日限额。别把安置数混进 `ReturnedFiefCount`。守住和平稳定天数、当前 Scenario、Party Army、绑定据点等现有决策条件。

### 5.4 AI：战时休整

`PartyVisitExecutionService.ExecuteWarRest` -> `PartyExchangeService.Execute(FiefReturn)` -> `PartyExchangeService.ExecuteFiefReturn` -> `WarRestPlanning.RecordCompleted`。

在**高阶的 `ExecuteWarRest`** 中，只在合法返乡提交成功后执行 `TryPlaceAfterReturn(...WarRest)`；不要在 PartyExchangeService 的通用单批次 transaction 里无条件追加安置。安置目标为此 `task.TargetSettlement` 的**家族合法辖村**，不是任一友军村庄。

安置数量由**返乡后重新计算的军事安全超额**与**当天剩余战时休整配额**共同限制。当前 `WarRestPlanning.RecordCompleted` 已记入原返乡人数，新增安置实际人数需另外记入一次（暴露一个只读的剩余 quota 查询接口或等效安全封装），不得重复记入原返乡人数。最终 Party 不得低于 WarRest 安全兵力下限。现有 `PartyExchangeExecutionResult.TransferredCount` 继续表示返乡事务量；安置采用额外 `PlacedCount` 的组合结果/日志，不把其伪装成已批准的 `FiefReturn` manifest。

**不加独立 WarRest 任务**；没有获批返乡时本路径也不擅自安置，保持原审批模型。

### 5.5 AI：正式整队解散

`DisbandSettlementService.SettleDisbandingParty` 当前顺序：全部可返乡在役批次 -> 试图将仍在役兵连同身份转入合法驻军 -> 移交普通兵 -> 结算损失。

在全部合法返乡后、任何驻军转移前新增 `TryPlaceAfterReturn(...PartyDisbandment)`；由于此 Party 即将解散，不受和平/战时每日上限，但仍严格受真实所在地、同Clan目标、所有在役身份保护、模板、供养限制。没有合法目标时保持旧的驻军/损失回退，不得从远方空投兵力。安置成功后，驻军转移逻辑读取实时剩余名册即可。

`DisbandSettlementResult` 新增 `FiefPlaced`；`DisbandSettlementCoordinator.BuildResultDetails` 增加对应本地化记录。**不得将安置兵算入 `AcceptedTroops`，该字典参与驻军捐兵影响力结算**；也不得把它计算成 `OrdinaryForcedLost`。完成后校验：返乡、安置、驻军接收、真实损失和剩余被清理兵力的总人数守恒。

## 6. 安置规则用例（必须做测试/断言）

| # | 场景 | 必须结果 |
|---|---|---|
| A | 同模板40名获救兵，无任何在役批次，供养>=40 | 对账产生40个候选；原返乡0；安置40冷却；Party -40；Hearth/Mobilized不变 |
| B | 同兵种70人（健康45、伤25），全部来源批次合计在役30，目标供养>=40 | 在役优先保留30健康；剩余健康15、伤25均可安置，累计40 |
| C | 在役30（健康仅20），实际70（伤50） | 在役保留健康20+伤10；额外伤40可安置，不会将登记伤员错误归入候选 |
| D | 登记T2×1，实际伤T2×1+健康合法升级T3×1 | 若严格健康优先，应该用健康T3满足在役，伤T2成为额外；在役匹配总数不得下降 |
| E | 不同来源村庄同时存在在役：A 20、B 20；当前到村庄C | 必须优先保护A+B共40，不能只保护C来源的在役 |
| F | 目标文化相同但士兵不在 `IsEnableTroop` 升级树 | 不安置，留原 Party |
| G | 供养预算仅容纳25/40，分兵成本可能不同 | 合法部分安置，绝不超 `CanIncreaseSupply`，其余留 Party |
| G1 | 城镇/城堡下辖A/B/C村，初始可用供养分别为15/10/30；40名同成本合法兵 | 每次优先选当前可用供养最大村庄并重排，不能按A/B/C固定顺序填满；最终合计安置40且各村不超额 |
| G2 | 当前两村可用封建供养均为20，兵种成本不同且村庄ID不同 | 先用村庄ID升序打破并列，每次成功安置后动态重算；不能仅按 FreeSupplyHearth 排序而忽略 FeudalBudgetRemainingHearth |
| G3 | Party在村庄A，附属村庄B供养远高于A | 只能安置到A；不可跨村分配 |
| H | 包含健康与受伤已安置兵 | 两者进入 Cooling，未立即 Ready；周 Tick 到期后全部健康入 Ready，人数不变 |
| I | `ReturnBatches` 已有相同 `WaitWeeks` 项 | 合并兵种数量，事务失败还原原批次准确前态 |
| J | 安置时突发兵种、伤员数、所有权、绑定或供养变化 | 提交前拒绝/安全回滚；不吞兵、不复制兵、不损坏在役台账 |
| K | 玩家在城镇解散、玩家在村庄解散、Serving 选择返乡 | 无新增 UI；各入口仅执行一次安置，位置范围正确；没有在役也能走解散入口安置 |
| L | AI和平复员，返乡10、允许余兵再复员15 | 安置<=15，然后驻军补剩余额度；当日累计包含安置数；低于目标不可再安置 |
| M | AI战时休整，返乡之后军力下限仅余8名可放 | 安置<=8且<=当日剩余额度，`RecordCompleted` 不双记；未获批休整不安置 |
| N | AI正式解散，合法放入Cooling 20、余下驻军接纳5，其他无法接纳 | 20列入Placed、不领取驻军捐兵影响力；驻军5正常结算；后续真实损失准确 |
| O | 保存/重载、周 Tick、定居点易主、村庄民兵重建与RecoveryRequired | Cooling持久化正确，无法恢复时冻结；无额外 Saveable 字段、无隐性人口/Hearth副作用 |
| P | 同一天不同事件重复访问/重复安置调用 | Party 已扣去的兵不能再次被同一调用添加到 Cooling；计数无重复 |

另外要覆盖**一组“原有返乡功能不退化”**：正常整批、部分返乡，经过升级树转换、带伤和冷却归队后的 Hearth 守恒；封邑服务工资豁免和 `FreeWageDays` 不受新安置影响。

## 7. 交付顺序与验证门禁

**阶段一：对账快照** —— 修改现有 `FiefServiceReconciliation.cs` 与 manager wrapper；通过含无在役、跨村在役、混合健康/伤员、升级树的纯逻辑用例。严禁先做 UI 或 AI Hook。

**阶段二：安置事务** —— 新建单一 service，复用 cooling/供养，不动 Hearth；城镇/城堡按**每轮剩余合法供养额度最大**的附属村庄动态选择并稳定处理并列，村庄现场严格限本村。验证事务守恒、动态分配、部分容量和回滚，再检查周 Tick。

**阶段三：玩家接入** —— 把 `ReturnTroopsToSettlement`、`ReturnTroopsToSettlementWithResult`、`ReturnSelectedFiefTroops` 的成功/0返乡/非法分支全覆盖，禁止重复执行。

**阶段四：AI接入** —— 和平复员、战时休整、正式整队解散，分别接线，核准配额/安全地板/日志/影响力。

**阶段五：全项目检查** —— `ModifiedArmy` Release 编译，关联 `ModifiedPolitics` Release 编译（如果构建链要求），现有架构/依赖门禁；增加可运行的单元/集成测试或给出等效可复现脚本；游戏内至少跨越 WeeklyTick 并实际保存重载。仓库若缺测试项目，可以建立独立测试适配，而不是声称编译即等于全部验证。

**最终交付必须报告**：

1. 本次实际采用的 HEAD SHA、完整修改文件清单、主要接口/调用图；
2. 哪些原有逻辑复用、哪些最小差异需要修改（尤其升级树健康优先）；
3. 各入口实际触发条件与幂等防护；
4. 编译、静态门禁、测试与游戏内验证的 **PASS / FAIL / BLOCKED**；
5. 任何未完成的测试与真实风险，不得宣称已在游戏内验证。

## 8. 给 Codex 的执行指令（可直接复制）

> 请在仓库 `GuihuaZhou/bannerlord` 的最新 `main`（或我本地现有工作分支）实现 `ModifiedArmy` 的“返乡后封建兵安置”功能，严格遵守本文件 V3.1 的设计与验收规则。先用代码搜索确认所有实际入口及 `HEAD`，以代码而非旧设计文档为准；保留已有 `FiefServiceReconciler`、封邑服务批次、Hearth 人口事务及 `FiefReturnBatch` 保存结构。扩展对账**结果**支持可安置余兵并满足健康优先；只在高阶返乡/复员流程完成后触发安置，统一复用一个事务服务。玩家不加按钮，AI支持和平复员、战时休整、正式解散。健康、伤员均进原有 Cooling；容量严格按模板与供养；城镇/城堡多村庄安置按每次实际剩余封建供养额度最大动态选择（并列按村庄 StringId 升序），不得固定填满第一个村庄；所有在役批次必须优先保护。先做对账与事务，再接 UI/AI，最后编译、测试、架构门禁并说明游戏内验证状态。未经我明确要求不要提交远程 PR。
