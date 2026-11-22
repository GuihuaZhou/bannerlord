# ModifiedArmy｜保留现有 AI 补员权重、Hearth 劫掠刷新与业务回滚清理：Codex 详细施工规范

> **用途**：直接交给 Codex 逐阶段修改 `GuihuaZhou/bannerlord`。此文档是**在现有功能已实现的基础上的修正与清理任务**，不是重做 AI Party 创建、封建兵财政豁免、XML 分支 Hearth 配额。
>
> **代码基线**：`main` / `db6fc84f80041c0e047c48e01ab14df9e9060337`（读取时间：2026-11-17；提交时间：2026-11-16 15:58:19 UTC）。**开工先 `git fetch` 并记录当前 HEAD**，若 HEAD 不同先做 diff 核对，不可机械套用行号。
>
> **范围**：`ModifiedArmy/ModifiedArmy` 的业务代码及 `ModifiedArmy/CODE_ORGANIZATION_RULES.md`；保留现有文化补员权重评分与任务选择算法，不擅自修改 `ModifiedPolitics` 人口服务协议、不调文化/XML 数值、不增加新存档数据台账。
>
> **交付**：修改后的代码、编译/静态测试结果、真实调用链清理结果、关键场景日志和最终提交 SHA。本文提供实施规范，不代表已修改仓库。

---

## 0. 最重要的最终需求：不要再误解

### 0.1 AI 补员文化权重：**现有实现符合需求，不得擅改为概率抽签**

- 用户要求是：不同文化的 `MilitaryDoctrine.FiefWeight / ProfessionalWeight / MercenaryWeight` 会影响 AI **补员来源选择倾向**。权重越大，在其他条件相近时该来源越容易获得更高候选分并被选中；**并未要求按 XML 比例严格随机抽签**。
- 仓库现有 `PartyForceCandidateService.GetSourceWeights` 读取军事制度与 `war`、`peace`、`siege`、`broke`、`critical` 情境修正；`CreateCandidate` 使用 `preference * culture * 100 + coverage * 80 - travelCost - economicPenalty` 计算综合分。
- `AiForcePlanningModel.BuildDailyPlan` 当前按 `Score` 排序取最高分；`InitialPartyMobilizationCommitter.Commit` 也按 `Score` 排序调兵。**这就是现有的权重偏好机制，保留。**
- `AiForcePlanningBehavior` 中的 `AiRecruitRandomChance` 控制是否启动补员，而非来源比例抽签。这一现状并不违反本轮需求。
- **不增加 `PartyForceSourceSelector`、不新增 SourceSelectionRoll、不做 7:2:1 → 70%/20%/10% 抽签、不调整创建 Party 初始补员的 `Score` 排序、不引入 Party 当前来源组成配额。**
- 只进行只读验收：检查来源权重确实传递到 Score，候选来源是否合法，任务仍遵守已有优先级和来源更新规则。若发现具体功能性故障，须先单独报告，不因本文件直接改算法。

### 0.2 Hearth 两种概念，绝不混算

| 数据 | 单位和变化 | 当前所有者 |
|---|---|---|
| `Village.Hearth` | 村庄现场实际 Hearth；征兵扣 1/人，返乡加 1/人；劫掠损失现场人口 | 游戏/人口服务 |
| `VillageFiefData.MobilizedHearth` | **每名尚在外服役的士兵 = 1 Hearth 冻结**，与 Tier 无关 | `VillageFiefData` |
| `EffectiveSupplyHearth` | `max(0, floor(Village.Hearth + MobilizedHearth))` | 派生值，不持久化 |
| `FeudalSupply` 占用 | `∑(各状态各 Tier 人数 × 文化该 Tier Hearth 成本)` | 根据 Ready/Serving/Cooling 派生 |

**禁止**：把 `MobilizedHearth += count` 改成 `+= count * tierCost`；把返乡恢复值改成 Tier 成本；劫掠时按比例扣减 `MobilizedHearth`。

### 0.3 Hearth 劫掠规则

- 劫掠降低的是 `Village.Hearth`；**不会直接杀死外出服役士兵、不会冲销其 `MobilizedHearth`**。
- 有效 Hearth 因劫掠减少，当天下一次查询配额时就使用**新的有效 Hearth**（不必等第二天）。
- 超额后仅**阻止新的供养义务、增额升级**；不强行删除 Ready/Serving/Cooling 现有士兵，不阻止原籍返乡/冷却结束。
- 返乡时村庄 Hearth 增 `n`、`MobilizedHearth` 减 `n`，正常情况下有效 Hearth 不变。战死时 `MobilizedHearth` 减 `n` 而村庄 Hearth 不恢复，有效 Hearth 随之减小；遵循当前服役核对语义。

### 0.4 删除业务回滚、补偿和冻结，但不能牺牲兵员守恒

- **删除**成功变更后的逆向 `Rollback*`、`Compensate*`、`TryCompensate` 调用链，及由失败触发的 `RecoveryRequired` / `IsDomainFrozen` 业务冻结。
- **保留**只读 DryRun、执行前合法性/资金/容量/归属检查、来源实时复核、请求幂等、任务加载索引恢复、按实际数量结算、正常状态核对、明确错误日志。
- 一个多来源任务 A 成功 30 人、B 失败 20 人时，保留 A 的 30 人，返回真实成功数量，不把 A 撤回。
- 一个来源内部跨村执行时，尽量采用**按村/批次小单元、先验证再写入、成功单元累积**；遇到后续单元失败只停止剩余单元。不要把多资源的中途异常误报为“全程成功”。
- **不要**把“删除回滚”理解为“先随意改 Roster、人口、金钱，再忽略中途出错”。必须针对每个多资源写入定义安全顺序和前置校验；异常时记录完成到哪个阶段，尝试**向前补齐一致的已承诺操作**或按权威数据核对，不进行反向撤销、不永久冻结。没有持久化事务日志时无法承诺任意进程崩溃点的跨资源原子性；必须明确该边界而不是虚构保证。

---

## 1. 已确认的仓库现状、入口、不要重做的内容

### 1.1 已实现，应尽量保留

1. `PartyManagement/Services/PartyCreationCoordinator.cs`：先 `RunSingleDryRun(..., 50)`，真实创建 Party，再调用 `InitialPartyMobilizationCommitter.Commit(party, preview)`。初始人数低于预演值保留 Party。
2. `PartyManagement/Services/InitialPartyMobilizationCommitter.cs`：使用真实 Party 构建 `PartyForceCandidateService.BuildContext`，通过 `UnifiedRecruitmentExecutionService` 补员；按既有 `Score` 排序，符合当前偏好规则，**不修改**。
3. `Recruitment/Models/DefaultAIRecruitmentModel.cs`：`RecruitmentSource.Fief` 绕过本次工资上限、购买成本、维护预算审批。
4. `Recruitment/Models/RecruitmentSimulationState.cs`：`Commit` 统计封建兵人数，但不把本次封建兵加入 `CommittedRecruitmentDailyWage`/费用。
5. `Recruitment/Finance/ClanRecruitmentBudgetManager.cs`：`CommitRecruitment(..., RecruitmentSource.Fief)` 不消耗家族共享招募预算。**不要扩大到已服役士兵免工资**；`FreeWageDays` 和真实 `NewPartyWageModel` 保持不变。
6. `Fief/Models/VillageFiefData.cs`：两级 Hearth，按 XML 根分支最大余数法，三态 Ready/Serving/Cooling 计入 Tier 供养成本；`EnsureDailySupplyBranchQuotas` 当前仅以**游戏日和模板**缓存。
7. `Fief/Models/PartyData/FiefTroopAdmission.cs`：`TryAddFeudalTroops` 已统一新增封邑兵供养审批，初始化、每日补员、无原籍兵员安置已接入。
8. `Fief/Models/PartyData/FiefPartyReinforcement.cs`：分支剩余 Hearth 选择生产，自动升级要求额外 Hearth 和同分支合法性。**保留配额不足则禁止升级**。
9. `Fief/Models/VillageFiefData.cs`：`TryMobilize(count)` 从人口服务扣减 `count` Hearth 并把 `MobilizedHearth += count`；`TryReleaseMobilization(count,true)` 恢复 `count`，再 `MobilizedHearth -= count`。这是**每兵 1 Hearth**而非 Tier 供养成本。

