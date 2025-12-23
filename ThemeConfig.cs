namespace APPID;

/// <summary>
///     ★★★ CHANGE YOUR THEME COLORS HERE ★★★
/// </summary>
public static class ThemeConfig
{
    // ═══════════════════════════════════════════════════════════════
    // ACRYLIC BLUR SETTINGS
    // ═══════════════════════════════════════════════════════════════
    // Format: 0xAABBGGRR (ABGR format - backwards from normal RGB!)
    // AA = Opacity (00=fully transparent, FF=fully opaque)
    // BB = Blue amount
    // GG = Green amount
    // RR = Red amount
    //
    // Examples:
    // 0xBB0A0A0F = Dark blue-ish with 73% opacity (current)
    // 0xCC000000 = Pure black with 80% opacity
    // 0xAA1A1A2F = Slightly more blue with 67% opacity
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Main acrylic blur color for all windows
    ///     Change this to adjust the frosted glass effect!
    /// </summary>
    public static int AcrylicBlurColor = unchecked((int)0xFE050814); // 99% opacity - just under max

    /// <summary>
    ///     Blur intensity (0-2)
    ///     0 = Minimal blur
    ///     1 = Medium blur
    ///     2 = Maximum blur
    /// </summary>
    public static int BlurIntensity = 0;


    // ═══════════════════════════════════════════════════════════════
    // BACKGROUND COLORS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Main window background color</summary>
    public static Color MainWindowBackground { get; } = Color.FromArgb(5, 8, 20);

    /// <summary>Share window background color</summary>
    public static Color ShareWindowBackground { get; } = Color.FromArgb(5, 8, 15);

    /// <summary>Dark controls background (search boxes, dropdowns, etc)</summary>
    public static Color DarkControlBackground { get; } = Color.FromArgb(8, 8, 12);

    /// <summary>Grid background color</summary>
    public static Color GridBackground { get; } = Color.FromArgb(5, 5, 8);

    /// <summary>Grid separator lines color</summary>
    public static Color GridLineColor { get; } = Color.FromArgb(30, 35, 45);


    // ═══════════════════════════════════════════════════════════════
    // TEXT COLORS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Primary text color (cyan-ish)</summary>
    public static Color PrimaryText { get; } = Color.FromArgb(192, 255, 255);

    /// <summary>Accent text color (light blue)</summary>
    public static Color AccentText { get; } = Color.FromArgb(100, 200, 255);

    /// <summary>Share Clean button color (green)</summary>
    public static Color ShareCleanColor { get; } = Color.FromArgb(100, 255, 150);

    /// <summary>Share Cracked button color (purple/magenta)</summary>
    public static Color ShareCrackedColor { get; } = Color.FromArgb(255, 100, 255);
}
