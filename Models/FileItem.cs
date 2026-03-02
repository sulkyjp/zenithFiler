using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler
{
    public partial class FileItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private DateTime _lastModified;
        
        [ObservableProperty]
        private string _typeName = string.Empty;

        [ObservableProperty]
        private ImageSource? _icon;

        [ObservableProperty]
        private ImageSource? _thumbnail;

        [ObservableProperty]
        private long? _folderIndexedSize;

        partial void OnFolderIndexedSizeChanged(long? value)
        {
            OnPropertyChanged(nameof(DisplaySize));
            OnPropertyChanged(nameof(IconViewSubText));
        }

        [ObservableProperty]
        private System.IO.FileAttributes _attributes;

        /// <summary>画像ファイルかどうか（拡張子ベース）。アイコンビューでサムネイル表示の対象。</summary>
        public bool IsImageFile => !IsDirectory && ShellIconHelper.IsImageFile(FullPath);

        [ObservableProperty]
        private SourceType _locationType;

        public string LocationIconKind
        {
            get
            {
                return LocationType switch
                {
                    SourceType.Server => "Network",
                    SourceType.Box => "Archive",
                    SourceType.SPO => "Cloud",
                    _ => "HardDrive"
                };
            }
        }

        partial void OnFullPathChanged(string value)
        {
            LocationType = PathHelper.DetermineSourceType(value);
        }

        public string DisplaySize => IsDirectory
            ? (FolderIndexedSize.HasValue ? FormatSize(FolderIndexedSize.Value) : "")
            : FormatSize(Size);

        /// <summary>アイコンビュー用のサブテキスト（フォルダ: 「フォルダ」or「フォルダ • サイズ」、ファイル: 「拡張子 • サイズ」）。</summary>
        public string IconViewSubText => IsDirectory
            ? (FolderIndexedSize.HasValue ? $"フォルダ • {FormatSize(FolderIndexedSize.Value)}" : "フォルダ")
            : $"{System.IO.Path.GetExtension(Name)?.TrimStart('.')?.ToUpperInvariant() ?? "—"} • {DisplaySize}";

        public static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
