# ModifiedArmy：AI兵力管理、驻军双向调配与Clan防御增援

**设计方案与实现方案（架构优先 / API与Model优先）**

> 文档版本：V1.1 · 日期：2026-10-18  
> 代码仓库：`GuihuaZhou/bannerlord` · 核对基线：`e669c477a4346254eadffe7b312638cc1443996e`（默认分支本次读取到的最新提交）  
> 文档用途：作为编码AI可直接执行的设计规范及分阶段开发清单。下文区分**现有实现**、**本次需要实现**、**仅定义API/Stub**。本文件不是代码修改结果，也不代表已经编译或验证。

## 项目交付目标

ModifiedArmy统一管理AI领主Party与Clan驻军的征兵、封邑兵返乡、兵力移交、兵种交换、军费约束以及跨定居点防御增援。**本体AI仍然负责实际军事行动**；Mod通过真实兵员与粮食的补充、运输和配置影响行动能力。

**落地原则**：先建立稳定的Model、Service、DTO、Facade和薄Patch，将业务判断与Roster修改分开；已确定的规则真实实现，尚未确定的阈值封装为默认保守返回的API，不得擅自补充复杂算法。

**明确范围**：本文件覆盖完整设计和实现计划。第一轮不要求立即启用自动跨城增援、高低级自动Swap和财政裁军；这些功能可以先搭建可测试的骨架，启用前必须通过相应安全门禁。

---

## 第一部分：设计方案

这一部分定义游戏内行为、约束与结果，不把Harmony或现有源码方法当成业务规则。

### 1. 系统目标、责任边界与首轮范围

#### 核心职责

1. **ModifiedArmy接管士兵补员、兵力移交、兵员回收、局部交换、Clan紧急增援需求与任务管理**。
2. **原版军事AI仍决定战斗、围城、劫掠、普通巡逻、Army战略和实际交战**；兵力规模的改变自然影响其能够执行的任务。Mod不通过虚构潜在战力直接修改本体战斗/目标评分。
3. **全Clan现有战斗力保留优先**。和平目标不构成强制裁军命令；已有兵力调配比解散优先。
4. “局部优化”不得引发专门跨城旅行；“实际防御/粮食危机的战略增援”允许派遣**现有AI领主Party**携兵携粮专程支援。
5. 保持**API/Model负责判断、Service负责执行、CampaignBehavior负责调度与存档、Harmony Patch仅适配本体事件**。
6. 尚未决定的策略（特别是边境无敌情但生产中断时的增援阈值）**不伪造为最终规则**：提供稳定接口、保守固定返回值、可替换实现、日志与测试注入点。

#### 第一轮实施成果和暂缓项

| 第一轮必须具备 | 先只定义API/Stub的内容 |
|---|---|
| 统一Party目标人数、严格主战门槛、场景覆盖、招募容量审批 | 复杂敌情强度、驻军需要外援的动态阈值 |
| 驻军自动新增士兵80%与普通回营/转移90%区分 | 精细兵种战术价值、跨全Clan最优化求解 |
| 到达驻军时Party→驻军转移及基本合法性审批 | 多站复杂路线规划、跨日驻军空位预约 |
| Army免征召接口/任务模型骨架，未通过保护验证不得激活自动任务 | 实际道路风险与接敌ETA评分、特殊兵种战法 |
| 封邑兵在役身份隔离、来源返乡调用、兵数守恒 | 大规模跨城驻军资源再平衡 |

---

### 2. Party与驻军的人数目标及征兵规则

#### 三层控制

- **目标兵力**：Party场景 + Clan战争倾向；不能由兵源多少反向虚构目标。
- **兵源偏好**：`militaryConfig.xml/MilitaryDoctrine`的封邑兵/职业兵/雇佣兵及情境修正；只表示偏好，不是硬性兵种配比。可用兵额、经济、抵达时间、文化资格、封邑兵单次门槛以真实审批为准。
- **分配与复员**：Clan总体战力保留优先；转移和交换不得凭空生产或丢兵，未经明确裁军审核不得隐式删除士兵。

#### Party和平人数目标

```text
PeaceTargetCount = Min(PartyHardLimit,
                       Max(50, Ceil(PartyHardLimit * 0.35)))
```

- 50人为和平**补员目标下限**，并不保证无经济/无兵源时瞬间达成。
- 当`PartyHardLimit < 50`，以硬编制上限为准。
- 低于50的正常有效Party不能被常规85%启用阈值长期卡住；应直接评估补员。
- 超过和平目标**停止常规招兵**，但不因此立即裁撤职业兵/雇佣兵。

#### 战时普通独立Party：严格区分是否“主战”

`D = Clan.WarDisposition`，明确分支（**必须编成独立API条件，不能仅靠公式碰巧分界**）：

```text
IsAggressiveWarClan = (AtWar && D > 20)

AtWar && D <= 20:
    仅执行中立/主和目标，不得因普通战争状态主动扩军至60%及以上

AtWar && D > 20:
    可根据D主动提高普通独立Party目标至60%～95%
```

首版建议映射（延续本轮已确认讨论的锚点）：

```text
D ∈ [-100, 0]:   R = 0.35 + 0.0015 * (D+100)      // -100→35%, 0→50%
D ∈ (0, 20]:    R = 0.50                       // 非主战倾向最高50%
D ∈ (20, 60]:   R = 0.60 + 0.005 * (D-20)      // +21≈60.5%, +60→80%
D ∈ (60, 100]:  R = 0.80 + 0.00375 * (D-60)   // +100→95%
```

**重要**：在20/21边界这里**故意有政策跃迁**，不能使用`0.50+0.005D`这一未区分主战资格的公式，否则中立`D=20`也会恰达60%。由`IsAggressiveWarClan`显式许可扩军，算法数值只是目标设置。日变化经过阈值时可在Service层加“本日已派招募任务不被频繁重定向”的执行迟滞；不得改变政治Mod原有WarDisposition定义。

战时目标人数采用`Ceil(PartyHardLimit*R)`并裁到硬上限。**和平50人下限只明确适用于和平场景**；是否进一步扩展为低主战倾向战时的保底，属于后续平衡接口，第一版不得借此突破`D<=20`非主战主动扩军门槛。对于小编制Party（例如上限80，和平最低50已达62.5%），按“是否因为战争而主动扩军”的语义判定，不以实际百分比倒推其政治意愿。

#### 军事特殊场景覆盖

优先级：紧急战斗/无法中断状态（限制移动，不代表目标为0）→自家Clan封邑防守救援→已加入Army正在集结→普通战时目标→和平目标。

| 场景 | 目标 | 额外限制 |
|---|---|---|
| 自家Clan封邑防守救援 | `100%`硬编制 | 只对**符合救援条件**的Party；不得为补员而错过紧急救援，不得抽空被围困驻军 |
| 已加入Army、正在集结 | `Max(90%, WarTargetRatio)` | 已附着成员不可自行离队招募；集结成员仅允许不延迟到达的补员 |
| 普通战时独立Party | D驱动35%～95%，非主战低于60%主动扩军目标 | 不取Raid/Siege作为前置扩军门槛 |
| 和平 | 35%且不少于50 | 不强制解散既有高价值兵员 |

本体Army的`0.6`被召集门槛**保持不变**。新增的“执行有效增援任务期间免被Army征召”是例外资格过滤，不是全局改门槛。

