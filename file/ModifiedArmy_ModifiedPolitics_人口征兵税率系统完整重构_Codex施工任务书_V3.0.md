# ModifiedArmy + ModifiedPolitics 人口、职业兵与税率系统完整重构施工任务书 V3.0

## 0. 任务说明

**仓库：** `GuihuaZhou/bannerlord`  
**主要模块：** `ModifiedArmy`、`ModifiedPolitics`

本任务要求直接修改 Git 仓库，完成代码、配置、存档兼容、UI 和测试。

**这是一个完整施工任务，不是要求再次制定设计方案。**

本次采用一次性完整实施，内部按阶段施工。前五阶段优先完成全部底层功能，**第六阶段才统一修改 UI**。

不要每完成一阶段就停止等待确认。除非遇到无法安全解决的严重阻塞，否则继续完成后续阶段。

开始前读取最新 HEAD，检查工作区是否已有未提交改动，并核对实际源码。不要直接覆盖其他任务的未提交修改。

## 1. 最终设计与已确定参数

### 1.1 总体原则

新系统建立以下关系：

- Hearth 决定城镇、城堡的经济人口规模。
- Hearth 继续决定繁荣度支撑上限。
- 新职业兵招募按 Tier 消耗 Hearth。
- 保留 30% 的 Hearth 容量作为最低民生人口。
- Hearth 规模与忠诚度共同影响职业兵训练效率。
- 征兵压力 RecruitmentBurden 完全退出运行时机制。
- 新增五档税率，同时影响收入、忠诚度、繁荣度。
- AI 每日按当前财政、经济、征兵需求及叛乱风险选择税率。
- AI 采用迟滞机制，避免频繁加税、减税。

不得增加新的兵源池、财政疲劳指标或人口惩罚累计值。

### 1.2 已确定的税率配置

| 档次 | 税收倍率 | 忠诚度/天 | 繁荣度/天 |
|---|---:|---:|---:|
| 极低 | 0.50 | +1 | +1 |
| 较低 | 0.75 | +0.5 | +0.5 |
| 普通 | 1.00 | 0 | 0 |
| 较高 | 1.25 | -1.5 | -1.5 |
| 极高 | 1.50 | -3 | -3 |

不能擅自削弱这些数值。

### 1.3 职业兵 Hearth 成本

采用第一版测试配置：

| Tier | Hearth/兵 |
|---|---:|
| T1 | 1 |
| T2 | 1.25 |
| T3 | 1.5 |
| T4 | 2 |
| T5 | 3 |
| T6 | 4 |

本项属于可配置平衡参数，七文化暂时统一。

最低成本不得低于 1 Hearth，随 Tier 单调增加。

### 1.4 人口效率

最低民生人口比例：`0.30`

完全发挥人口训练效率的 Hearth 容量比例：`0.40`

人口训练效率：

`PopulationEfficiency = Clamp((Hearth / Capacity - 0.30) / 0.10, 0, 1)`

不得改变已有 Hearth 容量、初始值和自然增长参数。

### 1.5 忠诚度征兵意愿

采用当前加载的本体 `SettlementLoyaltyModel` 阈值，不硬编码固定忠诚度数值。

设：

- R = RebelliousStateStartLoyaltyThreshold
- M = LoyaltyDriftMedium
- U = ThresholdForTaxBoost
- L = 当前忠诚度

规则：

- `Settlement.InRebelliousState == true`：效率为0。
- `L <= R`：效率为0。
- `R < L < M`：从0连续插值到1。
- `M <= L < U`：从1连续插值到1.15。
- `L >= U`：效率为1.15。

必须验证动态阈值之间的有效顺序，并处理其他 Mod 覆盖模型造成的异常或重合阈值，避免除零和 NaN。

职业兵最终训练能力：

`EffectiveTraining = OriginalTraining × PopulationEfficiency × LoyaltyEfficiency`

这里 OriginalTraining 包括现有兵营、文化及其他合法训练修正。

