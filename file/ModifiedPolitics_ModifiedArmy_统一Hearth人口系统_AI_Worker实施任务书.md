# ModifiedPolitics / ModifiedArmy — 统一 Hearth 人口系统实施任务书

## 0. 任务性质与代码基准

仓库：`GuihuaZhou/bannerlord`

本次需求：实现统一定居点人口机制，修复城镇、城堡职业兵过度依赖附属村庄 Hearth 的问题。

实施前必须：

1. 读取根目录 `AGENTS.md`，以及相关模块的适用规则。
2. 确认本地分支、HEAD、`git status --short`，不要覆盖未提交修改。
3. 以执行时最新源代码为准，不以旧设计 MD 或历史 Worker 报告为准。
4. 对照 `code/` 中本体反编译源码，不使用 API 文档推断本体行为。
5. 分阶段实施、编译、验证，不得仅凭编译成功宣称功能验收完成。
6. 未经要求，不自动提交、推送或创建 PR。

核对时的 GitHub 基准提交：`dddd8b8cc3f1572cc31e4427f6d3d8f8352da3e0`。

## 1. 不可改变的设计要求

### 1.1 人口数据归属

- `Village` 继续使用原版 `Village.Hearth`。
- `Town` 和 `Castle` 新增 Mod 层 Hearth，存储于 ModifiedPolitics。
- ModifiedPolitics 是人口规则、增长、消耗、补偿及持久化的唯一业务管理者。
- ModifiedArmy 仍管理职业兵训练、封邑兵供养、动员记录与军事事务。
- 所有 Mod 主动发起的 Hearth 增减必须经过 ModifiedPolitics 人口服务。
- 原版 `Village.DailyTick()` 仍执行底层自然增长写入，但增长规则由 ModifiedPolitics 模型控制。
- 不允许新建重复的村庄人口权威数据。

### 1.2 Hearth 初始值与上限

| 类型 | 城墙等级 | 初始 Hearth | 自然承载上限 |
|---|---:|---:|---:|
| Town | 1 | 500 | 1500 |
| Town | 2 | 1000 | 3000 |
| Town | 3 | 1500 | 4500 |
| Castle | 1 | 200 | 600 |
| Castle | 2 | 400 | 1200 |
| Castle | 3 | 600 | 1800 |
| Village | 不适用 | 保留原版初始值 | 1200 |

- 初始值与上限全部 XML 可配置。
- 城镇、城堡正常城墙等级按 1～3 处理。
- 使用本体 `Town.GetWallLevel()` 读取城墙等级。
- 新战役初始化、旧存档首次迁移时设置初始 Hearth。
- 之后升级、降级城墙仅改变上限，不重置当前 Hearth。
- 不强制截断超出上限的人口。
- 村庄 1200 仍为自然增长软上限。

### 1.3 人口与繁荣度

唯一方向：**Hearth → Prosperity**。

繁荣度不反向决定 Hearth 增长。

当当前繁荣度超过人口支持能力时，必须逐步衰退；低于支持能力时仍由原版经济模型决定实际增长。

### 1.4 招募人口来源

| 行为 | Hearth 来源 |
|---|---|
| 城镇职业兵招募 | 城镇自身 Hearth |
| 城堡职业兵招募 | 城堡自身 Hearth |
| 村庄士兵招募 | 该村 Hearth |
| 封邑兵供养 | 附属村庄 Hearth |
| 封邑兵动员与返乡 | 附属村庄 Hearth 与原有 MobilizedHearth 台账 |

职业兵永久征召消耗和封邑兵临时动员不可混淆。

保留当前 `militaryConfig.xml` 的职业兵招募成本：T1～T6 每成功征召一名，消耗 1 Hearth。此次不调整职业兵等级成本或文化军事制度。

## 2. 人口自然增长模型

### 2.1 统一公式

设：

- `H`：当前实际 Hearth。
- `M`：村庄已登记、在外动员的 Hearth；城镇、城堡为 0。
- `E = max(0, H + M)`：有效人口。
- `K`：定居点当前自然承载上限。
- `r = 0.004`：增长系数。
- `S = 100`：低人口恢复基础量。
- `L = 2.0`：低人口最大恢复倍率。
- `T = 0.5`：低人口恢复加成结束时的上限占比。

