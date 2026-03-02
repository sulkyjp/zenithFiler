using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ZenithFiler
{
    /// <summary>
    /// パンくずリストの親コンテナに付与し、幅変更時に ViewModel の UpdateVisibleSegments を呼ぶ。
    /// StackOverflowException 対策のため、実行は Loaded 優先度で遅延しレイアウト中の同期的な再入を防ぐ。
    /// </summary>
    public static class BreadcrumbOverflowBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(BreadcrumbOverflowBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement el)
            {
                if ((bool)e.NewValue)
                {
                    el.Loaded += OnLoaded;
                    el.SizeChanged += OnSizeChanged;
                    if (el.IsLoaded) Update(el);
                }
                else
                {
                    el.Loaded -= OnLoaded;
                    el.SizeChanged -= OnSizeChanged;
                }
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el) Update(el);
        }

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement el) Update(el);
        }

        private static void Update(FrameworkElement el)
        {
            double w = el.ActualWidth;
            if (w <= 0) return;

            // レイアウト中の同期的な SizeChanged→UpdateVisibleSegments→レイアウト→SizeChanged ループを防ぐため遅延実行
            el.Dispatcher?.BeginInvoke(() =>
            {
                if (el.ActualWidth <= 0) return;
                var vm = el.DataContext as TabItemViewModel;
                vm?.UpdateVisibleSegments(el.ActualWidth);
            }, DispatcherPriority.Loaded);
        }
    }
}
