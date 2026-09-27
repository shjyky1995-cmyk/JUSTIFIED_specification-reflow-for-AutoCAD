# 当前接续状态（所有 Agent 共用）

更新：2026-09-27。CAD v0.1.0 已由用户签收、合入 `main`、推送 GitHub **公开**仓库并上传 ZIP。发布页：[CAD v0.1.0](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/v0.1.0)。T12/T13 被用户免除的补充验收数据仍不得写成实测；详见任务日志与 BUILD.json。

## 2026-09-27 桌面 A 已合并，B1 清点完成

- T18/A 用户反馈已试用且无问题。[PR #1](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/pull/1) 已合入 `main`（2224681），`desktop-a-framework` 标签已推 GitHub。Word/WPS 单独打开导出文件的证据未留，不虚构。
- A 阶段交付：首页与导航（仅「设计说明」进入真实流程；搜索、当前项目、最近工作；继续/确认删除）、01 参数（六专业；仅结构显示结构参数，抗震烈度由用户手选；关联项目不覆盖已填内容；缺参数可先继续编制）、02A 模板（结构四套预设+各专业一套+自定义组合）、02B 章节工作区（拖动/按钮排序、添加标准或自定义章节、清空本章正文、纯文本编辑、约 1 秒防抖自动保存并显示保存中/已保存/失败）、03 连续只读预览（顺序与导出一致）+ 缺项定位并阻断导出 + 导出 DOCX（不覆盖、读回检查）。标准库提示只作引导，不自动成为正式正文；旧草稿中未改过的提示被识别为空章。桌面到 DOCX 为止，无 CAD 联动。
- 用户 2026-09-27 指出 UI 与 Figma 不符及 01 按钮问题后，已按 Make ZIP 初稿重排首页/01/02A/02B/03，统一使用 CAD 产品图标 `branding/product-icon.png`。烈度改人工选择，规范选项见 PRD。`npm run build`、worker/flow 六专业测试通过；`build.ps1 -Target All` CAD/安装器编译 0 警告错误、双框架各 164/164；Electron 在 1280×820 和 900×820 截图核对无遮挡，01 可继续至模板，03 对缺项明确阻断。截图与布局报告（不入库）在 `artifacts/t18-worktree/artifacts/ui-review/`。早期真实 Electron 保存/重开/导出读回证据见 T18 日志；本轮未重新用 Word/WPS 打开文件。
- 复核入口（若后续需要）：双击 `artifacts/t18-worktree/desktop/app/start-desktop.cmd`；在结构专业选择烈度、填写项目资料后走模板→编辑→预览→导出。Word/WPS 单独打开导出文件的实测尚无记录；不虚构该证据。真实 CAD 选择文件不属桌面验收前置。
- T22/B1：[PR #3](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/pull/3) 已合入 `main`（7f8c1ca），[内容清单](CONTENT_INVENTORY.md) 覆盖六专业 25 个章节、适用条件、来源与缺口。获核定的全局正文为 0 条；B 阶段总体未验收。T20 CAD 1.1.1 图签避让在独立[草稿 PR #2](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/pull/2)。用户反馈旧候选完整解压后安装器仍报缺数据；已在 `b9b1011` 把校验过的 bundle 嵌入 Setup，并把真实 ZIP 解压运行及无相邻 bundle 的安装器探测设为构建门槛。最新候选 ZIP 已放到 G 盘项目的 `测试文件/CAD安装候选/`，解压目录也在该处；SHA256 见 T20 日志。两个探测及双框架各 167/167 通过；用户真实安装和 1.1.1 打印待反馈，不作为桌面端前置。T20 开发工作树已迁至 G 盘项目的 `artifacts/t20-worktree/`，C 盘临时工作树已归档清理。

## 唯一下一动作

从 G 盘项目的 `测试文件/CAD安装候选/b9b1011-已解压/Setup.exe` 做 T20 真实安装复测，确认欢迎页出现源码提交而非「安装内容不完整」，再核对 `DSS` 与 A1/A2/A3 新版打印；细节见 [T20 宿主检查](T20_HOST_CHECK.md)。用户反馈后修复剩余问题或验收合并 T20。桌面 B2 同时等待结构专业可核定的正文、资料来源与适用条件；缺正式正文保持空章，不编造规范结论。用户要求**全部工作完成后**设置 10 分钟关机；当前仍有 B/C 阶段，不执行。

原工作目录的用户业务 DOCX、截图/PDF 结果与测试夹具换行差异保持未暂存；不得带入公开仓库。
