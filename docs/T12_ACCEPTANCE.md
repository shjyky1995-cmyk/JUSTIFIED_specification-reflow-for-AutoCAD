# T12 三图幅业务样本核对（本机）

当前只验证 A1/A2/A3 的正式两步命令与同一份业务说明。测试标准包在 `artifacts`，仅供本机验收；它的 JSON 为使正式命令可加载而标 `production`，外层 `manifest.json` 明确 `local-test-only`、`releaseAccepted=false`，不能放进 `standards/published` 或业务项目。

本轮目录：`G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\t12-paper\20260924-074811-649`。样本 `sample.docx`，SHA256 `3D6402EC462A5649341718B3A5597C39BDF6F519D8D4FD92EF0D250EEFA10E98`；已由项目 DOCX 解析器通过，160 块、无表格。三份本地模板均通过正式包校验。这些检查不替代 AutoCAD 字体、页面或打印实测。

## 用户在 AutoCAD 2021 操作

先保存现有工作，重新打开 AutoCAD。每种图幅都新建一张**独立空白模型空间图**，不要在业务图运行，也不要把三种图幅叠在同一图中。若尚未加载本版插件，用 `NETLOAD` 选择 `G:\JUSTIFIED_specification reflow for AutoCAD\测试文件\程序\Justified.SpecificationReflow.AutoCAD.PluginHost.dll`；其余同目录 DLL 保持原位。只按正常安全提示加载，不修改安全设置。

对 A1、A2、A3 各做一次：

1. 输入 `DN_NOTE_SET`。标准包根目录填 `G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\t12-paper\20260924-074811-649\local-test-package`；图幅填本次的 A1/A2/A3；模板序号填 `1`；单位比例明确填 `1`（此空白测试图按 1 图形单位 = 1 mm）。
2. 选择 `G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\t12-paper\20260924-074811-649\sample.docx`。报告目录填同一轮目录下的 `reports\A1`、`reports\A2` 或 `reports\A3`，须与本次图幅一致。
3. 输入 `DSS`，在空白图原点附近点一次位置。若出现警告，先选“否”并记下警告全文；若报错，记下错误码及前后对象数。
4. 成功后 `ZOOM` → `E` 查看全图：应有至少两页，页面向右排列；A1/A2 每页三栏，A3 每页两栏；无空白尾页、重叠、明显越栏或乱码。记录看到的页数，保存每种图幅一张截图供图面复核。测试图可不保存。

三种图幅结束后只需回复“完成”，附异常文字和截图；Agent 读取本机报告并运行 `scripts/check-t12-paper-validation.ps1 -RunDirectory <本轮目录>`。该检查核对输入 hash、规则/模板、图幅、页数、对象数、错误与警告，`T12_PAPER_REPORTS_OK` 仅表示运行报告通过。屏幕图面、外机、真实打印、取消时延、断网及专业复核独立签收，不能由脚本标通过。

## 证据边界与后续

既有 WPS A1 记录使用 `DN_NOTE_DEV` 与草案包，已证明真实字体及字符路径；本轮补正式 `DN_NOTE_SET` + `DSS` 三图幅。其余用户原始说明中部分含表格，V1 解析器按 `E_UNSUPPORTED_CONTENT` 阻断，不能直接把表格版拿来当“排版通过”样本，也不修改原件。用户不方便操作时按 AGENTS 规则另行告知 Agent 代操作。

## 剩余验收项与材料索引（2026-09-25 备齐）

| 项 | 材料 | 状态 |
| --- | --- | --- |
| 一次真实打印 | `docs/T12_PRINT.md`（打印步骤 + 逐页清单） | 待用户执行 |
| 断网运行 | `docs/T12_OFFLINE.md`（步骤 + 代码侧无网络引用测试已入核心测试） | 待用户执行 |
| 取消时延 P95（CAL-08） | `scripts/check-cancel-latency.ps1` + `docs/T14_SAMPLES.md` 第二节（报告新增 `Cancelled`/`CancelToFinishMilliseconds` 字段） | 待用户取样 |
| 真实性能 P95 | `scripts/summarize-performance.ps1`（≥20 次热运行，`DN_NOTE_TRACE=1` 可取命令行计时） | 待用户取样 |
| 专业签收 | `docs/T12_SIGNOFF.md`（签收单模板） | 待签收 |
| 不同 DOCX / 点位取消（T14） | `docs/T14_SAMPLES.md` + 样本包 `artifacts/t14-docx-samples/`（5 份本地预检通过） | 待用户执行 |

以上任一项未完成前，`productionReady=false` 与“未验收”状态不变。
