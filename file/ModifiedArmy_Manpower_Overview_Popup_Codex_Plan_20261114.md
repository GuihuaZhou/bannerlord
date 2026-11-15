# ModifiedArmy「查看兵源」原生风格弹窗改造——Codex 施工方案 V1.0

> 日期：2026-11-14  
> 仓库：`GuihuaZhou/bannerlord`，默认分支 `main`  
> 状态：设计与施工任务书，**尚未修改仓库，也未在游戏内渲染**。实际开发以 Codex 开始时的 Git HEAD 和 DLL 签名为准。  
> 视觉目标：用户确认的**第二张效果图**：窄型深色原版风格框、竖向栏目、棕色标题条、金色标题文字、左标签右数值、细横线、滚动区、下方完成按钮。图片属于效果示意，不能作为可运行控件的证据。

## 0. 目标与边界

**目标：** 城镇/城堡左侧菜单「查看兵源」点击后，不再跳转到文字子菜单，而是在当前原版菜单上方打开一个只读 Gauntlet 弹窗。显示职业兵、封邑兵、附属村庄供养三组信息，支持后续增加新栏目/字段，保持原版 UI 风格。

**严格不做：**

- 不改人口、Hearth、RecruitmentBurden、职业兵训练、招募候选、驻军补员、封邑兵补员、封邑兵生命周期、原版招募逻辑、军事制度/兵营/文化规则、AI 决策或财政模型。
- 不增加 Saveable 字段/编号、SyncData 键、每日结算、长期业务缓存、额外 CampaignBehavior 或第二套兵源账本。
- 不修改仓库 `code/` 下本体参考源码，不复制任何本体贴图，不用未经确认的“剑/城堡/麦穗”图标或自制现代风格卡片。
- 不用 `MBInformationManager.ShowMultiSelectionInquiry` 伪装多字段详情页，也不动已有「职业兵招募偏好」编辑入口。
- 不把视觉样例数字（1746、42/60、4.00 等）硬编码为显示值；数据必须来自游戏实时查询。

## 1. 当前真实代码基线（已从仓库 main 检查）

### 1.1 菜单入口

路径：`ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs`

- 城镇入口：`town_keep`；城堡入口：`castle`。
- 原选项 ID：`modified_army_view_reinforcement_rate`；语言键：`ModifiedArmy_ReinforcementRate_Entry`（中文「查看兵源」）。**保留两者**。
- 现有 consequence 为 `GameMenu.SwitchToMenu(REINFORCEMENT_RATE_MENU_ID)`；子菜单 ID：`modified_army_reinforcement_rate_menu`。
- 现有 `InitializeReinforcementRateMenu()` 从已有 Forecast/Population/Fief 服务获取数据并格式化文字；改造时**抽取既有取数逻辑，不重写公式**。
- 当前用 `fiefCondition` 设置 `GameMenuOption.LeaveType.Submenu`。独立弹窗入口应采用 `Manage`（参照同项目 `SettlementProfessionalPreferenceMenuBehavior.CanOpen`），不要继续标成 `Submenu`。

### 1.2 可复用的数据与语义

