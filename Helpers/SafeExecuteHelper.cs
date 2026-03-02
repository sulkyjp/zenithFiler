using System;
using System.Threading.Tasks;

namespace ZenithFiler.Helpers
{
    /// <summary>
    /// 共通エラー処理ユーティリティ。App.FileLogger によるログ記録と、
    /// オプションで App.Notification によるユーザー通知を提供する。
    /// OperationCanceledException は常にスキップ（キャンセルはエラーではない）。
    /// </summary>
    public static class SafeExecuteHelper
    {
        /// <summary>
        /// 同期アクションを安全に実行する。
        /// </summary>
        /// <param name="action">実行するアクション</param>
        /// <param name="context">エラーログに記録するコンテキスト文字列</param>
        /// <param name="notifyUser">true の場合、ユーザーにエラーを通知する</param>
        public static void Execute(Action action, string context, bool notifyUser = false)
        {
            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
                // キャンセルはエラーではない
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[{context}] {ex.GetType().Name}: {ex.Message}");
                if (notifyUser)
                {
                    App.Notification.Notify($"{context} に失敗しました");
                }
            }
        }

        /// <summary>
        /// 非同期アクションを安全に実行する。
        /// </summary>
        /// <param name="func">実行する非同期アクション</param>
        /// <param name="context">エラーログに記録するコンテキスト文字列</param>
        /// <param name="notifyUser">true の場合、ユーザーにエラーを通知する</param>
        public static async Task ExecuteAsync(Func<Task> func, string context, bool notifyUser = false)
        {
            try
            {
                await func();
            }
            catch (OperationCanceledException)
            {
                // キャンセルはエラーではない
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[{context}] {ex.GetType().Name}: {ex.Message}");
                if (notifyUser)
                {
                    App.Notification.Notify($"{context} に失敗しました");
                }
            }
        }

        /// <summary>
        /// 戻り値のある非同期アクションを安全に実行する。失敗時は fallback 値を返す。
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="func">実行する非同期アクション</param>
        /// <param name="fallback">失敗時の戻り値</param>
        /// <param name="context">エラーログに記録するコンテキスト文字列</param>
        /// <param name="notifyUser">true の場合、ユーザーにエラーを通知する</param>
        /// <returns>成功時は func の戻り値、失敗時は fallback</returns>
        public static async Task<T> ExecuteAsync<T>(Func<Task<T>> func, T fallback, string context, bool notifyUser = false)
        {
            try
            {
                return await func();
            }
            catch (OperationCanceledException)
            {
                return fallback;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[{context}] {ex.GetType().Name}: {ex.Message}");
                if (notifyUser)
                {
                    App.Notification.Notify($"{context} に失敗しました");
                }
                return fallback;
            }
        }
    }
}
