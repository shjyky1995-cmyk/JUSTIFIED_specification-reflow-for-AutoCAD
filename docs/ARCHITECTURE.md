# 架构与决策

2026-10-06 T40：投标原框架已接入平台同主窗口，前端Electron44＋后台原Electron41/SQLite/Word，通过本机IPC版本1转发现有白名单。用户继续指令与边界见[ADR-019](adr/ADR-019-bidding-embedded-engine.md)；ADR-018独立运行环境保留，T33无数据库/单运行时限制被用户原框架移植要求替代。

## 2026-10-04 T33/T34 标书增量方案（已批准，P2候选已实现）

原因：用户新增勘察设计投标业务，要求参考易标并独立并行开发。推荐保留 Electron/React/TypeScript＋.NET/Open XML，新增独立标书领域与本地 JSON 存储，项目资料快照复用；主进程/preload/worker 仅新增受限标书操作，既有说明操作与 CAD Contracts 保持兼容。第一阶段不引入数据库、第二套导出运行时或云服务。

影响：桌面首页导航、独立标书文件与备份、来源定位、文档导出样式；后续 AI 和 PDF/OCR 依赖需在对应阶段明确选择。共享接入点由集成时段串行修改，六专业说明必须回归。完整模块职责、许可取舍、测试与分阶段边界见 [投标计划](SURVEY_DESIGN_BID_PLAN.md)。用户2026-10-04批准，目录分类完成后已在T34实现P2。新增features/bidding模型、bids版本化JSON及哈希附件、本标独立备份、bid-*受限IPC、worker bid-extract/bid-export。便携包通过resources/.engispace-portable标记将数据/缓存/临时文件放到同盘portable-data。

原功能架构不变；没有修改CAD Contracts。已核对稿须逐项通过本标人工核对，不能解释为行业规范全覆盖。

状态：用户已确认架构；M0 加载与工具验证结果见 T02 日志。产品语义以 PRD 为准。

## T35 已选定 DeepSeek 的增量

用户2026-10-04/05确认服务为DeepSeek。Electron主进程通过官方HTTPS接口请求，不引入SDK；bid-ai独立本地任务和Windows加密配置，投标schema1及原备份兼容。新增bid-ai-*受限IPC；准备/确认/执行/候选校验/人工采纳分开，发送前绑定保存版本、模型及输入哈希，失效需重新确认。网络请求不锁正文编辑，采纳时统一保存锁；结果只增加待核对候选。每次调用记录token用量，不伪造费用。PDF/OCR依赖未选定，尚未实现。

## 技术栈
- 固定项目名：JUSTIFIED_specification-reflow-for-AutoCAD；解决方案与 .bundle 使用此名。
- C# 命名空间/程序集前缀：Justified.SpecificationReflow.AutoCAD（连字符不能用于 C# 命名空间）。
- C#；通用库 netstandard2.0；AutoCAD 2021 插件 net48 / x64。
- SDK 风格项目；.NET SDK 8.0.425 是构建工具，不是插件运行时。
- Open XML SDK 解析 DOCX；Newtonsoft.Json 处理 JSON；不使用 Word COM。
- NUnit 独立测试同时运行 net8.0 与 net48，后者验证实际 Framework 兼容性。
- CAD 原生命令和点选；必要 UI 使用 WinForms；本阶段无 UI 框架依赖。
- PowerShell 构建入口；离线 .bundle 发布；本地 Git + 阶段 GitHub 公开仓库备份（2026-09-26 用户确认）。
- 具体包版本在 Directory.Packages.props 与各项目 packages.lock.json 固定；升级必须说明原因和兼容性验证。
- JSON Schema 正式契约及校验实现在 T03 完成；不把序列化成功等同于 Schema 验证。

## 双端界面后续提案（2026-09-24，尚未实施）

用户确认产品最终有两个使用入口：Windows 独立桌面应用和 AutoCAD 内插件；希望两端视觉统一，桌面端倾向 Electron。当前 V1 仍以 AutoCAD 插件闭环为范围，桌面应用属于 PRD 后续能力。先完成 CAD 功能、真实性能及普通用户安装流程，再统一设计语言和改版 CAD 选择窗口，正式推广前验证视觉与交互；桌面应用待另行确定功能范围。

2026-09-26 接续：CAD v0.1.0 已发布。用户将桌面首批范围收敛为固定模板填写生成 DOCX、在 Word/WPS 修改、再由用户在 CAD `DSS` 重新选择最终 DOCX。两程序独立运行，仅经普通 DOCX 人工衔接；不引入同步、后台连接、交接文件或说明页码。用户要求先定整体技术框架与路线再编码；随后明确优先要炫酷前端，并将技术判断交给 Agent。取舍见 [桌面说明编制工作台 PRD V1](DESKTOP_WORKBENCH_PRD_V1.md)。

