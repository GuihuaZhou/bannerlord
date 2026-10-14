# Bannerlord Mod 扩展设计：封建兵征召与 MobilizedHearth

> **设计状态**：本文为拟实施的 Mod 扩展设计，并非当前仓库已经实现的机制。实际编码时需同步修改旧征兵经济结算与征召记录维护流程。

## 1. 目标与基本规则

- 将村庄 `Hearth` 作为**封建兵和职业兵共享的农村生产能力**：每征募1名封建兵扣1 Hearth；每征募1名职业兵扣2 Hearth。
- 每个村庄征兵后必须保留至少100 Hearth；100是**征兵保留线**，不限制本体劫掠等其他行为使 Hearth 继续下降。
- **实际从定居点征兵成功时**扣 Hearth；封建兵、职业兵源池每日生产名额或封建预备队补员**不直接**扣 Hearth。
- 封建兵是临时离乡劳动力：征募时 `Hearth −1, MobilizedHearth +1`；正常返乡时反向恢复；征召超期失效时只减少 `MobilizedHearth`。
- 职业兵是永久脱离农村生产的成本：招募时 `Hearth −2`，不增加 `MobilizedHearth`；转移、解散、阵亡不自动返还这2 Hearth。
- **征兵不直接扣减 Prosperity**，其经济后果经由真实 Hearth、商品/粮食减产、市场、FoodStocks 和饥饿机制传导。
- 玩家、AI、驻军从相应池实际招募时遵守**相同**的 Hearth 成本；不再使用 AI 的10% Hearth 成本倍率。

单村庄可用于征兵的余额：

$$
AvailableHearth_i=\max(0,\lfloor H_i-100\rfloor)
$$

整个城镇/城堡可用余额：

$$
AvailableHearth=\sum_{i\in BoundVillages}AvailableHearth_i
$$

本次征募人数为封建兵 $F$、职业兵 $C$ 时必须满足：

$$
\boxed{F+2C\le AvailableHearth}
$$

例如500 Hearth 村庄最多能够在该时刻额外征募400名封建兵或200名职业兵，或相应混合组合；具体兵种池容量仍另行限制。

## 2. Village 绑定 MobilizedHearth

每个 `Village` 有两个独立含义的数值：

| 字段 | 含义 | 是否随自然增长变化 |
|---|---|---|
| `Hearth` | 游戏本体实际户数，决定真实生产、直接粮食、民兵等 | 是 |
| `MobilizedHearth` | 仍登记在服役征召小队中的封建兵占用量，非负整数 | 否 |

`MobilizedHearth` **以 Village 为单位独立保存**。实际实现可由 Mod 的管理器使用 `Dictionary<Settlement, int>` 持久化，键为该村庄的 `Settlement`；无须修改本体 `Village` 类。

- **不把** `MobilizedHearth` 保存在每个 `FiefTroopDetachment` 中。
- **不绑定**征召小队与 `MobileParty`。
- **不追踪**每个士兵的出生村庄或唯一身份。
- 该值只改变自然增长计算的输入，**不能代替**实际 Hearth 参加商品产量、粮食或民兵计算。
- 同一村庄若关联不同领地数据，必须保证它的占用不会被多个管理实例重复计算；占用的征召记录与当前附属村庄集合发生变动时需进行核对。

## 3. 自然增长：只对基础增长使用有效 Hearth

令：

- $H_i$：Village $i$ 的实际 Hearth；
- $M_i$：Village $i$ 的 `MobilizedHearth`；
- $H_{e,i}=H_i+M_i$：**仅用于基础自然增长计算**的有效 Hearth。

则新的基础增长：

$$
\boxed{BaseHearthGrowth_i=\frac{4}{1+\left(\frac{H_i+M_i}{225}\right)^3}}
$$

**仅覆盖正常村庄的 Hearth 基础增长部分**；劫掠、政策、建筑、Perk、文化、事务等仍按原有规则产生其他修正。自然增长实际增加 Hearth，**不增减 `MobilizedHearth`**。

