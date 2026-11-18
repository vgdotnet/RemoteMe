using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using RemoteMe.Core.Model;

namespace RemoteMe.UI;

// The owner-drawn connection list: group captions with a collapse arrow, two-line connection rows
// with a status dot, a menu button on hover, and reordering by dragging.
[DesignerCategory("Code")]
internal sealed class ConnectionList : ScrollableControl {
    private const TextFormatFlags TextFlags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
        | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

    private static readonly Color ConnectedColor = Color.FromArgb(0x2E, 0xA0, 0x43);
    private static readonly Color BusyColor = Color.FromArgb(0xE3, 0xA0, 0x08);
    private static readonly Color ErrorColor = Color.FromArgb(0xD1, 0x34, 0x38);

    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private Settings _settings = new();
    private IReadOnlyList<ListRow> _rows = [];
    private int[] _rowTops = [];
    private int _contentHeight;
    private Guid? _activeConnectionId;
    private int _hoverIndex = -1;
    private bool _hoverOnMenuButton;
    private int _focusIndex = -1;
    private int _pressIndex = -1;
    private int _pressClicks;
    private Point _pressPoint;
    private bool _dragging;
    private int _dropIndex = -1;
    private bool _dropUpperHalf;
    private Font? _boldFont;
    private int _lineHeight;
    private int _connectionRowHeight;
    private int _groupBandHeight;
    private int _padding;
    private int _dotSize;
    private int _textLeft;
    private int _menuButtonSize;
    private int _rowInset;
    private int _cornerRadius;
    private int _accentWidth;