## 2. 施工原则与架构限制

1. ModifiedPolitics 继续持有城镇、城堡 Hearth 真值。
2. ModifiedArmy 继续负责职业兵训练、生产、实际招募。
3. 沿用现有 `ISettlementPopulationService` 和人口事务机制。
4. ModifiedPolitics 通过已有对 ModifiedArmy 的引用复用公共契约。
5. 不新建与已有模型竞争的 Harmony Patch。
6. 不新增第二套税收、忠诚度或繁荣度 Model。
7. 不重做封邑兵制度、兵营、兵种模板和文化军事制度。
8. 不因为取消征兵压力而取消真正的人口消费。
9. 不为了 AI 补员而凭空生成 Hearth、金钱或士兵。
10. 同一人口事务只能提交一次，失败补偿不能重复生效。

本次要实现最终接口和数据结构，不要先做临时模型再删除。

---

# 第一阶段：移除 RecruitmentBurden，重构职业兵人口事务

## 3. 清理征兵压力

重点文件：

- `ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs`
- `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`
- `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`
- `ModifiedArmy/ModifiedArmy/Main.cs`
- `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/PopulationGrowthModel.cs`
- `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs`
- `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs`
- `ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml`

需要删除运行时机制：

- RecruitmentBurden 每日增加。
- RecruitmentBurden 每日恢复。
- `Hearth - RecruitmentBurden` 招募容量。
- RecruitmentBurden 对自然人口增长的影响。
- RecruitmentBurden 对繁荣度支撑的影响。
- 旧征兵压力 Policy 和 Registry。
- 七文化压力成本配置。

配置中保留 `Fief/FeudalSupply`，该机制属于封邑兵供养，不是本次删除目标。

人口增长恢复为：

`HearthDailyChange = Natural + Food + Prosperity`

不得修改原有自然增长算法。

繁荣度支撑恢复为：

`SupportedProsperity = Hearth × ProsperityPerHearth`

城镇和城堡继续使用各自现有 XML 配置，保持超额繁荣度衰退规则。

## 4. 重构公共人口招募接口

职业兵招募改为 Hearth 成本事务。

不要再将第三个参数命名或解释为 `burdenCostPerTroop`。

建议统一以真实 `CharacterObject` 作为兵种参数：

```csharp
int GetMaxAffordableProfessionalCount(
    Settlement settlement,
    CharacterObject troop,
    int requested);

PopulationTransactionResult EvaluateProfessionalRecruitment(
    Settlement settlement,
    CharacterObject troop,
    int count);

PopulationTransactionResult TryRecruitProfessional(
    Settlement settlement,
    CharacterObject troop,
    int count,
    string operationId);
```

由统一配置解析服务根据 `troop.Tier` 获取 HearthCost，不允许各招募入口自行复制 Tier 成本数组。

如果实际调用关系需要保留更低层的纯计算方法，可以内部传入 `float hearthCostPerTroop`，但所有实际业务入口必须使用相同的成本解析来源。

必须实现一个统一的可负担数量纯函数，供预览与提交复用。

## 5. 保留事务原子性

当前 `SettlementPopulationBehavior` 中的：

- `TryRecruitProfessional`
- `TryConsume`
- `TryRestore`
- `TryCompensate`
- `GetTransactionResult`

必须保留原有事务生命周期。

新职业兵事务至少需要记录：

- OperationId。
- Settlement。
- RecruitCount。
- HearthConsumed。
- HearthBefore。
- HearthAfter。
- Status。
- CommittedDay。

重点：`RecruitCount` 和 `HearthConsumed` 现在不是同一数值。

例如：招募5名 T4，RecruitCount = 5，HearthConsumed = 10。

新事务中的 Hearth 扣除、日志和补偿必须使用真实 HearthConsumed。

为了兼容旧存档：

