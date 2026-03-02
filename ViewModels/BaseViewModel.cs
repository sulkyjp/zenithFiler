using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler.ViewModels
{
    /// <summary>
    /// アプリ内のすべての ViewModel の基底クラス。
    /// ローディング状態などの共通プロパティを提供します。
    /// </summary>
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _loadingMessage = string.Empty;
    }
}
