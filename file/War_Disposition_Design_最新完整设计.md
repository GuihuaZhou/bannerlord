# 战争倾向度（WarDisposition）完整设计

## 1. 核心概念

新增 Clan 属性：

```text
WarDisposition（战争倾向度）
范围：[-100, 100]
```

含义：

```text
WarDisposition > 0
倾向继续战争

WarDisposition < 0
倾向结束战争

WarDisposition = 0
中立
```

初始值：

```text
WarDisposition = 0
```

WarDisposition 表示 Clan 对当前战争的主观态度。

它不是战争疲劳，也不是战争潜力。

---

## 2. 基本计算模型

战争事件造成：

```text
ΔWarDisposition
=
EventBaseValue
× ClanRelationMultiplier
× PersonalityModifier
```

### 2.1 EventBaseValue

表示事件本身对战争态度的基础影响。

所有基础值默认按照事件发生在本 Clan 定义。

### 2.2 ClanRelationMultiplier

```text
本 Clan 事件：
ClanRelationMultiplier = 1.0

其他 Clan 事件：
ClanRelationMultiplier = 0.2
```

因此，本 Clan 自身损失和收益产生完整影响；王国内其他 Clan 的事件也会影响当前 Clan，但只按 20% 计算。

例如其他 Clan 一支 Party：

```text
Party战败        -3
Party被摧毁      -6
领队被俘        -3

合计            -12
```

对当前 Clan：

```text
-12 × 0.2 = -2.4
```

---

## 3. WarDisposition 事件来源

### 3.1 战斗事件

| 事件 | EventBaseValue |
|---|---:|
| Party胜利 | +3 |
| Party战败 | -3 |
| Party被摧毁 | -6 |
| Clan成员被俘虏 | -3 |
| Clan成员战死 | -4 |
| Clan成员被处决 | -10 |

### 3.2 定居点事件

| 事件 | EventBaseValue |
|---|---:|
| 村庄被劫掠 | -3 |
| 城堡被占领 | -6 |
| 城镇被占领 | -8 |

### 3.3 战争收益

| 事件 | EventBaseValue |
|---|---:|
| 俘虏敌人 | +2 |
| 攻占城堡 | +4 |
| 攻占城镇 | +5 |
| 获封城堡 | +10 |
| 获封城镇 | +15 |

其中：

```text
攻占封地
=
王国取得战争成果
```

```text
获封封地
=
Clan 自身获得长期战争利益
```

因此获封封地的影响明显更大。

### 3.4 经济结果

每周统计一次 Clan 财富变化比例。

| 财富变化 | EventBaseValue |
|---|---:|
| ≥ +50% | +12 |
| +20% ~ +50% | +8 |
| +10% ~ +20% | +4 |
| +5% ~ +10% | +2 |
| -5% ~ +5% | 0 |
| -10% ~ -5% | -2 |
| -20% ~ -10% | -4 |
| -50% ~ -20% | -8 |
| ≤ -50% | -12 |

经济变化属于长期因素，因此单次影响低于重大军事和封地事件。

---

## 4. Trait 组合修正

取消原来的：

```text
好人领主
一般领主
残暴领主
```

不再给 Hero 强制分类。

Hero 可以同时拥有多个 Trait，因此 WarDisposition 根据当前事件相关的多个 Trait 共同计算。

### 4.1 Trait 与翻译

参与战争倾向：

```text
Valor        勇武
Mercy        仁慈
Honor        荣誉
Generosity   慷慨
Calculating  谋算
```

暂不直接参与战争倾向：

```text
Egalitarian   平等主义
Oligarchic    寡头主义
Authoritarian 威权主义
Commander     统帅
```

其中 `Egalitarian（平等主义）`、`Oligarchic（寡头主义）`、`Authoritarian（威权主义）` 主要用于政治立场、王权和投票系统。

`Commander（统帅）` 更接近军事能力，不作为战争态度 Trait。

### 4.2 Trait 修正公式

```text
TraitReaction
=
Σ(TraitLevel × EventTraitWeight)
```

```text
PersonalityModifier
=
Clamp(
    1 + TraitReaction,
    -0.5,
    2.0
)
```

最终：

```text
ΔWarDisposition
=
EventBaseValue
× ClanRelationMultiplier
× PersonalityModifier
```

含义：

