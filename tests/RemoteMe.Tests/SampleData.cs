using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.Tests;

internal static class SampleData {
    public static readonly Guid NetworkConnectionId = Guid.Parse("6f1c2b8e-4a1d-4c3e-9b7a-2d5e8f0a1b3c");
    public static readonly Guid HyperVConnectionId = Guid.Parse("0b9e7d6c-5a4f-4e3d-8c2b-1a0f9e8d7c6b");
    public static readonly Guid VmId = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");

    public static Settings Settings(string password = "p@ss \"word\" \\ ünïcode ✓") => new() {
        Groups = ["Lab"],
        Connections = [
            new Connection {
                Id = NetworkConnectionId,
                Name = "Build server",
                Group = "Lab",
                Kind = ConnectionKind.Network,
                Address = "build.example.test",
                UserName = "test-user",
                Password = ProtectedSecret.Create(password),
                Redirection = new RedirectionSettings { Drives = true, Microphone = false },
            },
            new Connection {
                Id = HyperVConnectionId,
                Name = "Test VM",
                Kind = ConnectionKind.HyperV,
                Address = "localhost",
                VmId = VmId,
            },
        ],
    };

    public static Connection Connection(string name, string? group = null) => new() {
        Id = Guid.NewGuid(),
        Name = name,
        Group = group,
        Address = name.ToLowerInvariant() + ".example.test",
    };

    public static void AssertSame(Settings expected, Settings actual) {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.Groups, actual.Groups);
        Assert.Equal(expected.Connections.Count, actual.Connections.Count);
        foreach (var (expectedConnection, actualConnection) in expected.Connections.Zip(actual.Connections)) {
            Assert.Equal(expectedConnection with { Password = null }, actualConnection with { Password = null });
            Assert.Equal(expectedConnection.Password?.Reveal(), actualConnection.Password?.Reveal());
        }
    }
}