| 项目 | 已有接口/属性 | 约束 |
| --- | --- | --- |
| 预测快照 | `SettlementManpowerProductionBehavior.GetProductionForecast(settlement)` | 既有纯预测查询；不得调用 `EnsureDailyProduction()` 来“刷新” |
| 已完成的日结算 | `pools.GetProductionData(settlement)` | 读取 `HasProfessionalRecord`/`HasFiefRecord` 区分无记录与 0 人 |
| 职业可用兵源 | `SettlementPopulationServiceRegistry.Current.GetRecruitmentCapacity(settlement)` | 非负 `floor` 显示人数，**没有 `/天`**；服务缺失显示暂不可用 |
| 基础训练 | `forecast.ProfessionalTrainingBase` | `0.00 /天` |
| 兵营加成 | `forecast.ProfessionalTrainingStep * forecast.BarracksLevel` | `BarracksLevel` 是界面当前既有显示含义；`AllowedRecruitmentLevel` 为兵种开放资格，不可混为同一概念 |
| 文化效率 | `forecast.CultureEfficiency` | 转为百分比 |
| 额外加成 | `forecast.ExternalTrainingBonusRate` | 转为百分比，不重复加成、不全部归因于官职 |
| 训练能力 | `forecast.ProfessionalTrainingCapacity` | 仅理论能力，不是实际入伍人数 |
| 职业预计补员 | `forecast.ProfessionalAllocation` | 小数/天，可能因实际招募规则未兑现 |
| 职业实际补员 | `productionData.HasProfessionalRecord` + `LastProfessionalRecruited` + `LastProfessionalRecordedDay` | 最近一次**已完成**结算，整数人数 |
| 自动招募 | `forecast.ProfessionalRecruitmentEnabled` | 按既有语义显示开启/关闭 |
| 封邑基础补员 | `forecast.FiefBaseDailyRate` | 小数/天 |
| 封邑预计补员 | `forecast.FiefAllocation` | 小数/天 |
| 封邑实际补员 | `HasFiefRecord` + `LastFiefReplenished` + `LastFiefRecordedDay` | 最近已完成结算，整数人数 |
| 当前封邑兵 | `forecast.FiefCurrent` | 取自 `FiefPartyData.GetTotalTroopCount()`；包含**就绪、在役、冷却/返乡**等状态，不能称作可立即征召兵力 |
| 村庄供养 | `FiefPartyManager.TryGetReadyFiefData` + `FiefPartyData.Villages` + `VillageFiefData.GetFeudalBudgetHearth(doctrine)` / `GetOccupiedSupplyHearth(template,doctrine)` | 逐村按定居点 `StringId` 稳定排序；缺单村数据只标记该村不可用 |
| 全域供养剩余 | `fiefData.GetTotalFeudalBudgetRemainingHearth()` | 实现逐村 `max(0, budget-occupied)` 后求和，不能用总上限减总占用替代 |

**特别约束：效果图的「42 / 60」只是样例。** 当前 `RecruitmentProductionForecast` 已确认有 `FiefCurrent`，但没有确认可匹配的**封邑编制总人数**字段。Codex 如果找不到真实编制人数的纯只读查询，本期只显示「当前封邑兵：N 人」，**不要**凭空造分母，更不能把人口供养预算当成人数上限。

对应代码：

- `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs`
- `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/SettlementManpowerProductionData.cs`
- `ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs`
- `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs`

### 1.3 已核实的原版 Widget/Brush/Sprite（禁止创造不存在的名字）

| 功能 | 真实本体资源/组件 | 参考源码 |
| --- | --- | --- |
| 原版深色石框 | `Recruitment.Frame`（Sprite `StdAssets\standart_popup` + `Frame1_frame`） | `code/GUI/SandBox/GUI/Brushes/Recruitment.xml`；本 Mod `Fief/Presentation/FiefManagementTroopSelection.xml` 也在使用 |
| 标题装饰/文字 | `StdAssets\tabbar_popup`、`Recruitment.Popup.Title.Text` | `code/GUI/SandBox/GUI/Prefabs/Recruitment/RecruitmentPopup.xml` |
| 栏目棕色标题条 | `Clan.Management.FamilyCollapser`，其底图为 `Clan\flat_header` | `code/GUI/SandBox/GUI/Brushes/Clan.xml`；`code/GUI/SandBox/GUI/Prefabs/Clan/Management/ClanManagement.xml` |
| 栏目金色文字 | `Clan.Management.RightCollapser.Text` 或经游戏对照后确认的相近原版文字 Brush | `code/GUI/SandBox/GUI/Brushes/Clan.xml` |
| 行与行间细横线 | `Widget Sprite="BlankWhiteSquare_9" SuggestedHeight="1" AlphaFactor="0.3"` | `code/GUI/SandBox/GUI/Prefabs/Map/MarriageOfferPopup.xml` |
| 折叠交互 | `ToggleButtonWidget` + `WidgetToClose`，也可由 `ButtonWidget` 命令控制 VM `IsExpanded` | `code/GUI/SandBox/GUI/Prefabs/Clan/Management/ClanManagement.xml` |
| 关闭叉号 | `Popup.CloseButton` | 本 Mod `ModifiedArmy/ModifiedArmy/Fief/Presentation/FiefManagementTroopSelection.xml` |
| 完成按钮 | `Standard.PopupCloseButton`（需绑定参数），或本 Mod 已用且已核实的原版按钮组合 | `code/GUI/SandBox/GUI/Prefabs/Clan/ClanCardSelectionPopup.xml`；`TownManagement/TownManagement.xml` |
| 滚动 | `ScrollablePanel`、`ScrollbarWidget`、`ClipRect`、`InnerPanel` | `code/GUI/SandBox/GUI/Prefabs/Recruitment/RecruitmentPopup.xml`；本 Mod `FiefManagementTroopSelection.xml` |
| 独立窗口生命周期参考 | `GauntletLayer` + `LoadMovie` + `ReleaseMovie` + `OnPopScreen` | `ModifiedFamily/ModifiedFamily/UI/FamilyManagementScreenService.cs`（只参考，不建立 ModifiedArmy→ModifiedFamily 依赖） |

