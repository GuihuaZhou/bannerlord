# Bannerlord Kingdom 等级、外交与宗藩体系设计

## 1. 核心定位

不在 `Kingdom` 与 `Clan` 之间创建新的政治实体。

继续保持 Bannerlord 原有基本结构：

``` text
Kingdom
└─ Clan
   └─ Hero
```

扩展重点放在：

> **Kingdom 自身属性 + Kingdom ↔ Kingdom 关系**

公国、王国、帝国、苏丹国、埃米尔国等，本质上仍然全部是 `Kingdom`。

------------------------------------------------------------------------

## 2. Kingdom Rank

每个 Kingdom 增加：

``` text
KingdomRank : int
```

规则：

``` text
KingdomRank >= 1
```

数字越大，国家政治等级越高。

例如：

``` text
Rank 1
Rank 2
Rank 3
...
```

具体等级名称由文化决定，后续单独设计。

例如同一个 `Rank`，不同文化可以显示为：

``` text
公国
埃米尔国
汗国
……
```

因此代码层不建立：

``` text
Duchy
Emirate
Sultanate
Empire
```

等不同国家实体。

> **底层统一为 Kingdom + KingdomRank，文化决定显示名称。**

Rank 第一阶段主要表示**政治地位**，暂不直接赋予税收、军力等数值 Buff。

------------------------------------------------------------------------

## 3. Kingdom Relation

任意两个 Kingdom 之间维护国家关系：

``` text
KingdomRelation ∈ [-100, 100]
```

例如：

``` text
Vlandia ↔ Battania = -70
Empire ↔ Sturgia = +45
```

第一版采用**对称关系**：

``` text
Relation(A, B) = Relation(B, A)
```

Relation 表示两个国家长期的外交关系，但**不直接决定外交状态**。

因此：

``` text
Relation = +100
```

不等于自动结盟。

``` text
Relation = -100
```

也不等于自动战争。

Relation 主要用于：

-   AI 外交判断；
-   条约接受意愿；
-   宣战倾向；
-   臣服意愿；
-   解除条约倾向等。

------------------------------------------------------------------------

## 4. Kingdom ↔ Kingdom 外交结构

国家之间的关系拆成几个彼此独立的维度。

### 4.1 基础外交状态

``` text
Peace
War
```

两者互斥。

### 4.2 外交条约

目前包括：

``` text
Non-Aggression Pact   互不侵犯
Trade Agreement       贸易协定
Alliance              同盟
```

外交条约与 `Peace / War` 不使用同一个枚举。

因此和平状态下可以同时存在：

``` text
Peace
+ Non-Aggression
+ Trade Agreement
```

### 4.3 宗藩关系

``` text
None
Vassal
Puppet
```

宗藩关系具有方向性：

``` text
Overlord Kingdom
        ↓
Subject Kingdom
```

一个宗主可以拥有多个附庸国或傀儡国。

第一版暂不考虑多层宗藩嵌套。

------------------------------------------------------------------------

## 5. 附庸国与傀儡国

`Vassal` 与 `Puppet` 从框架建立之初就作为两种不同类型保存。

``` text
SubjectType
├─ None
├─ Vassal
└─ Puppet
```

但**第一版两者实际效果可以完全相同**。

第一版核心效果：

``` text
1. 外交上与宗主保持一致
2. 定期向宗主支付 Tribute
```

Tribute 尽量复用 Bannerlord 已有的定期赔款机制。

附庸国或傀儡国仍然是完整的 `Kingdom`，保留自己的：

-   Ruler
-   Clan
-   Settlement
-   Army
-   Policy
-   Kingdom Decision
-   Influence
-   内部政治

宗主主要限制其**对外主权**。

未来如果确有必要，再让 `Vassal` 与 `Puppet`
在外交自主权、贡赋、脱离方式等方面产生差异。

------------------------------------------------------------------------

## 6. 建立宗藩关系的三种主要途径

### 6.1 战争迫使臣服

战争中一方取得优势后，可以要求另一 Kingdom：

``` text
成为附庸国
```

或者：

``` text
成为傀儡国
```

接受后结束战争，并建立宗藩关系。

因此战争胜利不再只有：

> 占领 → 吞并

还可以：

> **战争 → 臣服 → 保留原 Kingdom**

### 6.2 主动建立附庸国 / 傀儡国

占领异文化地区以后，可以从自己的领土中建立新的 Kingdom。

基本流程：

``` text
占领异文化 Settlement
        ↓
选择异文化 Clan
        ↓
选择用于建国的 Settlement
        ↓
创建新的 Kingdom
        ↓
指定 King Clan / Ruler
        ↓
自动成为宗主的 Vassal / Puppet
```

