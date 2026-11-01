# ModifiedArmy：Hearth 劳动力与文化封建兵供养制度优化——Worker 实施方案 V1.1

> 状态：**设计已确认，尚未施工**；V1.1 修订：取消 UI 的 Workforce 改名，全部现有UI文本保持不变。  
> 基准仓库：`GuihuaZhou/bannerlord`，截至核对时最新提交 `bc01585378c516d2baa8071da538932f057a81cb`（2026-11-01 核对）  
> 实施模块：`ModifiedArmy`  
> 目的：在不重构既有村庄兵源、封建兵服役批次、职业兵招募事务和第五阶段 UI 布局的前提下，统一“劳动力”含义、简化职业兵扣除量、引入文化专属封建兵供养成本及 MilitaryDoctrine 配额硬约束。

## 0. 已确认的最终决策（不可自行修改）

1. **Hearth 的游戏设计含义是壮年劳动力/劳动力当量，不是字面上的户数**。这仅属于经济系统设计解释，代码、存档字段、Bannerlord `Village.Hearth`、XML 内数值属性 `Hearth` 和**所有现有UI显示名称、文案、语言词条均保持现状**。**不实施任何将 `Hearth` 改名为 `Workforce` 的UI修改。**
2. **职业兵无论 T1～T6，每成功招募 1 人永久消耗 1 Hearth**。此成本只发生一次；原有至少保留 100 Hearth 的保护规则与事务回滚保持不变。
3. **封建兵不直接按供养值永久扣除 Hearth**。其已登记的待役、在役、返乡冷却兵持续占用封建兵供养额度；出征、返乡、战死的 `MobilizedHearth` 及实际 Hearth 结算保持原样。
4. **七种文化分别拥有完整的 T1～T6 封建兵供养成本表**；巴旦尼亚和诺德完整保留原全局标准，其他文化依次降低成本，见第 2 节；不额外叠加文化效率倍率。
5. **利用现有 `MilitaryDoctrine.fief` 与 `MilitaryDoctrine.professional` 权重，为每个村庄设置封建兵供养额度硬上限**。`mercenary` 不计入本地劳动力份额。原有封邑兵总人数上限照旧存在；两者须同时满足。
6. **封建兵不能占用全部可分配劳动力；职业兵没有新增的固定人数硬上限**，继续按实际 Hearth、最低 100 保护量、封建兵现有占用计算是否能扣除。未用完的封建兵配额不强制生产封建兵。
7. **已存在封建兵因配额变化而超标时不删除、不强制解散、不清空任何在役/返乡批次**；停止新增和导致供养占用进一步增加的升级，等待兵员自然变化或劳动力恢复。
8. **文化来源沿用当前实现：封邑所在地文化 `GetDoctrine(fiefSettlement)`**，不改成统治王国、领主或家族文化。村庄的规则以其当前绑定的城镇/城堡封邑为准。
9. 本次只优化经济与配置；**不修改第五阶段「封建兵管理」UI的布局、名称、文字、菜单或文本资源**，不改变 AI 的战争/和平策略，也不推翻第四阶段身份转换的已调整行为。

## 1. 当前源码核对结果

| 已实现代码/行为 | 当前状态 | 本次要求 |
|---|---|---|
| `MilitaryConfig.cs`：`HearthEconomyConfig.FeudalSupply` | 全文化共用 T1=2,T2=5,T3=10,T4=15,T5=20,T6=40 | 改为每文化 `<Fief><FeudalSupply>` |
| `MilitaryConfig.cs`：`HearthEconomyConfig.ProfessionalRecruitment` | 2/4/6/8/10/12 | 改为 1/1/1/1/1/1 |
| `CulturalMilitaryDoctrine.ProfessionalShare` | `professional / (fief + professional)` | 继续用于每日兵源产能分配，同时以同一权重计算长期封建兵配额 |
| `FiefPartyData.CalculateSizeLimit()` | `baseLimit + villageBonus × BoundVillages.Count` | 保留，不把新配额替换掉该人数上限 |
| `VillageFiefData.GetOccupiedSupplyHearth()` | 累计待役 + 服役批次 + 冷却批次（按各自兵种等级） | 保留三类口径，调用文化专属供养成本 |
| `VillageFiefData.GetEffectiveSupplyHearth()` | `floor(Village.Hearth + MobilizedHearth)` | 完全保留，避免在役兵被重复扣算 |
| `VillageFiefData.CanIncreaseSupply()` | 仅检查原始剩余供养空间 | 增加 **封建兵文化配额余额** 限制 |
| `FiefPartyReinforcement.PerformReinforcement/PerformAutoUpgrade` | 通过供养空间检查新增/升级 | 同时强制执行新增配额；升级只看新旧单位成本差额 |
| `FiefPartyManager.TryConsumeProfessionalHearth()` | 检查 `Hearth - 100` 和原始免费供养容量，并按文化无关成本扣除 | 仍按该事务，成本改为每兵 1；**不得误用封建兵配额余额限制职业兵** |
| `SettlementManpowerProductionBehavior.GetProductionForecast()` | 根据权重分配每日**产能**，而非长期供养额度 | 保留其产能算法；新增配额是**独立的存量约束** |

