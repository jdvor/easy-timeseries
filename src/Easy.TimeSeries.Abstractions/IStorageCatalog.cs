namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// A container of stored buffers addressed by logical path: a blob container or a local directory. Adds what a
/// single <see cref="IReadStorage"/> or <see cref="IWriteStorage"/> cannot provide: listing, versions, and
/// conditional writes and deletes.
/// </summary>
/// <remarks>
/// Paths are logical, always separated by <c>/</c>, relative to the container root, and never start with <c>/</c>.
/// The layout of paths inside a container (root, granularity, subject id) belongs to <c>PathBuilder</c>, not to the
/// catalog.
/// <para>
/// A version taken from <see cref="GetAsync"/> or <see cref="ListAsync"/> turns a write or delete into a
/// compare-and-swap: it succeeds only if nobody changed the path since the version was taken.
/// </para>
/// </remarks>
public interface IStorageCatalog
{
    /// <summary>Lists entries whose path starts with <paramref name="prefix"/>, in no guaranteed order.</summary>
    IAsyncEnumerable<StorageEntry> ListAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the entry at <paramref name="path"/> with its current version, or <c>null</c> if it does not exist.
    /// </summary>
    Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Opens <paramref name="path"/> for reading. A missing path fails on read, not here.</summary>
    IReadStorage OpenRead(string path);

    /// <summary>
    /// Opens <paramref name="path"/> for writing. The content becomes visible only on <c>CloseAsync</c>.
    /// With <paramref name="ifMatch"/>, <c>CloseAsync</c> commits only if the stored version still equals it;
    /// otherwise it throws <see cref="StorageVersionMismatchException"/> and the stored content stays untouched.
    /// Implementations should return an <see cref="IVersionedWriteStorage"/>, so callers learn the version they wrote.
    /// </summary>
    IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null);

    /// <summary>
    /// Deletes <paramref name="path"/>. With <paramref name="ifMatch"/>, only if the stored version still equals it.
    /// Returns <c>false</c> when nothing was deleted, because the path did not exist or the version differed.
    /// </summary>
    Task<bool> DeleteAsync(string path, StorageVersion? ifMatch = null, CancellationToken cancellationToken = default);
}

/// <summary>Opaque version of a stored entry, e.g. a blob ETag. Compare only for equality.</summary>
public readonly record struct StorageVersion(string Value);

/// <summary>One stored entry of an <see cref="IStorageCatalog"/>.</summary>
/// <param name="Path">Logical path, separated by <c>/</c>.</param>
/// <param name="LastModified">Time of the last committed write.</param>
/// <param name="Size">Content length in bytes.</param>
/// <param name="Version">Version for conditional writes and deletes.</param>
public readonly record struct StorageEntry(string Path, DateTimeOffset LastModified, long Size, StorageVersion Version);

/// <summary>A conditional write found the stored version changed since it was taken; nothing was written.</summary>
public class StorageVersionMismatchException : Exception
{
    public StorageVersionMismatchException(string path)
        : base($"'{path}' was changed by someone else since its version was taken.")
    {
        Path = path;
    }

    public StorageVersionMismatchException(string path, Exception innerException)
        : base($"'{path}' was changed by someone else since its version was taken.", innerException)
    {
        Path = path;
    }

    /// <summary>Logical path of the entry.</summary>
    public string Path { get; }
}
