using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZenithFiler
{
    public partial class FilePaneViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TabItemViewModel> _tabs = new();

        [ObservableProperty]
        private TabItemViewModel _selectedTab;

        partial void OnSelectedTabChanged(TabItemViewModel? oldValue, TabItemViewModel newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= SelectedTab_PropertyChanged;
                oldValue.IsActive = false;
            }

            if (newValue != null)
            {
                newValue.PropertyChanged += SelectedTab_PropertyChanged;
                newValue.IsActive = IsActive; // ペインのアクティブ状態を同期
                newValue.RefreshIfNeededOnTabFocus();
            }

            NotifyAllPropertiesChanged();
        }

        private void NotifyAllPropertiesChanged()
        {
            OnPropertyChanged(string.Empty);
        }

        private void SelectedTab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TabItemViewModel.CurrentPath):
                    OnPropertyChanged(nameof(CurrentPath));
                    break;
                case nameof(TabItemViewModel.StatusText):
                    OnPropertyChanged(nameof(StatusText));
                    break;
                case nameof(TabItemViewModel.SelectionInfoText):
                    OnPropertyChanged(nameof(SelectionInfoText));
                    break;
                case nameof(TabItemViewModel.IsGroupFoldersFirst):
                    OnPropertyChanged(nameof(IsGroupFoldersFirst));
                    break;
                case nameof(TabItemViewModel.IsAdaptiveColumnsEnabled):
                    OnPropertyChanged(nameof(IsAdaptiveColumnsEnabled));
                    break;
                case nameof(TabItemViewModel.SortProperty):
                    OnPropertyChanged(nameof(SortProperty));
                    break;
                case nameof(TabItemViewModel.SortDirection):
                    OnPropertyChanged(nameof(SortDirection));
                    break;
                case nameof(TabItemViewModel.FileViewMode):
                    OnPropertyChanged(nameof(FileViewMode));
                    break;
                case nameof(TabItemViewModel.IsPathEditMode):
                    OnPropertyChanged(nameof(IsPathEditMode));
                    break;
            }
        }

        // プロキシプロパティ
        public bool IsGroupFoldersFirst
        {
            get => SelectedTab?.IsGroupFoldersFirst ?? true;
            set
            {
                if (SelectedTab != null) SelectedTab.IsGroupFoldersFirst = value;
                OnPropertyChanged(nameof(IsGroupFoldersFirst));
            }
        }

        public bool IsAdaptiveColumnsEnabled
        {
            get => SelectedTab?.IsAdaptiveColumnsEnabled ?? true;
            set
            {
                if (SelectedTab != null) SelectedTab.IsAdaptiveColumnsEnabled = value;
                OnPropertyChanged(nameof(IsAdaptiveColumnsEnabled));
            }
        }

        public string SortProperty
        {
            get => SelectedTab?.SortProperty ?? "Name";
            set
            {
                if (SelectedTab != null) SelectedTab.SortProperty = value;
                OnPropertyChanged(nameof(SortProperty));
            }
        }

        public ListSortDirection SortDirection
        {
            get => SelectedTab?.SortDirection ?? ListSortDirection.Ascending;
            set
            {
                if (SelectedTab != null) SelectedTab.SortDirection = value;
                OnPropertyChanged(nameof(SortDirection));
            }
        }

        public FileViewMode FileViewMode
        {
            get => SelectedTab?.FileViewMode ?? FileViewMode.Details;
            set
            {
                if (SelectedTab != null) SelectedTab.FileViewMode = value;
                OnPropertyChanged(nameof(FileViewMode));
            }
        }

        public string StatusText => SelectedTab?.StatusText ?? string.Empty;
        public string SelectionInfoText => SelectedTab?.SelectionInfoText ?? string.Empty;

        [ObservableProperty]
        private bool _isActive;

        partial void OnIsActiveChanged(bool value)
        {
            if (SelectedTab != null)
            {
                SelectedTab.IsActive = value;
            }
        }

        [ObservableProperty]
        private bool _isTabListPopupOpen;

        [RelayCommand]
        private void ToggleTabListPopup()
        {
            IsTabListPopupOpen = !IsTabListPopupOpen;
        }

        [RelayCommand]
        private void SelectTabFromList(TabItemViewModel? tab)
        {
            if (tab != null && Tabs.Contains(tab))
            {
                SelectedTab = tab;
                IsTabListPopupOpen = false;
            }
        }

        public string CurrentPath => SelectedTab?.CurrentPath ?? string.Empty;

        /// <summary>Aペインは "A"、Bペインは "B"。通知メッセージで「どのペインに表示したか」を表示するために使用。</summary>
        public string PaneLabel { get; }

        public bool IsPathEditMode
        {
            get => SelectedTab?.IsPathEditMode ?? false;
            set
            {
                if (SelectedTab != null) SelectedTab.IsPathEditMode = value;
                OnPropertyChanged(nameof(IsPathEditMode));
            }
        }

        public FilePaneViewModel(string initialPath, string paneLabel = "A")
        {
            PaneLabel = paneLabel;
            // 初期タブの作成
            var initialTab = new TabItemViewModel(initialPath);
            initialTab.CloseTabCommand = new RelayCommand(() => CloseTab(initialTab));
            initialTab.ParentPane = this;
            Tabs.Add(initialTab);
            SelectedTab = initialTab;
        }

        [RelayCommand]
        private void Navigate(string path) => SelectedTab?.NavigateCommand.Execute(path);

        [RelayCommand]
        private void NavigateToHome()
        {
            if (SelectedTab == null) return;

            var main = System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
            var homePath = main?.AppSettings?.GetHomePathForPane(PaneLabel) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(homePath))
            {
                bool useDownloads = string.Equals(PaneLabel, "B", StringComparison.OrdinalIgnoreCase);
                homePath = useDownloads ? PathHelper.GetDownloadsPath() : PathHelper.GetInitialPath(Environment.SpecialFolder.Desktop);
            }
            else
            {
                var physical = PathHelper.GetPhysicalPath(homePath);
                if (!Directory.Exists(physical))
                {
                    bool useDownloads = string.Equals(PaneLabel, "B", StringComparison.OrdinalIgnoreCase);
                    homePath = useDownloads ? PathHelper.GetDownloadsPath() : PathHelper.GetInitialPath(Environment.SpecialFolder.Desktop);
                }
                else
                {
                    homePath = physical;
                }
            }

            SelectedTab.NavigateCommand.Execute(homePath);
            App.Notification.Notify("ホームに移動しました", $"ホーム: {Path.GetFileName(homePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}");
        }

        public async Task NavigateAsync(string path, bool saveToHistory = true)
        {
            if (SelectedTab != null)
            {
                await SelectedTab.NavigateAsync(path, saveToHistory);
            }
        }

        public void OpenSearchTab(string path, string query, bool isIndexSearch)
        {
            var newTab = new TabItemViewModel(path);
            newTab.CloseTabCommand = new RelayCommand(() => CloseTab(newTab));
            newTab.ParentPane = this;
            newTab.IsSearchResultTab = true;
            newTab.IsIndexSearchMode = isIndexSearch;
            newTab.FileViewMode = SelectedTab?.FileViewMode ?? FileViewMode.Details;
            // 検索結果は更新日時降順で表示
            newTab.SortProperty = "LastModified";
            newTab.SortDirection = ListSortDirection.Descending;

            Tabs.Add(newTab);
            SelectedTab = newTab;

            newTab.SuppressSearchOnTextChanged = true;
            newTab.SearchText = query;
            newTab.SuppressSearchOnTextChanged = false;
            _ = newTab.ExecuteSearch(query);
        }

        [RelayCommand]
        private void AddTab()
        {
            // 新規タブ作成時はアドレスを空にし、編集モードにする
            AddTabWithPathInternal(string.Empty, true);
            App.Notification.Notify("新しいタブを追加しました", "タブを追加（パス未指定）");
        }

        [RelayCommand]
        private void AddTabWithPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            AddTabWithPathInternal(path, IsPathEditMode);
            App.Notification.Notify("タブを追加しました", $"タブを追加: {path}");
        }

        private void AddTabWithPathInternal(string path, bool isEditMode)
        {
            _ = App.Stats.RecordAsync("Tab.Open");
            var newTab = new TabItemViewModel(path);
            newTab.CloseTabCommand = new RelayCommand(() => CloseTab(newTab));
            newTab.ParentPane = this;
            newTab.IsPathEditMode = isEditMode;
            newTab.FileViewMode = SelectedTab?.FileViewMode ?? FileViewMode.Details;

            Tabs.Add(newTab);
            SelectedTab = newTab;

            if (!string.IsNullOrEmpty(path))
            {
                // タブ作成時は自動でロードされないため、明示的にロードする
                _ = newTab.LoadDirectoryAsync();
            }
        }

        /// <summary>タブが0件になったときに呼ぶ。1つだけデフォルトタブを追加する（タブ移動で他ペインに移した後など）。</summary>
        public void EnsureAtLeastOneTab()
        {
            if (Tabs.Count > 0) return;
            AddTabWithPathInternal(string.Empty, true);
        }

        [RelayCommand]
        internal void CloseTab(TabItemViewModel? tab)
        {
            var tabToClose = tab ?? SelectedTab;
            if (tabToClose == null || tabToClose.IsLocked || Tabs.Count <= 1) return;
            _ = App.Stats.RecordAsync("Tab.Close");

            bool wasSearchResultTab = tabToClose.IsSearchResultTab;
            string title = tabToClose.TabTitle;
            string path = tabToClose.CurrentPath;
            int index = Tabs.IndexOf(tabToClose);
            Tabs.Remove(tabToClose);
            tabToClose.Dispose();

            if (SelectedTab == tabToClose || SelectedTab == null)
            {
                SelectedTab = index < Tabs.Count ? Tabs[index] : Tabs[Tabs.Count - 1];
            }

            // 検索結果タブを閉じた場合、他の検索結果タブが残っていなければペイン数を復元
            if (wasSearchResultTab)
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
                main?.RestorePaneCountAfterSearchIfNeeded(tabToClose);
            }

            App.Notification.Notify("タブを閉じました", $"タブを閉じる: {title} ({path})");
        }

        [RelayCommand]
        private void NextTab()
        {
            if (Tabs.Count <= 1) return;

            int index = Tabs.IndexOf(SelectedTab);
            int nextIndex = (index + 1) % Tabs.Count;
            SelectedTab = Tabs[nextIndex];
            App.Notification.Notify("次のタブに切り替えました", $"タブ切り替え: {SelectedTab.TabTitle}");
        }

        public async Task RestoreTabsAsync(PaneSettings settings)
        {
            var paths = settings.TabPaths;
            var lockStates = settings.TabLockStates;
            var selectedIndex = settings.SelectedTabIndex;
            var isPathEditMode = settings.IsPathEditMode;

            if (paths is not { Count: > 0 }) return;

            // 特殊フォルダの物理パスは事前キャッシュ済みであること（MainViewModel.InitializeAsync で実行）
            PathHelper.EnsureSpecialFoldersCached();

            // 既存のタブを破棄
            foreach (var tab in Tabs.ToList())
            {
                tab.Dispose();
            }
            Tabs.Clear();

            // タブを復元（特殊フォルダは確実に解決し、Cドライブルートにフォールバックしない）
            var viewMode = settings.FileViewMode;
            for (int i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var isLocked = lockStates != null && i < lockStates.Count && lockStates[i];
                var validPath = await ResolvePathForTabRestoreAsync(path, i == 0);
                var newTab = new TabItemViewModel(validPath, viewMode);
                newTab.CloseTabCommand = new RelayCommand(() => CloseTab(newTab));
                newTab.ParentPane = this;
                newTab.IsLocked = isLocked;
                Tabs.Add(newTab);
            }

            // 選択状態の復元
            if (selectedIndex >= 0 && selectedIndex < Tabs.Count)
            {
                SelectedTab = Tabs[selectedIndex];
            }
            else if (Tabs.Count > 0)
            {
                SelectedTab = Tabs[0];
            }

            // パス編集モードの適用
            if (SelectedTab != null)
            {
                SelectedTab.IsPathEditMode = isPathEditMode;
            }

            // アクティブなタブの内容をロード（ツリー初期化を待たずに正しいパスで表示を確定）
            if (SelectedTab != null)
            {
                await SelectedTab.NavigateAsync(SelectedTab.CurrentPath, false);
            }
        }

        /// <summary>
        /// タブ復元用にパスを解決する。空・無効・ドライブルートのみの場合は
        /// 特殊フォルダ（Aペイン先頭=デスクトップ、Bペイン先頭=ダウンロード）にフォールバックし、C:\に化けないようにする。
        /// ネットワークパスはタイムアウト付きで存在確認し、応答がない場合はフォールバックする。
        /// </summary>
        private async Task<string> ResolvePathForTabRestoreAsync(string? path, bool isFirstTab)
        {
            bool useDownloads = isFirstTab && string.Equals(PaneLabel, "B", StringComparison.OrdinalIgnoreCase);
            string fallback = useDownloads
                ? PathHelper.GetDownloadsPath()
                : PathHelper.GetInitialPath(Environment.SpecialFolder.Desktop);

            if (string.IsNullOrWhiteSpace(path)) return fallback;
            string resolved = PathHelper.GetPhysicalPath(path);
            if (string.IsNullOrWhiteSpace(resolved)) return fallback;

            // PC / UNC ルートは仮想パスなので存在チェック不要
            if (PathHelper.IsPCPath(resolved) || PathHelper.IsUncRoot(resolved))
                return resolved;

            if (PathHelper.IsDriveRootOnly(resolved) || !await PathHelper.DirectoryExistsSafeAsync(resolved))
                return fallback;

            return resolved;
        }
    }
}