核对涉及路径（相对仓库根目录）：

- `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`
- `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`
- `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Services/FiefTroopConversionService.cs`（回归检查）

## 2. 确定的全部文化供养成本（不得改动）

**每名封建兵占用的 Hearth 供养额（设计含义为壮年劳动力当量）**，须按兵种当前 Tier，且随升级改变；所有数值都是正整数。

| Culture ID | 文化 | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---|---:|---:|---:|---:|---:|---:|
| `Culture.vlandia` | 瓦兰迪亚 | **1** | **2** | **4** | **7** | **11** | **22** |
| `Culture.empire` | 帝国 | **1** | **3** | **5** | **9** | **14** | **26** |
| `Culture.aserai` | 阿塞莱 | **1** | **3** | **6** | **10** | **16** | **30** |
| `Culture.khuzait` | 库赛特 | **2** | **4** | **8** | **12** | **18** | **35** |
| `Culture.sturgia` | 斯特吉亚 | **2** | **4** | **8** | **12** | **18** | **35** |
| `Culture.battania` | 巴旦尼亚 | **2** | **5** | **10** | **15** | **20** | **40** |
| `Culture.nord` | 诺德 | **2** | **5** | **10** | **15** | **20** | **40** |

**职业兵成本**（全文化、全等级）：

| Tier | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---:|---:|---:|---:|---:|---:|
| 一名新职业兵的实际 Hearth 消耗 | 1 | 1 | 1 | 1 | 1 | 1 |

升级封建兵按**新等级供养成本 − 原等级供养成本**增加占用，不把两档完整成本叠加。

## 3. 配额模型：逐村而不是整个封邑统一挤占

### 3.1 权重来源

使用已存在 `CulturalMilitaryDoctrine.FiefWeight` 与 `ProfessionalWeight`。当前 XML 文化权重实际为：

| 文化 | fief | professional | mercenary | 封建份额（只用前两项） |
|---|---:|---:|---:|---:|
| 瓦兰迪亚 | 7 | 2 | 1 | 7/9 |
| 帝国 | 1 | 4 | 3 | 1/5 |
| 阿塞莱 | 2 | 3 | 5 | 2/5 |
| 库赛特 | 5 | 3 | 2 | 5/8 |
| 斯特吉亚 | 6 | 4 | 1 | 6/10 |
| 巴旦尼亚 | 3 | 1 | 1 | 3/4 |
| 诺德 | 4 | 5 | 1 | 4/9 |

### 3.2 单村计算定义

对每个绑定村庄 `v`：

```
H_actual(v) = floor(v.Village.Hearth)                       // 原版实际 Hearth
H_mobilized(v) = vData.MobilizedHearth                      // 已临时离乡、在役的 Hearth
H_effective(v) = max(0, floor(v.Village.Hearth + H_mobilized(v)))
H_protected = 100                                          // 现有职业扣户最低保护线
H_allocatable(v) = max(0, H_effective(v) - H_protected)

F = doctrine.FiefWeight
P = doctrine.ProfessionalWeight
FeudalBudget(v) = floor(H_allocatable(v) * F / (F + P))    // 使用 long/double 防止整数溢出或精度问题

FeudalOccupied(v) = Σ(待役兵数 × 该文化对应Tier成本)
                  + Σ(在役批次数 × 该文化对应Tier成本)
                  + Σ(返乡冷却批次数 × 该文化对应Tier成本)

RawFreeSupplyHearth(v) = H_effective(v) - FeudalOccupied(v)  // 当前原始共享供养余额
FeudalBudgetRemaining(v) = FeudalBudget(v) - FeudalOccupied(v)
```

注意：

