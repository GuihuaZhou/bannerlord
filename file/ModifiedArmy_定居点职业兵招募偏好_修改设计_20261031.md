# ModifiedArmy：定居点职业兵招募偏好与驻军自动补员——修改设计

> **代码基线**：`GuihuaZhou/bannerlord` / `main`，HEAD `77828bf273b28e7959c5fa54423cacd7f2d0a9a9`（2026-10-30 14:56 UTC）。
> **文档日期**：2026-10-31。**交付性质**：Worker 实施依据，不代表已经改代码或通过实机测试。
> **用户已确认**：没有任何现有游戏存档需要兼容；删除原“招募订单/计划”业务；由驻军上下限和 Clan 现有共享财政预算决定是否及实际招多少兵；职业兵权重按**定居点**保存；玩家、AI 使用统一偏好服务；易主必须重置。

## 1. 不可妥协的规则

**R1 每个定居点一份独立、可保存的职业兵权重表**。数据归属 `Settlement.StringId`，而不是 Clan、文化、Party 或全局 XML。两个定居点使用相同 `ProfessionalTroopTemplate`，也不得互相影响。

**R2 模板是来源，不是保存对象**。从 `TroopProductionTemplateRepository.Resolve(settlement, Professional)` 获得当前 `ProfessionalTroopTemplate`。模板保留只读 `TroopProductionEntry.Weight`，运行期不改 XML 实例；权重表保存具体兵种 `CharacterObject.StringId -> int`，包括权重 0 的条目。

**R3 当前资格决定权重表的成员集合**。以代码真实的 `AllowedRecruitmentLevel` 为依据：

```
Eligible(s) = {
  t | t ∈ ProfessionalTroopTemplate(s).TroopEntries
      && t != null
      && t.RequiredBarracksLevel <= AllowedRecruitmentLevel(s)
}
```

`AllowedRecruitmentLevel(s) = min(GetBarracksLevel(s), ResolveProfessionalProductionRule(s).MaximumRecruitableBarracksLevel)`。后者受到定居点、Clan、王国文化关系影响，不能用建筑等级代替。XML 默认权重为 0 的有效模板兵种依然可以出现在偏好表中（默认禁止；可由合法管理者修改为正数）；非模板或等级不合格兵种不能手动添加。

**R4 动态权重同步是“删除 + 补入”，绝非隐藏后恢复**。若兵种丧失资格，立即从该定居点权重表**物理删除**；其他仍合格兵种数值完全保留；之后恢复资格时，视为新兵种，直接采用当时 `ProfessionalTroopTemplate` 的 `Weight`，不恢复失去资格前的用户自定义值。

示例（假定 A 的 XML 默认权重为 3）：

| 时点 | A | B | C | 实际表 |
| --- | ---: | ---: | ---: | --- |
| 原先玩家设定 | 10 | 5 | 2 | `{A:10, B:5, C:2}` |
| A 因兵营或文化规则失去资格 | 不存在 | 5 | 2 | `{B:5, C:2}` |
| A 后来重新合格 | **3（XML 默认）** | 5 | 2 | `{A:3, B:5, C:2}` |

B/C 不按比例缩放；0 的权重也要保留（不能误当“缺失”）。连续多次执行 `Reconcile` 必须幂等；在没有资格变化时不得重置用户自定义权重。

**R5 易主时重建整个偏好表**。`OwnerClanId` 变更，不继承前主人的配置；按新拥有者和当前规则重算资格，所有当前合格兵种取 XML 默认权重。同 Clan 内仅换领主 Hero 不重置。领地夺回也视为新一次统治并重置。发生王国归属变化而 Clan 未变时，不重置整个表，只基于新资格删除/补入。

**R6 权重只控制职业兵来源**：不按权重分配俘虏；已有驻军不遣散；封邑兵生产、军事制度份额、Hearth 支出、训练速度、财政公式保持既有政策。职业兵权重0表示不产生/接收该兵种的**新职业兵**，不自动禁止俘虏归化相同兵种。

