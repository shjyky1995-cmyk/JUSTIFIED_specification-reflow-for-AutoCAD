# 程序界面字体约定（2026-09-25 候选）

用户希望 CAD 插件和将来的桌面程序有统一、接近 ChatGPT 的界面字体。OpenAI 官方将 OpenAI Sans 描述为品牌字体，但没有确认当前 Windows 客户端的中文全部使用同一字体，也没有把该字体作为可直接嵌入本程序的开源资源发布。不能声称本程序使用了 ChatGPT 原字体。

本项目 CAD 选择窗口固定使用 **Noto Sans SC**。字体文件随插件包放在 `Contents/Windows/fonts/NotoSansSC-VF.ttf`，程序用私有字体集合直接加载，不要求用户先安装字体。字体 SHA256 固定为 `763146584CF0710223441356B4395E279021B0806C196614377A7A0174AE074A`，缺少或换成其他文件时包验证失败。Noto Sans SC 有简体中文、拉丁文和数字，按 SIL Open Font License 1.1 开放；许可全文在包内 `third-party/NotoSansSC-OFL.txt`。T13 全新电脑安装验收仍需确认实际显示。

CAD 导入说明窗口统一由 `NotePickerForm.UiFontFamily` 提供字体，标题 23 像素、区标题 17 像素、输入 16 像素、图幅主字 19 像素、辅助说明 14 像素；保留原有字级关系，避免字号再次过大。未来桌面端复用这套字体选择，具体控件尺寸需在该端实测。此约定仅适用于**程序界面**，不会改动说明落图的 DBText 字体、院标或打印字形。

参考：

- [OpenAI 品牌字体说明](https://openai.com/brand/)
- [OpenAI 开发者界面字体说明](https://developers.openai.com/plugins/concepts/ui-guidelines)
- [Noto 字体用途和授权](https://github.com/notofonts/noto-docs/blob/main/docs/website/use.md)
