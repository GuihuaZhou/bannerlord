# ModifiedArmy — FiefParty 村庄化重构 Worker 实施方案 V1.0

**目标仓库：** `GuihuaZhou/bannerlord`  
**源码基准：** `4610e3a1b98ee3625feb32bba39db7e4f81e0c5d`  
**目标模块：** `ModifiedArmy`  
**实施状态：** 待 Worker 实施，本文不代表已经修改代码  
**存档要求：** 用户当前没有需要兼容的旧存档，允许重构本功能的持久化结构；新游戏的保存与加载必须正常。

---

## 0. Worker 执行要求

### 0.1 必须先阅读

- 仓库根目录 `AGENTS.md`
- `ModifiedArmy/AGENTS.md`
- `ModifiedArmy/CODE_ORGANIZATION_RULES.md`
- 本文列出的实际源码

先检查当前分支、HEAD 和 `git status --short`，不要覆盖其他未提交修改。本文的提交 SHA 是设计审核基准，如果 Worker 执行时 HEAD 已变化，以最新实际源码为准，并报告差异。

### 0.2 实施范围

本次仅重构封建兵的存储地点、生命周期归属、统一调度和相关资源结算。

**必须实现：**

1. 新建 `VillageFiefData`，与单个村庄绑定。
2. 将 `FiefPartyData` 外壳化，继续绑定城镇或城堡。
3. 将封建兵真实就绪名册迁至各附属村庄的原版民兵Party。
4. 将在役、征召、返乡、冷却批次全部下沉至 `VillageFiefData`。
5. 将封建兵动员Hearth的权威余额下沉至 `VillageFiefData`。
6. 修改生产、升级、征召、战损、部队间调拨、返乡和冷却流程。
7. 保留封邑级对外接口，使现有AI、UI和调兵系统能够继续调用。
8. 适配现有 `SettlementPatch`。
9. 保证新存档保存、加载后记录完整。

**本次禁止顺带实施：**

- 新的T0—T6等级供养成本。
- T5=20 Hearth的容量模型。
- 50%封建兵税赋减免。
- 300最低税收。
- 职业兵与封建兵身份互转。
- 职业兵永久扣除2 Hearth规则变更。
- AI军事制度权重、征召评分、模板权重的重新平衡。
- 不相关的Harmony补丁重构。

第一阶段仍使用原来的 `BaseLimit + VillageBonus × VillageCount` 作为封邑总人数上限。它只是过渡约束，不是最终供养模型。

### 0.3 架构约束

- 真实兵员只保存在原版实际Roster中，不新增第二套驻村库存。
- `VillageFiefData` 是村庄封建兵生命周期的持久化权威。
- `FiefPartyData` 是封邑级聚合和调度外壳。
- `FiefPartyManager` 是Campaign入口与全局运行时索引管理者。
- Model/Data负责状态表达与查询；需要修改Roster、Hearth和批次的执行逻辑，优先放在相应业务Service或现有事务执行路径。
- 不允许同时维护村庄级和封邑级两套在役、冷却或动员账本。

---

## 1. 当前源码结构与问题

### 1.1 FiefPartyData

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`

当前：

- `[SaveableRootClass(2)] public partial class FiefPartyData`
- `_settlement`：城镇/城堡。
- `_fiefParty`：单个 `TroopRoster`。
- `ServiceBatches`：`[SaveableProperty(6)]`。
- `ReturnBatches`：`[SaveableProperty(7)]`。
- `RecruitedTroopDetachmentList`、`ReturnedTroopDetachmentList`：旧容器。
- `_totalTroopCount`：运行期人数。
- `_totalLimit`：封邑人数上限。

`InitialFeifPartyData()` 第355—365行目前把城镇/城堡民兵部队的 `MemberRoster` 绑定到 `_fiefParty`。

`SyncManpowerCounters()` 第202—210行计算：

`单一就绪Roster + ServiceBatches + ReturnBatches`

`CalculateSizeLimit()` 第219—230行仍然使用旧人数上限。

**改造要求：**

- 删除单一封邑Roster的权威地位。
- 移除封邑级的在役、冷却状态。
- 将人数统计改为所有村庄之和。
- 保留城镇/城堡关联、模板、封邑配置和旧公共接口。
- 初始化时创建、刷新与附属村庄对应的 `VillageFiefData`。

### 1.2 FiefPartyManager

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`

当前：

- `_fiefDataMap`：`Dictionary<Settlement,FiefPartyData>`。
- `_mobilizedHearth`：按村庄Settlement记录动员Hearth。
- `_mobilizationOwners`：动员村庄至来源封邑的映射。
- `_legacyUntrackedMobilization`：旧记录兼容。
- `_servingDetachmentIndex`：野战Party至服役批次的运行时索引。

`InitializeFiefData()` 第151—169行仅为城镇和城堡创建数据。

`RebuildServingDetachmentIndex()` 第380—383行从封邑级批次重建索引。

