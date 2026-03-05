using System;
using System.Threading.Tasks;

namespace ZenithFiler.Helpers
{
    public static class TaskHelper
    {
        /// <summary>
        /// Fire-and-forget で Task を実行し、例外発生時にログ出力する。
        /// <c>_ = SomeAsync()</c> の代替として使用する。
        /// </summary>
        public static async void FireAndForget(this Task task, string context)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[{context}] Fire-and-forget failed: {ex.Message}");
            }
        }
    }
}
