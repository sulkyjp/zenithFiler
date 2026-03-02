using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Runtime.InteropServices;
using VanaraShell = Vanara.Windows.Shell;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public class TreeViewDragDropBehavior
    {
        private enum DropPosition { Before, Inside, After }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static InsertionAdorner? _insertionAdorner;
        private static DragAdorner? _dragAdorner;
        /// <summary>Remove 時に AdornedElement がツリー外だと GetAdornerLayer が null になるため、Add 時の layer を保持する。</summary>
        private static AdornerLayer? _dragAdornerLayer;
        /// <summary>ツリービューロック中にドラッグオーバーでステータスメッセージを1回だけ表示するためのフラグ。</summary>
        private static bool _treeLockNotifyShownThisDrag;

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TreeViewDragDropBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeView treeView)
            {
                if ((bool)e.NewValue)
                {
                    treeView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                    treeView.MouseMove += OnMouseMove;
                    treeView.DragOver += OnDragOver;
                    treeView.DragLeave += OnDragLeave;
                    treeView.Drop += OnDrop;
                    treeView.AllowDrop = true;
                }
                else
                {
                    treeView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                    treeView.MouseMove -= OnMouseMove;
                    treeView.DragOver -= OnDragOver;
                    treeView.DragLeave -= OnDragLeave;
                    treeView.Drop -= OnDrop;
                    treeView.AllowDrop = false;
                }
            }
        }

        private static Point _startPoint;
        private static object? _draggedItem;
        private static TreeViewItem? _draggedContainer;

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeLockNotifyShownThisDrag = false;
            _startPoint = e.GetPosition(null);
            
            // クリックされたアイテムを特定
            if (sender is TreeView treeView)
            {
                var source = e.OriginalSource as DependencyObject;
                var container = GetContainerFromElement(treeView, source);
                
                if (container != null)
                {
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
            if (Math.Abs(pos.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is TreeView treeView)
                {
                    var data = new DataObject();
                    
                    // お気に入り項目の移動用データ
                    data.SetData(typeof(FavoriteItem), _draggedItem);
                    data.SetData(typeof(DirectoryItemViewModel), _draggedItem);
                    
                    // 外部へのドロップ用（ファイルパスがある場合）
                    if (_draggedItem is FavoriteItem favItem && !string.IsNullOrEmpty(favItem.Path))
                    {
                        var paths = new System.Collections.Specialized.StringCollection { favItem.Path };
                        data.SetFileDropList(paths);
                        
                        // ドラッグアドーナーの表示
                        ShowDragAdorner(treeView);
                    }
                    else if (_draggedItem is DirectoryItemViewModel dirItem && !string.IsNullOrEmpty(dirItem.FullPath))
                    {
                        var paths = new System.Collections.Specialized.StringCollection { dirItem.FullPath };
                        data.SetFileDropList(paths);

                        // ドラッグアドーナーの表示
                        ShowDragAdorner(treeView);
                    }

                    treeView.GiveFeedback += OnGiveFeedback;
                    try
                    {
                        DragDrop.DoDragDrop(treeView, data, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
                    }
                    finally
                    {
                        treeView.GiveFeedback -= OnGiveFeedback;
                        RemoveDragAdorner();
                    }
                }
                _draggedItem = null;
                _draggedContainer = null;
            }
        }

        private static void ShowDragAdorner(TreeView treeView)
        {
            var layer = AdornerLayer.GetAdornerLayer(treeView);
            if (layer != null)
            {
                _dragAdornerLayer = layer;
                _dragAdorner = new DragAdorner(treeView, "Aペイン、もしくはBペインにドロップしてください");
                layer.Add(_dragAdorner);
                _dragAdorner.UpdatePosition(Mouse.GetPosition(treeView));
            }
        }

        private static void RemoveDragAdorner()
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

        private static void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            _dragAdorner?.UpdatePositionFromCursor();
            e.Handled = false;
        }

        private static void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (sender is TreeView treeView)
            {
                var vm = treeView.DataContext as MainViewModel;
                if (vm?.Favorites == null) return;

                // ツリービュー（DirectoryTree）ロック時: フォルダ移動・一覧からのファイル/フォルダドロップを全て禁止（ステータスバーにメッセージ表示）
                bool isDirectoryTree = string.Equals(treeView.Name, "DirectoryTree", StringComparison.Ordinal);
                bool isTreeLockedDrop = isDirectoryTree && vm.IsTreeViewLocked &&
                    (e.Data.GetDataPresent(typeof(DirectoryItemViewModel)) || e.Data.GetDataPresent(DataFormats.FileDrop));
                if (isTreeLockedDrop)
                {
                    if (!_treeLockNotifyShownThisDrag)
                    {
                        App.Notification.Notify("ロック中のため操作不可", "ツリービューロック: ドラッグをブロック");
                        vm.TriggerTreeViewLockWarning();
                        _treeLockNotifyShownThisDrag = true;
                    }
                    return;
                }

                // ツリービュー（DirectoryTree）ロックOFF時: 一覧からのファイル/フォルダドロップで移動を許可
                if (isDirectoryTree && !vm.IsTreeViewLocked && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var data = e.Data.GetData(DataFormats.FileDrop);
                    string[]? files = data as string[];
                    if (files == null && data is System.Collections.Specialized.StringCollection sc)
                    {
                        files = new string[sc.Count];
                        sc.CopyTo(files, 0);
                    }
                    if (files != null && files.Length > 0)
                    {
                        var dropSource = e.OriginalSource as DependencyObject;
                        var dropTargetContainer = GetContainerFromElement(treeView, dropSource);
                        var targetDir = dropTargetContainer?.DataContext as DirectoryItemViewModel;
                        if (targetDir != null && !string.IsNullOrEmpty(targetDir.FullPath))
                        {
                            ClearAdorner();
                            dropTargetContainer!.IsSelected = true;
                            e.Effects = DragDropEffects.Move;
                        }
                    }
                    return;
                }

                // ツリービュー（DirectoryTree）ロックOFF時: ツリー内フォルダのドラッグ＆ドロップ移動を許可
                if (isDirectoryTree && !vm.IsTreeViewLocked && e.Data.GetDataPresent(typeof(DirectoryItemViewModel)))
                {
                    var draggedDir = e.Data.GetData(typeof(DirectoryItemViewModel)) as DirectoryItemViewModel;
                    var dropSource = e.OriginalSource as DependencyObject;
                    var dropTargetContainer = GetContainerFromElement(treeView, dropSource);
                    var targetDir = dropTargetContainer?.DataContext as DirectoryItemViewModel;
                    if (draggedDir != null && !string.IsNullOrEmpty(draggedDir.FullPath) && targetDir != null && !string.IsNullOrEmpty(targetDir.FullPath))
                    {
                        var srcNorm = PathHelper.NormalizePathForComparison(draggedDir.FullPath);
                        var tgtNorm = PathHelper.NormalizePathForComparison(targetDir.FullPath);
                        if (srcNorm.Equals(tgtNorm, StringComparison.OrdinalIgnoreCase)) return;
                        if (IsPathUnder(tgtNorm, srcNorm)) return; // 自分自身の子孫にはドロップ不可
                        ClearAdorner();
                        var pos = GetDropPositionForDirectory(dropTargetContainer!, e);
                        if (pos == DropPosition.Inside)
                        {
                            dropTargetContainer!.IsSelected = true;
                            e.Effects = DragDropEffects.Move;
                        }
                        else
                        {
                            ShowAdorner(dropTargetContainer!, pos == DropPosition.Before);
                            e.Effects = DragDropEffects.Move;
                        }
                    }
                    return;
                }

                var source = e.OriginalSource as DependencyObject;
                var targetContainer = GetContainerFromElement(treeView, source);

                // お気に入り項目の並べ替え・移動を優先。ロック時はブロック（登録は FileDrop で許可）。
                bool isFavoritesTreeOver = string.Equals(treeView.Name, "FavoritesTree", StringComparison.Ordinal);
                if (e.Data.GetDataPresent(typeof(FavoriteItem)))
                {
                    var draggedItem = e.Data.GetData(typeof(FavoriteItem)) as FavoriteItem;
                    if (draggedItem != null)
                    {
                        if (isFavoritesTreeOver && vm.Favorites.IsLocked)
                            return;

                        ClearAdorner();

                        // 自分自身や自分の子孫へのドロップは禁止
                        if (targetContainer != null)
                        {
                            var targetItem = targetContainer.DataContext as FavoriteItem;
                            if (targetItem == draggedItem || IsDescendant(draggedItem, targetItem))
                            {
                                return;
                            }

                            var pos = GetDropPosition(targetContainer, e);
                            if (pos == DropPosition.Inside && targetItem != null && targetItem.IsContainer)
                            {
                                targetContainer.IsSelected = true; // ハイライト
                                e.Effects = DragDropEffects.Move;
                            }
                            else if (pos != DropPosition.Inside)
                            {
                                ShowAdorner(targetContainer, pos == DropPosition.Before);
                                e.Effects = DragDropEffects.Move;
                            }
                        }
                        else
                        {
                            e.Effects = DragDropEffects.Move;
                        }
                        return;
                    }
                }

                // お気に入りビュー（FavoritesTree）のみ: ファイル/フォルダドロップでお気に入りに登録。ロック時も許可。
                if (isFavoritesTreeOver && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var data = e.Data.GetData(DataFormats.FileDrop);
                    string[]? files = data as string[];
                    if (files == null && data is System.Collections.Specialized.StringCollection sc)
                    {
                        files = new string[sc.Count];
                        sc.CopyTo(files, 0);
                    }

                    // フォルダまたはファイルが含まれている場合にコピー（お気に入り登録）を許可
                    if (files != null && Array.Exists(files, f => System.IO.Directory.Exists(f) || System.IO.File.Exists(f)))
                    {
                        ClearAdorner();
                        e.Effects = DragDropEffects.Copy;

                        if (targetContainer != null)
                        {
                            var targetItem = targetContainer.DataContext as FavoriteItem;
                            var pos = GetDropPosition(targetContainer, e);

                            if (pos == DropPosition.Inside && targetItem != null && targetItem.IsContainer)
                            {
                                targetContainer.IsSelected = true;
                            }
                            else if (pos != DropPosition.Inside)
                            {
                                ShowAdorner(targetContainer, pos == DropPosition.Before);
                            }
                        }
                    }
                    return;
                }
            }
        }

        private static void OnDragLeave(object sender, DragEventArgs e)
        {
            if (sender is TreeView tv && string.Equals(tv.Name, "DirectoryTree", StringComparison.Ordinal))
                _treeLockNotifyShownThisDrag = false;
            ClearAdorner();
        }

        private static bool IsDescendant(FavoriteItem parent, FavoriteItem? potentialChild)
        {
            if (potentialChild == null) return false;
            foreach (var child in parent.Children)
            {
                if (child == potentialChild || IsDescendant(child, potentialChild)) return true;
            }
            return false;
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            ClearAdorner();
            e.Handled = true;
            if (sender is TreeView treeView)
            {
                var vm = treeView.DataContext as MainViewModel;
                if (vm?.Favorites == null) return;

                bool isFavoritesTree = string.Equals(treeView.Name, "FavoritesTree", StringComparison.Ordinal);

                // ツリービューロック時は DirectoryTree 上への全てのドロップ（ツリー内フォルダ移動・一覧からのファイル/フォルダ）を禁止（ナビペインロック仕様）
                bool isDirectoryTree = string.Equals(treeView.Name, "DirectoryTree", StringComparison.Ordinal);
                bool isTreeLockedDrop = isDirectoryTree && vm.IsTreeViewLocked &&
                    (e.Data.GetDataPresent(typeof(DirectoryItemViewModel)) || e.Data.GetDataPresent(DataFormats.FileDrop));
                if (isTreeLockedDrop)
                {
                    App.Notification.Notify("ロック中のため操作不可", "ツリービューロック: ドロップをブロック");
                    vm.TriggerTreeViewLockWarning();
                    return;
                }

                // ツリービュー（DirectoryTree）ロックOFF時: 一覧からのファイル/フォルダドロップで移動
                if (isDirectoryTree && !vm.IsTreeViewLocked && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var dropSource = e.OriginalSource as DependencyObject;
                    var dropTargetContainer = GetContainerFromElement(treeView, dropSource);
                    var targetDir = dropTargetContainer?.DataContext as DirectoryItemViewModel;
                    if (targetDir != null && !string.IsNullOrEmpty(targetDir.FullPath))
                    {
                        var data = e.Data.GetData(DataFormats.FileDrop);
                        string[]? paths = data as string[];
                        if (paths == null && data is System.Collections.Specialized.StringCollection sc)
                        {
                            paths = new string[sc.Count];
                            sc.CopyTo(paths, 0);
                        }
                        if (paths != null && paths.Length > 0)
                        {
                            var pos = GetDropPositionForDirectory(dropTargetContainer!, e);
                            string? destDir = pos == DropPosition.Inside
                                ? targetDir.FullPath
                                : Path.GetDirectoryName(targetDir.FullPath);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                var destPhysical = PathHelper.GetPhysicalPath(destDir);
                                var pathsCopy = (string[])paths.Clone();
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    try
                                    {
                                        if (string.IsNullOrEmpty(destPhysical) || !Directory.Exists(destPhysical)) return;
                                        IntPtr ownerHandle = IntPtr.Zero;
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            if (Application.Current.MainWindow != null)
                                                ownerHandle = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
                                        });
                                        using var op = new VanaraShell.ShellFileOperations();
                                        op.Options |= VanaraShell.ShellFileOperations.OperationFlags.RenameOnCollision;
                                        op.OwnerWindow = ownerHandle;
                                        var movedList = new List<(string Source, string Dest)>();
                                        op.PostMoveItem += (s, ev) =>
                                        {
                                            var sp = ev.SourceItem?.ParsingName;
                                            var dp = ev.DestItem?.ParsingName;
                                            if (ev.Result.Succeeded && sp != null && dp != null)
                                            {
                                                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                                    Services.UndoService.Instance.Register(new Services.Commands.MoveUndoCommand(sp, dp)));
                                                movedList.Add((sp, dp));
                                            }
                                        };
                                        var destNorm = PathHelper.NormalizePathForComparison(destPhysical);
                                        int queuedCount = 0;
                                        foreach (var src in pathsCopy)
                                        {
                                            var srcPhysical = PathHelper.GetPhysicalPath(src);
                                            if (string.IsNullOrEmpty(srcPhysical)) continue;
                                            var srcNorm = PathHelper.NormalizePathForComparison(srcPhysical);
                                            var srcParent = Path.GetDirectoryName(srcPhysical);
                                            var srcParentNorm = string.IsNullOrEmpty(srcParent) ? "" : PathHelper.NormalizePathForComparison(srcParent);
                                            // 同一フォルダへの移動・自分自身の上へのドロップはスキップ（0x80270000 等を防ぐ）
                                            if (srcParentNorm.Equals(destNorm, StringComparison.OrdinalIgnoreCase)) continue;
                                            if (srcNorm.Equals(destNorm, StringComparison.OrdinalIgnoreCase)) continue;
                                            if (Directory.Exists(srcPhysical))
                                            {
                                                using var sourceItem = new VanaraShell.ShellItem(srcPhysical);
                                                using var destFolder = new VanaraShell.ShellFolder(destPhysical);
                                                op.QueueMoveOperation(sourceItem, destFolder);
                                                queuedCount++;
                                            }
                                            else if (System.IO.File.Exists(srcPhysical))
                                            {
                                                using var sourceItem = new VanaraShell.ShellItem(srcPhysical);
                                                using var destFolder = new VanaraShell.ShellFolder(destPhysical);
                                                op.QueueMoveOperation(sourceItem, destFolder);
                                                queuedCount++;
                                            }
                                        }
                                        if (queuedCount == 0) return;
                                        op.PerformOperations();
                                        if (!op.AnyOperationsAborted)
                                        {
                                            _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                            {
                                                foreach (var item in movedList)
                                                {
                                                    if (Directory.Exists(item.Dest))
                                                    {
                                                        var sourceParent = Path.GetDirectoryName(item.Source);
                                                        var targetParent = Path.GetDirectoryName(item.Dest);
                                                        if (!string.IsNullOrEmpty(sourceParent) && !string.IsNullOrEmpty(targetParent))
                                                            Services.FileOperationService.Instance.RaiseFolderMoved(sourceParent, targetParent, item.Source, item.Dest);
                                                    }
                                                }
                                                var count = movedList.Count;
                                                App.Notification.Notify(count == 1 ? "1 件を移動しました" : $"{count} 件を移動しました", "一覧からツリーへ移動");
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            var firstPath = pathsCopy.Length > 0 ? pathsCopy[0] : null;
                                            var friendlyMsg = Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, firstPath);
                                            App.Notification.Notify("移動に失敗しました", $"一覧→ツリー移動失敗: {ex.Message}");
                                            ZenithDialog.Show($"移動に失敗しました。\n{friendlyMsg}", "移動", ZenithDialogButton.OK, ZenithDialogIcon.Error);
                                        });
                                    }
                                });
                            }
                        }
                    }
                    return;
                }

                // ツリービュー（DirectoryTree）ロックOFF時: ツリー内フォルダのドロップで実フォルダを移動
                if (isDirectoryTree && !vm.IsTreeViewLocked && e.Data.GetDataPresent(typeof(DirectoryItemViewModel)))
                {
                    var draggedDir = e.Data.GetData(typeof(DirectoryItemViewModel)) as DirectoryItemViewModel;
                    var dropSource = e.OriginalSource as DependencyObject;
                    var dropTargetContainer = GetContainerFromElement(treeView, dropSource);
                    var targetDir = dropTargetContainer?.DataContext as DirectoryItemViewModel;
                    if (draggedDir != null && targetDir != null && !string.IsNullOrEmpty(draggedDir.FullPath) && !string.IsNullOrEmpty(targetDir.FullPath))
                    {
                        var pos = GetDropPositionForDirectory(dropTargetContainer!, e);
                        string? destPath = null;
                        if (pos == DropPosition.Inside)
                        {
                            destPath = Path.Combine(targetDir.FullPath, Path.GetFileName(draggedDir.FullPath));
                        }
                        else
                        {
                            var targetParent = Path.GetDirectoryName(targetDir.FullPath);
                            if (!string.IsNullOrEmpty(targetParent))
                                destPath = Path.Combine(targetParent, Path.GetFileName(draggedDir.FullPath));
                        }
                        if (!string.IsNullOrEmpty(destPath))
                        {
                            var srcNorm = PathHelper.NormalizePathForComparison(draggedDir.FullPath);
                            var destNorm = PathHelper.NormalizePathForComparison(destPath);
                            if (!srcNorm.Equals(destNorm, StringComparison.OrdinalIgnoreCase) && !IsPathUnder(destNorm, srcNorm))
                            {
                                var srcPhysical = PathHelper.GetPhysicalPath(draggedDir.FullPath);
                                var destPhysical = PathHelper.GetPhysicalPath(destPath);
                                var draggedName = Path.GetFileName(draggedDir.FullPath);
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    try
                                    {
                                        if (string.IsNullOrEmpty(srcPhysical) || !Directory.Exists(srcPhysical) || string.IsNullOrEmpty(destPhysical) || srcPhysical.Equals(destPhysical, StringComparison.OrdinalIgnoreCase))
                                            return;
                                        IntPtr ownerHandle = IntPtr.Zero;
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            if (Application.Current.MainWindow != null)
                                                ownerHandle = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
                                        });
                                        using var op = new VanaraShell.ShellFileOperations();
                                        op.Options |= VanaraShell.ShellFileOperations.OperationFlags.RenameOnCollision;
                                        op.OwnerWindow = ownerHandle;
                                        var movedList = new List<(string Source, string Dest)>();
                                        op.PostMoveItem += (s, ev) =>
                                        {
                                            var sp = ev.SourceItem?.ParsingName;
                                            var dp = ev.DestItem?.ParsingName;
                                            if (ev.Result.Succeeded && sp != null && dp != null)
                                            {
                                                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                                    Services.UndoService.Instance.Register(new Services.Commands.MoveUndoCommand(sp, dp)));
                                                movedList.Add((sp, dp));
                                            }
                                        };
                                        using var sourceItem = new VanaraShell.ShellItem(srcPhysical);
                                        using var destFolder = new VanaraShell.ShellFolder(Path.GetDirectoryName(destPhysical) ?? destPhysical);
                                        op.QueueMoveOperation(sourceItem, destFolder);
                                        op.PerformOperations();
                                        if (!op.AnyOperationsAborted)
                                        {
                                            _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                            {
                                                foreach (var item in movedList)
                                                {
                                                    if (Directory.Exists(item.Dest))
                                                    {
                                                        var sourceParent = Path.GetDirectoryName(item.Source);
                                                        var targetParent = Path.GetDirectoryName(item.Dest);
                                                        if (!string.IsNullOrEmpty(sourceParent) && !string.IsNullOrEmpty(targetParent))
                                                            Services.FileOperationService.Instance.RaiseFolderMoved(sourceParent, targetParent, item.Source, item.Dest);
                                                    }
                                                }
                                                App.Notification.Notify("フォルダを移動しました", $"ツリーから移動: {draggedName}");
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            var friendlyMsg = Helpers.RestartManagerHelper.GetFriendlyErrorMessage(ex, srcPhysical ?? "");
                                            App.Notification.Notify("フォルダの移動に失敗しました", $"ツリーD&D移動失敗: {ex.Message}");
                                            ZenithDialog.Show($"フォルダの移動に失敗しました。\n{friendlyMsg}", "移動", ZenithDialogButton.OK, ZenithDialogIcon.Error);
                                        });
                                    }
                                });
                            }
                        }
                    }
                    return;
                }

                var source = e.OriginalSource as DependencyObject;
                var targetContainer = GetContainerFromElement(treeView, source);
                var targetItem = targetContainer?.DataContext as FavoriteItem;

                // お気に入り項目の並べ替え・移動を優先（内部移動）。ロック時はブロック（登録は後続の FileDrop で許可）。
                if (e.Data.GetDataPresent(typeof(FavoriteItem)))
                {
                    var draggedItem = e.Data.GetData(typeof(FavoriteItem)) as FavoriteItem;
                    if (draggedItem == null) return;

                    if (isFavoritesTree && vm.Favorites.IsLocked)
                    {
                        App.Notification.Notify("ロック中のため移動できませんでした", "お気に入りロック: 並べ替えをブロック");
                        _ = vm.Favorites.TriggerLockWarningAsync();
                        return;
                    }

                    // 【スナップショット取得】削除前に記録（ロールバック用）
                    var snapshot = vm.Favorites.TakeSnapshot();
                    string sourceParentName = FindParentName(vm.Favorites.Items, draggedItem) ?? "(root)";
                    string targetParentName = targetItem != null ? targetItem.Name : "(root)";
                    _ = App.FileLogger.LogAsync(
                        $"[Favorites] Move Started: '{draggedItem.Name}' from '{sourceParentName}' to '{targetParentName}'");

                    try
                    {
                        // 削除
                        bool removed = RemoveFromTree(vm.Favorites.Items, draggedItem);
                        if (!removed) return;

                        // 挿入
                        if (targetContainer != null && targetItem != null)
                        {
                            var pos = GetDropPosition(targetContainer, e);
                            var parentCollection = GetParentCollection(vm.Favorites.Items, targetItem)
                                                   ?? vm.Favorites.Items;
                            int index = parentCollection.IndexOf(targetItem);

                            if (pos == DropPosition.Before)
                                parentCollection.Insert(index, draggedItem);
                            else if (pos == DropPosition.After || !targetItem.IsContainer)
                                parentCollection.Insert(index + 1, draggedItem);
                            else if (pos == DropPosition.Inside && targetItem.IsContainer)
                            {
                                targetItem.Children.Add(draggedItem);
                                targetItem.IsExpanded = true;
                            }
                            else
                                vm.Favorites.Items.Add(draggedItem);
                        }
                        else
                        {
                            vm.Favorites.Items.Add(draggedItem);
                        }

                        // 【保存確認】失敗した場合はロールバック
                        if (!vm.Favorites.TrySaveFavorites())
                        {
                            vm.Favorites.RestoreFromSnapshot(snapshot);
                            App.Notification.Notify(
                                "移動を保存できませんでした。元の状態に戻しました",
                                "[Favorites] ERROR: Move failed. Rollback executed. Detail: save returned false");
                            _ = App.FileLogger.LogAsync(
                                "[Favorites] ERROR: Move failed. Rollback executed. Detail: save returned false");
                            return;
                        }

                        _ = App.FileLogger.LogAsync(
                            "[Favorites] Move Completed: Successfully updated settings.json");
                    }
                    catch (Exception ex)
                    {
                        // 【例外ロールバック】
                        vm.Favorites.RestoreFromSnapshot(snapshot);
                        App.Notification.Notify(
                            "移動中にエラーが発生しました。元の状態に戻しました",
                            $"[Favorites] ERROR: Move failed. Rollback executed. Detail: {ex.Message}");
                        _ = App.FileLogger.LogAsync(
                            $"[Favorites] ERROR: Move failed. Rollback executed. Detail: {ex.Message}");
                    }
                    return;
                }

                // お気に入りビュー（FavoritesTree）のみ: ファイル/フォルダドロップでお気に入りに登録
                if (isFavoritesTree && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var data = e.Data.GetData(DataFormats.FileDrop);
                    string[]? files = data as string[];
                    if (files == null && data is System.Collections.Specialized.StringCollection sc)
                    {
                        files = new string[sc.Count];
                        sc.CopyTo(files, 0);
                    }

                    if (files != null)
                    {
                        bool added = false;
                        int insertIndex = -1;
                        System.Collections.ObjectModel.ObservableCollection<FavoriteItem>? targetCollection = null;
                        DropPosition pos = DropPosition.Inside;

                        if (targetContainer != null && targetItem != null)
                        {
                            pos = GetDropPosition(targetContainer, e);
                            targetCollection = GetParentCollection(vm.Favorites.Items, targetItem) ?? vm.Favorites.Items;
                            insertIndex = targetCollection.IndexOf(targetItem);
                        }

                        foreach (var file in files)
                        {
                            // フォルダまたはファイルを対象とする
                            if (!System.IO.Directory.Exists(file) && !System.IO.File.Exists(file)) continue;

                            // 重複チェック — FavoritesViewModel.NotifyDuplicate に委譲
                            if (vm.Favorites.ContainsPath(file))
                            {
                                vm.Favorites.NotifyDuplicate(file);
                                continue;
                            }

                            var name = System.IO.Path.GetFileName(file);
                            if (string.IsNullOrEmpty(name)) name = file;

                            var isDir = System.IO.Directory.Exists(file);
                            var newItem = new FavoriteItem
                            {
                                Name = name,
                                Path = file,
                                SourceType = PathHelper.DetermineSourceType(file),
                                Icon = ShellIconHelper.GetGenericInfo(file, isDir).Icon
                            };

                            if (targetCollection != null)
                            {
                                if (pos == DropPosition.Before)
                                {
                                    targetCollection.Insert(insertIndex++, newItem);
                                }
                                else if (pos == DropPosition.After || !targetItem!.IsContainer)
                                {
                                    targetCollection.Insert(++insertIndex, newItem);
                                }
                                else if (pos == DropPosition.Inside && targetItem!.IsContainer)
                                {
                                    targetItem.Children.Add(newItem);
                                    targetItem.IsExpanded = true;
                                }
                                else
                                {
                                    vm.Favorites.Items.Add(newItem);
                                }
                            }
                            else
                            {
                                vm.Favorites.Items.Add(newItem);
                            }
                            added = true;
                        }

                        if (added)
                        {
                            vm.Favorites.SaveFavorites();
                        }
                    }
                    return;
                }
            }
        }
        
        private static bool RemoveFromTree(System.Collections.ObjectModel.ObservableCollection<FavoriteItem> items, FavoriteItem target)
        {
            if (items.Remove(target)) return true;
            foreach (var item in items)
            {
                if (RemoveFromTree(item.Children, target)) return true;
            }
            return false;
        }

        private static TreeViewItem? GetContainerFromElement(TreeView treeView, DependencyObject? element)
        {
            if (element == null) return null;
            
            DependencyObject? current = element;
            while (current != null && current != treeView)
            {
                if (current is TreeViewItem item) return item;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static DropPosition GetDropPosition(TreeViewItem container, DragEventArgs e)
        {
            var pos = e.GetPosition(container);
            double height = container.ActualHeight;
            var targetItem = container.DataContext as FavoriteItem;

            if (container.IsExpanded && container.Items.Count > 0)
            {
                // Expanded の場合はヘッダー部分（通常一行分）のみで判定
                height = 25.0;
            }

            // 整理用フォルダ（IsContainer == true）のみ中へのドロップを許可する
            bool allowInside = targetItem != null && targetItem.IsContainer;

            if (allowInside)
            {
                if (pos.Y < height * 0.25) return DropPosition.Before;
                if (pos.Y > height * 0.75) return DropPosition.After;
                return DropPosition.Inside;
            }
            else
            {
                // 中へのドロップを許可しない場合は、上下半分で判定
                return pos.Y < height / 2.0 ? DropPosition.Before : DropPosition.After;
            }
        }

        /// <summary>DirectoryTree 用。フォルダは常に「中へのドロップ」を許可する。</summary>
        private static DropPosition GetDropPositionForDirectory(TreeViewItem container, DragEventArgs e)
        {
            var pos = e.GetPosition(container);
            double height = container.ActualHeight;
            if (container.IsExpanded && container.Items.Count > 0)
                height = 25.0;
            if (pos.Y < height * 0.25) return DropPosition.Before;
            if (pos.Y > height * 0.75) return DropPosition.After;
            return DropPosition.Inside;
        }

        /// <summary>childPath が parentPath の配下（同一または子孫）かどうか。</summary>
        private static bool IsPathUnder(string childPath, string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(childPath)) return false;
            var parent = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var child = childPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (child.Length <= parent.Length) return false;
            if (!child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) return false;
            return child[parent.Length] == Path.DirectorySeparatorChar || child[parent.Length] == Path.AltDirectorySeparatorChar;
        }

        private static System.Collections.ObjectModel.ObservableCollection<FavoriteItem>? GetParentCollection(System.Collections.ObjectModel.ObservableCollection<FavoriteItem> root, FavoriteItem target)
        {
            if (root.Contains(target)) return root;
            foreach (var item in root)
            {
                var found = GetParentCollection(item.Children, target);
                if (found != null) return found;
            }
            return null;
        }

        private static string? FindParentName(
            System.Collections.ObjectModel.ObservableCollection<FavoriteItem> collection,
            FavoriteItem target)
        {
            foreach (var item in collection)
            {
                if (item.Children.Contains(target)) return item.Name;
                var found = FindParentName(item.Children, target);
                if (found != null) return found;
            }
            return null;
        }

        private static void ShowAdorner(TreeViewItem target, bool isBefore)
        {
            if (_insertionAdorner != null)
            {
                if (_insertionAdorner.AdornedElement == target && _insertionAdorner.IsBefore == isBefore)
                    return;
                ClearAdorner();
            }

            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer != null)
            {
                _insertionAdorner = new InsertionAdorner(target, isBefore);
                layer.Add(_insertionAdorner);
            }
        }

        private static void ClearAdorner()
        {
            if (_insertionAdorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(_insertionAdorner.AdornedElement);
                layer?.Remove(_insertionAdorner);
                _insertionAdorner = null;
            }
        }
    }

    public class InsertionAdorner : Adorner
    {
        public bool IsBefore { get; }

        public InsertionAdorner(UIElement adornedElement, bool isBefore) : base(adornedElement)
        {
            IsBefore = isBefore;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var brush = Application.Current.Resources["AccentBrush"] as Brush ?? Brushes.DodgerBlue;
            var pen = new Pen(brush, 2);
            pen.Freeze();

            double y = IsBefore ? 0 : AdornedElement.RenderSize.Height;
            
            // TreeViewItemのヘッダー部分だけを考慮するため、展開されている場合は高さを調整
            if (AdornedElement is TreeViewItem tvi && tvi.IsExpanded && tvi.Items.Count > 0)
            {
                if (!IsBefore) y = 25.0; // ヘッダーの概算高さ
            }

            var startPoint = new Point(0, y);
            var endPoint = new Point(AdornedElement.RenderSize.Width, y);

            drawingContext.DrawLine(pen, startPoint, endPoint);
            drawingContext.DrawEllipse(brush, pen, startPoint, 3, 3);
            drawingContext.DrawEllipse(brush, pen, endPoint, 3, 3);
        }
    }
}
