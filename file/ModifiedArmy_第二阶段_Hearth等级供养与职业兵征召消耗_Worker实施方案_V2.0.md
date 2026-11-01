# ModifiedArmy — 第二阶段：封建兵等级 Hearth 供养与职业兵征召 Hearth 消耗

> **AI Worker 实施方案 V2.0**  
> 基线仓库：`GuihuaZhou/bannerlord`  
> 核对提交：`7bd3db3eb8196f024985338923b1ab880bc8b857`  
> 范围：仅第二阶段 Hearth 经济规则、XML 配置、生产与升级约束，以及必要的展示/调用链同步。  
> 状态：**设计与实现指令；尚未修改仓库、未编译、未进行游戏测试。**

## 0. 最终决定（不可自行变更）

1. 所有兵种等级由游戏对象的 **`CharacterObject.Tier`** 决定；XML 使用逐项 `<Tier Level="…" Hearth="…" />`，**不得**使用 `T1`、`T2` 之类固定属性，C# 也不应硬编码“六个等级”分支或经济数值。
2. **封建兵供养**：等级 1→6 的每人 Hearth 供养成本依次为 **2、5、10、15、20、40**。它是**持续占用的容量**，不因计算供养而修改实际村庄 Hearth。
3. **新职业兵征召**：等级 1→6 的每人 Hearth 实扣成本依次为 **2、4、6、8、10、12**。是**永久消耗**；士兵后续升级、战死、退役、解散，不因这次征召而退款。
4. 两套成本均由现有 `militaryConfig.xml` **全局 XML** 配置，文化制度、职业兵训练和封建兵每日产量仍使用已有文化参数；不为每个文化复制同一组成本。
5. 封建兵**就绪 / 在役 / 返乡冷却**全周期持续占用来源村庄供养；战损经已有对账确认后减少占用，伤兵不视为死亡。现有 `SourceVillage` 和批次不可丢失。
6. 封建兵出征临时扣除 **每人 1 Hearth**，返乡恢复、战损不恢复——这是**已实现的独立动员机制**。新的供养需求 **不代替、不重复扣除** 这一 Hearth。
7. 村庄实际 Hearth 降低或兵种升级造成供养不足时，现存士兵**不自动裁撤**；停止产生额外占用的封建兵和非必要的正向等级升级。职业兵扣户不得继续挤占已有封建兵供养。
8. 村庄实际 Hearth 不得因职业兵征召降到现有 **100 Hearth 保底** 以下。供养公式不额外扣掉100：保底是“**允许永久扣户的下限**”，不是“供养总额需再减100”。
9. 不修改已经完成的封邑/村庄双入口征召与解散、在役对账、返乡流程、税赋制度和兵种身份转换。**不处理旧存档迁移、村庄跨封邑重新绑定。** 新游戏的正常存取档必须通过测试。

## 1. XML 结构（直接使用这份定义）

在 `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` 的单个 `<MilitaryConfig id="default">` 下，添加**全局**同级节点 `<HearthEconomy>`，例如置于 `<AiRecruitment>` 之后、`<Cultures>` 之前：

```xml
<HearthEconomy>
  <!-- 封建兵供养：只占容量，不实际扣户 -->
  <FeudalSupply>
    <Tier Level="1" Hearth="2" />
    <Tier Level="2" Hearth="5" />
    <Tier Level="3" Hearth="10" />
    <Tier Level="4" Hearth="15" />
    <Tier Level="5" Hearth="20" />
    <Tier Level="6" Hearth="40" />
  </FeudalSupply>

  <!-- 新职业兵征召：实际永久扣除村庄 Hearth -->
  <ProfessionalRecruitment>
    <Tier Level="1" Hearth="2" />
    <Tier Level="2" Hearth="4" />
    <Tier Level="3" Hearth="6" />
    <Tier Level="4" Hearth="8" />
    <Tier Level="5" Hearth="10" />
    <Tier Level="6" Hearth="12" />
  </ProfessionalRecruitment>
</HearthEconomy>
```

