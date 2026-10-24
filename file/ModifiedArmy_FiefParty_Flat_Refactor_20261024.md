# ModifiedArmy — FiefParty 去五分类重构（代码核对版）

> 日期：2026-10-24  
> 仓库：`GuihuaZhou/bannerlord`，核对最新 main 上的 FiefParty、Recruitment、ModuleData 和 ModifiedPolitics 调用。  
> **状态：实施规格，尚未提交代码、编译或实测。** 本轮尝试创建远程重构分支时，GitHub 集成返回 `403 Resource not accessible by integration`，因此没有修改仓库。

## 0. 重构结论

移除 FiefParty 的五类旧兵员划分：`Militia / Sergeant / Marine / Slave / Retinue`。五类并非无引用：目前它们仍参与旧 FiefParty 编制限额、生产名额、征召、日志，且共享的 `SoldierTypeClassifier` 还被俘虏业务使用。它们在**当前封邑兵系统中不应具有业务含义**，但不能仅删除枚举及 XML 节点后保留上述计算。

FiefParty 应使用以下唯一信息：

- **身份资格：** 当前 `Settlement` 解析出的 `FiefPartyTemplate` 中具体兵种及其合法升级树。`IsEnableTroop(troop)` 是封邑身份资格的权威；不能使用全局 `SoldierTypeClassifier.IsFiefTroop` 代替。
- **兵种产出：** `fiefPartyTemplates.xml` 的具体 `<troop id="…" weight="…"/>` 决定各具体兵种的产出权重；`basic_troop_config.xml` 的对应兵种 `barracksLevel` 只提供生产解锁元数据。
- **唯一人数上限：** 封邑总体 `TotalLimit`，由已有 `FiefProductionConfig` 与封邑情况决定。**不再拆成五类子上限，也不以子上限限制补员/征召。**
- **真实人数：** 当前合法就绪成员 + `ServiceBatches` 中有效在役成员 + `ReturnBatches` 中有效冷却成员，按真实 `CharacterObject` 数量计算。任何一批不能因类型删除而自动转成战损。
- **征召操作：** 从实际就绪来源名册生成候选，预览、规划、审批、提交复用同一合法性和可用数量逻辑；已存在兵员的抽取与新兵生产是两种不同业务。

## 1. 现有代码发现

### Fief 本体

- `Fief/Models/FiefPartyTemplate.cs`：保留了 `_allBasicTroops : Dictionary<SoldierType,...>`、`_soldierTypeWeights`、`_allEnableTroops : Dictionary<CharacterObject,SoldierType>`；解析五类 TroopComposition 组。
- `Fief/Models/PartyData/FiefPartyData.cs`：保留 `_soldierTypeCounts`、`_soldierTypeMaxCounts`、`_soldierTypeWeights`、`_totalWeight`；`CalculateLimit()` 按类型分摊总编制；`SyncManpowerCounters()` 按类型聚合。`[SaveableProperty(3/4/5)]` 是旧 RetinueCount、SergeantCount、MilitiaCount，必须考虑旧存档。
- `Fief/Models/PartyData/FiefPartyReinforcement.cs`：`WithdrawFiefTroopEntries` 先以 `SoldierType` 类型剩余容量约束每个候选；`PerformReinforcement` 更新分类计数和分类日志。
- `Fief/Models/PartyData/FiefPartyRecruitment.cs`：AI征召的现有来源判断已有部分迁到模板，但候选数量仍按五类权重截断；`PreviewFiefRecruitmentCandidates` 亦如此。玩家手动选择的前置验证仍调用 `SoldierTypeClassifier.IsFiefTroop`，之后才二次检查模板。
- `Fief/Models/FiefMenuBehavior.cs`：玩家选兵允许条件仍调用全局 `IsFiefTroop`。
- `Fief/Patches/SettlementPatch.cs`：在 `Settlement.RemoveMilitiasFromParty` 中按全局类别保护民兵名册里的“封邑兵”。这是危险的越权识别，应限定**该民兵Party的来源封邑及真实许可兵员**。
- `Fief/Models/FiefPartyManager.cs`：`GetFiefTroopCounts()` / `GetFiefMaxTroopCounts()` 向外暴露旧分类字典。
- `Fief/Models/PartyData/FiefPartyReturn.cs`：返乡日志仍逐项输出五类总数。合法返乡、Hearth、批次及冷却本身不应重写。

