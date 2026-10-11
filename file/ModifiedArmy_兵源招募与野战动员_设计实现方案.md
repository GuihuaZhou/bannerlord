# ModifiedArmy：兵源、驻军招募与 AI 野战动员设计实现方案

**状态：已实现，待游戏内长周期验证。** 适用范围：`ModifiedArmy`，涉及与 `ModifiedPolitics` 官职加成的兼容；本轮未修改大元帅目标评分。

## 实现记录（2026-10-11）

- 新增驻军模型，0～3 级兵营的每日基础配额为 1～4，并保留军事长官的后置加成。
- AI 与玩家驻军共用自动招募开关、食物和围攻/战斗安全条件；玩家驻军使用实际金库，不受 AI 30,000 保底和 10% 日预算限制。
- 职业兵和可招降俘虏进入同一招募评估计划，共用每日配额，不再固定由职业兵先占满。
- AI WarParty 俘虏分为部队保留、同 Clan 监狱真实移交和出售；英雄俘虏恢复关押，玩家监狱不做 AI 自动售俘或周度整编。
- 驻军交换理想权重乘 0.40，保留城镇 125/城堡 75 的原版最低保护；部队未达工资、粮食和编制共同确定的合理目标时不再捐兵。
- `AiRecruitmentBehavior` 会把同 Clan 驻军补员、职业兵和俘虏移交合并为一次近距离入城任务，不会改派军团成员或战斗中部队。
- 新建 AI WarParty 优先在同 Clan 未被围攻且健康驻军最多的要塞附近出生，无候选时保留原版回退。
- 驻军日变化展示改为职业兵库存、已就绪俘虏和统一评估模型的可执行预测。

## 一、目标与原则

- 使职业兵池、俘虏、驻军、WarParty 构成真实的兵力流转链，不凭空补兵。
- **职业兵池生产速率及容量不变**；仍由现有 XML、文化与兵营等级控制。
- 驻军自动招募对 **AI 和玩家 Clan 的城镇、城堡均有效**；以本体开关为唯一启停控制，不恢复本体志愿兵自动招募。
- AI 与驻军仍为**双向兵力交换**，但分配优先保障 AI WarParty 的合理编制，避免反复取兵、还兵。
- 保留驻军人数上限、工资上限、现有真实招募费用及兵种编制；不调整 `DefaultTargetScoreCalculatingModel`。

## 二、设计规则

### 2.1 职业兵源和俘虏

1. 职业兵池的每日生产速率、文化修正、兵种模板、最大库存 30 等均不改。
2. AI WarParty 进入友方城镇或城堡时，将普通俘虏分为：**自身保留、移交本 Clan 监狱、出售**。
   - 自身仍需招降、符合自身编制的有价值俘虏：优先留在 WarParty。
   - 自身不需要、但本 Clan 驻军适合招降且监狱可容纳的俘虏：真实移交到本 Clan 定居点 `PrisonRoster`。
   - 低价值、不符合双方需求或超出容量的俘虏：按现有出售行为变现。
   - 敌方 Hero 俘虏：恢复本体关押/移交流程；玩家角色保护按原版处理。
3. 区分 **转移** 与 **出售**：`SellPrisonersAction` 只用于出售；英雄俘虏可沿用本体 `TransferPrisonerAction`；普通士兵的批量移交使用经验证的名册转移实现，保留伤兵等数据，保证源、目标人数守恒。
4. AI 定居点监狱不再仅因驻军“最低兵种/质量比例已达标”就出售高级俘虏。按照**价值、编制上限、监狱容量与预计招降用途**决定保留；优先出售低价值和明显无用的俘虏。保留限额与策略独立于自动招募开关，避免开关关闭即清空有用俘虏。
5. 玩家监狱继续尊重玩家管理权：不得因为扩大玩家驻军自动招募而启动 AI 的自动大规模售俘或周度整编；原有玩家监狱处理另行保持兼容。

### 2.2 AI/玩家驻军自动招募

1. 继续通过 `GarrisonRecruitmentPatch` 禁用原版 `GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange()`，防止从原版 notable 志愿兵槽重复招兵；本体其他非自动招募驻军变化与 XP 机制不在本轮替换范围。
2. 新增 `NewSettlementGarrisonModel : DefaultSettlementGarrisonModel`，只覆盖 `GetMaximumDailyAutoRecruitmentCount(Town)`，其余继承：

   `BaseDailyLimit = 1 + BarracksLevel`，其中 `BarracksLevel` 为对应 Town/Castle 兵营已建等级（0–3）。

   | 兵营等级 | 0 | 1 | 2 | 3 |
   |---|---:|---:|---:|---:|
   | 每日基础招募上限 | 1 | 2 | 3 | 4 |

