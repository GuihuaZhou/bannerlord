# ModifiedArmy 第五阶段：实施文档索引（M5-A ～ M5-E）

> **文档状态：设计/实施计划，非已完成代码。** 此文档从 `ModifiedArmy_Phase05_Clan_Reinforcement_Redesign.md`（2026-10-20）拆分整理。原方案的业务规则及建议默认值未改写，原方案摘录保留原文。  
> **仓库基线：** `GuihuaZhou/bannerlord` / `69cbe45a0ac3bf91fe721860933da453c52b3e2e`，Bannerlord `v1.4.7.117484`。  
> **注意：** 原第五阶段 `52d9d8f...` 已标记 BROKEN，具体启动异常尚未获托管堆栈证实；下述工作尚未实施、编译或实机验证。

## 文档使用方式

此文件是**索引与依赖路线**。五份阶段文档均可单独作为实施任务输入，已在各文档中补齐阶段适用的原方案规则、源码触点、操作顺序与验收条件。

所有文档源自 `ModifiedArmy_Phase05_Clan_Reinforcement_Redesign.md`（原版完整方案，未修改）；下面的“整理/执行顺序”用于拆分实施，不代表已经完成开发或测试。

| 次序 | 阶段 | 目标 | 前置依赖 | 关键验收 |
|---|---|---|---|---|
| 1 | [M5-A 旧第五阶段隔离与存档兼容](ModifiedArmy_M5A_Legacy_Startup_Compatibility.md) | 去掉旧Army Patch，旧任务安全退出，恢复可加载状态 | 无 | 启动、读档、旧领主Party不丢资源 |
| 2 | [M5-B 单程临时运兵](ModifiedArmy_M5B_OneWay_Reinforcement_Party.md) | 同Clan真实抽兵、创建临时Party、单程交付 | M5-A | Roster守恒、到站成功、清空销毁 |
| 3 | [M5-C 并发及资源回收](ModifiedArmy_M5C_Concurrent_Dispatch_Recovery.md) | 多队同目标、容量二次校验、失败安全回收 | M5-B | 不超编、不重复抽兵、不吞兵粮 |
| 4 | [M5-D 同Clan跨城援粮](ModifiedArmy_M5D_CrossSettlement_Food_Aid.md) | 征粮队新增援粮模式、交粮返回、保留旧流程 | M5-A/B/C | 护卫归队，原自用征粮不退化 |
| 5 | [M5-E AI安全与自动派遣](ModifiedArmy_M5E_Route_Safety_AutoDispatch.md) | 规避敌军、可被追击、读档、日志、长期实测 | M5-A/B/C/D | 实机通过后才开启自动派遣 |

## 已确认的业务决定（原方案原文）

## 0. 执行摘要与决策状态

经过需求讨论，**已无阻碍启动实现的业务决策**。采用以下最终方向：

1. **只在同一 Clan 的定居点之间**进行本阶段兵员与粮食支援，不能因为同王国就跨 Clan 调拨。创建、装载、交付及失效恢复期间都校验归属。
2. **军事增援队**：从源驻军真实抽调普通兵员，直接创建一个新临时 MobileParty；士兵本身同时承担护卫和增援，不设额外“返程护卫”。携带旅行口粮；源地安全富余时可携带给目标的额外粮食。正常情况下**单程前往目标**，全部幸存可接收兵员编入目标驻军、粮食交付后销毁临时 Party。
3. **跨城援粮征粮队**：复用既有 `SupplyPartyComponent`/`SupplyPartyBehavior`，由另一个同 Clan 驻军提供固定护卫、余粮，运粮到目标后返回出发地，护卫归还原驻军；**不是军事增援**，不把护卫留下。
4. **原有自用征粮**：继续按现有“从本地带护卫→寻找粮源→回本地”的机制工作。新模式不能回归破坏旧模式。
5. **兵员与粮食均以实际 `MemberRoster`、`ItemRoster` 为准**，绝不生成虚拟士兵、凭空粮食，不保存逐兵种/逐物品 `CargoManifest`；真实战损、耗粮造成的交付减少是正常结果。
6. 同一目标定居点**允许接收多个军事增援队和援粮队**，派遣时进行在途贡献抵扣；抵达时再次检查需求、驻军容量和 Clan 归属，防止重复交付和超编。
7. **普通领主 Party 不再参与承运**。`PartyForceTargetModel`、`AiRecruitmentBehavior` 的既有常规目标、招募策略不变；取消因第五阶段增援而设计的 Army Harmony 补丁与Army保护功能。
8. 临时 Party **遇敌可交战、战败可损失，危险时能规避敌军**。不得简单照搬征粮队现有“固定目标 + 禁止原生AI决策”，导致敌军不理睬与护卫不逃跑的缺陷。

