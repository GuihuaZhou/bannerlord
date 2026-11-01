# ModifiedArmy 第四阶段：封建兵 ↔ 职业兵，复用目标兵制补员流程

**Worker 实施方案 V4.0（2026-11-01）**  
**源码基准：** `GuihuaZhou/bannerlord`，`main` 提交 `2d6dd1c83530e7317ca8aea347267f4a7df952a7`。  
**范围：** `ModifiedArmy`。不修改 `ModifiedPolitics` 税收模型，不重构第一至第三阶段的 Hearth、封建服役、税赋减免逻辑。

> 最新业务要求优先于此前的“原地身份转换”初稿：**职业兵转封建兵，走封建兵补员逻辑；封建兵转职业兵，走职业兵补员逻辑。** 因而目标兵种可由目标模板重新抽取，不要求与原兵种同种、同级，也不新建直接改身份的平行生产算法。每成功补入1名目标兵员，才从来源编制退出1名兵员；未完成数量不丢失来源兵。

## 1. 先明确本阶段行为契约

1. 每次申请包含方向、来源封邑、来源兵种与数量；封建来源还包含精确来源村庄。提交后形成**待执行申请**，每日逐步兑现，不能凭提交请求当场制造新兵。
2. 不预扣来源兵：转换在**目标兵制已有合法当日产能/Offer、支付约束与资源检查通过**后才进入提交事务。为保证职业兵扣户审批可读取释放后的供养容量，允许在单个事务内**暂时移除**待役封建兵，但任何失败必须完全恢复；对玩家而言只有成功事务才会改变身份。
3. 目标补员以既有单日**共享兵源份额、训练速度、兵种模板、兵营等级、文化效率、财政审批、驻军编制与Hearth规则**为准；转换申请不能额外发放训练额度，不得重复计算一天的Offer或新增兵额。
4. 每成功补入1名目标兵员，仅结清1名来源兵员。新兵种允许不同于源兵种；申请10名但当天目标实际补入2名，只退出源兵2名，剩余8名申请继续等待。
5. 当来源临时不在编制、当日无目标产能、无Hearth/驻军容量/财政支付能力时，不发生转出；过期/失效申请根据明确规则取消，不生成负数人口或免费兵。
6. 不返还职业兵最初永久扣除的Hearth；封建兵转出只释放**供养占用**，不恢复历史征召扣户；封建兵转职业兵仍须按**新职业兵真实Tier**永久扣Hearth。
7. 仅支持**同一封邑**（城镇/城堡及附属村庄）内的转换。**封建来源仅允许村庄就绪健康兵**；职业来源仅允许所属驻军中的健康、普通、非在役封建兵。禁止直接改写 `FiefServiceBatch`/`FiefReturnBatch`，故出征和冷却封建兵不可直接转换。
8. 玩家和AI调用同一转换接口与计算；此阶段只建设可调用机制及玩家操作入口，不扩展AI自动转换策略，避免双向自动震荡。
9. 同一封邑同一时刻只允许一个转换方向存在待执行申请；反向转换需先取消/结清旧申请，避免反复拆编的资源套利。

## 2. 从代码确认的现状与入口

