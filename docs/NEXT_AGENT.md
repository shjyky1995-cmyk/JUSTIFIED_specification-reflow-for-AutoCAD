# 当前接续状态（所有 Agent 共用）

更新：2026-09-26。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 当前阶段：T15/D0 桌面端方案定稿

- 用户已明确选择首批场景“离线导入工作台”，并要求**先定整体技术框架和开发路线，再做桌面功能开发**。用户选择“先比较再定”Electron 与 .NET 方案。
- 新阶段分支 `task/T15-desktop-prototype`，独立目录 `artifacts/desktop-worktree`，从已验收 `main` 建立；草案提交 `a5d73af` 已推送同名 GitHub 分支，尚未合入 main。当前只有方案文档，无桌面代码、服务或新依赖。
- 方案草案在 `docs/DESKTOP_WORKBENCH_PRD_DRAFT.md`，从根 PRD、AGENTS、ARCHITECTURE、TASKS 链接。为保留 CAD V1 已签收边界，桌面 PRD 独立记录。推荐 WPF + .NET 10 LTS 直接复用 netstandard2.0 解析核心；Electron 需要额外 .NET 工作进程与进程间通信。此推荐尚待用户确认。
- 用户已确定固定模板填写生成 DOCX，在 Word/WPS 修改保存，CAD `DSS` 直接导入同一份 DOCX；不增加交接请求或文件哈希确认。桌面界面不显示说明“页数”，CAD 内部续排是图幅区域分配。草案已按此修订。
- 0925 测试 PDF 确有越界和顶部碰框；现行 1.1.0 几何为后续修复，修复后真实打印尚无 PDF 证据。下一步先核对当前发布包与模板、补充打印复核记录；不得把旧 PDF 当新版通过证据。

## 唯一下一动作

先完成 0925 PDF 与当前包的打印复核，再与用户确认 WPF/Electron 选型并形成最终 ADR/PRD。**技术栈确认之前不要编码、引入依赖或预建桌面模块。** 定稿后按 D1→D2→D3 依次实现和验收。用户之前要求工作完成后设置 10 分钟关机；本轮在进行方案讨论，尚未到该执行点。

原工作目录 `task/T13-installer` 的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
