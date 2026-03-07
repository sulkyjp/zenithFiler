using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZenithFiler.Helpers;
using ZenithFiler.Models;

namespace ZenithFiler
{
    /// <summary>
    /// タブ選択に合わせてヘッダー下のアクティブインジケータをスライドさせる添付ビヘイビア。
    /// テンプレート内のインジケータ要素に IsIndicatorPart="True" を付与する。
    /// </summary>
    public static class TabIndicatorSlideBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(TabIndicatorSlideBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        /// <summary>テンプレート内のインジケータ要素に付与し、ビヘイビアが対象として認識する。</summary>
        public static readonly DependencyProperty IsIndicatorPartProperty =
            DependencyProperty.RegisterAttached(
                "IsIndicatorPart",
                typeof(bool),
                typeof(TabIndicatorSlideBehavior),
                new PropertyMetadata(false));

        public static bool GetIsIndicatorPart(DependencyObject obj) => (bool)obj.GetValue(IsIndicatorPartProperty);
        public static void SetIsIndicatorPart(DependencyObject obj, bool value) => obj.SetValue(IsIndicatorPartProperty, value);

        private const double DurationSeconds = 0.25;

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TabControl tabControl)
                return;

            if ((bool)e.NewValue)
            {
                tabControl.Loaded += OnLoaded;
                tabControl.SelectionChanged += OnSelectionChanged;
                tabControl.Unloaded += OnUnloaded;
            }
            else
            {
                tabControl.Loaded -= OnLoaded;
                tabControl.SelectionChanged -= OnSelectionChanged;
                tabControl.Unloaded -= OnUnloaded;
                _indicatorCache.Remove(tabControl);
                _initialPositionSet.Remove(tabControl);
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is TabControl tc)
            {
                _indicatorCache.Remove(tc);
                _initialPositionSet.Remove(tc);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TabControl tc)
                return;
            // テンプレート適用・コンテナ生成後に初期位置を設定
            tc.Dispatcher.BeginInvoke(() => UpdateIndicatorPosition(tc, animate: false),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tc)
                UpdateIndicatorPosition(tc, animate: true);
        }

        private static readonly Dictionary<TabControl, FrameworkElement> _indicatorCache = new();
        private static readonly HashSet<TabControl> _initialPositionSet = new();

        private static FrameworkElement? FindIndicator(TabControl tabControl)
        {
            if (_indicatorCache.TryGetValue(tabControl, out var cached) && cached != null)
                return cached;

            var indicator = VisualTreeHelperExtensions.FindVisualChildByPredicate<FrameworkElement>(tabControl, el =>
                el.GetValue(IsIndicatorPartProperty) is true);

            if (indicator != null)
                _indicatorCache[tabControl] = indicator;

            return indicator;
        }

        private static void UpdateIndicatorPosition(TabControl tabControl, bool animate)
        {
            var indicator = FindIndicator(tabControl) as FrameworkElement;
            if (indicator == null)
                return;

            var headerContainer = indicator.Parent as FrameworkElement;
            if (headerContainer == null)
                return;

            var selectedItem = tabControl.SelectedItem;
            if (selectedItem == null)
            {
                indicator.Visibility = Visibility.Collapsed;
                return;
            }

            var tabItem = tabControl.ItemContainerGenerator.ContainerFromItem(selectedItem) as FrameworkElement;
            if (tabItem == null)
            {
                tabControl.Dispatcher.BeginInvoke(() => UpdateIndicatorPosition(tabControl, animate),
                    System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            var transform = tabItem.TransformToVisual(headerContainer);
            var origin = transform.Transform(new Point(0, 0));
            var left = origin.X;

            indicator.Visibility = Visibility.Visible;

            // タブの実幅に合わせてインジケータ幅を追従させる
            indicator.Width = tabItem.ActualWidth;

            // 初回はアニメーションせずに配置（起動時・初期選択時）
            var isFirstPosition = !_initialPositionSet.Contains(tabControl);
            if (isFirstPosition)
                _initialPositionSet.Add(tabControl);

            if (animate && !isFirstPosition && WindowSettings.ShowTabEffectsEnabled)
            {
                var currentMargin = indicator.Margin;
                var targetMargin = new Thickness(left, 0, 0, 0);

                var marginAnim = new ThicknessAnimation(currentMargin, targetMargin, new Duration(TimeSpan.FromSeconds(DurationSeconds)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                indicator.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
            }
            else
            {
                indicator.Margin = new Thickness(left, 0, 0, 0);
            }
        }

    }
}
