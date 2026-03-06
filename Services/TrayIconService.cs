using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace ZenithFiler.Services
{
    /// <summary>タスクトレイ常駐サービス。常駐モード有効時にウィンドウ×ボタンでトレイに最小化する。</summary>
    public class TrayIconService : IDisposable
    {
        private TaskbarIcon? _trayIcon;
        private readonly Window _mainWindow;
        private bool _forceClose;

        public TrayIconService(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Initialize()
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "Zenith Filer",
                ContextMenu = BuildMenu(),
                Icon = LoadEmbeddedIcon()
            };

            _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();

            // 初期表示は常駐モードの設定に従う
            _trayIcon.Visibility = WindowSettings.ResidentModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>WPF 埋め込みリソースからアプリアイコンを読み込む。</summary>
        private static Icon? LoadEmbeddedIcon()
        {
            try
            {
                // Resource として埋め込まれた .ico を WPF リソースストリームから取得
                var uri = new Uri("pack://application:,,,/ZenithFiler;component/assets/app_icon2_1.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo != null)
                    return new Icon(streamInfo.Stream);
            }
            catch { /* フォールバック: 実行ファイルからアイコンを抽出 */ }

            try
            {
                // EXE 自体に埋め込まれたアイコンを取得（ApplicationIcon 指定による）
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    return Icon.ExtractAssociatedIcon(exePath);
            }
            catch { }

            return null;
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();

            var showItem = new MenuItem { Header = "開く" };
            showItem.Click += (_, _) => ShowWindow();
            menu.Items.Add(showItem);

            menu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "終了" };
            exitItem.Click += (_, _) => ForceClose();
            menu.Items.Add(exitItem);

            return menu;
        }

        private void ShowWindow()
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
        }

        /// <summary>常駐モードを無視して完全終了する。</summary>
        public void ForceClose()
        {
            _forceClose = true;
            _mainWindow.Close();
        }

        /// <summary>Closing イベントで呼び出し、常駐モード時にキャンセルすべきかを返す。</summary>
        public bool ShouldCancelClose => WindowSettings.ResidentModeEnabled && !_forceClose;

        /// <summary>ウィンドウを非表示にしてトレイに常駐する。</summary>
        public void HideToTray()
        {
            _mainWindow.Hide();
        }

        /// <summary>トレイアイコンの表示/非表示を切り替える。</summary>
        public void SetVisible(bool visible)
        {
            if (_trayIcon != null)
                _trayIcon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Dispose()
        {
            _trayIcon?.Dispose();
        }
    }
}
