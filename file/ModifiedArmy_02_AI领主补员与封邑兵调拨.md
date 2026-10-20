# 02｜AI领主兵力目标、驻军调拨、封邑兵返乡及财政

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 本专题交付目标

修复AI领主Party获取/移交士兵的真实执行链，**不改变当前已经正常读取的WarDisposition**；与01的统一驻军人数API协作。避免Party奔赴无兵可取的城堡、无意触发整批封邑兵返乡或把已有士兵转移重复计入新兵预算。

## 1. Party人数规则（保留已工作的代码）

| 情况 | Party常规目标 | 边界 |
|---|---|---|
| 和平 | `min(HardLimit, max(50, ceil(.35×HardLimit)))` | 不因超过目标而自动裁军 |
| 正式战争，`D≤0` | 现有曲线35%～50% | 不改已有公式 |
| 正式战争，`0<D≤20` | **固定50%** | 不改成50%～59% |
| 正式战争，`20<D≤60` | 现有60%～80%分段 | 仅主战条件允许高目标 |
| 正式战争，`60<D≤100` | 80%～95% | 可到95%，不触发独立驻军90%限制 |
| 已在Army集结 | `max(90%,战争场景目标)` | 不强制出Army去招募 |
| 自家封邑防御救援 | 100%硬编制 | 不为补员耽误救援 |

Mod继续通过现有`WarDispositionBridge`读取`ModifiedPolitics.WarDispositionManager`真实值。**不修改**Bridge、`ModifiedArmy.csproj`对Politics的引用、两Mod现有运行依赖或反射路径。文化军事制度`MilitaryDoctrine`负责三兵源偏好；兵种角色与质量检查仍是软偏好。封邑兵AI单次实批不足25人则不征召的限制维持现状。

## 2. Party⇄驻军：明确接收方规则

- **驻军→Party**：来源驻军按01的边境75%/内陆50%最低保留线；接收Party按当前Party场景目标、工资和硬上限；专项增援装载另用任务Context。**不可把旧独立90%比例套用到Party接收方**。
- **Party→驻军**：源Party至少保留合法场景目标与必要安全成员；目标驻军按01的边境95%/内陆80%上限、实际成员+外出征粮队预留审批。
- 特殊攻城后的捐兵与玩家操作单独保护，本体合法规则不得因AI补丁而无条件被跳过。
- 同Clan转移只搬动已有成员，**不会在Clan层面新增长期军饷承诺**；接收方局部工资上限仍可检查。跨Clan转移另行审查。

## 3. 当前代码存在的问题及对应修改

| 代码位置 | 现状 | 修改方案 |
|---|---|---|
| `Recruitment/Behaviors/AiRecruitmentBehavior.cs` | `FindBestMultiPurposeSettlement`、`HasTransferableGarrisonTroops`仍用城镇125/城堡75固定判断 | 改为调用01的`GetLegalTransferOutCount`；无可取兵不得以“取兵”目的重派Party |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` | `RecruitmentSource.Transfer`的审批接收方采用统一90% | 用`TargetType + ForceOperation`确定Party或驻军目标，计划和执行同规则 |
| `Garrison/Models/GarrisonTransferPolicyModel.cs` | `GetAllowedDonationCount()`已有工资、食物、编制等约束与Party目标叠加 | 保留有效安全约束；对比有效人数和`GetSafeFieldSurplus()`，禁止业务模型重复推导目标 |
| `ForceManagement/Services/TroopTransferService.cs` | 已能保护封邑在役数量，但转移后会`CommitRecruitment(...unitDailyWage)` | 同Clan真实转移不再消耗新增采购和30天工资承诺；仍检查真实接收预算和上限 |
| `Garrison/Patches/GarrisonTroopDonationPatch.cs` | 同Clan普通捐兵入口先`ReturnTroopsToSettlementWithResult()` | **取消无条件返乡**；仅独立许可下调用来源返乡Service；Patch只转发 |
| `Fief/Models/FiefPartyManager.cs`和`FiefPartyReturn.cs` | 已有服役批次、来源、返乡、Hearth恢复和冷却 | 复用；新增独立场景许可，不重造台账 |
| `Recruitment/Patches/AiSettlementRecruitmentPatch.cs` | 已对到达时AI征兵统一拦截 | 确保目标可招、有授权且满足剩余场景缺口；玩家及特殊分支范围单独审查 |
| `Garrison/Patches/GarrisonTroopDonationPatch.cs`中的Take Prefix | 当前无明显Party类型/玩家例外分支且总是替代本体执行 | 加明确授权与边界检查；不能无条件影响玩家手动操作 |

## 4. 封邑兵返乡独立决策

设计一个可单元测试的 `IFiefReturnPolicy`，并把实际Roster/服役Hearth结算留给`FiefReturnService`与原`FiefPartyManager`。

```csharp
public enum FiefReturnReason
{
    ExplicitReturn, PlannedRotation, PostWarDemobilization,
    Disbanding, OrdinaryGarrisonDonation, RoutineVisit
}