```text
PersonalityModifier = 1.0
正常反应

PersonalityModifier > 1.0
放大事件影响

0 < PersonalityModifier < 1.0
削弱事件影响

PersonalityModifier < 0
事件方向反转
```

最大正向放大为 `2.0` 倍，最大反向效果为 `0.5` 倍。

---

## 5. 各事件 Trait 反应

### 5.1 Party胜利

```text
Valor（勇武）          +0.15 / level
Calculating（谋算）    +0.05 / level
```

勇武者更容易因为胜利支持继续战争；谋算者也会认可实际军事优势。

### 5.2 Party战败

```text
Valor（勇武）          -0.15 / level
Calculating（谋算）    +0.10 / level
```

勇武越高，越能承受普通失败；谋算越高，越重视战败对战争局势的实际意义。

### 5.3 Party被摧毁

```text
Valor（勇武）          -0.10 / level
Mercy（仁慈）          +0.10 / level
Calculating（谋算）    +0.15 / level
```

### 5.4 Clan成员被俘

```text
Valor（勇武）          -0.05 / level
Honor（荣誉）          +0.05 / level
```

### 5.5 Clan成员战死

```text
Mercy（仁慈）          +0.15 / level
Valor（勇武）          -0.10 / level
```

### 5.6 Clan成员被处决

```text
Mercy（仁慈）          +0.15 / level
Valor（勇武）          -0.15 / level
Honor（荣誉）          -0.20 / level
```

高仁慈更容易产生强烈厌战。

高勇武、高荣誉可能因为复仇和荣誉受辱显著降低厌战。

极端情况下：

```text
PersonalityModifier < 0
```

则成员被处决反而增加继续战争倾向。

### 5.7 村庄被劫掠

```text
Mercy（仁慈）          +0.20 / level
Honor（荣誉）          +0.05 / level
```

### 5.8 城堡 / 城镇被占领

```text
Calculating（谋算）    +0.15 / level
Valor（勇武）          -0.10 / level
```

### 5.9 攻占城堡 / 城镇

```text
Valor（勇武）          +0.10 / level
Calculating（谋算）    +0.10 / level
```

### 5.10 获封城堡 / 城镇

```text
Calculating（谋算）    +0.10 / level
Generosity（慷慨）     -0.10 / level
```

### 5.11 财富变化

```text
Calculating（谋算）    +0.20 / level
```

谋算者尤其关注战争造成的实际经济收益或损失，因此无论正向还是负向财富变化，其影响都会被放大。

---

## 6. Trait 组合示例

某 Clan Leader：

```text
Valor = +2
Calculating = +1
```

发生 Party 战败：

```text
TraitReaction
=
2 × -0.15
+ 1 × 0.10
=
-0.20
```

```text
PersonalityModifier
=
0.80
```

本 Clan：

```text
ΔWarDisposition
=
-3 × 1.0 × 0.80
=
-2.4
```

另一个领主：

```text
Valor = -1
Calculating = +2
```

则：

```text
TraitReaction
=
(-1 × -0.15)
+ (2 × 0.10)
=
0.35
```

```text
PersonalityModifier
=
1.35
```

```text
ΔWarDisposition
=
-3 × 1.35
=
-4.05
```

同样的失败，对不同 Hero 可以产生明显不同的政治态度变化。

---

## 7. 同源事件处理

取消同源事件合并、事件覆盖、事件优先级。

所有实际发生的事件全部独立计算。

例如：

```text
Party战败        -3
Party被摧毁      -6
成员被俘        -3
```

三项全部计算。

核心原则：

> 实际发生什么，就计算什么。

---

## 8. 事件累计与防刷

暂不设置简单固定的每日硬上限。

例如本 Clan 遭遇：

```text
3支Party全部战败
3支Party全部被摧毁
6名Clan成员被俘
自家城镇陷落
```

不考虑 Trait 时：

```text
3 × -3
+ 3 × -6
+ 6 × -3
- 8
=
-53
```

这种情况属于真正的 Clan 级灾难，因此 `-53` 本身可以是合理结果。

防刷机制的目标不是截断真实重大灾难，而是防止大量低价值重复事件异常刷高或刷低 WarDisposition。

具体规则等实际实现事件监听时再确定。

---

## 9. 每日回归

WarDisposition 每日按固定数值向 `0` 回归。

```text
DailyReturn
=
ReturnAmount
```

处理：

```text
WarDisposition > 0
→ 每日减少

WarDisposition < 0
→ 每日增加
```

最终：

