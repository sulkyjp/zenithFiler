using System;

namespace ZenithFiler
{
    /// <summary>
    /// 検索履歴の一覧表示用アイテム（キーワード＋通常/インデックス識別＋条件カラム）。
    /// </summary>
    public class SearchHistoryItem
    {
        public string Keyword { get; init; } = string.Empty;
        public bool IsIndexSearch { get; init; }
        public string PresetName { get; init; } = string.Empty;
        public string MinSizeText { get; init; } = string.Empty;
        public string MaxSizeText { get; init; } = string.Empty;
        public string StartDateText { get; init; } = string.Empty;
        public string EndDateText { get; init; } = string.Empty;

        /// <summary>いずれかの条件が設定されていれば true。</summary>
        public bool HasConditions =>
            !string.IsNullOrEmpty(PresetName)
            || !string.IsNullOrEmpty(MinSizeText)
            || !string.IsNullOrEmpty(MaxSizeText)
            || !string.IsNullOrEmpty(StartDateText)
            || !string.IsNullOrEmpty(EndDateText);

        /// <summary>プリセットが適用されていれば true。</summary>
        public bool HasPreset => !string.IsNullOrEmpty(PresetName);

        /// <summary>サイズ条件があれば true。</summary>
        public bool HasSize => !string.IsNullOrEmpty(MinSizeText) || !string.IsNullOrEmpty(MaxSizeText);

        /// <summary>日付条件があれば true。</summary>
        public bool HasDate => !string.IsNullOrEmpty(StartDateText) || !string.IsNullOrEmpty(EndDateText);

        /// <summary>カラム用: サイズ条件の略記値 (例: &gt;1MB, &lt;100KB, 1MB~10MB)。</summary>
        public string SizeSummary
        {
            get
            {
                bool hasMin = !string.IsNullOrEmpty(MinSizeText);
                bool hasMax = !string.IsNullOrEmpty(MaxSizeText);
                if (!hasMin && !hasMax) return string.Empty;

                string min = hasMin ? FormatSizeShort(MinSizeText) : "";
                string max = hasMax ? FormatSizeShort(MaxSizeText) : "";

                if (hasMin && hasMax) return $"{min}~{max}";
                if (hasMin) return $">{min}";
                return $"<{max}";
            }
        }

        /// <summary>カラム用: 日付条件の略記値 (例: 3/1~3/1, 3/1~, ~3/1)。</summary>
        public string DateSummary
        {
            get
            {
                bool hasStart = !string.IsNullOrEmpty(StartDateText);
                bool hasEnd = !string.IsNullOrEmpty(EndDateText);
                if (!hasStart && !hasEnd) return string.Empty;

                string start = hasStart ? FormatDateShort(StartDateText) : "";
                string end = hasEnd ? FormatDateShort(EndDateText) : "";

                if (hasStart && hasEnd) return $"{start}~{end}";
                if (hasStart) return $"{start}~";
                return $"~{end}";
            }
        }

        /// <summary>サイズ短縮フォーマット。スペースなしで 1.5MB, 500KB 等。</summary>
        private static string FormatSizeShort(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (ViewModels.SearchFilterViewModel.TryParseSizeInput(text, out long bytes) && bytes > 0)
            {
                if (bytes < 1024) return $"{bytes}B";
                if (bytes < 1024L * 1024) return $"{bytes / 1024.0:0.#}KB";
                if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#}MB";
                return $"{bytes / (1024.0 * 1024 * 1024):0.##}GB";
            }
            return text;
        }

        /// <summary>日付短縮フォーマット。M/d 形式（年省略）。</summary>
        private static string FormatDateShort(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (ViewModels.SearchFilterViewModel.TryParseDateInput(text, out var dt) && dt != default)
                return dt.ToString("M/d");
            return text;
        }
    }
}
