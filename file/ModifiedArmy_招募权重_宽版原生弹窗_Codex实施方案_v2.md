# ModifiedArmy「招募权重」宽版原生弹窗 — Codex 实施方案 V2

> **目标仓库**：`GuihuaZhou/bannerlord`（以 Codex 实际工作树为准）  
> **目标模块**：`ModifiedArmy`  
> **设计版本**：2026-11-14 / V2，取代此前使用 `Encyclopedia.Frame` 的实施任务书  
> **视觉基准**：交付包中的 `招募权重_宽版最终效果参考.png`；该 PNG 只用于视觉比对，**不是需要嵌入游戏的贴图**。  
> **状态**：仅为实施指引，未修改仓库代码，未进行编译或实机验证。

## 0. Codex 执行说明

请在本地仓库**实际实现**这一弹窗，替换现有 `MultiSelectionInquiry -> TextInquiry` 两次操作模式。先检查仓库状态、原版 UI XML/Brush、当前游戏 DLL 和运行目录，之后按本任务书施工。不要把本体 `code` 目录改掉，不新增自制 PNG/TPAC/SpriteAtlas，不借用 `ModifiedFamily` 的运行时类；只**参考**本体和 ModifiedFamily 的成功实现。

**最重要的设计变更：** 原实施方案以 `Encyclopedia.Frame`（百科框）为外框，**本版取消这个要求**。新外框应采用用户最开始截图中的**深棕色、细金属边框、右上圆形 X 的原版多选 Inquiry 弹窗质感**，但在画面上横向加宽。这不等同于保留 `MultiSelectionInquiry` 作为控件容器；必须使用新 Gauntlet Movie 来容纳滑块。原框准确资源归属应由 Codex 按第 3 节核实，不得以“看起来差不多”为由又替换成大百科石框。

先运行：

```bash
git status --short
git branch --show-current
git rev-parse HEAD
```

不要覆盖用户未提交的修改，未经要求不要 `push`、`merge` 或修改其它 Mod。

---

## 1. 最终交互与外观（不可擅自改回）

### 1.1 弹窗骨架

- 仅在当前玩家拥有的 **城镇/城堡菜单** 中打开，保留原菜单入口及权限判断。
- 弹窗标题：**「招募权重」**。菜单左侧的旧入口「职业兵招募偏好」此次可保留，不要求改菜单文案。
- 深棕色皮革/金属**原版 Inquiry 风格背景框**；形制参照用户提供的最初截图，外框比最初版本**明显加宽**。
- 标题顶部居中，右上原版 `Popup.CloseButton` 关闭键。
- **正常界面中没有**文字介绍、副标题、兵营等级、额外“权重”表头、工资、Hearth、生成比例、底部说明。
- 中央是滚动兵种列表；底部固定两个按钮：左 **「恢复默认」**、右 **「应用修改」**。
- 当条目超过视窗时滚动中部列表，顶部标题、关闭 X 和底部两个按钮始终固定可见。

### 1.2 每行必须同一水平线

单行从左到右，**所有核心控件垂直居中、同一排**：

```text
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ [兵牌/兵种类型图标]   瓦兰迪亚钩镰兵           0  ───●──────────────────  9    [2] │
│ [兵牌/兵种类型图标]   瓦兰迪亚步卒             0  ●─────────────────────  9    [0] │
│ [兵牌/兵种类型图标]   瓦兰迪亚硬弩兵           0  ───●──────────────────  9    [2] │
│ [兵牌/兵种类型图标]   瓦兰迪亚征召弩手         0  ●─────────────────────  9    [0] │
│ [兵牌/兵种类型图标]   城镇步兵                 0  ─────●────────────────  9    [3] │
└─────────────────────────────────────────────────────────────────────────────────────┘

                  [ 恢复默认 ]                          [ 应用修改 ]
```

- **保留每条滑块两端的 `0` 与 `9` 数字**：它们是最小值、最大值标签，不可删掉。
- 最右侧的独立数值框显示**当前权重**，范围为整数 `0…9`，并随拖动实时变化。
- 滑块不能放到兵种名称下一行；兵种名称与滑块必须水平对齐。
- 兵牌复用原版 `PartyTroopTuple` 的头像与兵种类型视觉；不得硬编码参考图里的头像或兵种名称。
- 每个兵种的滑块**完全独立**。改变一个兵种权重时，其他兵种的数值与滑块位置不发生变化（仅相对生产占比自然改变）。
- 效果图中五行名称和权重 `2 / 0 / 2 / 0 / 3` 为**示例状态**。真正行数与数据来自当前定居点的职业兵有效模板，不能写死五行或这些数字。

