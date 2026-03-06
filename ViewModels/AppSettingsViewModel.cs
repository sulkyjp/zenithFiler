using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenithFiler.Models;
using ZenithFiler.Services;
using ZenithFiler.Helpers;
using ZenithFiler.Views;

namespace ZenithFiler
{
    /// <summary>検索実行時の表示先・挙動。</summary>
    public enum SearchBehavior
    {
        /// <summary>同一ペインに新規タブを開き、Enter で検索実行。検索結果タブでは入力即検索。</summary>
        SamePaneNewTab,
        /// <summary>現在のタブで検索結果を表示。入力即検索（Enter 不要）。</summary>
        SamePaneCurrentTabInstant,
        /// <summary>反対ペインに新規タブを開き、Enter で検索実行。1ペイン時は 2 ペインへ切替。</summary>
        OtherPaneNewTab
    }

    /// <summary>検索結果一覧でパス列をクリックした際の表示先。</summary>
    public enum SearchResultPathBehavior
    {
        /// <summary>同一タブでそのパスを表示（検索結果ビューから通常一覧に遷移）。</summary>
        SameTab,
        /// <summary>同一ペインに新規タブを開く（従来の挙動）。</summary>
        SamePaneNewTab,
        /// <summary>反対ペインに新規タブを開く。1ペイン時は 2 ペインへ切替。</summary>
        OtherPaneNewTab
    }

    /// <summary>Control Deck の設定カテゴリ。</summary>
    public enum SettingsCategory
    {
        General,
        Search,
        Index,
        Backup,
        Theme,
        Display,
        Shortcut,
        License,
        Statistics,
        About
    }

    /// <summary>テーマの自動選択モード。</summary>
    public enum ThemeRandomizeMode
    {
        /// <summary>自動選択を無効にする（デフォルト）。</summary>
        Disabled,
        /// <summary>登録テーマ全体からランダムに選択。</summary>
        AllThemes,
        /// <summary>現在選択中のカテゴリ内からランダムに選択。</summary>
        CurrentCategory
    }

    /// <summary>ペイン個別テーマ適用のターゲットペイン。</summary>
    public enum PaneTarget { None, Nav, APane, BPane }

    /// <summary>テーマ適用モード（プリセット / 自動選択 / ペイン個別）。</summary>
    public enum ThemeApplyMode { Preset, Random, Pane }

    /// <summary>ファイル一覧の右クリック時に表示するコンテキストメニューの種類。</summary>
    public enum ContextMenuMode
    {
        /// <summary>アプリ独自の軽量メニュー（推奨）。Shift+右クリックでエクスプローラ互換メニューを表示。</summary>
        Zenith,
        /// <summary>エクスプローラ互換のシェルコンテキストメニュー。</summary>
        Explorer
    }

    /// <summary>ナビペイン「アプリ設定」ビューの ViewModel。A/B ペインのホームアドレスなどアプリ全体の設定を行う。</summary>
    public partial class AppSettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _main;

        [ObservableProperty]
        private string _leftPaneHomePath = string.Empty;

        [ObservableProperty]
        private string _rightPaneHomePath = string.Empty;

        [ObservableProperty]
        private SearchBehavior _searchBehavior = SearchBehavior.SamePaneNewTab;

        [ObservableProperty]
        private SearchResultPathBehavior _searchResultPathBehavior = SearchResultPathBehavior.SamePaneNewTab;

        [ObservableProperty]
        private ContextMenuMode _contextMenuMode = ContextMenuMode.Zenith;

        /// <summary>検索実行時に自動的に1画面モードに切り替えるか。</summary>
        [ObservableProperty]
        private bool _autoSwitchToSinglePaneOnSearch = false;

        // インデックス関連設定
        [ObservableProperty]
        private IndexUpdateMode _indexUpdateMode = IndexUpdateMode.Interval;
        [ObservableProperty]
        private int _indexUpdateIntervalHours = 2;

        [ObservableProperty]
        private bool _indexEcoMode = true;
        [ObservableProperty]
        private int _indexMaxParallelism = 2;
        [ObservableProperty]
        private bool _indexNetworkLowPriority = true;
        [ObservableProperty]
        private bool _indexFreshnessAggressive = false;
        [ObservableProperty]
        private bool _indexFreshnessWarnStale = true;
        [ObservableProperty]
        private int _indexFullRebuildCooldownHours = 24;

        /// <summary>CPU 負荷が低い時のみインデックスを更新するか。</summary>
        [ObservableProperty]
        private bool _indexIdleOnlyExecution = false;

        /// <summary>アイドル判定の CPU 使用率閾値（%）。</summary>
        [ObservableProperty]
        private int _indexIdleCpuThreshold = 20;

        /// <summary>テーマ一覧。</summary>
        public ObservableCollection<ThemeInfo> AvailableThemes { get; } = new();

        /// <summary>カテゴリグループ化済みのテーマビュー。XAML の ListBox.ItemsSource にバインド。</summary>
        public ICollectionView ThemesView { get; }

        /// <summary>選択中のテーマ情報。UI の ListBox にバインドされる。</summary>
        [ObservableProperty]
        private ThemeInfo? _selectedThemeInfo;

        /// <summary>選択中のテーマ名。変更時にライブ適用 + 永続化を行う。</summary>
        [ObservableProperty]
        private string _selectedThemeName = "standard";

        /// <summary>テーマ自動選択モード。起動時ランダム選択の挙動を制御する。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRandomCategoryModeActive))]
        [NotifyPropertyChangedFor(nameof(IsAwaitingCategorySelection))]
        [NotifyPropertyChangedFor(nameof(GalleryModeHint))]
        [NotifyPropertyChangedFor(nameof(IsAssignmentModeActive))]
        private ThemeRandomizeMode _themeRandomizeMode = ThemeRandomizeMode.Disabled;

        /// <summary>テーマ適用モード（プリセット / 自動 / ペイン個別）。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAssignmentModeActive))]
        [NotifyPropertyChangedFor(nameof(GalleryModeHint))]
        [NotifyPropertyChangedFor(nameof(IsRandomModeActive))]
        [NotifyPropertyChangedFor(nameof(IsRandomCategoryModeActive))]
        [NotifyPropertyChangedFor(nameof(IsAwaitingCategorySelection))]
        [NotifyCanExecuteChangedFor(nameof(DrawRandomThemeCommand))]
        private ThemeApplyMode _activeThemeMode = ThemeApplyMode.Preset;

        /// <summary>ペイン個別テーマ適用の選択ターゲット。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAssignmentModeActive))]
        [NotifyPropertyChangedFor(nameof(GalleryModeHint))]
        private PaneTarget _selectedPaneTarget = PaneTarget.None;

        /// <summary>ランダム選択のターゲットカテゴリ名（CurrentCategory モード時のみ使用）。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GalleryModeHint))]
        [NotifyPropertyChangedFor(nameof(IsAwaitingCategorySelection))]
        private string? _selectedRandomCategory;

