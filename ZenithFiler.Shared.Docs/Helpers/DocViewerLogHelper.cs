using System;
using System.Text;

namespace ZenithFiler
{
    /// <summary>
    /// マニュアルビューア用のログ出力フック。本体アプリが設定した場合のみログする。
    /// ZenithDocViewer 単独起動時は未設定のため何も出力しない。
    /// </summary>
    internal static class DocViewerLogHelper
    {
        public static Action<string>? LogAsync { get; set; }

        public static void TryLog(Exception ex)
        {
            if (ex == null) return;
            LogAsync?.Invoke(FormatException(ex));
        }

        private static string FormatException(Exception ex)
        {
            if (ex == null) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine($"[EXCEPTION] {ex.GetType().FullName}: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
                sb.AppendLine($"StackTrace:{Environment.NewLine}{ex.StackTrace}");
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                sb.AppendLine($"InnerException: {inner.GetType().FullName}: {inner.Message}");
                if (!string.IsNullOrEmpty(inner.StackTrace))
                    sb.AppendLine(inner.StackTrace);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
