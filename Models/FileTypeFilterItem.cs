using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler
{
    /// <summary>検索結果のフィルタ種別。</summary>
    public enum FileTypeFilter
    {
        Folder,
        Excel,
        Word,
        PowerPoint,
        Pdf,
        Text,
        Exe,
        Bat,
        Json,
        Image,
        Other
    }

    /// <summary>検索結果フィルターバー用のチェック可能なフィルタ項目。</summary>
    public partial class FileTypeFilterItem : ObservableObject
    {
        public FileTypeFilter FilterType { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        /// <summary>対象拡張子（小文字、先頭ドット付き）。フォルダ・その他は null。</summary>
        public string[]? Extensions { get; init; }

        [ObservableProperty]
        private bool _isEnabled = true;
    }
}
