# Bannerlord Kingdom 官职系统 UI 实现方案

> 基于《骑马与砍杀 2：霸主》原版 Kingdom 界面的仓库代码及现有 `ModifiedPolitics` UI 扩展方式整理。本文记录**已确定的 UI 设计**与**推荐实现技术方案**；代码片段为实现思路示意，并非未经调整即可编译的完整补丁。

## 一、设计目标与最终 UI

在原版 Kingdom 页面增加第六个主 Tab：**官职**。不新建独立 Screen，也不增加“中央官职／地方官职”子 Tab 或文字类别标签。

- **左侧**：单列展示当前文化全部可用官职。每项仅显示图标、名称、`已任命人数/总席位数`；点击后高亮。
- **右侧**：随左侧选择刷新，统一按“官职名称、说明与席位 → 官职收益 → 任职情况 → 空缺与任命”从上到下展示。
- **任职条目**：显示 Hero 头像、姓名、Clan 旗帜、所属 Clan 小字；需要关联定居点的官职再显示相关定居点。每位任职者右侧设置“撤职”。
- **有人任职**：显示实际人选列表；满编时显示“当前无空缺席位”，任命按钮隐藏或置灰。
- **无人任职**：任职区域显示“当前暂无任职者”；底部显示空缺席位数量与“任命”按钮。
- **不同文化**：不存在的官职不加入左侧列表；官职数量、效果及任命规则由数据和业务层决定，不由 XML 分支硬编码。

左侧示例（数字为界面说明示意，不是各文化最终编制配置）：

```text
大元帅       1/1
首席大臣     0/1
宫廷总管     1/1
税务长官     2/2
农业长官     1/2
军事长官     1/1
治安长官     0/2
```

## 二、原版 UI 与代码依据

参考仓库：<https://github.com/GuihuaZhou/bannerlord>

已定位的主要文件：

```text
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/KingdomManagement.xml
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/Clan/ClansPanel.xml
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/Clan/ClanTuple.xml
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/Fiefs/FiefsPanel.xml
code/GUI/SandBox/GUI/Prefabs/KingdomManagement/Policies/PoliciesPanel.xml
code/KingdomManagementVM.cs
code/KingdomCategoryVM.cs
code/KingdomTabControlListPanel.cs
```

### 2.1 KingdomManagement 原版结构

原版有五个主 Tab：`Clan / Fiefs / Policies / Armies / Diplomacy`；各自的面板由 `KingdomManagement.xml` 挂载。内容面板普遍使用以下上下边距：

```xml
<ArmiesPanel DataSource="{Army}" MarginTop="188" MarginBottom="75" />
<ClansPanel DataSource="{Clan}" MarginTop="188" MarginBottom="75" />
<FiefsPanel DataSource="{Settlement}" MarginTop="188" MarginBottom="75" />
<PoliciesPanel DataSource="{Policy}" MarginTop="188" MarginBottom="75" />
<DiplomacyPanel DataSource="{Diplomacy}" MarginTop="188" MarginBottom="75" />
```

顶部 Header 的原版高度为 `196`。新增官职面板应保持相同布局位置：

```xml
<OfficesPanel
    Id="OfficesPanel"
    DataSource="{Office}"
    MarginTop="188"
    MarginBottom="75" />
```

- `FiefsPanel.xml`：适合参考**左侧列表 + 右侧详情 + 底部操作**的整体骨架。
- `ClansPanel.xml`：适合参考**选中左侧项目后刷新右侧详情**的状态管理、列表滚动及条目选择行为。
- `KingdomCategoryVM`：可参考其 `Show`、`IsAcceptableItemSelected` 等绑定属性。
- `KingdomTabControlListPanel`：原版仅维护五个按钮及对应 Panel 的选中状态。

### 2.2 已有 ModifiedPolitics 的扩展方式

仓库已有实现，可沿用其代码风格：

```text
ModifiedPolitics/ModifiedPolitics/KingdomClan/UI/ClanScrollableDetailsPrefabExtension.cs
ModifiedPolitics/ModifiedPolitics/KingdomFief/UI/KingdomSettlementFinancePrefabExtension.cs
ModifiedPolitics/ModifiedPolitics/KingdomFief/UI/KingdomFiefLayoutPatches.cs
```

