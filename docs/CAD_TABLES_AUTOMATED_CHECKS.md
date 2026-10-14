# T28 后台检查记录（2026-10-02）

交付前由 Agent 自动执行，并实际打开图片复核。生成的 DOCX、测量缓存、图片、PDF 和日志只在本机保存，不上传公开仓库。

## 三层检查与证据边界

| 检查 | 本轮结果 | 能证明什么 |
|---|---|---|
| 完整构建和双框架测试 | 0 警告/0 错误；net8.0、net48 各 190/190 | 解析/合并/续栏/留边/限额等代码回归；含纯文字满栏与表格底线临界检查 |
| WinForms 后台预览 | 100%/125%/150%/200% 均生成真实控件图片；图片非空、卡片无控件重叠；选图幅、非法比例/缺文件拒绝正常 | 此控件布局在模拟缩放下可绘制；不替代 CAD 进程真实 DPI/多屏实测 |
| 实际 CAD 字体与出图 | Core Console 启动成功；328 条 TEXTBOX 字形测量；三图幅分别在双框架通过；3 张首图 PDF 由 CAD 输出并转图查看 | 使用本机实际 tssdeng/tssdchn 字形；图面有完整表线、合并和上下标，正文与表格未进入预留图签区域 |

真实字体测量复排：A1 2 页/794 个文字段/196 条表线，A2 3 页/914 段/241 条线，A3 7 页/874 段/286 条线。共 12 张全页布局检查图已查看。字体用 AutoLISP TEXTBOX 取边界，随后通过测试端口供排版使用；并未直接调用插件里的 HostTextMeasureService。

背景核心 NETLOAD 返回“无法加载程序集”，诊断命令未能执行。没有将此记为 DSS 导入通过，也没有降低 SECURELOAD 或改变受信任目录。准备好的 DXF 由 Core Console 直接读取、用真实 SHX 出图；此路径验证字体与布局显示，不能证明插件加载、事务、一次撤销或工程图框适配。用户原有 CAD 实例及业务图未修改。

## 样本与图面核对

复杂样本由测试源码生成：六个不等宽列、两层重复表头、横纵组合合并、40 组/120 个唯一正文条目、多段长文字及平方米上标；无业务结论或私有工程数据。三图幅自动要求所有条目仅出现一次、字形在栏内、文字占位不互相重叠、表线端点在安全区域内。另有 250 段纯文字满栏回归，验证与表格使用同一区域。

图签是简化示意（底部 25 mm），不会把未知工程图签当成标准。候选模板 1.2.1 按原定下留量 A1 41.63 / A2 50 / A3 52 mm 约束完整文字字形与表格边线。不同实际图框仍须按工程模板核对。

本轮先生成模拟字宽图，实际 SHX 出图暴露了模拟值对上标摆放的影响；随后捕获真实字形数据重新排版，最终图使用全部实测值，缺测项不能回退模拟值。最终上标位置、长文字换行、合并处及下部留白已重新目视复核。

## Agent 接续复现

1. `scripts/build.ps1 -Target All` 完整构建与检查。设置 `T28_VISUAL_OUTPUT` 为一个新本机目录，单独运行测试过滤器 `ComplexEngineeringTableAndTextShareSafeMargins`，得到三图幅布局、测量请求和复杂 DOCX。
2. 通过 Windows PowerShell 5.1 运行 `scripts/preview-note-picker.ps1 -StandardsPath standards/candidates/t28 -Scale <1/1.25/1.5/2> -OutputPath <图片路径>`。真实控件离屏绘制，检测空白图和卡片遮挡，同时检查图幅选择及非法输入。
3. `python scripts/render-table-checks.py <证据目录>` 生成初始布局检查图及 DXF；脚本依赖 Pillow，仅用于测试。
4. `python scripts/capture-table-font-measurements.py prepare <证据目录>` 生成 `host-measure.scr`。在独立、隐藏的 AutoCAD 2021 Core Console 上，以准备的 DXF 为输入运行此脚本；再执行同一 Python 工具的 `collect`。设置 `T28_HOST_MEASUREMENTS` 指向缓存，重跑三图幅测试；收集阶段可设置 `T28_COLLECT_MEASUREMENTS=1`，继续补齐新增候选字串。本轮两批分别 312、16 条。
5. 删除收集环境变量，最后重跑三图幅测试，必须零缺测并全部通过。带 `--host-measured` 参数重新生成检查图/DXF。此时必须已有完整真实测量测试证据，不能仅改图片标签。
6. 将 DXF 复制到新的出图目录，运行 `scripts/plot-table-inspection.ps1 -AutoCadDir <实际 CAD 目录> -EvidenceDirectory <出图目录> -LayoutMeasurement host-textbox`。该脚本只输出每种图幅第一页，用简化图框核对实际字体；全部续页的布局边界另由测试与全页检查图核对。
7. 用 Poppler 将三个 PDF 转为 PNG，打开首图、上标位置、复杂合并、末页与 UI 预览进行复核。相关程序由任务启动时须记录其 PID，超时只停止本次进程；不得关闭用户 CAD。

本机最终证据：`artifacts/t28-preview2-build-final.log`、`artifacts/t28-host-final.log`、`artifacts/t28-visual/`、`artifacts/t28-host-font-plots/`。测试目录环境变量不写入系统；测试结束后普通构建不依赖这些缓存。人工集中验收见 [试用步骤](CAD_TABLES_ACCEPTANCE.md)。
