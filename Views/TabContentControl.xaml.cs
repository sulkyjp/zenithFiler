using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using MahApps.Metro.IconPacks;
using ZenithFiler.Helpers;
using ZenithFiler.Services;
namespace ZenithFiler
{
    public partial class TabContentControl : UserControl
    {
        private INotifyCollectionChanged? _itemsViewSubscription;
        private DispatcherTimer? _focusRestoreClearTimer;
        private DispatcherTimer? _breadcrumbPopupLeaveCheckTimer;
        private DispatcherTimer? _breadcrumbPopupOpenDelayTimer;
        /// <summary>アイコンビュー列数計算のデバウンス用。SizeChanged 連打でレイアウト連鎖を防ぐ。</summary>
        private DispatcherTimer? _iconColumnCountDebounceTimer;
        private double _pendingIconColumnWidth;
        private string _incrementalSearchBuffer = string.Empty;
        private DispatcherTimer? _incrementalSearchTimer;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private ViewBase? _defaultView;
        private TabItemViewModel? _tabVmSubscription;

        internal ListBox? GetActiveFileList()
        {
            if (DataContext is not TabItemViewModel vm) return null;
            return vm.FileViewMode == FileViewMode.Details ? FileListView : FileListBox;
        }

        public static readonly DependencyProperty SearchPathColumnWidthProperty =
            DependencyProperty.Register(nameof(SearchPathColumnWidth), typeof(double), typeof(TabContentControl), new PropertyMetadata(300.0));

        public double SearchPathColumnWidth
        {
            get { return (double)GetValue(SearchPathColumnWidthProperty); }
            set { SetValue(SearchPathColumnWidthProperty, value); }
        }

        // 方針H-2: アイコンビューの UniformGrid 列数（幅とアイテムサイズから動的に計算）
        public static readonly DependencyProperty IconColumnCountProperty =
            DependencyProperty.Register(nameof(IconColumnCount), typeof(int), typeof(TabContentControl), new PropertyMetadata(5));

        public int IconColumnCount
        {
            get { return (int)GetValue(IconColumnCountProperty); }
            set { SetValue(IconColumnCountProperty, value); }
        }

        // 方針H-2 右端調整: UniformGrid の固定幅（列数×アイテム幅）。左寄せで余剰を右端に集約。
        public static readonly DependencyProperty IconViewGridWidthProperty =
            DependencyProperty.Register(nameof(IconViewGridWidth), typeof(double), typeof(TabContentControl), new PropertyMetadata(0.0));

        public double IconViewGridWidth
        {
            get { return (double)GetValue(IconViewGridWidthProperty); }
            set { SetValue(IconViewGridWidthProperty, value); }
        }

        // ドラッグ中かどうかを示す依存関係プロパティ（XAMLでのスタイル制御用）
        public static readonly DependencyProperty IsDraggingProperty =
            DependencyProperty.Register(nameof(IsDragging), typeof(bool), typeof(TabContentControl), new PropertyMetadata(false));

        public bool IsDragging
        {
            get { return (bool)GetValue(IsDraggingProperty); }
            set { SetValue(IsDraggingProperty, value); }
        }

        private void UpdateGridView(bool isSearching)
        {
            if (FileListView == null) return;

            // 初回切り替え時にデフォルトビューを保存
            if (_defaultView == null)
            {
                _defaultView = FileListView.View;
            }

            if (isSearching)
            {
                if (TryFindResource("SearchGridView") is GridView searchView)
                {
                    FileListView.View = searchView;
                    // ビュー切り替え直後に幅計算を実行
                    UpdateColumnWidths();
                }
            }
            else
            {
                if (_defaultView != null)
                {
                    FileListView.View = _defaultView;
                    UpdateColumnWidths();
                }
            }
        }

        public TabContentControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            // ListViewのサイズ変更イベントでカラム幅を調整
            FileListView.SizeChanged += FileListView_SizeChanged;
            FileListBox.SizeChanged += FileListBox_SizeChanged;

            // レイアウト調整用トリガーを追加
            Loaded += TabContentControl_Loaded;
            Unloaded += TabContentControl_Unloaded;
            IsVisibleChanged += TabContentControl_IsVisibleChanged;
            DataContextChanged += (_, _) => UpdateColumnWidths();

