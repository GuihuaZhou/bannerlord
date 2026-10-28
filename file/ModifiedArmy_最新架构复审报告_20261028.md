# ModifiedArmy 最新架构复审报告（2026-10-28）

## 0. 范围与证据等级

- 仓库：`GuihuaZhou/bannerlord`，主分支最新已核对提交：`3f57b3b10f32bec333ffc64d04ecb1325c744680`，此前整改锚点：`a3d46c90c9328bc776a5d480b7997d6d7c09613d`。
- 版本间差异：16 次提交，46 个文件变更；审查采用已上传《ModifiedArmy 架构整改 Worker 分阶段实施包 V2》S0–S8，以及 GitHub 最新源码、`baseline/` 阶段报告、验收矩阵。
- **已独立核对**：GitHub 文件、代码链路、两个模组的 manifest / csproj、新增服务与原 Behavior 的职责、最新提交差异。
- **未独立验证**：Windows Release 编译、游戏启动、旧/新存档和 30 天运行；仓库报告称编译 0 errors / 7 warnings、短时游戏 smoke PASS，但 T01–T24 全部 BLOCKED。这些不能被本次静态审查视为 PASS。

## 1. 首要问题（按优先级）

### P0：ModifiedArmy / ModifiedPolitics 在加载清单和编译引用形成双向依赖

- `ModifiedArmy/ModifiedArmy/SubModule.xml:9-12` 新增 `<DependedModule Id="ModifiedPolitics" />`。
- `ModifiedPolitics/SubModule.xml:10-12` 已有 `<DependedModule Id="ModifiedArmy" DependentVersion="v1.0.0" Optional="false" />`。
- `ModifiedArmy/ModifiedArmy/ModifiedArmy.csproj:28-29` 显式引用 `ModifiedPolitics.dll`；`ModifiedPolitics/ModifiedPolitics/ModifiedPolitics.csproj:61-62` 显式引用 `ModifiedArmy.dll`。
- **确定事实**：依赖图存在环，S7 的“显式声明必需依赖”并未解决整个仓库的依赖拓扑，反而将已有程序集双向依赖升级为 manifest 双向声明。
- **潜在影响**：模组加载排序不可能同时满足两者“先于自身加载”的约束；启动顺序、解析及版本兼容有风险。实际加载器表现仍需游戏验证。
- **建议**：单独 S7-fix 修复：选定一个方向（优先评估 `ModifiedPolitics -> ModifiedArmy` 是否为既有必需方向），让另一个方向通过既有 `IWarDispositionProvider` 接口/回调适配解除强程序集引用，并同步修改对应 SubModule；若反过来更合理，必须完整消除 Politics 到 Army 的编译/运行期依赖。不可只删一条 XML；先做两模组加载与功能测试。

### P1：读档恢复覆盖 `Suspended` 的状态语义

- `ForceManagement/Missions/PartyVisitExecutionService.cs:155-174`：发生 `RecoveryRequired` 将任务置 `Suspended`。
- `ForceManagement/Missions/PartyForceTaskPersistence.cs:17-35,64-70`：会保存/恢复任务状态。
- `ForceManagement/Behaviors/AiForcePlanningBehavior.cs:192-252`：`RevalidateLoadedTask` 未特判 `Suspended`，统一改为 `Traveling` 或 `Cancelled`。
- `ForceManagement/Missions/PartyForceTaskCoordinator.cs:49-61,66-106`：超过 7 天的任务可能被清理；正常规划也未明确保护 `Suspended` 不被替换。
- 结果：任务层不能稳定保持“待人工核对/冻结”状态；虽然 `TransferOperationJournalBehavior.IsDomainFrozen` 可阻止受其覆盖的事务重写，但任务状态、行动路线和错误报告可能与真实冻结不一致。
- **建议**：读档校验先查询 Journal 的域冻结状态；冻结任务保持 `Suspended`，不重新发出常规行动命令；正常每日规划不得替换被冻结的操作状态；冻结记录的人工核对/解冻应有明确定义，禁止自动回放。

### P1：雇佣兵事务的提交日志落后于已发生副作用

- `ForceManagement/Services/UnifiedRecruitmentExecutionService.cs:177-189`：try 块内变更金币、市场数量、士兵、预算；catch 做补偿。
- 同文件 `232-262` 附近：`CampaignEventDispatcher.Instance.OnTroopRecruited(...)` 和日志位于 `journal?.Record(result, CurrentDay())` **之前，且在 try/catch 外**。
- 若事件监听器、日志/格式化抛异常，可形成“资源已实际提交、操作 ID 未登记”的状态；上层看到异常后可能再规划执行。代码层已证实这一异常路径，但是否由实际事件监听器触发尚未复现。
- **建议**：明确资源提交成功的边界并优先持久化提交结果，再执行可失败的通知、日志；事件副作用不可盲目回滚，补充事件异常注入与同 ID 重入测试。

### P1：征粮 Behavior 仍保留活跃的兵员副作用和大型业务逻辑

