# ModifiedArmy 驻军编制、AI Party 征调与封邑兵日志修正设计（Worker 施工版）

> 版本：v1.0｜2026-11-06｜状态：用户已确认设计，尚未施工  
> 仓库：`GuihuaZhou/bannerlord`｜核对基线：`main` / `846dbea8679d93e7b459a88f33defec9b0fc756c`（2026-11-05 16:17:46 UTC）  
> 目标模块：`ModifiedArmy`；不得把本设计误认为已提交/部署的代码。  
> 权威顺序：当前源码与实际 Git 状态、用户本轮明确决定、适用的 `AGENTS.md`；历史文档仅作参考。

## 0. 本次设计结论（一页速览）

1. **驻军自动征兵目标上限**：内陆驻军 **75%**，边境驻军 **85%**。这是驻军自己的目标，不是 AI 领主 Party 编制目标。
2. **驻军兵力警戒下限**：内陆 **50%**，边境 **75%**。用于判断跨城驻军增援需求、来源驻军富余；不是 AI Party 从驻军征调时的留守线。
3. **绝对驻军留守底线：75 人**。正常从驻军抽兵，若当前驻军不超过 75 人，一律不得调出；调出后实际在城人数不得低于 75 人。
4. **AI Party 驻军征调**：使用 75 人硬底线，还受到原有封邑兵保护、招募资格、Party 容量与财政审批等限制。
5. **驻军跨城增援**：来源驻军至少留下 `max(75, 驻军警戒下限)` 人，且只调出实际可调出的合格普通健康士兵。计划审批和真正从名册扣兵时都需校验。
6. **征粮护卫抽调**：单独专题。本轮不得顺手重构；现有路径可能绕过 75 人底线，应登记为明确未覆盖风险，不能宣称“全路径均已修复”。
7. **封邑兵**：每日产能仍由所属城镇/城堡统筹，**实际士兵归属与供养校验按各附属村庄分别完成**；仅修正村庄级生产诊断及“产出零却提示成功”的日志问题。
8. **驻军绑定**：一支领主 Party 绑定一个定居点、一个定居点至多绑定一个 Party 的唯一性规则严格保留。
9. **默认不修改**：军事制度权重、职业兵训练速率、兵种模板、招募概率、AI Party 目标人数、封邑供养公式、征粮队运输状态机及存档结构。

## 1. 命名与职责边界（禁止混用“补员”一词）

| 术语 | 行为主体 | 动作 | 对应限制/目标 |
|---|---|---|---|
| **驻军自动征兵**（Garrison Auto Recruitment） | 城镇/城堡驻军 | 职业兵入驻、许可俘虏入驻等自动增加驻军 | 内陆 75%、边境 85% 的**上限目标**；同时受硬编制容量、经济预算等约束 |
| **AI Party 驻军征调**（AI Party Garrison Withdrawal） | AI 领主野战 Party | 从本 Clan 允许的驻军取得普通士兵 | **调出后驻军至少 75 人**；不以 50%/75% 警戒线作为 Party 征调门槛 |
| **驻军跨城增援**（Garrison-to-Garrison Reinforcement） | 两座同 Clan 驻军及其运输队 | 从来源驻军调人，送到目标驻军 | **来源至少保留 max(75, 警戒下限)**；目的地受接收容量约束 |
| **驻军兵力警戒下限**（Garrison Lower Threshold） | 防务评估/跨城增援 | 需求评估、来源富余判断 | 内陆 50%、边境 75%，**不是所有驻军抽兵的统一地板** |
| **征粮护卫抽调**（Supply Escort Withdrawal） | 征粮队 | 临时从驻军抽取护卫 | 另案整改；现状风险必须公开 |
| **封邑兵每日生产**（Village Feudal Production） | 所属城/堡的每日配额 + 各附属村庄名册 | 按村庄预算实际生成就绪封邑兵 | `VillageFiefData` 独立供养校验；不得用父级合计超额直接否决每个村庄 |

**日志/代码注释用语要求**：必须明确写出是“驻军自动征兵目标”“驻军警戒下限”“AI Party 驻军征调”“驻军跨城增援”中的哪一种。禁止笼统写“最低防御要求”“最低补员要求”“补员上限”而不说明主体。

