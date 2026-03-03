using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenithFiler.Models;

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
        Theme
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
                FullRebuildCooldownHours = IndexFullRebuildCooldownHours
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

        [RelayCommand]
        private async Task BackupNowAsync()
        {
            await Services.SettingsBackupService.CreateBackupAsync("手動バックアップ");
            App.Notification.Notify("設定をバックアップしました", "[Settings] Backup: 手動");
        }

        [RelayCommand]
        private void Restore()
        {
            var dialog = new BackupListDialog();
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            dialog.ShowDialog();
        }

        [RelayCommand]
        private void SetCategory(SettingsCategory category)
        {
            ActiveCategory = category;
        }

        // ─── テーマ ───

        private bool _suppressThemeChange;

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

            // カテゴリランダム選択モード: テーマのカテゴリを選択（テーマ適用しない）
            if (ActiveThemeMode == ThemeApplyMode.Random && ThemeRandomizeMode == ThemeRandomizeMode.CurrentCategory)
            {
                SelectedRandomCategory = value.CategoryDisplayName;
                return;
            }

            // ペイン個別割り当てモード（将来実装のため保存のみ）
            if (ActiveThemeMode == ThemeApplyMode.Pane && SelectedPaneTarget != PaneTarget.None)
            {
                switch (SelectedPaneTarget)
                {
                    case PaneTarget.Nav:   NavPaneThemeName = value.Name; break;
                    case PaneTarget.APane: APaneThemeName   = value.Name; break;
                    case PaneTarget.BPane: BPaneThemeName   = value.Name; break;
                }
                return;
            }

            // 通常モード: グローバルテーマ変更
            if (string.Equals(SelectedThemeName, value.Name, StringComparison.OrdinalIgnoreCase)) return;
            SelectedThemeName = value.Name;
        }

        partial void OnSelectedThemeNameChanged(string value)
        {
            if (_suppressThemeChange) return;
            if (string.IsNullOrWhiteSpace(value)) return;

            // ライブ適用
            App.ThemeService.ApplyThemeLive(value, Application.Current.Resources);
            // 永続化
            WindowSettings.SaveThemeOnly(value);
        }

        partial void OnActiveThemeModeChanged(ThemeApplyMode value)
        {
            switch (value)
            {
                case ThemeApplyMode.Random:
                    if (ThemeRandomizeMode == ThemeRandomizeMode.Disabled)
                        ThemeRandomizeMode = ThemeRandomizeMode.AllThemes;
                    SelectedPaneTarget = PaneTarget.None;
                    SelectedRandomCategory = null;
                    break;
                case ThemeApplyMode.Pane:
                    ThemeRandomizeMode = ThemeRandomizeMode.Disabled;
                    SelectedRandomCategory = null;
                    break;
                default: // Preset
                    ThemeRandomizeMode = ThemeRandomizeMode.Disabled;
                    SelectedRandomCategory = null;
                    SelectedPaneTarget = PaneTarget.None;
                    break;
            }
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
    }
}
