using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace RemoteMe.Core.Security;

// A secret kept encrypted in memory with a key that exists only in this process, so its plain text
// lives only for the moment it is used.
[JsonConverter(typeof(ProtectedSecretJsonConverter))]
public sealed class ProtectedSecret {
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly byte[] s_key = CreateKey();

    private readonly byte[] _nonce;
    private readonly byte[] _ciphertext;
    private readonly byte[] _tag;

    private ProtectedSecret(ReadOnlySpan<byte> utf8) {
        _nonce = RandomNumberGenerator.GetBytes(NonceSize);
        _ciphertext = new byte[utf8.Length];
        _tag = new byte[TagSize];
        using var aes = new AesGcm(s_key, TagSize);
        aes.Encrypt(_nonce, utf8, _ciphertext, _tag);
    }

    public bool IsEmpty => _ciphertext.Length == 0;

    public static ProtectedSecret Create(ReadOnlySpan<char> value) {
        var buffer = GC.AllocateArray<byte>(Encoding.UTF8.GetMaxByteCount(value.Length), pinned: true);
        try {
            var length = Encoding.UTF8.GetBytes(value, buffer);
            return new ProtectedSecret(buffer.AsSpan(0, length));
        } finally {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    internal static ProtectedSecret FromUtf8(ReadOnlySpan<byte> value) => new(value);

    public string Reveal() {
        var buffer = Decrypt();
        try {
            return Encoding.UTF8.GetString(buffer);
        } finally {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    internal void UseUtf8(Action<ReadOnlySpan<byte>> action) {
        var buffer = Decrypt();
        try {
            action(buffer);
        } finally {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    public override string ToString() => nameof(ProtectedSecret);

    private byte[] Decrypt() {
        var buffer = GC.AllocateArray<byte>(_ciphertext.Length, pinned: true);
        using var aes = new AesGcm(s_key, TagSize);
        aes.Decrypt(_nonce, _ciphertext, _tag, buffer);
        return buffer;
    }

    private static byte[] CreateKey() {
        var key = GC.AllocateArray<byte>(KeySize, pinned: true);
        RandomNumberGenerator.Fill(key);
        return key;
    }
}
