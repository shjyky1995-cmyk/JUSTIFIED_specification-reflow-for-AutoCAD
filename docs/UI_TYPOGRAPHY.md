# 程序界面字体约定（2026-09-25 候选）

用户希望 CAD 插件和将来的桌面程序有统一、接近 ChatGPT 的界面字体。OpenAI 官方将 OpenAI Sans 描述为品牌字体，但没有确认当前 Windows 客户端的中文全部使用同一字体，也没有把该字体作为可直接嵌入本程序的开源资源发布。不能声称本程序使用了 ChatGPT 原字体。

本项目界面首选 **Noto Sans SC**，未安装时回退 **Microsoft YaHei UI**。Noto Sans SC 有简体中文、拉丁文和数字，按 SIL Open Font License 1.1 开放，可用于程序；当前测试电脑已安装该字体。现阶段只指定并使用本机字体，不将字体文件默认入库。T13 安装器阶段再确定是否随包附带字体及许可证文本，并在全新电脑检验回退和显示。

CAD 导入说明窗口统一由 `NotePickerForm.UiFontFamily` 提供字体，标题 23 像素、区标题 17 像素、输入 16 像素、图幅主字 19 像素、辅助说明 14 像素；保留原有字级关系，避免字号再次过大。未来桌面端复用这套字体选择，具体控件尺寸需在该端实测。此约定仅适用于**程序界面**，不会改动说明落图的 DBText 字体、院标或打印字形。

参考：

- [OpenAI 品牌字体说明](https://openai.com/brand/)
- [OpenAI 开发者界面字体说明](https://developers.openai.com/plugins/concepts/ui-guidelines)
- [Noto 字体用途和授权](https://github.com/notofonts/noto-docs/blob/main/docs/website/use.md)
