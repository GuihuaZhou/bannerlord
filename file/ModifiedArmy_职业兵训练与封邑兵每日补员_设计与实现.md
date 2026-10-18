# ModifiedArmy：职业兵训练与封邑兵每日补员——设计及实现方案

> 状态：设计已讨论同意；**尚未按本文修改代码**。  
> 基准仓库：`GuihuaZhou/bannerlord`，`main` 分支；审阅时最新提交 `e0c351667e1d7a73bc94aaba882bbe046c9451df`（`docs(army): explain military configuration`）。  
> 文档日期：2026-10-17。  
> 代码定位规范：使用**仓库相对路径 + 类/方法/字段名**，不依赖行号。

## 1. 目标与边界

核心目标：**保证 AI 在大多数正常经济、村庄兵源及军事行动情形下，有稳定可用的兵员**，同时保留帝国、阿塞莱、瓦兰迪亚、斯特吉亚、库赛特、巴旦尼亚、诺德的军事制度差异。

本轮确定事项：

1. 保留 `MilitaryDoctrine` 的 `fief`、`professional`、`mercenary` 整数权重。其职责为 AI 兵源偏好、共享基础兵源分配；**不再强制用这些权重推导训练速度**。
2. 保留各文化的 `Professional/Training` 独立配置；调整 `base` 和 `barracksStep` 提高/降低对应文化的职业兵训练效率。
3. 保留各文化的 `Fief/Production` 独立配置，增加小数 `dailyRate`；**删除封邑兵每周补员限额**。
4. 保留基础兵源消耗、村庄户数及动员约束、封邑编制、驻军自动招募规则、文化效率、军事官职加成、每日小数进度。
5. 在现有“查看兵源”入口展示职业兵与封邑兵的**预计补员**以及**最近一次每日结算的实际补员**。
6. UI 命名固定：`基础兵源`、职业兵 `基础训练`/`兵营加成`/`文化效率`/`官职加成`/`训练能力`/`预计补员`/`实际补员`；封邑兵 `基础补员`/`当前兵力`/`预计补员`/`实际补员`。
7. 原 `今日实际补员` 改为 **`实际补员`**；原 `封邑兵每日补员能力` 改为 **`基础补员`**。

明确不做：不创建新兵源库存池、不绕过户数消耗、不重写驻军招募模型、不更改 AI 征召行动与调拨规则、不更换 `ModifiedPolitics` 的 Harmony 目标方法、不重建存档结构、不新增界面按钮。

## 2. 七文化军事制度定位与目标速率

下表为**理论产能候选值**，不是承诺每天实际产生/入伍的整数士兵。职业兵以**三级有效兵营、文化效率 100%、无军事官职加成**计算。封邑兵不因兵营等级提升每日 `dailyRate`。

| 文化 | 主要军事特点 | 职业兵城镇（人/天） | 职业兵城堡（人/天） | 封邑/部族兵基础补员（人/天） |
|---|---|---:|---:|---:|
| 帝国 | 军区制；高度依赖常备军，并使用雇佣兵 | **4.00** | **4.00** | 1.25 |
| 阿塞莱 | 伊克塔；常备兵力与雇佣兵并重 | **3.80** | **3.90** | 1.40 |
| 瓦兰迪亚 | **唯一成熟封建制**；大规模高效封建动员 | 1.60 | 1.80 | **4.00** |
| 斯特吉亚 | 王公亲兵与地方征召并重 | 1.50 | 1.65 | 1.90 |
| 库赛特 | 已定居；军事组织程度与斯特吉亚相近，仍有部族动员 | 1.50 | 1.65 | 1.90 |
| 巴旦尼亚 | 依赖部落军事力量，常备组织较弱 | 1.30 | 1.45 | 2.00 |
| 诺德 | 依赖部族兵，**两类补员效率均较低** | 1.10 | 1.20 | 1.65 |

