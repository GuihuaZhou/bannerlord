# ModifiedArmy：AI Party / 驻军兵种与质量比例软约束改造——设计及实现方案

> **方案状态**：已确认的设计方案；**尚未实施代码修改**。  
> **代码基准**：`GuihuaZhou/bannerlord`，`main` 分支提交 `0236335b6bed2cf88b75f7fab2df63a6e1dc7f24`（`fix(army): skip saturated recruitment categories`）。  
> **交付对象**：ChatGPT Work / 开发实施人员。  
> **定位习惯**：以仓库相对路径、类名、方法名定位，不使用行号。  
> **范围**：`ModifiedArmy` 的 AI Party 与驻军统一招募模型、职业兵生产、俘虏留用、部队解散并入驻军、驻军整编。**不修改**职业兵训练速率、文化兵种开放、封邑兵每日补员、军事制度权重。  
> **显示要求**：不增加任何游戏 UI 异常提示或诊断字段；必要的排查信息使用 `ModLogger`。

## 一、设计决策

1. **兵种结构是招募偏好，不是招募资格**：优先补足缺少的步兵、射手、骑兵、骑射手；所需兵种不可得时，允许招募其他合法、负担得起的士兵。
2. **质量等级（Low/Middle/Top）不设置硬性比例上限**：作为偏好或诊断指标即可；允许成熟部队自然保有大量中、高级士兵。
3. **既有合法士兵不得仅因兵种比例或质量比例超标而被清退**，包括每周整编及解散部队合并到驻军后的整编。
4. **真实硬约束不变**：编制容量、合法兵种与文化/兵营解锁、实际候选供给、购买费用、驻军工资及长期维持能力。现有 95% 招募安全阈值属于另一个独立人数策略，本轮**不自动修改**，后续如有必要单独讨论。
5. **驻军与 AI Party 使用不同文化编制偏好模板**，但两者均采用软比例，不再因一个类别超额停止其他类别补员。现有 AI 行为、兵源渠道选择及场景理想人数策略保持不变。
6. **预测与实际执行规则一致**：预计数可以是估算，不得以不会执行的硬比例规则拒绝候选；实际补员仍以真正进入名册的人数统计。

## 二、现状与问题（已核实的代码）

