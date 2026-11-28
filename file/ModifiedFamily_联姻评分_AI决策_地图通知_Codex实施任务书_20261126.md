# ModifiedFamily 联姻评分、AI决策与地图通知 — Codex 实施任务书

> 日期：2026-11-26；仓库：`GuihuaZhou/bannerlord`；审查分支 `main`；基线 HEAD `36b2b4c13f2bd2f49bcd187f31c9a979cb318999`（2026-11-24）。本文是施工规格，不是已完成的代码变更。仅修改 ModifiedFamily 及其设计文档。

## 0. 开工指令与权威顺序

1. 执行 `git rev-parse --show-toplevel`、`git branch --show-current`、`git rev-parse HEAD`、`git status --short`；读取根目录 `AGENTS.md`、目标目录实际存在的下级 `AGENTS.md`。本次远程查询 `ModifiedFamily/AGENTS.md` 返回 404，不代表本地一定不存在。不得覆盖用户未提交的修改。
2. 以用户最新明确要求为业务权威；以当前源码、实际 API 和 Git 状态为实现权威。`file/ModifiedFamily*` 设计文档冲突时按最后修改 Commit 时间排序，较新覆盖较旧，未覆盖的旧规则继续有效。本任务书经用户授权实施时覆盖旧概率、主动离族与人口系数、候选排序、地图通知相关条款。不要根据文档推定代码已实现。
3. 施工前对照以下文档及源码；如 HEAD 已变化，重新核查相关差异。以下目录均相对于仓库根目录：

| Git 最后修改（北京时间日期） | 文件（均在 `file/`） |
|---|---|
| 2026-11-14 | `ModifiedFamily_AI联姻频率接受率与礼金调整_Codex实施任务书.md`（当前最高权威；commit `f8890aae07d0fce3912c4ccd26a2c389b87372ec`，21:22:09） |
| 2026-11-13 | `ModifiedFamily_联姻候选与AI频率诊断实施方案_20261112.md` |
| 2026-11-12 | `ModifiedFamily_Marriage_Diagnostics_Implementation_Plan.md` |
| 2026-10-31 | `ModifiedFamily_UI_Sprite_Optimization_Worker_Implementation.md` |
| 2026-10-28 | `ModifiedFamily_联姻AI拒绝反提议与政治变化_统一实现方案_V2_20261025.md`、`ModifiedFamily_联姻政治变化边界处理_实现方案_20261025.md`、`ModifiedFamily_三级联姻界面_开发实施任务书_20261025.md` |
| 2026-10-24 | `ModifiedFamily_后台实现方案_基于Git_20261024.md` |

可用 PowerShell `Get-ChildItem file/ModifiedFamily* | ForEach-Object { git log -1 --format='%ci %h' -- $_.FullName; $_.Name }` 复查（必要时将路径改为相对仓库路径）。

4. 本任务不要求 Commit、Push、PR 或部署；未经用户明确授权不执行。优先局部修改，不新增模块依赖，不重构婚约状态机，不修改原版配婚拦截、执行事务、收养系统。

## 1. 目标、已确认规则和施工默认值

| 编号 | 已确认目标 | 施工方案 |
|---|---|---|
| MF-01/06/07 | AI联姻接受率约20%；低分偏拒绝，中分反议概率最高，高分接近300时主要接受；连续变化 | 统一三分支模型，保留300分理论上限，替换旧平方概率和零概率触发反议 |
| MF-02 | 主动入赘不产生普通成员流失负收益 | 主动外嫁也按对称规则取0，保留继承风险；明确反议意愿来源 |
| MF-08 | 少于4名成年贵族保持旧成本，较大家族降低成本；新版公式用户已同意 | N≤3 用旧式，N≥4 用 0.3+27.2/(N+1)^2 |
| MF-04/05 | 不能只考虑发起方收益 | 同政治层级按本方收益×对方直接接受概率排序；礼金模型不单独变更 |
| MF-03.1 | 右侧事件应允许关闭 | `ForceInspection=false`，复用原生关闭按钮 |
| MF-03.2 | 处理地图事件时游戏时间暂停 | 使用现有 ScreenHost 管理时间暂停、锁定、恢复 |
| MF-03.3 | AI接受、拒绝玩家提议应有地图通知 | 独立结果通知，订阅 `ProposalResolved` |

**待确认但为使施工可执行而给出的默认设计**：主动外嫁与入赘对称、反议意愿规则（§3）、初始概率控制点（§4）、同层预期收益公式（§5）、X仅关闭通知而不拒绝（§6）、玩家相关超时和客观失效结果通知（§8）、关闭后恢复打开前速度。若用户未确认这些细节，Codex 应在实施报告中逐项标记为本任务书建议默认，不宣称用户已确认；如用户另有明确要求则以其为准。20%为长期平衡目标，不是每份婚约固定概率，也不承诺小样本精确命中。

## 2. 源码对账与改动边界

下列路径前缀 `MF/` 指 `ModifiedFamily/ModifiedFamily/`。

