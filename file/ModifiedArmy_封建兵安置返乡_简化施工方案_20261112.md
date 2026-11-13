# ModifiedArmy：封建兵安置与返乡的最小化施工方案（Codex / AI Worker）

> 状态：**待实施**，本文件是施工指令，不代表已经修改代码。  
> 仓库：`GuihuaZhou/bannerlord`，目标模块：`ModifiedArmy`。  
> 编写依据：`main` 的 `8def87a0ee1493bff3b1446d2e651fcbcce13a74`（2026-11-12 读取）；**实施前以本地实际 HEAD 为准重新核对代码**。  
> 用户明确授权本范围内用简单流程替换安置/返乡的异常回滚和冻结；这项要求优先于仓库一般性“事务须补偿/冻结”的指导，但**不授权随意删改其他子系统的人口事务保护**。

## 1. 施工目标（一句话）

封建兵安置是 **Party.MemberRoster 减少士兵 + VillageFiefData.ReturnBatches 增加同等士兵**；返乡到期是 **ReturnBatches 减少一批 + 村庄待役 TroopRoster 增加同等士兵**。只做必要条件判断和直接数据修改，禁止再因安置/冷却操作引入村庄冻结、异常回滚或恢复状态机。

### 必须满足的行为

1. Party 的**未分配**封建兵，按原有文化兵种模板与 Hearth 供养配额安置到归属封邑的当前绑定村庄；伤员照原设计可以安置，进入返乡后视为恢复，不增加伤员持久化字段。
2. **保护所有在役批次**，包括 Party 中其他封邑来源；保留现有 `FiefServiceReconciler` 升级树匹配和“健康兵优先保护”的结果。绝不可按兵种名称粗暴把全部兵员当作可安置。
3. 安置**不改变** Hearth、`MobilizedHearth`、`ServiceBatches`、在役来源 ID、基础兵源及职业兵系统。不要建立第二套名册或兵源台账。
4. 返乡按既有配置 `Service.ReturnCooldownWeeks` 每周减 1；到 0 进入待役；如果原生村庄民兵 Party 暂未创建，则保留到期的返乡批次，在创建后再转为待役，**不重置冷却，不丢兵**。
5. **返乡和安置相互独立**：返乡 0 人不阻止安置；安置 0 人不影响既有返乡批次的结算。
6. 任何安置/冷却操作不再调用 `RequireRecovery`，也不因 `RecoveryRequired` 拦截已经存在的返乡计时/正常结算。
7. 新增或调整的**详细诊断只打印玩家当前拥有的封邑及其附属村庄的封建兵**，复用 `PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(...)`；不扫全图输出 AI 逐批明细。
8. 不新增 UI、配置参数、存档字段、定时器、任务、事务对象或独立补偿服务；只修改必要的现有方法。尽可能保留公开 API 及结果类型，避免破坏调用方。

## 2. 明确禁止

- **禁止**为本次安置与返乡增加或保留 `try/catch`、异常吞掉、自动回滚、事务快照字典、`ReplaceTroops` 补偿、二次重试、恢复状态机及 `RequireRecovery()`。
- **禁止**人为通过 `throw new InvalidOperationException("Placement...Mismatch")` 把正常业务判断改造成异常流程；业务不满足条件就直接 `return` / `ReasonCode`。
- **禁止**因为 `GetReadyRoster() == null` 拒绝“写入持久返乡队列”的安置；这个条件只影响**到期入待役**，不能影响返乡队列接收。
- **禁止**把所有 Party 士兵视为未分配封建兵；必须使用现有在役对账结果的 `UnassignedTroops`。
- **禁止**因历史 `PlacementRollbackUnverified` / `CoolingRollbackUnverified` 状态删除返乡批次或直接向待役/Party 补造士兵。
- **禁止**为清除这两个历史冻结标志无差别清除 `PopulationRollbackFailed`、`UnverifiedMobilizationDeficit`、来源/在役批次损坏等其他原因。
- 不改 AI 征兵策略、文化军事制度、人口生产、驻军、外交、原版 MilitiaParty 创建/销毁机制。

## 3. 必改文件和具体修改点

### A. `ModifiedArmy/ModifiedArmy/Fief/Services/FiefTroopPlacementService.cs`

现状：`TryPlaceAfterReturn` 的村庄候选排除了 `RecoveryRequired` 和 `GetReadyRoster()==null`；`CommitVillage` 从 Party 扣兵、给返乡字典加兵之后还有快照校验、主动抛异常、`catch` 回滚和 `PlacementRollbackUnverified` 冻结。

