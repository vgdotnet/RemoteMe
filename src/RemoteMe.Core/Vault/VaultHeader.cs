using System.Buffers.Binary;

namespace RemoteMe.Core.Vault;

// File layout, little-endian:
//    0  magic "RemoteMe" (8)      16  salt (32)
//    8  format version (2)        48  key check (32)
//   10  KDF id (1)                80  revision (8)
//   11  reserved, zero (1)        88  nonce (12)
//   12  PBKDF2 iterations (4)    100  ciphertext, then the AES-GCM tag (16)
// AES-GCM authenticates the whole header; bytes 0-47 also feed the key check.
internal sealed class VaultHeader {
    public const int Size = 100;
    public const int KdfPartSize = 48;
    public const int SaltSize = 32;
    public const int KeyCheckSize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const ushort FormatVersion = 1;
    public const byte KdfPbkdf2Sha256 = 1;

    public required uint Iterations { get; init; }

    public required byte[] Salt { get; init; }

    public required byte[] KeyCheck { get; init; }

    public required ulong Revision { get; init; }

    public required byte[] Nonce { get; init; }

    private static ReadOnlySpan<byte> Magic => "RemoteMe"u8;

    public static byte[] BuildKdfPart(uint iterations, ReadOnlySpan<byte> salt) {
        var part = new byte[KdfPartSize];
        Magic.CopyTo(part);
        BinaryPrimitives.WriteUInt16LittleEndian(part.AsSpan(8), FormatVersion);
        part[10] = KdfPbkdf2Sha256;
        BinaryPrimitives.WriteUInt32LittleEndian(part.AsSpan(12), iterations);
        salt.CopyTo(part.AsSpan(16));
        return part;
    }

    public static VaultHeader Read(ReadOnlySpan<byte> file) {
        if (file.Length < Size + TagSize || !file.StartsWith(Magic)) {
            throw new VaultException(VaultError.InvalidFormat);
        }
        var version = BinaryPrimitives.ReadUInt16LittleEndian(file[8..]);
        if (version > FormatVersion) {
            throw new VaultException(VaultError.UnsupportedVersion);
        }
        if (version != FormatVersion || file[10] != KdfPbkdf2Sha256 || file[11] != 0) {
            throw new VaultException(VaultError.InvalidFormat);
        }
        return new VaultHeader {
            Iterations = BinaryPrimitives.ReadUInt32LittleEndian(file[12..]),
            Salt = file.Slice(16, SaltSize).ToArray(),
            KeyCheck = file.Slice(48, KeyCheckSize).ToArray(),
            Revision = BinaryPrimitives.ReadUInt64LittleEndian(file[80..]),
            Nonce = file.Slice(88, NonceSize).ToArray(),
        };
    }

    public void WriteTo(Span<byte> destination) {
        BuildKdfPart(Iterations, Salt).CopyTo(destination);
        KeyCheck.CopyTo(destination[48..]);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[80..], Revision);
        Nonce.CopyTo(destination[88..]);
    }
}
