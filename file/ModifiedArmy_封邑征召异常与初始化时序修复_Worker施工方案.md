# ModifiedArmy 封邑征召异常与初始化时序修复——Worker 施工任务书

## 一、任务目标与代码基线

**目标：直接修改代码，完成修复、编译和静态验证，不要只提交分析报告。**

- 仓库：`GuihuaZhou/bannerlord`
- 分支：`main`
- 分析基线：`06991e8ee0fb4ae0c09685ede0045553b17d9a44`（2026-11-02）

施工前先执行 `git status` 和 `git log -1`，确认实际工作树状态。如 HEAD 已变化，以实际代码为准重新核对调用位置，不覆盖现有未提交修改。

本次解决三个明确问题：

1. 封邑征召事务在 `ValidateSource`、`Snapshot` 等阶段访问 `TroopRosterElement.WoundedNumber`，触发 `NullReferenceException`。
2. 存档读取后，封邑数据已反序列化，但运行时模板尚未恢复，兵源计算提前执行。
3. 每日兵源生产在确认封邑初始化成功之前，已将当天写入“已生产”记录，导致当天生产可能被跳过。

**原则：修复根因，不通过扩大 `try/catch`、吞异常或重置数据掩盖问题。**

## 二、P0：修复 TroopRoster 数量读取

### 涉及文件

`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs`

相关方法：

- `GetRosterCounts()`：约 1105 行
- `TryCommitVillageRecruitment()`：约 835 行
- `RecruitManualSelection()`：约 730 行
- `BuildTemplateWeightedRecruitmentCandidates()`：约 337 行
- `RestoreRosterEntry()`
- `ValidateRecruitmentCommit()`
- `VerifyRecruitmentRollback()`

### 2.1 统一安全读取入口

重写现有 `GetRosterCounts()`。

**禁止使用 `FirstOrDefault()` 返回的 `TroopRosterElement` 直接读取 `WoundedNumber`。**

使用 `TroopRoster` 原生 API：

```csharp
private static (int Count, int Wounded) GetRosterCounts(
    TroopRoster roster, CharacterObject troop)
{
    if (roster == null || troop == null)
        return (0, 0);

    int index = roster.FindIndexOfTroop(troop);
    if (index < 0)
        return (0, 0);

    int count = Math.Max(0, roster.GetElementNumber(index));
    int wounded = Math.Min(
        count,
        Math.Max(0, roster.GetElementWoundedNumber(index)));

    return (count, wounded);
}
```

要求：

- 兵种不存在时返回 `(0, 0)`。
- 伤兵数不得大于总数。
- 不缓存 roster 索引跨越 `AddToCounts()`、`RemoveTroop()` 等修改。
- 不修改 `TroopRoster` 的底层字段或直接调用低级 setter。
- 继续复用现有 `GetRosterCounts()`，避免产生多个互相不一致的读取实现。

### 2.2 修复 ValidateSource

当前代码遍历多个村庄寻找兵种时：

```csharp
TroopRosterElement element = roster.GetTroopRoster()
    .FirstOrDefault(e => e.Character == approval.Key);
```

某村庄没有该兵种是合法情况，不应抛异常。

替换为：

```csharp
var counts = GetRosterCounts(roster, approval.Key);

int healthy = counts.Count - counts.Wounded;
int available = healthyOnly ? healthy : counts.Count;

int count = Math.Min(remaining, available);
if (count <= 0)
    continue;

int wounded = healthyOnly
    ? 0
    : Math.Max(0, count - healthy);
```

构造 `VillageRecruitmentSource` 时：

```csharp
BeforeCount = counts.Count,
BeforeWounded = counts.Wounded
```

**保留原有规则：**

- `healthyOnly == true`：只能征召健康士兵。
- `healthyOnly == false`：按照现有规则计算包含伤兵的征召。
- 某村庄没有指定兵种时，继续寻找下一个村庄。
- 所有合法来源的总数仍不足时，正常返回 `InsufficientReadySource`。
- 不允许部分来源不足时提前提交不完整的事务。

### 2.3 修复 Snapshot

保留现有快照结构：

```csharp
targetBefore = plan.Select(p => p.Troop).Distinct()
    .ToDictionary(
        troop => troop,
        troop => GetRosterCounts(targetParty.MemberRoster, troop));
```

依靠修复后的 `GetRosterCounts()`，允许目标领主部队尚不存在该兵种。

