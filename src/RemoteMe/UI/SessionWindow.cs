using System.ComponentModel;

namespace RemoteMe.UI;

// A separate window for one session, so sessions can sit on different monitors.
[DesignerCategory("Code")]
internal sealed class SessionWindow : Form {
    private readonly Rectangle _initialBounds;
    private bool _closingFromCode;

    // The bounds are in physical pixels and are applied after the form has scaled its contents.
    public SessionWindow(Guid connectionId, string title, Rectangle bounds) {
        ConnectionId = connectionId;
        _initialBounds = bounds;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        MinimumSize = new Size(320, 240);
        Text = title;
        View = new SessionView();
        Controls.Add(View);
        ResumeLayout(false);
    }

    // Raised when the user closes the window; the handler takes the session control out first.
    public event EventHandler? ClosedByUser;

    public Guid ConnectionId { get; }

    public SessionView View { get; }

    public void CloseFromCode() {
        _closingFromCode = true;
        Close();
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        Bounds = _initialBounds;
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        base.OnFormClosing(e);
        if (!e.Cancel && !_closingFromCode) {
            _closingFromCode = true;
            ClosedByUser?.Invoke(this, EventArgs.Empty);
        }
    }
}
