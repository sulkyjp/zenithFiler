using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ZenithFiler
{
    public partial class FavoriteItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        /// <summary>階層レベル（0 がルート）。お気に入りビューでの視覚的な区切り用。</summary>
        [ObservableProperty]
        private int _level;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPathMissing))]
        private string? _path;

        /// <summary>概要説明。似た名前のフォルダを識別しやすくするための任意のメモ。</summary>
        [ObservableProperty]
        private string? _description;

        [ObservableProperty]
        private ImageSource? _icon;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        /// <summary>登録成功時のハイライト演出用フラグ。true で SuccessHighlight アニメーションを1回発火。</summary>
        [ObservableProperty]
        private bool _isSuccessHighlighted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LocationIconKind))]
        private SourceType _sourceType = SourceType.Local;

        public string LocationIconKind => SourceType switch
        {
            SourceType.Server => "Network",
            SourceType.Box => "Archive",
            SourceType.SPO => "Cloud",
            _ => "HardDrive"
        };

        public ObservableCollection<FavoriteItem> Children { get; } = new();

        public bool IsDirectory => Path == null || System.IO.Directory.Exists(Path);

        public bool IsContainer => Path == null; // 純粋なコンテナ（仮想フォルダ）かどうか

        public bool IsPhysicalFolder => !IsContainer && IsDirectory;

        public bool IsFile => !IsContainer && !IsDirectory;

        /// <summary>パスが設定されているが実在しない（リンク切れ）</summary>
        public bool IsPathMissing => Path != null && !System.IO.Directory.Exists(Path) && !System.IO.File.Exists(Path);
    }
}
