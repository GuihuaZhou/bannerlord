# W4｜战后解救俘虏野怪过滤——详细修改设计与实施方案

> 状态：待实施；基线`main@ccd222b`；Owner：Worker W4；**依赖W3的`RecruitmentTroopEligibilityPolicy`**。  
> 依据：资料库《ModifiedArmy_AI_Bandit_Recruitment_and_Rescued_Prisoner_Filter_Implementation.md》第0–1、3–7章；本文件为可独立派工的战后专项方案。

## 1. 必须精确区分的两条战后路径

仓库内原版反编译`code/MapEvent.cs`，`CalculateAndCommitMapEventResults()`的正常结算链：

```csharp
LootDefeatedPartyCasualties(...);
LootDefeatedPartyItems(...);
LootDefeatedPartyPrisoners(winnerParties, defeatedParties); // A: 获救俘虏
LootDefeatedPartyShips(...);
CaptureDefeatedPartyMembers(winnerParties, defeatedParties); // B: 俘获败兵
```

- **路径A：** 敌方原本`PrisonRoster`中的非英雄俘虏，在获胜方分配战利品时，可能通过`MapEventParty.RosterToReceiveLootMembers`直接进入获胜AI的`MemberRoster`。该NPC属性getter直接指向`party.Party.MemberRoster`，**不经过W3统一招募Model**。
- **路径B：** 敌方自己参战的士兵因战败而成为俘虏，进入获胜方`PrisonRoster`。无论是不是Bandit，**仍应允许正常俘获**，不得过滤。

**业务目标：** 对正规AI领主Party，只有路径A中新解救的Bandit士兵**不加入MemberRoster**。他们按“获释”处理；获救正规兵继续原版分配；现有士兵、英雄处理及玩家战后UI完全不变。

## 2. 最小实现边界

**只新增一个局部Harmony补丁：**

`ModifiedArmy/ModifiedArmy/Recruitment/Patches/RescuedPrisonerFilterPatch.cs`

目标方法：

```text
TaleWorlds.CampaignSystem.MapEvents.MapEvent
    .LootDefeatedPartyPrisoners(
       MBReadOnlyList<MapEventParty> winnerParties,
       MBReadOnlyList<MapEventParty> defeatedParties)
```

这是非公开方法；目标游戏版本可能变化，Worker要用实际Assembly的反射签名核对。模组`Main.OnSubModuleLoad`已有 `Harmony.PatchAll(assembly)`，因此新增带`[HarmonyPatch]`的类即可参与扫描，不需要注册第二个Behavior，不单独重复`PatchAll`。

**不改：** `MapEvent.CalculateAndCommitMapEventResults()`、`CaptureDefeatedPartyMembers()`、`TroopRoster.AddToCounts()`全局实现、原版胜利者分配权重、战斗结算次序、俘虏赎金/出售/英雄释放。

## 3. 共用身份Policy和接收方范围

使用W3已落地的：

```csharp
RecruitmentTroopEligibilityPolicy.IsBanditTroop(troop);
RecruitmentTroopEligibilityPolicy.IsRegularAiLordParty(party);
```

**仅处理非玩家主Party的正规AI领主**；玩家Clan的非主角AI领主也包含在内。默认不处理匪帮、民兵、商队、玩家主Party、临时玩家战后选择Roster以及非普通AI部队。

与W3不同，战后是直接进入`MemberRoster`的**非招募Model入口**；不调用`ShouldExcludeBanditForAiRecruitment`中的自动驻军判定，避免自动招募与战利品接收语义混淆。若之后需覆盖普通AI驻军，应作为范围扩展并先核对其`RosterToReceiveLootMembers`实际上写入哪个Roster（`MapEventParty`可能转向Settlement或Garrison）。

## 4. Harmony Prefix/Postfix 快照差额

### 4.1 为什么采用差额纠正

原版没有“获救普通士兵入队前审核”可供简单拦截。如果复制`LootDefeatedPartyPrisoners`全部逻辑，会牵连英雄、玩家战利品、战利品概率及其他Mod。**保留原版执行，随后仅撤销本次新增的Bandit数量**，是局部且可回退的方案。

