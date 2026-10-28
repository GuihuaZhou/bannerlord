# W5：PartyFinance 合并到 PartyManagement

## 已确认的业务边界

用户明确要求：`PartyFinance` **不再是顶层业务模块**，其职责属于部队管理。现有 `Recruitment/Finance` 是招募预算侧的业务协调，先不要盲目合并，避免随路径迁移误改预算语义。

## 当前文件与目标位置

| 源文件 | 目标文件 | 说明 |
|---|---|---|
| `PartyFinance/Models/NewPartyWageModel.cs` | `PartyManagement/Finance/Models/NewPartyWageModel.cs` | 原工资/招聘费用模型 |
| `PartyFinance/Models/AiRecruitmentFinancialModel.cs` | `PartyManagement/Finance/Models/AiRecruitmentFinancialModel.cs` | 经营预算及长期工资估算 |
| `PartyFinance/Models/MobilePartyWageLimitCalculator.cs` | `PartyManagement/Finance/Models/MobilePartyWageLimitCalculator.cs` | 部队工资限制 |
| `PartyFinance/Patches/ClanFinancialEvaluationPatch.cs` | `PartyManagement/Finance/Patches/ClanFinancialEvaluationPatch.cs` | Harmony 薄适配；其他逻辑 W4 执行 |
| `PartyFinance/Behaviors/PartyPersonnelLossLogBehavior.cs` | `PartyManagement/Behaviors/PartyPersonnelLossLogBehavior.cs` | 人员流失记录更属于部队管理而非财务模型 |

建议代码组织：

```text
PartyManagement/
├── Models/             # NewClanTierModel、NewPartySizeLimitModel 等
├── Behaviors/          # PartyPersonnelLossLogBehavior
├── Patches/            # 通用部队管理原版适配
└── Finance/
    ├── Models/         # 工资及财务能力
    ├── Services/       # 需要时增加共享财务核算
    └── Patches/        # 财政 Harmony 适配
```

## 安全迁移顺序

### W5-A — 只移动文件

- `git mv` 五个文件到新目录。C# 默认项目自动纳入 `.cs` 编译，但必须核对 `.csproj` 和构建脚本。
- 暂时 **不改 namespace**，先做一次编译，证明物理目录调整不改变运行逻辑。

### W5-B — 全量命名空间迁移

- 原 `ModifiedArmy.PartyFinance.Models` → `ModifiedArmy.PartyManagement.Finance.Models`。
- 原 `ModifiedArmy.PartyFinance.Patches` → `ModifiedArmy.PartyManagement.Finance.Patches`。
- 原 `ModifiedArmy.PartyFinance.Behaviors` 的 `PartyPersonnelLossLogBehavior` → `ModifiedArmy.PartyManagement.Behaviors`。
- 全仓（包括其他 Mod 项目）查找旧命名空间的 `using`、全限定类型名、反射字符串、XML 引用和外部公开 API 消费方。
- `Main.cs` 需更新 `using`，**严格保持 OnGameStart 注册 `NewPartyWageModel` 与 `PartyPersonnelLossLogBehavior` 的顺序和业务结果**；`OnAfterSessionLaunched` 的玩家 99000 金币/装备块必须零修改。
- 如其他 Mod 通过类型全名反射调用旧类型，优先说明兼容需求；不能为改目录盲目破坏跨 Mod 绑定。

### W5-C — 与 Recruitment/Finance 的关系

- `Recruitment/Finance/ClanRecruitmentBudgetManager`、`ClanRecruitmentBudgetSnapshot` 暂留在 `Recruitment`。
- 若它们只使用 `AiRecruitmentFinancialModel` 的输入，应改为引用新 `PartyManagement.Finance.Models`；不要让 `PartyManagement` 反向依赖 `Recruitment`。
- 长期建议：通用工资/财务计算只有 `PartyManagement.Finance` 一处权威；招募专属消耗与审批归 `Recruitment`。

## 验收

1. 顶层 `PartyFinance/` 移除；无旧命名空间引用（明确豁免兼容适配除外）。
2. 项目编译、启动、注册 Model/Behavior 与 Harmony Patch 数量不变。
3. 相同兵种、人数、政策下工资/维护费、预算、AI 招募审批与旧版一致。
4. `ClanFinancialEvaluationPatch` 拦截行为和日志不变。
5. `Main.cs` 的玩家奖励逻辑字节级等价（或明确 `git diff` 不包含相关修改）。
6. 不触碰 `ARCHITECTURE.md` 与 `Test-LocalizationPlaceholders.ps1`。
