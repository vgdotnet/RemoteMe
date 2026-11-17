using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.Core.Vault;

// The settings the application works with. Every change is saved at once. The master password stays
// in memory (encrypted) so the file can be read again after it was changed outside the application.
public sealed class SettingsStore : IDisposable {
    private readonly VaultPolicy _policy;
    private SettingsVault _vault;
    private ProtectedSecret _masterPassword;

    private SettingsStore(SettingsVault vault, Settings settings, ProtectedSecret masterPassword, VaultPolicy policy) {
        _vault = vault;
        _masterPassword = masterPassword;
        _policy = policy;
        Current = settings;
    }

    public event EventHandler? Changed;

    public Settings Current { get; private set; }

    public string Path => _vault.Path;

    public static SettingsStore Create(string path, string password) => Create(path, password, VaultPolicy.Default);

    public static SettingsStore Open(string path, string password) => Open(path, password, VaultPolicy.Default);

    internal static SettingsStore Create(string path, string password, VaultPolicy policy) {
        var settings = new Settings();
        var vault = SettingsVault.Create(path, password, settings, policy);
        return new SettingsStore(vault, settings, ProtectedSecret.Create(password), policy);
    }

    internal static SettingsStore Open(string path, string password, VaultPolicy policy) {
        var vault = SettingsVault.Open(path, password, policy, out var settings);
        return new SettingsStore(vault, SettingsEditor.Normalize(settings), ProtectedSecret.Create(password), policy);
    }

    public void Apply(Func<Settings, Settings> change, bool overwrite = false) {
        var updated = SettingsEditor.Normalize(change(Current));
        _vault.Save(updated, overwrite);
        Current = updated;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Throws VaultException with WrongPassword when the file now has another master password.
    public void Reload() => Reload(_masterPassword.Reveal());

    public void Reload(string password) {
        var vault = SettingsVault.Open(Path, password, _policy, out var settings);
        _vault.Dispose();
        _vault = vault;
        _masterPassword = ProtectedSecret.Create(password);
        Current = SettingsEditor.Normalize(settings);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsMasterPassword(string password) => _vault.IsCurrentPassword(password);

    public void ChangeMasterPassword(string newPassword, bool overwrite = false) {
        _vault.ChangePassword(newPassword, Current, overwrite);
        _masterPassword = ProtectedSecret.Create(newPassword);
    }

    public void Dispose() {
        _vault.Dispose();
    }
}
