using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZenithFiler.Services;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ZenithFiler.Helpers;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public partial class App : Application
    {
        // --- 多重起動防止 ---
        private const string SingleInstanceMutexName = "ZenithFiler_SingleInstance_Mutex";
        private static Mutex? _singleInstanceMutex;
        private static bool _mutexOwned;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        private static readonly Lazy<DatabaseService> _database = new(() => new DatabaseService());
        public static DatabaseService Database => _database.Value;

        private static readonly Lazy<FileLoggerService> _fileLogger = new(() => new FileLoggerService());
        public static FileLoggerService FileLogger => _fileLogger.Value;

        private static readonly Lazy<NotificationService> _notification = new(() => new NotificationService());
        public static NotificationService Notification => _notification.Value;

        private static readonly Lazy<IndexService> _indexService = new(() => new IndexService());
        public static IndexService IndexService => _indexService.Value;

        private static readonly Lazy<Services.ThemeService> _themeService = new(() => new Services.ThemeService());
        public static Services.ThemeService ThemeService => _themeService.Value;

        private static readonly Lazy<Services.LicenseService> _licenseService = new(() => new Services.LicenseService());
        public static Services.LicenseService License => _licenseService.Value;

        private static readonly Lazy<Services.KeyBindingService> _keyBindingService = new(() => new Services.KeyBindingService());
        public static Services.KeyBindingService KeyBindings => _keyBindingService.Value;

        public static Services.TrayIconService? TrayService { get; private set; }
        public static Services.CpuIdleService? CpuIdleService { get; private set; }
        public static Services.UpdateService? UpdateService { get; private set; }

        private static CancellationTokenSource? _gcCts;
        private const int GcDelaySeconds = 60;
        private const int LohCompactionDelaySeconds = 300;

        // --- Main() からの引き継ぎ（WPF 初期化前に開始した処理の結果） ---
        private SplashScreen? _firstLaunchSplash;
        private bool _isFirstLaunch;
        private Task<WindowSettings>? _settingsTask;

        /// <summary>バックグラウンドで実行中の起動時初期化タスク。Window_Loaded から await して完了を待機できる。</summary>
        internal static Task? StartupInitTask { get; private set; }

        /// <summary>起動時に適用したテーマ名（トースト表示用）。</summary>
        public static string? StartupAppliedThemeName { get; private set; }

        /// <summary>起動時に読み取った SavedThemeName（ThemeName へのフォールバック済み）。LoadThemeSettings 初期化用。</summary>
        public static string StartupSavedThemeName { get; private set; } = "standard";

        /// <summary>起動時に読み取ったペインテーマ名（Pane モード復元用）。</summary>
        public static (string Nav, string APane, string BPane) StartupPaneThemes { get; private set; }

        /// <summary>
        /// カスタムエントリーポイント。WPF フレームワーク初期化前にスプラッシュ表示と設定読み込みを開始する。
        /// App.xaml の BuildAction を Page に変更し、自動生成 Main を抑止して使用。
        /// </summary>
        [STAThread]
        public static void Main()
        {
            // マルチコア JIT: 起動プロファイルを記録し、2回目以降はバックグラウンドで先行 JIT
            System.Runtime.ProfileOptimization.SetProfileRoot(AppDomain.CurrentDomain.BaseDirectory);
            System.Runtime.ProfileOptimization.StartProfile("startup.jitprofile");

            // 最速でスプラッシュ表示（WPF フレームワーク初期化前、ネイティブ Win32 で描画）
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var settingsPath = Path.Combine(baseDir, "settings.json");
            bool isFirstLaunch = !File.Exists(settingsPath);

            SplashScreen? splash = null;
            if (isFirstLaunch)
            {
                splash = new SplashScreen("assets/splash.png");
                splash.Show(false);
            }

            // 設定の並列読み込みも WPF 初期化前に開始（App コンストラクタ + InitializeComponent と並列実行）
            var settingsTask = Task.Run(() =>
            {
                try { return WindowSettings.Load(); }
                catch { return WindowSettings.CreateDefault(); }
            });

            // WPF Application の初期化（App コンストラクタ → InitializeComponent → Run → OnStartup）
            var app = new App();
            app._firstLaunchSplash = splash;
            app._isFirstLaunch = isFirstLaunch;
            app._settingsTask = settingsTask;
            app.InitializeComponent();
            app.Run();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 0. 多重起動チェック（設定ファイル・DB 読み込みに先立ち最速で実行）
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                _mutexOwned = false;
                _firstLaunchSplash?.Close(TimeSpan.Zero);
                ActivateExistingInstance();
                Shutdown();
                return;
            }
            _mutexOwned = true;

            // スプラッシュと設定読み込みは Main() で開始済み（WPF 初期化と並列実行完了）

            // 基盤フォルダの準備はバックグラウンドへ（UI スレッドの I/O を排除）
            _ = Task.Run(EnsureAppFolders);
            _ = Services.SettingsBackupService.CleanupOldBackupsAsync(30);

            // 設定を await で取得（Main() で開始済み — ブロッキング排除）
            WindowSettings preloadedSettings = await _settingsTask!;

            // テーマ設定をメモリ上の設定オブジェクトから抽出（ファイル I/O なし）
            var ts = ExtractThemeStartupSettings(preloadedSettings);
            StartupSavedThemeName = ts.SavedTheme;
            StartupPaneThemes = (ts.NavPane, ts.APane, ts.BPane);
            string themeToApply = ts.SavedTheme;

            // テーマ関連のファイルI/Oをバックグラウンドで並列実行（UIスレッドのブロック削減）
            Task<string> autoThemeTask;
            if (ts.Mode == "Auto")
            {
                // Auto モード: テーマスキャン + ランダム選択 + JSON事前読み込み を一括で実行
                var tsCopy = ts;
                autoThemeTask = Task.Run(() =>
                {
                    var allThemes = ThemeService.ScanThemes();
                    var pool = (tsCopy.SubMode == "Category" && !string.IsNullOrEmpty(tsCopy.Category)
                        ? allThemes.Where(t => t.CategoryDisplayName == tsCopy.Category)
                        : allThemes).ToList();
                    var picked = PickRandomTheme(pool, tsCopy.LastApplied);
                    var name = picked?.Name ?? tsCopy.SavedTheme;
                    ThemeService.PreloadThemeJson(name);
                    return name;
                });
            }
            else
            {
                // 非Auto: テーマ JSON の事前読み込みをバックグラウンドで実行
                var nameToPreload = themeToApply;
                autoThemeTask = Task.Run(() =>
                {
                    ThemeService.PreloadThemeJson(nameToPreload);
                    return nameToPreload;
                });
            }

            // リソース辞書は UI スレッドで読み込む必要がある（XAML パース）
            // テーマ JSON のバックグラウンド読み込みと並列実行
            LoadHeavyResources();

            // バックグラウンド テーマ I/O の完了を await（ブロッキング排除）
            themeToApply = await autoThemeTask;
            if (themeToApply != ts.SavedTheme)
                _ = Task.Run(() => WindowSettings.SaveThemeOnly(themeToApply));

            ThemeService.LoadAndApply(Current.Resources, themeToApply);
            StartupAppliedThemeName = themeToApply;

            // マニュアルビューア用ログフック（共有プロジェクトから呼ばれる）
            DocViewerLogHelper.LogAsync = msg => _ = FileLogger.LogAsync(msg);

            // グローバル例外ハンドラの登録
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // 起動診断情報をバックグラウンドでログに記録
            _ = Task.Run(() => FileLogger.LogStartupDiagnostics());

            // アプリのアクティブ状態を監視し、非アクティブ時にメモリ解放を試みる
            AppActivationService.Instance.ActivationChanged += OnAppActivationChanged;

            Exit += App_Exit;

            if (_firstLaunchSplash != null)
            {
                _firstLaunchSplash.Close(TimeSpan.FromSeconds(0.5));
            }

            // 初回起動時: ウェルカムウィンドウで テーマ選択 → settings.json 作成
            if (_isFirstLaunch)
            {
                // WelcomeWindow が唯一のウィンドウなので、Close 時にアプリが終了しないよう一時変更
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                new WelcomeWindow().ShowDialog();
                ShutdownMode = ShutdownMode.OnLastWindowClose;
            }

            // EULA 同意チェック（バージョンごとに再表示）
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            if (preloadedSettings.EulaAcceptedVersion != currentVersion)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var eulaDialog = new Views.EulaDialog();
                if (eulaDialog.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
                WindowSettings.SaveEulaAcceptedOnly(currentVersion);
                // DebouncedSaver のフラッシュを待つ（即座に settings.json に書き込む）
                WindowSettings.FlushPendingSaves();
                ShutdownMode = ShutdownMode.OnLastWindowClose;
            }

            // 行高リソースを起動時設定から初期化（ListViewItem の MinHeight に DynamicResource で使用）
            Current.Resources["ListRowHeight"] = (double)preloadedSettings.ListRowHeight;
            // 一覧アニメーション時間リソースを初期化（ホバーフェード・スケール）
            Current.Resources["ListItemHoverDuration"] = new System.Windows.Duration(
                preloadedSettings.EnableListAnimations ? TimeSpan.FromSeconds(0.15) : TimeSpan.Zero);
            // カスタムキーバインドを起動時に適用
            KeyBindings.ApplyCustomBindings(preloadedSettings.CustomKeyBindings);

            var mainWindow = new MainWindow(preloadedSettings);

            // タスクトレイサービスの初期化
            TrayService = new Services.TrayIconService(mainWindow);
            TrayService.Initialize();

            // CPU アイドル監視サービスの初期化
            CpuIdleService = new Services.CpuIdleService();

            // 自動更新サービスの初期化
            UpdateService = new Services.UpdateService();
            UpdateService.Initialize();

            // --updated 引数処理（アップデート適用後の再起動時）
            if (Environment.GetCommandLineArgs().Contains("--updated"))
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                    Notification.Notify($"バージョン {ver} に更新しました"),
                    DispatcherPriority.Loaded);
                _ = Task.Run(() => UpdateService.CleanupTempFiles());
            }

            // レンダリング準備が整った段階で Visible に切り替え、完成済みの画面を一発表示
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                mainWindow.Visibility = Visibility.Visible;
                mainWindow.Activate();
            }), DispatcherPriority.Render);

            // 7. 重い初期化処理をバックグラウンドで実行（Window_Loaded 側で適宜 await）
            StartupInitTask = Task.Run(async () =>
            {
                PathHelper.EnsureSpecialFoldersCached();
                await Database.InitializeAsync();
                await License.InitializeAsync();
            });

            // ShellNew レジストリスキャン（コンテキストメニューの「新規作成」サブメニュー用）
            _ = ShellNewService.InitializeAsync();

            base.OnStartup(e);
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            // 1. UI 系サービス（トレイアイコン除去）
            TrayService?.Dispose();
            CpuIdleService?.Dispose();
            UpdateService?.Dispose();

            // 2. ShellThumbnailService（STA スレッド停止 + COM 後始末）
            if (ShellThumbnailService.IsCreated)
                ShellThumbnailService.Instance.Dispose();

            // 3. IndexService（Lucene ライター flush + CTS キャンセル）
            if (_indexService.IsValueCreated)
                _indexService.Value.Dispose();

            // 4. FileLogger を最後に（他サービスの Dispose 中のログを拾うため）
            if (_fileLogger.IsValueCreated)
                _fileLogger.Value.Dispose();

            // 5. Mutex 解放（所有者のみ）
            if (_mutexOwned && _singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }

        /// <summary>
        /// 既に起動している Zenith Filer のメインウィンドウを探し出し、最前面に表示する。
        /// 最小化されていた場合は復元してからフォアグラウンドへ移動する。
        /// </summary>
        private static void ActivateExistingInstance()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);
                foreach (var p in processes)
                {
                    if (p.Id == currentProcess.Id) continue;
                    var hwnd = p.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) continue;

                    // 最小化されている場合は元のサイズに復元
                    if (IsIconic(hwnd))
                        ShowWindow(hwnd, SW_RESTORE);

                    SetForegroundWindow(hwnd);
                    break;
                }
            }
            catch
            {
                // 既存ウィンドウの活性化に失敗しても、多重起動防止自体は有効
            }
        }

        /// <summary>index, backups, logs フォルダを同期で生成する。起動時に確実に存在させるため。</summary>
        private static void EnsureAppFolders()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (var dir in new[] { "index", "backups", "logs" })
                {
                    var path = Path.Combine(baseDir, dir);
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                _ = FileLogger.LogAsync($"[App] EnsureAppFolders failed: {ex.Message}");
            }
        }

        private record ThemeStartupSettings(
            string Mode, string SubMode, string? Category,
            string SavedTheme, string LastApplied,
            string NavPane, string APane, string BPane);

        /// <summary>読み込み済みの WindowSettings からテーマ関連フィールドを抽出する（ファイル I/O なし）。</summary>
        private static ThemeStartupSettings ExtractThemeStartupSettings(WindowSettings s)
        {
            string mode = s.CurrentThemeMode;
            string sub = s.AutoSelectSubMode;
            string? cat = s.SelectedCategory;
            string last = s.ThemeName;
            string saved = string.IsNullOrEmpty(s.SavedThemeName) ? last : s.SavedThemeName;
            string navPane = s.NavPaneThemeName;
            string aPane = s.APaneThemeName;
            string bPane = s.BPaneThemeName;
            return new(mode, sub, cat, saved, last, navPane, aPane, bPane);
        }

        /// <summary>プール内からランダムにテーマを選ぶ。前回と重複しないものを優先する。</summary>
        private static Models.ThemeInfo? PickRandomTheme(System.Collections.Generic.IList<Models.ThemeInfo> pool, string? lastThemeName)
        {
            if (pool.Count == 0) return null;
            var preferred = pool.Where(t => t.Name != lastThemeName).ToList();
            var candidates = preferred.Count > 0 ? preferred : (System.Collections.Generic.IList<Models.ThemeInfo>)pool;
            return candidates[Random.Shared.Next(candidates.Count)];
        }

        /// <summary>MainWindow 用の重いリソース（WPF-UI テーマ等）を遅延読み込みする。スプラッシュ表示中に呼ぶ。</summary>
        internal static void LoadHeavyResources()
        {
            var rd = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/ZenithFiler;component/AppResources.xaml", UriKind.Absolute)
            };
            Current.Resources.MergedDictionaries.Add(rd);
        }

        private void OnAppActivationChanged(object? sender, bool isActive)
        {
            if (isActive)
            {
                // アクティブ復帰時は保留中のGCをキャンセル（フォーカス復帰時のフリーズ防止）
                _gcCts?.Cancel();
                _gcCts?.Dispose();
                _gcCts = null;

                // リフレッシュが必要なタブをアクティブ優先・順次で実行（UI負荷分散）
                _ = RunStaggeredRefreshOnActivationAsync();
            }
            else
            {
                // 非アクティブになった一定時間後にメモリ解放（復帰直後のフリーズを避けるため60秒待機）
                _gcCts?.Cancel();
                _gcCts?.Dispose();
                _gcCts = new CancellationTokenSource();
                var token = _gcCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(GcDelaySeconds), token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    if (token.IsCancellationRequested || AppActivationService.Instance.IsActive)
                        return;

                    // OptimizedモードでGC（Forcedより負荷が低く、復帰時のフリーズを軽減）
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, true);
                    GC.WaitForPendingFinalizers();

                    // LOHコンパクションは5分以上非アクティブ時のみ（負荷が大きいため）
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(LohCompactionDelaySeconds - GcDelaySeconds), token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    if (token.IsCancellationRequested || AppActivationService.Instance.IsActive)
                        return;
                    if (token.IsCancellationRequested || AppActivationService.Instance.IsActive)
                        return;

                    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, true);
                }, token);
            }
        }

        private const int StaggeredRefreshDelayMs = 500;

        private async Task RunStaggeredRefreshOnActivationAsync()
        {
            var mainVm = Application.Current?.MainWindow?.DataContext as MainViewModel;
            if (mainVm == null) return;

            var toRefresh = new List<TabItemViewModel>();
            foreach (var tab in mainVm.LeftPane.Tabs.Concat(mainVm.RightPane.Tabs))
            {
                if (tab.TryConsumeRefreshOnActivation())
                    toRefresh.Add(tab);
            }
            if (toRefresh.Count == 0) return;

            // アクティブタブを先頭に
            var activeFirst = toRefresh.OrderByDescending(t => t.IsActive).ToList();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            for (var i = 0; i < activeFirst.Count; i++)
            {
                if (i > 0)
                    await Task.Delay(StaggeredRefreshDelayMs);

                var tab = activeFirst[i];
                await dispatcher.InvokeAsync(() =>
                {
                    try { tab.Refresh(); } catch { /* タブ破棄済み等 */ }
                });
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var ctx = $"Thread={System.Threading.Thread.CurrentThread.ManagedThreadId} IsBackground={System.Threading.Thread.CurrentThread.IsBackground}";
            var logMessage = $"【DispatcherUnhandledException】{ctx}{Environment.NewLine}{FileLoggerService.FormatException(e.Exception)}";
            FileLogger.LogSync(logMessage);
            MessageBox.Show($"予期せぬエラーが発生しました。\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", 
                            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ctx = $"IsTerminating={e.IsTerminating} Thread={System.Threading.Thread.CurrentThread.ManagedThreadId}";
            if (e.ExceptionObject is Exception ex)
            {
                var logMessage = $"【UnhandledException】{ctx}{Environment.NewLine}{FileLoggerService.FormatException(ex)}";
                FileLogger.LogSync(logMessage);
                MessageBox.Show($"致命的なエラーが発生しました。\n\n{ex.Message}\n\n{ex.StackTrace}", 
                                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                var logMessage = $"【UnhandledException】{ctx} ExceptionObject={e.ExceptionObject?.GetType().FullName ?? "null"} ToString={e.ExceptionObject}";
                FileLogger.LogSync(logMessage);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // バックグラウンドタスクの例外はログ出力にとどめ、ユーザーへの通知は抑制する（クラッシュ防止）
            var logMessage = $"【UnobservedTaskException】{FileLoggerService.FormatException(e.Exception)}";
            _ = FileLogger.LogAsync(logMessage);
            e.SetObserved();
        }

        /// <summary>
        /// コンテキストメニュー表示時に画面端にはみ出さないよう位置を補正する（OS標準スタイル）。
        /// マルチモニタ対応・影マージン考慮・ScrollViewer による動的 MaxHeight 設定を行う。
        /// </summary>
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;

            var popup = VisualTreeHelper.GetParent(contextMenu) as Popup;
            if (popup == null)
                return;

            contextMenu.ApplyTemplate();
            contextMenu.UpdateLayout();

            try
            {
                // --- マルチモニタ対応ワークエリア取得 ---
                var ownerWindow = Window.GetWindow(contextMenu.PlacementTarget)
                                  ?? Application.Current.MainWindow;
                Rect workArea = (ownerWindow != null)
                    ? WindowHelper.GetWorkArea(ownerWindow)
                    : SystemParameters.WorkArea;

                // --- 影マージン定数 ---
                const double shadowMargin = 28.0;
                const double menuPadding = 10.0;

                // --- ScrollViewer に動的 MaxHeight を設定 ---
                double maxMenuHeight = workArea.Height - shadowMargin - menuPadding;
                var scrollViewer = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(contextMenu);
                if (scrollViewer != null)
                {
                    scrollViewer.MaxHeight = Math.Max(100, maxMenuHeight);
                }
                contextMenu.UpdateLayout();

                // --- 位置補正（影マージン込みの実サイズで計算）---
                Point topLeft = contextMenu.PointToScreen(new Point(0, 0));
                double menuW = contextMenu.ActualWidth;
                double menuH = contextMenu.ActualHeight;

                double newLeft = Math.Max(workArea.Left,
                    Math.Min(topLeft.X, workArea.Right - menuW + shadowMargin));
                double newTop = Math.Max(workArea.Top,
                    Math.Min(topLeft.Y, workArea.Bottom - menuH + shadowMargin));

                popup.HorizontalOffset += newLeft - topLeft.X;
                popup.VerticalOffset += newTop - topLeft.Y;
            }
            catch
            {
                // レイアウト未確定などで失敗しても無視
            }
        }
    }
}
