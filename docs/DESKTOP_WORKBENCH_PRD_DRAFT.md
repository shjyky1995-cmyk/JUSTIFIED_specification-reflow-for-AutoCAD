# 桌面导入工作台 PRD 草案

状态：**待用户确认，仅供技术框架与路线讨论；不授权实现。** 日期：2026-09-26。

本文件单独记录 CAD V1 之后的桌面端。已签收的 CAD 范围仍以根目录 `CAD_DesignNote_PRD_V1.0.md` 为准；两端共同遵守 `docs/ARCHITECTURE.md` 的依赖方向和 `docs/VISUAL_SPEC_V1.md` 的视觉语言。开发入口、状态和下一步继续由 `docs/TASKS.md`、`docs/NEXT_AGENT.md` 管理。

## 1. 目的与第一版范围

用户选择的首批场景是**离线导入工作台**。设计人员可以先在独立 Windows 程序中选择 DOCX，核对识别出的标题、段落和编号，看到可定位的解析错误与警告，选定图幅和单位比例；最后仍由 AutoCAD 2021 的插件进行真实字形测量、排版、点位和 DBText 落图。

第一版桌面端不编辑 DOCX 正文、不复刻 Word 视觉样式，不创建项目数据库、模块库、AI、协同或预览渲染器。这些属于后续独立需求，不以空服务占位。

建议用户流程：

1. 打开桌面程序，选择本机 `.docx`；程序离线解析并显示文件名、内容 hash、标题/段落/编号数量和定位到段落的诊断。
2. 选择 A1/A2/A3，明确填写并核对单位比例。模板版本继续由管理员在已发布包中保持每图幅唯一有效，桌面端不按文件名猜标准。
3. 点击“准备在 CAD 中导入”。桌面程序只保存本次选择和源文件身份，不生成 CAD 对象。
4. 在 CAD 执行现有 `DSS`。窗口明显显示“来自桌面工作台的本次选择”，仍允许改选；设计人员确认后点取**图面左上角**。CAD 再次核对文件 hash，并完成真实测量、排版和一次事务落图。失败或取消不留本次新对象。
5. CAD 完成后，桌面端可读取本地运行报告，显示**实际**页数、对象数及诊断；未完成 CAD 测量前显示“页数待 CAD 计算”，不编造预计页数。

第 3～5 步需要新增桌面与 CAD 的交接约定，属于共享协议变更，实施前须得到用户确认。简单例子：桌面端可以把“这份 DOCX、A2、比例 100”写进一份本机请求文件；CAD 再打开原始 DOCX 并核对 hash，避免文件在两端之间被改动而不自知。

## 2. 技术框架比较

| 维度 | WPF + .NET 10（推荐） | Electron + .NET 本机工作进程 |
| --- | --- | --- |
| 与现有解析核心 | 桌面程序直接引用现有 `netstandard2.0` 的 Contracts、DocxAdapter、Standards；CAD 插件仍独立运行于 .NET Framework 4.8 | 网页界面不能直接调用 C# 库；需独立 .NET 进程和受控进程间消息 |
| 界面 | XAML 支持矢量、布局、样式与数据绑定，可按现有 Noto Sans SC、蓝灰色和间距规范制作 | HTML/CSS 容易迭代复杂界面；需维护 Electron 主进程、渲染进程及安全桥接 |
| 部署 | Windows x64 自包含发布可随包携带 .NET 运行时；仍需图形化安装与卸载 | 需打包 Electron 自带 Chromium/Node 与 .NET 工作进程，并维护额外运行时更新 |
| 长期成本 | 与现有项目统一 C#；Windows 专用，但目前承诺平台本就只有 Windows/AutoCAD | 前端技术选择更灵活，但产生 JavaScript/Node 与 C# 两套构建、接口和更新责任 |

**建议选 WPF + .NET 10 LTS**。理由：首批任务是本机 DOCX 解析和诊断，直接复用现有 C# 核心即可；不需要为同机调用增加一层进程间接口。WPF 的样式、矢量与布局能力足够承载桌面工作台，是否能达到预期外观仍须在原型中实测。Electron 不是错误方案；若后续明确要共享网页前端或跨平台，可在桌面数据边界稳定后重新评估。此取舍为基于官方架构资料与现有项目约束的**推论**，未把包体、启动速度或内存写成已测数值。

