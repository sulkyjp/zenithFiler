using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanara.Windows.Shell;
using ClosedXML.Excel;
using ZenithFiler.Helpers;
using ZenithFiler.Services;
using ZenithFiler.Services.Commands;
using ZenithFiler.ViewModels;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public class NavigationPathSegment
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsLast { get; set; }
    }

    public partial class TabItemViewModel : BaseViewModel, IDisposable
    {
        #region プロパティ・状態

        private MainViewModel? MainVM => Application.Current?.MainWindow?.DataContext as MainViewModel;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TabTitle))]
        private bool _isSearchResultTab;

        [ObservableProperty]
        private bool _isLocked;

        [ObservableProperty]
        private bool _isAdaptiveColumnsEnabled = true;

        partial void OnIsAdaptiveColumnsEnabledChanged(bool value)
        {
            App.Notification.Notify(value ? "アダプティブ列をオンにしました" : "アダプティブ列をオフにしました", $"アダプティブ列: {(value ? "オン" : "オフ")}");
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TabTitle))]
        [NotifyPropertyChangedFor(nameof(SourceType))]
        private string _currentPath = string.Empty;

        public string TabTitle => IsSearching
            ? (string.IsNullOrWhiteSpace(SearchText) && IsIndexSearchMode && !string.IsNullOrEmpty(CurrentPath)
                ? $"結果確認：{Path.GetFileName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}"
                : $"検索: {SearchText}")
            : (string.IsNullOrEmpty(CurrentPath) ? "ホーム" : (Path.GetFileName(CurrentPath) is string name && !string.IsNullOrEmpty(name) ? name : CurrentPath));

        public SourceType SourceType => PathHelper.DetermineSourceType(CurrentPath);

        [ObservableProperty]
        private bool _isPathEditMode;

        public ObservableCollection<NavigationPathSegment> PathSegments { get; } = new();

        /// <summary>表示するパンくずセグメント（幅に応じて省略）</summary>
        public ObservableCollection<NavigationPathSegment> VisiblePathSegments { get; } = new();

        /// <summary>省略されたセグメント（...メニュー用）</summary>
        public ObservableCollection<NavigationPathSegment> OverflowSegments { get; } = new();

        [ObservableProperty]
        private bool _hasBreadcrumbOverflow;

        [ObservableProperty]
        private bool _isNotificationFlashActive;

        [ObservableProperty]
        private bool _isGroupFoldersFirst = true;

        partial void OnIsGroupFoldersFirstChanged(bool value)
        {
            ApplySort();
            App.Notification.Notify(value ? "フォルダを先に表示するようにしました" : "フォルダを先に表示するのを解除しました", $"フォルダを先に表示: {(value ? "オン" : "オフ")}");
        }

        [ObservableProperty]
        private string _sortProperty = "Name";

        partial void OnSortPropertyChanged(string value)
        {
            ApplySort();
            OnPropertyChanged(nameof(ActiveSortProperty));
            OnPropertyChanged(nameof(ActiveSortDirection));
        }

        [ObservableProperty]
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        partial void OnSortDirectionChanged(ListSortDirection value)
        {
            ApplySort();
            OnPropertyChanged(nameof(ActiveSortProperty));
            OnPropertyChanged(nameof(ActiveSortDirection));
        }

        /// <summary>検索結果ビュー専用のソートプロパティ（デフォルト: 更新日時）。</summary>
        [ObservableProperty]
        private string _searchSortProperty = "LastModified";

        partial void OnSearchSortPropertyChanged(string value)
        {
            ApplySort();
            OnPropertyChanged(nameof(ActiveSortProperty));
            OnPropertyChanged(nameof(ActiveSortDirection));
        }

        /// <summary>検索結果ビュー専用のソート方向（デフォルト: 降順＝新しい順）。</summary>
        [ObservableProperty]
        private ListSortDirection _searchSortDirection = ListSortDirection.Descending;

        partial void OnSearchSortDirectionChanged(ListSortDirection value)
        {
            ApplySort();
            OnPropertyChanged(nameof(ActiveSortProperty));
            OnPropertyChanged(nameof(ActiveSortDirection));
        }

        /// <summary>現在アクティブなビュー（フォルダ or 検索）のソートプロパティ。カラムヘッダー矢印がバインドする。</summary>
        public string ActiveSortProperty => IsSearching ? SearchSortProperty : SortProperty;

        /// <summary>現在アクティブなビュー（フォルダ or 検索）のソート方向。カラムヘッダー矢印がバインドする。</summary>
        public ListSortDirection ActiveSortDirection => IsSearching ? SearchSortDirection : SortDirection;

        /// <summary>ファイル一覧の表示モード（詳細／大・中・小アイコン）。</summary>
        [ObservableProperty]
        private FileViewMode _fileViewMode = FileViewMode.Details;

        partial void OnFileViewModeChanged(FileViewMode value)
        {
            RefreshCommand.NotifyCanExecuteChanged(); // アイコンビュー時は更新ボタン無効化のため
            var label = ParentPane?.PaneLabel ?? "";
            var msg = value switch
            {
                FileViewMode.Details => "詳細一覧",
                FileViewMode.LargeIcon => "大アイコン",
                FileViewMode.MediumIcon => "中アイコン",
                FileViewMode.SmallIcon => "小アイコン",
                _ => value.ToString()
            };
            App.Notification.Notify($"表示モードを{msg}に変更しました", $"{label}ペイン: {msg}");
        }

        [RelayCommand]
        private void ChangeFileViewMode(FileViewMode mode)
        {
            FileViewMode = mode;
            if (IsIconViewMode(mode))
            {
                _ = LoadThumbnailsForCurrentItemsAsync();
            }
        }

        /// <summary>アイコンビュー（大・中・小）かどうか。</summary>
        private static bool IsIconViewMode(FileViewMode mode) =>
            mode == FileViewMode.LargeIcon || mode == FileViewMode.MediumIcon || mode == FileViewMode.SmallIcon;

        /// <summary>Box共有リンク整形成功時などに表示するグリーンフラッシュ演出を一瞬実行する。</summary>
        public async Task TriggerSuccessFlashAsync()
        {
            IsNotificationFlashActive = true;
            await Task.Delay(200);
            IsNotificationFlashActive = false;
        }

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _selectionInfoText = string.Empty;

        public SilentObservableCollection<FileItem> Items { get; } = new();
        public ICollectionView ItemsView { get; }
        public ICollectionView CurrentItemsView => IsSearching ? SearchResultsView : ItemsView;

        [ObservableProperty]
        private string _searchText = string.Empty;

        #endregion

        #region 検索

        /// <summary>インデックス検索モードかどうか。true のとき検索バーは⚡アイコン・黄色背景。</summary>
        [ObservableProperty]
        private bool _isIndexSearchMode;

        [RelayCommand]
        private void ToggleIndexSearchMode()
        {
            IsIndexSearchMode = !IsIndexSearchMode;

            // 検索テキストがあれば切り替え直後に即座に再検索
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                _ = ExecuteSearch(SearchText);
            }
        }

        [RelayCommand]
        private void EnterIndexSearchMode()
        {
            IsIndexSearchMode = true;
        }

        [RelayCommand]
        private void ExitIndexSearchMode()
        {
            IsIndexSearchMode = false;
        }

        partial void OnIsSearchingChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowEmptySearchResult));
            OnPropertyChanged(nameof(ActiveSortProperty));
            OnPropertyChanged(nameof(ActiveSortDirection));
        }

        partial void OnSearchTextChanged(string value)
        {
            // インデックス登録確認の場合（IsIndexSearchMode かつ CurrentPath がある）は、空文字列でも検索を続行
            if (string.IsNullOrWhiteSpace(value) && !(IsIndexSearchMode && IsSearchResultTab && !string.IsNullOrEmpty(CurrentPath)))
            {
                IsSearching = false;
                _searchCts?.Cancel();
                SearchResults.Clear();
                ShowIndexFreshnessWarning = false;
                IndexFreshnessWarningMessage = string.Empty;
                OnPropertyChanged(nameof(CurrentItemsView));
                OnPropertyChanged(nameof(TabTitle));
                return;
            }

            OnPropertyChanged(nameof(TabTitle));

            var behavior = MainVM?.AppSettings?.SearchBehavior ?? SearchBehavior.SamePaneNewTab;
            bool shouldImmediateSearch = IsSearchResultTab ||
                (behavior == SearchBehavior.SamePaneCurrentTabInstant && ParentPane != null);

            if (shouldImmediateSearch)
            {
                if (!IsSearchResultTab && behavior == SearchBehavior.SamePaneCurrentTabInstant)
                {
                    IsSearchResultTab = true;
                }
                _ = ExecuteSearch(value);
            }
        }

        [RelayCommand]
        private void ClearSearch()
        {
            // タブ枚数・検索挙動設定に関わらず「検索解除して通常フォルダ表示に戻る」に統一。
            // ※ CloseTab は呼ばない。

            // 1. 検索結果タブを先に通常タブへ戻す。
            //    OnSearchTextChanged のインデックス検索継続判定（IsSearchResultTab を参照）が
            //    SearchText="" のリセットを誤ってスキップしないよう、SearchText クリアより前に行う。
            if (IsSearchResultTab)
            {
                IsSearchResultTab = false;
            }

            // 2. 検索テキストをクリアし、IsSearching を明示的に false にする。
            //    OnSearchTextChanged も IsSearching=false・SearchResults.Clear・CTS キャンセル・
            //    ShowIndexFreshnessWarning リセットを自動実行するため冗長だが、
            //    いかなる条件下でも確実にリセットされるよう明示的に設定する。
            //    （ShowEmptySearchResult も IsSearching=false により自動で false に変わる）
            SearchText = string.Empty;
            IsSearching = false;

            // 3. 1画面モード自動切替でペイン数が変化していた場合は復元する。
            MainVM?.RestorePaneCountAfterSearchIfNeeded(this);

            // 4. 現在パスで通常の一覧を再読み込みする（NavigateAsync でパス正規化も行う）。
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                _ = NavigateAsync(CurrentPath, saveToHistory: false);
            }

            App.Notification.Notify("検索をクリアしました", "検索をクリア");
        }

        [ObservableProperty]
        private bool _isSearching;

        /// <summary>PerformSearchAsync 実行中（デバウンス含む）は true。SearchResults クリア直後のフラッシュ防止用。</summary>
        [ObservableProperty]
        private bool _isSearchRunning;

        partial void OnIsSearchRunningChanged(bool value) => OnPropertyChanged(nameof(ShowEmptySearchResult));

        public SilentObservableCollection<FileItem> SearchResults { get; } = new();
        public ICollectionView SearchResultsView { get; }

        /// <summary>検索結果のファイル種類フィルタ項目（フォルダ、Excel、Word 等）。</summary>
        public ObservableCollection<FileTypeFilterItem> FileTypeFilterItems { get; } = new();

        /// <summary>インデックスが古い可能性がある場合に検索結果ビューで警告を表示するか。</summary>
        [ObservableProperty]
        private bool _showIndexFreshnessWarning;

        /// <summary>検索結果ビューに表示する鮮度警告メッセージ。</summary>
        [ObservableProperty]
        private string _indexFreshnessWarningMessage = string.Empty;

        /// <summary>検索完了後かつ結果が0件の場合 true。ファイルリスト中央の Watermark 表示に使用。IsSearchRunning が true の間（デバウンス・検索実行中）は false を返し、SearchResults クリア直後のフラッシュを防ぐ。</summary>
        public bool ShowEmptySearchResult => IsSearching && SearchResults.Count == 0 && !IsSearchRunning;

        /// <summary>フィルタ適用後の検索結果件数。検索バー右端の「XX items」表示に使用。</summary>
        public int FilteredSearchResultCount => SearchResultsView?.Cast<object>().Count() ?? 0;

        [RelayCommand]
        private void DismissIndexFreshnessWarning()
        {
            ShowIndexFreshnessWarning = false;
            IndexFreshnessWarningMessage = string.Empty;
        }

        [RelayCommand]
        private void SelectAllFilters()
        {
            foreach (var item in FileTypeFilterItems)
                item.IsEnabled = true;
            SearchResultsView.Refresh();
            OnPropertyChanged(nameof(FilteredSearchResultCount));
            SaveFileTypeFilterState();
        }

        [RelayCommand]
        private void ClearAllFilters()
        {
            foreach (var item in FileTypeFilterItems)
                item.IsEnabled = false;
            SearchResultsView.Refresh();
            OnPropertyChanged(nameof(FilteredSearchResultCount));
            SaveFileTypeFilterState();
        }

        private void InitializeFileTypeFilters()
        {
            var savedState = MainVM?.AppSettings?.GetSearchResultFileTypeFilterState();
            var defaultEnabled = savedState != null && savedState.Count == 11;

            var items = new[]
            {
                new FileTypeFilterItem { FilterType = FileTypeFilter.Folder, DisplayName = "フォルダ", Extensions = null },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Excel, DisplayName = "Excel", Extensions = [".xlsx", ".xls", ".xlsm", ".xlsb"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Word, DisplayName = "Word", Extensions = [".docx", ".doc", ".docm"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.PowerPoint, DisplayName = "PPT", Extensions = [".pptx", ".ppt", ".pptm"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Pdf, DisplayName = "PDF", Extensions = [".pdf"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Text, DisplayName = "TXT", Extensions = [".txt", ".csv", ".log", ".md", ".ini"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Exe, DisplayName = "EXE", Extensions = [".exe"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Bat, DisplayName = "BAT", Extensions = [".bat", ".cmd", ".ps1"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Json, DisplayName = "JSON", Extensions = [".json", ".xml", ".yaml", ".yml"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Image, DisplayName = "画像", Extensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico"] },
                new FileTypeFilterItem { FilterType = FileTypeFilter.Other, DisplayName = "その他", Extensions = null },
            };
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (defaultEnabled && i < savedState!.Count)
                    item.IsEnabled = savedState[i];
                item.PropertyChanged += OnFileTypeFilterItemPropertyChanged;
                FileTypeFilterItems.Add(item);
            }
        }

        private void SaveFileTypeFilterState()
        {
            var state = FileTypeFilterItems.Select(f => f.IsEnabled).ToList();
            if (state.Count != 11) return;
            MainVM?.AppSettings?.UpdateSearchResultFileTypeFilters(state);
            WindowSettings.SaveSearchResultFiltersOnly(state);
        }

        private void OnFileTypeFilterItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileTypeFilterItem.IsEnabled))
            {
                SearchResultsView.Refresh();
                OnPropertyChanged(nameof(FilteredSearchResultCount));
                SaveFileTypeFilterState();
            }
        }

        /// <summary>保存済みのフィルター状態を適用する。起動時・設定読み込み後に既存タブへ反映するために呼び出し。</summary>
        internal void ApplySavedFilterState(IReadOnlyList<bool> state)
        {
            if (state == null || state.Count != 11 || FileTypeFilterItems.Count != 11) return;
            for (int i = 0; i < 11; i++)
                FileTypeFilterItems[i].IsEnabled = state[i];
            SearchResultsView.Refresh();
            OnPropertyChanged(nameof(FilteredSearchResultCount));
        }

        private bool IncludeSearchResultByFilter(object? obj)
        {
            if (obj is not FileItem fi) return false;

            // 1. ファイル種別フィルタ
            if (fi.IsDirectory)
            {
                var folderFilter = FileTypeFilterItems.FirstOrDefault(f => f.FilterType == FileTypeFilter.Folder);
                if (!(folderFilter?.IsEnabled ?? true)) return false;
            }
            else
            {
                var ext = Path.GetExtension(fi.Name)?.ToLowerInvariant() ?? string.Empty;
                bool typeMatched = false;
                foreach (var filter in FileTypeFilterItems)
                {
                    if (filter.Extensions == null) continue;
                    if (filter.Extensions.Contains(ext))
                    {
                        if (!filter.IsEnabled) return false;
                        typeMatched = true;
                        break;
                    }
                }
                if (!typeMatched)
                {
                    var otherFilter = FileTypeFilterItems.FirstOrDefault(f => f.FilterType == FileTypeFilter.Other);
                    if (!(otherFilter?.IsEnabled ?? true)) return false;
                }
            }

            // 2. サイズ・日付フィルタ
            if (MainVM?.SearchFilter?.MatchesFilter(fi) == false)
                return false;

            return true;
        }

        // 検索履歴（通常/インデックスを区別）
        public ObservableCollection<SearchHistoryItem> SearchHistory { get; } = new();

        [ObservableProperty]
        private bool _hasSearchHistory;

        [RelayCommand]
        public async Task ExecuteSearch(string? query)
        {
            var text = !string.IsNullOrEmpty(query) ? query : SearchText;
            // インデックス登録確認: 空クエリで rootPath のみ指定の全件表示を許可
            if (string.IsNullOrWhiteSpace(text) && !(IsIndexSearchMode && IsSearchResultTab && !string.IsNullOrEmpty(CurrentPath)))
                return;

            var behavior = MainVM?.AppSettings?.SearchBehavior ?? SearchBehavior.SamePaneNewTab;
            var autoSinglePane = MainVM?.AppSettings?.AutoSwitchToSinglePaneOnSearch ?? false;

            // 検索時の1画面モード自動切り替え処理
            if (autoSinglePane && !IsSearchResultTab && ParentPane != null && MainVM != null)
            {
                // Bペインで検索した場合、検索タブをAペインに移動して1画面モードにする
                if (ParentPane == MainVM.RightPane)
                {
                    // 1画面モードに切り替える前にペイン数を記録
                    if (MainVM.PaneCount == 2)
                    {
                        MainVM.RecordPaneCountBeforeSearch();
                    }
                    MainVM.MoveSearchToLeftPaneAndSwitchToSinglePane(ParentPane, CurrentPath, text, IsIndexSearchMode);
                    SearchText = string.Empty;
                    return;
                }
                
                // Aペインで検索した場合、1画面モードに切り替え
                if (MainVM.PaneCount == 2)
                {
                    MainVM.RecordPaneCountBeforeSearch();
                    MainVM.PaneCount = 1;
                    App.Notification.Notify("1画面モードに切り替えました", "検索結果を表示");
                }
            }

            if (!IsSearchResultTab && ParentPane != null)
            {
                if (behavior == SearchBehavior.SamePaneCurrentTabInstant)
                {
                    IsSearchResultTab = true;
                }
                else if (behavior == SearchBehavior.OtherPaneNewTab)
                {
                    MainVM?.OpenSearchInOtherPane(ParentPane, CurrentPath, text, IsIndexSearchMode);
                    SearchText = string.Empty;
                    return;
                }
                else
                {
                    ParentPane.OpenSearchTab(CurrentPath, text, IsIndexSearchMode);
                    return;
                }
            }

            // 検索結果タブの場合（または SamePaneCurrentTabInstant で昇格した場合）: このタブで検索を実行
            bool wasSearching = IsSearching;
            IsSearching = true;
            if (!wasSearching) OnPropertyChanged(nameof(CurrentItemsView));
            OnPropertyChanged(nameof(TabTitle));

            await PerformSearchAsync(text);

            var trimmed = text.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                var sf = MainVM?.SearchFilter;
                var matchedPreset = FindMatchingPresetName();
                await App.Database.SaveSearchHistoryAsync(trimmed, IsIndexSearchMode,
                    presetName: matchedPreset,
                    minSizeText: sf?.MinSizeText,
                    maxSizeText: sf?.MaxSizeText,
                    startDateText: sf?.StartDateText,
                    endDateText: sf?.EndDateText);
                App.Notification.Notify("検索を実行しました", $"検索実行: \"{trimmed}\" ({CurrentPath})");
            }
            else if (IsIndexSearchMode && !string.IsNullOrEmpty(CurrentPath))
            {
                var folderName = Path.GetFileName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                App.Notification.Notify(string.IsNullOrEmpty(folderName) ? "インデックス内容を表示しました" : $"{folderName} のインデックス内容を表示しました", "インデックス登録確認");
            }
        }

        [RelayCommand]
        private async Task SelectSearchHistory(SearchHistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.Keyword)) return;

            // フィルタ復旧
            var sf = MainVM?.SearchFilter;
            if (sf != null)
            {
                sf._isLoading = true;
                sf.MinSizeText = item.MinSizeText;
                sf.MaxSizeText = item.MaxSizeText;
                sf.StartDateText = item.StartDateText;
                sf.EndDateText = item.EndDateText;
                sf._isLoading = false;
                sf.NotifyAllFilterProperties();
            }

            SearchText = item.Keyword;
            IsIndexSearchMode = item.IsIndexSearch;
            await ExecuteSearch(item.Keyword);
        }

        public async Task LoadSearchHistoryAsync()
        {
            var history = await App.Database.GetSearchHistoryAsync();
            SearchHistory.Clear();
            foreach (var h in history) SearchHistory.Add(h);
            HasSearchHistory = SearchHistory.Count > 0;
        }

        /// <summary>
        /// 現在のフィルタ条件（サイズ・日付）が保存済みプリセットの定義と一致するか動的判定し、
        /// 一致するプリセット名を返す。キーワードは比較対象外。
        /// </summary>
        private string? FindMatchingPresetName()
        {
            var presets = MainVM?.SearchPresets?.Presets;
            var sf = MainVM?.SearchFilter;
            if (presets == null || presets.Count == 0 || sf == null) return null;

            SearchFilterViewModel.TryParseSizeInput(sf.MinSizeText, out long curMinBytes);
            SearchFilterViewModel.TryParseSizeInput(sf.MaxSizeText, out long curMaxBytes);
            SearchFilterViewModel.TryParseDateInput(sf.StartDateText, out DateTime curStart);
            SearchFilterViewModel.TryParseDateInput(sf.EndDateText, out DateTime curEnd);

            foreach (var preset in presets)
            {
                if (preset.IsIndexSearchMode != IsIndexSearchMode) continue;

                SearchFilterViewModel.TryParseSizeInput(preset.MinSizeText, out long pMinBytes);
                SearchFilterViewModel.TryParseSizeInput(preset.MaxSizeText, out long pMaxBytes);
                SearchFilterViewModel.TryParseDateInput(preset.StartDateText, out DateTime pStart);
                SearchFilterViewModel.TryParseDateInput(preset.EndDateText, out DateTime pEnd);

                if (curMinBytes == pMinBytes && curMaxBytes == pMaxBytes
                    && curStart == pStart && curEnd == pEnd)
                {
                    return preset.Name;
                }
            }

            return null;
        }

        private void OnSearchHistoryChanged(object? sender, EventArgs e)
        {
            _ = Application.Current.Dispatcher.InvokeAsync(async () => await LoadSearchHistoryAsync());
        }

        private System.Threading.CancellationTokenSource? _searchCts;
        private bool _scopeSubscribed;
        private bool _filterSubscribed;

        /// <summary>スコープ選択変更時に検索を再実行するハンドラ。</summary>
        private void OnScopeSelectionChanged()
        {
            // 検索結果タブ表示中（IsSearching=true かつインデックスモード）のときのみ再検索
            if (!IsSearching || !IsIndexSearchMode) return;
            if (string.IsNullOrWhiteSpace(SearchText)) return;
            _ = PerformSearchAsync(SearchText);
        }

        /// <summary>ScopeSelectionChanged イベントを遅延購読する（初回インデックス検索時）。</summary>
        private void EnsureScopeSubscription()
        {
            if (_scopeSubscribed) return;
            var settings = MainVM?.IndexSearchSettings;
            if (settings == null) return;
            settings.ScopeSelectionChanged += OnScopeSelectionChanged;
            _scopeSubscribed = true;
        }

        /// <summary>SearchFilter の FilterChanged イベントを遅延購読する（初回検索時）。</summary>
        private void EnsureFilterSubscription()
        {
            if (_filterSubscribed) return;
            var sf = MainVM?.SearchFilter;
            if (sf == null) return;
            sf.FilterChanged += OnSearchFilterChanged;
            _filterSubscribed = true;
        }

        private void OnSearchFilterChanged()
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 検索中なら再検索（Lucene レベルのフィルタも再適用される）
                if (IsSearching && !string.IsNullOrWhiteSpace(SearchText))
                {
                    _ = PerformSearchAsync(SearchText);
                    return;
                }
                // 検索中でなくても ICollectionView リフレッシュ
                SearchResultsView?.Refresh();
                OnPropertyChanged(nameof(FilteredSearchResultCount));
            });
        }

        private async Task PerformSearchAsync(string query)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

            using var busyToken = MainVM?.BeginBusy();

            IsSearchRunning = true;

            // GlowBar 管理用（デバウンス前に初期化）
            bool glowBarStarted = false;
            DispatcherTimer? progressTimer = null;
            double progressTarget = 2;
            string statusText = IsIndexSearchMode ? "[検索] インデックスから検索中..." : "[検索] 検索中...";

            try
            {
                // 入力中の負荷軽減のためのデバウンス
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) return;
                _ = App.FileLogger.LogAsync($"[{ParentPane?.PaneLabel ?? "?"}] Search: \"{query}\"");

                // スコープ選択変更・フィルタ変更による再検索を購読
                if (IsIndexSearchMode) EnsureScopeSubscription();
                EnsureFilterSubscription();

                // ── GlowBar 開始（デバウンス後）──
                MainVM?.BeginFileOperation(statusText, FlowDirection.LeftToRight);
                if (MainVM != null) MainVM.FileOperationProgress = 2;
                glowBarStarted = true;
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                // ── DispatcherTimer による滑らか進捗 ──
                progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                progressTimer.Tick += (_, _) =>
                {
                    if (MainVM == null) return;
                    double target = Volatile.Read(ref progressTarget);
                    double current = MainVM.FileOperationProgress;
                    MainVM.FileOperationStatusText = statusText;
                    if (Math.Abs(target - current) < 0.3) return;
                    double step = (target - current) * 0.18;
                    if (step > 0 && step < 0.5) step = 0.5;
                    MainVM.FileOperationProgress = Math.Min(current + step, target);
                };
                progressTimer.Start();
                var sw = System.Diagnostics.Stopwatch.StartNew();

                await Application.Current.Dispatcher.InvokeAsync(() => SearchResults.Clear());
                ShowIndexFreshnessWarning = false;
                IndexFreshnessWarningMessage = string.Empty;

                var settings = MainVM?.AppSettings;
                var aggressive = settings?.IndexFreshnessAggressive ?? false;
                var warnStale = settings?.IndexFreshnessWarnStale ?? true;

                // 鮮度優先モード（インデックス検索時）: バックグラウンドで差分更新・未インデックス作成を開始
                if (IsIndexSearchMode && aggressive)
                {
                    try
                    {
                        var paths = MainVM?.IndexSearchSettings?.GetPathsForSave() ?? new List<string>();
                        foreach (var p in paths)
                        {
                            if (string.IsNullOrEmpty(p) || !System.IO.Directory.Exists(p)) continue;
                            try
                            {
                                if (App.IndexService.IsPathIndexed(p))
                                    _ = App.IndexService.UpdateDirectoryDiffAsync(p, null).ContinueWith(t =>
                                    {
                                        if (t.Exception != null)
                                            _ = App.FileLogger.LogAsync($"[IndexService] UpdateDirectoryDiffAsync failed: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
                                    }, TaskContinuationOptions.OnlyOnFaulted);
                                else
                                    App.IndexService.TriggerUpdateNow(new[] { p }, null);
                            }
                            catch { /* 個別パスの失敗は無視 */ }
                        }
                    }
                    catch { /* 鮮度優先の開始失敗は無視 */ }
                }

                // 1. まず既存インデックスで検索＆表示（超高速）
                string? searchRoot = IsIndexSearchMode
                    ? (string.IsNullOrWhiteSpace(query) && !string.IsNullOrEmpty(CurrentPath) ? CurrentPath : null)
                    : CurrentPath;

                // searchRoot が null（インデックス全体検索）の場合のみスコープを適用
                IReadOnlyList<string>? scopePaths = null;
                if (IsIndexSearchMode && searchRoot == null)
                    scopePaths = MainVM?.IndexSearchSettings?.GetScopePathsForSearch();

                // サイズ・日付フィルタを構築
                Services.SearchFilter? searchFilter = null;
                var sf = MainVM?.SearchFilter;
                if (sf != null)
                {
                    var sr = sf.GetSizeRange();
                    var dr = sf.GetDateRange();
                    if (sr != null || dr != null)
                        searchFilter = new Services.SearchFilter { SizeRange = sr, DateRange = dr };
                }

                Volatile.Write(ref progressTarget, 50);
                var initialHits = await Task.Run(() =>
                    scopePaths != null
                        ? App.IndexService.Search(query, scopePaths, filter: searchFilter)
                        : App.IndexService.Search(query, searchRoot, filter: searchFilter), token);

                if (token.IsCancellationRequested) return;

                Volatile.Write(ref progressTarget, 95);
                statusText = "[検索] 結果を表示しています...";
                await UpdateSearchResultsAsync(initialHits, token);

                // 2a. インデックス検索モード: 作成中かつ初回 0 件ならプログレッシブ再検索
                if (IsIndexSearchMode && initialHits.Count == 0 && App.Notification.IsIndexing)
                {
                    Volatile.Write(ref progressTarget, 10);
                    statusText = "[検索] インデックス作成中 — 結果を順次更新しています...";

                    double loopTarget = 10;
                    while (App.Notification.IsIndexing && !token.IsCancellationRequested)
                    {
                        try { await Task.Delay(2000, token); }
                        catch (TaskCanceledException) { return; }
                        if (token.IsCancellationRequested) return;

                        var updatedHits = await Task.Run(() =>
                            scopePaths != null
                                ? App.IndexService.Search(query, scopePaths, filter: searchFilter)
                                : App.IndexService.Search(query, searchRoot, filter: searchFilter), token);
                        if (token.IsCancellationRequested) return;

                        loopTarget = Math.Min(loopTarget + 5, 85);
                        Volatile.Write(ref progressTarget, loopTarget);

                        if (updatedHits.Count > 0)
                        {
                            statusText = $"[検索] インデックス作成中 — {updatedHits.Count} 件を表示中...";
                            await Application.Current.Dispatcher.InvokeAsync(() => SearchResults.Clear());
                            await UpdateSearchResultsAsync(updatedHits, token);
                        }
                    }

                    // 完了後に最終検索
                    if (!token.IsCancellationRequested)
                    {
                        Volatile.Write(ref progressTarget, 90);
                        statusText = "[検索] 最終結果を取得中...";
                        var finalHits = await Task.Run(() =>
                            scopePaths != null
                                ? App.IndexService.Search(query, scopePaths, filter: searchFilter)
                                : App.IndexService.Search(query, searchRoot, filter: searchFilter), token);
                        if (!token.IsCancellationRequested && finalHits.Count > initialHits.Count)
                        {
                            Volatile.Write(ref progressTarget, 95);
                            statusText = "[検索] 結果を表示しています...";
                            await Application.Current.Dispatcher.InvokeAsync(() => SearchResults.Clear());
                            await UpdateSearchResultsAsync(finalHits, token);
                        }
                    }
                }

                // 2b. 通常検索モード: 未インデックスならプログレッシブ再検索
                if (!IsIndexSearchMode && !string.IsNullOrEmpty(CurrentPath))
                {
                    if (!App.IndexService.IsPathIndexed(CurrentPath))
                    {
                        Volatile.Write(ref progressTarget, 10);
                        statusText = "[検索] インデックス作成中 — 結果を順次更新しています...";

                        var indexingToken = _indexingCts?.Token ?? default;
                        var indexingTask = App.IndexService.AddDirectoryToIndexAsync(CurrentPath, null, indexingToken);

                        double loopTarget = 10;
                        while (!indexingTask.IsCompleted && !token.IsCancellationRequested)
                        {
                            try { await Task.WhenAny(indexingTask, Task.Delay(2000, token)); }
                            catch (TaskCanceledException) { return; }
                            if (token.IsCancellationRequested) return;

                            loopTarget = Math.Min(loopTarget + 5, 85);
                            Volatile.Write(ref progressTarget, loopTarget);

                            var updatedHits = await Task.Run(() => App.IndexService.Search(query, searchRoot, filter: searchFilter), token);
                            if (token.IsCancellationRequested) return;

                            if (updatedHits.Count > 0)
                                statusText = $"[検索] インデックス作成中 — {updatedHits.Count} 件を表示中...";

                            await Application.Current.Dispatcher.InvokeAsync(() => SearchResults.Clear());
                            await UpdateSearchResultsAsync(updatedHits, token);
                        }

                        // 最終検索
                        if (!token.IsCancellationRequested)
                        {
                            Volatile.Write(ref progressTarget, 90);
                            statusText = "[検索] 最終結果を取得中...";
                            var finalHits = await Task.Run(() => App.IndexService.Search(query, searchRoot, filter: searchFilter), token);
                            if (!token.IsCancellationRequested)
                            {
                                Volatile.Write(ref progressTarget, 95);
                                statusText = "[検索] 結果を表示しています...";
                                await Application.Current.Dispatcher.InvokeAsync(() => SearchResults.Clear());
                                await UpdateSearchResultsAsync(finalHits, token);
                            }
                        }
                    }
                }

                // 鮮度警告: インデックス検索時かつ WarnStale が ON のとき、未インデックスのフォルダがある場合のみ表示
                // （作成中のみの場合は一時的な状態のため表示しない。過剰な警告を避ける）
                if (IsIndexSearchMode && warnStale && Application.Current?.Dispatcher != null)
                {
                    try
                    {
                        var paths = MainVM?.IndexSearchSettings?.GetPathsForSave() ?? new List<string>();
                        var hasUnindexed = paths.Any(p => !string.IsNullOrEmpty(p) && !App.IndexService.IsPathIndexed(p));
                        if (hasUnindexed)
                        {
                            var msg = "一部のフォルダがまだインデックスされていません。インデックスビューで「未インデックスを作成」を実行してください。";
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    ShowIndexFreshnessWarning = true;
                                    IndexFreshnessWarningMessage = msg;
                                }
                                catch { /* UI更新の失敗は無視 */ }
                            });
                        }
                    }
                    catch { /* 鮮度チェックの失敗は無視 */ }
                }

                // 正常完了: 最低表示時間を確保してから EndFileOperation
                sw.Stop();
                var minDisplay = TimeSpan.FromMilliseconds(800);
                if (sw.Elapsed < minDisplay)
                    await Task.Delay(minDisplay - sw.Elapsed);

                progressTimer.Stop();
                progressTimer = null;
                MainVM?.EndFileOperation();
                glowBarStarted = false;
            }
            catch (TaskCanceledException)
            {
                // 無視
            }
            catch (Exception)
            {
                // エラーは無視して続行
            }
            finally
            {
                // キャンセルされた検索（古いもの）は IsSearchRunning を触らない。
                // 新しい検索が既に IsSearchRunning = true を立てているためリセットしない。
                if (!token.IsCancellationRequested)
                    IsSearchRunning = false;

                // Timer が残っていたら停止
                progressTimer?.Stop();

                // キャンセル時 or エラー時: EndFileOperation の非同期フェード(800ms)との競合を避け、
                // 直接プロパティをリセットして即座に GlowBar を消す
                if (glowBarStarted && MainVM != null)
                {
                    MainVM.IsFileOperationActive = false;
                    MainVM.FileOperationProgress = 0;
                    MainVM.FileOperationStatusText = string.Empty;
                }
            }
        }

        private async Task UpdateSearchResultsAsync(List<SearchHit> hits, System.Threading.CancellationToken token)
        {
            const int batchSize = 50;

            var (folderIcon, folderType) = ShellIconHelper.GetGenericInfo("dummy_folder", true);
            var (fallbackFileIcon, fallbackFileType) = ShellIconHelper.GetGenericInfo(".txt", false);
            var extensionCache = new Dictionary<string, (ImageSource? Icon, string TypeName)>(StringComparer.OrdinalIgnoreCase);

            await Application.Current.Dispatcher.InvokeAsync(() => SearchResultsView.SortDescriptions.Clear());

            var batch = new List<FileItem>();
            foreach (var hit in hits)
            {
                if (token.IsCancellationRequested) return;

                // ファイルで拡張子がない場合は除外（万が一の漏れ対策）
                if (!hit.IsDirectory && !System.IO.Path.HasExtension(hit.Name))
                    continue;

                ImageSource? icon;
                string typeName;

                if (hit.IsDirectory)
                {
                    icon = folderIcon;
                    typeName = folderType ?? "フォルダー";
                }
                else
                {
                    var ext = System.IO.Path.GetExtension(hit.Path);
                    if (!extensionCache.TryGetValue(ext, out var cached))
                    {
                        cached = ShellIconHelper.GetGenericInfo(hit.Path, false);
                        extensionCache[ext] = cached;
                    }
                    icon = cached.Icon ?? fallbackFileIcon;
                    typeName = cached.TypeName ?? fallbackFileType ?? "ファイル";
                }

                var item = new FileItem
                {
                    Name = hit.Name,
                    FullPath = hit.Path,
                    IsDirectory = hit.IsDirectory,
                    Size = hit.Size,
                    LastModified = hit.ModifiedTicks > 0 ? new DateTime(hit.ModifiedTicks) : DateTime.MinValue,
                    Icon = icon,
                    TypeName = typeName,
                    Attributes = System.IO.FileAttributes.Normal
                };

                batch.Add(item);
                if (batch.Count >= batchSize)
                {
                    var toAdd = batch.ToList();
                    batch.Clear();
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var i in toAdd) SearchResults.Add(i);
                    }, DispatcherPriority.Background);
                }
            }

            if (batch.Count > 0 && !token.IsCancellationRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var i in batch) SearchResults.Add(i);
                }, DispatcherPriority.Background);
            }

            if (!token.IsCancellationRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ApplySort();
                }, DispatcherPriority.Background);
            }
        }

        #endregion

        /// <summary>削除後に FileSystemWatcher による Refresh が走った際、このインデックスに選択を復元する。View が設定・クリアする。</summary>
        public int? SelectionIndexToRestore { get; set; }

        /// <summary>ペースト（ドロップ）完了後の Refresh でリストを再描画したあと、View がフォーカスをリストに戻すために使用する。</summary>
        public bool RequestFocusAfterRefresh { get; set; }

        /// <summary>ペースト後に選択・フォーカスする対象のファイル名一覧。View が使用後にクリアする。</summary>
        public List<string>? PastedFileNamesToSelect { get; set; }

        /// <summary>シェル（エクスプローラ）のコンテキストメニュー等からの変更を期待しているかどうか。</summary>
        public bool IsExpectingShellChange { get; set; }

        /// <summary>フォーカス復元後にリネーム（名前の変更）を開始する。新規フォルダ作成後に View が使用し、リネーム開始後にクリアする。</summary>
        public bool RequestRenameAfterFocusRestore { get; set; }

        /// <summary>Back 後、戻り先一覧でフォーカスするパス（入ったフォルダ）。View が OnRefreshCompleted で使用後にクリアする。</summary>
        public string? PathToFocusAfterNavigation { get; set; }

        /// <summary>新規作成された直後のフォルダパス。リネームがキャンセルされた場合に削除するために保持する。</summary>
        private string? _pendingNewFolderPath;

        /// <summary>新規作成フローから RenameItem が呼ばれたことを示すフラグ。パス比較に依存しない確実なロールバック判定に使用。</summary>
        private bool _isRenameForNewItem;

        private readonly Stack<string> _history = new();
        private readonly Stack<string> _forwardHistory = new();
        private FileSystemWatcher? _watcher;
        private int _watcherReconnectAttempt;
        private const int MaxWatcherReconnectAttempts = 10;
        private volatile bool _watcherDisconnected;
        private CancellationTokenSource? _watcherReconnectCts;
        private bool _needsRefreshOnActivation;
        /// <summary>裏タブ時にフォルダ変更があった場合、タブがフォーカスされたタイミングで最新化するためのフラグ。</summary>
        private bool _needsRefreshOnTabFocus;

        // タブ操作用コマンド
        public IRelayCommand? CloseTabCommand { get; set; } // 親から設定される想定

        /// <summary>親ペイン（タブ追加・ナビゲーション用）。FilePaneViewModel が設定する。</summary>
        public FilePaneViewModel? ParentPane { get; set; }

        public TabItemViewModel(string initialPath, FileViewMode? initialViewMode = null)
        {
            if (initialViewMode.HasValue)
                _fileViewMode = initialViewMode.Value;
            _currentPath = PathHelper.GetPhysicalPath(initialPath);
            ItemsView = CollectionViewSource.GetDefaultView(Items);
            SearchResultsView = CollectionViewSource.GetDefaultView(SearchResults);
            InitializeFileTypeFilters();
            SearchResultsView.Filter = IncludeSearchResultByFilter;
            SearchResults.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ShowEmptySearchResult));
                OnPropertyChanged(nameof(FilteredSearchResultCount));
            };

            App.Database.SearchHistoryChanged += OnSearchHistoryChanged;
            // コンストラクタでもロードしておくが、実際の表示時はView側で再ロード・待機する
            _ = LoadSearchHistoryAsync();

            // アプリのアクティブ状態を監視
            AppActivationService.Instance.ActivationChanged += OnAppActivationChanged;
            
            _indexingCts = new System.Threading.CancellationTokenSource();

            SetupWatcher();
            UpdatePathSegments();
            UpdateIsFavorite();
        }

        private void OnAppActivationChanged(object? sender, bool isActive)
        {
            // リフレッシュは App 側で一元化（アクティブタブ優先・他タブ順次でUI負荷を分散）
        }

        /// <summary>アクティブ化時にリフレッシュが必要か確認し、必要ならフラグをクリアして true を返す。</summary>
        internal bool TryConsumeRefreshOnActivation()
        {
            if (!_needsRefreshOnActivation) return false;
            _needsRefreshOnActivation = false;
            return true;
        }

        #region ナビゲーション

        [RelayCommand]
        private void ToggleLock()
        {
            IsLocked = !IsLocked;
            App.Notification.Notify(IsLocked ? "タブをロックしました" : "タブのロックを解除しました", $"タブロック: {(IsLocked ? "オン" : "オフ")} ({TabTitle})");
        }

        [RelayCommand]
        private void AddAllPaneTabsToFavorites()
        {
            var mainVM = MainVM;
            var pane = ParentPane;
            if (mainVM?.Favorites == null || pane == null) return;

            var paths = pane.Tabs
                .Select(t => t.CurrentPath)
                .Where(p => !string.IsNullOrWhiteSpace(p));

            mainVM.Favorites.AddPathsToNewFolder(paths);
        }

        [RelayCommand]
        private void MoveTabToOtherPane()
        {
            var mainVM = MainVM;
            if (mainVM == null || ParentPane == null) return;
            var targetPane = ParentPane == mainVM.LeftPane ? mainVM.RightPane : mainVM.LeftPane;
            mainVM.MoveTabToPane(this, targetPane);
        }

        [RelayCommand]
        private void Navigate(object? parameter)
        {
            string? path = parameter switch
            {
                string s => s,
                NavigationPathSegment seg => seg.FullPath,
                _ => null
            };
            _ = NavigateAsync(path, true);
        }

        public async Task NavigateAsync(string? path, bool saveToHistory = true)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // アドレスバーから "PC" と入力した場合に仮想 PC パスへ変換
            if (path.Trim().Equals("PC", StringComparison.OrdinalIgnoreCase))
                path = PathHelper.PCPath;

            // 仮想PCパス・UNCルートはディレクトリ存在チェックをスキップ
            if (!PathHelper.IsPCPath(path) && !await PathHelper.DirectoryExistsSafeAsync(path) && !PathHelper.IsUncRoot(path)) return;
            
            string normalizedPath = PathHelper.GetPhysicalPath(path);

            if (saveToHistory && !string.IsNullOrEmpty(CurrentPath))
            {
                _history.Push(CurrentPath);
                _forwardHistory.Clear();
                BackCommand.NotifyCanExecuteChanged();
                ForwardCommand.NotifyCanExecuteChanged();
            }
            CurrentPath = normalizedPath;
            await LoadDirectoryAsync();
            UpCommand.NotifyCanExecuteChanged();

            // クラウドフォルダの場合、シェル拡張メニューをプリフェッチしてキャッシュを温める
            var navSourceType = PathHelper.DetermineSourceType(normalizedPath);
            if (navSourceType == SourceType.Box || navSourceType == SourceType.SPO)
                CloudShellMenuService.PrefetchInBackground(normalizedPath);

            // 履歴の保存
            _ = App.Database.SaveHistoryAsync(normalizedPath, PathHelper.DetermineSourceType(normalizedPath));

            if (saveToHistory)
            {
                string displayName = PathHelper.IsPCPath(normalizedPath)
                    ? PathHelper.PCDisplayName
                    : Path.GetFileName(normalizedPath);
                if (string.IsNullOrEmpty(displayName)) displayName = normalizedPath;
                string paneLabel = ParentPane?.PaneLabel ?? "";
                string paneText = string.IsNullOrEmpty(paneLabel) ? "" : $"{paneLabel}ペインに";
                App.Notification.Notify($"{displayName} を{paneText}表示しました", $"フォルダを開く ({ParentPane?.PaneLabel ?? "?"}ペイン): {normalizedPath}");
            }
        }

        /// <summary>指定パス直下のサブフォルダ一覧を取得（パンくずドロップダウン用）。UIスレッドをブロックしない。</summary>
        public async Task<List<NavigationPathSegment>> GetSubfoldersAsync(string parentPath)
        {
            var list = new List<NavigationPathSegment>();
            if (string.IsNullOrWhiteSpace(parentPath)) return list;

            // 仮想 PC パスの場合はドライブ一覧を返す
            if (PathHelper.IsPCPath(parentPath))
            {
                return await Task.Run(() =>
                {
                    var drives = new List<NavigationPathSegment>();
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (!drive.IsReady) continue;
                        try
                        {
                            drives.Add(new NavigationPathSegment
                            {
                                Name = drive.RootDirectory.FullName.TrimEnd('\\'),
                                FullPath = drive.RootDirectory.FullName
                            });
                        }
                        catch { }
                    }
                    return drives;
                });
            }

            return await Task.Run(() =>
            {
                try
                {
                    string path = PathHelper.GetPhysicalPath(parentPath);
                    if (!Directory.Exists(path) && !PathHelper.IsUncRoot(path)) return list;

                    var options = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.System | FileAttributes.Temporary
                    };

                    foreach (string dir in Directory.EnumerateDirectories(path, "*", options))
                    {
                        try
                        {
                            string name = Path.GetFileName(dir);
                            if (string.IsNullOrEmpty(name)) continue;
                            list.Add(new NavigationPathSegment { Name = name, FullPath = dir, IsLast = false });
                        }
                        catch { /* スキップ */ }
                    }

                    list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                }
                catch { /* アクセス不可等 */ }
                return list;
            }).ConfigureAwait(false);
        }

        private void UpdatePathSegments()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentPath))
                {
                    PathSegments.Clear();
                    return;
                }

                var segments = new List<NavigationPathSegment>();

                // 仮想 PC パスの場合は「PC」のみ表示
                if (PathHelper.IsPCPath(CurrentPath))
                {
                    segments.Add(new NavigationPathSegment
                    {
                        Name = PathHelper.PCDisplayName,
                        FullPath = PathHelper.PCPath,
                        IsLast = true
                    });
                    PathSegments.Clear();
                    foreach (var seg in segments) PathSegments.Add(seg);
                    return;
                }

                if (CurrentPath.StartsWith(@"\\"))
                {
                    // UNCパスの処理
                    string[] parts = CurrentPath.Substring(2).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                    string currentBuildingPath = @"\\";
                    for (int i = 0; i < parts.Length; i++)
                    {
                        currentBuildingPath = i == 0 ? currentBuildingPath + parts[i] : Path.Combine(currentBuildingPath, parts[i]);
                        try
                        {
                            using (var item = new ShellItem(currentBuildingPath))
                            {
                                string displayName = item.Name ?? parts[i];
                                // OneDriveなどで GUID形式（::{...}）が返される場合は、フォルダ名を優先する
                                if (displayName.StartsWith("::"))
                                {
                                    displayName = parts[i];
                                }

                                segments.Add(new NavigationPathSegment
                                {
                                    Name = displayName,
                                    FullPath = currentBuildingPath
                                });
                            }
                        }
                        catch
                        {
                            segments.Add(new NavigationPathSegment
                            {
                                Name = parts[i],
                                FullPath = currentBuildingPath
                            });
                        }
                    }
                }
                else
                {
                    // ドライブレター等の処理
                    string[] parts = CurrentPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                    string currentBuildingPath = "";

                    if (CurrentPath.Contains(":"))
                    {
                        int colonIndex = CurrentPath.IndexOf(':');
                        currentBuildingPath = CurrentPath.Substring(0, colonIndex + 1) + Path.DirectorySeparatorChar;

                        try
                        {
                            using (var root = new ShellItem(currentBuildingPath))
                            {
                                string displayName = root.Name ?? currentBuildingPath;
                                // GUID形式（::{...}）が返される場合は、パス名を優先する
                                if (displayName.StartsWith("::"))
                                {
                                    displayName = currentBuildingPath;
                                }

                                segments.Add(new NavigationPathSegment
                                {
                                    Name = displayName,
                                    FullPath = currentBuildingPath
                                });
                            }
                        }
                        catch
                        {
                            segments.Add(new NavigationPathSegment
                            {
                                Name = currentBuildingPath,
                                FullPath = currentBuildingPath
                            });
                        }
                    }

                    string tempPath = currentBuildingPath;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].EndsWith(":")) continue;

                        tempPath = string.IsNullOrEmpty(tempPath) ? parts[i] : Path.Combine(tempPath, parts[i]);
                        try
                        {
                            using (var item = new ShellItem(tempPath))
                            {
                                string displayName = item.Name ?? parts[i];
                                // OneDriveなどで GUID形式（::{...}）が返される場合は、フォルダ名を優先する
                                if (displayName.StartsWith("::"))
                                {
                                    displayName = parts[i];
                                }

                                segments.Add(new NavigationPathSegment
                                {
                                    Name = displayName,
                                    FullPath = tempPath
                                });
                            }
                        }
                        catch
                        {
                            segments.Add(new NavigationPathSegment
                            {
                                Name = parts[i],
                                FullPath = tempPath
                            });
                        }
                    }
                }

                // ローカルパス（非UNC）の場合、先頭に PC セグメントを挿入
                if (!CurrentPath.StartsWith(@"\\"))
                {
                    segments.Insert(0, new NavigationPathSegment
                    {
                        Name = PathHelper.PCDisplayName,
                        FullPath = PathHelper.PCPath
                    });
                }

                if (segments.Count > 0) segments.Last().IsLast = true;

                // 一括で更新して通知を最小限にする
                PathSegments.Clear();
                foreach (var seg in segments) PathSegments.Add(seg);
            }
            catch
            {
                // フォールバック: 単純な文字列分割
                var segments = new List<NavigationPathSegment>();
                bool isUnc = CurrentPath.StartsWith(@"\\");
                string[] parts = (isUnc ? CurrentPath.Substring(2) : CurrentPath).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                string currentBuildingPath = isUnc ? @"\\" : "";

                for (int i = 0; i < parts.Length; i++)
                {
                    if (isUnc)
                    {
                        currentBuildingPath = i == 0 ? currentBuildingPath + parts[i] : Path.Combine(currentBuildingPath, parts[i]);
                    }
                    else
                    {
                        currentBuildingPath = i == 0 ? parts[i] : Path.Combine(currentBuildingPath, parts[i]);
                        if (i == 0 && currentBuildingPath.Length == 2 && currentBuildingPath[1] == ':') currentBuildingPath += Path.DirectorySeparatorChar;
                    }

                    segments.Add(new NavigationPathSegment
                    {
                        Name = parts[i],
                        FullPath = currentBuildingPath,
                        IsLast = i == parts.Length - 1
                    });
                }

                PathSegments.Clear();
                foreach (var seg in segments) PathSegments.Add(seg);
            }

            // パス変更直後のレイアウト連鎖による StackOverflow を防ぐため、表示セグメント更新を Loaded で遅延
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(() => UpdateVisibleSegments(_lastBreadcrumbWidth), DispatcherPriority.Loaded);
                // パンくず再描画を確実に発火させるため、PropertyChanged を手動通知
                dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(PathSegments)), DispatcherPriority.Render);
            }
            else
            {
                UpdateVisibleSegments(_lastBreadcrumbWidth);
            }
        }

        private double _lastBreadcrumbWidth;

        /// <summary>利用可能幅に応じて表示セグメントを更新（View から呼ぶ）</summary>
        public void UpdateVisibleSegments(double availableWidth)
        {
            _lastBreadcrumbWidth = availableWidth;
            if (PathSegments.Count == 0)
            {
                VisiblePathSegments.Clear();
                OverflowSegments.Clear();
                HasBreadcrumbOverflow = false;
                return;
            }

            if (availableWidth <= 0)
            {
                VisiblePathSegments.Clear();
                OverflowSegments.Clear();
                foreach (var s in PathSegments) VisiblePathSegments.Add(s);
                HasBreadcrumbOverflow = false;
                return;
            }

            const double overflowButtonWidth = 32;
            const double separatorWidth = 12;
            const double itemPadding = 16;
            double remainingWidth = availableWidth - overflowButtonWidth;

            double EstimateWidth(NavigationPathSegment seg)
            {
                if (seg.Name.Length == 0) return itemPadding;
                try
                {
                    var ft = new FormattedText(seg.Name, System.Globalization.CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, 1.0);
                    return ft.Width + itemPadding + separatorWidth;
                }
                catch { return seg.Name.Length * 8 + itemPadding + separatorWidth; }
            }

            var segmentWidths = PathSegments.Select(s => EstimateWidth(s)).ToList();
            int visibleCount = 0;
            double used = 0;

            for (int i = PathSegments.Count - 1; i >= 0; i--)
            {
                double w = segmentWidths[i];
                if (used + w > remainingWidth && visibleCount > 0) break;
                used += w;
                visibleCount++;
            }

            int overflowCount = PathSegments.Count - visibleCount;
            HasBreadcrumbOverflow = overflowCount > 0;

            OverflowSegments.Clear();
            for (int i = 0; i < overflowCount; i++)
                OverflowSegments.Add(PathSegments[i]);

            VisiblePathSegments.Clear();
            for (int i = overflowCount; i < PathSegments.Count; i++)
                VisiblePathSegments.Add(PathSegments[i]);

            if (VisiblePathSegments.Count > 0)
                VisiblePathSegments[^1].IsLast = true;
        }

        #endregion

        private void SetupWatcher()
        {
            try
            {
                _watcher?.Dispose();
                if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath)) return;

                _watcher = new FileSystemWatcher(CurrentPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 65536,
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileSystemChanged;
                _watcher.Deleted += OnFileSystemChanged;
                _watcher.Changed += OnFileSystemChanged;
                _watcher.Renamed += OnFileSystemChanged;
                _watcher.Error += OnFileSystemWatcherError;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[WARN][SetupWatcher] 監視設定失敗: '{CurrentPath}' {ex.Message}");
            }
        }

        private void OnFileSystemWatcherError(object sender, ErrorEventArgs e)
        {
            var ex = e.GetException();
            var hresult = ex is System.ComponentModel.Win32Exception w32 ? w32.NativeErrorCode : ex.HResult;
            var path = CurrentPath; // スレッドプールで安全に参照できるようキャプチャ
            _ = App.FileLogger.LogAsync(
                $"[WARN][FileSystemWatcher] 監視エラー発生 Path='{path}' {ex.GetType().Name}: {ex.Message} HResult=0x{hresult:X8} Attempt={_watcherReconnectAttempt}");

            // 前回のバックオフタスクをキャンセル（CTS の原子的スワップ）
            var oldCts = Interlocked.Exchange(ref _watcherReconnectCts, new CancellationTokenSource());
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch (ObjectDisposedException) { }
                oldCts.Dispose();
            }
            var ct = _watcherReconnectCts?.Token ?? CancellationToken.None;

            var attempt = Interlocked.Increment(ref _watcherReconnectAttempt);

            if (attempt > MaxWatcherReconnectAttempts)
            {
                _watcherDisconnected = true;
                _ = App.FileLogger.LogAsync($"[ERR][FileSystemWatcher] 再接続上限到達 Path='{path}' — 手動 F5 待ち");
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                    App.Notification.Notify("フォルダ監視が停止しました", $"再接続に失敗しました。F5 で更新してください: {path}"));
                return;
            }

            if (attempt == 1)
            {
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                    App.Notification.Notify("ネットワーク切断を検出。再接続を試行中...", $"フォルダ監視: {path}"));
            }

            var delaySec = Math.Min(1 << Math.Min(attempt - 1, 30), 30); // 整数ビットシフトで Pow 代替
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                    // UI スレッドをブロックしない BeginInvoke を使用
                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        try
                        {
                            SetupWatcher();
                            Interlocked.Exchange(ref _watcherReconnectAttempt, 0);
                            _watcherDisconnected = false;
                            _ = App.FileLogger.LogAsync($"[DEBUG][FileSystemWatcher] 監視再セットアップ成功: '{path}' (attempt={attempt})");
                            App.Notification.Notify("フォルダ監視を再開しました", $"再接続成功: {path}");
                        }
                        catch (Exception setupEx)
                        {
                            _ = App.FileLogger.LogAsync(
                                $"[ERR][FileSystemWatcher] 監視再セットアップ失敗: '{path}' {setupEx.GetType().Name}: {setupEx.Message} HResult=0x{setupEx.HResult:X8} Attempt={attempt}");
                        }
                    });
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
            });
        }

        private System.Threading.CancellationTokenSource? _watcherCts;

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // FileSystemWatcherのイベントはスレッドプールスレッドで発生するため、
            // UIスレッドが所有するオブジェクト（MainVM、ParentPane等）にアクセスする前に
            // UIスレッドにディスパッチする必要がある
            // イベント引数の情報を先にキャプチャ（値型や文字列なので安全）
            var changeType = e.ChangeType;
            var fullPath = e.FullPath;
            var name = e.Name ?? Path.GetFileName(e.FullPath);
            string? oldFullPath = null;
            if (e is RenamedEventArgs re)
            {
                oldFullPath = re.OldFullPath;
            }

            // UIスレッドにディスパッチしてから処理を実行
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                // シェル（エクスプローラ）のコンテキストメニュー等から「新しいフォルダ」が作成された場合、
                // そのフォルダにフォーカスを当ててリネームを開始するようにフラグを立てる。
                if (changeType == WatcherChangeTypes.Created && Directory.Exists(fullPath))
                {
                    if (IsExpectingShellChange && IsDefaultNewFolderName(name))
                    {
                        RequestFocusAfterRefresh = true;
                        PastedFileNamesToSelect = new List<string> { name };
                        RequestRenameAfterFocusRestore = true;
                        IsExpectingShellChange = false;
                    }
                }

                // インデックスの差分更新（Auto モード時のみ。Interval/Manual は定例または手動トリガーのみ）
                if (App.IndexService.CurrentUpdateMode == IndexUpdateMode.Auto)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            if (changeType == WatcherChangeTypes.Deleted)
                            {
                                App.IndexService.RemoveFileFromIndex(fullPath);
                            }
                            else if (changeType == WatcherChangeTypes.Created || changeType == WatcherChangeTypes.Changed)
                            {
                                App.IndexService.AddFileToIndex(fullPath);
                            }
                            else if (changeType == WatcherChangeTypes.Renamed && oldFullPath != null)
                            {
                                App.IndexService.RemoveFileFromIndex(oldFullPath);
                                App.IndexService.AddFileToIndex(fullPath);
                            }
                        }
                        catch { /* ログなど */ }
                    });
                }

                // シェルコンテキストメニューからの変更を期待している場合は、
                // アプリが非アクティブでも即時リフレッシュする（STA スレッドのメニュー表示中は Deactivated になるため）
                bool forceRefresh = IsExpectingShellChange;

                // アプリが非アクティブな間は監視通知を記録するだけにとどめ、UI更新を抑制する
                if (!forceRefresh && !AppActivationService.Instance.IsActive)
                {
                    _needsRefreshOnActivation = true;
                    return;
                }

                // 表示中タブのみ即時更新、裏タブはフォーカス時に更新（負荷軽減）
                // 2画面表示時はA・B両ペインの表示タブを最新化対象とする
                bool isVisibleTab = ParentPane != null && ParentPane.SelectedTab == this
                    && (ParentPane.IsActive || MainVM?.PaneCount == 2);
                if (!forceRefresh && !isVisibleTab)
                {
                    _needsRefreshOnTabFocus = true;
                    return;
                }

                // スロットリング: ウィンドウ内の初回イベントのみ Refresh をスケジュールする。
                // すでにスケジュール済み（_watcherCts != null）の場合は何もしない。
                // デバウンスと異なり、イベントが連続しても必ず一定時間以内に Refresh が実行されるため、
                // 大量ファイル転送中でもコピー先の一覧がリアルタイム更新される。
                // クラウド同期パスでは遅延を拡大（1000ms）し、同期ソフトの作成→削除→再作成サイクルを吸収する。
                if (_watcherCts == null)
                {
                    bool isCloudPath = PathHelper.IsCloudSyncedPath(CurrentPath);
                    int throttleMs = isCloudPath ? 1000 : 500;
                    _watcherCts = new System.Threading.CancellationTokenSource();
                    var token = _watcherCts.Token;
                    Task.Delay(throttleMs, token).ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully && !token.IsCancellationRequested)
                        {
                            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                            {
                                _watcherCts?.Dispose();
                                _watcherCts = null;
                                Refresh();
                            }), DispatcherPriority.Background);
                        }
                    }, TaskScheduler.Default);
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// タブがフォーカスされたときに、裏でフォルダ変更があった場合、または未ロードの裏タブ（復元時など）の場合は一覧を最新化する。
        /// 通常のフォルダタブのみ対象（検索結果タブはスキップ）。
        /// </summary>
        internal void RefreshIfNeededOnTabFocus()
        {
            if (IsSearchResultTab) return;
            bool needsRefresh = _needsRefreshOnTabFocus;
            if (needsRefresh) _needsRefreshOnTabFocus = false;
            bool neverLoaded = !string.IsNullOrEmpty(CurrentPath) && Items.Count == 0;
            if (needsRefresh || neverLoaded)
                _ = LoadDirectoryAsync();
        }

        /// <summary>
        /// 指定された名前が、OSのデフォルトの「新しいフォルダ」パターンに一致するか判定します。
        /// </summary>
        private bool IsDefaultNewFolderName(string name)
        {
            // 日本語: "新しいフォルダ" (当アプリ) または "新しいフォルダー" (Windows標準)
            if (name.StartsWith("新しいフォルダ") || name.StartsWith("新しいフォルダー")) return true;
            // 英語: "New folder"
            if (name.StartsWith("New folder", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        partial void OnCurrentPathChanged(string value)
        {
            // パス変更時は検索を解除して通常のファイル一覧に戻す
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                SearchText = string.Empty;
            }
            // インデックス作成をキャンセル
            _indexingCts?.Cancel();
            _indexingCts = new System.Threading.CancellationTokenSource();

            SetupWatcher();
            IsExpectingShellChange = false;
            UpdatePathSegments();
            UpdateIsFavorite();
            OpenCurrentFolderInExplorerCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void CutItems(IList? items)
        {
            if (items == null || items.Count == 0) return;
            var paths = new System.Collections.Specialized.StringCollection();
            foreach (FileItem item in items) paths.Add(item.FullPath);
            
            DataObject data = new DataObject();
            data.SetFileDropList(paths);
            
            // "Preferred DropEffect" を設定して切り取り（移動）であることを示す
            // 2 = Move, 5 = Copy
            byte[] moveEffect = { 2, 0, 0, 0 };
            MemoryStream ms = new MemoryStream(moveEffect);
            data.SetData("Preferred DropEffect", ms);
            
            Clipboard.SetDataObject(data);
            App.Notification.Notify($"{items.Count} 件を切り取りました", $"切り取り: {string.Join(", ", items.Cast<FileItem>().Select(x => x.FullPath))}");
        }

        [RelayCommand]
        private async Task RenameItem(FileItem? item)
        {
            if (item == null) return;

            // 新規作成フローからの呼び出しかどうかをフラグで判定（パス比較に依存しない確実なロールバック判定）
            bool isNewItem = _isRenameForNewItem;
            _isRenameForNewItem = false;   // 判定後は必ずクリア
            _pendingNewFolderPath = null;

            // ファイルの場合は拡張子以外を選択し、確定時に拡張子は変更不可とする
            bool selectNameOnly = !item.IsDirectory;
            string parentDir = Path.GetDirectoryName(item.FullPath) ?? string.Empty;
            string? inputText = RenameDialog.ShowDialog("名前の変更", item.Name, parentDir, selectNameWithoutExtension: selectNameOnly);
            if (inputText != null)
            {
                string trimmedInput = inputText.Trim();
                string newName = selectNameOnly
                    ? trimmedInput + Path.GetExtension(item.Name)
                    : trimmedInput;
                // ファイルで名前部分が空の場合は変更しない
                if (selectNameOnly && string.IsNullOrWhiteSpace(trimmedInput))
                    return;
                if (string.IsNullOrWhiteSpace(newName))
                    return;

                if (newName != item.Name)
                {
                    // 名前が変更された場合: リネーム実行 → 新名前で再選択
                    string newPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
                    try
                    {
                        if (item.IsDirectory)
                        {
                            Services.FileIoRetryHelper.MoveDirectory(item.FullPath, newPath);
                        }
                        else
                        {
                            Services.FileIoRetryHelper.MoveFile(item.FullPath, newPath);
                        }

                        // Undo登録
                        UndoService.Instance.Register(new RenameUndoCommand(item.FullPath, newPath, item.IsDirectory));

                        // リネーム後: 新しい名前でアイテムを選択・スクロール・フォーカスする
                        // （名前変更で並び順が変わるため、新名前で再検索が必要）
                        RequestFocusAfterRefresh = true;
                        PastedFileNamesToSelect = new List<string> { newName };
                        await LoadDirectoryAsync();

                        if (item.IsDirectory)
                            Services.FileOperationService.Instance.RaiseFolderRenamed(item.FullPath, newPath);
                        App.Notification.Notify("名前を変更しました", $"[{ParentPane?.PaneLabel ?? "?"}] Rename: '{item.Name}' → '{newName}'");
                    }
                    catch (Exception ex)
                    {
                        var friendlyMsg = Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, item.FullPath);
                        App.Notification.Notify("名前の変更に失敗しました", $"[ERR][{ParentPane?.PaneLabel ?? "?"}] Rename: '{item.Name}' - {ex.Message}");
                        ZenithDialog.Show($"名前の変更に失敗しました。\n{friendlyMsg}", "名前の変更", ZenithDialogButton.OK, ZenithDialogIcon.Error);
                    }
                }
                else
                {
                    // 名前を変更せず OK で確定: アイテムを選択・スクロール・フォーカスする
                    RequestFocusAfterRefresh = true;
                    PastedFileNamesToSelect = new List<string> { item.Name };
                    await LoadDirectoryAsync();
                }
            }
            else if (isNewItem)
            {
                // 新規作成直後のリネームがキャンセルされた場合、作成したアイテムを削除してロールバックする
                try
                {
                    if (item.IsDirectory && Directory.Exists(item.FullPath))
                    {
                        Directory.Delete(item.FullPath, recursive: false);
                        await LoadDirectoryAsync();
                        App.Notification.Notify("フォルダ作成をキャンセルしました", $"新規フォルダを削除しました: {item.FullPath}");
                    }
                    else if (!item.IsDirectory && File.Exists(item.FullPath))
                    {
                        File.Delete(item.FullPath);
                        await LoadDirectoryAsync();
                        App.Notification.Notify("ファイル作成をキャンセルしました", $"新規ファイルを削除しました: {item.FullPath}");
                    }
                }
                catch (Exception ex)
                {
                    App.Notification.Notify("キャンセル時の削除に失敗しました", $"削除失敗: {item.FullPath} 詳細: {ex}");
                }
            }
            else
            {
                // キャンセル（既存アイテムのリネーム中止）: 元のアイテムにフォーカスを戻す
                RequestFocusAfterRefresh = true;
                PastedFileNamesToSelect = new List<string> { item.Name };
                await LoadDirectoryAsync();
            }
        }

        [RelayCommand]
        private async Task CreateNewFolder()
        {
            string basePath = PathHelper.GetPhysicalPath(CurrentPath);
            if (string.IsNullOrEmpty(basePath))
                return;
            if (PathHelper.IsUncRoot(basePath))
            {
                ZenithDialog.Show("この場所にはフォルダを作成できません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }
            if (!Directory.Exists(basePath))
            {
                ZenithDialog.Show("現在のフォルダが存在しません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }

            bool isCloudSynced = PathHelper.IsCloudSyncedPath(basePath);
            _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFolder] 開始: basePath='{basePath}' isCloudSynced={isCloudSynced}");

            // デフォルト名でフォルダを即時作成し、リフレッシュ後にリネームダイアログを自動表示する
            string defaultName = "新しいフォルダ";
            int n = 1;
            while (Directory.Exists(Path.Combine(basePath, defaultName)))
            {
                n++;
                defaultName = $"新しいフォルダ ({n})";
            }

            string newDir = Path.Combine(basePath, defaultName);
            _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFolder] 作成パス決定: '{newDir}'");

            try
            {
                using var busyScope = MainVM?.BeginBusy();

                // リトライ付きディレクトリ作成（クラウド同期環境では強化リトライ + 存在確認）
                await FileIoRetryHelper.CreateDirectoryWithRetryAsync(newDir, isCloudSynced);
                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFolder] ディレクトリ作成成功: '{newDir}'");

                // クラウド同期パスの場合、同期ソフトの処理完了を待つための安定化ディレイ
                if (isCloudSynced)
                {
                    _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFolder] クラウド同期ディレイ (300ms)");
                    await Task.Delay(300);
                }

                // Undo登録およびフラグ設定は UI スレッドで行う
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UndoService.Instance.Register(new CreateFolderUndoCommand(newDir));
                    Services.FileOperationService.Instance.RaiseFolderCreated(basePath, newDir);
                    App.Notification.Notify("新しいフォルダを作成しました", $"[{ParentPane?.PaneLabel ?? "?"}] NewFolder: '{Path.GetFileName(newDir)}'");

                    string createdFolderName = Path.GetFileName(newDir);
                    _pendingNewFolderPath = newDir;        // キャンセル時の削除対象パスを保持
                    _isRenameForNewItem = true;            // 新規作成フローからの呼び出しを示すフラグ
                    RequestFocusAfterRefresh = true;
                    RequestRenameAfterFocusRestore = true; // リフレッシュ後にリネームダイアログを自動表示
                    PastedFileNamesToSelect = new List<string> { createdFolderName };
                });

                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFolder] LoadDirectoryAsync 開始");

                // リフレッシュ完了を待機: MergeItems 完了後に RefreshCompleted が発火し、
                // View の ApplyFocusAfterRefresh でアイテム選択・スクロール・リネームが実行される
                await LoadDirectoryAsync();
                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFolder] LoadDirectoryAsync 完了 → UI反映済み");
            }
            catch (Exception ex)
            {
                string logMsg = FileIoRetryHelper.FormatIoException(ex, "CreateNewFolder", newDir);
                _ = App.FileLogger.LogAsync(logMsg);
                App.Notification.Notify("フォルダ作成に失敗しました", $"フォルダ作成失敗: '{newDir}' 詳細: {ex}");

                string userMessage = isCloudSynced
                    ? $"フォルダの作成に失敗しました。\nOneDrive 等の同期ソフトがファイルをロックしている可能性があります。\nしばらく待ってから再試行してください。\n\n詳細: {ex.Message}"
                    : $"フォルダの作成に失敗しました。\n{ex.Message}";
                ZenithDialog.Show(userMessage, "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
            }
        }

        [RelayCommand]
        private async Task CreateNewTextFile()
        {
            string basePath = PathHelper.GetPhysicalPath(CurrentPath);
            if (string.IsNullOrEmpty(basePath))
                return;
            if (PathHelper.IsUncRoot(basePath))
            {
                ZenithDialog.Show("この場所にはファイルを作成できません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }
            if (!Directory.Exists(basePath))
            {
                ZenithDialog.Show("現在のフォルダが存在しません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }

            bool isCloudSynced = PathHelper.IsCloudSyncedPath(basePath);
            _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewTextFile] 開始: basePath='{basePath}' isCloudSynced={isCloudSynced}");

            // デフォルト名でファイルを即時作成し、リフレッシュ後にリネームダイアログを自動表示する
            string defaultName = "新しいテキストファイル.txt";
            int n = 1;
            while (File.Exists(Path.Combine(basePath, defaultName)))
            {
                n++;
                defaultName = $"新しいテキストファイル ({n}).txt";
            }

            string newFilePath = Path.Combine(basePath, defaultName);
            _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewTextFile] 作成パス決定: '{newFilePath}'");

            try
            {
                using var busyScope = MainVM?.BeginBusy();

                // リトライ付きファイル作成（クラウド同期環境では強化リトライ + 存在確認）
                await FileIoRetryHelper.CreateFileWithRetryAsync(newFilePath, isCloudSynced);
                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewTextFile] ファイル作成成功: '{newFilePath}'");

                // クラウド同期パスの場合、同期ソフトの処理完了を待つための安定化ディレイ
                if (isCloudSynced)
                {
                    _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewTextFile] クラウド同期ディレイ (300ms)");
                    await Task.Delay(300);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UndoService.Instance.Register(new CreateFileUndoCommand(newFilePath));
                    App.Notification.Notify("新しいテキストファイルを作成しました", $"[{ParentPane?.PaneLabel ?? "?"}] NewFile: '{Path.GetFileName(newFilePath)}'");

                    string createdFileName = Path.GetFileName(newFilePath);
                    _isRenameForNewItem = true;            // 新規作成フローからの呼び出しを示すフラグ
                    RequestFocusAfterRefresh = true;
                    RequestRenameAfterFocusRestore = true; // リフレッシュ後にリネームダイアログを自動表示
                    PastedFileNamesToSelect = new List<string> { createdFileName };
                });

                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewTextFile] LoadDirectoryAsync 開始");

                // リフレッシュ完了を待機: MergeItems 完了後に RefreshCompleted が発火し、
                // View の ApplyFocusAfterRefresh でアイテム選択・スクロール・リネームが実行される
                await LoadDirectoryAsync();
                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewTextFile] LoadDirectoryAsync 完了 → UI反映済み");
            }
            catch (Exception ex)
            {
                string logMsg = FileIoRetryHelper.FormatIoException(ex, "CreateNewTextFile", newFilePath);
                _ = App.FileLogger.LogAsync(logMsg);
                App.Notification.Notify("ファイル作成に失敗しました", $"ファイル作成失敗: '{newFilePath}' 詳細: {ex}");

                string userMessage = isCloudSynced
                    ? $"ファイルの作成に失敗しました。\nOneDrive 等の同期ソフトがファイルをロックしている可能性があります。\nしばらく待ってから再試行してください。\n\n詳細: {ex.Message}"
                    : $"ファイルの作成に失敗しました。\n{ex.Message}";
                ZenithDialog.Show(userMessage, "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
            }
        }

        /// <summary>ShellNew テンプレートから新しいファイルを作成する。</summary>
        public async Task CreateNewFileFromShellNewAsync(ShellNewItem shellNewItem)
        {
            string basePath = PathHelper.GetPhysicalPath(CurrentPath);
            if (string.IsNullOrEmpty(basePath))
                return;
            if (PathHelper.IsUncRoot(basePath))
            {
                ZenithDialog.Show("この場所にはファイルを作成できません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }
            if (!Directory.Exists(basePath))
            {
                ZenithDialog.Show("現在のフォルダが存在しません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }

            bool isCloudSynced = PathHelper.IsCloudSyncedPath(basePath);
            _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFileFromShellNew] 開始: basePath='{basePath}' ext='{shellNewItem.Extension}'");

            // デフォルト名の生成
            string baseName = $"新しい {shellNewItem.DisplayName}";
            string defaultName = baseName + shellNewItem.Extension;
            int n = 1;
            while (File.Exists(Path.Combine(basePath, defaultName)))
            {
                n++;
                defaultName = $"{baseName} ({n}){shellNewItem.Extension}";
            }

            string newFilePath = Path.Combine(basePath, defaultName);

            try
            {
                using var busyScope = MainVM?.BeginBusy();

                // テンプレートの種類に応じてファイル作成
                if (shellNewItem.TemplateFilePath != null && File.Exists(shellNewItem.TemplateFilePath))
                {
                    await Task.Run(() => File.Copy(shellNewItem.TemplateFilePath, newFilePath));
                }
                else if (shellNewItem.TemplateData != null)
                {
                    await Task.Run(() => File.WriteAllBytes(newFilePath, shellNewItem.TemplateData));
                }
                else
                {
                    // NullFile またはフォールバック: 空ファイル作成
                    await FileIoRetryHelper.CreateFileWithRetryAsync(newFilePath, isCloudSynced);
                }

                _ = App.FileLogger.LogAsync($"[DEBUG][CreateNewFileFromShellNew] ファイル作成成功: '{newFilePath}'");

                // クラウド同期パスの場合、同期ソフトの処理完了を待つための安定化ディレイ
                if (isCloudSynced)
                {
                    await Task.Delay(300);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UndoService.Instance.Register(new CreateFileUndoCommand(newFilePath));
                    App.Notification.Notify($"新しい{shellNewItem.DisplayName}を作成しました",
                        $"[{ParentPane?.PaneLabel ?? "?"}] NewFile: '{Path.GetFileName(newFilePath)}'");

                    string createdFileName = Path.GetFileName(newFilePath);
                    _isRenameForNewItem = true;
                    RequestFocusAfterRefresh = true;
                    RequestRenameAfterFocusRestore = true;
                    PastedFileNamesToSelect = new List<string> { createdFileName };
                });

                await LoadDirectoryAsync();
            }
            catch (Exception ex)
            {
                string logMsg = FileIoRetryHelper.FormatIoException(ex, "CreateNewFileFromShellNew", newFilePath);
                _ = App.FileLogger.LogAsync(logMsg);
                App.Notification.Notify("ファイル作成に失敗しました", $"ファイル作成失敗: '{newFilePath}' 詳細: {ex}");

                string userMessage = isCloudSynced
                    ? $"ファイルの作成に失敗しました。\nOneDrive 等の同期ソフトがファイルをロックしている可能性があります。\nしばらく待ってから再試行してください。\n\n詳細: {ex.Message}"
                    : $"ファイルの作成に失敗しました。\n{ex.Message}";
                ZenithDialog.Show(userMessage, "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
            }
        }

        [RelayCommand]
        private void CreateShortcut(IList? items)
        {
            if (items == null || items.Count == 0) return;
            
            object? shell = null;
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                
                shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                dynamic dShell = shell;
                string targetDir = PathHelper.GetPhysicalPath(CurrentPath);

                foreach (FileItem item in items)
                {
                    string shortcutPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(item.Name) + " - ショートカット.lnk");
                    int count = 1;
                    while (File.Exists(shortcutPath))
                    {
                        shortcutPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(item.Name) + $" - ショートカット ({count}).lnk");
                        count++;
                    }

                    object? shortcut = null;
                    try
                    {
                        shortcut = dShell.CreateShortcut(shortcutPath);
                        if (shortcut != null)
                        {
                            dynamic dShortcut = shortcut;
                            dShortcut.TargetPath = item.FullPath;
                            dShortcut.Save();
                        }
                    }
                    finally
                    {
                        if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                    }
                }
                Refresh();
                App.Notification.Notify($"{items.Count} 件のショートカットを作成しました",
                    $"{items.Count} 件のショートカットを作成しました。Sources: {string.Join(", ", items.Cast<FileItem>().Select(x => x.FullPath))}");
            }
            catch (Exception ex)
            {
                App.Notification.Notify("ショートカット作成に失敗しました", $"ショートカット作成失敗 詳細: {ex}");
                ZenithDialog.Show($"ショートカットの作成に失敗しました。\n{ex.Message}", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }
            finally
            {
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }

        [RelayCommand]
        private void ShowProperties(FileItem? item)
        {
            if (item == null) return;
            ShellIconHelper.ShowFileProperties(item.FullPath);
            App.Notification.Notify("プロパティを表示しました", $"プロパティ表示: {item.FullPath}");
        }

        [RelayCommand]
        private void PasteItems()
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                var fileArray = new string[files.Count];
                files.CopyTo(fileArray, 0);

                bool isMove = false;
                var data = Clipboard.GetDataObject();
                if (data != null && data.GetDataPresent("Preferred DropEffect"))
                {
                    var stream = data.GetData("Preferred DropEffect") as MemoryStream;
                    if (stream != null)
                    {
                        byte[] buffer = new byte[4];
                        stream.Read(buffer, 0, 4);
                        // 2 = Move, 5 = Copy
                        if (buffer[0] == 2) isMove = true;
                    }
                }

                _ = DropFilesInternal(fileArray, isMove);
            }
        }

        [RelayCommand]
        private async Task JumpToPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // パス解決
            string physicalPath = PathHelper.GetPhysicalPath(path);
            bool isFolder = Directory.Exists(physicalPath);
            bool isFile = File.Exists(physicalPath);
            if (!isFolder && !isFile) return;

            var behavior = MainVM?.AppSettings?.SearchResultPathBehavior ?? SearchResultPathBehavior.SamePaneNewTab;

            if (behavior == SearchResultPathBehavior.SameTab)
            {
                // 同一タブ: 検索をクリアし、現在タブで表示
                SearchText = string.Empty;
                if (isFolder)
                {
                    await NavigateAsync(physicalPath, true);
                }
                else
                {
                    string? parent = Path.GetDirectoryName(physicalPath);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    {
                        RequestFocusAfterRefresh = true;
                        PastedFileNamesToSelect = new List<string> { Path.GetFileName(physicalPath) };
                        await NavigateAsync(parent, true);
                    }
                }
            }
            else if (behavior == SearchResultPathBehavior.OtherPaneNewTab && ParentPane != null)
            {
                MainVM?.OpenPathInOtherPane(ParentPane, physicalPath, isFile);
            }
            else
            {
                // SamePaneNewTab（従来の挙動）
                if (isFolder)
                {
                    ParentPane?.AddTabWithPathCommand.Execute(physicalPath);
                    App.Notification.Notify("新しいタブでフォルダを開きました", $"新しいタブで開く: {physicalPath}");
                }
                else
                {
                    string? parent = Path.GetDirectoryName(physicalPath);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    {
                        ParentPane?.AddTabWithPathCommand.Execute(parent);
                        if (ParentPane?.SelectedTab is TabItemViewModel newTab)
                        {
                            newTab.RequestFocusAfterRefresh = true;
                            newTab.PastedFileNamesToSelect = new List<string> { Path.GetFileName(physicalPath) };
                        }
                        App.Notification.Notify("新しいタブでフォルダを開きました", $"新しいタブで開く: {parent}");
                    }
                }
            }
        }

        [RelayCommand]
        private void Sort(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            if (IsSearching)
            {
                // 検索結果ビューのソート状態を更新
                if (SearchSortProperty == propertyName)
                    SearchSortDirection = SearchSortDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending : ListSortDirection.Ascending;
                else
                {
                    SearchSortProperty = propertyName;
                    SearchSortDirection = ListSortDirection.Ascending;
                }
                App.Notification.Notify($"並び替え: {propertyName}（{(SearchSortDirection == ListSortDirection.Ascending ? "昇順" : "降順")}）",
                    $"並び替え: {SearchSortProperty} {(SearchSortDirection == ListSortDirection.Ascending ? "昇順" : "降順")}");
            }
            else
            {
                // フォルダビューのソート状態を更新
                if (SortProperty == propertyName)
                    SortDirection = SortDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending : ListSortDirection.Ascending;
                else
                {
                    SortProperty = propertyName;
                    SortDirection = ListSortDirection.Ascending;
                }
                App.Notification.Notify($"並び替え: {propertyName}（{(SortDirection == ListSortDirection.Ascending ? "昇順" : "降順")}）",
                    $"並び替え: {SortProperty} {(SortDirection == ListSortDirection.Ascending ? "昇順" : "降順")}");
            }

            ApplySort();
        }

        private void ApplySort()
        {
            // SortDescriptions の代わりに ListCollectionView.CustomSort を使用することで、
            // Windows Shell 自然順ソート（StrCmpLogicalW）と
            // ベースファイル優先ロジック（_t1, -copy 等の接尾語付きより先頭に表示）を適用する。
            if (ItemsView is ListCollectionView lcvItems)
                lcvItems.CustomSort = new FileItemComparer(SortProperty, SortDirection, IsGroupFoldersFirst);

            // 検索結果ビューは SearchSortProperty/SearchSortDirection（デフォルト: 更新日時 降順）を使用する。
            if (SearchResultsView is ListCollectionView lcvSearch)
                lcvSearch.CustomSort = new FileItemComparer(SearchSortProperty, SearchSortDirection, IsGroupFoldersFirst);
        }

        [RelayCommand]
        private void Back()
        {
            if (_history.Count > 0)
            {
                PathToFocusAfterNavigation = CurrentPath;  // 戻り先一覧に存在する「入ったフォルダ」のパス
                _forwardHistory.Push(CurrentPath);
                var path = _history.Pop();
                _ = NavigateAsync(path, saveToHistory: false);  // 履歴操作時は上書きしない
                BackCommand.NotifyCanExecuteChanged();
                ForwardCommand.NotifyCanExecuteChanged();
                App.Notification.Notify("戻りました", $"履歴で戻る: {path}");
            }
        }

        private bool CanBack() => _history.Count > 0;

        [RelayCommand]
        private void Forward()
        {
            if (_forwardHistory.Count > 0)
            {
                _history.Push(CurrentPath);
                var path = _forwardHistory.Pop();
                _ = NavigateAsync(path, saveToHistory: false);  // 履歴操作時は上書きしない
                BackCommand.NotifyCanExecuteChanged();
                ForwardCommand.NotifyCanExecuteChanged();
                App.Notification.Notify("進みました", $"履歴で進む: {path}");
            }
        }

        private bool CanForward() => _forwardHistory.Count > 0;

        [RelayCommand]
        private void Up()
        {
            if (PathHelper.IsPCPath(CurrentPath)) return; // 既に最上位

            var parent = Path.GetDirectoryName(CurrentPath);
            if (parent != null)
            {
                PathToFocusAfterNavigation = CurrentPath;
                Navigate(parent);
                App.Notification.Notify("上のフォルダに移動しました", $"上へ: {parent}");
            }
            else
            {
                // ドライブルート → PC へ
                PathToFocusAfterNavigation = CurrentPath;
                Navigate(PathHelper.PCPath);
                App.Notification.Notify("PC に移動しました", "上へ: PC");
            }
        }

        private bool CanUp() => !string.IsNullOrEmpty(CurrentPath) && !PathHelper.IsPCPath(CurrentPath);

        [RelayCommand]
        private void NavigateToDesktop()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Navigate(desktop);
        }

        [RelayCommand]
        private void NavigateToDownloads()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads");
            Navigate(downloads);
        }

        [ObservableProperty]
        private bool _isCurrentPathFavorite;

        [ObservableProperty]
        private bool _isFavoriteActionActive;

        [ObservableProperty]
        private bool _isFavoriteActionFailed;

        [RelayCommand]
        private async Task AddToFavorites()
        {
            if (Application.Current.MainWindow?.DataContext is not MainViewModel vm)
                return;

            // パスが空の場合
            if (string.IsNullOrWhiteSpace(CurrentPath))
            {
                await ShowFavoriteFailure("パスが指定されていないため登録できません",
                    "[ERR][Favorites] 登録失敗: パスが空");
                return;
            }

            // パスが存在しない場合
            if (!Directory.Exists(CurrentPath) && !File.Exists(CurrentPath) && !PathHelper.IsUncRoot(CurrentPath))
            {
                await ShowFavoriteFailure($"パスが見つからないため登録できません:\n{CurrentPath}",
                    $"[ERR][Favorites] 登録失敗: パスが存在しない ({CurrentPath})");
                return;
            }

            // 既に登録済みの場合 — FavoritesViewModel.NotifyDuplicate に委譲（ログは 1 件のみ）
            if (vm.Favorites.ContainsPath(CurrentPath))
            {
                IsFavoriteActionFailed = true;
                vm.Favorites.NotifyDuplicate(CurrentPath);
                await Task.Delay(1500);
                IsFavoriteActionFailed = false;
                return;
            }

            // 正常な登録フロー
            await vm.Favorites.AddPathWithDialogAndHighlightAsync(CurrentPath);
            UpdateIsFavorite();
        }

        /// <summary>お気に入り登録失敗時のフィードバック（赤シェイクアニメーション + ステータスバー通知 + ログ記録）。パス空・存在しない等の事前バリデーション失敗用。</summary>
        private async Task ShowFavoriteFailure(string reason, string logMessage)
        {
            IsFavoriteActionFailed = true;
            App.Notification.Notify("お気に入り登録に失敗しました", logMessage);
            _ = App.FileLogger.LogAsync(logMessage);
            ZenithDialog.Show(reason, "お気に入り", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
            await Task.Delay(1500);
            IsFavoriteActionFailed = false;
        }

        internal void UpdateIsFavorite()
        {
            if (Application.Current?.MainWindow?.DataContext is MainViewModel vm)
            {
                IsCurrentPathFavorite = vm.Favorites.ContainsPath(CurrentPath);
            }
        }

        [RelayCommand(CanExecute = nameof(CanOpenCurrentFolderInExplorer))]
        private void OpenCurrentFolderInExplorer()
        {
            try
            {
                if (!string.IsNullOrEmpty(CurrentPath) && (Directory.Exists(CurrentPath) || PathHelper.IsUncRoot(CurrentPath)))
                {
                    System.Diagnostics.Process.Start("explorer.exe", CurrentPath);
                    App.Notification.Notify("エクスプローラーで開きました", $"エクスプローラーで開く: {CurrentPath}");
                }
            }
            catch (Exception ex)
            {
                App.Notification.Notify("エクスプローラーで開けませんでした", $"エクスプローラーで開く失敗: {CurrentPath} 詳細: {ex}");
                ZenithDialog.Show($"エクスプローラーで開けませんでした。\n{ex.Message}", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }
        }

        private bool CanOpenCurrentFolderInExplorer() =>
            !string.IsNullOrEmpty(CurrentPath) &&
            (Directory.Exists(CurrentPath) || PathHelper.IsUncRoot(CurrentPath));

        [RelayCommand]
        private void ImportFromExplorer()
        {
            var list = ExplorerWindowHelper.GetOpenExplorerFolders();
            if (list.Count == 0)
            {
                ZenithDialog.Show("開いているエクスプローラーがありません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Info);
                return;
            }

            if (list.Count == 1)
            {
                ParentPane?.AddTabWithPathCommand.Execute(list[0].Path);
                ExplorerWindowHelper.CloseExplorerWindow(list[0].Hwnd);
                App.Notification.Notify("エクスプローラーをインポートしました", $"エクスプローラーからインポート: {list[0].Path}");
                return;
            }

            var dialog = new SelectExplorerWindowsDialog(list);
            if (dialog.ShowDialog() != true) return;

            var selected = dialog.SelectedItems;
            foreach (var item in selected)
            {
                ParentPane?.AddTabWithPathCommand.Execute(item.Path);
                ExplorerWindowHelper.CloseExplorerWindow(item.Hwnd);
            }
            App.Notification.Notify($"{selected.Count} 件のエクスプローラーをインポートしました", $"エクスプローラーからインポート: {selected.Count} タブ");
        }

        [RelayCommand]
        private void OpenItem(FileItem? item)
        {
            if (item == null) return;

            // ショートカット（.lnk）の場合はリンク先を解析し、フォルダなら新しいタブで開く
            if (!item.IsDirectory && item.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string? targetPath = Helpers.ShortcutHelper.GetShortcutTarget(item.FullPath);
                if (!string.IsNullOrEmpty(targetPath))
                {
                    if (Directory.Exists(targetPath))
                    {
                        ParentPane?.AddTabWithPathCommand.Execute(targetPath);
                        App.Notification.Notify("新しいタブでフォルダを開きました", $"ショートカット先: {targetPath}");
                        return;
                    }
                    if (!File.Exists(targetPath))
                    {
                        App.Notification.Notify("リンク先のパスが見つかりません", targetPath);
                        return;
                    }
                    // ファイルが存在する場合は、従来どおり OS 標準の関連付けに任せる（後続の処理にフォールスルー）
                }
            }

            // 検索結果タブの場合は、フォルダを新しいタブで開く
            if (IsSearchResultTab && item.IsDirectory)
            {
                ParentPane?.AddTabWithPathCommand.Execute(item.FullPath);
                App.Notification.Notify("新しいタブでフォルダを開きました", $"新しいタブで開く: {item.FullPath}");
                return;
            }

            if (item.IsDirectory)
                Navigate(item.FullPath);
            else
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FullPath) { UseShellExecute = true });
                    App.Notification.Notify("ファイルを開きました", $"ファイルを開く: {item.FullPath}");
                }
                catch (Exception ex)
                {
                    App.Notification.Notify("ファイルを開けませんでした", $"ファイルを開く失敗: {item.FullPath} 詳細: {ex}");
                    ZenithDialog.Show($"エラー: {ex.Message}", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Error);
                }
            }
        }

        private System.Threading.CancellationTokenSource? _loadCts;
        private System.Threading.CancellationTokenSource? _iconLoadCts;
        private System.Threading.CancellationTokenSource? _thumbSupplementCts;
        private System.Threading.CancellationTokenSource? _indexingCts;
        private System.Threading.CancellationTokenSource? _folderSizeCts;
        private readonly SemaphoreSlim _loadDirectorySemaphore = new(1, 1);
        private volatile bool _isRefreshInProgress;
        /// <summary>リフレッシュ処理中かどうか。View 側でCollectionChangedハンドラのガードに使用。</summary>
        public bool IsRefreshInProgress => _isRefreshInProgress;

        public event EventHandler? RefreshStarting;
        public event EventHandler? RefreshCompleted;

        private bool CanExecuteRefresh() => true;

        [RelayCommand(CanExecute = nameof(CanExecuteRefresh))]
        public void Refresh()
        {
            if (_isRefreshInProgress) return;
            // 監視停止状態の場合、手動リフレッシュ時にウォッチャーを再セットアップ
            if (_watcherDisconnected)
            {
                try
                {
                    SetupWatcher();
                    _watcherDisconnected = false;
                    Interlocked.Exchange(ref _watcherReconnectAttempt, 0);
                }
                catch (Exception watcherEx)
                {
                    _ = App.FileLogger.LogAsync($"[WARN][Refresh] ウォッチャー復帰失敗: '{CurrentPath}' {watcherEx.Message}");
                }
            }
            try
            {
                _ = LoadDirectoryAsync();
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ERR][{ParentPane?.PaneLabel ?? "?"}] Refresh: {ex}");
                throw;
            }
        }

        public async Task LoadDirectoryAsync()
        {
            await _loadDirectorySemaphore.WaitAsync();
            _isRefreshInProgress = true;
            if (MainVM != null)
            {
                MainVM.IsLoading = true;
                MainVM.LoadingMessage = "[読み込み中] フォルダ内容をスキャンしています...";
            }
            _loadCts?.Cancel();
            _iconLoadCts?.Cancel();
            _folderSizeCts?.Cancel();
            _loadCts?.Dispose();
            _iconLoadCts?.Dispose();
            _folderSizeCts?.Dispose();
            _loadCts = new System.Threading.CancellationTokenSource();
            _iconLoadCts = new System.Threading.CancellationTokenSource();
            _folderSizeCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;
            var iconToken = _iconLoadCts.Token;
            var folderSizeToken = _folderSizeCts.Token;

            try
            {
                if (string.IsNullOrWhiteSpace(CurrentPath))
                {
                    return;
                }

                // UIスレッドで開始イベント通知（Viewがスクロール位置などを保存するため）
                await Application.Current.Dispatcher.InvokeAsync(() => RefreshStarting?.Invoke(this, EventArgs.Empty), DispatcherPriority.Normal);

                // ローディングインジケータ表示（スコープを抜けると自動解除）
                using var busyToken = MainVM?.BeginBusy();

                try
                {
                    // 仮想PCパス・UNCルートの場合は Directory.Exists チェックをスキップ
                    bool isPCPath = PathHelper.IsPCPath(CurrentPath);
                    bool isUncRoot = !isPCPath && PathHelper.IsUncRoot(CurrentPath);
                    if (!isPCPath && !isUncRoot && !await PathHelper.DirectoryExistsSafeAsync(CurrentPath))
                    {
                        return;
                    }

                    string path = CurrentPath;

                    // ステータスのみ更新（リストはクリアしない）
                    StatusText = "読み込み中...";

                    await Task.Run(async () =>
                    {
                        List<FileItem> allItems;

                        if (isPCPath)
                        {
                            allItems = LoadItemsFromPC(token);
                        }
                        else if (isUncRoot)
                        {
                            allItems = LoadItemsFromUnc(path, token);
                        }
                        else
                        {
                            allItems = LoadItemsFromLocal(path, token);
                        }
                        if (token.IsCancellationRequested) return;
                        if (allItems == null) allItems = new List<FileItem>();

                        // 差分更新と完了処理
                        var newItemsMap = allItems.ToDictionary(x => x.FullPath);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MergeItems(newItemsMap);
                            StatusText = $"{Items.Count} 個の項目";
                            UpdateSelectionInfo(null);
                            ApplySort();
                            _ = LoadFolderSizesAsync(folderSizeToken);
                        }, DispatcherPriority.Background);

                        // アイコンと種類の取得をバックグラウンドで開始
                        await LoadIconsAsync(allItems, iconToken);

                        // LoadIconsAsync 完了後、アイコンビュー時はサムネイル未読み込みがあれば補完（フォルダ移動後も確実に表示）
                        if (IsIconViewMode(FileViewMode) && !token.IsCancellationRequested)
                        {
                            await LoadThumbnailsForCurrentItemsAsync(iconToken);
                        }

                    }, token);
                    // 正常完了: 監査ログ
                    _ = App.FileLogger.LogAsync($"[{ParentPane?.PaneLabel ?? "?"}] Open: {path}");
                }
                catch (OperationCanceledException)
                {
                    // キャンセルは正常終了として扱う
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync($"[ERR][{ParentPane?.PaneLabel ?? "?"}] Open: {CurrentPath} - {ex}");
                }
                finally
                {
                    // 完了イベント通知
                    await Application.Current.Dispatcher.InvokeAsync(() => RefreshCompleted?.Invoke(this, EventArgs.Empty), DispatcherPriority.Normal);
                }
            }
            finally
            {
                if (MainVM != null)
                {
                    MainVM.IsLoading = false;
                    MainVM.LoadingMessage = "";
                }
                _isRefreshInProgress = false;
                _loadDirectorySemaphore.Release();
            }
        }

        private async Task LoadFolderSizesAsync(CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentPath) || !App.IndexService.IsPathIndexed(CurrentPath))
                    return;

                var folderItems = Items.Where(i => i.IsDirectory).ToList();
                if (folderItems.Count == 0) return;

                var folderPaths = folderItems.Select(i => i.FullPath).ToList();
                var sizes = await App.IndexService.GetFolderSizesFromIndexAsync(folderPaths, token);
                if (token.IsCancellationRequested || sizes.Count == 0) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var item in folderItems)
                    {
                        if (token.IsCancellationRequested) break;
                        if (sizes.TryGetValue(item.FullPath, out var size))
                            item.FolderIndexedSize = size;
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ERR] LoadFolderSizesAsync: {ex.Message}");
            }
        }

        /// <summary>現在のアイテムのうち、サムネイル未読み込みの画像ファイルに対してサムネイルを読み込む。アイコンビュー切り替え時・フォルダ移動完了後に呼ぶ。</summary>
        /// <param name="cancellationToken">省略時は独自の CancellationTokenSource を使用。</param>
        private async Task LoadThumbnailsForCurrentItemsAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            var itemsNeedingThumbnails = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                return Items
                    .Where(item => item.IsImageFile && item.Thumbnail == null)
                    .Where(item => !ShouldSkipThumbnail(item))
                    .ToList();
            });
            if (itemsNeedingThumbnails.Count == 0) return;

            System.Threading.CancellationToken token;
            if (cancellationToken.CanBeCanceled)
            {
                token = cancellationToken;
            }
            else
            {
                _thumbSupplementCts?.Cancel();
                _thumbSupplementCts = new System.Threading.CancellationTokenSource();
                token = _thumbSupplementCts.Token;
            }
            await LoadIconsAsync(itemsNeedingThumbnails, token);
        }

        private List<FileItem> LoadItemsFromUnc(string path, System.Threading.CancellationToken token)
        {
            var items = new List<FileItem>();
            try
            {
                using (var shFolder = new ShellFolder(path))
                {
                    foreach (var item in shFolder)
                    {
                        if (token.IsCancellationRequested) break;
                        
                        string fullPath = item.ParsingName ?? string.Empty;
                        string name = item.Name ?? string.Empty;
                        
                        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(name)) continue;

                        var fileItem = new FileItem 
                        { 
                            Name = name, 
                            FullPath = fullPath, 
                            IsDirectory = true,
                            TypeName = "共有フォルダー"
                        };

                        // 基本的なアイコンを取得
                        var genericInfo = ShellIconHelper.GetGenericInfo(fullPath, true);
                        fileItem.Icon = genericInfo.Icon;

                        // 可能な範囲で属性を取得
                        try
                        {
                            var di = new DirectoryInfo(fullPath);
                            fileItem.LastModified = di.LastWriteTime;
                            fileItem.Attributes = di.Attributes;
                        }
                        catch (Exception ex)
                        {
                            _ = App.FileLogger.LogAsync($"[ERR] LoadItems: {fullPath} - {ex}");
                        }

                        items.Add(fileItem);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ERR] LoadItems: {path} - {ex}");
            }
            return items;
        }

        private List<FileItem> LoadItemsFromLocal(string path, System.Threading.CancellationToken token)
        {
            var items = new System.Collections.Concurrent.ConcurrentBag<FileItem>();

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Temporary,
                    ReturnSpecialDirectories = false
                };

                var enumerable = new FileSystemEnumerable<FileItem>(
                    path,
                    (ref FileSystemEntry entry) =>
                    {
                        bool isDirectory = entry.IsDirectory;
                        string fullPath = entry.ToFullPath();

                        var genericInfo = ShellIconHelper.GetGenericInfo(fullPath, isDirectory);

                        var item = new FileItem
                        {
                            Name = entry.FileName.ToString(),
                            FullPath = fullPath,
                            IsDirectory = isDirectory,
                            LastModified = entry.LastWriteTimeUtc.LocalDateTime,
                            Icon = genericInfo.Icon,
                            TypeName = genericInfo.TypeName,
                            Attributes = entry.Attributes
                        };

                        if (!isDirectory)
                        {
                            item.Size = entry.Length;
                        }

                        return item;
                    },
                    options);

                foreach (var item in enumerable)
                {
                    if (token.IsCancellationRequested) break;
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ERR] LoadItems: {path} - {ex}");
            }

            return items.ToList();
        }

        private List<FileItem> LoadItemsFromPC(System.Threading.CancellationToken token)
        {
            var items = new List<FileItem>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (token.IsCancellationRequested) break;
                    if (!drive.IsReady) continue;

                    try
                    {
                        string fullPath = drive.RootDirectory.FullName;

                        // ShellItem から表示名を取得（例: "ローカルディスク (C:)"）
                        string displayName;
                        try
                        {
                            using var shellItem = new ShellItem(fullPath);
                            displayName = shellItem.Name ?? drive.Name;
                            if (displayName.StartsWith("::")) displayName = drive.Name;
                        }
                        catch
                        {
                            displayName = drive.Name;
                        }

                        // ドライブ種別の日本語名
                        string typeName = drive.DriveType switch
                        {
                            DriveType.Fixed => "ローカルディスク",
                            DriveType.Network => "ネットワークドライブ",
                            DriveType.Removable => "リムーバブルディスク",
                            DriveType.CDRom => "CD/DVDドライブ",
                            _ => drive.DriveType.ToString()
                        };

                        var genericInfo = ShellIconHelper.GetGenericInfo(fullPath, true);

                        var fileItem = new FileItem
                        {
                            Name = displayName,
                            FullPath = fullPath,
                            IsDirectory = true,
                            Size = drive.TotalSize,
                            LastModified = DateTime.MinValue,
                            Icon = genericInfo.Icon,
                            TypeName = typeName
                        };

                        items.Add(fileItem);
                    }
                    catch (Exception ex)
                    {
                        _ = App.FileLogger.LogAsync($"[ERR] LoadItemsFromPC: {drive.Name} - {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ERR] LoadItemsFromPC: {ex}");
            }
            return items;
        }

        private const int ThumbnailUiBatchSize = 10;

        /// <summary>クラウド／オフライン等でサムネイル取得をスキップすべきか。</summary>
        private static bool ShouldSkipThumbnail(FileItem item) =>
            (item.Attributes & FileAttributes.Offline) != 0
            || (item.Attributes & FileAttributes.ReparsePoint) != 0
            || ((int)item.Attributes & 0x00400000) != 0
            || item.LocationType == SourceType.Box;

        private async Task LoadIconsAsync(List<FileItem> allItems, System.Threading.CancellationToken token)
        {
            // 第1パス: アイコン・TypeName のみ取得し、すぐに UI に反映（サムネイルは取らない）
            var iconBatch = new List<(FileItem Item, ImageSource? Icon, string TypeName)>();
            foreach (var item in allItems)
            {
                if (token.IsCancellationRequested) break;

                var info = ShellIconHelper.GetInfo(item.FullPath, item.IsDirectory, ShouldSkipThumbnail(item));
                iconBatch.Add((item, info.Icon, info.TypeName));

                if (iconBatch.Count >= 100)
                {
                    var currentBatch = iconBatch.ToList();
                    iconBatch.Clear();
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var b in currentBatch)
                        {
                            b.Item.Icon = b.Icon;
                            b.Item.TypeName = b.TypeName;
                        }
                    }, DispatcherPriority.Normal);
                    await Task.Delay(30, token);
                }
            }

            if (iconBatch.Count > 0 && !token.IsCancellationRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var b in iconBatch)
                    {
                        b.Item.Icon = b.Icon;
                        b.Item.TypeName = b.TypeName;
                    }
                }, DispatcherPriority.Normal);
            }

            // 第2パス: 画像のみ GetThumbnailAsync で非ブロック取得し、10件ずつ完了次第 UI に反映
            var imageItems = allItems
                .Where(item => ShellIconHelper.IsImageFile(item.FullPath))
                .Where(item => !ShouldSkipThumbnail(item))
                .Take(ShellThumbnailService.MaxPerFolder)
                .ToList();

            for (int i = 0; i < imageItems.Count && !token.IsCancellationRequested; i += ThumbnailUiBatchSize)
            {
                int count = Math.Min(ThumbnailUiBatchSize, imageItems.Count - i);
                var batch = imageItems.GetRange(i, count);
                var tasks = batch.Select(item => ShellThumbnailService.Instance.GetThumbnailAsync(item.FullPath, 256, token)).ToArray();
                ImageSource?[] results;
                try
                {
                    results = await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var updates = batch.Zip(results, (item, thumb) => (Item: item, Thumbnail: thumb)).ToArray();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var u in updates)
                    {
                        if (u.Thumbnail != null)
                            u.Item.Thumbnail = u.Thumbnail;
                    }
                }, DispatcherPriority.Normal);
            }
        }

        private void MergeItems(Dictionary<string, FileItem> newItemsMap)
        {
            // 既存アイテムからサムネイル・アイコン・TypeName を引き継ぎ
            foreach (var existing in Items)
            {
                if (newItemsMap.TryGetValue(existing.FullPath, out var newItem))
                {
                    // 既にロード済みのアイコン/サムネイルを新アイテムに引き継ぐ
                    if (existing.Icon != null) newItem.Icon = existing.Icon;
                    if (existing.Thumbnail != null) newItem.Thumbnail = existing.Thumbnail;
                    if (!string.IsNullOrEmpty(existing.TypeName)) newItem.TypeName = existing.TypeName;
                }
            }

            // 一括置換（CollectionChanged を Reset 1 回だけ発火し、
            // 個別 Add/Remove による連続的なレイアウト再計算を回避）
            Items.ReplaceAll(newItemsMap.Values);
        }

        public void UpdateSelectionInfo(IList? selectedItems)
        {
            var snapshot = selectedItems?.OfType<FileItem>().ToList() ?? new List<FileItem>();
            if (snapshot.Count == 0)
            {
                SelectionInfoText = string.Empty;
                return;
            }

            int count = snapshot.Count;
            long totalSize = 0;
            bool hasFile = false;

            foreach (var item in snapshot)
            {
                if (!item.IsDirectory)
                {
                    totalSize += item.Size;
                    hasFile = true;
                }
            }

            if (hasFile)
            {
                SelectionInfoText = $"{count} 個の項目を選択中  {FileItem.FormatSize(totalSize)}";
            }
            else
            {
                SelectionInfoText = $"{count} 個の項目を選択中";
            }
        }

        [RelayCommand]
        private void CopyItems(IList? items)
        {
            if (items == null || items.Count == 0) return;
            var paths = new System.Collections.Specialized.StringCollection();
            foreach (FileItem item in items) paths.Add(item.FullPath);
            Clipboard.SetFileDropList(paths);
            App.Notification.Notify($"{items.Count} 件をコピーしました（クリップボード）", $"コピー（クリップボード）: {string.Join(", ", items.Cast<FileItem>().Select(x => x.FullPath))}");
        }

        [RelayCommand]
        private async Task DeleteItems(IList? items)
        {
            if (items == null || items.Count == 0) return;

            // ローディングインジケータを表示
            using var busyToken = MainVM?.BeginBusy();

            var itemsToDelete = items.Cast<FileItem>().ToList();

            // 削除前: 選択アイテムの最小インデックスを保存（削除後のフォーカス復元用）
            int minSelectedIndex = int.MaxValue;
            var viewItems = CurrentItemsView.Cast<FileItem>().ToList();
            foreach (var item in itemsToDelete)
            {
                int idx = viewItems.IndexOf(item);
                if (idx >= 0 && idx < minSelectedIndex)
                    minSelectedIndex = idx;
            }
            if (minSelectedIndex == int.MaxValue) minSelectedIndex = 0;

            // シェル（エクスプローラ）のコンテキストメニュー等からの変更を期待する
            IsExpectingShellChange = true;

            bool operationCompleted = false;

            await Task.Run(() =>
            {
                var shellItems = new List<ShellItem>();
                try
                {
                    using var op = new ShellFileOperations();

                    // 親ウィンドウの設定（確認ダイアログをモーダルにするため）
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Application.Current.MainWindow != null)
                        {
                            var helper = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow);
                            op.OwnerWindow = helper.Handle;
                        }
                    });

                    // 削除操作をキューに追加（Windows標準の確認ダイアログが表示され、ごみ箱に送られます）
                    foreach (var item in itemsToDelete)
                    {
                        var shellItem = new ShellItem(item.FullPath);
                        shellItems.Add(shellItem);
                        op.QueueDeleteOperation(shellItem);
                    }

                    op.PerformOperations();

                    if (!op.AnyOperationsAborted)
                    {
                        operationCompleted = true;
                        foreach (var item in itemsToDelete.Where(x => x.IsDirectory))
                        {
                            var parent = Path.GetDirectoryName(item.FullPath);
                            if (!string.IsNullOrEmpty(parent))
                                Services.FileOperationService.Instance.RaiseFolderDeleted(parent, item.FullPath);
                        }

                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var pane = ParentPane?.PaneLabel ?? "?";
                            App.Notification.Notify($"{itemsToDelete.Count} 件の項目を削除しました",
                                $"[{pane}] Delete ({itemsToDelete.Count}): {string.Join(", ", itemsToDelete.Select(x => x.Name))}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var pane = ParentPane?.PaneLabel ?? "?";
                        var friendlyMsg = Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, itemsToDelete.FirstOrDefault()?.FullPath);
                        App.Notification.Notify("削除中にエラーが発生しました", $"[ERR][{pane}] Delete: {ex.Message}");
                        ZenithDialog.Show($"削除中にエラーが発生しました。\n{friendlyMsg}", "削除", ZenithDialogButton.OK, ZenithDialogIcon.Error);
                    });
                }
                finally
                {
                    foreach (var item in shellItems) item.Dispose();
                }
            });

            // 削除が実行された場合のみフォーカス復元を設定してリフレッシュ
            if (operationCompleted)
            {
                SelectionIndexToRestore = minSelectedIndex;
                await LoadDirectoryAsync();
            }
        }

        [RelayCommand]
        private void OpenInExplorer(IList? items)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    System.Diagnostics.Process.Start("explorer.exe", CurrentPath);
                    App.Notification.Notify("エクスプローラーで開きました", $"エクスプローラーで開く: {CurrentPath}");
                }
                else if (items[0] is FileItem firstItem)
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{firstItem.FullPath}\"");
                    App.Notification.Notify("エクスプローラーで開きました", $"エクスプローラーで開く（選択）: {firstItem.FullPath}");
                }
            }
            catch (Exception ex)
            {
                App.Notification.Notify("エクスプローラーで開けませんでした", $"エクスプローラーで開く失敗 詳細: {ex}");
            }
        }

        [RelayCommand]
        private void CopyAddress()
        {
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                Clipboard.SetText(CurrentPath);
                App.Notification.Notify("パスをコピーしました", CurrentPath);
            }
        }

        [RelayCommand]
        private async Task DropFiles((string[]? files, bool isMove, string? targetDirectory) args) =>
            await DropFilesInternal(args.files, args.isMove, args.targetDirectory);

        /// <summary>
        /// ブラウザなどからドロップされた URL をショートカットとして保存する。
        /// </summary>
        [RelayCommand]
        private async Task CreateUrlShortcut((string url, string? title, string? targetDirectory) args)
        {
            if (string.IsNullOrEmpty(args.url) || string.IsNullOrEmpty(CurrentPath)) return;

            // ドロップ先フォルダが明示されていればそれを優先し、なければ現在のフォルダに作成する
            string baseTarget = string.IsNullOrWhiteSpace(args.targetDirectory) ? CurrentPath : args.targetDirectory;
            string resolvedTargetDirectory = PathHelper.GetPhysicalPath(baseTarget);

            using var busyToken = MainVM?.BeginBusy();

            try
            {
                // ファイル作成のみバックグラウンドで実行
                string? resultPath = await Task.Run(() => ShortcutHelper.CreateUrlShortcut(args.url, resolvedTargetDirectory, args.title));

                if (resultPath != null)
                {
                    // UIスレッドでリフレッシュと通知を実行
                    // 先にRefreshすることで、成功通知が「一覧を更新しました」で上書きされないようにする
                    Refresh();
                    App.Notification.Notify("ショートカットを作成しました", Path.GetFileName(resultPath));
                }
                else
                {
                    App.Notification.Notify("ショートカットの作成に失敗しました");
                }
            }
            catch (Exception ex)
            {
                App.Notification.Notify("エラーが発生しました", ex.Message);
            }
        }

        // ─── Zenith Turbo Engine ────────────────────────────────────────────────────
        // ・FileStream バッファ: 4 MB（I/O オーバーヘッド削減）
        // ・コピーチャンク : 4 MB を ArrayPool<byte> で確保・再利用（GC 圧力ゼロ）
        // ・バイト単位進捗 : 書き込みごとにコールバックで大容量ファイルもバーを動かす
        // ・並列度        : Math.Clamp(ProcessorCount / 2, 1, 3) でコア数に追従

        private const int TurboStreamBufferSize = 4 * 1024 * 1024; // 4 MB  FileStream バッファ
        private const int TurboCopyChunkSize    = 4 * 1024 * 1024; // 4 MB  ArrayPool コピーチャンク

        private async Task DropFilesInternal(string[]? files, bool isMove = false, string? targetDirectory = null)
        {
            if (files == null || string.IsNullOrEmpty(CurrentPath)) return;
            string baseTarget = string.IsNullOrWhiteSpace(targetDirectory) ? CurrentPath : targetDirectory;
            string resolvedTargetDirectory = PathHelper.GetPhysicalPath(baseTarget);
            if (string.IsNullOrEmpty(resolvedTargetDirectory) || !Directory.Exists(resolvedTargetDirectory)) return;

            // ── UIスレッドで MainVM 参照をキャプチャ（Parallel内はスレッドプール）──────
            var mainVmRef = MainVM;
            using var busyToken = mainVmRef?.BeginBusy();

            string actionLabel = isMove ? "移動" : "コピー";

            // ── 有効パスを UIスレッド上でフィルタリング ──────────────────────────────
            var physicalFiles = files
                .Select(f => PathHelper.GetPhysicalPath(f))
                .Where(p => !string.IsNullOrEmpty(p) && (File.Exists(p) || Directory.Exists(p)))
                .ToList();

            int total = physicalFiles.Count;
            if (total == 0) return;

            // ── 進捗追跡カウンタ（Interlocked で複数スレッドから安全に操作）──────────
            int  completedFiles = 0;
            long copiedBytes    = 0;   // コピー済みバイト数（コピー時）
            long totalBytes     = 0;   // 総バイト数（コピー時、後述の非同期計算で確定）

            var postActions     = new ConcurrentBag<Action>();
            var actualDestNames = new ConcurrentBag<string>(); // 実際の転送先ファイル名（自動リネーム後）

            // ── 空間認識バー方向の決定 ──────────────────────────────────────────────
            // 自分（this）が転送先タブ。自分のペインが LeftPane(A) なら転送は B→A (RightToLeft)、
            // RightPane(B) なら A→B (LeftToRight)。外部ドロップ等は LeftToRight にフォールバック。
            var progressFlowDir = (mainVmRef?.LeftPane == this.ParentPane)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            // ── グロウバーを即時表示してスキャン中メッセージを出す ──────────────────
            mainVmRef?.BeginFileOperation($"[準備中] {total} 件のファイルをスキャン中...", progressFlowDir);
            var ct = mainVmRef?.FileOperationToken ?? CancellationToken.None;

            try
            {
                // ── コピー時のみ総バイト数を非同期計算 ───────────────────────────────
                // スキャン件数を 10 件ごとにステータスへ反映し「動いている感」を演出する。
                if (!isMove)
                {
                    int scanned = 0;
                    totalBytes = await Task.Run(() =>
                    {
                        long sum = 0;
                        foreach (var f in physicalFiles)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                if (File.Exists(f))
                                    sum += new FileInfo(f).Length;
                                else if (Directory.Exists(f))
                                    sum += new DirectoryInfo(f)
                                        .EnumerateFiles("*", SearchOption.AllDirectories)
                                        .Sum(fi => { try { return fi.Length; } catch { return 0L; } });
                            }
                            catch { /* アクセス不能ファイルは 0 扱い */ }

                            int s = ++scanned;
                            if (s == 1 || s % 10 == 0 || s == total)
                            {
                                try { mainVmRef?.ReportFileOperationProgress(0, $"[準備中] {s} / {total} 件スキャン済み..."); }
                                catch { }
                            }
                        }
                        return sum;
                    }, ct);
                }

                // ── 並列転送（MaxDegreeOfParallelism: コア数の半数、上限 3）───────────
                int dop = Math.Clamp(Environment.ProcessorCount / 2, 1, 3);
                await Parallel.ForEachAsync(
                    physicalFiles,
                    new ParallelOptions { MaxDegreeOfParallelism = dop, CancellationToken = ct },
                    async (physicalPath, cancellationToken) =>
                    {
                        string fileName = Path.GetFileName(physicalPath);
                        string destPath = Path.Combine(resolvedTargetDirectory, fileName);

                        if (isMove)
                        {
                            // 同一フォルダへの移動はスキップ（移動先 == 移動元）
                            if (!physicalPath.Equals(destPath, StringComparison.OrdinalIgnoreCase) &&
                                !PathHelper.NormalizePathForComparison(resolvedTargetDirectory)
                                    .Equals(PathHelper.NormalizePathForComparison(physicalPath), StringComparison.OrdinalIgnoreCase))
                            {
                                // 移動先に同名が存在する場合は「 - コピー」形式でリネーム
                                string uniqueDest = GetUniquePath(destPath);

                                if (Directory.Exists(physicalPath))
                                    Directory.Move(physicalPath, uniqueDest);
                                else
                                    File.Move(physicalPath, uniqueDest);

                                actualDestNames.Add(Path.GetFileName(uniqueDest));
                                var src = physicalPath;
                                var dst = uniqueDest;
                                postActions.Add(() => UndoService.Instance.Register(new MoveUndoCommand(src, dst)));
                            }

                            // 移動はファイル数ベースで進捗計算
                            int doneMv = Interlocked.Increment(ref completedFiles);
                            double pctMv = doneMv * 100.0 / total;
                            try { mainVmRef?.ReportFileOperationProgress(pctMv, $"[移動中] {(int)pctMv}% [{fileName}]"); }
                            catch { }
                        }
                        else
                        {
                            // ── 衝突回避: 同名ファイル/同一フォルダコピーを「 - コピー」形式で自動リネーム ──
                            // physicalPath == destPath（同一フォルダ）の場合もここで吸収される。
                            string uniqueDest = GetUniquePath(destPath);

                            // ── バイト単位コールバック ──────────────────────────────────
                            // 4MB 書き込み毎に呼ばれ、大容量ファイルでもバーをリアルタイムに動かす。
                            // Interlocked.Add で複数スレッドからの同時加算を安全に処理。
                            void onBytesWritten(long written)
                            {
                                long sofar = Interlocked.Add(ref copiedBytes, written);
                                double pctBytes = totalBytes > 0 ? sofar * 100.0 / totalBytes : 0.0;
                                try { mainVmRef?.ReportFileOperationProgress(pctBytes, $"[コピー中] {(int)pctBytes}% [{fileName}]"); }
                                catch { }
                            }

                            if (File.Exists(physicalPath))
                                await TurboCopyFileAsync(physicalPath, uniqueDest, cancellationToken, onBytesWritten);
                            else if (Directory.Exists(physicalPath))
                                await TurboCopyDirectoryAsync(physicalPath, uniqueDest, cancellationToken, onBytesWritten);

                            actualDestNames.Add(Path.GetFileName(uniqueDest));
                            var dst = uniqueDest;
                            postActions.Add(() => UndoService.Instance.Register(new CopyUndoCommand(dst)));
                        }
                    });

                // UI スレッドで Undo コマンドを登録
                foreach (var action in postActions) action();
            }
            catch (OperationCanceledException)
            {
                App.Notification.Notify($"{actionLabel}がキャンセルされました");
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var friendlyMsg = files.Length > 0 ? Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, files[0]) : ex.Message;
                    App.Notification.Notify($"{actionLabel}失敗", ex.Message);
                    ZenithDialog.Show($"{actionLabel}中にエラーが発生しました。\n{friendlyMsg}", actionLabel, ZenithDialogButton.OK, ZenithDialogIcon.Error);
                });
            }
            finally
            {
                // IsBusy は using var busyToken が確実に解放。ファイル操作進捗もリセット。
                mainVmRef?.EndFileOperation();
            }

            RequestFocusAfterRefresh = true;
            // 自動リネーム後の実際のファイル名でフォーカスを当てる
            PastedFileNamesToSelect = actualDestNames.Count > 0
                ? actualDestNames.ToList()
                : files.Select(f => Path.GetFileName(f)).Where(n => !string.IsNullOrEmpty(n)).ToList();
            Refresh();
        }

        /// <summary>
        /// ArrayPool バッファ（4MB）を使った非同期ファイルコピー（Zenith Turbo Engine）。
        /// <paramref name="onBytesWritten"/> に書き込みバイト数を都度通知し、
        /// 大容量ファイルでもプログレスバーをリアルタイムに動かします。
        /// GC 圧力を抑えるため ArrayPool&lt;byte&gt;.Shared でバッファを再利用します。
        /// </summary>
        private static async Task TurboCopyFileAsync(
            string src, string dest, CancellationToken ct,
            Action<long>? onBytesWritten = null)
        {
            var info = new FileInfo(src);
            var creationTimeUtc = info.CreationTimeUtc;
            var writeTimeUtc    = info.LastWriteTimeUtc;
            var attributes      = info.Attributes;

            byte[] buf = ArrayPool<byte>.Shared.Rent(TurboCopyChunkSize);
            try
            {
                await using var srcStream = new FileStream(
                    src, FileMode.Open, FileAccess.Read, FileShare.Read,
                    TurboStreamBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var dstStream = new FileStream(
                    dest, FileMode.Create, FileAccess.Write, FileShare.None,
                    TurboStreamBufferSize, FileOptions.Asynchronous);

                int bytesRead;
                while ((bytesRead = await srcStream.ReadAsync(buf.AsMemory(0, TurboCopyChunkSize), ct)) > 0)
                {
                    await dstStream.WriteAsync(buf.AsMemory(0, bytesRead), ct);
                    onBytesWritten?.Invoke(bytesRead);
                }
                await dstStream.FlushAsync(ct);
            }
            catch
            {
                // コピー失敗・キャンセル時は不完全ファイルを削除
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                throw;
            }
            finally
            {
                // バッファを必ずプールへ返却（GC に任せない）
                ArrayPool<byte>.Shared.Return(buf);
            }

            // タイムスタンプと属性を元ファイルから復元
            File.SetCreationTimeUtc(dest, creationTimeUtc);
            File.SetLastWriteTimeUtc(dest, writeTimeUtc);
            File.SetAttributes(dest, attributes);
        }

        /// <summary>
        /// ディレクトリを再帰的に Turbo コピーし、各ディレクトリのタイムスタンプも復元します。
        /// <paramref name="onBytesWritten"/> はファイルコピーのコールバックとして再帰的に伝播します。
        /// </summary>
        private static async Task TurboCopyDirectoryAsync(
            string sourceDir, string destDir, CancellationToken ct,
            Action<long>? onBytesWritten = null)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                ct.ThrowIfCancellationRequested();
                await TurboCopyFileAsync(file, Path.Combine(destDir, Path.GetFileName(file)), ct, onBytesWritten);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                ct.ThrowIfCancellationRequested();
                await TurboCopyDirectoryAsync(dir, Path.Combine(destDir, Path.GetFileName(dir)), ct, onBytesWritten);
            }

            // ディレクトリのタイムスタンプを復元
            var srcInfo = new DirectoryInfo(sourceDir);
            Directory.SetCreationTimeUtc(destDir, srcInfo.CreationTimeUtc);
            Directory.SetLastWriteTimeUtc(destDir, srcInfo.LastWriteTimeUtc);
        }

        /// <summary>
        /// 指定パスが既に存在する場合、Windows エクスプローラー準拠の「 - コピー」形式で
        /// 衝突しないユニークなパスを生成して返します。
        /// <list type="bullet">
        ///   <item>存在しない → そのまま返す</item>
        ///   <item>1 回目の衝突 → 「名前 - コピー.ext」</item>
        ///   <item>2 回目以降 → 「名前 - コピー (2).ext」「名前 - コピー (3).ext」...</item>
        ///   <item>フォルダも同様（拡張子なし）</item>
        /// </list>
        /// </summary>
        private static string GetUniquePath(string destPath)
        {
            // 存在しなければそのまま返す（最速パス）
            if (!File.Exists(destPath) && !Directory.Exists(destPath))
                return destPath;

            string dir            = Path.GetDirectoryName(destPath) ?? "";
            string ext            = Path.GetExtension(destPath);              // ファイル: ".txt" / フォルダ: ""
            string nameWithoutExt = Path.GetFileNameWithoutExtension(destPath); // フォルダ: フルネーム

            // 1 回目: 「名前 - コピー.ext」
            string candidate = Path.Combine(dir, nameWithoutExt + " - コピー" + ext);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;

            // 2 回目以降: 「名前 - コピー (N).ext」
            for (int n = 2; n <= 9999; n++)
            {
                candidate = Path.Combine(dir, $"{nameWithoutExt} - コピー ({n}){ext}");
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    return candidate;
            }

            // フォールバック（実用上は到達しない）
            return Path.Combine(dir, $"{nameWithoutExt} - コピー ({Guid.NewGuid():N}){ext}");
        }

        /// <summary>指定したファイル/フォルダのショートカットをドロップ先フォルダに作成する（右ドラッグ＆ドロップ用）。</summary>
        [RelayCommand]
        private void CreateShortcutsAt((string[]? files, string? targetDirectory) args)
        {
            if (args.files == null || args.files.Length == 0) return;

            string baseTarget = string.IsNullOrWhiteSpace(args.targetDirectory) ? CurrentPath : args.targetDirectory;
            if (string.IsNullOrEmpty(baseTarget)) return;

            string resolvedTargetDirectory = PathHelper.GetPhysicalPath(baseTarget);
            if (!Directory.Exists(resolvedTargetDirectory))
            {
                ZenithDialog.Show("ドロップ先フォルダが見つかりません", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }

            var created = new List<string>();
            var errors = new List<string>();

            foreach (string file in args.files)
            {
                if (string.IsNullOrEmpty(file)) continue;
                string? shortcutPath = ShortcutHelper.CreateShortcut(file, resolvedTargetDirectory, out string? errorDetail);
                if (shortcutPath != null)
                {
                    created.Add(Path.GetFileName(shortcutPath));
                    // Undo登録（ショートカット作成＝ファイル作成なので、CopyUndoCommand（ファイル削除）で対応）
                    UndoService.Instance.Register(new CopyUndoCommand(shortcutPath));
                }
                else
                    errors.Add($"{Path.GetFileName(file)}: {errorDetail ?? "不明なエラー"}");
            }

            if (errors.Count > 0)
            {
                App.Notification.Notify("一部のショートカット作成に失敗しました", $"ショートカット作成失敗: {string.Join(", ", errors)}");
                ZenithDialog.Show($"ショートカットの作成に失敗した項目があります。\n\n詳細:\n{string.Join("\n", errors)}", "ショートカット作成エラー", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }

            if (created.Count > 0)
            {
                RequestFocusAfterRefresh = true;
                PastedFileNamesToSelect = created;
                Refresh();
                App.Notification.Notify($"{created.Count} 件のショートカットを作成しました",
                    $"{created.Count} 件のショートカットを {resolvedTargetDirectory} に作成: {string.Join(", ", created)} (Sources: {string.Join(", ", args.files)})");
            }
        }

        public void Dispose()
        {
            var reconnectCts = Interlocked.Exchange(ref _watcherReconnectCts, null);
            if (reconnectCts != null) { try { reconnectCts.Cancel(); } catch { } reconnectCts.Dispose(); }
            _watcher?.Dispose();
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _iconLoadCts?.Cancel();
            _iconLoadCts?.Dispose();
            _thumbSupplementCts?.Cancel();
            _thumbSupplementCts?.Dispose();
            _loadDirectorySemaphore?.Dispose();
            _watcherCts?.Cancel();
            _watcherCts?.Dispose();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _indexingCts?.Cancel();
            _indexingCts?.Dispose();
            _folderSizeCts?.Cancel();
            _folderSizeCts?.Dispose();
            if (_scopeSubscribed)
            {
                var settings = MainVM?.IndexSearchSettings;
                if (settings != null) settings.ScopeSelectionChanged -= OnScopeSelectionChanged;
                _scopeSubscribed = false;
            }
            if (_filterSubscribed)
            {
                var searchFilter = MainVM?.SearchFilter;
                if (searchFilter != null) searchFilter.FilterChanged -= OnSearchFilterChanged;
                _filterSubscribed = false;
            }
            foreach (var filter in FileTypeFilterItems)
                filter.PropertyChanged -= OnFileTypeFilterItemPropertyChanged;
            App.Database.SearchHistoryChanged -= OnSearchHistoryChanged;
            AppActivationService.Instance.ActivationChanged -= OnAppActivationChanged;
            Items.Clear();
            SearchResults.Clear();
            GC.SuppressFinalize(this);
        }

        // ─── ファイル一覧出力（共通） ──────────────────────────────────────────────

        /// <summary>再帰スキャン結果の 1 レコード。</summary>
        private readonly record struct ExportEntry(
            string Name, long Size, string TypeName,
            DateTime LastModified, string RelFolderPath,
            string ShareableFolderPath, string FolderFullPath);

        /// <summary>フォルダ以下のファイルを再帰スキャンして ExportEntry リストを返す。</summary>
        private static List<ExportEntry> ScanSubtree(
            string folderPath, ref double progressTarget, ref string statusText)
        {
            var baseNormalized = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var baseWithSep = baseNormalized + Path.DirectorySeparatorChar;
            var entries = new List<ExportEntry>();
            int scanCount = 0;

            try
            {
                foreach (var fi in new DirectoryInfo(folderPath)
                    .EnumerateFiles("*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.System
                    }))
                {
                    try
                    {
                        string typeName = Path.GetExtension(fi.Name)?.TrimStart('.').ToUpperInvariant() ?? "";
                        var dirPath = fi.DirectoryName ?? "";

                        // 相対フォルダパスの算出（カレント直下 = ".\"）
                        string relDir;
                        if (string.Equals(dirPath, baseNormalized, StringComparison.OrdinalIgnoreCase))
                        {
                            // カレントフォルダ直下
                            relDir = @".\";
                        }
                        else if (dirPath.StartsWith(baseWithSep, StringComparison.OrdinalIgnoreCase))
                        {
                            // サブフォルダ配下
                            relDir = @".\" + dirPath.Substring(baseWithSep.Length) + @"\";
                        }
                        else
                        {
                            relDir = dirPath;
                        }

                        var shareablePath = PathHelper.GetShareablePath(dirPath);
                        entries.Add(new ExportEntry(fi.Name, fi.Length, typeName,
                            fi.LastWriteTime, relDir, shareablePath, dirPath));
                    }
                    catch (Exception ex)
                    {
                        _ = App.FileLogger.LogAsync($"[Export] Skipped file: {fi.FullName} - {ex.Message}");
                        continue;
                    }

                    scanCount++;
                    if (scanCount % 200 == 0)
                    {
                        double pct = Math.Min(3 + scanCount / 200.0, 9);
                        Volatile.Write(ref progressTarget, pct);
                        statusText = $"{scanCount:N0} 件をスキャン中...";
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[Export] Scan enumeration error: {ex.Message}");
            }
            return entries;
        }

        // ─── CSV出力 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定フォルダ以下の全ファイル・フォルダをスキャンして CSV を保存する。
        /// </summary>
        public async Task ExportSubtreeToCsvAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            var folderName = Path.GetFileName(
                folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var defaultFileName = $"filelist_{folderName}_{DateTime.Now:yyyyMMdd}.csv";
            string savePath = Path.Combine(folderPath, defaultFileName);

            using var busyToken = MainVM?.BeginBusy();

            MainVM?.BeginFileOperation("[CSV出力] フォルダをスキャンしています...", FlowDirection.LeftToRight);
            if (MainVM != null) MainVM.FileOperationProgress = 2;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            double progressTarget = 2;
            string statusText = "[CSV出力] フォルダをスキャンしています...";
            var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            progressTimer.Tick += (_, _) =>
            {
                if (MainVM == null) return;
                double target = Volatile.Read(ref progressTarget);
                double current = MainVM.FileOperationProgress;
                MainVM.FileOperationStatusText = statusText;
                if (Math.Abs(target - current) < 0.3) return;
                double step = (target - current) * 0.18;
                if (step > 0 && step < 0.5) step = 0.5;
                MainVM.FileOperationProgress = Math.Min(current + step, target);
            };
            progressTimer.Start();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int entryCount = 0;
            bool succeeded = false;

            try
            {
                entryCount = await Task.Run(() =>
                {
                    var entries = ScanSubtree(folderPath, ref progressTarget, ref statusText);
                    int total = entries.Count;
                    Volatile.Write(ref progressTarget, 10);
                    statusText = $"[CSV出力] {total:N0} 件の書き込みを開始...";

                    int lastTargetPct = 10;
                    var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                    using var writer = new StreamWriter(savePath, append: false, encoding: enc);
                    writer.WriteLine("名称,サイズ,種類,更新日時,相対フォルダパス,フォルダパス (共有),フォルダを開く");
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        var sizeStr = FileItem.FormatSize(e.Size);
                        var ts = e.LastModified.ToString("yyyy/MM/dd HH:mm:ss");
                        writer.WriteLine(string.Join(",",
                            CsvEscape(e.Name), CsvEscape(sizeStr), CsvEscape(e.TypeName),
                            ts, CsvEscape(e.RelFolderPath), CsvEscape(e.ShareableFolderPath),
                            CsvEscape(e.FolderFullPath)));

                        if (total > 0)
                        {
                            int pct = (int)(10 + (i + 1) * 80.0 / total);
                            if (pct >= lastTargetPct + 2)
                            {
                                lastTargetPct = pct;
                                Volatile.Write(ref progressTarget, (double)pct);
                                statusText = $"[CSV出力] {(i + 1):N0} / {total:N0} 件を処理中... ({pct}%)";
                            }
                        }
                    }
                    Volatile.Write(ref progressTarget, 95.0);
                    statusText = "[CSV出力] CSVファイルを保存しています...";
                    return entries.Count;
                });
                succeeded = true;
            }
            catch (IOException)
            {
                App.Notification.Notify("CSVファイルが使用中のため出力できませんでした", $"CSV出力失敗: {savePath}");
            }
            catch (Exception ex)
            {
                App.Notification.Notify("CSV出力に失敗しました", ex.Message);
            }
            finally
            {
                sw.Stop();
                var min = TimeSpan.FromMilliseconds(800);
                if (sw.Elapsed < min) await Task.Delay(min - sw.Elapsed);
                progressTimer.Stop();
                MainVM?.EndFileOperation();
            }
            if (succeeded)
                App.Notification.Notify($"CSVを出力しました（{entryCount:N0} 件）", $"CSV出力: {savePath}");
        }

        // ─── Excel出力 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定フォルダ以下の全ファイル・フォルダをスキャンして Excel (.xlsx) を保存する。
        /// </summary>
        public async Task ExportSubtreeToExcelAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            var folderName = Path.GetFileName(
                folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var defaultFileName = $"filelist_{folderName}_{DateTime.Now:yyyyMMdd}.xlsx";
            string savePath = Path.Combine(folderPath, defaultFileName);

            using var busyToken = MainVM?.BeginBusy();

            MainVM?.BeginFileOperation("[Excel出力] フォルダをスキャンしています...", FlowDirection.LeftToRight);
            if (MainVM != null) MainVM.FileOperationProgress = 2;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            double progressTarget = 2;
            string statusText = "[Excel出力] フォルダをスキャンしています...";
            var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            progressTimer.Tick += (_, _) =>
            {
                if (MainVM == null) return;
                double target = Volatile.Read(ref progressTarget);
                double current = MainVM.FileOperationProgress;
                MainVM.FileOperationStatusText = statusText;
                if (Math.Abs(target - current) < 0.3) return;
                double step = (target - current) * 0.18;
                if (step > 0 && step < 0.5) step = 0.5;
                MainVM.FileOperationProgress = Math.Min(current + step, target);
            };
            progressTimer.Start();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int entryCount = 0;
            bool succeeded = false;

            try
            {
                entryCount = await Task.Run(() =>
                {
                    var entries = ScanSubtree(folderPath, ref progressTarget, ref statusText);
                    int total = entries.Count;
                    Volatile.Write(ref progressTarget, 10);
                    statusText = $"[Excel出力] {total:N0} 件の書き込みを開始...";

                    using var workbook = new XLWorkbook();
                    var sheetName = SanitizeSheetName(folderName);
                    var ws = workbook.Worksheets.Add(sheetName);

                    // --- タイトル行 (Row 1) ---
                    ws.Cell("A1").Value = $"{folderName} ファイル一覧（{DateTime.Now:yyyy/MM/dd HH:mm} 出力・{total:N0} 件）";
                    ws.Cell("A1").Style.Font.Bold = true;
                    ws.Cell("A1").Style.Font.FontSize = 13;
                    ws.Range("A1:G1").Merge();

                    // --- ヘッダー行 (Row 2) ---
                    string[] headers = { "名称", "サイズ", "種類", "更新日時", "相対フォルダパス", "フォルダパス (共有)", "フォルダを開く" };
                    var headerBg = XLColor.FromHtml("#E6E1D3");
                    var headerFg = XLColor.FromHtml("#1A1A1A");
                    for (int c = 0; c < headers.Length; c++)
                    {
                        var cell = ws.Cell(2, c + 1); // A2〜G2
                        cell.Value = headers[c];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontSize = 11;
                        cell.Style.Fill.BackgroundColor = headerBg;
                        cell.Style.Font.FontColor = headerFg;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    // --- データ行 (Row 3〜) ---
                    var linkColor = XLColor.FromHtml("#0563C1");
                    int lastTargetPct = 10;

                    for (int i = 0; i < entries.Count; i++)
                    {
                        int row = i + 3;
                        var e = entries[i];

                        ws.Cell(row, 1).Value = e.Name;                                          // A: 名称
                        ws.Cell(row, 2).Value = FileItem.FormatSize(e.Size);                      // B: サイズ
                        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        ws.Cell(row, 3).Value = e.TypeName;                                       // C: 種類
                        ws.Cell(row, 4).Value = e.LastModified.ToString("yyyy/MM/dd HH:mm:ss");   // D: 更新日時
                        ws.Cell(row, 5).Value = e.RelFolderPath;                                  // E: 相対フォルダパス
                        ws.Cell(row, 6).Value = e.ShareableFolderPath;                            // F: フォルダパス (共有)

                        // G: フォルダを開く — 絶対パスのハイパーリンク
                        if (!string.IsNullOrEmpty(e.FolderFullPath))
                        {
                            var linkCell = ws.Cell(row, 7);
                            try
                            {
                                var uri = new Uri(e.FolderFullPath).AbsoluteUri;
                                var escaped = uri.Replace("\"", "\"\"");
                                linkCell.FormulaA1 = $"HYPERLINK(\"{escaped}\",\"開く\")";
                                linkCell.Style.Font.FontColor = linkColor;
                                linkCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                            }
                            catch
                            {
                                linkCell.Value = e.FolderFullPath;
                            }
                        }

                        // 2% 刻み進捗
                        if (total > 0)
                        {
                            int pct = (int)(10 + (i + 1) * 80.0 / total);
                            if (pct >= lastTargetPct + 2)
                            {
                                lastTargetPct = pct;
                                Volatile.Write(ref progressTarget, (double)pct);
                                statusText = $"[Excel出力] {(i + 1):N0} / {total:N0} 件を処理中... ({pct}%)";
                            }
                        }
                    }

                    // --- 全範囲スタイル（ヘッダー＋データ）---
                    int lastRow = Math.Max(entries.Count + 2, 2);
                    var fullRange = ws.Range(2, 1, lastRow, 7);
                    var borderColor = XLColor.FromHtml("#D9D9D9");
                    fullRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    fullRange.Style.Border.TopBorderColor = borderColor;
                    fullRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    fullRange.Style.Border.BottomBorderColor = borderColor;
                    fullRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                    fullRange.Style.Border.LeftBorderColor = borderColor;
                    fullRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
                    fullRange.Style.Border.RightBorderColor = borderColor;
                    fullRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                    fullRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#545B64");

                    if (lastRow >= 3)
                    {
                        var dataRange = ws.Range(3, 1, lastRow, 7);
                        dataRange.Style.Font.FontSize = 10;
                        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    // --- AutoFit + 最低幅保証 ---
                    ws.Columns(1, 7).AdjustToContents();
                    if (ws.Column(1).Width < 24) ws.Column(1).Width = 24;  // A: 名称
                    if (ws.Column(2).Width < 10) ws.Column(2).Width = 10;  // B: サイズ
                    if (ws.Column(3).Width < 10) ws.Column(3).Width = 10;  // C: 種類
                    if (ws.Column(4).Width < 20) ws.Column(4).Width = 20;  // D: 更新日時
                    if (ws.Column(5).Width < 20) ws.Column(5).Width = 20;  // E: 相対パス
                    if (ws.Column(6).Width < 24) ws.Column(6).Width = 24;  // F: 共有パス
                    if (ws.Column(7).Width < 10) ws.Column(7).Width = 10;  // G: リンク
                    // 列幅の上限（横スクロール防止）
                    for (int c = 1; c <= 7; c++)
                        if (ws.Column(c).Width > 60) ws.Column(c).Width = 60;

                    // オートフィルタ
                    ws.Range(2, 1, lastRow, 7).SetAutoFilter();
                    // ウィンドウ枠固定（ヘッダー行の下）
                    ws.SheetView.FreezeRows(2);
                    // 印刷設定
                    ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

                    Volatile.Write(ref progressTarget, 95.0);
                    statusText = "[Excel出力] Excelファイルを保存しています...";
                    workbook.SaveAs(savePath);
                    return entries.Count;
                });
                succeeded = true;
            }
            catch (IOException)
            {
                App.Notification.Notify("Excelファイルが使用中のため出力できませんでした", $"Excel出力失敗: {savePath}");
            }
            catch (Exception ex)
            {
                App.Notification.Notify("Excel出力に失敗しました", ex.Message);
            }
            finally
            {
                sw.Stop();
                var min = TimeSpan.FromMilliseconds(800);
                if (sw.Elapsed < min) await Task.Delay(min - sw.Elapsed);
                progressTimer.Stop();
                MainVM?.EndFileOperation();
            }
            if (succeeded)
                App.Notification.Notify($"Excelを出力しました（{entryCount:N0} 件）", $"Excel出力: {savePath}");
        }

        /// <summary>Excel シート名に使用不可の文字を除去し、31文字以内に切り詰める。</summary>
        private static string SanitizeSheetName(string name)
        {
            char[] invalid = { '\\', '/', '?', '*', '[', ']', ':' };
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            if (sanitized.Length > 31) sanitized = sanitized[..31];
            if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Sheet1";
            return sanitized;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // PDF 変換
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>選択ファイルを PDF へ変換して同一フォルダへ保存する。</summary>
        /// <remarks>
        /// 単一ファイル → 元のファイル名.pdf（同一フォルダ）。
        /// 複数ファイル → 各ファイルを個別変換後にページ結合して Combined_日時.pdf（同一フォルダ）。
        /// </remarks>
        public async Task ConvertToPdfAsync(IList<FileItem> files)
        {
            if (files == null || files.Count == 0) return;

            // 1. 保存先 = 操作しているカレントフォルダ（同一フォルダ保存）
            var mainVM = MainVM;
            string targetDir = CurrentPath ?? "";
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                ZenithDialog.Show("保存先フォルダを特定できません", "PDF 変換", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                return;
            }

            int total = files.Count;

            // 2. スピナー（BeginBusy）開始
            using var busyToken = mainVM?.BeginBusy();

            // 3. GlowBar 開始（左→右固定）
            mainVM?.BeginFileOperation($"[PDF変換中] 0/{total} ファイルを処理しています...",
                FlowDirection.LeftToRight);

            // WPF レンダリングサイクルを 1 回完了させてから Task.Run に入る
            // Background(4) < Render(7) なので、保留中の DataBind・Render 操作が先に処理される
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            var tempFiles = new List<string>();

            try
            {
                string finalPath = await Task.Run(() =>
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        var file   = files[i];
                        string tmp = Path.Combine(Path.GetTempPath(), $"zenith_pdf_{Guid.NewGuid():N}.pdf");
                        tempFiles.Add(tmp);

                        if (Services.PdfConversionService.IsImageFile(file.FullPath))
                            Services.PdfConversionService.CreatePdfFromSingleImage(file.FullPath, tmp);
                        else if (Services.PdfConversionService.IsOfficeFile(file.FullPath))
                            Services.PdfConversionService.ConvertOfficeToPdf(file.FullPath, tmp);
                        else
                            throw new NotSupportedException($"未対応のファイル形式: {file.FullPath}");

                        double pct = (i + 1) * 100.0 / total;
                        try { mainVM?.ReportFileOperationProgress(pct, $"[PDF変換中] {i + 1}/{total} ファイルを処理しています..."); }
                        catch { }
                    }

                    if (total == 1)
                    {
                        string destName = Path.GetFileNameWithoutExtension(files[0].Name) + ".pdf";
                        string destPath = Path.Combine(targetDir, destName);
                        File.Move(tempFiles[0], destPath, overwrite: true);
                        return destPath;
                    }
                    else
                    {
                        try { mainVM?.ReportFileOperationProgress(95, "[PDF変換中] ファイルを結合しています..."); }
                        catch { }
                        string stamp       = DateTime.Now.ToString("yyyyMMddHHmmss");
                        string combinedPath = Path.Combine(targetDir, $"Combined_{stamp}.pdf");
                        Services.PdfConversionService.MergePdfs(tempFiles, combinedPath);
                        return combinedPath;
                    }
                });

                App.Notification.Notify("PDF変換が完了しました",
                    $"[{ParentPane?.PaneLabel ?? "?"}] PDF: {Path.GetFileName(finalPath)} → {targetDir}");
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("Word") || ex.Message.Contains("Excel") ||
                ex.Message.Contains("PowerPoint") || ex.Message.Contains("Office"))
            {
                ZenithDialog.Show(
                    $"Microsoft Office がインストールされていないため変換できません。\n\n詳細: {ex.Message}",
                    "PDF 変換エラー", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
            }
            catch (OperationCanceledException)
            {
                App.Notification.Notify("PDF変換がキャンセルされました");
            }
            catch (Exception ex)
            {
                App.Notification.Notify("PDF変換に失敗しました", ex.Message);
            }
            finally
            {
                // 一時ファイル削除
                foreach (var tmp in tempFiles)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); }
                    catch { }
                }
                mainVM?.EndFileOperation();
            }
        }

        /// <summary>CSV フィールドを RFC 4180 に従いエスケープする。</summary>
        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // FileItemComparer — ヒューマン・ソート実装
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ファイル一覧のソートに使用するカスタム比較クラス。
    /// <list type="bullet">
    ///   <item>Windows Shell の StrCmpLogicalW を使った「自然順ソート」（数字を数値として比較し、file2 が file10 より前に来る）</item>
    ///   <item>名前ソート時の「ベースファイル優先」（_t1, -copy 等の接尾語付きよりベースファイルが直上に並ぶ）</item>
    ///   <item>ベース優先はソート方向（昇順/降順）に依存しないため、方向を切り替えてもベースが変異体の直上に留まる</item>
    /// </list>
    /// </summary>
    internal sealed class FileItemComparer : System.Collections.IComparer
    {
        private readonly string _sortProperty;
        private readonly ListSortDirection _direction;
        private readonly bool _foldersFirst;

        /// <summary>Windows Shell の自然順文字列比較。数字部分を数値として扱い file2 &lt; file10 を実現する。</summary>
        [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        public FileItemComparer(string sortProperty, ListSortDirection direction, bool foldersFirst)
        {
            _sortProperty = sortProperty;
            _direction = direction;
            _foldersFirst = foldersFirst;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not FileItem a || y is not FileItem b) return 0;

            // フェーズ 1: フォルダ先頭グループ（ソート方向に依存しない）
            if (_foldersFirst)
            {
                int fc = b.IsDirectory.CompareTo(a.IsDirectory);
                if (fc != 0) return fc;
            }

            int dir = _direction == ListSortDirection.Ascending ? 1 : -1;

            // フェーズ 2: 名前ソート — 自然順 + ベースファイル優先
            if (_sortProperty == "Name")
            {
                // ベース優先はソート方向に関わらず常に適用（ベースが変異体の直上に固定）
                int bp = CompareBasePriority(a, b);
                if (bp != 0) return bp;

                return StrCmpLogicalW(a.Name ?? "", b.Name ?? "") * dir;
            }

            // フェーズ 2: その他プロパティ（方向を適用）
            return CompareByProperty(a, b) * dir;
        }

        /// <summary>
        /// 一方が他方の「ベースファイル + 区切り文字 + 接尾語」形式のバリアントであれば
        /// ベース側を先に返す（-1: a が先、1: b が先、0: 関係なし）。
        /// 同一拡張子のファイル間のみ判定する。
        /// </summary>
        private static int CompareBasePriority(FileItem a, FileItem b)
        {
            string extA = System.IO.Path.GetExtension(a.Name ?? "");
            string extB = System.IO.Path.GetExtension(b.Name ?? "");

            // 拡張子が異なるファイルは同一グループとみなさない
            if (!extA.Equals(extB, StringComparison.OrdinalIgnoreCase)) return 0;

            string nameA = System.IO.Path.GetFileNameWithoutExtension(a.Name ?? "");
            string nameB = System.IO.Path.GetFileNameWithoutExtension(b.Name ?? "");

            if (IsSuffixVariant(potentialBase: nameA, candidate: nameB)) return -1; // a がベース → a が先
            if (IsSuffixVariant(potentialBase: nameB, candidate: nameA)) return 1;  // b がベース → b が先

            return 0;
        }

        /// <summary>
        /// candidate が potentialBase + 区切り文字（_ - スペース .）+ 任意文字 で始まるか判定する。
        /// 例: IsSuffixVariant("ファイル名", "ファイル名_t1") → true
        ///     IsSuffixVariant("file",      "file-copy")     → true
        ///     IsSuffixVariant("file",      "file2")         → false（区切り文字なし）
        /// </summary>
        private static bool IsSuffixVariant(string potentialBase, string candidate)
        {
            if (string.IsNullOrEmpty(potentialBase)) return false;
            if (candidate.Length <= potentialBase.Length) return false;
            if (!candidate.StartsWith(potentialBase, StringComparison.OrdinalIgnoreCase)) return false;

            char delimiter = candidate[potentialBase.Length];
            return delimiter == '_' || delimiter == '-' || delimiter == ' ' || delimiter == '.';
        }

        private int CompareByProperty(FileItem a, FileItem b) => _sortProperty switch
        {
            "LastModified" => a.LastModified.CompareTo(b.LastModified),
            "Size"         => a.Size.CompareTo(b.Size),
            "TypeName"     => StrCmpLogicalW(a.TypeName ?? "", b.TypeName ?? ""),
            "FullPath"     => StrCmpLogicalW(a.FullPath ?? "", b.FullPath ?? ""),
            _              => StrCmpLogicalW(a.Name ?? "", b.Name ?? ""),
        };
    }
}
