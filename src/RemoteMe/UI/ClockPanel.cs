using System.ComponentModel;
using System.Globalization;
using Microsoft.Win32;
using RemoteMe.Core.Clock;

namespace RemoteMe.UI;

// The clock above the list buttons: the date, the time of the chosen zone in large digits, and the zone's
// city with its UTC offset. A click raises Click, which asks for another zone.
[DesignerCategory("Code")]
internal sealed class ClockPanel : Control {
    public const string LocalName = "Local time";

    private const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private readonly System.Windows.Forms.Timer _timer = new();
    private string? _zoneId;
    private TimeZoneInfo? _zone;
    private Font? _timeFont;
    private int _padding;
    private int _gap;
    private int _lineHeight;
    private int _timeHeight;
    private (int Top, int Height) _textInk;
    private (int Top, int Height) _timeInk;
    private bool _hover;
    private bool _pressed;

    public ClockPanel() {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable,
            true);
        TabStop = true;
        BackColor = SystemColors.Window;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        _timer.Tick += (_, _) => ShowTime();
        SystemEvents.TimeChanged += OnSystemTimeChanged;
        UpdateMetrics();
        ShowZone();
    }

    // A TimeZoneInfo id; null, or an id this computer does not know, shows the local time.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ZoneId {
        get => _zoneId;
        set {
            _zoneId = value;
            ShowZone();
        }
    }

    [Browsable(false)]
    public string ZoneName => _zone?.DisplayName ?? $"{LocalName}: {TimeZoneInfo.Local.DisplayName}";

    // The narrowest width that shows the date, the time and the city with its offset whole.
    [Browsable(false)]
    public int MinimumWidth { get; private set; }

    public event EventHandler? MinimumWidthChanged;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            SystemEvents.TimeChanged -= OnSystemTimeChanged;
            _timer.Dispose();
            _timeFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        UpdateMetrics();
        ShowTime();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) {
        base.OnDpiChangedAfterParent(e);
        UpdateMetrics();
    }

    protected override void OnFontChanged(EventArgs e) {
        base.OnFontChanged(e);
        UpdateMetrics();
    }

    protected override void OnPaint(PaintEventArgs e) {
        var graphics = e.Graphics;
        graphics.Clear(_pressed ? Palette.Pressed : _hover ? Palette.Hover : BackColor);
        var (date, time, place) = Lines();

        var top = _padding;
        DrawLine(graphics, date, Font, _textInk, _lineHeight, top, SystemColors.GrayText);
        top += _textInk.Height + _gap;
        DrawLine(graphics, time, _timeFont ?? Font, _timeInk, _timeHeight, top, ForeColor);
        top += _timeInk.Height + _gap;
        DrawLine(graphics, place, Font, _textInk, _lineHeight, top, SystemColors.GrayText);

        if (Focused && ShowFocusCues) {
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
        }
    }

    protected override void OnGotFocus(EventArgs e) {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e) {
        base.OnLostFocus(e);
        Invalidate();
    }

    // A Control raises Click for any mouse button; only the left one, and Enter or Space, ask for a zone.
    protected override void OnClick(EventArgs e) {
        if (e is MouseEventArgs { Button: not MouseButtons.Left }) {
            return;
        }
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space) {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || base.IsInputKey(keyData);

    protected override void OnMouseEnter(EventArgs e) {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    private void UpdateMetrics() {
        _timeFont?.Dispose();
        _timeFont = new Font(Font.FontFamily, Font.SizeInPoints * 2.4f);
        _padding = LogicalToDeviceUnits(12);
        _gap = LogicalToDeviceUnits(6);
        _lineHeight = TextRenderer.MeasureText("Ag", Font, Size.Empty, TextFlags).Height;
        _timeHeight = TextRenderer.MeasureText("00:00", _timeFont, Size.Empty, TextFlags).Height;
        _textInk = Ink("H0", Font);
        _timeInk = Ink("00:00", _timeFont);
        Height = _padding * 2 + _textInk.Height * 2 + _timeInk.Height + _gap * 2;
        UpdateMinimumWidth();
        Invalidate();
    }

    private (string Date, string Time, string Place) Lines() {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _zone ?? TimeZoneInfo.Local);
        var city = _zone is null ? LocalName : ClockZone.CityName(_zone);
        return (
            now.ToString("ddd d MMM", CultureInfo.InvariantCulture),
            now.ToString("HH:mm", CultureInfo.InvariantCulture),
            $"{city} · {ClockZone.FormatOffset(now.Offset)}");
    }

    // Measured again with the zone, the font and every minute, since the date and the offset change.
    private void UpdateMinimumWidth() {
        if (_timeFont is null) {
            return;
        }
        var (date, time, place) = Lines();
        var widest = Math.Max(
            Math.Max(TextRenderer.MeasureText(date, Font, Size.Empty, TextFlags).Width, TextRenderer.MeasureText(place, Font, Size.Empty, TextFlags).Width),
            TextRenderer.MeasureText(time, _timeFont, Size.Empty, TextFlags).Width);
        var width = _padding * 2 + widest;
        if (width != MinimumWidth) {
            MinimumWidth = width;
            MinimumWidthChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Lines are put by the ink of their capitals and digits, not by the box TextRenderer draws in, so the
    // gaps between the date, the time and the city look the same.
    private void DrawLine(Graphics graphics, string text, Font font, (int Top, int Height) ink, int height, int inkTop, Color color) {
        var bounds = new Rectangle(_padding, inkTop - ink.Top, ClientSize.Width - _padding * 2, height);
        TextRenderer.DrawText(graphics, text, font, bounds, color, TextFlags | TextFormatFlags.EndEllipsis);
    }

    // Where the ink of the sample starts under the top of its box, and how tall it is.
    private static (int Top, int Height) Ink(string sample, Font font) {
        var size = TextRenderer.MeasureText(sample, font, Size.Empty, TextFlags);
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap)) {
            graphics.Clear(Color.White);
            TextRenderer.DrawText(graphics, sample, font, Point.Empty, Color.Black, TextFlags);
        }
        int top = -1, bottom = -1;
        for (var y = 0; y < bitmap.Height; y++) {
            for (var x = 0; x < bitmap.Width; x++) {
                if (bitmap.GetPixel(x, y).R < 128) {
                    top = top < 0 ? y : top;
                    bottom = y;
                    break;
                }
            }
        }
        return top < 0 ? (0, size.Height) : (top, bottom - top + 1);
    }

    private void ShowZone() {
        _zone = ClockZone.Find(_zoneId);
        AccessibleName = $"Clock, {(_zone is null ? LocalName : ClockZone.CityName(_zone))}. Change the time zone";
        UpdateMinimumWidth();
        Invalidate();
    }

    // Repaints and sets the timer to the start of the next minute.
    private void ShowTime() {
        UpdateMinimumWidth();
        Invalidate();
        var now = DateTime.Now;
        _timer.Interval = Math.Max(1, 60_000 - now.Second * 1000 - now.Millisecond + 50);
        _timer.Start();
    }

    // Also raised when the Windows time zone changes, which TimeZoneInfo.Local caches.
    private void OnSystemTimeChanged(object? sender, EventArgs e) {
        if (!IsHandleCreated) {
            return;
        }
        BeginInvoke(() => {
            TimeZoneInfo.ClearCachedData();
            ShowZone();
            ShowTime();
        });
    }
}
