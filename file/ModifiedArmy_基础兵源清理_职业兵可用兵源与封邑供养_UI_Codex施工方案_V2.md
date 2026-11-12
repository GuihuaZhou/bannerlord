# ModifiedArmy + ModifiedPolitics：删除历史基础兵源、职业兵征兵负担及封邑供养 UI — Codex 施工方案 V2.0

> 类型：可直接交给 Codex / AI Worker 的设计与施工任务书；**不是已经完成的代码改动**。  
> 仓库：`GuihuaZhou/bannerlord`  
> 代码审查基线：`9d6d100fba631eb996595019f2f55e7866402d86`（2026-11-08 查询时最新提交）；正式施工时以本地 `git rev-parse HEAD` 为准。  
> 上一版：`ModifiedArmy_ModifiedPolitics_RecruitmentBurden_Codex_Implementation_V1.md`。**本文 V2 替代 V1 的“保留现有职业兵人口产能公式”及旧 UI 设计，不能将两份文档中冲突的规则同时实现。**  
> 目标：消灭历史“基础兵源/SharedSupply”生产限制；职业兵按训练能力生成候选，以城镇/城堡 Hearth + RecruitmentBurden 约束实际征兵；封邑兵按文化 `dailyRate` 补员，仍受各村供养预算约束；界面先展示职业兵“可用兵源”，再展示封邑兵独立供养情况。

## A. Codex 总指令（须先执行）

1. 进入仓库，先执行：`git status --short`、`git branch --show-current`、`git rev-parse HEAD`；如 HEAD 与上述基线不同，以实际源码核对本任务全部修改入口。不要覆盖未提交变更。
2. 阅读 `AGENTS.md`、`ModifiedArmy/AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`，以及 ModifiedPolitics 下任何适用的仓库指令；检查模块引用关系、SaveDefiner、XML、原版 Harmony 目标及本任务涉及的现有行为。
3. 在修改前列出“当前生产链 → 执行链 → 资金链 → 人口事务 → 补偿链”与变更范围。按照本文定义分阶段施工，每阶段核对编译和重要不变量。不得将旧设计报告当作代码权威。
4. 不重建兵源库存池，不新增职业兵供给进度池，不更改封邑兵供养 `FeudalSupply`、已动员人口 `MobilizedHearth`、返乡/安置/服务批次数据及流程。
5. **不自动 commit / push / PR**；不要声称编译通过就等于游戏内通过。无法确认原版方法签名、存档兼容或入口性质时按 `BLOCKED` 汇报，不猜测绕过。

## B. 用户已确认的最终规则（禁止自行变更）

### B1. 彻底删除历史基础兵源

旧“基础兵源”是动态日生产流量，不是真实兵员库存；旧公式：

```text
S(h) = 0                                        (h <= 100)
S(h) = 0.03 * h * (h - 100) / (h + 50)         (h > 100)
SharedSupply = Σ S(村庄可用供养 Hearth)
ProfessionalPopulationSupply = S(城镇/城堡实际 Hearth)
ProfessionalAllocation_old = min(ProfessionalPopulationSupply * ProfessionalShare, ProfessionalTrainingCapacity)
FiefAllocation_old         = min(SharedSupply * (1 - ProfessionalShare), FiefDemand)
```

**全部退出实时业务链，删除该计算公式、100 Hearth 生产门槛、村庄汇总逻辑、相关 Forecast 字段、旧基础兵源显示和对应无效日志。** 不保留空壳或实际继续调用的兼容函数。

> “删除基础兵源”**不等于删除农村供养、Hearth、封邑预算、文化制度权重、兵营限制或者新征兵负担**。这些是不同系统，必须保留。

### B2. 职业兵：训练决定候选产量，Hearth/Burden 决定能否实际征募

```text
DailyProfessionalOffersRate = ProfessionalTrainingCapacity
                            = doctrine.GetTraining(settlement).GetDailyProduction(barracksLevel)
                              * existingCultureProductionRate
                              * existingExternalTrainingModifiers(if any)
```

