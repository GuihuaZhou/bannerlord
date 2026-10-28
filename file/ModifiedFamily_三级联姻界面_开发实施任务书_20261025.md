# ModifiedFamily 三级联姻界面——设计定稿与开发实施任务书

> **文档类型**：面向开发 Worker 的可实施规格（Functional Specification + UI Contract + Implementation Plan + Acceptance Tests）  
> **项目**：Mount & Blade II: Bannerlord / `GuihuaZhou/bannerlord` / `ModifiedFamily`  
> **代码基线**：`main`，提交 `ab062534eca33029eab6636dc70678bf7093b8a1`（2026-10-24，`feat: add clan family interaction UI`）  
> **文档日期**：2026-10-25  
> **性质**：任务书、非已实现代码；文档中的新增类名/布局名属于实施目标，需由 Worker 创建。  
> **执行限制**：本次文档不要求修改仓库、提交 PR 或重做 `ModifiedFamily` 联姻后台。修改权、分支策略由负责实施的 Worker 另行遵守项目约定。

## 0. 给接手 Worker 的一页摘要（必须先读）

### 0.1 任务目标

将游戏“家族 → 选中未婚家族成员 → 联姻”按钮打开的 **空白百科式窗口**，升级为可用的 **单窗口三级联姻选择界面**：

1. **第一级：联姻 Clan 列表**——展示可联姻家族的家族旗帜、名称、所属王国、家族文化、与玩家 Clan 的关系值、符合条件的 Hero 数量；支持按文化/王国优先级排序、筛选与搜索。
2. **第二级：Clan 详情与候选 Hero**——模仿原版 Clan 百科，展示家族旗帜、简介、领袖、王国、文化、等级、关系等；仅列出与当前己方 Hero 能联姻的候选人物。点击一个候选进入第三级。
3. **第三级：Hero 百科式详情**——尽量忠实采用原版百科 Hero 页面视觉信息层级：人物 3D 模型、姓名身份、技能、特性、家族关系及其他本体允许显示的信息。在人物详情上增加“安排联姻”操作。
4. **联姻条款面板（不是第四级页面）**——选择女性加入男性 Clan / 男性入赘女性 Clan、礼金方向和金额，提交前重新校验并二次确认，再创建后台联姻提议。

**核心交互规则**：三个页面/一个条款面板始终处于 **同一个 `GauntletLayer`、同一个 `Movie`、同一个根 ViewModel** 中切换。不要每前进一步关闭一个 Movie、打开另一个 Movie。顶部提供“返回”“面包屑路径”，可以回退、更换候选，再继续。关闭窗口回到原版 Clan 界面，不丢失原版家族页面。

### 0.2 必须交付的结果

- 点击“联姻”不再出现只有边框的大空白窗口；显示有效的家族列表或带原因的空状态。
- 页面视觉必须延续截图中的本体百科皮肤：`Encyclopedia.Frame`、本体标题签、面包屑、暗金文字、雕花框、旗帜/人物模型，不做网页风格卡片。
- 三级完整可导航，能在不同 Clan、不同 Hero 间反复切换；返回按钮与关闭按钮可靠。
- 最后仍调用 **现有** `FamilyProposalService.TryCreateMarriageProposal`，只创建“等待答复”的请求，不能直接执行 `MarriageAction.Apply`。
- 游戏正常加载存档，重复打开/关闭 30 次、切换 100 次不会崩溃、焦点卡死、出现多层遮罩或重复发提议。

### 0.3 范围与非目标

**本期范围**：仅“联姻”入口所打开的新 UI；不重写“收养”入口，不改变已确定的 AI 评分、七天答复、一次反提议、60 天冷却、30 天待执行等后端规则；不覆盖原版 `ClanScreen.xml` 或 `EncyclopediaHeroPage.xml`。

**非目标**：新增婚姻制度、重算 AI 接受率、增加新的保存字段、自动替玩家批准协议、修改原版百科页面本身。只读的 UI 导航状态不进存档。

---

## 1. 代码盘点：哪些已经存在，哪些尚未接通

以下为 **已核对的基线代码事实**，不得把后文“拟创建文件”误认作现有文件。

| 路径 | 基线状态 | 本任务处理 |
|---|---|---|
| `ModifiedFamily/ModifiedFamily/UI/Clan/ClanManagementVMMixin.cs` | 现有 `ClanLordItemVM` Mixin，`ExecuteOpenMarriageActions()` 从当前选中 Hero 打开联姻 | 保留注入点，不必重做按钮；必要时加防重入和失败提示 |
| `ModifiedFamily/ModifiedFamily/UI/Clan/FamilyActionsButton.xml` | 原版家族成员页“联姻 / 收养”入口 | 保留；确认按钮仍只对符合条件的己方 Hero 显示 |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageScreenService.cs` | `Open(hero)` 调用 `FamilyActionScreenHost.Open("ModifiedFamilyMarriageShell", ..., new FamilyActionScreenVM(hero,true,null))` | **P0 必改**：改用新的三级导航根 VM |
| `ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageShell.xml` | 仅百科暗底、框、标题和关闭按钮；没有任何 Clan/Hero 内容 | **P0 必改**：填入页容器/导航/条款层，同时继续复用百科外框 |
| `ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageManagement.xml` | 已有两栏 Clan→Hero 选择的 XML 原型，但当前 `csproj` 未包含且打开服务未指向它 | 可拆取列表、旗帜、滚动区代码，**不要全盘另开一个窗口** |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageSelectionVM.cs` | 已有 `MarriageClanItemVM`、`MarriageHeroItemVM`、可联姻目标筛选、Hero 头像及旗帜绑定；`ExecuteContinue()` 关闭当前窗口并跳转旧条款 Inquiry | **保留/重构**：抽出筛选层与列表 ItemVM，导航改为根 VM 内切换 |
| `ModifiedFamily/ModifiedFamily/UI/FamilyActionScreenHost.cs` | 已实现 `GauntletLayer`/`LoadMovie`/焦点/关闭及 `OnFinalize()` 清理 | **直接复用**，不在页切换时 `Close()` |
| `ModifiedFamily/ModifiedFamily/UI/FamilyPlayerInteractionService.cs` | 现有 `ShowMarriageTermsFor()`、`SelectMarriageTerms()`、`SubmitMarriage()` 等旧 Inquiry 工作流 | 后台提议生成可复用；新 UI 走内嵌条款，避免同一流程重复弹旧 Inquiry；保留其他旧入口兼容性 |
| `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs` | 存在 `CanCreateMarriageProposal(draft)`（无状态改变）与 `TryCreateMarriageProposal(draft)`（真正创建） | 必须直接复用；UI 不复制判断逻辑 |
| `ModifiedFamily/ModifiedFamily/Marriage/Models/MarriageEligibilityModel.cs` | 合法 Clan/Hero、亲属及临时阻塞规则 | UI 筛选必须与正式提议校验一致 |
| `ModifiedFamily/ModifiedFamily/ModifiedFamily.csproj` | 显式包含 `ModifiedFamilyMarriageShell.xml` 和成员按钮 XML；**未包含** `ModifiedFamilyMarriageManagement.xml` | 新 XML 文件必须列入资源部署并验证游戏目录 |
| `ModifiedFamily/ModifiedFamily/Main.cs` | 已启用 `UIExtender("ModifiedFamily")` 与 Harmony | 不要重复初始化 UIExtender |

