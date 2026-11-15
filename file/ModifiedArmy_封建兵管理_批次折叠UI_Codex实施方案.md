# ModifiedArmy — 封建兵管理：在役 / 返乡批次折叠 UI 实施方案

> **交付给 Codex 的施工文档**  
> 基准：`GuihuaZhou/bannerlord` 仓库；原版 UI 位于 `code/GUI/SandBox/GUI/Prefabs/Party`，模组位于 `ModifiedArmy`。  
> 任务范围：**改造封建兵管理界面的展示结构，保留既有征募 / 返乡 / 存档机制**。请先阅读本方案列出的现有文件，再编辑代码，勿根据示意 XML 直接盲贴。

## 1. 需求与最终 UI 约束

当前“封建兵管理”界面有三个页签：**在役、待役、返乡**；左侧可选择“全部附属村庄”或具体村庄。现在的主区域使用 `GridWidget` 绘制单个兵种卡片，没有展示批次。

### 1.1 在役页

- **按 LordParty 和真实服役批次展示**：先按 `ServingPartyId`（LordParty）排序，再展示各自 `BatchId`。同一 LordParty 的两个不同批次 **不得合并**，即使它们恰好拥有相同兵种、相同服役天数。
- 每个批次各有一条**可点击的折叠标题行**：部队名、已服役天数、免薪剩余天数或“正常计薪”、本批次人数。
- 标题行下方显示该批次兵种列表。**兵种行复用原版 Party → Member 的视觉结构：左侧兵牌/兵种图标、中间兵种名称、右侧数量**；不出现左侧转移箭头、右侧转移按钮、加减号、绿色血条或进度条。
- 点击批次标题收起/展开本批次。折叠互相独立，收起后兵种行完全消失，不预留空白。
- 不另设“LordParty → 批次 → 兵种”的两级折叠：**批次就是唯一可折叠层级**，部队仅用于分组排序及标题显示。

### 1.2 返乡页

- **按当前 `WaitWeeks` 分组**，同一剩余周数下不同村庄、原始返乡记录的兵种在**展示层**合并（仅在选中“全部附属村庄”时跨村庄合并）。
- 标题示例：`▶ 剩余 2 周 · 共 15 人`；展开后显示原版风格兵种行（兵牌 / 名称 / 数量）。
- 每个剩余周数一组；从短到长排序；每组可单独折叠。
- **不显示“剩余 N 天”作为精确值**：现有后端按周推进 `WaitWeeks`，缺少精确到日的到期时间。若未来要精确天数，另开独立需求处理。

### 1.3 其他页面与交互

- **待役页保持现有可选兵种卡片、征召操作与容量限制不变**。本阶段只调整在役 / 返乡的显示；不要重写待役生产或兵种筛选。
- 左侧村庄筛选、三个页签、窗口外框、标题、关闭按钮及底部操作维持原布局风格。
- 不做新 Sprite / Brush 材质，不修改原版 Party 文件，尽可能使用原版已有 Brush。
- 兵牌右键到兵种百科可选做，但绝不能因此触发招募、返回、升级或转移。
- **现有在役页针对玩家自己部队的封建兵“选中并返还”功能必须保留可达**，不能因换成只读批次列表而无声删除。建议独立“返还当前部队封建兵”入口复用既有操作，或保留一个清晰的旧管理流程。批次列表本身第一版只读。
- 批次信息与标题人数必须来自真实运行时数据；效果图中的具体部队名、天数、数量均非预设常量。

## 2. 已核对的仓库源码与实现依据

### 2.1 原版 Party UI

1. `code/GUI/SandBox/GUI/Prefabs/Party/PartyScreen.xml`
   - `PartyHeaderToggleWidget`：原版点击“士兵（204/210）”收起/展开士兵列表。
   - 折叠头使用 `Brush="Party.Toggle.Right"`，并绑定 `CollapseIndicator`、`ListPanel` 与 `WidgetToClose`。
   - 兵种区域使用 `PartyListPanel` 与 `PartyTroopTuple` / `PartyTroopTupleLeft`。
   - **注意**：原版包含部队转移逻辑，不可整页照搬。