备注：上述文化制度定位来自讨论，不要求仅凭补员速度体现全部制度差异。`Professional` 在 AI 兵源选择中仍代表**常备驻军体系**，不限定调拨兵种身份。雇佣兵由既有制度偏好独立控制，不进入本地基础兵源生产。当前仓库中的 `MilitaryDoctrine` 整数值如需同步修改，应另行核对，不要擅自将旧比例与本轮速率绑定。

### 2.1 建议职业兵训练配置

继续采用 `T_professional = (base + barracksStep × effectiveBarracksLevel) × cultureEfficiency × otherFactors`。

| 文化 | Town base | Town barracksStep | Castle base | Castle barracksStep |
|---|---:|---:|---:|---:|
| empire | 1.30 | 0.90 | 1.45 | 0.85 |
| aserai | 1.25 | 0.85 | 1.35 | 0.85 |
| vlandia | 1.00 | 0.20 | 1.05 | 0.25 |
| sturgia | 1.05 | 0.15 | 1.05 | 0.20 |
| khuzait | 1.05 | 0.15 | 1.05 | 0.20 |
| battania | 1.00 | 0.10 | 1.00 | 0.15 |
| nord | 1.01 | 0.03 | 1.05 | 0.05 |

港口城镇沿用 `Town`，除非现有 `TownPort` 单独配置；如需覆盖，保留原解析支持。实际有效兵营等级受 `ResolveProfessionalProductionRule` 的文化匹配限制，而不是简单地总取建筑物真实等级。`ModifiedPolitics` 的军事官职加成继续作用于 `GetDailyProduction`，不得重复施加。

### 2.2 封邑/部族兵基础补员

| 文化 | `dailyRate` |
|---|---:|
| empire | 1.25 |
| aserai | 1.40 |
| vlandia | 4.00 |
| sturgia | 1.90 |
| khuzait | 1.90 |
| battania | 2.00 |
| nord | 1.65 |

原 `weeklyLimit` 与 `FiefProducedThisWeek` 不再限制日生产。保留 `Fief/Service/returnCooldownWeeks`，它是封邑兵归还冷却时间，与每周生产配额无关。

## 3. 计算定义

### 3.1 基础兵源与分配（不改变原算法）

`S` 为现有 `CalculateSettlementSupply` 计算出的定居点共享基础兵源（人/天）。其村庄贡献由村庄户数、动员占用、封邑在役兵等机制确定；不凭空产生新兵源。

`P = professionalWeight / (fiefWeight + professionalWeight)`；`F = 1-P`。

初始额度：

- `A_professional_initial = min(S × P, T_professional)`，仅在职业兵生产/自动招募允许时计算。
- `A_fief_initial = min(S × F, min(dailyRate, remainingFiefCapacity))`。

保留当前 `GetProductionForecast()` 中**先按权重分配、再将未使用额度转给仍有需求的一侧**的算法。不要简单把初始额度当作最终额度，否则会浪费兵源。

`T_professional` 是考虑基础训练、有效兵营加成、文化效率及外部加成后的最终理论训练能力；其对外读取必须使用原有 `GetDailyProduction(settlement, TroopProductionKind.Professional)`，维持 `ModifiedPolitics` 补丁。

最终预测：

- `ProfessionalAllocation`：预计职业兵**生成候选**的生产额度（浮点人/天），**不是实际入伍**。
- `FiefAllocation`：预计封邑兵直接补员的生产额度（浮点人/天），**不是当日实际补员整数**。
- 若封邑满编，则封邑 `FiefAllocation=0`；若自动招募被关闭，则职业兵预计生产为零；基础兵源不足则两类产能均可能低于理论能力。

### 3.2 每日小数累计与实际补员

保留原 `ProfessionalProductionProgress` 和 `FiefProductionProgress`。小数采用累计而不是四舍五入：例如 1.25 人/天、无其他限制时，4 天总计可产生 5 个整数名额。

区分以下概念：

