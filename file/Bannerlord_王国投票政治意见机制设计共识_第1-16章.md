# Bannerlord 王国投票政治意见机制设计共识

> 本文为当前确认的第 1～16 章设计稿。

## 第1章：核心定位

本系统的核心目标不是重新制作一套完整的王国政治模拟，而是利用 Bannerlord 原有的 Kingdom Decision、Clan Voting、Clan Influence、Clan Relation、Culture 等机制，将“投票”扩展解释为各 Clan 的政治意见表达。

投票同时承担两个作用：
1. 决定当前 Kingdom Decision 的政治意见；
2. 表现各 Clan 对国王及其他贵族的政治立场。

本系统不额外建立一套独立的“贵族忠诚度”“政治支持度”等长期数值。

> **Clan Voting = 政治意见平台**

## 第2章：King Clan Strength 与 King Clan Influence

### King Clan Strength

**King Clan Strength（国王宗族强度 / 国王影响力阈值）**代表该文化政治体系认为国王宗族需要达到什么程度的影响力，才能正常发挥国王的政治作用。

它不是国王当前拥有的实际力量，而是一个文化层面的标准值 / 阈值。

### King Clan Influence

**King Clan Influence（国王宗族实际影响力）**直接使用 Bannerlord 原有的 **Clan Influence**。国王宗族当前拥有的 Influence，即为国王的实际政治影响力。

记：
- `I` = King Clan Influence，国王实际影响力
- `K` = King Clan Strength，文化规定的国王影响力阈值

基本关系：
- `I = K`：国王实际影响力正好达到文化标准；
- `I > K`：国王实际影响力高于文化标准；
- `I < K`：国王实际影响力低于文化标准。

不额外创造“王权”“权威”等新的长期数值。

## 第3章：文化决定 King Clan Strength

当前确定的文化排序：

> **Empire > Aserai > Vlandia > Khuzait ≈ Sturgia > Battania > Nord**

该排序代表不同文化对国王宗族政治能力的要求程度不同，并不简单代表“谁的国王绝对更强”。具体数值尚未最终确定。

示例值（仅用于说明，不是最终数值）：

| Culture | King Clan Strength |
|---|---:|
| Empire | 100 |
| Aserai | 85 |
| Vlandia | 70 |
| Khuzait | 55 |
| Sturgia | 55 |
| Battania | 35 |
| Nord | 20 |

## 第4章：所有 Clan 都参与投票

所有相关 Clan 均参与政治意见表达，包括 King Clan 和普通 Clan。

普通投票：
- `+100` = 支持该 Decision；
- `-100` = 反对该 Decision。

投票首先被解释为该 Clan 对当前政治议题的立场，因此投票也是政治关系计算的基础。

## 第5章：King Clan Vote 的特殊含义

普通 Clan：
> Vote = 该 Clan 的政治意见。

King Clan：
> **Vote = 国王的政治意见 + 国王最终希望执行的方向。**

因此 King Clan Vote 本身就是国王意志。

不存在“投票结束后国王再决定是否推翻投票结果”的独立步骤。国王在投票阶段已经通过 King Clan Vote 表达自己最终希望这个 Decision 朝哪个方向执行。

## 第6章：Majority Opinion 与 King Intended Result

### 6.1 Majority Opinion

**Majority Opinion（多数政治意见）**根据所有 Clan 的投票计算。

> **King Clan 必须计入多数意见计算。**

### 6.2 King Intended Result

**King Intended Result（国王意图结果）**直接取 King Clan Vote。

因此：
- Majority Opinion = 王国政治多数希望什么；
- King Intended Result = 国王希望最终执行什么。

## 第7章：两种基本政治状态

### 7.1 政治共识

当：

`Majority Opinion = King Intended Result`

说明国王意志与王国政治多数一致，Decision 正常执行。

### 7.2 国王意志与政治多数冲突

当：

`Majority Opinion ≠ King Intended Result`

说明国王希望执行的方向与王国政治多数意见相反，此时进入强制执行判断。

## 第8章：Clan Relation 的基本定位

不建立新的 Noble Relation、Kingdom Loyalty、Political Support、King Loyalty 等独立关系变量。

直接使用 Bannerlord 原有的：

> **Clan Relation**

尤其是 **King Clan ↔ Other Clan Relation**，作为国王与其他贵族之间的政治关系基础。

因为国王本身就是一个 Clan，所以不需要额外建立“国王与贵族关系”。

## 第9章：政治意见一致与 Clan Relation

投票不仅决定 Decision，也可以影响 Clan Relation。核心判断是两个 Clan 的政治意见是否一致。

