# 当前接续状态（所有 Agent 共用）

## 2026-10-02 当前：T30 参数、滚动与腐蚀关联待验收

- 分支 `task/T30-project-parameters`，基于T29 `da92611`；桌面工作区仍为G盘 `artifacts/t26-auto-content-worktree`。启动入口不变。
- 用户三张截图需求已实现：红框共用参数移01手填，后续自动引用；长章节独立滚动；蓝框恢复原稿固定文字；黄色参数按腐蚀控制等级关联候选材料与防护。修改01后可直接返回章节，保留人工正文。
- 构建、参数回归、隐藏Electron两尺寸六组滚动与参数修改保存返回、七模板1355段29表核对、三结构模板四等级12份DOCX生成、六专业及兼容回归通过。真实工程/Word视觉与专业核定仍pending；规则与边界见 `CORROSION_DESIGN_RULES.md`、日志T30。
- 本轮本地提交；不合main、不发正式版。私有资料和原有他人夹具差异不入库，CAD T28仍独立待验收，不重新安排关机。
- 用户待办：按 `DESKTOP_PARAMETERS_ACCEPTANCE.md` 重新启动并新建说明；旧草稿不会自动重建。唯一下一步：有反馈先修参数、版式或规则适用性，否则继续专业内容核定。依赖分支尚未顺序集成。


更新：2026-09-29。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 当前：T29/B 七套原稿完整编制，待用户视觉与专业核定

- 分支 `task/T29-desktop-source-fidelity`，worktree仍为G盘 `artifacts/t26-auto-content-worktree`，基于T27 `6ca2781`。T18/T24/T25/T26/T27依赖尚未顺序集成，不合main。
- 七套原件1355章内段、29表按原顺序进入编辑、预览、DOCX。工程取值集中填写并含表内字段；正文不再因候选筛选丢失。表格按原列宽比例限制页内、重复表头、保留换行，生成文件通过OpenXmlValidator。
- 私有 `source-layouts.json`、`source-fidelity-audit.json` 与 `source-export-tests.json` 在项目根 `content-library/private`；均不上传。独立源文字/输出核对、七模板导出、六专业回归和构建通过，详见 `devlog/T29.md`。
- 用户待办：方便时按 `DESKTOP_SOURCE_ACCEPTANCE.md` 重新选模板查看，再用Word/WPS核对分页与可编辑性。原有草稿不自动覆盖。当前无文档渲染组件，真实窗口与Word视觉未实测；规范和工程条件仍待专业核定。
- CAD T28在主工作区 `task/T28-cad-tables`，用户正在测试；两个候选分支单独推送备份，不把桌面导出当成CAD验收。
- T29实现598c1f3与T28交接c70abf4已推送origin并核对远端SHA；未合main、未打正式标签。
- 用户本轮明确要求完成后推GitHub，再10分钟关机；只在远端确认后安排一次。后续轮次不要重复关机。

## 历史：T27 水池原稿基准

T27已实现8章158段7表，但其他专业仍筛选候选、编辑区表格集中末尾，T29现按七份原稿完整顺序替代新建流程。T27原始日志与旧版式兼容保留。

## 历史：T26/B 编制导出流程已由用户试用，内容与格式需深化

- 分支 `task/T26-auto-content` 的 worktree 在 `G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\t26-auto-content-worktree`，基于 T25 `1f3c1e8`。T18 PR #1 尚未合 main，T24 资料清点在独立分支，`CONTENT_INVENTORY.md` 已按需带入。原 G 盘主工作目录及其他 worktree 的测试夹具修改、源 DOCX 和截图不属于本任务，保持未暂存。T25 的 C 盘 worktree 已在迁移并核对数据后归档；T26 尚未合 main/推公开远程。
- G 盘项目根目录的独立 `content-library/private/` 保存 `catalog.json`、`audit.json`、试用脚本及旧 C 盘缓存的单独备份；全部 Git 忽略。14 DOCX/1405 候选/25 CH 主题/368 统一字段已校验；5 处规范号或图集版次有来源证据的修正已记录。全部候选仍待核定，209 种规范/图集引用未逐一核验。
- 桌面端选择模板后自动装配对应来源的章节和正文初稿。结构水池、框架、水池＋框架分别取各自版本；其他有主体来源的专业模板也自动装配。02B 直接看到正文、填本工程数值，03 整篇核对后导出可编辑 DOCX。表格、条件句、旧项目残留和已发现的地区/工艺假设暂缓自动插入，DOCX 明示待核定。钢结构、其他无主体来源，仍为空框架。
- 构建、七套模板自动装配和 DOCX 生成读回、六专业 worker/flow 回归、旧内容包校验均通过；用户已在真实窗口确认可生成和导出。完整证据与待核定项见 T26 日志。当前成果是候选初稿流程，不等于已批准的正式标准正文。
- 2026-09-29 用户确认真实窗口可以生成并输出说明，流程没有问题；但内容和格式与原 DOCX 差距较大。以构筑物原件对照发现 8 大章/167 非空段/7 表，而当前水池自动稿为 99 条/12 CH 主题章；导出器没有表格与原分项层级。详见 `CONTENT_FIDELITY_GAP.md`。不要把本次试用当作内容质量签收。