- 保持旧 `PopulationTransactionRecord` 类型不变。
- 保持已有 `[SaveableProperty(1..10)]` 的编号不变。
- 旧 `Amount` 字段的历史语义不能被悄悄改写。
- 如需新增 `HearthConsumed`，使用新的未占用 SaveableProperty ID。
- 对旧职业兵事务，新增字段缺失时按照旧规则将每名士兵的 Hearth 成本视为1。
- 对非职业兵的旧事务，保持原有正负 Amount 语义。

补偿失败继续使用 RecoveryRequired，不得直接忽略事务错误。

## 6. 旧存档迁移

历史压力键：

`ModifiedPolitics.SettlementPopulation.RecruitmentBurden.v1`

第一版推荐：

- 只在加载旧存档时兼容读取。
- 读取的数据不再进入运行时业务状态。
- 新存档不再写入该压力字典。
- 不返还旧版本已扣除的 Hearth。
- 不删除旧存档中的既有士兵。
- 不修改 Hearth、LastDailyTick 和历史事务状态。

`SyncData` 中可以采用仅加载旧键的兼容方式，但必须实测 Bannerlord SaveSystem 对缺省键和重新保存的行为。

不能在未经验证的情况下假定直接删除 `SyncData` 一定兼容旧档。

保持：

- Hearth.v1
- Transactions.v1
- Recovery.v1
- LastDailyTick.v1

及现有 SaveableTypeDefiner 类型 ID。

旧的 BurdenDelta、BurdenBefore、BurdenAfter 属性可继续作为弃用存档字段保留，不能参与新事务运算。

旧存档已有 RecoveryRequired 时，不得无条件清除，因为无法断言冻结原因仅来自征兵压力。

---

# 第二阶段：Hearth 征兵容量与人口训练效率

## 7. 全局职业兵人口成本配置

建议在现有 `MilitaryConfig/HearthEconomy` 下新增一组全局配置，避免七文化重复配置：

```xml
<ProfessionalPopulation
    reserveRatio="0.30"
    fullEfficiencyRatio="0.40">
  <Tier level="1" hearth="1.00" />
  <Tier level="2" hearth="1.25" />
  <Tier level="3" hearth="1.50" />
  <Tier level="4" hearth="2.00" />
  <Tier level="5" hearth="3.00" />
  <Tier level="6" hearth="4.00" />
</ProfessionalPopulation>
```

XML 节点结构可按现有 MilitaryConfig 解析风格适当调整，但必须集中配置。

验证：

- 1～6级完整配置。
- 不允许重复 Tier。
- 数值必须是有效、正的有限浮点数。
- Hearth 成本不低于1。
- Tier 成本单调不下降。
- `0 <= reserveRatio < fullEfficiencyRatio <= 1`。

## 8. 最低民生 Hearth

设：

- H = 当前 Hearth。
- K = 当前 Hearth 容量。
- r = 0.30。
- C = 单兵 Hearth 成本。

最低民生人口：`ReserveHearth = K × r`

可消耗人口：`AvailableHearth = max(0, H - ReserveHearth)`

该 Tier 当前最多可招募：`MaxCount = max(0, floor(AvailableHearth / C))`

结果不能超过请求数。

实际事务提交前必须重新读取 Hearth，重新审批。

招募后必须满足：`HearthAfter >= ReserveHearth`

不能只在 UI 预览阶段检查。

注意：30% 底线只约束职业兵招募，不能阻止饥荒、掠夺或其他人口经济机制减少 Hearth。

村庄原有招募和封邑兵消费机制不应被错误套用这条规则。

## 9. 统一所有职业兵招募入口

必须检查并同步修改：

- `SettlementManpowerProductionBehavior`
- `GarrisonRecruitmentExecutionService`
- `ProfessionalRecruitmentService`
- `GarrisonRecruitmentPatch`
- `NativeRecruitmentPopulationPatch`
- `RecruitmentVmPopulationPatch`
- `FiefPartyManager`
- 其他搜索到的 `TryRecruitProfessional` 调用方。

不能出现：

