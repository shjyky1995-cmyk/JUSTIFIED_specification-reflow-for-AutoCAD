# 当前接续状态（所有 Agent 共用）

更新：2026-09-27。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 当前：T25/B 独立设计说明候选资料库待验收

- 分支 `task/T25-content-library` 从已获用户试用认可的 T18 `bae8d00` 建独立 worktree；T25 功能提交 `90d2fe5`。T18 PR #1 尚未合 main，T24 资料清点在独立分支，`CONTENT_INVENTORY.md` 已按需带入。原 G 盘工作目录及其他 worktree 中已有的夹具修改、源 DOCX 和截图不属于本任务，保持未暂存。T25 尚未合 main/推远程；私有包不会推送。
- 顶层 `content-library/` 保存公开的格式、转换工具和审核记录，`private/` 保存本机生成的 `catalog.json`、`audit.json` 和试用脚本（Git 忽略）。14 DOCX/1405 候选/25 CH 主题/368 统一字段已校验；5 处规范号或图集版次有来源证据的修正已记录。全部候选仍待核定，32 表格与 8 条疑似旧项目文书名禁用；修正后的 209 种规范/图集引用未逐一核验。
- 桌面端新增导入私有包、按专业/章节选条款、当前工程填写变量、逐条确认、保存快照和 DOCX 输出。已通过构建、脱敏端到端 DOCX 测试和六专业原流程回归；新增 UI 尚无真实窗口人工验收。软件不会自动把旧候选当全局已批准正文。

## 2026-09-27 T18/A 阶段完成，用户试用无问题

- T17 设计说明 PRD 与实施计划已由用户认可并合入 `main`（dea84f2，登记提交 2699485）。T18/A 阶段在分支 `task/T18-desktop-framework`（worktree `artifacts/t18-worktree`，自 main 合并 T16 checkpoint 433ec3a 后改造）完成；视觉修订 `36cabcd`、引导正文修复 `e4b1cbf` 已推公开 GitHub，[PR #1](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/pull/1) 待合并。用户反馈已经试用且无问题，作为 A 阶段使用体验认可；Word/WPS 单独打开证据未留。
- A 阶段交付：首页与导航（仅「设计说明」进入真实流程；搜索、当前项目、最近工作；继续/确认删除）、01 参数（六专业；仅结构显示结构参数，抗震烈度由用户手选；关联项目不覆盖已填内容；缺参数可先继续编制）、02A 模板（结构四套预设+各专业一套+自定义组合）、02B 章节工作区（拖动/按钮排序、添加标准或自定义章节、清空本章正文、纯文本编辑、约 1 秒防抖自动保存并显示保存中/已保存/失败）、03 连续只读预览（顺序与导出一致）+ 缺项定位并阻断导出 + 导出 DOCX（不覆盖、读回检查）。标准库提示只作引导，不自动成为正式正文；旧草稿中未改过的提示被识别为空章。桌面到 DOCX 为止，无 CAD 联动。
- 用户 2026-09-27 指出 UI 与 Figma 不符及 01 按钮问题后，已按 Make ZIP 初稿重排首页/01/02A/02B/03，统一使用 CAD 产品图标 `branding/product-icon.png`。烈度改人工选择，规范选项见 PRD。`npm run build`、worker/flow 六专业测试通过；`build.ps1 -Target All` CAD/安装器编译 0 警告错误、双框架各 164/164；Electron 在 1280×820 和 900×820 截图核对无遮挡，01 可继续至模板，03 对缺项明确阻断。截图与布局报告（不入库）在 `artifacts/t18-worktree/artifacts/ui-review/`。早期真实 Electron 保存/重开/导出读回证据见 T18 日志；本轮未重新用 Word/WPS 打开文件。
- 复核入口（若后续需要）：双击 `artifacts/t18-worktree/desktop/app/start-desktop.cmd`；在结构专业选择烈度、填写项目资料后走模板→编辑→预览→导出。Word/WPS 单独打开导出文件的实测尚无记录；不虚构该证据。真实 CAD 选择文件不属桌面验收前置。
- 未完成项：专业标准正文（阶段 B）、抗震地点自动映射数据（手选已可用）、可研/投标/AI 入口、离线安装打包（阶段 C）。T18 合 main、打标签、推送公开 origin 待执行。T20 CAD 1.1.1 图签避让已在另一个分支完成代码/包检查，真实打印待用户反馈；不作为桌面验收前置。

## 唯一下一动作

请用户用 `content-library/private/启动资料库试用.cmd` 打开桌面端，导入同目录 `catalog.json`，选一条候选、填写工程值、确认并导出 DOCX；据反馈修复。用户通过后按依赖顺序集成 T18/T24/T25，不带业务源文件；B 阶段后续逐专业核定全局正文，先结构。T20 CAD 新版打印由用户以后反馈。用户要求**全部工作完成后**设置 10 分钟关机；当前仍有 B/C 阶段，不执行。

原工作目录的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
