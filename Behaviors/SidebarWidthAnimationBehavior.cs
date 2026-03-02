using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZenithFiler
{
    /// <summary>
    /// ナビペインの幅を、ビュー切替時（お気に入り⇔ツリー/履歴）にスムーズかつモダンなアニメーションで伸縮させるビヘイビア。
    /// 現在の ActualWidth から目標幅へ QuarticEase(EaseOut) で補間し、アニメーション中は BitmapCache でレイアウト負荷を軽減する。
    /// </summary>
    public static class SidebarWidthAnimationBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(SidebarWidthAnimationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        /// <summary>アニメーション時間（秒）。0.3〜0.5の範囲で操作テンポを損なわない長さ。</summary>
        private const double DurationSeconds = 0.4;

        private static EventHandler? _renderingHandler;
        private static (Grid grid, double from, double to, MainViewModel vm, DateTime startTime)? _state;
        private static readonly Dictionary<Grid, (MainViewModel vm, PropertyChangedEventHandler handler)> _handlers = new();

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Grid grid)
                return;

            if ((bool)e.NewValue)
            {
                grid.Loaded += OnGridLoaded;
                grid.Unloaded += OnGridUnloaded;
                if (grid.IsLoaded)
                    Subscribe(grid);
            }
            else
            {
                grid.Loaded -= OnGridLoaded;
                grid.Unloaded -= OnGridUnloaded;
                Unsubscribe(grid);
            }
        }

        private static void OnGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Grid grid)
                Subscribe(grid);
        }

        private static void OnGridUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Grid grid)
                Unsubscribe(grid);
        }

        private static void Subscribe(Grid grid)
        {
            if (grid.DataContext is not MainViewModel vm)
                return;

            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName != nameof(MainViewModel.SidebarMode))
                    return;

                // 表示上の現在幅を起点にする（ウィンドウリサイズ後も破綻しない）
                double from = GetSidebarActualWidth(grid) ?? (vm.SidebarWidth.IsAbsolute ? vm.SidebarWidth.Value : 0);
                double to = vm.TargetSidebarWidth;
                if (Math.Abs(from - to) < 1)
                    return;

                // アニメーション中にモードが再度変わった場合は、現在のアニメーション値を起点に再開
                if (_state is { } state)
                {
                    from = state.vm.SidebarWidth.IsAbsolute ? state.vm.SidebarWidth.Value : from;
                    StopAnimation();
                }

                StartAnimation(grid, from, to, vm);
            };

            _handlers[grid] = (vm, handler);
            vm.PropertyChanged += handler;
        }

        private static void Unsubscribe(Grid grid)
        {
            if (_handlers.TryGetValue(grid, out var pair))
            {
                pair.vm.PropertyChanged -= pair.handler;
                _handlers.Remove(grid);
            }
            StopAnimation();
        }

        /// <summary>ナビペイン（Grid の第1列）の現在の表示幅を取得する。</summary>
        private static double? GetSidebarActualWidth(Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is FrameworkElement fe && Grid.GetColumn(fe) == 0)
                    return fe.ActualWidth > 0 ? fe.ActualWidth : null;
            }
            return null;
        }

        /// <summary>第1列の子要素（Border）を取得し、CacheMode の適用・解除に使う。</summary>
        private static FrameworkElement? GetSidebarElement(Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is FrameworkElement fe && Grid.GetColumn(fe) == 0)
                    return fe;
            }
            return null;
        }

        private static void SetSidebarCacheMode(Grid grid, bool enable)
        {
            var el = GetSidebarElement(grid);
            if (el == null) return;
            if (enable)
                el.SetValue(UIElement.CacheModeProperty, new BitmapCache());
            else
                el.ClearValue(UIElement.CacheModeProperty);
        }

        private static void StartAnimation(Grid grid, double from, double to, MainViewModel vm)
        {
            StopAnimation();
            vm.IsSidebarWidthAnimating = true;
            _state = (grid, from, to, vm, DateTime.UtcNow);
            SetSidebarCacheMode(grid, enable: true);
            _renderingHandler = OnRendering;
            CompositionTarget.Rendering += _renderingHandler;
        }

        private static void StopAnimation()
        {
            if (_state is { } s)
            {
                s.vm.IsSidebarWidthAnimating = false;
                SetSidebarCacheMode(s.grid, enable: false);
                _state = null;
            }
            if (_renderingHandler != null)
            {
                CompositionTarget.Rendering -= _renderingHandler;
                _renderingHandler = null;
            }
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            if (_state is not { } s)
                return;

            double elapsed = (DateTime.UtcNow - s.startTime).TotalSeconds;
            double t = Math.Clamp(elapsed / DurationSeconds, 0, 1);
            // QuarticEase EaseOut: 開始は速く、終止は優しく吸い付く
            double eased = QuarticEaseOut(t);
            // 目標値は毎フレーム再取得（モード再変更や将来の動的目標に対応）
            double to = s.vm.TargetSidebarWidth;
            double value = s.from + (to - s.from) * eased;

            s.vm.SidebarWidth = new GridLength(value);

            if (t >= 1)
            {
                s.vm.SidebarWidth = new GridLength(to);
                StopAnimation();
            }
        }

        /// <summary>QuarticEase EaseOut（終止がなめらかに吸い付く）。</summary>
        private static double QuarticEaseOut(double t)
        {
            return 1 - Math.Pow(1 - t, 4);
        }
    }
}
