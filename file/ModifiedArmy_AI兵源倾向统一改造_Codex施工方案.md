# ModifiedArmy AI兵源倾向与战争倾向统一改造方案

> Codex 施工方案｜仓库：`GuihuaZhou/bannerlord`｜模块：`ModifiedArmy`
>
> 方案基于 `main` 分支中 `PartyForceCandidateService`、`AiForcePlanningModel`、`PartyForceTargetModel`、`InitialPartyMobilizationCommitter`、`MilitaryConfig` 等代码核对形成。执行前以本地最新 HEAD 和仓库指令为准。

## 一、施工目标

### 1.1 最终设计

AI 兵力决策由三个职责明确的系统共同完成：

**MilitaryDoctrine（军事制度）**

- 决定文化的封邑兵、职业兵生产和潜在供应能力。
- 决定 AI 对三种兵源的基础偏好。
- 权重是相对偏好，不是硬性配额，也不是严格优先级。

**PartyForceTargetModel（兵力目标）**

- 根据战争倾向、和平状态和特殊军事任务决定 Party 的目标人数。
- 输出 `TargetCount`、`GapCount`。
- 保留现有战争倾向与兵力目标算法。

**AI Recruitment（补员决策）**

- 当 AI 确定需要补员时，评估实际能够征召的封邑兵、职业驻军和雇佣兵。
- 综合军事制度权重、可用人数、文化匹配、距离及经济成本进行评分。
- 选择综合评分最高的合法兵源。
- 兵源不足时允许转向其他兵源，而不是严格执行文化权重排序。
- 每次实际调兵后，剩余需求必须重新以真实 Party 状态计算。

### 1.2 关键验收示例

瓦兰迪亚：

```xml
<MilitaryDoctrine fief="7" professional="2" mercenary="1"/>
```

假设 Party 缺100人：

| 封邑兵可征 | 职业驻军可调 | 期望倾向 |
|---:|---:|---|
| 100 | 100 | 封邑兵 |
| 30 | 200 | 职业兵可以胜出 |
| 0 | 100 | 职业兵 |
| 100 | 0 | 封邑兵 |
| 0 | 0 | 可以考虑合法雇佣兵 |

这里的可用数量必须经过封邑批次、驻军留守、招募准入等规则计算，不能直接采用原始兵源库存。

**不得改成按照7:2:1分配70%、20%、10%的固定士兵比例。**

---

## 二、现有代码确认

### 2.1 当前兵源评分

文件：

`ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs`

相关函数：

- `BuildContext()`
- `EnumerateCandidates()`
- `AddFiefCandidate()`
- `AddProfessionalCandidate()`
- `AddMercenaryCandidate()`
- `CreateCandidate()`
- `GetSourceWeights()`
- `CalculateNeedScore()`

当前评分：

```csharp
Score = preference * culture * 100f
      + coverage * 80f
      - travelCost
      - economicPenalty;
```

其中：

- `preference`：归一化后的文化与情境权重。
- `culture`：文化匹配修正。
- `coverage`：候选兵源能够满足的缺口比例。
- `travelCost`：距离成本。
- `economicPenalty`：经济成本惩罚。

原有架构方向正确，应当保留综合评分机制。

### 2.2 当前问题

`GetSourceWeights()` 将文化权重与以下情境乘数叠加：

- `peace`
- `war`
- `siege`
- `broke`
- `critical`

其中 `critical` 使用 `PartySizeRatio < 0.3`，与战争倾向决定的目标人数脱节。

其他情境也会直接改变军事制度的兵源偏好，造成文化权重并非稳定的基础策略。

因此本次改造的核心是：**保留文化偏好与实际兵源的动态竞争，取消重复的情境乘数。**

---

## 三、第一阶段：统一兵源权重来源

### 修改文件

1. `ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs`
2. `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`
3. `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`

### 3.1 修改 GetSourceWeights()

当前：

```csharp
var basis = config.GetAiCulturalPreference(
    party?.LeaderHero?.Culture);
```

之后依次应用 `peace/war/siege/broke/critical` 修正。

改造后：