> **立即解释截图一**：当前 `MarriageScreenService` 加载的仅是空白百科外壳，而 `MarriageSelectionVM`/`ModifiedFamilyMarriageManagement.xml` 尚未成为该入口的实际 Movie 与 VM。它不是“可联姻对象一定为零”的证据。必须同时补 UI 页面内容与数据源绑定。

### 1.1 原版视觉/结构参考（全部来自 `code/GUI/SandBox/GUI/Prefabs`）

| 参考文件 | 复用内容 | 注意事项 |
|---|---|---|
| `Encyclopedia/EncyclopediaBar.xml` | `Encyclopedia.Frame`、标题条、返回前进、面包屑、关闭位置 | 复制**适配后的布局**，不盲用其私有命令绑定 |
| `Encyclopedia/EncyclopediaSubPages/EncyclopediaClanPage.xml` | 左 370 宽家族视觉区、右侧列表、王国徽记、领袖、成员、简介 | 改成“仅合法联姻候选”及本任务的 VM 字段 |
| `Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml` | `EncyclopediaCharacterTableauWidget` 3D 模型、技能、Traits、家族、盟友、敌人、百科信息 | 依赖大批专用 VM 字段和命令，**不能原样嵌入就认为会工作** |
| `Clan/ClanMembers.xml` | 人物选择视图、`ClanLordTuple`、人物信息、字体/分隔线 | 当前按钮已接入此体系，保留原入口 |
| `Clan/ClanScreen.xml` | 家族界面的按钮与纹理语言 | 不要全量覆盖原版文件 |

