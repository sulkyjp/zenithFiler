using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Ja;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System.Collections.Concurrent;
using ZenithFiler;

namespace ZenithFiler.Services
{
    /// <summary>検索時のサイズ・日付フィルタを保持する DTO。</summary>
    public class SearchFilter
    {
        public (long min, long max)? SizeRange { get; init; }
        public (DateTime start, DateTime end)? DateRange { get; init; }
    }

    /// <summary>
    /// Lucene.NET を使用した全文検索インデックスサービス。
    /// </summary>
    public class IndexService : IDisposable
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
        private readonly string _indexPath;
        private FSDirectory? _directory;
        private Analyzer? _analyzer;
        private IndexWriter? _writer;
        
        // セッション中にインデックス作成済み（または作成中）のルートパスを保持
        private readonly HashSet<string> _indexedRoots = new(StringComparer.OrdinalIgnoreCase);
        // 現在インデックス作成中のパスを保持（キャンセル不可な一貫性を保つため）
        private readonly HashSet<string> _inProgressRoots = new(StringComparer.OrdinalIgnoreCase);
        // セマフォ待ちのパスを保持（UI で「待機中」を表示するため）
        private readonly HashSet<string> _pendingRoots = new(StringComparer.OrdinalIgnoreCase);
        // 各ルートの最終インデックス完了日時（永続化）
        private readonly Dictionary<string, DateTime> _indexedTimestamps = new(StringComparer.OrdinalIgnoreCase);
        // ロック（アーカイブ）されたルートパス（一括更新の対象外。永続化）
        private readonly HashSet<string> _lockedRoots = new(StringComparer.OrdinalIgnoreCase);
        // ロック時にスナップショットしたドキュメント件数（永続化）
        private readonly Dictionary<string, int> _lockedDocCounts = new(StringComparer.OrdinalIgnoreCase);

        // スレッドセーフのためのロックオブジェクト
        private readonly object _lockObj = new object();

        // インデックス作成を一括キャンセルするためのグローバル CancellationTokenSource
        private CancellationTokenSource _globalIndexingCts = new CancellationTokenSource();

        // 同時に実行するインデックス作成を1件に制限（ステータスバーの頻繁な切り替え・サーバ負荷を軽減）
        private readonly SemaphoreSlim _indexingSemaphore = new(1, 1);

        private readonly System.Diagnostics.Stopwatch _indexingStopwatch = new();

        // テレメトリ用: UI ポーリングで読み取る（volatile アクセス）
        private volatile string _telemetryCurrentFolder = string.Empty;
        private volatile int _telemetryProcessedCount;
        private volatile string _telemetryRootPath = string.Empty;

        /// <summary>一時停止中の場合、新規のインデックス作成を開始しない。</summary>
        private volatile bool _isPaused;

        /// <summary>現在の更新モード。TabItemViewModel の FileSystemWatcher 連携で参照。</summary>
        public IndexUpdateMode CurrentUpdateMode { get; private set; } = IndexUpdateMode.Interval;

        /// <summary>Interval モード用の定例更新タスク。モード変更時にキャンセル。</summary>
        private CancellationTokenSource? _intervalCts;

        /// <summary>省エネモード。CPU とディスクの負荷を抑える。</summary>
        private volatile bool _ecoMode = true;

        /// <summary>ネットワークドライブを軽めに処理する。</summary>
        private volatile bool _networkLowPriority = true;

        private const string LastFullRebuildFileName = "last_full_rebuild.txt";

        /// <summary>各フォルダのインデックス作成完了時に発火。UI の即時反映に使用。</summary>
        public event Action<string>? RootIndexed;

        /// <summary>一時停止状態が変わったときに発火。UI のバインディング更新用。</summary>
        public event Action? PauseStateChanged;

        private const string IndexedRootsFileName = "indexed_roots.json";

        // インデックス作成済みディレクトリのキャッシュ（セッション中のみ有効）
        // これにより、検索のたびに同じディレクトリを全走査することを防ぐ
        private readonly ConcurrentDictionary<string, byte> _indexedDirectories = new();

        /// <summary>
        /// UI スレッドで IsIndexing を更新する。バックグラウンドスレッドからの更新では WPF バインディングが反映されないため。
        /// </summary>
        private void UpdateIsIndexingOnUiThread()
        {
            bool value;
            lock (_lockObj)
            {
                value = _inProgressRoots.Any();
            }
            try
            {
                var app = Application.Current;
                if (app == null)
                {
                    App.Notification.IsIndexing = value;
                    return;
                }
                if (app.Dispatcher.CheckAccess())
                {
                    App.Notification.IsIndexing = value;
                }
                else
                {
                    app.Dispatcher.BeginInvoke(() => App.Notification.IsIndexing = value);
                }
            }
            catch
            {
                App.Notification.IsIndexing = value;
            }
        }

        public IndexService()
        {
            _indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index");

            if (!System.IO.Directory.Exists(_indexPath))
            {
                System.IO.Directory.CreateDirectory(_indexPath);
            }

            LoadIndexedRoots();
            ValidateIndexedRoots();
        }

        /// <summary>
        /// インデックス作成中のキャンセルに使用するトークンを取得します。
        /// 呼び出し元のトークンとリンクさせる場合は CreateLinkedTokenSource を使用してください。
        /// </summary>
        public CancellationToken GetIndexingCancellationToken()
        {
            lock (_lockObj)
            {
                return _globalIndexingCts.Token;
            }
        }

        /// <summary>インデックス作成が一時停止中かどうか。</summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// インデックス作成を中止します。確認ダイアログは呼び出し元で表示してください。
        /// </summary>
        public void CancelIndexing()
        {
            lock (_lockObj)
            {
                _globalIndexingCts.Cancel();
                _globalIndexingCts.Dispose();
                _globalIndexingCts = new CancellationTokenSource();
            }
        }

        /// <summary>インデックス作成を一時停止します。検索は継続して利用できます。</summary>
        public void PauseIndexing()
        {
            _isPaused = true;
            CancelIndexing();
            PauseStateChanged?.Invoke();
        }

        /// <summary>インデックス作成の一時停止を解除します。</summary>
        public void ResumeIndexing()
        {
            _isPaused = false;
            PauseStateChanged?.Invoke();
        }

