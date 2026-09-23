# 当前接续状态（所有 Agent 共用）

更新：2026-09-23。阶段3 T11进行中，T12待开始；分支 task/T11-offline-delivery。main 保持T05，用户决定不合并、不打新标签。

## 协作方式

用户已要求继续工作，本轮准备人工验证，不启动宿主操作。今后默认用户方便时操作AutoCAD；Agent准备简短步骤、样本及自动报告，继续不依赖人工的开发。用户明确说不方便并要求代操作时再使用Computer Use，避免反复截图消耗额度。

## 已完成和证据

- T06～T10已登记闭环验收，GitHub私有 task/T09-dbtext 已备份。Word解析、换行、分栏续页、DBText、撤销、UCS、上下标与字符证据见各任务日志。
- T11报告、候选包、完整性校验和性能汇总已实现。旧版宿主21次万字生成成功，20次热运行点选后P95=373ms；两页重复生成快照一致、撤销和超限/损坏文件零残留。详见 docs/T11_HOST_VALIDATION.md。
- 8f0225b补齐处理中Esc取消、预检失败报告和提前页数限制；核心双框架各143/143、插件零警告。新版已取得正常生成/撤销/缺失文档报告的宿主证据；处理中取消仍待验证。
- 最新候选包 artifacts/packages/20260923-210741-496/JUSTIFIED_specification-reflow-for-AutoCAD-0.1.0-candidate-8f0225b.zip，22文件完整性校验通过；可加载DLL在 测试文件/程序/Justified.SpecificationReflow.AutoCAD.PluginHost.dll。仍是候选包，不是正式发布。

## 中断与下一步

新版人工运行已发生，见 docs/T11_HOST_VALIDATION.md 末节：两组正常/缺失文档报告通过，正常各300对象、一次撤销零残留，缺失文档 E_DOCX_READ 且零残留。取消尝试晚于约1.07秒的提交，报告为成功14页/2850对象，after-cancel=2850；核对器正确判失败。这不能证明处理中取消实现错误，也不能算取消验证通过。旧版万字性能不重测。

唯一恢复动作：人工重试资料已备齐，见 docs/T11_MANUAL_VALIDATION.md；新目录 artifacts/t11-manual/20260923-223055-503。用户方便时在独立空白图执行01-auto.scr、DN_NOTE点选后立即按住Esc、02-collect.scr；Agent再运行scripts/check-manual-validation.ps1读取报告判断。旧目录只保留证据，不复用；现有已加载插件的CAD不能直接替换程序集，需要新进程。继续不依赖宿主的T11工作，勿把一次验证失败误写成T11完成。

## 未完成与边界

- T11：新版处理中取消/时延、冷启动加载、限额标定、断网实测；预检中的文档缺失已实测，其他预检路径待复核。合成样本热运行已测，不等于真实业务性能签收。
- T12：三图幅真实业务样本、正式标准发布、外机安装、一次打印、专业复核，CAL-06 Word/WPS同义证据。
- 本轮记录与重试工具提交 fc67712，已推送 origin/task/T11-offline-delivery；人工验证原始报告仍为本机证据，main不变。
- main不合并、不打标签；字体/SDK/业务文件/artifacts不入库。
- 夹具 tests/Justified.SpecificationReflow.AutoCAD.Core.Tests/Fixtures/test-note-standard.json 另有末行换行差异，保留且不暂存。
