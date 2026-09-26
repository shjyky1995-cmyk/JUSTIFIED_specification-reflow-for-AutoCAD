# 当前接续状态（所有 Agent 共用）

更新：2026-09-27。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 2026-09-27 T17 已认可并合入 main；T18/A 阶段开发中

- 用户认可 T17 设计说明 PRD 与实施计划，`task/T17-design-spec-plan`（dea84f2）已快进合入 `main`；main 与 T18 分支的推送待用户集中验收后按 DEVELOPMENT 流程执行。桌面技术路线维持 Electron + React/TypeScript、.NET 10 本机工作进程（ADR-013）；CAD 独立运行，仅由用户手动选择最终 DOCX。D0 固定栏目需求为历史记录，三步说明编制流程见 [PRD](PRD.md) 与 [实施计划](IMPLEMENTATION_PLAN.md)。
- T16 checkpoint 在 `task/T16-desktop-authoring`（433ec3a）：Electron/React 骨架与 .NET 10 DOCX 生成/读回进程，构建与生成/检查测试通过；真实窗口未验、未合 main。按新 PRD 改造时保留该技术基础，替换旧固定栏目表单。
- A 阶段范围（本次只做这些）：首页与导航（仅「设计说明」进入真实流程，其余入口标注未开放）、六专业选择（建筑/结构/给排水/电气/暖通/其他；仅结构显示结构参数，抗震设防烈度无核验数据时显示「待核定」）、项目与草稿本机保存与恢复、章节模板与自定义组合、纯文本编辑与真实自动保存、连续预览、导出可编辑 DOCX；逐专业自动化验证新建→编辑→关闭重开→预览→导出并用现有 DocxDocumentParser 读回。可研/投标/AI/专业标准正文、CAD 联动与同步、额外交接文件不在范围。
- 0925 旧测试 PDF 有越界与顶部碰框；后续 1.1.0 几何已修，真实重印结果待用户自行反馈，不阻断桌面规划，也不得宣称打印已复核。

## 唯一下一动作

T18 分支 `task/T18-desktop-framework`（worktree `artifacts/t18-worktree`）按 A 阶段连续开发、自测、修复并提交；完成后交用户集中验收。验收通过后再合 main、打标签并推送公开 origin。抗震地点数据先留「待核定」，后续优先核对官方来源/许可。新版 CAD 打印由用户方便时反馈，不阻断。用户要求**全部工作完成后**设置 10 分钟关机；当前仍在开发，不执行。

原工作目录的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
