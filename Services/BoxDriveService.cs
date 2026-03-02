using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ZenithFiler.Services;

namespace ZenithFiler
{
    /// <summary>
    /// Box Drive 連携サービス。
    /// 責務: (1) Box パス生成は PathHelper に委譲。
    /// (2) Shift+右クリックを契機とした一時的クリップボード監視（20秒）で「box.com」URL を検知し、Boxパス＋URL の2行形式に整形する。
    /// (3) 「共有リンクをコピー」時は親フォルダURL + 各アイテム個別URLを順次取得し、HTML＋PlainText のマルチフォーマットで出力する。
    /// </summary>
    public static class BoxDriveService
    {
        private const int SharedLinkMonitorTimeoutSeconds = 20;
        private const int PerItemTimeoutSeconds = 15;
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private static HwndSource? _listenerSource;
        private static System.Windows.Threading.DispatcherTimer? _timeoutTimer;
        private static string? _pendingBoxPath;
        private static TabItemViewModel? _pendingVm;
        private static HwndSourceHook? _clipboardHook;
        private static bool _richMode;
        private static string[]? _pendingFileNames;
        private static readonly object _monitorLock = new();

        // --- マルチアイテム URL 取得用の状態 ---
        private enum MonitorPhase { Simple, ParentUrl, ItemUrl }
        private static MonitorPhase _phase;
        private static string[]? _itemFullPaths;        // 各アイテムのフルパス
        private static int _currentItemIndex;            // 現在処理中のアイテムインデックス
        private static string? _parentUrl;               // 取得した親フォルダ URL
        private static List<string?>? _itemUrls;         // 各アイテムの個別 URL
        private static Point _savedScreenPoint;          // シェルコマンド実行用
        private static string? _parentFolderPath;        // 親フォルダのフルパス

        // --- GlowBar 進捗表示用の状態 ---
        private static MainViewModel? MainVM => Application.Current?.MainWindow?.DataContext as MainViewModel;
        private static IDisposable? _busyToken;
        private static DispatcherTimer? _progressTimer;
        private static double _progressTarget;
        private static string _glowStatusText = "";
        private static Stopwatch? _glowStopwatch;
        private static int _totalItemCount;

