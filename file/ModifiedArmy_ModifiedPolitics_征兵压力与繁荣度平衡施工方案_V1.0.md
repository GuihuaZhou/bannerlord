# ModifiedArmy + ModifiedPolitics 征兵压力与繁荣度平衡施工方案 V1.0

**仓库：** `GuihuaZhou/bannerlord`  
**涉及模块：** ModifiedArmy、ModifiedPolitics  
**任务类型：** 现有机制调整、配置更新、存档兼容、回归测试  
**施工原则：** 复用现有模型与服务，不建立平行人口系统，不增加不必要的 Harmony Patch。

> 本文是可交给 Codex / AI Worker 的施工任务书，不代表修改已经实施或通过编译。施工前必须重新读取仓库最新 HEAD 和相关源码。

---

## 一、修改目标

当前职业兵征召会减少实际户数 Hearth，并累积 RecruitmentBurden（征兵压力）。现存两个主要问题：

1. 现有招募审批未检查招募后压力与实际户数的比例，允许征兵压力超过实际户数，甚至使军事可用容量成为负数。
2. ModifiedPolitics 将 `Hearth - RecruitmentBurden` 直接用于繁荣度支撑，导致长期自动征兵严重削弱经济，同时使征兵名额快速耗尽。

**本次目标：**

- 征兵压力最高不超过当前实际户数的 **60%**。
- 招募前根据剩余压力额度裁剪可招募人数；招募不能使压力突破上限。
- 压力达到上限后停止相应职业兵招募，恢复后自动继续。
- 征兵压力对繁荣度支撑的**直接影响**最多为 **15%**。
- 保留实际户数消耗、文化差异、职业兵训练机制和现有事务回滚能力。
- 兼容旧存档中的历史征兵压力。

不修改封邑兵供养、文化军事制度权重、职业兵训练速度及原有粮食消费设计。

## 二、确定的参数

| 参数 | 最终数值 |
|---|---|
| T1～T6 职业兵征兵压力成本 | 原值减半，奇数向上取整 |
| 征兵压力每日恢复量 | 4 |
| 最大征兵压力占实际户数比例 | 0.60 |
| 征兵压力经济影响系数 | 0.25 |
| 每日户数增长的压力惩罚系数 | 0.00015 |
| 城镇每户繁荣度支撑 | 5，保持不变 |
| 城堡每户繁荣度支撑 | 3，保持不变 |
| 超额繁荣度衰退时间 | 84 天，保持不变 |

必须将新参数放入已有 XML 配置体系，不在 C# 中重复硬编码业务数值。

## 三、ModifiedArmy：配置修改

### 3.1 修改 militaryConfig.xml

文件：`ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`

在现有 `HearthEconomy` 下新增全局征兵压力上限节点：

```xml
<RecruitmentBurdenLimit maxHearthRatio="0.60" />
```

保留现有 `FeudalTaxRelief`。七种文化的 `Professional/RecruitmentBurden` 将 `dailyRecovery` 统一设为 `4`，T1～T6 的 `Cost` 按下表修改：

| 文化 | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---:|---:|---:|---:|---:|---:|
| 帝国 | 2 | 3 | 5 | 7 | 11 | 18 |
| 瓦兰迪亚 | 3 | 5 | 8 | 13 | 19 | 30 |
| 阿塞莱 | 2 | 4 | 6 | 9 | 14 | 21 |
| 斯特吉亚 | 4 | 7 | 11 | 17 | 25 | 39 |
| 巴旦尼亚 | 5 | 8 | 13 | 20 | 31 | 48 |
| 库赛特 | 4 | 7 | 11 | 17 | 25 | 39 |
| 诺德 | 5 | 8 | 13 | 20 | 31 | 48 |

保留全部六级数据，不因为当前游戏主要招募 T1～T4 就移除 T5/T6。

### 3.2 修改 MilitaryConfig.cs

文件：`ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`

要求：

