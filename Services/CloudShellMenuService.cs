using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;
using ZenithFiler.Helpers;

namespace ZenithFiler.Services
{
    /// <summary>クラウドシェル拡張メニュー項目のデータモデル。</summary>
    public sealed class CloudMenuItem
    {
        public string Text { get; init; } = "";
        public ImageSource? Icon { get; init; }
        public string Verb { get; init; } = "";
        public int CommandId { get; init; }
        public string[] FilePaths { get; init; } = Array.Empty<string>();
        public bool IsBackground { get; init; }
        /// <summary>サブメニュー項目。null または空の場合は末端項目。</summary>
        public List<CloudMenuItem>? Children { get; init; }
        public bool HasChildren => Children != null && Children.Count > 0;
    }

    /// <summary>
    /// Box / SharePoint 等のクラウドシェル拡張メニュー項目を抽出し、独自メニューに統合する。
    /// </summary>
    public static class CloudShellMenuService
    {
        private sealed class CacheEntry
        {
            public List<CloudMenuItem> Items { get; init; } = new();
            public DateTime CachedAt { get; init; }
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private const int MaxCacheEntries = 20;

        private static string MakeCacheKey(string[] filePaths, bool isBackground)
        {
            if (isBackground)
                return $"bg:{filePaths[0]}";
            string? parent = Path.GetDirectoryName(filePaths[0]);
            return $"fg:{parent ?? filePaths[0]}";
        }

        /// <summary>キャッシュからクラウドメニュー項目を取得する。TTL 内であれば true を返す。</summary>
        public static bool TryGetCached(string[] filePaths, bool isBackground, out List<CloudMenuItem> items)
        {
            items = new List<CloudMenuItem>();
            if (filePaths == null || filePaths.Length == 0) return false;
            string key = MakeCacheKey(filePaths, isBackground);
            if (_cache.TryGetValue(key, out var entry) && (DateTime.UtcNow - entry.CachedAt) < CacheTtl)
            {
                items = entry.Items;
                return true;
            }
            return false;
        }

        /// <summary>フォルダ移動時にバックグラウンドでクラウドメニュー項目をプリフェッチしキャッシュを温める。</summary>
        public static void PrefetchInBackground(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            string key = MakeCacheKey(new[] { folderPath }, true);
            if (_cache.TryGetValue(key, out var entry) && (DateTime.UtcNow - entry.CachedAt) < CacheTtl)
                return;
            _ = ExtractCloudMenuItemsAsync(new[] { folderPath }, isBackground: true, timeoutMs: 5000);
        }

        /// <summary>キャッシュを全クリアする。</summary>
        public static void InvalidateCache() => _cache.Clear();

        /// <summary>
        /// クラウドメニュー項目リストから「リンクをコピー」/「リンクのコピー」/「Copy link」を再帰検索する。
        /// </summary>
        public static CloudMenuItem? FindCopyLinkItem(List<CloudMenuItem> items, SourceType sourceType)
        {
            foreach (var item in items)
            {
                if (IsCopyLinkText(item.Text, sourceType)) return item;
                if (item.HasChildren)
                    foreach (var child in item.Children!)
                        if (IsCopyLinkText(child.Text, sourceType)) return child;
            }
            return null;
        }

        private static bool IsCopyLinkText(string text, SourceType sourceType)
        {
            if (text.Contains("Copy link", StringComparison.OrdinalIgnoreCase)) return true;
            return sourceType == SourceType.Box
                ? text.Contains("リンクをコピー")      // Box は「を」
                : text.Contains("リンクのコピー");      // OneDrive は「の」
        }

        private static readonly string[] FilterKeywords =
        {
            // Box
            "Box",
            // OneDrive / SharePoint 共通
            "共有", "Share",
            "リンクのコピー", "Copy link",
            "アクセス許可の管理", "Manage access",
            "オンラインで表示", "View online",
            "バージョン履歴", "Version history", "Version",
            "OneDrive", "SharePoint", "同期",
        };

