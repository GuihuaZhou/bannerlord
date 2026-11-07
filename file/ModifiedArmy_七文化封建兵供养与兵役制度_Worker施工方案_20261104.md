# ModifiedArmy｜七文化封建兵供养与兵役制度调整——Worker 施工方案

> **状态**：依据 2026-11-04 讨论结果形成的实施规格，供 Worker 执行；**本文档本身不代表仓库已修改**。  
> **仓库**：`GuihuaZhou/bannerlord`  
> **实施模块**：`ModifiedArmy`  
> **目标文件**：`ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`  
> **实施原则**：优先 XML 最小变更；不新增服役计时系统，不无故重构 Model/Service。  
> **优先级**：供养成本、军事制度、免薪与返乡冷却参数均为本轮已商定的施工目标；农业效率、兵种质量训练参数另案讨论。

## 1. 任务目标与设计依据

本次将七文化封建兵政策统一为三条相互独立的规则：

1. **经济/土地供养效率**：T1～T2 统一成本，差异主要体现在 T3～T6；封建兵单兵 Hearth 供养成本划分四档。
2. **军事制度偏好**：`MilitaryDoctrine` 始终是 `fief / professional / mercenary` **三项**相对权重；帝国偏好职业兵与雇佣兵，阿塞莱重视职业兵与雇佣兵，瓦兰迪亚最偏好封建兵。本轮只调整诺德权重。
3. **封建兵役与返乡**：沿用 `Fief/Service` 现有 `freeWageDays` 和 `returnCooldownWeeks`；通过文化配置形成差异。不得把免薪期限误当作强制退役期限。

**文化经济发展与农业生产力的整体排序**（设计背景，本轮不修改农业模型）：帝国／阿塞莱 ＞ 瓦兰迪亚 ＞ 库赛特 ＞ 斯特吉亚 ＞ 巴旦尼亚 ＞ 诺德。

**封建军事制度成熟度排序**（与经济排序不同）：瓦兰迪亚 ＞ 帝国 ＞ 阿塞莱 ＞ 库赛特／斯特吉亚 ＞ 巴旦尼亚／诺德。

注意：发展排序不强制要求七文化使用七套不同数值。例如阿塞莱和瓦兰迪亚共用同一供养成本档次；帝国与阿塞莱的免薪天数也可以相同。实际总封建兵数取决于供养成本、可用 Hearth、封建预算份额和已有兵额，并非只由经济排序决定。

## 2. 必须修改：`Fief/FeudalSupply`（最终目标值）

单位：每名指定 Tier 封建兵持续占用的有效 Hearth（**不是招募时直接消耗这么多实际 Hearth**）。

| 文化 ID | 中文文化 | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---|---:|---:|---:|---:|---:|---:|
| `Culture.empire` | 帝国 | 1 | 2 | 4 | 7 | 11 | 22 |
| `Culture.aserai` | 阿塞莱 | 1 | 2 | 4 | 8 | 12 | 24 |
| `Culture.vlandia` | 瓦兰迪亚 | 1 | 2 | 4 | 8 | 12 | 24 |
| `Culture.khuzait` | 库赛特 | 1 | 2 | 5 | 9 | 14 | 28 |
| `Culture.sturgia` | 斯特吉亚 | 1 | 2 | 5 | 9 | 14 | 28 |
| `Culture.battania` | 巴旦尼亚 | 1 | 2 | 6 | 11 | 17 | 34 |
| `Culture.nord` | 诺德 | 1 | 2 | 6 | 11 | 17 | 34 |

**静态校验**：

- 七文化 T1=1、T2=2；同文化各级成本严格递增。
- T3～T6：帝国 ≤ 阿塞莱/瓦兰迪亚 ≤ 库赛特/斯特吉亚 ≤ 巴旦尼亚/诺德；后三档内部相等。
- 不新增文化供养倍率，不修改 `HearthEconomy/ProfessionalRecruitment`（职业兵各 Tier 的现有 Hearth 招募成本）。
- 不将文化成本再乘至 `GetEffectiveSupplyHearth()`，避免同一因素被重复计入。

### 2.1 可复制的 XML 目标片段

