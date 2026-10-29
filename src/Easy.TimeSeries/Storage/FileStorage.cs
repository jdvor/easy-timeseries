namespace Easy.TimeSeries.Storage;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// Local-file backing for both writing and reading a serialized buffer. Reads load the whole file into memory.
/// </summary>
/// <remarks>
/// Writes are atomic: data goes to a temp file next to the target (<c>.{name}.{guid}.tmp</c>), which
/// <see cref="CloseAsync"/> renames over the target. Readers never see a partially written file, and disposing
/// without <see cref="CloseAsync"/> deletes the temp file and leaves an existing target untouched. A crash can leave
/// an orphaned temp file behind; its name never ends in <c>.ets</c>, so path parsing and listings ignore it.
/// </remarks>
public sealed class FileStorage : IWriteStorage, IReadStorage, IDisposable
{
    /// <summary>Extension of the temp files written next to the target before the final rename.</summary>
    public const string TempFileExtension = ".tmp";

    private readonly FileInfo file;
    private readonly Action<FileInfo>? beforeCommit;
    private FileStream? fileStream;
    private string? tempPath;
    private bool disposed;

    public FileStorage(FileInfo file)
        : this(file, beforeCommit: null)
    {
    }

    public FileStorage(string filePath)
        : this(new FileInfo(filePath))
    {
    }

    /// <param name="beforeCommit">
    /// Runs just before the temp file replaces the target; throwing aborts the commit. Used for
    /// conditional writes.
    /// </param>
    internal FileStorage(FileInfo file, Action<FileInfo>? beforeCommit)
    {
        this.file = file;
        this.beforeCommit = beforeCommit;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (fileStream is null)
        {
            // The temp file lives in the target directory, so the final rename stays on one volume.
            var directory = file.Directory ?? throw new InvalidOperationException($"'{file.FullName}' has no directory.");
            directory.Create();
            tempPath = Path.Combine(directory.FullName, $".{file.Name}.{Guid.NewGuid():N}{TempFileExtension}");
            fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }

        await fileStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (fileStream is null || tempPath is null)
        {
            return;
        }

        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await fileStream.DisposeAsync().ConfigureAwait(false);
        fileStream = null;

        // Commits of one path are serialized, so a conditional check and its rename are never split by another commit.
        lock (PathLocks.For(file.FullName))
        {
            file.Refresh();
            beforeCommit?.Invoke(file);
            File.Move(tempPath, file.FullName, overwrite: true);
        }

        tempPath = null;
        file.Refresh();
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the file; without a preceding <see cref="CloseAsync"/> the write is aborted.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        fileStream?.Dispose();
        fileStream = null;
        DeleteTempFile();
    }

    /// <inheritdoc cref="Dispose"/>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (fileStream is not null)
        {
            await fileStream.DisposeAsync().ConfigureAwait(false);
            fileStream = null;
        }

        DeleteTempFile();
    }

    public override string ToString() => file.FullName;

    /// <summary>Whether <paramref name="fileName"/> looks like a temp file left by an aborted or crashed write.</summary>
    public static bool IsTempFileName(string fileName)
        => fileName.StartsWith('.') && fileName.EndsWith(TempFileExtension, StringComparison.Ordinal);

    private void DeleteTempFile()
    {
        if (tempPath is null)
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
            // Best effort: an orphaned temp file is harmless and removed by recovery.
        }
        catch (UnauthorizedAccessException)
        {
        }

        tempPath = null;
    }
}
