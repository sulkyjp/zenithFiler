using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler
{
    /// <summary>インデックス検索スコープ Popup の1行を表す ViewModel。</summary>
    public partial class IndexSearchScopeItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private bool _isLocked;

        [ObservableProperty]
        private int _documentCount;
    }
}
