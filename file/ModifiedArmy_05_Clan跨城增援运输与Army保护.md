# 05｜Clan跨城增援：现有领主Party运兵运粮、Army保护和任务存档

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 本专题交付目标

让已有的外援请求由**Clan现有AI领主Party**实际执行，携带正规士兵和粮食支援指定城镇/城堡；没有可靠路线与Army两条保护链之前仅保留接口和受控Fake测试，不启动自动运输。依赖01（统一驻军人数）、02（兵员资格与Party目标）、03（外援请求）。

## 1. 当前状态

- `ReinforcementMissionService`已有内存字典、`TryRegister/Complete/Cancel`，不是带实际移动/存档的任务系统。
- `ReinforcementMission`、`TroopCargoManifest`、`FoodCargoManifest`已有初步DTO，但需要可存档类型、实际装卸记录和修复/取消语义。
- `ReinforcementRouteStubModel`当前始终返回`RouteUnsafeOrUnknown`；`EnableAutomaticReinforcement=false`。
- 目前**尚未**接管原版本体Army的两条路径：被召集加入Army、自己主动创建Army。
- `SupplyPartyBehavior`属于已有独立征粮队机制；**不能把领主Party变成SupplyPartyComponent并在完成时DestroyParty**。

## 2. 请求与装运政策

**请求**：读取03的`ReinforcementNeedResult`，边境当前有效人数低于75%才请求补到75%（不减七日补员）；非边境暂沿用原七日预测策略。需求≠派遣，需要有合法来源、承运Party、路线和资源才执行。

**来源**：同Clan安全驻军的可调士兵、领主Party自身的合格富余普通兵员，以及已核实可用的实际粮仓/物品库存；在Army中的Party不得被强制拉走。源驻军抽兵不能跌破本身75%或50%下限。

**Party自身富余至少20人优先**：

```text
OwnEligibleSurplus = min(
    max(0, PartyCurrent - ProtectedPartyTarget),
    无封邑在役身份且可安全移交的普通兵员数)

如果 OwnEligibleSurplus >= 20：优先交付Party自己的富余士兵；
  不足目标需求时，容量允许再从安全驻军补装。
如果 OwnEligibleSurplus < 20：不为1～19人的小富余启动专门捐兵；
  优先从同Clan安全驻军装兵。
```

运输Party自己的富余士兵原本就存在其Roster中，**只在清单登记交付配额，不可再`AddToCounts`一次**；从来源驻军装载才发生源驻军-、Party+。Party硬编制不足时应分批/分次，不能超过真实上限。若Party上限150、当前120、自己可交67、目标需100：不能再装33名驻军兵（只剩30空位），可先交自己的67后重新计划。

## 3. 运粮及CargoManifest

- 允许纯运粮、纯运兵、同运。全部由真实ItemRoster、Roster进行装卸，不生成“计划上存在、游戏里没有”的资源。
- 粮食清单至少记录物品类型、来源、装载数量、实际交付、途中消耗/丢失；粮食与Party自用路粮同在ItemRoster时，应通过清单和实际剩余库存双重限制可交付量。
- 提货前检查来源实际库存及保留量；装载后检查Party载重、速度及自用食品天数；目标到站时重新计算可接收量。
- 交兵上限来自01的理想上限（边境95%、内陆80%），抽出下限来自源地（边境75%、内陆50%）。征粮队外出占目标编制，不能在其返还后超限。
- 伤员、战斗损失、Party被俘导致实际货物减少时，以当前真实Roster库存为准，禁止按计划制造补货。

## 4. 建议任务状态机和数据

```text
Requested -> ToSource? -> Loading? -> ToDestination -> Unloading -> Completed
                     \-----> Cancelled / Recovering (with reason)
```

- `Source`可能为null（只交Party自己的多余士兵/自带粮）；`ToSource`和`Loading`可省。
- 单Party同时最多一个活跃任务；目标城堡计算其他**在途**承诺，防止多Party送入后总计超过目标上限。
- 每个任务持久保存：任务ID、Carrier、目的地、来源、Clan、出发/截止时间、状态、兵种及伤员清单、粮食清单、已装/交付数量、是否已经计入在途承诺、取消原因。
- 存档读回时重查OwnerClan、任务Party是否幸存、是否参加Army、目标是否仍可达、来源地可调额、已交付清单；缺条件时安全取消，不得重复装卸。
- 取消时，Party自带的货物标记只解除；驻军装载且尚存的士兵尽可能退还原来源或最近安全Clan据点。不可退还时保留在实际Party名册并追踪后续归属，不删除资源。