## 已废弃的旧设计（原方案原文）

### 0.1 已讨论但放弃的旧设计

旧版“选一个领主 Party，把 `NormalTarget` 提高、再以 `Requested*/Available*` 跟踪准备”的整个分支**废弃**。这包括讨论过但仓库尚未实现的 `AvailableTroopCount/AvailableFoodCount` 两个持久化字段；**不要把这套字段加入新结构**。新军事增援队从源地直接带走实际兵员，完成交付不需要原领主Party编制补偿。

## 第一版参数：为推进开发暂定，不是用户已确认数值（原方案原文）

### 0.2 为免反复讨论而选定的第一版默认值（**建议值，非用户已敲定的数值**）

| 配置/策略 | 第一版建议默认 | 原因 |
|---|---|---|
| 每支军事增援队最大人数 | 50 | 与既有征粮队护卫规模一致，控制初期地图Party增量；可以由多个队共同支援 |
| 最少组建人数 | 10；紧急需求少于10时暂不自动派遣 | 避免大量1～2人临时Party；先验证路线稳定性 |
| 单支援粮征粮队护卫 | 50 | 沿用 `SupplyPartyBehavior.EscortSize`，不得自动为增援队借用返程护卫 |
| 来源及中转 | 军事增援队只从**一个来源驻军**出发，不执行多站补兵；不同来源可各派一队 | 减少路由、记账与存档复杂度 |
| 军事增援任务最长时限 | 建议20天，配置化；实装前测试路线可达性 | 防止永久滞留 |
| 援粮量与次数 | 按目标实际粮食缺口、来源安全余粮、运输容量与已有在途援粮计算；单队最多参考现有500单位上限 | 避免任意生成需求与重复派遣 |
| 派遣扫描 | 每日一次；途中状态每小时检查 | 贴合现有征粮队事件频率 |
| 派遣激活 | 开关初始关闭，仅受控任务测试通过后按批启用 | 当前阶段5快照存在启动崩溃，不能立即量产临时Party |

以上数值可做成 `ForceManagement`/后勤已有配置中的参数；如果项目已有等价参数，优先复用，不创建重复配置项。少于最小人数的缺口可依靠本地自动补员；不得把“最小10”理解为需凭空补出10人。

## 原方案阶段划分与验收（原文）

## 8. 开发阶段与验收门槛

| 阶段 | 实施范围 | 验收门槛 |
|---|---|---|
| **M5-A：先恢复启动** | 移除旧Army Harmony补丁、保留必要旧类型反序列化与兼容行为、编译 | 能进主菜单、进新战役、加载已有存档；有日志能定位问题；不删除全局Harmony |
| **M5-B：单程临时运兵** | `ReinforcementPartyComponent`、合法来源/目标、派遣模型、创建与交付服务、读档 | 从同Clan驻军实扣50以内普通健康兵→临时Party→到站入驻→清空销毁；人数总量守恒 |
| **M5-C：并发与失败恢复** | 多源多Party、目标在途承诺、容量重校验、被围/易主/战败/目标满员、资源回收 | 两队同目标不超编；任何失败不凭空增兵/吞兵；来源护卫不存在虚假预留 |
| **M5-D：扩展征粮队援粮模式** | 新模式与目标字段、同Clan余粮装载、交付、回源、回程编制预留 | 原有征粮功能不退化；送粮不留下护卫；返程不超编；无兵缺口也可救急送粮 |
| **M5-E：AI安全与自动派遣** | 规避敌军、可被追击、可达/风险评估、配置开关、批量每日扫描 | 实战遇敌、读档、超时、断粮、性能均通过后才开启自动派遣 |

### 8.1 必测情景清单

