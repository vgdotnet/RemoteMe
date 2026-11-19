using System.ComponentModel;
using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.UI;

[DesignerCategory("Code")]
internal sealed class ConnectionForm : Form {
    private readonly Connection? _original;
    private readonly IReadOnlyList<string> _groups;
    private readonly TextBox _name;
    private readonly ComboBox _group;
    private readonly RadioButton _network;
    private readonly RadioButton _hyperV;
    private readonly Label _addressCaption;
    private readonly TextBox _address;
    private readonly Label _vmIdCaption;
    private readonly TextBox _vmId;
    private readonly Label _vmIdHint;
    private readonly CheckBox _enhanced;
    private readonly Label _hyperVHint;
    private readonly Label _userNameCaption;
    private readonly TextBox _userName;
    private readonly Label _passwordCaption;
    private readonly TextBox _password;
    private readonly LinkLabel _forgetPassword;
    private readonly CheckBox _showPassword;
    private readonly FlowLayoutPanel _redirection;
    private readonly CheckBox _clipboard;
    private readonly CheckBox _audio;
    private readonly CheckBox _microphone;
    private readonly CheckBox _drives;
    private readonly CheckBox _printers;
    private readonly Label _message;
    private bool _hasSavedPassword;
    private bool _forgetSavedPassword;

    public ConnectionForm(Connection? original, IReadOnlyList<string> groups) {
        _original = original;
        _groups = groups;

        SuspendLayout();
        DialogLayout.SetUp(this, original is null ? "Add connection" : "Edit connection");

        var grid = DialogLayout.CreateGrid(2);

        _name = DialogLayout.CreateTextBox();
        AddRow(grid, "Name", _name);

        _group = new ComboBox { Width = DialogLayout.FieldWidth, DropDownStyle = ComboBoxStyle.DropDown };
        _group.Items.AddRange(groups.ToArray<object>());
        AddRow(grid, "Group", _group);

        _network = new RadioButton { Text = "Network address", AutoSize = true, Checked = true };
        _hyperV = new RadioButton { Text = "VM on a Hyper-V host", AutoSize = true };
        var kind = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        kind.Controls.Add(_network);
        kind.Controls.Add(_hyperV);
        AddRow(grid, "Type", kind);

        _address = DialogLayout.CreateTextBox();
        _addressCaption = AddRow(grid, "Computer", _address);

        _vmId = DialogLayout.CreateTextBox();
        _vmIdCaption = AddRow(grid, "VM ID", _vmId);
        _vmIdHint = AddHint(grid, "The Id that Get-VM shows on the Hyper-V host.");
        _enhanced = new CheckBox { Text = "Enhanced session", AutoSize = true, Margin = new Padding(3, 3, 3, 3) };
        grid.Controls.Add(_enhanced, 1, grid.RowCount++);
        _hyperVHint = AddHint(grid, "");

        _userName = DialogLayout.CreateTextBox();
        _userNameCaption = AddRow(grid, "User name", _userName);

        _password = DialogLayout.CreateTextBox(password: true);
        _passwordCaption = AddRow(grid, "Password", _password);
        _forgetPassword = new LinkLabel { Text = "Remove the saved password", AutoSize = true, Margin = new Padding(3, 0, 3, 3) };
        grid.Controls.Add(_forgetPassword, 1, grid.RowCount++);
        _showPassword = DialogLayout.CreateShowPassword(_password);
        grid.Controls.Add(_showPassword, 1, grid.RowCount++);

        _clipboard = new CheckBox { Text = "Clipboard", AutoSize = true };
        _audio = new CheckBox { Text = "Audio", AutoSize = true };
        _microphone = new CheckBox { Text = "Microphone", AutoSize = true };
        _drives = new CheckBox { Text = "Drives", AutoSize = true };
        _printers = new CheckBox { Text = "Printers", AutoSize = true };
        _redirection = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(DialogLayout.FieldWidth, 0), Margin = Padding.Empty };
        _redirection.Controls.AddRange([_clipboard, _audio, _microphone, _drives, _printers]);
        AddRow(grid, "Redirect", _redirection);

        _message = DialogLayout.CreateMessage();
        _message.ForeColor = DialogLayout.ErrorColor;
        grid.Controls.Add(_message, 1, grid.RowCount++);

        var buttons = DialogLayout.CreateButtons(this, "Save", "Cancel", out var save);
        grid.Controls.Add(buttons, 0, grid.RowCount);
        grid.SetColumnSpan(buttons, 2);
        grid.RowCount++;

        Fill(original ?? new Connection { Id = Guid.Empty, Name = "", Address = "" });
        UpdateKind();
        Controls.Add(grid);
        ResumeLayout(false);
        PerformLayout();

        _hyperV.CheckedChanged += (_, _) => {
            // Only the local Hyper-V host is needed (decided on 2026-11-17).
            if (_hyperV.Checked && _address.TextLength == 0) {
                _address.Text = "localhost";
            }
            UpdateKind();
        };
        _enhanced.CheckedChanged += (_, _) => UpdateKind();
        _forgetPassword.LinkClicked += (_, _) => ForgetPassword();
        save.Click += (_, _) => Save();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Connection? Result { get; private set; }

