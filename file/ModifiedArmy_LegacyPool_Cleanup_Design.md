# ModifiedArmy 旧兵池机制清理设计与实施清单

> **文档性质：** 设计与代码审查整理稿；**尚未实施任何代码修改**。  
> **仓库：** `GuihuaZhou/bannerlord`  
> **审查基线：** `main`，commit `c162342bd4eacb8f41af6ded90d40e540e586e96`（2026-10-14）  
> **主要范围：** `ModifiedArmy`；同时检查 `ModifiedPolitics` 的跨 Mod 依赖。  
> **定位规范：** 全文使用“文件路径 + 代码符号”，不使用容易因编辑而失效的行号。

## 1. 背景与目标

**旧流程：** 村庄兵源 → 职业兵池／封建兵池（持久化 troop 库存）→ 招募 → 驻军／封邑部队。  
**现行流程：** 村庄共享基础兵源 → 每日生产配额 → 具体 troop → 驻军／封邑部队。

其中职业兵会先形成**当日临时招募清单**，由驻军统一招募流程消耗；该清单不是旧的持久化 troop 库存。

**清理目标：**

1. 移除旧职业兵池、封建兵池的独立库存、容量、补库及兵种来源索引。
2. 移除已失效的志愿兵招募、旧兵池日志及配置。
3. 将仍然有效、却以“兵池／志愿兵”命名的代码改成反映实际职责的名称。
4. 保留共享基础兵源生产、职业兵进入驻军、封邑补员、AI 驻军调拨及相关经济约束。
5. 修正封邑补员 UI 使用旧公式的问题；保护存档兼容与 `ModifiedPolitics` 联动。

### 操作类型

- **删除**：无有效消费者，或仅服务于废弃持久化库存。
- **重命名**：实现仍有效，但名称不符合当前职责；必须同步更改调用点。
- **修改逻辑**：功能保留，但公式／输入数据／执行路径需要更正。
- **保留**：当前系统仍然依赖，不可误删。
- **待验证**：依赖动态注册、资源引用或存档兼容，不能直接认定可删。

以下 C# 路径均相对于 `ModifiedArmy/ModifiedArmy/`；XML 路径相对于 `ModifiedArmy/ModifiedArmy/ModuleData/`。

## 2. 删除废弃的库存与补库代码

