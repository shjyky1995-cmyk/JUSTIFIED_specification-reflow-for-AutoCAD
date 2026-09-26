# 桌面说明编制工作台 PRD 草案

状态：**待用户确认整体框架与路线；不授权桌面功能编码。** 日期：2026-09-26。

本文件单独记录 CAD V1 之后的桌面端。已签收的 CAD 功能与边界仍以根目录 `CAD_DesignNote_PRD_V1.0.md` 为准；两端共同遵守 `docs/ARCHITECTURE.md` 的依赖方向和 `docs/VISUAL_SPEC_V1.md` 的视觉语言。新 Agent 从根 PRD、`docs/TASKS.md`、`docs/NEXT_AGENT.md` 可找到本文和当前决策状态。

## 1. 用户确认的目标流程

**桌面端生成一份标准 DOCX → 修改并保存这份 DOCX → AutoCAD 直接导入这份最终 DOCX。** DOCX 是唯一正文成果，不需要额外的“交接请求”、隐藏同步文件或另一份正文副本。用户可以反复修改同一个 DOCX，再到 CAD 重新导入。

CAD 保持已经可用的 `DSS` 流程：选择本次 DOCX、图幅，核对单位比例，点一次图面左上角，生成可编辑 DBText。上次选择可预填；无警告不增加确认。桌面端不直接操作 DWG，也不替代 CAD 字体测量、分页与事务落图。

用户已确定第一版从**固定模板填写**生成 DOCX，之后主要在 **Word/WPS** 中修改并保存。桌面端负责选择模板、填写内容、生成与打开 DOCX，以及导入前的内容检查；具体字段和模板仍须在 D0 梳理。模块组装与 AI 初稿不纳入第一版。

“一键导入”指设计人员在 CAD 用一个正式命令 `DSS` 发起导入，程序从最终 DOCX 一次完成解析与落图；实际仍须确认图幅/单位并点取位置。不会要求用户找 JSON、复制路径或执行多步开发命令。

## 2. 产品边界

- DOCX 始终由用户保存后的版本决定 CAD 内容；CAD 中的修改不自动回写 DOCX。生成器不能悄悄覆盖用户手工修改。
- 桌面端可检查当前 DOCX 的标题、段落、编号和不支持内容，明确告诉用户哪里不能导入；这是辅助能力，不能替代 CAD 对真实字形和栏宽的检查。
- 用户操作界面不显示“页数”或“待 CAD 计算”。CAD 内部排版为了把长说明续排到下一张图幅，会记录分配了多少个图幅区域；它不是 Word 的分页，也不是用户要输入的说明页码。若需要提示生成范围，应使用“图幅张数”并以实际 CAD 结果为准。
- 首版不预建项目数据库、协同、AI 初稿、模块库或其他平台适配。
- 视觉上沿用 Noto Sans SC、蓝灰色、间距与状态文案；桌面窗口采用适合完整工作台的尺寸，不照搬 CAD 小窗像素坐标。

## 3. 技术框架比较（待用户选择）

| 维度 | WPF + .NET 10（推荐） | Electron + .NET 本机工作进程 |
| --- | --- | --- |
| 生成与检查 DOCX | 可直接复用现有 C# 解析库和 Open XML 依赖；新写入逻辑仍须单独设计与测试 | 网页界面通过受控进程间消息调用 .NET 生成与解析进程 |
| 界面 | XAML 提供布局、矢量、样式和数据绑定，能按现有视觉规范制作 Windows 工作台 | HTML/CSS 便于复杂界面迭代；需维护主进程、渲染进程和安全桥接 |
| 与 AutoCAD | 两者都只交换最终 `.docx`，CAD 插件保持 net48 和现有 `DSS`；无额外桌面→CAD 私有协议 | 同左；Electron 自身调用 .NET 仍需要内部进程通信 |
| 部署维护 | Windows x64 自包含发布可随包携带 .NET 运行时；主要语言保持 C# | 需打包 Electron 的 Chromium/Node 与 .NET 工作进程，并维护两套构建和运行时更新 |

