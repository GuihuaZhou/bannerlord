# M5-B｜同 Clan 临时军事增援队：单程创建与交付

> **文档状态：设计/实施计划，非已完成代码。** 此文档从 `ModifiedArmy_Phase05_Clan_Reinforcement_Redesign.md`（2026-10-20）拆分整理。原方案的业务规则及建议默认值未改写，原方案摘录保留原文。  
> **仓库基线：** `GuihuaZhou/bannerlord` / `69cbe45a0ac3bf91fe721860933da453c52b3e2e`，Bannerlord `v1.4.7.117484`。  
> **注意：** 原第五阶段 `52d9d8f...` 已标记 BROKEN，具体启动异常尚未获托管堆栈证实；下述工作尚未实施、编译或实机验证。

**本阶段交付物：** 同 Clan 驻军真实抽兵形成临时增援 Party；一程抵达目标后按真实存活成员和容量交付，再安全清空销毁。  
**依赖：** M5-A。  
**本阶段先用受控任务测试，不开放自动派遣；M5-C负责更全面的并发和失败回收，M5-E负责遇敌及风险放行。**

## 共同约束（各阶段必须遵守）

- **同 Clan 限定：** 首版军事增援和跨城援粮仅允许同一 Clan 的来源及目标，不因同 Kingdom 放宽；创建、装载、交付和异常回收都复核归属。
- **资源真实：** 转移真实 `MemberRoster` / `ItemRoster`；不生成虚拟兵粮，不保存 CargoManifest；战损、耗粮后只按实际余量交付。
- **任务分离：** 军事增援队单程，幸存士兵到站归并目标驻军；援粮征粮队往返，护卫返回来源驻军；现有本地自用征粮保持不变。
- **普通领主不承运：** 不修改常规 `PartyForceTargetModel`、`AiRecruitmentBehavior` 或原生 Army 模型；删除旧第五阶段新增 Army Harmony 补丁，但不得删除全局 `PatchAll`。
- **异常安全：** 交付/回收前复核合法 Clan 和驻军接收容量；已存活兵员或有归属价值的物资不能被直接销毁；读档、重复事件不得造成二次装卸。
- **开关：** 在 M5-E 实机验收前保持自动派遣关闭，之前仅允许受控入口。

## B1. 兵员增援需求（原方案原文）

### 1.1 军事兵员增援需求：复用第三阶段模型

继续调用 `GarrisonDefenseRequirementModel` → `ExternalReinforcementNeedModel`，不重算或重写既有城防逻辑：

- 边境目标驻军低于**有效编制75%**时，请求到最低防御线的缺口，不扣除未来七日预测补员。
- 内陆目标低于**有效编制50%**时，扣除既有模型认可的未来七日可获得补员，仅对仍不足的差额发起外援。
- 分母按驻军现有硬编制，征粮队外出护卫仍计入**其原驻地**有效编制和返程占用，不能误算为已从本Clan永久转移走。
- 目标接收除最低防御线之外，还需服从已有硬上限及驻军补员/转移规则（边境95%／内陆80%软目标是否对交付生效，应复用现有 `GarrisonRecruitmentCapacity` 的具体批准接口，不擅自改写）。

多支队伍在创建时按计划可交付士兵（新 Party 当前真实人数、不得超过目的地剩余需求）进行**在途预留**，不使用旧领主任务的递减 `RequestedTroopCount` 作为在途数。目标自发补员、征粮队返回、其他临时队先到达等情况会改变现实容量，所以预留仅用于**避免过量派遣**，并不取代到站检查。

## B2. 选源、实际抽兵与事务式创建（原方案原文）

## 2. 选源与派遣：真实资源守恒

### 2.1 兵员来源

第一版军事增援Party的主兵源是**同 Clan 其他定居点的现役驻军**，所有抽兵都来自实际 `MemberRoster`。约束：

