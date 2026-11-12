# ModifiedArmy — AI 领主部队粮食补给调整实施方案

## 一、任务目标

调整《骑马与砍杀2：霸主》1.4.7 版本中 AI 领主部队的粮食补给行为。

| 参数 | 当前原版参考值 | 修改目标 |
|---|---:|---:|
| AI 寻找补给的粮食阈值 | 12 天 | **20 天** |
| 城镇采购目标 | 30 天 | **40 天** |
| 村庄采购目标 | 12 天 | **40 天** |
| 创建军团所需粮食 | 严格大于 12 天 | 不变 |
| 被召集部队所需粮食 | 严格大于 15 天 | 不变 |
| 单位粮食消耗 | ModifiedArmy 每 10 人消耗 1 单位 | 不变 |

以上原版采购数值来自已查阅的相邻版本源码。实施时必须核对当前 1.4.7 DLL，不能直接假定其完全一致。

本次属于小范围平衡调整，暂不引入新的补给系统。

## 二、源码调查

首先检查以下源码：

**原版：**

- `DefaultPartyFoodBuyingModel`
- `PartyFoodBuyingModel`
- `AiVisitSettlementBehavior.AiHourlyTick`
- `PartiesBuyFoodCampaignBehavior`
- `DefaultMobilePartyAIModel`

**Mod：**

- `ModifiedArmy/ModifiedArmy/Main.cs`
- `ModifiedArmy/ModifiedArmy/Logistics/Models/NewMobilePartyFoodConsumptionModel.cs`
- `ModifiedArmy/ModifiedArmy/Utils/ModLogger.cs`

重点确认当前 DLL 的方法签名、Model 注册方式及 Harmony 补丁兼容性。

原版源码参考：`code/AiVisitSettlementBehavior.cs`

其中存在：

```csharp
float neededFoodsInDaysThresholdForSiege =
    Campaign.Current.Models.MobilePartyAIModel
        .NeededFoodsInDaysThresholdForSiege;

if (num21 < neededFoodsInDaysThresholdForSiege)
{
    // 提高 AI 前往定居点补给的评分
}
```

注意：原方法使用的 `NeededFoodsInDaysThresholdForSiege` 同时被军团创建等其他系统使用，**禁止全局修改该属性为 20**。

## 三、修改方案 A：AI 低于 20 天时寻找补给

### 1. 修改位置

`AiVisitSettlementBehavior.AiHourlyTick`

只调整该方法中用于计算定居点补给需求评分的阈值。

目标：

```csharp
const float FoodResupplyTriggerDays = 20f;
```

当 AI 部队当前粮食储备低于 20 天时，原版访问定居点的补给评分机制开始生效。

### 2. 实现方式

由于 `AiVisitSettlementBehavior` 属于原版 CampaignBehavior，使用 Harmony Transpiler 对特定 IL 调用点进行最小替换。

建议：

- 新增 `Logistics/Patches/AiFoodResupplyThresholdPatch.cs`。
- 仅定位 `AiHourlyTick` 中读取 `NeededFoodsInDaysThresholdForSiege` 的调用点。
- 将该调用替换为返回 20f 的专用方法。
- 保持原方法其他 IL 指令、控制流程和评分公式不变。
- 运行时检查替换数，预期只匹配一次；如果版本不符，明确记录失败，不能静默认为已生效。

替换方法的参数和返回类型必须与目标 IL 栈签名一致，不允许仅用 `ldc.r4` 覆盖实例属性调用而留下错误栈状态。

**禁止修改：**

- `MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege`
- `MobilePartyAIModel.NeededFoodsInDaysThresholdForRaid`
- `CanLordCreateArmy`
- 军团创建、攻城和劫掠的粮食门槛
- AI 原版定居点评分算法中的其他系数

注意，20 天只是补给行为开始获得额外评分的阈值，并不代表 AI 必须立即停止所有军事行动前往补给。

## 四、修改方案 B：采购目标统一为 40 天

### 1. 修改位置

`DefaultPartyFoodBuyingModel`

新增：`ModifiedArmy/ModifiedArmy/Logistics/Models/NewPartyFoodBuyingModel.cs`

