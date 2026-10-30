namespace Easy.TimeSeries.Aggregation;

/// <summary>The nine CLR types raw columns decode to, and the mapping from column types.</summary>
internal static class ClrTypes
{
    public const string SupportedNames = "float, double, decimal, int, long, DateTime, TimeSpan, bool, string";

    public static bool IsSupported(Type type)
        => type == typeof(float) || type == typeof(double) || type == typeof(decimal)
           || type == typeof(int) || type == typeof(long)
           || type == typeof(DateTime) || type == typeof(TimeSpan)
           || type == typeof(bool) || type == typeof(string);

    /// <summary>CLR type the core reader hydrates for <paramref name="valueType"/>.</summary>
    public static Type Of(ColumnValueType valueType)
        => valueType switch
        {
            ColumnValueType.DateTimeOrdered or ColumnValueType.DateTimeUnordered => typeof(DateTime),
            ColumnValueType.TimeSpan => typeof(TimeSpan),
            ColumnValueType.Float or ColumnValueType.FloatRaw or ColumnValueType.ScaledNumber32 => typeof(float),
            ColumnValueType.Double or ColumnValueType.DoubleRaw or ColumnValueType.ScaledNumber64 => typeof(double),
            ColumnValueType.Decimal => typeof(decimal),
            ColumnValueType.Int32 or ColumnValueType.Int32Raw => typeof(int),
            ColumnValueType.Int64 or ColumnValueType.Int64Raw => typeof(long),
            ColumnValueType.Bool => typeof(bool),
            ColumnValueType.Category => typeof(string),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType), valueType, "Unknown column value type."),
        };

    /// <summary>Whether <paramref name="type"/> is one of the numeric types.</summary>
    public static bool IsNumeric(Type type)
        => type == typeof(float) || type == typeof(double) || type == typeof(decimal)
           || type == typeof(int) || type == typeof(long);

    /// <summary>Encodings a result of <paramref name="resultType"/> may be written with.</summary>
    public static bool IsCompatible(Type resultType, ColumnValueType valueType)
    {
        if (resultType == typeof(double))
        {
            return valueType is ColumnValueType.Double or ColumnValueType.DoubleRaw
                or ColumnValueType.ScaledNumber64 or ColumnValueType.ScaledNumber32;
        }

        if (resultType == typeof(float))
        {
            return valueType is ColumnValueType.Float or ColumnValueType.FloatRaw or ColumnValueType.ScaledNumber32;
        }

        if (resultType == typeof(decimal))
        {
            return valueType == ColumnValueType.Decimal;
        }

        if (resultType == typeof(int))
        {
            return valueType is ColumnValueType.Int32 or ColumnValueType.Int32Raw;
        }

        if (resultType == typeof(long))
        {
            return valueType is ColumnValueType.Int64 or ColumnValueType.Int64Raw;
        }

        if (resultType == typeof(DateTime))
        {
            return valueType == ColumnValueType.DateTimeUnordered;
        }

        if (resultType == typeof(TimeSpan))
        {
            return valueType == ColumnValueType.TimeSpan;
        }

        if (resultType == typeof(bool))
        {
            return valueType == ColumnValueType.Bool;
        }

        return resultType == typeof(string) && valueType == ColumnValueType.Category;
    }

    /// <summary>The lossless encoding for a result of <paramref name="resultType"/>.</summary>
    public static OutputEncoding Lossless(Type resultType, TimePrecision timePrecision)
    {
        if (resultType == typeof(double))
        {
            return OutputEncoding.Double;
        }

        if (resultType == typeof(float))
        {
            return OutputEncoding.Float;
        }

        if (resultType == typeof(decimal))
        {
            return OutputEncoding.Decimal;
        }

        if (resultType == typeof(int))
        {
            return OutputEncoding.Int32;
        }

        if (resultType == typeof(long))
        {
            return OutputEncoding.Int64;
        }

        if (resultType == typeof(DateTime))
        {
            return OutputEncoding.DateTimeUnordered(timePrecision);
        }

        if (resultType == typeof(TimeSpan))
        {
            return OutputEncoding.TimeSpan(timePrecision);
        }

        if (resultType == typeof(bool))
        {
            return OutputEncoding.Bool;
        }

        return resultType == typeof(string)
            ? OutputEncoding.Category
            : throw new ArgumentOutOfRangeException(nameof(resultType), resultType, "Unsupported result type.");
    }
}
