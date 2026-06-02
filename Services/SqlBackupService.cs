using BakReader.Models;
using Microsoft.Data.SqlClient;
using System.Text;

namespace BakReader.Services
{
    /// <summary>
    /// SQL Server 備份檔案讀取服務
    /// 負責從 .bak 還原到臨時 LocalDB，並取得 Table 清單
    /// </summary>
    public class SqlBackupService : IDisposable
    {
        private readonly string _connectionString;
        private string? _tempDbName;
        private bool _disposed;

        public SqlBackupService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 讀取備份檔案的基本資訊（不需要還原）
        /// </summary>
        public async Task<BackupInfo> ReadBackupInfoAsync(string bakPath)
        {
            var info = new BackupInfo { FilePath = bakPath };

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // RESTORE HEADERONLY 取得備份摘要
            var headerSql = $"RESTORE HEADERONLY FROM DISK = N'{EscapeSqlString(bakPath)}'";
            using (var cmd = new SqlCommand(headerSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    info.DatabaseName = reader["DatabaseName"]?.ToString() ?? "";
                    info.ServerName = reader["ServerName"]?.ToString() ?? "";
                    if (reader["BackupFinishDate"] != DBNull.Value)
                        info.BackupFinishDate = (DateTime)reader["BackupFinishDate"];
                    info.BackupType = reader["BackupType"]?.ToString() ?? "";

                    // 嘗試取得壓縮備份大小
                    try
                    {
                        if (reader["CompressedBackupSize"] != DBNull.Value)
                            info.BackupSizeBytes = Convert.ToInt64(reader["CompressedBackupSize"]);
                        else if (reader["BackupSize"] != DBNull.Value)
                            info.BackupSizeBytes = Convert.ToInt64(reader["BackupSize"]);
                    }
                    catch { }

                    try
                    {
                        if (reader["IsEncrypted"] != DBNull.Value)
                            info.IsEncrypted = Convert.ToBoolean(reader["IsEncrypted"]);
                    }
                    catch { }
                }
            }

            // 若大小為 0，從檔案系統取得
            if (info.BackupSizeBytes == 0 && File.Exists(bakPath))
                info.BackupSizeBytes = new FileInfo(bakPath).Length;

            return info;
        }

        /// <summary>
        /// 讀取備份檔案中的邏輯檔案清單
        /// </summary>
        public async Task<List<BackupFileEntry>> ReadFileListAsync(string bakPath)
        {
            var files = new List<BackupFileEntry>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = $"RESTORE FILELISTONLY FROM DISK = N'{EscapeSqlString(bakPath)}'";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                files.Add(new BackupFileEntry
                {
                    LogicalName = reader["LogicalName"]?.ToString() ?? "",
                    PhysicalName = reader["PhysicalName"]?.ToString() ?? "",
                    Type = reader["Type"]?.ToString() ?? ""
                });
            }

            return files;
        }

