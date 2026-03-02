using System;
using SQLite;

namespace ZenithFiler
{
    /// <summary>
    /// リネームダイアログのカスタム定型ボタンを保持するクラスです。
    /// </summary>
    [Table("CustomRenameButtons")]
    public class CustomRenameButton
    {
        /// <summary>自動増分の主キー。</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>ボタンに表示するテキスト。</summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>クリック時に入力欄へ挿入する文字列。</summary>
        public string InsertText { get; set; } = string.Empty;

        /// <summary>作成日時。</summary>
        public DateTime CreatedAt { get; set; }
    }
}