现有技术包括 UIExtenderEx 的 `PrefabExtension`、`PrefabExtensionReplacePatch`、`PrefabExtensionSetAttributePatch`，通过独立 XML 扩展或替换指定控件。**尽量采用精确 XPath 局部修改，不复制维护整份原版 XML。**

## 三、四个常用前置 Mod 的职责

| 前置 | 本功能建议用途 |
|---|---|
| **Harmony** | 补充原版 Kingdom 中写死的五分类切换逻辑，支持第六个“官职”分类。 |
| **UIExtenderEx** | 注入 ViewModel Mixin 和命令；使用 Prefab Extension 插入官职按钮、官职面板与相关布局。 |
| **ButterLib** | 当前官职 UI 无强制用途；保留现有项目依赖，按需要使用日志、持久化辅助等能力。 |
| **MCM** | 当前官职 UI 无需使用；未来开放效果倍率、席位倍率、AI 任命频率等玩家设置时再接入。 |

不应仅因为项目有四个前置，就让官职 UI 强制依赖全部前置的 API。已有项目整体依赖维持不变。

相关项目（用于开发时对照所安装版本的 API）：

- Harmony：<https://github.com/BUTR/Bannerlord.Harmony>
- UIExtenderEx：<https://github.com/BUTR/Bannerlord.UIExtenderEx>
- ButterLib：<https://github.com/BUTR/Bannerlord.ButterLib>
- MCM：<https://github.com/Aragas/Bannerlord.MBOptionScreen>

## 四、推荐代码组织

```text
ModifiedPolitics/
└── ModifiedPolitics/
    └── KingdomOffice/
        ├── Campaign/
        │   ├── KingdomOfficeBehavior.cs
        │   ├── KingdomOfficeManager.cs
        │   └── KingdomOfficeSaveData.cs
        ├── Models/
        │   ├── OfficeDefinition.cs
        │   ├── OfficeInstance.cs
        │   └── OfficeEffectModel.cs
        └── UI/
            ├── KingdomOfficeVMMixin.cs
            ├── KingdomOfficeVM.cs
            ├── KingdomOfficeItemVM.cs
            ├── KingdomOfficeHolderVM.cs
            ├── OfficeEffectVM.cs
            ├── KingdomOfficeCategoryPatch.cs
            ├── KingdomOfficeTabPrefabExtension.cs
            ├── KingdomOfficePanelPrefabExtension.cs
            ├── OfficesPanel.xml
            └── OfficeTuple.xml
```

职责边界：

- `KingdomOfficeManager`：任职、席位、资格、文化可用性、关联定居点等真实 Campaign 数据。
- `OfficeDefinition`：职位静态定义（ID、名称、图标、编制、文化条件、效果、任命／撤职规则、是否需要定居点）。
- `OfficeInstance`：实际任职记录（Kingdom、Office ID、Hero、可选 Settlement、任职时间）。
- `KingdomOfficeVM` 及条目 VM：只负责向界面提供数据、选中状态和交互命令。
- `OfficeAppointmentService`（可另建）：统一执行不同文化任命路径，防止 UI 层直接写政治规则。

## 五、第六个 Tab：关键改动点

### 5.1 原版限制

`KingdomManagementVM` 的分类数量在原版构造函数里设置为：

```csharp
_categoryCount = 5;
```

`SetSelectedCategory(int index)` 只处理 `0–4`；原版五个 `Show` 状态分别是：

```text
0 = Clan
1 = Fiefs（Settlement）
2 = Policies（Policy）
3 = Armies（Army）
4 = Diplomacy
```

新增后：

```text
5 = Offices（Office）
```

只添加 XML 按钮而不修改分类逻辑，不能保证点击、手柄左右切换和激活状态正确。

### 5.2 Harmony 只扩展分类逻辑

需要确保：

