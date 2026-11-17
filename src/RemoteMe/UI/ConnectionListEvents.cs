using RemoteMe.Core.Model;

namespace RemoteMe.UI;

internal enum ConnectionStatus {
    NotConnected,
    Busy,
    Connected,
    Error,
}

internal sealed class ConnectionEventArgs(Connection connection) : EventArgs {
    public Connection Connection { get; } = connection;
}

internal sealed class ConnectionMenuEventArgs(Connection connection, Point screenLocation) : EventArgs {
    public Connection Connection { get; } = connection;

    public Point ScreenLocation { get; } = screenLocation;
}

internal sealed class ConnectionMoveEventArgs(Guid connectionId, ConnectionDrop drop) : EventArgs {
    public Guid ConnectionId { get; } = connectionId;

    public ConnectionDrop Drop { get; } = drop;
}

internal sealed class GroupMoveEventArgs(string group, string? beforeGroup) : EventArgs {
    public string Group { get; } = group;

    public string? BeforeGroup { get; } = beforeGroup;
}
