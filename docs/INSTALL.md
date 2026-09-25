# Word→CAD 安装与使用

当前 ZIP 是 0.1.0 验收候选包，已包含 Word→DBText 功能。BUILD.json 标明源码提交、工作区是否有改动及未验收项。三图幅正式标准尚未发布，不能把候选包当成正式出图版本。2026-09-25 起提供图形化安装程序（Setup.exe）；旧 ZIP 仍按其随包说明操作。

## 图形化安装（普通用户，推荐）

1. 环境：Windows x64、AutoCAD 2021（R24.0）、.NET Framework 4.8。其他 CAD 版本尚未承诺。
2. 解压 ZIP，双击根目录的 `Setup.exe`。安装程序依次：检查运行环境（.NET 4.8、AutoCAD 2021、AutoCAD 已退出）→ 按 SHA256.json 校验包完整性 → 复制插件到当前用户 `%APPDATA%/Autodesk/ApplicationPlugins` → 注册“应用和功能”卸载入口。不需要联网，不使用 GitHub、PowerShell 或手工复制 `.bundle`。
3. 升级：自动把旧版 `.bundle` 备份为同目录 `.bundle.backup-<时间戳>` 后再复制新版；回滚 = 退出 AutoCAD，把新版移出 ApplicationPlugins，再把备份目录改回原名称。
4. 卸载：在 Windows“应用和功能”选择本工具卸载，或重新运行 `Setup.exe --uninstall`。卸载只删插件文件，不删图纸或已生成的文字。
5. 安装完成后启动 AutoCAD，按正常安全提示加载可信来源插件；安装程序不修改 CAD 安全设置。执行 `DN_DIAG` 应显示 DN_DIAG_OK。DN_NOTE、DN_NOTE_REPEAT、DN_NOTE_SET 均已登记按命令自动加载。
6. 环境不满足（缺 .NET 4.8、无 AutoCAD 2021、AutoCAD 正在运行）时安装程序明确阻断并说明原因；包校验不通过时停止安装并列出问题文件。
7. 目标机无需安装 .NET SDK、NuGet 或联网下载运行依赖。字体与大字体需由有授权的来源单独安装；插件包不分发 Autodesk SDK 或字体。

## 手动安装与完整性检查（备用/开发）

1. 解压 ZIP。可在 PowerShell 中运行 `& '<解压目录>/JUSTIFIED_specification-reflow-for-AutoCAD.bundle/verify-package.ps1' -BundlePath '<解压目录>/JUSTIFIED_specification-reflow-for-AutoCAD.bundle'`，应显示 PACKAGE_VERIFY_OK。此检查确认文件完整性，不代替发布者签名或可信来源核验。
2. 退出 AutoCAD，把完整 .bundle 文件夹放入当前用户 `%APPDATA%/Autodesk/ApplicationPlugins`。如有旧版，先移动旧版到该目录之外留作回滚，不覆盖正在使用的 DLL。
3. 其余步骤（加载、诊断命令）与图形化安装相同。

## 设计人员使用

- 维护人准备已校验的标准包，目录为 `standards/<标准ID>/<版本>.json`、`templates/<模板ID>/<版本>.json`。默认位置是插件 `Contents/Windows/standards/published`；也可在首次设置时指定其他目录。候选包的默认目录目前只有说明，必须明确选择本机验收包才能试验；不要把草案改名冒充生产值。
- 新入口：在模型空间执行 `DN_NOTE`，在 CAD 小窗口选择本次 DOCX 和图幅，确认单位比例后点一次说明区右上角。模板版本由管理员在发布目录中为每个图幅保留唯一有效值；多份有效模板会阻断并提示管理员整理。下次执行会预填上次 DOCX/图幅/比例，但可改选；同图可生成不同图幅和说明。单位不明时先确认，一毫米对应一个图形单位才填 1。
- `DN_NOTE_REPEAT` 明确复用当前图最后一次选择并直接点位置。`DN_NOTE_SET` 的逐项提问仅供旧图和验收脚本。无警告不再提问；有警告先确认。错误则不落图。生成结果为可编辑单行文字，一次 `U` 撤销本次生成。
- 改好 DOCX 后，删除原说明并重新生成；不删除则另加一份，不会自动更新旧文字。不要使用 DN_NOTE_DEV 作为正式流程。

## 报告、验收与异常

报告只存本地，包含输入 hash、规则版本、行/页/对象数及读取、解析、排版、坐标放置、落图、用户等待和总耗时。EngineMilliseconds 不含用户等待；FirstRunInProcess 仅指首次生成命令，不能代表启动 CAD 的成本。输入 hash 来自实际解析的文件内容，不是事后重新读取的文件。

在同一输入和设置下生成两次，比较文字和坐标；测试图中一次 U 后应恢复生成前状态。异常时保留报告、命令输出、BUILD.json、AutoCAD 版本及触发步骤。报告可能含本地路径和诊断正文，发送前脱敏。外机安装、断网实测、正式三图幅/真实打印和专业复核须独立登记，不能由核心测试替代。

## 回滚

退出 AutoCAD，把此包移出 ApplicationPlugins，恢复备份的完整旧包及匹配的标准配置后重新启动。不要同时安装两个同名包。回滚不删除图纸或 DOCX；已生成 DBText 随图纸保留。