下面各块只替换**对应文化现有 `<Fief><FeudalSupply>` 与 `<Service>` 子节点**，不是替换完整 `<Culture>` 节点；应保留 `<Professional>`、`<Production>` 等原有设置。

**`Culture.empire`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="4" />
  <Tier Level="4" Hearth="7" />
  <Tier Level="5" Hearth="11" />
  <Tier Level="6" Hearth="22" />
</FeudalSupply>
<Service freeWageDays="35" returnCooldownWeeks="2"/>
```

**`Culture.aserai`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="4" />
  <Tier Level="4" Hearth="8" />
  <Tier Level="5" Hearth="12" />
  <Tier Level="6" Hearth="24" />
</FeudalSupply>
<Service freeWageDays="35" returnCooldownWeeks="2"/>
```

**`Culture.vlandia`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="4" />
  <Tier Level="4" Hearth="8" />
  <Tier Level="5" Hearth="12" />
  <Tier Level="6" Hearth="24" />
</FeudalSupply>
<Service freeWageDays="42" returnCooldownWeeks="2"/>
```

**`Culture.khuzait`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="5" />
  <Tier Level="4" Hearth="9" />
  <Tier Level="5" Hearth="14" />
  <Tier Level="6" Hearth="28" />
</FeudalSupply>
<Service freeWageDays="28" returnCooldownWeeks="2"/>
```

**`Culture.sturgia`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="5" />
  <Tier Level="4" Hearth="9" />
  <Tier Level="5" Hearth="14" />
  <Tier Level="6" Hearth="28" />
</FeudalSupply>
<Service freeWageDays="28" returnCooldownWeeks="2"/>
```

**`Culture.battania`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="6" />
  <Tier Level="4" Hearth="11" />
  <Tier Level="5" Hearth="17" />
  <Tier Level="6" Hearth="34" />
</FeudalSupply>
<Service freeWageDays="21" returnCooldownWeeks="3"/>
```

**`Culture.nord`**

```xml
<FeudalSupply>
  <Tier Level="1" Hearth="1" />
  <Tier Level="2" Hearth="2" />
  <Tier Level="3" Hearth="6" />
  <Tier Level="4" Hearth="11" />
  <Tier Level="5" Hearth="17" />
  <Tier Level="6" Hearth="34" />
</FeudalSupply>
<Service freeWageDays="21" returnCooldownWeeks="3"/>
```

## 3. 必须修改：`MilitaryDoctrine` 三项权重

| 文化 | 当前 Fief/Professional/Mercenary | 目标 Fief/Professional/Mercenary | 处理 |
|---|---|---|---|
| 帝国 | `1 / 4 / 3` | `1 / 4 / 3` | 保持；职业军队与雇佣兵均重要 |
| 阿塞莱 | `2 / 3 / 5` | `2 / 3 / 5` | 保持；职业兵与雇佣兵均重要 |
| 瓦兰迪亚 | `7 / 2 / 1` | `7 / 2 / 1` | 保持；封建军队为核心 |
| 库赛特 | `5 / 3 / 2` | `5 / 3 / 2` | 保持 |
| 斯特吉亚 | `6 / 4 / 1` | `6 / 4 / 1` | 保持 |
| 巴旦尼亚 | `3 / 1 / 1` | `3 / 1 / 1` | 保持 |
| **诺德** | **`4 / 5 / 1`** | **`5 / 2 / 1`** | **仅此行修改** |

诺德唯一需要的权重修改：

```xml
<MilitaryDoctrine fief="5" professional="2" mercenary="1"/>
```

### 3.1 三权重的实际代码含义（不得混淆）

- **AI 招募偏好**使用三项权重，加上战时、和平、围城、财政等情境乘数并归一化；并非固定兵员组成配比，也不是雇佣兵产能。
- **封建兵长期供养预算**和**共享基础兵源职业份额**只使用 `fief` 与 `professional`：
  - `FeudalBudgetHearth = floor(EffectiveSupplyHearth × fief / (fief + professional))`
  - `ProfessionalShare = professional / (fief + professional)`
