namespace Easy.TimeSeries.Aggregation.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;

/// <summary>An in-memory catalog whose operations tests can intercept, e.g. to fail writes or interleave a writer.</summary>
internal sealed class TestCatalog(TimeProvider? clock = null) : IStorageCatalog
{
    public InMemoryCatalog Inner { get; } = new(clock);

    /// <summary>Called before <c>OpenWrite</c>; throw to fail the write.</summary>
    public Func<string, Task>? BeforeOpenWrite { get; set; }

    /// <summary>Called before <c>GetAsync</c>.</summary>
    public Func<string, Task>? BeforeGet { get; set; }

    /// <summary>Prefixes of every <c>ListAsync</c> call, in call order.</summary>
    public List<string> Listed { get; } = [];

    public IAsyncEnumerable<StorageEntry> ListAsync(string prefix, CancellationToken cancellationToken = default)
    {
        lock (Listed)
        {
            Listed.Add(prefix);
        }

        return Inner.ListAsync(prefix, cancellationToken);
    }

    public async Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (BeforeGet is not null)
        {
            await BeforeGet(path);
        }

        return await Inner.GetAsync(path, cancellationToken);
    }

    public IReadStorage OpenRead(string path) => Inner.OpenRead(path);

    public IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null)
    {
        BeforeOpenWrite?.Invoke(path).GetAwaiter().GetResult();
        return Inner.OpenWrite(path, ifMatch);
    }

    public Task<bool> DeleteAsync(string path, StorageVersion? ifMatch = null, CancellationToken cancellationToken = default)
        => Inner.DeleteAsync(path, ifMatch, cancellationToken);

    public bool Exists(string path) => Inner.GetAsync(path).GetAwaiter().GetResult() is not null;

    public string[] Paths(string prefix = "")
        => [.. Inner.ListAsync(prefix).ToBlockingEnumerable().Select(x => x.Path).Order(StringComparer.Ordinal)];

    public async Task WriteAsync(string path, Writer writer)
    {
        await using var storage = Inner.OpenWrite(path);
        using (writer)
        {
            await writer.WriteToAsync(storage);
        }
    }
}