2. `code/GUI/SandBox/GUI/Brushes/PartyScreen.xml`
   - `Party.Toggle.Left` / `Party.Toggle.Right`：原版折叠标题背景。
   - `Party.Toggle.ExpandIndicator`：有 `Collapsed` (`StdAssets\\collapsed`) 和 `Expanded` (`StdAssets\\expanded`) 状态。
   - `Party.TroopTupple.Right`、`Party.TroopBack`、`Party.Text.Toggle` 等原版外观。
3. `code/GUI/SandBox/GUI/Prefabs/Party/PartyTroopTuple.xml`
4. `code/GUI/SandBox/GUI/Prefabs/Party/PartyTroopTupleLeft.xml`
   - 可参考兵种头像、兵种类型/等级图标、名称、数量的尺寸、Brush 和数据绑定。
   - 原版 `PartyTroopTupleButtonWidget` 带转移/升级业务数据，**不作为本 Mod 的整件可复用组件**；应以轻量只读行 VM + 原版 Brush 复刻同一行外观。

### 2.2 ModifiedArmy 当前界面

1. `ModifiedArmy/ModifiedArmy/Fief/Presentation/FiefManagementTroopSelection.xml`
   - 现有外框 `Recruitment.Frame`，宽 `1500`、高 `860`；左边村庄，顶部页签，中央 `GridWidget Id="FiefTroopsGrid" DataSource="{Troops}"`，底部 `Standard.TriplePopupCloseButtons`。
   - 推荐保留外框/导航、使 `FiefTroopsGrid` 只在待役页显示；在役/返乡新增批次可滚动列表。
2. `ModifiedArmy/ModifiedArmy/Fief/Presentation/FiefManagementTroopSelectionPrefabExtension.cs`
   - 通过 UIExtenderEx `[PrefabExtension("GameMenuTroopSelection", ...)]` 注入该 XML，隐藏原版选择面板。
   - 尽量继续使用当前注入机制，避免新建第二套窗口生命周期。
3. `ModifiedArmy/ModifiedArmy/Fief/Presentation/FiefManagementTroopSelectionVMMixin.cs`
   - 绑定 `GameMenuTroopSelectionVM`，已有 `Sources`、三个状态文本/选中状态与 `ExecuteSelectReady/Serving/Cooling`。
   - `RefreshManagementView(true)` 现在通过 `_fullRoster`/`InitList` 重新生成**合并**兵种列表。
4. `ModifiedArmy/ModifiedArmy/Fief/Presentation/FiefManagementPresentationContext.cs`
   - `GetFullRoster()` / `GetCount()` 基于 `FiefPartyManager.GetFiefManagementRoster`，其结果按兵种汇总，**不能直接作为批次分组来源**。
   - `Commit()` 调用现有征募或返还操作；当前在役仅允许从玩家本人部队选出并返还。
5. `ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs`
   - `OpenFiefManagement()` 通过 `args.MenuContext.OpenTroopSelection` 创建当前界面，`onDone: context.Commit`。

### 2.3 数据已经存在，无需存档迁移

- `ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefServiceBatch.cs`
  - `BatchId`、`ServingPartyId`、`ServingParty`、`SourceFief`、`SourceVillage`、`ServiceDays`、`FreeWageDays`、`Troops`。
  - `ServiceDays` 每日推进，**现行实现没有固定服役到期日**。
- `ModifiedArmy/ModifiedArmy/Fief/Models/Data/FiefReturnBatch.cs`
  - `WaitWeeks`、`Troops`，每周 `Tick()` 递减；不是精确日倒计时。
- `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`
  - `ServiceBatches`、`ReturnBatches`：真正的分组来源。
- `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`
  - `OrderedVillages(villageFilter)`：当前封邑及村庄过滤。
- `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs`
  - `GetFiefData`、`GetServingPartiesForFief`、`GetServingDetachments`、`GetFiefManagementRoster`。
- `ModifiedArmy/ModifiedArmy/Fief/Models/FiefServiceReconciliation.cs`
  - `ServingDetachmentRef` 含来源封邑/村庄/批次引用。

