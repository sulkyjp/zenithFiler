namespace ZenithFiler.Models;

public class ThemeModel
{
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? Category { get; set; }
    public ThemeBaseColors? Base { get; set; }
    public ThemeListColors? List { get; set; }
    public ThemeSearchColors? Search { get; set; }
    public ThemeAccentColors? Accent { get; set; }
    public ThemeContextMenuColors? ContextMenu { get; set; }
    public ThemeMiscColors? Misc { get; set; }
}

public class ThemeBaseColors
{
    public string? BackgroundColor { get; set; }
    public string? SidebarColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? TitleBarColor { get; set; }
    public string? TitleBarTextColor { get; set; }
    public string? BorderColor { get; set; }
    public string? PrimaryTextColor { get; set; }
    public string? SecondaryTextColor { get; set; }
    public string? InputBackgroundColor { get; set; }
    public string? FocusedPaneBackgroundColor { get; set; }
    public string? FocusedPaneTextColor { get; set; }
    public string? FocusedPaneHeaderBackgroundColor { get; set; }
    public string? OnePointDarkColor { get; set; }
    public string? TabSeparatorColor { get; set; }
    public string? TabHoverColor { get; set; }
    public string? ToolbarSeparatorColor { get; set; }
    public string? TreeLineColor { get; set; }
    public string? PopupBackgroundColor { get; set; }
    public string? PopupTextColor { get; set; }
    public string? PopupHoverBackgroundColor { get; set; }
}

public class ThemeListColors
{
    public string? ListHoverColor { get; set; }
    public string? SelectionColor { get; set; }
    public string? ButtonHoverColor { get; set; }
    public string? InactiveSelectionColor { get; set; }
    public string? OptionSelectedColor { get; set; }
    public string? OptionSelectedHoverColor { get; set; }
    public string? CheckboxHoverColor { get; set; }
    public string? CheckedHoverAccentColor { get; set; }
    public string? FilterActiveIndicatorColor { get; set; }
    public string? FilterChipBackgroundColor { get; set; }
    public string? FilterChipBorderColor { get; set; }
    public string? FilterToggleCheckedBackgroundColor { get; set; }
}

public class ThemeSearchColors
{
    public string? IndexSearchBackgroundColor { get; set; }
    public string? IndexSearchIconColor { get; set; }
    public string? IndexSearchFocusBorderColor { get; set; }
}

public class ThemeAccentColors
{
    public string? AccentColor { get; set; }
    public string? ErrorTextColor { get; set; }
    public string? DestructiveHoverBackgroundColor { get; set; }
    public string? DestructiveIconColor { get; set; }
    public string? SuccessColor { get; set; }
}

public class ThemeContextMenuColors
{
    public string? ContextMenuSeparatorColor { get; set; }
    public string? ContextMenuBackgroundColor { get; set; }
    public string? ContextMenuBorderColor { get; set; }
    public string? ContextMenuTextColor { get; set; }
    public string? ContextMenuIconColor { get; set; }
    public string? ContextMenuShortcutColor { get; set; }
    public string? ContextMenuDisabledColor { get; set; }
    public string? ContextMenuDisabledIconColor { get; set; }
    public string? ContextMenuHighlightShortcutColor { get; set; }
    public string? ContextMenuOuterBackgroundColor { get; set; }
}

public class ThemeMiscColors
{
    public string? GlowBarGlowColor { get; set; }
    public string? ScanBarColor { get; set; }
    public string? LoadingOverlayColor { get; set; }
    public string? ShadowColor { get; set; }
    public string? TelemetryBackgroundColor { get; set; }
    public string? TelemetryBorderColor { get; set; }
    public string? TelemetryTitleColor { get; set; }
    public string? TelemetryValueColor { get; set; }
    public string? PreviewPanelBorderColor { get; set; }
    public string? PreviewPdfBackgroundColor { get; set; }
    public string? IndexStatusNormalColor { get; set; }
    public string? IndexStatusWarmColor { get; set; }
}
