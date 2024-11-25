#pragma warning disable SA1402
namespace Easy.TimeSeries;

public class NoMoreDataToReadException : InvalidOperationException
{
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

public class InvalidReadBufferException : InvalidOperationException
{
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
