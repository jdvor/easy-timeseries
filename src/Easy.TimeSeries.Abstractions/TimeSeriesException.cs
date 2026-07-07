namespace Easy.TimeSeries.Abstractions;

/// <summary>
/// Base exception for errors raised while mapping between DTOs and the columnar format, such as an unmapped column
/// index or an unsupported property type. Storage- and buffer-level failures use the exception types in the core
/// library instead.
/// </summary>
public class TimeSeriesException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public TimeSeriesException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a descriptive message and the underlying cause.</summary>
    public TimeSeriesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>A column present in the file has no matching property on the target DTO.</summary>
    public static TimeSeriesException UnmappedColumnIndex(int columnIndex, Type classType)
        => new($"Unmapped column index {columnIndex} for type {classType.Name}");

    /// <summary>A decoded value cannot be assigned to the mapped property because its type is not supported.</summary>
    public static TimeSeriesException UnsupportedHydration(int columnIndex, Type classType, Type propertyType)
        => new($"Writing {propertyType.Name} value into column {columnIndex} of type {classType.Name} is not supported.");
}
