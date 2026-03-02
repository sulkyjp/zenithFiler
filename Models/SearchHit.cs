namespace ZenithFiler
{
    /// <summary>
    /// インデックス検索の1件分のヒット情報。ファイルシステムアクセスなしで UI に表示するために使用。
    /// </summary>
    public record SearchHit(string Path, string Name, long ModifiedTicks, long Size, bool IsDirectory);
}
