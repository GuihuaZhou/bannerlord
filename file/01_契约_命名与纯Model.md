# 01｜契约、命名与纯Model职责重构

> 项目：`GuihuaZhou/bannerlord` · 基线：`main` / `4ae5ff66084efa33e362c13ee7f8b686a22c17ab`（2026-10-22 核对）。
> 状态：**实施计划；并非代码已经修改或测试通过**。Worker 开始前必须读取最新仓库 HEAD；如果源码发生变化，以当时实际代码为准并报告差异。
> 范围：`ModifiedArmy/ModifiedArmy/`；除注明外均以此为相对路径。

## 1. 目标与边界

将分散的“决策结构”收敛为三类纯Model，不在此阶段改变AI行动或名册：

- `PartyForceTargetModel`：已有唯一人数目标来源，**完全保留规则**。
- `AiForcePlanningModel`：战略层生成 Recruit/WarRest/Maintain 的任务及候选定居点。
- `PartyExchangeModel`：已有士兵在不同Roster间的可转移性和具体计划。
- `AiTroopRecruitmentModel`：新增兵员的资格、编制、兵种、工资审批；兼容已有 `RecruitmentSource`，并保留囚犯特殊规则。

## 2. 已确认现状与修改文件

| 文件 | 当前状态 | 操作 |
|---|---|---|
| `Recruitment/Models/AIRecruitmentModel.cs` | 抽象 `BuildPlan`、`EvaluateRecruitment` | 更名/兼容迁移为 `AiTroopRecruitmentModel` |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` | 同时处理 `Fief / Transfer / Prisoner / Mercenary` | 更名/过渡：旧转移候选审核暂做内置委托，不重复重写 |
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` | 每日决策与移动指令都在Behavior | 后续改名 `AiForcePlanningBehavior`，本阶段先建立新依赖契约 |
| `ForceManagement/Contracts/ForceManagementTypes.cs` | `IPartyGarrisonExchangeModel`, `ExchangePlan` 仅Garrison↔Field | 新增 `IPartyExchangeModel`, `PartyExchangeContext`, `PartyExchangePlan`, `PartyForceTask`等 |
| `ForceManagement/Models/ForceManagementModels.cs` | `DisabledPartyGarrisonExchangeModel`及现有目标Model | 保留旧实现直到切换；增添新接口Stub |
| `ForceManagement/Services/ForceManagementFacade.cs` | `PartyTargets=PartyForceTargetModel(new WarDispositionBridge())` | 注册新接口但不启用旧路径以外的修改 |
| `Recruitment/Models/RecruitmentTypes.cs`、`common/common.cs` | `RecruitmentSource`与 `RecruitSource`是不同枚举 | 保留语义或显式转换，不直接混用 |

### 现有需保持不变的目标

`ForceManagementFacade.GetPartyTarget(party)`：
- Peace：`min(limit,max(50,ceil(limit*0.35)))`（使用现有XML数值）。
- WarRoutine：按 `WarDispositionBridge` 从 `WarDispositionManager` 读取Clan倾向，经 `GetWarTargetRatio(disposition)`。
- OwnFiefDefense：目标比例1.00。

**本阶段不修改战争倾向20临界点附近的非连续公式**；可单独记录技术债。

## 3. 建议契约（示意，Worker须按现有版本类型实现）

```csharp
public enum PartyForceIntent { Maintain, Recruit, WarRest, Suspended }
public enum PartyExchangeOperation {
    None, FieldToGarrison, GarrisonToField,
    FiefMobilization, FiefReturn, BidirectionalSwap
}

public interface IAiForcePlanningModel {
    PartyForcePlan BuildDailyPlan(PartyForcePlanningContext context);
}
public interface IPartyExchangeModel {
    PartyExchangePlan BuildPlan(PartyExchangeContext context);
}
```

`PartyForcePlanningContext`应包含：当前Party、任务归属信息、`PartyForceTargetResult`、战争态势/休整审批、Clan财政快照、候选定居点信息。只读，不产生移动行为。

`PartyExchangeContext`应包含：Party交换发起者、操作类型、当前战略意图、来源/目标资源描述、目标定居点、已有名册快照、当前时日、转移目的（自然访问/增援/新占领/复员/解散等）。

`PartyExchangePlan`应包含：来源/目标、各兵种批准数量、伤员、封邑来源与批次、关键约束快照与指纹、拒绝原因、预期双方人数和工资、后续执行器类型。空计划必须显式给出 `ReasonCode`。

**资源适配**：普通驻军也是 `MobileParty`；Fief当前 `_fiefParty` 从 `MilitiaPartyComponent.MobileParty.MemberRoster` 绑定，但伴随封邑数据、Hearth和服役批次，因此不应只用两个`MobileParty`表达封邑兵流转。建议 `PartyTroopSource` (Kind: Mobile/Garrison/Fief) + Settlement/BatchId/roster accessor，**不可将该描述对象作为另一个存档中复制的Roster真源**。

## 4. 迁移策略

1. 创建新合同，保留旧`IPartyGarrisonExchangeModel/ExchangePlan`供旧路径引用，直到第05任务原子切换。
2. `AiTroopRecruitmentModel`可先提供安全兼容适配，内部委托既有`DefaultAIRecruitmentModel`，之后再更换引用。不得同时存在两个相互矛盾的审批真源。
3. `RecruitmentModelManager`、`AiSettlementRecruitmentPatch`、`FiefPartyRecruitment`、`GarrisonRecruitFromPrisonersBehavior`、`PrisonerRecruitmentPatch`、`SettlementManpowerProductionBehavior`等引用需逐个清点并编译验证。
4. 新Model默认不触发真实调兵（不只是`EnableAutomaticSwap=false`；后者专门代表兵种互换）。
5. 新 `PartyForceTask` 如需持久化，采用正式的 `SaveableTypeDefiner`/字段定义，确认ID不与 `FiefSaveDefiner` 和其他模块冲突；不直接存储会过期的Roster快照，载入后重新评估。既有`modifiedarmy_party_garrison_exchange_days`及复员存档键不能删。

## 5. 纯函数和依赖要求

- `IAiForcePlanningModel`可依赖兵力目标及来源可行性接口；不依赖Harmony或Campaign事件对象进行操作。
- `IPartyExchangeModel`可读取GarrisonDefense、Fief批次/名册、招募规则与Clan工资快照；不得调用`AddToCounts`。
- `AiTroopRecruitmentModel`继续负责新增部队士兵资格与数量；转移用该模型时需要明确只**审核**，不把既有同Clan调拨视作新的购买/30天承诺。
- `WarRestPolicyModel`第04任务完成，先定义接口而非实现未经审批的阈值。

## 6. Worker步骤与验收

- [ ] 编译通过，所有调用点已迁移或使用明确过渡适配；没有未使用/重复的第二套审批类。
- [ ] 新接口的空计划、拒绝理由、兵力目标均可独立测试。
- [ ] 不变式：以旧目标Model为基准，和平目标及不同战争倾向返回完全一致。
- [ ] 文化XML权重、Source枚举映射没有语义丢失。
- [ ] 本阶段**没有改变任何AI的移动指令或Roster**；可单独回滚。

交付：接口变更清单、编译结果、旧接口保留的原因、后续移除点；不要在此PR禁用原版Behavior。