### 2.1 统一招募模型把比例作为硬性数量上限

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs`

- `EvaluateRecruitment()` 计算 `AllowedByCombatRole`、`AllowedByQuality`，并与编制、工资、费用和维持能力一起取最小值决定 `RecruitableCount`。
- `GetAllowedByMaximum()` 用 `floor(PartySizeLimit × MaximumRatio) - CurrentCount` 算余量；超额直接得到 0。
- `GetPriority()` 已实现按兵种和质量**最低比例缺口**进行排序，另有 Tier 与雇佣兵来源修正。可以复用这一偏好机制。
- `FindPrimaryLimit()` 会将 `CombatRole`、`Quality` 当作硬性拒绝原因；去硬约束后要同步调整。

**数学上的缺陷**：阿塞莱驻军模板中，低级最多 50%，中级最多 55%，高级最多 25%。若驻军完全由中、高级士兵组成，二者的上限总和只有 80%；即使尚未满编，也可能因质量比例无法继续补员。对于长期升级的驻军，此逻辑不合理。

### 2.2 职业兵候选在生成环节提前被比例拒绝

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

- `BuildDailyProfessionalOffers()` 在每次挑选模板候选时调用 `IsCompositionEligible()`。
- `IsCompositionEligible()` 要求 `AllowedByPartySize > 0 && AllowedByCombatRole > 0 && AllowedByQuality > 0`。
- 当一个职业兵模板中的可供兵种都被某种比例挡住时，**训练能力与预计生产额度大于零，但实际不会生成可供入伍的候选**。
- `BuildDailyProfessionalOffers()` 还使用模拟状态逐个 `Commit`，修改预估人数/预算；重构时应防止“生成候选”与“实际批准招募”的预算扣除语义相混。

### 2.3 驻军每周会因配比清退已有士兵

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

- `OnWeeklyTick()` 对 AI 驻军调用 `NormalizeGarrisonNow()` → `NormalizeGarrison()`。
- `NormalizeGarrison()` 先移除非正常驻军成员，随后反复使用 `ExceedsRoleMaximum()`、`ExceedsQualityMaximum()` 选取士兵删除；当前实现优先删除较低阶的超额类别士兵。
- 合法士兵可能仅因其兵种或等级占比超标而被裁撤。**这一行为必须取消**；独立的经济/工资裁军机制不属于本次删除范围。

另有调用方：`ModifiedArmy/ModifiedArmy/Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs` 的 `Prefix()`，在解散部队合并入驻军后调用 `NormalizeGarrisonNow()`，因此也可能触发比例型裁撤。

### 2.4 俘虏留用也受旧硬比例上限影响

文件：`ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`

- `SelectAndPreparePrisonerCandidates()` 与 `GetTargetRetentionCount()` 根据 `roleMaximum`、`qualityMaximum` 决定保留多少可转化俘虏。
- 即使驻军还有编制空位，因既有高级兵/步兵比例超额，正常士兵俘虏也可能被直接拒绝保留；AI 城镇的多余俘虏还可能自动出售。
- **只修改统一招募模型是不完整的**：俘虏入口必须同时移除这两种硬比例限制；但继续保留俘虏仓容、可招募身份、英雄处理与原有出售政策的独立条件。

### 2.5 历史案例：坦姆努堡

用户游戏观察：驻军 `292/331`，其中步兵 `232`、射手 `60`；主要为中、高级兵，且高级兵居多；阿塞莱文化匹配、三级兵营、基础兵源充足，职业兵预计 `3.90/天`、实际 `0`。

- 95% 安全招募容量：`floor(331×0.95) - 292 = 22`，**并非人数上限问题**。
- 阿塞莱驻军步兵比例上限 70%，`floor(331×0.70)=231`；现有步兵 232，因此步兵硬性拒绝已经可以确认。
- 射手仍有名额，然而其质量类别、经济限制和当天候选是否生成需以日志验证；**不能仅凭截图认定所有零补员均由比例规则导致**。
- 此案例应纳入回归测试，保证“步兵超标但仍有合法射手或其他兵种”时可以补员。

## 三、目标机制

### 3.1 明确软偏好与硬约束分工

| 因素 | AI Party | 驻军 | 新规则 |
|---|---|---|---|
| 当前人数 / 编制上限 | 硬约束 | 硬约束 | 禁止越过实际可用上限；本轮不擅自改变 95% 阈值 |
| 士兵资格、文化和兵营开放 | 硬约束 | 硬约束 | 不生成或招募未开放的兵种 |
| 真实兵源/候选数量 | 硬约束 | 硬约束 | 不凭空生产 |
| 工资、购买成本、持续供养 | 硬约束 | 硬约束 | 仍使用现有财务模型及共享预算 |
| 战斗兵种 `min/max` | 软偏好 | 软偏好 | 缺口加分、超标轻微降权，但不能令合法招募数为零 |
| 质量等级 `min/max` | 软偏好 | 软偏好 | 仅排序引导，严禁固定占比拒招和自动裁撤 |
| 已拥有合法士兵 | 不因比例裁军 | 不因比例裁军 | 仅保留真正非法成员清理及独立经济机制 |

### 3.2 建议继续使用原 XML 的 min/max，改变其语义

文件：`ModifiedArmy/ModifiedArmy/ModuleData/aiRecruitmentTemplates.xml`

保留每个文化、`mobileParty` / `garrison` 的 `CombatRoles` 和 `Qualities`。`min/max` 不再定义“最低目标 + 最高硬上限”，改成**偏好区间**：

- 低于 `min`：明显提高招募优先级；
- 处于 `min…max`：正常招募；
- 高于 `max`：降低该类型的优先级，**但不禁止**。

建议首版**不调整 XML 数值**，先检验软约束是否解决长期停招；之后再独立平衡各文化偏好权重。无需新增军事制度、定居点或全局存档属性。

### 3.3 偏好计算（建议实现，权重可调）

设编制容量为 `L`，某候选的战斗兵种数量为 `N_role`、质量组数量为 `N_quality`；XML 给出对应比例 `min/max`。

```text
MinTarget(group) = floor(L × group.min)
MaxTarget(group) = floor(L × group.max)

Shortage(group) = clamp((MinTarget(group) - N_group)
                        / max(1, MinTarget(group)), 0, 1)
Excess(group)   = clamp((N_group - MaxTarget(group))
                        / max(1, L), 0, 1)

Priority = 2.0 × Shortage(role)
         + 1.0 × Shortage(quality)
         - 0.5 × Excess(role)
         - 0.2 × Excess(quality)
         + 0.05 × TroopTier
         - MercenarySourcePenalty
