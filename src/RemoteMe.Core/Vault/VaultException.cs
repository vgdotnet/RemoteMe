namespace RemoteMe.Core.Vault;

public sealed class VaultException : Exception {
    public VaultException(VaultError error, Exception? innerException = null)
        : base(Describe(error), innerException) {
        Error = error;
    }

    public VaultError Error { get; }

    private static string Describe(VaultError error) => error switch {
        VaultError.InvalidFormat => "The file is not a RemoteMe settings file, or it is cut short.",
        VaultError.UnsupportedVersion => "The settings file was written by a newer version of RemoteMe.",
        VaultError.ParametersOutOfRange => "The settings file has key derivation parameters outside the accepted range.",
        VaultError.WrongPassword => "The master password is wrong.",
        VaultError.Damaged => "The settings file is damaged.",
        VaultError.InvalidContent => "The settings in the file cannot be read.",
        VaultError.UnsupportedSchema => "The settings were saved by a newer version of RemoteMe.",
        VaultError.Conflict => "The settings file was changed or removed outside RemoteMe after it was opened.",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}
