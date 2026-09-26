# 工程设计说明智能编制与 CAD 排版系统

## PRD V1.0 开发规格

版本：1.0　日期：2026-09-21　对象：开发 Agent、程序员、测试人员、业务验收人。

本文件定义 V1 开发和验收基线，并保留长期产品架构。V1 实现 DOCX → Document Model → 排版引擎 → AutoCAD DBText，使设计人员在 Word/WPS 中编辑说明，再按院内固定标准生成 CAD。Markdown 为维护主文件，Word 为同内容阅读版；修订主文件后重新生成 Word。

约束用语：MUST 为必须；MUST NOT 为禁止；SHOULD 为优先实施。状态分为 Confirmed（已确认）、Baseline（本 PRD 补足的可执行默认规则）、Calibration（实施前标定项）、Future（不属于 V1）。Baseline 可在技术评审时通过明确变更替换，不得擅自更改 Confirmed。全部参数示例均非真实院内标准。

## 1 产品目标与用户

当前说明主要在 AutoCAD 单行文字中维护，改长段落、插入条款和整体校对费时，复用旧项目容易残留旧信息。V1 解决内容编制与工程排版之间的转换；不自动判断工程设计正确性，也不以 AI 作为运行前提。

主要用户为结构设计人员；业务验收人为院内标准负责人及设计人员；维护人员负责标定、发布固定标准包。未来服务建筑、给排水、电气等专业。V1 不建设账号、审批或权限后台。

成功结果：支持范围内的正文、编号和工程符号无遗漏、无重排、无意义改变；文字满足固定院标；满栏换栏、满页续页；点击一次确定整批说明位置；修改后删除旧结果再生成。目标是显著减少人工排版时间，具体成效通过第 13 节测量。

## 2 已确认的产品原则

- AP-001 内容协议：所有来源先转换为 Document Model。JSON 是主要机器数据格式；Markdown 仅用于辅助阅读、调试、交换或未来 AI 输入，不能成为唯一底层协议。
- AP-002 内容权威：用户保存后的最终 DOCX 是 V1 正文权威。Document Model 是该文件的结构化快照；CAD 是输出成果。不得将 CAD 修改自动回写 DOCX。
- AP-003 表现归属：Word 负责标题层级、段落、编号和必要文本语义；CAD 院标与 Layout 模板决定字体、字高、宽度系数、缩进、坐标和行距。Word 字体、字号、颜色、页边距、页面尺寸、段前段后数值不得控制 CAD。
- AP-004 固定标准：院标内置为版本化配置资产，不从当前 DWG 现有文字推断院标。A1/A2/A3 各有固定 Layout 模板，不提供用户模板编辑器或矩形识别流程。每次新增说明时由设计人员选择本次已发布图幅/模板版本和 DOCX，核对单位；上次选择可预填但不锁定整张图。
- AP-005 固定网格：Visual Line 分配到固定 Row Slot，按 Column 顺序排布，满页自动续同规格 Page，不挤字号、不临时增栏、不改变行距。
- AP-006 标题规则：V1 一级标题、二级标题与正文同字高，标题前后额外占行均为 0。标题按实际换行数占槽。80 行只是说明示例，真实 RowCount 必须标定。
- AP-007 输出对象：Paragraph 是内容单元，Visual Line 是排版单元，DBText 是渲染单元。普通视觉行对应一个 DBText，符号或上下标组合可对应多个 DBText；不得将整段交给 MText 自动换行。
- AP-008 插入与重出：鼠标点击第一张图面的左上角 Anchor，后续页按固定偏移连续放置。不做增量更新、自动覆盖、持久 CAD 对象追踪或局部替换；用户删除旧说明后重新完整生成。
- AP-009 跨专业：结构专业仅是首批验证样本。核心模型、引擎和接口不得硬编码结构专业字段、章节名称或枚举穷举全部专业。

## 3 V1 范围

### 3.1 In Scope

V1 MUST 交付轻量 AutoCAD 插件、可独立测试的应用服务与核心引擎、DOCX 解析器、JSON 协议、院标包、A1/A2/A3 固定模板、中文和工程 token 换行、固定行槽分页、DBText 插入、诊断报告及结构专业验收样本。读取 Word/WPS 保存的标准 DOCX，不依赖 Word COM 或安装 Microsoft Word。

V1 支持一级标题、二级标题、正文、手工编号、明确空行、显式行内换行及已标定的工程字符。自动编号和上下标按第 6 节的有限输入契约处理，不宣称兼容任意复杂 DOCX。JSON 导出/回读属于开发与诊断能力；普通用户不必看到或编辑 JSON。

### 3.2 Out of Scope

V1 不实现独立桌面工作台、项目数据库、参数继承覆盖、模块编制、AI、多人协同、业务版本管理、CAD→DOCX 迁移、表格/图片排版、公式编辑器、专业规范校审、任意 Word 视觉格式复刻、图框自动识别、自定义模板编辑器、MText 输出或其他 CAD 平台适配。

可视化预览不作为 V1 必交项；生成前 MUST 提供页数、模板和诊断摘要。A1/A2/A3、自动续页和几何越界检查已经属于 V1，不得留到“模板完善”阶段。图框和标题栏复制不是正文排版的隐含要求，具体按第 7.4 节处理。

### 3.3 使用流程

2026-09-24 用户更新：同一张 DWG 可包含 A1、A2、A3 等不同图幅及不同说明版本；每次新增说明都应由设计人员在 CAD 内的小界面明确选择本次图幅、已发布模板版本及 Word DOCX，再在图上点一次位置。上次选择只作预填，不能锁定整张图；下次可改选。以后扩充图幅时选择界面从已发布模板目录列出可用项，具体新增图幅仍须补齐模型、模板与宿主验收，不因目录里有文件就宣称支持。

2026-09-25 用户再次确认：设计人员日常只选 Word DOCX、图幅并核对单位比例；模板版本不在日常窗口选择，由管理员维护安装包中每个受支持图幅的唯一有效模板。同一图幅出现多个有效模板时明确阻断并提示管理员处理，不能按目录顺序、文件名或历史选择静默决定。建筑、结构、工艺说明目前共用该图幅模板；表格等内容支持另列后续范围，不据此改变 V1 的表格拒绝规则。