- 解析 `maxHearthRatio`。
- 在现有 `IRecruitmentBurdenPolicy` 接口增加获取上限比例的方法。
- 配置校验必须保证 `0 < maxHearthRatio <= 1`。
- `dailyRecovery` 和 `Tier Cost` 继续使用整数。
- 不改变现有七文化训练速度、封邑供养和军事制度权重。

上限比例统一由该配置提供，ModifiedPolitics 通过现有 Policy Registry 获取，不得再独立定义另一份 0.60。

## 四、ModifiedArmy：统一征兵容量审批

主要文件：`ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs`

现有 `RecruitmentBurdenMath` 已经提供 `GetMaxAffordableCount` 和 `CanRecruitExact`。应直接扩展现有纯计算模型。

### 4.1 核心公式

定义：

- `H`：当前实际户数。
- `B`：当前征兵压力。
- `C`：单名士兵的压力成本。
- `N`：本次招募人数。
- `P`：征兵压力上限比例，配置值 0.60。

新上限：`MaxBurden = P × H`  
剩余额度：`RemainingBurden = max(0, MaxBurden - B)`

招募后：

```
H_after = H - N
B_after = B + N × C
```

审批必须同时满足：

```
H_after >= 0
B_after <= P × H_after
```

因此同一兵种批次的最大可招募数量：

```
N_max = floor((P × H - B) / (C + P))
```

结果必须限制在 `[0, requested]` 范围内。

**重要：必须使用招募后的实际户数检查上限。** 不能只检查 `B + N × C <= 0.6 × H_before`，否则仍可能突破约束。

### 4.2 修改现有纯函数

让 `GetMaxAffordableCount` 和 `CanRecruitExact` 接收上限比例，统一使用相同数学规则。

要求：

- 保留浮点精度保护，避免边界值误判。
- 非法输入直接拒绝，不返回虚假审批数量。
- 计算只读取参数，不修改状态。
- 单人审批与批量审批结果一致。
- 不创建第二套独立招募审批模型。

### 4.3 所有招募入口必须使用新规则

重点检查：

- `SettlementManpowerProductionBehavior`
- `GarrisonRecruitmentPatch`
- `GarrisonRecruitmentExecutionService`
- `NativeRecruitmentPopulationPatch`
- `RecruitmentVmPopulationPatch`
- `FiefPartyManager` 中的职业兵招募事务

其中 `SettlementManpowerProductionBehavior.GetTotalAffordableProfessionalOffers` 仍存在直接调用 `RecruitmentBurdenMath.CanRecruitExact` 的逻辑，必须同步修改。

所有预览、额度查询和实际执行必须遵守相同上限。不同等级士兵混合招募时，依据真实兵种的 `CharacterObject.Tier` 分别计算成本，不能只使用平均成本。

现有驻军容量、Clan 经济预算、兵种模板、文化资格和训练速度限制全部继续生效。

### 4.4 招募名额与生产候选

本次限制的是**实际允许招募的人数**，不是降低兵营训练能力。

- 日常职业兵候选仍按原有训练能力、模板权重生成。
- 招募审批根据剩余压力额度减少本次允许消费的候选数量。
- 压力不足时不增加士兵、不扣除 Hearth、不增加 Burden。
- 被拒绝的候选不能记为成功招募。
- 压力不足不能产生跨日无限累积的待招募欠账。
- 下一次压力恢复后，按当时实际容量重新审批。

不得为了本次功能重构整个职业兵生产模型。

## 五、ModifiedPolitics：征兵压力状态管理

主要文件：`ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Behaviors/SettlementPopulationBehavior.cs`

### 5.1 统一压力上限约束

增加内部纯计算或状态规范化辅助逻辑：

```
NormalizedBurden = min(max(0, B), P × max(0, H))
```

必须保证城镇、城堡在稳定状态下满足：

```
0 <= RecruitmentBurden <= 0.60 × Hearth
```

处理以下场景：

