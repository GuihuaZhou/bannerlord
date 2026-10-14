# ModifiedArmy：共享基础兵源系统实现文档

> **性质**：面向后续编码的实施规格与检查清单；本次**没有修改仓库代码**。  
> **代码基线**：`GuihuaZhou/bannerlord` 默认分支，核对至 [`dadeac409`](https://github.com/GuihuaZhou/bannerlord/commit/dadeac409f231f32044509ecac86b7f054dd682b)。  
> **设计规格**：参阅配套《ModifiedArmy_共享基础兵源_设计方案.md》。

## 1. 当前代码核查结论

### 1.1 `MobilizedHearth` 已推送，必须直接复用

最新 [`FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs) 已包含：

- `[SaveableField(2)] Dictionary<Settlement,int> _mobilizedHearth`：Village 对应的 Settlement → 服役占用；
- `[SaveableField(3)] _mobilizationOwners`：Village → 负责的城镇／城堡；
- `[SaveableField(4)] _mobilizationMigrationComplete`、`[SaveableField(5)] _legacyUntrackedMobilization`：旧档迁移支持；
- `GetMobilizedHearth(Village)`：只读实际 M；
- `TryMobilize(fief,count,hearthPerTroop)`：先验证村庄 Hearth 底线，再均匀扣 Hearth 并增加 M；
- `TryConsumeProfessionalHearth(source,count)`：扣职业兵每人2 Hearth 的既有辅助方法；
- `ReleaseFiefService`／`ReleaseMobilization`：归乡释放 M 并恢复 Hearth，失效仅释放 M；
- `ReconcileMobilization()`：加载和日常的占用记录一致性核对；
- `FiefSaveDefiner` 已登记 `Dictionary<Settlement,int>`、`Dictionary<Settlement,Settlement>`。

同期代码还已修复 `FiefPartyReturn.cs` 的按兵种和 `WaitCycle` 部分归还，并在超期、易主及陷落路径释放占用。`VillageEconomyPatches.cs` 已将自然增长计算接入 `H + M`，并按真实 Hearth 计算商品／粮食效果。**这些全部不是本次重写目标。**

**特别提醒**：新提交中的 `TryConsumeProfessionalHearth` 方法已存在，但在核对过的驻军、AI 直接招募和玩家池招募主路径中仍未发现调用；当前池招募主要执行 `TryConsume` 和名册增员。实施时务必**接入统一的职业兵真实生成交易**，并防止重复支付。

### 1.2 旧池仍运行，且调用范围比数据类广

| 现有路径 | 当前作用 | 本阶段处理 |
|---|---|---|
| `Recruitment/Pools/Behaviors/SettlementRecruitmentPoolBehavior.cs` | 每日给职业／封建池增加名额；`GetAvailableTroops`、`TryConsume` | 改为无库存的供给／生产协调行为；不再持久化两套兵员库存 |
| `Recruitment/Pools/Models/SettlementRecruitmentPoolData.cs` | `ProfessionalTroops`、`FiefTroops` 和两套进度 | 旧存档只读兼容；新存档仅保留合法小数进度／周计数 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | `WithdrawFiefPoolTroops` 从池提兵，每周入封邑名册 | 改成有供给、编制与周额度时直接 `AddToCounts`；独立保留周维护 |
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 合并职业兵池和俘虏候选、`BuildPlan`、预算提交 | 用临时职业候选替代池库存，俘虏路径完整保留 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | AI野战队伍直接购买职业池士兵 | 改用已有同 Clan 驻军调拨；不得在野战队伍处重新生产职业兵 |
| `Recruitment/Pools/UI/PlayerRecruitmentPoolPatch.cs` | 用池库存替换城镇招聘弹窗 | 移除旧池展示与提取；玩家从自有驻军转移，不新增第二条制造入口 |
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` | AI兵源类型的偏好、寻访 | 删除对池库存依赖，保留封建兵／佣兵等既有行为 |
| `Recruitment/Pools/Models/RecruitmentPoolTemplateRepository.cs` | 模板按文化／定居点类型解析、 troop tree source 标识 | **保留 XML 解析与已注册 `CharacterObject` 来源识别**；只淘汰库存概念 |
| `Recruitment/Patches/GarrisonRecruitmentPatch.cs` | 阻止原版 notable 自动招募、重写预估 | 保留禁用补丁；预估改读取虚拟当日供给与统一计划 |
| `Garrison/Models/NewSettlementGarrisonModel.cs` | `1 + BarracksLevel`，每日共用招募限额 | 保留 |
| `ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs` | 通过 Harmony 钩住旧池日 tick／`AddProductionProgress` 加速生产，以及驻军日限额加成 | **迁移加速钩子**到新训练能力计算；驻军限额加成保留 |
| `Main.cs`、`FiefSaveDefiner` | 行为与 Saveable 注册 | 更新行为注册与兼容类型，不直接删除旧 Saveable 类型 ID |

旧入口排查不能只按 `RecruitmentPoolBehavior` 类名搜索；所有 `RecruitmentPoolKind`、`ProfessionalTroops/FiefTroops`、`TryConsume()`、预测 UI 及 Harmony 方法目标都必须核对。

## 2. 建议模块切分：最小改动

建议只设一个生产协调入口 `SettlementManpowerProductionBehavior`（名称为建议，不是现有类），作为封邑当天共享供给的**唯一权威**：

- `CalculateVillageSupply(village, assignedFiefCount)`：纯计算，读取真实 H 和 `FiefPartyManager.GetMobilizedHearth()`，返回小数 `S_v`。
- `CalculateSettlementSupply(settlement)`：取得**所有**当前附属村庄，按 `StringId` 排序均匀分配当前封邑封建兵占用；逐村求和。
- `GetAllocationRatio(settlement)`：读取匹配的现有职业兵模板可选属性 `professionalRatio`，缺失回退0.5；另一侧自动 `1-r`。
- `PreviewProfessionalDemand`／`PreviewFiefDemand`：只读实际接收余量，不增兵、不扣钱；用以按权重分配并释放闲置份额。
- `PlanAndCommitDailyProduction`：一个定居点每日最多执行一次，负责共享额度、小数、实际兵种选择、提交。
- 每封邑保存：`ProfessionalFraction`、`FiefFraction`（各 `<1`）、`FiefProducedThisWeek`、`WeekKey`，必要时另存来源村庄当日出兵账；**不保存新兵库存**。

`FiefPartyManager` 继续只负责人力动员账本与封邑部队；其 `MobilizedHearth` 存储结构、征召／归还事务不搬家。

## 3. 详细计算规格

### 3.1 有效户数与人口占用

使用 `FiefPartyData` 同步后的总兵数：

\[
F_{total}=Ready+Recruited+Returned
\]

`Ready` 使用封邑当前名册（含伤）；`Recruited` 用在外分遣队登记；`Returned` 用冷却分遣队登记。统计时**不要再次把兵源池历史库存或非封建民兵混进 `F_total`**。应**先纳入现有村庄实际动员归属**，再将剩余人口占用确定性均分；避免简单平均导致某村 `M_v>F_v` 而产生虚假的本地供给：

```text
M_total = Σ GetMobilizedHearth(v)  // 只统计当前封邑负责的附属村庄
if M_total > F_total or ownership mismatch:
    reconcile / report            // 不静默改写真实 M
remaining = max(0, F_total - M_total)
q   = remaining / n                // 整数除法
rem = remaining % n
for each Village sorted by Settlement.StringId:
    F_v = M_v + q + (index < rem ? 1 : 0)
```

正常状态下满足 `ΣF_v=F_total` 且 `F_v≥M_v`。无需保存 `F_v`。`M_v` 始终是现有真实村庄动员标记，不能把 `_mobilizedHearth` 重新解释为“全部封建兵占用”；它只表示**已实际扣过 Hearth 的服役者**。旧档 `_legacyUntrackedMobilization`、改绑村庄等异常必须在生产前按现有核对逻辑处理。

\[
X_v=\max(0,H_v+M_v-F_v)
\]
\[
S_v= \begin{cases}0,&VillageState\ne Normal\text{ 或 }X_v\le100\\0.03X_v(X_v-100)/(X_v+50),&其他\end{cases}
\]

精确计算应使用 `float`／`double`，直到需要实际添加士兵才转整数；`BoundVillages` 空时直接返回0，不得除0。

### 3.2 生产额度与软比例

在一个封邑的同一个每日快照中求 `S_total=ΣS_v`，`r=professionalRatio`，初始：

```text
professionalShare = S_total * r
fiefShare         = S_total * (1-r)
```

再分别求可执行需求容量：

- 职业兵：**既有 XML 训练速度、文化匹配、有效兵营等级、军事官职训练加成**，驻军每日尚可招募量（被俘虏占用后的余额），可支付 Hearth（每人2）、编制、兵种结构、工资／Clan预算。不要将 `baseCapacity=30` 当作新兵编制上限。
- 封建兵：实际可用兵种、现有编制缺额、当周剩余 `MaxWeeklySupplement - FiefProducedThisWeek`，封邑的有效基础兵源。最低每周补员档次不能突破人口供给做保底；封建兵并不需要进入城镇／城堡兵营训练名册。

将初始份额中**当前无法兑现的部分**转移给另一个仍有合法需求的类型；如果两者都没有需求，则额度作废。两者实际使用的共享额度合计不得超过 `S_total`；小数进度只记录合法未满1人的已分配进度，不能把所有“已分配但因为预算不足没录用”的额度累计为士兵。

### 3.3 职业兵候选与统一招募模型

1. **候选筛选在招募模型之前**：职业兵模板按照文化／城镇或城堡／有效兵营解锁筛选 `CharacterObject`。`CharacterObject` 已从 XML 注册，不“创建一个新的 Troop 类”。
2. 每个候选的 `AvailableCount` 是**虚拟报价**，不是库存。各候选**共享同一职业兵总额度**，不能各自用满。
3. 建议给 `RecruitmentModelManager.Model.BuildPlan()` 调用层增加**整体可录用数量上限**，或在同等模型模拟状态内逐候选递减总额度。要在模型调用的模拟 `state.Commit` 阶段就约束，而不只在 `ExecuteUnifiedPlan` 末尾裁剪，避免前面的虚拟超额候选占用后续兵种的虚拟预算、错误改变后续评估。
4. 同一计划加入**真实俘虏候选**，共用 `GetDailyRecruitmentLimit()`；俘虏依旧由服从度与原有招募预算约束，但其申请不消耗村庄基础兵源。
5. 获批职业兵的实际提交顺序建议为：检查当日来源村庄剩余额度及 `Hearth≥102`、确定2 Hearth/人的支付村庄 → 验证金钱与名册接收仍有效 → 同一结算内扣 Hearth、增加相应 `CharacterObject`、提交 `AutoRecruitmentExpenses`／`ClanRecruitmentBudgetManager.CommitRecruitment()`、兑现小数进度。任一前置条件失败，**不应先出现士兵或先扣人口**。
6. **从本阶段起只有“职业兵真正进入驻军”扣2 Hearth**。从驻军转移出去不再次扣 Hearth；玩家／AI通过任何旧职业兵池直接入野战队伍的入口必须被替换或屏蔽。

`GarrisonRecruitFromPrisonersBehavior` 现有的 `ExecuteUnifiedPlan` 可继续充当最终提交入口，但必须移除 `pools.TryConsume()`；`GetPredictedDailyRecruitmentCount()` 必须使用同一套只读候选生成与配额，不能通过预测调用真的扣供给／Hearth／进度。

### 3.4 封建兵直接入现有封邑名册

- 从 `FiefPartyReinforcement.cs` 删除 `WithdrawFiefPoolTroops` 的池取货步骤；在分配到合法额度后，从 `fiefPartyTemplates.xml` 选出合法的**基础 `CharacterObject`**，遵守现有兵种权重及分类型编制上限，直接 `AddToCounts` 至 `_fiefParty`。
- 不得使用 `Settlement.MilitiaParty` 的**所有**成员数充当封建兵占用；需要按封建兵模板识别真正的封建兵。
- **周维护和生产分离**：`WeeklyUpdate()` 继续处理归还冷却、服役失效和已有升级；新增封建兵改由每天统一生产协调流程写入名册；避免周 tick 再独立调用旧 `PerformReinforcement()` 造成双重补员。
- 保留每封邑文化 `MaxWeeklySupplement` 为当周硬上限（帝国／阿塞莱10，斯特吉亚／巴旦尼亚／库塞特／诺德20，瓦兰迪亚30），每天入账并在新一周切换时清零。跨周不能结转未使用的**整人**补员额度；现有 `MinWeeklySupplement` 可先作为 XML 兼容字段保留，但不再提供无人口基础的保底增员。
- 新兵加入现有封邑名册时不扣 Hearth，也不增加 M；但计入 `F_total`，从下一日开始压低基础供给。
- 如保留现有具体兵种的 `RequiredBarracksLevel` 解锁规则，删除池之后仍应在候选选择时检查，防止无意开放高等级兵种；封建兵补员速率的核心限制来自周额度与共享供给，不应重新设计新的训练制度。

## 4. 一次结算的建议顺序

```text
每日（每个 Town/Castle 恰好一次）：
  1. 核对 / 读取最新 MobilizedHearth 与当前封建兵总量。
  2. 按附属村庄确定 F_v；逐村读取 H_v、M_v、VillageState；计算 S_v。
  3. 得到固定本日 S_total 快照，读取对应 XML professionalRatio。
  4. 预估职业兵、封建兵的实际接收需求与硬限制。
  5. 按比例初分；将某方无法使用的额度重分给另一方。
  6. 取得职业兵 XML CharacterObject 临时候选＋真实俘虏候选。
  7. 在共享驻军招募限额和统一财政模型内评估；提交真正获批的职业兵与俘虏。
  8. 把未被职业兵合法使用的当日基础兵源额度释放给封建兵。
  9. 若本周封建兵补员未达上限、封邑编制尚有缺额，则按模板直接补封建兵。
 10. 只保存不足1人的合法生产进度、本周已补员数；其余当日供给作废。
```

**日内快照**：新增职业兵扣 H、封建兵增 F 对**下一日**供给生效，避免行为监听器先后顺序改变同日配置份额。实际支付 Hearth 依然实时校验剩余余额和村庄 Normal 状态；只允许由本日提供过供给且实际 `H-100≥2` 的村庄承担职业兵成本。跨村汇总不足1人份额可用于形成一名实体兵，但成本最终记入合法来源村庄；账目不够时减少实际录用量，不能向另一村强行扣款或使 H 低于100。

**小数进度**：以封邑为单位持久化职业兵／封建兵的 `fraction ∈ [0,1)`，严格在有合法生产需求和来源供给时累加；结算整数立即减去已生成数量。若周上限已满或无驻军接收能力，则不能继续无限储存生产进度。对于改变归属、取消自动招募或模板切换，定义旧小数进度丢弃或受限继承策略，不得让换文化、易主、劫掠后瞬间兑现历史库存。

## 5. 既有系统必须同时处理的入口

### 5.1 AI 野战部队

当前 `AiSettlementRecruitmentPatch.cs` 的 `TryRecruitProfessionalPool()` 允许同 Clan 野战部队直接抽取原职业兵池；池撤销后应删除此生产来源，改为沿用 `GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison()` 在**已有驻军名册**转移，继续履行该模型的转移／兵种结构／工资／财政约束。转移不是生产，**不扣 Hearth**。封建兵征召、佣兵、俘虏其余入口不因本次供给变更而重写。

### 5.2 玩家职业兵入口

`PlayerRecruitmentPoolPatch.cs` 的职业池招募界面与 `OnDone` 消费路径在新方案下无有效库存来源，应移除或禁用。玩家使用自有城镇／城堡原生驻军转移进入野战队伍，不重复购买生产人口；不保留玩家通过旧池直接获得职业兵的第二条生成路径。注意不要不小心重新开启原版志愿兵自动补充（已有 `NotableVolunteerRetirementPatch` 与 `GarrisonRecruitmentPatch`）。检查城镇招募入口是否仍展示空白过期弹窗。

### 5.3 `ModifiedPolitics` 军事官职

当前 `MilitaryOfficeRecruitmentPatches.cs`：

- 以 `SettlementRecruitmentPoolBehavior.OnDailySettlementTick` + `SettlementRecruitmentPoolData.AddProductionProgress` 的 Harmony 前置补丁提升职业／封建池进度；旧方法取消后此补丁**失效且可能报错**。
- 驻军 `GetDailyRecruitmentLimit` 后置加成则仍可保留。

迁移要求：`OfficeEffectService.GetRegularRecruitmentFactor(settlement)` 用于现有职业兵训练能力修正（仍受共享兵源硬约束），不能放大 `S_total` 或突破人口源；`GetGarrisonRecruitmentFactor` 仍用于驻军招募数量上限。避免同一官职效果叠加两次。必要时同步发布 ModifiedArmy 与 ModifiedPolitics 兼容版本。

### 5.4 初始化与存档迁移

- `FiefSaveDefiner` 中现有 `SettlementRecruitmentPoolData` 类定义及旧字典 container 先**保留读取兼容性**；先停止新库存写入和生产，不要直接重用其 Saveable 类型 ID 注册不兼容类型。
- 初次加载旧档时，以现有**真实封邑／驻军名册**为基准，不将旧两套池的存量士兵自动直接加到名册，否则可能绕过 Hearth 和预算、复制士兵；旧池库存可按迁移策略丢弃，并记录一次诊断。
- 新增新生产状态专用存档字段和独立唯一 Saveable ID；加载旧存档时初始化 `ProfessionalFraction=0, FiefFraction=0, FiefProducedThisWeek=0`，明确周起点。
- 旧 `InitialFeifParty()` 若通过 `PerformReinforcement()` 从旧池获取初始化兵力，需改成不绕过共享供给；封邑陷落或易主即时补兵入口亦同。
- 保留 `_mobilizedHearth`、`_mobilizationOwners`、`_legacyUntrackedMobilization` 的已有字段号与迁移含义，不要丢失已保存的 M 或重新扣旧动员成本。

## 6. 校验与测试矩阵

| 场景 | 必须通过的断言 |
|---|---|
| 数学边界 | `X≤100`、非 Normal、无附属村庄→`S=0`；正常村庄逐村求和，绝不套总 Hearth |
| 服役人口恒定 | `H400/M0/F60→X340`；征召20后 `H380/M20/F60→X340`；返还20恢复初值 |
| 多村供给 | 正常登记时 ΣF_v=F_total 且每村 F_v≥M_v；伤兵、在外、冷却计一次 |
| 跨职业竞争 | 初始按 `r:(1-r)` 分配；某类无需求时剩余合法转移；总额度不超供给 |
| 兵种生成 | 具体 `CharacterObject` 来自现有 XML；职业兵招募前是候选，获批后才入名册；各候选共用一份职业额度 |
| 财政不足 | 无新增职业兵、无 Hearth 扣款、无进度／预算虚假消耗 |
| 人口不足 | 能付2 Hearth才允许生成1职业兵；所有来源村庄最终 H≥100 |
| 俘虏竞争 | 俘虏与职业兵共用日招募人数、预算及编制，俘虏不消耗村庄 S |
| 封建补员 | 直接进入现有封邑名册；封邑周累计不超文化10/20/30；不重复扣 Hearth |
| 驻军调拨 | 调拨已有职业兵到玩家／AI野战队伍不重新生成、不扣 Hearth |
| 低供给小数 | 小数合法兑现为整数；无需求／预算不足不能累积虚拟兵库存 |
| 易主／遭掠／围攻 | 生产停止或按现有规则受限；不凭历史进度恢复库存；M 的原有清理流程正常 |
| 老存档 | 已有真实驻军／封建兵与 M 原值维持；旧池库存不会在加载后重复新增 |
| Mod共存 | `ModifiedPolitics` 训练与招募上限加成仍正常，旧 Harmony 目标不再悬空 |
| 预测／UI | 招募预测不发生真实扣款；玩家没有旧池列表或无效招募入口 |

## 7. 实施次序（推荐逐步提交）

1. **基础层**：只读村庄 `H/M/F` 计算、3%公式、稳定整数分摊；写单元／数据测试。
2. **配置层**：现有职业 XML 增加单个 `professionalRatio`，另一比例 `1-r`，明确缺省与区间检查。
3. **生成协调层**：建立每天单入口、软分配、周额度、小数状态，并防止一日多次处理。
4. **职业兵接入**：在 `GarrisonRecruitFromPrisonersBehavior` 用 XML 虚拟候选替代池，统一审批、事务式扣2 Hearth／入驻军／计预算；俘虏保留。
5. **封建兵接入**：把旧提取库存改为合法产量直接 `AddToCounts`；旧周补兵入口只剩维护职责。
6. **外部入口与兼容**：AI从已有驻军调拨、玩家池弹窗移除、预测一致、`ModifiedPolitics` Harmony 迁移、保存兼容。
7. **全场景验证**：新档、旧档、归还、死亡、易主、围攻、食品、财政、文化与兵种结构。

## 8. 尚需在编码时选择的细节（不影响已确定设计）

- `professionalRatio` 具体七文化初值仍是**建议值**，可先使用0.5默认并由现有 XML 扩展逐文化配置。
- 俘虏与职业兵在统一招募计划内部的**相对排序**沿用现有模型，需以共享额度约束和测试确认实际行为。
- 小数进度在易主、模板切换、突然无可用村庄时的失效粒度需写成可复现规则，不能产生隐形库存。
- 如果不再使用封建兵旧 `baseDailyProduction/dailyProductionStep`，需把它们标为**兼容遗留字段**，保留 `MaxWeeklySupplement` 与受共享供给约束的每日结算；不因移除兵源池而改变旧兵种解锁条件。

---

### 源码索引

- [`FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [`FiefPartyData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [`FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [`FiefPartyRecruitment.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [`FiefPartyReturn.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [`VillageEconomyPatches.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Patches/VillageEconomyPatches.cs)
- [`SettlementRecruitmentPoolBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementRecruitmentPoolBehavior.cs)
- [`GarrisonRecruitFromPrisonersBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs)
- [`DefaultAIRecruitmentModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [`AiSettlementRecruitmentPatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Patches/AiSettlementRecruitmentPatch.cs)
- [`PlayerRecruitmentPoolPatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/UI/PlayerRecruitmentPoolPatch.cs)
- [`MilitaryOfficeRecruitmentPatches.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs)
