namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;
using System.IO;

public class FileStorageTests : IDisposable
{
    private readonly string dir;
    private bool disposed;

    public FileStorageTests()
    {
        dir = Path.Combine(Path.GetTempPath(), $"ets-filestorage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
    }

    [Fact]
    public async Task Write_then_read_round_trips()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(dir, "roundtrip.ets");
        var rows = Rows(64);

        using (var storage = new FileStorage(path))
        {
            using var writer = BuildWriter(rows);
            await writer.WriteToAsync(storage, ct);
        }

        using var readStorage = new FileStorage(path);
        var result = await Reader.ReadFromAsync(readStorage, new ReflectionBasedMaterializer<Row>(), null, ct);

        Assert.Equal(rows.Length, result.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(rows[i].Timestamp, result[i].Timestamp);
            Assert.Equal(rows[i].Value, result[i].Value);
        }
    }

    /// <summary>
    /// Regression test: FileInfo.OpenWrite is OpenOrCreate, which left the tail of a previously longer file in
    /// place. The reader still returned the right rows (the header drives slicing), so the only visible symptom
    /// was a file that never shrank - hence the assertion on length, not just on row count.
    /// </summary>
    [Fact]
    public async Task Overwriting_a_longer_file_truncates_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(dir, "overwrite.ets");

        using (var storage = new FileStorage(path))
        {
            using var writer = BuildWriter(Rows(500));
            await writer.WriteToAsync(storage, ct);
        }

        var longLength = new FileInfo(path).Length;

        var shortRows = Rows(3);
        using (var storage = new FileStorage(path))
        {
            using var writer = BuildWriter(shortRows);
            await writer.WriteToAsync(storage, ct);
        }

        var shortLength = new FileInfo(path).Length;
        Assert.True(
            shortLength < longLength,
            $"file was not truncated; {longLength} bytes before, {shortLength} bytes after rewriting with fewer rows");

        using var readStorage = new FileStorage(path);
        var result = await Reader.ReadFromAsync(readStorage, new ReflectionBasedMaterializer<Row>(), null, ct);

        Assert.Equal(shortRows.Length, result.Length);
        for (var i = 0; i < shortRows.Length; i++)
        {
            Assert.Equal(shortRows[i].Timestamp, result[i].Timestamp);
            Assert.Equal(shortRows[i].Value, result[i].Value);
        }
    }

    [Fact]
    public async Task Writing_creates_the_file_when_it_does_not_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(dir, "created.ets");
        Assert.False(File.Exists(path));

        using var storage = new FileStorage(path);
        using var writer = BuildWriter(Rows(4));
        await writer.WriteToAsync(storage, ct);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void ToString_returns_the_full_path()
    {
        var path = Path.Combine(dir, "named.ets");
        using var storage = new FileStorage(path);
        Assert.Equal(path, storage.ToString());
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static Writer BuildWriter(Row[] rows)
        => new Writer(rows.Length)
            .AddTimeOrdered(rows.Select(x => x.Timestamp), nameof(Row.Timestamp))
            .AddDouble(rows.Select(x => x.Value), nameof(Row.Value));

    private static Row[] Rows(int count)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new Row[count];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new Row { Timestamp = start.AddSeconds(i), Value = 100.5 + i };
        }

        return rows;
    }

    private sealed class Row
    {
        [Column(0)]
        public DateTime Timestamp { get; init; }

        [Column(1)]
        public double Value { get; init; }
    }
}

public class InMemoryStorageTests
{
    [Fact]
    public async Task Write_then_read_returns_what_was_written()
    {
        var ct = TestContext.Current.CancellationToken;
        using var storage = new InMemoryStorage();
        byte[] payload = [1, 2, 3, 4, 5];

        await storage.WriteAsync(payload, ct);

        Assert.Equal(payload.Length, storage.Size);
        var read = await storage.ReadAsync(ct);
        Assert.True(read.Span.SequenceEqual(payload));
    }

    [Fact]
    public async Task Successive_writes_append()
    {
        var ct = TestContext.Current.CancellationToken;
        using var storage = new InMemoryStorage();

        await storage.WriteAsync(new byte[] { 1, 2 }, ct);
        await storage.WriteAsync(new byte[] { 3, 4 }, ct);

        Assert.Equal(4, storage.Size);
        var read = await storage.ReadAsync(ct);
        Assert.True(read.Span.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public async Task Reading_before_anything_is_written_throws_with_a_message()
    {
        var ct = TestContext.Current.CancellationToken;
        using var storage = new InMemoryStorage();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.ReadAsync(ct));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task Writing_after_close_throws_with_a_message()
    {
        var ct = TestContext.Current.CancellationToken;
        using var storage = new InMemoryStorage();

        await storage.WriteAsync(new byte[] { 1 }, ct);
        await storage.CloseAsync(ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.WriteAsync(new byte[] { 2 }, ct));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task Round_trips_a_written_time_series()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var timestamps = Enumerable.Range(0, 32).Select(i => start.AddSeconds(i)).ToArray();
        var values = Enumerable.Range(0, 32).Select(i => (double)i).ToArray();

        using var storage = new InMemoryStorage();
        using (var writer = new Writer(timestamps.Length).AddTimeOrdered(timestamps, "ts").AddDouble(values, "v"))
        {
            await writer.WriteToAsync(storage, ct);
        }

        var result = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Sample>(), null, ct);

        Assert.Equal(timestamps.Length, result.Length);
        for (var i = 0; i < timestamps.Length; i++)
        {
            Assert.Equal(timestamps[i], result[i].Ts);
            Assert.Equal(values[i], result[i].V);
        }
    }

    private sealed class Sample
    {
        [Column(0)]
        public DateTime Ts { get; init; }

        [Column(1)]
        public double V { get; init; }
    }
}
