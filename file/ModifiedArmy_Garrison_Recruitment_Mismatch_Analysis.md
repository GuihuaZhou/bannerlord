# ModifiedArmy 驻军自动招募预测与实际补员不一致——分析报告

> **状态：问题分析与 Work 排查任务说明；尚未修改代码。**  
> **代码基准：** GitHub 仓库 `GuihuaZhou/bannerlord`，最新核对提交 `fecc9264`（`fecc9264df9c5d27d7a13cba3be7a1511de52efc`）  
> **涉及模块：** `ModifiedArmy`  
> **要求：** **不要在游戏 UI 显示异常原因或调试信息；诊断全部写入 Mod 日志。**  
> **说明：** 以下区分了已核实的代码行为、玩家实测现象和仍需验证的假设。代码定位使用相对路径与方法名称，不依赖行号。

## 一、问题现象

测试定居点：**沃斯特鲁姆**（Vostrum）。

| 项目 | 实际情况 |
|---|---|
| 驻军人数 | 37 人 |
| 基础兵源 | 17.34 人/天 |
| 职业兵训练能力 | 2.00 人/天 |
| 职业兵预计补员 | 2.00 人/天 |
| 职业兵实际补员 | 0 人 |
| 驻军悬浮提示 | 自动招募 `+2`，预期变化 `+2` |
| 自动招募开关 | 已启用 |
| 定居点俘虏 | 用户确认 **0 人** |
| 经过每日结算后 | 用户确认 **驻军人数未增加** |

**问题核心：** Mod 预测能够招募 2 名职业兵，但实际入伍为 0，驻军人数没有变化。

不能简单认定为 UI 显示错误，也不能在尚无完整事务日志时断言根因。需要同时排查**招募执行失败**和**预测口径失真**。

## 二、已确认的代码机制

### 2.1 原版驻军自动招募已被禁用

文件：

`ModifiedArmy/ModifiedArmy/Recruitment/Patches/GarrisonRecruitmentPatch.cs`

- `GarrisonRecruitmentPatch.Prefix()`：阻止原版 `GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange()` 执行。
- `GarrisonRecruitmentPredictionPatch.Postfix()`：替换 `GetGarrisonChangeExplainedNumber()` 中的驻军变化解释值。
- 其中“自动招募”的预测人数取自 `GarrisonRecruitFromPrisonersBehavior.GetPredictedDailyRecruitmentCount()`。

因此，截图的 `+2` **来自 ModifiedArmy 的预测**，不是原版显贵志愿兵自动招募。用户已确认定居点无俘虏，因此对应时点的预测候选应主要来自职业兵生产。

### 2.2 预测与真实招募路径并不等价

**预测路径：**

`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

`GetPredictedDailyRecruitmentCount()`：

1. 检查驻军、开关、粮食、围攻/战斗等前置条件。
2. 通过 `SettlementManpowerProductionBehavior.GetAvailableProfessionalOffers()` 取得当日职业兵候选。
3. 纳入符合条件的俘虏候选（本次玩家已确认无俘虏）。
4. 调用统一招募模型 `RecruitmentModelManager.Model.BuildPlan()`。
5. 用 `GetDailyRecruitmentLimit()` 和模拟可招募人数计算预测值。

此路径**不执行真正的职业兵候选消耗、村庄 Hearth 扣除和驻军名册变更**。

**真实招募路径：**

同文件 `OnDailySettlementTick()` → `ExecuteUnifiedPlan()`：

1. 读取职业兵候选并生成招募计划。
2. 按招募名额与模型评估得到本次实际申请人数。
3. 对职业兵调用 `SettlementManpowerProductionBehavior.TryConsumeProfessionalOffer()`。
4. 该方法进一步调用 `FiefPartyManager.TryConsumeProfessionalHearth()`，按每名职业兵 **2 Hearth** 的经济规则尝试消耗村庄户数。
5. 只有消耗成功才调用 `garrison.MemberRoster.AddToCounts()`，并增加 `professionalRecruited`。
6. 最后通过 `SettlementManpowerProductionData.RecordProfessionalRecruitment()` 写入实际入伍数。

**结论：** 预测 `+2` 只说明当前招募模拟认为存在可用名额，不能证明执行事务一定成功。

### 2.3 “实际补员 0”覆盖多种情况

文件：

`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

方法：`OnDailySettlementTick()`。

当前逻辑在每日招募入口先调用：

```csharp
productionData?.RecordProfessionalRecruitment(day, 0);
```

仅在招募流程结束后才记录 `professionalRecruited`。

因此以下情况都会表现为“实际补员 0 人”：

- 前置条件不满足，直接返回。
- 没有当天可用职业兵候选。
- `BuildPlan()` 批准人数为 0（例如兵种比例、工资、经济约束）。
- `TryConsumeProfessionalOffer()` 返回 `false`，执行被跳过。
- 因事件顺序或状态变化导致招募时拿不到预测曾看见的候选。

