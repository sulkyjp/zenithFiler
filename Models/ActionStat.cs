using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace ZenithFiler
{
    [Table("ActionStats")]
    public partial class ActionStat : ObservableObject
    {
        [PrimaryKey]
        public string ActionKey { get; set; } = "";

        [ObservableProperty]
        private int _count;

        public string LastUsedAt { get; set; } = "";
    }
}
