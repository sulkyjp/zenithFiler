using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ZenithFiler
{
    /// <summary>
    /// 本体アプリの MainViewModel が持つ BeginBusy を呼ぶためのインターフェース。
    /// ZenithDocViewer 単独起動時は未実装のため、using は no-op になる。
    /// </summary>
    internal interface IBusyTokenProvider
    {
        IDisposable? BeginBusy();
    }

    public class ManualTocItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public int Level { get; set; }

        public bool HasSummary => !string.IsNullOrEmpty(Summary);

        /// <summary>
        /// 目次用インデント幅（Level に応じて計算）
        /// H1 は 0、H2 以降は 1 レベルごとに少しずつ右にずらす。
        /// </summary>
        public double Indent => Math.Max(0, (Level - 1) * 14);

        /// <summary>
        /// セクション種別に応じたアイコン種別を返す（XAML 側で PackIconLucide.Kind にバインド）。
        /// 既存の XAML で利用実績のあるアイコン名のみを使い、すべての項目で必ずアイコンが表示されるようにする。
        /// </summary>
        public string IconKind => GetIconKindForTitle(Level, Title);

        private static string GetIconKindForTitle(int level, string title)
        {
            // ルートセクションは本のアイコン
            if (level == 1) return "BookOpen";

            if (string.IsNullOrEmpty(title)) return "FileText";

            const StringComparison c = StringComparison.OrdinalIgnoreCase;

            // タイトルに応じたマッピング（安全な既存アイコンのみ使用）
            if (title.Contains("キーボード", c) || title.Contains("ショートカット", c)) return "Keyboard";
            if (title.Contains("ドラッグ", c) || title.Contains("マウス", c)) return "MousePointer2";
            if (title.Contains("画面構成", c)) return "LayoutDashboard";
            if (title.Contains("ファイルペイン", c)) return "Columns2";
            if (title.Contains("ナビペイン", c)) return "PanelLeft";
            if (title.Contains("ヘルプ", c) || title.Contains("ドキュメントビューア", c)) return "BookOpen";
            if (title.Contains("ステータスバー", c)) return "PanelBottom";
            if (title.Contains("ナビゲーション", c)) return "ArrowRight";
            if (title.Contains("タブ操作", c)) return "LayoutGrid";
            if (title.Contains("標準的なファイル操作", c)) return "FileText";
            if (title.Contains("高度な機能", c)) return "Sparkles";
            if (title.Contains("お気に入り", c)) return "Star";
            if (title.Contains("表示オプション", c)) return "Rows3";
            if (title.Contains("検索機能", c) || title.Contains("検索", c)) return "Search";

            // バージョン項目 (v0.3.66 など) は履歴らしいアイコンにする
            if (title.StartsWith("v", c)) return "ScrollText";

            return "FileText";
        }
    }

    public partial class ManualViewModel : ObservableObject
    {
        private IBusyTokenProvider? BusyProvider => Application.Current?.MainWindow?.DataContext as IBusyTokenProvider;

        /// <summary>
        /// MANUAL.md の生テキスト
        /// </summary>
        [ObservableProperty]
        private string _markdownText = string.Empty;

        /// <summary>
        /// true のとき CHANGELOG.md（更新履歴）モード、false のとき MANUAL.md（ユーザーマニュアル）モード
        /// </summary>
        [ObservableProperty]
        private bool _isChangelog;

        /// <summary>
        /// Markdown から抽出した目次項目
        /// </summary>
        [ObservableProperty]
        private List<ManualTocItem> _tocItems = new();

        /// <summary>
        /// 現在選択中の目次項目（ビュー側でスクロールのトリガーとして使用）
        /// </summary>
        [ObservableProperty]
        private ManualTocItem? _selectedTocItem;

        /// <summary>
        /// マニュアル内検索クエリ
        /// </summary>
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        /// <summary>
        /// 検索ヒット件数と現在位置の表示テキスト（例: "1 / 12 items"）
        /// </summary>
        [ObservableProperty]
        private string _searchStatusText = string.Empty;

        /// <summary>
        /// 検索ヒット件数が1件以上あるかどうか
        /// </summary>
        [ObservableProperty]
        private bool _hasSearchMatches;

        public event EventHandler? SearchNextRequested;
        public event EventHandler? SearchPreviousRequested;

        [RelayCommand]
        private void SearchNext()
        {
            SearchNextRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void SearchPrevious()
        {
            SearchPreviousRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ClearSearchQuery()
        {
            SearchQuery = string.Empty;
            SearchStatusText = string.Empty;
            HasSearchMatches = false;
        }

        /// <summary>
        /// コンストラクタ。startWithChangelog が true の場合は更新履歴モードで初期化する。
        /// ManualWindow から「更新履歴」タブで開く際に渡し、初期ロードの競合を防ぐ。
        /// 初期表示は同期的にロードし、ウィンドウ表示前にコンテンツを確保する。
        /// </summary>
        public ManualViewModel(bool startWithChangelog = false)
        {
            _isChangelog = startWithChangelog;
            LoadDocumentForCurrentModeSync();
        }

        partial void OnIsChangelogChanged(bool value)
        {
            _ = LoadDocumentForCurrentModeAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    DocViewerLogHelper.TryLog(t.Exception.GetBaseException());
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// 初期表示用に同期的にドキュメントを読み込む。コンストラクタから呼ばれる。
        /// </summary>
        private void LoadDocumentForCurrentModeSync()
        {
            var (loadedText, loadedToc) = LoadDocumentCore(IsChangelog);
            MarkdownText = loadedText;
            TocItems = loadedToc;
            if (loadedToc.Any())
                SelectedTocItem = loadedToc.First();
        }

        /// <summary>
        /// 現在のモードに応じて MANUAL.md または CHANGELOG.md を読み込み、
        /// MarkdownText と TocItems を更新する。
        /// </summary>
        private async Task LoadDocumentForCurrentModeAsync()
        {
            var loadingForChangelog = IsChangelog;
            using var busyToken = BusyProvider?.BeginBusy();

            var (loadedText, loadedToc) = await Task.Run(() => LoadDocumentCore(loadingForChangelog));

            // UIスレッドで反映（タブ切り替えで別ロードが開始された場合は古い結果を無視）
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (IsChangelog != loadingForChangelog) return;

                MarkdownText = loadedText;
                TocItems = loadedToc;

                if (TocItems.Any())
                {
                    SelectedTocItem = TocItems.First();
                }
            }, DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// ファイル読み込みとパースの共通ロジック。
        /// </summary>
        private static (string loadedText, List<ManualTocItem> loadedToc) LoadDocumentCore(bool forChangelog)
        {
            var loadedToc = new List<ManualTocItem>();
            var loadedText = string.Empty;

            try
            {
                var fileName = forChangelog ? "CHANGELOG.md" : "MANUAL.md";
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                var candidates = new List<string>();
                var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    candidates.Add(Path.Combine(exeDir, "apps", fileName));
                    candidates.Add(Path.Combine(exeDir, fileName));
                }
                candidates.Add(Path.Combine(baseDir, "apps", fileName));
                candidates.Add(Path.Combine(baseDir, fileName));
                candidates.Add(fileName);
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), fileName));

                var dir = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                for (int i = 0; i < 10; i++)
                {
                    var parent = Directory.GetParent(dir);
                    if (parent == null) break;
                    dir = parent.FullName;
                    candidates.Add(Path.Combine(dir, "apps", fileName));
                    candidates.Add(Path.Combine(dir, fileName));
                }

                foreach (var path in candidates)
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                    var content = File.ReadAllText(path, Encoding.UTF8);

                    if (forChangelog)
                    {
                        content = RemoveUnreleasedSection(content);
                        loadedToc = BuildChangelogToc(content);
                        content = RemoveDatesFromChangelog(content);
                    }
                    else
                    {
                        loadedToc = BuildManualToc(content);
                    }

                    loadedText = content;
                    return (loadedText, loadedToc);
                }

                loadedText = $"# Document Not Found\n\n**{fileName}** could not be found.";
            }
            catch (Exception ex)
            {
                loadedText = $"# Error\n\nFailed to load document.\n\n{ex.Message}";
            }

            return (loadedText, loadedToc);
        }

        /// <summary>
        /// MANUAL.md 用の目次生成。
        /// H1〜H3 を対象に、元のマニュアルと同様の構成を維持する。
        /// </summary>
        private static List<ManualTocItem> BuildManualToc(string markdown)
        {
            var items = new List<ManualTocItem>();

            using var reader = new StringReader(markdown);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var match = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (!match.Success) continue;

                var level = match.Groups[1].Value.Length;
                if (level > 3) continue;

                var title = match.Groups[2].Value.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var plainTitle = StripMarkdown(title);
                var id = GenerateAnchorId(plainTitle);

                items.Add(new ManualTocItem
                {
                    Id = id,
                    Title = title,
                    Level = level
                });
            }

            return items;
        }

        /// <summary>
        /// CHANGELOG.md 用の目次生成。
        /// </summary>
        private static List<ManualTocItem> BuildChangelogToc(string markdown)
        {
            var items = new List<ManualTocItem>();

            using var reader = new StringReader(markdown);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var match = Regex.Match(line, @"^#{2}\s+\[(?<version>[^\]]+)\](?:\s*-\s*[\d-]+)?(?:\s*[:\-]\s*(?<summary>.*))?$");
                if (!match.Success) continue;

                var version = match.Groups["version"].Value.Trim();
                if (version.Equals("Unreleased", StringComparison.OrdinalIgnoreCase)) continue;

                var summary = match.Groups["summary"].Value.Trim();

                var cleanLine = Regex.Replace(line, @"^(##\s*\[[^\]]+\])\s*-\s*[\d-]+", "$1");
                var headingText = cleanLine.TrimStart('#').Trim();
                var id = GenerateAnchorId(headingText);

                items.Add(new ManualTocItem
                {
                    Id = id,
                    Title = $"v{version}",
                    Summary = summary,
                    Level = 2
                });
            }

            return items;
        }

        private static string StripMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = Regex.Replace(text, @"!\[([^\]]*)\]\([^\)]+\)", "$1");
            text = Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            text = Regex.Replace(text, @"(\*\*|__)(.*?)\1", "$2");
            text = Regex.Replace(text, @"(\*|_)(.*?)\1", "$2");
            text = Regex.Replace(text, @"`([^`]+)`", "$1");
            text = Regex.Replace(text, @"~~(.*?)~~", "$1");

            return text.Trim();
        }

        private static string RemoveDatesFromChangelog(string markdown)
        {
            return Regex.Replace(markdown, @"^(##\s*\[[^\]]+\])\s*-\s*[\d-]+", "$1", RegexOptions.Multiline);
        }

        private static string RemoveUnreleasedSection(string markdown)
        {
            var pattern = @"(?ims)^##\s*\[Unreleased\].*?(?=^##\s*\[|\z)";
            return Regex.Replace(markdown, pattern, string.Empty).TrimStart();
        }

        /// <summary>
        /// GitHub 風のアンカー ID を生成する（目次と本文のアンカーリンクで共通利用）
        /// </summary>
        public static string GenerateAnchorId(string heading)
        {
            var lower = heading.ToLowerInvariant();
            lower = Regex.Replace(lower, @"[^\p{L}\p{N}\s-]", string.Empty);
            lower = Regex.Replace(lower, @"\s+", "-");
            return lower;
        }
    }
}