此前 2026-09-22 的「工程或当前图设一次设置，日常只点位置」不再作为默认正式流程。多步命令行提问仍只用于开发核对。

正式流程分成两段：

1. 设计人员执行 `DSS`（`DN_NOTE` 为兼容别名），CAD 内弹出简短选择窗口，选择本次 Word DOCX 与图幅，核对图纸单位比例。模板版本由管理员为每个图幅维护唯一有效值，不向设计人员提问。上一次成功选择的 DOCX、图幅和比例可预填；用户每次都能改选。未知单位必须先确认，禁止按文件名、目录顺序或当前图里的旧字样式偷偷换标准。
2. 确认后在图上点一次图面左上角。程序用本次选择完成解析、换行、分栏续页，并一次写入全部页面。无警告时不再追问。下次新增说明重新打开选择窗口，允许同图混排不同图幅；管理员更新模板后，后续生成使用新的唯一有效版本。`DN_NOTE_REPEAT` 仅为明确重用上次选择的快捷命令。

有错误则停止并说明原因，不写入半套文字。只有出现警告时才请设计人员看过再决定继续或取消。取消、失败都不留本次新文字。再次生成不查找、不删除已有文字；要替换时由设计人员自己删掉旧说明后再生成。

`DN_NOTE_SET` 的逐项命令行提问保留给旧图和开发验收，不作为设计人员默认操作。

2026-09-24 用户补充正式推广要求：普通设计人员取得一个发布包后，应能通过图形化安装入口完成部署，随后直接启动受支持的 AutoCAD 使用；不得要求其克隆 GitHub 仓库、打开 PowerShell、手工校验哈希或复制 `.bundle` 文件夹。安装程序负责检查支持的 CAD/运行环境、校验包完整性、放置插件和已批准的标准模板、处理同名旧版本并给出可理解的成功/失败结果与回退办法。组织安全策略和字体授权仍须遵守；缺少授权字体或不支持的 CAD 版本时明确提示，不绕过安全设置、不猜测缺失配置。安装完成后的逐次选择与点位流程按本节最新约定执行。开发验收取证步骤不作为最终用户安装步骤。

## 4 Target Architecture 与 Dependency Rules

### 4.1 长期组件关系

```text
独立桌面应用                         轻量 AutoCAD 插件
项目 单体 参数 模块 版本 协同         文件 模板 Anchor CAD 环境
             \                       /
                 Application Services
                         |
DOCX Parser ----------> Document Core <----- 模块组合与参数绑定
CAD Migration Parser -> Document Model <----- 人工确认后的 AI 候选
                         |
       Institution Standard + Layout Template
                         |
        Tokenize → Measure → Wrap → Row → Column → Page
                         |
                    LayoutResult
                         |
             CAD Renderer      Preview Renderer
                  |
            AutoCAD DBText

外围端口：内网 Project Repository、Version Service、Collaboration
          AI Provider、Geotechnical Parser、DOCX Exporter
```

图示为逻辑调用关系，不承诺所有组件在同一进程。V1 可以在 CAD 宿主内装配核心库；未来桌面应用复用核心，CAD 宿主测量和落图通过适配器调用。不得承诺脱离 CAD 后仍能直接使用宿主专属字体测量器。

### 4.2 强制依赖方向

- DEP-001 Domain/Contracts 只含数据、校验规则和端口，不引用 AutoCAD、Word COM、UI、数据库或 AI SDK。
- DEP-002 Layout Engine 依赖 Domain、固定规则和 ITextMeasureService；不得读取 DOCX、DWG、项目数据库或调用 AI。
- DEP-003 DOCX Adapter 只产出 Document Model 与诊断；不得创建 CAD 对象或决定栏宽和字体。
- DEP-004 CAD Renderer 只消费已经验证的 LayoutResult、渲染配置和插入变换；不得读取 DOCX、重排换行或重新绑定项目参数。
- DEP-005 AutoCAD 类型和 API 仅出现在 Cad Adapter/Renderer/宿主交互层。接口使用自有 Point、Bounds、Run 等值类型，不泄露 ObjectId 等宿主对象。
- DEP-006 插件仅负责交互、环境检查、装配调用及事务落图。解析、分词、换行不得堆放在命令入口。
- DEP-007 院标、字符映射、模板各有独立 ID 和版本。固定模板以数据发布，坐标不得散落在 if/else 中；用户界面不开放逐项目覆盖院标。
- DEP-008 未来数据库、AI、协同通过外围端口接入。网络不可用不得阻断 V1 本地生成。未来业务模块不得反向成为引擎依赖。
- DEP-009 Document 的身份不能是 DOCX 路径；文档/块内部 ID 用于语义引用和诊断，不意味着在 CAD 实体上建立持久更新标识。
- DEP-010 用虚构 professional code 与测试模板运行核心契约测试，证明无需新增结构专业分支即可排版。该测试不等于已验收其他专业。

推荐逻辑模块：Contracts、DocumentCore、DocxAdapter、LayoutEngine、Standards、AutoCadAdapter、PluginHost、Tests。物理项目数量和语言由技术选型决定，不要求微服务或多进程。已知院内使用 AutoCAD 2021 及以下；必须标定实际支持版本清单，不得承诺“所有旧版本”。

## 5 数据模型与 JSON 契约

### 5.1 通用规则

UTF-8 JSON 为权威序列化格式。MUST 提供 JSON Schema、有效/无效 fixture 和往返测试；本节字段是最低契约，不等于已经交付可执行 Schema。索引统一从 0 开始，UI 显示从 1 开始。坐标内部使用毫米和双精度数；序列化禁止 NaN/Infinity。

schemaVersion 与文档业务版本不同。支持的主版本内只追加可选字段；未知扩展字段可保留，未知主版本或未知必需 block type 必须拒绝，不得静默忽略正文。迁移通过显式转换器完成。extensions 采用命名空间 key，核心规则不依赖任意扩展值。

