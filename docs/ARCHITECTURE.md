# 架构与决策
状态：用户已确认架构；M0 加载与工具验证结果见 T02 日志。产品语义以 PRD 为准。

## 技术栈
- 固定项目名：JUSTIFIED_specification-reflow-for-AutoCAD；解决方案与 .bundle 使用此名。
- C# 命名空间/程序集前缀：Justified.SpecificationReflow.AutoCAD（连字符不能用于 C# 命名空间）。
- C#；通用库 netstandard2.0；AutoCAD 2021 插件 net48 / x64。
- SDK 风格项目；.NET SDK 8.0.425 是构建工具，不是插件运行时。
- Open XML SDK 解析 DOCX；Newtonsoft.Json 处理 JSON；不使用 Word COM。
- NUnit 独立测试同时运行 net8.0 与 net48，后者验证实际 Framework 兼容性。
- CAD 原生命令和点选；必要 UI 使用 WinForms；本阶段无 UI 框架依赖。
- PowerShell 构建入口；离线 .bundle 发布；本地 Git + 阶段 GitHub 私有备份。
- 具体包版本在 Directory.Packages.props 与各项目 packages.lock.json 固定；升级必须说明原因和兼容性验证。
- JSON Schema 正式契约及校验实现在 T03 完成；不把序列化成功等同于 Schema 验证。

## 模块与允许引用
箭头表示左侧可以引用右侧；未列出方向禁止。
| 模块 | 允许引用 | 责任 |
| --- | --- | --- |
| Contracts | 无项目引用 | 自有值类型、模型、诊断、Active 端口 |
| DocumentCore | Contracts | 模型校验、JSON 协议 |
| DocxAdapter | Contracts | DOCX 转模型与定位诊断 |
| Standards | Contracts | 版本化院标和模板读取、校验 |
| LayoutEngine | Contracts | 分词、真实测量接口、换行、行槽分页 |
| Application | Contracts、DocumentCore | 用端口编排生成流程 |
| AutoCadAdapter | Contracts | 宿主测量、DBText 渲染、事务适配 |
| PluginHost | Application、DocumentCore、DocxAdapter、Standards、LayoutEngine、AutoCadAdapter、Contracts | 装配、环境、用户交互 |

Contracts、引擎不得引用宿主 SDK、UI、数据库、AI SDK。测试工程可引用被测模块。
渲染器不读 DOCX、不重新换行；解析器不决定字体或坐标。
坐标与标准配置的生产值只能来自已标定资产；字体环境影响测量缓存键。

## 接口演进
T03 按 PRD 第 5、9 节冻结 Document、LayoutResult、RenderRequest、自有几何值类型和诊断契约。
实现阶段逐项增加 Active 端口真实实现，取消和 Diagnostics 必须贯穿链路。
M0 仅提供构建边界及技术诊断，不以占位返回值伪造业务接口成功。
新增或修改共享契约，先在本文件追加提案（原因、受影响任务、兼容性、测试），等待用户确认。
内部修复不需架构审批；不得以此流程阻止普通开发。

## 已确认决策
- ADR-001：首版只承诺 AutoCAD 2021，其他版本必须单独验证。
- ADR-002：分层单仓库，本地生成不依赖网络或服务。
- ADR-003：默认串行；并行使用 worktree 与任务分支，由集成任务合并。
- ADR-004：最终 DOCX 为正文权威；固定院标决定 CAD 表现；删除旧说明后完整重出。
- ADR-005：共享协议、依赖方向和技术栈变更需用户确认；不预建 Future 空模块。
- ADR-006：main 保存已验收任务；宿主验证与图面验收分级记录，阶段标签不能伪造完成。
- ADR-007：2026-09-22 用户指定正式项目名与 GitHub 仓库一致，不再使用临时 DesignNote 产品名；原 PRD 文件名保留以维护来源。
