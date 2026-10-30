namespace Easy.TimeSeries.Storage;

using Easy.TimeSeries.Abstractions;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>
/// <see cref="IStorageCatalog"/> held in process memory, for tests and for staging. Versions are a counter;
/// conditional writes and deletes are atomic within the process.
/// </summary>
public sealed class InMemoryCatalog : IStorageCatalog
{
    private readonly Dictionary<string, Item> items = new(StringComparer.Ordinal);
    private readonly Lock sync = new();
    private readonly TimeProvider timeProvider;
    private long lastVersion;

    /// <param name="timeProvider">Source of <see cref="StorageEntry.LastModified"/>; defaults to the system clock.</param>
    public InMemoryCatalog(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Number of stored entries.</summary>
    public int Count
    {
        get
        {
            lock (sync)
            {
                return items.Count;
            }
        }
    }

    public async IAsyncEnumerable<StorageEntry> ListAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(prefix, allowPrefix: true, nameof(prefix));
        StorageEntry[] snapshot;
        lock (sync)
        {
            snapshot = items
                .Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(x => ToEntry(x.Key, x.Value))
                .ToArray();
        }

        foreach (var entry in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(path);
        lock (sync)
        {
            return Task.FromResult(items.TryGetValue(path, out var item) ? ToEntry(path, item) : (StorageEntry?)null);
        }
    }

    public IReadStorage OpenRead(string path)
    {
        StoragePath.Validate(path);
        return new ReadStorage(this, path);
    }

    public IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null)
    {
        StoragePath.Validate(path);
        return new WriteStorage(this, path, ifMatch);
    }

    public Task<bool> DeleteAsync(string path, StorageVersion? ifMatch = null, CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(path);
        lock (sync)
        {
            if (!items.TryGetValue(path, out var item) || (ifMatch is { } expected && ToVersion(item) != expected))
            {
                return Task.FromResult(false);
            }

            items.Remove(path);
            return Task.FromResult(true);
        }
    }

    private StorageVersion Commit(string path, byte[] data, StorageVersion? ifMatch)
    {
        lock (sync)
        {
            if (ifMatch is { } expected && (!items.TryGetValue(path, out var current) || ToVersion(current) != expected))
            {
                throw new StorageVersionMismatchException(path);
            }

            var item = new Item(data, ++lastVersion, timeProvider.GetUtcNow());
            items[path] = item;
            return ToVersion(item);
        }
    }

    private ReadOnlyMemory<byte> Read(string path)
    {
        lock (sync)
        {
            return items.TryGetValue(path, out var item)
                ? item.Data
                : throw new FileNotFoundException($"'{path}' does not exist in the catalog.", path);
        }
    }

    private static StorageEntry ToEntry(string path, Item item)
        => new(path, item.LastModified, item.Data.Length, ToVersion(item));

    private static StorageVersion ToVersion(Item item)
        => new(item.Version.ToString(CultureInfo.InvariantCulture));

    private sealed record Item(byte[] Data, long Version, DateTimeOffset LastModified);

    private sealed class ReadStorage(InMemoryCatalog catalog, string path) : IReadStorage
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken)
            => Task.FromResult(catalog.Read(path));

        public override string ToString() => path;
    }

    private sealed class WriteStorage(InMemoryCatalog catalog, string path, StorageVersion? ifMatch) : IVersionedWriteStorage
    {
        private readonly ArrayBufferWriter<byte> buffer = new();
        private bool closed;

        public StorageVersion? CommittedVersion { get; private set; }

        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (closed)
            {
                throw new InvalidOperationException("Cannot write to storage that has already been closed.");
            }

            buffer.Write(data.Span);
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (closed)
            {
                return Task.CompletedTask;
            }

            closed = true;

            // Like the file and blob storages: nothing written, nothing stored.
            if (buffer.WrittenCount > 0)
            {
                CommittedVersion = catalog.Commit(path, buffer.WrittenSpan.ToArray(), ifMatch);
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public override string ToString() => path;
    }
}
