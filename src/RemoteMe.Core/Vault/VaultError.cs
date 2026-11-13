namespace RemoteMe.Core.Vault;

public enum VaultError {
    InvalidFormat,
    UnsupportedVersion,
    ParametersOutOfRange,
    WrongPassword,
    Damaged,
    InvalidContent,
    UnsupportedSchema,
    Conflict,
}
