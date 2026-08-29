# ModifiedArmy Mod — 架构与实现文档

> 最后更新：2026-08-29
> 代码根目录：`D:\workspace\bannerlord\ModifiedArmy\ModifiedArmy\`

---

## 目录

1. [项目概览](#1-项目概览)
2. [入口与初始化](#2-入口与初始化)
3. [士兵类型体系](#3-士兵类型体系)
4. [封邑部队系统 (Fief Party)](#4-封邑部队系统-fief-party)
5. [志愿兵系统 (Volunteer)](#5-志愿兵系统-volunteer)
6. [雇佣兵与俘虏系统](#6-雇佣兵与俘虏系统)
7. [驻军与民兵系统](#7-驻军与民兵系统)
8. [经济与工资系统](#8-经济与工资系统)
9. [部队规模与升级](#9-部队规模与升级)
10. [建筑与AI建设](#10-建筑与ai建设)
11. [外交系统](#11-外交系统)
12. [定居点补丁](#12-定居点补丁)
13. [领主生成与部队创建](#13-领主生成与部队创建)
14. [囚犯管理](#14-囚犯管理)
15. [常量配置](#15-常量配置)
16. [工具类](#16-工具类)
17. [XML 配置数据](#17-xml-配置数据)
18. [待实现/改进项](#18-待实现改进项)

---

## 1. 项目概览

ModifiedArmy 是一个 Mount & Blade II: Bannerlord 的模组，核心目标是重构游戏的军事系统，引入封邑部队（Fief Party）机制，并调整志愿兵、雇佣兵、驻军、经济等子系统的平衡。

**设计理念**：三种兵种来源形成策略三角：
- **封邑部队**：文化核心，高质量，基于土地分封，消耗繁荣度/户数
- **志愿兵**：低成本，本地化，防守倾向，绑定 Hero 关系
- **雇佣兵**：高灵活性，即时可用，合约制，昂贵

**技术栈**：C# / HarmonyLib / TaleWorlds CampaignSystem API / MCM (设置框架，当前已注释)

**目录结构**：
```
ModifiedArmy/
├── Main.cs                  — 模组入口，注册所有 Model 和 Behavior
├── common/common.cs         — SoldierType 枚举 + CommonConstants 常量
├── Models/                  — 游戏模型（覆盖原版 Model）
│   ├── Fief/                — 封邑系统（独立子系统，8个文件）
│   ├── NewVolunteerModel.cs
│   ├── NewPartyWageModel.cs
│   ├── ...                  — 其他 Model
├── Patch/                   — Harmony 补丁（修改原版 Behavior）
├── Utils/                   — ModLogger + Settings（Settings 当前已注释）
└── Tool/                    — ModLogger 实际在此（命名空间 ModifiedArmy.Tool）
```

---

## 2. 入口与初始化

**文件**：`Main.cs`

### Main : MBSubModuleBase

`OnGameStart()` 中注册所有 Model 和 Behavior：

**注册的 Model**：
| Model | 作用 |
|-------|------|
| `NewVolunteerModel` | 志愿兵生成与招募限制 |
| `NewPartyWageModel` | 工资与招募费用 |
| `NewPartyTroopUpgradeModel` | 升级经验成本 |
| `NewPartySizeLimitModel` | 驻军人数限制 |
| `NewSettlementLoyaltyModel` | 定居点忠诚度（文化惩罚） |
| `NewClanTierModel` | Clan 分队数限制 |
| `NewSettlementMilitiaModel` | 民兵计算（扣除封邑人数） |
| `NewBuildingConstructionModel` | AI 建筑优先级 |
| `NewPrisonerRecruitmentCalculationModel` | 俘虏招募难度 |

**已注释的 Model**：
- `NewMinorFactionsModel` — 雇佣兵加入王国费用调整
- `FiefSettlementTaxModel` — 封邑制度下城镇税收（10% 原版）
- `DebugGarrisonMoraleModel` / `DebugDefaultPartyDesertionModel` — 调试用

**注册的 Behavior**：
| Behavior | 作用 |
|----------|------|
| `CampaignReadyBehavior` | 设置 `CampaignState.IsReady` / `IsNewGame` |
| `FiefPartyManager` | 封邑系统核心管理器 |
| `FiefMenuBehavior` | Town/Castle 菜单注入"封邑"入口 |
| `FiefWageExemptionManager` | 封邑兵工资豁免管理 |
| `AiRecruitFiefTroopsBehavior` | AI 征召封邑兵 |
| `AIBuildingAutoBoostBehavior` | AI 每周自动投入金币加速建筑 |

**已注释的 Behavior**：
- `GarrisonRecruitFromPrisonersBehavior` — 驻军从俘虏自动招募

**XML 数据加载**：
- `BasicTroopGroup` → `BasicTroopGroups` XML — 各文化的基础兵种配置
- `FiefPartyTemplate` → `FiefPartyTemplates` XML — 各文化封邑模板

**新游戏初始化**：`OnAfterSessionLaunched` 中给玩家 99000 金 + 斯图吉亚精锐装备

---

## 3. 士兵类型体系

**文件**：`common/common.cs` + `Models/NewBasicTroopManager.cs`

### SoldierType 枚举

```csharp
public enum SoldierType
{
    Retinue,    // 扈从 — 高级骑兵/贵族兵
    Sergeant,   // 军士 — 中级步兵/射手
    Militia,    // 民兵 — 低级基础兵
    Marine,     // 水兵 — 海军/港口兵种
    Slave,      // 军事奴隶 — 如马穆鲁克
    Other       // 其他 — 非封邑类型
}
```

### BasicTroopGroup（XML 配置）

每个文化一个 `BasicTroopGroup`，按 `SoldierType` 分类管理基础兵种条目：

```
BasicTroopGroup (per culture)
├── RetinueTroops  → List<BasicTroopEntry>
├── SergeantTroops → List<BasicTroopEntry>
├── MilitiaTroops  → List<BasicTroopEntry>
├── SlaveTroops    → List<BasicTroopEntry>
└── MarineTroops   → List<BasicTroopEntry>
```

**BasicTroopEntry** 字段：
- `Troop` (CharacterObject) — 兵种对象
- `Weight` (int) — 生成权重
- `Type` (SoldierType) — 兵种类型
- `RequiredBarracksLevel` (int) — 所需兵营等级（0=无要求）

**BasicTroopGroupManager** — 单例，按 Culture 索引 BasicTroopGroup

### SoldierTypeClassifier（全局分类器）

- 启动时遍历所有文化的 `BasicTroopGroup`
- 对每个基础兵种，递归传播其 `SoldierType` 到整个升级树
- 提供 `GetSoldierType(troop)` 和 `IsFiefTroop(troop)` 查询接口
- 缓存到 `_typeMap: Dictionary<CharacterObject, SoldierType>`

---

## 4. 封邑部队系统 (Fief Party)

**目录**：`Models/Fief/`，共 8 个文件

### 4.1 FiefPartyData（核心数据模型）

**文件**：`Models/Fief/FiefPartyData.cs`（2252行，最大的文件）

#### 核心字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `_settlement` | Settlement | 所属定居点 |
| `_fiefParty` | TroopRoster | 就绪封邑军队（复用民兵部队的 MemberRoster） |
| `RecruitedTroopDetachmentList` | List\<FiefTroopDetachment\> | 已征召的分遣队 |
| `ReturnedTroopDetachmentList` | List\<FiefTroopDetachment\> | 处于冷却期的分遣队 |
| `_soldierTypeCounts` | Dict\<SoldierType, int\> | 当前各类型实际人数 |
| `_soldierTypeMaxCounts` | Dict\<SoldierType, int\> | 各类型最大人数上限 |
| `_soldierTypeWeights` | Dict\<SoldierType, int\> | 各类型生成权重 |
| `_totalLimit` | int | 总兵力上限 |
| `_totalTroopCount` | int | 当前总人数 |
| `_fiefPartyTemplate` | FiefPartyTemplate | 使用的部队模板 |

#### 三大状态流转

```
就绪 (_fiefParty) ──征召──→ RecruitedTroopDetachmentList (WaitCycle=-1)
                                    │
                               服役周期到
                                    │
                                    ↓  (WaitCycle=FIEF_TROOP_MAX_SERVICE_CYCLE=5)
                             等待倒计时（每周 Tick -1）
                                    │
                               WaitCycle ≤ 0
                                    │
                                    ↓  (移除记录，不直接回到就绪)
                             从计数器中扣除

