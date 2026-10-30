namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// A write storage that reports the version its own commit produced. Storages opened by the built-in catalogs
/// implement it.
/// </summary>
/// <remarks>
/// A version read with <see cref="IStorageCatalog.GetAsync"/> after the commit may already belong to somebody else's
/// write; <see cref="CommittedVersion"/> always belongs to the bytes written through this storage.
/// </remarks>
public interface IVersionedWriteStorage : IWriteStorage
{
    /// <summary>
    /// Version of the content committed by <see cref="IWriteStorage.CloseAsync"/>; <c>null</c> before a successful
    /// close, and when nothing was written.
    /// </summary>
    StorageVersion? CommittedVersion { get; }
}