- **`mercenary` 不参与本地共享兵源和 Hearth 封建预算分母**。
- 当前 AI 来源偏好由领主文化读取，而具体封邑供养/生产主要按定居点文化读取；不得顺手将两者错误统一。
- 诺德封建供养预算比例从 `4/9≈44.4%` 调整为 `5/7≈71.4%`；该变化同时影响职业兵份额。勿叠加新的诺德额外兵源增益。

## 4. 必须修改：`Fief/Service` 兵役差异化

| 文化 | 当前 `freeWageDays` | 目标 | 当前 `returnCooldownWeeks` | 目标 |
|---|---:|---:|---:|---:|
| 瓦兰迪亚 | 42 | **42** | 3 | **2** |
| 帝国 | 28 | **35** | 3 | **2** |
| 阿塞莱 | 28 | **35** | 3 | **2** |
| 库赛特 | 28 | **28** | 1 | **2** |
| 斯特吉亚 | 28 | **28** | 3 | **2** |
| 巴旦尼亚 | 28 | **21** | 3 | **3** |
| 诺德 | 28 | **21** | 3 | **3** |

### 4.1 本轮沿用已有机制，不增设新的兵役系统

- `FiefServiceBatch.ServiceDays`：按战役日累计已服役天数；批次身份与来源封邑保持现有记录及对账机制。
- `FreeWageDays`：士兵在此天数内免发普通工资；超过后继续服役，但应正常计薪。它**不是强制返乡日或最大服役天数**。
- `returnCooldownWeeks`：一批士兵**实际返回来源封邑时**创建 `FiefReturnBatch`，按周冷却；冷却完成后回到就绪兵员。不是返乡行军时间，也不是在役批次到期时间。
- 保留当前“战损不等同返乡”的结算原则；不得为了数值调整重新引入 `MaxServiceWeeks` 到期删除部队、固定 `targetServiceDays` 或新批次账本。
- **现有 AI 自动战时休整/和平复员默认关闭**，本轮不要擅自打开，也不要宣称改 XML 就会到期自动返乡。当前若有主动归还或已允许的返乡流程，它们会按现有路径运行。

### 4.2 存档/生效语义（重点验收）

1. 新征召的在役批次在创建时写入该文化 `FreeWageDays` 快照。旧存档中**已经在役的批次仍保留原有快照**，不自动追溯修改，否则会改变已经成立的兵役义务。
2. 新的返乡冷却在**实际发生返乡时**读取来源封邑当前 `Service.ReturnCooldownWeeks` 并写入 `FiefReturnBatch.WaitWeeks`；已处于冷却中的批次保持原保存的剩余冷却周数。
3. 更新 `FeudalSupply` 会直接改变当前兵员持续供养占用；如果某村因现役高级兵/原有规模而暂时超额，**不能通过删兵、判阵亡、丢批次等手段硬性清空超额**。应保留真实兵额，由现有新增和升级检查阻止进一步超额，后续正常返乡/战损/变动逐步修正。
4. XML 修改后是否需要重新开始战役才能加载配置，以项目现有加载方式为准；Worker 必须明确测试所用的是**重新加载的配置**，不应把旧内存中缓存的数值误认为修改无效。

## 5. 关键代码路径与影响面

**预期唯一需要编辑的文件**：

- `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`
  - 各文化 `<Fief><FeudalSupply>`：共 7×6 条成本。
  - 各文化 `<Fief><Service>`：7 条配置。
  - 诺德 `<MilitaryDoctrine>`：1 条权重。
  - 同步修正相关注释，例如诺德“常备驻军权重略高于封邑兵”的旧描述已不再成立。

**需核对但原则上不修改的代码**：

