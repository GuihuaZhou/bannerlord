# ModifiedArmy — FiefParty 村庄化重构收尾修复方案 V1.2

> **交付对象：** AI Worker / Codex（实施指令）  
> **目标仓库：** `GuihuaZhou/bannerlord`  
> **审查基准：** `e912eb4e4822be93e9eeb14ad23decdda9349428`  
> **对比上次提交：** `e74f20a4d9ab65282c3f527b1bcc459cfab012b4`  
> **任务性质：** 针对已实现的村庄化重构进行最小范围一致性修复；不是重新设计、更不是重新开发。  
> **状态：** 修改方案，未对仓库提交代码；未执行游戏测试。

## 0. 用户已确认的设计边界（必须遵守）

1. **不需要历史存档兼容或存档迁移。** 可以调整新游戏使用的持久化类型与属性，但新游戏保存、载入必须正确。
2. **不处理村庄重新绑定到另一个城镇/城堡。** 正常原版中 `Village.Bound` 是固定的封邑关系；贸易关联 `TradeBound` 另当别论。城堡/城镇易主仍可能发生，但**不等于村庄改绑**。
3. 城镇/城堡统一征召及解散其全部合法附属村庄封建兵；村庄可以单独征召、解散本村封建兵。
4. `VillageFiefData` 持有村庄生命周期权威状态（实际就绪Roster仍为原版村庄民兵Roster）；`FiefPartyData` 提供封邑级协调；`FiefPartyManager` 提供外部入口和全局Party服役索引。
5. 每个 `FiefServiceBatch` 明确保存不可变的 `SourceFief` 与 `SourceVillage`，转入别的野战Party时原籍和服役天数不变。
6. 本次**不改**兵源生产公式、文化军事制度、税赋、职业兵转化、等级供养成本、每日动员Hearth消耗量、招募评分和AI预算权重。
7. 不为不存在的村庄改绑风险增加字典迁移、原籍再分配、历史封邑兼容系统。不要扩大Harmony补丁范围。

### 本次验收目标

- 修复**征召异常时的逐项回滚与验证**；回滚无法完整完成时，不悄悄丢兵、重复兵或清除恢复标记。
- 修复**存在异常批次的野战Party对账**，杜绝把异常来源士兵错误算入其他村庄。
- 为 `RecoveryRequired` 建立**受控、可审计的恢复流程**；没有证据时保持冻结，不“人数相等就自动解锁”。
- 统一村庄/封邑查询语义；保留已上线的双入口和来源追踪。
- 编译并以新游戏执行完整验收。

---

## 1. 当前代码已正确完成的部分：不要推倒重做

以下是 `e912eb4e` 已确认的实际实现，应保留：

| 路径 | 已实现 | 处理方式 |
|---|---|---|
| `Fief/Models/VillageFiefData.cs` | `VillageSettlement`、`ServiceBatches`、`ReturnBatches`、`MobilizedHearth`、`RecoveryRequired` | 保留数据所有权 |
| `Fief/Models/Data/FiefServiceBatch.cs` | `SourceFief`（SaveableProperty 7）、`SourceVillage`（8）、`SplitTo` 继承原籍 | 不改已确定的批次来源规则 |
| `Fief/Models/FiefOperationScope.cs` | `InteractionSettlement` / `SourceFief` / `VillageFilter` | 继续统一入口路由 |
| `Fief/Models/FiefPartyManager.cs` | `TryResolveOperationScope`、村庄/封邑征召、解散路由、在役索引 | 保持公共API |
| `Fief/Services/FiefServiceLedger.cs` | 按原籍创建 `ServingDetachmentRef`、检测重复BatchId和来源不符 | 扩展异常Party保护，不要拆成每村独立对账 |
| `Fief/Models/FiefServiceReconciliation.cs` | 野战Party统一匹配相同CharacterObject、升级树匹配、按批次分配战损 | 保留匹配算法，只改校验/提交边界 |
| `Fief/Models/FiefMenuBehavior.cs` | 城镇/城堡/村庄菜单，村庄选择范围与指纹检查 | 保持现有交互 |
| `ForceManagement/Models/PartyExchangeModel.cs` + `Services/PartyExchangeService.cs` | 双入口纯计划、来源指纹和执行复核 | 保留模型/执行器拆分 |

