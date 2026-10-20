# 01｜驻军理想编制、自动补员与征粮队预留

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 本专题交付目标

让所有城镇/城堡驻军的**自动新增、正常接兵、驻军提兵及容量校验**共享统一人数Model；不修改兵源生产本身、战术AI或跨城运输。优先完成本专题，再实施其他调兵方案。

## 1. 确定的设计规则

| 驻军类型 | 理想上限（自动补员及普通接兵） | 理想下限（抽兵后不得低于） | HardLimit=300示例 |
|---|---:|---:|---|
| 边境 | **95%** | **75%** | 上限285、下限225 |
| 非边境 | **80%** | **50%** | 上限240、下限150 |

- **上限**：职业兵/俘虏的自主补员目标、普通Party捐兵、Clan增援正常卸兵的容量线。
- **下限**：从该驻军向领主Party或增援运输Party调出正规兵的最低保留线；**不是自动补员停止线**。
- 100%硬编制仍是绝对合法性上限；既有士兵超过理想上限时不自动裁撤。征粮队返还、刚攻克后的捐兵、整Party解散要走受约束的例外政策，不能无条件适用普通接兵规则。
- 边境分类首版复用 `GarrisonTransferPolicyModel.IsEnemyBorderFortification()` 的邻接敌对堡垒判定，经单一Adapter输出，不在各处再各算一次。

## 2. **唯一**驻军人数口径

```text
ResidentCount = 驻军实际名册的成员数
EscortsReserved = 活跃、未完成的本驻军征粮队成员数
EffectiveCount = ResidentCount + EscortsReserved
MinimumCount = ceil(HardLimit × 本定居点下限比例)
MaximumCount = floor(HardLimit × 本定居点上限比例)
AutoRecruitmentGap = max(0, MaximumCount - EffectiveCount)
NormalReceivingSlots = max(0, MaximumCount - EffectiveCount)
PotentialTransferOut = max(0, EffectiveCount - MinimumCount)
ActualTransferOut = min(PotentialTransferOut, 城内名册可合法提取的正规士兵数量)
```

`EscortsReserved`复用现有 `GarrisonRecruitmentCapacity.GetActiveSupplyEscortCount(settlement)`，**不要再建第二套外出Party枚举**。不额外建立“实际留守战力”独立评价系统；真实转出时必须检查城内名册，不能把外出的征粮队再扣第二次。

示例：边境Hard300、城内235、外出征粮50 → Effective285，自动新增空间0、正常接兵空间0、人数允许抽出60（还受真实城内Roster约束）。城内190、征粮50→Effective240，最多补员45、最多抽调15。

## 3. 当前代码与必须修改点

| 现有文件/入口 | 当前状态 | 具体任务 |
|---|---|---|
| `common/MilitaryConfig.cs`及`ModuleData/militaryConfig.xml` | 独立`GarrisonAutoRecruitmentRatio=.80`、`ExistingTroopTransferRatio=.90`、`BorderDefenseRatio=.90`、`InteriorDefenseRatio=.50` | 改成唯一四个比例，解析器默认值与XML同步；旧键不得继续平行生效 |
| `Garrison/Models/GarrisonRecruitmentCapacity.cs` | 已有征粮人数和有效人数；自动补员目标仅接收HardLimit，不知道边境类型；正常转入用90% | 通过Settlement获得目标上限，保留当前容量API作为统一Model适配层 |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs`及`ForceManagement/Models/ForceManagementModels.cs` | 新招募和Transfer共用Admission，已有部分错误目标 | 接收方是驻军才按该驻军上限；接收方为Party必须转到Party目标策略（详见02） |
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 生产/俘虏真实执行二次调用SoftFreeSlots | 计划与执行查询**同一**类型上限，保留每日招募额度和财政限制 |
| `Garrison/Models/GarrisonTransferPolicyModel.cs` | 内陆仍有`InteriorIdealGarrisonRatio=0.40f × NativeIdealSize` | 审核这个本体适配是否与四参数冲突；不能用它覆盖新最低保留逻辑 |
| `Garrison/Supply/SupplyPartyBehavior.cs` | 回营时已有预留和归还流程 | 做回归，不改造成新运输Party；任务完成顺序与预留释放不能重复计数 |

## 4. Model/API建议

```csharp
public sealed class GarrisonIdealRange
{
    public bool IsBorder { get; set; }
    public int HardLimit { get; set; }
    public int MinimumCount { get; set; }
    public int MaximumCount { get; set; }
    public int ResidentCount { get; set; }
    public int ReservedEscortCount { get; set; }
    public int EffectiveCount { get; set; }
    public int RecruitmentGap { get; set; }
    public int ReceivingSlots { get; set; }
    public int PotentialTransferOut { get; set; }
}

public interface IGarrisonIdealStrengthModel
{
    GarrisonIdealRange Evaluate(Settlement settlement);
    int GetRecruitmentFreeSlots(Settlement settlement);
    int GetNormalReceivingSlots(Settlement settlement);
    int GetLegalTransferOutCount(Settlement settlement);
}
```

实现可放独立`ForceManagement/Models/GarrisonIdealStrengthModel.cs`，兼容旧路径的静态方法则向它转发。不要让配置值在Model、Patch和XML写三套。API示意需按项目`netstandard2.0`和实际依赖编译校验。

建议XML：

```xml
<BorderGarrisonMaximumRatio value="0.95" />
<BorderGarrisonMinimumRatio value="0.75" />
<InteriorGarrisonMaximumRatio value="0.80" />
<InteriorGarrisonMinimumRatio value="0.50" />
```

## 5. 分步实现

1. 引入四参数及解析校验：限制0～1，并保证各类型下限≤上限；删除独立80%自动补员与90%普通转入限制。旧XML如需兼容，显式采用新键优先。
2. 实现`GarrisonIdealStrengthModel`：只查询一次边境类型、硬上限、名册、现有征粮队预留，输出不可变决策快照（DTO）。
3. 现有`GarrisonRecruitmentCapacity`转成薄适配器；更改自动补员预测、BuildPlan与Execute的容量入口。
4. 正常取兵/交兵与Admission复用同一人数规则；特别注意Transfer的接收方类型，不能把驻军上限套用到领主Party。
5. 处理占领后捐兵、征粮队返还、整Party解散等受限例外（绝对不突破HardLimit）；异常日志写文件。
6. 对所有运行时使用旧比例、125/75固定门槛、内陆`0.40 × NativeIdeal`的入口做搜索并列明是否仍可合法运行；125/75寻址及Party保护详见02。

## 6. 验收（真实Roster变化与Model测试）

| 场景 | 必须结果 |
|---|---|
| 边境Hard300、有效224 | 自动补员目标285；最多仍可增加61（每日限制另计） |
| 内陆Hard300、有效150 | 自动补员**继续**，目标240，不在50%停下 |
| 边境Hard300、有效285 | 正常可抽至最低225，即人数上限60；不能无限制调空 |
| 内陆Hard300、有效240 | 正常最多调出90，抽后150 |
| 边境城内235、外出征粮50 | 有效285，自动补员和普通接收均停止；归队后人数不重复 |
| 边境城内190、外出征粮50 | 有效240，可补45、可抽15；被抽者必须真实存在于城内 |
| 超过理想上限 | 不新增、不普通接收、不裁撤；不能超过硬上限 |
| 读档、征粮队返还、刚占领据点 | 一次真实人数计入且政策例外不丢兵 |

## 7. 涉及源码

- [统一容量及征粮预留](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Garrison/Models/GarrisonRecruitmentCapacity.cs)
- [统一招募审批](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [配置解析器](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [当前军事配置](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
