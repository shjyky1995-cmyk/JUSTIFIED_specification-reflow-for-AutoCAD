using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Justified.SpecificationReflow.AutoCAD.Setup;

internal sealed class SetupWizard : Form
{
    private static readonly Color Ink = Color.FromArgb(26, 34, 51);
    private static readonly Color Muted = Color.FromArgb(99, 111, 133);
    private static readonly Color Blue = Color.FromArgb(19, 101, 230);
    private static readonly Color Line = Color.FromArgb(210, 220, 235);
    private static readonly Color Good = Color.FromArgb(22, 132, 74);
    private static readonly Color Bad = Color.FromArgb(200, 48, 40);

    private readonly bool _uninstallMode;
    private readonly InstallService _service;
    private readonly Panel _content;
    private readonly Label _steps;
    private readonly Button _primary;
    private readonly Button _cancel;
    private readonly Panel _welcomePage;
    private readonly Panel _envPage;
    private readonly Panel _installPage;
    private readonly Panel _finishPage;
    private readonly TableLayoutPanel _envTable;
    private readonly Label _envSummary;
    private readonly ProgressBar _progress;
    private readonly TextBox _log;
    private readonly Label _installStatus;
    private readonly Label _finishTitle;
    private readonly Label _finishBody;
    private readonly Label _versionLine;
    private int _page;