    private void Fill(Connection connection) {
        _name.Text = connection.Name;
        _group.Text = connection.Group ?? "";
        _hyperV.Checked = connection.Kind == ConnectionKind.HyperV;
        _address.Text = connection.Address;
        _vmId.Text = connection.VmId?.ToString() ?? "";
        _enhanced.Checked = !connection.BasicSession;
        _userName.Text = connection.UserName;
        _clipboard.Checked = connection.Redirection.Clipboard;
        _audio.Checked = connection.Redirection.Audio;
        _microphone.Checked = connection.Redirection.Microphone;
        _drives.Checked = connection.Redirection.Drives;
        _printers.Checked = connection.Redirection.Printers;
        _hasSavedPassword = connection.Password is { IsEmpty: false };
        _password.PlaceholderText = _hasSavedPassword ? "Saved; type a new one to replace it" : "";
    }

    // A Hyper-V host is signed in to with the current Windows account, so a VM has no user name or
    // password of its own; a VM's console has no redirection.
    private void UpdateKind() {
        var hyperV = _hyperV.Checked;
        var enhanced = _enhanced.Checked;
        _addressCaption.Text = hyperV ? "Hyper-V host" : "Computer";
        _address.AccessibleName = _addressCaption.Text;
        _vmIdCaption.Visible = hyperV;
        _vmId.Visible = hyperV;
        _vmIdHint.Visible = hyperV;
        _enhanced.Visible = hyperV;
        _hyperVHint.Visible = hyperV;
        _hyperVHint.Text = "RemoteMe signs in to the Hyper-V host with your Windows account. " + (enhanced
            ? "The VM asks for its own password in a Windows dialog; its Remote Desktop must be on."
            : "Sign in on the VM's console; \"Send Ctrl+Alt+Del\" is in the connection's menu.");
        _userNameCaption.Visible = !hyperV;
        _userName.Visible = !hyperV;
        _passwordCaption.Visible = !hyperV;
        _password.Visible = !hyperV;
        _forgetPassword.Visible = !hyperV && _hasSavedPassword;
        _showPassword.Visible = !hyperV;
        _redirection.Enabled = !hyperV || enhanced;
    }

    private void ForgetPassword() {
        _forgetSavedPassword = true;
        _hasSavedPassword = false;
        _password.Clear();
        _password.PlaceholderText = "";
        _forgetPassword.Visible = false;
    }

    private void Save() {
        Guid? vmId = Guid.TryParse(_vmId.Text.Trim(), out var parsed) ? parsed : null;
        var valid = Require(_name, _name.Text.Trim().Length > 0, "Enter a name.")
            && Require(_address, _address.Text.Trim().Length > 0, _hyperV.Checked ? "Enter the Hyper-V host." : "Enter the computer's name or address.")
            && (!_hyperV.Checked || Require(_address, HyperVHost.IsThisComputer(_address.Text), HyperVHost.Requirement))
            && (!_hyperV.Checked || Require(_vmId, vmId is not null, "Enter the VM ID, a GUID such as 1b4e28ba-2fa1-11d2-883f-0016d3cca427."));
        if (!valid) {
            return;
        }
        var hyperV = _hyperV.Checked;
        var password = hyperV ? null
            : _password.TextLength > 0 ? ProtectedSecret.Create(_password.Text)
            : _forgetSavedPassword ? null
            : _original?.Password;
        Result = new Connection {
            Id = _original?.Id ?? Guid.NewGuid(),
            Name = _name.Text.Trim(),
            Group = MatchExistingGroup(_group.Text),
            Kind = hyperV ? ConnectionKind.HyperV : ConnectionKind.Network,
            Address = _address.Text.Trim(),
            VmId = hyperV ? vmId : null,
            BasicSession = hyperV && !_enhanced.Checked,
            UserName = hyperV ? "" : _userName.Text.Trim(),
            Password = password,
            Redirection = new RedirectionSettings {
                Clipboard = _clipboard.Checked,
                Audio = _audio.Checked,
                Microphone = _microphone.Checked,
                Drives = _drives.Checked,
                Printers = _printers.Checked,
            },
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    // "lab" typed for an existing "Lab" joins that group instead of starting a second one.
    private string? MatchExistingGroup(string text) {
        var group = SettingsEditor.CleanGroup(text);
        return group is null ? null : _groups.FirstOrDefault(g => string.Equals(g, group, StringComparison.CurrentCultureIgnoreCase)) ?? group;
    }

    private bool Require(Control control, bool condition, string message) {
        if (condition) {
            _message.Text = "";
        } else {
            _message.Text = message;
            control.Focus();
        }
        return condition;
    }

    private static Label AddRow(TableLayoutPanel grid, string caption, Control control) {
        var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) };
        control.Margin = new Padding(3, 3, 3, 3);
        control.AccessibleName = caption;
        grid.Controls.Add(label, 0, grid.RowCount);
        grid.Controls.Add(control, 1, grid.RowCount);
        grid.RowCount++;
        return label;
    }

    private static Label AddHint(TableLayoutPanel grid, string text) {
        var hint = new Label {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(DialogLayout.FieldWidth, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 6),
        };
        grid.Controls.Add(hint, 1, grid.RowCount);
        grid.RowCount++;
        return hint;
    }
}