## 2. 数值与取整规范

| 参数 | 内陆 | 边境 | 配置字段 |
|---|---:|---:|---|
| 驻军自动征兵上限目标 | 0.75 | 0.85 | `InteriorGarrisonUpperRatio` / `BorderGarrisonUpperRatio` |
| 驻军兵力警戒下限 | 0.50 | 0.75 | `InteriorGarrisonLowerRatio` / `BorderGarrisonLowerRatio` |
| 驻军绝对留守底线 | 75 人 | 75 人 | 新增统一代码常量 `MinimumResidentGarrisonCount`；此次**不新增 XML 字段** |

沿用已存在的取整规则：

```csharp
UpperTarget = Math.Max(0, (int)Math.Floor(Math.Max(0, hardLimit) * upperRatio));
LowerTarget = Math.Max(0, (int)Math.Ceiling(Math.Max(0, hardLimit) * lowerRatio));
```

驻军绝对留守底线应用于**实际驻扎在城内**的驻军人数（现有 `GarrisonDefenseSnapshot.ResidentCount` 来自 `GarrisonParty.Party.NumberOfAllMembers`）；**不得**把外出征粮队护卫预留人数加回实际留守数。当前口径包括名册中的伤兵等成员：本轮保持现有计数语义，不擅自将 75 人替换为“75 名健康士兵”。如果今后希望改变计数口径，须另立需求。

**示例**（PartySizeLimit = 200）：

| 判定 | 内陆 | 边境 |
|---|---:|---:|
| 驻军自动征兵上限目标 | 150 | 170 |
| 警戒下限 | 100 | 150 |
| AI Party 征调后须保留 | 75 | 75 |
| 跨城驻军增援后须保留 | 100 | 150 |

**边界测试**：若 `hardLimit=234` 且为边境，则警戒下限 `ceil(234*0.75)=176`，自动征兵目标 `floor(234*0.85)=198`。不得沿用旧值 222（95%）。若 `hardLimit<75`，常规驻军调出应为 **0**，不得因 `min(75, hardLimit)` 将硬底线缩小；自动征兵仍依据合法的目标/容量计算。

## 3. 已核实的当前代码关系

| 位置 | 现有事实 | 本轮策略 |
|---|---|---|
| `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs` | `GetUpperTarget` 向下取整，`GetLowerTarget` 向上取整；`GetSoftRecruitmentFreeSlots` 与 `GetTransferFreeSlots` 使用上限目标；含默认上限 80/95 | 更新默认 75/85；新增唯一 75 人常量；不改前述取整与容量逻辑 |
| `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` | `ForceManagementConfig` 的上限默认值是 .80/.95；下限 .50/.75，解析时校验下限不能高于上限 | 同步更新上限默认值 .75/.85；下限保持 |
| `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` | 现行配置上限 .80/.95，下限 .50/.75；注释称“下限约束驻军抽调”过于笼统 | 改上限；重写注释说明跨城与 AI Party 征调不同 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/ClanGarrisonRecruitmentCoordinator.cs` | 驻军数低于 75 的优先征兵判定含字面量 `75` | 改引用统一常量；保持优先排序 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs` | 实际执行时同时检查硬容量、自动征兵软容量与预算 | 不改资源写入；校验新比例确实生效 |
| `ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs` | `GarrisonDefenseSnapshot.MinimumCount = GetLowerTarget(...)`，外部增援需求以该值判断；`GetSafeTransferOutCount` 目前以 `min(75, hardLimit)` 为留守底线 | 不更名 `MinimumCount`，补充明确注释；修为**固定 75**，不能用 `hardLimit` 将其压低 |
| `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonTransferPolicyModel.cs` | 领主从驻军取得士兵的入口使用 `GetSafeTransferOutCount` 并排除在役封邑兵 | 保留现有角色与保护；验收 75 边界 |
| `ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs` + `Services/PartyExchangeService.cs` | AI Party 交流计划与执行复核都使用 `GetSafeTransferOutCount` | 保留计划与提交双重限制，不增并行账本 |
| `ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs` | 驻军作为 AI Party 征兵候选时显示 `MinimumCount` 和可调数，容易被误读为按警戒下限调兵 | 日志同时展示警戒线和 75 人硬底线 |
| `ModifiedArmy/ModifiedArmy/ForceManagement/Transport/ReinforcementDispatchModel.cs` | 来源安全数量现为 `ResidentCount - MinimumCount` | 改为 `ResidentCount - max(75, MinimumCount)`，仍排除受保护兵和伤员 |
| `ModifiedArmy/ModifiedArmy/ForceManagement/Transport/ReinforcementTransferService.cs` | `TryCreate` 使用先前计划直接开始抽取来源名册，缺少明确的**临扣兵复核** | 在第一次写入前重新校验来源富余；不足则拒绝且不改变名册，保留原有失败回滚 |
| `ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/ForceManagementBehavior.cs` | 防务日志将警戒线称作“至少应保留”，并使用类似“已请求援兵”的措辞 | 改为“外部增援需求评估”，明确自动运输是否启用；保持稳定状态变化节流 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs` | 村庄民兵名册为就绪封邑兵来源，`CanIncreaseSupply` 对村庄独立供养和预算检查 | 不改运算和写入 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs` | 生产结果已有 `Village/Troop/Count`；父级聚合日志会在产量为零时输出“补充了” | 只调整日志：非零成功、逐村产出及预算明细、零产出诊断 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | 以城/堡产生每日封邑兵配额，再通过 `ProduceDailyFiefTroops` 写至村庄 | 保持每日配额计算与生产路由 |

