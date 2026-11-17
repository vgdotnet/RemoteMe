using RemoteMe.Core.Model;
using RemoteMe.Core.Sessions;

namespace RemoteMe.Tests.Sessions;

public sealed class SessionCycleTests {
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid D = Guid.NewGuid();
    private static readonly IReadOnlyList<Guid> Order = [A, B, C, D];

    [Fact]
    public void Next_SkipsConnectionsWithoutASession() {
        Assert.Equal(D, SessionCycle.Next(Order, A, id => id == A || id == D, forward: true));
    }

    [Fact]
    public void Next_WrapsAround() {
        Assert.Equal(A, SessionCycle.Next(Order, D, id => id == A || id == D, forward: true));
        Assert.Equal(D, SessionCycle.Next(Order, A, id => id == A || id == D, forward: false));
    }

    [Fact]
    public void Next_FromAConnectionWithoutASession_StartsFromItsPlace() {
        Assert.Equal(C, SessionCycle.Next(Order, B, id => id == C, forward: true));
        Assert.Equal(A, SessionCycle.Next(Order, B, id => id == A || id == C, forward: false));
    }

    [Fact]
    public void Next_WithoutACurrentConnection_StartsAtTheEdge() {
        Assert.Equal(B, SessionCycle.Next(Order, null, id => id == B || id == C, forward: true));
        Assert.Equal(C, SessionCycle.Next(Order, null, id => id == B || id == C, forward: false));
    }

    [Fact]
    public void Next_WithOnlyTheCurrentSession_ReturnsNull() {
        Assert.Null(SessionCycle.Next(Order, B, id => id == B, forward: true));
    }

    [Fact]
    public void ConnectionOrder_FollowsTheListIncludingCollapsedGroups() {
        var alpha = SampleData.Connection("Alpha");
        var beta = SampleData.Connection("Beta", "Lab");
        var gamma = SampleData.Connection("Gamma", "Office");
        var settings = SettingsEditor.Normalize(new Settings { Groups = ["Office", "Lab"], Connections = [beta, gamma, alpha] });

        Assert.Equal([alpha.Id, gamma.Id, beta.Id], ConnectionListLayout.ConnectionOrder(settings));
    }
}