| 文件 | 代码符号 | 操作 | 简要理由 |
|---|---|---|---|
| `Recruitment/Pools/Models/SettlementRecruitmentPoolData.cs` | `ProfessionalTroops` | 删除（存档验证后） | 旧职业兵持久化库存；现行生产不再向此字典存兵。 |
| 同上 | `FiefTroops` | 删除（存档验证后） | 旧封建兵持久化库存；当前直接产出到真实封邑部队名册。 |
| 同上 | `GetTroops()` | 删除 | 只用于访问上述旧库存。 |
| 同上 | `GetProductionProgress()` | 删除 | 未发现有效调用；当前生产直接通过进度读写方法实现。 |
| 同上 | `EnsureInitialized()` | 删除旧字典初始化，并移除其调用 | 仅用于确保两个废弃库存字典存在。 |
| `Recruitment/Pools/Behaviors/SettlementRecruitmentPoolBehavior.cs` | `ReplenishPool()` | 删除 | 旧补库流程；每日生产入口已改为配额→具体 troop。 |
| 同上 | `SelectLargestShortage()` | 删除 | 仅被旧补库函数用来填补容量缺口。 |
| 同上 | `GetCapacity()` | 删除 | 查询已废弃的库存容量，未见现行消费者。 |
| 同上 | `OnSessionLaunched()` 中 `InitializeTroopSources()`、`EnsureInitialized()`、清空两字典的语句 | 删除相关语句；如事件处理函数变空，则一并取消事件订阅 | 不再需要恢复／清空旧库存和初始化废弃索引。 |
| `Recruitment/Pools/Models/RecruitmentPoolTemplateRepository.cs` | `_troopSources` | 删除 | 旧“兵种→兵池来源”索引已无有效消费者。 |
| 同上 | `InitializeTroopSources()` | 删除 | 仅创建上述来源索引。 |
| 同上 | `GetSources()` | 删除 | 未发现有效调用。 |
| 同上 | `RegisterTemplateTroopTrees()` | 删除 | 只服务于旧来源索引。 |
| 同上 | `RegisterTroopTree()` 的相关重载 | 删除 | 仅负责遍历升级树并维护旧索引。 |
| `Recruitment/Pools/Models/ProfessionalTroopTemplate.cs` | `BaseCapacity`、`GetCapacity()` 与 `baseCapacity` XML 解析 | 删除 | 当前职业兵生产由每日训练能力约束，不存在此库存容量。 |
| `Fief/Models/FiefPartyTemplate.cs` | `BaseCapacity`、`GetCapacity()` 与对应 XML 解析 | 删除 | 封邑部队的实际编制上限另有计算，不需要旧库存容量。 |
| 同上 | `BaseDailyProduction`、`DailyProductionStep`、`GetDailyProduction()` 与对应 XML 解析 | 删除 | 封邑每日产量现由共享兵源及生产预测控制。 |
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | `RecruitProfessionalTroops()` | 删除 | 未见有效调用；职业兵招募已由 `ExecuteUnifiedPlan()` 统一执行。 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | `LogProfessionalPlan()` | 删除 | 旧职业兵池招募日志，无调用入口。 |
| 同上 | `GetRecruitVolunteerFromIndividual()` | 删除 | 旧志愿兵辅助入口已不参与当前 AI 招募。 |
| 同上 | `ApplyInternal()` 中仅服务于志愿兵的分支 | 删除无用分支 | 现行有效调用仍使用雇佣兵分支；不能删除整个有效雇佣兵路径。 |
| `Recruitment/Pools/UI/PlayerRecruitmentPoolPatch.cs` | 整个文件 | 删除 | 仅保留无业务行为的空类。 |
| `Fief/Models/PartyData/FiefPartyData.cs` | `InitialFeifParty()` | 删除 | 旧首次补员逻辑，未发现调用。 |

**共享接口同步处理：** `Recruitment/Pools/Models/RecruitmentPoolTemplate.cs` 的 `IRecruitmentPoolTemplate.BaseCapacity`、`GetCapacity()` 需与两类模板同步移除；`GetDailyProduction()` 不再强制由封邑模板实现。职业兵的 `ProfessionalTroopTemplate.GetDailyProduction()` 必须保留，并调整实际职业兵生产调用点，不可破坏 `GetDailyProduction(Settlement, ...)` 及其 Harmony 补丁。

## 3. 统一生产系统命名

这些代码仍在运行，**只做语义重命名，不改变其生产算法**。下面的新名称为本次方案确定的目标名称。

