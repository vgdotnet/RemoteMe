namespace RemoteMe.Core.Model;

public sealed record Settings {
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    // Display order of the groups. Within a group, connections follow their order in Connections.
    public IReadOnlyList<string> Groups { get; init; } = [];

    public IReadOnlyList<Connection> Connections { get; init; } = [];
}