**不得为“修复”引入新的封邑级可写封建兵Roster、复制的在役批次表，或在城堡驻军中储存同一封建兵。**

## 2. P1：征召事务回滚必须具备完整补偿和失败可见性

### 2.1 代码定位

`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs`

- `TryCommitVillageRecruitment(...)`：基准约第819—944行。
- 当前计划先从各村庄真实 `MemberRoster` 减兵，然后 `TryMobilize()` 扣Hearth并建立 `FiefServiceBatch`，最后写入野战Party。
- 当前 `catch` 依次对野战Party、村庄Roster、批次、Hearth回滚，**但补偿操作本身未逐项隔离**；补偿时发生异常可能阻断剩余修复。
- 当前 `created` 保存Batch对象，再通过 `plan.First(p => p.Village.ServiceBatches.Contains(batch))` 反查所有者，异常状态下这项反查自身可能失败。
- 当前村庄Roster不可用时仅 `RequireRecovery()`，无法凭空恢复士兵；这不是“回滚成功”。

**具体危险：** A村、B村都已经扣兵，中途异常；如果A村恢复名册时再抛异常，后面B村恢复、Hearth返还和批次撤销不一定执行。直接返回 `false` 可能掩盖内部账目不一致。

### 2.2 修改目标

在保留 `RecruitTroopsToParty()`、`RecruitManualSelection()` 和 `TryResolveOperationScope()` 对外语义的前提下，改造该方法或抽取 `FiefRecruitmentTransactionService`：

1. **准备阶段（只读）：** 校验来源范围、来源村庄 `RecoveryRequired == false`、Roster可用性、健康/伤员数量、目标Party空位、模板资格、每村Hearth下限和批准Manifest；生成 `VillageRecruitmentSource` 列表。
2. **捕获资源快照：** 对**触及的 CharacterObject**，分别记录各村、目标Party的总数与伤员数；记录各村 `MobilizedHearth`、实际Hearth，以及提交前对应批次集合/索引的身份。临时引用必须和目标村庄绑定，不要用 `plan.First(...)` 回查。
3. **逐步提交：** 每个成功的状态变更立即记录一条带明确来源对象的补偿步骤（如 `VillageRosterDelta`、`HearthDelta`、`ServiceBatchAdded`、`PartyRosterDelta`）。注意：如果底层Roster写入可能先修改后抛错，应在调用异常后用快照对比核实，不能只依赖“调用返回后才记录”。
4. **提交完成后复核：** 村庄实际减少、Party实际增加、在役批次增加和动员Hearth减少，数量必须相等；保持相同兵种和健康/伤员分布一致。
5. **异常补偿：** 按逆序执行所有补偿；**每步独立捕获异常，记录失败，不得中止后续补偿**；所有补偿结束后再次对比提交前资源快照。
6. **仅在复核完全一致时**返回普通失败（例如 `RolledBack`）。如果任一步无法恢复或无法核实，则对受影响的 **所有相关来源村庄** 调用 `RequireRecovery()`，输出包含操作ID、村庄、Party、兵种、预期/实际数量及失败步骤的结构化日志；不得记录为“成功回滚”。
7. `committed` 只在完整提交且校验成功后赋值。AI `ClanRecruitmentBudgetManager.CommitRecruitment()` 的调用仍应位于完全提交之后；不要发生失败事务也提交预算。
8. 交易失败不修改原有生产/征召概率，也不把已有就绪士兵转移到别的村庄补偿。

### 2.3 建议的数据结构（仅为实现参考）