- 采用当前的 `H_effective = Hearth + MobilizedHearth`，这是**记账抵消**，并非凭空新增劳动力。不得再额外从 `FeudalBudget` 扣一次在役兵的人数。
- `FeudalOccupied` 必须保留待役、在役和冷却三部分的完整记账；同一个兵种的不同村庄批次各自属于其源村，不得按 CharacterObject 全封邑简单汇总后丢失村庄来源。
- 配额**逐村**生效：A村用不完的额度不得被B村直接侵占；城镇/城堡可以汇总展示，但判定必须逐村。
- 要阻止一次补员/升级新增供养量 `delta > 0`，应同时要求：

```
RawFreeSupplyHearth(v) >= delta
FeudalBudgetRemaining(v) >= delta
```

- `delta <= 0` 的兵员损耗、返乡、转换退出等正常减少行为不应因超额被阻止；升级若使供养成本下降，也不应受新增配额限制。原有的身份记录完整性检查仍然执行。
- 在业务层保留原有 `FiefPartyData.GetFiefTroopLimit()` 人数上限，**不可用配额值覆盖 `_totalLimit`**；新的封建额度是附加限制，而非以“劳动力数直接作为最大士兵人数”。
- 如果 `F = 0`，封建预算为0；`P=0,F>0` 则封建预算为全部可分配份额。当前解析器已保证 `F+P > 0`，保留此校验。

### 3.3 不设置职业兵“数量配额”硬上限

职业兵招募的真实容量仍按现有：

```
ProfessionalFree(v) = min(
  max(0, floor(v.Village.Hearth - 100)),
  max(0, RawFreeSupplyHearth(v))
)
```

每名职业兵只消耗1 Hearth，严格通过现有职业兵事务判断，不能拿 `FeudalBudgetRemaining` 当职业兵可用量。军事制度权重仅给**封建兵长期存量占用**新增硬约束；原有权重分配每日产能的代码继续保持。

这里不是预锁死职业兵的份额：如果封建兵数量不满，职业兵仍可消耗其未占用劳动力。职业兵持续扣Hearth后，封建兵预算可能动态下降；原有封建兵因此超额时仅阻止继续新增/升级，**不强制清兵**。这一行为属于已确定设计，并非漏洞。

## 4. XML 修改方案

### 4.1 全局 HearthEconomy 仅保留职业兵与税收配置

从全局 `<HearthEconomy>` 删除原 `<FeudalSupply>`，不能保留两个相互竞争的权威来源。职业兵全部 1：

```xml
<HearthEconomy>
  <ProfessionalRecruitment>
    <Tier Level="1" Hearth="1" />
    <Tier Level="2" Hearth="1" />
    <Tier Level="3" Hearth="1" />
    <Tier Level="4" Hearth="1" />
    <Tier Level="5" Hearth="1" />
    <Tier Level="6" Hearth="1" />
  </ProfessionalRecruitment>
  <FeudalTaxRelief wageRatio="0.50" />
</HearthEconomy>
```

`FeudalTaxRelief` 原有职责、值和结算逻辑不变。

### 4.2 每个文化 Fief 下增加 FeudalSupply

示例瓦兰迪亚（其余6文化按第2节完整表填写）：

```xml
<Culture id="Culture.vlandia">
  <MilitaryDoctrine fief="7" professional="2" mercenary="1"/>
  <Professional><!-- 现有训练节点不变 --></Professional>
  <Fief>
    <FeudalSupply>
      <Tier Level="1" Hearth="1" />
      <Tier Level="2" Hearth="2" />
      <Tier Level="3" Hearth="4" />
      <Tier Level="4" Hearth="7" />
      <Tier Level="5" Hearth="11" />
      <Tier Level="6" Hearth="22" />
    </FeudalSupply>
    <Service><!-- 保留原有属性 --></Service>
    <Production><!-- 保留原有 Town / Castle / TownPort --></Production>
  </Fief>
</Culture>
```

上面 `Professional`/`Service`/`Production` 的注释仅表示**插入位置**，Worker 必须保留原文件中已有完整节点和属性，不能把注释示例当作可运行的完整文化配置。

### 4.3 配置解析校验