### 共享代码和跨 Mod 依赖

- `Recruitment/Models/NewBasicTroopManager.cs`：`BasicTroopGroup`、`BasicTroopEntry.Type`、`SoldierTypeClassifier` 仍以五类解析 `basic_troop_config.xml`。
- `Recruitment/Pools/Models/ProfessionalTroopTemplate.cs`：职业兵模板通过 BasicTroopGroup 查找兵营要求。**不得因为清理 Fief 破坏职业兵XML权重或兵营资格。**
- `Recruitment/Models/NewPrisonerRecruitmentCalculationModel.cs`：五类调整俘虏归顺难度。
- `Recruitment/Patches/PrisonerSalePatch.cs`：五类决定部分俘虏留用优先级。
- `ModifiedPolitics/WarPotential/Models/WarPotentialModel.cs`：读取 `GetFiefTroopCounts(settlement).Values.Sum()`。改接口必须同步修改 ModifiedPolitics。
- `ModuleData/submodule_strings.xml`：封邑补员/征召/返乡日志字符串含五类字段。

上述依赖说明：**Fief 里的五类业务配额可以删除；但若要从整个项目删除枚举和全局分类器，还必须给俘虏模块准备独立策略替代，不能让价格/留俘行为悄悄变化。**

## 2. 目标代码结构

```text
FiefPartyTemplate
  Culture, TemplateId, TroopEntries<TroopProductionEntry>
  EnabledTroops : HashSet<CharacterObject>（由模板基础兵种升级树形成）
  IsEnableTroop(CharacterObject)

FiefPartyData
  Settlement, Template
  TotalLimit, TotalCurrentCount（运行时派生）
  ReadyRoster（当前就绪兵源）
  ServiceBatches（已有持久化权威记录）
  ReturnBatches（已有持久化权威记录）
  GetReadyCount(), GetServingCount(), GetCoolingCount()
  GetAvailableRecruitmentRoster(), ProduceDailyFiefTroops()
  PreviewFiefRecruitmentCandidates(), RecruitTroopsToParty()
  RecruitManualSelection(), RecruitFromPrisoners()

FiefPartyManager
  GetFiefTroopCount(settlement) : int
  GetFiefTroopLimit(settlement) : int（优先复用已有公开方法）
  批次登记、身份对账、Hearth 与返乡事务保持原能力
```

旧 `_soldierTypeCounts/_soldierTypeMaxCounts/_soldierTypeWeights/_totalWeight` 不再作为运行时业务属性；不能替换成另一组功能相同的分类配额。`LegacyRetinueCount / LegacySergeantCount / LegacyMilitiaCount` 仅在必要时作为反序列化兼容字段保存，不参与计算或显示。

### 2.1 统一的只读候选

由一个方法同时提供预览与最终提交的候选名单，使用实际来源名册、模板和数量约束：

```csharp
// 伪代码，按现有方法签名落地
var roster = data.GetFiefTroopRoster();
foreach (var element in roster.GetTroopRoster()) {
    if (element.Character == null || element.Number <= 0) continue;
    if (!template.IsEnableTroop(element.Character)) continue;
    // 依据当前Party剩余容量/授权数量给出候选
    // 后续统一AI招募Model审核，不由五类配额决定兵种资格
}
```

批准清单必须在执行时**再检查来源名册仍持有对应兵种及健康/伤员数量**。`PartyExchangeModel.AddAmount` 在 source/roster 项不存在时应给出带原因码的拒绝，而非对 `FirstOrDefault` 的 `null` 使用 `WoundedNumber`。用户上传的《2026-10-23_统一调兵崩溃异常分析报告.md》已记载对此问题的静态修复；应以当前代码核对，避免重复补丁。

