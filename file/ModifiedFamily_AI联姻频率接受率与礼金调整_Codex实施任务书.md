# ModifiedFamily AI 联姻频率、接受概率与礼金调整 — Codex 实施任务书

## 0. 任务范围与审查基线

- 仓库：`GuihuaZhou/bannerlord`
- 已审查基线：`main`，`8def87a0ee1493bff3b1446d2e651fcbcce13a74`。执行前重新检查 `git status --short`、当前分支和 HEAD，以最新源码为准；不得覆盖无关本地改动。
- 仅修改 `ModifiedFamily`，不创建 Commit、不 Push、不创建 PR。
- 保留玩家联姻 UI、双向入族、礼金协商、反提议、同文化**或**同王国的政治资格判定、存档兼容及原有婚约执行生命周期。

## 1. 本次确定的参数

| 项目 | 确定规则 |
|---|---|
| AI 主动求婚检查 | 每 **7 游戏日**每个家族检查一次；不同家族错峰 |
| 主动求婚基础概率 | **50%**，XML 可配置；每个家族每次有效检查仅抽一次 |
| 家族主动求婚冷却 | **14 游戏日**，从成功创建原始主动婚约起算 |
| NPC 接受概率 | `0.50 × Clamp(TotalScore / MaxPositiveScore, 0, 1)^2`，即二次衰减、最高 **50%** |
| 当前理论评分满分 | **300**；从评分常量推导，不应四处硬编码 |
| AI 礼金档次 | **30,000 / 50,000 / 80,000 / 100,000** 第纳尔，保留零礼金 |
| AI 主动礼金支付约束 | 不超过支付方领袖现有金币 **30%**；且仍需满足真实资金校验 |
| 玩家 | 主动求婚不受 AI 周期/50%抽签/主动冷却限制；NPC 对玩家主动提议仍依据新评分概率决定 |
| 现有人物配对冷却 | 保留 **60 天**，与家族主动冷却分别处理 |

上述参数属于这次已确定版本；不要恢复先前的 20k/50k/100k/200k 礼金，也不要把主动概率改成 5%。

## 2. 已确认源码及修改切入点

### 2.1 `ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs`

现状：`DailyTickClanEvent` 调用 `OnDailyClan(Clan clan)`，在 AI 安全条件下直接 `_marriageAi.ConsiderClan(clan)`；`DailyTickEvent` 的 `OnDaily()` 处理婚约回复、执行重试、到期和既有冷却清理。

要求：

1. **保留每日事件注册**，仅在 `OnDailyClan()` 入口加入 7 日调度过滤；不得把 `OnDaily()` 改为周 Tick。
2. `today = (int)Math.Floor(CampaignTime.Now.ToDays)`；对 `Clan.StringId` 做稳定散列得到 `phase ∈ [0,6]`，只在 `(today + phase) % 7 == 0` 时进入主动求婚入口。
3. 不使用进程不稳定的 `string.GetHashCode()`；使用可跨启动稳定的简单散列，如固定 FNV-1a。
4. 同一游戏日不因载入、重复事件等而额外发起完整搜索；检查重复事件订阅的安全性。
5. 历史日期不补跑，旧存档首次载入也应在未来最多 6 个游戏日进入正常检查时序，不要让全部 Clan 同一天集中求婚。

### 2.2 `ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs`

现状：`ConsiderClan()` 遍历候选、双入族方向及礼金，并按 `Tier`、`ProposerScore`、`ReceiverScore` 排序；当前私有 `GiftSteps = {5000,10000,20000}`；`Respond()` 在 `score.Total >= AcceptanceThreshold(0)` 时直接接受；`TryCounter()` 复用 `GiftSteps`。

建议新的有效检查顺序：

1. 排除玩家 Clan、不符合合法资格的 Clan、已存在主动活跃婚约的 Clan；
2. 检查 14 天家族主动求婚冷却；
3. 使用 XML 配置的 `BaseProposalChance` **只抽一次**（不能对每个 Hero/Clan/礼金组合抽取）；
4. 抽中后才开始原有候选搜索；
5. 候选生成、预览评分、排序和 `TryCreateMarriageProposal()` 保留现有业务校验；
6. 仅在创建原始婚约成功后启动 14 天冷却。

调整 `GiftSteps` 的唯一来源为配置/不可变设置中的 `30000,50000,80000,100000`，`ConsiderClan()` 与 `TryCounter()` 共用；继续提供 `gift=0` 无礼金方案；玩家 UI 自由输入金额不受这些档次限制。

付款方领袖的 `Gold` 必须同时满足 `>= gift` 和 `gift <= floor(Gold * 0.30)` 才能作为 **AI 自动出价** 候选。不要给玩家手动出价强加此比例限制。历史婚约执行仍按实际支付承诺结算，不得事后用 30% 的新约束使原来已合法接受的提议失效。

**候选筛选与排序**：