- `ParseHearthEconomy()` 不再读取全局 `FeudalSupply`。
- `ParseDoctrine()` 读取 `Required(fiefNode, "FeudalSupply")`，并在 `CulturalMilitaryDoctrine` 保存 `IReadOnlyDictionary<int,int>` 或等价只读映射。
- 复用 `ParseTierCosts()` 的正整数与重复Tier检查；补充**精确覆盖T1～T6且六档严格递增**的封建兵配置校验（因为本次七文化表完全满足该要求）。不能悄悄使用默认文化或老的全局表替代缺失项。
- 职业兵仍按统一表读取；必须确保六个Tier都配置为1。编译期间可增加显式验证；不要误对职业兵配置添加“严格递增”约束。
- 参数、规则和注释说明更新为 `FeudalSupply` 各文化独立；`MilitaryDoctrine` 权重同时管每日产能与长期封建兵额度。

## 5. C# 修改建议（最小侵入）

### 5.1 `MilitaryConfig.cs`

建议在 `CulturalMilitaryDoctrine` 增加：

```csharp
public IReadOnlyDictionary<int, int> FeudalSupply { get; internal set; }

public int GetFeudalSupplyCost(CharacterObject troop)
{
    // 使用当前文化的 FeudalSupply 严格按 troop.Tier 查表；
    // 缺失或非正数直接抛错，不回退到全局价格。
}
```

将 `HearthEconomyConfig.FeudalSupply` 的全局权威及对应无文化 `GetFeudalSupplyCost` 用法改为文化版本；保留职业兵统一价格、税减免等字段。不要让 `VillageFiefData` 自己猜测文化。

### 5.2 `VillageFiefData.cs`

将供养相关读取接入已经解析的 `CulturalMilitaryDoctrine`（可以调整方法参数，也可以采用结构清晰的封邑上下文，但避免隐藏全局状态）：

- `GetOccupiedSupplyHearth(...)`：按封邑文化成本对待役/在役/冷却完整计价。
- `GetFreeSupplyHearth(...)`：**仍表示真实共享余额** `H_effective - FeudalOccupied`，不改成封建配额余额。
- `GetSupplyDeficitHearth(...)`：保留真实共享余额缺口语义。
- 新增诸如 `GetFeudalBudgetHearth(...)`、`GetFeudalBudgetRemainingHearth(...)` 的只读计算。
- `CanIncreaseSupply(...)`：额外检查封建兵预算余额。所有新增分支必须复用这一判定；**不能在这里写入/扣除 Hearth**。

由于 `GetOccupiedSupplyHearth` 的成本不再依赖全局 `HearthEconomyConfig`，各个调用点必须一致传递封邑文化。优先以**绑定的城镇或城堡 Settlement** 获取 `GetDoctrine(boundFief)`。若转换、跨村来源或占领变更后出现旧批次，按当前封邑文化及当前规则计算新的占用；不要修改批次身份记录。

### 5.3 `FiefPartyData.cs`

调用上述村庄级方法统一解析文化；同步更新：

- `GetTotalOccupiedSupplyHearth()`
- `GetTotalFreeSupplyHearth()`
- `GetTotalSupplyDeficitHearth()`
- `CanProduceAnyFiefTroop()`

**强制逐村判定，禁止只算全封邑的余额后让一个村庄透支。**

### 5.4 `FiefPartyReinforcement.cs`

必须覆盖两个方向：

1. `PerformReinforcement` 与 `ProduceDailyFiefTroopsDetailed`（含职业转封建的现有入口）：生成每个新封建兵时使用新的 `CanIncreaseSupply(...)`。
2. `PerformAutoUpgrade`：`delta = cultureCost(newTroop) - cultureCost(oldTroop)`；若 `delta > 0`，同时满足实际共享余额与文化封建配额余额；若 `delta <= 0` 不因配额超标拒绝升级。

保留当前“仅对Tier<4执行自动升级”的既有行为；本次不重做升级树和训练规则。

### 5.5 `FiefPartyManager.cs` / `SettlementManpowerProductionBehavior.cs`

- 保持 `TryConsumeProfessionalHearth` 的原有事务检查、分村扣除、回滚逻辑，职业价格新表使实际扣除 = 成功招募人数 × 1。
- 该事务读取 `GetFreeSupplyHearth` 时，**必须是原始共享余额，不是封建配额余额**。
- `OnTroopRecruited`、职业招募提议消费、驻军招募及第四阶段专业/封建身份转换所走的职业扣户路径，检查**一次成功招募只扣1次**；不要添加第二处重复扣户逻辑。
- `CalculateVillageSupply()` 继续读取实际共享余额，作为每日共享产能计算的依据；不要把它换成封建配额余额。
- `GetProductionForecast()` 的权重产能分配算法保留，不要因为新增封建配额再乘一遍相同权重，造成双重缩减。`FiefAllocation` 仍是产能预估，实际补员受配额约束。
- 保留原有每日提供职业兵候选、成功/失败回滚、封建补员记录的现有语义。

