using MahApps.Metro.IconPacks;

namespace ZenithFiler.Models;

/// <summary>テーマの雰囲気ベースカテゴリ。</summary>
public enum ThemeCategory
{
    Standard,
    WarmCozy,
    Professional,
    Premium,
    RetroTech
}

/// <summary>ThemeCategory の表示名・アイコン・ソート順を解決するヘルパー。</summary>
public static class ThemeCategoryInfo
{
    private static readonly Dictionary<ThemeCategory, (string DisplayName, PackIconLucideKind Icon, int SortOrder)> Map = new()
    {
        [ThemeCategory.Standard]     = ("スタンダード",           PackIconLucideKind.Star,      0),
        [ThemeCategory.WarmCozy]     = ("ウォーム & コージー",    PackIconLucideKind.Coffee,    1),
        [ThemeCategory.Professional] = ("プロフェッショナル",     PackIconLucideKind.Briefcase, 2),
        [ThemeCategory.Premium]      = ("プレミアム",             PackIconLucideKind.Crown,     3),
        [ThemeCategory.RetroTech]    = ("レトロ & テック",        PackIconLucideKind.Terminal,   4),
    };

    public static string GetDisplayName(ThemeCategory category) =>
        Map.TryGetValue(category, out var info) ? info.DisplayName : "スタンダード";

    public static PackIconLucideKind GetIconKind(ThemeCategory category) =>
        Map.TryGetValue(category, out var info) ? info.Icon : PackIconLucideKind.Star;

    public static int GetSortOrder(ThemeCategory category) =>
        Map.TryGetValue(category, out var info) ? info.SortOrder : 0;
}

public class ThemeInfo
{
    public string Name { get; }
    public string Description { get; }
    public string Author { get; }
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);

    /// <summary>テーマの雰囲気カテゴリ。</summary>
    public ThemeCategory Category { get; }

    /// <summary>バインディング用: カテゴリ表示名。</summary>
    public string CategoryDisplayName => ThemeCategoryInfo.GetDisplayName(Category);

    /// <summary>バインディング用: カテゴリアイコン Kind。</summary>
    public PackIconLucideKind CategoryIconKind => ThemeCategoryInfo.GetIconKind(Category);

    /// <summary>バインディング用: カテゴリソート順。</summary>
    public int CategorySortOrder => ThemeCategoryInfo.GetSortOrder(Category);

    /// <summary>スタンダードカテゴリ内で "standard" を先頭に固定するためのソートキー（0=standard, 1=その他）。</summary>
    public int StandardFirstSortKey => string.Equals(Name, "standard", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    /// <summary>テーマカードのカラーチップ用: 背景色。</summary>
    public string BackgroundColor { get; }
    /// <summary>テーマカードのカラーチップ用: アクセント色。</summary>
    public string AccentColor { get; }
    /// <summary>テーマカードのカラーチップ用: テキスト色。</summary>
    public string PrimaryTextColor { get; }
    /// <summary>テーマカードのカラーチップ用: サイドバー色。</summary>
    public string SidebarColor { get; }

    public ThemeInfo(string name, string? description = null, string? author = null,
        string? backgroundColor = null, string? accentColor = null,
        string? primaryTextColor = null, string? sidebarColor = null,
        ThemeCategory category = ThemeCategory.Standard)
    {
        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? "説明はありません" : description;
        Author = author ?? string.Empty;
        BackgroundColor = backgroundColor ?? "#F5F1E3";
        AccentColor = accentColor ?? "#268BD2";
        PrimaryTextColor = primaryTextColor ?? "#1A1A1A";
        SidebarColor = sidebarColor ?? "#E6E1D3";
        Category = category;
    }
}
