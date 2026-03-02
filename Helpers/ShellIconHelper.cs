using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using Vanara.Windows.Shell;
using Vanara.PInvoke;
using MahApps.Metro.IconPacks;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public static class ShellIconHelper
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (ImageSource Icon, string TypeName)> _realCache = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (ImageSource Icon, string TypeName)> _genericCache = new();

        public static (ImageSource Icon, string TypeName) GetInfo(string path, bool isDirectory, bool forceUseAttributes = false)
        {
            // フォルダや特殊なファイル（exe, lnk, ico）は個別に取得するためキャッシュしない
            // forceUseAttributes=true の場合は、実体を見に行かないのでキャッシュして良いかもしれないが、
            // 安全のため現状のキャッシュポリシー（拡張子ベース）を維持しつつ、実体アクセスだけ防ぐ。
            string extension = Path.GetExtension(path).ToLowerInvariant();
            
            // Box領域の場合は強制的に属性のみを使用する（スタブ未生成によるハングや汎用アイコン化を防止）
            // フォルダ・ファイルの両方に適用することで、Box Drive のオンライン状態に関わらず確実に標準アイコンが表示される
            if (!forceUseAttributes && PathHelper.IsInsideBoxDrive(path))
            {
                forceUseAttributes = true;
            }

            bool isCachable = !isDirectory && !string.IsNullOrEmpty(extension) && extension != ".exe" && extension != ".lnk" && extension != ".ico";

            if (!forceUseAttributes && isCachable && _realCache.TryGetValue(extension, out var cached))
            {
                return cached;
            }

            try
            {
                var shfi = new Shell32.SHFILEINFO();
                // 実際のファイルを参照して正確なアイコン（特殊フォルダや特定のファイルアイコン）を取得
                // forceUseAttributes が true なら SHGFI_USEFILEATTRIBUTES を付与して実体アクセスを回避
                var flags = Shell32.SHGFI.SHGFI_ICON | Shell32.SHGFI.SHGFI_SMALLICON | Shell32.SHGFI.SHGFI_TYPENAME;
                
                if (forceUseAttributes)
                {
                    flags |= Shell32.SHGFI.SHGFI_USEFILEATTRIBUTES;
                }
                
                Shell32.SHGetFileInfo(path, isDirectory ? System.IO.FileAttributes.Directory : System.IO.FileAttributes.Normal, 
                    ref shfi, Marshal.SizeOf(shfi), flags);

                ImageSource? icon = null;
                if (shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            (IntPtr)shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        if (bitmapSource.PixelWidth > 0 && bitmapSource.PixelHeight > 0)
                        {
                            bitmapSource.Freeze();
                            icon = bitmapSource;
                        }
                    }
                    finally
                    {
                        User32.DestroyIcon(shfi.hIcon);
                    }
                }

                var typeName = shfi.szTypeName ?? (isDirectory ? "フォルダー" : "ファイル");
                var result = (Icon: icon!, TypeName: typeName);

                if (isCachable && icon != null)
                {
                    _realCache[extension] = result;
                }

                return result;
            }
            catch
            {
                return (null!, isDirectory ? "フォルダー" : "ファイル");
            }
        }

        /// <summary>
        /// 実際のファイルを参照せず、拡張子のみから汎用的なアイコンと種類名を取得します（高速）。
        /// </summary>
        public static (ImageSource Icon, string TypeName) GetGenericInfo(string path, bool isDirectory)
        {
            string key = isDirectory ? "||dir||" : Path.GetExtension(path).ToLowerInvariant();
            
            if (_genericCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            try
            {
                var shfi = new Shell32.SHFILEINFO();
                // SHGFI_USEFILEATTRIBUTES を使用して、ファイルの実体がなくても情報を取得する
                var flags = Shell32.SHGFI.SHGFI_ICON | Shell32.SHGFI.SHGFI_SMALLICON | Shell32.SHGFI.SHGFI_TYPENAME | Shell32.SHGFI.SHGFI_USEFILEATTRIBUTES;
                
                // 拡張子または属性のみを使用して汎用アイコンを取得
                Shell32.SHGetFileInfo(path, isDirectory ? System.IO.FileAttributes.Directory : System.IO.FileAttributes.Normal, 
                    ref shfi, Marshal.SizeOf(shfi), flags);

                ImageSource? icon = null;
                if (shfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            (IntPtr)shfi.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        if (bitmapSource.PixelWidth > 0 && bitmapSource.PixelHeight > 0)
                        {
                            bitmapSource.Freeze();
                            icon = bitmapSource;
                        }
                    }
                    finally
                    {
                        User32.DestroyIcon(shfi.hIcon);
                    }
                }

                var result = (Icon: icon!, TypeName: shfi.szTypeName ?? (isDirectory ? "フォルダー" : "ファイル"));
                
                if (icon != null)
                {
                    _genericCache[key] = result;
                }

                return result;
            }
            catch
            {
                return (null!, isDirectory ? "フォルダー" : "ファイル");
            }
        }

        /// <summary>
        /// フォルダの場所に応じたアイコンを取得します。
        /// </summary>
        public static (ImageSource Icon, string TypeName) GetFolderIcon(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return GetGenericInfo("dummy_folder", true);
            }

            // システムから正確なアイコンを取得
            var info = GetInfo(path, true, false);

            // 場所の種類を判定
            var sourceType = PathHelper.DetermineSourceType(path);

            // 種類に応じた表示名の補正
            if (sourceType != SourceType.Local && (info.TypeName == "フォルダー" || info.TypeName == "File folder" || string.IsNullOrEmpty(info.TypeName)))
            {
                string newTypeName = sourceType switch
                {
                    SourceType.Server => "ネットワーク フォルダー",
                    SourceType.Box => "Box フォルダー",
                    SourceType.SPO => "SharePoint フォルダー",
                    _ => info.TypeName ?? "フォルダー"
                };
                info = (info.Icon, newTypeName);
            }

            return info;
        }

        /// <summary>
        /// 画像ファイルかどうか（拡張子ベース）。サムネイル表示の対象判定に使用。
        /// </summary>
        public static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            var e = ext.ToLowerInvariant();
            return e is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".tif" or ".webp" or ".ico" or ".heic" or ".heif";
        }

        public static void ShowFileProperties(string path)
        {
            try
            {
                Shell32.SHObjectProperties(IntPtr.Zero, Shell32.SHOP.SHOP_FILEPATH, path, null);
            }
            catch (Exception ex)
            {
                ZenithDialog.Show($"プロパティの表示に失敗しました。\n{ex.Message}", "Zenith Filer", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }
        }
    }
}