**A1. 保留原有规划规则**

- 继续使用 `TryResolveOperationScope` 校验玩家/AI 对封邑的合法操作范围。
- `AdvanceServiceDays` 和 `ReconcilePartyFiefTroops(party, true)` 原有在役匹配保留，不新写匹配器；任何在役身份无法安全确定时，不能偷取对应兵员。
- `template.IsEnableTroop`、`TryGetFeudalSupplyCost`、供养预算、当前村庄绑定、`maximumCount`、健康/伤员数量的限制保留。
- 保留按村庄配额分配的 `VillagePlan`，以免单一村庄超额；不改变原来的分配优先级。

**A2. 修改村庄候选门槛**

将下面这种候选过滤：

```csharp
.Where(village => village?.VillageSettlement != null &&
    !village.RecoveryRequired &&
    village.GetReadyRoster() != null)
```

改为只要求 **村庄有效且当前绑定**，其余由模板、供养、真实可安置人数和合法操作范围决定。不要在安置路径访问 `GetReadyRoster()` 作为准入条件；这次写入的是 `ReturnBatches`，不是待役 `TroopRoster`。

`CommitVillage` 的 `PlacementScopeChanged` 前置检查里同样去掉 `RecoveryRequired` 与 `GetReadyRoster() == null` 的拒绝条件；仍校验所属封邑、当前村庄绑定、Party 活跃和可用士兵/供养额度。诊断 `LogVillageEligibility()` 的 `rejection` 枚举同步调整，不再错误地输出 `ReadyRosterUnavailable` 或历史恢复冻结为安置拒绝理由。

**A3. 简化 `CommitVillage`**

- **提交前**一次性核对：计划中的每个兵种 `Count > 0`、`0 <= Wounded <= Count`；Party 实际总数/伤员数足够；`live.UnassignedTroops` 可安置总数、健康数和伤员数足够；模板允许、成本有效、供养总预算足够。任何不满足条件，返回准确原因，**不做资源写入**。
- 准备要使用的 `FiefReturnBatch`：优先复用 `WaitWeeks == doctrine.Service.ReturnCooldownWeeks` 的现有批次；不存在就创建新批次（保留现有按冷却周数合并方式）。
- **提交**：直接调用 `party.MemberRoster.AddToCounts(troop, -count, false, -wounded, 0, true, -1)`，再 `cooling.AddTroops(...)`；新建批次加入 `ReturnBatches`。多个兵种沿用既有确定性顺序。先核实原版 `AddToCounts` 对 `WoundedNumber` 的负数参数语义，别凭猜测修改。
- **删除** `coolingBefore`、`rosterBefore` 快照/恢复结构，`try/catch`、`ReplaceTroops` 回滚、`PlacementRosterDeltaMismatch` / `PlacementCoolingDeltaMismatch` / `PlacementConservationMismatch` 主动抛异常、`PlacementRollbackUnverified` 冻结分支。
- 只保留**简单的前后人数诊断**，一条操作日志记录 `partyBefore/partyAfter`、`coolingBefore/coolingAfter`、`placed`。若后态与预期不符，记录 `FileError` 的实际差值并停止后续村庄安置，**不伪称成功、不自动补兵、不冻结**；不另设补偿流程。正常合法路径不得频繁出现该异常诊断。
- 删除仅为被冻结/回滚服务的 `out bool recoveryRequired` 等局部传参；为了外部兼容可暂留 `FiefPlacementResult.RecoveryRequired` 字段，但此新安置路径不再设置成 true；检查调用方是否依赖它。
- 此文件里用于捕获军事配置读取错误、诊断供养计算错误的 `try/catch` 也应移除：前置条件明确时直接读配置和计算，真实编程错误不要吞掉。**不要顺便修改其他文件无关的异常处理。**

**伪代码（用于说明流程，不要求逐字复制）**

```csharp
// 先完成 scope/在役匹配/模板/供养和 Party 兵员检查；失败直接返回。
// 不检查 GetReadyRoster()，因为只写返乡队列。
FiefReturnBatch batch = village.ReturnBatches
    .FirstOrDefault(x => x != null && x.WaitWeeks == cooldown);
bool isNew = batch == null;
batch ??= new FiefReturnBatch(cooldown);

foreach (var entry in approvedTroops) // 人数、伤员数均已预检
    party.MemberRoster.AddToCounts(entry.Troop,
        -entry.Count, false, -entry.Wounded, 0, true, -1);

batch.AddTroops(approvedTroops.ToDictionary(x => x.Troop, x => x.Count));
if (isNew)
    village.ReturnBatches.Add(batch);

// 仅记录前后实际数量；没有 catch / 回滚 / RequireRecovery。
```