### 5.2 Document Model

| 对象 | 最低字段 | 约束 |
| --- | --- | --- |
| Document | schemaVersion, documentId, disciplineCode, source, blocks, extensions | blocks 保留原阅读顺序；无 CAD 坐标 |
| Source | kind, name, contentHash | kind=docx；路径仅本地诊断可选，不作为身份 |
| Block | id, type, sourceRef, runs, numbering | type 为 heading1/heading2/paragraph/spacer；spacer 使用 slotCount |
| TextRun | text, semantic | semantic 为 normal/superscript/subscript；硬换行使用独立 break run |
| Numbering | label, sourceKind, level | label 为已解析可见编号；manual/automatic；不得重复加编号 |
| SourceRef | paragraphIndex, runIndex, textRange | 指向原始段落与范围，供错误定位 |
| Diagnostic | code, severity, stage, message, sourceRef, details | error 阻断，warning 可审阅继续，info 说明行为 |

heading3/table/image 等不在 V1 支持集，预留 discriminated block 扩展方式，不得默默降级。原始正文空白与显示空白的差异必须可追溯；不得规范化工程符号为相似字符。字符串原样保留，显示编号与正文分开，避免自动编号重复。

```json
{
  "schemaVersion": "1.0",
  "documentId": "sample-doc-001",
  "disciplineCode": "structure",
  "source": {"kind": "docx", "name": "示例.docx", "contentHash": "example-only"},
  "blocks": [
    {"id": "b1", "type": "heading1",
     "sourceRef": {"paragraphIndex": 0},
     "runs": [{"text": "材料要求", "semantic": "normal"}]},
    {"id": "b2", "type": "paragraph",
     "sourceRef": {"paragraphIndex": 1},
     "numbering": {"label": "1.", "sourceKind": "automatic", "level": 0},
     "runs": [{"text": "混凝土采用C30。", "semantic": "normal"}]}
  ],
  "extensions": {}
}
```

### 5.3 Institution Standard 与 Layout Template

2026-09-22 用户授权补充：本项目不要求复刻完整院标；沿用已确认的字体组合，其余文字格式由项目制定。[项目说明文字格式 V1.0](docs/TEXT_FORMAT_V1.md) 为字体、统一字高、行距和缩进的实施依据。项目主动制定完整标准，允许多套机构/个人标准以独立 ID 和版本并存、选择、修改；格式与图幅模板是独立配置入口。当前套 A1/A2 三栏、A3 两栏，字高不随图幅变化，详见 [三图幅模板](docs/PAPER_TEMPLATES_V1.md)。本节 Institution Standard 保留既有名称与契约，含义为项目固定标准；已制定参数与尚未完成的宿主/打印验证须分开记录。此补充不扩大 V1 功能范围。


院标最低字段：standardId、version、fontProfile、textHeight、widthFactor、obliqueAngle、rowPitch、styles、layerPolicy、symbolMapVersion、lineBreakRuleVersion、measurementTolerance。fontProfile 记录字体种类、文件身份及必要大字体；styles 定义 heading1/heading2/body 的字体引用、缩进和编号悬挂缩进。三者字高必须相同，标题 beforeSlots/afterSlots 必须为 0。

Layout Template 最低字段：templateId、version、disciplineCode、paperCode、orientation、unit、standardRef、anchor、pageBounds、columns、pageStep、framePolicy。columns 是有序数组，每项含 columnId、left、right、top、bottom、firstBaselineY、rowCount。rowPitch 的权威来源是院标；若模板保存 resolvedRowPitch 快照，必须与引用院标一致，否则拒绝。

rowCount 在标定时确定并冻结，运行时只校验几何适配，不能随正文自动改变。anchor=(0,0)，x 向右、y 向上，说明区位于 Anchor 左下侧。Page 是排版页，不等同于 AutoCAD 的 Layout 纸空间对象。

```json
{
  "templateId": "structure-A1",
  "version": "calibration-draft",
  "status": "uncalibrated",
  "paperCode": "A1",
  "unit": "mm",
  "standardRef": {"id": "institution-note", "version": "pending"},
  "anchor": {"kind": "note_top_right", "x": 0, "y": 0},
  "columns": [],
  "pageStep": null,
  "framePolicy": "none"
}
```

此片段仅示意待标定资产，刻意不是可运行配置。发布校验 MUST 拒绝空 columns、null pageStep、未标定状态及非正 rowCount；不得将缺值填 0 后落图。V1 三个正式模板栏数/方向分别标定；引擎不能硬编码三栏。

### 5.4 LayoutResult 与 RenderRequest

LayoutResult 包含 schemaVersion、documentHash、standardRef、templateRef、engineVersion、measurementProfileHash、pages、diagnostics、statistics。Page 包含 pageIndex、pageOffset、columns；Column 包含 columnIndex、rows；RowSlot 包含 rowIndex、baseline、occupancy（text/spacer）、visualLine。

VisualLine 包含 sourceSlices、text、measuredWidth、inkBounds、renderRuns。RenderRun 包含 text、relativeOrigin、baselineOffset、resolvedStyle、measuredAdvance、inkBounds；样式已经解析，Renderer 不得再猜测格式。sourceSlices 允许一行跨多个 Word run，但不得跨不同段落拼成一段。

RenderRequest 包含 LayoutResult、anchorWcs、unitScale、targetSpace、environmentFingerprint。生成前环境与测量时不同（字体、样式配置、单位、宿主文档）则重新校验或重排。结果必须记录页/行/对象数量、告警、耗时；不存储用于未来增量更新的持久实体映射。

## 6 DOCX 输入与文本契约

### 6.1 支持边界

采用极简样式约定，不要求 Word 页面看起来像 CAD。一级/二级标题由明确的 styleId、样式继承链或已标定别名映射识别；不凭字号或加粗猜标题。正文使用约定正文样式。映射表是院标包的一部分。

