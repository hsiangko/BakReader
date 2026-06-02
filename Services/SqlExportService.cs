using BakReader.Models;
using Microsoft.Data.SqlClient;
using System.Text;

namespace BakReader.Services
{
    /// <summary>
    /// 資料匯出服務：從臨時 LocalDB 複製 Schema + 資料到目標 SQL Server
    /// </summary>
    public class SqlExportService
    {
        public record ExportProgress(int Current, int Total, string TableName, string Status);

        /// <summary>
        /// 執行完整匯出流程
        /// </summary>
        public async Task ExportAsync(
            string sourceTempConnStr,
            ExportOptions options,
            IProgress<ExportProgress>? progress = null,
            CancellationToken ct = default)
        {
            // 1. 建立目標資料庫（若需要）
            if (options.CreateDatabaseIfNotExists)
                await EnsureTargetDatabaseExistsAsync(options, ct);

            var targetConnStr = options.BuildTargetDbConnectionString();
            var tables = options.SelectedTables;
            int total = tables.Count;

            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var table = tables[i];
                progress?.Report(new ExportProgress(i + 1, total, table.DisplayName, "準備中..."));

                try
                {
                    // 2. 建立 Table Schema
                    if (options.CreateSchema)
                    {
                        progress?.Report(new ExportProgress(i + 1, total, table.DisplayName, "建立資料表結構..."));
                        await CreateTableSchemaAsync(sourceTempConnStr, targetConnStr, table, options.DropIfExists, ct);
                    }

                    // 3. 複製資料
                    if (options.CopyData)
                    {
                        progress?.Report(new ExportProgress(i + 1, total, table.DisplayName, "複製資料..."));
                        await CopyTableDataAsync(sourceTempConnStr, targetConnStr, table, ct);
                    }

                    progress?.Report(new ExportProgress(i + 1, total, table.DisplayName, "✓ 完成"));
                }
                catch (Exception ex)
                {
                    progress?.Report(new ExportProgress(i + 1, total, table.DisplayName, $"✗ 錯誤：{ex.Message}"));
                    throw; // 往上拋讓 UI 處理
                }
            }
        }

