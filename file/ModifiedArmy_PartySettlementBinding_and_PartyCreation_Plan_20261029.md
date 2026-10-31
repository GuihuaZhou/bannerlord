# ModifiedArmy：MobileParty—定居点绑定与 Party 创建资格审核实施方案

- 版本：V1.0 / 2026-10-29
- 适用仓库：`GuihuaZhou/bannerlord`，`main`，核对提交 `72e407a04f6ae73735877da96b4ff867d370faed`（2026-10-29）
- 范围：**只实施 Party 基地绑定、补员/返乡/休整目的地约束、创建资格和相关生命周期**；**不实施**驻军一次性招募计划、固定75人下限、财政模型新算法或驻军上限改动。
- 状态：实施方案，不是已提交的代码；涉及 Harmony 与原版引擎的挂接需以编译、实机运行结果验证。

## 0. 决策摘要

1. **一个城镇/城堡在任何时刻至多绑定一支普通 Lord MobileParty；每支 Lord Party 绑定1～2个基地（主基地、可选副基地）。** 初次创建至少一个主基地；第二基地不是必需。村庄不是独立的绑定槽位。绑定必须与 Party 的 `ActualClan` 所拥有的城镇/城堡一致。
2. **Party 数量上限保持现行 `NewClanTierModel` 规则，包括原版技能加成。** 新建再加两道门槛：存在至少一处空闲合法基地；计划在该基地能真实筹集、经当前财务及兵员保护规则批准**至少50名可转移士兵**。
3. **创建后必须从真实兵源完成首次动员；不能只检查库存、随后让原版免费模板兵成为新兵来源。** 统一允许通过基地驻军普通兵和同基地封邑兵组合筹满50；实际转移与登记要有事务边界，失败要回滚，不应留下零兵/半成品 Party 或重复士兵。
4. **普通 AI 的本 Clan 补员来源仅选本 Party 绑定基地；休整目的地及一般常规移交也受绑定限制。** 已在役封邑兵的返还必须优先服从其真实 `SourceFief + BatchId`，这是对绑定地理限制的明确例外，不能破坏原始服役账本。
5. **解散/毁灭/移除和定居点易主均应清理绑定；`OnPartyDisbandStarted` 只标记不解绑定，等结算完成后清理。** 读档重建反向索引、纠错，不进行不可逆士兵转移。
6. **老存档既有 Party 做温和迁移：不能凭空新造兵、不能强行销毁多余 Party；无法绑定的既有 Party 进入 `LegacyUnbound` 状态并有明确诊断。** 但新 Party 创建严格执行上述新门槛，不能借旧档例外创建无绑定新 Party。
7. **本阶段沿用仓库现行驻军最低人数与容量计算**（边境75%/95%，内陆50%/80%）；此前讨论的“固定保留75人”及“一次性动态招募订单”**均属后续单独阶段**，不要偷偷一起修改，否则测试结论无法归因。

## 1. 仓库已核对事实：不要把建议误当成已有实现

