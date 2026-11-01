# ModifiedArmy 封建兵与职业兵制度重构设计 V1.0

> 状态：设计讨论稿，尚未实施  
> 整理日期：2026-11-01  
> 适用范围：`ModifiedArmy`；税收模块需与 `ModifiedPolitics` 现有税务模型兼容。  
> 术语：Hearth（村庄实际户数）、OccupiedHearth（封建供养占用）、MobilizedHearth（出征临时扣户记录）。

## 一、设计目标

重新建立封建兵与职业兵之间的经济、人口和军事关系。

- **职业兵**：脱离农村生产，长期服役，领取正常工资，主要驻扎城镇和城堡。
- **封建兵**：依托农村土地收益及乡村人口维持，平时生活于村庄，战时受领主征召。
- 两类兵员共享农村经济基础，但其人口成本、供养形式、财税负担和军事用途不同。
- 城镇凭较强财政能力支持更多职业兵；农村人口和供养能力决定封建兵规模与质量。
- 避免重复扣户、出征后重复补员、身份互转凭空创造人口、免费无限扩军。

本次设计不重做全套定居点经济，仅涉及封建兵容量、封建税赋、职业兵人口消耗、互转及配套生产和记录机制。

## 二、核心资源模型

| 概念 | 含义 | 是否改变实际 Hearth |
|---|---|---|
| `Village.Hearth` | 村庄真实户数、生产能力 | 是 |
| `OccupiedHearth` | 在籍封建兵对农村供养能力的占用 | 否，按名册动态计算 |
| `MobilizedHearth` | 封建兵在外服役时临时扣除的户数及对应追踪记录 | 是：动员扣除，正常返乡恢复 |

村庄的供养计算：

```text
EffectiveHearth(v) = Hearth(v) + MobilizedHearth(v)
OccupiedHearth(v) = Σ [Count(v, troop) × HearthCost(troop.Tier)]
RawAvailableHearth(v) = EffectiveHearth(v) - OccupiedHearth(v)
AvailableHearth(v) = max(0, RawAvailableHearth(v))
OverCapacity(v) = max(0, -RawAvailableHearth(v))
```

计入供养占用的在籍封建兵包括：在村待役、外出服役、返乡冷却。不能因士兵出征就释放供养额度；不能把普通民兵或职业兵误计入封建占用。

`OccupiedHearth` 应当以各村庄的实际在籍兵种、数量及归属为准计算，不建议与名册并行保存一个难以同步的独立长期计数器。出征临时扣户需要独立追踪，用于返乡及战损结算。

## 三、封建兵容量重构

### 3.1 移除旧容量公式

移除“固定比例推算封建兵人数”的方案（例如之前讨论过的 `floor((Hearth - 100) × 5%)`），并替换目前代码的 `BaseLimit + VillageBonus × VillageCount` 作为最终人数判定的地位。

新方案不预先确定所有兵种共用的固定人数容量，而是由实际兵种等级决定供养消耗。

### 3.2 等级供养成本

**已明确提出：每名 T5 封建兵占用 20 Hearth 供养额度。**

| 士兵等级 | 每兵供养成本（Hearth） | 状态 |
|---|---:|---|
| T0 | 待定 | 未确认 |
| T1 | 4 | 候选 |
| T2 | 6 | 候选 |
| T3 | 10 | 候选 |
| T4 | 15 | 候选 |
| T5 | **20** | **明确提出** |
| T6 | 30 | 候选 |

只有 T5=20 已明确提出；其余数值不能视为最终决定。若文化还需要额外的绝对人数硬上限，该规则及数值尚未确认。

以单村 Hearth=600 为例，在没有其他封建兵占用时可维持 `floor(600/20)=30` 名 T5。**容量公式不额外扣除保留的100户**；当前职业兵和动员实际扣户时的最低100户保护属于另一种约束。

### 3.3 逐村归属与跨村处理

- 每批封建兵必须记录归属村庄，城镇/城堡仅做汇总。
- 一座村庄的供养余量不能无记录地给其他村庄士兵使用。
- 补员前应确定具体接收村庄及剩余供养能力。
- 归属村庄无法接收返乡士兵时，应按合法转移/等待规则处理，不得凭空释放或重分配供养额度。

### 3.4 补员、升级与超额

- 新增封建兵时，以**拟生产的实际兵种等级**逐个审批占用。
- T4→T5（假设候选成本15→20）需额外5户供养余量；没有余量则停止这次**正常**升级。
- 村庄被劫掠、户数下滑或存档迁移使现役占用超过供养能力时，不强制删除既有士兵；但禁止增加占用的新兵和正常升级。
- 降级、伤亡、身份转出或合法迁移可减少占用。返乡冷却本身不释放占用。

## 四、职业兵人口消耗