### 5.6 UI不修改（V1.1变更，最高优先级）

本次**不修改任何游戏UI或文字显示**。`FiefMenuBehavior.cs` 的 `Hearth supply` 等现有显示保持原样，不把 `Hearth`、`Actual Hearth`、`Effective Hearth`、`Occupied Supply`、`Free Supply` 或其他现有英文/中文词条替换为 `Workforce`。

- 不修改已有 `TextObject` 文本、文本资源、翻译键、菜单名称、窗口布局或 ViewModel 绑定。
- 不新增 `Feudal Budget` 等配额UI字段或提示；本阶段仅在内部计算、原有日志或现有调试信息中按既有机制验证新配额。
- 若其他经济代码因方法签名调整影响UI数据查询，必须保留现有UI的数据显示与操作功能，但不能因此改变文案或显示口径。
- 新文化供养与配额制度均为**内部逻辑改动**；Worker 不得将既有UI改名为 `Workforce`。

## 6. 实现举例：600劳动力村庄

**假设**该村当前 `H_effective = 600`、`MobilizedHearth = 0`、没有职业兵新扣除、无既存封建兵、未触及封邑人数上限。

```
保护量 = 100
可分配量 = 600 - 100 = 500
封建供养配额 = floor(500 × fief / (fief + professional))
某一等级的供养人数上限 = floor(封建配额 / 该文化该等级单兵成本)
```

| 文化 | fief/professional | 封建配额 | 全部T3最多 | 全部T4最多 | 全部T6最多 |
|---|---|---:|---:|---:|---:|
| 瓦兰迪亚 | 7/2 | 388 | 97 | 55 | 17 |
| 帝国 | 1/4 | 100 | 20 | 11 | 3 |
| 阿塞莱 | 2/3 | 200 | 33 | 20 | 6 |
| 库赛特 | 5/3 | 312 | 39 | 26 | 8 |
| 斯特吉亚 | 6/4 | 300 | 37 | 25 | 8 |
| 巴旦尼亚 | 3/1 | 375 | 37 | 25 | 9 |
| 诺德 | 4/5 | 222 | 22 | 14 | 5 |

此表是**劳动力配额的理论极值**，不是实际自动生成数量。实际可出现的兵员还受：原有整个封邑人数上限、每日补员率、兵种模板、兵营、文化条件、伤员、村庄状态和现有在籍兵员影响。

### 6.1 动态占用例子

以瓦兰迪亚为例，600有效劳动力、封建预算388、T3单兵成本4：已有90名T3封建兵，`FeudalOccupied=360`；还可以新添7名T3（7×4=28），第8名应当拒绝。升级一名T3→T4的额外供养是 `7-4=3`，如果预算剩余不足3则升级不能发生。

### 6.2 职业兵优先消耗后的动态预算

以帝国为例：`H_effective=600` 时封建配额100；如职业招募成功并从该村永久消耗100 Hearth、且其他因素不变，则有效劳动力降至500，封建配额变为80。已有封建兵占用90时，处于**超额10**；新封建兵补员和增加占用的升级暂停，既有兵不删除。职业兵仍按真实共享供养余额及100保护线判定，不能因此自动被新的封建配额拒绝。

## 7. 施工顺序（避免改变其他系统）

1. **先更新XML配置和解析**：7文化T1～T6配置、职业兵全1、校验和启动加载测试。
2. **新增统一文化封建占用计算器**：把现有所有T1～T6供养读取改为文化规则，并确保三状态全部计入。
3. **加入逐村的封建额度计算**：用原有权重与 `EffectiveHearth - 100` 计算预算；实现只读预算与余额。
4. **接入补员/升级/身份转换入口**：集中检查新供养增量的两个条件（真实余额、文化配额余额）；现有职业兵招募不能被封建配额误拦截。
5. **静态审查、编译、游戏存档回归**：确保之前四阶段功能和第五阶段现有UI均无回归；**UI显示名称与文案不变**。

## 8. 必须通过的验收用例

