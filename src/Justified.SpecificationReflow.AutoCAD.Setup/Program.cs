using System;
using System.Linq;

namespace Justified.SpecificationReflow.AutoCAD.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        global::System.Windows.Forms.Application.EnableVisualStyles();
        global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        var uninstall = args.Any(arg => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase));
        try
        {
            global::System.Windows.Forms.Application.Run(new SetupWizard(uninstall));
        }
        catch (System.Exception error)
        {
            global::System.Windows.Forms.MessageBox.Show(
                "安装程序启动失败：" + error.Message + "\n\n请把本窗口截图发给程序维护方。",
                "Word 设计说明落图工具",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }
}
