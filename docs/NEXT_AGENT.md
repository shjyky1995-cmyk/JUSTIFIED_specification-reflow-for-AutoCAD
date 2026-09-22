# 当前接续状态（所有 Agent 共用）

更新：2026-09-22 16:00。PRD3.3 正式一键两步已实现并通过核心检查，宿主图面证据未取得。分支task/T09-dbtext，继承未验收T06/T07/T08，不合main、不打标签、不推送。

## 已有证据

- 核心测试net8.0/net48各131/131，0警告、0错误；插件编译0警告。新增14个测试：设置Base64分块往返与损坏拒绝、Problems自检、MaxBlocks/Characters/Pages/Texts四个限额触发点、限额内正常生成、无限额兼容旧行为、运行报告字段（耗时/页数/对象数/包围盒/告警）、模板枚举与取消。
- Core Console（accoreconsole）拒绝加载未签名插件（SECURELOAD，无GUI无法点“加载一次”），宿主验证必须走GUI实例。
- 本机生产测试包与脚本已备：artifacts/cad-retry-note3/published/（classification=production，从drafts派生，仅本机不入库）、note3-main.scr（设置→生成→U撤销→平移生成→UCS原点(100,200)+Z旋转90°比例2）、note3-nosetting.scr（无设置拒绝、草案目录拒绝、A2无模板拒绝、DN_NOTE_DEV回归）、plugin/（新构建DLL，SHA256前8位0581531a）。

## 当前工作

用户在独立AutoCAD 2021实例NETLOAD artifacts/cad-retry-note3/plugin/Justified.SpecificationReflow.AutoCAD.PluginHost.dll，安全提示亲自点“加载一次”；SCRIPT运行note3-main.scr与note3-nosetting.scr。Agent据日志、实体快照（first/second/undo/ucs-scale-entities.txt）、报告JSON核对：正式DN_NOTE生成6个DBText、一次U恢复、平移(100,50)正确、UCS比例2坐标正确、无设置与草案包被拒、DN_NOTE_DEV仍可用。核对通过则修复发现的问题或转入阶段3剩余（T06打印/上下标、T11性能与离线包、T12外机验收）。

## 用户待办及恢复

1. 开新独立AutoCAD 2021（空白Drawing1），NETLOAD上述DLL，安全提示点“加载一次”。Agent不得改安全设置、不得搬移DLL绕过。
2. 命令行SCRIPT选note3-main.scr，再SCRIPT note3-nosetting.scr。FILEDIA已在脚本内置0。
3. 完成后回复结果；日志在artifacts/cad-retry-note3/（*.log为宿主日志）。

## 未完成

T06完整真实字符/上下标与打印；T09/T10完整正式流程验收（即本次宿主核对）；T11性能/冷热/离线包；T12三图幅业务样本、外机、打印和专业核验。草案pending/null/uncalibrated保持；本机production测试包不是发布资产，standards/published仍为空。不得把本次小样本通过视作正式发布通过。
