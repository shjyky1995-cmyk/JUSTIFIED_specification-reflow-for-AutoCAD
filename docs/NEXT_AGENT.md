# 当前接续状态（所有 Agent 共用）

更新：2026-09-27。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 2026-09-27 T18/A 阶段完成，待用户集中验收

- T17 设计说明 PRD 与实施计划已由用户认可并合入 `main`（dea84f2，登记提交 2699485）。T18/A 阶段在分支 `task/T18-desktop-framework`（worktree `artifacts/t18-worktree`，自 main 合并 T16 checkpoint 433ec3a 后改造）完成，**未合 main、未推送**，等用户集中验收。
- A 阶段交付：首页与导航（仅「设计说明」进入真实流程；搜索、当前项目、最近工作；继续/确认删除）、01 参数（六专业；仅结构显示结构参数，抗震烈度由用户手选；关联项目不覆盖已填内容；缺参数可先继续编制）、02A 模板（结构四套预设+各专业一套+自定义组合）、02B 章节工作区（拖动/按钮排序、添加标准或自定义章节、恢复默认、纯文本编辑、约 1 秒防抖自动保存并显示保存中/已保存/失败）、03 连续只读预览（顺序与导出一致）+ 缺项定位并阻断导出 + 导出 DOCX（不覆盖、读回检查）。桌面到 DOCX 为止，无 CAD 联动。
- 用户 2026-09-27 指出 UI 与 Figma 不符及 01 按钮问题后，已按 Make ZIP 初稿重排首页/01/02A/02B/03，统一使用 CAD 产品图标 `branding/product-icon.png`。烈度改人工选择，规范选项见 PRD。`npm run build`、worker/flow 六专业测试通过；`build.ps1 -Target All` CAD/安装器编译 0 警告错误、双框架各 164/164；Electron 在 1280×820 和 900×820 截图核对无遮挡，01 可继续至模板，03 对缺项明确阻断。截图与布局报告（不入库）在 `artifacts/t18-worktree/artifacts/ui-review/`。早期真实 Electron 保存/重开/导出读回证据见 T18 日志；本轮未重新用 Word/WPS 打开文件。
- 验收方式（用户）：双击 `artifacts/t18-worktree/desktop/app/start-desktop.cmd`；对照 `artifacts/t18-worktree/artifacts/ui-review/1280/` 的截图核对视觉，在结构专业选择烈度、填写项目资料后走模板→编辑→预览→导出，用 Word/WPS 打开导出的 DOCX 确认可编辑。真实 CAD 选择文件由用户自行完成，不属桌面验收前置。
- 未完成项：用户视觉/Word 复核；专业标准正文（阶段 B）、抗震地点自动映射数据（手选已可用）、可研/投标/AI 入口、离线安装打包（阶段 C）。T18 合 main、打标签、推送公开 origin 在用户验收通过后进行。

## 唯一下一动作

用户按上述方式集中验收 T18/A，若视觉仍有差异请给出具体页面与位置；通过后回复「继续下一步」进入阶段 B（标准内容与专业深化）。用户要求**全部工作完成后**设置 10 分钟关机；当前仍有 B/C 阶段，不执行。

原工作目录的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