1. 从现有 `MilitaryDoctrine` 读取 Fief、Professional、Mercenary 三项权重。
2. 不再应用任何 `SituationalModifiers`。
3. 对三项权重做归一化。
4. 返回现有 `PartyForceSourceWeights`。
5. 保留所有权重均为0时的安全回退处理。

示意：

```csharp
var doctrine = config.GetAiCulturalPreference(
    party.LeaderHero.Culture);

float total = doctrine.Fief
            + doctrine.GarrisonTransfer
            + doctrine.Mercenary;

return new PartyForceSourceWeights
{
    Fief = doctrine.Fief / total,
    Professional = doctrine.GarrisonTransfer / total,
    Mercenary = doctrine.Mercenary / total
};
```

示意省略了现有空值、无效配置以及总权重为0的保护。正式实现必须保留这些保护。

禁止为兵源倾向再引入另一套文化配置。

### 3.2 删除旧情境乘数

从 `militaryConfig.xml` 删除整个：

```xml
<SituationalModifiers>
    ...
</SituationalModifiers>
```

同步检查并清理 `MilitaryConfig.cs` 中不再使用的：

- `AiSituationalModifier`
- `AiRecruitmentConfig.SituationalModifiers`
- `GetAiSituationalModifier()`
- `GetAiSituationalThreshold()`
- `ParseAi()` 中情境节点解析与完整性校验

从 `PartyForceCandidateService` 删除：

- `ApplyConditionalModifier()`
- 仅用于情境乘数的 `IsBroke()`
- `critical` 固定30%阈值判断
- 不再需要的情境参数与调用链

清理旧 XML 注释，避免配置文档继续描述已经删除的机制。

### 3.3 保留的现有逻辑

不能因为删除情境乘数而误删下面这些机制：

- `PartyForceTargetModel` 的和平/战争目标。
- `GetWarTargetRatio()`。
- `OwnFiefDefenseTargetRatio`。
- `NeedScore` 中的和平、战争和围城紧迫度。
- 资金不足时的真实招募限制。
- 驻军保护、兵源准入、任务优先级。
- `CulturalIdentity` 中的文化匹配修正。

`broke` 的权重乘数要删除，但实际资金检查不能删除。

---

## 四、第二阶段：重新校准兵源评分

### 4.1 当前存在的问题

当前军事制度偏好项系数为100，兵源覆盖项系数为80。

在封邑兵权重较高，并具有文化匹配加成的情况下，少量封邑兵可能仍持续压过能够填满缺口的职业兵。

这与设计预期不完全一致。

### 4.2 推荐评分公式

建议初始采用：

\[
Score=40P+60C-D-E
\]

其中：

- \(P\)：归一化军事制度权重 × 文化匹配系数。
- \(C\)：候选实际批准人数 / 当前兵力缺口，上限1。
- \(D\)：现有距离惩罚。
- \(E\)：现有经济成本惩罚。

对应：

```csharp
Score = 40f * preference * culture
      + 60f * coverage
      - travelCost
      - economicPenalty;
```

40/60 是本次推荐的初始平衡值，不是不可修改的硬性规则。

### 4.3 为什么推荐40/60

瓦兰迪亚权重7:2:1，归一化后：

```text
Fief         = 0.7
Professional = 0.2
Mercenary    = 0.1
```

假设领主与定居点文化一致：

- 封邑兵文化匹配系数1.3。
- 职业兵文化系数1.0。
- 忽略两类兵源相同的距离成本。

**场景A：缺100人，封邑兵100、职业兵200**

```text
Fief = 40 × 0.7 × 1.3 + 60 × 1.0
     = 96.4

Professional = 40 × 0.2 + 60 × 1.0
             = 68
```

选择封邑兵。

**场景B：缺100人，封邑兵30、职业兵200**

```text
Fief = 40 × 0.7 × 1.3 + 60 × 0.3
     = 54.4

Professional = 40 × 0.2 + 60 × 1.0
             = 68
```

选择职业兵。

**场景C：缺30人，封邑兵30、职业兵200**

```text
Fief = 96.4

Professional = 68
```

选择封邑兵，因为30人足以填满全部缺口。

> 注意：案例C采用缺30人的情景，以便满足现有封邑兵单批至少25人的规则。纯评分测试可以使用缺20人的输入，但实际集成测试必须首先遵守最低批次限制。

### 4.4 参数配置

