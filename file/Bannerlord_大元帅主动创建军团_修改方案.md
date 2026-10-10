# Bannerlord 大元帅主动创建军团——修改方案

## 一、目标

修复 `ModifiedPolitics` 中“Hero 已担任大元帅，却不会由 AI 主动创建 Army”的问题。

最终规则：**无大元帅时恢复 Vanilla 创建军团逻辑；有大元帅时由大元帅专属 AI 负责主动组军团。大元帅只能从已实际领导 WarParty 的 Hero 中任命。**

## 二、军团创建权限与 AI 职责

| 王国情况 | 主动创建军团 | 其他说明 |
|---|---|---|
| 未任命大元帅 | Vanilla AI | 普通 Clan Leader 依原版规则创建 Army |
| 已任命 AI 大元帅 | `MarshalArmyAiBehavior` | 仅大元帅及王国统治者有权创建 Army |
| 王国统治者 | 保留创建权限 | 统治者的原版 AI/玩家操作不被大元帅 AI 接管 |
| 玩家本人担任大元帅 | 玩家手动创建 | 不由 AI 控制玩家主队创建 Army |
| 玩家 Clan 的 AI Hero 担任大元帅 | `MarshalArmyAiBehavior` | 只要 Hero 独立统兵，按 AI 逻辑执行 |

**职责边界：**`MarshalArmyAiBehavior` 只负责大元帅主动组军团的资格、时机与触发；尽量复用本体的军事目标评分、召集候选 Party、粮食、Influence 和军团后续行动逻辑，不重写全套军事 AI。

### 军团创建许可

```text
HasMarshal(kingdom) == false
    → 不增加大元帅专属限制，保持 Vanilla 权限

HasMarshal(kingdom) == true
    → armyLeader == kingdom.Leader：允许
    → armyLeader == MarshalHero：允许
    → 其他 Hero：禁止
```

此权限对 AI 和玩家手动创建均一致生效。已有 Army 不因新任大元帅就立即解散；大元帅卸任后，延续现有“解散其领导的军团”机制。

## 三、大元帅任命资格

只能从当前**实际领导一支 WarParty** 的 Hero 中选任，不为任命额外创建 Party，也不突破 Clan 部队数量上限。

除普通官职资格（存活、活跃、非俘虏、非雇佣兵 Clan、属于该 Kingdom）外，还必须满足：

```csharp
// OfficeRules.IsEligible(hero, kingdom, OfficeType.Marshal) 中的专属条件
MobileParty party = hero.PartyBelongedTo;

return hero != kingdom.Leader
    && hero.GovernorOf == null
    && hero.CanLeadParty()
    && party != null
    && party.IsActive
    && party.IsLordParty
    && party.LeaderHero == hero;
```

- 随行于其他 Party 的 Hero：不可任命。
- 没有 Party 的 Hero：不可任命。
- 总督、俘虏或无法统兵的 Hero：不可任命。
- 正在领导 WarParty、但暂时加入他人 Army 的 Hero：可保留大元帅资格；组建新 Army 前必须脱离原 Army。
- **Clan WarParty 已达上限：不影响现有 Party Leader 的选任**，因为任命不新增 Party。
- 没有合格的现任 WarParty Leader：大元帅职位保持空缺，不强行生成或替换 Party。

示例：

```text
Clan A：WarParty 数量 3 / 上限 3
  Party 1 → Hero A  [可参选]
  Party 2 → Hero B  [可参选]
  Party 3 → Hero C  [可参选]
  闲置 Hero D       [不可参选]
```

## 四、大元帅 AI 主动组军团

新增 `MarshalArmyAiBehavior`，由它负责拥有大元帅的王国中**大元帅本人**的主动组军团机会。

```text
AI 军事更新
  ├─ 王国无大元帅 → 完全交给 Vanilla
  └─ 王国有大元帅
       ├─ 大元帅是玩家本人 → 不执行自动创建
       ├─ 大元帅是否仍为 WarParty Leader？
       ├─ 大元帅的 Party 是否独立（Army == null）？
       ├─ 是否满足战争、兵力比例、Influence、粮食等条件？
       ├─ 是否有可召集的其他 WarParty？
       ├─ 复用本体军事目标评估
       └─ 目标和条件均满足 → 调用 Kingdom.CreateArmy(...)
```

核心修复点：当前安装版本已将上述身份门槛放入
`DefaultArmyManagementCalculationModel.CanLordCreateArmy()`。实现只在该方法内部把大元帅视为其
Clan 的有效组织者，不覆盖方法结果，因此大元帅仍继续接受本体的影响力、粮食、兵力、距离、
可召集部队等完整检查。

实现时必须确保同一次 AI 更新只有一条创建路径：不能既执行大元帅专属触发，又让 Vanilla 对同一 Party 额外触发一次。

