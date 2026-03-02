using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;

namespace ZenithFiler
{
    /// <summary>
    /// OS 標準のシェルコンテキストメニューを表示するラッパー。
    /// リネーム・削除・プロパティはアプリ側コールバックで処理する。
    /// </summary>
    public class ShellContextMenu
    {
        private IContextMenu2? _currentContextMenu2;
        private IContextMenu3? _currentContextMenu3;

        // リネーム要求時のコールバック
        private readonly Action<string>? _onRenameRequest;

        // 削除要求時のコールバック（右クリックメニュー「削除」をアプリ側で実行するため）
        private readonly Action<string[]>? _onDeleteRequest;

        // コマンド実行後のリフレッシュ要求コールバック（新規作成・圧縮等の後にフォルダ内容を再読み込み）
        private readonly Action? _onRefreshRequest;

        public ShellContextMenu(Action<string>? onRenameRequest = null, Action<string[]>? onDeleteRequest = null, Action? onRefreshRequest = null)
        {
            _onRenameRequest = onRenameRequest;
            _onDeleteRequest = onDeleteRequest;
            _onRefreshRequest = onRefreshRequest;
        }

        /// <summary>
        /// シェルコンテキストメニューを表示する。
        /// </summary>
        /// <param name="filePaths">対象ファイル／フォルダのパス</param>
        /// <param name="screenPoint">右クリック位置（呼び出し元で PointToScreen 済みの画面座標）</param>
        /// <param name="isBackground">true のときフォルダ背景メニュー</param>
        /// <param name="workArea">作業領域（画面端はみ出し防止用）。null の場合は補正しない</param>
        public void ShowContextMenu(string[] filePaths, System.Windows.Point screenPoint, bool isBackground = false, System.Windows.Rect? workArea = null, Action? onClosed = null)
        {
            if (filePaths == null || filePaths.Length == 0) return;

            // メインウィンドウのハンドルを取得
            IntPtr mainHwnd = IntPtr.Zero;
            if (Application.Current?.MainWindow != null)
            {
                var helper = new WindowInteropHelper(Application.Current.MainWindow);
                mainHwnd = helper.Handle;
            }

            // 別スレッド（STA）でメニュー処理を実行
            var thread = new Thread(() =>
            {
                try
                {
                    ShowContextMenuInternal(mainHwnd, filePaths, screenPoint, isBackground, workArea);
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync($"[ShellContextMenu] ContextMenu Error: {ex.Message}");
                }
                finally
                {
                    onClosed?.Invoke();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        // シェルメニューの概算サイズ（画面端判定用・論理ピクセル）
        private const double DefaultMenuWidth = 280;
        private const double DefaultMenuHeight = 450;

        private void ShowContextMenuInternal(IntPtr mainHwnd, string[] filePaths, System.Windows.Point screenPoint, bool isBackground, System.Windows.Rect? workArea)
        {
            // STA スレッド上にメッセージ受信用ウィンドウを作成。
            // 7-zip 等の Shell 拡張は InvokeCommand の hwnd を親として DialogBoxParam でモーダルダイアログを生成するため、
            // 右クリック位置に 1x1 の WS_POPUP ウィンドウを配置し、メインウィンドウをオーナーにすることで
            // ダイアログがメインウィンドウ付近に正しく表示されるようにする。
            var parameters = new HwndSourceParameters("ZenithFilerShellContext")
            {
                Width = 1,
                Height = 1,
                PositionX = (int)screenPoint.X,
                PositionY = (int)screenPoint.Y,
                WindowStyle = unchecked((int)0x80000000u),  // WS_POPUP
                ParentWindow = mainHwnd
            };

            using var source = new HwndSource(parameters);
            if (source.Handle == IntPtr.Zero) return;

            HwndSourceHook? hook = null;
            HMENU hMenu = HMENU.NULL;
            IContextMenu? contextMenu = null;
            HWND hwnd = (HWND)source.Handle;

            try
            {
                // IContextMenu の取得には STA スレッドの hwnd を使用する。
                // mainHwnd（UI スレッド）を渡すと 7-zip 等の Shell 拡張が cross-thread で
                // ダイアログ生成に失敗するため、同一 STA スレッド上の hwnd を使う。
                if (isBackground)
                {
                    string physicalPath = PathHelper.GetPhysicalPath(filePaths[0]);
                    using var folder = new ShellFolder(physicalPath);
                    contextMenu = folder.GetViewObject<IContextMenu>(hwnd);
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
                            contextMenu = parentFolder.GetChildrenUIObjects<IContextMenu>(hwnd, items);
                        }
                        finally
                        {
                            foreach (var item in items) item?.Dispose();
                        }
                    }
                }

                if (contextMenu == null) return;

                _currentContextMenu2 = contextMenu as IContextMenu2;
                _currentContextMenu3 = contextMenu as IContextMenu3;

                hMenu = CreatePopupMenu();
                if (hMenu.IsNull) return;

                CMF flags = CMF.CMF_NORMAL | CMF.CMF_EXPLORE | CMF.CMF_CANRENAME;
                contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, flags);

                hook = new HwndSourceHook(MenuHook);
                source.AddHook(hook);

                SetForegroundWindow(hwnd);

                // OS標準スタイル: マウス位置を起点に表示し、画面右・下にはみ出す場合は左・上に展開
                var tpmFlags = TrackPopupMenuFlags.TPM_RETURNCMD | TrackPopupMenuFlags.TPM_RIGHTBUTTON;
                if (workArea.HasValue)
                {
                    var r = workArea.Value;
                    if (screenPoint.X + DefaultMenuWidth > r.Right) tpmFlags |= TrackPopupMenuFlags.TPM_RIGHTALIGN;
                    if (screenPoint.Y + DefaultMenuHeight > r.Bottom) tpmFlags |= TrackPopupMenuFlags.TPM_BOTTOMALIGN;
                }

                uint cmd = TrackPopupMenuEx(hMenu, tpmFlags, (int)screenPoint.X, (int)screenPoint.Y, hwnd);

                source.RemoveHook(hook);
                hook = null;

                if (cmd != 0)
                {
                    int offset = (int)cmd - 1;

                    // コマンドの Verb を取得して判定
                    // 7-zip 等のサードパーティ Shell 拡張は GetCommandString で E_NOTIMPL を返すため
                    // 例外を捕捉して空文字列として扱う
                    const int bufSize = 256;
                    string verb = "";
                    IntPtr ptr = Marshal.AllocHGlobal(bufSize * 2); // Unicode = 2 bytes per char
                    try
                    {
                        contextMenu.GetCommandString((nint)offset, GCS.GCS_VERBW, IntPtr.Zero, ptr, (uint)bufSize);
                        verb = Marshal.PtrToStringUni(ptr)?.ToLowerInvariant() ?? "";
                    }
                    catch { /* Shell 拡張が GetCommandString 未実装の場合 */ }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }

                    // 組み込み「削除」では GetCommandString が空を返すことがあるため ANSI 版も試行
                    if (string.IsNullOrEmpty(verb))
                    {
                        ptr = Marshal.AllocHGlobal(bufSize);
                        try
                        {
                            contextMenu.GetCommandString((nint)offset, GCS.GCS_VERBA, IntPtr.Zero, ptr, (uint)bufSize);
                            verb = Marshal.PtrToStringAnsi(ptr)?.ToLowerInvariant() ?? "";
                        }
                        catch { /* 無視 */ }
                        finally { Marshal.FreeHGlobal(ptr); }
                    }

                    string? menuText = null;
                    try
                    {
                        if (!hMenu.IsNull)
                        {
                            menuText = GetMenuItemText(hMenu, (uint)cmd);
                        }
                    }
                    catch { /* 無視 */ }
                    
                    // verb が空の場合、メニュー項目の表示文字列で「削除」を判定する（日本語「削除」/ 英語 "Delete"）
                    bool isDeleteByMenuText = false;
                    if (_onDeleteRequest != null && !string.IsNullOrEmpty(menuText))
                    {
                        var t = menuText.Trim();
                        // 判定を緩和: "削除", "Delete" を含む場合も対象にする（アクセラレータキー対策）
                        isDeleteByMenuText = t.Contains("削除") || t.Contains("Delete", StringComparison.OrdinalIgnoreCase);
                    }

                    // リネームの場合
                    if (verb == "rename" && _onRenameRequest != null && filePaths.Length == 1)
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(
                            () => _onRenameRequest(filePaths[0]),
                            DispatcherPriority.Background);
                        return;
                    }

                    // 削除の場合（verb が "delete" を含む または 表示文字列が「削除」/ "Delete" のときアプリの削除処理に委譲）
                    if (((verb?.Contains("delete") == true) || isDeleteByMenuText) && _onDeleteRequest != null)
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(
                            () => _onDeleteRequest(filePaths),
                            DispatcherPriority.Background);
                        return;
                    }

                    // プロパティの場合（特に問題になりやすい）
                        if (verb == "properties")
                        {
                            // 単一ファイルなら安全な SHObjectProperties をメインスレッドで実行
                            if (filePaths.Length == 1)
                            {
                                _ = Application.Current.Dispatcher.InvokeAsync(
                                    () => ShellIconHelper.ShowFileProperties(filePaths[0]),
                                    DispatcherPriority.Background);
                            return;
                        }
                        // 複数ファイルの場合は通常のInvokeCommandに頼るが、メインスレッドにディスパッチしてみる
                        // IContextMenu のマーシャリングが機能するかは環境依存だが、スレッド終了で消えるよりはマシ
                        /*
                        // 注意: IContextMenu をメインスレッドで使うと RPC_E_WRONG_THREAD になる可能性が高い。
                        // そのため、バックグラウンドスレッドのまま実行するしかないが、プロパティウィンドウが消えないように工夫が必要。
                        // ここでは、InvokeCommand を実行し、少し待機するか、メッセージループを維持するトライを行う。
                        */
                    }

                    // 作業ディレクトリ算出（shell の NewMenu 等が作成先ディレクトリを正しく認識するために必要）
                    string? workingDir = isBackground
                        ? PathHelper.GetPhysicalPath(filePaths[0])
                        : Path.GetDirectoryName(PathHelper.GetPhysicalPath(filePaths[0]));

                    // InvokeCommand には STA スレッドの hwnd を使用する。
                    // Shell 拡張（7-zip 等）は InvokeCommand の hwnd を親としてダイアログを生成するため、
                    // 同一 STA スレッド上の hwnd でないとモーダルダイアログが正常に動作しない。
                    var ici = new CMINVOKECOMMANDINFOEX
                    {
                        cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                        fMask = CMIC.CMIC_MASK_UNICODE | CMIC.CMIC_MASK_PTINVOKE | CMIC.CMIC_MASK_NOASYNC,
                        hwnd = hwnd,
                        lpVerb = new SafeResourceId(offset),
                        lpVerbW = new SafeResourceId(offset),
                        lpDirectory = workingDir,
                        lpDirectoryW = workingDir,
                        nShow = ShowWindowCommand.SW_SHOWNORMAL,
                        ptInvoke = new POINT((int)screenPoint.X, (int)screenPoint.Y)
                    };

                    _ = App.FileLogger.LogAsync($"[ShellContextMenu] InvokeCommand: verb='{verb}', menuText='{menuText}', offset={offset}, isBackground={isBackground}");
                    try
                    {
                        contextMenu.InvokeCommand(ici);
                    }
                    catch (Exception invokeEx)
                    {
                        _ = App.FileLogger.LogAsync($"[ShellContextMenu] InvokeCommand failed: {invokeEx.GetType().Name}: {invokeEx.Message} (HResult=0x{invokeEx.HResult:X8})");
                        return;
                    }

                    // verb が空 = サードパーティ Shell 拡張（GetCommandString 未実装）の可能性が高い。
                    // 操作内容を判定できないため、安全のためメッセージポンプを実行する。
                    bool isShellExtension = string.IsNullOrEmpty(verb);
                    bool needsRefresh = isBackground || isShellExtension
                        || IsLongRunningVerb(verb ?? "") || IsLongRunningByMenuText(menuText);

                    // プロパティなどの場合、スレッドが即終了するとウィンドウが消えることがあるため、少し待つ
                    if (verb == "properties")
                    {
                        Thread.Sleep(1000);
                    }
                    else if (needsRefresh)
                    {
                        // 背景メニュー操作や Shell 拡張の場合、
                        // STA スレッドを維持して進捗ダイアログ等が正常に動作するようにする
                        RunPostInvokeMessagePump((IntPtr)hwnd);

                        // 完了後にフォルダ内容を再読み込み
                        _onRefreshRequest?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ShellContextMenu] ContextMenu Logic Error: {ex.Message}");
            }
            finally
            {
                if (source != null && hook != null) source.RemoveHook(hook);
                _currentContextMenu2 = null;
                _currentContextMenu3 = null;
                if (contextMenu != null)
                {
                    try { Marshal.FinalReleaseComObject(contextMenu); }
                    catch { /* 既に解放済み等 */ }
                }
                if (!hMenu.IsNull) DestroyMenu(hMenu);
            }
        }