- `Garrison/Supply/SupplyPartyBehavior.cs` 当前约 1446 行；`SupplyPartyProvisioning.cs` 约 651 行；`SupplyPartyTransaction.cs` 约 262 行，三个 `partial` 合计约 2359 行。
- `SupplyPartyBehavior.cs:842-901` 的 `ExtractAidEscortRoster/TransferTroopsLimited` 直接执行 `MemberRoster.AddToCounts`/`TroopRoster.AddToCounts`；`1040-1090` 的 `ExtractEscortRoster` 也直接修改驻军和护卫名册。
- `TryDispatchControlledAid`、`TryCreateSupplyParty`、`AdvanceAidMission` 等仍有跨职责的核心流程，虽确实已有 Dispatch/Cargo/Route/Recovery 四个服务。
- V2 S4 要求“最终 Behavior 只保留事件、SyncData 和独立 Service 委托；不能再保留几千行业务”。所以 S4 **实质改善，但严格验收仍不达标**。
- **建议**：不动粮食公式/随机/路线规则，先迁移两种护卫名册写入到有前后快照与补偿语义的 Cargo/Transfer Service；进一步把援粮/补给流程编排移入专用 Coordinator；同步删无调用的 partial helper 并做 T08–T12。

### P2：S3/S6 清理未完全闭合

- `AiForcePlanningBehavior.cs` 当前约 697 行，`CaptureTaskSaveFields`/`RestoreTaskSaveFields`、旧 `ApplyLivePlan`、`TryRoutineDonation`、`ExecuteWarRest` 等私有实现仍保留；活跃路径已委托新 Coordinator/Persistence/VisitService，但原 S3 明确要求删除重复私有 helper。应使用引用搜索+编译后删除，防止后续开发再次调用旧路径。
- `GarrisonRecruitFromPrisonersBehavior.cs` 当前约 716 行，`ExecuteUnifiedPlan` 仍实施多兵源的额度编排和费用提交；`SettlementManpowerProductionBehavior` 约 855 行且基本未改变。S6 虽抽取职业兵 Offer 和俘虏转化 Service，但尚不能称“Behavior 只做事件与持久化”的完整领域职责剥离。
- `ProfessionalRecruitmentService.TryCommit` 的顺序为先 `TryConsumeProfessionalOffer`，后写入驻军 roster，无本地补偿；这是现存异常原子性风险，建议围绕 offer 消耗成功、roster 写失败注入测试，谨慎修正，避免改变日常生成数值。

### P2（已承认未修复）：商队护卫刷新分支无效果

- `Mercenary/Services/MercenaryMarketService.cs` 的二次概率分支仅读取 `town.Culture.CaravanGuard`，无市场写入，Harmony Prefix 又返回 `false`，屏蔽原版。这一问题在 `baseline/Known_Issues.md` 中已如实列出，属于既有玩法缺陷，不是本轮新引入；应独立立项而非继续用架构改动夹带调整。

## 2. 明确完成的改进

- W1 旧增援兼容层与 SaveId 8/9 没有在本次 diff 中被重新编号。
- S1 引入 `TransferOperationJournalBehavior`（新增 `modifiedarmy_transfer_operation_journal_v1`）、结果的五态分流、雇佣兵异常补偿。
- S2 加入 `CampaignRecoveryCoordinator`，以及读档后对封邑索引和任务校验的排序。
- S3 新增 `PartyForceTaskCoordinator` / `PartyForceTaskPersistence` / `PartyVisitExecutionService`，活跃入口已有相当程度委托。
- S4 新增 `SupplyDispatchService` / `SupplyCargoService` / `SupplyRecoveryService` / `SupplyRouteController`，大量粮食/牲畜/销毁动作已外移。
- S5 封邑户数 Ledger、服役 Ledger、归还 Service 操作的是原数据字典和批次，不额外建立永久台账。
- S6 驻军职业兵和俘虏提交从 Behavior 抽出；S7 雇佣兵/工资 Patch 表面层已瘦身。
- Main 原新游戏奖励块、原 SaveDefiner 编号、保护文件在本次变更列表中未发生修改；新 Journal 是新增存档键，仍需读档确认。

## 3. S0–S8 验收概览

| 阶段 | 独立静态判定 | 缺口 |
|---|---|---|
| S0 | 完成基线与三张权威表 | 测试存档尚未建立 |
| S1 | 实质改进，未完成验收 | 事务通知顺序、故障注入、状态冻结 |
| S2 | 实质改进，未完成验收 | Suspended 恢复语义、A-B-A 实测 |
| S3 | 主链抽取完成，清理不完整 | 遗留重复 helper 与运行等价测试 |
| S4 | 服务抽取但边界不彻底 | 活跃兵员写入仍在 Behavior，T08–T12 |
| S5 | 静态结构基本符合 | T07/T19 实测 |
| S6 | 部分符合 | Behavior 剩余编排与异常补偿；T01/T15/T17 |
| S7 | 不通过 | **Army ↔ Politics 依赖环**、Known_Issues KI-01 |
| S8 | 未通过发布门槛 | 所有 T01–T24 均为 BLOCKED |

## 4. 修复次序 / 验证门槛