### 4.1 当前仓库已实现的规则

`FiefPartyManager.TryConsumeProfessionalHearth()` 对每个成功招募的职业兵，从正常附属村庄**永久扣除2个 Hearth**，并保持来源村庄扣户后不少于100 Hearth。职业兵候选名额生成不等于实际招募，消费名额时才扣户。

相关调用：`SettlementManpowerProductionBehavior.TryConsumeProfessionalOffer()`；普通征兵事件也可调用相同扣户接口。

### 4.2 新制度建议

**建议暂时沿用每名职业兵扣除2 Hearth，但尚未正式最终确认。**

- 新职业兵成功招募：实际Hearth减2；不增加封建供养占用。
- 职业兵升级、驻军/野战调动：不重复扣户。
- 职业兵阵亡：原则上不自动恢复此前已经永久消耗的Hearth。
- 招募成功前必须同时检查“扣户后实际Hearth保底100”和“扣户后不侵占现有封建兵供养额度”。
- 职业兵实际扣户会自然减少未来可用的封建供养能力，不另作一次重复扣减。

例如：村庄600 Hearth、20名T5封建兵占400供养。再招募10名职业兵后，实际Hearth变580，供养占用仍400，余额从200降至180。

## 五、封建兵税赋减免（已明确规则）

### 5.1 计算公式

城镇、城堡统一以**全部在籍封建兵标准日工资总额的50%**减免定居点基础税收，同时确保原税收不被减成负数，原则上最低保留300第纳尔。

```text
OriginalTax = SettlementTaxModel.CalculateTownTax(town)  // 正常税收修正完成之后
StandardWages = Σ [FiefTroopCount(troop) × BaseWage(troop)]
FinalTax = min(OriginalTax, max(300, OriginalTax - StandardWages × 0.5))
TaxReduction = OriginalTax - FinalTax
```

当 `OriginalTax < 300` 时，`FinalTax = OriginalTax`，不因“保底300”凭空创造税收。

### 5.2 现行标准工资

按仓库当前 `NewPartyWageModel.GetCharacterWage()` 基础值：

| 等级 | 基础日工资（第纳尔） |
|---|---:|
| T0 | 1 |
| T1 | 2 |
| T2 | 3 |
| T3 | 5 |
| T4 | 16 |
| T5 | 24 |
| T6 | 34 |

税赋减免按基础工资；**不叠加野战部队战时1.5倍工资倍率**，也不将服役免薪视为无税赋减免。结算时不得受技能、建筑等工资修正的影响而错误增加或缩减封建税赋。若兵种存在特殊工资属性，应以明确的“基础标准”接口统一核算，不简单复用含情境修正的总军饷。

### 5.3 统计范围与财政分项

纳入：在村待役、在外服役、返乡冷却的在籍封建兵；不纳入普通民兵、正式转为职业兵的士兵或已经结算的阵亡人员。同一士兵只计算一次。

优先将减免体现在所属城镇/城堡 `SettlementTaxModel.CalculateTownTax()` 的最终基础税收上，不改动关税、单独结算的村庄贸易收入、独立项目收入、工坊和其他家族收入。需要与 `ModifiedPolitics` 的税务官增益、政策、建筑修正明确排序，避免重复扣除。

**注意**：税收触及300保底后，新征召兵员的理论税赋成本仍须纳入AI长期财政承受能力评估，不能因实际减免暂时无法继续增长就认定新增封建兵免费。

## 六、职业兵与封建兵互转

互转不是生成新士兵，必须在一次原子事务中改变身份、来源/接收归属、名册和Hearth资源，不调用普通职业兵补员流程重复生成兵员。

### 6.1 封建兵 → 职业兵

以一名T5为例（T5供养20、职业兵扣户2）：

1. 验证兵种具备职业资格、职业驻军接收编制和资金条件。
2. 从来源村庄封建名册移除，释放20户供养占用。
3. 按职业兵脱产规则从实际村庄Hearth扣除2户（不得使实际户数低于保护值，也不得使余下封建兵供养越界）。
4. 取消其封建税赋减免，纳入职业兵工资、驻军管理。
5. 全程事务性提交，失败须回滚。

净释放封建供养余量18户；但必须正确使用具体来源村庄，而不是从封邑总户数里任意扣除。

### 6.2 职业兵 → 封建兵

以一名T5为例：

1. 验证兵种具备封建资格，指定接收村庄。
2. 验证接收地有20户供养能力。
3. 从职业兵名册移除，登记封建兵归属，并占用20户供养。
4. 停止职业工资，纳入封建税赋减免。
5. **是否恢复2实际Hearth，取决于人口来源是否可追溯。** 当前没有长期按兵种/村庄记录每名职业兵原始扣户来源，不能对来源不明兵员直接返还2户。