注意：代码运行在游戏内存中，并不能仅因“同一帧执行”就宣称严格原子。用户选择的是简单直接修改；如果引擎发生真正的非预期异常，应让它暴露以修正具体代码，而不是建立冻结补偿系统。

### B. `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`

现状：`ProcessVillageCooling()` 检查 `village.RecoveryRequired` 后直接停止；`CommitMaturedBatch()` 有复杂的前态快照、`try/catch`、回滚和 `CoolingRollbackUnverified` 冻结。

**B1. 返乡计时与冻结完全解耦**

- 移除 `ProcessVillageCooling` 内 `if (village.RecoveryRequired) { ... return; }`。
- `WeeklyTick` 每批仅调用一次 `Tick()`；`SessionLaunch`、`MilitiaPartyCreated` 等 `advanceWeeklyClock=false` 时**绝不扣周数**。
- `WaitWeeks > 0` 留在队列；`WaitWeeks <= 0 && GetReadyRoster() == null` 也留在队列，等原生民兵 Party 恢复。
- `WaitWeeks <= 0 && GetReadyRoster() != null` 时，把该批 `Troops` 的各兵种直接增加到待役 `roster`，再从 `ReturnBatches` 删除该批；只做一次，不能反复加入。

**B2. 删除多余事务结构**

- 删除 `CommitMaturedBatch` 中仅用于回滚的 `readyBefore` 快照、`try/catch`、回滚 `AddToCounts`、批次重新插回、`CoolingRollbackUnverified` 冻结。
- 操作前保证 `batch != null`、`batch.GetTotalCount() > 0`、条目兵种非空且人数为正，原生民兵 Party 的真实 `roster` 不为 null；非法条目要记录玩家诊断，**不能随意清空或折算为新兵**。
- 到期转入待役后按现有行为刷新人数统计。必要的前后人数日志仅对玩家当前拥有的封邑打印。
- 保留现有 `ProcessVillageCooling(village,false,"MilitiaPartyCreated")`，使民兵重建后已到期批次无需再等待一周。

**简化流程**

```csharp
if (advanceWeeklyClock)
    batch.Tick();
if (!batch.IsReady())
    continue;
var ready = village.GetReadyRoster();
if (ready == null)
    continue;
foreach (var (troop, count) in batch.Troops)
    ready.AddToCounts(troop, count, false, 0, 0, true, -1);
village.ReturnBatches.Remove(batch);
```

### C. `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`

现状：`CanIncreaseSupply()` 直接用 `RecoveryRequired` 拒绝，哪怕文化模板和供养额度足够。此方法同时被生产、升级相关流程调用，**不要为了安置另造一套 Hearth 算法**。

- 移除 `CanIncreaseSupply()` 里对 `RecoveryRequired` 的全局拒绝；该方法只回答“该兵种/数量是否满足供养与预算”。保留成本计算、空值/非法人数检查。
- 原有生产、动员人口、返还人口等业务若有独立安全校验，不在本任务里全部删除；它们并不是“安置 Party 士兵到 ReturnBatches”。
- **不要**删除或重排 `[SaveableProperty(5/6/7)]`、`RecoveryRequired`、`RecoveryReason`、`RecoveryOperationId`，因为旧存档仍需要读取。也不要修改 `FiefReturnBatch` 的保存格式。
- `GetReadyRoster()` 原生民兵名册的所有权不变；它只用于需要待役名册的操作，不影响写入持久返乡队列。

### D. `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`：旧存档兼容与调用入口

**D1. 消除历史冻结**

旧存档中，萨万特、韦桑当前均是 `RecoveryRequired=true`、`RecoveryReason="PlacementRollbackUnverified"`。在 `OnSessionLaunched()` 的 `InitializeFiefData()` **之后、重建在役索引和对账之前**，增加一个简短的一次性旧状态处理：

- 遍历现有 `VillageFiefData`；若 `RecoveryRequired` 且 `RecoveryReason` **恰好等于** `PlacementRollbackUnverified` 或 `CoolingRollbackUnverified`，将这两种废弃的流程冻结标志清除（可以调用已有 `CompleteRecovery()`）。
- 不修改 `ReturnBatches`、`ServiceBatches`、`MobilizedHearth`、Party 或民兵名册，也不重置 `WaitWeeks`。
- 仅为玩家拥有的村庄打印一次诊断，记录原原因、原 `RecoveryOperationId`、返乡批次与人数；不能声称历史回滚必然正确——旧保存数据没有充足的原始前态证据。
- 其他恢复原因不清理；若随后正常的在役/人口核对确实发现独立损坏，由原业务逻辑报告，不能把历史普通安置冻结永久视为合法数据错误。

