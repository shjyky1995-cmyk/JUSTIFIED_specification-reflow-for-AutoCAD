# 当前接续状态（所有 Agent 共用）

更新：2026-09-24。阶段3 T12 进行中；从已合入 T01～T11 的 main（2bb89ed）建立 `task/T12-acceptance`。mvp-1.0 是候选闭环标签，不是正式发布。T12 的三图幅业务、外机、打印、断网、取消时延和专业签收仍待真实证据。

## 已完成与证据

- 正式入口 `DN_NOTE_SET` 设一次标准/图幅/单位/DOCX，`DN_NOTE` 点一次位置；T11 已完成候选包、限额、报告、撤销与处理中取消宿主检查。证据见 T06/T10/T11 日志。
- T12 已取得 WPS 工程说明 A1 宿主证据：2 页、219 个 DBText、字形与续页核对；CAL-06 Word/WPS 输入语义完成，见 `docs/devlog/T12.md`。独立复核修正了旧日志的字符数：同 SHA 的原始 w:t 为 9080 个 UTF-16 单元，实体显示文本为 9076；仅 4 个自然换行边界可折叠空格，源映射保留。旧“8863 完全一致”不再作为证据。
- 本轮用项目真实 DocxAdapter 扫描用户 13 份原始说明：4 份无表格可解析，9 份含表格按 V1 拒绝。原件不改、不入库；本机报告在 `artifacts/t12-paper/20260924-074811-649/source-inventory.json`。
- 本机三图幅测试包与同一份无表格 WPS 样本已准备：`artifacts/t12-paper/20260924-074811-649`。样本 SHA256 `3D6402EC462A5649341718B3A5597C39BDF6F519D8D4FD92EF0D250EEFA10E98`；标准及 A1/A2/A3 模板均通过正式加载校验，外层清单标 local-test-only/releaseAccepted=false。`standards/published` 仍为空，不能把测试包当发布资产。步骤在 `docs/T12_ACCEPTANCE.md`。

## 用户待办与唯一恢复动作

用户方便时按 `docs/T12_ACCEPTANCE.md` 在 AutoCAD 2021 三张独立空白图分别执行正式 `DN_NOTE_SET` + `DN_NOTE`，检查至少两页、A1/A2 三栏及 A3 两栏，保留每种图幅一张截图。Agent 随后运行 `scripts/check-t12-paper-validation.ps1 -RunDirectory 'G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\t12-paper\20260924-074811-649'` 读取报告，登记通过或修复失败项。宿主/桌面默认由用户操作；仅用户明确不方便并要求代操作时才用 Computer Use。

## 仍待完成

- 本机三图幅真实宿主报告与图面复核；本地测试包不得提前放入 `standards/published`。
- 外机安装和字体身份、断网运行、一次真实打印、CAL-08 取消时延 P95 与真实业务性能、CAL-09 源文件/解压/页数/对象上限标定、结构专业签收与成效原始记录。缺证据保持 pending。
- 当前工作区原有 `tests/.../Fixtures/test-note-standard.json` 末行换行差异保留，不暂存。业务源文件、WPS 副本和截图为用户资料，已补忽略规则；仅提交脱敏工具与记录。

## 分支与交付

本轮 T12 工具及记录先在 `task/T12-acceptance` 检查、提交并推送私有远程；正式签收前不合 main、不打正式发布标签。下一阶段/恢复动作只有上述三图幅本机验证，完成后继续外机、断网、打印和专业签收。
