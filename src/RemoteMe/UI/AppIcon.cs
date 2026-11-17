namespace RemoteMe.UI;

// The icon of every RemoteMe window. The exe carries the same file through ApplicationIcon, but forms
// do not take it from there.
internal static class AppIcon {
    private static Icon? _icon;

    public static Icon Value => _icon ??= Load();

    private static Icon Load() {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("RemoteMe.ico")
            ?? throw new InvalidOperationException("The application icon is missing from the resources.");
        return new Icon(stream);
    }
}
