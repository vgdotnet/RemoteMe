namespace RemoteMe.Core.Logging;

// A change the detector found: the window stopped answering (Hung, with how long it has been silent) or
// answers again (not Hung, with how long it was silent in all).
public readonly record struct HangChange(bool Hung, TimeSpan Duration);

// Tells when the window stops answering and when it answers again. A watchdog thread calls Check about
// once a second and sends the window a message each time; the window's thread calls Answered when it
// gets to that message. Times come from one monotonic clock. When Check itself comes later than the
// threshold, the whole process or the computer stood still (sleep, for one), so that silence is not
// counted against the window.
public sealed class HangDetector {
    private readonly TimeSpan _threshold;
    private long _answered;
    private TimeSpan? _lastCheck;
    private TimeSpan? _hungSince;

    public HangDetector(TimeSpan threshold, TimeSpan now) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(threshold, TimeSpan.Zero);
        _threshold = threshold;
        _answered = now.Ticks;
    }

    public void Answered(TimeSpan now) => Interlocked.Exchange(ref _answered, now.Ticks);

    public HangChange? Check(TimeSpan now) {
        var answered = TimeSpan.FromTicks(Interlocked.Read(ref _answered));
        var paused = _lastCheck is { } last && now - last >= _threshold;
        _lastCheck = now;
        if (_hungSince is { } since) {
            if (now - answered < _threshold) {
                _hungSince = null;
                return new HangChange(false, answered - since);
            }
            return null;
        }
        if (paused) {
            Answered(now);
            return null;
        }
        if (now - answered >= _threshold) {
            _hungSince = answered;
            return new HangChange(true, now - answered);
        }
        return null;
    }
}
