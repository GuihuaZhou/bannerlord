# W3：Campaign 生命周期与状态管理

## 问题及真实代码依据

`ForceManagementFacade` 由 `static Lazy<ForceManagementFacade>` 提供进程级实例，内部同时持有纯模型与会变化的服务。其 `PartyExchangeService._completedOperationIds`、`WarRestPlanningService._progress`、`WarDispositionBridge.ReportedFallbacks` 可能跨新游戏和读档沿用运行时数据。此外，`GarrisonRecruitmentCapacity.ReportedMultipleParties` 是静态日志抑制集合；最新的 `SupplyPartyBehavior` 还有 `_threatAvoidanceUntil`、`_lastRouteMismatchDay`、`_lastHealthSnapshots`、`_lastFoodRiskDay` 和静态 `LastRouteCommandHours`。

**性质**：生命周期失配风险由源码结构证实；具体跨存档错误需要实机测试。不要把没有复现的风险写成确定发生的 Bug。

## 目标架构

- `Bootstrap/CampaignServices.cs`：当前 Campaign 专属的服务集合，不引入第三方 DI 容器。
- `Bootstrap/CampaignLifecycleBehavior.cs`：统一接收战役初始化/加载/退出相关事件，触发正确阶段的协调。
- `Models`：纯算法可复用；配置在现有 `MilitaryConfigManager` 启动时加载/校验。
- `Services`：运行时可变成员明确归属当前 Campaign，退出/换档时不能继续引用旧世界 `MobileParty`、`Settlement`。
- `Saveable` 业务状态保持原 Behavior/SaveDefiner 的序列化身份，**不因为引入 Scope 就挪进不可存档的普通服务**。

## 推荐接口

```csharp
public interface ICampaignRuntimeState
{
    void ResetForNewCampaign();
    void RestoreAfterLoad();
}

public sealed class CampaignServices
{
    public PartyExchangeService PartyExchangeRunner { get; }
    public WarRestPlanningService WarRestPlanning { get; }
    // …需要 Campaign 范围的实例；纯模型可共享。
}
```

具体注册时机依据游戏可用事件确定。`OnGameStart` 是新 Campaign 的构建阶段；Behavior 的 `SyncData` 是数据序列化阶段；`OnGameLoadFinished` 才做运行时索引重建；必须避开“重建完成后另一个 Behavior 再把缓存清空”的顺序错误。

## 分步骤迁移

1. **盘点状态**：列出所有 `static Dictionary/HashSet`、`Lazy`、字段类型为 `MobileParty`/`Settlement`/`Clan` 的持久化引用，以及所有 `SyncData`。
2. **不改调用形态先引入 Scope**：保留 `ForceManagementFacade.Current` 作为兼容入口，但使其按当前 `Campaign` 引用解析所属的服务，或于 Campaign 切换时替换其内部活动服务。不要一次性修改全部调用处。
3. **建立显式清理**：只清理运行时缓存、每日计数器、诊断抑制集合、旧操作 ID 缓存，不清理从存档恢复的封邑服役批次或长期兵源记录。
4. **明确恢复协议**：`SyncData` 负责读取 Saveable；`OnGameLoadFinished` 由所属 Behavior 重建索引和对账，最后通知 Scope 进入 `Ready`。需要防止新行为访问未恢复的服务。
5. **迁移桥接状态**：`WarDispositionBridge.ReportedFallbacks` 和 `GarrisonRecruitmentCapacity.ReportedMultipleParties` 由每 Campaign 日志控制器持有或在切换时清空；`SupplyPartyBehavior.LastRouteCommandHours` 改为实例状态或增加显式清理。
6. **每日状态边界**：`WarRestPlanningService._progress` 以 Campaign 日期重置；任务每日进度与入档永久任务分开。
7. **兼容现有存档**：不要随意修改 `CampaignReadyBehavior` 和 `CampaignState.IsNewGame` 的触发逻辑，**特别保护 Main.cs 99000 金币/装备行为**。

## 生命周期状态检查建议

- 未进入 Campaign：禁止读取 `Campaign.Current` 业务对象。
- 开始新 Campaign：运行状态全新；不继承前一个 Campaign 的 `OperationId` 和警告抑制集合。
- 加载已有 Campaign：先恢复数据，再重新建立非持久化索引，之后允许新调兵/征粮。
- 切换存档：原世界对象不可继续保存为键；任何旧 `MobileParty` 引用都不应影响新世界。
- 暂停、主菜单、再读档：不能重复初始化 XML 或重复订阅事件。

## 验收测试

1. 同一游戏进程：新游戏 A → 存档 A → 回主菜单 → 读取 B → 回主菜单 → 读取 A，记录运行时状态是否串档。
2. 在 A 中提交一次调兵，在 B 中存在同名角色、定居点、相同日期时不能被 A 的 `_completedOperationIds` 拒绝。
3. A 的征粮路线诊断节流不压制 B 的警告。
4. 读取有征粮队/增援队/服役批次的存档后，长期任务状态不丢失；运行时索引可重建。
5. 不能因为生命周期改造使新开局奖励重复发放；不得改变该奖励内容。

## Worker 交付

- 新增 Campaign Scope / Lifecycle 的实现及对接；逐一列出迁移和未迁移的有状态类。
- 状态归属表和加载事件时序图。
- 两个不同 Campaign 同进程切换的日志/测试结果。
