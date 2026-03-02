using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ZenithFiler
{
    public static class TextBlockHighlightBehavior
    {
        public static readonly DependencyProperty HighlightTextProperty =
            DependencyProperty.RegisterAttached(
                "HighlightText",
                typeof(string),
                typeof(TextBlockHighlightBehavior),
                new PropertyMetadata(string.Empty, OnHighlightTextChanged));

        public static string GetHighlightText(DependencyObject obj)
        {
            return (string)obj.GetValue(HighlightTextProperty);
        }

        public static void SetHighlightText(DependencyObject obj, string value)
        {
            obj.SetValue(HighlightTextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(TextBlockHighlightBehavior),
                new PropertyMetadata(string.Empty, OnHighlightTextChanged));

        public static string GetText(DependencyObject obj)
        {
            return (string)obj.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject obj, string value)
        {
            obj.SetValue(TextProperty, value);
        }

        private static void OnHighlightTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                string text = GetText(textBlock);
                string highlightText = GetHighlightText(textBlock);

                textBlock.Inlines.Clear();

                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                if (string.IsNullOrEmpty(highlightText))
                {
                    textBlock.Inlines.Add(new Run(text));
                    return;
                }

                int index = text.IndexOf(highlightText, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    // 完全一致しない場合は、とりあえずそのまま表示
                    // Fuzzy Searchでヒットしているが部分一致しない場合の対応
                    textBlock.Inlines.Add(new Run(text));
                    return;
                }

                // 前半
                if (index > 0)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(0, index)));
                }

                // ハイライト部分
                var highlightRun = new Run(text.Substring(index, highlightText.Length))
                {
                    Background = (Brush)Application.Current.Resources["AccentBrush"],
                    Foreground = Brushes.White // アクセントカラー背景なので白文字
                };
                textBlock.Inlines.Add(highlightRun);

                // 後半
                if (index + highlightText.Length < text.Length)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(index + highlightText.Length)));
                }
            }
        }
    }
}