```csharp
internal sealed class VillageRecruitmentCommit
{
    public VillageFiefData SourceVillage { get; init; }
    public CharacterObject Troop { get; init; }
    public int Count { get; init; }
    public int Wounded { get; init; }
    public TroopRoster SourceRosterAtPrepare { get; init; }
}

internal sealed class RecruitmentCompensationStep
{
    public string Stage { get; init; }
    public VillageFiefData Village { get; init; }
    public FiefServiceBatch Batch { get; init; }
    public Action Compensate { get; init; }
}
```

运行时补偿记录不是第二份持久化兵员库存。若要支持跨游戏崩溃恢复，应另作专题，不得本次随意声称内存事务能够提供磁盘原子性。

### 2.4 单项提交与回滚的细节

- **村庄Roster**：提交前记录具体Roster对象；异常时验证同一个Party/名册是否仍合法。若Roster已经消失，不可将“跳过”算成补偿成功；标记需恢复，记录确切缺口。
- **野战Party Roster**：与村庄一样记录修改前后总数与伤员数；不能假设移除/添加永不抛异常。
- **Hearth**：`VillageFiefData.TryMobilize` 与 `RollbackMobilizationReservation` 是已有入口，改造时直接使用；回滚后同时比较实际 `Village.Hearth`、`MobilizedHearth` 与快照。
- **ServiceBatch**：创建与索引注册要么全部成功，要么完全撤销。对`CreateServingDetachment()`已做的局部补偿保留，再由外层事务审计确认。
- **源村标记**：即使事务只在B村失败、A村补偿完成，也要在全部提交涉及资源核验后决定A村能否解锁；不应误把损坏传播到无关村庄。
- **返回结果**：如既有上层只有 `int`，保留签名，但应有内部原因码/日志区分`NothingCommitted`、`RolledBackVerified`、`RecoveryRequired`。

**不要**简单在 `catch` 中继续吞掉异常并返回0；这不能证明数据恢复完成。

---

## 3. P1：异常村庄在同一野战Party中的对账保护

### 3.1 代码定位

`ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs`

`Reconcile(...)` 现在先过滤：

```csharp
.Where(r => r.Detachment != null && !r.Detachment.IsEmpty() &&
    manager.GetVillageFiefData(r.SourceVillage)?.RecoveryRequired != true)
```

再读取**整支Party** `MemberRoster`、计算存活与升级、回写非异常批次。

**问题：** A村和B村各在同Party有10名相同兵种，实际只剩15人。A村被标记Recovery后，从匹配集合排除A村，但实际Party的15人仍全部可匹配B村。B村可能被判定10人完整存活，而A村仍冻结为10人；总登记20人，实际15人。在没有逐兵来源身份的情况下，这样的结果不能视为“其余村庄已安全对账”。

### 3.2 采用最小且可靠的规则

**按整支野战Party冻结其封建兵在役对账**，而不是试图在未知来源的相同兵种之间重新分摊。

在任何匹配、批次修改、Hearth释放之前：

1. 获得此Party的**全部** `ServingDetachmentRef`，不能先过滤Recovery的来源。
2. 逐项验证：`SourceVillage` 有合法 `VillageFiefData`；`BatchId` 非空且唯一；`FiefServiceBatch.SourceFief / SourceVillage` 与其权威持有者一致；此批次确实归当前Party；关联村庄没有 `RecoveryRequired`。
3. 若存在一项异常：返回一个能表达 `BlockedByRecovery` 的结果，不更新该Party下**任何村庄**的在役兵种、伤亡、动员Hearth或索引。添加日志，说明堵塞原因与所有来源村庄。
4. 若全部合法，沿用原有精确兵种匹配、升级树匹配和配额分摊算法。