**R7 偏好不决定招募人数**。取消存档化招募订单（`AuthorizedTotal`、`Remaining`、`Paused`、`OrderCooldownDays`、`PlanningCommitmentShare` 等业务）。驻军达到自动招募上限不招；低于上限则每日尝试。当天实际招募由候选兵源、每日名额、软/硬人数上限、驻军工资与 Clan 现有共享采购和30天工资承诺预算共同限制。不能创建新的财政“预留”池。

**R8 人数上下限各司其职**。自动补员上限继续复用当前 XML `InteriorGarrisonUpperRatio=0.80`、`BorderGarrisonUpperRatio=0.95` 与 `GarrisonRecruitmentCapacity.GetUpperTarget()`；驻军调出底线统一取 `min(75, hardLimit)` 个**真实驻军**；外出征粮/运输护卫计入**编制占用**，但不抵扣真实留守下限。普通调兵不再叠加原50%/75%比例下限。兵员及财政安全的其他硬条件不得削弱。

## 2. 权重表数据语义

每个定居点存储**所有当前有资格的模板兵种**的完整有效权重表，而不是只存用户覆盖项：

```csharp
// 接口草案，具体命名可遵循仓库现有规范。
public sealed class SettlementProfessionalPreferenceRecord
{
    [SaveableProperty(1)] public string SettlementId { get; set; }
    [SaveableProperty(2)] public string OwnerClanId { get; set; }
    [SaveableProperty(3)] public string TemplateId { get; set; }
    [SaveableProperty(4)] public Dictionary<string, int> ActiveWeights { get; set; }
}
```

- 为什么用完整表：R4 要求 A 无资格时确实被删除；重新有资格时发现 A 不存在，才能给它 XML 默认权重。只存覆盖项也能推导，但更容易误把“曾修改”当作可恢复设置，且难以区分 0 与无记录。
- 保存键为 `settlement.StringId`，持久化所有城镇、城堡的独立记录；记录的 Clan/模板 ID 用于变化检测。
- 最大允许自定义权重建议 `0..20`（包含用户示例的10），Worker 应统一验证和界面限制；超范围拒绝，不要悄悄变成默认值。XML 默认权重若大于 UI 上限，应先确认限制策略而非静默截断。
- 所有 read API 返回不可变/复制快照，不能将持久化内部 `Dictionary` 泄露给 UI 或其他服务。
- 记录应只在合法 Campaign 阶段通过 Behavior/Service 写入；只读预测不能偷偷触发存档数据变更。

## 3. 资格获取与消除循环依赖

当前生产实现中 `GetProductionForecast()` 计算 `AllowedRecruitmentLevel`；而未来为了在全体权重为0时停止职业兵份额，也可能需要读取偏好。**不要**实现为 `Preference.GetEligible() -> GetProductionForecast() -> Preference.GetEligible()`，否则产生递归。

把既有私有 `ResolveProfessionalProductionRule()` 所依赖的文化关系、`GetBarracksLevel()`、模板选择抽成一个**只读、无写入**的 `ProfessionalRecruitmentEligibilityModel`（名称可调整），产出：`TemplateId`、模板条目集合、`AllowedRecruitmentLevel`、文化/生产效率等必要结果。现有 `SettlementManpowerProductionBehavior.GetProductionForecast()` 和偏好服务都复用它；不要重复实现文化匹配和兵营计算。

变更后的资格过滤不能偷偷增加战斗兵种、放开 `RequiredBarracksLevel`、跨文化指定职业兵；`TroopProductionTemplateRepository.Resolve()` 仍由 `settlement.Culture`、`IsCastle`、`HasPort` 决定模板。

## 4. 确定性的 Reconcile 算法

输入：Settlement 当前拥有者、当前模板、当前 `AllowedRecruitmentLevel`、已有记录（可能不存在）。输出：更新后的当前合格权重表和变化清单。

