using System;
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
            if (IsPreviewActive) await CancelPreviewInternalAsync();

            _rollbackLeft      = CapturePaneState(_main.LeftPane);
            _rollbackRight     = CapturePaneState(_main.RightPane);
            _rollbackPaneCount = _main.PaneCount;
            IsPreviewActive    = true;
            PreviewingSet      = set;

            _main.PaneCount = set.PaneCount;
            await RestorePaneAsync(_main.LeftPane,  set.LeftPane);
            await RestorePaneAsync(_main.RightPane, set.RightPane);

            App.Notification.Notify($"「{set.Name}」をプレビュー中",
                                    $"[WorkingSet] Preview: '{set.Name}'");
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
        private async Task CancelPreviewAsync() => await CancelPreviewInternalAsync();

        internal async Task CancelPreviewInternalAsync()
        {
            if (!IsPreviewActive) return;
            IsPreviewActive = false;
            PreviewingSet   = null;
            _main.PaneCount = _rollbackPaneCount;
            if (_rollbackLeft  != null) await RestorePaneAsync(_main.LeftPane,  _rollbackLeft);
            if (_rollbackRight != null) await RestorePaneAsync(_main.RightPane, _rollbackRight);
            _rollbackLeft = _rollbackRight = null;
            App.Notification.Notify("元の状態に戻しました", "[WorkingSet] Rollback executed");
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