| 当前文件 / 方法 | 已核实行为 | 改动 |
|---|---|---|
| `MF/Marriage/Models/ClanHeroValueModel.cs` `Evaluate`、`CountAdultNobleHeroes`、`MarriageScore` | 只按 DestinationClan 判断流向；所有离族均扣成本；人数包含所有存活成年贵族；旧 preview helper 不含发起方 | 区分主动离族；新版人口系数；显式评分上下文 |
| `MF/Marriage/Models/MarriageScoreBreakdown.cs` `Total` | Inflow-Outflow-Succession+Alliance+Relation+Culture+Kingdom+Gift | 保留公式，增加只读诊断字段 |
| `MF/Marriage/Models/MarriageScoreTuning.cs` | 理论满分300，旧 `CalculateAcceptanceChance` | 三分支统一纯函数；原有其他阈值继续各司其职 |
| `MF/Marriage/Configuration/MarriageAiConfig.cs`、`MF/ModuleData/MarriageAIConfig.xml` | 读取旧 max=0.5、exponent=2.0，启动/会话加载 | 单一新曲线配置，完整校验与旧配置回退 |
| `MF/Marriage/AI/ClanMarriageDecisionService.cs` `ConsiderClan`、`AddCandidate`、`Respond`、`TryCounter`、`CounterPreview`、`ShouldContinueAcceptedProposal` | 先本方分数，再对方概率；0概率才尝试反议；反议预览用原始创建预览；政治重估独立阈值-20 | 同层预期收益、一次三分支抽签、反议预览一致、履约不抽签 |
| `MF/Proposals/FamilyProposalService.cs` `CreateMarriageProposalPreview`、`TryCounter` | 原始预览随机 ID；反议正式记录另行构造，交换双方、共享 ChainId、保存 ParentProposalId | 统一反议预览条款构造和主动意愿推导，不改变事务、锁与事件顺序 |
| `MF/Campaign/FamilyCampaignBehavior.cs` `BuildServices`、`OnSessionLaunched`、`OnDaily`、`RevalidateMarriageProposals` | 订阅 Created、Invalidated，不订阅 Resolved 发布结果；每日处理期限与执行 | 结果订阅、必要的幂等恢复；不改变 Tick |
| `MF/UI/Notifications/FamilyProposalMapNotificationVM.cs` | `ForceInspection=true`；终态移除；Inspect 打开提议界面 | 恢复关闭按钮；关闭与业务状态分离 |
| `MF/UI/FamilyActionScreenHost.cs` `Open`、`Close`、`OnScreenPopped` | Layer/Movie/input focus 生命周期；没有战役时间管理 | 增加 opt-in 时间控制与异常释放 |
| `MF/UI/Marriage/MarriageProposalScreenService.cs`、`MarriageProposalScreenVM.cs`、`MF/UI/FamilyPlayerInteractionService.cs` | 图形界面优先；失败回退 MultiSelectionInquiry；VM关闭、成功处理、百科跳转调用 Host.Close；存在 ShowFamilyActions→ShowInbox | 处理界面启用暂停；回退路径也须暂停；收件箱可重新进入待答复婚约 |
| `MF/UI/Notifications/FamilyProposalNotificationService.cs`、`FamilyProposalNotificationPatch.cs`、`FamilyProposalMapNotification.cs` | `PlayerNotified` 只表示来件通知已发布；失效仅文字提示；注册原有通知类型 | 独立结果通知类型与幂等标记；不复用 PlayerNotified |
| `MF/Persistence/FamilySaveDefiner.cs`、`MF/Proposals/FamilyProposal.cs`、`FamilyProposalEnums.cs` | Definer ID 2026102401，类 1/2/3/9，枚举 4～8；FamilyProposal property 1～24 | 只追加必要标识，不重排或改变旧 ID |

保持 7 日错峰、主动概率50%、家族主动提议冷却14日、人物配对冷却60日、回复期限7日、执行期限30日、最多一次反议、AI礼金档位0/3万/5万/8万/10万、资金30%上限、同文化或同王国合法性、政治层级、领袖不可离族、继承惩罚80、已接受婚约政治重估阈值-20、真实婚礼和金币结算所有权。不要修改其他 Mod。

## 3. Phase A — 评分模型（MF-02、MF-08）

### 3.1 保留原分项；只调整普通离族成本

`ClanHeroValueModel.ValueToClan` 仍为 `Clamp(75 - Abs(Age-28)*1.25,20,160)`（实际最高75）；成年计数仍为 Clan.Heroes 中存活、IsLord、属于本Clan、Age≥18，含已婚、被俘和参战者。不要以 `CanNegotiateHero` 计数。

```text
Total = InflowValue - OutflowCost - SuccessionPenalty
      + AllianceBenefit(55) + RelationValue(clamp(Relation*0.3,-30,30))
      + CultureValue(20 or 0) + KingdomValue(20 or 0) + GiftValue
```

若 DestinationClan == evaluatingClan，仅增加迁入的对方 Hero 基础价值；无普通流出、无流失继承惩罚。实际离族方则无 InflowValue；主动离族普通 OutflowCost=0，被动离族 Cost=BaseHeroValue×Scarcity(N)×性别系数（女1、男1.5）。无论主动被动，现有 `N-1<=1` 继承惩罚80继续生效，领袖禁止离族继续由硬合法性模型校验。不要把主动离族改成正的人物新增价值。

```csharp
// netstandard2.0 compatible; avoid MathF unless verified available.
float n = Math.Max(0, adultHeroCount) + 1f;
float scarcity = adultHeroCount <= 3 ? 1f + 4f / n : 0.3f + 27.2f / (n * n);
float multiplier = own.IsFemale ? 1f : 1.5f;
result.OutflowCost = voluntary ? 0f : result.BaseHeroValue * scarcity * multiplier;
if (result.AdultHeroCount - 1 <= 1)
    result.SuccessionPenalty = MarriageScoreTuning.SuccessionPenalty;
```

