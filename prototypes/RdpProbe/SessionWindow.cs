namespace RdpProbe;

internal sealed class SessionWindow : Form {
    public SessionWindow() {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1024, 700);
        Text = "RdpProbe - separate window";
        SessionPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32) };
        Controls.Add(SessionPanel);
        ResumeLayout(false);
    }

    public Panel SessionPanel { get; }
}
