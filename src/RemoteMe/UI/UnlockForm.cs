using System.ComponentModel;
using RemoteMe.Core.Vault;

namespace RemoteMe.UI;

// Asks for the master password: a new one on the first run, the current one at start, or the one a
// file changed outside the application was saved with. At start it also asks whether to remember the
// password on this computer; the caller saves or removes it.
[DesignerCategory("Code")]
internal sealed class UnlockForm : Form {
    private readonly Mode _mode;
    private readonly string _path;
    private readonly RememberedPassword? _remembered;
    private readonly SettingsStore? _reloadStore;
    private readonly TextBox _password;
    private readonly TextBox? _repeat;
    private readonly CheckBox? _remember;
    private readonly Label _message;
    private readonly Button _accept;

    private UnlockForm(Mode mode, string path, RememberedPassword? remembered, SettingsStore? reloadStore, string? notice) {
        _mode = mode;
        _path = path;
        _remembered = remembered;
        _reloadStore = reloadStore;

        SuspendLayout();
        DialogLayout.SetUp(this, Messages.Title, growOnly: true);
        if (mode != Mode.Reload) {
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
        }

        var grid = DialogLayout.CreateGrid(1);
        grid.Controls.Add(DialogLayout.CreateText(notice ?? mode switch {
            Mode.Create => "Create a master password. It encrypts the settings file next to RemoteMe "
                + "and cannot be recovered if you forget it.",
            Mode.Reload => "The settings file was saved with a different master password. "
                + "Enter that password to read the file again.",
            _ => "Enter the master password to open your connections.",
        }));
        grid.Controls.Add(DialogLayout.CreateCaption(mode == Mode.Create ? "New master password" : "Master password"));
        _password = DialogLayout.CreateTextBox(password: true);
        _password.AccessibleName = "Master password";
        grid.Controls.Add(_password);
        if (mode == Mode.Create) {
            grid.Controls.Add(DialogLayout.CreateCaption("Repeat the password"));
            _repeat = DialogLayout.CreateTextBox(password: true);
            _repeat.AccessibleName = "Repeat the password";
            grid.Controls.Add(_repeat);
        }
        grid.Controls.Add(_repeat is null ? DialogLayout.CreateShowPassword(_password) : DialogLayout.CreateShowPassword(_password, _repeat));
        if (remembered is not null) {
            // Ticked when a saved password did not fit, so entering the current one saves it again.
            _remember = DialogLayout.CreateCheckBox("Remember on this computer");
            _remember.Checked = mode == Mode.Unlock && remembered.Exists;
            grid.Controls.Add(_remember);
        }
        _message = DialogLayout.CreateMessage();
        grid.Controls.Add(_message);
        grid.Controls.Add(DialogLayout.CreateButtons(
            this,
            mode switch { Mode.Create => "Create", Mode.Reload => "Read again", _ => "Unlock" },
            mode == Mode.Reload ? "Cancel" : "Exit",
            out _accept));
        Controls.Add(grid);
        ResumeLayout(false);
        PerformLayout();

        _password.TextChanged += (_, _) => UpdateState();
        if (_repeat is not null) {
            _repeat.TextChanged += (_, _) => UpdateState();
        }
        _accept.Click += OnAccept;
        UpdateState();
    }

    private enum Mode {
        Create,
        Unlock,
        Reload,
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SettingsStore? Store { get; private set; }

    // Whether to save the master password on this computer; false when the box is not shown.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Remember => _remember?.Checked == true;

    // Why the master password saved on this computer could not be updated after the file was read again.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Warning { get; private set; }

    // The notice replaces the usual text, for example to say why the saved password did not fit.
    public static UnlockForm ForStartup(string path, RememberedPassword remembered, string? notice = null) =>
        new(File.Exists(path) ? Mode.Unlock : Mode.Create, path, remembered, null, notice);

    public static UnlockForm ForReload(SettingsStore store) => new(Mode.Reload, store.Path, null, store, null);

    private void UpdateState() {
        _accept.Enabled = _password.TextLength > 0 && (_repeat is null || _repeat.TextLength > 0);
        if (_mode == Mode.Create && _password.TextLength > 0 && MasterPassword.IsWeak(_password.Text)) {
            DialogLayout.ShowMessage(
                _message,
                $"This password is shorter than {MasterPassword.RecommendedLength} characters. It will work, "
                    + "but a copied settings file is easier to break.",
                DialogLayout.WarningColor);
        } else {
            _message.Text = "";
        }
    }

    private async void OnAccept(object? sender, EventArgs e) {
        var password = _password.Text;
        if (_repeat is not null && password != _repeat.Text) {
            DialogLayout.ShowMessage(_message, "The passwords do not match.", DialogLayout.ErrorColor);
            _repeat.Focus();
            _repeat.SelectAll();
            return;
        }

        SetBusy(true);
        try {
            switch (_mode) {
                case Mode.Create:
                    Store = await Task.Run(() => SettingsStore.Create(_path, password, _remembered));
                    break;
                case Mode.Unlock:
                    Store = await Task.Run(() => SettingsStore.Open(_path, password, _remembered));
                    break;
                case Mode.Reload:
                    try {
                        _reloadStore!.Reload(password);
                    } catch (RememberedPasswordException ex) {
                        Warning = ex.Message;
                    }
                    break;
            }
            DialogResult = DialogResult.OK;
            Close();
        } catch (VaultException ex) when (ex.Error == VaultError.WrongPassword) {
            DialogLayout.ShowMessage(_message, "Wrong master password.", DialogLayout.ErrorColor);
            _password.Focus();
            _password.SelectAll();
        } catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException) {
            Messages.Error(this, $"The settings file {_path} could not be opened.\n\n{ex.Message}");
        } finally {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy) {
        UseWaitCursor = busy;
        _password.Enabled = !busy;
        if (_repeat is not null) {
            _repeat.Enabled = !busy;
        }
        if (_remember is not null) {
            _remember.Enabled = !busy;
        }
        _accept.Enabled = !busy && _password.TextLength > 0;
    }
}
