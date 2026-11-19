using System.ComponentModel;

namespace RemoteMe.UI;

// A panel with the doodle wallpaper behind its controls; the controls on it have a transparent back
// color, so the wallpaper goes on under them.
[DesignerCategory("Code")]
internal sealed class WallpaperPanel : Panel {
    private WallpaperTheme _theme = Wallpaper.Themes[0];

    public WallpaperPanel() {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WallpaperTheme Theme {
        get => _theme;
        set {
            _theme = value;
            Invalidate(true);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Wallpaper.Paint(e.Graphics, ClientRectangle, _theme, DeviceDpi);
}