建议提取纯函数 `CalculateScarcity(int adultHeroCount)` 至现有 `MarriageScoreTuning`；在非持久化 `MarriageScoreBreakdown` 添加 `IsVoluntaryOutflow`、`ScarcityMultiplier`、`OutflowDirectionMultiplier` 等明细，沿用日志本地化。

| N | 原系数 | 新系数 | 女新成本（V=75） | 男新成本（V=75） |
|---:|---:|---:|---:|---:|
| 2 | 2.3333 | 2.3333 | 175.00 | 262.50 |
| 3 | 2.0000 | 2.0000 | 150.00 | 225.00 |
| 4 | 1.8000 | 1.3880 | 104.10 | 156.15 |
| 5 | 1.6667 | 1.0556 | 79.17 | 118.75 |
| 6 | 1.5714 | 0.8551 | 64.13 | 96.20 |
| 8 | 1.4444 | 0.6358 | 47.69 | 71.53 |
| 12 | 1.3077 | 0.4609 | 34.57 | 51.86 |
| 20 | 1.1905 | 0.3617 | 27.13 | 40.69 |

### 3.2 主动离族意愿必须与谈判链条一致

不能只写 `proposal.ProposerClan == evaluatingClan`：反议交换 Proposer/Receiver，同一方向的普通流出成本不应突然改变。建议采用以下明确默认规则（未引入人物性格、长期意愿或新的存档字段）：

- 原始婚约：实际离族源 Clan 如果等于原始 ProposerClan，则主动离族；否则被动离族。
- 反议仅变更礼金或其他不改变 DestinationClan 的条款：延续父提议对离族源 Clan 的主动/被动判定，不因发起方交换改变。
- 反议改变 DestinationClan：这是重新安排成员流向；若新离族源 Clan 是反议发起方，则主动；若是反议接收方，则被动。
- 已接受婚约重估：沿用最终条款对应的上述意愿，不因双方已接受、政治变化、重新加载而改变。不得重抽签、改变礼金或婚后归属。
- 父记录缺失/不合法、链 ID 不一致：保守按被动离族，记录一次诊断，不猜测历史主动意愿，不因此修改或撤销原婚约记录。仅正常业务重估按原规则处理。

推荐实现：在 Campaign-scoped `FamilyProposalService` 增加只读意愿解析 `ResolveVoluntaryOutflowClan(FamilyProposal p)`（返回 Clan 或 null），通过 `ParentProposalId`、`GetProposal`、`ChainId` 和保存的 OriginClan/DestinationClan 查找父提议，校验同一人物与源 Clan；只支持当前最多一次反议，异常循环有限保护。不要在 Model 内直接访问全局 Campaign 或修改存档数据。`ClanHeroValueModel.Evaluate` 增加显式意愿参数/上下文；`ClanMarriageDecisionService` 内统一包装所有 Evaluate 调用，保证 `AddCandidate`、`Respond`、`TryCounter`、所有评分日志、`ShouldContinueAcceptedProposal` 使用同一解析逻辑。旧 `MarriageScore(...)` 无 ProposerClan，调用点全仓搜索；如果保留兼容重载，显式默认被动，不偷偷视为主动。勿留下第二套评分路径。

### 3.3 统一反议预览与正式条款

当前 `CounterPreview` 使用 `CreateMarriageProposalPreview`，会生成独立 ChainId、缺少 ParentProposalId/CounterCount，而且根据 Hero 当前 Clan 建构 Origin。正式 `TryCounter` 又自行构建反议记录。建议在 FamilyProposalService 提取 `CreateCounterProposalPreview(old, responder, direction, payer, gift)`：共享旧 ChainId、ParentProposalId=old.ProposalId、CounterCount=1、保留 OriginClanA/B 与 HeroA/B，交换双方，依据原始源 Clan 和方向计算 DestinationClan，按条款解析付款方/收款方。预览无锁、无发布、无写入；正式 TryCounter 在原有校验后复用该构造路径，最终 ID 保持唯一；只有原有成功事务才能加入 _proposals、转移锁、触发事件。

预览与正式反议的评分条款必须一致；不能为了统一构造绕过 `CanRespond`、`CanProposeMarriage`、同条款拒绝、余额校验、CounterLimit。搜索阶段不更改旧提议。政治重估仍用 CommitmentThreshold=-20，不用新接受概率。

## 4. Phase B — 连续三分支概率（MF-01、MF-06、MF-07）

### 4.1 统一概率模型

现有 `CalculateAcceptanceChance(score)` 为 0.5×clamp(score/300,0,1)^2，`Respond` 仅在零概率尝试反议；新规则废止旧指数方案（不先改0.37再叠加新模型）。`MaxPositiveScore=300` 仍由六项常量推导；不要降低理论满分。

以下为**建议初始控制点**，具体数值未经新模型实机校准，提供可施工的默认版本：

| 净收益分数 | 接受 | 反议 | 拒绝（余量） |
|---:|---:|---:|---:|
| -100 | 0% | 0% | 100% |
| -50 | 0% | 1% | 99% |
| 0 | 0% | 5% | 95% |
| 30 | 22% | 20% | 58% |
| 60 | 35% | 35% | 30% |
| 100 | 45% | 45% | 10% |
| 150 | 65% | 30% | 5% |
| 200 | 80% | 17% | 3% |
| 250 | 90% | 8% | 2% |
| 300 | 95% | 4% | 1% |