**解析契约：**

- 节点名及属性名大小写按上述 XML 严格使用：`Tier`、`Level`、`Hearth`。
- `Level` 为整数，映射至 `troop.Tier`；`Hearth` 为正整数；允许未来添加其他等级，无需新增代码分支。
- 每一组不得有重复 `Level`；禁止空表、非整数、负数、零成本。加载时快速失败，错误明确指出组名、Level 和非法值。
- **无默认回退**：实际兵种的 `Tier` 若未在对应映射中配置，拒绝该笔生产/招募并记录明确错误，不能静默按最低等级、最高等级或“每人2 Hearth”处理。
- XML 是**静态军事配置**，运行时计算从加载后的只读字典查询，不需要新存档字段，不引入 MCN 之外的第二份同名设置。
- 原 XML `Fief/Production` 现有 `hearthCostPerTroop` 为旧的“每兵”字段，须核对实际调用并**移除或停用其在当前两套 Hearth 经济上的作用**，不得让同一封建兵再产生第三次扣户。`prosperityCostPerTroop` 等未在本阶段明确替代的繁荣度规则保持原行为；清理实际未使用字段要同时清理注释与解析器。

建议 C# 只读配置类型（示意；名称可遵循项目风格）：

```csharp
public sealed class HearthEconomyConfig
{
    public IReadOnlyDictionary<int, int> FeudalSupply { get; internal set; }
    public IReadOnlyDictionary<int, int> ProfessionalRecruitment { get; internal set; }

    public int GetFeudalSupplyCost(CharacterObject troop) =>
        GetRequiredCost(FeudalSupply, troop, "FeudalSupply");

    public int GetProfessionalRecruitmentCost(CharacterObject troop) =>
        GetRequiredCost(ProfessionalRecruitment, troop, "ProfessionalRecruitment");
}
```

在 `MilitaryConfig.Deserialize()` 中调用 `ParseHearthEconomy(Required(node, "HearthEconomy"))` 并存入 `MilitaryConfig.HearthEconomy`；延用现有 `NonNegativeInt` / `Attr` 的错误风格，但本节点约束为正数。

## 2. 领域模型与精确公式

### 2.1 来源村庄的有效 Hearth

对于村庄 `v`：

```text
EffectiveHearth(v) = floor(v.Village.Hearth + v.MobilizedHearth)
```

`MobilizedHearth` 是封建兵**已出征的、临时扣掉的村庄 Hearth**；必须加回用于容量计算，否则出征会使封建兵双重占用。

### 2.2 封建兵供养占用

```text
Occupied(v) = Σ_ready [troopCount × FeudalCost(troop.Tier)]
            + Σ_serving [batch.Troops[troop] × FeudalCost(troop.Tier)]
            + Σ_cooling [returnBatch.Troops[troop] × FeudalCost(troop.Tier)]

FreeSupply(v) = EffectiveHearth(v) − Occupied(v)
SupplyDeficit(v) = max(0, −FreeSupply(v))
```

注意：

- **就绪**采用本村原生民兵 `MemberRoster` 中**符合该封邑 `FiefPartyTemplate` 的封建兵**，用 `element.Number`（含伤员），不以 `TotalHealthy` 代替。
- **在役**严格来自本村 `VillageFiefData.ServiceBatches` 的 `Troops`，此处应采用批次**当前对账后的兵种等级**，不能用征召时原始等级。
- **冷却**严格来自本村 `VillageFiefData.ReturnBatches` 的 `Troops`。
- 就绪、在役和冷却每个真实士兵只计一次；**不要**再用 `FiefPartyData` 的封邑汇总Roster计一遍。
- `RecoveryRequired` 时不得默认为占用0，也不得忽略在役和冷却；可读统计尽量保留真实记录，但应冻结增加占用的写操作。
- 同一兵种在多个村庄按**各自数据持有者**计入，各村的 `FreeSupply` 不可混成城镇级共享池。

