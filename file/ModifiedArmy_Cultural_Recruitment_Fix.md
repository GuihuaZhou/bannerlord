# ModifiedArmy：异文化统治下职业兵训练与兵种开放规则修复——设计及实现方案

> 状态：设计确认，待代码实现；**未修改 GitHub 仓库**。  
> 仓库：`GuihuaZhou/bannerlord`；核对基线：`main` 最新提交 `fecc9264df9c5d27d7a13cba3be7a1511de52efc`（`feat(army): revise daily troop replenishment`，2026-10-17 检索）。  
> 参考：《Bannerlord_异文化统治兵源与职业兵生成设计.md》；以下以用户对该旧文档的最新修订为准。  
> 定位方式：全部使用**仓库相对路径 + 类 / 方法 / 属性**，不以源码行号定位。

## 一、范围与优先级

本次只修复**异文化统治对职业兵训练速度、可生成兵种的限制及对应 UI 文案**。

- **职业兵文化始终是 `Settlement.Culture`**；封邑兵也始终依据 `Settlement.Culture`。旧文档中 `A/B/B` 生成 B、`A/B/C` 生成 C 文化职业兵的规定**已废弃**。
- 统治 `OwnerClan.Culture` 与 `OwnerClan.Kingdom.Culture` 仅决定：①职业兵**文化效率**，②当前**允许开放的兵种所需兵营等级**。
- **实际兵营等级**只由定居点真实建筑决定，并完整参与训练速度计算；不能再因文化关系被截断为 1 或 2 级。
- UI 保持简洁，**不新增“职业兵文化”“开放兵种等级”字段**。现有“兵营加成（等级）”“文化效率”足够解释训练速度。
- 本次不改变 `militaryConfig.xml` 的七文化 `Professional/Training` 数值、`MilitaryDoctrine` 权重、封邑每日补员、共享基础兵源分配、AI 驻军预算及招募规则。
- **封邑每日补员、预计/实际补员统计已在最新 GitHub 提交中实现**；本轮只做兼容核对，不重复改造。

## 二、最终文化关系规则

设 `S = Settlement.Culture`、`C = OwnerClan.Culture`、`K = OwnerClan.Kingdom?.Culture ?? C`、`L = 实际兵营等级`。

| 文化关系（S / C / K） | 职业兵文化 | 文化训练效率 | 文化允许的兵种等级 | 备注 |
|---|---|---:|---|---|
| A/A/A | A | 100% | 1–3 级 | 完全一致 |
| A/A/B | A | 100% | 1–2 级 | Clan 与定居点一致 |
| A/B/A | A | 50% | 1–2 级 | 王国与定居点一致 |
| A/B/B | A | 50% | 1–2 级 | Clan、王国一致，定居点异文化 |
| A/B/C | A | 25% | 仅 1 级 | 三种文化两两不同 |

**不属于任何王国的 Clan：取 `K = C`**。因此无王国 Clan 控制同文化定居点等价于 A/A/A，控制异文化定居点等价于 A/B/B；不能因 `ownerClan.Kingdom == null` 而直接停止职业兵生产。

### 两种“等级”严格分离

- `BuildingBarracksLevel = GetBarracksLevel(settlement)`：真实建筑等级（0–3），用于**训练速度、UI 兵营加成**。
- `CultureRecruitmentLevelLimit`：表中由 S/C/K 判定的 1、2、3，**只限制兵种候选**。
- `AllowedRecruitmentLevel = min(BuildingBarracksLevel, CultureRecruitmentLevelLimit)`：用于筛选 `TroopProductionEntry.RequiredBarracksLevel`；**不用于训练速度公式**。
- 0 级建筑时可以仅生产原模板中 `RequiredBarracksLevel <= 0` 的候选；不要因“文化开放 1 级”凭空开放未建造建筑的兵种。

### 训练计算

令 `B = Professional.Training.Base`、`Step = Professional.Training.BarracksStep`、`E = 文化效率`，则本 Mod 基础值为：

`RawTraining = max(0, B + Step × BuildingBarracksLevel) × E`

`FinalTraining = GetDailyProduction(settlement, Professional)` 返回值；如加载 `ModifiedPolitics`，该值仍由它现有 Harmony Postfix 乘以官职修正。**保持 `GetDailyProduction(Settlement, TroopProductionKind)` 方法签名不变。**

