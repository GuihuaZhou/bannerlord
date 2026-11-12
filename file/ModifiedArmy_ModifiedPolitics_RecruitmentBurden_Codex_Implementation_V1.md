# ModifiedArmy + ModifiedPolitics：RecruitmentBurden（征兵负担）Codex 实施任务书 V1.0

> 任务类型：基于现有代码的功能实现方案；本文不是已经完成的改动或编译报告。  
> 仓库：`GuihuaZhou/bannerlord`  
> 审查基线：`9d6d100fba631eb996595019f2f55e7866402d86`（2026-11-08 查询时最新提交）  
> 基线链接：https://github.com/GuihuaZhou/bannerlord/commit/9d6d100fba631eb996595019f2f55e7866402d86
>
> **交给 Codex 的总指令：** 以工作区实际 HEAD 为权威，先执行仓库前置检查、阅读 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`，确认与本文基线的差异后再按阶段实施。不要重写现有统一人口服务，不修改封邑兵供养/动员机制，不重新引入兵源池。保留所有既有 SaveDefiner ID、序列化字段编号和 SyncData 键。只在明确实现并验证对应阶段后标记 PASS。不要自动提交或推送。

## 0. 已确认的最新代码结构与风险

| 文件／方法 | 现行实现（基线） | 本次施工目的 |
| --- | --- | --- |
| `ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs` | `ISettlementPopulationService` 仅有 Hearth 查询、`EvaluateConsumption`、`TryConsume`、`TryRestore`、`TryCompensate` | 扩展职业兵专用负担查询、可承担数量计算及原子征兵事务 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs` | `_hearth`、`_transactions`、`_recovery`、`_lastDailyTick`；城镇/城堡每日 Hearth 增长 | 唯一持有并保存 `RecruitmentBurden`；每日恢复、事务提交和补偿 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Persistence/SettlementPopulationSaveDefiner.cs` | 固定 Definer ID `2026110701`，`PopulationTransactionRecord` 类型 ID 1 | 在保留旧存档结构的前提下扩展记录字段；勿重复注册同类型字典 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs` | 繁荣度支持：`GetHearth(settlement) × (Town?5:3)`，超额84天衰退 | 改成 `EffectiveHearth × 5/3`，不立即扣 Prosperity |
| `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` | 各文化有 `Professional.Training` 和 `Fief.FeudalSupply`，职业兵 Hearth 成本在全局 `HearthEconomy.ProfessionalRecruitment` 中统一为 1 | 新增每文化 T1-T6 负担成本及 `dailyRecovery`；保留独立 `FeudalSupply` |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs` | `TryConsumeProfessionalHearth` 调用人口 `TryConsume(source,count,operationId)` | 改为统一 `TryRecruitProfessional`，取得并保存新事务 ID |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | 候选产出与实际招募分离；`GetProductionForecast()` 用 `GetRecruitableHearth` 求人口产能；`TryConsumeProfessionalOffer` 扣 Hearth，失败可 Rollback | 保持训练逻辑，新增实际招募可承担数量预审，并复用原有回滚 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Services/ProfessionalRecruitmentService.cs` | 先消费职业兵候选和人口，再写入驻军；失败回滚 | 将人口+负担视为不可分割的统一事务 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs` | 按计划批准数、日上限、驻军空间确定 `count`，再执行职业兵招募与财政事务 | 在提交前将 `count` 限制到可承担数，保证可部分招募，不整批失败 |
| `ModifiedArmy/ModifiedArmy/Population/Patches/NativeRecruitmentPopulationPatch.cs` | Prefix 只预检，Postfix 才扣 Hearth；失败没有完整回滚原版招募 | 改成安全的预留/提交/补偿适配，区分志愿兵、雇佣兵和来源不明的路线 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs` | “兵源/补员”预览已有训练及实际补充字段 | 增加 Hearth、Burden、Capacity、DailyRecovery、EffectiveHearth、繁荣度支持值和真实招募阻塞原因 |

### 已确认的特殊问题

