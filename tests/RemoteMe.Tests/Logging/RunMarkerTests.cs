using RemoteMe.Core.Logging;

namespace RemoteMe.Tests.Logging;

public sealed class RunMarkerTests {
    private static readonly DateTimeOffset First = new(2026, 11, 19, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset Second = new(2026, 11, 19, 10, 30, 0, TimeSpan.FromHours(1));

    [Fact]
    public void Begin_AfterARunThatClosed_ReturnsNull() {
        using var temp = new TempDirectory();
        var marker = new RunMarker(temp.Combine(Path.Combine("Logs", "PC.running")));

        Assert.Null(marker.Begin(First));
        marker.End();

        Assert.Null(marker.Begin(Second));
    }

    [Fact]
    public void Begin_AfterARunThatDidNotClose_ReturnsItsStart() {
        using var temp = new TempDirectory();
        var marker = new RunMarker(temp.Combine("PC.running"));

        marker.Begin(First);

        Assert.Equal(First, marker.Begin(Second));
        Assert.Equal(Second, marker.Begin(Second));
    }

    [Fact]
    public void Begin_WithAnUnreadableMarker_StillReportsTheRun() {
        using var temp = new TempDirectory();
        var path = temp.Combine("PC.running");
        File.WriteAllText(path, "not a time");
        var written = new DateTimeOffset(File.GetLastWriteTime(path));

        Assert.Equal(written, new RunMarker(path).Begin(Second));
    }

    [Fact]
    public void End_RemovesTheMarker() {
        using var temp = new TempDirectory();
        var marker = new RunMarker(temp.Combine("PC.running"));
        marker.Begin(First);

        marker.End();

        Assert.False(File.Exists(marker.Path));
    }
}
