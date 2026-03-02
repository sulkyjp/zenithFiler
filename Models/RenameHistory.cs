using System;
using SQLite;

namespace ZenithFiler
{
    /// <summary>
    /// リネーム履歴を保持するクラスです。
    /// 入力した名前を主キーとし、最終使用日時で並べ替えます。
    /// </summary>
    [Table("RenameHistory")]
    public class RenameHistory
    {
        /// <summary>
        /// リネーム時に入力した名前（主キー）。
        /// </summary>
        [PrimaryKey]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 最終使用日時。
        /// </summary>
        public DateTime LastUsed { get; set; }
    }
}
