using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ZenithFiler.Helpers;

namespace ZenithFiler.Services
{
    /// <summary>ShellNew レジストリエントリのデータモデル。</summary>
    public sealed class ShellNewItem
    {
        public string DisplayName { get; init; } = "";
        public string Extension { get; init; } = "";
        public ImageSource? Icon { get; init; }
        public string? TemplateFilePath { get; init; }
        public byte[]? TemplateData { get; init; }
        public bool IsNullFile { get; init; }
    }

    /// <summary>
    /// HKCR の ShellNew エントリをスキャンしてキャッシュし、
    /// コンテキストメニューの「新規作成」サブメニュー用に提供する。
    /// </summary>
    public static class ShellNewService
    {
        private static readonly object _lock = new();
        private static List<ShellNewItem>? _cachedItems;
        private static Task? _initTask;

        // アプリ側で独自処理済み or 不要な拡張子
        private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".lnk", ".contact", ".library-ms", ".rtf"
        };

        /// <summary>バックグラウンドでレジストリスキャンを開始する。</summary>
        public static Task InitializeAsync()
        {
            lock (_lock)
            {
                _initTask ??= Task.Run(() =>
                {
                    try
                    {
                        var items = ScanRegistry();
                        lock (_lock) { _cachedItems = items; }
                    }
                    catch (Exception ex)
                    {
                        _ = App.FileLogger.LogAsync($"[ShellNewService] レジストリスキャン失敗: {ex.Message}");
                        lock (_lock) { _cachedItems = new List<ShellNewItem>(); }
                    }
                });
                return _initTask;
            }
        }

        /// <summary>キャッシュ済みリストを返す。未完了なら空リスト。</summary>
        public static List<ShellNewItem> GetCachedItems()
        {
            lock (_lock)
            {
                return _cachedItems ?? new List<ShellNewItem>();
            }
        }

        // エンドユーザーに有用な拡張子のみ表示するホワイトリスト
        // Office 文書・圧縮ファイル等、実際にエクスプローラーの「新規作成」に表示される主要形式
        private static readonly HashSet<string> WhitelistedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".xlsx", ".pptx", ".zip",
            ".docm", ".xlsm", ".xltx", ".dotx",
            ".pub", ".accdb", ".one", ".vsdx",
            ".bmp",
        };

        private static List<ShellNewItem> ScanRegistry()
        {
            var results = new List<ShellNewItem>();
            var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var hkcr = Registry.ClassesRoot;
            foreach (var extName in hkcr.GetSubKeyNames())
            {
                if (!extName.StartsWith(".")) continue;
                if (ExcludedExtensions.Contains(extName)) continue;
                if (!WhitelistedExtensions.Contains(extName)) continue;

                try
                {
                    using var extKey = hkcr.OpenSubKey(extName);
                    if (extKey == null) continue;

                    // ShellNew を探索: 直下 → ProgID サブキー配下の順に検索
                    RegistryKey? shellNewKey = extKey.OpenSubKey("ShellNew");
                    string? shellNewProgId = null;

                    if (shellNewKey == null)
                    {
                        // ProgID サブキー配下を探索
                        // 例: .docx\Word.Document.12\ShellNew
                        foreach (var subKeyName in extKey.GetSubKeyNames())
                        {
                            // PersistentHandler, ShellEx 等は ProgID ではないのでスキップ
                            if (subKeyName.Equals("PersistentHandler", StringComparison.OrdinalIgnoreCase) ||
                                subKeyName.Equals("ShellEx", StringComparison.OrdinalIgnoreCase) ||
                                subKeyName.Equals("OpenWithProgids", StringComparison.OrdinalIgnoreCase) ||
                                subKeyName.Equals("OpenWithList", StringComparison.OrdinalIgnoreCase))
                                continue;

                            using var progSubKey = extKey.OpenSubKey(subKeyName);
                            var candidate = progSubKey?.OpenSubKey("ShellNew");
                            if (candidate != null)
                            {
                                shellNewKey = candidate;
                                shellNewProgId = subKeyName;
                                break;
                            }
                        }
                    }

                    if (shellNewKey == null) continue;

                    using (shellNewKey)
                    {
                        // Command エントリがあれば実行コマンド型 → スキップ
                        if (shellNewKey.GetValue("Command") != null) continue;

                        // テンプレート種類を判定
                        bool isNullFile = false;
                        string? templateFilePath = null;
                        byte[]? templateData = null;

                        if (shellNewKey.GetValue("NullFile") != null)
                        {
                            isNullFile = true;
                        }
                        else if (shellNewKey.GetValue("FileName") is string fileName)
                        {
                            templateFilePath = ResolveTemplatePath(fileName);
                            if (templateFilePath == null)
                            {
                                isNullFile = true;
                            }
                        }
                        else if (shellNewKey.GetValue("Data") is byte[] data)
                        {
                            templateData = data;
                        }
                        else
                        {
                            continue;
                        }

                        // 重複チェック（同一拡張子で直下と ProgID 配下の両方がある場合）
                        if (!seenExtensions.Add(extName)) continue;

                        // 表示名を取得
                        string displayName = "";

                        // まず ShellNew の ProgID から表示名を試みる
                        if (!string.IsNullOrEmpty(shellNewProgId))
                        {
                            using var progIdKey = Registry.ClassesRoot.OpenSubKey(shellNewProgId);
                            displayName = progIdKey?.GetValue(null) as string ?? "";
                        }

                        // フォールバック: extKey のデフォルト ProgID から
                        if (string.IsNullOrWhiteSpace(displayName))
                            displayName = GetDisplayName(extKey, extName);

                        if (string.IsNullOrWhiteSpace(displayName))
                            displayName = extName.TrimStart('.').ToUpperInvariant() + " ファイル";

                        // アイコンを取得
                        ImageSource? icon = null;
                        try
                        {
                            var info = ShellIconHelper.GetGenericInfo("dummy" + extName, false);
                            icon = info.Icon;
                        }
                        catch
                        {
                            // アイコン取得失敗は無視
                        }

                        results.Add(new ShellNewItem
                        {
                            DisplayName = displayName,
                            Extension = extName,
                            Icon = icon,
                            TemplateFilePath = templateFilePath,
                            TemplateData = templateData,
                            IsNullFile = isNullFile,
                        });
                    }
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync($"[ShellNewService] 拡張子 '{extName}' のスキャンでエラー: {ex.Message}");
                }
            }

            return results;
        }

        private static string GetDisplayName(RegistryKey extKey, string extension)
        {
            // ProgID を取得
            var progId = extKey.GetValue(null) as string;
            if (!string.IsNullOrEmpty(progId))
            {
                using var progIdKey = Registry.ClassesRoot.OpenSubKey(progId);
                if (progIdKey != null)
                {
                    var name = progIdKey.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
            return "";
        }

        private static string? ResolveTemplatePath(string fileName)
        {
            // 絶対パスならそのまま
            if (Path.IsPathRooted(fileName) && File.Exists(fileName))
                return fileName;

            // ShellNew テンプレートの標準検索パス
            string[] searchDirs =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ShellNew"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Templates)),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonTemplates)),
            };

            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var fullPath = Path.Combine(dir, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }
}
