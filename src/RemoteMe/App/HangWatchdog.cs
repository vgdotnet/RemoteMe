using System.Diagnostics;
using System.Globalization;
using RemoteMe.Core.Logging;

namespace RemoteMe.App;

// Writes to the log when the main window stops answering for five seconds and when it answers again.
// Its own thread sends the window a message once a second, so the entry reaches the log file while the
// window still hangs, and a run that was ended in that state leaves the entry as its last one.
internal sealed class HangWatchdog : IDisposable {
    private const string Source = "Application";
    private static readonly TimeSpan Threshold = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Control _window;
    private readonly AppLog _log;
    private readonly long _start = Stopwatch.GetTimestamp();
    private readonly ManualResetEventSlim _stop = new(false);
    private readonly HangDetector _detector;
    private readonly Action _answer;
    private Thread? _thread;
    private int _waiting;

    public HangWatchdog(Control window, AppLog log) {
        _window = window;
        _log = log;
        _detector = new HangDetector(Threshold, Now);
        _answer = () => {
            _detector.Answered(Now);
            Volatile.Write(ref _waiting, 0);
        };
    }

    private TimeSpan Now => Stopwatch.GetElapsedTime(_start);

    public void Start() {
        if (_thread is not null) {
            return;
        }
        _detector.Answered(Now);
        _thread = new Thread(Run) { IsBackground = true, Name = "Hang watchdog" };
        _thread.Start();
    }

    public void Dispose() {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _stop.Dispose();
    }

    private void Run() {
        while (!_stop.Wait(Interval)) {
            if (Interlocked.CompareExchange(ref _waiting, 1, 0) == 0) {
                try {
                    _window.BeginInvoke(_answer);
                } catch (InvalidOperationException) {
                    return;
                }
            }
            switch (_detector.Check(Now)) {
                case { Hung: true } change:
                    _log.Warning(Source, $"The window has not responded for {Seconds(change.Duration)} s.");
                    break;
                case { Hung: false } change:
                    _log.Warning(Source, $"The window responds again after {Seconds(change.Duration)} s.");
                    break;
            }
        }
    }

    private static string Seconds(TimeSpan duration) => duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture);
}
