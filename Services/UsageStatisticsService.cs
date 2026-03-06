using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZenithFiler.Services
{
    public sealed class UsageStatisticsService
    {
        public static readonly string[] AllActionKeys =
        {
            "Search.Normal", "Search.Index",
            "File.Rename", "File.Copy", "File.Move", "File.Delete",
            "Tab.Open", "Tab.Close",
            "Theme.Change",
            "Nav.OpenFolder", "Nav.SwitchPane",
            "Preview.QuickLook",
            "Favorites.Add", "Favorites.Remove",
            "WorkingSet.Apply",
            "Backup.Manual",
            "Index.UpdateNow",
            "Export.Csv", "Export.Excel",
            "DragDrop.Drop",
            "ContextMenu.Open"
        };

        /// <summary>アクションが記録された後に発火します。引数は actionKey です。</summary>
        public event Action<string>? ActionRecorded;

        public async Task RecordAsync(string actionKey)
        {
            try
            {
                var db = App.Database.Connection;
                await db.RunInTransactionAsync(conn =>
                {
                    var existing = conn.Find<ActionStat>(actionKey);
                    if (existing != null)
                    {
                        existing.Count++;
                        existing.LastUsedAt = DateTime.UtcNow.ToString("o");
                        conn.Update(existing);
                    }
                    else
                    {
                        conn.Insert(new ActionStat
                        {
                            ActionKey = actionKey,
                            Count = 1,
                            LastUsedAt = DateTime.UtcNow.ToString("o")
                        });
                    }
                }).ConfigureAwait(false);
                ActionRecorded?.Invoke(actionKey);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[UsageStats] RecordAsync failed: {ex.Message}");
            }
        }

        public async Task<List<ActionStat>> GetAllAsync()
        {
            try
            {
                var db = App.Database.Connection;
                var items = await db.Table<ActionStat>().ToListAsync().ConfigureAwait(false);
                return items.OrderByDescending(x => x.Count).ToList();
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[UsageStats] GetAllAsync failed: {ex.Message}");
                return new List<ActionStat>();
            }
        }

        public async Task ResetAsync()
        {
            try
            {
                var db = App.Database.Connection;
                await db.ExecuteAsync("DELETE FROM ActionStats").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[UsageStats] ResetAsync failed: {ex.Message}");
            }
        }

        public static string GetDisplayName(string actionKey) => actionKey switch
        {
            "Search.Normal" => "通常検索",
            "Search.Index" => "インデックス検索",
            "File.Rename" => "リネーム",
            "File.Copy" => "コピー",
            "File.Move" => "移動",
            "File.Delete" => "削除",
            "Tab.Open" => "タブを開く",
            "Tab.Close" => "タブを閉じる",
            "Theme.Change" => "テーマ変更",
            "Nav.OpenFolder" => "フォルダを開く",
            "Nav.SwitchPane" => "ペイン切替",
            "Preview.QuickLook" => "クイックプレビュー",
            "Favorites.Add" => "お気に入り追加",
            "Favorites.Remove" => "お気に入り削除",
            "WorkingSet.Apply" => "ワーキングセット適用",
            "Backup.Manual" => "バックアップ実行",
            "Index.UpdateNow" => "インデックス更新",
            "Export.Csv" => "CSVエクスポート",
            "Export.Excel" => "Excelエクスポート",
            "DragDrop.Drop" => "ドラッグ＆ドロップ",
            "ContextMenu.Open" => "コンテキストメニュー",
            _ => actionKey
        };

        public static string GetTriggerDescription(string actionKey) => actionKey switch
        {
            "Search.Normal" => "通常検索を実行",
            "Search.Index" => "インデックス検索を実行",
            "File.Rename" => "ファイル/フォルダをリネーム",
            "File.Copy" => "コピー操作（クリップボード/D&D）",
            "File.Move" => "移動操作（カット＆ペースト/D&D）",
            "File.Delete" => "ファイル/フォルダを削除",
            "Tab.Open" => "新しいタブを開く",
            "Tab.Close" => "タブを閉じる",
            "Theme.Change" => "テーマを切り替え",
            "Nav.OpenFolder" => "フォルダへ移動",
            "Nav.SwitchPane" => "ペイン間でフォーカスを切替",
            "Preview.QuickLook" => "クイックプレビューを表示",
            "Favorites.Add" => "お気に入りに追加",
            "Favorites.Remove" => "お気に入りから解除",
            "WorkingSet.Apply" => "ワーキングセットを適用",
            "Backup.Manual" => "手動バックアップを実行",
            "Index.UpdateNow" => "インデックスを手動更新",
            "Export.Csv" => "CSV ファイルへエクスポート",
            "Export.Excel" => "Excel ファイルへエクスポート",
            "DragDrop.Drop" => "ファイルをドラッグ＆ドロップ",
            "ContextMenu.Open" => "右クリックメニューを表示",
            _ => ""
        };
    }
}