建议在 `AiRecruitment` 下新增：

```xml
<SourceSelection>
    <DoctrineWeight value="40"/>
    <CoverageWeight value="60"/>
</SourceSelection>
```

要求：

- 通过 `MilitaryConfig.cs` 加载。
- 参数必须为非负有限数。
- 至少有一个权重大于0。
- 不新增存档字段。
- 不新增另一套兵源权重系统。

文化军事制度仍使用原有 `MilitaryDoctrine`。

### 4.5 代码组织

建议将纯评分公式提取到：

`ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyForceSourceScoringModel.cs`

该 Model 只负责计算：

- Doctrine 贡献。
- Coverage 贡献。
- 距离惩罚。
- 经济惩罚。
- 最终 Score。

不得访问或修改 Party、Hearth、Roster、Gold。

`PartyForceCandidateService.CreateCandidate()` 仍负责构建候选，只调用该 Model 得到评分。

这样可以单独测试评分结果，避免在 Service 中混入新的决策逻辑。

---

## 五、第三阶段：保持实际兵源可用性为权威

必须检查以下三条候选生成路径。

### 5.1 AddFiefCandidate()

继续通过：

```csharp
FiefPartyManager.PreviewFiefRecruitmentApprovals(...)
```

获取已经批准的封邑兵数量。

要求：

- 不使用村庄原始库存直接评分。
- 不绕过封邑兵最低批次要求。
- 不绕过绑定权限。
- 不为了获得评分重复执行有副作用的征召流程。
- 可用封邑兵为0时不生成候选。

### 5.2 AddProfessionalCandidate()

继续使用：

```csharp
GarrisonTransferPolicyModel
    .GetTransferableGarrisonTroopCount(...)
```

并通过现有 `RecruitmentModel.BuildPlan()` 获得批准量。

要求：

- 扣除不可调兵力。
- 保留驻军安全留守人数。
- 排除仍处于封邑服役身份、不得当作普通驻军调拨的士兵。
- 不允许超额抽空驻军。

### 5.3 AddMercenaryCandidate()

继续使用现有市场库存、招募准入和经济检查。

要求：

- 雇佣兵同样参加综合评分。
- 当封邑兵和职业兵不足时，雇佣兵应有机会胜出。
- 不保证所有文化一定使用雇佣兵，具体取决于权重、供应和成本。
- 不绕过购买资金和工资限制。

### 5.4 Coverage 的统一定义

全部来源统一采用：

```csharp
int covered = Math.Min(
    Math.Max(0, gap),
    Math.Max(0, approvedCount));

float coverage = gap <= 0
    ? 0f
    : (float)covered / gap;
```

必须使用当前真实缺口。

候选库存200人，但 Party 只缺60人时，评分覆盖量应为60，而不是200。

---

## 六、第四阶段：接入两个实际补员入口

### 6.1 日常 AI 补员

涉及：

- `ForceManagement/Models/AiForcePlanningModel.cs`
- `ForceManagement/Missions/PartyForceTaskCoordinator.cs`
- `ForceManagement/Missions/PartyVisitExecutionService.cs`
- `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs`

当前正常流程：

```text
计算目标人数
    ↓
计算补员需求
    ↓
生成候选
    ↓
按照 Score 排序
    ↓
选择最高分候选
    ↓
生成 Recruit 任务
    ↓
Party 抵达目标定居点
    ↓
重新检查真实资源
    ↓
执行调兵
```

保持这一架构。

本次只改变兵源权重来源和评分，不改：

- 每日任务规划事件。
- 战略 AI 的移动权。
- Recruit 任务生命周期。
- 任务持久化结构。
- 到达定居点后重新验证准入的流程。

当一次招募不足以补齐目标时，后续规划重新计算缺口并重新评分；不得将首次选中的兵源永久锁定为 Party 唯一兵源。

**不要求一次普通补员任务同时前往多个定居点。**

### 6.2 新建 Party 的初始补员

涉及：

- `PartyManagement/Services/PartyCreationCoordinator.cs`
- `PartyManagement/Services/InitialPartyMobilizationPlanner.cs`
- `PartyManagement/Services/InitialPartyMobilizationCommitter.cs`

必须保留当前设计：

