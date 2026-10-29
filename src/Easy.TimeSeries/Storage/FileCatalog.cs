namespace Easy.TimeSeries.Storage;

using Easy.TimeSeries.Abstractions;
using System.Runtime.CompilerServices;

/// <summary>
/// <see cref="IStorageCatalog"/> over a local directory; logical paths map to files below it. Writes go through
/// <see cref="FileStorage"/>, so they are atomic.
/// </summary>
/// <remarks>
/// The version of a file is its last-write time and size. Conditional writes and deletes compare it just before
/// the final rename or delete. Within a process, the check and the rename or delete are serialized per file, so they
/// are atomic. Across processes they are not: run at most one process that writes conditionally per directory.
/// </remarks>
public sealed class FileCatalog : IStorageCatalog
{
    private readonly DirectoryInfo directory;
    private readonly string rootPath;

    /// <summary>Catalog over <paramref name="directory"/>; logical paths map to files below it.</summary>
    public FileCatalog(DirectoryInfo directory)
    {
        Expect.NotNull(directory);
        this.directory = directory;
        rootPath = Path.TrimEndingDirectorySeparator(directory.FullName);
    }

    /// <summary>The directory the logical paths are relative to.</summary>
    public DirectoryInfo Directory => directory;

    public async IAsyncEnumerable<StorageEntry> ListAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StoragePath.Validate(prefix, allowPrefix: true, nameof(prefix));

        // Walk only the deepest directory the prefix fully names, then filter by the whole prefix.
        var lastSeparator = prefix.LastIndexOf(StoragePath.Separator);
        var start = lastSeparator < 0 ? rootPath : ToFullPath(prefix[..lastSeparator]);
        if (!System.IO.Directory.Exists(start))
        {
            yield break;
        }

        // Nothing is skipped by attribute: on Unix every dot-prefixed name counts as hidden. Temp files are skipped by name.
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var fullPath in System.IO.Directory.EnumerateFiles(start, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FileStorage.IsTempFileName(Path.GetFileName(fullPath)))
            {
                continue;
            }

            var path = ToLogicalPath(fullPath);
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var file = new FileInfo(fullPath);
            if (file.Exists)
            {
                yield return ToEntry(path, file);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public Task<StorageEntry?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(ToFullPath(path));
        return Task.FromResult(file.Exists ? ToEntry(path, file) : (StorageEntry?)null);
    }

    public IReadStorage OpenRead(string path) => new FileStorage(ToFullPath(path));

    public IWriteStorage OpenWrite(string path, StorageVersion? ifMatch = null)
    {
        var file = new FileInfo(ToFullPath(path));
        if (ifMatch is not { } expected)
        {
            return new FileStorage(file);
        }

        return new FileStorage(file, beforeCommit: f =>
        {
            if (!f.Exists || FileStorage.VersionOf(f) != expected)
            {
                throw new StorageVersionMismatchException(path);
            }
        });
    }

    public Task<bool> DeleteAsync(string path, StorageVersion? ifMatch = null, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(ToFullPath(path));
        lock (PathLocks.For(file.FullName))
        {
            if (!file.Exists || (ifMatch is { } expected && FileStorage.VersionOf(file) != expected))
            {
                return Task.FromResult(false);
            }

            try
            {
                file.Delete();
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Deletes temp files left behind by crashed writes (see <see cref="FileStorage"/>) whose last write is older
    /// than <paramref name="olderThan"/>. The age guard keeps writes still in progress intact.
    /// </summary>
    /// <returns>Number of temp files removed.</returns>
    public Task<int> RemoveOrphanedTempFilesAsync(
        TimeSpan olderThan,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(rootPath))
        {
            return Task.FromResult(0);
        }

        var cutoff = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime - olderThan;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        var removed = 0;
        foreach (var fullPath in System.IO.Directory.EnumerateFiles(rootPath, $"*{FileStorage.TempFileExtension}", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = new FileInfo(fullPath);
            if (!FileStorage.IsTempFileName(file.Name) || file.LastWriteTimeUtc >= cutoff)
            {
                continue;
            }

            try
            {
                file.Delete();
                ++removed;
            }
            catch (IOException)
            {
                // Still open by a writer, or already gone.
            }
        }

        return Task.FromResult(removed);
    }

    public override string ToString() => directory.FullName;

    private static StorageEntry ToEntry(string path, FileInfo file)
        => new(path, new DateTimeOffset(file.LastWriteTimeUtc), file.Length, FileStorage.VersionOf(file));

    private string ToFullPath(string path)
    {
        StoragePath.Validate(path);
        return Path.Join(rootPath, path.Replace(StoragePath.Separator, Path.DirectorySeparatorChar));
    }

    private string ToLogicalPath(string fullPath)
    {
        // Not a slice past the root: a file system root such as "/" keeps its trailing separator.
        var relative = Path.GetRelativePath(rootPath, fullPath);
        return Path.DirectorySeparatorChar == StoragePath.Separator
            ? relative
            : relative.Replace(Path.DirectorySeparatorChar, StoragePath.Separator);
    }
}