## 3. 首先处理的两项业务口径问题

### 3.1 在役不具备“剩余服役时间”

`FreeWageDays` 意味着**免薪期**，不是服役到期日。当前代码在免薪结束后继续服役，恢复正常薪资。正确标题应采用：

- `已服役 18 天 · 免薪剩余 12 天`（`max(0, FreeWageDays - ServiceDays)`）。
- 若 `ServiceDays >= FreeWageDays`，显示 `已服役 40 天 · 正常计薪`。
- 不可把 `FreeWageDays - ServiceDays` 标成“剩余服役”。

若需求将来改为强制期满返乡，需要单独修订服役生命周期，**不包含在此次 UI 施工中**。

### 3.2 返乡按周而非按日

`FiefReturnBatch.WaitWeeks` 是按周更新的冷却计数，不应直接写成 `WaitWeeks * 7 天` 的精确倒计时。

- 标题：`剩余 1 周` / `剩余 2 周`。
- 如果出现 `WaitWeeks <= 0`、但因为现有运行时原因尚未转入待役，显示 `待处理（0 周）`，不要误判已返乡完成。
- 只读 UI 不得自行调用 `Tick()`、`AdvanceServiceDays()` 或调整任何批次状态。

## 4. 推荐的数据读取与 ViewModel 结构

### 4.1 新增展示快照 / 读模型（不保存）

在 `ModifiedArmy/ModifiedArmy/Fief/Presentation/` 下新增适当的查询辅助类（名称可调整），从 `FiefPartyManager.GetFiefData(fief)` → `FiefPartyData.OrderedVillages(villageFilter)` 获取来源村庄。

**在役**（仅对真实批次逐一读取）：

1. 获取当前管理的源封邑，按左侧筛选应用 `villageFilter`。
2. 遍历 `village.ServiceBatches`，跳过空批次和无兵条目；`GroupKey` 优先使用稳定 `BatchId`，必要时加上 `SourceVillage.StringId` 消歧。
3. 通过 `ServingParty` 或 `ServingPartyId` 关联所属 `MobileParty`；部队名来自实时 `MobileParty.Name`；丢失引用时显示可理解的占位名称，**不要修改或删除记录**。
4. 排序：`ServingPartyId`、`ServiceDays`、`BatchId`；同一 Party 的不同 `BatchId` 分别显示自己的折叠标题。
5. `TotalCount` 等于批次 `Troops` 正数之和；兵种行只展示 `{CharacterObject, Count}`；不得从 Party 的整个 MemberRoster 取数替代封建兵批次。

**返乡**：

1. 遍历筛选后村庄的 `ReturnBatches`，跳过空批次。
2. 按 `WaitWeeks` 分组，并**仅在内存展示层**累计同一 `CharacterObject` 的数量。
3. `GroupKey` 采用 `cooldown:{WaitWeeks}`；升序排列；显示 `TotalCount` 和兵种行。
4. 不把合并结果写回 `ReturnBatches`，不改变原始返乡批次记录。

### 4.2 ViewModel 建议

```text
FiefManagementTroopSelectionVMMixin   (已有)
 ├─ IsReadyListVisible
 ├─ IsBatchListVisible
 ├─ Groups : MBBindingList<FiefManagementBatchGroupVM>
 └─ Source / State 切换时 RefreshBatchGroups()

FiefManagementBatchGroupVM : ViewModel    (新增，纯展示)
 ├─ GroupKey : string
 ├─ HeaderText : string
 ├─ TotalCount : int
 ├─ IsExpanded : bool
 ├─ Troops : MBBindingList<FiefManagementTroopRowVM>
 └─ ExecuteToggle() : void

FiefManagementTroopRowVM : ViewModel      (新增，纯展示)
 ├─ Name : string
 ├─ Count : int
 ├─ Visual : 已核实可用于 ImageIdentifierWidget 的数据结构
 ├─ TierIconData / TypeIconData（若需要）
 └─ ExecuteOpenEncyclopedia()（可选）
```

