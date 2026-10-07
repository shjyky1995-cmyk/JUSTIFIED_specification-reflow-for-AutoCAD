# JUSTIFIED_specification-reflow-for-AutoCAD

工程设计说明编制与CAD排版系统。CAD v0.1.0 已发布；最新设计说明、投标同窗口模块及CAD表格为独立待验收候选。

**2026-10-08单位电脑测试：** [下载可运行测试包](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/office-test-2026-10-07)，操作步骤见[单位电脑测试指南](docs/OFFICE_TEST_GUIDE.md)。桌面成品自带运行环境，完整解压即可启动；Source code ZIP不是可运行程序。

源码保留不同工作线：设计说明task/T32-content-review、投标task/T40-bid-in-platform、CAD task/T28-cad-tables；本分支task/T41-office-handoff用于跨电脑交接。未验收祖先不直接合入main。说明正文和模板已由用户授权公开，专业适用性/规范仍待核定；实际工程草稿、测试结果和密钥不公开。

开发从[AGENTS](AGENTS.md)、[任务清单](docs/TASKS.md)、[当前接续](docs/NEXT_AGENT.md)、[架构](docs/ARCHITECTURE.md)和[开发指南](docs/DEVELOPMENT.md)接续。设计说明最终交付可编辑DOCX，用户在Word/WPS调整后自行以CAD DSS导入；桌面端不连接CAD。

CAD当前承诺AutoCAD2021 / Windows x64 / .NET Framework4.8；核心测试无需CAD。真实单位电脑/Word视觉/CAD宿主验证仍需集中测试，自动检查不能替代人工签收。
