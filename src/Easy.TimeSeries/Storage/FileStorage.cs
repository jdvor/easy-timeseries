namespace Easy.TimeSeries.Storage;

using Easy.TimeSeries.Abstractions;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Local-file backing for both writing and reading a serialized buffer. Reads load the whole file into memory.
/// </summary>
/// <remarks>
/// Writes are atomic: data goes to a temp file next to the target (<c>.{name}.{guid}.tmp</c>, the name cut to
/// <see cref="TempNameLength"/> characters so the temp file fits the file system's name limit), which
/// <see cref="CloseAsync"/> renames over the target. Readers never see a partially written file, and disposing
/// without <see cref="CloseAsync"/> deletes the temp file and leaves an existing target untouched. A crash can leave
/// an orphaned temp file behind; its name never ends in <c>.ets</c>, so path parsing and listings ignore it.
/// </remarks>
public sealed partial class FileStorage : IVersionedWriteStorage, IReadStorage, IDisposable
{
    /// <summary>Extension of the temp files written next to the target before the final rename.</summary>
    public const string TempFileExtension = ".tmp";

    /// <summary>
    /// Characters of the target name kept in a temp file name: at most 1 + 4 x 32 + 1 + 32 + 4 = 166 bytes in UTF-8,
    /// well below the 255-byte name limit of common file systems, however long the target name is.
    /// </summary>
    private const int TempNameLength = 32;

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

    /// <summary>Last-write time and size of the committed file; <c>null</c> before a successful close.</summary>
    public StorageVersion? CommittedVersion { get; private set; }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (fileStream is null)
        {
            // The temp file lives in the target directory, so the final rename stays on one volume.
            var directory = file.Directory ?? throw new InvalidOperationException($"'{file.FullName}' has no directory.");
            directory.Create();
            tempPath = Path.Combine(directory.FullName, $".{TempNamePrefix(file.Name)}.{Guid.NewGuid():N}{TempFileExtension}");
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

            // A rename keeps last-write time and size, so the temp file already carries the committed version.
            var version = VersionOf(new FileInfo(tempPath));
            File.Move(tempPath, file.FullName, overwrite: true);
            CommittedVersion = version;
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

    /// <summary>
    /// Whether <paramref name="fileName"/> has the shape of a temp file written by this class,
    /// <c>.{name}.{32 hex digits}.tmp</c>, i.e. one left by an aborted or crashed write.
    /// </summary>
    public static bool IsTempFileName(string fileName) => TempFileNameRegex().IsMatch(fileName);

    /// <summary>Version of a local file: its last-write time and size.</summary>
    internal static StorageVersion VersionOf(FileInfo file)
        => new(string.Create(CultureInfo.InvariantCulture, $"{file.LastWriteTimeUtc.Ticks:x}-{file.Length:x}"));

    // Never splits a surrogate pair, so the temp name stays valid UTF-16.
    private static string TempNamePrefix(string name)
        => name.Length <= TempNameLength
            ? name
            : name[..(char.IsHighSurrogate(name[TempNameLength - 1]) ? TempNameLength - 1 : TempNameLength)];

    [GeneratedRegex(@"^\..*\.[0-9a-f]{32}\.tmp\z", RegexOptions.CultureInvariant)]
    private static partial Regex TempFileNameRegex();

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
