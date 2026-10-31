# ModifiedDiplomacy：王国谈判 Sprite 资源迁移与 Gauntlet 接入（Worker 执行方案）

> 编写日期：2026-10-29  
> 目标仓库：`https://github.com/GuihuaZhou/bannerlord`，模块：`ModifiedDiplomacy`  
> 任务状态：**用户已在游戏运行目录成功执行 SpriteSheetGenerator，并通过 Modding Kit 导入，已生成 `ui_kingdomnegotiation_1_tex.tpac`**。不必再次导入，除非资源内容发生变化或导入文件损坏。  
> 执行主体：能访问 Windows 本地游戏运行目录、Git 工作区和游戏 Modding Kit 的 Worker/Codex。  
> 本文是实施指导文档，**不是已经完成的代码变更或实机验收报告**。

## 0. Worker 的执行任务和边界

**请先阅读本文并按阶段执行，最终提交代码差异、文件清单、验证记录，不要仅给建议。**

本次任务只完成：

1. 将已经生成并验证的 Sprite **从游戏安装/运行目录同步回开发仓库**，确保版本可追踪。
2. 检查 SpriteData 中真实的 Sprite 名称与分类，并让独立王国谈判窗口正确加载、显示、卸载这张装饰图。
3. 补足**开发仓库 → 游戏运行目录**的稳定部署流程，不再依赖手工遗漏资源；保持现有谈判能正常打开、使用、关闭、再次打开。
4. 输出修改清单、测试结果、待用户操作步骤以及回滚方式。

**禁止事项**：不改变 `KingdomNegotiationVM` 的条款计算、AI 评分、议会表决、外交提案持久化/执行；不重排公共条款、不改变条款类型；不改动无关 Mod；不覆盖用户尚未提交的代码；不把整张带按钮和文字的 UI 效果图烘焙为背景；不在本任务自动 push/merge 到主分支（除非用户另有授权）。

> 后续“中央公共外交条款 + 左右资源交换”的 UI 重排应作为**另一个阶段**，在本次素材加载链路稳定后独立开发和验收。

## 1. 已核对的仓库现状

- 实际源码仓库中的模块根目录是 `bannerlord/ModifiedDiplomacy/`，**不是** `bannerlord/ModifiedDiplomacy/ModifiedDiplomacy/`。后者为 C# 项目目录。
- 当前原版风格谈判 XML：`ModifiedDiplomacy/GUI/Prefabs/KingdomNegotiationScreen.xml`。
- 当前窗口服务：`ModifiedDiplomacy/ModifiedDiplomacy/KingdomDiplomacy/Negotiation/KingdomNegotiationScreenService.cs`。
- 当前项目文件：`ModifiedDiplomacy/ModifiedDiplomacy/ModifiedDiplomacy.csproj`。
- XML 的 `CenterPanel` 是独立的中央 Barter 区域：`Id="CenterPanel"`、`SuggestedWidth="912"`、`SuggestedHeight="555"`；其现有子控件包括左右支持度条、双方已选择的交换列表、自动平衡按钮、重置/提交/取消按钮等。
- 服务类当前已手动加载 `ui_barter`：`UIResourceManager.GetSpriteCategory("ui_barter")` 并于关闭时 `Unload()`。新分类建议沿用这一生命周期管理方式。
- `.csproj` 显式包含两个谈判 Prefab，但**没有发现用于将新增 `SpriteParts`、SpriteData、`AssetSources` 和 `.tpac` 自动同步到游戏 Module 根目录的现成规则**。不要误以为 `CopyToOutputDirectory` 会自动部署全部游戏资源。
- 起步包曾提供源图文件名 `negotiation_center_decoration.png`；**最终 Sprite 引用名须以用户此次生成的 `ModifiedDiplomacySpriteData.xml` 实际内容为准**，不要硬编码臆测前缀。

相关代码链接：