### 2.3 新兵生产与升级

```text
生产 troop 一人：FreeSupply(v) >= FeudalCost(troop)
升级 from→to：delta = FeudalCost(to) − FeudalCost(from)
允许增加占用的升级：delta > 0 时 FreeSupply(v) >= delta
```

升级 `delta <= 0` 不增加供养，允许维持原规则（但仍须符合模板/训练/资格检查）。若现状已经透支，`delta > 0` 的升级必须停止，而不是把超额视为可继续透支的余额。

**只有实际兵员增加或等级变化才改变 Occupied；征召至Party、返乡、解散转冷却只改变状态，不改变 Occupied。**

### 2.4 职业兵永久扣户

对一笔新职业兵 `troop × count`：

```text
ProfessionalCost = ProfessionalRecruitment[troop.Tier] × count
```

必须按实际兵种而不是仅按人数计算；`checked` 整数乘法并处理溢出。对于城镇/城堡，用现有“附属村庄分摊”行为分配**实际消耗的 Hearth 数**，但分配前必须验证每村余额：

```text
Spendable(v) = max(0,
    min(floor(v.Hearth - 100), floor(FreeSupply(v))))
```

`Spendable` 为当前这笔**永久消耗**的上限。制定完整分配计划后原子提交；分摊时跳过无余额村庄，可按现有 StringId 排序和循环逐户分配，但不得因某村余额不足出现扣到低于100或侵占已占用供养。

该表达式确保职业兵征召后 `EffectiveHearth(v) >= Occupied(v)`，同时 `v.Hearth >= 100`。如果村庄已经供养超额，则**不得新增永久扣户**，但现有封建兵照常保留。

村庄直接职业兵征召时，只检查并扣本村；城镇/城堡职业兵征召只能使用**实际附属村庄**的可用余额。

**示例：**600 Hearth、20名等级5封建兵（20×20=400），剩余供养200；招募10名等级5职业兵（10×10=100），实际Hearth 600→500，供养占用仍为400，剩余供养100。

## 3. 修改文件与方法（按最新仓库实查）

### A. `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`

**现状：** `MilitaryConfig.Deserialize()` 调用 `ParseAi()` 并解析 `<Cultures>`，每文化 `CulturalMilitaryDoctrine` 的 `<Fief><Production …/>` 保存 `BaseLimit`、`VillageBonus`、`DailyRate`、`ProsperityCostPerTroop`、`HearthCostPerTroop`。

**修改：**新增全局 `HearthEconomyConfig`、解析与严格验证；原先文化生产训练参数继续加载；清理无用旧成本字段引用。公共 `GetRequiredCost(CharacterObject)` 不接受等级字符串，直接用 `troop.Tier`。

### B. `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`

**现状：**村庄存 `VillageSettlement`、`ServiceBatches`、`ReturnBatches`、`MobilizedHearth` 和恢复状态，`GetReadyRoster()` 返回原生村庄民兵Party名册。

**修改：**新增**无状态、只读**查询方法：

```csharp
int GetOccupiedSupplyHearth(FiefPartyTemplate template, HearthEconomyConfig economy);
int GetEffectiveSupplyHearth();
int GetFreeSupplyHearth(FiefPartyTemplate template, HearthEconomyConfig economy);
bool CanIncreaseSupply(FiefPartyTemplate template, HearthEconomyConfig economy, CharacterObject troop, int count);
```

实现只调用原有对象与批次；不新增 `[SaveableProperty]` 存放派生供养额度，不另立供养兵员库存。计算时跳过未被模板认可的**普通原生民兵**，但**在役和冷却历史批次无条件保留计费**。对异常的负数、缺失兵种成本要显式拒绝相关写操作、输出可诊断原因，不要按0处理。

### C. `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`