### 2.2 生产与容量

生产筛选：`Template.TroopEntries` + `RequiredBarracksLevel` + `weight>0`。按**具体兵种**的 XML 权重分配 approvedCount，不能先分五大类。生产后的封邑总体人数不得超过 `TotalLimit`，需要连同在役和冷却计算；日常补员预测不得推进真实持久化计数。

不要求现存就绪部队按XML权重强制重排。原有升级/伤亡和返回冷却行为保留。`RecruitFromPrisoners` 只校验具体模板和总体剩余容量，不校验五类各自限额。

### 2.3 五类XML结构裁剪

目标形态：

```xml
<BasicTroopGroup id="basic_troops_empire" culture="Culture.empire">
  <troop id="NPCCharacter.imperial_cataphract" barracksLevel="3" />
  <troop id="NPCCharacter.imperial_equite" barracksLevel="1" />
</BasicTroopGroup>

<FiefPartyTemplate id="empire_castle" culture="Culture.empire">
  <TroopComposition>
    <troop id="NPCCharacter.imperial_equite" weight="2" />
    <troop id="NPCCharacter.imperial_infantryman" weight="4" />
  </TroopComposition>
</FiefPartyTemplate>
```

这是结构示例，**不是完整模板或授权改变帝国模板实际兵种**。真实迁移应展平现有五类节点，原样搬迁每个 troop 的 ID、weight、barracksLevel，保留全部文化、城镇/城堡/港口变体；不得因为删除分类组而删兵种。`professionalTroopTemplates.xml` 同样需由平铺解析器兼容，但不改变长期生产权重和资格。

如果为了旧文件兼容暂时容许读取 `<RetinueTroops>...</RetinueTroops>` 等历史包装，解析后的内部数据也必须立即展平成具体兵种，不再给该包装任何运行时政策意义。正式 XML 不再写这些组。

## 3. 关键隐患：封邑就绪名册与原生民兵共享底层Roster

当前 `FiefPartyData.InitialFeifPartyData()` 在 `_fiefParty == null` 时，把 `_settlement.MilitiaPartyComponent.MobileParty.MemberRoster` 直接赋给 `_fiefParty`。这意味着 Fief 与原生 Militia 可能共享 `CharacterObject` 和人数。

改成模板资格后可避免跨封邑误判，但**不能凭 `CharacterObject` 判断某一名同类型士兵究竟来自原生民兵还是封邑生产**；这是原始数据模型本身无法区分的身份问题，五分类仅掩盖而非解决它。

本轮推荐：维持当前Roster存放方式并显式记录这种边界，先完成去分类与守恒测试，避免同时改变民兵守城行为；如果游戏中实际出现同兵种双来源的相互污染，另行增加独立、可存档的封邑就绪兵清单，进行旧档迁移并研究守城参与方式，**不得直接清空现有民兵Roster**。

## 4. 实施顺序（每一批可独立回归）

1. **R1：资格统一**：在 `FiefPartyRecruitment.RecruitManualSelection`、`FiefMenuBehavior.CanSelectTroop`、`SettlementPatch` 删除全局 `IsFiefTroop` 判断，换成当前封邑模板/真实可用Roster查询；预览和提交从同一候选生成器走。
2. **R2：删除分类额度**：`FiefPartyTemplate` 改为扁平 `TroopEntries`；`FiefPartyData` 改总体人数及上限；`FiefPartyReinforcement` 按具体兵种权重生产；`FiefPartyRecruitment` 去五类配额与五类计数；俘虏转入封邑按总上限处理。
3. **R3：XML与UI日志**：平铺 Fief 及基础兵种/职业兵模板的 XML 与读取模型，确保权重/兵营属性不变；把五类数量日志改成`本次征召数/当前总数/总上限`或实际兵种汇总。
4. **R4：跨Mod兼容**：`ModifiedPolitics.WarPotentialModel` 改为读取封邑总人数 API；保留旧 Saveable Property ID 3/4/5 的兼容读取，新的模型不消耗这些属性。保存/加载对账后不应改写在役来源批次。
5. **R5：全局遗留分类**：独立处理 `NewBasicTroopManager`、`NewPrisonerRecruitmentCalculationModel`、`PrisonerSalePatch` 使用的旧分类器。先保留现有俘虏规则或明确重新定义独立的俘虏价值/资格策略，最后才能彻底删除 `SoldierType` 枚举及 `SoldierTypeClassifier`。
6. **R6：验收**：静态引用搜索 + 编译 + 旧档/新档 + 30游戏日压力测试，特别是此前 `castle_ES8` / `imperial_menavliaton` 案例。

