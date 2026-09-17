# 王国政治集团（Bloc）机制设计

## 1. 基本定位

`Clan` 是王国政治的基本单位。

`Bloc` 是由某个 `Leader Clan` 为核心，自然形成的王国内部政治集团。

基本规则：

- 每个 Clan 同时最多属于一个 Bloc；
- Clan 允许处于 `Independent Clan` 状态；
- Bloc 不预设“主战派”“反王派”“大贵族派”等固定类型；
- Bloc 由 Clan 的政治立场、实力、关系和文化差异自然形成；
- King Clan 也可以成为 Bloc Leader。

---

## 2. Clan Political Profile

每个 Clan 拥有当前政治特征：

| 维度 | 含义 | 来源 |
|---|---|---|
| `Authority` | 贵族自治 ↔ 强王权 | Culture + Leader Trait + Political Experience |
| `WarTendency` | 反战 ↔ 主战 | WarDisposition |
| `WarPotential` | 战争潜力弱 ↔ 强 | WarPotential |
| `CrownAlignment` | 对现任 King Clan 的态度 | Clan Relation |
| `Culture` | 文化政治差异 | Clan.Culture |

`WarPotential` 体现 Clan 的军力、经济和持续作战能力，也自然区分大贵族与小贵族的利益。

不设置：

```text
Fief
Policy
ForeignAlignment
```

为独立 Political Profile 维度。

---

## 3. Authority

`Authority` 表示 Clan 在：

```text
贵族自治 ←→ 强王权
```

之间的政治位置。

公式：

```text
Authority
=
CultureAuthorityBase
+ LeaderTraitAuthorityModifier
+ PoliticalExperienceModifier
```

### 3.1 CultureAuthorityBase

表示 Clan 所属文化长期形成的王权观念。

它决定政治起点，但不决定最终立场。

例如：

```text
Empire
→ 基础更接受强王权

Battania / Nord
→ 基础更倾向贵族自治
```

因此，同文化 Clan 会有相似政治底色，但可以形成完全不同的实际立场。

### 3.2 LeaderTraitAuthorityModifier

由当前 Clan Leader 的原生 Trait 修正。

主要使用：

```text
Authoritarian  威权主义
Oligarchic     寡头主义
Egalitarian    平等主义
```

基本方向：

```text
Authoritarian
→ Authority 上升
→ 更接受强王权
```

```text
Oligarchic
→ Authority 下降
→ 更倾向贵族集团掌握权力
```

```text
Egalitarian
→ Authority 下降
→ 更反对权力高度集中
```

`Oligarchic` 与 `Egalitarian` 都可能反对强王权，但政治原因不同。

### 3.3 PoliticalExperienceModifier

表示 Clan 长期政治经历形成的立场变化。

例如：

```text
国王反复强制执行
长期与 King Clan 政治冲突
Clan 政治意见长期受到压制
```

则：

```text
PoliticalExperienceModifier ↓
```

Clan 逐渐倾向贵族自治。

反之：

```text
长期与国王合作
从王权体系中持续获益
国王长期保护或支持该 Clan
```

则：

```text
PoliticalExperienceModifier ↑
```

Clan 更接受强王权。

该值应缓慢变化，并限制最大幅度。

---

## 4. Trait 与 Political Profile

Trait 不直接增加新的 Political Distance 维度，而是作为现有 Political Profile 的来源。

```text
Hero Trait
    ↓
Political Profile
    ↓
Political Distance
    ↓
Bloc
```

### Authority

```text
Authoritarian  威权主义
Oligarchic     寡头主义
Egalitarian    平等主义
        ↓
Authority
```

### WarTendency

```text
Valor        勇武
Mercy        仁慈
Honor        荣誉
Generosity   慷慨
Calculating  谋算
        ↓
WarDisposition
        ↓
WarTendency
```

`Commander（统帅）` 属于军事能力，不直接进入政治集团立场。

---

## 5. Political Distance

两个 Clan 之间计算：

```text
PoliticalDistance(A, B)
```

比较：

```text
Authority
WarTendency
WarPotential
CrownAlignment
Culture
```

具体公式和权重后续平衡。

Bloc 本身不保存独立 Political Profile：

```text
BlocPoliticalPosition
=
LeaderClan.PoliticalProfile
```

Clan 判断是否适合某个 Bloc 时，只比较：

```text
PoliticalDistance(Clan, LeaderClan)
```

不计算集团平均值，也不逐个比较集团成员。

---

## 6. Bloc 数量

每个 Kingdom：

```text
MaxBlocCount = 4
```

表示最多可以同时存在 4 个正式政治集团。

不要求存在：

```text
1 Royal Bloc + 3 Other Blocs
```

而是单纯：

```text
CurrentBlocCount <= 4
```

王国完全可以长期只有：

```text
0 / 1 / 2 / 3 / 4
```

个 Bloc。

---

## 7. 初始状态

王国初始：

```text
CurrentBlocCount = 0
```

所有 Clan 都是：

```text
Independent Clan
```

即：

> 开局不存在任何正式政治集团。

随着王国政治运行：

```text
Clan Political Profile逐渐产生差异
        ↓
部分Clan形成稳定政治接近
        ↓
有Influence和支持者的Clan创建Bloc
        ↓
其他Clan根据PoliticalDistance选择是否加入
```

因此 Bloc 不是开局预设，而是真正从游戏进程中自然形成。

---

## 8. Royal Bloc

`Royal Bloc` 不再是开局必然存在的集团。

定义：

```text
Royal Bloc
=
当前由 King Clan 领导的 Bloc
```

如果 King Clan 没有 Bloc：

