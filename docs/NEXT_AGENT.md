# 当前接续状态（所有 Agent 共用）

更新：2026-09-22 20:20。T06 上下标与字符清单已落地并通过真实宿主验证；验收范围按用户确认收窄。分支task/T09-dbtext，继承未验收T06/T07/T08，不合main、不打标签、不推送。

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
