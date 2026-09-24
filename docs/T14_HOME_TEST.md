# 家中电脑本地测试（T14 候选版）

本页用于 AutoCAD 2021 的空白测试图。当前包是验证候选，不用于正式出图；测试标准位于本机 `artifacts/t14-share/20260924-fix-43fa507/local-test-package`，标有 LOCAL-TEST-ONLY。

## 已由 Agent 准备

- 新版 `.bundle` 已放入当前 Windows 用户的 `%APPDATA%\Autodesk\ApplicationPlugins`。旧版备份位置在本次交付记录中。
- 本机测试标准和 `sample.docx` 保留在 `artifacts/t14-share/20260924-fix-43fa507`，未上传云盘。

## 你在 AutoCAD 中做

1. 退出后重新打开 AutoCAD 2021，新建**空白测试图**。输入 `DN_DIAG`，应看到 `DN_DIAG_OK`。如果出现正常的插件加载提示，按本机既有授权流程处理；不要调低安全设置。
2. 输入 `DN_NOTE`。新窗口选“选择模板目录”，指向 `artifacts/t14-share/20260924-fix-43fa507/local-test-package`。Word 文件选同目录上一级的 `sample.docx`；选 A2、模板 v1.0.0。
3. 仅在确认测试图是 **1 图形单位 = 1 mm** 时，单位比例填 `1`。否则按实际图纸单位填写，未知就先停下核对。
4. 点击“在图纸中点位置”，在空白处点一次。成功时命令行出现 `DN_NOTE_OK`。按 F2 查看 `DN_NOTE_TIMING_MS`，记录点位到完成的实际等待时间。
5. 再执行 `DN_NOTE`，确认上次选项预填但可改，改选 A1 或 A3 后在另一处点位，检查旧说明仍保留。第三次打开窗口点击取消，不应新增文字。
6. 放大查看分栏、上下标 `m³` / `10⁻⁴` 和钢筋符号；测试图可用一次 `U` 撤销最后一次新增。

请回传：选择窗口截图、`DN_NOTE_TIMING_MS` 整行、是否看到 `DN_NOTE_OK`、异常位置放大图和错误命令行。窗口高 DPI、键盘 Tab/Enter/Esc、点位取消也请顺手观察。打印、断网与专业审查尚不由这次测试自动通过。
