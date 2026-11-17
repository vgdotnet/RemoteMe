using System.ComponentModel;

namespace RemoteMe.UI;

// A borderless window over one monitor. The session control moves here and back instead of changing the
// border of the window it came from, which could re-create that window and its controls.
[DesignerCategory("Code")]
internal sealed class FullScreenWindow : Form {
    private readonly Rectangle _monitorBounds;
    private bool _closingFromCode;

    public FullScreenWindow(Guid connectionId, string title, Rectangle monitorBounds) {
        ConnectionId = connectionId;
        _monitorBounds = monitorBounds;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = monitorBounds;
        BackColor = Color.Black;
        Text = title;
        Icon = AppIcon.Value;
        View = new SessionView();
        Controls.Add(View);
        ResumeLayout(false);
    }

    public event EventHandler? ClosedByUser;

    public Guid ConnectionId { get; }

    public SessionView View { get; }

    public void CloseFromCode() {
        _closingFromCode = true;
        Close();
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        Bounds = _monitorBounds;
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        base.OnFormClosing(e);
        if (!e.Cancel && !_closingFromCode) {
            _closingFromCode = true;
            ClosedByUser?.Invoke(this, EventArgs.Empty);
        }
    }
}
