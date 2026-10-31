# ModifiedFamily 联姻 UI Sprite 资源优化：Worker 实施任务书

> **用途**：交给 Worker 直接执行的代码/美术资源优化任务。  
> **仓库**：`GuihuaZhou/bannerlord`（`ModifiedFamily` 模块）。  
> **文档日期**：2026-10-31。  
> **状态**：实施建议，不代表已修改仓库或已证明卡顿根因。  
> **原则**：尽量小的改动；不改联姻玩法、不改存档结构、不降低实际界面视觉清晰度。

## 0. Worker 执行指令（可直接复制）

请基于仓库 `GuihuaZhou/bannerlord` 当前工作分支执行下列任务：**优化 `ModifiedFamily` 自定义 Gauntlet UI Sprite 的过大源图及图集占用**。保持现有 `ui_familyproposal` 单一 Category、现有 Sprite 名称及 XML 布局、现有手动 `Load/Unload` 生命周期；只修改确有必要的图片资源与重建生成物。先建立性能/画质基线，再根据下表制作缩放候选图，使用 TaleWorlds 官方工具重新生成和导入 Sprite Sheet，做 UI 与战斗 A/B 验证。若 2048×2048 无法打包或影响画质，优先保留画质与正确性，不得强制减质；报告实际结果。不得通过仅修改 `SpriteData.xml` 宽高冒充资源优化。除非独立定位性能问题，否则不要调整 AI、招募、经济或游戏战斗逻辑。提交修改、构建与测试证据，并总结是否发现与卡顿有关的因果证据。

## 1. 事实基线（2026-10-31 审查时）

以下来自仓库源码，不是用户 GPU 的实时测量。开始任务后应再次以最新工作树核验：

1. `ModifiedFamily/GUI/ModifiedFamilySpriteData.xml` 当前声明一个分类 `ui_familyproposal`，一张 **4096 × 4096** 图集，包含 6 个 `GenericSprite`。
2. 源素材位于 `ModifiedFamily/GUI/SpriteParts/ui_familyproposal/`；生成的合并 PNG 位于 `ModifiedFamily/AssetSources/GauntletUI/ui_familyproposal_1.png`；导入资源元数据位于 `ModifiedFamily/Assets/GauntletUI/ui_familyproposal_1_tex.tpac`。上述内容为一条资源生成链，**不能只更新其中一个文件**。
3. `ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageShell.xml`：主背景实际显示 `1515×919`，顶部 `marriage_proposal_title_betrothal` 实际显示 `480×160`。
4. `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageScreenService.cs` 使用 `FamilyActionScreenHost.Open(..., "ui_familyproposal")` 打开主联姻选择页。
5. `ModifiedFamily/ModifiedFamily/UI/FamilyActionScreenHost.cs` 在打开时调用 `SpriteCategory.Load()`，关闭时释放 Movie、移除 Layer、`SpriteCategory.Unload()`、Finalize ViewModel；当前代码并非显式 AlwaysLoad 模式。
6. `ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageProposalScreenService.cs` 打开收到的联姻请求页时没有传入 `ui_familyproposal`；其 `ModifiedFamilyMarriageProposal.xml` 目前主要引用 **游戏原生 Brush / Sprite**，而非上述自定义图集。**不要假设所有联姻弹窗都加载这张 4096 图集。** 若后续实际使用了自定义 Sprite，应另行验证分类加载。
7. 主选择页还使用 `EncyclopediaCharacterTableauWidget`、`ImageIdentifierWidget` 和动态人物展示，这些与静态 Sprite 的成本不同。必要时进行独立诊断，不要把它们混为一谈。

### 当前图集明细与候选尺寸

| Sprite 名称（严格保留大小写） | 当前 PNG 尺寸 | 第一轮候选尺寸 | 说明 |
|---|---:|---:|---|
| `marriage_proposal_background` | 1515×919 | **1515×919（保留）** | 占据整个主面板，优先保证细金边与纹理清晰 |
| `marriageProposal_conditions_parchment` | 1774×887 | **1024×512** | 保持 2:1 比例；逐项核对羊皮纸纤维及柔和边缘 |
| `marriage_proposal_title_betrothal` | 2172×724 | **768×256** | 3:1；当前显示 480×160，保留 1.6 倍像素余量 |
| `marriage_proposal_title_main` | 2172×724 | **768×256** | 同系列保持一致 |
| `marriage_proposal_title_section` | 2172×724 | **768×256** | 同系列保持一致 |
| `marriage_proposal_rings_icon` | 1254×1254 | **256×256** | 先搜索实际显示尺寸；若放大显示导致失真则提高至 384/512 |