**注意重新加载索引的异常：** `FiefServiceLedger.Rebuild()` 当前遇到来源不符或重复 `BatchId` 时会 `RequireRecovery()` 并跳过该批次的注册。如果该批次对应的Party仍可定位，必须使该Party也进入“对账禁止”集合；否则仅查看已注册索引时可能漏掉异常批次。建议维护一个**运行时** `HashSet<MobileParty>` 或等效按Party标记的阻塞状态，并在重建/复核时计算；不要复制批次库存。

### 3.3 对账提交仍需保证一致性

目前 `Reconcile()` 对每个批次执行 `ReplaceTroops(plan)`，然后 `ReleaseVillageFiefService(...)`；后者失败时恢复当批次，却不会自动回滚此前已经提交的其他批次。建议：

- **先规划**各批次的更新结果及所有村庄累计战损。
- **集中预检**每村 `MobilizedHearth >= 该村计划结算损失`、所有批次和Roster状态未变且没有Recovery。
- **再执行提交**。若要保证整Party原子结算，提交前保存所有要修改的批次与Hearth快照，任一失败时逐项补偿、全部重新核验，未恢复则把关联村庄标记Recovery并禁止后续结算。
- 不得先删空批次或索引，再判断Hearth是否释放成功。

这不是重写匹配算法，仅给匹配结果增加安全的统一提交边界。

### 3.4 与Party转移和销毁的一致性

同时检查：

- `FiefPartyManager.TransferFiefService(...)`：如果来源村庄Recovery或其身份不合法，**不能**先让调用方从野战Party转走兵员、再在这里 `continue` 跳过服役批次转移；必须在真实Roster移动前做好可转移身份/数量预校验，或失败时整体回滚Roster移动。尤其检查 `DisbandSettlementService` 内 `TransferRoster(...)` 之后再 `manager.TransferFiefService(...)` 的路径。
- `SettleRemainingFiefServiceAsLoss(...)` 与 `OnPartyRemoved(...)`：保留已加入的Hearth释放结果检查。失败时保留未结算记录与恢复标记，不伪造“损失已成功结算”。Party已销毁且不存在真实Roster时，不能用空Roster盲目对账，应进入明确的异常结算流程。
- 自动工资豁免读取批次记录时，也不能把来源异常记录默默当成已经正常结算的士兵；需定义并记录其只读处理策略。

---

## 4. P1/P2：RecoveryRequired 的受控恢复闭环

### 4.1 当前已知状态

`ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`：保存 `[SaveableProperty(5)] RecoveryRequired`，只提供 `RequireRecovery()`；上一版每天在“动员值==在役数量”时自动解除的逻辑已删除，这一点应保留。

`FiefPartyManager.ReconcileMobilization()` 目前只发现不等时标记并记录日志，不执行修复。

### 4.2 明确“需要恢复”的含义

区分三类，不要混为一谈：

1. `TransientOperationFailed`：一次修改失败，但所有资源已经补偿至原状。**不应进入长期锁定**，记录普通失败即可。
2. `UnverifiedRollback`：某步骤无法确认或恢复实际状态，包含原村庄Roster失效、目标Party失败补偿、Hearth不一致。**保持RecoveryRequired**。
3. `PersistentInvariantMismatch`：新存档加载后批次来源错误/重复、Party索引不完整、实际动员Hearth和在役账面不一致。**保持RecoveryRequired**。

### 4.3 最小恢复API

建议在 `FiefPartyManager` 下增设私有/内部 `ValidateVillageFiefState(...)` 和 `TryCompleteVillageRecovery(...)`，其中后者必须显式调用，不能由普通每日Tick在计数刚好相等时自动执行。

`ValidateVillageFiefState` 至少检查：

