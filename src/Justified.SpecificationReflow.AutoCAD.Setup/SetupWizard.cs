using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        Text = _uninstallMode ? "卸载 Word 设计说明工具" : "安装 Word 设计说明工具";
        ClientSize = new Size(780, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        ForeColor = Ink;
        Font = UiFont.Pixel(15);
        try
        {
            Icon = global::System.Drawing.Icon.ExtractAssociatedIcon(global::System.Windows.Forms.Application.ExecutablePath);
        }
        catch (System.Exception error) when (error is ArgumentException || error is System.ComponentModel.Win32Exception)
        {
        }

        var logo = LogoLoader.Load();
        if (logo != null)
        {
            var logoBox = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Left = 28, Top = 14, Width = 56, Height = 58 };
            Controls.Add(logoBox);
        }
        Controls.Add(Label(_uninstallMode ? "卸载 Word 设计说明工具" : "安装 Word 设计说明工具", 100, 14, 640, 34, 22, true));
        _versionLine = Label(string.Empty, 100, 52, 640, 24, 14, false);
        _versionLine.ForeColor = Muted;
        Controls.Add(_versionLine);
        Divider(90);

        _content = new Panel { Left = 16, Top = 104, Width = 748, Height = 396 };
        Controls.Add(_content);

        _welcomePage = Page();
        _envTable = new TableLayoutPanel
        {
            Left = 0, Top = 56, Width = 748, Height = 300, ColumnCount = 3, GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        _envTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        _envTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 540));
        _envTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
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
        _welcomePage.Controls.Clear();
        _welcomePage.Controls.Add(Label("欢迎使用", 0, 8, 748, 32, 20, true));
        _welcomePage.Controls.Add(Body(
            _service.BundleLooksComplete
                ? "本程序用于安装 Word 设计说明工具（AutoCAD 插件）。\n\n" +
                  "安装前请先保存图纸并退出 AutoCAD。\n\n" +
                  "支持环境：AutoCAD 2021（R24.0）；后续版本将适配 AutoCAD 2014–2021 全系。"
                : "未找到随本程序的安装内容（.bundle 文件夹）。\n\n" +
                  "请把发布 ZIP 完整解压到一个文件夹，保持 Setup.exe 与 JUSTIFIED_specification-reflow-for-AutoCAD.bundle 文件夹在同一目录，再重新运行本程序。\n\n" +
                  "不要单独拷贝 Setup.exe 到其他位置运行。"));        ShowPage(_welcomePage);
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
                ? "将移除本机安装的插件。图纸中已生成的文字不受影响。\n\n是否继续卸载？"
                : "未检测到已注册的安装记录。仍会尝试删除默认位置的插件包（如存在）。\n\n是否继续卸载？"));
        ShowPage(_welcomePage);
        _steps.Text = "卸载向导";
        _page = 0;
        _primary.Text = "继续卸载";
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
                ShowEnvironment(autoFix: true);
                break;
            case 1:
                StartInstall();
                break;
            default:
                Close();
                break;
        }
    }

    private void ShowEnvironment(bool autoFix)
    {
        var checks = EnvironmentProbe.Inspect();
        RenderChecks(checks);
        if (autoFix && !EnvironmentInspector.AllBlockingChecksPassed(checks)
            && checks.Where(item => !item.Passed).All(item => item.AutoInstallable))
        {
            FixNet48Async();
        }
    }

    private void RenderChecks(IReadOnlyList<EnvironmentCheck> checks)
    {
        _envTable.Controls.Clear();
        _envTable.RowStyles.Clear();
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
                Text = check.Passed ? check.Name : check.Name + "\n" + check.Detail,
                ForeColor = Ink,
                Font = UiFont.Pixel(14),
                AutoSize = true,
                MaximumSize = new Size(530, 0),
                Margin = new Padding(0, 4, 0, 8)
            };
            _envTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _envTable.Controls.Add(glyph, 0, row);
            _envTable.Controls.Add(text, 1, row);
            if (!check.Passed && check.AutoInstallable)
            {
                var action = Button("一键安装", 0, 4, 120, 34, Blue, Color.White, 13, true);
                action.Click += (_, _) => FixNet48Async();
                _envTable.Controls.Add(action, 2, row);
            }
            else if (!check.Passed)
            {
                var url = check.HelpUrl;
                var hasUrl = !string.IsNullOrWhiteSpace(url);
                var action = Button(hasUrl ? "去获取" : "重新检查", 0, 4, 120, 34, Color.White, Blue, 13, true);
                action.Click += (_, _) =>
                {
                    if (hasUrl) OpenUrl(url!);
                    else RenderChecks(EnvironmentProbe.Inspect());
                };
                _envTable.Controls.Add(action, 2, row);
            }
            else
            {
                _envTable.Controls.Add(new Panel(), 2, row);
            }
            row++;
        }
        var passed = EnvironmentInspector.AllBlockingChecksPassed(checks);
        _envSummary.Text = passed
            ? "全部满足，可以开始安装。"
            : "环境不满足。可自动安装的项目点“一键安装”；AutoCAD 需自行安装。";
        _envSummary.ForeColor = passed ? Good : Bad;
        ShowPage(_envPage);
        _page = 1;
        _primary.Text = "开始安装";
        _primary.Enabled = passed;
        _cancel.Enabled = true;
    }

    private async void FixNet48Async()
    {
        _primary.Enabled = false;
        _envSummary.Text = "正在安装缺失组件，请按系统提示操作…";
        _envSummary.ForeColor = Muted;
        var progress = new Progress<string>(line =>
        {
            _envSummary.Text = line;
        });
        try
        {
            var ok = await PrerequisiteInstaller.InstallNet48Async(progress);
            var checks = EnvironmentProbe.Inspect();
            RenderChecks(checks);
            _envSummary.Text = ok
                ? "缺失组件已安装，正在重新检查…"
                : "自动安装未完成，请手动安装后点“重新检查”。";
            _envSummary.ForeColor = ok ? Good : Bad;
        }
        catch (System.Exception error)
        {
            RenderChecks(EnvironmentProbe.Inspect());
            _envSummary.Text = "自动安装出错：" + error.Message;
            _envSummary.ForeColor = Bad;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (System.Exception error) when (error is System.ComponentModel.Win32Exception || error is InvalidOperationException)
        {
            global::System.Windows.Forms.MessageBox.Show(
                "无法打开浏览器，请手动访问：\n" + url,
                "安装 Word 设计说明工具",
                global::System.Windows.Forms.MessageBoxButtons.OK,
                global::System.Windows.Forms.MessageBoxIcon.Information);
        }
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
                : "\n\n旧版本已备份；如需回滚，退出 AutoCAD 后把备份目录改回原名称即可（详见包内 INSTALL.md）。";
            _finishBody.Text =
                "接下来：\n" +
                "1. 启动 AutoCAD 2021；如有插件安全提示，按你单位既有流程加载。\n" +
                "2. 命令行输入 DSS，选择说明文档和图幅、确认单位比例，点一次位置即可生成。" + backup;
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
            _finishBody.Text = "卸载完成。\n\n重新运行安装程序即可再次安装。图纸中已生成的文字不受影响。";
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
