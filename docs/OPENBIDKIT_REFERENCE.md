# 易标源码参考记录

记录日期：2026-10-04；用于 T33 勘察设计投标规划。

- 上游：https://github.com/FB208/OpenBidKit_Yibiao
- 当前源码 SHA：f185a25bec6709c9132c4f874d5d824e93acac84。
- 本机位置：`G:\JUSTIFIED_specification reflow for AutoCAD\local\references\OpenBidKit_Yibiao`。
- 获取方式：GitHub CLI 克隆，`--depth 1`；保留上游 .git、LICENSE、NOTICE；没有完整历史。
- 使用性质：源码阅读参考。未 npm install、未运行应用、未将代码或素材复制进 EngiSpace。
- 许可文件：AGPL-3.0-only；NOTICE 有作者归属要求。具体代码移植另行核对，不将「开源」等同于可无条件复制。
- 验证：Git HEAD 与上述 SHA 一致，工作区无改动，git fsck --connectivity-only 通过。
- 首次误放 C 盘后已迁入上述 G 盘路径；C 盘残留 .git 空目录及外层空目录，文件数为 0。两次删除尝试被自动审批拒绝，工具未给更具体原因；未改用其他删除途径。之前临时阅读文件也已迁入 G 盘 local/research/openbidkit-f185a25。
- 本项目忽略 local/；外部参考不进入产品打包，不成为构建依赖。

## 实际核对的代码及采用建议

| 上游路径（相对仓库根） | 观察 | 本项目处理 |
| --- | --- | --- |
| client/src/features/technical-plan/types.ts | 解析、目录、全局事实、正文工作流；分标段及任务状态 | 借鉴状态组织，按勘察设计重新定义字段 |
| client/electron/services/bidAnalysisTask.cjs | 分项提取评分、项目要求；原文提取与经验补充分开 | 工程要求必须有原文定位，经验建议不能当招标要求 |
| sql/workspace_schema.sql | 目录节点关联 source_requirement_id；任务/章节/事实分开存储 | 采用要求—响应关系；第一阶段保留现有 JSON 路线 |
| client/electron/services/taskService.cjs | 中断章节及任务恢复处理 | 保留人工稿，恢复时校验源文件与正文版本 |
| client/electron/services/globalFactsTaskV2.cjs | 缺资料有待填及按语境补具体值的不同模式 | 只采用待填和人工核定，不补造事实 |
| client/electron/services/knowledgeBaseService.cjs | 分块、候选提取、原文关联及中断恢复 | 企业事实与历史素材分库，保留来源和有效期 |
| client/electron/services/rejectionCheckTask.cjs | 检查结果带依据/证据及无明确依据的复核提示 | 命名为响应与风险检查，不保证不被否决 |
| client/electron/services/exportService.cjs | JavaScript docx 导出服务 | 不整体移植第二套导出实现 |
| openxmlhelper/src/OpenXmlHelper/ | .NET/Open XML 文档操作 | 借鉴操作隔离；沿用本项目工作进程 |

以上为源码静态观察，不构成对易标运行稳定性、识别准确率、工程合规性或模型费用的验收。

上游变更后不要自动 pull 覆盖本轮参考基线；需要更新时记录新 SHA、变化与许可影响，再按新版本复核。