| 源码位置 | 当前行为 | 本阶段挂载点 |
|---|---|---|
| `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs` `EnsureDailyProduction()`（约382–475行） | 当天只生产一次：计算职业 `professionalCount` / 封建 `fiefCount`，职业生成当日Offer，封建调用 `fiefData.ProduceDailyFiefTroops(fiefCount)`；未兑现的封建生产额度会返回进度 | 在**同一次每日生产调度**中优先尝试兑现职业→封建申请；不能另起第二次生产。 |
| 同文件 `GetProductionForecast()`（约514–607行） | 共享兵源在封建、职业之间分配，遵守文化制度、训练产能、供养可行性 | 不改原分配公式；申请只能消费已获批的目标份额。 |
| `Fief/Models/PartyData/FiefPartyReinforcement.cs` `WithdrawFiefTroopEntries()` / `PerformReinforcement()` / `ProduceDailyFiefTroops()` | 按封建模板抽取，选择有足够供养的来源村庄，更新村庄就绪Roster和计数 | 封建目标补员的**唯一实际生成路径**。最小扩展：能报告本次真实产生的 `Village + CharacterObject + Count` 结果，供转换申请逐条兑现。 |
| `Recruitment/Behaviors/GarrisonRecruitFromPrisonersBehavior.cs`（约47–165行） | 确保当日生产、收集当日职业Offer、建招募Plan，调用统一执行器 | 封建→职业申请通过这条正常执行链参与每日准入；不能额外消费Offer。 |
| `Recruitment/Services/GarrisonRecruitmentExecutionService.cs` `ExecuteDetailed()` | 按共同单日数量上限、驻军硬/软空位，调用专业兵/俘虏提交，提交经济账本；失败可滚回 | 在职业Offer正式提交的事务中协同兑现“封建来源退出”；优先在普通职业补员前处理当日转换申请，但所有人员计入**同一** `dailyLimit`。 |
| `Recruitment/Services/ProfessionalRecruitmentService.cs` `TryCommit()` / `TryRollback()` | 消费当日Offer，永久扣减目标职业兵Tier对应Hearth并加入驻军；支持失败退款和恢复Offer | 必须复用；禁止新写一个扣Hearth或写驻军Roster的职业兵生成入口。 |
| `Fief/Models/VillageFiefData.cs` `GetReadyRoster()` / `GetRegisteredFeudalTroopCounts()` | 就绪封建兵位于来源村庄Militia Party，服役/冷却存在另外两个批次 | 只对村庄就绪Roster做封建来源转出，保留受伤人数、来源村庄和当前Hearth供养余额不变量。 |
| `Fief/Models/FiefPartyManager.cs` / `FiefPartyData.cs` | 封建来源、服务/返乡与 `GetFiefData` 都由此管理；现有封建税赋接口依赖在籍兵 | 对外提供申请提交、撤销和查询；队列保存于所属 `FiefPartyData`，不设另一套人员身份表。 |
| `Fief/Models/Data/FiefServiceBatch.cs`、`FiefReturnBatch.cs` | 服役和返乡批次均有长期存档状态 | **不修改**，因为本期不允许在役/冷却直接转换。 |
| `ModuleData/militaryConfig.xml`、`common/MilitaryConfig.cs` | 含等级供养 `2/5/10/15/20/40` 与职业扣户 `2/4/6/8/10/12` | 不新增另一套等级消耗，也不修改已有配置。 |

**重要局限：** 当前驻军普通兵没有“最初是否由职业Offer生产”的长期来源凭证。因此本阶段把“职业编制兵”定义为**合法驻军中的健康普通兵，扣除 `FiefPartyManager.GetServingTroopCounts(garrison)` 所标识的封建在役份额，并限制到职业兵模板认可的兵种树**。不能声称这精确证明了士兵的历史来源；若必须按真实历史来源追溯，需另外设计持久化职业兵来源账本，不属于本期范围。

## 3. 最小持久化申请模型：只存请求，不存第二份士兵

在 `ModifiedArmy/ModifiedArmy/Fief/Models/Data/` 新增 `FiefConversionRequest.cs`，按现有 Saveable 规范：

```csharp
public enum FiefConversionDirection // 以现有存档支持方式持久化枚举或int
{
    ProfessionalToFeudal = 1,
    FeudalToProfessional = 2
}

[SaveableRootClass(/* 从 FiefSaveDefiner 取下一个空闲ID；例如13 */)]
public sealed class FiefConversionRequest
{
    [SaveableProperty(1)] public Settlement SourceFief { get; private set; }
    [SaveableProperty(2)] public Settlement SourceVillage { get; private set; } // 职业来源为null
    [SaveableProperty(3)] public CharacterObject SourceTroop { get; private set; }
    [SaveableProperty(4)] public int RemainingCount { get; private set; }
    [SaveableProperty(5)] public int DirectionValue { get; private set; }
    [SaveableProperty(6)] public string OwnerClanId { get; private set; }
    [SaveableProperty(7)] public string RequestId { get; private set; }
}
```

- `FiefPartyData` 使用**下一个未占用** `SaveableProperty` 存 `List<FiefConversionRequest>`；提供 `GetPendingConversions()`/`Enqueue`/`ConsumeCompleted`，并在 `InitialFeifPartyData`、载入修复路径填补null。不要重用旧的 `RecruitedTroopDetachmentList` 或 `ReturnedTroopDetachmentList` 存档字段。
- `FiefSaveDefiner.DefineClassTypes` 注册新类并在 `DefineContainerDefinitions` 注册 `List<FiefConversionRequest>`；如果保存枚举为int无需额外枚举容器。
- 每条申请记录**只是待执行量**，不创建/保留第二份兵员，不记录“曾经消耗的职业兵Hearth”，不参与税赋/供养统计。
- 添加申请时校验来源可用健康数，扣除同方向已排队申请量，防止超额重复预定；但**不锁住、不提前扣掉**来源Roster。执行时仍需重新校验来源还在、所有权未变、村庄仍附属、Party无危险状态、请求方向一致。所有权变化/来源村庄重绑后，取消相关请求。
- 客户端最小展示 `源兵种/来源/方向/请求总数/已完成/剩余`；已完成可以由初始数量减剩余得到，确有需要再增加初始数量字段。

