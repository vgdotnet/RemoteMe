namespace RemoteMe.UI;

// Colors of the connection list and its buttons, mixed from the system colors so they follow the
// Windows theme and accent color.
internal static class Palette {
    public static Color Hover => Tint(0.10);

    public static Color Pressed => Tint(0.22);

    public static Color GroupBand => Tint(0.16);

    public static Color GroupBandHover => Tint(0.24);

    public static Color GroupCount => Tint(0.32);

    public static Color Separator => Blend(SystemColors.Window, SystemColors.ControlDark, 0.35);

    public static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static Color Tint(double amount) => Blend(SystemColors.Window, SystemColors.Highlight, amount);
}
