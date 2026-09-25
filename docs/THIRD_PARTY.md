# 第三方依赖与分发边界
M0 采用精确版本并提交 NuGet lock 文件；不是对“最新版本”的承诺。
依赖元数据核对与加载结果见 T02；升级必须重新验证 net48 和宿主加载。

| 依赖 | 固定版本 | 许可证/用途 |
| --- | --- | --- |
| DocumentFormat.OpenXml | 3.0.2 | MIT，DOCX 解析 |
| DocumentFormat.OpenXml.Framework | 3.0.2（传递） | MIT，Open XML 基础 |
| Newtonsoft.Json | 13.0.3 | MIT，JSON |
| NUnit | 3.14.0 | MIT，仅测试 |
| NUnit3TestAdapter | 4.5.0 | MIT，仅测试 |
| Microsoft.NET.Test.Sdk | 17.8.0 | MIT，仅测试 |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | Microsoft .NET 引用程序集许可，仅编译引用，不分发 |
| AutoCAD 托管 DLL | 本机 24.0.47.0 | Autodesk 专有，仅本机引用，不随包分发 |
| Noto Sans SC Variable | 2.004 字体文件（SHA256 763146584CF0710223441356B4395E279021B0806C196614377A7A0174AE074A） | SIL Open Font License 1.1，仅用于程序界面，字体与许可全文随包分发 |

NuGet 包来自 https://api.nuget.org/v3/index.json。完整传递版本以 packages.lock.json 为准。
运行包复制 PluginHost 输出的运行 DLL 和固定的 Noto Sans SC 程序界面字体，不复制测试工具、Autodesk SDK 或 CAD 图纸字体。
运行依赖许可证原文随包 third-party/ 分发：Open XML 来源 v3.0.2 标签 LICENSE，Newtonsoft 来源官方 NuGet 包 LICENSE.md。
生产发布前复核依赖漏洞、实际字体授权、安装签名及所有传递许可证；M0 不冒充生产发行。

官方依据：
- [AutoCAD 兼容性](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm)
- [Open XML 3.0.2](https://www.nuget.org/packages/DocumentFormat.OpenXml/3.0.2)
- [Newtonsoft.Json 13.0.3](https://www.nuget.org/packages/Newtonsoft.Json/13.0.3)
- [AutoCAD 包支持范围](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Customization/files/GUID-1591CA01-EF87-48CD-952B-772FE26037F1.htm)
