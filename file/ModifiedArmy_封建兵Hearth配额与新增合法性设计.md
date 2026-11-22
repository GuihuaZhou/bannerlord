# ModifiedArmy：封建兵 Hearth 配额与新增合法性设计

> 独立方案二。本文仅讨论封邑封建兵的 Hearth 供养配额、兵种比例和新增兵源合法性，不包含 AI Party 创建、财政预算豁免、军事制度补员偏好等另一份方案的内容。
>
> 设计依据：`GuihuaZhou/bannerlord` 仓库 `main` 分支现有的 `ModifiedArmy` 封邑兵源实现，以及已经确定的设计要求。本文是实施设计，**不是已经完成的代码修改**。

## 一、目标和不可变原则

1. **严格执行两级 Hearth 分配**：先由 `militaryConfig.xml` 中的文化军事制度划分封建兵可用 Hearth，再由 `fiefPartyTemplates.xml` 中的各兵种 `weight` 划分其供养份额。
2. **XML 权重代表供养 Hearth 份额，不是生产人数比例，也不只是每日随机抽取概率。** 不得用其他兵种未使用的份额补足当前兵种。
3. **各村庄独立核算**：每个村庄依据自己的有效 Hearth 和其绑定封邑的模板分配配额；不跨村借用供养份额。
4. **外出服役只保留（占用）原籍 Hearth 供养义务**，不释放该兵种的配额。Ready、Serving、Cooling 三种状态应合计参与供养核算。
5. **一切真正新增封建兵供养的写入都必须经过同一个公共 API 的最终合法性验证和提交**；初始化、每日补员以及无原籍登记的兵员安置均不得直接绕过该入口写入。
6. **原籍士兵返乡、冷却结束不属于新增供养**。它们继续执行已有的状态转移，不重复审批 Hearth 或把原籍士兵当新兵；应维持准确的兵员守恒。
7. **升级导致 Hearth 成本增加时，需要检查新增供养成本**；不允许以升级绕过配额。
8. 不引入业务回滚、冻结、资源预留、永久事务日志、额外兵员身份台账；不强制删除现有存档中的超额兵员。

## 二、代码现状与根因

### 2.1 现有军事制度总配额基本正确

`Fief/Models/VillageFiefData.cs` 的 `GetFeudalBudgetHearth()` 使用：

```text
H_effective = floor(Village.Hearth + MobilizedHearth)
H_fief = floor(H_effective × FiefWeight / (FiefWeight + ProfessionalWeight))
```

这是本次设计的**第一级分配**，予以保留。`MercenaryWeight` 不参与这个 Hearth 分母。

`MobilizedHearth` 用来补回士兵外出服役后从村庄人口数值中动员出去的部分。因此，在没有其他人口变化时，士兵离村不应该使有效供养规模骤减。注意此处的动员人口数与按 Tier 核算的供养 Hearth 成本是不同概念，不要把二者混为同一扣减。

### 2.2 当前全部封建兵共享总额度

目前 `GetOccupiedSupplyHearth()` 将所有合法封建兵的供养成本相加；`CanIncreaseSupply()` 只检查：

- 村庄总体可用 Hearth；
- 该村庄封建兵总体预算还剩多少 Hearth。

它没有按 XML 兵种分支检查供养配额，因此一种兵可以占用其他兵种的剩余 Hearth。

### 2.3 初始化与日常生产都没有保证按 Hearth 权重分配

`Fief/Models/PartyData/FiefPartyReinforcement.cs` 中：

- `InitializeFiefTroopsByTemplateWeights()` 用权重分配**士兵人数**，然后检查全池预算；由于 Tier 成本不同，最终 Hearth 占比会偏离 XML 权重。
- `WithdrawFiefTroopEntries()` 根据可生产兵种的 `weight` 加权随机选择每一名新兵；当部分兵种尚未解锁时，剩余兵种获得了更高的生产概率，且没有分支 Hearth 上限。
- `PerformReinforcement()` 选择当前总预算有空余的村庄，直接写入 Ready Roster，没有按兵种分支审批。
- `PerformAutoUpgrade()` 检查升级的总体 Hearth 增量，却没有检查所属分支的 Hearth 上限。

