using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ZenithFiler.Services;

namespace ZenithFiler
{
    public partial class DirectoryTreeViewModel : ObservableObject
    {
        private readonly Action<string>? _navigateAction;
        /// <summary>プログラム的に展開・選択している間は true。この間はツリー選択によるナビゲーションを発火させない。</summary>
        private bool _isExpandInProgress;

        // スロットリング用: 短時間の重複イベントを無視する
        private readonly ConcurrentDictionary<string, DateTime> _recentEvents = new();
        private const int EventThrottleMs = 500;

        private bool IsRecentEvent(string key)
        {
            var now = DateTime.Now;
            // 簡易的なクリーンアップ: エントリ数が多すぎるときだけ全クリア
            if (_recentEvents.Count > 1000) _recentEvents.Clear();

            if (_recentEvents.TryGetValue(key, out var time))
            {
                if ((now - time).TotalMilliseconds < EventThrottleMs)
                    return true;
            }
            _recentEvents[key] = now;
            return false;
        }

        public ObservableCollection<DirectoryItemViewModel> Drives { get; } = new();

        public DirectoryTreeViewModel(Action<string>? navigateAction = null)
        {
            _navigateAction = navigateAction;
            FileOperationService.Instance.FolderCreated += OnFolderCreated;
            FileOperationService.Instance.FolderRenamed += OnFolderRenamed;
            FileOperationService.Instance.FolderMoved += OnFolderMoved;
            FileOperationService.Instance.FolderDeleted += OnFolderDeleted;
        }

        /// <summary>ファイル操作サービスからのイベント購読を解除する。VM の破棄時に呼ぶ。</summary>
        public void UnsubscribeFromFileOperations()
        {
            FileOperationService.Instance.FolderCreated -= OnFolderCreated;
            FileOperationService.Instance.FolderRenamed -= OnFolderRenamed;
            FileOperationService.Instance.FolderMoved -= OnFolderMoved;
            FileOperationService.Instance.FolderDeleted -= OnFolderDeleted;
        }

        private void OnFolderCreated(string parentPath, string newFolderPath)
        {
            if (IsRecentEvent($"Create:{newFolderPath}")) return;
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                SyncFolderCreated(parentPath, newFolderPath), DispatcherPriority.Normal);
        }