## 5. Army保护：两条原版入口分别处理

1. **被新征召加入Army**：在`code/DefaultArmyManagementCalculationModel.GetMobilePartiesToCallToArmy()`候选形成时排除`ReinforcementMissionService.IsProtected(party)`为true的承运Party。本体0.6召集比例保留。
2. **Party自己创建Army**：针对`code/AiMilitaryBehavior`的具体创建行为做窄Patch/适配器保护；**绝不能Prefix整个每小时军事AI Tick并return false**，否则干扰逃敌等任务。

- 已在Army、正被召集、正在战斗/被俘、目标不可达的Party不得开启增援。
- 有效任务保护期间不能被拉去Army，也不能自行创建Army，但遭敌仍能战斗/逃敌。
- 完成或取消后，`IsProtected`必须立即变false；Army资格恢复。
- 若无法可靠找到不阻断其他原版军事决策的窄挂钩，则**继续保持自动运输关闭**，不得用全局AI锁死来取巧。

## 6. Model、Service、Patch划分

| 部分 | 职责 |
|---|---|
| `IExternalReinforcementNeedModel` | 只读需求与请求量（03已经实现） |
| `IReinforcementRouteModel` | 可行性、预计路径风险；首版`RouteUnsafeOrUnknown` |
| `IArmyMissionProtectionModel` | 任务期间被召集、自建Army双向资格检查 |
| `ReinforcementMissionBehavior` | 事件、状态推进、`SyncData`存档与任务恢复 |
| `ReinforcementMissionService` | 任务注册/取消、来源资源预留和任务编排 |
| `TroopTransferService` | 真实名册装卸、封邑在役隔离、守恒 |
| `FoodTransferService` | 真实ItemRoster装卸、途中路粮与仓储审查 |
| Harmony窄Patch | 转交Model批准/对Army候选进行过滤，不遍历全Clan规划 |

## 7. 上线阶段

**M5：运输骨架，暂不自动派**

1. 完善可存档任务DTO、状态机、CargoManifest及`ReinforcementMissionBehavior.SyncData`。
2. 用Fake请求/安全路线Model驱动可控Party：从安全驻军真实提兵装载、运输粮食、到城卸货/兵、完成或取消。
3. 模拟OwnEligibleSurplus≥20、不足20、Party满编、遭遇敌军、任务被取消、目标失守、读档及运粮耗尽。
4. 验证两条Army窄保护不损坏其他原版决策；`EnableAutomaticReinforcement=false`仍保持。

**M6：正式路线评估、安全派遣，独立开启**

1. 用可替换`ReinforcementRouteModel`实现实际路线可达与敌军威胁评估；无数据时拒绝而非发送Party送死。
2. 选择合法运输Party与来源地，考虑Party自保、Party硬容量、来源最低留守、粮食储备、在途承诺及目标接收能力。
3. 将执行范围从玩家王国日志独立出来，覆盖符合策略的全部AI Clan。
4. 所有长时回归测试通过后，再把启用自动增援作为显式上线决定。

## 8. 必须通过的验收

| 场景 | 结果 |
|---|---|
| 运输Party自身富余57人，需80人 | 先登记自有57（不重复Add），容量允许再装23 |
| 自身富余12人，需80人 | 不为12人单独发任务，优先找合法驻军兵 |
| Party120/150，自有富余67，目标缺100 | 不一次额外装33造成153超编 |
| 源驻军Hard300、内陆有效240 | 不能抽出超过90，抽后≥150 |
| 目标边境Hard300、城内235、征粮50 | 有效285，正常卸兵0；不得征粮回来超限 |
| 正在执行增援的Party满足Army0.6候选条件 | 仍不能被新征召，也不能自行创建Army |
| 遇敌和任务取消后 | 仍可正常自保；结束恢复Army资格、真实兵粮可追踪 |
| 半途读档/目标易主/Party被俘 | 不重发、不复制货物、可恢复或安全终止 |

## 9. 源码索引

- [增援任务骨架](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Services/ReinforcementMissionService.cs)
- [任务DTO](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/ForceManagementTypes.cs)
- [原版Army候选](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/code/DefaultArmyManagementCalculationModel.cs)
- [原版创建Army](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/code/AiMilitaryBehavior.cs)
- [已有征粮队](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs)
