using RemoteMe.Core.Security;

namespace RemoteMe.Tests.Security;

public sealed class ProtectedSecretTests {
    [Theory]
    [InlineData("plain")]
    [InlineData("p@ss \"word\" \\ ünïcode ✓ 😀")]
    public void Reveal_ReturnsTheOriginalText(string value) {
        Assert.Equal(value, ProtectedSecret.Create(value).Reveal());
    }

    [Fact]
    public void Create_WithEmptyText_IsEmpty() {
        var secret = ProtectedSecret.Create("");

        Assert.True(secret.IsEmpty);
        Assert.Equal("", secret.Reveal());
    }

    [Fact]
    public void ToString_DoesNotShowTheSecret() {
        Assert.DoesNotContain("hidden", ProtectedSecret.Create("hidden").ToString());
    }
}
