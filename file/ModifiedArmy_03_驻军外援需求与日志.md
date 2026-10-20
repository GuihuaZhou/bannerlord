# 03｜驻军外援需求Model、调出安全与日志

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 本专题交付目标

让已经存在的`GarrisonDefenseRequirementModel`和`ExternalReinforcementNeedModel`按确定的人数规则准确输出请求与可安全调出人数，**本专题只评估和记录，不派遣跨城运输Party**。依赖专题01统一的`EffectiveCount`与驻军上下限。

## 1. 已确定的需求规则

### 边境：仅比较当前有效人数与75%下限

```text
BorderMinimum = ceil(HardLimit × .75)
BorderRequest = max(0, BorderMinimum - EffectiveGarrisonCount)
```

**不扣除**未来七日、本地产能、当日可招募数量；边境村庄被劫掠造成生产受阻，短期预测不能代替当前缺额。请求默认只补到**最低保留75%**，不是直接申请达到95%自动补员上限。

### 非边境：暂保留现有七日预测近似模型

```text
InteriorMinimum = ceil(HardLimit × .50)
InteriorRequest = max(0, InteriorMinimum - EffectiveCount - PredictedSevenDayRecruitment)
```

暂不擅自调整七日窗口或预测算法；当前`PredictedDailyRecruitmentCount × 7`是**近似值**，没有逐日模拟俘虏消耗、共享财政竞争和容量变化。该近似应封装为可替换Policy，后续可讨论，不需现在重构生产系统。

## 2. 与驻军最低留守及征粮队一致

- `EffectiveCount = ResidentCount + ActiveSupplyEscortCount`，外援计算、可调出人数和日志都使用此人数，不另立实际留守战力Model。
- 来源驻军安全调出：`min(max(0, EffectiveCount-MinimumCount), 实际城内可合法转出兵)`；边境最低75%，非边境50%。
- 不能把外出征粮队当成城内可转移库存；不能把来援Party的在途人数当成已经入驻的Roster，未来启用运输时在目标重复请求中单独预留在途额度。
- **需求计算和执行是两层**：Model输出`RequestedTroops`不等于发出有效任务，是否可送达/谁去送由专题05负责。

## 3. 最新实现中的具体问题

| 位置 | 已有行为 | 待修改 |
|---|---|---|
| `ForceManagement/Models/ForceManagementModels.cs`·`GarrisonDefenseRequirementModel.Evaluate` | `CurrentRegularCount`读取驻军名册但另存`EscortReservedCount`，最低边境90% | 改用专题01的IdealRange，当前有效人数直接可用 |
| 同文件·`ExternalReinforcementNeedModel.Evaluate` | `current + predictedDaily × 7`对所有驻军生效 | **只在非边境使用预测**，边境直接按当前人数请求 |
| 同文件·`GetSafeTransferOutCount` | 使用当前正规兵数扣最低保留线 | 复用专题01统一安全调出人数，实际名册复核 |
| `ForceManagement/Behaviors/ForceManagementBehavior.cs` | 为玩家所属王国的城堡/城镇打印状态变更日志 | 显示正确Effective/边境类型/最低人数/请求量，保持文件日志去重 |
| `common/MilitaryConfig.cs`、`militaryConfig.xml` | `BorderDefenseRatio=.90`、`InteriorDefenseRatio=.50`独立于自动补员上限 | 由专题01四参数唯一读取；此处不增独立比例 |

## 4. Model/Service边界

建议保持当前`IExternalReinforcementNeedModel.Evaluate(GarrisonDefenseSnapshot)`契约；Snapshot增添/规范为`EffectiveGarrisonCount`与`MinimumCount`，不能让`CurrentRegularCount`和`EscortReservedCount`在不同方法中反复自行加总。

```csharp
public ReinforcementNeedResult Evaluate(GarrisonDefenseSnapshot s)
{
    if (s == null || s.Settlement == null)
        return ReinforcementNeedResult.NoRequest("InvalidSettlement");

    int shortage = Math.Max(0, s.MinimumCount - s.EffectiveGarrisonCount);
    if (!s.IsEnemyBorder)
        shortage = Math.Max(0, shortage - s.PredictedSevenDayRecruitment);

    return shortage == 0
        ? ReinforcementNeedResult.NoRequest("MinimumCovered")
        : new ReinforcementNeedResult {
            IsRequested = true, RequestedTroops = shortage,
            ReasonCode = s.IsEnemyBorder
                ? "BorderBelowMinimum" : "InteriorBelowMinimumAfterForecast"
        };
}
```

以上仅为伪签名草案；实际修改时统一DTO字段，避免混用旧`CurrentDefenseValue`浮点字段与新整数；不要求改原版军事AI。

## 5. 实施步骤

1. 依赖01的四参数Model，新增/修改Snapshot字段；不要在外援Model重复判边境。
2. 改外援需求计算，边境不再执行或消费七日预测；可选保留预测信息仅用于日志诊断，但不影响`RequestedTroops`。
3. 同步来源安全调出人数，重复前的预审批与最终取兵必须调用同一源地最低保留规则。
4. 日志保持只输出玩家王国的展示范围可以接受；后续**实际全AI增援评估不能被玩家王国的日志过滤条件限制**。
5. 以只读Snapshot逐城输出请求/拒绝的ReasonCode；本专题不添加运输目标，不打开`EnableAutomaticReinforcement`。

## 6. 验收测试

| 情境 | 预期请求 |
|---|---:|
| 边境Hard300、Effective200、当地每日能补4 | **25人**，不减4×7 |
| 边境Hard300、Effective225、日产0 | 0 |
| 边境Hard300、Resident180、征粮50、Effective230 | 0；不能当成180来计算 |
| 内陆Hard300、Effective120、近似未来七天能补14 | 16 |
| 边境来源Hard300、Effective285、城内Roster充足 | 可安全调出最多60人 |
| 非边境来源Hard300、Effective240 | 可安全调出最多90人 |
| 玩家王国之外的AI Clan | Model仍能查询；只是日志可维持现有显示范围 |

## 7. 源码及说明

- [ForceManagementModels.cs](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs)
- [ForceManagementBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/ForceManagementBehavior.cs)
- [当前实现说明](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/file/AI兵力管理驻军需求当前实现.md)