| 编号 | 场景 | 预期 |
|---|---|---|
| C01 | 加载七文化配置 | 七套FeudalSupply全部含T1～T6，职业兵每Tier均为1，配置加载成功 |
| C02 | 配置漏一Tier或重复Tier | 启动明确报错，不静默回退全局表 |
| C03 | 600有效劳动力，七文化空村测试 | 单村配额分别为388/100/200/312/300/375/222（文化顺序同第6节） |
| C04 | 瓦兰迪亚T3，已有90名、预算388 | 可再添7名，第8名失败（忽略人数上限和产能，直接测试预算边界） |
| C05 | 巴旦尼亚/诺德任一T6 | 每人占用40，不得意外变成22或其他文化成本 |
| C06 | 待役、在役、冷却各一批同Tier | 三状态都计入总封建占用，无重复、无遗漏 |
| C07 | 同一兵种分别来自两个村庄 | 两村分别判预算，不得相互挪用村庄配额 |
| C08 | T3→T4升级 | 只增加新旧差额；超配额则拒绝升级且兵员状态保持不变 |
| C09 | 已在籍封建兵因扣职业兵或改文化超额 | 不删除、不中断服务/返乡；新封建兵与新增供养的升级暂停 |
| C10 | 城镇/城堡的职业兵征召 | 每人真实扣除1 Hearth；分村扣除和100保护线、回滚行为正常 |
| C11 | 玩家/AI驻军职业补员、原版招募事件及阶段四转换 | 扣户不重算、不重复扣除；回滚一致 |
| C12 | 封建兵征召→服役→返乡→冷却→再待役 | `MobilizedHearth` 守恒，无凭空生出/吞掉Hearth；配额占用持续正确 |
| C13 | 封建兵战死 | 不再返还已确认为损失的动员Hearth，服务记录一致，供养占用随损失下降 |
| C14 | 职业兵可用、封建配额已满 | 只封锁新封建兵；职业兵如满足共享容量/保护量仍允许招募 |
| C15 | 封建配额有余额、职业产能不足 | 不自动占满封建兵配额；每日产能预测不因重复乘权重减半 |
| C16 | 不同文化的城镇/城堡、占领变更 | 使用当前封邑文化；旧在役批次保留，超配额仅阻止新增 |
| C17 | 保存退出重新读档 | 人数、来源批次、Hearth、MobilizedHearth不因新配置而异常变化 |
| C18 | UI回归 | 保留原有 Hearth 等所有现有UI文本和页面行为，无 Workforce 替换、无新增配额UI |

**重要：**C04等“理论配额”用例应在纯模型/测试数据下绕过日补员率与封邑人数上限进行单独验证；游戏实测则遵循所有既有约束。不要用游戏内某一天没有生成兵员直接推断配额算法有误。

## 9. 不在本阶段范围内

- 不创建新的人口/劳动力持久化系统或额外职业/封建兵源池。
- 不修改职业兵自动训练速度、城镇/城堡驻军配额、玩家征召权限或AI策略。
- 不改封建兵最低招募批量、免费服役天数、返乡冷却、工资豁免或税收减免比例。
- 不改文化定义来源（仍为封邑所在地文化），不实现王国征服后的文化同化。
- **完全不改UI**：不重做封建兵窗口，不替换 Hearth 显示用语，不新增配额信息，不变更任何文字与布局。
- 不引入新的 FeudalSupplyMultiplier 与额外倍率；直接使用每文化6档绝对整数。
- 不根据新配额回溯性删除旧兵、不对旧存档大量进行批次迁移。

## 10. Worker 提交要求

1. **直接施工**，不是只输出一份方案；如本地仓库有新提交、与本文件基准不同，先检查差异并按最终确认的业务规则实施。
2. 输出修改文件与核心方法列表，说明 XML 配置来源如何从全局变为文化专属。
3. 确认“每日补员权重”与“长期封建占用配额”是两件事，且没有重复扣减产能；说明职业兵使用的是共享余额而非封建预算余额。
4. 编译并尽可能运行可用的测试；不能实际进游戏时明确标出未验证项，不能以静态检查冒充运行验收。
5. 明确记录旧存档超配额时的处理方式，并核查角色/村庄变更、返乡和身份转换的连续性。
6. 不增添未获用户确认的新经济参数或文化倍率；严格遵守第0节及第2节的最终数值。
7. **明确确认：UI维持现状，禁止将 Hearth 改为 Workforce，不新增 UI 字段/菜单/配额提示。**

---

**最终判定：**`MilitaryDoctrine` 确定村庄劳动力中允许封建兵占用的份额；每文化 `FeudalSupply` 确定每个Tier的占用成本；原有 `baseLimit + villageBonus×村庄数` 约束封邑总人数；职业兵每次成功招募只扣除1实际Hearth，继续按共享余额与最低100保护量运行。所有规则均在既有数据与事务上实施。
