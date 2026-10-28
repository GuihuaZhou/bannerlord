# ModifiedArmy AI 补员路线异常——第二轮纯日志诊断实施方案

> **用途**：交付 Worker 直接执行的只读诊断开发任务书  
> **项目**：`GuihuaZhou/bannerlord` / `ModifiedArmy`  
> **审查基准**：`main` 提交 `b31bf105feb4f868ff159caba5a1fc46cde16b8a`（2026-10-28）  
> **诊断依据**：`ModifiedArmy(3).log`  
> **任务性质**：**只增加诊断日志、中文本地化文本及验证测试，不改变 Mod 任何业务行为**  
> **说明**：执行前先同步仓库最新提交；如本方案所列函数或文件结构发生变化，应根据当前实现定位对应入口，保持任务边界不变。

## 1. 背景与目标

最新日志确认，多支严重缺员的 AI 领主部队能够建立补员任务，且部分目标驻军有可审批的调出兵力；然而补员移动命令在后续过程中反复丢失，任务被取消或重新规划，部队未能抵达指定驻军。

- 日志中有 **55 次**补员移动指令下达记录、**43 次**因移动目标变化取消任务的记录；其中 **41 次**取消时部队默认行为为 `Hold`。这些为跨游戏日重复出现的事件次数，并非独立任务数量。
- 典型案例：塔洛斯的部队为 **18/102 人**，费卡翁当时可批准调出 **79 人**。Mod 成功下达 `GoToSettlement(费卡翁)`，但随后部队进入其他定居点；每日检查时发现目标已变为“无”，行为为 `Hold`，并取消原任务。
- `NoSafeDonationSurplus` 多数出现在**访问非补员目标定居点时的普通驻军移交入口**，不得据此断言目标驻军补员审批失败。
- 统一驻军调拨事务本身存在成功记录，如阿庇斯从沃斯特鲁姆调兵，朱蒂拉从胡比亚调兵。沃斯特鲁姆的低额调拨还受到有效驻军最低留守人数限制；这与“路线失效”是两个不同现象。

**本轮唯一调查目标**：确定补员移动指令**首次何时、通过哪个方法、由哪条实际调用路径**从原定路线变为 `Hold`、被清除目标，或转往其他地点。不可预先把责任归给原版 AI、ModifiedArmy 或其他 Mod。

## 2. 不可突破的范围约束

本轮**禁止**修改：

1. AI 招募需求评分、每日随机门槛、优先级、候选来源、军事行为或部队目标人数。
2. `SetMoveGoToSettlement`、`SetMoveModeHold` 等方法的现有调用条件；补员路线恢复与取消规则；任务状态转换。
3. 驻军最低留守比例、驻军可调人数、工资预算、招募审批、兵种匹配与兵员转移逻辑。
4. 读档与存档数据结构、事务 `OperationId`、现有访问去重规则、事件顺序。
5. 军团、避敌、战斗的原有权限和处理流程。

允许：**只读快照、只读 Harmony Prefix/Postfix、纯日志格式化、本地化映射、仅内存中的诊断缓存和测试**。诊断代码不能调用会产生随机消耗、状态变更、重新审批或资金结算的业务方法。不得因输出日志而抛出异常、阻断原方法或改变原方法结果。

## 3. 优先修改文件

以下为相对目录 `ModifiedArmy/ModifiedArmy/` 的路径。

| 文件 | 工作内容 | 是否改变业务逻辑 |
| --- | --- | --- |
| `ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs` | 扩充状态变更、任务取消、访问地点及中文日志 | 否 |
| `ForceManagement/Diagnostics/PartyMovementTracePatch.cs` | **新增**只读 Harmony 移动指令追踪 | 否 |
| `ForceManagement/Missions/PartyForceTaskCoordinator.cs` | 在既有判断分支输出额外诊断，不改判断及返回 | 否 |
| `ForceManagement/Missions/PartyVisitExecutionService.cs` | 区分“补员目标访问”和“其他地点普通访问”的诊断文案 | 否 |
| `ForceManagement/Behaviors/PartyExchangeBehavior.cs` | 输出任务目标与实际定居点对照，沿用既有去重 | 否 |
| `ModuleData/submodule_strings.xml` | 正式书面中文文案及内部枚举/原因码映射 | 否 |