**不创建独立身份表、转换凭证表、单兵ID、二次生产进度、职业Hearth返还凭证。**

## 4. 职业兵 → 封建兵：依现有封建补员发生时兑换

建议调用关系：

```
Campaign daily tick
  → SettlementManpowerProductionBehavior.EnsureDailyProduction(fief)
  → 按 GetProductionForecast 获得 fiefCount（仅一次）
  → FiefPartyData.ProduceDailyFiefTroops(fiefCount)
      → 原有封建兵模板/随机权重/村庄供养检查/就绪名册生成
      → 取得本次实际生成的 Village + Troop + Count 明细
  → ConversionService 对不超过实际生成数的待兑现职业来源申请
      → 校验驻军中健康、非封建服役的可用源兵
      → 以每1名目标生成对应退出1名来源职业兵
      → 标记同数申请完成；其余继续排队
```

**实现重点：**

- 把原方法 `ProduceDailyFiefTroops(int approvedCount)` 保持旧入口兼容，新增 `ProduceDailyFiefTroopsDetailed` 或内部回调，返回**只读本次新增清单**；不重新运行模板抽取，也不再次 `AddToCounts`。
- 当日源驻军校验以 `roster健康数 - 本驻军在役封建兵数` 为上界；不能把 `FiefServiceBatch` 中由其他村征来的兵当普通职业兵转出。必要时先 `ReconcilePartyFiefTroops(garrison)`，若在恢复/阻止状态则不兑现。
- 为保证“产出成功且来源退出成功”整体成立，提交单元保存来源Roster/目标就绪Roster前状态，只对本次新增项做精确回滚；**出现事务中断时不能把已经计入转换的目标新增兵与未转出的源兵同时保留**。如果来源在生成后失效，允许本次合法封建补员按普通生产留存、而不完成该转换申请；不得再借转换另造兵额。
- 原职业兵最初消耗的实际 Hearth **不返还**；目标封建兵按本次生成真实 `Tier` 持续占用其分配来源村庄的供养Hearth；转出职业兵不产生 `MobilizedHearth`、服务天数或冷却批次。
- 刷新驻军编制、村庄封建统计与第三阶段税赋只通过已有数据读取，税收下一次计算自动变化。

**示例：** 申请把10名驻军职业兵转为封建兵；当天按共享产能仅生成3名合法封建兵，则只退出3名职业兵、请求剩余7名。新封建兵可按模板得到不同兵种/等级。无封建生产能力时当天退出0名。

## 5. 封建兵 → 职业兵：按现有职业Offer兑现

建议调用关系：

```
GarrisonRecruitFromPrisonersBehavior 每日招募
  → pools.EnsureDailyProduction(fief)（保证当日仅生产一次）
  → pools.GetAvailableProfessionalOffers(fief)
  → RecruitmentModelManager.Model.BuildPlan(garrison, 候选Offer)
  → GarrisonRecruitmentExecutionService.ExecuteDetailed(...)
      → 优先兑现同一日、同一dailyLimit中的封建→职业申请
          → 选择具体来源村庄和健康就绪封建兵
          → 暂移除该封建兵，释放其封建供养占用
          → 原 ProfessionalRecruitmentService.TryCommit(新职业troop, count)
               （消耗真正的当日Offer、按新Tier永久扣Hearth、加入驻军）
          → 原财政费用/预算提交
          → 全部成功才递减申请；失败原路回滚来源Roster+职业Offer+Hearth+驻军+财务
      → 剩余可用Offer和剩余dailyLimit按现有普通驻军招募执行
```

**实现重点：**