#### 驻军自动补员80%与普通接收90%上限

- 自动生成职业兵/招募俘虏：最多补至`Floor(GarrisonHardLimit*0.80)`，且已外出征粮队的实时人数**继续算入有效编制**。
- 已存在士兵的普通回营移交、Party双向交换：按`90%`编制、实际兵种/军饷/最低防御约束审批；达到80%后可继续接收，**不因超过80%而清人**。征粮队等已预留编制的任务部队真实返还仍可到100%硬上限。
- 80%是自动新增的软上限，**不是**所有驻军必须达到的防御目标或求援线。
- 正常的95%限制出现在多个代码入口，需要统一配置并明确调用上下文；不能只替换一个常量。

#### 封邑兵与职业兵/雇佣兵的归宿

- 封邑兵：依据`FiefServiceBatch`、所属原封邑及服役记录返回来源封邑，恢复相关Hearth占用并进入冷却；不能只按`CharacterObject`判断兵源。
- 职业兵和雇佣兵：优先移交同Clan合法驻军；驻军满编时可评估互换；真实无法安置时允许继续留在Party。裁军属于单独审批，**不是交换失败的默认处理**。
- 现有`FiefPartyManager.ReturnTroopsToSettlement(Settlement, MobileParty)`公开入口返回`void`，底层`FiefPartyData.ReturnTroopsToSettlement(MobileParty)`返回`int`；新Service不要假定管理器直接返回归还人数，应新建结果API或真实读取前后差值。

#### 战略增援与运输Party

1. 普通顺路兵力优化**不能为了提高兵种匹配而主动跨城行军**。
2. 确有边境驻军防御/兵粮缺口、且本地恢复不足时，可组织**Clan内现有AI领主Party**专程增援，携带**士兵与粮食**（允许纯运粮、纯运兵）。
3. 增援任务期间不受新的Army召集，也不能自行创建Army；已经在Army中的Party**不得强制脱离Army**。
4. 装运优先级：若Party可安全移交的**自身富余普通兵员≥20人**，优先使用；不足20人则优先从其他Clan驻军取得可安全调拨的兵员。自身富余不够满足总需求时，可再使用驻军兵源；所有装运受Party真实硬编制约束。
5. 必须保留Party执行任务自身的最低安全兵力、途粮、物品载重；不得牺牲来源驻军必要防御能力；不能依据计划产生士兵。
6. **最关键待定**：边境城堡无敌军威胁、村庄被洗劫导致生产中断时，兵力降到什么程度才请求外援。**本版不设具体阈值，封装API，首轮返回`NoRequest`。**

---

### 3. 兵种价值与驻军防御需求

#### 最小可行价值模型

基础已可复用现有`RecruitmentTroopClassifier`的`Infantry/Ranged/Cavalry/HorseArcher`与`LowTier/MiddleTier/TopTier`。第一阶段允许`BasicTroopValueModel`采用**兵种Tier或本体可用军事战力**作为稳定基础值；`Field/Garrison`暂取相同基础值或少量已知加权。

但**不能**直接用这个粗糙评分触发大规模高低级互换或裁撤；启用自动Swap前必须经过最低收益阈值与守军安全模型。第一版`AutoSwapEnabled=false`，保留BuildPlan可测试，返回`NoExchange`为正常结果。

以后完整评分：

```text
FieldMarginalValue = U(Party加入兵种后的战力/构成) - U(当前Party)
GarrisonMarginalValue = U(驻军加入兵种后的防御) - U(当前驻军)
SwapBenefit = ΔFieldValue + ΔGarrisonValue - TransferCost - RiskCost
```

兵种质量/角色比例只作软偏好，不应导致现有合法高级士兵被硬性剔除；评价应考虑军饷、伤员、兵种职责（攻城守城/机动野战）、边境形势。

#### 驻军防御的“最低保留”与“自动补员”必须解耦

- `AutoRecruitmentTarget = floor(HardLimit*0.80)`：是否自动新增兵。
- `DefenseRequirement`：守城实际军事需要，**不等于80%目标**。
- `TransferableStrength`：调出后仍满足防御要求的剩余战力，不能简单以80%以上人数作为可调上限。

首轮API：

```csharp
IGarrisonDefenseRequirementModel.Evaluate(settlement)
IGarrisonDefenseRequirementModel.GetSafeTransferOutCount(snapshot, roster)
```

可先用已有`GarrisonTransferPolicyModel.IsEnemyBorderFortification`和原版/现有理想驻军计算构成**保守简易最低留守值**。对于判定困难的未知威胁和临战来源，`GetSafeTransferOutCount`固定返回0以避免抽空城堡；不能为赶进度默认全部可调。

#### 外援阈值：明确待定Stub（用户要求）

对“边境城堡无直接敌情、附属村庄被洗劫、生产中断”不固化50%或60%等数值：

```csharp
public sealed class ExternalReinforcementNeedStubModel
    : IExternalReinforcementNeedModel
{
    public ReinforcementNeedResult Evaluate(GarrisonDefenseSnapshot s)
        => ReinforcementNeedResult.NoRequest("ThresholdPolicyNotImplemented");
}
```

- 初版**返回固定`NoRequest`**，有日志标识“尚未开启策略”，不假装增援已实现。
- 测试注入`FakeNeedModel`可返回`Routine/Urgent/Emergency`以贯通整条任务链。
- 后续替换为基于**防御战力、近7日本地产能、敌军威胁、粮食真实消耗与到达时间**的模型，不需要修改任务状态机与Patch。
- 后续可以另设“围城紧急增援”策略，但若发现无法安全抵达，仍禁止派兵送死。

---

### 4. Party—驻军的双向交换、封邑兵返乡与复员

#### 操作分类

| 类型 | 兵员变化 | 第一版 |
|---|---|---|
| `FieldToGarrison` | Party减、驻军增，Clan总数不变 | **实现** |
| `GarrisonToField` | Party增、驻军减，Clan总数不变 | 复用已有API，加审批Context |
| `Swap` | Party A换驻军B，双方人数可能不变，Clan总数不变 | 模型/事务接口先搭，默认不开启自动交换 |
| `FiefReturn` | Party在役封邑兵回来源封邑/冷却 | 复用既有FiefServiceAPI |
| `Dismiss` | Clan真实兵员下降 | 必须经单独`IDemobilizationPolicy`允许；**默认`false`** |

#### 局部触发

只在Party**正常到达同Clan城镇/城堡**，以及已经存在的明确回营/复员动作时评估。没有重大战略需求时，不为获得几分优化收益专门设置目的地。

`GetAllowedDonationCount`当前按`max(30,min(编制,工资,粮食))`算富余，需要增加一个以`PartyForceTargetResult`为依据的`GetSafeFieldSurplus`；同时保护职业骨干与正在Army/战斗/增援中的任务Party。普通局部交换不改变Party军事行为。

#### 交换计划、执行与守恒

建议`ExchangePlan`包含：

```text
SourceParty, TargetGarrison, ExpectedSnapshots
FieldToGarrison[CharacterObject,Count,Wounded]
GarrisonToField[CharacterObject,Count,Wounded]
FiefServiceReservation = None (普通交换不移动在役封邑兵)
Preconditions: OwnerClan / MapEvent / Army / siege / hard capacity / wages / defense floor
ExpectedFieldCountAfter, ExpectedGarrisonCountAfter
ReasonCode, BenefitScore, ExpireTime
```