涉及的原版参考源码在仓库 `code/MobileParty.cs`、`code/MobilePartyAi.cs`、`code/AiMilitaryBehavior.cs`；**禁止修改 `code/` 下原版代码**。

## 4. P0：直接定位谁改变了移动指令

### 4.1 新增只读 Harmony 调用追踪

新建 `ForceManagement/Diagnostics/PartyMovementTracePatch.cs`。先核实运行游戏版本中目标方法签名，再对下列原版 `MobileParty` 方法建立**只读** Prefix/Postfix（优先覆盖本次最可能导致异常的入口）。

```csharp
MobileParty.SetMoveModeHold()
MobileParty.SetMoveGoToSettlement(...)
MobileParty.SetMoveGoToPoint(...)
MobileParty.SetMoveEngageParty(...)
MobileParty.SetMoveGoAroundParty(...)
MobileParty.SetMoveEscortParty(...)
MobileParty.SetMovePatrolAroundPoint(...)
MobileParty.SetMovePatrolAroundSettlement(...)
MobileParty.SetMoveRaidSettlement(...)
MobileParty.SetMoveBesiegeSettlement(...)
MobileParty.SetMoveDefendSettlement(...)
MobileParty.SetMoveToNearestLand(...)
MobileParty.SetMoveGoToInteractablePoint(...)
MobileParty.SetTargetSettlement(...)
```

注意 `SetMoveModeHold()` 会重置参数并调用 `SetTargetSettlement(null, false)`；其他 `SetMove*` 也可能经过 `ResetAllMovementParameters()`。**必须处理嵌套调用**：记录顶层有意义的变化，不要把方法内部短暂的 `None` 或 `null` 当成独立事故。对 `SetTargetSettlement` 建议只在它不是同一线程内顶层 `SetMove*` 方法的内部调用时独立报告，或明确标记为“内部嵌套调用”。

日志触发条件：

- 仅针对 `ManagedPartyPolicy` 管理且符合 `RecruitmentLogFilter.ShouldLog(party)` 的 AI 领主 Party。
- 当前存在活动补员任务并且具有有效任务目标。
- 前后状态确实发生变化，或记录到异常的路线覆盖。
- 不为无任务的普通 AI 移动、每帧寻路或所有地图 Party 输出堆栈。

捕获字段：

| 字段 | 含义 |
| --- | --- |
| 任务追踪标识 | 复用 `ForceRecruitmentDiagnostics.GetTraceId()` |
| Party ID、中文名称 | 防止同名 Party 混淆 |
| 游戏日和游戏内时间 | 记录真正变化的时间，不仅是之后发现的时间 |
| 触发方法及程序集 | 例如 `MobileParty.SetMoveModeHold` |
| 修改前默认行为/短期行为/目标定居点 | 前置快照 |
| 修改后默认行为/短期行为/目标定居点 | 后置快照 |
| 原任务目的地、任务状态、`OwnsMovementOrder` | 关联任务生命周期 |
| 军团、战斗、避敌状态 | 限定变化发生时的上下文 |
| 调用路径及诊断事件 ID | 识别触发者，供后续与每日检查关联 |

在**真实异常变化发生时**才用 `System.Diagnostics.StackTrace` 取调用路径；保留足以判断原版 AI、ModifiedArmy、第三方 Mod 或其他游戏事件的相关方法和程序集。无堆栈或无法识别时写“调用来源尚未确认”，禁止自行归责。

**Harmony 安全要求**：

- Prefix 仅保存快照（如 `__state`）；Postfix 仅比较状态并写日志。
- 不更改 `__instance`、`__result`、`__args`、原始参数或游戏数据；不调用移动方法。
- Prefix 不得返回 `false` 阻止原方法；不要使用修改异常流的 Finalizer 作为行为补丁。
- 所有诊断异常在自身内部处理；不得把日志失败传递到游戏调用链。
- 避免与现有 Harmony patch owner 冲突；安装时记录目标方法是否成功匹配。
- 使用线程/嵌套计数等仅内存方案协调重复观察；Campaign 切换时清理缓存。

### 4.2 在任务取消处补充上下文

文件：`ForceManagement/Missions/PartyForceTaskCoordinator.cs`。

在 `EvaluateParty()` 现有条件中只补日志，不改判断：

```csharp
if (current?.OwnsMovementOrder == true &&
    party.TargetSettlement != current.TargetSettlement)
{
    // 在这里扩展诊断；以下原有任务取消逻辑必须完整保留。
}
```