```

**说明**：这些系数是可落地的首版建议，并非现有代码数值；`max` 仅用于微弱降权，不参与 `RecruitableCount`。优先级可以为负，排序仍合法；绝不能将“分数低”当成禁止招募的理由。优先使用已存在的 `GetNormalizedShortage()`，避免引入多套不同的偏好公式。

如果希望改动更小，第一阶段可保留现有 `GetPriority()` 的最低比例缺口评分，仅取消硬性 `max`；待稳定后再加入 `Excess` 微弱扣分。

## 四、逐文件实施方案

### A. `Recruitment/Models/DefaultAIRecruitmentModel.cs`

定位：`DefaultAIRecruitmentModel.EvaluateRecruitment()`、`GetPriority()`、`FindPrimaryLimit()`、`GetAllowedByMaximum()`。

1. `RecruitableCount` **不再与** `AllowedByCombatRole`、`AllowedByQuality` 取最小值；只对 `RequestedCount`、`AllowedByPartySize`、`AllowedByWageLimit`、`AllowedByRecruitmentCost`、`AllowedByMaintenance` 等真实硬约束求最小值。
2. 继续按候选 `CombatRole`、`TroopQuality` 和文化模板计算**优先级**，确保比例低的候选被优先选择，但不能直接拒招。
3. `FindPrimaryLimit()` 移除 `CombatRole`、`Quality` 作为招募失败主因的分支，只返回实际硬限制。
4. `GetAllowedByMaximum()` 如果没有其他用途，可在引用全部迁移后删除；`RecruitmentEvaluationResult.AllowedByCombatRole/AllowedByQuality` 可先保留为**非约束诊断**或直接删除并同步修复全部引用，但不能让旧字段继续决定数量。
5. 保留现有模拟状态 `RecruitmentSimulationState.Commit()`，按实际批准数更新人数、财务占用及编制组成，避免计划阶段重复占用预算。现有 `RecruitmentFillPercentage=95` 本轮不改。

建议逻辑示意：

```csharp
// 示例性伪代码，不是可直接替换的完整方法：
result.RecruitableCount = Minimum(
    result.RequestedCount,
    result.AllowedByPartySize,
    result.AllowedByWageLimit,
    result.AllowedByRecruitmentCost,
    result.AllowedByMaintenance);

result.Priority = CalculateSoftCompositionPriority(
    party, troop, source, state);
result.PrimaryLimit = FindPrimaryLimit(result); // 只分析真实硬约束
```

### B. `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

定位：`BuildDailyProfessionalOffers()`、`IsCompositionEligible()`、`GetPredictedProfessionalOffers()`、`EnsureDailyProduction()`。

1. 候选资格首先由专业兵模板、实际兵营/文化允许兵种等级判断；**移除角色/质量超过 max 就直接排除**的规则。
2. `IsCompositionEligible()` 可以删除或改名为仅检查真实候选资格/人数空间；避免为软比例重新制造硬性 `false`。
3. 在多种可生产兵种间，用 XML `Weight` 和软性结构评分决定生产顺序或权重；缺编种类优先，但缺编类型没有可生产兵种时仍选择其他合法候选。
4. 候选生产环节不应提前消耗真实 Hearth 或招募预算；Hearth 仍在 `TryConsumeProfessionalOffer()` 的真正入伍事务中扣除。对于模拟状态，只提交确实选定的候选，不要把统一招募模型拒绝的数量当作已批准数量提交预算模拟。
5. 小数进度 `ProfessionalProductionProgress` 及当日单次生产的幂等性保持不变；已有的当日生产与预测共享规则不能再引用硬性配比。
6. 当模板全部缺失合法候选，保留现有进度返还或未使用额度处理，防止生产额度直接丢失；**不要把拒招候选永久储存成免费兵源**。

### C. `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`：每周整编

定位：`OnWeeklyTick()`、`NormalizeGarrisonNow()`、`NormalizeGarrison()`、`ExceedsRoleMaximum()`、`ExceedsQualityMaximum()`、`GetExcessScore()`。

