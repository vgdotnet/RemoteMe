using System.Globalization;
using System.Text;

namespace RemoteMe.Core.Logging;

// Writes log entries to a text file on a thread of its own, so a slow disk never holds up the window and
// a hung window does not stop the writing. The file is opened only for each write, so it can be read
// while RemoteMe runs. Once the file is over the limit it becomes the ".old" file, which replaces the
// previous one. Text that could not be written stays queued and is tried again a second later.
public sealed class LogFile : IDisposable {
    private const int MaxPending = 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly long _limit;
    private readonly Lock _pendingGate = new();
    private readonly Lock _fileGate = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _thread;
    private StringBuilder _pending = new();
    private volatile bool _stopping;
    private bool _disposed;

    public LogFile(string path, long limit = 1024 * 1024) {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        Path = path;
        OldPath = System.IO.Path.ChangeExtension(path, ".old" + System.IO.Path.GetExtension(path));
        _limit = limit;
        _thread = new Thread(Run) { IsBackground = true, Name = "Log file" };
        _thread.Start();
    }

    public string Path { get; }

    public string OldPath { get; }

    public void Write(LogEntry entry) {
        lock (_pendingGate) {
            if (_pending.Length < MaxPending) {
                _pending.Append(Format(entry));
            }
        }
        _signal.Set();
    }

    // Writes what is queued on the calling thread, for the moment just before the process ends. It waits
    // at most two seconds for a write in progress, so a disk that does not answer cannot hold the process.
    public void Flush() => WritePending(TimeSpan.FromSeconds(2));

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        _stopping = true;
        _signal.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
        WritePending();
        _signal.Dispose();
    }

    // One line per entry: time with its offset, level, source and message, separated by tabs. The lines
    // of a longer message, such as a stack trace, follow indented by a tab.
    public static string Format(LogEntry entry) {
        var message = entry.Message.ReplaceLineEndings("\n").TrimEnd('\n').Replace("\n", Environment.NewLine + "\t", StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture,
            $"{entry.Time:yyyy-MM-dd HH:mm:ss.fff zzz}\t{entry.Level}\t{entry.Source}\t{message}{Environment.NewLine}");
    }

    private void Run() {
        while (!_stopping) {
            _signal.WaitOne(TimeSpan.FromSeconds(1));
            WritePending();
        }
    }

    private void WritePending() => WritePending(Timeout.InfiniteTimeSpan);

    private void WritePending(TimeSpan wait) {
        if (!_fileGate.TryEnter(wait)) {
            return;
        }
        try {
            string text;
            lock (_pendingGate) {
                if (_pending.Length == 0) {
                    return;
                }
                text = _pending.ToString();
                _pending.Clear();
            }
            try {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
                Rotate();
                File.AppendAllText(Path, text, Utf8);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                lock (_pendingGate) {
                    if (text.Length + _pending.Length <= MaxPending) {
                        _pending.Insert(0, text);
                    }
                }
            }
        } finally {
            _fileGate.Exit();
        }
    }

    private void Rotate() {
        var file = new FileInfo(Path);
        if (file.Exists && file.Length >= _limit) {
            File.Move(Path, OldPath, overwrite: true);
        }
    }
}
