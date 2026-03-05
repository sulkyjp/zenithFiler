using System;
using SQLite;

namespace ZenithFiler
{
    /// <summary>
    /// 有償機能の使用回数を記録するレコードです。
    /// </summary>
    [Table("UsageRecords")]
    public class UsageRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string FeatureKey { get; set; } = "";

        public string UsedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }
}
