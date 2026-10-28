# ModifiedFamily 联姻 AI 决策、拒绝／反提议与政治变化边界处理——统一实现方案 V2

> **用途：交付其他开发 Worker 直接实施的开发任务书。** 这是实现方案，不代表已修改 Git 仓库、编译或通过游戏内测试。  
> 仓库：[`GuihuaZhou/bannerlord`](https://github.com/GuihuaZhou/bannerlord)；分支：`main`；本次核对代码基线：`ab062534eca33029eab6636dc70678bf7093b8a1`（2026-10-24）。  
> 文档日期：2026-10-25；文档版本：V2（合并此前《联姻政治变化边界处理》与“AI 无拒绝”讨论）。  
> **范围：** `ModifiedFamily` 的联姻评分、自动提议、接受／拒绝／反提议、战争／换国对协商和待执行协议的影响、状态记录、日志和验收。**不改收养逻辑、不重做三级联姻 UI、不提交 PR。**  
> **重要：** 本文中的拟新增类、接口、阈值和评分权重属于建议实施方案，不是仓库已存在的 API。所有示例代码是结构性参考，需按项目编译程序集和存档契约实现。

---

## 0. 一页执行摘要：必须修复什么

本轮需要同时解决**两类相互依赖的问题**，不允许只实现其中一个：

1. **AI 联姻几乎全部被接受**：`ClanHeroValueModel.MarriageScore()` 在任意方向都无条件加上对方 Hero 的成员价值；失去己方 Hero 的 Clan 实际未得到对方 Hero，也被错误计算为获益。现有 AI 接收方 `Respond()` 只有接受或拒绝两个分支，**没有调用已经存在的 `TryCounter()`**；AI 发起方也只取年龄最大的己方 Hero、仅发零礼金的正常嫁娶。应修正成员流入/流出、家族成年成员稀缺、女性外嫁与男性入赘差异、双方 Clan Relation、外交和礼金评分；建立“接受／唯一一次反提议／明确拒绝”完整决策；优化搜索。
2. **政治变化边界处理**：双方 Clan 所属王国宣战，应使未完成婚约**立即失效、无 60 天冷却、议和不恢复**；换国后按实际是否同国、同文化、交战及 Clan 身份重新验证。仍合法的协商期继续但重评分；已同意待执行的仅在**实质政治变化**时重评双方履约意愿，**绝不自动改婚姻方向或礼金**。临时被俘／战斗／军团不是永久失效；已执行婚姻不回滚。

**完成标志不是“日志开始出现拒绝”本身。** 必须用可控用例证明：一位对外嫁/入赘成本不堪承受的 Clan 会拒绝或合理反提议；对自己有利的协议会接受；因战争而作废不被写为“拒绝”；能达成的反提议最多发生一次；长期自然模拟决策分布可解释。

### 0.1 先前政治文档遗漏核查

旧文件 `/ModifiedFamily_联姻政治变化边界处理_实现方案_20261025.md` 共 264 行，只在 §3、§4.6、§6 简要要求评分修正和主客观拒绝分流，**没有**定义成人口数、外嫁/入赘成本模型、Relation 参数、候选搜索、AI 反提议算法及专项验收。**本文为统一完整替代文档**，后续 Worker 应使用本文，不要单独实施旧文造成不完整交付。

---

## 1. 已核对的代码、运行日志与问题归因

### 1.1 当前仓库直接证据

| 仓库文件 | 当前已确认实现 | 缺陷与工作要求 |
|---|---|---|
| `ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs` | `ValueToClan()` 基于年龄和家族等级；`MarriageScore()` 无条件 `+ ValueToClan(otherHero)`，未按婚后归属判断对方是否真正进入本 Clan；Relation 系数 `0.5f` | **P0** 修改流入/流出计算，添加家族成年人数／嫁娶方式／继承风险和政治、礼金分项日志 |
| `ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs` | `ConsiderClan()` 只选 `own[0]`（按年龄倒序），固定 `FemaleJoinsMaleClan`、`GiftAmount=0`；`Respond()` 仅 `score >= 0` 接受，否则 `TryReject(..., InvalidProposal)` | **P0** 全候选搜索、双方向方案、接收方三分支、客观失效不冷却、业务拒绝原因 |
| `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriagePolicyModel.cs` | `AllowSameKingdomDifferentCulture=true`、`EnableAiToPlayer=true`、`MaxNewProposalsPerClanTick=1` | **P1** 在此或独立 `MarriageScoreTuning` 集中管理纯数值参数；**不要引用旧政治文档的 `EnableAiToPlayer=false` 断言** |
| `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageProposalModel.cs` | 先校验 Hero/Clan、血亲、文化、战争、领袖不得离 Clan、礼金结构；`CanRespond()` 和 `CanExecuteMarriage()` 基于统一资格 | 保持唯一硬性资格标准；暴露只读共享规则供政治校验和候选模拟使用 |
| `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageEligibilityModel.cs` | 不合格 Clan 包括灭亡、小派系、雇佣兵、强盗、叛军、`Kingdom==null`；被俘、逃亡、战斗、军团为临时阻碍 | 保留；成年人数计数**不应**使用只能谈判且必须未婚的 `CanNegotiateHero()` |
| `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs` | 提议／接受／拒绝／反提议、Hero 锁、配对冷却、创建预检 `CanCreateMarriageProposal()` 已存在 | **P0** 不重复实现提议系统；改进状态迁移、主客观失败分类、反提议预检和完成日志 |
| `ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs` | 监听 DailyTick、BeforeHeroesMarried、OnHeroChangedClan；日结算截止时调用 AI `Respond()` 或拒绝；执行期每日重试 | **P0** 增加战争、换国、家族灭亡事件；执行前政治复核；超时与利益拒绝区分 |
| `ModifiedFamily/ModifiedFamily/Marriage/Execution/MarriageExecutionService.cs` | 任何不允许或临时阻碍都记“等待”；否则调用原版 `MarriageAction.Apply` 再处理礼金 | 永久非法不得等待 30 天；执行中不得任意失效或重试已完成 Action |
| `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposal.cs` | `[SaveableProperty]` 1–24 已使用；状态含 `PendingResponse/AcceptedPendingExecution/Executing/...` | 若加字段从 **25** 往后分配，不得重排；状态枚举追加值不得改变旧顺序 |
| `ModifiedFamily/ModifiedFamily/Persistence/FamilySaveDefiner.cs` | `SaveableTypeDefiner(2026102401)`，已有 Class/Enum 注册 | 新状态若为普通内存分项无需保存；持久新类型按稳定 ID 注册并防冲突 |
| `ModifiedFamily/ModifiedFamily/UI/...` | 已实现玩家收件箱、地图通知、按钮和提议 API 调用 | 保留既有 UI；本次只补必要的业务拒绝原因显示与通知，不重构布局 |

`ClanMarriageDecisionService` 当前关键片段（**已存在的问题代码，禁止沿用**）：

```csharp
// 错：失去己方成员时仍然得到对方成员的完整正面价值。
float score = ValueToClan(otherHero, evaluatingClan)
    - (destination == evaluatingClan ? 0f : ValueToClan(ownHero, evaluatingClan));

// 错：AI 自动回复只有两种选择，反提议 API 未被调用。
if (score >= 0f) _service.TryAccept(p.ProposalId, p.ReceiverClan);
else _service.TryReject(p.ProposalId, p.ReceiverClan, FamilyReasonCode.InvalidProposal);
```

### 1.2 原始日志基线，供回归对照

已读取当前上传的 `/mnt/data/ModifiedFamily.log`（**旧运行环境日志，不代表新代码修复后的结果**）：

| 统计口径 | 数量 |
|---|---:|
| 出现“提出联姻提议”的记录 | 76 |
| 出现“已接受联姻提议”的记录 | 77 |
| 拒绝联姻的记录 | 0 |
| 反提议的记录 | 0 |
| 带“当前内部评分为”的接收方重评记录 | 34 |
| 上述接收方重评最低／最高分 | **+22.10 / +193.22** |

77 次接受大于本段日志 76 次新请求，原因可能是日志开头已有未完请求，**不可用 77/76 当作本批接受率**；要用 `ProposalId` 关联同一条谈判链统计。

**归因顺序：** 修正无条件加分 → 为流失人口建立成本 → 接入反提议和合理拒绝 → 修正候选搜索 → 补齐政治事件与理由分类。不要用“随机拒绝 20%”掩盖模型缺陷。

---

## 2. 已确认且不得被 Worker 擅自改动的业务规则

1. Clan 自主婚姻，不使用全局婚姻撮合器；提议方和接收方各自独立按**自己 Clan 的收益**评分，不以求婚方分数决定接收方结果。
2. 合法范围优先级：**同国同文化 > 异国同文化 > 同国异文化（现有开关 `true`）**；异国异文化禁止。文化口径使用 **`Clan.Culture`**（当前代码已从 `Hero.Culture` 转移）。
3. 婚后女性加入男性 Clan 为“正常嫁娶”，男性加入女性 Clan 为“入赘”；Clan 领袖不能离开自己 Clan，硬性资格不允许通过礼金弥补。
4. Clan 的成年贵族 Hero 越多，对外流成员容忍越高；越少则成本越高。**相同其他条件下，女性外嫁成本始终低于男性入赘成本**。
5. 两个 Clan 的当前 Relation 是联姻政治收益的一部分：高关系加分、低关系扣分，但不抵消硬性限制；好关系也不能自动抵消唯一继承人的严重流失。
6. 原始请求七天回应；**同一条谈判链最多一次反提议**，反提议另有七天回应；最后一轮只能接受或拒绝。主动拒绝或正常谈判超时的同一对 Hero 双向冷却 60 天，客观非法取消无该冷却。
7. 礼金方向可以为无礼金、我方付、要求对方付；双方必须逐一同意最终条款。金钱于成功婚礼时结算，支付异常不可重试结婚 Action。
8. Hero 被俘／逃亡／参战／在军团可以继续协商；双方同意后等待执行最多 30 天。两国宣战、Clan 灭亡／身份不合法、异国异文化等是立即失效，不是等待。
9. 已经成婚的协议不因战争或换国被撤销；已签署但未完成的协议不得悄悄改变婚姻方向或礼金。
10. `EnableAiToPlayer=true` 是当前仓库状态。与玩家 Clan 有关的已接受协议若要因非硬性收益变化撤销，须尊重玩家已同意的决策并给出通知／处理入口，不得替玩家静默重新拒绝。

---

## 3. 核心实现 A：修正家族价值和成年人口损失

### 3.1 必须拆分的评分结构

从指定 `evaluatingClan` 的视角、在**结婚前**计算：

\[
S(C,P)=V_{in}(C,P)-C_{out}(C,P)+V_{ties}(C,P)+V_{politics}(C,P)+V_{gift}(C,P)
\]

- `V_in`：**只有** `DestinationClan == evaluatingClan`，才计入对方 Hero 真正加入当前 Clan 的正面成员价值。反之为 **0**。
- `C_out`：**只有** `DestinationClan != evaluatingClan`（且自己的 Hero 确实是离族人）时，才计入己方 Hero 损失。反之为 **0**。不能“先无条件加对方 Hero 价值再减自己 Hero”。
- `V_ties`：两个原 Clan 建立姻亲后的基础关系收益，男女双方都可能受益（**建议初始 55 分，仅待调参**）。应与日后婚后的 Relation 变化分开；不要直接在评分阶段修改 relation。
- `V_politics`：当前 Relation、同文化、同王国和特定政治上下文。
- `V_gift`：有方向性的礼金净收益，金额经过递减效用与财政压力折算。双方所用角色、礼金方向必须基于提议本身，不根据“谁发送请求”猜测谁付钱。

### 3.2 成年人口计算必须正确

**成年 Hero 数量 N**：`Clan.Heroes` 中存活、`Age >= 18`、属于该 Clan 的贵族 Hero，**包括**领袖、已婚贵族、正在战斗／被俘的贵族；**不包括**同伴、未成年人、死亡 Hero、非贵族。严禁只统计 `CanNegotiateHero()` 返回可议婚的 Hero——那会排除已婚人物，严重低估家族规模。

```csharp
// 示意结构：实际 Hero.IsLord、Hero.Clan 等按引用 DLL 核实。
private static int CountAdultNobleHeroes(Clan clan) =>
    clan?.Heroes.Count(h => h != null && !h.IsDead && h.IsLord
                           && h.Clan == clan && h.Age >= 18f) ?? 0;
```

为便于调参，初版采用：

\[
Scarcity(N)=1+\frac{4}{N+1}
\]

以及：

\[
C_{out}=HeroValue\times Scarcity(N)\times DirectionPenalty+SuccessionRisk
\]

- 女性离开本 Clan（正常外嫁）：`DirectionPenalty=1.0`。
- 男性离开本 Clan（入赘）：`DirectionPenalty=1.5`。
- 不应对“男性嫁入当前 Clan”惩罚当前 Clan：方向系数只施加给**实际失去 Hero 的 Clan**。
- `SuccessionRisk`：唯一可行继承人或导致家族成年成员极度短缺时额外加成本；必须基于真实家族情况和领袖不可迁出规则。建议先实现可解释的基础人口风险，例如“失去后 Clan 仅剩 1 个成人”额外 +80；更复杂的继承制度日后扩展。

**必须在测试中覆盖的基准表**（不含继承附加成本）：

| 成年贵族 N | 人口稀缺系数 | 女性外嫁成本倍数 | 男性入赘成本倍数 |
|---:|---:|---:|---:|
| 2 | 2.333 | 2.333 | 3.500 |
| 3 | 2.000 | 2.000 | 3.000 |
| 5 | 1.667 | 1.667 | 2.500 |
| 8 | 1.444 | 1.444 | 2.167 |
| 12 | 1.308 | 1.308 | 1.962 |

示例：Hero 原始价值 80 分、N=3 时，女性外嫁损失 160，男性入赘损失 240；N=12 分别约 105、157。**这些是初始设计值，非已经完成实机平衡的最终常数。**

### 3.3 Hero 价值与继承风险拆开，避免负值和无效补偿

`ValueToClan()` 现在为 `60 - Abs(Age-28) + clan.Tier*10 - leaderPenalty(10000)`。要：

1. 去掉“从收益模型用 -10000 惩罚领袖”的混乱耦合：**Clan 领袖离族属于资格禁止**，留给 `MarriageProposalModel`，不是用打分表示。
2. 对非领袖合格 Hero 的基本价值设有界非负区间（建议 `Clamp(..., 20, 160)`），避免高龄负数导致失去成员反而加分。
3. 武将能力／年龄／亲族职务若要扩展，先以独立分项加到 `HeroValue`；未确认的官职/继承属性不应猜测 API。
4. 避免 `clan.Tier` 同时大幅放大流入收益和损失、使评分结果随被评分方变化反常。可在评分明细中单独展示家族等级对政治收益的影响。

### 3.4 Clan Relation 的推荐折算

当前已有：`evaluatingClan.GetRelationWithClan(otherHero.Clan) * 0.5f`。建议统一改为：

\[
V_{relation}=Clamp(Relation\times0.3,-30,30)
\]

| 当前 Relation | 关系分 |
|---:|---:|
| +100 | +30 |
| +50 | +15 |
| 0 | 0 |
| -50 | -15 |
| -100 | -30 |

**必须**从 `evaluatingClan.GetRelationWithClan(otherClan)` 读取**实时的另一个原 Clan**，避免 Hero 婚后迁族改变读取对象。重评时不可从创建时的陈旧 Relation 快照打分；事后日志需记来源 Clan 和 Relation 原值。关系加分不能突破战争、文化、领袖等硬性禁止规则。

### 3.5 同王国／同文化与礼金

- 同文化、同王国：各自保留作为可解释分项的奖励，建议起始 `+20 / +20`（当前实现均为 +20）。实际候选**层级优先**独立于评分：同国同文化优先匹配，不得仅用高 Relation 使第三档优先于第一档。
- 联姻基础收益建议 `+55`，避免纠正流失公式后所有正常嫁娶都无法成交；这个数值须随模拟重新校准。
- 礼金按支付方向：对收款 Clan 加分，对付款 Clan 减分；建议提供 `GiftValueModel` 以对数或分段函数折算，金额大时边际收益降低，付款 Clan 的现金紧张成本可增加。举例而非固定实现：收款效用 `min(100, 35 * ln(1+Gift/10000))`；付款负效用按可支配资金放大。不允许负金额、金额溢出或以礼金越权执行硬非法婚姻。
- `GiftAmount=0` 时双方礼金得分均为 0；**付款方余额校验**在创建（可支配）、答复及最终执行适当阶段进行，遵循“不会默默缩减支付额”的既有规则。

### 3.6 建议新增只读评分明细（非存档类）

```csharp
internal sealed class MarriageScoreBreakdown
{
    public Clan EvaluatingClan { get; init; }
    public Hero OwnHero { get; init; }
    public Hero OtherHero { get; init; }
    public int AdultHeroCount { get; init; }
    public float BaseHeroValue { get; init; }
    public float InflowValue { get; init; }
    public float OutflowCost { get; init; }
    public float SuccessionPenalty { get; init; }
    public float AllianceBenefit { get; init; }
    public float RelationValue { get; init; }
    public float CultureValue { get; init; }
    public float KingdomValue { get; init; }
    public float GiftValue { get; init; }
    public float Total { get; init; }
}
```

`init` 只是示意；由于当前工程 `netstandard2.0`，实施时可用 `private set` / 构造函数，避免编译依赖新增运行时类型。不要使 UI 或 AI 各自再次实现公式：统一调用模型公开的 `Evaluate(proposal, clan)`。原 `MarriageScore(...)` 可以过渡保留并标记为弃用，避免其他调用方编译中断。

---

## 4. 核心实现 B：AI 从“全接受”改为有根据的三分支协商

### 4.1 发起方搜索流程（`ConsiderClan`）

必须替换“己方年龄最大的 own[0] + 默认女入男家 + 0 礼金”路径：

1. 过滤可参与 Clan 及可协商 Hero；检查自身、接收方锁定／冷却／排他性提议、政治硬约束。
2. 遍历（或有界预筛）己方多个合格 Hero × 多个目标 Clan × 其合格异性 Hero；分别计算两种 `MarriageDirection` 的合法性；**仅合法方向进入评分**。
3. 保持文化／王国层级顺序：1 同国同文化，2 异国同文化，3 同国异文化；对同一层级，按发起 Clan 收益、接收 Clan 大致可接受程度、礼金负担综合排序。
4. 优先尝试无礼金；若本方愿意为交易支付，可生成合理预算内（例如 5k、10k、20k，受余额与预算上限约束）的候选条款；无需让 AI 每日暴力枚举所有金额。
5. 创建之前调用现有 `FamilyProposalService.CanCreateMarriageProposal(draft)`，最终用 `TryCreateMarriageProposal(draft)`；返回 `HeroLocked/PairCooldown/InvalidProposal` 时记录并尝试下一候选，不得在日志中假称创建成功。
6. 每 Clan 每日创建量遵循 `MarriagePolicyModel.MaxNewProposalsPerClanTick`。搜索应可控：预筛合格文化档、候选 TopK、每 Clan 每日上限，避免日结算所有 Clan×Hero 的高阶全组合。
7. 发起方预测接收方收益可以帮助减少无意义请求，但**不可强迫对方接受**；接收方始终以回应时最新状态独立评分。无需人为确保随机拒绝率。为了验证拒绝，必须有明确低收益测试请求进入接收端。

### 4.2 接收方正式决策（`Respond`）

按严格顺序执行：

**前置：硬合法性**

- 调用 `MarriageProposalModel.CanRespond(p)` 和政治校验。若战争、Clan 非法、文化非法、Hero 已转族等，走 `Invalidate(p, reason, false)`（客观失效、无冷却），**不走 `TryReject`**。
- 若仍合法但 Hero 当前被俘／参战／在军团：这是临时条件，不阻止回应，真正结婚延期执行。

**决策：**

1. `scoreReceiver = Evaluate(p, p.ReceiverClan).Total`。
2. `scoreReceiver >= AcceptanceThreshold`（初版建议 0）：`TryAccept`。若反提议发起者是玩家或存在玩家 Clan，玩家答复永远由 UI 决定，AI 不替玩家回答。
3. 否则，如果 `p.CounterCount == 0`，生成有限候选反方案：切换婚后归属、增加/调整礼金付款方向和金额；对每个反方案调用统一硬合法性检查，计算当前接收方 `scoreNewReceiver`，以及对方 Clan 的预估 `scoreNewOther`。只有本 Clan 不亏且对方有接受空间时调用 **一次** `_service.TryCounter(...)`。
4. 若不存在可行反方案，或已经 `CounterCount == 1`，`TryReject(..., LowClanBenefit/UnacceptableTerms)`，增加 60 天双向配对冷却。
5. `TryCounter` 返回失败时应区别硬性失效与业务失败；不直接当“已经反提议”，可回落明确拒绝并记录结果。

```csharp
// 结构性伪代码：以当前 proposalService 的真实方法签名为准。
void Respond(FamilyProposal p)
{
    var eligibility = ValidateHardConditions(p);
    if (!eligibility.IsAllowed)
    {
        proposalService.TryInvalidatePoliticallyOrLegally(p, eligibility.Reason);
        return;
    }
    var current = scoreModel.Evaluate(p, p.ReceiverClan);
    if (current.Total >= tuning.AcceptanceThreshold)
    {
        proposalService.TryAccept(p.ProposalId, p.ReceiverClan);
        return;
    }
    if (p.CounterCount == 0 && TryFindMutuallyPlausibleCounter(p, out var counter))
    {
        var result = proposalService.TryCounter(
            p.ProposalId, p.ReceiverClan, counter.Direction,
            counter.GoldPayerClan, counter.GiftAmount);
        if (result.IsAllowed) return;
        // 返回失败必须记录并处理，不得无限重复反提议。
    }
    proposalService.TryReject(p.ProposalId, p.ReceiverClan,
        FamilyReasonCode.LowClanBenefit /* 新增，枚举只追加 */);
}
```

### 4.3 反提议数量、回合和钱款安全

- 原请求 `CounterCount=0` 可反提议一次；反请求 `CounterCount=1` 只能接受或拒绝。
- 原请求原地改状态 `SupersededByCounter`，创建新 `ProposalId`，保留 `ChainId` 与 `ParentProposalId`；同一对 Hero 的锁属于**同一链**，不能先释放再重新争夺。
- 反请求有新七天答复期；**不得因 AI 未收到回复自动再发送一份反提议**。
- 若反方案提出由对方付款，最终仍须对方明确接受，不允许 AI 自己在对方未同意时扣金币。
- `TryCounter()` 在实现之前需检查 `FamilyProposalService` 只使用 `CanProposeMarriage()` 对新条款校验：要检查旧提议已失效、Hero/Clan 实时状态、指定付款方以及方案实际发生变化。反提议同一链锁占用不能被普通新提议预检错误阻止；需要专门的 counter 校验。
- 执行前金币不足，只等待财务恢复到执行截止，不可将对方承诺款项自动变成我方付款，也不可部分支付后视为完成。

### 4.4 状态与业务原因不可混淆

| 发生的事情 | 建议最终状态 | 配对冷却 | 事件性质 |
|---|---|---|---|
| 本 Clan 因人数少、不愿外嫁/入赘、政治价值低等拒绝 | `Rejected` | 60 天 | 主观利益拒绝 |
| 接收方提出唯一一次反提议 | 原：`SupersededByCounter`；新：`PendingResponse` | 不新增 | 条款协商 |
| 反提议被主动拒绝 | `Rejected` | 60 天 | 主观利益拒绝 |
| 七天无人作出回复 | `TimedOut`（使用现有枚举） | 60 天（按既定规则） | 谈判超时 |
| 开战、Clan 变非法、异国异文化 | `Invalidated` | **无** | 客观失效 |
| Hero 被俘、参战、逃亡 | 不改变待回应／待执行状态 | 无 | 临时阻碍 |
| 已接受、因换国发生实质政治利益变更而不再愿履约 | `Invalidated`，新 `PoliticalInterestChanged` 原因 | 无 | 政治环境变化 |
| 婚姻完成 | `Completed` | 无 | 不可逆成功 |
| 已结婚但支付结果不确定 | `PaymentReconcileRequired` | 不自动重试 | 人工/专用恢复 |

**注意**当前 `FamilyProposalStatus` 已存在 `TimedOut`，但 `FamilyCampaignBehavior.OnDaily()` 对超时回复调用 `TryReject(...Expired)`，而 `TryReject()` 固定把状态写成 `Rejected`；为避免“超时统计混入拒绝”，需要增加 `TryExpirePending()`，并定义原请求、反请求一致的期限行为。

---

## 5. 核心实现 C：战争、换国、Clan 灭亡的全生命周期处理

### 5.1 事件—状态矩阵（同时适用于第一次提议和反提议）

| 政治事件 | `PendingResponse` | `AcceptedPendingExecution` | `Executing/PaymentReconcileRequired` | `Completed` |
|---|---|---|---|---|
| 两个参与 Clan 所在王国直接宣战 | **立即失效**、解锁、无冷却 | **立即失效**、解锁、无冷却 | 交给执行恢复，不作盲目回滚 | 不拆散 |
| 先因战争失效，后来议和 | 不恢复旧请求 | 不恢复 | 不适用 | 不适用 |
| 一方换到和平王国、双方同文化 | 保留同一请求，最新状态重评，原 7 天时限不变 | 双方只对实质政治变更重评履约意愿 | 不改变已执行事实 | 不撤销 |
| 一方换国，成为异国异文化 | 立即失效 | 立即失效 | 不盲目回滚 | 不撤销 |
| 一方加入与另一方交战的王国 | 立即失效（`AtWar`） | 立即失效 | 不盲目回滚 | 不撤销 |
| 一方永久脱离王国、`Clan.Kingdom==null` | 立即失效（`InvalidClan`） | 立即失效 | 不盲目回滚 | 不撤销 |
| Clan 灭亡／成为雇佣兵／小派系／叛军／强盗家族 | 立即失效 | 立即失效 | 异常恢复 | 婚姻维持 |
| 任一 Hero 被俘、暂时逃亡、在战斗／军团中 | 可以继续协商 | 继续等待，最长 30 天 | 按执行阶段恢复 | 不适用 |

判断是否两 Clan 交战，必须检查 `OriginClanA.IsAtWarWith(OriginClanB)` 在**当前**政治环境下是否成立；**任一 Clan 只是与第三方王国开战**不等于这两 Clan 互相开战，不得把这种情况误判为无效联姻。

### 5.2 原版事件入口和触发位置

核对仓库 `code/CampaignEvents.cs` 存在：

```csharp
CampaignEvents.WarDeclared
// IMbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>
CampaignEvents.OnClanChangedKingdomEvent
// IMbEvent<Clan, Kingdom, Kingdom,
//          ChangeKingdomAction.ChangeKingdomActionDetail, bool>
CampaignEvents.OnClanDestroyedEvent
// IMbEvent<Clan>
```

`FamilyCampaignBehavior.RegisterEvents()` 补齐三个监听器；事件回调不直接修改 `p.Status`，通过 `FamilyProposalService` 的安全转移方法。Worker 编译时要对照**实际引用的游戏 DLL**确认签名，避免仅复制示意泛型签名而编译失败。

```csharp
CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, OnClanDestroyed);
```

- 宣战：关联双方 `OriginClanA/OriginClanB` 当前互为敌方的活跃婚约**立即失效**（未执行的）；事件要筛选，不能对所有与任何战争有关的婚约一刀切。
- 换国：对涉及该 Clan 的有效协议标记重校验；复合“离开旧王国→加入新王国”过程中短暂 `Kingdom==null` 不应误判为最终独立；建议在安全的 Campaign update 点读取最终状态并复核。若战争直接成立则立即失效。
- Clan 灭亡：关联协议立即失效；保护执行中协议，进入恢复路径。
- 兜底：日结算、玩家点击接受/反提议、**调用 `MarriageAction.Apply` 前**做统一即时硬约束验证，防事件漏报。

### 5.3 政治纯校验统一 API（建议新增）

新增 `MarriagePoliticalValidationModel.cs`，或向 `MarriageProposalModel` 增添独立只读方法。**不得复制出与现有 `CultureAllowed()` 不一致的文化规则**。

```csharp
PoliticalValidationResult Validate(FamilyProposal p)
{
    // 仅处理未完成可撤销的合法性；执行中由恢复服务接管。
    var a = p.OriginClanA;
    var b = p.OriginClanB;
    if (!MarriageEligibilityModel.CanUseClan(a).IsAllowed ||
        !MarriageEligibilityModel.CanUseClan(b).IsAllowed)
        return Invalid(FamilyReasonCode.InvalidClan);
    if (a.IsAtWarWith(b))
        return Invalid(FamilyReasonCode.AtWar);
    if (!SharedCultureAndKingdomRulesAllow(p.HeroA, p.HeroB))
        return Invalid(FamilyReasonCode.CultureForbidden);
    if (p.HeroA?.Clan != a || p.HeroB?.Clan != b)
        return Invalid(FamilyReasonCode.ClanChanged);
    return Allowed();
}
```

检查顺序可调整以优先在日志上显示战争，但不得遗漏其他禁用条件；`MarriageEligibilityModel.CanUseClan` 先报 `InvalidClan` 也合理。必须对同一输入给出稳定结果。

### 5.4 协商期与已接受待执行期的**不同**重评行为

**协商中**：

- 政治变化使硬规则非法 → 立即 `Invalidated(reason)`，无冷却。
- 换国后仍合法 → 保留原 `ProposalId/ChainId/CreatedAt/ReplyDeadline/CounterCount`；接收方在原截止前或第 7 天以**最新**家族 Relation、人口、文化、王国进行独立决策。
- 不因换国重新给予一轮反提议机会；`CounterCount=1` 仍只能接受或拒绝。

**双方已接受、只是暂时不能执行**：

- 战争、硬禁止 → 立即失效，除非已经进入 `Executing` 等不可安全回滚状态。
- 仅关系值日常浮动，不应每天重新谈判或取消已同意协议。
- 若**实质换国**，使用已经修正的评分模型重新计算双方在**原固定条款**下的当前收益；建议引入**履约容忍度**（例如总分低于 `-20` 才考虑取消；`-20` 是防抖初始阈值，不是谈判接受阈值），防止极小变化破坏承诺。
- 对 AI Clan，如果重新评分显示不可履约 → 终止，记为 `Invalidated(PoliticalInterestChanged)`、无冷却。对于玩家 Clan：不能由 AI 替玩家重新评分并自动撤销玩家承诺；如仍合法，要保留并通知玩家需要确认的重要变化，或者提供显式玩家撤回入口。**对方 AI Clan**仍可基于政治变化终止并通知玩家。
- 绝不偷偷把已签约的正常婚姻改成入赘，也不自行更改礼金；新条款必须发新请求。

### 5.5 执行中的安全边界

当前 `MarriageExecutionService.TryExecuteMarriage()` 把 `CanExecuteMarriage()` 不允许及临时阻碍统一写为等待；需调整业务编排：

1. `PendingResponse/AcceptedPendingExecution` 先跑硬政治验证，永久非法 → `Invalidate(..., false)`。
2. 被俘／逃亡／战斗／军团 → 等待直至已有 30 天执行期限，暂不终结。
3. 付款金额不足 → 按现有“资金恢复前等待，超期取消”处理；金币转账需使用 `GiveGoldAction` 后全额核验。
4. 已开始 `Executing`、`MarriageApplied` 或进入 `PaymentReconcileRequired` → 不调用普通撤销流程，不重新调用结婚 Action，也不能视情况把已付金币退回或再次扣取。使用 `ExecutionStep` 识别是否需要人工/专用恢复。
5. `MarriageAction.Apply` 原版会引发 Hero 迁族等事件；现有 `MarriageExecutionScope` 机制仍然保留，防止其自身事件反向取消婚约。
6. 现有 `FamilyProposalService.Invalidate()` 无前置状态保护：须提供带状态断言的唯一转移 API，防止重复事件把 `Completed` 改写成 `Invalidated`，或者错误释放执行中的锁。

---

## 6. 代码接口、状态与持久化契约

### 6.1 稳定接口（建议）

| 负责者 | 接口 | 语义 |
|---|---|---|
| `ClanHeroValueModel` | `MarriageScoreBreakdown Evaluate(FamilyProposal p, Clan evaluatingClan)` | 无副作用，计算指定 Clan 的成员流动、人口、Relation、政治、礼金分项 |
| `ClanHeroValueModel` | `int CountAdultNobleHeroes(Clan clan)` | 统一成人计数定义 |
| `MarriagePoliticalValidationModel` | `PoliticalValidationResult Validate(FamilyProposal p)` | 纯硬约束校验，不写请求状态 |
| `ClanMarriageDecisionService` | `ConsiderClan(Clan clan)` | 主动选择多种可行候选和条款，不代替接收 Clan 作终局判断 |
| `ClanMarriageDecisionService` | `Respond(FamilyProposal p)` | 先区分硬非法，再评分：接受／唯一一次反提议／主观拒绝 |
| `FamilyProposalService` | `TryCounter(...)` | 不更改现有签名；负责唯一反提议的创建与链锁迁移 |
| `FamilyProposalService` | `TryExpirePending(...)`（建议新增） | 超时终态 `TimedOut`、正常谈判冷却，不伪装主观拒绝 |
| `FamilyProposalService` | `TryInvalidateActive(..., false)`（建议新增） | 严格检查可撤销状态，终结整条链，无冷却、幂等 |
| `FamilyCampaignBehavior` | 事件回调／DailyTick | 调度政治复核、已接受政治意愿重评、执行及日志；不自己重写评分 |
| `MarriageExecutionService` | `TryExecuteMarriage(...)` | 经硬合法性/临时阻碍分流后才调用原版 Action，保留进度恢复 |

**禁止的依赖方向：** 评分模型不调用 `TryAccept/TryReject/TryCounter`；事件监听器不直接设置 `Proposal.Status`；UI 不复制家族评分或硬政治校验；AI 不调用 `MarriageAction.Apply`。

### 6.2 `FamilyReasonCode` 新增项与存档安全

建议增补 `LowClanBenefit`、`UnacceptableTerms`、`PoliticalInterestChanged`、`NoViableCounterProposal` 等，是否全部存档由开发者简化，但核心主观与客观原因必须区分。

**绝对约束：** 当前 `FamilyProposalEnums.cs` 的枚举是按值隐式递增保存的；**只在最后追加枚举项，不能在 `InvalidProposal` 等旧值之前插入、排序或重命名导致旧存档数值映射漂移**。如果需要重排，请先显式固定旧数值并做好迁移验证。

`FamilyProposal` 当前 `[SaveableProperty(1..24)]` 已被占用；新增政治快照如 `LastPoliticallyRechecked...` 或 `AcceptedKingdomA/B`，从 25 往后分配稳定 ID。对于评分明细推荐**每次按实时值重算而不保存**；如果要对已签约旧分留档以支持重评分对比，再审慎追加字段。`FamilySaveDefiner` 如引入新 Saveable class 注册新的 class ID，不得破坏已有定义。

### 6.3 日志格式与统计口径

追加结构化、可读的文件日志（优先 Debug，用当前 `FamilyDiagnostics`，避免刷屏）：

```text
[MF][MarriageEvaluation] proposal=<id> clan=<clan> phase=Receiver
own=<hero> other=<hero> direction=FemaleJoinsMaleClan
adultCount=3 baseHero=80 inflow=0 outflow=-160 succession=0
alliance=55 relationRaw=+40 relation=+12 culture=+20 kingdom=+20 gift=0 total=-53
[MF][MarriageDecision] proposal=<id> decision=Counter reason=LowClanBenefit
[MF][MarriageDecision] proposal=<id> decision=Rejected reason=LowClanBenefit
[MF][MarriageInvalidated] proposal=<id> reason=AtWar cooldown=false
```

以上是**期望格式样例**，不是当前已有日志。日志至少要区分：`Proposed`、`Accepted`、`Rejected(业务)`、`Countered`、`TimedOut`、`Invalidated(政治/死亡/身份)`、`ExecutionWaiting`、`Completed`；以 `ProposalId` 和 `ChainId` 关联，跨存档不要直接按行数计算接受率。

主观拒绝率 = `Rejected(明确业务原因)` / `实际进入 AI 回应且合法的谈判数`；政治失效率及超时率单独统计。自然模拟没有规定必须达到固定比例，但**人为构造的利益不足用例必须能得到拒绝或反提议**。

---

## 7. Worker 分工：文件所有权、依赖与交付物

每个 Worker 开工前先记录：仓库最新 SHA、运行游戏版本、将修改的文件。若主分支已更新则 diff 当前代码并调和，**不得用旧内容覆盖新改动**。以下是建议并行分工；同一文件只能有一名集成责任人。

### Worker A —— 评分模型（必须先交付可调用接口）

**独占主改文件**：

- `ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs`
- 新增 `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageScoreBreakdown.cs`
- 数值配置（可新建 `MarriageScoreTuning.cs`，或由 A 负责修改 `MarriagePolicyModel.cs`）

**任务**：成员流向对称计分、成年 Hero 统计、女外嫁 1.0 / 男入赘 1.5、人口稀缺函数、继承风险、Relation 0.3 封顶 30、联盟政治和礼金效用；输出分项评分且完全无副作用。向 Worker B/C 交付稳定 `Evaluate(p, clan)` 接口定义。

**交付证明**：针对同一 Hero，`N=2` 的出走成本 > `N=12`；男性离族成本 > 女性；Clan 留住人物不会被扣流失成本；目标 Clan 只计真正迁入 Hero；关系 -100 与 +100 的差异精确且受封顶。

### Worker B —— AI 提议与回应算法

**独占主改文件**：

- `ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs`
- 若需辅助：新增 `MarriageOfferCandidate.cs`、`MarriageCounterOfferEvaluator.cs`（不创建第二套评分实现）

**任务**：多位己方 Hero 候选、两种婚姻方向、可控的礼金方案、层级优先排序、创建前校验、失败时重试下一候选、AI 接收方接受／反提议／拒绝三分支；与 Worker A 的 `Evaluate`、Worker C 的 `TryInvalidate...` 和现有 `TryCounter` 对接。

**交付证明**：无对方收益的提议不自动接受；反提议只有一次；多个可行候选时不再只选年龄最大的 Hero；战争原因不走 `TryReject`；真正失效的候选不会在日志宣称提议成功。

### Worker C —— 政治生命周期／存档契约（共享状态的唯一所有者）

**独占主改文件**：

- `ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs`
- `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs`
- `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriagePoliticalValidationModel.cs`（新增）
- `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageProposalModel.cs`（共享校验规则时）
- `ModifiedFamily/ModifiedFamily/Marriage/Execution/MarriageExecutionService.cs`
- `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalEnums.cs`
- 如有需要，`FamilyProposal.cs` / `FamilySaveDefiner.cs` 的新增存档字段

**任务**：监听 `WarDeclared`、`OnClanChangedKingdomEvent`、`OnClanDestroyedEvent`；统一政治校验、活跃请求幂等失效；协商与签约延迟执行的分流；主观拒绝 vs 超时 vs 政治失效；30 天执行限期与恢复保护；遵循玩家回应权限；安全的反提议链锁与存档恢复。

**交付证明**：两个参与王国互相宣战当时立即失效；第三方战争不失效；换国但同文化可以保留；换国变异国异文化立即失效；所有政治失效不加冷却、议和不恢复；`Executing/Completed` 不被覆盖。

### Worker D —— 日志与验证（可并行，但不应与 B/C 同时改其文件）

**独占主改文件**：

- `ModifiedFamily/ModifiedFamily/Localization/FamilyLogText.cs`
- `ModifiedFamily/ModifiedFamily/Diagnostics/*` 中指定的日志桥接扩展
- 测试工程、脚本及测试案例文档（无须修改战役存档文件）

**任务**：实现 A/C 定义的日志结构；对比修复前日志；编写评分单元测试、三分支决策测试、政治状态回归场景。改动本体运行时模块的最终测试入口由 C 集成。

**交付证明**：输出按 `ProposalId`、`ChainId`、原因聚合的统计报告，严格区分 `Rejected/TimedOut/Invalidated`；日志值与 `MarriageScoreBreakdown` 一致，完成至少第 8 节全部强制用例。

### 集成负责人 —— 合并顺序（不能省略）

1. 固定 SHA 与基线构建；执行初始烟测并留存日志。
2. **A 评分模型**合入，编译、跑评分单测。
3. **C 政治状态机和安全转移接口**合入，编译、跑政治事件最小用例。
4. **B AI 协商**对接 A/C 最终 API 后合入，编译、跑协商模拟。
5. **D 日志与全套回归**合入，检查输出不刷屏。
6. 进入长期 AI 模拟及游戏内 UI 回归；发现不足只调整模型参数或业务理由，不设随机拒绝概率。
7. 如未被授权：不直接修改远端 Git、不提交 PR；由负责人按本地工作流处理。

---

## 8. 强制验收清单（必须全部测试，不能仅编译通过）

### 8.1 评分单元测试

| ID | 输入与场景 | 必须满足 |
|---|---|---|
| S01 | A 的 Hero 女性迁到 B；从 A 的视角 | `Inflow=0`，`Outflow>0`，不会错误获得 B 男方完整成员价值 |
| S02 | 与 S01 同一婚姻，从 B 的视角 | `Inflow>0`，`Outflow=0` |
| S03 | N=2 与 N=12，其余完全相同、女性外嫁 | N=2 流失成本严格更大 |
| S04 | 同一 Hero 基本价值、N 相同，女性外嫁 vs 男性入赘的源 Clan | 男性入赘成本 1.5 倍（不含继承额外成本） |
| S05 | Clan 已婚贵族 5 人，未婚贵族仅 1 人 | 成年人数 N 仍包含 5 名已婚者（及其他合格成人），不能只用可联姻 Hero 数 |
| S06 | Relation +100 vs -100，其余相同 | `V_relation=+30/-30`；不能覆盖战争硬性非法 |
| S07 | 领袖 Hero 离族，礼金 100 万 | 硬性拒绝，不允许分数越高越合法 |
| S08 | 无礼金、我方付、对方付三方案 | 对双方分项符号相反；不存在无付款方自动扣钱 |
| S09 | `ValueToClan` 高龄、低等级极端输入 | 合法 Hero 价值不产生负数、NaN/Infinity |
| S10 | 正常嫁娶／入赘同一对 Hero | 对接收双方各自实际流入/流出判定正确 |

### 8.2 AI 协商场景

| ID | 场景 | 必须满足 |
|---|---|---|
| A01 | 接收方仅有 2 名成年贵族，提出无礼金失去关键 Hero 的婚约 | 不会无条件接受；适当拒绝或提出确实有利的反提议 |
| A02 | 接收方成年贵族 12 名、关系 +80、收益足够的正常外嫁 | 有可能正式接受，不能全部拒绝 |
| A03 | 接收方正常外嫁总分为负，入赘对双方均可行 | 提出 `CounterCount=1` 的入赘反提议 |
| A04 | 入赘对自己好但对方必须失去唯一领袖 | 不提出违法反提议，改为合理礼金方案或拒绝 |
| A05 | 当前提议 `CounterCount=1` 仍低收益 | 只能拒绝；不再产生第二次反提议 |
| A06 | 同一 Hero 已被占用或与候选配对冷却 | `ConsiderClan` 尝试下一个，不陷入反复选同一人 |
| A07 | 人为强制 low-score 提议到接收方 | 返回可解释拒绝并产生日志 `LowClanBenefit`，不是 `InvalidProposal` |
| A08 | 玩家 Clan 收到请求／反提议 | 等待玩家操作，不由 AI 回应 |
| A09 | 付款方金币不足、金额为负、极大整数 | 不构建不可结算的合法付款方案，不抛异常 |
| A10 | 旧存档仍有待回应请求，加载后再评分 | 继续原有请求，不再复制发第二份 |

### 8.3 政治事件与执行安全

| ID | 场景 | 必须满足 |
|---|---|---|
| P01 | 第 3 天两个参与 Clan 所在王国宣战 | 当即 `Invalidated(AtWar)`；无冷却、释放 Hero 锁 |
| P02 | 双方所属王国没有互相开战，只是其中一方对第三国宣战 | 原婚约不因此失效 |
| P03 | 唯一一次反提议等待期间宣战 | 反请求失效，原请求仍为已取代，整条链不复活 |
| P04 | 签约后因被俘延迟，战争发生 | 立即失效；获释后不得自动成婚 |
| P05 | 两国议和，先前因战争失效 | 原 ProposalId 不恢复、不自动执行 |
| P06 | 同国同文化 → 异国同文化，双方和平 | 原请求仍可合法并按新 Relation/政治条件评分 |
| P07 | 同国异文化 → 异国异文化 | 立即失效，原因 `CultureForbidden` |
| P08 | Clan 正式退出王国且持续独立／成为雇佣兵／被消灭 | 立即失效，原因 `InvalidClan` |
| P09 | 先离开后加入，事件中短暂 Kingdom=null，但最终仍合法 | 延后读取最终状态，不误取消合法婚约 |
| P10 | 已接受后实质换国但仍合法，双方分数都高于履约阈值 | 继续原婚约，婚后归属与礼金不变 |
| P11 | 已接受后实质换国致 AI 一方严重不愿履约 | `Invalidated(PoliticalInterestChanged)`、无冷却、通知玩家（如相关） |
| P12 | 日常小幅 Relation 变动而无实质政治事件 | 不反复取消已接受协议 |
| P13 | 已完成婚姻并支付礼金后宣战 | 不拆散、不退款、不改变 `Completed` |
| P14 | `Executing/MarriageApplied/PaymentReconcileRequired` 时宣战 | 不重复 MarriageAction，不盲目清除已履行的款项义务 |
| P15 | 被俘后解救、无战争 | 待执行协议在 30 天内可以继续 |
| P16 | 玩家拒绝 vs 战争取消 vs 玩家逾期 | 三种终态／原因及 60 天冷却严格区分 |
| P17 | 同一天战争事件与回应到期 | 优先永久非法，不会先接受又失效、误付金币 |
| P18 | 保存重载后的 `Invalidated/TimedOut/SupersededByCounter` | 终态保持，锁重建正确，无重复通知或婚姻 |

### 8.4 长期模拟验收与统计约束

- 至少跑 **100～200 次有效进入 AI 回应阶段的婚约**，统计有效回应量、明确拒绝、反提议、接受、超时、政治失效、成功成婚分别的数量；**不能用跨存档的日志总行数除以新请求数**。
- 记录发起方／接收方成人数分布、关系分区间、婚姻方向、成员流出方、赠礼额度、文化层级。
- 若模拟依旧“全部接受”，必须先检查接收方总分和对真实流入的条件判断是否正确；若“全部拒绝”，检查基础联姻政治收益、人口系数与礼金是否过低或过高；只有可解释的数值分布才算合格。
- 拒绝率无强制固定百分比，且**不允许**以 `Random.NextDouble()<0.2` 之类随机拒绝造出好看的比例。
- 多轮加载、关窗、重复打开提议 UI、收到请求通知时不崩溃；输出的原因和后台状态相符。

---

## 9. 最小安全伪代码：将两类修复合并到一条主流程

```csharp
// 本段是任务协作伪代码，不是直接可编译的替换代码。
void AdvanceProposal(FamilyProposal p)
{
    if (p == null || IsTerminal(p.Status)) return;

    // 已经执行部分原版 Action 的请求必须独立恢复。
    if (p.Status == Executing || p.Status == PaymentReconcileRequired)
    {
        executionRecovery.Process(p);
        return;
    }

    // 优先硬资格与政治校验；战争永远不是“拒绝协商”。
    var legality = politicalValidator.Validate(p);
    if (!legality.IsAllowed)
    {
        proposalService.TryInvalidateActive(p, legality.Reason, cooldown: false);
        return;
    }

    if (p.Status == PendingResponse)
    {
        if (!p.ReplyDeadline.IsPast) return;
        if (p.ReceiverClan == Clan.PlayerClan)
        {
            proposalService.TryExpirePending(p);
            return;
        }
        marriageAi.Respond(p); // 接受、唯一反提议、业务拒绝。
        return;
    }

    if (p.Status == AcceptedPendingExecution)
    {
        if (p.ExecutionDeadline.IsPast)
        {
            proposalService.TryInvalidateActive(p, Expired, cooldown: false);
            return;
        }
        if (HasTemporaryBlockersOrRecoverableInsufficientFunds(p))
            return;
        executionService.TryExecuteMarriage(p);
    }
}

// WarDeclared、OnClanChangedKingdomEvent、OnClanDestroyedEvent
// 也调用相同的“硬资格+安全状态迁移”入口，实现即时事件和日结算兜底。
```

`AcceptedPendingExecution` 的 **政治利益重评分**还需在“发生实质换国”时另行调用 `EvaluateForBothClans`，而不是每天在 `AdvanceProposal` 里重复评估。

---

## 10. 不能犯的实现错误（代码审查阻断项）

1. 只改 `FamilyCampaignBehavior` 的宣战事件，不改 `ClanHeroValueModel` / `Respond()`：**仍会出现大量无条件接受**，本次需求不算完成。
2. 只改流入/流出公式，不加人口、性别方向、Relation 和基础联盟收益：AI 可能从“全接受”直接变为“全拒绝”，仍不算完成。
3. 用 `TryReject()` 处理战争／Clan 灭亡：会错误触发 60 天冷却；必须 `Invalidated(..., false)`。
4. 用 `TryReject(...InvalidProposal)` 表示家族利益不足：理由不准确，难以调试和统计。
5. 忘记 AI 调用 `TryCounter()`：UI 能反提议不等于 AI 会反提议。
6. 反提议直接编辑原请求或重新释放 Hero 锁：会破坏唯一反提议链和配对占用。
7. `CountAdultNobleHeroes()` 使用 `CanNegotiateHero()`：会排除已婚 Hero、被俘等成人，模型大幅高估人口稀缺。
8. 每次日结算对已同意婚约重新算分并直接取消：会让已达成协议因轻微数值抖动频繁失效。
9. 把另一国对第三国开战误认为两家族互相开战。
10. 宣战回调不看状态就直接 `Invalidate()`：可能把已经 `Completed` 的婚姻标成失效，甚至清理执行中锁。
11. 在保存／加载反序列化回调里直接执行结婚、修改 Clan、转金币；应在安全战役 Tick 校验。
12. 对持久化的 enum 重排或对 `SaveableProperty(1..24)` 复用编号：破坏旧存档兼容。
13. 由 AI 代玩家接受／拒绝，或已签约时静默更改玩家同意的礼金和归属。
14. 忽略候选创建错误，日志却记录“提议已经发出”。
15. 未实机验证 `MarriageAction.Apply()` 和礼金动作的中途失败／重复调用保护。

---

## 11. 文件级实施清单（给 Worker 的最终勾选表）

| 是否完成 | 优先级 | 文件／类 | 最终交付 |
|---|---|---|---|
| ☐ | P0 | `ClanHeroValueModel.cs` | 正确的流入/流出、成年人系数、外嫁／入赘成本、Relation 与分项评分 |
| ☐ | P0 | `MarriageScoreBreakdown.cs`（新增） | 所有评分组成可审查，AI 发送方和接收方共用 |
| ☐ | P0 | `ClanMarriageDecisionService.cs` | 多候选+两个方向，接收方三分支，业务拒绝原因准确 |
| ☐ | P0 | `FamilyProposalService.cs` | 一次反提议、超时独立状态、安全客观失效、幂等处理 |
| ☐ | P0 | `FamilyCampaignBehavior.cs` | 战争、换国、灭亡监听及日结算终局分流 |
| ☐ | P0 | `MarriagePoliticalValidationModel.cs`（新增） | 统一实时政治资格与战争判断 |
| ☐ | P0 | `MarriageExecutionService.cs` | 永久／临时分流，不重复结婚与礼金 |
| ☐ | P1 | `MarriagePolicyModel.cs` 或 `MarriageScoreTuning.cs` | 参数集中，初始系数可平衡 |
| ☐ | P1 | `FamilyProposalEnums.cs` | 新理由值追加，不破坏存档枚举 |
| ☐ | P1 | `FamilyProposal.cs` / `FamilySaveDefiner.cs`（按需） | 仅在确有保存需求时追加字段和类型 |
| ☐ | P1 | `MarriageProposalModel.cs` | 与政治模型共享硬规则、及时复核，对方 Clan 文化口径一致 |
| ☐ | P1 | `FamilyLogText.cs` / Diagnostics | 输出评分分项及接受/拒绝/反提议/失效原因 |
| ☐ | P1 | 自动化测试与实机测试说明 | §8 全用例验证，通过后出对照报告 |
| ☐ | P2 | 现有玩家 UI 通知（必要时） | AI 取消、客观失效与玩家待回应通知可区分 |

### Worker 最终提交的完成报告格式

```text
基线 commit:
修改文件（含新增文件）:
新增接口及被调用位置:
模型参数（当前值／来源／待调参点）:
存档兼容结论:
编译结果（引用游戏 DLL 版本）:
测试结果 S01–S10 / A01–A10 / P01–P18:
有效回应样本量／接受／拒绝／反提议／超时／政治失效:
未解决问题与复现步骤:
是否更改游戏后台规则／玩家 UI:
```

---

## 12. 相关源码和既有文档

**以本轮核对的具体 commit 版本为准，不盲信后续 main 的内容完全相同。**

- [评分模型 `ClanHeroValueModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/ModifiedFamily/Marriage/Models/ClanHeroValueModel.cs)
- [AI 决策 `ClanMarriageDecisionService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/ModifiedFamily/Marriage/AI/ClanMarriageDecisionService.cs)
- [提议生命周期 `FamilyProposalService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs)
- [战役事件 `FamilyCampaignBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/ModifiedFamily/Campaign/FamilyCampaignBehavior.cs)
- [资格模型 `MarriageProposalModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageProposalModel.cs)
- [执行服务 `MarriageExecutionService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/ModifiedFamily/Marriage/Execution/MarriageExecutionService.cs)
- [本体事件声明 `code/CampaignEvents.cs`](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/code/CampaignEvents.cs)
- 旧政治变化实现文档：`ModifiedFamily_联姻政治变化边界处理_实现方案_20261025.md`（**被本文整合取代**）。
- 联姻 UI 独立文档：`ModifiedFamily_三级联姻界面_开发实施任务书_20261025.md`（仅作为界面接口参考，**不属于本次实施主体**）。

**最终验收判据：** 真实成员流动被正确计价；人口少的家族更难放人且男性入赘更难；Relation 和礼金对利益有可解释贡献；合法低收益请求可拒绝或一次反提议；战争使未完成婚约立即无冷却失效；换国分支遵守文化王国规则；已执行婚姻不回滚；存档安全；玩家能理解每次拒绝／失效原因。