        /// <summary>
        /// Shift+右クリック後に、クリップボードへ box.com を含む共有リンクが書き込まれるまで
        /// 最大20秒間 WM_CLIPBOARDUPDATE で監視し、検知したら「Boxパス＋URL」の2行形式に整形してクリップボードを上書きする。
        /// 整形済みの2行が再度書き込まれた場合は無限ループを避けてスルーする。タイムアウト時は静かに終了する。
        /// </summary>
        public static void StartSharedLinkClipboardMonitor(string targetFilePath, TabItemViewModel? vm = null)
        {
            if (string.IsNullOrEmpty(targetFilePath)) return;
            if (!PathHelper.TryGetBoxSharePath(targetFilePath, out var boxPath) || string.IsNullOrEmpty(boxPath))
                return;

            lock (_monitorLock)
            {
                StopMonitoring();
                _pendingBoxPath = boxPath;
                _pendingVm = vm;
                _phase = MonitorPhase.Simple;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            _ = dispatcher.InvokeAsync(() =>
            {
                lock (_monitorLock)
                {
                    if (_pendingBoxPath == null) return;
                    StartClipboardListener(dispatcher);
                }
            });
        }

        /// <summary>
        /// Zenith コンテキストメニューの「共有リンクをコピー」クリック時に呼ばれる。
        /// 親フォルダの共有 URL を取得後、選択されている各アイテムの個別共有 URL を順次取得し、
        /// HTML（ハイパーリンク付き）＋ PlainText のマルチフォーマットでクリップボードを上書きする。
        /// </summary>
        /// <param name="targetPath">右クリック対象のフルパス（最初の選択アイテム）</param>
        /// <param name="names">選択アイテム名の配列</param>
        /// <param name="itemPaths">選択アイテムのフルパス配列（個別 URL 取得用）</param>
        /// <param name="cloudItem">親フォルダの「リンクをコピー」メニュー項目</param>
        /// <param name="screenPoint">シェルコマンド実行位置</param>
        /// <param name="vm">グリーンフラッシュ演出用の ViewModel</param>
        public static void StartRichShareLinkMonitor(
            string targetPath,
            string[] names,
            string[] itemPaths,
            CloudMenuItem cloudItem,
            Point screenPoint,
            TabItemViewModel? vm)
        {
            if (string.IsNullOrEmpty(targetPath)) return;

            // 親フォルダパスを算出
            string? parentFolder = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(parentFolder))
            {
                CloudShellMenuService.InvokeCloudMenuCommand(cloudItem, screenPoint);
                return;
            }

            if (!PathHelper.TryGetBoxSharePath(parentFolder, out var boxPath) || string.IsNullOrEmpty(boxPath))
            {
                CloudShellMenuService.InvokeCloudMenuCommand(cloudItem, screenPoint);
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(() => StartRichShareLinkMonitor(targetPath, names, itemPaths, cloudItem, screenPoint, vm));
                return;
            }

            lock (_monitorLock)
            {
                StopMonitoring();
                _pendingBoxPath = boxPath;
                _pendingVm = vm;
                _richMode = true;
                _pendingFileNames = names;
                _phase = MonitorPhase.ParentUrl;
                _itemFullPaths = itemPaths;
                _currentItemIndex = 0;
                _parentUrl = null;
                _itemUrls = new List<string?>();
                _savedScreenPoint = screenPoint;
                _parentFolderPath = parentFolder;

                StartClipboardListener(dispatcher);
            }

            // GlowBar 開始（アイテム数 + 1 は親フォルダ URL 取得分）
            StartGlowBar("[Box共有] フォルダURLを取得中...", itemPaths.Length);

            // リスナー設定完了後に親フォルダの「リンクをコピー」シェルコマンドを実行
            InvokeCopyLinkForPath(parentFolder, isBackground: true, screenPoint);
        }

        /// <summary>旧シグネチャとの後方互換。itemPaths なしの場合は名前のみのフォールバック。</summary>
        public static void StartRichShareLinkMonitor(
            string targetPath,
            string[] names,
            CloudMenuItem cloudItem,
            Point screenPoint,
            TabItemViewModel? vm)
        {
            // itemPaths が無い場合（背景右クリック等）は旧動作
            if (string.IsNullOrEmpty(targetPath)) return;
            if (!PathHelper.TryGetBoxSharePath(targetPath, out var boxPath) || string.IsNullOrEmpty(boxPath))
            {
                CloudShellMenuService.InvokeCloudMenuCommand(cloudItem, screenPoint);
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(() => StartRichShareLinkMonitor(targetPath, names, cloudItem, screenPoint, vm));
                return;
            }

            lock (_monitorLock)
            {
                StopMonitoring();
                _pendingBoxPath = boxPath;
                _pendingVm = vm;
                _richMode = true;
                _pendingFileNames = names;
                _phase = MonitorPhase.Simple;

                StartClipboardListener(dispatcher);
            }

            StartGlowBar("[Box共有] 共有URLを取得中...", 0);
            CloudShellMenuService.InvokeCloudMenuCommand(cloudItem, screenPoint);
        }

        /// <summary>クリップボードリスナーを開始する共通メソッド。</summary>
        private static void StartClipboardListener(System.Windows.Threading.Dispatcher dispatcher)
        {
            try
            {
                _listenerSource = new HwndSource(new HwndSourceParameters("BoxLinkMonitor")
                {
                    Width = 0, Height = 0, WindowStyle = 0
                });
                if (_listenerSource.Handle == IntPtr.Zero)
                {
                    StopMonitoring();
                    return;
                }

                _clipboardHook = ClipboardWndProc;
                _listenerSource.AddHook(_clipboardHook);

                if (!ClipboardListenerNative.AddClipboardFormatListener(_listenerSource.Handle))
                {
                    StopMonitoring();
                    return;
                }

                _timeoutTimer = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Normal, dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(SharedLinkMonitorTimeoutSeconds)
                };
                _timeoutTimer.Tick += OnTimeout;
                _timeoutTimer.Start();
            }
            catch
            {
                StopMonitoring();
            }
        }

