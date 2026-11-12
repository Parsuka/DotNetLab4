using System;
using System.Windows.Forms;

namespace ProcessManagerApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
#if NETCOREAPP
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