- `common/MilitaryConfig.cs`：`ParseDoctrine`、`ParseTierCosts(requireStrictIncrease: true)`、`ParseService`、`ProfessionalShare`、三权重文化偏好读取。
- `Fief/Models/VillageFiefData.cs`：可用 Hearth、占用、封建预算。占用包括就绪、在役及冷却批次。
- `Fief/Models/PartyData/FiefPartyReinforcement.cs`：每日补员、每周升级及升级后增量供养检查；保留最近已修复的 roster 快照枚举安全逻辑。
- `Fief/Models/Data/FiefServiceBatch.cs`：实际服役计时、批次 `FreeWageDays` 快照。
- `Fief/Models/PartyData/FiefPartyReturn.cs`、`Fief/Models/Data/FiefReturnBatch.cs`：返乡时读取配置，记录冷却批次，并按周恢复。
- `ForceManagement/Services/PartyForceCandidateService.cs`：AI 封建/职业/雇佣兵偏好和情境修正。
- `ForceManagement/Models/WarRestPolicyModel.cs`、`ForceManagement/Services/WarRestPlanningService.cs`：现有兵役压力参照免薪阈值，但自动休整默认关闭。

**严格不在本轮施工范围**：

- 不改 `ModifiedPolitics` 村庄农业生产模型；农业效率的具体文化系数尚未讨论确定。
- 不改 `fiefPartyTemplates.xml` 的兵种构成，也不改封建兵实际升级概率和职业兵训练速度；“发达文化兵种质量更好”是下一阶段设计方向，不是本次授权的数值。
- 不改其它六文化 MilitaryDoctrine；不改职业兵招募 Hearth 成本、不改佣兵市场供给、不改佣兵价格。
- 不启用 `EnableAutomaticDemobilization`、`EnableDemobilizationRouting`，也不自行启用战时休整或跨城增援。
- 不重构批次身份、保存结构、工资模型、返乡事务或政治模块依赖。

## 6. Worker 具体施工步骤

1. 从仓库当前默认分支读取上述 XML 和相关代码，不依赖历史设计文档中的过时数值。
2. 备份或保留修改前 diff；**仅按本文第 2、3、4 节修改**对应文化节点，保持 XML 层级及其他配置完全不变。
3. 修正受变更影响的文化 XML 注释（尤其诺德）。不要修改与任务无关的 AI 参数。
4. 解析完整 XML，核对七个 Culture ID 各出现一次；每个 `FeudalSupply` 具有 T1～T6 各一项且严格递增；七条 `Service` 参数均与目标一致。
5. 检查 `MilitaryDoctrine` 三项权重：帝国/阿塞莱的 `mercenary` 数值必须保持 `3/5`；只有诺德从 `4/5/1` 改为 `5/2/1`。
6. 若环境允许，按仓库已有方式执行 `ModifiedArmy` Release 构建和相关自动化门禁，并检查 XML 是否进入实际部署目录；如编译无关，仅配置改动也要说明结果。
7. 用最小有效游戏存档验证供养/补员/升级、免薪和返乡/冷却。不能完成游戏内验证则明确标记 **BLOCKED**，不得用编译成功代替游戏内通过。
8. 交付修改文件清单、统一 diff、静态校验结果、构建结果、实机验证结果及未验证项目；不要自行扩大改动范围。

## 7. 验收用例与预期结果

### 7.1 静态配置检查（必须 PASS）

| ID | 验收项 | 通过标准 |
|---|---|---|
| C01 | XML 完整性 | 有效 XML，七个文化 ID 唯一，T1～T6 完整 |
| C02 | 供养成本 | 与第 2 节 7×6 表逐格一致 |
| C03 | 等级连续性 | 每文化 T1～T6 成本严格递增 |
| C04 | 分档一致 | 阿塞莱=瓦兰迪亚；库赛特=斯特吉亚；巴旦尼亚=诺德 |
| C05 | 军事制度 | 只有诺德 `MilitaryDoctrine` 发生变化；三个权重均存在 |
| C06 | 兵役参数 | 七文化免薪与冷却均与第 4 节一致 |
| C07 | 非范围保护 | 其他职业兵、雇佣兵、AI 阈值、训练产能和原有开关不变 |
| C08 | 注释一致 | 不再有与新诺德权重相冲突的注释 |

### 7.2 逻辑计算抽查（可静态复核）

假设**某单个封邑关联村庄**的有效 Hearth = 600，封建兵供养占用当前为 0，且只用于 T5 封建兵（仅用于验证预算公式的理论上限，**不是实际兵种组成或生产速度**）：