`ViewModel` 的 setter 中使用 `OnPropertyChangedWithValue(...)`，通过 `[DataSourceProperty]` 和 `[DataSourceMethod]` 做绑定。`IsExpanded` 的变化只控制显示，不重建真实批次。

**不要假定自定义 RowVM 与原版 `PartyCharacterVM`/`GameMenuTroopSelectionItemVM` 的头像数据类型一致。** Codex 必须阅读现有角色头像绑定方式，复用已在项目中可用的 `ImageIdentifierWidget` 所需 `DataSource` / `TextureProviderName` / `Id`，避免出现空头像。不可为了显示而把 RowVM 绑定到部队转移逻辑。

### 4.3 折叠状态记忆

- 首次打开批次默认**全部展开**。
- 同一窗口内切页签、切村庄后可按 `GroupKey` 恢复每组折叠状态（比如 `Dictionary<string, bool>`，纯运行时）。
- 不增加 Saveable 字段；关闭窗口状态可清空。
- 动态刷新时，尽量复用已有组 VM 或按 GroupKey 记住展开状态，避免全部跳回展开。

## 5. Gauntlet 布局实施

### 5.1 尽量只改现有 `FiefManagementTroopSelection.xml`

- 保留 `Recruitment.Frame`、左侧 `Sources`、顶部三个按钮、关闭按钮、背景凹槽、右侧滚动条和底部 `Standard.TriplePopupCloseButtons`。
- `FiefTroopsGrid` 及其父级 `ScrollablePanel` 仅在 **Ready** 时显示。
- 在同一主区域增加另一个 `ScrollablePanel`，仅在 **Serving / Cooling** 时显示，包含垂直 `ListPanel DataSource="{Groups}"`。
- 保留外层尺寸及滚动裁剪；避免同一区域同时显示两个滚动条。
- 由于动态 ItemTemplate 中 `PartyHeaderToggleWidget` 的 `ListPanel`/`WidgetToClose` 相对路径绑定可能与原版固定结构不同，**优先使用普通 ButtonWidget + `Party.Toggle.*` Brush + VM 的 `ExecuteToggle()`**；如果想使用 `PartyHeaderToggleWidget`，须在当前 Gauntlet 版本中实际验证动态多实例折叠行为与路径引用。

### 5.2 折叠区域概念结构（仅结构示意，不可直接复制部署）

```xml
<ListPanel DataSource="{Groups}"
           StackLayout.LayoutMethod="VerticalTopToBottom">
  <ItemTemplate>
    <ListPanel StackLayout.LayoutMethod="VerticalTopToBottom">
      <!-- 批次标题：ButtonWidget，Brush="Party.Toggle.Right" -->
      <!-- Command.Click="ExecuteToggle" -->
      <!-- 箭头用 Party.Toggle.ExpandIndicator 的 Expanded/Collapsed 样式 -->
      <!-- 标题：HeaderText；右侧：TotalCount -->
      <ListPanel DataSource="{Troops}"
                 IsVisible="@IsExpanded"
                 StackLayout.LayoutMethod="VerticalTopToBottom">
        <ItemTemplate>
          <!-- 原版 Party/Member 只读兵种行：头像 + 名称 + 数量 -->
        </ItemTemplate>
      </ListPanel>
    </ListPanel>
  </ItemTemplate>
</ListPanel>
```

说明：这里省略了 `Children`、尺寸、数据源路径与具体 StateChanger。Codex 必须依据仓库可编译的现有 XML 习惯补全，而非照贴示意结构。

### 5.3 原版兵种行严格要求

**目标**：和用户截图中的 Party → Member 一样，是整行横向展示，而不是之前效果图里重新排列的小卡片。

```text
[兵种头像/兵种小图标]    瓦兰迪亚方旗骑士                       8
[兵种头像/兵种小图标]    瓦兰迪亚步卒                          23
```

