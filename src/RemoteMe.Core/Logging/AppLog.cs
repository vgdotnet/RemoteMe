namespace RemoteMe.Core.Logging;

public enum LogLevel {
    Information,
    Warning,
    Error,
}

public sealed record LogEntry(DateTimeOffset Time, LogLevel Level, string Source, string Message);

// Kept in memory only, never written to disk. Once full, the oldest entries are dropped. Callers never
// pass secrets here.
public sealed class AppLog {
    private readonly Lock _gate = new();
    private readonly Queue<LogEntry> _entries = new();
    private readonly int _capacity;
    private readonly TimeProvider _time;

    public AppLog(int capacity = 5000, TimeProvider? time = null) {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _time = time ?? TimeProvider.System;
    }

    public event EventHandler<LogEntry>? Added;

    public event EventHandler? Cleared;

    public IReadOnlyList<LogEntry> Entries {
        get {
            lock (_gate) {
                return _entries.ToArray();
            }
        }
    }

    public void Information(string source, string message) => Write(LogLevel.Information, source, message);

    public void Warning(string source, string message) => Write(LogLevel.Warning, source, message);

    public void Error(string source, string message) => Write(LogLevel.Error, source, message);

    public void Write(LogLevel level, string source, string message) {
        var entry = new LogEntry(_time.GetLocalNow(), level, source, message);
        lock (_gate) {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity) {
                _entries.Dequeue();
            }
        }
        Added?.Invoke(this, entry);
    }

    public void Clear() {
        lock (_gate) {
            _entries.Clear();
        }
        Cleared?.Invoke(this, EventArgs.Empty);
    }
}
