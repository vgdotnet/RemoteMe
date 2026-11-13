namespace RemoteMe.Core.Model;

// Read before the full settings, so a file from a newer version is reported as such rather than as
// unreadable content.
internal sealed record SchemaProbe {
    public int SchemaVersion { get; init; }
}