        /// <summary>指定ルートのインデックスを一度削除してから再作成します。削除済みファイルもインデックスから除外されます。</summary>
        /// <param name="path">対象ルートパス</param>
        /// <param name="progress">進捗報告</param>
        public void RebuildRoot(string path, IProgress<IndexingProgress>? progress = null)
        {
            if (_isPaused) return;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path)) return;

            UnmarkAsIndexed(path);
            DeleteDocumentsUnderRoot(path);
            TriggerUpdateNow(new[] { path }, progress);
        }

        /// <summary>指定ルートを削除せずに再スキャンして差分更新します。新規・更新は反映されますが、削除済みファイルはインデックスに残ります。</summary>
        /// <param name="path">対象ルートパス</param>
        /// <param name="progress">進捗報告</param>
        public Task UpdateDirectoryDiffAsync(string path, IProgress<IndexingProgress>? progress = null)
        {
            return AddDirectoryToIndexAsync(path, progress, default, forceRescan: true);
        }

        /// <summary>登録済みパスに対して今すぐインデックス更新を実行します。未インデックスのみ対象。ロック済みパスは自動スキップ。</summary>
        public void TriggerUpdateNow(IReadOnlyList<string> paths, IProgress<IndexingProgress>? progress = null)
        {
            if (_isPaused) return;
            foreach (var p in paths ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (!System.IO.Directory.Exists(p)) continue;
                // ロック済みパスは一括更新の対象外（手動の RebuildRoot / UpdateDirectoryDiffAsync は別経路で許容）
                if (IsRootLocked(p)) continue;
                _ = AddDirectoryToIndexAsync(p, progress);
            }
        }

        /// <summary>
        /// インデックス更新モードと設定を反映します。起動時および設定保存時に呼び出します。
        /// </summary>
        /// <param name="settings">インデックス設定（null の場合は Interval 2時間でデフォルト適用）</param>
        /// <param name="getTargetPaths">対象パス一覧を返すデリゲート。Interval 実行時に UI から取得するため。</param>
        /// <param name="itemSettings">アイテム別詳細設定。null の場合は全パスがグローバル設定に従う。</param>
        public void ConfigureIndexUpdate(IndexSettings? settings, Func<IReadOnlyList<string>> getTargetPaths, IReadOnlyList<IndexItemSettingsDto>? itemSettings = null)
        {
            var s = settings ?? IndexSettings.CreateDefaults();
            CurrentUpdateMode = s.UpdateMode;
            _ecoMode = s.EcoMode;
            _networkLowPriority = s.NetworkLowPriority;

            _intervalCts?.Cancel();
            _intervalCts?.Dispose();
            _intervalCts = null;

            if (s.UpdateMode == IndexUpdateMode.Interval && s.UpdateIntervalHours > 0)
            {
                _intervalCts = new CancellationTokenSource();
                var token = _intervalCts.Token;
                var intervalHours = s.UpdateIntervalHours;
                var progress = new Progress<IndexingProgress>(p =>
                {
                    App.Notification.IndexingStatusMessage =
                        $"インデックス更新中: {p.ProcessedCount:N0} 件";
                });

                _ = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromHours(intervalHours), token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;
                        if (CurrentUpdateMode != IndexUpdateMode.Interval || _isPaused) continue;

                        // アイドル時実行: CPU 使用率が閾値以下になるまで待機
                        if (s.IdleOnlyExecution && App.CpuIdleService != null)
                        {
                            await App.CpuIdleService.WaitForIdleAsync(s.IdleCpuThreshold, token);
                            if (token.IsCancellationRequested) break;
                        }

                        var paths = getTargetPaths();
                        if (paths == null || paths.Count == 0) continue;

                        var eligiblePaths = new System.Collections.Generic.List<string>();
                        foreach (var p in paths)
                        {
                            if (string.IsNullOrEmpty(p) || !System.IO.Directory.Exists(p)) continue;
                            // ロック済みパスは定期更新の対象外
                            if (IsRootLocked(p)) continue;
                            // アイテム別スケジュール判定
                            if (itemSettings != null)
                            {
                                var itemSetting = itemSettings.FirstOrDefault(sc =>
                                    string.Equals(sc.Path, p, StringComparison.OrdinalIgnoreCase));
                                if (itemSetting != null)
                                {
                                    if (itemSetting.ScheduleDays != null && !itemSetting.ScheduleDays.Contains(DateTime.Now.DayOfWeek))
                                        continue;
                                    if (itemSetting.ScheduleHour.HasValue && DateTime.Now.Hour != itemSetting.ScheduleHour.Value)
                                        continue;
                                }
                            }
                            // Per-Item 更新方式のルーティング
                            var updateMode = GetItemUpdateMode(p, itemSettings);
                            if (updateMode == IndexItemUpdateMode.FullRebuild)
                            {
                                UnmarkAsIndexed(p);
                                DeleteDocumentsUnderRoot(p);
                            }
                            else
                            {
                                // Incremental: 差分更新（既存を削除せず再スキャン）
                                UnmarkAsIndexed(p);
                            }
                            eligiblePaths.Add(p);
                        }
                        if (eligiblePaths.Count > 0)
                            TriggerUpdateNow(eligiblePaths.ToArray(), progress);
                    }
                }, token);
            }
        }

        /// <summary>指定パスの Per-Item 更新方式を返す。null の場合はグローバルのデフォルト（Incremental 相当）。</summary>
        private static IndexItemUpdateMode GetItemUpdateMode(string path, IReadOnlyList<IndexItemSettingsDto>? itemSettings)
        {
            if (itemSettings == null) return IndexItemUpdateMode.Incremental;
            var setting = itemSettings.FirstOrDefault(s =>
                string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
            return setting?.UpdateMode ?? IndexItemUpdateMode.Incremental;
        }

        /// <summary>指定パスのアイテム別設定を返す。設定がなければ null。</summary>
        public IndexItemSettingsDto? GetItemSettings(string rootPath)
        {
            // 設定は WindowSettings から直接取得（IndexService はステートレス）
            var settings = WindowSettings.Load();
            return settings.IndexItemSettings?.FirstOrDefault(s =>
                string.Equals(s.Path, rootPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>指定ルート配下のドキュメントをインデックスから削除します。Interval 再スキャン前に呼び出し。</summary>
        private void DeleteDocumentsUnderRoot(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) return;
            InitializeWriter();
            if (_writer == null) return;

            try
            {
                var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var prefix = normalized + Path.DirectorySeparatorChar;
                lock (_lockObj)
                {
                    _writer.DeleteDocuments(new PrefixQuery(new Term("path", prefix)));
                    _writer.Commit();
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] DeleteDocumentsUnderRoot failed: {ex.Message}");
            }
        }

        /// <summary>フル再構築を実行します。クールダウン内の場合は false を返します。</summary>
        /// <param name="paths">対象パス一覧</param>
        /// <param name="progress">進捗報告</param>
        /// <param name="cooldownHours">同一パスへの最短間隔（時間）</param>
        /// <returns>実行した場合は true、クールダウンでスキップした場合は false</returns>
        public bool RequestFullRebuild(IReadOnlyList<string> paths, IProgress<IndexingProgress>? progress, int cooldownHours)
        {
            if (paths == null || paths.Count == 0) return true;
            if (_isPaused) return false;

            var lastRebuildPath = Path.Combine(_indexPath, LastFullRebuildFileName);
            try
            {
                if (File.Exists(lastRebuildPath) && File.ReadAllText(lastRebuildPath).Trim() is string s && !string.IsNullOrEmpty(s)
                    && DateTime.TryParse(s, out var last))
                {
                    if ((DateTime.Now - last).TotalHours < cooldownHours)
                        return false;
                }
            }
            catch { }

            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (IsRootLocked(p)) continue;
                UnmarkAsIndexed(p);
            }
            try
            {
                File.WriteAllText(lastRebuildPath, DateTime.Now.ToString("O"));
            }
            catch { }

            TriggerUpdateNow(paths, progress);
            return true;
        }

        /// <summary>
        /// indexed_roots.json と実際の Lucene インデックスの整合性を検証します。
        /// インデックスファイルが存在しない場合は、キャッシュをクリアして再作成を可能にします。
        /// </summary>
        private void ValidateIndexedRoots()
        {
            if (_indexedRoots.Count == 0) return;

            try
            {
                // Lucene のインデックスファイル（segments*）が存在するか確認
                bool hasIndexFiles = System.IO.Directory.GetFiles(_indexPath, "segments*").Any();
                if (!hasIndexFiles)
                {
                    lock (_lockObj)
                    {
                        // ロック済みアイテムのデータは保護する（凍結されたメタデータを消失させない）
                        var lockedEntries = _indexedRoots.Where(r => _lockedRoots.Contains(r)).ToList();
                        var lockedTimestamps = lockedEntries
                            .Where(r => _indexedTimestamps.ContainsKey(r))
                            .ToDictionary(r => r, r => _indexedTimestamps[r], StringComparer.OrdinalIgnoreCase);

                        _indexedRoots.Clear();
                        _indexedTimestamps.Clear();

                        // ロック済みデータを復元
                        foreach (var r in lockedEntries)
                            _indexedRoots.Add(r);
                        foreach (var kvp in lockedTimestamps)
                            _indexedTimestamps[kvp.Key] = kvp.Value;
                    }
                    SaveIndexedRoots();
                    _ = App.FileLogger.LogAsync("[IndexService] Cleared indexed roots (locked items preserved) - no Lucene index files found");
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] Failed to validate indexed roots: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定したパスのインデックス済みマークを解除します。
        /// 検索対象フォルダの削除時など、再インデックスを可能にするために使用します。
        /// ロック状態は保持されます（ユーザー設定であり、インデックス状態とは独立）。
        /// </summary>
        public void UnmarkAsIndexed(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (_lockObj)
            {
                // ロック済みアイテムのインデックス済みマークは保護する（凍結データの消失を防止）
                if (!_lockedRoots.Contains(path))
                    _indexedRoots.Remove(path);
                _inProgressRoots.Remove(path);
                _pendingRoots.Remove(path);
                // _lockedRoots は保持（ロックはユーザー設定 → settings.json で永続化）
            }
            SaveIndexedRoots();
        }

        private void LoadIndexedRoots()
        {
            try
            {
                var path = Path.Combine(_indexPath, IndexedRootsFileName);
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                // 新形式（Dictionary）を試行し、失敗したら旧形式（List）にフォールバック
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        lock (_lockObj)
                        {
                            foreach (var kvp in dict)
                            {
                                if (string.IsNullOrEmpty(kvp.Key)) continue;
                                _indexedRoots.Add(kvp.Key);
                                // value 形式: "ISO8601" or "ISO8601|locked" or "ISO8601|locked|docCount"
                                var value = kvp.Value ?? "";
                                var parts = value.Split('|');
                                if (DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                                    _indexedTimestamps[kvp.Key] = dt;
                                if (parts.Length > 1 && parts[1] == "locked")
                                {
                                    _lockedRoots.Add(kvp.Key);
                                    // ロック時のドキュメント件数を復元
                                    if (parts.Length > 2 && int.TryParse(parts[2], out var docCount))
                                        _lockedDocCounts[kvp.Key] = docCount;
                                }
                            }
                        }
                        return;
                    }
                }
                catch { /* 旧形式にフォールバック */ }

                var roots = JsonSerializer.Deserialize<List<string>>(json);
                if (roots == null) return;

                lock (_lockObj)
                {
                    foreach (var r in roots)
                    {
                        if (!string.IsNullOrEmpty(r))
                            _indexedRoots.Add(r);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] Failed to load indexed roots: {ex.Message}");
            }
        }

        private void SaveIndexedRoots()
        {
            try
            {
                var path = Path.Combine(_indexPath, IndexedRootsFileName);
                Dictionary<string, string> dict;
                lock (_lockObj)
                {
                    dict = _indexedRoots.ToDictionary(
                        r => r,
                        r =>
                        {
                            var ts = _indexedTimestamps.TryGetValue(r, out var dt) ? dt.ToString("o") : "";
                            if (_lockedRoots.Contains(r))
                            {
                                var docCount = _lockedDocCounts.TryGetValue(r, out var c) ? c.ToString() : "";
                                return ts + "|locked|" + docCount;
                            }
                            return ts;
                        },
                        StringComparer.OrdinalIgnoreCase);
                }
                var json = JsonSerializer.Serialize(dict);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] Failed to save indexed roots: {ex.Message}");
            }
        }

        /// <summary>
        /// インデックスライターを初期化します。
        /// </summary>
        private void InitializeWriter()
        {
            lock (_lockObj)
            {
                if (_writer != null) return;

                try
                {
                    _directory = FSDirectory.Open(_indexPath);
                    // 日本語ファイル名に対応するため JapaneseAnalyzer を使用
                    _analyzer = new JapaneseAnalyzer(AppLuceneVersion);

                    var config = new IndexWriterConfig(AppLuceneVersion, _analyzer)
                    {
                        OpenMode = OpenMode.CREATE_OR_APPEND
                    };

                    _writer = new IndexWriter(_directory, config);
                }
                catch (Exception ex)
                {
                    // 部分状態をクリーンアップし、_writer = null で安全に劣化
                    // 全呼び出し元は既に if (_writer == null) return; チェック済み
                    _ = App.FileLogger.LogAsync($"[IndexService] InitializeWriter failed: {ex.Message}");
                    _writer = null;
                    _analyzer?.Dispose();
                    _analyzer = null;
                    _directory?.Dispose();
                    _directory = null;
                }
            }
        }

        /// <summary>
        /// 指定したルートパスがインデックス作成中かどうかを判定します。
        /// </summary>
        public bool IsRootInProgress(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            lock (_lockObj)
            {
                return _inProgressRoots.Any(r =>
                    string.Equals(r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalized, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 指定したルートパスがインデックス済みかどうかを判定します。
        /// </summary>
        public bool IsRootIndexed(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            lock (_lockObj)
            {
                return _indexedRoots.Any(r =>
                    string.Equals(r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalized, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 指定したフォルダパス群について、各フォルダ配下の全ファイルサイズ合計をインデックスから取得する。
        /// </summary>
        public async Task<Dictionary<string, long>> GetFolderSizesFromIndexAsync(
            IReadOnlyList<string> folderPaths,
            CancellationToken token = default)
        {
            var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (folderPaths == null || folderPaths.Count == 0) return result;

            InitializeWriter();
            if (_writer == null) return result;

            DirectoryReader? reader = null;
            try
            {
                lock (_lockObj)
                {
                    reader = _writer.GetReader(applyAllDeletes: true);
                }

                await Task.Run(() =>
                {
                    var searcher = new IndexSearcher(reader);
                    foreach (var folderPath in folderPaths)
                    {
                        if (token.IsCancellationRequested) break;
                        if (string.IsNullOrEmpty(folderPath)) continue;

                        var prefix = folderPath.TrimEnd(
                            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;

                        var boolQuery = new BooleanQuery();
                        boolQuery.Add(new PrefixQuery(new Term("path", prefix)), Occur.MUST);
                        boolQuery.Add(NumericRangeQuery.NewInt32Range("is_dir", 0, 0, true, true), Occur.MUST);

                        var probe = searcher.Search(boolQuery, 1);
                        if (probe.TotalHits == 0)
                        {
                            result[folderPath] = 0;
                            continue;
                        }

                        var hits = searcher.Search(boolQuery, probe.TotalHits);
                        long totalSize = 0;
                        foreach (var hit in hits.ScoreDocs)
                        {
                            if (token.IsCancellationRequested) break;
                            var sizeStr = searcher.Doc(hit.Doc).Get("size");
                            if (long.TryParse(sizeStr, out var s))
                                totalSize += s;
                        }

                        result[folderPath] = totalSize;
                    }
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] GetFolderSizesFromIndexAsync error: {ex.Message}");
            }
            finally
            {
                reader?.Dispose();
            }
            return result;
        }

        /// <summary>
        /// 指定したパスが既にインデックス済みか（またはその親ディレクトリがインデックス済みか）を判定します。
        /// </summary>
        public bool IsPathIndexed(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            lock (_lockObj)
            {
                // 自分自身、または親ディレクトリのいずれかが登録されていればインデックス済みとみなす
                foreach (var root in _indexedRoots)
                {
                    if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                foreach (var root in _inProgressRoots)
                {
                    if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 指定したルートパスがセマフォ待ち（待機中）かどうかを判定します。
        /// </summary>
        public bool IsRootPending(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            lock (_lockObj)
            {
                return _pendingRoots.Any(r =>
                    string.Equals(r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalized, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 指定したルートパスの最終インデックス完了日時を取得します。未完了の場合は null。
        /// </summary>
        public DateTime? GetLastIndexedTime(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            lock (_lockObj)
            {
                foreach (var kvp in _indexedTimestamps)
                {
                    if (string.Equals(kvp.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalized, StringComparison.OrdinalIgnoreCase))
                        return kvp.Value;
                }
            }
            return null;
        }

        /// <summary>指定パスのロック（アーカイブ）状態を設定します。ロック済みパスは一括更新・定期更新の対象外。</summary>
        public void SetLocked(string path, bool locked)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (locked)
            {
                // ロック時にドキュメント件数をスナップショットして凍結する（Lucene I/O はロック外で実行）
                bool needSnapshot;
                lock (_lockObj) { needSnapshot = !_lockedDocCounts.ContainsKey(path); }
                int snapshotCount = needSnapshot ? GetDocumentCountForRoot(path) : 0;

                lock (_lockObj)
                {
                    _lockedRoots.Add(path);
                    if (needSnapshot && snapshotCount > 0)
                        _lockedDocCounts[path] = snapshotCount;
                    // ロック時にインデックス済みマークも確保する（未登録なら追加）
                    _indexedRoots.Add(path);
                }
            }
            else
            {
                lock (_lockObj)
                {
                    _lockedRoots.Remove(path);
                    _lockedDocCounts.Remove(path);
                }
            }
            SaveIndexedRoots();
        }

        /// <summary>指定パスがロック（アーカイブ）されているかを返します。</summary>
        public bool IsRootLocked(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            lock (_lockObj)
            {
                return _lockedRoots.Contains(path);
            }
        }

        /// <summary>ロック時にスナップショットしたドキュメント件数を返します。未保存の場合は 0。</summary>
        public int GetLockedDocumentCount(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            lock (_lockObj)
            {
                return _lockedDocCounts.TryGetValue(path, out var count) ? count : 0;
            }
        }

        /// <summary>
        /// 指定したパスをインデックス済みとしてマークします。
        /// </summary>
        public void MarkAsIndexed(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (_lockObj)
            {
                _indexedRoots.Add(path);
                _inProgressRoots.Remove(path);
                _pendingRoots.Remove(path);
                _indexedTimestamps[path] = DateTime.Now;
                if (_inProgressRoots.Count == 0) _indexingStopwatch.Stop();
            }
            UpdateIsIndexingOnUiThread();
            SaveIndexedRoots();
            RootIndexed?.Invoke(path);
        }

        private void MarkAsInProgress(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (_lockObj)
            {
                bool wasEmpty = _inProgressRoots.Count == 0;
                _inProgressRoots.Add(path);
                _pendingRoots.Remove(path);
                if (wasEmpty) _indexingStopwatch.Restart();
            }
            _telemetryRootPath = path;
            UpdateIsIndexingOnUiThread();
        }

        private void MarkAsPending(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (_lockObj)
            {
                _pendingRoots.Add(path);
            }
        }

        /// <summary>
        /// 指定したディレクトリ配下のファイルをインデックスに追加します（再帰的）。
        /// </summary>
        /// <param name="path">インデックス対象のルートパス</param>
        /// <param name="progress">進捗報告用（100件ごとに Report される）</param>
        /// <param name="forceRescan">true の場合は既にインデックス済みでも再スキャンする（差分更新用）</param>
        public async Task AddDirectoryToIndexAsync(string path, IProgress<IndexingProgress>? progress = null, CancellationToken token = default, bool forceRescan = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                _ = App.FileLogger.LogAsync("[IndexService] AddDirectoryToIndexAsync skipped - path is null or empty");
                return;
            }
            if (_isPaused)
            {
                _ = App.FileLogger.LogAsync("[IndexService] AddDirectoryToIndexAsync skipped - indexing is paused");
                return;
            }

            // 既にインデックス済み、または作成中ならスキップ（forceRescan 時はバイパス）
            if (!forceRescan)
            {
                if (IsPathIndexed(path))
                {
                    // 親ルートでカバー済みだが、登録フォルダとして別行表示されている場合、UI を一貫させるためにルートとしてマーク
                    if (!IsRootIndexed(path))
                    {
                        MarkAsIndexed(path);
                    }
                    _ = App.FileLogger.LogAsync($"[IndexService] AddDirectoryToIndexAsync skipped - path already covered: {path}");
                    return;
                }
            }

            CancellationToken effectiveToken;
            CancellationTokenSource? linkedCts = null;
            try
            {
                CancellationToken globalToken;
                lock (_lockObj)
                {
                    globalToken = _globalIndexingCts.Token;
                }
                if (token != default && token.CanBeCanceled)
                {
                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken, token);
                    effectiveToken = linkedCts.Token;
                }
                else
                {
                    effectiveToken = globalToken;
                }
            }
            catch (ObjectDisposedException)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] AddDirectoryToIndexAsync skipped - CancellationTokenSource disposed: {path}");
                return;
            }

            bool semaphoreAcquired = false;
            try
            {
                MarkAsPending(path);
                await _indexingSemaphore.WaitAsync(effectiveToken).ConfigureAwait(false);
                semaphoreAcquired = true;

                try
                {
                    // セマフォ取得後に再チェック（forceRescan 時はバイパス）
                    if (!forceRescan && IsPathIndexed(path))
                    {
                        if (!IsRootIndexed(path))
                            MarkAsIndexed(path);
                        _ = App.FileLogger.LogAsync($"[IndexService] AddDirectoryToIndexAsync skipped after semaphore - path already covered: {path}");
                        return;
                    }
                    await Task.Run(async () =>
                    {
                        MarkAsInProgress(path); // 開始時にマーク（重複実行防止）
                        InitializeWriter();
                        if (_writer == null)
                        {
                            lock (_lockObj)
                            {
                                _inProgressRoots.Remove(path);
                                if (_inProgressRoots.Count == 0) _indexingStopwatch.Stop();
                            }
                            UpdateIsIndexingOnUiThread();
                            _ = App.FileLogger.LogAsync($"[IndexService] AddDirectoryToIndexAsync aborted - writer is null: {path}");
                            return;
                        }

                        try
                        {
                            var dirInfo = new DirectoryInfo(path);
                            if (!dirInfo.Exists)
                            {
                                lock (_lockObj)
                                {
                                    _inProgressRoots.Remove(path);
                                    if (_inProgressRoots.Count == 0) _indexingStopwatch.Stop();
                                }
                                UpdateIsIndexingOnUiThread();
                                _ = App.FileLogger.LogAsync($"[IndexService] AddDirectoryToIndexAsync skipped - directory does not exist: {path}");
                                return;
                            }

                            var isBox = PathHelper.IsBoxPath(path);
                            var isNetwork = PathHelper.IsNetworkPath(path);

                            // Box フォルダのプリスキャン（暖気運転）: スタブ未生成によるスキャン失敗を防ぐ
                            if (isBox)
                            {
                                await WarmUpBoxDirectoryAsync(path, effectiveToken);
                            }

                            // 手動再帰走査用オプション（RecurseSubdirectories = false で除外フォルダの子孫を完全スキップ）
                            var options = new EnumerationOptions
                            {
                                IgnoreInaccessible = true,
                                RecurseSubdirectories = false,
                                AttributesToSkip = FileAttributes.System | FileAttributes.Temporary
                            };

                            // 再開時は既にインデックス済みの件数から表示する（途中から再開であることが分かるようにする）
                            int baseCount = GetDocumentCountForRoot(path);
                            int count = 0;
                            progress?.Report(new IndexingProgress(baseCount, path));

                            // フォルダ名追跡
                            string? currentFolderName = null;

                            // パフォーマンス設定（Box/ネットワークは短い間隔、ローカルは大きめのバッチ）
                            int commitInterval = isBox ? 50 : (isNetwork ? 100 : 500);
                            int delayMs = GetThrottleDelayMs(isNetwork, isBox);

                            // スレッド優先度を下げてファイル操作のレスポンスを優先
                            var originalPriority = Thread.CurrentThread.Priority;
                            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                            try
                            {
                            // スタックベース再帰走査（除外フォルダをスタックに入れない → 子孫を一切走査しない）
                            int skippedCount = 0;
                            var dirStack = new Stack<DirectoryInfo>();
                            dirStack.Push(dirInfo);
                            int lastCommitCount = 0;
                            long lastProgressTick = Environment.TickCount64;

                            while (dirStack.Count > 0)
                            {
                                if (effectiveToken.IsCancellationRequested) break;
                                var currentDir = dirStack.Pop();

                                // サブディレクトリを列挙（除外フォルダはスタックに入れず子孫も走査しない）
                                try
                                {
                                    foreach (var subDir in currentDir.EnumerateDirectories("*", options))
                                    {
                                        if (effectiveToken.IsCancellationRequested) break;
                                        if (IsExcludedFolderName(subDir.Name)) continue;
                                        string dirPathForCheck = subDir.FullName.TrimEnd('\\') + "\\";
                                        if (IsExcludedPath(dirPathForCheck)) continue;

                                        try
                                        {
                                            AddFolderToIndexInternal(subDir);
                                            count++;
                                            dirStack.Push(subDir);
                                        }
                                        catch (Exception ex)
                                        {
                                            skippedCount++;
                                            if (skippedCount <= 5)
                                                _ = App.FileLogger.LogAsync($"[IndexService] Skipped folder: {subDir.FullName} - {ex.Message}");
                                        }
                                    }
                                }
                                catch { /* IgnoreInaccessible fallback */ }

                                // 現在のディレクトリ内のファイルを列挙
                                try
                                {
                                    foreach (var file in currentDir.EnumerateFiles("*", options))
                                    {
                                        if (effectiveToken.IsCancellationRequested) break;
                                        if (IsExcludedFileName(file.Name)) continue;

                                        try
                                        {
                                            AddFileToIndexInternal(file);
                                            count++;
                                        }
                                        catch (Exception ex)
                                        {
                                            skippedCount++;
                                            if (skippedCount <= 5)
                                                _ = App.FileLogger.LogAsync($"[IndexService] Skipped file: {file.FullName} - {ex.Message}");
                                        }
                                    }
                                }
                                catch { /* IgnoreInaccessible fallback */ }

                                // 時間ベース進捗報告（100ms ごと — UI スレッドへの負荷を最小限に）
                                long now = Environment.TickCount64;
                                if (now - lastProgressTick >= 100)
                                {
                                    currentFolderName = currentDir.Name;
                                    progress?.Report(new IndexingProgress(baseCount + count, path, currentFolderName));
                                    _telemetryProcessedCount = baseCount + count;
                                    _telemetryCurrentFolder = currentFolderName;
                                    lastProgressTick = now;
                                }

                                // Commit 間隔（I/O 負荷分散）
                                if (count - lastCommitCount >= commitInterval)
                                {
                                    lock (_lockObj)
                                    {
                                        _writer?.Commit();
                                    }
                                    lastCommitCount = count;
                                    if (delayMs > 0)
                                        await Task.Delay(delayMs, effectiveToken);
                                }
                            }
                            if (skippedCount > 0)
                                _ = App.FileLogger.LogAsync($"[IndexService] Total skipped items for {path}: {skippedCount}");
                            }
                            finally
                            {
                                Thread.CurrentThread.Priority = originalPriority;
                            }

                        progress?.Report(new IndexingProgress(baseCount + count, path, null));
                        lock (_lockObj)
                        {
                            _writer?.Commit();
                        }

                        // 正常終了した場合のみ「完了」としてマーク
                        if (!effectiveToken.IsCancellationRequested)
                        {
                            MarkAsIndexed(path);
                            var finalCount = count;
                            _ = Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                App.Notification.Notify($"{finalCount:N0} 件をインデックスに追加しました", $"インデックス作成完了: {path} ({finalCount} 件)");
                            });
                        }
                        else
                        {
                            lock (_lockObj)
                            {
                                _inProgressRoots.Remove(path);
                                _pendingRoots.Remove(path);
                                if (_inProgressRoots.Count == 0) _indexingStopwatch.Stop();
                            }
                            UpdateIsIndexingOnUiThread();
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_lockObj)
                        {
                            _inProgressRoots.Remove(path);
                            _pendingRoots.Remove(path);
                            if (_inProgressRoots.Count == 0) _indexingStopwatch.Stop();
                        }
                        UpdateIsIndexingOnUiThread();
                        _ = App.FileLogger.LogAsync($"[IndexService] Indexing error: {ex.Message}");
                    }
                }, effectiveToken).ConfigureAwait(false);
                }
                finally
                {
                    // Release は外側 finally で実行（WaitAsync キャンセル時の漏れを防止）
                }
            }
            catch (OperationCanceledException)
            {
                // WaitAsync のキャンセル or Task.Run 内でキャンセル
                lock (_lockObj)
                {
                    _pendingRoots.Remove(path);
                }
            }
            finally
            {
                if (semaphoreAcquired)
                    _indexingSemaphore.Release();
                linkedCts?.Dispose();
            }
        }

        private static readonly string[] _excludedPathKeywords =
        [
            @"\.git\", @"\node_modules\", @"\.vs\", @"\obj\", @"\bin\",
            // システムフォルダ
            @"\$Recycle.Bin\", @"\System Volume Information\",
            @"\$WINDOWS.~BT\", @"\$WINDOWS.~WS\", @"\$WinREAgent\",
            @"\Recovery\", @"\PerfLogs\",
            // アプリケーション一時フォルダ
            @"\AppData\Local\Temp\", @"\AppData\Local\Microsoft\Windows\INetCache\",
            @"\AppData\Local\Microsoft\Windows\Temporary Internet Files\",
        ];
        private bool IsExcludedPath(string fullPath)
        {
            foreach (var kw in _excludedPathKeywords)
            {
                if (fullPath.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>ユーザーが通常使用しないシステム関連ファイルかどうかを判定します。</summary>
        private static readonly HashSet<string> _excludedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "desktop.ini", "Thumbs.db", "ehthumbs.db", "ehthumbs_vista.db",
            ".DS_Store", ".localized", ".Spotlight-V100", ".Trashes",
            "NTUSER.DAT", "ntuser.dat.LOG1", "ntuser.dat.LOG2", "ntuser.ini",
            "UsrClass.dat", "UsrClass.dat.LOG1", "UsrClass.dat.LOG2",
        };
        private static readonly HashSet<string> _excludedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".tmp", ".temp", ".bak", ".swp", ".swo",
        };
        private static bool IsExcludedFileName(string fileName)
        {
            // 拡張子がないファイルを除外（一時ファイル・キャッシュ等のノイズを排除）
            if (!Path.HasExtension(fileName))
                return true;

            // 特定のシステムファイル名
            if (_excludedFileNames.Contains(fileName))
                return true;

            // Office 一時ファイル（~$xxxxx）
            if (fileName.StartsWith("~$", StringComparison.Ordinal))
                return true;

            // 一時ファイル拡張子
            var ext = Path.GetExtension(fileName);
            if (_excludedExtensions.Contains(ext))
                return true;

            return false;
        }

        /// <summary>ユーザーが通常使用しないシステムフォルダ・中間生成フォルダかどうかを判定します。</summary>
        private static readonly HashSet<string> _excludedFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // Windows システムフォルダ
            "$Recycle.Bin", "System Volume Information",
            "$WINDOWS.~BT", "$WINDOWS.~WS", "$WinREAgent",
            "Recovery", "PerfLogs", "$SysReset",
            "MSOCache", "Config.Msi",
            // バージョン管理
            ".git", ".svn", ".hg",
            // IDE / ビルド中間フォルダ
            ".vs", "obj", "bin",
            // パッケージマネージャ
            "node_modules", "bower_components", ".nuget",
            // 言語ランタイムキャッシュ
            "__pycache__", ".mypy_cache", ".pytest_cache", ".tox",
            "venv", ".venv",
            // フレームワークビルド出力
            ".next", ".nuxt", ".gradle",
            // ブラウザキャッシュ
            "INetCache", "Temporary Internet Files",
        };
        private static bool IsExcludedFolderName(string folderName)
        {
            if (_excludedFolderNames.Contains(folderName))
                return true;

            // $ で始まるシステムフォルダ（Windows 関連）
            if (folderName.StartsWith('$') && !folderName.Equals("$", StringComparison.Ordinal))
                return true;

            return false;
        }

        /// <summary>
        /// Box Drive のディレクトリ構造を再帰的に列挙し、スタブ（プレースホルダ）の生成を促します。
        /// インデックス作成の直前に行うことで、未生成のスタブによるスキャン失敗を防ぎます。
        /// </summary>
        private async Task WarmUpBoxDirectoryAsync(string path, CancellationToken token)
        {
            if (!PathHelper.IsBoxPath(path)) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                App.Notification.IndexingStatusMessage = "Box フォルダを準備中（暖気運転中）...";
            });

            try
            {
                // PathHelper.WarmUpBoxPath を使用して共通の暖気運転ロジックを利用
                // インデックス作成時は再帰的に列挙するため、追加で再帰列挙も行う
                await Task.Run(() =>
                {
                    PathHelper.WarmUpBoxPath(path);
                    
                    // インデックス作成時は全階層を暖気運転するため、再帰的に列挙
                    if (!token.IsCancellationRequested)
                    {
                        var options = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = true,
                        };
                        foreach (var _ in System.IO.Directory.EnumerateDirectories(path, "*", options))
                        {
                            if (token.IsCancellationRequested) break;
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] Box warm-up error: {ex.Message}");
            }
        }

        /// <summary>パフォーマンス設定に応じた待機時間（ミリ秒）を返します。Box 領域は OS のスタブ生成待ちを考慮して長めに設定します。</summary>
        private int GetThrottleDelayMs(bool isNetwork, bool isBox)
        {
            if (isBox)
            {
                // Box 領域のスキャン中は OS 側でのスタブ生成待ち時間を考慮（50件ごとに 200ms）
                int boxDelay = 200;
                if (_networkLowPriority) boxDelay += 150; // ネットワーク軽め設定ならさらに厚くしてタイムアウト防止
                if (_ecoMode) boxDelay += 50;
                return boxDelay;
            }
            if (isNetwork && _networkLowPriority)
                return _ecoMode ? 100 : 50;
            return _ecoMode ? 50 : 15;
        }

        /// <summary>
        /// 単一ファイルをインデックスに追加・更新します。
        /// </summary>
        public void AddFileToIndex(string filePath)
        {
             InitializeWriter();
             if (_writer == null) return;
             if (!File.Exists(filePath)) return;

             try 
             {
                 AddFileToIndexInternal(new FileInfo(filePath));
                 lock (_lockObj)
                 {
                     _writer.Commit();
                 }
             }
             catch { }
        }

        private void AddFileToIndexInternal(FileInfo file)
        {
            // 拡張子がないファイルはインデックス登録しない（AddFileToIndex 単体追加時用）
            if (!Path.HasExtension(file.Name))
                return;

            var doc = new Document();
            
            // Path: IDとして使用 (StringFieldはトークン化されない＝完全一致のみ)
            doc.Add(new StringField("path", file.FullName, Field.Store.YES));
            
            // Name: ファイル名検索用 (TextFieldはトークン化される)
            doc.Add(new TextField("name", file.Name, Field.Store.YES));
            // NameRaw: トークン化しない生のファイル名（ワイルドカード・部分一致フォールバック用、小文字で格納）
            doc.Add(new StringField("name_raw", file.Name.ToLowerInvariant(), Field.Store.YES));

            // Size: ファイルサイズ（バイト）
            doc.Add(new Int64Field("size", file.Length, Field.Store.YES));

            // LastModified: 更新日時（DateTools 形式で保存、旧 Int64 Ticks と読み出し互換）
            doc.Add(new StringField("modified", DateTools.DateToString(file.LastWriteTime, DateResolution.SECOND), Field.Store.YES));
            doc.Add(new Int32Field("is_dir", 0, Field.Store.YES));

            lock (_lockObj)
            {
                // パスが同じなら更新（削除＆追加）
                // Termは "フィールド名", "値"
                _writer?.UpdateDocument(new Term("path", file.FullName), doc);
            }
        }

        private void AddFolderToIndexInternal(DirectoryInfo dir)
        {
            var doc = new Document();
            doc.Add(new StringField("path", dir.FullName, Field.Store.YES));
            doc.Add(new TextField("name", dir.Name, Field.Store.YES));
            doc.Add(new StringField("name_raw", dir.Name.ToLowerInvariant(), Field.Store.YES));
            doc.Add(new Int64Field("size", 0L, Field.Store.YES));
            doc.Add(new StringField("modified", DateTools.DateToString(dir.LastWriteTime, DateResolution.SECOND), Field.Store.YES));
            doc.Add(new Int32Field("is_dir", 1, Field.Store.YES));

            lock (_lockObj)
            {
                _writer?.UpdateDocument(new Term("path", dir.FullName), doc);
            }
        }

        /// <summary>
        /// 単一ファイルをインデックスから削除します。
        /// </summary>
        public void RemoveFileFromIndex(string filePath)
        {
            InitializeWriter();
            if (_writer == null) return;
            
            try
            {
                lock (_lockObj)
                {
                    _writer.DeleteDocuments(new Term("path", filePath));
                    _writer.Commit();
                }
            }
            catch { }
        }

        /// <summary>
        /// インデックスに登録されているドキュメントの総件数を取得します。
        /// </summary>
        public int GetIndexedDocumentCount()
        {
            try
            {
                using var dir = FSDirectory.Open(_indexPath);
                if (!DirectoryReader.IndexExists(dir)) return 0;

                using var reader = DirectoryReader.Open(dir);
                return reader.NumDocs;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 指定したルートパス配下のインデックス件数を取得します。
        /// </summary>
        public int GetDocumentCountForRoot(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) return 0;

            try
            {
                using var dir = FSDirectory.Open(_indexPath);
                if (!DirectoryReader.IndexExists(dir)) return 0;

                using var reader = DirectoryReader.Open(dir);
                var searcher = new IndexSearcher(reader);

                var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var prefix = normalized + Path.DirectorySeparatorChar;
                var term = new Term("path", prefix);
                var query = new PrefixQuery(term);

                var result = searcher.Search(query, 1);
                return result.TotalHits;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>グローバルテレメトリスナップショット。ポップアップ表示中のみ呼ぶこと。</summary>
        public IndexingTelemetry GetTelemetrySnapshot()
        {
            int activeThreads;
            lock (_lockObj) { activeThreads = _inProgressRoots.Count; }
            return new IndexingTelemetry
            {
                CurrentScanFolder = _telemetryCurrentFolder,
                ProcessedCount = _telemetryProcessedCount,
                TotalDbRecords = GetIndexedDocumentCount(),
                ActiveThreads = activeThreads,
                MaxParallelDegree = 1,
                Elapsed = _indexingStopwatch.Elapsed,
                RootPath = _telemetryRootPath,
            };
        }

        /// <summary>特定ルートのテレメトリスナップショット。</summary>
        public IndexingTelemetry GetTelemetrySnapshotForRoot(string rootPath)
        {
            int activeThreads;
            bool isActive;
            lock (_lockObj)
            {
                isActive = _inProgressRoots.Contains(rootPath);
                activeThreads = isActive ? 1 : 0;
            }
            return new IndexingTelemetry
            {
                CurrentScanFolder = isActive ? _telemetryCurrentFolder : string.Empty,
                ProcessedCount = _telemetryProcessedCount,
                TotalDbRecords = GetDocumentCountForRoot(rootPath),
                ActiveThreads = activeThreads,
                MaxParallelDegree = 1,
                Elapsed = _indexingStopwatch.Elapsed,
                RootPath = rootPath,
            };
        }

        /// <summary>
        /// 検索文字列をアナライザーでトークン化し、PhraseQuery または TermQuery を構築する。
        /// ParseException 時のフォールバックで使用（*{escaped}* はトークン化されず日本語でヒットしないため）。
        /// </summary>
        private static Query BuildQueryFromTokenized(Analyzer analyzer, string fieldName, string searchContext)
        {
            var tokens = new List<string>();
            using var stream = analyzer.GetTokenStream(fieldName, new StringReader(searchContext));
            var termAttr = stream.GetAttribute<ICharTermAttribute>();
            stream.Reset();
            while (stream.IncrementToken())
            {
                tokens.Add(termAttr.ToString());
            }
            stream.End();

            if (tokens.Count == 0) return new BooleanQuery();
            if (tokens.Count == 1) return new TermQuery(new Term(fieldName, tokens[0]));

            var phraseQuery = new PhraseQuery();
            foreach (var t in tokens)
                phraseQuery.Add(new Term(fieldName, t));
            return phraseQuery;
        }

        /// <summary>
        /// インデックスからファイルを検索します。
        /// path, name, modified, is_dir を返し、ファイルシステムアクセスなしで即時表示可能にします。
        /// </summary>
        /// <param name="searchContext">検索キーワード（空の場合は rootPath があれば全件検索）</param>
        /// <param name="rootPath">検索対象のルートパス（指定した場合、このパス以下のみを検索します）</param>
        /// <returns>ヒットしたファイルの SearchHit リスト（更新日時降順でソート済み）</returns>
        public List<SearchHit> Search(string searchContext, string? rootPath = null, int maxResultsOverride = 0, SearchFilter? filter = null)
        {
            // キーワードが空でも rootPath があれば検索を続行（インデックス登録確認用）
            if (string.IsNullOrWhiteSpace(searchContext) && string.IsNullOrEmpty(rootPath)) return new List<SearchHit>();

            try 
            {
                using var dir = FSDirectory.Open(_indexPath);
                if (!DirectoryReader.IndexExists(dir)) return new List<SearchHit>();

                using var reader = DirectoryReader.Open(dir);
                var searcher = new IndexSearcher(reader);
                using var analyzer = new JapaneseAnalyzer(AppLuceneVersion);

                Query query;
                
                if (!string.IsNullOrWhiteSpace(searchContext))
                {
                    var parser = new QueryParser(AppLuceneVersion, "name", analyzer);
                    parser.AllowLeadingWildcard = true;
                    parser.DefaultOperator = Operator.AND;

                    Query parsedQuery;
                    try
                    {
                        parsedQuery = parser.Parse(searchContext);
                    }
                    catch (ParseException)
                    {
                        parsedQuery = BuildQueryFromTokenized(analyzer, "name", searchContext);
                    }

                    // 常に name_raw ワイルドカードを OR で追加（部分一致 100% ヒット保証）
                    var escaped = QueryParser.Escape(searchContext).ToLowerInvariant();
                    var wildcardOnRaw = new WildcardQuery(new Term("name_raw", "*" + escaped + "*"));

                    var hybrid = new BooleanQuery();
                    hybrid.Add(parsedQuery, Occur.SHOULD);
                    hybrid.Add(wildcardOnRaw, Occur.SHOULD);
                    query = hybrid;
                }
                else
                {
                    // キーワードが空の場合は全件ヒットするクエリを使用（インデックス登録確認用）
                    query = new MatchAllDocsQuery();
                }

                // rootPath による絞り込み（PrefixQuery）
                if (!string.IsNullOrEmpty(rootPath))
                {
                    var booleanQuery = new BooleanQuery();
                    
                    // query が MatchAllDocsQuery 以外の場合は MUST で組み合わせる
                    if (!(query is MatchAllDocsQuery))
                    {
                        booleanQuery.Add(query, Occur.MUST);
                    }

                    // パスは StringField なので、PrefixQuery で前方一致検索を行う
                    // rootPath が "C:\Work" なら "C:\Work\..." がヒットする
                    var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var prefix = normalized + Path.DirectorySeparatorChar;
                    var term = new Term("path", prefix);
                    var prefixQuery = new PrefixQuery(term);
                    booleanQuery.Add(prefixQuery, Occur.MUST);

                    query = booleanQuery;
                }

                // サイズ・日付フィルタの適用
                query = ApplySearchFilter(query, filter);

                // 登録確認の場合は多めに取得（1000件）、通常検索は500件
                // maxResultsOverride > 0 の場合はその値を使用（CSV出力等の全件取得用途）
                int maxResults = maxResultsOverride > 0 ? maxResultsOverride :
                    (string.IsNullOrWhiteSpace(searchContext) && !string.IsNullOrEmpty(rootPath) ? 1000 : 500);
                // 上位件数を取得（更新日時降順でソート）
                // DateTools 形式は STRING でソート可。旧 Int64 形式も互換のため STRING でソート
                var sort = new Sort(new SortField("modified", SortFieldType.STRING, true));
                var hits = searcher.Search(query, maxResults, sort).ScoreDocs;

                // AND で 0 件 → OR フォールバック（複数単語の場合のみ）
                if (hits.Length == 0 && !string.IsNullOrWhiteSpace(searchContext) && searchContext.Trim().Contains(' '))
                {
                    var orParser = new QueryParser(AppLuceneVersion, "name", analyzer);
                    orParser.AllowLeadingWildcard = true;
                    orParser.DefaultOperator = Operator.OR;
                    try
                    {
                        var orParsed = orParser.Parse(searchContext);
                        var escaped = QueryParser.Escape(searchContext).ToLowerInvariant();
                        var wildcardOnRaw = new WildcardQuery(new Term("name_raw", "*" + escaped + "*"));
                        var orHybrid = new BooleanQuery();
                        orHybrid.Add(orParsed, Occur.SHOULD);
                        orHybrid.Add(wildcardOnRaw, Occur.SHOULD);

                        Query orQuery = orHybrid;
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            var bq = new BooleanQuery();
                            bq.Add(orQuery, Occur.MUST);
                            var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            bq.Add(new PrefixQuery(new Term("path", normalized + Path.DirectorySeparatorChar)), Occur.MUST);
                            orQuery = bq;
                        }
                        orQuery = ApplySearchFilter(orQuery, filter);
                        hits = searcher.Search(orQuery, maxResults, sort).ScoreDocs;
                    }
                    catch { /* OR フォールバック失敗は無視 */ }
                }

                var results = new List<SearchHit>();

                foreach (var hit in hits)
                {
                    var foundDoc = searcher.Doc(hit.Doc);
                    var path = foundDoc.Get("path");
                    if (string.IsNullOrEmpty(path)) continue;

                    var name = foundDoc.Get("name") ?? System.IO.Path.GetFileName(path);

                    // サイズの復元（旧インデックスは持たない場合は 0）
                    var sizeStr = foundDoc.Get("size");
                    var size = long.TryParse(sizeStr, out var s) ? s : 0L;

                    // 更新日時の復元（DateTools 形式または旧 Int64 Ticks 形式に互換）
                    var modifiedStr = foundDoc.Get("modified");
                    long modifiedTicks = 0L;
                    if (!string.IsNullOrEmpty(modifiedStr))
                    {
                        if (long.TryParse(modifiedStr, out var t) && t > 100000000000000000L)
                        {
                            modifiedTicks = t;
                        }
                        else
                        {
                            try { modifiedTicks = DateTools.StringToDate(modifiedStr).Ticks; } catch { }
                        }
                    }

                    // is_dir: 新インデックスは持つ。旧インデックスは持たない（後方互換）
                    bool isDir;
                    var isDirStr = foundDoc.Get("is_dir");
                    if (!string.IsNullOrEmpty(isDirStr) && int.TryParse(isDirStr, out var isDirVal))
                        isDir = isDirVal != 0;
                    else
                        isDir = System.IO.Directory.Exists(path);

                    // ファイルで拡張子がない場合は除外（旧インデックスや万が一の漏れ対策）
                    if (!isDir && !Path.HasExtension(path))
                        continue;

                    results.Add(new SearchHit(path, name, modifiedTicks, size, isDir));
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] Search error: {ex.Message}");
                return new List<SearchHit>();
            }
        }

        /// <summary>
        /// 複数ルートパスを対象にインデックスからファイルを検索します。
        /// rootPaths が null/空の場合は全件検索、1件の場合は単一パス委譲、2件以上の場合は BooleanQuery(SHOULD) で複数 PrefixQuery を結合します。
        /// </summary>
        public List<SearchHit> Search(string searchContext, IReadOnlyList<string>? rootPaths, int maxResultsOverride = 0, SearchFilter? filter = null)
        {
            if (rootPaths == null || rootPaths.Count == 0)
                return Search(searchContext, (string?)null, maxResultsOverride, filter);
            if (rootPaths.Count == 1)
                return Search(searchContext, rootPaths[0], maxResultsOverride, filter);

            // キーワードが空の場合は全件検索扱い
            if (string.IsNullOrWhiteSpace(searchContext)) return new List<SearchHit>();

            try
            {
                using var dir = FSDirectory.Open(_indexPath);
                if (!DirectoryReader.IndexExists(dir)) return new List<SearchHit>();

                using var reader = DirectoryReader.Open(dir);
                var searcher = new IndexSearcher(reader);
                using var analyzer = new JapaneseAnalyzer(AppLuceneVersion);

                // メインクエリ構築
                var parser = new QueryParser(AppLuceneVersion, "name", analyzer);
                parser.AllowLeadingWildcard = true;
                parser.DefaultOperator = Operator.AND;

                Query parsedQuery;
                try { parsedQuery = parser.Parse(searchContext); }
                catch (ParseException) { parsedQuery = BuildQueryFromTokenized(analyzer, "name", searchContext); }

                var escaped = QueryParser.Escape(searchContext).ToLowerInvariant();
                var wildcardOnRaw = new WildcardQuery(new Term("name_raw", "*" + escaped + "*"));
                var hybrid = new BooleanQuery();
                hybrid.Add(parsedQuery, Occur.SHOULD);
                hybrid.Add(wildcardOnRaw, Occur.SHOULD);

                // パスフィルタ構築（複数 PrefixQuery を SHOULD で結合）
                var pathFilter = BuildPathScopeFilter(rootPaths);
                Query query = ApplySearchFilter(ApplyPathFilter(hybrid, pathFilter), filter);

                int maxResults = maxResultsOverride > 0 ? maxResultsOverride : 500;
                var sort = new Sort(new SortField("modified", SortFieldType.STRING, true));
                var hits = searcher.Search(query, maxResults, sort).ScoreDocs;

                // AND で 0 件 → OR フォールバック
                if (hits.Length == 0 && searchContext.Trim().Contains(' '))
                {
                    var orParser = new QueryParser(AppLuceneVersion, "name", analyzer);
                    orParser.AllowLeadingWildcard = true;
                    orParser.DefaultOperator = Operator.OR;
                    try
                    {
                        var orParsed = orParser.Parse(searchContext);
                        var orWild = new WildcardQuery(new Term("name_raw", "*" + escaped + "*"));
                        var orHybrid = new BooleanQuery();
                        orHybrid.Add(orParsed, Occur.SHOULD);
                        orHybrid.Add(orWild, Occur.SHOULD);
                        var orQuery = ApplySearchFilter(ApplyPathFilter(orHybrid, pathFilter), filter);
                        hits = searcher.Search(orQuery, maxResults, sort).ScoreDocs;
                    }
                    catch { /* OR フォールバック失敗は無視 */ }
                }

                var results = new List<SearchHit>();
                foreach (var hit in hits)
                {
                    var foundDoc = searcher.Doc(hit.Doc);
                    var path = foundDoc.Get("path");
                    if (string.IsNullOrEmpty(path)) continue;

                    var name = foundDoc.Get("name") ?? System.IO.Path.GetFileName(path);
                    var sizeStr = foundDoc.Get("size");
                    var size = long.TryParse(sizeStr, out var s) ? s : 0L;

                    var modifiedStr = foundDoc.Get("modified");
                    long modifiedTicks = 0L;
                    if (!string.IsNullOrEmpty(modifiedStr))
                    {
                        if (long.TryParse(modifiedStr, out var t) && t > 100000000000000000L)
                            modifiedTicks = t;
                        else
                        {
                            try { modifiedTicks = DateTools.StringToDate(modifiedStr).Ticks; } catch { }
                        }
                    }

                    bool isDir;
                    var isDirStr = foundDoc.Get("is_dir");
                    if (!string.IsNullOrEmpty(isDirStr) && int.TryParse(isDirStr, out var isDirVal))
                        isDir = isDirVal != 0;
                    else
                        isDir = System.IO.Directory.Exists(path);

                    if (!isDir && !Path.HasExtension(path)) continue;

                    results.Add(new SearchHit(path, name, modifiedTicks, size, isDir));
                }
                return results;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[IndexService] Search(multi-path) error: {ex.Message}");
                return new List<SearchHit>();
            }
        }

        /// <summary>複数ルートパスの PrefixQuery を SHOULD で結合した BooleanQuery を生成する。</summary>
        private static BooleanQuery BuildPathScopeFilter(IReadOnlyList<string> rootPaths)
        {
            var filter = new BooleanQuery();
            foreach (var rp in rootPaths)
            {
                var normalized = rp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var prefix = normalized + Path.DirectorySeparatorChar;
                filter.Add(new PrefixQuery(new Term("path", prefix)), Occur.SHOULD);
            }
            return filter;
        }

        /// <summary>メインクエリとパスフィルタを MUST で合成する。</summary>
        private static BooleanQuery ApplyPathFilter(Query mainQuery, BooleanQuery pathFilter)
        {
            var combined = new BooleanQuery();
            combined.Add(mainQuery, Occur.MUST);
            combined.Add(pathFilter, Occur.MUST);
            return combined;
        }

        /// <summary>サイズ・日付フィルタを MUST で合成する。filter が null なら元のクエリをそのまま返す。</summary>
        private static Query ApplySearchFilter(Query mainQuery, SearchFilter? filter)
        {
            if (filter == null) return mainQuery;
            bool hasSizeFilter = filter.SizeRange != null;
            bool hasDateFilter = filter.DateRange != null;
            if (!hasSizeFilter && !hasDateFilter) return mainQuery;

            var combined = new BooleanQuery();
            combined.Add(mainQuery, Occur.MUST);

            if (filter.SizeRange is var (minSize, maxSize))
            {
                var sizeQuery = NumericRangeQuery.NewInt64Range("size", minSize, maxSize, true, true);
                combined.Add(sizeQuery, Occur.MUST);
            }

            if (filter.DateRange is var (startDate, endDate))
            {
                var minStr = DateTools.DateToString(startDate, DateResolution.SECOND);
                var maxStr = DateTools.DateToString(endDate, DateResolution.SECOND);
                var dateQuery = new TermRangeQuery("modified",
                    new BytesRef(System.Text.Encoding.UTF8.GetBytes(minStr)),
                    new BytesRef(System.Text.Encoding.UTF8.GetBytes(maxStr)),
                    true, true);
                combined.Add(dateQuery, Occur.MUST);
            }

            return combined;
        }

        /// <summary>
        /// インデックスを全て削除します。
        /// </summary>
        public void ClearIndex()
        {
             InitializeWriter();
             lock (_lockObj)
             {
                 _writer?.DeleteAll();
                 _writer?.Commit();
                 _indexedDirectories.Clear();
                 _indexedRoots.Clear();
             }
             SaveIndexedRoots();
        }

        public void Dispose()
        {
            _intervalCts?.Cancel();
            _intervalCts?.Dispose();
            _intervalCts = null;
            _globalIndexingCts.Cancel();
            _globalIndexingCts.Dispose();
            _indexingSemaphore.Dispose();
            lock (_lockObj)
            {
                _writer?.Dispose(); // Writerを閉じるとロックファイルも解放される
                _writer = null;
                
                _analyzer?.Dispose();
                _directory?.Dispose();
            }
        }
    }
}