**关于用户上传的「职业兵招募偏好」截图：** 其源代码通过 `MBInformationManager.ShowMultiSelectionInquiry` 调用原生选择弹窗。仓库 `code/` 中目前核实到的是调用方，而非可自由承载任意统计布局的原生 Inquiry Prefab。新窗口应优先采用已验证的 `Recruitment.Frame` 做相近的**窄型**外框；若 Codex 在开发机的本体 UI 资源里找到了更准确的公开 Brush，必须先游戏验证后再替换。**不要承诺视觉像素级等同于 MultiSelectionInquiry。**

## 2. 视觉规格：用户选择的第二张方案

1. 左侧原城镇/城堡菜单保留并变暗；中央偏右一个紧凑型深色弹窗。无需左右两大统计栏、无现代卡片。
2. 顶部固定标题「兵源状况」，下方显示真实定居点名（例如萨哥特），右上角真实 `Popup.CloseButton`。
3. **中间统一滚动区**，从上到下三大栏目：**职业兵 → 封邑兵 → 村庄供养**。全部默认展开；点击棕色栏目标题可折叠，用户再次打开弹窗默认展开（不保存折叠状态）。
4. 棕色表头使用 `Clan.Management.FamilyCollapser` 风格。标题可无图标；如加剑/城堡/麦穗图标必须先验证 Sprite 实际存在和已加载，图标失败不能导致界面失败。
5. 每组是「标签左对齐，值右对齐」两列**行布局**，行间为 1px 30% 透明细横线，**不是** `HTML table`、全边框表格或自定义 `TableWidget`。
6. 村庄供养也采用窄窗口友好的两列：`村庄名` / `占用 / 上限`。如果需要剩余供养，可用鼠标提示显示；**不**硬塞四列导致长中文村庄名与数值重叠。
7. 顶部、底部固定，中部纵向滚动。条目几十个仍不改变外框尺寸；滚动条仅在必要时出现；鼠标滚轮与键盘输入不能穿透到地图或下层菜单。
8. 底部固定原版风格「完成」按钮。关闭方式至少为右上角 X 和底部「完成」；Esc 如果在目标 Gauntlet 上有可靠原版输入方案，应实现并实机验证，**不能假定注册 HotKeyCategory 后 Esc 就自动关闭**。
9. 建议首版内部尺寸以较窄的 600～800 设计单位、相对较高的固定高度试排，具体宽高以 Native 缩放、标题完整显示、长村庄名、最小 UI 缩放的游戏内验证结果为准；不要把样机的实际屏幕像素硬编码为 XML 单位。

### 首版完整展示字段（不因样机省略而丢失原有功能）

**职业兵**：可用兵源；基础训练；兵营加成（N级）；文化效率；其他加成；训练能力；预计补员；实际补员（上次结算）；自动招募。

**封邑兵**：当前封邑兵总数；基础补员；预计补员；实际补员（上次结算）。如现有编制模型中存在严格对应、无副作用的人员编制上限，可显示为 `N / 上限`；否则只显示 `N 人`。

**村庄供养**：按村显示名称与 `占用 / 上限`；多村滚动。可以在说明/tooltip 中解释供养不是人数，而是供养 Hearth 额度；不得把占用直接称为“已征召人数”。

**记录语义**：未结算 `暂无记录`；已结算 0 是 `0 人`；历史真实整数不要显示成 `/天`。展示理论训练/预计补员时应明确二者不保证实际入伍，且不虚构“拒招原因”。

## 3. 推荐项目组织