归还 ──→ ReturnedTroopDetachmentList (WaitCycle=RETURN_TROOP_WAIT_CYCLE=3)
                    │
               每周 Tick -1
                    │
              WaitCycle ≤ 0
                    │
                    ↓
             回到就绪 (_fiefParty)
```

#### 关键方法

| 方法 | 说明 |
|------|------|
| `InitialFeifPartyData()` | 初始化：解析模板、计算上限、同步计数器 |
| `CalculateSizeLimit()` | BaseLimit + VillageBonus × 村庄数 |
| `CalculateReinforcementMultiplier()` | 繁荣度权重0.4 + 户数权重0.6，映射到 [0,1] |
| `GetWeeklyUpdateCount()` | MinWeeklySupplement ~ MaxWeeklySupplement × 补员倍率 |
| `WeeklyUpdate()` | 自动升级 + 处理冷却到期 + 补员 |
| `DailyUpdate()` | 征召期间每日 Prosperity 损失 |
| `RecruitTroopsToParty()` | 按权重征召全部就绪兵 |
| `RecruitManualSelection()` | 玩家手动选择部分兵征召 |
| `ReturnTroopsToSettlement()` | 归还士兵，恢复 Prosperity/Hearth，进入冷却 |
| `RecruitFromPrisoners()` | 从定居点俘虏中直接招募到封邑 |
| `OnSiegeCompleted()` | 围攻结束：若陷落则清空征召记录并补员 |
| `OnSettlementOwnerChanged()` | 定居点易主：清空征召记录 |

#### 经济系统

**征召成本**（`CalculateRecruitmentProsperityCost`）：
- Town BaseCost = 3, Castle BaseCost = 1
- 分段累进倍率：0-25% → ×1.0, 25-50% → ×1.5, 50-75% → ×2.0, 75-100% → ×2.5
- 玩家 100% Prosperity 影响，AI 10%

**征召期每日 Debuff**（`DailyUpdate`）：
- Town: 0.1 Prosperity/人/天
- Castle: 0.05 Prosperity/人/天

**Hearth 消耗**：
- 每征召 1 人消耗 `VillageHearthCostPer` (=1) Hearth
- 分配到所有附属村庄

**归还恢复**：
- Prosperity: 按 `CalculateRecruitmentProsperityCost` 返还实际归还人数对应值
- Hearth: 每归还 1 人恢复 1 Hearth
- Daily Debuff 不返还

#### 自动升级（`PerformAutoUpgrade`）

- 每周对 Tier < 4 的健康士兵尝试升级
- 升级概率：基础 5%-9%（繁荣度），+ 训练场协同 0-11%（繁荣度 × 训练场等级）
- 最终概率范围 [0.05, 0.20]
- 升级目标按权重选择（步兵 > 骑兵 > 射手 = 5:3:2）

### 4.2 FiefTroopDetachment（分遣队）

**文件**：`Models/Fief/FiefPartyData.cs`（内嵌类）

| 字段 | 说明 |
|------|------|
| `Troops` | Dictionary\<CharacterObject, int\> — 分遣队兵种组成 |
| `WaitCycle` | -1=征召中, >0=冷却倒计时, ≤0=可回归 |

方法：`AddTroops`, `GetTotalCount`, `IsEmpty`, `Tick`, `IsReadyToReturn`, `IsLastCycle`, `Clear`

### 4.3 FiefPartyTemplate（封邑模板）

**文件**：`Models/Fief/FiefPartyTemplate.cs`

从 XML 加载，按 `{culture}_{town|castle}{_port?}` 命名匹配：

| 字段 | 说明 |
|------|------|
| `BaseLimit` | 定居点自身可供养的基础部队人数（默认100） |
| `VillageBonus` | 每个附属村庄的额外加成（默认10） |
| `MinWeeklySupplement` | 每周最少补员（默认5） |
| `MaxWeeklySupplement` | 每周最多补员（默认20） |
| `_allBasicTroops` | 各 SoldierType 的基础兵种列表 |
| `_soldierTypeWeights` | 各 SoldierType 的权重 |
| `_allEnableTroops` | 所有使能兵种（含升级树） |

XML 结构示例：
```xml
<FiefPartyTemplate id="vlandia_town" culture="vlandia" baseLimit="100" villageBonus="10" minWeeklySupplement="5" maxWeeklySupplement="20">
  <TroopComposition>
    <RetinueTroops weight="2">
      <troop id="vlandia_knight" weight="3" barracksLevel="2"/>
    </RetinueTroops>
    <SergeantTroops weight="3">...</SergeantTroops>
    <MilitiaTroops weight="1">...</MilitiaTroops>
  </TroopComposition>
