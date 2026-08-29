# QWEN.md — ModifiedArmy Mod

## Project Overview

ModifiedArmy 是一个 Mount & Blade II: Bannerlord 的模组，核心目标是重构军事系统，引入封邑部队（Fief Party）机制，并调整志愿兵、雇佣兵、驻军、经济等子系统平衡。

**核心设计**：三种兵源形成策略三角——封邑部队（文化核心/高质量/基于土地）、志愿兵（低成本/本地化/防守倾向）、雇佣兵（高灵活性/即时可用/昂贵）。

## Tech Stack

- **语言**：C# (netstandard2.0, LangVersion latest)
- **框架**：HarmonyLib (2.4.2) — 运行时补丁原版方法
- **游戏 API**：TaleWorlds.CampaignSystem / TaleWorlds.MountAndBlade
- **设置框架**：MCM (5.11.3) — 当前已注释未启用
- **UI 扩展**：UIExtenderEx (2.13.0)
- **构建**：Bannerlord.BuildResources (1.1.0.129)

## Project Structure

```
ModifiedArmy/
├── Main.cs                    — 模组入口，注册 Model/Behavior，加载 XML
├── Common/
│   ├── common.cs              — SoldierType 枚举 + CommonConstants 常量
│   └── ModConfig.cs           — XML 配置模型（MinLogLevel 等）
├── Models/
│   ├── Fief/                  — 封邑子系统（核心，8 个文件）
│   │   ├── FiefPartyData.cs   — 核心数据模型（最大文件 ~2250 行）
│   │   ├── FiefPartyTemplate.cs — XML 模板定义 + 管理器
│   │   ├── FiefPartyManager.cs  — CampaignBehavior 管理器 + 存档定义
│   │   ├── FiefMenuBehavior.cs  — 城镇/城堡菜单注入
│   │   ├── FiefWageExemptionManager.cs — 工资豁免
│   │   ├── AiRecruitFiefTroopsBehavior.cs — AI 征召
│   │   ├── FiefSettlementTaxModel.cs   — (已注释) 封邑税收
│   │   └── FiefPartyFoodConsumptionModel.cs — (已注释) 食物消耗
│   ├── NewVolunteerModel.cs   — 志愿兵生成/招募/文化倍率
│   ├── NewPartyWageModel.cs   — 招募费 + 工资（Tier 4+ 大幅提高）
│   ├── NewPartyTroopUpgradeModel.cs — 升级经验（Tier 4+ 翻倍）
│   ├── NewPartySizeLimitModel.cs    — 驻军人数限制
│   ├── NewSettlementLoyaltyModel.cs — 文化忠诚度惩罚
│   ├── NewClanTierModel.cs   — Clan 分队数（基于定居点数）
│   ├── NewSettlementMilitiaModel.cs — 民兵计算（扣除封邑人数）
│   ├── NewBuildingConstructionModel.cs — AI 建筑优先级
│   ├── NewPrisonerRecruitmentCalculationModel.cs — 俘虏招募（按 SoldierType 差异化）
│   ├── NewBasicTroopManager.cs — SoldierType 分类器 + BasicTroopGroup 管理
│   ├── MercenaryTemplate.cs  — 雇佣兵 XML 模板
│   └── ...（已注释的 Debug/Feature 模型）
├── Patch/                     — Harmony 补丁（Prefix 替换原版 Behavior）
│   ├── RecruitmentCampaignBehavior.cs — 雇佣兵生成（仅基础兵/固定人数）
│   ├── RecruitPrisonersCampaignBehavior.cs — 玩家/AI 俘虏招募
│   ├── GarrisonTroopsCampaignBehavior.cs — 驻军操作限制
│   ├── SettlementPatch.cs    — 民兵移除保护（跳过封邑兵）
│   ├── BuildingsCampaignBehavior.cs — AI 建筑每日 Tick
│   ├── DiplomaticBartersBehavior.cs — AI 外交决策
│   ├── FiefBarterBehavior.cs — 封地交易
│   ├── HeroSpawnCampaignBehaviorPatch.cs — 领主生成逻辑
│   ├── PartiesSellPrisonerCampaignBehavior.cs — 俘虏售卖
│   └── PrisonerReleaseCampaignBehaviorPatch.cs — 囚犯逃逸
├── Utils/
│   ├── ModLogger.cs          — 彩色日志（级别：Debug→Info→Notice→Warn→Error）
│   └── Settings.cs           — (已注释) MCM 设置界面
└── ModuleData/               — XML 配置数据
    ├── basic_troop_config.xml      — 各文化基础兵种（SoldierType 分类）
    ├── fiefPartyTemplates.xml      — 各文化封邑模板（town/castle/port 变体）
    ├── mercenaryTemplates.xml      — 雇佣兵模板
    ├── modConfigs.xml             — Mod 配置（MinLogLevel 等）
    └── submodule_strings.xml       — 本地化字符串
```

