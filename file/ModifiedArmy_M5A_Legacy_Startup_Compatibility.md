# M5-A｜旧第五阶段隔离、启动恢复与存档兼容

> **文档状态：设计/实施计划，非已完成代码。** 此文档从 `ModifiedArmy_Phase05_Clan_Reinforcement_Redesign.md`（2026-10-20）拆分整理。原方案的业务规则及建议默认值未改写，原方案摘录保留原文。  
> **仓库基线：** `GuihuaZhou/bannerlord` / `69cbe45a0ac3bf91fe721860933da453c52b3e2e`，Bannerlord `v1.4.7.117484`。  
> **注意：** 原第五阶段 `52d9d8f...` 已标记 BROKEN，具体启动异常尚未获托管堆栈证实；下述工作尚未实施、编译或实机验证。

**本阶段交付物：** 旧领主 Party 承运功能停止新派遣、两个高风险 Army Harmony 补丁退出，旧任务存档可安全加载并取消；第五阶段以外的行为不回退。

**依赖：** 无。本阶段必须首先完成。  
**不包含：** 新临时增援 Party 的创建、跨城援粮、自动派遣。

## 共同约束（各阶段必须遵守）

- **同 Clan 限定：** 首版军事增援和跨城援粮仅允许同一 Clan 的来源及目标，不因同 Kingdom 放宽；创建、装载、交付和异常回收都复核归属。
- **资源真实：** 转移真实 `MemberRoster` / `ItemRoster`；不生成虚拟兵粮，不保存 CargoManifest；战损、耗粮后只按实际余量交付。
- **任务分离：** 军事增援队单程，幸存士兵到站归并目标驻军；援粮征粮队往返，护卫返回来源驻军；现有本地自用征粮保持不变。
- **普通领主不承运：** 不修改常规 `PartyForceTargetModel`、`AiRecruitmentBehavior` 或原生 Army 模型；删除旧第五阶段新增 Army Harmony 补丁，但不得删除全局 `PatchAll`。
- **异常安全：** 交付/回收前复核合法 Clan 和驻军接收容量；已存活兵员或有归属价值的物资不能被直接销毁；读档、重复事件不得造成二次装卸。
- **开关：** 在 M5-E 实机验收前保持自动派遣关闭，之前仅允许受控入口。

## A1. 旧方案废弃边界（原方案原文）

### 0.1 已讨论但放弃的旧设计

旧版“选一个领主 Party，把 `NormalTarget` 提高、再以 `Requested*/Available*` 跟踪准备”的整个分支**废弃**。这包括讨论过但仓库尚未实现的 `AvailableTroopCount/AvailableFoodCount` 两个持久化字段；**不要把这套字段加入新结构**。新军事增援队从源地直接带走实际兵员，完成交付不需要原领主Party编制补偿。

## A2. 旧数据契约与迁移（原方案原文）

### 4.3 旧第五阶段存档兼容与崩溃修复

当前仓库仍有 `ReinforcementMission`：[SaveableRootClass(8)] 以及六个Saveable属性：`Carrier`、`Destination`、`RequestedTroopCount`、`RequestedFoodCount`、`State`、`Deadline`；`ReinforcementMissionBehavior.SyncData` 键为 `modifiedarmy_reinforcement_missions`。**这些是旧领主承运方案，不得直接覆盖为新临时Party组件。**

最小安全迁移：

1. **保留旧类型ID 8、六项Property ID、原有 `FiefSaveDefiner` 注册和老 `SyncData` 键**以供反序列化。
2. 旧行为不再允许新任务注册/自动派遣；第一次加载旧活动任务时，将这些任务安全标记 `Cancelled` 并记录文件日志。**既有Carrier是普通领主Party，不能销毁，更不能把其真实士兵“回滚”为不存在的货物。**
3. 老活动任务退出后清理其任务保护与移动指令影响，但原有Party里实际兵粮继续保留；如需回归原军事AI，请通过原版正常行为，不向其生成假订单。
4. 删除 `ForceManagement/Patches/ArmyReinforcementProtectionPatch.cs` 中与旧领主承运有关的**两个新增 Harmony Patch**，而**不是禁用全项目 `PatchAll`**；不需要再实施旧修复计划中额外的 ArmyCalculationModel 替代方案，因为新临时Party非领主Party。
5. 解除 `ReinforcementMissionService`、`ReinforcementCargoService`、`FoodTransferService` 对旧任务的运行时调用后，先以兼容只读/迁移状态保留旧存档契约；待旧存档覆盖测试通过再另行决定清理期限。