```text
ModifiedArmy/
├── GUI/Prefabs/
│   └── ModifiedArmyManpowerOverview.xml          [新：Gauntlet Prefab]
└── ModifiedArmy/
    ├── Fief/Models/FiefMenuBehavior.cs           [改：现有入口接弹窗]
    ├── Recruitment/Presentation/
    │   ├── ManpowerOverviewScreenService.cs     [新：创建/销毁/输入/焦点]
    │   ├── ManpowerOverviewVM.cs                [新：顶层 VM]
    │   ├── ManpowerSectionVM.cs                 [新：栏目和展开状态]
    │   ├── ManpowerRowVM.cs                     [新：一行标签和值]
    │   └── ManpowerOverviewDataProvider.cs      [新：只读数据适配与格式化]
    ├── ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml  [改]
    ├── ModuleData/submodule_strings.xml                      [按现有本地化约定修改]
    └── ModifiedArmy.csproj                                  [改：资源部署/缺失程序集]
```

* 新的 `Presentation` 类不是新的 CampaignBehavior；`FiefMenuBehavior` 已在 `Main.cs` 注册，不需要重新注册。
* `GUI/Prefabs` 参考项目内 `ModifiedFamily/GUI/Prefabs` 布局。但必须验证 **ModifiedArmy 的实际安装结构**：XML 最终存在于 `Modules/ModifiedArmy/GUI/Prefabs/`，被引擎识别和加载；不要只放在 DLL 输出目录。
* `ModifiedArmy/ModifiedArmy/ModifiedArmy.csproj` 当前有 `ModuleData` / `FiefManagementTroopSelection.xml` 的复制规则，但不包含新 `GUI/Prefabs` 的显式复制。可参考 `ModifiedFamily/ModifiedFamily/ModifiedFamily.csproj` 的 `<Content Include="..\GUI\Prefabs\..." Link="GUI\Prefabs\...">`，同时验证部署脚本是否还需额外复制。
* 如果新增 `GauntletLayer` 使用了当前 csproj 未引用的 `TaleWorlds.GauntletUI` 或 `TaleWorlds.TwoDimension`，先核实开发机 DLL，再按最小范围补引用；禁止莫名添加对 ModifiedFamily 的依赖。

## 4. 入口与生命周期实现

### 4.1 修改 FiefMenuBehavior

将 `town_keep`、`castle` 两处 `REINFORCEMENT_RATE_OPTION_ID` consequence 从 `GameMenu.SwitchToMenu(...)` 改成**同一个** `ManpowerOverviewScreenService.Open(Settlement.CurrentSettlement)`。入口条件与 UI 权限逻辑独立于「职业兵招募偏好」：沿用此前**可以查看他人定居点**的现有读取条件，不因为新窗口而加 `OwnerClan == PlayerClan` 限制。

示意（非可直接粘贴的完整方法）：

```csharp
// 仅示意入口差异；Codex 按当前代码签名、菜单行为实现。
private static bool ConfigureManpowerOverviewOption(MenuCallbackArgs args)
{
    bool canView = Settlement.CurrentSettlement?.IsFortification == true;
    args.IsEnabled = canView;
    if (canView) args.optionLeaveType = GameMenuOption.LeaveType.Manage;
    return canView;
}

private static void OpenManpowerOverview(MenuCallbackArgs args)
{
    ManpowerOverviewScreenService.Open(Settlement.CurrentSettlement);
}
```

删除/停用旧 `REINFORCEMENT_RATE_MENU_ID` 常规跳转逻辑和旧 `LEAVE_REINFORCEMENT_RATE_OPTION_ID`；但先检查**旧存档恰停留在该子菜单**的兼容行为。如果引擎恢复时需要注册旧菜单 ID，可以保留一个仅用于恢复的兼容路径，不应再让正常入口进入旧菜单，也不应残留两套可见「查看兵源」。相关文本格式化辅助函数移交 DataProvider 后才能移除。

### 4.2 ManpowerOverviewScreenService

参考本仓库 `ModifiedFamily/ModifiedFamily/UI/FamilyManagementScreenService.cs`，独立实现：

