using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ZenithFiler
{
    /// <summary>
    /// SQLite データベースへのアクセスを提供するサービスです。
    /// 履歴ビュー表示時または初回の履歴保存時に遅延初期化されます。
    /// </summary>
    public class DatabaseService
    {
        public event EventHandler? HistoryChanged;
        public event EventHandler? SearchHistoryChanged;
        private readonly SQLiteAsyncConnection _db;
        private readonly string _databasePath;
        private Task? _initTask;
        private readonly object _initLock = new();

        private static string GetDatabasePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index", "zenith.db");
        }

        public DatabaseService()
        {
            _databasePath = GetDatabasePath();
            _db = new SQLiteAsyncConnection(_databasePath);
        }

        /// <summary>
        /// データベース接続を取得します。
        /// </summary>
        public SQLiteAsyncConnection Connection => _db;

        /// <summary>
        /// 初回使用時に呼び出され、テーブル作成などを行います。以降の呼び出しは同一タスクを返します。
        /// </summary>
        private Task EnsureInitializedAsync()
        {
            lock (_initLock)
            {
                if (_initTask == null || _initTask.IsFaulted)
                    _initTask = DoInitializeAsync();
                return _initTask;
            }
        }

        /// <summary>
        /// データベースと必要なテーブルを初期化します。
        /// </summary>
        public async Task InitializeAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
        }

        private async Task DoInitializeAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_databasePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] Failed to create database directory: {ex.Message}");
            }

            try
            {
                await _db.CreateTableAsync<HistoryRecord>().ConfigureAwait(false);
                await _db.CreateTableAsync<SearchHistoryRecord>().ConfigureAwait(false);
                await _db.CreateTableAsync<RenameHistory>().ConfigureAwait(false);
                await _db.CreateTableAsync<CustomRenameButton>().ConfigureAwait(false);
                await MigrateSearchHistoryTableIfNeededAsync().ConfigureAwait(false);
                await MigrateSearchHistoryV2Async().ConfigureAwait(false);
                _ = Task.Run(CleanupHistoryAsync);
                _ = Task.Run(CleanupSearchHistoryAsync);
                _ = Task.Run(CleanupRenameHistoryAsync);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] Table initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 検索履歴テーブルが旧スキーマ（Keyword 単一 PK）の場合は新スキーマへマイグレーションします。
        /// 新スキーマかどうかは「Key 列へ書き込めるか」で判定します（SELECT のみではドライバによって例外が出ない場合があるため）。
        /// </summary>
        private async Task MigrateSearchHistoryTableIfNeededAsync()
        {
            const string testKey = "__migrate_check__";
            try
            {
                var probe = new SearchHistoryRecord
                {
                    Key = testKey,
                    Keyword = string.Empty,
                    IsIndexSearch = false,
                    LastSearched = DateTime.Now
                };
                await _db.InsertAsync(probe).ConfigureAwait(false);
                await _db.DeleteAsync(probe).ConfigureAwait(false);
                return;
            }
            catch
            {
                // 挿入失敗＝旧スキーマ（Key/IsIndexSearch 列なし）のためマイグレーション実行
            }

            try
            {
                await _db.ExecuteAsync(
                    "CREATE TABLE SearchHistoryRecord_new (Key TEXT PRIMARY KEY, Keyword TEXT NOT NULL, IsIndexSearch INTEGER NOT NULL, LastSearched DATETIME NOT NULL)").ConfigureAwait(false);
                await _db.ExecuteAsync(
                    "INSERT INTO SearchHistoryRecord_new (Key, Keyword, IsIndexSearch, LastSearched) SELECT Keyword || char(1) || '0', Keyword, 0, LastSearched FROM SearchHistoryRecord").ConfigureAwait(false);
                await _db.ExecuteAsync("DROP TABLE SearchHistoryRecord").ConfigureAwait(false);
                await _db.ExecuteAsync("ALTER TABLE SearchHistoryRecord_new RENAME TO SearchHistoryRecord").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] SearchHistory migration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 検索履歴テーブルに条件列（PresetName, MinSizeText 等）を追加する V2 マイグレーション。
        /// </summary>
        private async Task MigrateSearchHistoryV2Async()
        {
            try
            {
                await _db.ExecuteScalarAsync<string>(
                    "SELECT PresetName FROM SearchHistoryRecord LIMIT 1").ConfigureAwait(false);
                return; // 既に V2
            }
            catch { }

            try
            {
                await _db.ExecuteAsync("ALTER TABLE SearchHistoryRecord ADD COLUMN PresetName TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
                await _db.ExecuteAsync("ALTER TABLE SearchHistoryRecord ADD COLUMN MinSizeText TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
                await _db.ExecuteAsync("ALTER TABLE SearchHistoryRecord ADD COLUMN MaxSizeText TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
                await _db.ExecuteAsync("ALTER TABLE SearchHistoryRecord ADD COLUMN StartDateText TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
                await _db.ExecuteAsync("ALTER TABLE SearchHistoryRecord ADD COLUMN EndDateText TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] SearchHistory V2 migration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// フォルダ参照履歴を保存します（Upsert）。
        /// </summary>
        public async Task SaveHistoryAsync(string path, SourceType type)
        {
            if (string.IsNullOrEmpty(path)) return;
            await EnsureInitializedAsync().ConfigureAwait(false);

            try
            {
                await _db.RunInTransactionAsync(db =>
                {
                    var existing = db.Table<HistoryRecord>().Where(r => r.Path == path).FirstOrDefault();
                    if (existing != null)
                    {
                        existing.LastAccessed = DateTime.Now;
                        existing.AccessCount++;
                        db.Update(existing);
                    }
                    else
                    {
                        var record = new HistoryRecord
                        {
                            Path = path,
                            SourceType = type,
                            LastAccessed = DateTime.Now,
                            AccessCount = 1
                        };
                        db.Insert(record);
                    }
                }).ConfigureAwait(false);

                HistoryChanged?.Invoke(this, EventArgs.Empty);
                _ = Task.Run(CleanupHistoryAsync);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] SaveHistoryAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// フォルダ参照履歴を取得します（最近使用した順）。
        /// </summary>
        public async Task<List<HistoryRecord>> GetHistoryAsync(int limit = 50)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                return await _db.Table<HistoryRecord>()
                    .OrderByDescending(r => r.LastAccessed)
                    .Take(limit)
                    .ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] GetHistoryAsync failed: {ex.Message}");
                return new List<HistoryRecord>();
            }
        }

        /// <summary>
        /// 1ヶ月以上経過した履歴を削除します。
        /// </summary>
        public async Task CleanupHistoryAsync()
        {
            try
            {
                var threshold = DateTime.Now.AddMonths(-1);
                await _db.ExecuteAsync("DELETE FROM HistoryRecord WHERE LastAccessed < ?", threshold).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] CleanupHistoryAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 全ての履歴を削除します。
        /// </summary>
        public async Task ClearHistoryAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                await _db.DeleteAllAsync<HistoryRecord>().ConfigureAwait(false);
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] ClearHistoryAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 検索履歴を保存します（Upsert）。同一キーワードでも通常/インデックスを別履歴として保持します。
        /// 条件列（プリセット名・サイズ・日付フィルタ）も保存し、同一キーワード+モードの再検索では上書き更新します。
        /// </summary>
        public async Task SaveSearchHistoryAsync(string keyword, bool isIndexSearch = false,
            string? presetName = null, string? minSizeText = null, string? maxSizeText = null,
            string? startDateText = null, string? endDateText = null)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            await EnsureInitializedAsync().ConfigureAwait(false);

            await SaveSearchHistoryCoreAsync(keyword, isIndexSearch, retryAfterMigrate: true,
                presetName, minSizeText, maxSizeText, startDateText, endDateText).ConfigureAwait(false);
        }

        private async Task SaveSearchHistoryCoreAsync(string keyword, bool isIndexSearch, bool retryAfterMigrate,
            string? presetName = null, string? minSizeText = null, string? maxSizeText = null,
            string? startDateText = null, string? endDateText = null)
        {
            try
            {
                var key = SearchHistoryRecord.BuildKey(keyword, isIndexSearch);
                await _db.RunInTransactionAsync(db =>
                {
                    var existing = db.Table<SearchHistoryRecord>().Where(r => r.Key == key).FirstOrDefault();
                    if (existing != null)
                    {
                        existing.LastSearched = DateTime.Now;
                        existing.PresetName = presetName ?? string.Empty;
                        existing.MinSizeText = minSizeText ?? string.Empty;
                        existing.MaxSizeText = maxSizeText ?? string.Empty;
                        existing.StartDateText = startDateText ?? string.Empty;
                        existing.EndDateText = endDateText ?? string.Empty;
                        db.Update(existing);
                    }
                    else
                    {
                        var record = new SearchHistoryRecord
                        {
                            Key = key,
                            Keyword = keyword,
                            IsIndexSearch = isIndexSearch,
                            LastSearched = DateTime.Now,
                            PresetName = presetName ?? string.Empty,
                            MinSizeText = minSizeText ?? string.Empty,
                            MaxSizeText = maxSizeText ?? string.Empty,
                            StartDateText = startDateText ?? string.Empty,
                            EndDateText = endDateText ?? string.Empty
                        };
                        db.Insert(record);
                    }
                }).ConfigureAwait(false);

                SearchHistoryChanged?.Invoke(this, EventArgs.Empty);
                _ = Task.Run(CleanupSearchHistoryAsync);
            }
            catch (SQLiteException ex) when (retryAfterMigrate &&
                (ex.Message.Contains("no such column") || ex.Message.Contains("no such table")))
            {
                await MigrateSearchHistoryTableIfNeededAsync().ConfigureAwait(false);
                await MigrateSearchHistoryV2Async().ConfigureAwait(false);
                await SaveSearchHistoryCoreAsync(keyword, isIndexSearch, retryAfterMigrate: false,
                    presetName, minSizeText, maxSizeText, startDateText, endDateText).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] SaveSearchHistoryCoreAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 検索履歴を取得します（最近使用した順、最大100件）。通常/インデックスを区別して返します。
        /// </summary>
        public async Task<List<SearchHistoryItem>> GetSearchHistoryAsync(int limit = 100)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                var list = await _db.Table<SearchHistoryRecord>()
                    .OrderByDescending(r => r.LastSearched)
                    .Take(limit)
                    .ToListAsync().ConfigureAwait(false);
                return list.Select(r => new SearchHistoryItem
                {
                    Keyword = r.Keyword,
                    IsIndexSearch = r.IsIndexSearch,
                    PresetName = r.PresetName,
                    MinSizeText = r.MinSizeText,
                    MaxSizeText = r.MaxSizeText,
                    StartDateText = r.StartDateText,
                    EndDateText = r.EndDateText
                }).ToList();
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] GetSearchHistoryAsync failed: {ex.Message}");
                return new List<SearchHistoryItem>();
            }
        }

        /// <summary>
        /// 検索履歴が100件を超えた場合、古いものを削除します。
        /// </summary>
        public async Task CleanupSearchHistoryAsync()
        {
            try
            {
                int count = await _db.Table<SearchHistoryRecord>().CountAsync().ConfigureAwait(false);
                if (count <= 100) return;

                // 一括削除用: 古い順で count-100 件の Key を取得して IN 句で削除
                var deleteTargets = await _db.Table<SearchHistoryRecord>()
                    .OrderBy(r => r.LastSearched)
                    .Take(count - 100)
                    .ToListAsync().ConfigureAwait(false);

                if (deleteTargets.Count == 0) return;

                var keysToDelete = deleteTargets.ConvertAll(r => r.Key);
                var placeholders = string.Join(",", keysToDelete.Select(k => "?"));
                await _db.ExecuteAsync($"DELETE FROM SearchHistoryRecord WHERE Key IN ({placeholders})", keysToDelete.Cast<object>().ToArray()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] CleanupSearchHistoryAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 全ての検索履歴を削除します。
        /// </summary>
        public async Task ClearSearchHistoryAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                await _db.DeleteAllAsync<SearchHistoryRecord>().ConfigureAwait(false);
                SearchHistoryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] ClearSearchHistoryAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// リネーム履歴を保存します（Upsert）。既存なら LastUsed を更新、新規なら Insert。
        /// </summary>
        public async Task SaveRenameHistoryAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            await EnsureInitializedAsync().ConfigureAwait(false);

            try
            {
                await _db.RunInTransactionAsync(db =>
                {
                    var existing = db.Table<RenameHistory>().Where(r => r.Name == name).FirstOrDefault();
                    if (existing != null)
                    {
                        existing.LastUsed = DateTime.Now;
                        db.Update(existing);
                    }
                    else
                    {
                        var record = new RenameHistory
                        {
                            Name = name,
                            LastUsed = DateTime.Now
                        };
                        db.Insert(record);
                    }
                }).ConfigureAwait(false);

                _ = Task.Run(CleanupRenameHistoryAsync);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] SaveRenameHistoryAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// リネーム履歴を取得します（最近使用した順、最大100件）。名前の文字列リストを返します。
        /// </summary>
        public async Task<List<string>> GetRenameHistoryAsync(int limit = 100)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                var list = await _db.Table<RenameHistory>()
                    .OrderByDescending(r => r.LastUsed)
                    .Take(limit)
                    .ToListAsync().ConfigureAwait(false);
                return list.Select(r => r.Name).ToList();
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] GetRenameHistoryAsync failed: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// リネーム履歴が100件を超えた場合、古いものを削除します。
        /// </summary>
        public async Task CleanupRenameHistoryAsync()
        {
            try
            {
                int count = await _db.Table<RenameHistory>().CountAsync().ConfigureAwait(false);
                if (count <= 100) return;

                var deleteTargets = await _db.Table<RenameHistory>()
                    .OrderBy(r => r.LastUsed)
                    .Take(count - 100)
                    .ToListAsync().ConfigureAwait(false);

                if (deleteTargets.Count == 0) return;

                var namesToDelete = deleteTargets.ConvertAll(r => r.Name);
                var placeholders = string.Join(",", namesToDelete.Select(k => "?"));
                await _db.ExecuteAsync($"DELETE FROM RenameHistory WHERE Name IN ({placeholders})", namesToDelete.Cast<object>().ToArray()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] CleanupRenameHistoryAsync failed: {ex.Message}");
            }
        }

        // ── カスタムリネームボタン ──

        /// <summary>
        /// カスタムリネームボタンを全件取得します（作成日時順）。
        /// </summary>
        public async Task<List<CustomRenameButton>> GetCustomRenameButtonsAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                return await _db.Table<CustomRenameButton>()
                    .OrderBy(b => b.CreatedAt)
                    .ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] GetCustomRenameButtonsAsync failed: {ex.Message}");
                return new List<CustomRenameButton>();
            }
        }

        /// <summary>
        /// カスタムリネームボタンを追加します。
        /// </summary>
        public async Task<CustomRenameButton?> AddCustomRenameButtonAsync(string displayText, string insertText)
        {
            if (string.IsNullOrWhiteSpace(displayText)) return null;
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                var button = new CustomRenameButton
                {
                    DisplayText = displayText,
                    InsertText = insertText,
                    CreatedAt = DateTime.Now
                };
                await _db.InsertAsync(button).ConfigureAwait(false);
                return button;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] AddCustomRenameButtonAsync failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// カスタムリネームボタンを削除します。
        /// </summary>
        public async Task DeleteCustomRenameButtonAsync(int id)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            try
            {
                await _db.DeleteAsync<CustomRenameButton>(id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[DatabaseService] DeleteCustomRenameButtonAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// ジェネリックな取得メソッドの例
        /// </summary>
        public async Task<List<T>> GetAllAsync<T>() where T : new()
        {
            return await _db.Table<T>().ToListAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// ジェネリックな保存メソッドの例
        /// </summary>
        public async Task<int> SaveAsync<T>(T item)
        {
            return await _db.InsertOrReplaceAsync(item).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 非推奨: アプリ設定は settings.json で管理。お気に入り含む。
    /// 履歴（History）のみ SQLite を使用。
    /// </summary>
    [Obsolete("App settings including favorites are now stored in settings.json")]
    public class AppSetting
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