3. 本体 `Town.GarrisonAutoRecruitmentIsEnabled` **对 AI 与玩家均生效**：关闭时，均不得自动从职业兵池招募或将俘虏招降进驻军；开启后才按既有条件执行。建议保持本体安全前置条件：未在围攻/战斗中、`Town.FoodChange > 0`。是否关闭并不影响已有士兵的 Party↔驻军调拨。
4. 两类来源**共用一个每日名额**：`ProfessionalRecruited + PrisonersRecruited <= EffectiveDailyLimit`。有效上限再受空余人数、兵种编制、实际库存、可招降进度、工资和资金约束。不能从兵源池、监狱重复消费同一人。
5. 不再固定“职业兵优先吃完全部名额、俘虏只能捡剩余”，改为由现有招募评估模型结合编制需求、兵源可用性决定分配；禁止因俘虏未达到最低比例缺口而自动判定为无用。
6. 保留 `ModifiedPolitics` 军事长官对每日招募上限的现有加成，计算顺序：基础 `1+等级` → 官职加成 → 编制/工资/库存等实际限制；日志与 UI 采用一致口径。
7. **玩家财政需单独适配**：当前 AI `ClanRecruitmentBudgetManager` 至少保留 30,000 金币，并仅使用安全预算的一部分；如果直接让玩家驻军进入该逻辑，可能开了开关仍不招。玩家驻军应按实际可支配金币、购买费用、驻军工资上限及当日已发生支付约束，不套用 AI 的 30,000 金币保底/10% 日预算。沿用 `AutoRecruitmentExpenses` 结算，不重复扣费。仍遵守编制与库存限制。
8. 玩家驻军自动招募启用后 **不自动启用** `NormalizeGarrison()` 的 AI 周度裁兵，也不应因 AI 监狱清理规则失去玩家俘虏。
9. **UI 预测值同步**：本体 `GarrisonRecruitmentCampaignBehavior.GetGarrisonChangeExplainedNumber()` 仍按 notable 志愿兵列表展示自动补员预测，与已改用职业兵池/俘虏的实际数量可能不一致；应更新相关展示或明确采用 Mod 的有效配额与实际库存，不让玩家看到错误的“预计每天招募”数字。

### 2.3 Party 与驻军双向交换：Party 优先

1. 保留本体的 Party↔Garrison 双向交易、兵种名册和工资限制；AI Party 仍仅能在具备权限的定居点取兵（本 Clan 优先；新征服的特殊情况尊重本体）。不自动控制玩家亲自率领的主 Party。
2. 调低本体 `GarrisonTroopsCampaignBehavior.CalculateSettlementIdealPartySizeWithEffects()` 的计算结果，建议试验参数：`ModifiedIdealGarrison = round(VanillaIdealGarrison × 0.40)`。**这只影响交换中的理想驻军权重，不改驻军实际人数上限、工资上限和每天招募上限。**
3. 原版 `CollectPartyGarrisonTransferData()` 存在驻军最低分配目标：城镇 125、城堡 75。首轮保留，记入测试日志；不要误认为乘以 0.4 就能抽取最低目标以下的驻军。
4. 必须同时处理本体捐兵分支：原版 `SettlementIdealPartySize` 字段有时仍采用未打折的容量参考值。**Party 低于本体考虑编制、工资与粮食后的合理目标时，禁止 Party→驻军自动捐兵**；达到合理目标后才允许把多余兵力交驻军。避免再次进城时回捐。
5. 独立 Party 与 Army Leader 均验证；不得擅自挪用其他 Clan 驻军。遵守伤兵、Party 人数和工资约束，转移操作保持真实兵力守恒。

### 2.4 AI 主动访问本 Clan 定居点

1. 不新建独立行军 AI。扩展已有 `AiRecruitmentBehavior`：综合评估定居点的职业兵/封邑兵、可调拨驻军、有价值俘虏移交及距离成本，将**一次入城多用途**作为优先方案。
2. 可用 `RecruitSource.Garrison`（或更通用的 SettlementVisitPurpose 标识）来评估驻军补兵，但对“移交俘虏”的动机不能完全依赖缺兵 `NeedScore`，因为满编 Party 也可能需要回城。
3. 明确任务保持逻辑：已有目标未到达时不反复重派；已到达后由定居点进入事件完成移交、交换、招募；失败后允许回退其他兵源。避免新的目的地被原随机概率或当天其他 AI 决策覆盖。
4. AI 正在 Army、围城、被追击或承担重要任务时，不为了少量俘虏中断军事行动。不会为了无价值或少量俘虏跨地图长途运输。
5. 验证 `AiVisitSettlementBehavior` 本体已有“携俘虏访问要塞”评分，但 `num36 / num7` 使用整数除法，少于容量时经常没有加成；我们的主动访问以 Mod 决策为准，不依赖这一项。

### 2.5 新建 AI WarParty

1. 在本体 `HeroSpawnCampaignBehavior.SpawnLordParty()` 的出生地点选择处：优先本 Clan 未被围攻、可抵达的 Town/Castle，按**实际健康驻军或可调人数**选择（按照已确认需求，默认实际驻军最多，遇并列再比较可调兵数和距离）。
2. 无本 Clan 可用要塞时回退本体 `SettlementHelper.GetBestSettlementToSpawnAround()`。
3. 保留 XML 中已降低的初始兵力，不额外凭空刷兵；注意本体新游戏初始化还可能存在额外补兵分支，与普通存档中新建 Party 区分测试。
4. Party 出生在要塞**附近而非内部**；须让现有 `AiRecruitmentBehavior` 在具备补员条件时安排首访，不保证仅靠出生点改变就会取到驻军。

