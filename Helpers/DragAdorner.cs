using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace ZenithFiler
{
    public class DragAdorner : Adorner
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private readonly string _text;
        private readonly Typeface _typeface;
        private readonly Brush _backgroundBrush;
        private readonly Pen _borderPen;
        private readonly Brush _foregroundBrush;
        private double _left;
        private double _top;

        public DragAdorner(UIElement adornedElement, string text) : base(adornedElement)
        {
            _text = text;
            IsHitTestVisible = false;
            
            _typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _backgroundBrush = new SolidColorBrush(Color.FromArgb(220, 40, 40, 40));
            _borderPen = new Pen(Brushes.Gray, 1);
            _foregroundBrush = Brushes.White;
        }

        public void UpdatePosition(Point p)
        {
            _left = p.X;
            _top = p.Y;
            var layer = AdornerLayer.GetAdornerLayer(this);
            layer?.Update(this.AdornedElement);
        }

        /// <summary>
        /// 現在のマウス位置を取得してアドーナーの位置を更新します。
        /// GiveFeedback イベントハンドラ内で呼び出すことを想定しています。
        /// </summary>
        public void UpdatePositionFromCursor()
        {
            if (GetCursorPos(out POINT p))
            {
                var screenPos = new Point(p.X, p.Y);
                try 
                {
                    var relPos = AdornedElement.PointFromScreen(screenPos);
                    UpdatePosition(relPos);
                }
                catch 
                {
                    // 変換に失敗した場合は何もしない
                }
            }
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var result = new GeneralTransformGroup();
            var baseTransform = base.GetDesiredTransform(transform);
            if (baseTransform != null)
            {
                result.Children.Add(baseTransform);
            }
            result.Children.Add(new TranslateTransform(_left, _top));
            return result;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var formattedText = new FormattedText(
                _text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                12,
                _foregroundBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // マウスカーソルから少しずらす
            var textPos = new Point(16, 16);
            var rect = new Rect(textPos.X - 6, textPos.Y - 2, formattedText.Width + 12, formattedText.Height + 4);

            drawingContext.DrawRoundedRectangle(_backgroundBrush, _borderPen, rect, 2, 2);
            drawingContext.DrawText(formattedText, textPos);
        }
    }
}