`TransferFiefService()` 第516—552行直接修改 `FiefPartyData.ServiceBatches`。

`SettleRemainingFiefServiceAsLoss()` 和 `OnPartyRemoved()` 也直接访问封邑级批次。

**改造要求：**

- 保留 `_fiefDataMap` 作为城镇/城堡到外壳的映射。
- 新增村庄数据查找入口。
- 运行时在役索引从各 `VillageFiefData` 构建。
- 删除封邑级动员Hearth的权威存储，避免与村庄对象重复。
- 更新所有直接写 `FiefPartyData.ServiceBatches` 的路径。

### 1.3 生产与升级

文件：`Fief/Models/PartyData/FiefPartyReinforcement.cs`

当前关键函数：

- `WithdrawFiefTroopEntries()`：按XML权重选择具体兵种。
- `GetFiefTroopRoster()`：从单个 `_fiefParty` 过滤封建兵。
- `PerformReinforcement()`：向 `_fiefParty.AddToCounts()`。
- `ProduceDailyFiefTroops()`：对外生产入口。
- `PerformAutoUpgrade()`：直接修改 `_fiefParty`。
- `WeeklyUpdate()`：自动升级、冷却推进和回归。

**改造要求：**

- 原XML兵种选择保留在封邑级。
- 生产总额度仍由封邑统一审批。
- 新兵真正写入具体村庄的民兵Roster。
- 升级对每座村庄单独执行。
- 冷却推进和冷却结束后的入Roster行为迁移至村庄。

### 1.4 征召

文件：`Fief/Models/PartyData/FiefPartyRecruitment.cs`

当前：

- `RecruitTroopsToParty()` 从单一 `_fiefParty` 取兵。
- `BuildLegacyRecruitmentCandidates()` 依赖单一Roster。
- `BuildTemplateWeightedRecruitmentCandidates()` 从单一Roster建立实际库存。
- `RecruitManualSelection()` 按单一Roster处理玩家选择。
- `RecruitFromPrisoners()` 直接向单一Roster添加兵员。
- `CreateServingDetachment()` 在封邑级创建在役批次。
- 征召时调用 `manager.TryMobilize(_settlement, approvedCount, 1)`。

**改造要求：** 候选来源必须包含具体村庄。实际执行不得再按封邑总Roster直接增减兵员。

### 1.5 返乡

文件：`Fief/Models/PartyData/FiefPartyReturn.cs`

当前：

- `ReturnTroopsToSettlement()` 读取封邑级 `ServiceBatches`。
- `TryCommitFiefReturn()` 处理实际Party移除、服役批次扣减、动员Hearth释放、冷却批次新增及异常回滚。
- `WeeklyUpdate()` 在冷却结束后将士兵加入原来的 `_fiefParty`。

**改造要求：** 保留事务语义，但数据持有者改为来源村庄。

### 1.6 原版民兵保护

文件：`Fief/Patches/SettlementPatch.cs`

当前Harmony目标：`Settlement.RemoveMilitiasFromParty`

现有Prefix按照封建兵模板，保护符合条件的封建兵不被原版民兵减少操作错误移除。

**不要重新开发一套保护机制。**

目前第26—30行使用当前Settlement查找封邑数据。迁移后，需要由当前村庄找到其所属封邑及 `VillageFiefData`。

---

## 2. 最终数据结构

### 2.1 FiefPartyData：封邑外壳

**绑定对象：一个城镇或者城堡。**

建议继续保留文件：`Fief/Models/PartyData/FiefPartyData.cs`

```csharp
[SaveableRootClass(2)]
public class FiefPartyData
{
    private Settlement _settlement;
    private FiefPartyTemplate _fiefPartyTemplate;

    // 新的持久化村庄集合
    public Dictionary<Settlement, VillageFiefData> Villages
    {
        get;
        private set;
    }

    // 继续提供旧接口
    public int GetTotalTroopCount();
    public int GetReadyFiefTroopCount();
    public int GetRecruitedFiefTroopCount();
    public int GetFiefTroopLimit();

    public TroopRoster GetFiefTroopRoster();
    public int ProduceDailyFiefTroops(int count);
    public int RecruitTroopsToParty(MobileParty party, int count);
}
```

以上为建议API，不要求机械复制签名；Worker应尽量保持已有公开签名。

**FiefPartyData负责：**

1. 解析封邑兵种模板。
2. 管理本封邑全部附属村庄对象。
3. 汇总各村庄就绪、在役、冷却人数。
4. 统一分配每日生产额度。
5. 统一规划跨村征召。
6. 保留旧公共API。
7. 汇总业务执行结果。
8. 将来供封邑税收模型读取所有村庄兵员。

**FiefPartyData不再负责：**

- 保存真实就绪Roster。
- 保存 `ServiceBatches`。
- 保存 `ReturnBatches`。
- 保存封邑级动员Hearth余额。
- 直接修改村庄的 `MemberRoster`。
- 维护一份独立的兵员身份库存。

### 2.2 VillageFiefData：村庄封建兵实体

