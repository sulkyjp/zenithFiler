using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ZenithFiler.Services
{
    /// <summary>GitHub Releases API ベースの自動アップデートサービス。</summary>
    public sealed class UpdateService : IDisposable
    {
        private static readonly string AppVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        private const string GitHubApiUrl =
            "https://api.github.com/repos/sulkyjp/zenithFiler/releases/latest";

        private static readonly string TempUpdateDir =
            Path.Combine(Path.GetTempPath(), "ZenithFiler_update");

        private readonly HttpClient _client;
        private Timer? _periodicTimer;
        private bool _disposed;

        /// <summary>利用可能な新バージョン（なければ null）。</summary>
        public string? AvailableVersion { get; private set; }

        /// <summary>ZIP ダウンロード URL。</summary>
        public string? DownloadUrl { get; private set; }

        /// <summary>ダウンロード＆展開が完了し、再起動可能な状態。</summary>
        public bool IsReadyToRestart { get; private set; }

        /// <summary>ダウンロード中フラグ。</summary>
        public bool IsDownloading { get; private set; }

        /// <summary>状態が変わったときに発火する。UI スレッドから購読すること。</summary>
        public event EventHandler? StateChanged;

        public UpdateService()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ZenithFiler", AppVersion));
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>定期チェックタイマーを開始する。起動 30 秒後に初回、以後 4 時間間隔。</summary>
        public void Initialize()
        {
            if (!WindowSettings.AutoUpdateEnabled) return;
            _periodicTimer = new Timer(
                _ => _ = CheckForUpdatesInternalAsync(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromHours(4));
        }

        /// <summary>定期チェックの有効/無効を切り替える。</summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                _periodicTimer?.Dispose();
                _periodicTimer = new Timer(
                    _ => _ = CheckForUpdatesInternalAsync(),
                    null,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromHours(4));
            }
            else
            {
                _periodicTimer?.Dispose();
                _periodicTimer = null;
            }
        }

        /// <summary>手動チェック用。チェック結果を返す。</summary>
        public async Task<(bool HasUpdate, string? Version, string? Error)> CheckForUpdatesAsync()
        {
            try
            {
                var (hasUpdate, version) = await CheckGitHubAsync();
                if (hasUpdate)
                    return (true, version, null);
                return (false, null, null);
            }
            catch (Exception ex)
            {
                await App.FileLogger.LogAsync($"[Update] チェック失敗: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        /// <summary>ZIP をダウンロードして展開する。GlowBar で進捗表示。</summary>
        public async Task<bool> DownloadAndExtractAsync(MainViewModel? mainVm)
        {
            if (string.IsNullOrEmpty(DownloadUrl) || string.IsNullOrEmpty(AvailableVersion))
                return false;

            IsDownloading = true;
            RaiseStateChanged();

            // GlowBar 開始
            mainVm?.BeginFileOperation("アップデートをダウンロード中...", FlowDirection.LeftToRight);
            if (mainVm != null) mainVm.FileOperationProgress = 2;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            double progressTarget = 2;
            string statusText = "アップデートをダウンロード中...";
            var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            progressTimer.Tick += (_, _) =>
            {
                if (mainVm == null) return;
                double target = Volatile.Read(ref progressTarget);
                double current = mainVm.FileOperationProgress;
                mainVm.FileOperationStatusText = statusText;
                if (Math.Abs(target - current) < 0.3) return;
                double step = (target - current) * 0.18;
                if (step > 0 && step < 0.5) step = 0.5;
                mainVm.FileOperationProgress = Math.Min(current + step, target);
            };
            progressTimer.Start();
            var sw = Stopwatch.StartNew();

            try
            {
                await Task.Run(async () =>
                {
                    // 一時ディレクトリの準備
                    if (Directory.Exists(TempUpdateDir))
                        Directory.Delete(TempUpdateDir, true);
                    Directory.CreateDirectory(TempUpdateDir);

                    var zipPath = Path.Combine(TempUpdateDir, $"ZenithFiler_v{AvailableVersion}.zip");
                    Volatile.Write(ref progressTarget, 5.0);
                    statusText = "アップデートをダウンロード中... (0%)";

                    // ダウンロード（ストリーム）
                    using (var response = await _client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength ?? -1;
                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

                        var buffer = new byte[81920];
                        long totalRead = 0;
                        int bytesRead;
                        int lastPct = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                int pct = (int)(totalRead * 80 / totalBytes); // 0-80%
                                if (pct >= lastPct + 2)
                                {
                                    lastPct = pct;
                                    Volatile.Write(ref progressTarget, 10.0 + pct * 0.8); // 10-74%
                                    statusText = $"アップデートをダウンロード中... ({pct}%)";
                                }
                            }
                        }
                    }

                    Volatile.Write(ref progressTarget, 80.0);
                    statusText = "ZIP を展開中...";

                    // ZIP 展開
                    var extractDir = Path.Combine(TempUpdateDir, "extracted");
                    ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

                    // 展開先にサブフォルダが1つだけある場合、そのフォルダの中身を extracted 直下へ移動
                    var subDirs = Directory.GetDirectories(extractDir);
                    var subFiles = Directory.GetFiles(extractDir);
                    if (subDirs.Length == 1 && subFiles.Length == 0)
                    {
                        var innerDir = subDirs[0];
                        foreach (var file in Directory.GetFiles(innerDir, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(innerDir, file);
                            var destPath = Path.Combine(extractDir, relativePath);
                            var destDir = Path.GetDirectoryName(destPath);
                            if (destDir != null && !Directory.Exists(destDir))
                                Directory.CreateDirectory(destDir);
                            File.Move(file, destPath, overwrite: true);
                        }
                        Directory.Delete(innerDir, true);
                    }

                    Volatile.Write(ref progressTarget, 95.0);
                    statusText = "展開完了";
                });

                IsReadyToRestart = true;
                IsDownloading = false;
                RaiseStateChanged();
                return true;
            }
            catch (Exception ex)
            {
                await App.FileLogger.LogAsync($"[Update] ダウンロード失敗: {FileLoggerService.FormatException(ex)}");
                IsDownloading = false;
                RaiseStateChanged();
                return false;
            }
            finally
            {
                sw.Stop();
                var min = TimeSpan.FromMilliseconds(800);
                if (sw.Elapsed < min) await Task.Delay(min - sw.Elapsed);
                progressTimer.Stop();
                mainVm?.EndFileOperation();
            }
        }

        /// <summary>バッチスクリプトを生成して上書き更新＋再起動を実行する。</summary>
        public void ApplyAndRestart()
        {
            if (!IsReadyToRestart) return;
            LaunchUpdateBatch();
            // アプリを終了
            Application.Current.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
        }

        /// <summary>アプリ終了時にダウンロード済み更新を適用する。Shutdown は呼ばない。</summary>
        public void ApplyOnExit()
        {
            if (!IsReadyToRestart) return;
            LaunchUpdateBatch();
        }

        /// <summary>上書き更新＋再起動バッチを起動する共通処理。</summary>
        private void LaunchUpdateBatch()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var extractedDir = Path.Combine(TempUpdateDir, "extracted");
            var batchPath = Path.Combine(Path.GetTempPath(), "ZenithFiler_update.bat");
            var exePath = Path.Combine(appDir, "ZenithFiler.exe");

            var batchContent = $"""
                @echo off
                timeout /t 2 /nobreak >nul
                xcopy /s /y /q "{extractedDir}\*" "{appDir}\"
                start "" "{exePath}" --updated
                del "%~f0"
                """;

            File.WriteAllText(batchPath, batchContent, System.Text.Encoding.GetEncoding(932)); // Shift_JIS for cmd.exe

            var psi = new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }

        /// <summary>アップデート適用後の一時ファイルをクリーンアップする。</summary>
        public void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(TempUpdateDir))
                    Directory.Delete(TempUpdateDir, true);

                var batchPath = Path.Combine(Path.GetTempPath(), "ZenithFiler_update.bat");
                if (File.Exists(batchPath))
                    File.Delete(batchPath);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[Update] クリーンアップ失敗: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _periodicTimer?.Dispose();
            _client.Dispose();
        }

        // ─── 内部 ───

        private async Task CheckForUpdatesInternalAsync()
        {
            try
            {
                var (hasUpdate, _) = await CheckGitHubAsync();
                if (hasUpdate)
                {
                    await App.FileLogger.LogAsync($"[Update] 新バージョン {AvailableVersion} が利用可能です");
                }

                // 最終チェック日時を保存
                WindowSettings.SaveLastUpdateCheckOnly(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                await App.FileLogger.LogAsync($"[Update] 定期チェック失敗: {ex.Message}");
            }
        }

        private async Task<(bool HasUpdate, string? Version)> CheckGitHubAsync()
        {
            using var response = await _client.GetAsync(GitHubApiUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // tag_name からバージョンを抽出（"v1.2.3" → "1.2.3"）
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var versionStr = tagName.TrimStart('v', 'V');

            if (!Version.TryParse(versionStr, out var remoteVersion))
                return (false, null);

            if (!Version.TryParse(AppVersion, out var currentVersion))
                return (false, null);

            if (remoteVersion <= currentVersion)
                return (false, null);

            // スキップバージョンのチェック
            var settings = WindowSettings.Load();
            if (settings.SkippedVersion == versionStr)
                return (false, null);

            // assets から ZIP を探す
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        && name.Contains("ZenithFiler", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
                return (false, null);

            AvailableVersion = versionStr;
            DownloadUrl = downloadUrl;
            RaiseStateChanged();

            return (true, versionStr);
        }

        private void RaiseStateChanged()
        {
            try
            {
                if (Application.Current?.Dispatcher is Dispatcher d)
                    d.BeginInvoke(() => StateChanged?.Invoke(this, EventArgs.Empty));
            }
            catch { /* アプリ終了中 */ }
        }
    }
}
