# 统一客户端候选（2026-10-14）

本机入口：项目根目录 `测试文件/统一客户端试用/EngiSpace-0.5.0-preview.1/client/EngiSpace.exe`；安装入口为同级上层 `DesktopSetup.exe`。完整压缩包为 `测试文件/统一客户端试用/EngiSpace-0.5.0-preview.1.zip`。候选尚未上传 GitHub 成品发布页，源码 main 与远端备份结果见 T42 日志。

同一首页包含最新设计说明和同主窗口投标。自带 Electron44、投标后台Electron41、.NET工作进程/Word工具及七套候选正文，无需 SDK、Node 或 CAD。该整合不改变两个客户端数据格式；新包不附带旧工程/密钥，也不会自动迁移旧草稿。

## 集中试用

1. 关闭旧程序，完整解压ZIP到可写短目录（如 `G:\ES\Unified`），再启动 `client/EngiSpace.exe`。不要只复制EXE或在ZIP中直接运行。
2. 设计说明：新建、少填参数、选水池或框架模板、编辑正文/活荷载表、保存关闭、重新打开、导出Word草稿，点“打开这份DOCX”。应保留最后修改，Word/WPS可编辑；非结构专业不显示结构参数。缺项允许草稿导出，已核对说明仍须补齐。
3. 从首页进入勘察设计投标，导入脱敏TXT/文字PDF、修改保存目录、导出Word、返回首页并重新进入。应在同一个主窗口中切换，资料保留。真实AI配置与发送另按程序逐次确认。
4. 安装、升级、回退/卸载和Word分页仍按 DESKTOP_CLIENT_ACCEPTANCE、DESKTOP_CLIENT_GUIDE 观察。异常反馈版本、模板、具体步骤及完整提示；保留原件和旧包。

## 资料与回退

若需要旧草稿，先从旧设计说明客户端“设置与备份”导出，再在新版恢复为新增副本，不直接覆盖运行中的数据目录。投标SQLite迁移按 BIDDING_CLIENT_GUIDE：退出前后台、完整备份旧portable-data后再迁移，不复制正在使用的数据库。Agent没有自动迁移业务资料。

七套正文与模板为公开项目候选内容，不代表规范现行有效或适用于具体工程。钢结构/其他可靠主体正文、通用给排水、抗震地点数据仍待补。CAD表格按 CAD_TABLES_ACCEPTANCE 独立验收；本客户端导出到DOCX即止，不自动启动CAD。

旧设计说明preview.3、投标preview.3、CAD表格preview.2及10月7日GitHub测试包均原位保留。新包仍candidate/productionReady=false，不能当作正式签收版本。