```text
当前不存在 Royal Bloc
```

King Clan 可以像其他 Clan 一样创建 Bloc。

一旦：

```text
Bloc.LeaderClan == KingClan
```

该 Bloc 自动拥有：

```text
Royal Bloc
```

身份。

也就是说：

> Royal Bloc 是 King Clan 所领导 Bloc 的特殊身份，而不是一个永远存在的固定组织。

---

## 9. 加入 Bloc

Clan 加入 Bloc 时主要比较：

```text
PoliticalDistance(Clan, LeaderClan)
```

并采用：

```text
JoinThreshold
```

只有：

```text
PoliticalDistance <= JoinThreshold
```

才具有正常加入倾向。

其他因素可以作为修正：

```text
Relation(Clan, Leader)
Leader Influence
Bloc规模
近期政治合作
```

但 Political Distance 始终是核心判断。

---

## 10. 脱离 Bloc

采用迟滞机制：

```text
JoinThreshold < LeaveThreshold
```

即加入时要求比较高的政治契合，而已经加入后允许更大的政治差异。

目的：

> 避免 Clan 因小幅政治波动频繁跳 Bloc。

脱离主要考虑：

```text
PoliticalDistance(Member, Leader)
Relation(Member, Leader)
近期重大政治冲突
```

不建立永久：

```text
BlocCohesion
```

如有需要，只动态计算当前凝聚程度。

---

## 11. Trait 对 Bloc 行为的影响

Trait 除了影响 Political Profile，也可以修正具体集团行为。

### Honor（荣誉）

```text
Honor 高
→ 更不容易轻易背离当前 Leader
→ LeaveThreshold 略提高
```

但重大政治冲突仍可导致脱离。

### Calculating（谋算）

```text
Calculating 高
→ 更重视现实政治利益
```

当多个 Bloc Political Distance 接近时，更关注：

```text
Leader Influence
Bloc成员数量
政治收益
集团前景
```

Trait 只作为行为修正，不替代 Political Distance。

---

## 12. 创建 Bloc

Clan 脱离现有 Bloc 后，不会自动创建新 Bloc。

可以保持：

```text
Independent Clan
```

创建 Bloc 至少要求：

- 与现有 Bloc Leader 均缺乏足够政治契合；
- `Clan.Influence` 达到要求；
- 存在政治立场接近的潜在支持 Clan；
- `CurrentBlocCount < MaxBlocCount`；
- 不处于创建冷却期。

创建 Bloc 可以消耗：

```text
Clan.Influence
```

---

## 13. Bloc Split

已有 Bloc 内出现严重政治分歧时，成员 Clan 可以发起：

```text
Bloc Split
```

发起条件包括：

- 与当前 Leader Political Distance 较大；
- 自身 Influence 足够成为 Leader；
- 存在潜在追随者；
- 未达到 `MaxBlocCount`；
- 满足冷却要求。

潜在成员比较：

```text
D(Member, SplitLeader)
```

与：

```text
D(Member, OldLeader)
```

只有：

```text
D(Member, SplitLeader)
明显小于
D(Member, OldLeader)
```

才倾向跟随新 Leader。

---

## 14. 国王更替

因为 Royal Bloc 不再是固定组织，国王更替后不自动更换 Bloc Leader。

### 原 King Clan 的 Bloc

如果旧国王拥有 Royal Bloc：

```text
旧 Royal Bloc
→ 保留
→ 失去 Royal 身份
→ 成为普通 Bloc
```

原 Leader Clan 仍然保持 Leader。

### 新 King Clan

如果新 King Clan 已经领导一个 Bloc：

```text
该 Bloc
→ 自动成为新的 Royal Bloc
```

如果新 King Clan 没有 Bloc：

```text
Royal Bloc 暂时不存在
```

新国王可以后续自行组织集团。

这样不会因为王位更替而强行重组所有 Clan。

---

## 15. Bloc 的自然形成

由于 Political Profile 包括：

```text
Authority
WarTendency
WarPotential
CrownAlignment
Culture
```

因此游戏中可能自然出现：

```text
主战贵族聚集
反战贵族聚集
强王权支持者聚集
贵族自治派聚集
大贵族利益集团
弱势小Clan联盟
文化相近集团
反国王集团
```

但这些都不是系统预设的 Bloc 类型。

系统只知道：

```text
PoliticalDistance
```

不同政治集团只是这些变量自然演化后的结果。

---

## 16. 完整演化流程

```text
Culture
+ Leader Trait
+ Political Experience
+ WarDisposition
+ WarPotential
+ Clan Relation
        ↓
Clan Political Profile
        ↓
Political Distance
        ↓
初始全部 Independent
        ↓
有能力的Clan创建Bloc
        ↓
政治相近Clan加入
        ↓
Bloc扩张
        ↓
政治经历继续改变Profile
        ↓
成员与Leader差异扩大
        ↓
脱离 / Independent / 加入其他Bloc
        ↓
创建新Bloc / Bloc Split
        ↓
王国政治集团持续重组
```

---

## 17. 核心原则

政治集团系统不创建固定政治阵营。

核心逻辑始终是：

```text
Clan Political Profile
        ↓
Political Distance
        ↓
Bloc Formation
```

Trait 的作用是：

```text
Trait
→ 改变Clan政治立场和政治行为
```

而不是：

```text
Trait
→ 直接决定Clan属于哪个Bloc
```

最终目标：

> 让王国内部政治集团由 Clan 的王权态度、战争倾向、战争潜力、与国王关系、文化、人物 Trait 和政治经历自然涌现，而不是由脚本预先指定政治阵营。
