using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EngiSpace.DesktopSetup;

internal static class Program
{
    internal const string ProductId = "engispace-design-note-client-v1";
    internal static readonly string InstallRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EngiSpace", "DesignNote");
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EngiSpace.DesignNote";
    private static readonly string Executable = Assembly.GetExecutingAssembly().Location;
    internal static readonly string PackageRoot = Path.GetDirectoryName(Executable)!;

    [STAThread]
    private static int Main(string[] args)
    {
        var rollback = args.Contains("--rollback") || Path.GetFileNameWithoutExtension(Executable).Equals("DesktopRollback", StringComparison.OrdinalIgnoreCase);
        if (args.Contains("--verify-payload"))
        {
            try { VerifyPayload(Path.Combine(PackageRoot, "client"), Path.Combine(PackageRoot, "manifest.sha256")); return 0; }
            catch { return 1; }
        }
        // 安装目录内的卸载器转到临时副本执行，避免删除正在运行的文件。
        if (Path.GetFullPath(Executable).StartsWith(InstallRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var helper = Path.Combine(Path.GetTempPath(), "EngiSpace-DesktopSetup-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Executable, helper);
            Process.Start(new ProcessStartInfo(helper, rollback ? "--rollback" : "--uninstall") { UseShellExecute = true });
            return 0;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupWindow(args.Contains("--uninstall"), rollback));
        return 0;
    }

    internal static void VerifyPayload(string source, string manifest)
    {
        if (!Directory.Exists(source) || !File.Exists(manifest)) throw new IOException("安装资料不完整，请将 ZIP 完整解压后运行 DesktopSetup.exe。");
        if (File.ReadAllText(Path.Combine(source, ".engispace-client")).Trim() != ProductId) throw new IOException("安装资料的产品标识无效。");
        RejectLinks(source);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(manifest))
        {
            if (line.Length < 67 || line.Substring(64, 2) != "  ") throw new IOException("安装清单格式错误。");
            var relative = line.Substring(66).Replace('/', Path.DirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(source, relative));
            if (!path.StartsWith(Path.GetFullPath(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !expected.Add(path)) throw new IOException("安装清单包含非法或重复路径。");
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            if (!string.Equals(hash, line.Substring(0, 64), StringComparison.OrdinalIgnoreCase)) throw new IOException("安装文件损坏：" + relative);
        }
        var actual = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        if (actual.Length != expected.Count || actual.Any(path => !expected.Contains(Path.GetFullPath(path)))) throw new IOException("安装清单与文件不一致。");
        foreach (var required in new[] { "EngiSpace.exe", @"resources\app\dist\index.html", @"resources\worker\DocxWorkbench.Worker.exe" })
            if (!File.Exists(Path.Combine(source, required))) throw new IOException("安装包缺少必要文件：" + required);
    }

    private static void RejectLinks(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("安装目录不能使用链接或联接目录。");
        foreach (var child in Directory.GetFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(child);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("安装资料中存在链接：" + Path.GetFileName(child));
            if ((attributes & FileAttributes.Directory) != 0) RejectLinks(child);
        }
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.GetDirectories(source)) CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static void RemoveOwnedDirectory(string directory)
    {
        var path = Path.GetFullPath(directory);
        if (!string.Equals(path, InstallRoot, StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith(InstallRoot + ".stage-", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith(InstallRoot + ".previous-", StringComparison.OrdinalIgnoreCase)) throw new IOException("拒绝清理安装范围之外的目录。");
        if (!Directory.Exists(path)) return;
        RejectLinks(path);
        Directory.Delete(path, true);
    }

    private static void CheckNotRunning()
    {
        foreach (var process in Process.GetProcessesByName("EngiSpace"))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, Path.Combine(InstallRoot, "EngiSpace.exe"), StringComparison.OrdinalIgnoreCase)) throw new IOException("请先保存并关闭设计说明客户端，再安装或卸载。");
                }
                catch (System.ComponentModel.Win32Exception) { /* 无权读取其他用户进程；文件替换失败时仍保持旧版本。 */ }
            }
        }
    }

