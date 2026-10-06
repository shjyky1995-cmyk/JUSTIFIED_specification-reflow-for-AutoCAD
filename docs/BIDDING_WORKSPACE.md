# 勘察设计投标工作线入口

## 2026-10-06 T38 原框架移植版：待验收

- 用户要求直接移植易标框架，已确认独立Electron41运行及统计/诊断留本机、关闭自动上传，替代T33只参考自行实现的限制；见[ADR-018](adr/ADR-018-bidding-framework-import.md)。
- 工作树artifacts/worktrees/t34-bid-workbench，分支task/T38-bid-framework-import；选文件修复942029a、源码导入f9448c5、组合实现/源码基准299128c。固定上游f185a25，mark / yibiaoai署名及AGPL-3.0-only保留。
- 入口：测试文件/勘察设计投标试用/EngiSpace-0.4.0-bid-preview.1/client/EngiSpace.exe → 勘察设计投标 → 选择招标文件。新模块数据portable-data/data/bidding-framework，旧JSON草稿保留独立入口，preview.4程序与数据原位保留，不自动转换SQLite。
- artifacts/runs/t38证据：选择文件/取消/TXT导入/SQLite回读；成品资源主入口渲染无错误/关闭0；本地统计/凭据不落统计、utilityProcess诊断/密钥脱敏；原导出器与自带OpenXML助手正文/表格/实际技术方案DOCX读回；成品子EXE首页打开/退出返回5项；旧关窗保存。旧投标10项、AI17项、来源16项、包内PDF11项和六专业回归通过，未用真实AI密钥。
- 新包采用原版ASAR包装，最终文件数与实现基准见T38日志/BUILD.json，含许可/对应源码，不含业务资料/密钥/私有说明库；ZIP全新解压与CRC/SHA256最终证据见T38日志。builtFromDirtyTree=true如实包括当时测试脚本和既有夹具差异，最终生产源码基准见BUILD.json，未提交旧夹具。
- 用户按[BIDDING_CLIENT_GUIDE](BIDDING_CLIENT_GUIDE.md)集中核验文件选择、退出重开、脱敏AI和Word可编辑/版式。原框架内容先保留；真实API/费用/Word视觉、专业规则及锁定依赖52项风险（3严重）仍pending。仅本机候选，未合main、未推送、未正式发布。
- 唯一下一步：先修移植版试用反馈，再用脱敏勘察设计标书完善目录、材料组织和要求响应。另一Agent负责设计说明，CAD与设计说明原待验收状态不改。

下方T37及更早记录为历史；当前投标入口以上方T38为准。

## 2026-10-06 当前为T37候选来源核对版

投标当前分支`task/T37-bid-candidate-review`，工作树仍为`artifacts/worktrees/t34-bid-workbench`；实现/包基准bc6228a，preview.4待验收，未合main或推远端。用户安排本聊天负责投标，另一Agent在独立worktree负责设计说明。

当前入口：`测试文件/勘察设计投标试用/EngiSpace-0.3.0-bid-preview.4/client/EngiSpace.exe`。新增AI候选来源文件、生成时版本、引用原文定位；资料及关联补遗变化后旧候选禁止加入；旧版任务缺版本快照保留查看，重新生成后采纳。取消发送预览释放记录，回复读取阶段取消/超时可恢复。PDF及原流程保留，OCR未实施。

新核对16组、原AI17组、PDF11组、投标10组与六专业回归通过；316文件/322项ZIP CRC和SHA256通过；包内worker、候选界面4项及真实main关窗保存通过。旧版6个数据文件复制校验、两份投标及附件加载通过，旧原件保留，业务数据及密钥不进ZIP。证据`artifacts/runs/t37`。

方便时按[BIDDING_CLIENT_GUIDE](BIDDING_CLIENT_GUIDE.md)核对引用定位、版本变化和重新生成采纳。真实工程PDF、Word/WPS、DeepSeek权限/计费/效果与专业核定仍待验收。唯一下一动作：先修试用反馈；无反馈继续已批准的P4补遗/资信细化，OCR方案另行讨论。设计说明/CAD各自待验收状态由对应工作线维护，不重复关机。

以下为T36及更早历史，当前入口以上方preview.4为准。

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
