# ModifiedArmy 日志统一改造：Worker 实施步骤与验收

> 目标：完善文案、等级、本地化，并修复由日志**已经证实**的独立业务缺陷；其余可疑点先强化诊断。

## W1 汉化与话术修正（独立小提交）

文件：
- `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml`
- `ModifiedArmy/ModifiedArmy/ModuleData/submodule_strings.xml`
- 产生这些消息的 `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`、`Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`（若占位符 API 必须统一）

具体操作：

1. 修复六组已经列出的占位符差异，所有文字改为中文自然句子，而不是键值对。
2. 将 `ModifiedArmy_ManpowerConsume` 里的 `SETTLEMENT_ID/TROOP_ID` 改成 `SETTLEMENT_NAME/TROOP_NAME`；其他四组将 `SETTLEMENT_ID` 改 `SETTLEMENT_NAME`。
3. 对 `ModifiedArmy_ManpowerProduce` 保留 `MANIFEST`，显示兵种和人数；无兵源用中文说明。运输事件的 `STATE/EVENT_AMOUNT` 采取同步回补占位符或明确废弃并调整模板，不能出现盲目缺参。
4. 增加自动检查：从基础XML每项的 `{=xxx}` 映射到对应CN `id=xxx`；提取所有 `{UPPERCASE_WORD}`；断言所有实际使用的参数在本地化模板中存在。对于有意省略的仅列白名单并说明原因；不要对 `{=LOCALIZATION_KEY}` 当作参数比较。
5. 可本地化文案一律 `GameTexts.FindText`，动态名称使用 `TextObject`；不引入直接中文插值语句。
6. XML 加载及中文显示回归测试，包括中英文语言切换。

推荐文本：
- `[职业兵训练] 胡比亚今天预计训练4名职业兵。由于驻军没有招募空间，暂时无人入伍。`
- `[职业兵训练] 俄尼拉今天准备了4名新兵，其中2名通过审核并加入驻军。`
- `[部队调兵] 泰伊斯准备把13名士兵留在胡比亚，但驻军归属校验未通过，因此没有移交。`

## W2 驻军归属校验修复（独立 P0 Commit，不能只改日志）

文件：
- `ForceManagement/Models/PartyExchangeModel.cs`
- `ForceManagement/Services/PartyExchangeService.cs`
- 新建只读共用 `PartyTransferOwnershipPolicy.cs`（放置于项目既有合适目录）
- 如有其他 `ActualClan` 身份校验入口，请全局搜索并限定在驻军场景修复。

伪代码：

```csharp
// 仅说明逻辑，不是可直接套用的最终类型声明。
private static Clan EffectiveTransferClan(MobileParty party, Settlement settlement)
{
    if (party == null || settlement == null) return null;
    MobileParty realGarrison = settlement.Town?.GarrisonParty;
    if (party == realGarrison && party.IsGarrison)
        return settlement.OwnerClan;
    return party.ActualClan;
}
```

以下所有安全条件保持不变：类型正确、双方活动、Party 实际到达、非战斗、非围城、真驻军、同 Clan、容量、军粮与驻军安全底线、名册、封邑兵保护、操作幂等、刚占领领地的原有限定例外。

- 普通 `GarrisonToField`：来源必须是真实驻军，双方有效 Clan 均应等于 Settlement.OwnerClan。
- 普通 `FieldToGarrison`：接收方必须是真实驻军，野战来源有效 Clan 必须等于 Settlement.OwnerClan。
- `NewlyCapturedSettlement`：保留原有单次例外，不得由此扩展为普通跨 Clan 提兵。
- 同一校验应在 `BuildRosterPlan` 规划与 `ValidateRosterBoundaries`/`ExecuteRosterTransfer` 执行入口共同使用。

不调用 `garrison.ActualClan = ...`；不能通过禁用所有权校验解决。经过任一拒绝，来源与目的名册总人数、伤兵数、封邑兵账本、生产产额必须保持不变。

## W3 日志等级与节流重构

文件示例：
- `Utils/ModLogger.cs`：原则上沿用 API；只有确实需要日志节流帮助方法才增量封装。
- `ForceManagement/Services/PartyExchangeService.cs`：将失败合并输出为一次准确原因，正常失败 Debug、真正异常 Warn。
- `ForceManagement/Behaviors/AiForcePlanningBehavior.cs`：计划恢复/建立/正常维持现状分别 Info/Debug。
- `ForceManagement/Services/ForceEntryPatchAudit.cs`：启动概要 FileInfo，技术补丁清单 Debug，真实冲突 Warn。
- `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`：成功补员从 FileNotice 改 FileInfo，修改不正确的注释。
- `Fief/Models/PartyData/FiefPartyData.cs`、`Fief/Models/PartyData/FiefPartyReinforcement.cs`：静态上限只变更记录，升级只记录有升级的结果。
- `Garrison/Behaviors/GarrisonWageLimitBehavior.cs`：记录明显额度变化并仅在越过风险阈值时 Warn。
- `Garrison/Supply/*`、`PartyFinance/*`、`Mercenary/*`、`Transport/*`：分别聚合粮食、工资、市场、路线信息，转成自然口语化。

