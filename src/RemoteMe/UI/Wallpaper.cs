using System.Drawing.Drawing2D;

namespace RemoteMe.UI;

internal sealed record WallpaperTheme(string Name, Color Base, Color Light);

// The background of the left panel: a soft gradient with a light pattern of small doodles drawn here in
// code (monitors, clouds, coffee, rockets and the like), repeated as tiles.
internal static class Wallpaper {
    private const int TileSize = 400;
    private const int Cells = 8;
    private const float Unit = 24f;

    private static readonly Dictionary<(string Theme, int Dpi), Bitmap> Tiles = [];

    private static readonly Action<Graphics, Pen>[] Doodles = [
        Monitor, Cloud, Coffee, Rocket, Cat, Wifi, Plane, Laptop, Planet, Mouse, Heart, Gear, Plug, Star,
        Headphones, Bulb, Keyboard, Cursor, Server, Sun, Squiggle,
    ];

    public static IReadOnlyList<WallpaperTheme> Themes { get; } = [
        new("Green", Color.FromArgb(0xA9, 0xCC, 0x9E), Color.FromArgb(0xD8, 0xEA, 0xC9)),
        new("Blue", Color.FromArgb(0x9F, 0xC3, 0xE4), Color.FromArgb(0xD6, 0xE7, 0xF5)),
        new("Peach", Color.FromArgb(0xF0, 0xC0, 0x9E), Color.FromArgb(0xFB, 0xE5, 0xD2)),
        new("Lavender", Color.FromArgb(0xC2, 0xB4, 0xE3), Color.FromArgb(0xE7, 0xE0, 0xF6)),
        new("Mint", Color.FromArgb(0x94, 0xD1, 0xC4), Color.FromArgb(0xD2, 0xEE, 0xE7)),
        new("Sand", Color.FromArgb(0xE3, 0xD3, 0x96), Color.FromArgb(0xF5, 0xEE, 0xCF)),
    ];

    // The theme of a name from ListBackground.Names; an unknown name is the first theme.
    public static WallpaperTheme Theme(string name) => Themes.FirstOrDefault(t => t.Name == name) ?? Themes[0];