| 模块/文件 | 已有代码与后续接入点 |
|---|---|
| `PartyManagement/Models/NewClanTierModel.cs` | 非次要派系按封邑数返回1/2/3支部队的基础上限，加 `DefaultPerks.Leadership.TalentMagnet` 的技能加成。只管数量上限，不负责创建。 |
| 原版 `code/HeroSpawnCampaignBehavior.cs` | `ConsiderSpawningLordParties` 按 Clan 上限、可用领队、评分决定生成；`SpawnLordParty` 调 `MobilePartyHelper.SpawnLordParty`。新游戏分支还会追加默认模板士兵。 |
| 原版 `code/MobilePartyHelper.cs`, `code/LordPartyComponent.cs` | `LordPartyComponent.CreateLordParty` 是具体创建入口。`InitializationArgs.InitializeLordPartyProperties` 调用 `InitializeMobilePartyAroundPosition(defaultPartyTemplate, ...)`，加入英雄、粮食，并可能设置前往 `spawnSettlement` 的移动命令；底层 `MobileParty.InitializeMobilePartyWithPartyTemplate()` 会调用 `PartySizeLimitModel.FindAppropriateInitialRosterForMobileParty()` 得到模板初始名册；**设置 `spawnSettlement` 并不等于 `CurrentSettlement` 已进入该地。** |
| `ForceManagement/Services/PartyForceCandidateService.cs` | 当前遍历全部 `Settlement.All`，对自己 Clan 所属城镇/城堡尝试 `Fief`、`Professional` 候选；另有原本的城镇雇佣兵候选。应替换本 Clan 定居点的枚举范围，不改候选审批的内部权威。 |
| `ForceManagement/Models/AiForcePlanningModel.cs`，`Behaviors/AiForcePlanningBehavior.cs` | 日度任务+原版 AI 评分前往 `TargetSettlement`；旧已保存任务可能指向未来不再绑定的地方，需要校验和失效处理，不得继续按旧目标走。 |
| `ForceManagement/Models/PartyExchangeModel.cs` | 正常 `GarrisonToField` 需要 `field.CurrentSettlement == settlement`，且来源/目标同 Clan 与保留线、名册受保护兵等审批通过；不能在 Party 刚生成到城外时直接调用普通到访事务。 |
| `Fief/Models/FiefPartyManager.cs`、`Fief/Models/PartyData/FiefPartyRecruitment.cs` | 封邑兵具有真实批次/账本，招募需要真实 Party 来做审批、登记和动员。既有 `PreviewFiefRecruitmentApprovals` 依赖 Party，不能在未创建 Party 时无脑复用为“完全准确”预审。 |
| `ForceManagement/Services/WarRestPlanningService.cs` | 战时休整按 `SourceFief` 和真实批次选择返乡目标，当前 `WarRestPolicySettings.Enabled = false`；此阶段不应启用休整，只做未来启用时的绑定目的地策略与真实源例外。 |
| `ForceManagement/Missions/PartyVisitExecutionService.cs` | 除任务到达招募外，常规访问可向同 Clan 驻军捐兵；要加入绑定检查以避免绕开候选搜索限制。 |
| `ForceManagement/Services/DisbandSettlementService.cs`、`DisbandSettlementCoordinator.cs` | 正式解散先归还真实来源封邑兵，再把普通士兵放入合法驻军，容量不够会记录丢失；绑定不可在结算前删除。原版 `DisbandPartyCampaignBehavior` 可能按其他策略选择解散目的地，需要适配。 |
| `Recruitment/Finance/ClanRecruitmentBudgetManager.cs`、`Recruitment/Models/DefaultAIRecruitmentModel.cs` | 有 Clan 共用预算、30天承诺、部队工资上限和真实招募审批；新建50人必须遵守实际 Party 创建后的最终结果，预览不能视为锁库存或锁资金。 |
| `Garrison/Models/GarrisonRecruitmentCapacity.cs` | 外出征粮队会占用母驻军预留编制；本阶段保持现有规则。 |
| `ForceManagement/Behaviors/CampaignRecoveryCoordinator.cs` | 全局读档恢复屏障，先重建封邑批次索引，再复审 AI 任务，最后 `Ready`。新绑定反向索引应在 AI 任务复审**之前**恢复。 |
| `Fief/Models/FiefPartyManager.cs` 的 `FiefSaveDefiner` | 当前 `SaveableTypeDefiner(20251116)` 已定义若干存档类和集合；若新增可序列化绑定类，应使用未占用的类序号/集合定义，不可复用已有ID。 |

特别注意：仓库 `code/WarPartyControl.cs` 可搜索到早期的 `MobilePartyPermissionManager` 示例，但它不在已确认的 `ModifiedArmy/ModifiedArmy` 编译源码路径内。**不能仅因仓库有此示例，就声称现行 Mod 已对 `CreateLordParty` 打了该补丁**；应检查 `.csproj` 实际编译项及 `Main.cs` 的 Harmony 安装记录。

### 已确认的原版事件

- `CampaignEvents.MobilePartyCreated`：新建后检查和补偿挂钩。
- `CampaignEvents.MobilePartyDestroyed`：战败毁灭等。
- `CampaignEvents.OnPartyDisbandStartedEvent`、`OnPartyDisbandCanceledEvent`、`OnPartyDisbandedEvent`：解散生命周期。
- `CampaignEvents.OnPartyRemovedEvent`：所有 Party 的最终清理兜底。
- `CampaignEvents.OnSettlementOwnerChangedEvent`：封邑易主。
- `CampaignEvents.OnClanChangedKingdomEvent`：Clan 政治关系变动时重新检查合法性。
- `CampaignEvents.OnGameLoadFinishedEvent` / `OnSessionLaunchedEvent`：读档重建/新游戏初始化。

事件的具体先后顺序及部分原版调用路径仍须在本游戏版本做一次日志验证；特别是解散与移除事件，不能凭监听器注册顺序假定事务结算已完成。

## 2. 绑定模型与业务不变量

### 2.1 管理对象边界

- 适用：有 `LordPartyComponent`、有效 `ActualClan` 的普通领主野战 Party，包含玩家 Clan 的 AI 分队（如适用）；主角主部队不可被此规则拦截创建或强制绑定。
- 排除：驻军、村民、商队、强盗、巡逻/运输/征粮任务队伍、叛军与次要派系等当前不在统一领主兵力规划范围的 Party；这些不参与“每封邑一支领主 Party”的槽位竞争。对于次要派系维持 `NewClanTierModel` 现有原版式上限。
- 基地必须为 `IsTown || IsCastle`、由 Party `ActualClan` 拥有、未处于围攻、不属于另一支 Party 的主/副绑定；村庄可通过定居点自身管辖关系按原封邑机制产兵，但不算独立基地。
- 每定居点绑定最多1支 Party（主/副无差别）；每 Party 最多2个不同定居点。初创只要求主基地。第二基地的添加不应争抢已分配给其他 Party 的主基地。
- 一个基地可能被 Party 绑定但 Party 已离城作战；**绑定不意味着士兵在该 Party 中，也不自动扣减驻军库存**。

### 2.2 持久数据建议

最小结构（伪代码，字段名为设计建议）：

