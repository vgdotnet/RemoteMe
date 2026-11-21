using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RemoteMe.Core.Security;

namespace RemoteMe.Tests.Security;

public sealed class DpapiTests {
    private const int UiForbidden = 0x1;
    private const int LocalMachine = 0x4;
    private const int FlagsOffset = 40;

    private static readonly byte[] Data = "secret data"u8.ToArray();
    private static readonly byte[] Entropy = "entropy of the test"u8.ToArray();

    [Fact]
    public void Unprotect_AfterProtect_GivesTheData() {
        var blob = Dpapi.Protect(Data, Entropy);

        Assert.Equal(Data, Dpapi.Unprotect(blob, Entropy));
        Assert.Equal(0, BitConverter.ToInt32(blob, FlagsOffset) & LocalMachine);
    }

    [Fact]
    public void Unprotect_WithOtherEntropy_Throws() {
        var blob = Dpapi.Protect(Data, Entropy);

        Assert.ThrowsAny<CryptographicException>(() => Dpapi.Unprotect(blob, "other entropy"u8));
    }

    [Fact]
    public void Unprotect_ABlobForTheWholeComputer_IsRefused() {
        // Any user of the computer can make such a blob, and DPAPI decrypts it for everyone.
        var blob = ProtectForTheComputer(Data, Entropy);
        Assert.Equal(LocalMachine, BitConverter.ToInt32(blob, FlagsOffset) & LocalMachine);
        Assert.Equal(Data, UnprotectWithoutChecks(blob, Entropy));

        Assert.ThrowsAny<CryptographicException>(() => Dpapi.Unprotect(blob, Entropy));
    }

    [Fact]
    public void Unprotect_ABlobForTheWholeComputerWithTheFlagCleared_IsRefusedByDpapi() {
        var blob = ProtectForTheComputer(Data, Entropy);
        blob[FlagsOffset] &= unchecked((byte)~LocalMachine);

        Assert.ThrowsAny<CryptographicException>(() => UnprotectWithoutChecks(blob, Entropy));
        Assert.ThrowsAny<CryptographicException>(() => Dpapi.Unprotect(blob, Entropy));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(43)]
    public void Unprotect_ACutBlob_Throws(int length) {
        var blob = Dpapi.Protect(Data, Entropy);

        Assert.ThrowsAny<CryptographicException>(() => Dpapi.Unprotect(blob.AsSpan(0, length), Entropy));
    }

    internal static byte[] ProtectForTheComputer(byte[] data, byte[] entropy) {
        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        var entropyHandle = GCHandle.Alloc(entropy, GCHandleType.Pinned);
        try {
            var dataIn = new DataBlob { Size = data.Length, Data = dataHandle.AddrOfPinnedObject() };
            var entropyIn = new DataBlob { Size = entropy.Length, Data = entropyHandle.AddrOfPinnedObject() };
            if (!CryptProtectData(ref dataIn, IntPtr.Zero, ref entropyIn, IntPtr.Zero, IntPtr.Zero, UiForbidden | LocalMachine, out var dataOut)) {
                throw new CryptographicException(Marshal.GetLastPInvokeErrorMessage());
            }
            return TakeOutput(dataOut);
        } finally {
            dataHandle.Free();
            entropyHandle.Free();
        }
    }

    private static byte[] UnprotectWithoutChecks(byte[] blob, byte[] entropy) {
        var blobHandle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        var entropyHandle = GCHandle.Alloc(entropy, GCHandleType.Pinned);
        try {
            var dataIn = new DataBlob { Size = blob.Length, Data = blobHandle.AddrOfPinnedObject() };
            var entropyIn = new DataBlob { Size = entropy.Length, Data = entropyHandle.AddrOfPinnedObject() };
            if (!CryptUnprotectData(ref dataIn, IntPtr.Zero, ref entropyIn, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var dataOut)) {
                throw new CryptographicException(Marshal.GetLastPInvokeErrorMessage());
            }
            return TakeOutput(dataOut);
        } finally {
            blobHandle.Free();
            entropyHandle.Free();
        }
    }

    private static byte[] TakeOutput(DataBlob blob) {
        try {
            var output = new byte[blob.Size];
            Marshal.Copy(blob.Data, output, 0, output.Length);
            return output;
        } finally {
            LocalFree(blob.Data);
        }
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
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
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        ref DataBlob entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob {
        public int Size;
        public IntPtr Data;
    }
}
