using RemoteMe.Core.Sessions;

namespace RemoteMe.Tests.Sessions;

public sealed class DisconnectReasonsTests {
    [Theory]
    [InlineData(0x1, 0, "Disconnected.", false)]
    [InlineData(0x3, 26, "The remote computer is restarting.", false)]
    [InlineData(0x904, 7, "The remote computer refused the connection.", true)]
    [InlineData(0x904, 0, "The connection was lost.", true)]
    [InlineData(0x204, 0, "The remote computer could not be reached. Check that it is on and accepts remote connections.", true)]
    [InlineData(0x2, 12, "You signed out of the remote session.", false)]
    public void Describe_KnownReasons(int reason, int extended, string message, bool isError) {
        Assert.Equal(new DisconnectInfo(message, isError), DisconnectReasons.Describe(reason, extended, "ignored"));
    }

    [Fact]
    public void Describe_LicensingReasons_ReportALicensingProblem() {
        Assert.Equal("The remote computer has a licensing problem.", DisconnectReasons.Describe(0x3, 258, "").Message);
    }

    [Fact]
    public void Describe_UnknownReason_UsesTheComponentTextAndTheCode() {
        var info = DisconnectReasons.Describe(0x1234, 0, " Something odd happened. ");

        Assert.Equal("Something odd happened. (code 0x1234)", info.Message);
        Assert.True(info.IsError);
    }

    [Fact]
    public void Describe_UnknownReasonWithoutText_SaysTheConnectionEnded() {
        Assert.Equal("The connection ended. (code 0x1234)", DisconnectReasons.Describe(0x1234, 0, "").Message);
    }
}
