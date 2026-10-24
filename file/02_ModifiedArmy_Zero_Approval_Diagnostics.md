# W2｜统一调兵“请求N人、批准0人”诊断日志——设计与实现方案

> 状态：**仅新增日志，不修行为**；基线`main@ccd222b`；Owner：Worker W2；可独立于W1/W3/W4实施。  
> 核心调查对象：胡比亚（Hubyar）相同Clan野战部队到访后，分别请求28/53人、批准0人，提示“当前驻留、归属或安全条件不允许调兵”。

## 1. 证据与不允许偷换的结论

### 1.1 已确认的代码事实

- `PartyForceCandidateService.AddProfessionalCandidate()`根据同Clan `settlement.OwnerClan==party.ActualClan`、驻军存在、安全可调量和`RecruitmentModelManager.Model.BuildPlan`批准数创建“可行候选”，**没有校验执行端 `garrison.ActualClan`**。
- `PartyExchangeModel.BuildRosterPlan()`对`kindsMatch`、`field.CurrentSettlement==settlement`、`IsAuthorizedRosterPair`做**短路或运算**，任一失败即返回`InvalidOrUnsafeTransfer`；此时`RequestedCount`可为28/53，但计划的`SourceToDestination.TotalCount=0`。
- `IsAuthorizedRosterPair()`常规路径要求 `source.ActualClan != null && source.ActualClan==destination.ActualClan && source.ActualClan==settlement.OwnerClan`；攻占后特许路径另有条件。
- `PartyExchangeService.Validate()`可能返回`RosterChanged`、`UnsafeAtExecution`、`CapacityChanged`、`EmptyManifest`等。
- `PartyExchangeService.GetReasonText()`把`InvalidOrUnsafeTransfer`和`UnsafeAtExecution`映射到同一个中文“unsafe”提示，丢失具体失败谓词。
- `PartyExchangeService.Log()`目前**静默抑制visit操作的**`NoApprovedCapacity/NoApprovedTroops/EmptyManifest`，排查时需要允许失败诊断模式查看这类结果。

**尚未证实：** 胡比亚驻军`ActualClan==null`是否成立；真正失败是身份、驻留、战斗状态还是执行时快照变化。**不得基于猜测先改身份判断。**

### 1.2 独立的正常日志（不要误诊）

`ExternalReinforcementNeedModel.Evaluate()`只有`MinimumCount-EffectiveGarrisonCount>0`才请求外援。胡比亚驻军410、最低205，因此“对外请求0人”是正常判定，**不是**本任务研究的“对领主批准0人”。保留原防御规则和七天预测逻辑。

## 2. 最小改动目标

给每笔失败的统一交易产生一组**结构化、可读、不会触发业务副作用**的诊断：

```text
Planning(selected) -> Arrived -> BuildPlan(preconditions) ->
Admission/Manifest -> Execute.Validate -> Execute.Result
```

要能回答：
1. 当初为何把胡比亚评为“可补员53”的候选？
2. 到达后哪些上下文改变了？
3. 哪个具体布尔判定为False？
4. 交易是否根本没进入招募资格/预算审批，还是审批为0？
5. 真实拒绝发生在 Model 阶段还是执行前二次验证？

## 3. 推荐修改文件

| 组件 | 文件路径（均从`ModifiedArmy/ModifiedArmy/`开始） | 任务 |
|---|---|---|
| 规划候选 | `ForceManagement/Services/PartyForceCandidateService.cs` | 记录被选中候选的依据，不记录全地图全部候选 |
| 已规划任务/到场 | `ForceManagement/Behaviors/AiForcePlanningBehavior.cs` | 记录任务标识、所选Settlement/Source及到达后的操作ID |
| 执行桥接 | `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs` | `ExecuteGarrison`/`BuildAndExecute`传递OperationId、记录规划→审批结果；覆盖返回`Failed(...)`的情况 |
| 审批Model | `ForceManagement/Models/PartyExchangeModel.cs` | 解构`InvalidOrUnsafeTransfer`、NoApprovedCapacity、NoApprovedTroops，不改变分支逻辑 |
| 最终执行 | `ForceManagement/Services/PartyExchangeService.cs` | 解构`UnsafeAtExecution`、CapacityChanged、RosterChanged、回滚等；确保失败不被静默过滤 |
| 可选诊断辅助 | 新建`ForceManagement/Diagnostics/PartyExchangeFailureDiagnostics.cs`（或类似） | 纯只读快照和格式化，不接管审批或交易 |
| 配置 | `common/MilitaryConfig.cs`、`ModuleData/militaryConfig.xml` | 可选增加`EnableExchangeFailureDiagnostics`布尔开关，默认`false`；兼容旧XML缺字段 |

