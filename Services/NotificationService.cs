using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler
{
    /// <summary>
    /// アプリケーション全体の通知メッセージを管理するサービスです。
    /// </summary>
    public partial class NotificationService : ObservableObject
    {
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private string? _pendingMessage;
        private int _pendingDisplayTimeMs;

        /// <summary>ウェルカムアニメーション中は true。この間 Notify はログのみ記録し、ステータスバー表示を抑制する。</summary>
        public bool IsWelcomeActive { get; set; }

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isStatusMessageVisible = false;

        [ObservableProperty]
        private bool _isLogging = false;

        [ObservableProperty]
        private bool _isIndexing = false;

        /// <summary>インデックス作成中の進捗メッセージ（例: インデックス作成中: 1,234 件）。</summary>
        [ObservableProperty]
        private string _indexingStatusMessage = string.Empty;

        [ObservableProperty]
        private string _themeToastLabel = "Applied Theme";

        [ObservableProperty]
        private string _themeToastName = string.Empty;

        [ObservableProperty]
        private bool _isThemeToastVisible = false;

        /// <summary>起動時テーマ適用をトーストで通知する。label はヘッダー文字列、holdMs 後にフェードアウト。</summary>
        public void ShowThemeToast(string themeName, string label = "Applied Theme", int holdMs = 2500)
        {
            ThemeToastLabel = label;
            ThemeToastName = themeName;
            IsThemeToastVisible = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(holdMs);
                await Application.Current.Dispatcher.InvokeAsync(() => IsThemeToastVisible = false);
            });
        }

        /// <summary>
        /// ステータスバーにメッセージを表示します。ログには同じメッセージが記録されます。
        /// </summary>
        /// <param name="message">表示および記録するメッセージ</param>
        /// <param name="displayTimeMs">表示時間（ミリ秒）。-1 の場合は設定値を使用。</param>
        public void Notify(string message, int displayTimeMs = -1)
        {
            int ms = displayTimeMs < 0 ? WindowSettings.NotificationDurationMsValue : displayTimeMs;
            Notify(message, null, ms);
        }

        /// <summary>
        /// ステータスバーにメッセージを表示し、詳細なログを記録します。
        /// 表示中のメッセージがある場合は最新1件のみ保持し、フェードアウト完了後に表示します。
        /// </summary>
        /// <param name="displayMessage">ステータスバーに表示する簡潔なメッセージ</param>
        /// <param name="logMessage">ログファイルに記録する詳細なメッセージ（nullの場合はdisplayMessageを使用）</param>
        /// <param name="displayTimeMs">表示時間（ミリ秒）</param>
        public void Notify(string displayMessage, string? logMessage, int displayTimeMs = 3000)
        {
            string messageToLog = logMessage ?? displayMessage;

            if (IsWelcomeActive)
            {
                _ = Task.Run(async () => await App.FileLogger.LogAsync(messageToLog));
                return;
            }

            // ログ出力とペンアイコン（即時、キュー状態に関わらず）
            IsLogging = true;
            _ = Task.Run(async () =>
            {
                await App.FileLogger.LogAsync(messageToLog);
                await Task.Delay(500);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsLogging = false);
            });

            lock (_lock)
            {
                if (IsStatusMessageVisible)
                {
                    // 表示中 → 最新メッセージのみ保持（上書き）
                    _pendingMessage = displayMessage;
                    _pendingDisplayTimeMs = displayTimeMs;
                    return;
                }

                ShowMessageCore(displayMessage, displayTimeMs);
            }
        }

        /// <summary>_lock 保持下で呼び出すこと。</summary>
        private void ShowMessageCore(string message, int displayTimeMs)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            StatusMessage = message;
            IsStatusMessageVisible = true;

            _ = HideStatusAfterDelayAsync(displayTimeMs, token);
        }

        private async Task HideStatusAfterDelayAsync(int displayTimeMs, CancellationToken token)
        {
            try
            {
                await Task.Delay(displayTimeMs, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsStatusMessageVisible = false;
            });

            // XAML フェードアウト Duration="0:0:0.6" の完了を待機
            try
            {
                await Task.Delay(650, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;

                StatusMessage = string.Empty;

                // pending があれば次を表示
                lock (_lock)
                {
                    if (_pendingMessage != null)
                    {
                        string msg = _pendingMessage;
                        int time = _pendingDisplayTimeMs;
                        _pendingMessage = null;
                        _pendingDisplayTimeMs = 0;
                        ShowMessageCore(msg, time);
                    }
                }
            });
        }
    }
}
