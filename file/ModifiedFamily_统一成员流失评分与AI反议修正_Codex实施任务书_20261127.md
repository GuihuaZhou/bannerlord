# ModifiedFamily — 统一成员流失评分与 AI 反议修正

## Codex 增量开发实施任务书

- 日期：2026-11-27
- 仓库：`GuihuaZhou/bannerlord`，分支 `main`
- 源码审查基线：[ded4f8bf1cc4e64e9c8ef65ba4adc81674c57ed9](https://github.com/GuihuaZhou/bannerlord/commit/ded4f8bf1cc4e64e9c8ef65ba4adc81674c57ed9)，提交时间 2026-11-27 14:07:20 UTC。
- 依据：用户最新确认的两项规则、上一轮《ModifiedFamily_联姻评分_AI决策_地图通知_Codex实施任务书_20261126.md》、实机 `ModifiedFamily(3).log`（224 行）以及上述固定 SHA 的实际源码。
- 性质：待实施规格；不是已完成的修改、编译或验收报告。

## 0. 开工约束与权威顺序

1. 在仓库根目录执行 `git rev-parse --show-toplevel`、`git branch --show-current`、`git rev-parse HEAD`、`git status --short`。阅读根目录和目标目录实际存在的 `AGENTS.md`。不覆盖无关工作区改动。如果本地 HEAD 比审查基线更新，重新对账相关方法，不回退已实现功能。
2. 用户最新明确要求为业务规则权威；当前源码、实际 API、Git 状态为施工依据。根目录 `file/ModifiedFamily*` 按 Git 最后修改时间排序，较新的设计覆盖较旧的冲突内容。可使用 `git log -1 --format='%ci %h' -- file/<文件名>` 逐个核查。本任务书只覆盖上一轮关于主动离族豁免、男女流失方向系数及 AI 反议筛选/排序的规则，其余设计继续有效。
3. 分两阶段完成：A（成员流失评分），B（AI 反议及相关诊断）。每阶段检查差异、调用链、可执行测试及风险。仅修改 ModifiedFamily 和对应设计文档。不做无关重构，不修改其他 Mod。
4. 不提交 Commit、不 Push、不打开 PR、不部署游戏文件，除非用户另行明确要求。允许编译与可执行测试。没有实机证据的验收项必须报告 BLOCKED，而非 PASS。

## 1. 用户确认的业务规则及范围

| 编号 | 最新规则 | 状态 |
|---|---|---|
| MF-02-R | 主动离族不再零成本；所有离族一律扣除人物价值乘新版人口稀缺系数 | 已确认 |
| MF-08-R | 取消男女出嫁/入赘系数；保留已确认的新版稀缺公式 | 已确认 |
| MF-09-A | 反议必须改善本方收益且新收益非负；合理考虑接收方接受可能性 | 已确认 |
| MF-09-B | 反议按本方收益增量 × 对方接受概率排序 | 已确认 |
| MF-09-C | 两种归属方向、零礼金、双支付方向和各礼金档位完整比较；无合理方案直接拒绝 | 已确认 |
| MF-09-D | 反议及随机拒绝原因应与实际决策一致；日志可解释 | 配套修正 |

**本轮明确不变**：三分支概率曲线、概率插值算法、AI 普通联姻候选的排序/搜索范围、人口稀缺公式本身、人物基础价值、联盟/关系/文化/王国/礼金评分、继承惩罚、合法性/政治规则、婚约执行和金钱结算、地图通知及 UI 时间控制、收养、冷却与期限、现有存档结构。

AI 平均直接接受率约 20% 仍是长期平衡目标，而不是本轮硬性验收数字；先完成两项修复并收集新数据，再决定是否校准概率。不得沿用旧评分分布重新设置概率。

## 2. 固定 SHA 源码对账

以下 `MF/` 表示 `ModifiedFamily/ModifiedFamily/`。行号基于审查 SHA，仅供定位；施工以实际符号为准。

| 文件/方法 | 当前问题或现状 | 本轮处理 |
|---|---|---|
| `MF/Marriage/Models/ClanHeroValueModel.cs`，`Evaluate` L21–58 | 主动离族零成本；被动离族乘男女系数；可选第三参数 `voluntaryOutflowClan` | 统一成本，删除第三参数及豁免逻辑 |
| 同文件 `MarriageScore` L60–72 | 构造预览后调用 Evaluate | 保持接口，使用统一模型 |
| `MF/Marriage/Models/MarriageScoreTuning.cs` L15–30 | Female=1、Male=1.5；CalculateScarcity 已实现新公式 | 删除无用男女常量；公式原样保留 |
| `MF/Marriage/Models/MarriageScoreBreakdown.cs` | 保存 IsVoluntaryOutflow、OutflowDirectionMultiplier、ScarcityMultiplier（非存档模型） | 删除前两项，保留 ScarcityMultiplier 与 Total |
| `MF/Marriage/AI/ClanMarriageDecisionService.cs` L26–27 | Evaluate wrapper 调用 ResolveVoluntaryOutflowClan | 去除历史链依赖 |
| 同文件 `Respond` L116–159 | 一次抽签；反议失败和随机拒绝都使用 LowClanBenefit | 传入实时本方评分给 TryCounter；区分原因；不改概率 |
| 同文件 `TryCounter` L192–249 | 只生成反方向零礼金；本方≥0、对方≥-20，按双方总分之和排序 | 完整候选、收益增量约束及新排序 |
| 同文件 `TraceDecision`、`TraceAcceptance` L251–293、361–385 | 包含主动离族/男女系数；TraceAcceptance 记录原始 counter 而非实际有效概率；直接拒绝缺完整抽签日志 | 清理旧字段，补齐真实决策日志 |
| 同文件 `CounterPreview` L388–390 | 已调用 Service.CreateCounterProposalPreview | 保留统一预览，不另造正式记录 |
| `MF/Proposals/FamilyProposalService.cs`，`ResolveVoluntaryOutflowClan` L90–102 | 通过 ParentProposalId 回溯主动意愿 | 确认无其他调用后删除；保留父链和历史婚约数据 |
| 同文件 `TryCounter` L179–196 | 已负责合法性、次数、同条款检查、财力、ChainId 和人物锁、状态变更、事件 | 保留原业务事务；不把 AI 收益约束加到玩家通用 API |
| `MF/Localization/FamilyLogText.cs`、`MF/ModuleData/Languages/CNs/modifiedfamily-zho-CN.xml` 及实际基础语言资源 | LowClanBenefit、UnacceptableTerms、NoViableCounterProposal 等文案 | 仅补充与修改直接相关的日志/文案；不修改既有历史原因语义 |
| `MF/ModuleData/MarriageAIConfig.xml` | 已是 DecisionCurve | 不修改 |

引用源码可通过 `https://github.com/GuihuaZhou/bannerlord/blob/ded4f8bf1cc4e64e9c8ef65ba4adc81674c57ed9/ModifiedFamily/ModifiedFamily/<相对路径>` 查阅。远程代码搜索索引可能滞后于最新提交，以上核心方法来自固定 SHA 直接读取；开工必须以本地 `git grep` 核实完整引用。

## 3. Phase A：统一成员流失评分

### 3.1 唯一公式

设 N = 当前本家族存活且属于该 Clan 的成年贵族数，沿用 `CountAdultNobleHeroes` 当前统计条件（非死亡、IsLord、Age≥18、Clan 一致；不以未婚或可协商人数替代）。

```
Scarcity(N) = 1 + 4/(N+1)                  N <= 3
            = 0.3 + 27.2/(N+1)^2           N >= 4

OutflowCost = BaseHeroValue * Scarcity(N)
```

四种情况全部一致：主动外嫁、主动入赘、被动外嫁、被动入赘。只要 `DestinationClan != evaluatingClan`（合法的两家族婚约），即计算流失成本。不依据性别、发起方、接收方、ParentProposalId、CounterCount 或历史意愿减免/加重。N 取当前人数（含拟离族人物），不要预先减1；继承风险仍使用 N-1。

`MarriageScoreTuning.CalculateScarcity` 已正确实现以上公式（含 Math.Max(0, adultHeroCount) 的防御逻辑），保持不变。人物基础价值继续使用 `Clamp(75 - Abs(Age-28)*1.25, 20,160)`（实际最高75）；流入方收益仍为对方人物基础价值。`Total` 仍为 `InflowValue - OutflowCost - SuccessionPenalty + AllianceBenefit + RelationValue + CultureValue + KingdomValue + GiftValue`。

### 3.2 修改 Evaluate

把方法改为 `Evaluate(FamilyProposal proposal, Clan evaluatingClan)`，保留现有参数检查、归属方向计算、人物选择及其他评分分项。核心替换示意：

```csharp
result.AdultHeroCount = CountAdultNobleHeroes(evaluatingClan);
bool receivesHero = proposal.DestinationClan == evaluatingClan;
result.BaseHeroValue = ValueToClan(receivesHero ? other : own, evaluatingClan);
if (receivesHero)
{
    result.InflowValue = result.BaseHeroValue;
}
else
{
    float scarcity = MarriageScoreTuning.CalculateScarcity(result.AdultHeroCount);
    result.ScarcityMultiplier = scarcity;
    result.OutflowCost = result.BaseHeroValue * scarcity;
    if (result.AdultHeroCount - 1 <= 1)
        result.SuccessionPenalty = MarriageScoreTuning.SuccessionPenalty;
}
```

`OutflowCost` 保持正数成本，Total 中统一减去，不能重复取负。继承惩罚80仍独立计算；不更改领袖离族等硬性限制。

### 3.3 清理不再使用的上下文

- 删除 `MarriageScoreTuning.FemaleOutflowMultiplier`、`MaleOutflowMultiplier`；删除 `MarriageScoreBreakdown.IsVoluntaryOutflow`、`OutflowDirectionMultiplier`；保留 `ScarcityMultiplier`。
- `ClanMarriageDecisionService.Evaluate` 包装调用改为 `_values.Evaluate(proposal, clan)`。`TryCounter` 的两个预览 Evaluate 也走相同 wrapper；正式回复、普通候选、正式反议评分及 `ShouldContinueAcceptedProposal` 共享模型。`MarriageScore` helper 保持公开签名，不再依赖预览 ProposerClan 判断成本；不要额外删除构造字段，除非确认确无必要。
- 删除 `FamilyProposalService.ResolveVoluntaryOutflowClan`，前提是全仓确认无其他调用。不得删除 `ParentProposalId`、`ChainId`、`CounterCount`、`OriginClanA/B` 或父子链记录。这些用于协商、人物锁、冷却和存档，不是本次的冗余数据。
- 删除诊断字段 `directionMultiplier`、`voluntaryOutflow`；保留实际婚姻 direction、成人数、scarcity、outflow 等字段。更新注释和测试。
- 保持 `CreateCounterProposalPreview`，正式反议和预览继续用同一个构造入口。

### 3.4 数值验收

以人物基础价值75为例（成本不含继承惩罚）：

| N | 稀缺系数（约） | 四种场景统一成本（约） |
|---:|---:|---:|
| 1 | 3.000000 | 225.00 |
| 2 | 2.333333 | 175.00 |
| 3 | 2.000000 | 150.00 |
| 4 | 1.388000 | 104.10 |
| 5 | 1.055556 | 79.17 |
| 6 | 0.855102 | 64.13 |
| 8 | 0.635802 | 47.69 |
| 12 | 0.460947 | 34.57 |
| 20 | 0.361678 | 27.13 |
| 30 | 0.328304 | 24.62 |

人数越多成本越低，渐近下限为人物价值×0.3。N=1、2 另扣继承惩罚80；N=3 不扣。合法性仍先于评分（例如领袖不能离族）。不要让测试通过非法婚约去绕过正式业务校验；纯评分可使用隔离 fixture。

### 3.5 日志案例回归

- `ModifiedFamily(3).log` L5–8：巴努·萨马勒，N=6、人物67.54、旧主动流失成本0、旧总分173.52。新成本约57.75，其他项不变则总分约115.77。
- L57：乌拉斯拉夫，N=4、人物63.62、男性被动离族，旧成本132.46；新成本约88.30，旧总分39.74 对应新分约83.90。仅为固定输入重算，并非保证下一次候选仍相同。
- L26、52：女性被动离族原系数1，新公式成本与上一轮一致；男性被动离族成本为上一轮的2/3。

## 4. Phase B：AI 反议候选生成、筛选与排序

### 4.1 决策入口：保持三分支，只抽签一次

当前 `Respond` 先 `CanRespond`，再 `Evaluate`，计算 `DecisionChances`，一次 `MBRandom.RandomFloat`。已经反议的提议 `CounterCount != 0` 时，counter 有效概率为0，原反议概率并入拒绝。保持这一机制，不增加随机抽签。

修改反议入口为 `TryCounter(proposal, score.Total)`（方法名和参数可按项目风格调整）。传入的是本次正式回复时已计算的**接收方**分数，而不是原始发起方分数、创建时预测分数或上次保存时的分数。候选计算使用同一时点的实时家族状态及 Phase A 统一模型，避免因评分上下文改变虚构收益增量。

- 抽中接受：仍调用 `TryAccept`，不搜索反议。
- 抽中反议：搜索最优合法候选；只成功创建一份反议；找不到有效方案则直接拒绝，不能回头接受原提议或重抽。
- 抽中拒绝：直接拒绝，不搜索反议。
- 已失效或不是 PendingResponse 的记录不能再次决策或重复添加冷却。保留 `CanRespond` 和 Service 的实际状态校验。若反议尝试过程中 Service 已使原提议失效，应尊重现有终态而不是再尝试拒绝。

### 4.2 完整枚举条款

当前 `TryCounter` 只预置 `Opposite(currentDirection)` 的零礼金，漏掉 `currentDirection` 的零礼金。修复为遍历当前方向和相反方向，每个方向独立生成：

1. `GiftAmount=0, GoldPayer=null` 一项；
2. 每个正数 `MarriageAiConfig.Current.GiftSteps`：本方付款和对方付款各一项，通过现有 `CanAiOfferGift`。

默认档位 30000、50000、80000、100000，两方向共最多 18 组，含原条款等待过滤；不新增档位。若配置包含0或重复档位，跳过非正金额并去重；统一零礼金的付款方为 null。财力沿用当前 `CanAiOfferGift`（当前 leader.Gold、gift≤gold、gift≤floor(gold×0.30)），不修改礼金价值函数。

```csharp
// 示意：继续使用当前 CounterTerms 类型及 CounterPreview。
foreach (var direction in new[] { currentDirection, Opposite(currentDirection) })
{
    terms.Add(new CounterTerms { Direction = direction, GoldPayer = null, GiftAmount = 0 });
    foreach (int gift in config.GiftSteps.Where(g => g > 0).Distinct().OrderBy(g => g))
    {
        if (CanAiOfferGift(responder, gift, config))
            terms.Add(new CounterTerms { Direction = direction, GoldPayer = responder, GiftAmount = gift });
        if (CanAiOfferGift(otherClan, gift, config))
            terms.Add(new CounterTerms { Direction = direction, GoldPayer = otherClan, GiftAmount = gift });
    }
}
```

以现有 `CounterPreview` → `FamilyProposalService.CreateCounterProposalPreview` 统一构造，勿在 AI 内手动拼装 ChainId、ParentProposalId、OriginClanA/B 等。候选去重键为 `(Direction/DestinationClan, normalized payer, GiftAmount)`；无礼金时忽略历史记录中无意义的付款方。与原协议相同的归属、付款方及金额提前排除，不消耗 Service 尝试。

### 4.3 过滤条件（AI专属）

对每份候选依次执行：

| 条件 | 要求 |
|---|---|
| 原提议 | PendingResponse、当前 responder 是 ReceiverClan、CounterCount=0、婚约合法 |
| 新条款 | 与原条款不同且不重复 |
| 合法性 | `_proposalModel.CanProposeMarriage(preview).IsAllowed`，保留财力规则 |
| 本方净收益 | `newOwnScore >= 0` |
| 本方收益改善 | `newOwnScore - originalOwnScore > 0` |
| NPC 对方接受可能性 | `MarriageScoreTuning.CalculateAcceptanceChance(newOtherScore) > 0` |
| 玩家对方 | 不用 NPC 接受概率或 NPC 评分下限过滤；保留硬性合法性、财力和本方收益改善约束 |

以上本方非负、收益必须改善的限制属于 **AI 反议生成政策**，不是新增玩家手动反议条件；不修改 `FamilyProposalService.TryCounter` 对玩家的合法性语义。NPC 概率过滤替代现有 `CounterOtherClanFloor=-20`（当前曲线负分和零分直接接受概率为0，因此旧 floor 不足以保证有效接受机会）。该常量如无其他业务调用可删除，不能顺手删除或改动其他独立阈值。

默认严格比较 `delta > 0f`；不对正式评分先四舍五入。可使用极小的纯浮点容差防止精度噪声，但不得擅自引入游戏意义上的最低增量（如5分、10分）。有限数检查排除 NaN/Infinity（兼容 netstandard2.0，用 `float.IsNaN`、`float.IsInfinity` 而非新版 `float.IsFinite`）。若原分异常则不发反议，记录诊断。

### 4.4 排序公式

```
Gain = NewOwnScore - OriginalOwnScore
OtherChance = CalculateAcceptanceChance(NewOtherScore)
CounterUtility = Gain * OtherChance
```

同一反议搜索只涉及相同两个人物和家族，不需要新增政治层级排序。NPC 候选按 CounterUtility 降序；同分时依次按接受概率降序、本方新分降序、礼金金额升序，最后以方向和付款方稳定身份确定顺序。不得继续使用双方分数求和或绝对自身分数代替增量。

**玩家接收方施工默认值**：玩家无可知的随机接受概率，且上一轮普通 AI→玩家提议已使用不乘 NPC 概率的排序分支。因此 AI 向玩家反议时，使用 `CounterUtility = Gain`（等效排序权重1，而非声称玩家100%接受），NPC 概率不作为门槛；预测对方评分可作为同分辅助项，但不得阻断其自主决定。此处是为明确可施工细节提出的默认处理，不是用户另行确认的玩家概率公式。不得调用随机数预测玩家行为。

伪代码（非整文件替换；结合实际项目类型/风格落地）：

```csharp
// originalOwnScore 来自 Respond 中当前接收方实时评估
var ranked = terms
    .Select(t => new { Term = t, Preview = CounterPreview(original, t) })
    .Where(x => !SameTerms(original, x.Preview)) // 新建纯 helper 或复用等价比较
    .Where(x => _proposalModel.CanProposeMarriage(x.Preview).IsAllowed)
    .Select(x => new {
        x.Term, x.Preview,
        Own = Evaluate(x.Preview, responder),
        Other = Evaluate(x.Preview, otherClan)
    })
    .Where(x => IsFinite(x.Own.Total) && IsFinite(x.Other.Total))
    .Select(x => new {
        x.Term, x.Preview, x.Own, x.Other,
        Gain = x.Own.Total - originalOwnScore,
        OtherChance = MarriageScoreTuning.CalculateAcceptanceChance(x.Other.Total)
    })
    .Where(x => x.Own.Total >= MarriageScoreTuning.AcceptanceThreshold
        && x.Gain > 0f
        && (otherClan == Clan.PlayerClan || x.OtherChance > 0f))
    .OrderByDescending(x => x.Gain * (otherClan == Clan.PlayerClan ? 1f : x.OtherChance))
    .ThenByDescending(x => otherClan == Clan.PlayerClan ? 0f : x.OtherChance)
    .ThenByDescending(x => x.Own.Total)
    .ThenByDescending(x => otherClan == Clan.PlayerClan ? x.Other.Total : 0f)
    .ThenBy(x => x.Term.GiftAmount)
    .ThenBy(x => x.Term.Direction)
    .ThenBy(x => x.Term.GoldPayer?.StringId ?? string.Empty, StringComparer.Ordinal)
    .ToList();
```

上例 `IsFinite`、`SameTerms` 为需实现的纯 helper，非当前仓库已存在 API。过滤前后的计数可以在单次枚举中积累；避免同一候选反复计算模型或重复副作用。对玩家不需要计算实际 NPC 抽签概率，仅在诊断可标明预测分数/不适用；不得将排序权重1显示为真实接受概率。

### 4.5 正式执行与失败处理

继续调用现有 `_service.TryCounter(original.ProposalId, responder, candidate.Term.Direction, candidate.Term.GoldPayer, candidate.Term.GiftAmount)`。它负责状态改变、父链继承、锁转移、事件；AI 不直接修改 `_proposals`、`_locks`、金币或冷却。

- 按已排序列表依次尝试，若一个候选因条款/财力变化失败且原提议仍待回复，可尝试下一份；这是确定性候选遍历，不是第二次抽签。
- 若 Service 已改变原提议状态（失效、已被替换等），停止遍历，不重复拒绝/发布事件。
- 成功创建后立即返回；确认新记录 ParentProposalId 指向原记录、ChainId 相同、CounterCount=1，原记录 SupersededByCounter。只允许一份新提议；不要改变现有事件发布顺序。
- 所有有效候选失败或不存在：若原记录仍为 PendingResponse，使用既有 `NoViableCounterProposal` 拒绝一次。保持现有 Service 的冷却及人物锁释放规则。
- 已接受婚约的政治重估 `ShouldContinueAcceptedProposal` 继续走统一模型，保留 `CommitmentThreshold=-20` 和原政治事件触发条件；不要因本轮改动主动全量重判旧婚约。

### 4.6 反议回归样例

日志 L52–56：戴·阿罗曼克原收益59.06，保持归属方向、自己额外支付3万后收益7.40。新规则下即使对方收益高达206.35，也必须因本方增量为负而淘汰。女性被动流失成本本轮未变，因此此固定输入回归仍成立。

日志 L128–132：库吉特原收益133.09，反议主动支付5万后收益111.68；同样淘汰（同一归属方向，流入评分不受本轮改变）。日志 L80–84 的反转付款方向案例说明有收益增量并不保证合理；必须依据对方接受概率排序，不能直接断言所有反转礼金都禁止。保持无额外礼金时，取消礼金候选应当与其他候选一起竞争。

排序纯函数固定输入（假设概率，非当前曲线实际输出）：原分80，候选 A(40,0.80)、B(110,0.55)、C(158,0.35)、D(173,0.20)、E(194,0.05)。A 淘汰，B/C/D/E 的 CounterUtility 分别16.50/27.30/18.60/5.70，选择 C。另使用实际 DecisionCurve 产生的概率进行集成测试，确保两者不混淆。

## 5. 日志、原因码与本地化

必须可以从一条正式答复追溯：提议 ID/谈判链、家族名称、当前分数、accept/counter/reject 三个有效概率、roll、抽中分支、最终业务结果及失败原因。现有 `TraceAcceptance` 仅在接受/反议分支调用，且 `counterChance` 直接从曲线读取，CounterCount=1 时与有效概率不一致。本轮修正为记录本次真实有效概率（CounterCount=1 时 counter=0，reject=1-accept），直接拒绝也记录一次抽签。不可为了诊断再次抽签、计算业务动作或重复发布通知。

成功反议记录：原本方分、新本方分、delta、对方分、NPC接受概率或玩家不适用、CounterUtility、条款、过滤前/后候选数、真实结果；无方案则记录 `NoViableCounterProposal` 与简明过滤统计。保持现有诊断开关、玩家相关过滤和根 AGENTS 日志要求；必要时提供低频汇总用于 AI 样本统计，不输出所有家族每个候选每 tick 的明细。

原因处理采用最小兼容方案（施工默认，不增加序列化结构）：

- 抽中直接拒绝：沿用已有 `UnacceptableTerms` 表示对方未接受条款，详细诊断标明 `RandomReject`；不再无条件使用 `LowClanBenefit` 冒充实际概率抽签原因。
- 抽中反议而无有效方案：使用已有 `NoViableCounterProposal`；诊断区分无合法条款、无收益改善、NPC接受概率为0、Service失败等。
- 成功反议：不设失败原因，使用现有 `None` 或独立非持久化日志说明抽中反议；保持业务状态事件。不要把 `CounterBranch` 当作拒绝原因。
- 保留客观失效原因原样；玩家主动拒绝仍为 `PlayerDeclined`；历史已保存 `LowClanBenefit` 不重新解释、不改枚举编号。

如需要新文案，检查 `FamilyLogText.ReasonText`、`MarriageDecision` 与对应英文默认字符串、中文 XML 及实际基础资源中的键。不得硬编码中文在 C# 中，保持占位符一致。日志类别【】内尽量不超过5个汉字，使用人可读姓名和家族名，内部 ID 仅作为次要字段。清理旧主动意愿及男女系数诊断字段，但保留正常婚姻方向信息。

## 6. 保持不变的概率、配置、UI及存档

`MarriageAIConfig.xml` 当前 `DecisionCurve`（score,accept,counter）如下，直接保持原值：

```
-100: 0.00,0.00
 -50: 0.00,0.01
   0: 0.00,0.05
  30: 0.22,0.20
  60: 0.35,0.35
 100: 0.45,0.45
 150: 0.65,0.30
 200: 0.80,0.17
 250: 0.90,0.08
 300: 0.95,0.04
```

保留 `DecisionChances`、`CalculateAcceptanceChance`；NPC直接接受概率取现有插值。原提议排序 `ExpectedBenefit = receiver == Clan.PlayerClan ? proposerScore : proposerScore * receiverChance`，以及 Tier 等其余排序键保持不变。保持 AI 主动检查7日、基础概率50%、家族主动提议冷却14日、现有配对冷却和回复/执行期限、最大反议一次、礼金档位和30%限制。

`FamilyProposal` 当前已包含 SaveableProperty 1–26，其中24 PlayerNotified、25 PlayerResultNotificationFlags、26 PendingResultNotificationFlags；本轮不新增、删除、重排这些字段，不改 SaveableTypeDefiner、SyncData key、enum 数值或旧存档的链路状态。`MarriageScoreBreakdown` 是非持久化评分结果；其冗余字段可以清理。不要误删父提议数据或上轮结果地图通知功能，不修改 UI host 和 notification service。现有存档的 PendingResponse 用新规则在下一次正常答复时重新评分；AcceptedPendingExecution 保持现有执行和政治重估生命周期；历史终态不回算、不补发、不迁移。

## 7. 验收测试矩阵

以下是必须执行或明确标记 BLOCKED 的检查；测试尽量使用仓库已有框架。若缺游戏程序集，先执行不依赖运行时的纯公式/排序验证，并明确编译或实机受阻。不要虚构测试项目或通过结果。

| ID | 场景 | 通过条件 |
|---|---|---|
| A01 | N=1/2/3/4/5/6/8/12/20/30，人物75 | 匹配 §3.4，浮点误差允许±0.01 |
| A02 | 同一人物价值与成人数，四种离族场景 | 成本完全相同，均非零 |
| A03 | 两种归属方向的流入方 | 人物流入收益正确，流失成本0 |
| A04 | N=1/2/3 | 继承惩罚分别80/80/0，独立于流失成本 |
| A05 | 原提议、反议交换双方、反议预览 | 相同条款及实时状态下每一家族评分相同，不因发起方改变 |
| A06 | 旧存档有 ParentProposalId 的待回复反议 | 无迁移、无异常，按新模型正常处理 |
| A07 | 已接受婚约政治重估 | 统一评分生效；保留-20阈值及现有触发，不额外抽签 |
| A08 | 全仓查找旧系数和意愿字段 | 活跃代码无残余；历史文档可保留并标注被覆盖 |
| B01 | 双方财力充足，默认4档礼金 | 两方向18种唯一初始组合，分别有零礼金 |
| B02 | 原条款有礼金 | 同方向零礼金可成为有效反议，原条款被提前排除 |
| B03 | 自身新收益小于/等于原分 | 一律淘汰，不调用 TryCounter |
| B04 | 原分-20、新分-5/0/10 | -5淘汰；0和10满足自身门槛（仍需其他条件） |
| B05 | NPC接受概率0与正数 | 前者淘汰，后者可参与排序，不额外添加硬阈值 |
| B06 | 玩家作为反议接收方，预测分为负 | 不因NPC概率或NPC对方分而淘汰；合法性和本方收益规则仍有效 |
| B07 | 玩家手动反议 | 通用 Service 仍按原合法性校验，不套用 AI 增量限制 |
| B08 | 固定排序样例 §4.6 | C 胜出；同分排序稳定 |
| B09 | 不满足付款能力或30%限制 | 不生成该 AI 礼金候选；不改变资金结算 |
| B10 | 抽中接受/拒绝 | 不执行反议搜索；一次随机数 |
| B11 | 抽中反议且无有效候选 | 直接拒绝一次、NoViableCounterProposal、正确冷却及解锁；不重抽 |
| B12 | 抽中反议，第一候选合法性复检失败 | 原记录仍 Pending 时可试下一候选；若已失效则停止 |
| B13 | 成功反议 | 原 SupersededByCounter，新 PendingResponse，同 ChainId、正确 ParentProposalId、CounterCount=1，人物锁与事件正常 |
| B14 | 已反议记录正式答复 | 有效反议概率0，只有接受/拒绝，不创建第二次反议 |
| B15 | 重复调用或状态已变化 | 不重复产生反议、冷却或结果通知 |
| B16 | 实际日志 L52–56、128–132 固定条件 | 两个损害自身收益的旧候选均被过滤 |
| B17 | 三分支概率日志 | 概率和为1，记录一次 roll 及最终决策，CounterCount=1 时 counter=0 |
| B18 | 普通提议候选和配置 diff | 排序逻辑、XML曲线、搜索限制及礼金档位不变 |
| R01 | 上轮地图通知、暂停、结果通知 | 无代码改动；已有场景 smoke test 不回归 |
| R02 | 旧存档读写与婚约执行 | Saveable ID不变，已接受婚约不重复执行，金币与归属不重复结算 |
| R03 | 全量编译 | ModifiedFamily 成功，无新增错误；记录现有警告和环境 |

## 8. 实施顺序与交付

### A 阶段

1. 以 `git grep -n -E 'ResolveVoluntaryOutflowClan|IsVoluntaryOutflow|OutflowDirectionMultiplier|FemaleOutflowMultiplier|MaleOutflowMultiplier' -- ModifiedFamily` 查找实际全部引用。
2. 修改统一评分、调用签名、诊断字段及注释；核对其他分项没有变化。
3. 完成 A01–A08 可执行测试并检查 diff。

### B 阶段

1. 调整 Respond 的反议调用并保持一次抽签。
2. 实现 TryCounter 的完整条款枚举、去重、改善约束、NPC概率及玩家分支、确定性排序。
3. 保持 Service 的权威写入路径及反议预览构造，补足失败状态检查、实际原因和日志。
4. 完成 B01–B18 及回归 R01–R03；检查 `git diff --check`、`git diff --stat`、`git diff`。

### 编译

在仓库根目录、具备项目现有 TaleWorlds 引用及 NuGet 缓存的 Windows 开发环境：

```powershell
dotnet build .\ModifiedFamily\ModifiedFamily\ModifiedFamily.csproj --no-restore -c Release
```

项目目标 `netstandard2.0`，现有 `.csproj` 指向本地 Bannerlord DLL。禁止为了通过编译随意替换 SDK、游戏版本、其他 Mod 或放宽 API 约束。缺依赖时说明实际错误，不能宣称编译成功。未经授权不运行部署命令；如用户后续要求，按根 AGENTS 使用 `tools/Deploy-Mods.ps1 -Build -Configuration Release -Modules ModifiedFamily`。

### 文档交付

完成后将本任务的最终实现与验证结果同步为 `file/ModifiedFamily_统一成员流失评分与AI反议修正_Codex实施任务书_20261127.md`（可直接以本文为基线），注明覆盖旧主动离族豁免及男女系数、旧反议规则，不覆盖未变条款。若仓库已有更新的相关文档，合并冲突时以最新用户需求为准并保留历史，不删除旧文档。未获提交授权时，文档和代码保留在工作区。

### 完工报告必须包含

- 实际 HEAD、变更文件/方法、差异范围及保留边界；
- A/B 两阶段所有验收项 PASS / FAIL / BLOCKED（含原因与证据）；
- 实际 build 命令及输出、未验证的实机和存档风险；
- 玩家反议权重1、原因码复用及任何其他施工默认的明确说明；
- 不提交、不部署状态。

## 9. 后续实机平衡验证（不属于本轮改概率的授权）

收集至少100–200次唯一有效 NPC 正式答复，记录原始提议及反议分开口径，并按 ProposalId/ChainId 去重、区分日志窗口前遗留提议、未完成谈判与已结束谈判。至少统计：原始直接接受率、反议率、直接拒绝率、反议后接受率、谈判链最终接受率、实际完成婚姻数；同时比较归属方向、发起方流入/流出、礼金方向与档位、成人数及双方评分、每次反议 delta 和概率。任何成功 AI 反议的 delta 必须严格为正，本方新分必须非负；同输入四种离族成本必须一致。若直接接受率仍明显偏离20%，根据新样本再独立设计概率曲线，而非本任务内擅改配置。玩家候选列表为空问题独立排查，不在本次范围。

## 10. 交付边界与尚未验证项

本文已根据固定 SHA 的实际评分模型、AI 服务、存档类、反议 Service 和配置编写，未修改 Git，也没有执行实际编译或游戏测试。全仓最新引用须由 Codex 开工本地核查（远程搜索索引存在滞后）；本任务提供的 helper 名称、同分规则及玩家反议权重属于施工默认，非现有 API 或实际运行结果。日志原因复用现有 enum 是为避免不必要的持久化变更；若现有项目其他消费者存在特殊语义，应核对后以兼容方式实现，并在报告说明。目标接受率只能通过完成后的实机数据校准，不保证本轮修复后立即精确达到20%。