建议新增来源凭证/账本，以`来源村庄 + 兵种 + 数量 + 已扣户凭证`维持汇总，不要求每个士兵独立ID。来源可确认且凭证有效时，允许将此前永久脱产的人口以合法迁入/返乡方式恢复到接收村庄，并核销凭证；来源不明时不返还，不能凭空增加人口。

恢复2户仍只是**建议中的互转实现方案**，不是已最终确认的业务规则。初次正式实现前要明确跨村、跨领地转移及旧存档来源缺失的政策。

### 6.3 已在外服役的封建兵

第一阶段建议只允许返乡或驻地完成身份互转，降低与临时动员账本的耦合。在外直转如后续开放，必须同时完成：结算/解除临时 `MobilizedHearth`、释放封建供养、职业兵永久扣户、解除封建服役批次、更新工资身份。不得把临时返乡恢复和永久职业扣户双重计入。

## 七、封建兵出征、返乡和死亡

### 7.1 出征

继续沿用当前 `FiefManpowerLedger.TryMobilize()` 的机制：**每位动员封建兵临时扣除1实际Hearth，并记录 `MobilizedHearth`**（目前代码使用每人1户）。出征不释放士兵原有的按等级供养额度。

例如村庄已有30名T5封建兵占600供养额度，30人全部出征后，村庄有效供养能力通过 `Hearth+MobilizedHearth` 保持正确，不能因兵员离乡再补出30名T5。

### 7.2 服役工资

按现有文化 `FreeWageDays` 运行：免薪期不支付普通野战工资，超过免薪期限后依工资Model支付。封建身份仍在时，税赋减免持续存在，因此长期征召可能同时产生税赋损失与野战工资。

### 7.3 正常返乡

恢复原临时动员扣除的Hearth，核销对应 `MobilizedHearth`，士兵继续按当前等级占用封建供养，并按现有 `ReturnCooldownWeeks` 处理返乡冷却。

### 7.4 死亡或彻底损失

士兵死亡/永久损失时释放封建供养额度，停止税赋减免。若在外服役期间死亡，已扣除的1 Hearth不自动返还，只清理相应动员标记和服役批次。

## 八、村庄归属、共享名册与AI

- 生产、服役、返乡、死亡和身份互转均应具备可验证的来源村庄。
- 城镇、城堡汇总附属村庄兵力，但不得以平均分摊人数代替真实来源归属。
- 封建兵可共享现有村庄民兵Party的实际驻村载体，但必须独立于普通民兵记录封建身份与等级。
- 每日同步在村、在役和冷却批次，维持 `CharacterObject + 数量 + 来源村庄` 与升级树兼容的身份核对。
- AI按行动情境动员封建兵，和平时期优先解除长期不必要的动员；单次征召最小批量沿用此前目标的**整封邑至少25人**，不是单村至少25人。
- AI经济判断包括封建税赋理论负担、出征后免薪期结束的工资成本，以及职业兵维护费用，不因封建兵工资暂时为零或税收已经触底而无限扩军。

## 九、共享兵源与产能的调整

当前 `SettlementManpowerProductionBehavior.CalculateSettlementSupply()` 会从农村有效人口中按封建兵人数估算共享产能。新设计改为等级供养之后，应重新明确三个不同层次：

1. `Village.Hearth`：真实人口与商品/粮食生产基础。
2. `OccupiedHearth`：封建兵占用的土地供养承诺。
3. 兵源训练/生产进度：影响每天可以形成多少职业兵/封建兵，不是另一次人口扣除。

不得简单将每名T5供养占用20，再在生产模型中将同一20户作为永久人口损失扣除，同时在职业兵实际招募时又扣2户。训练速率与可容纳兵力应分别判断。

## 十、配置建议

建议统一至 `militaryConfig.xml`：

| 参数 | 建议/状态 |
|---|---|
| `FiefTaxReductionRate` | **0.5，已确定** |
| `MinimumSettlementTax` | **300，已确定** |
| `FiefHearthCost.T5` | **20，已提出** |
| `FiefHearthCost.T0-T4/T6` | 待定；第3.2节为候选值 |
| `ProfessionalHearthCost` | 2，建议沿用当前实现，待最终确认 |
| `FreeWageDays` | 沿用现有文化配置 |
| `ReturnCooldownWeeks` | 沿用现有文化配置 |
| 文化封建兵人数额外上限 | 是否保留待定 |
| 职业转封建人口返还 | 建议有可核销来源凭证才恢复；具体方案待定 |

删除或停止使用旧容量判定所需的固定比例/`BaseLimit + VillageBonus × VillageCount` 字段，但不能随意移除旧存档所依赖的 Saveable 定义。

## 十一、主要代码改造清单

