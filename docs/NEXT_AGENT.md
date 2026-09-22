# 当前接续状态（所有 Agent 共用）

更新：2026-09-22 17:50。PRD3.3 正式一键两步已通过真实宿主验证（含一个 bug 修复），进入阶段3剩余项。分支task/T09-dbtext，继承未验收T06/T07/T08，不合main、不打标签、不推送。

## 已有证据

- 修复：DrawingSettingsStore 首次写入抛 eKeyNotFound（DBDictionary.GetAt 键不存在时抛异常），改为 Contains 预检。提交 216478a，插件 DLL SHA256 前 8 位 cbf99922。
- 核心测试 net8.0/net48 各 131/131，0 警告、0 错误；插件编译 0 警告。
- 真实宿主（computer-use 驱动独立 acad.exe /b 实例，未签名的 PluginHost.dll 代点一次“加载一次”，未改 SECURELOAD、未碰用户实例）：DN_NOTE_SET_OK 写入图内设置；DN_NOTE 一键生成 6 个 DBText（TEXT/AcDbText、字高4.5、宽度0.75、旋转0、图层 JSR_NOTE_TEXT、版本化样式、行距7.2、首行(-710,-6.2)）；一次 U 撤销零残留；第二次锚点(100,50)6 个实体全平移(+100,+50)只追加；UCS 原点(100,200)+Z旋转90°输入(10,20)→WCS(80,210)坐标正确；运行报告 3 份 JSON（耗时约790ms）。
- 拒绝路径全部按预期：无设置图 DN_NOTE_SET_REQUIRED 零新增；草案目录/A2 无模板被正式命令拒绝；DN_NOTE_DEV 上下标样本 E_TEMPLATE_INVALID 零新增；DN_NOTE_DEV 回归正常（比例2 字高9、坐标×2）。证据在 artifacts/cad-retry-note3/（日志为宿主 GBK）。
- 未覆盖：Escape 点选取消（脚本无法表达，逻辑与 T10 旧命令同路径）；打印/字形未验收；正式命令比例来自设置（PRD3.3），比例2 由 DEV 复验。

## 当前工作

T10 宿主证据已取得，待用户确认后登记。下一动作进入阶段3剩余：T06 完整真实字符/上下标与打印验收、T11 冷/热性能与离线包、T12 三图幅业务样本与外机验收。本机 production 测试包（artifacts/cad-retry-note3/published）不是发布资产；standards/published 仍为空，正式发布前需真实标定资产。

## 用户待办及恢复

当前无需用户操作。若需继续宿主核对：新开独立实例，NETLOAD artifacts/cad-retry-note3/plugin/Justified.SpecificationReflow.AutoCAD.PluginHost.dll（SHA256 前 8 位 cbf99922），安全提示点“加载一次”，SCRIPT 运行 artifacts/cad-retry-note3/note3-main.scr 与 note3-nosetting.scr（脚本内已含 NETLOAD，注意 AutoCAD 脚本遇未知命令会中止、NETLOAD 命令行模式循环提示）。

## 未完成

T06完整真实字符/上下标与打印；T10 用户确认；T11性能/离线包/部署文档；T12三图幅业务样本、外机、打印和专业核验。草案pending/null/uncalibrated保持。不得把小样本通过视作正式发布通过。
