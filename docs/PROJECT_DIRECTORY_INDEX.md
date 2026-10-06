# 项目目录使用索引

根目录为 G:\JUSTIFIED_specification reflow for AutoCAD；以下路径相对根目录。2026-10-06 T40平台内嵌候选交付后更新。

| 用途 | 位置 | 使用规则 |
| --- | --- | --- |
| CAD 活动开发 | 根目录，task/T28-cad-tables | 保留原修改，独立验收 |
| 设计说明活动开发 | artifacts/t26-auto-content-worktree | T32候选，保持原流程 |
| 投标活动开发 | artifacts/worktrees/t34-bid-workbench | 当前T40 / task/T40-bid-in-platform，44平台页面＋41后台原引擎 |
| 投标批准计划 | artifacts/t33-bid-plan-worktree | 只保留规划历史，不作为新开发基线 |
| 当前设计说明试用 | 测试文件/设计说明客户端试用/EngiSpace-0.2.0-preview.2-内容核对版/client/EngiSpace.exe | 当前入口保留 |
| 投标便携试用 | 测试文件/勘察设计投标试用/EngiSpace-0.4.0-bid-preview.3/client/EngiSpace.exe | T40同主窗口；数据在同包portable-data；基准5da55ab |
| 投标回退 | 测试文件/勘察设计投标试用/EngiSpace-0.3.0-bid-preview.4（另保留preview.3/preview.2/preview.1） | 另保留0.4.0-bid-preview.1/preview.2及原SQLite数据 |
| 投标检查证据 | artifacts/runs/t34、artifacts/runs/t35、artifacts/runs/t36、artifacts/runs/t37、artifacts/runs/t38、artifacts/runs/t39、artifacts/runs/t40 | 模型/UI/关窗/包完整性报告；不入公开库 |
| 设计说明回退 | 测试文件/设计说明客户端试用/EngiSpace-0.2.0-preview.1-最终试用 | 保留可运行副本 |
| CAD表格候选/样本 | 测试文件/CAD表格试用 | 保留原入口和验收证据 |
| 早期桌面解压副本 | local/archive/desktop-extracted-T34 | 已校验归档，不是当前试用入口；ZIP在原试用目录 |
| 外部易标源码 | local/references/OpenBidKit_Yibiao | 只读来源f185a25；移植副本desktop/bidding-framework随候选打包 |
| 业务原稿与私有内容 | 说明文件、content-library/private、测试文件内业务原稿 | 原位保护，不公开提交 |
| 历史验收证据 | 测试结果、artifacts各任务检查目录 | 原位登记，不能当普通缓存删 |
| PDF依赖缓存 | artifacts/nuget-packages | T36新包均存G盘；不是业务原件 |
| 构建环境 | artifacts/dotnet10、既有worktree依赖 | 保留，按锁定版本使用 |
| 整理清单 | local/organization | 本机哈希与动作报告，不入库 |

恢复早期程序：从归档目录移回原同名目录，或解压保留的同名 ZIP。未释放磁盘空间：系统拒绝删除，归档只改善分类，不声称已清理容量。后续新增资料、工作树和产物遵守 AGENTS 与目录方案。
