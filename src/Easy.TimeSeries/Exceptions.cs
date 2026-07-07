#pragma warning disable SA1402
namespace Easy.TimeSeries;

/// <summary>Thrown when a decoder is asked for another value but the column's bit stream is exhausted.</summary>
public class NoMoreDataToReadException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public NoMoreDataToReadException()
    {
    }

    public NoMoreDataToReadException(string message)
        : base(message)
    {
    }

    public NoMoreDataToReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a buffer cannot be read as a valid file: bad magic bytes, an unsupported version, or malformed layout.</summary>
public class InvalidReadBufferException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public InvalidReadBufferException()
    {
    }

    public InvalidReadBufferException(string message)
        : base(message)
    {
    }

    public InvalidReadBufferException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

#pragma warning restore SA1402