- [谈判 Prefab](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedDiplomacy/GUI/Prefabs/KingdomNegotiationScreen.xml)
- [谈判 ScreenService](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedDiplomacy/ModifiedDiplomacy/KingdomDiplomacy/Negotiation/KingdomNegotiationScreenService.cs)
- [ModifiedDiplomacy.csproj](https://github.com/GuihuaZhou/bannerlord/blob/main/ModifiedDiplomacy/ModifiedDiplomacy/ModifiedDiplomacy.csproj)

## 2. 两个目录的角色与复制原则

### 2.1 运行目录（已有成功资源）

```text
D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedDiplomacy\
```

### 2.2 开发目录（Worker 需要自行定位）

开发仓库以 `git rev-parse --show-toplevel` 得到的目录为准；以下记为 `<RepoRoot>`。资源应放在：

```text
<RepoRoot>\ModifiedDiplomacy\
```

不要擅自假设开发仓库位于 `D:\...`，也不要把游戏运行目录当作 Git 根目录。若两者实际是同一目录，**先确认，不要重复复制**。

### 2.3 必须或建议同步的文件

| 相对路径（均相对于 `ModifiedDiplomacy/`） | 用途 | 处理 |
|---|---|---|
| `GUI/SpriteParts/ui_kingdomnegotiation/*.png` | 用户可编辑的 Sprite 输入 PNG | **必须同步**；后续重新生成图集的来源 |
| `GUI/ModifiedDiplomacySpriteData.xml` | 生成的分类、Sprite 名称、图集位置清单 | **必须同步**；若文件名不同，以实际生成的 `*SpriteData.xml` 为准 |
| `AssetSources/GauntletUI/ui_kingdomnegotiation_1.png` | SpriteSheetGenerator 生成的图集纹理源 | **建议同步**，可编辑资产/后续资源导入需要 |
| `Assets/GauntletUI/ui_kingdomnegotiation_1_tex.tpac` | Modding Kit 导入成功的游戏资源 | **必须同步**，包含于受版本管理的可编辑开发资产集 |
| `art/KingdomNegotiation/*_source.png`（若已有） | 美术设计原稿 | **建议同步**；仅作为源素材，不自动引用为 Sprite |
| `GUI/SpriteParts/Config.xml`（如存在） | 自定义类别加载选项 | **按现状同步**；本次建议手动 `Load/Unload`，不主动新增 `AlwaysLoad` |
| `RuntimeDataCache/`、崩溃转储、日志、整个 `bin/`、无关 `.tpac` | 缓存、日志、编译输出及其他模块产物 | **不要从运行目录反向导入 Git** |

如果 `Assets/GauntletUI` 还有与本分类关联的必要元数据/伴随文件，请先列清单并查证再同步；不得把整棵 `Assets` 无差别覆盖到开发仓库。

**重要**：开发仓库保留 `Assets` + `AssetSources` 是官方文档允许的可编辑资产交付方式；面向最终玩家发行时也可以使用编辑器的 **Client 打包**流程产生 `AssetPackages`。**不能假定仅复制 `.tpac` 就已完成所有最终发行打包步骤**。

## 3. 阶段 A —— 从运行目录同步资源到开发仓库

### A-1. 创建安全工作区

1. 检查 `git status --short`。如存在用户尚未提交的修改，不得清理/重置；保留现状并隔离任务变更。
2. 在不覆盖其他工作分支的前提下建专用分支，例如 `feature/modifieddiplomacy-negotiation-sprite`；如果当前环境有既定分支管理规则，优先遵守。
3. 记录运行目录上述四个资源的存在性、字节数、SHA-256 与修改时间。
4. 先不要覆盖开发目录下已有不同内容的同名资源；比对后确认来源，必要时备份再复制。

### A-2. PowerShell 参考命令（Worker 可按环境调整）

在 **Windows PowerShell** 中执行。请先从正确的 Git 工作树进入，再运行：

```powershell
$GameMod = 'D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedDiplomacy'
$RepoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $RepoRoot) { throw '当前目录不是 Git 仓库，请先进入开发仓库' }
$DevMod = Join-Path $RepoRoot 'ModifiedDiplomacy'
if (-not (Test-Path (Join-Path $DevMod 'ModifiedDiplomacy\ModifiedDiplomacy.csproj'))) {
    throw "开发目录校验失败：$DevMod"
}

$RelPaths = @(
    'GUI\SpriteParts\ui_kingdomnegotiation\negotiation_center_decoration.png',
    'GUI\ModifiedDiplomacySpriteData.xml',
    'AssetSources\GauntletUI\ui_kingdomnegotiation_1.png',
    'Assets\GauntletUI\ui_kingdomnegotiation_1_tex.tpac'
)

foreach ($rel in $RelPaths) {
    $src = Join-Path $GameMod $rel
    $dst = Join-Path $DevMod $rel
    if (-not (Test-Path -LiteralPath $src)) {
        Write-Warning "运行目录文件不存在，请核实实际生成路径：$src"
        continue
    }
    $parent = Split-Path -Parent $dst
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    if (Test-Path -LiteralPath $dst) {
        $s = (Get-FileHash -Algorithm SHA256 -LiteralPath $src).Hash
        $d = (Get-FileHash -Algorithm SHA256 -LiteralPath $dst).Hash
        if ($s -ne $d) {
            # 不直接覆盖工作区中可能包含用户修改的文件
            Write-Warning "开发目录已存在不同内容，需先审核并备份：$dst"
            continue
        }
        Write-Host "已一致：$rel"
        continue
    }
    Copy-Item -LiteralPath $src -Destination $dst
    $s = (Get-FileHash -Algorithm SHA256 -LiteralPath $src).Hash
    $d = (Get-FileHash -Algorithm SHA256 -LiteralPath $dst).Hash
    if ($s -ne $d) { throw "复制后校验失败：$rel" }
    Write-Host "已复制且 SHA256 一致：$rel"
}
```

**不要机械执行上述四个文件名**：先检查 `AssetSources/GauntletUI` 和 `GUI/` 里实际生成的文件名，如有不同，应修改 `$RelPaths`。确认其他同分类 Sprite 原图、多个图集或新文件后，将它们**完整列入**清单。对于 `art/` 设计源图，如当前只在先前的起步 ZIP 中，另行从该 ZIP 加入开发库（不要把原图误认为运行资源）。

### A-3. 迁移后检查

```powershell
Get-ChildItem (Join-Path $DevMod 'GUI\SpriteParts\ui_kingdomnegotiation') -File
Get-ChildItem (Join-Path $DevMod 'AssetSources\GauntletUI') -File |
    Where-Object Name -like 'ui_kingdomnegotiation*'
Get-ChildItem (Join-Path $DevMod 'Assets\GauntletUI') -File |
    Where-Object Name -like 'ui_kingdomnegotiation*'
git status --short
```

对新增 PNG/TPAC 采取仓库已有大文件管理规范；如仓库**已经使用 Git LFS**，检查 `.gitattributes` 并适当纳管；如果没有 LFS，**不要自行在本任务引入新依赖**。不要误将这些文件加入 `.gitignore`。提交前检查 Git 能实际记录二进制文件。

## 4. 阶段 B —— 确认真实 Sprite 名称与分类

1. 打开运行目录和开发目录中的 `GUI/ModifiedDiplomacySpriteData.xml`（以实际名称为准），应能找到类似 `<Name>ui_kingdomnegotiation</Name>` 的 `SpriteCategory`。
2. 在 `SpriteParts` / `GenericSprite` 条目中搜索 `negotiation_center_decoration`，提取**Sprite 的完整 `<Name>`**。
3. 检查该条目对应 `SheetID` 及图集分类，确认没有与原版 Sprite 重名。
4. **在 Gauntlet XML 的 `Sprite="..."` 中使用该 `<Name>` 的真实值。** 文档示例中的 `negotiation_center_decoration` 仅是候选值；**不要自动拼接 `ui_kingdomnegotiation\` 前缀**，因为分类名称与 Sprite 名称不是一回事。
5. 通过 XML 解析工具验证生成的 `SpriteData` 是有效 XML；检查同名重复资源及图集文件存在性。

官方示例也是把 `SpriteData.xml` 中的真实 Sprite 名直接用于 `Sprite="mysprite"`，而不是 PNG 文件路径。

## 5. 阶段 C —— 最小化接入现有王国谈判窗口

### C-1. 修改 ScreenService：加入手动加载与卸载

修改：

```text
ModifiedDiplomacy/ModifiedDiplomacy/KingdomDiplomacy/Negotiation/
KingdomNegotiationScreenService.cs
```

要求：

1. 保留现有 `ui_barter` 分类加载/卸载和 `GauntletLayer`/`GauntletMovieIdentifier` 生命周期。
2. 新增 `SpriteCategory` 字段，例如 `_negotiationArtCategory`。
3. **在 `_layer.LoadMovie("KingdomNegotiationScreen", _dataSource)` 之前**获取并 `Load()` `ui_kingdomnegotiation`，保证 Movie 创建时资源已经可用。
4. 在 `Close()` 中与已有资源同样按幂等方式捕获局部变量、清空静态字段、`Unload()`；成功打开、主动关闭、打开失败、父屏退出都应正确清理。
5. 缺少分类或失败应记录可读诊断信息。不能仅为装饰图不可用而永久阻断玩家的外交功能；若资源缺失时必须降级，应明确说明所用的降级策略并实机验证。
6. **不要**同时选择 `AlwaysLoad` 和手动加载/卸载作为两个竞争的生命周期方案。本次优先手动加载/卸载。

现有代码已使用：

```csharp
_barterCategory = UIResourceManager.GetSpriteCategory("ui_barter");
_barterCategory?.Load();
```

新分类参考：

```csharp
_negotiationArtCategory =
    UIResourceManager.GetSpriteCategory("ui_kingdomnegotiation");
_negotiationArtCategory?.Load();
```

**该片段并非完整补丁**；Worker 应把初始化、异常回滚、`Close()` 清理与日志一起实现，且以当前源码 API 为准。

> 代码卫生提醒：现有 `ScreenService` 的部分注释/错误提示仍写 `ModifiedPolitics`，是历史迁移遗留文案；本次只可在附近针对资源路径做最小修正，不借此大范围重构。

### C-2. 修改 XML：只在现有中央面板叠加非交互装饰

修改：

```text
ModifiedDiplomacy/GUI/Prefabs/KingdomNegotiationScreen.xml
```

定位 `Id="CenterPanel"`（当前尺寸 912×555），在其**直接 `<Children>` 列表开头**加入一层 Sprite；放在进度条、列表、按钮等交互控件之前。示例：

```xml
<!-- 非交互装饰层：Sprite 名需以 SpriteData 的实际 Name 为准 -->
<Widget Id="NegotiationArtBackground"
        WidthSizePolicy="StretchToParent"
        HeightSizePolicy="StretchToParent"
        Sprite="negotiation_center_decoration"
        AlphaFactor="0.25"
        IsEnabled="false"
        DoNotAcceptEvents="true" />
```

说明：

- `AlphaFactor="0.25"` 只是初始验证值；这批美术图背景较暗，需根据游戏中真实可见性和文本对比度调整，优先保证清晰可读。
- 这只是静态装饰；不能挡住原有 `ScrollablePanel`、`BarterOfferItemTuple`、`FillBarWidget` 和提交按钮。
- 不删除原有 `Sprite="Barter\barter_panel"` 等资源、也不改变现有 `Command` / `DataSource` 绑定。
- 不保证这张全框式装饰图在现有多层 Panel 上一定美观；如视觉重叠、裁剪或不显示，调整图层透明度和位置，不要以大幅重写布局作为第一反应。
- 如原图含较重边框，必须确认不会与原生中央边框叠出双重线条；必要时只保留淡背景纹理或考虑去掉该装饰图。

### C-3. 首次验证不要求重做整套 UI

这一步成功的定义是：**游戏中的原有外交谈判界面正常运行，中央装饰图片确实出现且不阻挡操作**。它不是最终美术方案、也不是前面讨论的“公共外交条款改版”。

## 6. 阶段 D —— 让开发代码可稳定部署到游戏目录

**核心问题**：目前资源在游戏目录里是对的，但如果只修改/编译开发目录的 `.csproj`，后续再部署 DLL 时不一定同步 SpriteData 和 `.tpac`，会出现“代码已经更新、图片却丢失”的情况。

Worker 应检查仓库既有部署/打包脚本，优先沿用既有机制。若没有，新增**小范围、可重复执行**的资源同步脚本（例如 `ModifiedDiplomacy/tools/deploy_ui_assets.ps1`）；不得全量覆盖游戏 Module 或删除手工导入的其他资源。脚本至少同步：

```text
<RepoRoot>\ModifiedDiplomacy\GUI\Prefabs\KingdomNegotiationScreen.xml
<RepoRoot>\ModifiedDiplomacy\GUI\Prefabs\KingdomNegotiationItemTuple.xml
<RepoRoot>\ModifiedDiplomacy\GUI\ModifiedDiplomacySpriteData.xml
<RepoRoot>\ModifiedDiplomacy\GUI\SpriteParts\ui_kingdomnegotiation\*.png
<RepoRoot>\ModifiedDiplomacy\AssetSources\GauntletUI\ui_kingdomnegotiation*.png
<RepoRoot>\ModifiedDiplomacy\Assets\GauntletUI\ui_kingdomnegotiation*.tpac
```

目标为：

```text
D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedDiplomacy\
```

**注意复制方向**：第一次是 `运行目录 → 开发仓库`，目的是把成功导入的资源收归版本管理；之后正常开发迭代原则上采用 `开发仓库 → 运行目录`。禁止把后续游戏目录中的旧代码反向覆盖开发仓库。

- 资源同步脚本必须有“目标路径校验、只同步本分类文件、可重复运行、不删除其他资源”的安全性。
- 对项目文件 `.csproj` 的修改需遵循当前构建布局；不能以为 `CopyToOutputDirectory` 就会自动把资源复制到 `Modules/ModifiedDiplomacy/GUI`、`Assets/GauntletUI`。
- 如将来修改了源 PNG：需要重新执行 SpriteSheetGenerator → **彻底退出生成器** → 用 Modding Kit Reimport/更新资源 → 把新的 SpriteData、atlas、TPAC 再同步回开发仓库。**用户此前已经验证：不退出生成器会导致 `FreeImage_Load` 无法打开图集、进而崩溃。**

## 7. 阶段 E —— 验收与回归测试

### E-1. 静态检查

- [ ] 开发仓库中 SpriteParts PNG、SpriteData XML、AssetSources 图集、Assets TPAC 都存在，文件大小非零。
- [ ] 与游戏运行目录同步后的 SHA-256 一致；`SpriteData.xml` XML 可解析，Sprite 名确实存在。
- [ ] Git 只显示本任务相关的 PNG、XML、TPAC、少量 C# 和必要部署脚本改动；没有提交游戏本体文件、日志、临时缓存。
- [ ] `KingdomNegotiationScreen.xml` 不存在重复插入的 `NegotiationArtBackground`，原有绑定和按钮仍在。
- [ ] `KingdomNegotiationScreenService.cs` 中对分类有与界面生命周期匹配的 `Load/Unload` 和异常路径清理。
- [ ] 现有工程构建通过，DLL 能部署至匹配的游戏 Client 目录；若要再次进编辑器则同版本 Editor 目录也须有可用 DLL。

### E-2. 游戏内测试（Worker 能操作游戏时亲测；否则明确列为待用户确认）

- [ ] 启用 `ModifiedDiplomacy` 能启动游戏并加载旧存档。
- [ ] 王国外交页能打开王国谈判界面，加载 Sprite 没有缺失图集/找不到 Sprite/资源类别的报错。
- [ ] 中央装饰图可见，文字可读，支持度条和各个原有列表都可正常操作。
- [ ] 金币 +/-、选取城镇/俘虏、重置、取消、提交（尽量用专门测试存档）、鼠标滚动等原有交互功能没有被装饰层遮挡。
- [ ] 连续打开→关闭→再次打开至少 3 次，无图层重叠、重复加载、异常、显著卡顿。
- [ ] 资源缺失情况下有明确诊断/降级行为，不出现无法恢复的 UI 锁死。
- [ ] 此次不改变双方议会支持度计算、不改变外交提案结果。

### E-3. Worker 最终报告必须包含

1. 变更文件列表（新增/修改、用途、目标路径）。
2. 运行目录来源资源、开发目录目标资源及 SHA-256 对账结果。
3. SpriteData 读取到的**确切分类名和确切 Sprite 名**。
4. 编译结果与在 Client/Editor 部署检查的结果；未执行的步骤写“未执行”，不能假称通过。
5. 游戏内截图或实测说明；如 Worker 无法启动用户本地游戏，给用户最短的本地验证步骤。
6. 任何问题、剩余风险、回滚方法。

## 8. 快速回滚

- 保留用户原本文件，不直接 `git reset --hard`。
- 若新增装饰导致打不开 UI：优先回滚 `NegotiationArtBackground` 及 `_negotiationArtCategory` 的引用与生命周期修改，保留可复用的资源源文件。
- 重新部署未修改的谈判 Prefab 与 ScreenService DLL，确认原版 Barter 风格谈判恢复。
- 不要在未确认依赖关系之前删除用户运行目录已有的 `.tpac` 或批量清空 `Assets`。

## 9. 官方参考

1. TaleWorlds：**Generating and Loading UI Sprite Sheets**  
   https://moddocs.bannerlord.com/asset-management/generating_and_loading_ui_sprite_sheets/
2. TaleWorlds：**Adding & Overriding Assets**（`Assets`、`AssetSources`、`AssetPackages` 的用途及分发差异）  
   https://moddocs.bannerlord.com/asset-management/asset-types/overriding_assets/

---

## Worker 可直接接收的任务指令（复制以下正文给 Worker）

> 阅读 `ModifiedDiplomacy_Sprite资源迁移与Gauntlet接入_Worker执行方案_20261029.md` 并执行阶段 A～E。游戏运行目录为 `D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedDiplomacy`，其中 `ui_kingdomnegotiation_1_tex.tpac` 已由用户用 Modding Kit 成功导入。定位本地 Git 仓库 `GuihuaZhou/bannerlord` 的实际根路径；先备份和核对哈希，再把本分类 PNG、SpriteData、AssetSources 图集和 TPAC 同步回 `ModifiedDiplomacy/` 开发模块根目录。然后按生成 XML 中的真实 Sprite 名在现有 Gauntlet 中添加非交互装饰层，正确管理 `ui_kingdomnegotiation` 的加载/卸载，确保部署脚本能同步这些资源到游戏目录。先不改变外交业务逻辑、不重排外交条款、不推送主分支。最后给出代码差异、文件清单、构建/运行测试结果、未完成事项与回滚办法；不能访问用户 Windows 游戏目录的步骤必须明确标注，禁止虚构成功。