- `Open(Settlement)` 检查参数、`ScreenManager.TopScreen`、弹窗当前是否打开；防重复打开和多层叠加。
- 在构建窗口前从真实数据组装**一次只读快照**；构造 VM 后创建 `GauntletLayer`、设置 `IsFocusLayer`、输入限制、加载 `LoadMovie("ModifiedArmyManpowerOverview", vm)`。顺序与已在游戏使用的屏幕服务一致。
- **不要假设**所有 `ScreenManager` API、`GauntletLayer` 构造参数和 hotkey 行为可以仅凭样例照搬；以本项目编译所用 TaleWorlds DLL 和已有成功实现为准。
- `Close()` 要幂等：先清空 static 状态，解除屏幕事件，释放 Movie、移除 Layer、移交/释放焦点，调用 VM `OnFinalize()`。处理打开失败、关闭时屏幕变化、回到主菜单、加载存档与二次点击。
- 关闭只关闭弹窗，不能误把原城镇/城堡 GameMenu 退回地图。
- X/完成/可用的 Esc 路径统一转到 `Close()`。Esc 必须测试实际键盘焦点和父菜单是否被影响；没有实测不能标记 PASS。
- 打开时取数即可；默认不每帧轮询 Forecast，不在 VM 持有会改变 Campaign 状态的操作句柄。刷新需求未来单独立项。

## 5. ViewModel 与数据扩展契约

### 5.1 结构

使用本体 `ViewModel` 与 `MBBindingList<T>`，而不是三个独立硬编码的 XML 信息区：

```text
ManpowerOverviewVM
  Title / SettlementName / Description / DoneText
  Sections: MBBindingList<ManpowerSectionVM>
  ExecuteClose()

ManpowerSectionVM
  Title
  IsExpanded (默认 true，可变更并发送 PropertyChanged)
  Rows: MBBindingList<ManpowerRowVM>
  ExecuteToggle()

ManpowerRowVM
  LabelText / ValueText
  可选 HintText / HasHint
  可选 IsEmptyState / IsWarning（真实条件驱动）
```

将来新增“征兵压力”“军事制度”等栏目，在 DataProvider 中追加 `SectionVM` 即可，**无须改弹窗外层结构**；当前不增加这些未决领域的新字段。

### 5.2 读取与格式化

`ManpowerOverviewDataProvider` 的职责：调用已存在的接口，读取 Snapshot，转换为本地化的字符串/展示项；**绝不修改生产或招募业务状态**。

- 预计/训练/基础速率：沿用旧 `FormatRate(float)` 语义 `max(0,value).ToString("0.00")`，再拼本地化的 `/天`。
- 兵营加成：使用 `forecast.ProfessionalTrainingStep * forecast.BarracksLevel`，不根据 `AllowedRecruitmentLevel` 改训练速度；仍显示既有建筑等级语义。
- 百分比：沿用旧界面逻辑，对 `CultureEfficiency`、`ExternalTrainingBonusRate` 乘 100 后取整。
- 可用兵源：沿用旧 `CreateAvailableManpowerText` 的非负 floor 处理、服务不可用与 NaN/Infinity 防御。
- 实际补员：结合 `Has…Record` 和最近日结算数据；未记录显示 `暂无记录`，已记录 0 显示 `0 人`。
- 村庄：复用原 `CreateVillageSupplyText` 的 `BoundVillages`、`fiefData.Villages`、`MilitaryConfig` 和 `FiefPartyTemplate` 获取方式，但产出**一村一行** VM，而非整段换行字符串。
- 局部故障：职业兵的人口服务异常时只将该行标记暂不可用，其他项目仍呈现；封邑未就绪时其栏目/村庄区域显示暂不可用。错误可写既有 Logger，但防止频繁重复日志。
- 定居点名由 `settlement.Name` 读取；不要硬编码“萨哥特”，也不必列出王国/领主/城镇文化（原入口没有这些字段）。

### 5.3 UI 核对的风险点

1. `GetProductionForecast()` 文档标为只读；Codex 须沿调用链静态核对（尤其权重查询 `GetEffectiveWeights()` 是否触及可变状态），并以实际游戏对比打开前后的数据验证。不得调用 `EnsureDailyProduction()` 强行刷新。
2. `FiefCurrent` 包含多个服役状态。除非已有严格匹配的可征召人数模型，**不要把它改称“就绪士兵”**。
3. 封邑供养是 Hearth 额度，不是部队人数。村庄 `occupied/limit` 是供养额度，不能与 `FiefCurrent` 直接相减。
4. `ProfessionalRecruitmentEnabled == false` 时理论训练能力仍可能非零；必须依照原 Forecast 显示，而不是将训练能力一并伪改为零。

