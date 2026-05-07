using System.Windows.Forms;
using volt_design;
using volt_design.Forms;

namespace VoltOfflineRebuild;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Config.InitOfflineDefaults();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