新增记录：上次确认路线有效的时间、最近一次路线变化事件 ID、变更源方法、原任务目的地、当前目标、默认与短期行为、当前驻留地点、`TargetParty`、`ShortTermTargetParty`、`party.Ai.IsAlerted`、军团与战斗状态、任务持续时间、取消发生的时间。

需要明确区分：**路线实际被修改的时刻**与**每日规划发现路线不一致的时刻**。原有取消语义保持不变。

### 4.3 保留任务时只观察、不恢复

文件：`PartyForceTaskCoordinator.cs` 中 `ApplyLivePlan()` 的 `next == current` 分支。

扩展现有 `LogTaskContinuation()`，区分：

- 任务有效、仍按目标路线移动。
- 任务存在，但没有取得移动指令。
- 任务存在，当前目标与计划不符。
- 原版临时避敌生效。
- 任务已终止或不符合继续执行条件。

**不允许**调用 `SetMoveGoToSettlement()` 补发指令，不允许修改 `OwnsMovementOrder` 或任务状态，不允许绕过当前 `return`。

## 5. P1：避免把普通访问误判为补员失败

文件：`ForceManagement/Missions/PartyVisitExecutionService.cs`。

当前代码在 `task == null || task.TargetSettlement != settlement` 时进入普通驻军移交流程；`NoSafeDonationSurplus` 描述的是普通移交，不是原补员任务失败。

在原分支开始时只增加日志，清楚区分：

1. 根本不存在活动补员任务。
2. 存在任务，但实际进入的定居点与任务目标不一致。
3. 已抵达原定补员定居点，将进行目标来源审批。

第二种情况必须记录原定补员地与实际访问地。例如：

> [兵力访问诊断] 塔洛斯的部队进入波利西亚，当前补员任务指定地点为费卡翁。本次访问不满足原定补员任务的执行条件，未对费卡翁驻军发起调兵审批。系统将按照现有规则处理普通定居点访问。

随后**原有代码必须继续执行**，不得提前返回、绕过普通移交或改变事务结果。

在 `PartyExchangeBehavior.cs` 复用任务追踪标识补充目标与访问地对照，保留原有的**同一 Party／定居点／游戏日**访问去重规则，不增加事务执行次数。

## 6. 中文日志要求：正式书面表达

新增日志统一使用 `GameTexts.FindText()` 与 `ModuleData/submodule_strings.xml`，不得硬编码混合中英文解释。内部枚举、ReasonCode、操作 ID 和任务追踪标识原样保留，仅负责展示层汉化。

建议映射：

| 内部值 | 日志展示 |
| --- | --- |
| `Hold` | 原地待命 |
| `GoToSettlement` | 前往定居点 |
| `Traveling` | 正在前往目标定居点 |
| `Planned` | 已制定计划 |
| `Cancelled` | 任务已取消 |
| `Committed` | 事务已完成提交 |
| `Rejected` | 事务未获批准 |
| `MovementOrderChangedExternally` | 检测到移动目标发生变化（修改来源待核实） |
| `NoSafeDonationSurplus` | 当前没有可以安全移交给驻军的多余兵力 |
| `NoMatchingForceTask` | 当前访问地点不存在匹配的兵力任务 |

**禁止**在正式中文日志主体直接输出 `Rejected`、`NoSafeDonationSurplus`、`Recruit` 等未翻译字段。技术标识、程序集名、方法名、`PartyId` 和 `TraceId` 可原样作为诊断字段展示。

示例日志（仅为格式，不代表已经确定实际调用来源）：

> [兵力路线追踪] 检测到塔洛斯的部队移动指令发生变化。原定补员地点为费卡翁；默认行为由“前往定居点”变为“原地待命”；原移动目标为费卡翁，新目标为无。触发方法：MobileParty.SetMoveModeHold；调用来源：待由实际调用栈填写。任务追踪标识：lord_1_68_party_1/93128/1/Recruit。

> [兵力任务诊断] 塔洛斯的部队原计划前往费卡翁补充兵力，但在每日规划检查时发现移动目标已经改变。上次确认路线有效时间：……；最早记录的状态变化时间：……；本次任务按现有规则处理，未进行兵员转移。

