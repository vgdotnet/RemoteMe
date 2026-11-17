using System.Runtime.InteropServices;
using RemoteMe.Core.Logging;
using RemoteMe.Core.Model;

namespace RemoteMe.Core.Sessions;

public enum SessionState {
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed,
}

// One connection's session. A dropped connection is reconnected only by the component itself; when it
// gives up, the session shows the reason and waits for the user (decided on 2026-11-13).
public sealed class Session : IDisposable {
    private readonly AppLog _log;
    private bool _disconnectRequested;

    internal Session(Connection connection, IRdpClient client, AppLog log) {
        Connection = connection;
        Client = client;
        _log = log;
        Client.LoginComplete += OnLoginComplete;
        Client.AutoReconnecting += OnAutoReconnecting;
        Client.AutoReconnected += OnAutoReconnected;
        Client.Disconnected += OnDisconnected;
        Client.LogonNotice += OnLogonNotice;
        Client.Warning += OnWarning;
    }

    public event EventHandler? Changed;

    public Connection Connection { get; private set; }

    public IRdpClient Client { get; }

    public SessionState State { get; private set; }

    public string Message { get; private set; } = "";

    public bool IsActive => State is SessionState.Connecting or SessionState.Connected or SessionState.Reconnecting;

    // Ended by the user's own Disconnect (or a cancelled connect), not by the remote side or an error.
    public bool DisconnectedByUser { get; private set; }

    public void Connect(Connection connection) {
        if (IsActive) {
            return;
        }
        Connection = connection;
        _disconnectRequested = false;
        DisconnectedByUser = false;
        var target = DescribeTarget(connection);
        SetState(SessionState.Connecting, $"Connecting to {target}...");
        _log.Information(connection.Name, $"Connecting to {target}.");
        try {
            Client.Connect(RdpConnectRequest.For(connection));
        } catch (Exception ex) when (ex is ExternalException or InvalidOperationException or ArgumentException) {
            SetState(SessionState.Failed, $"The connection could not be started. {ex.Message}");
            _log.Error(connection.Name, $"The connection could not be started: {ex.Message}");
        }
    }

    public void Disconnect() {
        if (!IsActive) {
            return;
        }
        _disconnectRequested = true;
        _log.Information(Connection.Name, "Disconnecting.");
        Client.Disconnect();
    }

    public void Dispose() {
        Client.LoginComplete -= OnLoginComplete;
        Client.AutoReconnecting -= OnAutoReconnecting;
        Client.AutoReconnected -= OnAutoReconnected;
        Client.Disconnected -= OnDisconnected;
        Client.LogonNotice -= OnLogonNotice;
        Client.Warning -= OnWarning;
        Client.Dispose();
    }

    private void OnLoginComplete(object? sender, EventArgs e) {
        SetState(SessionState.Connected, "");
        _log.Information(Connection.Name, "Signed in.");
    }

    private void OnAutoReconnecting(object? sender, EventArgs e) {
        if (State == SessionState.Reconnecting) {
            return;
        }
        SetState(SessionState.Reconnecting, "The connection was interrupted. Reconnecting...");
        _log.Warning(Connection.Name, "The connection was interrupted; the RDP component is reconnecting.");
    }

    private void OnAutoReconnected(object? sender, EventArgs e) {
        _log.Information(Connection.Name, "The connection is restored.");
    }

    private void OnDisconnected(object? sender, RdpDisconnectedEventArgs e) {
        var codes = $"reason 0x{e.Reason:X}, extended {e.ExtendedReason}";
        if (_disconnectRequested) {
            _disconnectRequested = false;
            DisconnectedByUser = true;
            SetState(SessionState.Disconnected, "Disconnected.");
            _log.Information(Connection.Name, $"Disconnected ({codes}).");
            return;
        }
        var info = DisconnectReasons.Describe(e.Reason, e.ExtendedReason, e.ComponentText);
        SetState(info.IsError ? SessionState.Failed : SessionState.Disconnected, info.Message);
        _log.Write(info.IsError ? LogLevel.Error : LogLevel.Information, Connection.Name, $"{info.Message} ({codes})");
    }

    private void OnLogonNotice(object? sender, RdpLogonNoticeEventArgs e) {
        _log.Information(Connection.Name, $"Logon notice {e.Code}.");
    }

    private void OnWarning(object? sender, string message) {
        _log.Warning(Connection.Name, message);
    }

    private static string DescribeTarget(Connection connection) => connection.Kind switch {
        ConnectionKind.HyperV when connection.BasicSession => $"the console of the VM on {connection.Address}",
        ConnectionKind.HyperV => $"the VM on {connection.Address}",
        _ => connection.Address,
    };

    private void SetState(SessionState state, string message) {
        State = state;
        Message = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
