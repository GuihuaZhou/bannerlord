好的！我们结合您提供的四个核心文件（`MakePeaceKingdomDecision.cs`、`DefaultDiplomacyModel.cs`、`KingdomDecisionPermissionModel.cs`、`KingdomDecisionProposalBehavior.cs`），完整梳理 **AI 氏族（Clan）如何发起并实现和平或战争** 的决策流程。

---

## 🧠 核心机制：王国决策系统（Kingdom Decision System）

在 Bannerlord 中，重大外交行为（如宣战、议和）不是由单个英雄决定的，而是通过 **“王国决议”（Kingdom Decision）** 机制完成。这模拟了封建议会：由某个氏族**提议**，全体氏族**投票**，统治者**最终裁决**。

> ⚔️ **开战** 和 ✌️ **议和** 都是 `KingdomDecision` 的子类。

---

## 🔁 整体流程概览

```mermaid
graph TD
    A[AI 每小时检查] --> B{是否考虑开战/议和?}
    B -- 是 --> C[创建 KingdomDecision 提案]
    C --> D[验证提案合法性]
    D -- 合法 --> E[启动王国决议流程]
    E --> F[各氏族计算支持度]
    F --> G{支持率 ≥ 60%?}
    G -- 是 --> H[统治者批准]
    H --> I[执行 Action (宣战/议和)]
    G -- 否 --> J[提案失败]
```

---

## 第一阶段：AI 决定“是否要提”

### 1. 入口：`KingdomDecisionProposalBehavior.HourlyTick()`
- AI 每游戏小时运行一次。
- 调用 `ConsiderWar()` 或 `ConsiderPeace()`。

### 2. `ConsiderPeace()` 流程（以议和为例）
来自 `KingdomDecisionProposalBehavior.cs`：

#### 步骤 1️⃣：前置检查
```csharp
// 检查和平是否“合适”
if (!DiplomacyModel.IsPeaceSuitable(clan.MapFaction, otherFaction))
    return false;
```
- **作用**：快速排除明显不该议和的情况（如刚开战、一方已灭）。
- **日志点**：您已添加补丁记录此结果。

#### 步骤 2️⃣：分数达标？
```csharp
float score = DiplomacyModel.GetScoreOfDeclaringPeace(clan.MapFaction, otherFaction);
if (score < DiplomacyModel.GetDecisionMakingThreshold(clan.Kingdom))
    return false;
```
- **关键方法**：
  - `GetScoreOfDeclaringPeace(...)`: 计算和平带来的**净收益**（基于领土、兵力、战争进度等）。
  - `GetDecisionMakingThreshold(...)`: 返回一个阈值（通常为王国总领地价值的 1/6）。
- **逻辑**：只有当和平收益 > 阈值，才值得提出。

#### 步骤 3️⃣：计算赔款
```csharp
int tribute = DiplomacyModel.GetDailyTributeToPay(clan, otherClan, out duration);
```
- 根据战争胜负情况，决定谁赔钱、赔多少、赔多久。

#### 步骤 4️⃣：创建提案并验证
```csharp
var decision = new MakePeaceKingdomDecision(clan, otherFaction, tribute, duration);
if (!decision.CanMakeDecision(out reason)) 
    return false; // 权限模型拒绝
```
- **权限检查**：调用 `KingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms(...)`
  - 检查双方是否处于战争状态。
  - 检查是否已有相同提案。
  - 检查提议者是否有足够影响力（Influence）。

✅ 如果通过，`ConsiderPeace` 返回 `true`，并将 `decision` 作为输出参数。

---

## 第二阶段：启动王国决议流程

一旦 `ConsiderPeace()` 返回 `true`，游戏会：

```csharp
KingdomDecisionManager.AddDecision(decision);
```

这将触发完整的 **王国决议 UI 和投票流程**（即使对 AI 也模拟此过程）。

---

## 第三阶段：各氏族投票（支持度计算）

每个氏族（Clan）会调用：

```csharp
float support = decision.DetermineSupport(clan, outcome);
```