| 名称 | 定义 | 典型类型 |
|---|---|---|
| 训练能力 | 职业兵全部训练修正后的理论产能 | `float` 人/天 |
| 基础补员 | 封邑兵文化配置 `dailyRate` | `float` 人/天 |
| 预计补员 | 经共享兵源分配及容量等条件限制后的**预计**额度 | `float` 人/天 |
| 实际补员（职业兵） | **真实添加进驻军**的职业兵人数；排除俘虏转化 | `int` 人/次每日结算 |
| 实际补员（封邑兵） | `ProduceDailyFiefTroops()` 实际增加的封邑兵人数 | `int` 人/次每日结算 |

建议 UI 在“实际补员”附近标注 **最近一次每日结算**（必要时提供日期）。如尚无记录，显示“暂无记录”；如确实结算而人数为零，显示“0 人”。不能把预计值当成实际数。

## 4. XML 示例与数据模型

路径：`ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`。

瓦兰迪亚示例（不省略现有封邑成本字段）：

```xml
<Culture id="Culture.vlandia">
  <MilitaryDoctrine fief="7" professional="2" mercenary="1" />
  <Professional>
    <Training>
      <Town base="1.00" barracksStep="0.20" />
      <Castle base="1.05" barracksStep="0.25" />
    </Training>
  </Professional>
  <Fief>
    <Service freeWageDays="42" returnCooldownWeeks="3" />
    <Production>
      <Town baseLimit="120" villageBonus="30" dailyRate="4.00"
            prosperityCostPerTroop="10" hearthCostPerTroop="1" />
      <Castle baseLimit="80" villageBonus="25" dailyRate="4.00"
              prosperityCostPerTroop="4" hearthCostPerTroop="1" />
    </Production>
  </Fief>
</Culture>
```

**注意：示例 `MilitaryDoctrine` 是当前仓库原始值的结构示意，不表示批准维持或恢复该值；本轮不通过它计算训练速度。** 其他六种文化沿用同结构，只修改上述训练参数、`dailyRate`，保留本身的编制、村庄加成、征召成本、免薪与归还规则。巴旦尼亚等文化已有的 `TownPort` 差异不能丢失。

代码 `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`：

- 在 `FiefProductionConfig` 删除生产业务使用的 `WeeklyLimit`，新增 `public float DailyRate { get; internal set; }`。
- 在 `MilitaryConfig.ParseProduction(XmlNode)` 读取并校验非负 `dailyRate`，停止读取 `weeklyLimit`。
- `CulturalMilitaryDoctrine.GetProduction(Settlement)` 继续处理 Castle、TownPort、Town 回退。
- `ProfessionalTrainingConfig`、`CulturalMilitaryDoctrine.GetTraining()`、`MilitaryConfig.GetDoctrine()` 保留。
- 启动时严格校验七种文化的生产参数完整；不建议因缺字段静默回退到无效产能。

## 5. 逐文件实现方案

### 5.1 共享兵源生产行为

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`。

**A. 修改 `GetProductionForecast(Settlement)`**

1. 删除计算 `weekKey`、调用 `ResetWeekIfNeeded()`，删除 `weeklyLimit`/`weeklyRemaining` 参与需求计算。
2. 获取 `FiefProductionConfig.DailyRate`：`fiefDemand = min(dailyRate, max(0, capacity-current))`。
3. 原 `professionalDemand` / `GetDailyProduction` 保留，原共享兵源分配及剩余额度再分配逻辑保留。
4. 输出 `FiefBaseDailyRate`（替代原 `FiefWeeklyLimit`、`FiefProducedThisWeek`）及现有预测字段。
5. `GetProductionForecast` 应尽量保持**无状态副作用**；此前其更新周计数器的行为一并移除。

**B. 修改 `OnDailySettlementTick(Settlement)`**

1. 删除周键计算、重置、周剩余限制及 `RecordFiefProduction`。
2. 保留小数累计，得到 `requestedFiefCount`；调用 `FiefPartyData.ProduceDailyFiefTroops(requestedFiefCount)`，使用返回的 `actualFiefCount` 写入本日统计。
3. 保留生成职业兵候选供驻军招募的执行流程。**候选不等于实际入伍**。
4. 对符合结算范围的定居点，即使没有可用兵源、没有可补封邑人数，也应为本次结算记录封邑实际补员 `0`；避免 UI 误把前一天的结果当作今天。
5. 如候选兵种或实际生产失败，应检查进度退款/延后消耗策略，避免整数生产额度静默丢失；不要让已经消费的兵源重复消费。

**C. 小数精度**

- 保留 `ConvertProgressToCount(...)` 以及原小数进度字段；新增日速率不能简单 `int` 截断。
- 若封邑生产申请量与实际成功人数不同，应明确是否退还失败量对应的生产进度；退款时仍须遵守不重复占用/释放基础兵源的原则。

### 5.2 封邑兵真正补充结果

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`。