其中负分控制点是本任务书新增默认值；不是已经确定的用户数值。低于-100或高于300时分别取端点；NaN/Infinity 或无法得到有效评分直接拒绝并诊断。每段线性插值 `t=(s-lo.Score)/(hi.Score-lo.Score)`；A和C分别插值，R=1-A-C。分数连续变化，无突跳；非正收益不直接接受，仍允许概率较低的反议。负分不代表硬性非法；硬性非法由既有校验优先失效。已反议时 C=0，R+=原C，A保持原值。无第二次反议。

### 4.2 XML 配置与代码权威

修改 `MarriageAIConfig.xml` 和 `MarriageAiConfig.cs`。建议新单一配置项如下（例，最终保持一个权威来源）：

```xml
<DecisionCurve>
  <Point score="-100" accept="0.00" counter="0.00" />
  <Point score="-50" accept="0.00" counter="0.01" />
  <Point score="0" accept="0.00" counter="0.05" />
  <Point score="30" accept="0.22" counter="0.20" />
  <Point score="60" accept="0.35" counter="0.35" />
  <Point score="100" accept="0.45" counter="0.45" />
  <Point score="150" accept="0.65" counter="0.30" />
  <Point score="200" accept="0.80" counter="0.17" />
  <Point score="250" accept="0.90" counter="0.08" />
  <Point score="300" accept="0.95" counter="0.04" />
</DecisionCurve>
```

读取一次、InvariantCulture、不可变点列表；验证分数严格递增、包含0及300端点（末点与理论上限一致）、有限数、概率0～1、A+C≤1、A非递减、R非递增、C单峰且高低两端低于峰值、非正分A=0。全部曲线作为一个配置单元校验，非法或缺失回退完整内置默认曲线并一次警告，不能部分混合产生非单调曲线。旧 XML 的 AcceptanceMaxChance/AcceptanceExponent 停用并移除默认 XML；旧文件可加载，新模型缺失时走新默认曲线，不将旧0.5/2.0与新曲线叠加；调整 `HasInvalidValues` 不再要求旧字段。保留其他配置及默认回退规则。

`MarriageScoreTuning` 增加纯决策概率 API，例如 `CalculateDecisionProbabilities(float score, bool canCounter)` 返回非持久化只读值 `{Accept,Counter,Reject}`，`CalculateAcceptanceChance` 若仍有调用仅委托新模型 Accept，不保留第二套旧算法。概率限制与输入合法性阈值不是同一概念：`AcceptanceThreshold=0` 仍可用于AI主动候选本方不亏、反议新条款筛选；`CounterOtherClanFloor=-20`、`CommitmentThreshold=-20` 不因重写答复概率而删除。全仓核对其他调用。

### 4.3 Respond 执行顺序

1. 玩家接收方直接返回，不代玩家做决定；`CanRespond` 硬校验不通过走已有 `TryInvalidateActive`（无主观配对冷却）。
2. 使用 Phase A 统一评分；计算三种有效概率；`CounterCount==0` 才有反议概率。
3. 取一次 `MBRandom.RandomFloat`。示意：

```csharp
float roll = MBRandom.RandomFloat;
if (roll < p.Accept) { /* TryAccept */ }
else if (roll < p.Accept + p.Counter) { /* TryCounter; no viable terms => reject */ }
else { /* TryReject */ }
```

4. 抽中反议才搜索候选；若没有合法、改善且对方有接受空间的条款，`TryReject(...NoViableCounterProposal)`，不得重抽或再尝试接受。若服务已使婚约失效，不得再把它写为拒绝；服务调用失败不记录成功，检查真实状态。每条原始提议只进行一次正式决策抽签；反议是下一轮独立的正式答复，且不可继续反议。避免重复 Tick 或同状态服务失败导致重复随机判定；若发生可重试业务失败，记录并沿用已确定决策，不为同一待答复记录多次抽签。优先检查现有调用与失败路径，若需新增持久化决策状态才能满足异常重载不重抽，先说明实际场景及兼容方案，不默认扩建状态机。
5. 随机拒绝不应标记为 `LowClanBenefit`（尤其高正分）。建议追加 `AiDeclined` 到 `FamilyReasonCode` 末尾（不重排旧值），用对应本地化“对方家族未接受本次提议”；非正分拒绝可保留 LowClanBenefit；无可行反议使用已有 NoViableCounterProposal。业务原因与统计结果区分。
6. `TryCounter` 中保留现有有界条款枚举和资金限制，修正 Phase A 预览；排除完全相同条款；要求本方新分≥0且严格改善相对原条款（容差如0.001），NPC对方有正接受概率；玩家不按NPC概率拒绝合法新条款，保留现有对方分数合理下限作为AI自身策略。允许同方向零礼金方案（当前只有反向零礼金），否则无法取消原礼金。反议候选按 Phase C 类似预期收益选择，或沿用双方总分排序并明确差异；本任务建议统一预期收益以免重复出现只利己方案。不新增反议次数或更改冷却。

### 4.4 20%目标的校准

统计口径：`直接接受率 = AI→AI 已正式答复且直接接受的原始提议数 / AI→AI 原始提议实际正式答复数`。另外分别报告原始反议率、直接拒绝率、反议失败回落拒绝率（不重复计入分母）、谈判链最终接受率（每 ChainId 一次）、已接受链最终成婚率；排除玩家参与、硬非法未抽签、未到答复的记录；反议轮单列。用户说成功率约20%，先按此前讨论的原始提议接受率校准，谈判链最终成功率若偏离过大另行报告，不擅自把两者混用。