```text
WarDisposition → 0
```

每日回归量：

| 战争持续时间 | ReturnAmount |
|---|---:|
| 0 ~ 30 天 | 1 |
| 30 ~ 90 天 | 2 |
| 90 ~ 180 天 | 4 |
| 180 天以上 | 6 |

战争持续越久，历史战争事件形成的态度越快向中立回归。

这里是每日回归，不是每日衰减。

---

## 10. 王国级重大失败测试

假设一个由 10 支其他 Clan Party 组成的 Army 全灭。

每支：

```text
Party战败        -3
Party被摧毁      -6
领队被俘        -3

合计            -12
```

10 支：

```text
10 × -12
=
-120
```

因为属于其他 Clan：

```text
ClanRelationMultiplier = 0.2
```

因此：

```text
-120 × 0.2
=
-24
```

不考虑 Trait：

```text
一次10-Party大型Army全灭
≈ -24 WarDisposition
```

连续三次类似重大失败：

```text
约 -72
```

实际还会受到 Trait 组合修正、每日回归和事件发生时间间隔影响。

设计目标：

> 王国可以承受一两次重大失败，但连续约三次大型惨败后，多数 Clan 应明显转向厌战。

---

## 11. 战争倾向等级

UI 使用：

```text
文字等级 + 实际数值
```

数值区间暂定：

```text
-100 ~ -61
-60  ~ -21
-20  ~ +20
+21  ~ +60
+61  ~ +100
```

等级词汇暂不继续细化，后续再定。

---

## 12. 战争倾向 UI 子面板

战争倾向采用独立子面板，与战争潜力保持相同布局，不合并。

建议：

```text
战争倾向

战争倾向        （等级词汇）(-36)
人物特性        勇武+2 / 荣誉+1 / 谋算-1
战争持续        74天
每日回归        2

今日战斗影响    -9
今日领地影响    -6
今日战争收益    +4
本周财富变化    -8%(-1)
```

### 12.1 战争倾向

显示：

```text
WarDisposition等级(数值)
```

### 12.2 人物特性

显示 Clan Leader 当前与战争倾向相关、且 `TraitLevel != 0` 的 Trait。

例如：

```text
人物特性
勇武+2 / 仁慈-1 / 荣誉+1 / 谋算+2
```

### 12.3 战争持续

显示当前战争持续时间，例如：

```text
74天
```

### 12.4 每日回归

显示当前 `ReturnAmount`，例如：

```text
2
```

显示当前每日固定回归量，不是百分比。

### 12.5 今日战斗影响

汇总当天战斗类事件产生的实际 WarDisposition 变化。

包括：

```text
Party胜利
Party战败
Party被摧毁
成员被俘
成员战死
成员被处决
```

该值只用于 UI 汇总，不参与二次计算。

### 12.6 今日领地影响

汇总当天村庄被劫掠、城堡被占领、城镇被占领产生的 WarDisposition 变化。

### 12.7 今日战争收益

汇总当天俘虏敌人、攻占城堡、攻占城镇、获封城堡、获封城镇产生的 WarDisposition 变化。

### 12.8 本周财富变化

同时显示财富实际变化百分比和对应 WarDisposition 影响。

例如：

```text
-8%(-1)
```

---

## 13. 完整计算流程

```text
战争事件发生
        ↓
确定 EventBaseValue
        ↓
判断：
本 Clan / 其他 Clan
        ↓
确定 ClanRelationMultiplier
        ↓
读取 Clan Leader 相关 Trait
        ↓
计算 TraitReaction
        ↓
计算 PersonalityModifier
        ↓
计算 ΔWarDisposition
        ↓
每个事件独立累计
        ↓
必要时应用防刷规则
        ↓
限制 WarDisposition 范围 [-100, 100]
        ↓
记录当天分类影响供 UI 显示
        ↓
每日执行 DailyReturn
        ↓
WarDisposition 向 0 回归
        ↓
用于战争相关王国政治决策
```

---

## 14. 核心定位

```text
WarPotential
=
这个 Clan 能不能继续打
```

```text
WarDisposition
=
这个 Clan 愿不愿意继续打
```

因此可以出现：

```text
战争潜力很强
战争倾向很低
```

表示：

> 有能力继续战争，但已经不愿意继续。

也可以出现：

```text
战争潜力很弱
战争倾向很高
```

表示：

> 已经缺乏持续战争能力，但政治上仍然希望继续战争。
