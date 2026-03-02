using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZenithFiler.Views;

namespace ZenithFiler.ViewModels
{
    public partial class SearchPresetViewModel : ObservableObject
    {
        private readonly MainViewModel _main;

        public ObservableCollection<SearchPresetDto> Presets { get; } = new();
        public ObservableCollection<SearchPresetDto> FilteredPresets { get; } = new();

        [ObservableProperty]
        private bool _hasPresetsForCurrentMode;

        public SearchPresetViewModel(MainViewModel main)
        {
            _main = main;
        }

        public void LoadFromSettings(WindowSettings settings)
        {
            Presets.Clear();
            foreach (var p in settings.SearchPresets)
                Presets.Add(p);
            _ = App.FileLogger.LogAsync($"[SearchPresets] {settings.SearchPresets.Count} 件のプリセットを読み込みました");
            RefreshFilteredPresets();
        }

        /// <summary>現在の検索モード（通常/インデックス）に合致するプリセットのみを FilteredPresets に反映する。</summary>
        public void RefreshFilteredPresets()
        {
            var tab = _main.ActivePane?.SelectedTab;
            bool isIndex = tab?.IsIndexSearchMode ?? false;

            FilteredPresets.Clear();
            foreach (var p in Presets.Where(p => p.IsIndexSearchMode == isIndex))
                FilteredPresets.Add(p);

            HasPresetsForCurrentMode = FilteredPresets.Count > 0;
        }

        [RelayCommand]
        private void SaveCurrentAsPreset()
        {
            var tab = _main.ActivePane?.SelectedTab;
            if (tab == null) return;

            var name = RenameDialog.ShowDialog("プリセット名を入力", "", "", false);
            if (string.IsNullOrWhiteSpace(name)) return;

            var sf = _main.SearchFilter;
            var dto = new SearchPresetDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                CreatedAt = DateTime.Now.ToString("o"),
                IsIndexSearchMode = tab.IsIndexSearchMode,
                SearchText = tab.SearchText ?? string.Empty,
                MinSizeText = sf.MinSizeText ?? string.Empty,
                MaxSizeText = sf.MaxSizeText ?? string.Empty,
                StartDateText = sf.StartDateText ?? string.Empty,
                EndDateText = sf.EndDateText ?? string.Empty,
                SearchSortProperty = tab.SearchSortProperty ?? "LastModified",
                SearchSortDirection = tab.SearchSortDirection,
                FileTypeFilterEnabled = tab.FileTypeFilterItems.Select(f => f.IsEnabled).ToList(),
            };

            if (tab.IsIndexSearchMode)
            {
                dto.ScopePaths = _main.IndexSearchSettings.GetScopePathsForSearch()?.ToList();
            }

            var summaryPanel = BuildPresetSummaryPanel(dto);
            var result = ZenithDialog.Show(
                "以下の条件でプリセットを保存しますか？",
                $"プリセット「{name}」",
                ZenithDialogButton.OKCancel,
                ZenithDialogIcon.Question,
                summaryPanel);

            if (result != ZenithDialogResult.OK) return;

            Presets.Add(dto);
            SaveToSettings();
            RefreshFilteredPresets();
            App.Notification.Notify($"プリセット「{name}」を保存しました", "検索プリセット保存");
            _ = App.FileLogger.LogAsync($"[SearchPresets] プリセット「{name}」を保存しました");
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task ApplyPreset(SearchPresetDto? preset)
        {
            if (preset == null) return;
            var tab = _main.ActivePane?.SelectedTab;
            if (tab == null) return;

            var sf = _main.SearchFilter;

            // サイズ・日付フィルタを一括設定（中間 FilterChanged を抑制）
            sf._isLoading = true;
            sf.MinSizeText = preset.MinSizeText ?? string.Empty;
            sf.MaxSizeText = preset.MaxSizeText ?? string.Empty;
            sf.StartDateText = preset.StartDateText ?? string.Empty;
            sf.EndDateText = preset.EndDateText ?? string.Empty;
            sf._isLoading = false;

            // 全フィルタプロパティを一括通知 → アイコンのアンダーライン状態を即座に同期
            sf.NotifyAllFilterProperties();

            // 検索モード
            tab.IsIndexSearchMode = preset.IsIndexSearchMode;

            // ファイルタイプフィルタ
            if (preset.FileTypeFilterEnabled != null && preset.FileTypeFilterEnabled.Count == tab.FileTypeFilterItems.Count)
            {
                for (int i = 0; i < preset.FileTypeFilterEnabled.Count; i++)
                    tab.FileTypeFilterItems[i].IsEnabled = preset.FileTypeFilterEnabled[i];
            }

            // インデックスモード時のスコープ
            if (preset.IsIndexSearchMode && preset.ScopePaths != null)
            {
                _main.IndexSearchSettings.RebuildScopeItems(preset.ScopePaths);
            }

            // ソート
            tab.SearchSortProperty = preset.SearchSortProperty ?? "LastModified";
            tab.SearchSortDirection = preset.SearchSortDirection;

            // 検索テキスト設定 + 検索実行
            tab.SearchText = preset.SearchText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(preset.SearchText))
            {
                await tab.ExecuteSearch(preset.SearchText);
            }

