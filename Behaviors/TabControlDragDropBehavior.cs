using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Collections;
using System.Collections.ObjectModel;
using ZenithFiler.Helpers;
using ZenithFiler.ViewModels;

namespace ZenithFiler
{
    public class TabControlDragDropBehavior
    {
        private const double DragThreshold = 10.0;
        /// <summary>タブヘッダー領域の高さ（XAML の Grid Row="0" Height="32" + 余白）。</summary>
        private const double HeaderAreaHeight = 36.0;

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TabControlDragDropBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TabControl tabControl)
            {
                if ((bool)e.NewValue)
                {
                    tabControl.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                    tabControl.MouseMove += OnMouseMove;
                    tabControl.DragOver += OnDragOver;
                    tabControl.DragLeave += OnDragLeave;
                    tabControl.Drop += OnDrop;
                    tabControl.AllowDrop = true;
                }
                else
                {
                    tabControl.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                    tabControl.MouseMove -= OnMouseMove;
                    tabControl.DragOver -= OnDragOver;
                    tabControl.DragLeave -= OnDragLeave;
                    tabControl.Drop -= OnDrop;
                    tabControl.AllowDrop = false;
                }
            }
        }

        private static Point _startPoint;
        private static object? _draggedItem;
        private static TabItem? _draggedContainer;
        private static TabPanelInsertionAdorner? _insertionAdorner;
        private static DragAdorner? _dragAdorner;
        private static AdornerLayer? _dragAdornerLayer;

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TabControl tabControl)
            {
                _startPoint = e.GetPosition(null);

                var source = e.OriginalSource as DependencyObject;
                var container = FindAncestor<TabItem>(source);

                if (container != null)
                {
                    // 閉じるボタンなどのボタン上でのクリックはドラッグ開始しない
                    var button = FindAncestor<Button>(source);
                    if (button != null)
                    {
                        _draggedItem = null;
                        _draggedContainer = null;
                        return;
                    }

                    _draggedContainer = container;
                    _draggedItem = container.DataContext;
                }
                else
                {
                    _draggedItem = null;
                    _draggedContainer = null;
                }
            }
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItem == null || e.LeftButton == MouseButtonState.Released) return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _startPoint.X) > DragThreshold ||
                Math.Abs(pos.Y - _startPoint.Y) > DragThreshold)
            {
                if (sender is TabControl tabControl)
                {
                    var data = new DataObject();
                    data.SetData("ZenithFilerTabItem", _draggedItem);

                    // ゴーストイメージ（DragAdorner）を表示
                    var tabTitle = (_draggedItem as TabItemViewModel)?.TabTitle ?? "";
                    ShowDragAdorner(tabControl, tabTitle);

                    // ドラッグ元タブを半透明に（Chrome 風）
                    if (_draggedContainer != null) _draggedContainer.Opacity = 0.4;

                    tabControl.GiveFeedback += OnGiveFeedback;
                    try
                    {
                        DragDrop.DoDragDrop(tabControl, data, DragDropEffects.Move);
                    }
                    finally
                    {
                        tabControl.GiveFeedback -= OnGiveFeedback;
                        RemoveDragAdorner();
                        ClearInsertionAdorner();

                        // ドラッグ元タブの透明度を復元
                        if (_draggedContainer != null) _draggedContainer.Opacity = 1.0;
                    }
                }
                _draggedItem = null;
                _draggedContainer = null;
            }
        }

        private static void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            _dragAdorner?.UpdatePositionFromCursor();
            e.UseDefaultCursors = true;
            e.Handled = true;
        }

        private static void ShowDragAdorner(TabControl tabControl, string text)
        {
            var layer = AdornerLayer.GetAdornerLayer(tabControl);
            if (layer != null)
            {
                _dragAdornerLayer = layer;
                _dragAdorner = new DragAdorner(tabControl, text);
                layer.Add(_dragAdorner);
                _dragAdorner.UpdatePosition(Mouse.GetPosition(tabControl));
            }
        }

        private static void RemoveDragAdorner()
        {
            if (_dragAdorner != null && _dragAdornerLayer != null)
            {
                _dragAdornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
                _dragAdornerLayer = null;
            }
        }

        // ────────────────────────────────────────────
        //  DragOver: 挿入インジケーター＋カーソル制御
        // ────────────────────────────────────────────

        private static void OnDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("ZenithFilerTabItem"))
            {
                // フォルダドロップ等は DockPanel の TabHeader_DragOver に伝播させる
                return;
            }

            if (sender is not TabControl tabControl)
            {
                e.Handled = true;
                return;
            }

            var draggedItem = e.Data.GetData("ZenithFilerTabItem");
            var itemsSource = tabControl.ItemsSource as IList;
            var tabPanel = FindTabPanel(tabControl);

            if (tabPanel == null || itemsSource == null || draggedItem == null)
            {
                ClearInsertionAdorner();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            bool isCrossPane = !itemsSource.Contains(draggedItem);

            // ヘッダー領域判定（タブストリップ外はインジケーター非表示だがカーソルは Move 維持）
            var mouseInTabControl = e.GetPosition(tabControl);
            bool isInHeaderArea = mouseInTabControl.Y >= -4 && mouseInTabControl.Y <= HeaderAreaHeight;

            if (!isInHeaderArea)
            {
                ClearInsertionAdorner();
                // クロスペインならドロップ可（末尾追加）、同一ペインならキャンセル
                e.Effects = isCrossPane ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // TabPanel 座標でのマウス位置から挿入位置を計算
            var mouseInPanel = e.GetPosition(tabPanel);
            var (insertIndex, lineX) = CalcInsertionInfo(tabPanel, itemsSource, mouseInPanel);

            // 同一ペイン: 移動不要位置ならインジケーター非表示（ただしカーソルは Move を維持）
            if (!isCrossPane)
            {
                int oldIndex = itemsSource.IndexOf(draggedItem);
                if (oldIndex == insertIndex || oldIndex == insertIndex - 1)
                {
                    ClearInsertionAdorner();
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                    return;
                }
            }

            // 挿入インジケーターを表示
            ShowInsertionAdorner(tabPanel, lineX);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private static void OnDragLeave(object sender, DragEventArgs e)
        {
            // 子要素間の遷移による疑似 DragLeave でのフリッカーを防ぐ
            if (sender is TabControl tabControl)
            {
                var pos = e.GetPosition(tabControl);
                if (pos.X >= -2 && pos.Y >= -2 &&
                    pos.X <= tabControl.ActualWidth + 2 && pos.Y <= tabControl.ActualHeight + 2)
                {
                    return;
                }
            }
            ClearInsertionAdorner();
        }

        // ────────────────────────────────────────────
        //  Drop: 並べ替え・ペイン間移動を実行
        // ────────────────────────────────────────────

        private static void OnDrop(object sender, DragEventArgs e)
        {
            ClearInsertionAdorner();

            if (!e.Data.GetDataPresent("ZenithFilerTabItem") || sender is not TabControl tabControl)
            {
                // フォルダドロップ等は DockPanel の TabHeader_Drop に伝播させる
                return;
            }

            var draggedItem = e.Data.GetData("ZenithFilerTabItem");
            var itemsSource = tabControl.ItemsSource as IList;
            var tabPanel = FindTabPanel(tabControl);

            if (itemsSource == null || draggedItem == null)
            {
                e.Handled = true;
                return;
            }

            bool isCrossPane = !itemsSource.Contains(draggedItem);

            // ヘッダー領域判定
            var mouseInTabControl = e.GetPosition(tabControl);
            bool isInHeaderArea = mouseInTabControl.Y >= -4 && mouseInTabControl.Y <= HeaderAreaHeight;

            // 挿入インデックスを計算（ヘッダー外はデフォルト -1 = 末尾）
            int targetIndex = -1;
            if (isInHeaderArea && tabPanel != null)
            {
                var mouseInPanel = e.GetPosition(tabPanel);
                (targetIndex, _) = CalcInsertionInfo(tabPanel, itemsSource, mouseInPanel);
            }

            // ── クロスペイン移動 ──
            if (isCrossPane && draggedItem is TabItemViewModel tabVm
                && tabControl.DataContext is FilePaneViewModel targetPane
                && tabVm.ParentPane != targetPane)
            {
                var mainVm = Application.Current?.MainWindow?.DataContext as MainViewModel;
                mainVm?.MoveTabToPane(tabVm, targetPane, targetIndex);
                e.Handled = true;
                return;
            }

            // ── 同一ペイン: ヘッダー領域外ならキャンセル ──
            if (!isInHeaderArea)
            {
                e.Handled = true;
                return;
            }

            // ── 同一ペイン内の並べ替え ──
            int oldIndex = itemsSource.IndexOf(draggedItem);
            if (oldIndex == -1 || targetIndex == -1)
            {
                e.Handled = true;
                return;
            }

            // Move 用のインデックス補正: 自身が抜ける分を考慮
            int newIndex = targetIndex;
            if (oldIndex < newIndex) newIndex--;
            if (newIndex < 0) newIndex = 0;

            if (oldIndex != newIndex)
            {
                var path = (draggedItem as TabItemViewModel)?.CurrentPath ?? "";
                _ = App.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (itemsSource is ObservableCollection<TabItemViewModel> tabs)
                    {
                        tabs.Move(oldIndex, newIndex);
                    }
                    else
                    {
                        itemsSource.RemoveAt(oldIndex);
                        itemsSource.Insert(newIndex, draggedItem);
                    }
                });
                _ = App.FileLogger.LogAsync($"[Tab] Reordered: {path} to Index {newIndex}");
            }
            e.Handled = true;
        }

        // ────────────────────────────────────────────
        //  挿入インデックスの精密計算（TabPanel ベース）
        // ────────────────────────────────────────────

        /// <summary>
        /// TabPanel 内の各 TabItem の境界を走査し、マウス X 座標から
        /// 挿入インデックスとインジケーター表示用の X 座標を返す。
        /// </summary>
        private static (int index, double lineX) CalcInsertionInfo(TabPanel tabPanel, IList itemsSource, Point mouseInTabPanel)
        {
            int count = itemsSource.Count;
            double lastRightEdge = 0;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(tabPanel); i++)
            {
                var child = VisualTreeHelper.GetChild(tabPanel, i) as TabItem;
                if (child == null) continue;

                // TabItem の TabPanel 内座標での領域を取得
                GeneralTransform transform;
                try { transform = child.TransformToAncestor(tabPanel); }
                catch { continue; }

                var topLeft = transform.Transform(new Point(0, 0));
                double leftEdge = topLeft.X;
                double rightEdge = leftEdge + child.ActualWidth;
                double centerX = leftEdge + child.ActualWidth / 2;

                int dataIndex = itemsSource.IndexOf(child.DataContext);
                if (dataIndex < 0) continue;

                lastRightEdge = Math.Max(lastRightEdge, rightEdge);

                // マウスがこのタブの中心より左 → このタブの前に挿入
                if (mouseInTabPanel.X < centerX)
                {
                    return (dataIndex, leftEdge);
                }
            }

            // 全てのタブの右側 → 末尾に挿入
            return (count, lastRightEdge);
        }

        // ────────────────────────────────────────────
        //  挿入インジケーター（TabPanel レベル・アドーナー）
        // ────────────────────────────────────────────

        private static void ShowInsertionAdorner(TabPanel tabPanel, double xPosition)
        {
            // 既存アドーナーが同じ TabPanel 上にあれば位置のみ更新
            if (_insertionAdorner != null && _insertionAdorner.AdornedElement == tabPanel)
            {
                _insertionAdorner.UpdatePosition(xPosition);
                return;
            }

            ClearInsertionAdorner();
            var layer = AdornerLayer.GetAdornerLayer(tabPanel);
            if (layer != null)
            {
                _insertionAdorner = new TabPanelInsertionAdorner(tabPanel, xPosition);
                layer.Add(_insertionAdorner);
                _insertionAdorner.FadeIn();
            }
        }

        private static void ClearInsertionAdorner()
        {
            if (_insertionAdorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(_insertionAdorner.AdornedElement);
                layer?.Remove(_insertionAdorner);
                _insertionAdorner = null;
            }
        }

        // ────────────────────────────────────────────
        //  ユーティリティ
        // ────────────────────────────────────────────

        private static TabPanel? FindTabPanel(TabControl tabControl)
        {
            return VisualTreeHelperExtensions.FindVisualChild<TabPanel>(tabControl);
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }

    // ────────────────────────────────────────────
    //  TabPanel 上に挿入位置の青い縦線を描画するアドーナー
    // ────────────────────────────────────────────

    public class TabPanelInsertionAdorner : Adorner
    {
        private double _xPosition;
        private readonly Brush _lineBrush;
        private readonly Pen _linePen;

        public TabPanelInsertionAdorner(UIElement adornedElement, double xPosition)
            : base(adornedElement)
        {
            _xPosition = xPosition;
            IsHitTestVisible = false;

            _lineBrush = Application.Current.Resources["AccentBrush"] as Brush ?? Brushes.DodgerBlue;
            _linePen = new Pen(_lineBrush, 2.5);
            try { _linePen.Freeze(); } catch { /* brush が Frozen でない場合は無視 */ }
        }

        public void UpdatePosition(double x)
        {
            if (Math.Abs(_xPosition - x) > 0.5)
            {
                _xPosition = x;
                InvalidateVisual();
            }
        }

        /// <summary>表示時に短いフェードインアニメーションを実行する。</summary>
        public void FadeIn()
        {
            Opacity = 0;
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            double height = AdornedElement.RenderSize.Height;
            var start = new Point(_xPosition, 0);
            var end = new Point(_xPosition, height);

            drawingContext.DrawLine(_linePen, start, end);
            drawingContext.DrawEllipse(_lineBrush, null, start, 3, 3);
            drawingContext.DrawEllipse(_lineBrush, null, end, 3, 3);
        }
    }
}