**D2. 返乡/安置独立**

- 当前 `ReturnSelectedFiefTroops()` 末尾已经**无条件调用** `TryPlaceAfterReturn()`，这一正确变化保持不动。不要恢复 `if (returned > 0)`。
- `ReturnTroopsAndPlace()` 已经无条件调用安置，保持不动；验证返乡 0 人时通过现有定居点菜单仍能触发。
- `FiefManagementPresentationContext.Commit()` 在未选中在役兵时直接返回是**选择式返乡 UI 的正常含义**，不要把空选择硬改成“安置所有士兵”；若用户可通过现有定居点菜单完成安置，则不新增按钮。

### E. `ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs`：仅检查与必要兼容

- 现有 `FiefServiceReconciler` 负责**在役身份匹配**，保留升级树/伤员优先/战损逻辑，不能用新的简单计数器代替。
- `ValidatePartyRefs` 和在役计数校验会因 `village.RecoveryRequired` 阻断。D1 的历史标记清理必须在任何索引重建、在役对账之前完成，避免两个旧安置标记再次传染对账。
- 如果实测发现此文件仍把已废弃的 `PlacementRollbackUnverified/CoolingRollbackUnverified` 当成阻断原因，做**最小条件修正**；不要顺便重写它处理真正 `ServiceBatches`/人口账本错误的逻辑，也不因本任务移除其独立的复杂战损对账算法。

## 4. 仅对玩家封邑输出诊断

允许保留现有玩家封邑诊断工具 `PlayerFiefDiagnostics`，无需新开关/新类。

- 安置请求结果：封邑名/ID、Party 名/ID、批准人数、实际 Party 减少人数、返乡队列增加人数、失败的具体前置条件。
- 返乡每周状态：村庄名/ID、批次数、人数、`WaitWeeksBefore/After`、`CoolingProgress` / `MaturedWaitingForRoster` / `MovedToReady`。
- 旧状态清理：村庄、`RecoveryReason`、`RecoveryOperationId`、返乡人数与剩余周数。
- 所有细粒度日志先判断 `PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(...)`；禁止全地图逐村/逐批打印。避免重复/循环日志；不在诊断中执行资源写入。
- 统一说明 `BlockedByRecovery` 在新的返乡冷却路径中**不应再出现**；`GetReadyRoster()==null` 只能产生 `MaturedWaitingForRoster`，不能拒绝写返乡队列。

## 5. 施工顺序（按此执行，不要跳步）

1. **预检**：确认本地 HEAD、`git status --short`，读根 `AGENTS.md`、`ModifiedArmy/AGENTS.md` 和 `CODE_ORGANIZATION_RULES.md`；如实现与本方案不符，先列出差异，按现有代码做最小实现。
2. 检查原版 `TroopRoster.AddToCounts()` 中伤员负增量语义；确认当前 `-count/-wounded` 参数正确。找不到原版代码时，标注运行时验证为 `BLOCKED`，不要编造结论。
3. 修改 **安置服务**（A），保留既有在役与供养审批；删除异常回滚/冻结和待役名册错误准入门槛。
4. 修改 **返乡冷却**（B），每周推进，成熟且名册存在后直接转入待役；去掉 catch/补偿/冻结。
5. 修改 **供养判定**（C），使历史冻结不污染纯容量判断。
6. 修改 **旧存档加载处理**（D1），精确废弃两种旧冻结原因；不动任何兵员及周数。
7. 检查 **在役对账与各入口**（D2/E），仅补齐必要兼容，不引入新 UI 或新服务。
8. 搜索并检查改动范围内所有 `PlacementRollbackUnverified`、`CoolingRollbackUnverified`、`BlockedByRecovery`、`GetReadyRoster`、`RequireRecovery`、`try/catch`；确认该两条业务路径已不再产生回滚冻结，其他业务代码未被误删。
9. 编译并执行仓库现有适用的发布检查；只汇报真正执行的结果。**不得擅自 commit/push/开 PR。**

## 6. 必须覆盖的验收用例

