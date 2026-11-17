using System.Text.Json.Serialization;
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

    // Hyper-V only: the VM's console instead of an enhanced session. Left out of the file when off, so
    // files without it open as enhanced sessions.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BasicSession { get; init; }

    // Not used for a Hyper-V host, which is signed in to with the current Windows account.
    public string UserName { get; init; } = "";

    public ProtectedSecret? Password { get; init; }

    public RedirectionSettings Redirection { get; init; } = new();
}
