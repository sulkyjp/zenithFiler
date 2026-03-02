using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private static CancellationTokenSource? _gcCts;
        private const int GcDelaySeconds = 60;
        private const int LohCompactionDelaySeconds = 300;

        /// <summary>バックグラウンドで実行中の起動時初期化タスク。Window_Loaded から await して完了を待機できる。</summary>
        internal static Task? StartupInitTask { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 0. 多重起動チェック（設定ファイル・DB 読み込みに先立ち最速で実行）
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                _mutexOwned = false;
                ActivateExistingInstance();
                Shutdown();
                return;
            }
            _mutexOwned = true;

            // 1. 初回起動判定 (settings.json の存在確認)
            // ポータブル性を維持するため、AppDomain.CurrentDomain.BaseDirectory を使用
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var settingsPath = Path.Combine(baseDir, "settings.json");
            bool isFirstLaunch = !File.Exists(settingsPath);

            // 2. スプラッシュ表示 (初回起動時のみ)
            SplashScreen? splash = null;
            if (isFirstLaunch)
            {
                splash = new SplashScreen("assets/splash.png");
                // フェードインなし(false)で即座に表示し、準備中であることを伝える
                splash.Show(false);
            }

            // 3. 基盤フォルダの準備はバックグラウンドへ（UI スレッドの I/O を排除）
            _ = Task.Run(EnsureAppFolders);
            _ = Services.SettingsBackupService.CleanupOldBackupsAsync(30);
            // リソース辞書は UI スレッドで読み込む必要がある（XAML パース）
            LoadHeavyResources();
            // テーマカラーを外部 JSON から適用（ファイルがなければデフォルトのまま）
            var savedTheme = ReadThemeNameFromSettings(settingsPath);
            ThemeService.LoadAndApply(Current.Resources, savedTheme);

            // マニュアルビューア用ログフック（共有プロジェクトから呼ばれる）
            DocViewerLogHelper.LogAsync = msg => _ = FileLogger.LogAsync(msg);

            // 4. グローバル例外ハンドラの登録
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // 起動診断情報をログに記録
            FileLogger.LogStartupDiagnostics();

            // 5. アプリのアクティブ状態を監視し、非アクティブ時にメモリ解放を試みる
            AppActivationService.Instance.ActivationChanged += OnAppActivationChanged;

            Exit += App_Exit;

            if (splash != null)
            {
                // 初回起動時は 0.5秒 かけてフェードアウト
                splash.Close(TimeSpan.FromSeconds(0.5));
            }

            // 6a. 初回起動時: ウェルカムウィンドウで テーマ選択 → settings.json 作成
            if (isFirstLaunch)
            {
                // WelcomeWindow が唯一のウィンドウなので、Close 時にアプリが終了しないよう一時変更
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                new WelcomeWindow().ShowDialog();
                ShutdownMode = ShutdownMode.OnLastWindowClose;
            }

            // 6b. 設定を読み込み、メインウィンドウを生成（まだ見せない: XAML で Visibility="Hidden"）
            WindowSettings preloadedSettings;
            try
            {
                preloadedSettings = WindowSettings.Load();
            }
            catch
            {
                preloadedSettings = WindowSettings.CreateDefault();
            }
            var mainWindow = new MainWindow(preloadedSettings);

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
            });

            // ShellNew レジストリスキャン（コンテキストメニューの「新規作成」サブメニュー用）
            _ = ShellNewService.InitializeAsync();

            // 8. IndexService の初期化は起動 2 秒後に遅延実行（UIの応答性を優先）
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                IndexService.ConfigureIndexUpdate(null, () => new List<string>());
            });

            base.OnStartup(e);
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            if (_indexService.IsValueCreated)
                _indexService.Value.Dispose();

            if (_fileLogger.IsValueCreated)
                _fileLogger.Value.Dispose();

            // Mutex 解放（所有者のみ）
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

        /// <summary>settings.json から ThemeName だけを高速に取得する。フル Load を避けて起動を高速化。</summary>
        private static string ReadThemeNameFromSettings(string settingsPath)
        {
            try
            {
                if (!File.Exists(settingsPath)) return "standard";
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath),
                    new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
                if (doc.RootElement.TryGetProperty("ThemeName", out var prop))
                    return prop.GetString() ?? "standard";
            }
            catch { }
            return "standard";
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
