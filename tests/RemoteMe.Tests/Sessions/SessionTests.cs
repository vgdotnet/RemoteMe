using System.Runtime.InteropServices;
using RemoteMe.Core.Logging;
using RemoteMe.Core.Model;
using RemoteMe.Core.Security;
using RemoteMe.Core.Sessions;

namespace RemoteMe.Tests.Sessions;

public sealed class SessionTests {
    private readonly AppLog _log = new();
    private readonly FakeRdpClient _client = new();
    private readonly SessionManager _manager;
    private readonly Connection _connection = SampleData.Connection("Build server") with {
        UserName = "test-user",
        Password = ProtectedSecret.Create("not-a-real-password"),
    };

    public SessionTests() {
        _manager = new SessionManager(() => _client, _log);
    }

    [Fact]
    public void Connect_SendsTheConnectionToTheClient() {
        var session = _manager.GetOrCreate(_connection);

        session.Connect(_connection);

        var request = Assert.Single(_client.ConnectRequests);
        Assert.Equal(_connection.Address, request.Server);
        Assert.Equal("test-user", request.UserName);
        Assert.Equal("not-a-real-password", request.Password?.Reveal());
        Assert.Equal(_connection.Redirection, request.Redirection);
        Assert.Null(request.HyperV);
        Assert.Equal(SessionState.Connecting, session.State);
    }

    [Fact]
    public void LoginComplete_MakesTheSessionConnected() {
        var session = Connected();

        Assert.Equal(SessionState.Connected, session.State);
        Assert.Equal("", session.Message);
    }

    [Fact]
    public void Connect_WhileActive_DoesNothing() {
        var session = Connected();

        session.Connect(_connection);

        Assert.Single(_client.ConnectRequests);
    }

    [Fact]
    public void Disconnect_ByTheUser_EndsDisconnectedWithoutAnError() {
        var session = Connected();

        session.Disconnect();
        _client.RaiseDisconnected(0x1, 0, "An internal error has occurred.");

        Assert.Equal(1, _client.DisconnectCalls);
        Assert.Equal(SessionState.Disconnected, session.State);
        Assert.Equal("Disconnected.", session.Message);
        Assert.True(session.DisconnectedByUser);
    }

    [Fact]
    public void Connect_AfterTheUsersDisconnect_ForgetsIt() {
        var session = Connected();
        session.Disconnect();
        _client.RaiseDisconnected(0x1);

        session.Connect(_connection);

        Assert.False(session.DisconnectedByUser);
        Assert.Equal(SessionState.Connecting, session.State);
    }

    [Fact]
    public void Disconnect_ByTheRemoteComputer_ShowsTheReason() {
        var session = Connected();

        _client.RaiseDisconnected(0x3, 26);

        Assert.Equal(SessionState.Disconnected, session.State);
        Assert.Equal("The remote computer is restarting.", session.Message);
        Assert.False(session.DisconnectedByUser);
    }

    [Fact]
    public void Disconnect_WithAnError_Fails() {
        var session = _manager.GetOrCreate(_connection);
        session.Connect(_connection);

        _client.RaiseDisconnected(0x807);

        Assert.Equal(SessionState.Failed, session.State);
        Assert.Equal("The user name or password is not accepted.", session.Message);
        Assert.Contains(_log.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("0x807", StringComparison.Ordinal));
    }

    [Fact]
    public void AutoReconnect_GoesThroughReconnectingBackToConnected() {
        var session = Connected();

        _client.RaiseAutoReconnecting();
        Assert.Equal(SessionState.Reconnecting, session.State);
        _client.RaiseAutoReconnected();
        _client.RaiseLoginComplete();

        Assert.Equal(SessionState.Connected, session.State);
    }

    [Fact]
    public void Connect_AfterAFailure_ConnectsAgain() {
        var session = Connected();
        _client.RaiseDisconnected(0x904);

        session.Connect(_connection);

        Assert.Equal(2, _client.ConnectRequests.Count);
        Assert.Equal(SessionState.Connecting, session.State);
    }