- VillageData和当前固定 `Village.Bound` 的对应关系；
- `SourceVillage`、`SourceFief`、权威持有村庄和批次归属一致；
- 全局 `BatchId` 唯一（含新建批次）；
- `ServingParty`/`ServingPartyId` 能解析并符合当前Party信息；
- 各批次兵种总数、伤员/普通Roster等合理范围（不凭空认定所有在役数都应精确等于单个Party物理Roster）；
- 本村 `MobilizedHearth == 本村有效在役总数`（当前每兵1户规则）；
- 冷却批次和就绪Roster没有事务遗留的重复提交；
- 任何与本村批次共享Party的其他来源是否处于冻结状态；
- 重要：对于已记录的 `UnverifiedRollback`，还须有原始操作补偿证据，不能仅凭“计数相等”解锁。

### 4.4 恢复操作策略

- 对于**完整、可证明的事务补偿**：核对前后快照一致后恢复正常，不需要长期锁定。
- 对于**来源错误、重复批次ID、Party丢失或没有可追溯原始Roster的错误**：不要擅自删兵、补兵、平均分摊死亡、恢复Hearth或清空批次。输出诊断报告并保持冻结，交由明确的修复路径/人工检查。
- 如实现可自动修复的特例（例如运行时索引丢失、权威保存批次完整且Party仍存在），允许从唯一权威批次重建索引，再完整校验；成功后显式清除锁定，记录理由。不能直接从数值差异推导“正确值”。
- 若需要在新游戏保存/加载后保留具体故障原因，可在 `VillageFiefData` 新增 `RecoveryReason`、`RecoveryOperationId` 等简短的**诊断状态**（分配未占用SaveableProperty编号）；不要复制完整Roster作为永久账本。
- 恢复时需重建/刷新相关Party的运行时服役索引，之后才能允许征召、返乡、转移和对账。

### 4.5 阻断范围

- **村庄级写入**：RecoveryRequired村庄不能参与生产、升级、征召、返乡、冷却完成后写回名册等危险变更，除非该操作属于经过验证的恢复步骤。
- **野战Party的批次匹配**：任何来源不明或Recovery参与的Party，整体阻断其封建兵对账；其他不相关Party正常工作。
- **全局流程**：不需要让一个村庄错误冻结所有封邑或所有AI。异常隔离到受影响的村庄及共享在役Party。
- **只读展示**：仍可显示数据，但应有明确“待恢复/不可操作”诊断，不把冻结数据当成可招募库存。

---

## 5. P2：村庄与封邑的只读查询接口保持一致

`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`

当前 `GetAvailableTroopCount(village)`、`GetRecruitedTroopCount(village)`、`GetWaitCycleTroopCount(village)` 已能读取本村数据；但 `GetFiefTroopCount(village)` 只从 `_fiefDataMap` 查城镇/城堡，村庄输入返回0。

**要求：**

- `GetFiefTroopCount(village)`：返回该村 `VillageFiefData.GetTotalFiefTroopCount(fiefTemplate)`；城镇/城堡输入继续返回所属封邑全部村庄总数。
- `GetFiefTroopLimit(village)`：不要制造“独立村庄上限”。若调用方需要统一查询，允许映射到其城镇/城堡的**共享总上限**，在API注释中说明该值不是村庄单独容量；若现有调用方必须把此API限定于封邑，则显式限制并提供分开的 `GetFiefSharedLimitForVillage(...)`。Worker先检索实际调用点选择兼容性最好的方法。
- 村庄显示就绪、在役、冷却、总数应满足 `总数 = 就绪 + 在役 + 冷却`；村庄UI不能显示城堡三村的总数冒充本村数。
- `GetFiefTroopRoster(village)` 保持只读快照，不能直接修改真实Roster。

这是小范围接口一致性修复，不设计新经济规则。

---

## 6. 建议逐文件清单