1. `GetRecruitableHearth()` 当前是 Hearth 的向下取整；职业兵产能还经过 `CalculatePopulationProductionSupply(h) = h <= 100 ? 0 : 0.03*h*(h-100)/(h+50)`。**不能直接把这个函数参数替换为 `Hearth - RecruitmentBurden`**，否则虽然设计允许余额剩 1 时再招募最后 1 人，实际却会额外受到“余额不超过 100 就不产兵”的隐藏门槛。
2. `NativeRecruitmentPopulationPatch` 当前 Postfix 才人口扣费，无法保证资源事务与实际新增士兵一致；必须一并修复，而不能只替换费率。
3. `SettlementPopulationBehavior.ValidateNewTransaction` 遇到已存在 OperationId 时，返回旧事务状态，可能让调用方把重放误认为全新成功。新职业兵事务必须阻断重复提交，旧接口如需调整须保持旧业务行为并增加测试。
4. `PopulationTransactionRecord` 只保存 Hearth 变动，不保存事务类型、征兵负担变动；直接复用旧 `TryConsume()` 无法保证两种资源原子提交。
5. 最新提交 `9d6d100` 增加了封邑兵返乡/安置相关实现，本任务不修改这些路径，也不把 `RecruitmentBurden` 与村庄 `MobilizedHearth` 混用。

## 1. 最终业务规则（本任务不可自行改值）

- **人口语义**：1 Hearth = 1 单位实际劳动力。每成功新增 1 名职业兵，实际 Hearth 永久扣除 1。招募候选、升级、驻军→领主部队调拨、士兵死亡均不重复扣费。
- **征兵负担语义**：每座城镇/城堡独立持久化 `RecruitmentBurden`，它代表尚未消退的社会动员负担，不代表在役人数。每成功新增 1 名职业兵，按“定居点自身文化 + 实际 Tier”增加对应负担，不从 `Fief.FeudalSupply` 推导。
- **征募检查**：每招募下一名士兵之前要求 `Hearth - RecruitmentBurden >= 1`。不用一次性具备全额等级成本，允许征募后 `RecruitmentBurden > Hearth`，保留负的 `RecruitmentCapacity`；UI `AvailableHearth = max(0,H-B)`。
- **批量提交**：依次模拟每个士兵，不能在一次检查后越过限制多招。对可部分招募的 AI 驻军计划，预先裁剪 `count` 到可承担人数，然后在服务内按该数执行全有或全无的单笔事务；不准出现实际招募人数与消费人数不等。
- **每日恢复**：`RecruitmentBurden = max(0, RecruitmentBurden - dailyRecovery)`；所有文化首版值均为 `2`，但 XML 必须逐文化独立配置。每个城镇/城堡每日独立结算、同一天最多一次。不能因读档重复恢复。
- **繁荣度**：招募不直接修改 Prosperity；经济影响系数固定为 **0.50**；`EffectiveHearth = max(0,H) - 0.5*min(max(0,H), max(0,B))`；城镇 `SupportedProsperity=EffectiveHearth*5`，城堡 `*3`。原有超额繁荣度按 **84 天** 衰退的逻辑保留。
- **原版自然增长**：城镇/城堡初始 Hearth、墙级容量、增长 `0.004*(H+100)*(1-H/K)`、低人口加速、村庄玩法保持不变。
- **文化判定**：成本和每日恢复均按 `settlement.Culture`，**不按领主/家族/王国文化**；只改变定居点主人不会清空历史负担。
- **受影响对象**：城镇与城堡的新生职业兵。村庄封邑兵继续原有 `FeudalSupply`、`MobilizedHearth`、返乡和损失处理。原版村庄志愿兵的旧 Hearth 扣费不能被无意删掉；雇佣兵等特殊人口来源另按明确的来源分类处理。
- **模块方向**：ModifiedArmy 定义文化政策、招募规则与接口；ModifiedPolitics 唯一管理人口、征兵负担、繁荣度与存档；只能由 Politics 引用 Army，不能新增 Army→Politics。

### 1.1 第一版文化负担成本（必须原样配置）

