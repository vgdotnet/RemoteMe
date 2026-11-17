using System.ComponentModel;
using RemoteMe.Core.Vault;

namespace RemoteMe.UI;

// Asks for the master password: a new one on the first run, the current one at start, or the one a
// file changed outside the application was saved with.
[DesignerCategory("Code")]
internal sealed class UnlockForm : Form {
    private readonly Mode _mode;
    private readonly string _path;
    private readonly SettingsStore? _reloadStore;
    private readonly TextBox _password;
    private readonly TextBox? _repeat;
    private readonly Label _message;
    private readonly Button _accept;

    private UnlockForm(Mode mode, string path, SettingsStore? reloadStore) {
        _mode = mode;
        _path = path;
        _reloadStore = reloadStore;

        SuspendLayout();
        DialogLayout.SetUp(this, Messages.Title, growOnly: true);
        if (mode != Mode.Reload) {
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
        }

        var grid = DialogLayout.CreateGrid(1);
        grid.Controls.Add(DialogLayout.CreateText(mode switch {
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

    public static UnlockForm ForStartup(string path) => new(File.Exists(path) ? Mode.Unlock : Mode.Create, path, null);

    public static UnlockForm ForReload(SettingsStore store) => new(Mode.Reload, store.Path, store);

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
                    Store = await Task.Run(() => SettingsStore.Create(_path, password));
                    break;
                case Mode.Unlock:
                    Store = await Task.Run(() => SettingsStore.Open(_path, password));
                    break;
                case Mode.Reload:
                    _reloadStore!.Reload(password);
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
        _accept.Enabled = !busy && _password.TextLength > 0;
    }
}
