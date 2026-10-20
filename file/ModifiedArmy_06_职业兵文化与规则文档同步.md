# 06｜职业兵文化判定、兵源生产规则和说明文档同步

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 本专题交付目标

以**当前已正常工作的生产代码为准**，修正不一致的文字说明和回归测试；**不要**因旧版文档表格不一致就重写生产算法。本专题可与01～05并行，单独完成。

## 1. 兵源生产现状（不重造旧兵池）

根据仓库`ModifiedArmy/file/兵源池与职业兵文化判定.md`及最新源码：

- 已删除长期积累的职业兵/封邑兵**库存池**。职业兵每日生成可用生产Offer，实际招募时进入目标Party；封邑兵每日分配到封邑部队。
- 保存小数生产进度和周期统计；历史存档键`_settlementRecruitmentPools`保留兼容意义，不代表长期士兵仓储。
- 当附属村庄`VillageState != Normal`时，`CalculateVillageSupply`对该村庄返回0；边境村庄被毁会显著限制本地供兵，内陆村庄完整则有更强补员能力。
- XML本地军事制度`MilitaryDoctrine`决定封邑兵/职业兵相对生产份额及文化的基本参数，雇佣兵不进入本地共享产能比例。

## 2. 五种文化组合（实际代码为准）

`S=Settlement.Culture`；`C=OwnerClan.Culture`；`K=OwnerClan.Kingdom.Culture`。

| S/C/K关系 | 采用的兵种模板文化 | 文化生产倍率 | 允许生产的最高兵营等级兵种 |
|---|---|---:|---:|
| A/A/A | A | 1.00 | 3 |
| A/A/B | A | 1.00 | 2 |
| A/B/A | A | 0.50 | 2 |
| **A/B/B** | A | **0.50** | **2** |
| A/B/C | A | 0.25 | 1 |

**A/B/B以代码为准开放2级**，不按旧文档改回1级。`OwnerClan.Kingdom`为null时，当前代码用Clan文化替代Kingdom文化参与判定；除缺少其他必要条件外，不无条件判定当天停产。

## 3. 兵种解锁和训练效率是两个独立计算

```text
AllowedTroopBarracksLevel = min(ActualBarracksLevel, CulturalMaximumLevel)
ProfessionalTrainingCapacity =
    (Training.base + Training.barracksStep × ActualBarracksLevel)
    × CulturalProductionRate
```

文化仅限制**可开放兵种所需兵营等级**，不截断实际兵营提供的效率加成。例如A/B/C且实际Barracks=3、文化最多开放1级兵种：只出一级兵种，但训练能力仍按三级建筑加成，再乘0.25倍率。**代码已有这一语义，不修改。**

## 4. 实际Offer权重算法：避免错误文档承诺

`BuildDailyProfessionalOffers()`按当前驻军的统一招募优先级、Role/Quality软需求、Tier等选择候选，XML兵种权重参与候选循环；源码**没有清晰按累计历史生产数量差额强制拉回XML长期比例**。旧文档如果宣称“累计产量按历史差额追平XML权重”，则需要把文字改成真实算法，或将长期权重平衡作为未来单独需求讨论。

**本批不授权**重写Offer挑兵策略、强制均衡高低级或改变已成功使用的文化生产倍率。

## 5. 同步修改的文档及边界

| 文件 | 修改内容 |
|---|---|
| `ModifiedArmy/file/兵源池与职业兵文化判定.md` | A/B/B最高2级；生产效率使用真实Barracks；解释Kingdom缺失回退；说明Offer实际选择策略 |
| `ModifiedArmy/file/AI兵力管理驻军需求当前实现.md` | **等专题01、03真实代码合并后**再将旧80%/90%/边境90%更新为实际95/75与80/50；不能提前把未实现的比例写成已实现 |
| 本专题及README索引 | 标记当前代码状态与文档状态；文档规则不是对已测试玩法的保证 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | **不修改**已确认正确的文化规则与训练计算；只做回归检查 |

## 6. 推荐实施流程

1. 阅读文化产兵文档及上述Production Behavior实际方法：`ResolveProfessionalProductionRule`、`GetDailyProduction`、`BuildDailyProfessionalOffers`、`GetProductionForecast`、`CalculateVillageSupply`。
2. 用代码逻辑逐项改文档表格与公式，不凭经验补写文化禁令。
3. 测试五种文化组合在Barracks=1/2/3时的解锁和生产速率；VillageState=Raided状态应降低有效共享供应。
4. 仓库有新增文化类型/城堡模板时重新核对Culture与模板ID，确保依旧取Settlement文化。
5. 单独提交**文档修订**；其余专题实际并入时再改驻军当前实现说明。

## 7. 验收

| 场景 | 必须行为 |
|---|---|
| A/B/B、Barracks3 | 生产倍率0.5，开放≤2级，训练能力仍用真实3级计算 |
| A/B/C、Barracks3 | 生产倍率0.25，开放≤1级，训练能力仍用真实3级计算 |
| A/A/A、Barracks3 | 文化倍率1.0，开放至三级 |
| 村庄非Normal | 该村的共享兵源贡献为0，不假装所有村庄仍正常生产 |
| Kingdom为null、Clan及Settlement文化有效 | 按当前代码的Clan文化回退逻辑判断，不错误归类为无条件0产量 |
| 不涉及文化变更的正常玩家驻军 | 无行为变更，因为只同步文档 |

## 8. 源码及参考

- [职业兵生产Behavior](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [生产模板Repo](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/TroopProductionTemplateRepository.cs)
- [当前文化文档](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/file/兵源池与职业兵文化判定.md)
- [现有配置](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