- **只允许来源村庄的健康就绪封建兵转出**，由该村 `GetReadyRoster()` 中当前封建模板认可的兵种减员；不要修改 `ServiceBatches`、`ReturnBatches` 或 `MobilizedHearth`。
- 因职业兵扣户会保护现有封建兵供养，可在**同一个原子事务内先暂移除封建来源、再调用职业 `TryCommit`**；否则可能错误地因为未释放供养能力而拒绝可行转换。任何失败恢复来源。
- 新职业兵**不继承**原封建兵Tier，必须根据原有职业兵模板/Offer实际产出选择；每个新兵按该新Tier扣户，且支付原本职业兵招募费用。不得从普通驻军直接挑选兵种替代当日Offer。
- 修改 `GarrisonRecruitmentExecutionService` 为可共享一个内部的“职业Offer单次提交+财政提交”辅助流程；原普通职业兵招募和转换均调用它，避免复制付款/回滚。优先服务已排队转换但不超 `dailyLimit`、驻军硬/软空位，也不得消耗俘虏的独立来源配额以外名额。
- 若职业 `TryCommit` 成功但财政提交失败，必须调用 `ProfessionalRecruitmentService.TryRollback`，恢复当日Offer和扣除的村庄Hearth，再恢复封建来源；若恢复不能完全验证，按项目已有 `RecoveryRequired`/阻止继续提交机制记录，防止重复执行。
- 转出后的封建供养占用与税收减免自动随源名册减少；不返还职业兵既有Hearth，且**本次新职业兵实际扣户仅发生一次**。

**示例：** 申请把10名村庄待役封建兵转为职业驻军；当日实际有2个经过计划和财政准入的职业Offer，执行成功2个，则仅从来源村庄移除2名封建兵，请求剩余8名。若某Offer扣户失败，来源封建兵仍在村。

## 6. 设计中最容易出错的守恒条件

| 检查 | 转换要求 |
|---|---|
| **总人数守恒** | 单次转换提交后，来源Roster减少数 = 目标Roster增加数；失败均为0；旧的正常补员可继续按其原计划独立发生。 |
| **每日名额守恒** | 目标转换完成数 `<=` 当日合法封建生产数或职业已提交Offer数；不可因为有转换请求就第二次生产/重复扣每日进度。 |
| **封建供养** | 职业→封建按目标实际Tier增加指定村供养占用；封建→职业先释放来源村封建供养再核验职业实际扣户后的容量。 |
| **永久人口消耗** | 职业→封建**不退**原职业人口；封建→职业按新Tier及所分配村庄**永久扣**人口；失败回滚当前事务扣户。 |
| **工资和税收** | 新专业兵按正常驻军工资；新封建兵仅作为在村待役封建兵参与第三阶段减免。不直接变更财政收入，不新增减免账本。 |
| **服务身份** | 不触碰在役/冷却批次；不能转走在役封建兵；普通来源不得从同一驻军混合Roster中吃掉已被 `GetServingTroopCounts` 保留的同兵种人数。 |
| **人源与领地** | 只在同一封邑内转换，来源村庄仍附属、玩家/AI所有权合法、目标城堡/城镇仍存在；失去所有权取消请求。 |
| **保存读取** | 只保存转换申请的方向、来源、数目、所有权指纹；载入后每日重新核对，不重复执行已经完成的数量。 |

## 7. 按文件分派给Worker

1. **新增** `Fief/Models/Data/FiefConversionRequest.cs`：轻量持久化申请DTO，保存方向、来源兵种、精确村庄/驻军、剩余数量、所属家族、请求ID；不保存兵员副本。
2. **修改** `Fief/Models/PartyData/FiefPartyData.cs`：新SaveableProperty与读写/恢复队列；沿用当前 `OrderedVillages`/模板规则做申请合法性核对。
3. **修改** `Fief/Models/FiefPartyManager.cs`：对外申请/撤销/查询接口（例如 `TryRequestFiefConversion`, `CancelFiefConversion`, `GetPendingFiefConversions`），统一入口，并在城邑易主/初始化时清理失效申请；继续保留第三阶段 `GetFeudalTaxReliefAmount` 不变。
4. **修改** 同文件 `FiefSaveDefiner`：注册新类及列表容器；使用未占用的类型编号。
5. **新增** `Fief/Services/FiefTroopConversionService.cs`：只负责两种方向的单日兑现、源兵快照、明确回滚及日志；**只调用现有补员方法**，不自建训练速度、模板、Hearth扣户或财政公式。
6. **修改** `Fief/Models/PartyData/FiefPartyReinforcement.cs`：为 `ProduceDailyFiefTroops` 增加返回实际生产按村和兵种的明细/回调，保留原入口和原生产规则。
7. **修改** `Recruitment/Pools/Behaviors/SettlementManpowerProductionBehavior.cs`：在 `EnsureDailyProduction` 的**唯一**封建生产调用点接入“职业→封建”申请兑现；维持进度退还、日幂等与原始日志语义。
8. **修改** `Recruitment/Services/GarrisonRecruitmentExecutionService.cs`：在共用 `dailyLimit` 下优先处理“封建→职业”，使用现有 `RecruitmentModelManager.Model.BuildPlan` 和专业兵提交/回滚、Clan财务账本；普通职业兵和俘虏招募不发生重复消费。
9. **修改** `Fief/Models/FiefMenuBehavior.cs`：加入两个简单的玩家申请菜单与待执行查询/取消入口；使用当下已有Roster与驻军数据显示，**不可展示预测数字为已成功转换**。若UI接口实现成本高，可先提供公共服务及开发调试入口；不得仅凭日志宣称已有可用玩家菜单。
10. **语言XML**：新增提示“申请已提交/当日已完成/待执行/不满足源兵/目标补员不足/已取消”，不要产生每名兵1条冗余日志。

