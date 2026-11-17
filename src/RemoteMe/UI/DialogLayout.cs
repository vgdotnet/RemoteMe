namespace RemoteMe.UI;

// Building blocks for the dialogs. Sizes are in 96-DPI units; the forms scale them.
internal static class DialogLayout {
    public const int FieldWidth = 320;

    public static readonly Color WarningColor = Color.FromArgb(0x9D, 0x5D, 0x00);
    public static readonly Color ErrorColor = Color.FromArgb(0xC4, 0x2B, 0x1C);

    // growOnly keeps a dialog's height when a message under its fields disappears, so it does not jump.
    public static void SetUp(Form form, string title, bool growOnly = false) {
        form.AutoScaleDimensions = new SizeF(96F, 96F);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.ClientSize = new Size(1, 1);
        form.AutoSize = true;
        form.AutoSizeMode = growOnly ? AutoSizeMode.GrowOnly : AutoSizeMode.GrowAndShrink;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.Padding = new Padding(12);
        form.Text = title;
        form.Icon = AppIcon.Value;
    }

    public static TableLayoutPanel CreateGrid(int columns) {
        var grid = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns,
            Dock = DockStyle.Fill,
        };
        for (var i = 0; i < columns; i++) {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        return grid;
    }

    public static Label CreateText(string text) => new() {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(FieldWidth, 0),
        Margin = new Padding(3, 3, 3, 9),
    };

    public static Label CreateCaption(string text) => new() {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 6, 3, 2),
    };

    public static Label CreateMessage() => new() {
        AutoSize = true,
        MaximumSize = new Size(FieldWidth, 0),
        Margin = new Padding(3, 6, 3, 0),
    };

    public static TextBox CreateTextBox(bool password = false) => new() {
        Width = FieldWidth,
        UseSystemPasswordChar = password,
    };

    // Shows or hides the typed text of the given password boxes; off by default.
    public static CheckBox CreateShowPassword(params TextBox[] passwordBoxes) {
        var box = new CheckBox { Text = "Show password", AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
        box.CheckedChanged += (_, _) => {
            foreach (var passwordBox in passwordBoxes) {
                passwordBox.UseSystemPasswordChar = !box.Checked;
            }
        };
        return box;
    }

    public static void ShowMessage(Label label, string text, Color color) {
        label.ForeColor = color;
        label.Text = text;
    }

    public static FlowLayoutPanel CreateButtons(Form form, string acceptText, string cancelText, out Button accept) {
        accept = new Button { Text = acceptText, AutoSize = true, MinimumSize = new Size(88, 0) };
        var cancel = new Button {
            Text = cancelText,
            AutoSize = true,
            MinimumSize = new Size(88, 0),
            DialogResult = DialogResult.Cancel,
        };
        var panel = new FlowLayoutPanel {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
            WrapContents = false,
        };
        panel.Controls.Add(cancel);
        panel.Controls.Add(accept);
        form.AcceptButton = accept;
        form.CancelButton = cancel;
        return panel;
    }
}
