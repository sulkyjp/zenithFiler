using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ZenithFiler
{
    public partial class InputBox : Window
    {
        // ウィンドウを破棄せず Hide で再利用するためシングルトンとして保持
        private static InputBox? _instance;

        /// <summary>InputBox がモーダル表示中かどうか。外部コードがフォーカス奪取を抑制するために参照する。</summary>
        public static bool IsOpen => _instance?._frame != null;

        private DispatcherFrame? _frame;
        private bool _dialogResult;
        private bool _isFirstShow = true;
        private bool _originalTopmost;

        public string InputText => InputTextBox.Text;

        /// <summary>
        /// ウィンドウを再利用するゼロレイテンシ表示用ヘルパー。
        /// ShowDialog() の代わりに DispatcherFrame でモーダルループを管理し、
        /// 完了後は Hide() するのみでウィンドウを破棄しない。
        /// </summary>
        public static string? ShowDialog(string prompt, string defaultText = "", bool selectNameWithoutExtension = false)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            string? resultText = null;

            dispatcher.Invoke(() =>
            {
                // 初回のみ生成。以降は同一インスタンスを再利用する
                _instance ??= new InputBox();

                var dialog = _instance;
                dialog.Prepare(prompt, defaultText, selectNameWithoutExtension);

                var main = Application.Current?.MainWindow;
                if (main != null && main.IsLoaded)
                {
                    dialog.Owner = main;
                    dialog._originalTopmost = main.Topmost;
                }

                // キーボード操作モードで非表示になっているカーソルを復元
                Mouse.OverrideCursor = null;

                if (dialog.ShowAsModal())
                    resultText = dialog.InputText;

            }, DispatcherPriority.Send);

            return resultText;
        }

        public InputBox()
        {
            InitializeComponent();

            // HWND を事前生成してテンプレート・レイアウトを処理済みにする（初回 Show の遅延を削減）
            new WindowInteropHelper(this).EnsureHandle();
            UpdateLayout();

            Loaded += OnLoaded;
            IsVisibleChanged += OnIsVisibleChanged;
            ContentRendered += OnContentRendered;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Loaded: レイアウト完了直後にフォーカス設定
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;

            // 背景色はハードコード済みのため Opacity ハックは不要（黒フレームの原因になり得る）
            if (_isFirstShow)
                _isFirstShow = false;

            // 描画完了後にフォーカスを設定（Input 優先度）
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
        }

        private void OnContentRendered(object? sender, EventArgs e)
        {
            // ContentRendered でも念押しフォーカス
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
        }

        /// <summary>OS レベルでウィンドウを活性化し、TextBox に強制フォーカス＋全選択。</summary>
        private void ForceActivateAndFocus()
        {
            if (!IsVisible || _frame == null) return;
            Activate();
            FocusTextBox();
        }

        private void FocusTextBox()
        {
            InputTextBox.Focus();
            Keyboard.Focus(InputTextBox);
            InputTextBox.SelectAll();
        }

        /// <summary>DispatcherFrame を使ったカスタムモーダルループ。ShowDialog() と違いウィンドウを破棄しない。</summary>
        private bool ShowAsModal()
        {
            _dialogResult = false;
            _frame = new DispatcherFrame();

            // Topmost トリック: 一時的に最前面にして OS レベルで確実にアクティブ化
            Topmost = true;

            Show();

            // Show() 直後: Topmost を戻しつつ、多段階でフォーカスを確保
            // ① Loaded 優先度: レンダリング完了直後（他コンポーネントのノイズより先に発火）
            Dispatcher.BeginInvoke(() =>
            {
                Topmost = _originalTopmost;
                ForceActivateAndFocus();
            }, DispatcherPriority.Loaded);
            // ② Input 優先度: UI 描画サイクルが安定した後の念押し
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
            // ③ ContextIdle 優先度: FileSystemWatcher 等の遅延イベント後の最終防衛
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.ContextIdle);
            // ④ ApplicationIdle 優先度: すべての Dispatcher 作業完了後の絶対保証
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.ApplicationIdle);

            Dispatcher.PushFrame(_frame);

            // アプリ終了などでフレームが外部から抜けた場合も確実に null にする
            _frame = null;
            if (IsVisible) Hide();

            return _dialogResult;
        }

        private void ExitFrame()
        {
            if (_frame == null) return;
            _frame.Continue = false;
            _frame = null;
        }

        /// <summary>ウィンドウを破棄させず Hide で代替する。モーダルループ外（アプリ終了など）は通常通り閉じる。</summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_frame != null)
            {
                e.Cancel = true;
                _dialogResult = false;
                ExitFrame();
                return;
            }
            base.OnClosing(e);
        }

        /// <summary>Escape キーをトンネリングイベントで捕捉してキャンセル扱いにする。</summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitFrame();
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        private void Prepare(string prompt, string defaultText, bool selectNameWithoutExtension)
        {
            Title = prompt;

            if (selectNameWithoutExtension)
            {
                string namePart = Path.GetFileNameWithoutExtension(defaultText);
                string ext = Path.GetExtension(defaultText);
                InputTextBox.Text = namePart;
                InputTextBox.SelectAll();
                ExtensionLabel.Text = ext;
                ExtensionLabel.Visibility = ext.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                InputTextBox.Text = defaultText;
                InputTextBox.SelectAll();
                ExtensionLabel.Text = string.Empty;
                ExtensionLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _dialogResult = true;
            ExitFrame();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ExitFrame(); // _dialogResult は false のまま
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Text = DateTime.Now.ToString("yyyyMMdd");
            InputTextBox.Select(InputTextBox.Text.Length, 0);
            InputTextBox.Focus();
        }
    }
}