**特别注意**：`GarrisonDefenseSnapshot.MinimumCount` 还是既有调用点的契约，征粮队也引用它。本轮**不重命名该属性**；用注释、日志字段和专门的留守底线常量澄清含义，而非进行跨领域机械重构。

## 4. Worker 逐项施工任务

### G-01｜驻军目标参数与默认值

修改文件：

- `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`
- `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`
- `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs`

要求：

1. XML 内 `InteriorGarrisonUpperRatio` 改 `0.75`，`BorderGarrisonUpperRatio` 改 `0.85`；两个 LowerRatio 分别保持 `0.50`、`0.75`。
2. `ForceManagementConfig` 上限属性初始化默认改成 `0.75f`、`0.85f`。
3. `GarrisonRecruitmentCapacity.DefaultInteriorUpperPercentage = 75`，`DefaultBorderUpperPercentage = 85`，确保缺失/不可用配置时的后备目标一致。
4. `GetUpperTarget` 的向下取整、`GetLowerTarget` 的向上取整、边境判定 `IsEnemyBorderFortification` 保持不变。
5. `GetTransferFreeSlots` 现有正常接收容量仍使用上限目标；不得误改 `GetHardFreeSlots` 的硬上限语义。不得改 AI Party 人数目标、自然驻军变化公式或封邑兵生产速率。

### G-02｜统一 AI Party 驻军征调 75 人硬底线

修改文件：

- `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/ClanGarrisonRecruitmentCoordinator.cs`

建议唯一常量：

```csharp
public const int MinimumResidentGarrisonCount = 75;
```

模型保留现有 `GetSafeTransferOutCount(GarrisonDefenseSnapshot snapshot, TroopRoster roster)` 签名，以真实驻军 `ResidentCount` 判定：

```csharp
int residentFloor = GarrisonRecruitmentCapacity.MinimumResidentGarrisonCount;
int residentSurplus = Math.Max(0, snapshot.ResidentCount - residentFloor);
return Math.Min(residentSurplus, Math.Max(0, roster.TotalRegulars));
```

**行为不变量**：`ResidentCount <= 75 => approved 0`；`ResidentCount > 75 => approved <= ResidentCount - 75`。其余在役封邑兵保护、Party 招募审批和计划/执行时复核，继续在原有调用链进行。

`ClanGarrisonRecruitmentCoordinator` 对低于 75 人驻军的优先排序改用该常量，不修改排序逻辑。不得将 `GetLowerTarget` 替换成这个常量，也不得把硬底线改成可配百分比。

### G-03｜跨城驻军增援：来源需同时满足警戒线和绝对线

修改文件：

