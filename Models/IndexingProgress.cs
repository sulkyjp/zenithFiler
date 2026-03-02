namespace ZenithFiler
{
    /// <summary>インデックス作成の進捗情報。</summary>
    /// <param name="ProcessedCount">処理済みファイル数</param>
    /// <param name="RootPath">インデックス作成中のルートパス</param>
    /// <param name="CurrentFolder">現在スキャン中のサブフォルダ名（1秒スロットル）</param>
    public record IndexingProgress(int ProcessedCount, string RootPath, string? CurrentFolder = null);
}