1. 旧存档加载完成。
2. 新游戏人口系统初始化完成。
3. 每日 Hearth 增长或减少后。
4. 成功征兵事务提交后。
5. 其他会减少城镇、城堡 Hearth 的人口事务后。
6. 事务补偿及恢复后。

村庄不适用职业兵压力上限，不改变村庄征兵与实际户数消费规则。

### 5.2 保留事务原子性

现有 `TryRecruitProfessional` 会同时提交 `_hearth`、`_recruitmentBurden`、`_transactions`。本次必须保留该原子提交结构。

正确顺序：

1. 执行现有每日人口结算检查。
2. 读取最新 Hearth/Burden。
3. 使用新公式审批完整请求。
4. 计算招募后的 Hearth/Burden。
5. 验证上限。
6. 原子提交事务及人口状态。
7. 执行后续招募。
8. 后续失败时使用现有事务补偿。

不能先增加压力，再发现超过上限后简单截断，因为这会使人口事务记录与真实负担变化不一致。

### 5.3 非征兵人口损失与补偿

必须特别处理当前的 `TryConsume`、`TryRestore`、`TryCompensate` 和 `SetHearth` 路径。

例如 `H=500, B=290`，人口损失导致 `H=450`。新的压力上限为 270，因此需要将 B 规范化至 270。

但不能在执行人口事务时悄悄修改 B，而让对应的事务记录和补偿逻辑仍然认为 B 没有变化。

建议复用 `PopulationTransactionRecord` 现有的 `BurdenBefore`、`BurdenAfter`、`BurdenDelta` 和事务类型字段，记录因人口减少而产生的压力规范化。

如果需要区分普通人口消费与带压力调整的人口消费，可扩展 `TransactionKind` 语义，但不得重编号已有 `[SaveableProperty(1..10)]`。

补偿时同时核对 Hearth 和 Burden；无法安全恢复时沿用现有 RecoveryRequired 机制，不得静默制造额外人口或负压力。

### 5.4 每日压力恢复

保持既有每日结算机制和同日去重逻辑。

先使用结算前的压力计算人口变化：

```
B_recovered = max(0, B_before - 4)
```

得到当日 Hearth 后再应用上限：

```
B_after = min(B_recovered, 0.60 × H_after)
```

如果因为 Hearth 下降触发额外压力规范化，单独记录该调整量，不得与自然恢复的 4 点混为一谈。

不得修改 `_lastDailyTick` 的持久化语义。

## 六、ModifiedPolitics：繁荣度计算调整

### 6.1 分离军事容量和经济有效户数

当前 `GetEffectiveHearth()` 的 `max(0,H-B)` 语义不应直接改成经济公式，以免影响历史调用方。

新增可复用的纯计算函数：

```
EconomicHearth = max(0, H - 0.25 × min(B, 0.60 × H))
```

经济有效户数仅服务于繁荣度及相关经济预测。军事招募继续使用独立的压力上限审批。

不要创建第二个持久化的“经济户数”字典。

### 6.2 修改繁荣度模型

文件：`ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/SettlementEconomyProsperityModel.cs`

现有：

```
SupportedProsperity = GetEffectiveHearth(settlement) × PerHearth
```

改为：

```
SupportedProsperity = EconomicHearth × PerHearth
```

城镇 `PerHearth=5`，城堡 `PerHearth=3`。

保持现有超额繁荣度 84 天衰退规则、原版繁荣度计算及 `ExplainedNumber` 描述。

在实际户数不变时，最大征兵压力为 `60% × Hearth`，经济折算系数为 `25%`，所以征兵压力造成的**直接繁荣度支撑减少最多为 15%**。

这不是实际繁荣度总下降幅度的硬上限；粮食、人口真实减少以及其他经济因素仍按原有模型影响繁荣度。

### 6.3 修改人口增长模型

文件：`ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Models/PopulationGrowthModel.cs`

要求：

