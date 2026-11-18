using Microsoft.Win32;

namespace RemoteMe.UI;

// Colors of the connection list and its buttons, mixed from the window color of Windows so they follow the
// theme. The hover and pressed states take the selection color; the group captions take the accent color
// of the Windows settings, and in high contrast the selection color instead.
internal static class Palette {
    private static Color? _accent;

    public static Color Accent => _accent ??= ReadAccent();

    public static Color Hover => Tint(SystemColors.Highlight, 0.10);

    public static Color Pressed => Tint(SystemColors.Highlight, 0.22);

    public static Color GroupBand => Tint(Accent, 0.16);

    public static Color GroupBandHover => Tint(Accent, 0.24);

    public static Color GroupCount => Tint(Accent, 0.32);

    public static Color Separator => Blend(SystemColors.Window, SystemColors.ControlDark, 0.35);

    public static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    // The accent color is read once and again after Windows reports a change.
    public static void ResetAccent() => _accent = null;

    // The value the Windows settings save, as 0xAABBGGRR; DwmGetColorizationColor gives a darker shade of it.
    private static Color ReadAccent() {
        if (!SystemInformation.HighContrast
            && Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int color) {
            return Color.FromArgb(color & 0xFF, (color >> 8) & 0xFF, (color >> 16) & 0xFF);
        }
        return SystemColors.Highlight;
    }

    private static Color Tint(Color color, double amount) => Blend(SystemColors.Window, color, amount);
}