- 来源与目标不同，源地和目标当前 `OwnerClan` 相同、非空；均非正在被围攻的非法装卸地点；源有合法驻军。
- 仅抽取**健康、非英雄、非封邑在役绑定**的普通驻军士兵；借助 `FiefPartyManager.GetServingTroopCounts` 等现有接口排除受服役绑定保护的兵种数量。不要把相同兵种的在役/非在役士兵统统视为可调拨。
- 源地剩余**实际驻军**不得跌破本地安全最低驻军线；既有外出征粮护卫**不能再次抽调**。计算有效编制时可以记录回营预留，但为防止短期空城，首版优先更严格的“源地实际驻军抽调后仍不低于下限”。
- 源地要有正常行军所需的口粮；否则不创建注定会立即断粮的临时Party。装载额外送粮前需保留自身粮食安全底线。
- 军事增援人数取 `min(源地可安全抽调普通士兵, 目标尚缺人数, 单队人数上限)`。多个同Clan来源可分别组建增援队，支持同城并发。

**雇佣兵和封邑兵**：此前讨论提出过雇佣兵，但新设计的首个可执行版本建议**暂不实现临时Party的雇佣兵招募**，避免额外商贸账目、非领主Party招募接口与资金授权问题。后续可通过单独接口给新Party付费补员；封邑兵按用户要求留待以后讨论。本次不能将尚未实现的来源伪装为已支持。

### 2.2 兵源选择策略

在所有合法同 Clan 来源驻军之间排序：先筛掉不足10人的候选（紧急不足10是否特例留给配置），再以**可安全调出人数、地图距离、来源驻军自身威胁**作为简单排序依据。**首版不做整条多站路线优化**，每支Party仅从一个驻军出发。

创建过程应预评估源地可调人数、目的地需求、载重、能否创建Party与路径，然后以事务式步骤进行：从源驻军扣真实Roster → 建临时Party并承接Roster → 装载口粮/可调余粮 → 提交任务与移动。任何中途失败，尽可能将新建Party仍有的士兵和物品**真实回滚**到源驻军；不得在失败路径多次扣除。

## B3. 单程状态机与装卸规则（原方案原文）

### 3.1 军事增援队（正常单程）

```text
需求与同Clan选源
   │
   ├─ 无可安全调拨兵员 → 不创建队伍；留待下次每日评估
   │
   └─ 有真实可调兵员
       → 在源地创建 ReinforcementPartyComponent 的临时Party
       → TravelingToDestination
       → 到站校验实际驻军容量与所属Clan
       → Unloading（只转移真实存活士兵/实际食物）
       → Completed（成员与物品全部结算完毕）
       → DestroyPartyAction销毁**已清空**的临时Party

途中目标失守／目标已满／临时队被迫撤退
       → Recovering（选其他同Clan安全接收点）
       → 安全归并全部剩余兵员、物品，再销毁Party
       → 无接收点时不准销毁有兵员的Party，持续标记并记录异常
```

正常交付细则：

- 每个正常增援队全部幸存的合法士兵都是交付对象，**不存在独立“护卫兵必须返还”**的问题。
- 到站重新计算目的地 `GarrisonRecruitmentCapacity.GetTransferFreeSlots` 和真实需求，移交上限为 `min(临时Party当前可交付Roster, 目标可接收人数)`；**不因为请求数历史值硬塞超编**。对暂时多出的兵员执行安全恢复，不直接销毁。
- 临时Party携带的额外粮食按真实余量、目标可接受的需求交付；交付后剩余物资也需向同Clan驻军作安全结算，不能因销毁Party而凭空丢失（正常目标可接收普通物资时可统一交付）。
- 战损自然影响实际到达人数；不生成“未兑现人数”的替代士兵。
- 每支军事增援Party**只执行一次正常目标交付**。异常恢复不等于重新向多个目标执行反复增援。

## B4. 新组件存档结构（原方案原文）

### 4.1 新建 `ReinforcementPartyComponent`

使用原版 `PartyComponent` + `MobileParty.CreateParty()` 模式，参考现有 `SupplyPartyComponent`，但不继承它、也不把临时增援队伪装成商队。建议最小持久化数据如下（示意，准确字段编号在提交前检查注册冲突）：

```csharp
[SaveableRootClass(9)] // 建议复用FiefSaveDefiner中的下一个空闲类型ID 9
public sealed class ReinforcementPartyComponent : PartyComponent
{
    [SaveableProperty(1)] public Settlement SourceSettlement { get; private set; }
    [SaveableProperty(2)] public Settlement Destination { get; private set; }
    [SaveableProperty(3)] public Clan DispatchClan { get; private set; }
    [SaveableProperty(4)] public int StateValue { get; private set; }
    [SaveableProperty(5)] public int RequestedTroopCount { get; private set; }
    [SaveableProperty(6)] public int RequestedFoodCount { get; private set; }
    [SaveableProperty(7)] public Settlement RecoverySettlement { get; private set; }
    [SaveableProperty(8)] public CampaignTime Deadline { get; private set; }

    // 已装载和剩余资源始终读取 MobileParty.MemberRoster/ItemRoster。
    // 不保存 AvailableTroopCount、AvailableFoodCount 或 CargoManifest。
}
```

