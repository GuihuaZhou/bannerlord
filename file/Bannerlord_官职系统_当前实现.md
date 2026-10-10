# Bannerlord 官职系统当前实现

> 更新日期：2026-10-10。当前官职 UI 与任命功能暂停开发，后续优先处理大元帅主动创建军团。

## 1. 已实现功能

### 1.1 数据与存档

- 按王国文化从 `hero_offices.xml` 读取可用官职、地方席位参数和大元帅投票模式。
- 官职任命数据随存档保存并在重新加载后恢复。
- 每名 Hero 同时只能担任一个官职。
- 王国灭亡、Hero 死亡或被俘、Clan 离开王国等情况下会清理失效任命。
- 地方席位因领土减少而超额时，保留候选评分较高者并清理多余任命。

### 1.2 官职与效果

- 已实现大元帅、首席大臣、宫廷总管、税务长官、农业长官、军事长官和治安长官。
- 已实现大元帅军团影响力消耗、首席大臣地方官效果增幅、宫廷总管政治关系损失修正，以及四类地方官职效果。
- 官员所属家族按中央或地方官职获得每日影响力。
- 大元帅离任时，其本人领导的现有 Army 会立即解散。

### 1.3 任命、撤职与 AI

- 普通官职由王国统治者任命和撤职。
- 大元帅任命与撤职通过王国决议执行，并区分文化投票规则。
- 撤职补偿为 15,000 / 25,000 / 35,000，直接支付给被撤职 Hero。
- 地方官职 AI 每 28 天检查替换，候选人至少比现任高 25%。
- 军事长官仅限城堡总督；税务、农业和治安长官仅限城镇总督。

### 1.4 官职申请

- 非统治者玩家可以在官职页面申请空缺官职。
- AI Hero 可以主动申请大元帅和地方官职。
- 每个王国两份 AI 申请至少间隔 7 天，每名 AI Hero 的个人申请冷却为 28 天。
- AI 统治者自动评估申请；玩家统治者通过确认框同意或拒绝。
- 大元帅申请获准后仍进入王国决议，不直接获得官职。
- 首席大臣与宫廷总管的 AI 候选评分和 AI 主动申请仍暂缓。

### 1.5 Kingdom 官职页面

- Kingdom 页面已有位于军团与外交之间的“官职”标签。
- 左侧显示文化可用官职、图形和席位占用情况。
- 右侧显示说明、收益、全部席位、任职者头像、Clan、治理地点和空缺头像框。
- 玩家统治者可以任命和撤职；非统治者可以申请任职。
- 已实现鼠标、键盘和手柄标签切换，以及任命变化后的实时刷新。
- 已生成七套原创图形源文件；当前运行时为避免 TPAC 资源问题，界面使用原版无文字 Sprite。

### 1.6 大元帅组织军团

- 无大元帅的王国完整保留 Vanilla 军团创建路径。
- 有大元帅时，AI 大元帅即使不是 Clan Leader，也可进入本体军团目标评估。
- 只绕过本体的 Clan Leader 身份门槛，影响力、粮食、兵力、距离和可召集部队仍由本体判断。
- 玩家本人担任大元帅时保持手动控制；王国统治者始终保留创建权限。
- 大元帅必须实际领导有效 WarParty；失去部队后通过既有撤职决议处理，不静默清除官职。

## 2. 当前关键文件

- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/HeroOfficeBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/LocalOfficeAiBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalOfficeAiBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/MarshalArmyAiBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Behaviors/OfficeApplicationAiBehavior.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Services/OfficeAppointmentService.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Services/OfficeApplicationService.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/Decisions/MarshalOfficeDecision.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/UI/KingdomOfficeManagementVMMixin.cs`
- `ModifiedPolitics/ModifiedPolitics/HeroOffices/UI/KingdomOfficesPanel.xml`
- `ModifiedPolitics/ModifiedPolitics/ModuleData/hero_offices.xml`

## 3. 尚未完成或待游戏内验证

- 原创 PNG 图标尚未制作成 Bannerlord 可直接加载的 TPAC/图集资源。
- 首席大臣和宫廷总管的 AI 候选评分暂缓。
- 官职申请、AI 申请频率、各文化大元帅申请决议需要完整游戏内验证。
- 官职页面在不同分辨率下的最终布局仍需实机确认。
- 大元帅主动组建军团及无大元帅时 Vanilla 回退路径需要游戏内验证。
- 所有新增代码均按用户要求未由 Codex 编译，编译与游戏内验证由用户执行。

## 4. 相关提交

- `86c1e2a` 至 `5f4631c`：官职页面、真实数据、任命撤职与只读视图。
- `c84ecbe`：无文字官职图形。
- `1da8fc6`：玩家与 AI 官职申请。
- `ab20468`、`791485d`、`d475519`：申请反馈、地方资格和边界加固。
- `60522ba`：大元帅决议期间重新验证提名者。
