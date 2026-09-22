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
坐标与标准配置的生产值须来自有来源的实测资产或用户授权制定的版本化规则；正式发布仍需通过相关验证，不能用制定规则代替验证证据。字体环境影响测量缓存键。

## 接口演进
T03 按 PRD 第 5、9 节冻结 Document、LayoutResult、RenderRequest、自有几何值类型和诊断契约。
实现阶段逐项增加 Active 端口真实实现，取消和 Diagnostics 必须贯穿链路。
M0 仅提供构建边界及技术诊断，不以占位返回值伪造业务接口成功。
新增或修改共享契约，先在本文件追加提案（原因、受影响任务、兼容性、测试），等待用户确认。
内部修复不需架构审批；不得以此流程阻止普通开发。

T03 已交付：上述契约与 7 个 Active 端口冻结于 Contracts；schemas/ 提供 document、layout-result、render-request 三份 draft-07 Schema，由 DocumentCore 嵌入校验，未知主版本/块类型以 E_SCHEMA_VERSION 拒绝、其余协议违例以 E_SCHEMA_INVALID 拒绝，往返测试覆盖 AC-04。院标与 Layout Template 的 JSON Schema 及发布校验随 T06 标定资产落地（PRD 5.3 示例刻意不是可运行配置）。

T10/T11 已交付（ADR-010 落地）：Contracts 增加 `NoteSettings`（工程设置快照：包根目录、标准与模板 id/version、图幅、单位比例、说明文档、可选报告目录）与 `GenerationLimits`（单次生成资源上限）；AutoCadAdapter 增加 `DrawingSettingsStore`，把设置的 Base64 分块 JSON 存进当前图 JSR_NOTE_SETTINGS 命名字典，跟随图纸走；Application 的 `NoteGenerationService` 增加限额检查点（E_RESOURCE_LIMIT，不截断）与 `NoteRunReportBuilder` 本地运行报告；Standards 的 `DirectoryPackageCatalog` 增加 `ListTemplates` 摘要枚举，`NoteSettingsCodec` 负责编解码。正式入口为 `DN_NOTE_SET`（设一次，只接受 classification=production 的包）+ `DN_NOTE`（日常点一次位置）；原多步命令更名 `DN_NOTE_DEV` 留作开发核对，仍走草案内存补齐。不改变依赖方向或技术栈；限额与报告不写入共享 Schema。

## 已确认决策
- ADR-001：首版只承诺 AutoCAD 2021，其他版本必须单独验证。
- ADR-002：分层单仓库，本地生成不依赖网络或服务。
- ADR-003：默认串行；并行使用 worktree 与任务分支，由集成任务合并。
- ADR-004：最终 DOCX 为正文权威；固定院标决定 CAD 表现；删除旧说明后完整重出。
- ADR-005：共享协议、依赖方向和技术栈变更需用户确认；不预建 Future 空模块。
- ADR-006：main 保存已验收任务；宿主验证与图面验收分级记录，阶段标签不能伪造完成。
- ADR-007：2026-09-22 用户指定正式项目名与 GitHub 仓库一致，不再使用临时 DesignNote 产品名；原 PRD 文件名保留以维护来源。

- ADR-008：2026-09-22 用户授权自行统一文字格式，不再等待完整院标；以 [项目格式 V1](TEXT_FORMAT_V1.md) 为实施依据。保留 InstitutionStandard 契约和既有分层，仅更新参数来源与业务规则；宿主/打印验收仍独立完成。

- ADR-009：2026-09-22 用户明确由项目制定完整院标，支持多套机构/个人标准独立配置与修改。沿用 IStandardProvider/ILayoutTemplateProvider，以独立ID、版本和standardRef隔离；一次生成显式选择匹配组合。当前套采用A1/A2三栏、A3两栏，字体大小不随图幅变化，配置规则见 PAPER_TEMPLATES_V1.md。本次不改变共享协议、依赖方向或技术栈。

- ADR-010：2026-09-22 用户要求设计人员的正式操作接近一键。ADR-009 里的「显式选择」改为工程或当前图设定一次并记住，日常生成不再重问。仍禁止按文件名或旧图样式偷偷换标准，未知单位不猜。不新增模板编辑器，不改变共享协议、依赖方向或技术栈。当前多步命令只留作开发核对。

T06 已交付（2026-09-22 范围调整后）：Contracts 增加 `ScriptCalibration`（上下标标定 Problems 校验与 Apply：缩放字高+基线偏移，项目制定 0.7/0.35/0.7/0.2）与 `FontGlyphCoverage`（按字体身份的实测缺失字形清单，命中即 E_FONT_MISSING 阻断，不落问号）；LayoutEngine 按 run 语义把视觉行拆成多个 RenderRun（普通/上标/下标各一个 DBText 段），段宽按缩放字高实测、基线偏移按标定，整行宽度按正文字高保守测量；NotePlacement 落 BaselineOffset 坐标；DraftSessionLoader 内存补齐上下标字段，PackageValidator 发布时要求齐全。不改变依赖方向或技术栈；缺失字形清单属实测证据，随字体资产版本重新标定。
