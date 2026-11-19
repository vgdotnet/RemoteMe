using RemoteMe.Core.About;

namespace RemoteMe.Tests.About;

public sealed class ProductVersionTests {
    [Theory]
    [InlineData("0.6.0", "57730facd2a6ea60c34f8e74f3b71df23aa8130a", "0.6.0 (57730fa)")]
    [InlineData("0.6.0", null, "0.6.0")]
    [InlineData("0.6.0", " ", "0.6.0")]
    [InlineData("0.6.0+57730facd2a6", null, "0.6.0")]
    [InlineData("0.6.0", "abc", "0.6.0 (abc)")]
    [InlineData(null, "57730facd2a6", "unknown (57730fa)")]
    public void Format_GivesTheVersionAndTheShortCommit(string? version, string? revision, string expected) {
        Assert.Equal(expected, ProductVersion.Format(version, revision));
    }
}