```csharp
sealed class PartySettlementBindingRecord
{
    public string PartyId;                 // 主键，现有 Party StringId
    public string ClanId;                  // 绑定时 Clan StringId，用于复验
    public string PrimarySettlementId;
    public string SecondarySettlementId; // nullable
    public int Version;                    // 乐观更新/调试
    public bool LegacyUnbound;            // 老存档豁免，只读迁移标记
}

sealed class PartySettlementBindingBehavior : CampaignBehaviorBase
{
    // 存档中的唯一权威：Dictionary<string, PartySettlementBindingRecord>
    // 运行期反向索引：Dictionary<string, string> settlementId -> partyId
    // 运行期临时预留：Dictionary<string, PendingPartyCreation>，不参与存档
}
```

`StringId` 更适合减少对失效对象引用的持久化依赖，但需要定义 Saveable 类和容器、在恢复期由当前 `MobileParty.All` / `Settlement.All` 精确解析。注意 StringId 的重新使用与创建事件先后，应以当前 Session 的真实对象有效性复验；不可仅信任字符串匹配。

唯一写入接口建议：

```csharp
TryReserveForCreation(Hero leader, Clan clan, Settlement primary,
    out PendingPartyCreation token);
TryCommitCreation(PendingPartyCreation token, MobileParty createdParty);
CancelCreationReservation(PendingPartyCreation token);
TryBindSecondary(MobileParty party, Settlement settlement);
GetBoundSettlements(MobileParty party);
IsBoundSettlement(MobileParty party, Settlement settlement);
ReleasePartyBindings(MobileParty party, string reason);
RevalidateAndRebuildIndexes();
```

索引层统一验证：重复占用、同一个 Party 主副指向同一定居点、Clan 已易主、Party 已失效、未完成创建临时预留超期。操作应**幂等**；无持久记录的删除不能抛异常。

## 3. 创建资格与最关键的 50 人真实兵源事务

### 3.1 必须同时通过的门槛

1. 原版 `ClanTierModel.GetPartyLimitForTier` / `WarPartyComponents.Count` 等门槛仍通过，保留任何已生效的原版技能加成；不要从模型中删除 `TalentMagnet`。
2. 领队是可合法指挥普通 Lord Party 的英雄；不能处于俘虏、死亡、担任总督等冲突状态。
3. Clan 至少拥有一个未被占用且未被临时预留、可达/安全的城镇或城堡（新主基地）。
4. 新 Party 的最终实际编制容量必须至少容纳50名**普通士兵**加领队，不能用50名含英雄总人数蒙混（建议阈值按非英雄正规兵）。
5. **真实来源**：基地驻军能安全调出的非保护士兵 + 同基地符合封邑批次/动员条件的封邑兵，不能重复计数。不能计入尚未生产的每日虚拟职业兵报价、未来训练额、同 Clan 其他未绑定城堡的库存。现行 `MinimumFiefRecruitBatch=25`，少于25名的封邑兵预览不能被当成可正常征召的有效批次（除非日后另行调整该规则）。
6. 财政、工资、30天维护能力及兵种合法性按现有招募模型复核。50 人是**实际可完成转移**，不是 `GarrisonCount >= 50`，也不是两个来源分别预览50、重复加总。
7. 所有条件通过后执行创建；任何一步失败，保留源兵、解除临时占位，不得留下空壳 Party 或重复转兵。

本阶段**不修改**驻军原有保留线：创建时 Garrison 可调数仍由 `GarrisonDefenseRequirementModel.GetSafeTransferOutCount()` 决定，封邑兵按原版批次保护/实际动员审核。即便一座边境城堡有100名驻军，也可能因当前**百分比保留线**而没有可调职业兵。固定75人的替代方案属于下一阶段；本阶段先把审核做正确。

### 3.2 候选基地选择

- 按 Clan 的**空闲城镇/城堡**建立候选列表，过滤被围、已绑定、已预留或无合法兵源的地点。
- 对每个候选构造**只读**首次动员预审：驻军保护人数、真实封邑兵库存、同 Clan 归属、地理安全、当前领队能够负担的工资/编制，以及预期所需50人。
- 预览有可能比真正创建后的审批更乐观，因为 `PreviewFiefRecruitmentApprovals` 依赖已存在的 Party，且新 Party 会改变 Clan 支持队伍数和当日财务分配。**必须增加专门的 `InitialPartyMobilizationPreflightModel`，采用包含拟新增 Party 的保守财务测算，不得用空 Party 假冒已创建 Party。**
- 候选选取先确保能实际凑50人，再参考距离、实际可动员士兵数、是否接近战区等软评分；`StringId` 作为稳定平分规则。
- 创建**只从主基地完成首批50人入队**，不要跨两个异地驻军瞬间转兵（避免瞬移）；副基地可在创建成功后或后续维护时绑定，不能用于凑齐主基地50人的首次创建门槛。
- 允许同一主基地内组合真实封邑兵与可调普通驻军士兵。两类名册库存与对应工资审批必须共用一次真实状态，不得用两个独立估算器重复占用相同 Clan 承诺预算。

### 3.3 原版创建路径的必要适配（不能只做 Postfix）