执行Service按照“**先整体计算执行后的两侧名册，再逐步应用**”的方式预审双方容量、军费、实际持有数、伤员数、归还预留征粮队占位与封邑服役身份。对未批准/过期计划放弃执行，而不是悄悄解散多余兵。

为避免 `AddToCounts` 部分执行产生重复，应优先实现批处理回滚/逆操作日志，并将同一事务的日志写入文件。若无法确保整笔事务安全，第一版宁可只允许单向转移；启用Swap前必须通过异常注入测试。

#### 驻军80%留下的空位

第一版不预约空位。正常自动补员止于80%，相当于自然预留缓冲；到达时重读硬编制及征粮队预留数。若驻军已超过80%却未超硬编制，可接收；不要为了恢复80%而清理部队。

---

### 5. Clan防御增援：现有领主Party携兵携粮

#### 明确与局部优化不同

请求源：**Clan城镇/城堡**；承运：**现有AI领主Party**；可用来源：Clan其他安全驻军、领主自己的合格富余士兵，以及真实可提取粮食库存（具体对非Clan来源的采购权限由Logistics Adapter审查）。

只有`IExternalReinforcementNeedModel`返回需要增援、`IReinforcementRouteModel`确认安全可行、`IArmyMissionProtectionModel`确认Army免征召保护已生效，Service才创建活跃任务。

#### 增援装载优先序

```text
RequestedTroops = 接收地经模型批准的需求
OwnEligibleSurplus = max(0, PartyCurrent - ProtectedFieldTarget)
                ∩ 可移交普通兵员 ∩ 自保需要

if OwnEligibleSurplus >= 20:
    优先使用Party自身富余士兵（可小于总需求）
    若剩余需求 > 0，再评估驻军可安全调拨兵员
else:
    不主动从该Party拆出这1～19名富余兵员
    优先使用驻军可安全调拨兵员

实际装载量 <= PartyHardLimit - 当前人数
实际交付量 <= 接收驻军硬空位（含征粮队预留）
```

注意：Party自身富余兵员已经在Party名册中，装载时**不能再把它们加一次**。`OwnCargoManifest`只是**为已有成员标注交付配额**，不是增员操作。来自内陆驻军的`GarrisonCargoManifest`才真实发生源驻军减员/Party增员。抵达交付后才减少Party实际士兵并加入目标驻军。

**示例A**：Party 110人，和平目标53，可安全移交57，目标缺80；优先自带57。如果Party硬上限150，当前仅能另载40，考虑本身需要，最多另取23即可一次完成80。  
**示例B**：Party65，目标53，自有富余12（不足20），默认不拆这12，优先从安全驻军装载；装载仍不得使Party超硬上限。  
**示例C**：Party120/150，自身富余67，目标缺100，不能一次装载67+33（超过硬上限），可先送67再议下一趟；不要假装已运输100。

#### 粮食装载与卸载

单独建`FoodCargoManifest`，至少记录物品种类、原来源、计划数量、实际装载、实际交付、途中消耗或损失。数额受以下约束：

- 来源仓库/市场真实库存与不可挪用的留存量；
- Party自用粮储备（不能以全部粮食作为任务货物）；
- Party实际载重、驮畜和移动速度；
- 目标驻军允许接收的真实粮量；
- 实际目标和任务状态仍有效。

**必须区分“货物口粮”和“运输Party自用口粮”**；游戏若将二者放在同一`ItemRoster`，要在任务账本里记录可交付上限，并在到达时以实际剩余库存再次计算，严禁使Party交货后立即断粮。

现有 `SupplyPartyBehavior` 的购买/路线功能可抽取纯后勤服务复用，但**不能**直接把领主Party改造成`SupplyPartyComponent`临时队，也不能对它调用临时征粮队完成时的`DestroyPartyAction`。

#### 任务状态与取消、恢复

```text
Requested → ToSource → Loading → ToDestination → Unloading → Completed
                 └─────────────► Cancelled / Fallback
```

- 仅当需要到源驻军取兵取粮时才出现`ToSource`；自身货物足够则可直接去目的地。
- Loading时复核来源驻军可调兵额；Unloading时复核目标城堡所有权、围城、容量和可接收粮量。
- 中途战损造成货物士兵死亡，应依据实时名册减少可交付数；不能据原计划补兵。
- 取消时：自身士兵货物标记解除；从其他驻军装载且尚存的兵员应尽可能退还来源或安全Clan据点；无法安全送回时保留在Party并更新归属/任务状态，**不可凭空删除**。
- 所有权变化、Party被俘/毁灭、读取存档、进Army、目标被围攻、目的地失效均应触发重新校验。不能出现永远不结束的任务。
- 一次Party仅有一个活跃增援任务；同一目标请求计入已在途兵粮，防止多队重复搬运。

#### Army排除：必须同时保护被召集和自行创建

核对原版：

1. `DefaultArmyManagementCalculationModel.GetMobilePartiesToCallToArmy()`列出可召集对象；默认候选比例0.6，应从**最终候选列表排除活跃增援任务Party**。
2. `AiMilitaryBehavior`另有`PartySizeRatio > 0.6`时创建Army的判断；仅拦截候选列表**不足以防止该Party自己创建Army**。

实施要求：

```csharp
IArmyMissionProtectionModel.IsProtected(party) // 完成或取消后立即false
IArmyMissionProtectionModel.MayStartReinforcement(party) // Army != null → false
```

- Patch 仅检查Service中的有效任务标识并排除候选；**不得通过全局降低Army门槛**实现。
- 自建Army需要针对本体真实决策的**窄切入点**验证可拦截位置。**不要Prefix整个`AiMilitaryBehavior.AiHourlyTick`并`return false`**，那会阻断Party其他军事决策。
- 不建议永久启用`Ai.DoNotMakeNewDecisions`，以免抑制正常逃避敌军和即时战斗行为。
- 当已收到Army召集/在向Army集结途中时，不得开启增援任务；如果任务开始后才发生外部召集，任务保护优先，完成后恢复资格。
- **上线门禁**：未通过“被召集+自行建军”双重阻断自动化测试前，`EnableAutomaticReinforcement=false`，不得为了赶交付让任务带着一半保护运行。

---

### 6. 尚未确定的策略与稳定边界

1. 边境无敌军威胁、附属村庄停产时，究竟需要多少防御战力缺口/预计修复天数才请求外援。
2. 急救援的敌军战力阈值及安全路径如何评估；本体导航能否给出可靠ETA。
3. 驻军高级射手、野战高级骑兵等不同角色的边际价值权重。
4. 已有`GarrisonLogisticsModel`的模拟粮食需求与实际驻军消耗的启用关系。
5. 目标驻军存在80%～90%的空间时，怎样在多个同时在途的增援Party间分配到达名额（首版不预约，只在实际交付时再审）。
6. Clan财政长期无法承担的职业兵/雇佣兵，应在什么条件下批准真裁军；第一版禁止自动裁撤。
7. Party战争中低倾向时，是否仍保持“和平最低50人”作为补员底线；当前只将50设为和平规则，不擅自更改战时主战资格门槛。
8. 对整支Party解散合并驻军的历史Patch是否增加新的容量Context以避免与80%新增策略相互误伤；不可与普通复员混淆。

