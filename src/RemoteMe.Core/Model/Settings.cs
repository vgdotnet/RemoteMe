namespace RemoteMe.Core.Model;

public sealed record Settings {
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    // Display order of the groups. Within a group, connections follow their order in Connections.
    public IReadOnlyList<string> Groups { get; init; } = [];

    // The groups shown collapsed in the connection list, kept between runs (decided on 2026-11-19). The
    // JSON reader gives null, not the initial value, for a file written before it existed.
    public IReadOnlyList<string> CollapsedGroups {
        get;
        init => field = value ?? [];
    } = [];

    public IReadOnlyList<Connection> Connections { get; init; } = [];

    // The time zone of the clock above the list buttons, as a TimeZoneInfo id; null is the local time of
    // Windows. Left out of the file while null, so files without it open with the local time.
    public string? ClockTimeZone { get; init; }

    // The color of the doodle background of the left panel, one of ListBackground.Names; null, or a name
    // this version does not know, is the first one. Left out of the file while null.
    public string? Background { get; init; }
}
