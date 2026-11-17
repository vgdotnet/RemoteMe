using System.ComponentModel;

namespace RemoteMe.UI;

// Covers a session area while its session is not on screen: the connection name, what is going on,
// and up to two actions.
[DesignerCategory("Code")]
internal sealed class SessionOverlay : Panel {
    private readonly Label _title;
    private readonly Label _message;
    private readonly Button _primary;
    private readonly Button _secondary;

    public SessionOverlay() {
        Dock = DockStyle.Fill;
        BackColor = SystemColors.Control;

        _title = new Label { AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(3, 3, 3, 6) };
        _title.Font = new Font(_title.Font.FontFamily, _title.Font.Size * 1.4f, FontStyle.Bold);
        _message = new Label { AutoSize = true, Anchor = AnchorStyles.None, MaximumSize = new Size(520, 0), Margin = new Padding(3, 3, 3, 12) };
        _primary = new Button { Size = new Size(170, 32) };
        _secondary = new Button { Size = new Size(170, 32) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, WrapContents = false };
        buttons.Controls.Add(_primary);
        buttons.Controls.Add(_secondary);

        var content = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Anchor = AnchorStyles.None };
        content.Controls.Add(_title);
        content.Controls.Add(_message);
        content.Controls.Add(buttons);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        center.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        center.Controls.Add(content, 1, 1);
        Controls.Add(center);

        _primary.Click += (_, _) => PrimaryClicked?.Invoke(this, EventArgs.Empty);
        _secondary.Click += (_, _) => SecondaryClicked?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PrimaryClicked;

    public event EventHandler? SecondaryClicked;

    // An empty button text hides that button.
    public void ShowState(string title, string message, string primary = "", string secondary = "", bool isError = false) {
        _title.Text = title;
        _title.Visible = title.Length > 0;
        _message.Text = message;
        _message.ForeColor = isError ? DialogLayout.ErrorColor : SystemColors.GrayText;
        _primary.Text = primary;
        _primary.Visible = primary.Length > 0;
        _secondary.Text = secondary;
        _secondary.Visible = secondary.Length > 0;
        Visible = true;
        BringToFront();
    }
}