仓库链接（固定基线）：
- [当前提交](https://github.com/GuihuaZhou/bannerlord/tree/ab062534eca33029eab6636dc70678bf7093b8a1)
- [原版百科顶部](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/code/GUI/SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaBar.xml)
- [原版 Clan 百科](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/code/GUI/SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaClanPage.xml)
- [原版 Hero 百科](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/code/GUI/SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml)
- [现有联姻原型](https://github.com/GuihuaZhou/bannerlord/blob/ab062534eca33029eab6636dc70678bf7093b8a1/ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageManagement.xml)

---

## 2. 最终界面定义（Worker 实现必须达到的视觉与行为）

### 2.1 共用外框

- 游戏家族页面上方覆盖单层与原版百科近似的深色遮罩，中央由 `Encyclopedia.Frame` 控制外框尺寸和缩放；不要通过截图像素硬编码全屏尺寸。
- 顶部居中：“联姻”；顶部左侧显示后退（必要时前进）；标题下方显示 `联姻 > 目标家族 > 目标人物`，每段可按导航层级点击。
- 右上角原版风格关闭按钮；最下部右侧为随页面变化的操作条。
- 所有页面始终显示“己方联姻 Hero”的简短标识（姓名、家族、头像/小图），避免玩家浏览多个目标后不知道自己正在为谁择偶。
- 在 16:9 常见分辨率及不同 UI 缩放下不发生页面重叠、文字溢出或关闭按钮超出画面；保留滚动控件。
- 提示和描述使用 `TextObject("{=MF_UI_...}中文")` 对应语言资源；不要在大量 XML 里散落不可本地化的中文字符串。

### 2.2 第一级：可联姻 Clan 列表

**布局草图**：

```text
┌───────────────────────────────────────────────────────────────────────────┐
│  ←   联姻                   联姻 > 选择家族                        [×]   │
├───────────────────────────────────────────────────────────────────────────┤
│  为 [己方 Hero 姓名/头像] 寻找配偶                                       │
│  [搜索家族……] [全部] [同国同文化] [异国同文化] [同国异文化]              │
│                                                                           │
│  ┌────────┬────────────────┬──────────┬───────┬────────┬──────────────┐   │
│  │ Clan旗 │ 家族名称       │ 所属王国 │ 文化  │ Relation│ 可联姻人数  │   │
│  ├────────┼────────────────┼──────────┼───────┼────────┼──────────────┤   │
│  │ 旗帜   │ 家族 A         │ 王国甲   │ 帝国  │ +35     │ 3            │   │
│  │ 旗帜   │ 家族 B         │ 王国乙   │ 帝国  │ -10     │ 2            │   │
│  │ 旗帜   │ 家族 C         │ 王国甲   │ 阿塞莱│ +12     │ 1            │   │
│  └────────┴────────────────┴──────────┴───────┴────────┴──────────────┘   │
│  [说明：仅显示至少拥有一名符合条件 Hero 的合法 Clan]       [关闭]       │
└───────────────────────────────────────────────────────────────────────────┘
```

**字段**：`Banner`（本体 `BannerImageIdentifierVM`）、`Name`、`KingdomName`、`CultureName`（严格用 `Clan.Culture`）、`RelationValue/RelationText`（`Clan.PlayerClan.GetRelationWithClan(targetClan)`）、`CandidateCount`、`MatchTier`、`LeaderName`（可作为悬停）及操作 `ExecuteSelect()`。

**候选与排序**：
1. 先调用 `MarriageEligibilityModel.CanUseClan` 排除不合法家族；不能直接拿 `Clan.All` 全量显示。
2. 针对当前己方 Hero，至少存在一位异性、可协商、且两种婚后归属中至少一种通过 `FamilyProposalService.CanCreateMarriageProposal`，才展示该 Clan。
3. 分类采用 **Clan 文化与所属王国**，与当前 `MarriageProposalModel.CultureAllowed` 同口径；同国同文化 → 异国同文化 → 同国异文化（仅 `AllowSameKingdomDifferentCulture` 开启时）；异国异文化不可联姻。
4. 同级内部优先可联姻人数、玩家 Clan Relation 等；具体稳定排序：`MatchTier ASC, Relation DESC, CandidateCount DESC, Clan.Name ASC`。这是 UI 推荐排序，不修改 AI 评分。
5. 搜索应匹配 Clan 名称，且可扩展王国和文化文本；即时筛选不应反复调用有副作用的业务方法。
6. 如果没有候选，显示原因文案和“返回家族”操作，不允许空白框。
7. 单击家族**进入第二级**；双击不是必要条件。页切换不创建新 Movie。

### 2.3 第二级：Clan 百科式详情 + 可联姻 Hero

**布局草图**：

```text
┌───────────────────────────────────────────────────────────────────────────┐
│  ←   联姻                    联姻 > 家族 A                        [×]   │
├───────────────────────┬───────────────────────────────────────────────────┤
│  [大号 Clan 旗帜]     │  可联姻成员（N）                                │
│  家族 A               │  ┌────────────┐ ┌────────────┐ ┌────────────┐ │
│  所属王国：……         │  │ Hero 头像  │ │ Hero 头像  │ │ Hero 头像  │ │
│  文化：……             │  │ 姓名 年龄  │ │ 姓名 年龄  │ │ 姓名 年龄  │ │
│  Clan 领袖：……        │  │ 身份/特性  │ │ 身份/特性  │ │ 身份/特性  │ │
│  家族等级：……         │  └────────────┘ └────────────┘ └────────────┘ │
│  与我方关系：……       │                                                   │
│                       │  家族简介 / 家族实力 / 领地信息（可滚动）         │
│  家族百科简介：       │  点击人物进入 Hero 详情                          │
│  ……                  │                                                   │
├───────────────────────┴───────────────────────────────────────────────────┤
│  [返回家族列表]                                                 [关闭]   │
└───────────────────────────────────────────────────────────────────────────┘
```

**关键视觉**：左侧比例和旗帜风格参考 `EncyclopediaClanPage.xml` 的 `SuggestedWidth="370"` 左栏，右侧参考本体成员网格。使用 `EncyclopediaClanSubPageElement` / `MaskedTextureWidget` / `EncyclopediaDivider` 等**适配后的部件**；如组件依赖百科专用 VM，优先使用已验证的 `MaskedTextureWidget + BannerImageIdentifierVM`，禁止让缺少 `DataSource` 的组件空转。

**数据**：`SelectedClan`、`ClanBanner`、`ClanName`、`KingdomName`、`CultureName`、`LeaderName`、`TierText`、`RelationText`、`DescriptionText`（优先 `Clan.EncyclopediaText`，无内容时显示基本介绍）、`MBBindingList<MarriageHeroItemVM> EligibleHeroes`。成员 Item 显示姓名、头像、年龄、文化/特性摘要、是否能够正常嫁娶/入赘（可标记说明；不是投票保证）。

**操作**：点击候选进入三级；返回一级保持之前搜索筛选和滚动位置（能保留则保留，不强求重建 ScrollablePanel 原始 offset）；选择另一个家族时清空旧 Hero 选择，防止串人。

### 2.4 第三级：Hero 百科式详情（与截图二尽量一致）

**布局草图**：

```text
┌───────────────────────────────────────────────────────────────────────────┐
│  ←   联姻                 联姻 > 家族 A > Hero X                  [×]   │
├────────────────────────────┬──────────────────────────────────────────────┤
│    [本体 3D Hero 全身像]   │  信息                                       │
│     Hero 名称 / 身份       │  年龄 · 文化 · 所属 Clan · 身份              │
│     特性图标               │  与玩家 Clan 关系                           │
│                            │                                              │
│     技能图标 + 数值        │  好友 / 敌人（本体可见信息）                │
│     统御/魅力/战术……       │  [头像网格]                                  │
│                            │                                              │
│                            │  家族 / 亲属（本体可见信息）                │
│                            │  [头像网格]                                  │
│                            │                                              │
│                            │  当前联姻对象：己方 Hero ↔ Hero X          │
├────────────────────────────┴──────────────────────────────────────────────┤
│  [返回该 Clan]                         [查看原版百科]  [安排联姻] [关闭] │
└───────────────────────────────────────────────────────────────────────────┘
```

**硬性标准**：以 `code/GUI/SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml` 为主要视觉基准，左侧 3D 人像、姓名/身份、Traits、Skills；右侧 `Stats`、好友/敌人/家族信息层级。可按原版规则隐藏尚未获知的信息，不泄露游戏原本不可见的信息。没有资料时展示空状态或灰化区块，不因 null 绑定崩溃。

**非常重要**：原版 `EncyclopediaHeroPage.xml` 并非一个可以任意复用的独立完整 Movie，涉及 `HeroCharacter`、`Skills`、`Stats`、`Allies`、`Enemies`、`Family`、悬停事件、页内导航、信息隐藏等属性与命令。Worker 必须：
- 从源码提取**布局与已验证的控件用法**，新建 `MarriageHeroDetailVM` 暴露相同/等价的必需绑定；
- 核对当前实际游戏 DLL 的 `HeroVM`、百科 Hero 页 VM 和 `EncyclopediaCharacterTableauWidget` 的属性构造方式（仓库并未提供完整可直接引用的百科 Hero VM 类源码），不能臆测可编译接口；
- 对不适合无侵入移植的书签、百科历史跳转、搜索条等功能不复制；另设“查看原版百科”按钮作为**附加功能**，不能用打开原版百科代替三级详情交付。

**三级点击行为**：首次选中 Hero 后进入三级；右下“安排联姻”打开**当前 Movie 中的条款浮层**；后退回二级必须仍保留 Clan 与候选英雄列表；Hero 意外结婚或转族时需要禁用“安排联姻”并告知原因。

### 2.5 条款浮层（三级上方覆盖，不能作为第四个完整页面）

**字段/控件**：
- 已选双方 Hero、双方 Clan、旗帜/小头像（只读）。
- 方向单选：`FemaleJoinsMaleClan`（女性加入男性 Clan）或 `MaleJoinsFemaleClan`（男性加入女性 Clan/入赘）；明确展示“最终哪位 Hero 离开哪个 Clan、加入哪个 Clan”。
- 礼金三态：`None` / `PlayerPays` / `OtherClanPays`；非零模式显示非负整数金额输入，单位第纳尔；展示资金付款方/收款方。
- 说明：提议发送后由接收 Clan 评估，不是立即结婚；7 天答复，允许至多一次反提议；礼金按后台婚姻执行流程结算，不在发请求时扣款。
- 操作：`取消`（仅关浮层）、`确认发出提议`（二次确认后业务提交），避免一击误发。

**条件与禁用**：
- 只有当前方向和当前礼金参数通过**正式同一套服务**的 `CanCreateMarriageProposal(draft)`，才能启用“确认发出”。在改变方向、礼金方向、金额时重新验证，显示准确的 `FamilyReasonCode` 提示。
- 禁止领袖非法离族；不会把无效方向硬编码为允许；已婚、年龄、亲属/养亲、王国/战争、锁定、冷却等由已有服务决定。
- 付款方余额可在 UI 提前提示，但“当前金币余额”不等于最终执行成功的承诺；输入金额必须 `int.TryParse` + 有界范围，不得抛 `int.Parse` 异常。
- 提交前再次用当前实时 `Hero/Clan` 建 Draft，调用 `CanCreateMarriageProposal`，再调用 `TryCreateMarriageProposal`；以真实 `FamilyResult` 为准。
- `TryCreateMarriageProposal` 成功：提示“联姻提议已发送（等待对方答复）”，关闭条款和联姻 Movie；失败：停留当前页，显示原因，**绝不可默认为成功**。
- 防双击：提交中立即置 `IsSubmitting` / 禁用按钮，调用结束后恢复/关闭；一个点击至多创建一条提议。

---

## 3. 单 Movie / ViewModel 数据契约（多 Worker 必须遵循）

### 3.1 状态图

```text
Open(ownHero)
    |
    v
ClanList  --SelectClan--> ClanDetail --SelectHero--> HeroDetail
    ^                          |   ^                      |
    |                          |   |                      |
    +------Back----------------+   +------Back------------+
                                                      |
                                            ExecuteArrangeMarriage
                                                      v
                                          TermsOverlay (same Movie)
                                       /                       \
                                   Cancel                Validate + Confirm
                                     |                          |
                                 HeroDetail          TryCreateMarriageProposal
                                                                |
                                    Success -> ShowResult + Close movie
                                    Failure -> ErrorText + remain in overlay
```

推荐定义 `enum MarriageUiPage { ClanList, ClanDetail, HeroDetail }`，`TermsOverlay` 独立布尔状态，避免第四级页面。`IsClanListVisible/IsClanDetailVisible/IsHeroDetailVisible/IsTermsOpen` 必须统一由根 VM 的页状态派生，并触发 `OnPropertyChanged`；不要由不同子 VM 各自操作窗口资源。

### 3.2 根 VM：`MarriageNavigationVM`

建议新建 `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageNavigationVM.cs`，基类 `TaleWorlds.Library.ViewModel`：

```csharp
// 字段名是 Worker A/B/C/D 的约定；实现时补通知、null 和权限检查。
internal sealed class MarriageNavigationVM : ViewModel
{
    internal Hero OwnHero { get; }
    internal Clan SelectedTargetClan { get; private set; }
    internal Hero SelectedTargetHero { get; private set; }

    [DataSourceProperty] public MBBindingList<MarriageClanItemVM> Clans { get; }
    [DataSourceProperty] public MarriageClanDetailsVM ClanDetails { get; private set; }
    [DataSourceProperty] public MarriageHeroDetailVM HeroDetails { get; private set; }
    [DataSourceProperty] public MarriageTermsVM Terms { get; private set; }

    [DataSourceProperty] public bool IsClanListVisible { get; private set; }
    [DataSourceProperty] public bool IsClanDetailVisible { get; private set; }
    [DataSourceProperty] public bool IsHeroDetailVisible { get; private set; }
    [DataSourceProperty] public bool IsTermsOpen { get; private set; }
    [DataSourceProperty] public string BreadcrumbText { get; private set; }
    [DataSourceProperty] public string SearchText { get; set; }
    [DataSourceProperty] public string EmptyStateText { get; private set; }

    [DataSourceMethod] public void ExecuteBack() { /* 依导航状态退回 */ }
    [DataSourceMethod] public void ExecuteClose() { FamilyActionScreenHost.Close(); }
    [DataSourceMethod] public void ExecuteArrangeMarriage() { /* 打开 Terms */ }
    // 每个列表 ItemVM ExecuteSelect -> 回调根 VM 的 SelectClan/SelectHero。
}
```

`[DataSourceProperty]` 和 `[DataSourceMethod]` 均按照当前仓库的 UIExtender/Gauntlet 用法；部分 VM 内部构造顺序与访问修饰符可以细化，但**以上绑定名在合并前不得擅自改动**。

**子 VM 合同**：

| 类型 | 必需属性 | 必需动作 / 说明 |
|---|---|---|
| `MarriageClanItemVM`（现有，扩展） | `Name`, `Banner`, `KingdomName`, `CultureName`, `RelationText`, `CandidateCountText`, `MatchTierText` | `ExecuteSelect` 回调根 VM，不能创建 Movie |
| `MarriageClanDetailsVM`（新增） | `Name`, `Banner`, `KingdomName`, `CultureName`, `LeaderName`, `TierText`, `RelationText`, `DescriptionText`, `EligibleHeroes` | 初始化来自选中 `Clan`，候选只来自统一筛选 |
| `MarriageHeroItemVM`（现有，扩展） | `Name`, `Visual`, `AgeText`, `RoleText`, `IsSelected` | `ExecuteSelect` 回调根 VM |
| `MarriageHeroDetailVM`（新增） | `NameText`, `HeroCharacter`, `Traits`, `Skills`, `Stats`, `Allies`, `Enemies`, `Family`, `DescriptionText` 及图片渲染必需字段 | 由第三页 Worker 对照本体百科 XML 补齐每个 Widget 的属性及命令；没有时为空集/禁用 |
| `MarriageTermsVM`（新增） | `Direction`, `GiftMode`, `GiftAmountText`, `PaymentSummaryText`, `ValidationReasonText`, `CanSubmit`, `IsSubmitting` | `ExecuteConfirm`, `ExecuteCancel`, `ExecuteSubmit`；只能经 `FamilyProposalService` 写入 |

> 注意：`HeroCharacter` 内部属性约定应以当前版本百科 `EncyclopediaCharacterTableauWidget` 的真实数据源为准。上表描述的是界面需要的信息，不是对未核实游戏 DLL 构造函数的断言。Worker C 必须提交自己的精确绑定表。

### 3.3 Prefab 页面组成

推荐**保留一个已注册 Movie**：`ModifiedFamilyMarriageShell`。顶层 `<Prefab><Window>...` 包含共同外框/标题/导航和三个页容器：

```xml
<!-- 示意片段；不是可直接粘贴编译的完整 XML。 -->
<Widget IsVisible="@IsClanListVisible">
  <!-- Clan 列表；DataSource={Clans} 的 ItemTemplate -->
</Widget>
<Widget IsVisible="@IsClanDetailVisible" DataSource="{ClanDetails}">
  <!-- Clan 百科式详情和 EligibleHeroes 网格 -->
</Widget>
<Widget IsVisible="@IsHeroDetailVisible" DataSource="{HeroDetails}">
  <!-- 本体 Hero 百科式 3D 模型、技能、亲属等 -->
</Widget>
<Widget IsVisible="@IsTermsOpen" DataSource="{Terms}">
  <!-- 盖在同一 Movie 上的条款层 -->
</Widget>
```

三页可按 `ModifiedFamilyMarriageClanList.xml` / `ModifiedFamilyMarriageClanDetail.xml` / `ModifiedFamilyMarriageHeroDetail.xml` / `ModifiedFamilyMarriageTerms.xml` 分拆为**子 Prefab**，前提是实际游戏资源系统已注册且 `<子Prefab/>` 经运行测试可引用；若遇到 Gauntlet 自定义 Prefab 注册/加载问题，**第一版允许将这四块直接内联在 Shell 内**，先确保功能与不崩溃，之后再抽文件。禁止用一堆独立 `LoadMovie()` 假装“三级导航”。

### 3.4 页面刷新和数据一致性

- 创建根 VM 时确定并只读保存 `OwnHero`；若玩家在外部改变已选 Hero 状态，提交前必须验证。
- Clan 筛选可首次建索引，进入第二级和第三级时重新检查目标有效性；不要每个 UI 属性 Getter 都遍历 `Clan.All`、调用 Service 或生成大图片。
- Clan/ Hero 列表项只存 `Clan` / `Hero` 引用和只读显示 VM，禁止把 UI 所需的排序层级、缓存当作存档。
- `SelectClan` 必须清空 `SelectedTargetHero`，关闭当前条款，重建详情 VM。
- `SelectHero` 必须先验证人物仍属于选定 Clan 且存在至少一个合法方向；失败则提示并刷新列表，禁止显示已失效人物的“安排联姻”。
- 进入/退出详情时，旧 VM 订阅事件若有必须解除；3D 人物资源必须有清理路径。不要频繁使用未释放的模型实例。
- 只有 `TryCreateMarriageProposal` 有权写提议；`CanCreateMarriageProposal` 不得导致锁定、扣款或状态改变（当前代码提供该无副作用入口）。

---

## 4. 文件级实施清单（直接照此分配）

| 路径 | 操作 | 责任 | 完成标准 |
|---|---|---|---|
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageScreenService.cs` | 修改 | Worker B | 从 `FamilyActionScreenVM` 切换到 `MarriageNavigationVM`；仍只 `FamilyActionScreenHost.Open()` 一次 |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageNavigationVM.cs` | 新增 | Worker B | 三页状态、回退、面包屑、筛选、选中 Clan/Hero、条款层生命周期 |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageSelectionVM.cs` | 重构 | Worker B | 重用/迁移现有筛选及 `MarriageClanItemVM` / `MarriageHeroItemVM`，不得维护第二套独立选人流程 |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageClanDetailsVM.cs` | 新增 | Worker B | Clan 百科信息和已筛选候选列表；每个字段可绑定 |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageHeroDetailVM.cs` | 新增 | Worker C | Hero 3D、技能、性格、家族及关系数据，与 XML 一一对应 |
| `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageTermsVM.cs` | 新增 | Worker D | 方向、礼金、校验/原因、二次确认、发请求、防重复 |
| `ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageShell.xml` | 修改 | Worker A | 本体百科式外框、顶部导航、三级页容器、条款浮层、滚动、关闭 |
| `ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageManagement.xml` | 参考/整理 | Worker A | 迁移可用 Clan Banner、Hero 头像、Scroller 结构；不能成为第二个正在打开的联姻 Movie |
| `ModifiedFamily/ModifiedFamily/ModifiedFamily.csproj` | 修改 | Worker E | 若新增独立子 Prefab，全部正确 Include / 链接并部署；原 Shell 不能漏 |
| `ModifiedFamily/ModifiedFamily/ModuleData/Languages/CNs/modifiedfamily-zho-CN.xml` | 修改 | Worker D（或 E） | 新页面、字段、错误原因全部有本地化键 |
| `ModifiedFamily/ModifiedFamily/UI/Clan/ClanManagementVMMixin.cs` | 原则上不改 | Worker E 审核 | 已选本人传参正确，入口显示资格正确，失败可提示 |
| `ModifiedFamily/ModifiedFamily/UI/FamilyActionScreenHost.cs` | 原则上不改 | Worker E 审核 | 同 Movie 实现、不泄漏焦点，连续反复开关正常 |
| `ModifiedFamily/ModifiedFamily/UI/FamilyPlayerInteractionService.cs` | 少量兼容修整 | Worker D | 新页面不再弹旧选人 Inquiry；旧收养/提议收件箱逻辑不回归 |
| `ModifiedFamily/ModifiedFamily/Proposals/FamilyProposalService.cs` | **禁止无理由变更** | 仅 Worker E 审核 | 新页面调用现成 `CanCreate...` 和 `TryCreate...`，不复制后端状态机 |

> 若团队只有单 Worker，则按下文阶段顺序实施；多人并行前必须由 Worker B 发布 `MarriageNavigationVM`/子 VM 属性命名合同，由 Worker A 冻结 XML 数据源命名，避免相互不一致。

### 4.1 服务接入精确要求

在 `MarriageScreenService.cs` 中用下面结构替换旧 VM（**代码骨架**）：

```csharp
public static bool Open(Hero hero)
{
    if (hero == null || hero.Clan != Clan.PlayerClan)
        return false;

    // 增加当前已存在的资格检查；服务内亦会最终重新验证。
    return FamilyActionScreenHost.Open(
        "ModifiedFamilyMarriageShell",
        "ModifiedFamilyMarriageLayer",
        new MarriageNavigationVM(hero));
}
```

页面跳转操作**绝不可**写成：

```csharp
FamilyActionScreenHost.Close();
FamilyActionScreenHost.Open("...ClanDetail...", "...", new ...());
```

因为这会销毁当前选择、重绑输入焦点、潜在触发关闭事件，且难以保持三级返回状态。

### 4.2 可联姻过滤示意

```csharp
bool CanShowTarget(Hero own, Hero target, FamilyProposalService service)
{
    if (own == null || target == null || service == null) return false;
    if (target.IsFemale == own.IsFemale) return false;
    if (!MarriageEligibilityModel.CanNegotiateHero(target).IsAllowed) return false;

    foreach (MarriageDirection direction in new[]
    {
        MarriageDirection.FemaleJoinsMaleClan,
        MarriageDirection.MaleJoinsFemaleClan
    })
    {
        var draft = new MarriageProposalDraft
        {
            ProposerClan = Clan.PlayerClan,
            ReceiverClan = target.Clan,
            HeroA = own,
            HeroB = target,
            Direction = direction,
            GiftAmount = 0
        };
        if (service.CanCreateMarriageProposal(draft).IsAllowed) return true;
    }
    return false;
}
```

**这是筛选示意，不代表必须新增同名函数**。当前 `MarriageSelectionVM.IsValidCandidate` 已含类似逻辑，优先迁移/复用它；明确每次正式提交仍需完整 Draft 校验。`CultureName` 必须取 `Clan.Culture.Name`，不能把一个 Hero 的出生文化误当成 Clan 的制度/文化。

### 4.3 礼金提交骨架及结果

```csharp
// MarriageTermsVM 统一负责，不调用 MarriageAction 或 GiveGoldAction。
private FamilyResult SubmitOnce()
{
    if (IsSubmitting) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
    IsSubmitting = true;
    try
    {
        // BuildDraft() 把当前双方、Direction、付款 Clan、金额组装完整。
        MarriageProposalDraft draft = BuildDraft();
        FamilyResult check = _service.CanCreateMarriageProposal(draft);
        if (!check.IsAllowed) return check;
        return _service.TryCreateMarriageProposal(draft);
    }
    finally { IsSubmitting = false; }
}
```

要把 `FamilyReasonCode` 映射为清晰、可本地化的错误文本：`HeroLocked`、`PairCooldown`、`AtWar`、`CultureForbidden`、`LeaderCannotLeave`、`InvalidGift`、`InsufficientFunds`、`AlreadyMarried` 等；财务不足类的提示应与服务实际返回一致，不得用占位文本把其他失败统一当成资金不足。

---

## 5. 多 Worker 分工协议与交接格式

### Worker A：Gauntlet XML 与本体视觉复刻

**输入**：本体 `EncyclopediaBar.xml`、`EncyclopediaClanPage.xml`、`EncyclopediaHeroPage.xml`；`MarriageNavigationVM` 固定数据源合同。  
**只负责**：Shell 顶层布局、三个页面容器和条款浮层 XML，Banner/头像/Scrollbar/背景/边框/标题/可见性绑定。  
**不负责**：联姻业务判断、发请求、AI 分数。  
**输出**：可解析 XML，`Movie=ModifiedFamilyMarriageShell`，绑定审计表“XML 表达式 → 归属 VM → 数据类型”，以及一级/二级/三级运行截图。  
**完成定义**：第三页不是空框、三个页面显示不重叠，16:9 UI 缩放下无文字和按钮遮挡。

### Worker B：导航 VM / 候选检索

**输入**：本任务书 2/3/4 节及现有 `MarriageSelectionVM.cs`。  
**只负责**：`MarriageNavigationVM`、`MarriageClanDetailsVM`、重构 Clan/Hero ItemVM、筛选、排序、状态、返回。  
**输出**：编译通过的 VM，候选筛选复用 `CanCreateMarriageProposal`，VM 字段列表及页面切换测试结果。  
**完成定义**：进出三级不会丢 `OwnHero`，新选 Clan 会清旧 Hero，关闭 UI 不改变后台状态。

### Worker C：Hero 百科信息页

**输入**：原版 `EncyclopediaHeroPage.xml`、本机当前版本 `TaleWorlds.CampaignSystem.ViewModelCollection.dll` 及其他相关 DLL。  
**只负责**：`MarriageHeroDetailVM`、3D 模型与 Traits、Skills、Stats、Allies、Enemies、Family 的实测绑定；按原版可见性规则处理信息隐藏。  
**输出**：XML-VM 逐项绑定对照表，三级页人物模型截图、缺失数据时回退方案。  
**完成定义**：目标 Hero 更换时人物模型/数据确实切换；无人物模型或没有亲属时不崩溃。

### Worker D：条款面板、业务接入、本地化

**输入**：已确认选中的 `OwnHero`/`TargetHero`；现有 `FamilyProposalService`。  
**只负责**：`MarriageTermsVM`、两种婚姻方向、三种礼金模式、数值输入、当前校验原因、二次确认与结果提示；新增中文文本键。  
**输出**：从第三级成功发出一条真实提议的日志、失败留在本页的截图。  
**完成定义**：成功只创建 Pending 提议，不立即结婚/扣款；同一双击不出现两条请求。

### Worker E：集成、资源部署与 QA

**输入**：A/B/C/D 的合并成果。  
**只负责**：`MarriageScreenService` 最终接线、`csproj` XML 内容配置、游戏安装目录验证、UI 生命周期、回归测试和验收记录。  
**输出**：编译/运行日志、手动验收 checklist、低分辨率和长存档截图。  
**完成定义**：游戏无异常崩溃，原版 Clan 界面保持正常，现有收养按钮/通知/存档不退化。

**交接门槛**：A/B 先冻结根 VM 属性名和 Movie 名称；C/D 只能基于已冻结字段实施；E 才负责最终集成。多个 Worker 不得同时改 `ModifiedFamilyMarriageShell.xml` 和 `MarriageNavigationVM.cs`，除非已约定文件所有者。

每个 Worker 交付说明必须包含：**改动文件、目的、完成/未完成、已执行测试、截图或日志、剩余风险、需下一位 Worker 核对项**；禁止仅提交“已完成 UI”。

---

## 6. 分阶段实施与最小可验收里程碑

| 阶段 | 具体操作 | 完成后必须现场验证 |
|---|---|---|
| **M1：空框变列表** | 入口改 `MarriageNavigationVM`；Shell 内添加一级 Clan 列表；用现有 `BannerImageIdentifierVM` | 点击“联姻”能见家族名、旗帜、候选数；无候选时有说明；可关闭重开 |
| **M2：二级 Clan** | 新建 ClanDetailsVM；显示王国/文化/Relation/领袖/成员；绑定 Hero 缩略图 | 从一级点 Clan 进入二级，点击返回能再次选择其他 Clan |
| **M3：三级 Hero** | 从原版 Hero 百科提炼 Widget 与数据源；人物 3D、技能、特性、关系；验证 null 情况 | 从二级点 Hero 进入三级，模型与技能确实属于目标 Hero，返回二级正常 |
| **M4：条款** | 第三级浮层、方向/礼金/校验、提交、二次确认 | 一份提议落地为 Pending；失败不离开页面；成功显示已发送 |
| **M5：交互细化** | 搜索/筛选、面包屑、空状态、危险场景提示、UI 缩放、图标/文本打磨 | 在常见分辨率及异常人物状态下正常使用 |
| **M6：长期回归** | 连续进出、重开、保存加载、跨天、死亡/转 Clan/战争后重检 | 原存档可进入；无新崩溃/输入卡死/重复提交 |

**先做 M1 再做 M3。** 此前已经发生过 UI 崩溃/空白的问题，不允许第一批合入时一次性复制全部百科详情组件导致无法排查。

---

## 7. 验收用例（须逐项勾选并附日志）

### 7.1 正常流程

- [ ] 玩家家族选中符合资格的未婚成员，点击“联姻”，弹出带 Clan 列表的百科皮肤窗口（不是空白）。
- [ ] 第一级旗帜、家族名称、所属王国、**Clan 文化**、关系值、合法候选人数均真实，且与所选己方 Hero 关联。
- [ ] 第一级同国同文化、异国同文化、同国异文化按规则排序；不出现异国异文化或敌国不合法 Clan。
- [ ] 第一级按名称搜索/文化筛选，结果正确；零结果显示说明。
- [ ] 第二级看到 Clan 简介、领袖、等级、旗帜以及合法候选 Hero，不混入已婚/不符合规则的人物。
- [ ] 第三级包含原版百科式人物 3D 视觉、技能、特性、关系/家族信息；从 Hero A 切换到 B 时没有 A 的旧资料。
- [ ] 返回三级→二级→一级与点击路径导航一致；始终是同一个 Movie。
- [ ] 第三级条款可选正常嫁娶或入赘，显示**哪位 Hero 去哪一个 Clan**。
- [ ] 礼金支持无、己方支付、请求对方支付，非负整数输入和合法性校验。
- [ ] 提交后产生且仅产生一个 `PendingResponse` 联姻提议，Hero 锁定生效；玩家看到“已发送，等待答复”，没有直接结婚或立刻扣款。

### 7.2 异常与回归

- [ ] 进入二级后某 Hero 已被其他提议锁定，页面刷新后禁用/删除此候选或给予准确原因。
- [ ] 进入三级期间目标 Hero 结婚、死亡或 Clan 改变，提交被安全阻止。
- [ ] 双方王国开战或文化/王国关系变化使婚约非法，提交被安全阻止，理由可读。
- [ ] 目标 Clan 没有 `EncyclopediaText` 时仍可正常显示基本介绍。
- [ ] Hero 无亲属或关系数据时第三页不崩溃。
- [ ] 任何一级按 Esc / 关闭按钮回到 Clan 页面，输入焦点和原版家族页面正常。
- [ ] 连续打开/关闭 30 次、在一级二级三级间切换至少 100 次，Movie/Layer 数量不累积。
- [ ] 在 1920×1080、2560×1440 与截图对应的较小窗口尺寸（约 1649×928）检查可读性；另测 UI 缩放（如 80%、100%、125%，以游戏实际支持项为准）。
- [ ] 原有“收养”按钮、家族 4 个标签、收件箱通知、原版百科入口不退化。
- [ ] 旧存档与新档可正常加载；不增加不必要的 `SaveDefiner` 字段；游戏内保存加载后再次打开联姻 UI 正常。

### 7.3 崩溃排查矩阵

| 症状 | 第一检查点 | 具体措施 |
|---|---|---|
| 仍只有空白百科边框 | `MarriageScreenService.Open` 是否仍传 `FamilyActionScreenVM(hero,true,null)`；Shell 是否无页内容 | 确认当前实际加载 Movie 与 VM |
| 点击“联姻”直接崩溃 | Shell XML 解析、未找到 Sprite/Brush、自定义 Widget/Prefab 未注册 | 最小化为一级静态文本再逐块添加，查完整异常堆栈 |
| Clan 列表空但应有候选 | `CanCreateMarriageProposal`、`OwnHero`、锁定/冷却、`Clan.Culture`、后台是否已初始化 | 把每个过滤阶段数量写 Debug 日志，不强行显示非法候选 |
| 二级 Clan 显示错旗/错资料 | 选中 VM 不刷新、`DataSource="{...}"` 层级错误 | 对照 `BannerImageIdentifierVM` 的 `Id/AdditionalArgs/TextureProviderName` |
| 三级打开崩溃 | `EncyclopediaCharacterTableauWidget` 数据源字段/人物资源/百科 VM 私有绑定 | 删去未接通 Widget，逐项验证 `HeroCharacter` 与必要属性 |
| 关闭后 UI 黑屏/无法点击 | `FamilyActionScreenHost.Close` 没释放 Layer/Movie/焦点；嵌套叠层 | 一次 Open 对一次 Close，只有一个全局 Host |
| 点击“发起联姻”自动完成婚姻 | UI 绕开 `TryCreateMarriageProposal` 直接调用 Action | 移除 Action 调用；请求应为 `PendingResponse` |
| 同一次按钮出现多个请求 | 双击、UI 重入、未禁用提交按钮 | `IsSubmitting` + 服务最终验证 + 成功后立即关闭 |

日志至少记录：页面切换 `[ClanList/ClanDetail/HeroDetail]`、当前 `OwnHero/TargetClan/TargetHero` 标识（游戏对象 ID/名称）、候选过滤数量、合法性原因、Movie 打开/关闭、提议提交结果；Debug 层级可控，默认不要刷屏。

---

## 8. 禁止事项与最终交付验收定义

### 8.1 严禁

1. **严禁**直接复制整份原版 `EncyclopediaHeroPage.xml` 却不提供其完整 VM 所需字段，造成大量空绑定与崩溃。
2. **严禁**在点击 Clan/Hero 后使用新的 `GauntletLayer` 或 `LoadMovie` 堆叠第二、第三层弹窗。
3. **严禁**重写原版 `MarriageAction.Apply`、变更婚姻状态机或绕过既有 `FamilyProposalService`。
4. **严禁**把所有 Clan/Hero 无条件列出来，然后在最后一步才发现根本不合法。
5. **严禁**用 `int.Parse()` 处理外部输入而不捕获异常；用 `TryParse()` 并在校验失败时留在原页。
6. **严禁**因为“先能显示再说”就让第三页完全跳到本体百科而无法回到联姻流程；“查看原版百科”只能是可选附加入口。
7. **严禁**同时维护旧 Inquiry 选人流与新三级选人流造成两套互相不一致的过滤规则。
8. **严禁**把“已经能编译”或“XML 有效”当作实机验收完成。

### 8.2 最终 Worker 交付格式

项目管理者应收到：

- 变更文件清单（新增/修改/删除，逐条附目的）。
- 三级最终游戏内截图各 1 张，另附条款浮层 1 张。
- XML → VM 绑定检查表，尤其是 Hero 模型/Traits/Skills/关系。
- `dotnet build`/项目实际编译结果 + 游戏版本 + 资源部署列表。
- 一条实际成功创建的提议的后台日志（`ProposalId`、`PendingResponse`；不含支付动作）。
- 7.1～7.2 验收清单的通过/失败状态；尚未解决的崩溃堆栈与复现步骤。
- 有冲突时以**现有 `FamilyProposalService` 的权威校验**为准，不能为了 UI 效果改变业务事实；差异需明确写入交接说明。

**完成定义（Definition of Done）**：玩家从原版 Clan 页面点击“联姻”，看到设计中的百科式三级页面，能查看 Clan 和 Hero 的真实信息、返回更换目标、设置合法的联姻方向及礼金，并且安全地创建一条待答复提议；重复操作和复杂边界情况下没有崩溃或状态混乱。

---

## 9. 对接须知：已确认事实与实施前核查项

**已确认（仓库直接支持）**：当前空白 Shell + 旧 VM 的入口连线、`MarriageSelectionVM` 中的候选过滤原型、`FamilyProposalService.CanCreateMarriageProposal` 与 `TryCreateMarriageProposal`、原版百科与家族 XML 的布局、UIExtender 已启用、csproj 仅显式包含部分 XML。

**实施前必须实测/核查（不要在文档中假定已经工作）**：

- 游戏实际版本中百科 Hero VM / `EncyclopediaCharacterTableauWidget` 的公开属性和构造/释放路径。
- 自定义子 Prefab 是否能被模块的 XML 资源注册方案稳定发现；失败则内联内容。
- 当前 game build 对 `TextWidget`、`EditableTextWidget` 的输入绑定细节、ScrollBar 相对路径和人物模型 Tableaus 的性能。
- UI 按钮实际部署后，在玩家不同 Clan、文化、关系、战况和同伴状态下是否符合后端规则。
- 本体百科的资料可见性机制，严禁借移植绕过尚未发现 Hero 的信息隐藏。

**执行顺序建议**：Worker B 发布固定 VM 合同 → Worker A 验证 M1 Shell+Clan 列表 → Worker B 交付 M2 → Worker C 独立完成 M3 → Worker D 交付条款 → Worker E 合并验收。仅在所有阶段通过后再清理旧原型，避免中途移除可用代码导致回归。