        /// <summary>
        /// 指定パスの IContextMenu からクラウド関連メニュー項目を抽出する。
        /// STA スレッドで COM 操作を行い、結果を返す。
        /// </summary>
        public static async Task<List<CloudMenuItem>> ExtractCloudMenuItemsAsync(
            string[] filePaths, bool isBackground, int timeoutMs = 3000,
            CancellationToken cancellationToken = default)
        {
            if (filePaths == null || filePaths.Length == 0)
                return new List<CloudMenuItem>();

            var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var tcs = new TaskCompletionSource<List<CloudMenuItem>>();

            var thread = new Thread(() =>
            {
                try
                {
                    var items = ExtractMenuItemsOnSta(filePaths, isBackground);
                    // キャッシュに書き込み
                    if (items.Count > 0)
                    {
                        string key = MakeCacheKey(filePaths, isBackground);
                        _cache[key] = new CacheEntry { Items = items, CachedAt = DateTime.UtcNow };
                        // MaxCacheEntries 超過時は最古エントリを削除
                        while (_cache.Count > MaxCacheEntries)
                        {
                            var oldest = _cache.OrderBy(kv => kv.Value.CachedAt).FirstOrDefault();
                            if (oldest.Key != null)
                                _cache.TryRemove(oldest.Key, out _);
                            else
                                break;
                        }
                    }
                    tcs.TrySetResult(items);
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync($"[CloudShellMenuService] 抽出エラー: {ex.Message}");
                    tcs.TrySetResult(new List<CloudMenuItem>());
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            // タイムアウト or メニュー閉鎖キャンセル
            using (linkedCts.Token.Register(() => tcs.TrySetResult(new List<CloudMenuItem>())))
            {
                var result = await tcs.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
        }

        /// <summary>
        /// クラウドメニュー項目を実行する。新しい STA スレッドで IContextMenu を再構築して InvokeCommand する。
        /// </summary>
        public static void InvokeCloudMenuCommand(CloudMenuItem item, Point screenPoint)
        {
            if (item == null) return;

            var thread = new Thread(() =>
            {
                try
                {
                    InvokeOnSta(item, screenPoint);
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync($"[CloudShellMenuService] InvokeCommand エラー: {ex.Message}");
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private static List<CloudMenuItem> ExtractMenuItemsOnSta(string[] filePaths, bool isBackground)
        {
            var results = new List<CloudMenuItem>();
            HMENU hMenu = HMENU.NULL;
            IContextMenu? contextMenu = null;

            var parameters = new HwndSourceParameters("ZenithFilerCloudExtract")
            {
                Width = 0, Height = 0, WindowStyle = 0
            };
            using var source = new HwndSource(parameters);
            if (source.Handle == IntPtr.Zero) return results;
            HWND hwnd = (HWND)source.Handle;

            try
            {
                contextMenu = BuildContextMenu(filePaths, isBackground, hwnd);
                if (contextMenu == null) return results;

                hMenu = CreatePopupMenu();
                if (hMenu.IsNull) return results;

                CMF flags = CMF.CMF_NORMAL | CMF.CMF_EXPLORE;
                contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, flags);

                // メニュー項目を列挙してフィルタ
                EnumerateMenuItems(hMenu, filePaths, isBackground, results, depth: 0);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[CloudShellMenuService] ExtractMenuItemsOnSta エラー: {ex.Message}");
            }
            finally
            {
                if (!hMenu.IsNull) DestroyMenu(hMenu);
                if (contextMenu != null) Marshal.ReleaseComObject(contextMenu);
            }

            return results;
        }

        // Win32 MENUITEMINFO fMask 定数
        private const uint MIIM_STRING = 0x00000040;
        private const uint MIIM_BITMAP = 0x00000080;
        private const uint MIIM_ID     = 0x00000002;
        private const uint MIIM_SUBMENU = 0x00000004;

        private static void EnumerateMenuItems(HMENU hMenu, string[] filePaths, bool isBackground,
            List<CloudMenuItem> results, int depth)
        {
            // 再帰は 3 階層まで（十分な深さ）
            if (depth > 3) return;

            IntPtr hMenuPtr = hMenu.DangerousGetHandle();
            int count = NativeMethods.GetMenuItemCount(hMenuPtr);
            for (int i = 0; i < count; i++)
            {
                var mii = new MenuItemInfoNative
                {
                    cbSize = (uint)Marshal.SizeOf<MenuItemInfoNative>(),
                    fMask = MIIM_STRING | MIIM_BITMAP | MIIM_ID | MIIM_SUBMENU,
                };

                // まず文字列長を取得
                NativeMethods.GetMenuItemInfoW(hMenuPtr, (uint)i, true, ref mii);

                IntPtr hSub = mii.hSubMenu;

                if (mii.cch == 0)
                {
                    // テキストなし → サブメニューがあれば中を探索
                    if (hSub != IntPtr.Zero)
                        EnumerateMenuItems((HMENU)hSub, filePaths, isBackground, results, depth + 1);
                    continue;
                }

                // 文字列バッファを確保して再取得
                mii.cch++;
                mii.dwTypeData = Marshal.AllocHGlobal((int)(mii.cch * 2));
                try
                {
                    mii.fMask = MIIM_STRING | MIIM_BITMAP | MIIM_ID | MIIM_SUBMENU;
                    NativeMethods.GetMenuItemInfoW(hMenuPtr, (uint)i, true, ref mii);
                    string text = Marshal.PtrToStringUni(mii.dwTypeData) ?? "";
                    hSub = mii.hSubMenu;

                    // キーワードフィルタ
                    if (!MatchesFilter(text))
                    {
                        // マッチしない → サブメニューがあれば中を探索（子にマッチする項目があるかも）
                        if (hSub != IntPtr.Zero)
                            EnumerateMenuItems((HMENU)hSub, filePaths, isBackground, results, depth + 1);
                        continue;
                    }

                    // アイコン取得
                    ImageSource? icon = ExtractIcon(mii.hbmpItem);

                    // この項目がサブメニューを持つ場合、子項目を全て収集する
                    List<CloudMenuItem>? children = null;
                    if (hSub != IntPtr.Zero)
                    {
                        children = new List<CloudMenuItem>();
                        CollectAllMenuItems((HMENU)hSub, filePaths, isBackground, children);
                    }

                    results.Add(new CloudMenuItem
                    {
                        Text = text.Replace("&", ""), // アクセラレータキー除去
                        Icon = icon,
                        CommandId = (int)mii.wID,
                        FilePaths = filePaths,
                        IsBackground = isBackground,
                        Children = children,
                    });
                }
                finally
                {
                    Marshal.FreeHGlobal(mii.dwTypeData);
                }
            }
        }

        /// <summary>サブメニュー内の全項目をフィルタなしで収集する（1階層のみ）。</summary>
        private static void CollectAllMenuItems(HMENU hMenu, string[] filePaths, bool isBackground,
            List<CloudMenuItem> results)
        {
            IntPtr hMenuPtr = hMenu.DangerousGetHandle();
            int count = NativeMethods.GetMenuItemCount(hMenuPtr);
            for (int i = 0; i < count; i++)
            {
                var mii = new MenuItemInfoNative
                {
                    cbSize = (uint)Marshal.SizeOf<MenuItemInfoNative>(),
                    fMask = MIIM_STRING | MIIM_BITMAP | MIIM_ID | MIIM_SUBMENU,
                };

                NativeMethods.GetMenuItemInfoW(hMenuPtr, (uint)i, true, ref mii);
                if (mii.cch == 0) continue; // セパレータ等はスキップ

                mii.cch++;
                mii.dwTypeData = Marshal.AllocHGlobal((int)(mii.cch * 2));
                try
                {
                    mii.fMask = MIIM_STRING | MIIM_BITMAP | MIIM_ID | MIIM_SUBMENU;
                    NativeMethods.GetMenuItemInfoW(hMenuPtr, (uint)i, true, ref mii);
                    string text = Marshal.PtrToStringUni(mii.dwTypeData) ?? "";
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    ImageSource? icon = ExtractIcon(mii.hbmpItem);

                    // さらにサブメニューがあれば子項目も収集
                    List<CloudMenuItem>? children = null;
                    if (mii.hSubMenu != IntPtr.Zero)
                    {
                        children = new List<CloudMenuItem>();
                        CollectAllMenuItems((HMENU)mii.hSubMenu, filePaths, isBackground, children);
                    }

                    results.Add(new CloudMenuItem
                    {
                        Text = text.Replace("&", ""),
                        Icon = icon,
                        CommandId = (int)mii.wID,
                        FilePaths = filePaths,
                        IsBackground = isBackground,
                        Children = children,
                    });
                }
                finally
                {
                    Marshal.FreeHGlobal(mii.dwTypeData);
                }
            }
        }

        private static ImageSource? ExtractIcon(IntPtr hbmpItem)
        {
            if (hbmpItem == IntPtr.Zero) return null;
            try
            {
                var bmp = Imaging.CreateBitmapSourceFromHBitmap(
                    hbmpItem, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesFilter(string text)
        {
            foreach (var kw in FilterKeywords)
            {
                if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void InvokeOnSta(CloudMenuItem item, Point screenPoint)
        {
            HMENU hMenu = HMENU.NULL;
            IContextMenu? contextMenu = null;

            var parameters = new HwndSourceParameters("ZenithFilerCloudInvoke")
            {
                Width = 0, Height = 0, WindowStyle = 0
            };
            using var source = new HwndSource(parameters);
            if (source.Handle == IntPtr.Zero) return;
            HWND hwnd = (HWND)source.Handle;

            try
            {
                contextMenu = BuildContextMenu(item.FilePaths, item.IsBackground, hwnd);
                if (contextMenu == null) return;

                hMenu = CreatePopupMenu();
                if (hMenu.IsNull) return;

                CMF flags = CMF.CMF_NORMAL | CMF.CMF_EXPLORE;
                contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, flags);

                // InvokeCommand
                int offset = item.CommandId - 1;
                var ci = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                    fMask = CMIC.CMIC_MASK_UNICODE,
                    hwnd = hwnd,
                    lpVerb = new SafeResourceId(offset),
                    lpVerbW = new SafeResourceId(offset),
                    nShow = ShowWindowCommand.SW_SHOWNORMAL,
                    ptInvoke = new POINT((int)screenPoint.X, (int)screenPoint.Y),
                };
                contextMenu.InvokeCommand(ci);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[CloudShellMenuService] InvokeOnSta エラー: {ex.Message}");
            }
            finally
            {
                if (!hMenu.IsNull) DestroyMenu(hMenu);
                if (contextMenu != null) Marshal.ReleaseComObject(contextMenu);
            }
        }

        private static IContextMenu? BuildContextMenu(string[] filePaths, bool isBackground, HWND hwnd)
        {
            try
            {
                if (isBackground)
                {
                    string physicalPath = PathHelper.GetPhysicalPath(filePaths[0]);
                    using var folder = new ShellFolder(physicalPath);
                    return folder.GetViewObject<IContextMenu>(hwnd);
                }
                else
                {
                    var physicalPaths = filePaths.Select(p => PathHelper.GetPhysicalPath(p)).ToArray();
                    string? parentDir = Path.GetDirectoryName(physicalPaths[0]);
                    if (parentDir != null)
                    {
                        using var parentFolder = new ShellFolder(parentDir);
                        var items = physicalPaths.Select(p => new ShellItem(p)).ToArray();
                        try
                        {
                            return parentFolder.GetChildrenUIObjects<IContextMenu>(hwnd, items);
                        }
                        finally
                        {
                            foreach (var item in items) item?.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[CloudShellMenuService] BuildContextMenu エラー: {ex.Message}");
            }
            return null;
        }
    }
}
