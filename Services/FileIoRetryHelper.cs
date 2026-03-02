using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace ZenithFiler.Services
{
    /// <summary>
    /// ファイル/ディレクトリ操作に対するリトライポリシーを集約するヘルパー。
    /// 一時的なロックや共有違反に対して、短時間の再試行を行う。
    /// </summary>
    public static class FileIoRetryHelper
    {
        private static readonly ResiliencePipeline _ioRetryPipeline =
            new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<IOException>()
                        .Handle<UnauthorizedAccessException>()
                })
                .Build();

        /// <summary>OneDrive 等クラウド同期環境向けの強化リトライパイプライン（5回・500ms 基底）。</summary>
        private static readonly ResiliencePipeline _cloudRetryPipeline =
            new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 5,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<IOException>()
                        .Handle<UnauthorizedAccessException>()
                })
                .Build();

        public static void MoveFile(string sourceFileName, string destFileName)
        {
            _ioRetryPipeline.Execute(() => File.Move(sourceFileName, destFileName));
        }

        public static void MoveDirectory(string sourceDirName, string destDirName)
        {
            _ioRetryPipeline.Execute(() => Directory.Move(sourceDirName, destDirName));
        }

        public static void CopyFile(string sourceFileName, string destFileName, bool overwrite)
        {
            _ioRetryPipeline.Execute(() => File.Copy(sourceFileName, destFileName, overwrite));
        }

        /// <summary>
        /// リトライ付きでディレクトリを作成します。
        /// OneDrive 等の同期ソフトによる一時ロックを想定し、指数バックオフで再試行します。
        /// </summary>
        /// <param name="path">作成するディレクトリのフルパス</param>
        /// <param name="isCloudSynced">クラウド同期パスの場合 true（リトライ回数・間隔を増加）</param>
        public static async Task CreateDirectoryWithRetryAsync(string path, bool isCloudSynced = false)
        {
            int attempt = 0;
            var pipeline = isCloudSynced ? _cloudRetryPipeline : _ioRetryPipeline;

            await Task.Run(() =>
            {
                pipeline.Execute(() =>
                {
                    attempt++;
                    if (attempt > 1)
                    {
                        _ = App.FileLogger.LogAsync(
                            $"[DEBUG][CreateDir] リトライ {attempt} 回目: '{path}'");
                    }
                    Directory.CreateDirectory(path);
                });
            }).ConfigureAwait(false);

            // 作成後の存在確認（同期ソフトが即座にリネーム/移動する場合への対策）
            if (isCloudSynced)
            {
                await VerifyExistsWithRetryAsync(path, isDirectory: true).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// リトライ付きで空テキストファイルを作成します。
        /// OneDrive 等の同期ソフトによる一時ロックを想定し、指数バックオフで再試行します。
        /// </summary>
        /// <param name="path">作成するファイルのフルパス</param>
        /// <param name="isCloudSynced">クラウド同期パスの場合 true（リトライ回数・間隔を増加）</param>
        public static async Task CreateFileWithRetryAsync(string path, bool isCloudSynced = false)
        {
            int attempt = 0;
            var pipeline = isCloudSynced ? _cloudRetryPipeline : _ioRetryPipeline;

            await Task.Run(() =>
            {
                pipeline.Execute(() =>
                {
                    attempt++;
                    if (attempt > 1)
                    {
                        _ = App.FileLogger.LogAsync(
                            $"[DEBUG][CreateFile] リトライ {attempt} 回目: '{path}'");
                    }
                    File.WriteAllText(path, string.Empty, System.Text.Encoding.UTF8);
                });
            }).ConfigureAwait(false);

            // 作成後の存在確認（同期ソフトが即座にリネーム/移動する場合への対策）
            if (isCloudSynced)
            {
                await VerifyExistsWithRetryAsync(path, isDirectory: false).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 作成直後のファイル/ディレクトリが存在することを確認します。
        /// クラウド同期ソフトが一時的にファイルを移動・リネームする場合に備え、
        /// 短い間隔でリトライして存在を確認します。
        /// </summary>
        private static async Task VerifyExistsWithRetryAsync(string path, bool isDirectory, int maxAttempts = 5, int baseDelayMs = 100)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                bool exists = isDirectory ? Directory.Exists(path) : File.Exists(path);
                if (exists) return;

                int delayMs = baseDelayMs * (1 << i); // 100, 200, 400, 800, 1600
                _ = App.FileLogger.LogAsync(
                    $"[DEBUG][VerifyExists] 作成直後に未検出（{i + 1}/{maxAttempts}）: '{path}' 次回待機: {delayMs}ms");
                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            // 最終確認でも未検出の場合はログのみ（呼び出し元で UI 表示判断）
            _ = App.FileLogger.LogAsync(
                $"[WARN][VerifyExists] 作成後の存在確認に失敗（全リトライ消費）: '{path}'");
        }

        /// <summary>
        /// 例外の詳細（HResult、InnerException）を含むログメッセージを生成します。
        /// </summary>
        public static string FormatIoException(Exception ex, string operation, string path)
        {
            var sb = new StringBuilder();
            sb.Append($"[ERR][{operation}] Path='{path}'");

            if (ex is IOException ioEx)
            {
                sb.Append($" HResult=0x{ioEx.HResult:X8}");
            }
            else if (ex is UnauthorizedAccessException uaEx)
            {
                sb.Append($" HResult=0x{uaEx.HResult:X8}");
            }

            sb.Append($" {ex.GetType().Name}: {ex.Message}");

            if (ex.InnerException != null)
            {
                sb.Append($" Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }

            return sb.ToString();
        }
    }
}