节流只控制日志输出，不影响被拒绝的任务状态、重试、生产或运输行为。事件键建议 `游戏日+Party.StringId+Settlement.StringId+操作类型+实际原因`；同一个键 Notice/Warn 仅首次输出，其余 Debug 记录或者当天总结“同类情况发生N次”。要确保多个 Party 不相互遮蔽。

## W4 准确诊断与“0批准”解释

现有运行版 `ExchangeDiag` 显示出了根因，但内容使用英文字段串且 Info 等级偏高。保留同样诊断信号，改为简明中文原因句子，额外的信息在 Debug 内一条完整句子描述。例：

`[统一调兵] 胡比亚的驻军未记录有效的家族归属，伊勒塔斯的63人调兵计划暂时无法执行。`。

规划与执行都应具有统一的实际失败类型，并以同一个 `OperationId` 关联；请求人数、批准人数、实调人数、容量变化和错误阶段要明确。正常没有编制和安全余量为 Debug；不允许把 `InvalidOrUnsafeTransfer` 和 `UnsafeAtExecution` 都变成同一句“当前不允许调兵”。

对于驻军职业兵候选已生产而批准0的情况，在既有评估结果后记录：
- 有效驻军人数、编制上限、实际预留征粮队人数、软招募剩余容量、硬编制剩余容量；
- 当日生成候选、已有兵种方案的审批数量、批准数、实际数；
- 以模型返回的 `PrimaryLimit` 聚合原因：人数、军饷、入伍费用、维持资金、兵种资格；
- 若可转出但最终没有调兵，额外注明具体阶段及权限原因。

日志不得重新运行具有副作用的 `BuildPlan` 或 `TryConsume...`。

## W5 集成验收

| 测试 | 步骤 | 预期 |
|---|---|---|
| T01 本地化 | 中文运行并训练职业兵、消费候选 | 定居点和兵种显示中文名，不再有“定居点=”空字段 |
| T02 语言切换 | 英文及中文切换 | 文案语言一致，所有数值和名称正常，未解析出 `{PLACEHOLDER}` |
| T03 双向调兵 | 在胡比亚复测泰伊斯送13、伊勒塔斯取63；使用合理实时变动人数 | 正常同 Clan 调兵不再仅因驻军 `ActualClan==null` 被拒绝 |
| T04 非法跨 Clan | 两个不同 Clan，访问非自己封邑 | 除原有占领例外外拒绝，不能越权调兵 |
| T05 假驻军/位置 | 非真实驻军冒充来源或部队未抵达 | 必须拒绝，日志能指出原因 |
| T06 围城和战斗 | 两边转移时进入围城或战斗 | 必须拒绝，不改变兵员 |
| T07 兵员守恒 | 完成一次调兵 | 来源减少数=接收增加数，伤兵和保护兵台账保持正确 |
| T08 驻军满编 | 胡比亚当日生产4但软容量为0 | 不产生错误警告，Debug 说明满编或预留容量原因 |
| T09 审批失败 | 非满编但费用不足 | 说明实际审批瓶颈，不误写“编制已满” |
| T10 日志级别 | 默认 MCM 阈值，模拟正常日常 | 不出现大量 Notice；正常日志进入 Info/Debug |
| T11 日志节流 | 一日多次同一失败 | 通知只一次，排障追踪仍可用；不改变任务重试行为 |
| T12 读档任务 | 旧任务恢复/安全取消 | 一次汇总，原因可定位，无额外调兵 |
| T13 周期日志 | 133座封邑周更新 | 不再有144条无变化的编制上限和168条升级概率刷屏 |
| T14 故障告警 | 人为制造不守恒、缺权限等 | 显示 Warn/Error，含具体 Party 和定居点，可定位操作 |
| T15 Debug 观察 | 临时把文件日志阈值设为 Debug | 中文调试细节完整，取消 Debug 后不影响游戏运行 |
| T16 业务回归 | 复用同一个存档分别跑日志改造前后 | 若无单独 P0 所有权修复，兵员/财政应无变化；有P0修复仅改变合法调兵结果 |

## 验收报告须包含

1. 所有改动的源码 diff、实际编译使用的 commit 和 DLL 版本。
2. 中文及英文日志示例，至少覆盖19个类别的输出或明确说明未触发。
3. 本地化占位符静态检查结果，以及 XML 读取结果。
4. 实际胡比亚双向调兵测试、拒绝与成功的守恒记录。
5. 与本次612条日志样本相比的等级分布、类别数量和 Notice 降噪幅度。
6. 如被阻断的驻军尚有其他原因，必须出具新的真实日志，不能仅靠猜测改归属安全边界。