先固定初始曲线与新评分、排序运行；收集至少100～200次独立正式答复，尽量更多并按多存档/游戏日区间分组。记录实际评分分布、预测概率均值、实际比例及样本数；目标约20%，15%～25%作为初步观察范围（非用户强制验收区间）。如评分整体提高，调节低中分段控制点并保持单调和单峰，不调主动求婚频率来伪造接受率，不按历史拒绝次数强制保底。没有新实机日志不得声称已达20%。

## 5. Phase C — 双方利益候选排序（MF-04、MF-05）

当前 `ConsiderClan` 原排序 Tier→ProposerScore→ReceiverAcceptanceChance→GiftAmount；`AddCandidate` 仅过滤本方负分及NPC零接受概率。保留文化/王国层级 1同国同文化、2异国同文化、3同国异文化（受政策许可）；保留现有 own 3/target clan 8/other 3 有界搜索、礼金档位与余额约束，不扩大枚举范围。

NPC接收方：同层主排序采用 `ExpectedBenefit=ProposerScore * P_accept(receiverScore)`，再按接收方接受概率、本方收益、礼金金额升序，最后可用稳定原有候选顺序或稳定 ID 作确定性并列处理。使用统一新模型的直接接受概率（暂不估算反议未来条款收益）。例如 180×5%=9，125×25%=31.25，105×35%=36.75，应选择后者（假设概率，仅说明排序）。

过滤默认：本方收益必须≥0；NPC接收方直接接受概率为0仍不作为普通主动候选（保留现有过滤语义，避免纯反议投机），但玩家手动提出的低分请求仍可进入三分支答复。反议概率不加到直接接受概率上。玩家接收候选不以NPC概率过滤；排序可使用相同曲线作为偏好估计（不是替玩家抽签），为避免所有零概率方案同值并列，随后使用 ReceiverScore 与 ProposerScore 等稳定评分；仅玩家能决定。不得修改玩家 UI 的手动礼金额度或合法性。

`OfferCandidate` 增加 ExpectedBenefit 或排序计算属性；`AddCandidate` 预览评分使用 Phase A 解析。`TryCreateMarriageProposal` 仍按候选顺序逐个尝试，成功才返回及开启冷却；失败不得记录创建成功。`TryCounter` 与 Phase B 保持一致，避免只利己反议。礼金收益公式、付款财政压力系数及资金30%限制均不变。不要在候选枚举中随机抽签。

## 6. Phase D1 — 地图事件关闭按钮（MF-03.1）

当前 `FamilyProposalMapNotificationVM` 构造函数 `ForceInspection=true`。仓库 `code/GUI/SandBox/GUI/Prefabs/Map/MapNotificationItem.xml` 绑定 `IsInspectionForced=@ForceInspection`、`Command.OnRemove=ExecuteRemove`，已有 `NotificationRemoveButtonWidget` 和 X 文本；同仓库 `ModifiedPolitics/.../GovernorAppointmentMapNotificationVM.cs` 使用 false。先将该属性改为 false，不覆盖本体 XML 或贴图。实际客户端 `MapNotificationItemWidget`、`MapNotificationItemBaseVM.ExecuteRemove` 逻辑须用本地游戏 DLL/反编译确认（远程仓库未见完整 C# 实现），确保仅关闭通知，不调用 TryReject/TryWithdraw，不改变锁/冷却/期限。

来件 X 关闭后待答复婚约仍存在，`PlayerNotified` 保持 true，重新加载不重复弹出已关闭通知。现有 `FamilyPlayerInteractionService.ShowFamilyActions` 提供 inbox 选项，`ShowInbox` 从 GetIncoming 读取 IsActive 列表；实际 UI 入口需查 Clan 页按钮与 FamilyManagementVM 的绑定，保证玩家有可见的待答复入口并只允许 PendingResponse 的来件进行处理。若现有入口不可达，最小增加“待处理提议”入口，复用现有收件箱，不另建婚约数据或大改页面。不要因为玩家关闭地图事件而自动拒绝。打开后关闭处理界面则保留通知；接受/拒绝/反议/超时/失效后原通知自动移除；原来的 OnProposalResolved 订阅和 OnFinalize 解绑保留并验证一次性。

## 7. Phase D2 — 处理界面暂停战役时间（MF-03.2）

当前调用链：`FamilyProposalMapNotificationVM.InspectProposal`→`FamilyPlayerInteractionService.ShowIncomingProposal`→`MarriageProposalScreenService.Open`→`FamilyActionScreenHost.Open`。Host 只创建 GauntletLayer、限制输入、加载 Movie；输入限制不等于战役暂停。

本体源码 `code/CampaignEvents.cs` 2876～2900：人物弹窗打开时保存 TimeControlMode→Stop→SetTimeControlModeLock(true)，关闭时解锁→恢复原状态。`code/Campaign.cs` 459～477 中 TimeControlModeLock 为公开只读属性，锁定时 setter 不允许写入；2293～2296 为 SetTimeControlModeLock。此为本体人物弹窗参考，不是已核实的原版婚姻弹窗宿主完整调用链；开发时先反编译实际版本的原版 MarriageOfferPopup host 与 MapNotification VM，若有安全原生暂停机制优先复用其语义，不触发无关人物肖像事件。

### 7.1 Host 最小修改

给 `FamilyActionScreenHost.Open` 最后增加可选 `bool pauseCampaignTime=false`，仅 `MarriageProposalScreenService.Open` 及新结果通知详情入口传 true；其他主动联姻/收养界面维持现有行为。暂停必须在界面可交互之前完成，不因地图通知出现而暂停。管理一次打开的临时 pause lease（与当前 host/layer 绑定，关闭即清理，不成为跨战役全局缓存）。