### 3.1 诊断开关和噪音控制

推荐`EnableExchangeFailureDiagnostics=false`默认关闭；定位存档时改为true（需要重载配置）。

- `false`：沿用现有行为与日志抑制；`true`：对**调拨失败**（尤其`GarrisonToField`）输出一份详细诊断，常规成功只保留原摘要。
- 默认使用当前`RecruitmentLogFilter.ShouldLog(party)`仅显示玩家王国；如需全地图排查须有明确调试配置或按目标Party/Settlement筛选，而非无界输出。
- **一个OperationId一个终态记录**；不要在`BuildPlan()`与`Execute()`两边重复输出相同终态；前者可以先生成内存诊断对象，由Service在失败时统一写。
- 成功路径不能新增高频详细日志；多次访问/每日Tick同一天已有去重应保留。

## 4. 诊断数据合同（必须写真实值）

建议一个不进存档的`PartyExchangeDiagnosticSnapshot`纯数据对象，字段如下：

**关联标识：** `Day, OperationId, Phase, Intent, Purpose, Operation, SourceKind, SettlementId/Name, InitiatorPartyId/Name, SourcePartyId, DestinationPartyId, PlanReason, ResultReason, Requested, PlannedApproved, ExecutedTransferred`。

**身份/位置：** `SourceActualClanId, DestinationActualClanId, SettlementOwnerClanId, SameClanSourceDestination, SourceClanMatchesOwner, DestinationClanMatchesOwner, SourceIsGarrisonParty, DestinationIsGarrisonParty, SourceKindMatches, DestinationKindMatches, FieldCurrentSettlementId, FieldAtTargetSettlement, SourceActive, DestinationActive, SourceInMapEvent, DestinationInMapEvent, SettlementIsUnderSiege, PurposeAllowsCaptureException`。

**数量/安全：** `PartyLimit, CurrentCount, ForceTargetCount, ForceGap, CandidatePreviewApproved, SourceRegulars, SourceWounded, DestinationRegulars, DestinationWounded, GetSafeTransferOutCount, ProtectedServingFiefCount(if available), GetSafeFieldSurplus(if relevant), GarrisonTransferFreeSlots(if relevant), ManifestTotal, PerReasonApprovals(if admission reached), SnapshotMatched, ExecutableCapacity`。

**失败解释：** `FirstFailedPredicate`使用稳定英文枚举或字符串，如`SourceKindMismatch`、`GarrisonIdentityMismatch`、`FieldNotAtSettlement`、`SourceClanNull`、`OwnerMismatch`、`SourceInactive`、`DestinationInBattle`、`SettlementUnderSiege`、`CaptureExceptionNotApplicable`。保留原始`ReasonCode`作为最高层结果，**不要擅自将其替换成新逻辑返回码**。

字段取值必须安全处理null；Party可使用`StringId`，使用对象引用等价比较，而不是仅比较名称；中文显示用已有`PartyLogFormatter`和文本资源。

## 5. 不影响行为的实现方式

### 5.1 拆分诊断谓词，但不改变原判断

Model当前：

```csharp
if (!kindsMatch || field?.CurrentSettlement != settlement ||
    !IsAuthorizedRosterPair(source,destination,settlement,context.Purpose))
    return Reject(context,"InvalidOrUnsafeTransfer");
```

**推荐做法A（最安全）**：保留该`if`原样不动；仅在返回之前调用 `Diagnostics.CaptureRosterRejection(context,source,destination,field,garrison,kindsMatch)`。这方法只读属性并计算与原函数等价的分解布尔项；绝不写名册/账本、绝不做第二次`PrepareContext()`。

**推荐做法B（可读性更好）**：重构为只读的`RosterAuthorizationDetails`，让原校验和日志共享返回对象；但要严格验证 `NewlyCapturedSettlement` 例外分支的语义、短路/null行为**完全相同**，不得引入行为变更。第一轮优先A。

### 5.2 Model发生零审批时继续查因

- `NoApprovedCapacity`：记录请求数、源安全调出、接收方容量、计算结果；不要只写“0”。
- `NoApprovedTroops`：区分 `allowed>0 但候选数量=0`、`protectedCounts吃完可用士兵`、`soft withdrawal筛选后总数=0`、`RecruitmentModelManager.Model.BuildPlan`每候选限制等；**注意GarrisonToField当前走`FillSoftGarrisonWithdrawalManifest`，不等于所有路径都用同一个BuildPlan**。
- `EmptyManifest`：记录生成前后名册指纹与每兵种总数；避免每个士兵重复打印，按拒绝主因分组聚合。

### 5.3 Service执行二次验证