- 自动驻军补员扣人口，玩家招募却不扣。
- 玩家招募扣人口，AI 招募不扣。
- 同一士兵驻军调拨时再次扣人口。
- 职业兵升级时重复扣人口。
- 招募失败但 Hearth 未恢复。
- UI 或预览批准数量大于实际可招募数量。

`GetTotalAffordableProfessionalOffers` 必须按各兵种真实 Tier 消费 Hearth，不能简单使用“候选总人数 <= Hearth”。

多个不同 Tier 的候选应在统一顺序下累计审批，避免重复使用同一笔 Hearth 余额。

继续保留驻军编制限制、Clan 资金预算、兵种模板、文化资格与其他现有审批。

## 10. Hearth 人口效率

现有 `SettlementManpowerProductionBehavior.GetDailyProduction` 当前会计算兵营训练能力并应用既有文化效率。

新增：`PopulationEfficiency = Clamp((H/K - 0.30)/0.10, 0, 1)`

必须只作用于职业兵生产，不作用于封邑兵产量。

需同步检查：

- GetDailyProduction
- GetProductionForecast
- Daily professional offers
- ProfessionalTrainingCapacity
- ProfessionalAllocation
- 原有小数训练进度

避免在多个路径重复乘人口效率。

特别注意，现有 ModifiedPolitics 的 `MilitaryOfficeRecruitmentPatches.IncreaseRegularProduction` 会对 `GetDailyProduction` 的结果增加官职训练加成。

必须保留该加成，并确保人口效率最终不会被官职加成绕过。

建议在训练计算链中清晰拆分：

- 原有训练能力。
- 人口效率。
- 忠诚度效率。
- 官职等额外训练加成。
- 最终训练能力。

现有 `ExternalTrainingBonusRate` 等预测字段必须同步调整，不能因为新增乘数就错误显示加成为0或负值。

## 11. 职业兵候选与进度

保留现有：

- 职业兵模板权重。
- 每日生产候选。
- 训练进度小数累计。
- 现有兵营等级和文化效率限制。

人口效率为0时，不应继续积累能够在未来一次性释放的大量职业兵生产进度。

当人口下限限制实际招募时，未成功招募的候选不得视为已征召士兵。

不得让候选数无限积压后在 Hearth 恢复时突然大量补员。

同时保持正常情况下的小数训练进度累计，不能简单每次向下取整而永久损失小数产能。

---

# 第三阶段：忠诚度征兵效率与异文化忠诚度调整

## 12. 移除默认异文化忠诚度惩罚

文件：`ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/OfficeSettlementModels.cs`

现有 `OfficeSettlementLoyaltyModel` 继承 `DefaultSettlementLoyaltyModel`。

直接覆盖本体提供的属性：

- `SettlementOwnerDifferentCultureLoyaltyEffect` 返回0。
- `GovernorDifferentCultureLoyaltyEffect` 返回0。

保留：

- `GovernorSameCultureLoyaltyEffect` 原有正面效果。
- 既有治安官忠诚度加成。
- 王国政策的独立修正。
- 原版治安、饥荒、建筑、民兵和其他忠诚度规则。

不要为了这个修改再添加 Harmony Patch。

## 13. 忠诚度征兵效率

在 ModifiedArmy 专业招募计算模块新增纯函数：

`CalculateLoyaltyRecruitmentEfficiency(Settlement settlement)`

从实际模型获取：

- `RebelliousStateStartLoyaltyThreshold`
- `LoyaltyDriftMedium`
- `ThresholdForTaxBoost`

同时检查：`settlement.InRebelliousState`

按照第1.5节的分段连续插值计算。

忠诚度效率最大为1.15。

低忠诚度只减少本地新职业兵训练，不得：

- 删除已有驻军。
- 禁止正常驻军调拨。
- 禁止雇佣兵。
- 错误修改封邑兵原有征召制度。

最终训练效率：

`EffectiveTraining = OriginalTraining × PopulationEfficiency × LoyaltyEfficiency`

