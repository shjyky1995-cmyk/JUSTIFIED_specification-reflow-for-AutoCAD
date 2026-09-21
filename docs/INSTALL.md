# JUSTIFIED_specification-reflow-for-AutoCAD：M0 安装检查
此包仅包含 DN_DIAG 技术诊断，没有 DOCX 生成命令、生产院标或图纸排版功能。

## 环境与安装
- Windows x64、AutoCAD 2021（R24.0）、.NET Framework 4.8。
- 解压 ZIP，将 JUSTIFIED_specification-reflow-for-AutoCAD.bundle 放入当前用户
  %APPDATA%/Autodesk/ApplicationPlugins；安装前退出 AutoCAD。
- 若有同名包，先保留旧包到 ApplicationPlugins 之外；禁止覆盖正在加载的 DLL。
- 启动 AutoCAD，依正常安全提示确认可信来源；不要关闭 SECURELOAD。
- 打开空白图，输入 DN_DIAG；预期列出依赖版本并出现 DN_DIAG_OK。
- 命令不创建、删除、保存实体，也不修改现有图纸。
- 未签名包可能出现信任提示；如被组织策略阻止，记录情况，由环境管理者处理。

## 复测与回滚
记录包 SHA256、AutoCAD 版本、Windows 版本、命令输出和测试人。
开发机控制台加载通过不代表图形界面自动加载已验收，外部电脑必须独立记录。
退出 AutoCAD 后移出本包，恢复先前备份的完整包即可回滚；旧包与标准资产保持版本配对。
安装包不包含 Autodesk DLL、字体和业务样本，也不需要在目标机运行 NuGet。