1. 分类总数变成 `6`。
2. 切换任意分类时，五个原版面板和官职面板都正确关闭，再只显示目标面板。
3. `SelectPreviousCategory()` / `SelectNextCategory()` 正确覆盖索引 `5`。
4. 关闭页面、刷新 VM 和游戏手柄导航时，官职页不会残留选中状态。

示意（**非可直接编译的完整实现**）：

```csharp
[HarmonyPatch(typeof(KingdomManagementVM), "SetSelectedCategory")]
internal static class KingdomOfficeCategoryPatch
{
    static bool Prefix(KingdomManagementVM __instance, int index)
    {
        // 获取与 __instance 绑定的官职 Mixin / Office VM
        // 关闭原版五个页面和 Office 页面
        // 按 0~5 设置目标 Show 属性
        // 更新当前分类索引
        // 此处需处理原版私有字段与方法的实际访问
        return false;
    }
}
```

**注意**：完全跳过原版 `SetSelectedCategory()` 会把全部五个原版分支也变成本 Mod 的维护责任。实际编码前要比较 Harmony `Prefix/Postfix/Transpiler` 方案，优先最小化改动，避免原版升级后破坏已有 Tab。

### 5.3 用 UIExtenderEx Mixin 扩展 VM

新增并暴露：

```text
Office
OfficesText
ExecuteShowOffices
```

结构示意：

```csharp
[ViewModelMixin("RefreshValues")]
internal sealed class KingdomOfficeVMMixin
    : BaseViewModelMixin<KingdomManagementVM>
{
    [DataSourceProperty]
    public KingdomOfficeVM Office { get; }

    [DataSourceProperty]
    public string OfficesText => "官职";

    [DataSourceMethod]
    public void ExecuteShowOffices()
    {
        KingdomOfficeCategoryController.SetCategory(ViewModel, 5);
    }
}
```

Mixin 初始化、绑定刷新、属性通知及生命周期须按项目**实际引用的 UIExtenderEx 版本**补齐。

### 5.4 按钮 Brush 和控件边界

原版 tab 顺序是五项。增加“官职”后按钮边缘 Brush 建议调整为：

```text
Clan       Header.Tab.Left
Fiefs      Header.Tab.Center
Policies   Header.Tab.Center
Armies     Header.Tab.Center
Diplomacy  Header.Tab.Center
Offices    Header.Tab.Right
```

- 外交由右端 Brush 改成中间 Brush。
- 官职使用右端 Brush。
- 尽可能通过 Prefab Extension 更换按钮组中的必要元素，不改原版窗口其他布局。
- 官职 Button 使用 `Command.Click="ExecuteShowOffices"`，选中状态跟随 `Office.Show`。
- 原版 `KingdomTabControlListPanel` 内部已经维护五项，初版尽量不为第六项重写整个 Widget；但**若选中视觉或游戏手柄导航无法单靠绑定正确工作，再考虑专用扩展 Widget**。

## 六、官职页面 VM 结构

### 6.1 主页面 `KingdomOfficeVM`

```csharp
public sealed class KingdomOfficeVM : ViewModel
{
    public MBBindingList<KingdomOfficeItemVM> Offices { get; }
    public KingdomOfficeItemVM CurrentOffice { get; }
    public bool Show { get; set; }
    public bool IsAcceptableItemSelected { get; set; }

    public void SelectOffice(KingdomOfficeItemVM office);
    public void RefreshOfficeList();
    public void RefreshCurrentOffice();
}
```

主 VM 负责左侧官职列表、选中条目、右侧详情刷新及操作入口。正式实现需补 `DataSourceProperty` 与 `OnPropertyChangedWithValue` 等 Gauntlet 绑定通知。

### 6.2 左侧条目 `KingdomOfficeItemVM`

推荐字段：

```text
OfficeId
Name
Icon
OccupiedCount
SeatCount
IsSelected
OnSelect()
```

列表采用 `ScrollablePanel` + `NavigatableListPanel` + `OfficeTuple`。所有官职混排，不加中央／地方类别小字；仅对文化允许的官职生成条目。

### 6.3 右侧统一详情

推荐 VM 字段：

```text
Name
Description
SeatCount
OccupiedCount
VacantCount
Effects
Holders
HasVacancy
CanAppoint
```

