namespace Easy.TimeSeries;

using System.Buffers.Binary;
using static Constants;

internal readonly struct ColumnHeader
{
    public const int Size = 9; // sizeof(uint) + sizeof(byte);
    private const int RecordsIdx = 4;
    private const int BitsInLastWordIdx = 8;

    public static readonly ColumnHeader Empty = new(0);

    public long DataLength { get; }

    public int Records { get; }

    public int BitsInLastWord { get; }

    public bool IsEmpty => DataLength == 0;

    public long TotalBits => BitsInLastWord == 0
            ? DataLength * WordBitSize
            : ((DataLength - 1) * WordBitSize) + BitsInLastWord;

    public long ColumnLength => DataLength + Size;

    public ColumnHeader(long dataLength, int records, int bitsInLastWord)
    {
        Expect.Range(dataLength, 1, uint.MaxValue);
        Expect.Range(records, 1, int.MaxValue);
        Expect.Range(bitsInLastWord, 0, WordBitSize);

        DataLength = dataLength;
        Records = records;
        BitsInLastWord = bitsInLastWord;
    }

    private ColumnHeader(long dataLength)
    {
        DataLength = dataLength;
        Records = 0;
        BitsInLastWord = 0;
    }

    public void WriteTo(Span<byte> buffer)
    {
        Expect.MinLength(buffer, Size);

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)DataLength);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[RecordsIdx..], Records);
        buffer[BitsInLastWordIdx] = (byte)BitsInLastWord;
    }

    public static bool TryReadFrom(ReadOnlySpan<byte> buffer, out ColumnHeader columnHeader)
    {
        if (buffer.Length < Size)
        {
            columnHeader = Empty;
            return false;
        }

        var byteLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        if (byteLength < 1)
        {
            columnHeader = Empty;
            return false;
        }

        var records = BinaryPrimitives.ReadInt32LittleEndian(buffer[RecordsIdx..]);
        if (records < 1)
        {
            columnHeader = Empty;
            return false;
        }

        var bitsInLastWord = (int)buffer[BitsInLastWordIdx];
        columnHeader = new ColumnHeader(byteLength, records, bitsInLastWord);
        return true;
    }

    public static int GetTotalLength(ReadOnlySpan<byte> buffer, ColumnValueType valueType = ColumnValueType.None)
    {
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer) + Size;
        if (valueType == ColumnValueType.Category)
        {
            length += CategoryMap.GetTotalLength(buffer[length..]);
        }

        return length;
    }
}
