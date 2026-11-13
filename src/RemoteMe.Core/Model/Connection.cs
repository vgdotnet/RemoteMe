using RemoteMe.Core.Security;

namespace RemoteMe.Core.Model;

public sealed record Connection {
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Group { get; init; }

    public ConnectionKind Kind { get; init; }

    // The computer to connect to, or the Hyper-V host for a VM.
    public required string Address { get; init; }

    public Guid? VmId { get; init; }

    public string UserName { get; init; } = "";

    public ProtectedSecret? Password { get; init; }

    public RedirectionSettings Redirection { get; init; } = new();
}