> 注意：原第五阶段仍标记 BROKEN（游戏早期崩溃），只是已发现两处 Army Harmony 高风险点，未取得托管异常堆栈。移除补丁后仍必须完成实际启动隔离测试，不能先宣称已修复。

## A3. 对应源码及职责分界（原方案原文）

## 5. 架构与源码修改范围

以规则/模型处理决策，Behavior负责监听生命周期，Service负责执行真实Roster事务，Patch仅在确实没有Model/公开事件入口时使用。

| 当前文件/建议新文件 | 动作 | 职责 |
|---|---|---|
| `ForceManagement/Contracts/ForceManagementTypes.cs` | 保留旧 `ReinforcementMission` | 旧档反序列化迁移用；不追加曾讨论的Available字段 |
| `ForceManagement/Patches/ArmyReinforcementProtectionPatch.cs` | 删除两处第五阶段Patch | 不再保护普通领主Party，避免旧版启动风险 |
| `ForceManagement/Behaviors/ReinforcementMissionBehavior.cs` | 冻结旧新任务入口，保留读档迁移 | 不再负责新军事增援运输 |
| **新增** `ForceManagement/Transport/ReinforcementPartyComponent.cs` | 新建 | 新临时Party身份、存档、归属、状态 |
| **新增** `ForceManagement/Transport/ReinforcementTransportBehavior.cs` | 新建 | 每日派遣、小时事件、入城事件、战斗状态、回收、读档恢复 |
| **新增** `ForceManagement/Transport/ReinforcementDispatchModel.cs` | 新建 | 读取第三阶段驻军缺口、选源/受援校验、在途承诺、需求与线路许可 |
| **新增** `ForceManagement/Transport/ReinforcementTransferService.cs` | 新建 | 扣除源驻军Roster、创建Party、装粮、交付、失败回滚；可抽取复用旧 `ReinforcementCargoService` 的纯Roster方法 |
| `Garrison/Supply/SupplyPartyComponent.cs` | **扩展**字段/工厂，原字段不改 | `MissionMode`、`AidDestination`、`DispatchClan`、可恢复目标 |
| `Garrison/Supply/SupplyPartyBehavior.cs` | **扩展**援粮派遣和状态推进 | 原自用征粮流程不变；援粮出发→交付→返程 |
| `Garrison/Supply/SupplyPartyTransaction.cs` | 局部增加来源余粮装载、目标卸粮方法 | 真正从库存扣粮；区别市场交易、军粮仓库与返程自用口粮 |
| `Garrison/Models/GarrisonRecruitmentCapacity.cs` | 局部扩展 | 征粮/援粮护卫的同源地返程占编、改道时回收点预留；运兵队**不**占源地返程编制 |
| `PartyManagement/Models/NewPartySizeLimitModel.cs` | **扩展**组件识别 | 新军事增援队设置合法人数上限，避免原版速度/超编惩罚 |
| `Fief/Models/FiefPartyManager.cs` 内 `FiefSaveDefiner` | 仅追加类型 | 旧类型8不动，新增军事组件类型9，保持全局ID唯一 |
| `Main.cs` | 只注册新Behavior/必要Model | 不修改原有普通领主Party补员和Army模型；保留其它正常Harmony |
| `ModuleData/militaryConfig.xml` 等配置/本地化 | 最小增量 | 初版开关、人数上限、风险判断、日志文本 |

**尽量减少Patch**：本阶段不应新增“让临时增援队不能加入Army”的Harmony；其本身不是LordParty，但需要验证原生判定。对敌规避和敌军目标识别优先查找原版 AI Model/事件/API，只有确有未覆盖入口时才提交独立的最小补丁，附可重复验证证据。

