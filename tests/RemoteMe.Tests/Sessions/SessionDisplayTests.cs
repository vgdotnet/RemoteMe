using RemoteMe.Core.Sessions;

namespace RemoteMe.Tests.Sessions;

public sealed class SessionDisplayTests {
    [Theory]
    [InlineData(96, 100u, 100u)]
    [InlineData(120, 125u, 100u)]
    [InlineData(144, 150u, 140u)]
    [InlineData(192, 200u, 180u)]
    public void From_TakesTheScaleFromTheDpi(int dpi, uint desktopScale, uint deviceScale) {
        var display = SessionDisplay.From(1600, 900, dpi);

        Assert.Equal(desktopScale, display.DesktopScaleFactor);
        Assert.Equal(deviceScale, display.DeviceScaleFactor);
    }

    [Fact]
    public void From_MakesTheWidthEvenAndKeepsTheSizeInRange() {
        Assert.Equal(1604u, SessionDisplay.From(1605, 990, 144).Width);
        Assert.Equal((200u, 200u), (SessionDisplay.From(50, 10, 96).Width, SessionDisplay.From(50, 10, 96).Height));
        Assert.Equal(8192u, SessionDisplay.From(9000, 900, 96).Width);
    }

    [Fact]
    public void From_GivesThePhysicalSizeInMillimeters() {
        var display = SessionDisplay.From(1920, 1080, 96);

        Assert.Equal(508u, display.PhysicalWidthMm);
        Assert.Equal(286u, display.PhysicalHeightMm);
    }
}
