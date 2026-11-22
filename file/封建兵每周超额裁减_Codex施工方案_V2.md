# ModifiedArmy：AI 封建兵每周超额供养裁减 — Codex 施工方案 V2

> **直接交给 Codex 的任务书**。以当前 Git 仓库代码为权威依据施工；本文是方案，并不表示这些修改已经实现。**未经用户另行要求，不得 commit、push、创建 PR 或合并代码。**
>
> 本版新增：①日志仅针对玩家所在王国、避免刷屏；②重要参数由 `militaryConfig.xml` 提供；③保留此前确认的 120% 触发 / 105% 目标、仅 Ready+Cooling、低阶优先和同阶伤兵优先等规则。

## 0. 仓库基线与施工原则

- 仓库：`GuihuaZhou/bannerlord`
- 参考分支：`main`
- 核对时的提交：`07183a6544125c4e0e015c5652c0fde82b21bf8e`
- 模块：`ModifiedArmy`
- 开工前先核对工作区、当前 Git HEAD 与相应文件。如已有改动，先分析再施工，不覆盖既有工作。
- 只增加封建兵**每周超额清理**。不修改日常补员、XML 兵种权重、自动升级、征召与返乡处理的既有业务语义。
- 不涉及驻军（garrison）周检；不增加新的 CampaignBehavior。
- 优先小范围修改，避免系统性重构。

## 1. 已确定的业务规则（不可擅改）

| 项目 | 规定 |
|---|---|
| 触发周期 | 使用现有每周 Tick；每周检查一次 |
| 作用对象 | **AI 封邑**各附属村庄；玩家 Clan 自有封邑不自动裁减 |
| 判断单位 | 每个村庄 × 每个 `fiefPartyTemplates.xml` 供养分支 |
| 分支配额 | 复用现有 `GetSupplyBranchQuotas(template, doctrine)`，保持原 XML 权重 |
| 分支供养占用 | Ready + Serving + Cooling，按**具体士兵当前 Tier**的文化 Hearth 成本求和 |
| **触发阈值** | `Occupied > Quota × 120%`，严格大于 |
| **停止目标** | 一旦触发，清理到 `Occupied <= Quota × 105%` |
| 可以删除 | Ready 和 Cooling |
| 不允许删除 | Serving（服役记录与实际军队都不能动） |
| 最大删除人数 / 比例 | **不设置任何上限**，按照需要裁减 |
| 优先级 | **Tier 从低到高；同 Tier 优先伤兵** |
| 补员策略 | **仅现有 XML 默认权重**，不要新增模式 |
| 日志 | **仅输出玩家所在王国**有关封邑的本功能日志；正常无动作不输出；每个发生清理的分支输出聚合摘要 |
| 参数 | 至少 `Enabled`、`TriggerPercent`、`TargetPercent`、`LogEnabled` 由 XML 配置 |
| 存档 | 不新增持久化字段，兼容旧存档 |

### 1.1 用整数百分比实现阈值

默认 `TriggerPercent=120`、`TargetPercent=105`。

- 触发：`100L * occupied > (long)triggerPercent * quota`
- 目标：`100L * occupied <= (long)targetPercent * quota`
- 整数目标 Hearth：`targetHearth = (long)quota * targetPercent / 100`（向下取整）。
- 正常占用、配额与乘法均应采用 `long` 参与比较，避免 `int` 溢出；实际供养接口返回 `int` 时在外围做 checked/异常保护。
- `Quota==0`：只要 `Occupied>0` 即触发，目标 0，但仍禁止删除 Serving。
- 比如配额 100，120 不触发，121 触发；触发后裁减至不超过 105。
- 不是“超额一点删一点”，也不是“所有分支都控制在 105%”：只有突破 120% 的分支才执行清理。

## 2. 已核对的现有代码入口

### 2.1 周调度

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`

- `OnWeeklyTick()` 遍历 `_fiefDataMap.Values`，调用 `data.WeeklyUpdate()`。

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`