与之相对，生产使用**真实** Hearth：

$$
\boxed{ProductionMultiplier_i=\frac{2H_i}{H_i+300}}
$$

附属村庄直接供粮：

$$
\boxed{BoundVillageFood_i=12\times ProductionMultiplier_i=\frac{24H_i}{H_i+300}}
$$

上述生产调整是相关 Mod 经济设计的一部分，并非本体公式；具体商品类型的建筑、Perk 等修正继续保留。

**示例**：原有300 Hearth 的村庄征走100名封建兵后，实际 Hearth=200、MobilizedHearth=100，基础自然增长仍按300计算（约1.19/天），但粮食和商品产出按实际200 Hearth计算。100人如全数返回，则实际 Hearth+100、MobilizedHearth−100，计算基础增长所用的有效 Hearth 不突变。

## 4. 现有征召小队数据结构与限制

沿用 `FiefPartyData` 中的：

- `RecruitedTroopDetachmentList`：仍服役的 `FiefTroopDetachment` 集合；
- `ReturnedTroopDetachmentList`：归还后处于冷却期的集合；
- `FiefTroopDetachment.Troops`：按 `CharacterObject` 记录各兵种登记人数；
- `FiefTroopDetachment.WaitCycle`：小队剩余服役周期。

当前 `FiefPartyRecruitment.cs` 会将**剩余周期相同**的征召合并到已有分遣队；因此它不是严格的“一次征募必定一支独立小队”。新方案**无需按 Party 拆分**，可继续按 `WaitCycle` 管理。

**现有严重问题**：`FiefPartyReturn.cs` 在处理返还前，对 `RecruitedTroopDetachmentList` 执行全量 `Clear()`，导致其他部队或未归还兵种的征召记录也被删除。新方案必须取消该行为。

## 5. 征召操作：精确扣除实际 Hearth 与增加占用

征募 $N$ 名封建兵时：

1. 从当前领地附属村庄中计算可用 Hearth，数量不足时不得完成超额招募。
2. 按**尽量均匀、受每村可用余额约束**的方式扣除合计 $N$ Hearth；每村扣除后不得低于100。
3. **同一村庄**增加与实际扣除量相等的 `MobilizedHearth`。
4. 将实际招募的兵种和人数登记到 `RecruitedTroopDetachmentList`（可按现有相同 `WaitCycle` 的归并机制处理）。
5. 士兵从封邑就绪名册转入目标 Party，保障新增小队登记量、实际入队量和 Hearth 支付量一致。

所有数据修改应视为**同一完整结算**：先核验可用额度，再提交士兵、征召记录和村庄数值，避免扣户数失败而实际已入队。

## 6. 正常归还：按兵种匹配，按 WaitCycle 从小到大处理

调用 `ReturnTroopsToSettlement(sourceParty)` 时，**不再全量清空征召列表**。

1. 读取 Party 的士兵名册，筛选封建兵模板允许的 `CharacterObject`，取得每一兵种 $t$ 的实际可归还人数。
2. 统计 `RecruitedTroopDetachmentList` 中该兵种尚登记的人数，取两者较小值；再考虑本次归还的有效兵力容量。不能仅用旧代码的 `MaxCount - _soldierTypeCounts` 判定，因为 `_soldierTypeCounts` 已包含征召中的兵力。
3. 将小队按 `WaitCycle` **从小到大**处理。逐兵种优先消耗即将到期的小队；该小队人数不足则继续下一个小队。
4. 对成功归还的士兵，从对应小队 `Troops` **只扣除已归还人数**；某兵种人数归零删除该兵种条目，整支小队为空时才移除该 `FiefTroopDetachment`。
5. 从来源 Party 中实际移除同数量士兵，将其加入 `ReturnedTroopDetachmentList`，沿用原有归还冷却机制。
6. 按**附属村庄均匀分摊、各村现有 `MobilizedHearth` 不可低于0**的原则，为实际成功归还的 $R$ 名封建兵同步执行：村庄总 `Hearth +R`、`MobilizedHearth −R`。每个村庄增加的 Hearth 必须与该村庄减少的占用量完全相等。
7. 未归还的兵种、小队及其对应的总占用不变。**归还本身不直接增减 Prosperity。**