    internal static void Install()
    {
        var source = Path.Combine(PackageRoot, "client");
        var manifest = Path.Combine(PackageRoot, "manifest.sha256");
        VerifyPayload(source, manifest);
        CheckNotRunning();
        var parent = Path.GetDirectoryName(InstallRoot)!;
        Directory.CreateDirectory(parent);
        if (Directory.Exists(InstallRoot))
        {
            RejectLinks(InstallRoot);
            if (!File.Exists(Path.Combine(InstallRoot, ".engispace-client")) || File.ReadAllText(Path.Combine(InstallRoot, ".engispace-client")).Trim() != ProductId) throw new IOException("目标目录已有其他文件，不能覆盖。请先检查：" + InstallRoot);
        }
        var stage = InstallRoot + ".stage-" + Guid.NewGuid().ToString("N");
        var previous = InstallRoot + ".previous-" + Guid.NewGuid().ToString("N");
        var oldMoved = false;
        var newMoved = false;
        try
        {
            CopyTree(source, stage);
            VerifyPayload(stage, manifest);
            File.Copy(Executable, Path.Combine(stage, "DesktopSetup.exe"));
            File.Copy(Executable, Path.Combine(stage, "DesktopRollback.exe"));
            if (Directory.Exists(InstallRoot)) { Directory.Move(InstallRoot, previous); oldMoved = true; }
            Directory.Move(stage, InstallRoot);
            newMoved = true;
            CreateShortcut();
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            if (key is null) throw new IOException("无法登记卸载入口。");
            key.SetValue("DisplayName", "EngiSpace · 设计说明");
            key.SetValue("DisplayVersion", File.ReadAllText(Path.Combine(InstallRoot, "VERSION.txt")).Trim());
            key.SetValue("Publisher", "EngiSpace");
            key.SetValue("InstallLocation", InstallRoot);
            key.SetValue("DisplayIcon", Path.Combine(InstallRoot, "EngiSpace.exe"));
            key.SetValue("UninstallString", "\"" + Path.Combine(InstallRoot, "DesktopSetup.exe") + "\" --uninstall");
            key.SetValue("NoModify", 1);
            key.SetValue("NoRepair", 1);
        }
        catch
        {
            if (newMoved) RemoveOwnedDirectory(InstallRoot);
            if (oldMoved) Directory.Move(previous, InstallRoot);
            throw;
        }
        finally { if (Directory.Exists(stage)) RemoveOwnedDirectory(stage); }
        // 旧版本保留一份用于回退，不删除草稿或项目。
        if (oldMoved) File.WriteAllText(Path.Combine(InstallRoot, "PREVIOUS_VERSION.txt"), previous);
    }

    private static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EngiSpace 设计说明.lnk");

    private static void CreateShortcut()
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("无法创建桌面快捷方式。");
        var shell = Activator.CreateInstance(type)!;
        var shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath })!;
        var shortcutType = shortcut.GetType();
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Path.Combine(InstallRoot, "EngiSpace.exe") });
        shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallRoot });
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "离线编制并导出可编辑的设计说明" });
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, Array.Empty<object>());
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }

    internal static void Uninstall()
    {
        CheckNotRunning();
        if (Directory.Exists(InstallRoot))
        {
            if (!File.Exists(Path.Combine(InstallRoot, ".engispace-client")) || File.ReadAllText(Path.Combine(InstallRoot, ".engispace-client")).Trim() != ProductId) throw new IOException("安装目录标识无效，不能自动卸载。");
            var previousFile = Path.Combine(InstallRoot, "PREVIOUS_VERSION.txt");
            var previous = File.Exists(previousFile) ? File.ReadAllText(previousFile).Trim() : null;
            RemoveOwnedDirectory(InstallRoot);
            if (previous is not null && previous.StartsWith(InstallRoot + ".previous-", StringComparison.OrdinalIgnoreCase)) RemoveOwnedDirectory(previous);
        }
        if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
    }

    internal static void Rollback()
    {
        CheckNotRunning();
        var previousFile = Path.Combine(InstallRoot, "PREVIOUS_VERSION.txt");
        if (!File.Exists(previousFile)) throw new IOException("没有上一版本可以回退。");
        var previous = Path.GetFullPath(File.ReadAllText(previousFile).Trim());
        if (!previous.StartsWith(InstallRoot + ".previous-", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(previous)
            || File.ReadAllText(Path.Combine(previous, ".engispace-client")).Trim() != ProductId) throw new IOException("上一版本目录无效，不能自动回退。");
        RejectLinks(InstallRoot);
        RejectLinks(previous);
        var current = InstallRoot + ".previous-" + Guid.NewGuid().ToString("N");
        Directory.Move(InstallRoot, current);
        try
        {
            Directory.Move(previous, InstallRoot);
            File.WriteAllText(Path.Combine(InstallRoot, "PREVIOUS_VERSION.txt"), current);
            CreateShortcut();
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue("DisplayVersion", File.ReadAllText(Path.Combine(InstallRoot, "VERSION.txt")).Trim());
        }
        catch
        {
            if (Directory.Exists(InstallRoot)) Directory.Move(InstallRoot, previous);
            Directory.Move(current, InstallRoot);
            throw;
        }
    }
}