> [兵力访问诊断] 塔洛斯的部队访问波利西亚，当前补员任务目标为费卡翁。此次访问并非原定补员目标访问；后续事务属于普通定居点访问处理，不应计入费卡翁驻军补员失败。

## 7. 日志性能、去重和安全

1. 只记录**状态变化事件**，不对正常连续行军逐帧记录。
2. 不在 Prefix 的每次正常进入时创建调用栈；只有 Postfix 确认具有诊断价值的变化后才采集、输出。
3. 相同任务、相同来源、相同状态变化应限制重复堆栈输出；可仅内存累计发生次数。
4. 日志缓存不得写入战役存档或成为决策条件。
5. 诊断输出不调用产生副作用的业务模型方法，不重新运行候选排序、审批、支付或交易。
6. 严格限制范围为玩家所属王国的受管理 AI 领主 Party，避免全地图过量日志。
7. 业务流程内的异常日志不得因格式化、本地化或诊断堆栈采集失败而导致游戏异常。
8. 信息分级：普通状态变化 `Debug`；需要关注的补员路线异常丢失 `Warn`；关键事务结果视现有规范使用 `Info`。

## 8. Worker 验收矩阵

| 编号 | 验收场景 | 预期结果 |
| --- | --- | --- |
| T01 | 编译并安装 Harmony | 无目标方法签名不匹配、重复 Patch 或运行时异常 |
| T02 | 正常建立补员任务 | 日志准确列出原定目标、任务 ID、首次移动指令 |
| T03 | 外部代码改变补员路线 | 记录变化的**实际时间、方法和调用路径** |
| T04 | `SetMove*` 内部嵌套重置 | 不把临时 `None`/`null` 重置误报为独立路线故障 |
| T05 | 下一日因目标变化取消任务 | 关联先前路线变化诊断事件与任务追踪标识 |
| T06 | 当日访问非补员目标定居点 | 明确标识普通访问，不再误报目标驻军补员失败 |
| T07 | 访问原定目标并成功调兵 | 保持原申请、审批、实际转移结果不变 |
| T08 | 补员任务未取得移动指令 | 仅记录状态，不自动补发指令 |
| T09 | 保存、读档、切换 Campaign | 无新增存档字段；诊断缓存不跨 Campaign 污染 |
| T10 | 长时间运行 | 无海量重复调用栈、明显卡顿或日志无限膨胀 |
| T11 | 中文日志检查 | 解释性描述全部为正式书面中文，技术标识可保留原文 |
| T12 | 行为不变量检查 | 与未安装本轮诊断前相比，任务执行、移动结果、兵员、金币、随机消耗和去重规则未被主动改变 |

无法完成实机验证的项目必须标注 **BLOCKED**，不得只凭编译成功填写 PASS；使用存档副本完成验证。

## 9. Worker 交付清单

- Git 提交 SHA 与变更文件清单。
- 新增 Harmony 追踪的全部目标方法、签名及安装状态。
- 一份采集到实际调用来源的中文路线变化日志样例（若无法复现，应写明尚未取得）。
- 编译结果与测试结果表（PASS / FAIL / BLOCKED）。
- 新增日志的输出量、去重策略及性能控制说明。
- **逐项确认**：没有修改 AI 行为、补员概率、任务取消／恢复、驻军防务、财政审批、兵员事务、存档结构与去重规则。
- 如果发现某个 Harmony 目标方法无法安全 Patch，明确报告问题并保留现有行为；不得擅自采用可能改变业务逻辑的替代方案。

## 10. 最终判定标准

下一份日志应当可以区分：

| 实际调用证据 | 能够得出的结论 |
| --- | --- |
| 调用栈显示 `ModifiedArmy` 直接发起路线重置 | Mod 自身修改了移动命令；后续再单独分析是否合理 |
| 调用栈显示原版 `AiMilitaryBehavior` 发起重置 | 原版军事 AI 参与了路线改变 |
| 调用栈显示其他原版游戏事件发起重置 | 调查相应事件与状态条件 |
| 调用栈显示第三方 Mod 发起重置 | 进一步分析 Mod 兼容性 |
| 仅有状态变化，没有可识别调用栈 | 不得归因；继续通过只读诊断缩小范围 |

**最终交付目标不是修复 AI 行为，而是将“每日规划发现补员路线丢失”定位成“实际发生变化的时刻、触发方法与调用来源”。得到证据后再另行制定业务逻辑修复方案。**
