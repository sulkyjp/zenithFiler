using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZenithFiler
{
    /// <summary>インデックスアイテム別詳細設定ポップアップの ViewModel。</summary>
    public partial class IndexItemSettingsPopupViewModel : ObservableObject
    {
        // ── 表示用（読み取り専用） ──

        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private string _path = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _locationIconKind = "HardDrive";

        // ── ロック ──

        [ObservableProperty] private bool _isLocked;

        // ── スケジュール曜日 ──

        [ObservableProperty] private bool _scheduleMonday = true;
        [ObservableProperty] private bool _scheduleTuesday = true;
        [ObservableProperty] private bool _scheduleWednesday = true;
        [ObservableProperty] private bool _scheduleThursday = true;
        [ObservableProperty] private bool _scheduleFriday = true;
        [ObservableProperty] private bool _scheduleSaturday = true;
        [ObservableProperty] private bool _scheduleSunday = true;

        // ── スケジュール時刻 ──

        /// <summary>時刻選択肢（-1=制限なし, 0〜23）。</summary>
        public List<ScheduleHourOption> HourOptions { get; } = BuildHourOptions();

        [ObservableProperty] private int _selectedHourValue = -1;

        // ── 更新方式 ──

        /// <summary>null=グローバル設定に従う, Incremental, FullRebuild</summary>
        [ObservableProperty] private int _updateModeIndex;  // 0=Global, 1=Incremental, 2=FullRebuild

        private static List<ScheduleHourOption> BuildHourOptions()
        {
            var list = new List<ScheduleHourOption> { new(-1, "制限なし") };
            for (int h = 0; h < 24; h++)
                list.Add(new(h, $"{h}:00"));
            return list;
        }

        /// <summary>IndexSearchTargetItemViewModel + 既存 DTO から初期値を読み込む。</summary>
        public void LoadFrom(IndexSearchTargetItemViewModel item, IndexItemSettingsDto? dto)
        {
            DisplayName = item.DisplayName;
            Path = item.Path;
            LocationIconKind = item.LocationIconKind;
            IsLocked = item.IsLocked;

            // 状態テキスト
            var countText = item.DocumentCount > 0 ? $"{item.DocumentCount:N0} 件" : string.Empty;
            var dateText = item.LastIndexedDateTime is DateTime dt ? $"最終更新: {dt:yyyy/MM/dd HH:mm}" : string.Empty;
            StatusText = string.Join(" | ", new[] { countText, dateText }.Where(s => !string.IsNullOrEmpty(s)));

            // スケジュール
            if (dto?.ScheduleDays != null)
            {
                ScheduleMonday = dto.ScheduleDays.Contains(DayOfWeek.Monday);
                ScheduleTuesday = dto.ScheduleDays.Contains(DayOfWeek.Tuesday);
                ScheduleWednesday = dto.ScheduleDays.Contains(DayOfWeek.Wednesday);
                ScheduleThursday = dto.ScheduleDays.Contains(DayOfWeek.Thursday);
                ScheduleFriday = dto.ScheduleDays.Contains(DayOfWeek.Friday);
                ScheduleSaturday = dto.ScheduleDays.Contains(DayOfWeek.Saturday);
                ScheduleSunday = dto.ScheduleDays.Contains(DayOfWeek.Sunday);
            }
            else
            {
                // null = 毎日 → 全 true
                ScheduleMonday = ScheduleTuesday = ScheduleWednesday = ScheduleThursday =
                    ScheduleFriday = ScheduleSaturday = ScheduleSunday = true;
            }

            SelectedHourValue = dto?.ScheduleHour ?? -1;

            // 更新方式
            UpdateModeIndex = dto?.UpdateMode switch
            {
                IndexItemUpdateMode.Incremental => 1,
                IndexItemUpdateMode.FullRebuild => 2,
                _ => 0
            };
        }

        /// <summary>現在の値を DTO として返す。</summary>
        public IndexItemSettingsDto ToDto()
        {
            var days = new List<DayOfWeek>();
            if (ScheduleMonday) days.Add(DayOfWeek.Monday);
            if (ScheduleTuesday) days.Add(DayOfWeek.Tuesday);
            if (ScheduleWednesday) days.Add(DayOfWeek.Wednesday);
            if (ScheduleThursday) days.Add(DayOfWeek.Thursday);
            if (ScheduleFriday) days.Add(DayOfWeek.Friday);
            if (ScheduleSaturday) days.Add(DayOfWeek.Saturday);
            if (ScheduleSunday) days.Add(DayOfWeek.Sunday);

            // 全曜日選択 → null に正規化
            List<DayOfWeek>? scheduleDays = days.Count == 7 ? null : days;

            int? scheduleHour = SelectedHourValue >= 0 ? SelectedHourValue : null;

            IndexItemUpdateMode? updateMode = UpdateModeIndex switch
            {
                1 => IndexItemUpdateMode.Incremental,
                2 => IndexItemUpdateMode.FullRebuild,
                _ => null
            };

            return new IndexItemSettingsDto
            {
                Path = Path,
                ScheduleDays = scheduleDays,
                ScheduleHour = scheduleHour,
                UpdateMode = updateMode
            };
        }

        /// <summary>item に結果を書き戻す。</summary>
        public void ApplyTo(IndexSearchTargetItemViewModel item)
        {
            item.IsLocked = IsLocked;
            App.IndexService.SetLocked(item.Path, IsLocked);
            if (IsLocked)
            {
                item.IsWaiting = false;
                item.IsInProgress = false;
            }

            var dto = ToDto();
            item.ScheduleDays = dto.ScheduleDays;
            item.ScheduleHour = dto.ScheduleHour;
            item.UpdateMode = dto.UpdateMode;
            item.NotifyAllScheduleProperties();
        }

        [RelayCommand]
        private void ResetSchedule()
        {
            ScheduleMonday = ScheduleTuesday = ScheduleWednesday = ScheduleThursday =
                ScheduleFriday = ScheduleSaturday = ScheduleSunday = true;
            SelectedHourValue = -1;
        }
    }

    public record ScheduleHourOption(int Value, string Display);
}