必须验证后续 `ValidateRecruitmentCommit()`、`RestoreRosterEntry()` 和 `VerifyRecruitmentRollback()` 都能处理“提交前数量为 0”的情况。

**不能仅让快照不抛异常，还必须验证真正的征召提交成功。**

### 2.4 修复手动征召

`RecruitManualSelection()` 同样存在 `FirstOrDefault() + WoundedNumber` 风险。

将当前封邑可征召 roster 先获取一次，然后通过 `GetRosterCounts()` 计算健康人数。

保留以下行为：

- 兵种消失时跳过。
- 伤兵不能通过健康征召进入部队。
- 不能超过目标部队人数上限。
- 不能绕过 `TryCommitVillageRecruitment()`。

### 2.5 核查其他直接读取伤兵数的封邑路径

重点检查：

- `FiefPartyReinforcement.cs` 中的 `GetFiefTroopRoster()`。
- `FiefPartyReinforcement.cs` 中的 `PerformAutoUpgrade()`。
- `FiefPartyRecruitment.cs` 中的 `BuildTemplateWeightedRecruitmentCandidates()`。
- `FiefPartyManager.cs` 中的封邑部队查询与展示。

对于需要按兵种获取伤兵数量的逻辑，优先复用 `GetRosterCounts()`。

特别注意：`PerformAutoUpgrade()` 之前已经修复集合枚举修改异常，其 `ToList()` 快照机制必须保留。如果改造该方法，应**在开始升级前一次性记录每个兵种的健康人数**，不能升级一部分士兵后重新计算待升级人数，否则可能导致新升级士兵在同一周再次升级。

不要为解决空指针而重新引入 `Collection was modified`。

## 三、P0：修复初始化生命周期

### 涉及文件

- `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

### 3.1 建立明确的初始化状态

当前 `FiefPartyData._fiefPartyTemplate` 是运行时字段，不随存档保存。

读取旧存档时：

1. `_fiefDataMap` 已经恢复。
2. `FiefPartyData` 对象已存在。
3. `_fiefPartyTemplate` 可能仍为 `null`。
4. `OnSessionLaunched()` 才重新解析模板。

因此，不能通过 `GetFiefData() != null` 判断初始化完成。

在 `FiefPartyManager` 增加**不参与存档序列化**的会话就绪状态，例如：

```csharp
public bool IsSessionReady { get; private set; }
```

在 `FiefPartyData` 增加运行时初始化成功状态，例如：

```csharp
internal bool IsRuntimeReady { get; private set; }
```

`InitialFeifPartyData()` 应：

- 进入初始化时设置 `IsRuntimeReady = false`。
- 成功完成模板、村庄绑定及现有合法性检查后才设置 `true`。
- 任一失败分支保留 `false`。
- 不修改原有存档字段定义。

### 3.2 修复 FiefPartyManager 初始化流程

保持现有初始化阶段顺序：

1. 初始化封邑模板管理器。
2. 恢复全部封邑及其村庄数据。
3. 新游戏执行初始封邑兵生成。
4. 重建征召记录索引。
5. 校验和协调兵员动员状态。
6. 完成后发布会话就绪状态。

`IsSessionReady` 应在整体必要初始化完成后才设置 `true`。

修改 `InitializeFiefData()`：

- 必须检查每个 `InitialFeifPartyData()` 的返回值。
- 不能继续用 `_fiefDataMap.Count` 代替初始化成功数。
- 记录应有封邑数、成功数、失败数。
- 单个封邑初始化失败时，记录封邑 ID、文化、期望模板 ID、失败原因。
- 其他封邑不应因此失去正常运行能力。

建议向管理器提供统一查询方法：

```csharp
public bool TryGetReadyFiefData(
    Settlement settlement,
    out FiefPartyData data)
{
    data = null;

    if (!IsSessionReady || settlement == null)
        return false;

    FiefPartyData candidate = GetFiefData(settlement);

    if (candidate?.IsRuntimeReady != true)
        return false;

    data = candidate;
    return true;
}
```

实际权限修饰符可按现有调用范围收窄。

**禁止**在各种只读兵源计算方法里临时调用 `InitialFeifPartyData()`。初始化应由生命周期管理器负责。

### 3.3 区分“尚未初始化”和“初始化失败”

在 `CalculateVillageSupply()`、`GetProductionForecast()` 中使用统一就绪判断。

- 会话尚未准备完成：返回未就绪结果，暂不记录 `Error`。
- 会话已就绪，但指定封邑初始化失败：记录结构化错误。
- 初始化成功，但正常计算所得兵源为零：视为正常业务结果。

`GetProductionForecast()` 现有返回类型允许返回 `null`，可以继续以 `null` 表示暂不可计算。

`CalculateVillageSupply()` 返回 `float`，可在未就绪时暂时返回 `0f`，但不能允许上层把这个临时值作为已经完成的每日生产结果持久化。

对于 `VillageFiefData.GetOccupiedSupplyHearth()`：

- 保留模板和 doctrine 必须有效的严格前置条件。
- 不要删除现有异常检查。
- 正确做法是阻止未就绪调用进入该方法。

## 四、P0：修复每日生产的错误提交时机

### 涉及文件

`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`

关键方法：

- `EnsureDailyProduction()`
- `GetProductionForecast()`
- `CalculateVillageSupply()`

### 4.1 调整 EnsureDailyProduction 执行顺序

现有代码在计算前执行：

```csharp
_dailyProfessionalOffers.Remove(settlement);
_professionalOfferDays[settlement] = day;
```

这是错误的初始化时序边界。

修改为以下流程：

```text
检查 settlement 有效
    ↓
