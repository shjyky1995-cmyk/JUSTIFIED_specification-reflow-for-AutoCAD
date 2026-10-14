# 项目目录使用索引（2026-10-14）

所有项目源码、参考、缓存和产物均位于 `G:\JUSTIFIED_specification reflow for AutoCAD`。根main为统一开发入口；旧工作树保留原提交及未提交夹具，不再作为新任务基线。

| 用途 | 位置 | 使用规则 |
| --- | --- | --- |
| 统一开发 | 项目根main；src、desktop、content-library/shared | 新任务从main开task分支，串行开发；人工状态独立 |
| 当前统一试用 | 测试文件/统一客户端试用/EngiSpace-0.5.0-preview.1 | client/EngiSpace.exe，设计说明＋同窗口投标；DesktopSetup.exe安装；candidate |
| 整合/检查环境 | artifacts/worktrees/t42-main-consolidation | 保留源码和构建环境用于证据复现，不接续旧NEXT_AGENT |
| 本轮证据 | artifacts/runs/t42 | 包核验、全新解压、DOCX、内嵌报告、Git bundle；不公开提交 |
| 旧设计说明/投标工作树 | artifacts/t26-auto-content-worktree；artifacts/worktrees/t34-bid-workbench | 原位历史/依赖回退，不删除数据 |
| 其他旧工作树 | artifacts/desktop-worktree、release-worktree、t18-worktree、t20-worktree、t23-worktree、t33-bid-plan-worktree | 原位历史；标签恢复旧任务 |
| 旧设计说明/投标包 | 测试文件/设计说明客户端试用；测试文件/勘察设计投标试用 | 保留原包、草稿/SQLite和回退用途；不自动迁移 |
| CAD表格候选 | 测试文件/CAD表格试用 | 原preview.2供宿主试用；集成窗口宿主仍待验收 |
| 业务原稿/旧私有内容 | 说明文件、content-library/private、测试文件内业务资料 | 原位保护，不公开提交 |
| 历史验收证据 | 测试结果、artifacts既有任务目录 | 不能当普通缓存批量删除 |
| 外部源码/资料 | local/references/OpenBidKit_Yibiao；local/research | 外部参考原位保留，非主线代码 |
| 缓存/构建环境 | artifacts/nuget-packages；artifacts/dotnet10；既有工作树node_modules/vendor | 复用G盘资源，不在C盘建项目副本 |
| 旧归档/整理清单 | local/archive；local/organization | 保留既有哈希和动作报告，未执行本轮容量清理 |
| 分支恢复清单 | docs/BRANCH_ARCHIVE_20261014.json | archive/2026-10-14/*远端标签；归档不是发行 |

本轮只收束Git分支引用；不删除任何工作树、包、业务资料或验收证据，不宣称释放磁盘空间。业务数据迁移必须退出程序并完整备份，见UNIFIED_CLIENT_GUIDE与BIDDING_CLIENT_GUIDE。
