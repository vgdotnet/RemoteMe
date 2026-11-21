using RemoteMe.Core.Model;
using RemoteMe.Core.Vault;

namespace RemoteMe.Tests.Vault;

public sealed class SettingsStoreRememberedTests : IDisposable {
    private const string Password = "correct horse battery staple";
    private const string NewPassword = "a completely new passphrase";
    private static readonly VaultPolicy FastPolicy = new(1_000, 1_000, 10_000_000);

    private readonly TempDirectory _directory = new();

    private string FilePath => _directory.Combine("RemoteMe.settings");

    private RememberedPassword ThisComputer => new(FilePath, "THIS-PC");

    private RememberedPassword OtherComputer => new(FilePath, "OTHER-PC");

    public void Dispose() {
        _directory.Dispose();
    }

    [Fact]
    public void OpenRemembered_AfterRemember_OpensWithoutThePassword() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Alpha")));
            Assert.False(store.IsRemembered);
            store.Remember();
            Assert.True(store.IsRemembered);
        }

        using var opened = SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state);

        Assert.Equal(RememberedPasswordState.Fits, state);
        Assert.NotNull(opened);
        Assert.Equal("Alpha", Assert.Single(opened.Current.Connections).Name);
        Assert.True(opened.IsMasterPassword(Password));
        Assert.True(opened.IsRemembered);
    }

    [Fact]
    public void OpenRemembered_WithNothingSaved_IsMissing() {
        SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer).Dispose();

        Assert.Null(SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state));
        Assert.Equal(RememberedPasswordState.Missing, state);
    }

    [Fact]
    public void OpenRemembered_WithoutASettingsFile_IsMissingAndDoesNotCreateOne() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Remember();
        }
        File.Delete(FilePath);

        Assert.Null(SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state));
        Assert.Equal(RememberedPasswordState.Missing, state);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void OpenRemembered_UsesOnlyTheCopyOfThisComputer() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, OtherComputer)) {
            store.Remember();
        }

        Assert.Null(SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state));
        Assert.Equal(RememberedPasswordState.Missing, state);
    }

    [Fact]
    public void OpenRemembered_AfterThePasswordChangedOnAnotherComputer_ReportsIt() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Remember();
        }
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy, OtherComputer)) {
            other.ChangeMasterPassword(NewPassword);
        }

        Assert.Null(SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state));
        Assert.Equal(RememberedPasswordState.PasswordChanged, state);
        Assert.True(ThisComputer.Exists);
    }

    [Fact]
    public void OpenRemembered_WithADamagedCopy_IsUnreadable() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Remember();
        }
        var file = File.ReadAllBytes(ThisComputer.Path);
        file[^1] ^= 0x01;
        File.WriteAllBytes(ThisComputer.Path, file);

        Assert.Null(SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state));
        Assert.Equal(RememberedPasswordState.Unreadable, state);
    }

    [Fact]
    public void OpenRemembered_WithADamagedSettingsFile_Throws() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Remember();
        }
        File.WriteAllBytes(FilePath, [1, 2, 3]);

        var error = Assert.Throws<VaultException>(() => SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out _));
        Assert.Equal(VaultError.InvalidFormat, error.Error);
    }

    [Fact]
    public void Forget_RemovesTheSavedPassword() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Remember();
            store.Forget();
            Assert.False(store.IsRemembered);
        }

        Assert.Null(SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state));
        Assert.Equal(RememberedPasswordState.Missing, state);
    }

    [Fact]
    public void Remember_WithoutAPlace_Throws() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);

        Assert.False(store.IsRemembered);
        Assert.Throws<InvalidOperationException>(store.Remember);
        store.Forget();
    }

    [Fact]
    public void Remember_WhenSavingFails_RemovesTheCopySavedBefore() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer);
        store.Remember();
        BlockTheNextSave();

        Assert.Throws<RememberedPasswordException>(store.Remember);

        Assert.False(store.IsRemembered);
    }

    [Fact]
    public void ChangeMasterPassword_ReplacesTheSavedPassword() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.Remember();
            store.ChangeMasterPassword(NewPassword);
        }

        using var opened = SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state);

        Assert.Equal(RememberedPasswordState.Fits, state);
        Assert.True(opened!.IsMasterPassword(NewPassword));
    }

    [Fact]
    public void ChangeMasterPassword_WithNothingSaved_SavesNothing() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer)) {
            store.ChangeMasterPassword(NewPassword);
        }

        Assert.False(ThisComputer.Exists);
    }

    [Fact]
    public void ChangeMasterPassword_WhenTheSavedPasswordCannotBeReplaced_ChangesThePasswordAndReportsIt() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer);
        store.Remember();
        BlockTheNextSave();

        Assert.Throws<RememberedPasswordException>(() => store.ChangeMasterPassword(NewPassword));

        Assert.True(store.IsMasterPassword(NewPassword));
        Assert.False(store.IsRemembered);
        using var reopened = SettingsStore.Open(FilePath, NewPassword, FastPolicy);
    }

    [Fact]
    public void Reload_WithTheNewPassword_ReplacesTheSavedPassword() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer);
        store.Remember();
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy, OtherComputer)) {
            other.ChangeMasterPassword(NewPassword);
        }

        store.Reload(NewPassword);

        using var opened = SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state);
        Assert.Equal(RememberedPasswordState.Fits, state);
        Assert.True(opened!.IsMasterPassword(NewPassword));
    }

    [Fact]
    public void Reload_WhenTheSavedPasswordCannotBeReplaced_ReadsTheFileAndReportsIt() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer);
        store.Remember();
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy, OtherComputer)) {
            other.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("From another machine")));
            other.ChangeMasterPassword(NewPassword);
        }
        BlockTheNextSave();

        Assert.Throws<RememberedPasswordException>(() => store.Reload(NewPassword));

        Assert.Equal("From another machine", Assert.Single(store.Current.Connections).Name);
        Assert.True(store.IsMasterPassword(NewPassword));
        Assert.False(store.IsRemembered);
        store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Local")));
    }

    [Fact]
    public void Reload_AfterTheSamePasswordWasSetAgainElsewhere_ReplacesTheSavedPassword() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer);
        store.Remember();
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy, OtherComputer)) {
            // A new master password, even the same one, gives the file a new salt.
            other.ChangeMasterPassword(Password);
        }

        store.Reload();

        using var opened = SettingsStore.OpenRemembered(FilePath, ThisComputer, FastPolicy, out var state);
        Assert.Equal(RememberedPasswordState.Fits, state);
    }

    [Fact]
    public void Reload_WithTheSamePassword_LeavesTheSavedPasswordAlone() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy, ThisComputer);
        store.Remember();
        var saved = File.ReadAllBytes(ThisComputer.Path);
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy, OtherComputer)) {
            other.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("From another machine")));
        }

        store.Reload();

        Assert.Equal(saved, File.ReadAllBytes(ThisComputer.Path));
    }

    // A folder where the temporary file of the atomic write goes makes the write fail.
    private void BlockTheNextSave() {
        Directory.CreateDirectory(ThisComputer.Path + ".tmp");
    }
}
