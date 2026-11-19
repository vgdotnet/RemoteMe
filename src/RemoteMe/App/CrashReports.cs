using System.Diagnostics.Eventing.Reader;
using RemoteMe.Core.Logging;

namespace RemoteMe.App;

// What Windows wrote to its Application log about RemoteMe since a given time: a crash of the process
// ("Application Error", event 1000), a program closed because its file could not be read (1005), an
// exception .NET could not hand to RemoteMe (".NET Runtime", 1026, with the stack), or a close while the
// window was not responding ("Application Hang", 1002). A crash inside the Remote Desktop component, or
// an exe that could not be read from its disk, ends the process before RemoteMe can write anything, so
// these records are the only trace of it. The records are matched by their fields, which do not depend
// on the language of Windows. Reading the log needs no administrator rights.
internal static class CrashReports {
    private const string Source = "Windows";
    private static readonly string[] Providers = ["Application Error", ".NET Runtime", "Application Hang"];

    public static void CopyToLog(AppLog log, DateTimeOffset since, string exe) => Task.Run(() => {
        try {
            var found = 0;
            foreach (var text in Read(since, exe)) {
                log.Error(Source, text);
                found++;
            }
            if (found == 0) {
                log.Information(Source, "The Windows Application log has no crash records of RemoteMe since then.");
            }
        } catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException) {
            log.Warning(Source, $"The Windows Application log could not be read: {ex.Message}");
        }
    });

    private static IEnumerable<string> Read(DateTimeOffset since, string exe) {
        var time = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        var query = new EventLogQuery("Application", PathType.LogName,
            $"*[System[(EventID=1000 or EventID=1002 or EventID=1005 or EventID=1026) and TimeCreated[@SystemTime>='{time}']]]");
        using var reader = new EventLogReader(query);
        for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent()) {
            using (record) {
                if (Providers.Contains(record.ProviderName) && IsAbout(record, exe)) {
                    yield return $"{record.ProviderName}, event {record.Id}, {record.TimeCreated:yyyy-MM-dd HH:mm:ss}: {Describe(record)}";
                }
            }
        }
    }

    // Events 1000 and 1002 name the exe, 1005 the program without ".exe", and 1026 starts its only field
    // with "Application: " and the exe.
    private static bool IsAbout(EventRecord record, string exe) {
        var name = Path.GetFileNameWithoutExtension(exe);
        return record.Properties.Any(p => p.Value is string value
            && (value.Equals(exe, StringComparison.OrdinalIgnoreCase)
                || value.Equals(name, StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("Application: " + exe, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Describe(EventRecord record) {
        try {
            if (record.FormatDescription() is { Length: > 0 } description) {
                return description.Trim();
            }
        } catch (EventLogException) {
        }
        return string.Join("; ", record.Properties.Select(p => p.Value?.ToString()));
    }
}