> **尺寸只是候选，不是强制指标。** 对于高分辨率显示器或 UI 缩放比例提高的情况，可以上调为 960×320 等，并以清晰度验收为准。未被界面引用的 Sprite 也不能随意删除，需检查其他 XML 和代码引用。

### 为什么不把图片拆成多个图集？

同一联姻主界面使用的元素共享加载时机，集中在 `ui_familyproposal` 是合理的。官方文档推荐按**加载生命周期**划分类别，而非按单张图片拆分。只有证实某些素材属于不同页面、长期不被需要且有独立加载/卸载收益时，才提交单独的 Category 拆分方案；**本任务默认不拆**。

## 2. 修改范围与步骤

### P0 — 建立基线、检查引用（必须先做）

- [ ] `git status` 确保不覆盖用户未提交的工作；记录 `git rev-parse HEAD` 和当前分支。新建独立工作分支或按 Worker 的正常工作流提交；不得对主分支直接覆盖未经确认的修改。
- [ ] 检查上述 6 个 PNG 的像素、Alpha 通道、实际美术边界（是否有多余透明留白）及所有引用位置。重点扫描 `ModifiedFamily/GUI/Prefabs/`、`GUI/Brushes/`（若存在）和代码中的 Sprite 字符串。
- [ ] 记录原始与目标的图片尺寸、PNG 文件大小、生成图集尺寸、导入资产，以及与运行时纹理格式有关的信息（格式可读时记录；不可读时标记“未知”）。
- [ ] 在当前版本采集画质基线截图：**联姻选择页**、候选列表、家族详情、条件面板、顶部标题；同时测试**收到其他家族联姻请求**页面以确认它不是错误的资源加载路径。
- [ ] 尽可能在固定硬件/分辨率/UI 缩放/画质设置下记录：首次打开耗时、重复打开耗时、关闭后的显存占用、战役地图帧时间和标准战斗帧时间（至少三次重复）。没有可靠采集工具则注明未测，不得编造数据。

### P1 — 仅优化图片源文件（优先）

- [ ] 先在工作目录中生成候选图片；保留原稿可追溯性，不直接破坏唯一源图。
- [ ] 对过大的 Sprite 使用高质量缩放（优先 Lanczos 等优质重采样）；保证金属细线、四角花纹、戒指形状、羊皮纸边缘依旧清楚。
- [ ] **保留透明通道**，检查内部镂空和外部四周必须透明；边缘不得出现黑色/红黄杂色光晕；注意透明像素的颜色和 Alpha 重采样可能造成的边缘污染。
- [ ] 保持原来的美术风格、配色、构图、边框厚度相对比例，不新增描边、阴影、文字、按钮和不需要的底色。
- [ ] 对有明显大面积无用透明留白的 Sprite，先在不破坏 UI 对齐/锚点的前提下论证是否裁切；**不能直接裁切后不修正布局**。
- [ ] 图片尺寸优先用上表候选值；在实际页面（含目标分辨率及 UI 比例）对比锐度，必要时回调到更高尺寸。
- [ ] **不要把 PNG 文件大小当作 VRAM 占用**；PNG 无损压缩率和 GPU 实际导入格式不是同一个概念。

### P2 — 按官方流程重新生成图集、导入 TPAC