1. **S7-hotfix**：先消除加载依赖环，再编译两个项目、查看游戏模块加载顺序和 Harmony audit。
2. **S1/S2-hotfix**：恢复任务不能丢失 Suspended；把已提交资源与持久化防重记录的顺序收敛，配套异常注入与重载验证。
3. **S4 收尾**：迁出仍活跃的护卫提取/转移副作用，实测发队、回程、救援、敌情、72 小时、读档。
4. **S3/S6 清理**：移除死代码，明确每日驻军额度/职业兵/俘虏提交所有权，补齐异常回滚与对照测试。
5. **独立 S8 运行验收**：在有备份的旧/新存档上依次完成 T01–T24。每个 PASS 应有存档 ID、时间范围、资源前后快照、日志和执行版本；BLOCKED 不当 PASS。

## 5. 最终结论

本次源码重构相较 `a3d46c90` 有明显实质进展，不能归类为“仅移动文件”。但发现一项未被 Worker S7 报告识别的**模组加载依赖环**，加上冻结任务读档语义、雇佣兵提交记录时序、S4 不彻底等缺口，**当前不宜宣告架构整改全部完成或一般发布 GO**。仓库自身 `Release_Go_NoGo.md` 亦为 NO-GO，且全部 T01–T24 尚无场景性 PASS。

审查不修改 Git 代码；本报告为静态代码复审，不替代 Bannerlord 运行验证。

## 6. 实施记录（2026-10-28）

本报告提出的问题已开始按优先级落地，当前工作区完成如下：

- **P0 依赖环已解除**：移除 ModifiedArmy 对 ModifiedPolitics 的 csproj
  引用和 SubModule 依赖；`WarDispositionBridge` 改为受保护的运行时反射
  读取。保留原 `ModifiedPolitics -> ModifiedArmy` 方向。Politics 缺失或
  接口不兼容时回退正式战争判断。
- **两个项目构建通过**：ModifiedArmy 与 ModifiedPolitics Release 均为
  0 errors / 7 warnings。
- **Suspended 恢复语义已修复**：读档校验先查询 Journal 冻结域；冻结或
  已暂停任务保持 `Suspended`，不恢复路线、不会被每日计划替换，也不会
  因七日清理丢失。
- **雇佣兵防重顺序已修复**：资源提交成功后先记录 Journal，再触发可失败
  的招募事件和日志；通知异常只记录错误，不把已提交事务伪装成未执行。
- **征粮兵员写入已迁出 Behavior**：两类健康护卫提取和伤员保持的限量
  转移统一进入 `SupplyCargoService`；Behavior 内不再直接执行兵员
  `AddToCounts`。
- **S3 重复实现已清理**：Behavior 内旧的任务序列化/解析、路线应用、常规
  捐兵、战时返乡和和平复员 helper 已删除；对应职责只保留在
  Persistence、Coordinator 与 VisitExecutionService。
- **S6 每日执行已收口**：新增 `GarrisonRecruitmentExecutionService`，统一
  承担职业兵/俘虏共享额度、容量复核、费用与预算提交；Behavior 只调用
  执行结果并负责事件时序和日志。
- **职业兵提交补偿已补齐**：offer 消耗会记录准确的村庄户数扣减；驻军
  roster 写入异常时恢复实际新增人数、当日 offer 和对应村庄户数，避免
  “未获得士兵但兵源已消耗”。
- **商队护卫刷新已修复**：保留现有独立概率门禁，复用原版升级树权重与
  数量计算方法选出实际兵种，并写回城镇佣兵市场；反射不兼容时记录错误
  并安全跳过，不使 Harmony Prefix 抛出启动异常。
- **S8 静态闸门已自动化**：新增 `Test-ArchitectureReleaseGate.ps1`，每次
  运行同时构建 Army/Politics，并检查依赖方向、SaveId 8/9、保护文件、
  退役引用以及 Supply/AI/Garrison Behavior 的关键职责边界。该脚本明确
  不替代 T01-T24 游戏场景证据。
- **S8 运行证据闸门已建立**：新增 `Test-RuntimeAcceptanceEvidence.ps1`，
  校验 T01-T24 行完整性、状态和 PASS 证据字段；发布模式要求 T01-T20
  全部 PASS，T21-T24 必须 PASS 或留下明确豁免理由，避免短时冒烟被误记
  为完整验收。
- **S8 实机执行步骤已拆批**：新增 `S8-Runtime-Acceptance-Runbook.md`，
  将 24 项测试按启动/Patch、事务恢复、封邑兵源、征粮运输、生命周期与
  长期运行分为五批，并规定存档副本、资源快照、日志时间段和停止条件。
- **S8 收尾入口已统一**：新增 `Invoke-ReleaseValidation.ps1`，一次执行
  Army/Politics 构建、架构静态闸门和运行证据矩阵校验；发布参数在运行
  证据不足时返回专用 NO-GO 退出码，不再需要人工拼接多个检查命令。

仍需运行环境完成：有/无 ModifiedPolitics 两种启动、冻结任务读档、事件
异常注入、T08-T12、职业兵 offer 写入异常注入，以及 T01-T24 全矩阵。
因此这些代码修复不自动把发布结论提升为 GO。
