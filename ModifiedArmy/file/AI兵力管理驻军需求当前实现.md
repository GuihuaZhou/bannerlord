# AI兵力管理：驻军防御与外援需求当前实现

本文记录当前代码实现，不修改或替代设计方案。

## 一、需求判断

驻军有效人数沿用第一阶段统一口径：驻军实际名册人数加活跃征粮队预留人数。

- 边境最低线为硬上限的 75%。当前人数低于最低线时立即请求外援，不计算或等待未来七日补员。
- 内陆最低线为硬上限的 50%。当前人数不足时，可以用未来七日预计本地补员判断是否暂缓请求。
- 边境请求人数是“最低线减当前有效人数”；内陆请求人数还会扣除七日预测补员。

例如内陆最低线为 150，当前 120，七日预计补充 10：请求 20 人。边境最低线为 225、当前 210 时直接请求 15 人，预测值不参与判断。

## 二、源驻军安全调出

安全调出人数同时受到两项限制：

```text
编制盈余 = 最大值（0，当前有效人数 - 本地最低线）
安全调出 = 最小值（编制盈余，驻军名册内实际正规士兵数）
```

因此征粮队预留人数可以参与判断驻地是否高于最低线，但无法凭空成为可调出的士兵。第二阶段的实际驻军取兵还会继续排除有服役身份的封邑兵。

## 三、日志

每日评估玩家王国的城镇和城堡，并写入文件日志。日志包括：

- 当前有效守军；
- 最低保留人数；
- 本次判断采用的七日预测补员（边境固定为 0）；
- 是否请求外援、请求人数及 ReasonCode 对应原因；
- 边境或内陆状态。

只有状态发生变化时才重新记录。状态键包含当前人数、最低线、预测人数、请求人数、原因和边境状态，因此缺口数量变化不会被旧的去重逻辑隐藏。

## 四、安全边界

- 本阶段只生成需求判断和安全调出上限，不创建运输 Party。
- 路线模型继续返回 `RouteUnsafeOrUnknown`。
- `EnableAutomaticReinforcement` 继续保持 `false`。
- 自动跨城增援留待后续阶段实现。

## 五、主要代码位置

- `ForceManagement/Models/ForceManagementModels.cs`
- `ForceManagement/Contracts/ForceManagementTypes.cs`
- `ForceManagement/Services/ForceManagementFacade.cs`
- `ForceManagement/Behaviors/ForceManagementBehavior.cs`
- `Garrison/Models/GarrisonRecruitmentCapacity.cs`

## 六、验证状态

- 已通过 Debug 与 Release 构建。
- 未进行 Bannerlord 实机战役测试。
- 本阶段未改变存档格式。