| 文化 | Fief/Professional | 封建供养预算 `floor(600×fief/(fief+professional))` | T5 单兵成本 | 理论最多 T5 |
|---|---:|---:|---:|---:|
| 帝国 | 1/4 | 120 | 11 | 10 |
| 阿塞莱 | 2/3 | 240 | 12 | 20 |
| 瓦兰迪亚 | 7/2 | 466 | 12 | 38 |
| 库赛特 | 5/3 | 375 | 14 | 26 |
| 斯特吉亚 | 6/4 | 360 | 14 | 25 |
| 巴旦尼亚 | 3/1 | 450 | 17 | 26 |
| 诺德 | 5/2 | 428 | 17 | 25 |

核对重点：

- 帝国供养效率最好，但因封建预算仅 20%，**封建兵总体容量不一定最多**。
- 瓦兰迪亚依赖封建制度，理论 T5 规模较大。
- 诺德封建份额提高后，不应把份额再次施加到每兵成本上。
- 预算是各村约束；不可擅自把同一封邑各村额度混成能互相任意填补的总池。

### 7.3 游戏内回归（按可行性逐项验证）

| ID | 场景 | 预期 |
|---|---|---|
| G01 | 新战役/重载配置，查看不同文化供养额 | 相同有效 Hearth 下，各级每兵占用按目标表计算 |
| G02 | 封邑每日补员 | 新增人员不突破村庄总可用供养额及封建预算 |
| G03 | 每周自动升级 | T3～T6 成本变化计入升级增量；现有快照枚举修复无回归崩溃 |
| G04 | 诺德 AI 选择兵源 | 三项偏好读取为 `5/2/1`；实际仍受可用人数、财政、情境限制 |
| G05 | 新征召批次免薪 | 瓦兰迪亚 42、帝国/阿塞莱 35、库赛特/斯特吉亚 28、巴旦尼亚/诺德 21 天；跨阈值后正常计薪 |
| G06 | 主动返乡及冷却 | 帝国、阿塞莱、瓦兰迪亚、库赛特、斯特吉亚 2 周；巴旦尼亚、诺德 3 周；归还军籍、Hearth、冷却批次一致 |
| G07 | 旧存档中已在役批次 | 旧 `FreeWageDays` 快照保持原值；不被新配置意外覆盖 |
| G08 | 旧存档中已冷却批次 | 原保存 `WaitWeeks` 不被覆盖；新返乡批次才使用新冷却值 |
| G09 | 改动后预算暂时不足 | 不凭空删兵、不丢来源批次；后续新增和升级受到供养检查 |
| G10 | AI 自动复员/战时休整设置 | 未擅自启用关闭中的开关；改参数不等于强制到期返乡 |

## 8. 完工回报格式

Worker 最终应提供：

```text
[ModifiedArmy 七文化封建兵调整]
改动文件：...
核心改动：7文化×6档供养成本；7文化Service；Nord Doctrine 4/5/1→5/2/1
未修改：职业兵/雇佣兵/农业/兵种模板/自动复员开关...
静态校验：C01...C08（逐项 PASS/FAIL）
构建及发布：PASS/FAIL/BLOCKED（命令及简要输出）
游戏内验证：G01...G10（逐项 PASS/FAIL/BLOCKED）
存档兼容与已知风险：...
修改 Diff / 文件路径：...
```

**验收标准**：优先以可证实的修改和验证结果报告；严禁把尚未游戏实测的项目写为 PASS。

## 9. 仓库核对来源

- [当前军事配置 `militaryConfig.xml`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml)
- [军事配置解析 `MilitaryConfig.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs)
- [封建兵供养预算 `VillageFiefData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [补员与升级 `FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [服役批次 `FiefServiceBatch.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefServiceBatch.cs)
- [返乡结算 `FiefPartyReturn.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReturn.cs)
- [AI 兵源偏好 `PartyForceCandidateService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Services/PartyForceCandidateService.cs)
- [战时休整 `WarRestPolicyModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ForceManagement/Models/WarRestPolicyModel.cs)

---

**本次交付结论**：按已商定参数先完成 `ModifiedArmy` XML 最小修改。文化农业效率及更细致的封建兵质量、训练比例不在本轮施工授权内。
