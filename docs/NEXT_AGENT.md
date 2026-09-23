# 当前接续状态（所有 Agent 共用）

更新：2026-09-23。T06～T10 验收登记已完成；task/T09-dbtext 已推送 GitHub 私有仓库作阶段备份，并设置远程跟踪。不合 main、不打新标签。

## 已有证据

- 核心测试 net8.0/net48 各 140/140，0 警告、0 错误；插件编译 0 警告（DLL SHA256 前 8 位 87f242e8，交付副本 artifacts/cad-retry-note3/plugin/）。
- 范围调整（用户确认）：删除独立打印标定关卡（并入 T12 一次真实打印确认）；符号映射以 passthrough-1.0.0 为发布值，不建完整映射表。上下标保留并已实现：项目制定缩放 0.7、上标抬升 0.35、下标下沉 0.2（相对正文字高）。
- 宿主验证（computer-use 驱动独立实例，代点一次“加载一次”）：
  - 含上下标样本 objects=18（原整篇拒绝）；字高 3.15 的上下标 DBText，上标 y=-62.225（+1.575）、下标 y=-64.7（-0.9），与标定一致。
  - 字符清单样本（21 段，Φ±℃≤≥≈×÷→←↑↓、N/mm²、w₁/w₂）：objects=29 warnings=0，实体字符完整，屏幕截图确认全部正常渲染。
  - ① ② ③ 在 tssdchn.shx 下渲染为问号：已实现 FontGlyphCoverage 实测缺失清单 + E_FONT_MISSING 阻断（不落问号），核心测试覆盖。
  - 证据：artifacts/cad-retry-note3/（script-entities.txt、chars-entities.txt、宿主日志、chars.docx 构造目录）。
- T10 正式命令证据见 T10 日志（DN_NOTE_SET+DN_NOTE 闭环、撤销、UCS、报告、拒绝路径、DEV 回归）。

## 当前工作

MVP 功能闭环已完成：Word→解析→换行→分栏续页→DBText（含上下标、工程字符）→一键两步→撤销→运行报告→限额。剩余为 T11 性能/离线包/部署文档与 T12 三图幅业务样本、外机、一次打印确认、Word/WPS 同义核对（CAL-06）、专业核验。

## 用户待办及恢复

当前无需用户操作。宿主复验方式（如需）：新开独立实例 → NETLOAD artifacts/cad-retry-note3/plugin/Justified.SpecificationReflow.AutoCAD.PluginHost.dll（SHA256 前 8 位 87f242e8）→ 安全提示点“加载一次” → SCRIPT 运行 artifacts/cad-retry-note3/note3-main.scr、note3-nosetting.scr。注意：AutoCAD 脚本遇未知命令会中止；NETLOAD 命令行模式循环提示；脚本内不能有裸空行。

## 未完成

CAL-06 Word/WPS 同义核对（轻量，可并入 T12）；T11 冷/热性能、断网离线包、部署文档；T12 三图幅业务样本、外机、一次打印确认、专业核验。standards/published 仍为空——本机 production 测试包不是发布资产。不得把小样本通过视作正式发布通过。

## 2026-09-23 推送状态

用户要求检测并恢复 GitHub 连接、推送仓库。此次 `git ls-remote origin` 成功，GitHub API 确认仓库为 PRIVATE、当前账号有 ADMIN 权限；`git push -u origin task/T09-dbtext` 成功，新建远端同名分支并建立跟踪。此前连接 reset/超时未复现，无需修改代理、TLS 或凭据配置。阶段代码基线 0ac4ef0 已完成远程备份；本次连接检查记录随后同分支提交推送。

main 保持 T05（c8e75fd），未合并、未打新标签。用户无需网络修复或手工推送操作。唯一下一步：继续阶段3（T11 性能/离线包/部署文档，随后 T12 正式验收）；按已记录决定从 main 开新任务分支，实施前核对并明确继承当前已验收功能代码的方式，避免丢失 T06～T10 成果。

2026-09-23 独立核对：`git ls-remote origin` 确认远程 refs/heads/task/T09-dbtext=2f051ea（与本地 HEAD 逐位一致）、refs/heads/main=c8e75fd（T05 未动）、无新标签。远程分支内容与本地一致（Git 内容寻址保证），阶段2备份完整。

## 2026-09-23 阶段3进行中（以此节为准）

分支 task/T11-offline-delivery：从 main 建立后 fast-forward 接入 cc219b7，保留 T06～T10，未改 main。当前 T11 进行中；新增分阶段报告及复现测试，双框架各 142/142、插件零警告。下一动作：继续离线候选包与部署验证，再做宿主性能及 T12；不把本机生产测试包提升为已发布资产。用户无需操作。
