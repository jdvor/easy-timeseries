namespace Easy.TimeSeries;

using System.IO.Compression;
using System.Runtime.InteropServices;
using static Constants;

/// <summary>
/// Brotli-compressed raw storage for numeric columns whose values are uncorrelated between rows, where
/// XOR/delta ("Gorilla") encoding does not pay off (e.g. geographic coordinates). The column payload is the
/// Brotli image of the values' raw little-endian bytes; <see cref="ColumnHeader.Records"/> holds the value
/// count and <see cref="ColumnHeader.DataLength"/> the compressed byte length.
/// </summary>
internal static class RawColumn
{
    /// <summary>
    /// Compresses <paramref name="src"/> (raw little-endian value bytes) into a fresh column buffer prefixed
    /// by its <see cref="ColumnHeader"/> and returns the writer. The caller owns disposal of the writer.
    /// </summary>
    public static PooledArrayBufferWriter Compress(
        ReadOnlySpan<byte> src,
        int records,
        float growFactor,
        int maxAllowedBufferSize)
    {
        var maxCompressed = BrotliEncoder.GetMaxCompressedLength(src.Length);
        var bufferWriter = new PooledArrayBufferWriter(ColumnHeader.Size + maxCompressed, growFactor, maxAllowedBufferSize);
        bufferWriter.Advance(ColumnHeader.Size);

        var dest = bufferWriter.GetSpan(maxCompressed);
        if (!BrotliEncoder.TryCompress(src, dest, out var written, Brotli.Quality, Brotli.Window))
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
    public static void Decompress<T>(ReadOnlySpan<byte> payload, Span<T> destination)
        where T : struct
    {
        var destBytes = MemoryMarshal.AsBytes(destination);
        if (!BrotliDecoder.TryDecompress(payload, destBytes, out var written) || written != destBytes.Length)
        {
            throw new InvalidOperationException(
                "Brotli decompression of a raw column failed or produced an unexpected size.");
        }
    }
}
