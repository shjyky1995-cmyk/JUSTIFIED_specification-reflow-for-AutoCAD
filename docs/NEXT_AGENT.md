# 当前接续状态（所有 Agent 共用）

更新：2026-09-26。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 当前阶段：T15/D0 已定稿，T16/D1 已开始

- 用户已明确选择固定模板生成 DOCX 的独立桌面工作台，并要求**先定整体技术框架和开发路线，再做桌面功能开发**。此路线已定稿。
- D0 文档在 `task/T15-desktop-prototype`、独立目录 `artifacts/desktop-worktree` 编写；D0 合入 main 后从 main 建 D1 任务分支。D0 只有方案文档，无桌面代码、服务或新依赖。
- 用户已委托 Agent 选择界面技术并强调炫酷前端。D0 决策为 Electron + React/TypeScript 和 .NET 10 本机工作进程，桌面 PRD 在 `docs/DESKTOP_WORKBENCH_PRD_V1.md`，ADR-013 在 ARCHITECTURE。工作进程只服务桌面界面，不连接 CAD；两程序只经用户手动保存/选择 DOCX 衔接。
- 用户已确定固定模板填写生成 DOCX，在 Word/WPS 修改保存，再由用户在 CAD `DSS` 重新选择同一份 DOCX；不增加交接请求、自动同步或文件哈希确认。桌面界面不显示说明“页数”，CAD 内部续排是图幅区域分配。正式 PRD 已按此修订。
- 0925 测试 PDF 确有越界和顶部碰框；现行 1.1.0 几何为后续修复，修复后真实打印尚无 PDF 证据。下一步先核对当前发布包与模板、补充打印复核记录；不得把旧 PDF 当新版通过证据。

## 唯一下一动作

用户安排新版 CAD 打印后自行反馈，当前不等打印结果。T16/D1 分支 `task/T16-desktop-authoring` 在 `artifacts/desktop-worktree` 已开始；工作进程可生成并检查 DOCX，自动集成脚本通过，前端构建通过，真实 Electron 窗口及按钮流程待验证。用户要求尽快推送并讨论整体后续计划和最终成品目标；先完成该讨论，再接续 D1→D2→D3。D1 checkpoint 与已知边界见 `docs/devlog/T16.md`，不得合入 main 或当成完整产品。用户之前要求全部工作完成后设置 10 分钟关机；当前仍在开发，不执行。

原工作目录 `task/T13-installer` 的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