## 三、修改文件与职责

| 文件 | 类型 | 职责 |
|---|---|---|
| `ModifiedArmy/Garrison/Models/NewSettlementGarrisonModel.cs` | 新增 | 继承本体，返回 `1+BarracksLevel`；仅更改每日招募上限 |
| `ModifiedArmy/Main.cs` | 修改 | 注册新 Model |
| `ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs` | 修改 | AI/玩家同用开关和配额，分离监狱整理与自动招募，优化俘虏留存/来源配额 |
| `ModifiedArmy/Recruitment/Models/RecruitmentBudget.cs` 及必要财务配套 | 修改 | 玩家驻军的实际可用资金路径；AI 继续现有保守预算 |
| `ModifiedArmy/Recruitment/Patches/GarrisonRecruitmentPatch.cs` | 保留 | 继续阻止原版 notable 招募，不恢复原版自动招募 |
| 驻军日变化说明 UI 的对应补丁（若有展示） | 必要时修改 | 用职业兵池/俘虏的实际可招数量替代本体 notable 志愿兵预估 |
| `ModifiedArmy/Recruitment/Patches/PrisonerSalePatch.cs` | 修改 | 俘虏保留/移交/出售分流，恢复敌方 Hero 处理，玩家监狱防误售 |
| `ModifiedArmy/Garrison/Patches/GarrisonTroopDonationPatch.cs` 及理想驻军目标补丁 | 修改 | Party 优先双向交换、0.40 目标系数、禁止缺编 Party 捐兵 |
| `ModifiedArmy/Recruitment/Behaviors/AiRecruitmentBehavior.cs` | 修改 | 将驻军补兵、俘虏移交纳入现有招募/访问决策 |
| `HeroSpawnCampaignBehavior.SpawnLordParty` 定点 Harmony Patch | 新增 | 仅替换新建 Party 出生地点选取，不全局改变 `SettlementHelper` |
| `ModifiedPolitics/.../MilitaryOfficeRecruitmentPatches.cs` | 校验兼容 | 继续在基础配额上提供官职加成，避免重复放大或错误显示 |

避免为同一 `OnSettlementEntered` 和同一每日 Settlement Tick 增加相互覆盖的重复 Patch；负责同一规则的判定逻辑尽可能共用助手方法。

## 四、验收标准

1. 玩家城镇/城堡：开关关→职业兵、俘虏均**零自动招募**；开关开→有资金、库存、编制及安全条件时可自动招募；金币不足/工资超限时正确停止；无须持有 30,000 金币才生效；玩家士兵不得被 AI 周度整编擅自裁撤。
2. AI 城镇/城堡：0～3 级兵营基础配额分别为 1、2、3、4；含军事长官加成后实际招募人数仍不会超过有效上限，职业兵 + 俘虏共用名额。无库存时不凭空生成；职业兵源日生产/库存 30 保持不变。
3. 俘虏：低价值按策略出售，有价值俘虏可在 Party 保留、真实转移到本 Clan 监狱、驻军随后招降；英雄俘虏可正常转移；移交前后名册总数守恒，伤兵状态一致；监狱不再当天把有用俘虏全部卖掉。
4. 交换：Party 缺编时不自动捐兵；有可调驻军时可以补员；再进城不发生反复取还兵；驻军最低保护量、军团和工资限制正常。
5. 出生：新 WarParty 出现在本 Clan 具备驻军的最佳要塞附近，能够被现有招募行为引导入城；无要塞时回退本体，不额外增加初始兵。
6. 日志：区分 `RecruitmentSwitch`、兵营等级、基础/官职配额、职业兵招募、俘虏招降、库存及资金限制、WarParty↔驻军调拨、俘虏保留/移交/售卖、AI 访问原因；按 Clan 汇总 Party 数与满编率、驻军人数，便于与大元帅可召集 Party 数对照。

## 五、实施顺序

1. **先修玩家与 AI 的驻军自动招募链路**：补上开关、玩家财务分支、`NewSettlementGarrisonModel`，确认开启/关闭及日配额正确。
2. **修复俘虏实际流转**：WarParty 保留/出售/移交分流、监狱保留政策、敌方 Hero 处理及玩家监狱保护。
3. **调整驻军双向交换**：理想目标系数先试 `0.40`，并加入缺编 Party 禁止捐兵；分别测试单 Party、Army。
4. **优化 AI 动线与新 Party 选址**：并入 `AiRecruitmentBehavior` 的入城目的地评分，出生位置优先本 Clan 最大驻军要塞。
5. **长周期测试**：至少比较和平与战争阶段的 Party 满编率、Clan 驻军与野战兵力、职业兵库存和大元帅可召集部队数量；先观察再考虑调整 `0.40`，不预先提高职业兵生产速率。

**本方案仅输出设计与实现步骤，未改动仓库代码。**
