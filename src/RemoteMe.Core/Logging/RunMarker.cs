using System.Globalization;

namespace RemoteMe.Core.Logging;

// A small file that exists while RemoteMe runs and holds the time it started. If the file is still there
// at the next start, the previous run did not close normally: it crashed, or it was ended while it was
// not responding.
public sealed class RunMarker(string path) {
    public string Path { get; } = path;

    // Marks this run as started and returns the start of the previous run when that run did not close
    // normally, or null when it did.
    public DateTimeOffset? Begin(DateTimeOffset now) {
        var previous = Read();
        try {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.WriteAllText(Path, now.ToString("o", CultureInfo.InvariantCulture));
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
        }
        return previous;
    }

    public void End() {
        try {
            File.Delete(Path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
        }
    }

    private DateTimeOffset? Read() {
        try {
            if (!File.Exists(Path)) {
                return null;
            }
            var text = File.ReadAllText(Path).Trim();
            return DateTimeOffset.TryParseExact(text, "o", CultureInfo.InvariantCulture, DateTimeStyles.None, out var started)
                ? started
                : new DateTimeOffset(File.GetLastWriteTime(Path));
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return null;
        }
    }
}