- `ModifiedArmy/ModifiedArmy/ForceManagement/Transport/ReinforcementDispatchModel.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Transport/ReinforcementTransferService.cs`

计划阶段 `GetSafeOrdinaryHealthyCount(source)`：

```csharp
int requiredReserve = Math.Max(
    GarrisonRecruitmentCapacity.MinimumResidentGarrisonCount,
    defense.MinimumCount);
int safeByCount = Math.Max(0, defense.ResidentCount - requiredReserve);
// 继续与「合格普通健康士兵」可用数量取 min。
```

保留 `ReinforcementDispatchModel.MinimumPartySize=10`、`MaximumPartySize=50`、原有同 Clan 与路线安全要求、来源兵种筛选。这里使用的警戒线来自**来源驻军当前**防务快照，而不是目的地。

执行阶段 `ReinforcementTransferService.TryCreate(plan)`：

1. 完成现有的计划、所有权、来源/目的地合法性检查后，**在 `ExtractHealthyOrdinaryTroops` 第一次从实际名册扣兵前**重新读取来源驻军快照并计算 `GetSafeOrdinaryHealthyCount(plan.Source)`。
2. 若复核可调人数 `< plan.TroopCount`，直接拒绝本次既定计划；建议稳定原因代码 `SourceReserveChanged`；此时**不得发生任何名册写入、创建运输队或动用资源**。不隐式缩小计划中的兵数、不得绕过最低运输批量。
3. 仍按既有代码抽取健康普通兵，保护在役封邑兵身份、保留伤员与相关事务回滚。若抽取未达计划量，则按原流程全部退回。
4. 复核必须基于当前 `ResidentCount`；不要因为既往计划成功或征粮护卫“将来归还”就视为可再调一次。
5. 检查当期边境身份变化、名册成员变化、多个增援计划先后执行等场景。若执行可能触发异步/回调资源变化，应检验实际名册扣除完成后的留守人数，失败要通过原有回滚路径恢复；不能静默丢兵。
6. 本次不改变跨城增援自动派遣开关，现行 `EnableAutomaticReinforcement=false` 继续保持。

这里的“跨城增援”是驻军人员运输，**不等同于征粮队/粮食运输**。不得为了复用 75 规则而顺手改 `SupplyPartyBehavior` 或 Supply Cargo 状态机。

### G-04｜驻军日志及 XML 语义改正

修改文件：

- `ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/ForceManagementBehavior.cs`
- `ModifiedArmy/ModifiedArmy/ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs`
- `ModifiedArmy/ModifiedArmy/ModuleData/submodule_strings.xml`
- `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml`
- `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`（仅注释）

日志按主体分离：

- `GarrisonAutoRecruitment`：显示自动征兵目标 `UpperTarget`，不要称之为 AI Party 目标。
- `GarrisonExternalReinforcementAssessment`：`ResidentCount`、`EffectiveGarrisonCount`、`LowerTarget`、`UpperTarget`、是否属于边境、预测本地征兵、外部增援需求数、ReasonCode、自动派遣开关。开关关闭时写**“需要外部增援（仅评估，未自动派遣）”**，不得写“已派出援兵”。
- `AiPartyGarrisonWithdrawalCandidate`：在现有候选诊断中同时输出 `MINIMUM`（驻军警戒下限）、`HARD_FLOOR=75`、`SAFE`（按真实策略可调出）以及封邑兵保护、普通兵候选数和招募审批数；明确 `SAFE` 不依赖 `MINIMUM` 直接计算。

现有 `str_modifiedarmy_garrison_reinforcement_requested` / `str_modifiedarmy_garrison_reinforcement_not_needed` 和对应 `{=...}` 翻译键尽量保留，必要时增加占位符，不随意删除旧 ID。`str_modifiedarmy_force_diag_garrison_candidate` 原始英文位置当前实际是中文；本轮既要改它，必须将基础文本修为英文、在 CN 本地化文件增加对应中文翻译。所有新格式均维护占位符一致性。

XML 说明修改为：“上下限约束驻军自身的自动征兵与普通接收；50%/75% 下限用于跨城驻军外援需求/来源保护；AI Party 从驻军征调的绝对留守底线为 75 人。”不得再笼统写“下限约束所有驻军抽调”。