- 保留 `FiefPartyData.ProduceDailyFiefTroops(int approvedCount)` 的入口和返回值语义，即“实际新增人数”。
- 当前已有 `_totalTroopCount` 变化比较计算实际数。无需重写封邑生成流程。
- 从 `SettlementManpowerProductionBehavior.OnDailySettlementTick()` 记录**返回的实际人数**，不要记录 `approvedCount`。

### 5.3 职业兵实际入伍统计

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`。

- `OnDailySettlementTick(Settlement)` 已有 `professionalRecruited`、`prisonerRecruited`，应在执行结束后向兵源生产行为/数据模型上报 `professionalRecruited`。
- `ExecuteUnifiedPlan(...)` 的职业兵分支在 `TryConsumeProfessionalOffer` 成功，并向 `garrison.MemberRoster.AddToCounts` **完成真实入伍**之后递增 `professionalRecruited`，继续作为统计唯一来源。
- 不得将 `professionalOffered` 或 `recruited`（包含俘虏）记作职业兵实际入伍。
- 在驻军不存在、自动招募关闭、粮食不足、围城、事件阻断、没有名额等提前返回分支，仍要正确记当日职业兵实际入伍 `0`（在验证结算适用定居点后）；避免 UI 残留旧日报数。
- **每日事件顺序审计**：`Main.OnGameStart()` 目前注册 `SettlementManpowerProductionBehavior` 后注册 `GarrisonRecruitFromPrisonersBehavior`，但设计不能仅依赖未经验证的回调顺序。至少测试职业候选是否已经生成、每日是否只执行一次，必要时引入每定居点日键的幂等/阶段协调。

### 5.4 持久化真实统计（不破坏旧档）

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/SettlementManpowerProductionData.cs`。

已有保存属性：

- `ProfessionalProductionProgress` — `SaveableProperty(3)`，**必须保留**。
- `FiefProductionProgress` — `SaveableProperty(4)`，**必须保留**。
- `FiefProducedThisWeek` — `SaveableProperty(5)`，暂保留仅为**旧存档兼容**，业务停止使用。
- `WeekKey` — `SaveableProperty(6)`，暂保留仅为**旧存档兼容**，业务停止使用。

建议增加不冲突的 `SaveableProperty` 编号（实际分配前核对当前存档 Schema）：

```csharp
// 属性示意，编号须与当前 SaveDefiner/Schema 核对。
public int LastProfessionalRecruited { get; private set; }
public int LastFiefReplenished { get; private set; }
public int LastProfessionalRecordedDay { get; private set; } = -1;
public int LastFiefRecordedDay { get; private set; } = -1;
```

推荐按**兵源类别分别记录结算日期**，而不是只存一个共用的 `LastRecordedDay`：职业兵驻军实际结算和封邑兵生产发生在两个行为中，时间/完成状态可能不同；类别独立日期更安全。

- `RecordFiefReplenishment(day,actual)`：每日最多写入一次或以同日幂等规则累计，视实际执行次数而定，绝不重复计数。
- `RecordProfessionalRecruitment(day,actual)`：真实完成后写入；正常零招募也需写 0。
- UI 各自读取最近一次已完成的 `day`，必要时分行说明“最近一次每日结算”，避免把一方今天、一方昨天的数据误认为同日。
- 既有 `SyncData("_settlementRecruitmentPools", ...)` 键以及 `SettlementManpowerProductionData` 类型注册/保存类型编号不变。
- 老存档新字段缺失可能获得默认 0；**不要单纯以 0 推断“已记录”**。加载兼容处理确保日期/有效标志可区分“暂无记录”和“确实为 0”。