`Fief/Models/FiefTroopQuery.cs` 已能合并 Ready、Serving、Cooling 数量，可作为供养占用的权威查询基础，无须新建平行库存。

## 三、两级 Hearth 配额规则

### 3.1 第一级：军事制度划分封建兵总供养

对每个村庄 `v`：

\[
H_{effective,v}=\max(0,\lfloor Hearth_v+MobilizedHearth_v\rfloor)
\]

\[
H_{fief,v}=\left\lfloor H_{effective,v}\cdot
\frac{W_{fief}}{W_{fief}+W_{professional}}\right\rfloor
\]

军事制度权重取当前封邑适用的文化配置，并沿用现有代码中的文化解析规则。非法分母、缺失配置应视为配置错误，而非悄悄按全部 Hearth 供养。

### 3.2 第二级：XML 按兵种分支分配封建兵 Hearth

对该封邑模板中所有 `weight > 0` 的兵种根节点 `i`：

\[
H_{quota,v,i}\approx H_{fief,v}\cdot\frac{weight_i}{\sum_j weight_j}
\]

分配方法采用**最大余数法**：先逐项向下取整，再把余数 Hearth 按小数余数从大到小、兵种 ID 的稳定顺序分配，确保各项整型配额之和恰好等于 `H_fief,v`。

- `weight = 0` 不获得供养额度，也不允许新生产。
- 所有有效正权重根节点均参与分母，**包括兵营等级尚未解锁的兵种**。
- 兵营未解锁的配额保留为空额，不能转给低阶兵种。
- 各村庄独立计算；不跨村、跨分支转移未使用额度。
- 配额是 Hearth 单位，不是人头单位；每个具体士兵按其当前 Tier 的 `FeudalSupply` 成本核算。

**瓦兰迪亚示例**：当某村庄的封建兵总供养预算是 900 Hearth，模板权重分别为骑士 1、轻骑兵 1、封建步兵 2、普通步兵 3、征召弩手 2 时，五个分支的 Hearth 配额分别为 **100、100、200、300、200**。人数由各兵种的实际 Tier 成本决定，而不是按 1:1:2:3:2 分配人头。

## 四、分支定义及供养占用

### 4.1 一个 XML 根节点对应一个供养分支

`fiefPartyTemplates.xml` 的每一条有效 `<troop id="..." weight="..." />` 对应一个供养分支。该根节点本身，以及其在合法升级树中产生的封建兵，都归入同一供养分支。

为避免同一实际士兵被重复计算：

- 加载模板时建立**确定性的、只读的** `CharacterObject → XML 根节点` 归属映射。
- 对升级树交叉或一个兵种可从多个根节点抵达的情况必须检查和报告；不能把同一人算入多个配额。
- 原则上要求有效分支可唯一归属；若模板存在无法确定原籍分支的路径，应修正模板或明确兼容策略，**不能假装只靠当前 `CharacterObject` 就能恢复每个历史士兵的真实来源**。
- 已有保存的 `FiefServiceBatch`、村庄 Ready/Cooling 状态应尽量复用；不得为普通补员增加第二套持久化士兵库存。若确需处理历史歧义，先明确存档兼容影响。

### 4.2 已占用 Hearth 包含全部状态

对村庄 `v`、分支 `i`：

```text
H_occupied[v,i] =
    Σ Ready    士兵数量 × 当前 Tier 的 Hearth 成本
  + Σ Serving  士兵数量 × 当前 Tier 的 Hearth 成本
  + Σ Cooling  士兵数量 × 当前 Tier 的 Hearth 成本
```

其中计入伤兵；不能只按健康待征召兵统计。查询来源沿用 `FiefTroopQuery.Query(..., All)` 及现有服役、返乡批次，避免另建缓存式资源账本。

分支的新增空间：

```text
H_remaining[v,i] = max(0, H_quota[v,i] - H_occupied[v,i])
```

真正添加一批 `troop × count` 时同时满足：

