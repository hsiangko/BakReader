using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace BakReader.Services
{
    /// <summary>
    /// SQL Server LocalDB 管理服務
    /// 負責偵測、啟動 LocalDB 並提供連線字串
    /// </summary>
    public static class LocalDbService
    {
        private const string LocalDbInstanceName = "MSSQLLocalDB";
        public const string LocalDbConnectionString =
            @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=60;";

        /// <summary>
        /// 檢查 LocalDB 是否已安裝
        /// </summary>
        public static bool IsLocalDbInstalled()
        {
            try
            {
                // 方法1：嘗試執行 sqllocaldb.exe
                var result = RunCommand("sqllocaldb", "info");
                return result.ExitCode == 0;
            }
            catch
            {
                // 方法2：檢查登錄檔
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions");
                    return key != null && key.GetSubKeyNames().Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 啟動 LocalDB 實例
        /// </summary>
        public static async Task<bool> StartInstanceAsync()
        {
            try
            {
                var result = RunCommand("sqllocaldb", $"start {LocalDbInstanceName}");
                if (result.ExitCode == 0) return true;

                // 若實例不存在，嘗試建立
                RunCommand("sqllocaldb", $"create {LocalDbInstanceName}");
                result = RunCommand("sqllocaldb", $"start {LocalDbInstanceName}");
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 測試 LocalDB 連線是否可用
        /// </summary>
        public static async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                using var conn = new SqlConnection(LocalDbConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT @@VERSION", conn);
                var version = await cmd.ExecuteScalarAsync() as string ?? "";
                return (true, $"連線成功\n{version[..Math.Min(80, version.Length)]}...");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 取得 LocalDB 資料檔案預設存放路徑
        /// </summary>
        public static string GetDefaultDataPath()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BakReader", "TempDbs");
            Directory.CreateDirectory(path);
            return path;
        }

        private static (int ExitCode, string Output) RunCommand(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            return (proc.ExitCode, output);
        }
    }
}
