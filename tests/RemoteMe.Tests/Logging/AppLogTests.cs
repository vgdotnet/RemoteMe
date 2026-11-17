using RemoteMe.Core.Logging;

namespace RemoteMe.Tests.Logging;

public sealed class AppLogTests {
    [Fact]
    public void Write_KeepsEntriesInOrderAndRaisesAdded() {
        var log = new AppLog();
        var added = new List<LogEntry>();
        log.Added += (_, entry) => added.Add(entry);

        log.Information("A", "first");
        log.Error("B", "second");

        Assert.Equal(["first", "second"], log.Entries.Select(e => e.Message));
        Assert.Equal([LogLevel.Information, LogLevel.Error], added.Select(e => e.Level));
    }

    [Fact]
    public void Write_BeyondTheCapacity_DropsTheOldestEntries() {
        var log = new AppLog(capacity: 3);

        for (var i = 1; i <= 5; i++) {
            log.Information("Test", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.Equal(["3", "4", "5"], log.Entries.Select(e => e.Message));
    }

    [Fact]
    public void Clear_RemovesEverythingAndRaisesCleared() {
        var log = new AppLog();
        var cleared = false;
        log.Cleared += (_, _) => cleared = true;
        log.Warning("Test", "entry");

        log.Clear();

        Assert.Empty(log.Entries);
        Assert.True(cleared);
    }
}