### 1.3 行为

- 每个权重只能为整数 **0—9（含两端），步长 1**；`0` 表示停止生成该类新的职业兵候选，不能被当成空值。
- **恢复默认**：把当前可见各兵种的**草稿**恢复为职业兵 XML 模板提供的默认权重；不立即写存档。
- **应用修改**：一次性提交整个合法兵种键集合；返回成功后关闭；失败显示反馈、保留或刷新当前界面，不得假装成功。
- 右上 X / Esc / 宿主界面被关闭：关闭并丢弃未保存草稿，不调用 `TrySetWeights`。若要为“未保存改动”额外加入确认，仅允许使用原版 Inquiry，不额外在主界面放常驻文字。
- 兵牌或兵种名称区域**右键**打开该兵种的原版百科；右侧滑块与数值区域不得被百科按钮覆盖，也不得触发兵员转移或升级。
- 右键打开百科如果会导致当前 GauntletLayer 被后台销毁，应先安全关闭弹窗再 `GoToLink`，并保证有未保存改动时不会悄悄丢弃（例如先弹出放弃更改确认）。

---

## 2. 核实过的代码和资源（开始编码前仍需对照本地版本）

| 路径 | 当前事实 / 用途 |
|---|---|
| `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/SettlementProfessionalPreferenceMenuBehavior.cs` | `town_keep`、`castle` 菜单入口；当前 `Open()` 用 `MBInformationManager.ShowMultiSelectionInquiry`，选中后 `InformationManager.ShowTextInquiry`；本次只替换显示调用路径。 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Behaviors/SettlementProfessionalPreferenceBehavior.cs` | `MaximumCustomWeight` 现在为 **20**；`Reconcile()` 负责同步有效键及默认值；`TrySetWeights()` 验证键、权重、所有权后写入记录，并调用 `FilterProfessionalOffers()`。 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Models/ProfessionalRecruitmentEligibilityModel.cs` | `Evaluate(settlement)` 提供 `Template`、`EligibleEntries` 等合法职业兵资格，已含建筑、文化资格判断；**不要重新实现资格筛选**。 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Models/SettlementProfessionalPreferenceRecord.cs` | `ActiveWeights` 是现有存档持久化结构；不增加新存档字段，不改存档键、Saveable Id。 |
| `ModifiedArmy/ModifiedArmy/ModuleData/professionalTroopTemplates.xml` | 当前模板默认权重处于新范围内；“恢复默认”取该模板对应兵种的 `TroopProductionEntry.Weight`，不是效果图数值。 |
| `ModifiedArmy/ModifiedArmy/Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` | 生产通过有效权重分配；提交后现有 `FilterProfessionalOffers()` 会移除已被禁用兵种的未消耗候选，避免重新创造第二套算法。 |
| `code/GUI/SandBox/GUI/Prefabs/Party/PartyTroopTuple.xml` | 带 `Party.TroopTupple.Right`、`Party.TroopBack`、`ImageIdentifierWidget`、`Party.Text.Tuple`，右键百科命令。完整 `PartyTroopTupleButtonWidget` 还包含部队转移等逻辑，**不能直接照搬整个控件**。 |
| `code/GUI/SandBox/GUI/Brushes/PartyScreen.xml` | 已确认 `Party.TroopTupple.Right`（注意官方资源本身拼写为 **Tupple**）、`Party.TroopBack`。 |
| `code/GUI/SandBox/GUI/Prefabs/Clan/ClanPartiesRightPanel.xml` | 原版工资上限：`SliderWidget`，`IsDiscrete="true"`，`Filler="Filler"`、`Handle="SliderHandle"`。 |
| `code/GUI/SandBox/GUI/Brushes/Clan.xml` | 已确认 `SPClan.Slider.Canvas`、`.Fill`、`.Frame`、`.Handle`。 |
| `code/GUI/SandBox/GUI/Prefabs/Recruitment/RecruitmentPopup.xml` | 原版可用的弹窗结构，`Brush="Recruitment.Frame"`、原版关闭键、滚动列表等；**可作候选参考，不可假定就是初始 Inquiry 的同一外框**。 |
| `code/GUI/SandBox/GUI/Brushes/Recruitment.xml` | 已确认 `Recruitment.Frame` 的 Default 层使用 `StdAssets\standart_popup`，Frame 层使用 `Frame1_frame`，是可验证的原版弹窗背景组合。 |
| `code/RecruitVolunteerTroopVM.cs` | `CharacterCode`、`CharacterImageIdentifierVM`、`CampaignUIHelper.GetCharacterTypeData/GetCharacterTierData` 示例；`ExecuteOpenEncyclopedia` 使用 `Campaign.Current.EncyclopediaManager.GoToLink(Character.EncyclopediaLink)`。 |
| `ModifiedFamily/ModifiedFamily/UI/FamilyActionScreenHost.cs` | 已有独立 `GauntletLayer` / `LoadMovie` / `ReleaseMovie` / `OnPopScreen` / 输入焦点维护范例；**只参考，不依赖 ModifiedFamily**。 |

特别声明：仓库中已确认的是上述 Brush/Prefab，**尚未验证**最初 `MultiSelectionInquiry` 弹窗的准确 Brush 标识。Codex 需在当前游戏本体 Native/SandBox 实际 GUI 资源中定位 `MBInformationManager.ShowMultiSelectionInquiry` 对应 Movie/外框，优先复用其同一背景；如果无法从仓库直接定位，须在 `Recruitment.Frame`（已核实）和其它原版 `StdAssets\standart_popup` 组合中找最接近截图的方案，并附实机对比及选择理由。**不要退回百科框 `Encyclopedia.Frame`**。

---

## 3. 背景框识别与宽度适配：必须先做的验证

1. 保存最初旧版弹窗截图；在可检索的本体 `code/GUI`、游戏 `Modules/Native/GUI` 或现有 SandBox/Native UI Prefab 与 Brushes 中查找 `Inquiry` / `MultiSelectionInquiry` / `InquiryPopup` 类外框实现。注意该 UI 可能不在当前仓库收录的 SandBox Prefab 中。
2. 确定 Popup 外边框实际由哪个 `Brush` / `Sprite` / 底图组成。若底图不支持九宫格或不支持非失真拉伸，不要简单将整张原版图暴力拉宽；优先选择已经实现可伸缩的原版 Frame/Canvas/弹窗组合。
3. 将**宽度作为配置/单一常量**：建议先以 `SuggestedWidth≈1250–1450` UI 坐标、`SuggestedHeight≈720–840` 做初值，在 1920×1080、1600×900 和不同 UI Scale 下实机定标。以上是**起始建议**，不是游戏已验证的固定数值。
4. 最终比例应保留最初截图的**深棕材质、细框边缘、简洁标题和右上 X**，不能变成百科的粗石纹多区域内容页。
5. 外层遮罩使用游戏已有半透明黑色，不增加背景图片；城镇左侧原版 GameMenu 保持原样，弹窗打开后禁止误点底层菜单。
6. 从最简单的纯标题/底框/关闭按钮 Movie 起步，确认新窗口与原本 UI 共存、能释放焦点，再增加完整兵种数据。

---

## 4. 拟修改/新增文件

### 必改

- `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/SettlementProfessionalPreferenceMenuBehavior.cs`
  - 保留 `AddMenus()`、`OptionId`、`CanOpen()` 的现有权限规则。
  - `Open()` 改为构造/打开新的 `RecruitmentWeightScreenService`；删除旧 `ShowMultiSelectionInquiry` + `ShowTextInquiry` 路径，不保留双份真实提交 UI。
- `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Behaviors/SettlementProfessionalPreferenceBehavior.cs`
  - `MaximumCustomWeight = 9`；同时处理旧存档 `10…20` 的合理归一化，确保 UI 和实际生产权重一致。
  - 保留 `_records`、`Reconcile()`、`TrySetWeights()`、资格重建、易主重置、`FilterProfessionalOffers()` 原有业务职责。
- `ModifiedArmy/ModifiedArmy/ModuleData/submodule_strings.xml`
- `ModifiedArmy/ModifiedArmy/ModuleData/Languages/CNs/modifiedarmy-zho-CN.xml`
  - 添加/修正弹窗标题 `招募权重`、`恢复默认`、`应用修改`、必要错误消息、旧 `0～20` 提示改为 `0～9`；注意同步英文/中文，复用合适的旧 key，不要删除仍在其它地方引用的本地化 key。

### 建议新增

- `ModifiedArmy/GUI/Prefabs/ModifiedArmyRecruitmentWeightPopup.xml`：新的 Movie 根 Prefab；内置同排 Row 模板、可滚动列表、X 和底部按钮；如确有必要再拆独立 Row Prefab。
- `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/RecruitmentWeightScreenService.cs`：对话框创建、焦点、事件订阅/清理、Esc、Screen 移除、重入控制。
- `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/RecruitmentWeightVM.cs`：弹窗总 VM，包含权重草稿、恢复默认、提交、退出。
- `ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/RecruitmentWeightTroopItemVM.cs`：行 VM，兵种图片/名称/右键百科/权重双向绑定。

### 可能需要修改

- `ModifiedArmy/ModifiedArmy/ModifiedArmy.csproj` 或项目已有 `build/deploy`：确认 **模块根目录** `ModifiedArmy/GUI/Prefabs/*.xml` 是否会被正确部署到游戏 `Modules/ModifiedArmy/GUI/Prefabs/`。当前 `.csproj` 没有明确涵盖这个路径；**编译成功不代表 Movie 可加载**。
- 如没有原版合适的“当前数值小方框” Brush，可组合原版边框 Brush + `TextWidget`，不得为方框新作贴图。

**不修改**：`professionalTroopTemplates.xml` 默认数值、职业兵训练/每日补兵、Hearth/工资、驻军策略、原版 `code` 文件、`ModifiedFamily`、`ModifiedPolitics`。

---

## 5. ViewModel 约定（Codex 依真实 DLL 完成可编译代码）

### 5.1 根 VM

```csharp
// RecruitmentWeightVM : ViewModel
// 保存对 Settlement 的引用及打开时的 TemplateId / OwnerClanId / 资格快照标识。

[DataSourceProperty] public string TitleText { get; }  // 招募权重
[DataSourceProperty] public string ResetText { get; }  // 恢复默认
[DataSourceProperty] public string ApplyText { get; }  // 应用修改
[DataSourceProperty] public MBBindingList<RecruitmentWeightTroopItemVM> Troops { get; }

public void ExecuteResetDefaults();  // 所有行恢复 XML 默认权重，草稿更新
public void ExecuteApply();          // 一次性 TrySetWeights；仅成功后关闭
public void ExecuteCancel();         // 丢弃草稿，关闭
```

- `Open` 时计算 `snapshot = ProfessionalRecruitmentEligibilityModel.Evaluate(settlement)`，要求 `snapshot?.Template != null`、OwnerClan 为玩家。
- `behavior.Reconcile(settlement)` 得到当前有效权重；对每个 `snapshot.EligibleEntries` 创建行 VM，将当前值复制为**草稿**，并记录 `entry.Weight` 作为默认值。
- 条目数量可能为零，也可能多于五个；不能硬编码参考图。顺序优先保持 `snapshot.EligibleEntries` 的稳定顺序（如另排序，只改变 UI 顺序，不改业务身份）。
- 行权重变更**仅作用于 VM**，不写 `_records`、不调用 `FilterProfessionalOffers`、不调用生产逻辑。
- `ExecuteApply` 使用 `ToDictionary(troop.StringId, weight)` 形成完整键集合；调用后端已有 `TrySetWeights(settlement, draft, Clan.PlayerClan)` **一次**。如果所有权、资格、模板、行集合在弹窗打开后发生变化，后端返回 `false`，必须有游戏内失败提示，不要静默丢失设置。
- `ExecuteResetDefaults` 只写行 VM 的 `Weight = Math.Clamp(entry.Weight,0,9)`；**不是**所有值重置为 `1` 或 `0`。
- 若没有合法职业兵，空列表需有简短空状态/禁止应用，但**正常列表不允许添加解释文字**。

### 5.2 行 VM

```csharp
// RecruitmentWeightTroopItemVM : ViewModel
internal CharacterObject Troop { get; }
internal int DefaultWeight { get; }
[DataSourceProperty] public string NameText { get; }
[DataSourceProperty] public CharacterImageIdentifierVM Visual { get; }
[DataSourceProperty] public int Weight { get; set; } // 0..9，变化时通知绑定
public void ExecuteOpenEncyclopedia();
```

- 兵牌参考 `code/RecruitVolunteerTroopVM.cs`：`new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(troop, false))` 或已验证等价方法；图标取 `CampaignUIHelper.GetCharacterTypeData/GetCharacterTierData`，不得写死剑/弩图标。
- 名字从 `troop.Name.ToString()` 获取。
- `Weight` setter 将 UI 回传值限制在 `[0,9]`，触发 `OnPropertyChangedWithValue(value, nameof(Weight))` 或当前项目兼容写法，并确保不是修改了副本而界面不刷新的“假滑块”。
- `ExecuteOpenEncyclopedia` 只基于该行 `Troop.EncyclopediaLink`，参考原版 `RecruitVolunteerTroopVM.ExecuteOpenEncyclopedia`；必须解决当前模态层遮挡百科和脏草稿的情况。

### 5.3 持久化/旧档兼容（**不仅是 UI**）

当前业务上限为 `20`。新规则要保证**所有入口**都只能写入 `0–9`，旧存档的权重 `10–20` 也不导致新 UI 展示 9、后台继续按 18 生产。

建议优先采用以下**保守、幂等**策略：

1. 把 `SettlementProfessionalPreferenceBehavior.MaximumCustomWeight` 从 `20` 修改为 `9`。
2. `Reconcile()` 发现现有合法条目旧值 `>9` 时，按兵种逐项钳制到 `9`，`<0` 则钳制到 `0`，不重建整个定居点设置；合法的 `0…9` 不变。只对真正更改的值记录可追溯日志，避免每次刷新刷屏。
3. 对“数据尚未 `Reconcile` 就被 `GetEffectiveWeights()` 读取”的路径也加合法值保护，防止恢复后首日生产使用越界旧值。可复用统一的 `NormalizeWeight()`；保持数据校验**唯一策略**，不要散落重复魔数。
4. 模板默认权重必须保持与有效权重规则一致。当前 XML 已落在新范围；如果未来超界，记录配置错误且保证有效值合法，不直接修改 XML 对象。
5. 不改变现有 Saveable 数据结构、字段索引、存档键 `modifiedarmy_settlement_professional_preferences_v1`。
6. `TrySetWeights()` 仍要求**整个合法键集合**，允许全零；成功后继续调用已有 `FilterProfessionalOffers()`，保持原业务行为。

以上旧档处理是**本实施方案建议的迁移策略**，不是已实现的仓库事实。Codex 若发现现有恢复流程另有策略，须解释差异并确保新规则仍然满足。

---

## 6. Gauntlet XML 的具体构造

以下是**可供编码的结构示意，不是省略资源布局后就能直接运行的完整 Prefab**。必须从真实 XML 中取子节点、外观/边框、尺寸属性，不能用图形编辑工具把效果图作为背景图。

### 6.1 顶层结构

```xml
<Prefab>
  <Window>
    <!-- 半透明遮罩，禁止穿透点击底层城镇菜单 -->
    <Widget WidthSizePolicy="StretchToParent"
            HeightSizePolicy="StretchToParent"
            Sprite="BlankWhiteSquare_9" Color="#000000B0">
      <Children>
        <!-- 确认原版 Inquiry 风格 Brush；以下 Recruitment.Frame 是已知候选 -->
        <BrushWidget Id="RecruitmentWeightPopup"
                     WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                     SuggestedWidth="1350" SuggestedHeight="800"
                     HorizontalAlignment="Center" VerticalAlignment="Center"
                     Brush="Recruitment.Frame">
          <Children>
            <!-- @TitleText / Popup.CloseButton -->
            <!-- 可滚动的 Troops 列表 -->
            <!-- @ResetText / @ApplyText 两个原版按钮 -->
          </Children>
        </BrushWidget>
      </Children>
    </Widget>
  </Window>
</Prefab>
```

- `Recruitment.Frame` **仅代表经源码确认的一种可用原版候选**，正式施工必须与初始背景框截图对比后定稿；绝不将其视为已证明的 Inquiry 精确 Brush。
- `SuggestedWidth/Height` 是需要按画面校准的初始参数，不是必须固定的分辨率；避免超出 UI Safe Area。
- 不再使用 `Encyclopedia.Frame` 或 `StdAssets\tabbar_popup` 的百科式大标题签（除非原弹窗自身就是该风格且实机验证吻合）。

### 6.2 中间列表

- 中间使用 `ScrollablePanel` + `ListPanel DataSource="{Troops}"` + `ItemTemplate`，滚动裁剪和标准 Scrollbar 的 Id 必须互相对应。
- 行背景建议从 `Party.TroopTupple.Right` 或同类可扩展的**游戏本体** Brush/贴图取得，必要时保留原生兵牌和简化行底，**不得直接复制带部队交易命令的 `PartyTroopTupleButtonWidget`**。
- **兵牌/名称区**是独立可接收右键事件的控件；滑块区是**兄弟节点**，不能把 Slider 放进覆盖整行的点击按钮。
- 所有子元素使用 `VerticalAlignment="Center"` 且统一行高。建议首版行高约 `84~96` UI 单位，肖像 `~110×76`，滑块 `~300–390` 宽，名称列 `~300–360` 宽；这些是调试基线，须根据实际本体资源尺寸校准。
- 悬浮标题不需要显示军饷/兵营/Hearth/权重说明。可保持原版图标悬浮提示和兵种百科跳转。

### 6.3 滑块细节：数字 `0` 和 `9` 必须是每行可见的独立文本

核心属性：

```xml
<!-- 仅是 ItemTemplate 中的滑块局部示意 -->
<ListPanel StackLayout.LayoutMethod="HorizontalLeftToRight"
           WidthSizePolicy="CoverChildren" HeightSizePolicy="Fixed"
           SuggestedHeight="48" VerticalAlignment="Center">
  <Children>
    <TextWidget Text="0" WidthSizePolicy="Fixed" SuggestedWidth="28"
                HeightSizePolicy="StretchToParent" VerticalAlignment="Center" />

    <SliderWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                  SuggestedWidth="340" SuggestedHeight="42"
                  VerticalAlignment="Center"
                  MinValueInt="0" MaxValueInt="9"
                  IsDiscrete="true" DiscreteIncrementInterval="1"
                  ValueInt="@Weight"
                  Filler="Filler" Handle="SliderHandle"
                  DoNotUpdateHandleSize="true" UpdateChildrenStates="true">
      <Children>
        <!-- 用 ClanPartiesRightPanel.xml 中已验证的完整子节点替换： -->
        <!-- SPClan.Slider.Canvas / .Fill / .Frame / .Handle -->
        <!-- Id="Filler"、Id="SliderHandle" 必须与 SliderWidget 属性一致 -->
      </Children>
    </SliderWidget>

    <TextWidget Text="9" WidthSizePolicy="Fixed" SuggestedWidth="28"
                HeightSizePolicy="StretchToParent" VerticalAlignment="Center" />
    <TextWidget IntText="@Weight" WidthSizePolicy="Fixed" SuggestedWidth="56"
                HeightSizePolicy="StretchToParent" VerticalAlignment="Center" />
  </Children>
</ListPanel>
```

> XML 片段有意略去 Brush 和若干外观布局属性，**不是可直接放入项目的最终 XML**。请参照 `code/GUI/SandBox/GUI/Prefabs/Clan/ClanPartiesRightPanel.xml` 的真实 Slider 内部四层 `ImageWidget` 构造。

关键验证：到 `0`、`1`、`9` 时位置和数值准确；鼠标拖动、点击轨道和滚轮（若允许）不会产生浮点中间值；输入变化只写该 `ItemVM.Weight`；数值框不遮挡滑块终点 `9`；列表滚动不会误触滑块。

### 6.4 右键百科和交互区域

用 `ButtonWidget` 包住兵牌和兵种名称区域（也可分开两按钮），参考：

```xml
<ButtonWidget Command.AlternateClick="ExecuteOpenEncyclopedia"
              HoveredCursorState="RightClickLink"
              DoNotPassEventsToChildren="true">
  <Children>
    <!-- 原版肖像 ImageIdentifierWidget + NameText -->
  </Children>
</ButtonWidget>
```

- 切勿把整个条目、滑块、数字框装进此按钮里。
- 不复制 `PartyTroopTupleButtonWidget` 的交易相关属性、`ExecuteTransferSingle`、`ExecuteApplyTransaction`、升级扩展面板。
- 若点击百科需要关闭弹窗，先处理草稿，再安全释放 Movie/Layer 后执行 `Campaign.Current.EncyclopediaManager.GoToLink(troop.EncyclopediaLink)`。

---

## 7. ScreenService 生命周期（可直接参考 ModifiedFamily，但要自有实现）

建议通过 `RecruitmentWeightScreenService` 管理独立 `GauntletLayer`：

```text
GameMenu: SettlementProfessionalPreferenceMenuBehavior.Open()
    -> 读取 Settlement.CurrentSettlement
    -> 验证 OwnerClan == Clan.PlayerClan 和 Template 有效
    -> 创建 RecruitmentWeightVM
    -> RecruitmentWeightScreenService.Open(vm)
       -> ScreenManager.TopScreen
       -> new GauntletLayer("ModifiedArmyRecruitmentWeightLayer", priority, false)
       -> IsFocusLayer = true; InputRestrictions = All
       -> 注册 GenericPanelGameKeyCategory（以本体运行时 HotKey API 为准）
       -> hostScreen.AddLayer(layer)
       -> layer.LoadMovie("ModifiedArmyRecruitmentWeightPopup", vm)
       -> 订阅宿主 Screen pop 事件
       -> ScreenManager.TrySetFocus(layer)

Close() （幂等）:
    -> 先清理静态引用、解绑宿主事件，阻止重复 Close
    -> TryLoseFocus(layer)
    -> ReleaseMovie(movie)
    -> hostScreen.RemoveLayer(layer)
    -> vm.OnFinalize()
```

- 完整异常回滚：半途 `LoadMovie()` 抛异常/返回 null 时必须从宿主撤掉已经 Add 的 Layer，释放焦点和所有引用。
- 同一窗口重复点击入口不叠加 Layer；主界面退出/换存档不会残留。
- Esc 需要真实绑定原版输入类别与关闭命令；不要只设置一个名称而没有键盘事件处理。
- 没有自定义图集就不要额外加载/卸载自定义 SpriteCategory。
- 如果右键百科进入新 Screen，避免旧 Layer 持续置顶遮挡百科，按第 6.4 节安全跳转。

---

## 8. 完成顺序（实际施工建议）

1. **环境确认**：检查 git 状态、当前分支/HEAD、目标游戏版本与构建/部署路径。
2. **定位旧背景框**：比较原版 Inquiry 背景与 `Recruitment.Frame`，确认哪个原版 Brush 能复现“最开始的深棕框”；宽版九宫格拉伸无破图。
3. **搭建最小窗口**：标题「招募权重」、宽版深色外框、右上 X、两按钮；实机打开关闭正常。
4. **绑定真实数据**：`Evaluate`/`Reconcile` -> `MBBindingList`，动态条目，原版兵牌/兵名；观察五行和多行。
5. **绑定滑块**：每行真实 `SliderWidget` 与 `Weight` 双向绑定；保留左 `0`、右 `9`、末端数值框，全部同线。
6. **右键百科**：仅兵牌/名字触发，验证不会穿透及不会误改权重。
7. **写入规则**：后端上限 9、旧档归一化、恢复默认只改草稿、应用一次性提交；异常/过期校验失败提示。
8. **回归验证**：换文化、城堡/城镇、无合法兵、9+ 行、缩放、读档与重新打开、当天 offer 被禁用时的处理。
9. **提交产物**：提供修改文件清单、构建结果、实机截图、旧存档测试、未验证项；不要未经授权推送主分支。

---

## 9. 强制验收清单

| 检查项 | 通过标准 |
|---|---|
| 外框 | 原版 **深棕色 Inquiry 风格**，比旧框明显更宽，不能换成百科大石框；无自定义贴图；关闭 X 在右上角 |
| 标题和正文 | 弹窗标题唯一是 **「招募权重」**；正常列表里无说明、兵营、工资、Hearth、比例条/表头 |
| 每行排版 | 兵牌＋名称＋左端 `0`＋水平滑块＋右端 `9`＋权重数值框全部**同一水平** |
| 数字含义 | 每个兵种都显示**最小 0**和**最大 9**，且末端另显示当前值；不能把两端 0/9 当作当前值 |
| 权重范围 | 权重仅为 0 到 9 的十个整数；端点可达；拖动一个兵种不会拖动其它滑块 |
| 列表来源 | 数据与行数来自当前 `ProfessionalRecruitmentEligibilitySnapshot.EligibleEntries`，非固定五行/假头像 |
| 恢复默认 | 恢复模板每个 troop 的真实默认权重，仅在草稿中，未点击应用之前存档不变 |
| 保存 | 一次 `TrySetWeights` 完整提交；保存后重开/读档仍正确；失败不关闭且有反馈 |
| 取消 | X/Esc 关闭后修改未写入；不要出现明明点击关闭却已经保存的情况 |
| 右键百科 | 右键**兵牌/兵名**打开对应 Troop 百科；不会触发部队转移/滑块事件，脏草稿不被悄悄丢掉 |
| 旧档兼容 | 旧 `10…20` 权重正常映射到 9 或项目审核确认的等效策略；后台生产权重与 UI 一致 |
| 动态资格 | 易主、文化/兵营/模板变化后现有 `Reconcile` 规则仍有效，非法条目不可提交 |
| 极端列表 | 0、1、5、8、9+ 行及中文长兵名时不重叠，不遮按钮，列表滚动正常 |
| 分辨率 | 测试 1920×1080、1600×900 和至少两档 UI Scale；面板不越屏，末端数值不被裁剪 |
| 生命周期 | 连续开关 20 次不积累层；打开百科后焦点正确；退出战役/读档不遗留覆盖层 |
| 资源部署 | XML 到达实际 `Modules/ModifiedArmy/GUI/Prefabs/`；无 `LoadMovie` 找不到文件、Brush/Sprite 缺失 |

## 10. Codex 最终交付格式

请汇报：

1. 当前分支和基线 HEAD，以及 `git diff --stat`；
2. 新增/修改文件清单，按 Popup XML / ScreenService / VM / Behavior / 本地化分类；
3. 实际使用的**背景 Brush 名称**以及证明其与原始 Inquiry 窗口一致或足够接近的比对（这是本版最大差异）；
4. 真实构建命令与结果、资源安装目录、实际实机运行检查；
5. 宽版主界面、权重分别 0/9、滚动列表和右键百科截图；
6. 权重恢复默认/取消/保存、旧存档、不同定居点的测试结果；
7. 所有尚未验证的事项、已知问题及回滚方法；
8. 未经用户允许**不要 push/merge**。

---

## 11. 代码与资料索引

- [当前 ModifiedArmy 招募偏好菜单](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Presentation/SettlementProfessionalPreferenceMenuBehavior.cs)
- [当前 ModifiedArmy 招募偏好 Behavior](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Behaviors/SettlementProfessionalPreferenceBehavior.cs)
- [当前资格模型](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/Recruitment/Preferences/Models/ProfessionalRecruitmentEligibilityModel.cs)
- [职业兵模板](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedArmy/ModifiedArmy/ModuleData/professionalTroopTemplates.xml)
- [本体 PartyTroopTuple](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Prefabs/Party/PartyTroopTuple.xml)
- [本体 PartyScreen Brush](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Brushes/PartyScreen.xml)
- [本体 Clan 工资滑块](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Prefabs/Clan/ClanPartiesRightPanel.xml)
- [本体 Clan Slider Brush](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Brushes/Clan.xml)
- [本体 Recruitment Popup](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Prefabs/Recruitment/RecruitmentPopup.xml)
- [本体 Recruitment Frame Brush](https://github.com/GuihuaZhou/bannerlord/blob/main/code/GUI/SandBox/GUI/Brushes/Recruitment.xml)
- [本体 RecruitVolunteerTroopVM](https://github.com/GuihuaZhou/bannerlord/blob/main/code/RecruitVolunteerTroopVM.cs)
- [ModifiedFamily GauntletLayer 生命周期参考](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/UI/FamilyActionScreenHost.cs)