1. 兵种属于合法 XML 分支，且该来源允许本次新增；
2. `H_occupied[v,i] + count × tierCost(troop) <= H_quota[v,i]`；
3. 封建兵全池 Hearth 以及当前村庄有效 Hearth 不被突破；
4. 村庄、模板、兵营等级、兵员来源等其他必要条件合法。

任一分支已超额时，停止对该分支新增供养，但不强制清除或遣散存量兵。

### 4.3 服役期间的原籍占用保持不变

出征时，Ready 减少、Serving 增加；`MobilizedHearth` 与村庄实际 Hearth 按现有动员机制配合调整，但该士兵仍持续占用**原村庄、原供养分支**的 Hearth 配额。

例如某分支配额 300 Hearth：Ready 占用 100、Serving 占用 200，则剩余 **0 Hearth**；即使村庄现场 Ready 士兵很少，也不能据此再次补充该分支。

返乡时 Serving → Cooling，冷却结束 Cooling → Ready，都是身份和状态转移，**不是再次供养审批**。必须避免双计数，但不得因为比例暂时超额就阻止已有原籍士兵回乡。

## 五、统一公共新增 API

### 5.1 唯一入口的适用范围

所有**真正增加村庄封建兵供养占用**的场景，统一走 `FiefPartyManager` 或其委托的封邑兵源服务公开入口；调用方不得在绕过审批后直接 `AddToCounts()` 或 `ReturnBatches.AddTroops()`。

建议接口示意（命名与最终签名以现有项目组织为准）：

```csharp
FiefTroopAdmissionResult TryAddFeudalTroops(
    Settlement sourceFief,
    Settlement sourceVillage,
    CharacterObject troop,
    int requestedCount,
    FiefTroopAdmissionDestination destination,
    MobileParty sourceParty = null);
```

`destination` 用于明确这是新生产至 Ready，还是合法的**新安置**至 Cooling；不代表已有原籍士兵每次返乡也需要重新审批。

返回信息至少包括：实际新增人数、拒绝/部分批准原因、村庄、兵种及其供养分支。只有一个入口能**最终确认合法并完成新增写入**；调用方的预先查询只能用于候选排序，不能代替 API 内的实时校验。

### 5.2 API 内部的统一合法性步骤

1. 验证封邑及来源村庄确实绑定，读取当前有效模板和文化军事制度。
2. 验证兵种属于正权重的合法封建分支，满足实际生产或安置的条件。
3. 计算村庄有效 Hearth、封建兵总 Hearth、该分支 XML Hearth 配额。
4. 汇总同村庄 Ready、Serving、Cooling 当前占用，并计算该兵种 Tier 单位成本。
5. 按`requestedCount`、分支剩余额度、总体剩余额度得出本次允许人数。
6. 对合法数量直接提交相应的 Ready/Cooling 写入；如来自 Party 的新安置，还要同时处理已被批准的来源士兵扣减，保证数量守恒。
7. 返回**真实成功数量**；未批准士兵不改变其来源状态。

只允许部分成功，不设置补偿、回滚或业务冻结。如果来源状态在正式提交前变动，重新读取并按最新状态批准；不要复用已经过期的数量假设。

### 5.3 正常原籍状态转移不走新增审批

| 变化 | 是否新增供养 | 处理方式 |
|---|---:|---|
| 新游戏初始化 → Ready | 是 | 必须走公共新增 API |
| 每日补员 → Ready | 是 | 必须走公共新增 API |
| 无原籍登记的 Party 兵员安置 → Cooling | 是 | 必须走公共新增 API |
| Ready → Serving（征召） | 否 | 继续现有原籍服役登记与动员流程 |
| Serving → Cooling（原籍返乡） | 否 | 继续正常返乡状态转移，不重复审批 |
| Cooling → Ready（冷却结束） | 否 | 继续正常状态转移，不重复审批 |
| 战死或永久脱离封建兵体系 | 减少占用 | 依据现有服役核对释放对应供养空间 |
| 同一分支内升级 | 可能增加成本 | 检查分支 Hearth 增量 |

