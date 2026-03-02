using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ZenithFiler.Helpers
{
    /// <summary>
    /// Windows Restart Manager API を使用してファイルをロックしているプロセスを特定するヘルパー。
    /// 全メソッドはスレッドセーフ（呼び出しごとに独立した RM セッションを使用）。
    /// </summary>
    public static class RestartManagerHelper
    {
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_MORE_DATA = 234;
        private const int CCH_RM_MAX_APP_NAME = 255;
        private const int CCH_RM_MAX_SVC_NAME = 63;

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint pSessionHandle,
            uint nFiles, string[] rgsFileNames,
            uint nApplications, [In] RM_UNIQUE_PROCESS[]? rgApplications,
            uint nServices, string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded, ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, ref uint lpdwRebootReasons);

        /// <summary>
        /// 指定ファイルをロックしているプロセス名のリストを返す。
        /// ガード: null / 空パスや存在しないファイルは空リストを返す。
        /// P/Invoke 失敗時もログ出力して空リストを返す（呼び出し元でクラッシュしない）。
        /// </summary>
        public static List<string> GetLockingProcessNames(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return [];

            int result = RmStartSession(out uint sessionHandle, 0, Guid.NewGuid().ToString());
            if (result != ERROR_SUCCESS)
            {
                _ = App.FileLogger.LogAsync($"[WARN][RestartManager] RmStartSession failed: error={result}");
                return [];
            }

            try
            {
                string[] resources = [filePath];
                result = RmRegisterResources(sessionHandle, 1, resources, 0, null, 0, null);
                if (result != ERROR_SUCCESS)
                {
                    _ = App.FileLogger.LogAsync($"[WARN][RestartManager] RmRegisterResources failed: error={result} path='{filePath}'");
                    return [];
                }

                uint needed = 0, count = 0, rebootReasons = 0;
                result = RmGetList(sessionHandle, out needed, ref count, null, ref rebootReasons);

                // ERROR_SUCCESS(0) + needed==0 → ロック中プロセスなし
                if (result == ERROR_SUCCESS && needed == 0)
                    return [];

                if (result != ERROR_MORE_DATA || needed == 0)
                    return [];

                var processInfo = new RM_PROCESS_INFO[needed];
                count = needed;
                result = RmGetList(sessionHandle, out needed, ref count, processInfo, ref rebootReasons);
                if (result != ERROR_SUCCESS)
                    return [];

                // HashSet で重複排除してからリスト化
                var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < count; i++)
                {
                    string? appName = processInfo[i].strAppName;
                    if (!string.IsNullOrWhiteSpace(appName))
                        nameSet.Add(appName);
                }
                return [.. nameSet];
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[WARN][RestartManager] GetLockingProcessNames failed: {ex.GetType().Name}: {ex.Message} path='{filePath}'");
                return [];
            }
            finally
            {
                RmEndSession(sessionHandle);
            }
        }

        /// <summary>
        /// 例外とファイルパスからユーザー向けのエラーメッセージを生成する。
        /// IOException の場合はロック中プロセスの特定を試みる。
        /// </summary>
        public static string GetFriendlyErrorMessage(Exception? ex, string? filePath)
        {
            if (ex is null)
                return string.Empty;

            if (ex is UnauthorizedAccessException)
                return $"アクセス権限が不足しています\n{filePath ?? ""}";

            if (ex is IOException ioEx)
            {
                int hr = ioEx.HResult & 0xFFFF;

                // ERROR_SHARING_VIOLATION (0x0020) / ERROR_LOCK_VIOLATION (0x0021)
                if (hr is 0x0020 or 0x0021)
                {
                    var lockingProcesses = GetLockingProcessNames(filePath);
                    if (lockingProcesses.Count > 0)
                    {
                        string processNames = string.Join(", ", lockingProcesses);
                        return $"{processNames} が使用中のため操作できません\n{filePath ?? ""}";
                    }
                    return $"別のプロセスがファイルを使用中のため操作できません\n{filePath ?? ""}";
                }

                // ERROR_DISK_FULL (0x0070)
                if (hr == 0x0070)
                    return $"ディスクの空き容量が不足しています\n{filePath ?? ""}";
            }

            return ex.Message;
        }
    }
}
