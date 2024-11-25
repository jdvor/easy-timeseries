namespace Easy.TimeSeries.Tests;

using System.Buffers.Binary;

public class PooledArrayBufferWriterTests
{
    [Fact]
    public void CorrectResizingOfUnderlyingBuffer()
    {
        const int start = 1;
        const int count = 3000;
        const int valueSize = sizeof(int);

        var sut = new PooledArrayBufferWriter(sizeHint: 1, growFactor: 1f, maxAllowedSize: int.MaxValue);
        foreach (var i32 in Enumerable.Range(start, count))
        {
            var span = sut.GetSpan(valueSize);
            BinaryPrimitives.WriteInt32LittleEndian(span, i32);
            sut.Advance(valueSize);
        }

        Assert.True(sut.ResizedCount > 0, "not resized; initial buffer was big enough that resizing was not required");

        var written = sut.WrittenSpan;
        Assert.True(written.Length == count * valueSize, "written span size is incorrect");

        for (int i = 0; i < count; i++)
        {
            var from = i * valueSize;
            var i32Span = written.Slice(from, valueSize);
            var actual = BinaryPrimitives.ReadInt32LittleEndian(i32Span);
            var expected = start + i;
            Assert.Equal(expected, actual);
        }

        // Reading all sequential numbers exactly as they have been written
        // ensures that during resizing nothing has been messed up.
    }
}