对于“安置”，须先确定是**有原籍记录的归队**，还是**未登记士兵首次纳入封邑供养体系**。只有后者才属于新增加入。

## 六、每日补充与初始化

### 6.1 每日补员

保留 `militaryConfig.xml` 中已有 `Fief.Production` 的 `dailyRate` 及现有小数生产进度机制；它决定每天最多生成多少封建兵。

具体兵种选择应改为：

1. 枚举该封邑绑定村庄中的有效正权重 XML 分支。
2. 读取每个分支实际剩余 Hearth；当前兵营尚未解锁的分支不参与当天生产，也不转让配额。
3. 在**确实还能供养至少一人**的分支中，优先补足相对供养缺口大的分支，避免简单按 XML 权重反复随机而导致长期偏斜。
4. 每次通过公共新增 API 写入实际士兵；拒绝或不足时选择其他合法、有缺口的分支，或结束此次补员。
5. 生产完成后记录实际人数，维持现有生产进度核算，不对既有产能参数进行额外平衡调整。

推荐缺口排序依据可用 `剩余Hearth / 分支配额`，再考虑该分支生产一个当前 Tier 士兵所需的 Hearth；稳定同分排序依据 XML 次序和兵种 ID。不得仅用 Ready 人数计算缺口。

### 6.2 新游戏初始化

`InitializeFiefTroopsByTemplateWeights()` 不再先按 `weight` 分配人头，而是直接在各分支的 Hearth 配额内，按 Tier 成本计算合法人数。每个新增兵源都通过同一 API 进入 Ready。

不能因部分兵种尚未解锁，就把它们的 Hearth 份额分给其他兵种。允许产生因整数 Tier 成本造成的小额剩余 Hearth。

## 七、自动升级及存档兼容

- 现有 `PerformAutoUpgrade()` 应先确定士兵供养分支，再校验升级后 Tier 成本的增量是否超出该分支和总体额度。
- 升级**不增加人数**，因此不是普通新增新兵；但不能以升级形式绕过新增 Hearth 占用限制。
- 同分支升级保持原分支；跨分支升级不得悄悄转移供养配额。对配置中含交叉树的情况，应通过确定性映射和升级合法性规则处理，不能含糊放行。
- 对旧存档已有超额士兵：保留真实士兵，按其可确认分支计入占用；以后只限制新增加入，不强行清理、重置或重新配兵。
- 若有效 Hearth 因正常游戏变化减少，分支配额可以低于既有占用；这种超额状态属于**暂停该分支新增的资格条件**，不意味着冻结封邑业务，更不需要回滚其他正常行为。
- 优先复用既有 `VillageFiefData`、`FiefServiceBatch`、`FiefReturnBatch` 及查询方法。新增分支配额应按实时数据和 XML 推导，不新建可独立变化的持久化 Hearth 账本。

## 八、主要修改位置