### 5.5 预测 DTO

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs`。

- 删除运行/展示使用的 `FiefProducedThisWeek`、`FiefWeeklyLimit`。
- 新增 `float FiefBaseDailyRate`；保留 `FiefAllocation`、`FiefDemand`、`FiefCurrent`、`FiefCapacity`。
- 保留 `ProfessionalTrainingBase`、`ProfessionalTrainingStep`、`ProfessionalTrainingCapacity`、`ProfessionalAllocation` 等。
- 建议新增 `EffectiveBarracksLevel`（或同等数据），以区别 UI 需要的有效兵营等级与建筑真实等级。
- 建议新增/派生 `RawProfessionalTrainingBeforeExternalEffects`，便于 UI 说明文化效率与外部补丁；`ProfessionalTrainingCapacity` 应始终反映 `GetDailyProduction()` 的**实际最终返回值**。
- 实际补员统计是**已发生事实**，建议不进入纯预测计算 `GetProductionForecast()`，由 UI 单独读取日报快照。

### 5.6 UI：完善现有“查看兵源”

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs`。

修改 `InitializeReinforcementRateMenu(MenuCallbackArgs)`：

```text
查看兵源 · [定居点名称]
基础兵源：42.50 /天

职业兵
基础训练：1.30 /天
兵营加成（有效3级）：+2.70 /天
文化效率：100%
官职加成：0%（无相关 Mod 时可隐藏或显示无）
训练能力：4.00 /天
预计补员：4.00 /天
实际补员：3 人（最近一次每日结算）
自动招募：开启

封邑兵
基础补员：1.25 /天
当前兵力：42 / 60
预计补员：1.25 /天
实际补员：1 人（最近一次每日结算）
```

- 以上均是**示意数值**，不能硬编码在实际界面。
- UI 不显示“本周补员 / 每周上限”；也不必须显示三种军事制度整数权重。
- “训练能力”：职业兵基础训练、兵营、文化效率以及官职等修正后的**最终理论数**。
- “预计补员”：调用同一套 `GetProductionForecast()` 算法返回的小数人/天；职业兵是预计候选生产额，不保证入伍。
- “实际补员”：真实完成的**整数人/次已完成的每日结算**；未结算显示“暂无记录”，不能把预测人数当真实人数。
- “封邑基础补员”：直接读取 `dailyRate`，**不叫训练能力**。
- 若希望识别瓶颈，可追加一行如“限制：基础兵源不足 / 封邑已满 / 驻军招募关闭 / 粮食不足 / 招募上限或预算不足”；必须基于真实条件与现有模型推断，不能伪装成诊断结果。
- 有效兵营等级按 `ResolveProfessionalProductionRule` 限制后计算；避免展示等级 3 而实际按等级 1 训练。
- 外部官职补丁由 `ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs` 的 `GetDailyProduction` Postfix 施加。**不得在 ModifiedArmy UI 中再次施加同一加成**。若不能可靠区分多个外部 Mod 的修正，标为“额外加成（含官职）”，不要错误归因。

本地化文件：`ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml`。

- 更新 `ModifiedArmy_ReinforcementRate_Description`；实际中文文本覆盖英语回退文本，仅修改 C# 英文模板是不充分的。
- 调整名称：`共享兵源` → `基础兵源`；`封邑兵每日补员能力` → `基础补员`；`今日实际补员` → `实际补员`。
- 清理 `WEEKLY_CURRENT`、`WEEKLY_LIMIT` 变量与本地化字符串中的周限额内容；保留 UI 入口 `ModifiedArmy_ReinforcementRate_Entry`。

### 5.7 与 ModifiedPolitics 的联动

文件：`ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs`。