- 预估 NPC 接收方接受概率；概率为 0 的普通主动提议不要作为优先有效候选。
- 仍先遵循既有 `ClanTier` 政治优先级，但同层方案考虑发起方利益、接收方接受概率、礼金成本，而非盲目选昂贵礼金或只有自己有利的方案。
- 玩家接收方的婚约不要用 NPC 的概率筛选掉，因为玩家保留最终接受/拒绝权。
- 每家族每次有效检查最多创建一份原始主动提议。

**NPC 回复流程**：

1. 首先调用 `_proposalModel.CanRespond()`；非法提议沿用现有失效处理。
2. `_values.Evaluate(proposal, proposal.ReceiverClan)`，计算 `p=0.5×Clamp(score/MaxPositiveScore,0,1)^2`。
3. `p=0` 且 `CounterCount==0` 时，可按原有 `TryCounter()` 流程寻求新条款；无法反提议则拒绝。
4. `p>0` 时执行 **一次**随机抽签：成功调用 `_service.TryAccept()`；失败调用 `_service.TryReject()`，**随机失败不得再反提议或重复抽签**。
5. 只有真实服务方法调用成功才能记录 `Accepted` / `Rejected`，失败按返回状态处理。
6. 保留 `ReceiverClan == Clan.PlayerClan` 的早退，玩家收到的婚约仍由玩家决定；AI 对玩家发起的婚约按此概率回复。
7. 反提议属于同一 `ChainId`，不得算作新的主动求婚机会或刷新家族冷却；反提议不应形成反复抽签的漏洞。
8. 不改变 `OnDaily()` 原有 7 天回复期限、30 天执行期限及执行重试周期。

### 2.3 `ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs`

现有公式：

`Total = InflowValue - OutflowCost - SuccessionPenalty + AllianceBenefit + RelationValue + CultureValue + KingdomValue + GiftValue`。

当前正向上限：人物流入 75 + 联姻 55 + 关系 30 + 同文化 20 + 同王国 20 + 收礼金 100 = **300**。

`ValueToClan()` 实际形式 `75 - |Age - 28|×1.25`，虽然有 `Clamp(...,20,160)`，当前真实最大值是 **75**，不是 160。

`GiftValue()` 当前为 `Min(100, 35×ln(1+gift/10000))`，付款方还受到当前余额压力系数的负向影响。保持公式不变；仅更新 AI 礼金档次，确保收钱加分、付钱扣分。30k/50k/80k/100k 对收礼方的理论分值分别约 `48.5/62.7/76.9/83.9`。

### 2.4 `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageScoreTuning.cs`

把 `MaxPositiveScore` 由已有正向构成项统一推导，避免多个独立 300。增加明确的接受率模型：

```csharp
public static float CalculateAcceptanceChance(float score)
{
    if (float.IsNaN(score) || float.IsInfinity(score)) return 0f;
    float ratio = Math.Max(0f, Math.Min(1f, score / MaxPositiveScore));
    return 0.50f * ratio * ratio;
}
```

其中 `0.50f` 和平方指数 `2.0f` 建议统一作为配置项（见下文），不能出现 XML 值与编译常量两套权威来源。百分比语义为实际概率（范围 0～1）。

边界用例：score ≤ 0 → 0%；50 → 约 1.39%；100 → 约 5.56%；150 → 12.5%；170 → 约 16.06%；200 → 约 22.22%；300 及以上 → 50%。

注意旧 `AcceptanceThreshold`、`CandidateReceiverFloor`、`CounterOtherClanFloor`、`CommitmentThreshold` 在候选、反提议、政治重估处的各自用途。不能为了新概率算法粗暴删除并改变已接受婚约的政治重估语义；逐一核对调用点并解释如何处理。

### 2.5 `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs`

已保存 `_proposals` 包含 `ProposerClan`、`ParentProposalId`、`ChainId`、`CreatedAt`、`Status` 等。新增只读查询方法：按 Clan 查询最近一次 **原始主动婚姻提议**，条件为 `ProposalType == Marriage && ProposerClan == clan && ParentProposalId == null`。通过其创建时间推导 14 日冷却，`today - createdDay < 14` 禁止主动求婚。

以下状态不提前解除冷却：已接受、已拒绝、失效、超时、撤回；成功创建才触发，创建失败不触发。`TryCounter()` 产生的记录不得作为新求婚刷新冷却；其他 Clan 对本 Clan 求婚时，本 Clan只是接收方，也不触发主动冷却。

可优先复用现有存档历史数据，**不新增 Saveable 类和 SyncData 字段**。若历史数据存在不可信空值或时间问题，做合理防护；如实践中证实历史法不可行，先在变更说明中解释，再采用追加式兼容存档状态。保留原有 `Hero` 锁、配对冷却 60 天、`ReleaseChain()`、状态机及金币结算边界。

### 2.6 `ModifiedFamily/ModifiedFamily/Marriage/Execution/MarriageExecutionService.cs`

