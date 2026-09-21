# 任务清单
默认一次对话推进一个任务。任务范围是修改边界，不授权扩大 PRD。
领取时记录负责人、输入/输出与细分步骤到 docs/devlog/<编号>.md。
状态：待开始、进行中、阻塞、待验收、完成。无真实证据不得完成。
当前任务：T05 编号与空白审计已完成，本提交以快进合入 main。T04 DOCX 解析已完成并合入 main（6d3b77e）。
远程：[GitHub 私有仓库](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD)。M0 验收基线标签：m0-foundation；合并、推送结果通过 Git 查看。

| 编号/阶段 | 目标与输入 → 输出 | 前置 | 修改范围 | REQ / AC | 验收方式 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| T01/M0 | PRD/确认计划 → 规范、任务、日志、Git | 无 | 根文档、docs、Git 配置 | PRD 4/15/16 | 入口可接续、规则≤60行、初始提交 | 完成 |
| T02/M0 | 本机环境 → 分层骨架、构建测试、最小加载包 | T01 | src、tests、scripts、构建/打包配置、环境日志 | DEP-001～008、OPS-001 基础 | 双运行时各 4/4；编译无警告；用户返回本机 DN_DIAG_OK | 完成 |
| T03/M1 | PRD 5/9 → 模型、Active 契约、Schema、JSON | T02 | Contracts、DocumentCore、schemas、相关测试 | DATA-001 / AC-04 | 往返/扩展字段/非法版本与块/有限数值 | 完成 |
| T04/M1 | DOCX 样本 → 段落/样式/诊断解析 | T03 | DocxAdapter、脱敏 fixtures、测试 | DOC-001/002 / AC-01～03 | Word/WPS 一致、不支持内容阻断定位 | 完成 |
| T05/M1 | numbering/换行样本 → 有限编号与空白语义 | T04 | DocxAdapter、fixtures、测试 | DOC-002 / AC-01～03 | 编号重启、不重复、空段/硬换行审计 | 完成 |
| T06/M2 | CAL-02～07 → 标准校验与测量原型 | T03 | Standards、AutoCadAdapter、标准资产、测试 | STD-001、TEXT-001 / AC-05～06 | 缺标定阻断、真实字形/测量/打印证据 | 待开始 |
| T07/M2 | 文本+真实测量 → token/禁则换行 | T05、T06 | LayoutEngine、测试 | LAYOUT-001 / AC-07 | 中文临界、长 token 警告、非法断点有限退出 | 待开始 |
| T08/M3 | 行流+模板 → 固定槽、不同栏宽、续页 | T07 | LayoutEngine、测试 | LAYOUT-002 / AC-08～09 | 6/7 行边界、空行、跨栏、无空尾页 | 待开始 |
| T09/M3 | 布局+Anchor → 真实 DBText 输出 | T08 | AutoCadAdapter、宿主测试 | CAD-001 / AC-10～11 | 两 Anchor、UCS、单位、对象类型、上下标 | 待开始 |
| T10/M4 | 编排及交互 → 原子生成闭环 | T09 | Application、PluginHost、AutoCadAdapter、测试 | CAD-002、UX-001 / AC-02/11/12 | 取消/失败零残留、一次 Undo、独立重出 | 待开始 |
| T11/M4 | 完整闭环 → 限额、运行报告、离线包 | T10 | 相关实现、scripts、打包、测试、部署文档 | OPS-001 / AC-13～14 | 断网、确定性、非结构测试、冷暖性能与限额 | 待开始 |
| T12/M5 | 三图幅/业务样本 → 签收与成效记录 | T11、全部 CAL | 验收记录、脱敏样本、操作/演示文档 | AC-01～14 | 本机+外机、屏幕+打印、业务复核 | 待开始 |

M0 完成后才创建阶段标签；T02 加载不等于 AC-05～14 通过。
T03 需按既定架构细化共享契约，若改变 PRD/依赖/技术栈则先取得用户确认。
T06 标定可与独立解析任务准备资料，但默认不启动并行 Agent。
后续任务需要修改范围外文件时，先说明原因；共享架构变动执行审批规则。
