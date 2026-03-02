using System;
using System.IO;
using System.Threading.Tasks;

namespace ZenithFiler
{
    public enum SourceType
    {
        Local,
        Server,
        Box,
        SPO
    }

    public static class PathHelper
    {
        /// <summary>仮想「PC（マイコンピュータ）」を表す内部センチネルパス。</summary>
        public const string PCPath = "::{PC}";

        /// <summary>PC パスの表示名。</summary>
        public const string PCDisplayName = "PC";

        /// <summary>パスが仮想 PC パスかどうかを判定します。</summary>
        public static bool IsPCPath(string? path)
            => !string.IsNullOrEmpty(path) && path == PCPath;

        private static readonly string? _envOneDriveCommercial = Environment.GetEnvironmentVariable("OneDriveCommercial");
        private static readonly string? _envOneDriveConsumer = Environment.GetEnvironmentVariable("OneDriveConsumer")
            ?? Environment.GetEnvironmentVariable("OneDrive");
        private static readonly string _defaultBoxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Box");

        /// <summary>起動直後に取得・キャッシュした特殊フォルダのパス。タブ復元時に確実に参照する。</summary>
        private static string? _cachedDesktopPath;
        private static string? _cachedDownloadsPath;
        private static string? _cachedDocumentsPath;
        private static bool _specialFoldersCached;

