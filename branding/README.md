# 产品图标唯一源文件

`product-icon.png` 是 EngiSpace 的产品图标原稿。桌面界面和 CAD 选择窗口、安装器中的显示图都以它为源。要更换图标，只替换这个 PNG，并通知开发 Agent 重新构建、核对桌面/CAD 显示，同时由 Agent 从新 PNG 重新生成安装器 `app.ico`。请保留正方形透明底，建议至少 512×512 像素。

`src/Justified.SpecificationReflow.AutoCAD.Setup/Assets/app.ico` 是 Windows 安装器使用的派生文件，不是编辑入口。旧 CAD 资源名 `Assets.note-logo.png` 为兼容读取代码保留，实际读取本目录的 PNG。
