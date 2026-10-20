# 04｜AI Party与驻军的局部双向换兵及兵种价值Model

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 本专题交付目标

在AI领主Party**自然到达其Clan城镇/城堡**时，允许根据合法性及收益对Party与驻军做有限兵种交换；不为纯战术构成优化专程跨城。此专题依赖01的上下限和02的兵源身份/财政政策；**未通过原子事务测试前，自动Swap保持关闭**。

## 1. 当前仓库状态

- `ForceManagement/Contracts/ForceManagementTypes.cs`已定义`ExchangePlan`和`IPartyGarrisonExchangeModel`。
- `ForceManagement/Models/ForceManagementModels.cs`已有`BasicTroopValueModel`，用`Tier×Count`做简化值；`DisabledPartyGarrisonExchangeModel`当前返回`NoExchange`。
- `ForceManagement/Services/TroopTransferService.cs`已处理部分Party→驻军的单向真实兵员转移；`GarrisonTransferPolicyModel`可执行取兵。**目前不存在安全完整的自动双向交换执行链**。

## 2. 确定的行为边界

1. **局部触发**：同Clan、无当前战斗/围城危险、Party正常在该定居点时评估；不建立跨城换兵任务。
2. **实际兵员交换**：执行前双方Roster、在役封邑批次、兵种、伤员、军饷与目标容量须全部通过预审。
3. **人数政策**：向驻军正常接收最多达到边境95%/内陆80%；驻军向外调出后不得低于边境75%/内陆50%；Party自身目标及硬限仍有效。
4. **价值与安全排序**：Clan总战力保护优先于局部完美构成；软兵种角色、质量级别、Tier可以参与排序，不能作为强制赶走高级兵员的理由。
5. **不自动裁军**：换兵失败不允许`Clear()`或转成神秘人力池；不改变服役批次来源。
6. **禁用默认值**：无完整交换策略时`BuildPlan()`可继续返回`NoExchange("AutomaticSwapDisabled")`，不准为了完成接口先返回“总是可以交换”。

## 3. 核心架构

```text
SettlementEntered / 经验证的自然到访
  -> ForceManagementFacade.Exchanges.BuildPlan(snapshot)
  -> TroopTransferService.ValidateExchangePlan(plan)
  -> TroopTransferService.ExecuteExchange(plan)
  -> 记录实际前后Roster差额与异常
```

Patch负责桥接，Model决定候选计划，Service负责事务，CampaignBehavior仅负责防重复/安全时间窗。避免在一个Harmony Prefix内写兵种打分和双向AddToCounts逻辑。

`ExchangePlan`推荐包含：Party/驻军、Clan、预计快照版本或指纹、两侧预期名册、`FieldToGarrison`和`GarrisonToField`清单、伤员数、可用工资额度、在役封邑预留、收益分数、过期/拒绝原因。

## 4. 两阶段事务及失败策略

**阶段A：生成计划（只读）**

- 只对双方真实持有兵员生成转移计划，排除英雄与不可合法转换的在役封邑批次。
- 在最终交换后名册上验证：Party硬编制及目标约束、驻军上限与下限、两端工资、伤员数、所有权、任务与战斗状态。
- **双向净结果**满足条件即可，不要先入再出导致临时超编而拒绝合法交换。

**阶段B：提交（原子/可回滚）**

- 临执行前重新读取两侧名册和Clan归属；任何重要前提变化则整笔放弃、重新计算。
- 优先采用可整体校验并回滚的名册交易；记录转移日志，包含具体兵种及伤员，不用`Clear()`清空失败。
- 同Clan既有兵迁移不提交新增征兵预算，财务只审核接收方局部负担变化。
- 做好异常注入测试：中途Roster变更、目标被围、Party突然加入Army、驻军达到上限、两个Party同日交换。

## 5. 兵种价值保持独立Model

```csharp
public interface ITroopValueModel
{
    float GetFieldMarginalValue(PartyForceSnapshot party,
                               CharacterObject troop, int count);
    float GetGarrisonMarginalValue(GarrisonDefenseSnapshot garrison,
                                  CharacterObject troop, int count);
}
```

首版允许以当前`Tier×Count`作为纯诊断值，但**不得仅凭这个分数启用高低级真实交换**。真正启用需评估Soft Role/Quality、边际防御收益和交换门槛，并要求可配置/可验证的正收益；复杂战术权重暂保留Stub，后续独立设计。

## 6. 实施次序

1. 升级`ExchangePlan`字段并写纯函数“构造/验证计划”；把01和02的合法性Model注入。
2. 在`TroopTransferService`中实现双向批处理执行与反向回滚/事务日志。
3. 验证正常转入上限、驻军调出下限、Party目标、在役封邑保护、军费和真实名册守恒。
4. 创建自然到访触发入口，保证同日不反向震荡、不和原有捐兵/返乡多次执行。
5. 所有验收通过后才把`EnableAutomaticSwap`由`false`作为一个独立上线决策；兵种评分不足时可只上线基础换兵API而不自动执行。

## 7. 验收矩阵

| 场景 | 预期 |
|---|---|
| 互换双方当前都接近上限 | 若净结果合法，不能因为临时先入后出失败 |
| 边境驻军Hard300、Effective225 | 不能净抽出任何普通兵员使其低于225 |
| 内陆驻军Hard300、Effective240 | 可净抽出至150，不能再低 |
| Party含同Character普通15+在役25 | 只允许普通15参与，服役记录不变 |
| 换兵收益不明/仅略高 | 不执行，默认`NoExchange` |
| 执行中前提被破坏 | 全部放弃或可靠回滚，无重复士兵 |
| 只为换兵远道而来 | **不发专程任务** |

## 8. 源码

- [交换契约](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/ForceManagementTypes.cs)
- [Disabled交换Model及价值Stub](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs)
- [转移Service](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Services/TroopTransferService.cs)