Electron 用于独立桌面进程在技术上可行，但不会把 Electron 或网页运行时直接嵌入现有 AutoCAD 2021/net48 插件，也不让前端重写已验证的 C# 解析/排版核心。可行的候选路径是桌面前端通过受控本机接口调用独立 .NET 服务进程，共用现有核心库；真正的 CAD 字体测量、点位和 DBText 提交仍由 CAD 适配器在宿主中执行。接口形式（如本机命名管道）、进程生命周期、离线安装、版本兼容、错误回传与安全边界均未定，须用最小原型验证。两端先共享视觉规范与文案，不预设能共享整套 UI 代码。

2026-09-26 ADR-013（桌面 D0）：Electron + React/TypeScript 承载独立桌面 UI，.NET 10 本机工作进程写出和检查 DOCX；Electron 主进程通过仅桌面内部使用的固定操作白名单调用工作进程，渲染窗口只用预加载桥访问允许的操作，不开放网络端口。用户优先要求前端视觉表现并委托技术判断；Electron 增加 JavaScript/Node 构建、包体与更新责任，换取更灵活的网页式界面。WPF 的单栈维护优势已比较；D1/D3 实测启动、资源、安装与外观，若达不到要求再记录和讨论调整。两个程序之间只有用户手动保存/选择 DOCX，CAD 代码与共享协议不变。

2026-09-27 T18/A 阶段实现登记：桌面代码位于 `desktop/`（不在 CAD 解决方案内）。`desktop/app` 为 Electron + React/TypeScript：渲染进程只经预加载桥调用白名单 IPC；`desktop/app/src/shared/model.ts`（专业/结构参数/章节库/模板/说明快照/校验/预览与导出映射，零依赖可擦除语法）与 `store.ts`（本机 JSON 原子存储）由渲染进程、Electron 主进程和 Node 自动化测试三方共用；主进程以 `app.getPath('userData')/data` 存 projects.json 与 notes/<id>.json。`desktop/worker` 为 net10.0 控制台工作进程，引用 DocxAdapter（现有 `DocxDocumentParser`）做生成后读回检查，不引用 AutoCAD API。桌面与 CAD 之间仍只有用户手动保存/选择的 DOCX；未新增共享契约、模块依赖或技术栈变化（相对 ADR-013）。

2026-09-28 ADR-014（用户明确调整）：候选资料包的正式位置是 G 盘项目根目录的 `content-library/private/catalog.json`，不再由用户导入后复制到 C 盘应用数据。Electron 主进程从项目祖先目录或明确配置的路径只读加载；用户数据目录仍保存草稿/项目，不保存资料包。模板选择调用纯数据装配器，按来源文件版本、主题章和排除规则建立条款快照。说明 JSON 增加可选的 `assemblyPackageId`、`assemblyReviewConfirmed`，旧草稿缺字段时按原逻辑读取；模板、正文或工程值改动会使整篇核对失效。影响为桌面本机存储与编制模型，CAD 共享契约、模块依赖方向及技术栈不变。用户已要求“选模板后第二步直接有完整说明、资料库在 G 盘”，因此按此决策实施。真实性能、规范引用和工程适用性另行验收；当前只称候选初稿。

2026-09-29 ADR-015（水池成品基准，用户要求继续）：T26 主题章句级装配丢失原稿 8 章顺序、分项与 7 张表，用户明确要求先形成接近原说明的完整版本。G 盘私有 `pool-layout.json` 保存原件顺序、参数化段落与表格位置；桌面说明快照增可选 `layoutBlocks`，预览与工作进程请求传递有序文本/表格块，旧纯文本草稿保持兼容。工作进程生成可编辑 OOXML 表格，并对含表格文件做结构读回；CAD V1 解析器仍拒绝表格，因此工作进程返回明确告警，不声称 CAD 可直接导入。这是桌面内部协议与 B 阶段范围的有据扩展；未改 CAD 共享契约、模块依赖方向或技术栈。水池专业内容仍待核定，表格 CAD 兼容另行实施。
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
| SetupCore | 无项目引用 | 安装包清单校验、安装计划、环境检查判断（netstandard2.0 纯逻辑） |
| Setup | SetupCore | 普通用户图形化安装/升级/卸载向导（net48 WinForms，不引用 AutoCAD API） |

