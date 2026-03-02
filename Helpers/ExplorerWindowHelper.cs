using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ZenithFiler
{
    /// <summary>
    /// Windows エクスプローラーで開いているフォルダ一覧の取得とウィンドウの閉じ方を提供する。
    /// Shell.Application COM (SHDocVw) を使用する実績のある方式に準拠。
    /// </summary>
    public static class ExplorerWindowHelper
    {
        private const int WM_CLOSE = 0x0010;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 開いているエクスプローラーウィンドウのフォルダパスとハンドル一覧を取得する。
        /// フォルダを表示しているウィンドウのみ対象（file: 以外の URL は除外）。
        /// </summary>
        public static List<ExplorerWindowInfo> GetOpenExplorerFolders()
        {
            var list = new List<ExplorerWindowInfo>();
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return list;

            object? shellApp = null;
            try
            {
                shellApp = Activator.CreateInstance(shellType);
                if (shellApp == null) return list;

                dynamic shell = shellApp;
                var windows = shell.Windows();
                int count = windows.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic? ie = windows.Item(i);
                        if (ie == null) continue;
                        string fullName = (string)ie.FullName;
                        if (string.IsNullOrEmpty(fullName) ||
                            !fullName.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string locationUrl = (string)ie.LocationURL;
                        if (string.IsNullOrEmpty(locationUrl) || !locationUrl.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string? path = null;
                        try
                        {
                            var uri = new Uri(locationUrl);
                            path = uri.LocalPath;
                        }
                        catch
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(path)) continue;
                        path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (!Directory.Exists(path) && !PathHelper.IsUncRoot(path))
                            continue;

                        int hwndVal = (int)ie.HWND;
                        list.Add(new ExplorerWindowInfo(path, new IntPtr(hwndVal)));
                    }
                    catch
                    {
                        // 1件の取得失敗は無視して続行
                    }
                }
            }
            finally
            {
                if (shellApp != null && Marshal.IsComObject(shellApp))
                    Marshal.ReleaseComObject(shellApp);
            }

            return list;
        }

        /// <summary>
        /// 指定したエクスプローラーウィンドウを閉じる。
        /// </summary>
        public static void CloseExplorerWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public record ExplorerWindowInfo(string Path, IntPtr Hwnd);

}
