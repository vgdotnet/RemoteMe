namespace RemoteMe.Core.Model;

public sealed record RedirectionSettings {
    public bool Clipboard { get; init; } = true;

    public bool Audio { get; init; } = true;

    public bool Microphone { get; init; } = true;

    public bool Drives { get; init; }

    public bool Printers { get; init; }
}
