namespace Easy.TimeSeries.Storage;

public sealed class FileStorage(FileInfo file) : IWriteStorage, IReadStorage, IDisposable
{
    private FileStream? fileStream;
    private bool disposed;

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
}
