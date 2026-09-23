# 当前接续状态（所有 Agent 共用）

更新：2026-09-23。阶段3 T11进行中，T12待开始；分支 task/T11-offline-delivery。main 保持T05，用户决定不合并、不打新标签。

## 协作方式

用户已要求继续工作，本轮准备人工验证，不启动宿主操作。今后默认用户方便时操作AutoCAD；Agent准备简短步骤、样本及自动报告，继续不依赖人工的开发。用户明确说不方便并要求代操作时再使用Computer Use，避免反复截图消耗额度。

## 已完成和证据

- T06～T10已登记闭环验收，GitHub私有 task/T09-dbtext 已备份。Word解析、换行、分栏续页、DBText、撤销、UCS、上下标与字符证据见各任务日志。
- T11报告、候选包、完整性校验和性能汇总已实现。旧版宿主21次万字生成成功，20次热运行点选后P95=373ms；两页重复生成快照一致、撤销和超限/损坏文件零残留。详见 docs/T11_HOST_VALIDATION.md。
- 8f0225b补齐处理中Esc取消、预检失败报告和提前页数限制；核心双框架各143/143、插件零警告，新代码尚未完成宿主验证。
- 最新候选包 artifacts/packages/20260923-210741-496/JUSTIFIED_specification-reflow-for-AutoCAD-0.1.0-candidate-8f0225b.zip，22文件完整性校验通过；可加载DLL在 测试文件/程序/Justified.SpecificationReflow.AutoCAD.PluginHost.dll。仍是候选包，不是正式发布。

## 中断与下一步

用户已处理旧版“加载一次”，旧版宿主验证已完成，不再要求重做旧步骤。尝试检查新版验证窗口时Computer Use报告用户按实体Esc停止；未启动新版实例，不能假定当前窗口或弹窗状态。

唯一恢复动作：人工验证已备齐，见 docs/T11_MANUAL_VALIDATION.md；本轮目录 artifacts/t11-manual/20260923-211640-163。用户方便时在独立空白图执行01-auto.scr、DN_NOTE点选后按住Esc、02-collect.scr；Agent再运行scripts/check-manual-validation.ps1读取报告判断。现有已加载旧版的CAD不能直接替换程序集，需要新进程。不要把旧版成绩算作新版通过。

## 未完成与边界

- T11：新版处理中取消/时延和预检报告宿主复核；冷启动加载、限额标定、断网实测。合成样本热运行已测，不等于真实业务性能签收。
- T12：三图幅真实业务样本、正式标准发布、外机安装、一次打印、专业复核，CAL-06 Word/WPS同义证据。
- 最新功能提交8f0225b、协作规则f145625及本轮人工验证工具待本轮推送核对；main不变。人工操作尚未发生，检查器合成正反例不算宿主验收。
- main不合并、不打标签；字体/SDK/业务文件/artifacts不入库。
- 夹具 tests/Justified.SpecificationReflow.AutoCAD.Core.Tests/Fixtures/test-note-standard.json 另有末行换行差异，保留且不暂存。

