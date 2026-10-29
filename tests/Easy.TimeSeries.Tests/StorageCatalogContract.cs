namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// Behavior every <see cref="IStorageCatalog"/> implementation must share. Derive once per implementation; the
/// Azure catalog tests link this file into their own project.
/// </summary>
public abstract class StorageCatalogContract
{
    /// <summary>A catalog over an empty container, unique per test.</summary>
    protected abstract IStorageCatalog Catalog { get; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Written_content_reads_back_after_close()
    {
        await WriteAsync("raw/2026/10/25/s1.ets", [1, 2, 3]);

        var read = await Catalog.OpenRead("raw/2026/10/25/s1.ets").ReadAsync(Ct);
        Assert.Equal([1, 2, 3], read.ToArray());
    }

    [Fact]
    public async Task Content_is_not_visible_before_close()
    {
        await using var storage = Catalog.OpenWrite("a/pending.ets");
        await storage.WriteAsync(new byte[] { 1 }, Ct);

        Assert.Null(await Catalog.GetAsync("a/pending.ets", Ct));
        await storage.CloseAsync(Ct);
        Assert.NotNull(await Catalog.GetAsync("a/pending.ets", Ct));
    }

    [Fact]
    public async Task Disposing_without_close_keeps_existing_content()
    {
        await WriteAsync("a/kept.ets", [9]);

        await using (var storage = Catalog.OpenWrite("a/kept.ets"))
        {
            await storage.WriteAsync(new byte[] { 1, 2 }, Ct);
        }

        var read = await Catalog.OpenRead("a/kept.ets").ReadAsync(Ct);
        Assert.Equal([9], read.ToArray());
    }

    [Fact]
    public async Task Get_returns_null_for_a_missing_path()
    {
        Assert.Null(await Catalog.GetAsync("a/missing.ets", Ct));
    }

    [Fact]
    public async Task Get_returns_size_and_a_version_that_changes_on_rewrite()
    {
        await WriteAsync("a/x.ets", [1, 2, 3]);
        var first = await Catalog.GetAsync("a/x.ets", Ct);

        Assert.NotNull(first);
        Assert.Equal("a/x.ets", first.Value.Path);
        Assert.Equal(3, first.Value.Size);
        Assert.False(string.IsNullOrEmpty(first.Value.Version.Value));

        await WriteAsync("a/x.ets", [4, 5, 6]);
        var second = await Catalog.GetAsync("a/x.ets", Ct);

        Assert.NotEqual(first.Value.Version, second!.Value.Version);
    }

    [Fact]
    public async Task Close_reports_the_committed_version()
    {
        await using var storage = Catalog.OpenWrite("a/x.ets");
        var versioned = Assert.IsAssignableFrom<IVersionedWriteStorage>(storage);
        await storage.WriteAsync(new byte[] { 1, 2 }, Ct);
        Assert.Null(versioned.CommittedVersion);

        await storage.CloseAsync(Ct);

        Assert.NotNull(versioned.CommittedVersion);
        Assert.Equal((await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version, versioned.CommittedVersion);
    }

    [Fact]
    public async Task Parallel_conditional_writes_with_one_version_commit_once()
    {
        await WriteAsync("a/x.ets", [1]);
        var version = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
        {
            try
            {
                await WriteAsync("a/x.ets", [(byte)i, 0], version);
                return true;
            }
            catch (StorageVersionMismatchException)
            {
                return false;
            }
        })));