检查今天是否已经完成生产
    ↓
检查 FiefPartyManager 会话已就绪
    ↓
检查指定封邑运行时初始化成功
    ↓
同步现有职业兵招募偏好
    ↓
生成有效的 RecruitmentProductionForecast
    ↓
确认产能快照可用
    ↓
清理当天旧报价并标记本日已处理
    ↓
执行现有职业兵/封邑兵生产流程
```

要求：

- 会话未就绪：不能写 `_professionalOfferDays`。
- 封邑未就绪：不能删除 `_dailyProfessionalOffers`。
- Forecast 无法生成：不能写当日完成标记。
- 不能提前执行 `RecordFiefReplenishment(day, 0)`。
- Forecast 正常生成但 `SharedSupply == 0`：属于有效的零产能日，可以正常标记完成。
- 保留“同一日最多执行一次”的已有机制。

**重要：** 不要简单将生产标记移动至所有增兵操作之后。当前生产包含多个状态变更，如果执行途中发生异常，尾部标记可能造成重试时重复生成。应在**确认初始化及 Forecast 有效后、首次生产状态修改前**记录当日处理状态，保持既有幂等语义。

### 4.2 处理初始化前触发的生产请求

Worker 需要确认启动阶段是否确实存在 `EnsureDailyProduction()` 的提前调用。

若存在，应采用轻量延迟机制：

- 记录本次会话因初始化未完成而跳过的定居点及游戏日期。
- `FiefPartyManager` 初始化完成后，通知生产模块重试这些请求。
- 只重试当天仍有效的请求。
- 已经成功执行过当天生产的定居点不得重复执行。
- 不允许每次加载存档就无条件给全部封邑补发一天兵员。

若调用链证实仅为初始化期间的只读预测，则无需引入延迟生产队列；只需修复读取顺序并提交调用链证据。

这部分由 worker 根据真实调用关系决定，**不应为了保险直接引入复杂调度框架**。

## 五、P1：同类问题排查

使用代码搜索，核查以下模式：

```text
FirstOrDefault(...) 后访问 .WoundedNumber
FirstOrDefault(...) 后访问 .Number
GetTroopRoster() 枚举时修改同一 roster
GetFiefData() != null 就认定已初始化
GetProductionForecast() 返回 null 后仍提交每日处理标记
```

至少覆盖：

- 封邑 AI 征召
- 玩家手动征召
- 跨村庄征召
- 封邑兵返乡与冷却
- 每日补员
- 每周升级
- 封邑及村庄兵源查询

只修改实际存在风险的调用，不做无关模块的全面重构。

## 六、必须执行的验收场景

### A. 征召事务

**A1：目标领主原本没有该兵种**

- 目标 roster 查询返回 `(0, 0)`。
- Snapshot 不抛异常。
- 征召成功，目标部队实际增加相应兵员。
- 来源村庄人数正确减少。
- 动员 Hearth、服务批次、共享征召索引一致。

**A2：多个村庄中部分村庄没有该兵种**

- 不再出现 `ValidateSource` 空指针。
- 能正确跳过无兵种村庄。
- 来源不足时，正常返回 `InsufficientReadySource`。
- 没有残留的部分提交。

**A3：健康士兵和伤兵混合**

- 健康征召只转移健康人员。
- 允许包含伤兵的现有业务路径仍能正确计算人数。
- 事务前后总人数与伤兵人数守恒。

**A4：手动征召列表过期**

- 兵种已消失时安全拒绝或跳过。
- 不抛空指针。
- 不产生额外兵员。

**A5：人为制造事务中途失败**

至少验证一次已进入修改阶段的失败：

- 恢复目标 roster。
- 恢复村庄 roster。
- 恢复 Hearth。
- 恢复服务批次及其索引。
- 回滚验证成功才能记录 `Verified`。
- 验证失败继续保留 `RecoveryRequired`。

### B. 初始化与生产

**B1：连续两次读取同一旧存档**

- 不再出现初始化阶段成批的 `Fief supply calculation requires template and doctrine` 错误。
- 显示真实初始化成功数。
- 初始化完成后各文化的正常封邑均可计算兵源。

**B2：初始化前提前请求生产**

- 不记录当天已生产。
- 不修改生产进度和当天报价。
- 正常初始化完成后，该请求可按设计恢复执行。

**B3：同一天重复调用生产**

- 不重复生成职业兵报价。
- 不重复生成封邑兵。
- 不重复累计生产进度。

**B4：真正缺少封邑模板**

- 初始化失败日志能明确指出缺失模板。
- 相关封邑不进入正式生产。
- 不能伪装为普通的零兵源日。
- 其他正常封邑仍可运行。

### C. 回归

**C1：跨过 Weekly Tick**

- 不再出现 `Collection was modified`。
- 自动升级成功。
- 同一周内新升级的士兵不能再次升级。

**C2：旧存档兼容**

- 不修改 SaveableField 编号。
- 不改变存档数据类型定义。
- 旧存档可读取、运行、再次保存和重新载入。

## 七、构建与发布门禁

从仓库根目录执行：

```powershell
dotnet build ModifiedArmy/ModifiedArmy.sln -c Release
dotnet build ModifiedPolitics/ModifiedPolitics.sln -c Release

