# AIRecruitmentModel 统一招募模型设计

## 1. 模型定位

新增：

```text
AIRecruitmentModel
```

负责统一处理 AI 的招募数量判断。

所有兵源：

```text
志愿兵
采邑兵
雇佣兵
其他以后新增的兵源
```

在真正加入 Party 前，都调用同一个 Model。

调用方不再自己判断：

```text
空间
军队组成
工资
招募费用
```

而只负责：

```text
发现候选兵
→ 调用 AIRecruitmentModel
→ 根据返回数量执行招募
```

---

## 2. 核心接口

建议：

```csharp
public abstract class AIRecruitmentModel : GameModel
{
    public abstract RecruitmentEvaluationResult EvaluateRecruitment(
        MobileParty party,
        CharacterObject troop,
        int availableCount,
        RecruitmentSource source);
}
```

其中：

```text
party = 要招兵的AI Party
troop = 候选兵种
availableCount = 当前兵源最多可以提供多少人
source = 兵源类型
```

---

## 3. RecruitmentSource

```csharp
public enum RecruitmentSource
{
    Volunteer,
    Fief,
    Mercenary
}
```

以后可以扩展：

```text
Prisoner
Special
Event
```

`RecruitmentSource` 不决定兵员身份，真正的兵员身份仍由 `troop` 判断。

---

## 4. 返回结果

建议不要只返回 `int`，而是：

```csharp
public class RecruitmentEvaluationResult
{
    public int RequestedCount;
    public int RecruitableCount;

    public int PartySizeLimit;
    public int SoldierClassLimit;
    public int CombatRoleLimit;
    public int WageLimit;
    public int GoldLimit;
}
```

例如：

```text
RequestedCount = 12
PartySizeLimit = 20
SoldierClassLimit = 5
CombatRoleLimit = 8
WageLimit = 6
GoldLimit = 10
RecruitableCount = 5
```

这样日志可以明确显示最终限制来自哪里。

---

## 5. Model 内部判断顺序

```text
候选兵 + 数量
↓
Party人数限制
↓
兵员身份限制
↓
战斗功能限制
↓
工资限制
↓
招募费用限制
↓
返回最终数量
```

最终：

```text
RecruitableCount
=
min(
    availableCount,
    AllowedByPartySize,
    AllowedBySoldierClass,
    AllowedByCombatRole,
    AllowedByWage,
    AllowedByGold
)
```

---

## 6. Party 人数限制

```text
AllowedByPartySize
=
PartySizeLimit
-
NumberOfAllMembers
```

如果：

```text
AllowedByPartySize <= 0
```

直接：

```text
RecruitableCount = 0
```

---

## 7. 兵员身份判断

候选兵划分为四类，互斥：

```text
Militia
Sergeant
Retainer
Mercenary
```

例如：

```csharp
SoldierClass soldierClass =
    SoldierTypeClassifier.GetSoldierClass(troop);
```

---

## 8. 兵员身份规则

每类拥有：

```text
TargetRatio
MaxRatio
MaxCount
```

示例：

```text
Militia
TargetRatio = 0.55
MaxRatio = 0.70
MaxCount = -1

Sergeant
TargetRatio = 0.25
MaxRatio = 0.35
MaxCount = -1

Retainer
TargetRatio = 0.10
MaxRatio = 0.20
MaxCount = 30

Mercenary
TargetRatio = 0.10
MaxRatio = 0.20
MaxCount = 30
```

其中：

```text
TargetRatio = 理想结构
MaxRatio = 比例硬上限
MaxCount = 绝对人数硬上限
```

---

## 9. 比例允许数量

设：

```text
C = 当前Party总人数
X = 当前该类别人数
R = MaxRatio
N = 新招人数
```

要求：

```text
(X + N) / (C + N) <= R
```

因此：

```text
AllowedByRatio
=
floor(
    (R × C - X)
    /
    (1 - R)
)
```

绝对人数：

```text
AllowedByCount
=
MaxCount - X
```

最终：

```text
AllowedBySoldierClass
=
min(
    AllowedByRatio,
    AllowedByCount
)
```

如果：

```text
MaxCount = -1
```

则忽略绝对人数限制。

---

## 10. 战斗功能判断

候选兵同时划分为：

```text
Infantry
Ranged
HorseArcher
Cavalry
```

例如：

```csharp
CombatRole role =
    SoldierTypeClassifier.GetCombatRole(troop);
```

每种功能同样拥有：

```text
TargetRatio
MaxRatio
MaxCount
```

示例：

```text
Infantry
TargetRatio = 0.45
MaxRatio = 0.60

Ranged
TargetRatio = 0.25
MaxRatio = 0.40

HorseArcher
TargetRatio = 0.10
MaxRatio = 0.20

Cavalry
TargetRatio = 0.20
MaxRatio = 0.35
```

计算方式和兵员身份完全一致。

---

## 11. 工资限制

工资判断统一放到 Model 中。

```text
CurrentTotalWage
=
party.TotalWage
```

```text
PaymentLimit
=
party.PaymentLimit
```

建议给 AI 留一定余量：

```text
WageUsageRatio = 0.90
```

因此：

```text
RecruitmentWageBudget
=
PaymentLimit × WageUsageRatio
-
CurrentTotalWage
```

---

## 12. 候选兵实际工资

基础工资：

```text
BaseWage
=
PartyWageModel.GetCharacterWage(troop)
```

如果当前 Party 是战争中的 `WarParty`：