| 文件／位置 | 旧名称 | 新名称 | 简要理由 |
|---|---|---|---|
| `Recruitment/Pools/Behaviors/SettlementRecruitmentPoolBehavior.cs` | `SettlementRecruitmentPoolBehavior` | `SettlementManpowerProductionBehavior` | 负责基础兵源分配、累计进度和 troop 生产，而非库存。 |
| `Recruitment/Pools/Models/SettlementRecruitmentPoolData.cs` | `SettlementRecruitmentPoolData` | `SettlementManpowerProductionData` | 保存生产进度和每周配额，不再保存 troop 库存。 |
| 行为类字段 | `_settlementPools` | `_settlementProductionData` | 集合元素是生产状态数据。 |
| 行为类方法 | `GetOrCreatePool()` | `GetOrCreateProductionData()` | 返回生产状态记录。 |
| `Recruitment/Pools/Models/RecruitmentPoolTemplate.cs` | `RecruitmentPoolKind` | `TroopProductionKind` | 区分职业兵／封邑兵生产类型。 |
| 同上 | `RecruitmentPoolTroopEntry` | `TroopProductionEntry` | 模板兵种及权重配置，不是实际库存条目。 |
| 同上 | `IRecruitmentPoolTemplate` | `ITroopProductionTemplate` | 表达兵种生产模板的共同契约。 |
| 同上 | `RecruitmentPoolTemplateUtility` | `TroopProductionTemplateUtility` | 主要提供配置解析及职业兵训练速率计算。 |
| `Recruitment/Pools/Models/RecruitmentPoolTemplateRepository.cs` | `RecruitmentPoolTemplateRepository` | `TroopProductionTemplateRepository` | 管理现行生产模板的注册与解析。 |
| 职业兵与封邑兵模板 | `PoolTroops` | `TroopEntries` | 表示可选的兵种配方而非库存。 |
| 生产行为类 | `BuildVirtualOffers()` | `BuildDailyProfessionalOffers()` | 明确生成当日职业兵临时招募清单。 |
| 生产行为类 | `GetAvailableTroops()` | `GetAvailableProfessionalOffers()` | 查询当日职业兵招募额度。 |
| 生产行为类 | `TryConsume()` | `TryConsumeProfessionalOffer()` | 消耗当日招募额度及关联的基础兵源成本。 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | `WithdrawFiefPoolTroops()` | `SelectFiefTroopsForProduction()` | 按模板选具体 troop，不是从旧库存提兵。 |
| `Fief/Models/FiefMenuBehavior.cs` | `InitializeFiefPoolMenu()` | `InitializeFiefTroopRosterMenu()` | 菜单显示 `GetFiefTroopRoster()` 的真实封邑部队。 |
| `Recruitment/Models/RecruitmentTypes.cs` | `RecruitmentSource.Professional` | `RecruitmentSource.ProfessionalProduction` | 标识当日生产来源，而非持久化库存。 |

**同步范围：** 所有引用、类型参数、模板注册、诊断输出及 Harmony 特性／补丁目标均需随名称更新。对跨 Mod 类型尤其要同步检查 `ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs`。

**刻意保留的名称：** `ProfessionalTroopTemplate`、`FiefPartyTemplate`、`RecruitmentProductionForecast`、`ProfessionalProductionProgress`、`FiefProductionProgress`、`_dailyProfessionalOffers`、`GetProductionForecast()`；这些名称与现行职责一致。

> **兼容性约束：** 类与字段可以在 C# 中重命名，但 `SyncData` 的旧键 `"_settlementRecruitmentPools"` 不得顺手改成新键，否则可能无法读取旧存档。存档字段 ID 与 `SaveDefiner` 注册须单独验证。

## 4. AI 招募：将旧 Volunteer 路径明确改为驻军调拨

### 4.1 当前实际调用链

`AiRecruitmentBehavior` 决定前往某个自有城镇／城堡 → `AiRecruitmentPatch` 的 `RecruitSource.Volunteer` 分支 → `TryRecruitVolunteers()` → `TryRecruitProfessionalPool()` → `GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison()`。

虽然仍叫“志愿兵／职业兵池”，**实际兵力来自驻军的 `MemberRoster`，调入野战部队时同步从驻军扣减**，因此不属于旧持久化库存招募。必须保留这一有效调拨能力。

### 4.2 具体调整

