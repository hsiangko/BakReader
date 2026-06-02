using BakReader.Forms;

namespace BakReader
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 高 DPI 支援
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 全域未捕捉例外處理
            Application.ThreadException += (_, e) =>
            {
                MessageBox.Show(
                    $"發生未預期的錯誤：\n\n{e.Exception.Message}",
                    "錯誤",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"嚴重錯誤：\n\n{ex?.Message}",
                    "嚴重錯誤",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }
}