基础增长：

`BaseGrowth = r × (E + S) × (1 - E / K)`

低人口倍率：

`Multiplier = 1 + (L - 1) × max(0, 1 - E / (K × T))`

最终自然增长：

`NaturalGrowth = BaseGrowth × Multiplier`

要求：

- 低人口增长为较小正值，最多获得 2 倍恢复加成。
- `E >= K × 0.5` 时，倍率为 1。
- `E = K` 时，自然增长为 0。
- `E > K` 时，自然增长可以为负数。
- 低人口恢复倍率不放大负增长。
- 征兵损失不参与自然增长倍率计算。
- 保留原版其他合法政策、建筑、文化、事务修正，不丢失原有负面效果。
- 不再叠加旧的 225 分母三次方自然增长曲线与旧四次方衰减，避免重复修正。

### 2.2 村庄参考结果

村庄上限 1200 Hearth：

| Hearth | 预期每日基础自然增长 |
|---:|---:|
| 10 | +0.87 |
| 50 | +1.10 |
| 100 | +1.34 |
| 300 | +1.80 |
| 600 | +1.40 |
| 900 | +1.00 |
| 1200 | 0 |
| 1300 | -0.47 |

计算时应使用浮点数，不要每日取整。

村庄有效人口必须继续包含 `MobilizedHearth`，防止封邑兵离乡后重复产生人口。

### 2.3 城镇、城堡参考结果

各等级按规定初始 Hearth 初始化时：

| 定居点 | 初始每日自然增长 |
|---|---:|
| 1级城镇 | +2.13 |
| 2级城镇 | +3.91 |
| 3级城镇 | +5.69 |
| 1级城堡 | +1.07 |
| 2级城堡 | +1.78 |
| 3级城堡 | +2.49 |

这是自然增长，不包括职业兵征召消耗。

## 3. Hearth 对繁荣度的支持机制

### 3.1 支持能力

`SupportedProsperity = Hearth × ProsperityPerHearth`

推荐默认参数：

- Town：`ProsperityPerHearth = 5`
- Castle：`ProsperityPerHearth = 3`

上述数值 XML 可配置。

### 3.2 每日繁荣度变化

设：

- `P`：当前 Prosperity。
- `S`：Hearth 可支持的 Prosperity。
- `B`：原版模型计算的每日 Prosperity 变化。
- `D = 84`：超额繁荣度的调整周期。

当 `P < S`：

`ActualChange = min(B, S - P)`

当 `P >= S`：

`ActualChange = min(B, 0, -(P - S) / D)`

要求：

1. 保留原版模型的粮食、市场、建筑、政策、忠诚度等全部计算。
2. 在计算结果上施加人口支持约束。
3. 人口不足时允许繁荣度逐渐衰退，不能瞬间截断。
4. 如果原版已有更严重的负增长，保留该负增长，不重复叠加相同损失。
5. 人口支持能力提高时，只解除限制，不凭空增加繁荣度。
6. 繁荣度不得反向影响 Hearth 增长。
7. `ExplainedNumber` 中应增加可本地化的“人口支持不足”原因，供 UI 解释。

特别检查旧存档：初始人口可能无法支持已有高繁荣度，允许合理衰退，但不得瞬间清空经济数据。

## 4. XML 配置

修改：

`ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml`

推荐新增：

```xml
<Population>
  <NaturalGrowth
      rate="0.004"
      seed="100"
      lowPopulationMultiplier="2.0"
      lowPopulationThreshold="0.5" />

  <Village capacity="1200" />

  <Town>
    <Wall level="1" initial="500" capacity="1500" />
    <Wall level="2" initial="1000" capacity="3000" />
    <Wall level="3" initial="1500" capacity="4500" />
  </Town>

  <Castle>
    <Wall level="1" initial="200" capacity="600" />
    <Wall level="2" initial="400" capacity="1200" />
    <Wall level="3" initial="600" capacity="1800" />
  </Castle>

  <ProsperitySupport
      townPerHearth="5"
      castlePerHearth="3"
      excessDecayDays="84" />
</Population>
```

