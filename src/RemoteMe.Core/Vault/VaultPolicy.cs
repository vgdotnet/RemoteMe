namespace RemoteMe.Core.Vault;

internal sealed record VaultPolicy(uint Iterations, uint MinIterations, uint MaxIterations) {
    // 600,000 is the OWASP recommendation for PBKDF2-HMAC-SHA256; the upper bound stops a crafted file
    // from hanging the application.
    public static VaultPolicy Default { get; } = new(600_000, 600_000, 10_000_000);
}