仅看这一整数**无法定位是哪个步骤失败**，应通过日志区分。

## 三、重点排查原因（按优先级）

| 优先级 | 待验证假设 | 核查重点 |
|---|---|---|
| P0 | 生产与招募的每日事件执行顺序异常 | 招募事件执行时，当天候选是否已经生成？读档后是否一致？ |
| P0 | `TryConsumeProfessionalOffer()` 失败 | 候选是否存在、数量是否充足、`FiefPartyManager` 是否可用、Hearth 是否满足？ |
| P1 | 招募模型预测批准数与执行数不一致 | `BuildPlan()` 的 `RecruitableCount`、`PrimaryLimit`、实际执行的 `count` |
| P1 | 当日招募已经结束，但预测仍读取剩余候选 | `GetPredictedDailyRecruitmentCount()` 是否缺少当天结算状态判断？ |

目前**不能确认**哪一种是实际根因。

特别注意：**剩余候选导致预测继续显示 `+2`**可以解释 UI 的持续误导，但不能单独解释最初为何招募失败。必须查到一次完整真实执行轨迹。

## 四、日志诊断方案

诊断全部写入现有 `ModLogger`；**不新增任何游戏 UI 异常、失败原因或调试字段**。

### 4.1 职业兵生产阶段

文件：

`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

方法：`OnDailySettlementTick()`。

建议记录：

- `Settlement.StringId`、当前游戏日。
- `ProfessionalTrainingCapacity`、`ProfessionalAllocation`、基础兵源。
- `ProfessionalProductionProgress` 累计前后值。
- `ConvertProgressToCount()` 得到的整数生产人数。
- `BuildDailyProfessionalOffers()` 实际创建的候选总数、具体兵种与数量。
- 未生成候选时的原因：模板缺失、共享兵源为 0、无可生产兵种、招募开关关闭等。

核心验证：**当天是否确实生成了 2 名可用职业兵候选。**

### 4.2 驻军招募入口

文件：

`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

方法：`OnDailySettlementTick()`。

建议记录：

- 游戏日、定居点 ID。
- 自动招募开关、`FoodChange`、围攻/战斗状态、是否存在驻军。
- 驻军现有人数、`PartySizeLimit`、当日招募上限。
- 职业兵候选数、俘虏候选数（本测试应为 0）。
- 统一模型批准人数及 `PrimaryLimit`。
- 所有提前退出路径的准确原因。
- 真实招募结束后的职业兵入伍数、俘虏入伍数与驻军人数。

避免仅写 `actual=0` 而丢失失败上下文。

### 4.3 统一招募执行

方法：`ExecuteUnifiedPlan()`。

按兵种候选记录：

- `Troop.StringId`。
- `RequestedCount`、`RecruitableCount`、本次执行数量。
- `PrimaryLimit`。
- `TryConsumeProfessionalOffer()` 是否成功。
- `MemberRoster` 变更前后数量。

可复用 `RecruitmentEvaluationResult` 已提供的限制值：

- `AllowedByPartySize`
- `AllowedByCombatRole`
- `AllowedByQuality`
- `AllowedByWageLimit`
- `AllowedByRecruitmentCost`
- `AllowedByMaintenance`

若 `Approved > 0` 但 `Actual == 0`，必须能够从日志直接定位执行失败环节。

### 4.4 职业兵候选和 Hearth 消耗

文件：

`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

方法：`TryConsumeProfessionalOffer()`。

建议区分以下失败原因（可用局部枚举、诊断结构或明确的日志标签，无需更改现有公开接口签名）：

| 日志原因 | 含义 |
|---|---|
| `InvalidTroop` | 士兵为空或申请人数无效 |
| `OfferNotFound` | 当天无对应候选集合 |
| `OfferInsufficient` | 有集合但该兵种剩余数量不足 |
| `FiefManagerUnavailable` | 无法获取 `FiefPartyManager` |
| `InsufficientHearth` | 村庄可用 Hearth 不足，真实消耗失败 |

对于 Hearth 失败，再检查：

`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`

方法：`TryConsumeProfessionalHearth()`。

记录附属正常村庄数、需要消耗的 Hearth（每人 2）、各村保留 100 Hearth 后可用余额总量。**只读取和记录，不为了日志改变征兵成本、Hearth 底线或提交顺序。**

### 4.5 建议日志级别与样例

正常逐项记录采用 `Debug`，每个定居点每日尽量一条汇总；若出现 `Approved > 0 && Actual == 0`，输出一条 `Warn`，并附明确原因及关联标识，防止全地图日志爆炸。

下面只是格式示例，**`Reason` 必须由实际检查结果生成，不可预先假定 Hearth 失败**：

```text
[Manpower][Produce] Day=123 Settlement=vostrum Allocation=2.00 Offered=2
[Manpower][Recruitment] Day=123 Settlement=vostrum GarrisonBefore=37 ProfessionalOffers=2 Approved=2 Actual=0
[Manpower][RecruitmentFailed] Day=123 Settlement=vostrum Reason=<observed_reason> Requested=2
```

## 五、预测逻辑修复要求

文件：

`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

