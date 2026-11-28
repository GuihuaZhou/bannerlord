# ModifiedArmy 驻军职业兵与俘虏招募开关拆分 — Codex 施工方案

## 1. 目标及范围

采用最小化修改，不重构现有招募架构。

- 原版 `Town.GarrisonAutoRecruitmentIsEnabled` 仅控制职业兵自动招募。
- 新增独立的俘虏招募开关，以城镇/城堡为单位，默认 `true`，支持存档。
- `ProcessSettlement()` 中两个开关均关闭才跳过自动招募；任一个开启即可进入公共招募流程。
- 分别根据开关收集职业兵和俘虏候选，继续合并候选并使用现有 `BuildPlan()`、`ExecuteUnifiedPlan()`。
- 不新增 Behavior 或 Service，不修改 UI、Prefab、ViewModel、MCN，不做旧存档迁移。
- 玩家和 AI 驻军复用现有调度，不改变每日共享额度、编制、工资、费用、兵种优先级及人口扣减规则。

## 2. 源码依据

分析所用 Git 快照：`90d1e2a35d6dbb4a169aa925788a2d7da3020305`，仓库 `GuihuaZhou/bannerlord`。施工前以本地 HEAD 为准核对差异，不覆盖已有修改。

以下路径相对于 `ModifiedArmy/ModifiedArmy/`：

- `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`：`ProcessSettlement()`（约 53–255 行）目前使用原版开关统一控制两类候选；`GetPredictedDailyRecruitmentCount()`（约 452–518 行）同样使用原版开关；`SyncData()` 当前为空。
- `Recruitment/Services/PrisonerGarrisonRecruitmentService.cs`：`SelectAndPrepare()`（约 46–93 行）同时负责留用筛选、归顺度增加、AI 多余俘虏出售。
- `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`：职业兵生产已由原版开关控制；`EnsureDailyProduction()` 同时负责封邑兵生产。
- `Recruitment/Models/RecruitmentTroopEligibilityPolicy.cs`：AI 驻军野怪过滤目前依赖原版开关。
- `Recruitment/Patches/GarrisonRecruitmentPatch.cs` 与 `ForceManagement/Models/ForceManagementModels.cs`：均使用现有招募总数预测接口，应保持其签名与调用方式。

## 3. 新增俘虏招募开关及存档

直接在 `GarrisonRecruitFromPrisonersBehavior` 中增加字段，无须另建设置 Behavior：

```csharp
private Dictionary<string, bool> _prisonerRecruitmentSettings
    = new Dictionary<string, bool>();
```

提供后端读写接口，使用 `settlement.StringId` 作为键；不存在记录时返回 `true`，绝不回退到原版职业兵开关。无效定居点应安全拒绝；以下是参考实现：

```csharp
public bool IsPrisonerRecruitmentEnabled(Settlement settlement)
{
    if (settlement?.IsFortification != true)
        return false;

    return !_prisonerRecruitmentSettings.TryGetValue(
        settlement.StringId, out bool enabled) || enabled;
}

public void SetPrisonerRecruitmentEnabled(
    Settlement settlement, bool enabled)
{
    if (settlement?.IsFortification != true)
        return;

    _prisonerRecruitmentSettings[settlement.StringId] = enabled;
}
```

修改现有 `SyncData()`：

```csharp
public override void SyncData(IDataStore dataStore)
{
    dataStore.SyncData(
        "_prisonerRecruitmentSettings",
        ref _prisonerRecruitmentSettings);

    _prisonerRecruitmentSettings ??=
        new Dictionary<string, bool>();
}
```

核对现有 `SaveableTypeDefiner` 是否已经注册 `Dictionary<string, bool>`；仅在确有需要时新增容器定义，避免重复注册。不新增迁移字段或旧存档迁移逻辑。设置属于定居点，不随驻军 Party 重建而丢失。不额外引入易主重置策略。

## 4. 修改 ProcessSettlement()

### 4.1 获取两个独立开关

在确认 `settlement` 为有效城镇/城堡之后读取：

```csharp
bool professionalEnabled = town.GarrisonAutoRecruitmentIsEnabled;
bool prisonerEnabled = IsPrisonerRecruitmentEnabled(settlement);
bool recruitmentEnabled = professionalEnabled || prisonerEnabled;
```

保留 `EnsureDailyProduction()`、职业兵实际补员初始记录等原有调用及相对顺序。不要把双关闭退出判断提前到 `EnsureDailyProduction()` 之前，因为该方法同时负责封邑兵生产。

### 4.2 驻军自动创建

原来的 `town.GarrisonAutoRecruitmentIsEnabled` 条件改成 `recruitmentEnabled`，保留其他保护：

```csharp
if (town.GarrisonParty == null &&
    recruitmentEnabled &&
    settlement.Party.MapEvent == null &&
    settlement.Party.SiegeEvent == null)
{
    settlement.AddGarrisonParty();
}
```

仅开启俘虏招募时也应能够按现有规则创建驻军。

### 4.3 公共退出条件

```csharp
if (town.GarrisonParty == null ||
    settlement.OwnerClan == null ||
    !recruitmentEnabled ||
    settlement.Party.MapEvent != null ||
    settlement.Party.SiegeEvent != null)
{
    // 保留现有阻塞日志及 return。
    return;
}
```

### 4.4 按开关收集候选

保持原有俘虏准备操作与候选合并顺序（职业兵候选在前、俘虏候选在后），避免改变同分候选的优先级：