| 文件／位置 | 原名称／行为 | 新名称／操作 | 简要理由 |
|---|---|---|---|
| `common/common.cs` | `RecruitSource.Volunteer` | `RecruitSource.GarrisonTransfer` | 实际兵源为驻军。 |
| `common/common.cs` | `RecruitmentDecision.VolunteerWeight` | `RecruitmentDecision.GarrisonTransferWeight` | 控制驻军调拨偏好。 |
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` | `FindBestVolunteerSettlement()` | `FindBestGarrisonTransferSettlement()` | 查找自有、驻军兵力足够的城镇／城堡。 |
| 同上 | `HasAvailableVolunteers()` | `HasTransferableGarrisonTroops()` | 实际检查驻军可用性。 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | `TryRecruitVolunteers()` | `TryTransferFromGarrison()` | 真实动作为从驻军调兵，不是招募志愿兵。 |
| 同上 | `TryRecruitProfessionalPool()` | 将其校验及调拨逻辑合并入 `TryTransferFromGarrison()`，删除旧方法 | 消除冗余调用和旧兵池语义。 |
| `common/ModConfig.cs` | `AiCultMismatchVolunteerPenalty` | `AiCultMismatchGarrisonTransferPenalty` | 作用对象是驻军调拨目标选择。 |
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` | `IsPartyCurrentlyOnRecruitmentTask()` 内“前往 Clan 村庄招募志愿兵”判断 | 删除村庄分支，保留有效的城镇／城堡任务判断 | 村庄不再提供旧志愿兵招募入口。 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | `LogProfessionalPlan()` 及旧本地化日志 | 清除无效日志及其无引用文本 | 当前实际调拨已由驻军转移模型完成。 |

**重构后的 AI 兵源枚举：**

```csharp
RecruitSource.Fief
RecruitSource.GarrisonTransfer
RecruitSource.Mercenary
```

语义分别为：**封邑征召、驻军调拨、雇佣兵招募**。

保留 `GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison()` 的现行执行路径、可招募数量限制及部队名册的双向更新，不应恢复直接从旧职业兵池取兵。

**额外检查：** `AiRecruitmentBehavior` 还有不受普通随机招募门控约束的 `FindBestMultiPurposeSettlement()`，用于驻军补兵／交付俘虏；属于有效后勤逻辑，应保留并确保重命名后路径正常。`RecruitmentDecision` 的决策权重、日志、配置解析均需同步调整。

## 5. 修正封邑补员 UI 与旧公式

| 文件 | 代码符号 | 操作 | 简要理由 |
|---|---|---|---|
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | `GetWeeklyUpdateCount()` | 将调用方迁往当前生产预测后删除 | 仍按旧每周补员公式估算，已不代表当前实际生产。 |
| `Fief/Models/FiefPartyManager.cs` | `GetWeeklyUpdateCount(Settlement)` | 同步删除旧接口 | 仅对外提供旧估算。 |
| `Fief/Models/FiefMenuBehavior.cs` | `InitializeFiefPoolMenu()` → `InitializeFiefTroopRosterMenu()` | 保留实际名册展示；将显示的每日补员改为 `GetProductionForecast().FiefAllocation` | 当前菜单用旧每周值填充 `Daily reinforcement`，口径不一致。 |
| 同上 | 菜单说明中的 `WEEKLY_REINFORCEMENT` 等旧补员变量 | 改用有明确单位的当前预测值；文字同步调整 | 避免把旧周产量误认为日生产能力。 |
| 同上 | `InitializeReinforcementRateMenu()` | 保留其当前预测逻辑 | 已使用 `RecruitmentProductionForecast` 的每日生产信息。 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | `CalculateReinforcementMultiplier()` | 删除旧补员／初始补员消费者后再次检查，若无其他消费者则删除 | 避免删除仍被其他强化逻辑使用的计算。 |
| `Fief/Models/FiefPartyTemplate.cs` | `MinWeeklySupplement` | 待核实其他调用／兼容用途后决定是否删除 | 可能仅作为旧公式及旧配置的回退参数。 |

**有效生产指标：** `RecruitmentProductionForecast.FiefAllocation`（每日封邑兵生产分配）；实际补员仍受现行库存余量（实际封邑部队编制）、本周产量配额及基础兵源供应限制。

**UI ID 注意：** 封邑菜单内部字符串常量（如旧 `fief_view_pool` 或菜单 ID）不必为了视觉语义立即改动；先保证界面行为和兼容性，再检查是否需要调整内部 ID 或本地化文案。

## 6. XML、配置与资源整理