训练速度配置始终使用**定居点文化 `S`** 对应的 `CulturalMilitaryDoctrine.GetTraining(settlement)`；职业兵模板也始终使用 `S`。

> 示例：帝国三级城堡由阿塞莱 Clan + 阿塞莱 Kingdom 统治（A/B/B）。按当前帝国城堡配置 `base=1.45`、`barracksStep=0.85`：原来错误的 `(1.45+0.85×1)×0.5=1.15/天`，修改后 `(1.45+0.85×3)×0.5=2.00/天`（无额外官职修正）。**可生成兵种仅限所需兵营等级 ≤2**，不是“只允许 1 级”。

同一场景的帝国三级城镇：`(1.30 + 0.90×3)×0.5 = 2.00/天`。

## 三、当前仓库已经确认的实现偏差

核心代码：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`。

1. `GetDailyProduction()` 把实际建筑等级 `Math.Min(barracksLevel, professionalRule.MaximumBarracksLevel)` 后用于 `GetDailyProduction()`，错误地把“高级兵种招募资格惩罚”变成“建筑训练能力惩罚”，再叠加文化效率。
2. `OnDailySettlementTick()` 构建候选时传入 `forecast.EffectiveBarracksLevel`；当前这个字段同样是 `min(实际等级, 文化上限)`，**候选筛选现阶段实际上已经受到文化限制**。修改时必须继续保留这个筛选功能，不可简单让该字段等于实际等级而放开高级兵种。
3. `ResolveProfessionalProductionRule()` 在 `S != C` 且 `C == K` 的分支给出中速但允许等级 **1**，与最终确定的 A/B/B “中速 + 1–2 级”不一致。
4. `ResolveProfessionalProductionRule()` 对没有 Kingdom 的 Clan 返回 `null`，导致训练为 0，而设计明确要求 `K=C`。
5. `GetProductionForecast()` 的 `RawProfessionalTrainingBeforeExternalEffects` 及 `ExternalTrainingBonusRate` 依赖截断后的等级；主生产公式修好后，此处必须同步，避免显示的“额外加成”不准确。
6. 中文本地化键 `ModifiedArmy_ReinforcementRate_Description` 仍使用“兵营加成（有效{BARRACKS_LEVEL}级）”，会继续误导玩家。

> 上述是 `main` 在所列基线下的代码审计。现有 `TroopProductionTemplateRepository.Resolve(settlement, kind)` 默认选择定居点文化，此行为**符合最新设计，不应改为按统治 Clan 或 Kingdom 选模板**。

## 四、最小侵入实现方案

### 4.1 专业兵生产规则：改为两种互不混用的输入

**文件**：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`  
**私有类**：`ProfessionalProductionRule`；**方法**：`ResolveProfessionalProductionRule()`。

- 将原属性 `MaximumBarracksLevel` 的含义明确为 `MaximumRecruitableBarracksLevel`（可重命名；该私有类非存档结构）。
- 保留 `Culture` 为 `settlement.Culture`；保留 `ProductionRate` 作为速度惩罚（1.0/0.5/0.25）。
- 文化决策不再改变实际建筑等级；只产生 `ProductionRate` 与 `MaximumRecruitableBarracksLevel`。
- `OwnerClan == null`、`OwnerClan.IsEliminated`、`settlement.Culture == null` 等非法状态仍可按当前约定禁止生产。
- `OwnerClan.Kingdom == null` 时 `kingdomCulture = clanCulture`；如果 `Kingdom` 存在但文化数据异常，应采用明确的日志/失败处理，不要默默错误匹配。
- 注意 A/B/B 的开放等级改为 2；A/B/C 才限制为 1。

推荐独立判定伪代码（只展示核心分支）：

```csharp
CultureObject s = settlement.Culture;
CultureObject c = settlement.OwnerClan.Culture;
CultureObject k = settlement.OwnerClan.Kingdom?.Culture ?? c;

bool sEqualsC = CulturesMatch(s, c);
bool sEqualsK = CulturesMatch(s, k);
bool cEqualsK = CulturesMatch(c, k);

float efficiency;
int maximumRecruitableBarracksLevel;
if (sEqualsC)
{
    efficiency = 1f;
    maximumRecruitableBarracksLevel = cEqualsK ? 3 : 2;
}
else if (sEqualsK || cEqualsK)
{
    efficiency = 0.5f;
    maximumRecruitableBarracksLevel = 2;
}
else
{
    efficiency = 0.25f;
    maximumRecruitableBarracksLevel = 1;
}

return new ProfessionalProductionRule(
    s, efficiency, maximumRecruitableBarracksLevel);
```