internal sealed class SetupWindow : Form
{
    private readonly bool uninstall;
    private readonly bool rollback;
    private readonly Label status;
    private readonly Button primary;
    private readonly Button close;
    private bool busy;
    private bool complete;

    internal SetupWindow(bool uninstallMode, bool rollbackMode)
    {
        uninstall = uninstallMode;
        rollback = rollbackMode;
        Text = rollback ? "回退设计说明客户端" : uninstall ? "卸载设计说明客户端" : "安装设计说明客户端";
        ClientSize = new Size(660, 420);
        Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.White;
        var title = new Label { Text = "EngiSpace · 设计说明", Font = new Font(Font.FontFamily, 19, FontStyle.Bold), AutoSize = true, Location = new Point(34, 30) };
        var description = new Label { Text = rollback ? "将程序恢复到上一安装版本，保留草稿和项目。\n请先导出草稿备份，并关闭客户端。" : uninstall ? "卸载客户端，保留本机草稿、项目和导出的 DOCX。" : "离线编制 · 自动保存 · 可编辑 Word 导出\n无需 Node.js、.NET SDK 或 CAD，安装到当前用户。", AutoSize = false, Size = new Size(580, 65), Location = new Point(36, 88) };
        var path = new Label { Text = "程序位置：\n" + Program.InstallRoot, AutoSize = false, Size = new Size(580, 65), Location = new Point(36, 172), ForeColor = Color.DimGray };
        status = new Label { Text = uninstall ? "请先关闭客户端，再点击卸载。" : "首次安装或升级都使用此入口；升级保留旧版本用于回退。", AutoSize = false, Size = new Size(580, 72), Location = new Point(36, 247), ForeColor = Color.DimGray };
        primary = new Button { Text = rollback ? "回退上一版本" : uninstall ? "卸载" : "安装 / 升级", Size = new Size(150, 42), Location = new Point(466, 342), BackColor = Color.FromArgb(31, 111, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        close = new Button { Text = "关闭", Size = new Size(110, 42), Location = new Point(340, 342) };
        close.Click += (_, _) => Close();
        primary.Click += async (_, _) => await Run();
        FormClosing += (_, args) => { if (busy) args.Cancel = true; };
        Controls.AddRange(new Control[] { title, description, path, status, primary, close });
    }

    private async Task Run()
    {
        if (complete)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallRoot, "EngiSpace.exe")) { UseShellExecute = true }); Close(); }
            catch (Exception error) { status.Text = error.Message; }
            return;
        }
        busy = true;
        primary.Enabled = close.Enabled = false;
        status.Text = rollback ? "正在回退…" : uninstall ? "正在卸载…" : "正在检查并安装文件，请稍候…";
        try
        {
            await Task.Run(() => { if (rollback) Program.Rollback(); else if (uninstall) Program.Uninstall(); else Program.Install(); });
            complete = true;
            status.ForeColor = Color.FromArgb(22, 128, 70);
            status.Text = uninstall ? "客户端已卸载；草稿和项目仍保存在本机。" : rollback ? "已回退上一版本，草稿和项目已保留。" : "安装完成。可用桌面快捷方式启动，或点击下方打开客户端。";
            primary.Text = uninstall ? "已卸载" : "打开客户端";
        }
        catch (Exception error) { status.ForeColor = Color.Firebrick; status.Text = "未完成：" + error.Message; }
        finally { busy = false; close.Enabled = true; primary.Enabled = !uninstall || !complete; }
    }
}
