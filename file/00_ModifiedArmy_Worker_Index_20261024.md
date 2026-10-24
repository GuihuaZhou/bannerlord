# ModifiedArmy｜本轮变更总索引与 Worker 分工（2026-10-24）

> 状态：**待实施设计与执行指令**；不表示 GitHub 已修改、代码已编译或游戏验收已完成。  
> 仓库：`GuihuaZhou/bannerlord`；检查基线 `main@ccd222bda72ed7587300cc4360c7593c7aa6c52e`（2026-10-24）。  
> 游戏版本参考：近期崩溃报告 `v1.4.7.117484`，Worker 必须以实际目标游戏程序集核对私有方法签名。  
> 文档用途：每份可直接交给不同 Worker 独立实施；本索引用于集成管理。

## 一、范围及确定决策

此次只覆盖最近已讨论的四个主题，另加一个 **可选** 的低风险审计清理；**不重做此前已经落地的 AI 兵力管理、封邑批次、职业兵生产、调拨体系或复员系统**。

| 文档 | Worker | 任务 | 范围与依赖 | 优先级 |
|---|---|---|---|---|
| `01_ModifiedArmy_War_Disposition_Target_Model.md` | W1 | 战争倾向动态阈值、30%—50%—61%—95%兵力目标曲线 | 独立 | P0 |
| `02_ModifiedArmy_Zero_Approval_Diagnostics.md` | W2 | 调兵“请求N/批准0”全链路、无副作用诊断；**先查证，不修安全判断** | 独立，可与W1并行 | P0 |
| `03_ModifiedArmy_AI_Bandit_Eligibility_Model.md` | W3 | 统一AI招募模型拒绝野怪；共用资格Policy | 独立，为W4提供Policy | P1 |
| `04_ModifiedArmy_Rescued_Prisoner_Bandit_Filter.md` | W4 | 战后解救野怪只排除**此次新增**名额 | **依赖W3的Policy**；可先开发后合并 | P1 |
| `05_ModifiedArmy_Harmony_Audit_Cleanup_Optional.md` | W5 | 解释重复Harmony记录、移除空Postfix、审计打印具体patch方法 | 独立；**可选，不属于功能Bug修复** | P2 |

## 二、不可误解的关键决定

1. **战争最低目标是30%，不是0%。** 当 `D <= -80`，战时常规兵力目标维持30%；不代表解散部队，也不代表取消自有封邑紧急防御。和平时仍按原有35%及最低50人。
2. 使用现有 `AggressiveWarDispositionThreshold = T` 配置（XML当前值`-50`）；阈值以下目标比例**最大50%**；在 `[T-10,T)` 保持50%平台；**达到** `T` 进入61%；到`D>=80`为95%。
3. Army原版调用条件是实际 `PartySizeRatio > 0.6`，**达到目标61%≠保证进入Army**；无计划改写原版召集入口或禁令。
4. 大量“批准0人”：只添加结构化日志，覆盖**规划选择 → 到达 → Model审批 → Service执行**。之前怀疑驻军`ActualClan`与`Settlement.OwnerClan`不一致，**尚未实机确认，不允许作为既定根因直接修复**。
5. “外部增援请求0人”若驻军已覆盖其最低防御人数，是另一种正常需求评估，不等于“请求53、审批0”。禁止混为一个故障或因此改动防御下限。
6. AI招募野怪定义以 `Occupation.Bandit || troop.Culture?.IsBandit == true` 为基础；**不按本地化兵种名称判断**；仅针对正规AI Party / AI管理驻军自动招募，不影响玩家主Party手动操作。
7. 战后“解救战败方此前囚禁的俘虏”与“俘获战败方参战士兵”是不同路径。只阻止前者中的野怪**直接编入AI的MemberRoster**，不阻止后者进入`PrisonRoster`，不删除已有士兵。
8. 两条野怪过滤使用**同一资格Policy**；W4必须等待W3接口落地再合并，避免重复定义。
9. Harmony“两次同owner、400”由Prefix和空Postfix造成，**不是重复执行证据**；W5仅做可选审计消歧，不以其为“批准0”根因。

## 三、可并行开发的工作流与交付

