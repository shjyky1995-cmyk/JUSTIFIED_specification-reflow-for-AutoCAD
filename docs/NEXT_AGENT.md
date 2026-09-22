# 当前接续状态（所有 Agent 共用）

更新：2026-09-22。阶段2 还差一次图面重试。不要开始阶段3，不要合入 main。

## 现在做哪一步

用户已在空白图跑过一次 `DN_NOTE`。排版和坐标正确，写入 DBText 失败并已回滚。修复在 `a46c5b2`，新程序在 `artifacts/dn-note-2`。下一动作只是让用户退出 AutoCAD、加载这个新目录、用同一份样本再跑一次，并把命令行全文发回。

本对话如果还开着，就在本对话收这次结果。新开对话时，只读本文件、`docs/TASKS.md`、`docs/devlog/T10.md` 末尾，不要重做 T07–T10，也不要根据聊天记忆改坐标公式。

## 当前状态

- 分支 `task/T09-dbtext`，工作区应干净。最新提交 `a46c5b2` `fix(T10): 左对齐文字不再设置对齐点`。其上是 `0c41799`、`e11101c`。
- T09、T10 待验收。T06、T07、T08 仍待验收。整条分支继承未验收的 T06，禁止合入 main，禁止打阶段标签，禁止推送。
- 可加载目录只认 `artifacts/dn-note-2/Justified.SpecificationReflow.AutoCAD.PluginHost.dll`。`artifacts/dn-note` 是会写入失败的旧构建。`src/.../PluginHost/bin/Release/net48` 曾被正在运行的 AutoCAD 锁住，里面也可能是旧文件。
- 核心测试在修复前为 net8.0 与 net48 各 117/117。这次修复只改了写入方式，没有再跑测试。宿主图面仍未通过。

## 已经证实的第一次运行

空白图，单位比例 1，图幅 A1，样本 `测试文件/示例-单行说明.docx`，标准目录 `standards/drafts`。UCS 与 WCS 相同：`28.706599762571557,12.977053983885803,0`。

- `DN_NOTE_SUMMARY pages=1 objects=6 warnings=0`
- `DN_NOTE_PAGE_STEP x=861 y=0 scale=1`
- `DN_NOTE_FIRST x=-681.29340023742839 y=6.7770539838858026 h=4.5 widthFactor=0.75 text=设计说明`
- 这与锚点加 A1 首栏左 `-710`、首基线 `-6.2` 一致。
- `DN_NOTE_DRAFT` 四行是预期提示，不是失败。草案文件没有被修改。
- 随后 `E_RENDER_FAILED Exception`，`before=0 after=0`。原因是左对齐 DBText 设置了 AlignmentPoint 并调用 AdjustAlignment，AutoCAD 2021 抛错。测量命令能成功，是因为它只用 Position。`a46c5b2` 已去掉这两步，样式改为写入 `tssdeng.shx` / `tssdchn.shx` 文件名，并把 AutoCAD ErrorStatus 打进失败信息。

## 用户待办

完全退出 AutoCAD 后重新打开一张空白图。不要打开 `新块.dwg`，不要改 SECURELOAD。

1. `NETLOAD`：`G:\JUSTIFIED_specification reflow for AutoCAD\artifacts\dn-note-2\Justified.SpecificationReflow.AutoCAD.PluginHost.dll`
2. `DN_NOTE`：选 `测试文件/示例-单行说明.docx`；标准目录填上面的 drafts；图幅 `A1`；单位比例 `1`；在图上点一下。
3. 成功标准：`DN_NOTE_OK objects=6`，并且 after 比 before 多 6。特性里是单行文字。再跑一次不删除旧字；一次 `U` 只撤销最新一次。
4. 把从 `DN_NOTE` 到结束的命令行全文发回。若仍失败，新的 `DN_NOTE_FAILED` 里会有具体错误码，按那个修，不要重写排版引擎。

不要用 `示例-结构说明.docx`。它含上下标，命令会整篇拒绝且不写图。上下标在标定前继续阻断，不能改成普通数字。

## 通过之后

把这次命令行记入 `docs/devlog/T10.md`，状态仍是待验收，直到用户确认图面和一次撤销。然后才把下一阶段写成阶段3：T06 剩余的真实字形、上下标和打印，以及 T11、T12。用户说「继续下一步」且这次重试已记录为通过时，才进入阶段3。

## 最终操作不是现在这套提问

用户已明确：现在的 `DN_NOTE` 要手填标准目录、图幅和单位比例，太复杂，不能成为设计人员的正式流程。正式用法要简单，接近一键：日常就是生成，而不是每次重走调试问答。

目标流程：标准和图幅、单位比例属于工程设置，一个项目或一张图设一次并记住。设计人员之后执行一个命令，确认要生成的说明，在图上点一下说明区右上角，文字就生成。没有警告时不要再追问。有错误就停下并说明原因，不写出半套文字。

仍须遵守、不能为了少一步而丢掉的规则：单位未知时不猜比例；不按文件名偷偷换一套标准；不覆盖旧文字；上下标未标定前不改成普通数字。这些检查放在设置和生成内部，不要变成设计人员每次都要看懂的路径和版本问答。

当前 `DN_NOTE` 继续只作为开发验证。简化正式命令放到这次重试记录之后，不在修复写入的同时改交互。

## 不要重做

- 不改 `standards/drafts` 里的 pending、null 公差和 uncalibrated。
- 不把 4.5、0.75、7.2、三栏两栏写死进引擎。
- 不实现 MText，不追踪旧图增量。
- PDF 未生成。页数上限 10000 只是失控保护。正式发布验证未做。
