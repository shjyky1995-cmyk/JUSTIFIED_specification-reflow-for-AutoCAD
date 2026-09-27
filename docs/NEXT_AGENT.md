# 当前接续状态（所有 Agent 共用）

更新：2026-09-27。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 2026-09-27 T17 已认可并合入 main；T18/A 阶段开发中

- 用户认可 T17 设计说明 PRD 与实施计划，`task/T17-design-spec-plan`（dea84f2）已快进合入 `main`；main 与 T18 分支的推送待用户集中验收后按 DEVELOPMENT 流程执行。桌面技术路线维持 Electron + React/TypeScript、.NET 10 本机工作进程（ADR-013）；CAD 独立运行，仅由用户手动选择最终 DOCX。D0 固定栏目需求为历史记录，三步说明编制流程见 [PRD](PRD.md) 与 [实施计划](IMPLEMENTATION_PLAN.md)。
- T16 checkpoint 在 `task/T16-desktop-authoring`（433ec3a）：Electron/React 骨架与 .NET 10 DOCX 生成/读回进程，构建与生成/检查测试通过；真实窗口未验、未合 main。按新 PRD 改造时保留该技术基础，替换旧固定栏目表单。
- A 阶段范围（本次只做这些）：首页与导航（仅「设计说明」进入真实流程，其余入口标注未开放）、六专业选择（建筑/结构/给排水/电气/暖通/其他；仅结构显示结构参数，抗震设防烈度无核验数据时显示「待核定」）、项目与草稿本机保存与恢复、章节模板与自定义组合、纯文本编辑与真实自动保存、连续预览、导出可编辑 DOCX；逐专业自动化验证新建→编辑→关闭重开→预览→导出并用现有 DocxDocumentParser 读回。可研/投标/AI/专业标准正文、CAD 联动与同步、额外交接文件不在范围。
- 用户 `测试结果/0927` 的 A2/A3 PDF 显示旧模板文字与下方图签相碰。T20 已将 A1/A2/A3 统一升为 1.1.1 并整体上移 21.6mm，自动检查通过；1.1.1 的真实 CAD 打印待用户以后反馈，不阻断独立桌面端工作，也不得宣称打印已复核。

## 2026-09-27 T20：用户新 Figma CAD 窗口改版

- 新任务优先于原接续 T18；独立分支 `task/T20-cad-figma-ui`，基于已发布的 `main`，worktree 为 `C:/Users/Administrator/.codex/worktrees/cad-ui-figma/JUSTIFIED_specification reflow for AutoCAD`。不把尚未验收的 T18 桌面分支带入 CAD。用户 Figma Make、本地 ZIP 和截图来源、实现差异与检查见 [T20 日志](devlog/T20.md) 和 [视觉规范](VISUAL_SPEC_V1.md)。
- T20 代码与 100%/模拟 150% 离线预览已完成。用户确认旧候选安装器可用；`测试文件/程序/Setup.exe` 缺同目录 bundle 的交付错误已修复。三图幅模板 1.1.1 修订提交 `c101624` 已推公开 `origin/task/T20-cad-figma-ui`；最新候选 ZIP 为 `artifacts/packages/20260927-172321-245/JUSTIFIED_specification-reflow-for-AutoCAD-0.1.0-candidate-c101624.zip`（SHA256 见 T20 日志），包内 `productionReady=false`。T20 为 [草稿 PR #2](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/pull/2)，尚未合并或发布。几何/包/测试检查通过，1.1.1 真实打印还待用户反馈；工作目录原有 `tests/.../Fixtures/test-note-standard.json` 换行差异不是本任务改动，不暂存。用户业务文件与测试结果不入库。

## 唯一下一动作

接续桌面端 T18/A 阶段已实现成果的集中核对及收尾；T20 的 1.1.1 安装包供用户方便时按 [T20 宿主检查](T20_HOST_CHECK.md) 复印 A1/A2/A3，特别核对下方图签至少一行余量。待真实打印反馈后再将 T20 标完成并合入 `main`；在此之前不把草稿 PR 当作已发布。桌面端可独立推进，T18 分支 `task/T18-desktop-framework`（worktree `artifacts/t18-worktree`）无需等待 CAD 打印。后续 A/B/C 任务顺序见 [实施计划](IMPLEMENTATION_PLAN.md)，不安排日历工期。用户要求**全部工作完成后**设置 10 分钟关机；当前仍在开发，不执行。

原工作目录的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
