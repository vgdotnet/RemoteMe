using RemoteMe.Core.Sessions;

namespace RemoteMe.Tests.Sessions;

internal sealed class FakeRdpClient : IRdpClient {
    public event EventHandler? LoginComplete;

    public event EventHandler? AutoReconnecting;

    public event EventHandler? AutoReconnected;

    public event EventHandler<RdpDisconnectedEventArgs>? Disconnected;

    public event EventHandler<RdpLogonNoticeEventArgs>? LogonNotice;

    public event EventHandler<string>? Warning;

    public List<RdpConnectRequest> ConnectRequests { get; } = [];

    public int DisconnectCalls { get; private set; }

    public bool IsDisposed { get; private set; }

    public Exception? ConnectFailure { get; set; }

    public void Connect(RdpConnectRequest request) {
        if (ConnectFailure is not null) {
            throw ConnectFailure;
        }
        ConnectRequests.Add(request);
    }

    public void Disconnect() {
        DisconnectCalls++;
    }

    public void Dispose() {
        IsDisposed = true;
    }

    public void RaiseLoginComplete() => LoginComplete?.Invoke(this, EventArgs.Empty);

    public void RaiseAutoReconnecting() => AutoReconnecting?.Invoke(this, EventArgs.Empty);

    public void RaiseAutoReconnected() => AutoReconnected?.Invoke(this, EventArgs.Empty);

    public void RaiseDisconnected(int reason, int extendedReason = 0, string text = "") =>
        Disconnected?.Invoke(this, new RdpDisconnectedEventArgs(reason, extendedReason, text));

    public void RaiseLogonNotice(int code) => LogonNotice?.Invoke(this, new RdpLogonNoticeEventArgs(code));

    public void RaiseWarning(string message) => Warning?.Invoke(this, message);
}