**DryRun 只判断能否创建 Party，不要求其估计的封邑兵/驻军人数与实际调兵完全一致。**

目前 `InitialPartyMobilizationCommitter.Commit()`：

```csharp
.OrderByDescending(item => item.Score)
.ThenBy(item => item.SourceKind)
```

实际使用 `PartyForceCandidateService` 生成的候选。

因此，改造共享评分之后，新建 Party 也应自然应用新的兵源倾向。

要求：

- 不把 DryRun 的兵源组成变成实际调兵配额。
- 实际创建后仍从实时资源重新生成候选。
- 不强制封邑兵优先。
- 一种兵源不足时继续尝试其他合法候选。
- 每次实际调兵仍由现有 `UnifiedRecruitmentExecutionService` 执行。
- 不修改创建时的事务、保留和失败处理规则。

特别注意：初始只有1名领主不再触发 `critical`，因为整个 `critical` 机制已经删除。

---

## 七、第五阶段：可解释的评分日志

修改：

`ModifiedArmy/ModifiedArmy/ForceManagement/Diagnostics/ForceRecruitmentDiagnostics.cs`

建议添加或扩展只读的兵源选择诊断。

每次需要记录：

```text
PartyName
PartyId
ClanName
Culture
TargetCount
CurrentCount
GapCount

CandidateSettlement
SourceKind
DoctrineWeight
NormalizedPreference
CultureFactor
AvailableApprovedCount
Coverage
DoctrineScore
CoverageScore
TravelPenalty
EconomicPenalty
FinalScore

SelectedSource
SelectionReason
```

要求：

- 使用已有 `RecruitmentLogFilter` 限制高频日志范围。
- 普通成功只记录一次选择结果及必要的候选比较。
- 不每小时重复记录全地图全部候选。
- 日志读取已计算的结果，不能为了打印日志再次执行 Preview 或 BuildPlan。
- 文件日志使用英文基础文本。
- 需要面向玩家显示的信息遵守中英文多语言要求。

建议新增稳定原因码：

```text
BestWeightedFeasibleSource
SourceCoverageInsufficient
SourceUnavailable
NoFeasibleRecruitmentSource
```

其中 `BestWeightedFeasibleSource` 表示本次按统一综合评分选择成功，不代表严格的军事制度优先级。

---

## 八、必须完成的测试

### T1：瓦兰迪亚封邑兵充足

```text
Doctrine = 7:2:1
Gap = 100
FiefApproved = 100
ProfessionalApproved = 100
CultureMatch = true
Distance = equal
```

预期：封邑兵评分更高。

### T2：瓦兰迪亚封邑兵不足

```text
Doctrine = 7:2:1
Gap = 100
FiefApproved = 30
ProfessionalApproved = 100
CultureMatch = true
Distance = equal
```

预期：职业兵评分更高。

### T3：瓦兰迪亚仅缺少量士兵

```text
Doctrine = 7:2:1
Gap = 30
FiefAvailable = 30
ProfessionalAvailable = 200
```

预期：只计算最多30人的覆盖率，封邑兵具有更高偏好。集成测试必须满足封邑兵最低25人的实际批次规则。

### T4：封邑兵耗尽

封邑兵批准量为0，职业驻军有可调兵力。

预期：不生成封邑兵候选，职业兵可以被选择。

### T5：职业驻军不可调拨

驻军有200人，但安全保护规则使批准量为0。

预期：职业兵不能凭原始200人获得高分。

### T6：三种兵源不足

封邑兵、职业兵、雇佣兵的合法供应不足以填满缺口。

预期：允许部分补员，后续可重新规划；不得制造士兵或突破保护规则。

### T7：帝国兵源偏好

```text
Doctrine = 1:4:3
Gap = 100
FiefApproved = 100
ProfessionalApproved = 100
MercenaryApproved = 100
```

同文化、相同距离、忽略成本差异时，预期职业兵具有最高基础倾向。

### T8：战争倾向独立性

给同一文化、相同实际缺口与候选资源，设置不同战争倾向。

预期：

- 战争倾向影响 `TargetCount` 和实际缺口。
- 在相同缺口条件下，文化来源权重不再被 `war` / `critical` 额外修改。

### T9：新建 Party

