using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZenithFiler
{
    /// <summary>インデックス検索対象フォルダの1件を表す ViewModel。</summary>
    public partial class IndexSearchTargetItemViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(LocationIconKind))]
        private string _path = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusKind))]
        [NotifyPropertyChangedFor(nameof(SortGroup))]
        private bool _isInProgress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusKind))]
        private bool _isIndexed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(CountBadgeText))]
        private int _documentCount;

        /// <summary>セマフォ待ちでインデックス作成がキュー済みかどうか。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusKind))]
        [NotifyPropertyChangedFor(nameof(SortGroup))]
        private bool _isWaiting;

        /// <summary>最後にインデックスが正常完了した日時。永続化される。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LastIndexedText))]
        [NotifyPropertyChangedFor(nameof(CompactDateText))]
        [NotifyPropertyChangedFor(nameof(IsStale))]
        private DateTime? _lastIndexedDateTime;

        /// <summary>スキャン中のサブフォルダ名（InProgress 時のみ有効）。</summary>
        [ObservableProperty]
        private string _currentScanFolder = string.Empty;

        /// <summary>登録成功時のハイライト演出用フラグ。true で SuccessHighlight アニメーションを1回発火。</summary>
        [ObservableProperty]
        private bool _isSuccessHighlighted;

        // テレメトリ表示用（ポップアップ表示中のみ更新）
        [ObservableProperty] private string _telemetryScanText = "---";
        [ObservableProperty] private string _telemetrySpeedText = "0 items/sec";
        [ObservableProperty] private string _telemetryDbText = "0 records";
        [ObservableProperty] private string _telemetryThreadText = "0/1";
        [ObservableProperty] private string _telemetryElapsedText = "0:00";
        [ObservableProperty] private bool _isTelemetryPopupOpen;

        /// <summary>スループット計算用の前回サンプル値。</summary>
        internal int LastTelemetrySampleCount { get; set; }

        /// <summary>
        /// ロック（アーカイブ）状態。true の場合、一括更新・定期更新の対象外となる。
        /// 手動での個別更新（右クリック→差分更新/再作成）は許容される。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStale))]
        [NotifyPropertyChangedFor(nameof(LockToolTipText))]
        [NotifyPropertyChangedFor(nameof(StatusKind))]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(SortGroup))]
        private bool _isLocked;

        /// <summary>一覧表示用の短い名前（フォルダ名）。</summary>
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Path)) return string.Empty;
                var name = System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
                return string.IsNullOrEmpty(name) ? Path : name;
            }
        }

        /// <summary>状態と件数を含む表示用テキスト（例: 作成中 (8,800件) / 待機中 / 1,234 件 / アーカイブ済）。</summary>
        public string StatusText
        {
            get
            {
                if (IsInProgress) return DocumentCount > 0 ? $"作成中... ({DocumentCount:N0}件)" : "作成中...";
                if (IsWaiting) return "待機中";
                if (IsLocked) return DocumentCount > 0 ? $"{DocumentCount:N0} 件" : "アーカイブ済";
                if (IsIndexed) return DocumentCount > 0 ? $"{DocumentCount:N0} 件" : "完了";
                return "未作成";
            }
        }

        /// <summary>UI の状態分岐に使用するステータス種別。</summary>
        public string StatusKind
        {
            get
            {
                if (IsInProgress) return "InProgress";
                if (IsWaiting) return "Waiting";
                if (IsLocked) return "Locked";
                if (IsIndexed) return "Indexed";
                return "NotCreated";
            }
        }

        /// <summary>パスから判定した場所アイコン（Local→HardDrive, Server→Network, Box→Archive, SPO→Cloud）。</summary>
        public string LocationIconKind => PathHelper.DetermineSourceType(Path) switch
        {
            SourceType.Server => "Network",
            SourceType.Box => "Archive",
            SourceType.SPO => "Cloud",
            _ => "HardDrive"
        };

        /// <summary>4カラムレイアウト Col 2 用の日時テキスト（例: 2026/02/27 08:05）。</summary>
        public string CompactDateText
            => LastIndexedDateTime?.ToString("yyyy/MM/dd HH:mm") ?? string.Empty;

        /// <summary>4カラムレイアウト Col 3 用の件数テキスト（例: (12,345)）。</summary>
        public string CountBadgeText
            => DocumentCount > 0 ? $"{DocumentCount:N0}件" : string.Empty;

        /// <summary>最終インデックス日時から24時間以上経過しているか（鮮度アラート）。ロック済みは常に false。</summary>
        public bool IsStale
            => !IsLocked && LastIndexedDateTime.HasValue && (DateTime.Now - LastIndexedDateTime.Value).TotalHours >= 24;

        /// <summary>ロック状態用ツールチップテキスト。</summary>
        public string LockToolTipText
            => IsLocked ? "このインデックスはロック（アーカイブ）されており、一括更新の対象外です" : string.Empty;

        /// <summary>ソートグループ: 0=作成中/待機中, 1=未ロック, 2=ロック済み。</summary>
        public int SortGroup
        {
            get
            {
                if (IsInProgress || IsWaiting) return 0;
                if (!IsLocked) return 1;
                return 2;
            }
        }

        /// <summary>最終更新日時の表示用テキスト（例: 最終更新: 2026/02/27 08:05）。</summary>
        public string LastIndexedText
        {
            get
            {
                if (LastIndexedDateTime is not DateTime dt) return string.Empty;
                return $"最終更新: {dt:yyyy/MM/dd HH:mm}";
            }
        }

        // ─── アイテム別スケジュール ───

        /// <summary>スケジュール対象の曜日。null の場合は毎日。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleDisplayText))]
        [NotifyPropertyChangedFor(nameof(HasCustomSchedule))]
        private System.Collections.Generic.List<DayOfWeek>? _scheduleDays;

        /// <summary>スケジュール対象の時刻（0-23）。null の場合はグローバル間隔に従う。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScheduleDisplayText))]
        [NotifyPropertyChangedFor(nameof(HasCustomSchedule))]
        private int? _scheduleHour;

        /// <summary>Per-Item 更新方式。null=グローバル設定に従う。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCustomSchedule))]
        [NotifyPropertyChangedFor(nameof(UpdateModeDisplayText))]
        private IndexItemUpdateMode? _updateMode;

        private static readonly string[] _dayNames = { "日", "月", "火", "水", "木", "金", "土" };

        /// <summary>スケジュール設定の表示用テキスト。</summary>
        public string ScheduleDisplayText
        {
            get
            {
                if (ScheduleDays == null && !ScheduleHour.HasValue)
                    return "グローバル設定に従う";
                var days = ScheduleDays != null
                    ? string.Join("", ScheduleDays.OrderBy(d => d).Select(d => _dayNames[(int)d]))
                    : "毎日";
                var time = ScheduleHour.HasValue ? $" {ScheduleHour.Value}:00" : "";
                return $"{days}{time}";
            }
        }

        /// <summary>カスタム設定が存在するか（スケジュール・更新方式のいずれか）。インジケーター表示用。</summary>
        public bool HasCustomSchedule => ScheduleDays != null || ScheduleHour.HasValue || UpdateMode.HasValue;

        /// <summary>更新方式の表示テキスト。サマリーテーブル用。</summary>
        public string UpdateModeDisplayText => UpdateMode switch
        {
            IndexItemUpdateMode.Incremental => "差分更新",
            IndexItemUpdateMode.FullRebuild => "フル再作成",
            _ => "グローバル"
        };

        /// <summary>スケジュール関連のすべてのプロパティ変更を通知する。外部からも呼び出し可能。</summary>
        internal void NotifyAllScheduleProperties()
        {
            OnPropertyChanged(nameof(HasCustomSchedule));
            OnPropertyChanged(nameof(ScheduleDisplayText));
        }
    }
}
