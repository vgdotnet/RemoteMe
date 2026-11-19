using RemoteMe.Core.Model;
using RemoteMe.Core.Vault;

namespace RemoteMe.Tests.Vault;

public sealed class SettingsStoreTests : IDisposable {
    private const string Password = "correct horse battery staple";
    private static readonly VaultPolicy FastPolicy = new(1_000, 1_000, 10_000_000);

    private readonly TempDirectory _directory = new();

    private string FilePath => _directory.Combine("RemoteMe.settings");

    public void Dispose() {
        _directory.Dispose();
    }

    [Fact]
    public void Create_StartsWithEmptySettingsAndCreatesTheFile() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);

        Assert.Empty(store.Current.Connections);
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Apply_SavesAtOnceAndRaisesChanged() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Alpha", "Lab")));

        Assert.Equal(1, changed);
        using var reopened = SettingsStore.Open(FilePath, Password, FastPolicy);
        Assert.Equal("Alpha", Assert.Single(reopened.Current.Connections).Name);
        Assert.Equal(["Lab"], reopened.Current.Groups);
    }

    [Fact]
    public void Apply_WhenTheFileChangedOutside_ReportsConflictAndKeepsTheCurrentSettings() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy)) {
            other.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("From another machine")));
        }

        var error = Assert.Throws<VaultException>(() => store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Local"))));

        Assert.Equal(VaultError.Conflict, error.Error);
        Assert.Empty(store.Current.Connections);
    }

    [Fact]
    public void Apply_WithOverwrite_ReplacesAFileChangedOutside() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy)) {
            other.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("From another machine")));
        }

        store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Local")), overwrite: true);

        using var reopened = SettingsStore.Open(FilePath, Password, FastPolicy);
        Assert.Equal("Local", Assert.Single(reopened.Current.Connections).Name);
    }

    [Fact]
    public void Apply_KeepsTheClockTimeZoneForTheNextOpen() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy)) {
            store.Apply(s => s with { ClockTimeZone = "GMT Standard Time" });
            store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Alpha")));
        }

        using var reopened = SettingsStore.Open(FilePath, Password, FastPolicy);
        Assert.Equal("GMT Standard Time", reopened.Current.ClockTimeZone);
    }

    [Fact]
    public void Apply_KeepsTheBackgroundForTheNextOpen() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy)) {
            store.Apply(s => s with { Background = "Lavender" });
        }

        using var reopened = SettingsStore.Open(FilePath, Password, FastPolicy);
        Assert.Equal("Lavender", reopened.Current.Background);
    }

    [Fact]
    public void Apply_KeepsCollapsedGroupsForTheNextOpen() {
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy)) {
            store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Alpha", "Lab")));
            store.Apply(s => SettingsEditor.SetGroupCollapsed(s, "Lab", collapsed: true));
        }

        using var reopened = SettingsStore.Open(FilePath, Password, FastPolicy);
        Assert.Equal(["Lab"], reopened.Current.CollapsedGroups);
    }

    [Fact]
    public void Reload_ReadsChangesMadeOutside() {
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy)) {
            other.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("From another machine")));
        }
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.Reload();

        Assert.Equal(1, changed);
        Assert.Equal("From another machine", Assert.Single(store.Current.Connections).Name);
        store.Apply(s => SettingsEditor.AddConnection(s, SampleData.Connection("Local")));
    }

    [Fact]
    public void Reload_AfterThePasswordChangedOutside_NeedsTheNewPassword() {
        const string otherPassword = "changed on another machine";
        using var store = SettingsStore.Create(FilePath, Password, FastPolicy);
        using (var other = SettingsStore.Open(FilePath, Password, FastPolicy)) {
            other.ChangeMasterPassword(otherPassword);
        }

        var error = Assert.Throws<VaultException>(store.Reload);
        Assert.Equal(VaultError.WrongPassword, error.Error);

        store.Reload(otherPassword);
        store.Reload();
        Assert.True(store.IsMasterPassword(otherPassword));
    }

    [Fact]
    public void ChangeMasterPassword_IsUsedForTheNextOpen() {
        const string newPassword = "a completely new passphrase";
        using (var store = SettingsStore.Create(FilePath, Password, FastPolicy)) {
            store.ChangeMasterPassword(newPassword);
            Assert.True(store.IsMasterPassword(newPassword));
            Assert.False(store.IsMasterPassword(Password));
        }

        using var reopened = SettingsStore.Open(FilePath, newPassword, FastPolicy);
    }
}
