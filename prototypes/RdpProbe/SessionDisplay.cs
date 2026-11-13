namespace RdpProbe;

internal readonly record struct SessionDisplay(
    uint Width,
    uint Height,
    uint PhysicalWidthMm,
    uint PhysicalHeightMm,
    uint DesktopScaleFactor,
    uint DeviceScaleFactor) {

    private const int MinSize = 200;
    private const int MaxSize = 8192;

    public static SessionDisplay From(int clientWidth, int clientHeight, int dpi, bool passScale) {
        var width = Math.Clamp(clientWidth & ~1, MinSize, MaxSize);
        var height = Math.Clamp(clientHeight, MinSize, MaxSize);
        var scale = passScale ? Math.Clamp((int)Math.Round(dpi * 100.0 / 96), 100, 500) : 100;
        // Allowed device scale factors are 100, 140 and 180 (from memory, to be verified).
        var deviceScale = scale < 140 ? 100 : scale < 180 ? 140 : 180;
        return new SessionDisplay(
            (uint)width,
            (uint)height,
            (uint)Math.Round(width * 25.4 / dpi),
            (uint)Math.Round(height * 25.4 / dpi),
            (uint)scale,
            (uint)deviceScale);
    }

    public override string ToString() =>
        $"{Width}x{Height}, {PhysicalWidthMm}x{PhysicalHeightMm} mm, desktop scale {DesktopScaleFactor}%, device scale {DeviceScaleFactor}%";
}