- `FiefPartyData.WeeklyUpdate()`：先 `PerformAutoUpgrade()`，后遍历 `ProcessVillageCooling(village, true, "WeeklyTick")`，然后 `ShowReadyNotification()`、`SyncManpowerCounters()`。
- 在 `ShowReadyNotification()` 之后、`SyncManpowerCounters()` 之前新增 `NormalizeWeeklyFeudalSurplus()`。这样刚到期的 Cooling 先按现有流程转为 Ready，再参与裁减。
- 保持 `IsRuntimeReady` 检查；不要另行订阅每周事件。

### 2.2 配额与占用

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`

复用：

- `GetFeudalBudgetHearth(doctrine)`
- `GetSupplyBranchQuotas(template, doctrine)`
- `GetOccupiedSupplyBranchHearth(template, doctrine, branch)`
- `GetReadyRoster()`
- `QueryFeudalTroops(template, states)`

配额本身已经由有效 Hearth、文化 `FiefWeight/ProfessionalWeight`、XML 分支权重分配，且是非存档化的日内计算快照。**不要另造配额账本，不要更改它的算法。**

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyTemplate.cs`

- `SupplyBranches`、`TryGetSupplyBranch(troop, out branch)`、`IsEnableTroop(troop)`。
- 必须沿原有升级树映射识别分支；无法唯一归属的兵种不要猜测后删除。

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefTroopQuery.cs`

- `FiefTroopState.Ready/Serving/Cooling`，聚合快照只读，不得修改快照来代替真实库存。

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefTroopRosterFilter.cs`

- `GetEntries(roster, template)`：枚举封建兵相关 Ready 名册条目。
- `TryGetCounts(roster, troop, out count, out wounded)`：获取实际总数、负伤数。

### 2.3 Cooling 数据

文件：`ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefReturnBatch.cs`

- `FiefReturnBatch.Troops`：`Dictionary<CharacterObject,int>`；`WaitWeeks` 存冷却周数。
- **Cooling 无伤兵字段**，不可凭空推断 Cooling 的 wounded 状态。
- 删除要真实修改 `ReturnBatches` 内批次的 `Troops` 字典；若兵种数量为零则移除键，批次为空则移除整个批次；不更改 `WaitWeeks`。

### 2.4 Ready 名册 API 语义

仓库中的 `code/TroopRoster.cs` 表明：

- `RemoveTroop(troop, n)` 只明确减少总数，伤员处理会受 `AddToCountsAtIndex` 的保护逻辑影响；**不能用它表达“精确优先删除伤员”**。
- 删除 `N` 名 Ready 伤兵：`roster.AddToCounts(troop, -N, false, -N, 0, true, -1)`。
- 删除 `N` 名 Ready 健康兵：`roster.AddToCounts(troop, -N, false, 0, 0, true, -1)`。
- 两种操作都必须以新鲜库存数据验证 `N` 的合法范围，并检查操作前后 `Number/WoundedNumber/Healthy` 三个计数。
- 不允许写入 `FiefPartyData._fiefParty`，它只是快照；**必须修改 `village.GetReadyRoster()`**。
- Ready 与原生村庄民兵共用实体名册，过滤兵种时禁止误删普通民兵、英雄、不可识别旧兵种。

## 3. 新增 XML 参数（本版重点）

### 3.1 配置位置

文件：`ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`

建议在根 `<MilitaryConfig id="default">` 下，作为 `AiRecruitment`、`HearthEconomy`、`Cultures` 的**同级节点**增加一次**全局**配置（不要分别给每种文化重复配置）：

```xml
<!-- AI 封建兵每周超额编制清理：
     超过 triggerPercent% 才执行，清理至 targetPercent%。
     保留在役兵；仅清理 Ready、Cooling。 -->
<WeeklyFeudalSurplusNormalization
    enabled="true"
    triggerPercent="120"
    targetPercent="105"
    logEnabled="true" />
```

配置字段：