| ID | 场景 | 正确结果 |
|---|---|---|
| T01 | 玩家沙拉斯旧存档含萨万特 20、韦桑 21 返乡人、`PlacementRollbackUnverified` | 加载后两个旧标记不再阻断；41 人保留、剩余周数不重置 |
| T02 | 正常 2 周返乡 | 每个真正的 `WeeklyTick` 扣 1 周，到期转入待役一次；存读档不双扣周数 |
| T03 | `WaitWeeks=0` 且民兵 Party 暂不存在 | 到期批次继续保留，`GetReadyRoster()==null` 不丢兵；创建后一次性转入待役 |
| T04 | 返乡 0 人，但 Party 有未分配且可安置的合法封建兵 | 现有定居点菜单能安置，Party 减少 = ReturnBatches 增加 |
| T05 | Party 有其他村庄/封邑在役兵与未分配兵混编 | 只扣真正未分配数；升级树/伤员匹配结果不受破坏 |
| T06 | 村庄缺少原生民兵 Party，但有供养容量 | 可以写入 `ReturnBatches`，冷却到期后等待民兵重建；不拒绝安置 |
| T07 | 容量不足、兵种不匹配、村庄绑定变化、Party 数量不足 | 提交前拒绝，不修改 Party 或 ReturnBatches，也不冻结村庄 |
| T08 | 伤员安置 | Party 总数及伤员按正确参数减少；返乡队列增加相同总数，期满为健康待役 |
| T09 | 多兵种、多村庄批量安置 | 按原规则分配，任何村庄供养容量均不被突破；累计计数相等 |
| T10 | 同一村同周已有冷却批次 | 按既有 `WaitWeeks` 规则合并；不生成多份重复兵员 |
| T11 | 返乡已完成再次执行创建回调或每周检查 | 不重复增加待役，不出现负人数或重复扣冷却 |
| T12 | 仍存在真正的人口/在役数据错误（非这两种历史原因） | 不被旧状态清理逻辑无差别清除，不凭空补兵 |
| T13 | 玩家的两个村庄日志 | 可读出前后数量与冷却变化；其他 AI 村庄不输出新增逐批诊断 |
| T14 | 游戏原版 `AddToCounts` 返回后的实际兵员数 | 操作前后符合预期，尤其是伤员负数参数；若不能测，标记为 BLOCKED |
| T15 | 编译、旧存档加载、存档保存/再次加载 | 编译成功且兵员、冷却、在役来源和 Hearth 没有意外变化 |

**测试注意**：旧存档 `PlacementRollbackUnverified` 证明曾经发生过“回滚无法证实”，并不证明过去没有发生过数量偏差。本次迁移仅废弃造成长期停摆的标记，**不宣称能在缺少原始快照的情况下还原历史兵员**。测试统计现存真实兵员即可，不能通过自动补数让测试看起来通过。

## 7. AI Worker 最终交付要求

请完成源代码修改，而不只是写一份分析报告。最终回复必须包含：

1. 逐文件的实际修改摘要（具体方法、删除了哪些冻结/catch/恢复逻辑）。
2. `git diff --stat` 和关键 diff；说明是否误改了其他模块。
3. 编译命令与真实结果，`PASS/FAIL/BLOCKED`；对 T01–T15 逐项给出证据状态。
4. 专门确认：**`TryPlaceAfterReturn` 不再要求 ReadyRoster、`CommitVillage` 不再 `RequireRecovery`、`ProcessVillageCooling` 不再检查 RecoveryRequired、`CommitMaturedBatch` 不再有 catch/补偿、旧冻结状态已可从原存档安全读出并废弃、返乡 0 不影响安置。**
5. 剩余已知风险如实说明：直接连续的两个内存修改并非形式上的原子事务；若引擎发生真实非预期异常，本方案选择暴露原始错误而非编造回滚系统。
6. 不新增第二套兵员账本、不更改 SaveId、`SyncData`、文化配置、UI 或未授权的其他逻辑；不自动提交或推送。

## 8. 可直接给 Codex 的执行指令

> 请在当前 `GuihuaZhou/bannerlord` 工作树中执行附件《ModifiedArmy：封建兵安置与返乡的最小化施工方案》。严格以实际代码为准，遵守明确的“不要 try/catch、不要事务回滚/冻结、不要额外服务/状态机”的约束。只改 `ModifiedArmy` 的安置、返乡冷却和必要的旧冻结兼容；保留全 Party 在役保护、原文化/供养规则、原存档格式和玩家封邑专属诊断。先确认 HEAD、工作树和原版 `TroopRoster.AddToCounts` 的伤员语义，再实施最小改动。进行适用编译/检查并逐项汇报 T01–T15 的 PASS/FAIL/BLOCKED，不要声称未运行的游戏测试已通过，不要擅自 commit/push。