建议新增：`ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`

```csharp
public class VillageFiefData
{
    // 原版村庄Settlement
    public Settlement VillageSettlement { get; private set; }

    // 本村全部在役批次
    public List<FiefServiceBatch> ServiceBatches
    {
        get;
        private set;
    }

    // 本村全部返乡冷却批次
    public List<FiefReturnBatch> ReturnBatches
    {
        get;
        private set;
    }

    // 本村当前出征临时扣除的人口
    public int MobilizedHearth
    {
        get;
        private set;
    }

    // 动态获取实际名册，不持久化Roster副本
    public TroopRoster GetReadyRoster();
}
```

**权威持久化状态：** 对应的村庄Settlement；本村在役批次；本村冷却批次；本村动员Hearth余额。

本村驻留士兵以原版村庄民兵Party的真实 `MemberRoster` 为准。

建议提供只读查询：

- `GetReadyFiefTroopCount()`
- `GetServingFiefTroopCount()`
- `GetCoolingFiefTroopCount()`
- `GetTotalFiefTroopCount()`
- `GetReadyFiefTroopRoster()`
- `GetMobilizedHearth()`

为方便业务实施，增加对应的村庄级执行Service，负责实际Roster写入、Hearth扣除和批次更新。不要在Model中形成另一套独立的执行状态。

### 2.3 真实Roster获取

必须通过村庄自身的：

`VillageSettlement.MilitiaPartyComponent.MobileParty.MemberRoster`

动态获取。

**禁止：**

- 再把城镇/城堡民兵Roster当作封建兵库存。
- 为村庄额外创建一支独立的封建兵MobileParty。
- 在Mod中保存另一份完整 `TroopRoster`。
- 长期缓存一个已被原版销毁的MobileParty或Roster引用。

当村庄民兵Party不存在、未激活或正在销毁时，应返回明确的不可用状态，不能自动清空本村在役或冷却记录。

需要新增兵员却没有民兵Party时，Worker必须核对原版Party创建生命周期，选择安全的原版创建入口或延期补员；禁止未经检查重复创建民兵Party。

### 2.4 兵员身份识别

现有 `FiefPartyTemplate.IsEnableTroop(CharacterObject)` 可继续作为封建兵兵种资格判断。

但需要在初始化阶段检查当前封建兵模板与原版普通民兵兵种是否重叠。如果同一个 `CharacterObject` 同时作为普通民兵和封建兵：

- 不能单靠Roster中的CharacterObject区别身份。
- 不能简单把该兵种全部认定为封建兵。
- 不能继续使用仅靠模板资格识别的保护和统计逻辑。

Worker必须先报告重叠情况。如果实际存在重叠，需要增加有权威来源的身份数量记录或采取其他能够证明正确性的方案，不得静默混用。

---

## 3. 存档结构改造

### 3.1 当前入口

`FiefPartyManager.cs` 中：

- `FiefSaveDefiner : SaveableTypeDefiner`
- Definer基值：`20251116`
- `FiefPartyData` ClassId：2。
- `FiefServiceBatch` ClassId：6。
- `FiefReturnBatch` ClassId：7。

`FiefPartyManager.SyncData()` 当前保存封邑映射和动员相关字典。

### 3.2 新保存结构

目标：

```text
FiefPartyManager._fiefDataMap
  → FiefPartyData
    → Dictionary<Settlement, VillageFiefData>
      → VillageFiefData.ServiceBatches / ReturnBatches / MobilizedHearth
```

Worker具体操作：

1. 为 `VillageFiefData` 分配未占用的Save ClassId，例如12，实际以当前Definer为准。
2. 为 `FiefPartyData.Villages` 分配未占用的SaveableProperty编号。
3. 注册 `Dictionary<Settlement,VillageFiefData>` 容器。
4. 注册 `VillageFiefData` 自身所需的保存属性。
5. 沿用 `FiefServiceBatch` 和 `FiefReturnBatch` 的已有数据结构，除非源码证明必须修改。
6. 删除FiefPartyData上已经不再使用的批次权威字段。
7. 删除Manager中已被村庄记录替代的动员权威字典及其旧存档迁移路径。
8. 新存档加载时重新建立所有运行时引用和在役索引。

无旧存档需要兼容，不代表可以忽略新存档测试。不得为本次重构随意修改其他Mod、其他业务的SaveId或存档类型。

### 3.3 批次来源身份

不建议同时在 `FiefServiceBatch` 中保存一份可变的来源村庄字段、又在 `VillageFiefData` 中记录其归属，形成双重权威。

推荐：

- `VillageFiefData` 持有批次，因此持久化归属自然明确。
- `ServingDetachmentRef` 在运行期同时提供 `SourceFief` 和 `SourceVillage`。
- `BatchId` 保持唯一，作为原有公开返乡API的批次定位依据。

---

## 4. FiefPartyData 初始化与村庄集合管理

当前 `FiefPartyManager.InitializeFiefData()` 只为城镇、城堡创建 `FiefPartyData`。这个外部行为保持不变。

