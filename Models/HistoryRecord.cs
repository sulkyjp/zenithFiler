using System;
using SQLite;

namespace ZenithFiler
{
    /// <summary>
    /// フォルダ参照履歴を保持するクラスです。
    /// </summary>
    public class HistoryRecord
    {
        /// <summary>
        /// フォルダの物理パスを主キーとします。
        /// </summary>
        [PrimaryKey]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 最終アクセス日時。
        /// </summary>
        public DateTime LastAccessed { get; set; }

        /// <summary>
        /// 保存場所のタイプ。
        /// </summary>
        public SourceType SourceType { get; set; }

        /// <summary>
        /// アクセス回数。
        /// </summary>
        public int AccessCount { get; set; }
    }
}
