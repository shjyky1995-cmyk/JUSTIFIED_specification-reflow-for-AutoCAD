# 桌面说明编制工作台 PRD V1

状态：**D0 历史基线，桌面业务主流程已由 2026-09-27 的 [设计说明 PRD 评审稿](PRD.md) 与 [实施计划](IMPLEMENTATION_PLAN.md) 提议更新；不得按本文固定栏目方案直接继续开发。** 日期：2026-09-26。用户确认业务流程，并将 UI 技术判断交由 Agent；按视觉表现优先的要求选定 Electron。

本文件单独记录 CAD V1 之后的桌面端。已签收的 CAD 功能与边界仍以根目录 `CAD_DesignNote_PRD_V1.0.md` 为准；两端共同遵守 `docs/ARCHITECTURE.md` 的依赖方向和 `docs/VISUAL_SPEC_V1.md` 的视觉语言。新 Agent 从根 PRD、`docs/TASKS.md`、`docs/NEXT_AGENT.md` 可找到本文和当前决策状态。

## 1. 用户确认的目标流程

**桌面端生成一份标准 DOCX → 修改并保存这份 DOCX → 用户在 AutoCAD 中重新选择并导入这份最终 DOCX。** 两个程序独立运行，唯一关系是用户保存和选择的普通 DOCX 文件。它们不自动同步、不互相启动、不共享后台服务；也不需要额外的“交接请求”、隐藏同步文件或另一份正文副本。用户可以反复修改同一个 DOCX，再到 CAD 重新导入。

CAD 保持已经可用的 `DSS` 流程：选择本次 DOCX、图幅，核对单位比例，点一次图面左上角，生成可编辑 DBText。上次选择可预填；无警告不增加确认。桌面端不直接操作 DWG，也不替代 CAD 字体测量、分页与事务落图。

用户已确定第一版从**固定模板填写**生成 DOCX，之后主要在 **Word/WPS** 中修改并保存。桌面端负责填写、生成、打开与导入前检查。首个固定模板包含说明标题、工程名称、专业、依次排列的一级/二级标题和正文段落；字段是生成初稿的起点，不限制用户随后在 Word/WPS 中增删正文。正文只使用现有解析器支持的样式与文本类型，不生成表格、图片或宏。模板作为独立版本化资源保存，填写数据不建立项目数据库。模块组装与 AI 初稿不纳入第一版。

“一键导入”指设计人员在 CAD 用一个正式命令 `DSS` 发起导入，程序从最终 DOCX 一次完成解析与落图；实际仍须确认图幅/单位并点取位置。不会要求用户找 JSON、复制路径或执行多步开发命令。

## 2. 产品边界

- DOCX 始终由用户保存后的版本决定 CAD 内容；CAD 中的修改不自动回写 DOCX。生成器不能悄悄覆盖用户手工修改。
- 桌面端可检查当前 DOCX 的标题、段落、编号和不支持内容，明确告诉用户哪里不能导入；这是辅助能力，不能替代 CAD 对真实字形和栏宽的检查。
- 用户操作界面不显示“页数”或“待 CAD 计算”。CAD 内部排版为了把长说明续排到下一张图幅，会记录分配了多少个图幅区域；它不是 Word 的分页，也不是用户要输入的说明页码。若需要提示生成范围，应使用“图幅张数”并以实际 CAD 结果为准。
- 首版不预建项目数据库、协同、AI 初稿、模块库或其他平台适配。
- 视觉上沿用 Noto Sans SC、蓝灰色、间距与状态文案；桌面窗口采用适合完整工作台的尺寸，不照搬 CAD 小窗像素坐标。

## 3. 技术框架比较与决策

| 维度 | WPF + .NET 10（推荐） | Electron + .NET 本机工作进程 |
| --- | --- | --- |
| 生成与检查 DOCX | 可直接复用现有 C# 解析库和 Open XML 依赖；新写入逻辑仍须单独设计与测试 | 网页界面通过受控进程间消息调用 .NET 生成与解析进程 |
| 界面 | XAML 提供布局、矢量、样式和数据绑定，能按现有视觉规范制作 Windows 工作台 | HTML/CSS 便于复杂界面迭代；需维护主进程、渲染进程和安全桥接 |
| 与 AutoCAD | 两者都只交换最终 `.docx`，CAD 插件保持 net48 和现有 `DSS`；无额外桌面→CAD 私有协议 | 同左；Electron 自身调用 .NET 仍需要内部进程通信 |
| 部署维护 | Windows x64 自包含发布可随包携带 .NET 运行时；主要语言保持 C# | 需打包 Electron 的 Chromium/Node 与 .NET 工作进程，并维护两套构建和运行时更新 |