**当前建议 WPF + .NET 10 LTS。** 首批核心工作是生成、检查本机 DOCX，直接引用现有 C# 库可以减少技术边界；Windows/AutoCAD 也是当前唯一承诺平台。WPF 能否达到期望外观，仍须在方案定稿后的最小原型中实测。Electron 的网页样式迭代可能更快，若将来明确需要网页共用界面，可再评估。这里的体积、速度与维护判断是依据官方进程/部署模型和现有项目作出的**推论**，没有伪造本机对比数值。

.NET 10 是当前 LTS，官方支持到 2028-11；.NET 8 到 2026-11。新桌面程序建议从 .NET 10 起步；已通过验收的 AutoCAD net48 插件保持原运行时，不把新 UI 装进 CAD 进程。

## 4. 建议的架构边界

```text
桌面 UI（选定 WPF 或 Electron 后落实）
  → 说明编制应用服务（创建 DOCX、打开/复核文件、诊断）
    → 现有 Contracts / DocxAdapter / Standards
    → 新 DOCX 输出适配器（待明确生成方式后设计）

用户保存的最终 DOCX
  → AutoCAD 2021 中现有 DSS
    → 现有解析、真实测量、换行分页、DBText 事务落图
```

- 桌面程序不引用 Autodesk SDK、不直接写 DWG。AutoCAD API 仍只在 AutoCadAdapter、PluginHost 中。
- 桌面生成的 DOCX 必须能由现有 `DocxDocumentParser` 读回；至少检查正文、标题、编号、符号、空行及 Word/WPS 保存后的语义。修改过的文件由用户明确保存，不自动覆盖。
- 若选择 WPF，桌面 UI 可以直接使用 netstandard2.0 核心；若选择 Electron，桌面内部需要一个受控的 .NET 工作进程，但该内部进程接口不应泄漏到 CAD 插件。
- 桌面与 CAD 不新增请求文件。现有 `DSS` 中的 DOCX 文件选择器就是交接点。

## 5. 开发路线与验收关卡

| 阶段 | 交付 | 验收要点 |
| --- | --- | --- |
| D0 框架与需求定稿 | 本 PRD 定稿、DOCX 生成方式、修改地点、UI 技术、分阶段任务与架构决策 | 用户确认后才能编码；CAD V1 范围保持冻结 |
| D1 DOCX 编制 | 桌面填写固定模板并生成标准 DOCX，提供打开 Word/WPS 的入口 | Word/WPS 可编辑并保存；现有解析器读回内容与语义一致；错误不覆盖已有文件 |
| D2 导入检查与 CAD 回归 | 桌面提示不支持内容；用户在 CAD 用 `DSS` 导入最终 DOCX | 最终 DOCX 与 CAD 内容一致；多次修改后可重出；取消/错误零残留；真实字形和图幅内位置由 CAD 检查 |
| D3 安装与正式签收 | 桌面程序、CAD 插件版本与图形化安装/升级/卸载、离线包和指南 | 普通用户不使用 GitHub/命令行；离线运行、高 DPI、Word/WPS 与真实 CAD 闭环 |

各阶段仍按 `docs/WORKFLOW.md` 连续实现、检查和提交。D0 定稿前不启动 D1。用户业务 DOCX、DWG、截图/PDF 和运行报告不进入公开 GitHub。

## 6. 待共同定稿的决定

已决定：固定模板填写生成 DOCX；在 Word/WPS 修改；CAD 直接导入同一份最终 DOCX；界面不引入“页数”概念。

尚待共同决定：桌面 UI 用 WPF + .NET 10，还是 Electron + .NET 工作进程？当前推荐 WPF；关键技术栈经用户确认后再编码。

## 官方资料

- [Microsoft：WPF 概览](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Microsoft：.NET Standard 2.0 兼容表](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
- [Microsoft：.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Microsoft：自包含与单文件发布](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Electron：进程模型与 IPC](https://www.electronjs.org/docs/latest/tutorial/process-model)
- [Electron：打包与分发](https://www.electronjs.org/docs/latest/tutorial/distribution-overview)
- [Electron：安全与上下文隔离](https://www.electronjs.org/docs/latest/tutorial/security)