### 关键方法：`MakePeaceKingdomDecision.DetermineSupport()`
来自 `MakePeaceKingdomDecision.cs`：

```csharp
public override float DetermineSupport(Clan clan, DecisionOutcome possibleOutcome)
{
    // 1. 如果和平“不合适”且不是对方提出的，反对
    if (!DiplomacyModel.IsPeaceSuitable(...) && !_isProposedByOpponent) {
        return possibleOutcome.ShouldPeaceBeDeclared ? 0f : 200f;
    }

    // 2. 获取该氏族的和平倾向分
    float clanScore = DiplomacyModel.GetScoreOfDeclaringPeaceForClan(kingdom, target, clan, ...);

    // 3. 获取王国整体和平倾向分
    float kingdomScore = DiplomacyModel.GetScoreOfDeclaringPeace(kingdom, target);

    // 4. 比较：如果该氏族比王国整体更支持和平，则投赞成票
    if (clanScore > kingdomScore * 0.95f && ...) {
        return 200f; // 支持
    }
    return 0f; // 反对
}
```

- **返回 `200f`** 表示全力支持（其影响力计入总支持）。
- **返回 `0f`** 表示反对。

> 💡 投票不是“是/否”，而是 **按影响力加权**。最终支持率 = 支持氏族影响力之和 / 总合格氏族影响力。

---

## 第四阶段：决议通过与执行

### 1. 统治者裁决
- 如果支持率 ≥ 60%，统治者（Ruling Clan Leader）**必须批准**。
- 游戏调用：
  ```csharp
  decision.ApplyChosenOutcome(chosenOutcome);
  ```

### 2. 执行 Action
来自 `MakePeaceKingdomDecision.cs`：
```csharp
public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
{
    if (_applyResults && ((MakePeace...)chosenOutcome).ShouldPeaceBeDeclared)
    {
        MakePeaceAction.ApplyByKingdomDecision(
            base.Kingdom, 
            this.FactionToMakePeaceWith, 
            this.DailyTributeToBePaid, 
            this.DailyTributeDurationInDays
        );
    }
}
```

- `MakePeaceAction.ApplyByKingdomDecision(...)` 是最终执行议和的底层方法。
- 它会：
  - 结束战争状态。
  - 设置赔款协议。
  - 触发通知（如 `PeaceOfferMapNotification`）。

---

## ⚔️ 开战（War）流程对比

开战流程几乎完全对称：

| 步骤 | 议和 (Peace) | 开战 (War) |
|------|---------------|------------|
| 决策类 | `MakePeaceKingdomDecision` | `DeclareWarKingdomDecision` |
| 考虑方法 | `ConsiderPeace()` | `ConsiderWar()` |
| 分数方法 | `GetScoreOfDeclaringPeace()` | `GetScoreOfDeclaringWar()` |
| 权限检查 | `IsPeaceDecisionAllowed...` | `IsWarDecisionAllowed...` |
| 执行 Action | `MakePeaceAction.Apply...` | `DeclareWarAction.Apply...` |

---

## 📌 关键模型总结

| 文件 | 作用 |
|------|------|
| `KingdomDecisionProposalBehavior.cs` | **AI 决策入口**：每小时检查是否要提和平/战争 |
| `DefaultDiplomacyModel.cs` | **核心计算**：和平/战争分数、赔款、阈值、适宜性 |
| `KingdomDecisionPermissionModel.cs` | **合法性检查**：能否提这个决议？ |
| `MakePeaceKingdomDecision.cs` | **决议定义**：如何投票？如何执行？ |

---

## 🔍 调试建议

您已为以下环节添加日志，形成完整链路：

1. **`IsPeaceSuitable`** → 是否值得考虑？
2. **`ConsiderPeace`** → AI 是否决定提？
3. **`DetermineSupport`** → 各氏族是否支持？
4. **`GetScoreOfDeclaringPeace`**（已有）→ 分数怎么算的？

这四层日志足以定位任何 AI 外交异常！

需要我为您生成开战（`ConsiderWar`）的对应补丁吗？