```csharp
int targetCount = 0;
int readyCount = 0;
int sold = 0;

List<RecruitmentCandidate> prisonerCandidates =
    new List<RecruitmentCandidate>();

if (prisonerEnabled)
{
    prisonerCandidates = SelectAndPreparePrisonerCandidates(
        settlement, out targetCount, out readyCount, out sold);
}

List<RecruitmentCandidate> candidates =
    new List<RecruitmentCandidate>();

if (professionalEnabled && pools != null)
{
    candidates.AddRange(
        pools.GetAvailableProfessionalOffers(settlement)
            .Where(entry => entry.Key != null && entry.Value > 0)
            .Select(entry => new RecruitmentCandidate(
                entry.Key,
                entry.Value,
                RecruitmentSource.ProfessionalProduction)));
}

candidates.AddRange(prisonerCandidates);
```

保留现有 `dailyLimit > 0 && candidates.Count > 0` 判断及 `BuildPlan()`、`ExecuteUnifiedPlan()`、实际职业兵补员记录和费用结算。

注意：关闭俘虏开关必须跳过整个 `SelectAndPreparePrisonerCandidates()`，而非在调用后过滤候选。该方法会产生归顺度增加及 AI 俘虏出售的副作用。俘虏开关开启时，即使每日额度为 0，也保留原有俘虏准备流程。关闭职业兵开关时，即使当日存在旧 Offer，也不得纳入候选。

## 5. 同步修改的三个小地方

### 5.1 后端预测

`GetPredictedDailyRecruitmentCount(Town town)` 保留签名及返回总数语义：

- 从当前 Campaign 的 `GarrisonRecruitFromPrisonersBehavior` 获取俘虏开关，职业兵使用原版开关；正常有效定居点的无记录默认开启。
- 保留现有定居点、驻军、交战和围城保护；两个开关都关闭时返回 0。
- 仅职业兵开启时执行现有职业兵预测候选收集；仅俘虏开启时执行现有俘虏预测候选遍历；两个都开启则维持原有候选合并行为。
- 保留现有统一计划审批、每日额度限制及总人数返回；不拆分预测结果类型、不修改 UI，不顺带调整既有预测算法。
- 预测必须只读，不调用会增加归顺度、出售俘虏、消耗兵源或扣费的准备/提交接口。

### 5.2 AI 驻军野怪过滤

在 `RecruitmentTroopEligibilityPolicy.ShouldExcludeBanditForAiRecruitment()` 中，将 AI 驻军的原版开关单独判断改为任一个招募开关开启。保留玩家主部队、玩家驻军、AI 领主等现有识别及排除规则；避免仅开启俘虏招募时绕过野怪兵种限制。处理 Behavior 获取为空的情况，不能产生空引用异常。

### 5.3 日志

俘虏招募日志触发条件改为：

```csharp
if (prisonerEnabled && (initialPrisonerCount > 0 || sold > 0))
{
    LogDailyResult(/* 保留现有参数 */);
}
```

保留原有职业兵日志及 `RecruitmentLogFilter`。关闭俘虏招募后不得打印归顺度不足、无俘虏候选等误导日志。仅记录真实执行结果；既有职业兵阻塞日志中的原版开关仍表示职业兵状态。不新增日志框架或 UI 文本。

## 6. 文件修改范围

| 文件 | 操作 |
|---|---|
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 新增开关、存档，修改公共准入、候选筛选、预测、日志 |
| `Recruitment/Models/RecruitmentTroopEligibilityPolicy.cs` | 调整 AI 驻军野怪过滤条件 |
| 现有 `SaveableTypeDefiner` 所在文件 | 仅在确实缺少容器注册时修改 |

原则上不修改 `Main.cs`、`ClanGarrisonRecruitmentCoordinator.cs`、`SettlementManpowerProductionBehavior.cs`、`GarrisonRecruitmentExecutionService.cs`、`ProfessionalRecruitmentService.cs`、`PrisonerGarrisonRecruitmentService.cs`，不新增文件或架构层。每周非法驻军兵种清理、野战部队俘虏招募、玩家手动招募、封邑兵生产及原版基础驻军变化不受影响。

## 7. 验收清单

| 职业兵开关 | 俘虏开关 | 招募候选 |
|---|---|---|
| true | true | 职业兵 + 俘虏兵，维持原有统一审批 |
| true | false | 仅职业兵 |
| false | true | 仅俘虏兵 |
| false | false | 无，不影响封邑兵生产 |

- [ ] 俘虏开关无记录时默认 true；两个开关互不影响。
- [ ] 不同城镇/城堡设置独立；保存并读档后设置正确恢复。
- [ ] 仅俘虏开关开启且没有驻军时，按原规则创建驻军。
- [ ] 两个开关关闭时不执行两类自动招募；封邑兵正常生产。
- [ ] 关闭俘虏招募后，该路径不增加归顺度、不出售俘虏、不招募俘虏；已有归顺度不清空。
- [ ] 关闭职业兵招募后不使用当日已有职业兵 Offer。
- [ ] 两个开关同时开启时，仍使用一个候选列表、一个审批计划、一个每日共享额度；顺序、费用、工资、编制与人口规则不变。
- [ ] 交战、围城、满员及财政不足等现有保护有效。
- [ ] 后端预测正确遵守两个开关，不写入任何兵源、俘虏或财政状态。
- [ ] AI 驻军仅允许俘虏招募时，既有野怪过滤仍然生效。
- [ ] 日志不误报已关闭来源的招募原因，每周驻军清理不受开关影响。
- [ ] 完成编译和引用检查；报告改动文件、实际测试结果及未验证事项。

## 8. 施工约束

本方案只要求新增一个可持久化开关及少量条件判断，不拆分招募计划、不设置职业兵优先、不改变两类来源的竞争关系，不做 UI 或旧存档迁移。代码片段为施工参考，Codex 需按本地实际源码适配并验证。方案编制时尚未修改仓库或编译测试。