- 不增加左侧“转移”箭头（用户明确要求去掉）。
- 不显示 `100%` 血条；不显示加减号、可选中圆点或其他 Party 交换控件。
- 不修改头像、名称、数量的相对布局和主要 Brush 风格；仅根据实际弹窗宽度做必要的容器尺寸适配。
- 原版 `PartyTroopTuple` 带逻辑，**不能整块直接插入**。仅复制必要的视觉节点 / Brush / 标签。`Command.AlternateClick` 到百科不应与折叠标题的 `Command.Click` 冲突。

## 6. 保留原有操作：非常重要

目前 `FiefManagementPresentationContext.Commit` 和 `FiefPartyManager.ReturnSelectedFiefTroops` 已经实现**玩家当前部队**封建兵的选中返还；该调用按照选中的兵种数量，再按已有批次顺序返回，并非“指定 BatchId 返还”。

- 在役“全封邑批次列表”应定义为只读统计，展示所有相关 LordParty；**不能把其他 LordParty 的服役批次误当成玩家可选择返还的数量**。
- 如果原页面切到在役就显示只读组，需保留一个明确、可操作的“返还当前部队封建兵”入口，以便继续调用现有 `OpenTroopSelection / Commit / ReturnSelectedFiefTroops` 的功能；不能把原功能删掉，也不能静默失效。
- 一阶段**不新增按 BatchId 精确指定返还**的命令，更不修改 `FiefServiceBatch` / `FiefReturnBatch` 的存档结构。
- 待役原有征募操作保持原样，`Commit` 的权限与旧的来源指纹校验保持原样。
- 返回、Esc、关闭按钮行为不应导致招募 / 返还提交；原版已存在的重置、取消、完成语义须测试。

## 7. 过滤、统计与更新要求

- 左侧“全部附属村庄”表示此封邑所有有效从属村庄，具体村庄仅展示该来源村庄对应的真实批次。
- 顶部在役 / 待役 / 返乡人数必须维持现有业务统计口径；显示组数及组内人数需要和当前筛选后的查询一致。
- 在役 `TotalCount` = 每个 **实际 `FiefServiceBatch`** 的兵力之和；返乡展示层同 `WaitWeeks` 合组后人数相加；必须能区分同兵种不同批次。
- 注意当前 `_context.GetCount()` 通过合并后的 roster 计算，而组数据来自实际批次：若出现数据异常或资格过滤差异，先定位原因，不要通过 UI 人工补数量，也不要删保存记录。可在调试日志中输出各页计数来源差异。
- 屏幕打开时构建只读快照；页签/村庄切换时重建相应组。现有每日/每周生命周期业务不可由 UI 触发；必要时刷新按钮只重新查询。
- 当 `ServingPartyId` 已不能解析时，显示占位文本或诊断提示，不让窗口崩溃；不要将“找不到部队”直接认定为已阵亡。
- 长部队名/长中文兵种名应截断或自适应，不挤压最右侧数量；scrollbar、窗口边距保持原有风格。

## 8. Codex 推荐的分阶段改动与文件安排

### 阶段 A：只读查询与 VM

1. 阅读原版 `PartyScreen.xml` 中 `PartyHeaderToggleWidget`、`PartyListPanel` 及 PartyTroopTuple 的绑定。
2. 在 `Fief/Presentation` 新增只读查询/数据映射类，暴露当前筛选条件下的在役/返乡批次快照。
3. 新增 `FiefManagementBatchGroupVM.cs`、`FiefManagementTroopRowVM.cs`；纯内存，不追加 save keys / Saveable ID。
4. 扩展 `FiefManagementTroopSelectionVMMixin.cs`：`Groups`、Ready/Batch 显示条件、`RefreshBatchGroups`、折叠状态。

### 阶段 B：XML 视图

5. 编辑 `FiefManagementTroopSelection.xml`：待役 GridWidget 保持；在役/返乡走新列表，复用本体折叠 Brush 与 Party 兵种条目外观。
6. 复用 `FiefManagementTroopSelectionPrefabExtension.cs` 的注入，不更改原版 UI 文件。
7. 如果新增 `Fief/Presentation` 下 XML/CS，校验项目构建项和运行时复制路径（按现有 `.csproj` / 包结构处理）。

### 阶段 C：原功能回归与收尾

