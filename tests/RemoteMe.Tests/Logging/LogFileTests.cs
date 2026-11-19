using RemoteMe.Core.Logging;

namespace RemoteMe.Tests.Logging;

public sealed class LogFileTests {
    private static readonly DateTimeOffset Time = new(2026, 11, 19, 12, 3, 4, 56, TimeSpan.FromHours(1));

    [Fact]
    public void Format_PutsTimeLevelSourceAndMessageOnOneLine() {
        var line = LogFile.Format(new LogEntry(Time, LogLevel.Warning, "HOST", "Disconnected."));

        Assert.Equal("2026-11-19 12:03:04.056 +01:00\tWarning\tHOST\tDisconnected." + Environment.NewLine, line);
    }

    [Fact]
    public void Format_IndentsTheFollowingLinesOfAMessage() {
        var line = LogFile.Format(new LogEntry(Time, LogLevel.Error, "Application", "Boom\r\n   at A()\n   at B()\n"));

        var nl = Environment.NewLine;
        Assert.Equal($"2026-11-19 12:03:04.056 +01:00\tError\tApplication\tBoom{nl}\t   at A(){nl}\t   at B(){nl}", line);
    }

    [Fact]
    public void Dispose_WritesEveryEntryAndCreatesTheFolder() {
        using var temp = new TempDirectory();
        var path = temp.Combine(Path.Combine("Logs", "PC.log"));

        using (var file = new LogFile(path)) {
            file.Write(new LogEntry(Time, LogLevel.Information, "A", "first"));
            file.Write(new LogEntry(Time, LogLevel.Error, "B", "second"));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("\tA\tfirst", lines[0]);
        Assert.EndsWith("\tB\tsecond", lines[1]);
    }

    [Fact]
    public void Flush_WritesOnTheCallingThread() {
        using var temp = new TempDirectory();
        var path = temp.Combine("PC.log");
        using var file = new LogFile(path);

        file.Write(new LogEntry(Time, LogLevel.Error, "Application", "crash"));
        file.Flush();

        Assert.EndsWith("\tApplication\tcrash", File.ReadAllLines(path).Last());
    }

    [Fact]
    public void Write_AppendsToTheFileOfAnEarlierRun() {
        using var temp = new TempDirectory();
        var path = temp.Combine("PC.log");
        File.WriteAllText(path, "earlier run" + Environment.NewLine);

        using (var file = new LogFile(path)) {
            file.Write(new LogEntry(Time, LogLevel.Information, "Application", "started"));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal("earlier run", lines[0]);
        Assert.EndsWith("\tstarted", lines[1]);
    }

    [Fact]
    public void Write_OverTheLimit_MovesTheFileToOldReplacingThePreviousOne() {
        using var temp = new TempDirectory();
        var path = temp.Combine("PC.log");
        File.WriteAllText(path, new string('x', 200));
        File.WriteAllText(temp.Combine("PC.old.log"), "older");

        using (var file = new LogFile(path, limit: 100)) {
            Assert.Equal(temp.Combine("PC.old.log"), file.OldPath);
            file.Write(new LogEntry(Time, LogLevel.Information, "Application", "after"));
        }

        Assert.Equal(new string('x', 200), File.ReadAllText(temp.Combine("PC.old.log")));
        Assert.EndsWith("\tafter", File.ReadAllLines(path).Single());
    }

    [Fact]
    public void Write_WhileTheFileIsLocked_KeepsTheTextAndWritesItLater() {
        using var temp = new TempDirectory();
        var path = temp.Combine("PC.log");
        using var file = new LogFile(path);

        using (new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
            file.Write(new LogEntry(Time, LogLevel.Information, "A", "while locked"));
            file.Flush();
        }
        file.Write(new LogEntry(Time, LogLevel.Information, "B", "after"));
        file.Flush();

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("\twhile locked", lines[0]);
        Assert.EndsWith("\tafter", lines[1]);
    }
}