**现状：**`ProduceDailyFiefTroops()` 与 `PerformReinforcement()` 被 `_totalLimit - _totalTroopCount` 限制，生成之后按 `OrderedVillages` **轮流**加入村庄；`PerformAutoUpgrade()` 每周逐兵尝试升级，尚未检查供养差额。

**修改：**

1. `ProduceDailyFiefTroops` 保留已有 `approvedCount` / `DailyRate` 和共享生产授权约束，**移除旧静态人数上限作为生产硬闸**。
2. `PerformReinforcement` 在产生**每个具体 CharacterObject** 时，寻找按本村当前 `FreeSupply` 可负担的村庄；成功才加入其Roster并计数。排序确定性，优先可用余额较高的村庄或使用稳定轮询，但**只在可支付者中选取**。
3. 一次批次生产过程中，必须使用前一名士兵增加后的最新占用，防止当日超额；无可承载村庄时停止该具体兵种，不得生成后依赖事后校正删除。
4. 现有 `WithdrawFiefTroopEntries()` 和配置模板的权重选择不得被随意重写；若先生成候选再落地，未落地者必须计为**实际未生产**，并检查每日兵源进度/`RecordFiefReplenishment` 是否会因失败被错误消耗或二次生成。
5. `PerformAutoUpgrade` 在每次升级前计算 `delta`；若正差额超出本村 `FreeSupply`，则跳过该兵，不影响其他村；按真实升级成功数量结算供养，仍遵循原有等级、模板及概率规则。
6. `WeeklyUpdate` 冷却回到就绪Roster属于同一村庄内部的**状态转移**，不能产生第二笔供养额度，也不能因“当前空余为负”导致冷却士兵无法回到就绪。

### D. `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`

**现状：**`CalculateSizeLimit()` 返回 `BaseLimit + BoundVillages.Count × VillageBonus`，`GetFiefTroopLimit()` 和 `_totalLimit` 是“最大人数”，并被生产预测与实际生产使用。

**修改：**取消该固定人数上限对封建兵生产/升级的最终控制作用。注意 **不存在一个与兵种构成无关的准确“最大人数”**：同样600 Hearth可供养的等级1、等级5士兵人数不同。因此：

- `GetTotalTroopCount()` 仍表示真实人数，用于统计和其他与人数有关的行为。
- 新增按村庄汇总的 `GetTotalOccupiedSupplyHearth()`、`GetTotalFreeSupplyHearth()` 等明确**Hearth单位**的方法。
- 原 `GetFiefTroopLimit()` 若仍需要保留以免破坏其他调用，不可继续作为权威生产硬闸，也不可拿“Hearth数”直接冒充“兵员人数”。为 UI 标记理论上限/参考值，或者逐个调用处转向正确的供养或实际数量指标。
- XML `baseLimit` 与 `villageBonus` 已失去当前“人数上限”功能；在确认全部引用后删除相关 XML、字段和误导性说明；保留现有 `dailyRate` 作为真实补员速度。

### E. `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

**现状：**

- `CalculateSettlementSupply()` 用“封邑总人数均分至各村”算 `assignedFiefCount`；`CalculateVillageSupply(village, assignedFiefCount)` 减的是人数，不是等级供养。
- `GetProductionForecast()` 以 `capacity - current` 限制 `fiefDemand`。
- `TryConsumeProfessionalOffer()` 已知道具体 `CharacterObject troop`，但调用 `manager.TryConsumeProfessionalHearth(settlement, count, ...)`，仅传人数。

**修改：**

1. `CalculateVillageSupply` 改用 `FreeSupplyHearth(v)`（来自真实村庄等级供养），保留其现有**供给曲线系数及100门槛**，不借机重调生产率。
2. `CalculateSettlementSupply` 直接汇总附属村庄供给，不再将封邑人数均分作为虚拟占用。
3. 预测中的 `fiefDemand` 以文化 `DailyRate` 和实际可新增封建兵的供养可能性为准，不再受静态 `capacity-current` 限制；**不要**因为某个村没容量而将所有村的需求清零。
4. 职业兵Offer结算将完整兵种传入，调用 `TryConsumeProfessionalHearth(settlement, troop, count, out ...)`，保留原有“Offer→实际扣户→驻军Roster→失败时回滚Offer及Hearth”事务。
5. `GetPredictedProfessionalOffers` 等预览不得扣 Hearth；最终提交重新检查实际余量并原子分摊。
6. 每日专业训练名额和文化分流比例保持原样。若显示预测名额，应说明这是训练候选而非保证可支付 Hearth 的最终加入人数。