新的 `FiefPartyData.InitialFeifPartyData()`：

1. 验证Settlement是城镇/城堡。
2. 按当前代码解析 `FiefPartyTemplate`。
3. 遍历 `_settlement.BoundVillages`。
4. 使用村庄 `Settlement` 作为Key，查找已有 `VillageFiefData`。
5. 缺失时创建新的村庄对象。
6. 刷新村庄运行时引用和模板关联。
7. 对所有村庄汇总人数。
8. 返回封邑初始化结果。

不要因为民兵Party暂时不存在而删除整个 `VillageFiefData`。

附属关系、所有者变化时，不能自动销毁仍有合法在役记录的村庄对象；这些批次可能还在野战Party中。

`VillageFiefData` 的生命周期和原版 `MilitiaParty` 生命周期不能等同。

---

## 5. 封建兵生产改造

### 5.1 现有入口保持不变

`SettlementManpowerProductionBehavior.cs` 第463—471行：

`fiefData.ProduceDailyFiefTroops(fiefCount)`

Worker保留外部调用方式。

### 5.2 FiefPartyData负责生产规划

继续使用现有：

- `WithdrawFiefTroopEntries()`
- `FiefPartyTemplate.TroopEntries`
- `RequiredBarracksLevel`
- XML生产权重。
- 旧封邑总人数上限。

生产计划至少包含目标村庄、具体 `CharacterObject` 和实际数量。

每日生产额度只在整个封邑范围内计算一次，不允许每座村庄重新获得一整份 `fiefCount`。

### 5.3 VillageFiefData负责接收

对实际分配至本村的兵员：

1. 验证村庄归属。
2. 验证本村民兵Party可用。
3. 验证兵种符合当前封邑模板。
4. 按批准数量写入本村 `MemberRoster`。
5. 返回实际增加的人数。

外壳汇总全部村庄实际增加量，作为原有 `ProduceDailyFiefTroops()` 返回值。

生产目标村庄选择需要采用稳定、明确的规则，不能取决于Dictionary的非确定性遍历顺序。本阶段不改变生产概率和总额度。

### 5.4 自动升级迁移

`FiefPartyReinforcement.PerformAutoUpgrade()` 当前直接使用单一 `_fiefParty`。

改为：

- 保留原有升级概率计算。
- 保留现有升级树、模板资格和T级限制。
- 每座村庄独立读取实际封建兵。
- 在该村庄真实Roster内完成旧兵种移除、新兵种加入。
- 不让普通民兵进入封建兵自动升级流程。

升级事件不得让封建兵跨村庄移动。本阶段不新增等级供养检查。

### 5.5 俘虏转封建兵

`FiefPartyRecruitment.RecruitFromPrisoners()` 目前直接写 `_fiefParty`。改为：

- 保留原有封邑级资格及容量审批。
- 审批成功后，选择合法的附属村庄接收。
- 通过村庄级执行路径写入真实Roster。
- 更新实际封建兵汇总。

本阶段不调整该功能的经济成本。

---

## 6. 封建兵征召改造

### 6.1 保留对外接口

继续保留：

- `FiefPartyManager.RecruitFiefTroopsFromSettlement()`
- `FiefPartyData.RecruitTroopsToParty()`
- `PreviewFiefRecruitmentCandidates()`
- `PreviewFiefRecruitmentApprovals()`
- `RecruitManualSelection()`

它们仍以城镇/城堡为来源参数。

### 6.2 内部增加村庄来源计划

```csharp
public sealed class VillageFiefRecruitmentEntry
{
    public Settlement SourceVillage { get; init; }
    public CharacterObject Troop { get; init; }
    public int Count { get; init; }
}
```

统一征召流程：

1. `FiefPartyData` 枚举各村庄实际就绪名册。
2. 汇总封邑可征召兵种。
3. 保留当前AI模板权重和玩家选择规则。
4. 生成带来源村庄的实际执行计划。
5. 再次检查每村名册、健康人数、Party容量和预算条件。
6. 按来源村庄锁定本次动员Hearth。
7. 从各村庄Roster扣除已批准的实际士兵。
8. 向野战Party增加对应兵员。
9. 分别在各 `VillageFiefData.ServiceBatches` 中创建在役批次。
10. 注册全局运行时在役索引。
11. 返回实际征召人数并更新相关预算记录。

### 6.3 AI权重逻辑

`BuildTemplateWeightedRecruitmentCandidates()` 当前使用封邑级兵种库存和权重分配。

不能简单对每个村庄分别执行一次原来的权重分配，否则整体兵种比例可能发生变化。

应先在封邑范围内完成一次总量与权重规划，再把批准数量匹配到拥有对应真实兵种的村庄。缺少某兵种的村庄不允许凭空产生候选。

### 6.4 玩家选择逻辑

当前玩家选择流程使用临时 `TroopRoster`，只包含兵种和数量，不包含来源村庄。可以保留原有UI交互。

