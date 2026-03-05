using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using System.Linq;
using ZenithFiler.Helpers;
namespace ZenithFiler
{
    public partial class FilePaneControl : UserControl
    {
        private Window? _parentWindow;
        private Popup? _tabListPopup;

        public FilePaneControl()
        {
            InitializeComponent();
            Loaded += FilePaneControl_Loaded;
            Unloaded += FilePaneControl_Unloaded;
        }

        private void FilePaneControl_Loaded(object sender, RoutedEventArgs e)
        {
            _parentWindow = Window.GetWindow(this);
            if (_parentWindow != null)
            {
                _parentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
            }

            // タブ一覧ポップアップをリスト表示ボタンの下で×ボタン列が揃うよう右端揃えで表示
            MainTabControl.ApplyTemplate();
            if (MainTabControl.Template?.FindName("TabListPopup", MainTabControl) is Popup tabListPopup)
            {
                _tabListPopup = tabListPopup;
                tabListPopup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point(targetSize.Width - popupSize.Width, targetSize.Height),
                        PopupPrimaryAxis.Vertical)
                };
                tabListPopup.Opened += TabListPopup_Opened;
            }
        }

        private void FilePaneControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null)
            {
                _parentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
                _parentWindow = null;
            }

            if (_tabListPopup != null)
            {
                _tabListPopup.Opened -= TabListPopup_Opened;
                _tabListPopup = null;
            }
        }

        /// <summary>
        /// クリック時にのみペインをアクティブ化し、ファイルリストにフォーカスを移す（ホバーでは切り替えない）。
        /// </summary>
        private void FilePaneControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is FilePaneViewModel vm)
            {
                var window = Window.GetWindow(this);
                if (window?.DataContext is MainViewModel mainVm)
                {
                    // クリック対象が入力系・操作系コントロール（TextBox, Button, ComboBox 等）の場合は、FocusList を呼び出さない
                    // (それぞれのコントロール自体のフォーカス処理・クリック処理を優先させるため)
                    var element = e.OriginalSource as DependencyObject;
                    bool isControl = FindAncestor<TextBox>(element) != null || 
                                     FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(element) != null ||
                                     FindAncestor<ComboBox>(element) != null;

                    if (!ReferenceEquals(mainVm.ActivePane, vm))
                    {
                        // コントロールをクリックした場合は、ActivePane 変更時の自動フォーカス要求を抑制する
                        if (isControl) mainVm.SuppressFocusRequest = true;
                        try
                        {
                            mainVm.ActivePane = vm;
                        }
                        finally
                        {
                            if (isControl) mainVm.SuppressFocusRequest = false;
                        }
                    }

                    if (isControl)
                    {
                        return;
                    }

                    FocusList();
                }
            }
        }

        private void FilePaneControl_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is FilePaneViewModel vm)
            {
                var window = Window.GetWindow(this);
                if (window?.DataContext is MainViewModel mainVm)
                {
                    if (!ReferenceEquals(mainVm.ActivePane, vm))
                    {
                        mainVm.ActivePane = vm;
                    }
                }

                // タブアイテム（ヘッダー）やペイン自体にフォーカスが当たった場合、
                // リストへフォーカスを移すことでショートカットキーを常に有効にする。
                // TextBox 等の入力系コントロール以外からのフォーカス移動であれば、積極的にリストへ誘導する。
                if (e.OriginalSource is TabItem || ReferenceEquals(e.OriginalSource, this))
                {
                    FocusList();
                }
            }
        }
        
        public void FocusActiveTab()
        {
            // すでにこのコントロール内にフォーカスがあり、かつTextBox（パス入力欄など）にフォーカスがある場合は
            // リストへの強制フォーカス移動を行わない（入力作業を妨げないため）
            if (this.IsKeyboardFocusWithin)
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (focused is TextBox)
                {
                    return;
                }
            }

            // 名前付きTabControlがあればそれを使う、なければ探す
            var tabControl = this.FindName("MainTabControl") as TabControl 
                             ?? VisualTreeHelperExtensions.FindVisualChild<TabControl>(this);

            if (tabControl != null)
            {
                // TabContentControlを探す
                // TabControl -> ContentPresenter -> TabContentControl の構造になっているはず
                var tabContent = VisualTreeHelperExtensions.FindVisualChild<TabContentControl>(tabControl);
                if (tabContent != null)
                {
                    tabContent.FocusList();
                }
            }
        }

        public void FocusList()
        {
            FocusActiveTab();
        }

        /// <summary>現在選択中のタブ内の検索バーにフォーカスを移す（Ctrl+F 用）。</summary>
        internal void FocusSearchBox()
        {
            var tabControl = this.FindName("MainTabControl") as TabControl
                             ?? VisualTreeHelperExtensions.FindVisualChild<TabControl>(this);
            if (tabControl == null) return;
            var tabContent = VisualTreeHelperExtensions.FindVisualChild<TabContentControl>(tabControl);
            tabContent?.FocusSearchBox();
        }

        /// <summary>現在選択中のタブ内の検索バーにフォーカスを移し、インデックス検索モードに入る（Ctrl+Shift+F 用）。</summary>
        internal void FocusSearchBoxAndEnterIndexMode()
        {
            var tabControl = this.FindName("MainTabControl") as TabControl
                             ?? VisualTreeHelperExtensions.FindVisualChild<TabControl>(this);
            if (tabControl == null) return;
            var tabContent = VisualTreeHelperExtensions.FindVisualChild<TabContentControl>(tabControl);
            if (tabContent != null)
            {
                tabContent.FocusSearchBox();
                if (tabContent.DataContext is TabItemViewModel vm)
                    vm.EnterIndexSearchModeCommand.Execute(null);
            }
        }

        /// <summary>現在選択中のタブの TabContentControl を取得する。</summary>
        internal TabContentControl? GetActiveTabContent()
        {
            var tabControl = this.FindName("MainTabControl") as TabControl
                             ?? VisualTreeHelperExtensions.FindVisualChild<TabControl>(this);
            if (tabControl == null) return null;
            return VisualTreeHelperExtensions.FindVisualChild<TabContentControl>(tabControl);
        }

        /// <summary>
        /// 現在選択中のタブ内のファイル一覧 ListView を取得する（MainWindow のキー転送用）。
        /// </summary>
        internal ListView? GetActiveFileListView()
        {
            var tabControl = this.FindName("MainTabControl") as TabControl
                             ?? VisualTreeHelperExtensions.FindVisualChild<TabControl>(this);
            if (tabControl == null) return null;
            var tabContent = VisualTreeHelperExtensions.FindVisualChild<TabContentControl>(tabControl);
            return tabContent?.FileListView;
        }

        private void TabHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 既存のタブ項目上でないことを確認
                var element = e.OriginalSource as DependencyObject;
                var tabItem = FindAncestor<TabItem>(element);
                var button = FindAncestor<Button>(element);
                
                // タブ以外の空白部分、またはScrollViewer/TabPanel自体でのダブルクリックで新規タブ作成
                if (tabItem == null && button == null && DataContext is FilePaneViewModel vm)
                {
                    if (vm.AddTabCommand.CanExecute(null))
                    {
                        vm.AddTabCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }

        private void TabHeader_DragOver(object sender, DragEventArgs e)
        {
            // タブの D&D は TabControlDragDropBehavior が処理する（イベントをバブルさせる）
            if (e.Data.GetDataPresent("ZenithFilerTabItem"))
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                e.Data.GetDataPresent(typeof(FavoriteItem)) ||
                e.Data.GetDataPresent(typeof(DirectoryItemViewModel)) ||
                e.Data.GetDataPresent(typeof(HistoryItemViewModel)))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void TabHeader_Drop(object sender, DragEventArgs e)
        {
            // タブの D&D は TabControlDragDropBehavior が処理する（イベントをバブルさせる）
            if (e.Data.GetDataPresent("ZenithFilerTabItem"))
                return;

            if (DataContext is FilePaneViewModel vm)
            {
                string? path = null;

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0)
                    {
                        // 最初のフォルダを採用
                        path = files.FirstOrDefault(f => System.IO.Directory.Exists(f));
                    }
                }
                else if (e.Data.GetDataPresent(typeof(FavoriteItem)))
                {
                    var favItem = e.Data.GetData(typeof(FavoriteItem)) as FavoriteItem;
                    var window = Window.GetWindow(this);
                    if (favItem != null && window?.DataContext is MainViewModel mainVm)
                    {
                        if (await mainVm.Favorites.EnsurePathExistsAsync(favItem))
                        {
                            path = favItem.Path;
                        }
                        else
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }
                else if (e.Data.GetDataPresent(typeof(DirectoryItemViewModel)))
                {
                    var dirItem = e.Data.GetData(typeof(DirectoryItemViewModel)) as DirectoryItemViewModel;
                    path = dirItem?.FullPath;
                }
                else if (e.Data.GetDataPresent(typeof(HistoryItemViewModel)))
                {
                    var historyItem = e.Data.GetData(typeof(HistoryItemViewModel)) as HistoryItemViewModel;
                    path = historyItem?.Path;
                }

                // フォルダがドロップされた場合、タブアイテム上でも空白部分でも新しいタブで開く。
                // 方式A: ドロップイベント内で同期的にタブ追加するとペインがフリーズするため、Dispatcher で遅延実行する。
                if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                {
                    var pathToOpen = path;
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (DataContext is FilePaneViewModel paneVm)
                            paneVm.AddTabWithPathCommand.Execute(pathToOpen);
                    }, DispatcherPriority.Loaded);
                    e.Handled = true;
                }
            }
        }

        private void TabListPopup_Opened(object? sender, EventArgs e)
        {
            if (sender is not Popup popup || popup.Child is not Border border) return;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Frozen 回避: 毎回新しい TranslateTransform インスタンスを割り当て直す
            // ※ XAML 宣言の TranslateTransform は Frozen（シール済み）のため BeginAnimation 不可
            var tt = new TranslateTransform(0, -5);
            border.RenderTransform = tt;
            border.Opacity = 0;

            // Opacity 0→1 (120ms) — キビキビしたフェードイン
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // TranslateY -5→0 (140ms) — 短い距離でスッと降りてくるスライド
            var slideIn = new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease };
            tt.BeginAnimation(TranslateTransform.YProperty, slideIn);
        }

        private void TabListButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is FilePaneViewModel vm)
            {
                vm.ToggleTabListPopupCommand.Execute(null);
            }
            // ボタンクリックで即座に「外側クリック」と扱われて閉じないようにする
            e.Handled = true;
        }

        private void TabListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is TabItemViewModel selectedTab)
            {
                if (DataContext is FilePaneViewModel vm)
                {
                    vm.SelectedTab = selectedTab;
                    vm.IsTabListPopupOpen = false;
                }
            }
        }

        private void TabListCloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 削除ボタンのコマンド実行後にタブ一覧を閉じる
            if (DataContext is FilePaneViewModel vm)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    vm.IsTabListPopupOpen = false;
                }, DispatcherPriority.Input);
            }
        }

        private void ParentWindow_PreviewMouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (DataContext is not FilePaneViewModel vm || !vm.IsTabListPopupOpen)
                return;

            if (e.OriginalSource is not DependencyObject src)
            {
                vm.IsTabListPopupOpen = false;
                return;
            }

            // タブ一覧ボタン自体のクリックなら閉じない（トグル動作はボタン側の Click で制御）
            var listButton = MainTabControl.Template?.FindName("TabListButton", MainTabControl) as Button;
            if (listButton != null && IsDescendantOf(src, listButton))
                return;

            // ポップアップ内（一覧またはその子要素）のクリックなら閉じない
            var popup = MainTabControl.Template?.FindName("TabListPopup", MainTabControl) as System.Windows.Controls.Primitives.Popup;
            if (popup?.Child != null && IsDescendantOf(src, popup.Child))
                return;

            // それ以外（一覧外）のクリックで閉じる
            vm.IsTabListPopupOpen = false;
        }

        private static bool IsDescendantOf(DependencyObject? element, DependencyObject? ancestor)
            => Helpers.VisualTreeExtensions.IsDescendantOf(element, ancestor);

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
}
