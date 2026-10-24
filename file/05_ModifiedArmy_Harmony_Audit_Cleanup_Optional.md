# W5（可选）｜Harmony 双记录审计消歧与空Postfix清理

> 状态：可选低风险独立任务；基线`main@ccd222b`。**该问题不是已确认的双重执行Bug**，也不是大量审批0的已确认原因。

## 1. 问题还原

启动日志对 `GarrisonTroopsCampaignBehavior.OnSettlementEntered` 报告两条相同记录：

```text
com.mod.ModifiedArmy (priority 400)
com.mod.ModifiedArmy (priority 400)
```

目前 `ForceManagement/Services/ForceEntryPatchAudit.cs`：

```csharp
patches.Prefixes.Concat(patches.Postfixes)
    .OrderBy(item => item.owner)
    .ThenByDescending(item => item.priority)
    .Select(item => FormatPatch(item.owner, item.priority))
```

只显示Owner和Priority、不显示Prefix/Postfix及执行方法，所以容易误以为同一方法被注册两次。

实际 `Garrison/Patches/GarrisonTroopDonationPatch.cs` 对该原版方法有一个**有功能的Prefix**和一个**空Postfix**。两条日志来自两种不同补丁类型，并不能证明重复调兵。不能因此删除Prefix或关闭`PartyExchangeBehavior`。

## 2. 修改目标

1. 删除**仅此一个**空的`GarrisonTroopDonationPatch.Postfix(MobileParty,Settlement)`；它不执行任何逻辑，只会贡献多余的Patch记录。**绝不能误删同文件`GarrisonPartyPriorityPatch`的有效Postfix**：它负责真实兵种身份转移账本，删除会造成数据错误。
2. 优化`ForceEntryPatchAudit.LogMethod()`以清楚打印：
   - patch类型：`Prefix` / `Postfix`（如果系统后来需要可补`Transpiler`和`Finalizer`，但本轮非必需）；
   - `owner`；
   - `priority`；
   - `PatchMethod.DeclaringType.FullName + MethodName`（必须null安全）。
3. 原有启动审计只打印一次，保留 `ManagedPartyPolicy.IsEnabled` 的原子开关行为；不增加每次入城日志。

## 3. 参考实现

```csharp
var descriptions = Enumerable.Empty<string>()
    .Concat(patches.Prefixes.Select(p => Format("Prefix", p)))
    .Concat(patches.Postfixes.Select(p => Format("Postfix", p)));
```

若想保留既有本地化字符串`str_modifiedarmy_force_patch_item`（仅Owner/Priority），可通过新的资源键包装`TYPE`和`METHOD`，或新增只用于FileNotice的诊断格式；保持所有现存本地化资源可查。不要把生硬英文异常暴露为玩家弹窗。

**兼容要求：** 只有`LogMethod`中的展示发生变化，禁止`Harmony.UnpatchAll()`、禁止修改优先级、禁止改已有Prefix返回值和Donating逻辑。

## 4. 精确修改文件

- `ModifiedArmy/ModifiedArmy/Garrison/Patches/GarrisonTroopDonationPatch.cs`：仅删除该类最外层空Postfix。
- `ModifiedArmy/ModifiedArmy/ForceManagement/Services/ForceEntryPatchAudit.cs`：区分Patch类型及方法名。
- `ModifiedArmy/ModifiedArmy/ModuleData/submodule_strings.xml`（仅当需要新文本资源时）。

## 5. 验收条件

1. `OnSettlementEntered`日志能明确展示 `Prefix:...GarrisonTroopDonationPatch.Prefix`，不再出现此类空Postfix对应项。
2. 对`RecruitmentCampaignBehavior.CheckRecruiting`审计仍正确；若其他Mod注册了补丁，显示不同Owner/Method而不混淆。
3. `GarrisonPartyPriorityPatch.Postfix`仍存在且合法；封邑在役身份/账本转移正确。
4. 玩家手动进入城镇、AI原版和Mod托管路径的执行次数、兵员数及历史表现无变化。
5. 日志无按兵种/Party刷屏，旧存档可加载。

建议提交：`chore(army): clarify harmony patch audit and remove no-op postfix`。可随集成验证合并，但不要把它写成“修复重复调兵”或“修复审批0人”。
