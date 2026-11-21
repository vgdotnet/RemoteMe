namespace RemoteMe.Core.Vault;

// Saving, updating or removing the master password saved on this computer failed. Whatever the
// operation was part of (opening, changing the master password, reading the file again) has succeeded.
public sealed class RememberedPasswordException : Exception {
    public RememberedPasswordException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