提交时由FiefPartyData将用户选择数量解析成具体村庄来源。必须确保：

- 用户选中的数量没有超过实际健康且合法的可征召兵员。
- 同一兵种分布在多个村庄时能够正确拆分。
- 预览和执行使用一致的资格判断。
- 同一士兵不被两个村庄重复提交。
- 事务失败时不会留下半完成动员。

### 6.5 原子事务

现有征召会涉及真实Roster、Hearth、在役批次、Party成员和AI预算。新实现必须有统一事务边界。

在提交前准备各村名册、Hearth和目标Party的必要回滚数据；任何一步失败时，恢复全部已修改资源。

不能出现：

- 两个村庄征召成功，第三个失败却直接返回0。
- Hearth已扣但兵员没有加入Party。
- Party已收到兵员但没有在役批次。
- 在役批次已创建但来源村庄并未真正失去兵员。
- AI预算已经提交，但招募事务最终失败。

---

## 7. 动员Hearth迁移

### 7.1 现有问题

当前 `FiefManpowerLedger.TryMobilize(fief,count,hearthPerTroop)` 会遍历整个封邑附属村庄，并轮流扣除Hearth。因此，当前实际出兵村庄与被扣户村庄未必相同。

### 7.2 目标结构

每座 `VillageFiefData` 自己持有 `MobilizedHearth`。

执行仍可复用 `FiefManpowerLedger` 服务，但该服务不再拥有另一套持久化余额。

新的动员操作应明确传入来源村庄及数量，例如：`TryMobilizeVillage(villageData, count)`。

当前每名封建兵出征临时扣除1 Hearth的规则不变。

### 7.3 动员执行

某村庄实际征召10人：

1. 验证该村庄拥有10名已批准封建兵。
2. 验证实际Hearth足以按当前100户保护规则扣除10。
3. 该村 `Hearth -= 10`。
4. 该村 `MobilizedHearth += 10`。
5. 提交对应的在役批次。

不得从其他村庄代扣。

### 7.4 正常返乡

假设本村10人在役士兵中有6人正式返乡：

- `MobilizedHearth -= 6`。
- 本村实际 `Hearth += 6`。
- 在役批次减少6人。
- 返乡冷却批次增加6人。

### 7.5 战损

如果4人确认死亡：

- 本村在役记录减少4人。
- 本村 `MobilizedHearth -= 4`。
- 不恢复已临时扣除的4 Hearth。

### 7.6 启动对账

`FiefPartyManager.ReconcileMobilization()` 和 `AddMobilizationMarkers()` 目前按封邑汇总并修补记录。

重构后：

- 逐村核对 `MobilizedHearth` 与实际在役批次。
- 不再平均生成动员标记。
- 不再为了弥补计数差异凭空生成Hearth记录。
- 发现无法解释的差异应报告明确错误并冻结相关危险操作，不能静默重置记录。

用户不需要旧存档迁移，因此旧版 `_legacyUntrackedMobilization` 可随本次业务改造清理。

---

## 8. 在役索引与战损对账

### 8.1 FiefServiceLedger

文件：`Fief/Services/FiefServiceLedger.cs`

当前 `Rebuild()` 直接遍历：`FiefPartyData.ServiceBatches`

改为：`FiefPartyData.Villages → VillageFiefData.ServiceBatches`

运行时索引 `ServingDetachmentRef` 明确包含：

- `SourceFief`：城镇/城堡，用于原有外部接口。
- `SourceVillage`：实际来源村庄。
- `Detachment`：原 `FiefServiceBatch`。

索引只是引用，不保存第二份批次。

### 8.2 FiefServiceReconciler

文件：`Fief/Models/FiefServiceReconciliation.cs`

当前第32—132行在野战Party范围内统一对账全部批次，已有：

- 同兵种精确匹配。
- 升级树上下兼容匹配。
- 跨批次按比例分配。
- 根据 `BatchId` 稳定排序。
- 确认死亡后释放动员记录。

**保留核心匹配算法，不要为了村庄化重新写一套。**

只修改对账结果归属：

`SourceFief → FiefPartyData.ServiceBatches` 改为 `SourceVillage → VillageFiefData.ServiceBatches`。

同一野战Party中来自多个村庄的相同兵种，必须在全部相关批次之间统一对账，不能让各VillageFiefData分别用整支Party的实际Roster独立判断存活人数。

### 8.3 转移服役身份

`FiefPartyManager.TransferFiefService()` 当前直接在封邑级集合增加、移除拆分批次。

修改时：

- 拆分后的批次仍归属原来的 `VillageFiefData`。
- `SourceVillage` 不随野战Party转移而改变。
- `SplitTo()` 原有服役天数和免薪天数保留。
- 更新运行时索引，但不重复扣动员Hearth。
- 不得把转移兵员当作新征召产生新的农村人口成本。

### 8.4 Party解散或被销毁

同步修改：

- `SettleRemainingFiefServiceAsLoss()`
- `OnPartyRemoved()`

