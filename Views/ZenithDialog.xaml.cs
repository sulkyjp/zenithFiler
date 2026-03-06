using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;

namespace ZenithFiler.Views
{
    public enum ZenithDialogButton { OK, OKCancel, YesNo }
    public enum ZenithDialogIcon { Info, Warning, Error, Question }
    public enum ZenithDialogResult { None, OK, Cancel, Yes, No }

    public partial class ZenithDialog : Window
    {
        // ── アイコン種別→Kind マッピング ──
        private static readonly PackIconLucideKind[] IconKinds =
        [
            PackIconLucideKind.Info,           // Info = 0
            PackIconLucideKind.TriangleAlert,  // Warning = 1
            PackIconLucideKind.CircleX,        // Error = 2
            PackIconLucideKind.CircleHelp,     // Question = 3
        ];

        // ── アイコン種別→リソースキー マッピング ──
        private static readonly string[] IconBrushKeys =
        [
            "AccentBrush",            // Info = 0
            "IndexSearchIconBrush",   // Warning = 1
            "ErrorTextBrush",         // Error = 2
            "AccentBrush",            // Question = 3
        ];

        // ── ボタン定義テーブル（毎回 new Button しない） ──
        private static readonly (string Text, ZenithDialogResult Result, bool IsDefault, bool IsCancel)[][] ButtonDefs =
        [
            [(  "OK",        ZenithDialogResult.OK,     true,  false )],                                        // OK
            [(  "OK",        ZenithDialogResult.OK,     true,  false ), ( "キャンセル", ZenithDialogResult.Cancel, false, true )],  // OKCancel
            [(  "はい",      ZenithDialogResult.Yes,    true,  false ), ( "いいえ",     ZenithDialogResult.No,     false, true )],  // YesNo
        ];

        private static ZenithDialog? _instance;

        private DispatcherFrame? _frame;
        private ZenithDialogResult _result = ZenithDialogResult.None;
        private bool _originalTopmost;
        private ZenithDialogButton _lastButtonLayout = (ZenithDialogButton)(-1); // キャッシュ判定用

        public ZenithDialog()
        {
            InitializeComponent();
            KeyDown += OnKeyDown;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        /// <summary>ZenithDialog がモーダル表示中かどうか。</summary>
        public static bool IsOpen => _instance?._frame != null;

        /// <summary>
        /// カスタムダイアログを表示する。InputBox と同一パターン（シングルトン + DispatcherFrame モーダル）。
        /// </summary>
        public static ZenithDialogResult Show(
            string message,
            string title = "Zenith Filer",
            ZenithDialogButton buttons = ZenithDialogButton.OK,
            ZenithDialogIcon icon = ZenithDialogIcon.Info,
            FrameworkElement? customContent = null)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            ZenithDialogResult result = ZenithDialogResult.None;

            dispatcher.Invoke(() =>
            {
                _instance ??= new ZenithDialog();
                var dialog = _instance;
                dialog.Prepare(message, title, buttons, icon, customContent);

                var main = Application.Current?.MainWindow;
                if (main is { IsLoaded: true })
                {
                    dialog.Owner = main;
                    dialog._originalTopmost = main.Topmost;
                }

                dialog.Show();

                if (main != null)
                    main.Topmost = false;

                dialog._frame = new DispatcherFrame();
                Dispatcher.PushFrame(dialog._frame);

                result = dialog._result;
            });

            return result;
        }

        private void Prepare(string message, string title, ZenithDialogButton buttons, ZenithDialogIcon icon, FrameworkElement? customContent = null)
        {
            _result = ZenithDialogResult.None;
            TitleText.Text = title;
            MessageText.Text = message;

            // アイコン: 配列インデックスで O(1) ルックアップ + テーマ対応ブラシ
            DialogIcon.Kind = IconKinds[(int)icon];
            DialogIcon.Foreground = Application.Current.Resources[IconBrushKeys[(int)icon]] as Brush
                                    ?? Brushes.Gray;

            // カスタムコンテンツ
            if (customContent != null)
            {
                CustomContent.Content = customContent;
                CustomContent.Visibility = Visibility.Visible;
            }
            else
            {
                CustomContent.Content = null;
                CustomContent.Visibility = Visibility.Collapsed;
            }

            // ボタン: レイアウトが前回と同じならスキップ
            if (_lastButtonLayout != buttons)
            {
                BuildButtons(buttons);
                _lastButtonLayout = buttons;
            }
        }

        private void BuildButtons(ZenithDialogButton buttons)
        {
            ButtonPanel.Children.Clear();
            var defs = ButtonDefs[(int)buttons];
            for (int i = 0; i < defs.Length; i++)
            {
                var (text, dialogResult, isDefault, isCancel) = defs[i];
                var btn = new Button
                {
                    Content = text,
                    MinWidth = 96,
                    MinHeight = 30,
                    Padding = new Thickness(14, 4, 14, 4),
                    Margin = new Thickness(6, 0, 6, 0),
                    IsDefault = isDefault,
                    IsCancel = isCancel,
                };
                var capturedResult = dialogResult;
                btn.Click += (_, _) => CloseWithResult(capturedResult);
                ButtonPanel.Children.Add(btn);
            }
        }

        private void CloseWithResult(ZenithDialogResult result)
        {
            // 二重呼び出しガード（OnClosing → CloseWithResult → Hide 循環防止）
            if (_frame == null) return;

            _result = result;
            var frame = _frame;
            _frame = null; // 先にクリアして再入を防ぐ

            Hide();

            if (Owner is Window main)
                main.Topmost = _originalTopmost;

            frame.Continue = false;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseWithResult(ZenithDialogResult.Cancel);
                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsVisible)
            {
                try { DragMove(); } catch { /* ウィンドウ非表示遷移中のレースを安全に無視 */ }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // ウィンドウを破棄せず非表示にする（シングルトン再利用）
            e.Cancel = true;
            CloseWithResult(_result == ZenithDialogResult.None ? ZenithDialogResult.Cancel : _result);
        }

    }
}