这些待定项不应散入Patch或构成M0～M4编译阻碍；用固定值、安全开关和接口模拟测试隔离。

---

---

## 第二部分：实现方案

这一部分列出最新仓库中的实际代码入口、新增模块、接口签名、分阶段实施以及必须通过的测试；各类`建议新增`的类型均属**待实现**。


### 7. 当前代码基线及功能差距

> 以下基于`e669c477`下的代码阅读核对；“功能存在”不等于已对本方案新规则提供支持。

| 事项 | 当前状态 | 现有位置 / 与本方案的关系 |
|---|---|---|
| 驻军自动新增职业兵及俘虏 | 已有 | `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`；生产候选来自 `SettlementManpowerProductionBehavior`，统一`BuildPlan`后按日额度及空位执行 |
| 封邑兵产出、征召、归还、批次与Hearth对账 | 已有底层 | `Fief/Models/FiefPartyManager.cs`、`Fief/Models/PartyData/FiefPartyRecruitment.cs`、`FiefPartyReturn.cs`，已有`FiefServiceBatch`与`TransferFiefService` |
| AI招募雇佣兵、从自家驻军取兵 | 已有 | `Recruitment/Behaviors/AiRecruitmentBehavior.cs`、`Recruitment/Patches/AiSettlementRecruitmentPatch.cs`、`GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison` |
| AI访问驻军时交出士兵 | 已有**部分**本体桥接 | `Garrison/Patches/GarrisonTroopDonationPatch.cs` Patch `LeaveTroopsToGarrison`，通过 `GetAllowedDonationCount`限制；目前不是统一和平复员计划，不应误认为新系统已实现 |
| 整支Party解散合并驻军 | 已有但**不是普通复员入口** | `Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs`；按`BuildPlan`接收，不接受的剩余普通兵员会随整Party清空。**不能复用此流程做活跃Party日常回营** |
| 驻军与Party按军事实用价值双向交换 | 未实现 | 需新建Allocation Model、Exchange Planner、Transfer Service |
| Clan跨驻军防御兵粮增援，由已有领主Party承运 | 未实现 | 新任务管理、装卸清单、Army保护、存档恢复及异常结算 |
| 驻军征粮队 | 已有，但不同业务 | `Garrison/Supply/SupplyPartyBehavior.cs`由**缺粮驻军自身抽50人**创建临时SupplyParty；不是领主Party增援，不能直接替代 |
| 外出征粮队计入驻军有效编制 | 已有 | `Garrison/Models/GarrisonRecruitmentCapacity.cs`中的`GetActiveSupplyEscortCount`等，需继续纳入所有硬容量审批 |
| 自动补员软上限 | **当前95%，需调整** | `GarrisonRecruitmentCapacity.RecruitmentFillPercentage = 95` |
| 统一招募容量 | **当前另有95%，需解耦** | `Recruitment/Models/DefaultAIRecruitmentModel.cs`中的私有`RecruitmentFillPercentage=95`，其`EvaluateRecruitment`计算安全编制 |
| 领主定居点招募入口 | 已有95%门槛 | `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` Prefix内有`PartySizeLimit * 95`硬编码 |
| 领主每日普通征兵判断 | 尚未支持新目标 | `AiRecruitmentBehavior.CalculateNeedScore()`目前缺口用`1-PartySizeRatio`，`FindBestMultiPurposeSettlement`仍用`<0.85`，`OnDailyTick`直接跳过`Army != null` |
| 战争倾向 | **存在于另一Mod** | `ModifiedPolitics/WarDisposition/Models/Persistence/WarDispositionManager.cs`，`TryGetData`与`WarDispositionData.Value`；数值[-100,100]、`GetLevel`以`20/21`作为中立/主战边界 |
| 两Mod的编译依赖 | **当前不存在显式依赖** | `ModifiedArmy/ModifiedArmy/ModifiedArmy.csproj`未引用ModifiedPolitics；`ModifiedArmy/ModifiedArmy/SubModule.xml`仅声明ModifiedTroop依赖。必须设计桥接/降级，不可直接假设能调用政治Mod类 |
| 军事制度兵源权重 | 已有 | `ModuleData/militaryConfig.xml`中的`MilitaryDoctrine fief/professional/mercenary`及`SituationalModifiers`，`common/MilitaryConfig.cs`负责解析 |
| Army门槛 | 原版现存 | `code/DefaultArmyManagementCalculationModel.cs`的`AIMobilePartySizeRatioToCallToArmy=0.6`；`AiMilitaryBehavior`还有独立的创建Army门槛`PartySizeRatio>0.6` |

#### 特别澄清：驻军粮食口径

`GarrisonLogisticsModel`目前以驻军实际物品及模拟消耗计算`FoodSupplyDays`，代码注释声明本批次**驻军实际消耗尚未启用**。因而不能简单按“模拟少于15天”无限制派遣真实领主运输队。粮食增援必须通过明确的`ILogisticsEmergencyPolicy`与“实际运行口径”开关审核；基线先保守禁用自动派遣。

#### 特别澄清：本地生产中断

`SettlementManpowerProductionBehavior.CalculateVillageSupply()`在`VillageState != Normal`时返回0；故遭劫掠的附属村庄确实会削弱共享兵源产出。但**村庄遭劫掠本身不是外援硬触发**；驻军是否请求支援仍需通过`IExternalReinforcementNeedModel`判断。

---


#### 实现顺序与失效默认值必须显式区分

| 功能 | 设计约束 | 立即实现 | 骨架默认行为 |
|---|---|---|---|
| Party人数目标 | 和平 `max(50,35%)`、主战 `D>20` 才主动超过60% | 是 | 不适用 |
| 驻军自动新增士兵 | 80%软上限、按征粮队占位校核 | 是 | 不适用 |
| Party向驻军移交 | 不含在役封邑兵、普通转移容量90%、整体守恒 | 是 | 容量或权限不明时拒绝 |
| 封邑兵返乡 | 依据来源批次、真实返还与Hearth结算 | 复用并完善 | 来源不明时不得擅自返还 |
| 满编驻军高级/低级Swap | 以Clan整体战力为先，结果须可回滚 | 先建接口 | `NoExchange` |
| 外援请求阈值 | 依据防御缺口/本地恢复/粮食/敌情 | 先建接口 | `NoRequest` |
| 增援路径与交通风险 | 无安全路线就不得启动 | 先建接口 | `RouteUnsafeOrUnknown` |
| Army召集/创建保护 | 承运领主任务期间双向阻断 | 必须先打通并验证 | 保护未验证则禁止启用任务 |
| 财政困难裁撤 | 独立审批，绝不作为转移失败副作用 | 先建接口 | `CanDismiss=false` |

**先定义行为边界，再允许启用开关**：不得为使演示流程成功而将Stub默认改为`true`。可由测试专用Fake Model注入可派遣、可交换结果，但正式游戏逻辑必须默认保守。

### 8. 推荐分层结构与依赖方向

#### 推荐目录（建议新增，不是已存在路径）

