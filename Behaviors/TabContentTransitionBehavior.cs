using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZenithFiler.Helpers;

namespace ZenithFiler
{
    /// <summary>
    /// タブ切り替え時にコンテンツをフェードインする添付ビヘイビア。
    /// </summary>
    public static class TabContentTransitionBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(TabContentTransitionBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private const double DurationSeconds = 0.1;
        private const double StartOpacity = 0.8;

        /// <summary>各 TabControl で初回選択をスキップしたか（ペインごとに保持）</summary>
        private static readonly HashSet<TabControl> _initialSelectionSkipped = new();

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TabControl tabControl)
                return;

            if ((bool)e.NewValue)
            {
                _initialSelectionSkipped.Add(tabControl);
                tabControl.SelectionChanged += OnSelectionChanged;
                tabControl.Unloaded += OnUnloaded;
            }
            else
            {
                _initialSelectionSkipped.Remove(tabControl);
                tabControl.SelectionChanged -= OnSelectionChanged;
                tabControl.Unloaded -= OnUnloaded;
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is TabControl tc)
                _initialSelectionSkipped.Remove(tc);
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || sender is not TabControl tabControl)
                return;

            // 初回表示（起動時の選択）ではアニメーションしない
            if (_initialSelectionSkipped.Remove(tabControl))
                return;

            RunFadeIn(tabControl);
        }

        private static void RunFadeIn(TabControl tabControl)
        {
            var contentHost = VisualTreeHelperExtensions.FindVisualChild<ContentPresenter>(tabControl);
            if (contentHost == null)
            {
                tabControl.Dispatcher.BeginInvoke(() => RunFadeIn(tabControl), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            // コンテンツ本体（TabContentControl または ContentPresenter の直下）を対象にする
            var content = VisualTreeHelperExtensions.FindVisualChild<TabContentControl>(contentHost) as UIElement
                ?? VisualTreeHelper.GetChild(contentHost, 0) as UIElement;
            if (content == null)
            {
                tabControl.Dispatcher.BeginInvoke(() => RunFadeIn(tabControl), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            if (!WindowSettings.MicroAnimationsEnabled)
            {
                content.BeginAnimation(UIElement.OpacityProperty, null);
                content.Opacity = 1;
                return;
            }

            content.Opacity = StartOpacity;
            var animation = new DoubleAnimation(StartOpacity, 1, new Duration(TimeSpan.FromSeconds(DurationSeconds)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            content.BeginAnimation(UIElement.OpacityProperty, animation);
        }

    }
}