```
Reconcile(settlement):
  snapshot := EligibilityModel.Evaluate(settlement)
  eligible := snapshot.template.TroopEntries
              .Where(troop != null && requiredBarracksLevel <= allowedLevel)
              .DistinctBy(Troop.StringId)

  record := stored[settlement.StringId]
  if record absent or OwnerClanId changed or TemplateId changed:
      record := new record(owner, template, empty map)
      // 易主或模板整体切换：原表全部丢弃

  for each id in record.ActiveWeights.Keys:
      if id not in eligible:
          remove id completely  // 不保留 tombstone/历史自定义值

  for each troop in eligible:
      if troop.StringId not in record.ActiveWeights:
          record.ActiveWeights[troop.StringId] := troop.TemplateWeight
      else:
          keep original weight including 0

  store record iff changed
  return snapshot + immutable copy of effective weights
```

当模板无法解析或主人不明确时，停止新增职业兵，禁止玩家修改；不能因此把尚未确认原因的持久化记录清空。拥有者确定发生变化时必须重建。`DistinctBy` 是伪代码，Worker 按目标 .NET 版本实现，避免假定存在 LINQ API。

**权重0不是“不合格”**：仍在 `ActiveWeights` 中，参与 UI 显示，但生成候选时排除。**不合格是“不存在”**：退出 `ActiveWeights`；再进入时从 XML 取默认。

## 5. 今日候选与跨天加权账本

现有两种状态必须区别对待：

1. `_dailyProfessionalOffers` / `_professionalOfferDays`：当天已生成、尚未实际招入驻军的具体候选。该缓存不是第二个可长期存档的兵源池。
2. `SettlementManpowerProductionData.ProfessionalProducedByTroop` + `ProfessionalProductionSignature`：已分配职业兵候选的历史数量，用来使小批量生产长期趋近权重，而不是对驻军现存士兵做编成比例控制。

**用户/AI修改权重**：立即写入偏好表。若某兵种变成权重0或失去资格，**立即过滤/清理当天该兵种未消费的候选**；其他已生成、仍合法的候选不必重抽。正权重或权重比例的变化，从**下一次正常日生产**按新配比完整生效。禁止同日因编辑权重产生第二轮候选或额外 Hearth 支出。

**资格恢复**：新解锁 A 加入权重表取 XML 默认值；当天不凭空生成 A 候选，从下一次生产开始进入抽取。已经生产但后来无资格的 A 不得通过 `GetAvailableProfessionalOffers`、`GetPredictedProfessionalOffers`、`TryConsumeProfessionalOffer`、实际驻军招募入口绕过限制。

**历史签名**：`BuildProductionSignature()` 用实际有效的正权重条目（兵种 ID + 数值），含模板/OwnerClan/文化等现有字段。改变权重、资格、主人或模板会自然切换签名，历史 ledger 在下一次生成时重置；不得用旧历史在新配比下“追欠账”。**留意账本记的是候选分配，不是实际入伍**，不能随便挪到财政执行阶段。

**全部当前合格权重为0**：职业兵需求/分配应为0；允许封邑兵照原有 `FiefDemand` 和文化制度进行补充；当天不消费职业兵 Hearth，不增加新的职业兵累计训练进度（避免关闭十天后爆发补员）；已有小数生产进度应保持原样，不偷清零。预测界面显示“无启用职业兵种”。生产需求计算必须避免与偏好资格 Model 的调用循环。

**所有权变更**：立即清空该定居点今天的旧候选，保留“本日已生产”标记以禁止同日新主人重抽生产/重复消费基础兵源；新的默认权重从下一次自然日生产生效。不要更改现有已生成的封邑兵或驻军花名册。

## 6. 每日驻军招募，不再有订单