| 文件 | 具体改动 | 优先级 |
|---|---|---|
| `Fief/Models/PartyData/FiefPartyRecruitment.cs` | 准备-提交-补偿-复核；失败类型与详细诊断；保留来源村庄事务范围 | P1 |
| `Fief/Models/PartyData/FiefPartyData.cs` | `CreateServingDetachment` 与外层事务协同；批次创建/注册在失败时完全撤销 | P1 |
| `Fief/Models/VillageFiefData.cs` | 村庄冻结、恢复原因/审计状态（必要时）、受控校验接口；保留权威Hearth | P1 |
| `Fief/Models/FiefServiceReconciliation.cs` | Party级冻结，来源校验，先规划再统一结算；不要重写升级树匹配 | P1 |
| `Fief/Services/FiefServiceLedger.cs` | 索引重建异常时将有关Party登记为blocked；已有重复BatchId检测保留 | P1 |
| `Fief/Models/FiefPartyManager.cs` | Recovery生命周期、统一Party状态、转移/销毁结算预检；村庄总数查询 | P1/P2 |
| `ForceManagement/Services/DisbandSettlementService.cs` | 真实Roster转移前确保服役身份可以一起转移；失败可回滚 | P1 |
| `ForceManagement/Services/PartyExchangeService.cs` | 复核失败原因、确保成功交易后才提交上层后续副作用 | P1 |
| `Fief/Models/PartyData/FiefPartyReturn.cs` | 核实返乡`catch`补偿完整性；失败时保持批次与Hearth一致，锁定不完整情况 | P2 |
| `Fief/Services/FiefReturnService.cs` | 在来源/Party恢复状态异常时拒绝危险返乡并给出原因码 | P2 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | Recovery村庄不参与生产、升级及冷却回库；不影响健康村庄 | P2 |
| `Fief/Models/FiefMenuBehavior.cs` | 只需反映禁止操作/诊断，不改变双入口UI结构 | P2 |

按实际架构规范决定是否抽 `FiefRecruitmentTransactionService` 或 `FiefRecoveryService`；**为这一问题不应拆出大量没有业务价值的接口/模板**。

---

## 7. Worker实施步骤（严格顺序）

1. 读取仓库根及模块 `AGENTS.md`、代码组织规范；确认HEAD及工作区状态；搜索现有测试设施，不覆盖其他修改。
2. 建立或补充针对 `VillageFiefData`、`FiefServiceBatch`、`FiefServiceLedger` 的不变量断言/测试辅助方法。
3. 修复征召事务及逐项补偿，先覆盖成功、普通失败、回滚失败三个路径；不要改变权重与AI策略。
4. 修复Party级对账冻结及统一结算提交；处理索引重建遇到无效批次却不能标记Party的问题。
5. 在对账冻结策略稳定后，修改 `TransferFiefService` 与所有直接先改Roster后转移身份的调用方。
6. 完善 `RecoveryRequired` 原因、诊断、验证和显式恢复方式；不能无证据自动纠正Hearth/士兵数量。
7. 检查返乡、冷却、生产、自动升级遇到冻结村庄的路径，避免部分业务继续写入。
8. 统一 `GetFiefTroopCount(village)` 等村庄查询语义。
9. 编译受影响项目，执行可用架构/静态检查，进行新游戏保存载入、征召解散、战损、异常注入的验证。
10. 输出改动文件、编译日志、测试结果和未覆盖风险；未得到用户指令不要自动提交、推送或开PR。

---

## 8. 验收用例（必须报告 PASS / FAIL / BLOCKED）