例如：

``` text
Empire
└─ 新建立的 Aserai Kingdom
   ├─ Aserai Clan A
   ├─ Aserai Clan B
   └─ 若干 Aserai Settlement
```

这个新国家是真正的 `Kingdom`，而不是新增的"附庸实体"。

### 6.3 外交谈判

和平时期也可以通过外交建立宗藩关系。

包括：

``` text
要求对方成为附庸
主动要求成为对方附庸

要求对方成为傀儡
主动要求成为对方傀儡
```

AI 是否接受由后续外交评价公式决定。

------------------------------------------------------------------------

## 7. 外交选项扩展

当前计划形成：

``` text
宣战
求和

一次性付款
定期 Tribute

互不侵犯
贸易协定
结盟

要求对方成为附庸
主动成为对方附庸

要求对方成为傀儡
主动成为对方傀儡

建立附庸国
建立傀儡国

割让 Settlement
```

其中需要区分：

> **"使现有 Kingdom 成为附庸"**

与：

> **"从自己的领土中创建一个新的附庸 Kingdom"**

这是两个不同机制。

------------------------------------------------------------------------

## 8. Settlement 割让

允许 Kingdom 之间通过外交转移 Settlement。

例如：

``` text
Kingdom A
    ↓ 割让 Town X
Kingdom B
```

外交系统只负责：

> **Settlement 的国家归属发生转移。**

至于 Kingdom B 内部最终由哪个 Clan 获得该 Settlement，可以继续利用
Bannerlord 原有的封地分配 / Kingdom Decision 机制。

这样避免外交系统同时承担 Clan 内部分封逻辑。

------------------------------------------------------------------------

## 9. Kingdom Relation 的变化

`KingdomRelation [-100, 100]` 主要由国家之间发生的外交事件改变。

例如：

``` text
共同作战
履行同盟
长期贸易
履行宗藩义务
        ↓
Relation 上升
```

而：

``` text
撕毁互不侵犯
解除同盟
发动战争
宗主压迫附庸
附庸违抗宗主
        ↓
Relation 下降
```

第一阶段**不额外建立 Diplomatic Reputation / 外交信誉属性**。

违约行为直接通过 Kingdom Relation 以及相关外交后果表现。

同时暂不简单使用所有 `Clan Relation` 的平均值作为 Kingdom Relation，避免
Clan 政治层与国家外交层过度耦合。

------------------------------------------------------------------------

## 10. 当前整体数据结构

``` text
Kingdom
├─ KingdomRank : int
├─ 原版 Clan / Settlement / Ruler ...
└─ SubjectRelation
    ├─ OverlordKingdom
    └─ SubjectType
        ├─ None
        ├─ Vassal
        └─ Puppet


Kingdom A ↔ Kingdom B
├─ Relation : -100 ~ 100
├─ WarState
│   ├─ Peace
│   └─ War
│
├─ Treaties
│   ├─ NonAggression
│   ├─ TradeAgreement
│   └─ Alliance
│
└─ SubjectRelation
    ├─ None
    ├─ Vassal
    └─ Puppet
```

------------------------------------------------------------------------

## 11. 当前设计核心原则

1.  **不增加 Kingdom 与 Clan 之间的新政治实体。**
2.  **公国、王国、帝国、苏丹国、埃米尔国等全部继续使用 Kingdom。**
3.  **`KingdomRank` 使用整数表达政治等级，从 1
    开始，数字越大等级越高；文化决定各等级名称。**
4.  **`KingdomRelation` 取值范围为 `-100 ~ 100`，第一版采用对称关系。**
5.  **国家关系、战争/和平状态、外交条约、宗藩关系彼此分离。**
6.  **附庸与傀儡从数据框架上立即区分，但第一版允许使用完全相同的实际机制。**
7.  **附庸国与傀儡国仍然是完整
    Kingdom，只是在外交和贡赋上受到宗主约束。**
8.  **可以通过战争、外交谈判以及主动建立新国家三种方式形成宗藩体系。**
9.  **允许通过外交割让 Settlement，具体 Clan 分封继续尽量复用 Bannerlord
    原有 Kingdom Decision。**
10. **第一阶段不额外建立 Diplomatic Reputation 等新的长期外交数值。**

最终目标：

> **在不新增政治实体的前提下，将 Bannerlord 原本平行的 Kingdom
> 体系扩展为具有国家等级、外交关系、条约以及宗主---附庸 /
> 傀儡关系的多层国际政治体系。**
