# 02｜PartyExchangeModel计划生成与安全执行引擎

> 项目：`GuihuaZhou/bannerlord` · 基线：`main` / `4ae5ff66084efa33e362c13ee7f8b686a22c17ab`（2026-10-22 核对）。
> 状态：**实施计划；并非代码已经修改或测试通过**。Worker 开始前必须读取最新仓库 HEAD；如果源码发生变化，以当时实际代码为准并报告差异。
> 范围：`ModifiedArmy/ModifiedArmy/`；除注明外均以此为相对路径。

## 1. 目标

建立单一“现有兵员流转决策门”，支持 `Garrison→Mobile`、`Mobile→Garrison`、`Fief→Mobile`、`Mobile→Fief`，统一日志与幂等。当前已有服务应复用，不重写成熟的Hearth与批次回滚；此阶段新执行入口可测试但**不接管事件、不关闭原版**。

## 2. 已确认代码及风险

- `Garrison/Models/GarrisonTransferPolicyModel.cs:TransferApprovedTroopsFromGarrison`：真实执行驻军→野战；读取 `GarrisonDefense.GetSafeTransferOutCount`、在役封邑保护、`RecruitmentModelManager.Model.BuildPlan()`。**当前一次排序然后依次全部转移**，对目标Party和来源驻军兵种的动态影响有限。
- `ForceManagement/Services/TroopTransferService.cs:TransferFieldToGarrison`：野战→驻军；`GetAllowedDonationCount`、驻军接收容量、`FiefPartyManager.GetServingTroopCounts`、统一招募模型，已打文件日志。
- 同文件 `ValidateExchangePlan`要求 `FieldToGarrison.TotalCount > 0` 且 `GarrisonToField.TotalCount > 0`。**现有双向`ExchangePlan`不支持普通单向调兵**，不得直接复用这项约束。
- `ForceManagement/Services/ForceManagementFacade.cs`：`Exchanges = new DisabledPartyGarrisonExchangeModel()`。
- `Fief/Models/FiefPartyManager.cs`：`RecruitFiefTroopsFromSettlement`, `TryReturnFiefTroops`；`FiefPartyRecruitment`和`FiefPartyReturn`持有事务/批次逻辑。
- `ForceManagement/Services/DisbandSettlementService.cs`、`ForceManagement/Services/ReinforcementCargoService.cs`也是转移调用者，必须做旁路清查。

## 3. 设计决策

### 3.1 单一模型审批，而非巨型执行模型

`PartyExchangeModel.BuildPlan(context)`给出批准的操作、来源/目标、兵种及数量；不执行名册修改。

`PartyExchangeService.Execute(plan)`依计划版本、操作类型和原有专用Service完成真实提交。旧执行函数可以先作为内部适配器，调用者不得脱离模型批准计划而自作选择。

过渡期对旧Service签名“只有数量、没有兵种清单”的问题：**首选扩展为可消费精确批准Manifest的内部API**，而不是模型批准了A士兵、Service又重新选择B士兵。不得重复应用同一个名单。

### 3.2 四个方向

**Garrison→Mobile**：需要真实兵力缺口；来源高于防御最低目标；考虑外出征粮队预留占编和封邑在役保护；目标工资、编制、软兵种/质量偏好；既有同Clan转移不支付重复购买成本。

**Mobile→Garrison**：驻军需要接收或当前任务为明确批准的上缴；野战Party保留任务要求的最低作战人数；驻军编制上限、实际工资与有效人数校验；在役封邑不得被误作为普通兵转入。

**Fief→Mobile**：`FiefPartyManager`和`FiefPartyRecruitment.RecruitTroopsToParty`处理Hearth、封邑库存和`FiefServiceBatch`创建；保留Player/AI不同成本和当前最小AI批次审批；Model仅计划并委派事务。

**Mobile→Fief**：选取**实际来源封邑+批次**；由`TryReturnFiefTroops`执行名册扣减、批次调整、动员释放和冷却写入；正常AI返乡须已到来源封邑（第04细化），特殊整支Party解散有独立明确授权场景。

### 3.3 执行计划

建议包含：`Operation`、`Intent`、`PartyId/Clan`、`SourceKind/TargetKind`、`Settlement`、`BatchId`、`TroopManifest`（普通/伤兵）、`SourceRosterHash`/`TargetRosterHash`、目标和驻军上下限快照、`ApprovedCount`、`ReasonCode`、执行身份（操作事件唯一ID）。

执行时必须重新核验最新局势：Clan、兵源归属、战争、Party活动状态、Siege/MapEvent、任务、名册、已服役批次数、已接收的其他Party调拨、两个Party工资预算。失效则拒绝或重建新计划，不“凑数执行”。

### 3.4 兵种选择策略

复用现有 `DefaultAIRecruitmentModel` 的文化/战斗角色/质量软模板，不能强制硬兵种比例。改善原来一次性排序的偏差，建议先每一小批更新接收方模拟组成和来源驻军剩余组成，再评估边际收益；设置保守的单兵种提取量上限或软惩罚，优先满足接收方缺口。**兵种占比不能单独触发无缺口抽兵**。

具体权重先保持可配置/独立纯函数并提供对照测试，不在本阶段擅自调整XML军制。

### 3.5 日志与守恒

每笔操作：Party/Clan、起止资源、任务ID、操作类型、原因、批准与实际人数、兵种及伤员、来源/目标前后名册摘要、批次/Hearth状态。文件日志即可。

单向普通兵员转移保持来源损失=目标增益；封邑返乡应遵循“名册扣减+批次减少+Hearth释放+冷却入账”，不能把返回者立即视作可征召兵源；失败可回滚，重复事件不会重复执行。

## 4. Worker实现步骤

1. 加入 `PartyExchangeModel` 单向 `BuildPlan()`（空计划/不可执行均有ReasonCode），使用01契约。
2. 新增 `PartyExchangeService`；复用/适配`TroopTransferService`及`FiefPartyManager`，先实现 dry-run 和显式调用测试，不绑定日常事件。
3. 用统一Model审核替代`GarrisonTransferPolicyModel`直接选择的决策权；其驻军防御边界可以保留为共享Policy/计算组件。
4. 建立按Manifest精确执行，完整恢复伤兵，并在实际转移成功后统一记录日志；跨Roster操作采用预审及必要回滚。
5. 建立双向`BidirectionalSwap`独立开关，**不要**为完成单向流转就启用旧 `EnableAutomaticSwap`。
6. 清查 `DisbandSettlementService`, `ReinforcementCargoService`, `ReinforcementTransferService`，区分自然AI调兵与已有明确特殊任务授权；对受统一管理的主动调拨统一转到审批门，不破坏既有任务专用事务。

## 5. 最低验收

- [ ] 四种单向计划分别可生成/拒绝/执行；双向Swap继续关闭。
- [ ] 驻军Hard300、Border最低225且Effective225：批准抽出0。
- [ ] 驻军外出征粮队被计入有效编制及归还预留；转入后不产生超编。
- [ ] 同种Character普通15+在役25：普通调拨最多15，不破坏剩余25身份。
- [ ] 同一次访问或重放计划不重复加减兵员。
- [ ] 兵种清单严格等于实际移转，记录每次调拨；战前/战后Roster守恒（封建兵需额外批次/Hearth审计）。
- [ ] 执行过程中名册/归属改变时拒绝或成功回滚，绝不重复士兵。
- [ ] 未接入事件前游戏行为与基线相同；可独立回退。
