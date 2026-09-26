# 当前接续状态（所有 Agent 共用）

更新：2026-09-27。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 当前阶段：T17/P0 设计说明 PRD 与计划待评审

- 桌面技术路线维持 Electron + React/TypeScript、.NET 10 本机工作进程，见 ADR-013；CAD 独立运行，仅由用户手动选择最终 DOCX。D0 固定栏目需求为历史记录，用户新定的三步说明编制流程见 [PRD](PRD.md) 与 [实施计划](IMPLEMENTATION_PLAN.md)，均待评审。
- T16 代码在 `artifacts/desktop-worktree` 的 `task/T16-desktop-authoring`，提交 433ec3a 已推送。工作进程构建零警告、生成/检查测试通过；真实 Electron 窗口与业务端到端未验，不合 main。新需求确认后复用可用基础并改造表单。
- T17 文档在 `artifacts/release-worktree` 的 `task/T17-design-spec-plan`；用户确认六专业都可选并走到章节与生成，非结构跳过结构专属参数；先搭通用框架，专业正文后补；按大阶段一次验收，不排日历工期。PRD/计划/抗震数据预研已更新。Figma 账号连接有效但 Make 只返回源码目录链接，浏览器读取服务失败，当前画面未核对；历史讨论指出首页曾是占位页。
- 0925 旧测试 PDF 有越界与顶部碰框；后续 1.1.0 几何已修，真实重印结果待用户自行反馈，不阻断桌面规划，也不得宣称打印已复核。

## 唯一下一动作

继续通过可用只读途径核对 Figma 当前首页和设计说明画面；按用户补充决定收束 T17 文档，完成后交付大阶段计划供用户一次审阅。用户已指定先不写代码；审阅后才将文档合入 main，并从 T16 checkpoint 按 A→C 大阶段连续开发与验收。抗震地点数据先留「待核定」，后续优先核对官方来源/许可。新版 CAD 打印由用户方便时反馈，不阻断。用户之前要求**全部工作完成后**设置 10 分钟关机；当前仍在开发，不执行。

原工作目录 `task/T13-installer` 的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