UI 尚未修改前，可以先通过日志和纯计算测试验证全部数据。

---

# 第四阶段：实现五档税率与财政整合

## 14. 复用 ModifiedPolitics 现有财政体系

现有代码：

- `HeroOffices/Models/OfficeSettlementModels.cs`
- `KingdomFief/Models/SettlementFinanceBreakdown.cs`
- `KingdomFief/UI/KingdomSettlementFinanceVMMixin.cs`
- `SettlementEconomy/Models/SettlementEconomyProsperityModel.cs`

已注册：

- `OfficeSettlementTaxModel`
- `OfficeSettlementLoyaltyModel`
- `SettlementEconomyProsperityModel`

应扩展这些模型，不再新增重复的 Campaign Tax/Loyalty/Prosperity Model。

## 15. 新增封邑税率状态

在 ModifiedPolitics 内创建税率政策数据与持久化服务。

每座城镇、城堡独立保存档次：

- VeryLow
- Low
- Normal
- High
- VeryHigh

默认 Normal。

税率效果配置建议放在 ModifiedPolitics 的 XML 配置体系中。

五档数据必须与第1.2节完全一致。

一个权威税率服务提供：

- 获取当前税率档次。
- 获取税率收入倍率。
- 获取每日忠诚度修正。
- 获取每日繁荣度修正。
- 设置税率，并校验修改权限。
- 供 AI 和玩家 UI 共用的读取接口。

定居点易主时，必须制定明确且一致的规则。第一版建议恢复普通税率，避免新领主被上一任领主的苛税政策永久绑定。

旧存档缺少税率记录时默认为普通。

## 16. 税收模型

修改 `OfficeSettlementTaxModel.CalculateTownTax`。

按既有税收模型计算正常税基，应用新的税率倍率，同时保留：

- 原版税收修正。
- 税务官效果。
- 本体忠诚度、治安带来的税收变化。
- 现有封邑兵税收减免。

税率仅影响当前城镇或城堡的税收，不自动乘到：

- 关税。
- 附属村庄独立收入。
- 驻军工资。
- 其他不属于城镇税收的项目。

封邑兵税收减免不能因为税率倍率而被错误放大。

现有 `ExplainedNumber` 包含累计乘数，需要特别检查 AddFactor 和减免金额的运算顺序。

最终必须满足：

`SettlementFinanceBreakdown` 展示的税收与游戏实际结算收入一致。

不得额外发放一笔“税率差额”金币，税率应直接进入既有税收模型。

## 17. 忠诚度模型

在 `OfficeSettlementLoyaltyModel.CalculateLoyaltyChange` 中加入税率修正。

每日效果：

- 极低 +1。
- 较低 +0.5。
- 普通 0。
- 较高 -1.5。
- 极高 -3。

使用 ExplainedNumber 正常添加单项修正。

保留所有本体及官职修正。

不得通过每日 Tick 再手动修改一次忠诚度。

## 18. 繁荣度模型

在 `SettlementEconomyProsperityModel.CalculateProsperityChange` 中加入税率对应的每日修正。

必须先计算完整的每日繁荣度变化，再套用 Hearth 支撑上限与原有超额繁荣度衰退规则。

因此：

- 低税率带来的正向繁荣度变化不能突破 Hearth 上限。
- 高税率的负向繁荣度变化正常生效。
- 原版其他繁荣度变化继续保留。
- 税率修正不能重复结算。

Hearth 容量、自然增长与原有食品系统不变。

---

# 第五阶段：AI 每日税率决策与迟滞机制

## 19. AI 每天判断税率

新增 ModifiedPolitics 的 AI 税率决策 Behavior。

通过已有 Campaign 每日事件，每天对 AI 管理的城镇、城堡进行一次评估。

要求：

