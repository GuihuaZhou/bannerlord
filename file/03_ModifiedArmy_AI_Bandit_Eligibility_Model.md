# W3｜AI野怪统一招募资格过滤——详细修改设计与实施方案

> 状态：待实施；基线`main@ccd222b`；Owner：Worker W3；**W4必须复用本任务的Policy**。  
> 依据：资料库《ModifiedArmy_AI_Bandit_Recruitment_and_Rescued_Prisoner_Filter_Implementation.md》第0–2章、第4–7章；本文件按最新代码调整实际文件/函数。

## 1. 目的与现状

**目的：** 不论普通AI领主从何种候选来源通过统一招募Model获得士兵，野怪候选一律审核为0；AI自动驻军亦遵守相同准入。已有局部的俘虏招募白名单保留不变。主角手动行为、原版战利品/战斗分配、已入伍野怪及不属于正规AI部队的匪帮/商队/民兵保持原状。

当前代码确认：

- `Recruitment/Models/DefaultAIRecruitmentModel.cs`的 `DefaultAiTroopRecruitmentModel.EvaluateRecruitment()` 基础资格只排除 `party == null/notActive`、`troop == null/IsHero`、`availableCount<=0`，**无通用Bandit判断**。
- `Recruitment/Patches/PrisonerRecruitmentPatch.cs` → `RecruitPrisonersAiPatch.Prefix` 现有白名单：`Occupation.Mercenary || Occupation.Soldier`，且 `return false` 跳过原版方法；这条已阻止普通 `Occupation.Bandit` 通过此入口入伍。
- `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`的`IsRegularGarrisonTroop()`只接受 Soldier/Mercenary，且留用时要求Tier>=3。
- 其他来源可能直接调用统一 `RecruitmentModelManager.Model.BuildPlan()`；需要通用Model层防线，不能只在俘虏Prefix做局部排除。
- **战后解救野怪直接进入MemberRoster不走统一Model**，属于W4，不由本文件修复。

## 2. 准入规则（严格）

**野怪身份：**

```csharp
bool IsBanditTroop(CharacterObject troop) =>
    troop != null &&
    (troop.Occupation == Occupation.Bandit ||
     troop.Culture?.IsBandit == true);
```

必须同时覆盖`Occupation.Bandit`和`Culture.IsBandit`；**禁止汉化名字硬编码**、禁止粗暴把所有`Occupation != Soldier`都当野怪（可能误伤合法雇佣兵）。Worker需要检查本体及`ModifiedTroops`模板实际属性，记录样本。

**受规则约束的接收方：**

- 非主角、`party.IsLordParty==true` 的**正规AI领主Party**：包含玩家Clan的非主角AI领主Party。
- `party.IsGarrison`且属于**AI自动管理**的驻军：遵循现有驻军自动招募开关和所有权；不限制玩家手动接收/调整驻军。
- 玩家主Party一律不因本规则新增限制；野怪Party、民兵、商队、村民等非本次正规军类型不新增限制。

**玩家Clan驻军边界：** 原资料库文档提供`party.CurrentSettlement?.OwnerClan != Clan.PlayerClan`作为第一版建议，并明确这是“玩家驻军由玩家手动管理”的假设，不是已核实事实。Worker应优先依据当前`GarrisonAutoRecruitmentIsEnabled`和实际调用来源确定“自动管理”语义；如现有代码不能可靠区分，则**第一版保留玩家Clan驻军不受新增拦截**并在PR报告需后续明确的边界。不要因为本功能更改玩家驻军的手动权限。

## 3. 修改文件清单

| 文件 | 改动 |
|---|---|
| **新增** `Recruitment/Models/RecruitmentTroopEligibilityPolicy.cs` | 静态共享函数`IsBanditTroop`、`ShouldExcludeBanditForAiRecruitment`、可复用`IsRegularAiLordParty`（供W4） |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` | 在`EvaluateRecruitment`基础合法性之后、编制和财务计算之前新增硬拒绝 |
| `Recruitment/Models/RecruitmentTypes.cs` | `RecruitmentLimitReason` **末尾追加** `BanditTroopExcluded` |
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | `GetLimitText`添加拒绝原因文本；原`IsRegularGarrisonTroop`保持不变 |
| `ModuleData/submodule_strings.xml` | 新增与当前资源结构匹配的文字键：“禁止AI招募野怪” |
| 其他实际引用`RecruitmentLimitReason`的日志switch（通过代码搜索核对） | 若有显示/诊断映射则一并补充；**不要假定旧文档提及的 `AiSettlementRecruitmentPatch` 仍有该switch**，最新文件很短 |

## 4. 具体实现步骤

### 4.1 共享Policy接口

建议代码骨架（根据项目namespace/usings调整）：

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    public static class RecruitmentTroopEligibilityPolicy
    {
        public static bool IsBanditTroop(CharacterObject troop)
        {
            return troop != null &&
                (troop.Occupation == Occupation.Bandit ||
                 troop.Culture?.IsBandit == true);
        }

        public static bool IsRegularAiLordParty(MobileParty party)
        {
            return party != null && party.IsActive &&
                   !party.IsMainParty && party.IsLordParty;
        }

        public static bool ShouldExcludeBanditForAiRecruitment(
            MobileParty party, CharacterObject troop)
        {
            if (party == null || !IsBanditTroop(troop) ||
                party.IsMainParty)
                return false;
            if (party.IsLordParty) return true;
            if (party.IsGarrison)
            {
                // 与AI自动驻军管控范围对齐；第一版保守跳过玩家Clan。
                return party.CurrentSettlement != null &&
                    party.CurrentSettlement.OwnerClan != Clan.PlayerClan;
            }
            return false;
        }
    }
}
```