| 文件 | 配置／资源 | 操作 | 简要理由 |
|---|---|---|---|
| `professionalTroopTemplates.xml` | `baseCapacity` | 删除 | 职业兵不再有固定库存容量。 |
| `fiefPartyTemplates.xml` | `baseCapacity` | 删除 | 封邑兵的真实部队名册已有独立上限。 |
| 同上 | `baseDailyProduction`、`dailyProductionStep` | 删除 | 旧封建兵池产速公式已不决定每日补员。 |
| `modConfigs.xml` | `<VolunteerRecruitment>` | 删除；同时清理 `ModConfig` 及 `common` 对应已失效生成倍率 | `NewVolunteerModel` 已移除，旧原版志愿兵生成倍率没有发现有效消费者。 |
| `modConfigs.xml` | `<CulturalPreferences>` 下 `<Volunteer .../>` | 改为 `<GarrisonTransfer .../>`，同步 XML 解析与默认值 | 目前仍影响 AI 驻军调拨，不可简单删掉权重。 |
| `modConfigs.xml` | `<MismatchVolunteerPenalty .../>` | 改为 `<MismatchGarrisonTransferPenalty .../>`，同步解析 | 实际用于驻军调拨目标文化修正。 |
| `submodule_strings.xml` | `str_modifiedarmy_ai_recruitment_volunteer_plan`、`str_modifiedarmy_ai_recruitment_professional_plan` | 确认无动态引用后删除 | 仅见于已经废弃的 `LogProfessionalPlan()`。 |
| `ModuleData/Languages/` | 旧职业兵池／志愿兵显示文本 | 查验引用后清理或改文案 | 避免保留无效描述或误删仍在展示的文本。 |

**绝对保留：** `professionalTroopTemplates.xml` 的 `baseDailyProduction`、`dailyProductionStep`，以及 `ProfessionalTroopTemplate.GetDailyProduction()`；这些仍决定职业兵训练能力，也与 `ModifiedPolitics` 军事官职加成相关。

## 7. 待验证后再删除的代码

| 文件 | 对象 | 原因／核查方法 |
|---|---|---|
| `Recruitment/Pools/UI/RecruitmentPoolTuplePatches.cs` | 三个 `RecruitVolunteerTuple` UI 补丁 | 旧玩家招募菜单虽已移除，但 UIExtenderEx 自动发现补丁；确认没有其他仍在使用的界面后再删除。 |
| `ModuleData/submodule_strings.xml` 与语言文件 | 旧文本 ID | 需搜索字符串及动态调用，不能只凭静态直接引用判断。 |
| `FiefPartyTemplate.cs` | `MinWeeklySupplement` | 先确认旧周补员公式和 XML 回退用途是否全部消失。 |
| `FiefPartyReinforcement.cs` | `CalculateReinforcementMultiplier()` | 先清理旧调用，再核对是否仍有真实业务消费者。 |
| 相关 `SaveDefiner` 与已持久化字段 | `SettlementRecruitmentPoolData` 类型／字典 | 先用旧存档进行载入测试，确认 Saveable 类型 ID、属性 ID、容器定义及转换方案安全。 |

## 8. 必须保留的现行功能

| 代码／功能 | 保留原因 |
|---|---|
| `SettlementRecruitmentPoolBehavior.OnDailySettlementTick()`（重命名后对应入口） | 每日基础兵源生产入口。 |
| `GetProductionForecast()`、`CalculateVillageSupply()` | 共享基础兵源与职业兵／封邑兵的配额分配。 |
| `ProfessionalProductionProgress`、`FiefProductionProgress` | 小数生产进度跨天累计。 |
| `FiefProducedThisWeek`、`WeekKey` | 封邑兵周配额及周次跟踪。 |
| `_dailyProfessionalOffers`、`BuildVirtualOffers()`（重命名后） | 当日职业兵临时供应，并非持久化库存。 |
| `GetAvailableTroops()`、`TryConsume()`（重命名后） | 驻军读取并实际消耗当日职业兵供应及对应村庄户数。 |
| `GarrisonRecruitFromPrisonersBehavior.ExecuteUnifiedPlan()` | 职业兵／战俘等来源的驻军统一招募。 |
| `FiefPartyData.ProduceDailyFiefTroops()` | 封邑部队每日生产。 |
| `WithdrawFiefPoolTroops()`（重命名后） | 选取实际生成的封邑 troop 类型。 |
| `FiefPartyData.WeeklyUpdate()` | 仍承担自动升级、服务周期等维护职责；不能按名称认定为旧补员流程。 |
| `FiefPartyManager.GetFiefTroopRoster()` | 返回实际封邑部队名册，不是旧兵池。 |
| `GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison()` | AI 从真实驻军扣兵并补入野战部队。 |
| `ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs` | 修改职业兵每日训练能力的有效 Harmony 补丁。 |
| `RecruitmentPoolTemplateRepository` 的模板 `Register()`、`Resolve()`（重命名后） | 现行生产仍依赖模板解析；只能删除其中无用的来源索引。 |

