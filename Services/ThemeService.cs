using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using ZenithFiler.Models;

namespace ZenithFiler.Services;

public class ThemeService
{
    private string _currentDescription = "Zenith Filer 標準の落ち着いたベージュ基調のテーマ";
    private string _currentAuthor = "赤阪和彦";
    private ThemeCategory _currentCategory = ThemeCategory.Standard;

    /// <summary>テーマ名 → ThemeCategory のフォールバックマッピング（JSON に Category がない場合に使用）。</summary>
    private static readonly Dictionary<string, ThemeCategory> CategoryFallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["standard"]       = ThemeCategory.Standard,
        ["SolarizedLight"] = ThemeCategory.Standard,
        ["Nordic"]         = ThemeCategory.Standard,
        ["PaperWhite"]     = ThemeCategory.Standard,
        ["Espresso"]       = ThemeCategory.WarmCozy,
        ["MilkTeaPearl"]   = ThemeCategory.WarmCozy,
        ["Sakura"]         = ThemeCategory.WarmCozy,
        ["MintSorbet"]     = ThemeCategory.WarmCozy,
        ["LavenderMist"]   = ThemeCategory.WarmCozy,
        ["midnight"]       = ThemeCategory.Professional,
        ["SlateBlue"]      = ThemeCategory.Professional,
        ["Monochrome"]     = ThemeCategory.Professional,
        ["blueprint"]      = ThemeCategory.Professional,
        ["ImperialGold"]   = ThemeCategory.Premium,
        ["CrimsonPeak"]    = ThemeCategory.Premium,
        ["Forest"]         = ThemeCategory.Premium,
        ["DeepSea"]        = ThemeCategory.Premium,
        ["Terminal"]       = ThemeCategory.RetroTech,
        ["CyberNeon"]      = ThemeCategory.RetroTech,
        ["AmberConsole"]   = ThemeCategory.RetroTech,
        ["Retro95"]        = ThemeCategory.RetroTech,
    };

    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["BackgroundColor"] = "#F5F1E3",
        ["SidebarColor"] = "#E6E1D3",
        ["HeaderColor"] = "#F5F1E3",
        ["TitleBarColor"] = "#545B64",
        ["BorderColor"] = "#D3D0C3",
        ["PrimaryTextColor"] = "#1A1A1A",
        ["SecondaryTextColor"] = "#777777",
        ["InputBackgroundColor"] = "#FFFFFF",
        ["FocusedPaneBackgroundColor"] = "#EBE6D5",
        ["FocusedPaneTextColor"] = "#000000",
        ["FocusedPaneHeaderBackgroundColor"] = "#DFDAC8",
        ["OnePointDarkColor"] = "#545B64",
        ["TabSeparatorColor"] = "#D3D0C3",
        ["TabHoverColor"] = "#D8D3C5",
        ["ToolbarSeparatorColor"] = "#777777",
        ["TreeLineColor"] = "#C4BFB0",
        ["ListHoverColor"] = "#DFDAC8",
        ["SelectionColor"] = "#D0E6F8",
        ["ButtonHoverColor"] = "#C9C4B3",
        ["InactiveSelectionColor"] = "#E5E5E5",
        ["OptionSelectedColor"] = "#E8F0F6",
        ["OptionSelectedHoverColor"] = "#D8E6EE",
        ["CheckboxHoverColor"] = "#F0ECE0",
        ["CheckedHoverAccentColor"] = "#1E7AB8",
        ["IndexSearchBackgroundColor"] = "#FFF9E6",
        ["IndexSearchIconColor"] = "#E6B800",
        ["IndexSearchFocusBorderColor"] = "#F9A825",
        ["AccentColor"] = "#268BD2",
        ["ErrorTextColor"] = "#E05555",
        ["DestructiveHoverBackgroundColor"] = "#3D0000",
        ["DestructiveIconColor"] = "#FF6B6B",
        ["ContextMenuSeparatorColor"] = "#D5D5D0",
        ["ContextMenuBackgroundColor"] = "#F5F5F0",
        ["ContextMenuBorderColor"] = "#B0B0B0",
        ["ContextMenuTextColor"] = "#222222",
        ["ContextMenuIconColor"] = "#444444",
        ["ContextMenuShortcutColor"] = "#888888",
        ["ContextMenuDisabledColor"] = "#AAAAAA",
        ["ContextMenuDisabledIconColor"] = "#BBBBBB",
        ["ContextMenuHighlightShortcutColor"] = "#CCCCCC",
        ["ContextMenuOuterBackgroundColor"] = "#FEFEFE",
        ["GlowBarGlowColor"] = "Cyan",
        ["ScanBarColor"] = "#90CAF9",
        ["LoadingOverlayColor"] = "#66545B64",
        ["ShadowColor"] = "#000000",
        ["TelemetryBackgroundColor"] = "#E6181818",
        ["TelemetryBorderColor"] = "#40FFFFFF",
        ["TelemetryTitleColor"] = "#80C0FF",
        ["TelemetryValueColor"] = "#E0E0E0",
    };

    /// <summary>Color リソースキー → SolidColorBrush リソースキーのマッピング。</summary>
    private static readonly Dictionary<string, string> ColorToBrush = new()
    {
        ["BackgroundColor"] = "BackgroundBrush",
        ["SidebarColor"] = "SidebarBrush",
        ["HeaderColor"] = "HeaderBrush",
        ["TitleBarColor"] = "TitleBarBrush",
        ["BorderColor"] = "BorderBrush",
        ["PrimaryTextColor"] = "TextBrush",
        ["SecondaryTextColor"] = "SubTextBrush",
        ["InputBackgroundColor"] = "InputBackgroundBrush",
        ["FocusedPaneBackgroundColor"] = "FocusedPaneBackgroundBrush",
        ["FocusedPaneTextColor"] = "FocusedPaneTextBrush",
        ["FocusedPaneHeaderBackgroundColor"] = "FocusedPaneHeaderBackgroundBrush",
        ["OnePointDarkColor"] = "OnePointDarkBackground",
        ["TabSeparatorColor"] = "TabSeparatorBrush",
        ["TabHoverColor"] = "TabHoverBrush",
        ["ToolbarSeparatorColor"] = "ToolbarSeparatorBrush",
        ["TreeLineColor"] = "TreeLineBrush",
        ["AccentColor"] = "AccentBrush",
        ["ListHoverColor"] = "ListHoverBrush",
        ["SelectionColor"] = "SelectionBrush",
        ["ButtonHoverColor"] = "ButtonHoverBrush",
        ["InactiveSelectionColor"] = "InactiveSelectionBrush",
        ["OptionSelectedColor"] = "OptionSelectedBrush",
        ["OptionSelectedHoverColor"] = "OptionSelectedHoverBrush",
        ["CheckboxHoverColor"] = "CheckboxHoverBrush",
        ["CheckedHoverAccentColor"] = "CheckedHoverAccentBrush",
        ["IndexSearchBackgroundColor"] = "IndexSearchBackgroundBrush",
        ["IndexSearchIconColor"] = "IndexSearchIconBrush",
        ["IndexSearchFocusBorderColor"] = "IndexSearchFocusBorderBrush",
        ["ErrorTextColor"] = "ErrorTextBrush",
        ["DestructiveHoverBackgroundColor"] = "DestructiveHoverBackgroundBrush",
        ["DestructiveIconColor"] = "DestructiveIconBrush",
        ["ContextMenuSeparatorColor"] = "ContextMenuSeparatorBrush",
        ["ContextMenuBackgroundColor"] = "ContextMenuBackgroundBrush",
        ["ContextMenuBorderColor"] = "ContextMenuBorderBrush",
        ["ContextMenuTextColor"] = "ContextMenuTextBrush",
        ["ContextMenuIconColor"] = "ContextMenuIconBrush",
        ["ContextMenuShortcutColor"] = "ContextMenuShortcutBrush",
        ["ContextMenuDisabledColor"] = "ContextMenuDisabledBrush",
        ["ContextMenuDisabledIconColor"] = "ContextMenuDisabledIconBrush",
        ["ContextMenuHighlightShortcutColor"] = "ContextMenuHighlightShortcutBrush",
        ["ContextMenuOuterBackgroundColor"] = "ContextMenuOuterBackgroundBrush",
        ["ScanBarColor"] = "ScanBarBrush",
        ["LoadingOverlayColor"] = "LoadingOverlayBrush",
        ["TelemetryBackgroundColor"] = "TelemetryBackgroundBrush",
        ["TelemetryBorderColor"] = "TelemetryBorderBrush",
        ["TelemetryTitleColor"] = "TelemetryTitleBrush",
        ["TelemetryValueColor"] = "TelemetryValueBrush",
    };

    private static readonly Dictionary<string, string> Comments = new()
    {
        ["BackgroundColor"] = "メイン背景色",
        ["SidebarColor"] = "サイドバー（ナビペイン）背景色",
        ["HeaderColor"] = "ヘッダー背景色",
        ["TitleBarColor"] = "タイトルバー背景色（チャコールブラック）",
        ["BorderColor"] = "枠線・区切り線の色",
        ["PrimaryTextColor"] = "メインテキスト色（ほぼ黒）",
        ["SecondaryTextColor"] = "サブテキスト・補足情報の色",
        ["InputBackgroundColor"] = "テキスト入力欄の背景色",
        ["FocusedPaneBackgroundColor"] = "フォーカス中ペインの背景色（標準よりわずかに濃い）",
        ["FocusedPaneTextColor"] = "フォーカス中ペインのテキスト色",
        ["FocusedPaneHeaderBackgroundColor"] = "フォーカス中ペインのヘッダー背景色（一覧より濃い）",
        ["OnePointDarkColor"] = "ワンポイント用ダーク帯（お気に入り第1階層・アクティブタブ）",
        ["TabSeparatorColor"] = "タブ間の区切り線",
        ["TabHoverColor"] = "タブホバー時の背景色",
        ["ToolbarSeparatorColor"] = "ツールバーセパレーター色",
        ["TreeLineColor"] = "ツリービュー接続線の色",
        ["ListHoverColor"] = "リスト項目ホバー時の背景色",
        ["SelectionColor"] = "選択項目のハイライト色",
        ["ButtonHoverColor"] = "ボタンホバー時の背景色",
        ["InactiveSelectionColor"] = "非アクティブペインの選択色",
        ["OptionSelectedColor"] = "ラジオボタン等の選択時背景色",
        ["OptionSelectedHoverColor"] = "ラジオボタン等の選択＋ホバー時背景色",
        ["CheckboxHoverColor"] = "チェックボックスホバー時の背景色",
        ["CheckedHoverAccentColor"] = "チェック済みホバー時のアクセント色（暗めのアクセント）",
        ["IndexSearchBackgroundColor"] = "インデックス検索モード時の背景色（穏やかな黄色）",
        ["IndexSearchIconColor"] = "インデックス検索の雷アイコン色（ゴールド）",
        ["IndexSearchFocusBorderColor"] = "インデックス検索＋フォーカス時のボーダー色（アンバー）",
        ["AccentColor"] = "アクセントカラー（リンク・強調 / Solarized Blue）",
        ["ErrorTextColor"] = "エラーテキスト色",
        ["DestructiveHoverBackgroundColor"] = "破壊的操作ホバー時の背景色（暗い赤）",
        ["DestructiveIconColor"] = "破壊的操作のアイコン色（明るい赤）",
        ["ContextMenuSeparatorColor"] = "コンテキストメニューのセパレーター色",
        ["ContextMenuBackgroundColor"] = "コンテキストメニューの背景色",
        ["ContextMenuBorderColor"] = "コンテキストメニューの枠線色",
        ["ContextMenuTextColor"] = "コンテキストメニューのテキスト色",
        ["ContextMenuIconColor"] = "コンテキストメニューのアイコン色",
        ["ContextMenuShortcutColor"] = "コンテキストメニューのショートカットキー表示色",
        ["ContextMenuDisabledColor"] = "コンテキストメニューの無効項目テキスト色",
        ["ContextMenuDisabledIconColor"] = "コンテキストメニューの無効項目アイコン色",
        ["ContextMenuHighlightShortcutColor"] = "ハイライト時のショートカットキー表示色",
        ["ContextMenuOuterBackgroundColor"] = "コンテキストメニュー外枠の背景色",
        ["GlowBarGlowColor"] = "進捗バー（GlowBar）のグロー色",
        ["ScanBarColor"] = "スキャンバーの色",
        ["LoadingOverlayColor"] = "ローディングオーバーレイ色（#AARRGGBB 半透明）",
        ["ShadowColor"] = "ダイアログのシャドウ色",
        ["TelemetryBackgroundColor"] = "テレメトリーパネル背景色（#AARRGGBB 半透明）",
        ["TelemetryBorderColor"] = "テレメトリーパネル枠線色（#AARRGGBB 半透明）",
        ["TelemetryTitleColor"] = "テレメトリーパネルのタイトル色",
        ["TelemetryValueColor"] = "テレメトリーパネルの値テキスト色",
    };

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private Dictionary<string, string> _resolvedColors = new(Defaults);

    /// <summary>現在適用中のテーマ名。</summary>
    public string CurrentThemeName { get; private set; } = "standard";

    // ─── Theme Discovery ───

    /// <summary>themes フォルダ内のすべての .json ファイルを ThemeInfo としてリストアップする。</summary>
    public List<ThemeInfo> ScanThemes()
    {
        var themesDir = GetThemesDirectory();
        var results = new List<ThemeInfo>();
        ThemeInfo? standardInfo = null;

        if (Directory.Exists(themesDir))
        {
            foreach (var file in Directory.EnumerateFiles(themesDir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var info = ReadThemeInfo(file, name);
                if (string.Equals(name, "standard", StringComparison.OrdinalIgnoreCase))
                {
                    standardInfo = info;
                }
                else
                {
                    results.Add(info);
                }
            }
        }

        // standard がなければハードコードデフォルト
        standardInfo ??= new ThemeInfo("standard", "Zenith Filer 標準の落ち着いたベージュ基調のテーマ", "赤阪和彦",
            backgroundColor: Defaults["BackgroundColor"], accentColor: Defaults["AccentColor"],
            primaryTextColor: Defaults["PrimaryTextColor"], sidebarColor: Defaults["SidebarColor"],
            category: ThemeCategory.Standard);
        results.Add(standardInfo);
        // カテゴリ順 → 名前順でソート
        results.Sort((a, b) =>
        {
            int cmp = a.CategorySortOrder.CompareTo(b.CategorySortOrder);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return results;
    }

    /// <summary>JSON の Category 文字列またはフォールバック辞書からカテゴリを解決する。</summary>
    private static ThemeCategory ResolveCategory(string themeName, string? jsonCategory)
    {
        if (!string.IsNullOrWhiteSpace(jsonCategory) && Enum.TryParse<ThemeCategory>(jsonCategory, true, out var parsed))
            return parsed;
        return CategoryFallback.TryGetValue(themeName, out var fallback) ? fallback : ThemeCategory.Standard;
    }

    /// <summary>テーマ JSON からメタデータのみを軽量に読み取る。</summary>
    private static ThemeInfo ReadThemeInfo(string path, string name)
    {
        try
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<ThemeModel>(json, _readOptions);
            var category = ResolveCategory(name, model?.Category);
            return new ThemeInfo(name, model?.Description, model?.Author,
                backgroundColor: model?.Base?.BackgroundColor,
                accentColor: model?.Accent?.AccentColor,
                primaryTextColor: model?.Base?.PrimaryTextColor,
                sidebarColor: model?.Base?.SidebarColor,
                category: category);
        }
        catch
        {
            return new ThemeInfo(name, category: ResolveCategory(name, null));
        }
    }

    // ─── Load & Apply (起動時) ───

    /// <summary>
    /// テーマ JSON を読み込み、リソース辞書に適用する（起動時用）。
    /// ファイルが存在しない場合はデフォルトのまま使用する。
    /// </summary>
    public void LoadAndApply(ResourceDictionary resources, string themeName = "standard")
    {
        CurrentThemeName = themeName;
        LoadThemeColors(themeName);
        ApplyToResources(resources);
    }

    // ─── Live Hot-Swap ───

    /// <summary>
    /// テーマを即時切り替える。Color リソースを更新し、新しい SolidColorBrush インスタンスを
    /// トップレベルリソースに設定する。DynamicResource 参照が自動的に新しいブラシを解決する。
    /// </summary>
    public bool ApplyThemeLive(string themeName, ResourceDictionary resources)
    {
        CurrentThemeName = themeName;

        if (!LoadThemeColors(themeName))
        {
            return false;
        }

        ApplyToResources(resources);
        return true;
    }

    // ─── Export ───

    /// <summary>
    /// 現在のリソース辞書から全 Color 値を読み取り、コメント付きテーマ JSON にエクスポートする。
    /// </summary>
    public async Task ExportAsync(string? themeName = null)
    {
        var name = themeName ?? CurrentThemeName;
        var resources = Application.Current.Resources;
        var outputPath = GetThemePath(name);
        var dir = Path.GetDirectoryName(outputPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var jsonc = BuildCommentedJsonc(resources);
        await File.WriteAllTextAsync(outputPath, jsonc);
    }

    // ─── Private: Theme Loading ───

    /// <summary>テーマの色をロードして _resolvedColors にマージする。成功時 true。</summary>
    private bool LoadThemeColors(string themeName)
    {
        _resolvedColors = new Dictionary<string, string>(Defaults);
        _currentDescription = "Zenith Filer 標準の落ち着いたベージュ基調のテーマ";
        _currentAuthor = "赤阪和彦";
        _currentCategory = ResolveCategory(themeName, null);

        if (string.Equals(themeName, "standard", StringComparison.OrdinalIgnoreCase))
        {
            // standard テーマ: JSON があればロード、なければデフォルトのまま
            var path = GetThemePath("standard");
            if (File.Exists(path))
            {
                return TryLoadAndMerge(path);
            }
            return true;
        }

        // カスタムテーマ
        var themePath = GetThemePath(themeName);
        if (!File.Exists(themePath))
        {
            App.Notification.Notify($"テーマファイルが見つかりません", $"themes/{themeName}.json");
            return false;
        }

        return TryLoadAndMerge(themePath);
    }

    private bool TryLoadAndMerge(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<ThemeModel>(json, _readOptions);
            if (model != null)
            {
                MergeModel(model);
                if (!string.IsNullOrWhiteSpace(model.Description))
                    _currentDescription = model.Description;
                if (!string.IsNullOrWhiteSpace(model.Author))
                    _currentAuthor = model.Author;
                _currentCategory = ResolveCategory(
                    Path.GetFileNameWithoutExtension(path), model.Category);
            }
            return true;
        }
        catch (Exception ex)
        {
            _ = App.FileLogger.LogAsync($"[ThemeService] テーマ JSON の読み込みに失敗: {ex.Message}");
            App.Notification.Notify("テーマの読み込みに失敗しました", Path.GetFileName(path));
            _resolvedColors = new Dictionary<string, string>(Defaults);
            return false;
        }
    }

    private void MergeModel(ThemeModel model)
    {
        if (model.Base != null) MergeCategory(model.Base);
        if (model.List != null) MergeCategory(model.List);
        if (model.Search != null) MergeCategory(model.Search);
        if (model.Accent != null) MergeCategory(model.Accent);
        if (model.ContextMenu != null) MergeCategory(model.ContextMenu);
        if (model.Misc != null) MergeCategory(model.Misc);
    }

    private void MergeCategory(object category)
    {
        foreach (var prop in category.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(category) as string;
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!Defaults.ContainsKey(prop.Name)) continue;

            if (TryParseColor(value))
            {
                _resolvedColors[prop.Name] = value;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] 不正なカラーコード: {prop.Name} = \"{value}\"");
            }
        }
    }

    // ─── Private: Resource Application ───

    /// <summary>
    /// Color リソースを更新し、新しい SolidColorBrush インスタンスをトップレベルリソースに設定する。
    /// DynamicResource 参照がトップレベルの新しいブラシを自動解決するため、UI が即座に更新される。
    /// </summary>
    private void ApplyToResources(ResourceDictionary resources)
    {
        foreach (var (key, colorString) in _resolvedColors)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorString);

            // Color リソースを更新（Storyboard 等の StaticResource Color 参照用）
            resources[key] = color;

            // 対応する SolidColorBrush を新規作成してトップレベルに設定
            if (ColorToBrush.TryGetValue(key, out var brushKey))
            {
                resources[brushKey] = new SolidColorBrush(color);
            }
        }
    }

    // ─── Private: Helpers ───

    private static bool TryParseColor(string value)
    {
        try
        {
            ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ColorToString(Color c)
    {
        if (c.A == 255)
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static string GetThemesDirectory()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "themes");
    }

    private static string GetThemePath(string themeName)
    {
        return Path.Combine(GetThemesDirectory(), $"{themeName}.json");
    }

    private static string EscapeJsonString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    // ─── JSONC Export ───

    private string BuildCommentedJsonc(ResourceDictionary resources)
    {
        string GetColor(string key)
        {
            if (resources.Contains(key) && resources[key] is Color c)
                return ColorToString(c);
            foreach (var merged in resources.MergedDictionaries)
            {
                if (merged.Contains(key) && merged[key] is Color mc)
                    return ColorToString(mc);
            }
            return Defaults.TryGetValue(key, out var def) ? def : "#000000";
        }

        string Comment(string key) =>
            Comments.TryGetValue(key, out var c) ? c : "";

        string Entry(string key, bool last = false)
        {
            var comma = last ? "" : ",";
            var comment = Comment(key);
            var pad = comment.Length > 0 ? new string(' ', Math.Max(1, 44 - key.Length - GetColor(key).Length)) : "";
            return comment.Length > 0
                ? $"    \"{key}\": \"{GetColor(key)}\"{comma}{pad}// {comment}"
                : $"    \"{key}\": \"{GetColor(key)}\"{comma}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  // ============================================================");
        sb.AppendLine("  //  Zenith Filer Theme Configuration");
        sb.AppendLine("  //  カラーコード: #RRGGBB / #AARRGGBB / 色名 (例: Cyan)");
        sb.AppendLine("  //  テーマ選択時に即座に反映されます（再起動不要）");
        sb.AppendLine("  // ============================================================");
        sb.AppendLine();

        sb.AppendLine("  // テーマのメタデータ");
        sb.AppendLine($"  \"Description\": \"{EscapeJsonString(_currentDescription)}\",");
        sb.AppendLine($"  \"Author\": \"{EscapeJsonString(_currentAuthor)}\",");
        sb.AppendLine($"  \"Category\": \"{_currentCategory}\",");
        sb.AppendLine();

        sb.AppendLine("  // ── 基本 UI 要素 ──");
        sb.AppendLine("  \"Base\": {");
        sb.AppendLine(Entry("BackgroundColor"));
        sb.AppendLine(Entry("SidebarColor"));
        sb.AppendLine(Entry("HeaderColor"));
        sb.AppendLine(Entry("TitleBarColor"));
        sb.AppendLine(Entry("BorderColor"));
        sb.AppendLine(Entry("PrimaryTextColor"));
        sb.AppendLine(Entry("SecondaryTextColor"));
        sb.AppendLine(Entry("InputBackgroundColor"));
        sb.AppendLine(Entry("FocusedPaneBackgroundColor"));
        sb.AppendLine(Entry("FocusedPaneTextColor"));
        sb.AppendLine(Entry("FocusedPaneHeaderBackgroundColor"));
        sb.AppendLine(Entry("OnePointDarkColor"));
        sb.AppendLine(Entry("TabSeparatorColor"));
        sb.AppendLine(Entry("TabHoverColor"));
        sb.AppendLine(Entry("ToolbarSeparatorColor"));
        sb.AppendLine(Entry("TreeLineColor", last: true));
        sb.AppendLine("  },");
        sb.AppendLine();

        sb.AppendLine("  // ── リスト・選択・ホバー ──");
        sb.AppendLine("  \"List\": {");
        sb.AppendLine(Entry("ListHoverColor"));
        sb.AppendLine(Entry("SelectionColor"));
        sb.AppendLine(Entry("ButtonHoverColor"));
        sb.AppendLine(Entry("InactiveSelectionColor"));
        sb.AppendLine(Entry("OptionSelectedColor"));
        sb.AppendLine(Entry("OptionSelectedHoverColor"));
        sb.AppendLine(Entry("CheckboxHoverColor"));
        sb.AppendLine(Entry("CheckedHoverAccentColor", last: true));
        sb.AppendLine("  },");
        sb.AppendLine();

        sb.AppendLine("  // ── インデックス検索 ──");
        sb.AppendLine("  \"Search\": {");
        sb.AppendLine(Entry("IndexSearchBackgroundColor"));
        sb.AppendLine(Entry("IndexSearchIconColor"));
        sb.AppendLine(Entry("IndexSearchFocusBorderColor", last: true));
        sb.AppendLine("  },");
        sb.AppendLine();

        sb.AppendLine("  // ── アクセント・エラー・破壊的操作 ──");
        sb.AppendLine("  \"Accent\": {");
        sb.AppendLine(Entry("AccentColor"));
        sb.AppendLine(Entry("ErrorTextColor"));
        sb.AppendLine(Entry("DestructiveHoverBackgroundColor"));
        sb.AppendLine(Entry("DestructiveIconColor", last: true));
        sb.AppendLine("  },");
        sb.AppendLine();

        sb.AppendLine("  // ── 右クリックメニュー ──");
        sb.AppendLine("  \"ContextMenu\": {");
        sb.AppendLine(Entry("ContextMenuSeparatorColor"));
        sb.AppendLine(Entry("ContextMenuBackgroundColor"));
        sb.AppendLine(Entry("ContextMenuBorderColor"));
        sb.AppendLine(Entry("ContextMenuTextColor"));
        sb.AppendLine(Entry("ContextMenuIconColor"));
        sb.AppendLine(Entry("ContextMenuShortcutColor"));
        sb.AppendLine(Entry("ContextMenuDisabledColor"));
        sb.AppendLine(Entry("ContextMenuDisabledIconColor"));
        sb.AppendLine(Entry("ContextMenuHighlightShortcutColor"));
        sb.AppendLine(Entry("ContextMenuOuterBackgroundColor", last: true));
        sb.AppendLine("  },");
        sb.AppendLine();

        sb.AppendLine("  // ── その他（進捗バー・オーバーレイ・テレメトリー） ──");
        sb.AppendLine("  \"Misc\": {");
        sb.AppendLine(Entry("GlowBarGlowColor"));
        sb.AppendLine(Entry("ScanBarColor"));
        sb.AppendLine(Entry("LoadingOverlayColor"));
        sb.AppendLine(Entry("ShadowColor"));
        sb.AppendLine(Entry("TelemetryBackgroundColor"));
        sb.AppendLine(Entry("TelemetryBorderColor"));
        sb.AppendLine(Entry("TelemetryTitleColor"));
        sb.AppendLine(Entry("TelemetryValueColor", last: true));
        sb.AppendLine("  }");

        sb.AppendLine("}");
        return sb.ToString();
    }
}