## A3.1 第五阶段已实现代码的选择性清理清单（2026-10-20 代码复核补充）

> **关键约束：禁止 `git revert` 整个第五阶段提交 `52d9d8f...`，更不得回滚第一至第四阶段代码。** 本清单依据 `5540162`（第四阶段之后）与 `52d9d8f`（旧第五阶段实现）的提交差异，以及当前 `69cbe45` 的源码逐文件梳理。旧第五阶段有 **9 项文件变更**（其中新增四个文件），并不意味着9个文件都要删除。

| 第五阶段变更文件 | M5-A阶段动作 | 新方案后续复用/替换原则 |
|---|---|---|
| `ForceManagement/Patches/ArmyReinforcementProtectionPatch.cs`（新增） | **删除文件中的两个旧 Army Harmony Patch** | 新临时队伍不是 `LordParty`，不需要给普通领主Party做Army保护；切勿删除项目的全局 `PatchAll` |
| `ForceManagement/Behaviors/ReinforcementMissionBehavior.cs`（新增） | **保留兼容性Behavior，冻结 `StartControlledMission`**，旧任务只读档、取消与清理影响 | 原旧领主运输状态机不再运行；新临时Party另用Behavior负责路线与事件 |
| `ForceManagement/Services/ReinforcementMissionService.cs`（修改） | **停用旧注册/派遣入口与Old Carrier保护**，保留必要旧任务引用供存档取消 | 不要原样移植 `IsEligibleCarrier(IsLordParty)` 或 `GetInboundTroopCommitment`（原有Requested递减导致失真）；新并发协调单独实现 |
| `ForceManagement/Services/ReinforcementCargoService.cs`（新增） | **暂保留，禁止直接用于新任务** | M5-B抽取真实 `MemberRoster` 转移、来源驻军安全额度、在役封邑兵保护；重构成不依赖旧 `ReinforcementMission.Carrier` 的服务 |
| `ForceManagement/Services/FoodTransferService.cs`（新增） | **暂保留，禁止直接用于新任务** | M5-B/M5-D提取真实 `ItemRoster` 转移、来源余粮保护、载重限制；军援单程和援粮返程的自用口粮策略分离 |
| `ForceManagement/Contracts/ForceManagementTypes.cs`（修改） | **保留旧 `ReinforcementMission` 存档契约**（类型ID 8、属性1–6），其余现有模型/接口保留 | 不恢复更早期非持久化CargoManifest，也不写入讨论时废弃的Available字段 |
| `ForceManagement/Services/ForceManagementFacade.cs`（修改） | **保留现有军事模型与服务绑定；局部断开旧任务的执行路径** | 新运输服务在M5-B按职责接入；不更改 `GetPartyTarget` / `GetSafeFieldSurplus` 原有行为 |
| `Fief/Models/FiefPartyManager.cs`（修改） | **保留 `FiefSaveDefiner` 中旧任务类及列表的注册** | 加载老存档必需；新临时Party组件使用不冲突的新Saveable类型ID，未经检查不可复用ID 8 |
| `Main.cs`（修改） | **保留旧兼容Behavior注册，禁止旧派遣；保留其他全部正常Behavior及 `PatchAll`** | M5-B开始添加新临时Party Behavior，不在M5-A引入新自动派遣 |

**保留的上游依赖不属于“旧领主运输遗留”**：第三阶段的 `GarrisonDefenseRequirementModel`、`ExternalReinforcementNeedModel`；第四阶段的 `TroopTransferService` 和局部双向换兵；第一至第二阶段的驻军自动补员、AI领主补员、封邑兵服务；以及 `GarrisonRecruitmentCapacity` 和现有征粮队。这些全部不应回滚。

### 本阶段不得顺手重做的内容