遵循当前实际 `GetDailyProduction(settlement, Professional)` 的原生政策、外部加成与舍入进度机制；只有训练模板、兵营资格与自动招募允许时才生产候选。不再乘人口产能 `S(H)` 或军事制度 `ProfessionalShare`。

- 候选仅表示“今天训练完成的可招兵种”，**产生候选时不扣 Hearth、不增加负担**。
- **成功实际产生一名新职业兵**：其来源城镇/城堡 `Hearth -= 1`，`RecruitmentBurden += C(culture, tier)`。
- 招募每下一名士兵前，均要求 `Hearth - RecruitmentBurden >= 1`；允许最后一人征募后出现负余额。
- 职业兵训练进度允许小数累计，未达到 1 人当天为零；不能用 `round()` 每日直接改整数。
- 驻军的日招募上限、经济预算、驻军编制、候选模板、文化等级资格和俘虏招募独立限制仍保留；**产出候选数 ≠ 实际新增驻军数**。
- 军队调拨、升级、战损和已存在士兵返还**不是新征兵**，不额外扣 Hearth / Burden。原版志愿兵按可核实的实际来源分类处理，不把所有 `AddToCounts` 当新职业兵。

### B3. 封邑兵：独立的每日补员率 + 逐村供养检查

```text
DailyFiefReplenishmentRate = CanProduceAnyFiefTroop()
    ? doctrine.GetProduction(settlement).DailyRate
    : 0
```

即移除 `min(SharedSupply * FiefShare, FiefDailyRate)`，直接以 XML 文化 `Fief/Production/{Town,Castle,TownPort}.dailyRate` 作为每日补员基础上限，沿用小数累计；整数补员到对应村庄已存在的封邑兵名册。

每生成一名封邑兵仍须复用当前 `VillageFiefData.CanIncreaseSupply()`（以及模板、兵营、各村恢复状态）：

1. 村庄完整供养余额 `GetFreeSupplyHearth` 足够支付该兵 Tier 供养成本；
2. 村庄封邑预算余额 `GetFeudalBudgetRemainingHearth` 足够；
3. 不允许将 A 村供养余额转嫁给 B 村。

**保留** `FeudalSupply[Tier]`、`GetFeudalBudgetHearth()` 对 `FiefWeight/(FiefWeight+ProfessionalWeight)` 的预算分配，文化 MilitaryDoctrine 作为 AI 偏好和封邑预算政策继续存在；只是移除权重对**每日产量**的再次缩放。

`Fief/Production prosperityCostPerTroop` 原有含义/逻辑如有在其他路径使用，应按实际调用保留，不因旧基础兵源清理连带更改。封邑兵生成、动员 Hearth 扣除、返乡返还、死亡损失、冷却和服役台账均保持现有语义。

### B4. 新增城镇/城堡职业兵征兵负担（与 V1 一致）

```text
H = 实际城镇/城堡 Hearth
B = 该城镇/城堡 RecruitmentBurden（持久化，不等于在役人数）
CapacitySigned = H - B
AvailableProfessionalManpowerUI = max(0, floor(CapacitySigned))

一名职业兵招募前：CapacitySigned >= 1
成功招募后：H -= 1，B += 文化/兵种 Tier 负担成本
每日 B = max(0, B - DailyRecovery)
DailyRecovery：七文化独立 XML 配置，首版均为 2
```

职业兵累计负担不随已有兵员死亡、退伍或调拨退还；新保存/加载、同日 Tick、异常补偿要保持一致。逐兵征募或等价的同事务批量模拟不允许连续透支。一次可承担人数为：

```text
if H-B < 1: affordable = 0
else affordable = min(requested, 1 + floor(((H-B)-1)/(1+CostPerTroop)))
```

注意浮点容差/上限与实际逐兵校验一致，负余额在内部完整保存，不能在存储层截为零。

### B5. 文化等级成本和每日恢复：固定首版参数

