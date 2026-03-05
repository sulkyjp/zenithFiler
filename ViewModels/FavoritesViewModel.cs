using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using System.ComponentModel;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using Vanara.Windows.Shell;
using ZenithFiler.Views;

namespace ZenithFiler
{
    public partial class FavoritesViewModel : ObservableObject
    {
        public ObservableCollection<FavoriteItem> Items { get; } = new();

        /// <summary>お気に入りビュー内の変更を禁止するロック。ナビペインロック仕様（ステータスバー通知＋南京錠アニメーション）に従う。登録・名前変更・概要変更はロック時も許可。</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
        private bool _isLocked;

        [ObservableProperty]
        private bool _isLockWarningActive;

        [ObservableProperty]
        private bool _confirmDelete = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSearching))]
        private string _searchText = string.Empty;

        /// <summary>お気に入り検索中に選択されたアイテム。検索クリア時にツリービューの選択を復元するために使用。</summary>
        [ObservableProperty]
        private FavoriteItem? _selectedSearchItem;

        /// <summary>お気に入り検索のヒット条件（名前・概要 または フルパス・概要）。</summary>
        [ObservableProperty]
        private FavoritesSearchMode _searchMode = FavoritesSearchMode.NameAndDescription;

        partial void OnSearchModeChanged(FavoritesSearchMode value)
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
                UpdateFilteredFavoritesList();
            if (_notificationEnabled)
                App.Notification.Notify(
                    value == FavoritesSearchMode.PathAndDescription ? "お気に入り検索を「フルパス・概要」に切り替えました" : "お気に入り検索を「名前・概要」に切り替えました",
                    $"検索モード: {(value == FavoritesSearchMode.PathAndDescription ? "フルパス・概要" : "名前・概要")}");
        }

        public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

        /// <summary>検索時にお気に入りを絞り込んだ結果を表示するためのフラットリスト（全ての候補を保持し、ICollectionViewでフィルタリングする）。</summary>
        public ObservableCollection<FavoriteItem> FilteredFavoritesList { get; } = new();

        /// <summary>FilteredFavoritesList のフィルタリング用ビュー。</summary>
        public ICollectionView FavoriteItemsView { get; private set; }

        partial void OnSearchTextChanged(string value)
        {
            UpdateFilteredFavoritesList();
        }

        [RelayCommand]
        private void ClearFavoritesSearch()
        {
            SearchText = string.Empty;
            _mainViewModel.FavoritesSearchText = string.Empty;
            if (_notificationEnabled)
                App.Notification.Notify("お気に入り検索を解除しました", "全件表示");
        }

        private void UpdateFilteredFavoritesList()
        {
            var search = SearchText?.Trim();

            // 検索結果の更新をUIスレッドで行う（InvokeAsyncでUIブロックを避ける）
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // リストをリセット（全ての項目を再取得して追加する）
                // 検索テキストが空の場合はクリアして終了（UI側で非表示になる）
                if (string.IsNullOrEmpty(search))
                {
                    // フィルタ解除前に選択中アイテムを保持し、解除後にツリービューで復元する
                    var itemToRestore = SelectedSearchItem;
                    SelectedSearchItem = null;
                    FilteredFavoritesList.Clear();
                    FavoriteItemsView.Refresh();

                    if (itemToRestore != null)
                    {
                        // ツリー内の祖先ノードを全て展開してアイテムを可視化する
                        var ancestors = new List<FavoriteItem>();
                        if (FindAncestors(Items, itemToRestore, ancestors))
                        {
                            foreach (var ancestor in ancestors)
                                ancestor.IsExpanded = true;
                            // 展開後のレイアウト完了を待ってから選択・スクロールを適用する
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                itemToRestore.IsSelected = true;
                                _mainViewModel.RequestScrollToFavorite?.Invoke(itemToRestore);
                            }), DispatcherPriority.Loaded);
                        }
                    }
                    return;
                }

                // 既存のリストと同期を取るのは複雑なので、シンプルに全クリアして全追加する方式を採用
                // ※ Favorites は数が少ないためパフォーマンスへの影響は軽微
                FilteredFavoritesList.Clear();

                var allItems = Flatten(Items).ToList();
                foreach (var item in allItems)
                {
                    FilteredFavoritesList.Add(item);
                }

                // ビューのリフレッシュを実行してフィルタを適用
                FavoriteItemsView.Refresh();

                // 検索結果の件数をステータスバーに通知
                int count = 0;
                foreach (var _ in FavoriteItemsView) count++;
                
                if (_notificationEnabled)
                {
                    App.Notification.Notify($"{count} 件のお気に入りが見つかりました", $"検索ワード: {search}");
                }
            });
        }

        /// <summary>ツリーを再帰的に走査し、全ての項目をフラットにして返す。</summary>
        private static IEnumerable<FavoriteItem> Flatten(IEnumerable<FavoriteItem> items)
        {
            foreach (var item in items)
            {
                // 整理用フォルダ（仮想フォルダ）は検索対象から除外（子要素は再帰的に追加）
                // IsContainer=true かつ Path が空の場合は整理用フォルダとみなす
                if (!item.IsContainer || !string.IsNullOrEmpty(item.Path))
                {
                    yield return item;
                }

                foreach (var child in Flatten(item.Children))
                    yield return child;
            }
        }

        /// <summary>
        /// ツリーを深さ優先で走査し、target への祖先チェーンを ancestors に積んで返す。
        /// target が見つかった場合は true（ancestors にはルートから target の親までが格納される）。
        /// </summary>
        private static bool FindAncestors(IEnumerable<FavoriteItem> items, FavoriteItem target, List<FavoriteItem> ancestors)
        {
            foreach (var item in items)
            {
                if (ReferenceEquals(item, target)) return true;
                ancestors.Add(item);
                if (FindAncestors(item.Children, target, ancestors)) return true;
                ancestors.RemoveAt(ancestors.Count - 1);
            }
            return false;
        }

        /// <summary>ICollectionView のフィルタロジック。SearchMode に応じて名前・概要 または フルパス・概要でヒット判定する。</summary>
        private bool FilterFavorites(object obj)
        {
            if (obj is not FavoriteItem item) return false;
            
            var search = SearchText?.Trim();
            if (string.IsNullOrWhiteSpace(search)) return true;

            var name = item.Name ?? string.Empty;
            var desc = item.Description ?? string.Empty;
            var path = item.Path ?? string.Empty;

            if (SearchMode == FavoritesSearchMode.NameAndDescription)
            {
                // 名前・概要に含まれる場合にヒット
                if (name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains(search, StringComparison.OrdinalIgnoreCase))
                    return true;
                int threshold = search.Length <= 3 ? 0 : search.Length <= 7 ? 1 : 2;
                if (threshold > 0)
                {
                    if (StringHelper.ComputeLevenshteinDistance(search.ToLower(), name.ToLower()) <= threshold)
                        return true;
                    if (!string.IsNullOrEmpty(desc) && StringHelper.ComputeLevenshteinDistance(search.ToLower(), desc.ToLower()) <= threshold)
                        return true;
                }
            }
            else
            {
                // フルパス・概要に含まれる場合にヒット
                if (path.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains(search, StringComparison.OrdinalIgnoreCase))
                    return true;
                int threshold = search.Length <= 3 ? 0 : search.Length <= 7 ? 1 : 2;
                if (threshold > 0)
                {
                    if (!string.IsNullOrEmpty(path) && StringHelper.ComputeLevenshteinDistance(search.ToLower(), path.ToLower()) <= threshold)
                        return true;
                    if (!string.IsNullOrEmpty(desc) && StringHelper.ComputeLevenshteinDistance(search.ToLower(), desc.ToLower()) <= threshold)
                        return true;
                }
            }

            return false;
        }

        // MatchesSearch メソッドは FilterFavorites に統合したので削除（あるいはリファクタリング用として残しても良いが今回は削除）

        partial void OnConfirmDeleteChanged(bool value)
        {
            if (_notificationEnabled)
                App.Notification.Notify(value ? "お気に入り削除時の確認をオンにしました" : "お気に入り削除時の確認をオフにしました", $"削除確認: {(value ? "オン" : "オフ")}");
        }

        private bool _notificationEnabled;

        /// <summary>MainViewModel.MarkInitializationComplete から呼ばれ、設定トグル通知を有効にする。</summary>
        public void MarkNotificationEnabled() => _notificationEnabled = true;

        private bool CanModify => !IsLocked;

        private CancellationTokenSource? _lockWarningCts;
        private readonly MainViewModel _mainViewModel;

        public FavoritesViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;

            // ICollectionView の初期化
            FavoriteItemsView = new ListCollectionView(FilteredFavoritesList);
            FavoriteItemsView.Filter = FilterFavorites;

            // お気に入りは MainWindow.Loaded で LoadFromSettings により読み込む（起動時UIブロック回避）

            Items.CollectionChanged += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(SearchText))
                    UpdateFilteredFavoritesList();
            };
        }

        /// <summary>ナビペインロック仕様: ロック中に禁止操作を試みたときに南京錠付近の矢印＋点滅アニメーションを約3秒表示する。</summary>
        public async Task TriggerLockWarningAsync()
        {
            _lockWarningCts?.Cancel();
            _lockWarningCts = new CancellationTokenSource();
            var token = _lockWarningCts.Token;
            IsLockWarningActive = true;
            try
            {
                await Task.Delay(3000, token);
            }
            catch (OperationCanceledException)
            {
                // ロック解除クリックなどでキャンセルされた場合は何もしない（OnIsLockedChanged でフラグをオフにする）
            }
            if (!token.IsCancellationRequested)
                IsLockWarningActive = false;
        }

        partial void OnIsLockedChanged(bool value)
        {
            if (!value)
            {
                _lockWarningCts?.Cancel();
                IsLockWarningActive = false;
            }
            if (_notificationEnabled)
                App.Notification.Notify(value ? "お気に入りをロックしました" : "お気に入りのロックを解除しました", $"お気に入りロック: {(value ? "オン" : "オフ")}");
        }

        [RelayCommand]
        private async Task NavigateToFavorite(FavoriteItem item)
        {
            if (!await EnsurePathExistsAsync(item)) return;

            // ショートカット（.lnk）の場合はリンク先を解析し、フォルダならアクティブペインの新しいタブで開く
            if (item.IsFile && !string.IsNullOrEmpty(item.Path) &&
                item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                string? targetPath = Helpers.ShortcutHelper.GetShortcutTarget(item.Path);
                if (!string.IsNullOrEmpty(targetPath))
                {
                    if (Directory.Exists(targetPath))
                    {
                        var pane = _mainViewModel.ActivePane;
                        pane?.AddTabWithPathCommand.Execute(targetPath);
                        App.Notification.Notify($"お気に入り「{item.Name}」のリンク先を新しいタブで開きました", $"ショートカット先: {targetPath}");
                        return;
                    }
                    if (!File.Exists(targetPath))
                    {
                        App.Notification.Notify("リンク先のパスが見つかりません", targetPath);
                        return;
                    }
                    // ファイルが存在する場合は、従来どおり OS 標準の関連付けに任せる（後続の処理にフォールスルー）
                }
            }

            if (item.IsFile)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(item.Path!) { UseShellExecute = true });
                    App.Notification.Notify($"お気に入り「{item.Name}」を開きました", $"お気に入りから開く: {item.Path}");
                }
                catch (Exception ex)
                {
                    ZenithDialog.Show($"ファイルを開けませんでした。\n{ex.Message}", "エラー", ZenithDialogButton.OK, ZenithDialogIcon.Warning);
                }
            }
            else
            {
                _mainViewModel.ActivePane?.NavigateCommand.Execute(item.Path);
                App.Notification.Notify($"お気に入り「{item.Name}」を開きました", $"お気に入りから開く: {item.Path}");
            }
        }

        [RelayCommand]
        private async Task OpenInAPane(FavoriteItem item)
        {
            if (!await EnsurePathExistsAsync(item)) return;

            if (item.IsFile)
            {
                var parentDir = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    _mainViewModel.LeftPane.NavigateCommand.Execute(parentDir);
                    App.Notification.Notify($"Aペインにお気に入り「{item.Name}」の親フォルダを表示しました", $"Aペインに表示: {parentDir}");
                }
            }
            else
            {
                _mainViewModel.LeftPane.NavigateCommand.Execute(item.Path);
                App.Notification.Notify($"Aペインにお気に入り「{item.Name}」を表示しました", $"Aペインにお気に入り表示: {item.Path}");
            }
        }

        [RelayCommand]
        private async Task OpenInBPane(FavoriteItem item)
        {
            if (!await EnsurePathExistsAsync(item)) return;

            if (item.IsFile)
            {
                var parentDir = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    _mainViewModel.RightPane.NavigateCommand.Execute(parentDir);
                    App.Notification.Notify($"Bペインにお気に入り「{item.Name}」の親フォルダを表示しました", $"Bペインに表示: {parentDir}");
                }
            }
            else
            {
                _mainViewModel.RightPane.NavigateCommand.Execute(item.Path);
                App.Notification.Notify($"Bペインにお気に入り「{item.Name}」を表示しました", $"Bペインにお気に入り表示: {item.Path}");
            }
        }

        /// <summary>
        /// お気に入りを新しいタブで開く。フォルダはそのフォルダを、ファイルは親フォルダを新規タブで表示する。
        /// </summary>
        [RelayCommand]
        private async Task OpenInNewTab(FavoriteItem item)
        {
            if (!await EnsurePathExistsAsync(item)) return;
            if (string.IsNullOrEmpty(item.Path)) return; // 仮想フォルダは対象外

            var pane = _mainViewModel.ActivePane;
            if (pane == null) return;

            string pathToOpen;
            if (item.IsFile)
            {
                var parentDir = Path.GetDirectoryName(item.Path);
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) return;
                pathToOpen = parentDir;
            }
            else
            {
                pathToOpen = item.Path;
            }

            pane.AddTabWithPathCommand.Execute(pathToOpen);
            App.Notification.Notify($"お気に入り「{item.Name}」を新しいタブで開きました", $"新規タブ: {pathToOpen}");
        }

        /// <summary>
        /// パスの存在確認を行い、見つからない場合は削除するかユーザーに確認する。
        /// クラウドパス（Box / SPO）の場合は親ディレクトリへのアクセスでハイドレーションをトリガーし、リトライする。
        /// ユーザーが削除を選択した場合は、ロック状態に関わらず削除を実行する。
        /// </summary>
        public async Task<bool> EnsurePathExistsAsync(FavoriteItem item)
        {
            if (item == null) return false;
            if (string.IsNullOrEmpty(item.Path)) return true; // 仮想フォルダなどは常に true

            if (Directory.Exists(item.Path) || File.Exists(item.Path)) return true;

            // クラウドパスの場合はハイドレーションをトリガーしてリトライ
            var sourceType = item.SourceType != SourceType.Local
                ? item.SourceType
                : PathHelper.DetermineSourceType(item.Path);

            if (sourceType is SourceType.Box or SourceType.SPO)
            {
                App.Notification.Notify("クラウドパスにアクセスしています...", item.Path);

                for (int i = 0; i < 2; i++)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            var parent = Path.GetDirectoryName(item.Path);
                            if (!string.IsNullOrEmpty(parent))
                                _ = Directory.Exists(parent);
                        }
                        catch { /* ignore */ }
                    });
                    await Task.Delay(500);
                    if (Directory.Exists(item.Path) || File.Exists(item.Path)) return true;
                }
            }

            var result = ZenithDialog.Show(
                $"パスが見つかりません:\n{item.Path}\n\nこの項目をお気に入りから削除しますか？",
                "パス未検出",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Warning);

            if (result == ZenithDialogResult.Yes)
            {
                // ロック状態にかかわらず削除（ユーザーの明示的な指示）
                if (RemoveItemRecursive(Items, item))
                {
                    SaveFavorites();
                    App.Notification.Notify($"見つからないお気に入り「{item.Name}」を削除しました", $"存在しないパスのため削除: {item.Path}");
                }
            }
            return false;
        }

        [RelayCommand(CanExecute = nameof(CanModify))]
        private void AddFolder(FavoriteItem? referenceItem)
        {
            if (IsLocked)
            {
                App.Notification.Notify("ロック中のため登録できませんでした", "お気に入りロック: 登録をブロック");
                _ = TriggerLockWarningAsync();
                return;
            }
            
            // 「新しいフォルダ」「新しいフォルダ (n)」の形式でデフォルト名を決定（既存と重複しないようにする）
            string defaultName = GetNextFolderName();

            // ルートまたは指定された項目の直下に新しい仮想フォルダを追加
            var newItem = new FavoriteItem { Name = defaultName };
            var info = ShellIconHelper.GetGenericInfo("dummy_folder", true);
            newItem.Icon = info.Icon;

            // 追加先コレクションと挿入位置を決定
            ObservableCollection<FavoriteItem> targetCollection;
            int insertIndex;

            if (referenceItem == null || !TryGetCollectionAndIndex(Items, referenceItem, out targetCollection!, out insertIndex))
            {
                // ルートの末尾に追加
                targetCollection = Items;
                insertIndex = Items.Count - 1;
            }

            // Level を決定（ルート直下 = 0、その他は参照アイテムと同じ階層）
            if (referenceItem == null)
            {
                newItem.Level = 0;
            }
            else
            {
                newItem.Level = referenceItem.Level;
            }

            // 「選択された項目の直下」に挿入するため index + 1 に挿入
            insertIndex = Math.Max(0, Math.Min(insertIndex + 1, targetCollection.Count));
            targetCollection.Insert(insertIndex, newItem);

            // 追加直後に選択状態にして、TreeView 側の Selected ハンドラから BringIntoView させる
            newItem.IsSelected = true;

            SaveFavorites();

            // 名前と概要を入力するダイアログを表示
            var addFolderInput = new AddFolderDialog(newItem.Name, "");
            if (addFolderInput.ShowDialog() == true)
            {
                string newName = addFolderInput.NameText.Trim();
                if (!string.IsNullOrWhiteSpace(newName) && newName != newItem.Name)
                    newItem.Name = newName;
                newItem.Description = string.IsNullOrWhiteSpace(addFolderInput.DescriptionText) ? null : addFolderInput.DescriptionText.Trim();
                SaveFavorites();
                App.Notification.Notify("お気に入りに新しいフォルダを追加しました", "お気に入りに仮想フォルダを追加");
            }
            else
            {
                // キャンセルされた場合は追加したアイテムを削除して元の状態に戻す
                targetCollection.Remove(newItem);
                SaveFavorites();
                App.Notification.Notify("お気に入りのフォルダ追加をキャンセルしました", "フォルダ追加をキャンセル");
            }
        }

        [RelayCommand(CanExecute = nameof(CanModify))]
        private void RemoveItem(FavoriteItem item)
        {
            if (item == null) return;
            if (IsLocked)
            {
                App.Notification.Notify("ロック中のため削除できませんでした", "お気に入りロック: 削除をブロック");
                _ = TriggerLockWarningAsync();
                return;
            }

            if (ConfirmDelete)
            {
                var result = ZenithDialog.Show($"'{item.Name}' をお気に入りから削除しますか？", "確認", ZenithDialogButton.YesNo, ZenithDialogIcon.Question);
                if (result != ZenithDialogResult.Yes) return;
            }

            if (RemoveItemRecursive(Items, item))
            {
                SaveFavorites();
                App.Notification.Notify($"お気に入り「{item.Name}」を削除しました", $"[Favorites] Remove: '{item.Name}'");
            }
        }

        [RelayCommand]
        private void RenameItem(FavoriteItem item)
        {
            if (item == null) return;

            string? inputText = InputBox.ShowDialog("名前の変更", item.Name, selectNameWithoutExtension: false);
            if (inputText != null)
            {
                string newName = inputText;
                if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

                string oldName = item.Name;
                item.Name = newName;
                SaveFavorites();
                App.Notification.Notify($"お気に入り「{oldName}」の名前を変更しました", $"お気に入り名前変更: '{oldName}' -> '{newName}'");
            }
        }

        [RelayCommand]
        private void EditDescription(FavoriteItem item)
        {
            if (item == null) return;

            var dialog = new DescriptionEditDialog(item.Description ?? "");
            if (dialog.ShowDialog() == true)
            {
                var newDesc = dialog.DescriptionText?.Trim();
                var oldDesc = item.Description;
                if (newDesc == oldDesc) return;

                item.Description = string.IsNullOrWhiteSpace(newDesc) ? null : newDesc;
                SaveFavorites();
                App.Notification.Notify($"お気に入り「{item.Name}」の概要を更新しました", $"概要編集: {(string.IsNullOrEmpty(newDesc) ? "(空)" : newDesc)}");
            }
        }

        private bool RemoveItemRecursive(ObservableCollection<FavoriteItem> collection, FavoriteItem target)
        {
            if (collection.Remove(target)) return true;

            foreach (var child in collection)
            {
                if (RemoveItemRecursive(child.Children, target)) return true;
            }
            return false;
        }

        /// <summary>
        /// 指定された項目を保持しているコレクションと、そのインデックスを取得する。
        /// </summary>
        private bool TryGetCollectionAndIndex(ObservableCollection<FavoriteItem> collection, FavoriteItem target, out ObservableCollection<FavoriteItem> resultCollection, out int index)
        {
            // まず現在のコレクション内を検索
            int localIndex = collection.IndexOf(target);
            if (localIndex >= 0)
            {
                resultCollection = collection;
                index = localIndex;
                return true;
            }

            // 見つからない場合は子要素を再帰的に探索
            foreach (var child in collection)
            {
                if (TryGetCollectionAndIndex(child.Children, target, out resultCollection, out index))
                {
                    return true;
                }
            }

            resultCollection = collection;
            index = -1;
            return false;
        }

        public bool ContainsPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return ContainsPathRecursive(Items, path);
        }

        private bool ContainsPathRecursive(IEnumerable<FavoriteItem> collection, string path)
        {
            foreach (var item in collection)
            {
                if (string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)) return true;
                if (ContainsPathRecursive(item.Children, path)) return true;
            }
            return false;
        }

        private FavoriteItem? FindItemByPathRecursive(IEnumerable<FavoriteItem> collection, string path)
        {
            foreach (var item in collection)
            {
                if (string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)) return item;
                var found = FindItemByPathRecursive(item.Children, path);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>指定パスをお気に入りから削除する。ロック時は実行しない。</summary>
        public bool RemovePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !ContainsPath(path)) return false;
            if (IsLocked)
            {
                App.Notification.Notify("ロック中のため削除できませんでした", "お気に入りロック");
                _ = TriggerLockWarningAsync();
                return false;
            }
            var item = FindItemByPathRecursive(Items, path);
            if (item == null) return false;
            if (ConfirmDelete)
            {
                var result = ZenithDialog.Show($"'{item.Name}' をお気に入りから削除しますか？", "確認", ZenithDialogButton.YesNo, ZenithDialogIcon.Question);
                if (result != ZenithDialogResult.Yes) return false;
            }
            if (RemoveItemRecursive(Items, item))
            {
                SaveFavorites();
                App.Notification.Notify($"お気に入り「{item.Name}」を削除しました", $"[Favorites] Remove: '{item.Name}'");
                return true;
            }
            return false;
        }

        /// <summary>
        /// お気に入り内の仮想フォルダ名と重複しない「新しいフォルダ」「新しいフォルダ (n)」の名前を返す。
        /// </summary>
        private string GetNextFolderName()
        {
            const string baseName = "新しいフォルダ";
            if (!ContainsNameRecursive(Items, baseName))
            {
                return baseName;
            }

            int n = 2;
            while (true)
            {
                string candidate = $"{baseName} ({n})";
                if (!ContainsNameRecursive(Items, candidate))
                {
                    return candidate;
                }
                n++;
            }
        }

        private bool ContainsNameRecursive(IEnumerable<FavoriteItem> collection, string name)
        {
            foreach (var item in collection)
            {
                if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (item.Children.Count > 0 && ContainsNameRecursive(item.Children, name))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>登録済みパスに対する 3 段フィードバック（MessageBox + ステータスバー通知 + ログ）を実行する。</summary>
        public void NotifyDuplicate(string path)
        {
            var log = $"[INF][Favorites] 登録スキップ: 既に登録済み ({path})";
            _ = App.FileLogger.LogAsync(log);
            App.Notification.Notify("登録済みのためスキップしました", log);
            ZenithDialog.Show($"既にお気に入りに登録されています:\n{path}", "お気に入り", ZenithDialogButton.OK, ZenithDialogIcon.Info);
        }

        /// <summary>パスをお気に入りに追加する。displayName と description は任意。フォルダ・ファイル両対応。ロック時も実行可能。</summary>
        public void AddPath(string path, string? displayName = null, string? description = null)
        {
            if (string.IsNullOrEmpty(path) || (!Directory.Exists(path) && !File.Exists(path))) return;

            // 重複チェック
            if (ContainsPath(path))
            {
                NotifyDuplicate(path);
                return;
            }

            var name = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path; // ドライブルートなどの場合

            var item = new FavoriteItem
            {
                Name = name,
                Path = path,
                Level = 0,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                SourceType = PathHelper.DetermineSourceType(path)
            };

            // アイコン取得（フォルダ/ファイルで使い分け）
            var info = Directory.Exists(path)
                ? ShellIconHelper.GetFolderIcon(path)
                : ShellIconHelper.GetInfo(path, false);
            item.Icon = info.Icon;

            Items.Add(item);
            SaveFavorites();
            App.Notification.Notify($"お気に入りに登録しました: {name}", $"[Favorites] Add: '{name}' ({path})");
        }

        /// <summary>ダイアログを表示して名前・概要を入力後、お気に入りに追加する。キャンセル時は false。フォルダ・ファイル両対応。ロック時も実行可能。</summary>
        public bool AddPathWithDialog(string path)
        {
            if (string.IsNullOrEmpty(path) || (!Directory.Exists(path) && !File.Exists(path)) || ContainsPath(path))
                return false;

            var defaultName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(defaultName)) defaultName = path;

            var dialog = new AddToFavoritesDialog(defaultName, "");
            if (dialog.ShowDialog() != true)
                return false;

            var name = dialog.NameText.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = defaultName;

            AddPath(path, name, dialog.DescriptionText?.Trim());
            return true;
        }

        /// <summary>ダイアログ表示後に登録し、お気に入りビューへ遷移して追加項目を強調表示する。コンテキストメニュー・ツールバーから呼ぶ。</summary>
        public async Task AddPathWithDialogAndHighlightAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || (!Directory.Exists(path) && !File.Exists(path)))
                return;
            if (ContainsPath(path))
            {
                NotifyDuplicate(path);
                return;
            }

            var defaultName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(defaultName)) defaultName = path;

            var dialog = new AddToFavoritesDialog(defaultName, "");
            if (dialog.ShowDialog() != true)
                return;

            var name = dialog.NameText.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = defaultName;

            AddPath(path, name, dialog.DescriptionText?.Trim());

            // お気に入りビューへ切り替え
            _mainViewModel.SidebarMode = SidebarViewMode.Favorites;

            var item = FindItemByPathRecursive(Items, path);
            if (item != null)
            {
                item.IsSelected = true;
                item.IsSuccessHighlighted = true;
                _mainViewModel.RequestScrollToFavorite?.Invoke(item);

                await Task.Delay(2100);
                item.IsSuccessHighlighted = false;
            }
        }

        /// <summary>複数パスを新規仮想フォルダにまとめてお気に入りに追加する。フォルダ名はダイアログで入力。ロック中は実行不可。</summary>
        public void AddPathsToNewFolder(IEnumerable<string> paths)
        {
            var validPaths = paths
                .Where(p => !string.IsNullOrWhiteSpace(p) && (Directory.Exists(p) || File.Exists(p)))
                .Select(p => p!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(p => !ContainsPath(p))
                .ToList();

            if (validPaths.Count == 0)
            {
                ZenithDialog.Show("追加できるタブがありません。\n（存在しないパス、空のパス、既にお気に入りに登録済みのパスはスキップされます）", "お気に入り", ZenithDialogButton.OK, ZenithDialogIcon.Info);
                return;
            }

            if (IsLocked)
            {
                App.Notification.Notify("ロック中のため登録できませんでした", "お気に入りロック: 登録をブロック");
                _ = TriggerLockWarningAsync();
                return;
            }

            string defaultName = GetNextFolderName();
            var addFolderInput = new AddFolderDialog(defaultName, "");
            if (addFolderInput.ShowDialog() != true)
                return;

            string folderName = addFolderInput.NameText.Trim();
            if (string.IsNullOrWhiteSpace(folderName)) folderName = defaultName;

            var folderItem = new FavoriteItem
            {
                Name = folderName,
                Level = 0,
                Path = null,
                Description = string.IsNullOrWhiteSpace(addFolderInput.DescriptionText) ? null : addFolderInput.DescriptionText.Trim()
            };
            var folderInfo = ShellIconHelper.GetGenericInfo("dummy_folder", true);
            folderItem.Icon = folderInfo.Icon;

            foreach (var path in validPaths)
            {
                var name = System.IO.Path.GetFileName(path);
                if (string.IsNullOrEmpty(name)) name = path;

                var childItem = new FavoriteItem
                {
                    Name = name,
                    Level = folderItem.Level + 1,
                    Path = path,
                    SourceType = PathHelper.DetermineSourceType(path)
                };
                var info = Directory.Exists(path)
                    ? ShellIconHelper.GetFolderIcon(path)
                    : ShellIconHelper.GetInfo(path, false);
                childItem.Icon = info.Icon;
                folderItem.Children.Add(childItem);
            }

            Items.Add(folderItem);
            SaveFavorites();
            App.Notification.Notify($"{folderName} に {validPaths.Count} 件を追加しました", "タブをお気に入りに追加");
        }

        private const string ImportFolderName = "インポート";
        private const string ImportFolderDescription = "エクスプローラのクイックアクセスからインポート";

        /// <summary>「インポート」フォルダを取得する。存在しなければ作成して返す。</summary>
        private FavoriteItem GetOrCreateImportFolder()
        {
            foreach (var item in Items)
            {
                if (item.Path == null && string.Equals(item.Name, ImportFolderName, StringComparison.Ordinal))
                    return item;
            }

            var folder = new FavoriteItem
            {
                Name = ImportFolderName,
                Description = ImportFolderDescription,
                Level = 0,
                Path = null,
                IsExpanded = true
            };
            var folderInfo = ShellIconHelper.GetGenericInfo("dummy_folder", true);
            folder.Icon = folderInfo.Icon;
            Items.Add(folder);
            return folder;
        }

        /// <summary>エクスプローラのクイックアクセス（ピン留め等）の項目を「インポート」フォルダ内にお気に入りにインポートする。</summary>
        [RelayCommand]
        private void ImportFromQuickAccess()
        {
            try
            {
                var targetFolder = GetOrCreateImportFolder();
                using var quickAccess = new ShellFolder("shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}");
                int importedCount = 0;

                foreach (var item in quickAccess)
                {
                    string? path = item.FileSystemPath;
                    if (string.IsNullOrEmpty(path) || ContainsPath(path)) continue;

                    bool isDirectory = Directory.Exists(path);
                    if (!isDirectory && !File.Exists(path)) continue;

                    var favorite = new FavoriteItem
                    {
                        Name = item.Name ?? System.IO.Path.GetFileName(path) ?? path,
                        Path = path,
                        Level = targetFolder.Level + 1,
                        SourceType = PathHelper.DetermineSourceType(path)
                    };

                    var info = isDirectory ? ShellIconHelper.GetFolderIcon(path) : ShellIconHelper.GetInfo(path, false);
                    favorite.Icon = info.Icon;

                    targetFolder.Children.Add(favorite);
                    importedCount++;
                }

                if (importedCount > 0)
                {
                    SaveFavorites();
                    App.Notification.Notify($"{importedCount} 件の項目をクイックアクセスからインポートしました", "インポート完了");
                }
                else
                {
                    App.Notification.Notify("インポートできる新しい項目はありませんでした", "インポート");
                }
            }
            catch (Exception ex)
            {
                ZenithDialog.Show($"インポート中にエラーが発生しました。\n{ex.Message}", "エラー", ZenithDialogButton.OK, ZenithDialogIcon.Error);
            }
        }

        public void SaveFavorites()
        {
            WindowSettings.SaveFavoritesOnly(ToDto(Items));
        }

        /// <summary>現在の Items のスナップショット（DTO ディープコピー）を返す。ロールバック用。</summary>
        internal List<FavoriteItemDto> TakeSnapshot() => ToDto(Items);

        /// <summary>スナップショットから Items を再構築する。移動失敗時のロールバック用。</summary>
        internal void RestoreFromSnapshot(List<FavoriteItemDto> snapshot)
        {
            Items.Clear();
            foreach (var dto in snapshot)
                Items.Add(FromDto(dto));
        }

        /// <summary>設定ファイルへの保存を試み、成否を返す。失敗時は監査ログへ記録。</summary>
        internal bool TrySaveFavorites()
        {
            try
            {
                WindowSettings.SaveFavoritesOnlyOrThrow(ToDto(Items));
                return true;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[ERR][Favorites] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>読み込み済みの設定からお気に入りを復元する。起動時は MainWindow.Loaded から呼ばれる。</summary>
        public void LoadFromSettings(WindowSettings settings)
        {
            if (settings == null) return;
            Items.Clear();

            if (settings.Favorites is { Count: > 0 })
            {
                foreach (var dto in settings.Favorites)
                {
                    Items.Add(FromDto(dto, 0));
                }
                // アイコンを非同期バッチ取得（UIスレッドをブロックしない）
                _ = LoadIconsAsync(Items);
                return;
            }

            AddDefaultFavoriteInternal(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            AddDefaultFavoriteInternal(PathHelper.GetDownloadsPath());
            AddDefaultFavoriteInternal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            SaveFavorites();
        }

        private void AddDefaultFavorite(string path)
        {
            if (Directory.Exists(path)) AddPath(path);
        }

        /// <summary>初期ロード時用。SaveFavorites を呼ばずに追加する。</summary>
        private void AddDefaultFavoriteInternal(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (ContainsPath(path)) return;

            var name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;

            var item = new FavoriteItem
            {
                Name = name,
                Path = path,
                Level = 0,
                SourceType = PathHelper.DetermineSourceType(path)
            };
            var info = ShellIconHelper.GetFolderIcon(path);
            item.Icon = info.Icon;
            Items.Add(item);
        }

        private List<FavoriteItemDto> ToDto(IEnumerable<FavoriteItem> items)
        {
            var list = new List<FavoriteItemDto>();
            foreach (var item in items)
            {
                list.Add(new FavoriteItemDto
                {
                    Name = item.Name,
                    Path = item.Path,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                    IsExpanded = item.IsExpanded,
                    LocationType = item.SourceType,
                    Children = ToDto(item.Children)
                });
            }
            return list;
        }

        /// <summary>設定保存用。現在のお気に入りを DTO リストで返す。</summary>
        public List<FavoriteItemDto> GetFavoritesForSave() => ToDto(Items);

        private FavoriteItem FromDto(FavoriteItemDto dto, int level = 0)
        {
            var item = new FavoriteItem
            {
                Name = dto.Name,
                Path = dto.Path,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                IsExpanded = dto.IsExpanded,
                Level = level,
                SourceType = !string.IsNullOrEmpty(dto.Path) ? PathHelper.DetermineSourceType(dto.Path) : dto.LocationType
            };
            // アイコンは後で非同期バッチ読み込み（FromDto を高速化）

            foreach (var childDto in dto.Children)
            {
                item.Children.Add(FromDto(childDto, level + 1));
            }
            return item;
        }

        /// <summary>全項目のアイコンをバックグラウンドスレッドで取得し、UI スレッドに反映する。</summary>
        private static async Task LoadIconsAsync(IEnumerable<FavoriteItem> items)
        {
            var flatList = FlattenItems(items).ToList();
            if (flatList.Count == 0) return;

            // バックグラウンドで全アイコンを一括取得
            var icons = await Task.Run(() =>
            {
                var result = new List<(FavoriteItem item, ImageSource? icon)>(flatList.Count);
                foreach (var item in flatList)
                {
                    ImageSource? icon = null;
                    if (!string.IsNullOrEmpty(item.Path))
                    {
                        var info = Directory.Exists(item.Path)
                            ? ShellIconHelper.GetFolderIcon(item.Path)
                            : ShellIconHelper.GetInfo(item.Path, false);
                        icon = info.Icon;
                    }
                    else
                    {
                        var info = ShellIconHelper.GetGenericInfo("dummy_folder", true);
                        icon = info.Icon;
                    }
                    result.Add((item, icon));
                }
                return result;
            });

            // UI スレッドで反映（Icon は [ObservableProperty] なので PropertyChanged が自動発火）
            foreach (var (item, icon) in icons)
            {
                item.Icon = icon;
            }
        }

        /// <summary>お気に入りツリーを再帰的にフラット化する。</summary>
        private static IEnumerable<FavoriteItem> FlattenItems(IEnumerable<FavoriteItem> items)
        {
            foreach (var item in items)
            {
                yield return item;
                foreach (var child in FlattenItems(item.Children))
                    yield return child;
            }
        }

        // ─── お気に入りExcelバックアップ ────────────────────────────────────────────

        /// <summary>お気に入りデータをExcel形式でバックアップ保存する。</summary>
        [RelayCommand]
        private async Task ExportFavoritesToExcelAsync()
        {
            // SaveFileDialog（UIスレッドで表示）
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel ファイル (*.xlsx)|*.xlsx",
                FileName = $"favorites_backup_{DateTime.Now:yyyyMMdd}.xlsx",
                Title = "お気に入りバックアップの保存先"
            };
            if (dialog.ShowDialog() != true) return;
            var savePath = dialog.FileName;

            // ツリーを深さ優先でフラット化（グループ含む全項目）
            var allItems = FlattenAll(Items).ToList();
            if (allItems.Count == 0)
            {
                App.Notification.Notify("エクスポート対象がありません", "お気に入りが空です");
                return;
            }

            using var busyToken = _mainViewModel.BeginBusy();

            // ── GlowBar 開始 ──
            _mainViewModel.BeginFileOperation("[お気に入りバックアップ] データを解析しています...",
                FlowDirection.LeftToRight);
            _mainViewModel.FileOperationProgress = 2;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            // ── DispatcherTimer による滑らか進捗 ──
            double progressTarget = 2;
            string statusText = "[お気に入りバックアップ] データを解析しています...";

            var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            progressTimer.Tick += (_, _) =>
            {
                double target = Volatile.Read(ref progressTarget);
                double current = _mainViewModel.FileOperationProgress;
                _mainViewModel.FileOperationStatusText = statusText;
                if (Math.Abs(target - current) < 0.3) return;
                double step = (target - current) * 0.18;
                if (step > 0 && step < 0.5) step = 0.5;
                _mainViewModel.FileOperationProgress = Math.Min(current + step, target);
            };
            progressTimer.Start();

            var sw = Stopwatch.StartNew();
            bool succeeded = false;

            try
            {
                await Task.Run(() =>
                {
                    // ── 解析フェーズ (target: 10) ──
                    Volatile.Write(ref progressTarget, 10.0);
                    statusText = $"[お気に入りバックアップ] {allItems.Count:N0} 件のデータを書き込み中...";

                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("お気に入り");

                    // --- タイトル行 (Row 1) ---
                    ws.Cell("A1").Value = $"お気に入りバックアップ（{DateTime.Now:yyyy/MM/dd HH:mm} 出力・{allItems.Count:N0} 件）";
                    ws.Cell("A1").Style.Font.Bold = true;
                    ws.Cell("A1").Style.Font.FontSize = 13;
                    ws.Range("A1:F1").Merge();

                    // --- ヘッダー行 (Row 2) ---
                    string[] headers = { "No", "グループ", "表示名", "フルパス", "種類", "開く" };
                    for (int c = 0; c < headers.Length; c++)
                    {
                        var cell = ws.Cell(2, c + 1);
                        cell.Value = headers[c];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontSize = 11;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6E1D3");
                        cell.Style.Font.FontColor = XLColor.FromHtml("#1A1A1A");
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    // --- データ行 (Row 3〜) ---
                    int lastTargetPct = 10;
                    int total = allItems.Count;

                    for (int i = 0; i < total; i++)
                    {
                        int row = i + 3;
                        var (item, groupName) = allItems[i];

                        ws.Cell(row, 1).Value = i + 1;                                    // A: No
                        ws.Cell(row, 2).Value = groupName;                                 // B: グループ
                        ws.Cell(row, 3).Value = item.Name;                                 // C: 表示名
                        ws.Cell(row, 4).Value = item.Path ?? "（整理用フォルダ）";           // D: フルパス
                        ws.Cell(row, 5).Value = FormatSourceType(item);                    // E: 種類
                        ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // F: 開くリンク（D列を参照する HYPERLINK 関数）
                        if (!string.IsNullOrEmpty(item.Path))
                        {
                            var linkCell = ws.Cell(row, 6);
                            linkCell.FormulaA1 = $"HYPERLINK(D{row},\"開く\")";
                            linkCell.Style.Font.FontColor = XLColor.FromHtml("#0563C1");
                            linkCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                        }

                        // ゼブラストライプ
                        if (i % 2 == 1)
                        {
                            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");
                        }

                        // 2% 刻みで進捗更新
                        if (total > 0)
                        {
                            int currentPct = (int)(10 + (i + 1) * 70.0 / total);
                            if (currentPct >= lastTargetPct + 2)
                            {
                                lastTargetPct = currentPct;
                                Volatile.Write(ref progressTarget, (double)currentPct);
                                statusText = $"[お気に入りバックアップ] {(i + 1):N0} / {total:N0} 件を処理中... ({currentPct}%)";
                            }
                        }
                    }

                    // --- 全範囲のスタイル（ヘッダー＋データ）---
                    int lastRow = total + 2;
                    if (lastRow >= 2)
                    {
                        var fullRange = ws.Range(2, 1, lastRow, 6);
                        // 全セルに格子罫線
                        var borderColor = XLColor.FromHtml("#D9D9D9");
                        fullRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                        fullRange.Style.Border.TopBorderColor = borderColor;
                        fullRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                        fullRange.Style.Border.BottomBorderColor = borderColor;
                        fullRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                        fullRange.Style.Border.LeftBorderColor = borderColor;
                        fullRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
                        fullRange.Style.Border.RightBorderColor = borderColor;
                        // 外周: やや太めの線
                        fullRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                        fullRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#545B64");
                    }
                    if (lastRow >= 3)
                    {
                        var dataRange = ws.Range(3, 1, lastRow, 6);
                        dataRange.Style.Font.FontSize = 10;
                        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    // --- レイアウト: AutoFit + 最低幅保証 ---
                    ws.Columns(1, 6).AdjustToContents();
                    if (ws.Column(1).Width < 6)  ws.Column(1).Width = 6;   // A: No
                    if (ws.Column(2).Width < 14) ws.Column(2).Width = 14;  // B: グループ
                    if (ws.Column(3).Width < 20) ws.Column(3).Width = 20;  // C: 表示名
                    if (ws.Column(4).Width < 30) ws.Column(4).Width = 30;  // D: フルパス
                    if (ws.Column(5).Width < 10) ws.Column(5).Width = 10;  // E: 種類
                    if (ws.Column(6).Width < 10) ws.Column(6).Width = 10;  // F: 開く

                    // オートフィルタ + ヘッダー固定
                    ws.Range(2, 1, lastRow, 6).SetAutoFilter();
                    ws.SheetView.FreezeRows(2);
                    ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

                    // ── 保存フェーズ (target: 95) ──
                    Volatile.Write(ref progressTarget, 95.0);
                    statusText = "[お気に入りバックアップ] ファイルを保存しています...";

                    workbook.SaveAs(savePath);
                });

                succeeded = true;
            }
            catch (Exception ex)
            {
                App.Notification.Notify("お気に入りバックアップに失敗しました", ex.Message);
            }
            finally
            {
                sw.Stop();
                var minDisplay = TimeSpan.FromMilliseconds(800);
                if (sw.Elapsed < minDisplay)
                    await Task.Delay(minDisplay - sw.Elapsed);

                progressTimer.Stop();
                _mainViewModel.EndFileOperation();
            }

            if (succeeded)
            {
                App.Notification.Notify($"お気に入りバックアップを保存しました（{allItems.Count:N0} 件）",
                    $"保存先: {savePath}");
            }
        }

        /// <summary>ツリーを深さ優先で走査し、グループ名付きで全項目をフラット化する（整理用フォルダ含む）。</summary>
        private static IEnumerable<(FavoriteItem Item, string GroupName)> FlattenAll(IEnumerable<FavoriteItem> items)
        {
            foreach (var item in items)
            {
                if (item.Level == 0 && item.IsContainer)
                {
                    // Level 0 のグループ自体も出力し、子を再帰
                    yield return (item, "");
                    foreach (var entry in FlattenAllWithGroup(item.Children, item.Name))
                        yield return entry;
                }
                else
                {
                    // ルート直下の非グループ項目
                    yield return (item, "（ルート）");
                }
            }
        }

        private static IEnumerable<(FavoriteItem Item, string GroupName)> FlattenAllWithGroup(
            IEnumerable<FavoriteItem> items, string groupName)
        {
            foreach (var item in items)
            {
                yield return (item, groupName);
                if (item.Children.Count > 0)
                {
                    // サブグループ名を「親 > 子」形式に
                    var subGroup = item.IsContainer ? $"{groupName} > {item.Name}" : groupName;
                    foreach (var entry in FlattenAllWithGroup(item.Children, subGroup))
                        yield return entry;
                }
            }
        }

        private static string FormatSourceType(FavoriteItem item)
        {
            if (item.IsContainer && string.IsNullOrEmpty(item.Path))
                return "フォルダグループ";
            if (item.IsFile)
                return $"ファイル ({item.SourceType})";
            return item.SourceType switch
            {
                SourceType.Server => "ネットワーク",
                SourceType.Box => "Box",
                SourceType.SPO => "SharePoint",
                _ => "ローカル"
            };
        }
    }
}
