using System.Text.Json;
using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class ListBackgroundTests {
    [Fact]
    public void Of_TakesTheChosenColor() {
        Assert.Equal("Peach", ListBackground.Of(new Settings { Background = "Peach" }));
    }

    [Fact]
    public void Of_TakesNoneAsTheLastChoice() {
        Assert.Equal(ListBackground.None, OfChoice(ListBackground.None));
        Assert.Equal(ListBackground.None, ListBackground.Names[^1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Purple")]
    public void Of_WithoutAKnownChoice_IsTheFirstColor(string? background) {
        Assert.Equal("Green", ListBackground.Of(new Settings { Background = background }));
    }

    private static string OfChoice(string background) => ListBackground.Of(new Settings { Background = background });

    [Fact]
    public void Background_IsLeftOutOfTheFileWhileNotChosenAndReadBack() {
        var plain = JsonSerializer.Serialize(new Settings(), ModelJsonContext.Default.Settings);
        var chosen = JsonSerializer.Serialize(new Settings { Background = "Mint" }, ModelJsonContext.Default.Settings);
        var loaded = JsonSerializer.Deserialize(chosen, ModelJsonContext.Default.Settings);

        Assert.DoesNotContain("background", plain, StringComparison.Ordinal);
        Assert.Contains("\"background\":\"Mint\"", chosen, StringComparison.Ordinal);
        Assert.Equal("Mint", loaded!.Background);
    }
}