- 每座封邑独立决策。
- 每天都执行判断。
- 不设置14天税率冷却。
- 不为玩家所属 Clan 的封邑擅自修改税率。
- 同一封邑同一天最多实际执行一次税率决策。
- 无效、未加载完成或不具备管理资格的封邑跳过。
- 使用当前游戏状态，不模拟未来忠诚度。

## 20. 读取真实财政数据

从现有 `Campaign.Current.Models.ClanFinanceModel` 获取：

- Clan 当前 Gold。
- Clan 当前实际收入。
- Clan 当前实际支出。
- 日均净收支。

可参照：

- `ClanRecruitmentBudgetManager`
- `KingdomClanFinanceUI`
- `SettlementFinanceBreakdown`

财政模型只调用无副作用的评估方式，例如 `applyWithdrawals=false`。

不得为了评分执行实际收支结算。

计算 Clan 财政紧张程度时，至少考虑：

- 目前是否存在持续净亏损。
- 亏损在支出中的占比。
- 资金储备能够承担当前亏损的程度。

不要只通过是否处于战争状态判断财政压力。

战争中的富裕 Clan 不应自动加税。

## 21. 税率候选评分

每日评估五种候选税率。

评分仅考虑当日信息：

`TaxScore = FiscalBenefit + EconomyBenefit + RecruitmentBenefit - RebellionRisk`

所有分量应进行归一化，不允许直接把第纳尔与忠诚度数值相加。

财政收益：

- 当前财政越困难，税收收入的权重越高。
- 当前财政越宽裕，减税带来的民生收益越重要。

经济收益：

- 当前 Hearth 与繁荣度。
- 当前税基规模。
- 当前税率的繁荣度效果。

征兵收益：

- 当前人口可征召余量。
- 当前忠诚度征兵效率。
- 当前本地职业兵补员需求及驻军情况。

叛乱风险：

- 使用本体 `Settlement.InRebelliousState`。
- 使用当前加载模型的 `RebelliousStateStartLoyaltyThreshold`。
- 不硬编码本体忠诚度阈值。

候选评分可以评估五档税率已经定义的直接效果，但不得模拟未来数天忠诚度或预测叛乱概率。

需要从 XML 读取评分权重和必要的容差参数。初始值以稳定、可解释为原则，保留日志便于平衡调整。

对候选税收收益应使用纯计算，不允许临时修改实际税率后调用带副作用的财政结算。

## 22. 防止 AI 税率反复横跳

采用迟滞机制和非对称升降税规则。

### 22.1 民生保护状态

每座封邑保存：

- 是否处于民生保护状态。
- 连续安全天数。
- 最近评估日期。

进入条件：

- 本体 `InRebelliousState` 为真；或者
- 当前忠诚度已接近本体提供的叛乱风险阈值。

“接近”应通过本体动态阈值与正常忠诚度中心之间的相对安全区间计算，不使用固定忠诚度数字。

退出条件：

- 不再处于叛乱风险状态。
- 忠诚度恢复到 `LoyaltyDriftMedium` 附近。
- 连续安全7天。

7天作为 XML 配置项。

不要求预测未来忠诚度。

### 22.2 非对称税率变化

降税：

- 每天允许。
- 遇到当前叛乱风险可以立即跨多个档次。
- 不受加税收益门槛限制。

加税：

- 每次最多提高一档。
- 需要新档次评分明显超过当前档次。
- 在民生保护期间禁止常规加税。
- 不因当天财政数据的小幅波动就提高税率。

### 22.3 税率切换门槛

增加可配置评分差门槛。

仅当候选税率相对于当前税率具有明确收益优势时，才允许非紧急变更。

如果差距较小，维持当前档次。

每日评估必须保留，但每日不一定修改税率。

### 22.4 不能形成长期无意义循环

AI 应具备财政需求约束：

- 财政充裕时不应为了微小税收收益反复加税。
- 进入民生保护后即使忠诚度略有回升也不能立即提高税率。
- 退出保护状态后，加税仍需真实财政收益优势。
- 允许战争财政危机时承担一定民生风险，但不能无视已经存在的叛乱风险。