1. **彻底删除** `NormalizeGarrison()` 中依据兵种比例、质量比例挑选并移除士兵的循环。
2. 保留对真实非法成员的原有处理（例如非 `Soldier` / `Mercenary` 的非英雄角色），但核实 Mod 的其他合法士兵类型不会被误判；不因为人数结构过高、品级高、文化不一致就自动清退。
3. 在调用方仍需要清理无效成员时，可保留 `NormalizeGarrisonNow()` 入口，改为执行“仅合法性清理”；日志方法应反映真实移除原因，不再写“违反编制比例”。
4. 若 `ExceedsRoleMaximum()`、`ExceedsQualityMaximum()`、`GetExcessScore()` 没有其他用途，完成全仓引用检查后删除。
5. `OnWeeklyTick()` 可以继续做必要合法性检查，但不执行比例裁军。工资不足导致的独立裁军仍由原有工资/维护系统负责。

### D. `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`：俘虏留用

定位：`SelectAndPreparePrisonerCandidates()`、`GetTargetRetentionCount()`。

1. 当前 AI 驻军留用可招降俘虏也被 `roleMaximum` 和 `qualityMaximum` 卡死；移除这些上限对**保留数量**的硬截断。
2. 仍保留可转换兵种身份、俘虏容量、英雄俘虏保护、归顺度、出售政策等独立业务逻辑。
3. 留用优先级可按当前角色缺口、质量偏好、俘虏兵阶决定，但比例超标本身不能强制卖掉原本允许留用的正规士兵；低阶俘虏现有售卖策略如需保留，应明确它是**独立经济政策**，而不是隐藏的质量上限。
4. 俘虏实际入伍仍由统一模型检查硬约束；不能因为取消比例上限就跳过财务、编制或归顺度。
5. 人数/留用逻辑改变后，对 `GetPredictedDailyRecruitmentCount()` 与实际每日俘虏准备逻辑的一致性进行回归测试。

### E. `Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs`

定位：`Prefix()`、`TransferApprovedRegulars()` 与 `NormalizeGarrisonNow()` 调用。

1. 解散 AI Party 并入友方驻军时，仍要检查驻军真实空间以及与该转移场景相应的费用/合法性规则。
2. 不得由于质量或兵种占比高而拒收仍有空间的正规士兵，也不得在合并后借 `NormalizeGarrisonNow()` 清退已存在的合法士兵。
3. **原方法可能清空解散部队名册**：重点检查被拒接收者的最终去向，避免只是去掉比例限制却留下其它静默损失；本轮仅要求发现并确保行为符合当前合法性与容量政策，不重新设计解散全流程。

### F. `Recruitment/Models/RecruitmentEvaluationResult.cs`、`Recruitment/Models/RecruitmentTypes.cs`

1. `AllowedByCombatRole`、`AllowedByQuality` 如不再作为数量上限，应调整字段语义或移除；优先明确的诊断数据（例如 `RoleShortageScore`、`QualityShortageScore`）而非复用 `AllowedBy...` 造成误导。
2. 现有 `RecruitmentLimitReason.CombatRole`、`Quality` 可先保留枚举数值避免牵连外部代码，但不再由正常招募失败路径生成；相关日志/本地化引用同步检查。
3. `RecruitmentSimulationState`、`RecruitmentTemplateRepository` 保留已有兵种和质量计数/模板结构，以继续为软偏好评分服务。

### G. `ModuleData/aiRecruitmentTemplates.xml`

- 首版**不删除** `CombatRoles`、`Qualities`，不修改七文化的比例参数。
- 代码中明确 `min/max` 为软性偏好区间，而非合法性要求。
- 更新对应 XML 注释和有关说明文档，避免后续维护者又将 `max` 重新用作硬上限。
- 不增加游戏 UI 数据；日志可打印缺口评分和真实拒招原因。

## 五、注意事项与边界

- **保留文化差异**：帝国驻军倾向步射、阿塞莱驻军偏步射并容纳骑射、库赛特偏骑兵与骑射等；软约束影响优先级，不影响本地文化兵种模板与建筑解锁。
- **避免“全选最贵兵”**：移除质量配额后，`GetPriority()` 当前带有 `Tier*0.05` 高阶偏好；应同时监控高阶集中度。高阶单位实际成本和维护费仍是硬约束；必要时调低 `Tier` 偏好系数，不恢复硬配额。
- **容量仍是真约束**：`PartySizeLimit`、现有 `RecruitmentFillPercentage=95`、每日最大招募人数都按本轮范围保留。它们也可能限制补员，需要以日志区分，不可笼统归罪于比例。
- **经济与粮食是另一层问题**：负 `FoodChange` 造成的招募停止、经济预算过低、Hearth 扣除失败等问题，本轮不擅自改变，但要保证可独立诊断。
- **候选≠入伍**：训练能力与生产额度、候选数、招募批准数、实际入伍数仍需分开；不许将软约束改造包装成保证实际每日入伍的承诺。
- **存档兼容**：尽量不更改任何现有 `SaveableProperty` 编号、持久化结构和文化配置格式；多数改动仅涉及运行时算法与已有 XML 注释。
- **日志而非 UI**：新增诊断信息写入 `ModLogger`，建议默认 Debug、异常时 Warn，避免全天候对全地图大规模输出。

