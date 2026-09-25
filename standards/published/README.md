# 随包默认标准与模板（前期固定）

用户 2026-09-25 决定：前期发布包固定内置这一套模板，安装后即可在 CAD 选择窗直接选 A1/A2/A3；后续要改或有新模板再加入，属于管理员维护动作，随新版本发布替换。

- 标准：`standards/jsr-note/1.0.0.json`（classification=production）
- 模板：`templates/jsr-A1-three-column/1.0.0.json`、`jsr-A2-three-column`、`jsr-A3-two-column`（各一份，classification=production、status=calibrated）
- 目录布局即加载布局：`standards/<标准ID>/<版本>.json`、`templates/<模板ID>/<版本>.json`；每图幅必须保持唯一有效模板，多份会阻断并提示管理员。
- 这些资产来自已在本机 AutoCAD 2021 真实生成验证过的版本；正式院标发布、打印与专业签收完成后，在此目录整份替换并提升版本号，旧版本留存备查。
- 不要把草案或未标定文件放进本目录冒充生产值；改动需重新跑核心测试与宿主验收。