这段设计直接覆盖五种情况。若在现有代码中保留 `MaximumBarracksLevel` 属性名，也必须明确它**仅表示文化开放的兵种等级上限**。

### 4.2 `GetDailyProduction()`：速度不再截断建筑等级

- 保持签名、公共访问方式与 ModPolitics Harmony Patch 的调用兼容。
- `int actualBarracksLevel = GetBarracksLevel(settlement)`，**不要**执行 `Math.Min(actualBarracksLevel, rule.MaximumRecruitableBarracksLevel)`。
- `doctrine = MilitaryConfigManager.Instance.RequireActiveConfig().GetDoctrine(settlement.Culture)`。
- 只对最终基础训练值乘一次 `rule.ProductionRate`。外部官职倍增仍由 `ModifiedPolitics` 负责。

核心表达：

```csharp
int actualBarracksLevel = GetBarracksLevel(settlement);
ProfessionalProductionRule rule = ResolveProfessionalProductionRule(settlement);
if (rule == null) return 0f;

CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
    .RequireActiveConfig()
    .GetDoctrine(settlement.Culture);

return doctrine.GetTraining(settlement)
    .GetDailyProduction(actualBarracksLevel) * rule.ProductionRate;
```

### 4.3 `GetProductionForecast()`：拆开建筑等级和候选等级

**文件**：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs`（不持久化的 UI/生产预测 DTO）。

推荐将目前混用的 `EffectiveBarracksLevel` 拆为：

- `BarracksLevel`：**实际建筑等级**，用于 UI、兵营加成、`RawProfessionalTrainingBeforeExternalEffects`。
- `AllowedRecruitmentLevel`：`min(实际建筑等级, 文化开放兵种上限)`，只给当日候选生成使用，**不显示在 UI 中**。

或为了减少字段重命名的调用影响，保留 `EffectiveBarracksLevel` 仅作为**候选兵种等级**，新增 `BarracksLevel` 单独用于速度和 UI。**不要直接把旧 `EffectiveBarracksLevel` 改成实际等级后继续传给候选生成方法**，那会放开不应招募的高级兵种。

核心一致性约束：

```csharp
int actualBarracks = GetBarracksLevel(settlement);
int eligibleBarracks = rule == null ? 0 : Math.Min(
    actualBarracks, rule.MaximumRecruitableBarracksLevel);

float rawTraining = training.GetDailyProduction(actualBarracks)
    * (rule?.ProductionRate ?? 0f);

// GetDailyProduction 仍是包含现有外部 Harmony 加成的权威返回值。
float finalTraining = GetDailyProduction(
    settlement, TroopProductionKind.Professional);

float externalBonusRate = rawTraining > 0.0001f
    ? Math.Max(0f, finalTraining / rawTraining - 1f)
    : 0f;