### 4.2 Prefix：记录执行前基线

- 针对`winnerParties`中每个`MapEventParty.Party.MobileParty`，筛选 `IsRegularAiLordParty`；同时核对该 `MapEventParty.IsNpcParty`。
- 通过`MapEventParty.RosterToReceiveLootMembers`核对**真实接收目标正是该Party的MemberRoster**；若不能确认，安全跳过并记录一条受限日志，禁止修改玩家临时Roster。
- 对每支符合范围的Party记录现有所有Bandit兵种：`CharacterObject -> (TotalCount, WoundedCount)`；也要保存初始健康人数，可由总数减伤兵数得到。
- 用`__state`持有一次调用的 Dictionary/列表，仅保存Party引用、StringId、快照；**不能使用全局static字典、不存入SaveData**，避免并发战斗和读档互相污染。
- 如果`winnerParties`为null、筛选不到目标、世界状态缺失，`__state`置空；**不得阻断原版执行**。

伪代码：

```csharp
static void Prefix(MBReadOnlyList<MapEventParty> winnerParties,
                   out RescuedBanditSnapshot __state)
{
    __state = new RescuedBanditSnapshot();
    foreach (var entry in winnerParties ?? /* 空集合 */)
    {
        var party = entry?.Party?.MobileParty;
        if (!RecruitmentTroopEligibilityPolicy.IsRegularAiLordParty(party))
            continue;
        // 验证NPC、真实接收Roster等条件
        __state.Record(party, SnapshotBanditCounts(party.MemberRoster));
    }
}
```

注意：`MBReadOnlyList`、`MapEventParty`、`MemberRoster`所需namespace以及foreach的null处理须使用项目实际支持的C#版本，**上述只是算法骨架，不是直接复制即能编译的补丁**。

### 4.3 原版方法完整执行

Prefix不要`return false`；执行原版所有战利品分配。不得移动英雄和正规俘虏，不参与`BattleRewardModel.GetLootPrisonerChances()`。

### 4.4 Postfix：只移除净新增的野怪

对每个Prefix快照中记录的受控Party：

1. 重新读取当前真实`MemberRoster`；获取所有Bandit兵种（注意**原来没有、此时才有**的兵种也需扫描）。
2. `before = snapshot[character].TotalCount`（缺失按0）；`after = roster.GetTroopCount(character)`；`newlyAdded = max(0, after-before)`。
3. `newlyAdded==0`则不处理；不能因为发现已有Bandit就整批清除。
4. 计算`healthyAfter = after - woundedAfter`；本原版路径以`woundedCount=0`加入获救普通士兵，故需**优先确保新入健康人数足够**；如果`healthyAfter < newlyAdded`或其他不一致，记录安全失败并跳过该兵种，禁止动到历史伤兵。
5. `roster.AddToCounts(troop, -newlyAdded, false, 0, 0, true, -1)`，然后复核实际 `afterFinal == before` 且受伤人数`woundedFinal == woundedBefore`；如接口语义不满足须改为目标程序集支持的安全扣健康士兵API，绝不通过减伤兵参数硬凑。
6. 对单Party汇总被排除总人数和兵种统计；不一人一行。
7. 从战败方原`PrisonRoster`移除后的被拒Bandit视为**获释**，**不回填败方囚犯、不加入获胜方囚犯、不视为战死、不额外支付招募费用**。

伪代码：

```csharp
int newlyAdded = Math.Max(0, after.Count - before.Count);
if (newlyAdded > 0 && after.Healthy >= newlyAdded)
{
    roster.AddToCounts(troop, -newlyAdded, false, 0, 0, true, -1);
    // assert/diagnose: final total == before total,
    //                  final wounded == before wounded
}
```

**现有方案限制：** Prefix/Postfix统计的是本方法前后净增量；如果其他Mod在同一方法、同一时点也修改同一Party的同一兵种名册，无法在所有加载顺序下百分百区分“获救”与“其他并发来源”。必须在PR标识此限制并做共存测试；不得把此方法误宣传为全局阻断野怪的写入前回调。

## 5. 边界、空引用与安全策略

