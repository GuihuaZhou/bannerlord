# 给集成 Worker 的可直接使用的执行指令

> 对 `GuihuaZhou/bannerlord` 中 `ModifiedArmy` 执行**渐进式架构重构**，不是重写军事玩法。基准检查仓库 `main@66c75728172c057af22256b8ccd69d3186617dd3`，`ModifiedArmy` 最近修改为 `12299c7df148b46952bd859a51ba7be3557ae319`。开工时获取最新 HEAD 并复核差异；遇到上游新修改，不准强行覆盖。

**需求已确认**：一个 DLL、业务领域组织、`PartyFinance` 并入 `PartyManagement`、Harmony Patch 薄适配、模型无副作用、真实执行有状态且可追踪、Campaign 状态隔离、退役旧增援 Service。暂不修改 `Main.cs` 赠送玩家99000金币和装备的逻辑，暂不修改 `ModifiedArmy/ARCHITECTURE.md`、`ModifiedArmy/tools/Test-LocalizationPlaceholders.ps1`。所有游戏规则数值、AI 策略、封邑服役、兵源生产、征粮保障、中文本地化既有键均保持不变。

## 顺序

1. 阅读 `00` 总索引并建立对照基线与四类存档备份。
2. 实施 W1 旧增援 Service 清理，隔离旧 SaveId 8，不更改新运输 SaveId 9；独立编译测试。
3. 实施 W2 操作状态契约，准确处理 `PartiallyCommitted` 和幂等性；测试守恒与重复执行。
4. 实施 W3 Campaign Scope，区分持久化与临时状态，测试同进程跨存档。
5. W5 将 PartyFinance 实体迁到 PartyManagement（目录后命名空间），W4 将大 Patch 内业务迁到对应 Service，要求每组单独验收。
6. W6 在稳定基线下拆 SupplyPartyBehavior、AiForcePlanningBehavior、FiefPartyManager 和驻军招募模块；只做职责分离，算法等价。
7. 按 `07` 进行联合 T01–T20 验收，给每一项通过/失败/环境无法验证的明确结论。

## 关键阻断条件

- 某个真实存档不能读、Saveable 编号被改、反复加载触发重复转移、兵员/粮食/金币/户数不守恒。
- Harmony Patch 的 `true/false` 返回与原版接管语义改变。
- 在压力下跳过 `PartyTransferOwnershipPolicy` 的驻军归属验证。
- 在 `SupplyPartyProvisioning` 重构时遗漏最新的出发配粮、危险暂停、粮荒回退和本地不可出发时跨城援粮机制。
- 未按用户约定的排除范围执行。

## 不允许的做法

- 全项目一次性自动替换 namespace 但不做分阶段编译。
- 因方便测试而重写 SaveDefiner 类型编号或重新创建封邑服役批次。
- 只按代码行数拆出没有职责的 Service；Patch 把业务逻辑藏到同文件另一个静态类也不算达标。
- 只做静态搜索却宣称游戏内测试已通过。

## 交付格式

- 按 Worker 编号分别提交 PR/Commit、附源代码差异清单和测试表。
- 最后以一份中文总结说明新目录、调用链、SaveDefiner/Harmony 一致性、行为对比、已知限制和剩余债务。
- 如果某阶段实现与文档建议需要偏离，必须说明新源码证据和理由，不得默默改变既定规则。
