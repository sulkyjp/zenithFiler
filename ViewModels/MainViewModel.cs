using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenithFiler.Services;
using ZenithFiler.Services.Commands;
using ZenithFiler.ViewModels;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public enum SidebarViewMode
    {
        Favorites,
        Tree,
        History,
        IndexSearch,
        AppSettings,
        WorkingSet
    }

    /// <summary>お気に入り検索のヒット条件。</summary>
    public enum FavoritesSearchMode
    {
        /// <summary>名前・概要に含まれる場合にヒット。</summary>
        NameAndDescription,
        /// <summary>フルパス・概要に含まれる場合にヒット。</summary>
        PathAndDescription
    }

    public partial class MainViewModel : BaseViewModel, IBusyTokenProvider
    {
        private readonly string _baseAppTitle;

        [ObservableProperty]
        private string _appTitle;

        public FilePaneViewModel LeftPane { get; }
        public FilePaneViewModel RightPane { get; }

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<HistoryItemViewModel> _historyItems = new();

        public ICollectionView HistoryView { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RightPaneWidth))]
        [NotifyPropertyChangedFor(nameof(SplitterWidth))]
        private int _paneCount = 2;

        /// <summary>検索実行前に1画面モードに切り替えた場合の、元のペイン数。</summary>
        private int? _previousPaneCountBeforeSearch;

        [ObservableProperty]
        private bool _isAlwaysOnTop = false;

        partial void OnIsAlwaysOnTopChanged(bool value)
        {
            if (_notificationEnabled)
                App.Notification.Notify(value ? "常に最前面をオンにしました" : "常に最前面をオフにしました", $"常に最前面: {(value ? "オン" : "オフ")}");
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGeneralBusy))]
        private bool _isBusy;

        private int _busyCount;
        private readonly object _busyLock = new();

        /// <summary>
        /// バックグラウンド処理を開始し、IsBusy を true にします。
        /// 戻り値の IDisposable を Dispose すると IsBusy が解除されます（参照カウント方式）。
        /// </summary>
        public IDisposable BeginBusy()
        {
            lock (_busyLock)
            {
                _busyCount++;
                IsBusy = true;
            }
            return new BusyToken(this);
        }

        private void EndBusy()
        {
            lock (_busyLock)
            {
                _busyCount--;
                if (_busyCount <= 0)
                {
                    _busyCount = 0;
                    IsBusy = false;
                }
            }
        }

        private class BusyToken : IDisposable
        {
            private readonly MainViewModel _vm;
            private bool _disposed;
            public BusyToken(MainViewModel vm) => _vm = vm;
            public void Dispose()
            {
                if (!_disposed)
                {
                    _vm.EndBusy();
                    _disposed = true;
                }
            }
        }

        // ─── ファイル操作進捗（Zenith Turbo Engine） ────────────────────────────────

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGeneralBusy))]
        private bool _isFileOperationActive;

        [ObservableProperty]
        private double _fileOperationProgress;

        [ObservableProperty]
        private string _fileOperationStatusText = string.Empty;

        /// <summary>
        /// コピー/移動の空間方向。左→右転送は LeftToRight、右→左転送は RightToLeft。
        /// GlowProgressBar の FlowDirection にバインドされ、バーが物理的な移動方向に伸びる。
        /// </summary>
        [ObservableProperty]
        private FlowDirection _progressFlowDirection = FlowDirection.LeftToRight;

        /// <summary>スピナー表示条件：ビジー中かつファイル操作進行中ではない。</summary>
        public bool IsGeneralBusy => IsBusy && !IsFileOperationActive;

        private CancellationTokenSource? _fileOperationCts;

        // キャンセル状態フラグ（UIスレッドのみで読み書き）
        private bool _cancelWasPressed;

        /// <summary>
        /// キャンセルボタン押下時に発火する。
        /// コードビハインドがリトラクション（逆行）アニメーションを実行するためのフック。
        /// </summary>
        public event EventHandler? CancelRetractionRequested;

        // スロットリング用フィールド（複数スレッドから読み書きされるため long / Interlocked を使用）
        private long _lastProgressReportTick;    // 最終報告のTickCount64
        private long _lastReportedProgressBits;  // 最終報告の進捗率（double を long bits に変換して Interlocked 操作）

        /// <summary>現在のファイル操作キャンセル用トークン。操作がなければ CancellationToken.None。</summary>
        public CancellationToken FileOperationToken => _fileOperationCts?.Token ?? CancellationToken.None;

        /// <summary>
        /// ファイル操作（コピー/移動）を開始し、進捗追跡を初期化します。
        /// <paramref name="initialStatus"/> を指定すると、グロウバーが出た直後に表示するメッセージを設定できます。
        /// <paramref name="flowDirection"/> でバーが伸びる方向（転送方向）を指定します。
        /// </summary>
        public void BeginFileOperation(string initialStatus = "", FlowDirection flowDirection = FlowDirection.LeftToRight)
        {
            _cancelWasPressed = false;
            _fileOperationCts?.Cancel();
            _fileOperationCts?.Dispose();
            _fileOperationCts = new CancellationTokenSource();
            Interlocked.Exchange(ref _lastProgressReportTick, 0);
            Interlocked.Exchange(ref _lastReportedProgressBits, 0L);
            FileOperationProgress = 0;
            FileOperationStatusText = initialStatus;
            ProgressFlowDirection = flowDirection;
            IsFileOperationActive = true;
        }

        /// <summary>
        /// ファイル操作を終了します。
        /// 正常完了時: バーを 100% まで伸ばして 300ms 表示後にフェードアウト。
        /// キャンセル時: リトラクションアニメーション（0.3s）の完了を待ってからフェードアウト。
        /// </summary>
        public void EndFileOperation()
        {
            // キャンセル時フラグをここで読み取り、resetする（UIスレッド上でのみ呼ばれる想定）
            bool wasCancelled = _cancelWasPressed;
            _cancelWasPressed = false;

            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    if (wasCancelled)
                    {
                        // キャンセル時: コードビハインドのリトラクションアニメーション（0.3s）完了を待つ
                        await Task.Delay(350);
                    }
                    else
                    {
                        // 正常完了時: 100% まで伸ばして一瞬見せてからフェードアウト
                        FileOperationProgress = 100;
                        await Task.Delay(300);
                    }
                    IsFileOperationActive = false;  // Grid の Opacity フェードアウト開始 (0.5s)
                    await Task.Delay(500);           // フェードアウト完了を待ってからリセット
                    FileOperationProgress = 0;
                    FileOperationStatusText = string.Empty;
                }
                catch { /* ウィンドウ閉鎖時など、UI 更新失敗は無視 */ }
                finally
                {
                    _cancelWasPressed = false;
                    _fileOperationCts?.Dispose();
                    _fileOperationCts = null;
                }
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// ファイル操作の進捗を更新します（任意スレッドから呼び出し可能）。
        /// 「前回報告から 200ms 経過」または「進捗率が 1% 以上変化」のいずれかを満たすときのみ
        /// UI を更新し、17GB/800 件超の大規模転送時でも描画 CPU 消費を最小限に抑えます。
        /// CAS により複数スレッドからの競合的な呼び出しを 1 回に絞ります。
        /// </summary>
        public void ReportFileOperationProgress(double progress, string statusText)
        {
            long now = Environment.TickCount64;
            long prevTick = Interlocked.Read(ref _lastProgressReportTick);
            double prevProgress = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _lastReportedProgressBits));
            double delta = Math.Abs(progress - prevProgress);

            // 200ms 未満かつ 1% 未満の変化はスキップ（CPU 節約）
            if (now - prevTick < 200 && delta < 1.0) return;

            // CAS で更新権を取得（競合時はスキップして Dispatcher への過剰投入を防ぐ）
            if (Interlocked.CompareExchange(ref _lastProgressReportTick, now, prevTick) != prevTick) return;
            Interlocked.Exchange(ref _lastReportedProgressBits, BitConverter.DoubleToInt64Bits(progress));

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    FileOperationProgress = progress;
                    FileOperationStatusText = statusText;
                }
                catch { /* UI 更新失敗は無視（ウィンドウ閉鎖時など） */ }
            }, DispatcherPriority.Background);
        }

        [RelayCommand]
        private void CancelFileOperation()
        {
            _cancelWasPressed = true;
            _fileOperationCts?.Cancel();
            // コードビハインドにリトラクション（逆行）アニメーション開始を通知する
            CancelRetractionRequested?.Invoke(this, EventArgs.Empty);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SidebarColumnWidth))]
        private bool _isSidebarVisible = true;

        partial void OnIsSidebarVisibleChanged(bool value)
        {
            if (_notificationEnabled)
                App.Notification.Notify(value ? "ナビペインを表示しました" : "ナビペインを非表示にしました", $"ナビペイン表示: {(value ? "オン" : "オフ")}");
        }

        /// <summary>ナビペインの幅を任意変更不可にするロック。お気に入りビュー時のみ表示。ツリー/履歴の自動拡張はロック時も有効。</summary>
        [ObservableProperty]
        private bool _isNavWidthLocked = false;

        partial void OnIsNavWidthLockedChanged(bool value)
        {
            if (_notificationEnabled)
                App.Notification.Notify(value ? "ナビの幅をロックしました" : "ナビの幅のロックを解除しました", $"ナビ幅ロック: {(value ? "オン" : "オフ")}");
        }

        /// <summary>ツリービュー内でのフォルダ移動・リネーム・削除を禁止するロック。ツリービュー表示時のみ有効。ナビペインロック仕様に従う。</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RequestRenameTreeFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(RequestDeleteTreeFolderCommand))]
        private bool _isTreeViewLocked = false;

        /// <summary>ツリービューロック解除を促すアニメーション用フラグ（ナビペインロック仕様）。</summary>
        [ObservableProperty]
        private bool _isTreeViewLockWarningActive = false;

        private CancellationTokenSource? _treeViewLockWarningCts;

        partial void OnIsTreeViewLockedChanged(bool value)
        {
            if (!value)
            {
                _treeViewLockWarningCts?.Cancel();
                IsTreeViewLockWarningActive = false;
            }
            if (_notificationEnabled)
                App.Notification.Notify(value ? "ツリービューをロックしました" : "ツリービューのロックを解除しました", $"ツリービューロック: {(value ? "オン" : "オフ")}");
        }

        /// <summary>ナビペインロック仕様: ツリービューロック中に禁止操作を試みたときに南京錠付近の矢印＋点滅アニメーションを約3秒表示する。</summary>
        public void TriggerTreeViewLockWarning()
        {
            _treeViewLockWarningCts?.Cancel();
            _treeViewLockWarningCts = new CancellationTokenSource();
            var token = _treeViewLockWarningCts.Token;
            IsTreeViewLockWarningActive = true;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000, token);
                }
                catch (OperationCanceledException) { }
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                        IsTreeViewLockWarningActive = false;
                });
            });
        }

        /// <summary>ツリービュー・履歴ビュー共通のナビペイン幅（履歴で横スクロールが出ない幅：名前150+参照日時110+回数40+ヘッダ/セル余白+マージン）。</summary>
        private const double ExpandedSidebarWidth = 340;

        /// <summary>お気に入りビュー用に保持する幅。ツリー/履歴から戻ったときに復元する。</summary>
        private double _sidebarWidthFavoritesTree = 220;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SidebarColumnWidth))]
        private GridLength _sidebarWidth = new GridLength(220);

        [ObservableProperty]
        private string _historySearchText = string.Empty;

        /// <summary>お気に入り検索テキスト。Favorites.SearchText と同期し、バインディングの確実性を確保する。</summary>
        [ObservableProperty]
        private string _favoritesSearchText = string.Empty;

        partial void OnFavoritesSearchTextChanged(string value)
        {
            if (Favorites.SearchText != value)
                Favorites.SearchText = value;
        }

        partial void OnHistorySearchTextChanged(string value)
        {
            // 検索時はグループ化を解除して一覧表示、クリア時は日付グループに戻す
            if (string.IsNullOrWhiteSpace(value))
            {
                if (HistoryView.GroupDescriptions.Count == 0)
                {
                    HistoryView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HistoryItemViewModel.LastAccessedDate)));
                }
            }
            else
            {
                HistoryView.GroupDescriptions.Clear();
            }

            HistoryView.Refresh();
        }

        [RelayCommand]
        private void ClearHistorySearch()
        {
            HistorySearchText = string.Empty;
            App.Notification.Notify("履歴検索をクリアしました");
        }


        public GridLength SidebarColumnWidth => IsSidebarVisible ? SidebarWidth : new GridLength(0);

        /// <summary>設定保存用。お気に入り・ツリービュー時の幅を返す（履歴ビュー時も拡張前の幅を保持）。</summary>
        public double NormalSidebarWidth => _sidebarWidthFavoritesTree;

        /// <summary>現在の SidebarMode に応じた目標幅。ビュー側のアニメーションで使用。</summary>
        public double TargetSidebarWidth =>
            SidebarMode is SidebarViewMode.History or SidebarViewMode.Tree
                or SidebarViewMode.IndexSearch or SidebarViewMode.AppSettings
                or SidebarViewMode.WorkingSet
            ? ExpandedSidebarWidth : _sidebarWidthFavoritesTree;

        /// <summary>ナビペイン幅のアニメーション中は true。この間は OnSidebarWidthChanged で _sidebarWidthFavoritesTree を更新しない（ユーザー設定幅を保持するため）。</summary>
        public bool IsSidebarWidthAnimating { get; set; }

        partial void OnSidebarWidthChanged(GridLength value)
        {
            // アニメーション中はお気に入り幅を上書きしない（ツリー/履歴→お気に入り復帰時にユーザー設定幅へ正しく戻すため）
            if (!IsSidebarWidthAnimating && SidebarMode == SidebarViewMode.Favorites && value.IsAbsolute && value.Value > 0)
                _sidebarWidthFavoritesTree = value.Value;
            OnPropertyChanged(nameof(TargetSidebarWidth));
        }


        public event Action? FocusRequested;
        public event Action? FocusHistorySearchRequested;
        public event Action? FocusActiveSearchRequested;
        public event Action? FocusIndexSearchRequested;

        /// <summary>お気に入り登録後、対象項目を表示するためのスクロール要求。View が購読して ScrollIntoView を実行する。</summary>
        public Action<FavoriteItem>? RequestScrollToFavorite { get; set; }

        /// <summary>インデックス検索登録後、対象項目を表示するためのスクロール要求。View が購読して ScrollIntoView を実行する。</summary>
        public Action<IndexSearchTargetItemViewModel>? RequestScrollToIndexSearchTarget { get; set; }

        /// <summary>
        /// ローディング画面（オーバーレイ）を表示しながら非同期処理を実行します。
        /// 処理中は操作がブロックされます。
        /// </summary>
        public async Task RunBusyAsync(Func<Task> action, string message = "読み込み中...")
        {
            LoadingMessage = message;
            
            // ローディングオーバーレイとビジーの両方を有効化
            IsLoading = true;
            using (BeginBusy())
            {
                try
                {
                    await action();
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        /// <summary>
        /// バックグラウンドで非同期処理を実行します。
        /// ステータスバーにインジケータを表示しますが、操作はブロックしません。
        /// </summary>
        public async Task RunBackgroundAsync(Func<Task> action)
        {
            using (BeginBusy())
            {
                await action();
            }
        }

        [ObservableProperty]
        private FilePaneViewModel _activePane;

        public bool SuppressFocusRequest { get; set; }

        partial void OnActivePaneChanged(FilePaneViewModel value)
        {
            if (LeftPane != null) LeftPane.IsActive = (value == LeftPane);
            if (RightPane != null) RightPane.IsActive = (value == RightPane);

            if (value != null)
            {
                value.SelectedTab?.RefreshIfNeededOnTabFocus();
                if (SidebarMode == SidebarViewMode.Tree)
                {
                    _ = DirectoryTree.ExpandToPathAsync(value.CurrentPath);
                }
            }
            
            // ペイン切り替え時にフォーカスも移動させる
            if (!SuppressFocusRequest)
            {
                FocusRequested?.Invoke();
            }

            UpdateWindowTitle();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetSidebarWidth))]
        private SidebarViewMode _sidebarMode = SidebarViewMode.Favorites;

        partial void OnSidebarModeChanged(SidebarViewMode value)
        {
            // ワーキングセット以外へ切り替えたときはプレビューをロールバック
            if (value != SidebarViewMode.WorkingSet && ProjectSets?.IsPreviewActive == true)
                _ = ProjectSets.CancelPreviewInternalAsync();

            if (value == SidebarViewMode.History)
            {
                // 履歴ビューは初回表示時のみロード（起動時は読まない）
                _ = RefreshHistoryAsync();
            }
            else if (value == SidebarViewMode.Tree)
            {
                // ツリービューは初回表示時に非同期でドライブ一覧をロードしてから展開
                _ = InitializeTreeViewAsync();
            }
            else if (value == SidebarViewMode.IndexSearch)
            {
                // インデックス検索設定ビュー：表示切替時に Items を再通知し、空状態 Border と ListView の表示を正しく更新する
                IndexSearchSettings.NotifyItemsChanged();
                IndexSearchSettings.RefreshStatus();
            }
            else if (value == SidebarViewMode.AppSettings)
            {
                // アプリ設定ビュー：特別な初期化は不要
            }
            else if (value == SidebarViewMode.WorkingSet)
            {
                // ワーキングセットビュー：特別な初期化は不要
            }
            else
            {
                // お気に入りビューに戻ったとき：ユーザー設定幅に復元（アニメーションで行う）。不正値時のみ補正。
                if (_sidebarWidthFavoritesTree <= 0 && SidebarWidth.IsAbsolute)
                    _sidebarWidthFavoritesTree = Math.Max(180, SidebarWidth.Value);
            }
            // 幅の変更はビュー側のアニメーションで行う（即時設定しない）
        }

        /// <summary>ツリービュー初回表示時に非同期でドライブをロードし、アクティブペインのパスに展開する。</summary>
        private async Task InitializeTreeViewAsync()
        {
            if (DirectoryTree.Drives.Count == 0)
                await DirectoryTree.LoadDrivesAsync();
            if (ActivePane != null)
                _ = DirectoryTree.ExpandToPathAsync(ActivePane.CurrentPath);
        }

        /// <summary>設定読み込み時に、お気に入り・ツリービュー用の幅を初期化する。</summary>
        public void SetNormalSidebarWidth(double width)
        {
            if (width > 0)
                _sidebarWidthFavoritesTree = width;
        }

        public DirectoryTreeViewModel DirectoryTree { get; }
        public FavoritesViewModel Favorites { get; }
        public IndexSearchSettingsViewModel IndexSearchSettings { get; }
        public SearchFilterViewModel SearchFilter { get; } = new();
        public SearchPresetViewModel SearchPresets { get; }
        public AppSettingsViewModel AppSettings { get; }
        public ProjectSetsViewModel ProjectSets { get; }
        public NotificationService Notification => App.Notification;
        public UndoService UndoService => UndoService.Instance;

        private readonly TreeFolderOperationHandler _treeFolderHandler;

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            UndoService.Undo();
        }

        private bool CanUndo() => UndoService.CanUndo;

        /// <summary>ナビペインのディレクトリツリーを再読み込みする（空白右クリックメニュー用）。</summary>
        [RelayCommand]
        private async Task RefreshDirectoryTree()
        {
            await DirectoryTree.LoadDrivesAsync();
        }

        /// <summary>初期化・設定復元完了後に true にし、設定トグルの通知を有効にする。</summary>
        private bool _notificationEnabled;

        /// <summary>保存済みの検索結果フィルター状態を全タブに適用する。設定読み込み後に呼び出し。</summary>
        internal void ApplySearchResultFilterStateToAllTabs()
        {
            var state = AppSettings.GetSearchResultFileTypeFilterState();
            if (state.Count != 11) return;
            foreach (var tab in LeftPane.Tabs)
                tab.ApplySavedFilterState(state);
            foreach (var tab in RightPane.Tabs)
                tab.ApplySavedFilterState(state);
        }

        /// <summary>ウィンドウの設定復元が完了したあとに View から呼ぶ。これ以降、設定変更時にステータスバー通知を行う。</summary>
        public void MarkInitializationComplete()
        {
            _notificationEnabled = true;
            Favorites.MarkNotificationEnabled();
        }

        public GridLength RightPaneWidth => PaneCount == 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        public GridLength SplitterWidth => PaneCount == 2 ? GridLength.Auto : new GridLength(0);

        public MainViewModel()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            _baseAppTitle = $"Zenith Filer v{version}";
            _appTitle = _baseAppTitle;

            LeftPane = new FilePaneViewModel(string.Empty, "A");
            RightPane = new FilePaneViewModel(string.Empty, "B");
            ActivePane = LeftPane;
            LeftPane.IsActive = true;

            LeftPane.PropertyChanged += OnPanePropertyChanged;
            RightPane.PropertyChanged += OnPanePropertyChanged;

            App.Notification.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NotificationService.IsIndexing))
                {
                    CancelIndexingCommand.NotifyCanExecuteChanged();
                    if (!App.Notification.IsIndexing)
                    {
                        App.Notification.IndexingStatusMessage = string.Empty;
                        IndexSearchSettings?.RefreshIndexedDocumentCount();
                        IndexSearchSettings?.RefreshItemsStatus();
                    }
                }
            };

            DirectoryTree = new DirectoryTreeViewModel(path => 
            {
                if (ActivePane != null)
                {
                    // 文字列比較の前にパスを正規化して冗長なナビゲーションを防ぐ
                    string normalizedTarget = PathHelper.GetPhysicalPath(path);
                    string normalizedCurrent = PathHelper.GetPhysicalPath(ActivePane.CurrentPath);
                    
                    if (!normalizedTarget.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                    {
                        ActivePane.NavigateCommand.Execute(path);
                    }
                }
            });

            _treeFolderHandler = new TreeFolderOperationHandler(
                () => IsTreeViewLocked,
                TriggerTreeViewLockWarning,
                UndoService.Instance);

            Favorites = new FavoritesViewModel(this);
            IndexSearchSettings = new IndexSearchSettingsViewModel(this);
            SearchPresets = new SearchPresetViewModel(this);
            AppSettings = new AppSettingsViewModel(this);
            ProjectSets = new ProjectSetsViewModel(this);

            // インデックス更新モード・間隔・パフォーマンス設定の変更時に即座に IndexService へ反映
            AppSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(AppSettingsViewModel.IndexUpdateMode)
                    or nameof(AppSettingsViewModel.IndexUpdateIntervalHours)
                    or nameof(AppSettingsViewModel.IndexFullRebuildCooldownHours)
                    or nameof(AppSettingsViewModel.IndexEcoMode)
                    or nameof(AppSettingsViewModel.IndexNetworkLowPriority))
                {
                    App.IndexService.ConfigureIndexUpdate(AppSettings.GetIndexSettingsForSave(), () => IndexSearchSettings.GetPathsForSave() ?? new List<string>());
                }
            };

            // 各フォルダのインデックス作成完了時に即座にナビビューへ反映
            App.IndexService.RootIndexed += (path) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    IndexSearchSettings?.RefreshIndexedDocumentCount();
                    IndexSearchSettings?.RefreshItemsStatus();
                });
            };

            // UndoServiceの変更監視
            UndoService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UndoService.CanUndo))
                {
                    UndoCommand.NotifyCanExecuteChanged();
                }
            };

            HistoryView = CollectionViewSource.GetDefaultView(HistoryItems);
            HistoryView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HistoryItemViewModel.LastAccessedDate)));
            HistoryView.SortDescriptions.Add(new SortDescription(nameof(HistoryItemViewModel.LastAccessed), ListSortDirection.Descending));
            HistoryView.Filter = FilterHistoryItem;
        }

        public async Task InitializeAsync(WindowSettings settings)
        {
            // DB 初期化の完了は起動に不要 — 履歴表示時に初めて必要になる
            // await App.StartupInitTask は行わず、DB アクセス時に遅延待機させる
            App.Database.HistoryChanged += (s, e) =>
            {
                if (SidebarMode == SidebarViewMode.History)
                    _ = RefreshHistoryAsync();
            };

            _ = App.FileLogger.CleanupOldLogsAsync();

            // 設定値のパースのみ実行。パスの「実在確認」は行わない。
            // 無効パスは各タブの NavigateAsync 内でフォールバック処理される。
            EnsureTabPaths(settings.LeftPane, isRight: false);
            EnsureTabPaths(settings.RightPane, isRight: true);

            // タブ復元（fire-and-forget: NavigateAsync 内で無効パスは安全に処理される）
            _ = LeftPane.RestoreTabsAsync(settings.LeftPane);
            _ = RightPane.RestoreTabsAsync(settings.RightPane);

            // フィルター適用
            await Application.Current.Dispatcher.InvokeAsync(() => ApplySearchResultFilterStateToAllTabs());

            // ツリーも fire-and-forget（ドライブ列挙のタイムアウト待ちでUIを止めない）
            if (SidebarMode == SidebarViewMode.Tree)
            {
                _ = InitializeTreeViewAsync();
            }
        }

        /// <summary>
        /// TabPaths が空の場合に、設定値の文字列解決のみで即座にフォールバックする（I/O なし）。
        /// パスの実在確認は各タブの NavigateAsync に委ねる。
        /// </summary>
        private static void EnsureTabPaths(PaneSettings pane, bool isRight)
        {
            if (pane.TabPaths is { Count: > 0 }) return;
            // CurrentPath があればそのまま採用（実在確認は NavigateAsync に委ねる）
            if (!string.IsNullOrWhiteSpace(pane.CurrentPath))
            {
                pane.TabPaths = new List<string> { PathHelper.GetPhysicalPath(pane.CurrentPath) };
                return;
            }
            // フォールバック（純粋な文字列操作のみ）
            var fallback = isRight
                ? PathHelper.GetDownloadsPath()
                : PathHelper.GetInitialPath(Environment.SpecialFolder.Desktop);
            pane.TabPaths = new List<string> { fallback };
        }

        private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilePaneViewModel.CurrentPath))
            {
                var pane = sender as FilePaneViewModel;
                if (pane != null)
                {
                    if (pane == ActivePane && SidebarMode == SidebarViewMode.Tree)
                    {
                        _ = DirectoryTree.ExpandToPathAsync(pane.CurrentPath);
                    }
                    if (pane == ActivePane)
                    {
                        UpdateWindowTitle();
                    }
                }
            }
        }

        /// <summary>
        /// アクティブペインのパスに応じてウィンドウタイトルを更新する。Dispatcher 経由で UI スレッドに送り、Popup/ContextMenu の描画に干渉しないようにする。
        /// </summary>
        private void UpdateWindowTitle()
        {
            var path = ActivePane?.CurrentPath ?? string.Empty;
            var title = string.IsNullOrEmpty(path) ? _baseAppTitle : $"{_baseAppTitle} - {path}";
            Application.Current.Dispatcher.InvokeAsync(() => { AppTitle = title; }, DispatcherPriority.Background);
        }

        [RelayCommand]
        private void SetPaneCount(string countStr)
        {
            if (int.TryParse(countStr, out int count))
            {
                PaneCount = count;
                App.Notification.Notify(count == 1 ? "1ペイン表示に切り替えました" : "2ペイン表示に切り替えました",
                    $"ペイン数変更: {count} ペイン");
            }
        }

        /// <summary>反対ペインに検索結果タブを新規作成。1ペイン時は 2 ペインへ切替えてから実行。</summary>
        public void OpenSearchInOtherPane(FilePaneViewModel sourcePane, string path, string query, bool isIndexSearch)
        {
            if (PaneCount == 1)
            {
                PaneCount = 2;
                App.Notification.Notify("2ペイン表示に切り替えました", "検索結果を反対ペインに表示");
            }

            var targetPane = sourcePane == LeftPane ? RightPane : LeftPane;
            targetPane.OpenSearchTab(path, query, isIndexSearch);
            ActivePane = targetPane;
        }

        /// <summary>Bペインで検索した際に、検索タブをAペインに移動して1画面モードにする。</summary>
        public void MoveSearchToLeftPaneAndSwitchToSinglePane(FilePaneViewModel sourcePane, string path, string query, bool isIndexSearch)
        {
            if (sourcePane != RightPane) return; // Bペイン以外は処理しない

            // 1. Aペインに検索タブを作成
            LeftPane.OpenSearchTab(path, query, isIndexSearch);

            // 2. 1画面モードに切り替え
            if (PaneCount == 2)
            {
                PaneCount = 1;
                App.Notification.Notify("1画面モードに切り替えました", "検索結果を表示");
            }

            // 3. Aペインをアクティブに
            ActivePane = LeftPane;
        }

        /// <summary>検索実行前に1画面モードに切り替える場合、現在のペイン数を記録する。</summary>
        public void RecordPaneCountBeforeSearch()
        {
            if (PaneCount == 2)
            {
                _previousPaneCountBeforeSearch = 2;
            }
        }

        /// <summary>検索結果タブが全て閉じられた場合、記録されたペイン数に復元する。</summary>
        public void RestorePaneCountAfterSearch()
        {
            if (_previousPaneCountBeforeSearch.HasValue && PaneCount == 1)
            {
                PaneCount = _previousPaneCountBeforeSearch.Value;
                _previousPaneCountBeforeSearch = null;
                App.Notification.Notify($"{PaneCount}ペイン表示に戻しました", "検索結果を閉じました");
            }
        }

        /// <summary>指定タブを閉じた後、検索結果タブが残っているかチェックし、残っていなければペイン数を復元する。</summary>
        public void RestorePaneCountAfterSearchIfNeeded(TabItemViewModel? closedTab)
        {
            if (!_previousPaneCountBeforeSearch.HasValue) return;

            // 両ペインに検索結果タブが残っているかチェック
            bool hasOtherSearchTabs = LeftPane.Tabs.Any(t => t.IsSearchResultTab && t != closedTab) ||
                                      RightPane.Tabs.Any(t => t.IsSearchResultTab && t != closedTab);

            if (!hasOtherSearchTabs)
            {
                RestorePaneCountAfterSearch();
            }
        }

        /// <summary>反対ペインに指定パスを新規タブで開く。1ペイン時は 2 ペインへ切替えてから実行。</summary>
        public void OpenPathInOtherPane(FilePaneViewModel sourcePane, string physicalPath, bool isFile)
        {
            if (PaneCount == 1)
            {
                PaneCount = 2;
                App.Notification.Notify("2ペイン表示に切り替えました", "反対ペインに表示");
            }

            var targetPane = sourcePane == LeftPane ? RightPane : LeftPane;
            if (isFile)
            {
                string? parent = Path.GetDirectoryName(physicalPath);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    targetPane.AddTabWithPathCommand.Execute(parent);
                    if (targetPane.SelectedTab is TabItemViewModel newTab)
                    {
                        newTab.RequestFocusAfterRefresh = true;
                        newTab.PastedFileNamesToSelect = new List<string> { Path.GetFileName(physicalPath) };
                    }
                    App.Notification.Notify("新しいタブでフォルダを開きました", $"新しいタブで開く: {parent}");
                }
            }
            else
            {
                targetPane.AddTabWithPathCommand.Execute(physicalPath);
                App.Notification.Notify("新しいタブでフォルダを開きました", $"新しいタブで開く: {physicalPath}");
            }
            ActivePane = targetPane;
            // 新規タブの描画完了後にフォーカスを当てる
            Application.Current.Dispatcher.InvokeAsync(() => FocusRequested?.Invoke(), DispatcherPriority.Loaded);
        }

        /// <summary>指定タブを別ペインに移動する。1ペイン時は2ペインに切替えてから移動する。</summary>
        /// <param name="targetIndex">挿入位置。-1 の場合は末尾に追加。</param>
        public void MoveTabToPane(TabItemViewModel tab, FilePaneViewModel targetPane, int targetIndex = -1)
        {
            var sourcePane = tab.ParentPane;
            if (sourcePane == null || sourcePane == targetPane) return;

            if (PaneCount == 1)
            {
                PaneCount = 2;
            }

            _ = App.Current.Dispatcher.InvokeAsync(() =>
            {
                int index = sourcePane.Tabs.IndexOf(tab);
                bool wasSelected = (sourcePane.SelectedTab == tab);
                sourcePane.Tabs.Remove(tab);

                if (sourcePane.Tabs.Count == 0)
                {
                    sourcePane.EnsureAtLeastOneTab();
                }
                else if (wasSelected)
                {
                    int selIndex = Math.Min(index, sourcePane.Tabs.Count - 1);
                    sourcePane.SelectedTab = sourcePane.Tabs[selIndex];
                }

                tab.ParentPane = targetPane;
                tab.CloseTabCommand = new RelayCommand(() => targetPane.CloseTab(tab));

                if (targetIndex >= 0 && targetIndex <= targetPane.Tabs.Count)
                    targetPane.Tabs.Insert(targetIndex, tab);
                else
                    targetPane.Tabs.Add(tab);

                targetPane.SelectedTab = tab;
                ActivePane = targetPane;

                string targetLabel = targetPane.PaneLabel == "A" ? "Aペイン" : "Bペイン";
                int finalIndex = targetPane.Tabs.IndexOf(tab);
                App.Notification.Notify($"タブを{targetLabel}に移動しました", $"[Tab] Moved: {tab.CurrentPath} to {targetLabel} Index {finalIndex}");
                _ = App.FileLogger.LogAsync($"[Tab] Moved: {tab.CurrentPath} to {targetLabel} Index {finalIndex}");
            });
        }

        /// <summary>フォーカス中のペインの表示モードを変更する。</summary>
        [RelayCommand]
        private void SetFileViewMode(FileViewMode mode)
        {
            ActivePane?.SelectedTab?.ChangeFileViewModeCommand.Execute(mode);
        }

        [RelayCommand]
        private void SetSidebarMode(SidebarViewMode mode)
        {
            SidebarMode = mode;
            var modeText = mode switch { SidebarViewMode.Favorites => "お気に入り", SidebarViewMode.Tree => "ツリー", SidebarViewMode.History => "参照履歴", SidebarViewMode.IndexSearch => "インデックス検索設定", SidebarViewMode.AppSettings => "アプリ設定", SidebarViewMode.WorkingSet => "ワーキングセット", _ => "お気に入り" };
            App.Notification.Notify($"ナビを「{modeText}」に切り替えました", $"サイドバーモード変更: {modeText}");
        }

        /// <summary>アプリ設定ビュー内のインデックスセクションへスクロールしてほしいときに発火。</summary>
        public event Action? AppSettingsIndexSectionRequested;

        /// <summary>インデックスビューからアプリ設定ビューへ切り替える。</summary>
        public void RequestSwitchToAppSettings()
        {
            SetSidebarMode(SidebarViewMode.AppSettings);
        }

        /// <summary>
        /// インデックスビューからアプリ設定ビューへ切り替え、インデックス設定セクションが見える位置までスクロールする。
        /// </summary>
        public void RequestSwitchToAppSettingsIndexSection()
        {
            SetSidebarMode(SidebarViewMode.AppSettings);
            AppSettingsIndexSectionRequested?.Invoke();
        }

        [RelayCommand]
        private void FocusActivePane()
        {
            FocusRequested?.Invoke();
            App.Notification.Notify("ファイルリストにフォーカスしました");
        }

        [RelayCommand]
        private void FocusHistorySearch()
        {
            if (SidebarMode != SidebarViewMode.History)
            {
                SidebarMode = SidebarViewMode.History;
                App.Notification.Notify("ナビを「参照履歴」に切り替えました", "サイドバーモード変更: 参照履歴");
            }
            FocusHistorySearchRequested?.Invoke();
            App.Notification.Notify("履歴検索にフォーカスしました");
        }

        /// <summary>アクティブなペインの検索バーにフォーカスする（Ctrl+F）。</summary>
        [RelayCommand]
        private void FocusActiveSearch()
        {
            FocusActiveSearchRequested?.Invoke();
        }

        /// <summary>検索バーにフォーカスし、インデックス検索モードに入る（Ctrl+Shift+F）。</summary>
        [RelayCommand]
        private void FocusIndexSearch()
        {
            FocusIndexSearchRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanCancelIndexing))]
        private void CancelIndexing()
        {
            var result = ZenithDialog.Show(
                "インデックス作成を中止しますか？",
                "インデックス作成の中止",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Question);

            if (result == ZenithDialogResult.Yes)
            {
                App.IndexService.CancelIndexing();
                App.Notification.Notify("インデックス作成を中止しました");
            }
        }

        private bool CanCancelIndexing() => Notification.IsIndexing;

        [RelayCommand]
        private void ShowChangelog()
        {
            var main = Application.Current.MainWindow;
            // 旧 ChangelogWindow の代わりに、DocViewer ベースの ManualWindow を
            // 「更新履歴」タブをアクティブにした状態で開く
            var window = new ManualWindow(startWithChangelog: true);
            window.Owner = main;
            if (main != null) window.Topmost = main.Topmost;
            window.ShowDialog();
            App.Notification.Notify("更新履歴を表示しました");
        }

        [RelayCommand]
        private void ShowManual()
        {
            var main = Application.Current.MainWindow;
            var window = new ManualWindow();
            window.Owner = main;
            if (main != null) window.Topmost = main.Topmost;
            window.ShowDialog();
            App.Notification.Notify("マニュアルを表示しました");
        }

        [RelayCommand]
        private void OpenLogFolder()
        {
            App.FileLogger.OpenLogFolder();
            App.Notification.Notify("ログフォルダを開きました");
        }

        public async Task RefreshHistoryAsync()
        {
            var records = await App.Database.GetHistoryAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                HistoryItems.Clear();
                foreach (var record in records)
                {
                    HistoryItems.Add(new HistoryItemViewModel(record));
                }
                HistoryView.Refresh();
            }, DispatcherPriority.Normal);
        }

        [RelayCommand]
        private void OpenHistory(HistoryItemViewModel item)
        {
            if (item != null && ActivePane != null)
            {
                ActivePane.NavigateCommand.Execute(item.Path);
                App.Notification.Notify("履歴からフォルダを開きました", $"参照履歴から開く: {item.Path}");
            }
        }

        [RelayCommand]
        private void OpenHistoryInAPane(HistoryItemViewModel item)
        {
            if (item != null)
            {
                LeftPane.NavigateCommand.Execute(item.Path);
                App.Notification.Notify("Aペインに履歴のフォルダを表示しました", $"Aペインに表示: {item.Path}");
            }
        }

        [RelayCommand]
        private void OpenHistoryInBPane(HistoryItemViewModel item)
        {
            if (item != null)
            {
                RightPane.NavigateCommand.Execute(item.Path);
                App.Notification.Notify("Bペインに履歴のフォルダを表示しました", $"Bペインに表示: {item.Path}");
            }
        }

        private bool CanModifyTreeView(DirectoryItemViewModel? item) => !IsTreeViewLocked;

        [RelayCommand(CanExecute = nameof(CanModifyTreeView))]
        private void RequestRenameTreeFolder(DirectoryItemViewModel? item)
        {
            if (item == null) return;
            if (_treeFolderHandler.CheckLockAndWarn()) return;

            string path = PathHelper.GetPhysicalPath(item.FullPath);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ツリー名前変更: {item.FullPath}");
                return;
            }

            string? parentDir = Path.GetDirectoryName(path);
            string? inputText = RenameDialog.ShowDialog("名前の変更", item.Name, parentDir ?? string.Empty, selectNameWithoutExtension: false);
            if (inputText == null) return;

            string newName = inputText.Trim();
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
            if (string.IsNullOrEmpty(parentDir))
            {
                App.Notification.Notify("ルートは名前変更できません", "ツリー名前変更: ルート");
                return;
            }

            _treeFolderHandler.ExecuteRename(path, newName, parentDir);
        }

        [RelayCommand(CanExecute = nameof(CanModifyTreeView))]
        private void RequestDeleteTreeFolder(DirectoryItemViewModel? item)
        {
            if (item == null) return;
            if (_treeFolderHandler.CheckLockAndWarn()) return;

            string path = PathHelper.GetPhysicalPath(item.FullPath);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                App.Notification.Notify("フォルダが見つかりません", $"ツリー削除: {item.FullPath}");
                return;
            }

            string? parentPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parentPath))
            {
                App.Notification.Notify("ルートは削除できません", "ツリー削除: ルート");
                return;
            }

            _treeFolderHandler.ExecuteDelete(path, parentPath);
        }

        [RelayCommand]
        private void OpenTreeItemInAPane(DirectoryItemViewModel item)
        {
            if (item != null)
            {
                LeftPane.NavigateCommand.Execute(item.FullPath);
                App.Notification.Notify("Aペインにフォルダを表示しました", $"Aペインに表示: {item.FullPath}");
            }
        }

        [RelayCommand]
        private void OpenTreeItemInBPane(DirectoryItemViewModel item)
        {
            if (item != null)
            {
                RightPane.NavigateCommand.Execute(item.FullPath);
                App.Notification.Notify("Bペインにフォルダを表示しました", $"Bペインに表示: {item.FullPath}");
            }
        }

        /// <summary>ツリー項目をフォーカス中のペインに表示する。ダブルクリック・Enter で呼ばれる。</summary>
        [RelayCommand]
        private void OpenTreeItemInActivePane(DirectoryItemViewModel? item)
        {
            if (item != null && !string.IsNullOrEmpty(item.FullPath) && ActivePane != null)
            {
                ActivePane.NavigateCommand.Execute(item.FullPath);
            }
        }

        /// <summary>ツリー項目を反対側ペインに表示する。Ctrl+ダブルクリックで呼ばれる。1ペイン時は2ペインに切替。</summary>
        [RelayCommand]
        private void OpenTreeItemInOppositePane(DirectoryItemViewModel? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FullPath)) return;
            if (ActivePane == null) return;

            if (PaneCount == 1)
            {
                PaneCount = 2;
                App.Notification.Notify("2ペイン表示に切り替えました", "反対ペインに表示");
            }

            var targetPane = ActivePane == LeftPane ? RightPane : LeftPane;
            targetPane.NavigateCommand.Execute(item.FullPath);
            ActivePane = targetPane;
        }

        /// <summary>ツリー項目をフォーカス中のペインにアイコンビューで表示する。Shift+クリックで呼ばれる。</summary>
        [RelayCommand]
        private void OpenTreeItemInActivePaneWithIconView(DirectoryItemViewModel? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FullPath) || ActivePane == null) return;
            ActivePane.NavigateCommand.Execute(item.FullPath);
            ActivePane.SelectedTab?.ChangeFileViewModeCommand.Execute(FileViewMode.LargeIcon);
        }

        /// <summary>ツリー項目を反対側ペインにアイコンビューで表示する。Shift+Ctrl+クリックで呼ばれる。1ペイン時は2ペインに切替。</summary>
        [RelayCommand]
        private void OpenTreeItemInOppositePaneWithIconView(DirectoryItemViewModel? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FullPath) || ActivePane == null) return;

            if (PaneCount == 1)
            {
                PaneCount = 2;
                App.Notification.Notify("2ペイン表示に切り替えました", "反対ペインにアイコンビューで表示");
            }

            var targetPane = ActivePane == LeftPane ? RightPane : LeftPane;
            targetPane.NavigateCommand.Execute(item.FullPath);
            targetPane.SelectedTab?.ChangeFileViewModeCommand.Execute(FileViewMode.LargeIcon);
            ActivePane = targetPane;
        }

        [RelayCommand]
        private async Task ClearHistoryAsync()
        {
            var result = ZenithDialog.Show(
                "参照履歴をすべて削除しますか？",
                "履歴のクリア",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Question);

            if (result == ZenithDialogResult.Yes)
            {
                await App.Database.ClearHistoryAsync();
                await RefreshHistoryAsync();
                App.Notification.Notify("参照履歴をクリアしました", "参照履歴をすべて削除しました");
            }
        }

        [RelayCommand]
        private void SnapLeft()
        {
            WindowHelper.SnapLeft(Application.Current.MainWindow);
            App.Notification.Notify("ウィンドウを左に配置しました");
        }

        [RelayCommand]
        private void MaximizeWindow()
        {
            WindowHelper.MaximizeWindow(Application.Current.MainWindow);
            App.Notification.Notify("ウィンドウを最大化しました");
        }

        [RelayCommand]
        private void SnapRight()
        {
            WindowHelper.SnapRight(Application.Current.MainWindow);
            App.Notification.Notify("ウィンドウを右に配置しました");
        }

        [RelayCommand]
        private void SnapBottom()
        {
            WindowHelper.SnapBottom(Application.Current.MainWindow);
            App.Notification.Notify("ウィンドウを下半分に配置しました");
        }

        [RelayCommand]
        private void SnapBottomLeft()
        {
            WindowHelper.SnapBottomLeft(Application.Current.MainWindow);
            App.Notification.Notify("ウィンドウを左下に配置しました");
        }

        [RelayCommand]
        private void SnapBottomRight()
        {
            WindowHelper.SnapBottomRight(Application.Current.MainWindow);
            App.Notification.Notify("ウィンドウを右下に配置しました");
        }

        private bool FilterHistoryItem(object item)
        {
            if (string.IsNullOrWhiteSpace(HistorySearchText)) return true;

            if (item is HistoryItemViewModel vm)
            {
                var search = HistorySearchText.Trim();
                
                // 完全一致・部分一致（最優先）
                if (vm.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    vm.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Fuse.js 風の Fuzzy Search (Levenshtein Distance)
                // 文字数に応じて許容する距離を変える
                // 3文字以下: タイポ許容なし（部分一致のみ）
                // 4-7文字: 距離1まで
                // 8文字以上: 距離2まで
                int threshold = search.Length <= 3 ? 0 : search.Length <= 7 ? 1 : 2;
                
                if (threshold > 0)
                {
                    // 名前またはパスに対して距離計算（パスは長すぎるので名前優先で判定）
                    // パス全体との距離計算は重いので、パスの場合は「検索語が含まれるか」を重視し、距離計算は名前メインにする
                    int distName = StringHelper.ComputeLevenshteinDistance(search.ToLower(), vm.Name.ToLower());
                    if (distName <= threshold) return true;
                }
            }
            return false;
        }
    }
}
