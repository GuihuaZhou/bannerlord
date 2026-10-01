# ModifiedArmy 当前招募限制机制

## 1. 结论

当前代码存在一套统一的 AI 招募评估模型，但它不是覆盖所有“士兵进入部队”路径的全局闸门。

统一模型能够保证：在同一次招募计划的快照中，经它批准的普通士兵不会让预计人数超过当时的 `PartySizeLimit`。但以下情况仍可能造成实际超编：

1. 部分玩家招募入口仍绕过统一模型或缺少最终人数校验；
2. 招募计划使用创建时的快照，执行前没有统一的最终复核；
3. 部队仍可通过招募之外的方式获得人员；
4. `PartySizeLimit` 会在人员已经加入后动态下降；
5. “因编制超编”日志只是逃兵发生后的原因推断，不是原版逃兵系统传入的精确原因。

因此，看到该日志不代表统一招募模型本身一定批准了越界招募。

## 2. 统一 AI 招募模型

核心实现：

- `Recruitment/Models/DefaultAIRecruitmentModel.cs`
- `Recruitment/Models/RecruitmentSimulationState.cs`
- `Recruitment/Models/RecruitmentBudget.cs`
- `Recruitment/Models/RecruitmentPlan.cs`
- `Recruitment/Models/RecruitmentEvaluationResult.cs`

### 2.1 计划快照

建立计划时，`RecruitmentSimulationState.Create` 一次性记录：

- 当前 `PartySizeLimit`；
- 当前 `NumberOfAllMembers`；
- 各战斗角色人数；
- 各质量等级人数；
- 当前长期工资；
- 工资上限、即时购买资金和三十日维持资金。

这些值在一个计划内保持一致，不会随真实部队状态自动刷新。

### 2.2 候选排序

候选兵种按以下因素排序：

1. 战斗角色相对模板最低比例的缺口；
2. 质量等级相对模板最低比例的缺口；
3. 兵种阶级带来的小幅优先级；
4. 雇佣兵具有小幅优先级惩罚。

### 2.3 单个候选的数量限制

每个候选兵种的最终批准数量为下列数值的最小值：

```text
RecruitableCount = min(
    RequestedCount,
    AllowedByPartySize,
    AllowedByCombatRole,
    AllowedByQuality,
    AllowedByWageLimit,
    AllowedByRecruitmentCost,
    AllowedByMaintenance
)
```

各限制含义如下：

| 限制 | 计算含义 |
|---|---|
| PartySize | `PartySizeLimit - ProjectedMemberCount` |
| CombatRole | 该角色人数不得超过模板中相对于 `PartySizeLimit` 的最高比例 |
| Quality | 该质量等级人数不得超过模板中相对于 `PartySizeLimit` 的最高比例 |
| WageLimit | 预计工资不得超过部队工资支付上限的 90% |
| RecruitmentCost | 执行交易的人必须能够支付即时招募费用 |
| MaintenanceFunds | Clan 共享预算必须承担招募费用和新增士兵未来 30 日工资 |

每批准一个候选，模拟状态立即增加预计人数、工资、成本和构成计数。因此，同一计划后面的候选不能重复占用前面已经预留的空间或资金。

### 2.4 95% 硬阈值

统一模型在评估每个候选前检查预计人数比例。只要当前预计人数严格超过 `PartySizeLimit` 的 95%，该候选立即以 `PartySize` 为原因被拒绝。

领主定居点招募入口也执行同样的前置检查并直接返回 `false`。比例恰好等于 95% 时仍允许建立计划；一次已批准的招募可以把人数推到 95% 以上，之后同一计划中的后续候选和后续招募都会被拒绝。

### 2.5 限制的边界

统一模型只保证“计划快照内部”不超出快照时的上限：

```text
AllowedByPartySize = max(0, SnapshotPartySizeLimit - ProjectedMemberCount)
```

它不是持续监控器，也不会自动拦截所有对 `MemberRoster` 的写入。

## 3. 当前入口覆盖情况

| 人员进入路径 | 是否经过统一模型 | 是否有额外人数限制 | 结论 |
|---|---:|---:|---|
| AI 领主招募职业兵池 | 是 | 执行前仅依赖计划批准数量 | 正常情况下受限 |
| AI 领主招募雇佣兵 | 是 | 使用计划批准数量 | 正常情况下受限 |
| AI 领主征召封邑兵 | 是 | 入口先计算剩余空间 | 正常情况下受限 |
| 驻军招募职业兵池 | 是 | 每日上限同时受空余位置限制 | 受限较完整 |
| 驻军从定居点俘虏中招募 | 是 | 每日上限与计划共同限制 | 受限较完整 |
| 解散部队并入驻军的普通士兵 | 是 | 只接收计划批准数量 | 普通士兵受限 |
| 解散部队并入驻军的英雄 | 否 | 转移完成后立即执行既有驻军模板检查 | 英雄仍不会被模板检查删除 |
| AI 部队转化随军俘虏 | 是 | 受 95% 阈值、编制、构成、工资与预算限制 | 已纳入统一机制 |
| 商队招募酒馆雇佣兵 | 否 | 只在循环开始前检查一次是否已满 | 循环内可能越过上限 |
| 玩家从职业兵池招募 | 否 | 只校验金钱和兵池库存 | 明确缺口 |
| 玩家手动征召封邑兵 | 否 | `RecruitManualSelection` 未校验目标部队剩余位置 | 明确缺口 |
| 玩家招募俘虏 | 否 | 当前补丁主要处理事件和费用，不提供全局人数闸门 | 依赖界面或原版调用方 |