核对 `LordPartyComponent.InitializationArgs` 可知：原版 `CreateLordParty` 一次创建包含**领队入队 + 默认 `PartyTemplateObject` 初始化 + 粮食**；对于新游戏，`HeroSpawnCampaignBehavior.SpawnLordParty` 还会追加模板士兵。且 `spawnSettlement` 只触发 `SetMoveGoToSettlement`，不代表 Party 已驻留。故不能把创建后模板自动产生的人数当成本方案的50名真实兵源。

推荐最小可验证策略（具体 Harmony 入口在实施前先检查 `.csproj` 和运行时方法签名）：

A. **创建前 `Prefix` 预审**：在 `LordPartyComponent.CreateLordParty` 及其他通向它的普通 Lord Party 入口统一调用创建准入服务。仅普通受管 Party 开启；保留原生数量上限和 Hero 资格检查。没有合法主基地/50名真实可用兵，返回拒绝；临时预留有唯一 token，保证同一天同一个基地不会被两个创建流程同时选中。

B. **临时创建/定位**：只有预审通过，才允许原版进行内部创建。创建过程中产生的模板兵应单独快照并清理为可替换状态，严格保留领队等英雄和必要粮食；别将原版免费模板士兵当成本次来源兵员。尽量使新 Party 安全地进入所选主基地再调用现有交换服务，**不能直接伪造 `party.CurrentSettlement`**。若当前游戏版本无法安全完成“创建后入城”的位置过渡，需实现专门的创建授权事务，显式验证创建 token、出生位置、Clan、基地所有权，并禁止其他 Party 复用；不得在普通交换服务中全局放松 CurrentSettlement 检查。

C. **提交首批真实士兵**：在相同操作域里依次核对源库存、75%/50%当前保留线、封邑批次、Partysize、工资本金和未来维护预算；对两种来源的额度合并裁剪，实际**非英雄士兵至少50人**后才写入正式绑定、允许进入普通 AI 日常规划。建议引入专用 `InitialPartyMobilizationService` 协调原有 `PartyExchangeRunner`、封邑 `FiefPartyManager` 和 `TransferOperationJournal`；禁止直接 `Roster.AddToCounts()` 制造士兵。

D. **失败补偿/回滚**：如果出现 `roster` 改变、Clan 所有权变动、预算耗尽、封邑批次提交失败、新 Party 初始化失败等，回滚该创建操作已移动的士兵、封邑服务记录、Hearth/Clan 财务承诺；恢复源名册、清除临时预留，销毁未正式生效的 Party，不能将临时 Party 留在地图上。仅应在原子事务已完成后生成成功日志、注册正式绑定。失败期间严禁触发通用“解散党兵转驻军”的二次路径。

E. **再次核验原版额外初始兵**：新游戏路径额外赠送模板兵的逻辑位于 `HeroSpawnCampaignBehavior.SpawnLordParty` 内，可能发生在底层 `CreateLordParty` 的 `Postfix` 之后；应在**完整原版初创链路结束**后禁止这批免费的额外士兵入队，或执行明确的替代/移除操作。须分别测试“新游戏初建”和“中途领主重建”，确保既没有双倍兵，也不会误删真实动员的士兵。

**实现纪律**：以上是一条需实现的事务协议，不是声称原有接口已经支持跨驻军+封邑的一键回滚。真正开始开发前，应先做创建时序探针（`Prefix → OnMobilePartySetOnCreation → MobilePartyCreated → Postfix → SpawnLordParty返回 → FirstTick`），确认能完整控制模板兵及生效节点。若无法可靠处理这种时序，先提交纯绑定阶段，绝不以“暂时允许空壳 Party”交付50人验收。

### 3.4 创建失败不是正常解散

创建失败属于事务回滚，和“已存在并服役的 Party 正式解散”不同。后一种有封邑兵真实服役历史、返乡合法性和剩余普通士兵结算；前一种不能触发相同的损失登记和士兵遗失。需要专门区分 `ProvisionalCreation` 与 `ActiveParty`，或采用完全对外不可见的初始化阶段。

## 4. 绑定对现有补员、返乡、休整的具体影响

### 4.1 主动补员

改 `PartyForceCandidateService.EnumerateCandidates`：

```csharp
foreach (Settlement settlement in bindings.GetBoundSettlements(party))
{
    if (!IsValidForRecruitment(party, settlement)) continue;
    AddFiefCandidate(...);
    AddProfessionalCandidate(...);
}
```

- 不再遍历该 Clan 其他未绑定封邑招募 `Fief` 或 `Professional` 兵源。
- 既有 `AiForcePlanningModel` 任务只选合法候选；持续任务每日及入城前都核验绑定，易主/解绑立即取消旧目标并请求原版 AI 下次评分重想，避免持续前往不再合法的据点。
- 雇佣兵原来可能来自**非 Clan 所属的中立城镇**。这一渠道不属于“在 Clan 全部封邑中寻找”的集合。本阶段默认**保留原有外部雇佣兵搜索与文化需求**，不把外部城镇错误绑定到 Party；若产品要求所有招募目的地都必须是绑定地，则需要明确单独禁用外部雇佣兵，而不是借改枚举误删。
- 既有封邑兵批次归还不等同于新兵征召，不受上述新增征召候选限制。