Contracts、引擎不得引用宿主 SDK、UI、数据库、AI SDK。测试工程可引用被测模块。
Setup 与 SetupCore 不接触 CAD 图纸与共享协议；AutoCAD API 仍只允许出现在 AutoCadAdapter、PluginHost。
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

2026-09-24 ADR-011（用户更新）：同图允许多次插入不同图幅、模板版本和 DOCX。`NoteSettings` 与图内 `JSR_NOTE_SETTINGS` 保留原结构，只表示上一次明确选择，用作下次窗口预填；不把它当成全图唯一说明类型。`DN_NOTE` 每次打开 PluginHost 内的 WinForms 选择窗口，确认本次选择后复用已验证的排版/落图链路。`DN_NOTE_REPEAT` 明确重用上次选择；`DN_NOTE_SET` 保留供旧图及验收脚本。当前模型/模板仅支持 A1/A2/A3，未来新增图幅需另行扩展 enum、资产和验证。此项不改变共享协议、模块依赖或技术栈；影响 PRD 3.3、操作文档与宿主验收。

2026-09-25 ADR-012（用户更新）：设计人员不再选择模板版本或专业分类，日常只选 DOCX、图幅、核对单位比例。安装/管理层为每个受支持图幅提供唯一有效的 production+calibrated 模板；同图幅有 0 或多于 1 份时窗口阻断并提示管理员，不根据文件名、目录顺序或图内上次版本静默挑选。`NoteSettings` 仍记录实际使用的标准/模板 ID 和版本，用于审计和 `DN_NOTE_REPEAT`；`DN_NOTE` 每次从当前安装目录重新解析唯一有效模板，因此管理员更新后新生成生效。此项只改 PluginHost 选择逻辑、PRD/操作文档与验收，不改共享契约、模块依赖或技术栈。建筑、结构、工艺目前复用同一图幅模板；表格排版仍在 V1 范围之外。

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

## 2026-09-27 ADR-014：桌面设计说明独立内容库（T25）

用户批准把旧说明整理成可按篇章选用、按工程填写数值的模块化资料库，并要求独立文件夹。采用顶层 `content-library/` 保存公开的格式与整理工具，`content-library/private/` 保存本机候选包（忽略，不推送）；应用导入后复制到本机用户数据 `data/content-library/`。不把旧工程值、正文或私有来源打进公开仓库与前端构建。内容包 `packageId` 基于正文与来源哈希，`schemaVersion` 控制兼容。

原桌面 `sec-*` 章节与旧资料 `CH01–CH25` 主题分类分别保留稳定 ID；前者用于既有章节组合，后者由用户按需添加。选择条款时把文字、来源、包版本和核对状态复制到当前说明，工程取值按字段 ID 保存在这份说明中；更新资料包不会静默更改旧草稿。`project_name`、`project_location` 直接使用 01 步当前工程资料。变更仅在桌面 Electron 的本机数据模型、存储和界面，.NET DOCX 工作进程仍收到最终纯文本段落；CAD 共享契约、模块依赖和技术栈不变。旧草稿缺新增属性时解析为无候选条款、空字段，继续可读。

内容风险边界：1405 条全部待核定；当前选择一条并确认只代表用户对**本份工程**的适用性确认，预览保留待核定提示。未确认条款、未填字段、未知占位符阻断导出；表格与疑似残留旧项目名暂不能选入。全局标准批准仍需核定人、日期、适用条件、规范现行性和可使用范围记录，不由该操作代替。

## 2026-10-05 T36 本地文字型PDF（用户已确认）

用户在方案选择中明确同意先本地文字型PDF、新增解析依赖，扫描件暂不OCR。仅desktop/worker引用PdfPig 0.1.16（Apache-2.0、锁定内容哈希），复用现有bid-extract白名单操作，按扩展名分派到独立BidPdf。Electron主进程读取已导入哈希原件并保存逐页摘录，AI内部选择支持可选页范围，旧任务未带页范围仍按原语义读取。不改CAD Contracts或依赖方向，不新增运行时/数据库/云解析。限额和失败行为见[BID_PDF_PARSING](BID_PDF_PARSING.md)。

## 2026-10-06 T38 易标原框架直接移植

用户明确改变T33的只参考/自行实现路线，并确认独立Electron41和自动上传本地化；详见[ADR-018](adr/ADR-018-bidding-framework-import.md)。现有桌面入口保留Electron44/.NET10，投标源码子树desktop/bidding-framework使用原React/SQLite/Pi Agent/OpenXML架构和运行时，通过独立进程打开及返回，数据隔离；CAD依赖方向与设计说明流程不改变。上游许可/出处和对应源码随候选提供；正式发行、安全和专业验收未完成。