| 模块/文件 | 变更目标 |
|---|---|
| `FiefPartyData` | 村庄归属、按等级供养占用、容量聚合接口 |
| `FiefPartyReinforcement` | 新兵生产与每周升级的户数成本检查 |
| `FiefManpowerLedger` | 区分职业永久扣户、封建动员临时扣户与按村庄归属 |
| `FiefPartyRecruitment` | 按具体来源村庄记录征召、服务身份及动员扣户 |
| `FiefPartyReturn` | 返乡、死亡、冷却和身份转换的资源结算 |
| `FiefPartyManager` | 统一Hearth消耗、来源凭证、账本及互转服务 |
| `SettlementManpowerProductionBehavior` | 共享供应、职业兵消费名额与封建兵新容量审批 |
| `NewPartyWageModel` | 复用现行基础工资，不重写军饷体系 |
| 税收模型 | 对城镇/城堡统一实现50%减税、300保底及财政展示 |
| `militaryConfig.xml` | 配置分级供养、职业扣户与封建财税参数 |
| 财政/兵源UI | 展示封建税赋减免、已占用/可用Hearth与超额情况 |

实施顺序：①村庄来源与供养计算→②封建补员/升级约束→③职业兵扣户联合审核→④税收结算→⑤身份互转与可追溯人口凭证→⑥动员/返乡/死亡核销→⑦AI/财务/UI联调→⑧存档迁移与验收。

## 十二、验收场景

1. **600 Hearth / 30名T5**：正好占用600供养，无需额外减100；新增T5被拒绝。
2. **600 Hearth / 20名T5 / 10名新职业兵**：成功后Hearth580、占用400、可用180。
3. **600 Hearth / 30名T5转出10名职业兵**：在所有审批通过且职业扣2户规则生效后，Hearth580、占用400、可用180。
4. **职业兵返乡转T5**：有可核销来源凭证时，依据明确的最终人口返还规则结算；无凭证不得凭空恢复Hearth。
5. **封建兵正常升级**：不足额外等级供养差额时禁止升级，允许已存在的超额状态原样保留。
6. **全员出征**：供养额度不释放；不能因兵员离村而生成第二批满额封建兵。
7. **正常返乡**：临时扣户只恢复一次，封建供养占用持续存在。
8. **战死**：封建供养及税赋减免及时释放；动员损失不凭空返还人口。
9. **低税收**：税收500、理论减免1000时最终300；税收200时仍200，不增税也不为负。
10. **经济恶化**：封建兵供养超额时停止新增与增负升级；既有部队不被强制删除。
11. **来源村庄不同**：A村释放的供养额度不会错误计入B村；跨村转化必须真实移动归属并通过容量检查。
12. **玩家和AI**：遵守一致的经济/容量底层约束；不得只对玩家扣真实Hearth而让AI无成本扩军。
13. **税收模块兼容**：税务官、建筑、文化政策修正与封建税赋扣减不重复、不相互覆盖。
14. **旧存档**：来源未知的职业兵不凭空返还Hearth；旧封建兵的来源补录需可审计。

## 十三、仍待最终确认

1. 除T5外各等级的供养户数，特别是T0、T6。
2. 是否保留文化固定人数上限。
3. 职业兵每人永久扣2 Hearth是否正式确立。
4. 职业兵转封建兵的人口返还与跨村、跨城人口来源凭证规则。
5. 无法追溯来源的旧封建兵如何分配到附属村庄。
6. 出征临时扣户按每人1户是否继续沿用现行规则。
7. 共享兵源公式如何在不重复扣户的前提下反映封建供养占用。

## 十四、设计总结

**农村人口**决定长期能培养多少脱产职业兵；**土地供养**决定可以维持多少、什么等级的封建兵；**财政**决定领主能持续支付多少常备军工资，以及要因封建兵让渡多少税收。

职业兵是低单次人口成本、高持续财政成本的脱产力量；封建兵是高农村供养占用、较低平时现金军饷、适合短期动员的地方力量。

两者依靠明确的村庄来源、人口变动、按级供养、税赋与可回滚的身份转换事务衔接，避免无中生有的兵员与农村人口。

---

## 代码依据（仓库路径）

- [`ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs)
- [`ModifiedArmy/ModifiedArmy/Fief/Services/FiefManpowerLedger.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Services/FiefManpowerLedger.cs)
- [`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [`ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [`ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [`ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/NewPartyWageModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/NewPartyWageModel.cs)
- [`code/DefaultSettlementTaxModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultSettlementTaxModel.cs)
- [`code/DefaultClanFinanceModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/code/DefaultClanFinanceModel.cs)
- [`file/Bannerlord_Vanilla_Economy_Mechanics.md`](https://github.com/GuihuaZhou/bannerlord/blob/main/file/Bannerlord_Vanilla_Economy_Mechanics.md)
