using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenithFiler.Views;

namespace ZenithFiler
{
    /// <summary>ナビペイン「インデックス検索設定」ビューの ViewModel。検索対象フォルダの登録・削除を行う。</summary>
    public partial class IndexSearchSettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _main;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<IndexSearchTargetItemViewModel> _items = new();

        [ObservableProperty]
        private bool _isEmpty = true;

        [ObservableProperty]
        private int _indexedDocumentCount;

        [ObservableProperty]
        private bool _isPaused;

        /// <summary>インデックスの状態表示用テキスト。</summary>
        [ObservableProperty]
        private string _indexStatusText = "インデックス完了";

        // ── スコープセレクター ──

        /// <summary>スコープ Popup に表示するフォルダ一覧。</summary>
        [ObservableProperty]
        private ObservableCollection<IndexSearchScopeItemViewModel> _scopeItems = new();

        /// <summary>スコープバッジテキスト（例: "3/10"）。</summary>
        [ObservableProperty]
        private string _scopeBadgeText = string.Empty;

        /// <summary>スコープが絞り込まれているか（全選択でない場合 true）。</summary>
        [ObservableProperty]
        private bool _isScopeFiltered;

        /// <summary>スコープ選択が変更されたときに発火するイベント。検索結果のリアルタイム再描画に使用。</summary>
        public event Action? ScopeSelectionChanged;