| 文化 ID | 文化 | T1 | T2 | T3 | T4 | T5 | T6 | dailyRecovery |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Culture.empire` | 帝国 | 3 | 5 | 9 | 14 | 22 | 36 | 2 |
| `Culture.aserai` | 阿塞莱 | 4 | 7 | 11 | 17 | 27 | 42 | 2 |
| `Culture.vlandia` | 瓦兰迪亚 | 6 | 10 | 16 | 25 | 38 | 60 | 2 |
| `Culture.khuzait` | 库赛特 | 8 | 13 | 21 | 33 | 50 | 78 | 2 |
| `Culture.sturgia` | 斯特吉亚 | 8 | 13 | 21 | 33 | 50 | 78 | 2 |
| `Culture.battania` | 巴旦尼亚 | 10 | 16 | 26 | 40 | 62 | 96 | 2 |
| `Culture.nord` | 诺德 | 10 | 16 | 26 | 40 | 62 | 96 | 2 |

### 1.2 XML 结构

修改 `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`，在 **每一项** `<Culture><Professional>` 下加入：

```xml
<RecruitmentBurden dailyRecovery="2">
  <Tier Level="1" Cost="3" />
  <Tier Level="2" Cost="5" />
  <Tier Level="3" Cost="9" />
  <Tier Level="4" Cost="14" />
  <Tier Level="5" Cost="22" />
  <Tier Level="6" Cost="36" />
</RecruitmentBurden>
```

上例仅帝国；另外六种文化严格按表填写。保留各文化既有 `<Training>` 和 `<Fief><FeudalSupply>`。移除与新规则重复且会造成误导的全局 `<HearthEconomy><ProfessionalRecruitment>` 表及其旧解析分支（保留 `<FeudalTaxRelief>`）。`MilitaryConfig.cs` 现有 `ParseTierCosts()` 固定读取属性 `Hearth`；对新节点应添加参数化属性名 `Cost` 或专用解析器，不允许改坏旧 `FeudalSupply` 读取规则。要求 T1～T6 恰好各 1 个、均为正整数、严格递增、`dailyRecovery` 为正的有限数/正整数（本次全填 2），缺失配置启动报错而不是悄悄回退到全局成本。

## 2. 接口、状态与数据归属

### 2.1 Army 的公共契约

扩展 `ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs`，保持既有方法签名兼容；至少新增职责等价的接口（实际命名以项目现有风格为准）：

```csharp
float GetRecruitmentBurden(Settlement settlement);
float GetRecruitmentCapacity(Settlement settlement); // signed H - B
float GetEffectiveHearth(Settlement settlement);
int GetMaxAffordableProfessionalCount(
    Settlement settlement, int burdenCostPerTroop, int requested);
PopulationTransactionResult EvaluateProfessionalRecruitment(
    Settlement settlement, int count, int burdenCostPerTroop);
PopulationTransactionResult TryRecruitProfessional(
    Settlement settlement, int count, int burdenCostPerTroop, string operationId);
