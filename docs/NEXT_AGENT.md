# 当前接续状态（所有 Agent 共用）

更新：2026-09-22 15:08。阶段2 图面重试被新版 DLL 加载安全提示阻塞。不要开始阶段3，不要合入 main。

## 现在做哪一步

用户已明确授权 Agent 代做 CAD 测试，通过后继续开发，不再要求用户自行跑命令或抄日志。旧实例保留；Agent 已启动独立 AutoCAD 2021 实例并新建空白 Drawing1，执行 NETLOAD 加载 `artifacts/dn-note-2`，现停在“安全性 - 未签名的可执行文件”对话框。用户只需亲自点“加载一次”，后续测试由 Agent 接续。computer-use 规则禁止 Agent 操作安全许可提示，不得修改 SECURELOAD、信任路径或搬移 DLL 绕过提示。

恢复时重新枚举窗口，识别有安全弹窗的新实例（本次主窗口 ID 920964；旧实例 330580，仅供识别，不能直接复用旧句柄）。核实提示已由用户处理后，运行同一样本的 DN_NOTE、检查真实图面和 DBText 类型、重复生成以及一次 U 撤销，再依据结果修复或进入下一阶段。未执行的测试仍为 pending。

本对话如果还开着，就在本对话收这次结果。新开对话时，只读本文件、`docs/TASKS.md`、`docs/devlog/T10.md` 末尾，不要重做 T07–T10，也不要根据聊天记忆改坐标公式。

## 当前状态

- 分支 `task/T09-dbtext`，工作区应干净。最新提交 `a46c5b2` `fix(T10): 左对齐文字不再设置对齐点`。其上是 `0c41799`、`e11101c`。
- T09、T10 待验收。T06、T07、T08 仍待验收。整条分支继承未验收的 T06，禁止合入 main，禁止打阶段标签，禁止推送。
- 可加载目录只认 `artifacts/dn-note-2/Justified.SpecificationReflow.AutoCAD.PluginHost.dll`。`artifacts/dn-note` 是会写入失败的旧构建。`src/.../PluginHost/bin/Release/net48` 曾被正在运行的 AutoCAD 锁住，里面也可能是旧文件。
- 2026-09-22 15:08 已补跑 Core：net8.0 与 net48 各 117/117，构建 0 警告、0 错误。Core 不覆盖 AutoCadAdapter 修复，不能替代宿主图面验证。

## 已经证实的第一次运行

空白图，单位比例 1，图幅 A1，样本 `测试文件/示例-单行说明.docx`，标准目录 `standards/drafts`。UCS 与 WCS 相同：`28.706599762571557,12.977053983885803,0`。

- `DN_NOTE_SUMMARY pages=1 objects=6 warnings=0`
- `DN_NOTE_PAGE_STEP x=861 y=0 scale=1`
- `DN_NOTE_FIRST x=-681.29340023742839 y=6.7770539838858026 h=4.5 widthFactor=0.75 text=设计说明`
- 这与锚点加 A1 首栏左 `-710`、首基线 `-6.2` 一致。
- `DN_NOTE_DRAFT` 四行是预期提示，不是失败。草案文件没有被修改。
- 随后 `E_RENDER_FAILED Exception`，`before=0 after=0`。原因是左对齐 DBText 设置了 AlignmentPoint 并调用 AdjustAlignment，AutoCAD 2021 抛错。测量命令能成功，是因为它只用 Position。`a46c5b2` 已去掉这两步，样式改为写入 `tssdeng.shx` / `tssdchn.shx` 文件名，并把 AutoCAD ErrorStatus 打进失败信息。

## 用户待办（本次恢复）

仅在新 AutoCAD 实例现有的插件安全提示中亲自点击“加载一次”，然后告知已点。不需要关闭旧窗口，不需要自行测试，不需要改安全设置。若后续依赖出现同类安全提示，也须用户亲自处理。以下原测试步骤改由 Agent 执行，旧的退出重开要求不再适用于当前已准备好的新实例。

## 原重试步骤（由 Agent 接续执行）

完全退出 AutoCAD 后重新打开一张空白图。不要打开 `新块.dwg`，不要改 SECURELOAD。

1. `NETLOAD`：`G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\dn-note-2\Justified.SpecificationReflow.AutoCAD.PluginHost.dll`
2. `DN_NOTE`：选 `测试文件/示例-单行说明.docx`；标准目录填上面的 drafts；图幅 `A1`；单位比例 `1`；在图上点一下。
3. 成功标准：`DN_NOTE_OK objects=6`，并且 after 比 before 多 6。特性里是单行文字。再跑一次不删除旧字；一次 `U` 只撤销最新一次。
4. 把从 `DN_NOTE` 到结束的命令行全文发回。若仍失败，新的 `DN_NOTE_FAILED` 里会有具体错误码，按那个修，不要重写排版引擎。

不要用 `示例-结构说明.docx`。它含上下标，命令会整篇拒绝且不写图。上下标在标定前继续阻断，不能改成普通数字。

## 通过之后

把真实命令行、图面观察、实体类型和一次撤销证据记入 `docs/devlog/T10.md`。用户本轮已授权由 Agent 完成这些检查并依据结果继续，成功后不必再要求用户重复手工验收这一轮测试。完整验收未覆盖项仍保留待验收，阶段3继续 T06 剩余真实字形、上下标和打印，以及 T11、T12；不能把本次简单样本通过等同于正式发布通过。

## 正式操作要求（已改项目基线）

2026-09-22 用户要求修改项目要求，已写入 PRD 3.3、AP-004、AGENTS、WORKFLOW 和 PAPER_TEMPLATES。设计人员的正式操作是：工程或当前图设一次标准、图幅和单位比例，之后一个命令点一次说明区右上角即生成。无警告不再追问。未知单位不猜，不按文件名换标准，不删除旧文字。

当前 `DN_NOTE` 的多步提问只是开发核对，不能再被当成正式流程。把它收成一键生成是后续实现，不在本次写入修复里顺手改命令。阶段3之前的当下动作仍是重载 `artifacts/dn-note-2` 再跑一次现有命令。

## 不要重做

- 不改 `standards/drafts` 里的 pending、null 公差和 uncalibrated。
- 不把 4.5、0.75、7.2、三栏两栏写死进引擎。
- 不实现 MText，不追踪旧图增量。
- PDF 未生成。页数上限 10000 只是失控保护。正式发布验证未做。