        private void OnFolderRenamed(string oldPath, string newPath)
        {
            if (IsRecentEvent($"Rename:{newPath}")) return;
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                SyncFolderRenamed(oldPath, newPath), DispatcherPriority.Normal);
        }

        private void OnFolderMoved(string sourceParentPath, string targetParentPath, string oldFolderPath, string movedFolderPath)
        {
            if (IsRecentEvent($"Move:{movedFolderPath}")) return;
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                SyncFolderMoved(sourceParentPath, targetParentPath, oldFolderPath, movedFolderPath), DispatcherPriority.Normal);
        }

        private void OnFolderDeleted(string parentPath, string deletedFolderPath)
        {
            if (IsRecentEvent($"Delete:{deletedFolderPath}")) return;
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                SyncFolderDeleted(parentPath, deletedFolderPath), DispatcherPriority.Normal);
        }

        private void SyncFolderCreated(string parentPath, string newFolderPath)
        {
            var parent = FindNodeByPath(Drives, PathHelper.NormalizePathForComparison(parentPath));
            if (parent == null) return;
            
            if (parent.IsChildrenLoaded)
            {
                parent.TryAddChildFolder(newFolderPath);
            }
            else
            {
                // 未ロードなら IsLoaded = false に設定して次回読み込みを促す
                parent.MarkAsDirty();
            }
        }

        private void SyncFolderRenamed(string oldPath, string newPath)
        {
            var parentPath = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(parentPath)) return;

            var parent = FindNodeByPath(Drives, PathHelper.NormalizePathForComparison(parentPath));
            if (parent == null || !parent.IsChildrenLoaded) return;

            parent.RenameChild(oldPath, Path.GetFileName(newPath), newPath);
        }

        private void SyncFolderMoved(string sourceParentPath, string targetParentPath, string oldFolderPath, string movedFolderPath)
        {
            var normalizedSource = PathHelper.NormalizePathForComparison(sourceParentPath);
            var normalizedTarget = PathHelper.NormalizePathForComparison(targetParentPath);
            var normalizedOld = PathHelper.NormalizePathForComparison(oldFolderPath);
            
            // 同一フォルダ内移動（リネーム扱いすべきだが念のため）なら何もしないか、Renameとして扱うべきだが
            // FolderMovedイベントは親が変わることを想定
            if (normalizedSource.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)) return;

            var sourceParent = FindNodeByPath(Drives, normalizedSource);
            var targetParent = FindNodeByPath(Drives, normalizedTarget);

            // 移動元からの削除
            if (sourceParent != null && sourceParent.IsChildrenLoaded)
            {
                var childNode = sourceParent.Children.FirstOrDefault(c => 
                    PathHelper.NormalizePathForComparison(c.FullPath).Equals(normalizedOld, StringComparison.OrdinalIgnoreCase));
                
                if (childNode != null)
                {
                    sourceParent.Children.Remove(childNode);
                    
                    // 移動先にロード済みなら追加（インスタンス再利用）
                    if (targetParent != null && targetParent.IsChildrenLoaded)
                    {
                        // パスと名前を更新
                        var newName = Path.GetFileName(movedFolderPath);
                        childNode.UpdateNameAndPath(newName, movedFolderPath);
                        targetParent.TryAddExistingChildFolder(childNode);
                        return;
                    }
                }
            }

            // 移動先への追加（再利用できなかった場合や、移動元が見えなかった場合用）
            if (targetParent != null)
            {
                if (targetParent.IsChildrenLoaded)
                {
                    targetParent.TryAddChildFolder(movedFolderPath);
                }
                else
                {
                    targetParent.MarkAsDirty();
                }
            }
        }

        private void SyncFolderDeleted(string parentPath, string deletedFolderPath)
        {
            RemoveChildFolderByPath(parentPath, deletedFolderPath);
        }

        /// <summary>ツリー選択時に呼ばれる。プログラム的展開中はナビゲーションしない。</summary>
        internal void OnTreeSelectionRequestNavigate(string path)
        {
            if (_isExpandInProgress) return;
            _navigateAction?.Invoke(path);
        }

        public async Task ExpandToPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            path = PathHelper.GetPhysicalPath(path);
            if (string.IsNullOrEmpty(path)) return;

            _isExpandInProgress = true;
            try
            {
                // ドライブ未読み込みなら先に非同期で読み込み
                if (Drives.Count == 0)
                {
                    await LoadDrivesAsync();
                }

                var segments = new List<string>();
                try
                {
                    var di = new DirectoryInfo(path);
                    while (di != null)
                    {
                        segments.Insert(0, PathHelper.NormalizePathForComparison(di.FullName));
                        di = di.Parent;
                    }
                }
                catch { return; }

                if (segments.Count == 0) return;

                DirectoryItemViewModel? current = null;
                var currentChildren = Drives;
                int lastIndex = segments.Count - 1;

                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    // コレクション参照は UI スレッドで行う（WPF のバインディング整合性のため）
                    var match = await Application.Current.Dispatcher.InvokeAsync(() =>
                        currentChildren.FirstOrDefault(x =>
                            PathHelper.NormalizePathForComparison(x.FullPath).Equals(segment, StringComparison.OrdinalIgnoreCase)),
                        DispatcherPriority.Normal);

                    if (match == null) break;

                    current = match;

                    if (i < lastIndex)
                    {
                        // 先に子を読み込んでから IsExpanded を true にする（逆だと OnIsExpandedChanged の
                        // fire-and-forget で _isLoading が立ち、直後の await が即 return して待てない）
                        await match.EnsureChildrenLoadedAsync();
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            match.IsExpanded = true;
                        }, DispatcherPriority.Normal);
                        currentChildren = await Application.Current.Dispatcher.InvokeAsync(() => match.Children, DispatcherPriority.Normal);
                    }
                }

                if (current != null)
                {
                    // カレントフォルダ自身も必ず展開する（ツリー上で矢印が開いた状態で表示）
                    await current.EnsureChildrenLoadedAsync();
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        current.IsExpanded = true;
                        current.IsSelected = true;
                    }, DispatcherPriority.Normal);
                }
            }
            finally
            {
                _isExpandInProgress = false;
            }
        }

        /// <summary>指定フォルダの子一覧をツリー上で再読み込みする。一覧でフォルダを移動・コピーした後に呼び出してツリーを連動させる。</summary>
        /// <param name="folderPath">子を更新したい親フォルダのフルパス（ドライブルートまたはサブフォルダ）。</param>
        public void RefreshChildrenOfFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            folderPath = PathHelper.GetPhysicalPath(folderPath);
            if (string.IsNullOrEmpty(folderPath)) return;

            var normalized = PathHelper.NormalizePathForComparison(folderPath);
            DirectoryItemViewModel? node = FindNodeByPath(Drives, normalized);
            if (node == null) return;

            node.MarkAsDirty();
            _ = node.EnsureChildrenLoadedAsync();
        }

        /// <summary>指定した親フォルダの子のうち、指定パスのノードだけをツリーから削除する。移動時に移動元の子を全クリアせず該当ノードのみ外すことで、兄弟ノード（移動先など）をツリーに残す。</summary>
        /// <param name="parentPath">親フォルダのフルパス。</param>
        /// <param name="childFullPath">削除する子フォルダのフルパス。</param>
        /// <returns>削除した場合 true。</returns>
        public bool RemoveChildFolderByPath(string parentPath, string childFullPath)
        {
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(childFullPath)) return false;
            parentPath = PathHelper.GetPhysicalPath(parentPath);
            childFullPath = PathHelper.GetPhysicalPath(childFullPath);
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(childFullPath)) return false;

            var normalizedParent = PathHelper.NormalizePathForComparison(parentPath);
            var normalizedChild = PathHelper.NormalizePathForComparison(childFullPath);
            DirectoryItemViewModel? parentNode = FindNodeByPath(Drives, normalizedParent);
            if (parentNode == null) return false;

            var toRemove = parentNode.Children.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.FullPath) &&
                PathHelper.NormalizePathForComparison(c.FullPath).Equals(normalizedChild, StringComparison.OrdinalIgnoreCase));
            if (toRemove == null) return false;

            parentNode.Children.Remove(toRemove);
            return true;
        }

        /// <summary>ツリー内で指定パスに一致するノードを再帰的に検索する。パスは正規化済みで渡すこと。</summary>
        private static DirectoryItemViewModel? FindNodeByPath(IEnumerable<DirectoryItemViewModel> collection, string normalizedTargetPath)
        {
            if (string.IsNullOrWhiteSpace(normalizedTargetPath)) return null;
            foreach (var item in collection)
            {
                if (string.IsNullOrEmpty(item.FullPath)) continue;
                var normalizedItem = PathHelper.NormalizePathForComparison(item.FullPath);
                if (string.IsNullOrEmpty(normalizedItem)) continue;
                if (normalizedItem.Equals(normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
                    return item;
                var prefix = normalizedItem.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (normalizedTargetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var found = FindNodeByPath(item.Children, normalizedTargetPath);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private MainViewModel? MainVM => Application.Current?.MainWindow?.DataContext as MainViewModel;

        /// <summary>
        /// ドライブ一覧を差分更新する。増えたドライブのみ Add、消えたドライブのみ Remove。
        /// DriveInfo の取得はバックグラウンドで行い、ネットワークドライブの無応答による UI フリーズを回避する。
        /// </summary>
        public async Task RefreshDrivesAsync()
        {
            var busyToken = MainVM?.BeginBusy();
            try
            {
                var currentDrives = await Task.Run(() =>
                {
                    var result = new List<(string Path, string Label)>();
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        try
                        {
                            if (!drive.IsReady) continue;
                            var label = drive.DriveType == DriveType.Network ? "" : drive.VolumeLabel;
                            var path = PathHelper.NormalizePathForComparison(drive.RootDirectory.FullName);
                            result.Add((path, label ?? ""));
                        }
                        catch { }
                    }
                    return result;
                });

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var newPaths = new HashSet<string>(currentDrives.Select(d => d.Path), StringComparer.OrdinalIgnoreCase);
                    var existingPaths = new HashSet<string>(
                        Drives.Where(d => !string.IsNullOrEmpty(d.FullPath))
                              .Select(d => PathHelper.NormalizePathForComparison(d.FullPath)),
                        StringComparer.OrdinalIgnoreCase);

                    // 消えたドライブを Remove
                    for (int i = Drives.Count - 1; i >= 0; i--)
                    {
                        var drivePath = PathHelper.NormalizePathForComparison(Drives[i].FullPath);
                        if (!newPaths.Contains(drivePath))
                            Drives.RemoveAt(i);
                    }

                    // 増えたドライブを Add
                    foreach (var (path, label) in currentDrives)
                    {
                        if (!existingPaths.Contains(path))
                            Drives.Add(new DirectoryItemViewModel(path, OnTreeSelectionRequestNavigate));
                    }
                });
            }
            finally
            {
                busyToken?.Dispose();
            }
        }

        /// <summary>ルート（ドライブ）一覧を再構築する。正規化パスで重複を防ぐ。</summary>
        public void LoadDrives()
        {
            Drives.Clear();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                try
                {
                    var path = PathHelper.NormalizePathForComparison(drive.RootDirectory.FullName);
                    if (seenPaths.Add(path))
                        Drives.Add(new DirectoryItemViewModel(drive.RootDirectory.FullName, OnTreeSelectionRequestNavigate));
                }
                catch { }
            }
        }

        /// <summary>
        /// ルート（ドライブ）一覧を非同期で再構築する。
        /// 各ドライブの IsReady チェックを 500ms タイムアウト付きで行い、
        /// 切断ネットワークドライブによる UI フリーズを防止する。
        /// </summary>
        public async Task LoadDrivesAsync()
        {
            var readyDrives = await Task.Run(() =>
            {
                var result = new List<string>();
                try
                {
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        try
                        {
                            var isReadyTask = Task.Run(() =>
                            {
                                try { return drive.IsReady; }
                                catch { return false; }
                            });
                            if (isReadyTask.Wait(500) && isReadyTask.Result)
                                result.Add(drive.RootDirectory.FullName);
                        }
                        catch { }
                    }
                }
                catch { }
                return result;
            });

            // await 後は UI コンテキストに復帰するため ObservableCollection の操作は安全
            Drives.Clear();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in readyDrives)
            {
                var normalized = PathHelper.NormalizePathForComparison(path);
                if (seenPaths.Add(normalized))
                    Drives.Add(new DirectoryItemViewModel(path, OnTreeSelectionRequestNavigate));
            }
        }
    }
}
