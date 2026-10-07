# 设计说明内容库

2026-10-07 用户确认：说明正文、模板及后续补充均为项目组成部分，允许在公开仓库与测试包中发布。实际工程草稿、密钥和测试结果继续排除。公开授权不代表专业适用性或规范有效性已经核定。

## 目录与维护

| 路径 | 作用 |
| --- | --- |
| shared/catalog.json | 七模板候选内容包，公开源码与成品共用 |
| shared/source-layouts.json | 原稿章节、正文、表格、参数与来源版式 |
| sources/ | 用户授权公开的设计说明原DOCX，试验派生稿不在其中 |
| build_catalog.py / build_pool_layout.py | 已有转换工具，旧 private 输入/输出继续兼容；没有自动授权其中任意报告或草稿 |
| ASSEMBLY_RULES.md / catalog_schema.md | 装配与审核规则、内容包格式 |
| private/ | 历史本机输出和检查；不整体提交，仅已明确授权的正文整理到 shared |

桌面源码启动优先发现同项目 content-library/shared/catalog.json，再兼容旧 private；成品优先读取包内 resources/content-library。两个JSON必须一起更新，来源ID/哈希需匹配。显式设置的资料库优先，旧草稿保留正文快照，不被新正文覆盖。

当前七套按原顺序包含1355段、29表，参数缺失仍可导出有待填写标记的Word草稿。钢结构/其他主体来源与通用给排水内容仍待补齐，不能用工艺总图候选冒充已核定通用标准。规范库索引不能替代规范有效性审核。

单位电脑下载与集中验收见 docs/OFFICE_TEST_GUIDE.md；设计说明交付到可编辑DOCX，CAD DSS由用户另行选择导入。CAD表格候选仍需真实宿主/一次撤销/打印验收。
