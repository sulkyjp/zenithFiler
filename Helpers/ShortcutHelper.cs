using System;
using System.IO;
using System.Text;
using Vanara.Windows.Shell;

namespace ZenithFiler.Helpers
{
    /// <summary>
    /// Windows ショートカット（.lnk）およびインターネットショートカット（.url）の作成・解析を行うヘルパー。
    /// </summary>
    public static class ShortcutHelper
    {
        /// <summary>
        /// 指定したショートカットファイル（.lnk）のリンク先パスを取得する。
        /// </summary>
        /// <param name="shortcutPath">ショートカットファイルのフルパス</param>
        /// <returns>リンク先のフルパス。失敗時や解析不能時は null</returns>
        public static string? GetShortcutTarget(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath) || !File.Exists(shortcutPath) ||
                !shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                using var link = new ShellLink(shortcutPath);
                var target = link.TargetPath;
                if (string.IsNullOrWhiteSpace(target))
                    return null;

                return PathHelper.GetPhysicalPath(target);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 指定したパスを参照するショートカットファイルをドロップ先に作成する。
        /// ファイル名は「元の名前 - ショートカット.lnk」形式。同名がある場合は連番を付与。
        /// </summary>
        /// <param name="targetPath">ショートカットが指すファイルまたはフォルダのフルパス</param>
        /// <param name="destinationDirectory">ショートカットを生成するフォルダのフルパス</param>
        /// <param name="errorMessage">失敗時の詳細メッセージ（null の場合は取得成功）</param>
        /// <returns>作成したショートカットのフルパス。失敗時は null</returns>
        public static string? CreateShortcut(string targetPath, string destinationDirectory, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(destinationDirectory))
            {
                errorMessage = "パスが指定されていません";
                return null;
            }

            if (!Directory.Exists(destinationDirectory))
            {
                errorMessage = "保存先フォルダが存在しません";
                return null;
            }

            string physicalTargetPath = PathHelper.GetPhysicalPath(targetPath);
            string baseName = Path.GetFileName(physicalTargetPath);
            if (string.IsNullOrEmpty(baseName))
            {
                // ドライブルート（C:\ など）の場合
                string? root = Path.GetPathRoot(physicalTargetPath);
                baseName = root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':') ?? "ショートカット";
            }

            string shortcutFileName = $"{baseName} - ショートカット.lnk";
            string shortcutPath = Path.Combine(destinationDirectory, shortcutFileName);

            // 同名ファイルがある場合は連番を付与（例: 名前 - ショートカット (2).lnk）
            int counter = 1;
            while (File.Exists(shortcutPath))
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(shortcutFileName);
                if (nameWithoutExt.EndsWith(")"))
                {
                    int lastOpenParen = nameWithoutExt.LastIndexOf(" (");
                    if (lastOpenParen != -1)
                    {
                        nameWithoutExt = nameWithoutExt.Substring(0, lastOpenParen);
                    }
                }
                shortcutFileName = $"{nameWithoutExt} ({++counter}).lnk";
                shortcutPath = Path.Combine(destinationDirectory, shortcutFileName);
            }

            try
            {
                string? workingDir = Directory.Exists(physicalTargetPath)
                    ? physicalTargetPath
                    : Path.GetDirectoryName(physicalTargetPath);
                using var _ = ShellLink.Create(shortcutPath, physicalTargetPath, null, workingDir, null);
                return shortcutPath;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 指定した URL へのインターネットショートカット（.url）をドロップ先に作成する。
        /// </summary>
        /// <param name="url">ショートカットが指す URL</param>
        /// <param name="destinationDirectory">ショートカットを生成するフォルダのフルパス</param>
        /// <param name="title">ショートカットのファイル名（拡張子なし）。null の場合は URL から生成</param>
        /// <returns>作成したショートカットのフルパス。失敗時は null</returns>
        public static string? CreateUrlShortcut(string url, string destinationDirectory, string? title = null)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(destinationDirectory))
                return null;

            if (!Directory.Exists(destinationDirectory))
                return null;

            string baseName = title ?? string.Empty;
            if (string.IsNullOrEmpty(baseName))
            {
                try
                {
                    Uri uri = new Uri(url);
                    baseName = uri.Host;
                }
                catch
                {
                    baseName = "インターネットショートカット";
                }
            }

            // 無効な文字を除去
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            // ファイル名が長すぎる場合は切り詰め (MAX_PATH制限対策)
            if (baseName.Length > 200)
            {
                baseName = baseName.Substring(0, 200);
            }
            
            // 空になった場合のフォールバック
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "URLショートカット";
            }

            string shortcutFileName = $"{baseName.Trim()}.url";
            string shortcutPath = Path.Combine(destinationDirectory, shortcutFileName);

            // 同名ファイルがある場合は連番を付与
            int counter = 1;
            while (File.Exists(shortcutPath))
            {
                shortcutFileName = $"{baseName.Trim()} ({++counter}).url";
                shortcutPath = Path.Combine(destinationDirectory, shortcutFileName);
            }

            try
            {
                // .url ファイルは INI 形式。
                // Windows シェルとの互換性のため、Encoding.Default (ANSI) または UTF-8 with BOM を推奨。
                // ここでは標準的な形式で書き込む。
                var sb = new StringBuilder();
                sb.AppendLine("[InternetShortcut]");
                sb.AppendLine($"URL={url}");
                
                File.WriteAllText(shortcutPath, sb.ToString(), Encoding.Default);
                return shortcutPath;
            }
            catch
            {
                return null;
            }
        }
    }
}
