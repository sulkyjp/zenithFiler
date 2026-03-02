using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZenithFiler
{
    /// <summary>
    /// ManualTocItem.Indent (double) を ListBoxItem の左インデント用 Thickness に変換するコンバーター。
    /// 上下には最小限だけマージンを付けて、TOC 全体をタイトに保つ。
    /// </summary>
    public class IndentToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double indent)
            {
                return new Thickness(indent, 3, 4, 3);
            }

            return new Thickness(0, 3, 4, 3);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