- 玩家驻军：保留 `GarrisonRecruitFromPrisonersBehavior` 原有每日自动招募路径，但候选受到新的定居点偏好控制。
- AI 驻军：保留轻量级 `ClanGarrisonRecruitmentCoordinator`，按 Clan 分组、稳定排序：实际驻军低于75者优先；其余以距自动上限的缺口比例排序；同等情况下边境优先，最后按定居点 ID。不能继续用招募订单、长期规划份额或财政“预占”阻塞后续定居点。
- 财政：`ClanRecruitmentBudgetManager` 的30天承诺、实际购买额度、工资与储备公式继续作为权威来源。每日招募真实成功才 `CommitRecruitment`，失败或无兵源不占其他城堡预算；不创建另一资金池。
- 人数：`GetAutomaticRecruitmentTarget` + `GetSoftRecruitmentFreeSlots` + `GetHardFreeSlots`（已包含返回护卫预留）共同限制；实际成功时计入同一每日额度。职业兵和俘虏共同使用上限，但偏好仅调整职业兵部分。俘虏留用、服从度和出售按原业务规则正常推进，即使没有可招职业兵也不停止。
- `GarrisonRecruitmentExecutionService` 中本轮提交、回滚、财政扣款的保护应保留并补齐，不因删除 `ExecuteUnderOrder` 订单参数而退回原始不安全路径。
- 原有 `GarrisonDefenseRequirementModel.GetSafeTransferOutCount` 中百分比下限与75人真实驻军底线叠加的缺陷要同时修正，避免“上下限分开”的新设计在调兵时被旧比例暗中限制。

## 7. 定居点易手与其他动态变化

订阅仓库已有 `CampaignEvents.OnSettlementOwnerChangedEvent`；可参考 `PartySettlementBindingBehavior` 的真实签名。事件用 `oldOwner/newOwner` 的 Clan 信息判定是否换 Clan；若事件时 `settlement.OwnerClan` 尚未指向新主，则标记待同步，并在安全时点二次 `Reconcile`，不要在旧主人状态下重新生成偏好。

- **跨 Clan**：清理旧权重记录、按新主人和当前模板初始化全部默认；清理旧的今日职业兵候选；不清理现存兵、封邑兵生产台账或已消耗的金钱。
- **同 Clan 换 Hero**：权重原样保留；若有效文化规则变化导致资格变化，按 R4 删除/补入。
- **Clan 改变所属王国**：订阅相应事件或在每日 Reconcile 检查，调整 `MaximumRecruitableBarracksLevel`；不把同 Clan 权重全部重置。
- **兵营升级/降级、有效文化变化**：在玩家打开设置页、修改权重、每日职业兵生产之前进行合格集合核验；必要时响应已有建筑变更入口。特别注意，若 A 曾失去资格，即使未读档，必须及时**实际删除**旧记录。
- **读档/新游戏**：新的 Behavior 的 `SyncData` 持久化 `Dictionary<string, SettlementProfessionalPreferenceRecord>`；恢复阶段在对象和模板可用后统一核验 OwnerClan、模板、资格；新游戏按需懒创建或每日首次初始化。无需对旧招募订单存档做兼容迁移。

## 8. 玩家 UI 与 AI 接口

提供每个玩家自有城镇/城堡的“职业兵招募偏好”入口，可优先挂靠现有镇/城堡菜单（与 `GarrisonInspectionMenuBehavior` 分离）。必须显示模板兵种名称、兵营要求、有效兵营等级、XML 默认权重、当前有效权重。当前不合格的兵种不得以旧自定义权重保留；界面可作为只读“暂不可招”说明展示模板信息，但不可修改，且不得混入可保存 `ActiveWeights`。提供逐兵种调整、恢复该项默认、全部恢复默认、保存/取消。

玩家只有在 `settlement.OwnerClan == Clan.PlayerClan` 时可编辑；AI 通过同一写入服务（带合法所有者校验），不另设静态/全局权重表。默认 AI 使用 XML；以后如果添加 AI 兵种偏好策略，应显式指定调整何时发生，不在本任务发明新的 AI 权重公式。

界面保存可采用“先在 VM 编辑临时副本 -> 一次性提交”，避免用户点取消后已经改变游戏；提交时重新验证拥有者、模板、资格和数值，防止窗口打开期间城池失守/等级变化。使用 Mod 现有本地化体系提供中文文案。

## 9. 修改/移除模块和边界

