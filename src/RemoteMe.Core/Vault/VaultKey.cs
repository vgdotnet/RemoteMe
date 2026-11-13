using System.Security.Cryptography;
using System.Text;

namespace RemoteMe.Core.Vault;

internal sealed class VaultKey : IDisposable {
    private const int KeySize = 32;

    private readonly byte[] _encryptionKey;

    private VaultKey(byte[] encryptionKey, byte[] keyCheck) {
        _encryptionKey = encryptionKey;
        KeyCheck = keyCheck;
    }

    public byte[] KeyCheck { get; }

    // PBKDF2 gives 64 bytes: the first half encrypts, the second half only proves the password, so a
    // wrong password can be told apart from a damaged file. A guess still costs a full PBKDF2 run.
    public static VaultKey Derive(string password, uint iterations, ReadOnlySpan<byte> salt) {
        var material = GC.AllocateArray<byte>(KeySize * 2, pinned: true);
        try {
            var normalized = password.Normalize(NormalizationForm.FormC);
            Rfc2898DeriveBytes.Pbkdf2(normalized, salt, material, checked((int)iterations), HashAlgorithmName.SHA256);
            var encryptionKey = GC.AllocateArray<byte>(KeySize, pinned: true);
            material.AsSpan(0, KeySize).CopyTo(encryptionKey);
            var keyCheck = HMACSHA256.HashData(material.AsSpan(KeySize), VaultHeader.BuildKdfPart(iterations, salt));
            return new VaultKey(encryptionKey, keyCheck);
        } finally {
            CryptographicOperations.ZeroMemory(material);
        }
    }

    public bool Matches(ReadOnlySpan<byte> keyCheck) => CryptographicOperations.FixedTimeEquals(KeyCheck, keyCheck);

    public void Encrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag,
        ReadOnlySpan<byte> associatedData) {
        using var aes = new AesGcm(_encryptionKey, VaultHeader.TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
    }

    public void Decrypt(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext,
        ReadOnlySpan<byte> associatedData) {
        using var aes = new AesGcm(_encryptionKey, VaultHeader.TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
    }

    public void Dispose() {
        CryptographicOperations.ZeroMemory(_encryptionKey);
    }
}
