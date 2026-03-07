using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ZenithFiler
{
    /// <summary>お気に入りアイテムのシリアライズ用 DTO。settings.json に保存される。</summary>
    public class FavoriteItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Path { get; set; }
        /// <summary>概要説明。既存データにない場合は null として安全に処理する。</summary>
        public string? Description { get; set; }
        public bool IsExpanded { get; set; }
        /// <summary>場所の種類（Local / Server / Box / SPO）。仮想フォルダなどパスが null の場合に使用。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SourceType LocationType { get; set; } = SourceType.Local;
        public List<FavoriteItemDto> Children { get; set; } = new();
    }

    /// <summary>検索プリセットのシリアライズ用 DTO。settings.json に保存される。</summary>
    public class SearchPresetDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public bool IsIndexSearchMode { get; set; }
        public string SearchText { get; set; } = string.Empty;
        public string MinSizeText { get; set; } = string.Empty;
        public string MaxSizeText { get; set; } = string.Empty;
        public string StartDateText { get; set; } = string.Empty;
        public string EndDateText { get; set; } = string.Empty;
        public string SearchSortProperty { get; set; } = "LastModified";
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ListSortDirection SearchSortDirection { get; set; } = ListSortDirection.Descending;
        public List<bool>? FileTypeFilterEnabled { get; set; }
        public List<string>? ScopePaths { get; set; }

        // ── プレビュー表示用 (JsonIgnore) ──

        private static readonly string[] _fileTypeLabels =
            { "フォルダ", "Excel", "Word", "PPT", "PDF", "TXT", "EXE", "BAT", "JSON", "画像", "その他" };

        [JsonIgnore]
        public bool HasKeyword => !string.IsNullOrWhiteSpace(SearchText);

        [JsonIgnore]
        public bool HasSize => !string.IsNullOrWhiteSpace(MinSizeText) || !string.IsNullOrWhiteSpace(MaxSizeText);

        [JsonIgnore]
        public bool HasDate => !string.IsNullOrWhiteSpace(StartDateText) || !string.IsNullOrWhiteSpace(EndDateText);

        [JsonIgnore]
        public bool HasFilters
        {
            get
            {
                if (FileTypeFilterEnabled == null || FileTypeFilterEnabled.Count != _fileTypeLabels.Length) return false;
                return FileTypeFilterEnabled.Any(f => !f);
            }
        }

        [JsonIgnore]
        public bool HasScope => IsIndexSearchMode && ScopePaths != null && ScopePaths.Count > 0;

        [JsonIgnore]
        public string DisplaySize
        {
            get
            {
                bool hasMin = !string.IsNullOrWhiteSpace(MinSizeText);
                bool hasMax = !string.IsNullOrWhiteSpace(MaxSizeText);
                if (!hasMin && !hasMax) return string.Empty;
                string min = FormatSizeText(MinSizeText);
                string max = FormatSizeText(MaxSizeText);
                if (hasMin && hasMax) return $"{min} ~ {max}";
                if (hasMin) return $"{min} 以上";
                return $"{max} 以下";
            }
        }

        [JsonIgnore]
        public string DisplayDate
        {
            get
            {
                bool hasStart = !string.IsNullOrWhiteSpace(StartDateText);
                bool hasEnd = !string.IsNullOrWhiteSpace(EndDateText);
                if (!hasStart && !hasEnd) return string.Empty;
                string start = FormatDateText(StartDateText);
                string end = FormatDateText(EndDateText);
                if (hasStart && hasEnd) return $"{start} ~ {end}";
                if (hasStart) return $"{start} 以降";
                return $"{end} 以前";
            }
        }

        [JsonIgnore]
        public string DisplayFilters
        {
            get
            {
                if (FileTypeFilterEnabled == null || FileTypeFilterEnabled.Count != _fileTypeLabels.Length) return string.Empty;
                var enabled = new System.Collections.Generic.List<string>();
                for (int i = 0; i < _fileTypeLabels.Length; i++)
                    if (FileTypeFilterEnabled[i]) enabled.Add(_fileTypeLabels[i]);
                if (enabled.Count == _fileTypeLabels.Length) return string.Empty;
                return string.Join(", ", enabled);
            }
        }

        [JsonIgnore]
        public string DisplayScope
        {
            get
            {
                if (ScopePaths == null || ScopePaths.Count == 0) return string.Empty;
                return string.Join(", ", ScopePaths.Select(p =>
                    Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))));
            }
        }

        [JsonIgnore]
        public string DisplaySort
        {
            get
            {
                var propName = SearchSortProperty switch
                {
                    "Name" => "名前",
                    "LastModified" => "更新日",
                    "Size" => "サイズ",
                    "Extension" => "拡張子",
                    _ => SearchSortProperty
                };
                var dir = SearchSortDirection == ListSortDirection.Ascending ? "↑" : "↓";
                return $"{propName} {dir}";
            }
        }

        internal static string FormatSizeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (ViewModels.SearchFilterViewModel.TryParseSizeInput(text, out long bytes) && bytes > 0)
            {
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024L * 1024) return $"{bytes / 1024.0:0.#} KB";
                if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
                return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
            }
            return text;
        }

        internal static string FormatDateText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (ViewModels.SearchFilterViewModel.TryParseDateInput(text, out var dt) && dt != default)
                return dt.ToString("yyyy/MM/dd");
            return text;
        }
    }

    /// <summary>インデックスのアイテム別スケジュール設定（旧形式、マイグレーション用に残す）。</summary>
    public class IndexScheduleDto
    {
        public string Path { get; set; } = string.Empty;
        public List<DayOfWeek>? ScheduleDays { get; set; }  // null = 毎日
        public int? ScheduleHour { get; set; }               // null = 制限なし（グローバル間隔に従う）
    }

    /// <summary>Per-Item のインデックス更新方式。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IndexItemUpdateMode
    {
        /// <summary>差分更新（増分）。新規・更新ファイルのみ追加。</summary>
        Incremental,
        /// <summary>フル再作成。既存インデックスを削除して再スキャン。</summary>
        FullRebuild
    }

    /// <summary>インデックスのアイテム別詳細設定 DTO。スケジュール・ロック・更新方式を管理。</summary>
    public class IndexItemSettingsDto
    {
        public string Path { get; set; } = string.Empty;
        public List<DayOfWeek>? ScheduleDays { get; set; }  // null = 毎日
        public int? ScheduleHour { get; set; }               // null = 制限なし
        /// <summary>Per-Item 更新方式。null の場合はグローバル設定に従う。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IndexItemUpdateMode? UpdateMode { get; set; }
    }

    /// <summary>インデックスの更新タイミング。負荷を抑えるためデフォルトは Interval。</summary>
    public enum IndexUpdateMode
    {
        /// <summary>変更を検知して差分更新。負荷はやや高め。</summary>
        Auto,
        /// <summary>一定間隔でまとめて更新。推奨。</summary>
        Interval,
        /// <summary>手動の「今すぐ更新」のみ。</summary>
        Manual
    }

    /// <summary>インデックス関連の設定。settings.json の Index セクション。</summary>
    public class IndexSettings
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IndexUpdateMode UpdateMode { get; set; } = IndexUpdateMode.Interval;
        public int UpdateIntervalHours { get; set; } = 2;
        public bool EcoMode { get; set; } = true;
        public int MaxParallelism { get; set; } = 2;
        public bool NetworkLowPriority { get; set; } = true;
        public bool FreshnessAggressive { get; set; } = false;
        public bool FreshnessWarnStale { get; set; } = true;
        public int FullRebuildCooldownHours { get; set; } = 24;

        /// <summary>CPU 負荷が低い時のみインデックスを更新するか。</summary>
        public bool IdleOnlyExecution { get; set; } = false;
        /// <summary>アイドル判定の CPU 使用率閾値（%）。</summary>
        public int IdleCpuThreshold { get; set; } = 20;

        /// <summary>ベストプラクティス（負荷控えめ）のデフォルトを返す。</summary>
        public static IndexSettings CreateDefaults() => new();
    }

    public class PaneSettings
    {
        public bool IsGroupFoldersFirst { get; set; } = true;
        public bool IsAdaptiveColumnsEnabled { get; set; } = true;
        public string SortProperty { get; set; } = "Name";
        public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;
        public string CurrentPath { get; set; } = string.Empty;
        public List<string> TabPaths { get; set; } = new();
        public int SelectedTabIndex { get; set; } = 0;
        public bool IsPathEditMode { get; set; } = false;
        public List<bool> TabLockStates { get; set; } = new();
        /// <summary>ホームボタンで移動するフォルダパス。空の場合はデスクトップ(A)／ダウンロード(B)にフォールバック。</summary>
        public string HomePath { get; set; } = string.Empty;
        /// <summary>ファイル一覧の表示モード（詳細／大・中・小アイコン）。次回起動時に復元。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FileViewMode FileViewMode { get; set; } = FileViewMode.Details;
    }

    public class WindowSettings
    {
        public double Width { get; set; } = 1400;
        public double Height { get; set; } = 850;
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public WindowState State { get; set; } = WindowState.Normal;
        public double SidebarWidth { get; set; } = 260;
        public bool IsSidebarVisible { get; set; } = true;
        public SidebarViewMode SidebarMode { get; set; } = SidebarViewMode.Favorites;
        public int PaneCount { get; set; } = 1;
        public bool IsAlwaysOnTop { get; set; } = false;
        public bool IsFavoritesLocked { get; set; } = false;

        /// <summary>ツリービュー内でのフォルダ移動・リネーム・削除を禁止するロック。次回起動時に復元。</summary>
        public bool IsTreeViewLocked { get; set; } = false;

        /// <summary>ナビペインの幅を任意変更不可にするロック。お気に入りビュー時のみ有効。ツリー/履歴の自動拡張はロック時も行う。</summary>
        public bool IsNavWidthLocked { get; set; } = false;

        /// <summary>削除時の確認メッセージを表示するか（お気に入り削除時）。settings.json に保存される。</summary>
        public bool ConfirmDeleteFavorites { get; set; } = true;

        /// <summary>お気に入り検索のヒット条件（名前・概要 または フルパス・概要）。次回起動時に復元。</summary>
        public FavoritesSearchMode FavoritesSearchMode { get; set; } = FavoritesSearchMode.NameAndDescription;

        /// <summary>お気に入り一覧。settings.json に保存され、アプリ移行時にバックアップで引き継げる。</summary>
        public List<FavoriteItemDto> Favorites { get; set; } = new();

        /// <summary>ワーキングセット一覧。settings.json に保存される。</summary>
        public List<WorkingSetDto> WorkingSets { get; set; } = new();

        /// <summary>インデックス検索の対象フォルダパス一覧。settings.json に保存される。</summary>
        public List<string> IndexSearchTargetPaths { get; set; } = new();

        /// <summary>インデックス更新ロック済みフォルダパス一覧。settings.json に保存される。</summary>
        public List<string> IndexSearchLockedPaths { get; set; } = new();

        /// <summary>インデックス検索スコープ（検索対象に含めるフォルダ）。null は全選択。</summary>
        public List<string>? IndexSearchScopePaths { get; set; }

        /// <summary>検索実行時の表示先・挙動（同一ペイン新規タブ／同一ペイン現在タブ即時／反対ペイン新規タブ）。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchBehavior SearchBehavior { get; set; } = SearchBehavior.SamePaneNewTab;

        /// <summary>検索結果一覧でパス列をクリックした際の表示先（同一タブ／同一ペイン新規タブ／反対ペイン新規タブ）。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchResultPathBehavior SearchResultPathBehavior { get; set; } = SearchResultPathBehavior.SamePaneNewTab;

        /// <summary>ファイル一覧の右クリック時に表示するコンテキストメニューの種類（アプリ独自／エクスプローラ互換）。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ContextMenuMode ContextMenuMode { get; set; } = ContextMenuMode.Zenith;

        /// <summary>検索結果フィルターバーの有効状態（フォルダ, Excel, Word, PPT, PDF, TXT, EXE, BAT, JSON, 画像, その他 の順）。null の場合は全項目有効。</summary>
        public List<bool>? SearchResultFileTypeFilterEnabled { get; set; }

        /// <summary>検索プリセット一覧。settings.json に保存される。</summary>
        public List<SearchPresetDto> SearchPresets { get; set; } = new();

        /// <summary>検索サイズフィルタモード（SizeFilterMode enum の int 値）。</summary>
        public int? SearchSizeFilter { get; set; }
        public long? SearchCustomMinSize { get; set; }
        public long? SearchCustomMaxSize { get; set; }
        public string? SearchMinSizeText { get; set; }
        public string? SearchMaxSizeText { get; set; }

        /// <summary>検索日付フィルタモード（DateFilterMode enum の int 値）。</summary>
        public int? SearchDateFilter { get; set; }
        public string? SearchCustomStartDate { get; set; }
        public string? SearchCustomEndDate { get; set; }
        public string? SearchStartDateText { get; set; }
        public string? SearchEndDateText { get; set; }

        /// <summary>検索実行時に自動的に1画面モードに切り替えるか。</summary>
        public bool AutoSwitchToSinglePaneOnSearch { get; set; } = false;

        /// <summary>マイクロアニメーション（ホバーフェード・タブ切替トランジション等）の有効/無効。</summary>
        public bool EnableMicroAnimations { get; set; } = true;

        // ─── Display ───
        /// <summary>ファイル一覧の行高（px）。24=コンパクト, 32=標準, 40=ゆったり。</summary>
        public int ListRowHeight { get; set; } = 32;

        /// <summary>ファイル一覧ビューのホバーアニメーション（フェード・スケール等）を有効にするか。</summary>
        public bool EnableListAnimations { get; set; } = true;

        // ─── General 追加 ───
        /// <summary>シングルクリックでフォルダを開く（false=ダブルクリック）。</summary>
        public bool SingleClickOpenFolder { get; set; } = false;

        /// <summary>ファイル削除時に確認ダイアログを表示するか。</summary>
        public bool ConfirmDelete { get; set; } = true;

        /// <summary>起動時に前回のタブ構成を復元するか（false=ホームフォルダのみ）。</summary>
        public bool RestoreTabsOnStartup { get; set; } = true;

        /// <summary>通知トーストの表示時間（ms）。</summary>
        public int NotificationDurationMs { get; set; } = 3000;

        // ─── Search デフォルト ───
        /// <summary>新規タブ作成時のフォルダ先頭表示デフォルト。</summary>
        public bool DefaultGroupFoldersFirst { get; set; } = true;

        /// <summary>新規タブ作成時のデフォルトソートプロパティ名。</summary>
        public string DefaultSortProperty { get; set; } = "Name";

        /// <summary>新規タブ作成時のデフォルトソート方向。</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ListSortDirection DefaultSortDirection { get; set; } = ListSortDirection.Ascending;

        // ─── General 追加 (v0.20) ───
        /// <summary>タイトルバーにアクティブペインのパスを表示するか。</summary>
        public bool ShowPathInTitleBar { get; set; } = true;

        /// <summary>ファイル名に拡張子を表示するか。</summary>
        public bool ShowFileExtensions { get; set; } = true;

        /// <summary>隠しファイル・フォルダを表示するか。</summary>
        public bool ShowHiddenFiles { get; set; } = false;

        /// <summary>ダウンロードフォルダに移動した際、更新日時の新しい順に自動ソートするか。</summary>
        public bool DownloadsSortByDate { get; set; } = false;

        /// <summary>ウィンドウを閉じた際にタスクトレイに常駐するか。</summary>
        public bool ResidentMode { get; set; } = false;

        /// <summary>起動時にステータスバーのウェルカムアニメーションを表示するか。</summary>
        public bool ShowWelcomeAnimation { get; set; } = true;

        /// <summary>フォルダ読み込み時のスキャンバー（流れるプログレスバー）を表示するか。</summary>
        public bool ShowScanBar { get; set; } = true;

        // ─── Effects カテゴリ別 ON/OFF (v0.25.0) ───

        /// <summary>A. 起動・全体: ウェルカムアニメーション、ローディングオーバーレイ。</summary>
        public bool ShowStartupEffects { get; set; } = true;

        /// <summary>B. GlowBar: 進捗バー全体（フェード・補間・グロー）。</summary>
        public bool ShowGlowBar { get; set; } = true;

        /// <summary>D. タブ操作: タブインジケータースライド、タブコンテンツフェード、タブD&amp;Dマーカー。</summary>
        public bool ShowTabEffects { get; set; } = true;

        /// <summary>E. ペイン・トランジション: ペインフェード、Control Deck展開/閉じ、サイドバー幅アニメーション。</summary>
        public bool ShowPaneTransitions { get; set; } = true;

        /// <summary>F. テーマ: テーマ切替オーバーレイ、テーマトースト通知。</summary>
        public bool ShowThemeEffects { get; set; } = true;

        /// <summary>H. クイックプレビュー: プレビュー開く/閉じるアニメーション。</summary>
        public bool ShowPreviewEffects { get; set; } = true;

        /// <summary>I. ファイル一覧: アイコンビュー情報オーバーレイ、お気に入りボタンホバー、検索アイコンスケール、コンテキストメニューアニメ。</summary>
        public bool ShowListEffects { get; set; } = true;

        /// <summary>L. ドラッグ＆ドロップ: ドラッグアドーナー（D&amp;D中のカーソル追従ラベル）。</summary>
        public bool ShowDragEffects { get; set; } = true;

        // ─── Auto Update ───
        /// <summary>GitHub Releases からの自動更新を有効にするか。</summary>
        public bool AutoUpdate { get; set; } = true;

        /// <summary>最終更新チェック日時（ISO 8601）。</summary>
        public string? LastUpdateCheck { get; set; }

        /// <summary>ユーザーがスキップしたバージョン。</summary>
        public string? SkippedVersion { get; set; }

        /// <summary>ユーザーが同意した EULA のバージョン。未同意時は空文字。</summary>
        public string EulaAcceptedVersion { get; set; } = "";

        /// <summary>起動時テーマ適用トーストを表示するか。</summary>
        public bool ShowStartupToast { get; set; } = true;

        /// <summary>適用中のテーマ名（themes/ フォルダ内の拡張子なしファイル名）。</summary>
        public string ThemeName { get; set; } = "standard";

        /// <summary>テーマ適用モード ("Personalize" / "Auto" / "Pane")。</summary>
        public string CurrentThemeMode { get; set; } = "Personalize";

        /// <summary>自動選択サブモード ("All" / "Category")。</summary>
        public string AutoSelectSubMode { get; set; } = "All";

        /// <summary>カテゴリランダム時の対象カテゴリ名。</summary>
        public string? SelectedCategory { get; set; }

        /// <summary>パーソナライズモード時の最終適用テーマ名。</summary>
        public string SavedThemeName { get; set; } = "standard";

        /// <summary>ナビペインに適用するテーマ名。</summary>
        public string NavPaneThemeName { get; set; } = string.Empty;

        /// <summary>Aペインに適用するテーマ名。</summary>
        public string APaneThemeName { get; set; } = string.Empty;

        /// <summary>Bペインに適用するテーマ名。</summary>
        public string BPaneThemeName { get; set; } = string.Empty;

        private static int _listRowHeight = 32;
        public static int ListRowHeightValue => _listRowHeight;

        private static bool _singleClickOpenFolder = false;
        public static bool SingleClickOpenFolderEnabled => _singleClickOpenFolder;

        private static bool _confirmDelete = true;
        public static bool ConfirmDeleteEnabled => _confirmDelete;

        private static int _notificationDurationMs = 3000;
        public static int NotificationDurationMsValue => _notificationDurationMs;

        private static bool _defaultGroupFoldersFirst = true;
        public static bool DefaultGroupFoldersFirstValue => _defaultGroupFoldersFirst;

        private static string _defaultSortProperty = "Name";
        public static string DefaultSortPropertyValue => _defaultSortProperty;

        private static ListSortDirection _defaultSortDirection = ListSortDirection.Ascending;
        public static ListSortDirection DefaultSortDirectionValue => _defaultSortDirection;

        private static bool _showPathInTitleBar = true;
        public static bool ShowPathInTitleBarEnabled => _showPathInTitleBar;

        private static bool _showFileExtensions = true;
        public static bool ShowFileExtensionsEnabled => _showFileExtensions;

        private static bool _showHiddenFiles = false;
        public static bool ShowHiddenFilesEnabled => _showHiddenFiles;

        private static bool _downloadsSortByDate = false;
        public static bool DownloadsSortByDateValue => _downloadsSortByDate;

        private static bool _residentMode = false;
        public static bool ResidentModeEnabled => _residentMode;

        private static bool _showScanBar = true;
        public static bool ShowScanBarEnabled => _showScanBar;

        // ─── Effects カテゴリ別 static フラグ (v0.25.0) ───
        private static bool _showStartupEffects = true;
        public static bool ShowStartupEffectsEnabled => _showStartupEffects;

        private static bool _showGlowBar = true;
        public static bool ShowGlowBarEnabled => _showGlowBar;

        private static bool _showTabEffects = true;
        public static bool ShowTabEffectsEnabled => _showTabEffects;

        private static bool _showPaneTransitions = true;
        public static bool ShowPaneTransitionsEnabled => _showPaneTransitions;

        private static bool _showThemeEffects = true;
        public static bool ShowThemeEffectsEnabled => _showThemeEffects;

        private static bool _showPreviewEffects = true;
        public static bool ShowPreviewEffectsEnabled => _showPreviewEffects;

        private static bool _showListEffects = true;
        public static bool ShowListEffectsEnabled => _showListEffects;

        private static bool _showDragEffects = true;
        public static bool ShowDragEffectsEnabled => _showDragEffects;

        private static bool _autoUpdate = true;
        public static bool AutoUpdateEnabled => _autoUpdate;

        /// <summary>インスタンスプロパティの値を static フラグへ反映する。Load() 完了後に呼び出す。</summary>
        internal void ApplyStaticFlags()
        {
            // ── マイグレーション: 旧設定が false で新設定がデフォルト(true)のままなら伝播 ──
            if (!EnableMicroAnimations)
            {
                if (ShowTabEffects) ShowTabEffects = false;
                if (ShowPaneTransitions) ShowPaneTransitions = false;
                if (ShowPreviewEffects) ShowPreviewEffects = false;
            }
            if (!EnableListAnimations && ShowListEffects) ShowListEffects = false;
            if (!ShowWelcomeAnimation && ShowStartupEffects) ShowStartupEffects = false;

            _listRowHeight = ListRowHeight;
            _singleClickOpenFolder = SingleClickOpenFolder;
            _confirmDelete = ConfirmDelete;
            _notificationDurationMs = NotificationDurationMs;
            _defaultGroupFoldersFirst = DefaultGroupFoldersFirst;
            _defaultSortProperty = DefaultSortProperty;
            _defaultSortDirection = DefaultSortDirection;
            _showPathInTitleBar = ShowPathInTitleBar;
            _showFileExtensions = ShowFileExtensions;
            _showHiddenFiles = ShowHiddenFiles;
            _downloadsSortByDate = DownloadsSortByDate;
            _residentMode = ResidentMode;
            _showScanBar = ShowScanBar;
            _autoUpdate = AutoUpdate;

            // Effects カテゴリ別 static フラグ (v0.25.0)
            _showStartupEffects = ShowStartupEffects;
            _showGlowBar = ShowGlowBar;
            _showTabEffects = ShowTabEffects;
            _showPaneTransitions = ShowPaneTransitions;
            _showThemeEffects = ShowThemeEffects;
            _showPreviewEffects = ShowPreviewEffects;
            _showListEffects = ShowListEffects;
            _showDragEffects = ShowDragEffects;
        }

        // ─── Runtime setter メソッド（ViewModel から呼び出し）───
        internal static void SetListRowHeightRuntime(int v) => _listRowHeight = v;
        internal static void SetSingleClickOpenFolderRuntime(bool v) => _singleClickOpenFolder = v;
        internal static void SetConfirmDeleteRuntime(bool v) => _confirmDelete = v;
        internal static void SetNotificationDurationRuntime(int v) => _notificationDurationMs = v;
        internal static void SetDefaultGroupFoldersFirstRuntime(bool v) => _defaultGroupFoldersFirst = v;
        internal static void SetDefaultSortPropertyRuntime(string v) => _defaultSortProperty = v;
        internal static void SetDefaultSortDirectionRuntime(ListSortDirection v) => _defaultSortDirection = v;
        internal static void SetShowPathInTitleBarRuntime(bool v) => _showPathInTitleBar = v;
        internal static void SetShowFileExtensionsRuntime(bool v) => _showFileExtensions = v;
        internal static void SetShowHiddenFilesRuntime(bool v) => _showHiddenFiles = v;
        internal static void SetDownloadsSortByDateRuntime(bool v) => _downloadsSortByDate = v;
        internal static void SetResidentModeRuntime(bool v) => _residentMode = v;
        internal static void SetAutoUpdateRuntime(bool v) => _autoUpdate = v;

        // Effects カテゴリ別 Runtime setter (v0.25.0)
        internal static void SetEffectCategoryRuntime(string propertyName, bool v)
        {
            switch (propertyName)
            {
                case nameof(ShowStartupEffects): _showStartupEffects = v; break;
                case nameof(ShowGlowBar): _showGlowBar = v; break;
                case nameof(ShowScanBar): _showScanBar = v; break;
                case nameof(ShowTabEffects): _showTabEffects = v; break;
                case nameof(ShowPaneTransitions): _showPaneTransitions = v; break;
                case nameof(ShowThemeEffects): _showThemeEffects = v; break;
                case nameof(ShowPreviewEffects): _showPreviewEffects = v; break;
                case nameof(ShowListEffects): _showListEffects = v; break;
                case nameof(ShowDragEffects): _showDragEffects = v; break;
            }
        }

        /// <summary>インデックスのアイテム別スケジュール（旧形式）。マイグレーション用。新規保存には IndexItemSettings を使用。</summary>
        public List<IndexScheduleDto>? IndexSchedules { get; set; }

        /// <summary>インデックスのアイテム別詳細設定。スケジュール・更新方式を管理。null の場合は全パスがグローバル設定に従う。</summary>
        public List<IndexItemSettingsDto>? IndexItemSettings { get; set; }

        /// <summary>カスタムキーバインド設定。null の場合はすべてデフォルト。</summary>
        public List<Models.KeyBindingDto>? CustomKeyBindings { get; set; }

        /// <summary>設定スキーマのバージョン。マイグレーション判定に使用。</summary>
        public int SettingsVersion { get; set; } = 1;

        /// <summary>インデックス関連の設定。null の場合は CreateDefaults() で補完。</summary>
        [JsonPropertyName("Index")]
        public IndexSettings? IndexSettings { get; set; }

        public PaneSettings LeftPane { get; set; } = new();
        public PaneSettings RightPane { get; set; } = new();

        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static readonly string TmpFilePath = FilePath + ".tmp";
        private static readonly string BakFilePath = FilePath + ".bak";

        private static JsonSerializerOptions CreateJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 日本語などを \uXXXX にエスケープせずそのまま保存
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>ファイルを読まずにデフォルト設定のインスタンスを返す。起動時UIブロック回避用。</summary>
        public static WindowSettings CreateDefault()
        {
            var s = new WindowSettings();
            s.EnsureIndexSettingsDefaults();
            return s;
        }

        public static WindowSettings Load()
        {
            // 1. settings.json を試行
            var result = TryLoadFrom(FilePath);
            if (result != null) return result;

            // 2. settings.json が破損/不在 → .bak からフォールバック
            result = TryLoadFrom(BakFilePath);
            if (result != null)
            {
                _ = App.FileLogger.LogAsync("[Settings] settings.json が破損していたため settings.json.bak から復旧しました");
                return result;
            }

            // 3. 両方ダメ → デフォルト
            var s = new WindowSettings();
            s.EnsureIndexSettingsDefaults();
            s.ApplyStaticFlags();
            return s;
        }

        /// <summary>指定パスから設定を読み込む。ファイル不在・0バイト・JSON 不正の場合は null を返す。</summary>
        private static WindowSettings? TryLoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var info = new FileInfo(path);
                if (info.Length == 0) return null;

                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<WindowSettings>(json, CreateJsonOptions());
                if (s == null) return null;
                s.MigrateIndexSchedulesToItemSettings();
                s.EnsureIndexSettingsDefaults();
                s.ApplyStaticFlags();
                return s;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>旧 IndexSchedules → 新 IndexItemSettings へのマイグレーション。</summary>
        private void MigrateIndexSchedulesToItemSettings()
        {
            if (IndexItemSettings != null || IndexSchedules == null || IndexSchedules.Count == 0) return;
            IndexItemSettings = IndexSchedules.Select(s => new IndexItemSettingsDto
            {
                Path = s.Path,
                ScheduleDays = s.ScheduleDays,
                ScheduleHour = s.ScheduleHour,
                UpdateMode = null
            }).ToList();
            IndexSchedules = null; // 旧形式をクリア
        }

        /// <summary>IndexSettings が null の場合にデフォルトを補完する。</summary>
        private void EnsureIndexSettingsDefaults()
        {
            if (IndexSettings == null)
                IndexSettings = IndexSettings.CreateDefaults();
        }

        /// <summary>
        /// アトミック・スワップ方式で settings.json に書き出す。
        /// .tmp に書き込み → .bak を作成 → .tmp を本体に置換。
        /// </summary>
        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, CreateJsonOptions());

                // 1. .tmp に書き出し
                File.WriteAllText(TmpFilePath, json);

                // 2. アトミック・スワップ (.bak を一世代保持)
                if (File.Exists(FilePath))
                {
                    // File.Replace: tmp → 本体, 旧本体 → bak (1操作)
                    File.Replace(TmpFilePath, FilePath, BakFilePath);
                }
                else
                {
                    // 初回保存 (本体がまだない)
                    File.Move(TmpFilePath, FilePath);
                }
            }
            catch (Exception ex)
            {
                // File.Replace 失敗時のフォールバック（.tmp が残っていれば Move で救済）
                try
                {
                    if (File.Exists(TmpFilePath))
                    {
                        if (File.Exists(FilePath))
                        {
                            // .bak 作成を試みてから上書き
                            try { File.Copy(FilePath, BakFilePath, overwrite: true); } catch { }
                            File.Delete(FilePath);
                        }
                        File.Move(TmpFilePath, FilePath);
                    }
                }
                catch { /* best effort */ }

                _ = App.FileLogger.LogAsync(
                    $"[Settings] settings.json の書き込みに失敗しました: {FileLoggerService.FormatException(ex)}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  デバウンス・バックグラウンド保存 (DebouncedSettingsSaver)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 設定変更をデバウンスし、バックグラウンドスレッドでアトミック保存する。
        /// 変更検知後 800ms 待機し、その間に新たな変更があればタイマーをリセット。
        /// </summary>
        private static class DebouncedSettingsSaver
        {
            private static readonly object _lock = new();
            private static CancellationTokenSource? _debounceCts;
            private static Action<WindowSettings>? _pendingMutation;

            /// <summary>
            /// 設定を部分更新してデバウンス保存をスケジュールする。
            /// mutation は Load 済みの settings に対して呼ばれ、変更を適用する。
            /// 800ms 以内に次の呼び出しがあれば前のタイマーはキャンセルされ、
            /// 最新の mutation だけが最終的に書き出される。
            /// </summary>
            public static void ScheduleSave(Action<WindowSettings> mutation)
            {
                lock (_lock)
                {
                    // 前回のデバウンスタイマーをキャンセル
                    _debounceCts?.Cancel();
                    _debounceCts = new CancellationTokenSource();
                    var token = _debounceCts.Token;

                    // mutation を蓄積（前の未実行 mutation は最新で上書き）
                    var prevMutation = _pendingMutation;
                    _pendingMutation = prevMutation != null
                        ? s => { prevMutation(s); mutation(s); }
                        : mutation;

                    var pendingRef = _pendingMutation;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(800, token);
                        }
                        catch (OperationCanceledException)
                        {
                            return; // 新しい変更が来たのでこのタイマーは破棄
                        }

                        // デバウンス完了 → 保存実行
                        Action<WindowSettings>? mutationToApply;
                        lock (_lock)
                        {
                            mutationToApply = _pendingMutation;
                            _pendingMutation = null;
                        }

                        if (mutationToApply == null) return;

                        try
                        {
                            var settings = Load();
                            mutationToApply(settings);
                            settings.Save();
                        }
                        catch (Exception ex)
                        {
                            _ = App.FileLogger.LogAsync(
                                $"[Settings] デバウンス保存に失敗しました: {FileLoggerService.FormatException(ex)}");
                        }
                    });
                }
            }

            /// <summary>
            /// シャットダウン時にデバウンス待機中の変更を即座にフラッシュする（同期）。
            /// MainWindow_Closing から呼び出す。
            /// </summary>
            public static void Flush()
            {
                Action<WindowSettings>? mutationToApply;
                lock (_lock)
                {
                    _debounceCts?.Cancel();
                    _debounceCts = null;
                    mutationToApply = _pendingMutation;
                    _pendingMutation = null;
                }

                if (mutationToApply == null) return;

                try
                {
                    var settings = Load();
                    mutationToApply(settings);
                    settings.Save();
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync(
                        $"[Settings] フラッシュ保存に失敗しました: {FileLoggerService.FormatException(ex)}");
                }
            }
        }

        /// <summary>シャットダウン時にデバウンス待機中の変更を即座にフラッシュする。</summary>
        public static void FlushPendingSaves() => DebouncedSettingsSaver.Flush();

        /// <summary>検索結果フィルターバーのみを更新して settings.json に保存する。フィルター変更のたびに呼ばれる。</summary>
        public static void SaveSearchResultFiltersOnly(List<bool> enabled)
        {
            if (enabled == null || enabled.Count != 11) return;
            DebouncedSettingsSaver.ScheduleSave(s =>
                s.SearchResultFileTypeFilterEnabled = new List<bool>(enabled));
        }

        /// <summary>検索フィルタ（サイズ・日付）のみを更新して settings.json に保存する。</summary>
        public static void SaveSearchFilterOnly(ViewModels.SearchFilterViewModel filter)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.SearchSizeFilter = (int)filter.SizeFilter;
                s.SearchCustomMinSize = filter.CustomMinSizeBytes;
                s.SearchCustomMaxSize = filter.CustomMaxSizeBytes;
                s.SearchMinSizeText = filter.MinSizeText;
                s.SearchMaxSizeText = filter.MaxSizeText;
                s.SearchDateFilter = (int)filter.DateFilter;
                s.SearchStartDateText = filter.StartDateText;
                s.SearchEndDateText = filter.EndDateText;
                s.SearchCustomStartDate = filter.ParsedStartDate?.ToString("o");
                s.SearchCustomEndDate = filter.ParsedEndDate?.ToString("o");
            });
        }

        /// <summary>インデックス検索スコープのみを更新して settings.json に保存する。選択変更のたびに呼ばれる。</summary>
        public static void SaveIndexSearchScopeOnly(List<string>? scopePaths)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
                s.IndexSearchScopePaths = scopePaths);
        }

        /// <summary>お気に入りのみを更新して settings.json を保存する。追加・削除・並べ替えのたびに呼ばれる。</summary>
        public static void SaveFavoritesOnly(List<FavoriteItemDto> favorites)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
                s.Favorites = favorites);
        }

        /// <summary>ワーキングセットのみを更新して settings.json を保存する。</summary>
        public static void SaveWorkingSetsOnly(List<WorkingSetDto> sets)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
                s.WorkingSets = sets);
        }

        /// <summary>検索プリセットのみを更新して settings.json に保存する。</summary>
        public static void SaveSearchPresetsOnly(List<SearchPresetDto> presets)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.SearchPresets = presets;
                _ = App.FileLogger.LogAsync($"[SearchPresets] {presets.Count} 件のプリセットを保存しました");
            });
        }

        /// <summary>テーマ名のみを更新して settings.json に保存する。テーマ切替時に呼ばれる。</summary>
        public static void SaveThemeOnly(string themeName)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.ThemeName = themeName);
        }

        /// <summary>テーマモード設定のみを更新して保存する。</summary>
        public static void SaveThemeModeOnly(string mode, string subMode, string? category)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.CurrentThemeMode = mode;
                s.AutoSelectSubMode = subMode;
                s.SelectedCategory = category;
            });
        }

        /// <summary>パーソナライズモードの選択テーマ名のみ保存する。</summary>
        public static void SavePresetThemeNameOnly(string themeName)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.SavedThemeName = themeName);
        }

        /// <summary>起動時テーマトーストの表示設定を保存する。</summary>
        public static void SaveShowStartupToastOnly(bool show)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.ShowStartupToast = show);
        }

        /// <summary>Display 設定（行高）を保存する。</summary>
        public static void SaveDisplaySettingsOnly(int rowHeight)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.ListRowHeight = rowHeight;
            });
        }

        /// <summary>General 追加設定（シングルクリック・削除確認・タブ復元・通知時間）を保存する。</summary>
        public static void SaveGeneralSettingsOnly(bool singleClick, bool confirmDelete, bool restoreTabs, int notifMs)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.SingleClickOpenFolder = singleClick;
                s.ConfirmDelete = confirmDelete;
                s.RestoreTabsOnStartup = restoreTabs;
                s.NotificationDurationMs = notifMs;
            });
        }

        /// <summary>Search デフォルト設定（フォルダ先頭・ソート）を保存する。</summary>
        public static void SaveSearchDefaultsOnly(bool foldersFirst, string sortProp, ListSortDirection sortDir)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.DefaultGroupFoldersFirst = foldersFirst;
                s.DefaultSortProperty = sortProp;
                s.DefaultSortDirection = sortDir;
            });
        }

        /// <summary>EULA 同意バージョンを保存する。</summary>
        public static void SaveEulaAcceptedOnly(string version)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.EulaAcceptedVersion = version);
        }

        /// <summary>ペイン個別テーマ名を保存する。</summary>
        public static void SavePaneThemeNames(string nav, string aPane, string bPane)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.NavPaneThemeName = nav;
                s.APaneThemeName   = aPane;
                s.BPaneThemeName   = bPane;
            });
        }

        /// <summary>タイトルバーのパス表示設定のみを保存する。</summary>
        public static void SaveShowPathInTitleBarOnly(bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.ShowPathInTitleBar = value);
        }

        /// <summary>拡張子表示設定のみを保存する。</summary>
        public static void SaveShowFileExtensionsOnly(bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.ShowFileExtensions = value);
        }

        /// <summary>隠しファイル表示設定のみを保存する。</summary>
        public static void SaveShowHiddenFilesOnly(bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.ShowHiddenFiles = value);
        }

        /// <summary>ダウンロードフォルダ更新日時ソート設定のみを保存する。</summary>
        public static void SaveDownloadsSortByDateOnly(bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.DownloadsSortByDate = value);
        }

        /// <summary>常駐モード設定のみを保存する。</summary>
        public static void SaveResidentModeOnly(bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.ResidentMode = value);
        }

        /// <summary>演出カテゴリ設定のみを保存する（A〜L カテゴリ別 ON/OFF）。</summary>
        public static void SaveEffectCategoryOnly(string propertyName, bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                switch (propertyName)
                {
                    case nameof(ShowStartupEffects): s.ShowStartupEffects = value; break;
                    case nameof(ShowGlowBar): s.ShowGlowBar = value; break;
                    case nameof(ShowScanBar): s.ShowScanBar = value; break;
                    case nameof(ShowTabEffects): s.ShowTabEffects = value; break;
                    case nameof(ShowPaneTransitions): s.ShowPaneTransitions = value; break;
                    case nameof(ShowThemeEffects): s.ShowThemeEffects = value; break;
                    case nameof(ShowPreviewEffects): s.ShowPreviewEffects = value; break;
                    case nameof(ShowListEffects): s.ShowListEffects = value; break;
                    case nameof(ShowDragEffects): s.ShowDragEffects = value; break;
                }
            });
        }

        /// <summary>自動更新設定のみを保存する。</summary>
        public static void SaveAutoUpdateOnly(bool value)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.AutoUpdate = value);
        }

        /// <summary>最終更新チェック日時のみを保存する。</summary>
        public static void SaveLastUpdateCheckOnly(DateTime dt)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.LastUpdateCheck = dt.ToString("o"));
        }

        /// <summary>スキップバージョンのみを保存する。</summary>
        public static void SaveSkippedVersionOnly(string? version)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.SkippedVersion = version);
        }

        /// <summary>インデックススケジュール設定のみを保存する（旧形式、後方互換）。</summary>
        public static void SaveIndexSchedulesOnly(List<IndexScheduleDto>? schedules)
        {
            DebouncedSettingsSaver.ScheduleSave(s => s.IndexSchedules = schedules);
        }

        /// <summary>インデックスのアイテム別詳細設定のみを保存する。</summary>
        public static void SaveIndexItemSettingsOnly(List<IndexItemSettingsDto>? itemSettings)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
            {
                s.IndexItemSettings = itemSettings;
                s.IndexSchedules = null; // 旧形式をクリア
            });
        }

        /// <summary>カスタムキーバインドのみを更新して settings.json に保存する。</summary>
        public static void SaveKeyBindingsOnly(List<Models.KeyBindingDto>? bindings)
        {
            DebouncedSettingsSaver.ScheduleSave(s =>
                s.CustomKeyBindings = bindings);
        }

        /// <summary>お気に入りを保存する。失敗した場合は例外を投げる（ロールバック判定用）。即時保存。</summary>
        public static void SaveFavoritesOnlyOrThrow(List<FavoriteItemDto> favorites)
        {
            var settings = Load();
            settings.Favorites = favorites;
            settings.Save();
        }
    }
}