public interface IFiefReturnPolicy
{
    bool MayReturn(MobileParty party, Settlement sourceFief,
                   FiefReturnReason reason);
}
```

- `OrdinaryGarrisonDonation`、`RoutineVisit`默认**false**；主动返乡、合法轮换和特定解散另行审批。
- 转移普通士兵时，按**来源在役批次**保护士兵数量。与普通职业兵同`CharacterObject`时，实际可移交数＝名册数－登记在役数。
- 只对真正完成的来源返乡调用`ReturnTroopsToSettlementWithResult`，使Hearth和冷却记录一致；不可对一次捐兵请求直接归还该封邑所有在役批次。
- 玩家路径保留手动语义，但必须检查调用来源，不可在通用Patch里无条件触发。

## 5. 财政与转移事务

区分**新兵创建**与**既有士兵迁移**：`ClanRecruitmentBudgetManager.CommitRecruitment`会按单位工资×30天扣除新增招募共享预算。Clan A的驻军→Clan A的Party只是工资责任迁移，不是新增士兵；不得继续调用新兵预算提交。接收者工资上限与财务可承担性仍可以只读审批，必要时设计`ApproveExistingTroopTransfer()`。

Roster操作要先验证两端真实库存、伤员、硬/理想容量、Party状态、Clan所有权和在役来源批次，然后执行；失败时保证两侧名册总量一致，不通过`Clear()`掩盖失败。

## 6. 分阶段实施和验收

**先改寻址、审批、转移；再加返乡许可和财政拆分。** 先把现有可运行的单向转移修稳，不把未来复杂Swap塞进本阶段。

| 测试 | 预期 |
|---|---|
| Party上限150，D=100，目标143，当前135 | 可继续从合法驻军获取≤8名，不能被驻军旧90%=135封死 |
| 边境Hard300、驻军实际200、无征粮队 | Party不能因旧城堡75人判断而专程前往取兵 |
| Party同一Character含在役封邑25和普通15 | 普通捐兵最多移15，不改变25在役记录 |
| Party在战争中从来源城堡路过，普通捐兵5人 | 不自动让60名在役封邑兵全部返乡 |
| 同Clan既有士兵转移30名 | Clan总士兵数守恒、**不**扣新增30天招募预算 |
| D=0、20、21、100 | `0<D≤20`固定50%；超过20后执行现有主战曲线 |
| 玩家手动换驻军/攻城后捐兵/Party解散 | 原合法行为不被AI专用Prefix错误替代 |

## 7. 源码入口

- [Party招募行为](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/AiRecruitmentBehavior.cs)
- [驻军取兵审批](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonTransferPolicyModel.cs)
- [Party与驻军Patch](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Garrison/Patches/GarrisonTroopDonationPatch.cs)
- [实际普通兵员转移](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Services/TroopTransferService.cs)
- [封邑在役经理](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [政治Bridge与Facade](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Services/ForceManagementFacade.cs)
