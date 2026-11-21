using System.Security.Cryptography;
using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.Core.Vault;

// The settings the application works with. Every change is saved at once. The master password stays
// in memory (encrypted) so the file can be read again after it was changed outside the application,
// and so it can be saved on this computer (Remembered) without asking for it again.
public sealed class SettingsStore : IDisposable {
    private readonly VaultPolicy _policy;
    private SettingsVault _vault;
    private ProtectedSecret _masterPassword;

    private SettingsStore(SettingsVault vault, Settings settings, ProtectedSecret masterPassword, VaultPolicy policy, RememberedPassword? remembered) {
        _vault = vault;
        _masterPassword = masterPassword;
        _policy = policy;
        Remembered = remembered;
        Current = settings;
    }

    public event EventHandler? Changed;

    public Settings Current { get; private set; }

    public string Path => _vault.Path;

    // Where the master password is saved on this computer; null when it cannot be saved.
    public RememberedPassword? Remembered { get; }

    public bool IsRemembered => Remembered?.Exists == true;

    public static SettingsStore Create(string path, string password, RememberedPassword? remembered = null) =>
        Create(path, password, VaultPolicy.Default, remembered);

    public static SettingsStore Open(string path, string password, RememberedPassword? remembered = null) =>
        Open(path, password, VaultPolicy.Default, remembered);

    // Opens the settings file with the master password saved on this computer. There is no store when
    // nothing is saved, the settings file does not exist yet or the saved password does not fit; the
    // state says which. A settings file that cannot be read throws as in Open.
    public static SettingsStore? OpenRemembered(string path, RememberedPassword remembered, out RememberedPasswordState state) =>
        OpenRemembered(path, remembered, VaultPolicy.Default, out state);

    internal static SettingsStore Create(string path, string password, VaultPolicy policy, RememberedPassword? remembered = null) {
        var settings = new Settings();
        var vault = SettingsVault.Create(path, password, settings, policy);
        return new SettingsStore(vault, settings, ProtectedSecret.Create(password), policy, remembered);
    }

    internal static SettingsStore Open(string path, string password, VaultPolicy policy, RememberedPassword? remembered = null) {
        var vault = SettingsVault.Open(path, password, policy, out var settings);
        return new SettingsStore(vault, SettingsEditor.Normalize(settings), ProtectedSecret.Create(password), policy, remembered);
    }

    internal static SettingsStore? OpenRemembered(string path, RememberedPassword remembered, VaultPolicy policy, out RememberedPasswordState state) {
        state = RememberedPasswordState.Missing;
        if (!remembered.Exists || !File.Exists(path)) {
            return null;
        }
        state = remembered.Read(SettingsVault.ReadSalt(path), out var password);
        if (state != RememberedPasswordState.Fits) {
            return null;
        }
        try {
            return Open(path, password!.Reveal(), policy, remembered);
        } catch (VaultException ex) when (ex.Error == VaultError.WrongPassword) {
            // The file was replaced between reading its salt and opening it.
            state = RememberedPasswordState.PasswordChanged;
            return null;
        }
    }

    public void Apply(Func<Settings, Settings> change, bool overwrite = false) {
        var updated = SettingsEditor.Normalize(change(Current));
        _vault.Save(updated, overwrite);
        Current = updated;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Throws VaultException with WrongPassword when the file now has another master password.
    public void Reload() => Reload(_masterPassword.Reveal());

    // With the master password saved on this computer and the file now under another master password,
    // the saved one is replaced; when that fails, the file has been read and RememberedPasswordException
    // is thrown.
    public void Reload(string password) {
        var vault = SettingsVault.Open(Path, password, _policy, out var settings);
        var newPassword = !vault.Salt.SequenceEqual(_vault.Salt);
        _vault.Dispose();
        _vault = vault;
        _masterPassword = ProtectedSecret.Create(password);
        Current = SettingsEditor.Normalize(settings);
        Changed?.Invoke(this, EventArgs.Empty);
        if (newPassword) {
            RefreshRemembered();
        }
    }

    public bool IsMasterPassword(string password) => _vault.IsCurrentPassword(password);

    // The master password saved on this computer is replaced too; when that fails, the password has been
    // changed and RememberedPasswordException is thrown.
    public void ChangeMasterPassword(string newPassword, bool overwrite = false) {
        _vault.ChangePassword(newPassword, Current, overwrite);
        _masterPassword = ProtectedSecret.Create(newPassword);
        RefreshRemembered();
    }

    // Saves the current master password on this computer. When that fails, a copy saved before is removed:
    // it would not fit, yet it would show as saved.
    public void Remember() {
        var remembered = Remembered ?? throw new InvalidOperationException("There is no place to save the master password.");
        try {
            remembered.Save(_masterPassword, _vault.Salt);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException) {
            try {
                remembered.Delete();
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
            throw new RememberedPasswordException($"The master password could not be saved on this computer. {ex.Message}", ex);
        }
    }

    public void Forget() {
        try {
            Remembered?.Delete();
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new RememberedPasswordException($"The master password saved on this computer could not be removed. {ex.Message}", ex);
        }
    }

    public void Dispose() {
        _vault.Dispose();
    }

    private void RefreshRemembered() {
        if (!IsRemembered) {
            return;
        }
        try {
            Remember();
        } catch (RememberedPasswordException ex) {
            throw new RememberedPasswordException(
                $"The master password saved on this computer could not be updated, so RemoteMe will ask for it at the next start. {ex.InnerException!.Message}",
                ex.InnerException);
        }
    }
}