        private static void OnTimeout(object? sender, EventArgs e)
        {
            // タイムアウト時: 途中まで取得できている場合はそこまでの結果で出力
            MonitorPhase phase;
            lock (_monitorLock)
            {
                phase = _phase;
            }

            if (phase == MonitorPhase.ItemUrl)
            {
                // 一部のアイテム URL が未取得だが、親 URL は取得済み → 現状で出力
                FinalizeAndWriteClipboard();
            }
            else
            {
                StopMonitoring();
            }
        }

        private static IntPtr ClipboardWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                ProcessClipboardChanged();
            }
            return IntPtr.Zero;
        }

        private static void ProcessClipboardChanged()
        {
            string? boxPath;
            TabItemViewModel? vm;
            bool richMode;
            string[]? fileNames;
            MonitorPhase phase;
            lock (_monitorLock)
            {
                boxPath = _pendingBoxPath;
                vm = _pendingVm;
                richMode = _richMode;
                fileNames = _pendingFileNames;
                phase = _phase;
            }
            if (string.IsNullOrEmpty(boxPath)) return;

            try
            {
                if (!Clipboard.ContainsText()) return;
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                var trimmed = text.Trim();
                if (IsAlreadyFormatted(trimmed, boxPath)) return;
                if (!trimmed.Contains("box.com", StringComparison.OrdinalIgnoreCase)) return;

                switch (phase)
                {
                    case MonitorPhase.Simple:
                        ProcessSimpleMode(boxPath, trimmed, richMode, fileNames, vm);
                        break;

                    case MonitorPhase.ParentUrl:
                        ProcessParentUrlCaptured(trimmed);
                        break;

                    case MonitorPhase.ItemUrl:
                        ProcessItemUrlCaptured(trimmed);
                        break;
                }
            }
            catch
            {
                /* クリップボード競合等は無視 */
            }
        }

        /// <summary>旧モード（Simple）: 親 URL + ファイル名一覧をテキストで出力。</summary>
        private static void ProcessSimpleMode(string boxPath, string url, bool richMode, string[]? fileNames, TabItemViewModel? vm)
        {
            string formatted;
            string notification;
            if (richMode && fileNames != null && fileNames.Length > 0)
            {
                var namesBlock = string.Join("\n", fileNames);
                formatted = $"{boxPath}\n{url}\n{namesBlock}";
                notification = "Box共有情報をコピーしました";
            }
            else
            {
                formatted = $"{boxPath}\n{url}";
                notification = "BoxパスとURLを結合しました";
            }

            Clipboard.SetText(formatted);
            App.Notification.Notify(notification, null);
            _ = vm?.TriggerSuccessFlashAsync();
            StopMonitoring();
        }

        /// <summary>Phase 1 完了: 親フォルダ URL を記録し、個別アイテムの URL 取得を開始する。</summary>
        private static void ProcessParentUrlCaptured(string url)
        {
            lock (_monitorLock)
            {
                _parentUrl = url;
                _phase = MonitorPhase.ItemUrl;
                _currentItemIndex = 0;

                // タイムアウトをリセット
                ResetTimeout(PerItemTimeoutSeconds);
            }

            int total = _totalItemCount;
            UpdateGlowProgress(9, $"[Box共有] 0 / {total} 件のリンクを取得中...");

            // 最初のアイテムの URL 取得を開始
            InvokeNextItemCopyLink();
        }

        /// <summary>Phase 2+: アイテム URL を記録し、次のアイテムへ進むか完了する。</summary>
        private static void ProcessItemUrlCaptured(string url)
        {
            int currentIndex;
            bool hasMore;
            lock (_monitorLock)
            {
                _itemUrls?.Add(url);
                _currentItemIndex++;
                currentIndex = _currentItemIndex;
                hasMore = _itemFullPaths != null && _currentItemIndex < _itemFullPaths.Length;
            }

            // GlowBar 進捗: 10→90% の範囲をアイテム数で按分
            int total = _totalItemCount;
            if (total > 0)
            {
                int pct = (int)(10 + currentIndex * 80.0 / total);
                UpdateGlowProgress(Math.Min(pct, 90), $"[Box共有] {currentIndex} / {total} 件のリンクを取得中...");
            }

            if (hasMore)
            {
                // タイムアウトをリセットして次のアイテムへ
                lock (_monitorLock)
                {
                    ResetTimeout(PerItemTimeoutSeconds);
                }
                InvokeNextItemCopyLink();
            }
            else
            {
                // 全アイテム完了 → クリップボードにマルチフォーマット出力
                FinalizeAndWriteClipboard();
            }
        }

        /// <summary>現在のアイテムインデックスに対応するアイテムの「リンクをコピー」を実行。</summary>
        private static void InvokeNextItemCopyLink()
        {
            string? itemPath;
            Point screenPoint;
            lock (_monitorLock)
            {
                if (_itemFullPaths == null || _currentItemIndex >= _itemFullPaths.Length) return;
                itemPath = _itemFullPaths[_currentItemIndex];
                screenPoint = _savedScreenPoint;
            }

            InvokeCopyLinkForPath(itemPath, isBackground: false, screenPoint);
        }

        /// <summary>
        /// 指定パスに対して Box シェル拡張の「リンクをコピー」を自動検出・実行する。
        /// CloudShellMenuService で IContextMenu を再構築し、「リンクをコピー」/「Copy link」を検索して InvokeCommand する。
        /// </summary>
        private static async void InvokeCopyLinkForPath(string filePath, bool isBackground, Point screenPoint)
        {
            try
            {
                var paths = new[] { filePath };
                var menuItems = await CloudShellMenuService.ExtractCloudMenuItemsAsync(paths, isBackground, timeoutMs: 3000);
                var copyLinkItem = FindCopyLinkCommand(menuItems);
                if (copyLinkItem != null)
                {
                    CloudShellMenuService.InvokeCloudMenuCommand(copyLinkItem, screenPoint);
                }
                else
                {
                    // 「リンクをコピー」が見つからない → このアイテムは URL なしとして記録
                    _ = App.FileLogger.LogAsync($"[BoxDriveService] Copy link menu not found for: {filePath}");
                    HandleMissingUrl();
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[BoxDriveService] InvokeCopyLinkForPath failed: {ex.Message}");
                HandleMissingUrl();
            }
        }

        /// <summary>URL 取得に失敗したアイテムを null としてスキップし、次へ進む。</summary>
        private static void HandleMissingUrl()
        {
            MonitorPhase phase;
            lock (_monitorLock)
            {
                phase = _phase;
            }

            if (phase == MonitorPhase.ParentUrl)
            {
                // 親フォルダ URL すら取得できない → アイテム個別取得に進む
                lock (_monitorLock)
                {
                    _parentUrl = null;
                    _phase = MonitorPhase.ItemUrl;
                    _currentItemIndex = 0;
                }

                int total = _totalItemCount;
                UpdateGlowProgress(9, $"[Box共有] 0 / {total} 件のリンクを取得中...");
                InvokeNextItemCopyLink();
            }
            else if (phase == MonitorPhase.ItemUrl)
            {
                int currentIndex;
                bool hasMore;
                lock (_monitorLock)
                {
                    _itemUrls?.Add(null);
                    _currentItemIndex++;
                    currentIndex = _currentItemIndex;
                    hasMore = _itemFullPaths != null && _currentItemIndex < _itemFullPaths.Length;
                }

                // スキップしたアイテムも進捗に反映
                int total = _totalItemCount;
                if (total > 0)
                {
                    int pct = (int)(10 + currentIndex * 80.0 / total);
                    UpdateGlowProgress(Math.Min(pct, 90), $"[Box共有] {currentIndex} / {total} 件のリンクを取得中...");
                }

                if (hasMore)
                {
                    lock (_monitorLock)
                    {
                        ResetTimeout(PerItemTimeoutSeconds);
                    }
                    InvokeNextItemCopyLink();
                }
                else
                {
                    FinalizeAndWriteClipboard();
                }
            }
        }

        /// <summary>収集した全 URL を HTML + PlainText でクリップボードに書き込む。</summary>
        private static void FinalizeAndWriteClipboard()
        {
            string? boxPath;
            string? parentUrl;
            string[]? fileNames;
            List<string?>? itemUrls;
            TabItemViewModel? vm;
            lock (_monitorLock)
            {
                boxPath = _pendingBoxPath;
                parentUrl = _parentUrl;
                fileNames = _pendingFileNames;
                itemUrls = _itemUrls != null ? new List<string?>(_itemUrls) : null;
                vm = _pendingVm;
            }

            if (string.IsNullOrEmpty(boxPath)) { StopMonitoring(); return; }

            UpdateGlowProgress(95, "[Box共有] クリップボードに書き込み中...");

            try
            {
                var plainText = BuildPlainText(boxPath, parentUrl, fileNames, itemUrls);
                var html = BuildHtml(boxPath, parentUrl, fileNames, itemUrls);
                var cfHtml = WrapAsCfHtml(html);

                var dataObject = new DataObject();
                dataObject.SetData(DataFormats.UnicodeText, plainText);
                dataObject.SetData(DataFormats.Text, plainText);
                dataObject.SetData(DataFormats.Html, cfHtml);
                Clipboard.SetDataObject(dataObject, true);

                App.Notification.Notify("Box共有情報をコピーしました", null);
                _ = vm?.TriggerSuccessFlashAsync();
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[BoxDriveService] FinalizeAndWriteClipboard failed: {ex.Message}");
            }
            finally
            {
                StopMonitoring();
            }
        }

        // ---- フォーマット生成 ----

        /// <summary>
        /// PlainText 形式を生成する。
        /// [親パス]
        /// [親URL]
        /// [アイテム名1] ( [個別URL1] )
        /// </summary>
        private static string BuildPlainText(string boxPath, string? parentUrl, string[]? fileNames, List<string?>? itemUrls)
        {
            var sb = new StringBuilder();
            sb.AppendLine(boxPath);
            if (!string.IsNullOrEmpty(parentUrl))
                sb.AppendLine(parentUrl);

            if (fileNames != null)
            {
                for (int i = 0; i < fileNames.Length; i++)
                {
                    string name = fileNames[i];
                    string? url = (itemUrls != null && i < itemUrls.Count) ? itemUrls[i] : null;
                    if (!string.IsNullOrEmpty(url))
                        sb.AppendLine($"{name} ( {url} )");
                    else
                        sb.AppendLine(name);
                }
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        /// <summary>
        /// HTML フラグメントを生成する。親パス + 親 URL リンク + 各アイテム名をハイパーリンクとして構築。
        /// </summary>
        private static string BuildHtml(string boxPath, string? parentUrl, string[]? fileNames, List<string?>? itemUrls)
        {
            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:sans-serif;font-size:13px;\">");

            // 親パス
            sb.Append($"<div>{HtmlEncode(boxPath)}</div>");

            // 親 URL
            if (!string.IsNullOrEmpty(parentUrl))
            {
                sb.Append($"<div><a href=\"{HtmlAttrEncode(parentUrl)}\">{HtmlEncode(parentUrl)}</a></div>");
            }

            // 各アイテム
            if (fileNames != null)
            {
                for (int i = 0; i < fileNames.Length; i++)
                {
                    string name = fileNames[i];
                    string? url = (itemUrls != null && i < itemUrls.Count) ? itemUrls[i] : null;
                    if (!string.IsNullOrEmpty(url))
                    {
                        sb.Append($"<div><a href=\"{HtmlAttrEncode(url)}\">{HtmlEncode(name)}</a></div>");
                    }
                    else
                    {
                        sb.Append($"<div>{HtmlEncode(name)}</div>");
                    }
                }
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        /// <summary>
        /// HTML フラグメントを CF_HTML (DataFormats.Html) 形式のヘッダー付き文字列にラップする。
        /// </summary>
        private static string WrapAsCfHtml(string htmlFragment)
        {
            // CF_HTML format: UTF-8 byte offsets in the header
            const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            const string htmlStart = "<html><body>\r\n<!--StartFragment-->";
            const string htmlEnd = "<!--EndFragment-->\r\n</body></html>";

            // ヘッダーのプレースホルダーのバイト長を計算
            string sampleHeader = string.Format(header, 0, 0, 0, 0);
            int headerBytes = Encoding.UTF8.GetByteCount(sampleHeader);
            int htmlStartBytes = Encoding.UTF8.GetByteCount(htmlStart);
            int fragmentBytes = Encoding.UTF8.GetByteCount(htmlFragment);
            int htmlEndBytes = Encoding.UTF8.GetByteCount(htmlEnd);

            int startHtml = headerBytes;
            int startFragment = headerBytes + htmlStartBytes;
            int endFragment = startFragment + fragmentBytes;
            int endHtml = endFragment + htmlEndBytes;

            string finalHeader = string.Format(header, startHtml, endHtml, startFragment, endFragment);
            return finalHeader + htmlStart + htmlFragment + htmlEnd;
        }

        private static string HtmlEncode(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string HtmlAttrEncode(string url)
        {
            return url.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // ---- ユーティリティ ----

        /// <summary>メニュー階層を再帰的に探索し、「リンクをコピー」/「Copy link」コマンドを返す。</summary>
        private static CloudMenuItem? FindCopyLinkCommand(List<CloudMenuItem> items)
        {
            foreach (var item in items)
            {
                if (item.Text.Contains("リンクをコピー", StringComparison.OrdinalIgnoreCase) ||
                    item.Text.Contains("Copy link", StringComparison.OrdinalIgnoreCase))
                    return item;

                if (item.HasChildren)
                {
                    var found = FindCopyLinkCommand(item.Children!);
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>タイムアウトタイマーをリセットする。_monitorLock 内から呼ぶこと。</summary>
        private static void ResetTimeout(int seconds)
        {
            if (_timeoutTimer != null)
            {
                _timeoutTimer.Stop();
                _timeoutTimer.Interval = TimeSpan.FromSeconds(seconds);
                _timeoutTimer.Start();
            }
        }

        // ---- GlowBar ----

        /// <summary>GlowBar + DispatcherTimer 追従を開始する。UI スレッドから呼ぶこと。</summary>
        private static void StartGlowBar(string statusText, int totalItems)
        {
            _totalItemCount = totalItems;
            _glowStatusText = statusText;
            _glowStopwatch = Stopwatch.StartNew();

            _busyToken = MainVM?.BeginBusy();
            MainVM?.BeginFileOperation(statusText, FlowDirection.LeftToRight);
            if (MainVM != null) MainVM.FileOperationProgress = 2;

            _progressTarget = 2;
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();
        }

        private static void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (MainVM == null) return;
            double target = Volatile.Read(ref _progressTarget);
            double current = MainVM.FileOperationProgress;
            MainVM.FileOperationStatusText = _glowStatusText;
            if (Math.Abs(target - current) < 0.3) return;
            double step = (target - current) * 0.18;
            if (step > 0 && step < 0.5) step = 0.5;
            MainVM.FileOperationProgress = Math.Min(current + step, target);
        }

        /// <summary>GlowBar の進捗目標とステータスを更新する。</summary>
        private static void UpdateGlowProgress(double target, string statusText)
        {
            _glowStatusText = statusText;
            Volatile.Write(ref _progressTarget, target);
        }

        /// <summary>GlowBar を停止し、100% まで追従＋最低表示時間を保証してからフェードアウトする。</summary>
        private static async void StopGlowBar()
        {
            var timer = _progressTimer;
            _progressTimer = null;

            // 進捗目標を 100% に引き上げ、Timer が追いつく猶予を与える
            Volatile.Write(ref _progressTarget, 100);

            var sw = _glowStopwatch;
            _glowStopwatch = null;
            sw?.Stop();

            // Timer 追従アニメーション（350ms）と最低表示時間（800ms）の大きい方を待機
            int waitMs = 350;
            if (sw != null)
            {
                int minRemaining = (int)(800 - sw.Elapsed.TotalMilliseconds);
                if (minRemaining > waitMs) waitMs = minRemaining;
            }
            if (waitMs > 0)
            {
                try { await Task.Delay(waitMs); } catch { }
            }

            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= ProgressTimer_Tick;
            }

            // 最終値を確実にセットしてからフェードアウト
            if (MainVM != null) MainVM.FileOperationProgress = 100;
            MainVM?.EndFileOperation();

            var token = _busyToken;
            _busyToken = null;
            token?.Dispose();
        }

        /// <summary>監視を完全に停止し、リソースを解放する。</summary>
        private static void StopMonitoring()
        {
            // GlowBar が開始されている場合のみ停止（無関係な進行中操作の GlowBar を破壊しない）
            if (_progressTimer != null || _glowStopwatch != null || _busyToken != null)
                StopGlowBar();
            CleanupMonitoringState();
        }

        /// <summary>
        /// メニュー閉鎖時に呼ばれるサイレントキャンセル。
        /// GlowBar を即リセット（アニメーション・通知なし）し、監視リソースを解放する。
        /// IO 処理の CancellationToken（MainVM._fileOperationCts）には一切触れない。
        /// </summary>
        public static void CancelMonitoringSilently()
        {
            bool hasGlowBar = _progressTimer != null || _glowStopwatch != null || _busyToken != null;

            // GlowBar を即リセット（EndFileOperation を呼ばない → アニメーション・通知ゼロ）
            var timer = _progressTimer;
            _progressTimer = null;
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= ProgressTimer_Tick;
            }

            _glowStopwatch?.Stop();
            _glowStopwatch = null;

            // GlowBar が開始されていた場合のみ UI プロパティをリセット（無関係な進行中操作を破壊しない）
            if (hasGlowBar)
            {
                var mainVm = MainVM;
                if (mainVm != null)
                {
                    mainVm.FileOperationProgress = 0;
                    mainVm.FileOperationStatusText = "";
                    mainVm.IsFileOperationActive = false;
                }
            }

            var token = _busyToken;
            _busyToken = null;
            token?.Dispose();

            CleanupMonitoringState();
        }

        /// <summary>クリップボードリスナー・タイマー・状態フィールドをリセットする共通メソッド。</summary>
        private static void CleanupMonitoringState()
        {
            lock (_monitorLock)
            {
                var timeoutTmr = _timeoutTimer;
                _timeoutTimer = null;
                if (timeoutTmr != null)
                {
                    timeoutTmr.Stop();
                    timeoutTmr.Tick -= OnTimeout;
                }

                if (_listenerSource != null)
                {
                    try
                    {
                        ClipboardListenerNative.RemoveClipboardFormatListener(_listenerSource.Handle);
                    }
                    catch { /* 既に解除済み等 */ }
                    if (_clipboardHook != null)
                    {
                        _listenerSource.RemoveHook(_clipboardHook);
                        _clipboardHook = null;
                    }
                    _listenerSource.Dispose();
                    _listenerSource = null;
                }

                _pendingBoxPath = null;
                _pendingVm = null;
                _richMode = false;
                _pendingFileNames = null;
                _phase = MonitorPhase.Simple;
                _itemFullPaths = null;
                _currentItemIndex = 0;
                _parentUrl = null;
                _itemUrls = null;
                _parentFolderPath = null;
                _totalItemCount = 0;
            }
        }

        /// <summary>クリップボードが既に「1行目=Boxパス, 2行目=URL(, 3行目以降=ファイル名)」の整形済みかどうか。</summary>
        private static bool IsAlreadyFormatted(string text, string boxPath)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(boxPath)) return false;
            var parts = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            var line0 = parts[0].Trim();
            var line1 = parts[1].Trim();
            return string.Equals(line0, boxPath, StringComparison.OrdinalIgnoreCase)
                   && line1.Contains("box.com", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class ClipboardListenerNative
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