## 五、大元帅丧失 WarParty 的处理

- **暂时加入别人的 Army：**保留官职，暂停主动创建；恢复独立统兵后继续工作。
- **不再领导任何 WarParty：**大元帅不能创建 Army；由官职 AI 定期核验任职资格，并通过既有王国决议机制处理不再符合资格的大元帅。
- **死亡、被俘、所属 Clan 失效或离开王国：**继续通过 `HeroOfficeBehavior` 的既有清理机制处理。
- 不为维持官职而强制拆分其他 WarParty，也不提高 Clan Party 上限。

## 六、文件修改清单

| 文件 | 操作 | 修改要点 |
|---|---|---|
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/OfficeRules.cs` | 修改 | `OfficeType.Marshal` 资格改为现任有效 WarParty Leader，排除 Governor |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalOfficeAiBehavior.cs` | 修改 | 保留任免决议、周期复核；对丧失 WarParty 的大元帅进行合法性复核 |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs` | **新增** | AI 大元帅主动建军团的判断与触发；排除玩家主队 |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs` | 修改 | 无 Marshal 走 Vanilla；有 Marshal 才限制创建人选为统治者或 Marshal |
| `ModifiedPolitics/ModifiedPolitics/Main.cs` | 修改 | 注册 `MarshalArmyAiBehavior` |
| `ModifiedPolitics/ModifiedPolitics/HeroOffices/Services/OfficeArmyService.cs` | 保留 | 大元帅离任时解散其带领的 Army |
| `code/AiMilitaryBehavior.cs` | 本体参考，不修改 | 复用/对接 AI 筹建条件与军事目标评分 |
| `code/DefaultArmyManagementCalculationModel.cs` | 本体参考，不修改 | 召集 Party、Influence 成本及候选判断 |

**实现注意：**本体的 `AiMilitaryBehavior.AiHourlyTick()` 并非简单公开的“创建 Army”接口。应在具体编码时选择可靠的 Harmony 切入点或复用本体决策数据，使大元帅进入实际军事目标评分与 Army 创建链路；仅修改 `Kingdom.CreateArmy` 权限，不足以使其主动组军团。

## 七、关键日志（仅玩家所在王国）

使用 `ModifiedPolitics.Tool.ModLogger`。关键事件输出：大元帅任命资格检查结果、是否有自有 WarParty、是否已经属于 Army、未创建原因（Influence、粮食、兵力、无可召集 Party、无合适目标）、实际创建 Army 成功/失败。普通每小时检查不逐条刷屏；仅状态变化、成功建军团及必要的 Debug 记录。

## 八、验收测试

1. **无大元帅**：普通 Clan Leader 可以按 Vanilla 逻辑自行组 Army。
2. **有 AI 大元帅**：若其不是 Clan Leader，仍可在满足军事条件时主动组 Army。
3. **有大元帅**：其他普通 Clan Leader 无法主动创建新的 Army；王国统治者仍可创建。
4. **大元帅已在其他 Army**：不重复创建；恢复独立后可重新参与创建判断。
5. **候选 Hero 无 Party / 为 Governor / 被俘**：不能当选。
6. **Clan Party 达上限**：已统兵的 Hero 仍可当选；系统不强制创建新 Party。
7. **玩家本人为大元帅**：只能玩家手动操作，不自动创建。
8. **玩家 Clan 的 AI Hero 为大元帅**：可以按 AI 主动创建。
9. **无合格 WarParty Leader**：官职空缺，且 Vanilla 军团行为不被禁用。
10. **大元帅卸任**：沿用已有解散其领导 Army 的处理，无多次创建或重复解散。

## 九、涉及的仓库源码

- [MarshalArmyPatches.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Patches/MarshalArmyPatches.cs)
- [MarshalOfficeAiBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalOfficeAiBehavior.cs)
- [OfficeRules.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/OfficeRules.cs)
- [AiMilitaryBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/AiMilitaryBehavior.cs)
- [DefaultArmyManagementCalculationModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultArmyManagementCalculationModel.cs)

## 十、当前实现状态（2026-10-10）

- 已新增 `MarshalArmyAiBehavior`，让不符合原版 Clan Leader 身份路径的 AI 大元帅参与本体军事目标评分。
- 已针对当前安装 DLL 的 `CanLordCreateArmy()` 身份读取做局部兼容；未重写本体战备条件。
- 王国没有大元帅时，不施加官职系统的创建限制，继续使用 Vanilla AI。
- 王国有大元帅时，大元帅和统治者保留创建权限；其他领主不再发起新的军团。
- 玩家担任大元帅时保持手动操作；AI 不接管玩家主队。
- 已补充仅玩家所在王国可见、且仅在状态变化时输出的诊断日志。
- 已完成静态差异与 XML 格式检查；按项目约定未在本地编译，等待进游戏验收。