```text
EffectiveWage
=
BaseWage × WarWageMultiplier
```

当前：

```text
WarWageMultiplier = 1.5
```

否则：

```text
EffectiveWage = BaseWage
```

雇佣兵额外工资已经包含在 `GetCharacterWage(troop)` 中，不再重复计算。

最终：

```text
AllowedByWage
=
floor(
    RecruitmentWageBudget
    /
    EffectiveWage
)
```

---

## 13. 招募费用限制

单人招募费用：

```text
RecruitmentCost
=
PartyWageModel.GetTroopRecruitmentCost(
    troop,
    party.LeaderHero,
    false
)
```

可用资金：

```text
AvailableGold
=
party.PartyTradeGold
```

建议保留一定现金：

```text
GoldUsageRatio = 0.75
```

因此：

```text
RecruitmentGoldBudget
=
AvailableGold × GoldUsageRatio
```

然后：

```text
AllowedByGold
=
floor(
    RecruitmentGoldBudget
    /
    RecruitmentCost
)
```

采邑兵如果没有金币招募成本，则：

```text
AllowedByGold = availableCount
```

---

## 14. TargetRatio 负责优先级

`TargetRatio` 不作为硬限制，而是决定当前是否需要该兵种。

```text
ClassNeed
=
TargetClassRatio
-
CurrentClassRatio
```

```text
RoleNeed
=
TargetRoleRatio
-
CurrentRoleRatio
```

然后：

```text
RecruitmentPriority
=
ClassNeed
+
RoleNeed
```

例如：

```text
民兵缺口 = +0.15
射手缺口 = +0.10
```

则一个：

```text
Militia + Ranged
```

的候选兵：

```text
Priority = 0.25
```

---

## 15. Model 提供两个接口

### 数量评估

```csharp
EvaluateRecruitment(
    party,
    troop,
    availableCount,
    source
)
```

回答：

```text
最多招几个？
```

### 招募优先级

```csharp
GetRecruitmentPriority(
    party,
    troop,
    source
)
```

回答：

```text
当前有多需要这个兵？
```

调用流程：

```text
多个候选兵
↓
先计算Priority
↓
从高到低排序
↓
再调用EvaluateRecruitment
↓
得到RecruitableCount
↓
执行招募
```

---

## 16. RecruitmentCampaignBehavior 职责

以后 `RecruitmentCampaignBehavior` 不再负责复杂判断。

例如现在的：

```text
TryRecruitLordMercenary()
```

简化为：

```text
获取酒馆雇佣兵
↓
得到 troopType
↓
得到 availableCount
↓
调用 AIRecruitmentModel
↓
得到 RecruitableCount
↓
执行 ApplyRecruitMercenary()
```

原来的：

```text
Party空间判断
金币判断
工资判断
```

统一移入：

```text
AIRecruitmentModel
```

---

## 17. 志愿兵

现在 `RecruitVolunteersFromNotable()` 中的：

```text
PartyTradeGold判断
GetAvailableWageBudget判断
```

逐渐移到 Model。

以后：

```text
发现 Volunteer
↓
AIRecruitmentModel.EvaluateRecruitment(
    party,
    volunteer,
    1,
    Volunteer
)
↓
返回0 → 跳过
返回1 → 招募
```

---

## 18. 采邑兵

先通过：

```text
GetAvailableTroopCount(settlement)
```

取得：

```text
availableCount
```

然后：

```text
AIRecruitmentModel.EvaluateRecruitment(
    party,
    troop,
    availableCount,
    Fief
)
```

Model 负责判断：

```text
军队结构
Party空间
工资
```

采邑兵如果免招募费，则跳过金币限制。

---

## 19. 推荐代码结构

```text
ModifiedArmy/
└─ Models/
   └─ Recruitment/
      ├─ AIRecruitmentModel.cs
      ├─ DefaultAIRecruitmentModel.cs
      ├─ ArmyCompositionTemplate.cs
      ├─ RecruitmentEvaluationResult.cs
      └─ RecruitmentSource.cs
```

职责：

```text
AIRecruitmentModel
= 抽象接口

DefaultAIRecruitmentModel
= 实际计算

ArmyCompositionTemplate
= 身份/功能比例规则

RecruitmentEvaluationResult
= 计算结果

RecruitmentSource
= 招募来源
```

---

## 20. Campaign 注册

由：

```text
DefaultAIRecruitmentModel
```

作为正式实现加入：

```text
CampaignGameStarter.AddModel(...)
```

如果 Bannerlord 的 `CampaignModels` 不方便直接增加自定义属性，则也可以由 Mod 自己维护统一入口，例如：

```text
RecruitmentModelManager.Model
```

但设计上保持：

```text
AIRecruitmentModel
→ 唯一招募规则来源
```

---

## 21. 最终职责关系

```text
AiRecruitmentBehavior
=
决定现在有没有招兵需求
以及哪种兵源值得尝试
```

```text
RecruitmentCampaignBehavior
=
发现具体候选兵
执行实际招募
```

```text
AIRecruitmentModel
=
决定这个具体兵种最多可以招多少
以及招募优先级
```

```text
ArmyCompositionTemplate
=
定义什么叫一支合理的军队
```

最终流程：

```text
AI决定需要招兵
↓
到达Settlement
↓
发现候选兵
↓
AIRecruitmentModel评估
↓
身份结构
+ 功能结构
+ Party容量
+ 工资
+ 金币
↓
返回RecruitableCount
↓
RecruitmentCampaignBehavior执行招募
```