        /// <summary>
        /// 將備份還原到臨時 LocalDB 資料庫
        /// </summary>
        /// <param name="bakPath">備份檔案路徑</param>
        /// <param name="progress">進度回報</param>
        public async Task RestoreToTempAsync(string bakPath, IProgress<string>? progress = null)
        {
            var guid = Guid.NewGuid().ToString("N")[..8];
            _tempDbName = $"BakReader_Temp_{guid}";

            progress?.Report("讀取備份檔案清單...");
            var fileList = await ReadFileListAsync(bakPath);

            var dataPath = LocalDbService.GetDefaultDataPath();
            progress?.Report($"準備還原到臨時資料庫：{_tempDbName}");

            // 建立 MOVE 子句，將所有檔案重新導向到 temp 路徑
            var moveClauses = new StringBuilder();
            int fileIndex = 0;
            foreach (var f in fileList)
            {
                var ext = f.Type == "L" ? "_log.ldf" : $"_data{(fileIndex > 0 ? fileIndex.ToString() : "")}.mdf";
                var destPath = Path.Combine(dataPath, _tempDbName + ext);
                moveClauses.Append($",\n  MOVE N'{EscapeSqlString(f.LogicalName)}' TO N'{EscapeSqlString(destPath)}'");
                if (f.Type != "L") fileIndex++;
            }

            var restoreSql = $@"
RESTORE DATABASE [{_tempDbName}]
FROM DISK = N'{EscapeSqlString(bakPath)}'
WITH
  NOUNLOAD,
  REPLACE,
  RECOVERY{moveClauses}";

            using var conn = new SqlConnection(_connectionString);
            conn.FireInfoMessageEventOnUserErrors = true;
            bool upgradeNotified = false;
            conn.InfoMessage += (_, e) =>
            {
                var msg = e.Message ?? "";
                if (msg.Contains("running the upgrade step") || msg.Contains("upgrade step"))
                {
                    // 僅顯示一次友善訊息，不洗版 Log
                    if (!upgradeNotified)
                    {
                        upgradeNotified = true;
                        progress?.Report("正在轉換資料庫版本格式，請稍候...");
                    }
                }
                else if (msg.Contains("RESTORE DATABASE") || msg.Contains("processed"))
                {
                    progress?.Report("✓ 備份資料還原完成");
                }
                else if (!string.IsNullOrWhiteSpace(msg))
                {
                    progress?.Report(msg);
                }
            };

            await conn.OpenAsync();

            // 設定較長的 timeout（大型備份可能需要幾分鐘）
            using var cmd = new SqlCommand(restoreSql, conn)
            {
                CommandTimeout = 600  // 10 分鐘
            };

            progress?.Report("正在還原資料庫，請稍候...");
            await cmd.ExecuteNonQueryAsync();
            progress?.Report($"資料庫還原完成：{_tempDbName}");
        }

        /// <summary>
        /// 取得臨時資料庫中所有資料表的清單
        /// </summary>
        public async Task<List<TableInfo>> GetTableListAsync()
        {
            if (_tempDbName == null)
                throw new InvalidOperationException("請先呼叫 RestoreToTempAsync()");

            var tables = new List<TableInfo>();
            var connStr = _connectionString.Replace(
                "Integrated Security=True",
                "Integrated Security=True") + $";Initial Catalog={_tempDbName};";
            // 修正連線字串加上資料庫
            connStr = AddDatabaseToConnectionString(_connectionString, _tempDbName);

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var sql = @"
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    ISNULL(SUM(p.rows), 0) AS TotalRows
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
LEFT JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name
ORDER BY s.name, t.name";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(new TableInfo
                {
                    Schema = reader["SchemaName"]?.ToString() ?? "dbo",
                    TableName = reader["TableName"]?.ToString() ?? "",
                    RowCount = Convert.ToInt64(reader["TotalRows"]),
                    IsSelected = false
                });
            }

            return tables;
        }

        /// <summary>
        /// 取得臨時資料庫名稱（供 ExportService 使用）
        /// </summary>
        public string? TempDbName => _tempDbName;

        /// <summary>
        /// 取得臨時資料庫的連線字串
        /// </summary>
        public string GetTempDbConnectionString()
        {
            if (_tempDbName == null) throw new InvalidOperationException("尚未還原資料庫");
            return AddDatabaseToConnectionString(_connectionString, _tempDbName);
        }

        /// <summary>
        /// 刪除臨時資料庫並清除檔案
        /// </summary>
        public async Task DropTempDatabaseAsync()
        {
            if (_tempDbName == null) return;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 先強制中斷所有連線，再刪除
                var sql = $@"
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{_tempDbName}')
BEGIN
    ALTER DATABASE [{_tempDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{_tempDbName}];
END";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* 刪除失敗不影響主流程 */ }
            finally
            {
                _tempDbName = null;
            }
        }

        private static string AddDatabaseToConnectionString(string baseConnStr, string dbName)
        {
            var builder = new SqlConnectionStringBuilder(baseConnStr)
            {
                InitialCatalog = dbName
            };
            return builder.ConnectionString;
        }

        private static string EscapeSqlString(string value)
            => value.Replace("'", "''");

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // 非同步清理
                Task.Run(async () => await DropTempDatabaseAsync());
            }
        }
    }
}