| 内容 | V1 行为 |
| --- | --- |
| Heading 1 / Heading 2 / 正文 | 必须支持；中文和 WPS 样式别名标定 |
| 手工编号 | 作为原正文保留，不再自动编号 |
| 自动编号 | Baseline 支持样本覆盖的十进制单级/多级编号及重启；按 numbering 定义生成可见 label；其他格式明确阻断并提示转为手工编号 |
| 空段落 | Baseline 每个真实空段映射 1 个 spacer；连续空段不合并；末尾编辑器终止空段不生成无内容尾页 |
| 显式文本换行 | Baseline 强制结束当前视觉行，连续显式换行保留空槽；Word 自动折行不保留 |
| 页面和分栏控制符 | 不控制 CAD 页/栏，记录 info；页眉页脚不进入说明正文 |
| 普通加粗 下划线 颜色 | 不决定 CAD 表现；存在时报告忽略格式信息，文本保留 |
| 字面 ² ³ 与工程符号 | 按字符映射保留；不得替换为普通 2/3 |
| Word 上下标 run | Baseline 按标定的缩放和基线偏移生成多个 DBText；缺标定或字形则阻断，不扁平化改变语义 |
| 图片 表格 文本框 公式 脚注 | V1 不排版；检测到承载内容时阻断，列出位置，不静默丢失 |
| 修订 动态域 嵌入对象 | Baseline 未接受修订/未固化域阻断；提示用户确认修订或转换静态文本 |
| 未映射样式 三级标题 Tab | 报告位置并要求套用支持样式/用明确文本替换；不猜测缩进或丢弃内容 |

上述 Baseline 是 V1 的有限支持策略，不等于完整 Word 兼容。应提供最小示例 DOCX 和一页输入说明，列出支持样式、编号及符号写法。

### 6.2 宽度和字符测量

MUST 按最终固定字体、字高、宽度系数、倾斜角、大字体及符号映射测量，不允许以“汉字数×字高”作为正式换行依据。测量返回 advance 与字形边界，检查左右及上下越界。最终候选整行必须复测；不能仅把独立字符宽度相加当作所有字体下的真实行宽。

V1 优先由 CAD 适配器提供宿主测量；可缓存相同字体环境和文本结果。缓存 key 含字体身份、院标版本、字高、宽度系数、文本和上下标参数。不得复用不同环境的缓存。临时测量对象不得残留图中；宿主线程约束由适配器负责。

### 6.3 换行优先级

1. 按源顺序解析 grapheme cluster、中文字符、标点、英文单词、连续数字和工程 token；不得拆代理对或组合字符。
2. 优先最长匹配工程 token，例如 C30、HRB400、Φ20@200、0.10g、35mm、20kN/m²、1/1000、GB 50010-2010、GB/T 50011-2010。例子仅是排版样本，不是工程规范有效性声明。
3. 当前可用宽度为栏宽减首行/续行缩进与边界保留量；逐步加入单元，寻找满足实际宽度及禁则的最靠后断点。英文短语优先在空格处断，规范编号中的空格可属于已识别 token。
4. 基本行首禁则包括 ，。；：！？、）】》〉”’ 及对应半角闭合符号；行尾禁则包括 （【《〈“‘ 及对应半角开启符号。具体字符集合随规则包版本发布。回退时移动完整 token，不能为搬标点而拆短工程表达式。
5. 完整 token 能放入空行但放不进当前余量时，整体移到下一行；禁止缩字或压缩宽度系数。
6. 单 token 比空行可用宽度还大时，允许按 grapheme 边界强制拆分，并产生 W_TOKEN_SPLIT，记录原词和断点。只有此异常允许拆英文/数字/工程 token；工程师查看警告后方可继续。
7. 若单字符也放不下，或禁则导致无法形成非空合法行，返回 E_NO_LEGAL_BREAK；不得无限回退或输出越界文字。排版循环每步必须消费内容、推进有限槽位或返回错误。

正文不做自动两端拉伸、不插入连字符、不删字改句。行边界用于显示的可折叠空格处理必须有 sourceSlices 映射，审计时能重建原始文本；编号和单词中的有意义空格必须保留。

## 7 Row Slot 分栏分页与插入

### 7.1 固定行槽

每个视觉行消耗一个 Row Slot；上下标与符号分段共享该槽。每个显式 spacer 消耗规定的整数槽位，不创建空 DBText。标题与正文同字高，标题前后不额外插槽。Baseline 不启用自动“标题与下一段同栏”，避免与固定填槽规则产生未声明留白；后续若院标要求，再通过独立规则变更。

对第 p 页、第 c 栏、第 r 行（0-based）：

```text
pageOffset(p) = p × pageStep
xLocal = columns[c].left + indent(line)
yLocal = columns[c].firstBaselineY - r × standard.rowPitch
worldPoint = anchorWcs + unitScale × (pageOffset + localPoint)
```

rowCount 是唯一容量基线，不能用字体 bounding box 高度动态挤出更多行；但必须验证首末行和上下标字形边界均在栏内，rowPitch 足以避免相邻行重叠。不能仅用 (top-bottom)/rowPitch 推算生产行数而忽略第一基线偏移。

### 7.2 确定性续排

```text
p = 0; c = 0; r = 0
while remaining content exists:
  if r == rowCount(c): c += 1; r = 0
  if c == columnCount: p += 1; c = 0
  get next visual line using current column's available width
  place line or spacer in slot(p, c, r)
  r += 1
```

不同栏宽必须在切换栏时使用新栏宽重新计算未消费文本，不得一次按第一栏宽切完整篇。只有还有内容待排时才创建下一 Page，因此刚好填满末槽不产生空白下一页。段落可跨栏跨页，源段落身份保留；无需在每页重启编号或重复标题。

所有续页使用同 templateRef 和 standardRef，页偏移按固定 pageStep 计算。pageStep、页边界及间距须验证相邻页不重叠。内存/页数上限仅用于异常资源保护，达到上限必须报错，不得把正常满页作为失败，也不得截断生成前几页。

### 7.3 Anchor 与坐标系

Anchor 定义为模板说明区域固定右上角，不是本次文字实际包围盒右上角。因此内容长短不会改变同模板首行位置。图框右上角与说明区右上角如有差值，需在样板中标定并展示点击说明，不得默认两者重合。