例如：
- King +100、Clan A +100 → 意见一致，有概率增加 Relation；
- King +100、Clan A -100 → 意见不同，有概率降低 Relation。

关系变化采用概率触发，而不是每次投票都必然发生，以避免关系快速极端化。

## 第10章：普通 Clan 之间的政治关系

政治意见不仅影响 King Clan ↔ Other Clan，也可以影响 Clan ↔ Clan。

- 意见一致 → 有概率增加 Relation；
- 意见不同 → 有概率降低 Relation。

这样 Kingdom Decision 可以成为王国贵族之间政治关系变化的自然来源。

## 第11章：Relation 与最终 Decision 结果相互独立

> **政治意见关系的变化，与最终 Decision 谁获胜，是两套独立逻辑。**

例如 King +100、Clan A -100，即使国王最终成功强制执行，Clan A 与国王的政治意见仍然相反，因此仍可能产生 Relation 负面影响。

因此：
- 强制执行成功 ≠ 贵族政治立场转变；
- 国王最终失败 ≠ 政治意见关系变化被取消。

## 第12章：完整投票逻辑流程

1. 生成 Kingdom Decision；
2. 所有 Clan 投票，包括 King Clan；
3. 记录政治意见；
4. 根据所有 Clan Vote 计算 Majority Opinion；
5. 直接从 King Clan Vote 取得 King Intended Result；
6. 比较 Majority Opinion 与 King Intended Result；
7. 根据政治意见一致性概率性修改 Clan Relation；
8. 若两者冲突，则使用 King Clan Influence 与 King Clan Strength 判断强制执行能力；
9. 强制执行成功则执行 King Intended Result 并消耗 King Clan Influence；
10. 无法强制执行则执行 Majority Opinion。

## 第13章：强制执行机制

### 13.1 所有文化均可以强制执行

文化差异不表现为某文化可以强制、某文化不能强制，而表现为 King Clan Strength 不同、强制执行所需实际影响力不同、超过或低于阈值后的政治效果不同，以及强制执行成本及政治后果可以不同。

> **所有文化理论上都允许国王强制执行。**

### 13.2 强制执行条件

`I ≥ K × Cforce`

其中：
- `I` = King Clan Influence；
- `K` = King Clan Strength；
- `Cforce` = 强制执行倍率。

例如 `K = 100`、`Cforce = 1.5` 时，需要 `I ≥ 150`。

### 13.3 Cforce 的意义

`Cforce` 是全局平衡参数，不是新的政治属性、政治资源或长期数值系统。

它用于区分：
- 达到正常政治能力标准；
- 强大到足以无视政治多数。

因此：
> **K 决定正常政治能力的标准。**
>
> **K × Cforce 决定强制执行所需要达到的标准。**

### 13.4 强制执行成本

强制执行成功后消耗 King Clan Influence，不建立新的“强制执行点”“王权点”等资源。

`Cost = BaseCost + OppositionCost`

其中：
- `BaseCost` = 强制执行基础成本；
- `OppositionCost` = 根据政治反对力量产生的额外成本。

反对国王的政治力量越强，国王强行推翻多数意见所需要付出的 Influence 越多。

具体 BaseCost、OppositionCost 及反对力量计算方式暂不锁死。

### 13.5 强制执行与 Relation 独立

强制执行成功不会取消之前根据政治意见产生的 Relation 变化。

> **强制执行只改变最终 Decision Result，不改变 Clan 的政治意见。**

### 13.6 King Clan Strength 对政治效果的影响

采用：

`E = (I - K) × (K / K₀)`

其中：
- `I` = King Clan Influence；
- `K` = King Clan Strength；
- `K₀` = King Clan Strength 基准值；
- `E` = 政治效果。

当 `I = K` 时，`E = 0`。

当 `I > K` 时，`E > 0`，表示国王实际影响力高于文化要求，产生正向政治效果。

当 `I < K` 时，`E < 0`，表示国王实际影响力低于文化要求，产生负向政治效果。

### 13.7 高 King Clan Strength 的特殊意义

King Clan Strength 越高：
1. 国王需要维持的实际影响力标准越高；
2. 同样幅度的实际影响力偏离，会产生更大的政治效果。

因此高 King Clan Strength 并不只是代表“国王更强”，而是代表该文化对国王政治能力要求更高，同时更加放大国王实际能力不足或过剩造成的政治影响。

### 13.8 K₀：King Clan Strength 基准值

`K₀` 是全局公式基准值，不是新的游戏属性、政治资源、实际政治能力或某个 Clan 的独立数值。

其唯一作用是：
> **控制 King Clan Strength 在政治效果公式中的整体尺度。**

公式：

`E = (I - K) × (K / K₀)`