### F. `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`

**现状：** `TryConsumeProfessionalHearth(Settlement source, int count, …)` 使用 `checked(count * 2)`，对关联村庄循环扣1 Hearth；`OnTroopRecruitedEvent` 的 `OnTroopRecruited` 同样使用固定2成本，且发生于原版招募后。

**修改：**

- 替换为接收 `CharacterObject troop` 的 tier-aware 接口，或接收每兵种数量的精确清单；主事务须预先计算**本次所需的全部Hearth**、验证每村消耗上限并记录 `ConsumedHearth` 字典，提交成功后返回。不得部分消耗后悄悄返回false。
- 原 `RollbackProfessionalHearth` 延用真实每村消费映射回滚；不能按 `count × 2` 猜测返还。
- 查清 `OnTroopRecruited` 与专业Offer、原版招募事件是否为**不同**实际招募来源：
  - 对仍明确属于“新职业兵征召”的事件路径，改为使用该兵种的等级成本；
  - 对封建兵从村庄民兵Roster转到野战Party、返乡、驻军转移、囚犯转换等**非新职业兵征召**，禁止再次永久扣户；
  - 如果原版事件与专业Offer结算在某路径上重叠，必须依操作来源解决重复计费，而不是凭名称猜测事件含义。
- 当前事件是**招募完成之后**才触发；原有用 `roster.RemoveTroop(troop, rejected)` 进行简单撤销的代码可能无法精准撤销这次招募（同兵种原有兵员会混在一起）。优先在可控招募入口前置支付检查；若必须保留事件路径，要求有可靠的前后快照/可验证的撤销方法，否则不能把“移走部分同兵种兵员”当作成功回滚。
- 若有其他代码调用旧的 `TryConsumeProfessionalHearth(Settlement,int)`，逐个改造到携带 troop/Tier 的API，不保留一个会偷偷回退至固定2成本的入口。

### G. `ModifiedArmy/ModifiedArmy/Recruitment/Services/ProfessionalRecruitmentService.cs`

这是实际Offer→驻军的权威提交路径。原 `ProfessionalRecruitmentCommit.Consumption` 已保存每村消耗映射。保持失败回滚以及成功记账的单一职责，只更换准确的Tier成本来源。验收必须检查**一次成功只扣一次、一次失败全额返还**。

### H. UI/日志/只读接口

`FiefMenuBehavior` 与兵源视图应能查看：

- 当前村庄：实际Hearth、动员Hearth、有效Hearth、就绪/在役/冷却供养占用、剩余供养、超额供养。
- 城镇/城堡：按附属村庄汇总，并允许查看各村明细；不得展示“所有兵种共用一个准确人数上限”。
- 职业兵：当前选中兵种Tier对应的每人实扣与本次总实扣，最终成功/失败要与实际消耗一致。
- 运行时统计必须使用与提交相同的共享计算函数，避免UI显示可用但后台认为不可用。

UI 可以沿用现有菜单和日志，优先实现正确的只读数据方法，不要为了本阶段新增大型窗口系统。

## 4. 源码与潜在重复扣户审计清单

Worker 必须在提交前全局搜索：`TryConsumeProfessionalHearth`、`OnTroopRecruited`、`TryConsumeProfessionalOffer`、`RollbackProfessionalHearth`、`UpdateHearth`、`HearthCostPerTroop`、`baseLimit`、`villageBonus`、`_totalLimit`、`GetFiefTroopLimit`、`CalculateVillageSupply`、`CalculateSettlementSupply`、`ProduceDailyFiefTroops`、`PerformAutoUpgrade`。