        /// <summary>ナビペインに適用中のテーマ名（ペイン個別適用用）。</summary>
        [ObservableProperty]
        private string _navPaneThemeName = string.Empty;

        /// <summary>Aペインに適用中のテーマ名（ペイン個別適用用）。</summary>
        [ObservableProperty]
        private string _aPaneThemeName = string.Empty;

        /// <summary>Bペインに適用中のテーマ名（ペイン個別適用用）。</summary>
        [ObservableProperty]
        private string _bPaneThemeName = string.Empty;

        /// <summary>プリセット以外のモードがアクティブのとき true（ヒントバー表示判定）。</summary>
        public bool IsAssignmentModeActive => ActiveThemeMode != ThemeApplyMode.Preset;

        /// <summary>自動選択モードがアクティブのとき true。</summary>
        public bool IsRandomModeActive => ActiveThemeMode == ThemeApplyMode.Random;

        /// <summary>カテゴリランダムモードがアクティブのとき true（ギャラリーグループ枠表示用）。</summary>
        public bool IsRandomCategoryModeActive =>
            ActiveThemeMode == ThemeApplyMode.Random && ThemeRandomizeMode == ThemeRandomizeMode.CurrentCategory;

        /// <summary>カテゴリランダムモードがアクティブかつカテゴリ未選択のとき true（選択誘導インジケーター表示用）。</summary>
        public bool IsAwaitingCategorySelection =>
            IsRandomCategoryModeActive && string.IsNullOrEmpty(SelectedRandomCategory);

        /// <summary>ギャラリー上部に表示する割り当てモードのヒントテキスト。</summary>
        public string GalleryModeHint => ActiveThemeMode switch
        {
            ThemeApplyMode.Random when ThemeRandomizeMode == ThemeRandomizeMode.CurrentCategory =>
                string.IsNullOrWhiteSpace(SelectedRandomCategory)
                    ? "カテゴリをクリックして対象を選択"
                    : $"「{SelectedRandomCategory}」からランダム選択",
            ThemeApplyMode.Random =>
                "起動時に全テーマからランダムでテーマが選択されます",
            ThemeApplyMode.Pane when SelectedPaneTarget == PaneTarget.None =>
                "ペインを選択してからテーマをクリックして適用",
            ThemeApplyMode.Pane =>
                SelectedPaneTarget switch
                {
                    PaneTarget.Nav   => "ナビペインを選択中 — テーマをクリックして適用",
                    PaneTarget.APane => "Aペインを選択中 — テーマをクリックして適用",
                    PaneTarget.BPane => "Bペインを選択中 — テーマをクリックして適用",
                    _                => string.Empty,
                },
            _ => string.Empty
        };

        /// <summary>Control Deck で選択中のカテゴリ。</summary>
        [ObservableProperty]
        private SettingsCategory _activeCategory = SettingsCategory.General;

        /// <summary>検索結果フィルターバーの有効状態（FileTypeFilter の順）。次回検索時に復元。</summary>
        private List<bool> _searchResultFileTypeFilterEnabled = new();