Baseline V1 在当前 DWG 模型空间生成，方向为 WCS 水平，旋转角为 0。用户点选坐标转换为 WCS 后使用；非默认 UCS 必须测试。图纸单位到毫米的比例明确配置；未知单位时阻断并提示，不猜比例。任意旋转、纸空间布局、视口比例自动推导均为后续能力。

第一次点击确定全部页位置，不逐页点击。插入前的页数摘要说明后续页排列方向和跨度。已有图形碰撞自动检测不属于 V1；用户选择空白区域，程序不得自动移动或清除既有实体。

### 7.4 Page 与图框的边界

自动续页 MUST 新建同规格逻辑页并在 DWG 中生成下一页说明，不能只在内存中分页。Baseline framePolicy=none：不画辅助矩形、行槽线或标题栏，使用现有图框或由用户另行配置图框。

如院内确认“续一张图纸”必须同步复制真实图框，则在标定项 CAL-05 中提供可复用图框块及相对 Anchor 偏移，使用独立 framePolicy=standardBlock；该适配器只复制标准资产，不识别任意图框，不推断项目标题栏内容。此选择影响落图适配与验收样本，不改变正文引擎；未标定前按 none 开发，不阻塞 PRD。

## 8 功能需求编号

以下均为 V1 MUST，除明确标注的可选辅助项。详细规则以对应章节为准。

### REQ-DOC-001 文件解析

输入：已保存 .docx 与样式映射。输出：Document Model、source hash、诊断。规则：只读源文件，按阅读顺序解析支持块；不依赖 Word COM。异常：损坏、加密、被占用无法读取、未支持内容均明确定位并阻断。验收：AC-01、AC-02。

### REQ-DOC-002 语义和编号

输入：段落、runs、样式与编号定义。输出：语义块、独立可见编号、sourceRef。规则：保留章节层级、手工编号、显式空行及支持的上下标；不复制 Word 视觉格式。异常：无法确定自动编号/未知样式不得猜测。验收：AC-01、AC-03。

### REQ-DATA-001 协议序列化

输入：Document Model/JSON。输出：可验证 JSON/同义模型。规则：版本校验、字段约束、sourceRef；JSON 往返不丢语义。Markdown 导出可作为辅助工具，不得替代 JSON。异常：未知主版本/块类型拒绝。验收：AC-04。

### REQ-STD-001 固定标准资产

输入：院标与 A1/A2/A3 模板包。输出：已校验配置快照。规则：缺关键参数不能发布；院标覆盖目标生成样式，不继承当前 DWG 默认样式。异常：字体缺失、样式同名异义、图层冲突明确处理。验收：AC-05。

### REQ-TEXT-001 测量与字符呈现

输入：最终样式与文本。输出：宽度、字形边界与 renderRuns。规则：同一套度量用于排版与实际落图，符号映射前后一致。异常：缺字形不得用问号/方框或相似字符替代。验收：AC-06。

### REQ-LAYOUT-001 中文换行

输入：语义块、当前栏宽、测量服务。输出：不越界 Visual Lines。规则：第 6.3 节全部优先级；不改正文。异常：长 token 警告；无法合法排版阻断。验收：AC-07。

### REQ-LAYOUT-002 固定槽与续页

输入：视觉内容流、固定模板。输出：Page/Column/RowSlot。规则：标题同字高无额外空行，满栏换栏，满页续同模板，刚好满页无尾空页。异常：非法模板、资源限制不允许部分成功。验收：AC-08、AC-09。

### REQ-CAD-001 点击定位与落图

输入：有效 LayoutResult、点选 Anchor。输出：多页可编辑 DBText。规则：第 7 节坐标换算，普通一行一个对象，上下标可多对象，禁止再换行。异常：取消不落图；宿主环境变化重新验证。验收：AC-10、AC-11。

### REQ-CAD-002 原子生成与撤销

输入：整批生成请求。输出：完整结果或零新增结果。规则：预检通过后在文档锁/合适宿主上下文中执行事务；失败回滚本次文字及新增资源；一次撤销恢复生成前状态。不得修改既有同名样式影响旧图。验收：AC-11、AC-12。

### REQ-UX-001 诊断与重出

输入：执行状态/诊断。输出：按源段落定位的问题和完成摘要。规则：error 不提供“仍生成”；warning 先展示再继续；重出不寻找或更新旧说明。无警告走最短路径。验收：AC-02、AC-12。

### REQ-OPS-001 可复现运行

输入：版本化文件、配置及宿主环境。输出：运行报告。规则：记录输入 hash、规则版本、页/行/对象数、阶段耗时与错误码；默认本地，不上传正文。提供取消及资源上限。验收：AC-13、AC-14。

## 9 Extension Points 与长期能力

### 9.1 扩展端口

Active 表示 V1 必须有可替换契约及真实实现；Reserved 表示本 PRD 固定边界与输入输出方向，不要求空类、后台、数据库表或网络服务。Future 端口在对应阶段细化方法签名和权限。

| 端口 | V1 状态 | 输入与输出 | 未来方向 |
| --- | --- | --- | --- |
| IDocumentSource / IDocumentParser | Active DOCX | source → Document + Diagnostics | CAD、Markdown、数据库来源 |
| IStandardProvider | Active 固定包 | standardRef → 院标快照 | 专业标准版本管理 |
| ILayoutTemplateProvider | Active A1/A2/A3 | templateRef → 模板 | 更多专业与图幅 |
| ITextMeasureService | Active CAD 适配 | runs + style → 度量 | 其他宿主或一致性验证后的独立测量 |
| ILayoutEngine | Active | Document + Standard + Template → LayoutResult | 表格、复杂块 |
| ICadRenderer | Active DBText | LayoutResult + Transform → RenderReport | 其他 CAD 平台 |
| IPreviewRenderer | Reserved | LayoutResult → 可视预览 | 桌面预览、PDF |
| IParameterProvider | Reserved | project/unit/revision → 参数快照与来源 | 继承覆盖 |
| IModuleProvider | Reserved | module refs + 参数 → 语义块 | 条款组合、条件触发 |
| IDocumentExporter | Reserved | Document → DOCX/Markdown | 历史迁移、模块编制 |
| IAIProvider / IGeotechnicalParser | Reserved | 授权资料 → 候选及来源 | 地勘提取、校审、推荐 |
| IProjectRepository | Reserved | 查询/带预期版本写入 → 项目快照 | 内网集中存储 |
| IVersionService | Reserved | 快照引用 → 版本/差异 | 内容及生成版本追溯 |
| ICollaborationService | Reserved | 参数变更事件 → 影响列表/状态 | 多人协同 |