**决策：Electron + TypeScript + React 负责界面，.NET 10 负责 DOCX 生成与检查。** 用户在比较后明确优先要求视觉表现，并委托 Agent 判断。WPF 可做到精致界面，但项目后续需要快速打磨丰富布局和动效，Electron 的 HTML/CSS 更合适；代价是安装体积、运行资源和安全更新责任增加。以本机窄接口和离线打包控制维护范围。此处关于界面迭代与成本的判断是依据官方架构和当前项目作出的**推论**；D1/D3 再实测外观、包体、启动和内存，不伪造数值。

.NET 10 是当前 LTS，官方支持到 2028-11；.NET 8 到 2026-11。新桌面工作进程从 .NET 10 起步；已通过验收的 AutoCAD net48 插件保持原运行时，不把新 UI 装进 CAD 进程。

## 4. 建议的架构边界

```text
Electron 窗口（React/TypeScript，本地静态资源）
  → 预加载桥（仅暴露生成、检查、打开、选文件）
    → Electron 主进程（调用本机 .NET 工作进程）
      → 说明编制应用服务（创建 DOCX、复核文件、诊断）
    → 现有 Contracts / DocxAdapter / Standards
    → 新 DOCX 输出适配器（待明确生成方式后设计）

用户保存的最终 DOCX
  → AutoCAD 2021 中现有 DSS
    → 现有解析、真实测量、换行分页、DBText 事务落图
```

- 桌面程序不引用 Autodesk SDK、不直接写 DWG。AutoCAD API 仍只在 AutoCadAdapter、PluginHost 中。
- 桌面生成的 DOCX 必须能由现有 `DocxDocumentParser` 读回；至少检查正文、标题、编号、符号、空行及 Word/WPS 保存后的语义。修改过的文件由用户明确保存，不自动覆盖。
- Electron 渲染进程不能直接访问文件系统或执行任意命令；主进程通过固定白名单调用本机 .NET 工作进程。首版使用每次操作启动一个工作进程、标准输入输出传递限长 JSON，避免常驻服务和开放网络端口。工作进程只接受受控操作和用户明确选择的本机路径。此内部通信仅在桌面程序内，CAD 不参与。
- DOCX 输出采用 Open XML SDK；写出后立即用现有 `DocxDocumentParser` 读回并检查语义，任何失败不覆盖已有 DOCX。保存采用临时文件完成后替换或让用户另存，不能静默覆盖手改文件。
- 桌面与 CAD 不新增请求文件、连接或调用。现有 `DSS` 中由用户选择 DOCX；桌面内部的 Electron ↔ .NET 调用不得扩展成 CAD 接口。

## 5. 开发路线与验收关卡

| 阶段 | 交付 | 验收要点 |
| --- | --- | --- |
| D0 框架与需求定稿 | 本 PRD、固定模板初版字段、Word/WPS 修改、Electron/.NET 边界与架构决策 | 已确定；CAD V1 范围保持冻结 |
| D1 DOCX 编制 | 桌面填写固定模板并生成标准 DOCX，提供打开 Word/WPS 的入口 | Word/WPS 可编辑并保存；现有解析器读回内容与语义一致；错误不覆盖已有文件 |
| D2 导入检查与 CAD 回归 | 桌面提示不支持内容；用户在 CAD 用 `DSS` 导入最终 DOCX | 最终 DOCX 与 CAD 内容一致；多次修改后可重出；取消/错误零残留；真实字形和图幅内位置由 CAD 检查 |
| D3 安装与正式签收 | 桌面程序、CAD 插件版本与图形化安装/升级/卸载、离线包和指南 | 普通用户不使用 GitHub/命令行；离线运行、高 DPI、Word/WPS 与真实 CAD 闭环 |

各阶段仍按 `docs/WORKFLOW.md` 连续实现、检查和提交。D1 先做窄范围可运行闭环与真实界面预览；D2 查出并修复 Word/WPS 回写及 CAD 导入问题；D3 做离线安装和正式签收。用户业务 DOCX、DWG、截图/PDF 和运行报告不进入公开 GitHub。

## 6. 已定决定与变更条件

固定模板填写生成 DOCX；Word/WPS 修改；CAD 直接导入同一份最终 DOCX；界面不引入“页数”概念。Electron + React/TypeScript 与 .NET 10 本机工作进程已定。若首个原型的视觉、交互或部署成本不达标，记录实测并与用户讨论调整，不暗中换技术。

## 官方资料

- [Microsoft：WPF 概览](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Microsoft：.NET Standard 2.0 兼容表](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
- [Microsoft：.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Microsoft：自包含与单文件发布](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Electron：进程模型与 IPC](https://www.electronjs.org/docs/latest/tutorial/process-model)
- [Electron：打包与分发](https://www.electronjs.org/docs/latest/tutorial/distribution-overview)
- [Electron：安全与上下文隔离](https://www.electronjs.org/docs/latest/tutorial/security)