- 不改动原 HarmonyPatch 目标 `SettlementManpowerProductionBehavior.GetDailyProduction`、方法签名和 `TroopProductionKind.Professional`。
- `GetDailyProduction()` 的最终返回值必须真实参与 `ProfessionalTrainingCapacity` 计算与 UI。
- 驻军每日招募上限另由 `GetDailyRecruitmentLimit` Postfix 修改；**训练能力 ≠ 当日可成功入伍上限**，可单独在 UI 提示。

## 6. AI 真正可用兵力的配套核验

产能提升不是无条件保证士兵：

1. 职业兵候选生成后可能被经济预算、驻军人数/配比、每日实际招募上限、粮食条件否决。需要比较 `professionalOffered` 与 `professionalRecruited`，明确差距。
2. `BuildDailyProfessionalOffers(...)` 目前对日内单个候选可能长时间偏向 `StringId` 最小的兵种；建议修复低日产量下的跨日权重选择偏差，保持兵种模板比例。属于配套修复，独立测试。
3. 生成候选时若已经消耗进度、之后未能招募，隔天候选消失，存在产能损失；建议先通过真实统计量化，再决定有限重试或进度返还。不能直接形成不消耗新的村庄户数的隐性兵源库存。
4. AI 调拨现有门槛：城镇驻军通常需超过 **125** 人、城堡超过 **75** 人才允许调拨；封邑兵虽可恢复，也需要 AI 决策征召。这些机制目前保持不变，但必须在 30～60 游戏日压力测试里评估。
5. 兵营三级时驻军基础每日自动招募上限为 `1 + barracksLevel = 4` 人；帝国职业兵理论 4 人/天与当前基础上限一致，阿塞莱约 3.8～3.9 人/天。额外官职补丁或俘虏招募可能争夺名额。

## 7. 推荐实施阶段

### 阶段 A：纯生产规则

- 更新七文化 `Professional/Training` 数值。
- 更新七文化 `Fief/Production` 为 `dailyRate`，删除 `weeklyLimit`。
- 更新 `FiefProductionConfig`/`ParseProduction`。
- 更新 `GetProductionForecast`/`OnDailySettlementTick`，彻底取消每周生产限制。
- 验收：配置成功加载；小数进度准确；兵源不足与满编仍按原规则阻断；不重复计算。

### 阶段 B：实际补员日报

- 记录职业兵真正入伍和封邑真正新增人数。
- 按定居点、种类、结算日存储；正确处理实际零补员和旧存档暂无记录。
- 验收：真实新增数量与 UI 记录一致；加载旧存档不丢进度、不抛异常。

### 阶段 C：UI

- 替换原“查看兵源”正文与中文本地化。
- 展示有效兵营等级、基础训练、兵营加成、文化效率、外部官职加成、最终训练能力、预计及实际补员。
- 删除周配额相关展示。
- 验收：修改菜单不引入计算副作用；关闭自动招募、兵源不足、封邑满编时显示合理。

### 阶段 D：真实补员有效性

- 测试候选生成、驻军实际入伍与候选丢失。
- 修复低日产职业兵模板选择偏差；统计并评估候选失败成本。
- 监测 AI 野战部队调拨、封邑征召实际成效，并根据数据微调 `base`、`barracksStep` 与 `dailyRate`，而不是直接改变军事制度权重。

## 8. 测试矩阵与验收标准

