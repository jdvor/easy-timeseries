namespace Easy.TimeSeries.Storage;

using Easy.TimeSeries.Abstractions;

/// <summary>
/// Local-file backing for both writing and reading a serialized buffer. Writes stream to the file as columns are
/// flushed; reads load the whole file into memory. Dispose to release the underlying stream.
/// </summary>
public sealed class FileStorage : IWriteStorage, IReadStorage, IDisposable
{
    private readonly FileInfo file;
    private FileStream? fileStream;
    private bool disposed;

    public FileStorage(FileInfo file)
    {
        this.file = file;
    }

    public FileStorage(string filePath)
        : this(new FileInfo(filePath))
    {
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        fileStream ??= file.OpenWrite();
        await fileStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        fileStream?.Close();
        return Task.CompletedTask;
    }

    public async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        fileStream?.Dispose();
    }

    public override string ToString() => file.FullName;
}
