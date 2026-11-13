namespace RemoteMe.Core.Vault;

// There is no hard minimum: a weak master password is allowed, the UI only warns about it.
public static class MasterPassword {
    public const int RecommendedLength = 12;

    public static bool IsWeak(string password) => password.Length < RecommendedLength;
}