每个来源村庄仅结算自己的批次。无法正常返乡的兵员按照现有损失语义处理，释放动员标记但不恢复对应Hearth。

---

## 9. 返乡和冷却重构

### 9.1 保留公开接口

`FiefPartyManager.TryReturnFiefTroops(party, sourceFief, batchId, maximumCount)`

继续保留。`sourceFief` 仍然是城镇/城堡，不要求所有外部调用者改为直接传入村庄。

### 9.2 FiefReturnService

文件：`Fief/Services/FiefReturnService.cs`

当前按 `SourceFief` 和 `BatchId` 选择服役批次，再调用 `FiefPartyData.TryCommitFiefReturn()`。

新实现：

1. 接收原有来源封邑。
2. 根据BatchId在本封邑所属村庄批次中定位唯一记录。
3. 取得真实 `SourceVillage`。
4. 验证来源、在役Party、所有权、战斗状态。
5. 调用对应村庄返乡事务。
6. 返回原有 `FiefReturnResult` 类型。

### 9.3 村庄返乡事务

以当前 `TryCommitFiefReturn()` 第71—264行为事务基础，迁移实际操作。必须包含：

- 真实Party成员检查。
- 伤员数量正确处理。
- 原在役批次扣减。
- 来源村庄动员Hearth准确恢复。
- 来源村庄冷却批次创建或合并。
- 完整失败回滚。
- 稳定拒绝原因码。

不要再从整个封邑动员余额中随机释放Hearth。

### 9.4 冷却推进

当前 `FiefPartyReinforcement.WeeklyUpdate()` 遍历封邑级 `ReturnBatches`，冷却结束时向 `_fiefParty` 添加士兵。

改为：

- FiefPartyData每周调度各村庄。
- VillageFiefData推进本村 `ReturnBatches`。
- 冷却完成后写入本村原版民兵Roster。
- 成功写入后删除对应冷却记录。
- 民兵Party暂不可用时保留记录，不得因为时间已经结束就提前删除。

冷却兵员不能同时存在于村庄ReadyRoster与ReturnBatches中。

---

## 10. FiefPartyManager 对外接口兼容

| 当前接口 | 新实现 |
|---|---|
| `GetFiefData(settlement)` | 返回城镇/城堡外壳 |
| `GetFiefTroopRoster(settlement)` | 汇总附属村庄就绪封建兵快照 |
| `GetAvailableTroopCount(settlement)` | 汇总各村就绪人数 |
| `GetRecruitedTroopCount(settlement)` | 汇总各村在役人数 |
| `GetWaitCycleTroopCount(settlement)` | 汇总各村冷却人数 |
| `GetFiefTroopCount(settlement)` | 汇总各村全部在籍人数 |
| `GetFiefTroopLimit(settlement)` | 暂保留旧封邑总人数上限 |
| `RecruitFiefTroopsFromSettlement(...)` | 通过外壳协调跨村征召 |
| `PreviewFiefRecruitmentApprovals(...)` | 汇总预览但按来源村庄建立内部执行计划 |
| `TryReturnFiefTroops(...)` | 根据BatchId路由至来源村庄 |
| `GetMobilizedHearth(village)` | 直接读取对应VillageFiefData |

建议新增内部接口：`GetVillageFiefData(Settlement villageSettlement)`。

它通过村庄 `Village.Bound` 找到所属封邑外壳，再获取对应村庄对象。不要求上层AI系统全部改用村庄对象。

### 10.1 GetFiefTroopRoster 的重要语义

返回结果必须是**新建的汇总Roster快照**。不能返回一个原版村庄的真实Roster，也不能把多个村庄Roster错误合并成一个可写权威库存。

实际征召必须进入带来源村庄的事务执行流程。

---

## 11. SettlementPatch 适配

文件：`ModifiedArmy/ModifiedArmy/Fief/Patches/SettlementPatch.cs`

当前：

1. Harmony Prefix接收 `militiaParty`。
2. 使用 `militiaParty.CurrentSettlement` 获取Settlement。
3. 使用 `manager.GetFiefData(settlement)` 获取封邑对象。
4. 使用模板排除封建兵。
5. 仅对非封建民兵执行原版减少逻辑的替代操作。

迁移后：

1. 判断当前民兵Party对应的是不是村庄。
2. 根据村庄 `Village.Bound` 解析所属城镇或城堡。
3. 查找该村对应的 `VillageFiefData`。
4. 使用该封邑模板识别本村封建兵。
5. 保留原有民兵保护逻辑。

不要在Patch里创建村庄数据、重新生成封建兵或修改动员Hearth。

**注意：** 原版 `Settlement.Militia` 将民兵Party总人数纳入民兵统计，因此还需要测试民兵自然增长、衰减的数量是否受驻村封建兵影响。现有Patch已经保护实际移除过程，但不等于修改所有原版民兵统计。先验证真实行为，再决定是否进一步调整。

---

## 12. 必须同步检查的调用点

### 12.1 ModifiedArmy内部

