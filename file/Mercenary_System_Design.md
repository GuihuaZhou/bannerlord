# Bannerlord 雇佣兵系统设计

## 1. 设计原则

- 雇佣兵必须归属于游戏现有文化，不新增专门的“佣兵文化”。
- 雇佣兵文化由士兵来源决定，与雇主文化无关。
- 不要求每个文化都有雇佣兵，也不要求各文化佣兵数量对称。
- 历史原型决定兵种是否合理；游戏性决定是否值得实现。
- 雇佣兵主要服务于其他文化，因此允许与来源文化正规军兵种定位相似。
- 第一版不考虑定居点距离某个佣兵输出文化越近，对应佣兵生成概率越高。

## 2. 雇佣兵统一属性规则

与同 Tier 正规军相比：

- **装备质量更低**
- **武器/战斗熟练度更高**
- **工资更高**
- **招募费用更高**

“装备更低”是相对同 Tier 正规军而言，不代表佣兵必须轻甲。中装、重装佣兵均可存在。

佣兵的价值主要来自职业经验和熟练度，而不是国家提供的顶级制式装备。

## 3. 各文化佣兵兵种

### Vlandia

历史原型：诺曼/西欧军事冒险者、职业军士等。

- 雇佣骑士
- 锁甲军士
- 锁甲弩手

### Sturgia

历史原型：罗斯/北方军事人员。

- 持盾斧兵
- 双手斧兵

### Khuzait

历史原型：突厥、库曼、钦察等草原军事人员。

- 中/重装雇佣枪骑兵
- 中/重装雇佣骑射手

### Aserai

- 轻装雇佣标枪骑兵
- 装备方向：马匹、盾牌、标枪、单手剑
- 重点熟练度：Riding、Throwing
- 不需要弓；原则上不配置长枪，以保持兵种定位

### Empire

- 不设置本文化佣兵。
- Empire 是大陆最重要的国际佣兵消费市场。

### Battania

- 当前不设置佣兵。

### Nord

- 当前不设置佣兵。

---

## 4. 定居点佣兵生成规则

佣兵生成机制由：

`Settlement.Culture`

决定，而不是由：

`OwnerClan.Culture`

或：

`Kingdom.Culture`

决定。

例如：

> Vlandia 占领 Empire 城镇后，该城镇仍然按照 Empire 佣兵市场规则生成佣兵。

反过来，Empire 占领 Vlandia 城镇，该城镇仍按照 Vlandia 的本土佣兵规则运行。

这样可以表现长期形成的地方佣兵市场不会随着一次领土易手立即消失。

## 5. Empire 佣兵市场

所有 `Empire Culture` 定居点都可以生成**全部已定义佣兵**：

- Vlandia 雇佣骑士
- Vlandia 锁甲军士
- Vlandia 锁甲弩手
- Sturgia 持盾斧兵
- Sturgia 双手斧兵
- Khuzait 雇佣枪骑兵
- Khuzait 雇佣骑射手
- Aserai 轻装标枪骑兵

第一版所有 Empire 定居点使用统一佣兵池。

暂时不计算：

- 与 Vlandia 的距离
- 与 Sturgia 的距离
- 与 Khuzait 的距离
- 与 Aserai 的距离
- 是否属于边境地区

## 6. 其他文化的本土佣兵

拥有本文化佣兵的非 Empire 定居点，仅有**极低概率**生成本文化佣兵。

规则：

- Vlandia 定居点 → 仅 Vlandia 佣兵
- Sturgia 定居点 → 仅 Sturgia 佣兵
- Khuzait 定居点 → 仅 Khuzait 佣兵
- Aserai 定居点 → 仅 Aserai 佣兵
- Battania 定居点 → 不生成佣兵
- Nord 定居点 → 当前不生成佣兵

设计目标是：

> 大部分佣兵离开本土前往 Empire 出售军事服务，但本土仍可能偶尔出现少量自由职业军人。

## 7. 文化生成概率

文化生成概率与兵种权重是两个不同概念。

### 文化生成概率

表示一次佣兵刷新判定时，该文化定居点是否生成佣兵。