    public static void Paint(Graphics graphics, Rectangle bounds, WallpaperTheme theme, int dpi) {
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }
        PaintGradient(graphics, bounds, theme);
        using var texture = new TextureBrush(Tile(theme, dpi), WrapMode.Tile);
        texture.TranslateTransform(bounds.Left, bounds.Top);
        graphics.FillRectangle(texture, bounds);
    }

    private static void PaintGradient(Graphics graphics, Rectangle bounds, WallpaperTheme theme) {
        using var gradient = new LinearGradientBrush(bounds, theme.Base, theme.Light, 45f);
        graphics.FillRectangle(gradient, bounds);
    }

    // The doodles on a transparent tile, in a darker shade of the theme, made once for a theme and a DPI.
    // Rows are shifted by half a cell like bricks, and sizes, turns and places vary a little, so the
    // pattern does not look like a grid; every doodle is also drawn a tile away on each side, so the ones
    // that cross an edge go on in the next tile.
    private static Bitmap Tile(WallpaperTheme theme, int dpi) {
        if (Tiles.TryGetValue((theme.Name, dpi), out var cached)) {
            return cached;
        }
        var scale = dpi / 96f;
        var size = (int)Math.Round(TileSize * scale);
        var tile = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(tile)) {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var ink = Color.FromArgb(120, Palette.Blend(theme.Base, Color.Black, 0.35));
            var width = 1.4f * scale;
            var random = new Random(19);
            var cell = size / (float)Cells;
            var order = Enumerable.Range(0, Cells * Cells).Select(i => Doodles[i % Doodles.Length]).OrderBy(_ => random.Next()).ToArray();
            Action<Graphics, Pen>[] fillers = [Dot, Plus, Ring, Dot, Star];
            float Jitter(double part) => (float)((random.NextDouble() * 2 - 1) * part * cell);
            for (var row = 0; row < Cells; row++) {
                var shift = row % 2 == 0 ? 0 : cell / 2;
                for (var column = 0; column < Cells; column++) {
                    var x = column * cell + cell / 2 + shift + Jitter(0.1);
                    var y = row * cell + cell / 2 + Jitter(0.1);
                    var doodle = cell * (0.58f + (float)random.NextDouble() * 0.12f);
                    var angle = (float)(random.NextDouble() * 50 - 25);
                    Wrapped(graphics, order[row * Cells + column], ink, width, x, y, doodle, angle, size);
                    if (random.NextDouble() < 0.85) {
                        var filler = fillers[random.Next(fillers.Length)];
                        var small = cell * (0.12f + (float)random.NextDouble() * 0.06f);
                        Wrapped(graphics, filler, ink, width, column * cell + shift + Jitter(0.08), row * cell + Jitter(0.08), small, (float)(random.NextDouble() * 90), size);
                    }
                }
            }
        }
        Tiles[(theme.Name, dpi)] = tile;
        return tile;
    }

    private static void Wrapped(Graphics graphics, Action<Graphics, Pen> doodle, Color ink, float width, float x, float y, float size, float angle, int tile) {
        for (var dx = -tile; dx <= tile; dx += tile) {
            for (var dy = -tile; dy <= tile; dy += tile) {
                Draw(graphics, doodle, ink, width, x + dx, y + dy, size, angle);
            }
        }
    }

    // Draws a doodle made on a 24 by 24 grid, centered at x, y, size pixels wide and turned by angle.
    private static void Draw(Graphics graphics, Action<Graphics, Pen> doodle, Color ink, float width, float x, float y, float size, float angle) {
        var state = graphics.Save();
        graphics.TranslateTransform(x, y);
        graphics.RotateTransform(angle);
        graphics.ScaleTransform(size / Unit, size / Unit);
        graphics.TranslateTransform(-Unit / 2, -Unit / 2);
        using var pen = new Pen(ink, width * Unit / size) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        doodle(graphics, pen);
        graphics.Restore(state);
    }

    private static void Round(Graphics g, Pen pen, float x, float y, float width, float height, float radius) {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static void Monitor(Graphics g, Pen p) {
        Round(g, p, 2, 4, 20, 13, 2);
        g.DrawLine(p, 12, 17, 12, 20);
        g.DrawLine(p, 8, 20.5f, 16, 20.5f);
    }

    private static void Laptop(Graphics g, Pen p) {
        Round(g, p, 5, 5, 14, 10, 1.5f);
        g.DrawLine(p, 2, 18, 22, 18);
        g.DrawLine(p, 4, 15, 2, 18);
        g.DrawLine(p, 20, 15, 22, 18);
    }

    private static void Mouse(Graphics g, Pen p) {
        Round(g, p, 7, 5, 10, 15, 5);
        g.DrawLine(p, 12, 5, 12, 10);
        g.DrawBezier(p, 12, 5, 12, 2, 16, 3, 18, 1);
    }

    private static void Cloud(Graphics g, Pen p) {
        using var path = new GraphicsPath();
        path.AddArc(2, 10, 8, 8, 90, 180);
        path.AddArc(6, 5, 11, 11, 180, 180);
        path.AddArc(14, 10, 8, 8, 270, 180);
        path.CloseFigure();
        g.DrawPath(p, path);
    }

    private static void Wifi(Graphics g, Pen p) {
        foreach (var r in new[] { 4f, 8f, 12f }) {
            g.DrawArc(p, 12 - r, 20 - r, r * 2, r * 2, 225, 90);
        }
        g.DrawEllipse(p, 11, 19, 2, 2);
    }

    private static void Star(Graphics g, Pen p) {
        var points = new PointF[10];
        for (var i = 0; i < 10; i++) {
            var radius = i % 2 == 0 ? 10 : 4.2;
            var a = -Math.PI / 2 + i * Math.PI / 5;
            points[i] = new PointF(12 + (float)(radius * Math.Cos(a)), 12.5f + (float)(radius * Math.Sin(a)));
        }
        g.DrawPolygon(p, points);
    }

    private static void Plug(Graphics g, Pen p) {
        Round(g, p, 7, 7, 10, 8, 2);
        g.DrawLine(p, 10, 7, 10, 3);
        g.DrawLine(p, 14, 7, 14, 3);
        g.DrawBezier(p, 12, 15, 12, 20, 6, 18, 6, 23);
    }

    private static void Coffee(Graphics g, Pen p) {
        Round(g, p, 4, 9, 12, 11, 2);
        g.DrawArc(p, 13, 11, 6, 6, 270, 180);
        g.DrawBezier(p, 8, 7, 6, 5, 10, 4, 8, 2);
        g.DrawBezier(p, 12, 7, 10, 5, 14, 4, 12, 2);
    }

    private static void Plane(Graphics g, Pen p) {
        g.DrawPolygon(p, [new PointF(2, 12), new PointF(22, 3), new PointF(15, 21), new PointF(11, 14)]);
        g.DrawLine(p, 11, 14, 22, 3);
    }

    private static void Rocket(Graphics g, Pen p) {
        using var body = new GraphicsPath();
        body.AddBezier(12, 2, 17, 6, 16, 12, 15, 16);
        body.AddLine(15, 16, 9, 16);
        body.AddBezier(9, 16, 8, 12, 7, 6, 12, 2);
        g.DrawPath(p, body);
        g.DrawEllipse(p, 10, 7, 4, 4);
        g.DrawLines(p, [new PointF(8.5f, 12), new PointF(5, 17), new PointF(9, 16)]);
        g.DrawLines(p, [new PointF(15.5f, 12), new PointF(19, 17), new PointF(15, 16)]);
        g.DrawLines(p, [new PointF(10, 18), new PointF(12, 22), new PointF(14, 18)]);
    }

    private static void Planet(Graphics g, Pen p) {
        g.DrawEllipse(p, 6, 6, 12, 12);
        var state = g.Save();
        g.TranslateTransform(12, 12);
        g.RotateTransform(-20);
        g.DrawArc(p, -11, -3.5f, 22, 7, 160, 220);
        g.Restore(state);
    }

    private static void Gear(Graphics g, Pen p) {
        var points = new List<PointF>();
        for (var i = 0; i < 8; i++) {
            var a = i * Math.PI / 4;
            foreach (var (offset, radius) in new[] { (-0.28, 7.5), (-0.16, 10.0), (0.16, 10.0), (0.28, 7.5) }) {
                points.Add(new PointF(12 + (float)(radius * Math.Cos(a + offset)), 12 + (float)(radius * Math.Sin(a + offset))));
            }
        }
        g.DrawPolygon(p, [.. points]);
        g.DrawEllipse(p, 9, 9, 6, 6);
    }

    private static void Bulb(Graphics g, Pen p) {
        g.DrawArc(p, 6, 2, 12, 12, 130, 280);
        g.DrawLine(p, 8.2f, 12.6f, 9.5f, 16);
        g.DrawLine(p, 15.8f, 12.6f, 14.5f, 16);
        g.DrawLine(p, 9.5f, 16, 14.5f, 16);
        g.DrawLine(p, 10, 18.5f, 14, 18.5f);
        g.DrawLine(p, 11, 21, 13, 21);
    }

    private static void Headphones(Graphics g, Pen p) {
        g.DrawArc(p, 4, 4, 16, 16, 180, 180);
        Round(g, p, 3, 12, 4, 7, 1.5f);
        Round(g, p, 17, 12, 4, 7, 1.5f);
    }

    private static void Cursor(Graphics g, Pen p) {
        g.DrawPolygon(p, [new PointF(6, 3), new PointF(6, 19), new PointF(10, 15), new PointF(13, 21), new PointF(15, 20), new PointF(12, 14), new PointF(18, 14)]);
    }

    private static void Heart(Graphics g, Pen p) {
        using var path = new GraphicsPath();
        path.AddBezier(12, 20, 2, 13, 4, 3, 12, 8);
        path.AddBezier(12, 8, 20, 3, 22, 13, 12, 20);
        g.DrawPath(p, path);
    }

    private static void Cat(Graphics g, Pen p) {
        g.DrawArc(p, 4, 7, 16, 14, 200, 140);
        g.DrawArc(p, 4, 7, 16, 14, 20, 140);
        g.DrawLines(p, [new PointF(5, 11), new PointF(6, 3), new PointF(10, 7.6f)]);
        g.DrawLines(p, [new PointF(19, 11), new PointF(18, 3), new PointF(14, 7.6f)]);
        g.DrawEllipse(p, 8.5f, 12, 1.6f, 1.6f);
        g.DrawEllipse(p, 13.9f, 12, 1.6f, 1.6f);
        g.DrawLine(p, 1, 15, 6, 15.5f);
        g.DrawLine(p, 23, 15, 18, 15.5f);
        g.DrawArc(p, 10, 14, 4, 3, 20, 140);
    }

    private static void Keyboard(Graphics g, Pen p) {
        Round(g, p, 1, 7, 22, 11, 1.5f);
        for (var x = 4f; x <= 19; x += 3.75f) {
            g.DrawLine(p, x, 10.5f, x + 1, 10.5f);
        }
        g.DrawLine(p, 7, 14.5f, 17, 14.5f);
    }

    private static void Server(Graphics g, Pen p) {
        Round(g, p, 4, 3, 16, 8, 1.5f);
        Round(g, p, 4, 13, 16, 8, 1.5f);
        g.DrawEllipse(p, 6.5f, 6, 1.6f, 1.6f);
        g.DrawEllipse(p, 6.5f, 16, 1.6f, 1.6f);
        g.DrawLine(p, 11, 7, 17, 7);
        g.DrawLine(p, 11, 17, 17, 17);
    }

    private static void Sun(Graphics g, Pen p) {
        g.DrawEllipse(p, 7.5f, 7.5f, 9, 9);
        for (var i = 0; i < 8; i++) {
            var a = i * Math.PI / 4;
            g.DrawLine(p, 12 + (float)(7 * Math.Cos(a)), 12 + (float)(7 * Math.Sin(a)), 12 + (float)(10 * Math.Cos(a)), 12 + (float)(10 * Math.Sin(a)));
        }
    }

    private static void Squiggle(Graphics g, Pen p) {
        g.DrawBeziers(p, [new PointF(1, 12), new PointF(4, 5), new PointF(8, 19), new PointF(12, 12), new PointF(16, 5), new PointF(20, 19), new PointF(23, 12)]);
    }

    private static void Dot(Graphics g, Pen p) => g.DrawEllipse(p, 10, 10, 4, 4);

    private static void Plus(Graphics g, Pen p) {
        g.DrawLine(p, 12, 6, 12, 18);
        g.DrawLine(p, 6, 12, 18, 12);
    }

    private static void Ring(Graphics g, Pen p) => g.DrawEllipse(p, 5, 5, 14, 14);
}