</FiefPartyTemplate>
```

**FiefPartyTemplateManager** — 单例，按 TemplateId 索引

### 4.4 FiefPartyManager（封邑管理器）

**文件**：`Models/Fief/FiefPartyManager.cs`

CampaignBehaviorBase，管理所有定居点的封邑数据。

| 事件 | 行为 |
|------|------|
| `OnSessionLaunched` | 初始化 SoldierTypeClassifier + 所有模板使能兵种 + 初始化封邑数据 |
| `WeeklyTick` | 对所有 FiefPartyData 调用 `WeeklyUpdate()` |
| `DailyTick` | 对所有 FiefPartyData 调用 `DailyUpdate()` |
| `AfterSiegeCompleted` | 调用 `OnSiegeCompleted(isWin)` |
| `OnSettlementOwnerChanged` | 调用 `OnSettlementOwnerChanged()` |
| `OnTroopRecruited` | 志愿兵招募时扣除繁荣度/户数 |

核心字典：`_fiefDataMap: Dictionary<Settlement, FiefPartyData>`

公共 API：`GetFiefData`, `GetFiefTroopRoster`, `RecruitFiefTroopsFromSettlement`, `ReturnTroopsToSettlement`, `ManualRecruitFromFief`, `GetFiefTroopLimit`, `GetAvailableTroopCount`, `GetRecruitedTroopCount`, `GetWaitCycleTroopCount`, `GetFiefTroopCounts`, `GetFiefMaxTroopCounts`, `GetWeeklyUpdateCount`

### 4.5 FiefMenuBehavior（封邑菜单）

**文件**：`Models/Fief/FiefMenuBehavior.cs`

在 Town/Castle 菜单中注入"Enter Fief"选项（仅定居点所有者的 Clan 可用）。

子菜单选项：
| 选项 | 功能 |
|------|------|
| Recruit Fief Troops | 按权重全部征召 |
| Disband Fief Troops | 归还封邑兵到定居点 |
| Recruit Partial Fief Troops | 打开兵种选择 UI，玩家手动选择 |
| Return | 返回上级菜单 |

菜单描述包含文化专属文案（帝国=Pronoia, 瓦兰迪亚=Fief, 阿塞莱=Iqta, 库塞特=Ulus, 斯图吉亚=Princely Domain, 巴丹尼亚=Tribal Holding, 诺德=Viking Holding）

### 4.6 FiefWageExemptionManager（工资豁免）

**文件**：`Models/Fief/FiefWageExemptionManager.cs`

征召封邑兵时给予 28 天工资豁免，按 FIFO 消耗。

| 字段 | 说明 |
|------|------|
| `_exemptionMap` | Dictionary\<string(partyId), List\<FiefWageExemption\>\> |

`FiefWageExemption`：`ExemptedTroopCount` + `DaysRemaining`，每日 Tick

> 注意：`GetTotalWage` 中的豁免逻辑当前已注释掉，豁免目前仅记录但未在工资计算中生效。

### 4.7 AiRecruitFiefTroopsBehavior（AI 征召）

**文件**：`Models/Fief/AiRecruitFiefTroopsBehavior.cs`

| 事件 | 行为 |
|------|------|
| `DailyTick` | AI 氏族每日决策：有可招募封邑兵的定居点 → 派 WarParty 前往 |
| `AiHourlyTick` | AI 领主到达定居点后执行征召 |

征召条件：`PartySizeRatio < 0.7f` 且有足够金钱（≥ 30% 工资）

### 4.8 FiefSaveDefiner（存档序列化）

**文件**：`Models/Fief/FiefPartyManager.cs`

基 ID：20251116，定义了 `FiefTroopDetachment`, `FiefPartyData`, `FiefWageExemption`, `FiefWageExemptionManager` 的类和容器序列化。

### 4.9 FiefSettlementTaxModel（封邑税收）

**文件**：`Models/Fief/FiefSettlementTaxModel.cs`（已注释未启用）

城镇税收 = 原版 × 10%

### 4.10 FiefPartyFoodConsumptionModel（封邑食物消耗）

**文件**：`Models/Fief/FiefPartyFoodConsumptionModel.cs`（已注释未启用）

封邑部队不消耗食物

---

## 5. 志愿兵系统 (Volunteer)

**文件**：`Models/NewVolunteerModel.cs`（941行）

继承 `DefaultVolunteerModel`，重写三个核心方法：

### 5.1 GetDailyVolunteerProductionProbability

每日志愿兵生成概率，基于原版公式（基础 0.4 + 领地修正 + Cantons 政策 + Cavalry Tactics Perk），最后乘以**文化生成倍率**：

| 文化 | 倍率 | 设计意图 |
|------|------|---------|
| Vlandia | 0.2× | 封邑部队强，志愿兵极少 |
| Empire | 0.6× | 封邑较少，更依赖志愿兵 |
| Aserai | 0.7× | 封邑较少，依赖志愿兵+雇佣兵+军事奴隶 |
| 其他 | 1.0× | 保持原版 |

### 5.2 MaximumIndexHeroCanRecruitFromHero

招募位置数量限制，关键改动：
- **NPC 只能在自己 Clan 拥有的 Settlement 招募志愿兵**（`ownerClan == buyerClan`）
- 否则返回 -1，禁止跨领地招募

### 5.3 GetBasicVolunteer

按定居点类型和权重选择基础志愿兵：
- **Village**：仅 Militia
- **Town**：Sergeant + Retinue（+ Marine 如有港口）

### 5.4 UpdateVolunteersOfNotablesInSettlement（内部方法）

每日更新 Hero 提供的志愿兵，**严格控制兵种比例**：

**Town 无港口**：
```
Retinue : Sergeant = 1 : 2
最大：Retinue=2, Sergeant=4
```

**Town 有港口**：
```
Retinue : Sergeant : Marine = 2 : 3 : 1
最大：Retinue=2, Sergeant=3, Marine=1
```

比例失衡时当天停止生成（不重新随机），第二天重新处理。

---

## 6. 雇佣兵与俘虏系统

### 6.1 雇佣兵生成

**文件**：`Patch/RecruitmentCampaignBehavior.cs`

Harmony Prefix 替换 `RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount`：
- 只生成**基础雇佣兵**（从 `Culture.BasicMercenaryTroops` 随机选择）
- 人数固定 5-15（而非原版的等级相关计算）
- 禁止原版的升级逻辑

### 6.2 俘虏招募

**文件**：`Models/NewPrisonerRecruitmentCalculationModel.cs`

按 SoldierType 差异化驯服度需求：
| 类型 | levelOffset | 说明 |
|------|-------------|------|
| Mercenary | 3 | 最容易 |
| Militia / Other | 6（默认） | 较容易 |
| Sergeant / Marine / Slave | 15 | 中等 |
| Retinue | 25 | 最难 |

公式：`(Level + levelOffset)² - 10`

金币成本：原版招募费 × 0.8（80%折扣）

### 6.3 玩家俘虏招募

**文件**：`Patch/RecruitPrisonersCampaignBehavior.cs`

`OnMainPartyPrisonerRecruited` Prefix：
- 招募俘虏需要支付金币 = NewPrisonerRecruitmentCalculationModel 计算值 × 50%
- 从玩家金库扣除

### 6.4 AI 俘虏招募

**文件**：`Patch/RecruitPrisonersCampaignBehavior.cs`

`RecruitPrisonersAi` Prefix：
- 禁止 AI 招募野怪（只允许 Mercenary 和 Soldier 职业）
- 移除原版的 `ConformityToSkip` 逻辑

---

## 7. 驻军与民兵系统

### 7.1 驻军人数限制

**文件**：`Models/NewPartySizeLimitModel.cs`

`CalculateGarrisonPartySizeLimit`：
- 基础人数 = 100（`GarrisonConstants.BaseGarrisonSize`）
- Town 加成 = +50（`GarrisonConstants.TownGarrisonBonus`）
- 叠加 Leadership 技能、Perk、建筑加成

### 7.2 民兵计算

**文件**：`Models/NewSettlementMilitiaModel.cs`

关键改动：`CalculateMilitiaChange` 中**减去封邑部队人数**：
```csharp
militia -= fiefManager.GetAvailableTroopCount(settlement);
```
效果：封邑兵越多，原版民兵增长越慢（因为封邑兵已占用"人口"）

### 7.3 驻军从俘虏招募（已注释）

**文件**：`Models/GarrisonRecruitFromPrisonersBehavior.cs`

- 定居点每天自动从俘虏中招募守军
- Militia/Other 类型 10% 概率，高阶 40% 概率
- 排除 Bandit 职业

### 7.4 驻军兵力操作限制

**文件**：`Patch/GarrisonTroopsCampaignBehavior.cs`

Harmony Prefix 替换 `GarrisonTroopsCampaignBehavior.OnSettlementEntered`：
- Army Leader 保留原逻辑
- 单独 AI 领主：必须是定居点 OwnerClan 才能操作驻军兵力
- 防止 AI 路过盟友城市时随意捐兵导致自身兵力枯竭

### 7.5 封邑兵与民兵隔离

**文件**：`Patch/SettlementPatch.cs`

Harmony Prefix 替换 `Settlement.RemoveMilitiasFromParty`：
- 移除民兵时跳过封邑类型士兵（IsFiefTroop）
- 只按比例移除非封邑民兵

---

## 8. 经济与工资系统

### 8.1 招募费用

**文件**：`Models/NewPartyWageModel.cs`

`GetTroopRecruitmentCost`：大幅提高高等级兵的招募费用

| Level | 原版 | Mod |
|-------|------|-----|
| ≤1 | - | 10 |
| 2-6 | - | 20 |
| 7-11 | - | 50 |
| 12-16 | - | 100 |
| 17-21 | 200 | **400** |
| 22-26 | 400 | **800** |
| 27-31 | 600 | **1200** |
| 32-36 | 1000 | **2000** |
| >36 | 1500 | **3000** |

骑兵附加费：Level<26 加 400（原150），Level≥26 加 1200（原500）
雇佣兵/Gangster/CaravanGuard：×2 系数

### 8.2 日常工资

**文件**：`Models/NewPartyWageModel.cs`

`GetCharacterWage`：大幅提高 Tier 4+ 工资

| Tier | 原版 | Mod |
|------|------|-----|
| 0 | 1 | 1 |
| 1 | 2 | 2 |
| 2 | 3 | 3 |
| 3 | 5 | 5 |
| 4 | 8 | **16** |
| 5 | 12 | **30** |
| 6 | 17 | **51** |
| 7+ | 23 | **46** |

Mercenary 职业：×1.25

### 8.3 GetTotalWage（已注释）

**文件**：`Models/NewPartyWageModel.cs`（注释代码，约100行）

完整的工资计算逻辑，包含：
- FiefWageExemption 豁免消费
- 所有原版 Perk 减免
- 驻军减免
- 政策减免
- 文化增益

> 当前未启用，豁免逻辑仅记录但未在工资计算中生效

### 8.4 定居点忠诚度

**文件**：`Models/NewSettlementLoyaltyModel.cs`

`SettlementOwnerDifferentCultureLoyaltyEffect` = -2（原版为 -3）

### 8.5 封邑税收（已注释）

**文件**：`Models/Fief/FiefSettlementTaxModel.cs`

城镇税收 = 原版 × 10%

---

## 9. 部队规模与升级

### 9.1 部队升级经验

**文件**：`Models/NewPartyTroopUpgradeModel.cs`

`GetXpCostForUpgrade`：大幅提高 Tier 4+ 升级经验

| Tier | 原版 | Mod |
|------|------|-----|
| 1 | 100 | 100 |
| 2 | 300 | 300 |
| 3 | 550 | 550 |
| 4 | 900 | **1800** |
| 5 | 1300 | **2600** |
| 6 | 1700 | **3400** |
| 7 | 2100 | **4200** |

### 9.2 Clan 分队数

**文件**：`Models/NewClanTierModel.cs`

`GetPartyLimitForTier`：基于定居点数量而非 Clan Tier

| 定居点数 | 分队数 |
|----------|--------|
| ≤1 | 1 |
| 2-3 | 2 |
| >3 | 3 |

Minor Faction 保持原逻辑（基于 Tier，1-4）

---

## 10. 建筑与AI建设

### 10.1 AI 建筑优先级

**文件**：`Models/NewBuildingConstructionModel.cs`

`GetNextBuilding`：按优先级选择下一个建筑：
1. 兵营 (SettlementBarracks / CastleBarracks)
2. 城墙 (Fortifications)
3. 训练场 (TrainingFields)
4. 粮仓 (Warehouse / Granary)
5. 水利 (Waterworks)
6. 市场 (Marketplace)
7. 税务/农田 (TaxOffice / Farmlands)

`GetNextDailyBuilding`：有未满级主建筑时，不进行任何日常项目

### 10.2 AI 建设加速

**文件**：`Models/AIBuildingAutoBoostBehavior.cs`

每周 AI 定居点（非玩家）自动投入 10000 金币加速建筑，条件：
- 建筑队列非空
- 未已有加速投入
- Clan 金库 ≥ 50000

### 10.3 建筑每日 Tick

**文件**：`Patch/BuildingsCampaignBehavior.cs`

Harmony Prefix 替换 `BuildingsCampaignBehavior.DailyTickSettlement`：
- AI 定居点每日判定是否修建新建筑
- 1% 概率切换日常项目
- 非玩家定居点使用自定义建筑优先级模型

---

## 11. 外交系统

### 11.1 AI 外交加速

**文件**：`Patch/DiplomaticBartersBehavior.cs`

Harmony Prefix 替换 `DiplomaticBartersBehavior.DailyTickClan`，大幅修改 AI 氏族外交决策：

| 场景 | 行为 |
|------|------|
| 独立 Clan（50%概率进入） | 50% 尝试与随机独立 Clan 和平；50% 尝试与交战王国和平 |
| 有定居点的独立 Clan | 非次要派系：直接与交战王国和平；次要派系：按关系判定 |
| 王国内 Clan（20%概率） | 10% 考虑叛逃到其他王国 |
| 领主/雇佣兵加入王国（概率提升至 0.8） | 先尝试同文化王国，再尝试异文化王国；独立 Clan 加入前检查是否有任何敌人 |
| 王国内 Clan（40%概率） | 考虑离开王国 |
| 独立 Clan（剩余概率） | 考虑对随机势力宣战 |

关键改动：加入王国的概率从原版 0.4 提升到 0.8，加速政治整合

### 11.2 封地交易

**文件**：`Patch/FiefBarterBehavior.cs`

Harmony Prefix 替换 `FiefBarterBehavior.CheckForBarters`：
- 玩家为君主时，可跳过 EverythingHasAPrice 技能要求直接交易封地

---

## 12. 定居点补丁

### 12.1 民兵移除保护

**文件**：`Patch/SettlementPatch.cs`

（见 7.5 节）

---

## 13. 领主生成与部队创建

### 13.1 领主部队生成

**文件**：`Patch/HeroSpawnCampaignBehaviorPatch.cs`

Harmony Prefix 替换 `HeroSpawnCampaignBehavior.ConsiderSpawningLordParties`：
- 完全重写领主部队生成逻辑
- 按 `GetHeroPartyCommandScore`（战术×3 + 领导力×2 + 侦查 + 管理 + 战斗技能）选择最佳指挥官
- 按 `CalculateScoreToCreateParty`（封地×100 - 部队数×100 + 金币×0.01）判断是否创建
- 创建时扣除 `CreationGoldCost` (=5000) 金币
- 新部队自动获取附近村庄的补给（食物/马匹）

---

## 14. 囚犯管理

### 14.1 俘虏售卖

**文件**：`Patch/PartiesSellPrisonerCampaignBehavior.cs`

两个 Harmony Prefix：

**OnSettlementEntered**（领主进入定居点时卖俘虏）：
- 所属 Clan 的定居点：转移全部俘虏
- 非 Clan 定居点：低价值全部转移，高阶（Retinue/Marine/Sergeant/Slave）仅 30% 概率转移
- Mercenary 俘虏不转移

**DailyTickSettlement**（每日自动卖俘虏）：
- AI 定居点每天卖 1% 的俘虏（按 Tier 升序优先卖低阶）
- 玩家定居点：超过监狱上限才卖

### 14.2 囚犯逃逸

**文件**：`Patch/PrisonerReleaseCampaignBehaviorPatch.cs`

Harmony Prefix 替换 `PrisonerReleaseCampaignBehavior.DailyHeroTick`：
- 玩家定居点：Hero 逃逸概率 = 0.01（`PlayerSettlementPrisonerEscapeChance`），近乎不可能
- 玩家部队中的 Hero 囚犯：同样 0.01
- 其他场景：保留原版逻辑（4% 基础概率 × 各种修正）

---

## 15. 常量配置

**文件**：`common/common.cs`

### CommonConstants

| 常量 | 值 | 说明 |
|------|---|------|
| `TOWN_POOR_THRESHOLD` | 1000 | 城镇贫困繁荣度 |
| `TOWN_AVERAGE_THRESHOLD` | 4000 | 城镇一般繁荣度 |
| `TOWN_RICH_THRESHOLD` | 8000 | 城镇富有繁荣度 |
| `TOWN_VERY_RICH_THRESHOLD` | 12000 | 城镇非常富有繁荣度 |
| `CASTLE_POOR_THRESHOLD` | 300 | 城堡贫困繁荣度 |
| `CASTLE_AVERAGE_THRESHOLD` | 800 | 城堡一般繁荣度 |
| `CASTLE_RICH_THRESHOLD` | 1500 | 城堡富有繁荣度 |
| `CASTLE_VERY_RICH_THRESHOLD` | 2000 | 城堡非常富有繁荣度 |
| `FIEF_WAGE_EXEMPTION_DAYS` | 28 | 封邑兵工资豁免天数 |
| `RETURN_TROOP_WAIT_CYCLE` | 3 | 归还冷却周数 |
| `FIEF_TROOP_MAX_SERVICE_CYCLE` | 5 | 封邑兵最大服役周数 |
| `TownProsperityCostPerTier` | 8 | 城镇每 Tier 繁荣度成本 |
| `CastleProsperityCostPerTier` | 4 | 城堡每 Tier 繁荣度成本 |
| `VillageHearthCostPer` | 1 | 村庄每招募 1 人 Hearth 消耗 |
| `VillageMinHearthThreshold` | 100 | 村庄最低 Hearth |
| `VillageMaxReinforcementHearthThreshold` | 900 | 村庄满补员 Hearth |
| `ProsperityWeight` | 0.4 | 繁荣度在补员公式中的权重 |
| `HearthsWeight` | 0.6 | 户数在补员公式中的权重 |
| `AI_FIEF_PROSPERITY_IMPACT_MULTIPLIER` | 0.1 | AI 封邑繁荣度影响系数 |
| `AI_FIEF_HEARTH_IMPACT_MULTIPLIER` | 0.1 | AI 封邑户数影响系数 |
| `VLANDIA_VOLUNTEER_GENERATION_MULTIPLIER` | 0.2 | 瓦兰迪亚志愿兵生成倍率 |
| `EMPIRE_VOLUNTEER_GENERATION_MULTIPLIER` | 0.6 | 帝国志愿兵生成倍率 |
| `ASERAI_VOLUNTEER_GENERATION_MULTIPLIER` | 0.7 | 阿塞莱志愿兵生成倍率 |
| `PlayerSettlementPrisonerEscapeChance` | 0.01 | 玩家定居点囚犯逃逸概率 |

### GarrisonConstants

| 常量 | 值 | 说明 |
|------|---|------|
| `BaseGarrisonSize` | 100 | 驻军基础人数 |
| `TownGarrisonBonus` | 50 | 城镇驻军加成 |
| `BarracksGarrisonBonus` | 20 | 军营加成 |
| `TrainingFieldGarrisonBonus` | 20 | 训练场加成 |

### ClanPartySpawnConstants

| 常量 | 值 | 说明 |
|------|---|------|
| `MinGoldToChargeCreationFee` | 50000 | Clan 创建部队最低金库 |
| `CreationGoldCost` | 5000 | 创建部队扣除金币 |

### 外交系统常量（繁荣度 → 战争倾向）

| 常量 | 值 | 说明 |
|------|---|------|
| `PROSPERITY_SCORE_POOR` | -0.8 | 贫困 → 强烈反战 |
| `PROSPERITY_SCORE_AVERAGE` | -0.4 | 一般 → 略微反战 |
| `PROSPERITY_SCORE_RICH` | +0.3 | 富有 → 倾向战争 |
| `PROSPERITY_SCORE_VERY_RICH` | +0.5 | 非常富有 → 强烈主战 |
| `LANDLESS_CLAN_WAR_PROPENSITY` | +0.6 | 无封地氏族 → 倾向战争 |
| `WAR_PROPENSITY_SCORE_MULTIPLIER` | 5000000 | 繁荣度倾向影响强度 |

---

## 16. 工具类

### ModLogger

**文件**：`Utils/ModLogger.cs`（命名空间 `ModifiedArmy.Tool`）

日志级别：Debug → Info → Notice → Warn → Error

当前最低显示级别：`Notice`（Debug/Info 不显示）

颜色编码：Debug=淡青绿, Info=浅蓝, Notice=淡黄, Warn=橙黄, Error=红

### Settings

**文件**：`Utils/Settings.cs`（已注释）

基于 MCM (Mod Configuration Menu) 的设置界面，包含：
- 囚犯逃逸概率
- 招募成本（城镇/城堡/村庄）
- 补员效率阈值
- 日志级别

当前全部注释，所有值硬编码在 `CommonConstants` 中。

---

## 17. XML 配置数据

配置文件目录：`D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ModifiedArmy\ModuleData\`

### 17.1 BasicTroopGroups（基础兵种配置）

**文件**：`basic_troop_config.xml`

每个文化定义 5 类基础兵种（Retinue/Sergeant/Marine/Militia/Slave），每种兵带 `barracksLevel` 属性。

| 文化 | Retinue | Sergeant | Marine | Militia | Slave |
|------|---------|----------|--------|---------|-------|
| **Empire** | imperial_cataphract (b3) | veteran_infantryman (b1), menavliaton (b1), veteran_archer (b2), crossbowman (b2) | empire_marine_t3 (b2) | imperial_recruit (b0) | — |
| **Aserai** | aserai_faris (b3) | aserai_archer (b2), aserai_infantry (b1), mameluke_axeman (b2), mameluke_regular (b3) | aserai_marine_t4 (b2) | aserai_recruit (b0) | *(注释)* |
| **Sturgia** | druzhinnik (b3) | berzerker (b2), archer (b2), spearman (b1), hardened_brigand (b2) | sturgia_marine_t3 (b2) | sturgian_recruit (b0) | — |
| **Vlandia** | vlandian_knight (b3) | billman (b1), light_cavalry (b2), swordsman (b1), hardened_crossbowman (b2) | vlandian_marine_t4 (b2) | vlandian_recruit (b0) | — |
| **Battania** | battanian_hero (b3) | veteran_skirmisher (b1), falxman (b1) | battanian_marine_t4 (b2) | battanian_volunteer (b0) | — |
| **Khuzait** | khuzait_torguud (b3) | archer (b2), spear_infantry (b1), horseman (b1) | — | khuzait_nomad (b0) | — |
| **Nord** | nord_jarlsmann (b3) | spear_warrior (b1), marksman (b2) | — | nord_youngling (b0) | — |

> `b` = barracksLevel，表示所需最低兵营等级

### 17.2 FiefPartyTemplates（封邑部队模板）

**文件**：`fiefPartyTemplates.xml`

每个文化按 `{culture}_{town|castle}{_port?}` 定义 2-3 个模板。关键数值：

| 文化 | 定居点类型 | baseLimit | villageBonus | minWeekly | maxWeekly | Retinue权重 | Sergeant权重 | Marine权重 | Slave权重 | Militia权重 |
|------|-----------|-----------|-------------|-----------|-----------|------------|-------------|-----------|----------|------------|
| **Empire** | town_port | 40 | 10 | 5 | 10 | — | 2 | 1 | — | 2 |
| | town | 40 | 10 | 5 | 10 | — | 1 | — | — | 1 |
| | castle | 40 | 10 | 5 | 10 | 1 | 1 | — | — | 3 |
| **Aserai** | town_port | 50 | 10 | 5 | 10 | — | 2 | 1 | — | 2 |
| | town | 50 | 10 | 5 | 10 | — | 1 | — | — | 1 |
| | castle | 40 | 10 | 5 | 10 | 1 | — | — | 1 | 3 |
| **Sturgia** | town_port | 90 | 20 | 10 | 20 | — | 2 | 1 | — | 2 |
| | town | 90 | 20 | 10 | 20 | — | 1 | — | — | 1 |
| | castle | 90 | 20 | 10 | 20 | 1 | 1 | — | — | 3 |
| **Vlandia** | town_port | 120 | 30 | 10 | 30 | — | 2 | 1 | — | 2 |
| | town | 120 | 30 | 10 | 30 | — | 1 | — | — | 1 |
| | castle | 80 | 25 | 10 | 30 | 1 | 1 | — | — | 3 |
| **Battania** | town_port | 80 | 30 | 10 | 20 | — | 2 | 1 | — | 2 |
| | town | 70 | 30 | 10 | 20 | — | 1 | — | — | 1 |
| | castle | 70 | 30 | 10 | 20 | 1 | — | — | — | 4 |
| **Khuzait** | town_port | 70 | 20 | 10 | 20 | — | 1 | — | — | 4 |
| | town | 70 | 20 | 10 | 20 | — | 1 | — | — | 4 |
| | castle | 70 | 40 | 10 | 20 | 1 | 1 | — | — | 3 |
| **Nord** | town_port | 70 | 20 | 10 | 20 | 1 | 2 | — | — | 7 |
| | town | 70 | 20 | 10 | 20 | 1 | 2 | — | — | 7 |
| | castle | 70 | 20 | 10 | 20 | 1 | 2 | — | — | 7 |

**文化差异总结**：

| 文化 | 特色 | baseLimit | villageBonus | 补员速度 | Retinue 出现 | Marine | Slave |
|------|------|-----------|-------------|---------|-------------|--------|-------|
| **Vlandia** | 封建最强，兵力上限最高 | 80-120 | 25-30 | 最快(10-30) | Castle | ✓ | — |
| **Sturgia** | 次强封建，兵力上限高 | 90 | 20 | 快(10-20) | Castle | ✓ | — |
| **Battania** | 中等，依赖村庄 | 70-80 | 30 | 中(10-20) | Castle | ✓ | — |
| **Khuzait** | 中等，民兵占比极高 | 70 | 20-40 | 中(10-20) | Castle | — | — |
| **Nord** | 中等，民兵占比极高 | 70 | 20 | 中(10-20) | Town+Castle | — | — |
| **Aserai** | 较弱封建，但城堡有军事奴隶 | 40-50 | 10 | 慢(5-10) | Castle | ✓ | Castle |
| **Empire** | 最弱封建，武德不堪 | 40 | 10 | 最慢(5-10) | Castle | ✓ | — |

**设计意图**：
- Vlandia/Sturgia = 西欧/东欧封建制，封邑兵多且强
- Empire = 拜占庭式，封邑弱但志愿兵多（生成倍率 0.6）
- Aserai = 阿拉伯式，封邑弱但依赖军事奴隶+雇佣兵+志愿兵
- Khuzait/Battania/Nord = 中等封建，民兵占比高

### 17.3 submodule_strings.xml（本地化文本）

**文件**：`submodule_strings.xml`

定义所有 `GameTexts.FindText()` 使用的本地化字符串，包含变量占位符：

| string id | 用途 | 关键变量 |
|-----------|------|---------|
| `str_modifiedarmy_initialization_complete` | Mod 初始化完成 | — |
| `str_modifiedarmy_fief_init_complete` | 封邑初始化 | {COUNT} |
| `str_modifiedarmy_fief_template_registered` | 模板注册 | {TEMPLATE_ID} |
| `str_modifiedarmy_fief_recruit_to_party` | 征召封邑兵 | {PARTY_NAME}, {SETTLEMENT_NAME}, {RETINUE}, {SERGEANT}, {MARINE}, {SLAVE}, {MILITIA}, {PROSPERITY_COST}, {HEARTH_COST} |
| `str_modifiedarmy_fief_return_result` | 归还封邑兵 | {PARTY_NAME}, {SETTLEMENT_NAME}, {RETURNED_COUNT}, {PROSPERITY_COST}, {HEARTH_COST}, {RETINUE_COUNT}/{MAX_RETINUE}, ... |
| `str_modifiedarmy_fief_weekly_reinforcement` | 每周补员 | {SETTLEMENT_NAME}, {RETINUE}, {SERGEANT}, ... |
| `str_modifiedarmy_wage_exemption_added` | 工资豁免添加 | {PARTY_NAME}, {COUNT}, {DAYS} |
| `str_modifiedarmy_wage_exemption_cleared` | 工资豁免清除 | {PARTY_NAME} |
| `str_modifiedarmy_wage_exemption_consumed` | 工资豁免消耗 | {PARTY_NAME}, {CONSUMED}, {REQUESTED} |
| `str_modifiedarmy_ai_recruit_behavior_loaded` | AI 行为加载 | — |

> 菜单文案（FiefMenuBehavior 中的文化描述）使用内联 TextObject，不在 XML 中定义

---

## 18. 待实现/改进项

### 高优先级

1. **文化差异化机制** — 当前各文化只有兵种池不同，机制层面完全一样
   - 建议在 `FiefPartyTemplate` 中增加文化特质字段（服役周期倍率、冷却周期倍率、补员加成等）
   - 文件位置：`Models/Fief/FiefPartyTemplate.cs`

2. **封邑兵忠诚度系统** — 封邑兵缺少行为约束
   - 建议在 `FiefPartyData` 中增加 `Loyalty` 字段
   - 低忠诚度 → 拒绝服役/逃兵/叛变
   - 文件位置：`Models/Fief/FiefPartyData.cs`

3. **Hero 差异化志愿兵** — 所有 Hero 的志愿兵来源相同
   - 建议根据 Hero 的技能/性格调整志愿兵质量和类型
   - 文件位置：`Models/NewVolunteerModel.cs`

### 中优先级

4. **独立雇佣兵系统** — 当前仅修改原版酒馆雇佣兵
   - 建议新增 `MercenaryCamp` 概念
   - 文件位置：待新建 `Models/Mercenary/`

5. **工资豁免逻辑生效** — `GetTotalWage` 中的豁免逻辑已注释
   - 需要取消注释并测试
   - 文件位置：`Models/NewPartyWageModel.cs`

6. **AI 征召逻辑优化** — 当前只看 PartySizeRatio 和金钱
   - 建议增加战争状态、敌军距离、围城状态判断
   - 文件位置：`Models/Fief/AiRecruitFiefTroopsBehavior.cs`

7. **封邑驻军防守** — 封邑兵可以部分留守定居点防守
   - 文件位置：`Models/Fief/FiefPartyData.cs`

### 低优先级

8. **装备质量与建筑挂钩** — 封邑兵装备质量与锻造坊/马厩等级关联

9. **MCM Settings 启用** — 将硬编码常量迁移到可配置设置

10. **FiefSettlementTaxModel 启用** — 封邑制度下城镇税收调整为原版10%

11. **GarrisonRecruitFromPrisonersBehavior 启用** — 驻军从俘虏自动招募

---

## 代码行数参考

| 文件 | 行数 | 说明 |
|------|------|------|
| `Models/Fief/FiefPartyData.cs` | ~2252 | 最大的文件，封邑核心逻辑 |
| `Models/NewVolunteerModel.cs` | ~941 | 志愿兵系统 |
| `Patch/DiplomaticBartersBehavior.cs` | ~180 | AI 外交补丁 |
| `Models/NewPartyWageModel.cs` | ~280 | 工资系统（含注释的 GetTotalWage） |
| `Models/NewBasicTroopManager.cs` | ~280 | 兵种管理 + SoldierTypeClassifier |
| `Models/Fief/FiefPartyManager.cs` | ~230 | 封邑管理器 |
| `Models/Fief/FiefPartyTemplate.cs` | ~180 | 封邑模板 |
| `Models/Fief/FiefMenuBehavior.cs` | ~250 | 封邑菜单 |
| `Models/Fief/FiefWageExemptionManager.cs` | ~190 | 工资豁免 |
| `Patch/HeroSpawnCampaignBehaviorPatch.cs` | ~160 | 领主生成 |
