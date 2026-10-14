# ModifiedArmy：共享基础兵源系统设计方案

> **状态**：设计基线，不代表代码已实现。  
> **依据**：`GuihuaZhou/bannerlord`，最新核对提交 [`dadeac409`](https://github.com/GuihuaZhou/bannerlord/commit/dadeac409f231f32044509ecac86b7f054dd682b)（2026-10-12）；资料库 `Bannerlord_Mod_Extension_Design.md`。  
> **范围**：仅改造兵源供给及士兵生成流程，不修改既有封建兵驻扎组织。

## 1. 目标和边界

1. 由附属村庄的**有效户数**产生每日基础兵源，职业兵、封建兵共享同一份供给。
2. **取消职业兵员池与封建兵员池**；基础兵源是每日数值额度，不是 `CharacterObject`、`TroopRoster` 或可储存的兵员库存。
3. 生产与招募审核通过后直接使用已有的 `CharacterObject` 增加实体士兵：职业兵进入 `Town.GarrisonParty.MemberRoster`；封建兵进入现有城镇/城堡封邑部队名册（当前 `_fiefParty`）。
4. **封建兵仍集中驻扎于城镇/城堡**，本阶段不分散至村庄，不引入野战子 Party，不调整现有封建兵编制、服役周期与归还冷却。
5. 保留职业兵的文化与兵营训练参数、封建兵按文化的**每周最高补员档次**（10/20/30），以及现有驻军职业兵／俘虏共享的财政、工资、编制、兵种结构与每日自动招募限制。
6. 不通过新比例倒推或限制职业兵、封建兵**编制上限**；文化比例**只用于基础兵源分配**。

## 2. 已实现的农村动员经济：直接复用

根据已推送的 `FiefPartyManager`：

- `H_v` = 村庄实际 `Village.Hearth`；
- `M_v` = `FiefPartyManager.GetMobilizedHearth(village)`，即该村**在外服役封建兵已从 Hearth 扣除的占用标记**，并非全部封建兵人数；
- `F_v` = 该村应承担的**全部封建兵人口占用**（就绪、在外服役、归还冷却；含伤兵）。该值只参与计算，不要求新建持久化的村庄士兵名单。

有效户数：

\[
\boxed{X_v=\max(0,H_v+M_v-F_v)}
\]

`M_v` 是为了抵消在外服役封建兵已从实际 `Hearth` 扣除的人数，避免与 `F_v` 重复计算。例：某村 `H=400,M=0,F=60` 时 `X=340`；征走20名后 `H=380,M=20,F=60`，`X` 仍为340。

**现阶段 `F_v` 的来源**：先在封邑级统计现有封建兵总数 `F_total`，包括 `_fiefParty` 中就绪士兵，以及 `RecruitedTroopDetachmentList`、`ReturnedTroopDetachmentList` 中的登记人数（同一士兵只计一次）。先保留每村已有的 `M_v` 对应服役封建兵占用，再把剩余的未动员封建兵人口占用 `F_remaining = max(0, F_total − ΣM_v)`，在**所有附属村庄**之间按稳定顺序尽量均匀地作虚拟整数分摊；因此 `F_v=M_v+F_remaining_v`，通常满足 `ΣF_v=F_total` 且 `F_v≥M_v`。这种分配更符合现有村庄动员登记，避免某村 `M_v>F_v` 时虚增其可用人口。此步骤不移动士兵、也不修改 `MobilizedHearth`；村庄状态异常只影响供给，不影响人口分摊。若改绑／旧档导致 `ΣM_v>F_total` 或登记归属不一致，先走现有 `ReconcileMobilization`，不能静默修改 M。

原有征召与人口结算保持不变：

| 事件 | 实际 Hearth | MobilizedHearth | 备注 |
|---|---:|---:|---|
| 封建兵补入封邑名册 | 0 | 0 | 仅 `F_total` 增加，次日基础供给下降 |
| 封建兵被领主征召1人 | −1 | +1 | 沿用 `TryMobilize` 与征召记录 |
| 封建兵正常返还1人 | +1 | −1 | 沿用 `ReleaseFiefService(..., true)` |
| 封建兵服役超期／失效1人 | 0 | −1 | 沿用占用释放，不恢复 Hearth |
| 职业兵获批并进入驻军1人 | −2 | 0 | 必须在新的生成交易内支付，且不可重复扣费 |
| 驻军职业兵调拨野战部队 | 0 | 0 | 已在进入驻军时支付人口成本 |
| 驻军俘虏转化 | 0 | 0 | 无村庄基础兵源及新增 Hearth 成本，沿用俘虏处理规则 |

真实 Hearth 必须遵守每村**至少100**的主动征兵保留线。现有自然增长 `4/(1+((H+M)/225)^3)` 与按真实 H 计算的粮食／商品产出**不在本次改造范围内**。

## 3. 村庄的每日基础兵源

\[
\boxed{S_v=\begin{cases}
0, & VillageState_v\ne Normal\text{ 或 }X_v\le100\\[3pt]
0.03X_v\dfrac{X_v-100}{X_v+50}, & \text{其他}
\end{cases}}
\]

- `0.03`：**每日最大理论供给率3%**，不是每天固定产生3%的士兵。实际供给率随有效人口下降而降低。
- `X≤100` 或村庄被劫掠、遭掠后未恢复正常：当日无基础兵源。
- 封邑供给 `S_total = Σ_{v∈BoundVillages} S_v`。按村逐一计算再求和，不能先合并 Hearth 再套非线性函数。
- 基础兵源不是可持久储存的“基础兵员池”；当天无人接收的供给额度**作废**。

示例：一座领地有2座正常村庄，分别 `H=400,M=0,F=60`，每村 `X=340`、`S≈6.277`，合计 `S_total≈12.554人/天`；这里表示供给**能力**，并非必然实际生成12.554人。

## 4. 文化比例：只分配供给，不约束人数上限

建议给现有 `professionalTroopTemplates.xml` 的每个 `ProfessionalTroopTemplate` 增加一个可选属性（先在同文化城镇、城堡与港口配置相同数值）：

```xml
<ProfessionalTroopTemplate
    culture="Culture.empire" id="empire_castle"
    professionalRatio="0.80"
    baseCapacity="30" baseDailyProduction="0.40"
    dailyProductionStep="0.30">
    <!-- 现有 TroopComposition 不变 -->
</ProfessionalTroopTemplate>
```

`professionalRatio = r ∈ [0,1]`；`fiefRatio = 1−r`，无需再单独配置第二个比例。旧 XML 缺失时建议回退 `r=0.5` 并记录提示，非法值拒绝或告警。**原来的 `baseCapacity=30` 仅是待废弃池容量，不是新编制上限。**

| 文化 | 建议 `professionalRatio` | 封建兵份额 `1-r` |
|---|---:|---:|
| 帝国 | 0.80 | 0.20 |
| 阿塞莱 | 0.60 | 0.40 |
| 斯特吉亚 | 0.50 | 0.50 |
| 库塞特 | 0.40 | 0.60 |
| 诺德 | 0.40 | 0.60 |
| 巴旦尼亚 | 0.25 | 0.75 |
| 瓦兰迪亚 | 0.20 | 0.80 |

> 表中仅为**建议初始值**，尚未单独确认每一种文化的最终比例。比例不影响双方的训练能力、每周补员上限、编制上限或最终部队人数。

每日初始分配：

\[
S_P=rS_{total},\qquad S_F=(1-r)S_{total}
\]

采用**权重优先、剩余重分配**：先受职业兵训练／预算／驻军额度及封建兵补员／编制约束，某方无法使用的当日额度可以转给另一方；都不能使用则作废。**兵源充足时两类士兵可以同时达到自身生产上限，符合预期。**

## 5. 从数值供给到具体 `CharacterObject`

### 5.1 职业兵

1. 根据 `professionalTroopTemplates.xml`、文化匹配与有效兵营等级筛选**已存在的**合法 `CharacterObject` 候选（包括步兵、射手、骑兵等）；XML 中的具体兵种权重仅作为同条件候选的偏好，不强制按固定比例生成。
2. 将这些兵种形成**仅在本次招募调用中存在的虚拟 `RecruitmentCandidate`**。所有职业候选共享同一个 `S_P` 额度，不能每种兵都各得到一次完整额度。
3. 职业候选与真实驻军俘虏候选交给现有 `RecruitmentModelManager` 统一评估；沿用职业兵和俘虏共享的驻军日招募上限、编制、兵种结构、工资、购买成本、Clan 财政预算和长期维持能力。
4. **审核通过且真实 Hearth 可支付时**，在同一次结算中：扣供给额度、从有可用 Hearth 的正常来源村庄扣 **2 Hearth/人**、将相应 `CharacterObject` 计数直接加入 `GarrisonParty.MemberRoster`，执行现有金币支出与 `ClanRecruitmentBudgetManager.CommitRecruitment()`。失败则三者均不发生。
5. 俘虏转化保留原有俘虏服从度、兵种筛选、费用、预算、共享日招募额度；**俘虏不消耗 S_P/S_F**。

职业兵实际每天生成量不得超过**现有文化训练速度**（`baseDailyProduction + dailyProductionStep × effectiveBarracksLevel`，保留既有文化匹配规则及有效的军事官职训练加成），也不得超过可支付人口、已批准招募数量与驻军当日剩余额度。

### 5.2 封建兵

1. 不使用驻军招募模型；继续依靠 `FiefPartyData` 现有封建兵编制缺额、`fiefPartyTemplates.xml` 的 `CharacterObject` 与兵种权重选择补员。
2. **从可用基础兵源额度直接生成**封建兵进入城镇／城堡现有 `_fiefParty` 名册，不经任何池存取。
3. 保留文化**每周最高补员档次**：帝国、阿塞莱10；斯特吉亚、巴旦尼亚、库塞特、诺德20；瓦兰迪亚30；同时不得突破封邑整体及兵种编制。按日结算并记录本周已经新增人数；周额度用尽后本周不再新增。原 XML 的每周最低值**不是脱离兵源限制的保底产兵**。
4. 封建兵处于就绪名册时**不扣实际 Hearth**；领主正式征召才沿用已有 `TryMobilize` 扣 Hearth 与增加 `MobilizedHearth`。具体兵种可继续沿用现有解锁要求，勿因删除池而扩大可选兵种。

## 6. 生产小数与日／周节奏

- **每天一次**由同一生产协调流程结算基础供给及两类兵源分配，避免职业兵与封建兵各算一遍 `S_total`。
- 职业兵保留文化及兵营训练速率形成的不足1人进度；封建兵保留不足1人的进度，但仅在当日有真实供给、接收能力及明确需求时累加。
- 进度代表不足1人的**已获准生产进度**，而非可结转的未使用兵源库存；进度上限应小于1，达到整数时立即结算实体士兵。需求消失或领地状态变化时必须有明确的失效／清理规则。
- 封建兵仍可每周进行原有服役到期、冷却回归、升级等维护；**不要再由旧每周补员路径另外生成士兵**，避免重复。
- 已支付 Hearth 的职业兵转移、解散、死亡不再次扣除或返还生产时的 Hearth。

## 7. 非目标

本阶段**不**：分散封建兵到村庄、建立子 Party、改变封建兵编制计算、从文化比例推导额外职业兵编制、重写驻军财政／俘虏招募、修改封建兵现有归还机制。玩家职业兵主要通过其自有驻军的既有转移入口获得，AI 野战部队通过现有同 Clan 驻军调拨机制获得；不保留从中间职业兵池直接向野战队伍或玩家招募的旧入口。

## 8. 不变量／验收要求

1. 每座村庄 `S_v` 只贡献一次；`本日职业兵基础供给消耗 + 本日封建兵基础供给消耗 ≤ ΣS_v + 合法历史小数进度的兑现`，且历史进度不会变成不限量库存。
2. 新增封建兵使 `F_total` 增加；征召／返还按现有 `H/M` 对称规则，任何场景避免在外封建兵重复计数。
3. 一名职业兵至多扣一次2 Hearth；每个被扣 Hearth 的村庄结算后保持 `H≥100`（本体其他行为除外）。
4. 军官／文化／兵营／工资／预算／驻军招募日额度不能被新生成流程绕过。
5. 两类池不再生产、不再持有库存；存档加载不得复活旧池或复制士兵。

---

### 主要代码依据

- [`FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs) — 村庄 MobilizedHearth 保存、扣减、返还与核对。
- [`FiefPartyData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs) — 当前封邑就绪＋征召＋冷却总人数与编制。
- [`FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs) — 现有每周补员和池提取。
- [`GarrisonRecruitFromPrisonersBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs) — 职业兵／俘虏统一招募。
- [`DefaultAIRecruitmentModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Models/DefaultAIRecruitmentModel.cs) — 编制、结构、费用、工资与长期预算评估。
- [`VillageEconomyPatches.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Patches/VillageEconomyPatches.cs) — 已有自然增长及村庄生产公式。
