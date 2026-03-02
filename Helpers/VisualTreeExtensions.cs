using System.Windows;
using System.Windows.Media;

namespace ZenithFiler.Helpers
{
    /// <summary>
    /// WPF ビジュアルツリーに関するユーティリティメソッド。
    /// using static ZenithFiler.Helpers.VisualTreeExtensions; で呼び出し側の変更を最小化する。
    /// </summary>
    public static class VisualTreeExtensions
    {
        /// <summary>
        /// element が ancestor の子孫（自身を含む）かどうかを判定する。
        /// </summary>
        public static bool IsDescendantOf(DependencyObject? element, DependencyObject? ancestor)
        {
            if (element == null || ancestor == null) return false;
            var current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor)) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
