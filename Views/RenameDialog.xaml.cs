using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ZenithFiler
{
    public partial class RenameDialog : Window
    {
        // ウィンドウを破棄せず Hide で再利用するためシングルトンとして保持
        private static RenameDialog? _instance;

        /// <summary>RenameDialog がモーダル表示中かどうか。外部コードがフォーカス奪取を抑制するために参照する。</summary>
        public static bool IsOpen => _instance?._frame != null;

        private DispatcherFrame? _frame;
        private bool _dialogResult;
        private bool _isFirstShow = true;
        private bool _originalTopmost;
        private List<string> _historyItems = new();
        private string _parentFolderName = string.Empty;
        private List<CustomRenameButton> _customButtons = new();

        /// <summary>親フォルダ名の先頭 8桁日付+アンダースコア を除去する正規表現。</summary>
        private static readonly Regex DatePrefixRegex = new(@"^\d{8}_", RegexOptions.Compiled);

        public string InputText => InputTextBox.Text;

        /// <summary>
        /// リネーム専用ダイアログを表示します。
        /// </summary>
        public static string? ShowDialog(string prompt, string defaultText, string parentFolderPath, bool selectNameWithoutExtension = false)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            string? resultText = null;

            dispatcher.Invoke(() =>
            {
                _instance ??= new RenameDialog();

                var dialog = _instance;
                dialog.Prepare(prompt, defaultText, parentFolderPath, selectNameWithoutExtension);

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

        public RenameDialog()
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
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;

            if (_isFirstShow)
                _isFirstShow = false;

            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
        }

        private void OnContentRendered(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
        }

        /// <summary>OS レベルでウィンドウを活性化し、TextBox に強制フォーカス。</summary>
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

            Topmost = true;
            Show();

            Dispatcher.BeginInvoke(() =>
            {
                Topmost = _originalTopmost;
                ForceActivateAndFocus();
            }, DispatcherPriority.Loaded);
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.Input);
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.ContextIdle);
            Dispatcher.BeginInvoke(ForceActivateAndFocus, DispatcherPriority.ApplicationIdle);

            Dispatcher.PushFrame(_frame);

            _frame = null;
            if (IsVisible) Hide();

            return _dialogResult;
        }

        private void ExitFrame()
        {
            HistoryPopup.IsOpen = false;
            if (_frame == null) return;
            _frame.Continue = false;
            _frame = null;
        }

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

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (HistoryPopup.IsOpen)
                {
                    HistoryPopup.IsOpen = false;
                    InputTextBox.Focus();
                }
                else
                {
                    ExitFrame();
                }
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        private void Prepare(string prompt, string defaultText, string parentFolderPath, bool selectNameWithoutExtension)
        {
            Title = prompt;

            // 拡張子分離（InputBox と同一ロジック）
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

            // 日付ボタンの Content を本日の日付で更新
            string today = DateTime.Now.ToString("yyyyMMdd");
            DateBtn1.Content = today;
            DateBtn2.Content = today + "_";
            DateBtn3.Content = "_" + today;

            // コンテキストボタン（親フォルダ名）
            _parentFolderName = string.Empty;
            if (!string.IsNullOrEmpty(parentFolderPath))
            {
                string? dirName = Path.GetFileName(parentFolderPath);
                // ドライブルート（例: C:\）の場合 GetFileName は空文字を返す
                if (!string.IsNullOrEmpty(dirName))
                {
                    _parentFolderName = dirName;
                }
            }

            if (!string.IsNullOrEmpty(_parentFolderName))
            {
                ParentFolderBtn.Content = _parentFolderName;
                ParentFolderBtn.ToolTip = _parentFolderName;
                ParentFolderBtn.Tag = _parentFolderName;
                ParentFolderBtn.Visibility = Visibility.Visible;

                // 議事ログ生成: 親フォルダ名の先頭が 8桁数字+_ の場合は除去
                string cleanParent = DatePrefixRegex.Replace(_parentFolderName, "");
                string dateParent = today + "_議事ログ_" + cleanParent;
                DateParentBtn.Content = dateParent;
                DateParentBtn.ToolTip = dateParent;
                DateParentBtn.Tag = dateParent;
                DateParentBtn.Visibility = Visibility.Visible;
            }
            else
            {
                ParentFolderBtn.Visibility = Visibility.Collapsed;
                DateParentBtn.Visibility = Visibility.Collapsed;
            }

            // 履歴読み込み
            LoadHistory();

            // カスタムボタン読み込み
            LoadCustomButtons();

            // ポップアップは閉じた状態で開始
            HistoryPopup.IsOpen = false;
        }

        // ── 履歴 ──

        private async void LoadHistory()
        {
            try
            {
                _historyItems = await App.Database.GetRenameHistoryAsync().ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() =>
                {
                    HistoryList.Items.Clear();
                    foreach (var item in _historyItems)
                    {
                        HistoryList.Items.Add(item);
                    }
                    // 履歴がなければトグルボタンも非表示
                    HistoryToggleBtn.Visibility = _historyItems.Count > 0
                        ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[RenameDialog] LoadHistory failed: {ex.Message}");
            }
        }

        private void ToggleHistoryPopup()
        {
            if (HistoryList.Items.Count == 0) return;
            HistoryPopup.IsOpen = !HistoryPopup.IsOpen;
            if (HistoryPopup.IsOpen)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (HistoryList.Items.Count > 0)
                    {
                        HistoryList.SelectedIndex = 0;
                        var container = HistoryList.ItemContainerGenerator
                            .ContainerFromIndex(0) as ListBoxItem;
                        container?.Focus();
                    }
                }, DispatcherPriority.Input);
            }
        }

        private void SelectHistoryItem()
        {
            if (HistoryList.SelectedItem is string selected)
            {
                InputTextBox.Text = selected;
                InputTextBox.Select(InputTextBox.Text.Length, 0);
                HistoryPopup.IsOpen = false;
                InputTextBox.Focus();
            }
        }

        // ── カスタムボタン ──

        private async void LoadCustomButtons()
        {
            try
            {
                _customButtons = await App.Database.GetCustomRenameButtonsAsync().ConfigureAwait(false);
                await Dispatcher.InvokeAsync(RebuildCustomButtons);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[RenameDialog] LoadCustomButtons failed: {ex.Message}");
            }
        }

        private void RebuildCustomButtons()
        {
            CustomButtonsHost.Items.Clear();
            foreach (var cb in _customButtons)
            {
                var btn = CreateCustomButton(cb);
                CustomButtonsHost.Items.Add(btn);
            }
        }

        private Button CreateCustomButton(CustomRenameButton cb)
        {
            var btn = new Button
            {
                Content = cb.DisplayText,
                Tag = cb,
                ToolTip = cb.InsertText,
                MinHeight = 26,
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 6, 4),
                FontSize = 12,
                Foreground = Application.Current.Resources["TextBrush"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Black,
                Background = Application.Current.Resources["InputBackgroundBrush"] as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                MaxWidth = 260,
                Template = (ControlTemplate)FindResource("TrimmedButtonTemplate"),
            };
            btn.Click += CustomButton_Click;

            // 右クリックメニュー
            var menu = new ContextMenu();
            var deleteItem = new MenuItem { Header = "このボタンを削除" };
            deleteItem.Click += (s, _) => DeleteCustomButton(cb, btn);
            menu.Items.Add(deleteItem);
            btn.ContextMenu = menu;

            return btn;
        }

        private void CustomButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CustomRenameButton cb)
            {
                InputTextBox.Text = cb.InsertText;
                InputTextBox.Select(InputTextBox.Text.Length, 0);
                HistoryPopup.IsOpen = false;
                InputTextBox.Focus();
            }
        }

        private async void DeleteCustomButton(CustomRenameButton cb, Button btn)
        {
            try
            {
                await App.Database.DeleteCustomRenameButtonAsync(cb.Id);
                _customButtons.Remove(cb);
                CustomButtonsHost.Items.Remove(btn);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[RenameDialog] DeleteCustomButton failed: {ex.Message}");
            }
        }

        private async void AddCustomButton_Click(object sender, RoutedEventArgs e)
        {
            var text = InputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // 既に同じテキストのボタンがあれば追加しない
            if (_customButtons.Any(b => b.InsertText == text))
            {
                InputTextBox.Focus();
                return;
            }

            try
            {
                // 表示テキストは長すぎる場合に省略
                string displayText = text.Length > 30 ? text[..27] + "..." : text;
                var saved = await App.Database.AddCustomRenameButtonAsync(displayText, text);
                if (saved != null)
                {
                    _customButtons.Add(saved);
                    var btn = CreateCustomButton(saved);
                    CustomButtonsHost.Items.Add(btn);
                }
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[RenameDialog] AddCustomButton failed: {ex.Message}");
            }
            InputTextBox.Focus();
        }

        // ── イベントハンドラ ──

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // TextBox や Button 上のクリックは DragMove しない（OriginalSource で判定）
            if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
            if (e.OriginalSource is TextBox) return;
            if (e.OriginalSource is ListBox || e.OriginalSource is ListBoxItem) return;

            try
            {
                if (IsVisible) DragMove();
            }
            catch (InvalidOperationException)
            {
                // ウィンドウ非表示遷移中の例外を無視
            }
        }

        private void DateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string text)
            {
                InputTextBox.Text = text;
                InputTextBox.Select(InputTextBox.Text.Length, 0);
                HistoryPopup.IsOpen = false;
                InputTextBox.Focus();
            }
        }

        private void ContextButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string text)
            {
                InputTextBox.Text = text;
                InputTextBox.Select(InputTextBox.Text.Length, 0);
                HistoryPopup.IsOpen = false;
                InputTextBox.Focus();
            }
        }

        private void HistoryToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleHistoryPopup();
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && !HistoryPopup.IsOpen)
            {
                ToggleHistoryPopup();
                e.Handled = true;
            }
        }

        private void HistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SelectHistoryItem();
        }

        private void HistoryList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SelectHistoryItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && HistoryList.SelectedIndex == 0)
            {
                // 先頭で↑を押したら TextBox に戻る
                HistoryPopup.IsOpen = false;
                InputTextBox.Focus();
                InputTextBox.Select(InputTextBox.Text.Length, 0);
                e.Handled = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _dialogResult = true;
            string text = InputTextBox.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _ = System.Threading.Tasks.Task.Run(() => App.Database.SaveRenameHistoryAsync(text));
            }
            ExitFrame();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ExitFrame();
        }
    }
}
