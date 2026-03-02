using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZenithFiler
{
    public partial class ProjectSetsViewModel : ObservableObject
    {
        private readonly MainViewModel _main;

        public ObservableCollection<WorkingSetDto> Items { get; } = new();

        [ObservableProperty]
        private WorkingSetDto? _previewingSet;

        [ObservableProperty]
        private bool _isPreviewActive;

        private PaneStateDto? _rollbackLeft;
        private PaneStateDto? _rollbackRight;
        private int           _rollbackPaneCount;
        private bool          _isSwitching;

        public ProjectSetsViewModel(MainViewModel main) => _main = main;

        public void LoadFromSettings(WindowSettings settings)
        {
            Items.Clear();
            foreach (var dto in settings.WorkingSets)
                Items.Add(dto);
        }

        [RelayCommand]
        private void SaveCurrentState()
        {
            string? name = InputBox.ShowDialog("ワーキングセット名を入力", $"セット {Items.Count + 1}");
            if (string.IsNullOrWhiteSpace(name)) return;

            var dto = new WorkingSetDto
            {
                Name      = name.Trim(),
                CreatedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                PaneCount = _main.PaneCount,
                LeftPane  = CapturePaneState(_main.LeftPane),
                RightPane = CapturePaneState(_main.RightPane)
            };
            Items.Add(dto);
            WindowSettings.SaveWorkingSetsOnly(Items.ToList());
            App.Notification.Notify($"ワーキングセット「{dto.Name}」を保存しました",
                                    $"[WorkingSet] Save: '{dto.Name}'");
        }

        [RelayCommand]
        private async Task StartPreviewAsync(WorkingSetDto set)
        {
            // 多重発火ガード
            if (_isSwitching) return;
            _isSwitching = true;

            try
            {
                // 初回プレビュー時のみロールバック状態をキャプチャ
                // （プレビュー間切り替え時は元のロールバックポイントを保持）
                if (!IsPreviewActive)
                {
                    _rollbackLeft      = CapturePaneState(_main.LeftPane);
                    _rollbackRight     = CapturePaneState(_main.RightPane);
                    _rollbackPaneCount = _main.PaneCount;
                }
                IsPreviewActive = true;
                PreviewingSet   = set;

                // ── Phase 0: 事前バリデーション（フェード前に I/O 完了） ──
                var (leftPaths, leftMsgs)   = await ValidateAndResolvePathsAsync(set.LeftPane,  isRightPane: false);
                var (rightPaths, rightMsgs) = await ValidateAndResolvePathsAsync(set.RightPane, isRightPane: true);

                try
                {
                    // ── Phase 1: フェードアウト（150ms、完了を await） ──
                    if (_main.AnimatePaneFadeOut != null)
                        await _main.AnimatePaneFadeOut();

                    // ── Phase 2: 両ペインを一括スワップ（Opacity=0 で完全不可視） ──
                    _main.PaneCount = set.PaneCount;
                    await RestorePaneWithResolvedPathsAsync(_main.LeftPane,  set.LeftPane,  leftPaths);
                    await RestorePaneWithResolvedPathsAsync(_main.RightPane, set.RightPane, rightPaths);

                    // ── Phase 3: フェードイン（180ms、完了を await） ──
                    if (_main.AnimatePaneFadeIn != null)
                        await _main.AnimatePaneFadeIn();

                    // フォールバック通知
                    var allMessages = leftMsgs.Concat(rightMsgs).ToList();
                    if (allMessages.Count > 0)
                    {
                        App.Notification.Notify($"「{set.Name}」をプレビュー中（一部パスを置換）",
                                                $"[WorkingSet] Preview: '{set.Name}' — {string.Join("; ", allMessages)}");
                    }
                    else
                    {
                        App.Notification.Notify($"「{set.Name}」をプレビュー中",
                                                $"[WorkingSet] Preview: '{set.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    // フェードアウト中に失敗した場合は即座に復帰
                    if (_main.AnimatePaneFadeIn != null)
                        try { await _main.AnimatePaneFadeIn(); } catch { }

                    try
                    {
                        _main.PaneCount = _rollbackPaneCount;
                        if (_rollbackLeft  != null) await RestorePaneAsync(_main.LeftPane,  _rollbackLeft);
                        if (_rollbackRight != null) await RestorePaneAsync(_main.RightPane, _rollbackRight);
                    }
                    catch { /* ロールバックも失敗 → 状態リセットのみ */ }

                    IsPreviewActive = false;
                    PreviewingSet   = null;
                    _rollbackLeft = _rollbackRight = null;

                    App.Notification.Notify($"ワーキングセットの切り替えに失敗しました: {ex.Message}",
                                            $"[WorkingSet] Error: {ex.Message}");
                }
            }
            finally
            {
                _isSwitching = false;
            }
        }

        [RelayCommand]
        private void Apply()
        {
            if (!IsPreviewActive || PreviewingSet == null) return;
            string name = PreviewingSet.Name;
            _rollbackLeft   = _rollbackRight = null;
            IsPreviewActive = false;
            PreviewingSet   = null;
            App.Notification.Notify($"ワーキングセット「{name}」を適用しました",
                                    $"[WorkingSet] Apply: '{name}'");
        }

        [RelayCommand]
        private async Task CancelPreviewAsync() => await CancelPreviewInternalAsync(animate: true);

        internal async Task CancelPreviewInternalAsync(bool animate = true)
        {
            if (!IsPreviewActive) return;

            if (animate)
            {
                if (_isSwitching) return;
                _isSwitching = true;
            }

            try
            {
                IsPreviewActive = false;
                PreviewingSet   = null;

                try
                {
                    // ── Phase 1: フェードアウト（完了を await） ──
                    if (animate && _main.AnimatePaneFadeOut != null)
                        await _main.AnimatePaneFadeOut();

                    // ── Phase 2: 一括ロールバック（Opacity=0 で不可視） ──
                    _main.PaneCount = _rollbackPaneCount;
                    if (_rollbackLeft  != null) await RestorePaneAsync(_main.LeftPane,  _rollbackLeft);
                    if (_rollbackRight != null) await RestorePaneAsync(_main.RightPane, _rollbackRight);
                }
                catch { /* ロールバック失敗時も状態クリーンアップ */ }

                _rollbackLeft = _rollbackRight = null;

                // ── Phase 3: フェードイン（完了を await） ──
                if (animate && _main.AnimatePaneFadeIn != null)
                    await _main.AnimatePaneFadeIn();

                App.Notification.Notify("元の状態に戻しました", "[WorkingSet] Rollback executed");
            }
            finally
            {
                if (animate) _isSwitching = false;
            }
        }

        [RelayCommand]
        private void DeleteSet(WorkingSetDto set)
        {
            if (!Items.Remove(set)) return;
            WindowSettings.SaveWorkingSetsOnly(Items.ToList());
            App.Notification.Notify($"ワーキングセット「{set.Name}」を削除しました",
                                    $"[WorkingSet] Delete: '{set.Name}'");
        }

        [RelayCommand]
        private void RenameSet(WorkingSetDto set)
        {
            string? newName = InputBox.ShowDialog("名前の変更", set.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == set.Name) return;
            string oldName = set.Name;
            set.Name = newName.Trim();
            // WorkingSetDto は ObservableObject でないためリスト再挿入でビュー更新
            int idx = Items.IndexOf(set);
            if (idx >= 0) { Items.RemoveAt(idx); Items.Insert(idx, set); }
            WindowSettings.SaveWorkingSetsOnly(Items.ToList());
            App.Notification.Notify($"「{oldName}」→「{set.Name}」に変更しました",
                                    $"[WorkingSet] Rename: '{set.Name}'");
        }

        // ── ヘルパー ────────────────────────────────────────────────────────────

        /// <summary>
        /// 全タブパスを事前バリデーションし、アクセス不能パスをフォールバックに置換する。
        /// </summary>
        private static async Task<(List<string> resolvedPaths, List<string> fallbackMessages)> ValidateAndResolvePathsAsync(
            PaneStateDto state, bool isRightPane)
        {
            var resolved = new List<string>(state.Tabs.Count);
            var messages = new List<string>();

            for (int i = 0; i < state.Tabs.Count; i++)
            {
                bool useDownloads = (i == 0) && isRightPane;
                string fallback = useDownloads
                    ? PathHelper.GetDownloadsPath()
                    : PathHelper.GetInitialPath(Environment.SpecialFolder.Desktop);

                string raw = state.Tabs[i].Path;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    resolved.Add(fallback);
                    continue;
                }

                string path = PathHelper.GetPhysicalPath(raw);
                if (string.IsNullOrWhiteSpace(path))
                {
                    resolved.Add(fallback);
                    messages.Add($"タブ{i + 1}: パスの解決に失敗 → フォールバック");
                    continue;
                }

                // 仮想パス（PC / UNC ルート）は存在チェック不要
                if (PathHelper.IsPCPath(path) || PathHelper.IsUncRoot(path))
                {
                    resolved.Add(path);
                    continue;
                }

                if (PathHelper.IsDriveRootOnly(path) || !await PathHelper.DirectoryExistsSafeAsync(path))
                {
                    resolved.Add(fallback);
                    messages.Add($"タブ{i + 1}: 「{raw}」にアクセスできません → フォールバック");
                    continue;
                }

                resolved.Add(path);
            }

            return (resolved, messages);
        }

        /// <summary>
        /// 事前検証済みパスで RestoreTabsAsync を呼ぶ。
        /// </summary>
        private static async Task RestorePaneWithResolvedPathsAsync(
            FilePaneViewModel pane, PaneStateDto state, List<string> resolvedPaths)
        {
            var ps = new PaneSettings
            {
                TabPaths            = resolvedPaths,
                TabLockStates       = state.Tabs.Select(t => t.IsLocked).ToList(),
                SelectedTabIndex    = Math.Clamp(state.SelectedTabIndex, 0,
                                          Math.Max(0, state.Tabs.Count - 1)),
                FileViewMode        = state.FileViewMode,
                SortProperty        = state.SortProperty,
                SortDirection       = state.SortDirection,
                IsGroupFoldersFirst = state.IsGroupFoldersFirst
            };
            await pane.RestoreTabsAsync(ps);
        }

        private static PaneStateDto CapturePaneState(FilePaneViewModel pane)
        {
            int idx = pane.SelectedTab != null ? pane.Tabs.IndexOf(pane.SelectedTab) : 0;
            return new PaneStateDto
            {
                Tabs = pane.Tabs.Select(t => new TabStateDto
                {
                    Path     = t.CurrentPath ?? string.Empty,
                    IsLocked = t.IsLocked
                }).ToList(),
                SelectedTabIndex    = Math.Max(0, idx),
                FileViewMode        = pane.SelectedTab?.FileViewMode ?? FileViewMode.Details,
                SortProperty        = pane.SelectedTab?.SortProperty ?? "LastModified",
                SortDirection       = pane.SelectedTab?.SortDirection ?? ListSortDirection.Descending,
                IsGroupFoldersFirst = pane.SelectedTab?.IsGroupFoldersFirst ?? true
            };
        }

        private static async Task RestorePaneAsync(FilePaneViewModel pane, PaneStateDto state)
        {
            var ps = new PaneSettings
            {
                TabPaths            = state.Tabs.Select(t => t.Path).ToList(),
                TabLockStates       = state.Tabs.Select(t => t.IsLocked).ToList(),
                SelectedTabIndex    = Math.Clamp(state.SelectedTabIndex, 0,
                                          Math.Max(0, state.Tabs.Count - 1)),
                FileViewMode        = state.FileViewMode,
                SortProperty        = state.SortProperty,
                SortDirection       = state.SortDirection,
                IsGroupFoldersFirst = state.IsGroupFoldersFirst
            };
            await pane.RestoreTabsAsync(ps);
        }
    }
}
