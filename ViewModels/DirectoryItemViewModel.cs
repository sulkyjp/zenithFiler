using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZenithFiler
{
    public partial class DirectoryItemViewModel : ObservableObject
    {
        private readonly Action<string>? _onNavigationRequested;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private ImageSource? _icon;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        public ObservableCollection<DirectoryItemViewModel> Children { get; } = new();

        private bool _hasDummyChild;
        /// <summary>一度子要素の読み込みが完了したか。true の間は明示的な更新がない限り再読み込みしない。</summary>
        private bool _isLoaded;
        /// <summary>子要素の読み込み中か。多重読み込み（レースコンディション）を防ぐ。</summary>
        private bool _isLoading;

        public DirectoryItemViewModel(string path, Action<string>? onNavigationRequested)
        {
            _fullPath = path;
            _onNavigationRequested = onNavigationRequested;
            
            // ルートドライブと通常のフォルダで名前の取得方法を変える
            var info = new DirectoryInfo(path);
            _name = info.Parent == null ? info.Name.TrimEnd(Path.DirectorySeparatorChar) : info.Name;
            if (string.IsNullOrEmpty(_name)) _name = path; // フォールバック

            // 初期化時には軽量なアイコンのみ設定し、詳細はバックグラウンドで取得
            _icon = null; 

            // サブフォルダがあるか確認してダミーを追加（展開可能アイコンを表示するため）
            // コンストラクタでの I/O を最小限にするため、ここでは常にダミーを追加し、展開時に実在を確認する方式に変更
            Children.Add(new DirectoryItemViewModel()); // ダミー
            _hasDummyChild = true;

            // アイコン等の重い情報をバックグラウンドで取得
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Box領域の場合はアイコン取得前に暖気運転を行い、スタブ生成を促す
                if (PathHelper.IsInsideBoxDrive(FullPath))
                {
                    await Task.Run(() => PathHelper.WarmUpBoxPath(FullPath));
                }

                // アイコン取得をバックグラウンドへ
                // GetFolderIcon を使うことで、場所に応じた適切なフォルダーアイコンが取得される
                var info = await Task.Run(() => ShellIconHelper.GetFolderIcon(FullPath));
                await Application.Current.Dispatcher.InvokeAsync(() => Icon = info.Icon, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch { }
        }

        // ダミー用コンストラクタ
        private DirectoryItemViewModel()
        {
        }

        partial void OnIsExpandedChanged(bool value)
        {
            if (value && _hasDummyChild)
            {
                _ = EnsureChildrenLoadedAsync();
            }
        }

        /// <summary>子要素が既にディスクから読み込み済みか。未展開の場合は false。</summary>
        internal bool IsChildrenLoaded => _isLoaded;

        /// <summary>指定パスの子フォルダを Children に追加する。親が未読み込みの場合は何もしない。</summary>
        /// <returns>追加した場合 true。未読み込みで何もしなかった場合 false。</returns>
        internal bool TryAddChildFolder(string childFullPath)
        {
            if (!_isLoaded) return false;
            var normalized = PathHelper.NormalizePathForComparison(childFullPath);
            if (Children.Any(c => !string.IsNullOrEmpty(c.FullPath) &&
                PathHelper.NormalizePathForComparison(c.FullPath).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                return true; // 既に存在する場合は成功扱い
            var newChild = new DirectoryItemViewModel(childFullPath, _onNavigationRequested);
            InsertChildInSortedOrder(newChild);
            return true;
        }

        /// <summary>既存の子ノード（移動などで他から外れたノード）を Children に挿入する。</summary>
        /// <returns>追加した場合 true。未読み込みで何もしなかった場合 false。</returns>
        internal bool TryAddExistingChildFolder(DirectoryItemViewModel node)
        {
            if (!_isLoaded) return false;
            var normalized = PathHelper.NormalizePathForComparison(node.FullPath);
            if (Children.Any(c => !string.IsNullOrEmpty(c.FullPath) &&
                PathHelper.NormalizePathForComparison(c.FullPath).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                return true;
            InsertChildInSortedOrder(node);
            return true;
        }

        private void InsertChildInSortedOrder(DirectoryItemViewModel child)
        {
            string childName = Path.GetFileName(child.FullPath);
            if (string.IsNullOrEmpty(childName)) childName = child.Name;
            int idx = 0;
            foreach (var c in Children)
            {
                if (string.IsNullOrEmpty(c.FullPath)) continue;
                if (string.Compare(childName, c.Name, StringComparison.OrdinalIgnoreCase) < 0)
                    break;
                idx++;
            }
            Children.Insert(idx, child);
        }

        internal void UpdateNameAndPath(string newName, string newPath)
        {
            Name = newName;
            FullPath = newPath;
        }

        /// <summary>
        /// 子要素の状態を「未読み込み」マークする。
        /// 次回展開時にディスクから再読み込みが行われるようにする。
        /// </summary>
        internal void MarkAsDirty()
        {
            _isLoaded = false;
            // 既に読み込み済みで子が0件だった場合、展開可能アイコンが出ないためダミーを追加して展開可能にする
            if (Children.Count == 0 && !_hasDummyChild)
            {
                _hasDummyChild = true;
                Children.Add(new DirectoryItemViewModel());
            }
        }

        /// <summary>
        /// 子ノードをリネームし、必要ならソート順序を維持するために並べ替える。
        /// </summary>
        internal void RenameChild(string oldFullPath, string newName, string newFullPath)
        {
            var normalizedOld = PathHelper.NormalizePathForComparison(oldFullPath);
            var targetNode = Children.FirstOrDefault(c => 
                !string.IsNullOrEmpty(c.FullPath) && 
                PathHelper.NormalizePathForComparison(c.FullPath).Equals(normalizedOld, StringComparison.OrdinalIgnoreCase));

            if (targetNode != null)
            {
                targetNode.UpdateNameAndPath(newName, newFullPath);
                
                // ソート順序を維持するために一度削除して再挿入
                // (名前が変わって順序が変わる可能性があるため)
                Children.Remove(targetNode);
                InsertChildInSortedOrder(targetNode);
            }
        }

        public async Task EnsureChildrenLoadedAsync()
        {
            // 既に読み込み済みのノードは再読み込みしない（明示的更新時を除く）
            if (_isLoaded) return;
            // 多重読み込みの防止（展開の連打やバインディングの二重発火で同じ処理が走らないようにする）
            if (_isLoading) return;
            // ダミー以外で既に子がいる場合は読み込み済みとみなす（後方互換）
            if (!_hasDummyChild && Children.Count > 0) return;

            _isLoading = true;
            try
            {
                _hasDummyChild = false;
                Children.Clear();

                await Task.Run(async () =>
                {
                    try
                    {
                        // タイムアウト付きでフォルダ一覧を取得（ネットワークドライブ等のハング対策）
                        var directories = await Task.Run(() =>
                        {
                            try
                            {
                                var options = new EnumerationOptions
                                {
                                    IgnoreInaccessible = true,
                                    AttributesToSkip = FileAttributes.System | FileAttributes.Temporary
                                };
                                return Directory.EnumerateDirectories(FullPath, "*", options).ToArray();
                            }
                            catch { return Array.Empty<string>(); }
                        });

                        // フルパスで一意化（同一パスが返る可能性に備える）
                        var uniquePaths = directories
                            .Select(p => p.TrimEnd(Path.DirectorySeparatorChar))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        Array.Sort(uniquePaths, (a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));

                        // UIスレッドへの反映をバッチ化して負荷を軽減
                        const int batchSize = 20;
                        for (int i = 0; i < uniquePaths.Length; i += batchSize)
                        {
                            var currentBatch = uniquePaths.Skip(i).Take(batchSize).ToList();
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                foreach (var d in currentBatch)
                                {
                                    var normalized = PathHelper.NormalizePathForComparison(d);
                                    if (Children.Any(c => !string.IsNullOrEmpty(c.FullPath) &&
                                        PathHelper.NormalizePathForComparison(c.FullPath).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                                        continue;
                                    Children.Add(new DirectoryItemViewModel(d, _onNavigationRequested));
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);

                            await Task.Delay(10);
                        }

                        if (uniquePaths.Length == 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => Children.Clear());
                        }

                        await Application.Current.Dispatcher.InvokeAsync(() => _isLoaded = true, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch { }
                });
            }
            finally
            {
                _isLoading = false;
            }
        }

        partial void OnIsSelectedChanged(bool value)
        {
            // ワンクリックでは選択のみ。ナビゲーションはダブルクリック・Enter・コンテキストメニューで行う。
        }
    }
}