- `Fief/Models/FiefPartyManager.cs`
- `Fief/Models/FiefServiceReconciliation.cs`
- `Fief/Services/FiefServiceLedger.cs`
- `Fief/Services/FiefManpowerLedger.cs`
- `Fief/Services/FiefReturnService.cs`
- `Fief/Models/FiefMenuBehavior.cs`
- `Fief/Models/PartyData/FiefPartyData.cs`
- `Fief/Models/PartyData/FiefPartyReinforcement.cs`
- `Fief/Models/PartyData/FiefPartyRecruitment.cs`
- `Fief/Models/PartyData/FiefPartyReturn.cs`
- `Fief/Models/PartyData/FiefPartyEconomy.cs`
- `Fief/Patches/SettlementPatch.cs`
- `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`
- `ForceManagement/Services/PartyExchangeService.cs`
- `ForceManagement/Models/PartyExchangeModel.cs`
- `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`
- `ForceManagement/Services/DisbandSettlementService.cs`
- `PartyManagement/Models/PartyCreationSettlementModel.cs`

重点检查 `PartyExchangeService` 原有招募计划批准量与实际执行结果是否仍一致。

`FiefMenuBehavior` 的兵员选择可以继续使用汇总Roster快照，但实际提交必须经过来源村庄统一执行路径。

### 12.2 ModifiedPolitics跨模块

当前还存在以下调用：

- `ModifiedPolitics/WarPotential/Models/WarPotentialModel.cs` 使用 `GetFiefTroopCount(settlement)`。
- `ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs` 使用 `GetMobilizedHearth(village)`。

保留上述公开接口的含义，避免第二个Mod需要跟随内部重构。

### 12.3 共享兵源逻辑

`SettlementManpowerProductionBehavior.CalculateSettlementSupply()` 当前通过封邑总兵数、动员Hearth及村庄数量，估算共享农村兵源。

本阶段不重新设计共享兵源公式，但必须确保其调用不会因来源变为村庄而产生重复计算、负数或错误数据。

下一阶段实施等级供养制度时，再专门改造这一公式。

---

## 13. 关键一致性约束

### 13.1 人数守恒

任一村庄：

`封建兵总数 = 就绪 + 在役 + 冷却`

其中就绪取自实际村庄民兵Roster，在役和冷却取自该村持久化批次。不能额外把原版普通民兵计入封建兵数量。

### 13.2 动员守恒

正常情况下：

`本村MobilizedHearth = 本村有对应动员记录的实际在役人数`

本阶段维持每名封建兵出征临时扣除1 Hearth。正常返乡恢复Hearth，死亡不恢复。

### 13.3 数据归属唯一

每个在役或冷却批次只存在于一个 `VillageFiefData` 中。`FiefPartyData` 不再复制保存批次。

### 13.4 跨村征召守恒

全部村庄实际扣除的封建兵总数，必须等于野战Party实际收到的士兵数量，也等于新建在役批次记录总数。

### 13.5 战损只结算一次

同一个野战Party跨村混编的同兵种，只能在一次统一对账中分配实际存活和损失。

### 13.6 不使用虚假恢复

如果村庄民兵Party消失，不能根据历史人数快照自动重建全部驻村封建兵。

真实战损和Party生命周期变化必须按原版状态确认；在役与冷却批次不能因民兵Party暂时不存在而直接丢失。

---

## 14. 实施顺序

### Step 1：建立新持久化结构

- 新增 `VillageFiefData`。
- 修改 `FiefPartyData`，加入村庄集合。
- 调整 `FiefSaveDefiner`。
- 初始化村庄数据。
- 保持旧业务行为暂时不变，先确保新数据结构可编译、可序列化。

### Step 2：迁移就绪Roster、生产和升级

- 删除单一 `_fiefParty` 对真实Roster的绑定。
- 实现村庄Roster动态读取。
- 改造生产分配。
- 改造自动升级。
- 改造俘虏转封建兵。
- 实现封邑级只读汇总Roster。

**验收：** 新兵实际出现在附属村庄民兵Party，不在城镇/城堡中形成第二份封建兵库存。

### Step 3：迁移动员Hearth和征召记录

- 改造 `FiefManpowerLedger`。
- 从Manager移动动员权威余额。
- 修改征召计划以记录来源村庄。
- 实现跨村统一提交和回滚。
- 在每座 `VillageFiefData` 中创建在役批次。

**验收：** 哪座村庄出兵，就从哪座村庄扣除对应Hearth。

### Step 4：迁移在役索引与对账

- 修改 `ServingDetachmentRef`。
- 修改 `FiefServiceLedger.Rebuild/Register/Unregister`。
- 修改 `FiefServiceReconciler` 对账结果写入目标。
- 修改 `TransferFiefService`。
- 修改Party删除和战损结算。

**验收：** 多村相同兵种后，死亡、升级、调拨不丢失来源或重复结算。

### Step 5：迁移返乡和冷却