- `__state==null`、`MapEventParty.Party`已无效、`MemberRoster`变更引用、原Party被战后解散：**不修改名册**，只输出受限诊断。
- 包含多个获胜方：按 Party 的稳定ID/引用分别记录，不允许将各Party同兵种累计到一个全局数。
- 同Party在`winnerParties`中若重复出现，使用对象引用去重，避免重复撤销。
- 若Prefix阶段PlayerEncounter正在使用玩家战后选择Roster，不能触碰该临时名册，确保玩家手动选择体验不变。
- Hero不进入本过滤；不处理被俘敌方原`MemberRoster`的野怪；不影响同战斗中正常俘虏产生。
- Harmony私有方法目标不存在时，必须至少让游戏本体战后结算可继续；**不要吞掉Mod全部初始化失败而假称补丁成功**。测试、记录补丁绑定诊断，兼容性修复另提PR。
- Patch内异常不能导致战斗损坏；仅在已验证可安全局部修改时变更，异常记录要通过项目`ModLogger`，注意不要造成成功一半而重复回滚。

## 6. 输出日志（成功过滤才通知，失败才诊断）

示例：

```text
[战后解救][RescuedBanditExcluded] Party=伊勒塔斯 ID=...
 rejectedTotal=3 troops=looter×3; action=released
```

建议使用`RecruitmentLogFilter.ShouldLog(party)`限制到玩家王国，再以`ModLogger.FileNotice`写一条/Party/战斗。无Bandit获救不输出。与其他Mod冲突时记录 `PatchConflict/UnexpectedRosterDelta` 一条聚合提示，禁止逐兵种刷屏。

## 7. 验收测试矩阵

| 编号 | 输入情景 | 期望 |
|---|---|---|
| B01 | AI胜利，敌人原俘虏含3名Bandit | 解救3名不进入AI MemberRoster；视为获释 |
| B02 | AI胜利，敌人原俘虏含20名Soldier/Mercenary | 原版直接分配照常；本Patch不新增财务审核 |
| B03 | AI打败10名正在作战的Bandit并俘获其中若干 | 正常进入胜方PrisonRoster；本Patch不碰 |
| B04 | 胜利Party原有2同类Bandit，本次解救3 | 最终仍保留原2；只撤销新增3 |
| B05 | 获胜三名AI领主分别收到不同Bandit | 分Party快照、无互相删除、总数准确 |
| B06 | 玩家参战/主Party接收获救Bandit | 玩家选择界面、招募体验完全不变；AI成员按规则过滤 |
| B07 | BanditParty/商队/民兵及英雄获救 | 不受额外拦截 |
| B08 | 同一场战斗同时“解救野怪+俘获敌方野怪” | 只移除从败方PrisonRoster解救入队的野怪，俘获保留 |
| B09 | 旧Party同兵种含伤兵，解救新增健康Bandit | 总数恢复前值，伤兵数不变 |
| B10 | 获救对象包含`Culture.IsBandit`但非`Occupation.Bandit` | 被正确过滤；分类依据与W3一致 |
| B11 | 连续战斗+存档重载 | 没有静态缓存残留、不会重复移除 |
| B12 | 补丁无法命中/其他Mod Postfix更晚改变名册 | 清楚记录兼容限制，本体战后流程不被强行替代 |

## 8. 提交前技术复核清单

- [ ] 用实际目标版本反射/源码核对`MapEvent`类namespace和私有方法签名；`Harmony.GetPatchInfo`能看见Prefix/Postfix。
- [ ] 实机证实获救非英雄AI名册直写发生在`LootDefeatedPartyPrisoners()`而不是其他模组替换实现。
- [ ] W3的Policy已可调用；没有重复Bandit定义。
- [ ] Prefix/Postfix都不拦原版函数；每次`__state`独立。
- [ ] 撤销人数只等于本次净增人数，受伤/健康名册守恒；不误删历史老兵。
- [ ] 游戏内含玩家、多个AI胜方的混合战斗通过；存档兼容。
- [ ] 附Harmony诊断、战后前后名册差分、未完成实测项。
- [ ] 建议Commit：`fix(army): exclude rescued bandits from regular AI parties`。
