using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.Core.Sessions;

// The RDP component as the sessions see it. The implementation lives with the UI, since it is an
// ActiveX control; events are raised on the UI thread.
public interface IRdpClient : IDisposable {
    event EventHandler? LoginComplete;

    event EventHandler? AutoReconnecting;

    event EventHandler? AutoReconnected;

    event EventHandler<RdpDisconnectedEventArgs>? Disconnected;

    event EventHandler<RdpLogonNoticeEventArgs>? LogonNotice;

    // Problems that do not end the session, for the log.
    event EventHandler<string>? Warning;

    void Connect(RdpConnectRequest request);

    void Disconnect();
}

// The password stays protected until the client hands it to the component.
public sealed record RdpConnectRequest(string Server, string UserName, ProtectedSecret? Password, RedirectionSettings Redirection) {
    // Set for a VM reached through its Hyper-V host.
    public HyperVTarget? HyperV { get; init; }

    // A Hyper-V host is signed in to with the current Windows account (only the local host is needed,
    // decided on 2026-11-17), so a VM's request carries no user name or password.
    public static RdpConnectRequest For(Connection connection) {
        if (connection.Kind != ConnectionKind.HyperV) {
            return new(connection.Address, connection.UserName, connection.Password, connection.Redirection);
        }
        if (connection.VmId is not { } vmId) {
            throw new InvalidOperationException("The VM ID is missing.");
        }
        if (!HyperVHost.IsThisComputer(connection.Address)) {
            throw new InvalidOperationException($"{connection.Address} is not this computer. {HyperVHost.Requirement}");
        }
        return new(connection.Address, "", null, connection.Redirection) {
            HyperV = new HyperVTarget(vmId, EnhancedSession: !connection.BasicSession),
        };
    }
}

public sealed record HyperVTarget(Guid VmId, bool EnhancedSession);

public sealed class RdpDisconnectedEventArgs(int reason, int extendedReason, string componentText) : EventArgs {
    public int Reason { get; } = reason;

    public int ExtendedReason { get; } = extendedReason;

    public string ComponentText { get; } = componentText;
}

public sealed class RdpLogonNoticeEventArgs(int code) : EventArgs {
    public int Code { get; } = code;
}