| 文化 ID | 文化 | T1 | T2 | T3 | T4 | T5 | T6 | 每日恢复 |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `Culture.empire` | 帝国 | 3 | 5 | 9 | 14 | 22 | 36 | 2 |
| `Culture.aserai` | 阿塞莱 | 4 | 7 | 11 | 17 | 27 | 42 | 2 |
| `Culture.vlandia` | 瓦兰迪亚 | 6 | 10 | 16 | 25 | 38 | 60 | 2 |
| `Culture.khuzait` | 库赛特 | 8 | 13 | 21 | 33 | 50 | 78 | 2 |
| `Culture.sturgia` | 斯特吉亚 | 8 | 13 | 21 | 33 | 50 | 78 | 2 |
| `Culture.battania` | 巴旦尼亚 | 10 | 16 | 26 | 40 | 62 | 96 | 2 |
| `Culture.nord` | 诺德 | 10 | 16 | 26 | 40 | 62 | 96 | 2 |

在 `militaryConfig.xml` 的每个 `<Culture><Professional>` 下加入独立：

```xml
<!-- 示例：帝国。其余六文化按上表替换 Tier Cost。 -->
<RecruitmentBurden dailyRecovery="2">
  <Tier Level="1" Cost="3" />
  <Tier Level="2" Cost="5" />
  <Tier Level="3" Cost="9" />
  <Tier Level="4" Cost="14" />
  <Tier Level="5" Cost="22" />
  <Tier Level="6" Cost="36" />
</RecruitmentBurden>
```

`MilitaryConfig.cs` 当前 `ParseTierCosts` 固定读取 `Hearth`；新 `Cost` 属性须使用独立解析器或可参数化属性名，保留 `FeudalSupply` 的旧解析契约。所有文化必须有 T1～T6 且唯一、正数、严格递增；恢复为非负/正数（本次统一 2，禁止静默缺省），非法配置应启动失败而非默默免费征兵。职业负担成本和恢复按**定居点自身文化**选择；不是所属家族/王国文化。

现有全局 `<HearthEconomy><ProfessionalRecruitment>` 中固定 Tier Hearth=1 的旧成本表与新逻辑重叠，需在全体调用方迁移后删除该表及不再使用的解析分支；每名职业兵实际 Hearth -1 是新事务的硬规则，**不能再额外扣一次**。保留相邻的 `FeudalTaxRelief`。

### B6. 经济影响（与 V1 一致）

```text
EffectiveHearth = H - 0.50 * min(H, B)
SupportedProsperity = EffectiveHearth * (城镇?5 : 城堡?3)
```

不直接减少 `Town.Prosperity`；继续现有 `ExcessProsperityDecayDays = 84` 的增量/衰退约束；H、B 经济计算做好 0、异常值保护。村庄人口自然增长、粮食、农产品、封邑税费等不改。**经济有效 Hearth 只用于繁荣度支持，不代替真实 Hearth 训练或人口事务，也不作为封邑兵供养预算。**

## C. 目标 UI 定稿（用户已明确）

现有入口：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs` 的 `InitializeReinforcementRateMenu()`，原有 `REINFORCEMENT_RATE_TEXT`。保留同一个菜单，不额外创建弹窗。

**1. 职业兵字段放在整个页面的最前面**（在“职业兵”标题下第一行），删掉历史 `基础兵源` 行，不以新字段复用 `SharedSupply` 的意义：

```text
【兵源查看】
职业兵
可用兵源：1746
基础训练：1.25 /天
兵营加成：+2.55 /天
文化效率：100%
其他加成：0%
训练能力：3.80 /天
预计补员：3.80 /天
实际补员：3 人

