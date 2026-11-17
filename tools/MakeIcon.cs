#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true

// Draws the application icon, option B from docs/icons (chosen on 2026-11-17), at the sizes Windows
// uses and writes it to src/RemoteMe/Assets/RemoteMe.ico.
// Run from the repository root: dotnet run tools/MakeIcon.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
var output = args.Length > 0 ? args[0] : Path.Combine("src", "RemoteMe", "Assets", "RemoteMe.ico");
var images = sizes.Select(size => (Size: size, Bitmap: Draw(size))).ToList();
using (var stream = File.Create(output)) {
    WriteIcon(stream, images);
}
foreach (var (_, bitmap) in images) {
    bitmap.Dispose();
}
Console.WriteLine($"Wrote {output}");

// The drawing follows option-b-r-on-screen.svg on its 48x48 grid. Below 32 pixels the lines are drawn
// thicker, so that they stay at least about one and a half pixels wide.
static Bitmap Draw(int size) {
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    var scale = size / 48f;
    graphics.ScaleTransform(scale, scale);
    var line = Math.Max(3f, 1.5f / scale);

    using var tile = new SolidBrush(Color.FromArgb(0x0B, 0x7A, 0x75));
    using (var path = RoundedRectangle(2, 2, 44, 44, 10)) {
        graphics.FillPath(tile, path);
    }
    using var pen = new Pen(Color.White, line) {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round,
    };
    using (var path = RoundedRectangle(9.5f, 10.5f, 29, 21, 3)) {
        graphics.DrawPath(pen, path);
    }
    graphics.FillRectangle(Brushes.White, 21, 32, 6, 4);
    using (var path = RoundedRectangle(15, 36, 18, 3, 1.5f)) {
        graphics.FillPath(Brushes.White, path);
    }
    using (var letter = new GraphicsPath()) {
        letter.AddLine(19.5f, 27, 19.5f, 15.5f);
        letter.AddLine(19.5f, 15.5f, 24.5f, 15.5f);
        letter.AddArc(20.75f, 15.5f, 7.5f, 7.5f, 270, 180);
        letter.AddLine(24.5f, 23, 19.5f, 23);
        letter.StartFigure();
        letter.AddLine(24, 23, 28.5f, 27);
        graphics.DrawPath(pen, letter);
    }
    return bitmap;
}

static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius) {
    var diameter = radius * 2;
    var path = new GraphicsPath();
    path.AddArc(x, y, diameter, diameter, 180, 90);
    path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
    path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
    path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
    path.CloseFigure();
    return path;
}

// An .ico file: the 256-pixel image as PNG, the smaller ones as 32-bit bitmaps with alpha.
static void WriteIcon(Stream stream, List<(int Size, Bitmap Bitmap)> images) {
    var data = images.Select(image => image.Size >= 256 ? Png(image.Bitmap) : Dib(image.Bitmap)).ToList();
    using var writer = new BinaryWriter(stream);
    writer.Write((short)0);
    writer.Write((short)1);
    writer.Write((short)images.Count);
    var offset = 6 + 16 * images.Count;
    for (var i = 0; i < images.Count; i++) {
        var size = images[i].Size;
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(data[i].Length);
        writer.Write(offset);
        offset += data[i].Length;
    }
    foreach (var bytes in data) {
        writer.Write(bytes);
    }
}

static byte[] Png(Bitmap bitmap) {
    using var memory = new MemoryStream();
    bitmap.Save(memory, ImageFormat.Png);
    return memory.ToArray();
}

static byte[] Dib(Bitmap bitmap) {
    var size = bitmap.Width;
    var maskStride = (size + 31) / 32 * 4;
    using var memory = new MemoryStream();
    using var writer = new BinaryWriter(memory);
    writer.Write(40);
    writer.Write(size);
    writer.Write(size * 2);
    writer.Write((short)1);
    writer.Write((short)32);
    writer.Write(0);
    writer.Write(size * size * 4 + maskStride * size);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    var bits = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try {
        var row = new byte[size * 4];
        for (var y = size - 1; y >= 0; y--) {
            System.Runtime.InteropServices.Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, row.Length);
            writer.Write(row);
        }
    } finally {
        bitmap.UnlockBits(bits);
    }
    writer.Write(new byte[maskStride * size]);
    writer.Flush();
    return memory.ToArray();
}