.NET 10 是当前 LTS，官方支持期到 2028-11；现有 .NET 8 支持期到 2026-11，新桌面程序不建议从接近支持终点的 .NET 8 起步。现有 CAD 插件保持 net48，不把 .NET 10 UI 或运行时装进 AutoCAD 进程。

## 3. 模块边界与交接建议

```text
桌面 WPF（仅 UI 和本机交互）
  → 桌面应用服务（编排、诊断摘要、文件校验）
    → 现有 netstandard2.0 Contracts / DocxAdapter / Standards
  → 版本化本机交接请求（仅路径、hash、图幅、比例和请求 ID）

AutoCAD PluginHost / AutoCadAdapter
  → 经用户确认读取交接请求，重新校验源 DOCX
  → 现有解析、真实测量、换行分页与 DBText 事务
  → 本地运行报告（实际页数、对象数、诊断）
```

- 桌面程序不得引用 Autodesk SDK，也不直接读取或写入 DWG。AutoCAD API 仍只出现在 AutoCadAdapter、PluginHost。
- 交接请求建议使用当前用户目录中的版本化 JSON，写入时采用临时文件后原子替换；只保留最近一次待处理请求，不存 DOCX 正文或隐含单位。CAD 打开时展示来源并让用户确认，不能静默落图。
- 若文件不存在、hash 改变、协议主版本不受支持、模板不唯一或单位未确认，CAD 明确阻断；旧 `DSS` 不依赖桌面程序，仍可独立使用。
- 实际页数只能由 CAD 当前字体与测量环境算出。桌面离线状态显示解析摘要，不能把模拟测量或 Word 页数当 CAD 页数。
- 第一版仅存最近选择与运行报告，不引入数据库。项目、模块和业务版本到下一份独立 PRD 再设计。

## 4. 开发路线与每阶段验收

| 阶段 | 交付 | 验收要点 |
| --- | --- | --- |
| D0 框架决策 | 本 PRD 定稿、架构决策记录、交接协议边界 | 用户确认桌面框架、首批范围、CAD 交接和准确页数规则 |
| D1 桌面离线解析 | Windows 窗口、DOCX 选择、结构摘要、诊断定位、A1/A2/A3 与单位输入 | 无 CAD 也可运行；真实 DOCX 与不支持表格样本均给出正确结果；高 DPI 无遮挡 |
| D2 CAD 交接 | 版本化本机请求、`DSS` 显示并确认本次桌面选择、源文件 hash 复核、本地实际结果回读 | 旧 `DSS` 流程不退化；取消/错误零残留；同图多次不同选择；报告页数与宿主一致 |
| D3 安装与正式签收 | 桌面程序和插件的一致版本安装/升级/卸载，离线包和操作指南 | 普通用户图形化安装；断网使用；不同电脑字体与 DPI；真实 CAD 闭环 |

阶段内按现有 WORKFLOW 连续实现、检查和提交；D0 决策前不开始 D1。用户的业务 DOCX、DWG、截图和运行报告不进入公开 GitHub。

## 5. 需共同定稿的三个决定

1. 桌面 UI 采用 **WPF + .NET 10**，还是采用 Electron + .NET 工作进程？建议 WPF。
2. 桌面选择通过**版本化本机请求文件**交给 `DSS`，`DSS` 仍显示并允许改选后点位；是否接受？建议接受。
3. CAD 未执行前，桌面只显示内容与诊断摘要，页数标为“待 CAD 计算”；CAD 完成后回读实际页数。是否接受？建议接受。

## 资料来源（官方）

- [Microsoft：WPF 概览](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Microsoft：.NET Standard 2.0 兼容表](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
- [Microsoft：.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Microsoft：自包含与单文件发布](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Electron：进程模型与 IPC](https://www.electronjs.org/docs/latest/tutorial/process-model)
- [Electron：打包与分发](https://www.electronjs.org/docs/latest/tutorial/distribution-overview)
- [Electron：安全与上下文隔离](https://www.electronjs.org/docs/latest/tutorial/security)