新 Party 在正式创建后，通过真实资源和新评分选择兵源。

预期：

- DryRun 仍仅决定创建资格。
- 无须匹配 DryRun 的兵源组成。
- 无固定封邑优先。
- 不出现重复调兵。
- 实际执行仍遵守目标人数和招募上限。

### T10：存档兼容与长期运行

确认：

- 新存档和旧存档均可加载。
- `MilitaryDoctrine` 配置正常加载。
- 不新增或更改 Saveable 类型及字段 ID。
- 和平复员、战争休整、封邑兵返乡行为不受影响。
- 正常补员不突破驻军下限。
- 日志没有新增高频刷屏问题。

---

## 九、实施步骤与交付要求

### 阶段A：代码审计

在修改之前：

1. 读取仓库 `AGENTS.md`、`ModifiedArmy/CODE_ORGANIZATION_RULES.md`，以及存在的模块级指令。
2. 记录当前分支、HEAD SHA 和 `git status --short`。
3. 确认上述函数仍与本方案审阅版本一致。
4. 全仓搜索旧 `SituationalModifiers` 的实际调用，确认不会遗漏运行时依赖。
5. 列出本次修改文件及不应触碰的资源写入路径。

### 阶段B：模型与配置修改

1. 移除旧情境乘数。
2. 保留军事制度原始权重。
3. 增加可配置的评分系数。
4. 提取纯评分 Model。
5. 确保已有候选 API 和契约尽可能保持不变。

### 阶段C：回归验证

1. 先完成纯评分测试。
2. 再验证普通日常补员。
3. 再验证新建 Party 的真实调兵。
4. 检查旧配置引用、无用函数和过时注释。
5. 验证资源准入、事务及保存数据没有受到修改。

### 阶段D：构建和报告

至少执行：

```powershell
dotnet build .\ModifiedArmy\ModifiedArmy.sln -c Release
```

并检查适用的仓库验证脚本：

```powershell
.\ModifiedArmy\tools\Invoke-ReleaseValidation.ps1
```

注意：静态检查通过不等于游戏内验收通过。没有实际游戏证据的项目应标记为 `BLOCKED`，不得虚报 `PASS`。

交付报告必须包含：

- 修改文件清单。
- 各主要修改点的具体代码位置。
- 配置变化。
- 评分前后对照。
- 各项测试 PASS / FAIL / BLOCKED。
- 编译及静态检查结果。
- `git diff` 检查结果。
- 未解决的问题与风险。

不得未经授权提交、推送或部署。

---

## 十、明确禁止顺带修改的内容

本次只改 AI 兵源倾向的决策规则。

不得顺带修改：

1. `PartyForceTargetModel` 的战争倾向目标公式。
2. 新建 Party 的50人门槛及34人目标冲突。
3. `MissingServingParty` 失效批次清算。
4. 封邑 Hearth 消耗、生产、服役批次和返乡逻辑。
5. 驻军生产速度、驻军上限及保护下限。
6. 和平复员、战争休整和军团创建资格。
7. 跨定居点运输任务和原版战略 AI 移动行为。
8. 兵种升级、文化兵种定义和经济公式。

这些是其他独立任务，不能因为本次改造而引入额外风险。

## 十一、最终验收原则

**军事制度决定文化偏好，战争倾向决定目标兵力，实际兵源可用性决定当下最适合从哪里补员。**

不能因为封邑兵权重高就无视职业兵的数量优势，也不能因为职业驻军数量多，就在所有情况下压过文化本身的封邑兵偏好。

不设置固定兵源配额，不执行严格优先级，不再叠加 `critical` 等独立情境权重。

尽量复用现有规划、准入、调兵执行及日志体系，以最小修改实现统一决策。

---

## 附：施工范围摘要

**应修改**：兵源评分、情境乘数、配置解析、相关诊断。

**应复用**：现有候选生成、统一调兵执行、Party 创建与日常补员入口。

**暂不修改**：战争倾向目标、Hearth、驻军保护和服役批次数据结构。

评分初始参数采用**文化偏好40、兵源覆盖60**；需在游戏中验证其他文化和不同供给条件下的效果。若需要后续调整，仅改 `SourceSelection` 配置，不重复实现新的评分机制。