        public AppSettingsViewModel(MainViewModel main)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            ThemesView = CollectionViewSource.GetDefaultView(AvailableThemes);
            ThemesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ThemeInfo.CategoryDisplayName)));
            ThemesView.SortDescriptions.Add(new SortDescription(nameof(ThemeInfo.CategorySortOrder), ListSortDirection.Ascending));
            ThemesView.SortDescriptions.Add(new SortDescription(nameof(ThemeInfo.StandardFirstSortKey), ListSortDirection.Ascending));
            ThemesView.SortDescriptions.Add(new SortDescription(nameof(ThemeInfo.Name), ListSortDirection.Ascending));
            SubscribeUpdateService();
        }

        /// <summary>設定読み込み時にホームパスを反映する。</summary>
        public void LoadHomePaths(string leftPath, string rightPath)
        {
            LeftPaneHomePath = leftPath ?? string.Empty;
            RightPaneHomePath = rightPath ?? string.Empty;
        }

        /// <summary>設定読み込み時に検索挙動を反映する。</summary>
        public void LoadSearchBehavior(SearchBehavior behavior)
        {
            SearchBehavior = behavior;
        }

        /// <summary>設定読み込み時に検索結果パスクリック挙動を反映する。</summary>
        public void LoadSearchResultPathBehavior(SearchResultPathBehavior behavior)
        {
            SearchResultPathBehavior = behavior;
        }

        /// <summary>設定読み込み時にコンテキストメニュー方式を反映する。</summary>
        public void LoadContextMenuMode(ContextMenuMode mode)
        {
            ContextMenuMode = mode;
        }

        /// <summary>設定読み込み時に検索時の1画面モード自動切り替えを反映する。</summary>
        public void LoadAutoSwitchToSinglePaneOnSearch(bool enabled)
        {
            AutoSwitchToSinglePaneOnSearch = enabled;
        }

        /// <summary>設定読み込み時に検索結果フィルターバーの状態を反映する。</summary>
        public void LoadSearchResultFileTypeFilters(List<bool>? enabled)
        {
            if (enabled != null && enabled.Count == 11)
            {
                _searchResultFileTypeFilterEnabled = new List<bool>(enabled);
            }
            else
            {
                _searchResultFileTypeFilterEnabled = Enumerable.Repeat(true, 11).ToList();
            }
        }

        /// <summary>現在の検索結果フィルターバー状態を保存用に返す。</summary>
        public List<bool> GetSearchResultFileTypeFiltersForSave() => new(_searchResultFileTypeFilterEnabled);

        /// <summary>検索結果フィルターバーの状態を更新する（タブ側から呼び出し）。</summary>
        public void UpdateSearchResultFileTypeFilters(IReadOnlyList<bool> enabled)
        {
            if (enabled.Count != 11) return;
            _searchResultFileTypeFilterEnabled = new List<bool>(enabled);
        }

        /// <summary>保存済みの検索結果フィルターバー状態を取得する。初期化時にタブから呼び出し。</summary>
        public IReadOnlyList<bool> GetSearchResultFileTypeFilterState() => _searchResultFileTypeFilterEnabled;

        /// <summary>設定読み込み時にインデックス設定を反映する。</summary>
        public void LoadIndexSettings(IndexSettings? s)
        {
            if (s == null) return;
            IndexUpdateMode = s.UpdateMode;
            IndexUpdateIntervalHours = s.UpdateIntervalHours is 1 or 2 or 4 or 24 ? s.UpdateIntervalHours : 2;
            IndexEcoMode = s.EcoMode;
            IndexMaxParallelism = s.MaxParallelism;
            IndexNetworkLowPriority = s.NetworkLowPriority;
            IndexFreshnessAggressive = s.FreshnessAggressive;
            IndexFreshnessWarnStale = s.FreshnessWarnStale;
            IndexFullRebuildCooldownHours = s.FullRebuildCooldownHours is 6 or 12 or 24 ? s.FullRebuildCooldownHours : 24;
            IndexIdleOnlyExecution = s.IdleOnlyExecution;
            IndexIdleCpuThreshold = s.IdleCpuThreshold;
        }

        /// <summary>現在のインデックス設定を DTO として返す。</summary>
        public IndexSettings GetIndexSettingsForSave()
        {
            return new IndexSettings
            {
                UpdateMode = IndexUpdateMode,
                UpdateIntervalHours = IndexUpdateIntervalHours,
                EcoMode = IndexEcoMode,
                MaxParallelism = IndexMaxParallelism,
                NetworkLowPriority = IndexNetworkLowPriority,
                FreshnessAggressive = IndexFreshnessAggressive,
                FreshnessWarnStale = IndexFreshnessWarnStale,
                FullRebuildCooldownHours = IndexFullRebuildCooldownHours,
                IdleOnlyExecution = IndexIdleOnlyExecution,
                IdleCpuThreshold = IndexIdleCpuThreshold
            };
        }

        [RelayCommand]
        private void SetLeftPaneHomeFromCurrent()
        {
            var path = _main.LeftPane?.CurrentPath;
            if (string.IsNullOrEmpty(path))
            {
                App.Notification.Notify("Aペインに表示中のフォルダがありません", "ホーム設定");
                return;
            }

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ホーム設定: {path}");
                return;
            }

            LeftPaneHomePath = physical;
            App.Notification.Notify("Aペインのホームを設定しました", $"ホーム: {Path.GetFileName(physical)}");
        }

        [RelayCommand]
        private void SetRightPaneHomeFromCurrent()
        {
            var path = _main.RightPane?.CurrentPath;
            if (string.IsNullOrEmpty(path))
            {
                App.Notification.Notify("Bペインに表示中のフォルダがありません", "ホーム設定");
                return;
            }

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ホーム設定: {path}");
                return;
            }

            RightPaneHomePath = physical;
            App.Notification.Notify("Bペインのホームを設定しました", $"ホーム: {Path.GetFileName(physical)}");
        }

        [RelayCommand]
        private void SetLeftPaneHomeFromDialog()
        {
            var currentPath = _main.LeftPane?.CurrentPath ?? string.Empty;
            if (string.IsNullOrEmpty(currentPath) || !Directory.Exists(PathHelper.GetPhysicalPath(currentPath)))
                currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) ?? string.Empty;

            var dialog = new SelectFolderDialog(currentPath);
            if (dialog.ShowDialog() != true) return;

            var path = (dialog.SelectedPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path)) return;

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ホーム設定: {path}");
                return;
            }

            LeftPaneHomePath = physical;
            App.Notification.Notify("Aペインのホームを設定しました", $"ホーム: {Path.GetFileName(physical)}");
        }

        [RelayCommand]
        private void SetRightPaneHomeFromDialog()
        {
            var currentPath = _main.RightPane?.CurrentPath ?? string.Empty;
            if (string.IsNullOrEmpty(currentPath) || !Directory.Exists(PathHelper.GetPhysicalPath(currentPath)))
                currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) ?? string.Empty;

            var dialog = new SelectFolderDialog(currentPath);
            if (dialog.ShowDialog() != true) return;

            var path = (dialog.SelectedPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path)) return;

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ホーム設定: {path}");
                return;
            }

            RightPaneHomePath = physical;
            App.Notification.Notify("Bペインのホームを設定しました", $"ホーム: {Path.GetFileName(physical)}");
        }

        /// <summary>ペインラベルに対応するホームパスを取得する。</summary>
        public string GetHomePathForPane(string paneLabel)
        {
            return string.Equals(paneLabel, "A", StringComparison.OrdinalIgnoreCase)
                ? LeftPaneHomePath
                : RightPaneHomePath;
        }

        /// <summary>指定パスをAペインのホームに設定する（コンテキストメニューから呼び出し用）。</summary>
        public void SetLeftPaneHomeFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ホーム設定: {path}");
                return;
            }
            LeftPaneHomePath = physical;
            App.Notification.Notify("Aペインのホームを設定しました", $"ホーム: {Path.GetFileName(physical)}");
        }

        /// <summary>指定パスをBペインのホームに設定する（コンテキストメニューから呼び出し用）。</summary>
        public void SetRightPaneHomeFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ホーム設定: {path}");
                return;
            }
            RightPaneHomePath = physical;
            App.Notification.Notify("Bペインのホームを設定しました", $"ホーム: {Path.GetFileName(physical)}");
        }

        // ─── Backup ───

        private List<BackupEntryViewModel> _allBackupEntries = new();
        private const int BackupPageSize = 20;

        /// <summary>バックアップ一覧（現在ページ分）。Backup カテゴリにインライン表示。</summary>
        public ObservableCollection<BackupEntryViewModel> BackupEntries { get; } = new();

        /// <summary>現在のページ番号（1-based）。</summary>
        [ObservableProperty]
        private int _backupCurrentPage = 1;

        /// <summary>総ページ数。</summary>
        public int BackupTotalPages => _allBackupEntries.Count == 0 ? 1 : (int)Math.Ceiling((double)_allBackupEntries.Count / BackupPageSize);

        /// <summary>ページネーションバーに表示するページ番号アイテム一覧。</summary>
        public ObservableCollection<BackupPageItem> BackupPageItems { get; } = new();

        /// <summary>バックアップ一覧を非同期で再読み込みする。</summary>
        public async Task LoadBackupEntriesAsync()
        {
            var entries = await Task.Run(() =>
                SettingsBackupService.GetBackups()
                    .Select(e => new BackupEntryViewModel(e))
                    .ToList());
            _allBackupEntries = entries;
            BackupCurrentPage = 1;
            ApplyBackupPage();
        }

        private void ApplyBackupPage()
        {
            BackupEntries.Clear();
            foreach (var entry in _allBackupEntries.Skip((BackupCurrentPage - 1) * BackupPageSize).Take(BackupPageSize))
                BackupEntries.Add(entry);
            RebuildBackupPageItems();
            OnPropertyChanged(nameof(BackupTotalPages));
            BackupPrevPageCommand.NotifyCanExecuteChanged();
            BackupNextPageCommand.NotifyCanExecuteChanged();
        }

        /// <summary>ページ番号リストを再構築する（省略記号付き）。</summary>
        private void RebuildBackupPageItems()
        {
            BackupPageItems.Clear();
            int total = BackupTotalPages;
            int current = BackupCurrentPage;
            if (total <= 1) return;

            // 表示するページ番号を決定: 先頭2 + 末尾2 + 現在ページ周辺2
            var pages = new SortedSet<int>();
            for (int i = 1; i <= Math.Min(2, total); i++) pages.Add(i);
            for (int i = Math.Max(1, total - 1); i <= total; i++) pages.Add(i);
            for (int i = Math.Max(1, current - 1); i <= Math.Min(total, current + 1); i++) pages.Add(i);

            int prev = 0;
            foreach (var p in pages)
            {
                if (prev > 0 && p - prev > 1)
                    BackupPageItems.Add(new BackupPageItem { PageNumber = -1, IsCurrent = false }); // ellipsis
                BackupPageItems.Add(new BackupPageItem { PageNumber = p, IsCurrent = p == current });
                prev = p;
            }
        }

        [RelayCommand(CanExecute = nameof(CanBackupNextPage))]
        private void BackupNextPage() => GoToBackupPage(BackupCurrentPage + 1);

        private bool CanBackupNextPage() => BackupCurrentPage < BackupTotalPages;

        [RelayCommand(CanExecute = nameof(CanBackupPrevPage))]
        private void BackupPrevPage() => GoToBackupPage(BackupCurrentPage - 1);

        private bool CanBackupPrevPage() => BackupCurrentPage > 1;

        [RelayCommand]
        private void GoToBackupPage(object? pageObj)
        {
            int page = pageObj switch
            {
                int i => i,
                string s when int.TryParse(s, out var p) => p,
                _ => 1
            };
            int clamped = Math.Clamp(page, 1, Math.Max(1, BackupTotalPages));
            BackupCurrentPage = clamped;
            ApplyBackupPage();
        }

        [RelayCommand]
        private async Task BackupNowAsync()
        {
            _ = App.Stats.RecordAsync("Backup.Manual");
            await Services.SettingsBackupService.CreateBackupAsync("手動バックアップ");
            App.Notification.Notify("設定をバックアップしました", "[Settings] Backup: 手動");
            await LoadBackupEntriesAsync();
        }

        [RelayCommand]
        private void RestoreEntry(BackupEntryViewModel? vm)
        {
            if (vm == null) return;

            var confirm = ZenithDialog.Show(
                $"この設定（{vm.Timestamp:yyyy/MM/dd HH:mm:ss}）で復元しますか？\n現在の設定は上書きされます。",
                "設定の復元",
                ZenithDialogButton.OKCancel,
                ZenithDialogIcon.Warning);
            if (confirm != ZenithDialogResult.OK) return;

            try
            {
                SettingsBackupService.Restore(vm.JsonPath);
                _ = App.FileLogger.LogAsync($"[Settings] Recovery: restored from '{Path.GetFileName(vm.JsonPath)}'");
            }
            catch (Exception ex)
            {
                ZenithDialog.Show($"復元に失敗しました。\n{ex.Message}", "エラー",
                    ZenithDialogButton.OK, ZenithDialogIcon.Error);
                return;
            }

            var restart = ZenithDialog.Show(
                "設定を復元しました。変更を有効にするにはアプリの再起動が必要です。\n今すぐ再起動しますか？",
                "再起動の確認",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Question);
            if (restart == ZenithDialogResult.Yes)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                    Application.Current.Shutdown();
                }
            }
        }

        [RelayCommand]
        private void ToggleLock(BackupEntryViewModel? vm)
        {
            if (vm == null) return;
            var newLocked = !vm.IsLocked;
            SettingsBackupService.SetLock(vm.JsonPath, newLocked);
            vm.IsLocked = newLocked;
        }

        [RelayCommand]
        private void SetCategory(SettingsCategory category)
        {
            ActiveCategory = category;
        }

        /// <summary>起動時テーマ適用トーストを表示するか。</summary>
        [ObservableProperty]
        private bool _showStartupToast = true;

        partial void OnShowStartupToastChanged(bool value)
        {
            WindowSettings.SaveShowStartupToastOnly(value);
        }

        /// <summary>設定読み込み時に ShowStartupToast を反映する（保存トリガーなし）。</summary>
        public void LoadShowStartupToast(bool value)
        {
            // OnShowStartupToastChanged の保存処理を起動させないためフィールドを直接更新する（意図的）
#pragma warning disable MVVMTK0034
            _showStartupToast = value;
#pragma warning restore MVVMTK0034
            OnPropertyChanged(nameof(ShowStartupToast));
        }

        // ─── Display ───

        /// <summary>マイクロアニメーション ON/OFF。</summary>
        [ObservableProperty]
        private bool _enableMicroAnimations = true;

        partial void OnEnableMicroAnimationsChanged(bool value)
        {
            WindowSettings.SetMicroAnimationsRuntime(value);
            WindowSettings.SaveDisplaySettingsOnly(value, ListRowHeight);
        }

        /// <summary>ファイル一覧の行高（px）。24=コンパクト, 32=標準, 40=ゆったり。</summary>
        [ObservableProperty]
        private int _listRowHeight = 32;

        partial void OnListRowHeightChanged(int value)
        {
            WindowSettings.SetListRowHeightRuntime(value);
            Application.Current.Resources["ListRowHeight"] = (double)value;
            WindowSettings.SaveDisplaySettingsOnly(EnableMicroAnimations, value);
        }

        /// <summary>一覧表示のホバーアニメーション（フェード・スケール等）ON/OFF。</summary>
        [ObservableProperty]
        private bool _enableListAnimations = true;

        partial void OnEnableListAnimationsChanged(bool value)
        {
            WindowSettings.SetListAnimationsRuntime(value);
            Application.Current.Resources["ListItemHoverDuration"] = new System.Windows.Duration(
                value ? TimeSpan.FromSeconds(0.15) : TimeSpan.Zero);
            WindowSettings.SaveListAnimationsOnly(value);
        }

        // ─── General 追加 ───

        /// <summary>シングルクリックでフォルダを開く。</summary>
        [ObservableProperty]
        private bool _singleClickOpenFolder = false;

        partial void OnSingleClickOpenFolderChanged(bool value)
        {
            WindowSettings.SetSingleClickOpenFolderRuntime(value);
            WindowSettings.SaveGeneralSettingsOnly(value, ConfirmDelete, RestoreTabsOnStartup, NotificationDurationMs);
        }

        /// <summary>ファイル削除時に確認ダイアログを表示するか。</summary>
        [ObservableProperty]
        private bool _confirmDelete = true;

        partial void OnConfirmDeleteChanged(bool value)
        {
            WindowSettings.SetConfirmDeleteRuntime(value);
            WindowSettings.SaveGeneralSettingsOnly(SingleClickOpenFolder, value, RestoreTabsOnStartup, NotificationDurationMs);
        }

        /// <summary>起動時に前回のタブ構成を復元するか。</summary>
        [ObservableProperty]
        private bool _restoreTabsOnStartup = true;

        partial void OnRestoreTabsOnStartupChanged(bool value)
        {
            WindowSettings.SaveGeneralSettingsOnly(SingleClickOpenFolder, ConfirmDelete, value, NotificationDurationMs);
        }

        /// <summary>通知トーストの表示時間（ms）。</summary>
        [ObservableProperty]
        private int _notificationDurationMs = 3000;

        partial void OnNotificationDurationMsChanged(int value)
        {
            WindowSettings.SetNotificationDurationRuntime(value);
            WindowSettings.SaveGeneralSettingsOnly(SingleClickOpenFolder, ConfirmDelete, RestoreTabsOnStartup, value);
        }

        // ─── General 追加 (v0.20) ───

        /// <summary>タイトルバーにパスを表示するか。</summary>
        [ObservableProperty]
        private bool _showPathInTitleBar = true;

        partial void OnShowPathInTitleBarChanged(bool value)
        {
            WindowSettings.SetShowPathInTitleBarRuntime(value);
            _main.RefreshWindowTitle();
            WindowSettings.SaveShowPathInTitleBarOnly(value);
        }

        /// <summary>ファイル名に拡張子を表示するか。</summary>
        [ObservableProperty]
        private bool _showFileExtensions = true;

        partial void OnShowFileExtensionsChanged(bool value)
        {
            WindowSettings.SetShowFileExtensionsRuntime(value);
            _main.RefreshAllDisplayNames();
            WindowSettings.SaveShowFileExtensionsOnly(value);
        }

        /// <summary>隠しファイル・フォルダを表示するか。</summary>
        [ObservableProperty]
        private bool _showHiddenFiles = false;

        partial void OnShowHiddenFilesChanged(bool value)
        {
            WindowSettings.SetShowHiddenFilesRuntime(value);
            _main.RefreshAllPanes();
            WindowSettings.SaveShowHiddenFilesOnly(value);
        }

        /// <summary>ダウンロードフォルダに移動した際、更新日時降順で自動ソートするか。</summary>
        [ObservableProperty]
        private bool _downloadsSortByDate = false;

        partial void OnDownloadsSortByDateChanged(bool value)
        {
            WindowSettings.SetDownloadsSortByDateRuntime(value);
            WindowSettings.SaveDownloadsSortByDateOnly(value);
        }

        /// <summary>タスクトレイ常駐モード。</summary>
        [ObservableProperty]
        private bool _residentMode = false;

        partial void OnResidentModeChanged(bool value)
        {
            WindowSettings.SetResidentModeRuntime(value);
            App.TrayService?.SetVisible(value);
            WindowSettings.SaveResidentModeOnly(value);
        }

        // ─── Auto Update ───

        /// <summary>自動更新を有効にするか。</summary>
        [ObservableProperty]
        private bool _enableAutoUpdate = true;

        /// <summary>更新ステータス表示テキスト。</summary>
        [ObservableProperty]
        private string _updateStatus = "";

        /// <summary>新バージョンが利用可能か。</summary>
        [ObservableProperty]
        private bool _isUpdateAvailable = false;

        /// <summary>ダウンロード完了で再起動可能か。</summary>
        [ObservableProperty]
        private bool _isUpdateReadyToRestart = false;

        partial void OnEnableAutoUpdateChanged(bool value)
        {
            WindowSettings.SetAutoUpdateRuntime(value);
            App.UpdateService?.SetEnabled(value);
            WindowSettings.SaveAutoUpdateOnly(value);
        }

        /// <summary>手動で更新をチェックする。</summary>
        public async Task CheckForUpdateAsync()
        {
            UpdateStatus = "更新を確認中...";
            var (hasUpdate, version, error) = await (App.UpdateService?.CheckForUpdatesAsync()
                ?? Task.FromResult<(bool, string?, string?)>((false, null, null)));

            if (error != null)
            {
                UpdateStatus = $"チェック失敗: {error}";
                return;
            }

            if (hasUpdate && version != null)
            {
                IsUpdateAvailable = true;
                UpdateStatus = $"バージョン {version} が利用可能です";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatus = "最新バージョンです";
            }
        }

        /// <summary>ダウンロード→展開→再起動適用。</summary>
        public async void DownloadAndApplyAsync()
        {
            if (App.UpdateService == null) return;

            UpdateStatus = "ダウンロード中...";
            var success = await App.UpdateService.DownloadAndExtractAsync(_main);
            if (success)
            {
                UpdateStatus = "ダウンロード完了 — 再起動して適用できます";
                IsUpdateReadyToRestart = true;
            }
            else
            {
                UpdateStatus = "ダウンロードに失敗しました";
            }
        }

        /// <summary>UpdateService の StateChanged イベントを購読する。</summary>
        public void SubscribeUpdateService()
        {
            if (App.UpdateService == null) return;
            App.UpdateService.StateChanged += (_, _) =>
            {
                var svc = App.UpdateService;
                if (svc.AvailableVersion != null && !IsUpdateAvailable)
                {
                    IsUpdateAvailable = true;
                    UpdateStatus = $"バージョン {svc.AvailableVersion} が利用可能です";
                }
                if (svc.IsReadyToRestart && !IsUpdateReadyToRestart)
                {
                    IsUpdateReadyToRestart = true;
                    UpdateStatus = "ダウンロード完了 — 再起動して適用できます";
                }
            };
        }

        // ─── Search デフォルト ───

        /// <summary>新規タブ作成時のフォルダ先頭表示デフォルト。</summary>
        [ObservableProperty]
        private bool _defaultGroupFoldersFirst = true;

        partial void OnDefaultGroupFoldersFirstChanged(bool value)
        {
            WindowSettings.SetDefaultGroupFoldersFirstRuntime(value);
            WindowSettings.SaveSearchDefaultsOnly(value, DefaultSortProperty, DefaultSortDirection);
        }

        /// <summary>新規タブ作成時のデフォルトソートプロパティ名。</summary>
        [ObservableProperty]
        private string _defaultSortProperty = "Name";

        partial void OnDefaultSortPropertyChanged(string value)
        {
            WindowSettings.SetDefaultSortPropertyRuntime(value);
            WindowSettings.SaveSearchDefaultsOnly(DefaultGroupFoldersFirst, value, DefaultSortDirection);
        }

        /// <summary>新規タブ作成時のデフォルトソート方向。</summary>
        [ObservableProperty]
        private ListSortDirection _defaultSortDirection = ListSortDirection.Ascending;

        partial void OnDefaultSortDirectionChanged(ListSortDirection value)
        {
            WindowSettings.SetDefaultSortDirectionRuntime(value);
            WindowSettings.SaveSearchDefaultsOnly(DefaultGroupFoldersFirst, DefaultSortProperty, value);
        }

        /// <summary>設定読み込み時に Display / General追加 / Search デフォルト 設定を反映する（保存トリガーなし）。</summary>
        public void LoadDisplayAndGeneralAndSearchSettings(WindowSettings s)
        {
#pragma warning disable MVVMTK0034
            _enableMicroAnimations = s.EnableMicroAnimations;
            _listRowHeight = s.ListRowHeight;
            _singleClickOpenFolder = s.SingleClickOpenFolder;
            _confirmDelete = s.ConfirmDelete;
            _restoreTabsOnStartup = s.RestoreTabsOnStartup;
            _notificationDurationMs = s.NotificationDurationMs;
            _defaultGroupFoldersFirst = s.DefaultGroupFoldersFirst;
            _defaultSortProperty = s.DefaultSortProperty;
            _defaultSortDirection = s.DefaultSortDirection;
            _enableListAnimations = s.EnableListAnimations;
            _showPathInTitleBar = s.ShowPathInTitleBar;
            _showFileExtensions = s.ShowFileExtensions;
            _showHiddenFiles = s.ShowHiddenFiles;
            _downloadsSortByDate = s.DownloadsSortByDate;
            _residentMode = s.ResidentMode;
            _enableAutoUpdate = s.AutoUpdate;
#pragma warning restore MVVMTK0034
            OnPropertyChanged(nameof(EnableMicroAnimations));
            OnPropertyChanged(nameof(ListRowHeight));
            OnPropertyChanged(nameof(SingleClickOpenFolder));
            OnPropertyChanged(nameof(ConfirmDelete));
            OnPropertyChanged(nameof(RestoreTabsOnStartup));
            OnPropertyChanged(nameof(NotificationDurationMs));
            OnPropertyChanged(nameof(DefaultGroupFoldersFirst));
            OnPropertyChanged(nameof(DefaultSortProperty));
            OnPropertyChanged(nameof(DefaultSortDirection));
            OnPropertyChanged(nameof(EnableListAnimations));
            OnPropertyChanged(nameof(ShowPathInTitleBar));
            OnPropertyChanged(nameof(ShowFileExtensions));
            OnPropertyChanged(nameof(ShowHiddenFiles));
            OnPropertyChanged(nameof(DownloadsSortByDate));
            OnPropertyChanged(nameof(ResidentMode));
            OnPropertyChanged(nameof(EnableAutoUpdate));
        }

        // ── ペイン個別テーマ ──
        private ResourceDictionary? _navPaneResources;
        private ResourceDictionary? _aPaneResources;
        private ResourceDictionary? _bPaneResources;

        /// <summary>MainWindow から起動時に各ペインの ResourceDictionary を登録する。</summary>
        public void RegisterPaneResources(
            ResourceDictionary navPane,
            ResourceDictionary aPane,
            ResourceDictionary bPane)
        {
            _navPaneResources = navPane;
            _aPaneResources   = aPane;
            _bPaneResources   = bPane;
        }

        private ResourceDictionary? GetPaneResources(PaneTarget target) => target switch
        {
            PaneTarget.Nav   => _navPaneResources,
            PaneTarget.APane => _aPaneResources,
            PaneTarget.BPane => _bPaneResources,
            _                => null,
        };

        private void ClearPaneResources()
        {
            foreach (var dict in new[] { _navPaneResources, _aPaneResources, _bPaneResources })
                dict?.Clear();
        }

        /// <summary>起動時に保存済みペインテーマ名を ViewModel に反映する（実際の適用は MainWindow.xaml.cs で行う）。</summary>
        public void LoadPaneThemeNames(WindowSettings settings)
        {
            _suppressThemeChange = true;
            NavPaneThemeName = settings.NavPaneThemeName;
            APaneThemeName   = settings.APaneThemeName;
            BPaneThemeName   = settings.BPaneThemeName;
            _suppressThemeChange = false;
        }

        // ─── テーマ ───

        private bool _suppressThemeChange;
        private bool _isLoadingSettings = false;
        private bool _themeTransitionInProgress;

        /// <summary>テーマ適用前のフェードインアニメーション。MainWindow が設定する。</summary>
        internal Func<Task>? OnBeforeThemeChangeAsync { get; set; }
        /// <summary>テーマ適用後のフェードアウトアニメーション開始。MainWindow が設定する。</summary>
        internal Action? OnAfterThemeChangeApplied { get; set; }

        /// <summary>「パーソナライズ」モード時の最終テーマ名キャッシュ。閉じる時の SavedThemeName 保存に使用。</summary>
        private string _savedThemeNameCache = "standard";

        internal static string ToModeStr(ThemeApplyMode m) => m switch
        {
            ThemeApplyMode.Random => "Auto", ThemeApplyMode.Pane => "Pane", _ => "Personalize"
        };
        internal static string ToSubStr(ThemeRandomizeMode s) =>
            s == ThemeRandomizeMode.CurrentCategory ? "Category" : "All";
        private static ThemeApplyMode ParseMode(string? s) => s switch
        {
            "Auto" => ThemeApplyMode.Random, "Pane" => ThemeApplyMode.Pane, _ => ThemeApplyMode.Preset
        };
        private static ThemeRandomizeMode ParseSubMode(string? s) =>
            s == "Category" ? ThemeRandomizeMode.CurrentCategory : ThemeRandomizeMode.AllThemes;

        /// <summary>設定読み込み時にテーマ名とテーマ一覧を初期化する。</summary>
        public void LoadTheme(string? themeName)
        {
            RefreshThemes();
            _suppressThemeChange = true;
            var name = !string.IsNullOrWhiteSpace(themeName) ? themeName : "standard";
            SelectedThemeName = name;
            SelectedThemeInfo = AvailableThemes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? AvailableThemes.FirstOrDefault();
            _suppressThemeChange = false;
        }

        /// <summary>起動時に保存済みのモード設定を復元する（保存トリガーなし）。</summary>
        public void LoadThemeSettings(WindowSettings settings)
        {
            // App.StartupSavedThemeName は ReadThemeSettingsFromSettings で ThemeName フォールバック済み
            _savedThemeNameCache = App.StartupSavedThemeName;
            _isLoadingSettings = true;
            try
            {
                // OnXxxChanged の副作用（他フィールド上書き・保存）を起動させないためフィールドを直接更新する（意図的）
#pragma warning disable MVVMTK0034
                _selectedRandomCategory = settings.SelectedCategory;
                _themeRandomizeMode = ParseSubMode(settings.AutoSelectSubMode);
                _activeThemeMode = ParseMode(settings.CurrentThemeMode);
#pragma warning restore MVVMTK0034
                OnPropertyChanged(nameof(IsAssignmentModeActive));
                OnPropertyChanged(nameof(IsRandomModeActive));
                OnPropertyChanged(nameof(IsRandomCategoryModeActive));
                OnPropertyChanged(nameof(IsAwaitingCategorySelection));
                OnPropertyChanged(nameof(GalleryModeHint));
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        /// <summary>themes フォルダをスキャンしてテーマ一覧を更新する。</summary>
        public void RefreshThemes()
        {
            var themes = App.ThemeService.ScanThemes();
            var current = SelectedThemeName;
            _suppressThemeChange = true;
            AvailableThemes.Clear();
            foreach (var t in themes) AvailableThemes.Add(t);

            // スキャン結果に現在のテーマがなければ standard にフォールバック
            var match = AvailableThemes.FirstOrDefault(t => string.Equals(t.Name, current, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                SelectedThemeName = "standard";
                SelectedThemeInfo = AvailableThemes.FirstOrDefault();
            }
            else
            {
                SelectedThemeInfo = match;
            }
            _suppressThemeChange = false;
        }

        partial void OnSelectedThemeInfoChanged(ThemeInfo? value)
        {
            if (_suppressThemeChange) return;
            if (value == null) return;
            _ = App.Stats.RecordAsync("Theme.Change");

            // カテゴリランダム選択モード: テーマのカテゴリを選択（テーマ適用しない）
            if (ActiveThemeMode == ThemeApplyMode.Random && ThemeRandomizeMode == ThemeRandomizeMode.CurrentCategory)
            {
                SelectedRandomCategory = value.CategoryDisplayName;
                return;
            }

            // ペイン個別割り当てモード
            if (ActiveThemeMode == ThemeApplyMode.Pane && SelectedPaneTarget != PaneTarget.None)
            {
                var dict = GetPaneResources(SelectedPaneTarget);
                if (dict != null)
                    App.ThemeService.ApplyThemeLive(value.Name, dict);

                switch (SelectedPaneTarget)
                {
                    case PaneTarget.Nav:   NavPaneThemeName = value.Name; break;
                    case PaneTarget.APane: APaneThemeName   = value.Name; break;
                    case PaneTarget.BPane: BPaneThemeName   = value.Name; break;
                }
                WindowSettings.SavePaneThemeNames(NavPaneThemeName, APaneThemeName, BPaneThemeName);
                return;
            }

            // 通常モード: グローバルテーマ変更
            if (string.Equals(SelectedThemeName, value.Name, StringComparison.OrdinalIgnoreCase)) return;
            SelectedThemeName = value.Name;
        }

        /// <summary>アニメーション付きでテーマを適用する共通ヘルパー。連続クリック時はアニメなし即時適用。</summary>
        private async Task ApplyThemeAnimatedAsync(string themeName, System.Windows.ResourceDictionary resources)
        {
            if (_themeTransitionInProgress)
            {
                App.ThemeService.ApplyThemeLive(themeName, resources);
                return;
            }
            _themeTransitionInProgress = true;
            try
            {
                if (OnBeforeThemeChangeAsync != null)
                    await OnBeforeThemeChangeAsync();
                App.ThemeService.ApplyThemeLive(themeName, resources);
                OnAfterThemeChangeApplied?.Invoke();
            }
            finally
            {
                _themeTransitionInProgress = false;
            }
        }

        partial void OnSelectedThemeNameChanged(string value)
        {
            if (_suppressThemeChange) return;
            if (string.IsNullOrWhiteSpace(value)) return;

            // fire-and-forget でアニメーション付き適用（保存は同期で即実行）
            ApplyThemeAnimatedAsync(value, Application.Current.Resources).FireAndForget("ApplyThemeAnimatedAsync");
            // 永続化
            WindowSettings.SaveThemeOnly(value);
            // プリセットモード時は SavedThemeName にも保存し、キャッシュを更新
            if (ActiveThemeMode == ThemeApplyMode.Preset)
            {
                _savedThemeNameCache = value;
                WindowSettings.SavePresetThemeNameOnly(value);
            }
        }

        /// <summary>閉じる時の保存用に現在のテーマモード設定をまとめて返す。</summary>
        public (string Mode, string SubMode, string? Category, string SavedTheme) GetThemePersistenceForSave() =>
            (ToModeStr(ActiveThemeMode), ToSubStr(ThemeRandomizeMode), SelectedRandomCategory, _savedThemeNameCache);

        /// <summary>自動選択モード時に即座にランダム抽選してテーマを適用する。</summary>
        [RelayCommand(CanExecute = nameof(CanDrawRandomTheme))]
        private async Task DrawRandomThemeAsync()
        {
            var allThemes = AvailableThemes.ToList();
            var pool = (ThemeRandomizeMode == ThemeRandomizeMode.CurrentCategory && !string.IsNullOrEmpty(SelectedRandomCategory)
                ? allThemes.Where(t => t.CategoryDisplayName == SelectedRandomCategory)
                : (IEnumerable<Models.ThemeInfo>)allThemes).ToList();
            if (pool.Count == 0) return;

            var preferred = pool.Where(t => t.Name != SelectedThemeName).ToList();
            var candidates = preferred.Count > 0 ? preferred : pool;
            var pick = candidates[Random.Shared.Next(candidates.Count)];

            await ApplyThemeAnimatedAsync(pick.Name, Application.Current.Resources);
            _suppressThemeChange = true;
            SelectedThemeName = pick.Name;
            SelectedThemeInfo = AvailableThemes.FirstOrDefault(t => t.Name == pick.Name);
            _suppressThemeChange = false;
            WindowSettings.SaveThemeOnly(pick.Name);
            App.Notification.Notify($"テーマを変更: {pick.Name}");
        }

        private bool CanDrawRandomTheme() => ActiveThemeMode == ThemeApplyMode.Random;

        partial void OnActiveThemeModeChanged(ThemeApplyMode value)
        {
            switch (value)
            {
                case ThemeApplyMode.Random:
                    if (ThemeRandomizeMode == ThemeRandomizeMode.Disabled)
                        ThemeRandomizeMode = ThemeRandomizeMode.AllThemes;
                    SelectedPaneTarget = PaneTarget.None;
                    SelectedRandomCategory = null;
                    // ペイン個別設定をクリア → Application.Current.Resources にフォールバック
                    ClearPaneResources();
                    break;
                case ThemeApplyMode.Pane:
                    ThemeRandomizeMode = ThemeRandomizeMode.Disabled;
                    SelectedRandomCategory = null;
                    break;
                default: // Preset
                    ThemeRandomizeMode = ThemeRandomizeMode.Disabled;
                    SelectedRandomCategory = null;
                    SelectedPaneTarget = PaneTarget.None;
                    // ペイン個別設定をクリア → Application.Current.Resources にフォールバック
                    ClearPaneResources();
                    break;
            }
            if (!_isLoadingSettings)
                WindowSettings.SaveThemeModeOnly(ToModeStr(value), ToSubStr(ThemeRandomizeMode), SelectedRandomCategory);
        }

        partial void OnThemeRandomizeModeChanged(ThemeRandomizeMode value)
        {
            // ランダムサブ選択（AllThemes / CurrentCategory）が直接クリックされたとき、
            // 自動選択モード自体をアクティブにする。これにより他モード中でも
            // RadioButton をクリックするだけでモード切り替えが完結する。
            if (value != ThemeRandomizeMode.Disabled && ActiveThemeMode != ThemeApplyMode.Random)
                ActiveThemeMode = ThemeApplyMode.Random;

            if (value != ThemeRandomizeMode.CurrentCategory)
                SelectedRandomCategory = null;

            if (!_isLoadingSettings)
                WindowSettings.SaveThemeModeOnly(ToModeStr(ActiveThemeMode), ToSubStr(value), SelectedRandomCategory);
        }

        partial void OnSelectedRandomCategoryChanged(string? value)
        {
            if (!_isLoadingSettings)
                WindowSettings.SaveThemeModeOnly(ToModeStr(ActiveThemeMode), ToSubStr(ThemeRandomizeMode), value);
        }

        /// <summary>テーマ適用モードを切り替える。</summary>
        [RelayCommand]
        private void SetThemeMode(ThemeApplyMode mode) => ActiveThemeMode = mode;

        /// <summary>ランダム選択のターゲットカテゴリを設定する。</summary>
        [RelayCommand]
        private void SelectRandomCategory(string? categoryName) => SelectedRandomCategory = categoryName;

        [RelayCommand]
        private async Task ExportThemeAsync()
        {
            try
            {
                await App.ThemeService.ExportAsync();
                RefreshThemes();
                App.Notification.Notify("テーマをエクスポートしました", $"themes/{App.ThemeService.CurrentThemeName}.json");
            }
            catch (Exception ex)
            {
                App.Notification.Notify("テーマのエクスポートに失敗しました", ex.Message);
            }
        }

        // ═══ ライセンス ═══

        /// <summary>ライセンス状態の表示テキスト。</summary>
        [ObservableProperty]
        private string _licenseStatusText = "読み込み中...";

        /// <summary>Full ライセンスかどうか。</summary>
        [ObservableProperty]
        private bool _isFullLicense;

        /// <summary>各機能の使用状況一覧。</summary>
        public ObservableCollection<FeatureUsageInfo> FeatureUsages { get; } = new();

        // ── ショートカットキー設定 ──

        /// <summary>グループ別キーバインド定義。UI バインド用。</summary>
        public ObservableCollection<Models.KeyBindingGroupViewModel> KeyBindingGroups { get; } = new();

        private bool _keyBindingsLoaded;

        /// <summary>ショートカットキー設定をロードする。</summary>
        public void LoadKeyBindings()
        {
            if (_keyBindingsLoaded) return;
            _keyBindingsLoaded = true;

            KeyBindingGroups.Clear();
            foreach (var group in App.KeyBindings.GetGroups())
                KeyBindingGroups.Add(group);
        }

        [RelayCommand]
        private void ResetAllKeyBindings()
        {
            App.KeyBindings.ResetAll();
            WindowSettings.SaveKeyBindingsOnly(null);

            // グループを再ロード
            _keyBindingsLoaded = false;
            LoadKeyBindings();
        }

        [RelayCommand]
        private void ResetKeyBinding(string? actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            App.KeyBindings.ResetToDefault(actionId);
            SaveKeyBindings();

            // グループを再ロードして UI を更新
            _keyBindingsLoaded = false;
            LoadKeyBindings();
        }

        /// <summary>現在のカスタムバインドを保存する。</summary>
        internal void SaveKeyBindings()
        {
            var customBindings = App.KeyBindings.GetCustomBindingsForSave();
            WindowSettings.SaveKeyBindingsOnly(customBindings.Count > 0 ? customBindings : null);
        }

        /// <summary>カテゴリ切替時に License / Shortcut を選んだ場合に読み込みを実行する。</summary>
        partial void OnActiveCategoryChanged(SettingsCategory value)
        {
            if (value == SettingsCategory.License)
                LoadLicenseStatusAsync().FireAndForget("LoadLicenseStatusAsync");
            else if (value == SettingsCategory.Shortcut)
                LoadKeyBindings();
            else if (value == SettingsCategory.Backup)
                LoadBackupEntriesAsync().FireAndForget("LoadBackupEntriesAsync");
            else if (value == SettingsCategory.Statistics)
                LoadStatisticsAsync().FireAndForget("LoadStatisticsAsync");
        }

        // ── 利用統計 ──────────────────────────────────────────
        public ObservableCollection<ActionStat> StatItems { get; } = new();

        [ObservableProperty]
        private bool _isStatsLoading;

        /// <summary>UsageStatisticsService.ActionRecorded を購読して StatItems を即時更新します。</summary>
        public void SubscribeStatEvents()
        {
            App.Stats.ActionRecorded += OnActionRecorded;
        }

        private void OnActionRecorded(string actionKey)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var item = StatItems.FirstOrDefault(x => x.ActionKey == actionKey);
                if (item != null)
                    item.Count++;
            });
        }

        public async Task LoadStatisticsAsync()
        {
            IsStatsLoading = true;
            try
            {
                var dbItems = await App.Stats.GetAllAsync();
                var dict = dbItems.ToDictionary(x => x.ActionKey, x => x);
                StatItems.Clear();
                foreach (var key in Services.UsageStatisticsService.AllActionKeys)
                {
                    if (dict.TryGetValue(key, out var item))
                        StatItems.Add(item);
                    else
                        StatItems.Add(new ActionStat { ActionKey = key, Count = 0 });
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[AppSettingsVM] LoadStatisticsAsync failed: {ex.Message}");
            }
            finally
            {
                IsStatsLoading = false;
            }
            await LoadTopFoldersAsync();
        }

        public ObservableCollection<HistoryRecord> TopFolders { get; } = new();

        private async Task LoadTopFoldersAsync()
        {
            try
            {
                var items = await App.Database.GetTopFoldersAsync(50);
                TopFolders.Clear();
                foreach (var item in items)
                    TopFolders.Add(item);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[AppSettingsVM] LoadTopFoldersAsync failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenFolderInExplorer(HistoryRecord? record)
        {
            if (record == null || string.IsNullOrEmpty(record.Path)) return;
            if (Directory.Exists(record.Path))
                Process.Start("explorer.exe", record.Path);
            else
                App.Notification.Notify("フォルダが見つかりません", record.Path);
        }

        [RelayCommand]
        private void AddFolderToFavorites(HistoryRecord? record)
        {
            if (record == null || string.IsNullOrEmpty(record.Path)) return;
            if (_main.Favorites.ContainsPath(record.Path))
            {
                App.Notification.Notify("既にお気に入りに登録されています", System.IO.Path.GetFileName(record.Path));
                return;
            }
            _main.Favorites.AddPath(record.Path);
            App.Notification.Notify("お気に入りに追加しました", System.IO.Path.GetFileName(record.Path));
            _ = App.Stats.RecordAsync("Favorites.Add");
        }

        public async Task ResetStatisticsAsync()
        {
            await App.Stats.ResetAsync();
            StatItems.Clear();
            foreach (var key in Services.UsageStatisticsService.AllActionKeys)
                StatItems.Add(new ActionStat { ActionKey = key, Count = 0 });
            App.Notification.Notify("利用統計をリセットしました", "[Stats] Reset");
        }

        /// <summary>ライセンス状態と各機能の使用状況を読み込みます。</summary>
        public async Task LoadLicenseStatusAsync()
        {
            try
            {
                var svc = App.License;
                IsFullLicense = svc.IsFullLicense;
                LicenseStatusText = svc.IsFullLicense ? "製品版（Full License）" : "無料版（Free）";

                var usages = await svc.GetAllFeatureUsagesAsync();
                FeatureUsages.Clear();
                foreach (var u in usages)
                    FeatureUsages.Add(u);
            }
            catch (Exception ex)
            {
                LicenseStatusText = "読み込みに失敗しました";
                _ = App.FileLogger.LogAsync($"[AppSettingsVM] LoadLicenseStatusAsync failed: {ex.Message}");
            }
        }
    }
}