1. 仅同 Kingdom 非同 Clan：**不得**派遣军事增援队或跨城援粮队；玩家Clan与AI Clan等同执行。
2. 同Clan源地普通驻军有可调兵力：真实抽取，不触碰在役封邑兵，不跨越最低防御线；来源仅有在役兵时拒绝派遣。
3. 源地有多个合法候选：每个临时Party只从一个源地装兵；多人多源并发，路途中不凭空补员。
4. 目标已接近编制上限：剩余名额不足时部分交付，多余幸存士兵进入安全回收，不能消失或超编。
5. 两个军事增援队同时支援同一城：第二队出发前计入第一队在途预估；抵达时按真实剩余容量结算。
6. 军事增援队途中损失50%成员、粮食消耗：到站只交真实剩余资源，任务不自动补足差额。
7. 军事增援队目标易主或被围：不得交付敌对目标；找同Clan安全回收点，归并后销毁。
8. 纯粮食需求且本地已可正常征粮：优先本地征粮，不派援粮；本地不可自行征粮：由同Clan另一驻军派50护卫援粮队。
9. 援粮队送粮到站：交付不包含护卫与必要返程口粮，随后原路/安全路线回源归队；来源驻军自动补员不能占用护卫保留编制。
10. 援粮队途中来源易主：Party保持派遣Clan归属，转向同Clan替代回收驻军；敌方新驻军不为其预留编制。
11. 不同来源并发援粮：实际库存减少、在途贡献扣抵目标缺口，不得重复采购或无偿取走市场库存。
12. 原有自用征粮队的状态0/1/2旧存档继续可读，原有多站采购功能仍正常，新的Mode默认值不误判旧任务。
13. 旧第五阶段领主Carrier活动任务读档：旧任务安全取消，领主Party保留Roster、恢复原AI，不存在Army Harmony启动崩溃。
14. 保存发生在创建后、临时Party已抵达、交付中、回收途中：读档恢复后无重复派兵/卸粮/销毁；多次事件调用幂等。
15. 敌军能追击并进入战斗，临时运输队会规避强敌；威胁解除能回到任务目标，不因小时Tick覆盖逃跑而反复送死。
16. 任务创建失败：源地兵员、粮食、马、资金有明确回滚或保留记录；不遗留幽灵Party。
17. 工资、负重、速度、封邑兵归属、原驻军与征粮队的硬容量在批量运行20～30个战役日后无系统性漂移。

### 8.2 日志与性能

统一文件日志记录 `MissionId(Party.StringId)`、`DispatchClanId`、`SourceId`、`DestinationId`、`Mode`、`State`、请求兵粮、实际出发/交付/战损、来源剩余安全编制、目标容量、返回预留、失败原因。重要异常写日志，**不在UI反复弹窗**。

每日每Clan汇总候选、做路线预筛选与调度，小时事件**只处理活动运输Party**；避免对所有普通领主Party逐小时反复遍历所有Settlement进行评分。读档需要重建运行时索引（可重建索引不是另一份存档事实来源）。

## 阶段交接原则（整理）

- M5-A完成后才能开发运行M5-B的新任务，避免旧Patch影响启动测试。
- M5-B完成“单源单队单程”受控验证，M5-C再补全同目标多队和异常回收；中间不提前开启自动派遣。
- M5-D沿用`SupplyPartyComponent`既有存档ID1..5，追加新援粮模式，不替换旧自用征粮逻辑。
- M5-E是自动派遣的最终放行关口，必须落实敌对AI、路线恢复、失败回收、持久化与长时间运行测试。
- 全部实施完成前均保留日志，确保可区分“资源不足”与“程序异常”；UI不打印异常信息。

## 原始源码清单（原方案原文）

## 10. 依据源码（审查基线）

- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyComponent.cs`：临时Party `CreateSupplyParty()`、保存类型、姓名/Clan归属、AI行为。
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs`：征粮队派遣、HourlyTick、到站交易、返程销毁、已有回收与冷却。
- `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyTransaction.cs`：现有真实食物采购、装载与食物来源逻辑。
- `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs`：`GetActiveSupplyEscortCount`、有效编制及回程预留。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs`：`GarrisonDefenseRequirementModel`、`ExternalReinforcementNeedModel`、`ReinforcementRouteStubModel`。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/ForceManagementTypes.cs`：旧 `ReinforcementMission` 持久化契约。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/ReinforcementMissionBehavior.cs`：旧领主任务Runner，迁移后不再新建任务。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Services/ReinforcementMissionService.cs`、`ReinforcementCargoService.cs`、`FoodTransferService.cs`：旧领主装卸运输逻辑，不能直接沿用请求递减语义。
- `ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewPartySizeLimitModel.cs`：征粮队50人合法上限，供新临时Party参考。
- `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`：现有 `FiefSaveDefiner(20251116)` 与旧类型ID8。
- `ModifiedArmy/file/第五阶段增援运输实现报告.md`、`ModifiedArmy/file/第五阶段启动崩溃Debug日志.md`：旧版失败状态与崩溃证据。
- `ModifiedArmy/file/驻军征粮队当前实现.md`：现有征粮队架构、缺陷、护卫/返程规则。

仓库基线：<https://github.com/GuihuaZhou/bannerlord/tree/69cbe45a0ac3bf91fe721860933da453c52b3e2e>。

**最终原则：** 同Clan真实资源调拨；军事增援队单程、援粮征粮队往返；普通领主Party军事AI不被后勤任务污染；异常不能吞兵吞粮；先稳定启动和可控任务，再打开自动派遣。