            // カラムヘッダーの手動リサイズ検知用
            FileListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnColumnHeaderDragCompleted));
            FileListView.AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler(OnColumnHeaderGripperDoubleClicked), true);

            // ショートカットツールチップ初期化 + 変更追従
            UpdateShortcutTooltips();
            App.KeyBindings.BindingsChanged += (_, _) =>
                _ = Dispatcher.InvokeAsync(UpdateShortcutTooltips);
        }

        /// <summary>ツールチップのショートカット表示を KeyBindingService から動的に設定する。</summary>
        private void UpdateShortcutTooltips()
        {
            var kb = App.KeyBindings;
            void SetTip(FrameworkElement? el, string label, string actionId)
            {
                if (el == null) return;
                var shortcut = kb.GetShortcutText(actionId);
                el.ToolTip = string.IsNullOrEmpty(shortcut) ? label : $"{label} ({shortcut})";
            }

            SetTip(BackBtn, "戻る", "FileList.Back");
            SetTip(ForwardBtn, "進む", "FileList.Forward");
            SetTip(GoUpBtn, "上へ", "FileList.GoUp");
            SetTip(RefreshBtn, "更新", "FileList.Refresh");
            SetTip(NewFolderBtn, "フォルダを作成", "FileList.NewFolder");
        }

        private void OnColumnHeaderDragCompleted(object sender, DragCompletedEventArgs e)
        {
            // ヘッダーのリサイズが行われたら自動調整モードを解除する
            if (e.OriginalSource is Thumb && DataContext is TabItemViewModel vm && vm.IsAdaptiveColumnsEnabled)
            {
                vm.IsAdaptiveColumnsEnabled = false;
            }
        }

        private void OnColumnHeaderGripperDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            // ヘッダー（グリッパー）のダブルクリックが行われたら自動調整モードを解除する
            if (e.OriginalSource is Thumb && DataContext is TabItemViewModel vm && vm.IsAdaptiveColumnsEnabled)
            {
                vm.IsAdaptiveColumnsEnabled = false;
            }
        }

        private void TabContentControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateColumnWidths();
        }

        private void TabContentControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _focusRestoreClearTimer?.Stop();
            _focusRestoreClearTimer = null;
            _breadcrumbPopupLeaveCheckTimer?.Stop();
            _breadcrumbPopupLeaveCheckTimer = null;
            _breadcrumbPopupOpenDelayTimer?.Stop();
            _breadcrumbPopupOpenDelayTimer = null;
            if (_iconColumnCountDebounceTimer != null)
            {
                _iconColumnCountDebounceTimer.Tick -= IconColumnCountDebounceTimer_Tick;
                _iconColumnCountDebounceTimer.Stop();
                _iconColumnCountDebounceTimer = null;
            }
            _incrementalSearchTimer?.Stop();
            _incrementalSearchTimer = null;

            if (BreadcrumbSubfoldersPopup != null)
                BreadcrumbSubfoldersPopup.IsOpen = false;

            if (_tabVmSubscription != null)
            {
                _tabVmSubscription.PropertyChanged -= TabVm_PropertyChanged;
                _tabVmSubscription.RefreshStarting -= OnRefreshStarting;
                _tabVmSubscription.RefreshCompleted -= OnRefreshCompleted;
                _tabVmSubscription = null;
            }
            if (_itemsViewSubscription != null)
            {
                _itemsViewSubscription.CollectionChanged -= OnItemsViewCollectionChanged;
                _itemsViewSubscription = null;
            }

            IsVisibleChanged -= TabContentControl_IsVisibleChanged;
        }

        private void TabContentControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 表示状態になった時に幅を再計算
            if ((bool)e.NewValue)
            {
                UpdateColumnWidths();
            }
        }

        private void FileListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 検索結果ビューの場合のみ計算を実行
            UpdateColumnWidths();
        }

        private bool _isUpdatingIconColumns; // 再入防止：列数変更→レイアウト→SizeChanged の無限ループを防ぐ
        private bool _updateColumnWidthsPending; // UpdateColumnWidths の InvokeAsync 重複抑制
        /// <summary>FileListBox のサイズ変更時に UniformGrid の列数を再計算する。デバウンスで連続発火を抑制。</summary>
        private void FileListBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isUpdatingIconColumns) return;
            ScheduleIconColumnCountUpdate(e.NewSize.Width);
        }

        /// <summary>表示モードに対応するアイテム幅（カード幅+マージン）。列数計算の一元定義。</summary>
        private static double GetItemWidthForMode(FileViewMode mode)
        {
            return mode switch
            {
                FileViewMode.LargeIcon => 160 + 6,   // Width=160, Margin=3 (左右で6)
                FileViewMode.MediumIcon => 116 + 6,   // Width=116, Margin=3 (左右で6)
                FileViewMode.SmallIcon => 80 + 6,     // Width=80, Margin=3 (左右で6)
                _ => 128
            };
        }

        /// <summary>列数更新をスケジュールする。約30ms デバウンスし、最後の幅で1回だけ ApplicationIdle で適用する。</summary>
        private void ScheduleIconColumnCountUpdate(double availableWidth)
        {
            if (availableWidth <= 0) return;
            var vm = DataContext as TabItemViewModel;
            if (vm == null) return;
            if (vm.FileViewMode == FileViewMode.Details) return;

            _pendingIconColumnWidth = availableWidth;
            if (_iconColumnCountDebounceTimer == null)
            {
                _iconColumnCountDebounceTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(30)
                };
                _iconColumnCountDebounceTimer.Tick += IconColumnCountDebounceTimer_Tick;
            }
            _iconColumnCountDebounceTimer.Stop();
            _iconColumnCountDebounceTimer.Start();
        }

        private void IconColumnCountDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _iconColumnCountDebounceTimer?.Stop();
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ApplyIconColumnCountFromPending));
        }

        /// <summary>デバウンス満了後、ApplicationIdle で1回だけ列数を適用。再入防止：列数変更→レイアウト→SizeChanged の無限ループを防ぐ。ActualWidth は読まず _pendingIconColumnWidth を使用。</summary>
        private void ApplyIconColumnCountFromPending()
        {
            if (_isUpdatingIconColumns) return;
            if (_pendingIconColumnWidth <= 0) return;
            var vm = DataContext as TabItemViewModel;
            if (vm == null) return;

            double itemWidth = GetItemWidthForMode(vm.FileViewMode);
            // Border でラップしているため _pendingIconColumnWidth はすでに Padding 適用後の幅
            const double ScrollBarBuffer = 20;
            double usable = _pendingIconColumnWidth - ScrollBarBuffer;
            int cols = Math.Max(1, (int)(usable / itemWidth));
            double gridWidth = cols * itemWidth;
            if (cols == IconColumnCount && Math.Abs(IconViewGridWidth - gridWidth) < 0.1) return;

            _isUpdatingIconColumns = true;
            try
            {
                IconColumnCount = cols;
                IconViewGridWidth = gridWidth;
            }
            finally
            {
                _isUpdatingIconColumns = false;
            }
        }

        private void UpdateColumnWidths()
        {
            if (_updateColumnWidthsPending) return;
            _updateColumnWidthsPending = true;

            // レイアウト確定後に計算が走るように遅延実行
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    bool isAdaptive = DataContext is TabItemViewModel vm && vm.IsAdaptiveColumnsEnabled;

                    // 自動整列が無効な場合は何もしない
                    if (!isAdaptive) return;

                if (FileListView.View is GridView gridView)
                {
                    // ScrollViewerを取得して ViewportWidth を使う（スクロールバー除外後の幅）
                    var scrollViewer = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(FileListView);
                    
                    // 基本はActualWidthだが、ScrollViewerが有効ならViewportWidthを使う
                    double availableWidth = FileListView.ActualWidth;
                    if (scrollViewer != null && scrollViewer.ViewportWidth > 0)
                    {
                        availableWidth = scrollViewer.ViewportWidth;
                    }

                    if (availableWidth <= 0) return;

                    // 検索モード判定: カラム構成で判定（検索モードは5列、通常モードは4列）
                    // 検索モード: 名前, 場所, パス, 更新日時, サイズ
                    // 通常モード: 名前, 更新日時, 種類, サイズ
                    bool isSearchMode = gridView.Columns.Count >= 5 && 
                                      gridView.Columns[2].Header?.ToString() == "パス";

                    if (isSearchMode)
                    {
                        // --- 検索モード (パス列を可変にする) ---
                        // 列順序: 0:名前, 1:場所, 2:パス, 3:更新日時, 4:サイズ
                        if (gridView.Columns.Count < 5) return;

                        var nameCol = gridView.Columns[0];
                        var locationCol = gridView.Columns[1];
                        var pathCol = gridView.Columns[2];
                        var dateCol = gridView.Columns[3];
                        var sizeCol = gridView.Columns[4];
                        
                        // アダプティブ制御: 優先度の低い列を表示/非表示にする
                        // 優先度(高->低): パス > 名前 > 場所 > 更新日時 > サイズ
                        // つまり、サイズが最初に消え、パスが最後まで残る
                        
                        double nameTarget = 240.0; // 更新日時(140)の2倍
                        double locTarget = 40.0;
                        double dateTarget = 140.0;
                        double sizeTarget = 105.0;
                        double minPathWidth = 100.0; // パスの最小幅確保

                        // 各カラムを表示するために必要な全幅の閾値
                        double thresholdSize = minPathWidth + nameTarget + locTarget + dateTarget + sizeTarget; // 625
                        double thresholdDate = minPathWidth + nameTarget + locTarget + dateTarget;              // 520
                        double thresholdLoc = minPathWidth + nameTarget + locTarget;                            // 380
                        double thresholdName = minPathWidth + nameTarget;                                       // 340

                        if (isAdaptive)
                        {
                            double newSize = availableWidth >= thresholdSize ? sizeTarget : 0.0;
                            double newDate = availableWidth >= thresholdDate ? dateTarget : 0.0;
                            double newLoc = availableWidth >= thresholdLoc ? locTarget : 0.0;
                            double newName = availableWidth >= thresholdName ? nameTarget : 0.0;

                            if (Math.Abs(sizeCol.Width - newSize) > 1.0) sizeCol.Width = newSize;
                            if (Math.Abs(dateCol.Width - newDate) > 1.0) dateCol.Width = newDate;
                            if (Math.Abs(locationCol.Width - newLoc) > 1.0) locationCol.Width = newLoc;
                            if (Math.Abs(nameCol.Width - newName) > 1.0) nameCol.Width = newName;
                        }

                        double fixedWidths = nameCol.ActualWidth + locationCol.ActualWidth + dateCol.ActualWidth + sizeCol.ActualWidth;
                        
                        // 隙間をゼロにする計算式: 全幅 - 固定列幅 - 2(枠線等)
                        double newWidth = Math.Max(0, availableWidth - fixedWidths - 2);

                        // パス列（インデックス2）の幅を設定
                        if (Math.Abs(pathCol.Width - newWidth) > 1.0)
                        {
                            pathCol.Width = newWidth;
                        }

                        // バインディング用にプロパティも更新（コンバーターで使用）
                        if (Math.Abs(SearchPathColumnWidth - newWidth) > 1.0)
                        {
                            SearchPathColumnWidth = newWidth;
                        }
                    }
                    else
                    {
                        // --- 通常モード (名前列を可変にする) ---
                        if (gridView.Columns.Count < 5) return;

                        // 固定列・アダプティブ列の現在の幅合計
                        // 1: 場所, 2: 更新日時, 3: 種類, 4: サイズ
                        // ※名前列は index 0
                        var locationCol = gridView.Columns[1];
                        var dateCol = gridView.Columns[2];
                        var typeCol = gridView.Columns[3];
                        var sizeCol = gridView.Columns[4];

                        double fixedWidths = locationCol.ActualWidth + dateCol.ActualWidth + typeCol.ActualWidth + sizeCol.ActualWidth;

                        // 隙間をゼロにする計算式: 全幅 - 固定列幅 - 2(枠線等)
                        double newWidth = Math.Max(0, availableWidth - fixedWidths - 2);

                        // 名前列（インデックス0）の幅を設定
                        var nameColumn = gridView.Columns[0];

                        if (Math.Abs(nameColumn.Width - newWidth) > 1.0)
                        {
                            nameColumn.Width = newWidth;
                        }
                    }
                }
                }
                finally
                {
                    _updateColumnWidthsPending = false;
                }
            }, DispatcherPriority.Render);
        }

        private double _savedVerticalOffset;
        private string? _savedFocusedPath;
        private List<string>? _savedSelectedPaths;

        private void OnRefreshStarting(object? sender, EventArgs e)
        {
            var list = GetActiveFileList();
            if (list == null) return;
            if (list is ListView lv && lv.View is GridView)
            {
                var scrollViewer = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(lv);
                _savedVerticalOffset = scrollViewer?.VerticalOffset ?? 0;
            }
            
            try
            {
                _savedSelectedPaths = list.SelectedItems.Cast<FileItem>().Select(x => x.FullPath).ToList();
            }
            catch (InvalidOperationException)
            {
                _savedSelectedPaths = new List<string>();
            }
            
            if (Keyboard.FocusedElement is ListBoxItem lbi && lbi.Content is FileItem fi)
            {
                _savedFocusedPath = fi.FullPath;
            }
            else
            {
                _savedFocusedPath = null;
            }
        }

        private void OnRefreshCompleted(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    var list = GetActiveFileList();
                    if (list == null) return;

                    var vm = DataContext as TabItemViewModel;
                    // 削除後のインデックス復元（OnItemsViewCollectionChanged は IsRefreshInProgress 中に抑制されるため、ここで処理する）
                    if (vm?.SelectionIndexToRestore is int)
                    {
                        Dispatcher.BeginInvoke(new Action(ApplySelectionRestore), DispatcherPriority.ContextIdle);
                        return;
                    }
                    // 新規フォルダ作成・ペースト後は ApplyFocusAfterRefresh で選択とリネームを実行（CollectionChanged はリフレッシュ中に発火するため OnItemsViewCollectionChanged ではスケジュールされない）
                    if (vm?.RequestFocusAfterRefresh == true)
                    {
                        Dispatcher.BeginInvoke(new Action(ApplyFocusAfterRefresh), DispatcherPriority.Loaded);
                        return;
                    }

                    // Back 後、戻り先一覧で「入ったフォルダ」にフォーカスする（InputBox 表示中はフォーカス操作スキップ）
                    if (vm?.PathToFocusAfterNavigation != null)
                    {
                        string pathToFocus = vm.PathToFocusAfterNavigation;
                        vm.PathToFocusAfterNavigation = null;

                        foreach (var item in list.Items.Cast<object>().ToList())
                        {
                            if (item is FileItem fi && fi.FullPath == pathToFocus)
                            {
                                list.SelectedItems.Clear();
                                list.SelectedItems.Add(fi);
                                list.ScrollIntoView(fi);
                                if (!InputBox.IsOpen && !RenameDialog.IsOpen)
                                {
                                    var container = list.ItemContainerGenerator.ContainerFromItem(fi) as ListBoxItem;
                                    if (container != null)
                                    {
                                        container.Focus();
                                        Keyboard.Focus(container);
                                    }
                                }
                                return;
                            }
                        }
                    }

                    bool isPasteRestore = vm?.PastedFileNamesToSelect != null && vm.PastedFileNamesToSelect.Count > 0;

                    if (!isPasteRestore && list is ListView lv)
                    {
                        var scrollViewer = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(lv);
                        scrollViewer?.ScrollToVerticalOffset(_savedVerticalOffset);
                    }

                    if (vm != null && (vm.PastedFileNamesToSelect == null || vm.PastedFileNamesToSelect.Count == 0))
                    {
                        if (_savedSelectedPaths != null && _savedSelectedPaths.Count > 0)
                        {
                            var itemsToSelect = new List<object>();
                            var itemMap = new Dictionary<string, FileItem>();
                            // コレクション変更時の例外を防ぐため ToList でスナップショット取得
                            foreach (var item in list.Items.Cast<object>().ToList())
                            {
                                if (item is FileItem fi) itemMap[fi.FullPath] = fi;
                            }

                            foreach (var path in _savedSelectedPaths)
                            {
                                if (itemMap.TryGetValue(path, out var item))
                                    itemsToSelect.Add(item);
                            }

                            if (itemsToSelect.Count > 0)
                            {
                                list.SelectedItems.Clear();
                                foreach (var item in itemsToSelect) list.SelectedItems.Add(item);
                            }
                        }

                        // フォーカス復元（InputBox 表示中はスキップ）
                        if (!string.IsNullOrEmpty(_savedFocusedPath) && !InputBox.IsOpen && !RenameDialog.IsOpen)
                        {
                            foreach (var item in list.Items.Cast<object>().ToList())
                            {
                                if (item is FileItem fi && fi.FullPath == _savedFocusedPath)
                                {
                                    var container = list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                                    if (container != null)
                                    {
                                        container.Focus();
                                        Keyboard.Focus(container);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // フォーカス復元等で例外が発生してもリフレッシュ完了は継続
                }
            }));
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // IsSearching の購読を解除
            if (_tabVmSubscription != null)
            {
                _tabVmSubscription.PropertyChanged -= TabVm_PropertyChanged;
                _tabVmSubscription.RefreshStarting -= OnRefreshStarting;
                _tabVmSubscription.RefreshCompleted -= OnRefreshCompleted;
                _tabVmSubscription = null;
            }

            if (_itemsViewSubscription != null)
            {
                _itemsViewSubscription.CollectionChanged -= OnItemsViewCollectionChanged;
                _itemsViewSubscription = null;
            }

            if (e.NewValue is TabItemViewModel vm)
            {
                _tabVmSubscription = vm;
                _tabVmSubscription.PropertyChanged += TabVm_PropertyChanged;
                _tabVmSubscription.RefreshStarting += OnRefreshStarting;
                _tabVmSubscription.RefreshCompleted += OnRefreshCompleted;
                UpdateGridView(vm.IsSearching);

                // 方針G-2: 表示モードに応じて非表示側の ItemsSource を切断
                SyncListItemsSources(vm);

                if (vm.ItemsView is INotifyCollectionChanged incc)
                {
                    _itemsViewSubscription = incc;
                    _itemsViewSubscription.CollectionChanged += OnItemsViewCollectionChanged;
                }
            }
        }

        private void TabVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is TabItemViewModel vm)
            {
                if (e.PropertyName == nameof(TabItemViewModel.IsSearching))
                {
                    UpdateGridView(vm.IsSearching);
                    // 検索モード切替時は CurrentItemsView が変わるため ItemsSource を再同期
                    SyncListItemsSources(vm);
                }
                else if (e.PropertyName == nameof(TabItemViewModel.IsAdaptiveColumnsEnabled))
                {
                    UpdateColumnWidths();
                }
                else if (e.PropertyName == nameof(TabItemViewModel.FileViewMode))
                {
                    // 方針G-2: アイコンビュー時は非表示の FileListView の ItemsSource を切断し、
                    // 同一 ICollectionView を 2 つの ItemsControl が同時処理することによるレイアウト干渉を排除
                    SyncListItemsSources(vm);
                    // 方針H-2: 表示モード変更時に UniformGrid 列数をデバウンスで再計算
                    ScheduleIconColumnCountUpdate(FileListBox.ActualWidth);
                }
            }
        }

        /// <summary>表示モードに応じて、非表示側リストの ItemsSource を切断/再接続する（方針G-2）。方式B: タブ切替・DataContext 変更時も正しく同期するため、常にアクティブ側を vm に合わせる。</summary>
        private void SyncListItemsSources(TabItemViewModel vm)
        {
            if (vm.FileViewMode == FileViewMode.Details)
            {
                // 詳細ビュー: FileListView を接続、FileListBox を切断
                FileListView.ItemsSource = vm.CurrentItemsView;
                FileListBox.ItemsSource = null;
            }
            else
            {
                // アイコンビュー: FileListBox を接続、FileListView を切断
                FileListBox.ItemsSource = vm.CurrentItemsView;
                FileListView.ItemsSource = null;
            }
        }

        private void OnItemsViewCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is not TabItemViewModel vm)
                return;
            // リフレッシュ中（MergeItems + ApplySort 実行中）は CollectionChanged が連続発火するため、
            // フォーカス復元処理のスケジュールを抑制する（StackOverflowException 対策）。
            // 復元は OnRefreshCompleted 側で行われる。
            if (vm.IsRefreshInProgress)
                return;
            if (vm.SelectionIndexToRestore is int index)
            {
                // ContextIdle で列数更新(ApplicationIdle)より後に実行し、レイアウトとスクロール/フォーカスが同フレームで競合しないようにする
                Dispatcher.BeginInvoke(new Action(ApplySelectionRestore), System.Windows.Threading.DispatcherPriority.ContextIdle);
                return;
            }
            if (vm.RequestFocusAfterRefresh)
            {
                Dispatcher.BeginInvoke(new Action(ApplyFocusAfterRefresh), DispatcherPriority.ContextIdle);
            }
        }

        private void ApplySelectionRestore()
        {
            try
            {
                // InputBox がモーダル表示中はフォーカス操作をスキップ
                if (InputBox.IsOpen || RenameDialog.IsOpen) return;

                if (DataContext is not TabItemViewModel vm || vm.SelectionIndexToRestore is not int index)
                    return;
                vm.SelectionIndexToRestore = null;
                var list = GetActiveFileList();
                if (list == null || list.Items.Count == 0) return;
                int count = list.Items.Count;
                int idx = Math.Min(index, count - 1);
                if (idx < 0) return;

                list.SelectedIndex = idx;
                list.UpdateLayout();
                var item = list.Items[idx];
                list.ScrollIntoView(item);
                list.Focus();

                if (list.ItemContainerGenerator.ContainerFromIndex(idx) is ListBoxItem lbItem)
                {
                    lbItem.Focus();
                    Keyboard.Focus(lbItem);
                }
                else
                {
                    // 仮想化により ItemContainer がまだ生成されていない場合、念押しリトライ
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (DataContext is not TabItemViewModel) return;
                        list.UpdateLayout();
                        list.ScrollIntoView(item);
                        if (list.ItemContainerGenerator.ContainerFromIndex(idx) is ListBoxItem retryItem)
                        {
                            retryItem.Focus();
                            Keyboard.Focus(retryItem);
                        }
                    }, DispatcherPriority.Input);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>ペースト（ドロップ）後の Refresh でリストが更新されたあと、ペーストしたファイルを選択してフォーカスを当てる。FileSystemWatcher による後続の Refresh が来ても一定時間は再適用する。</summary>
        private void ApplyFocusAfterRefresh()
        {
            try
            {
                // InputBox がモーダル表示中は一切のフォーカス操作をスキップ
                if (InputBox.IsOpen || RenameDialog.IsOpen) return;

                if (DataContext is not TabItemViewModel vm)
                    return;
                var list = GetActiveFileList();
                if (list == null) return;

                ScheduleFocusRestoreClearTimer(vm);

                var namesToSelect = vm.PastedFileNamesToSelect;
                bool willRename = vm.RequestRenameAfterFocusRestore;

                // リネーム予定がない場合のみリストにフォーカス（InputBox へのフォーカス奪取を防止）
                if (!willRename)
                    list.Focus();

                if (list.Items.Count == 0)
                    return;

                if (namesToSelect != null && namesToSelect.Count > 0)
                {
                    var nameSet = new HashSet<string>(namesToSelect, StringComparer.OrdinalIgnoreCase);
                    var toSelect = new List<object>();
                    foreach (var obj in list.Items.Cast<object>().ToList())
                    {
                        if (obj is FileItem fi && nameSet.Contains(fi.Name))
                            toSelect.Add(obj);
                    }

                    // アイテム未発見かつリネーム予定: MergeItems 反映待ちの可能性があるため 1 フレーム後にリトライ
                    if (toSelect.Count == 0 && willRename)
                    {
                        Dispatcher.BeginInvoke(new Action(ApplyFocusAfterRefresh), DispatcherPriority.Input);
                        return;
                    }

                    if (toSelect.Count > 0)
                    {
                        list.SelectedItems.Clear();
                        foreach (var obj in toSelect)
                            list.SelectedItems.Add(obj);
                        var first = toSelect[0];

                        // 仮想化 ListView 対策: レイアウトを強制完了してから ScrollIntoView
                        list.UpdateLayout();
                        list.ScrollIntoView(first);

                        if (willRename && first is FileItem fileItem)
                        {
                            vm.RequestRenameAfterFocusRestore = false;
                            // 1 フレーム遅延: UI ノイズ完了 → 再スクロール（念押し）→ InputBox 開始
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (InputBox.IsOpen || RenameDialog.IsOpen) return;
                                list.ScrollIntoView(first);
                                vm.RenameItemCommand.Execute(fileItem);
                            }, DispatcherPriority.Input);
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                            {
                                list.ScrollIntoView(first);
                                FocusFirstSelectedItem();
                            }));
                        }
                        return;
                    }
                }

                // ペースト対象が不明な場合は先頭項目を選択
                list.SelectedIndex = 0;
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    if (list.Items.Count > 0)
                    {
                        list.ScrollIntoView(list.Items[0]);
                        FocusFirstSelectedItem();
                    }
                }));
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void ScheduleFocusRestoreClearTimer(TabItemViewModel vm)
        {
            _focusRestoreClearTimer?.Stop();
            _focusRestoreClearTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _focusRestoreClearTimer.Tick += (s, e) =>
            {
                _focusRestoreClearTimer?.Stop();
                _focusRestoreClearTimer = null;
                vm.RequestFocusAfterRefresh = false;
                vm.PastedFileNamesToSelect = null;
                vm.RequestRenameAfterFocusRestore = false;
            };
            _focusRestoreClearTimer.Start();
        }

        private void FocusFirstSelectedItem()
        {
            var list = GetActiveFileList();
            if (list == null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                // InputBox がモーダル表示中はフォーカス奪取しない
                if (InputBox.IsOpen || RenameDialog.IsOpen) return;

                if (list.SelectedIndex < 0) return;
                var selected = list.SelectedItem;
                if (selected != null)
                    list.ScrollIntoView(selected);
                var container = list.ItemContainerGenerator.ContainerFromIndex(list.SelectedIndex) as ListBoxItem;
                if (container != null)
                {
                    if (Keyboard.FocusedElement != container)
                    {
                        container.Focus();
                        Keyboard.Focus(container);
                    }
                }
                else if (Keyboard.FocusedElement != list)
                {
                    list.Focus();
                }
            }));
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is TabItemViewModel vm && sender is ListBox lb)
            {
                vm.UpdateSelectionInfo(lb.SelectedItems);
            }
        }

        /// <summary>Image 要素の描画失敗時に Source を null にクリアし、WPF レンダリングスレッドのクラッシュを防止する。</summary>
        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img)
            {
                _ = App.FileLogger.LogAsync($"[ImageFailed] Source={img.Source?.GetType().Name ?? "null"} Error={e.ErrorException?.Message}");
                img.Source = null;
            }
            e.Handled = true;
        }

        private void TabContentControl_GotFocus(object sender, RoutedEventArgs e)
        {
            // タブ内容領域（背景など）がフォーカスを得た際、
            // 入力コントロール（TextBox等）以外であればファイルリストへフォーカスを移す。
            if (ReferenceEquals(e.OriginalSource, this))
            {
                FocusList();
            }
        }

        private void ListViewItem_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_suppressNextRightClick)
            {
                _suppressNextRightClick = false;
                e.Handled = true;
                return;
            }
            if (sender is ListBoxItem item && item.Content is FileItem fileItem)
            {
                var listBox = ItemsControl.ItemsControlFromItemContainer(item) as ListBox;
                if (listBox != null)
                {
                    if (!listBox.SelectedItems.Contains(fileItem))
                    {
                        listBox.SelectedItems.Clear();
                        listBox.SelectedItems.Add(fileItem);
                    }

                    var paths = new List<string>();
                    foreach (FileItem selected in listBox.SelectedItems)
                    {
                        paths.Add(selected.FullPath);
                    }

                    _lastRightClickScreenPoint = PointToScreen(e.GetPosition(this));
                    _lastRightClickPaths = paths.ToArray();
                    
                    if (DataContext is TabItemViewModel vm)
                    {
                        vm.IsExpectingShellChange = true;
                        ShowContextMenuForItems(vm, listBox.SelectedItems.Cast<FileItem>().ToList(), false);
                    }
                    
                    e.Handled = true;
                }
            }
        }

        private void ListView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_suppressNextRightClick)
            {
                _suppressNextRightClick = false;
                e.Handled = true;
                return;
            }

            // クリックされた要素がアイテム、ヘッダー、スクロールバーの一部である場合は背景メニューを表示しない
            var element = e.OriginalSource as DependencyObject;
            if (FindAncestor<ListBoxItem>(element) != null ||
                FindAncestor<GridViewColumnHeader>(element) != null ||
                FindAncestor<ScrollBar>(element) != null)
            {
                return;
            }

            if (!e.Handled)
            {
                if (DataContext is TabItemViewModel vm && !string.IsNullOrEmpty(vm.CurrentPath))
                {
                    vm.IsExpectingShellChange = true;
                    _lastRightClickScreenPoint = PointToScreen(e.GetPosition(this));
                    _lastRightClickPaths = new[] { vm.CurrentPath };
                    ShowContextMenuForItems(vm, new List<FileItem>(), true);
                    e.Handled = true;
                }
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox lb && lb.SelectedItem is FileItem item)
            {
                if (DataContext is TabItemViewModel vm)
                {
                    vm.OpenItemCommand.Execute(item);
                }
            }
        }

        private void ListView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TabItemViewModel vm)
            {
                if (e.ChangedButton == MouseButton.XButton1)
                {
                    if (vm.BackCommand.CanExecute(null))
                    {
                        vm.BackCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.ChangedButton == MouseButton.XButton2)
                {
                    if (vm.ForwardCommand.CanExecute(null))
                    {
                        vm.ForwardCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }

        private Point _dragStartPoint;
        private ListBoxItem? _mouseDownItem; // ドラッグ開始時の選択ロック用
        private ListBoxItem? _rightMouseDownItem; // 右ドラッグ用
        private const double DragStartThreshold = 10.0;  // ドラッグ開始までの移動ピクセル（誤操作防止）

        private const string RightDragDataFormat = "ZenithFiler.RightDragFiles";
        private Point _rightDragStartPoint;
        private bool _isRightDragPossible;
        private bool _suppressNextRightClick;
        private Point _lastRightClickScreenPoint;
        private string[] _lastRightClickPaths = Array.Empty<string>();
        private CancellationTokenSource? _menuTaskCts;

        /// <summary>
        /// コンテキストメニュー（Zenith / Explorer 両方）表示中に true。
        /// ドラッグ開始・マウスイベント処理を抑制するための統一フラグ。
        /// </summary>
        private volatile bool _isContextMenuActive;

        /// <summary>
        /// コンテキストメニューが閉じた瞬間の TickCount64。
        /// メニュー外クリックの Win32 メッセージが WPF に転送される際の
        /// タイミング競合を防ぐクールダウン期間（150ms）に使用する。
        /// </summary>
        private long _contextMenuClosedTick;

        /// <summary>
        /// コンテキストメニュー表示中、またはクローズ直後のクールダウン期間中かを判定する。
        /// メニュー外クリック時の Win32→WPF マウスイベント転送による誤ドラッグを防止する。
        /// </summary>
        private bool IsContextMenuCooldown()
            => _isContextMenuActive
               || (Environment.TickCount64 - Interlocked.Read(ref _contextMenuClosedTick)) < 150;

        // 右ドラッグ用の一時データ
        private string[]? _dragFiles;
        private string? _dragTargetDirectory;
        private TabItemViewModel? _dragVm;

        private DragAdorner? _dragAdorner;
        /// <summary>Remove 時に AdornedElement がツリー外だと GetAdornerLayer が null になるため、Add 時の layer を保持する。</summary>
        private AdornerLayer? _dragAdornerLayer;

        private void ListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // コンテキストメニュー閉鎖直後の右クリックはドラッグ起点にしない
            if (IsContextMenuCooldown())
            {
                _rightMouseDownItem = null;
                _isRightDragPossible = false;
                return;
            }

            // スクロールバーやヘッダー上での操作ならドラッグ開始を抑制
            var element = e.OriginalSource as DependencyObject;
            if (FindAncestor<ScrollBar>(element) != null || FindAncestor<GridViewColumnHeader>(element) != null)
            {
                _rightMouseDownItem = null;
                _isRightDragPossible = false;
                return;
            }
            _rightDragStartPoint = e.GetPosition(null);
            _rightMouseDownItem = FindAncestor<ListBoxItem>(element);
            _isRightDragPossible = _rightMouseDownItem != null;
        }

        private void ListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // コンテキストメニュー閉鎖直後のクリック（Win32 メニュー外クリックの転送）は
            // ドラッグ起点にしない。選択処理はそのまま通す。
            if (IsContextMenuCooldown())
            {
                _mouseDownItem = null;
                return;
            }

            // スクロールバーやヘッダー上での操作ならドラッグ開始を抑制し、標準のスクロール処理等を優先する
            var dep = e.OriginalSource as DependencyObject;
            if (FindAncestor<ScrollBar>(dep) != null || FindAncestor<GridViewColumnHeader>(dep) != null)
            {
                _mouseDownItem = null;
                return;
            }
            _dragStartPoint = e.GetPosition(null);

            // ドラッグ開始の起点となるアイテムを記録
            _mouseDownItem = FindAncestor<ListBoxItem>(dep);

            if (sender is ListBox listBox)
            {
                // ダブルクリックはドラッグ準備よりも優先して処理する
                if (e.ClickCount == 2 && _mouseDownItem != null)
                {
                    if (_mouseDownItem.Content is FileItem fileItem && DataContext is TabItemViewModel vm)
                    {
                        // シングルクリックモード時、フォルダはシングルクリックで処理済みのためスキップ
                        if (WindowSettings.SingleClickOpenFolderEnabled && fileItem.IsDirectory)
                        {
                            e.Handled = true;
                            return;
                        }
                        // Enter キーと同様に、フォルダ移動／ファイル実行（フォルダショートカット含む）を即座に実行
                        vm.OpenItemCommand.Execute(fileItem);
                        e.Handled = true;
                        return;
                    }
                }

                // シングルクリックでフォルダを開くモード（修飾キーなし・フォルダのみ）
                if (e.ClickCount == 1 && WindowSettings.SingleClickOpenFolderEnabled
                    && Keyboard.Modifiers == ModifierKeys.None && _mouseDownItem != null)
                {
                    if (_mouseDownItem.Content is FileItem folderItem && folderItem.IsDirectory
                        && DataContext is TabItemViewModel vmSingle)
                    {
                        vmSingle.OpenItemCommand.Execute(folderItem);
                        e.Handled = true;
                        return;
                    }
                }

                // すでに選択されている項目をクリックした場合は、複数選択状態を維持する。
                // （Extended 選択時に、既に選択済みの行をクリックしても単一選択に絞り込まない）
                // ※ドラッグ維持ロジックはシングルクリック時のみ適用
                if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.None && _mouseDownItem != null && _mouseDownItem.IsSelected)
                {
                    // フォーカスだけ当てて、WPF 標準の選択処理はキャンセルする
                    listBox.Focus();
                    e.Handled = true;
                    return;
                }

                if (listBox.Name == "FileListBox")
                {
                    // アイコンビュー: クリックがカード（SelectionBorder）外なら選択しない
                    var iconItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
                    if (iconItem != null && !IsClickInsideIconItemContent(iconItem, e))
                    {
                        e.Handled = true;
                        return;
                    }
                }
                // 空きスペースをクリックした時にフォーカスを当て、選択を解除する
                var element = e.OriginalSource as DependencyObject;
                var item = FindAncestor<ListBoxItem>(element);
                var header = FindAncestor<GridViewColumnHeader>(element);
                var scrollBar = FindAncestor<ScrollBar>(element);
                if (item == null && header == null && scrollBar == null)
                {
                    listBox.Focus();
                    listBox.SelectedItems.Clear();
                }
            }
        }

        /// <summary>アイコンビューで、クリック位置が ListBoxItem のカード（SelectionBorder）内かどうか。</summary>
        private static bool IsClickInsideIconItemContent(ListBoxItem item, MouseButtonEventArgs e)
        {
            var selectionBorder = item.Template?.FindName("SelectionBorder", item) as FrameworkElement;
            if (selectionBorder == null || selectionBorder.ActualWidth <= 0 || selectionBorder.ActualHeight <= 0)
                return true;
            var posInItem = e.GetPosition(item);
            return posInItem.X >= 0 && posInItem.Y >= 0
                   && posInItem.X < selectionBorder.ActualWidth && posInItem.Y < selectionBorder.ActualHeight;
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

        private async void ShowContextMenuForItems(TabItemViewModel vm, IList<FileItem> selectedItems, bool isBackground)
        {
            _ = App.Stats.RecordAsync("ContextMenu.Open");
            _menuTaskCts?.Cancel();
            _menuTaskCts = new CancellationTokenSource();
            var menuToken = _menuTaskCts.Token;

            // 統一コンテキストメニューフラグ ON + ドラッグ状態を完全リセット
            _isContextMenuActive = true;
            _mouseDownItem = null;
            _isRightDragPossible = false;
            _rightMouseDownItem = null;

            var mainVm = Application.Current?.MainWindow?.DataContext as MainViewModel;
            var useExplorer = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ||
                (mainVm?.AppSettings?.ContextMenuMode ?? ContextMenuMode.Zenith) == ContextMenuMode.Explorer;

            if (useExplorer)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _lastRightClickPaths.Length > 0)
                {
                    var path = PathHelper.GetPhysicalPath(_lastRightClickPaths[0]);
                    if (PathHelper.IsInsideBoxDrive(path))
                        BoxDriveService.StartSharedLinkClipboardMonitor(path, vm);
                }
                ShowExplorerContextMenu(vm, isBackground, onMenuClosed: () =>
                {
                    _isContextMenuActive = false;
                    Interlocked.Exchange(ref _contextMenuClosedTick, Environment.TickCount64);
                    _menuTaskCts?.Cancel();
                    BoxDriveService.CancelMonitoringSilently();
                    _mouseDownItem = null;
                    _isRightDragPossible = false;
                    _rightMouseDownItem = null;
                });
                return;
            }

            var menu = BuildZenithContextMenu(vm, selectedItems, isBackground, out Task? cloudItemsTask, menuToken);
            if (menu == null) return;

            menu.Closed += (_, _) =>
            {
                _isContextMenuActive = false;
                Interlocked.Exchange(ref _contextMenuClosedTick, Environment.TickCount64);
                _menuTaskCts?.Cancel();
                _mouseDownItem = null;
                _isRightDragPossible = false;
                _rightMouseDownItem = null;
            };

            // キャッシュミス時、最大 300ms だけ COM 完了を待つ
            if (cloudItemsTask != null)
                await Task.WhenAny(cloudItemsTask, Task.Delay(300));

            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.RelativePoint;
            var controlPos = PointFromScreen(_lastRightClickScreenPoint);
            menu.HorizontalOffset = controlPos.X;
            menu.VerticalOffset = controlPos.Y;
            menu.IsOpen = true;
        }

        private void ShowExplorerContextMenu(TabItemViewModel vm, bool isBackground, Action? onMenuClosed = null)
        {
            // _isContextMenuActive は ShowContextMenuForItems で既にセット済み

            // エクスプローラ互換メニューを開く前に「キーボード操作モード」を明示的に解除し、
            // マウスカーソルが確実に表示された状態でメニューを表示する。
            if (Application.Current?.MainWindow is MainWindow mw)
            {
                mw.ExitKeyboardOperationMode();
            }

            // onClosed は STA スレッドから呼ばれるため、Dispatcher 経由で UI スレッドに戻す
            Action? closedCallback = onMenuClosed != null
                ? () => Dispatcher.InvokeAsync(() => onMenuClosed())
                : null;

            var scm = new ShellContextMenu(
                onRenameRequest: path =>
                {
                    var target = vm.Items.FirstOrDefault(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
                    if (target != null) vm.RenameItemCommand.Execute(target);
                },
                onDeleteRequest: pathsToDelete =>
                {
                    var pathSet = new HashSet<string>(pathsToDelete, StringComparer.OrdinalIgnoreCase);
                    var itemsToDelete = vm.Items.Where(x => x.FullPath != null && pathSet.Contains(x.FullPath)).ToList();
                    if (itemsToDelete.Count > 0) vm.DeleteItemsCommand.Execute(itemsToDelete);
                },
                onRefreshRequest: () =>
                {
                    Dispatcher.InvokeAsync(() => vm.LoadDirectoryAsync(), DispatcherPriority.Background);
                });
            var window = Window.GetWindow(this);
            var workArea = window != null ? WindowHelper.GetWorkArea(window) : (System.Windows.Rect?)null;
            if (isBackground)
                scm.ShowBackgroundContextMenu(vm.CurrentPath, _lastRightClickScreenPoint, workArea, closedCallback);
            else
                scm.ShowContextMenu(_lastRightClickPaths, _lastRightClickScreenPoint, false, workArea, closedCallback);
        }

        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".md", ".cfg", ".ini", ".csv", ".tsv", ".json", ".xml", ".yaml", ".yml",
            ".lua", ".py", ".cs", ".vb", ".js", ".ts", ".html", ".htm", ".css", ".scss",
            ".bat", ".ps1", ".sh", ".reg", ".inf", ".rst", ".rst.txt"
        };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".ico"
        };

        private static readonly HashSet<string> PdfConvertExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // 画像
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            // Office
            ".docx", ".doc", ".docm", ".xlsx", ".xls", ".xlsm", ".pptx", ".ppt", ".pptm"
        };

        private static bool IsConvertibleToPdf(string path) =>
            PdfConvertExtensions.Contains(Path.GetExtension(path));

        private static bool IsTextLike(string fullPath)
        {
            var ext = Path.GetExtension(fullPath);
            return !string.IsNullOrEmpty(ext) && TextExtensions.Contains(ext);
        }

        private static bool IsImageLike(string fullPath)
        {
            var ext = Path.GetExtension(fullPath);
            return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
        }

        /// <summary>.txt の既定アプリの実行ファイルパスをレジストリから取得する。</summary>
        private static string? GetDefaultTextEditorPath()
        {
            try
            {
                // 1. HKCR\.txt の (Default) から ProgId を取得（ユーザーが既定エディタを変更している場合は txtfile 以外）
                var progId = (Registry.ClassesRoot.OpenSubKey(".txt")?.GetValue(null) as string)?.Trim();
                if (string.IsNullOrEmpty(progId)) return null;

                // 2. HKCR\<ProgId>\shell\open\command からコマンド行を取得
                using var key = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
                if (key == null) return null;

                var command = key.GetValue(null) as string;
                if (string.IsNullOrEmpty(command)) return null;

                var m = Regex.Match(command.Trim(), @"^""([^""]+)""\s*(?:%1|""%1"")?|^([^\s""]+)\s*(?:%1|""%1"")?");
                var exePath = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return null;
                return exePath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>.txt の既定アプリで開く。取得できない場合は通常の「開く」にフォールバック。</summary>
        private static void OpenWithTextEditor(IEnumerable<string> paths)
        {
            var editorPath = GetDefaultTextEditorPath();
            var pathList = paths.ToList();

            if (string.IsNullOrEmpty(editorPath))
            {
                foreach (var path in pathList)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        App.Notification.Notify($"ファイルを開けませんでした: {Path.GetFileName(path)}", ex.Message);
                    }
                }
                return;
            }

            foreach (var path in pathList)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = editorPath,
                        Arguments = $"\"{path}\"",
                        UseShellExecute = false
                    });
                }
                catch (Exception ex)
                {
                    App.Notification.Notify($"ファイルを開けませんでした: {Path.GetFileName(path)}", ex.Message);
                }
            }
        }

        private static void OpenWithEditVerb(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        Verb = "edit",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Win32Exception)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        App.Notification.Notify($"ファイルを開けませんでした: {Path.GetFileName(path)}", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    App.Notification.Notify($"ファイルを開けませんでした: {Path.GetFileName(path)}", ex.Message);
                }
            }
        }

        private ContextMenu? BuildZenithContextMenu(TabItemViewModel vm, IList<FileItem> selectedItems, bool isBackground,
            out Task? cloudItemsTask, CancellationToken menuToken = default)
        {
            cloudItemsTask = null;
            Task? pendingCloudTask = null;
            var mainVm = Application.Current?.MainWindow?.DataContext as MainViewModel;
            if (mainVm == null) return null;

            var menu = new ContextMenu();
            if (Application.Current?.TryFindResource("ContextMenuItemStyle") is Style itemStyle)
                menu.ItemContainerStyle = itemStyle;
            var allFolders = selectedItems.All(x => x.IsDirectory);
            var allFiles = selectedItems.All(x => !x.IsDirectory);
            var allText = allFiles && selectedItems.All(x => IsTextLike(x.FullPath));
            var allImages = allFiles && selectedItems.All(x => IsImageLike(x.FullPath));
            var single = selectedItems.Count == 1;
            var first = selectedItems.Count > 0 ? selectedItems[0] : null;

            void Add(string header, PackIconLucideKind icon, Action action)
            {
                var mi = new MenuItem { Header = header };
                mi.Icon = new PackIconLucide { Kind = icon, Width = 14, Height = 14 };
                mi.Click += (_, _) => action();
                menu.Items.Add(mi);
            }

            void AddSep()
            {
                var sep = new Separator();
                if (Application.Current.TryFindResource("ContextMenuSeparatorStyle") is Style style)
                    sep.Style = style;
                menu.Items.Add(sep);
            }

            void AddWithImage(string header, ImageSource? icon, Action action, MenuItem? parent = null)
            {
                var mi = new MenuItem { Header = header };
                if (icon != null)
                    mi.Icon = new Image { Source = icon, Width = 16, Height = 16, SnapsToDevicePixels = true };
                else
                    mi.Icon = new PackIconLucide { Kind = PackIconLucideKind.File, Width = 14, Height = 14 };
                mi.Click += (_, _) => action();
                (parent ?? (ItemsControl)menu).Items.Add(mi);
            }

            // リンク連携コピー（独立項目）: Box=リッチコピー / OneDrive=標準Copy link / 領域外=非活性
            void AddLinkCopyItem(string[] targetPaths, bool isBg)
            {
                var sourceType = PathHelper.DetermineSourceType(vm.CurrentPath ?? "");
                bool isCloud = sourceType == SourceType.Box || sourceType == SourceType.SPO;

                var mi = new MenuItem
                {
                    Header = "リンク連携コピー",
                    Icon = new PackIconLucide { Kind = PackIconLucideKind.Link2, Width = 14, Height = 14 },
                    IsEnabled = isCloud,
                };

                if (isCloud)
                {
                    mi.Click += async (_, _) =>
                    {
                        // キャッシュ or 新規抽出で CloudMenuItem を取得
                        if (!CloudShellMenuService.TryGetCached(targetPaths, isBg, out var cloudItems)
                            || cloudItems.Count == 0)
                            cloudItems = await CloudShellMenuService.ExtractCloudMenuItemsAsync(
                                targetPaths, isBg, timeoutMs: 5000);

                        var copyLinkItem = CloudShellMenuService.FindCopyLinkItem(cloudItems, sourceType);
                        if (copyLinkItem == null)
                        {
                            App.Notification.Notify("リンクコピーのメニュー項目が見つかりませんでした", null);
                            return;
                        }

                        // FilePaths を実際の targetPaths に差し替え
                        var adjusted = new CloudMenuItem
                        {
                            Text = copyLinkItem.Text, Icon = copyLinkItem.Icon,
                            Verb = copyLinkItem.Verb, CommandId = copyLinkItem.CommandId,
                            FilePaths = targetPaths, IsBackground = isBg,
                            Children = copyLinkItem.Children,
                        };

                        if (sourceType == SourceType.Box)
                        {
                            // Box: リッチ HTML+PlainText コピー
                            var names = isBg
                                ? vm.CurrentItemsView?.Cast<FileItem>().Select(x => x.Name).ToArray()
                                  ?? Array.Empty<string>()
                                : selectedItems.Select(x => x.Name).ToArray();
                            if (!isBg && selectedItems.Count > 0)
                            {
                                var itemPaths = selectedItems.Select(x => x.FullPath).ToArray();
                                BoxDriveService.StartRichShareLinkMonitor(
                                    targetPaths[0], names, itemPaths, adjusted,
                                    _lastRightClickScreenPoint, vm);
                            }
                            else
                            {
                                BoxDriveService.StartRichShareLinkMonitor(
                                    targetPaths[0], names, adjusted,
                                    _lastRightClickScreenPoint, vm);
                            }
                        }
                        else // SPO (OneDrive)
                        {
                            // OneDrive: 標準の Copy link 実行
                            CloudShellMenuService.InvokeCloudMenuCommand(adjusted,
                                _lastRightClickScreenPoint);
                        }
                    };
                }

                menu.Items.Add(mi);
            }

            // クラウドメニュー（サブメニュー）: 全シェル拡張項目を標準動作で格納 / 領域外=非活性
            void AddCloudSubmenu(string[] targetPaths, bool isBg)
            {
                var sourceType = PathHelper.DetermineSourceType(vm.CurrentPath ?? "");
                bool isCloud = sourceType == SourceType.Box || sourceType == SourceType.SPO;

                var cloudParent = new MenuItem
                {
                    Header = "クラウドメニュー",
                    Icon = new PackIconLucide { Kind = PackIconLucideKind.Cloud, Width = 14, Height = 14 },
                    IsEnabled = isCloud,
                };
                if (Application.Current?.TryFindResource("ContextMenuItemStyle") is Style subStyle)
                    cloudParent.ItemContainerStyle = subStyle;

                if (isCloud)
                {
                    if (CloudShellMenuService.TryGetCached(targetPaths, isBg, out var cachedItems)
                        && cachedItems.Count > 0)
                    {
                        // キャッシュヒット → 即反映
                        var adjusted = AdjustCloudItemPaths(cachedItems, targetPaths, isBg);
                        PopulateCloudSubmenu(cloudParent, adjusted);
                        _ = CloudShellMenuService.ExtractCloudMenuItemsAsync(
                            targetPaths, isBg, timeoutMs: 5000, cancellationToken: menuToken); // バックグラウンドリフレッシュ
                    }
                    else
                    {
                        // キャッシュミス → プレースホルダー + 非同期読み込み
                        var placeholder = new MenuItem
                        {
                            Header = "読み込み中...",
                            IsEnabled = false,
                            Icon = new PackIconLucide
                                { Kind = PackIconLucideKind.Loader, Width = 14, Height = 14 },
                        };
                        cloudParent.Items.Add(placeholder);

                        pendingCloudTask = Task.Run(async () =>
                        {
                            try
                            {
                                var cloudItems = await CloudShellMenuService.ExtractCloudMenuItemsAsync(
                                    targetPaths, isBg, timeoutMs: 3000, cancellationToken: menuToken);
                                var app = Application.Current;
                                if (app != null)
                                {
                                    await app.Dispatcher.InvokeAsync(() =>
                                    {
                                        cloudParent.Items.Clear();
                                        if (cloudItems.Count > 0)
                                            PopulateCloudSubmenu(cloudParent, cloudItems);
                                        else
                                            cloudParent.IsEnabled = false;
                                    });
                                }
                            }
                            catch (OperationCanceledException) { /* メニュー閉鎖によるキャンセル — サイレント */ }
                        });
                    }
                }

                menu.Items.Add(cloudParent);
            }

            // クラウドサブメニュー内に全項目を標準の InvokeCloudMenuCommand で追加
            void PopulateCloudSubmenu(MenuItem parent, List<CloudMenuItem> items)
            {
                foreach (var ci in items)
                {
                    var captured = ci;
                    if (captured.HasChildren)
                    {
                        var subMi = new MenuItem { Header = ci.Text };
                        if (ci.Icon != null)
                            subMi.Icon = new Image { Source = ci.Icon, Width = 16, Height = 16,
                                SnapsToDevicePixels = true };
                        else
                            subMi.Icon = new PackIconLucide
                                { Kind = PackIconLucideKind.File, Width = 14, Height = 14 };
                        if (Application.Current?.TryFindResource("ContextMenuItemStyle") is Style s)
                            subMi.ItemContainerStyle = s;
                        foreach (var child in captured.Children!)
                        {
                            var cc = child;
                            AddWithImage(child.Text, child.Icon, () =>
                                CloudShellMenuService.InvokeCloudMenuCommand(cc,
                                    _lastRightClickScreenPoint),
                                parent: subMi);
                        }
                        parent.Items.Add(subMi);
                    }
                    else
                    {
                        AddWithImage(ci.Text, ci.Icon, () =>
                            CloudShellMenuService.InvokeCloudMenuCommand(captured,
                                _lastRightClickScreenPoint),
                            parent: parent);
                    }
                }
            }

            // キャッシュ項目の FilePaths を実際の targetPaths に差し替え
            static List<CloudMenuItem> AdjustCloudItemPaths(
                List<CloudMenuItem> items, string[] targetPaths, bool isBg)
            {
                return items.Select(ci => new CloudMenuItem
                {
                    Text = ci.Text, Icon = ci.Icon, Verb = ci.Verb,
                    CommandId = ci.CommandId, FilePaths = targetPaths,
                    IsBackground = isBg,
                    Children = ci.Children?.Select(ch => new CloudMenuItem
                    {
                        Text = ch.Text, Icon = ch.Icon, Verb = ch.Verb,
                        CommandId = ch.CommandId, FilePaths = targetPaths,
                        IsBackground = isBg, Children = ch.Children,
                    }).ToList(),
                }).ToList();
            }

            void AddExplorerLink()
            {
                Add("エクスプローラのコンテキストメニューを表示する", PackIconLucideKind.ExternalLink, () =>
                {
                    menu.IsOpen = false;
                    ShowExplorerContextMenu(vm, isBackground);
                });
            }

            void AddCopyNameAndPath()
            {
                if (selectedItems.Count == 0) return;
                Add("ファイル名をコピー", PackIconLucideKind.FileText, () =>
                {
                    var text = string.Join(Environment.NewLine, selectedItems.Select(x => x.Name));
                    Clipboard.SetText(text);
                    App.Notification.Notify("ファイル名をコピーしました", null);
                });
                Add("フルパスをコピー", PackIconLucideKind.ClipboardList, () =>
                {
                    var text = string.Join(Environment.NewLine, selectedItems.Select(x => x.FullPath));
                    Clipboard.SetText(text);
                    App.Notification.Notify("フルパスをコピーしました", null);
                });
            }

            void AddBoxSharePathCopyItem()
            {
                if (PathHelper.DetermineSourceType(vm.CurrentPath) != SourceType.Box) return;
                Add("連携用BOXパスをコピー", PackIconLucideKind.Share2, () =>
                {
                    if (isBackground)
                    {
                        if (PathHelper.TryGetBoxSharePath(vm.CurrentPath, out var boxPath) && !string.IsNullOrEmpty(boxPath))
                        {
                            Clipboard.SetText(boxPath);
                            App.Notification.Notify("連携用BOXパスをコピーしました", null);
                        }
                    }
                    else
                    {
                        var lines = new List<string>();
                        foreach (var item in selectedItems)
                        {
                            if (PathHelper.TryGetBoxSharePath(item.FullPath, out var boxPath) && !string.IsNullOrEmpty(boxPath))
                                lines.Add(boxPath);
                        }
                        if (lines.Count > 0)
                        {
                            Clipboard.SetText(string.Join(Environment.NewLine, lines));
                            App.Notification.Notify("連携用BOXパスをコピーしました", null);
                        }
                    }
                });
            }

            if (isBackground)
            {
                // クリップボード
                Add("貼り付け", PackIconLucideKind.ClipboardPaste, () => vm.PasteItemsCommand.Execute(null));
                AddSep();
                // 名前・パスのコピー・アプリ固有
                Add("フルパスをコピー", PackIconLucideKind.ClipboardList, () => vm.CopyAddressCommand.Execute(null));
                Add("フォルダ内の一覧をコピー（名前）", PackIconLucideKind.List, () =>
                {
                    var items = vm.CurrentItemsView.Cast<FileItem>().ToList();
                    var text = items.Count == 0 ? "" : string.Join(Environment.NewLine, items.Select(x => x.Name));
                    Clipboard.SetText(text);
                    App.Notification.Notify(items.Count == 0 ? "一覧は0件でした" : $"{items.Count}件の名前をコピーしました", null);
                });
                Add("フォルダ内の一覧をコピー（フルパス）", PackIconLucideKind.ListOrdered, () =>
                {
                    var items = vm.CurrentItemsView.Cast<FileItem>().ToList();
                    var text = items.Count == 0 ? "" : string.Join(Environment.NewLine, items.Select(x => x.FullPath));
                    Clipboard.SetText(text);
                    App.Notification.Notify(items.Count == 0 ? "一覧は0件でした" : $"{items.Count}件のフルパスをコピーしました", null);
                });
                Add("このフォルダ以下の一覧をCSV出力", PackIconLucideKind.Table, () =>
                    _ = vm.ExportSubtreeToCsvAsync(vm.CurrentPath ?? ""));
                Add("このフォルダ以下の一覧をExcel出力", PackIconLucideKind.FileSpreadsheet, () =>
                    _ = vm.ExportSubtreeToExcelAsync(vm.CurrentPath ?? ""));
                AddBoxSharePathCopyItem();
                Add("インデックス検索でこのフォルダを検索", PackIconLucideKind.Search, () =>
                    _ = mainVm.IndexSearchSettings.AddFolderFromContextMenuAndHighlightAsync(vm.CurrentPath ?? ""));
                AddSep();
                // 新規作成
                Add("新しいフォルダ", PackIconLucideKind.FolderPlus, () => vm.CreateNewFolderCommand.Execute(null));
                Add("新しいテキストファイル", PackIconLucideKind.FilePlus, () => vm.CreateNewTextFileCommand.Execute(null));
                // 新規作成サブメニュー（ShellNew エントリ）
                var shellNewItems = ShellNewService.GetCachedItems();
                if (shellNewItems.Count > 0)
                {
                    var newSubMenu = new MenuItem
                    {
                        Header = "新規作成",
                        Icon = new PackIconLucide { Kind = PackIconLucideKind.FilePlus2, Width = 14, Height = 14 }
                    };
                    if (Application.Current?.TryFindResource("ContextMenuItemStyle") is Style subStyle)
                        newSubMenu.ItemContainerStyle = subStyle;
                    foreach (var sni in shellNewItems)
                    {
                        var captured = sni;
                        AddWithImage(captured.DisplayName, captured.Icon,
                            () => _ = vm.CreateNewFileFromShellNewAsync(captured),
                            parent: newSubMenu);
                    }
                    menu.Items.Add(newSubMenu);
                }
                AddSep();
                // その他・システム
                Add("プロパティ", PackIconLucideKind.Info, () =>
                {
                    if (!string.IsNullOrEmpty(vm.CurrentPath))
                        ShellIconHelper.ShowFileProperties(vm.CurrentPath);
                });
                AddLinkCopyItem(new[] { vm.CurrentPath ?? "" }, true);
                AddCloudSubmenu(new[] { vm.CurrentPath ?? "" }, true);
                AddSep();
                AddExplorerLink();
            }
            else if (allFolders && single && first != null)
            {
                // 開く・表示
                Add("開く", PackIconLucideKind.FolderOpen, () => vm.OpenItemCommand.Execute(first));
                Add("新しいタブで開く", PackIconLucideKind.FolderPlus, () => vm.ParentPane?.AddTabWithPathCommand.Execute(first.FullPath));
                Add("反対ペインで開く", PackIconLucideKind.PanelRight, () => mainVm.OpenPathInOtherPane(vm.ParentPane!, first.FullPath, false));
                AddSep();
                // クリップボード
                Add("コピー", PackIconLucideKind.Copy, () => vm.CopyItemsCommand.Execute(selectedItems));
                Add("切り取り", PackIconLucideKind.Scissors, () => vm.CutItemsCommand.Execute(selectedItems));
                Add("貼り付け", PackIconLucideKind.ClipboardPaste, () => vm.PasteItemsCommand.Execute(null));
                AddSep();
                // 編集・変更
                Add("削除", PackIconLucideKind.Trash2, () => vm.DeleteItemsCommand.Execute(selectedItems));
                Add("名前の変更", PackIconLucideKind.Pencil, () => vm.RenameItemCommand.Execute(first));
                Add("ショートカットの作成", PackIconLucideKind.Link, () => vm.CreateShortcutCommand.Execute(selectedItems));
                AddSep();
                // 名前・パスのコピー・アプリ固有
                AddCopyNameAndPath();
                AddBoxSharePathCopyItem();
                Add("このフォルダをAペインのホームに設定", PackIconLucideKind.PanelLeft, () => mainVm.AppSettings.SetLeftPaneHomeFromPath(first.FullPath));
                Add("このフォルダをBペインのホームに設定", PackIconLucideKind.PanelRight, () => mainVm.AppSettings.SetRightPaneHomeFromPath(first.FullPath));
                if (mainVm.Favorites.ContainsPath(first.FullPath))
                    Add("お気に入りから解除", PackIconLucideKind.Minus, () => { _ = App.Stats.RecordAsync("Favorites.Remove"); mainVm.Favorites.RemovePath(first.FullPath); vm.UpdateIsFavorite(); });
                else
                    Add("お気に入りに追加", PackIconLucideKind.Star, () => _ = mainVm.Favorites.AddPathWithDialogAndHighlightAsync(first.FullPath));
                Add("インデックス検索でこのフォルダを検索", PackIconLucideKind.Search, () =>
                    _ = mainVm.IndexSearchSettings.AddFolderFromContextMenuAndHighlightAsync(first.FullPath));
                Add("このフォルダ以下の一覧をCSV出力", PackIconLucideKind.Table, () =>
                    _ = vm.ExportSubtreeToCsvAsync(first.FullPath));
                Add("このフォルダ以下の一覧をExcel出力", PackIconLucideKind.FileSpreadsheet, () =>
                    _ = vm.ExportSubtreeToExcelAsync(first.FullPath));
                AddSep();
                // その他・システム
                Add("プロパティ", PackIconLucideKind.Info, () => vm.ShowPropertiesCommand.Execute(first));
                AddLinkCopyItem(new[] { first.FullPath }, false);
                AddCloudSubmenu(new[] { first.FullPath }, false);
                AddSep();
                AddExplorerLink();
            }
            else if (allFiles && single && first != null)
            {
                // 開く・表示
                Add("開く", PackIconLucideKind.FileText, () => vm.OpenItemCommand.Execute(first));
                if (IsTextLike(first.FullPath))
                    Add("テキストエディタで開く", PackIconLucideKind.FileText, () => OpenWithTextEditor(new[] { first.FullPath }));
                if (IsImageLike(first.FullPath))
                    Add("ペインターで開く", PackIconLucideKind.Brush, () => OpenWithEditVerb(new[] { first.FullPath }));
                AddSep();
                // クリップボード
                Add("コピー", PackIconLucideKind.Copy, () => vm.CopyItemsCommand.Execute(selectedItems));
                Add("切り取り", PackIconLucideKind.Scissors, () => vm.CutItemsCommand.Execute(selectedItems));
                Add("貼り付け", PackIconLucideKind.ClipboardPaste, () => vm.PasteItemsCommand.Execute(null));
                AddSep();
                // 編集・変更
                Add("削除", PackIconLucideKind.Trash2, () => vm.DeleteItemsCommand.Execute(selectedItems));
                Add("名前の変更", PackIconLucideKind.Pencil, () => vm.RenameItemCommand.Execute(first));
                Add("ショートカットの作成", PackIconLucideKind.Link, () => vm.CreateShortcutCommand.Execute(selectedItems));
                AddSep();
                // 名前・パスのコピー・アプリ固有
                AddCopyNameAndPath();
                AddBoxSharePathCopyItem();
                if (mainVm.Favorites.ContainsPath(first.FullPath))
                    Add("お気に入りから解除", PackIconLucideKind.Minus, () => { _ = App.Stats.RecordAsync("Favorites.Remove"); mainVm.Favorites.RemovePath(first.FullPath); vm.UpdateIsFavorite(); });
                else
                    Add("お気に入りに追加", PackIconLucideKind.Star, () => _ = mainVm.Favorites.AddPathWithDialogAndHighlightAsync(first.FullPath));
                if (IsConvertibleToPdf(first.FullPath))
                    Add("PDFに変換して同フォルダへ保存", PackIconLucideKind.FileBadge, () =>
                        _ = vm.ConvertToPdfAsync(selectedItems.ToList()));
                AddSep();
                // その他・システム
                Add("プロパティ", PackIconLucideKind.Info, () => vm.ShowPropertiesCommand.Execute(first));
                AddLinkCopyItem(new[] { first.FullPath }, false);
                AddCloudSubmenu(new[] { first.FullPath }, false);
                AddSep();
                AddExplorerLink();
            }
            else
            {
                // 開く・表示（複数選択でテキスト/画像のみのとき）
                if (allText && selectedItems.Count > 0)
                {
                    Add("テキストエディタで開く", PackIconLucideKind.FileText, () => OpenWithTextEditor(selectedItems.Select(x => x.FullPath)));
                    AddSep();
                }
                else if (allImages && selectedItems.Count > 0)
                {
                    Add("ペインターで開く", PackIconLucideKind.Brush, () => OpenWithEditVerb(selectedItems.Select(x => x.FullPath)));
                    AddSep();
                }
                // クリップボード
                Add("コピー", PackIconLucideKind.Copy, () => vm.CopyItemsCommand.Execute(selectedItems));
                Add("切り取り", PackIconLucideKind.Scissors, () => vm.CutItemsCommand.Execute(selectedItems));
                Add("貼り付け", PackIconLucideKind.ClipboardPaste, () => vm.PasteItemsCommand.Execute(null));
                AddSep();
                // 編集・変更
                Add("削除", PackIconLucideKind.Trash2, () => vm.DeleteItemsCommand.Execute(selectedItems));
                if (single && first != null)
                    Add("名前の変更", PackIconLucideKind.Pencil, () => vm.RenameItemCommand.Execute(first));
                Add("ショートカットの作成", PackIconLucideKind.Link, () => vm.CreateShortcutCommand.Execute(selectedItems));
                AddSep();
                // 名前・パスのコピー・アプリ固有
                AddCopyNameAndPath();
                AddBoxSharePathCopyItem();
                bool allConvertible = selectedItems.Count > 0 &&
                    selectedItems.All(x => !x.IsDirectory && IsConvertibleToPdf(x.FullPath));
                if (allConvertible)
                    Add("PDFに変換して結合・同フォルダへ保存", PackIconLucideKind.FileBadge, () =>
                        _ = vm.ConvertToPdfAsync(selectedItems.ToList()));
                AddSep();
                // その他・システム
                if (single && first != null)
                    Add("プロパティ", PackIconLucideKind.Info, () => vm.ShowPropertiesCommand.Execute(first));
                AddLinkCopyItem(selectedItems.Select(x => x.FullPath).ToArray(), false);
                AddCloudSubmenu(selectedItems.Select(x => x.FullPath).ToArray(), false);
                AddSep();
                AddExplorerLink();
            }

            cloudItemsTask = pendingCloudTask;
            return menu;
        }

        private void ListView_MouseMove(object sender, MouseEventArgs e)
        {
            // コンテキストメニュー表示中またはクローズ直後はドラッグ開始を一切抑制
            if (IsContextMenuCooldown()) return;

            // スクロールバーやヘッダー上での操作ならドラッグ開始を抑制
            var dep = e.OriginalSource as DependencyObject;
            if (FindAncestor<ScrollBar>(dep) != null || FindAncestor<GridViewColumnHeader>(dep) != null)
            {
                return;
            }
            Point mousePos = e.GetPosition(null);
            Vector diffLeft = _dragStartPoint - mousePos;
            Vector diffRight = _rightDragStartPoint - mousePos;
            bool OverThreshold(Vector d) => Math.Abs(d.X) > DragStartThreshold || Math.Abs(d.Y) > DragStartThreshold;

            if (e.LeftButton == MouseButtonState.Pressed && OverThreshold(diffLeft))
            {
                if (sender is ListBox listBox)
                {
                    // 滑り防止ロジック:
                    // マウスダウン時にアイテム上だった場合、ドラッグ開始時にそのアイテムが選択状態であることを強制する。
                    // これにより、クリック後にマウスが動いて隣のアイテムに移動してしまっても、
                    // 最初にクリックした（つかんだ）アイテムが確実に操作対象となる。
                    if (_mouseDownItem != null)
                    {
                         var content = _mouseDownItem.Content;
                         // もし対象アイテムが選択されていなければ、強制的に選択する（滑り補正）
                         // ただし、既に複数選択されている中の一つであれば、選択変更はしない（正規の複数ドラッグ）
                         if (!listBox.SelectedItems.Contains(content))
                         {
                             // 単一選択として強制リセット
                             listBox.SelectedItems.Clear();
                             listBox.SelectedItems.Add(content);
                             
                             // 念のためフォーカスも戻す
                             _mouseDownItem.Focus();
                         }
                    }

                    if (listBox.SelectedItems.Count > 0)
                    {
                        var paths = listBox.SelectedItems
                            .OfType<FileItem>()
                            .Select(item => item.FullPath)
                            .Where(path => !string.IsNullOrEmpty(path))
                            .Distinct()
                            .ToArray();
                        if (paths.Length == 0)
                            return;

                        var data = new DataObject();
                        data.SetData(DataFormats.FileDrop, paths);

                        // ドラッグアドーナーの表示
                        ShowDragAdorner(listBox);
                        listBox.GiveFeedback += FileListView_GiveFeedback;
                        
                        // ドラッグ状態フラグをON（ホバーエフェクト抑制など）
                        IsDragging = true;
                        try
                        {
                            DragDrop.DoDragDrop(listBox, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
                        }
                        finally
                        {
                            IsDragging = false;
                            listBox.GiveFeedback -= FileListView_GiveFeedback;
                            RemoveDragAdorner();
                            _mouseDownItem = null;
                        }
                    }
                }
            }
            else if (e.RightButton == MouseButtonState.Pressed && _isRightDragPossible && OverThreshold(diffRight))
            {
                if (sender is ListBox listBox)
                {
                    // 右ドラッグ開始時の選択補正
                    if (_rightMouseDownItem != null)
                    {
                        var content = _rightMouseDownItem.Content;
                        if (!listBox.SelectedItems.Contains(content))
                        {
                            listBox.SelectedItems.Clear();
                            listBox.SelectedItems.Add(content);
                            _rightMouseDownItem.Focus();
                        }
                    }

                    if (listBox.SelectedItems.Count > 0)
                    {
                        var paths = listBox.SelectedItems
                            .OfType<FileItem>()
                            .Select(item => item.FullPath)
                            .Where(path => !string.IsNullOrEmpty(path))
                            .Distinct()
                            .ToArray();
                        if (paths.Length == 0)
                            return;

                        var data = new DataObject();
                        data.SetData(DataFormats.FileDrop, paths);
                        data.SetData(RightDragDataFormat, true);

                        // ドラッグアドーナーの表示
                        ShowDragAdorner(listBox);
                        listBox.GiveFeedback += FileListView_GiveFeedback;
                        try
                        {
                            DragDrop.DoDragDrop(listBox, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
                        }
                        finally
                        {
                            listBox.GiveFeedback -= FileListView_GiveFeedback;
                            RemoveDragAdorner();
                            _isRightDragPossible = false;
                            _rightMouseDownItem = null;
                        }
                    }
                }
            }
        }

        private void ShowDragAdorner(UIElement element)
        {
            if (!WindowSettings.ShowDragEffectsEnabled) return;

            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer != null)
            {
                _dragAdornerLayer = layer;
                int count = (element as ListBox)?.SelectedItems.Count ?? 0;
                string baseMessage = "コピー/移動先にドロップしてください";
                string message = count > 0
                    ? $"{count}件の項目を{baseMessage}"
                    : baseMessage;
                _dragAdorner = new DragAdorner(element, message);
                layer.Add(_dragAdorner);
                _dragAdorner.UpdatePosition(Mouse.GetPosition(element));
            }
        }

        private void RemoveDragAdorner()
        {
            if (_dragAdorner != null && _dragAdornerLayer != null)
            {
                _dragAdornerLayer.Remove(_dragAdorner);
                _dragAdornerLayer = null;
                _dragAdorner = null;
            }
            else if (_dragAdorner != null)
            {
                _dragAdorner = null;
            }
        }

        private void FileListView_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            _dragAdorner?.UpdatePositionFromCursor();
            e.Handled = false;
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(RightDragDataFormat))
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
            }
            else if (e.Data.GetDataPresent(typeof(FavoriteItem)) ||
                e.Data.GetDataPresent(typeof(HistoryItemViewModel)) ||
                e.Data.GetDataPresent(typeof(DirectoryItemViewModel)))
            {
                e.Effects = DragDropEffects.Link;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                bool ctrlPressed = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                e.Effects = ctrlPressed ? DragDropEffects.Copy : DragDropEffects.Move;
            }
            else if (e.Data.GetDataPresent("UniformResourceLocator") || e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void ListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
            if (DataContext is not TabItemViewModel vm)
                return;

            // お気に入りアイテムのドロップ：存在確認付きナビゲーション
            if (e.Data.GetDataPresent(typeof(FavoriteItem)))
            {
                var favItem = e.Data.GetData(typeof(FavoriteItem)) as FavoriteItem;
                if (favItem != null)
                {
                    var window = Window.GetWindow(this);
                    if (window?.DataContext is MainViewModel mainVm)
                    {
                        if (await mainVm.Favorites.EnsurePathExistsAsync(favItem))
                        {
                            ApplySearchClearAndNavigate(vm, favItem.Path!);
                        }
                        e.Handled = true;
                        return;
                    }
                }
            }

            // 右ドラッグ＆ドロップ: ドロップ完了後にメニューを表示
            if (e.Data.GetDataPresent(RightDragDataFormat) && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string? targetDirectory = ResolveDropTargetDirectory(sender, e);
                if (string.IsNullOrEmpty(targetDirectory))
                    targetDirectory = vm.CurrentPath;
                SearchHistoryPopup.IsOpen = false;
                vm.SearchText = string.Empty;
                _suppressNextRightClick = true;
                
                // オーバーレイ配置用なので UserControl 基準の座標を取得
                Point dropPos = e.GetPosition(this);
                
                ShowCustomMenu(files, targetDirectory ?? vm.CurrentPath, vm, dropPos);
                return;
            }

            // ナビゲーション系ドロップ（お気に入り・履歴・ツリー）は先に判定し、検索解除→モード復帰→遷移の順で処理
            string? navPath = GetNavigationPathFromDrop(e);
            if (navPath != null)
            {
                ApplySearchClearAndNavigate(vm, navPath);
                e.Handled = true;
                return;
            }

            // それ以外のドロップ: 検索履歴を閉じて検索文字をクリア
            SearchHistoryPopup.IsOpen = false;
            vm.SearchText = string.Empty;

            string? targetDirectory2 = ResolveDropTargetDirectory(sender, e);

            // 外部ファイルドロップ（コピー/移動）
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool ctrlPressed = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                vm.DropFilesCommand.Execute((files, !ctrlPressed, targetDirectory2));  // Ctrlなし＝移動、Ctrlあり＝コピー
            }
            // URL ドロップ
            else if (TryGetUrlAndTitle(e.Data, out string? url, out string? title))
            {
                if (!string.IsNullOrEmpty(url))
                {
                    vm.CreateUrlShortcutCommand.Execute((url, title, targetDirectory2));
                }
            }
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] ListView_Drop: {ex.Message}"); }
        }

        private static bool TryGetUrlAndTitle(IDataObject data, out string? url, out string? title)
        {
            url = null;
            title = null;

            try
            {
                // 1. UniformResourceLocatorW (Unicode)
                if (data.GetDataPresent("UniformResourceLocatorW"))
                {
                    url = ExtractStringFromData(data.GetData("UniformResourceLocatorW"), System.Text.Encoding.Unicode);
                }

                // 2. UniformResourceLocator (ANSI)
                if (string.IsNullOrEmpty(url) && data.GetDataPresent("UniformResourceLocator"))
                {
                    url = ExtractStringFromData(data.GetData("UniformResourceLocator"), System.Text.Encoding.Default);
                }

                // 3. Text
                if (string.IsNullOrEmpty(url) && data.GetDataPresent(DataFormats.Text))
                {
                    url = data.GetData(DataFormats.Text) as string;
                }

                if (string.IsNullOrEmpty(url)) return false;
                
                url = url.Trim().Split('\0')[0];
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) return false;

                // タイトルの抽出を試みる (FileGroupDescriptorW)
                if (data.GetDataPresent("FileGroupDescriptorW"))
                {
                    title = ExtractTitleFromFileGroupDescriptor(data.GetData("FileGroupDescriptorW"), true);
                }
                else if (data.GetDataPresent("FileGroupDescriptor"))
                {
                    title = ExtractTitleFromFileGroupDescriptor(data.GetData("FileGroupDescriptor"), false);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? ExtractStringFromData(object? data, System.Text.Encoding encoding)
        {
            if (data is MemoryStream ms)
            {
                using var reader = new StreamReader(ms, encoding, false, 1024, true);
                return reader.ReadToEnd();
            }
            if (data is byte[] bytes)
            {
                return encoding.GetString(bytes);
            }
            return data?.ToString();
        }

        private static string? ExtractTitleFromFileGroupDescriptor(object? data, bool isUnicode)
        {
            try
            {
                byte[]? bytes = data as byte[];
                if (bytes == null && data is MemoryStream ms)
                {
                    bytes = ms.ToArray();
                }

                if (bytes == null || bytes.Length < 76) return null;

                // FileGroupDescriptor の構造: 
                // [4 bytes: count] 
                // [72+ bytes: FILEDESCRIPTOR x count]
                // FILEDESCRIPTORW (Unicode) の場合、cFileName は 76バイト目(offset 72)から
                
                if (isUnicode)
                {
                    // 72バイト目から開始されるファイル名（固定長 MAX_PATH=260文字分、Wなので520バイト）
                    string fileName = System.Text.Encoding.Unicode.GetString(bytes, 72, Math.Min(520, bytes.Length - 72));
                    fileName = fileName.Split('\0')[0];
                    return Path.GetFileNameWithoutExtension(fileName);
                }
                else
                {
                    string fileName = System.Text.Encoding.Default.GetString(bytes, 72, Math.Min(260, bytes.Length - 72));
                    fileName = fileName.Split('\0')[0];
                    return Path.GetFileNameWithoutExtension(fileName);
                }
            }
            catch { return null; }
        }

        private static string? ResolveDropTargetDirectory(object sender, DragEventArgs e)
        {
            if (sender is not ListBox) return null;
            var element = e.OriginalSource as DependencyObject;
            var container = FindAncestor<ListBoxItem>(element);
            if (container?.Content is FileItem targetItem && targetItem.IsDirectory)
                return targetItem.FullPath;
            return null;
        }

        private void BreadcrumbSegment_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(RightDragDataFormat))
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                bool ctrlPressed = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                e.Effects = ctrlPressed ? DragDropEffects.Copy : DragDropEffects.Move;
            }
            else if (e.Data.GetDataPresent("UniformResourceLocator") || e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void BreadcrumbSegment_Drop(object sender, DragEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not NavigationPathSegment segment ||
                DataContext is not TabItemViewModel vm)
                return;

            string targetDirectory = segment.FullPath;
            SearchHistoryPopup.IsOpen = false;
            vm.SearchText = string.Empty;

            // 右ドラッグ＆ドロップ: ドロップ後にコピー/移動メニューを表示
            if (e.Data.GetDataPresent(RightDragDataFormat) && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                _suppressNextRightClick = true;
                Point dropPos = e.GetPosition(this);
                ShowCustomMenu(files, targetDirectory, vm, dropPos);
                return;
            }

            // 通常のファイルドロップ: 指定階層へコピー/移動
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool ctrlPressed = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                vm.DropFilesCommand.Execute((files, !ctrlPressed, targetDirectory));
            }
            // URL ドロップ
            else if (TryGetUrlAndTitle(e.Data, out string? url, out string? title))
            {
                e.Handled = true;
                if (!string.IsNullOrEmpty(url))
                {
                    vm.CreateUrlShortcutCommand.Execute((url, title, targetDirectory));
                }
            }
        }

        private void ShowCustomMenu(string[] files, string targetDirectory, TabItemViewModel vm, Point dropPos)
        {
            _dragFiles = files;
            _dragTargetDirectory = targetDirectory;
            _dragVm = vm;

            // マウス位置に合わせてメニューを表示（画面外にはみ出さない簡易補正）
            double x = dropPos.X;
            double y = dropPos.Y;
            
            // 右端・下端のチェック（簡易）
            if (x + 180 > ActualWidth) x = ActualWidth - 180;
            if (y + 160 > ActualHeight) y = ActualHeight - 160;

            CustomMenuBorder.Margin = new Thickness(Math.Max(0, x), Math.Max(0, y), 0, 0);
            CustomMenuOverlay.Visibility = Visibility.Visible;
        }

        private void CustomMenuOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseCustomMenu();
        }

        private void CloseCustomMenu()
        {
            CustomMenuOverlay.Visibility = Visibility.Collapsed;
            _suppressNextRightClick = false;
            _dragFiles = null;
            _dragTargetDirectory = null;
            _dragVm = null;
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_dragVm != null) _dragVm.DropFilesCommand.Execute((_dragFiles, false, _dragTargetDirectory));
            CloseCustomMenu();
        }

        private void MenuMove_Click(object sender, RoutedEventArgs e)
        {
            if (_dragVm != null) _dragVm.DropFilesCommand.Execute((_dragFiles, true, _dragTargetDirectory));
            CloseCustomMenu();
        }

        private void MenuShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (_dragVm != null) _dragVm.CreateShortcutsAtCommand.Execute((_dragFiles, _dragTargetDirectory));
            CloseCustomMenu();
        }

        private void MenuCancel_Click(object sender, RoutedEventArgs e)
        {
            CloseCustomMenu();
        }

        /// <summary>ドロップデータが履歴・ツリーのナビゲーション用かどうかを判定し、遷移先パスを返す。</summary>
        private static string? GetNavigationPathFromDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(DirectoryItemViewModel)))
            {
                var dirItem = e.Data.GetData(typeof(DirectoryItemViewModel)) as DirectoryItemViewModel;
                if (dirItem != null && !string.IsNullOrEmpty(dirItem.FullPath))
                    return dirItem.FullPath;
            }
            if (e.Data.GetDataPresent(typeof(HistoryItemViewModel)))
            {
                var historyItem = e.Data.GetData(typeof(HistoryItemViewModel)) as HistoryItemViewModel;
                if (historyItem != null && !string.IsNullOrEmpty(historyItem.Path))
                    return historyItem.Path;
            }
            return null;
        }

        /// <summary>検索状態をクリアし、通常モードに戻したうえで指定パスへ遷移する（お気に入り等ドロップ時）。</summary>
        private void ApplySearchClearAndNavigate(TabItemViewModel vm, string path)
        {
            SearchHistoryPopup.IsOpen = false;
            vm.SearchText = string.Empty;
            vm.IsSearching = false;
            UpdateGridView(false);
            vm.NavigateCommand.Execute(path);
        }

        /// <summary>検索バーにフォーカスを移す（Ctrl+F 用）。</summary>
        public void FocusSearchBox()
        {
            _userInitiatedSearchFocus = true;
            SearchTextBox?.Focus();
            SearchTextBox?.SelectAll();
        }

        public void FocusList()
        {
            var list = GetActiveFileList();
            if (list == null) return;
            // すでにリスト内（アイテム含む）にフォーカスがある場合は何もしない
            if (list.IsKeyboardFocusWithin) return;

            list.Focus();
            if (list.Items.Count == 0) return;

            var item = list.SelectedItem ?? list.Items[0];
            if (list.SelectedItem == null)
                list.SelectedIndex = 0;

            list.ScrollIntoView(item);

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                var container = list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    container.Focus();
                    Keyboard.Focus(container);
                }
                else
                    list.Focus();
            }));
        }

        private void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            // アイコンビュー安定化：レイアウト競合防止のため、修飾キーなしのキー（F5・矢印等）を無効化。ツールバーの更新ボタンは有効。Ctrl+C/V/Xなどは許可。
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = Keyboard.Modifiers;
            if (DataContext is TabItemViewModel vm && vm.FileViewMode != FileViewMode.Details)
            {
                var qpDef = App.KeyBindings.Get("Window.QuickPreview");
                bool isQuickPreviewKey = qpDef != null && actualKey == qpDef.ActiveKey && mods == qpDef.ActiveModifiers;
                if (!isQuickPreviewKey && (mods & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) == ModifierKeys.None)
                {
                    e.Handled = true;
                    return;
                }
            }
            if (sender is ListBox listBox && DataContext is TabItemViewModel vm2)
            {
                // Select All
                if (App.KeyBindings.Matches("FileList.SelectAll", actualKey, mods))
                {
                    listBox.SelectAll();
                    e.Handled = true;
                }
                // Copy
                else if (App.KeyBindings.Matches("FileList.Copy", actualKey, mods))
                {
                    vm2.CopyItemsCommand.Execute(listBox.SelectedItems);
                    listBox.Focus();
                    FocusFirstSelectedItem();
                    e.Handled = true;
                }
                // Paste
                else if (App.KeyBindings.Matches("FileList.Paste", actualKey, mods))
                {
                    vm2.PasteItemsCommand.Execute(null);
                    listBox.Focus();
                    e.Handled = true;
                }
                // Cut
                else if (App.KeyBindings.Matches("FileList.Cut", actualKey, mods))
                {
                    vm2.CutItemsCommand.Execute(listBox.SelectedItems);
                    listBox.Focus();
                    FocusFirstSelectedItem();
                    e.Handled = true;
                }
                // New Folder
                else if (App.KeyBindings.Matches("FileList.NewFolder", actualKey, mods))
                {
                    vm2.CreateNewFolderCommand.Execute(null);
                    e.Handled = true;
                }
                // Rename
                else if (App.KeyBindings.Matches("FileList.Rename", actualKey, mods))
                {
                    if (listBox.SelectedItem is FileItem item)
                    {
                        vm2.RenameItemCommand.Execute(item);
                        e.Handled = true;
                    }
                }
                // Delete
                else if (App.KeyBindings.Matches("FileList.Delete", actualKey, mods))
                {
                    int selectedIndex = listBox.SelectedIndex;
                    vm2.DeleteItemsCommand.Execute(listBox.SelectedItems);
                    e.Handled = true;
                    // 削除後に次の項目を選択し、フォーカスをリストに戻す。FileSystemWatcher による Refresh 後も同じ位置を維持するため復元用インデックスを記録する。
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        int count = listBox.Items.Count;
                        int indexToSelect = count == 0 ? -1 : Math.Min(selectedIndex, count - 1);
                        if (indexToSelect >= 0)
                        {
                            listBox.SelectedIndex = indexToSelect;
                            vm2.SelectionIndexToRestore = indexToSelect;
                            listBox.Focus();
                            var item = listBox.Items[indexToSelect];
                            listBox.ScrollIntoView(item);
                            var container = listBox.ItemContainerGenerator.ContainerFromIndex(indexToSelect) as ListBoxItem;
                            if (container != null)
                            {
                                container.Focus();
                                Keyboard.Focus(container);
                            }
                        }
                        else
                        {
                            listBox.Focus();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                // Open
                else if (App.KeyBindings.Matches("FileList.Open", actualKey, mods))
                {
                    if (listBox.SelectedItem is FileItem item)
                    {
                        vm2.OpenItemCommand.Execute(item);
                    }
                    e.Handled = true;
                }
                // Go Up
                else if (App.KeyBindings.Matches("FileList.GoUp", actualKey, mods))
                {
                    vm2.UpCommand.Execute(null);
                    e.Handled = true;
                }
                // Refresh
                else if (App.KeyBindings.Matches("FileList.Refresh", actualKey, mods))
                {
                    vm2.RefreshCommand.Execute(null);
                    e.Handled = true;
                }
                // Back
                else if (App.KeyBindings.Matches("FileList.Back", actualKey, mods))
                {
                    vm2.BackCommand.Execute(null);
                    e.Handled = true;
                }
                // Forward
                else if (App.KeyBindings.Matches("FileList.Forward", actualKey, mods))
                {
                    vm2.ForwardCommand.Execute(null);
                    e.Handled = true;
                }
                // インクリメンタルサーチ（アルファベット・数字キー、修飾キーなし or Shift のみ）
                else if (TryGetIncrementalSearchChar(e, out char searchChar))
                {
                    HandleIncrementalSearch(listBox, vm2, searchChar);
                    e.Handled = true;
                }
            }
            else if (sender is ListView listView && DataContext is TabItemViewModel vmDetail)
            {
                // 詳細ビュー（FileListView）用のキー処理
                if (App.KeyBindings.Matches("FileList.Refresh", actualKey, mods))
                {
                    vmDetail.RefreshCommand.Execute(null);
                    e.Handled = true;
                }
                else if (App.KeyBindings.Matches("FileList.Open", actualKey, mods) && listView.SelectedItem is FileItem enterItem)
                {
                    vmDetail.OpenItemCommand.Execute(enterItem);
                    e.Handled = true;
                }
                else if (App.KeyBindings.Matches("FileList.GoUp", actualKey, mods))
                {
                    vmDetail.UpCommand.Execute(null);
                    e.Handled = true;
                }
                else if (App.KeyBindings.Matches("FileList.Back", actualKey, mods))
                {
                    vmDetail.BackCommand.Execute(null);
                    e.Handled = true;
                }
                else if (App.KeyBindings.Matches("FileList.Forward", actualKey, mods))
                {
                    vmDetail.ForwardCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                string propertyName = "";
                var headerText = header.Content?.ToString();
                switch (headerText)
                {
                    case "名前": propertyName = "Name"; break;
                    case "場所": propertyName = "LocationType"; break;
                    case "パス": propertyName = "FullPath"; break;
                    case "更新日時": propertyName = "LastModified"; break;
                    case "種類": propertyName = "TypeName"; break;
                    case "サイズ": propertyName = "Size"; break;
                }

                if (!string.IsNullOrEmpty(propertyName) && DataContext is TabItemViewModel vm)
                {
                    vm.SortCommand.Execute(propertyName);
                }
            }
        }

        private void FileListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // ViewportWidthChange != 0 : スクロールバーの出現/消失による幅の変化
            // ExtentHeightChange != 0   : アイテム追加などによるコンテンツ高さの変化（スクロールバー出現の予兆）
            if (e.ViewportWidthChange != 0 || e.ExtentHeightChange != 0)
            {
                if (FileListView.View is GridView gridView)
                {
                    foreach (var column in gridView.Columns)
                    {
                        // Width の MultiBinding を強制的に再評価させる
                        BindingOperations.GetMultiBindingExpression(column, GridViewColumn.WidthProperty)?.UpdateTarget();
                    }
                }
            }
        }

        private void BreadcrumbScrollViewer_Loaded(object sender, RoutedEventArgs e) => ScrollBreadcrumbToEnd();
        private void BreadcrumbScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ペイン幅変更時にレイアウトを強制再計算し、パンくずの描画欠けを防止
            BreadcrumbScrollViewer?.InvalidateMeasure();
            BreadcrumbScrollViewer?.InvalidateArrange();
            ScrollBreadcrumbToEnd();
        }
        private void BreadcrumbScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentWidthChange != 0) ScrollBreadcrumbToEnd();
        }
        private void ScrollBreadcrumbToEnd()
        {
            if (BreadcrumbScrollViewer?.ScrollableWidth > 0) BreadcrumbScrollViewer.ScrollToRightEnd();
        }

        private void PathBreadcrumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TabItemViewModel vm)
            {
                vm.IsPathEditMode = true;
                FocusPathTextBox();
            }
        }

        private async void BreadcrumbDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
            if (sender is not ToggleButton tb || tb.DataContext is not NavigationPathSegment segment || DataContext is not TabItemViewModel vm)
                return;

            if (tb.IsChecked == true)
            {
                var list = await vm.GetSubfoldersAsync(segment.FullPath);
                if (list.Count == 0)
                {
                    tb.IsChecked = false;
                    return;
                }
                BreadcrumbSubfoldersListBox.ItemsSource = list;
                BreadcrumbSubfoldersListBox.SelectedItem = null;
                BreadcrumbSubfoldersPopup.PlacementTarget = tb;
                BreadcrumbSubfoldersPopup.IsOpen = true;
            }
            else
            {
                BreadcrumbSubfoldersPopup.IsOpen = false;
            }
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] BreadcrumbDropdownButton_Click: {ex.Message}"); }
        }

        private void BreadcrumbSubfoldersPopup_Opened(object sender, EventArgs e)
        {
            _breadcrumbPopupLeaveCheckTimer?.Stop();
            _breadcrumbPopupLeaveCheckTimer = null;
            _breadcrumbPopupOpenDelayTimer?.Stop();
            _breadcrumbPopupOpenDelayTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _breadcrumbPopupOpenDelayTimer.Tick += BreadcrumbPopupOpenDelayTimer_Tick;
            _breadcrumbPopupOpenDelayTimer.Start();
        }

        private void BreadcrumbPopupOpenDelayTimer_Tick(object? sender, EventArgs e)
        {
            _breadcrumbPopupOpenDelayTimer?.Stop();
            _breadcrumbPopupOpenDelayTimer = null;
            if (!BreadcrumbSubfoldersPopup.IsOpen)
                return;
            _breadcrumbPopupLeaveCheckTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _breadcrumbPopupLeaveCheckTimer.Tick += BreadcrumbPopupLeaveCheckTimer_Tick;
            _breadcrumbPopupLeaveCheckTimer.Start();
        }

        private void BreadcrumbSubfoldersPopup_Closed(object sender, EventArgs e)
        {
            _breadcrumbPopupOpenDelayTimer?.Stop();
            _breadcrumbPopupOpenDelayTimer = null;
            _breadcrumbPopupLeaveCheckTimer?.Stop();
            _breadcrumbPopupLeaveCheckTimer = null;
            // ポップアップが閉じられたら、ボタンのチェックを外す
            if (BreadcrumbSubfoldersPopup.PlacementTarget is ToggleButton tb)
            {
                tb.IsChecked = false;
            }
        }

        private void BreadcrumbPopupLeaveCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (!BreadcrumbSubfoldersPopup.IsOpen)
                return;

            // 1. Mouse.DirectlyOver を使って、現在マウスの下にある要素を確実に取得
            // （Popupは同一スレッドの別ウィンドウなので、DirectlyOverで取得可能）
            var mouseOver = Mouse.DirectlyOver as DependencyObject;
            var popupChild = BreadcrumbSubfoldersPopup.Child;
            var placementTarget = BreadcrumbSubfoldersPopup.PlacementTarget as DependencyObject;

            if (mouseOver != null)
            {
                // マウスがポップアップ（メニュー）上、またはその子孫上にあるか
                if (popupChild != null && (mouseOver == popupChild || IsDescendantOf(mouseOver, popupChild)))
                    return;

                // マウスが配置ターゲット（▼ボタン）上、またはその子孫上にあるか
                if (placementTarget != null && (mouseOver == placementTarget || IsDescendantOf(mouseOver, placementTarget)))
                    return;
            }

            // 2. マウスが要素上にない（null）、またはメニュー・ボタン以外の場所（メインウィンドウの背景など）にある場合
            // 念のため従来の座標チェックも行い、確実にメインウィンドウ領域内でターゲット外であることを確認してから閉じる
            if (!GetCursorPos(out POINT p))
                return;
            var screenPos = new Point(p.X, p.Y);
            var window = Window.GetWindow(this);
            if (window == null)
                return;

            try
            {
                var windowScreenRect = new Rect(window.PointToScreen(new Point(0, 0)), new Size(window.ActualWidth, window.ActualHeight));
                if (!windowScreenRect.Contains(screenPos))
                {
                    // マウスがメインウィンドウの矩形外にある場合
                    // Mouse.DirectlyOver が null (アプリ外) なら閉じてよいが、
                    // コンテキストメニューや別ウィンドウの可能性もあるため、安全側に倒して「何もしない」か、
                    // 明確にアプリ外なら「閉じる」べきか。
                    // ここでは「メニュー外に出たら閉じる」要件なので、明らかにアプリ外なら閉じて良いが、
                    // 誤判定を防ぐため Mouse.DirectlyOver が null の場合のみ閉じる判定に進む手もある。
                    
                    // Mouse.DirectlyOver が null で ウィンドウ外 → アプリ外へ出た → 閉じるべき
                    if (mouseOver == null)
                        BreadcrumbSubfoldersPopup.IsOpen = false;
                    
                    return;
                }

                var relPos = window.PointFromScreen(screenPos);
                var hit = VisualTreeHelper.HitTest(window, relPos);
                
                // ヒットしない（null）はウィンドウ内の非クライアント領域などの可能性 → 閉じてよい
                // ヒットしたがターゲット外 → 閉じる
                if (hit == null)
                {
                    BreadcrumbSubfoldersPopup.IsOpen = false;
                    return;
                }

                // ここでの HitTest は Popup を貫通してメインウィンドウの要素を拾う（これがバグの原因だった）。
                // しかし、最初の Mouse.DirectlyOver チェックで「ポップアップ上」なら既に return しているため、
                // ここに来る＝「ポップアップ上ではない」ことが確定している。
                // したがって、安心して HitTest の結果を信用してよい。
                
                var overPlacement = hit.VisualHit == placementTarget || IsDescendantOf(hit.VisualHit, placementTarget);
                if (!overPlacement)
                    BreadcrumbSubfoldersPopup.IsOpen = false;
            }
            catch
            {
                // エラー時は安全のため何もしない
            }
        }




        private static bool IsDescendantOf(DependencyObject? child, DependencyObject? ancestor)
            => Helpers.VisualTreeExtensions.IsDescendantOf(child, ancestor);

        private void BreadcrumbSubfoldersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BreadcrumbSubfoldersListBox.SelectedItem is not NavigationPathSegment seg || DataContext is not TabItemViewModel vm)
                return;
            BreadcrumbSubfoldersPopup.IsOpen = false;
            vm.NavigateCommand.Execute(seg.FullPath);
        }

        private void BreadcrumbPopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                BreadcrumbSubfoldersPopup.IsOpen = false;
                e.Handled = true;
            }
        }


        private void PathEditButton_Checked(object sender, RoutedEventArgs e)
        {
            FocusPathTextBox();
        }
        
        private void FocusPathTextBox()
        {
            // TextBoxにフォーカスを当てる
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PathTextBox.Focus();
                PathTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>ユーザー操作（Ctrl+F 等）でフォーカスを移す際に true にし、GotFocus で履歴ポップアップを開く許可を与える。</summary>
        private bool _userInitiatedSearchFocus;

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // タブ切替やプログラム的フォーカス移動では履歴ポップアップを開かない。
            // ユーザー操作（Ctrl+F → FocusSearchBox）の場合のみ開く。
            // マウスクリックは PreviewMouseDown で処理済み、Down キーは PreviewKeyDown で処理済み。
            if (_userInitiatedSearchFocus)
            {
                _userInitiatedSearchFocus = false;
                OpenSearchHistory();
            }
        }

        private void SearchTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e) => OpenSearchHistory();

        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // フォーカスが検索エリア外に移ったらポップアップを閉じる（ポップアップ内への移動は除外）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!SearchHistoryPopup.IsOpen) return;
                var newFocus = Keyboard.FocusedElement as DependencyObject;
                if (newFocus != null && (IsDescendantOf(newFocus, SearchTextBox) || IsDescendantOf(newFocus, SearchHistoryPopup.Child as DependencyObject)))
                    return;
                SearchHistoryPopup.IsOpen = false;
            }), DispatcherPriority.Input);
        }

        private void SearchHistoryListBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // 履歴リストからフォーカスが外れたらポップアップを閉じる（検索ボックスへの戻りは除外）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!SearchHistoryPopup.IsOpen) return;
                var newFocus = Keyboard.FocusedElement as DependencyObject;
                if (newFocus != null && (IsDescendantOf(newFocus, SearchTextBox) || IsDescendantOf(newFocus, SearchHistoryPopup.Child as DependencyObject)))
                    return;
                SearchHistoryPopup.IsOpen = false;
            }), DispatcherPriority.Input);
        }

        private async void OpenSearchHistory()
        {
            try
            {
            if (DataContext is TabItemViewModel vm)
            {
                // データを強制ロードして待機（これで0件問題を回避）
                await vm.LoadSearchHistoryAsync();

                // await 後にフォーカスが既に SearchTextBox から離れている場合はポップアップを開かない
                // （非同期待機中にフォーカス遷移が完了し、一瞬だけポップアップが表示される現象を防止）
                if (!SearchTextBox.IsKeyboardFocusWithin)
                    return;

                // 履歴が0件でも「履歴はありません」を表示するためにPopupを開く
                SearchHistoryPopup.PlacementTarget = SearchTextBox;
                SearchHistoryPopup.Width = SearchTextBox.ActualWidth;
                SearchHistoryPopup.IsOpen = true;
            }
            }
            catch (Exception ex) { _ = App.FileLogger.LogAsync($"[ERR] OpenSearchHistory: {ex.Message}"); }
        }

        private void SearchHistoryItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 履歴アイテムをクリックしたら検索を実行して閉じる（選択した履歴のモードに自動切り替え）
            if (sender is ListBoxItem item && item.DataContext is SearchHistoryItem historyItem && DataContext is TabItemViewModel vm)
            {
                SearchHistoryPopup.IsOpen = false;
                vm.SelectSearchHistoryCommand.Execute(historyItem);
            }
        }

        private void SearchHistoryEmpty_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 「履歴はありません」をクリックしたら非表示にする
            SearchHistoryPopup.IsOpen = false;
        }

        private void SearchHistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 上下キーでの移動時は検索を実行しない（Enterまたはクリックで実行）
            // 以前のロジックは削除
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                 // 下キー: 一覧を上から順にフォーカス／選択。履歴が未表示なら開き、表示中ならリストにフォーカスして先頭を選択。
                 if (!SearchHistoryPopup.IsOpen)
                 {
                     OpenSearchHistory();
                 }
                 if (SearchHistoryPopup.IsOpen)
                 {
                    // すでにフォーカスがポップアップ内（履歴リスト）にある場合はキーを消費するだけ（リスト側で処理されるのでここでは何もしない）
                    var focused = Keyboard.FocusedElement as DependencyObject;
                    if (focused != null && IsDescendantOf(focused, SearchHistoryPopup.Child as DependencyObject))
                    {
                         e.Handled = true;
                         return;
                    }
                    // 検索ボックスにフォーカスがあるときだけ、リストにフォーカスして先頭を選択（遅延で確実にフォーカスを移す）
                    e.Handled = true;
                    if (SearchHistoryListBox.Items.Count > 0)
                    {
                         SearchHistoryListBox.SelectedIndex = 0;
                         Dispatcher.BeginInvoke(() =>
                         {
                             FocusManager.SetFocusedElement(SearchHistoryPopup, SearchHistoryListBox);
                             var item = SearchHistoryListBox.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                             item?.Focus();
                         }, DispatcherPriority.Input);
                    }
                 }
            }
            else if (e.Key == Key.Escape)
            {
                if (SearchHistoryPopup.IsOpen)
                {
                    SearchHistoryPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }

                // 多段 Esc リセット:
                // 1回目 — テキストが入力中なら全消去
                // 2回目 — テキストが空ならフィルタ（サイズ・日付・スコープ）も全消去して ClearSearch
                if (DataContext is TabItemViewModel tabVm)
                {
                    if (!string.IsNullOrEmpty(tabVm.SearchText))
                    {
                        // 1段目: テキストのみクリア
                        tabVm.SearchText = string.Empty;
                    }
                    else
                    {
                        // 2段目: フィルタ全クリア + 検索解除
                        var mainVm = Window.GetWindow(this)?.DataContext as MainViewModel;
                        mainVm?.SearchFilter.ResetAllFiltersCommand.Execute(null);
                        // スコープもリセット
                        mainVm?.IndexSearchSettings.RebuildScopeItems(null);
                        tabVm.ClearSearchCommand.Execute(null);
                    }
                    e.Handled = true;
                }
            }
        }

        private void SearchHistoryListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 下キー: 一覧を上から順にフォーカス／選択。一番下にいる場合は何もしない。
            if (e.Key == Key.Down && SearchHistoryListBox.Items.Count > 0 && SearchHistoryListBox.SelectedIndex >= SearchHistoryListBox.Items.Count - 1)
            {
                e.Handled = true;
                return;
            }
            // 上キー: 一覧を上方向にフォーカス／選択。一番上にいる場合は何もしない。
            if (e.Key == Key.Up && SearchHistoryListBox.SelectedIndex <= 0)
            {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                if (SearchHistoryListBox.SelectedItem is SearchHistoryItem historyItem && DataContext is TabItemViewModel vm)
                {
                    SearchHistoryPopup.IsOpen = false;
                    vm.SelectSearchHistoryCommand.Execute(historyItem);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                SearchHistoryPopup.IsOpen = false;
                SearchTextBox.Focus();
                e.Handled = true;
            }
        }

        // ── スコープ Popup ──

        private void ScopeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSizePopup();
            CloseDatePopup();
            ClosePresetPopup();
            ScopeFilterPopup.IsOpen = !ScopeFilterPopup.IsOpen;
        }

        /// <summary>Popup の Border にフェードイン + スライドインアニメーションを適用する共通ヘルパー。</summary>
        private static void AnimatePopupOpen(Border border, Action? afterAction = null)
        {
            if (!WindowSettings.ShowListEffectsEnabled)
            {
                border.Opacity = 1;
                border.RenderTransform = new TranslateTransform(0, 0);
                afterAction?.Invoke();
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var tt = new TranslateTransform(0, -5);
            border.RenderTransform = tt;
            border.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var slideIn = new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease };
            tt.BeginAnimation(TranslateTransform.YProperty, slideIn);

            afterAction?.Invoke();
        }

        private void ScopeFilterPopup_Opened(object sender, EventArgs e)
        {
            // 外側クリック dismiss + ウィンドウ非アクティブ時オートクローズを購読
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown += ScopePopup_OutsideClick;
                window.Deactivated += ScopePopup_WindowDeactivated;
            }

            if (sender is not System.Windows.Controls.Primitives.Popup popup) return;
            if (popup.Child is not Border border) return;

            AnimatePopupOpen(border);
        }

        /// <summary>ウィンドウが非アクティブになったらスコープ Popup を閉じる（スマート・クローズ）</summary>
        private void ScopePopup_WindowDeactivated(object? sender, EventArgs e)
        {
            CloseScopePopup();
        }

        private void ScopePopup_OutsideClick(object sender, MouseButtonEventArgs e)
        {
            if (!ScopeFilterPopup.IsOpen) return;

            // Popup 内クリック判定: Popup.Child の InputHitTest で座標がヒットすれば内部クリック
            var popupChild = ScopeFilterPopup.Child as UIElement;
            if (popupChild != null)
            {
                var posInPopup = e.GetPosition(popupChild);
                if (popupChild.InputHitTest(posInPopup) != null)
                    return;
            }

            // ボタン自体のクリックはトグルで処理するので閉じない
            var posInButton = e.GetPosition(ScopeFilterButton);
            if (posInButton.X >= 0 && posInButton.Y >= 0
                && posInButton.X <= ScopeFilterButton.ActualWidth
                && posInButton.Y <= ScopeFilterButton.ActualHeight)
                return;

            e.Handled = true;
            CloseScopePopup();
        }

        /// <summary>スコープ Popup を閉じ、イベント購読を解除する</summary>
        private void CloseScopePopup()
        {
            if (!ScopeFilterPopup.IsOpen) return;
            ScopeFilterPopup.IsOpen = false;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= ScopePopup_OutsideClick;
                window.Deactivated -= ScopePopup_WindowDeactivated;
            }
        }

        // ── サイズフィルタ Popup ──

        private void SizeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            CloseScopePopup();
            CloseDatePopup();
            ClosePresetPopup();
            SizeFilterPopup.IsOpen = !SizeFilterPopup.IsOpen;
        }

        private void SizeFilterButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchFilter;
            vm?.ResetSizeFilterCommand.Execute(null);
            CloseSizePopup();
            e.Handled = true;
        }

        private void SizeFilterPopup_Opened(object sender, EventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown += SizePopup_OutsideClick;
                window.Deactivated += SizePopup_WindowDeactivated;
            }

            if (sender is not System.Windows.Controls.Primitives.Popup popup) return;
            if (popup.Child is not Border border) return;

            AnimatePopupOpen(border, FocusSizeMinTextBox);
        }

        private void FocusSizeMinTextBox()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                SizeMinTextBox?.Focus();
                SizeMinTextBox?.SelectAll();
            });
        }

        private void SizeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                CloseSizePopup();
                e.Handled = true;
            }
        }

        private void SizePopup_WindowDeactivated(object? sender, EventArgs e)
        {
            CloseSizePopup();
        }

        private void SizePopup_OutsideClick(object sender, MouseButtonEventArgs e)
        {
            if (!SizeFilterPopup.IsOpen) return;

            var popupChild = SizeFilterPopup.Child as UIElement;
            if (popupChild != null)
            {
                var posInPopup = e.GetPosition(popupChild);
                if (popupChild.InputHitTest(posInPopup) != null)
                    return;
            }

            var posInButton = e.GetPosition(SizeFilterButton);
            if (posInButton.X >= 0 && posInButton.Y >= 0
                && posInButton.X <= SizeFilterButton.ActualWidth
                && posInButton.Y <= SizeFilterButton.ActualHeight)
                return;

            e.Handled = true;
            CloseSizePopup();
        }

        private void CloseSizePopup()
        {
            if (!SizeFilterPopup.IsOpen) return;
            SizeFilterPopup.IsOpen = false;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= SizePopup_OutsideClick;
                window.Deactivated -= SizePopup_WindowDeactivated;
            }
        }

        // ── 日付フィルタ Popup ──

        private bool _isDateStartFocused = true; // デフォルトは開始
        private bool _isSyncingCalendar; // Calendar ↔ TextBox 再帰ガード

        private void DateFilterButton_Click(object sender, RoutedEventArgs e)
        {
            CloseScopePopup();
            CloseSizePopup();
            ClosePresetPopup();
            DateFilterPopup.IsOpen = !DateFilterPopup.IsOpen;
        }

        private void DateFilterButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchFilter;
            vm?.ResetDateFilterCommand.Execute(null);
            CloseDatePopup();
            e.Handled = true;
        }

        private void DateFilterPopup_Opened(object sender, EventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown += DatePopup_OutsideClick;
                window.Deactivated += DatePopup_WindowDeactivated;
            }

            // VM の PropertyChanged を購読してテキスト→カレンダー同期
            var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchFilter;
            if (vm != null)
                vm.PropertyChanged += DateFilter_PropertyChanged;

            if (sender is not System.Windows.Controls.Primitives.Popup popup) return;
            if (popup.Child is not Border border) return;

            AnimatePopupOpen(border, () => { FocusDateStartTextBox(); SyncCalendarFromText(); });
        }

        private void FocusDateStartTextBox()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                DateStartTextBox?.Focus();
                DateStartTextBox?.SelectAll();
            });
        }

        /// <summary>テキスト → カレンダー同期: ParsedStartDate/EndDate からカレンダーの選択状態と表示月を更新。</summary>
        private void SyncCalendarFromText()
        {
            if (_isSyncingCalendar) return;
            _isSyncingCalendar = true;
            try
            {
                var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchFilter;
                var startDt = vm?.ParsedStartDate;
                var endDt = vm?.ParsedEndDate;

                DateCalendar.SelectedDates.Clear();

                if (startDt is DateTime s && endDt is DateTime en && s <= en)
                {
                    // 範囲が大きすぎると Calendar が重くなるので上限設定
                    if ((en - s).TotalDays <= 366)
                    {
                        DateCalendar.SelectedDates.AddRange(s, en);
                    }
                    else
                    {
                        DateCalendar.SelectedDates.Add(s);
                    }
                    DateCalendar.DisplayDate = s;
                }
                else if (startDt is DateTime sd)
                {
                    DateCalendar.SelectedDates.Add(sd);
                    DateCalendar.DisplayDate = sd;
                }
                else if (endDt is DateTime ed)
                {
                    DateCalendar.SelectedDates.Add(ed);
                    DateCalendar.DisplayDate = ed;
                }
                else
                {
                    DateCalendar.DisplayDate = DateTime.Today;
                }
            }
            finally
            {
                _isSyncingCalendar = false;
            }
        }

        /// <summary>VM の ParsedStartDate / ParsedEndDate 変更時にカレンダーを同期。</summary>
        private void DateFilter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ViewModels.SearchFilterViewModel.ParsedStartDate)
                              or nameof(ViewModels.SearchFilterViewModel.ParsedEndDate))
            {
                SyncCalendarFromText();
            }
        }

        private void DateStartTextBox_GotFocus(object sender, RoutedEventArgs e)
            => _isDateStartFocused = true;

        private void DateEndTextBox_GotFocus(object sender, RoutedEventArgs e)
            => _isDateStartFocused = false;

        private void DateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingCalendar) return; // テキスト→カレンダー同期中は無視
            if (sender is not System.Windows.Controls.Calendar cal || cal.SelectedDates.Count == 0) return;
            var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchFilter;
            if (vm == null) return;

            _isSyncingCalendar = true;
            try
            {
                if (cal.SelectedDates.Count == 1)
                {
                    // 単一選択 → フォーカス中の TextBox を更新
                    var date = cal.SelectedDates[0];
                    if (_isDateStartFocused)
                        vm.SetStartDateFromCalendar(date);
                    else
                        vm.SetEndDateFromCalendar(date);
                }
                else
                {
                    // 範囲選択 → 最小日=開始, 最大日=終了
                    var sorted = cal.SelectedDates.OrderBy(d => d).ToList();
                    vm.SetStartDateFromCalendar(sorted.First());
                    vm.SetEndDateFromCalendar(sorted.Last());
                }
            }
            finally
            {
                _isSyncingCalendar = false;
            }
        }

        private void DateTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CloseDatePopup();
                SearchTextBox?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseDatePopup();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && sender == DateStartTextBox && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                DateEndTextBox?.Focus();
                DateEndTextBox?.SelectAll();
                e.Handled = true;
            }
        }

        private void DatePopup_WindowDeactivated(object? sender, EventArgs e)
        {
            CloseDatePopup();
        }

        private void DatePopup_OutsideClick(object sender, MouseButtonEventArgs e)
        {
            if (!DateFilterPopup.IsOpen) return;

            var popupChild = DateFilterPopup.Child as UIElement;
            if (popupChild != null)
            {
                var posInPopup = e.GetPosition(popupChild);
                if (popupChild.InputHitTest(posInPopup) != null)
                    return;
            }

            var posInButton = e.GetPosition(DateFilterButton);
            if (posInButton.X >= 0 && posInButton.Y >= 0
                && posInButton.X <= DateFilterButton.ActualWidth
                && posInButton.Y <= DateFilterButton.ActualHeight)
                return;

            e.Handled = true;
            CloseDatePopup();
        }

        private void CloseDatePopup()
        {
            if (!DateFilterPopup.IsOpen) return;
            DateFilterPopup.IsOpen = false;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= DatePopup_OutsideClick;
                window.Deactivated -= DatePopup_WindowDeactivated;
            }

            // PropertyChanged 購読解除
            var vm = (window?.DataContext as MainViewModel)?.SearchFilter;
            if (vm != null)
                vm.PropertyChanged -= DateFilter_PropertyChanged;
        }

        private void ScopeItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is IndexSearchScopeItemViewModel item)
            {
                item.IsSelected = !item.IsSelected;
                e.Handled = true;
            }
        }

        // ── プリセット Popup ──

        private void SearchPresetButton_Click(object sender, RoutedEventArgs e)
        {
            CloseScopePopup();
            CloseSizePopup();
            CloseDatePopup();

            var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchPresets;
            vm?.RefreshFilteredPresets();

            SearchPresetPopup.IsOpen = !SearchPresetPopup.IsOpen;
        }

        private void SearchPresetButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var mainVm = Window.GetWindow(this)?.DataContext as MainViewModel;
            // フィルタ（サイズ・日付）をすべてクリア（通知・ログは ResetAllFilters 内で処理）
            mainVm?.SearchFilter.ResetAllFiltersCommand.Execute(null);
            // スコープをリセット（全選択に戻す）
            mainVm?.IndexSearchSettings.RebuildScopeItems(null);
            // 検索テキストをクリア
            if (DataContext is TabItemViewModel tabVm)
                tabVm.SearchText = string.Empty;
            ClosePresetPopup();
            e.Handled = true;
        }

        private void SearchPresetPopup_Opened(object sender, EventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown += PresetPopup_OutsideClick;
                window.Deactivated += PresetPopup_WindowDeactivated;
            }

            if (sender is not System.Windows.Controls.Primitives.Popup popup) return;
            if (popup.Child is not Border border) return;

            AnimatePopupOpen(border);
        }

        private void PresetPopup_WindowDeactivated(object? sender, EventArgs e)
        {
            ClosePresetPopup();
        }

        private void PresetPopup_OutsideClick(object sender, MouseButtonEventArgs e)
        {
            if (!SearchPresetPopup.IsOpen) return;

            var popupChild = SearchPresetPopup.Child as UIElement;
            if (popupChild != null)
            {
                var posInPopup = e.GetPosition(popupChild);
                if (popupChild.InputHitTest(posInPopup) != null)
                    return;
            }

            var posInButton = e.GetPosition(SearchPresetButton);
            if (posInButton.X >= 0 && posInButton.Y >= 0
                && posInButton.X <= SearchPresetButton.ActualWidth
                && posInButton.Y <= SearchPresetButton.ActualHeight)
                return;

            e.Handled = true;
            ClosePresetPopup();
        }

        private void ClosePresetPopup()
        {
            if (!SearchPresetPopup.IsOpen) return;
            SearchPresetPopup.IsOpen = false;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= PresetPopup_OutsideClick;
                window.Deactivated -= PresetPopup_WindowDeactivated;
            }
        }

        private void PresetItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is SearchPresetDto preset)
            {
                var vm = (Window.GetWindow(this)?.DataContext as MainViewModel)?.SearchPresets;
                vm?.ApplyPresetCommand.Execute(preset);
                ClosePresetPopup();
                e.Handled = true;
            }
        }

        /// <summary>
        /// インクリメンタルサーチ対象の入力文字を取得する。
        /// Ctrl / Alt が押されている場合は対象外。Shift は大文字変換に使用。
        /// 対象: A-Z, 0-9, テンキー 0-9。
        /// </summary>
        private static bool TryGetIncrementalSearchChar(KeyEventArgs e, out char c)
        {
            c = '\0';
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.None)
                return false;
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                c = (char)('a' + (e.Key - Key.A));
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) c = char.ToUpper(c);
                return true;
            }
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            { c = (char)('0' + (e.Key - Key.D0)); return true; }
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            { c = (char)('0' + (e.Key - Key.NumPad0)); return true; }
            return false;
        }

        /// <summary>
        /// インクリメンタルサーチ処理。
        /// - 同一文字の連打 → 同頭文字の次候補へサイクル（末尾で先頭折り返し）
        /// - 異なる文字を追加 → プレフィックスが一致する先頭候補へジャンプ
        /// - 500ms 無入力でバッファをリセット
        /// </summary>
        private void HandleIncrementalSearch(ListBox listBox, TabItemViewModel vm, char c)
        {
            if (_incrementalSearchTimer == null)
            {
                _incrementalSearchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _incrementalSearchTimer.Tick += (_, _) =>
                {
                    _incrementalSearchTimer.Stop();
                    _incrementalSearchBuffer = string.Empty;
                };
            }

            // サイクルモード判定: バッファが全て同じ文字 かつ 今回もその文字
            bool isCycleMode = _incrementalSearchBuffer.Length > 0
                && _incrementalSearchBuffer.All(x => char.ToLowerInvariant(x) == char.ToLowerInvariant(c));

            _incrementalSearchBuffer = isCycleMode ? c.ToString() : _incrementalSearchBuffer + c;

            _incrementalSearchTimer.Stop();
            _incrementalSearchTimer.Start();

            var matches = listBox.Items
                .Cast<FileItem>()
                .Select((item, idx) => (item, idx))
                .Where(x => x.item.Name.StartsWith(_incrementalSearchBuffer, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0) return;

            int currentIndex = listBox.SelectedIndex;
            int targetIndex;

            if (isCycleMode && currentIndex >= 0)
            {
                var next = matches.FirstOrDefault(x => x.idx > currentIndex);
                targetIndex = next.item != null ? next.idx : matches[0].idx;
            }
            else
            {
                targetIndex = matches[0].idx;
            }

            listBox.UnselectAll();
            listBox.SelectedIndex = targetIndex;
            listBox.ScrollIntoView(listBox.Items[targetIndex]);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (listBox.ItemContainerGenerator.ContainerFromIndex(targetIndex) is ListBoxItem container)
                {
                    container.Focus();
                    Keyboard.Focus(container);
                }
            }), DispatcherPriority.Loaded);
        }
    }
}