```

`TryCompensate(settlement,operationId)` 沿用同一入口，根据持久化事务类型补偿人口或“人口+负担”。所有拒绝需要稳定 `ReasonCode`：例如 `InsufficientRecruitmentCapacity`、`MissingProfessionalBurdenPolicy`、`InvalidBurdenCost`、`SettlementRecoveryRequired`、`OperationAlreadyExists`。

**建议单独增加只读策略契约 `IRecruitmentBurdenPolicy`**，至少提供 `GetDailyRecovery(Settlement)`；由 ModifiedArmy 的已加载 `MilitaryConfig` 实现/适配，在 GameStart 配置加载完成后注册，每次开新 Campaign 清理旧注册。ModifiedPolitics 每日恢复通过该 Army 定义的契约取文化值，不复制七种文化成本表到 Politics，不让 Politics 读取 Army 内部 XML 细节。

### 2.2 Politics 的唯一状态所有者

在 `SettlementPopulationBehavior` 新增：

```csharp
private Dictionary<Settlement, float> _recruitmentBurden = new();
```

持久化键建议 `ModifiedPolitics.SettlementPopulation.RecruitmentBurden.v1`，只用于城镇/城堡。旧存档缺失时字典为空，读取视为 0，**不清空原有 Hearth**。墙级升级、领主变更、王国变更、存档加载都不重置该负担。

`SettlementPopulationSaveDefiner` 已注册 `Dictionary<Settlement,float>`（Hearth 字典），新负担字典使用同一容器类型，**不要重复 ConstructContainerDefinition 相同类型**；仅在确有新类型时追加新的定义，禁止修改 `2026110701` 和历史类型 ID `1`。

### 2.3 对现有事务记录的向后兼容

保留 `PopulationTransactionRecord` 的 `SaveableProperty(1)..(5)`，在后续未使用的编号增加：事务类别（历史记录缺省视为 `LegacyHearth`）、征兵负担增加量、负担 Before/After、提交游戏日等必要字段；不得改旧 `Amount` 的正负语义，不能把历史 `TryConsume/TryRestore` 记录解释为新增职业兵事务。

`PopulationTransactionResult` 可追加本次 Hearth/负担 Before/After 和实际批准数量供调用方、日志使用，原状态枚举含义不变。

## 3. 核心算法与原子事务

### 3.1 招募前能负担多少人

定义 `capacity = H - B`（不要提前 `max(0,...)`），对单兵负担 `c > 0`、请求 `requested`：

- 若 `capacity < 1`：`maxCount = 0`。
- 否则：依次按一次招募的 `H -= 1; B += c` 检查能否再征下一人。可以等价计算：

```text
maxCount = min(requested, 1 + floor((capacity - 1) / (1 + c)))
```

浮点边界使用统一容差并由同一纯算法复核。`count`、`c` 的乘法必须检查整数溢出；H、B 始终为非负有限数。**此公式只决定职业兵实际招募可承担数量，不代表候选训练产量。**

示例：`H=1500, B=1499, c=14 (帝国T4)`，可招 1 人：后 `H=1499, B=1513, capacity=-14`，再招受阻。

### 3.2 完整业务事务

`TryRecruitProfessional` 必须先以同一个状态快照验证整笔已裁剪请求，然后提交：

1. 前置验证定居点类型、Culture/配置、军队 Tier、人数、H/B 有效、`RecoveryRequired==false`、事务 ID 非空且从未提交。
2. 使用模拟逐人征召算法确认本笔 `count` 全部允许；不允许在验证后按照 `count*c` 无检查盲扣。
3. 快照 `HBefore, BBefore`，计算 `HAfter=HBefore-count`, `BAfter=BBefore+count*c`。
4. 在唯一拥有者 `SettlementPopulationBehavior` 中一次完成两项资源变动并登记同一事务记录；不得在 Army 和 Politics 分别持久化扣款。
5. 任何中途异常都按快照回滚两项资源和事务状态。补偿不成功置 `RecoveryRequired`、记录结构化错误并阻止该定居点进一步职业招募。
6. 返回已提交事务 ID；重复 OperationId 必须返回可辨认的 **非新提交** 状态/原因，绝不让调用者再次写入兵员。
7. 回滚由同一个 `TryCompensate` 实现，根据新事务中持久化的 `burdenDelta` 做反向变更；正常同日回滚不能强制重置成旧快照（可能覆盖其他合法事务）。

**跨日补偿警戒：** 每日恢复会改变全局 B，因此不能在跨日后盲目减去整笔历史新增负担并吞掉其他事务形成的负担。新事务记录应保存 `CommittedDay`；若无法证明跨日补偿的准确剩余额度，则拒绝自动猜测、置 RecoveryRequired 并输出可修复证据。正常驻军事务应在同一调用链内即时回滚。

### 3.3 每日结算

在 `SettlementPopulationBehavior.OnDailyTick` 现有 `_lastDailyTick` 机制中集成：

```text
NaturalHearthGrowth = 原有 PopulationGrowthModel.Calculate(...)
H' = max(0, H + NaturalHearthGrowth)
B' = max(0, B - GetDailyRecovery(settlement.Culture))
```

恢复 **每个城镇或城堡每日 2**，新建负担为 0 时不允许负数；使用现有 `lastDailyTick` 判定单日一次，注意更新 H、B、dayStamp 的异常原子性及与招募事件顺序。读档不能多恢复一次；缺少政策不可擅自使用默认值而造成文化规则绕过。恢复异常应记录并冻结危险操作，不制造负数/NaN。

建议把当日自然增长、负担恢复放在单一 `EnsureDailyPopulationUpdate(settlement)`（或等效内部方法），从每日事件及需要强一致性的征兵提交入口复用，避免事件订阅先后顺序改变招募额度。保持现有日志汇总机制，新增 before/after 与 `day`。

## 4. ModifiedArmy 招募执行接线

### 4.1 文化配置与征兵服务

- `CulturalMilitaryDoctrine` 增加独立的 `ProfessionalBurdenCosts`、`ProfessionalBurdenDailyRecovery`（具体类名可沿用 `Professional` 配置模型），调用时使用 `GetDoctrine(settlement)`，绝不使用 `GetDoctrine(party.LeaderHero.Culture)`。
- `FiefPartyManager.TryConsumeProfessionalHearth()` 应更名或提供兼容的内部调用封装，如 `TryRecruitProfessionalPopulation(...)`，调用新增原子事务而非旧 `TryConsume`；保留对外现有签名兼容或一并完成项目内引用的受控改名。
- `RollbackProfessionalHearth()` 使用原事务 ID 统一补偿，不能只恢复 Hearth、遗漏 Burden。
- `ProfessionalOfferConsumption.PopulationOperationId` 继续承载唯一操作 ID，不另起重复事务台账。

### 4.2 候选生产 ≠ 实际招募

`SettlementManpowerProductionBehavior.GetProductionForecast()` 当前用 Hearth 算职业兵**生产供给**。保留这一现有基于**实际 Hearth**的训练/产能公式，不得直接替换为 `Hearth-Burden` 触发旧 `<=100` 人口供给阈值；新限制施加在**实际提交招募**和实际可征募预览上。

`GetAvailableProfessionalOffers` 仍表示“今天训练好的候选”；实际能征募数另由 `GetMaxAffordableProfessionalCount` 根据候选 Tier 的不同成本实时计算。每日候选重建不扣人口、也不累计 Burden。

### 4.3 驻军批量招募

`GarrisonRecruitmentExecutionService.ExecuteDetailed` 现有循环决定 `count` 后、调用 `ProfessionalRecruitmentService.TryCommit` 前：

1. 对 `RecruitmentSource.ProfessionalProduction` 再按该候选兵种 `tier/cost` 与实时 H/B 计算 `affordable`；
2. `count = min(count, affordable)`；若 0 则跳过该评估项并记录 `InsufficientRecruitmentCapacity`；
3. 当余额仅够 1 人、计划 4 人时，**实际招募 1 人**，其余候选保持未消费，不允许整笔全部拒绝，也不允许继续透支其他 3 人；
4. 继续走原有 `ProfessionalRecruitmentService.TryCommit` → `TryConsumeProfessionalOffer` → 新人口事务 → 驻军 roster 写入 → 财政提交；
5. 任意来源失败或财政失败，按既有 `ProfessionalRecruitmentService.TryRollback` 恢复候选、驻军、Hearth、Burden，不能重复记入 `AutoRecruitmentExpenses`；
6. 日招募上限、财政上限、编制空间、训练模板、混编权重不变。

`count` 经计算裁剪后，所有成功日志中的批准数、人口扣费数、驻军新增数、预算扣费数必须一致。

### 4.4 原版招募 Harmony 必须专项修复

现有 Patch 位于 `ModifiedArmy/ModifiedArmy/Population/Patches/NativeRecruitmentPopulationPatch.cs`，TargetMethod 为 `RecruitmentCampaignBehavior.ApplyInternal`。仓库自带 `code/RecruitmentCampaignBehavior.cs` 显示：`VolunteerFromIndividual`、`VolunteerFromIndividualToGarrison`、`MercenaryFromTavern`、`VolunteerFromMap` 等 detail 分支。**动手前用真实运行中的 TaleWorlds DLL 核对签名、版本和 Harmony 兼容性。**

建议处理方式：

1. 仅对能够确认**本地来源**的职业/志愿兵征募收费。城镇、城堡来源使用新的 Hearth+Burden 原子事务；村庄来源沿用原版/现有 Hearth 消耗规则，不虚构村庄 Burden。
2. 在 Prefix 前置验证并**预留/提交人口事务**，只有成功才允许原版改变金钱、志愿兵槽位和 roster。用 `__state` 保存 Settlement、Party、Troop、count、原始 roster 数、operationId。
3. 原版正常结束后核对实际新增 roster 数；成功则确认只结算一次。原版未执行、被其他 Patch 跳过或没有真实新增士兵时，必须补偿人口事务。
4. 原版抛异常时，用经验证的 Harmony Finalizer/等效机制补偿人口；需承认原版已部分改变金钱/槽位的异常路径可能还需要额外恢复，不能把人口回滚冒称为整个原版事务回滚成功。
5. Tavern mercenary、地图来源为空、事件生成/俘虏转化等路径必须**逐一归类并记录来源**。不能擅自对雇佣兵扣本地城镇 Hearth，也不能假定 `settlement==null` 就代表可以无限新增本地职业兵。对无可证明来源的分支保留明确诊断和测试清单；若被认定属于本地职业兵但无法安全扣费，应在资源变更前拒绝而非事后无成本放行。
6. 避免全局 Patch `TroopRoster.AddToCounts`：这会错误地为驻军调拨、战俘、救援和部队转移重复收费。

**必须检查** `ApplyInternal` 以外的真实新兵生成路径，建立“入口 → 来源 → 是否产生新人口 → 是否应收费 → 是否已有事务”的审计表。不改变封邑兵召集、非职业俘虏转换和普通军队调拨。

## 5. 经济模型、显示与日志

### 5.1 经济有效劳动力

`SettlementPopulationBehavior.GetEffectiveHearth` 作为唯一公式实现：

```text
H = max(0, GetHearth(settlement))
B = max(0, GetRecruitmentBurden(settlement))
EffectiveHearth = H - 0.50 * min(H, B)
```

`SettlementEconomyProsperityModel.CalculateProsperityChange` 把原先 `service.GetHearth(settlement)` 改为 `service.GetEffectiveHearth(settlement)`；保留 `TownPerHearth=5`、`CastlePerHearth=3`、`ExcessDecayDays=84` 以及当前正增长约束/超额衰退逻辑。**不在招募事务中直接写 `Town.Prosperity`**。村庄的 Hearth 自然增长、农产品、粮食消耗及封邑供养逻辑不修改。

### 5.2 军事管理 UI

在 `FiefMenuBehavior.InitializeReinforcementRateMenu` 的现有“职业兵训练/补员”说明中追加：

- 实际 Hearth（H）
- 征兵负担（B）
- 征兵余额（带符号的 H−B）与可用征兵额度（UI 截断至 ≥0）
- 每日负担消退量（来自定居点文化）
- 经济有效 Hearth
- 当前支持繁荣度与实际 Prosperity（明确区别）
- 本地职业兵的候选/训练额度与**当前实际能招募的数量**分别展示

复用语言系统的基准英文文本和 `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml` 翻译，保证占位符匹配。不要在 C# 硬编码面向玩家的中文；无需增改不相关界面。

### 5.3 日志

成功或失败时应可还原：

```text
[ProfessionalRecruitment] settlementName=... settlementId=...
 culture=... troop=... tier=... requested=... approved=... committed=...
 hearthBefore=... hearthAfter=... burdenBefore=... burdenAfter=...
 capacityBefore=... capacityAfter=... operationId=... reason=...
