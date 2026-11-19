using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class HyperVHostTests {
    [Theory]
    [InlineData("localhost")]
    [InlineData("LocalHost")]
    [InlineData(" localhost ")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("::1")]
    public void IsThisComputer_AcceptsLocalhostAndLoopback(string host) {
        Assert.True(HyperVHost.IsThisComputer(host));
    }

    [Fact]
    public void IsThisComputer_AcceptsTheComputerName() {
        Assert.True(HyperVHost.IsThisComputer(Environment.MachineName));
        Assert.True(HyperVHost.IsThisComputer(Environment.MachineName.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("hyperv.example.test")]
    [InlineData("203.0.113.5")]
    [InlineData("2001:db8::5")]
    [InlineData("")]
    public void IsThisComputer_RefusesOtherComputers(string host) {
        Assert.False(HyperVHost.IsThisComputer(host));
    }
}