封邑兵
供养上限：900
已占用供养：680
剩余供养：220
基础补员：1.40 /天
预计补员：1.40 /天
实际补员：1 人
```

> 以上为**排版示例**，数值不对应特定真实存档；如现有 UI 还显示“自动招募启用/禁用”等字段，保留原字段，不为节省篇幅而删除。

**2. 职业兵 `可用兵源` 的唯一语义：** `max(0, floor(Hearth - RecruitmentBurden))`；只读来自人口服务契约的实时 H/B 或专门返回值，显示整数，无“/天”；与当前每日训练上限无关。若内部余额是 -20，UI 展示 `0`，但不可在后台丢掉 -20。

**3. 封邑兵供养的三个主要字段**直接使用现有 `FiefPartyData` 查询，禁止自造供养账本：

```text
供养上限 = GetTotalFeudalBudgetHearth()
已占用供养 = GetTotalOccupiedSupplyHearth()
剩余供养 = GetTotalFeudalBudgetRemainingHearth()
```

`GetTotalFeudalBudgetRemainingHearth()` 是“**逐村 max(0,预算_i-占用_i)，再求和**”；不能替换为 `max(0,总预算-总占用)`，因为村庄彼此不能挪用供养额度。因此极端情况下：`供养上限 - 已占用 ≠ 剩余供养` 是合理的。若某村超预算，应**有条件显示** `超额供养占用`（`sum_i max(0, occupied_i-budget_i)`，只读派生，禁止另存）或明确的提示，避免 UI 误导。`GetTotalSupplyDeficitHearth()` 反映完整村庄供养能力的物理缺口，不等于封邑预算超额，**不要混为一谈**。

**4. UI 不应列出每个文化成本表、内部负余额或所有技术账本字段；只新增用户确认的“可用兵源”和“封邑兵供养三项”。** 调试需要的 H/B/恢复数和事务 ID 进入适当日志即可。保留职业兵和封邑兵原有训练/补员信息。

**5. 数据不可用时**应显式显示“暂不可用”并保留其余可正常查询的栏目，不能悄悄显示 0 导致误解；所有新增显示文字必须进入英文基础词条和中文翻译，保证 TextObject 占位符一致。

## D. 需要核对并修改的具体文件与方法

| 文件 | 现有位置/职责 | 施工内容 |
|---|---|---|
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | `GetProductionForecast`、`CalculateVillageSupply`、`CalculateSettlementSupply`、`CalculatePopulationProductionSupply`、`EnsureDailyProduction` | 删除基础兵源函数和旧乘数；新候选 = 训练能力；封邑 `FiefAllocation = fiefDemand`；更正零产量早退、Debug/预测数据；保留小数进度、兵种权重选择、实际名册事务 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs` | 预测视图数据 | 删除 `SharedSupply`、`ProfessionalPopulationSupply`、只为旧生产分配而存在的 `ProfessionalRatio`（核实所有引用后）；保留训练、封邑预算和补员信息 |
| `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` | `ProfessionalTrainingConfig`、`CulturalMilitaryDoctrine`、旧统一 Hearth 成本 | 新增按文化 T1～T6 的职业负担和每日消退配置；删除旧全局职业 Hearth 成本分支；保留 `FiefWeight`、`ProfessionalWeight`、AI 选择与封邑供养预算 |
| `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` | 7 文化军事制度 | 各 `<Professional>` 配置负担成本与每日 2 恢复；原 `Training`、`Fief`、`FeudalSupply`、`dailyRate`、`Service` 不变 |
| `ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs` | Army 定义、Politics 实现的人口服务 | 向后兼容地新增 B/有效 Hearth/可负担人数/职业征募事务只读与提交接口；保留村庄 `TryConsume/Restore` |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs` | Hearth 真值、事务及每日更新 | 新增按定居点持久化 Burden，每日按文化恢复，职业征募 H/B 原子变更与补偿，旧存档初始化0；已有人口增长及日 Tick 去重保留 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Persistence/SettlementPopulationSaveDefiner.cs` | Saveable 定义 | 保留旧 Definer ID/历史 class ID/事务字段，必要时追加属性；`Dictionary<Settlement,float>` 若已注册不可重复注册 |
| `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs` | 人口支持繁荣度 | `GetHearth` → `GetEffectiveHearth`，保留84天超额衰退及正增长约束，不直接写 Prosperity |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs` | `TryConsumeProfessionalHearth`/rollback | 改为读取该地文化 Tier Cost 并提交统一 H/B 事务，既有事务 ID/回滚链继续可用 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs` | 已批准招募与资金预算执行 | 提交前以实时 H/B 按兵种裁剪 count，能够部分招募；恢复财政/roster/候选和 H/B |
| `ModifiedArmy/ModifiedArmy/Recruitment/Services/ProfessionalRecruitmentService.cs` | 具体实际驻军招募 | 防止候选扣费先后错乱；成功人数、候选消费、roster 变化、人口事务完全一致 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 玩家/AI 驻军招募，预测和结果日志 | 不移除原版每日招募上限、软硬容量、财政、俘虏来源；当前候选与真实可招募能力分开诊断；核实预测不再显示历史兵源误导信息 |
| `ModifiedArmy/ModifiedArmy/Population/Patches/NativeRecruitmentPopulationPatch.cs` | Native `ApplyInternal` Harmony | 审计不同来源，城镇/城堡新兵走原子人口事务，修复执行后扣费失败漏洞；村庄志愿兵/佣兵等别乱套城市负担 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs` | `InitializeReinforcementRateMenu()` | 删除基础兵源行；职业兵栏目最先显示可用兵源；封邑兵展示供养预算/已占用/剩余；原有补员字段保留 |
| `ModifiedArmy/ModifiedArmy/ModuleData/submodule_strings.xml` 与 `Languages/CNs/modifiedarmy-zho-CN.xml` | 显示/日志本地化 | 新增/调整词条，不留 `SUPPLY` 等废弃占位符，不破坏英文/中文占位符对应 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/SettlementManpowerProductionData.cs` | 保存已有小数进度和权重历史 | **原则上不修改**：保留 `ProfessionalProductionProgress`、`FiefProductionProgress`、`ProfessionalProducedByTroop`、`ProfessionalProductionSignature`、原 Saveable ID 与 `"_settlementRecruitmentPools"` Key |
| `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`、`.../PartyData/FiefPartyData.cs` | 逐村封邑预算与供养及生命周期 | **不改真实供养计算**；允许 UI 使用已有只读方法；如缺少按村超额供养显示，仅加纯查询，不改库存或序列化 |