        public void ShowBackgroundContextMenu(string folderPath, System.Windows.Point screenPoint, System.Windows.Rect? workArea = null, Action? onClosed = null)
        {
            ShowContextMenu(new[] { folderPath }, screenPoint, true, workArea, onClosed);
        }

        private IntPtr MenuHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const uint WM_DRAWITEM = 0x002B;
            const uint WM_MEASUREITEM = 0x002C;
            const uint WM_INITMENUPOPUP = 0x0117;
            const uint WM_MENUCHAR = 0x0120;
            const uint WM_NEXTMENU = 0x0213;

            uint uMsg = (uint)msg;
            if (uMsg == WM_DRAWITEM || uMsg == WM_MEASUREITEM || uMsg == WM_INITMENUPOPUP || uMsg == WM_MENUCHAR || uMsg == WM_NEXTMENU)
            {
                if (_currentContextMenu3 != null)
                {
                    if (_currentContextMenu3.HandleMenuMsg2(uMsg, wParam, lParam, out var lResult).Succeeded)
                    {
                        handled = true;
                        if (uMsg == WM_DRAWITEM || uMsg == WM_MEASUREITEM) return (IntPtr)1;
                        return lResult;
                    }
                }
                else if (_currentContextMenu2 != null)
                {
                    if (_currentContextMenu2.HandleMenuMsg(uMsg, wParam, lParam).Succeeded)
                    {
                        handled = true;
                        if (uMsg == WM_DRAWITEM || uMsg == WM_MEASUREITEM) return (IntPtr)1;
                        return IntPtr.Zero;
                    }
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// InvokeCommand 後に STA スレッドを維持し、shell 拡張の進捗ダイアログ等が正常に動作するようにする。
        /// DispatcherFrame + DispatcherTimer で STA スレッド上のウィンドウを監視し、
        /// ダミーウィンドウ以外が閉じたら（quiet period 後）終了する。
        /// </summary>
        private static void RunPostInvokeMessagePump(IntPtr staHwnd)
        {
            var frame = new DispatcherFrame();
            uint nativeThreadId = NativeMethods.GetCurrentThreadId();
            var startTime = DateTime.UtcNow;
            int quietCount = 0;
            bool everSawChildWindow = false;
            // Phase 1: ダイアログが出ない操作（新規フォルダ等）は 2 秒で早期終了
            // Phase 2: ダイアログが出た場合（圧縮等）は閉じてから 1.5 秒待機
            const int initialQuietThreshold = 20;   // 20 * 100ms = 2秒（ダイアログ出現前の早期終了を防止）
            const int postDialogQuietThreshold = 15; // 15 * 100ms = 1.5 秒
            const int maxMinutes = 10;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                if ((DateTime.UtcNow - startTime).TotalMinutes > maxMinutes)
                {
                    frame.Continue = false;
                    return;
                }

                bool hasChildWindows = false;
                NativeMethods.EnumThreadWindows(nativeThreadId, (hWnd, _) =>
                {
                    if (hWnd != staHwnd)
                    {
                        hasChildWindows = true;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                if (hasChildWindows)
                {
                    everSawChildWindow = true;
                    quietCount = 0;
                }
                else
                {
                    quietCount++;
                    int threshold = everSawChildWindow ? postDialogQuietThreshold : initialQuietThreshold;
                    if (quietCount >= threshold)
                    {
                        frame.Continue = false;
                    }
                }
            };

            timer.Start();
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>圧縮・展開・送る等の長時間実行が予想される verb かどうかを判定する。</summary>
        private static bool IsLongRunningVerb(string verb)
        {
            if (string.IsNullOrEmpty(verb)) return false;
            return verb.Contains("compress") || verb.Contains("extract")
                || verb.Contains("zip") || verb.Contains("sendto")
                || verb.Contains("7-zip") || verb.Contains("7zip");
        }

        /// <summary>メニューテキストから長時間操作（圧縮・展開等）かどうかを判定する。verb が空の Shell 拡張向け。</summary>
        private static bool IsLongRunningByMenuText(string? menuText)
        {
            if (string.IsNullOrEmpty(menuText)) return false;
            var t = menuText.ToLowerInvariant();
            return t.Contains("7-zip") || t.Contains("7zip")
                || t.Contains("圧縮") || t.Contains("展開") || t.Contains("解凍")
                || t.Contains("extract") || t.Contains("compress") || t.Contains("archive")
                || t.Contains("winrar") || t.Contains("bandizip");
        }

        /// <summary>
        /// 指定したメニュー項目の表示文字列を取得する。サブメニューも再帰的に検索する。
        /// 7-zip 等の Shell 拡張はサブメニュー内に項目を配置するため、ルートメニューだけでは見つからない。
        /// </summary>
        private static string? GetMenuItemText(HMENU hMenu, uint itemId)
        {
            // まずルートメニューから ID 指定で検索
            string? text = GetMenuItemTextDirect((IntPtr)hMenu, itemId);
            if (text != null) return text;

            // 見つからない場合はサブメニューを再帰検索
            int count = NativeMethods.GetMenuItemCount((IntPtr)hMenu);
            for (int i = 0; i < count; i++)
            {
                IntPtr subMenu = NativeMethods.GetSubMenu((IntPtr)hMenu, i);
                if (subMenu == IntPtr.Zero) continue;

                text = GetMenuItemTextDirect(subMenu, itemId);
                if (text != null) return text;

                // さらに深いサブメニューも検索（7-zip のネスト対応）
                int subCount = NativeMethods.GetMenuItemCount(subMenu);
                for (int j = 0; j < subCount; j++)
                {
                    IntPtr subSubMenu = NativeMethods.GetSubMenu(subMenu, j);
                    if (subSubMenu == IntPtr.Zero) continue;
                    text = GetMenuItemTextDirect(subSubMenu, itemId);
                    if (text != null) return text;
                }
            }
            return null;
        }

        private static string? GetMenuItemTextDirect(IntPtr hMenu, uint itemId)
        {
            const uint MIIM_STRING = 0x80;
            const int cchMax = 256;
            IntPtr buf = Marshal.AllocHGlobal(cchMax * 2);
            try
            {
                var mii = new MenuItemInfoNative
                {
                    cbSize = (uint)Marshal.SizeOf<MenuItemInfoNative>(),
                    fMask = MIIM_STRING,
                    dwTypeData = buf,
                    cch = (uint)cchMax
                };
                if (!NativeMethods.GetMenuItemInfoW(hMenu, itemId, false, ref mii))
                    return null;
                return Marshal.PtrToStringUni(mii.dwTypeData, (int)mii.cch);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MenuItemInfoNative
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public UIntPtr dwItemData;
        public IntPtr dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool GetMenuItemInfoW(IntPtr hMenu, uint uItem, bool fByPosition, ref MenuItemInfoNative lpmii);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

        internal delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern uint GetCurrentThreadId();
    }
}