    public ConnectionList() {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable,
            true);
        AutoScroll = true;
        TabStop = true;
        BackColor = SystemColors.Window;
        AccessibleName = "Connections";
        UpdateMetrics();
    }

    // A click or Space selects a connection; a double click or Enter also asks to connect it.
    public event EventHandler<ConnectionEventArgs>? ConnectionSelected;

    public event EventHandler<ConnectionEventArgs>? ConnectionActivated;

    public event EventHandler<ConnectionEventArgs>? DeleteRequested;

    public event EventHandler<ConnectionMenuEventArgs>? MenuRequested;

    public event EventHandler<ConnectionMoveEventArgs>? MoveRequested;

    public event EventHandler<GroupMoveEventArgs>? GroupMoveRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Guid? ActiveConnectionId {
        get => _activeConnectionId;
        set {
            _activeConnectionId = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Guid, ConnectionStatus> StatusOf { get; set; } = _ => ConnectionStatus.NotConnected;

    public void ShowSettings(Settings settings) {
        _settings = settings;
        _collapsedGroups.IntersectWith(settings.Groups);
        Rebuild();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _boldFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        UpdateMetrics();
        Rebuild();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) {
        base.OnDpiChangedAfterParent(e);
        UpdateMetrics();
        Rebuild();
    }

    protected override void OnFontChanged(EventArgs e) {
        base.OnFontChanged(e);
        UpdateMetrics();
        Rebuild();
    }

    protected override void OnPaint(PaintEventArgs e) {
        var graphics = e.Graphics;
        if (_rows.Count == 0) {
            TextRenderer.DrawText(graphics, "No connections yet", Font, ClientRectangle, SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        for (var i = 0; i < _rows.Count; i++) {
            var bounds = RowBounds(i);
            if (bounds.Bottom < e.ClipRectangle.Top || bounds.Top > e.ClipRectangle.Bottom) {
                continue;
            }
            switch (_rows[i]) {
                case GroupRow group:
                    PaintGroup(graphics, group, bounds, i);
                    break;
                case ConnectionRow row:
                    PaintConnection(graphics, row.Connection, bounds, i);
                    break;
            }
        }
        if (_dragging && _dropIndex >= 0) {
            PaintDropLine(graphics);
        }
    }

    protected override void OnScroll(ScrollEventArgs se) {
        base.OnScroll(se);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e) {
        base.OnMouseWheel(e);
        Invalidate();
        UpdateHover(e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        Focus();
        var index = IndexAt(e.Y);
        if (index >= _rows.Count) {
            return;
        }
        SetFocusIndex(index);
        if (e.Button == MouseButtons.Left) {
            _pressIndex = index;
            _pressClicks = e.Clicks;
            _pressPoint = e.Location;
        } else if (e.Button == MouseButtons.Right && _rows[index] is ConnectionRow row) {
            MenuRequested?.Invoke(this, new ConnectionMenuEventArgs(row.Connection, PointToScreen(e.Location)));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (_pressIndex >= 0 && e.Button == MouseButtons.Left) {
            if (!_dragging && HasLeftDragArea(e.Location)) {
                _dragging = true;
                _hoverIndex = -1;
            }
            if (_dragging) {
                UpdateDrop(e.Location);
                return;
            }
        }
        UpdateHover(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _pressIndex < 0) {
            return;
        }
        var pressed = _pressIndex;
        _pressIndex = -1;
        if (_dragging) {
            FinishDrag(pressed);
            return;
        }
        if (IndexAt(e.Y) != pressed) {
            return;
        }
        switch (_rows[pressed]) {
            case GroupRow group:
                ToggleGroup(group.Name);
                break;
            case ConnectionRow row when MenuButtonBounds(RowBounds(pressed)) is var button && button.Contains(e.Location):
                MenuRequested?.Invoke(this, new ConnectionMenuEventArgs(row.Connection, PointToScreen(new Point(button.Left, button.Bottom))));
                break;
            case ConnectionRow row:
                (_pressClicks > 1 ? ConnectionActivated : ConnectionSelected)?.Invoke(this, new ConnectionEventArgs(row.Connection));
                break;
        }
    }

    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        if (_hoverIndex >= 0) {
            _hoverIndex = -1;
            _hoverOnMenuButton = false;
            Invalidate();
        }
    }

    protected override void OnGotFocus(EventArgs e) {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e) {
        base.OnLostFocus(e);
        if (_dragging) {
            CancelDrag();
        }
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (_dragging) {
            if (e.KeyCode == Keys.Escape) {
                CancelDrag();
                e.Handled = true;
            }
            return;
        }
        if (_rows.Count == 0) {
            return;
        }
        var focused = _focusIndex >= 0 && _focusIndex < _rows.Count ? _rows[_focusIndex] : null;
        switch (e.KeyCode) {
            case Keys.Up:
                SetFocusIndex(Math.Max(0, _focusIndex - 1));
                break;
            case Keys.Down:
                SetFocusIndex(Math.Min(_rows.Count - 1, _focusIndex + 1));
                break;
            case Keys.Home:
                SetFocusIndex(0);
                break;
            case Keys.End:
                SetFocusIndex(_rows.Count - 1);
                break;
            case Keys.Left when focused is GroupRow { IsCollapsed: false } group:
                ToggleGroup(group.Name);
                break;
            case Keys.Right when focused is GroupRow { IsCollapsed: true } group:
                ToggleGroup(group.Name);
                break;
            case Keys.Enter or Keys.Space when focused is GroupRow group:
                ToggleGroup(group.Name);
                break;
            case Keys.Enter when focused is ConnectionRow row:
                ConnectionActivated?.Invoke(this, new ConnectionEventArgs(row.Connection));
                break;
            case Keys.Space when focused is ConnectionRow row:
                ConnectionSelected?.Invoke(this, new ConnectionEventArgs(row.Connection));
                break;
            case Keys.Delete when focused is ConnectionRow row:
                DeleteRequested?.Invoke(this, new ConnectionEventArgs(row.Connection));
                break;
            case Keys.Apps or Keys.F10 when (e.KeyCode == Keys.Apps || e.Shift) && focused is ConnectionRow row:
                var bounds = RowBounds(_focusIndex);
                MenuRequested?.Invoke(this, new ConnectionMenuEventArgs(row.Connection, PointToScreen(new Point(bounds.Left + _textLeft, bounds.Bottom))));
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void UpdateMetrics() {
        _boldFont?.Dispose();
        _boldFont = new Font(Font, FontStyle.Bold);
        _lineHeight = TextRenderer.MeasureText("Ag", Font, Size.Empty, TextFormatFlags.NoPadding).Height;
        _padding = LogicalToDeviceUnits(10);
        _connectionRowHeight = _lineHeight * 2 + _padding * 2;
        _groupBandHeight = _lineHeight + LogicalToDeviceUnits(14);
        _dotSize = LogicalToDeviceUnits(10);
        _textLeft = _padding * 2 + _dotSize;
        _menuButtonSize = LogicalToDeviceUnits(26);
        _rowInset = LogicalToDeviceUnits(4);
        _cornerRadius = LogicalToDeviceUnits(4);
        _accentWidth = LogicalToDeviceUnits(3);
    }

    private void Rebuild() {
        var previousFocus = _focusIndex;
        var focusedKey = _focusIndex >= 0 && _focusIndex < _rows.Count ? KeyOf(_rows[_focusIndex]) : null;
        _rows = ConnectionListLayout.Build(_settings, _collapsedGroups);
        _rowTops = new int[_rows.Count];
        var top = 0;
        for (var i = 0; i < _rows.Count; i++) {
            _rowTops[i] = top;
            top += RowHeight(i);
        }
        _contentHeight = top;
        AutoScrollMinSize = new Size(0, top);
        var found = focusedKey is null ? -1 : _rows.ToList().FindIndex(row => KeyOf(row) == focusedKey);
        _focusIndex = found >= 0 ? found : Math.Min(previousFocus, _rows.Count - 1);
        _hoverIndex = -1;
        Invalidate();
    }

    private void ToggleGroup(string group) {
        if (!_collapsedGroups.Remove(group)) {
            _collapsedGroups.Add(group);
        }
        Rebuild();
    }

    private void SetFocusIndex(int index) {
        _focusIndex = index;
        EnsureVisible(index);
        Invalidate();
    }

    private void EnsureVisible(int index) {
        if (index < 0 || index >= _rows.Count) {
            return;
        }
        var top = _rowTops[index];
        var bottom = top + RowHeight(index);
        var viewTop = -AutoScrollPosition.Y;
        if (top < viewTop) {
            AutoScrollPosition = new Point(0, top);
        } else if (bottom > viewTop + ClientSize.Height) {
            AutoScrollPosition = new Point(0, bottom - ClientSize.Height);
        }
    }

    private bool HasLeftDragArea(Point location) {
        var area = SystemInformation.DragSize;
        return Math.Abs(location.X - _pressPoint.X) > area.Width / 2 || Math.Abs(location.Y - _pressPoint.Y) > area.Height / 2;
    }

    private void UpdateDrop(Point location) {
        ScrollWhileDragging(location.Y);
        var index = Math.Max(0, IndexAt(location.Y));
        _dropIndex = index;
        _dropUpperHalf = IsUpperHalf(index, location.Y);
        Invalidate();
    }

    private void ScrollWhileDragging(int y) {
        var step = y < _lineHeight ? -_lineHeight : y > ClientSize.Height - _lineHeight ? _lineHeight : 0;
        if (step == 0) {
            return;
        }
        var target = Math.Clamp(-AutoScrollPosition.Y + step, 0, Math.Max(0, _contentHeight - ClientSize.Height));
        AutoScrollPosition = new Point(0, target);
    }

    private void FinishDrag(int pressed) {
        var dropIndex = _dropIndex;
        var upperHalf = _dropUpperHalf;
        CancelDrag();
        if (dropIndex < 0) {
            return;
        }
        switch (_rows[pressed]) {
            case ConnectionRow row:
                var drop = ConnectionListLayout.DropConnection(_rows, dropIndex, upperHalf);
                MoveRequested?.Invoke(this, new ConnectionMoveEventArgs(row.Connection.Id, drop));
                break;
            case GroupRow group:
                var before = ConnectionListLayout.DropGroup(_rows, dropIndex, upperHalf);
                GroupMoveRequested?.Invoke(this, new GroupMoveEventArgs(group.Name, before));
                break;
        }
    }

    private void CancelDrag() {
        _dragging = false;
        _dropIndex = -1;
        _pressIndex = -1;
        Invalidate();
    }

    private void UpdateHover(Point location) {
        var index = IndexAt(location.Y);
        if (index >= _rows.Count) {
            index = -1;
        }
        var onButton = index >= 0 && _rows[index] is ConnectionRow && MenuButtonBounds(RowBounds(index)).Contains(location);
        if (index != _hoverIndex || onButton != _hoverOnMenuButton) {
            _hoverIndex = index;
            _hoverOnMenuButton = onButton;
            Invalidate();
        }
    }

    // Returns the row under the client Y coordinate, or the row count for the space below the last row.
    private int IndexAt(int clientY) {
        var y = clientY - AutoScrollPosition.Y;
        for (var i = 0; i < _rows.Count; i++) {
            if (y < _rowTops[i] + RowHeight(i)) {
                return i;
            }
        }
        return _rows.Count;
    }

    private bool IsUpperHalf(int index, int clientY) =>
        index < _rows.Count && clientY - AutoScrollPosition.Y < _rowTops[index] + RowHeight(index) / 2;

    private int RowHeight(int index) => _rows[index] is GroupRow ? _groupBandHeight : _connectionRowHeight;

    private Rectangle RowBounds(int index) =>
        new(0, _rowTops[index] + AutoScrollPosition.Y, ClientSize.Width, RowHeight(index));

    private Rectangle MenuButtonBounds(Rectangle row) => new(
        row.Right - _padding / 2 - _menuButtonSize,
        row.Top + (row.Height - _menuButtonSize) / 2,
        _menuButtonSize,
        _menuButtonSize);

    // The plate of the selected or hovered row keeps the same margin on every side, and its text the same
    // padding inside it; the status dot stands on the line of the name.
    private void PaintConnection(Graphics graphics, Connection connection, Rectangle bounds, int index) {
        var active = connection.Id == _activeConnectionId;
        var hover = index == _hoverIndex && !_dragging;
        var plate = Rectangle.Inflate(bounds, -_rowInset, -_rowInset);
        if (active) {
            FillRounded(graphics, SystemColors.Highlight, plate, _cornerRadius);
        } else if (hover) {
            FillRounded(graphics, Palette.Hover, plate, _cornerRadius);
        }
        var foreground = active ? SystemColors.HighlightText : SystemColors.ControlText;
        var secondary = active ? SystemColors.HighlightText : SystemColors.GrayText;

        var textTop = bounds.Top + (bounds.Height - _lineHeight * 2) / 2;
        var dot = new Rectangle(bounds.Left + _padding, textTop + (_lineHeight - _dotSize) / 2, _dotSize, _dotSize);
        PaintStatusDot(graphics, dot, StatusOf(connection.Id), active);

        var textRight = hover ? MenuButtonBounds(bounds).Left - _padding / 2 : bounds.Right - _padding;
        var nameBounds = new Rectangle(bounds.Left + _textLeft, textTop, textRight - bounds.Left - _textLeft, _lineHeight);
        TextRenderer.DrawText(graphics, connection.Name, _boldFont ?? Font, nameBounds, foreground, TextFlags);
        TextRenderer.DrawText(graphics, Details(connection), Font, nameBounds with { Y = nameBounds.Bottom }, secondary, TextFlags);

        if (hover) {
            PaintMenuButton(graphics, MenuButtonBounds(bounds), foreground);
        }
        if (Focused && index == _focusIndex) {
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(plate, -1, -1), foreground, active ? SystemColors.Highlight : BackColor);
        }
    }

    // A group caption is a tinted band with an accent bar on the left and the number of its connections
    // on the right.
    private void PaintGroup(Graphics graphics, GroupRow group, Rectangle band, int index) {
        using (var brush = new SolidBrush(index == _hoverIndex && !_dragging ? Palette.GroupBandHover : Palette.GroupBand)) {
            graphics.FillRectangle(brush, band);
        }
        using (var accent = new SolidBrush(Palette.Accent)) {
            graphics.FillRectangle(accent, band.Left, band.Top, _accentWidth, band.Height);
        }

        var arrow = LogicalToDeviceUnits(4);
        var center = new Point(band.Left + _padding + _dotSize / 2, band.Top + band.Height / 2);
        Point[] triangle = group.IsCollapsed
            ? [new(center.X - arrow / 2, center.Y - arrow), new(center.X + arrow, center.Y), new(center.X - arrow / 2, center.Y + arrow)]
            : [new(center.X - arrow, center.Y - arrow / 2), new(center.X + arrow, center.Y - arrow / 2), new(center.X, center.Y + arrow)];
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPolygon(SystemBrushes.ControlText, triangle);
        graphics.SmoothingMode = SmoothingMode.Default;

        var count = group.ConnectionCount.ToString(CultureInfo.CurrentCulture);
        var pillHeight = _lineHeight + LogicalToDeviceUnits(2);
        var pillWidth = Math.Max(pillHeight, TextRenderer.MeasureText(count, Font, Size.Empty, TextFormatFlags.NoPadding).Width + _padding);
        var pill = new Rectangle(band.Right - _padding - pillWidth, band.Top + (band.Height - pillHeight) / 2, pillWidth, pillHeight);
        FillRounded(graphics, Palette.GroupCount, pill, pillHeight / 2);
        TextRenderer.DrawText(graphics, count, Font, pill, SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        var textBounds = Rectangle.FromLTRB(band.Left + _textLeft, band.Top, pill.Left - _padding / 2, band.Bottom);
        TextRenderer.DrawText(graphics, group.Name, _boldFont ?? Font, textBounds, SystemColors.ControlText, TextFlags);

        if (Focused && index == _focusIndex) {
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(band, -1, -1));
        }
    }

    private static void FillRounded(Graphics graphics, Color color, Rectangle bounds, int radius) {
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter < 2) {
            using var plain = new SolidBrush(color);
            graphics.FillRectangle(plain, bounds);
            return;
        }
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(brush, path);
        graphics.SmoothingMode = SmoothingMode.Default;
    }

    private void PaintStatusDot(Graphics graphics, Rectangle dot, ConnectionStatus status, bool onHighlight) {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        switch (status) {
            case ConnectionStatus.Connected or ConnectionStatus.Busy or ConnectionStatus.Error:
                var color = status switch {
                    ConnectionStatus.Connected => ConnectedColor,
                    ConnectionStatus.Busy => BusyColor,
                    _ => ErrorColor,
                };
                using (var brush = new SolidBrush(color)) {
                    graphics.FillEllipse(brush, dot);
                }
                break;
            default:
                using (var pen = new Pen(onHighlight ? SystemColors.HighlightText : SystemColors.GrayText, LogicalToDeviceUnits(1) * 1.5f)) {
                    graphics.DrawEllipse(pen, Rectangle.Inflate(dot, -1, -1));
                }
                break;
        }
        graphics.SmoothingMode = SmoothingMode.Default;
    }

    private void PaintMenuButton(Graphics graphics, Rectangle button, Color color) {
        if (_hoverOnMenuButton) {
            using var background = new SolidBrush(Color.FromArgb(40, color));
            graphics.FillRectangle(background, button);
        }
        var dotSize = Math.Max(2, LogicalToDeviceUnits(3));
        var gap = LogicalToDeviceUnits(4);
        var centerY = button.Top + button.Height / 2 - dotSize / 2;
        var left = button.Left + (button.Width - dotSize * 3 - gap * 2) / 2;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        for (var i = 0; i < 3; i++) {
            graphics.FillEllipse(brush, left + i * (dotSize + gap), centerY, dotSize, dotSize);
        }
        graphics.SmoothingMode = SmoothingMode.Default;
    }

    private void PaintDropLine(Graphics graphics) {
        var y = _dropIndex >= _rows.Count
            ? _contentHeight
            : _rowTops[_dropIndex] + (_dropUpperHalf ? 0 : RowHeight(_dropIndex));
        y = Math.Clamp(y + AutoScrollPosition.Y, 1, Math.Max(1, ClientSize.Height - 2));
        using var pen = new Pen(SystemColors.Highlight, LogicalToDeviceUnits(2));
        graphics.DrawLine(pen, _padding, y, ClientSize.Width - _padding, y);
    }

    private static string Details(Connection connection) {
        var address = connection.Kind == ConnectionKind.HyperV ? $"{connection.Address} (Hyper-V)" : connection.Address;
        return connection.UserName.Length > 0 ? $"{connection.UserName} · {address}" : address;
    }

    private static string KeyOf(ListRow row) => row switch {
        GroupRow group => "group:" + group.Name,
        ConnectionRow connection => "connection:" + connection.Connection.Id,
        _ => "",
    };
}
