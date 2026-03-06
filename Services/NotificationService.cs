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
        private string? _lastShownMessage;

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
        /// 表示中のメッセージは即座に上書きされ、タイマーがリセットされます。同一メッセージは無視します。
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
                bool wasVisible = IsStatusMessageVisible;
                string? lastMsg = _lastShownMessage;

                if (wasVisible && string.Equals(lastMsg, displayMessage, StringComparison.Ordinal))
                {
                    // 同一メッセージ: テキストはそのまま、タイマーだけリセット（早期消失を防止）
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    _ = HideStatusAfterDelayAsync(displayTimeMs, _cts.Token);
                    return;
                }

                // 即座に上書き（タイマーリセット）
                ShowMessageCore(displayMessage, displayTimeMs);
            }
        }

        /// <summary>_lock 保持下で呼び出すこと。</summary>
        private void ShowMessageCore(string message, int displayTimeMs)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _lastShownMessage = message;
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
                if (token.IsCancellationRequested) return;
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
            });
        }
    }
}
