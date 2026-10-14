# 设计说明内容库

本文件夹集中管理说明平台的模块化资料。公开仓库只保存格式、转换工具、审核规则和不含真实工程数据的测试样本；`private/` 保存从用户旧说明整理出的候选内容包及报告，已被本目录的 `.gitignore` 排除。

## 目录约定

| 路径 | 作用 |
| --- | --- |
| `build_catalog.py` | 读取本机 `artifacts/content-handoff/`，规范化字段、检查交接包，生成应用可导入的内容包 |
| `build_pool_layout.py` | 从原构筑物 DOCX 和候选包生成按原章序排列的私有水池版式 |
| `ASSEMBLY_RULES.md` | 模板、来源版本、自动装配与暂缓插入规则 |
| `catalog_schema.md` | 内容包、章节、条款与工程字段的格式和审核规则 |
| `private/catalog.json` | 私有内容包；桌面端从项目文件夹自动读取，用户不需要导入或选条款 |
| `private/audit.json` | 本次整理的数量、来源校验和需要人工复核的条目 |
| `private/pool-layout.json` | 水池模板的 8 章、158 段与 7 张表；自动读取，留在 G 盘且不入 Git |

本次私有内容包的正式位置是 `G:\JUSTIFIED_specification reflow for AutoCAD\content-library\private\`。后续在项目根目录运行 `python content-library/build_catalog.py --input artifacts/content-handoff --output content-library/private` 可重新生成；输入与输出都只留在本项目文件夹。桌面端启动时自动查找这个位置，**不会把资料包复制到 C 盘用户数据目录**。工作草稿保留自动装配条款的快照与本工程填写值；以后更新内容包不覆盖已写的说明。

旧资料全部是**候选**，不是平台已批准的标准正文。用户选模板后，`assembly.ts` 按来源版本自动形成章节和正文；用户无需在资料库再次挑选。水池模板优先使用私有 `pool-layout.json`，保留原件章序、分项段落及表格位置；两段无法参数化的旧工程事实必须改写，条件做法必须逐项确认，带工程取值的表必须填写。其他模板仍使用原 T26 主题章候选规则，表格与条件句暂不自动装入。预览前填写本工程数值、核对整篇说明，导出的 DOCX 明示待核定。

更新水池版式时，在项目根目录用带 `python-docx` 的 Python 运行 `content-library/build_pool_layout.py`。程序会从同一 G 盘项目的 `说明文件/` 和 `content-library/private/catalog.json` 重建私有 JSON；不得把生成文件或真实工程原文提交到公开仓库。桌面端自动读取该文件，旧草稿保持原快照。

水池 DOCX 现在包含可编辑表格。当前 CAD 插件 V1 仍拒绝表格，所以这类文件用于 Word/WPS 内容审查；桌面端检查会明确给出 `W_TABLE_CAD_UNSUPPORTED`。CAD 表格兼容处理属于后续单独任务，不能将目前的 Word 检查说成 CAD 导入通过。

`CH01–CH25` 是资料主题分类；水池说明按原件 8 章编排，其余现有预设仍按有内容的 `CH` 主题章编排。原桌面端 `sec-*` 仍用于空白和自定义组合，旧草稿照旧恢复。钢结构与“其他”目前缺对应已整理的来源，只生成空框架并提示缺资料，不伪造正文。
