namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;
using System.IO;

public sealed class FileCatalogTests : StorageCatalogContract, IDisposable
{
    private readonly DirectoryInfo dir = new(Path.Combine(Path.GetTempPath(), $"ets-catalog-{Guid.NewGuid():N}"));

    public FileCatalogTests()
    {
        Catalog = new FileCatalog(dir);
    }

    protected override IStorageCatalog Catalog { get; }

    [Fact]
    public async Task Temp_files_are_not_listed()
    {
        await WriteAsync("a/x.ets", [1]);
        await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "a", ".x.ets.0123456789abcdef0123456789abcdef.tmp"), [1], Ct);

        Assert.Equal(["a/x.ets"], await ListPathsAsync("a/"));
    }

    [Fact]
    public async Task Files_below_dot_directories_are_listed()
    {
        await WriteAsync(".cache/raw/x.ets", [1]);

        Assert.Equal([".cache/raw/x.ets"], await ListPathsAsync(string.Empty));
    }

    [Fact]
    public async Task Hidden_files_not_shaped_like_temp_files_are_kept()
    {
        await WriteAsync("cfg/.settings.tmp", [1]);
        File.SetLastWriteTimeUtc(Path.Combine(dir.FullName, "cfg", ".settings.tmp"), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0, await ((FileCatalog)Catalog).RemoveOrphanedTempFilesAsync(TimeSpan.FromHours(1), cancellationToken: Ct));
        Assert.Equal(["cfg/.settings.tmp"], await ListPathsAsync("cfg/"));
    }

    [Fact]
    public async Task Logical_paths_map_below_the_directory()
    {
        await WriteAsync("raw/2026/s1.ets", [1]);
        Assert.True(File.Exists(Path.Combine(dir.FullName, "raw", "2026", "s1.ets")));
    }

    [Fact]
    public async Task Listing_a_missing_root_directory_is_empty()
    {
        var catalog = new FileCatalog(new DirectoryInfo(Path.Combine(dir.FullName, "missing")));
        await foreach (var _ in catalog.ListAsync(string.Empty, Ct))
        {
            Assert.Fail("nothing expected");
        }
    }

    [Fact]
    public async Task Orphaned_temp_files_older_than_the_threshold_are_removed()
    {
        await WriteAsync("a/b/x.ets", [1]);
        var old = Path.Combine(dir.FullName, "a", "b", ".x.ets.0123456789abcdef0123456789abcdef.tmp");
        var fresh = Path.Combine(dir.FullName, "a", ".y.ets.fedcba9876543210fedcba9876543210.tmp");
        await File.WriteAllBytesAsync(old, [1], Ct);
        await File.WriteAllBytesAsync(fresh, [1], Ct);
        File.SetLastWriteTimeUtc(old, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var removed = await ((FileCatalog)Catalog).RemoveOrphanedTempFilesAsync(TimeSpan.FromHours(1), cancellationToken: Ct);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(Path.Combine(dir.FullName, "a", "b", "x.ets")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/abs/x.ets")]
    [InlineData("a/../x.ets")]
    [InlineData("a/./x.ets")]
    [InlineData("a//x.ets")]
    [InlineData("a\\x.ets")]
    public void Invalid_logical_paths_are_rejected(string path)
    {
        Assert.Throws<ArgumentException>(() => Catalog.OpenWrite(path));
    }

    public void Dispose()
    {
        if (dir.Exists)
        {
            dir.Delete(recursive: true);
        }
    }
}

public sealed class InMemoryCatalogTests : StorageCatalogContract
{
    protected override IStorageCatalog Catalog { get; } = new InMemoryCatalog();

    [Fact]
    public async Task Reading_a_missing_path_throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => Catalog.OpenRead("a/missing.ets").ReadAsync(Ct));
    }
}
