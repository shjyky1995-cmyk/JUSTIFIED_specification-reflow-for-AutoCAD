# 勘察设计投标工作线入口

## 2026-10-05 当前为T36本地文字型PDF候选

当前分支`task/T36-bid-pdf`，工作树仍为`artifacts/worktrees/t34-bid-workbench`。实现及包基准dfda806，检查接续954f739；未合main/推远端，独立候选待验收。

当前入口：`测试文件/勘察设计投标试用/EngiSpace-0.3.0-bid-preview.3/client/EngiSpace.exe`。本地PDF逐物理页提取，AI可只选指定页；扫描/加密/超限资料明确提示，TXT完整提取，资料版本变动使关联核对失效。原正文及原件保留。旧版6个数据文件已复制校验，两份投标及附件可加载；旧版继续保留，ZIP不含业务数据和密钥。

PDF/TXT11组、原投标10组、AI17组、六专业worker/flow及隐藏UI通过；新ZIP316文件/322项CRC与SHA256通过。包内worker实际PDF/DOCX及隔离入口加载包内main/preload/界面的关窗保存/PDF检查通过；直接成品exe传测试脚本未执行，不记为检查通过。证据在`artifacts/runs/t36`。

用户方便时按[BIDDING_CLIENT_GUIDE](BIDDING_CLIENT_GUIDE.md)或根试用目录`PDF试用步骤.md`集中核对首末页、物理页序、否定条件、表格文字与选页AI预览。真实工程PDF/Word/DeepSeek及专业核定仍待验收，OCR与P3/P4其余深化未完成。唯一下一动作是接收本轮脱敏试用反馈并先修复，再按真实扫描样本讨论OCR方案；不重复关机。

以下为T35与T34历史，当前入口以上述preview.3为准。

## 2026-10-05 当前为T35 DeepSeek辅助候选

当前分支task/T35-bid-assistance，工作树仍为artifacts/worktrees/t34-bid-workbench。实现90a535d、接续0823891，未合main/推远程。

新版入口：`测试文件/勘察设计投标试用/EngiSpace-0.3.0-bid-preview.2/client/EngiSpace.exe`。本地候选及DeepSeek要求/目录/章节建议已实现；逐次预览确认、安全加密密钥、原文核对、任务恢复和人工采纳，不覆盖原正文。统一既有UI风格。旧版6个数据文件已复制校验，旧原件保留；ZIP不含业务数据。

AI模拟17组、实际Windows密钥加密及隐藏UI、原三类型与六专业回归、新ZIP全新解压306文件、包内真实关窗保存和DOCX检查通过。真实DeepSeek权限/计费/工程效果、Word视觉仍待验收；PDF/OCR及其他P3深化未完成。

下一步用户按[BIDDING_CLIENT_GUIDE](BIDDING_CLIENT_GUIDE.md)在程序内填写密钥并用脱敏短资料集中试用，Agent接收反馈修复。1004文件仅登记用户导出样本，不推定完整验收。用户要求本轮完成后10分钟关机，执行证据在artifacts/runs/t35/shutdown.json；以后不重复安排。

以下保留T34历史交付记录；当前入口以上方preview.2为准。


2026-10-04，T34/P2候选已实现，待人工验收。用户批准T33计划；开发在独立分支 task/T34-bid-workbench / artifacts/worktrees/t34-bid-workbench 完成。基于T32 859ca6a，代码bb87696、包基准6db0808、检查接续a4e3205；尚未合main/推远程。本根目录T28保持原开发边界，本文件只同步工作线位置，不表示投标源码已集成根分支。

- 试用：`测试文件/勘察设计投标试用/EngiSpace-0.3.0-bid-preview.1/client/EngiSpace.exe`；数据/原件/缓存位于同包 `portable-data`，全在G盘。
- [操作与验收](BIDDING_CLIENT_GUIDE.md)、[目录索引](PROJECT_DIRECTORY_INDEX.md)。试用新建→资料→要求响应→章节→导出，并核对关窗重开及备份恢复；异常提供步骤和脱敏截图。
- 功能：勘察/设计/联合三类型，原件/补遗、人工要求和资信、章节表格、保存恢复、核对、可编辑DOCX及含附件备份；UI沿用原设计语言。未复制易标代码，未引入AI依赖。
- 检查：三类型真实UI流程/导出、保存失败/并发输入/立即关窗保存、坏稿回退/哈希备份、旧六专业编制/导出/滚动、304文件及ZIP310项校验通过；证据在artifacts/runs/t34。真实Word/WPS视觉与工程专业核定仍pending。
- 下一步：用户集中试用，Agent修复反馈；P2验收后确认P3模型和解析方案。T32/T28各自验收状态不变，原设计说明候选入口保留。
- 整理：旧解压副本903文件与ZIP哈希一致后归档local/archive/desktop-extracted-T34；删除被自动审批拒绝，未说明具体原因，实际释放0字节。原件、工作树、SDK与当前候选保留。

详细批准计划、架构、日志与接续文件在 `artifacts/worktrees/t34-bid-workbench/docs`；读取NEXT_AGENT.md继续本工作线。目录与设计统一长期要求同步到根AGENTS.md。