不通过给 AI 隐藏税收、忠诚度或免费军队解决问题。

## 23. AI 状态持久化

当前税率和必要的 AI 迟滞状态保存到 ModifiedPolitics。

保留存档兼容：

- 旧档缺失税率记录时默认普通。
- 旧档缺失 AI 状态时初始化为正常状态。
- 保存后重新加载不能让 AI 重复执行当天判断。
- 封邑易主应正确处理政策与 AI 状态。
- 不存储不必要的未来预测和历史忠诚度曲线。

---

# 第六阶段：最后统一实现 UI

**只有前五阶段核心功能完成并通过基础测试后，才开始 UI 修改。**

## 24. 城镇、城堡兵源 UI

修改现有兵源展示入口，保持与当前 ModifiedArmy 的实际功能一致。

建议显示：

- 实际 Hearth。
- Hearth 容量。
- 最低民生 Hearth。
- 当前可征召 Hearth 余量。
- 当前人口训练效率。
- 当前忠诚度征兵效率。
- 原有基础训练能力。
- 原有文化与兵营修正。
- 最终职业兵训练能力。
- 当前实际可招募人数。
- 不同 Tier 的 Hearth 消耗说明。

彻底移除征兵压力及压力恢复字段。

注意“可消耗 Hearth”和“可招募士兵人数”不能混用。后者必须根据具体兵种 Tier 及实际招募条件计算。

原有日志或 UI 中对 `GetRecruitmentCapacity` 的旧语义应同步清理。

## 25. 封邑财政 UI

在 ModifiedPolitics 现有王国封邑财政界面中扩展：

- 当前税率档次。
- 当前税收倍率。
- 当前实际税收。
- 当前税率的忠诚度日修正。
- 当前税率的繁荣度日修正。
- 玩家可操作的五档税率选择控件。
- AI 封邑当前税率及必要的政策说明。

必须使用原有 UIExtenderEx 和当前 Gauntlet VM、Prefab 体系。

先检查：

- `KingdomSettlementFinanceVMMixin`
- `KingdomSettlementFinance.xml`
- `SettlementFinanceBreakdown`
- 当前封邑列表选中行为和实际所有者判定。

税率修改权限必须根据实际封邑所有者与玩家管理权限判定，不允许玩家随意修改其他 Clan 的封邑税率。

需要更新中英文文本及相关本地化资源。

玩家切换税率后，实际财政模型、忠诚度和繁荣度提示应同步刷新。

## 26. 其他 UI 和日志清理

清除：

- 旧 RecruitmentBurden 字段。
- 压力恢复文本。
- 旧压力预算与容量提示。
- 旧成本日志字段。
- 已失效的本地化条目。

保留必要的招募诊断，新增：

- TroopTier。
- HearthCostPerTroop。
- RequestedCount。
- ApprovedCount。
- HearthConsumed。
- PopulationEfficiency。
- LoyaltyEfficiency。
- TaxLevel。
- TaxRate。
- TaxLoyaltyEffect。
- TaxProsperityEffect。
- AI 税率调整原因。

遵守现有日志过滤规则，避免每天对所有 AI 城镇产生大量无意义日志。

---

# 第七阶段：编译、集成与回归验收

## 27. 编译检查

至少编译：

- ModifiedArmy
- ModifiedPolitics

不得因为缺少环境依赖就删除游戏程序集引用或改变项目目标框架。

如果环境中缺少 Bannerlord DLL，明确标记编译阻塞，并通过静态检查、可独立运行的纯数学测试尽可能验证代码。

不得声称未执行的测试已经通过。

## 28. 核心测试用例

### 28.1 人口事务

测试：

- Hearth消耗按Tier正确计算。
- 1.25、1.5等小数人口成本正确扣除。
- 招募后不突破30%民生保护线。
- 不同Tier混合招募正确扣除总Hearth。
- 多入口连续招募共享真实人口。
- 招募失败正确补偿。
- 重复补偿不重复返还。
- 跨日职业兵补偿继续受到原有安全保护。
- 驻军调拨和职业兵升级不重复扣除Hearth。