## 9. 存档兼容与跨 Mod 约束

1. **不要因删除旧字典而删除整个生产状态数据类。** 当前仍需持久化职业兵／封邑兵生产进度与封邑周配额。
2. **保留原 `SyncData` 键** `"_settlementRecruitmentPools"`，除非提供明确的双键兼容迁移。
3. 旧 `[SaveableProperty]` 编号不能随意重用；先验证删除字段后的旧存档载入，必要时暂留废弃字段用于兼容。
4. 检查 `FiefSaveDefiner` 内 `SettlementRecruitmentPoolData` 类型及 `Dictionary<Settlement, SettlementRecruitmentPoolData>` 容器定义。迁移到新类型名时不能遗漏。
5. `ModifiedPolitics` 对生产行为类及 `GetDailyProduction` 的 Harmony 绑定须与新类名／方法签名同步。
6. 配置重命名需修改 `ModConfig` 解析逻辑，核对默认值及不同文化偏好结果，不能只改 XML 标签。
7. 所有公开方法、模板接口、枚举与引用点同步修改；不能只执行局部文本替换。

## 10. 建议实施顺序与验收

| 阶段 | 内容 | 为什么按此顺序 |
|---|---|---|
| 1 | 删除无效补库函数、旧来源索引、无调用日志和空类 | 基本不涉及有效状态，最容易验证。 |
| 2 | 删除旧容量、封邑旧产速及 XML 解析 | 清除已失效的库存规则和模板契约。 |
| 3 | 将 AI `Volunteer` 统一为 `GarrisonTransfer`，合并重复入口 | 先保留能力，再消除概念混用。 |
| 4 | 修正封邑部队显示与旧每周补员公式 | 保证 UI 和实际生产一致。 |
| 5 | 核实 UI 扩展、文本资源后清理 | 避免误删动态依赖。 |
| 6 | 完成全局类型重命名、跨 Mod 更新、存档字段迁移 | 风险最高，需集中处理兼容性。 |
| 7 | 编译、启动及新／旧存档回归测试 | 确保清理没有破坏实际生产与调拨。 |

### 验收清单

- [ ] 不再存在职业兵池／封建兵池的独立持久化 troop 库存。
- [ ] 不再存在向旧库存补兵或从旧库存招兵的有效路径。
- [ ] 无旧库存容量、封邑旧产速等无效配置影响现行生产。
- [ ] 职业兵继续通过每日清单进入驻军，并正确消耗基础兵源。
- [ ] 封邑兵继续进入实际封邑部队名册，受现行生产与编制限制。
- [ ] AI 仍可从驻军调兵，但不再以 `Volunteer`／`ProfessionalPool` 表述。
- [ ] 封邑菜单“每日补员”显示当前生产预测值。
- [ ] `ModifiedArmy` 与 `ModifiedPolitics` 编译、加载、Harmony 补丁正常。
- [ ] 新存档及目标旧存档能够读取，生产进度和周配额保持正确。

---

**范围说明：** 本文是基于指定 commit 的静态代码审查和讨论所得的**实施方案**，不是已完成的代码修改或运行测试报告。删除项中的“未发现调用”仍应在实际实施时结合最新仓库版本及编译结果复核。
