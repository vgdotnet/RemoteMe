using System.ComponentModel;
using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.UI;

// Asks for the password of a connection that has none saved, and offers to save it.
[DesignerCategory("Code")]
internal sealed class CredentialsForm : Form {
    private readonly TextBox _userName;
    private readonly TextBox _password;
    private readonly CheckBox _save;
    private readonly Button _accept;

    public CredentialsForm(Connection connection) {
        SuspendLayout();
        DialogLayout.SetUp(this, $"Sign in to {connection.Name}");
        var grid = DialogLayout.CreateGrid(1);
        grid.Controls.Add(DialogLayout.CreateText($"No password is saved for {connection.Address}."));
        grid.Controls.Add(DialogLayout.CreateCaption("User name"));
        _userName = DialogLayout.CreateTextBox();
        _userName.AccessibleName = "User name";
        _userName.Text = connection.UserName;
        grid.Controls.Add(_userName);
        grid.Controls.Add(DialogLayout.CreateCaption("Password"));
        _password = DialogLayout.CreateTextBox(password: true);
        _password.AccessibleName = "Password";
        grid.Controls.Add(_password);
        grid.Controls.Add(DialogLayout.CreateShowPassword(_password));
        _save = new CheckBox { Text = "Save the password in the settings", AutoSize = true, Checked = true, Margin = new Padding(3, 9, 3, 0) };
        grid.Controls.Add(_save);
        grid.Controls.Add(DialogLayout.CreateButtons(this, "Connect", "Cancel", out _accept));
        Controls.Add(grid);
        ResumeLayout(false);
        PerformLayout();

        ActiveControl = _userName.TextLength > 0 ? _password : _userName;
        _userName.TextChanged += (_, _) => UpdateState();
        _password.TextChanged += (_, _) => UpdateState();
        _accept.Click += (_, _) => {
            DialogResult = DialogResult.OK;
            Close();
        };
        UpdateState();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string UserName => _userName.Text.Trim();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ProtectedSecret Password => ProtectedSecret.Create(_password.Text);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SavePassword => _save.Checked;

    private void UpdateState() {
        _accept.Enabled = _userName.Text.Trim().Length > 0 && _password.TextLength > 0;
    }
}