**日志安全**：复用已有玩家王国过滤、状态变化去重、`Debug/Info` 级别；不得每 tick 对全部地图驻军输出详细记录。日志构建只读，不得为打印日志再次计算会随机决策或修改资源的操作。

### F-01｜封邑兵真实归属：保留村庄权威，不改生产事务

只读核对，不改变下列行为：

- 生产入口 `SettlementManpowerProductionBehavior` 的城/堡每日封邑兵配额；
- `FiefPartyData.ProduceDailyFiefTroops` / `PerformReinforcement` 对村庄的选择顺序；
- `VillageFiefData.GetReadyRoster()` 返回村庄民兵 Party 的真实名册；
- `VillageFiefData.CanIncreaseSupply(template, doctrine, troop, count)` 同时检查村庄自由供养及封建供养预算；
- `VillageFiefData.ServiceBatches`、`ReturnBatches`、`MobilizedHearth`、`RecoveryRequired`；
- 已经存在的 `FiefTroopProductionResult(Village, Troop, Count)`；
- `_fiefParty` 不得成为新的可写汇总士兵池。

某村庄供养超额，不代表同一城/堡下其他村庄不能继续生产。父级聚合 `总占用/总预算` 是展示信息，**不是新的生产审批条件**。

### F-02｜封邑每日生产：仅修正村庄明细日志与空成功日志

修改文件：

- `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`
- 上述本地化 XML 两份文件。

#### 数据获取

从 `OrderedVillages()` 获取当前城/堡全部附属村庄，对每个村庄生成只读日志快照：

- `VillageSettlement.StringId` 与显示名称；
- `GetEffectiveSupplyHearth()`；
- `GetFeudalBudgetHearth(doctrine)`；
- `GetOccupiedSupplyHearth(_fiefPartyTemplate, doctrine)`；
- `GetFeudalBudgetRemainingHearth(_fiefPartyTemplate, doctrine)`（**保留负数**）；
- `RecoveryRequired`；
- 使用本次 `FiefTroopProductionResult` 按 `(Village, Troop)` 聚合实际新增数量。

必要的“新增前/新增后供养”必须分别在变更前、变更后取得，不能以变更后的单一快照臆测 `before`。为避免给日志引入新的失败点：只有当当前玩家王国诊断确实需要输出时才采样详细指标；对于村庄无有效名册、无 doctrine、处于恢复冻结等情况，日志记录稳定状态或 `Unavailable`，不得让诊断异常阻断兵员生产。

#### 输出要求

当实际 `produced.Sum(x => x.Count) > 0`，输出**单条封邑生产汇总**，按 `Village.StringId`、`Troop.StringId` 固定排序；其中包含零产出村庄的供养状态，以说明为何父级总预算超额却仍有村庄生产。日志示例（演示数字而非真实计算）：

```text
[Fief production] fief=castle_example produced=2 totalOccupied=255 totalBudget=200;
  village_a produced=0 occupied=160 budget=100 remaining=-60 recovery=false;
  village_b produced=2 [infantry] occupiedBefore=93 occupiedAfter=95 budget=100 remaining=5 recovery=false
```

注意：`GetFeudalBudgetRemainingHearth` 有既定签名，查询时须传入 `_fiefPartyTemplate` 和 `doctrine`。不得凭空假设无参数的 `GetOccupiedSupplyHearth()` 存在于 `VillageFiefData`。

#### 零产出

当 `produced.Count == 0`（或总和为零）时：

- **不再输出“补充了。”成功日志**；
- 在实际生产处理与既有 `SyncManpowerCounters()` 之后，以原返回值正常结束；
- 对 `approvedCount > 0` 且适用过滤条件的情况，可输出 `Debug`，稳定代码如 `NoCommittedTroops`；只有真实可判断才细分 `NoEligibleVillage`、`NoSupplyCapacity` 等原因，不能凭猜测伪造原因；
- 不返还或新增生产进度，不重抽随机，不改变兵种模板或插入第二次 `AddToCounts`；
- `RecoveryRequired` 村庄依然不生产，不因为诊断而自动解除恢复锁。