8. 保留当前玩家部队的返还入口并实际操作测试；待役招募行为不变。
9. 中英文本使用项目现有 `TextObject` / 本地化路径，中文示例并非硬编码数据；处理空数据、异常长名称与资源缺失。
10. 编译，并在实机完成下列验收。日志应仅保留必要的诊断，不持续刷屏。

## 9. 游戏内验收清单

- [ ] 游戏内打开封建兵管理，外框/菜单/页签/底部原生风格不变。
- [ ] **待役**：原先选择兵种、征募、最大征募人数、取消/完成完全正常。
- [ ] **在役**：同一 LordParty 的不同 BatchId 分别有独立标题；标题显示 LordParty 部队名、已服役、免薪剩余或正常计薪、该批次人数。
- [ ] **返乡**：不同村庄中相同 `WaitWeeks` 的兵种合并显示于同一个剩余周数组；不同周数组严格独立。
- [ ] 点击一个组的标题，仅收起/展开该组；标题不消失，组内行完全隐藏且不留空白。
- [ ] 不显示血条、不显示兵种转移箭头/加减号；兵种行只包含原版风格的兵牌、名称、数量。
- [ ] 默认全部展开；切村庄/切页签的折叠状态不异常重置（按 GroupKey 决定）。
- [ ] 全部附属村庄与单个村庄筛选正确；页签人数与批次人数一致，无跨来源重复计数。
- [ ] 玩家当前部队封建兵返还仍有操作入口且可成功执行；其他 LordParty 的只读批次不可被误返还。
- [ ] 周推进后返乡 `WaitWeeks` 正确变化；日推进后服役天数变化，但 UI 不导致重复推进。
- [ ] 军团灭亡/Party 引用丢失、空批次、空列表时不闪退、不误删记录。
- [ ] 退出、Esc、再次打开、读旧存档后界面正常；没有新增存档字段。
- [ ] 通过 `dotnet build` / 项目既有编译脚本验证可构建（使用仓库实际目标环境）。

## 10. 明确禁止的做法

1. ❌ 把 `GetFiefManagementRoster()` 的合并兵种结果当成真实批次。
2. ❌ 仅按 LordParty 合并掉多个不同 BatchId。
3. ❌ 把 `FreeWageDays - ServiceDays` 称为“剩余服役”。
4. ❌ 把 `WaitWeeks * 7` 显示成精确“返乡剩余天数”。
5. ❌ 直接用带转移/升级逻辑的整个 `PartyTroopTupleButtonWidget`。
6. ❌ 为展示而更改 `FiefServiceBatch` / `FiefReturnBatch` / `SaveDefiner` 的存档结构。
7. ❌ 丢失当前“玩家部队在役士兵返还”操作。
8. ❌ 修改 `code/GUI/...` 原版文件或创建新美术资源。
9. ❌ 显示游戏目前并不存在的精确到期或血量信息。

## 11. 给 Codex 的执行指令（可直接复制）

> 请以仓库 `GuihuaZhou/bannerlord` 当前分支为准，先检查本任务文档第 2 节所列源码。完成《封建兵管理》在役/返乡批次折叠 UI 改造：在役按 `LordParty + BatchId` 作为独立折叠分组，显示部队、已服役、剩余免薪/正常计薪和人数；返乡按 `WaitWeeks` 展示层合并成可折叠分组。兵种行严格复用原版 `Party/Member` 的头像/兵种名/数量布局，不显示血条、转移箭头、升级/加减控件。保留现有窗口、村庄筛选、三个页签和待役可招募逻辑，必须保留玩家本人部队的封建兵返还入口。不要改变任何存档模型或服务逻辑。实现时优先 `ButtonWidget` + 原版 `Party.Toggle.*` Brush + `IsExpanded`，先做只读组 VM，再接 XML，之后编译并验证本方案第 9 节验收项，报告改动文件与剩余风险。

---

**状态**：设计与施工指导文档。仓库代码尚未因本文件而自动修改或进行游戏内验证。  
**说明**：原版 XML 的具体绑定、Brush 可用性均以 Codex 在当前游戏版本实测及项目编译结果为准。