### 4.2 战时休整和封邑兵返乡

- 休整基地选择先从 `GetBoundSettlements(party)` 中评估有效地址。单基地不可用、双基地均不可用时，返回无安全休整基地；不能悄悄回退到全 Clan 封邑。
- **真实封邑服役批次的返还继续限定 `SourceFief + BatchId`**，即便该封邑不是当前绑定地；若源封邑已易主/不安全，走现有归还失败或服役结算逻辑，不能擅自改写来源。
- `WarRestPolicySettings.Enabled` 当前为 `false`，只预留接入接口，不在本阶段开启自动战时休整，不强制普通士兵转入驻军。

### 4.3 和平复员、普通到访捐兵、正式解散

- 正常和平复员/普通到访捐兵：对受管 Party，普通驻军移交目标必须是绑定基地且同 Clan；在未绑定定居点的自然访问不得悄悄移交普通兵员。
- `PartyVisitExecutionService.ExecuteRoutineDonation`、`DemobilizationService`、`GarrisonDonationAdapter`、`TroopTransferService` 的入口都要复核授权，防止只改目的地搜索而让交易层绕过绑定。
- 正式解散：**不能先释放绑定再结算**。原版可能根据非绑定地选择解散去处，应优先引导至主/副绑定地，并让 `DisbandSettlementCoordinator`/`DisbandSettlementService` 在结算时检查有效绑定。
- 若正式解散是原版强制的、绑定基地已失效/被围/无容量，必须记录清晰的强制结算降级原因。是否允许最后兜底使用其他友方定居点，应当单独选择；为遵守“仅绑定地”，本阶段不把其他 Clan/非绑定驻军悄悄作为默认目的地，也不要为了严格限制而触发不受控循环或崩溃。
- **本阶段不改变80%/95%驻军接收软上限**，也不宣称已修复返乡因驻军容量不足而解散。固定75人、自动招募订单和独立返乡容量属于后续阶段。

## 5. 生命周期：什么时候占用、释放、重新分配基地

| 事件 | 行为 | 备注 |
|---|---|---|
| `MobilePartyCreated` | 仅为被预审准入的新普通 Lord Party 完成待提交绑定校验；不要为车队/驻军绑基地 | 若尚未完成首次实际动员，不进入正常规划 |
| `MobilePartyDestroyed` | 幂等解除双向绑定、释放临时预留、取消指向该 Party 的 Mod 任务 | 仅清绑定，不凭空退还已在战斗中损失的兵 |
| `OnPartyDisbandStartedEvent` | 标记 `PendingDisband`，不解绑 | 原版可能因新领队加入而撤销解散 |
| `OnPartyDisbandCanceledEvent` | 清除 PendingDisband；保留原绑定 | 需验证 Party 仍有效且同 Clan |
| `OnPartyDisbandedEvent` | 确保士兵结算已完成之后释放基地 | 跨原版监听器的真实时序必须探针核验 |
| `OnPartyRemovedEvent` | 无条件幂等兜底释放；若结算未完成必须由原始结算服务持有身份快照 | 不以此事件做二次兵员转移 |
| `OnSettlementOwnerChangedEvent` | 只取消已失去的那个绑定点，取消相关待执行任务，重评可用基地 | Party 保有另一个合法基地则不销毁；无基地进入待分配状态 |
| Clan 转国/失去 Clan/领队变更 | 对所有该 Clan 关联绑定重验（必要时提供通用 `ValidatePartyAndOwner`） | 不能把 Party 自动变成新定居点所有者的 Party |
| 新游戏 / 读档恢复 | 校验数据、迁移、重建反向索引、确认唯一性 | 不发生任何不可逆士兵事务 |

### 5.1 失去唯一基地后的现役 Party

已有 Party 不应因城堡易主而立刻强制解散。绑定状态为 `NeedsRebinding`；在同 Clan 仍有其他空闲合法城镇/城堡时，按确定性策略重新分配，否则暂停本 Mod 的基地补员/本地休整任务。原版军事行动继续运行，不能为了基地重分配而突然强制改变原版战斗/逃跑行为。只有 **新创建** Party 必须先有基地并满足50兵准入。

### 5.2 不要混淆绑定释放与兵源可用

`OnPartyRemovedEvent` 释放基地槽位，并不等于原基地立刻拥有50名可招募士兵。下一支创建仍要重新经过真实库存和财务审批。绑定也不能“预留”未生成的职业兵虚拟报价。

## 6. 旧存档迁移与顺序