### 28.2 Hearth和训练效率

测试人口比例：

- 低于30%。
- 等于30%。
- 33.3%。
- 35%。
- 40%。
- 高于40%。

测试不同忠诚度状态及本体模型阈值变动。

确保人口、忠诚度、文化、官职效率正确组合且不重复应用。

### 28.3 税率效果

对五档税率逐项测试：

- 实际税收倍率。
- 忠诚度日变化。
- 繁荣度日变化。
- 原版税收修正。
- 官职税收加成。
- 封邑兵税收减免。
- 城镇与城堡。
- 经济模型与财政 UI 一致性。

Hearth 繁荣度上限继续生效。

### 28.4 AI 税率

至少覆盖：

1. 财政健康、忠诚度正常。
2. 财政亏损、忠诚度正常。
3. 财政危机、处于叛乱风险。
4. 极高税率导致民生保护启动。
5. 降税后忠诚度刚超过风险阈值。
6. 忠诚度恢复到正常区间但不足7天。
7. 连续安全7天后退出保护。
8. 再次加税需要明确财政优势。
9. 同日重复事件不重复评估。
10. 存档加载后状态一致。
11. 玩家封邑不被 AI 自动修改税率。

尤其关注防横跳：

**不允许只因忠诚度在风险阈值附近变化，就发生连续数日高税、低税反复切换。**

### 28.5 旧存档

准备旧版存档，至少包括：

- 高征兵压力城镇。
- 历史压力超过 Hearth 的城镇。
- 不同类型人口事务。
- 已存在大量职业兵的存档。

验证：

旧存档加载 → Hearth 不变 → 历史压力不生效 → 招募正常 → 新槽保存 → 重启加载 → 人口和政策状态一致。

禁止无依据地重置历史 Hearth、已招募士兵或其他存档数据。

### 28.6 长期平衡

至少观察30、60、120游戏日：

- 七文化职业兵补员。
- Hearth 变化。
- 最低民生人口约束。
- 城镇与城堡繁荣度。
- Clan 财政收入和支出。
- AI 税率分布。
- AI 税率调整次数。
- 叛乱风险与税率关系。

长期测试是平衡分析，不得自行修改已确定的五档税率参数。

## 29. 交付要求

完成全部阶段后提供：

1. 最新 Git HEAD 和最终提交记录。
2. 全部修改文件清单。
3. 新增、删除及弃用接口清单。
4. 新职业兵 Hearth 成本与训练效率实现说明。
5. 五档税率及 AI 决策说明。
6. 旧存档迁移方式。
7. 编译、测试与运行结果。
8. 已知风险和未完成事项。

尽可能按阶段提交独立 Git Commit，最后确保整个任务处于一致、可编译状态。

每个阶段完成后继续下一阶段，不需要重新等待用户输入施工任务。

**最终验收目标：**

Hearth 决定繁荣度支撑与潜在兵源；职业兵按Tier真实消耗Hearth；忠诚度和人口规模调节职业兵产能；税率形成财政收益与民生代价；AI每日根据当前状况决定税率并避免反复横跳；新旧存档正常运行；所有UI与真实数据一致。

---

## 30. 重点检查：跨模块训练加成与历史事务

- 当前 `MilitaryOfficeRecruitmentPatches` 使用 Harmony Postfix 修改 `GetDailyProduction` 的结果。必须保证人口效率、忠诚度效率和官职加成正确组合，不重复应用，且预测展示真实最终数值。
- 当前旧职业兵事务的 `Amount` 表示招募人数，不可直接改为新的 Hearth 消耗量；须使用兼容字段区分历史人数与新消费，保证旧档补偿正确。

**统一交付：前五阶段修改业务机制，第六阶段才修改 UI，第七阶段完成整体回归。**
