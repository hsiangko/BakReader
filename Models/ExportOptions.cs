namespace BakReader.Models
{
    /// <summary>
    /// 匯出選項設定
    /// </summary>
    public class ExportOptions
    {
        // 目標 SQL Server 連線
        public string TargetServer { get; set; } = "";
        public string TargetDatabase { get; set; } = "";
        public string TargetUsername { get; set; } = "";
        public string TargetPassword { get; set; } = "";
        public bool UseWindowsAuth { get; set; } = false;
        public int Port { get; set; } = 1433;

        // 匯出選項
        public bool CreateSchema { get; set; } = true;    // 建立 Table 結構
        public bool CopyData { get; set; } = true;         // 複製資料
        public bool DropIfExists { get; set; } = false;    // 若 Table 已存在則先刪除
        public bool CreateDatabaseIfNotExists { get; set; } = true;

        // 要匯出的 Table 清單
        public List<TableInfo> SelectedTables { get; set; } = new();

        public string BuildConnectionString()
        {
            var serverPart = Port != 1433
                ? $"{TargetServer},{Port}"
                : TargetServer;

            if (UseWindowsAuth)
                return $"Server={serverPart};Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=30;";

            return $"Server={serverPart};Database=master;User Id={TargetUsername};Password={TargetPassword};TrustServerCertificate=True;Connection Timeout=30;";
        }

        public string BuildTargetDbConnectionString()
        {
            var serverPart = Port != 1433
                ? $"{TargetServer},{Port}"
                : TargetServer;

            if (UseWindowsAuth)
                return $"Server={serverPart};Database={TargetDatabase};Integrated Security=True;TrustServerCertificate=True;Connection Timeout=30;";

            return $"Server={serverPart};Database={TargetDatabase};User Id={TargetUsername};Password={TargetPassword};TrustServerCertificate=True;Connection Timeout=30;";
        }
    }
}