单种兵种的上限：

$$
Returnable_t\le\min\left(PartyCount_t,\sum_d DetachmentCount_{d,t}\right)
$$

而本次总返还 Hearth 只能按真正完成移除、入冷却池的士兵数量计算，不可仅按 Party 所持同兵种数量计算。

**示例**：原登记分遣队 A（周期1，步兵30、弓10）、B（周期2，步兵20、弓20）、C（周期4，步兵10、弓10）。本次归还步兵40、弓15：A全数删除；B减少步兵10、弓5，剩余步兵10、弓15；C不变。合计只结算55名兵力，不能清空其他45名登记兵力。

## 7. 超时、易主、陷落等清理

在 `FiefPartyReinforcement.WeeklyUpdate()` 中，当某小队 `WaitCycle` 到期：

1. 读取其**当前仍登记**的人数 $N=detachment.GetTotalCount()`；
2. 从 `RecruitedTroopDetachmentList` 中移除**该小队**；
3. 按有占用量的附属村庄分摊，合计减少 $N$ `MobilizedHearth`；
4. **不恢复**实际 Hearth（该批征召按永久损失处理）。

`OnSiegeCompleted()`、`OnSettlementOwnerChanged()` 等导致征召记录被清除的路径，均必须先计算/结算对应占用，再删除记录，不能留下孤立的 `MobilizedHearth`。

`ReturnedTroopDetachmentList` 记录的是已完成返乡、正在冷却的士兵，**不再占用** `MobilizedHearth`；其冷却到期只恢复封邑就绪名册，不重复恢复 Hearth。

## 8. 村庄分配算法：均匀与整数守恒

- **征募**：尽量平均从所有可用村庄扣除 Hearth；某村扣至100则停止向它分配，其余成本继续由其他有余额的村庄承担。
- **正常返乡**：只向 `MobilizedHearth > 0` 的村庄恢复 Hearth；某村可释放占用不足时，把未分配的返还量继续均匀分配给其他有占用的村庄。
- **超时/失效**：与返乡使用相同的**占用释放**分配方法，但只减少占用、不增加实际 Hearth。
- 分配数量均用整数，按可用容量逐轮分配余数；不允许先平均减去固定值、再将负数截断为0，因为这会导致**实际释放总量小于预期**。
- **每一次返乡**必须确保每村增加 Hearth 的量，严格等于该村减少 `MobilizedHearth` 的量。

## 9. 数据一致性与防止 MobilizedHearth 残留

对每个 Town / Castle 所管理的征召记录，始终要求：

$$
\boxed{\sum_{v\in BoundVillages}MobilizedHearth_v=\sum_{d\in RecruitedTroopDetachmentList}d.GetTotalCount()}
$$

这里的 `BoundVillages` 表示该领地在结算时负责承载占用的村庄集合。如领地易主、附属关系变动，应先处理旧占用，再按必要的迁移或清理规则恢复一致性，**不能把新的村庄集合当作旧占用从未存在**。

统一提供 `ReconcileMobilizedHearth()` 进行校验，建议在**征召、归还、超期清理、陷落、易主、存档载入**之后运行，并可在每日经济更新前作兜底检查：

| 检查结果 | 处理 |
|---|---|
| 征召记录人数为0，仍有占用 | 清零该领地对应村庄的孤立占用；不修改实际 Hearth |
| 占用量大于登记人数 | 精确释放超额占用；不修改实际 Hearth |
| 占用量小于登记人数 | 记录异常，按均匀分摊补足缺失的占用；**不可再次扣实际 Hearth** |
| 两侧人数一致 | 不作数值修改 |

另外：

- 旧存档迁移需区别处理，因为旧征召记录**不一定按新人口制度足额扣除了 Hearth**；不能盲目按照登记人数补占用、再次扣 Hearth。
- 多个领地共享或改绑村庄时，应防止校验方法相互抢占、重复清零；以明确的当前占用管理归属为准。
- 匹配逻辑是**按兵种数量的抽象归还**，不能严格识别原籍；跨 Party 转移仍可以按当前定居点登记兵种匹配，但无法证明单兵身份。
- 超期清理后再带回来的同兵种士兵没有剩余登记额度，不应额外返还 Hearth。

## 10. 对现有 ModifiedArmy 的修改清单

| 源码 | 修改要求 |
|---|---|
| `Fief/Models/PartyData/FiefPartyRecruitment.cs` | 玩家手选与 AI/自动封建兵征召均校验并扣 Hearth，村庄增加 MobilizedHearth，小队登记数量对应真实入队人数 |
| `Fief/Models/PartyData/FiefPartyReturn.cs` | **取消全量 Clear**；按 `WaitCycle` 升序、兵种数量逐批扣减；只归还真实入队冷却池的兵数；同步返还 Hearth、释放占用 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | 每周征召小队到期时只移除该小队，并释放相同数量的 MobilizedHearth，不恢复实际 Hearth |
| `Fief/Models/PartyData/FiefPartyData.cs` | 陷落、易主等所有清除征召记录的路径统一释放占用 |
| `Fief/Models/FiefPartyManager.cs` 或专属数据管理组件 | 保存 Village → MobilizedHearth 的持久化字典；提供均匀分摊、核对与修复接口 |
| `Fief/Models/PartyData/FiefPartyEconomy.cs` | 移除旧 Prosperity 一次性/每日征兵费用和返乡返还；统一玩家/AI Hearth 成本和底线检查 |
| Hearth 增长模型 | 仅基础增长使用 `Hearth + MobilizedHearth`，其他效应使用原版修正 |
| 村庄商品/粮食、`NewSettlementFoodModel.cs` | 生产以**实际 Hearth**计算，采用相关新公式 |
| `FiefSaveDefiner` 和相应 Saveable 字段 | 注册新增持久化占用数据结构，处理旧存档兼容 |

**实现关键检查**：`FiefPartyReturn.cs` 现有归还容量算法、`RecruitedTroopDetachmentList.Clear()`、`FiefPartyData.cs` 中的陷落/易主 `Clear()`，以及 `FiefPartyEconomy.cs` 对 AI 仅收约10% Hearth 的逻辑，均与本设计存在直接冲突。

---

**最终约定**：`MobilizedHearth` **绑定 Village 并持久化**；`RecruitedTroopDetachmentList` 只保留兵种和服役周期，不绑定 Party、不保存村庄分配；征募、按周期优先归还、超期及定居点变化统一同步结算；以总量校验和精确分摊防止占用残留。

## 相关源码

- [`FiefPartyRecruitment.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyRecruitment.cs)：征召与登记。
- [`FiefPartyReturn.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)：正常归还及待修复的全量清理。
- [`FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)：到期小队清理。
- [`FiefTroopDetachment.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefTroopDetachment.cs)：兵种与周期数据。
- [`FiefPartyData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)：征召列表与易主处理。
- [`FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)：封邑数据管理、存档。
- [`FiefPartyEconomy.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyEconomy.cs)：待替换的旧人口与繁荣度扣费。
- [`DefaultSettlementProsperityModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementProsperityModel.cs)：原有 Hearth 增长修改入口。
- [`NewSettlementFoodModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Garrison/Models/NewSettlementFoodModel.cs)：新村庄直接供粮公式修改位置。