- `CalculateDailyForecast` 的 `SupportProsperity` 使用同一经济有效户数函数。
- 不改变自然增长、粮食贡献和繁荣度正反馈的其他计算规则。
- `burdenPerCapacity` 改为从 XML 读取 `0.00015`。
- 不要额外叠加第二次 0.25 折算，防止重复减弱同一项压力。

### 6.4 XML 配置

文件：`ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml`

原 `DailyFactors burdenPerCapacity="0.0003"` 改为：

```
burdenPerCapacity="0.00015"
```

在 `ProsperitySupport` 节点增加：

```
recruitmentBurdenFactor="0.25"
```

对应更新 `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs`。

要求该配置使用非负浮点数，且范围不超过 1。

经济折算系数只在 ModifiedPolitics 定义，军事上限比例只在 ModifiedArmy 定义。

## 七、UI 和日志一致性

涉及：

- `SettlementPopulationVMMixin.cs`
- `FiefMenuBehavior.cs`
- `SettlementManpowerProductionBehavior.cs`
- 当前人口事务及职业兵招募诊断日志

原 UI 的 `GetRecruitmentCapacity()` 与 `H-B` 计算不再足以表示真实可用招募额度。

| 信息 | 含义 |
|---|---|
| 实际户数 | Hearth |
| 征兵压力 | Burden |
| 压力上限 | 0.60 × Hearth |
| 剩余压力额度 | max(0, 压力上限 - Burden) |
| 经济有效户数 | Hearth - 0.25 × 有界 Burden |
| 繁荣度支撑 | 经济有效户数 × 支撑系数 |
| 实际可招募人数 | 根据具体兵种 Cost 和事务审批计算 |

可以保留原有通用容量 API 的兼容语义并新增明确查询，也可以在核对全部调用方后修改其语义，但不能让 UI 把压力额度错误显示成可招募士兵人数。

新增日志至少应能检查：Hearth、Burden、MaxBurden、RemainingBurden、TroopTier、BurdenCost、RequestedCount、ApprovedCount、拒绝原因。

减少重复或逐兵刷屏日志，遵循现有日志过滤规则。

## 八、旧存档兼容

沿用现有持久化键：

- `Hearth.v1`
- `Transactions.v1`
- `Recovery.v1`
- `LastDailyTick.v1`
- `RecruitmentBurden.v1`

不重命名这些键，不修改已有 SaveableTypeDefiner ID，不重编号已有存档属性。

旧存档加载完成后：

1. 读取 Hearth 和 Burden。
2. 对超过新上限的合法历史压力进行一次规范化。
3. 保留实际户数不变。
4. 不改变历史已招募士兵人数。
5. 记录一次迁移日志。
6. 后续正常执行每日恢复。

例如：

```
H=400, B=500
→ H=400, B=240
```

不能因为存在历史超限压力就冻结整个城镇。

但 NaN、Infinity、负数或损坏事务等真实异常状态，仍应走现有错误处理，不得借规范化静默掩盖存档损坏。

迁移逻辑应放在配置和人口状态均已加载的适当生命周期内，不能在 `SyncData` 保存阶段重复执行。

## 九、测试要求

### 9.1 纯数学测试

| Hearth | Burden | T4 Cost | 请求数 | 预期批准 |
|---:|---:|---:|---:|---:|
| 1000 | 0 | 20 | 50 | 29 |
| 1000 | 200 | 20 | 50 | 19 |
| 1000 | 400 | 20 | 50 | 9 |
| 1000 | 500 | 20 | 50 | 4 |
| 1000 | 580 | 20 | 1 | 0 |
| 500 | 295 | 20 | 1 | 0 |
| 0 | 0 | 20 | 1 | 0 |

还要测试不同 Cost 的混合兵种、批次分割、浮点边界及零/非法参数。

### 9.2 经济计算测试

例如 `H=1000`、城镇每户支撑 5 繁荣度：

