# 第三阶段：封建兵税赋减免 — ModifiedArmy / ModifiedPolitics 职责分离实施方案 V3.1

> 目标：**税收裁剪全部由 `ModifiedPolitics` 负责；`ModifiedArmy` 只提供一个公共只读接口，返回指定封邑的「应减免税额」。**
>
> 源码基准：[`GuihuaZhou/bannerlord@8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b`](https://github.com/GuihuaZhou/bannerlord/commit/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b)。截至本方案撰写，该提交为仓库最新。此文档是实施指导，尚未修改 Git 仓库或完成编译运行。

## 1. 最终职责与必须遵守的边界

| 模组 | 只负责 | 不负责 |
| --- | --- | --- |
| `ModifiedArmy`（军事修改） | 根据封邑关联村庄中**在籍封建兵**的兵种、数量、标准工资，和调用方提供的减免比例，返回**应减免金额**（未针对税额裁剪） | 获取原版城镇税收、扣除税收、裁剪至 0、注册税收模型、官职税加成、修改村庄Hearth/兵员/存档 |
| `ModifiedPolitics`（内政修改） | 持有减免比例配置；调用上述接口；先执行原版与官职税收修正；将减免额限制在可扣税额内并最终不低于0；展示明细 | 自行复制封建兵生命周期统计、重复计算标准工资、修改封建兵在役记录 |

**唯一跨模组公共入口（拟新增）：**

```csharp
// ModifiedArmy.Models.Fief.FiefPartyManager
public float GetFeudalTaxReliefAmount(Settlement fief, float wageRatio);
```

调用语义：

- `fief` 为**城镇/城堡**的 `Settlement`，不是村庄；所属村庄全部由 `FiefPartyData` 统计。
- `wageRatio` 为 `ModifiedPolitics` 传入的 `[0, 1]` 减免比例，当前默认 `0.50`。
- 返回值为单位**第纳尔/天**的非负 `float`：`在籍封建兵标准日工资总额 × wageRatio`，不使用该封邑的原税收作为上限。
- 只读；重复调用不会更改任何税额、Hearth、名册、在役批次、冷却状态或工资账户。
- 无合法封邑数据返回0；非法比例（NaN/无穷/小于0/大于1）拒绝并抛出参数异常，配置解析应事先校验。
- **减免上限、最终结果不小于0，全部在 `ModifiedPolitics` 处理**，不得放入此接口。

## 2. 已核对的源码事实

1. [`ModifiedArmy/Fief/Models/FiefSettlementTaxModel.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedArmy/ModifiedArmy/Fief/Models/FiefSettlementTaxModel.cs) 目前把原版税额直接乘0.1；[`ModifiedArmy/Main.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedArmy/ModifiedArmy/Main.cs#L168-L183) 对其注册已注释。**继续保持不注册，也不要将它重构为新的生效税收模型。** 此旧类可原样保留，不参与此阶段运行。
2. [`ModifiedPolitics/HeroOffices/Models/OfficeSettlementModels.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/OfficeSettlementModels.cs) 当前 `OfficeSettlementTaxModel : DefaultSettlementTaxModel` 已调用原版税收并用 `OfficeEffectService.GetTaxFactor(town)` 追加官职修正。它是本阶段**唯一**要编辑的生效税收模型。
3. [`ModifiedPolitics/Main.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedPolitics/ModifiedPolitics/Main.cs#L103-L111) 已注册 `new OfficeSettlementTaxModel()`，**不新增第二个税收模型**。
4. [`ModifiedPolitics/ModifiedPolitics.csproj`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedPolitics/ModifiedPolitics/ModifiedPolitics.csproj#L59-L65) 已引用 `ModifiedArmy.dll`，且 [`ModifiedPolitics/SubModule.xml`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedPolitics/SubModule.xml) 将 `ModifiedArmy` 设为必需依赖，不需要反射或Harmony桥接。
5. [`VillageFiefData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs) 持有真实就绪Roster、`ServiceBatches`、`ReturnBatches`；[`FiefPartyData.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs) 已将附属村庄集中于 `Villages`，提供 `OrderedVillages()`、`GetFiefPartyTemplate()`。
6. [`NewPartyWageModel.GetCharacterWage(CharacterObject)`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedArmy/ModifiedArmy/PartyManagement/Finance/Models/NewPartyWageModel.cs#L372-L410) 已按Tier定义标准工资；应直接从 `Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop)` 取得**当前实际生效的标准单兵工资**，不再硬编码第二套工资表。
7. [`ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs) 与 [`ModifiedPolitics/ModuleData/settlement_economy.xml`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml) 共同管理内政经济配置。**税赋减免比例在此配置，不放入军制 XML。**
8. 原版 [`DefaultClanFinanceModel.AddSettlementIncome()`](https://github.com/GuihuaZhou/bannerlord/blob/8375e3ba6d7c22ab600ce43dfa5a4562f5c5f53b/code/DefaultClanFinanceModel.cs#L318-L341) 读取 `Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(...)`；因此仅编辑 `OfficeSettlementTaxModel` 即会影响主封邑城镇/城堡税收，无需再改财务结算或扣款事件。原版村庄市场收入由另一路 `CalculateVillageIncome` 计算，本阶段**不直接裁剪该通道**。

## 3. 本次涉及文件与修改顺序

| 顺序 | 文件 | 具体操作 |
|---|---|---|
| 1 | `ModifiedArmy/ModifiedArmy/Fief/Models/VillageFiefData.cs` | 新增一个 `internal`、只读的在籍封建兵按兵种聚合方法 |
| 2 | `ModifiedArmy/ModifiedArmy/Fief/Models/PartyData/FiefPartyData.cs` | 汇总所有关联村庄的标准日工资，并乘外部传入比例 |
| 3 | `ModifiedArmy/ModifiedArmy/Fief/Models/FiefPartyManager.cs` | **仅新增一个 `public` 跨模组入口** `GetFeudalTaxReliefAmount` |
| 4 | `ModifiedPolitics/ModifiedPolitics/ModuleData/settlement_economy.xml` | 增加内政税减比例配置，默认 `0.50` |
| 5 | `ModifiedPolitics/ModifiedPolitics/SettlementEconomy/Config/SettlementEconomyConfig.cs` | 增加该比例读取、范围检查及Manager暴露 |
| 6 | `ModifiedPolitics/ModifiedPolitics/HeroOffices/Models/OfficeSettlementModels.cs` | 保留原税与官职加成，再调用军事接口并真正扣减、限额裁剪 |
| 7 | `ModifiedPolitics/ModifiedPolitics/ModuleData/Languages/CNs/modifiedpolitics-zho-CN.xml`（按现有本地化组织） | 增加「封建兵税赋减免」的显示文本键；如果采用内联本地化文本，则不必新增键 |

**无需修改：** `ModifiedArmy/Main.cs`、`ModifiedPolitics/Main.cs` 的税收模型注册、`FiefSettlementTaxModel.cs`、Hearth供养/职业兵扣户/在役对账/归还事务。不得引入新的税收Behavior、每日账本、Harmony补丁或重复的封建兵实体。

## 4. ModifiedArmy：如何实现只读金额接口

### 4.1 `VillageFiefData` — 获取三状态在籍人数

新增内部方法（示例；按项目命名风格调整）：

```csharp
internal Dictionary<CharacterObject, int> GetRegisteredFeudalTroopCounts(
    FiefPartyTemplate template)
{
    var counts = new Dictionary<CharacterObject, int>();

    void Add(CharacterObject troop, int count)
    {
        if (troop == null || count <= 0) return;
        counts[troop] = counts.TryGetValue(troop, out int old)
            ? checked(old + count) : count;
    }

    // 仅从村庄原生就绪Roster中筛选受当前封建兵模板认可的兵种。
    foreach (var entry in FilteredEntries(template))
        Add(entry.Character, entry.Number); // Number已包含伤员

    // 在役、冷却批次本身是封建兵身份的权威记录，不再按当前模板过滤。
    foreach (var batch in ServiceBatches ?? Enumerable.Empty<FiefServiceBatch>())
        if (batch != null)
            foreach (var pair in batch.Troops)
                Add(pair.Key, pair.Value);

    foreach (var batch in ReturnBatches ?? Enumerable.Empty<FiefReturnBatch>())
        if (batch != null)
            foreach (var pair in batch.Troops)
                Add(pair.Key, pair.Value);

    return counts;
}
```

**注意：** 方法只是读取现有三处数据，不调用重新对账/升级/生成兵员；每个批次只统计一次，特别不能再额外把 `_fiefParty` 汇总快照加入一次。伤兵已包含在 `TroopRosterElement.Number` 内，不能额外叠加 `WoundedNumber`。当来源村庄仍有在役批次时，仍按来源村庄归属统计，部队转移不改变减免归属。

### 4.2 `FiefPartyData` — 只计算标准工资与应减免额

新增 `internal` 方法，使用既有 `OrderedVillages()` 和 `GetFiefPartyTemplate()`：

```csharp
internal float CalculateFeudalTaxReliefAmount(float wageRatio)
{
    var wageModel = Campaign.Current?.Models?.PartyWageModel;
    if (wageModel == null || _fiefPartyTemplate == null) return 0f;

    long dailyWages = 0;
    foreach (VillageFiefData village in OrderedVillages())
    {
        foreach (var pair in village.GetRegisteredFeudalTroopCounts(
                     _fiefPartyTemplate))
        {
            int unitWage = Math.Max(0, wageModel.GetCharacterWage(pair.Key));
            dailyWages = checked(dailyWages +
                                 (long)unitWage * pair.Value);
        }
    }
    return (float)(dailyWages * (double)wageRatio);
}
```

说明：这里应使用游戏**实际启用的单兵标准工资模型**，而不是 `GetTotalWage`，因为后者混入临时免薪、战时倍率、Party修正等，与税赋名义减免无关。禁用重复遍历城堡驻军、领主Party普通士兵或当前 `GetTotalTroopCount` 整数总量（丢失Tier信息）。

### 4.3 `FiefPartyManager` — 唯一对外接口

```csharp
public float GetFeudalTaxReliefAmount(Settlement fief,
    float wageRatio)
{
    if (float.IsNaN(wageRatio) || float.IsInfinity(wageRatio) ||
        wageRatio < 0f || wageRatio > 1f)
        throw new ArgumentOutOfRangeException(nameof(wageRatio));

    if (fief?.IsFortification != true || wageRatio == 0f)
        return 0f;

    FiefPartyData data = GetFiefData(fief);
    return Math.Max(0f,
        data?.CalculateFeudalTaxReliefAmount(wageRatio) ?? 0f);
}
```

注意工程中 `System`、`TaleWorlds.CampaignSystem.Settlements` 引用应沿用原文件。**这个接口不接收当前税额，不调用 `SettlementTaxModel`，不裁剪最终税收**。对没有封建兵资料的城镇或城堡返回0。

## 5. ModifiedPolitics：配置、扣税和结算完全在内政侧

### 5.1 内政 XML 配置

在当前 `ModuleData/settlement_economy.xml` 的 `SettlementEconomyConfig` 下新增同级节点：

```xml
<SettlementEconomyConfig id="default">
    <Food>
        <ProsperityConsumptionDivisor value="34" />
    </Food>
    <Tax>
        <FeudalTaxRelief wageRatio="0.50" />
    </Tax>
</SettlementEconomyConfig>
```

`wageRatio` 是**减免比例**，不是税收乘数；有效区间 `[0,1]`。配置 `0` 表示关闭减免。将这个参数暴露为 `SettlementEconomyConfigManager.Instance.ActiveFeudalTaxReliefWageRatio`，而**不修改 `ModifiedArmy/common/MilitaryConfig.cs` 和 `militaryConfig.xml`**。

建议具体落点：

- `SettlementEconomyConfig` 添加 `public float FeudalTaxReliefWageRatio { get; private set; } = 0.50f;`。
- 在 `Deserialize(...)` 读取 `Tax/FeudalTaxRelief/@wageRatio`，使用 `CultureInfo.InvariantCulture`；值缺失则默认0.50，值存在但不是有限数或不在0～1则报错，不静默解释成另一个比例。
- `SettlementEconomyConfigManager` 添加对应的私有有效值和公开只读属性，`Register` 与 `Clear()` 同时维护。保留原有 `ProsperityConsumptionDivisor=34` 的配置行为不变。

### 5.2 修改唯一生效的 `OfficeSettlementTaxModel`

**保持继承 `DefaultSettlementTaxModel`，不需要改成继承 `FiefSettlementTaxModel`。** 只在当前已经存在的 `CalculateTownTax` 末尾接入军事接口：

```csharp
public override ExplainedNumber CalculateTownTax(
    Town town, bool includeDescriptions = false)
{
    ExplainedNumber result = base.CalculateTownTax(town,
        includeDescriptions);

    float officeFactor = OfficeEffectService.GetTaxFactor(town);
    if (officeFactor > 0f)
        result.AddFactor(officeFactor, TaxOfficeText);

    // 在官职加成后计算当前可扣税额。
    float taxBeforeRelief = Math.Max(0f, result.ResultNumber);
    float ratio = SettlementEconomyConfigManager.Instance
        .ActiveFeudalTaxReliefWageRatio;
    float requestedRelief = Campaign.Current?
        .GetCampaignBehavior<FiefPartyManager>()?
        .GetFeudalTaxReliefAmount(town?.Settlement, ratio) ?? 0f;
    float actualRelief = Math.Min(taxBeforeRelief,
        Math.Max(0f, requestedRelief));

    if (actualRelief > 0f)
        result.Add(-actualRelief, FeudalTaxReliefText);

    result.LimitMin(0f);
    return result;
}
```

配套在此模型声明：

```csharp
private static readonly TextObject FeudalTaxReliefText =
    new TextObject("{=MP_FeudalTaxRelief}Feudal troop tax relief");
```

增加 `using ModifiedArmy.Models.Fief;` 和 `using ModifiedPolitics.SettlementEconomy.Config;`。

**重要验收细节：** `ExplainedNumber.AddFactor`、`Add` 的最终组合结果必须在实际游戏版本上核对。预期税额明确是 `max(0, 原版税额经官职调整后的值 − actualRelief)`，若 `ExplainedNumber` 的内部因子机制使后追加的负数也被再次放大/缩小，则请调整内政模型的结果构建方式，保证**最终数值正确且说明仍可用**；不要为了凑算术更改军事接口或重复在其他结算路径扣税。

### 5.3 不修改独立村庄收入通道

本方案将相应封建兵减免应用到**其所属城镇/城堡的 `CalculateTownTax`**。原版 `DefaultClanFinanceModel.CalculateVillageIncome` 作为村庄独立收入通道保持不变，不要再对同一名封建兵通过村庄税再次减免，否则会双扣。

## 6. 示例与验收条件

约定标准日工资来自当前 `NewPartyWageModel`：T5工资24；T4工资16；T6工资34。XML减免比例50%。

| 案例 | `ModifiedArmy` 返回请求减免 | `ModifiedPolitics` 实际扣减 |
|---|---:|---:|
| 20名T5，税额1000 | 240 | 240，最终760 |
| 20名T5，税额200 | 240 | 200，最终0 |
| 20名T5，税额0 | 240 | 0，最终0 |
| 10名T4 + 10名T5，税额1000 | 200 | 200，最终800 |
| 0名封建兵 | 0 | 0，保持原版及官职税收 |
| 20名T5，其中5名服役、5名冷却、10名待役 | 240 | 不因兵员状态变化而重复计算 |
| 20名T5，XML比例0 | 0 | 0，税收不变 |
| 20名T5，XML比例1 | 480 | 不超过可扣税额 |

验收还应覆盖：

1. **接口只读**：同一天重复调用10次，返回值一致，Hearth、兵员、冷却批次、任何收入都不发生变化。
2. **出征/返乡**：待役→在役→冷却→就绪，同一批兵员始终仅计一次、来源属于原村庄。
3. **战损/升级**：真实战损后的在役人数减少，升级后按新兵种 `GetCharacterWage` 计算；伤员不重复计算。
4. **官职加成**：同时启用税官与封建减免，最终数值符合“原税→官职增益→名义减免裁剪”的顺序。
5. **UI与真实收入**：`includeDescriptions=true/false` 得到相同最终税额；家族收入和内政封邑财政明细使用同一 `SettlementTaxModel`，不存在二次扣税。
6. **边界**：没有封建兵、税额为0、税额不足减免额，均不会增税或产生负税。
7. **配置**：0、0.5、1可用；负值、非数字、NaN、超过1明确拒绝或进入已有配置加载失败/默认策略，不产生模糊部分加载。
8. **模组协作**：只加载ModifiedArmy时军事接口可用但不会裁剪税收；加载ModifiedPolitics时仅有一个有效税收模型 `OfficeSettlementTaxModel`。

## 7. 编译顺序、交付及禁止扩展

1. **先编译 `ModifiedArmy`**，确认新的 `FiefPartyManager.GetFeudalTaxReliefAmount(Settlement,float)` 在DLL中公开可调用。
2. 再按照仓库 `ModifiedPolitics.csproj` 的引用方式更新目标环境中的 `ModifiedArmy.dll`，随后**编译 `ModifiedPolitics`**。
3. 游戏内启动验证实际税收模型类型、同日税收、UI说明和家族收入。
4. 保留 `ModifiedArmy/Main.cs` 原本注释的 `FiefSettlementTaxModel` 注册；**不要开启它**。无需修改两模组 `Main.cs` 的模型注册行。
5. 不新增新的 `FiefTaxSaveData`、工资账本、Hearth操作、历史存档迁移、村庄重新绑定逻辑、定时税收修正Behavior。

### 明确的完成定义

- `ModifiedArmy` 的新增公共能力只有：**返回应减免金额**；它不知道现有税收是多少。
- `ModifiedPolitics` 的税收计算链拥有配置、扣减、归零裁剪和说明；不复制兵员业务。
- 50%减免来自内政XML，标准工资来自当前生效的 `PartyWageModel`，封建兵人数来自现有村庄生命周期权威记录。
- 不新增平行的税收模型或第二条实际扣款路径。
