using System.ComponentModel;
using RemoteMe.Core.Vault;

namespace RemoteMe.UI;

// Checks the current master password and collects the new one; the caller saves it.
[DesignerCategory("Code")]
internal sealed class ChangePasswordForm : Form {
    private readonly SettingsStore _store;
    private readonly TextBox _current;
    private readonly TextBox _new;
    private readonly TextBox _repeat;
    private readonly Label _message;
    private readonly Button _accept;

    public ChangePasswordForm(SettingsStore store) {
        _store = store;

        SuspendLayout();
        DialogLayout.SetUp(this, "Change master password", growOnly: true);
        var grid = DialogLayout.CreateGrid(1);
        grid.Controls.Add(DialogLayout.CreateCaption("Current master password"));
        _current = DialogLayout.CreateTextBox(password: true);
        _current.AccessibleName = "Current master password";
        grid.Controls.Add(_current);
        grid.Controls.Add(DialogLayout.CreateCaption("New master password"));
        _new = DialogLayout.CreateTextBox(password: true);
        _new.AccessibleName = "New master password";
        grid.Controls.Add(_new);
        grid.Controls.Add(DialogLayout.CreateCaption("Repeat the new password"));
        _repeat = DialogLayout.CreateTextBox(password: true);
        _repeat.AccessibleName = "Repeat the new password";
        grid.Controls.Add(_repeat);
        grid.Controls.Add(DialogLayout.CreateShowPassword(_current, _new, _repeat));
        _message = DialogLayout.CreateMessage();
        grid.Controls.Add(_message);
        grid.Controls.Add(DialogLayout.CreateButtons(this, "Change", "Cancel", out _accept));
        Controls.Add(grid);
        ResumeLayout(false);
        PerformLayout();

        _current.TextChanged += (_, _) => UpdateState();
        _new.TextChanged += (_, _) => UpdateState();
        _repeat.TextChanged += (_, _) => UpdateState();
        _accept.Click += OnAccept;
        UpdateState();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string NewPassword { get; private set; } = "";

    private void UpdateState() {
        _accept.Enabled = _current.TextLength > 0 && _new.TextLength > 0 && _repeat.TextLength > 0;
        if (_new.TextLength > 0 && MasterPassword.IsWeak(_new.Text)) {
            DialogLayout.ShowMessage(
                _message,
                $"The new password is shorter than {MasterPassword.RecommendedLength} characters. It will work, "
                    + "but a copied settings file is easier to break.",
                DialogLayout.WarningColor);
        } else {
            _message.Text = "";
        }
    }

    private async void OnAccept(object? sender, EventArgs e) {
        if (_new.Text != _repeat.Text) {
            DialogLayout.ShowMessage(_message, "The new passwords do not match.", DialogLayout.ErrorColor);
            _repeat.Focus();
            _repeat.SelectAll();
            return;
        }

        var current = _current.Text;
        UseWaitCursor = true;
        _accept.Enabled = false;
        try {
            if (!await Task.Run(() => _store.IsMasterPassword(current))) {
                DialogLayout.ShowMessage(_message, "The current master password is wrong.", DialogLayout.ErrorColor);
                _current.Focus();
                _current.SelectAll();
                return;
            }
            NewPassword = _new.Text;
            DialogResult = DialogResult.OK;
            Close();
        } finally {
            UseWaitCursor = false;
            _accept.Enabled = true;
        }
    }
}