保留当前：

`Food/ProsperityConsumptionDivisor = 34`

修改 `SettlementEconomyConfig.cs`，读取、验证所有参数。

配置验证要求：

- 所有 Hearth 初始值非负。
- 上限必须大于 0。
- 初始值不能高于对应上限。
- 等级 1、2、3 配置必须完整且不可重复。
- 自然增长系数非负。
- 低人口倍率不得小于 1。
- 低人口阈值必须位于 `(0,1]`。
- Prosperity 支持比例与调整周期必须大于 0。
- 使用 InvariantCulture 解析浮点数。
- 错误配置必须报告具体节点和原因，不允许悄悄使用错误数值继续结算。

## 5. ModifiedPolitics 人口数据与服务

在现有 `SettlementEconomy` 业务域内建立数据、计算、行为和事务服务。

建议职责：

- `Models`：纯计算人口增长、人口上限、繁荣度支持。
- `Behaviors`：每日定居点事件、人口初始化和存档。
- `Services`：人口查询、扣除、恢复、事务补偿。
- `Config`：XML 参数、验证、配置对象。
- `Presentation`：只读人口数据和变化说明。

具体类名可根据现有组织结构调整，不要求机械照搬。

### 5.1 数据存储

- 村庄直接使用 `Village.Hearth`，不得创建第二份持久化 Hearth。
- 城镇、城堡建立 Campaign 级人口数据。
- 使用稳定的 `Settlement` 引用或稳定 ID 关联存档，遵守 TaleWorlds SaveSystem 的已注册类型与容器要求。
- 不得覆盖既有 SaveId 或 `SyncData` Key。
- 不得通过进程级静态字典永久保存 Campaign 对象。

### 5.2 初始化

- 通过 `Town.GetWallLevel()` 获取城墙等级。
- 在建筑数据已初始化、可访问后建立人口记录。
- 首次建立记录时才写入 XML 初始 Hearth。
- 旧存档缺少记录时只补充缺失的城镇、城堡。
- 已有记录必须原样恢复。
- 同一存档重复加载不能再次初始化人口。
- 遇到异常的 0 级或缺失城墙建筑时，不得静默将人口设为 0，应有明确兼容策略和日志。

### 5.3 每日结算

- 村庄继续使用本体 `Village.DailyTick()` 和 ModifiedPolitics 的 `CalculateHearthChange()`。
- 城镇、城堡通过新的 CampaignBehavior 执行 Hearth 每日增长。
- 每个定居点每个游戏日最多执行一次自然增长结算。
- 重复事件、重复进入菜单、UI 查询不得造成重复人口增长。
- 不要为了人口系统直接替换原版完整 `Town.DailyTick()`。

### 5.4 人口事务

提供统一的业务能力：

- 查询当前 Hearth。
- 查询当前自然承载上限。
- 查询职业兵可动员人口。
- 申请人口扣除。
- 提交扣除事务。
- 失败时精确补偿。
- 查询事务结果与恢复状态。

使用带操作标识的事务结果，而不是简单返回 `bool` 后让调用者自行推测恢复量。

发生补偿失败时，不得静默继续修改 Hearth；应记录可恢复故障并阻止受影响事务继续执行。

## 6. ModifiedArmy 跨模块调用改造

### 6.1 必须保持单向依赖

当前依赖方向为：

`ModifiedPolitics → ModifiedArmy`

不得反向增加 ModifiedArmy 对 ModifiedPolitics 的编译引用或 SubModule 依赖。

推荐在 ModifiedArmy 的集成契约层定义人口服务接口，由 ModifiedPolitics 提供实现。

运行期桥接不持有第二份人口台账，且必须具备 Campaign 生命周期隔离和清理机制。

无人口服务时，相关征召事务应明确拒绝或采用经过明确批准的兼容策略，不能悄悄恢复旧的村庄扣费逻辑。

### 6.2 职业兵消耗来源改造

当前关键位置：

`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`

方法：

- `TryConsumeProfessionalHearth`
- `RollbackProfessionalHearth`
- `OnTroopRecruited`

