# ModifiedArmy 征粮队专项修复：总索引及 Worker 交接说明

> 设计日期：2026-10-24  
> 校验代码基线：`GuihuaZhou/bannerlord`，`main`，提交 `28f87c824ab07b4230e821052ae62e47e46ccf78`。  
> 模块范围：`ModifiedArmy/ModifiedArmy/Garrison/Supply/`。  
> 本文件是实施计划，不代表代码已修改、编译或测试。执行前核对 Worker 实际分支与 DLL 来源。

## 1. 用户观察与证据等级

- **现象 A**：征粮队出发后伤兵持续增多；用户怀疑未配给粮食。**代码确认**：`TryCreateSupplyParty()` 会调用 `TransferOutboundProvisions()`，它按 `ceil(abs(supplyParty.FoodChange) * 5)` 从驻军 `ItemRoster` 取食物，但未检查实际搬运量，缺粮时仍出发。**待证实**：伤兵增加是否由饥饿造成；应结合原版粮耗、战斗、伤员恢复/来源诊断。
- **现象 B**：路上大约每24小时短暂折返再去目的地，未见危险。**代码确认**：创建和读档后 `SetDoNotMakeNewDecisions(false)`，征粮任务每小时 `EnsureMovingTo` 重申目标；原版无领队部队的 `AiVisitSettlementBehavior` 也可能自主选择定居点。**高可信假设，非实机定论**：两套 AI 争夺目标。实际移动命令来源须先观察。
- **联动风险**：`CanOrganizeLocalForaging()` 把“存在冷却”等同于“能够本地征粮”，而 `TryCreateSupplyParty()` 可能由于缺粮失败。若未处理，可能抑制跨城援粮的兜底机会。需要回归验证。

## 2. 交付任务及并行关系

| 工作包 | 负责人 | 优先级 | 独立交付物 | 依赖 |
|---|---|---|---|---|
| W1 口粮保障与兜底 | Worker-Food | P0 | `01_征粮队口粮保障与兜底方案.md` 对应代码、单测、出发日志 | 独立；与 W2 同改 `SupplyPartyBehavior`，避免直接并行提交同一文件 |
| W2 路线诊断及仲裁 | Worker-Route | P0 | `02_征粮队路线折返诊断与修复方案.md` 对应代码、路线变更日志、测试 | 与 W1 分支并行分析，集成时顺序合并 |
| W3 联合验证与日志 | Worker-QA | P1 | `03_统一日志与联合验收清单.md` 的检查结果和 `ModifiedArmy.log` 证据 | W1、W2 集成后 |

**推荐提交顺序**：W2 第1阶段只加日志 → 用原行为复现现象 → W1 口粮及调度兜底修复 → W2 定向阻断冲突并复测 → W3 联合回归。不得为“快速消除折返”直接关闭全部原版 AI。

## 3. 代码地图（精确方法名）

- `Garrison/Supply/SupplyPartyBehavior.cs`：`OnDailySettlementTick`、`OnHourlyPartyTick`、`ShouldPauseForThreat`、`OnGameLoadFinished`、`TryCreateSupplyParty`、`TransferOutboundProvisions`、`TransferFood`、`FindBestSource`、`CanOrganizeLocalForaging`、`EnsureMovingTo`、`MoveToSettlement`、`CompleteMission`。
- `Garrison/Supply/SupplyPartyComponent.cs`：`OnMobilePartySetOnCreation`、`SynchronizeOwnership`、`MissionState`、`SourceSettlement`、`VisitedSettlements`，注意已存档字段及兼容性。
- `Garrison/Supply/SupplyPartyTransaction.cs`：`AcquireFoodAtSource`、`PurchaseFoodFromTown`、`PurchaseFoodFromVillage`。
- `Garrison/Models/GarrisonLogisticsModel.cs`：真实驻军粮仓 `GarrisonParty.ItemRoster.TotalFood`；**不是** `Town.FoodStocks`。
- `ForceManagement/Transport/TransportThreatModel.cs`：`HasSuperiorEnemy` 与 `IsNativeAvoidanceActive`。
- `ForceManagement/Transport/ReinforcementTransportBehavior.cs`：跨城援粮调用、重验和调度；不把征粮修复变成增援系统重构。
- 原版镜像 `code/FoodConsumptionBehavior.cs`、`code/DefaultMobilePartyFoodConsumptionModel.cs`、`code/AiVisitSettlementBehavior.cs`、`code/MobilePartyAi.cs` 仅作验证参考，**不要直接编辑原版镜像以解决 MOD 问题**。

## 4. 不得擅改的系统边界

1. 不修改职业兵训练、封邑兵台账、兵源池、AI Clan 调兵、外交、驻军保底人数或征粮护卫固定抽兵规则（50人）本身。
2. 不凭空创建粮食；食品必须从真实 ItemRoster 转移，市场采购必须按现有支付、存量规则执行。
3. 货物口粮与拟交付驻军的援粮要分账；不可把给护卫吃的口粮重复记作到货补给。
4. 不新建 Saveable 字段优先；若确实必须新增，要遵循 SaveDefiner/版本兼容与旧档回退要求。
5. 不直接阻止原版逃跑、战斗或危险规避；征粮任务只应排斥无关的自主日常改道。
6. 日志以中文自然语句显示；内部事件码仅可附 Debug，默认以 Info/Warn 记录重要生命周期和异常，非每小时刷屏。

## 5. Worker 的通用验收交付

每个 Worker 最终提交：文件列表及 commit、算法说明、潜在副作用、编译结果、自动化测试、旧档兼容结果、代表性中文日志（附发生时间和队伍）以及未覆盖风险。若无法运行游戏，要明确标注仅已编译或静态检查，不能声称实测通过。

## 6. 关键验收底线

- 正常任务从出发至到达粮源不应无缘无故断粮，也不能使粮仓食物为负数。
- 无危险、来源不变的情况下连续72小时不出现目标来回切换或净距离周期性倒退。
- 有强敌时允许短时逃避；危险解除后任务可以恢复，6小时保护期不构成永久僵死。
- 对伤兵增加有可核实的粮食/战斗/伤员变化日志，不写未经证实的“因为挨饿受伤”。
- 本地征粮因不足口粮不能出发时，允许符合条件的跨城援粮路径继续判断，不被“冷却中=本地可用”误拦。

## 7. 文件使用

先将本索引及相应工作包 MD 发给 Worker。W2 诊断优先实施并记录原始复现，再应用行为修复；QA 依第03份验收，不以“看上去不折返”作为唯一通过条件。