逐一标注它们属于：**职业兵真实新增、职业兵调拨、封建兵状态转移、封建兵生产、封建兵升级、非当前阶段其他Hearth消耗**中的哪种，再决定是否更换Tier成本/供养检测。特别留意 `FiefPartyEconomy.UpdateHearth` 的历史代码，绝不能和新供养计算形成重复扣减。

## 5. 应遵守的数据不变量

1. `FiefSupplyHearth`（供养）只是查询结果，不直接做 `Village.Hearth -= Occupied`。
2. `EffectiveHearth = actual Hearth + MobilizedHearth`；出征扣1与临时标记 +1，供养有效值不变；返乡 +1/-1，供养有效值不变。
3. 返乡冷却与就绪转换不改变 `Occupied`；同种同Tier不复制。
4. 战损释放动员标记、不会恢复实际 Hearth；死亡人员同时从批次中消失，`Occupied` 按其最后Tier减少。
5. 职业兵成功新增使实际 Hearth 精确减少 `count × configuredTierCost`，不产生 `MobilizedHearth`。
6. 任一村职业兵扣费后 `ActualHearth >= 100` 且 `EffectiveHearth >= Occupied`；若预先存在供养赤字，禁止再扣该村。
7. 等级升级只在供养增加额度足够的村发生，降级或无需新增供养的变动不受该限制。
8. 已在籍士兵受Hearth下降影响时只出现正 `SupplyDeficit`，不得因此自动销毁或征回野战兵。
9. `RecoveryRequired` 的村庄不得因新的生产、自动升级或永久扣户被继续改写；正常对账、返乡仍按已有安全规则。
10. 任何因成本不足导致失败的实际提交不得部分扣Hearth、部分加入Roster或部分生成批次。

## 6. 重点边界与数值例子

### 例A：600 Hearth村庄仅有等级5封建兵

600 / 20 = **30人**。若30人全出征，村庄实际 Hearth = 570，`MobilizedHearth = 30`，有效Hearth仍为600，`Occupied = 600`；不能再额外生产第31人。

### 例B：等级4升等级5

原占用15，目标占用20，**仅增加5供养**。余量4则拒绝本次升级，余量5则允许。不得再将20作为新增消耗，更不得将实际村庄 Hearth 扣20。

### 例C：职业兵挤占封建兵供养

实际Hearth600、`MobilizedHearth=0`，20名等级5封建兵占用400，职业兵等级5招募10人实扣100，村庄Hearth变500、封建兵占用仍400、剩余供养100。若企图再征召11名同级职业兵（需110），则整笔拒绝或在正式审批阶段缩小为**明确获准的可支付数量**，不能先扣部分110后报告成功11人。

### 例D：多村城堡与余量不足

A村实际Hearth450、封建兵供养占用440，B村实际Hearth600、占用200；两村都保留100实际Hearth保底。职业兵等级6、5人需60 Hearth。A村最多承担10，B村最多承担400；按现有轮询/稳定顺序完成总计60的分摊，**不能**从A村扣11；若总体可支付量低于60则本次失败且零扣费。

### 例E：Hearth自然衰减导致供养赤字

有效Hearth从600下降到550，但原有30名等级5封建兵仍占用600。记录赤字50，原有30人继续存在；新兵生产停止，所有增加占用的升级暂停，不删除已在籍人员。

## 7. 验收矩阵（至少执行）