```text
ModifiedArmy/ModifiedArmy/
  ForceManagement/
    Contracts/
      ForceManagementTypes.cs            // 枚举、请求、只读快照、结果
      IWarDispositionProvider.cs
      IPartyForceTargetModel.cs
      IRecruitmentAdmissionModel.cs
      IGarrisonDefenseRequirementModel.cs
      IExternalReinforcementNeedModel.cs
      ITroopValueModel.cs
      IPartyGarrisonExchangeModel.cs
      IReinforcementRouteModel.cs
      IArmyMissionProtectionModel.cs
    Models/
      PartyForceTargetModel.cs
      RecruitmentAdmissionModel.cs
      GarrisonDefenseRequirementModel.cs
      ExternalReinforcementNeedStubModel.cs
      BasicTroopValueModel.cs
      PartyGarrisonExchangeModel.cs
      ReinforcementCargoModel.cs
      ReinforcementRouteStubModel.cs
    Services/
      ForceManagementFacade.cs           // 单一公共入口/依赖装配
      TroopTransferService.cs            // 真实转移、校验、守恒与回滚
      FiefReturnService.cs               // 原封邑返乡适配
      ReinforcementMissionService.cs     // 创建/推进/取消任务
      WarDispositionBridge.cs            // ModifiedPolitics只读适配
      RecruitmentSourceService.cs        // 已有征兵行为的服务化入口
    Behaviors/
      ForceManagementBehavior.cs         // Daily/SettlementEntered/Load生命周期
      ReinforcementMissionBehavior.cs    // 任务状态推进及存档
    Patches/
      SettlementRecruitmentBridgePatch.cs
      GarrisonExchangeBridgePatch.cs
      ArmyCallEligibilityPatch.cs
      ArmyCreationEligibilityPatch.cs
    Diagnostics/
      ForceManagementLog.cs
```

**不要求第一阶段一次创建以上所有文件**；先创建Contracts、Facade、Model、关键Service与薄Patch即可。若模块体积较小，可合并纯DTO文件，但不得将复杂判断写回Patch。

#### 依赖方向

```text
Bannerlord Events / Harmony Patch
          │ 仅收集参数 / 调用 / 返回原版所需值
          ▼
   ForceManagementFacade
          ├── IPartyForceTargetModel
          ├── IRecruitmentAdmissionModel
          ├── IGarrisonDefenseRequirementModel
          ├── IExternalReinforcementNeedModel [Stub]
          ├── ITroopValueModel [Basic/Stub]
          ├── IPartyGarrisonExchangeModel
          ├── IArmyMissionProtectionModel
          └── ReinforcementMissionService
                         ├── TroopTransferService
                         ├── FiefReturnService
                         └── Cargo/Route/Storage adapters
                                  │
                                  ▼
                              游戏真实名册
```

- **Models**：尽量对不可变快照运算；禁止直接调用`MemberRoster.AddToCounts`、`SetMove...`、扣Hearth或改预算。
- **Services**：唯一有权执行士兵转移、消耗库存、发出任务命令和变更持久化状态。
- **Behaviors**：监听DailyTick、SettlementEntered、GameLoad、PartyDestroyed、SettlementOwnerChanged等；调度Service，负责防重复、防震荡、状态存档。
- **Patches**：补丁方法尽可能在10～30行范围，完成安全条件判定后交给Facade。由Service内部实现复杂算法。
- **配置**：已确定参数可在XML配置，所有缺省值集中在`ForceManagementDefaults`；不要在不同Patch里复制数值。
- **适配器**：政治Mod、原版Army系统和已有驻军/封邑API分别通过Adapter进入，降低修改影响面。

---

### 9. 核心DTO、Model/API契约与审批上下文

> 以下为**建议签名**，可按现有项目命名空间与Bannerlord版本微调；关键是稳定输入输出契约，而不是强迫逐字复制编译。

```csharp
public enum ForceScenario { Peace, WarRoutine, ArmyGathering, OwnFiefDefense }
public enum ForceOperation { NewRecruitment, FieldToGarrison, GarrisonToField, Swap, FiefReturn, Reinforcement }
public enum ReinforcementPriority { None, Routine, Urgent, Emergency }
public enum MissionState { Requested, ToSource, Loading, ToDestination, Unloading, ReturningOrFinishing, Completed, Cancelled }
public enum CancelReason { None, InvalidOwner, ArmyConflict, EnemyThreat, InvalidDestination, NoCargo, RouteBlocked, Expired }

public sealed class PartyForceTargetResult {
    public ForceScenario Scenario { get; init; }
    public bool IsAggressiveWarClan { get; init; }
    public float WarDisposition { get; init; }
    public int PartyLimit { get; init; }
    public int CurrentCount { get; init; }
    public int TargetCount { get; init; }
    public int GapCount { get; init; }
    public bool CanStartRecruitmentVisit { get; init; }
    public string ReasonCode { get; init; }
}
public interface IPartyForceTargetModel {
    PartyForceTargetResult Evaluate(PartyForceContext context);
}

public sealed class RecruitmentCapacityContext {
    public ForceOperation Operation { get; init; }
    public int RequestedCount { get; init; }
    public int HardLimit { get; init; }
    public int EffectiveCurrentCount { get; init; }
    public int TargetCount { get; init; }
}
public interface IRecruitmentAdmissionModel {
    RecruitmentApproval Evaluate(RecruitmentCapacityContext context);
}

public interface IWarDispositionProvider {
    bool TryGet(Clan clan, out float disposition, out bool atFormalWar);
}

public sealed class GarrisonDefenseSnapshot {
    public Settlement Settlement { get; init; }
    public float CurrentDefenseValue { get; init; }
    public float RequiredDefenseValue { get; init; }
    public int CurrentRegularCount { get; init; }
    public int HardLimit { get; init; }
    public int EscortReservedCount { get; init; }
    public float LocalSevenDayRecoveryValue { get; init; }
    public float SimulatedFoodDays { get; init; }
    public bool HasVerifiedActualFoodConsumption { get; init; }
}
public interface IGarrisonDefenseRequirementModel {
    GarrisonDefenseSnapshot Evaluate(Settlement settlement);
    int GetSafeTransferOutCount(GarrisonDefenseSnapshot snapshot, TroopRoster roster);
}
public interface IExternalReinforcementNeedModel {
    ReinforcementNeedResult Evaluate(GarrisonDefenseSnapshot snapshot);
}

public interface ITroopValueModel {
    float GetFieldMarginalValue(PartyForceSnapshot field, CharacterObject troop, int count);
    float GetGarrisonMarginalValue(GarrisonDefenseSnapshot garrison, CharacterObject troop, int count);
}
public interface IPartyGarrisonExchangeModel {
    ExchangePlan BuildPlan(PartyForceSnapshot field, GarrisonDefenseSnapshot garrison);
}
public interface IReinforcementRouteModel {
    RouteEvaluation Evaluate(MobileParty carrier, Settlement source, Settlement destination);
}
public interface IArmyMissionProtectionModel {
    bool IsProtected(MobileParty party);
    bool MayStartReinforcement(MobileParty party);
}
```

建议DTO只携带必要只读快照，避免各Model重复遍历`MobileParty.All`和实际名册。快照采集由`ForceManagementFacade`统一完成；执行前由Service再次读取真实游戏状态，不能信任过期快照。

#### 招募审批统一语义

必须分清三个上限：

```text
HardLimit                    Party/驻军实际硬容量
NewRecruitmentTargetCount    本次允许新增士兵的目标（驻军80%/Party场景目标）
TransferCapacity             既有士兵普通转移容量（驻军/Party最高90%）
```

