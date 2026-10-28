# W4：Harmony Patch 薄适配层重构

## 约束

**Patch 只负责接收原版参数、确认接管范围、调用业务入口、设置返回值与必要异常边界。** 复杂兵种选择、资源操作、金币计算、任务管理、领域判断与恢复逻辑都放在同业务模块的 Service/Policy/Model。**不以行数作硬性门槛**：Harmony 特殊参数、多个嵌套 Patch、TargetMethods、Prefix/Postfix 双阶段快照可在 Patch 中保留必要接入代码。

## 源码审查后的优先级（2026-10-25）

| P | Patch | 当前行数 | 优先迁移内容 | 推荐业务入口 |
|---|---|---:|---|---|
| P0 | `Recruitment/Patches/DisbandPartyGarrisonMergePatch.cs` | 313 | 俘虏搬运、英雄转移、影响力、结算日志、解散后状态处理 | `ForceManagement/Transfers/DisbandSettlementService` + 独立结算协调器 |
| P0 | `Garrison/Patches/GarrisonTroopDonationPatch.cs` | 267 | TransferState 前后名册快照、封邑服役归属迁移、校验/记录；多目标补丁职责拆离 | `Garrison/Services/GarrisonDonationAdapter` / `Fief/Services/FiefServiceTransferReconciler` |
| P1 | `Recruitment/Patches/PrisonerSalePatch.cs` | 202 | 扣加俘虏名册、出售决策、钱物/结果记录 | `Recruitment/Services/PrisonerSaleService` |
| P1 | `Recruitment/Patches/PrisonerRecruitmentPatch.cs` | 164 | AI 俘虏筛选、招募审批、GoldAction、名册移动 | `Recruitment/Services/PrisonerRecruitmentService` |
| P1 | `Recruitment/Patches/RescuedPrisonerFilterPatch.cs` | 140 | 快照、净新增匪兵判定、移除及记录 | `Recruitment/Services/RescuedPrisonerFilterService` |
| P1 | `Garrison/Patches/KingdomSettlementItemVMPatch.cs` | 199 | 驻军 VM 文本与 tooltip 组装 | `Garrison/Presentation/GarrisonSettlementVmPresenter` |
| P2 | `PartyFinance/Patches/ClanFinancialEvaluationPatch.cs` | 106 | 财政评估/记录适配 | 随 W5 迁至 `PartyManagement/Finance` |
| P2 | `Mercenary/Patches/MercenaryMarketPatch.cs` | 90 | 雇佣兵生成规则与日志 | `Mercenary/Services/MercenaryMarketService` |

**已足够轻的补丁**如 `AiSettlementRecruitmentPatch`（31行）、`GarrisonStarvationPatch`（24行）、`NativeVolunteerRecruitmentPatch`（52行）及只做 UI 禁用的 Prefix，不强制再引入空包装服务。先查是否确有领域规则，再决定是否抽取。

## W4-A：制定接管契约，禁止语义漂移

每个 Patch 在重构前列一张表：

- Harmony 目标类型、目标方法、签名、版本；Prefix/Postfix/Finalizer、优先级和 owner。
- `Prefix` 什么时候返回 `true`（原版继续）/`false`（原版被阻止）；异常时应该怎样处理。
- `__state` 保存什么，谁使用它，是否因重入/多次调用而泄漏。
- `__result` 是否会被改写、何时改写；返回默认值与原版含义。
- 与其他补丁的冲突或顺序依赖；`ForceEntryPatchAudit.LogActivePatches()` 继续检查。

特别注意：不能将所有异常统一处理为 `return true`，这会造成原版与 Mod 双重修改；也不能一律 `return false` 阻断原版安全兜底。应逐 Patch 固定契约。

## W4-B：示例结构（只示意边界，不是可直接替换的代码）

```csharp
[HarmonyPatch(typeof(SomeNativeBehavior), "NativeEvent")]
internal static class ThinNativePatch
{
    [HarmonyPrefix]
    private static bool Prefix(MobileParty party, Settlement settlement)
    {
        return NativePatchAdapter.ShouldRunNative(party, settlement);
    }
}
```

`NativePatchAdapter` 不再隐藏另一层巨型协调器，而是清楚地调用一个可测试的 Policy / Service。对复杂 `__state`，用不可变快照 DTO 传入 Service；业务 Service 负责计算差异并提交，Patch 仅保存/传回快照。

## W4-C：逐组具体实施

1. **解散驻军合并**：保留 `DisbandPartyCampaignBehavior` 目标，提炼 `HandlePrisoners`、`TransferHeroes`、`ApplyInfluence` 和结算日志。拆出业务服务与日志格式化；Patch 仅识别 NPC/玩家/自动解散情形并委托。
2. **驻军捐兵**：保留原版 `OnSettlementEntered`、`LeaveTroopsToGarrison`、`TakeTroopsFromGarrison` 的 Harmony 目标；将捐兵前后快照核对/服务转移收口到 Fief/Reconciliation，避免在 Patch 算封邑兵归属。
3. **俘虏出售与招募**：把金币扣款、名册变动与审批放在 `Prisoner*Service`；同一实体不能既在 Patch 又在 Service 各扣一次。
4. **战后解救匪兵**：保留 `LootDefeatedPartyPrisoners` Prefix/Postfix 的游戏级时序，Service 完成前后对比并移除 **本次新增的健康匪兵**；以前就在部队里的匪兵与既有伤兵不得误删，玩家手动操作不改变。
5. **VM Patch**：将大量 Tooltip/ViewModel 文案组装移到 Presentation helper，确保 UI 更新顺序与属性注入不变，避免闭包引用已失效 VM。
6. **其他小补丁**：只做注释与签名审计，没有收益不拆。

## 注意事项与验收

- 不移动 Harmony Patch 的目标方法标识、Prefix/Postfix 优先级；不重命名会被 Patch 审计或互相反射引用的类型，除非明确更新并验证。
- 在同一存档、同一事件中，重构前后兵员、金钱、俘虏、任务状态差异应为零。
- 验证 Player/AI 分支、守城/围城分支、读档后第一次回调，以及与其他 Mod 同时打补丁的情形。
- 如 `PatchAll(assembly)` 重构后未发现补丁，必须作为阻断问题；不得仅凭编译通过完成交付。
- 任何新写的 patch 里直接出现 `MemberRoster.AddToCounts`、`GiveGoldAction`、大量 `foreach` 选择、原版任务状态赋值，都要给出正当理由，否则迁移到 Service。

## 提交方式

每个 Patch 组独立 PR：W4-P0A 解散；W4-P0B 捐兵；W4-P1A 俘虏；W4-P1B UI；W4-P2 其他。不要打包成一次“全补丁重构”。
