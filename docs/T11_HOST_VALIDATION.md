# T11 本机宿主验证（2026-09-23）

范围：AutoCAD 2021、独立空白 Drawing1、本机合成 DOCX 和本机测试标准；不代表正式业务、断网、外机或打印验收。运行代码为 f7a1576，测试包未进入正式标准目录。

## 性能

输入 100 段、10000 字，SHA256 `1298cedeee01de9f6358f9a22648e0a7ea7bf4fc5cd962ac864da6b3f5ff553a`。21 次成功（首次命令 1 次、热运行 20 次），每次 1 页、200 个 DBText，无警告。每次后执行一次 U。

| 指标 | 实测 |
|---|---:|
| 热运行准备 P95 | 1.0939 ms |
| 热运行点选后 P95（扣除用户等待） | 373 ms |
| 热运行引擎 P95 | 372.6311 ms |
| 首次命令总时间 | 805 ms |

P95 按 nearest-rank；首次命令不等于宿主冷启动。汇总 `artifacts/t11-host/summary.json`，21 份原始报告在其 reports 目录，脚本 benchmark.scr；releaseAccepted=false。

## 重复、撤销与拒绝

- 15000 字、150 段，两次各生成 2 页、300 个 DBText。快照比较正文、插入点、字高、宽度因子、样式和真实包围盒，完全一致；提取正文合计15000字。
- 两份快照 SHA256 都是 `4D6752D8573C082B3AEB739AD2222C6665C616272D2244BF1586B872EB603A89`。两次 U 后的 TEXT 快照均为空。
- 501 段超过500段，201000字超过200000字，均 E_RESOURCE_LIMIT；损坏 DOCX 为 E_DOCX_READ。三次快照为空，没有部分输出。
- 点选位置时按 Esc，报告 CANCELLED、Committed=false、Objects=0，图面仍为空。此项不代表处理中取消时延。
- 原始证据 `artifacts/t11-validation/`：validate.scr、reports、two-pages-6.txt/two-pages-18.txt、undo-6.txt/undo-18.txt、checks.json。业务源文件未修改，脚本恢复 FILEDIA/日志变量。

## 待验证

新开发的前台 Esc 轮询取消、失败预检报告和提前页数限制尚未在新宿主加载；核心双框架143/143、编译零警告。仍需处理中取消与零残留、冷启动加载、限额标定、断网运行，以及T12正式三图幅/业务/外机/打印/专业复核。原始报告与字体、SDK不入库。

## 2026-09-23 新版人工验证回收

8f0225b 候选 DLL 已在 AutoCAD 2021 的空白 Drawing1 加载。`artifacts/t11-manual/20260923-211640-163` 有两组正常/缺失文档报告：正常生成各 300 个 DBText，`undo.txt=0`；缺失文档各记录 `E_DOCX_READ`，`missing.txt=0`。因此新版正常生成、一次撤销和该预检失败报告已取得宿主证据。

处理中取消**未通过**：取消样本报告显示成功提交 14 页、2850 个 DBText，读取/解析/排版/落图共约 1.07 秒；`after-cancel.txt=2850`。这是按 Esc 晚于提交的验证失败，现有报告不能判断轮询取消是否有效，也不能当作“取消后残留”的产品缺陷。核对器保留 `hostChecksPassed=false`；重新验证须使用独立空白图和新目录。未测的冷启动、断网、资源标定及正式样本继续 pending。
