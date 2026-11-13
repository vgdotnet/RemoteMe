using System.Buffers;
using System.Security.Cryptography;

namespace RemoteMe.Core.Security;

// A buffer for plain text that zeroes every array it gives up, on growth and on dispose.
internal sealed class SensitiveBufferWriter : IBufferWriter<byte>, IDisposable {
    private byte[] _buffer;
    private int _written;

    public SensitiveBufferWriter(int initialCapacity = 16 * 1024) {
        _buffer = GC.AllocateArray<byte>(initialCapacity, pinned: true);
    }

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public void Advance(int count) {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _buffer.Length - _written);
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0) {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0) {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    public void Dispose() {
        CryptographicOperations.ZeroMemory(_buffer);
        _written = 0;
    }

    private void EnsureCapacity(int sizeHint) {
        var needed = Math.Max(sizeHint, 1);
        if (_buffer.Length - _written >= needed) {
            return;
        }
        var larger = GC.AllocateArray<byte>(Math.Max(_buffer.Length * 2, _written + needed), pinned: true);
        _buffer.AsSpan(0, _written).CopyTo(larger);
        CryptographicOperations.ZeroMemory(_buffer);
        _buffer = larger;
    }
}
