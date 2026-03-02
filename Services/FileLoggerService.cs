using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace ZenithFiler
{
    public class FileLoggerService : IDisposable
    {
        private readonly string _logDirectory;
        private readonly ILogger _logger;

        public FileLoggerService()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Async(a => a.File(
                    path: Path.Combine(_logDirectory, "log-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "[{Timestamp:yyyy/MM/dd HH:mm:ss}][{Level:u3}] {Message:lj}{NewLine}{Exception}"
                ))
#if DEBUG
                .WriteTo.Debug(restrictedToMinimumLevel: LogEventLevel.Debug)
#endif
                .CreateLogger();
        }

        /// <summary>
        /// 例外の詳細をログ用文字列にフォーマットします。
        /// </summary>
        public static string FormatException(Exception ex)
        {
            if (ex == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine($"[EXCEPTION] {ex.GetType().FullName}: {ex.Message} HResult=0x{ex.HResult:X8}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
                sb.AppendLine($"StackTrace:{Environment.NewLine}{ex.StackTrace}");
            if (ex is AggregateException agg)
            {
                foreach (var ie in agg.InnerExceptions)
                {
                    sb.AppendLine("--- InnerException ---");
                    sb.AppendLine(FormatException(ie));
                }
            }
            else
            {
                for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                {
                    sb.AppendLine($"InnerException: {inner.GetType().FullName}: {inner.Message} HResult=0x{inner.HResult:X8}");
                    if (!string.IsNullOrEmpty(inner.StackTrace))
                        sb.AppendLine(inner.StackTrace);
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 起動時の診断情報（バージョン・OS・.NET ランタイム・PID）をログに記録します。
        /// </summary>
        public void LogStartupDiagnostics()
        {
            try
            {
                var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
                var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                var dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                var arch = Environment.Is64BitProcess ? "x64" : "x86";
                _logger.Information("{Message}",
                    $"[STARTUP] Zenith Filer v{ver} | {os} | {dotnet} | {arch} | PID={Environment.ProcessId}");
            }
            catch
            {
                // 起動診断の失敗はアプリに影響させない
            }
        }

        /// <summary>
        /// 致命的なエラー時に同期的にログを記録します（プロセス終了前に確実に書き込むため）。
        /// </summary>
        public void LogSync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try
            {
                var fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
                var filePath = Path.Combine(_logDirectory, fileName);
                var timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                var logContent = $"[{timestamp}] {message}{Environment.NewLine}";
                File.AppendAllText(filePath, logContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Log write failed (sync): {ex.Message}");
            }

            // 構造化ログとしても残す（他シンクへの出力も含める）
            _logger.Fatal("{Message}", message);
        }

        /// <summary>
        /// ログを記録します（非同期・高頻度向け）。
        /// </summary>
        /// <param name="message">記録するメッセージ</param>
        public Task LogAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return Task.CompletedTask;

            _logger.Information("{Message}", message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Serilog のロール設定により古いログは自動的に削除されるため、ダミー実装とする。
        /// </summary>
        public Task CleanupOldLogsAsync() => Task.CompletedTask;

        /// <summary>
        /// ログフォルダをエクスプローラーで開きます。
        /// </summary>
        public void OpenLogFolder()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = _logDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open log directory: {ex.Message}");
                _logger.Error(ex, "Failed to open log directory");
            }
        }

        public void Dispose()
        {
            ( _logger as IDisposable )?.Dispose();
        }
    }
}