右侧模块顺序：

```text
官职图标／名称／简介
席位数、已任命数、空缺数
────────────────
官职收益
────────────────
任职情况
────────────────
空缺提示／任命按钮
```

`Effects` 使用 `MBBindingList<OfficeEffectVM>`；具体效果数据由业务层提供，不要在 XML 中区分大元帅、首席大臣、税务长官。

### 6.4 任职者 `KingdomOfficeHolderVM`

```csharp
public class KingdomOfficeHolderVM : ViewModel
{
    public HeroVM Hero { get; }
    public string Name { get; }
    public BannerImageIdentifierVM ClanBanner { get; }
    public string ClanName { get; }
    public string AssignmentText { get; }
    public MBBindingList<OfficeEffectVM> Effects { get; }
    public bool CanDismiss { get; }

    public void ExecuteDismiss();
}
```

显示示例：

```text
伊拉
瓦诺尼亚氏族
[撤职]
```

关联定居点职位：

```text
奥罗斯
德瑟特氏族
管理定居点：帕拉汶
[撤职]
```

没有关联定居点时隐藏 `AssignmentText`。任职者在右侧依次纵向排列，Clan 信息比 Hero 姓名字号小。

## 七、有人任职与无人任职：同一 Prefab

两种状态不制作不同的页面。

- **有人任职**：渲染 `Holders` 人物行，包括头像、Clan、关联信息、撤职按钮；满员时显示“当前无空缺席位”。
- **无人任职**：展示空状态“当前暂无任职者”；底部显示空缺数量与“任命”。
- **部分空缺**：既展示已有任职者，也展示剩余空缺及任命按钮。

使用 `HasHolders / HasNoHolders / HasVacancy / CanAppoint` 等属性控制 `IsVisible / IsEnabled`，无需按官职类型切换 XML 模板。

## 八、OfficesPanel.xml 视觉实现

- **整体骨架**参考 `FiefsPanel.xml`：左栏、右侧详情、底部动作。
- **左侧选中与更新**参考 `ClansPanel.xml`：`CurrentSelectedXXX` 数据绑定，点选后刷新右侧。
- 复用原版组件和 Brush，减少美术成本：

```text
Kingdom.TitleMedium.Text
Kingdom.ParagraphSmall.Text
GradientDivider_9
ButtonBrush2
Kingdom.GeneralButtons.Text
Frame1Brush
Kingdom.Item.Tuple
```

主页面只用官职自身图标，不显示“中央官职”“地方官职”等额外文字。Hero 头像和 Clan Banner 复用原版 ViewModel/图像控件。

## 九、任命与撤职

### 9.1 普通任命

点击右侧 `[任命]`：

```text
任命 → 候选 Hero 列表 → 选择 → 确认 → OfficeManager 记录任职 → 刷新当前官职
```

第一版优先复用 `MBInformationManager.ShowMultiSelectionInquiry(...)` 和 `InquiryElement`。候选选项可包含 Hero 姓名、Clan、评分；候选内容过长时再制作独立的 `KingdomOfficeAppointmentPopup.xml`。

### 9.2 大元帅特殊任命

外层 UI 始终只调用：

```csharp
ExecuteAppoint();
```

业务层根据文化选择制度（以下是此前讨论的流程方向，最终以官职制度设计文档为准）：

```text
帝国、阿塞莱、库赛特 → 君主任命
瓦兰迪亚             → 贵族选举 / Kingdom Decision
斯特吉亚             → 提名 + 贵族确认 / Kingdom Decision
```

统一入口：

```csharp
OfficeAppointmentService.BeginAppointment(office);
```

**XML 不应判断文化**；调用 Kingdom Decision 的政治逻辑与 UI 解耦。

### 9.3 撤职

点击具体任职者的 `[撤职]`，业务层检查权限及补偿，并计算关系变化。第一版可复用 `InformationManager.ShowInquiry(...)` 或 `MBInformationManager.ShowMultiSelectionInquiry(...)`。

撤职补偿示意：不补偿、低额、中额、高额。具体金额与关系变化由既有制度设计确定，不在 UI 中硬编码。