例如：驻军硬上限300，外出征粮队20，驻军实际230：有效人数250。自动补员目标240，因此**自动新增0**；普通转移最多接收`270-250=20`（仍须经过财政和防御审批）；已预留的征粮队返还可到300硬上限。

`DefaultAIRecruitmentModel`不能单纯把95改为80，否则本次要把Party补到95%的合法战时招募会被错误卡在80%。建议增加`RecruitmentPolicyContext`或审批参数，由统一模型区分`NewRecruitment`和`Transfer`；旧方法签名保留适配重载，先保证原调用点不破坏。对已有私人硬编码95应逐步去除。

#### NeedScore原则

```text
Gap = Max(0, TargetCount - ActualCount)
Gap == 0 => NormalRecruitmentAllowed = false
Gap > 0 => NeedScore = 当前XML权重 * [Gap/TargetCount、战争紧迫度、财政承受力]
```

即使战争紧迫度与财政分很高，只要已达目标，不能继续常规补员。`85%`常规启动阈值应对`PeaceCurrent<50`单独放行。补员动机不应直接被`AiBehavior.RaidSettlement`等行为作为门槛。

---

### 10. WarDisposition跨Mod桥接

当前ModifiedArmy未显式依赖ModifiedPolitics，因此不能简单在前者引用`WarDispositionManager`后假定项目可直接编译。

**推荐**：把`IWarDispositionProvider`作为唯一依赖；具体桥接方案在第一阶段按Mod实际加载顺序选定：

1. 若ModifiedPolitics必须随套件启用：明确工程引用和`SubModule.xml`依赖，编译期使用`WarDispositionManager.TryGetData(clan,out data)`，读取`data.Value`及`IsInFormalWar`。
2. 若其应当可选：在`WarDispositionBridge`使用**一次性解析/缓存的运行时适配器**，不要在各业务Model散布反射；必须对模块缺失、API变化、存档初始化失败进行测试。

失效默认：`TryGet=false`→`D=0`**中立**，不得因此误判为主战Clan；战争状态从游戏实际外交关系独立读取，不把`WarDisposition`读取失败等同和平。发生降级写入**文件日志**一次/每Clan限频，不打印大量UI异常消息。

---

### 11. 待定策略Stub清单与替换策略

| API/策略 | 首轮固定/简化返回 | 为什么这样安全 | 后续何时替换 |
|---|---|---|---|
| `IExternalReinforcementNeedModel.Evaluate` | `NoRequest("ThresholdPolicyNotImplemented")` | 用户决定后续再设计增援阈值；不凭空触发跨城任务 | 实测边境生产中断/敌情/本地恢复后 |
| `IReinforcementRouteModel.Evaluate` | `RouteUnsafeOrUnknown`，不发任务 | 未验证AI专程行军风险前避免送死 | 实现真实导航/敌情/ETA与载重后 |
| `ITroopValueModel` | Tier/本体基础战力简化分数；不给出自动裁军资格 | 先建立可调用接口，不将粗评分当作裁军依据 | 有野战/守城边际效用实测后 |
| `IPartyGarrisonExchangeModel.AutoSwap` | `NoExchange`（允许纯Model单元测试） | 首轮不开启可能导致兵种来回震荡的全自动交换 | 交换事务回滚、军饷和滞回测试完成 |
| `IGarrisonDefenseRequirementModel.GetSafeTransferOutCount` | 无法核实安全边界时返回`0` | 不抽空来源守军 | 接通已有前线/内陆标准和敌情模型 |
| `IDemobilizationPolicy.CanDismiss` | `false` | 保护Clan总体兵力，避免隐性清兵 | 未来明确财政紧急裁军制度后 |
| `IWarDispositionProvider`缺失降级 | `D=0`，`IsAggressive=false`，战争状态另取原版 | 不因未装政治Mod变主战 | 桥接稳定后为正常真实值 |
| `IArmyMissionProtectionModel.IsProtected` | 对不存在有效任务返回`false`，对已建活跃任务必须返回`true` | 不无故排除普通Party；增援任务激活前须验证Patch | 第一轮实现并测试，不长期Stub |
| `AutoRecruitmentFillRatio` | **0.80真实规则** | 已明确决定，不是Stub | 可配置后用于平衡 |
| `PeaceMinCount`、`OwnSurplusMinDelivery` | **50、20真实规则** | 已明确决定，不是Stub | 可配置，非随意更改 |

> 原则：**返回固定值并不意味着可以越过真实安全审查**。`Unknown`场景一律采取保守不派、不裁、不抽兵、不交换；需要测试全流程时使用Fake Policy向Service注入预设结果，而不是把线上Stub临时改为`true`。

---

### 12. 薄Harmony Patch与Behavior事件接入

#### 单一业务入口

```csharp
// 示意，不是可直接编译的项目补丁
static bool Prefix(MobileParty mobileParty, Settlement settlement) {
    // 验证活动Party、游戏阶段和Patch范围后：
    return ForceManagementFacade.Current
        .HandleSettlementRecruitment(mobileParty, settlement)
        .ShouldRunNative;
}
```

**Patch不能**直接调用`FindBest...`、评价兵种价值、遍历所有Clan驻军、修改多个Roster、操作`WarDisposition`读取、推导军饷或决定跨城路线。跨模块实质判断属于Model/Service。

#### 建议至少四个薄接入点

| 接入点 | 薄Patch或Behavior的责任 | 业务实现位置 |
|---|---|---|
| `RecruitmentCampaignBehavior.CheckRecruiting` | 把`mobileParty/settlement`交给Facade、选择是否继续原版 | `RecruitmentSourceService` + `RecruitmentAdmissionModel` |
| `GarrisonTroopsCampaignBehavior.LeaveTroopsToGarrison`及取兵 | 向Facade请求批准数量、必要时根据结果记录真实变化 | `TroopTransferService` + `PartyGarrisonExchangeModel` |
| Army候选/自建入口 | 检查`IArmyMissionProtectionModel.IsProtected`，不做任务规划 | `ReinforcementMissionService`维护保护生命周期 |
| `CampaignEvents`的Daily/SettlementEntered/Destroyed/Load等 | 组织单次评估、推进任务状态 | `ForceManagementBehavior` / `ReinforcementMissionBehavior` |

当前`AiSettlementRecruitmentPatch`包含多兵源招募执行和概率逻辑，重构时先迁出业务函数，再让原Patch变薄；**不要在未经验证的情况下另加第二个相同Prefix重复接管原版招募**。

---

### 13. 军事配置与向后兼容解析

实际解析器在`common/MilitaryConfig.cs`，新增配置前必须同步扩展`Deserialize/ParseAi`、`AiRecruitmentConfig`及默认值。**新增节点建议可选、缺失回退默认值**，不要破坏旧存档及现有XML加载。示例：

```xml
<!-- 作为现有 <AiRecruitment> 的可选子节点；先扩展解析器 -->
<ForceManagement>
  <PeaceTargetRatio value="0.35" />
  <PeaceMinimumCount value="50" />
  <AggressiveWarDispositionThreshold value="20" />
  <ArmyGatheringTargetRatio value="0.90" />
  <OwnFiefDefenseTargetRatio value="1.00" />
  <GarrisonAutoRecruitmentRatio value="0.80" />
  <MinimumOwnSurplusDelivery value="20" />
  <MinimumFiefRecruitBatch value="25" />
  <EnableAutomaticSwap value="false" />
  <EnableAutomaticReinforcement value="false" />
</ForceManagement>
```