- 已有 layer 或无 TopScreen：Open 失败，不接管现有时间锁。
- 取得当前 Campaign 后，若 TimeControlModeLock=false，保存原模式；设置 Stop，确认 Stop 后设置锁；记录本窗口拥有锁。若已锁且处于 Stop（或经本地 API 核实的暂停模式），借用现有暂停，不取得所有权、不解锁；若已锁却继续运行，不能抢锁，安全拒绝打开并给出可本地化提示，待条件允许再开。
- `Close` 清空宿主引用、解除事件订阅、释放 focus/movie/layer/sprite/VM；以 try/finally 确保任一释放异常都不漏掉本窗口时间锁，重复 Close 幂等；只有实际取得的锁且仍是同一 Campaign 才解除，并恢复保存模式。别在战役切换时对新 Campaign 恢复旧时间。
- 锁 API 只是 bool，没有所有权 token；不允许凭 bool 断言能识别其他模块在窗口期间新加的锁。开工时查原版嵌套弹窗生命周期和其他 Mod 用法，限制本窗口与其他 modal 的并发，尽可能使用原生宿主提供的暂停机制。无法证明共享 bool 锁在嵌套场景安全时标为 BLOCKED，不宣称已支持所有并发。
- `Open` 异常调用 Close 仍须释放暂停 lease；`OnScreenPopped`、VM `ExecuteClose`、接受/拒绝/反议成功 `Finish`、百科 `OpenHero`、地图离开、战役卸载均查清释放路径；失败仍留界面时继续暂停，失效或关闭时释放。
- 如果 `ShowIncomingProposal` 图形界面打开失败后转入 MultiSelectionInquiry（当前 `ShowMultiSelectionInquiry(...,false,false)`），必须验证本体 inquiry 是否自动暂停并锁定，若不支持则使用其真实 pause 参数/生命周期或安全返回通知并提示，禁止以未暂停的 fallback 继续处理联姻。若 fallback 支持反议子窗口，暂停覆盖完整子流程。结果通知若复用原生 Inquiry，使用同一验证原则。

## 8. Phase D3 — AI接受/拒绝结果地图通知（MF-03.3）

### 8.1 业务事件源

`FamilyProposalService.TryAccept` 先置 AcceptedPendingExecution 再触发 ProposalResolved；`TryReject` 先置 Rejected、ReasonCode、配对冷却、释放链锁再触发；`TryCounter` 原记录 SupersededByCounter 后触发 Resolved，替代记录触发 Created；`TryExpirePending` 触发 Resolved；`Invalidate` 同时触发 Invalidated 和 Resolved。`FamilyCampaignBehavior.BuildServices` 目前只订阅 Created→Publish、Invalidated→PublishInvalidated。新增 Resolved→PublishResolved；保留来件原订阅，不修改婚约服务执行顺序。通知回调必须隔离自身异常，避免已经提交的业务被UI异常误报为失败或导致后续订阅者不执行。

| 事件 | 地图通知 |
|---|---|
| AI向玩家原始提议/反议 | 继续用现有来件通知；原提议被反议替代不发额外拒绝结果 |
| 玩家是当前 proposer，AI receiver 接受（含玩家反议被接受） | 新增“联姻提议已接受”；明确待执行，不能声称已成婚 |
| 玩家是当前 proposer，AI receiver 拒绝 | 新增“联姻提议被拒绝”，使用真实 ReasonCode |
| 玩家相关客观失效 | 建议新增结果地图通知；原文字提示和新地图通知避免重复刷屏，收养仍走既有逻辑 |
| 玩家发起且真的进入 TimedOut | 建议新增“联姻提议超时”；当前 AI 正常在期限到时 Respond，不将其误标超时；玩家收到来件逾期按原逻辑处理，可提示玩家来件失效但不算AI拒绝 |
| 玩家主动接受、拒绝、撤回 | 不额外发本次AI答复结果 |
| AI→AI、收养 | 不新增联姻结果地图通知 |
| Completed / ExecutionFailure / PaymentReconcileRequired | 本任务不新增成婚或执行异常地图事件；保留现有执行日志/提示 |

AcceptedPendingExecution 后可能客观失效，因此同一 ProposalId 可先有 Accepted 再有 Invalidated 两种不同的结果通知。不得使用当前 Status 自动移除已发布历史结果；通知文案描述当时发生的结果，另可只读显示当前婚约状态。

### 8.2 独立通知数据与 VM

新增 `MF/UI/Notifications/FamilyProposalResultMapNotification.cs`（继承 InformationData，模式参考现有来件），以及 `FamilyProposalResultMapNotificationVM.cs`（继承 MapNotificationItemBaseVM，复用 `NotificationIdentifier="marriage"`、原生音效/资源，`ForceInspection=false`，不得在 ManualRefreshRelevantStatus 中因婚约终态自动移除）。持久化 ProposalId、发布时结果状态、发布时 ReasonCode；详情使用原提议双方/人物/礼金/方向，缺失记录时展示安全的简短结果说明、允许关闭，不崩溃。标题、描述按结果区分；不是再次接受/拒绝的操作窗口。点击打开只读详情（可用现有原生 Inquiry/自定义轻量结果界面，要求打开暂停，关闭释放时间；无需重做婚姻人物画面）。玩家确认阅读时移除结果通知，X直接移除；没有结果通知按钮修改婚约。

