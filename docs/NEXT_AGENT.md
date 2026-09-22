# 当前接续状态（所有 Agent 共用）

更新：2026-09-22。阶段2 已交付。用户要在空白图里执行 DN_NOTE，并把命令行全文发回。不要合入 main。

## 当前状态

- 阶段2 代码检查完成，T09、T10 待验收。T06、T07、T08 仍待验收，未合入 main。
- 分支 task/T09-dbtext。提交用 `git log --grep="(T09)"` 和 `git log --grep="(T10)"`。
- 可加载目录：artifacts/dn-note。旧的 PluginHost/bin 当时被正在运行的 AutoCAD 锁住，不要 NETLOAD 那份旧 DLL。
- 用户待办：按「测试文件/开始测试.md」退出 AutoCAD 后重新打开，NETLOAD 新 DLL，对「示例-单行说明.docx」执行 DN_NOTE。不要用「新块.dwg」，也不要改 SECURELOAD。

## 怎样算这次人工检查通过

- 出现 DN_NOTE_OK，DN_NOTE_ENTITIES 的 after 比 before 多出 objects 的数量。
- 单位比例 1 时，DN_NOTE_FIRST 的字高是 4.5，宽度系数是 0.75；锚点在世界原点时第一行 x 约为 -710、y 约为 -6.2。
- 特性里是单行文字。再生成一次不删除旧字。一次 U 只撤销最新一次。
- 若失败，把从 DN_NOTE 到结束的命令行原文发回。DN_NOTE_DRAFT 本身不是失败。

## 下一阶段

用户回复「继续下一步」或贴出命令行后，进入阶段3：补 T06 剩余的真实字形/上下标/打印，以及 T11、T12。上下标在标定前继续阻断，不能改成普通数字。

## 保留

- 草案文件仍是 pending / uncalibrated。DN_NOTE 只在内存里补齐以便演示。
- 示例-结构说明.docx 含上下标，DN_NOTE 会整篇拒绝且不写图。
- PDF 未生成。页数上限 10000 仍只是失控保护。正式发布验证未做。