- **不在M5-A重写**运输队创建、征粮队逻辑、兵粮模型；这属于M5-B～D的工作。抽取既有Roster事务可在M5-B按实际需要进行，避免阶段A增加编译或存档风险。
- **不要把旧 `ReinforcementCargoService.UnloadTroops()` 直接用于新临时增援队。** 它以 `GetSafeFieldSurplus` 保留普通领主Party目标兵力，而新军事增援队抵达后需要交付全部幸存、可接收的兵员。
- **不要把旧 `FoodTransferService.Unload()` 直接用于新军援队。** 它固定保护承运领主Party五日口粮；单程临时军援队到站不需要保留返程粮，往返援粮队则必须保留返程口粮。
- **不要直接把旧 `ReinforcementMissionBehavior` 整个删除。** 目前旧存档通过 `modifiedarmy_reinforcement_missions` 恢复任务，需要在确认存档兼容策略前保留反序列化入口。
- **不要宣称已经解决启动崩溃。** 两个旧Harmony目标是高风险点，但当前缺托管堆栈；删除后须实际测试游戏启动、旧档和新档。

### 推荐的工作提交拆分

1. `M5-A1`：删除两个旧Harmony补丁，其他源码不动，验证编译和游戏启动；异常仍在则继续获取真实堆栈并做隔离。
2. `M5-A2`：冻结旧 `StartControlledMission` 和保护/控制路径，旧活动任务读档后标记取消，保证领主Roster、任务键和SaveDefiner不变。
3. `M5-A3`：进行旧档回归测试，证明其余四阶段功能、征粮队正常、没有残留旧任务接管原生AI；保存独立的启动和存档测试报告。
4. **M5-B以后**才提取 `ReinforcementCargoService` / `FoodTransferService` 的可复用事务并增加临时Party组件，不以重复复制旧逻辑代替复用。

## A4. 可执行实施顺序（整理）

1. 检查当前 HEAD 是否仍包含 `ArmyReinforcementProtectionPatch.cs`、旧 `ReinforcementMissionBehavior` 注册及其调用；先建立启动与读档的日志基线。
2. 移除**仅第五阶段两个 Army Harmony 补丁**；`Main` 的全局 Harmony 机制及其他功能保持不动；构建并测试主菜单、新战役和旧战役。
3. 保留 `ReinforcementMission` 老类、`FiefSaveDefiner` 的类型 ID 8、属性 ID 1–6、旧行为 `SyncData` 键 `modifiedarmy_reinforcement_missions`。
4. 阻止旧行为启动新领主任务；读档时将旧活动任务取消，去除任务关联调度，**不删除/回滚领主 Party 的真实士兵和粮食**。
5. 再次测试：全新档、存在旧活动增援任务的存档、没有旧任务的正常存档；记录未修复异常及完整托管日志。

## A5. 验收清单（源方案第8.1节选）

- 原有自用征粮队的状态0/1/2旧存档继续可读，原有多站采购功能仍正常，新的Mode默认值不误判旧任务。
- 旧第五阶段领主Carrier活动任务读档：旧任务安全取消，领主Party保留Roster、恢复原AI，不存在Army Harmony启动崩溃。
- 保存发生在创建后、临时Party已抵达、交付中、回收途中：读档恢复后无重复派兵/卸粮/销毁；多次事件调用幂等。
- 任务创建失败：源地兵员、粮食、马、资金有明确回滚或保留记录；不遗留幽灵Party。

**放行标准：** 启动与读档稳定、旧增援不再派遣、常规领主Party功能未受影响、未遗留旧Patch；不能以“编译通过”代替实机启动验证。

## A6. 原方案总约束（原文）

## 9. 实施注意事项与任务外范围

- 当前只有**设计方案**，未修改/编译/部署，不能把测试表写成“已经通过”。
- 继续使用游戏原版和Mod现有正确的模型扩展点，**以Rule/Model、Service、Behavior分层为准，尽量不新增Harmony**；既有旧Army两个补丁需移除。
- 第三阶段驻军需求、第一至第四阶段AI与驻军补员、文化职业兵生产、WarDisposition桥均保持现有规则。
- 第一阶段不支持跨Clan支援、跨王国援兵、封邑兵抽调，也不增加军事增援队的多站取兵；雇佣兵加入新临时Party另做后续扩展。
- 不把临时Party创建行为误认为可跳过外交、围城、地图路线、敌方识别、工资和真实物资结算等原版游戏约束。
- 自动派遣开关维持关闭直到 M5-E 验收；M5-B～D 可设计受控测试入口并输出结构化日志。