采用 Model 继承：

```csharp
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Logistics.Models
{
    public class NewPartyFoodBuyingModel
        : DefaultPartyFoodBuyingModel
    {
        public override float MinimumDaysFoodToLastWhileBuyingFoodFromTown
            => 40f;

        public override float MinimumDaysFoodToLastWhileBuyingFoodFromVillage
            => 40f;
    }
}
```

以上代码以实际游戏 DLL 的公开 API 可用为前提。

### 2. 注册 Model

在 `ModifiedArmy/Main.cs` 的 `OnGameStart` 中注册：

```csharp
campaignStarter.AddModel(
    new NewPartyFoodBuyingModel());
```

核对原版、战帆 DLC 和其他 Mod 的 Model 注册顺序，避免覆盖其非目标功能。

如果运行时已经存在其他 `PartyFoodBuyingModel` 扩展，则应保留其余行为，必要时改成委托底层 Model 的实现。

### 3. 保持原版购买流程

不重写：

- `PartiesBuyFoodCampaignBehavior`
- `CalculateFoodCountToBuy`
- `BuyFoodInternal`
- `FindItemToBuy`

保留原版：

- 真实市场库存扣减
- 购买价格
- 部队资金限制
- 粮食商品选择逻辑
- 军团成员采购规则

新 Model 只改变目标粮食储备天数。

**绝不生成免费粮食，也不突破市场库存或购买资金限制。**

## 五、验证与 Debug

在游戏加载后输出一次：

```text
[AI Food Supply] phase=MODEL_ACTIVE
foodResupplyTriggerDays=20
townTargetDays=40
villageTargetDays=40
foodBuyingModel=实际类型
patchReplacementCount=1
```

在领主部队实际补给时，建议增加文件级 Debug，记录：

```text
[AI Food Supply]
party=戈里戈斯的部队
foodDaysBefore=7
foodDaysAfter=实际数值
targetDays=40
settlement=实际城镇或村庄
requestedFood=计划采购量
purchasedFood=实际采购量
goldBefore=原资金
goldAfter=剩余资金
```

日志只写文件，不频繁弹出游戏提示。

避免每小时记录所有领主的完整评分，仅对实际补给事件、配置初始化及异常情况输出详细日志。

## 六、测试要求

完成后进行以下验证：

1. **20 天阈值**：19 天粮食时补给评分机制启用；20 天及以上不触发该额外评分。
2. **城镇采购**：检查采购目标为 40 天，实际购买数量受库存、资金和价格约束。
3. **村庄采购**：检查采购目标为 40 天。
4. **军团规则**：原有 12 天统帅门槛、15 天候选部队门槛保持不变。
5. **消耗规则**：`NewMobilePartyFoodConsumptionModel` 仍保持每 10 人消耗 1 单位。
6. **Model 兼容性**：战帆 DLC 加载后，采购目标仍然是 40 天。
7. **存档兼容性**：能够加载已有存档，不新增不必要的 Saveable 字段。

Release 编译必须无错误。静态编译不等于游戏内验证通过，应明确区分。

## 七、交付要求

由 Codex 完成：

- 新增补给阈值 Harmony Patch。
- 新增粮食采购 Model。
- 在 `Main.cs` 注册新 Model。
- 增加必要的诊断日志。
- 编译 ModifiedArmy。
- 检查对 ModifiedPolitics 的兼容性。
- 报告实际修改的文件、补丁匹配数量、编译结果和未完成的实机验证。

**不要借此调整其他 AI 决策、常备军招募、封建兵、驻军粮食或经济参数。**

## 八、本轮观察目标

部署后让游戏运行若干天，重点观察西帝国的戈里戈斯与加里俄斯：

- 粮食是否稳定高于 12 天。
- AI 是否更早主动前往城镇或村庄。
- 进入定居点后是否能购满目标粮食。
- `CanLordCreateArmy` 是否开始通过统帅粮食检查。
- 是否暴露新的候选部队、军团容量或战斗力限制。

如未能获得预期效果，优先根据日志分析真实采购失败原因，不要立即继续提高粮食目标或降低军团门槛。