```text
main@ccd222b
├── W1：战争目标模型       ────────┐
├── W2：批准0诊断日志      ────────┤
├── W3：野怪统一准入Policy ──┐      ├── 集成构建／存档／30游戏日观察
├── W4：战后获救野怪过滤  ──┴─────┤
└── W5：Harmony审计清理（可选） ────┘
```

每位Worker单独分支/PR；**禁止直接混入不在自己任务的算法重构**。PR要附：修改文件清单、关键diff、日志样例、编译/测试命令及结果、未通过项、回滚办法、是否改变存档结构。

### 合并顺序建议

1. W2日志先合并到可复现分支，收集“请求N→批准0”的真实失败布尔门槛；**基于证据另开后续修复PR**。
2. W1可独立合并，并用目标比例单元测试验证；不必等待W2。
3. W3之后合并W4（共享资格Policy）。
4. W5可最后做；不阻塞功能验收。

### 冲突热点与分工边界

- W1 **独占** `ForceManagement/Models/ForceManagementModels.cs` 的 `PartyForceTargetModel` 与配置解析；W2**只增加诊断**，不要在同一模型中改变兵力比例。
- W2可修改 `PartyExchangeModel.cs`、`PartyExchangeService.cs`、`PartyForceCandidateService.cs`、`UnifiedRecruitmentExecutionService.cs` 但**不得修改条件的真假、批准数量和执行顺序**。
- W3只动 `Recruitment/Models` 下Policy、`DefaultAIRecruitmentModel`、拒绝原因和资源文本；W4只动战后Patch并调用W3公用Policy。
- W5只动 `ForceEntryPatchAudit.cs` 及 `GarrisonTroopDonationPatch.cs` 的**空Postfix**；不能删除同文件其他合法Postfix。

## 四、全局统一验收门槛

- Git仓库目标 `main` 的最新版本、编译目标和各 Harmony 签名已核验；使用固定基线对比diff。
- 构建成功；`Harmony.PatchAll` 在实际版本加载成功且无异常（若无法实机测试明确标注）。
- 已存在的 **D1-D4复员、封邑批次、玩家手动操作、驻军安全下限、征粮队回归预留、军饷/Clan预算** 不发生意外变化。
- 计划与执行名册变更守恒；只日志任务不能产生额外副作用；战后过滤只扣本次获救野怪。
- 加载旧存档、保存重新加载、至少30游戏日运行记录中无重复交易／空引用／日志刷屏；100天压力测试宜在独立验收阶段做。
- 如果原版程序集私有方法签名或其他Mod改变了调用路径，必须在PR写清“证据—兼容方案—未覆盖范围”，不能直接宣称已拦截全部途径。

## 五、仓库定位（基线固定链接）

- [战争目标模型](https://github.com/GuihuaZhou/bannerlord/blob/ccd222bda72ed7587300cc4360c7593c7aa6c52e/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs)
- [配置模型与解析器](https://github.com/GuihuaZhou/bannerlord/blob/ccd222bda72ed7587300cc4360c7593c7aa6c52e/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [调兵Model](https://github.com/GuihuaZhou/bannerlord/blob/ccd222bda72ed7587300cc4360c7593c7aa6c52e/ModifiedArmy/ModifiedArmy/ForceManagement/Models/PartyExchangeModel.cs)
- [调兵执行Service](https://github.com/GuihuaZhou/bannerlord/blob/ccd222bda72ed7587300cc4360c7593c7aa6c52e/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyExchangeService.cs)
- [AI招募模型](https://github.com/GuihuaZhou/bannerlord/blob/ccd222bda72ed7587300cc4360c7593c7aa6c52e/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs)
- [原版解救路径](https://github.com/GuihuaZhou/bannerlord/blob/ccd222bda72ed7587300cc4360c7593c7aa6c52e/code/MapEvent.cs)

## 六、明确范围外

本轮**不**启用WarRest、重写和平复员、不处理30/100日旧清单中已经完成的模板清理、不修“批准0”的疑似归属根因、不改征粮队驻军计数、不改原版Army召集模型、不把获救正规兵全部接入财务招募模型、不清理存档历史野怪、不重写战后分配算法。

**资料库依据：** `ModifiedArmy_AI_Bandit_Recruitment_and_Rescued_Prisoner_Filter_Implementation.md`（完整292行，性质为设计计划）以及本轮关于30%最低值、动态阈值、先加日志的后续确认；以本索引列明的新共识优先，其他旧规划不自动恢复为待办。
