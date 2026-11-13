using System.Security.Cryptography;
using System.Text.Json;
using RemoteMe.Core.Model;
using RemoteMe.Core.Security;

namespace RemoteMe.Core.Vault;

public sealed class SettingsVault : IDisposable {
    private const int MaxFileSize = 16 * 1024 * 1024;

    private readonly VaultPolicy _policy;
    private VaultKey _key;
    private uint _iterations;
    private byte[] _salt;
    private byte[] _fileHash;
    private bool _disposed;

    private SettingsVault(string path, VaultPolicy policy, VaultKey key, uint iterations, byte[] salt, ulong revision, byte[] fileHash) {
        Path = path;
        _policy = policy;
        _key = key;
        _iterations = iterations;
        _salt = salt;
        Revision = revision;
        _fileHash = fileHash;
    }

    public string Path { get; }

    public ulong Revision { get; private set; }

    public static SettingsVault Create(string path, string password, Settings settings) =>
        Create(path, password, settings, VaultPolicy.Default);

    public static SettingsVault Open(string path, string password, out Settings settings) =>
        Open(path, password, VaultPolicy.Default, out settings);

    internal static SettingsVault Create(string path, string password, Settings settings, VaultPolicy policy) {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (File.Exists(path)) {
            throw new IOException($"The settings file already exists: {path}");
        }
        var salt = RandomNumberGenerator.GetBytes(VaultHeader.SaltSize);
        var key = VaultKey.Derive(password, policy.Iterations, salt);
        try {
            var fileHash = WriteFile(path, key, policy.Iterations, salt, 1, settings);
            return new SettingsVault(path, policy, key, policy.Iterations, salt, 1, fileHash);
        } catch {
            key.Dispose();
            throw;
        }
    }

    internal static SettingsVault Open(string path, string password, VaultPolicy policy, out Settings settings) {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var file = ReadFile(path);
        var header = VaultHeader.Read(file);
        if (header.Iterations < policy.MinIterations || header.Iterations > policy.MaxIterations) {
            throw new VaultException(VaultError.ParametersOutOfRange);
        }
        var key = VaultKey.Derive(password, header.Iterations, header.Salt);
        try {
            if (!key.Matches(header.KeyCheck)) {
                throw new VaultException(VaultError.WrongPassword);
            }
            settings = Decrypt(file, header, key);
            return new SettingsVault(path, policy, key, header.Iterations, header.Salt, header.Revision, SHA256.HashData(file));
        } catch {
            key.Dispose();
            throw;
        }
    }

    public void Save(Settings settings) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFileUnchanged();
        _fileHash = WriteFile(Path, _key, _iterations, _salt, Revision + 1, settings);
        Revision++;
    }

    public void ChangePassword(string newPassword, Settings settings) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);
        EnsureFileUnchanged();
        var iterations = Math.Max(_iterations, _policy.Iterations);
        var salt = RandomNumberGenerator.GetBytes(VaultHeader.SaltSize);
        var key = VaultKey.Derive(newPassword, iterations, salt);
        try {
            _fileHash = WriteFile(Path, key, iterations, salt, Revision + 1, settings);
        } catch {
            key.Dispose();
            throw;
        }
        _key.Dispose();
        _key = key;
        _iterations = iterations;
        _salt = salt;
        Revision++;
    }

    public bool IsCurrentPassword(string password) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var key = VaultKey.Derive(password, _iterations, _salt);
        return key.Matches(_key.KeyCheck);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _key.Dispose();
        _disposed = true;
    }

    private void EnsureFileUnchanged() {
        var current = new FileInfo(Path);
        if (!current.Exists || current.Length > MaxFileSize || !SHA256.HashData(File.ReadAllBytes(Path)).AsSpan().SequenceEqual(_fileHash)) {
            throw new VaultException(VaultError.Conflict);
        }
    }

    private static byte[] ReadFile(string path) {
        if (new FileInfo(path).Length > MaxFileSize) {
            throw new VaultException(VaultError.InvalidFormat);
        }
        return File.ReadAllBytes(path);
    }

    private static byte[] WriteFile(string path, VaultKey key, uint iterations, byte[] salt, ulong revision, Settings settings) {
        using var json = new SensitiveBufferWriter();
        using (var writer = new Utf8JsonWriter(json)) {
            JsonSerializer.Serialize(writer, settings, ModelJsonContext.Default.Settings);
        }
        var plaintext = json.WrittenSpan;
        var header = new VaultHeader {
            Iterations = iterations,
            Salt = salt,
            KeyCheck = key.KeyCheck,
            Revision = revision,
            Nonce = RandomNumberGenerator.GetBytes(VaultHeader.NonceSize),
        };
        var file = new byte[VaultHeader.Size + plaintext.Length + VaultHeader.TagSize];
        header.WriteTo(file);
        key.Encrypt(
            header.Nonce,
            plaintext,
            file.AsSpan(VaultHeader.Size, plaintext.Length),
            file.AsSpan(VaultHeader.Size + plaintext.Length),
            file.AsSpan(0, VaultHeader.Size));
        AtomicFile.Write(path, file);
        return SHA256.HashData(file);
    }

    private static Settings Decrypt(byte[] file, VaultHeader header, VaultKey key) {
        var length = file.Length - VaultHeader.Size - VaultHeader.TagSize;
        var plaintext = GC.AllocateArray<byte>(length, pinned: true);
        try {
            try {
                key.Decrypt(
                    header.Nonce,
                    file.AsSpan(VaultHeader.Size, length),
                    file.AsSpan(VaultHeader.Size + length),
                    plaintext,
                    file.AsSpan(0, VaultHeader.Size));
            } catch (AuthenticationTagMismatchException ex) {
                throw new VaultException(VaultError.Damaged, ex);
            }
            return Deserialize(plaintext);
        } finally {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static Settings Deserialize(ReadOnlySpan<byte> json) {
        try {
            var probe = JsonSerializer.Deserialize(json, ModelJsonContext.Default.SchemaProbe);
            if (probe is not null && probe.SchemaVersion > Settings.CurrentSchemaVersion) {
                throw new VaultException(VaultError.UnsupportedSchema);
            }
            return JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings)
                ?? throw new VaultException(VaultError.InvalidContent);
        } catch (JsonException ex) {
            throw new VaultException(VaultError.InvalidContent, ex);
        }
    }
}