| 字段 | 类型 | 默认值 | 含义 |
|---|---|---:|---|
| `enabled` | bool | `true`（随新版 XML 发布） | 总开关；关闭后不执行本功能 |
| `triggerPercent` | 正整数 | `120` | 严格超过多少百分比才开始裁减 |
| `targetPercent` | 正整数 | `105` | 清理后的最大目标百分比 |
| `logEnabled` | bool | `true` | 是否输出本功能聚合日志；**不允许扩大到玩家王国外** |

校验规则：

1. `enabled`、`logEnabled` 使用严格的 `bool.TryParse`；不接受静默解析失败。
2. `triggerPercent`、`targetPercent` 使用不受操作系统小数点影响的整数解析。
3. 必须满足 `0 < targetPercent < triggerPercent`；否则加载时抛出说明字段名称的配置错误，不静默纠正、不进入周检再报错。
4. 不添加“每周最多删除人数”“比例上限”“补员模式”等字段。
5. 推荐 XML 节点整体缺失时**禁用新功能**，兼容用户仍使用旧版自定义 XML 的情况；新版发行 XML 明确启用并设默认数值。若项目现行配置加载策略要求节点必填，需说明兼容方案并以不会误触发旧配置为优先。
6. 参数只作为当前配置加载时的内存数据，**不写入存档字段**。

### 3.2 对应 C# 解析

文件：`ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`

当前 `MilitaryConfig : MBObjectBase` 在 `Deserialize(MBObjectManager, XmlNode)` 内调用 `ParseAi`、`ParseHearthEconomy`、`ParseDoctrine`；`MilitaryConfigManager.Instance.RequireActiveConfig()` 用来获得活动配置。

新增紧凑的 `WeeklyFeudalSurplusNormalizationConfig`（名称可依项目规范调整），四个属性：`Enabled`、`TriggerPercent`、`TargetPercent`、`LogEnabled`。

- 在 `MilitaryConfig` 添加配置属性。
- 在 `Deserialize` 增加对应 XML 节点解析与校验。
- 复用现有 `Attr`、`PositiveInt` 和 `CultureInfo.InvariantCulture` 风格；bool 可加独立小 helper。
- 不写到 `CulturalMilitaryDoctrine`，因为本需求是全文化统一的维护策略，而不是某一文化的军事制度。
- `WeeklyUpdate` 直接读取 `MilitaryConfigManager.Instance.RequireActiveConfig().WeeklyFeudalSurplusNormalization`。
- 不增加 MCN/UI 配置，不需要新的存档数据模型。

## 4. 玩家所在王国日志过滤（本版重点）

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Diagnostics/RecruitmentLogFilter.cs`

仓库已有：

```csharp
public static bool ShouldLog(Clan clan)
{
    Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
    return playerKingdom != null &&
        clan?.Kingdom == playerKingdom;
}
```

**必须复用此函数**，而不是比较 `OwnerClan == Clan.PlayerClan`，否则漏掉玩家王国其他 AI 贵族的封邑。

本功能日志的统一门禁：

```csharp
bool shouldLog = config.LogEnabled &&
    RecruitmentLogFilter.ShouldLog(_settlement?.OwnerClan);