        public IndexSearchSettingsViewModel(MainViewModel main)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            UpdateIsEmpty();
            App.IndexService.PauseStateChanged += OnPauseStateChanged;
            if (App.Notification is INotifyPropertyChanged npc)
                npc.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(NotificationService.IsIndexing))
                        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            UpdateIndexStatusText();
                            RefreshItemsStatus();
                        });
                };
        }

        private void OnPauseStateChanged()
        {
            _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsPaused = App.IndexService.IsPaused;
                UpdateIndexStatusText();
            });
        }

        private void UpdateIndexStatusText()
        {
            if (App.IndexService.IsPaused)
                IndexStatusText = "一時停止中";
            else if (App.Notification.IsIndexing)
                IndexStatusText = "差分更新中";
            else
                IndexStatusText = "インデックス完了";
        }

        /// <summary>インデックス作成進捗を Notification に反映する Progress を生成します。</summary>
        private IProgress<IndexingProgress> CreateIndexingProgress()
        {
            return new Progress<IndexingProgress>(p =>
            {
                // ステータスバー表示（安定幅、パスなし）
                App.Notification.IndexingStatusMessage =
                    $"インデックス更新中: {p.ProcessedCount:N0} 件";

                // per-item スキャンフォルダ更新
                var item = Items.FirstOrDefault(x =>
                    string.Equals(x.Path, p.RootPath, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                    item.CurrentScanFolder = p.CurrentFolder ?? string.Empty;
            });
        }

        /// <summary>設定保存用にパス一覧を返す。</summary>
        public List<string> GetPathsForSave()
        {
            return Items.Select(x => x.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        }

        public List<string> GetLockedPathsForSave()
        {
            return Items.Where(x => x.IsLocked && !string.IsNullOrWhiteSpace(x.Path))
                        .Select(x => x.Path).ToList();
        }

        /// <summary>設定読み込み時にパス一覧を反映する。初回インデックスは MainWindow で ConfigureIndexUpdate 後にトリガー。</summary>
        public void LoadPaths(IReadOnlyList<string> paths)
        {
            Items.Clear();
            if (paths != null)
            {
                foreach (var p in paths)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    var normalized = PathHelper.GetPhysicalPath(p);
                    if (!Directory.Exists(normalized)) continue;
                    if (Items.Any(x => string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase))) continue;
                    Items.Add(new IndexSearchTargetItemViewModel { Path = normalized });
                }
            }
            NotifyItemsChanged();
            RefreshStatus();
        }

        [RelayCommand]
        private void AddFolder()
        {
            var currentPath = _main.ActivePane?.CurrentPath ?? string.Empty;
            if (string.IsNullOrEmpty(currentPath) || !Directory.Exists(PathHelper.GetPhysicalPath(currentPath)))
                currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) ?? string.Empty;

            var dialog = new SelectFolderDialog(currentPath);
            if (dialog.ShowDialog() != true) return;

            var path = (dialog.SelectedPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                App.Notification.Notify("パスが入力されていません", "インデックス検索対象の追加");
                return;
            }

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"インデックス検索対象: {path}");
                return;
            }

            if (Items.Any(x => string.Equals(x.Path, physical, StringComparison.OrdinalIgnoreCase)))
            {
                NotifyItemsChanged(); // 既に登録済みでも一覧表示を正しく更新（空メッセージを隠して ListView を見せる）
                App.Notification.Notify("既に登録されています", $"インデックス検索対象: {Path.GetFileName(physical)}");
                return;
            }

            var newItem = new IndexSearchTargetItemViewModel { Path = physical };
            Items.Add(newItem);
            NotifyItemsChanged();
            RefreshItemsStatus();

            // Manual モード以外では新規追加時にインデックス作成を開始。Manual は「今すぐ更新」でのみ
            if (App.IndexService.CurrentUpdateMode != IndexUpdateMode.Manual)
                _ = App.IndexService.AddDirectoryToIndexAsync(physical, CreateIndexingProgress());

            App.Notification.Notify("検索対象に追加しました", $"インデックス検索: {Path.GetFileName(physical)}");
        }

        [RelayCommand(CanExecute = nameof(CanRemove))]
        private void Remove(IndexSearchTargetItemViewModel? item)
        {
            var toRemove = item ?? SelectedItem;
            if (toRemove != null && Items.Remove(toRemove))
            {
                // インデックス済みマークも解除し、再登録時に再インデックスできるようにする
                App.IndexService.UnmarkAsIndexed(toRemove.Path);
                if (SelectedItem == toRemove) SelectedItem = null;
                NotifyItemsChanged();
                App.Notification.Notify("検索対象から削除しました", $"インデックス検索: {toRemove.DisplayName}");
            }
        }

        /// <summary>Items 変更を UI に通知（空状態 Border の表示切替・ListView の再評価用）。</summary>
        internal void NotifyItemsChanged()
        {
            OnPropertyChanged(nameof(Items));
            UpdateIsEmpty();
            RebuildScopeItems();
        }

        /// <summary>インデックスの総件数を再取得し、UI に反映します。登録フォルダが0件のときは0を表示します。</summary>
        public void RefreshIndexedDocumentCount()
        {
            if (Items.Count == 0)
            {
                IndexedDocumentCount = 0;
                return;
            }
            IndexedDocumentCount = App.IndexService.GetIndexedDocumentCount();
        }

        /// <summary>各フォルダの状態（作成中/待機中/完了/アーカイブ済）と件数・日時を再取得し、UI に反映します。</summary>
        public void RefreshItemsStatus()
        {
            foreach (var item in Items)
            {
                item.IsLocked = App.IndexService.IsRootLocked(item.Path);
                // ロック済みアイテムは一括更新キューに入らないため、待機中/作成中にはならない
                item.IsInProgress = !item.IsLocked && App.IndexService.IsRootInProgress(item.Path);
                item.IsWaiting = !item.IsLocked && !item.IsInProgress && App.IndexService.IsRootPending(item.Path);
                item.IsIndexed = App.IndexService.IsRootIndexed(item.Path);

                if (item.IsLocked)
                {
                    // ロック済みアイテムは凍結されたスナップショットを優先的に使用する
                    var lockedCount = App.IndexService.GetLockedDocumentCount(item.Path);
                    if (lockedCount > 0)
                        item.DocumentCount = lockedCount;
                    else if (item.IsIndexed)
                        item.DocumentCount = App.IndexService.GetDocumentCountForRoot(item.Path);
                    // else: 既存値を維持（0 でリセットしない）
                }
                else
                {
                    item.DocumentCount = item.IsIndexed ? App.IndexService.GetDocumentCountForRoot(item.Path) : 0;
                }

                item.LastIndexedDateTime = App.IndexService.GetLastIndexedTime(item.Path);
                if (!item.IsInProgress)
                    item.CurrentScanFolder = string.Empty;
            }
            UpdateIndexStatusText();
            ApplySort();

            // ScopeItems の IsLocked / DocumentCount を同期
            foreach (var scope in ScopeItems)
            {
                var src = Items.FirstOrDefault(i => string.Equals(i.Path, scope.Path, StringComparison.OrdinalIgnoreCase));
                if (src == null) continue;
                scope.IsLocked = src.IsLocked;
                scope.DocumentCount = src.DocumentCount;
            }
        }

        /// <summary>インデックス状況を再取得し、UI に反映します。</summary>
        public void RefreshStatus()
        {
            IsPaused = App.IndexService.IsPaused;
            RefreshIndexedDocumentCount();
            RefreshItemsStatus();
        }

        private void UpdateIsEmpty()
        {
            IsEmpty = Items.Count == 0;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
        private IndexSearchTargetItemViewModel? _selectedItem;

        private bool CanRemove(IndexSearchTargetItemViewModel? item) => item != null || SelectedItem != null;

        /// <summary>コンテキストメニュー「インデックス検索でこのフォルダを検索」から呼ぶ。登録し、インデックス設定ビューへ遷移して強調表示する。</summary>
        public async Task AddFolderFromContextMenuAndHighlightAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                App.Notification.Notify("パスが指定されていません", "インデックス検索対象の追加");
                return;
            }

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"インデックス検索対象: {path}");
                return;
            }

            var existing = Items.FirstOrDefault(x => string.Equals(x.Path, physical, StringComparison.OrdinalIgnoreCase));
            IndexSearchTargetItemViewModel targetItem;

            if (existing != null)
            {
                targetItem = existing;
            }
            else
            {
                targetItem = new IndexSearchTargetItemViewModel { Path = physical };
                Items.Add(targetItem);
                NotifyItemsChanged();
                RefreshItemsStatus();

                if (App.IndexService.CurrentUpdateMode != IndexUpdateMode.Manual)
                    _ = App.IndexService.AddDirectoryToIndexAsync(physical, CreateIndexingProgress());

                App.Notification.Notify("検索対象に追加しました", $"インデックス検索: {Path.GetFileName(physical)}");
            }

            // インデックス検索設定ビューへ切り替え
            _main.SidebarMode = SidebarViewMode.IndexSearch;

            SelectedItem = targetItem;
            targetItem.IsSuccessHighlighted = true;
            _main.RequestScrollToIndexSearchTarget?.Invoke(targetItem);

            await Task.Delay(2100);
            targetItem.IsSuccessHighlighted = false;
        }

        /// <summary>ドロップされたパス群からフォルダだけを検索対象に追加する。</summary>
        public async Task AddFoldersByDropAsync(IEnumerable<string> paths)
        {
            int addedCount = 0;
            foreach (var raw in paths)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var physical = PathHelper.GetPhysicalPath(raw);

                // フォルダ存在確認を非同期で行い UI フリーズを防止
                var exists = await Task.Run(() => Directory.Exists(physical)).ConfigureAwait(true);
                if (!exists) continue;

                // 重複チェック
                if (Items.Any(x => string.Equals(x.Path, physical, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var newItem = new IndexSearchTargetItemViewModel { Path = physical };
                Items.Add(newItem);
                addedCount++;

                if (App.IndexService.CurrentUpdateMode != IndexUpdateMode.Manual)
                    _ = App.IndexService.AddDirectoryToIndexAsync(physical, CreateIndexingProgress());
            }

            if (addedCount > 0)
            {
                NotifyItemsChanged();
                RefreshItemsStatus();
                App.Notification.Notify($"{addedCount} 件のフォルダを追加しました", "インデックス検索");

                // 最後に追加したアイテムを強調表示
                var lastItem = Items.LastOrDefault();
                if (lastItem != null)
                {
                    SelectedItem = lastItem;
                    lastItem.IsSuccessHighlighted = true;
                    _main.RequestScrollToIndexSearchTarget?.Invoke(lastItem);
                    await Task.Delay(2100);
                    lastItem.IsSuccessHighlighted = false;
                }
            }
            else
            {
                App.Notification.Notify("追加できるフォルダがありませんでした（ファイルまたは登録済み）", "インデックス検索");
            }
        }

        /// <summary>アクティブペインの現在フォルダを検索対象に追加する。</summary>
        [RelayCommand]
        private void AddCurrentFolder()
        {
            var path = _main.ActivePane?.CurrentPath;
            if (string.IsNullOrEmpty(path))
            {
                App.Notification.Notify("表示中のフォルダがありません", "インデックス検索対象の追加");
                return;
            }

            var physical = PathHelper.GetPhysicalPath(path);
            if (!Directory.Exists(physical))
            {
                App.Notification.Notify("フォルダが見つかりません", $"インデックス検索対象: {path}");
                return;
            }

            if (Items.Any(x => string.Equals(x.Path, physical, StringComparison.OrdinalIgnoreCase)))
            {
                NotifyItemsChanged();
                App.Notification.Notify("既に登録されています", $"インデックス検索対象: {Path.GetFileName(physical)}");
                return;
            }

            var newItem = new IndexSearchTargetItemViewModel { Path = physical };
            Items.Add(newItem);
            NotifyItemsChanged();
            RefreshItemsStatus();

            // Manual モード以外では新規追加時にインデックス作成を開始。Manual は「今すぐ更新」でのみ
            if (App.IndexService.CurrentUpdateMode != IndexUpdateMode.Manual)
                _ = App.IndexService.AddDirectoryToIndexAsync(physical, CreateIndexingProgress());

            App.Notification.Notify("検索対象に追加しました", $"インデックス検索: {Path.GetFileName(physical)}");
        }

        [RelayCommand]
        private void PauseIndexing()
        {
            App.IndexService.PauseIndexing();
            App.Notification.Notify("インデックス作成を一時停止しました", "インデックス");
        }

        [RelayCommand]
        private void ResumeIndexing()
        {
            App.IndexService.ResumeIndexing();
            var paths = GetPathsForSave()
                .Where(p => !App.IndexService.IsRootLocked(p))
                .ToArray();
            App.IndexService.TriggerUpdateNow(paths, CreateIndexingProgress());
            App.Notification.Notify("インデックス作成を再開しました", "インデックス");
        }

        [RelayCommand]
        private void UpdateNow()
        {
            if (App.IndexService.IsPaused)
            {
                App.Notification.Notify("一時停止中です。「再開」でインデックス作成を再開してください", "インデックス");
                return;
            }
            // IndexService の実際の状態に合わせて UI を再評価してから対象を判定する
            RefreshItemsStatus();
            var paths = GetPathsForSave();
            if (paths.Count == 0)
            {
                App.Notification.Notify("対象フォルダがありません", "インデックス");
                return;
            }
            // まだインデックスされていないフォルダだけを抽出する（ロック済みは除外）
            var pendingPaths = paths
                .Where(p => !string.IsNullOrEmpty(p)
                            && Directory.Exists(p)
                            && !App.IndexService.IsRootLocked(p)
                            && !App.IndexService.IsPathIndexed(p)
                            && !App.IndexService.IsRootInProgress(p))
                .ToArray();

            if (pendingPaths.Length == 0)
            {
                App.Notification.Notify("未インデックスのフォルダはありません", "インデックス");
                return;
            }

            App.IndexService.TriggerUpdateNow(pendingPaths, CreateIndexingProgress());
            App.Notification.Notify("未インデックスのフォルダのインデックス更新を開始しました", "インデックス");
        }

        [RelayCommand]
        private void RebuildRoot(IndexSearchTargetItemViewModel? item)
        {
            var target = item ?? SelectedItem;
            if (target == null)
            {
                App.Notification.Notify("対象フォルダが選択されていません", "インデックス");
                return;
            }

            if (App.IndexService.IsPaused)
            {
                App.Notification.Notify("一時停止中です。「再開」でインデックス作成を再開してください", "インデックス");
                return;
            }

            if (!Directory.Exists(target.Path))
            {
                App.Notification.Notify("フォルダが見つかりません", $"インデックス検索: {target.DisplayName}");
                return;
            }

            var result = ZenithDialog.Show(
                "このフォルダのインデックスを一度削除して、最初から再作成します。時間がかかる場合があります。実行しますか？",
                "インデックスの再作成",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Question);

            if (result != ZenithDialogResult.Yes) return;

            App.IndexService.RebuildRoot(target.Path, CreateIndexingProgress());
            RefreshItemsStatus();
            App.Notification.Notify("インデックスの再作成を開始しました", $"インデックス検索: {target.DisplayName}");
        }

        [RelayCommand]
        private async Task UpdateRootDiff(IndexSearchTargetItemViewModel? item)
        {
            var target = item ?? SelectedItem;
            if (target == null)
            {
                App.Notification.Notify("対象フォルダが選択されていません", "インデックス");
                return;
            }

            if (App.IndexService.IsPaused)
            {
                App.Notification.Notify("一時停止中です。「再開」でインデックス作成を再開してください", "インデックス");
                return;
            }

            if (!Directory.Exists(target.Path))
            {
                App.Notification.Notify("フォルダが見つかりません", $"インデックス検索: {target.DisplayName}");
                return;
            }

            await App.IndexService.UpdateDirectoryDiffAsync(target.Path, CreateIndexingProgress());
            RefreshItemsStatus();
            App.Notification.Notify("インデックスの差分更新を開始しました", $"インデックス検索: {target.DisplayName}");
        }

        [RelayCommand]
        private void RequestFullRebuild()
        {
            var paths = GetPathsForSave();
            if (paths.Count == 0)
            {
                App.Notification.Notify("対象フォルダがありません", "インデックス");
                return;
            }
            var result = ZenithDialog.Show(
                "すべてのフォルダを最初から再インデックスします。時間がかかる場合があります。実行しますか？",
                "フル再構築の確認",
                ZenithDialogButton.YesNo,
                ZenithDialogIcon.Question);
            if (result != ZenithDialogResult.Yes) return;

            // ロック済みフォルダを除外
            var targetPaths = paths.Where(p => !App.IndexService.IsRootLocked(p)).ToArray();
            if (targetPaths.Length == 0)
            {
                App.Notification.Notify("すべてのフォルダがロック済みです", "インデックス");
                return;
            }

            var cooldown = _main.AppSettings?.IndexFullRebuildCooldownHours ?? 24;
            var ok = App.IndexService.RequestFullRebuild(targetPaths, CreateIndexingProgress(), cooldown);
            if (!ok)
                App.Notification.Notify($"フル再構築は{cooldown}時間以内に実行済みです。しばらく待ってから再度お試しください", "インデックス");
            else
                App.Notification.Notify("フル再構築を開始しました", "インデックス");
        }

        /// <summary>ロック（アーカイブ）状態を切り替える。ロック済みアイテムは一括更新の対象外。</summary>
        [RelayCommand]
        private void ToggleLock(IndexSearchTargetItemViewModel? item)
        {
            var target = item ?? SelectedItem;
            if (target == null) return;

            var newState = !target.IsLocked;
            App.IndexService.SetLocked(target.Path, newState);
            target.IsLocked = newState;
            // ロック時は待機中/作成中の表示をクリア（キューから実質除外されるため）
            if (newState)
            {
                target.IsWaiting = false;
                target.IsInProgress = false;
            }
            ApplySort();
            App.Notification.Notify(
                newState ? "インデックス更新をロックしました" : "ロックを解除しました",
                $"インデックス検索: {target.DisplayName}");
        }

        /// <summary>アプリ設定ビューのインデックス設定セクションを開く。MainViewModel 経由で呼ぶ。</summary>
        [RelayCommand]
        private void OpenAppSettings()
        {
            _main.RequestSwitchToAppSettingsIndexSection();
        }

        // ── スコープセレクター メソッド ──

        /// <summary>Items と同期して ScopeItems を再構築する。selectedPaths が指定された場合はそのパスのみ選択状態にする。</summary>
        public void RebuildScopeItems(IReadOnlyList<string>? selectedPaths = null)
        {
            // 既存の選択状態を保持（selectedPaths が null かつ既存 ScopeItems がある場合）
            Dictionary<string, bool>? prevSelection = null;
            if (selectedPaths == null && ScopeItems.Count > 0)
            {
                prevSelection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in ScopeItems)
                    prevSelection[s.Path] = s.IsSelected;
            }

            ScopeItems.Clear();
            foreach (var item in Items)
            {
                if (string.IsNullOrWhiteSpace(item.Path)) continue;
                bool selected = true;
                if (selectedPaths != null)
                    selected = selectedPaths.Any(p => string.Equals(p, item.Path, StringComparison.OrdinalIgnoreCase));
                else if (prevSelection != null && prevSelection.TryGetValue(item.Path, out var prev))
                    selected = prev;

                var scope = new IndexSearchScopeItemViewModel
                {
                    Path = item.Path,
                    DisplayName = item.DisplayName,
                    IsLocked = item.IsLocked,
                    DocumentCount = item.DocumentCount,
                    IsSelected = selected,
                };
                scope.PropertyChanged += OnScopeItemPropertyChanged;
                ScopeItems.Add(scope);
            }
            UpdateScopeBadge();
        }

        private void OnScopeItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IndexSearchScopeItemViewModel.IsSelected))
            {
                UpdateScopeBadge();
                ScopeSelectionChanged?.Invoke();
                // 選択変更を即座に永続化
                WindowSettings.SaveIndexSearchScopeOnly(GetScopePathsForSearch()?.ToList());
            }
        }

        private void UpdateScopeBadge()
        {
            int total = ScopeItems.Count;
            int selected = ScopeItems.Count(s => s.IsSelected);
            ScopeBadgeText = total > 0 ? $"{selected}/{total}" : string.Empty;
            IsScopeFiltered = total > 0 && selected < total;
        }

        /// <summary>全選択時は null を返す。部分選択時は選択パスリストを返す。</summary>
        public IReadOnlyList<string>? GetScopePathsForSearch()
        {
            if (ScopeItems.Count == 0) return null;
            int selected = ScopeItems.Count(s => s.IsSelected);
            if (selected == 0 || selected == ScopeItems.Count) return null;
            return ScopeItems.Where(s => s.IsSelected).Select(s => s.Path).ToList();
        }

        [RelayCommand]
        private void SelectAllScope()
        {
            foreach (var s in ScopeItems)
                s.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAllScope()
        {
            foreach (var s in ScopeItems)
                s.IsSelected = false;
        }

        /// <summary>Items の CollectionView にソートを適用/再評価する。</summary>
        private void ApplySort()
        {
            var view = CollectionViewSource.GetDefaultView(Items);
            if (view is ListCollectionView lcv)
            {
                if (lcv.CustomSort is not IndexTargetSortComparer)
                    lcv.CustomSort = IndexTargetSortComparer.Instance;
                else
                    lcv.Refresh();
            }
        }

        /// <summary>インデックス対象の表示順比較: 作成中/待機中 → 未ロック(古い順) → ロック済み(古い順)。</summary>
        private sealed class IndexTargetSortComparer : System.Collections.IComparer
        {
            public static readonly IndexTargetSortComparer Instance = new();
            public int Compare(object? x, object? y)
            {
                if (x is not IndexSearchTargetItemViewModel a ||
                    y is not IndexSearchTargetItemViewModel b) return 0;

                int cmp = a.SortGroup.CompareTo(b.SortGroup);
                if (cmp != 0) return cmp;

                // 同グループ内: 更新日時が古い順（null=未作成 → 最上位）
                var dtA = a.LastIndexedDateTime ?? DateTime.MinValue;
                var dtB = b.LastIndexedDateTime ?? DateTime.MinValue;
                return dtA.CompareTo(dtB);
            }
        }

        /// <summary>選択されたフォルダのインデックス登録内容を A ペインに表示する。</summary>
        [RelayCommand]
        private void ConfirmIndexing(IndexSearchTargetItemViewModel? item)
        {
            var target = item ?? SelectedItem;
            if (target == null)
            {
                App.Notification.Notify("対象フォルダが選択されていません", "インデックス登録確認");
                return;
            }

            // Aペイン（左ペイン）に検索結果タブとして追加し、検索を自動実行（ロード完了時に通知）
            _main.LeftPane.OpenSearchTab(target.Path, "", true);
        }
    }
}