日志使用英文基础文本和配套中文翻译。玩家自己封邑原有成功生产 `Info` 等级可保留；其他非玩家领地的高频诊断按既有玩家王国范围过滤并降为 `Debug`，杜绝全图刷屏。

## 5. 征粮队：明确隔离、明确风险

独立专题，不在这次修改其源码。已观察到：

- `Garrison/Supply/SupplyPartyBehavior.cs` 中正常征粮队创建会尝试从驻军直接抽取 `EscortSize=50` 护卫；
- 粮食援助护卫另有基于 `ResidentCount - MinimumCount` 的预估；
- 这两类护卫提取与 AI Party 的 `GetSafeTransferOutCount()` 不属于完全同一调用链；
- 因此，**即使本轮所有驻军/AI Party/跨城增援测试通过，仍不能断言“征粮护卫抽调已遵守 75 人硬底线”**。

Worker 只能在交付报告列出此风险和专门待办，不能私自修改征粮队调兵、食物供应、返回或恢复状态机。若真实测试确认征粮队使驻军低于 75，属于**已知范围外问题**，单独提交专题方案。

## 6. 资源与兼容性不变量

1. **一 Party—一定居点唯一绑定**：不修改 `PartySettlementBindingBehavior`，不放松 `SettlementReservationRejected` 的设计条件，也不修改新领主 Party 创建资格。
2. **一个真实名册对应一个权威兵源**：驻军以 `GarrisonParty.MemberRoster` 为权威；村庄就绪封邑兵以村庄实际民兵名册为权威；在役/冷却以现有服务记录为权威。
3. **保持资源守恒**：士兵（含伤员）、封邑服役批次、Hearth、物资、金币、招募预算、回滚与冷却计数不得静默增加或丢失。
4. **不改存档**：`SaveableTypeDefiner` 类/属性 ID、`SyncData` keys、历史 SaveId 8/9、事务记录结构不变；`RecoveryRequired` 冻结语义不变。
5. **不改原生 AI 行军所有权**：不新增持续覆盖原生决策的移动指令；不新增 Harmony 适配器、事件重复订阅、进程级 Campaign 引用。
6. **不改跨模块依赖**：ModifiedArmy 不得对 ModifiedPolitics 新增编译或 SubModule 反向依赖。
7. **模型与服务职责**：政策计算放模型；实际名册写入保持原有服务或既有执行路径；日志为只读诊断，不制造第二份账本。

### 资源操作前必需记录的事务检查表

| 操作 | 原始资源所有者 | 写入前检查 | 失败恢复 |
|---|---|---|---|
| AI Party 驻军征调 | 驻军名册 → AI Party 名册 | 拥有权、Party 所在地、真实驻军人数、75 地板、保护中的封邑兵、招募审批、目标容量 | 沿用 `PartyExchangeService` 的已存在快照验证/回滚，其他入口不得绕开 |
| 驻军跨城增援出发 | 来源驻军名册 → 增援运输 Party 名册 | 与计划一致的同 Clan 关系、真实来源人数、`max(75,警戒下限)`、合格健康兵、运输最小批量 | 不足时零写入拒绝；已提取但建队或装粮失败时按原逻辑退兵/退物资并记录状态 |
| 封邑每日生产 | 某村庄 `GetReadyRoster()` | 当前村庄 `CanIncreaseSupply`，非恢复冻结，剩余预算足够 | 本轮不修改既有生产事务；不得为日志调用新增资源写入 |

## 7. 分阶段实施顺序与门禁

### Phase A：预检（只读）

Worker 在仓库根目录执行：

```powershell
git rev-parse --show-toplevel
git branch --show-current
git rev-parse HEAD
git status --short
```