战争倾向的分段锚点建议在`WarTargetCurveModel`集中定义并测试；后续有需要再给XML配置`WarDispositionPoints`。避免在XML/Model/Patch三处各复制一套曲线。`MilitaryDoctrine`原有七文化相对权重及`SituationalModifiers`保持原结构，不新增与其重复的“战时兵源偏好百分比”。

---

### 14. 文件级修改位置与代码迁移清单

| 现有文件 | 计划修改 | 原则 |
|---|---|---|
| `ModifiedArmy/ModifiedArmy/Main.cs` | 注册新的Facade装配、CampaignBehavior；检查Patch开关及存档类型 | 统一初始化，避免散在多个Behavior重复初始化 |
| `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` | 可选解析`ForceManagement`配置、默认值、校验 | `MilitaryDoctrine`继续保持旧格式 |
| `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` | 写入已确认参数与特性开关 | 尽量不修改已有文化参数 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/AiRecruitmentBehavior.cs` | `OnDailyTick`转发统一Party目标/派遣审批；`CalculateNeedScore`依据目标缺口；`FindBestMultiPurposeSettlement`消除无目标场景的0.85误招 | 不在Behavior散落WarDisposition公式 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | 移除硬编码95对新目标的误限，转给Facade重新核验；既有封邑/驻军/雇佣兵执行服务化 | Patch变薄，保留原版不应拦截的特殊Party分支 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs` | 统一招募审批新增上下文，按Party目标与驻军80%分类；修正95全局假设 | 不影响Role/Quality软偏好和财政审批 |
| `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs` | 自动补员比例95→80，保留`GetHardFreeSlots`与征粮队预留 | 软/硬容量独立 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 自动新增用80%规则；预测与实际调用相同审批上下文 | 超过80%的既有驻军不可因此被清理 |
| `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonTransferPolicyModel.cs` | 暴露可复用的取兵/捐兵资格；接入Party目标及防御保留模型 | 尽量复用已能执行真实Roster转移的代码 |
| `ModifiedArmy/ModifiedArmy/Garrison/Patches/GarrisonTroopDonationPatch.cs` | 接入局部交还与交换Facade；协调本体捐兵路径，避免双重转移 | Patch只桥接，避免重复原版捐兵 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`及`FiefPartyReturn.cs` | 补足可查询批次、定向归还结果API、移交时在役身份隔离；不重造返乡账本 | 仅真实返乡才恢复Hearth/冷却 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs` | 统一AI目标人数审批与一次成功至少25封邑兵约束（如当前版本尚未实现） | 不影响玩家手动征召 |
| `ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs` | 可选择抽出纯粮食采购/路线工具供Service复用；保持原征粮队行为独立 | 不能将现有领主Party销毁/当作SupplyPartyComponent |
| `ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonLogisticsModel.cs` | 新增“实际消耗口径/模拟口径”只读结果供任务策略判断 | 粮食模拟不足不直接触发无限运粮 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs` | **原则上不用于本方案活跃Party日常转移**；仅审核与本方案容量上下文是否冲突 | 保留整Party解散本来语义 |
| `ModifiedArmy/ModifiedArmy/SubModule.xml`及`csproj` | 如采用强依赖政治Mod才添加；若可选则仅在Bridge隔离适配 | 不随意破坏用户组合Mod安装 |
| `code/DefaultArmyManagementCalculationModel.cs`、`code/AiMilitaryBehavior.cs` | **仅参考的本体反编译源码，不修改** | Harmony窄补丁不修改反编译文件 |

---

### 15. M0—M6分阶段落地路线

#### M0. 建骨架和基线回归（不启用新行为）

- 新建Contracts、Facade、Defaults、诊断、Model注册/测试替身。
- 保持原自动补员、封邑兵/雇佣兵招募和已有驻军取兵的入口继续工作。
- 写基本日志：每日每Clan/Party目标值、审批原因；不要用UI弹窗打印异常。
- 验收：不装ModifiedPolitics时可运行；其他既有路径无回归。

#### M1. Party人数目标与招募审批（优先实际落地）

- 接入`IWarDispositionProvider`，验证真实读取`Value`及正式战争状态。
- 建`PartyForceTargetModel`、`RecruitmentAdmissionModel`、`ForceManagementConfig`。
- 和平35%/最低50；中立D≤20不主动扩军至60%以上；主战D>20到95%；集结90%；自家封邑救援100%。
- 修复`NeedScore`以真实场景缺口触发、到站二次审批；消除Party/驻军不同业务共享95%硬编码。
- 对Army已附着、正在战斗和紧急救援的Party禁止不合理重派。
- 复用文化MilitaryDoctrine和财务模型，不新增重复兵源系统。
- 验收：WarDisposition分界值`20/21`、PartySizeLimit`80/100/150/200`、到达前任务变更全部测试通过。

#### M2. 驻军80%自动新增与活跃Party归营

- 驻军自动新增上限80%，普通移交上限90%，补员预测和执行一致；征粮队预留及返还硬上限继续计算。
- 新建`TroopTransferService`与`PartyGarrisonExchangeModel`，首版启用简单的**单向Party→驻军真实移交**。
- 封邑兵归还单独走原来源批次，不错误转入普通驻军；保留已超和平目标的职业兵。
- Swap和裁军接口存在，但默认不启用；低价值/高价值计算有基础Stub供测试。
- 验收：驻军240/300停止自动新增但能通过普通转移增长到270；外出50名征粮队正确占容量并可返还至硬上限；没有士兵消失、重复或Hearth错误。

#### M3. 双向交换事务与战力模型改进

- 实现`ExchangePlan`全量预审、伤员和所有权校验、原子性/失败回滚策略。
- `AutoSwapEnabled`在单元+集成测试完成后再开启；首次启用限定于**Party自然到达本Clan城防**，不得专程跑图。
- 验收：双方满编交换后不超编、不丢兵；Swap收益不足时什么都不做；财政不许可时不隐性裁兵。

#### M4. 军事增援任务骨架（**先不自动派遣**）

- 新建`ReinforcementMissionService/Behavior`、任务状态机、`CargoManifest`、Army资格保护、存档类型/恢复、取消处理。
- 建`IExternalReinforcementNeedModel`和`IReinforcementRouteModel`并以Stub默认不派兵。
- 用Fake Need/Route模拟：Party本身富余≥20、<20改取驻军、混合士兵粮食、缺少硬容量、途中战损、目标失效。
- 分别核验Army**被召集**和**自行创建**阻断；两者没验证前不启用活跃任务。
- 验收：真实Party任务完成不被销毁；任务取消有明确货物/兵员去向；读档任务继续或安全取消。

#### M5. 驻军外援需求与路线策略（后续独立决策）

- 用正式的`GarrisonDefenseRequirementModel`和`ExternalReinforcementNeedModel`替换固定`NoRequest`。
- 评估防御战力、本地7天补员、遭劫掠/生产中断、军团逼近、模拟/实际粮食库存、运输成本、来源地安全。
- 只有通过Army保护和路线安全时开启`EnableAutomaticReinforcement`。
- **本阶段才决定**“无敌情但生产中断，最低多少战力需要外援”，并加入可配置/可测试阈值。M0～M4不擅自定值。

#### M6. 长期复员、轮换与配置平衡（可选后置）

- 战后封邑兵来源返乡、续役与轮换的具体积极程度；职业兵/雇佣兵的非强制复员。
- 未来若需要财政危机裁军，再单独实现`IDemobilizationPolicy`，不改换兵模型基本语义。
- 中长期模拟不同文化、主战/主和Clan和前线/内陆驻军构成，调整评分/比例。

---

### 16. 数据守恒和安全不变量

1. **兵数守恒**：纯转移/双向交换前后同一Clan普通兵员数相等（独立真实征兵、阵亡、合法返乡、明确裁军除外）。
2. **在役身份守恒**：同兵种职业兵与封邑兵可以并存；按来源批次分配，不能只按`CharacterObject`一刀切。
3. **粮食/物品守恒**：库存转移不得产生负数或凭空生成；途中真实消耗要计入账本，装运口粮与自用口粮分开。
4. **编制守恒**：Party与驻军不得突破各自硬上限；驻军有效成员包含未归队的征粮队预留。
5. **权限守恒**：只从合法Clan驻军和授权粮源调兵调粮；所属Clan/阵营变化后计划失效。
6. **财政保护**：招募/转移/交换后目标单位工资可持续；转换兵源不可绕过已有Clan预算约束。
7. **Army保护双向完整**：有效增援任务Party既不被召集，也不自行创建Army；任务结束后可正常重新参加。
8. **不影响战斗自保**：增援任务的导航锁定不能阻止原版逃敌与即时交战。
9. **防震荡**：同日不进行互为逆操作的招募/捐兵；重复请求考虑在途数量；已完成任务释放资源标记。
10. **无隐式裁撤**：被拒绝转移/计划失败不能`Clear()`普通Party士兵。
11. **可观测**：决策与未执行原因写文件日志，记录Party/城防ID、场景、兵源、预计与实际兵粮、来源地预留、拒绝原因；避免高频UI弹窗。

---

### 17. 测试矩阵与验收条件

| 测试 | 输入/场景 | 期待结果 |
|---|---|---|
| 和平保底 | Party上限100，实际42，D任意但和平 | 目标50，进入补员评估；不被旧85%触发线阻挡 |
| 和平上限 | Party上限150，实际70，和平 | 目标53，停止普通补员；不自动删17名职业兵 |
| 非主战边界 | D=20，正式战争，普通独立Party | 目标比例50%，**不得**按普通战争扩军到60%以上 |
| 主战边界 | D=21，战争，普通独立Party | 有资格主动提高到约60.5%的编制目标 |
| 主战上界 | D=100，战争，普通独立Party | 目标95%，实际不超Party硬编制 |
| 特殊覆盖 | D=-80，Clan自家城堡需紧急救援 | 合格Party目标100%，但不得因招兵延误救援 |
| Army集结 | D=+100，已入Army正在集结 | 目标`Max(90%,95%)=95%`，受不延误集结约束 |
| 驻军自动新增 | 上限300，实际240，无征粮队 | 自动补员0；不可由于软上限把已有士兵裁至240 |
| 回营可过80% | 上限300，实际240，Party移交40 | 可按硬编制移交至280（仍受财政等限制） |
| 征粮预留 | 上限300，驻军220、外出50 | 有效270，最多接收30个已有士兵；自动补员0 |
| 同兵种不同兵源 | Party中同一种`CharacterObject`同时含职业与封邑在役批次 | 仅职业部分可普通移交，封邑按原批次返乡 |
| 自有富余≥20 | Party110，目标53，可安全移交57 | 优先标记57名现有兵作为货物，不额外`Add`一次；缺额再考虑驻军 |
| 自有富余<20 | Party65，目标53 | 不动用12名自身富余兵员，优先从驻军装载 |
| 运输硬编制 | Party120/150，自己可交67，目标缺100 | 不将33驻军兵一次装入30个空位导致超编 |
| Army征召冲突 | 执行中的运输Party，人数>60% | 不在Army召集候选列表，也不能自行建Army |
| 取消任务 | 目标城堡失守，Party已装载驻军兵与粮 | 取消/改派有明确真实物资去向，完成/取消后Army资格恢复 |
| 外援未实现 | 无敌军威胁、村庄被洗劫，尚使用Stub | `NoRequest`，日志为`ThresholdPolicyNotImplemented`，不凭空派遣 |
| 同Clan多Party | 两支Party同时前往同一驻军取兵/送兵 | 每次提交前重算余额，不重复承诺同一份兵源/空位 |
| 读档 | 运输任务半程、正在执行交换计划 | 状态恢复或安全取消；无Army保护失效或重复货物 |
| 缺政治Mod | 仅启用ModifiedArmy | 使用中立降级D=0，不报启动异常，不误主动主战扩军 |

---

### 18. 固定提交的仓库源码参考

- [AI日常招募行为 `AiRecruitmentBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/AiRecruitmentBehavior.cs)
- [定居点AI招募补丁 `AiSettlementRecruitmentPatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Recruitment/Patches/AiSettlementRecruitmentPatch.cs)
- [统一招募模型 `DefaultAIRecruitmentModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [驻军容量 `GarrisonRecruitmentCapacity.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs)
- [驻军转移政策 `GarrisonTransferPolicyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonTransferPolicyModel.cs)
- [本体驻军交换补丁 `GarrisonTroopDonationPatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Garrison/Patches/GarrisonTroopDonationPatch.cs)
- [整Party解散补丁 `DisbandPartyGarrisonMergePatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs)
- [驻军自动补员 `GarrisonRecruitFromPrisonersBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs)
- [村庄职业/封邑产出 `SettlementManpowerProductionBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [封邑服役管理 `FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [封邑兵征召 `FiefPartyRecruitment.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [封邑兵返乡 `FiefPartyReturn.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [现有征粮队 `SupplyPartyBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Garrison/Supply/SupplyPartyBehavior.cs)
- [驻军粮食状态 `GarrisonLogisticsModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonLogisticsModel.cs)
- [军事配置 `militaryConfig.xml`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
- [军事配置解析器 `MilitaryConfig.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [战争倾向管理 `WarDispositionManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Persistence/WarDispositionManager.cs)
- [战争倾向分级 `WarDispositionCalculator.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/ModifiedPolitics/ModifiedPolitics/WarDisposition/Models/Calculation/WarDispositionCalculator.cs)
- [原版Army候选 `DefaultArmyManagementCalculationModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/code/DefaultArmyManagementCalculationModel.cs)
- [原版军事决策 `AiMilitaryBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/e669c477a4346254eadffe7b312638cc1443996e/code/AiMilitaryBehavior.cs)

---

### 19. 实施边界与最终验收定义

**先让所有兵力管理行为通过一组稳定Model/API进出，再逐项提升策略智能程度。** 已确定的和平50人、主战阈值、驻军80%新增软上限、自有富余至少20人的增援货源规则现在即可落地；精细兵种价值、守军缺额触发、敌情安全路线等仍不确定的项目必须封装为可替换策略，在默认保守返回值下不影响现有玩法。这样今后改变“边境城堡何时请求外援”，只需替换`IExternalReinforcementNeedModel`，不需要重新改AI招募Patch、Party兵粮转移事务或Army任务保护逻辑。
