using System.ComponentModel;
using System.Globalization;
using Microsoft.Win32;
using RemoteMe.Core.Clock;

namespace RemoteMe.UI;

// The clock above the list buttons: the time of the chosen zone in large digits, and under it the zone's
// city, UTC offset and date. A click raises Click, which asks for another zone.
[DesignerCategory("Code")]
internal sealed class ClockPanel : Control {
    public const string LocalName = "Local time";

    private const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private readonly System.Windows.Forms.Timer _timer = new();
    private string? _zoneId;
    private TimeZoneInfo? _zone;
    private Font? _timeFont;
    private int _padding;
    private int _top;
    private int _timeHeight;
    private int _lineHeight;
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
        var zone = _zone ?? TimeZoneInfo.Local;
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var width = ClientSize.Width - _padding * 2;
        var time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        var offset = ClockZone.FormatOffset(now.Offset);
        var date = now.ToString("ddd d MMM", CultureInfo.InvariantCulture);
        var city = _zone is null ? LocalName : ClockZone.CityName(_zone);
        var timeFont = _timeFont ?? Font;

        TextRenderer.DrawText(graphics, time, timeFont, new Rectangle(_padding, _top, width, _timeHeight), ForeColor, TextFlags);
        var offsetTop = _top + Ascent(timeFont) - Ascent(Font);
        TextRenderer.DrawText(graphics, offset, Font, new Rectangle(_padding, offsetTop, width, _lineHeight),
            SystemColors.GrayText, TextFlags | TextFormatFlags.Right);

        var secondTop = _top + _timeHeight;
        var dateWidth = TextRenderer.MeasureText(graphics, date, Font, Size.Empty, TextFlags).Width;
        TextRenderer.DrawText(graphics, date, Font, new Rectangle(_padding, secondTop, width, _lineHeight),
            SystemColors.GrayText, TextFlags | TextFormatFlags.Right);
        TextRenderer.DrawText(graphics, city, Font, new Rectangle(_padding, secondTop, Math.Max(0, width - dateWidth - _padding), _lineHeight),
            SystemColors.GrayText, TextFlags | TextFormatFlags.EndEllipsis);

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
        _top = LogicalToDeviceUnits(4);
        _timeHeight = TextRenderer.MeasureText("00:00", _timeFont, Size.Empty, TextFlags).Height;
        _lineHeight = TextRenderer.MeasureText("Ag", Font, Size.Empty, TextFlags).Height;
        Height = _top + _timeHeight + _lineHeight + LogicalToDeviceUnits(4);
        Invalidate();
    }

    private void ShowZone() {
        _zone = ClockZone.Find(_zoneId);
        AccessibleName = $"Clock, {(_zone is null ? LocalName : ClockZone.CityName(_zone))}. Change the time zone";
        Invalidate();
    }

    // Repaints and sets the timer to the start of the next minute.
    private void ShowTime() {
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

    // The distance from the top of the text to its baseline, to put the offset on the time's baseline.
    private int Ascent(Font font) {
        var family = font.FontFamily;
        return (int)Math.Round(font.SizeInPoints * DeviceDpi / 72f * family.GetCellAscent(font.Style) / family.GetEmHeight(font.Style));
    }
}
