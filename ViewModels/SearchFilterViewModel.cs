using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZenithFiler.ViewModels
{
    public enum SizeFilterMode { None, Small, Medium, Large, Huge, Custom }
    public enum DateFilterMode { None, Today, Last7Days, Last30Days, Custom }

    public partial class SearchFilterViewModel : ObservableObject
    {
        internal bool _isLoading;

        // ── サイズ (テキスト入力方式) ──
        [ObservableProperty] private string _minSizeText = "";
        [ObservableProperty] private string _maxSizeText = "";

        // ── 日付 (テキスト入力方式) ──
        [ObservableProperty] private DateFilterMode _dateFilter = DateFilterMode.None;
        [ObservableProperty] private string _startDateText = "";
        [ObservableProperty] private string _endDateText = "";

        // ── 旧プロパティ (後方互換・永続化用) ──
        public DateTime? CustomStartDate => ParsedStartDate;
        public DateTime? CustomEndDate => ParsedEndDate;

        // ── 算出プロパティ (サイズ) ──
        public bool IsSizeFilterActive => !string.IsNullOrWhiteSpace(MinSizeText) || !string.IsNullOrWhiteSpace(MaxSizeText);
        public string MinSizePreview => FormatPreview(MinSizeText);
        public string MaxSizePreview => FormatPreview(MaxSizeText);
        public bool HasMinSizeError => !string.IsNullOrWhiteSpace(MinSizeText) && !TryParseSizeInput(MinSizeText, out _);
        public bool HasMaxSizeError => !string.IsNullOrWhiteSpace(MaxSizeText) && !TryParseSizeInput(MaxSizeText, out _);

        /// <summary>Min と Max の両方が有効な正の値で、Min &gt; Max のとき true。</summary>
        public bool HasSizeRangeError
        {
            get
            {
                if (!TryParseSizeInput(MinSizeText, out long min) || min <= 0) return false;
                if (!TryParseSizeInput(MaxSizeText, out long max) || max <= 0) return false;
                return min > max;
            }
        }

        // ── 永続化互換プロパティ (SaveSearchFilterOnly / MainWindow Closing で使用) ──
        public SizeFilterMode SizeFilter => IsSizeFilterActive ? SizeFilterMode.Custom : SizeFilterMode.None;
        public long CustomMinSizeBytes => TryParseSizeInput(MinSizeText, out var v) ? v : 0;
        public long CustomMaxSizeBytes => TryParseSizeInput(MaxSizeText, out var v) ? v : 0;

        // ── 算出プロパティ (日付) ──
        public bool IsDateFilterActive => DateFilter != DateFilterMode.None
            || !string.IsNullOrWhiteSpace(StartDateText)
            || !string.IsNullOrWhiteSpace(EndDateText);
        public string StartDatePreview => FormatDatePreview(StartDateText);
        public string EndDatePreview => FormatDatePreview(EndDateText);
        public bool HasStartDateError => !string.IsNullOrWhiteSpace(StartDateText) && !TryParseDateInput(StartDateText, out _);
        public bool HasEndDateError => !string.IsNullOrWhiteSpace(EndDateText) && !TryParseDateInput(EndDateText, out _);

        /// <summary>開始日が終了日を超えている場合 true。</summary>
        public bool HasDateRangeError
        {
            get
            {
                if (!TryParseDateInput(StartDateText, out DateTime startDt) || startDt == default) return false;
                if (!TryParseDateInput(EndDateText, out DateTime endDt) || endDt == default) return false;
                return startDt > endDt;
            }
        }

        /// <summary>パースされた開始日を返す（Calendar 表示月の同期用）。</summary>
        public DateTime? ParsedStartDate => TryParseDateInput(StartDateText, out var d) && d != default ? d : null;
        /// <summary>パースされた終了日を返す（Calendar 表示月の同期用）。</summary>
        public DateTime? ParsedEndDate => TryParseDateInput(EndDateText, out var d) && d != default ? d : null;

        // ── イベント ──
        public event Action? FilterChanged;

        // ── partial OnChanged → FilterChanged 発火 ──
        partial void OnMinSizeTextChanged(string value) { NotifySizeChanged(); }
        partial void OnMaxSizeTextChanged(string value) { NotifySizeChanged(); }
        partial void OnDateFilterChanged(DateFilterMode value) { NotifyDateChanged(); }
        partial void OnStartDateTextChanged(string value) { NotifyDateChanged(); }
        partial void OnEndDateTextChanged(string value) { NotifyDateChanged(); }

        private void NotifySizeChanged()
        {
            OnPropertyChanged(nameof(IsSizeFilterActive));
            OnPropertyChanged(nameof(MinSizePreview));
            OnPropertyChanged(nameof(MaxSizePreview));
            OnPropertyChanged(nameof(HasMinSizeError));
            OnPropertyChanged(nameof(HasMaxSizeError));
            OnPropertyChanged(nameof(HasSizeRangeError));
            OnPropertyChanged(nameof(SizeFilter));
            OnPropertyChanged(nameof(CustomMinSizeBytes));
            OnPropertyChanged(nameof(CustomMaxSizeBytes));
            if (_isLoading) return;
            FilterChanged?.Invoke();
            SaveToSettings();
        }

        private void NotifyDateChanged()
        {
            OnPropertyChanged(nameof(IsDateFilterActive));
            OnPropertyChanged(nameof(StartDatePreview));
            OnPropertyChanged(nameof(EndDatePreview));
            OnPropertyChanged(nameof(HasStartDateError));
            OnPropertyChanged(nameof(HasEndDateError));
            OnPropertyChanged(nameof(HasDateRangeError));
            OnPropertyChanged(nameof(ParsedStartDate));
            OnPropertyChanged(nameof(ParsedEndDate));
            if (_isLoading) return;
            // テキスト入力がある場合は Custom モードに自動切替
            if (!string.IsNullOrWhiteSpace(StartDateText) || !string.IsNullOrWhiteSpace(EndDateText))
            {
                if (DateFilter != DateFilterMode.Custom)
                    _dateFilter = DateFilterMode.Custom; // backing field 直接代入で再帰回避
            }
            FilterChanged?.Invoke();
            SaveToSettings();
        }

        // ── サイズ範囲取得 (null = フィルタなし / 整合性エラー時も null) ──
        public (long min, long max)? GetSizeRange()
        {
            TryParseSizeInput(MinSizeText, out long minBytes);
            TryParseSizeInput(MaxSizeText, out long maxBytes);
            if (minBytes <= 0 && maxBytes <= 0) return null;
            long effectiveMin = Math.Max(0, minBytes);
            long effectiveMax = maxBytes > 0 ? maxBytes : long.MaxValue;
            if (effectiveMin > effectiveMax) return null; // Min > Max → フィルタ無効
            return (effectiveMin, effectiveMax);
        }

        // ── 日付範囲取得 (null = フィルタなし) ──
        public (DateTime start, DateTime end)? GetDateRange() => DateFilter switch
        {
            DateFilterMode.Today      => (DateTime.Today, DateTime.Now),
            DateFilterMode.Last7Days  => (DateTime.Today.AddDays(-7), DateTime.Now),
            DateFilterMode.Last30Days => (DateTime.Today.AddDays(-30), DateTime.Now),
            DateFilterMode.Custom     => GetCustomDateRange(),
            _ => (!string.IsNullOrWhiteSpace(StartDateText) || !string.IsNullOrWhiteSpace(EndDateText))
                 ? GetCustomDateRange() : null
        };

        private (DateTime start, DateTime end)? GetCustomDateRange()
        {
            TryParseDateInput(StartDateText, out DateTime startDt);
            TryParseDateInput(EndDateText, out DateTime endDt);
            DateTime effectiveStart = !string.IsNullOrWhiteSpace(StartDateText) && startDt != default ? startDt : DateTime.MinValue;
            DateTime effectiveEnd = !string.IsNullOrWhiteSpace(EndDateText) && endDt != default ? endDt.Date.AddDays(1) : DateTime.MaxValue;
            if (effectiveStart > effectiveEnd) return null; // 整合性エラー
            return (effectiveStart, effectiveEnd);
        }

        // ── ICollectionView 用フィルタ判定 ──
        public bool MatchesFilter(FileItem fi)
        {
            var sizeRange = GetSizeRange();
            if (sizeRange != null && !fi.IsDirectory)
            {
                if (fi.Size < sizeRange.Value.min || fi.Size > sizeRange.Value.max)
                    return false;
            }
            var dateRange = GetDateRange();
            if (dateRange != null)
            {
                if (fi.LastModified < dateRange.Value.start || fi.LastModified > dateRange.Value.end)
                    return false;
            }
            return true;
        }

        /// <summary>サイズ・日付フィルタをすべてリセットする。</summary>
        [RelayCommand]
        private void ResetAllFilters()
        {
            bool hadSize = IsSizeFilterActive;
            bool hadDate = IsDateFilterActive;

            _isLoading = true; // 個別通知・保存を抑制して最後に一括通知
            MinSizeText = "";
            MaxSizeText = "";
            _dateFilter = DateFilterMode.None; // backing field 直接代入で中間通知を回避
            StartDateText = "";
            EndDateText = "";
            _isLoading = false;

            NotifyAllFilterProperties();

            if (hadSize || hadDate)
            {
                App.Notification.Notify("検索フィルタをすべてクリアしました", "フィルタクリア");
                _ = App.FileLogger.LogAsync("[SearchFilter] サイズ・日付フィルタを全クリアしました");
            }
        }

        // ── コマンド (サイズ) ──
        [RelayCommand]
        private void ResetSizeFilter()
        {
            bool had = IsSizeFilterActive;
            MinSizeText = "";
            MaxSizeText = "";
            if (had)
            {
                App.Notification.Notify("サイズフィルタをクリアしました", "フィルタクリア");
                _ = App.FileLogger.LogAsync("[SearchFilter] サイズフィルタをクリアしました");
            }
        }

        [RelayCommand]
        private void ApplySizeChip(string? param)
        {
            if (param == null) return;
            var parts = param.Split('|');
            if (parts.Length != 2) return;
            MinSizeText = parts[0];
            MaxSizeText = parts[1];

            string min = FormatPreview(parts[0]);
            string max = FormatPreview(parts[1]);
            string range = !string.IsNullOrEmpty(min) && !string.IsNullOrEmpty(max) ? $"{min} ~ {max}"
                         : !string.IsNullOrEmpty(min) ? $"{min} 以上" : $"{max} 以下";
            App.Notification.Notify($"サイズフィルタを設定: {range}", "サイズフィルタ");
            _ = App.FileLogger.LogAsync($"[SearchFilter] サイズフィルタを設定: {range}");
        }

        // ── コマンド (日付) ──
        [RelayCommand]
        private void ResetDateFilter()
        {
            bool had = IsDateFilterActive;
            DateFilter = DateFilterMode.None;
            StartDateText = "";
            EndDateText = "";
            if (had)
            {
                App.Notification.Notify("日付フィルタをクリアしました", "フィルタクリア");
                _ = App.FileLogger.LogAsync("[SearchFilter] 日付フィルタをクリアしました");
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> DatePresetLabels = new()
        {
            ["Today"] = "今日", ["Yesterday"] = "昨日", ["Last3Days"] = "直近3日",
            ["Last7Days"] = "直近7日", ["Last14Days"] = "直近14日", ["Last30Days"] = "直近30日",
            ["ThisMonth"] = "今月", ["LastMonth"] = "先月", ["ThisYear"] = "今年",
        };

        [RelayCommand]
        private void ApplyDatePreset(string? param)
        {
            var today = DateTime.Today;
            switch (param)
            {
                case "Today":
                    StartDateText = today.ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
                case "Yesterday":
                    var yesterday = today.AddDays(-1);
                    StartDateText = yesterday.ToString("yyyyMMdd");
                    EndDateText = yesterday.ToString("yyyyMMdd");
                    break;
                case "Last3Days":
                    StartDateText = today.AddDays(-2).ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
                case "Last7Days":
                    StartDateText = today.AddDays(-6).ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
                case "Last14Days":
                    StartDateText = today.AddDays(-13).ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
                case "Last30Days":
                    StartDateText = today.AddDays(-29).ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
                case "ThisMonth":
                    StartDateText = new DateTime(today.Year, today.Month, 1).ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
                case "LastMonth":
                    var firstOfLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var lastOfLastMonth = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                    StartDateText = firstOfLastMonth.ToString("yyyyMMdd");
                    EndDateText = lastOfLastMonth.ToString("yyyyMMdd");
                    break;
                case "ThisYear":
                    StartDateText = new DateTime(today.Year, 1, 1).ToString("yyyyMMdd");
                    EndDateText = today.ToString("yyyyMMdd");
                    break;
            }

            if (param != null && DatePresetLabels.TryGetValue(param, out var label))
            {
                App.Notification.Notify($"日付フィルタを設定: {label}", "日付フィルタ");
                _ = App.FileLogger.LogAsync($"[SearchFilter] 日付プリセット「{label}」を適用");
            }
        }

        /// <summary>Calendar の SelectedDate から TextBox を更新する（コードビハインド用）。</summary>
        public void SetStartDateFromCalendar(DateTime date) => StartDateText = date.ToString("yyyyMMdd");
        public void SetEndDateFromCalendar(DateTime date) => EndDateText = date.ToString("yyyyMMdd");

        // ── スマート日付パーサー ──
        public static bool TryParseDateInput(string? input, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(input)) return true; // 空 = 未指定 (valid)
            input = input.Trim();

            // 1. yyyyMMdd (8桁)
            if (input.Length == 8 && DateTime.TryParseExact(input, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
            // 2. MMdd (4桁) → 今年
            if (input.Length == 4 && int.TryParse(input[..2], out int mm) && int.TryParse(input[2..], out int dd)
                && mm >= 1 && mm <= 12 && dd >= 1 && dd <= 31)
            {
                try { date = new DateTime(DateTime.Today.Year, mm, dd); return true; } catch { }
            }
            // 3. yyyy/MM/dd or yyyy-MM-dd or M/d etc.
            string[] formats = { "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d", "M/d", "MM/dd" };
            if (DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                if (date.Year == 1) date = new DateTime(DateTime.Today.Year, date.Month, date.Day);
                return true;
            }
            return false;
        }

        // ── サフィックスパーサー (k=KB, m=MB, g=GB, 未指定=MB) ──
        public static bool TryParseSizeInput(string? input, out long bytes)
        {
            bytes = 0;
            if (string.IsNullOrWhiteSpace(input)) return true;

            input = input.Trim().ToLowerInvariant();
            long multiplier = 1024L * 1024; // default MB

            if (input.EndsWith("k"))
            {
                multiplier = 1024L;
                input = input[..^1];
            }
            else if (input.EndsWith("m"))
            {
                multiplier = 1024L * 1024;
                input = input[..^1];
            }
            else if (input.EndsWith("g"))
            {
                multiplier = 1024L * 1024 * 1024;
                input = input[..^1];
            }

            if (string.IsNullOrWhiteSpace(input)) return false;
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
                return false;

            bytes = (long)(value * multiplier);
            return true;
        }

        // ── プレビュー表示 (サイズ) ──
        private static string FormatPreview(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            if (!TryParseSizeInput(input, out long bytes)) return "";
            if (bytes == 0) return "";
            return "\u2192 " + FormatBytes(bytes);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:0.#} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
        }

        // ── プレビュー表示 (日付) ──
        private static string FormatDatePreview(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            if (!TryParseDateInput(input, out DateTime dt) || dt == default) return "";
            return "\u2192 " + dt.ToString("yyyy/MM/dd");
        }

        // ── バイトからテキスト変換 (設定ロード時の後方互換) ──
        private static string BytesToSizeText(long bytes)
        {
            if (bytes <= 0) return "";
            double gb = bytes / (1024.0 * 1024 * 1024);
            double mb = bytes / (1024.0 * 1024);
            double kb = bytes / 1024.0;

            if (gb >= 1 && Math.Abs(gb - Math.Round(gb, 2)) < 0.001)
                return gb % 1 == 0 ? $"{(long)gb}g" : $"{gb:0.##}g";
            if (mb >= 1 && Math.Abs(mb - Math.Round(mb, 2)) < 0.001)
                return mb % 1 == 0 ? $"{(long)mb}m" : $"{mb:0.##}m";
            if (kb >= 1 && Math.Abs(kb - Math.Round(kb, 2)) < 0.001)
                return kb % 1 == 0 ? $"{(long)kb}k" : $"{kb:0.##}k";
            return bytes.ToString();
        }

        /// <summary>プリセット適用後に呼び出し、全フィルタプロパティの変更を通知して FilterChanged を発火する。</summary>
        internal void NotifyAllFilterProperties()
        {
            OnPropertyChanged(nameof(IsSizeFilterActive));
            OnPropertyChanged(nameof(MinSizePreview));
            OnPropertyChanged(nameof(MaxSizePreview));
            OnPropertyChanged(nameof(HasMinSizeError));
            OnPropertyChanged(nameof(HasMaxSizeError));
            OnPropertyChanged(nameof(HasSizeRangeError));
            OnPropertyChanged(nameof(SizeFilter));
            OnPropertyChanged(nameof(CustomMinSizeBytes));
            OnPropertyChanged(nameof(CustomMaxSizeBytes));
            OnPropertyChanged(nameof(IsDateFilterActive));
            OnPropertyChanged(nameof(StartDatePreview));
            OnPropertyChanged(nameof(EndDatePreview));
            OnPropertyChanged(nameof(HasStartDateError));
            OnPropertyChanged(nameof(HasEndDateError));
            OnPropertyChanged(nameof(HasDateRangeError));
            OnPropertyChanged(nameof(ParsedStartDate));
            OnPropertyChanged(nameof(ParsedEndDate));
            FilterChanged?.Invoke();
            SaveToSettings();
        }

        // ── 永続化 ──
        private void SaveToSettings() { WindowSettings.SaveSearchFilterOnly(this); }

        public void LoadFromSettings(WindowSettings s)
        {
            _isLoading = true;
            try
            {
                // サイズフィルタ復元 (テキスト形式を優先)
                if (!string.IsNullOrEmpty(s.SearchMinSizeText) || !string.IsNullOrEmpty(s.SearchMaxSizeText))
                {
                    MinSizeText = s.SearchMinSizeText ?? "";
                    MaxSizeText = s.SearchMaxSizeText ?? "";
                }
                else if (s.SearchSizeFilter is int sf && Enum.IsDefined(typeof(SizeFilterMode), sf))
                {
                    // 後方互換: 旧プリセット → テキストに変換
                    switch ((SizeFilterMode)sf)
                    {
                        case SizeFilterMode.Small:
                            MaxSizeText = "100k"; break;
                        case SizeFilterMode.Medium:
                            MinSizeText = "100k"; MaxSizeText = "10m"; break;
                        case SizeFilterMode.Large:
                            MinSizeText = "10m"; MaxSizeText = "1g"; break;
                        case SizeFilterMode.Huge:
                            MinSizeText = "1g"; break;
                        case SizeFilterMode.Custom:
                            if (s.SearchCustomMinSize is long minS && minS > 0)
                                MinSizeText = BytesToSizeText(minS);
                            if (s.SearchCustomMaxSize is long maxS && maxS > 0)
                                MaxSizeText = BytesToSizeText(maxS);
                            break;
                    }
                }

                // 日付フィルタ復元 (テキスト形式を優先)
                if (!string.IsNullOrEmpty(s.SearchStartDateText) || !string.IsNullOrEmpty(s.SearchEndDateText))
                {
                    StartDateText = s.SearchStartDateText ?? "";
                    EndDateText = s.SearchEndDateText ?? "";
                    // テキストがあれば Custom モード
                    if (!string.IsNullOrWhiteSpace(StartDateText) || !string.IsNullOrWhiteSpace(EndDateText))
                        _dateFilter = DateFilterMode.Custom;
                }
                else if (s.SearchDateFilter is int df && Enum.IsDefined(typeof(DateFilterMode), df))
                {
                    var mode = (DateFilterMode)df;
                    DateFilter = mode;
                    // 後方互換: 旧 ISO 文字列 → yyyyMMdd テキストに変換
                    if (mode == DateFilterMode.Custom)
                    {
                        if (s.SearchCustomStartDate is string startStr
                            && DateTime.TryParse(startStr, null, DateTimeStyles.RoundtripKind, out var startDt))
                            StartDateText = startDt.ToString("yyyyMMdd");
                        if (s.SearchCustomEndDate is string endStr
                            && DateTime.TryParse(endStr, null, DateTimeStyles.RoundtripKind, out var endDt))
                            EndDateText = endDt.ToString("yyyyMMdd");
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }
        }
    }
}
