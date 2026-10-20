# ModifiedArmy：AI兵力管理专项实施索引

> **适用仓库**：`GuihuaZhou/bannerlord` · 审查基线：[`c60e8a9`](https://github.com/GuihuaZhou/bannerlord/commit/c60e8a9370250ead3ae6e1e4678716ddbfff139d)  
> **性质**：设计与实施方案，不表示已修改、已编译或已完成实机测试。编码前应再次确认仓库 HEAD。  
> **实施边界**：Model负责规则，Service负责真实Roster/库存交易，CampaignBehavior负责事件/任务及存档，Harmony Patch只桥接本体入口。禁止在Patch复制复杂政策。


## 为什么拆分

每份Markdown是**可独立交付给编码AI的实施单元**：有明确范围、当前代码、确定的设计、API/实现方法及完成条件。请勿把未实现的骨架写成“已完成”；也不要一次性执行全部模块。

## 六份专题文档

| 序号 | 独立文档 | 建议实施次序 | 范围 | 完成后可单独验收 |
|---|---|---|---|---|
| 01 | `ModifiedArmy_01_驻军编制与自动补员.md` | **第一批（P0）** | 边境95%/75%、内陆80%/50%、征粮队编制、招募/移交容量 | 是 |
| 02 | `ModifiedArmy_02_AI领主补员与封邑兵调拨.md` | **第二批（P0）** | Party场景目标、取兵寻址、封邑返乡保护、同Clan转移财政 | 是；依赖01的统一人数API |
| 03 | `ModifiedArmy_03_驻军外援需求与日志.md` | **第三批（P1）** | 当前人数请求、边境无七日预测、日志和源地安全调出 | 是；依赖01 |
| 04 | `ModifiedArmy_04_驻军局部双向换兵.md` | **第四批（P2）** | Party⇄驻军交易、兵种价值Stub、原子性与守恒 | 是；依赖01、02 |
| 05 | `ModifiedArmy_05_Clan跨城增援运输与Army保护.md` | **第五批（P3）** | 现有领主Party运兵运粮、任务/存档/保护、路线Stub | 先Fake测试，再实机；依赖01、02、03 |
| 06 | `ModifiedArmy_06_职业兵文化与规则文档同步.md` | 可并行 | 文化兵种解锁、实际兵营效率、规则文档与回归 | 是，不修改已确认正确的代码 |

**注意**：文档序号表示推荐施工顺序，不表示Runtime必须按该顺序调用。编写代码时以各专题接口为真实契约，不要求程序运行时读取索引文件。

## 横向不变量

1. **四比例单一来源**：边境上限95%下限75%，内陆上限80%下限50%。上限管自动补员与正常接收；下限只管从驻军抽调。
2. **驻军统一人数**：实际驻军名册人数 + 活跃征粮队预留人数。征粮队占编制但不可从城内再扣一次。
3. **兵数/粮食守恒**：转移不创造士兵或物品，失败不得隐式裁军；特殊收编及征粮队返还仍受硬上限约束。
4. **本体战略照旧**：战斗、围城、劫掠与Army战略归本体；增援任务只能对执行中的Party设置受限覆盖，不改本体全局战术评分。
5. **保留已工作的WarDisposition读取**：不改变 `WarDispositionBridge`、当前政治程序集引用与运行方式；战时 `0<D≤20` 固定50%。
6. **暂不开危险功能**：自动Swap与自动跨城增援维持关闭，直到相关测试全部通过。
7. **可观测**：记录文件日志、ReasonCode、实际变更数量；不在UI持续打印异常。

## 各批提交/验收要求

- 先核对最新Git提交及目标源码，再改动；对照已实现与Stub，避免重复功能。
- 每份独立提交，列出新增/修改文件、配置变更、是否改变存档格式、实测与未测项目。
- 先Model和Service后Patch；不得同时部署相同Harmony Prefix的第二套竞争实现。
- 功能尚未支持时保持安全Stub：`NoRequest`、`NoExchange`、`RouteUnsafeOrUnknown`等，不得因赶进度返回无条件true。
- 最后统一更新仓库实际实现说明；历史文档保留为历史，不作为新比例的并行权威。

## 关联当前源码与资料

- [ForceManagement契约](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Contracts/ForceManagementTypes.cs)
- [ForceManagement模型](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Models/ForceManagementModels.cs)
- [ForceManagement服务Facade](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/ModifiedArmy/ForceManagement/Services/ForceManagementFacade.cs)
- [仓库现状：驻军需求](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/file/AI兵力管理驻军需求当前实现.md)
- [职业兵文化规则](https://github.com/GuihuaZhou/bannerlord/blob/c60e8a9370250ead3ae6e1e4678716ddbfff139d/ModifiedArmy/file/兵源池与职业兵文化判定.md)
