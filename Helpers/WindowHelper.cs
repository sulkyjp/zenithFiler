using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Vanara.PInvoke;

namespace ZenithFiler
{
    public static class WindowHelper
    {
        public static void SnapLeft(Window window)
        {
            if (window == null) return;

            window.WindowState = WindowState.Normal;
            var workArea = GetWorkArea(window);
            var offset = GetWindowBorderOffset(window);
            
            window.Left = workArea.Left - offset.Left;
            window.Top = workArea.Top - offset.Top;
            window.Width = (workArea.Width / 2) + offset.Left + offset.Right;
            window.Height = workArea.Height + offset.Top + offset.Bottom;
        }

        public static void MaximizeWindow(Window window)
        {
            if (window == null) return;

            window.WindowState = window.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        public static void SnapRight(Window window)
        {
            if (window == null) return;

            window.WindowState = WindowState.Normal;
            var workArea = GetWorkArea(window);
            var offset = GetWindowBorderOffset(window);
            
            window.Width = (workArea.Width / 2) + offset.Left + offset.Right;
            window.Height = workArea.Height + offset.Top + offset.Bottom;
            window.Left = workArea.Left + (workArea.Width / 2) - offset.Left;
            window.Top = workArea.Top - offset.Top;
        }

        public static void SnapBottom(Window window)
        {
            if (window == null) return;

            window.WindowState = WindowState.Normal;
            var workArea = GetWorkArea(window);
            var offset = GetWindowBorderOffset(window);

            var halfH = workArea.Height / 2;
            window.Left = workArea.Left - offset.Left;
            window.Top = workArea.Top + halfH - offset.Top;
            window.Width = workArea.Width + offset.Left + offset.Right;
            window.Height = halfH + offset.Top + offset.Bottom;
        }

        public static void SnapBottomLeft(Window window)
        {
            if (window == null) return;

            window.WindowState = WindowState.Normal;
            var workArea = GetWorkArea(window);
            var offset = GetWindowBorderOffset(window);

            var halfW = workArea.Width / 2;
            var halfH = workArea.Height / 2;
            window.Left = workArea.Left - offset.Left;
            window.Top = workArea.Top + halfH - offset.Top;
            window.Width = halfW + offset.Left + offset.Right;
            window.Height = halfH + offset.Top + offset.Bottom;
        }

        public static void SnapBottomRight(Window window)
        {
            if (window == null) return;

            window.WindowState = WindowState.Normal;
            var workArea = GetWorkArea(window);
            var offset = GetWindowBorderOffset(window);

            var halfW = workArea.Width / 2;
            var halfH = workArea.Height / 2;
            window.Left = workArea.Left + halfW - offset.Left;
            window.Top = workArea.Top + halfH - offset.Top;
            window.Width = halfW + offset.Left + offset.Right;
            window.Height = halfH + offset.Top + offset.Bottom;
        }

        private static Thickness GetWindowBorderOffset(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                
                // GetWindowRect: 見えない境界線を含む矩形
                User32.GetWindowRect(handle, out RECT windowRect);
                
                // DWMWA_EXTENDED_FRAME_BOUNDS: 実際に見える境界線の矩形
                if (DwmApi.DwmGetWindowAttribute(handle, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT frameRect) == HRESULT.S_OK)
                {
                    var source = PresentationSource.FromVisual(window);
                    var matrix = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

                    return new Thickness(
                        (frameRect.left - windowRect.left) / matrix.M11,
                        (frameRect.top - windowRect.top) / matrix.M22,
                        (windowRect.right - frameRect.right) / matrix.M11,
                        (windowRect.bottom - frameRect.bottom) / matrix.M22
                    );
                }
            }
            catch
            {
                // エラー時はフォールバック
            }
            // DWM が使えない場合はシステムのリサイズ枠の厚さで隙間を解消
            return new Thickness(
                SystemParameters.ResizeFrameVerticalBorderWidth,
                SystemParameters.ResizeFrameHorizontalBorderHeight,
                SystemParameters.ResizeFrameVerticalBorderWidth,
                SystemParameters.ResizeFrameHorizontalBorderHeight);
        }

        public static Rect GetWorkArea(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var monitor = User32.MonitorFromWindow(handle, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
            var info = new User32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(User32.MONITORINFO)) };
            if (User32.GetMonitorInfo(monitor, ref info))
            {
                var source = PresentationSource.FromVisual(window);
                var matrix = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
                
                return new Rect(
                    info.rcWork.left / matrix.M11,
                    info.rcWork.top / matrix.M22,
                    (info.rcWork.right - info.rcWork.left) / matrix.M11,
                    (info.rcWork.bottom - info.rcWork.top) / matrix.M22
                );
            }
            return SystemParameters.WorkArea;
        }
    }
}