因此：
- `K₀` 越大 → 同样的 `I-K` 产生的效果越小；
- `K₀` 越小 → 同样的 `I-K` 产生的效果越大。

> **K₀ 控制“国王超过/低于文化标准后，政治效果有多强”。**

### 13.9 K₀ 与 Cforce 的区别

| 参数 | 作用 |
|---|---|
| `K` | 每种文化自己的 King Clan Strength |
| `I` | 国王当前实际 King Clan Influence |
| `K₀` | 控制偏离 K 后政治效果的整体尺度 |
| `Cforce` | 控制强制执行需要达到的 Influence 倍率 |

> **K₀ 控制“超过/低于阈值后，效果有多大”。**
>
> **Cforce 控制“国王需要强到什么程度，才能强制执行”。**

## 第14章：明确不加入的系统

为了避免政治系统过度复杂，当前明确不建立：

### 14.1 独立的王权资源

不建立 Royal Authority、King Authority、王权点、政治权威值。国王政治能力直接使用 King Clan Influence。

### 14.2 独立的贵族忠诚度

不建立 Noble Loyalty，直接使用 Clan Relation。

### 14.3 独立的政治支持度

不建立 Political Support，政治支持直接由 Clan Vote / Political Opinion 表达。

### 14.4 独立的中央集权数值

当前不建立 Centralization，文化差异直接通过 King Clan Strength 体现。

### 14.5 King Clan 不从多数意见中排除

King Clan 必须参与 Majority Opinion 计算，因为国王也是王国政治共同体的一部分。

## 第15章：当前保留的全局参数与待定内容

### 15.1 已确定的核心参数

**King Clan Strength**：文化参数。当前排序为：

> **Empire > Aserai > Vlandia > Khuzait ≈ Sturgia > Battania > Nord**

具体数值待平衡。

**King Clan Influence**：直接使用 Bannerlord 原有 Clan Influence。国王宗族当前 Influence 即国王实际政治影响力。

### 15.2 已确定的公式

政治效果：

`E = (I - K) × (K / K₀)`

强制执行门槛：

`I ≥ K × Cforce`

强制执行成本：

`Cost = BaseCost + OppositionCost`

### 15.3 全局公式参数

**K₀：King Clan Strength 基准值**

用于调整 King Clan Strength 在政治效果公式中的整体尺度。它不是实际政治属性，也不是独立资源。

**Cforce：强制执行倍率**

用于区分正常发挥国王影响力与强行推翻多数意见两个层级。它不是实际政治属性，也不是独立资源。

### 15.4 尚未最终确定的内容

1. 各文化 King Clan Strength 的具体数值；
2. `K₀` 的具体数值；
3. `Cforce` 的具体数值；
4. `BaseCost` 的具体数值；
5. `OppositionCost` 的具体计算方式；
6. `E` 最终具体影响哪些游戏行为；
7. Clan Relation 每次投票触发变化的概率；
8. Relation 增减的具体幅度。

以上属于下一阶段的数值平衡与具体实现设计，当前阶段只确定整体逻辑。

## 第16章：最终设计逻辑总结

### 16.1 投票层

所有 Clan 投票：
> **Clan Vote = Political Opinion**

King Clan Vote：
> **Political Opinion + King Intended Result**

### 16.2 多数层

所有 Clan，包括 King Clan，共同计算 Majority Opinion，得到王国政治多数意见。

### 16.3 国王层

King Clan Vote 直接决定 King Intended Result，得到国王希望最终执行的方向。

### 16.4 冲突层

比较 Majority Opinion 与 King Intended Result：
- 一致 → 政治共识 → 正常执行；
- 不一致 → 国王意志与政治多数冲突 → 检查强制执行能力。

### 16.5 国王能力层

文化决定：
> **King Clan Strength = K**

游戏中的国王宗族当前：
> **King Clan Influence = I**

正常政治效果：

`E = (I - K) × (K / K₀)`

强制执行条件：

`I ≥ K × Cforce`

### 16.6 强制执行层

若 `I ≥ K × Cforce`：国王可以强制执行 King Intended Result，并消耗 `Cost = BaseCost + OppositionCost`。

若 `I < K × Cforce`：国王无法强制推翻政治多数，最终执行 Majority Opinion。

### 16.7 Relation 层

根据 Clan 与 Clan、Clan 与 King Clan 的政治意见是否一致，概率性产生 Clan Relation 增减。这一过程与最终 Decision Result 独立。

最终形成：

**Clan Voting → Political Opinion → Majority Opinion / King Intended Result → Political Conflict → King Clan Influence → Force Execution → Final Decision**

同时并行：

**Political Opinion → Clan Relation**