## 5. 不可破坏的不变量

- `FiefServiceBatch`、`FiefReturnBatch`、来源封邑、归还冷却、`FreeWageDays`、Hearth占用和释放不因五类移除而改变。
- 玩家手动招募与AI征召都只能转移真实存在、当前模板认可的士兵，预览/执行集合一致。
- `GetFiefTroopRoster()` 只读查询不应改变名册；Model 不得通过对账/库存交易产生副作用。
- `ServiceBatches` 记录的在役士兵不能因为新模板改变或领地易主而自动丢失；历史在役身份独立于今日生产资格。
- 生产、实际抽调、在役登记、返乡冷却的兵员与 Hearth 账本保持守恒。
- 旧存档 ID 和键不复用；即使历史分类数据已废弃，也必须保证旧存档可以反序列化。
- ModifiedPolitics 引用修改时同仓检查，以避免运行期 `MissingMethodException`。
- 封邑生产、职业兵生产与俘虏政策各自有明确边界，不把一个领域的资格判定误用于另一个领域。

## 6. 验收矩阵（必须全部通过后才算已重构）

| 场景 | 期望 |
|---|---|
| 帝国城堡不包含 `imperial_menavliaton` | 预览、AI审批、手动选择、提交均不可把其当作该封邑的可征召来源 |
| 同文化、不同城镇/城堡模板 | 同一兵种可能在一处合法，在另一处不合法；不再由全局分类决定 |
| 五类XML包装移除 | 原 troop ID、weight、barracksLevel、城镇类型和文化全部完整 |
| 每天仅1～3名封邑生产 | 具体兵种权重长期分配合理，不因类别整数截断永久漏产 |
| 就绪100，在役30，返乡冷却20，总限200 | 可新生产上限50（其他政策另计） |
| 旧档包含在役批次及历史SaveableProperty(3/4/5) | 正常反序列化、批次/Hearth/冷却不丢失，旧五类值不影响人数 |
| 玩家手动选兵与每日AI征召 | 候选与来源真实Roster一致，拒绝超过来源数量，日志实际数正确 |
| 民兵移除和围城结束 | 不误删本封邑合法士兵，也不保护其他封邑/职业兵；原生民兵规则正常 |
| ModifiedPolitics战争潜力 | 使用封邑总兵力API，编译运行正常 |
| Fief返乡/解散及跨升级树对账 | 不因为五类被删除而隐性战损、重建或恢复不该恢复的Hearth |
| 30/100日压力测试 | 无重复征召、无负数库存、无编制漂移，无同日重复执行导致崩溃 |

## 7. 完工定义

- `Fief/` 的业务代码不再引用五个类别的枚举或分类器，也不保存类别配额；全部按具体模板、统一总容量和真实批次执行。
- `basic_troop_config.xml`、`fiefPartyTemplates.xml`、`professionalTroopTemplates.xml` 的正式结构不再使用五类包装；保留原始兵种与数值。
- 与其他模块/Mod共享的旧分类器在确认被独立政策替代后删除；`SoldierType` 不再作为新系统的业务接口。
- 编译、旧存档读写、AI与玩家征召及长期实机验收有明确结果；否则不宣布已完成。