## 6. Gauntlet XML 结构（设计骨架，不是未编译的成品代码）

推荐在 `ModifiedArmy/GUI/Prefabs/ModifiedArmyManpowerOverview.xml` 组织：

```text
<Prefab> / <Window>
  Root Widget：整屏暗化层（原版 BlankWhiteSquare_9，原版颜色/透明度）
    Popup container：固定设计宽高 + 中心/偏右对齐
      原版 BrushWidget：Recruitment.Frame
      Title：StdAssets\tabbar_popup + Recruitment.Popup.Title.Text
      SettlementName：TextWidget
      X：ButtonWidget Brush=Popup.CloseButton Command.Click=ExecuteClose
      ContentViewport：固定上下边界的 ScrollablePanel
        ClipRect Widget (ClipContents=true)
          Sections ListPanel DataSource={Sections} 纵向排列
            ItemTemplate：
              SectionHeader ButtonWidget/ToggleButtonWidget
                Brush=Clan.Management.FamilyCollapser
                TextWidget Text=@Title
                Command.Click=ExecuteToggle（若 VM 控制状态）
              SectionRows ListPanel DataSource={Rows} IsVisible=@IsExpanded
                ItemTemplate：
                  横向 ListPanel：LabelText 左 / ValueText 右
                  横线 Widget：BlankWhiteSquare_9、height=1、AlphaFactor=0.3
        竖向 ScrollbarWidget（正确指向 InnerPanel/ClipRect）
      BottomFooter：固定位置，不属于 ScrollablePanel
        Standard.PopupCloseButton / 经验证的原版完成按钮
```

**布置原则：** `SectionHeader` 位于滚动区内；标题和底部按钮不会随内容滚动。动态的栏目项使用 `ItemTemplate`，不是复制三套完全一样的固定布局。`IsExpanded` 的 PropertyChanged/绑定、滚动区域的 `ClipRect` 与 `InnerPanel` 路径、Scrollbar 的 `Handle` 必须按真实 Gauntlet 语法配对，参考 repo 已运行成功的 XML。

针对窄窗口：标题文字不要沿用 `Clan.Management.RightCollapser.Text` 默认的大字号（源 Brush 默认字号偏大），通过已有 `Brush.FontSize` 属性在 Prefab 里缩小到适配的尺寸。`Clan.Management.FamilyCollapser` 是本体家族管理区的装饰，实际拉伸/边缘的效果必须在游戏里验证；如果失真，换本体真实存在且相似的横向标题 Brush，不要开发新纹理。

**千万不要**把效果图里的剑/堡垒/麦穗图标视为已确认素材。图标可以不做，不影响验收。

## 7. 本地化与文本管理

现有：`ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml` 中包括 `ModifiedArmy_ReinforcementRate_Entry`、`ModifiedArmy_ReinforcementRate_Description`、`ModifiedArmy_VillageSupply`、`ModifiedArmy_ReinforcementRate_NoRecord`、`ModifiedArmy_Common_Unavailable` 等。

- **保留 `Entry` ID**，新窗口标题/栏目/行标签用新唯一 ID；复用合法的现有字串避免重复。
- 新增的「兵源状况」「职业兵」「封邑兵」「村庄供养」「当前封邑兵」「最近一次结算」「完成」「暂无记录」「暂不可用」都应有中英文可用文本。按项目现有 `submodule_strings.xml` 与 CN XML 的对应机制实施，检查文本占位符一致。
- 原旧 `ReinforcementRate_Description` 如确认不再被引用可保留为未使用兼容词条，不必为了界面重构大量清洗汉化。
- `TextObject` 在 VM 绑定时转换为最终字符串；不要在 GUI XML 中复制未本地化的中文测试值。

## 8. 分阶段施工步骤

**P0. 仓库与原版核查**

