# 单位电脑测试（2026-10-08历史快照）

2026-10-14更新：源码现统一于main，当前本机整合候选见 [统一客户端指南](UNIFIED_CLIENT_GUIDE.md)。以下10月7日发布附件为历史快照，未自动升级；旧分支恢复点见 [归档清单](BRANCH_ARCHIVE_20261014.json)。

下载入口：[GitHub 单位电脑测试包](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/releases/tag/office-test-2026-10-07)。展开 Assets，下载需要的 ZIP；不要把页面的 Source code ZIP 当作可运行程序。

## 下载与启动

| 测试内容 | 下载文件 | 完整解压后启动 |
| --- | --- | --- |
| 设计说明，含七套正文及最新反馈修正 | EngiSpace-DesignNote-0.2.0-preview.3-office.zip | client/EngiSpace.exe；需要安装时运行 DesktopSetup.exe |
| 平台同主窗口投标模块 | EngiSpace-Bidding-0.4.0-bid-preview.3-office.zip | client/EngiSpace.exe → 勘察设计投标 |
| CAD 表格与留边修正 | CAD-Tables-0.2.0-preview.2-office.zip | 退出 CAD 后运行 Setup.exe，重开 AutoCAD 2021 输入 DSS |
| 原稿、共享资料库与脱敏测试样本 | Project-Content-And-Samples-office.zip | 供核对/导入，不是程序；CAD 使用其中 samples/复杂工程表格试用.docx |

假设单位电脑为 Windows 10/11 的 64 位系统。桌面包自带运行环境，无需 Git、Node、.NET SDK 或 CAD；用 Word/WPS 打开导出的 DOCX。CAD 插件目前只承诺 AutoCAD 2021、.NET Framework 4.8，并需要单位 CAD 已有 tssdeng/tssdchn 字体；包不分发 CAD 字体或 Autodesk SDK。

下载后右键 ZIP → 属性，如果有「解除锁定」先勾选，再完整解压到可写的本地短目录。建议分别指定 G:\ES\Note、G:\ES\Bid、G:\ES\CAD、G:\ES\Content；若单位没有 G 盘，换成 D 盘也可。投标包必须用短目录，不要层层套入下载文件名目录，Windows 默认路径长度限制会导致解压失败。至少预留 5 GB 空间。不要在 ZIP 内直接双击 EXE，也不要只复制 EXE。单位管理策略若阻止 EXE 或安装，请联系单位管理员，不自行关闭防护。先用免安装入口测试桌面程序。

这些是10月7日待验收候选快照，不改变 v0.1.0 正式版。10月14日统一源码合入main不表示它们通过人工验收。正文与模板已由用户授权公开，但规范有效性及工程适用性仍需核定。投标包中设计说明代码是该工作线基线；测试最新设计说明请使用单独的 DesignNote 包。

## 一次集中测试

1. 设计说明：少填参数 → 选水池/框架等模板 → 生成 → 修改正文/活荷载 → 保存并关闭 → 重开核对最后修改 → 导出 Word 草稿 → 点击「打开这份 DOCX」。其他专业也可新建，非结构专业不应显示结构参数。
2. 用 Word/WPS 核对 DOCX 正文、表格、待填写标记及可编辑性；草稿允许缺项导出。单位电脑首次启动没有本机旧草稿，七套正文应可选。钢结构/其他可靠主体正文仍待补；给排水当前为工艺总图候选，不等于已完成通用专业标准。
3. 投标：从首页进入「勘察设计投标」→ 导入包内脱敏 TXT/文字 PDF → 编辑目录/正文 → 导出 Word → 返回首页 → 再进入核对保存内容。前台应保持一个平台主窗口。程序首次启动是空工作台，不附带本机投标工程或密钥；真实 AI 需自行配置账号/密钥并确认发送，不是离线检查的必要条件。
4. CAD：先保存现有图纸并关闭 CAD → 安装候选 → 新开测试图 → DSS 选择复杂表格 DOCX、A3、核对单位比例、点图面左上角 → 核对文字、合并线、续栏表头、下部留白 → 输入一次 U 核对整体撤销；再按需测 A1/A2。不要用正式工程图作为首次测试对象。

通过标准：能启动、模板可见、最后修改能恢复、导出文件能打开编辑；投标进入/返回/重入正常；CAD 表格不越栏且一次撤销完整。真实单位电脑启动、Word/WPS 分页、安装权限、CAD DSS/打印此前未代替你验证。

异常反馈：记录使用哪个 ZIP、出现问题的步骤、Windows/CAD/Word版本、完整报错；方便时给脱敏截图。先不要删除程序目录或旧草稿。

## 完整源码下载

当前开发统一使用 [main](https://github.com/shjyky1995-cmyk/JUSTIFIED_specification-reflow-for-AutoCAD/tree/main)，不再切换三个工作线。旧分支历史已保留为 archive/2026-10-14/* 标签，详见归档清单。SDK、node_modules、运行数据、密钥与机器缓存仍需单独配置；纯测试无需开发依赖。