V1 核心签名应体现 cancellationToken 和 Diagnostics：Parse(source, profile)、Measure(runs, style)、Layout(document, standard, template, measure)、Render(result, transform)。渲染失败不返回伪成功；measurement service 不要求 UI 或项目服务。

### 9.2 项目参数与单体继承覆盖

Future 数据关系：Project → Units → DisciplineDocuments；项目参数与单体覆盖分开保存。参数最低语义包含 key、value、unit、source、confirmedBy、revision。有效值=显式单体覆盖值，否则继承项目值；覆盖状态独立存储，0/false/空字符串不能用作“没有覆盖”的判断。

公共参数变更不覆盖单体显式覆盖。应返回受影响单体、原继承值、新继承值、冲突和需要重新生成的文档。清除覆盖才恢复继承。负责人和设计人员按未来项目权限修改，不引入复杂 OA 审批作为核心依赖。

绑定产生 Document Model 或初始 DOCX 后，工程师仍可自由增删正文。再次参数绑定必须识别人工修改并展示差异；没有可靠绑定来源时提示人工确认，不静默替换用户最终 DOCX。V1 不做这项同步逻辑。

### 9.3 模块库与专篇

Future 支持通用条款、框架、水池、钢结构、防腐及独立专篇；专业名与模块均为数据。模块含稳定 ID、版本、适用条件、参数声明、语义块及来源。组合允许多模块、排序与冲突提示；条件触发给出理由，人工可增删。输出仍进入 Document Model，不直接输出 CAD 坐标。

### 9.4 内网版本与协同

Future 内网服务集中管理项目、单体、参数、文档和模块版本。每次生成记录输入文档 hash、参数快照、模块版本、模板/院标版本及输出状态；schemaVersion、模板版本和业务修订号分别管理。

公共参数变化 → 计算受影响单体与文档 → 标记待处理 → 设计人员确认/覆盖 → 重新生成 → 记录状态。多人同时修改采用预期版本检查并返回冲突，禁止后写静默覆盖。离线缓存、部署方式和认证在该阶段定义；不能把共享盘文件覆盖当作完整协同实现。

业务文档版本和重新生成状态不等于 CAD 实体增量更新。即使未来有版本服务，也可继续删除重出工作流。

### 9.5 AI 与历史迁移

AI Future：地勘 PDF 参数提取、说明矛盾检查、旧项目信息检查、规范变化提示、模块推荐及图审意见联动。候选结果必须带原文/页码或定位、来源身份和不确定性；工程师确认后才写入关键参数。接口允许院内平台替换，默认不把项目资料发送公网，V1 不调用 AI。

CAD→DOCX Future：读取历史文字 → 空间顺序重建 → 段落/标题候选 → 人工校对 → Document Model → DOCX。记录低置信度和歧义，不承诺从碎片 DBText 无损恢复原始段落。用于初始资产迁移和老项目兼容，不改变日常 DOCX→CAD 主链路。

## 10 异常处理与数据保护

| 错误码 | 情况 | 行为与恢复 |
| --- | --- | --- |
| E_DOCX_READ | 无法读取 损坏 加密 | 终止；提示路径及保存为普通 DOCX |
| E_UNSUPPORTED_CONTENT | 表格 图片 公式 未映射语义 | 指明位置和类型；修复源文档后重试 |
| E_NUMBERING | 编号规则不支持或无法确定 | 不猜编号；转手工编号或补映射 |
| E_SCHEMA_VERSION | 协议主版本或必需块不支持 | 拒绝；升级或显式迁移 |
| E_TEMPLATE_INVALID | 空栏 非正行数 越界 未标定 | 不生成；由维护人修复配置 |
| E_FONT_MISSING | 字体/大字体缺失或字形不可用 | 阻断；安装批准字体或补符号映射 |
| E_NO_LEGAL_BREAK | 单字过宽或无合法断点 | 定位原文及栏；不循环 不越界 |
| W_TOKEN_SPLIT | 超长 token 被迫拆分 | 显示原 token 和断点；审阅继续或取消 |
| E_CAD_ENV | 非支持版本 空间 单位 锁定图层 | 显示具体环境原因；修复后重新校验 |
| E_STYLE_CONFLICT | 同名样式与院标不同 | 使用独立版本化样式名或阻断；不修改旧样式 |
| E_RENDER_FAILED | 创建对象失败 | 回滚整批；报告阶段及原因 |
| E_RESOURCE_LIMIT | 页数 文件大小 内存上限 | 终止并说明限制；禁止截断成功 |
| CANCELLED | 用户取消 | 清理临时数据及未提交事务；不报成功 |

检查 DOCX 压缩展开大小、解析时长和实体资源上限，防止异常文档耗尽宿主；具体上限 CAL-09 标定。禁执行文档宏和外部链接；读取源文件不得修改原件。默认诊断只记必要 hash、定位和错误信息，正文摘录只在本地报告；不启用公网遥测。生成输出保留为可编辑文字，字体与模板资产分发须核实使用授权。

## 11 非功能要求与性能口径