`Validate()`按原先优先级返回`MissingApprovedPlan → PlanRejected → OperationAlreadyCompleted → BidirectionalSwapDisabled → EmptyManifest → RosterChanged → ValidateRosterBoundaries`；诊断时记录**第一个导致终止的阶段**。

`ValidateRosterBoundaries()`保留现有执行限制，记录：

- 真实驻军身份：`source==settlement.Town.GarrisonParty`或`destination==...`。
- 执行时野战队伍是否停留在`CurrentSettlement==settlement`。
- `Source/Target ActualClan`与`OwnerClan`、NewlyCapturedSettlement特例。
- 双方Active/MapEvent、围城状态。
- `GetSafeFieldSurplus`、`GetTransferFreeSlots`、`GetSafeTransferOutCount`、接收方硬上限及实际manifest总数。
- `MatchesSnapshot`失败时记录前后总数、伤兵数及**匿名化/截断**的名册指纹对比；不能打印无限长整份Roster。

**危险：** 诊断函数不能调用会改变世界的`FiefPartyManager.ReconcilePartyFiefTroops`、招募`BuildPlan`（若依赖可变财务缓存不明）、任何`AddToCounts`、`Commit`、`TakePrisonerAction`、`TryConsume...`，也不能重复发送游戏事件。现有`Runner.PrepareContext`本身可能执行Reconcile；**日志只使用它之后的现有快照或属性读取，不额外触发它**。

## 6. 输出格式（示意，非本存档事实）

```text
[ExchangeDiag][Planning] op=force:partyid:day:version:Recruit
 party=伊勒塔斯 id=... clan=banu_gilad target=胡比亚 kind=Professional
 targetGap=53 candidateApproved=53 garrisonSafe=82
[ExchangeDiag][BuildPlanRejected] op=... operation=GarrisonToField
 request=53 manifest=0 reason=InvalidOrUnsafeTransfer
 kindsMatch=true sourceIsRealGarrison=true atSettlement=true
 sourceActive=true destinationActive=true mapEventSource=false mapEventDestination=false siege=false
 sourceClan=null destClan=banu_gilad ownerClan=banu_gilad
 strictOwnerMatch=false captureException=false failed=SourceClanNull
[ExchangeDiag][Final] op=... executed=false moved=0 planReason=InvalidOrUnsafeTransfer
```

如果实测`sourceClan=banu_gilad`且`atSettlement=false`，诊断必须如实显示`FieldNotAtSettlement`而非沿用上例结论。

## 7. 诊断验收场景

| ID | 操作 | 应打印/不应发生 |
|---|---|---|
| Z01 | 胡比亚请求28人批准0 | 具体失败字段；同一操作的Preview/Arrive/Model/Result能串联 |
| Z02 | 胡比亚请求53人批准0 | 同上，不得只写中文“安全条件不允许” |
| Z03 | 驻军`ActualClan=null`的模拟条件 | 显示`SourceClanNull`；**不自动放行** |
| Z04 | 到达时`CurrentSettlement`不匹配 | 显示`FieldNotAtSettlement`；不重复交易 |
| Z05 | 围城/战斗/失活 | 对应精确门槛，返回码与现有行为一致 |
| Z06 | 驻军可用安全数为0 | `NoApprovedCapacity`与源安全数；不开强制调出 |
| Z07 | 兵种均被封邑在役保护、软选择无法生成清单 | `NoApprovedTroops`并标明候选/受保护人数 |
| Z08 | 规划后驻军名册变化 | `RosterChanged`及快照差异；仍拒绝 |
| Z09 | 到场后接收方硬上限已满 | `CapacityChanged`与边界数值；仍拒绝 |
| Z10 | 完成一笔成功调拨 | 名册净变化与原版一致；失败详报不刷屏 |
| Z11 | 同日双事件触发或读档重规划 | OperationId可关联且终态不重复输出 |
| Z12 | 驻军最低人数已满足、外部增援申请0 | 不输出错误级ExchangeDiag，也不更改防御评估 |

## 8. 交付与后续决策

- PR只提供**诊断**；附真实失败日志或可控伪场景日志，解释哪些字段从哪里读出。
- 给出`git diff`证明 `BuildRosterPlan/Validate/Execute`判断、实际转移人数与原来完全一致；如无法证明则回退任何行为改动。
- 至少有日志开关关闭、打开两套回归；说明旧存档无需迁移。
- 完成后按真实失败谓词开**新的修复任务**；比如只有`SourceClanNull`实证成立时才考虑统一驻军所有权身份方案；不能在本PR悄悄改。
- 提交建议：`diag(army): trace zero-approved garrison transfers without changing rules`。