| Burden | 经济有效户数 | 繁荣度支撑 |
|---:|---:|---:|
| 0 | 1000 | 5000 |
| 200 | 950 | 4750 |
| 400 | 900 | 4500 |
| 600 | 850 | 4250 |

历史超限压力先规范化；经济计算本身也应具有安全边界。

### 9.3 事务测试

必须验证：

- 招募前预览与实际批准一致。
- 不允许招募后压力超过上限。
- 成功招募减少 Hearth 并增加正确 Burden。
- 招募失败不扣 Hearth、不增加 Burden。
- 重复 OperationId 不重复招兵。
- 补偿能够正确处理 Hearth 和 Burden。
- 非征兵人口减少不会留下永久超限压力。
- 每日恢复只执行一次。
- 恢复后可重新招募。
- 加载旧存档不会破坏历史事务和军队。

### 9.4 游戏回归

至少覆盖帝国、阿塞莱、瓦兰迪亚、斯特吉亚、巴旦尼亚、库赛特、诺德七文化，对普通城镇及城堡分别测试。

- 30 游戏日：检查压力上升是否按上限停止。
- 60 游戏日：检查是否存在持续零补员。
- 120 游戏日：观察战时补员与停战恢复。
- 人口下降：确认压力同步规范化。
- 驻军已满与未满：确认压力名额不干扰驻军编制。
- 异文化统治：确认原有文化效率及兵种开放规则不变。

重点检查诺德和巴旦尼亚是否因为高等级兵种成本而长期无法招募，检查帝国和阿塞莱是否能在压力上限内合理持续补员。

本阶段不为解决这些文化差异而再次擅自修改训练速度或恢复值。

## 十、禁止事项

- 不重做 Hearth 人口系统。
- 不重新引入兵源池或共享基础兵源。
- 不增加第二套征兵压力存档。
- 不改变封邑兵供养规则。
- 不修改文化军事制度权重。
- 不改变职业兵模板和兵营开放等级。
- 不为了绕过压力限制直接给 AI 凭空补兵。
- 不通过新的 Harmony Patch 替代已有服务接口。
- 不把纯数学公式散落复制到多个调用方。
- 不把经济有效户数重新拿来直接决定军事招募。
- 不为了快速通过测试破坏原有事务补偿、存档安全和日志约定。

## 十一、施工顺序与交付要求

1. **配置与数学模型**：修改 XML、配置解析和统一招募容量纯函数。
2. **ModifiedPolitics 人口状态**：修改职业兵审批、事务状态、上限规范化和每日结算。
3. **经济模型**：独立经济有效户数，修改繁荣度与人口增长计算。
4. **ModifiedArmy 调用方**：核查自动招募、原版招募、驻军生成、AI 使用和所有直接数学调用。
5. **UI、日志及兼容**：展示新额度、处理旧存档、验证事务补偿。
6. **编译、测试与回归**：确保两个 Mod 及其依赖可以正常加载运行。

Codex 开始施工前必须重新读取仓库最新 HEAD 和相关源码，不能仅凭本方案中的文件路径或过时方法假设直接替换。

交付时必须提供：

- 实际修改文件清单。
- 每个文件的修改内容与原因。
- 新旧规则的行为对照。
- 单元测试及运行结果。
- 编译结果。
- 存档兼容处理说明。
- 未通过的测试与剩余风险。

**验收核心条件：**

1. 正常招募后绝不突破 60% 压力上限。
2. UI 预测与实际招募审批一致。
3. 经济有效户数与军事压力容量分别计算。
4. 征兵压力造成的繁荣度支撑直接折损不超过 15%。
5. 历史超限存档能够恢复到合法状态。
6. 已有征兵事务、回滚和每日结算不发生回归。
7. 七文化保持各自的训练能力与军事制度差异。

---

**特别关注：** `RecruitmentBurdenMath` 的批量审批公式、`SettlementPopulationBehavior` 的事务补偿，以及 UI 中原有 `H-B` 的语义。这三处最容易出现预览/实际不一致、旧存档异常等回归问题。