        /// <summary>
        /// 確保目標資料庫存在，不存在則建立
        /// </summary>
        private async Task EnsureTargetDatabaseExistsAsync(ExportOptions options, CancellationToken ct)
        {
            var masterConnStr = options.BuildConnectionString(); // 連到 master
            using var conn = new SqlConnection(masterConnStr);
            await conn.OpenAsync(ct);

            var dbName = options.TargetDatabase.Replace("'", "''").Replace("]", "]]");
            var sql = $@"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{dbName.Replace("'", "''")}')
BEGIN
    CREATE DATABASE [{dbName}]
END";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// 在目標建立與來源相同的 Table Schema
        /// </summary>
        private async Task CreateTableSchemaAsync(
            string sourceConnStr, string targetConnStr,
            TableInfo table, bool dropIfExists, CancellationToken ct)
        {
            var script = await GenerateCreateScriptAsync(sourceConnStr, table, ct);

            using var targetConn = new SqlConnection(targetConnStr);
            await targetConn.OpenAsync(ct);

            if (dropIfExists)
            {
                var dropSql = $@"
IF OBJECT_ID(N'{table.FullName.Replace("'", "''")}', 'U') IS NOT NULL
    DROP TABLE {table.FullName}";
                using var dropCmd = new SqlCommand(dropSql, targetConn) { CommandTimeout = 60 };
                await dropCmd.ExecuteNonQueryAsync(ct);
            }

            using var createCmd = new SqlCommand(script, targetConn) { CommandTimeout = 120 };
            await createCmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// 產生 CREATE TABLE 腳本（不依賴 SMO，直接從 system catalog 產生）
        /// </summary>
        private async Task<string> GenerateCreateScriptAsync(
            string sourceConnStr, TableInfo table, CancellationToken ct)
        {
            using var conn = new SqlConnection(sourceConnStr);
            await conn.OpenAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine($"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{table.FullName.Replace("'", "''")}') AND type = 'U')");
            sb.AppendLine("BEGIN");

            // 取得所有欄位資訊
            var colsSql = @"
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.DATETIME_PRECISION,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY,
    IDENT_SEED(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AS IDENT_SEED,
    IDENT_INCR(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AS IDENT_INCR
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @tableName
ORDER BY c.ORDINAL_POSITION";

            var columns = new List<string>();
            bool hasIdentity = false;

            using (var cmd = new SqlCommand(colsSql, conn))
            {
                cmd.Parameters.AddWithValue("@schema", table.Schema);
                cmd.Parameters.AddWithValue("@tableName", table.TableName);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var colName = reader["COLUMN_NAME"].ToString()!;
                    var dataType = reader["DATA_TYPE"].ToString()!;
                    var isNullable = reader["IS_NULLABLE"].ToString() == "YES";
                    var isIdentity = Convert.ToInt32(reader["IS_IDENTITY"]) == 1;
                    if (isIdentity) hasIdentity = true;

                    var typeStr = BuildColumnType(dataType,
                        reader["CHARACTER_MAXIMUM_LENGTH"],
                        reader["NUMERIC_PRECISION"],
                        reader["NUMERIC_SCALE"],
                        reader["DATETIME_PRECISION"]);

                    var identityStr = isIdentity
                        ? $" IDENTITY({reader["IDENT_SEED"]},{reader["IDENT_INCR"]})"
                        : "";
                    var nullStr = isNullable ? " NULL" : " NOT NULL";

                    var defaultStr = "";
                    if (reader["COLUMN_DEFAULT"] != DBNull.Value)
                    {
                        var defVal = reader["COLUMN_DEFAULT"].ToString();
                        if (!string.IsNullOrWhiteSpace(defVal))
                            defaultStr = $" DEFAULT {defVal}";
                    }

                    columns.Add($"    [{colName}] {typeStr}{identityStr}{defaultStr}{nullStr}");
                }
            }

            // 取得主鍵
            var pkSql = @"
SELECT kc.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc
    ON tc.CONSTRAINT_NAME = kc.CONSTRAINT_NAME
    AND tc.TABLE_SCHEMA = kc.TABLE_SCHEMA
WHERE tc.TABLE_SCHEMA = @schema
  AND tc.TABLE_NAME = @tableName
  AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY kc.ORDINAL_POSITION";

            var pkColumns = new List<string>();
            using (var cmd = new SqlCommand(pkSql, conn))
            {
                cmd.Parameters.AddWithValue("@schema", table.Schema);
                cmd.Parameters.AddWithValue("@tableName", table.TableName);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    pkColumns.Add($"[{reader["COLUMN_NAME"]}]");
            }

            sb.AppendLine($"CREATE TABLE {table.FullName} (");
            sb.AppendLine(string.Join(",\n", columns));

            if (pkColumns.Count > 0)
            {
                var pkName = $"PK_{table.TableName}";
                sb.AppendLine($",    CONSTRAINT [{pkName}] PRIMARY KEY ({string.Join(", ", pkColumns)})");
            }

            sb.AppendLine(")");
            sb.AppendLine("END");

            return sb.ToString();
        }

        private static string BuildColumnType(string dataType, object maxLen, object precision, object scale, object dtPrecision)
        {
            return dataType.ToLower() switch
            {
                "nvarchar" or "varchar" or "char" or "nchar" =>
                    maxLen != DBNull.Value && Convert.ToInt32(maxLen) == -1
                        ? $"{dataType}(MAX)"
                        : maxLen != DBNull.Value
                            ? $"{dataType}({maxLen})"
                            : dataType,
                "decimal" or "numeric" =>
                    precision != DBNull.Value && scale != DBNull.Value
                        ? $"{dataType}({precision},{scale})"
                        : dataType,
                "datetime2" or "time" or "datetimeoffset" =>
                    dtPrecision != DBNull.Value
                        ? $"{dataType}({dtPrecision})"
                        : dataType,
                _ => dataType
            };
        }

        /// <summary>
        /// 使用 SqlBulkCopy 高速複製資料
        /// </summary>
        private async Task CopyTableDataAsync(
            string sourceConnStr, string targetConnStr,
            TableInfo table, CancellationToken ct)
        {
            using var sourceConn = new SqlConnection(sourceConnStr);
            await sourceConn.OpenAsync(ct);

            // 先確認目標 Table 是否有 IDENTITY 欄位
            bool hasIdentity = await HasIdentityColumnAsync(sourceConn, table, ct);

            using var selectCmd = new SqlCommand($"SELECT * FROM {table.FullName}", sourceConn)
            {
                CommandTimeout = 3600 // 1 小時
            };
            using var reader = await selectCmd.ExecuteReaderAsync(ct);

            var options = SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.FireTriggers;
            if (hasIdentity) options |= SqlBulkCopyOptions.KeepIdentity;

            using var bulkCopy = new SqlBulkCopy(targetConnStr, options)
            {
                DestinationTableName = table.FullName,
                BatchSize = 1000,
                BulkCopyTimeout = 3600
            };

            await bulkCopy.WriteToServerAsync(reader, ct);
        }

        private async Task<bool> HasIdentityColumnAsync(SqlConnection conn, TableInfo table, CancellationToken ct)
        {
            var sql = @"
SELECT COUNT(*) FROM sys.columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @schema AND t.name = @tableName AND c.is_identity = 1";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@schema", table.Schema);
            cmd.Parameters.AddWithValue("@tableName", table.TableName);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return count > 0;
        }

        /// <summary>
        /// 測試目標 SQL Server 連線
        /// </summary>
        public static async Task<(bool Success, string Message)> TestTargetConnectionAsync(ExportOptions options)
        {
            try
            {
                using var conn = new SqlConnection(options.BuildConnectionString());
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT @@SERVERNAME, @@VERSION", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var serverName = reader[0]?.ToString() ?? "";
                    return (true, $"連線成功！伺服器：{serverName}");
                }
                return (true, "連線成功！");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
