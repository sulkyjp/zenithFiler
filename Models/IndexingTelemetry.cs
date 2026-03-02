namespace ZenithFiler
{
    /// <summary>インデックス作成中のテレメトリスナップショット。ポップアップ表示専用。</summary>
    public class IndexingTelemetry
    {
        public string CurrentScanFolder { get; set; } = string.Empty;
        public int ProcessedCount { get; set; }
        public int TotalDbRecords { get; set; }
        public int ActiveThreads { get; set; }
        public int MaxParallelDegree { get; set; } = 1;
        public TimeSpan Elapsed { get; set; }
        public string RootPath { get; set; } = string.Empty;
    }
}
