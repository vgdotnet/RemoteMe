using System.ComponentModel;

namespace RemoteMe.UI;

// Where session controls live: the main window, a separate session window or a full-screen window.
// A control moves between views with the WinForms Parent property, which keeps the session alive.
[DesignerCategory("Code")]
internal sealed class SessionView : Panel {
    public SessionView() {
        Dock = DockStyle.Fill;
        Overlay = new SessionOverlay();
        Controls.Add(Overlay);
    }

    public SessionOverlay Overlay { get; }

    public void Attach(Control sessionControl) {
        sessionControl.Dock = DockStyle.Fill;
        sessionControl.Parent = this;
        if (Overlay.Visible) {
            Overlay.BringToFront();
        }
    }
}