```

每日摘要追加：人口自然增长、当日新增职业兵数量、当日新增负担、负担恢复 2、BBefore/BAfter、有效 Hearth、支持繁荣度。Routine Debug/Info 走玩家王国日志过滤；按结算日汇总避免刷屏；拒绝按 `(settlement,reason,day)` 限频；恢复失败、非法成本、事务失配 `Error` 全局记录。

## 6. 可执行施工阶段与停工门禁

### Phase 0 — 环境与架构基线（只读）

1. `git branch --show-current`、`git rev-parse HEAD`、`git status --short`；不得覆盖未提交改动。
2. 阅读 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`，如另有 Politics 适用规则也读取。
3. 核对本文相关文件与当前 HEAD 的差异、真实 DLL Harmony 目标和 SaveDefiner。
4. 列出每一个新增兵种入口、谁写 roster、谁提交人口和财政、谁回滚。
5. **门禁：** 如资源修改路径无法确定，先报告 `BLOCKED`，不得猜测实现。

### Phase 1 — XML 与纯规则模型

修改 `MilitaryConfig.cs`、`militaryConfig.xml`，添加每文化 T1-T6 成本、每日恢复配置；新增纯计算方法（不直接修改 Campaign state）；新增只读策略契约/注册；单独测试文化排序、读取、溢出、缺失、重复 Tier、0/负数拒绝。**门禁：XML 加载通过，原 FeudalSupply 完全不变。**