        Assert.Single(outcomes, x => x);
    }

    [Fact]
    public async Task Parallel_conditional_deletes_with_one_version_delete_once()
    {
        await WriteAsync("a/x.ets", [1]);
        var version = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => Catalog.DeleteAsync("a/x.ets", version, Ct))));

        Assert.Single(outcomes, x => x);
    }

    [Fact]
    public async Task List_returns_only_paths_under_the_prefix()
    {
        await WriteAsync("raw/2026/10/25/14/s1.ets", [1]);
        await WriteAsync("raw/2026/10/25/15/s1.ets", [1]);
        await WriteAsync("raw/2026/10/26/00/s1.ets", [1]);
        await WriteAsync("agg-1m-daily/2026/10/25/s1.ets", [1]);

        Assert.Equal(
            ["raw/2026/10/25/14/s1.ets", "raw/2026/10/25/15/s1.ets"],
            await ListPathsAsync("raw/2026/10/25/"));
        Assert.Equal(
            ["raw/2026/10/25/14/s1.ets", "raw/2026/10/25/15/s1.ets", "raw/2026/10/26/00/s1.ets"],
            await ListPathsAsync("raw/2026/10/2"));
        Assert.Equal(4, (await ListPathsAsync(string.Empty)).Length);
        Assert.Empty(await ListPathsAsync("nothing/"));
    }

    [Fact]
    public async Task List_entries_carry_the_same_version_as_get()
    {
        await WriteAsync("a/x.ets", [1, 2]);

        var listed = await ListAsync("a/");
        var entry = Assert.Single(listed);
        Assert.Equal(await Catalog.GetAsync("a/x.ets", Ct), entry);
    }

    [Fact]
    public async Task Conditional_write_with_current_version_commits()
    {
        await WriteAsync("a/x.ets", [1]);
        var version = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;

        await WriteAsync("a/x.ets", [2], version);

        var read = await Catalog.OpenRead("a/x.ets").ReadAsync(Ct);
        Assert.Equal([2], read.ToArray());
    }

    [Fact]
    public async Task Conditional_write_with_stale_version_throws_and_keeps_content()
    {
        await WriteAsync("a/x.ets", [1]);
        var stale = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;
        await WriteAsync("a/x.ets", [2, 2]);

        var ex = await Assert.ThrowsAsync<StorageVersionMismatchException>(() => WriteAsync("a/x.ets", [3], stale));

        Assert.Equal("a/x.ets", ex.Path);
        var read = await Catalog.OpenRead("a/x.ets").ReadAsync(Ct);
        Assert.Equal([2, 2], read.ToArray());
    }

    [Fact]
    public async Task Conditional_write_to_a_deleted_path_throws()
    {
        await WriteAsync("a/x.ets", [1]);
        var version = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;
        await Catalog.DeleteAsync("a/x.ets", cancellationToken: Ct);

        await Assert.ThrowsAsync<StorageVersionMismatchException>(() => WriteAsync("a/x.ets", [3], version));
        Assert.Null(await Catalog.GetAsync("a/x.ets", Ct));
    }

    [Fact]
    public async Task Delete_removes_an_existing_path()
    {
        await WriteAsync("a/x.ets", [1]);

        Assert.True(await Catalog.DeleteAsync("a/x.ets", cancellationToken: Ct));
        Assert.Null(await Catalog.GetAsync("a/x.ets", Ct));
        Assert.False(await Catalog.DeleteAsync("a/x.ets", cancellationToken: Ct));
    }

    [Fact]
    public async Task Conditional_delete_respects_the_version()
    {
        await WriteAsync("a/x.ets", [1]);
        var stale = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;
        await WriteAsync("a/x.ets", [2, 2]);
        var current = (await Catalog.GetAsync("a/x.ets", Ct))!.Value.Version;

        Assert.False(await Catalog.DeleteAsync("a/x.ets", stale, Ct));
        Assert.NotNull(await Catalog.GetAsync("a/x.ets", Ct));
        Assert.True(await Catalog.DeleteAsync("a/x.ets", current, Ct));
        Assert.False(await Catalog.DeleteAsync("a/x.ets", current, Ct));
    }

    [Fact]
    public async Task Writer_output_round_trips_through_the_catalog()
    {
        DateTime[] times = [new(2026, 10, 25, 14, 0, 0, DateTimeKind.Utc)];
        await using (var storage = Catalog.OpenWrite("raw/s1.ets"))
        {
            using var writer = new Writer(1).AddTimeOrdered(times, "ts").AddDouble([1.5], "v");
            await writer.WriteToAsync(storage, Ct);
        }

        var result = await Reader.ReadFromAsync(
            Catalog.OpenRead("raw/s1.ets"), new ReflectionBasedMaterializer<Row>(), null, Ct);
        var row = Assert.Single(result);
        Assert.Equal(times[0], row.Timestamp);
        Assert.Equal(1.5d, row.Value);
    }

    protected async Task WriteAsync(string path, byte[] data, StorageVersion? ifMatch = null)
    {
        await using var storage = Catalog.OpenWrite(path, ifMatch);
        await storage.WriteAsync(data, Ct);
        await storage.CloseAsync(Ct);
    }

    protected async Task<StorageEntry[]> ListAsync(string prefix)
    {
        var entries = new List<StorageEntry>();
        await foreach (var entry in Catalog.ListAsync(prefix, Ct))
        {
            entries.Add(entry);
        }

        return [.. entries];
    }

    protected async Task<string[]> ListPathsAsync(string prefix)
        => [.. (await ListAsync(prefix)).Select(x => x.Path).Order(StringComparer.Ordinal)];

    private sealed class Row
    {
        [Column(0)]
        public DateTime Timestamp { get; init; }

        [Column(1)]
        public double Value { get; init; }
    }
}
