# 勘察设计投标工作线入口

2026-10-04，T34/P2候选已实现，待人工验收。用户批准T33计划；开发在独立分支 task/T34-bid-workbench / artifacts/worktrees/t34-bid-workbench 完成。基于T32 859ca6a，代码bb87696、包基准6db0808、检查接续a4e3205；尚未合main/推远程。本根目录T28保持原开发边界，本文件只同步工作线位置，不表示投标源码已集成根分支。

- 试用：`测试文件/勘察设计投标试用/EngiSpace-0.3.0-bid-preview.1/client/EngiSpace.exe`；数据/原件/缓存位于同包 `portable-data`，全在G盘。
- [操作与验收](BIDDING_CLIENT_GUIDE.md)、[目录索引](PROJECT_DIRECTORY_INDEX.md)。试用新建→资料→要求响应→章节→导出，并核对关窗重开及备份恢复；异常提供步骤和脱敏截图。
- 功能：勘察/设计/联合三类型，原件/补遗、人工要求和资信、章节表格、保存恢复、核对、可编辑DOCX及含附件备份；UI沿用原设计语言。未复制易标代码，未引入AI依赖。
- 检查：三类型真实UI流程/导出、保存失败/并发输入/立即关窗保存、坏稿回退/哈希备份、旧六专业编制/导出/滚动、304文件及ZIP310项校验通过；证据在artifacts/runs/t34。真实Word/WPS视觉与工程专业核定仍pending。
- 下一步：用户集中试用，Agent修复反馈；P2验收后确认P3模型和解析方案。T32/T28各自验收状态不变，原设计说明候选入口保留。
- 整理：旧解压副本903文件与ZIP哈希一致后归档local/archive/desktop-extracted-T34；删除被自动审批拒绝，未说明具体原因，实际释放0字节。原件、工作树、SDK与当前候选保留。

详细批准计划、架构、日志与接续文件在 `artifacts/worktrees/t34-bid-workbench/docs`；读取NEXT_AGENT.md继续本工作线。目录与设计统一长期要求同步到根AGENTS.md。