```

`ProfessionalTrainingCapacity`、`ProfessionalAllocation`、共享兵源 `SharedSupply`、`FiefAllocation` 仍使用既有算法。不要因为文化修复而改变共享兵源分配比例或封邑生产逻辑。

### 4.4 `OnDailySettlementTick()` / `BuildDailyProfessionalOffers()`：兵种筛选继续按文化开放等级

- 模板继续用 `TroopProductionTemplateRepository.Instance.Resolve(settlement, TroopProductionKind.Professional)`；其无文化显式重载默认选择 `settlement.Culture`，正确。
- 从预测结果读取 `AllowedRecruitmentLevel`（或继续使用表示候选等级的旧 `EffectiveBarracksLevel`），传给 `BuildDailyProfessionalOffers()`。
- `BuildDailyProfessionalOffers()` 的参数名建议为 `allowedRecruitmentLevel`；保持条件 `entry.RequiredBarracksLevel <= allowedRecruitmentLevel`。
- 不调整最新提交已经实现的加权轮转、职业兵候选天数戳或失败进度返还逻辑。
- 若候选为空，沿用现有候选差额返还；注意持久化小数生产进度的上限与下一次结算行为一致，不能引入重复补兵。

### 4.5 UI：不增加字段，只更正解释及取值

**文件**：

- `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs`，方法 `InitializeReinforcementRateMenu()`；
- `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml`，字符串 `ModifiedArmy_ReinforcementRate_Description`。

现有 UI 展示项目保持不变：基础兵源、基础训练、兵营加成、文化效率、额外加成（含官职）、训练能力、预计补员、实际补员、自动招募，以及封邑兵对应信息。

仅修改以下内容：

1. `BARRACKS_LEVEL` 绑定**真实** `forecast.BarracksLevel`。
2. `BARRACKS_BONUS = forecast.ProfessionalTrainingStep * forecast.BarracksLevel`，不使用文化开放等级。
3. 中文文案由 **“兵营加成（有效{BARRACKS_LEVEL}级）”** 改为 **“兵营加成（{BARRACKS_LEVEL}级）”**（或“兵营加成（实际{BARRACKS_LEVEL}级）”）。
4. `CULTURE_EFFICIENCY` 继续显示 100%、50%、25%；不要把该惩罚藏在兵营等级里。
5. `TRAINING` 使用包含既有外部官职修正的 `ProfessionalTrainingCapacity`；`EXTERNAL_BONUS` 基于**同一实际兵营等级**计算的 `rawTraining` 比值。不得重复计算 Harmony 加成。
6. **不新增**“职业兵文化”“开放招募等级”“建筑上限”等 UI 行；仍保留当前“实际补员”统计字段。

**UI 期望示例：**帝国三级城堡、阿塞莱 Clan + 阿塞莱 Kingdom 统治、无官职加成：

```
职业兵
基础训练：1.45 /天
兵营加成（3级）：+2.55 /天
文化效率：50%
额外加成（含官职）：0%
训练能力：2.00 /天
预计补员：由共享兵源/招募开关等计算
实际补员：读取最近一次真实入伍记录
```

不展示任何额外的文化或兵种开放等级说明。

### 4.6 与 ModifiedPolitics、存档及旧兵种兼容

**文件**：`ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs`。

- 保留其对 `SettlementManpowerProductionBehavior.GetDailyProduction()` 的 Harmony Postfix；**不改变方法名、参数或返回类型**。
- `GetDailyProduction()` 内部先完成实际建筑等级与文化效率计算，Postfix 再加官职增益；UI 用同一原始公式显示“额外加成”，不再调用第二次增益。
- 不添加新的 Saveable 字段，不迁移已有存档；`RecruitmentProductionForecast` 是临时数据 DTO，内部字段变更不触发存档结构迁移。
- 保留 `SettlementManpowerProductionData` 现有实际补员记录及历史已弃用的周度存档字段，不修改保存编号。
- 文化关系变化只影响**后续每日生成**，不删除、不强制转换已有驻军或仍有效的职业兵候选；封邑兵始终由定居点文化决定且不受本次修复影响。

## 五、可直接执行的修改清单

| 路径（仓库相对） | 类 / 方法或字段 | 操作 | 目的 |
|---|---|---|---|
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | `ProfessionalProductionRule.MaximumBarracksLevel` | 明确其为候选兵种开放上限，可重命名 | 防止与建筑训练等级混淆 |
| 同上 | `ResolveProfessionalProductionRule()` | 修正 A/B/B 允许等级为 2；无 Kingdom 时 `K=C` | 符合最终五分支规则 |
| 同上 | `GetDailyProduction()` | 用真实建筑等级计算速度，仅乘一次文化效率 | 移除对兵营加成的额外等级惩罚 |
| 同上 | `GetProductionForecast()` | 实际建筑等级用于原始产能及 UI；文化上限仅用于候选资格 | 预测、UI、实际生产统一 |
| 同上 | `OnDailySettlementTick()` | 传入候选允许等级，继续按 `Settlement.Culture` 取得模板 | 不允许异文化提前生产高级兵种 |
| 同上 | `BuildDailyProfessionalOffers()` | 参数/筛选语义明确为候选等级 | 防止回归 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs` | `EffectiveBarracksLevel` / 新增字段 | 拆分真实等级与候选等级（非存档 DTO） | 避免一个字段两种含义 |
| `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs` | `InitializeReinforcementRateMenu()` | 绑定实际建筑等级，修正兵营加成与外部增益口径 | UI 准确且不增加字段 |
| `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml` | `ModifiedArmy_ReinforcementRate_Description` | 删除“有效X级”的误导表述 | 文案与机制一致 |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MilitaryOfficeRecruitmentPatches.cs` | `IncreaseRegularProduction()` | 保持接口并执行联调，无需默认改动 | 兼容官职加成 |
| `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml` | `Professional/Training` | **不需改动** | 沿用七文化现有训练速率 |

## 六、测试与验收

### 6.1 五种文化关系：实际建筑三级

| 关系 | 实际兵营 | 训练倍率 | 可生成的 `RequiredBarracksLevel` | 职业兵文化 |
|---|---:|---:|---|---|
| A/A/A | 3 | 100% | ≤3 | A |
| A/A/B | 3 | 100% | ≤2 | A |
| A/B/A | 3 | 50% | ≤2 | A |
| A/B/B | 3 | 50% | ≤2 | A |
| A/B/C | 3 | 25% | ≤1 | A |

断言：**所有五组训练速率中兵营 Step 都按实际三级计算**；可生成兵种则按单独文化等级上限筛选。

### 6.2 边界与动态变化

- 同文化、异王国（A/A/B）：100% 训练速率，但不产生三级兵种。
- 阿塞莱 Clan/Kingdom 控制帝国三级城堡（A/B/B）：职业兵依然是帝国兵种，训练能力由 1.15 → 2.00 /天（无官职），可生成 ≤2 级的帝国兵种。
- 三种文化互不相同（A/B/C）：速度 25%，但三级建筑仍贡献 3 级的训练加成；兵种仅允许一级。
- Clan 不属于 Kingdom：若 `S=C` 视作 A/A/A；若 `S!=C` 视作 A/B/B；不会因无 Kingdom 直接训练 0。
- 实际建筑 0/1/2/3 级分别检查：不允许任何 `RequiredBarracksLevel > 实际等级` 的候选。
- 城镇、城堡、港口城镇都按已有 `GetTraining(settlement)` 的类型覆盖/回退规则选配置。
- 易主、换国、退出王国后**次日**按最新关系重新判定；已入驻军或已有有效候选不强制换文化、清空或回溯处理。
- `ModifiedPolitics` 官职启用/禁用：训练速度增加仅一次，UI“额外加成”准确匹配最终 `GetDailyProduction()`；驻军每日招募上限的独立官职补丁仍正常。
- 原封邑每日补员、两兵种共享基础兵源、职业兵实际补员统计、进度累计及保存/读取均无回归。
- 中文菜单始终显示**实际建筑等级**，文化效率单独展示；不新增用户拒绝的文化与兵种等级 UI 项目。

### 6.3 建议日志（仅调试日志，不是 UI 新字段）

每次职业兵生产时可按开关输出：`SettlementId`、`SettlementCulture`、`ClanCulture`、`KingdomCulture`（独立者取 Clan）、`BuildingBarracksLevel`、`CultureRecruitmentLevelLimit`、`AllowedRecruitmentLevel`、`CultureEfficiency`、`RawTraining`、`FinalTraining`、`OfferCount`。用于快速定位“训练能力 > 0 但实际补员 = 0”，不影响正常菜单显示。

## 七、实施顺序

1. **先改规则判定**：修正 A/B/B 和独立 Clan 的文化分支；不改变产量。
2. **拆开等级**：预测 DTO 同时保存真实建筑等级与候选开放等级。
3. **改训练速度**：真实建筑等级参与 `GetDailyProduction()` 和 `rawTraining`，只叠加文化效率与已有官职加成。
4. **核验候选筛选**：`BuildDailyProfessionalOffers()` 继续使用候选开放等级，并仍按定居点文化取模板。
5. **修 UI 和中文字符串**：仅改“兵营加成”的等级取值与显示；不加字段。
6. **完整回归**：五种文化组合、独立 Clan、三个建筑类型、官职加成、旧存档、每日生产/实际补员结果。

**验收原则：**文化关系只限制兵种开放层级和文化训练效率；真实建筑等级完整决定兵营加成；职业兵与封邑兵文化均由定居点决定；UI 简洁且与生产计算完全一致。
