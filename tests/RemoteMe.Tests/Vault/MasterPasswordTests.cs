using RemoteMe.Core.Vault;

namespace RemoteMe.Tests.Vault;

public sealed class MasterPasswordTests {
    [Theory]
    [InlineData("", true)]
    [InlineData("short", true)]
    [InlineData("elevenchars", true)]
    [InlineData("twelve chars", false)]
    [InlineData("a much longer passphrase", false)]
    public void IsWeak_DependsOnTheLength(string password, bool expected) {
        Assert.Equal(expected, MasterPassword.IsWeak(password));
    }
}