**实现注意：** 以上为示意，`party.IsGarrison`场景需核对`CurrentSettlement`是否总能正确反映驻地；若会为null，则判断应以现有可靠的`HomeSettlement`/所属定居点属性取值，不能导致AI驻军悄悄漏拦，也不能误拦玩家驻军。确保W4只使用`IsBanditTroop`和`IsRegularAiLordParty`，不会意外用自动招募的驻军开关限制战后解救。

### 4.2 Model准入拦截

在`DefaultAiTroopRecruitmentModel.EvaluateRecruitment()`以下判断后：

```csharp
if (troop == null || troop.IsHero || availableCount <= 0)
{
    result.PrimaryLimit = RecruitmentLimitReason.InvalidTroop;
    return result;
}
```

紧接着新增：

```csharp
if (RecruitmentTroopEligibilityPolicy
        .ShouldExcludeBanditForAiRecruitment(party, troop))
{
    result.RecruitableCount = 0;
    result.PrimaryLimit = RecruitmentLimitReason.BanditTroopExcluded;
    return result;
}
```

必须在`ForceManagementFacade.Current.RecruitmentAdmission.Evaluate`、`GetAllowedByWageLimit`、购买成本和财政预算等后续流程之前返回；不得以`Priority=-100`替代硬拒绝。`RecruitmentEvaluationResult`其他字段沿用创建时的零值即可；如果需要区分拒绝原因，`PrimaryLimit`必须保持新枚举。

### 4.3 枚举及本地化

当前`Recruitment/Models/RecruitmentTypes.cs`的枚举依序为 `None, InvalidParty, InvalidTroop, PartySize, WageLimit, RecruitmentCost, MaintenanceFunds`。**在最后一个值后追加**：

```csharp
BanditTroopExcluded
```

不要插在中间，也不要无理由改已有枚举值。现有`GarrisonRecruitFromPrisonersBehavior.GetLimitText`为switch，新增：

```csharp
case RecruitmentLimitReason.BanditTroopExcluded:
    return GameTexts.FindText("str_modifiedarmy_recruit_limit_bandit");
```

在实际`ModuleData/submodule_strings.xml`相应语言节点新增资源键；日志优先`Debug/FileNotice`并复用`RecruitmentLogFilter`，避免大量UI弹窗。

### 4.4 与既有入口的关系

- `PrisonerRecruitmentPatch`里的 Soldier/Mercenary 白名单**保留**；俘虏归顺所需`Conformity`、扣费、预算、士气和`OnTroopRecruited`完全不变。
- 驻军俘虏留用/出售路径及`Tier >= 3`继续有效，不顺手修改政策。
- `RecruitmentSource.Transfer`、`Fief`、`ProfessionalProduction`、`Mercenary`通过同一模型时受统一Bandit过滤（前提是接收Party在范围内）；野怪候选不被消费，实际交易由现有Service执行。
- **已有Party中的野怪不主动删除**，只是阻止新的招募候选被批准。
- 当前`BuildPlan`用`RecruitableCount`合计形成批准量；过滤前后除了野怪候选之外，应产生相同结果。

## 5. 必须避免的错误实现

1. 全局Harmony Patch `TroopRoster.AddToCounts`或`MapEvent.CalculateAndCommitMapEventResults`：会误伤战利品/玩家UI，不允许。
2. 只过滤`Occupation.Bandit`，忽略`Culture.IsBandit`：不能满足文档中非标准野怪模板需求。
3. 用自动清理任务删玩家/历史存档现有野怪：明确范围外。
4. 删除`RecruitPrisonersAiPatch`或把它换成全局Model：会丢失原有招募费用、Conformity和事件。
5. 把玩家主Party的手动招募改为拒绝：范围外。
6. 直接变动驻军/Army征召目标以“避免出现野怪”：跨任务，禁止。

## 6. 验收矩阵

| ID | 案例 | 预期 |
|---|---|---|
| A01 | AI领主俘虏3名`Occupation.Bandit`且已足够Conformity | 统一Model批准0；仍在PrisonRoster |
| A02 | 非俘虏`BuildPlan`提供5名Bandit候选 | 批准0，资源未被消耗 |
| A03 | `Occupation.Soldier`但所属Culture标记`IsBandit=true` | 按野怪拒绝；记录具体模板属性 |
| A04 | AI自动驻军收到野怪候选 | 资格拒绝；不影响保留/出售既有处理 |
| A05 | Soldier/Mercenary候选正常 | 同条件审批人数、工资和成本不变 |
| A06 | 玩家主Party手动招募Bandit | 与修改前完全一致 |
| A07 | 玩家Clan其他AI领主Party招募Bandit | 拒绝 |
| A08 | 匪帮/商队/民兵/玩家手动驻军行为 | 不受新增规则影响 |
| A09 | Party已拥有2名历史Bandit | 不自动移除、不修改存档 |
| A10 | 预算不足的Soldier及Bandit同时存在 | Soldier仍按原财政限制；Bandit以`BanditTroopExcluded`拒绝 |

## 7. 交付及依赖说明

- W3应先提交并公开Policy API，W4在该API基础上开发，**不新建第二套野怪判断函数**。
- 至少验证“Model零批准不会扣除银币、俘虏、职业兵池或封邑批次”。
- PR附模型拒绝实例、实际兵种属性核对清单、旧存档与加载结果。
- 提交建议：`fix(army): reject bandit candidates in unified AI recruitment`。
- 无游戏实测条件时标注“仅静态检查／编译”，不能宣称战后过滤已完成；战后部分由W4单独验收。