`FamilyProposalNotificationPatch.Register` 在已有 `RegisterMapNotificationType` 之后注册新数据/VM对。核实原版基类构造与类型注册签名、实例生命周期和 `ExecuteRemove` 方法；不为新通知替换整个 MapNotificationVM，也不覆写原版通用 XML。

### 8.3 幂等与旧存档

现有 `FamilyProposal.PlayerNotified` 仅为来件已发布标记，不可复用结果标记。建议追加 `[SaveableProperty(25)] public int PlayerResultNotificationFlags {get;set;}`（开工检查未被占用），位定义 Accepted=1、Rejected=2、TimedOut=4、Invalidated=8；默认0。仅在本次成功发布结果时置对应位；UI关闭不清位。新结果通知 `SaveableRootClass(10)` + `FamilySaveDefiner.AddClassDefinition(...,10)`（当前类9已占用，开工确认10未被使用）；通知字段 ID从1开始，原 Proposal 1～24、Definer ID 2026102401、原类/枚举 ID保持不变。若新类使用现有状态枚举无需新增枚举注册；禁止重排枚举。同步本体 InformationData 派生类型的存档约定（本地验证）。

避免 `OnSessionLaunched` 对历史数百条已结束婚约补发结果。默认仅在升级后实际发生的业务事件发布新结果；现有保存的地图通知由本体恢复，flags 保证不重复。对于发布时 manager 不可用的情况，建立 Campaign-scoped 有界待发布机制：若需要跨存档保证不丢失结果，建议追加独立 PendingResultNotificationFlags（property26，核查编号），事件发生时先标记待发布、成功发布后移除 pending 并置已发布位；会话启动/安全 Tick 仅补发明确有 pending 位的记录，而不是扫描旧终态并补发。旧存档两字段默认0，绝不自动补发旧历史。若采用其他实现须保证同等幂等性，不能同时维护两套队列权威。异常若 NewMapNoticeAdded 在部分成功后抛出，开发时核实本体入队顺序/可查询通知列表并按 proposalId+结果去重，不能声称原子性已证明。

`BuildServices` 重建时保证旧事件订阅解绑或旧服务不可达，新服务恰好一次订阅。失效事件同时触发两个回调，结果发布只有一个权威入口（`PublishResolved`）；旧 `PublishInvalidated` 仅保留需要的文字提示，严禁双发地图结果。所有发布异常在通知层记录，不回滚已完成业务。

### 8.4 本地化

在 `MF/ModuleData/submodule_strings.xml`（如实际文件名不同以仓库为准）及 `MF/ModuleData/Languages/CNs/modifiedfamily-zho-CN.xml` 添加新 key，检查实际 base strings 位置与语言注册；英文源码使用 TextObject、中文翻译；占位符完全一致。新 AiDeclined 原因同步 `FamilyLogText.ReasonText` 的实际映射。示例：联姻提议已接受 / 联姻提议被拒绝 / 联姻提议已失效 / 联姻提议已超时；AI随机拒绝用“对方家族未接受本次提议”，不谎称收益不足。接受文案“对方接受了联姻提议，婚约等待执行”。

## 9. 诊断及测试

沿用 FamilyLogText、MarriageDiagnosticTrace、FamilyDiagnostics。新评分明细记录 N、scarcity、direction multiplier、voluntary/source、BaseHeroValue、流入/流出、继承、双方分数；候选最终选中记录 Tier、ProposerScore、ReceiverScore、A/C/R、ExpectedBenefit、礼金/付款方与资金约束、实际 ProposalId。正式 AI 回复记录 ProposalId、ChainId、ParentProposalId、CounterCount、双方名称、玩家参与、score、A/C/R、原始一次 roll、抽中分支、最终服务结果/ReasonCode/状态。不要为输出日志再次抽签；记录被选择方案及汇总而不是所有枚举候选。遵循根 AGENTS 日志语言、名称、等级、刷屏限制；高频诊断可受现有开关，AI→AI 校准所需样本必须可在诊断模式下获取，若当前 Trace 仅对玩家相关有效，新增有界汇总或采样输出，不让玩家屏幕刷屏。以 ChainId 区分原始和反议，统计硬非法不进入抽签的数量。