**特别注意旧的 `EnsureDailyProduction` 早退条件：** 原为 `if (forecast.SharedSupply <= 0f && forecast.ProfessionalAllocation <= 0f)`，删除 SharedSupply 后须改成按**双方实际 Allocation** 判断，不可只检查职业兵产量，否则可能阻止封邑兵每天补员。

**特别注意 `ProfessionalShare`：** 旧 Forecast 乘法可移除，但 `CulturalMilitaryDoctrine` 权重仍被 `GetFeudalBudgetHearth()` 使用；不能把军事制度配置节点整体删除。

## E. 持久化与事务设计（由 V1 保留，严禁简化）

### E1. 唯一数据所有者与互相依赖

- `ModifiedPolitics.SettlementPopulationBehavior` 唯一保存城镇/城堡 Hearth 与 `RecruitmentBurden`，由 Army 公共接口对外暴露只读和事务；模块依赖仍为 Politics → Army。
- B 存档使用新键，例如 `ModifiedPolitics.SettlementPopulation.RecruitmentBurden.v1`；旧存档缺失初始化 B=0，保留原 H。`SettlementPopulationSaveDefiner` 现有 ID `2026110701`、`PopulationTransactionRecord` 类型 ID `1`、已有 SaveableProperty(1..5)、旧 SyncData 键全部保留。
- 已登记旧事务必须按历史语义补偿，只更新 Hearth；新职业事务在新编号扩展记录中带上 Burden Delta / 类型 / 提交日等必要数据；按唯一 OperationId 幂等，**重复调用不得二次生成士兵**。
- 单笔职业招募完成时 `H-=count` 与 `B+=count*cost` 在一个业务原子事务中提交/记录；失败补偿必须处理两者及关联候选/roster/财政。无法安全恢复则置 `RecoveryRequired`，冻结受影响招募路径并输出证据，不能静默继续。
- 日结算在同一 settlement/day 只应用一次：先按旧自然增长公式更新 H，再 `B=max(0,B-dailyRecovery)`；要避免启动/读档事件顺序变化使每日恢复重复。成本与 dailyRecovery 都按 `settlement.Culture` 查 XML；城墙升级/更换领主不清 B。
- 推荐新增 `IRecruitmentBurdenPolicy`（在 Army 契约中声明），从已加载 `MilitaryConfig` 读取文化恢复值；每次 Campaign 清理静态注册/按生命周期注册，Politics 不复制文化表。
- 默认的 `GetRecruitableHearth` 现用于 Hearth 查询，若还有村庄或其他既有调用必须保持原合同，不要粗暴改成 `H-B`。新 UI / 职业招募使用专门 `GetRecruitmentCapacity` / `GetAvailableProfessionalManpower`。