## 2026-09-27 T18/A 阶段完成，用户试用无问题

- T17 设计说明 PRD 与实施计划已由用户认可并合入 `main`（dea84f2，登记提交 2699485）。T18/A 阶段在分支 `task/T18-desktop-framework`（worktree `artifacts/t18-worktree`，自 main 合并 T16 checkpoint 433ec3a 后改造）完成；视觉修订 `36cabcd`、引导正文修复 `e4b1cbf` 已推公开 GitHub，[PR #1](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/pull/1) 待合并。用户反馈已经试用且无问题，作为 A 阶段使用体验认可；Word/WPS 单独打开证据未留。
- A 阶段交付：首页与导航（仅「设计说明」进入真实流程；搜索、当前项目、最近工作；继续/确认删除）、01 参数（六专业；仅结构显示结构参数，抗震烈度由用户手选；关联项目不覆盖已填内容；缺参数可先继续编制）、02A 模板（结构四套预设+各专业一套+自定义组合）、02B 章节工作区（拖动/按钮排序、添加标准或自定义章节、清空本章正文、纯文本编辑、约 1 秒防抖自动保存并显示保存中/已保存/失败）、03 连续只读预览（顺序与导出一致）+ 缺项定位并阻断导出 + 导出 DOCX（不覆盖、读回检查）。标准库提示只作引导，不自动成为正式正文；旧草稿中未改过的提示被识别为空章。桌面到 DOCX 为止，无 CAD 联动。
- 用户 2026-09-27 指出 UI 与 Figma 不符及 01 按钮问题后，已按 Make ZIP 初稿重排首页/01/02A/02B/03，统一使用 CAD 产品图标 `branding/product-icon.png`。烈度改人工选择，规范选项见 PRD。`npm run build`、worker/flow 六专业测试通过；`build.ps1 -Target All` CAD/安装器编译 0 警告错误、双框架各 164/164；Electron 在 1280×820 和 900×820 截图核对无遮挡，01 可继续至模板，03 对缺项明确阻断。截图与布局报告（不入库）在 `artifacts/t18-worktree/artifacts/ui-review/`。早期真实 Electron 保存/重开/导出读回证据见 T18 日志；本轮未重新用 Word/WPS 打开文件。
- 复核入口（若后续需要）：双击 `artifacts/t18-worktree/desktop/app/start-desktop.cmd`；在结构专业选择烈度、填写项目资料后走模板→编辑→预览→导出。Word/WPS 单独打开导出文件的实测尚无记录；不虚构该证据。真实 CAD 选择文件不属桌面验收前置。
- 未完成项：专业标准正文（阶段 B）、抗震地点自动映射数据（手选已可用）、可研/投标/AI 入口、离线安装打包（阶段 C）。T18 合 main、打标签、推送公开 origin 待执行。T20 CAD 1.1.1 图签避让已在另一个分支完成代码/包检查，真实打印待用户反馈；不作为桌面验收前置。

## 唯一下一动作

收集七套模板真实窗口与Word/WPS反馈，修正具体版式和工程变量，再推进专业内容核定。无需重复CAD实测作为桌面前置。旧工程源文件、测试结果不入公开仓库，已有测试夹具行尾差异保持未暂存。

原工作目录的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