## Key Conventions

### 代码风格
- 命名空间：`ModifiedArmy`、`ModifiedArmy.Models`、`ModifiedArmy.Models.Fief`、`ModifiedArmy.Patch`、`ModifiedArmy.Tool`（注意 ModLogger 在 Utils/ 目录但命名空间是 `ModifiedArmy.Tool`）
- 常量集中在 `CommonConstants`、`GarrisonConstants`、`ClanPartySpawnConstants`（`common.cs`）
- XML 可配置的数值使用模板属性（如 `prosperityCostPerTier`、`maxServiceWeeks`），硬编码常量放在 `CommonConstants`
- 注释掉的代码保留不删除（标记 `//` 或 `////`），表示计划启用或历史参考

### 扩展游戏的方式
- **Model**：继承原版 Model，重写方法，通过 `campaignStarter.AddModel()` 注册——游戏自动使用最新注册的 Model
- **Behavior**：继承 `CampaignBehaviorBase`，通过 `campaignStarter.AddBehavior()` 注册——注册事件监听
- **Harmony Patch**：`Patch/` 目录下，使用 `[HarmonyPatch]` + `[HarmonyPrefix]` 替换原版 Behavior 中的方法——用于无法通过 Model 覆盖的场景
- **XML 数据**：通过 `MBObjectManager.RegisterType` + `LoadXML` 加载，定义单例 Manager 按文化索引

### 封邑系统核心流程
1. `FiefPartyTemplateManager` 加载 XML 模板，按 `{culture}_{town|castle}{_port?}` 索引
2. `FiefPartyManager` 初始化每个 Settlement 的 `FiefPartyData`
3. `FiefPartyData.WeeklyUpdate()` 处理：自动升级 → 冷却到期回归 → 按模板+繁荣度补员
4. 征召：就绪兵 → `RecruitedTroopDetachmentList`（WaitCycle=-1）→ 服役期满(WaitCycle=5) → 从计数器扣除
5. 归还：→ `ReturnedTroopDetachmentList`（WaitCycle=3）→ 冷却期满 → 回到就绪
6. 经济：征召消耗繁荣度+户数，服役期每日繁荣度 Debuff，归还恢复

### 文化差异化设计
| 文化 | 封邑特色 | 志愿兵倍率 | 设计原型 |
|------|---------|-----------|---------|
| Vlandia | 服役长(7周)、征召贵(10) | 0.2× | 西欧采邑制 |
| Empire | 封邑弱、征召便宜(2-5) | 0.6× | 拜占庭军区制 |
| Aserai | 征召便宜(2-5)、有奴隶兵 | 0.7× | 阿拉伯伊克塔 |
| Sturgia | 兵力上限高、中等成本 | 1.0× | 东欧亲兵制 |
| Battania | 村庄加成高 | 1.0× | 凯尔特部落制 |
| Khuzait | 冷却快(1周)、民兵占比高 | 1.0× | 游牧召之即来 |
| Nord | 民兵占比极高 | 1.0× | 维京全民战士 |

## 核心规则

1. **禁止随意读取代码** — 只读取与当前任务直接相关的文件，不要为了"了解项目"而大面积浏览源码。ARCHITECTURE.md 已包含完整架构信息，优先查阅它而非读源文件。
2. **禁止自动提交 commit** — 任何 git commit 必须由用户明确要求后才可执行，不要在完成任务后主动提议或自动提交。

## Important Notes

- **不要删除注释掉的代码**——它们是计划功能或历史参考，与项目 Roadmap 关联
- **ARCHITECTURE.md 是权威参考**——包含完整的字段名、方法签名、常量值、XML 结构。做任何实现前先读它
- **存档序列化**：`FiefPartyManager.cs` 中定义了存档序列化（基 ID 20251116），修改 FiefPartyData/FiefTroopDetachment 字段时需同步更新序列化定义
- **FiefPartyData.cs 是最大文件**（~2250 行），包含核心数据模型 + 分遣队内嵌类，修改需谨慎
- **游戏 DLL 路径**：引用 `D:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\` 下的 DLL
- **XML 数据部署路径**：`D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedArmy\ModuleData\`
- **当前无自动化测试**——验证需在游戏内运行，修改后建议检查存档兼容性
- **日志级别**：默认 `Notice`，Debug/Info 不显示；可通过 `modConfigs.xml` 的 `MinLogLevel` 调整
