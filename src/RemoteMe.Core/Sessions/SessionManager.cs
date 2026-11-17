using RemoteMe.Core.Logging;
using RemoteMe.Core.Model;

namespace RemoteMe.Core.Sessions;

// Keeps one session per connection. The client factory belongs to the UI, which places each new
// RDP control on screen before the session connects.
public sealed class SessionManager : IDisposable {
    private readonly Dictionary<Guid, Session> _sessions = [];
    private readonly Func<IRdpClient> _createClient;
    private readonly AppLog _log;

    public SessionManager(Func<IRdpClient> createClient, AppLog log) {
        _createClient = createClient;
        _log = log;
    }

    // Raised with the connection id when a session is created, changes state or is closed.
    public event EventHandler<Guid>? SessionChanged;

    public IReadOnlyCollection<Session> Sessions => _sessions.Values;

    public Session? Find(Guid connectionId) => _sessions.GetValueOrDefault(connectionId);

    public Session GetOrCreate(Connection connection) {
        if (_sessions.TryGetValue(connection.Id, out var existing)) {
            return existing;
        }
        var session = new Session(connection, _createClient(), _log);
        session.Changed += (_, _) => SessionChanged?.Invoke(this, connection.Id);
        _sessions.Add(connection.Id, session);
        SessionChanged?.Invoke(this, connection.Id);
        return session;
    }

    public void Close(Guid connectionId) {
        if (!_sessions.Remove(connectionId, out var session)) {
            return;
        }
        session.Disconnect();
        session.Dispose();
        SessionChanged?.Invoke(this, connectionId);
    }

    // Closes the sessions of connections that no longer exist, for example after the file was read again.
    public void CloseMissing(IEnumerable<Guid> existingConnectionIds) {
        var existing = existingConnectionIds.ToHashSet();
        foreach (var id in _sessions.Keys.Where(id => !existing.Contains(id)).ToList()) {
            Close(id);
        }
    }

    public void Dispose() {
        foreach (var id in _sessions.Keys.ToList()) {
            Close(id);
        }
    }
}
