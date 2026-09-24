# 当前接续状态（所有 Agent 共用）

更新：2026-09-24。阶段3 T12 进行中；从已合入 T01～T11 的 main（2bb89ed）建立 `task/T12-acceptance`。mvp-1.0 是候选闭环标签，不是正式发布。T12 的三图幅业务、外机、打印、断网、取消时延和专业签收仍待真实证据。

2026-09-24 新需求及分支：用户明确同一 DWG 可先后插入不同图幅/版本/DOCX，要求每次在 CAD 小窗口选择。`task/T14-note-picker` 从当前 `task/T12-acceptance` 派生，不能绕过未验收祖先直接合 main。T14 的 WinForms 入口已接入 `DN_NOTE`；`DN_NOTE_REPEAT` 重用最后一次选择；`DN_NOTE_SET` 仍供旧图/验收。当前仅完成代码构建与双框架核心测试，实际窗口/混合图面 pending；原 T12 私有转移 ZIP 仍是旧入口，不含 T14。

2026-09-24 新用户要求：推广时普通设计人员须从单一发布包经图形化入口安装，重启 AutoCAD 后使用；不得要求 GitHub、PowerShell、手工复制 `.bundle` 或人工校验哈希。PRD 3.3/NFR-006 已登记，T13 跟踪安装、升级、卸载与全新电脑实测。当前 T12 私有 ZIP 和手工步骤仅供开发验收，不能包装为正式用户体验；正式发布还须完成 T13，且不能把 T12 的 local-test-only 标准装进生产包。

## 已完成与证据

- 正式入口 `DN_NOTE_SET` 设一次标准/图幅/单位/DOCX，`DN_NOTE` 点一次位置；T11 已完成候选包、限额、报告、撤销与处理中取消宿主检查。证据见 T06/T10/T11 日志。
- T12 已取得 WPS 工程说明 A1 宿主证据：2 页、219 个 DBText、字形与续页核对；CAL-06 Word/WPS 输入语义完成，见 `docs/devlog/T12.md`。独立复核修正了旧日志的字符数：同 SHA 的原始 w:t 为 9080 个 UTF-16 单元，实体显示文本为 9076；仅 4 个自然换行边界可折叠空格，源映射保留。旧“8863 完全一致”不再作为证据。
- 本轮用项目真实 DocxAdapter 扫描用户 13 份原始说明：4 份无表格可解析，9 份含表格按 V1 拒绝。原件不改、不入库；本机报告在 `artifacts/t12-paper/20260924-074811-649/source-inventory.json`。
- 本机三图幅测试包与同一份无表格 WPS 样本已准备：`artifacts/t12-paper/20260924-074811-649`。样本 SHA256 `3D6402EC462A5649341718B3A5597C39BDF6F519D8D4FD92EF0D250EEFA10E98`；标准及 A1/A2/A3 模板均通过正式加载校验，外层清单标 local-test-only/releaseAccepted=false。`standards/published` 仍为空，不能把测试包当发布资产。步骤在 `docs/T12_ACCEPTANCE.md`。
- 外机/断网材料已装成私有本机转移包 `artifacts/t12-transfer/20260924-080328-947.zip`，ZIP SHA256 `E7848DED77D078BD625D8BC9478452C3DF6644726F88A903A09A944583CB1209`；解压后清单校验 9 文件通过，候选插件内部 22 文件校验通过。包内标准目录另有 LOCAL-TEST-ONLY 标识。含用户业务样本，仅在其授权设备内使用，外机运行本身仍 pending。

## 用户待办与唯一恢复动作

当前先验 T14 新窗口：用户方便时在 AutoCAD 2021 独立空白图，使用 T14 候选插件与本地测试标准包，执行 `DN_NOTE` 选择 A1/样本 DOCX 点位一次；再次 `DN_NOTE` 改选 A2 或 A3/另一 DOCX，再点另一处；检查两组文字均在、图幅不同、取消窗口不落图。留窗口与图面截图、命令输出。完成后再按 `docs/T12_ACCEPTANCE.md` 用原 T12 私有转移 ZIP 验收三张独立空白图的原有正式链路，不能混用两份候选插件。Agent 随后运行 `scripts/check-t12-paper-validation.ps1 -RunDirectory 'G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\t12-paper\20260924-074811-649'` 读取报告，登记通过或修复失败项。宿主/桌面默认由用户操作；仅用户明确不方便并要求代操作时才用 Computer Use。

## 仍待完成

- 本机三图幅真实宿主报告与图面复核；本地测试包不得提前放入 `standards/published`。
- 外机安装和字体身份、断网运行、一次真实打印、CAL-08 取消时延 P95 与真实业务性能、CAL-09 源文件/解压/页数/对象上限标定、结构专业签收与成效原始记录。缺证据保持 pending。
- T13 普通用户图形化安装包及全新电脑安装/升级/卸载验收尚未开始，不能宣称已有“一键安装”。
- T14 实际 AutoCAD 窗口、取消、连续 A1/A2/A3 不同 DOCX 的同图效果未做宿主核验；当前 `standards/published` 仍为空，不能把本地测试包视为发布资产。
- 当前工作区原有 `tests/.../Fixtures/test-note-standard.json` 末行换行差异保留，不暂存。业务源文件、WPS 副本和截图为用户资料，已补忽略规则；仅提交脱敏工具与记录。

## 分支与交付

T14 最新独立候选包：`artifacts/packages/20260924-083931-125/JUSTIFIED_specification-reflow-for-AutoCAD-0.1.0-candidate-7fb5884.zip`，SHA256 `FDA77CE7B7ABB8DEA31A09DB950B8F69B75663FD717BC85DB9A9BDF85612D41A`；0 警告、双框架各 143/143、bundle 22 文件校验通过。原 T12 私有转移 ZIP 不含新界面。

当前唯一恢复动作：由用户方便时在 AutoCAD 2021 空白图用 T14 候选包执行 `DN_NOTE`，验证窗口、取消与连续两次不同选择的落图；报告结果后修复问题，再继续 T12 三图幅/外机等真实验收。T12/T14 完成后进入 T13 安装体验交付，正式发布不能跳过。宿主/桌面默认由用户操作；仅用户明确不方便并要求代操作时使用 Computer Use。