1. 以新存档键（例如 `modifiedarmy_party_settlement_bindings_v1`）读取绑定；旧存档缺键视为“未迁移”，不得误以为绑定表完整为空。
2. 在 `CampaignRecoveryCoordinator` 执行 `RevalidateAfterCampaignRecovery()` **之前**完成绑定对象解析与反向索引重建，再恢复 AI 日任务，否则旧补员任务会在绑定未就绪时被错误保留/取消。
3. 对既有普通 Lord Party 排序（ClanId, PartyId 等稳定ID），优先保留已经有效的绑定，再依次尝试分配空闲同 Clan 封邑。主基地分配优先于给现有 Party 追加第二基地，避免有人占两个基地、另一支 Party 没基地。
4. 既有 Party 超过可用基地数量时，标记 `LegacyUnbound` / `NeedsRebinding`；**不强制解散、不补造50兵，也不得把它误判为新建资格的成功。** 可在后续腾出基地时自动绑定。
5. 每日及所有事务使用同一状态模型：正常新 Party 没绑定应拒绝 Mod 的基地补员；旧 `LegacyUnbound` 按可观测的兼容策略处理，建议只保留原版正常军事行为，不开放全 Clan 驻军补员绕过本次设计。
6. 迁移结果应汇总一次：迁移 Party 数、主基地数、副基地数、未绑定数、冲突数、取消旧任务数。重复读档、保存、再加载结果相同，不会发生额外招募或返乡。

## 7. 具体文件修改清单（相对于 `ModifiedArmy/ModifiedArmy/`）

| 优先级 | 文件或新类 | 修改目标 |
|---|---|---|
| P0 新增 | `PartyManagement/Bindings/PartySettlementBindingBehavior.cs` | 唯一权威保存、临时预留、唯一性、生命周期、旧档迁移、查询接口 |
| P0 新增 | `PartyManagement/Bindings/PartySettlementBindingRecord.cs` | Saveable 纯数据契约（PartyId、ClanId、Primary/Secondary、版本等） |
| P0 修改 | `Fief/Models/FiefPartyManager.cs` (`FiefSaveDefiner`) | 注册新 Saveable 定义与集合；不得复用旧类型/容器ID |
| P0 修改 | `Main.cs` | 注册绑定 Behavior；确保在统一 AI 规划与恢复屏障之前完成依赖初始化 |
| P0 修改 | `ForceManagement/Behaviors/CampaignRecoveryCoordinator.cs` | 加入绑定索引恢复/迁移步骤，然后再复审 AI 任务 |
| P0 新增 | `PartyManagement/Creation/InitialPartyCreationEligibilityModel.cs` | 纯读候选封邑、最少50人预审、避免隐式双计、预算含拟新增 Party |
| P0 新增 | `PartyManagement/Creation/InitialPartyMobilizationService.cs` | 正式创建时冻结上下文、原版模板兵处理、真实动员事务与回滚 |
| P0 新增 | `PartyManagement/Patches/LordPartyCreationAdmissionPatch.cs` | `LordPartyComponent.CreateLordParty` 创建入口把关；区分普通AI Lord/主角/非受管队伍 |
| P0 适配 | 原版完整出生路径（先探针） | 处理 `HeroSpawnCampaignBehavior.SpawnLordParty` 新游戏模板兵追加；不造成免费兵/重复兵 |
| P1 修改 | `ForceManagement/Services/PartyForceCandidateService.cs` | `Fief/Professional` 候选只枚举合法绑定基地；保留候选真实审批和原生 AI 评分 |
| P1 修改 | `ForceManagement/Behaviors/AiForcePlanningBehavior.cs` 及任务验证 | 解绑/易主/读档导致目标不再绑定时废除旧任务、重新规划 |
| P1 修改 | `ForceManagement/Services/WarRestPlanningService.cs` | 休整基地只从绑定范围选；原服役批次返还仍按源封邑 |
| P1 修改 | `ForceManagement/Missions/PartyVisitExecutionService.cs` | 自然访问捐兵的绑定校验，防止交易绕过 |
| P1 修改 | `ForceManagement/Services/DemobilizationService.cs` / `DemobilizationBehavior.cs` | 和平复员普通兵目标限制、归还真实源封邑兵例外（当前功能开关保持关闭） |
| P1 修改 | `ForceManagement/Services/DisbandSettlementService.cs` / `DisbandSettlementCoordinator.cs` | 先结算后解绑、优选绑定基地、不合法目的地明确退化原因 |
| P1 检查 | `Garrison/Services/GarrisonDonationAdapter.cs`, `ForceManagement/Services/TroopTransferService.cs` | 统一执行授权检查；不要仅在候选层约束 |
| P1 修改 | `ModuleData/submodule_strings.xml`、`Languages/CNs/modifiedarmy-zho-CN.xml` | 新错误和结果原因中文本地化，不在业务代码硬编码中文日志 |
| 不修改 | `NewClanTierModel.cs` | 当前数量上限和 TalentMagnet 保留，无需降低理论 Party 上限 |
| 不修改 | `GarrisonRecruitmentCapacity.cs`, `militaryConfig.xml` 驻军比例项 | 旧75%/50%下限与95%/80%上限保留到后续驻军计划阶段 |

说明：路径为建议的最小变更位置；新增 `SaveDefiner` 可以选择在现有 `FiefSaveDefiner` 中注册或单独新 Definer，但要保持全局 ID 唯一，先核对现有工程注册机制。

## 8. 分阶段开发步骤（建议五个彼此独立的提交）

### W1 — 绑定纯数据 + 生命周期 + 存档（不碰创建）