读取 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`。如果当前 HEAD 不等于文档基线，先重新比对以上涉及的方法与数据流；如已有其他未提交改动，必须隔离，不覆盖、不执行清理重置。核查适用原生 API、存档与 Harmony 边界，不泛读全部仓库。

### Phase B：驻军配置与 AI Party 硬底线（G-01、G-02）

变更后检查四个比例、缺省值、向上/向下取整，验证 `GetSafeTransferOutCount` 边界。禁止提前合并后续独立问题。

### Phase C：跨城增援真实扣兵前复核（G-03）

先写可证明“计划通过但执行时来源剩余人数不足”的回归场景，再在计划点和实际扣兵前置二次守卫；确保失败零资源变更，最小运输数量仍有效。

### Phase D：驻军日志与封邑日志（G-04、F-01、F-02）

保留现有本地化 key，并同步英文、中文文本；村庄日志须表现真实生产归属；禁止重复资源写入。按照需要分别交付，可独立回滚诊断变更。

### Phase E：静态验证与游戏验收

先查 `git diff`，确保无征粮队、Party 绑定、存档 ID 或无关玩法修改。然后运行仓库现有门禁，记录每条 **真实观察到** 的 `PASS`、`FAIL`、`BLOCKED`；编译通过不代表游戏运行验收通过。阻塞的必需门禁不得无授权进入依赖阶段。

建议命令（在 Windows PowerShell 且处于仓库根目录）：

```powershell
git diff --check
git diff --stat
git diff -- ModifiedArmy/ModifiedArmy

.\ModifiedArmy\tools\Test-LocalizationPlaceholders.ps1
.\ModifiedArmy\tools\Test-ArchitectureReleaseGate.ps1
.\ModifiedArmy\tools\Test-RuntimeAcceptanceEvidence.ps1
```

`Test-ArchitectureReleaseGate.ps1` 已包含 ModifiedArmy 和 ModifiedPolitics Release 编译、模块依赖和存档安全检查；`Invoke-ReleaseValidation.ps1` 是综合入口，可依实际验收策略调用。未经确认，不改保护文件 `ModifiedArmy/ARCHITECTURE.md`、`ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`。

## 8. 必须验证的测试矩阵

| ID | 场景 | 必须满足的结果 |
|---|---|---|
| G01 | 内陆 `hardLimit=200` | 驻军自动征兵目标=150，警戒下限=100 |
| G02 | 边境 `hardLimit=200` | 驻军自动征兵目标=170，警戒下限=150 |
| G03 | 边境 `hardLimit=234` | 自动征兵目标=198，警戒下限=176，取整正确 |
| G04 | 驻军人数 50/74/75 | AI Party 征调审批=0，名册无变更 |
| G05 | 驻军人数 80/100/150 | AI Party 征调数分别不超过 5/25/75；其他既有过滤可能进一步减少 |
| G06 | 内陆 `hardLimit=100`、驻军=90 | 警戒线=50，跨城增援实际最多调出 15（而不是 40） |
| G07 | 边境 `hardLimit=200`、驻军=180 | 跨城增援至多调出 30，AI Party 普通征调仍按 75 地板单独计算 |
| G08 | 跨城计划批准后，来源驻军人数下降 | 在第一次扣兵前拒绝旧计划，无扣兵、无建队、无重复消耗 |
| G09 | 出发过程中不足量或建队失败 | 已抽士兵完全回滚，伤员、物品、任务状态正确 |
| G10 | 驻军包含外出护卫预留数 | 调出地板以 `ResidentCount` 判定，不把外出护卫当成实驻人数 |
| G11 | `EnableAutomaticReinforcement=false` | 不自动派遣跨城增援；日志表述为“需求评估”，非已派出 |
| G12 | AI Party 从城内征调与跨城增援日志同时出现 | 能从标签、目标、警戒线、硬底线及 SAFE 看出不同决策含义 |
| F01 | A 村供养超标，B 村尚有预算 | B 村仍可按现有 `CanIncreaseSupply` 生产，日志逐村真实显示；父级汇总超标不阻断 B 村 |
| F02 | 当日批准产能但无实际生产 | 无“补充了”成功日志；可有受控 Debug 原因，兵数不变 |
| F03 | 当日实际生产一部分 | 成功产出数等于各 `(Village,Troop)` 明细之和，名册人数变化一致 |
| F04 | 村庄 `RecoveryRequired=true` | 不因诊断重新生产或解除恢复冻结；其他有效村庄照旧 |
| F05 | 封邑兵在役、冷却或存档重载 | 生命周期记录、供养占用与村庄归属不丢失、不重算产生新兵 |
| C01 | 创建/解散领主 Party | 一个定居点至多一个 Party 的绑定规则保持 |
| C02 | 无玩家王国或其他王国驻军频繁 tick | 无全图日志刷屏；日志仅只读并保留级别过滤 |
| C03 | 本地化及 Release 门禁 | 占位符一致、ModifiedArmy/Politics 均 Release 编译、架构门禁通过 |
| C04 | 游戏 T01–T24 既有验收矩阵 | 依实际日志证据报告；未实测项目保留 `BLOCKED`，不得假报 PASS |
| OUT01 | 征粮队抽取 50 名护卫 | **独立专题，非本轮验收通过项**；保留风险登记 |

### 75 人地板关键差异（用于审阅）

```text
实际驻军 74：AI Party 调出=0；跨城调出=0
实际驻军 75：AI Party 调出=0；跨城调出=0
实际驻军 80：AI Party 最多调出5；跨城还要受 max(75,警戒下限) 限制
内陆警戒线 <75：跨城也必须保留75
边境警戒线 >75：AI Party 普通征调只看75地板；跨城须保留更高的警戒线
```

## 9. 明确禁止的改动

- 放松一 Party 对一定居点的唯一绑定、扩大可绑定据点或自行创建第二支 Party；
- 用警戒线替代 AI Party 的 75 人留守地板，或用 75 人替代跨城增援的较高警戒线；
- 把驻军自动征兵 75%/85% 写入 AI Party 编制人数公式；
- 修改 `GarrisonTransferPolicyModel.InteriorIdealGarrisonRatio`（原生交换理想值模型，和本次参数不是同一职责）；
- 把父级城/堡汇总供养超额当作禁止全部附属村庄生产的条件；
- 引入新封邑兵池、新账本、持久缓存、新存档键或无授权数据迁移；
- 改写征粮队/援粮队的护卫、粮食、派遣、返回或恢复；
- 改变任何兵种模板、军事制度、升级比例、AI 战争倾向、财政与工资免除规则；
- 没有明确授权就 commit、push、开 PR 或重写 Git 历史。

## 10. Worker 交付报告模板

```markdown
# ModifiedArmy 驻军与封邑日志施工交付报告