powershell -NoProfile -ExecutionPolicy Bypass `
  -File ModifiedArmy/tools/Test-ArchitectureReleaseGate.ps1
```

然后按现有项目要求执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File ModifiedArmy/tools/Invoke-ReleaseValidation.ps1
```

保持：

- ModifiedArmy 不反向依赖 ModifiedPolitics。
- 不增加 Harmony 临时补丁绕过正式模型。
- 不修改文化军事制度 XML 或职业兵训练参数。
- 不调整封邑人口、兵源经济公式。
- 不擅自修改封邑兵升级概率。
- 不清除已有服务批次或通过删除兵员解决兵源预算问题。

没有游戏内验证时，应明确标记 `Runtime: BLOCKED`，不得仅凭 Release 编译宣布全部完成。

## 八、Worker 最终交付

完成施工后提交：

1. **根因报告**：列明旧调用链与新调用链，解释初始化早期访问来自哪里。
2. **修改清单**：具体文件、方法、关键实现及修改理由。
3. **代码 Diff / Commit**：所有修改可追溯，不混入无关功能。
4. **验证报告**：编译、静态门禁、可执行测试和未完成的游戏内测试。
5. **运行期诊断**：展示初始化状态、每日生产状态和事务提交结果。
6. **部署产物**：确认编译成功后提供最新 DLL 路径，并标明是否已经游戏验证。

**完成标准：不只是让日志安静，而是确保 AI 真正能征召封邑兵、每日生产不会因加载时序丢失、事务失败时能正确恢复状态。**

## 九、源码参考

- [FiefPartyRecruitment.cs](https://github.com/GuihuaZhou/bannerlord/blob/06991e8ee0fb4ae0c09685ede0045553b17d9a44/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)
- [FiefPartyReinforcement.cs](https://github.com/GuihuaZhou/bannerlord/blob/06991e8ee0fb4ae0c09685ede0045553b17d9a44/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [FiefPartyData.cs](https://github.com/GuihuaZhou/bannerlord/blob/06991e8ee0fb4ae0c09685ede0045553b17d9a44/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [FiefPartyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/06991e8ee0fb4ae0c09685ede0045553b17d9a44/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [VillageFiefData.cs](https://github.com/GuihuaZhou/bannerlord/blob/06991e8ee0fb4ae0c09685ede0045553b17d9a44/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [SettlementManpowerProductionBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/06991e8ee0fb4ae0c09685ede0045553b17d9a44/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
