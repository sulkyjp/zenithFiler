using System;
using SQLite;

namespace ZenithFiler
{
    /// <summary>
    /// 検索履歴を保持するクラスです。
    /// 同一キーワードでも通常検索とインデックス検索を別履歴として保持します。
    /// </summary>
    public class SearchHistoryRecord
    {
        /// <summary>
        /// 複合主キー（Keyword + 区切り + IsIndexSearch の文字列）。
        /// </summary>
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 検索キーワード。
        /// </summary>
        public string Keyword { get; set; } = string.Empty;

        /// <summary>
        /// インデックス検索で実行したかどうか。
        /// </summary>
        public bool IsIndexSearch { get; set; }

        /// <summary>
        /// 最終検索日時。
        /// </summary>
        public DateTime LastSearched { get; set; }

        /// <summary>検索時に適用されていたプリセット名。</summary>
        public string PresetName { get; set; } = string.Empty;

        /// <summary>検索時のサイズフィルタ下限テキスト。</summary>
        public string MinSizeText { get; set; } = string.Empty;

        /// <summary>検索時のサイズフィルタ上限テキスト。</summary>
        public string MaxSizeText { get; set; } = string.Empty;

        /// <summary>検索時の日付フィルタ開始テキスト。</summary>
        public string StartDateText { get; set; } = string.Empty;

        /// <summary>検索時の日付フィルタ終了テキスト。</summary>
        public string EndDateText { get; set; } = string.Empty;

        /// <summary>
        /// Key を組み立てます（保存時に使用）。
        /// </summary>
        public static string BuildKey(string keyword, bool isIndexSearch)
        {
            return keyword + "\u0001" + (isIndexSearch ? "1" : "0");
        }
    }
}
