namespace Easy.TimeSeries;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Constants;

internal ref struct BitReader
{
    private readonly ReadOnlySpan<byte> buffer;
    private int bufferPosition;
    private ulong word;
    private long unreadBits;
    private int bufferedBits;

    public ColumnHeader ColumnHeader { get; }

    public BitReader(ReadOnlySpan<byte> buffer, ColumnHeader columnHeader)
    {
        EnsureBufferCanFit(ref buffer, columnHeader.DataLength);
        this.buffer = buffer;
        unreadBits = columnHeader.TotalBits;
        ColumnHeader = columnHeader;
    }

    public BitReader(ReadOnlySpan<byte> buffer)
    {
        if (!ColumnHeader.TryReadFrom(buffer, out var columnHeader))
        {
            throw new ArgumentException("Buffer contains invalid column header.", nameof(buffer));
        }

        this.buffer = buffer[ColumnHeader.Size..];
        EnsureBufferCanFit(ref this.buffer, columnHeader.DataLength);
        unreadBits = columnHeader.TotalBits;
        ColumnHeader = columnHeader;
    }

    public ulong Read(int bits)
    {
        Debug.Assert(bits is >= 1 and <= WordBitSize, "bits not in range <1, 64>");

        if (unreadBits <= 0)
        {
            throw new NoMoreDataToReadException();
        }

        var value = word;
        if (bufferedBits < bits)
        {
            ReadWord();
            value += word << bufferedBits;
            bufferedBits = bufferedBits + WordBitSize - bits;

            if (bufferedBits == 0)
            {
                word = 0;
            }
            else
            {
                word >>= WordBitSize - bufferedBits;
                value &= (2UL << (bits - 1)) - 1;
            }
        }
        else
        {
            bufferedBits -= bits;
            word >>= bits;
            value &= (1UL << bits) - 1;
        }

        unreadBits -= bits;

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadWord()
    {
        var slice = buffer.Slice(bufferPosition, WordByteSize);
        bufferPosition += WordByteSize;
        word = BinaryPrimitives.ReadUInt64LittleEndian(slice);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureBufferCanFit(ref ReadOnlySpan<byte> buffer, long declaredLength)
    {
        if (buffer.Length < declaredLength - ColumnHeader.Size)
        {
            throw new ArgumentException(
                $"Buffer is smaller than declared by column header ({declaredLength - ColumnHeader.Size}).",
                nameof(buffer));
        }
    }
}