### E2. Native 招募补丁与逐兵约束

- 最新基线 `NativeRecruitmentPopulationPatch` 是 Prefix 预检 + Postfix 扣 Hearth；需要改为**执行前可核实提交、执行失败时补偿**（或同等安全方案），不能在原版 roster/金钱/志愿兵槽位已经变化后才发现余额不足。
- 先在实际 TaleWorlds 游戏 DLL 核对 `RecruitmentCampaignBehavior.ApplyInternal` 签名和 `RecruitingDetail` 各分支，记录来源分类清单；不要为 `MercenaryFromTavern`、无本地来源或俘虏转换错误扣城市人口，也不要让真正属于本地新增职业兵的原版入口免费绕过。
- 普通村庄志愿兵若已有 Hearth 规则，保留其原始语义；不得把村庄加入 Burden 字典。
- 招募前对所需数额逐名模拟；已批准四人、仅可承担一人时，**AI 驻军计划只提交一人**；原版无法部分执行的调用只在能保证全部事务成功时允许原调用，否则安全拒绝，不能冒然修改原版志愿兵数量造成金钱或事件失配。
- 不全局 Patch `TroopRoster.AddToCounts`，不因被救士兵、调拨、返乡、升级等已有士兵流转而收取新增职业兵费用。

### E3. 处理同日重复生产与负担恢复的时序

- 当前 `SettlementManpowerProductionBehavior` 使用 `_professionalOfferDays` 和 `EnsureDailyProduction()` 作为单日候选缓存；删除基础兵源只调整产能输入，不得让同一天调用重复结算进度。
- `SettlementPopulationBehavior` 已保存 `_lastDailyTick`；负担恢复与人口增长使用同一日结算锚点，不能创建第二个可能重复执行的独立日恢复行为。
- 训练预测 `GetPredictedProfessionalOffers()` 只读，不提交进度或负担；若实际招募受 H/B 限制，日志/提示应给出真实阻断原因而不修改训练速度。

## F. 建议的执行阶段（逐阶段门禁）

| 阶段 | 实施项 | 进入下一阶段的门禁 |
|---|---|---|
| P0 — 基线核对 | `git status`、读仓库指令、检索所有 `SharedSupply`、`ProfessionalPopulationSupply`、`CalculateVillageSupply` 和 `ProfessionalShare` 引用、审计 native 入口 | 出具变更文件表，确认不影响封邑供养和序列化 |
| P1 — 历史生产链清理 | 修改生产 Forecast/Behavior，移除旧 S(H) 与权重对日生产的乘算；保留训练能力、封邑 `dailyRate`、小数进度 | 无旧函数业务引用，按新公式出候选与补员；Village 供养检查正常 |
| P2 — 文化 XML | 加每文化 T1～T6 新负担+恢复2，清旧全局职业兵 Hearth 成本配置和引用 | 7文化配置完整、旧封邑 Tier 成本不变，XML 加载通过 |
| P3 — Politics 事务与存档 | B 持久化，H/B 原子事务、补偿、每日恢复、对旧存档兼容 | 新旧事务不串账，同日唯一恢复，跨日异常可诊断 |
| P4 — 真实驻军职业招募 | FiefPartyManager、候选消费、Executor、预算与库存事务衔接，按可承担数裁剪 | 1/2/4 人边界、错误回滚通过，四类资源一致 |
| P5 — Native 招募入口 | 根据实际 DLL Patch，完成前置事务与异常补偿，区分特殊来源 | 无已知无成本绕过和重复扣费，未确认入口明确 BLOCKED |
| P6 — 经济+UI+本地化 | 50% EffectiveHearth 进繁荣度支持；职业兵「可用兵源」首行，封邑供养3项；删除基础兵源行 | 数值正确，语言词条一致，缺服务不伪装成0，UI显示与实账一致 |
| P7 — 发布核验 | Release 双模块、架构门禁、存档、20/84/168天模拟、游戏内关键路径 | 输出逐场景 PASS/FAIL/BLOCKED + 证据，不用编译结果冒充运行结果 |

