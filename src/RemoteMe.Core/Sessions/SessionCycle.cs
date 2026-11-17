namespace RemoteMe.Core.Sessions;

public static class SessionCycle {
    // The connection with a session that comes after (or before) the current one in list order,
    // wrapping around. Null when there is no other session.
    public static Guid? Next(IReadOnlyList<Guid> order, Guid? current, Func<Guid, bool> hasSession, bool forward) {
        var count = order.Count;
        if (count == 0) {
            return null;
        }
        var start = -1;
        for (var i = 0; i < count; i++) {
            if (order[i] == current) {
                start = i;
                break;
            }
        }
        for (var step = 1; step <= count; step++) {
            var index = forward
                ? Modulo(start + step, count)
                : Modulo((start < 0 ? count : start) - step, count);
            var candidate = order[index];
            if (candidate != current && hasSession(candidate)) {
                return candidate;
            }
        }
        return null;
    }

    private static int Modulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;
}