### Phase 2 — Politics 人口负担与事务基础

修改 `PopulationContracts.cs`、`SettlementPopulationBehavior.cs`、`SettlementPopulationSaveDefiner.cs`（只做必要扩展），增加 B 持久化、单日恢复、专用职业事务、旧记录兼容、完整补偿、异常冻结。测试新旧存档、同日多笔、重复 ID、跨日补偿、超额负担、负数/NaN 防御。**门禁：H/B/事务记录一致；未通过不得接入真实招募。**

### Phase 3 — 真实职业兵招募接入

按调用顺序修改 `FiefPartyManager`、`SettlementManpowerProductionBehavior`、`GarrisonRecruitmentExecutionService`、`ProfessionalRecruitmentService`；把余额限制插在真正写 roster 之前。核对 `GarrisonRecruitFromPrisonersBehavior` 的调度与上限，不改变它对俘虏、财政和训练的既有逻辑。新增 1/2/4 人边界测试、资金失败回滚和驻军软硬上限测试。**门禁：四项数量一致：roster 新增、候选消耗、人口损失、负担累计。**

### Phase 4 — 原版路径及交易异常

只在核实原版运行签名之后修改 `NativeRecruitmentPopulationPatch`；分类处理城镇志愿兵、村庄志愿兵、佣兵、地图来源等；完成安全预留与异常补偿。**门禁：不得存在“原版已新增兵员但该来源应扣的人口和负担未扣”的已知路径。若某路径来源不可判定，单列 BLOCKED 并阐明复现前提。**

