using RemoteMe.Core.Model;
using RemoteMe.Core.Vault;

namespace RemoteMe.Tests.Vault;

public sealed class SettingsVaultTests : IDisposable {
    private const string Password = "correct horse battery staple";
    private static readonly VaultPolicy FastPolicy = new(1_000, 1_000, 10_000_000);

    private readonly TempDirectory _directory = new();

    private string FilePath => _directory.Combine("settings.vault");

    public void Dispose() {
        _directory.Dispose();
    }

    [Fact]
    public void Open_AfterCreate_ReturnsTheSameSettings() {
        var settings = SampleData.Settings();
        CreateFile(settings);

        using var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out var loaded);

        SampleData.AssertSame(settings, loaded);
        Assert.Equal(1UL, vault.Revision);
    }

    [Fact]
    public void Open_WithDefaultPolicy_UsesAtLeast600000Iterations() {
        Assert.True(VaultPolicy.Default.Iterations >= 600_000);
        var settings = SampleData.Settings();
        SettingsVault.Create(FilePath, Password, settings).Dispose();

        using var vault = SettingsVault.Open(FilePath, Password, out var loaded);

        SampleData.AssertSame(settings, loaded);
    }

    [Fact]
    public void Open_WithWrongPassword_ReportsWrongPassword() {
        CreateFile();

        AssertOpenFails(VaultError.WrongPassword, "not the password");
    }

    [Theory]
    [InlineData("пароль на русском")]
    [InlineData(" spaces at both ends ")]
    [InlineData("Mixed ЯЗЫКИ 123 !@#$%^&*()")]
    public void Open_WithTheSamePassword_WorksForAnyText(string password) {
        SettingsVault.Create(FilePath, password, SampleData.Settings(), FastPolicy).Dispose();

        using var vault = SettingsVault.Open(FilePath, password, FastPolicy, out _);

        Assert.Throws<VaultException>(() => SettingsVault.Open(FilePath, password.Trim() + "x", FastPolicy, out _));
    }

    [Fact]
    public void Open_WithDifferentUnicodeNormalization_AcceptsThePassword() {
        SettingsVault.Create(FilePath, "café au lait", SampleData.Settings(), FastPolicy).Dispose();

        using var vault = SettingsVault.Open(FilePath, "café au lait", FastPolicy, out _);
    }

    [Fact]
    public void Open_WithDamagedCiphertext_ReportsDamage() {
        CreateFile();
        Tamper(file => file[VaultHeader.Size] ^= 1);

        AssertOpenFails(VaultError.Damaged);
    }

    [Fact]
    public void Open_WithDamagedTag_ReportsDamage() {
        CreateFile();
        Tamper(file => file[^1] ^= 1);

        AssertOpenFails(VaultError.Damaged);
    }

    [Fact]
    public void Open_WithChangedRevisionInHeader_ReportsDamage() {
        CreateFile();
        Tamper(file => file[80] ^= 1);

        AssertOpenFails(VaultError.Damaged);
    }

    [Fact]
    public void Open_WithChangedSalt_ReportsWrongPassword() {
        CreateFile();
        Tamper(file => file[16] ^= 1);

        AssertOpenFails(VaultError.WrongPassword);
    }

    [Fact]
    public void Open_WithIterationsBelowPolicy_ReportsParametersOutOfRange() {
        CreateFile();

        var error = Assert.Throws<VaultException>(() => SettingsVault.Open(FilePath, Password, VaultPolicy.Default, out _));

        Assert.Equal(VaultError.ParametersOutOfRange, error.Error);
    }

    [Fact]
    public void Open_WithIterationsAbovePolicy_ReportsParametersOutOfRange() {
        CreateFile();
        Tamper(file => BitConverter.TryWriteBytes(file.AsSpan(12, 4), uint.MaxValue));

        AssertOpenFails(VaultError.ParametersOutOfRange);
    }

    [Fact]
    public void Open_WithNewerFormatVersion_ReportsUnsupportedVersion() {
        CreateFile();
        Tamper(file => file[8] = 2);

        AssertOpenFails(VaultError.UnsupportedVersion);
    }

    [Fact]
    public void Open_WithNewerSchema_ReportsUnsupportedSchema() {
        CreateFile(new Settings { SchemaVersion = Settings.CurrentSchemaVersion + 1 });

        AssertOpenFails(VaultError.UnsupportedSchema);
    }

    [Fact]
    public void Open_WithForeignFile_ReportsInvalidFormat() {
        File.WriteAllText(FilePath, new string('x', 1000));

        AssertOpenFails(VaultError.InvalidFormat);
    }

    [Fact]
    public void Open_WithTruncatedFile_ReportsInvalidFormat() {
        CreateFile();
        File.WriteAllBytes(FilePath, File.ReadAllBytes(FilePath)[..50]);

        AssertOpenFails(VaultError.InvalidFormat);
    }

    [Fact]
    public void Create_WhenTheFileExists_Throws() {
        CreateFile();

        Assert.Throws<IOException>(() => SettingsVault.Create(FilePath, Password, new Settings(), FastPolicy));
    }

    [Fact]
    public void Save_WritesANewRevisionWithANewNonce() {
        CreateFile();
        var nonceBefore = File.ReadAllBytes(FilePath)[88..100];
        var changed = SampleData.Settings(password: "another secret");

        using (var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out _)) {
            vault.Save(changed);
            Assert.Equal(2UL, vault.Revision);
        }

        Assert.NotEqual(nonceBefore, File.ReadAllBytes(FilePath)[88..100]);
        using var reopened = SettingsVault.Open(FilePath, Password, FastPolicy, out var loaded);
        Assert.Equal(2UL, reopened.Revision);
        SampleData.AssertSame(changed, loaded);
    }

    [Fact]
    public void Save_LeavesNoTemporaryFile() {
        CreateFile();
        using var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out var settings);

        vault.Save(settings);

        Assert.Equal([FilePath], Directory.GetFileSystemEntries(_directory.Path));
    }

    [Fact]
    public void Save_WhenTheFileChangedOnDisk_ReportsConflictAndKeepsTheFile() {
        CreateFile();
        using var first = SettingsVault.Open(FilePath, Password, FastPolicy, out var settings);
        using (var second = SettingsVault.Open(FilePath, Password, FastPolicy, out _)) {
            second.Save(settings);
        }
        var afterSecond = File.ReadAllBytes(FilePath);

        var error = Assert.Throws<VaultException>(() => first.Save(settings));

        Assert.Equal(VaultError.Conflict, error.Error);
        Assert.Equal(afterSecond, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void Save_WithOverwrite_ReplacesAFileChangedOnDisk() {
        CreateFile();
        using var first = SettingsVault.Open(FilePath, Password, FastPolicy, out var settings);
        using (var second = SettingsVault.Open(FilePath, Password, FastPolicy, out _)) {
            second.Save(new Settings());
        }

        first.Save(settings, overwrite: true);

        using var reopened = SettingsVault.Open(FilePath, Password, FastPolicy, out var loaded);
        SampleData.AssertSame(settings, loaded);
        first.Save(settings);
    }

    [Fact]
    public void Save_WhenTheFileWasDeleted_ReportsConflict() {
        CreateFile();
        using var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out var settings);
        File.Delete(FilePath);

        var error = Assert.Throws<VaultException>(() => vault.Save(settings));

        Assert.Equal(VaultError.Conflict, error.Error);
    }

    [Fact]
    public void Save_WhenWritingFails_KeepsThePreviousFile() {
        CreateFile();
        var before = File.ReadAllBytes(FilePath);
        using var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out var settings);
        var blocker = Directory.CreateDirectory(FilePath + ".tmp");

        var error = Record.Exception(() => vault.Save(settings));

        Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected {error?.GetType().Name ?? "success"}");
        Assert.Equal(before, File.ReadAllBytes(FilePath));
        Assert.Equal(1UL, vault.Revision);

        blocker.Delete();
        vault.Save(settings);
        Assert.Equal(2UL, vault.Revision);
    }

    [Fact]
    public void ChangePassword_ReplacesTheOldPasswordAndKeepsTheSettings() {
        var settings = SampleData.Settings();
        CreateFile(settings);
        const string newPassword = "a completely new passphrase";

        using (var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out var loaded)) {
            vault.ChangePassword(newPassword, loaded);
            Assert.True(vault.IsCurrentPassword(newPassword));
            Assert.False(vault.IsCurrentPassword(Password));
        }

        AssertOpenFails(VaultError.WrongPassword, Password);
        using var reopened = SettingsVault.Open(FilePath, newPassword, FastPolicy, out var reloaded);
        Assert.Equal(2UL, reopened.Revision);
        SampleData.AssertSame(settings, reloaded);
    }

    [Fact]
    public void IsCurrentPassword_TellsTheRightPasswordFromAWrongOne() {
        CreateFile();
        using var vault = SettingsVault.Open(FilePath, Password, FastPolicy, out _);

        Assert.True(vault.IsCurrentPassword(Password));
        Assert.False(vault.IsCurrentPassword(Password + "!"));
    }

    private void CreateFile(Settings? settings = null) {
        SettingsVault.Create(FilePath, Password, settings ?? SampleData.Settings(), FastPolicy).Dispose();
    }

    private void Tamper(Action<byte[]> change) {
        var file = File.ReadAllBytes(FilePath);
        change(file);
        File.WriteAllBytes(FilePath, file);
    }

    private void AssertOpenFails(VaultError expected, string password = Password) {
        var error = Assert.Throws<VaultException>(() => SettingsVault.Open(FilePath, password, FastPolicy, out _));
        Assert.Equal(expected, error.Error);
    }
}