```

规则：

1. **只输出玩家当前所在 Kingdom 的封邑日志**；其他王国一律静默，包含本功能新引入的普通调试、警告、异常摘要。
2. 玩家未加入王国（`Clan.PlayerClan?.Kingdom == null`）时默认无本功能日志，不因为“玩家 Clan 自有封邑”例外打印（符合“仅玩家所在王国”）。
3. 本功能静态/代码级开关 `logEnabled=false` 时完全不输出本功能日志。
4. **无裁减动作**不打印：普通的 `occupied <= trigger` 检查静默，不按村/按兵种逐条刷屏。
5. 正常真正删除了士兵后，每个**村庄 × 分支 × 周检**最多一条聚合摘要。禁止逐个士兵 `Info/Debug`。
6. 异常或未达标且需要诊断时，在该周检对应分支最多一条聚合日志；同类异常可在封邑级汇总。不要因几千个 AI 村庄的数据错误刷屏。
7. 尽量使用现有 `ModLogger.FileWarn` / `FileError` 等符合项目日志风格的方法，但务必先做上面的 `shouldLog` 门禁。
8. **仅过滤本次新增功能的日志，不得全局重写其他系统的日志策略**。XML 配置加载错误属于系统启动配置错误，按既有加载规则报告，不视为某个 AI 封邑运行日志。

建议聚合字段：

- `fiefId`, `villageId`, `branchId`, `quotaHearth`
- `occupiedBefore`, `triggerPercent`, `targetPercent`, `occupiedAfter`
- `readyWoundedRemoved`, `readyHealthyRemoved`, `coolingRemoved`
- `removedTotal`, `result`（`Normalized` / `RemainingOverQuota` / `NoEligibleTroops` / `SkippedInvalidData` / `PostconditionMismatch`）

## 5. 算法与优先级

新增：`private int NormalizeWeeklyFeudalSurplus()`，建议放在 `FiefPartyReinforcement.cs`，供 `WeeklyUpdate()` 调用。

### 5.1 范围与检查

- AI 封邑：`_settlement?.OwnerClan != null` 且不等于 `Clan.PlayerClan`。
- 运行时未准备好、模板缺失、文化制度缺失、归属不一致：安全跳过。
- 村庄 `RecoveryRequired == true`：跳过该村庄。
- 遍历 `OrderedVillages()`，每个村庄通过已有 `GetSupplyBranchQuotas` 取得完整分支额度。
- 对每个分支使用已有 `GetOccupiedSupplyBranchHearth`，它统计 Ready + Serving + Cooling。
- 仅 `100L*occupied > triggerPercent*quota` 才进入删除流程。
- 删除到 `100L*occupied <= targetPercent*quota` 或 Ready/Cooling 无合法候选即停止。
- 任何涉及 `int` 计数或供养成本乘法的环节需要边界保护；异常隔离到当前村庄/分支，不能让一处错误中断整次世界周检。

### 5.2 候选排序

同一分支内部，对 `Ready` 和 `Cooling` 生成不可变候选快照，按以下规则：

1. **Tier 升序**：T1、T2、T3……；等级优先级高于伤兵优先级。
2. **同 Tier：Ready 伤兵优先。**
3. **同 Tier：Cooling 其次**（因为没有保存伤兵信息）。
4. **同 Tier：Ready 健康兵最后。**
5. 同类候选用 `CharacterObject.StringId`、Cool batch 的稳定索引做确定性排序。

这样既满足“低阶优先”，也满足“同阶伤兵优先”。

### 5.3 实际删除

对于一个候选类型，计算：

```text
remainingHearth = max(0, occupied - targetHearth)
neededCount = ceil(remainingHearth / costPerTroop)
actualRemove = min(neededCount, availableCount)
```

- `costPerTroop` 由 `doctrine.GetFeudalSupplyCost(candidate.Troop)` 返回，必须大于 0。
- 每个候选**批量**减少对应库存，无需一兵一循环；删除后检查真实库存变化，再按实际删除数更新占用。
- 无法验证库存变化时停止当前分支并记录（只限玩家王国）异常，不能继续盲删。
- 每次更新都核对操作没有删除当前分支之外的士兵。
- 如果 `Serving` 供养独自超过目标，Ready/Cooling 删完后仍超标是预期的 `RemainingOverQuota`，不得碰 Serving。

### 5.4 Ready 的真实删兵

- 数据必须来自 `village.GetReadyRoster()`，不允许修改 `FiefPartyData._fiefParty` 快照。
- 首先用 `FiefTroopRosterFilter.GetEntries` 和 `TryGetCounts` 取得实际库存。
- 删除 `N` 名伤兵：`roster.AddToCounts(troop, -N, false, -N, 0, true, -1)`。
- 删除 `N` 名健康兵：`roster.AddToCounts(troop, -N, false, 0, 0, true, -1)`。
- 写入前后重新核实 `total`、`wounded`、`healthy`；`N` 不得超过目标类别实际人数。
- 禁止删除英雄、原生普通民兵、未启用模板兵种、模糊分支兵种。
- 快照枚举，避免修改 live roster 时使枚举器失效。

### 5.5 Cooling 的真实删兵

- 使用 `VillageFiefData.ReturnBatches` 实际批次，修改 `FiefReturnBatch.Troops`，而非聚合快照。
- 在批次的合法数值范围内扣减字典数量；扣到 0 移除键，空批次从列表移除。
- 保留其他兵种、其他批次、`WaitWeeks`、`ServiceBatches`、`MobilizedHearth`。
- 不能因为冷却批次缺伤兵字段就给它添加一个没有需求的持久化伤兵字段。
- 若先枚举后修改，按快照或索引倒序执行，避免集合修改异常。
- 对于某个无效批次，保守跳过并记录玩家王国可见的聚合异常，不能擅自修复全部旧存档坏数据。

### 5.6 Ready 名册临时不可用

现有 `GetReadyRoster()` 可能因村庄民兵 Party 未激活而返回 `null`，此时 `QueryFeudalTroops` 无法统计真实 Ready 库存。

- 不得把这时算到的 `occupied` 当作完整三状态占用而声明“已正常归一化”。
- 如果采用**安全下界**策略，只能在已知 `Serving+Cooling` 占用本身就超过触发阈值时考虑清理明确有效的 Cooling 批次；结束时标记为 `PartialUnknownReady`，不能声称达到全量 105% 目标。
- 如果无法严谨实现下界策略，则直接跳过该村庄，待下周 Ready 名册恢复后处理；**安全性优先于本周一定裁减**。
- Codex 应在交付报告说明具体选择及覆盖测试。

## 6. 关键不变量

**不允许修改**：

- `ServiceBatches`、Serving 记录、其真实领主队伍成员名册；
- `MobilizedHearth`、真实村庄 Hearth、繁荣度；
- `fiefPartyTemplates.xml` 权重、文化制度份额；
- 每日补员速率、自动升级概率、AI 领主征召方式；
- 旧兼容 `FiefTroopDetachment` 序列化字段；
- 原生村庄民兵与玩家封邑；
- 任何新 Saveable 字段或新存档类型。

**必须保证**：

1. 被清理分支的占用不增加；不会跨分支删除。
2. `ReadyRemoved + CoolingRemoved == TotalRemoved`，且与各自库存变化一致。
3. 伤兵优先时，不能错误删除健康兵。
4. Serving 数据、`MobilizedHearth` 不变。
5. 已到期 Cooling 转入 Ready 的流程正常，之后不会再凭旧批次重复生兵。
6. 同样状态下连续运行两次，第一次归一化成功后第二次不再删兵。
7. 未超过 `TriggerPercent` 的分支完全不变。
8. 只会从玩家所在王国输出新增诊断日志。

## 7. 推荐施工文件清单

| 文件 | 处理方式 |
|---|---|
| `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs` | **必须修改**：每周清理入口、候选、真实库存删除及清理摘要 |
| `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs` | **必须修改**：全局配置类型、XML 读取、校验 |
| `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` | **必须修改**：增加全局配置节点及注释 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefReturnBatch.cs` | **按需修改**：如要封装安全批次删兵 helper，但不能增加存档字段 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Diagnostics/RecruitmentLogFilter.cs` | **只读复用**：已有 `ShouldLog(Clan)`，不得另造新过滤器 |
| `FiefPartyManager.cs`、`VillageFiefData.cs`、`FiefPartyTemplate.cs`、`FiefTroopQuery.cs` | 优先不修改，复用其现有行为 |

不要修改 `GarrisonRecruitFromPrisonersBehavior.cs`，不要新增封建兵补员模式，不要新写额外供养配额算法。

## 8. 建议施工步骤

1. **静态核对**：确认仓库本地版本、当前 WeeklyUpdate、供养/归属查询和日志过滤的签名。
2. **配置**：添加全局 XML 节点、配置模型、严格的解析与约束；编译/静态校验。
3. **只读计算**：实现 AI 范围门槛、单村分支触发/目标判断，并确保无副作用。
4. **真实删除**：实现 Ready 伤/健删兵与 Cooling 批次删兵，执行后验证计数。
5. **每周接入**：插入 `WeeklyUpdate` 既有冷却处理之后、计数汇总之前。
6. **日志控制**：`config.LogEnabled && RecruitmentLogFilter.ShouldLog(_settlement?.OwnerClan)`；只在有删除或可诊断异常时输出聚合日志。
7. **编译与测试**：执行有条件的编译、静态检查、模拟/游戏验收；如没有游戏 DLL，明确写出未执行的项目。
8. **交付说明**：给出实际改动文件、关键逻辑、配置与测试结果，不创建 PR、不自动提交。

## 9. 验收用例（至少）

### 9.1 触发、停止与 XML 改值

- 配额 100，配置 120/105：占用 100、105、110、119、120 全部不删；121、130、200 触发并尝试裁到 ≤105。
- 配额 1、配额 0、非整百配额：验证整数比较/向下取整正确。
- 将 XML 改为 `triggerPercent=130`、`targetPercent=110`：不改 C# 即能按新比例工作。
- `enabled=false`：完全不删。
- `targetPercent>=triggerPercent` 或非法数字/bool：启动加载时清晰报错，不能隐式兜底。
- 旧 XML 未含新增节点：按定义禁用新功能，不因缺节点使已有军事配置加载失败。

### 9.2 优先级与库存准确性

同一个分支同时存在 T1 Ready 伤、T1 Cooling、T1 Ready 健康、T2 Ready 伤、T3 Ready 健康：

- 顺序必须是 T1 Ready 伤 → T1 Cooling → T1 Ready 健康 → T2 Ready 伤 → T3 Ready 健康。
- 伤兵删除前后 `WoundedNumber` 精确变化，健康人数保持不变。
- Cooling 批次部分/全部裁减后保存重读，不恢复已删除士兵。
- `ServiceBatches` 原样不变。
- 普通民兵、英雄、其他分支的数量不变。

### 9.3 无法达到目标

- 配额 100；Serving 占用 120，Ready=10、Cooling=20：删除可用部分后，Serving 仍保留，正常退出并记录 `RemainingOverQuota`（仅玩家王国）。
- 只有 Serving 超额，没有 Ready/Cooling：不删 Serving、不死循环。
- Ready 名册未激活：不作虚假的全量占用判断；按安全跳过或明确实现的下界 Cooling-only 策略运行。

### 9.4 日志过滤与避免刷屏

- 玩家加入王国 A：A 的 AI 封邑发生裁减，可以输出**每村庄每分支每次周检最多一条**摘要。
- 同时王国 B/C 的 AI 封邑发生裁减：依然正常执行，但**本功能不得输出 B/C 的任何封邑日志**。
- 玩家无王国：本功能不输出任何阵营日志。
- `logEnabled=false`：不输出本功能日志，但正常执行裁减。
- 同周有数千个没有超额的村庄：不应有周检逐条日志。
- 无有效候选、异常/未达标：同一分支/同次检查最多一条聚合诊断。
- 不影响模块其他功能既有日志的过滤策略。

### 9.5 存档与回归

- 旧存档中历史超额的封建兵可以逐周纠正。
- 老 Cooling 批次无需新字段即可读写。
- 多村庄、多个供养分支、供养配额为零、旧 XML 分支匹配模糊。
- 每日正常生产、每周升级、服役返乡、原生民兵、驻军与 AI 征召回归不受影响。

## 10. Codex 最终反馈格式

完成施工后汇报：

1. 最终代码基准和修改文件清单。
2. XML 配置最终结构及配置合法性校验。
3. 触发 120% / 目标 105% 的准确整数判断及测试结果。
4. Ready 伤兵、Cooling 删除的 API 语义与账实验证。
5. “仅玩家所在王国日志”的实际过滤调用和日志示例。
6. Serving、Hearth、MobilizedHearth、普通民兵不被误改的验证。
7. 编译、单测、实机测试分别是否完成；未测试不得写“通过”。
8. 剩余风险和必要的人工实机测试步骤。

**禁止自动执行 Git commit / push / 创建 PR / 合并；仅在用户显式后续授权时执行这些操作。**
