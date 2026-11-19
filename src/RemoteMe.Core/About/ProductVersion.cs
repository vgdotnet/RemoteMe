namespace RemoteMe.Core.About;

// The version the About window shows: the release version and the first seven characters of the commit
// the exe is built from, such as "0.6.0 (57730fa)".
public static class ProductVersion {
    public static string Format(string? version, string? revision) {
        var number = version?.Split('+')[0].Trim();
        if (string.IsNullOrEmpty(number)) {
            number = "unknown";
        }
        var commit = revision?.Trim();
        return string.IsNullOrEmpty(commit) ? number : $"{number} ({commit[..Math.Min(7, commit.Length)]})";
    }
}
