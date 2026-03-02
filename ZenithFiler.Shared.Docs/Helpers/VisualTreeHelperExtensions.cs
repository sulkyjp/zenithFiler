using System;
using System.Windows;
using System.Windows.Media;

namespace ZenithFiler.Helpers
{
    /// <summary>
    /// ビジュアルツリー走査の共通ヘルパー。
    /// FindVisualChild の重複実装を集約する。
    /// </summary>
    public static class VisualTreeHelperExtensions
    {
        /// <summary>
        /// 指定した親要素の子孫から、指定した型の最初の要素を検索する。
        /// </summary>
        public static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// 指定した親要素の子孫から、条件を満たす最初の要素を検索する。
        /// </summary>
        public static T? FindVisualChildByPredicate<T>(DependencyObject? obj, Func<T, bool> predicate)
            where T : DependencyObject
        {
            if (obj == null || predicate == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t && predicate(t)) return t;
                var descendant = FindVisualChildByPredicate(child, predicate);
                if (descendant != null) return descendant;
            }
            return null;
        }
    }
}