### 1.2 已核实的保留项与真实待修问题

- **不属于缺陷**：`AiForcePlanningModel.BuildDailyPlan` 按 `Score` 选择最高分；`InitialPartyMobilizationCommitter.Commit` 按 `Score` 依次处理现有来源；`PartyForceCandidateService.GetSourceWeights` 已读取文化、情境修正并用于 `Score`。这些是用户认可的加权偏好机制，不得作为改造项。
- **需要修复**：`Fief/Models/VillageFiefData.cs` `EnsureDailySupplyBranchQuotas` 只判断 `_supplyQuotaCampaignDay`、`_supplyQuotaTemplate`，同一天村庄遭劫掠，缓存可能仍是旧的分支配额；实时 `GetFeudalBudgetHearth` 会与缓存结果不一致。
- **需要清理**：`FiefPartyRecruitment.TryCommitVillageRecruitment`、`FiefPartyReturn.TryCommitFiefReturn`、`PartyExchangeService.ExecuteRosterTransfer`、`UnifiedRecruitmentExecutionService.ExecuteMercenary`、`VillageFiefData.RollbackMobilization*`、`FiefServiceReconciler.Reconcile`、驻军招募/生产，以及 `TransferOperationJournalBehavior.IsDomainFrozen` 的旧事务回滚／冻结业务路径。
- **需要统一规范**：根目录 `AGENTS.md` 默认不引入回滚/补偿/业务冻结；`ModifiedArmy/CODE_ORGANIZATION_RULES.md` 仍强制要求回滚与冻结，需修正后者。

### 1.3 定位方法：按函数名而非仅靠行号

GitHub 源文件基准链接（行号可能随着新提交移动）：

