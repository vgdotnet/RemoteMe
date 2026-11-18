using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class ConnectionInitialsTests {
    [Theory]
    [InlineData("HOST", "HO")]
    [InlineData("agronet", "AG")]
    [InlineData("Legal Data Space", "LD")]
    [InlineData("PRO_AGENT", "PA")]
    [InlineData("LEGAL_DATA_SPACE", "LD")]
    [InlineData("web-front 2", "WF")]
    [InlineData("  dev   box ", "DB")]
    [InlineData("X", "X")]
    [InlineData("élan vital", "ÉV")]
    public void Of_TakesTwoLettersOfOneWordOrTheFirstLettersOfTwoWords(string name, string expected) {
        Assert.Equal(expected, ConnectionInitials.Of(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" _- ")]
    public void Of_ANameWithoutWords_IsEmpty(string name) {
        Assert.Equal("", ConnectionInitials.Of(name));
    }

    [Fact]
    public void Of_KeepsALetterWrittenWithTwoCharsWhole() {
        Assert.Equal("𝐀B", ConnectionInitials.Of("𝐀lpha Beta"));
    }
}
