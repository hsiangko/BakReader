namespace BakReader.Models
{
    /// <summary>
    /// 資料表資訊
    /// </summary>
    public class TableInfo
    {
        public string Schema { get; set; } = "dbo";
        public string TableName { get; set; } = "";
        public long RowCount { get; set; }
        public bool IsSelected { get; set; } = false;

        public string FullName => $"[{Schema}].[{TableName}]";
        public string DisplayName => Schema == "dbo" ? TableName : $"{Schema}.{TableName}";
        public string RowCountDisplay => RowCount.ToString("N0");
    }
}
