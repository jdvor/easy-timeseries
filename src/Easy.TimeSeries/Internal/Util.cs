namespace Easy.TimeSeries;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

internal static class Util
{
    /// <summary>
    /// Brotli-compressed raw storage for numeric columns whose values are uncorrelated between rows, where
    /// XOR/delta ("Gorilla") encoding does not pay off (e.g., geographic coordinates). The column payload is the
    /// Brotli image of the values' raw little-endian bytes; <see cref="ColumnHeader.Records"/> holds the value
    /// count and <see cref="ColumnHeader.DataLength"/> the compressed byte length.
    /// Compresses <paramref name="src"/> (raw little-endian value bytes) into a fresh column buffer prefixed
    /// by its <see cref="ColumnHeader"/> and returns the writer. The caller owns disposal of the writer.
    /// </summary>
    public static PooledArrayBufferWriter BrotliCompress(
        ReadOnlySpan<byte> src,
        int records,
        float growFactor,
        int maxAllowedBufferSize)
    {
        var maxCompressed = BrotliEncoder.GetMaxCompressedLength(src.Length);
        var bufferWriter = new PooledArrayBufferWriter(ColumnHeader.Size + maxCompressed, growFactor, maxAllowedBufferSize);
        bufferWriter.Advance(ColumnHeader.Size);

        var dest = bufferWriter.GetSpan(maxCompressed);
        if (!BrotliEncoder.TryCompress(src, dest, out var written, Constants.Brotli.Quality, Constants.Brotli.Window))
        {
            bufferWriter.Dispose();
            throw new InvalidOperationException("Brotli compression of a raw column failed.");
        }

        bufferWriter.Advance(written);
        var header = new ColumnHeader(written, records, 0);
        header.WriteTo(bufferWriter.GetColumnHeaderMemory().Span);
        return bufferWriter;
    }

    /// <summary>
    /// Decompresses a raw-column payload (the bytes after the <see cref="ColumnHeader"/>) into
    /// <paramref name="destination"/>, whose length must exactly match the original value count.
    /// </summary>
    public static void BrotliDecompress<T>(ReadOnlySpan<byte> payload, Span<T> destination)
        where T : struct
    {
        var destBytes = MemoryMarshal.AsBytes(destination);
        if (!BrotliDecoder.TryDecompress(payload, destBytes, out var written) || written != destBytes.Length)
        {
            throw new InvalidOperationException(
                "Brotli decompression of a raw column failed or produced an unexpected size.");
        }
    }

    public static long GetPrecisionDivisor(TimePrecision precision)
    {
        return precision switch
        {
            TimePrecision.Milliseconds => TimeSpan.TicksPerMillisecond,
            TimePrecision.TenthsOfSecond => TimeSpan.TicksPerSecond / 10,
            TimePrecision.Seconds => TimeSpan.TicksPerSecond,
            TimePrecision.Days => TimeSpan.TicksPerSecond * 60 * 60 * 24,
            _ => throw new NotImplementedException(),
        };
    }

    public static string GetDateTimeFormat(TimePrecision precision)
    {
        return precision switch
        {
            TimePrecision.Milliseconds => "yyyy-MM-dd HH:mm:ss.fff",
            TimePrecision.TenthsOfSecond => "yyyy-MM-dd HH:mm:ss.f",
            TimePrecision.Seconds => "yyyy-MM-dd HH:mm:ss",
            _ => "yyyy-MM-dd HH:mm:ss.fff",
        };
    }

    public static TimeSpan GetMaxTimeSpan(TimePrecision precision)
    {
        var div = GetPrecisionDivisor(precision);
        return TimeSpan.FromTicks((long)int.MaxValue * div);
    }

    public static TimeSpan GetMinTimeSpan(TimePrecision precision)
    {
        var div = GetPrecisionDivisor(precision);
        return TimeSpan.FromTicks((long)int.MinValue * div);
    }

    public static string ToHeadDebug(this Span<byte> buffer, int max = 8, bool hex = true)
        => ToHeadDebug((ReadOnlySpan<byte>)buffer, max, hex);

    public static string ToHeadDebug(this ReadOnlySpan<byte> buffer, int max = 8, bool hex = true)
    {
        var len = buffer.Length < max ? buffer.Length : max;
        var capacity = (len * 5) + 10;
        var sb = new StringBuilder(capacity);
        sb.Append($"[{buffer.Length}] {{ ");
        for (var i = 0; i < len; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            if (hex)
            {
                sb.Append(buffer[i].ToString("X2"));
            }
            else
            {
                sb.Append(buffer[i]);
            }
        }

        if (buffer.Length != max)
        {
            sb.Append(", ...");
        }

        sb.Append(" }");

        return sb.ToString();
    }

    public static string ToTailDebug(this Span<byte> buffer, int max = 8, bool hex = true)
        => ToTailDebug((ReadOnlySpan<byte>)buffer, max, hex);

    public static string ToTailDebug(this ReadOnlySpan<byte> buffer, int max = 8, bool hex = true)
    {
        var len = buffer.Length < max ? buffer.Length : max;
        var capacity = (len * 5) + 10;
        var sb = new StringBuilder(capacity);
        sb.Append($"[{buffer.Length}] {{ ");
        if (buffer.Length != max)
        {
            sb.Append("..., ");
        }

        for (var i = buffer.Length - len; i < len; i++)
        {
            if (hex)
            {
                sb.Append(buffer[i].ToString("X2"));
            }
            else
            {
                sb.Append(buffer[i]);
            }
        }

        sb.Append(" }");

        return sb.ToString();
    }

    public static string ToHexString(this Span<byte> buffer)
        => ToHexString((ReadOnlySpan<byte>)buffer);

    public static ulong ToWord(this Span<byte> buffer)
        => BinaryPrimitives.ReadUInt64LittleEndian(buffer);

    public static ulong ToWord(this ReadOnlySpan<byte> buffer)
        => BinaryPrimitives.ReadUInt64LittleEndian(buffer);

    public static string ToHexString(this ReadOnlySpan<byte> buffer)
    {
        var capacity = (buffer.Length * 2) + ((buffer.Length % 8) * 2);
        var sb = new StringBuilder(capacity);
        for (var i = 0; i < buffer.Length; i++)
        {
            if (i > 0 && i % 8 == 0)
            {
                sb.AppendLine();
            }

            sb.Append(buffer[i].ToString("X2"));
        }

        return sb.ToString();
    }

    public static string ToHexString(this ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return string.Concat(bytes.Select(x => x.ToString("X2")));
    }

    public static string ToDebug(
        this IEnumerable<DateTime> times,
        bool newLines = true,
        string fmt = "yyyy-MM-dd HH:mm:ss.fff")
    {
        var sb = new StringBuilder();
        var writeInLineSeparator = false;
        foreach (var time in times)
        {
            if (newLines)
            {
                sb.AppendLine(time.ToString(fmt));
            }
            else
            {
                if (writeInLineSeparator)
                {
                    writeInLineSeparator = true;
                }
                else
                {
                    sb.Append(", ");
                }

                sb.Append(time.ToString(fmt));
            }
        }

        return sb.ToString();
    }

    public static int BitsRequiredToStore(long value)
        => value == 0 ? 1 : (int)Math.Ceiling(Math.Log2(Math.Abs(value) + 1));

    public static long MaxValueStoredInBits(int bits)
    {
        Expect.Range(bits, 1, 64);
        return (long)Math.Pow(2, bits) - 1;
    }

    public static string ToBinaryString(this long value)
        => Convert.ToString(value, 2);
}