| # | 场景 | 必须结果 |
|---|---|---|
| 1 | XML正常加载 | 两套映射读取无误；所有文化共用 |
| 2 | XML重复Level | 加载报错，明确冲突等级 |
| 3 | XML无效Hearth | 加载报错，无静默零成本 |
| 4 | 兵种Tier未配置 | 拒绝该写操作，日志说明兵种和Tier |
| 5 | 村庄600H仅等级5封建兵 | 最多增加到30人 |
| 6 | 同村有等级1/3/5混编 | 占用=2×人数+10×人数+20×人数 |
| 7 | 封建兵全部征召到野战 | 供养占用不变；有效Hearth不变 |
| 8 | 多村同Party各批次 | 分村计供养，不跨村借额度 |
| 9 | 同村返乡→冷却→就绪 | 三态切换不重复计费 |
| 10 | 同村确认战损 | 对账数量和供养下降，Hearth不恢复 |
| 11 | 等级4→5 升级 | 仅检查供养增量5 |
| 12 | 等级5→6 升级 | 仅检查供养增量20，余量不足跳过 |
| 13 | 原有供养透支 | 保留原兵，禁止生产/提高等级占用 |
| 14 | 城镇/城堡生成多个封建兵 | 逐人选可支付村庄，不能事后超额 |
| 15 | 村庄实际Hearth降至100 | 拒绝继续永久扣户 |
| 16 | 职业兵等级1、3、6各成功一人 | 真实消耗分别2、6、12 |
| 17 | 职业兵同Tier批量征召 | 总消耗=人数×配置值，且仅一次 |
| 18 | 职业兵城堡附属两村分摊 | 不越100保底，不挤占已供养容量 |
| 19 | 职业兵Offer预览 | 不改变任何Hearth或Offer状态 |
| 20 | 职业兵Offer提交失败 | Hearth/Offer/驻军Roster全部恢复 |
| 21 | 触发原版OnTroopRecruited路径 | 分类明确、按Tier扣费、不与Offer重复 |
| 22 | 老的 `hearthCostPerTroop` 路径 | 无重复扣Hearth |
| 23 | 查看村庄及封邑统计 | 与实际兵员、成本公式一致 |
| 24 | AI及玩家 | 使用同一两组XML等级成本 |
| 25 | 新游戏保存/加载 | 在役/冷却继续计算供养且无重复扣费 |
| 26 | 生产共享进度 | 仍符合文化 `DailyRate`，无重复造兵/掉进度 |

其中**6、7、10、12、17、18、20、21、25、26**必须至少有一次可审阅的游戏日志或自动化测试结果。

## 8. 建议的实现顺序

1. 增加 XML 节点、全局配置类、严格解析及单元测试。
2. 在 `VillageFiefData` 增加只读有效/占用/剩余供养计算。
3. 将共享村庄供给从“人数均分”改为“村庄真实供养”，保持原产能曲线。
4. 调整封建兵每日生产、自动升级，移除旧人数容量硬闸。
5. 修改职业兵Offer的 Tier 真实扣户及回滚，处理城镇/城堡多个村庄的可支付分摊。
6. 梳理 `OnTroopRecruited` 的事件路径和重复扣费问题。
7. 更新村庄/封邑视图及日志，再执行全场景测试。
8. 最后清除已确认失效的旧配置字段/注释；不开展与本阶段无关的重构。

## 9. Worker 交付与验收要求

- 提供逐文件修改摘要、实际新增 XML、用例结果及尚未验证事项。
- 提供修改前后代表性数值日志：村庄600H/等级5、混合等级、等级升级、职业兵多村扣户、征召失败回滚。
- 编译项目并报告编译结果；若当前环境缺少游戏依赖，明确报告受阻，不得伪称通过。
- **不得**顺带实现封建税赋减免、职业兵/封建兵身份互转、旧档迁移、村庄改绑迁移或无关数值调整。
- 与本实施方案相冲突时，以第0节“最终决定”为准；如发现现有招募路径特殊，保留并解释实际路径，不凭空扩大扣户范围。

---

**最终原则：封建兵按自身等级持续占用来源村庄的 Hearth 供养；新职业兵按自身等级一次性永久消耗村庄 Hearth。全部成本来自 `militaryConfig.xml` 中的 `<Tier Level="…" Hearth="…" />`，而不是固定T1/T2属性或C#常量。**
