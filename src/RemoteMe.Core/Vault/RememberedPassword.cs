using System.Buffers.Binary;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using RemoteMe.Core.Security;

namespace RemoteMe.Core.Vault;

// The master password saved on one computer, so RemoteMe opens there without asking for it. It is
// encrypted with DPAPI for the current Windows user, and the salt of the settings file is part of the
// entropy, so it fits only the settings file and the master password it was saved for: the salt changes
// with every new master password. The file sits next to the settings file, one per computer and Windows
// user, since the folder may be shared by several computers through a cloud drive and only this computer
// and user can decrypt it.
//
// File layout, little-endian: magic "RMUnlock" (8), format version (2), reserved, zero (2), the salt of
// the settings file (32), then the DPAPI blob. The salt in clear tells a master password changed since
// then apart from a copy DPAPI cannot decrypt.
public sealed class RememberedPassword {
    private const ushort FormatVersion = 1;
    private const int SaltOffset = 12;
    private const int HeaderSize = SaltOffset + VaultHeader.SaltSize;
    private const int MaxFileSize = 64 * 1024;

    // The computer id becomes part of the file name; ForThisComputer gives the one for this computer.
    public RememberedPassword(string settingsPath, string computerId) {
        ArgumentException.ThrowIfNullOrEmpty(computerId);
        if (computerId.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) {
            throw new ArgumentException("The computer id cannot be part of a file name.", nameof(computerId));
        }
        var fullPath = System.IO.Path.GetFullPath(settingsPath);
        var name = $"{System.IO.Path.GetFileNameWithoutExtension(fullPath)}.{computerId}.unlock";
        Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fullPath) ?? "", name);
    }

    public string Path { get; }

    public bool Exists => File.Exists(Path);

    private static ReadOnlySpan<byte> Magic => "RMUnlock"u8;

    private static ReadOnlySpan<byte> EntropyLabel => "RemoteMe remembered master password"u8;

    public static RememberedPassword ForThisComputer(string settingsPath) => new(settingsPath, ThisComputerId());

    // The computer name keeps the file recognizable. The hash of the machine id and the user's SID tells
    // apart computers with the same name (the name is cut to 15 characters) and users of one computer,
    // whose copies would otherwise overwrite each other in a shared folder.
    internal static string ThisComputerId() {
        if (!OperatingSystem.IsWindows()) {
            return Environment.MachineName;
        }
        string? machineId = null;
        try {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            machineId = key?.GetValue("MachineGuid") as string;
        } catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException) {
        }
        using var identity = WindowsIdentity.GetCurrent();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{machineId}\n{identity.User?.Value}"));
        return $"{Environment.MachineName}-{Convert.ToHexStringLower(hash.AsSpan(0, 4))}";
    }

    public void Delete() {
        if (File.Exists(Path)) {
            File.Delete(Path);
        }
    }

    internal void Save(ProtectedSecret password, ReadOnlySpan<byte> salt) {
        if (salt.Length != VaultHeader.SaltSize) {
            throw new ArgumentException("The salt has the wrong size.", nameof(salt));
        }
        var entropy = Entropy(salt);
        var blob = Array.Empty<byte>();
        password.UseUtf8(utf8 => blob = Dpapi.Protect(utf8, entropy));
        var file = new byte[HeaderSize + blob.Length];
        Magic.CopyTo(file);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(Magic.Length), FormatVersion);
        salt.CopyTo(file.AsSpan(SaltOffset));
        blob.CopyTo(file.AsSpan(HeaderSize));
        AtomicFile.Write(Path, file);
    }

    internal RememberedPasswordState Read(ReadOnlySpan<byte> salt, out ProtectedSecret? password) {
        password = null;
        byte[] file;
        try {
            var info = new FileInfo(Path);
            if (!info.Exists) {
                return RememberedPasswordState.Missing;
            }
            if (info.Length > MaxFileSize) {
                return RememberedPasswordState.Unreadable;
            }
            file = File.ReadAllBytes(Path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return RememberedPasswordState.Unreadable;
        }

        var span = file.AsSpan();
        if (span.Length <= HeaderSize
            || !span.StartsWith(Magic)
            || BinaryPrimitives.ReadUInt16LittleEndian(span[Magic.Length..]) != FormatVersion
            || BinaryPrimitives.ReadUInt16LittleEndian(span[(Magic.Length + 2)..]) != 0) {
            return RememberedPasswordState.Unreadable;
        }
        if (!span.Slice(SaltOffset, VaultHeader.SaltSize).SequenceEqual(salt)) {
            return RememberedPasswordState.PasswordChanged;
        }

        byte[] plaintext;
        try {
            plaintext = Dpapi.Unprotect(span[HeaderSize..], Entropy(salt));
        } catch (CryptographicException) {
            return RememberedPasswordState.Unreadable;
        }
        try {
            if (plaintext.Length == 0) {
                return RememberedPasswordState.Unreadable;
            }
            password = ProtectedSecret.FromUtf8(plaintext);
            return RememberedPasswordState.Fits;
        } finally {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] Entropy(ReadOnlySpan<byte> salt) {
        var entropy = new byte[EntropyLabel.Length + salt.Length];
        EntropyLabel.CopyTo(entropy);
        salt.CopyTo(entropy.AsSpan(EntropyLabel.Length));
        return entropy;
    }
}
