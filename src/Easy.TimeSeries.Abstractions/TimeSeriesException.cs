namespace Easy.TimeSeries.Abstractions;

public class TimeSeriesException : Exception
{
    public TimeSeriesException(string message)
        : base(message)
    {
    }

    public TimeSeriesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static TimeSeriesException UnmappedColumnIndex(int columnIndex, Type classType)
        => new($"Unmapped column index {columnIndex} for type {classType.Name}");

    public static TimeSeriesException UnsupportedHydration(int columnIndex, Type classType, Type propertyType)
        => new($"Writing {propertyType.Name} value into column {columnIndex} of type {classType.Name} is not supported.");
}