- NFR-001 一致性：同一输入模型、规则版本、字体环境和引擎版本产生相同分页、行文字及坐标；随机运行 ID/耗时不参与几何一致性比较。
- NFR-002 完整性：支持内容字符完整率 100%，编号不重复、不漏项；标点和特殊字符不得因格式转换被替换。审计排除明确忽略的页眉页脚及页面控制符，但必须报告忽略项。
- NFR-003 几何：所有文字字形在栏边界内且相邻行不重叠，允许数值公差由 CAL-07 定义；公差不能用于容忍可见越界。
- NFR-004 响应：Baseline 性能目标为暖启动、已保存文件、1 万字符/不超过 10 页样本，从点击生成到准备点 Anchor 的 P95 ≤ 3 秒，从点 Anchor 到全部提交 P95 ≤ 1 秒；最终按 CAL-08 指定机器和样本标定。此为目标而非实测承诺。
- NFR-005 计时：分别记录读取、解析、测量/排版、等待用户、落图和总时长；不得将用户等待计入引擎耗时，也不得隐去启动/加载成本，冷启动单独记录。
- NFR-006 可维护性与安装：发布包包含版本、支持环境清单、已批准的标准模板包及部署/回滚说明；不要求开发机路径或联网安装依赖。面向普通设计人员提供图形化安装入口，部署成功后重启 AutoCAD 即可发现插件；安装、升级和卸载均无需 GitHub、命令行或手工复制 `.bundle`。环境不满足要求时给出明确原因，不降低 CAD 或单位电脑的安全策略。
- NFR-007 可取消：耗时阶段提供进度和取消；实际取消时延 CAL-08 标定。事务提交期间不可安全取消时显示正在完成，失败仍须回滚。

## 12 验收标准与追踪矩阵

发布门槛：所有 MUST 功能通过；无未解决的 error；标定数据齐备；性能目标按最终样本实测；结构专业业务人员完成图面核验。不能用模拟测量单测替代实际 AutoCAD 字体核验。

2026-09-22 用户确认的验收范围调整：打印核验不单独立项，由 T12 最终验收做一次真实打印确认（DBText 为 CAD 原生实体，无可由插件标定的打印参数）；符号映射以 passthrough-1.0.0 为发布值，以 20～50 条真实句子字符清单核对结果为准，不构建完整符号映射表。上下标仍是 V1 有限输入契约：有标定按缩放与基线偏移生成，缺标定或字形则阻断。

| 验收编号 | 输入或操作 | 可判定结果 | 关联需求 |
| --- | --- | --- | --- |
| AC-01 | 同内容的 Word 与 WPS 文件，含标题 正文 编号 | 语义一致；字符及编号完整率 100% | DOC-001/002 |
| AC-02 | 损坏 DOCX 表格 未接受修订 未映射样式 | 逐项错误定位；DWG 无新增对象 | DOC-001 UX-001 |
| AC-03 | 只改 Word 字体 字号 页边距 行距和颜色 | CAD 分页 坐标 样式不变；标题同正文字高且无额外空槽 | DOC-002 |
| AC-04 | JSON 往返 未知字段 未知主版本/块 | 支持语义不丢；不支持主版本/块拒绝 | DATA-001 |
| AC-05 | 三模板及同名冲突样式 缺字体 缺标定值 | 各模板合法；不改既有样式；缺值或缺字阻断 | STD-001 |
| AC-06 | Φ ± ℃ ≤ ≥ ² ³、上下标及 20～50 条真实句子 | 屏幕与实体属性字形正确；测量与实体边界误差在标定内（打印由 T12 确认） | TEXT-001 |
| AC-07 | 中文标点临界位置 短 token 长 token 单字过宽 | 禁则通过；短 token 完整；超长警告；无合法断点阻断 | LAYOUT-001 |
| AC-08 | 测试模板 3 栏×2 槽，分别输入 6 行与 7 行 | 6 行=1 页；7 行=2 页且第 7 行在第二页首栏首槽 | LAYOUT-002 |
| AC-09 | A1/A2/A3 各自跨至少 2 页；含空行 跨栏段落 不同栏宽 | 同规格续页；无漏行 重行 空尾页或越界；缩进正确 | LAYOUT-002 |
| AC-10 | 两个 Anchor 相差已知向量；非默认 UCS；指定单位 | 全对象相对位置相同，整体平移正确，续页偏移正确 | CAD-001 |
| AC-11 | 普通行与组合符号；中途取消/故障；一次 Undo | 对象为 DBText；失败零残留；一次撤销还原 | CAD-001/002 |
| AC-12 | 框选删除旧说明后重出；不删除直接再生成 | 前者正确重出；后者新增独立结果，不更新/删除旧图 | CAD-002 UX-001 |
| AC-13 | 同输入运行两次；断网；非结构测试 disciplineCode | 几何一致；本地可完成；核心无专业专用分支 | OPS-001 DEP-010 |
| AC-14 | 冷暖启动 性能样本 资源上限 故障重试 | 输出各阶段记录；满足标定门槛；上限错误无截断 | OPS-001 NFR |

测试分层：纯模型和换行单元测试；测量契约测试；宿主字体与实体集成测试；三套真实图幅业务验收。Golden fixtures 固定输入、规则、测量环境与预期结果，变更必须解释差异。验收报告记录环境、版本、样本 hash、通过项、未通过项及复核人。

## 13 竞赛演示与量化成效

### 13.1 演示脚本

建议 6～8 分钟：先展示传统 CAD 单行编辑及改段落的操作；在 Word/WPS 修改同一份真实脱敏说明；选择 A1 并点击 Anchor，展示三栏及自动续页；放大中文标点、工程 token、上下标；切换 A3 重排；删除旧说明后重出；展示一例不支持内容如何被阻断；最后展示架构和实测成效表。

现场演示必须使用已验收字体、插件和模板，另备录屏。长期参数、模块、AI、协同只展示为路线图，不能伪装成已完成功能。V1 的算法价值在字符度量、语义转换、禁则换行与可控工程分页，不为参赛强塞 AI。

### 13.2 成效测量设计

采用相同源内容、相同院标和相同质量检查口径，对比人工 CAD 流程与新流程。Baseline 建议至少 3 位设计人员、每人 3 份不同长度说明，交叉安排操作顺序；正式样本量由竞赛准备阶段确定。记录每个样本原始值，不仅给均值。

