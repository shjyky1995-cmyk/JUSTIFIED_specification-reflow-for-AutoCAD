# T12 断网运行实测（本机）

目标：证明插件安装后不依赖网络即可完整工作。安装器与插件本身不访问网络；本项在禁用网络后实测签收。

## 代码侧保证（Agent 已做）

- 插件程序集（Contracts/DocumentCore/DocxAdapter/Standards/LayoutEngine/Application/AutoCadAdapter/PluginHost）不引用任何网络 API；核心测试含一条“无网络引用”断言，违反即构建失败。
- 安装器仅在对缺失 .NET Framework 4.8 执行“一键安装”时访问微软官方下载地址，属可选的补救动作；不补环境时完全离线可用。

## 操作步骤

1. 禁用网络：设置 → 网络和 Internet → 关闭 WLAN，并拔掉网线（或按单位网管方式断开）。
2. 启动 AutoCAD 2021，加载插件（如尚未加载）。
3. 输入 `DN_DIAG`，应显示 `DN_DIAG_OK`。
4. 输入 `DSS`，选说明 DOCX、图幅、单位比例，点一次位置，确认完整生成（成功行 `DN_NOTE_OK`）。
5. 再执行一次 `DN_NOTE_REPEAT`，确认沿用上次选择可直接生成。
6. 恢复网络。

## 回传内容

- 三步的命令行截图（DN_DIAG、DSS 生成、REPEAT）；
- 断网方式（关 WLAN/拔线）与是否有任何卡顿或超时提示。

## 通过标准

断网状态下 `DN_DIAG_OK`、DSS 完整生成、REPEAT 复用成功，全过程无网络相关提示。任何一步失败即不通过，记录现象交 Agent 定位。
