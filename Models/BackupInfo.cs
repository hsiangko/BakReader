namespace BakReader.Models
{
    /// <summary>
    /// 備份檔案的基本資訊（從 RESTORE HEADERONLY 取得）
    /// </summary>
    public class BackupInfo
    {
        public string DatabaseName { get; set; } = "";
        public string ServerName { get; set; } = "";
        public DateTime BackupFinishDate { get; set; }
        public string BackupType { get; set; } = "";
        public long BackupSizeBytes { get; set; }
        public string FilePath { get; set; } = "";
        public string Compatibility { get; set; } = "";
        public bool IsEncrypted { get; set; }
        public bool IsCompressed { get; set; }

        public string BackupSizeDisplay =>
            BackupSizeBytes < 1024 * 1024
                ? $"{BackupSizeBytes / 1024.0:F1} KB"
                : BackupSizeBytes < 1024 * 1024 * 1024
                    ? $"{BackupSizeBytes / 1024.0 / 1024:F1} MB"
                    : $"{BackupSizeBytes / 1024.0 / 1024 / 1024:F2} GB";

        public string BackupTypeDisplay => BackupType switch
        {
            "D" => "完整備份",
            "I" => "差異備份",
            "L" => "交易記錄備份",
            _ => BackupType
        };
    }

    /// <summary>
    /// 備份檔案中的邏輯檔案清單（從 RESTORE FILELISTONLY 取得）
    /// </summary>
    public class BackupFileEntry
    {
        public string LogicalName { get; set; } = "";
        public string PhysicalName { get; set; } = "";
        public string Type { get; set; } = "";  // D = Data, L = Log
    }
}