| ID | 验收场景 | 预期 |
|---|---|---|
| A01 | N=1/2/3，V=75，主动/被动 | 被动旧值不变；主动普通成本0；N≤2仍有80继承惩罚 |
| A02 | N=4/5/6/8/12/20 | 新表数值容差0.02；人数越多成本越低；N大时趋近0.3 |
| A03 | 同一婚约双方 | 实际迁入方仅计对方价值；离族方仅计真实流出；男1.5、女1 |
| A04 | 领袖主动离族、血亲、已婚、死亡等 | 仍由硬校验禁止，高礼金不能突破 |
| A05 | 原提议主动离族，反议只改礼金 | 预览/正式反议及履约重估的主动意愿一致 |
| A06 | 反议改变归属，反议发起方送出自己/要求对方送出 | 前者主动，后者被动；原始 Clan 不变 |
| A07 | 旧存档反议，父记录缺失 | 正常父链正确解析；缺失保守被动并记录，不写坏数据 |
| B01 | 每个控制点、相邻点中值、超范围、NaN/Inf | 与表一致；A/C/R范围0～1、和为1；高分主要接受，反议单峰，无突跳 |
| B02 | XML缺失/旧格式/非法点/重复点/非单调 | 整条新默认曲线生效；旧参数不叠加；其他设置正常 |
| B03 | 固定roll覆盖三分支边界 | 一次随机判定；精确边界使用 <；不重抽 |
| B04 | 已反议、无可行反议、反议服务失败 | 无二次反议；C并入R；无可行回落拒绝；客观失效不覆写 |
| B05 | 玩家接收方、硬非法、政治重估 | 玩家不抽签；非法走Invalidated无主观冷却；已接受重估仍用-20且不抽签 |
| C01 | 相同Tier候选不同双方收益 | 选择最大预期收益而非本方最高收益；同层稳定排序 |
| C02 | 跨Tier、候选服务失败、NPC零A、玩家零A | 保留政治优先；失败尝试下个；NPC过滤；玩家不按NPC概率过滤 |
| C03 | 各礼金档、余额30%边界 | 保留现有可支付性，玩家手动金额不受新AI限制 |
| D01 | AI来件、反议，鼠标悬停右侧事件 | X按本体规则出现，关闭仅移除通知；锁/期限不变 |
| D02 | X关闭后收件箱、退出重载 | 仍可重新处理；已关闭通知不反复弹；只待答复可处理 |
| D03 | 地图正常/加速/暂停时点击通知 | 打开前记状态；处理期间 CampaignTime.Now 不前进，底层操作不恢复时间；关闭恢复原模式 |
| D04 | 接受/拒绝/反议、关闭、百科、加载失败、异常、ScreenPop、卸载 | 不遗留本窗口时间锁或 Layer；其他模块预有锁不解除 |
| D05 | 已锁运行状态、嵌套弹窗、fallback Inquiry | 不抢锁；按§7安全降级/原生暂停；禁止永久暂停或后台流逝 |
| D06 | AI接受/拒绝玩家原始提议或玩家反议 | 恰好一条对应结果地图通知；接受显示待执行；真实拒绝原因；结果点击暂停 |
| D07 | AI反议、玩家主动答复、AI→AI、收养 | 原通知正确替换；无多余结果；收养不受影响 |
| D08 | 玩家相关失效（两事件）、超时、接受后失效 | 单种结果只发一次；接受和后续失效可分别发；不冒充已成婚 |
| D09 | 保存/加载未读及已读通知、旧存档历史 | 未读可恢复、已读不重发；旧历史不补发；新旧存档无重复锁/扣款 |
| D10 | 发布异常、manager暂不可用、服务重建 | 业务成功不回滚；pending恢复有界且不重复；事件一次订阅 |
| E01 | 至少100～200次AI→AI原始正式答复 | 报告原始接受率、反议率、拒绝率、链最终接受率、实际成婚率及评分/礼金/方向分布；约20%目标，偏离须说明 |
| E02 | 实际婚礼、礼金、政治变化、锁/冷却 | 原事务与30天执行不变，不能重复结婚或扣钱 |

## 10. 编译、阶段门禁、文档及交付

1. Phase A→B→C依赖顺序；D独立，可单独施工。每阶段先核对调用链再修改，执行针对性测试、检查 `git diff --check` 和实际 diff；需要本地程序集的 D 项不可用时报告 BLOCKED，不用推测 API 代替确认。
2. 工程为 `ModifiedFamily/ModifiedFamily/ModifiedFamily.csproj`、`netstandard2.0`；现有本体 DLL HintPath 指向 Windows 安装路径。编译例：`dotnet build .\ModifiedFamily\ModifiedFamily\ModifiedFamily.csproj -c Release --no-restore`。不要在无本体引用时虚报 PASS，不引入其他 DLL/框架依赖解决缺失。若新增 prefab，按现有 Content Include + CopyToOutputDirectory 注册，并确认 ModuleData XML 复制。使用现有脚本部署仅在用户授权后执行：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Deploy-Mods.ps1 -Build -Configuration Release -Modules ModifiedFamily`（在仓库根目录，检查脚本实际参数；无 -Build 不会编译）。本任务要求方案而非部署。
3. 在 `file/` 下新增本轮统一设计与实现任务书，明确覆盖11-14旧概率条款及相关旧评分/通知条款；保留历史文档，不无故改写。若实际实现与本任务默认规则不同，写明理由及测试证据，不宣称已确认的用户要求被改变。
4. 最终提交给用户的施工报告：开工/结束 HEAD、git status、实际修改文件和方法、最终配置、新概率曲线、主动离族判定、存档 ID、UI时间控制行为、实际 PASS/FAIL/BLOCKED 项、实机接受率统计和未验证风险。不能用编译成功代替实机通过。未经授权不 commit/push/deploy。

### 本方案编写时的证据与限制

- 已经通过 GitHub 读取 main 上的实际相关源码、根目录 AGENTS.md、最新设计文档并查询全部8份 ModifiedFamily 设计文档的最后提交时间。基线链接：https://github.com/GuihuaZhou/bannerlord/commit/36b2b4c13f2bd2f49bcd187f31c9a979cb318999 。
- 用户已确认 MF-08 新公式，确认概率变化方向、20%目标、候选需考虑双方、主动入赘不扣普通流失、地图关闭与暂停需求。其他具体初始参数和默认实现选择已在§1注明，未伪称用户定稿。
- 当前未重新读取原始运行日志，未使用旧日志样本宣称新模型能达到20%。需改后实际采集。
- 未运行本地游戏、未编译、未验证原生 Widget C# 与原版婚姻弹窗完整宿主行为、未修改 Git。地图关闭与暂停的具体 API 已从仓库 XML、现有 Mod 和 Campaign/CampaignEvents 核对，但实际交互与嵌套锁安全性必须由 Codex 本地验证。
