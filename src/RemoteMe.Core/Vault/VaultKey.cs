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

    private static ReadOnlySpan<byte> EncryptionKeyInfo => "RemoteMe encryption key"u8;

    private static ReadOnlySpan<byte> KeyCheckInfo => "RemoteMe key check"u8;

    // PBKDF2 gives one SHA-256 block, and HKDF splits it into the encryption key and a key that only
    // proves the password, so a wrong password can be told apart from a damaged file. An unlock costs
    // exactly what one guess costs.
    public static VaultKey Derive(string password, uint iterations, ReadOnlySpan<byte> salt) {
        var block = Pbkdf2(password, iterations, salt, KeySize);
        try {
            return FromBlock(block, iterations, salt);
        } finally {
            CryptographicOperations.ZeroMemory(block);
        }
    }

    // Format 1 asked PBKDF2 for 64 bytes: two blocks of full cost, the first the encryption key and the
    // second the key-check key, so an unlock cost twice what a guess costs.
    public static VaultKey DeriveFormat1(string password, uint iterations, ReadOnlySpan<byte> salt) {
        var material = Pbkdf2(password, iterations, salt, KeySize * 2);
        try {
            var encryptionKey = GC.AllocateArray<byte>(KeySize, pinned: true);
            material.AsSpan(0, KeySize).CopyTo(encryptionKey);
            var keyCheck = HMACSHA256.HashData(material.AsSpan(KeySize), VaultHeader.BuildKdfPart(VaultHeader.Format1, iterations, salt));
            return new VaultKey(encryptionKey, keyCheck);
        } finally {
            CryptographicOperations.ZeroMemory(material);
        }
    }

    // The format 1 encryption key is the PBKDF2 block that Derive starts from with the same salt and
    // iterations, so the current keys come from it without another PBKDF2 run.
    public VaultKey UpgradeFormat1(uint iterations, ReadOnlySpan<byte> salt) => FromBlock(_encryptionKey, iterations, salt);

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

    private static byte[] Pbkdf2(string password, uint iterations, ReadOnlySpan<byte> salt, int length) {
        var output = GC.AllocateArray<byte>(length, pinned: true);
        var normalized = password.Normalize(NormalizationForm.FormC);
        Rfc2898DeriveBytes.Pbkdf2(normalized, salt, output, checked((int)iterations), HashAlgorithmName.SHA256);
        return output;
    }

    private static VaultKey FromBlock(ReadOnlySpan<byte> block, uint iterations, ReadOnlySpan<byte> salt) {
        var encryptionKey = GC.AllocateArray<byte>(KeySize, pinned: true);
        var checkKey = GC.AllocateArray<byte>(KeySize, pinned: true);
        try {
            HKDF.Expand(HashAlgorithmName.SHA256, block, encryptionKey, EncryptionKeyInfo);
            HKDF.Expand(HashAlgorithmName.SHA256, block, checkKey, KeyCheckInfo);
            var keyCheck = HMACSHA256.HashData(checkKey, VaultHeader.BuildKdfPart(VaultHeader.FormatVersion, iterations, salt));
            return new VaultKey(encryptionKey, keyCheck);
        } finally {
            CryptographicOperations.ZeroMemory(checkKey);
        }
    }
}
