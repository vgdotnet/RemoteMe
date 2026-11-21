using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RemoteMe.Core.Security;
using RemoteMe.Core.Vault;
using RemoteMe.Tests.Security;

namespace RemoteMe.Tests.Vault;

public sealed class RememberedPasswordTests : IDisposable {
    private const string Password = "correct horse battery staple";
    private const string Computer = "TEST-PC";

    private readonly TempDirectory _directory = new();

    private string SettingsPath => _directory.Combine("RemoteMe.settings");

    public void Dispose() {
        _directory.Dispose();
    }

    [Fact]
    public void Path_IsNextToTheSettingsFileAndNamedAfterTheComputer() {
        var remembered = new RememberedPassword(SettingsPath, Computer);

        Assert.Equal(_directory.Combine("RemoteMe.TEST-PC.unlock"), remembered.Path);
        Assert.NotEqual(remembered.Path, new RememberedPassword(SettingsPath, "OTHER-PC").Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BAD/NAME")]
    [InlineData("BAD:NAME")]
    public void Constructor_RefusesAComputerIdThatIsNotAFileName(string computerId) {
        Assert.ThrowsAny<ArgumentException>(() => new RememberedPassword(SettingsPath, computerId));
    }

    [Fact]
    public void ForThisComputer_NamesTheFileAfterTheComputerAndAHashOfItsIdAndUser() {
        var remembered = RememberedPassword.ForThisComputer(SettingsPath);

        var name = Path.GetFileName(remembered.Path);
        Assert.Matches($@"^RemoteMe\.{Regex.Escape(Environment.MachineName)}-[0-9a-f]{{8}}\.unlock$", name);
        Assert.Equal(_directory.Path, Path.GetDirectoryName(remembered.Path));
        Assert.Equal(remembered.Path, RememberedPassword.ForThisComputer(SettingsPath).Path);
    }

    [Theory]
    [InlineData(Password)]
    [InlineData("пароль на русском")]
    [InlineData(" spaces at both ends ")]
    public void Read_AfterSave_GivesThePassword(string password) {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        var salt = NewSalt();

        remembered.Save(ProtectedSecret.Create(password), salt);
        var state = remembered.Read(salt, out var read);

        Assert.Equal(RememberedPasswordState.Fits, state);
        Assert.Equal(password, read!.Reveal());
    }

    [Fact]
    public void Read_WithoutAFile_IsMissing() {
        var remembered = new RememberedPassword(SettingsPath, Computer);

        Assert.False(remembered.Exists);
        Assert.Equal(RememberedPasswordState.Missing, remembered.Read(NewSalt(), out var read));
        Assert.Null(read);
    }

    [Fact]
    public void Read_ForAnotherSalt_ReportsThePasswordChanged() {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        remembered.Save(ProtectedSecret.Create(Password), NewSalt());

        Assert.Equal(RememberedPasswordState.PasswordChanged, remembered.Read(NewSalt(), out var read));
        Assert.Null(read);
    }

    [Fact]
    public void Read_WhenTheSaltInTheFileIsReplaced_CannotDecrypt() {
        // The salt is part of the DPAPI entropy, so a blob cannot be moved over to another settings file
        // by rewriting the salt in clear.
        var remembered = new RememberedPassword(SettingsPath, Computer);
        remembered.Save(ProtectedSecret.Create(Password), NewSalt());
        var otherSalt = NewSalt();
        var file = File.ReadAllBytes(remembered.Path);
        otherSalt.CopyTo(file, 12);
        File.WriteAllBytes(remembered.Path, file);

        Assert.Equal(RememberedPasswordState.Unreadable, remembered.Read(otherSalt, out var read));
        Assert.Null(read);
    }

    [Fact]
    public void Read_WithADamagedBlob_IsUnreadable() {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        var salt = NewSalt();
        remembered.Save(ProtectedSecret.Create(Password), salt);
        var file = File.ReadAllBytes(remembered.Path);
        file[^1] ^= 0x01;
        File.WriteAllBytes(remembered.Path, file);

        Assert.Equal(RememberedPasswordState.Unreadable, remembered.Read(salt, out var read));
        Assert.Null(read);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(44)]
    public void Read_WithACutFile_IsUnreadable(int length) {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        var salt = NewSalt();
        remembered.Save(ProtectedSecret.Create(Password), salt);
        File.WriteAllBytes(remembered.Path, File.ReadAllBytes(remembered.Path).AsSpan(0, length).ToArray());

        Assert.Equal(RememberedPasswordState.Unreadable, remembered.Read(salt, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    public void Read_WithAWrongHeader_IsUnreadable(int offset) {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        var salt = NewSalt();
        remembered.Save(ProtectedSecret.Create(Password), salt);
        var file = File.ReadAllBytes(remembered.Path);
        file[offset] ^= 0x40;
        File.WriteAllBytes(remembered.Path, file);

        Assert.Equal(RememberedPasswordState.Unreadable, remembered.Read(salt, out _));
    }

    [Fact]
    public void Read_ATooLargeFile_IsUnreadable() {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        File.WriteAllBytes(remembered.Path, new byte[64 * 1024 + 1]);

        Assert.Equal(RememberedPasswordState.Unreadable, remembered.Read(NewSalt(), out _));
    }

    [Fact]
    public void Read_ACopyProtectedForTheWholeComputer_IsUnreadable() {
        // Another user of this computer could make such a copy for a settings file of their own.
        var remembered = new RememberedPassword(SettingsPath, Computer);
        var salt = NewSalt();
        var entropy = Encoding.UTF8.GetBytes("RemoteMe remembered master password").Concat(salt).ToArray();
        var blob = DpapiTests.ProtectForTheComputer(Encoding.UTF8.GetBytes(Password), entropy);
        var file = new byte[44 + blob.Length];
        "RMUnlock"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(8), 1);
        salt.CopyTo(file, 12);
        blob.CopyTo(file, 44);
        File.WriteAllBytes(remembered.Path, file);

        Assert.Equal(RememberedPasswordState.Unreadable, remembered.Read(salt, out var read));
        Assert.Null(read);
    }

    [Fact]
    public void Save_DoesNotWriteThePasswordInClear() {
        var remembered = new RememberedPassword(SettingsPath, Computer);

        remembered.Save(ProtectedSecret.Create(Password), NewSalt());

        var file = File.ReadAllBytes(remembered.Path);
        Assert.Equal(-1, file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Password)));
        Assert.Equal(-1, file.AsSpan().IndexOf(Encoding.Unicode.GetBytes(Password)));
    }

    [Fact]
    public void Save_RefusesASaltOfTheWrongSize() {
        var remembered = new RememberedPassword(SettingsPath, Computer);

        Assert.Throws<ArgumentException>(() => remembered.Save(ProtectedSecret.Create(Password), new byte[16]));
        Assert.False(remembered.Exists);
    }

    [Fact]
    public void Delete_RemovesTheFileAndDoesNothingWithoutOne() {
        var remembered = new RememberedPassword(SettingsPath, Computer);
        remembered.Save(ProtectedSecret.Create(Password), NewSalt());

        remembered.Delete();
        remembered.Delete();

        Assert.False(remembered.Exists);
    }

    private static byte[] NewSalt() => RandomNumberGenerator.GetBytes(VaultHeader.SaltSize);
}