### Phase 5 — 繁荣度、展示与日志

修改 `SettlementEconomyProsperityModel`、`FiefMenuBehavior`、语言文件和相应诊断。保留 84 天的衰减模型，不直接扣 Prosperity。**门禁：经济模型、UI、真实事务都读取同一份 B。**

### Phase 6 — 构建与游戏验收

仓库已有 `ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1`，该脚本会对 `ModifiedArmy/ModifiedArmy.sln` 和 `ModifiedPolitics/ModifiedPolitics.sln` 执行 Release 编译，并检查模块依赖等项。推荐在 Windows 仓库根目录执行：

```powershell
pwsh -NoProfile -File .\ModifiedArmy\tools\Test-ArchitectureReleaseGate.ps1
pwsh -NoProfile -File .\ModifiedArmy\tools\Invoke-ReleaseValidation.ps1
```

注意脚本依赖本地游戏程序集、Git 与运行证据环境；未满足时应报告 `BLOCKED`，不能伪称 PASS。完整 T01-T24 仍需游戏存档证据。最终还应执行 `git diff --check`、`git diff --stat` 与对资源变更路径的人工复核。

## 7. 必须覆盖的验收矩阵

| ID | 场景 | 预期 |
| --- | --- | --- |
| RB01 | 旧存档有 Hearth，无 B | 保留 Hearth，B=0，无重置人口 |
| RB02 | 新档初始载入 | 城镇/城堡 B=0，村庄无职业负担 |
| RB03 | 帝国 T4：H=1500,B=1499，招 1 | 成功；H=1499,B=1513,余额=-14 |
| RB04 | 同上请求 4 | 实际只批准并招募 1，3 人不扣费、可保留候选 |
| RB05 | 余额 H−B≤0 | 不允许新职业兵招募；原版不得绕过 |
| RB06 | B>H | 可显示负余额，经济影响最多使有效劳动力降至 H×50% |
| RB07 | 每日恢复 | B 减 2，B 不为负；读档/同日重复事件不重复减 |
| RB08 | 城镇 T4 成本 | 帝国14、阿塞莱17、瓦兰迪亚25、库赛特/斯特吉亚33、巴旦尼亚/诺德40 |
| RB09 | 城堡招募 | 同文化、Tier 成本，使用城堡自身 Hearth/B；经济倍率为3 |
| RB10 | 部队调拨、升级、死亡 | 无重复扣 H/B，无死亡时反向返还 |
| RB11 | 驻军新增兵员失败 | 撤销人口+负担、候选及 roster，不泄露预算 |
| RB12 | 同 OperationId 重放 | 明确拒绝为第二笔新事务，无二次 roster 新增 |
| RB13 | 事务途中异常 | 恢复原值；补偿失败进入 RecoveryRequired 且有日志 |
| RB14 | 原版志愿兵、到驻军志愿兵 | 成功扣对应来源，失败无单方面产生士兵 |
| RB15 | 村庄志愿兵、雇佣兵、战俘转换 | 按来源审计，不被错误计成城市职业兵 |
| RB16 | 经济：H=3000,B=2000 | EffectiveHearth=2000，城镇支持繁荣度=10000 |
| RB17 | 经济：H=3000,B=3500 | EffectiveHearth=1500，城镇支持繁荣度=7500 |
| RB18 | 繁荣度支撑高于/低于实际值 | 保留原正增长约束和84天超额衰退；招募无直接 Prosperity 写入 |
| RB19 | 改变领主与王国文化 | 本地定居点文化成本不变，B 不清零 |
| RB20 | 玩家/AI 等价招募 | 同定居点、兵种、条件下同成本，无 AI 免费豁免 |
| RB21 | 停战与继续征兵 20/84/168 天 | B 累积/消退、人口与繁荣度符合逐日公式，不重复 Tick |
| RB22 | 现有封邑兵返乡及安置 | `FeudalSupply`、`MobilizedHearth`、返乡台账完全不变 |
| RB23 | 已有 SaveDefiner 类/ID 与旧交易 | 旧事务仍可查询/按旧语义补偿，新字段不破坏反序列化 |
| RB24 | 两模块 Release + 架构门禁 | 无 Army→Politics 反向依赖、0 编译错误、诊断无洪水 |

