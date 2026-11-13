namespace RemoteMe;

internal sealed class MainForm : Form {
    public MainForm() {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RemoteMe";
        ResumeLayout(false);
    }
}
