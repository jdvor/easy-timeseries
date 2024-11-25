namespace Easy.TimeSeries;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Constants;

internal ref struct BoolReader
{
    private readonly ReadOnlySpan<byte> buffer;
    private int bufferPosition;
    private ulong word;
    private long unreadBits;
    private int bufferedBits;

    public ColumnHeader ColumnHeader { get; }

    public BoolReader(ReadOnlySpan<byte> buffer, ColumnHeader columnHeader)
    {
        EnsureBufferCanFit(ref buffer, columnHeader.DataLength);
        this.buffer = buffer;
        unreadBits = columnHeader.TotalBits;
        ColumnHeader = columnHeader;
    }

    public BoolReader(ReadOnlySpan<byte> buffer)
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

    public bool Read()
    {
        Debug.Assert(unreadBits > 0, "cant' read anymore");

        if (bufferedBits == 0)
        {
            ReadWord();
            bufferedBits = WordBitSize;
        }

        var bitPosition = WordBitSize - bufferedBits;
        var value = (word & (1UL << bitPosition)) != 0;
        --unreadBits;
        --bufferedBits;
        return value;
    }

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