1. `git status` / 记录起始 HEAD、当前修改、构建与部署规则。不得覆盖用户未提交修改。
2. 阅读当前 `FiefMenuBehavior`、Forecast/Data、`VillageFiefData`、人口服务、`FiefManagementTroopSelection.xml`。
3. 读取 `code/GUI` 中本文列出的所有真实 Prefab/Brush。验证目标版本 DLL 中 `GauntletLayer`、VM、按键、`Standard.PopupCloseButton` 调用签名以及 `Clan.Management.FamilyCollapser` 是否能被加载。
4. 核查既有问询框是否有**公开可复用**的外壳 Brush；如没有，使用 `Recruitment.Frame` 近似实现，不依赖私有类。

**P1. 只读数据层**

1. 把旧菜单的格式化/数据读取逻辑迁入 `ManpowerOverviewDataProvider`，数据来源保持一致。
2. 建立动态 section/row VM，三栏默认展开、空状态正确。
3. 对纯数据输出进行可测试的场景验证：关闭自动招募；职业训练仍非零；缺人口服务；村庄缺数据；实际补员记录为 0 与无记录。

**P2. Prefab 与屏幕生命周期**

1. 新建 XML，固定头/底，中央统一滚动区，栏目棕色表头、数据左/右对齐、细线。
2. 新建 ScreenService，LoadMovie/Close/FaultCleanup 完整，禁止重复 Overlay。
3. XML 添加到 csproj/部署流程，启动游戏验证 `LoadMovie()` 确实找到它；**只编译不渲染不算完成**。

**P3. 入口切换**

1. 在 `town_keep`/`castle` 现有 `REINFORCEMENT_RATE_OPTION_ID` 上替换 consequence。
2. 菜单条件改为适合弹窗的 `Manage`，保留外人领地的只读查看能力。
3. 确认旧菜单 ID 是否对旧存档菜单恢复有影响；只保留必要兼容，不允许正常点击回旧文字页。

**P4. 联调、本地化与收尾**

1. 中英文渲染、长村庄名、2/3/4+ 附属村庄、0 数据/空数据、UI 不同缩放。
2. 对比前后 Forecast、人口、兵源生产进度、仓库/驻军 roster、在役批次等在打开 UI 前后没有任何变化。
3. 不影响职业兵招募偏好 MultiSelectionInquiry、查看封邑、其他城镇菜单。
4. 编译完成且实际游戏内打开成功、滚动与关闭正常，才可标记 PASS。

## 9. 验收矩阵

| ID | 场景 | 必须结果 |
| --- | --- | --- |
| UI01 | 玩家城镇点击查看兵源 | 在原菜单上方出现窄型原版风格详情窗，不跳文字子菜单 |
| UI02 | 城堡点击查看兵源 | 与城镇同一 Prefab、不同实时数据 |
| UI03 | 非玩家所有的城镇/城堡 | 保留原先允许查看的只读能力，不开放编辑 |
| UI04 | 三栏目展开/折叠 | 初始展开；折叠时数据区收缩、其他栏目向上排列；再次展开正确 |
| UI05 | 更多数据行 / 多村庄 | 内容区滚动且不溢出框架，顶部标题和底部按钮固定 |
| UI06 | 长中文名称 / 游戏不同 UI 缩放 | 文字不越界、不遮挡数值，关闭按钮可点击 |
| UI07 | X/完成/已实现的 ESC | 干净关闭、不改变下层游戏菜单、不留下焦点或黑色遮罩 |
| UI08 | 重复打开/切换屏幕/打开失败 | 不叠层、无残余 Layer，异常能清理 |
| DATA01 | 职业可用兵源 1746 | 显示「1746」，不显示 `/天` |
| DATA02 | 人口服务缺失 / Forecast 为空 | 局部「暂不可用」，可以关闭，不 crash |
| DATA03 | 训练能力非零、自动招募关闭 | 训练能力仍为真实值，预计依 Forecast，自动招募为关闭 |
| DATA04 | 日结算尚未发生 | 实际补员显示「暂无记录」 |
| DATA05 | 最近结算 0 | 实际补员显示「0 人」，不回填上一次正数 |
| DATA06 | `FiefCurrent` 多状态 | 只标「当前封邑兵总数」，不冒充就绪可征召 |
| DATA07 | 村庄 A 占用超预算，村庄 B 空闲 | 各村占用真实显示，全域剩余用逐村非负值之和 |
| DATA08 | 界面打开多次 | 不触发每日生产、招募、Hearth 消耗、RecruitmentBurden 增长、进度变化 |
| COMP01 | 原职业兵偏好、查看封邑、旧存档 | 原有入口/存档能用；无 Saveable 结构变化 |
| RENDER01 | 资源/字体/边框/滚动条 | 游戏内可渲染；只依赖实际存在的原版 Brush/Sprite，缺图标可降级 |
| RENDER02 | 资源部署 | `Modules/ModifiedArmy/GUI/Prefabs/...` 实际存在，游戏成功载入 Prefab |
| LANG01 | 中英文 | 所有标签、单位、空状态和栏标题正确，无未解析 `{VARIABLE}` |