- 完成单向持久化 + 运行期反查、主/副绑定 API、冲突防御、读档重建、老档迁移。
- 注册 `MobilePartyDestroyed`、`OnPartyDisbandStarted/Cancelled/Disbanded`、`OnPartyRemovedEvent`、`OnSettlementOwnerChangedEvent`，先只做清理与诊断。
- 以独立 `BindingInvariantAudit` 检查每一座基地最多一个 Party、每 Party 最多两座，以及所有者匹配。

### W2 — 只改普通 AI 补员目的地与任务续期

- 修改 `PartyForceCandidateService` 仅对绑定地生成本 Clan 的 `Fief/Professional` 候选。
- 修改现有 `AiForcePlanningBehavior` 和恢复流程：任务目标丢失绑定时取消并重新让原版 AI 思考；禁止创设本 Mod 的冲突 AI 目的地评分。
- 证明原有“每日任务 → 原版AI评分 → 到达结算”链路不被打断。

### W3 — 创建时序探针 + 50人严格准入

- 先仅增加**只读日志探针**，确认新游戏和中途重建中的真实初始化时序、模板士兵出现时机、在定居点内/城外状态。
- 新建预审模型：独立测算真实封邑兵/驻军可调兵、拟新增 Party 的财务影响、部队容量、50正规兵门槛。
- 统一 Hook 各个实际会创造普通 Lord Party 的入口；不能只拦 `HeroSpawnCampaignBehavior`，遗漏直接 `MobilePartyHelper.SpawnLordParty` 等路径。
- 对非目标 Party 完全不改变原版创建流程。

### W4 — 首次动员原子化 + 正式绑定

- 新增创建专用事务和失败回滚，避免“有库存就创建，事后再补兵”的状态间隙。
- 把原版新建模板兵与本 Mod 首次动员的真实士兵严格区分，测试不重复增加兵力。
- 真实来源兵员合计至少50后才将 Party 视为创建成功并正式绑定；失败资源全部回滚。
- 新建 Party 初始可在主基地取得兵源，不得向异地未绑定地点瞬移征兵。

### W5 — 返乡/休整/正式解散授权及回归

- 正常移交、休整目的地、复员只允许绑定基地；封邑批次返回真实源封邑。
- 正式解散必须先做源兵员结算，再释放绑定，确认不会二次丢失、二次退兵。
- 最后做旧存档与全游戏长期运行验收。

## 9. 必须通过的验收用例（建议 Worker 自动化 + 实机日志）

| 编号 | 场景 | 预期 |
|---|---|---|
| T01 | 一个城堡只绑定 Party A，Party B 请求同一堡 | B 拒绝绑定，无重复占用 |
| T02 | Party A 已有2个基地，再请求第三个 | 拒绝，不破坏原绑定 |
| T03 | 仅1空闲基地，Clan 技能使理论 Party 上限从1提高到2 | 上限数值保留，但不能创建无基地第2 Party |
| T04 | 无封邑 Clan 要创建普通 Lord Party | 不创建；不影响原版其他类型 Party |
| T05 | 基地驻军人数足够，但扣除当前驻军保留线后可调少于50 | 不创建，日志准确显示剩余可调数 |
| T06 | 基地25名可调普通驻军兵 + 25名真实合法封邑兵且财务允许 | 可形成50名真实士兵，再创建、绑定；不重复计算，且满足现有封邑兵最低批量25的限制 |
| T07 | 基地预览足50，但新 Party 计入 Clan 分摊后只能负担40 | 不创建，来源名册和金钱不发生变化 |
| T08 | 同日两个领主同时申请同一个基地 | 临时预留保证最多一个创建流程提交 |
| T09 | 创建期间基地易主或驻军被取走 | 取消并回滚，不留下半成品 Party |
| T10 | 新游戏模板初始化会自动加兵 | 不把免费兵算入50；最终 Party 兵力与源减少数及英雄数可对账 |
| T11 | 中途恢复领主新建 Party | 同 T10，不存在新游戏专属额外赠兵重复问题 |
| T12 | 新 Party 出现在主基地城外，普通调兵要求 CurrentSettlement | 采用合法入城或专用创建事务，不绕过全局同地检查 |
| T13 | Party A 绑堡A、城B，Clan 另有城C | 招募 `Fief/Professional` 仅候选 A/B |
| T14 | 失去城B，城A仍有效 | 保留 Party 和城A绑定；取消指向B的未完成任务 |
| T15 | Party 战斗中完全覆灭 | 移除时释放A/B的绑定，不平白返还已阵亡兵 |
| T16 | Party 开始解散然后取消 | 原绑定仍在；不因开始事件提前释放 |
| T17 | Party 正式解散、封邑服役兵来自原始封邑C | 原批次必须返C，普通士兵按绑定合法驻军结算，最后清绑定 |
| T18 | Party 到未绑定的同Clan城堡自然访问 | 不发生本 Mod 的普通驻军捐兵或抽兵；常规原版行为不被错误劫持 |
| T19 | 已存 Recruit 任务指向易主/解绑的城B | 任务失效、原版AI重新评分，不继续前往B |
| T20 | 加载旧存档：Clan拥有2堡但已有3支 Party | 保留3支 Party；至多2支绑定，剩余 LegacyUnbound；新建仍拒绝 |
| T21 | 连续保存/加载3次 | 绑定映射稳定、反向索引无冲突、不会重复招兵/退兵 |
| T22 | Clan没有兵员但有财政、日职业兵生产预览有50 | 不视为现实库存，不创建 |
| T23 | 原版技能或政策提高领主数量上限 | 理论上限保持；创建仍须双硬条件 |
| T24 | 带港口/船舶的创建、海军移动、围城及紧急撤离 | 不因绑定逻辑阻断原版安全/逃生路径 |
| T25 | 关闭 UnifiedAIForcePlanning 或创建规则不适用队伍 | 需有明确回退边界，不产生半绑定与空壳的受管 Party |