    public SetupWizard(bool uninstallMode)
    {
        _uninstallMode = uninstallMode;
        _service = new InstallService(BundleLocator.BundleRoot);
        Text = _uninstallMode ? "卸载 Word 设计说明落图工具" : "安装 Word 设计说明落图工具";
        ClientSize = new Size(780, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        ForeColor = Ink;
        Font = UiFont.Pixel(15);

        var logo = LogoLoader.Load();
        if (logo != null)
        {
            var logoBox = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Left = 28, Top = 14, Width = 56, Height = 58 };
            Controls.Add(logoBox);
        }
        Controls.Add(Label(_uninstallMode ? "卸载 Word 设计说明落图工具" : "安装 Word 设计说明落图工具", 100, 14, 640, 34, 22, true));
        _versionLine = Label(string.Empty, 100, 52, 640, 24, 14, false);
        _versionLine.ForeColor = Muted;
        Controls.Add(_versionLine);
        Divider(90);

        _content = new Panel { Left = 16, Top = 104, Width = 748, Height = 396 };
        Controls.Add(_content);

        _welcomePage = Page();
        _envTable = new TableLayoutPanel
        {
            Left = 0, Top = 56, Width = 748, Height = 300, ColumnCount = 2, GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        _envTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        _envTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _envSummary = Label(string.Empty, 0, 368, 748, 24, 14, true);
        _envPage = Page();
        _envPage.Controls.Add(Label("运行环境检查", 0, 8, 748, 28, 18, true));
        _envPage.Controls.Add(_envTable);
        _envPage.Controls.Add(_envSummary);

        _installStatus = Label(string.Empty, 0, 8, 748, 28, 16, true);
        _progress = new ProgressBar { Left = 0, Top = 44, Width = 748, Height = 12, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
        _log = new TextBox
        {
            Left = 0, Top = 72, Width = 748, Height = 280, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, Font = UiFont.Pixel(13)
        };
        _installPage = Page();
        _installPage.Controls.Add(_installStatus);
        _installPage.Controls.Add(_progress);
        _installPage.Controls.Add(_log);
        var hint = Label("安装期间请勿关闭本窗口。", 0, 360, 748, 22, 13, false);
        hint.ForeColor = Muted;
        _installPage.Controls.Add(hint);

        _finishTitle = Label(string.Empty, 0, 8, 748, 34, 20, true);
        _finishBody = new Label
        {
            Left = 0, Top = 56, Width = 748, Height = 320, AutoSize = false, ForeColor = Ink, Font = UiFont.Pixel(14)
        };
        _finishPage = Page();
        _finishPage.Controls.Add(_finishTitle);
        _finishPage.Controls.Add(_finishBody);

        Divider(508);
        _steps = Label(string.Empty, 16, 516, 440, 26, 13, false);
        _steps.ForeColor = Muted;
        Controls.Add(_steps);
        _primary = Button("下一步", 500, 514, 140, 44, Blue, Color.White, 16, true);
        _primary.Click += (_, _) => OnPrimary();
        Controls.Add(_primary);
        _cancel = Button("取消", 652, 514, 112, 44, Color.White, Muted, 15, true);
        _cancel.Click += (_, _) => Close();
        Controls.Add(_cancel);
        AcceptButton = _primary;
        CancelButton = _cancel;

        if (_uninstallMode)
        {
            ShowUninstallStart();
        }
        else
        {
            ShowInstallStart();
        }
    }

    private void ShowInstallStart()
    {
        var build = _service.LoadBuildInfo();
        _versionLine.Text = "版本 " + build.Version + " · 源码提交 " + Short(build.SourceCommit) + " · 构建于 " + build.BuiltUtc;
        var pending = build.Pending.Count == 0 ? string.Empty : "\n尚未完成的验收项：" + string.Join("；", build.Pending);
        _welcomePage.Controls.Clear();
        _welcomePage.Controls.Add(Label("欢迎使用", 0, 8, 748, 32, 20, true));
        _welcomePage.Controls.Add(Body(
            _service.BundleLooksComplete
                ? "本程序在你的电脑上安装 Word 设计说明落图工具（AutoCAD 2021 插件）。\n\n" +
                  "安装步骤：检查运行环境 → 校验安装包完整性 → 复制插件并注册卸载入口。全程约十几秒，不需要联网。\n\n" +
                  "安装前请先保存图纸并退出 AutoCAD 2021。\n\n" +
                  "支持环境：" + build.SupportedHost + "\n" +
                  "插件内含 Noto Sans SC 界面字体（SIL OFL 1.1 许可，见包内 third-party 目录）。" + pending
                : "未找到随本程序的安装内容（.bundle 文件夹）。\n\n" +
                  "请把发布 ZIP 完整解压到一个文件夹，保持 Setup.exe 与 JUSTIFIED_specification-reflow-for-AutoCAD.bundle 文件夹在同一目录，再重新运行本程序。\n\n" +
                  "不要单独拷贝 Setup.exe 到其他位置运行。"));
        ShowPage(_welcomePage);
        _steps.Text = "① 欢迎 → ② 环境检查 → ③ 安装 → ④ 完成";
        _page = 0;
        _primary.Text = "下一步";
        _primary.Enabled = _service.BundleLooksComplete;
        _cancel.Text = "取消";
        _cancel.Enabled = true;
    }

    private void ShowUninstallStart()
    {
        _versionLine.Text = "仅移除本机安装的插件文件，不会删除图纸或已生成的文字。";
        var installed = RegistryStore.ReadInstall();
        var location = installed.InstallLocation;
        var found = !string.IsNullOrWhiteSpace(location) && System.IO.Directory.Exists(location);
        _welcomePage.Controls.Clear();
        _welcomePage.Controls.Add(Label("卸载确认", 0, 8, 748, 32, 20, true));
        _welcomePage.Controls.Add(Body(
            found
                ? "已检测到安装位置：\n" + location + "\n\n卸载将删除该插件包，并移除“应用和功能”中的卸载入口。图纸中已生成的 DBText 文字不受影响。"
                : "未检测到已注册的安装记录。仍会尝试删除默认位置的插件包（如存在）。\n\n图纸中已生成的 DBText 文字不受影响。"));
        ShowPage(_welcomePage);
        _steps.Text = "卸载向导";
        _page = 0;
        _primary.Text = "卸载";
        _primary.Enabled = true;
        _cancel.Text = "关闭";
        _cancel.Enabled = true;
    }

    private void OnPrimary()
    {
        if (_uninstallMode)
        {
            if (_page == 0)
            {
                ShowPage(_installPage);
                _installStatus.Text = "正在卸载…";
                _log.Clear();
                _primary.Enabled = false;
                _cancel.Enabled = false;
                RunUninstall();
            }
            else
            {
                Close();
            }
            return;
        }

        switch (_page)
        {
            case 0:
                ShowEnvironment();
                break;
            case 1:
                StartInstall();
                break;
            default:
                Close();
                break;
        }
    }

    private void ShowEnvironment()
    {
        _envTable.Controls.Clear();
        _envTable.RowStyles.Clear();
        var checks = EnvironmentProbe.Inspect();
        _envTable.RowCount = checks.Count;
        var row = 0;
        foreach (var check in checks)
        {
            var glyph = new Label
            {
                Text = check.Passed ? "✔" : "✘",
                ForeColor = check.Passed ? Good : Bad,
                Font = UiFont.Pixel(15, true),
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 6)
            };
            var text = new Label
            {
                Text = check.Name + "\n" + check.Detail,
                ForeColor = Ink,
                Font = UiFont.Pixel(14),
                AutoSize = true,
                MaximumSize = new Size(690, 0),
                Margin = new Padding(0, 4, 0, 8)
            };
            _envTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _envTable.Controls.Add(glyph, 0, row);
            _envTable.Controls.Add(text, 1, row);
            row++;
        }
        var passed = EnvironmentInspector.AllBlockingChecksPassed(checks);
        _envSummary.Text = passed ? "全部满足，可以开始安装。" : "有不满足的项目，请先按上面提示处理，再运行本安装程序。";
        _envSummary.ForeColor = passed ? Good : Bad;
        ShowPage(_envPage);
        _page = 1;
        _primary.Text = "开始安装";
        _primary.Enabled = passed;
    }

    private void StartInstall()
    {
        ShowPage(_installPage);
        _installStatus.Text = "正在安装…";
        _log.Clear();
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 30;
        _primary.Enabled = false;
        _cancel.Enabled = false;
        RunInstall();
    }

    private async void RunInstall()
    {
        IProgress<string> progress = new Progress<string>(line => _log.AppendText(line + Environment.NewLine));
        try
        {
            var result = await Task.Run(() =>
            {
                var build = _service.LoadBuildInfo();
                var verification = _service.Verify(out var entries);
                progress.Report(verification.Ok
                    ? "包完整性校验通过（" + entries.Count + " 个文件）。"
                    : "包完整性校验未通过：");
                foreach (var problem in verification.Problems)
                {
                    progress.Report("  " + problem);
                }
                if (!verification.Ok)
                {
                    throw new InvalidOperationException("安装包不完整或被改动，已停止安装。请重新下载发布包。");
                }
                return _service.Install(build, progress);
            });
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = _progress.Maximum;
            _finishTitle.Text = "安装完成";
            _finishTitle.ForeColor = Good;
            var backup = string.IsNullOrEmpty(result.BackupPath)
                ? string.Empty
                : "\n\n旧版本已备份到：\n" + result.BackupPath + "\n如需回滚：退出 AutoCAD，把新版插件包移出 %APPDATA%\\Autodesk\\ApplicationPlugins，再把该备份目录改回原名称。";
            _finishBody.Text =
                "接下来：\n" +
                "1. 启动 AutoCAD 2021。\n" +
                "2. 如弹出插件安全提示，请按你单位既有流程加载可信来源；本程序不修改 CAD 安全设置。\n" +
                "3. 在命令行输入 DN_DIAG，应显示 DN_DIAG_OK。\n" +
                "4. 执行 DN_NOTE：在弹窗中选择说明 Word 文档和图幅、确认单位比例，然后点一次说明区右上角。\n\n" +
                "以后可在“应用和功能”中卸载本工具。" + backup;
            ShowPage(_finishPage);
            _page = 3;
            _steps.Text = "④ 完成";
            _primary.Text = "完成";
            _primary.Enabled = true;
            _cancel.Enabled = true;
        }
        catch (System.Exception error)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _installStatus.Text = "安装失败：" + error.Message;
            _log.AppendText("失败原因：" + error.Message + Environment.NewLine);
            _primary.Text = "重试";
            _primary.Enabled = true;
            _cancel.Enabled = true;
        }
    }

