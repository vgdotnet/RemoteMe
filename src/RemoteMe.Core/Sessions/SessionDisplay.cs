namespace RemoteMe.Core.Sessions;

// The remote desktop size and scale for a session area of the given size in physical pixels.
public readonly record struct SessionDisplay(
    uint Width,
    uint Height,
    uint PhysicalWidthMm,
    uint PhysicalHeightMm,
    uint DesktopScaleFactor,
    uint DeviceScaleFactor) {

    private const int MinSize = 200;
    private const int MaxSize = 8192;

    public static SessionDisplay From(int width, int height, int dpi) {
        ArgumentOutOfRangeException.ThrowIfLessThan(dpi, 1);
        var desktopWidth = Math.Clamp(width & ~1, MinSize, MaxSize);
        var desktopHeight = Math.Clamp(height, MinSize, MaxSize);
        var scale = Math.Clamp((int)Math.Round(dpi * 100.0 / 96), 100, 500);
        // Device scale factors are 100, 140 or 180 (as remembered from the component's documentation;
        // 140 at 150% worked in the stage 1 prototype).
        var deviceScale = scale < 140 ? 100 : scale < 180 ? 140 : 180;
        return new SessionDisplay(
            (uint)desktopWidth,
            (uint)desktopHeight,
            (uint)Math.Round(desktopWidth * 25.4 / dpi),
            (uint)Math.Round(desktopHeight * 25.4 / dpi),
            (uint)scale,
            (uint)deviceScale);
    }
}