第一版先确定相对关系：

`Empire : 本土输出文化 ≈ 8 : 1`

即 Empire 的佣兵生成能力应远高于 Vlandia、Sturgia、Khuzait、Aserai 本土。

此前讨论的：

`Empire = 40%`

`Home Culture = 5%`

仅作为候选值，**暂不定为最终数值**。

绝对概率必须结合以下机制共同确定：

- 刷新周期
- 单次生成数量
- 每座定居点佣兵容量
- 佣兵补充速度

Battania、Nord 当前为：

`SpawnChance = 0`

## 8. 佣兵兵种权重

兵种 Weight 用于已经成功触发佣兵生成后，决定具体生成哪一种佣兵。

| Culture | Mercenary | Weight |
|---|---|---:|
| Vlandia | 锁甲军士 | 20 |
| Vlandia | 锁甲弩手 | 15 |
| Vlandia | 雇佣骑士 | 8 |
| Sturgia | 持盾斧兵 | 20 |
| Sturgia | 双手斧兵 | 12 |
| Khuzait | 中/重装雇佣枪骑兵 | 12 |
| Khuzait | 中/重装雇佣骑射手 | 8 |
| Aserai | 轻装雇佣标枪骑兵 | 10 |

总 Weight：

`105`

### Empire 实际兵种占比

Empire 使用完整佣兵池，因此约为：

- Vlandia 锁甲军士：19.0%
- Sturgia 持盾斧兵：19.0%
- Vlandia 锁甲弩手：14.3%
- Sturgia 双手斧兵：11.4%
- Khuzait 雇佣枪骑兵：11.4%
- Aserai 轻装标枪骑兵：9.5%
- Vlandia 雇佣骑士：7.6%
- Khuzait 雇佣骑射手：7.6%

### 本土文化权重

本土定居点仍使用相同 Weight，只过滤掉其他文化兵种。

例如 Vlandia：

- 锁甲军士：20
- 锁甲弩手：15
- 雇佣骑士：8

因此成功生成 Vlandia 本土佣兵后，三者比例约为：

- 锁甲军士：46.5%
- 锁甲弩手：34.9%
- 雇佣骑士：18.6%

Sturgia：

- 持盾斧兵：62.5%
- 双手斧兵：37.5%

Khuzait：

- 雇佣枪骑兵：60%
- 雇佣骑射手：40%

Aserai：

- 轻装标枪骑兵：100%

## 9. Tier 设计

普通佣兵原则上最高为：

`T5`

包括：

- Vlandia 锁甲军士
- Vlandia 锁甲弩手
- Sturgia 持盾斧兵
- Sturgia 双手斧兵
- Khuzait 雇佣枪骑兵
- Khuzait 雇佣骑射手
- Aserai 轻装标枪骑兵

### T6 雇佣兵

T6 佣兵可以存在，但必须属于极少数顶尖职业军人，不能让所有佣兵树自然升级至 T6。

当前确定：

**Vlandia 雇佣骑士可作为 T6 佣兵。**

规则：

- 极低概率直接在佣兵市场生成
- 不由普通 T5 佣兵升级获得
- 装备质量低于同 Tier 的 Vlandia 正规 T6 重甲骑士
- 战斗熟练度高于同 Tier 正规军
- 招募价格显著更高
- 工资显著更高

其 T6 身份主要代表长期战争经验与职业能力，而不是更好的国家制式装备。

## 10. 第一版实现结构

建议核心参数：

`MercenarySpawnChance[Culture]`

用于控制不同定居点文化的佣兵生成概率。

`MercenaryTemplate.Weight`

用于控制具体佣兵兵种的相对生成概率。

基本逻辑：

```text
if Settlement.Culture == Empire:
    Pool = AllMercenaryTemplates
else:
    Pool = MercenaryTemplates where MercenaryCulture == Settlement.Culture

根据 MercenarySpawnChance 判断是否生成
→ 从 Pool 中按照 Weight 抽取兵种
→ 生成对应佣兵
```

第一版保持简单，不加入地理距离、边境、当前占领者文化等额外修正。