    private async void RunUninstall()
    {
        IProgress<string> progress = new Progress<string>(line => _log.AppendText(line + Environment.NewLine));
        try
        {
            await Task.Run(() => _service.Uninstall(progress));
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = _progress.Maximum;
            _finishTitle.Text = "卸载完成";
            _finishTitle.ForeColor = Good;
            _finishBody.Text = "插件文件与卸载入口已移除。\n\n如果之后想重新使用，重新运行安装程序即可。图纸中已生成的文字不受影响。";
            ShowPage(_finishPage);
            _page = 1;
            _steps.Text = "卸载完成";
            _primary.Text = "关闭";
            _primary.Enabled = true;
            _cancel.Enabled = true;
        }
        catch (System.Exception error)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _installStatus.Text = "卸载失败：" + error.Message;
            _log.AppendText("失败原因：" + error.Message + Environment.NewLine);
            _primary.Text = "重试";
            _primary.Enabled = true;
            _cancel.Enabled = true;
        }
    }

    private Panel Page()
    {
        var panel = new Panel { Left = 0, Top = 0, Width = 748, Height = 396, Visible = false };
        _content.Controls.Add(panel);
        return panel;
    }

    private static Label Body(string text)
    {
        return new Label
        {
            Left = 0, Top = 52, Width = 748, Height = 330, AutoSize = false, ForeColor = Ink, Font = UiFont.Pixel(14), Text = text
        };
    }

    private static Label Label(string text, int left, int top, int width, int height, float size, bool bold)
    {
        return new Label
        {
            Text = text, Left = left, Top = top, Width = width, Height = height,
            Font = UiFont.Pixel(size, bold), ForeColor = Ink, AutoSize = false
        };
    }

    private static Button Button(string text, int left, int top, int width, int height, Color back, Color fore, float size, bool bold)
    {
        var button = new Button
        {
            Text = text, Left = left, Top = top, Width = width, Height = height,
            BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = UiFont.Pixel(size, bold)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void Divider(int top)
    {
        Controls.Add(new Panel { Left = 16, Top = top, Width = 748, Height = 1, BackColor = Line });
    }

    private void ShowPage(Panel page)
    {
        foreach (var control in _content.Controls)
        {
            if (control is Panel panel)
            {
                panel.Visible = false;
            }
        }
        page.Visible = true;
        page.BringToFront();
    }

    private static string Short(string commit)
    {
        return commit.Length <= 7 ? commit : commit.Substring(0, 7);
    }
}
