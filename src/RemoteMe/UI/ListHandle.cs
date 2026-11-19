using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RemoteMe.UI;

// The tab at the right edge of the list, half way down (option A of four mockups, decided on 2026-11-19).
// A click steps the width of the list, see ListWidth; dragging the tab moves the border of the list, as
// the splitter does. The tab has rounded corners and a light fill, so it first paints what lies under it:
// the wallpaper of its parent and the controls below it.
[DesignerCategory("Code")]
internal sealed class ListHandle : Control {
    private bool _pointsRight;
    private bool _hover;
    private bool _pressed;
    private bool _dragging;
    private int _pressX;

    public ListHandle() {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
            true);
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        BackColor = Color.Transparent;
        TabStop = false;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public event EventHandler? Clicked;

    public event EventHandler? DragStarted;

    // The horizontal distance in pixels from the point where the drag began.
    public event EventHandler<int>? Dragged;

    public event EventHandler? DragEnded;

    // The arrows point right on the compact strip, which the tab widens, and left on the full list.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PointsRight {
        get => _pointsRight;
        set {
            if (_pointsRight != value) {
                _pointsRight = value;
                Invalidate();
            }
        }
    }

    protected override void OnMouseEnter(EventArgs e) {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) {
            return;
        }
        _pressed = true;
        // The screen position, since the tab moves with the border while it is dragged.
        _pressX = MousePosition.X;
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (!_pressed) {
            return;
        }
        var offset = MousePosition.X - _pressX;
        if (!_dragging && Math.Abs(offset) >= SystemInformation.DragSize.Width) {
            _dragging = true;
            Cursor = Cursors.SizeWE;
            DragStarted?.Invoke(this, EventArgs.Empty);
        }
        if (_dragging) {
            Dragged?.Invoke(this, offset);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_pressed) {
            return;
        }
        var dragged = _dragging;
        Release();
        if (dragged) {
            DragEnded?.Invoke(this, EventArgs.Empty);
        } else if (ClientRectangle.Contains(e.Location)) {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    // The mouse was taken away during a press, by Alt+Tab for one; a drag ends where it is.
    protected override void OnMouseCaptureChanged(EventArgs e) {
        base.OnMouseCaptureChanged(e);
        if (_pressed && !Capture) {
            var dragged = _dragging;
            Release();
            if (dragged) {
                DragEnded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
        base.OnPaintBackground(e);
        PaintControlsBelow(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = TabPath()) {
            var fill = _pressed ? Palette.Pressed : _hover ? Palette.Hover : Color.FromArgb(210, SystemColors.Window);
            using (var brush = new SolidBrush(fill)) {
                graphics.FillPath(brush, path);
            }
            // Darker than the separator lines, so the tab shows on the white rows too.
            using var pen = new Pen(Palette.Blend(SystemColors.Window, SystemColors.ControlDark, 0.7));
            graphics.DrawPath(pen, path);
        }
        PaintArrows(graphics);
        graphics.SmoothingMode = SmoothingMode.Default;
    }

    private void Release() {
        _pressed = false;
        _dragging = false;
        Cursor = Cursors.Default;
        Capture = false;
        Invalidate();
    }

    // The controls under the tab in the z-order of its parent: the rows of the list, or the clock in a low
    // window. Their own paint code draws them, moved to the tab's coordinates.
    private void PaintControlsBelow(PaintEventArgs e) {
        if (Parent is not { } parent) {
            return;
        }
        var controls = parent.Controls;
        for (var i = controls.Count - 1; i > controls.GetChildIndex(this); i--) {
            var below = controls[i];
            if (!below.Visible || !below.IsHandleCreated || !below.Bounds.IntersectsWith(Bounds)) {
                continue;
            }
            var offset = new Size(Left - below.Left, Top - below.Top);
            var state = e.Graphics.Save();
            e.Graphics.TranslateTransform(-offset.Width, -offset.Height);
            var args = new PaintEventArgs(e.Graphics, new Rectangle(e.ClipRectangle.Location + offset, e.ClipRectangle.Size));
            InvokePaintBackground(below, args);
            InvokePaint(below, args);
            e.Graphics.Restore(state);
        }
    }

    // Rounded on the left; the right side lies outside the control, so the tab is open toward the border.
    private GraphicsPath TabPath() {
        var left = 0.5f;
        var top = 0.5f;
        var right = Width + 1f;
        var bottom = Height - 0.5f;
        var diameter = Width - 1f;
        var radius = diameter / 2;
        var path = new GraphicsPath();
        path.AddLine(right, top, left + radius, top);
        path.AddArc(left, top, diameter, diameter, 270, -90);
        path.AddLine(left, top + radius, left, bottom - radius);
        path.AddArc(left, bottom - diameter, diameter, diameter, 180, -90);
        path.AddLine(left + radius, bottom, right, bottom);
        path.CloseFigure();
        return path;
    }

    // Two chevrons, « on the full list and » on the compact strip.
    private void PaintArrows(Graphics graphics) {
        var scale = DeviceDpi / 96f;
        var half = 3.5f * scale;
        var depth = 3f * scale;
        var gap = 3.5f * scale;
        var first = (Width - gap - depth) / 2f;
        var middle = Height / 2f;
        using var pen = new Pen(SystemColors.ControlText, 1.25f * scale) {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        for (var k = 0; k < 2; k++) {
            var x = first + k * gap;
            var tip = _pointsRight ? x + depth : x;
            var back = _pointsRight ? x : x + depth;
            graphics.DrawLines(pen, [new PointF(back, middle - half), new PointF(tip, middle), new PointF(back, middle + half)]);
        }
    }
}