方法：`GetPredictedDailyRecruitmentCount()`。

在取得根因后修正：

1. 确保同一天**先生产职业兵候选，再执行驻军招募**；读档恢复后行为也应一致。
2. 防止同日重复生产或重复招募；不得因预测读 UI 而消耗候选、扣 Hearth、写入名册或新增存档副作用。
3. 预测尽量使用与真实执行一致的前置条件和可执行性约束，不得将模拟批准结果冒充确定入伍数。
4. 已经完成当日招募结算时，避免再次把同日剩余候选展示为“尚待自动招募”。
5. 明确区分 **职业兵训练能力 / 生产额度 / 招募批准数量 / 实际入伍数量**。
6. **禁止**简单强制预测为 0、隐藏预测值或改 UI 文案来掩盖招募失败。

如果招募链路需要结构调整，应优先重用当前的候选管理器和每日结算机制，不新增全局人力库存。

## 六、UI 修改边界

**不增加任何 UI 字段、异常描述或诊断信息。**

继续保留现有“查看兵源”的：

- 基础兵源
- 基础训练
- 兵营加成
- 文化效率
- 额外加成
- 训练能力
- 预计补员
- 实际补员
- 自动招募状态

驻军悬浮提示仍使用现有布局。只有在明确发现预测数据错误时，才调整背后的计算结果和数据来源。

## 七、测试及验收标准

| 测试条件 | 验收要求 |
|---|---|
| 当天候选 2 人，资金/Hearth/编制和招募模型均允许 | 实际入伍 2 人；驻军名册增加 2；实际补员显示 2 |
| 没有职业兵候选且没有可招降俘虏 | 不显示有把握的 `+2` 自动招募预测 |
| 招募模型批准，但 Hearth 消耗失败 | 驻军不增加；日志给出真实失败原因 |
| 当天招募已结算、仍存在未消耗候选 | 不把“当天剩余候选”当作下一次确定招募数量 |
| 自动招募关闭 | 职业兵不自动入驻军，实际补员正确为 0 |
| 读档后跨过每日结算 | 事件执行顺序稳定，无重复生产/重复招募 |
| 每日正常招募 | 日志简洁，Debug 可追踪，不大量刷出 Warn |
| 招募过程中发生异常 | 日志记录上下文；UI 不出现新异常提示 |
| 无俘虏场景 | 职业兵预测/实际人数不混入俘虏招募 |

重点复现 **沃斯特鲁姆：驻军 37、训练 2.00/天、无俘虏、预测 +2、实际 0**，保留同一次结算前后的日志与驻军总人数截图进行比对。

## 八、提交给 ChatGPT Work 的实施任务

### 优先目标

**先定位职业兵实际入伍为 0 的根本原因，再修复自动招募预测与真实执行不一致的问题。**

### 实施约束

- 基于最新 GitHub 仓库代码复核；若后续提交改变路径或行为，以实际最新版本为准。
- 通过完整日志验证，不预设一定是 Hearth、经济预算或事件顺序问题。
- 尽量复用现有类和方法，不新增独立兵员库存或不必要的长期变量。
- 不改变职业兵训练速率、异文化文化效率/高级兵种开放规则、封邑兵每日补员规则。
- 不在游戏 UI 新增异常提示、限制原因或调试信息。
- 利用现有 `ModLogger` 输出可追溯的招募事务记录；避免频繁或大量输出正常信息。
- 修复后报告：**根因、修改的相对文件路径及方法、测试方法、实测结果、剩余风险**。

### 建议诊断顺序

1. 收集一次同定居点、同游戏日的“候选生产 → 统一招募计划 → 实际候选/Hearth 消耗 → 驻军名册变更 → 统计写入”完整日志。
2. 对照每日回调注册及执行顺序，复测首次进入游戏、读档、跨日情形。
3. 明确导致 `Approved > 0` 而 `Actual == 0` 的直接失败点。
4. 在不破坏经济与兵源守恒的前提下进行最小修复。
5. 最后修正驻军 `+2` 预测，使其不再误导玩家。

---

**最终结论：** 当前 Mod 的驻军预测读取的是**候选与招募模拟**，真正入伍还需完成**候选消耗与 Hearth 经济结算**；两条路径未可靠统一。修复应从实际招募失败的日志证据出发，不能只改 UI 文字或隐藏预测值。