**明确不修改：** `FiefServiceBatch`、`FiefReturnBatch`、`NewPartyWageModel`、`ModifiedPolitics` 税收流程、`militaryConfig.xml` 既有各级成本数值，及职业兵原有生产权重、文化与兵营约束。

## 8. 验收用例（必须在游戏内验证）

1. 同城申请职业→封建10人，封建日产能3人：首日成功最多3，源驻军减3、村庄封建兵加3、剩余申请7，不额外制造士兵。
2. 职业→封建申请时村庄供养满额：转换0，源驻军兵数不变，无职业Hearth退款。
3. 封建→职业申请10人，当日职业合法Offer只有2：只完成至多2，村庄就绪封建兵只减实际完成量、驻军增加相同数。
4. 封建→职业无Hearth余量且释放源封建供养仍不足：全部拒绝且名册回滚；若源封建释放后足够则合法成功。
5. 新职业兵Tier 5按XML永久扣10户/名，不使用来源封建兵Tier的成本；职业→封建按目标兵Tier增加相应供养占用。
6. 同一角色ID同时在驻军普通兵与服役封建兵存在：普通职业来源扣除在役封建保留份额，不能偷取服务批次对应的人头。
7. 封建在役、冷却、伤兵不参与直接转出；驻军伤兵不能作为职业转出源。
8. 玩家与AI的转换使用相同产能/财政/Hearth规则，AI不会自动自行发起无限双向转换。
9. 当日生成Offer被先行普通招募用尽时，转换申请只等待，不二次生产或透支次日进度。
10. 职业/封建各自正常生产与转换申请在同一天运行，不重复消耗同一 `professionalCount`、`fiefCount` 或 `dailyLimit`。
11. `ProfessionalRecruitmentService` 提交后故障（模拟财务失败），Offer、Hearth、驻军与村庄封建Roster全部恢复，请求不错误递减。
12. 保存载入后待执行数不重复执行；封邑易主或来源村重绑会终止不合法申请。
13. 转换后税赋减免随实际封建在籍兵变化，不需要由转换服务主动“裁剪税收”。
14. 再次运行无申请的普通游戏存档，封建与职业补员数、日进度、驻军正常招募均不发生回归。

## 9. Worker 的实施与交付要求

- **先通数据流，后接UI**：先完成持久化申请/源校验、两方向生产Hook、精确事务与回滚，再补菜单。
- 严禁新增“专属转换兵源”“单独每日训练点”“转换补员权重”等并行机制；不改变原职业和封建模板权重。
- 对比 `2d6dd1c8` 的新增/修改文件，只提交本阶段必要改动；保持旧接口兼容，避免改动 `ModifiedPolitics`。
- 在可用的 Bannerlord 构建环境分别运行编译；提供编译结论、改动文件列表、实际游戏验收未覆盖项，**不要用静态检视代替编译/存档测试**。

### 简短执行指令

> 基于 `GuihuaZhou/bannerlord` 最新 `2d6dd1c8` 完成第四阶段：以持久化转换申请为调度信号，**职业兵→封建兵只消耗当日封建补员产能并调用现有封建兵补员**，**封建兵→职业兵只消耗当日职业Offer并调用现有职业兵补员/财政提交**。每成功补入1名目标兵才从来源编制精确退出1名；失败回滚名册、Offer、Hearth及财政，剩余申请不销毁兵员。不得直接重标记身份，不得新建平行补员算法；仅同一封邑内的健康就绪封建兵与驻军普通职业编制兵可转换，不触及在役/返乡批次及已有三阶段税制。

---

**状态：** 基于Git静态代码编制的实施方案；并非本阶段代码已经写入仓库，未宣称已编译或游戏验证。
