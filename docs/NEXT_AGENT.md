# 当前接续状态（所有 Agent 共用）

更新：2026-09-23。阶段3 T11 进行中，T12 待开始。用户要求代验证并继续开发；main 保持 T05，不合并、不打新标签。任务分支 task/T11-offline-delivery 从 main 建立，再 fast-forward 接入已登记验收的 T06～T10（cc219b7）。

## 已完成和证据

- 阶段2：Word→DBText 正式一键两步、撤销、UCS、上下标与字符核验，原始本机证据 artifacts/cad-retry-note3，说明见 T06/T10 日志。阶段代码已备份 GitHub 私有分支 task/T09-dbtext。
- 本轮基线复核双框架各 140/140、插件零警告；增加运行报告与确定性测试后各 142/142、插件零警告。T11 提交 f7a1576：输入 hash、规则版本、视觉行数、读取/解析/排版/坐标放置/落图/用户等待独立计时；标准加载后的失败及取消报告。
- 一万字符及 structure/electrical/water 核心重复生成几何一致；模拟测量不代替宿主性能验收。
- 离线候选包含运行依赖、Schema、安装回滚说明、构建元数据与完整性校验；解压/篡改拦截和 PowerShell 5.1 校验通过。productionReady=false；正式标准仍未发布，未将本机生产测试包充作发布资产。
- 性能汇总脚本 scripts/summarize-performance.ps1 已验证 P95、等待扣除、混合输入拒绝。宿主合成样本和21次脚本在 artifacts/t11-host。

## 用户待办与唯一恢复动作

AutoCAD 已停在 NETLOAD 的“安全性—未签名的可执行文件”弹窗，目标为 artifacts/packages/20260923-175057-362/JUSTIFIED_specification-reflow-for-AutoCAD.bundle/Contents/Windows/Justified.SpecificationReflow.AutoCAD.PluginHost.dll。

请用户亲自点“加载一次”。computer-use SKILL 引用的 guidance.md 明确禁止 Agent 操作安全许可请求；不改安全设置或搬移 DLL 绕过。处理后 Agent 观察脚本执行，核对 artifacts/t11-host/reports 与命令日志，失败先修，再继续 T11 真实性能、资源/取消及离线验证。不要把无报告当测试通过。

脚本在新建空白 Drawing1 执行，不动业务图；每次生成后 U，末尾恢复 FILEDIA/LOGFILEMODE/LOGFILEPATH。如取消了脚本，应先恢复这些变量（t11fd/t11lm/t11lp 保存原值）再重跑。原始样本未修改。

## 未完成与边界

- T11：真实冷/暖性能、启动加载计时、取消时延、限额标定及断网实测，完整失败路径报告仍需宿主复核。
- T12：三图幅真实业务样本、正式标准发布、外机安装、一次打印、专业复核，CAL-06 Word/WPS 同义证据整理。
- 不合 main，阶段验收后再按用户决定顺序集成；只备份任务分支。字体/SDK/本机业务及 artifacts 不入库。
- 另有测试夹具 test-note-standard.json 末行换行差异，未纳入本任务提交，不覆盖它。