**最重要的运行期对账**：`源驻军正规兵减少 + 源封邑兵减少 == 新 Party 从真实源增加的正规兵`；同时核对真实服役批次总数、Clan 现金/预算承诺、成功创建数、临时预留与绑定唯一性。原版模板生成兵如被移除，不得计入动员成功数。

## 10. 日志与诊断（新增日志建议）

建议只对创建准入失败、首次动员事务失败、绑定冲突及迁移异常打印 Notice/Warning；日常合格正常路径用 Debug 或 Info：

```text
[PartyCreationPreflight] clan=... hero=... primary=... garrisonSafe=34 fiefApproved=20 financeApproved=50 totalApproved=50 gate=Pass
[PartyCreationCommit] party=... base=... garrisonActual=32 fiefActual=18 regulars=50 rollback=false status=Committed
[PartyCreationRejected] clan=... hero=... reason=NoUnboundSettlement|Below50RealTroops|FinanceLimited|UnderSiege|NoLegalLeader
[PartyBinding] party=... primary=... secondary=... version=... action=Bind|Unbind|Rebind
[PartyBindingRecovery] migrated=... unbound=... conflicts=... invalid=... taskCancelled=...
```

这些是未来新增的诊断格式，不是当前仓库已经出现的日志。日志严禁调用再次生成、再次提交招募或消费 Hearth 的方法；所有预览只读。沿用项目已有本地化文本与日志分级，不引入大量每小时打印。

## 11. 本阶段明确不做与后续依赖

- **不做** `GarrisonRecruitmentOrder` 一次性招募计划。今后它应当每天动态调整剩余额度，招募完成自动删除；不保持长期编制目标。
- **不做**固定75名驻军保留数，不移除当前边境75%/内陆50%比例保护，不调整95%/80%补员软上限。新的50人创建资格可能因此比较严格，这是本阶段的已知限制。
- **不做**按 Party 最大编制永久预留返乡空位，不让战时休整强制转走普通兵，不开启目前默认关闭的跨城增援和 WarRest。
- **不做**对 `NewClanTierModel` 数量上限或技能加成的反向改造。
- **不做**为创建50人引入持久虚拟兵源池；库存与日订单严格区分。
- 后续阶段：等绑定、创建和真实动员稳定后，再单独实施固定75人守军线、一次性可动态调整的驻军招募订单、返乡容量管理，并根据运行日志评估新 Party 重建速度。

## 12. Worker 执行指令 / PR 边界

1. 使用当前 Git HEAD 重新核对文件路径与所有 Harmony 目标签名；如仓库 HEAD 已变化，先报告差异。
2. 按 W1→W5 分离提交，先报告需要对原版创建链路新增的探针与真实结果，再决定使用何种最小补丁入口。
3. 必须显式复用 `PartyExchangeRunner` / `FiefPartyManager` / `RecruitmentModelManager` / `ClanRecruitmentBudgetManager` 的唯一业务权威；不得另造第二套成本、库存与封邑服役计算，不在 Harmony Patch 中实现大段领域模型。
4. 遵守原有 `CampaignRecoveryCoordinator` 事务屏障、幂等操作和存档兼容，不覆盖已有存档键或 Saveable Type ID。
5. 任何无法保证50真实士兵转移、成功回滚、原版模板兵不重复的路径，**拒绝创建并记录原因**；不允许用 `AddToCounts` 免费补足50人。
6. 提交需包含单元测试/模型测试、上述 T01—T25 的验收结果、实机日志和最终代码差异清单。未经测试不能宣称“已经解决 AI 补员”或“零兵丢失”。

## 核验代码入口（固定到审查时提交）

- [NewClanTierModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/PartyManagement/Models/NewClanTierModel.cs)
- [PartyForceCandidateService.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs)
- [AiForcePlanningBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/AiForcePlanningBehavior.cs)
- [PartyExchangeModel.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs)
- [FiefPartyManager.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [DisbandSettlementService.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/ForceManagement/Services/DisbandSettlementService.cs)
- [CampaignRecoveryCoordinator.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/ModifiedArmy/ModifiedArmy/ForceManagement/Behaviors/CampaignRecoveryCoordinator.cs)
- [原版 HeroSpawnCampaignBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/code/HeroSpawnCampaignBehavior.cs)
- [原版 LordPartyComponent.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/code/LordPartyComponent.cs)
- [原版 CampaignEvents.cs](https://github.com/GuihuaZhou/bannerlord/blob/72e407a04f6ae73735877da96b4ff867d370faed/code/CampaignEvents.cs)