- [AI 日常决策 `AiForcePlanningModel`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/ForceManagement/Models/AiForcePlanningModel.cs#L15-L84)
- [候选和权重 `PartyForceCandidateService`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs#L205-L477)
- [新 Party 初始调兵 `InitialPartyMobilizationCommitter`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/PartyManagement/Services/InitialPartyMobilizationCommitter.cs)
- [村庄 Hearth 和配额 `VillageFiefData`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs#L133-L299)
- [征召与人口动员 `FiefPartyRecruitment`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs#L1139-L1630)
- [封建兵返乡 `FiefPartyReturn`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs#L80-L319)
- [调兵与回滚 `PartyExchangeService`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs#L81-L382)
- [行动日志 `TransferOperationJournalBehavior`](https://github.com/GuihuaZhou/bannerlord/blob/db6fc84f80041c0e047c48e01ab14df9e9060337/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/TransferOperationJournalBehavior.cs)

---

## 2. 阶段 A：AI 补员权重的只读验收（无代码修改）

### A1. 现有实现审查结论

1. `PartyForceCandidateService.GetSourceWeights`（基准约 L416–462）读取文化军事制度并应用情境乘数；`CreateCandidate`（约 L378–413）以 `preference` 参与最终 `Score`。
2. `AiForcePlanningModel.BuildDailyPlan`（约 L47–84）将合法候选按综合分降序排列，选第一个作为任务来源和目的地；保留现状。
3. `InitialPartyMobilizationCommitter.Commit`（约 L28–67）使用真实 Party 候选、按评分顺序依次尝试；保留现状。
4. `AiForcePlanningBehavior` 的随机门禁只决定何时尝试补员，不要求它承担来源概率抽签。
5. `DefaultAIRecruitmentModel.GetPriority` 属于来源内部兵种排序，不可为了文化偏好改为部队来源编制比例。

### A2. Codex 只需验证，不应实施任何“加权随机改造”

- 读取各文化 `militaryConfig.xml` 的 `MilitaryDoctrine`，核实权重真实影响 `GetSourceWeights` 和候选最终 `Score`，不与兵种 Tier 等因素混淆。
- 验证候选选择时原有 `IsSafe`、可用数量、战争/和平、驻军安全底线、雇佣兵财力等筛选仍然生效。
- 验证同样条件下较高 `preference` 会提高该来源评分，但不能将评分等同于固定百分比抽签概率；距离、覆盖率、经济罚分仍可让低基础权重的来源胜出。
- 验证合法已创建任务不会因新增设计文档而改变任务归属与移动逻辑；本轮不调整此算法。
- 如果验收未发现其他缺陷，A 阶段的产出仅为“现状通过”的说明，无独立修改提交。

**严格禁改**：`PartyForceSourceSelector`、`SourceSelectionRoll`、对来源按 7/2/1 随机抽签、按 Party 现有兵种组成计算缺口、把高权重来源强制限制在 70% 人员编制，以及更改 `Score` 公式或候选排序。

---

## 3. 阶段 B：Hearth 变化（尤其劫掠）导致当日配额缓存立即失效

### B1. 必须理解当前的时效性缺陷

文件 `Fief/Models/VillageFiefData.cs`：

- `GetEffectiveSupplyHearth()`：每次从 `Village.Hearth + MobilizedHearth` 读取实时值；**无需修改公式**。
- `GetFeudalBudgetHearth()`：每次使用实时有效 Hearth 计算封建兵总预算；**无需修改公式**。
- `EnsureDailySupplyBranchQuotas()` 当前条件：`_dailySupplyBranchQuotas != null && _supplyQuotaCampaignDay == campaignDay && _supplyQuotaTemplate == template`。因此同日劫掠导致总预算变化时，**分支配额仍可能是旧值**。

### B2. 最小修复：缓存键加有效 Hearth

在现有 `VillageFiefData` 增加**非 Saveable** 私有字段：

```csharp
private int _supplyQuotaEffectiveHearth = int.MinValue;
// 如果支持同日热更文化军事制度，再记录以下权重（也是非 Saveable）：
private int _supplyQuotaFiefWeight = int.MinValue;
private int _supplyQuotaProfessionalWeight = int.MinValue;
```

`EnsureDailySupplyBranchQuotas(template, doctrine)` 进入时先计算：

```csharp
int day = (int)CampaignTime.Now.ToDays;
int effective = GetEffectiveSupplyHearth();
if (_dailySupplyBranchQuotas != null &&
    _supplyQuotaCampaignDay == day &&
    _supplyQuotaTemplate == template &&
    _supplyQuotaEffectiveHearth == effective &&
    _supplyQuotaFiefWeight == doctrine.FiefWeight &&
    _supplyQuotaProfessionalWeight == doctrine.ProfessionalWeight)
    return;

// 沿用原来的 GetFeudalBudgetHearth/doctrine/最大余数法重算；
// 完整构造好 ReadOnlyDictionary 后，再一次性记录缓存日/模板/effective/权重。
```

`InvalidateDailySupplyBranchQuotas()` 同时将上述缓存键复位。`Repair()` 已调用 `Invalidate...`，须确保新字段也清理。

**不能只在 `DailyUpdate()` 增加一次刷新**：如果同一游戏日已经有缓存，即使 `DailyUpdate` 再调用 `Ensure...`，旧键仍会命中。

### B3. 是否要监听村庄被劫掠事件？

**首选不用新增事件监听**，使用上述“读时校验实际有效 Hearth”的方式：

- 无论劫掠的原版事件早于还是晚于 Hearth 实际扣减，**只要下一次读取发生在扣减之后，就必定取到新配额**。
- 也覆盖饥荒、其他 Mod、税制/人口调整、村庄成长、动员异常修复等同日有效 Hearth 变化。
- 省去维护多个事件入口和事件时序风险，不新增持久状态。

若 UI 需要劫掠结束后主动刷新展示，可以在既有 UI 刷新动作中重新查询 `GetSupplyBranchQuotas()`；不需要额外维护一份配额模型。不要根据 `VillageBeingRaided` 的“正在劫掠”事件提前错误扣除一遍 Hearth。

### B4. 有效 Hearth、冻结 Hearth、Tier 供养占用的算例

**例子 1：征兵**

- 初始实际 Hearth = 1000，冻结 = 0，有效 = 1000。
- 依法征召 100 名 T4 士兵：`TryMobilize(100)` 使实际 900、冻结 100、有效仍 1000。
- 假设 T4 供养成本 8，则 100 人占用**800 Hearth 供养配额**；冻结仍只有 **100 Hearth**。
- 本阶段不能改动 `FeudalSupply` 成本与冻结计数的区别。

**例子 2：遭到劫掠**

- 前一状态：实际 900，冻结 100，有效 1000。
- 劫掠导致实际降至 300，冻结仍 100，有效 **400**。
- 各分支配额基于有效 400 和现有文化 Fief/Professional 权重、XML 权重重算。
- 即使现有 Ready/Serving/Cooling 占用大于新配额，不删除、不解散、不强行回收士兵；新增兵源和增加 Hearth 成本的升级被拒绝。

**例子 3：返乡**

- 原状态：实际 300，冻结 100，有效 400。
- 全部 100 人合法返乡：实际 400，冻结 0，有效仍 **400**。
- `Serving → Cooling` 是状态转移，不走 `TryAddFeudalTroops` 新供养审批，也不因分支超额被阻断。

**例子 4：战死**

- 原状态：实际 300，冻结 100，有效 400。
- 20 名服役士兵战死：最终冻结应随服役记录核对减少至 80；已减少的现场 Hearth **不恢复**；有效变成 380。
- 不得因为“补齐冻结总数”而虚增村庄人口，不得按 20×Tier 供养成本扣冻结值。

### B5. 配额重算后保持原有法律边界

- 使用现有最大余数法，分支 quota 总和恰好等于该村庄 Fief Hearth budget。
- 未解锁分支仍参与分母但不允许生产；其 quota 不转移给其他分支。
- Ready + Serving + Cooling（含伤兵）按实际 Tier 供养成本计入对应根分支。
- 不允许跨村借额度，不允许跨 XML 分支借额度。
- `TryAddFeudalTroops` 的实时容量计算使用新配额；`PerformAutoUpgrade` 的增额检查也使用新配额。
- 超额状态允许为负的 `RemainingHearth`；不要把**展示**需要的 `max(0, …)` 写进底层真实剩余额度，避免隐藏超额程度。
- 兼容存档：缓存字段不加 `[SaveableProperty]`，不改原有 ID；读档 `Repair` 会重建。

### B6. Hearth 测试矩阵

| 场景 | 预期 |
|---|---|
| 同日首次查询，H=1000 | 建立分支缓存 |
| 同日重复查询且有效 Hearth 不变 | 缓存命中，结果稳定 |
| 同日劫掠现场 Hearth 减 600 | 下一次查询**立即重算**，不等次日 |
| 征召 n 人，现场 -n，冻结 +n | 有效 Hearth 不变；供养占用 Ready→Serving 平移 |
| 返乡 n 人，现场 +n，冻结 -n | 有效 Hearth 不变；Serving→Cooling 仍合法 |
| 战死 n 人，冻结 -n，现场不返还 | 有效 Hearth -n，配额下调 |
| Hearth 下调导致某分支负余量 | 存量不删；新增、增额升级被拒绝 |
| 多分支和未解锁分支 | 最大余数与按 XML 权重分配不改变 |
| 同日军事制度 Fief/Professional 权重有效更改 | 若支持热更，则下一次读取也重算 |
| 存档读写重建 | 新缓存字段不进入存档，数值正确 |

---

## 4. 阶段 C：删除业务回滚／补偿／恢复冻结——必须逐个调用链施工

> **不能只全文替换 `Rollback` 字符串或删除 catch。** 要用源/目的实际数量守恒、人口动员数量以及已执行成功量来驱动新的控制流。

### C0. 安全边界与允许的行为

**应删除的**：逆向撤销已成功资源变更（Roster、Hearth、Gold、市场库存、预算）、补偿原操作、遇到部分失败把 Party/村庄/整个任务标记为 `RecoveryRequired` 并不允许再招募。

**应保留的**：

- 执行前检查 Party 是否活跃、是否正战斗、所属、绑定、兵种资格、现存来源库存、兵员容量、驻军最低防御、家族招募额度、村庄 Hearth、服务批次身份。
- 幂等：同 `operationId` 重复请求不能再次交付或扣款；**完成记录只代表实际已完成的操作**，不是锁死整个 Party。
- 只读快照作**即时预检查**可以保留；不要把旧快照清单当必须完全匹配才能允许一次合法部分执行的唯一依据。
- 正常服役登记 `FiefServiceBatch`、`MobilizedHearth`、`Ready/Serving/Cooling` 三态核对。
- 读档/新游戏**初始化顺序**的 Ready 判断。`CampaignRecoveryCoordinator` 目前也负责启动时 `RebuildServingDetachmentIndex`、`RevalidateAndRebuildIndexes`、任务/偏好重新验证；这是**加载就绪**，不应因为名称里有 Recovery 就整类删掉。
- `PartyForceTaskStatus.Suspended` 被高优先级原版 AI 行为占用时的正常暂停，不等同于 `RecoveryRequired` 冻结；要区分。

**失败语义**：`Rejected`（此单元没改东西）、`PartiallyCommitted`（至少一个完整单元成功）、`Committed`（已批准数量全部成功）。不用 `RolledBack` 和 `RecoveryRequired` 表示新的业务执行结果；若 enum 已关联旧存档，暂保留旧数值占位/兼容解析、停止产生这些结果，不要重排值。

### C1. `PartyExchangeService`：移除执行器的反向 Roster 修改

文件 `ForceManagement/Services/PartyExchangeService.cs`。

**当前**：

- `ExecuteRosterTransfer`（约第 279–382 行）把每一步逆向操作记录到 `rollback List<Action>`，后续失败反向执行，可能返回 `RolledBack / RecoveryRequired`。
- `CompensateCreationGarrisonWithdrawal`（约第 122–198 行）专门恢复创建时的驻军撤兵；新创建模式不需要它。
- `Validate` 除校验 `operationId` 幂等外还看 `journal.IsDomainFrozen(...)`，令相关 Party 一直不能操作。
- `ExecuteFiefMobilization` 仍依赖旧计划指纹/完全一致的封邑 manifest；计划和真实来源变化可造成不必要拒绝。此点需与实时执行器结合处理，不可粗暴放过不合法差异。

**修改步骤**：

1. 全仓搜索 `CompensateCreationGarrisonWithdrawal`，确认无有效调用；删除该方法及专属补偿链。若旧调用存在，改为保留已成功转移结果，**不能调用补偿**。
2. `ExecuteRosterTransfer` 在写入前一次性校验：来源/目的活跃、合法配对、人数与伤员数、来源受保护封建兵数、目的 Party 容量、驻军防御底线，快照如已变动则**重新评估或拒绝当前单元**。
3. 把 manifest 按 `CharacterObject.StringId` 稳定排序，逐个 `(troop,count,wounded)` 作为完成单元；成功移动后累加 `actualMoved`。要按真实 `(count,wounded)` 验证来源 -n、目的 +n；下一个兵种不合法则停止并保留先前成功。
4. 删除 `rollback List<Action>` 及逆向 `AddToCounts`。**每一单元**的预验证必须在修改 Roster 前完成；普通业务不足不得靠抛异常驱动。若底层 API 在两个 Roster 改写之间发生未预期异常，输出严重诊断（两侧实际数量、当前 troop、阶段），不可虚报守恒；必要时在同一操作调用中进行**向前完成已承诺的单元**，而不是反向扣回来。避免永久冻结整个 Party。
5. 根据 `actualMoved` 返回 `Committed`/`PartiallyCommitted`/`Rejected`，日记只记已提交的数量。未完成计划不要在完成缓存中冒充全额成功。
6. 保护 `OperationAlreadyCompleted` 等幂等；不再使用 `IsDomainFrozen()` 作门禁。
7. `ExecuteFiefMobilization` 保留真实来源授权，但重审 `SourceVillageManifestChanged` 和 `AuthoritativeFiefManifestChanged` 的“完全相等”条件：改成按最新实际来源求合法上限（由底层统一 Fief 招募处理），允许源改变导致减少；不允许一次请求超出当前源真实数量。尽量不另写一套兵源审批算法。

### C2. `FiefPartyRecruitment.TryCommitVillageRecruitment`：按村成功，不再全局回滚

文件 `Fief/Models/PartyData/FiefPartyRecruitment.cs`，当前约第 1139–1630 行。

**当前路径**：`SourceRoster.AddToCounts(-n)` → `TryMobilize(n)` → 创建/注册服务批次 → Party `AddToCounts(+n)` → postcondition；发生异常后启动 `FiefRecruitmentStage.Rollback`，包括 `RollbackMobilizationReservation`、Roster 反向恢复及 `RecoveryRequired`。

**目标改法**：

1. 保留现有原籍、Party 绑定、兵源批准、伤员明细、最小 AI 征召批量、Hearth 实际扣减与 `FiefServiceBatch` 对应关系。
2. 招募候选**先按村**分组、再按 troop 稳定排序；不对跨村总批次做“全有或全无”的封邑资源提交。规划批准只是上限，执行前以村庄实时数据重新批准实际数。
3. 在每个村庄提交之前**完整检查**该村 Ready 各 troop/伤员数、`Village.Hearth >= 本村出征人数`（注意每兵 1，而非 Tier 成本）、`MobilizedHearth` 正常范围、目的 Party 剩余容量、服务批次登记可行性及来源指纹/生命周期有效性。
4. 只有这些可预见失败检查通过后才开始本村写入；每村新建合法 `FiefServiceBatch`，与真实 Party 和村庄关联。
5. 一个村庄成功的调拨必须同时满足：Ready 减 n、Party 增 n、Serving 增 n、现场 `Village.Hearth` 减 n、`MobilizedHearth` 增 n。**每兵冻结正好 1 Hearth**。供养成本仍按 Tier 计入 Serving 分支；不要在征兵时另扣 Tier 供养成本。
6. 一村成功后立即累加 `actualCommitted`，下一村遇到检查失败直接停止（或跳过到其他有足额来源的村，保持既有优先级）——绝不回滚前面村庄。
7. 删除 `FiefRecruitmentStage.Rollback` 和 `VerifyRecruitmentRollback` 等只为逆向恢复服务的逻辑，替换为真实完成数、村庄维度记录及正常核对。`RecoveryRequired` 不再由此路径产生。
8. **特别处理中途异常**：多资源写入不是天然原子操作。重构时应尽量把可失败操作前置并封装成尽可能小的“已完成单元”；如果不能保证本村整个单元成功，必须标记其**实际已发生的分步写入**并记录严重日志，然后进行基于真实 Roster/ServiceBatch/Hearth 的正向校准，不可简单 `catch { return false; }` 让损失被隐藏。
9. 已成功补员后仍通过现有家族财政 API 传递 `RecruitmentSource.Fief`；不在这一阶段改动实际 `FreeWageDays` 工资。

**重要：**已有代码里的 `TryCommitVillageRecruitment` 虽然含旧事务机制，但不等于新 Party 创建仍使用预演旧 manifest；修改它时应同时覆盖玩家手动征召和 AI 普通补员，防止单一路径被改漏。

### C3. `FiefPartyReturn.TryCommitFiefReturn`：返乡保留部分成功

文件 `Fief/Models/PartyData/FiefPartyReturn.cs`，当前约 80–319 行。

**当前**：返乡后若晚一步失败，会逆向恢复 Party Roster、批次、Cooling、MobilizedHearth、村庄 Hearth，异常时 `RequireRecovery`。

**新规则**：

1. 返乡前按**真实 `FiefServiceBatch`** 检查来源 Party、对应村庄、可返人数、伤员数和 `MobilizedHearth >= 返乡人数`，检查目标 Cooling 批次可建立/合并。**不审核新的 Hearth 分支配额**：Serving→Cooling 不是新增供养，即使村庄刚被劫掠后严重超额也允许返乡。
2. 尽量按 `troop`/小批次执行且只处理已核准的实际人数；每完成一批：Party -n、Serving -n、Cooling +n、`MobilizedHearth -n`、村庄现场 Hearth +n。
3. `TryReleaseMobilizationExact` 的 `count` 是人数，恢复 `n` 个实际 Hearth，不是 `n × tierCost`。若人口服务因可预见条件会拒绝，必须在其他写入前做准确检查；不通过则**本批不动**。
4. 一批完成后保留该批，后续失败只停止剩余；不反向恢复已完成返乡。返回 `ActualReturnedCount` 和 `ActualRestoredHearth`，两者在本机制下通常同为 `n`。
5. 删除 `RollbackMobilizationRelease`、`RestoreRosterEntry` 异常补偿分支等已无其他有效用途的回滚代码；移除 `RequireRecovery` 后不能把状态不同步视为成功。异常日志必须报告哪一个资源变更已经执行，后续核对按权威批次状态前向修正。
6. 冷却结束 Cooling→Ready 不重新申请 Hearth、新增供养额度；继续复用原状态转移。

### C4. `VillageFiefData`：去掉补偿操作及冻结标记，保留人口规则

文件 `Fief/Models/VillageFiefData.cs`。

**保留并加强**：

- `TryMobilize(count)`：验证当前现场 `Village.Hearth` 足够、人口服务成功扣 `count`，再 `MobilizedHearth += count`；`count` 是**人数**。
- `TryReleaseMobilization(count, restoreHearth)`：人口服务成功恢复 `count`（若需要）后 `MobilizedHearth -= count`。
- `RemoveInvalidMobilization(count, restoreHearth)`：**作为正向核对中的不再服役人数释放**可以保留；如果来源是战死，`restoreHearth=false`，减少冻结不回补现场人口。审查全部调用者。
- `TryGetRawServingCount` 对服务批次非法数据的检测、`Repair` 初始化、真实 Hearth 查询。

**删除/失活**：

- `RollbackMobilization`、`RollbackMobilizationReservation`，以及只供上述方法使用的 `LastMobilizationOperationId/Amount`、`LastReturnOperationId/Amount` 的**运行时依赖**。
- `RequireRecovery`、`CompleteRecovery` 导致的**持续业务冻结**；`TryMobilize`/`TryReleaseMobilization` 不因历史标志为 true 而拒绝合法请求。
- `RecoveryRequired / RecoveryReason / RecoveryOperationId` 为**已存在存档属性**，不要贸然重排或复用旧 `[SaveableProperty(5..7)]` ID。推荐暂保留为**兼容读取但不影响业务**的旧属性，不再写新的 true，读档清理旧标记并记录诊断；待跨版本存档验证后再决定物理删除。
- 老事务 operation ID 同样遵守存档兼容策略，不能把旧字段编号分配给新用途。

**注意**：`TryMobilize` 使用 `SettlementPopulationServiceRegistry.Current.TryConsume`；停止调用 `TryCompensate` 不等于可以自己直接写 `Village.Hearth` 绕过人口服务。

### C5. `FiefServiceReconciler` 与 `FiefPartyManager`：用正向核对代替冻结

主要文件：

- `Fief/Models/FiefServiceReconciliation.cs`：`FiefServiceReconciler.Reconcile` 约第 45–245 行。
- `Fief/Models/FiefPartyManager.cs`：`ReconcileMobilization` 约第 471–529 行、`PrepareLegacyMobilizationRecovery`、`RebuildServingDetachmentIndex`、`RollbackMobilizationRelease`。
- `Fief/Services/FiefServiceLedger.cs`：Party 与服务批次索引和阻塞逻辑。

当前核对会先计算 Party 现存与服务批次的升级树匹配，再替换批次、释放多余冻结；失败后尝试恢复旧批次及旧 `MobilizedHearth`，严重时 `RequireRecovery`。

新逻辑（必须精确）：

1. **核对仍必须存在**。保持 `FiefServiceBatch` 为服役义务权威记录，Party Roster 为实际士兵权威来源，`Village.Hearth` 的改变仍由人口服务拥有。不能建立第二份士兵台账。
2. 对真实已战死/永久丧失的服务兵：对应 `FiefServiceBatch` 减少，`MobilizedHearth` 按**人数**降低，**不恢复村庄现场 Hearth**；之后有效 Hearth 同步下降。
3. 当 `MobilizedHearth > rawServingCount`：原代码已有 `RemoveInvalidMobilization(excess, false)`。保留这一**正向收敛**手段和详细日志，不要把这类确认为战损后的减记误判成回滚。
4. 当 `MobilizedHearth < rawServingCount`：不能凭空将冻结加到 `rawServingCount`，那会虚增有效 Hearth。先检查可否按真实现场人口补做合法动员（调用现有人口服务成功扣减 n，再加冻结 n）；若不满足，记录明确的本村数据不一致与受影响批次，并拒绝**无法证明合法的本次操作**；健康 Party/村庄可继续工作。不设置永久 `RecoveryRequired`/全域冻结，更不擅自清除服役士兵。
5. 某批次原籍/来源标识异常、重复 BatchId 等：只跳过不安全的这批、记录诊断供修复；其余合法批次继续核对。不要把“局部数据未通过校验”升级为整个战役无法修改资源。
6. 调整 `FiefReconciliationResult.BlockedByRecovery` 及调用链语义：如已在其他模块使用该字段，可暂作为**本次数据完整性拒绝**的兼容字段（建议重命名为 `InvalidCurrentState`，调用方同步替换），不再代表持续冻结。返回当前 `ReasonCode`，不要保存冻结标志。
7. 原 `FiefServiceLedger` 的 `_blockedServiceParties` 需要核查全部写/读和重建逻辑：移除历史持久冻结含义；需要防重复服务索引时，可做当次局部校验，但不持续阻止合法 Party 行动。
8. 新游戏/读档时的 `FiefPartyManager.InitializeFiefData`、`RebuildServingDetachmentIndex`、`ReconcileMobilization` 保留；`PrepareLegacyMobilizationRecovery` 替换成一次旧冻结字段兼容迁移/数据诊断，不再“等待人工恢复”才能继续正常业务。

### C6. `TransferOperationJournalBehavior`：保留幂等，删除冻结

当前 `TransferOperationJournalBehavior.cs` 的 `_records` 使用存档键 `modifiedarmy_transfer_operation_journal_v1`，把 status、完成数、source/destination fingerprint、`FreezeDomain`、天数写成分隔字符串；`IsDomainFrozen(domain)` 搜索 `RecoveryRequired` 记录。不能把这份持久记录简单清空，否则读档后可能重复执行旧任务。

实施步骤：

1. 保留 `Contains(operationId)` 防重复执行以及已完成操作的存档记录读取能力；禁止在新流程产生 `RecoveryRequired` 记录。
2. 删除 `IsDomainFrozen` 的业务调用：`PartyExchangeService.Validate`、`UnifiedRecruitmentExecutionService.ExecuteMercenary`、`PartyForceTaskCoordinator.EvaluateParty`、`AiForcePlanningBehavior` 相关入口；其他调用者用全文搜索补齐。
3. 旧存档 journal 含 `RecoveryRequired` 的条目：**不再视为冻结令牌**。对历史 `Committed / PartiallyCommitted` 保持幂等；对历史 `RecoveryRequired` 既不能一律当已完成也不能无脑重放，须读取实际 Roster/批次/预算事实，在载入时一次性决定该 `operationId` 是否应只保留为“已观察操作”，并使用新任务 ID 继续缺口，不执行旧写入清单。
4. 删除 `FreezeDomain` 新增写入；若 `PartyExchangeExecutionResult` 中的这个字段被外部日志/API 读取，可暂留兼容属性但恒为空。不要重新排列 `TransferCommitStatus` 枚举数值。
5. `TrimCompletedRecords` 不再因旧 `RecoveryRequired` 永久保留无限记录；但 2048 容量和幂等窗口的现有约束要先核对真实调用和保存要求，不能为了“简化”导致频繁重复扣兵。

### C7. `UnifiedRecruitmentExecutionService.ExecuteMercenary`：删除市场/金钱反向退款

文件 `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`，约第 138–336 行。

**当前**：购买雇佣兵时变更 `Hero.Gold`、市场库存、Party Roster、家族招募预算；异常执行 `TryRollbackMercenaryPurchase`，失败则冻结。

**新要求**：

1. 保留市场当前 offer、`RecruitmentModelManager.Model.BuildPlan`、金币余额、`ClanRecruitmentBudgetManager`、人数容量、合法领主及定居点检查；先算真正批准的 `count` 和 `count * unitCost`，整数乘法检测溢出。
2. 所有可能**正常拒绝**的条件都在资源写入前完成；同一个 `operationId` 查成功日志防重复购买。
3. 将批量购买拆成由当前基础市场 API 可安全执行的最小单元（若单次整批 API 已能一次保证两边数量守恒，可沿用整批）；顺序须使货币扣减、市场减少、Roster 增加与预算承诺在正常路径下相互对应。
4. 已经完整购买的士兵不执行反向退款、退回市场、Roster 扣除或预算还原。后续步骤失败只按可确定的已成功单元记录实际人数与成本。
5. 删除 `TryRollbackMercenaryPurchase`、`RestoreRemainingBudget` 在此流程的使用、`RolledBack/RecoveryRequired` 失败赋值及 `FreezeDomain` 写入。
6. 游戏通知 `OnTroopRecruited` 和本地化消息应在业务提交之后执行；通知抛错只记录，不影响购买既有成功结果（当前代码对此已单独 catch，应保留）。
7. 金币、市场、Roster、预算存在多个独立 API，任意时点崩溃不可凭空保证完全一致；需加入精确阶段日志/失败前置校验和回归测试，不能“删除 catch 后当作安全”。

### C8. 驻军职业兵/俘虏招募的补偿：一并删除，必须保持产能/资金守恒

主要调用链：

- `Recruitment/Services/GarrisonRecruitmentExecutionService.cs`：财务 `CommitRecruitment` 失败时恢复预算及 `TryRollback` 原兵源；异常设 `RecoveryRequired`。
- `Recruitment/Services/ProfessionalRecruitmentService.cs`：招募成功后会更改 garrison Roster 和职业兵 offer，存在 `TryRollback()`、`RollbackProfessionalOffer` 路径。
- `Recruitment/Services/PrisonerGarrisonRecruitmentService.cs`：查 `TryRollback` 和俘虏来源数量的转移。
- `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`：查 `RollbackProfessionalOffer` 的实现与调用方。
- `Recruitment/Patches/GarrisonRecruitmentPatch.cs`：查操作失败后的逆向改写和人口补偿。
- `Recruitment/Services/GarrisonRecruitmentExecutionResult.cs`：`RecoveryRequired` 结果字段的引用链。

实施步骤：

1. 将 `Budget` 是否可承诺、新兵人数/费用、驻军可用空位、真实专业 offer/俘虏数、文化/级别资格放入写入前完整审批。
2. 先决定 `approvedCount`；采用每来源/每种 troop 的小单元成功累积。每个完成单元必须真实消费对应职业兵 offer 或俘虏、增加驻军人数，并按该来源规则扣预算/实际成本。
3. 已完整成功的单元不回滚；后续来源失败返回实际成功数与原因。
4. 删除 `ProfessionalRecruitmentService.TryRollback`、`PrisonerGarrisonRecruitmentService.TryRollback` 和其唯一使用的调用；如果 `RollbackProfessionalOffer` 是公共库方法，被其他功能调用要先迁移全部调用者，再删接口。不要只删一个方法导致编译失败。
5. `GarrisonRecruitmentExecutionResult.RecoveryRequired` 可保留反序列化兼容占位但不再置 true；只读错误/部分完成状态应由真实计数和明确结果代码表达。
6. 不动**玩家驻军自动招募与 AI 共享预算的既有区别**；不把封建兵财政豁免传播到职业/俘虏招募。

### C9. `FiefPartyEconomy.UpdateHearth`：多村变更不再整单逆向补偿

文件 `Fief/Models/PartyData/FiefPartyEconomy.cs` 约第 195–405 行。

当前 `UpdateHearth` 把人口变化分配到多个 `BoundVillages`，如果后一个村失败就 `RollbackPopulationChanges`（逐村 `population.TryCompensate`），失败又设 `RequireRecovery`。

修改：

1. 先按照现在的算法计算各村**计划实际变化**，包括 `VillageMinHearthThreshold` 下限；**不要悄悄改变经济公式、总数拆分或正负余数规则**。
2. 对每个村单独调用已有 `ISettlementPopulationService.TryConsume/TryRestore`，成功则累加**真实已应用**人口变化；失败则跳过或停止余下待执行村庄，不撤销先前成功的村庄。
3. 删除 `RollbackPopulationChanges` 和该路径全部 `TryCompensate` 调用；不设置 `RecoveryRequired`。
4. 返回真实已应用的数额（注意原代码 `committedAmount` 累加的是**绝对值**，现有 `UpdateHearth` 约定若返回带符号量，需保持签名契约），记录 partial count/村庄 Id/操作 Id/实际值。
5. 人口服务的 `TryConsume/TryRestore` 是权威写入入口，不能直接写 `Village.Hearth`。

### C10. 其他遗漏：全仓机械检索 + 逐条判定，不能只改已列文件

Codex 必须执行以下检索，把命中形成清单；只处理 `ModifiedArmy/ModifiedArmy` 当前业务代码，不因 `file/`、`baseline/` 的历史方案文字执行过时指令：

```bash
# 在仓库根目录，Windows 可使用 rg，也可用 Git Bash。
rg -n 'Rollback|RolledBack|TryRollback|TryCompensate|Compensat' ModifiedArmy/ModifiedArmy -g '*.cs'
rg -n 'RecoveryRequired|RequireRecovery|CompleteRecovery|IsDomainFrozen|FreezeDomain|DomainFrozen' ModifiedArmy/ModifiedArmy -g '*.cs'
rg -n 'RestoreRemainingBudget|Restor.*Roster|SourceSnapshotChanged|ManifestChanged' ModifiedArmy/ModifiedArmy -g '*.cs'
rg -n 'CanMutateCampaignState|CampaignRecoveryCoordinator' ModifiedArmy/ModifiedArmy -g '*.cs'
```

需检查 `FiefTroopConversionService`、`FiefTroopPlacementService`、`FiefReturnService`、`Recruitment/Patches`、`Garrison/Supply` 下的补偿分支，以及供养/补员 UI 或日志中以 “RolledBack” 表示状态的地方。某些 `Rollback` 可能只是**历史注释或纯只读预演概念**，不要误删真实功能。

**尤其不要忽略 `Garrison/Supply` 征粮队的货物/人员状态**：若存在为撤销合法配送而写的逆向操作，在本轮统一改为前置检查、按实际装卸成功量推进；不应改变征粮队 AI 导航和货物经济模型。对确实不属于“业务失败恢复”而用于合法玩家**主动退货/取消订单**的反向动作，应与取消机制区别，保持功能。

### C11. 项目规范与存档兼容

1. `ModifiedArmy/CODE_ORGANIZATION_RULES.md` 的“Resource-changing workflows must define validation, mutation order, compensation, downstream event handling, and save/load recovery”必须改为：**审批→小单元提交→真实部分成功→日志/权威核对；默认禁止业务回滚、反向补偿和失败冻结；只保留读档重建与幂等**。
2. 删除其中“`RecoveryRequired` state freezes affected workflow...” 的强制规则。
3. `AGENTS.md` 中“默认不得引入回滚/补偿/冻结；确需使用需用户确认”的更高优先级规范保留，确保两处统一。
4. 旧存档可能携带 `VillageFiefData.[SaveableProperty(5..11)]`、旧 operation journal、`PartyForceTask` 状态字段。不要重排 Saveable Property ID、Saveable Type Definer ID、或旧 enum 数值。兼容旧值但**不再用它们阻断合法业务**；不要把旧的 `RecoveryRequired=true` 直接转成“补偿成功”伪记录。
5. `CampaignRecoveryCoordinator` 的“游戏加载索引重建”保留或改名为 Initialization Coordinator。**删除的是业务失败触发的长久冻结，不是加载完成前禁止在未初始化世界乱写数据**。
6. 诊断与 localization：如果撤掉某些 `str_...rollback...` 文案，先检索全部引用；未引用的 key 可以后续单独清理，不得因删除文案破坏中文本地化。

---

## 5. 实施顺序：A 阶段只读验收，B/C 阶段分小提交

### Commit 1：修复 Hearth 日内配额缓存

涉及：`VillageFiefData.cs`。只改运行时缓存键及相应测试/日志，不触碰动员数值公式。验收 B6。**这是最小且优先的补丁**。

### Commit 2：封建兵征召/返乡及人口冻结的无回滚重构

涉及：`FiefPartyRecruitment.cs`、`FiefPartyReturn.cs`、`VillageFiefData.cs`、`FiefPartyManager.cs`、`FiefServiceReconciliation.cs`、`FiefServiceLedger.cs`。先建立前置校验和局部成功记录，再去除补偿函数；逐条跑兵员/Hearth 不变量测试。验收第 6 节 C 系列。

### Commit 3：调兵执行器和雇佣兵的无回滚重构

涉及：`PartyExchangeService.cs`、`UnifiedRecruitmentExecutionService.cs`、`TransferOperationJournalBehavior.cs`、`PartyForceTaskCoordinator.cs`、`AiForcePlanningBehavior.cs`，包括 legacy operationId 和旧存档 marker 的迁移。验收第 6 节。

### Commit 4：职业驻军/俘虏/村庄经济及其他补偿链清理

涉及：`GarrisonRecruitmentExecutionService.cs`、`ProfessionalRecruitmentService.cs`、`PrisonerGarrisonRecruitmentService.cs`、`SettlementManpowerProductionBehavior.cs`、`GarrisonRecruitmentPatch.cs`、`FiefPartyEconomy.cs` 及 `rg` 查出的所有有效运行时调用。保持现有产能、采购、人口/财政公式。验收第 6 节。

### Commit 5：存档、规范与回归测试

涉及：`CODE_ORGANIZATION_RULES.md`、代码中旧标记兼容、日志、编译和两类存档测试。不得通过删除测试/屏蔽异常实现“全绿”。

**每个 Commit 的固定交付**：修改文件列表、删除/保留了哪些入口、关键 `before/after` 不变量、编译结果、最小复现场景、当前 SHA。失败即在当前阶段修正，不叠加下一阶段。

---

## 6. 必测用例：运行结果必须有具体数量和日志

### A 组：现有文化权重行为验收（不修改算法）与 Party 创建

| ID | 步骤 | 期望 |
|---|---|---|
| A01 | 三来源各有合法候选，文化权重为 7/2/1；其他条件相同 | Fief 的 `preference` 贡献更高，综合 `Score` 相应提高；不要求 70/20/10 抽签 |
| A02 | 同一文化分别构建封建兵、职业兵与雇佣兵候选 | 候选 `Score` 包含权重、覆盖、文化、距离和经济因素，仍选择综合最高分 |
| A03 | 高权重 Fief 的所有候选村庄被围攻/无兵 | 不生成不合法的 Fief 候选；从其他合法候选按现有评分选择 |
| A04 | Party 已在走原合法 Recruit 任务 | 现有任务有效性与稳定性保持原有行为，无新来源抽签 |
| A05 | 文化战争/和平/危急修正 | 原有 SourceWeights 和 Score 反映修正，不新增随机抽选机制 |
| A06 | 新 Party DryRun 预演 55，真实只招 45 | Party 保留，45 人真实入伍，不回滚 |
| A07 | 新 Party 先选 Fief 且可填满目标 | 允许全封建兵；不因 7/2/1 拆分人数 |
| A08 | 实际源A完成后源B失败 | 保留源A人数，返回正确累计结果 |

### B 组：人口冻结、劫掠、配额

| ID | 步骤 | 期望 |
|---|---|---|
| B01 | 现场1000冻结0，征召100名T4，T4 cost=8 | 现场900、冻结100、有效1000、兵员供养800 |
| B02 | 紧接 B01 劫掠至现场300，仍是同一天 | 冻结100不变、有效400，分支配额**当日更新** |
| B03 | B02 使某分支已超供养配额 | 老兵保留；新增及增额升级被拒绝 |
| B04 | 100名服役兵全部返乡 | 现场400、冻结0、有效400；Serving→Cooling，无新增审批 |
| B05 | B02 中 20 人战死 | 冻结从100降到80；现场不加20；有效变380 |
| B06 | 同日读配额10次且 Hearth 不变 | 结果一致，缓存正常复用 |
| B07 | 同日多次实际 Hearth 变化，跨午夜再变化 | 每次查询按当前有效Hearth计算；次日正常重建 |
| B08 | 多村庄、多个根分支、一个锁定分支 | 不跨村借 quota；锁定分支不向其他分支让出 quota |

### C 组：无回滚与兵员守恒

| ID | 触发 | 期望 |
|---|---|---|
| C01 | Roster A→B 转移计划 50，其中 30 可转、后 20 变动 | 实际成功30；A -30、B +30；无逆向回写 |
| C02 | 村庄 A 征兵成功、村庄 B Hearth 不足 | A 服役批次和冻结保持成功；B 不动；不冻结封邑 |
| C03 | 返乡 25 人，后续兵种不合法 | 已完成部分 Serving→Cooling，Hearth 每人返1；不反向撤销 |
| C04 | 同 operationId 重复执行成功调兵/买雇佣兵 | 不再次加兵、不再次扣金/库存 |
| C05 | 旧存档带 `RecoveryRequired=true` | 加载后不因旧标记长期冻结正常 Party；记录诊断，核对真实数据 |
| C06 | 兵员战死后下一次核对 | 批次减少 n，冻结减少 n，现场不恢复，无凭空回填 |
| C07 | `MobilizedHearth < rawServing` 且现场 Hearth 不足 | 不凭空加冻结、不删除士兵、不全域冻结；本次不安全操作拒绝并记录问题 |
| C08 | 职业兵招募财政审批不足 | 源兵/offer/驻军 roster 不提前消耗，不靠回滚退款 |
| C09 | 雇佣兵购买成功但后续 UI 通知抛错 | 兵力、钱、市场、预算仍保留真实成功；不冻结 |
| C10 | 多村经济人口变更中途一村拒绝 | 先前村庄已成功的变更保留；返回真实变化，不调用 TryCompensate |
| C11 | 旧存档 operation journal 含历史 RecoveryRequired | 不因历史条目冻结；不重放旧清单；幂等仍有效 |
| C12 | 读档重建服务批次/Party绑定后正常行动 | 不因移除业务冻结而破坏加载时序与索引 |

### C 组“数据守恒”检查式（每个已完成小单元）

```text
# 普通 Roster 转移（不包含战损）：
SourceBefore - SourceAfter = ActualMoved
DestinationAfter - DestinationBefore = ActualMoved
Wounded 数量同步转移，0 <= Wounded <= Total

# 封建兵征召：
ReadyBefore - ReadyAfter = n
ServingAfter - ServingBefore = n
PartyAfter - PartyBefore = n
VillageHearthBefore - VillageHearthAfter = n
MobilizedHearthAfter - MobilizedHearthBefore = n
# 注意：n 是士兵人数，不是供养成本！

# 原籍返乡：
ServingBefore - ServingAfter = n
CoolingAfter - CoolingBefore = n
PartyBefore - PartyAfter = n
VillageHearthAfter - VillageHearthBefore = n
MobilizedHearthBefore - MobilizedHearthAfter = n
EffectiveHearthAfter = EffectiveHearthBefore （假设期间无其他人口变动）

# 战死（确认不返还 Hearth）：
ServingBefore - ServingAfter = n
MobilizedHearthBefore - MobilizedHearthAfter = n
VillageHearthAfter = VillageHearthBefore
EffectiveHearthBefore - EffectiveHearthAfter = n

# 供养约束：
OccupiedBranchHearth = sum(Ready + Serving + Cooling 的各兵种数量 × 对应 Tier 成本)
新增条件：OccupiedBranchHearth +新增Tier成本 <= 新的BranchQuota
超额旧兵不删；返乡/冷却不新增供养；增额升级必须满足剩余额度。
```

---

## 7. Codex 执行检查单（逐项勾选，不要跳步骤）

- [ ] `git status`、HEAD SHA、分支、当前 diff 已记录；如 HEAD 与文档不同先核对差异。
- [ ] 阅读 `AGENTS.md` 和 `ModifiedArmy/CODE_ORGANIZATION_RULES.md`，记录并修正规范冲突。
- [ ] 列出所有补员入口、来源选择入口、`RecoveryRequired` 生产和消费者、`TryCompensate` 调用处。
- [ ] 已核实文化军事制度权重经 `GetSourceWeights` 进入候选 `Score`，现有选择算法未修改。
- [ ] 创建 Party 初始真实征兵继续按现有候选评分排序；DryRun 仍然只读。
- [ ] `MobilizedHearth` 仍每外出士兵冻结 1，不按 Tier；真实服役记录与冻结人数能够核对。
- [ ] Hearth 缓存以 day+template+effectiveHearth(+doctrine relevant weights) 为键；劫掠同日生效。
- [ ] Hearth 超额时禁止新增及增额升级，但 Ready/Serving/Cooling 老兵可正常转移/返乡。
- [ ] `PartyExchangeService` 中的 creation compensation、roster rollback、domain freeze 均停止使用。
- [ ] `FiefPartyRecruitment` 和 `FiefPartyReturn` 中的 rollback/unverified freeze 移除并改为按实际完成单元处理。
- [ ] `VillageFiefData` 的旧 recovery/operation IDs 坚持兼容存档而非直接改 Saveable ID。
- [ ] `FiefServiceReconciler` / `FiefPartyManager` 继续完成正向核对，不永久冻结健康 Party/村庄。
- [ ] `TransferOperationJournalBehavior` 保留幂等、停止产生/消费冻结 token，处理旧记录兼容。
- [ ] 驻军职业兵、俘虏、雇佣兵、村庄人口经济旧补偿链已逐个清理。
- [ ] `rg` 搜索列出的**运行时**旧调用、日志、枚举消费者已审查；不能留不可达死方法或编译断链。
- [ ] 编译 `ModifiedArmy/ModifiedArmy.sln`，在具备 Bannerlord 依赖的 Windows 环境按当前项目构建配置执行；无环境则明确列出无法验证项。
- [ ] 运行 A/B/C 用例，结果填实际数量/日志；不要只写“测试通过”。
- [ ] 提交分阶段 commit，报告修改文件、所有删除/保留的相关方法、存档风险、最终 SHA。

### 实施时严禁的“快速修复”

1. 把 `GetPriority` 中兵种 Tier 权重改成文化来源比率，或者新增 Party 人员结构目标。
2. 以“权重不是概率”为由新增抽签选择器、来源抽签随机数或改动原有 `Score` 排序。
3. 每小时或每日对进行中的合法任务重掷来源。
4. 认为 7/2/1 需要 Party 保持 70/20/10 的人数组成。
5. 把 Tier 供养成本当成征兵冻结人口，或让 `MobilizedHearth` 在劫掠时按比例损失。
6. Hearth 不够时放宽升级；超额时直接删除士兵或阻止原籍返乡。
7. 去掉回滚时也去掉合法性/资源守恒验证，或者把异常静默 `catch { return true; }`。
8. 为了消除冻结，随便把历史 `RecoveryRequired` 操作重放一遍。
9. 更改 `ModifiedPolitics` 人口计算规则、军制 XML、训练速度、战争经济平衡、驻军保护阈值。
10. 删除/重排存档 `SaveableProperty` ID、核心 enum 数值或原版导航行为。

---

## 8. Codex 交付报告模板

```text
仓库: GuihuaZhou/bannerlord
基线 SHA:
最终 SHA:
修改阶段: A（仅验证） / B / C1...C11

变更摘要:
1.
2.
3.

文件清单（逐文件写函数变化）:
- 文件: 函数: 原行为 → 新行为

关键边界验证:
- 现有文化权重经 Score 影响来源选择（代码不变）:
- 既有任务保持稳定:
- 1 soldier = 1 MobilizedHearth:
- Hearth 劫掠同日重算:
- 返乡及战死 Hearth:
- 无 rollback/compensation/freeze 的调用链证据:
- 存档与 operationId 幂等:

编译命令与结果:
测试案例 A01...C12 的结果/日志:
失败、未测项及原因:
有无修改 XML/经济公式/真实工资/军队结构目标（应为“无”）:
剩余风险及建议:
```

**完成标准**：全部关键路径逻辑一致，实测能区分“合法部分成功”和“完全拒绝”；不再有运行时业务回滚/冻结；存档兼容；Hearth 动员人数与 Tier 供养分开；劫掠同日缓存重算；**现有文化评分影响补员来源选择倾向，不实施概率抽签或 Party 人数结构调整**。