无需为了调概率而修改婚姻执行事务。继续走原版 `MarriageAction.Apply` 和现有配偶/归属 Clan 核对、金币转账与实际余额差异验证。不得绕过真实扣款、重复执行，也不得因新的 AI 出价上限取消已接受且支付能力仍满足原有婚约条款的记录。

## 3. XML 与运行期配置

新增 `ModifiedFamily/ModifiedFamily/ModuleData/MarriageAIConfig.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<MarriageAI>
  <ProposalCheckIntervalDays>7</ProposalCheckIntervalDays>
  <BaseProposalChance>0.50</BaseProposalChance>
  <ClanProposalCooldownDays>14</ClanProposalCooldownDays>
  <AcceptanceMaxChance>0.50</AcceptanceMaxChance>
  <AcceptanceExponent>2.0</AcceptanceExponent>
  <GiftSteps>30000,50000,80000,100000</GiftSteps>
  <AiMaxGiftGoldRatio>0.30</AiMaxGiftGoldRatio>
</MarriageAI>
```

建立模块内唯一配置读取入口，启动或战役载入时读取一次；使用 `InvariantCulture`；允许缺失和非法输入回退默认值并仅告警一次；概率和比例校验 `[0,1]`，指数要求为正数，天数为正数，礼金档次为去重、递增、非负且不溢出的整数。禁止每日 Tick 重读磁盘。理论评分满分应由评分模型推导，不通过 XML 再维护第二份。

确认 `ModifiedFamily.csproj` 的 `None Update="ModuleData\**\*"` 是否能复制新文件，并核对实际部署路径；不得硬编码个人本机安装路径。

## 4. 玩家交互边界

- 玩家主动提交 `MarriageNavigationVM` 不受 AI 周检、50%主动求婚抽签、AI 主动冷却及 AI 30%自动支付约束；仍受现有合法人物、政治条件、金额真实性约束。
- NPC 接收玩家提议时使用新的二次衰减接受概率；玩家作为接收方则人工决定。
- 玩家 UI 手动输入金额上限及有效性保持原逻辑；若因高礼金想提高按钮步进，可单独评估，但不要把 AI `GiftSteps` 强制应用到 UI。
- 继续保留允许同文化或同王国、两种婚后入族方向，以及原有收养逻辑。

## 5. 日志建议

复用现有 `MarriageDiagnosticTrace`（只在启用诊断时输出 Debug）；新增 `AI_PROPOSAL_CHECK`、`AI_MARRIAGE_ACCEPTANCE` 记录：游戏日、家族名称/ID、调度相位、上次原始提议日、冷却剩余、XML 配置概率、实际随机值、是否进入搜索、婚约 ID/ChainId、礼金金额、付款方金币、评分、满分、接受概率、是否反提议和最后服务调用结果。每次业务决策只取一次随机值，日志复用同一值；不得让日志本身触发行为。每周实际检查输出完整记录，普通每日跳过不刷屏。

## 6. 验收测试

1. **调度**：同一 Clan 7 天仅一次有效检查；不同 Clan 具有稳定错峰；旧存档重载不集中爆发、不会重复检查/补历史检查。
2. **主动概率**：XML=0 不进入搜索，=1 每次合格检查进入搜索，=0.5 大样本抽签比例接近50%；只有家族层抽签一次，不能按 Hero/礼金重复抽签。
3. **14天冷却**：成功发起后第13天不可主动新提议，第14天冷却结束但仍须等待周检查；被拒/失效/超时也不提前解除；无有效候选或服务失败不触发；反提议不刷新；玩家手动不受限。
4. **接受概率**：score 0→0%，150→12.5%，300→50%；`NaN/Infinity` 防护；单次真实决策只抽一次；随机拒绝不能通过反提议重抽；NPC 接收玩家提议采用该概率，玩家接收则人工决定。
5. **礼金**：只出现 0/30k/50k/80k/100k 自动提议档次；主动提议和反提议共用；AI 自动付款比例 ≤ 30%；没有足够金币的报价被过滤；礼金对收礼方加分、付款方减分；玩家自定义金额不受档次和 30% 约束。
6. **状态机**：Pending→Accepted/Rejected/Counter；Accepted→Completed；过期、政治变化、死亡、资金不足、暂缓执行与失败均保留原语义。重新加载存档后没有重复扣钱或重婚。
7. **构建及兼容**：检查最新 `AGENTS.md`、执行 Release 编译、验证 XML 运行部署，检查 `git diff`；新/旧存档实测如不能完成，明确报告 `BLOCKED`，不能以编译通过代替实机成功。

## 7. Codex 完成交付

汇报 HEAD、实际修改的文件及方法、XML 路径和最终配置值、实际理论满分推导、如何保证抽签和冷却不会重复触发、旧存档兼容性、构建与测试 PASS/FAIL/BLOCKED、剩余风险。不要修改其他 Mod，不要 Commit、Push 或创建 PR。