`Requested*` 是该任务创建时的**派遣计划快照**，只用于日志和在途协调；`MobileParty` 内真实Roster决定卸载量。组件中的 `DispatchClan` 是跨城调拨权属基准，不能在来源易主时根据新Owner重置。`RecoverySettlement` 用于异常归并途中的存档恢复。`StateValue` 仅可由业务服务受控转移。

`FiefSaveDefiner`目前已注册旧 `ReinforcementMission` 类型ID 8，建议增补新类型ID 9并保留旧类型注册；不要修改既有根类型、Definer ID 或旧Property ID。新组件存档对象随 `MobileParty` 本体持久化，优先不再创建重复的 `[Saveable] List<ReinforcementParty>` 索引。

## B5. 开发触点（从原方案第5节摘取的本阶段事项）

| 文件 | 动作 |
|---|---|
| `ForceManagement/Transport/ReinforcementPartyComponent.cs` | 新建临时Party组件、任务状态和持久化属性 |
| `ForceManagement/Transport/ReinforcementTransportBehavior.cs` | 新建受控派遣、到站事件、任务完成生命周期 |
| `ForceManagement/Transport/ReinforcementDispatchModel.cs` | 复用驻军缺口模型；同Clan选源、容量检查 |
| `ForceManagement/Transport/ReinforcementTransferService.cs` | Roster实转、失败回滚、卸载与销毁前结算 |
| `Fief/Models/FiefPartyManager.cs` | 在`FiefSaveDefiner`中只追加新组件建议类型ID9（先验冲突） |
| `PartyManagement/Models/NewPartySizeLimitModel.cs` | 临时增援队的合法编制上限 |
| `Main.cs` | 注册新Behavior与必要服务，原AI和Army规则不变 |

## B6. 执行顺序（整理）

1. 通过第三阶段现有模型确定目标驻军净缺口；仅同Clan且有安全可调健康普通兵的来源可选。
2. 避免抽取在役封邑兵，来源实际驻军抽兵后不可跌破安全线；成功创建临时 Party 后再确认账实一致，创建失败按实际Roster回滚。
3. `ReinforcementPartyComponent`随 `MobileParty` 存档，不另存相互冲突的部队货物清单；真实装兵、装粮并移动。
4. 到站复核同Clan、未被围及接收容量；全部幸存**可合法接收**兵员入目标驻军，剩余粮食按实际交付；Party内剩余资源不能被销毁。
5. 对新档、读档中途和重复到站事件测试；本阶段即使出现异常，也不得破坏资源守恒。

## B7. 验收清单（源方案第8.1节选）

- 仅同 Kingdom 非同 Clan：**不得**派遣军事增援队或跨城援粮队；玩家Clan与AI Clan等同执行。
- 同Clan源地普通驻军有可调兵力：真实抽取，不触碰在役封邑兵，不跨越最低防御线；来源仅有在役兵时拒绝派遣。
- 源地有多个合法候选：每个临时Party只从一个源地装兵；多人多源并发，路途中不凭空补员。
- 目标已接近编制上限：剩余名额不足时部分交付，多余幸存士兵进入安全回收，不能消失或超编。
- 军事增援队途中损失50%成员、粮食消耗：到站只交真实剩余资源，任务不自动补足差额。
- 保存发生在创建后、临时Party已抵达、交付中、回收途中：读档恢复后无重复派兵/卸粮/销毁；多次事件调用幂等。
- 任务创建失败：源地兵员、粮食、马、资金有明确回滚或保留记录；不遗留幽灵Party。
- 工资、负重、速度、封邑兵归属、原驻军与征粮队的硬容量在批量运行20～30个战役日后无系统性漂移。

**放行标准：** 一支从单一合法来源创建的临时增援队能完成真实Roster单程交付，创建/卸载/清空后销毁不复制、不吞兵；自动派遣仍关闭。

## B8. 本阶段适用默认值（原方案原文）

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