- 修改 `FiefReturnService`。
- 将返乡提交事务改为村庄级。
- 将冷却推进迁入村庄。
- 冷却结束后写回原村庄Roster。

**验收：** 返乡恢复正确村庄的Hearth，冷却完成后士兵回到原村庄。

### Step 6：兼容旧公共接口

- 修改 `FiefPartyManager` 查询方法。
- 修改 `SettlementPatch`。
- 检查UI、AI、PartyExchange、解散流程及跨Mod调用。

**验收：** AI和玩家仍可通过城镇/城堡统一征召，无需直接知道内部村庄结构。

### Step 7：新游戏运行验证

| 编号 | 场景 | 预期 |
|---|---|---|
| T01 | 城堡附属3村 | 创建3个VillageFiefData |
| T02 | 每村生成10人 | 各村真实Roster增加对应兵员 |
| T03 | 原版普通民兵增减 | 不错误删除封建兵 |
| T04 | 单村征召10人 | 该村Hearth减少10，在役增加10 |
| T05 | 三村联合征召25人 | 野战Party与三村批次总数守恒 |
| T06 | 两村同兵种混编 | 对账不重复计算存活人数 |
| T07 | 野战兵种升级 | 对应村庄在役记录同步 |
| T08 | 野战死亡5人 | 仅对应村庄损失与动员标记结算 |
| T09 | 返乡10人 | 回原村庄、恢复10动员Hearth、进入冷却 |
| T10 | 冷却结束 | 回到原村庄民兵Roster |
| T11 | 村庄遭袭 | 驻村封建兵真实参与防御，战损反映在名册 |
| T12 | 民兵Party被销毁 | 不造成重复生成或错误恢复 |
| T13 | 野战Party间调拨 | 保留原村庄、原服役天数、免薪信息 |
| T14 | 野战Party解散/毁灭 | 逐村结清在役兵员，无虚假Hearth返还 |
| T15 | 封邑失陷/易主 | 不错误清空仍在外的合法服役记录 |
| T16 | 新存档保存并加载 | 村庄、批次、冷却、动员记录及索引恢复 |
| T17 | 玩家菜单招募 | 原入口可用，实际抽兵来源正确 |
| T18 | AI自动招募与调兵 | 原审批和执行流程可用 |
| T19 | AI预算提交失败 | 不留下半完成事务 |
| T20 | 跨模块战力与人口查询 | 返回结果正确，未破坏现有公开接口 |

---

## 15. 构建和交付要求

Worker完成后必须：

1. 检查全部修改文件及 `git diff`。
2. 编译 `ModifiedArmy` 及受影响项目。
3. 运行适用的架构检查与发布验证脚本。
4. 核对关键 `Saveable` 类型及容器注册。
5. 检查所有对 `FiefPartyData.ServiceBatches`、`ReturnBatches`、`_fiefParty` 以及Manager旧动员字段的引用，确认不再通过旧路径执行写操作。
6. 输出实际变更文件列表。
7. 逐项报告编译、静态检查和游戏运行测试结果，区分PASS、FAIL、BLOCKED。
8. 未经用户明确要求，不提交、不推送、不创建PR。

编译成功不等于游戏运行验证成功；没有实际游戏证据的场景应标记BLOCKED。

---

## 16. 最终完成标准

### VillageFiefData

- 拥有本村封建兵完整的持久化生命周期状态。
- 关联本村真实民兵Roster。
- 在役、返乡、冷却与动员Hearth全部归本村所有。
- 驻村兵员升级、死亡、征召和返乡能够正确改变本村状态。

### FiefPartyData

- 只管理一座城镇或城堡。
- 维护附属村庄的集合关系。
- 负责统一兵员查询、生产额度分配和跨村征召协调。
- 不持有独立真实封建兵Roster。
- 不持有封邑级在役、返乡、冷却和动员账本。
- 保持现有对外公共接口兼容性。

### FiefPartyManager

- 保持Campaign事件和封邑级公共入口。
- 全局索引可以从村庄权威记录重建。
- 不维护第二份权威兵员或动员户数。
- 所有实际结算能够最终定位至来源村庄。

**核心原则：村庄持有状态，封邑负责协调，Manager维护全局入口，Service执行事务。**

第一阶段完成后，才进入按兵种等级占用村庄Hearth供养能力的第二阶段。届时无需再次重构封建兵存储结构。

---

## 附：源码核对链接

以下链接均固定到本文审核时的提交 `4610e3a1b98ee3625feb32bba39db7e4f81e0c5d`：

- [FiefPartyData.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [FiefPartyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [FiefPartyRecruitment.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [FiefPartyReturn.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [FiefServiceReconciliation.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs)
- [FiefManpowerLedger.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Services/FiefManpowerLedger.cs)
- [SettlementPatch.cs](https://github.com/GuihuaZhou/bannerlord/blob/4610e3a1b98ee3625feb32bba39db7e4f81e0c5d/ModifiedArmy/ModifiedArmy/Fief/Patches/SettlementPatch.cs)
