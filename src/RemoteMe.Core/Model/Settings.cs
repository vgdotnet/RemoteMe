namespace RemoteMe.Core.Model;

public sealed record Settings {
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<Connection> Connections { get; init; } = [];
}