        /// <summary>
        /// パスからソースタイプを判別します。
        /// </summary>
        public static SourceType DetermineSourceType(string path)
        {
            if (string.IsNullOrEmpty(path)) return SourceType.Local;

            // 1. Server: UNCパス
            if (path.StartsWith(@"\\")) return SourceType.Server;

            // 2. Box: プロファイル直下のBoxフォルダ、またはパス中に \Box\ を含む
            if (path.StartsWith(_defaultBoxPath, StringComparison.OrdinalIgnoreCase) ||
                IsInsideBoxDrive(path))
            {
                return SourceType.Box;
            }

            // 3. SPO: 環境変数配下、またはキーワードマッチ
            if (!string.IsNullOrEmpty(_envOneDriveCommercial) && path.StartsWith(_envOneDriveCommercial, StringComparison.OrdinalIgnoreCase))
            {
                return SourceType.SPO;
            }

            if (path.Contains("OneDrive -", StringComparison.OrdinalIgnoreCase) || 
                path.Contains(@"\SharePoint\", StringComparison.OrdinalIgnoreCase))
            {
                return SourceType.SPO;
            }

            return SourceType.Local;
        }

        /// <summary>
        /// パスが Box Drive 領域内（\Box\ を含む）かどうかを判定します。
        /// </summary>
        public static bool IsInsideBoxDrive(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(@"\Box\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// パスが Box 領域内かどうかを判定します（IsInsideBoxDrive のエイリアス）。インデックス作成時の暖気運転判定などで使用します。
        /// </summary>
        public static bool IsBoxPath(string? path) => IsInsideBoxDrive(path);

        /// <summary>
        /// Box Drive のディレクトリ構造を列挙し、スタブ（プレースホルダ）の生成を促します。
        /// ディレクトリ一覧を軽く取得するだけで Box Drive がスタブを生成するため、同期的に列挙を行います。
        /// </summary>
        public static void WarmUpBoxPath(string path)
        {
            if (!IsBoxPath(path)) return;
            try
            {
                // 直下の子要素を列挙するだけで、Box Drive 側でディレクトリ属性が確定し、アイコン取得が安定する
                var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
                foreach (var _ in Directory.EnumerateFileSystemEntries(path, "*", options))
                {
                    break; // 最初の一つだけで十分な場合が多いが、列挙自体がトリガーとなる
                }
            }
            catch { }
        }

        /// <summary>
        /// Box 領域内のフルパスから連携用パス（Box\～）を取得します。
        /// 他ユーザーと共有する際に使う、ユーザー名に依存しないパス形式です。
        /// </summary>
        /// <param name="fullPath">フルパス（例: C:\Users\user\Box\Folder\file.txt）</param>
        /// <param name="boxSharePath">連携用パス（例: Box\Folder\file.txt）。取得できない場合は null。</param>
        /// <returns>連携用パスを取得できた場合 true</returns>
        public static bool TryGetBoxSharePath(string fullPath, out string? boxSharePath)
        {
            boxSharePath = null;
            if (string.IsNullOrEmpty(fullPath)) return false;
            if (DetermineSourceType(fullPath) != SourceType.Box) return false;

            int idx = fullPath.IndexOf(@"\Box\", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            boxSharePath = fullPath.Substring(idx + 1);
            return true;
        }

        /// <summary>
        /// フルパスを環境非依存の共有用パスに変換します。
        /// Box → "Box\…"、OneDrive → "OneDrive - …\…"、その他 → 絶対パスをそのまま返します。
        /// </summary>
        public static string GetShareablePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;

            // Box: \Box\ を検出して以降を抽出
            int boxIdx = fullPath.IndexOf(@"\Box\", StringComparison.OrdinalIgnoreCase);
            if (boxIdx >= 0)
                return fullPath.Substring(boxIdx + 1); // "Box\…"

            // OneDrive for Business
            if (!string.IsNullOrEmpty(_envOneDriveCommercial)
                && fullPath.StartsWith(_envOneDriveCommercial, StringComparison.OrdinalIgnoreCase))
            {
                // "C:\Users\x\OneDrive - Company\sub" → "OneDrive - Company\sub"
                var rootName = Path.GetFileName(_envOneDriveCommercial.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var remainder = fullPath.Substring(_envOneDriveCommercial.Length).TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(remainder) ? rootName : rootName + @"\" + remainder;
            }

            // OneDrive Personal
            if (!string.IsNullOrEmpty(_envOneDriveConsumer)
                && fullPath.StartsWith(_envOneDriveConsumer, StringComparison.OrdinalIgnoreCase))
            {
                var rootName = Path.GetFileName(_envOneDriveConsumer.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var remainder = fullPath.Substring(_envOneDriveConsumer.Length).TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(remainder) ? rootName : rootName + @"\" + remainder;
            }

            // "OneDrive -" キーワードマッチ（環境変数未設定でもフォルダ名から検出）
            int odIdx = fullPath.IndexOf(@"\OneDrive", StringComparison.OrdinalIgnoreCase);
            if (odIdx >= 0)
            {
                var sub = fullPath.Substring(odIdx + 1); // "OneDrive - Company\…"
                if (sub.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
                    return sub;
            }

            return fullPath;
        }

        /// <summary>
        /// パスがUNCルート（\\server または \\server\）かどうかを判別します。
        /// </summary>
        public static bool IsUncRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!path.StartsWith(@"\\", StringComparison.Ordinal) || path.Length < 3) return false;

            var rest = path.AsSpan(2);
            int index = rest.IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return index == -1 || index == rest.Length - 1;
        }

        /// <summary>
        /// パスを正規化します。入力パスをフルパスに解決し、末尾の区切り文字を整理します。
        /// 環境に依存しない一貫した動作のため、OneDrive 等へのパス変換は行いません。
        /// </summary>
        public static string GetPhysicalPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return path;

                // 仮想 PC パスは正規化せずそのまま返す
                if (IsPCPath(path)) return path;

                string input = path;
                // "C:" のようなドライブレターのみの形式は、現在のディレクトリではなくルートディレクトリとして扱う
                if (input.Length == 2 && input[1] == ':' && char.IsLetter(input[0]))
                {
                    input += Path.DirectorySeparatorChar;
                }

                string fullPath = Path.GetFullPath(input);

                // ルートディレクトリ（"C:\"や"\\server\share\"）でない場合のみ、末尾のスラッシュを削除して正規化する
                string? root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root) && fullPath.Length > root.Length)
                {
                    fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                return fullPath;
            }
            catch
            {
                return path;
            }
        }

        /// <summary>
        /// 起動直後にデスクトップ・ダウンロード・ドキュメントのパスを取得しキャッシュします。
        /// タブ復元やツリー展開の前に呼び出し、特殊フォルダの解決を安定させます。
        /// </summary>
        public static void EnsureSpecialFoldersCached()
        {
            if (_specialFoldersCached) return;
            try
            {
                _cachedDesktopPath = GetPhysicalPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _cachedDownloadsPath = GetPhysicalPath(Path.Combine(userProfile, "Downloads"));
                _cachedDocumentsPath = GetPhysicalPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                _specialFoldersCached = true;
            }
            catch { _specialFoldersCached = true; }
        }

        /// <summary>
        /// 指定された特殊フォルダの最適な初期パスを取得します。
        /// キャッシュ済みの場合はキャッシュを返し、起動直後の解決を安定させます。
        /// </summary>
        public static string GetInitialPath(Environment.SpecialFolder folder)
        {
            EnsureSpecialFoldersCached();
            if (folder == Environment.SpecialFolder.Desktop && !string.IsNullOrEmpty(_cachedDesktopPath))
                return _cachedDesktopPath;
            if (folder == Environment.SpecialFolder.MyDocuments && !string.IsNullOrEmpty(_cachedDocumentsPath))
                return _cachedDocumentsPath;
            string path = Environment.GetFolderPath(folder);
            return GetPhysicalPath(path);
        }

        /// <summary>
        /// ダウンロードフォルダのパスを取得します。
        /// キャッシュ済みの場合はキャッシュを返します。
        /// </summary>
        public static string GetDownloadsPath()
        {
            EnsureSpecialFoldersCached();
            if (!string.IsNullOrEmpty(_cachedDownloadsPath))
                return _cachedDownloadsPath;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return GetPhysicalPath(Path.Combine(userProfile, "Downloads"));
        }

        /// <summary>
        /// 重複比較用にパスを正規化します。必ず Path.GetFullPath で正規化してから比較に使用してください。
        /// </summary>
        public static string NormalizePathForComparison(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            try
            {
                if (path.Length == 2 && path[1] == ':' && char.IsLetter(path[0]))
                    path += Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                string? root = Path.GetPathRoot(full);
                if (!string.IsNullOrEmpty(root) && full.Length > root.Length)
                    full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return full;
            }
            catch { return path; }
        }

        /// <summary>
        /// パスがネットワーク上（UNC またはネットワークマップドドライブ）かを判定します。
        /// </summary>
        public static bool IsNetworkPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // UNC パスの判定は文字列操作のみ（I/O なし）
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
            // マップドドライブの DriveType チェックは削除：
            // DriveInfo が切断ドライブで数十秒ブロックするため。
            // マップドドライブは DirectoryExistsSafeAsync のタイムアウトで保護される。
            return false;
        }

        /// <summary>
        /// パスが OneDrive や Box 等のクラウド同期対象フォルダ配下かを判定します。
        /// 同期ソフトによる一時ロックや遅延を考慮した処理分岐に使用します。
        /// </summary>
        public static bool IsCloudSyncedPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // OneDrive for Business
            if (!string.IsNullOrEmpty(_envOneDriveCommercial)
                && path.StartsWith(_envOneDriveCommercial, StringComparison.OrdinalIgnoreCase))
                return true;

            // OneDrive Personal
            if (!string.IsNullOrEmpty(_envOneDriveConsumer)
                && path.StartsWith(_envOneDriveConsumer, StringComparison.OrdinalIgnoreCase))
                return true;

            // キーワードベースの OneDrive / SharePoint 判定
            if (path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)
                || path.Contains(@"\SharePoint\", StringComparison.OrdinalIgnoreCase))
                return true;

            // Box Drive
            if (IsInsideBoxDrive(path)) return true;

            return false;
        }

        /// <summary>
        /// タイムアウト付きの Directory.Exists。ネットワークパスの場合は指定ミリ秒で打ち切り false を返す。
        /// ローカルパスは即座に同期判定する。
        /// </summary>
        public static async Task<bool> DirectoryExistsSafeAsync(string? path, int timeoutMs = 300)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                var task = Task.Run(() => Directory.Exists(path));
                var completed = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
                return completed == task && task.Result;
            }
            catch { return false; }
        }

        /// <summary>
        /// パスがドライブルート（例: C:\）のみかどうかを判定します。
        /// </summary>
        public static bool IsDriveRootOnly(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string normalized = NormalizePathForComparison(path);
                string? root = Path.GetPathRoot(normalized);
                if (string.IsNullOrEmpty(root)) return false;
                return normalized.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
