# JUSTIFIED_specification-reflow-for-AutoCAD

工程设计说明编制与 CAD 排版系统。2026-10-14起以 **main 为统一开发入口**：设计说明、平台同窗口投标和 CAD 表格源码已整合，人工验收与专业核定仍独立保留。CAD v0.1.0 既有正式发布状态不变。

当前本机统一客户端候选为 **EngiSpace 0.5.0-preview.1**，设计说明包含最新反馈修正和七套公开候选正文，投标在同一主窗口中使用。启动、安装和集中试用见[统一客户端指南](docs/UNIFIED_CLIENT_GUIDE.md)。此前[GitHub单位电脑测试包](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/office-test-2026-10-07)仍保留作为历史回退；Source code ZIP 不是可运行程序。

开发从[AGENTS](AGENTS.md)、[任务清单](docs/TASKS.md)、[当前接续](docs/NEXT_AGENT.md)、[架构](docs/ARCHITECTURE.md)和[开发指南](docs/DEVELOPMENT.md)接续。旧工作树只保留历史，旧分支恢复点见[归档清单](docs/BRANCH_ARCHIVE_20261014.json)。main 中的候选通过自动检查不代表正式发行，规则见[主线决策](docs/adr/ADR-020-unified-development-main.md)。

设计说明交付可编辑 DOCX，用户在 Word/WPS 调整后自行以 CAD DSS 导入；桌面端不连接 CAD。正文与模板已由用户授权公开，但规范有效性与工程适用性待核定；实际工程草稿、测试结果和密钥不公开。

CAD 当前承诺 AutoCAD 2021 / Windows x64 / .NET Framework 4.8；核心测试无需 CAD。真实 Word视觉/安装/CAD宿主/AI效果仍待集中验收，自动检查不替代人工签收。