- [ ] 使用 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`，输入放在 `ModifiedFamily/GUI/SpriteParts/ui_familyproposal/` 的优化源图。
- [ ] 由工具重新生成 `ModifiedFamily/GUI/ModifiedFamilySpriteData.xml`、`ModifiedFamily/AssetSources/GauntletUI/ui_familyproposal_1.png`（以工具实际产物为准）。不手工指定 `SheetX/SheetY` 等布局坐标。
- [ ] 用与游戏版本对应的 **Bannerlord Modding Kit** 启动资源浏览器，在 `GauntletUI` 中 `Scan new asset files` 并导入 `ui_familyproposal`，更新 `ModifiedFamily/Assets/GauntletUI/ui_familyproposal_1_tex.tpac` 及工具生成的实际所需资源文件。
- [ ] **优先尝试一张 2048×2048 的图集**。是否成功由官方工具的打包结果决定；若放不下，先调整源图留白和尺寸方案，不能为达成 2048 而损坏画质。如果必须保留 4096，记录原因。
- [ ] 确认 6 个 Sprite 名称完全不变（尤其 `marriageProposal_conditions_parchment` 的大小写），没有重复名称、丢图、空白、UV 错位或资源浏览器报错。
- [ ] 编译并以游戏客户端启动验证。不得只提交 PNG、不提交所需生成资源；也不得只手动编辑 SpriteData.xml 而不重建图集/导入资产。

### P3 — 保持加载机制，验证资源生命周期

- [ ] 以现有 `FamilyActionScreenHost.Open/Close` 手动加载释放机制为默认方案；**本次不加 `AlwaysLoad`**，也不拆分 Category。
- [ ] 检查异常打开失败、顶部 Screen 被移除、正常关闭、连续重复打开等路径，确认 Movie/Layer/Category/ViewModel 仅被加载一次、相应释放一次；检查失败路径是否会重复卸载或静默残留。
- [ ] 关注 `LoadOptionalSpriteCategory` 的资源不可用提示：当前即使加载失败，XML 对自定义 Sprite 仍有引用。先验证实际加载是否成功，若要修复失败兜底，必须另外说明原因及范围，避免顺手重写界面系统。
- [ ] 若没有实测泄漏，不要借本次优化引入缓存池、常驻大图、全局静态纹理引用、周期性 Reload 或不受控延迟卸载。

### P4 — 可选的诊断实验，不纳入默认生产代码

只有在无法确认开销来源时，使用独立测试构建进行：

1. **A 版**：原图集/原 UI。**B 版**：缩小后的图集/同样 UI。比较首次打开、反复打开与关闭后的指标。
2. **C 版（诊断专用）**：保持 `ModifiedFamily` AI 与联姻行为不变，仅临时关闭/替换联姻主选择页自定义背景贴图，观察是否影响性能。不要让 C 版成为正式交付。
3. 如果怀疑 3D Hero 模型而非贴图，单独做 Tableau 显示/隐藏对照；不要同时改变人物和图集而无法区分原因。
4. 使用一致的游戏运行状态和战斗人数进行 A/B 比较。若没有进入 UI 也持续发生战斗卡顿，或关闭该类别后没有可重复改善，**不能归因于 Sprite**。同时保留 GPU 驱动/硬件及其他 Mod 高频代码为独立待查线索。

## 3. 必须遵守的边界（禁止事项）

- **禁止**拆成 6 个独立 Category／图集（除非经过测量并提出新的设计审批）。
- **禁止**统一将所有资源低清化；任何画质回退优先保留原尺寸。
- **禁止**新增 `<AlwaysLoad/>`，除非后续有明确需求、性能数据并经过单独决策。
- **禁止**修改玩家/AI 联姻判定、请求收发、接受拒绝协商、结算、存档/持久化结构、`SaveableTypeDefiner` ID 或其他 Mod 行为。
- **禁止**仅通过裁切预览图片、改 `SpriteData.xml` 数字、手动重命名 `Sprite` 来冒充优化成果。
- **禁止**在没有对照测试时声称“联姻贴图导致战斗卡顿”或声称“优化后一定解决战斗卡顿”。
- 不主动改动 `ModifiedArmy`、`ModifiedPolitics`、`ModifiedDiplomacy`、`ModifiedTroops`。

## 4. 视觉与功能验收

请提交截图或录屏及验收记录，至少覆盖：

| 验收项 | 通过条件 |
|---|---|
| Sprite 完整性 | 6 个名称未变、正确引用、无缺图和纹理坐标错位 |
| 画质一致性 | 主背景金属细线、标题雕花、羊皮纸纹理、戒指清晰；与基线相比无明显模糊、黑边和变色 |
| UI 布局 | 各页面尺寸、锚点、层级、内容显示与操作不变 |
| 资源加载 | 第一次进入与重复进入均正常，关闭后没有持续增加的资源占用趋势（以重复测量为准） |
| 联姻行为 | 选择我方 Hero → 目标家族 → 目标 Hero → 协商条款 → 提交，流程正常；收到请求的接受/拒绝/协商仍正常 |
| 图集与导入 | 若达到 2048×2048，工具生成的 SpriteData/合并图/导入资产一致；否则记录为什么保留更大尺寸 |
| 性能归因 | 提交 A/B 实测表；若改善不明显明确写“未能证实 Sprite 是卡顿根因” |
| 版本兼容 | 现有存档可读，未改存档架构；没有异常日志/崩溃 |

### 性能记录表（由 Worker 填写）

| 测试场景 | A 原始版 | B 优化版 | 变化/说明 |
|---|---|---|---|
| 首次打开联姻主界面耗时 (ms) | 待测 | 待测 |  |
| 第 2–10 次打开耗时 (ms，均值或 P95) | 待测 | 待测 |  |
| 关闭后专用 GPU 内存 (MiB) | 待测 | 待测 |  |
| 连续打开/关闭 20 次后显存/内存趋势 | 待测 | 待测 |  |
| 战役地图帧时间 P95 (ms) | 待测 | 待测 |  |
| 固定规模战斗帧时间 P95 (ms) | 待测 | 待测 |  |
| 图片视觉问题 | 待测 | 待测 |  |

> 记录测试方法、游戏版本、分辨率/UI 比例、Mod 加载列表、GPU 驱动版本、相同存档/战斗设定。结果存在明显波动时增加重复次数。不得用单次 FPS 截图代替性能归因。

## 5. 资源大小与性能预期（说明而非承诺）

- 原单张 **4096²** 图集，在 **RGBA8 未压缩** 的纯像素假设下约为 **64 MiB**；完整 mip 链理论约 **85 MiB**。
- 若工具产出 **2048²** 图集，在相同假设下约 **16 MiB**；完整 mip 链约 **21 MiB**。
- 以上不是当前游戏 TPAC 纹理的实际显存实测数字，导入格式、压缩、Mip 配置和渲染资源处理都会影响结果。磁盘上 PNG 的文件大小也不代表显存占用。
- 减少图集面积可能降低纹理内存需求与加载开销，但**对战斗 FPS 的改善未经证实**。高频 AI、Hero 3D Tableau、GPU 驱动异常和其他因素须依靠独立实验排查。

## 6. 交付物与回滚

**要求 Worker 在一个明确的变更集内交付：**

1. 实际修改过的源 PNG、经官方流程生成的 SpriteData / 图集 / 导入资源文件，以及必需的最小构建调整（如有）。
2. `git diff --stat`、变更文件清单、当前 Commit SHA、生成工具及游戏版本。
3. `before/after` UI 截图、图集尺寸/像素/资源大小对比表、性能测试原始记录与总结。
4. 说明有无采用 2048 图集，若未采用则写明原因；若测得画质下降则保留原始资源。
5. 不修改既有存档数据；回滚通过恢复优化前整套图集资源（源 PNG、合并图、SpriteData、TPAC），**不得只恢复其中之一**。

**完成定义**：界面功能与美术效果不倒退，资源生成链自洽，性能测量有前后对照。即使性能未改善，只要优化有效且画质合格，也应如实标记“资源优化完成，卡顿原因尚未确认”。

## 7. 官方文档与源码入口

**TaleWorlds 官方**：[Generating and Loading UI Sprite Sheets](https://moddocs.bannerlord.com/asset-management/generating_and_loading_ui_sprite_sheets/)

官方明确说明：Sprite 按 Category 一起加载/卸载；可用 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` 重建图集；在 Modding Kit 资源浏览器中导入；可选择手动 Load/Unload 或 AlwaysLoad，后者用常驻内存换更短加载时间。**官方并未因此断言某个单张 UI 图集会造成战斗持续掉帧。**

**GitHub 源码**：

- [ModifiedFamilySpriteData.xml](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/GUI/ModifiedFamilySpriteData.xml)
- [SpriteParts/ui_familyproposal](https://github.com/GuihuaZhou/bannerlord/tree/main/ModifiedFamily/GUI/SpriteParts/ui_familyproposal)
- [ModifiedFamilyMarriageShell.xml](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageShell.xml)
- [ModifiedFamilyMarriageProposal.xml](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/GUI/Prefabs/ModifiedFamilyMarriageProposal.xml)
- [FamilyActionScreenHost.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/UI/FamilyActionScreenHost.cs)
- [MarriageScreenService.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageScreenService.cs)
- [MarriageProposalScreenService.cs](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedFamily/ModifiedFamily/UI/Marriage/MarriageProposalScreenService.cs)

---

**Worker 最终汇报格式**：`完成事项 / 变更文件 / 原图集与新图集参数 / UI 清晰度截图 / 加载释放检查 / A-B 性能数据 / 已知限制与未完成项 / 回滚方式`。