| 指标 | 计算口径 | 目标或交付 |
| --- | --- | --- |
| 人工操作耗时 | 编辑完成到合格出图的主动操作时间 | 建议目标降低 ≥70%，需实测确认 |
| 端到端耗时 | 从打开任务至检查合格，包含等待及返工 | 同时报中位数与 P95 |
| 内容错误 | 漏字 改字 编号错误/总有效字符或条目 | 支持范围内为 0 |
| 排版缺陷 | 越界 禁则失败 重叠/视觉行总数 | 验收样本为 0 |
| 重出效率 | 删除旧说明至新结果完成 | 拆分删除 点选 系统生成耗时 |
| 首次成功率 | 无需修正文档即可完成的任务数/任务总数 | 报实际值及失败原因，不预设 100% |
| 推广成本 | 新专业增加配置 样本与适配工作量 | 展示专业无关核心测试，不冒称多专业已上线 |

节时率=(人工流程时间−新流程时间)/人工流程时间。年化估算=单任务净节时×年任务量−维护/培训时间；任务量未调查不得编造成效。AI/BIM 竞赛四项各 25 分来自现有项目背景材料，提交前核对当届规则：场景价值用真实痛点，应用成效用对比原始数据，技术难度用引擎实现和边界测试，可推广性用架构与配置隔离证明。

## 14 实施前标定项

这些项目不阻塞 PRD 或通用核心开发，但正式出图发布前必须补齐。不得把缺失参数的样例当成院标。维护人员完成配置，业务标准负责人核对并冻结版本。

| 编号 | 标定内容 | 用户或实施方提供 | 阻塞环节 |
| --- | --- | --- | --- |
| CAL-01 | CAD 版本 位数 OS 单位 模型空间使用方式 | 实际电脑环境与至少一个基准版本 | 宿主集成验收 |
| CAL-02 | 字体 大字体 字高 宽度系数 倾角 图层 | 成熟 DWG 与文字属性 字体文件身份 | 真实测量与输出 |
| CAL-03 | 标题 正文 编号缩进及 rowPitch | 样板首行/续行 标题与正文 | 固定标准发布 |
| CAL-04 | A1/A2/A3 朝向 栏数 边界 首基线 rowCount | 每图幅成熟样板及 Anchor 标注 | 三模板发布 |
| CAL-05 | 多页方向 间距 图框偏移 framePolicy | 连续图幅示意；如需图框则提供标准块 | 多页图面验收 |
| CAL-06 | Word/WPS 样式别名与自动编号样本 | 两种编辑器的同义 DOCX | 解析覆盖验收 |
| CAL-07 | 特殊字符 上下标映射与几何公差 | 2～5 套图纸 字符清单 20～50 条真实句子 | 字符及度量验收 |
| CAL-08 | 性能样本 硬件 冷暖启动及取消目标 | 基准设备 典型/最大说明 | 性能签收 |
| CAL-09 | 文件/解压大小 页数/实体/耗时上限 | 最大真实样本与资源测试 | 异常保护发布 |

建议交付标定包：A1/A2/A3 样板 DWG、对应 DOCX、字体属性说明、Anchor 截图或坐标、character-cases.txt。字符样本格式如下；可由业务人员手工整理，无需编程。

```text
编号：CHAR-001
原文：楼面活荷载标准值为2.0kN/m²。
CAD期望：² 显示为上标；2.0kN/m² 正常栏宽下不拆。
来源：样板名称与位置
补充：不得改成 kN/m2；附现有 CAD 写法或截图。
```

符号映射必须区分 Unicode 码点、CAD 特殊编码、字体私有字形和 Word 语义上下标。视觉相似不等于语义相同；钢筋符号等需逐项确认。

## 15 开发阶段与交付清单

阶段 M0：冻结输入契约与标定表，拆出技术能力地图。调研 DOCX 解析、宿主插件框架、字体测量、中文换行及 CAD 输出可复用库；记录兼容性、许可证、维护性及自研边界。技术选型在此阶段完成，本 PRD 不把某库版本当作已验证结论。

阶段 M1：实现 Contracts、JSON Schema、DOCX Parser 与诊断；用 fixture 通过 AC-01～04。输入不规范时先产生清晰错误，不能以猜测解析替代契约。

阶段 M2：完成真实字体测量最小原型与符号标定，确认宽度系数只按最终有效值应用一次；通过 AC-05～07。测量若不能达到误差要求，先解决再堆 UI。

阶段 M3：实现固定槽、不同栏宽、续页与坐标引擎；通过 AC-08～10。核心测试不要求启动 UI。

阶段 M4：插件交互、事务、撤销、取消及部署；通过 AC-11～14，在承诺版本清单逐项集成验收。

阶段 M5：结构专业试用、三图幅验收、性能及竞赛对比测量、演示录制。存在缺字体、内容丢失或越界不得以“演示版”绕过。

V1 开发交付物 MUST 包括：源码与构建说明、插件安装包、支持环境清单、版本化院标/三模板、Document/Layout/Result Schema、示例 DOCX/JSON、字符与边界 fixture、自动测试、实际 CAD 验收报告、简明操作说明、标定说明、成效原始记录和演示脚本。

后续能力按依赖逐步推进：预览/更多专业模板与表格；历史 CAD 迁移；模块库；项目与单体参数；AI；内网版本与协同。独立桌面应用在模块/项目能力引入时承载工作台，不重新编写核心引擎。顺序可调整，Future 的存在不扩大 V1 验收范围。

## 16 开发 Agent 执行约束

开始编码前先把 REQ 与 AC 拆成任务，明确每项输入、输出及对应测试。未标定数值使用显式 pending 状态或测试 fixture，不得静默填入生产默认值。每项实现声明覆盖的 REQ/AC、真实宿主验证范围及剩余标定项。

不得重新引入已否决方案：DOCX 直接生成整段 MText、继承 Word 排版、每次读取 DWG 推断院标、让用户逐行画框、满页停止或自动缩字、持久 CAD 对象追踪与增量更新、把结构专业章节写死、把 AI 或内网服务做成本地生成的必需依赖。

本版的核心完成条件是可验收的 DOCX→CAD 闭环与稳定扩展边界。架构评审必须验证依赖方向和模型可替换性，而非以预建未来空模块数量衡量完成度。