## 六、日志与验证指标

建议对每次被跳过的职业兵候选（调试级）记录：

```text
[Recruitment][Plan]
Day=... Settlement=... Party=... PartyType=Garrison|MobileParty
Count=292 SizeLimit=331 FillThreshold=314
Candidate=... Source=ProfessionalProduction
Role=Ranged RoleCount=60 RolePreferredMin=... RolePreferredMax=...
Quality=MiddleTier QualityCount=... QualityPreferredMin=... QualityPreferredMax=...
SoftPriority=...
Available=... Approved=... Actual=...
HardLimit=PartySize|WageLimit|RecruitmentCost|MaintenanceFunds|None
```

必须保证：`RolePreferredMax` 和 `QualityPreferredMax` **只用于评分和诊断**，不进入 `HardLimit` 或 `RecruitableCount` 最小值计算。

对于每周整编，记录仅因成员确实非法而清理的数量；在正常合法驻军情况下，比例不符合但 `RatioBasedDismissed` 必须为 0（最好删除该类型行为）。

## 七、验收测试

| 场景 | 必须满足的结果 |
|---|---|
| 阿塞莱驻军 292/331、步兵232、射手60，且有合法射手/骑兵候选 | 比例超额不再造成拒招；是否实际入伍由真实编制、供给、工资、费用和维护条件决定 |
| 驻军高阶士兵超过旧 TopTier max | 不因质量比例拒绝同类合法士兵，也不因质量超额在每周整编时清退 |
| AI Party 缺骑兵，但当前定居点只提供合法步兵 | 首选骑兵无法得到时仍允许步兵恢复兵力 |
| 兵种结构明显缺射手且有多类候选 | 在费用等硬条件相近时，射手优先级应高于已过量的类别 |
| 驻军人数已到允许招募上限 | 不越编，不因软化比例而突破人数限制 |
| 招募预算或长期维持资金不足 | 仍正常拒绝并记录真实硬原因 |
| 异文化定居点 | 兵种仍来自定居点文化；训练速度和开放等级按已确认文化机制，不被本方案重写 |
| 没有合格职业兵模板或兵种未开放 | 不生成非法候选，不凭空生产 |
| AI 城镇仅有合格俘虏，其他比例已经超标 | 留用决策不以比例为硬拒绝；实际转化仍遵守俘虏归顺、费用和真实容量 |
| 解散 AI Party 并入驻军 | 合法士兵不因配比超额而被拒或事后裁撤；实际容量和其他政策照常执行 |
| 连续 30—60 个游戏日 | 驻军及 AI Party 实际可用兵力提升；不会因升级导致持续自我裁军；监控工资与高阶过度集中 |
| 玩家/AI、城镇/城堡、新档/旧档 | 行为一致，无重复招募、越编或存档加载异常 |
| UI 与日志 | 游戏 UI 不新增错误/调试信息；诊断只在 ModLogger |

## 八、实施顺序与交付内容

1. **统一模型**：去除战斗兵种和质量的硬性批准数量上限，保留原有偏好及所有真实硬约束。
2. **职业兵生产**：移除比例型候选筛除，保证候选生成与正式批准的软偏好一致。
3. **驻军整编**：取消按比例清退合法士兵，保留真正非法成员清理。
4. **俘虏留用与解散合并**：补齐非统一招募入口中的旧比例限制。
5. **模型字段、配置注释与日志**：清理硬限制语义，维持 XML/存档兼容。
6. **测试**：针对坦姆努堡用例及 30—60 天仿真验证；检查职业兵候选→批准→消耗→实际入伍完整链路。

Work 完成后应提交：根因与旧机制清单、修改文件及具体类/方法、核心偏好计算、新旧结果对比、测试结果，以及尚未解决的独立经济/粮食/人数阈值限制。**不得把移除比例硬限制等同于保证每天实际补员。**

---

**最终原则**：兵种与质量比例只决定“优先选谁”；真正的数量/费用/资格/供给决定“能否招募”；合法既有士兵不因比例变化而被自动裁撤。