## 10. Codex 交付格式要求

最终提交报告应包括：

1. 起始/结束 HEAD、分支、工作区状态；本次新增与改动文件的清单和理由。
2. 真实使用的本体 `Widget` / `Brush` / `Sprite` 及对应源文件，说明最终为何采用该窗口外框（`Recruitment.Frame` 或有证据的其他原版 Brush）。
3. 旧菜单 → 新 Gauntlet 窗口的调用链和关闭/清理链、资源放置路径和构建复制链。
4. 数据字段 → 唯一业务来源映射；明确封邑兵总数语义、村庄剩余算法；确认没有新增业务公式或 Saveable 变更。
5. 构建日志、游戏截图（含窗体、折叠后、村庄多行、不同缩放）、实际运行结果和 9 节测试矩阵的 PASS/FAIL/BLOCKED 及证据。
6. 任何未能验证的 DLL API、纹理类别、ESC 输入、旧档恢复行为必须列为 `BLOCKED`，**不得使用“应该支持”“看起来没问题”假装已验收**。
7. 不直接操作本体 `code`，不额外修改 ModifiedPolitics、ModifiedFamily、AI、生产/财政/服役模块。确实需要超范围改动时先提出问题、影响和替代方案，不能顺手重构。

## 11. 可直接发送给 Codex 的任务说明

> 请在 `GuihuaZhou/bannerlord` 的 ModifiedArmy 上实施《ModifiedArmy「查看兵源」原生风格弹窗改造——Codex 施工方案 V1.0》。以当前仓库 HEAD 为基线，先检查实际代码、DLL 和 UI 资源，再按文档 P0→P4 完成开发。保留 `town_keep`/`castle` 的原「查看兵源」菜单按钮，但点击后打开只读 Gauntlet 弹窗，不再切到文字子菜单。界面采用用户确认的第二张效果图：紧凑、深色、原版石框，职业兵/封邑兵/村庄供养三个可折叠棕色表头，内容纵向滚动，左侧标签、右侧数值、1px 淡横线，固定关闭按钮。仅使用已核实的本体资源；图标非必需。数值全部来自现有 Forecast、人口服务及 Fief 供养模型，保留旧界面完整信息，绝不改变生产、存档和招募逻辑。完成后必须编译并进入游戏验证实际 Prefab 渲染、关闭焦点、动态滚动、长文本和数据对账；提交改动清单、运行截图、测试矩阵和存在的风险/阻塞。

## 12. 源码检索参考链接

- [FiefMenuBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/FiefMenuBehavior.cs)
- [RecruitmentProductionForecast.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/RecruitmentProductionForecast.cs)
- [SettlementManpowerProductionBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs)
- [SettlementManpowerProductionData.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Pools/Models/SettlementManpowerProductionData.cs)
- [VillageFiefData.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs)
- [FiefPartyData.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs)
- [PopulationContracts.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Population/Contracts/PopulationContracts.cs)
- [SettlementProfessionalPreferenceMenuBehavior.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/SettlementProfessionalPreferenceMenuBehavior.cs)
- [ModifiedArmy 本 Mod 原有 FiefManagementTroopSelection.xml](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Fief/Presentation/FiefManagementTroopSelection.xml)
- [本体 Recruitment Brush](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Brushes/Recruitment.xml)
- [本体 Clan Brush](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Brushes/Clan.xml)
- [本体 ClanManagement Prefab](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Prefabs/Clan/Management/ClanManagement.xml)
- [本体 MarriageOfferPopup 细横线](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Prefabs/Map/MarriageOfferPopup.xml)
- [ModifiedFamily 的模态 Gauntlet 生命周期参考](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/UI/FamilyManagementScreenService.cs)
