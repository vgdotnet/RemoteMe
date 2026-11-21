using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RemoteMe.Core.Security;

// DPAPI for the current Windows user: what it encrypts only the same user on the same computer can
// decrypt, and only with the same entropy. Plain-text buffers, managed and native, are zeroed.
internal static class Dpapi {
    private const int UiForbidden = 0x1;
    private const int LocalMachine = 0x4;

    // A blob starts with its version (4), the provider (16), the master key version (4) and the master
    // key (16); then come the flags it was made with. With the machine flag cleared, DPAPI refuses the
    // blob (DpapiTests).
    private const int FlagsOffset = 40;

    public static byte[] Protect(ReadOnlySpan<byte> data, ReadOnlySpan<byte> entropy) {
        var input = PinnedCopy(data);
        var entropyCopy = PinnedCopy(entropy);
        try {
            var dataIn = Blob(input);
            var entropyIn = Blob(entropyCopy);
            if (!NativeMethods.CryptProtectData(ref dataIn, IntPtr.Zero, ref entropyIn, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var dataOut)) {
                throw new CryptographicException(Marshal.GetLastPInvokeErrorMessage());
            }
            try {
                var output = new byte[dataOut.Size];
                Marshal.Copy(dataOut.Data, output, 0, output.Length);
                return output;
            } finally {
                NativeMethods.LocalFree(dataOut.Data);
            }
        } finally {
            CryptographicOperations.ZeroMemory(input);
            GC.KeepAlive(entropyCopy);
        }
    }

    // The result is pinned; the caller zeroes it. A blob made with CRYPTPROTECT_LOCAL_MACHINE is refused:
    // any user of this computer can make one, so it does not prove the current user saved the data.
    public static byte[] Unprotect(ReadOnlySpan<byte> blob, ReadOnlySpan<byte> entropy) {
        if (blob.Length < FlagsOffset + sizeof(int) || (BinaryPrimitives.ReadInt32LittleEndian(blob[FlagsOffset..]) & LocalMachine) != 0) {
            throw new CryptographicException("The data is not protected for the current user.");
        }
        var input = PinnedCopy(blob);
        var entropyCopy = PinnedCopy(entropy);
        var dataIn = Blob(input);
        var entropyIn = Blob(entropyCopy);
        if (!NativeMethods.CryptUnprotectData(ref dataIn, IntPtr.Zero, ref entropyIn, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var dataOut)) {
            throw new CryptographicException(Marshal.GetLastPInvokeErrorMessage());
        }
        GC.KeepAlive(input);
        GC.KeepAlive(entropyCopy);
        try {
            var output = GC.AllocateArray<byte>(dataOut.Size, pinned: true);
            Marshal.Copy(dataOut.Data, output, 0, output.Length);
            return output;
        } finally {
            if (dataOut.Data != IntPtr.Zero) {
                Marshal.Copy(new byte[dataOut.Size], 0, dataOut.Data, dataOut.Size);
                NativeMethods.LocalFree(dataOut.Data);
            }
        }
    }

    private static byte[] PinnedCopy(ReadOnlySpan<byte> data) {
        var copy = GC.AllocateArray<byte>(data.Length, pinned: true);
        data.CopyTo(copy);
        return copy;
    }

    // The arrays come from the pinned object heap, so their address does not change.
    private static NativeMethods.DataBlob Blob(byte[] pinned) => new() {
        Size = pinned.Length,
        Data = pinned.Length == 0 ? IntPtr.Zero : Marshal.UnsafeAddrOfPinnedArrayElement(pinned, 0),
    };

    private static class NativeMethods {
        [DllImport("crypt32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptProtectData(
            ref DataBlob dataIn,
            IntPtr description,
            ref DataBlob entropy,
            IntPtr reserved,
            IntPtr prompt,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            ref DataBlob entropy,
            IntPtr reserved,
            IntPtr prompt,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr LocalFree(IntPtr memory);

        [StructLayout(LayoutKind.Sequential)]
        public struct DataBlob {
            public int Size;
            public IntPtr Data;
        }
    }
}