    [Fact]
    public void Connect_WhenTheClientThrows_Fails() {
        _client.ConnectFailure = new COMException("Bad server name.");
        var session = _manager.GetOrCreate(_connection);

        session.Connect(_connection);

        Assert.Equal(SessionState.Failed, session.State);
        Assert.Contains("Bad server name.", session.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Connect_ToAHyperVMachine_SignsInToTheHostWithTheWindowsAccount() {
        var vm = _connection with { Kind = ConnectionKind.HyperV, Address = "localhost", VmId = SampleData.VmId };
        var session = _manager.GetOrCreate(vm);

        session.Connect(vm);

        var request = Assert.Single(_client.ConnectRequests);
        Assert.Equal("localhost", request.Server);
        Assert.Equal("", request.UserName);
        Assert.Null(request.Password);
        Assert.Equal(new HyperVTarget(SampleData.VmId, EnhancedSession: true), request.HyperV);
        Assert.Equal(SessionState.Connecting, session.State);
        Assert.Equal("Connecting to the VM on localhost...", session.Message);
    }

    [Fact]
    public void Connect_ToAHyperVMachineConsole_AsksForABasicSession() {
        var vm = _connection with { Kind = ConnectionKind.HyperV, VmId = SampleData.VmId, BasicSession = true };
        var session = _manager.GetOrCreate(vm);

        session.Connect(vm);

        var request = Assert.Single(_client.ConnectRequests);
        Assert.Equal(new HyperVTarget(SampleData.VmId, EnhancedSession: false), request.HyperV);
    }

    [Fact]
    public void Connect_ToAHyperVMachineWithoutAnId_Fails() {
        var vm = _connection with { Kind = ConnectionKind.HyperV, VmId = null };
        var session = _manager.GetOrCreate(vm);

        session.Connect(vm);

        Assert.Empty(_client.ConnectRequests);
        Assert.Equal(SessionState.Failed, session.State);
        Assert.Contains("The VM ID is missing.", session.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Warning_FromTheClient_GoesToTheLog() {
        var session = Connected();

        _client.RaiseWarning("The display could not be updated.");

        Assert.Equal(SessionState.Connected, session.State);
        Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning && e.Message == "The display could not be updated.");
    }

    [Fact]
    public void Log_NeverContainsThePassword() {
        var session = Connected();
        _client.RaiseLogonNotice(-2);
        _client.RaiseDisconnected(0x807);

        Assert.DoesNotContain(_log.Entries, e => e.Message.Contains("not-a-real-password", StringComparison.Ordinal));
    }

    [Fact]
    public void Manager_KeepsOneSessionPerConnectionAndReportsChanges() {
        var changes = new List<Guid>();
        _manager.SessionChanged += (_, id) => changes.Add(id);

        var first = _manager.GetOrCreate(_connection);
        var second = _manager.GetOrCreate(_connection);
        first.Connect(_connection);

        Assert.Same(first, second);
        Assert.Equal([_connection.Id, _connection.Id], changes);
    }

    [Fact]
    public void Manager_Close_DisconnectsAndDisposesTheClient() {
        Connected();

        _manager.Close(_connection.Id);

        Assert.Null(_manager.Find(_connection.Id));
        Assert.Equal(1, _client.DisconnectCalls);
        Assert.True(_client.IsDisposed);
    }

    [Fact]
    public void Manager_CloseMissing_ClosesSessionsOfRemovedConnections() {
        Connected();

        _manager.CloseMissing([Guid.NewGuid()]);

        Assert.Empty(_manager.Sessions);
    }

    private Session Connected() {
        var session = _manager.GetOrCreate(_connection);
        session.Connect(_connection);
        _client.RaiseLoginComplete();
        return session;
    }
}
