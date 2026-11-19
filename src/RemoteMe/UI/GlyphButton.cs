using System.ComponentModel;

namespace RemoteMe.UI;

// A flat button with an icon from the Segoe MDL2 Assets font. The button centers the icon by its ink,
// not by the line box of the font, which put the icons two pixels too high at 150% (decided on
// 2026-11-19). The background, hover, press and focus are the button's own.
[DesignerCategory("Code")]
internal sealed class GlyphButton : Button {
    private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    private string _glyph = "";
    private Rectangle? _ink;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Glyph {
        get => _glyph;
        set {
            if (_glyph != value) {
                _glyph = value;
                _ink = null;
                Invalidate();
            }
        }
    }

    protected override void OnFontChanged(EventArgs e) {
        _ink = null;
        base.OnFontChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent) {
        base.OnPaint(pevent);
        if (_glyph.Length == 0) {
            return;
        }
        var ink = _ink ??= Ink();
        var origin = new Point((ClientSize.Width - ink.Width) / 2 - ink.X, (ClientSize.Height - ink.Height) / 2 - ink.Y);
        TextRenderer.DrawText(pevent.Graphics, _glyph, Font, origin, Enabled ? ForeColor : SystemColors.GrayText, Flags);
    }

    // Where the ink of the glyph is in the box TextRenderer draws it in, measured at the DPI of the button.
    private Rectangle Ink() {
        var size = TextRenderer.MeasureText(_glyph, Font, Size.Empty, Flags);
        using var screen = CreateGraphics();
        using var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), screen);
        using (var graphics = Graphics.FromImage(bitmap)) {
            graphics.Clear(Color.White);
            TextRenderer.DrawText(graphics, _glyph, Font, Point.Empty, Color.Black, Flags);
        }
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < bitmap.Height; y++) {
            for (var x = 0; x < bitmap.Width; x++) {
                if (bitmap.GetPixel(x, y).R < 128) {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        return right < 0 ? new Rectangle(Point.Empty, size) : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