            App.Notification.Notify($"プリセット「{preset.Name}」を適用しました", "検索プリセット適用");
            _ = App.FileLogger.LogAsync($"[SearchPresets] プリセット「{preset.Name}」を適用しました");
        }

        [RelayCommand]
        private void DeletePreset(SearchPresetDto? preset)
        {
            if (preset == null) return;
            Presets.Remove(preset);
            SaveToSettings();
            RefreshFilteredPresets();
            App.Notification.Notify($"プリセット「{preset.Name}」を削除しました", "検索プリセット削除");
            _ = App.FileLogger.LogAsync($"[SearchPresets] プリセット「{preset.Name}」を削除しました");
        }

        [RelayCommand]
        private void ResetAllSettings()
        {
            var tab = _main.ActivePane?.SelectedTab;
            if (tab == null) return;

            var sf = _main.SearchFilter;

            // サイズ・日付フィルタを一括リセット（中間 FilterChanged を抑制）
            sf._isLoading = true;
            sf.MinSizeText = string.Empty;
            sf.MaxSizeText = string.Empty;
            sf.DateFilter = DateFilterMode.None;
            sf.StartDateText = string.Empty;
            sf.EndDateText = string.Empty;
            sf._isLoading = false;

            // 全フィルタプロパティを一括通知 → アイコンのアンダーライン状態を即座に同期
            sf.NotifyAllFilterProperties();

            // ファイルタイプフィルタ: 全て有効に戻す
            foreach (var item in tab.FileTypeFilterItems)
                item.IsEnabled = true;

            // ソート: デフォルト値に戻す
            tab.SearchSortProperty = "LastModified";
            tab.SearchSortDirection = System.ComponentModel.ListSortDirection.Descending;

            // 検索テキストをクリア
            tab.SearchText = string.Empty;

            App.Notification.Notify("全検索条件をリセットしました", "検索条件リセット");
            _ = App.FileLogger.LogAsync("[SearchPresets] 全検索条件をリセットしました");
        }

        private static readonly SolidColorBrush LabelBrush = CreateFrozenBrush(0x88, 0x88, 0x88);
        private static readonly SolidColorBrush ValueBrush = CreateFrozenBrush(0x1A, 0x1A, 0x1A);

        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static FrameworkElement BuildPresetSummaryPanel(SearchPresetDto dto)
        {
            var panel = new StackPanel();

            // モード（常時表示）
            AddRow(panel, "モード", dto.IsIndexSearchMode ? "インデックス検索" : "通常検索");

            // キーワード
            if (dto.HasKeyword)
                AddRow(panel, "キーワード", dto.SearchText);

            // サイズ
            if (dto.HasSize)
                AddRow(panel, "サイズ", dto.DisplaySize);

            // 日付
            if (dto.HasDate)
                AddRow(panel, "日付", dto.DisplayDate);

            // 拡張子フィルタ
            if (dto.HasFilters)
                AddRow(panel, "拡張子", dto.DisplayFilters);

            // スコープ
            if (dto.HasScope)
                AddRow(panel, "スコープ", dto.DisplayScope);

            // ソート（常時表示）
            AddRow(panel, "ソート", dto.DisplaySort);

            return panel;
        }

        private static void AddRow(StackPanel panel, string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = LabelBrush,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(labelBlock, 0);

            var valueBlock = new TextBlock
            {
                Text = value,
                Foreground = ValueBrush,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(valueBlock, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
            panel.Children.Add(grid);
        }

        private void SaveToSettings()
        {
            WindowSettings.SaveSearchPresetsOnly(Presets.ToList());
        }
    }
}