| ID | 场景 | 不可违反的结果 |
|---|---|---|
| R01 | A村正常单独征召10人 | 仅A村减员/扣10Hearth；Party与批次各增10 |
| R02 | 城堡统一征召A/B两村共25人 | 两村各自记录来源；合计人数和Hearth守恒 |
| R03 | A村在提交前Hearth不足 | 0招募，所有Roster/批次/Hearth不变 |
| R04 | 中途某个Roster写入失败，补偿全部成功 | 返回失败且状态等于操作前；不留下RecoveryRequired |
| R05 | 中途失败，补偿时村庄Roster不可用 | 明确RecoveryRequired，缺口日志，禁止后续危险操作 |
| R06 | Batch创建/索引注册阶段注入失败 | 没有孤儿批次、孤儿索引；不能丢兵或虚扣Hearth |
| R07 | A/B各提供10个同兵种，实际剩15 | 全Party匹配后总存活15，损失5只结算一次 |
| R08 | 同Party A村RecoveryRequired，B村正常 | **整个Party**封建兵对账不写任何批次或Hearth |
| R09 | A村RecoveryRequired，另一个完全无关Party | 另一个Party正常对账，不被全局冻结 |
| R10 | 加载后BatchId重复或SourceVillage不符 | 记录明确异常，涉及Party被阻断；不静默跳过后继续匹配 |
| R11 | 几项计数偶然相等但补偿未证实 | RecoveryRequired保持，不因每日Tick清除 |
| R12 | 运行时索引丢失、权威批次完整且Party有效 | 可重建并复核后显式清除恢复标记 |
| R13 | Party已移动兵员但身份转移被阻断 | 转移前拒绝或能完整回滚Roster，不产生无身份兵员 |
| R14 | 野战Party被销毁、Hearth结算失败 | 保留异常记录及诊断，不伪造结算成功 |
| R15 | 城堡或村庄正常部分返乡 | 返回来源村庄冷却；仅对应村庄恢复Hearth |
| R16 | 冷却结束但村庄Roster暂不可用 | 冷却记录保留，不提前删除或转入别村 |
| R17 | 村庄接口查询就绪/在役/冷却/总数 | 数量对应本村；城堡总数对应全部附属村庄 |
| R18 | 城镇/城堡及村庄双入口 | 现有菜单、AI计划、执行前二次校验继续可用 |
| R19 | 保存并加载新游戏 | Batch原籍、Recovery状态、Hearth、冷却与索引全部正确恢复 |
| R20 | AI预算审批后交易失败 | 不得提交招募费用/工资预算增量 |

**最低交付条件：** R01—R03、R07—R09、R11、R15、R17、R18、R19必须有测试证据；其余异常注入测试如果没有可用测试设施，应如实写BLOCKED，不能声称已通过。

---

## 9. 本次不处理的事项

- **旧存档兼容/迁移**：用户没有旧存档需要保留。
- **村庄改绑到另一个封邑**：不作为正常游戏需求。
- **按等级占用Hearth供养能力**、税收减免、职业兵扣户、AI军事制度权重：另一个阶段。
- 普通民兵自然衰减是否受封建兵规模影响：此前审查的**独立游戏机制问题**，可以保留作为后续验收记录；不要与这次异常一致性修复混在一个补丁中。
- 仅为“代码看起来整洁”而迁移全部`FiefPartyData`的partial方法：这次只修改存在实际风险的执行边界。

---

## 10. 源码审查链接（固定提交）

- [本次审查基准commit e912eb4e](https://github.com/GuihuaZhou/bannerlord/commit/e912eb4e4822be93e9eeb14ad23decdda9349428)
- [`FiefPartyRecruitment.cs` 征召事务](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs#L819-L944)
- [`FiefServiceReconciliation.cs` Party对账](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs#L35-L148)
- [`FiefPartyManager.cs` 对账、Recovery、Party删除及查询](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [`VillageFiefData.cs` 权威村庄数据](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [`FiefServiceLedger.cs` 运行时索引重建](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/Fief/Services/FiefServiceLedger.cs)
- [`FiefPartyReturn.cs` 返乡事务](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [`DisbandSettlementService.cs` 先转Roster再转服役身份的调用链](https://github.com/GuihuaZhou/bannerlord/blob/e912eb4e4822be93e9eeb14ad23decdda9349428/ModifiedArmy/ModifiedArmy/ForceManagement/Services/DisbandSettlementService.cs)

**核心要求：保留已达标的村庄化与双入口结构，只修复真实发生或合理注入的状态不一致；不做历史存档迁移，也不为村庄改绑扩展架构。**