| 场景 | 验收项 |
|---|---|
| 帝国三级有效兵营、无额外官职 | 城镇职业兵理论训练能力 4.00 人/天 |
| 阿塞莱三级有效兵营 | 城镇 3.80、城堡 3.90 人/天 |
| 瓦兰迪亚三级有效兵营 | 城镇职业兵 1.60；封邑基础补员 4.00 人/天 |
| 斯特吉亚 / 库赛特 | 同等级、同文化效率条件下训练参数一致 |
| 巴旦尼亚 / 诺德 | 职业兵产能低于斯特吉亚；诺德两种补员能力均较低 |
| 异文化占领、有效兵营限制 | 训练能力与实际有效等级、文化效率一致，UI 不错误展示原建筑等级带来的额外产能 |
| 无可用共享基础兵源 | 预计补员降低乃至 0，实际不凭空生成 |
| 封邑满编 | `FiefAllocation` 为 0；实际补员为 0 |
| 自动招募关闭 / 缺粮 | 职业兵实际入伍为 0，UI 明确显示 |
| 小数速率连续多日 | 累计结果符合浮点进度逻辑，不每天取整丢失尾数 |
| 本次结算 0 人、前次结算非 0 | UI 显示本次 0，不残留前一天实际补员 |
| 旧存档加载 | 旧进度和存档键保持有效；日报暂无记录时不伪装成零或错误旧日数据 |
| 同时加载 ModifiedPolitics | 军事官职加成仅计算一次，驻军招募上限与训练能力概念独立 |
| 30～60 游戏日观察 | 记录 AI 驻军储备、实际训练入伍、封邑兵库存、征召成功率与野战军补充情况 |

## 9. 关键风险与实现注意事项

- **周统计存档字段**：移除它们的业务用途即可，保留旧保存属性/编号以兼容存档；不要把 `FiefProducedThisWeek` 直接改名复用于“实际补员”。
- **每日事件先后**：两个 Behavior 均订阅 `DailyTickSettlementEvent`。实际补员应分系统按日记录并保持幂等，不能假设菜单打开时两者总已结算完。
- **候选 ≠ 入伍**：职业兵必须在驻军 `MemberRoster.AddToCounts` 成功后计入真实入伍，俘虏转换不得混入。
- **训练能力 ≠ 入伍上限**：驻军人数限制、粮食、工资预算和招聘模型仍然独立生效。
- **封邑生产失败**：`ProduceDailyFiefTroops` 可少于申请数，必须用返回值统计，并审视进度退款规则。
- **外部加成显示**：保持 `ModifiedPolitics` HarmonyPatch 目标签名稳定；若有其他 Mod 修改返回值，UI 不应把差值一概错误归因于官职。
- **平衡参数和军事依赖分离**：保留文化军事特性，但不要将 `Professional/Training` 与 `Fief/Production/dailyRate` 简化为 `MilitaryDoctrine` 权重的一元函数。

## 10. 修改文件索引

| 仓库相对路径 | 类 / 方法 / 字段 | 改动 |
|---|---|---|
| `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` | `Professional/Training`, `Fief/Production` | 训练数值、`dailyRate`、移除 `weeklyLimit` |
| `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` | `FiefProductionConfig`, `MilitaryConfig.ParseProduction` | 配置定义与解析 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | `GetProductionForecast`, `OnDailySettlementTick`, `ConvertProgressToCount` | 取消每周上限、每日生产、记录封邑结果 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs` | `FiefPartyData.ProduceDailyFiefTroops` | 复用实际增员返回数 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | `OnDailySettlementTick`, `ExecuteUnifiedPlan` | 记录实际职业兵入伍；排除俘虏 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/SettlementManpowerProductionData.cs` | `ProfessionalProductionProgress`, `FiefProductionProgress` 及新日报属性 | 保留存档字段、增加实际补员记录 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs` | `FiefBaseDailyRate`, `ProfessionalTrainingCapacity`, `FiefAllocation` | 预测 DTO 调整 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs` | `InitializeReinforcementRateMenu` | UI 文本、预测及实际结果 |
| `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml` | `ModifiedArmy_ReinforcementRate_Description` | 中文 UI 文案 |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs` | `GetDailyProduction` Postfix、`GetDailyRecruitmentLimit` Postfix | 联调验证，原则上不改签名或重复加成 |

---

**落地判断**：首先完成生产规则与真实补员日报，再用 UI 反映“理论—预测—实际”三个层次；最后依据实际数据判断 AI 的兵员短缺来自生产、驻军准入、经济还是征召决策。这样既能保留七种文化的特色，也便于有依据地微调补员速率。
