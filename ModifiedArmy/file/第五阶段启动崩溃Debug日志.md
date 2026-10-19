# 第五阶段启动崩溃 Debug 日志

## 1. 现象

第五阶段 DLL 部署后，Bannerlord 在启动过程中退出。崩溃发生在进入主菜单之前，未进入战役，也未执行每小时增援任务逻辑。

截至 2026-10-20 00:33，至少观察到以下重复失败：

| 启动日志 | DLL 成功加载 | 日志停止时间 | Windows 崩溃事件 |
|---|---:|---:|---:|
| `rgl_log_22744.txt` | 00:31:35.884 | 00:31:37.606 | 00:31:40 |
| `rgl_log_13712.txt` | 00:32:00.532 | 00:32:02.320 | 00:32:04 |
| `rgl_log_28284.txt` | 00:32:42.768 | 00:32:44.531 | 00:32:46 |
| `rgl_log_22632.txt` | 00:33:05.053 | 00:33:06.816 | 00:33:08 |

更早的同类失败还包括 2026-10-19 23:46 和 23:51。

## 2. Bannerlord RGL 日志

所有失败样本都记录：

```text
Loading assembly: ..\..\Modules\ModifiedArmy\bin\Win64_Shipping_Client\ModifiedArmy.dll
Assembly load result: SUCCESS
```

随后游戏继续完成：

- `ModifiedPolitics.dll` 和 `ModifiedDiplomacy.dll` 加载；
- `System.Numerics.Vectors` 解析；
- locale 初始化；
- banner icons 读取；
- GauntletUI 资源目录收集；
- 字体加载；
- `MusicParameters have been resetted.`

日志在这里突然结束，没有 RGL 错误正文。对应的 `rgl_log_errors_*.txt` 只有日志头和游戏 Build Version，没有异常信息。

结论：`ModifiedArmy.dll` 文件本身可以被 CLR 加载；故障发生在启动初始化阶段，而不是找不到 DLL、程序集加载失败或战役运行阶段。

## 3. Watchdog 证据

`watchdog_log_22632.txt` 记录：

```text
Crash occurred. Asking for dump.
User canceled dump and report generation!
EXIT_PROCESS_DEBUG_EVENT occurred. Detaching...
```

该次启动参数包含：

```text
Bannerlord.Harmony
Bannerlord.ButterLib
Bannerlord.UIExtenderEx
Bannerlord.MBOptionScreen
Native
SandBoxCore
Sandbox
CustomBattle
StoryMode
BirthAndDeath
NavalDLC
ModifiedTroop
ModifiedArmy
ModifiedPolitics
ModifiedDiplomacy
```

由于转储生成被取消，当前没有可用于解析的 dump 或托管堆栈。

## 4. Windows 事件日志

连续失败均产生相同签名：

```text
Application: TaleWorlds.MountAndBlade.Launcher.exe
Fault module: KERNELBASE.dll
Exception code: 0xe0434352
Fault offset: 0x00000000000c408a
```

`0xe0434352` 表示 CLR 托管异常越过了未处理边界。`KERNELBASE.dll` 是异常最终被系统报告的位置，不足以说明错误来自 Windows 内核或显卡。

最近一次报告：

```text
Time: 2026-10-20 00:33:08
Process ID: 0x5868
Report ID: 0070a13d-45c8-41d2-8d86-d9bad8bca42a
WER archive: AppCrash_TaleWorlds.Mount_59c89aa44ca8043fd487f4fdcbd26e45d79848_7140fc22_0935e59f-89b3-4a89-b7a5-30dae5820fc9
```

多次报告具有相同故障存储段哈希：

```text
6ae24954cd7ab665e5fa662fd3441e2d
```

这表明多次启动命中了同一种稳定故障，而不是随机崩溃。

## 5. ButterLib 与模组日志

- ButterLib 能正确选择并加载游戏 `v1.4.7` 对应实现；
- ButterLib 日志没有记录本次异常堆栈；
- `ModifiedArmy.log` 的最后业务记录停留在 2026-10-19 23:13；
- 00:31 至 00:33 的连续启动失败没有写入新的 ModifiedArmy 业务日志。

结论：崩溃发生在 `OnGameStart` 添加 CampaignBehavior 以及增援任务运行之前。

## 6. 已执行排查

### 6.1 存档注册第一轮修正

最初第五阶段新增独立 `ForceManagementSaveDefiner`，并与现有定义器重复注册 `Dictionary<CharacterObject, int>`。已删除重复注册，但崩溃未消失。

### 6.2 存档结构彻底简化

随后执行：

- 删除独立 `ForceManagementSaveDefiner`；
- 将 `ReinforcementMission` 并入现有 `FiefSaveDefiner`；
- 删除 `Dictionary<ItemObject, int>`；
- 删除 `TroopCargoManifest` 和 `FoodCargoManifest`；
- 只注册 `ReinforcementMission` 与 `List<ReinforcementMission>`。

简化后 DLL 仍以相同时间点、相同 Windows 异常代码崩溃。因此“新增物品字典容器是唯一原因”已被排除，但存档注册整体尚未完全排除。

### 6.3 构建和部署验证

- 编译成功，0 错误；
- 构建 DLL 与游戏目录 DLL SHA-256 完全一致；
- 排除“部署了旧 DLL”这一可能。

## 7. 当前嫌疑排序

### A. 第五阶段 Army Harmony 补丁——高优先级，未证实

`Main.OnSubModuleLoad` 会执行：

```csharp
new Harmony("com.mod.ModifiedArmy").PatchAll(assembly);
```

第五阶段新增两个 Harmony Patch，目标为：

- `DefaultArmyManagementCalculationModel.CanLordCreateArmy`
- `DefaultArmyManagementCalculationModel.CheckPartyEligibility`

该执行时机与启动早期崩溃吻合。可能问题包括目标方法重载解析、参数签名、`ref` 参数或游戏 v1.4.7 实际方法形态不匹配。编译和 Harmony Analyzer 通过不能完全替代运行时补丁验证。

### B. `ReinforcementMission` Saveable 注册——中优先级，未证实

存档 DTO 已大幅简化，但引擎会通过反射发现 Saveable 类型。仍需通过“保留业务代码、临时移除任务 Saveable 注册”的隔离实验完全排除。

### C. CampaignBehavior 和装卸逻辑——低优先级

这些逻辑在 `OnGameStart` 或进入战役后才运行，而现有日志显示崩溃发生得更早，因此不符合当前时间线。

## 8. 当前不能得出的结论

- 不能仅凭 `KERNELBASE.dll` 判定为系统组件问题；
- 不能确认是某一个 Harmony 方法，尚未做单项隔离；
- 不能确认是存档类型 ID 冲突；
- 没有 dump 和托管堆栈，不能给出具体异常类和源码行号。

## 9. 下一步 Debug 操作

1. 禁用两个第五阶段 Army Harmony Patch，保持其他代码不变，测试能否启动。
2. 若能启动，逐个恢复补丁，确认具体故障目标。
3. 若仍崩溃，禁用 `ReinforcementMission` Saveable 注册做第二层隔离。
4. 在 `PatchAll` 和 UIExtender 注册前后增加最小启动日志及异常捕获。
5. 下一次复现时保留 crash dump，不取消报告生成。

