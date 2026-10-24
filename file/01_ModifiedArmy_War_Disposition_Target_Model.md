# W1｜战争倾向动态阈值与战时Party目标兵力——设计及实现方案

> 状态：待开发；基线 `main@ccd222b`；Owner：Worker W1；独立于W2–W5。  
> 目标：将现在“配置阈值只改变IsAggressiveWarClan标记”的行为，变为“配置阈值真正控制战争动员目标”。

## 1. 现状与已确认Bug

现有代码：`ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs` → `PartyForceTargetModel.Evaluate()`：

```csharp
bool aggressive = atWar && disposition > config.AggressiveWarDispositionThreshold;
// ...
else if (atWar) {
    scenario = ForceScenario.WarRoutine;
    ratio = GetWarTargetRatio(disposition); // BUG：不读取配置阈值
}
```

`GetWarTargetRatio(float disposition)` 固定采用旧节点0/20/60，对 `AggressiveWarDispositionThreshold` 无感；`ModuleData/militaryConfig.xml` 当前阈值`-50`。因此将T从20改为-50不会自动让倾向-50的Clan目标提高到60%以上。

现有 `WarRestPolicyModel.CalculateUrgency()` 会消费 `IsAggressiveWarClan`，但WarRest默认关闭；不能把这个旗标当作扩军已经生效的证据。

## 2. 需求确认与数学模型

- `D`：`IWarDispositionProvider` 提供的Clan战争倾向，裁剪到`[-100,100]`。
- `T`：`ForceManagementConfig.AggressiveWarDispositionThreshold`，XML当前值`-50`，必须真正决定曲线。
- `R`：战争常规场景 `ForceScenario.WarRoutine` 中队伍编制上限的目标比例。
- 强制锚点：`D <= -80 → 30%`；`D=T` → **61%**；`D >= 80 → **95%**`。
- 防止阈值前误扩军：`D<T` 的比例**不得超过50%**，且 `[T-10,T)` 固定50%。
- 曲线允许在`D=T`发生**刻意的不连续跳变**，`50% → 61%`，体现突破原版严格大于60%的Army人数门槛。

分段函数：

```text
D <= -80               R = 0.30
-80 < D < T-10         R = 0.30 + 0.20 * (D+80)/(T+70)
T-10 <= D < T          R = 0.50
T <= D < 80            R = 0.61 + 0.34 * (D-T)/(80-T)
D >= 80                R = 0.95
```

这里需保证`T>-70`和`T<80`，否则分母可能无效，平台也可能反转。**建议可配置合法范围 `[-60,60]`**（是执行建议，用户只明确要求“动态可配置”，未单独规定边界）；最终实现必须在配置解析时校验，禁止除零、NaN以及静默错误配置。

**T=-50验收表**：

| D | -100 | -80 | -70 | -60 | -55 | -50.001 | -50 | -40 | 0 | 40 | 80 | 100 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| R | 30% | 30% | 40% | 50% | 50% | 50% | 61% | 63.62% | 74.08% | 84.54% | 95% | 95% |

**整数人数**仍使用现有 `target = min(limit, max(0, ceil(limit * R)))`。不得将战时最低人数理解成“固定30人”：**30%是比例而不是人数**。

## 3. 需要修改的文件与位置

1. `ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs`：`PartyForceTargetModel.Evaluate()`、`GetWarTargetRatio`。
2. `ModifiedArmy/ModifiedArmy/common/MilitaryConfig.cs`：`ForceManagementConfig.AggressiveWarDispositionThreshold` 默认值与 `ParseForceManagement()` 的边界解析。
3. `ModifiedArmy/ModifiedArmy/ModuleData/militaryConfig.xml`：更新旧“超过才扩军”的注释，明确达到T时61%、T以下最高50%；当前值仍`-50`。
4. 如有当前项目测试工程：添加目标曲线边界测试。没有就写无游戏依赖的测试表/小型测试helper并标明运行结果，避免引入多余测试基础设施。

### 3.1 推荐的最小函数替换

```csharp
// 将原静态函数签名从 GetWarTargetRatio(float) 改为双参数；
// 若其他调用者依赖旧签名，应搜索并同步，不另留悄悄使用旧曲线的入口。
public static float GetWarTargetRatio(float disposition, float threshold)
{
    float d = Math.Max(-100f, Math.Min(100f, disposition));
    // 配置应在加载处验证；这里防御不应掩盖坏配置。
    if (float.IsNaN(threshold) || float.IsInfinity(threshold) ||
        threshold < -60f || threshold > 60f)
        throw new ArgumentOutOfRangeException(nameof(threshold));

    if (d <= -80f) return 0.30f;
    if (d < threshold - 10f)
        return 0.30f + 0.20f * (d + 80f) / (threshold + 70f);
    if (d < threshold) return 0.50f;
    if (d < 80f)
        return 0.61f + 0.34f * (d - threshold) / (80f - threshold);
    return 0.95f;
}
```

