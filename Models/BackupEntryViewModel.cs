using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenithFiler.Services;

namespace ZenithFiler.Models
{
    /// <summary>BackupEntry（immutable record）のラッパー ViewModel。編集状態を保持する。</summary>
    public partial class BackupEntryViewModel : ObservableObject
    {
        public BackupEntry Source { get; }
        public string JsonPath => Source.JsonPath;
        public string DescPath => Source.DescPath;
        public System.DateTime Timestamp => Source.Timestamp;

        [ObservableProperty] private bool _isLocked;
        [ObservableProperty] private string _summary;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _editBuffer = string.Empty;

        public BackupEntryViewModel(BackupEntry source)
        {
            Source = source;
            _isLocked = source.IsLocked;
            _summary = source.Summary;
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (IsLocked) return;
            EditBuffer = Summary;
            IsEditing = true;
        }

        [RelayCommand]
        private void CommitEdit()
        {
            SettingsBackupService.UpdateSummary(DescPath, EditBuffer);
            Summary = EditBuffer;
            IsEditing = false;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
        }
    }

    /// <summary>ページネーションバーの1アイテム。PageNumber == -1 は省略記号「…」を表す。</summary>
    public class BackupPageItem
    {
        public int PageNumber { get; init; }
        public bool IsCurrent { get; init; }
        public bool IsEllipsis => PageNumber < 0;
        public string Display => IsEllipsis ? "…" : PageNumber.ToString();
    }
}