P1/P2 的独立静态验证可先做；真正接入招募必须在 P3 原子事务可用之后，不能暂时将 Burden 增量写在一个独立后置 Patch 内。

## G. 验收矩阵（必须逐条报告）

| ID | 验收内容 | 预期结果 |
|---|---|---|
| BS01 | 在有效源码中检索 `CalculatePopulationProductionSupply`、`CalculateVillageSupply`、`CalculateSettlementSupply`、`SharedSupply`、`ProfessionalPopulationSupply` | 旧生产路径与 Forecast/UI 均移除；无未迁移的编译引用 |
| BS02 | 三级兵营帝国：候选产生 | 候选按既有训练能力 4.00/天、分数进度，不再被 S(H)*ProfessionalShare 缩放 |
| BS03 | Hearth 80、B 0，训练条件满足 | **候选生产**不再被旧 `<=100` 公式阻断；**实际招募**仍受 H-B 与其他真实条件限制 |
| BS04 | Hearth 3000，B 2999，帝国 T4，计划 4 人 | 只成功招募 1 人；H 2999、B 3013、余额 -14；其余三人未扣费 |
| BS05 | H-B <= 0 | 可训练候选，但实际职业兵招募 0；不可重复付费或绕过 |
| BS06 | 不同文化相同训练条件 | 不乘旧职业兵人口产能、权重份额；保留文化训练参数和文化效率 |
| BS07 | 瓦兰迪亚封邑兵 dailyRate=4 且村庄供养充足 | 日均补员 4，不再乘 77.8% 或旧 S(H) |
| BS08 | 封邑兵有 dailyRate，但村庄预算不足 | 实际补员仍为 0/受限；不得借用另一村预算 |
| BS09 | 低人口村庄仍有供养预算与生产资格 | 旧 100 Hearth 产能门槛不再阻止封邑兵补员，但封邑预算和 Tier 成本照旧 |
| BS10 | `MilitaryDoctrine` AI 权重、封邑 `FeudalBudget` | 原值原算法；只移除它对日生产的旧乘数 |
| BS11 | 旧存档 `"_settlementRecruitmentPools"`、生产进度、ledger | 加载后进度与历史按既有保存约定保留，不初始化成0或重产 |
| RB01 | 旧存档 Hearth 有值、Burden 没字段 | Hearth 保留、Burden=0，原人口增长不变 |
| RB02 | 七文化 T1～T6 | 成本表完全一致，dailyRecovery 全为2且分别可配 |
| RB03 | H=1500, B=1499，帝国T4 招1 | 成功 H=1499,B=1513,余额=-14 |
| RB04 | 同日再招 | 拒绝，新增兵=0、人口/负担不变 |
| RB05 | 每日恢复 | B 独立减2，最多一天一次，读档不会重复恢复 |
| RB06 | B > H | UI 可用兵源为0；实际负余额持久化；有效劳动力最低为0.5H |
| RB07 | 招募异常、财政失败、资金不足 | 候选+roster+金钱预算+H+B 一致恢复，不冻结正常定居点 |
| RB08 | 重复 OperationId | 无二次招兵或二次收费；异常有可诊断 reason |
| RB09 | Native 城镇/城堡招募 | 该收费的入口实际新增后正确 H-1 / B+Cost，无事后扣费失败漏洞 |
| RB10 | Native 村庄志愿兵、佣兵、俘虏、调拨 | 不错误计入城市职业负担；已有村庄 Hearth 消耗不回归 |
| RB11 | 城镇 H3000、B2000 | EffectiveHearth=2000；繁荣度支持=10000 |
| RB12 | 城镇 H3000、B3500 | EffectiveHearth=1500；支持繁荣度7500，B仍保留3500 |
| RB13 | 20/84/168 天持续招募与停招恢复 | 真实 H/B、支持繁荣度、每日产出、实际补员符合规则 |
| UI01 | 打开“兵源查看” | 全页面最先展示“职业兵 → 可用兵源”，旧“基础兵源”行不存在 |
| UI02 | H=3000、B=1254 | “可用兵源：1746”，无 `/天`，不等于当天预计招募量 |
| UI03 | H=100、B=120 | “可用兵源：0”，内部 CapacitySigned=-20 不丢失 |
| UI04 | 封邑供养预算900、占用680、各村均有正常余额 | 上限900、已占用680、剩余220 |
| UI05 | A村预算100/占用130，B村预算100/占用0 | 全域上限200、已占用130、剩余100（逐村取max后汇总），不可错误显示70；须有超预算提示30或等价说明 |
| UI06 | 初始无数据/未就绪/服务不可用 | 显示“暂不可用”，不能用0伪装缺失；不崩溃 |
| UI07 | 中英文本地化 | UI 值、换行、占位符一致；无需额外新增菜单 |
| RG01 | 双模块 Release 编译 | 0 errors，实际记录 exit code |
| RG02 | 架构发布门禁 | 无 Army→Politics 依赖，旧 SaveId/Definer/Key 不被无故改写 |
| RG03 | 玩家/AI 招募、旧存档、封邑兵返乡/安置 | 游戏内有证据；不能只标记静态 PASS |

