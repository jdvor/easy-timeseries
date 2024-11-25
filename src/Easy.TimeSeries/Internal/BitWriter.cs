namespace Easy.TimeSeries;

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Constants;

internal sealed class BitWriter
{
    private readonly IBufferWriter<byte> bufferProvider;
    private readonly Func<Memory<byte>>? getColumnHeaderMemory;
    private ulong word;
    private int bufferedBits;
    private int dataLength;
    private int records;

    public BitWriter(IBufferWriter<byte> bufferProvider, Func<Memory<byte>>? getColumnHeaderMemory = null)
    {
        this.bufferProvider = bufferProvider;
        this.getColumnHeaderMemory = getColumnHeaderMemory;
    }

    public void Write(ulong value, int bits)
    {
        Debug.Assert(bits is >= 1 and <= WordBitSize, "bits not in range <1, 64>");

        records++;
        word += value << bufferedBits;
        bufferedBits += bits;
        if (bufferedBits >= WordBitSize)
        {
            value >>= 1;
            bits--;
            bufferedBits -= WordBitSize;
            WriteWord();
            word = value >> (bits - bufferedBits);
        }

        word &= (1UL << bufferedBits) - 1;
    }

    public void Write(long value, int bits)
        => Write((ulong)value, bits);

    public void Write(int value, int bits)
        => Write((ulong)value, bits);

    public void Flush()
    {
        var bitsInLastWord = 0;
        if (bufferedBits > 0)
        {
            bitsInLastWord = bufferedBits;
            WriteWord();
            word = 0;
            bufferedBits = 0;
        }

        if (getColumnHeaderMemory is not null)
        {
            var header = new ColumnHeader(dataLength, records, bitsInLastWord);
            var memory = getColumnHeaderMemory();
            header.WriteTo(memory.Span);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWord()
    {
        var destination = bufferProvider.GetSpan(WordByteSize);
        bufferProvider.Advance(WordByteSize);
        BinaryPrimitives.WriteUInt64LittleEndian(destination, word);
        dataLength += WordByteSize;
    }
}