另外，原版驻军自动从名流槽位招募的行为已被 `GarrisonRecruitmentPatch` 禁用，改由本模组的驻军职业兵池行为处理。

## 4. 已确认的超编风险

### 4.1 AI 随军俘虏转化已接入统一模型

`Recruitment/Patches/PrisonerRecruitmentPatch.cs` 会把候选俘虏交给统一模型，只转化 `RecruitableCount` 批准的数量，并提交招募费用和三十日工资预算。

它现在受 95% 硬阈值、绝对人数上限、兵种构成、质量构成、工资上限、即时费用与维持资金共同约束。

### 4.2 商队雇佣兵循环只检查一次

`AiSettlementRecruitmentPatch.TryRecruitCaravanMercenary` 在进入循环前检查一次：

```text
NumberOfAllMembers >= PartySizeLimit
```

之后可能在同一个循环中多次招募，每次成功后没有重新计算剩余位置。例如只剩 1 个位置但连续成功 3 次，就可能超过上限 2 人。

### 4.3 玩家职业兵池结算没有人数校验

`Recruitment/Pools/UI/PlayerRecruitmentPoolPatch.OnDonePrefix` 会校验：

- 是否属于玩家定居点；
- 玩家金钱是否足够；
- 兵池库存是否足够。

随后直接把整个购物车加入主角部队，没有对 `PartySizeLimit - NumberOfAllMembers` 做最终校验。

### 4.4 手动封邑征召没有人数校验

AI 使用的 `RecruitTroopsToParty` 会先计算剩余空间并经过统一模型；但玩家手动入口 `RecruitManualSelection` 直接处理所选 roster，最终整体加入目标部队。该方法内部没有重新检查目标部队剩余位置。

### 4.5 部队并入驻军后立即整编

解散部队并入驻军时，普通士兵经过统一模型，英雄成员直接转移；完成转移后立即调用与每周检查相同的 `NormalizeGarrison` 逻辑。

该现有检查会删除非军事普通角色，并按驻军模板修正战斗角色和质量等级的最高比例。它不会删除英雄，也不会单独依据总人数上限裁掉合法军事单位，因此英雄仍可能占用额外位置。

### 4.6 上限可能在招募后下降

`PartySizeLimit` 并非常量。领主部队使用原版动态计算；驻军上限还取决于：

- 基础驻军容量；
- 城镇固定加成；
- 所有者领袖的领导技能效果；
- 所有者相关 Perk；
- 定居点建筑的驻军容量效果。

定居点易主、领袖变化、技能或 Perk 条件变化、建筑状态变化等，都可能让当前上限低于此前合法招募时的上限。此时没有发生越界招募，部队仍会在下一次原版逃兵检查时表现为超编。

## 5. 为什么日志会显示“因编制超编”

该日志来自 `PartyFinance/Behaviors/PartyPersonnelLossLogBehavior.cs`，它只监听原版 `OnTroopsDesertedEvent`，并不负责决定或执行逃兵。

事件没有向该行为提供精确原因，所以代码在逃兵发生后按下列优先级推断原因：

1. 饥饿；
2. 未支付工资；
3. 超过工资上限；
4. 逃兵前人数超过当前 `PartySizeLimit`；
5. 士气低于阈值；
6. 未知。

逃兵前人数按以下方式重建：

```text
membersBeforeDesertion = currentMembersAfterDesertion + desertedCount
```

只要这个重建值大于事件发生时的当前人数上限，日志就显示“编制超编”。这只能证明逃兵前人数高于当时上限，不能证明：

- 是哪一次操作造成超编；
- 超编来自招募还是上限下降；
- 原版逃兵模型本次实际选择士兵的唯一原因就是超编。

多个条件同时成立时，日志采用上述固定优先级，因此它是诊断性推断，而不是权威原因记录。

## 6. 最可能的来源排序

对于频繁出现的领主部队超编日志，建议优先排查：

1. 部队人数上限动态下降；
2. 其他原版事件、任务或未接入统一模型的人员转移；
3. 招募计划建立后、实际执行前部队状态发生变化；
4. 英雄成员转移，因为驻军模板检查不会删除英雄。

对于玩家主部队，优先排查：

1. 职业兵池购物车一次性结算；
2. 手动封邑兵征召；
3. 玩家俘虏招募界面是否在所有情况下正确限制人数。

对于商队，优先排查同一次酒馆招募循环中的连续成功次数。

## 7. 建议的修正方向

如果目标是保证任何招募都不会造成超编，应当增加一个执行时的统一最终闸门，而不能只依赖计划阶段：

```text
actualCount = min(
    requestedOrApprovedCount,
    max(0, party.Party.PartySizeLimit - party.Party.NumberOfAllMembers)
)
```

该校验应在每次真实写入 `MemberRoster` 之前执行，至少覆盖：

- 商队每一次循环招募；
- 玩家职业兵池结算；
- 玩家手动封邑征召；
- 英雄或普通士兵的部队合并与转移。

同时建议在超编日志中加入以下诊断字段，才能确认根因：

- 逃兵前人数；
- 当前人数上限；
- 超编差值；
- 是否为驻军；
- 最近一次成员增加来源；
- 最近一次记录的人数上限及其变化量。

若只增加最终容量闸门，仍无法消除“合法招募后上限下降”造成的超编；这类情况需要单独决定是允许原版逃兵、提前裁军，还是对某些上限变化设置缓冲。
