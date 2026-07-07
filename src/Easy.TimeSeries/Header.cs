namespace Easy.TimeSeries;

using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

/// <summary>
/// The file preamble: magic bytes, format <see cref="Version"/>, and the ordered <see cref="ColumnInfo"/> descriptors.
/// It is the self-describing table of contents a reader uses to validate the buffer and locate each column block.
/// </summary>
public readonly record struct Header
{
    internal const byte H1 = 0x02;
    internal const byte H2 = 0xFD;

    /// <summary>Maximum number of columns a file may contain.</summary>
    public const int MaxColumns = 255;
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Sentinel representing an unreadable or absent header.</summary>
    public static readonly Header Empty = new(Version.None, ImmutableArray<ColumnInfo>.Empty);

    /// <summary>Format version of the file.</summary>
    public Version Version { get; }

    /// <summary>Column descriptors in file order.</summary>
    public ImmutableArray<ColumnInfo> Columns { get; }

    /// <summary>Whether this is the <see cref="Empty"/> sentinel.</summary>
    public bool IsEmpty => Version == Version.None;

    // 2 magic bytes, layout (1), label count (1), columns (n)
    /// <summary>Upper bound on the serialized header size in bytes.</summary>
    public int SizeHint => 2 + 1 + 1 + Columns.Sum(x => x.SizeHint);

    /// <summary>Creates a header for the given version and columns.</summary>
    public Header(Version version, ImmutableArray<ColumnInfo> columns)
    {
        if (columns.Length > MaxColumns)
        {
            throw new ArgumentException($"Maximum number of labels is {MaxColumns}", nameof(columns));
        }

        Version = version;
        Columns = columns;
    }

    /// <summary>Serializes the header into <paramref name="buffer"/> and returns the number of bytes written.</summary>
    public int WriteTo(Span<byte> buffer)
    {
        buffer[0] = H1;
        buffer[1] = H2;
        buffer[2] = (byte)Version;
        buffer[3] = (byte)Columns.Length;
        buffer = buffer[4..];
        var total = 4;
        foreach (var column in Columns)
        {
            var written = WriteColumn(column, buffer);
            if (written > 0)
            {
                buffer = buffer[written..];
                total += written;
            }
        }

        return total;
    }

    /// <summary>
    /// Attempts to parse a header from the start of <paramref name="buffer"/>. Returns <c>false</c> (and
    /// <see cref="Empty"/>) on bad magic bytes, an unknown version, or a malformed descriptor rather than throwing.
    /// </summary>
    /// <param name="read">Number of header bytes consumed, i.e. the offset of the first column block.</param>
    public static bool TryReadFrom(ReadOnlySpan<byte> buffer, out Header header, out int read)
    {
        read = 0;
        try
        {
            if (buffer[0] != H1 || buffer[1] != H2 || !Enum.IsDefined(typeof(Version), buffer[2]))
            {
                header = Empty;
                return false;
            }

            var layout = (Version)buffer[2];
            var labelCount = buffer[3];
            read = 4;
            buffer = buffer[4..];
            var columns = new ColumnInfo[labelCount];
            for (var i = 0; i < labelCount; i++)
            {
                var column = ReadColumn(i, buffer, out var bytesRead);
                read += bytesRead;
                if (column.IsEmpty)
                {
                    header = Empty;
                    return false;
                }

                columns[i] = column;
                buffer = buffer[bytesRead..];
            }

            header = new Header(layout, ImmutableArray.Create(columns));
            return true;
        }
        catch
        {
            header = Empty;
            return false;
        }
    }

    private static int WriteColumn(ColumnInfo column, Span<byte> buffer)
    {
        const int intSize = sizeof(int);
        int pos = 0;

        // column value type at index: 0
        buffer[pos] = (byte)column.ValueType;
        ++pos;

        // column meta at index <1, 4>
        var metaSpan = buffer.Slice(pos, intSize);
        BinaryPrimitives.WriteInt32LittleEndian(metaSpan, column.Meta);
        pos += intSize;

        if (string.IsNullOrEmpty(column.Label))
        {
            // column empty label at index 5
            buffer[pos] = 0;
            return pos + 1;
        }

        // column label at index <5, 5 + label.Length>
        var labelBytes = Utf8.GetBytes(column.Label);
        buffer[pos] = (byte)labelBytes.Length;
        ++pos;
        labelBytes.CopyTo(buffer[pos..]);
        pos += labelBytes.Length;

        return pos;
    }

    private static ColumnInfo ReadColumn(int index, ReadOnlySpan<byte> buffer, out int bytesRead)
    {
        const int intSize = sizeof(int);
        int pos = 0;

        if (!Enum.IsDefined(typeof(ColumnValueType), buffer[pos]))
        {
            bytesRead = 1;
            return ColumnInfo.Empty;
        }

        var valueType = (ColumnValueType)buffer[pos];
        ++pos;
        var metaSpan = buffer.Slice(pos, intSize);
        var meta = BinaryPrimitives.ReadInt32LittleEndian(metaSpan);
        pos += intSize;

        var labelLength = (int)buffer[pos];
        ++pos;
        if (labelLength == 0)
        {
            bytesRead = pos;
            return new ColumnInfo(index, valueType, meta, string.Empty);
        }

        var labelSpan = buffer.Slice(pos, labelLength);
        var label = Utf8.GetString(labelSpan);
        bytesRead = pos + labelLength;
        return new ColumnInfo(index, valueType, meta, label);
    }

    /// <summary>
    /// Parses the header and walks the column blocks to produce a located <see cref="Column"/> for each. When
    /// <paramref name="columnIndexes"/> is given, only those columns are returned, in that order (projection);
    /// otherwise every column is returned. Throws <see cref="InvalidReadBufferException"/> on an invalid buffer.
    /// </summary>
    public static (Header header, Column[] columns) ReadLayout(ReadOnlySpan<byte> buffer, int[]? columnIndexes = null)
    {
        if (!TryReadFrom(buffer, out var header, out var bytesRead))
        {
            throw new InvalidReadBufferException("cannot read buffer header");
        }

        ValidateHeaderAndColumnIndexes(header, columnIndexes);

        var explicitIndexes = columnIndexes is not null;
        var numCols = explicitIndexes ? columnIndexes!.Length : header.Columns.Length;
        var position = bytesRead;
        var columns = new Dictionary<int, Column>(numCols);
        for (var i = 0; i < header.Columns.Length; i++)
        {
            var info = header.Columns[i];
            var length = ColumnHeader.GetTotalLength(buffer[position..], info.ValueType);
            if (explicitIndexes)
            {
                if (columnIndexes!.Contains(i))
                {
                    columns.Add(i, new Column(info, position, length));
                }
            }
            else
            {
                columns.Add(i, new Column(info, position, length));
            }

            position += length;

            if (explicitIndexes && columns.Count == numCols)
            {
                break;
            }
        }

        if (!explicitIndexes)
        {
            return (header, columns.Values.ToArray());
        }

        var result = new Column[numCols];
        var j = 0;
        foreach (var index in columnIndexes!)
        {
            result[j] = columns[index];
            j++;
        }

        return (header, result);
    }

    private static void ValidateHeaderAndColumnIndexes(Header header, int[]? columnIndexes)
    {
        if (header.Version != Version.V1)
        {
            throw new InvalidReadBufferException("version is not V1");
        }

        if (columnIndexes is null)
        {
            return;
        }

        var unmatchedIndexes = columnIndexes.Where(x => x < 0 || x >= header.Columns.Length).ToArray();
        if (unmatchedIndexes.Length > 0)
        {
            throw new InvalidReadBufferException("umatched columns");
        }

        if (columnIndexes.Distinct().Count() != columnIndexes.Length)
        {
            throw new InvalidReadBufferException("duplicate column indexes");
        }
    }
}