- 仓库：GuihuaZhou/bannerlord
- 开始前：branch=...，HEAD=...，git status --short=...
- 完成后：HEAD=...，git status --short=...
- 规范：根 AGENTS.md / ModifiedArmy/AGENTS.md / CODE_ORGANIZATION_RULES.md 已读
- 完成任务：G-01...G-04，F-01...F-02（逐项注明路径和实际行为）
- 未修改边界：征粮队、唯一绑定、存档 ID、封邑生产公式、跨模块依赖
- 变更文件：逐文件列出相应差异、原因和回滚影响
- 关键不变量：75 人底线、max(75,警戒线)、AI Party 与跨城增援区别、村庄兵力所有权
- 资源安全：前置条件、实际写入顺序、失败回滚、事件/回调风险、读档兼容
- 验证：git diff --check / 本地化 / ModifiedArmy Release / ModifiedPolitics Release / 架构门禁
- 游戏场景证据：逐项 G01...F05、C01...C04，标注 PASS/FAIL/BLOCKED 并附证据
- 未覆盖已知风险：征粮护卫抽调可能绕开 75 人底线，另立专题
- 最终说明：哪些已验证，哪些只是静态分析/理论正确，哪些实机仍 BLOCKED
```

## 11. 方案结语

本次是**驻军目标比例调整、两类驻军调出边界修正、诊断语义澄清、村庄级封邑生产日志修正**，而不是招募或封邑系统重做。验收关键是：

- 驻军自己的自动征兵目标：**内陆 75%、边境 85%**；
- 驻军防务警戒线：**内陆 50%、边境 75%**；
- AI Party 从驻军征调：**固定至少留 75 人**；
- 驻军跨城增援：**至少留 max(75,驻军警戒线)**，出发前重新复核；
- 封邑兵真实归属：**附属村庄**，父级只统筹当日产能和汇总展示；
- 征粮护卫：**明确隔离，另行分析**。

以上为**经用户确认的设计**；没有对真实 Git 工作区执行修改，也没有生成二进制或游戏内通过证据。