| 代码文件 | 修改重点 |
|---|---|
| `Fief/Models/VillageFiefData.cs` | 保留第一级军事制度预算；增加按 XML 分支读取的 Hearth 配额、占用与新增许可计算 |
| `Fief/Models/FiefPartyTemplate.cs` | 提供正权重分支定义、唯一升级树归属和模板合法性检查 |
| `Fief/Models/PartyData/FiefPartyData.cs` / `Fief/Models/FiefPartyManager.cs` | 暴露唯一封建兵新增入口及查询，不另建重复库存 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` | 初始化按 Hearth 分配；每日补员按分支缺口；升级检查成本增量 |
| `Fief/Services/FiefTroopPlacementService.cs` | 区分有原籍归队与无原籍新安置；新安置必须通过公共新增 API |
| `Fief/Models/FiefTroopQuery.cs` | 复用并必要时扩充 Ready/Serving/Cooling 分支占用查询 |
| `Fief/Models/PartyData/FiefPartyReturn.cs` | 保持原籍返乡语义，不因为配额改造而误走新增审批 |
| `ModuleData/fiefPartyTemplates.xml` | 保留原权重数值，明确权重对应供养 Hearth；检查升级分支歧义 |

仅在现有类职责不足时增加一个小型内部供养计算/准入服务，禁止堆叠多层事务协调器。

## 九、禁止事项

1. 禁止直接把 XML `weight` 当作人数比例或唯一随机生产概率，而忽略 Hearth 配额。
2. 禁止跨分支、跨村庄借用未使用的 Hearth；未解锁兵种的份额不能重新分配。
3. 禁止在补员时把 Serving 兵员当成“村里不在场，因此未占用供养”的空额。
4. 禁止初始化、每日补员、无原籍安置绕过公共新增 API 直接增加封建兵供养。
5. 禁止把正常返乡和冷却完成当成新兵重复扣占 Hearth。
6. 禁止因旧存档超额而自动删兵、转换兵种、回滚或冻结业务。
7. 禁止创建与权威 Ready/Serving/Cooling 记录并行的第二套持久化供养台账。
8. 禁止擅自修改各文化军事制度权重、单兵 Hearth 成本、每日生产率及其他非本方案平衡参数。

## 十、验收测试

1. **两级预算**：同一村庄根据本文化 `FiefWeight/ProfessionalWeight` 得到正确封建兵总额度，再按模板 `weight` 得到正确分支额度；两级整数取整结果稳定。
2. **不同 Tier**：骑士和普通步兵的 Hearth 占比符合 XML 分支配额，但人数不必符合 XML 权重比例。
3. **外出服役**：大量封建兵从 Ready 转入 Serving 后，对应分支剩余供养额不变，不触发该分支重复自动生产。
4. **战损**：当 Serving 士兵真实死亡且服役记录核对后，只有所属分支释放相应供养；每日补员优先填补该分支缺口。
5. **返乡和冷却**：Serving→Cooling→Ready 期间总人数和分支供养成本保持守恒，无二次 Hearth 审批。
6. **低级兵营**：未解锁高级兵的 Hearth 配额保留，普通兵不能借用；解锁后可逐渐按对应配额补齐。
7. **配额不足**：只剩 6 Hearth 而目标兵种每人需要 8 Hearth 时，不允许新增该兵种；未批准士兵不消失。
8. **旧存档超额**：原有兵保留，但该分支不能继续扩张；其他分支可在自身额度内正常补员。
9. **升级**：同分支升级所增加的 Hearth 经过合法性检查；不允许借用其他分支的份额。
10. **新安置**：未登记兵员只能在公共新增 API 批准后纳入 Cooling；被拒绝者留在原 Party。
11. **兼容性**：存档载入前后 Ready/Serving/Cooling 数量与供养口径一致，不重复生产或遗失兵员。
12. **代码审计**：所有增加村庄封建兵供养的路径都能追踪到唯一公共 API；未修改原籍返乡与冷却正常状态转移的语义。

## 十一、实施顺序

1. 建立 XML 根节点分支与现有士兵的稳定归属映射，检查升级树交叉歧义。
2. 在现有村庄供养模型内加入第二级按分支 Hearth 配额及占用查询。
3. 实现唯一新增封建兵公共 API，并将现有新兵写入点统一接入。
4. 调整新游戏初始化和每日补员，使其按分支 Hearth 缺口分配，而非按人数权重随机。
5. 调整升级增量和无原籍安置的准入检查；保持原籍返乡、冷却结束的既有状态转移。
6. 使用既有存档和新游戏分别验证比例、战损、出征、返乡、升级以及超额状态。

---

**最终要求：军事制度决定封建兵总供养 Hearth；XML 的每个兵种 `weight` 决定自己的供养 Hearth；Ready、Serving、Cooling 始终共同占用原籍分支额度；所有新增加入由统一公共 API 审批；正常返乡与冷却结束不重复审批。**

## 代码参考

- [`VillageFiefData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [`FiefPartyReinforcement.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyReinforcement.cs)
- [`FiefTroopQuery.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefTroopQuery.cs)
- [`FiefTroopPlacementService.cs`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Services/FiefTroopPlacementService.cs)
- [`fiefPartyTemplates.xml`](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ModuleData/fiefPartyTemplates.xml)
