namespace RemoteMe.Core.Vault;

internal static class AtomicFile {
    public static void Write(string path, ReadOnlySpan<byte> content) {
        var temporaryPath = path + ".tmp";
        try {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        } catch {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