Worker应确保实际文件已有 `using System;`，并避免以 `Math.Clamp` 替换为目标Framework不支持的API。既有 `Math.Max/Min` 可复用。

### 3.2 在Evaluate中传递同一个阈值

```csharp
float t = config.AggressiveWarDispositionThreshold;
bool aggressive = atWar && disposition >= t; // 关键：在T即视为积极
...
ratio = GetWarTargetRatio(disposition, t);
```

说明：当前使用`>`；需求指定“达到动态阈值就突破”，应统一改为`>=`。`IsAggressiveWarClan`目前主要供WarRest读；虽然默认关闭，仍应与曲线语义一致。

### 3.3 配置解析

- 现有 `BoundedFloatValue(node,...,-100f,100f)` 能接受负数；应将允许范围收窄为`-60f,60f`（若采用此推荐边界）。
- `ForceManagementConfig`代码默认建议与XML一致设为`-50f`，避免缺配置时退回旧的`20f`；旧存档无新增字段。
- 不要使用`OptionalValue/NonNegativeFloat`解析战争倾向（会错误拒绝负阈值）。
- XML注释说明“修改配置在重载模块/配置后生效”，**不是承诺玩家在游戏进行时热更新**。

## 4. 与现存兵力体系的边界

**保留、不改动：**

- `ForceScenario.Peace`：`PeaceTargetRatio=0.35`、`PeaceMinimumCount=50`，还需受 PartySizeLimit 限制。
- `ForceScenario.OwnFiefDefense`：`OwnFiefDefenseTargetRatio=1.00`，自有封邑防御优先。
- `GetPartyTarget()`唯一入口、`CanStartRecruitmentVisit`、NeedScore、随机门、预算、征召来源偏好、驻军下限、Daily规划/调度。
- `WarRestPolicySettings.Enabled=false`；不因为低战争倾向目标变30%而立即复员、退伍或丢弃现有士兵。
- 和平自动复员属于另一机制，不因本次战时公式直接修改。

**Army判定说明：** 仓库 `code/DefaultArmyManagementCalculationModel.cs` 中`AIMobilePartySizeRatioToCallToArmy=0.6f`，召集候选要求 `mobileParty.PartySizeRatio > ...`。61%目标只是让补员后更容易达到数量门槛；食品、距离、军团状态等仍会制约；若D<T的部队本来就已超过60%，仅调低*目标值*不会自动禁其参军。**本次不得Patch原版 Army资格判断**，除非另有明确需求。

## 5. 质量、日志和防回归

建议在既有目标规划调试日志中按已有日志过滤显示：`D,T,scenario,ratio,limit,current,target,gap,aggressive`；只记录已有计划/诊断点，**避免每日为全地图Clan输出日志**。日志工作不要与W2互相覆盖：W1只增加战时计算参数，W2负责审批失败诊断。

**必须验收：**

1. `[D=-80,-70,T-10,T-ε,T,T+ε,0,80,100]` 精确点和连续插值。
2. 更改XML的T（例如`-50→0→20`）在相同D下产生可预测的目标变化；不是只改变旗标。
3. 对全部`D<T`测试`R<=0.50`；全部`D`测试`0.30<=R<=0.95`；除阈值处跳变外单调不减。
4. `T=-60`、`T=60`无除零；非法配置应明确失败并报告属性，不能悄悄返回失真值。
5. `limit=0,1,50,126,173,500`正确使用Ceiling并不超编。
6. 和平与守家防御目标完全不变；战时已超目标的队伍**不会立刻遭到删除/解散**。
7. 与W2诊断叠加后日志能区分“目标值变更”与“执行时批准0”。

## 6. Worker交付检查清单

- [ ] 修改上述3个代码/配置文件，无额外Army调用Hook。
- [ ] 附T=-50和T=0的计算样表或测试输出；不得把-80写为0%。
- [ ] 说明为何`>=T`、为何61%以及原版Army严格 `>0.6`。
- [ ] 项目编译、模组加载、旧存档与至少多个Clan的战时验收记录。
- [ ] 提交建议：`fix(army): make configurable war threshold drive party targets`。
- [ ] 未运行的实机测试明确列出，不得冒充完成。
