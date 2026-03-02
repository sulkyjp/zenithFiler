using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ZenithFiler.Services
{
    /// <summary>バックアップ一覧の1エントリ。BackupListDialog の ItemsSource として使用。</summary>
    public record BackupEntry(
        string JsonPath,
        string DescPath,
        string LockPath,
        DateTime Timestamp,
        bool IsLocked,
        string Summary);

    public static class SettingsBackupService
    {
        private static readonly string SettingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static readonly string BackupsDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");

        // 辞書：JSON キーパス → 日本語ラベル
        // トップレベルは "PropName"、ネストは "Parent.Child" 表記
        private static readonly List<(string Path, string Label)> Labels = new()
        {
            ("LeftPane.HomePath",             "Aペインホーム"),
            ("RightPane.HomePath",            "Bペインホーム"),
            ("SearchBehavior",                "検索挙動"),
            ("SearchResultPathBehavior",      "検索結果パスクリック"),
            ("ContextMenuMode",               "コンテキストメニュー"),
            ("AutoSwitchToSinglePaneOnSearch","検索時1画面切替"),
            ("IsSidebarVisible",              "サイドバー表示"),
            ("PaneCount",                     "ペイン数"),
            ("SidebarMode",                   "サイドバーモード"),
            ("IsFavoritesLocked",             "お気に入りロック"),
            ("IsTreeViewLocked",              "ツリービューロック"),
            ("IsNavWidthLocked",              "ナビ幅ロック"),
            ("ConfirmDeleteFavorites",        "お気に入り削除確認"),
            ("FavoritesSearchMode",           "お気に入り検索モード"),
            ("Favorites",                     "お気に入り"),
            ("IndexSearchTargetPaths",        "インデックス対象フォルダ"),
            ("IndexSearchLockedPaths",        "インデックスロック済みフォルダ"),
            ("IndexSearchScopePaths",         "インデックス検索スコープ"),
            ("Index.UpdateMode",              "インデックス更新モード"),
            ("Index.UpdateIntervalHours",     "インデックス更新間隔"),
            ("Index.EcoMode",                 "インデックス省エネモード"),
            ("Index.MaxParallelism",          "インデックス並列数"),
            ("Index.NetworkLowPriority",      "ネットワーク低優先"),
            ("SearchSizeFilter",              "検索サイズフィルタ"),
            ("SearchDateFilter",              "検索日付フィルタ"),
        };

        /// <summary>
        /// 現在の settings.json をバックアップする。
        /// summaryOverride が null の場合は最新バックアップとの差分を自動生成。
        /// </summary>
        public static void CreateBackup(string? summaryOverride = null)
        {
            if (!File.Exists(SettingsPath)) return;
            try
            {
                if (!Directory.Exists(BackupsDir))
                    Directory.CreateDirectory(BackupsDir);

                string currentJson = File.ReadAllText(SettingsPath, Encoding.UTF8);
                string? prevJson = GetLastBackupJson();
                string summary = summaryOverride ?? GenerateSummary(prevJson, currentJson);

                string stem = $"settings_{DateTime.Now:yyyyMMdd_HHmmss}";
                string jsonDest = Path.Combine(BackupsDir, stem + ".json");
                string descDest = Path.Combine(BackupsDir, stem + ".desc");

                File.Copy(SettingsPath, jsonDest, overwrite: false);
                File.WriteAllText(descDest, summary, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[SettingsBackupService] CreateBackup failed: {ex.Message}");
            }
        }

        /// <summary>30日より古く、かつ .lock ファイルが存在しないバックアップを削除する。</summary>
        public static void CleanupOldBackups(int retentionDays = 30)
        {
            if (!Directory.Exists(BackupsDir)) return;
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            try
            {
                foreach (var jsonFile in Directory.GetFiles(BackupsDir, "settings_*.json"))
                {
                    string lockFile = Path.ChangeExtension(jsonFile, ".lock");
                    if (File.Exists(lockFile)) continue; // ロック中はスキップ
                    if (File.GetCreationTime(jsonFile) < cutoff)
                    {
                        File.Delete(jsonFile);
                        string descFile = Path.ChangeExtension(jsonFile, ".desc");
                        if (File.Exists(descFile)) File.Delete(descFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[SettingsBackupService] Cleanup failed: {ex.Message}");
            }
        }

        /// <summary>非同期でバックアップを作成する（UI スレッドをブロックしない）。</summary>
        public static Task CreateBackupAsync(string? summaryOverride = null)
            => Task.Run(() => CreateBackup(summaryOverride));

        /// <summary>非同期で古いバックアップを削除する（UI スレッドをブロックしない）。</summary>
        public static Task CleanupOldBackupsAsync(int retentionDays = 30)
            => Task.Run(() => CleanupOldBackups(retentionDays));

        /// <summary>バックアップ一覧を新しい順で返す。</summary>
        public static List<BackupEntry> GetBackups()
        {
            if (!Directory.Exists(BackupsDir)) return new();
            var result = new List<BackupEntry>();
            foreach (var jsonFile in Directory.GetFiles(BackupsDir, "settings_*.json")
                                               .OrderByDescending(f => f))
            {
                string descFile = Path.ChangeExtension(jsonFile, ".desc");
                string lockFile = Path.ChangeExtension(jsonFile, ".lock");
                bool isLocked   = File.Exists(lockFile);
                string summary  = File.Exists(descFile)
                    ? File.ReadAllText(descFile, Encoding.UTF8)
                    : string.Empty;
                var ts = ParseTimestamp(Path.GetFileNameWithoutExtension(jsonFile));
                result.Add(new BackupEntry(jsonFile, descFile, lockFile, ts, isLocked, summary));
            }
            return result;
        }

        /// <summary>指定バックアップを settings.json に上書きコピーする。</summary>
        public static void Restore(string jsonPath)
        {
            try
            {
                File.Copy(jsonPath, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[SettingsBackupService] Restore failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>バックアップのロック状態を切り替える。</summary>
        public static void SetLock(string jsonPath, bool locked)
        {
            try
            {
                string lockFile = Path.ChangeExtension(jsonPath, ".lock");
                if (locked)
                    File.WriteAllText(lockFile, string.Empty);
                else if (File.Exists(lockFile))
                    File.Delete(lockFile);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[SettingsBackupService] SetLock failed: {ex.Message}");
                throw;
            }
        }

        private static string? GetLastBackupJson()
        {
            if (!Directory.Exists(BackupsDir)) return null;
            var latest = Directory.GetFiles(BackupsDir, "settings_*.json")
                                  .OrderByDescending(f => f)
                                  .FirstOrDefault();
            return latest != null ? File.ReadAllText(latest, Encoding.UTF8) : null;
        }

        private static string GenerateSummary(string? prevJson, string currentJson)
        {
            if (prevJson == null) return "初回バックアップ";
            try
            {
                using var prev = JsonDocument.Parse(prevJson);
                using var curr = JsonDocument.Parse(currentJson);
                var changes = new List<string>();
                foreach (var (path, label) in Labels)
                {
                    string prevVal = GetValue(prev.RootElement, path);
                    string currVal = GetValue(curr.RootElement, path);
                    if (prevVal != currVal) changes.Add(label);
                }
                return changes.Count > 0 ? string.Join("、", changes) : "変更なし";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>"Parent.Child" 形式のパスで JsonElement の値を文字列として取得する。</summary>
        private static string GetValue(JsonElement root, string dotPath)
        {
            var parts = dotPath.Split('.');
            var elem = root;
            foreach (var part in parts)
            {
                if (!elem.TryGetProperty(part, out elem))
                    return string.Empty;
            }
            return elem.GetRawText();
        }

        private static DateTime ParseTimestamp(string stem)
        {
            // stem 例: "settings_20260221_153045"
            var s = stem.Replace("settings_", "");
            return DateTime.TryParseExact(s, "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt)
                ? dt : DateTime.MinValue;
        }
    }
}
