# S7｜残余 Patch 与跨 Mod 依赖

## 公共约束（逐条执行）

- 仓库 `GuihuaZhou/bannerlord`，参考基线 `a3d46c90c9328bc776a5d480b7997d6d7c09613d`。开始先 `git fetch`、记录 `BASE_SHA`、`git status --short`，不得硬性 reset 到旧 SHA；如果上游变化需出对比。
- 首先阅读同包的 `00_总体方案与实施顺序.md`，本文件是**唯一可实施阶段**，不要顺手执行后续阶段。
- 不触碰 `ModifiedArmy/ARCHITECTURE.md`、`ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`；不改变 `Main.cs` 的 99000 金币及装备、新游戏奖励触发行为。
- 不改变 SaveDefiner/Saveable 类型及字段编号、原 SyncData 键，尤其旧增援 8 / 新运输 9。保留 Harmony 目标/拦截返回、兵源数值、文化规则、征粮/封邑规则和授权。
- 只报告实测过的内容；未运行 Bannerlord 就不能声称游戏测试通过。末尾交付必须有 Base/New SHA、文件差异、每条工作项、PASS/FAIL/BLOCKED、日志/复现证据、Rollback、NEXT_PHASE_ALLOWED。

### 具体任务


1. 残余 `Mercenary/Patches/MercenaryMarketPatch.cs`：提取概率/兵种/数量/日志到 `MercenaryMarketService`，保留 Prefix 拦截语义；当前代码商队护卫 `return false` 未更改市场的疑点单独登记缺陷，**未核实前不可在架构 PR 中变更玩法**。
2. `PartyManagement/Finance/Patches/ClanFinancialEvaluationPatch.cs`：按需将工资限额评估与日志抽到领域 Service，保持游戏规则和原版拦截。
3. 冻结 `ForceEntryPatchAudit` 原有目标并运行时逐项检查。已轻薄的 Patch 不强迫拆空类。
4. `ModifiedPolitics`：先明确**强制依赖/可选依赖**；强制补实际依赖和构建检查，可选则必须处理运行时程序集解析，不是假装仅移动一个 Adapter 就可缺 DLL；本地 Steam DLL 引用路径的工程化改造独立小提交。
5. W1/W5 静态已完成，避免重新迁移。

## 停止与交接规则

- 一旦发现上游改动覆盖本阶段修改文件，先做差异对照再更改。
- 阶段中遇到存档载入失败、士兵/金钱/物资不守恒、Harmony 原版双执行，立即停止进一步提交到主分支并记录复现。
- 最终独立提交或 PR，不将下一阶段代码打包进本阶段 Commit。输出 `NEXT_PHASE_ALLOWED=YES/NO` 和证据。