## 十、Campaign 数据与刷新

`OfficeDefinition` 负责静态定义；`OfficeInstance` 负责实际任职；`KingdomOfficeSaveData` / `KingdomOfficeBehavior` 负责存档及事件监听；`KingdomOfficeManager` 作为统一查询和修改入口。

建议 API（示意）：

```text
GetAvailableOffices(Kingdom)
GetHolders(Office)
GetVacantCount(Office)
CanAppoint(...)
Appoint(...)
CanDismiss(...)
Dismiss(...)
```

刷新条件：任命、撤职、Hero 死亡、Clan 离开王国、关联 Settlement 归属变化、Kingdom 变化等。

UI 打开：

```text
RefreshValues → RefreshOfficeList → Select 默认官职 → RefreshCurrentOffice
```

任命／撤职后：

```text
修改 Campaign 数据 → RefreshOfficeList / RefreshCurrentOffice → PropertyChanged
```

不要每帧遍历全部官职；应根据事件或脏状态刷新。注意左侧席位数字和右侧详情需要**同步**更新。

## 十一、推荐开发阶段与验收

1. **第六个 Tab**：插入官职按钮和空 `OfficesPanel`；鼠标与手柄 Previous / Next Tab 均可切换；原五 Tab 正常。
2. **左侧列表**：使用测试数据生成若干官职；点击高亮并切换右侧选中对象。
3. **右侧详情**：完成官职简介、席位、收益、任职条目与空状态；至少验证“首席大臣有人任职 / 无人任职”两种界面。
4. **真实数据**：接入 `KingdomOfficeManager`，按 Kingdom Culture、席位、Hero、Clan、Settlement 展示。
5. **普通任命/撤职**：先复用 Inquiry；核实权限、存档、关系变动与 UI 刷新。
6. **大元帅特殊流程**：最后接入各文化任命制度及 Kingdom Decision。

## 十二、实现注意事项

- 不复制整份 `KingdomManagement.xml`，以精确 Prefab Patch 为主。
- 不修改原版 TaleWorlds DLL。
- 不将制度规则写入 VM 或 Gauntlet XML。
- 不为中央／地方官职单独做第二套页面。
- 不强行引入 MCM 或 ButterLib API。
- 初版优先使用原版 Inquiry，不急于制作独立 Popup。
- `KingdomTabControlListPanel` 原本只认识五项；新增按钮的选中状态、控制器导航、按钮宽度和布局需要在游戏内实际验证。
- Harmony 示例只是方向，特别需要确认私有字段 `_currentCategory`、`_categoryCount` 与 Mixin 的实际访问方式，以及 Patch 执行顺序。
- 已有其他 `ModifiedPolitics` UIExtenderEx Patch 时，应验证 XPath 命中和不同 Patch 的加载兼容性。
- 效果数值、席位数以及某些文化的具体任命路径以上述既定制度文档为准；本文重点记录 UI 技术架构，不重新定义政治机制。

## 十三、最终架构图

```text
KingdomManagement.xml
  └─ UIExtenderEx
      ├─ 第六主 Tab：官职
      └─ OfficesPanel.xml
          ├─ 左侧 Offices / OfficeTuple
          └─ 右侧 CurrentOffice
              ├─ 基本信息 / 席位
              ├─ Effects / OfficeEffectVM
              ├─ Holders / KingdomOfficeHolderVM
              └─ 空缺 / 任命按钮

KingdomManagementVM
  ├─ 原版五个 VM
  ├─ KingdomOfficeVMMixin → KingdomOfficeVM
  └─ Harmony → 5 个分类扩展为 6 个分类

KingdomOfficeVM
  └─ KingdomOfficeManager / AppointmentService
      ├─ OfficeDefinition
      ├─ OfficeInstance
      ├─ KingdomOfficeBehavior / SaveData
      └─ 必要时：KingdomDecision
```

**核心原则：** 使用原版 Kingdom 的 UI 结构、UIExtenderEx 增加视图和 VM、Harmony 最小化补充第六分类；官职数据及政治制度全部留在 Campaign 业务层。