## 8. 完成判据与 Codex 最终汇报格式

Codex 完成后必须给出：

1. 实际起点 HEAD、最终 HEAD、工作区未提交文件与差异概述。
2. 逐文件改动列表；新旧事务 API 和数据归属说明；新旧存档迁移说明。
3. 被审计的职业兵/原版招募入口清单、费用规则、真实 roster 写入位置、补偿入口。
4. `RB01–RB24` 每项 `PASS / FAIL / BLOCKED` 及证据，不能用编译通过替代游戏内证据。
5. Release 编译与仓库架构门禁的实际运行命令、退出码、关键输出。
6. 至少一组城镇的实际游戏日志：20 天 Hearth/B/Capacity/EffectiveHearth/ProsperitySupport/实际补员数。
7. 如果仍有来源不明的 native 招募路径或跨日补偿难以证实时，单列风险及可复现步骤；禁止静默绕过。
8. 不主动 `commit`/`push`，除非用户再次明确要求。

**禁止的“看似完成”做法：** 用静态公式替换全部 `GetRecruitableHearth`、把文化封邑兵 `FeudalSupply` 直接当职业兵成本、仅在 Postfix 扣负担、招募前只检查一整批一次、允许半笔 H/B 事务、无证据清理老事务或解除冻结、让 AI 获得豁免、借本任务重构封邑兵安置系统。

---

## 9. 施工参考源码（固定基线链接）

- [Army 人口服务契约](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs)
- [Politics 人口 Behavior](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs)
- [Politics 存档 Definer](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Persistence/SettlementPopulationSaveDefiner.cs)
- [Politics 繁荣度模型](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs)
- [Army 文化军事 XML 配置](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
- [Army 军事配置解析](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [Army 职业候选与人口供给](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [Army 驻军招募执行](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs)
- [Army 职业招募服务](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Recruitment/Services/ProfessionalRecruitmentService.cs)
- [Army 原版征兵 Patch](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Population/Patches/NativeRecruitmentPopulationPatch.cs)
- [仓库原版招募源](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/code/RecruitmentCampaignBehavior.cs)
- [Army 补员菜单 UI](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs)
- [Army 架构发布门禁](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1)