改造后：

- Village：扣本村 Hearth。
- Town：扣本城镇 Hearth。
- Castle：扣本城堡 Hearth。
- 不再使用城镇、城堡的 `BoundVillages` 进行职业兵扣费。
- 保留职业兵每人 1 Hearth 的现行成本配置。
- 每次实际招募成功只扣一次，兵种升级、调拨、战死不重复扣除，也不返还。

### 6.3 必须拆分职业兵与封邑兵生产来源

这是本任务的重点，不能遗漏。

当前文件：

`Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

目前：

- `CalculateVillageSupply()` 计算村庄供给。
- `CalculateSettlementSupply()` 汇总附属村庄供给。
- `GetProductionForecast()` 将同一份 SharedSupply 分配给职业兵与封邑兵。
- `EnsureDailyProduction()` 在 `SharedSupply <= 0` 时直接返回。

因此即使修改人口扣除来源，只要村庄全部被毁，职业兵仍会停产。

必须拆成两条生产路径：

**职业兵**

- 使用城镇、城堡自身 Hearth 的可征召余量。
- 受原有训练能力、兵营、文化效率、军事制度、驻军容量约束。
- 不因为附属村庄 Hearth 低于 100 或村庄全部被劫掠而自动停产。
- 不得超出本地可消耗 Hearth。

**封邑兵**

- 保留现有村庄供给、文化制度、供养预算及封邑生产规则。
- 继续受附属村庄损失影响。
- 不使用城镇或城堡新增 Hearth 来供养封邑兵。

原 `ProfessionalShare`、文化训练速度、每日生产进度等逻辑应进行逐项审计，保留现有军事制度效果，不应因简单移除 SharedSupply 而无意放大职业兵产量。

生产预测必须是只读行为；人口扣除只能发生在最终实际招募事务中。

### 6.4 封邑兵人口写入点改造

至少审计：

- `Fief/Models/VillageFiefData.cs`
- `Fief/Models/PartyData/FiefPartyRecruitment.cs`
- `Fief/Models/PartyData/FiefPartyReturn.cs`
- `Fief/Models/FiefPartyManager.cs`
- `Fief/Services/FiefTroopConversionService.cs`

现有 `VillageFiefData` 在动员和返乡时会直接增减 `Village.Hearth`。

要改成通过 ModifiedPolitics 人口事务服务修改实际 Hearth，但保留 ModifiedArmy 对 `MobilizedHearth`、封邑兵批次、Roster 和 `RecoveryRequired` 的业务所有权。

确保：

- 封邑兵动员：实际 Hearth 减少，MobilizedHearth 等量增加。
- 正常返乡：实际 Hearth 恢复，MobilizedHearth 等量减少。
- 战损确认：释放对应动员记录，不返还死者 Hearth。
- 伤兵不视为死亡。
- 失败回滚恢复原始 Hearth、Roster、服务批次和动员记录。
- 原有 `Hearth + MobilizedHearth` 有效供养语义保持。
- 不能修改已有存档字段 ID，不能因转移职责丢失现有服务批次。

特别审计原版招募事件 `OnTroopRecruited`：该事件在招募行为发生后触发，不能仅凭兵种类型删除士兵来假定能精确撤销本次招募。必须核实 Roster 前后状态和补偿边界，避免误删已有士兵。

## 7. 实施阶段与交付门禁

### 阶段 A — 基准审计与接口设计

核实最新 HEAD、调用链、全部 Hearth 写入点、存档类型、依赖图。

交付：

- Hearth 写入点清单。
- Population Service 契约。
- 数据所有权与事务流程说明。
- 风险与回滚策略。

此阶段不改人口数值。

### 阶段 B — ModifiedPolitics 人口基础

完成：

- XML 配置。
- 城镇、城堡人口数据。
- SaveSystem 注册与存档。
- 初始化、迁移。
- 只读人口查询。
- 城墙等级对应上限。

验证后再继续。

### 阶段 C — 自然增长与繁荣度

完成：

- 新统一自然增长公式。
- 村庄低人口 2 倍恢复。
- 城镇、城堡每日增长。
- Hearth 支持 Prosperity 约束。
- `ExplainedNumber` 说明与本地化。

核对计算表，与预期值一致。

### 阶段 D — ModifiedArmy 征召整合

完成：

- 跨模块人口事务桥接。
- 城镇、城堡职业兵改用自身 Hearth。
- 职业兵、封邑兵每日生产来源拆分。
- 封邑兵动员及返乡改用人口事务。
- 转换和自动驻军招募的完整回滚。
- UI 预测与实际结果一致性校验。

不得改变军队文化、训练、编制等无关业务规则。

### 阶段 E — 验证与报告

运行适用的构建、架构和静态检查，并提供实机测试矩阵。

至少覆盖：

| 场景 | 预期结果 |
|---|---|
| 新战役初始化 1～3 级城镇 | Hearth 与 XML 一致 |
| 新战役初始化 1～3 级城堡 | Hearth 与 XML 一致 |
| 旧存档首次加载 | 补充人口且不破坏原有数据 |
| 旧存档重复加载 | 不重复初始化 |
| 城墙升级、降级 | 只改变承载上限 |
| 村庄 10 Hearth | 每日自然增长约 +0.87 |
| 村庄 1200 Hearth | 自然增长为 0 |
| 村庄 1300 Hearth | 自然增长约 -0.47 |
| 城镇 Hearth 很低 | 可支持繁荣度下降，繁荣度渐进衰退 |
| 城镇 Hearth 恢复 | 繁荣度增长限制逐步解除 |
| 所有附属村庄被劫掠 | 城镇、城堡仍可凭自身 Hearth 招募职业兵 |
| 城镇或城堡自身 Hearth 不足 | 不允许无人口成本生成职业兵 |
| 职业兵实际招募成功 | Hearth 精确扣除一次 |
| 职业兵招募失败 | Hearth、Roster、生产名额均恢复 |
| 封邑兵动员与正常返乡 | Hearth 与 MobilizedHearth 对账一致 |
| 封邑兵战损、伤兵、冷却 | 不重复返还或损失 Hearth |
| 存档、读档、继续日结算 | 没有丢失、重复结算或重复扣人口 |
| AI 长期战争与驻军补员 | 仍能持续补员，且受真实人口约束 |

必须同步验证：

- ModifiedArmy Release 编译。
- ModifiedPolitics Release 编译。
- 无 Army → Politics 循环依赖。
- 现有架构门禁。
- 关键存档兼容检查。
- 游戏内跨 Daily Tick 和读档测试。

没有游戏内证据的项目标记为 `BLOCKED`，不能标记为 PASS。

## 8. 明确不属于本次任务的内容

除非现有代码执行本方案所必需，否则不得额外修改：

- AI 战争决策。
- 文化军事制度权重。
- 职业兵 Tier 消耗表。
- 封邑兵 Tier 供养表。
- 工资及免薪天数。
- 村庄物品生产与粮食供给公式。
- `ProsperityConsumptionDivisor = 34`。
- 新的城镇难民、移民系统。
- 新的独立人口阶层或职业人口池。
- 其他军事、外交及家族功能。

本阶段只建立人口资源的基础运行机制，不同时扩大成完整人口经济模拟器。

## 9. Worker 最终交付要求

报告必须包含：

1. 实际使用的 Git HEAD SHA 和工作区状态。
2. 实际改动文件及主要功能。
3. 每条 Hearth 写入路径的权威所有者与事务关系。
4. XML 实际生效的默认值。
5. 数值公式及关键验证结果。
6. 两个 Mod 的依赖关系检查结果。
7. 存档迁移与失败回滚验证结果。
8. 编译、架构门禁、测试的 PASS / FAIL / BLOCKED 清单。
9. 尚未验证的风险、必要游戏内测试步骤。

**完成标准：** 城镇、城堡拥有独立且持久化的 Hearth；村庄正常增长及封邑动员语义保留；繁荣度受实际人口约束；附属村庄全部被毁时职业兵仍可依靠定居点自身人口补充；所有人口变动有单一权威管理者，且没有资源复制、重复扣除、存档损坏或循环依赖。