## H. 测试命令与交付说明

仓库根目录 Windows PowerShell：

```powershell
git status --short
git rev-parse HEAD
git diff --check

dotnet build .\ModifiedArmy\ModifiedArmy.sln -c Release --nologo
dotnet build .\ModifiedPolitics\ModifiedPolitics.sln -c Release --nologo

pwsh -NoProfile -File .\ModifiedArmy\tools\Test-ArchitectureReleaseGate.ps1
pwsh -NoProfile -File .\ModifiedArmy\tools\Invoke-ReleaseValidation.ps1
```

`Invoke-ReleaseValidation` 可能因游戏运行证据未完整而返回 BLOCKED；必须逐项说明，不允许用“静态编译通过”替代运行门禁。

最终报告必须包含：起始 HEAD、最终 HEAD、工作区状态；按文件列出改动；旧基础兵源死代码清理清单；文化负担 XML 与 7 文化校验结果；接口与持久化契约；职/封两条生产流程的调用链；UI 截图或实测文本；业务/编译/存档/游戏内测试结果（PASS/FAIL/BLOCKED+证据）；风险与未验证入口。

**最终不变量：**

1. 不再存在旧基础兵源对职业或封邑兵每日产量的任何乘算。
2. 职业兵候选产量只依训练能力、现有资格及修正；真正征召才以城镇/城堡 Hearth + RecruitmentBurden 结算。
3. 封邑兵每日增量由文化 `dailyRate` + 真实逐村供养额度决定；封邑兵生命周期完全不改。
4. UI 上职业兵先展示“可用兵源”；封邑兵展示独立的供养预算、占用和逐村剩余；显示值来自唯一现有数据所有者。
5. 旧档不丢原 Hearth、生产小数进度、封邑供养与服务状态；H/B/roster/财政失败后可对账。

---

## I. 核心源码参考（施工前先用实际 HEAD 对照）

- [仓库根 AGENTS.md](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/AGENTS.md)
- [ModifiedArmy 组织规则](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/CODE_ORGANIZATION_RULES.md)
- [基础兵源与候选生产 Behavior](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [生产预报 Forecast](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs)
- [村庄封邑供养](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [封邑兵补员](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [军制配置](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [军制 XML](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
- [人口服务契约](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs)
- [人口与事务 Behavior](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs)
- [繁荣度支撑模型](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs)
- [兵源查看 UI](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs)
- [驻军实际招募 Executor](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Recruitment/Services/GarrisonRecruitmentExecutionService.cs)
- [原版志愿兵补丁](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/ModifiedArmy/Population/Patches/NativeRecruitmentPopulationPatch.cs)
- [架构门禁](https://github.com/GuihuaZhou/bannerlord/blob/9d6d100fba631eb996595019f2f55e7866402d86/ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1)