| 路径 | 处理 |
| --- | --- |
| `Recruitment/Pools/Models/ProfessionalTroopTemplate.cs` | 保留 XML 权重只读，不修改全局模板实例 |
| `Recruitment/Pools/Models/TroopProductionTemplateRepository.cs` | 继续作为模板唯一解析入口 |
| `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | 抽资格读模型；使用定居点权重；改变当天 offer 过滤和历史签名；避免所有权重0时产能积累 |
| `Recruitment/Pools/Models/SettlementManpowerProductionData.cs` | 保留历史 ledger 与小数生产进度，不把用户偏好塞入生产统计 |
| 新 `Recruitment/Preferences/{Models,Behaviors,Services,Presentation}` | 持久化、资格同步、编辑权限和 UI |
| `Recruitment/Orders/*` | 删除旧订单业务与其配置、诊断、纯数学测试和验收矩阵；不保留无意义“订单兼容层” |
| `Recruitment/Models/DefaultAIRecruitmentModel.cs` 等 | 删除订单剩余额度上下文；保持现有审批、软上限、工资和 Clan 预算检查 |
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 玩家原有流程；AI 交由无订单协调器；消除同日统计覆盖 |
| `Recruitment/Services/{GarrisonRecruitmentExecutionService,ProfessionalRecruitmentService}` | 维护原子事务，额外验证招募时的有效偏好 |
| `ForceManagement/Models/ForceManagementModels.cs` | 固定75人真实守军底线，移除普通调兵的旧比例下限叠加 |
| `common/MilitaryConfig.cs` + `ModuleData/militaryConfig.xml` | 删除订单专属配置，保留驻军上下限和军事制度 |
| `Fief/Models/FiefPartyManager.cs` (`FiefSaveDefiner`) | 无存档兼容需求，移除旧订单类型注册，注册新的偏好数据类型/容器；不要修改其他 SaveId |
| `Main.cs` / `CampaignRecoveryCoordinator.cs` | 更新 Behavior 注册与恢复顺序；不得重复事件订阅 |

## 10. 关键验收场景

1. **A 自定义10/B5/C2 -> A失去资格 -> A重新合格**：精确得到 `A=XML默认、B=5、C=2`；必须跨保存/读档成立。
2. A 自定义0后失去资格，重新合格得到 XML 默认（不是0），证明“0被删除”与“无条目”不同。
3. B/C 在 A 消失及恢复过程中保持原数值，不归一化；同时不同定居点互不影响。
4. 兵营由3降到1、再升3；有效文化限制由3降1、再升3，行为相同。
5. 同 Clan 换 Hero 不重置；跨 Clan 易主整个表重置；夺回后仍重置；跨王国但未换 Clan 只按资格变化修订。
6. 当天已生成 A 候选后禁用/失去资格：当天不得入伍 A；不能因改权重额外再生成一批 B/C。
7. 权重全部0：新职业兵=0、职业兵 Hearth支出=0、没有隐藏的训练进度堆积；封邑兵仍生产。
8. 历史账本签名与实际有效权重一致，预测不提交账本、读档不重复生产。
9. 低于上限且有兵源/财政即可招；满上限、无兵源、无钱或工资已满均不得招；多定居点共享一份 Clan 当日预算。
10. 真实守军74/75/76，带外出护卫的边界，按75底线控制调出；预测和实际名额一致。
11. 俘虏归化不受职业兵权重限制，不重复消耗 Clan 预算；玩家和 AI 分支不双重招募。
12. 无旧存档迁移；新存档保存、退出、重新进入后偏好、权重0、归属与历史账本均正确。

## 11. 代码核验索引（2026-10-31）

- [职业兵模板与 XML 权重解析](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/ProfessionalTroopTemplate.cs#L33-L86)
- [职业兵模板仓库选择规则](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/TroopProductionTemplateRepository.cs#L35-L78)
- [预测和真实每日生产入口](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs#L137-L197)
- [职业兵分配、历史权重账本、签名](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs#L638-L727)
- [职业兵资格、文化效率及真实兵营](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs#L821-L903)
- [原订单 Clan 协调器（需要删减）](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/Recruitment/Orders/Behaviors/ClanGarrisonRecruitmentCoordinator.cs#L33-L112)
- [定居点易主事件参数示例](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/PartyManagement/Bindings/PartySettlementBindingBehavior.cs#L383-L390)
- [驻军真实守军底线与旧下限冲突](https://github.com/GuihuaZhou/bannerlord/blob/77828bf273b28e7959c5fa54423cacd7f2d0a9a9/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs#L189-L253)
