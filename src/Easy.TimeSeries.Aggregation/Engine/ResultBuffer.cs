namespace Easy.TimeSeries.Aggregation.Engine;

using System.Runtime.CompilerServices;

/// <summary>
/// Results of one function, typed by its result. Created once per run through a closed switch on the result type,
/// so computing a slice is a direct generic call with no boxing.
/// </summary>
internal abstract class ResultBuffer : IDisposable
{
    public static ResultBuffer Create(IAggregationFunction function)
    {
        var type = function.ResultType;
        if (type == typeof(double))
        {
            return new ResultBuffer<double>((IAggregationFunction<double>)function);
        }

        if (type == typeof(float))
        {
            return new ResultBuffer<float>((IAggregationFunction<float>)function);
        }

        if (type == typeof(decimal))
        {
            return new ResultBuffer<decimal>((IAggregationFunction<decimal>)function);
        }

        if (type == typeof(int))
        {
            return new ResultBuffer<int>((IAggregationFunction<int>)function);
        }

        if (type == typeof(long))
        {
            return new ResultBuffer<long>((IAggregationFunction<long>)function);
        }

        if (type == typeof(DateTime))
        {
            return new ResultBuffer<DateTime>((IAggregationFunction<DateTime>)function);
        }

        if (type == typeof(TimeSpan))
        {
            return new ResultBuffer<TimeSpan>((IAggregationFunction<TimeSpan>)function);
        }

        if (type == typeof(bool))
        {
            return new ResultBuffer<bool>((IAggregationFunction<bool>)function);
        }

        return type == typeof(string)
            ? new ResultBuffer<string>((IAggregationFunction<string>)function)
            : throw new AggregationException($"Function '{function.Name}' returns unsupported type {type.Name}.", function: function.Name);
    }

    public abstract void Compute(in AggregationSlice slice);

    /// <summary>Adds the results as one column, after checking they fit <paramref name="encoding"/>.</summary>
    public abstract void WriteTo(Writer writer, string label, OutputEncoding encoding);

    public abstract void Dispose();
}

internal sealed class ResultBuffer<TOut>(IAggregationFunction<TOut> function) : ResultBuffer
{
    private readonly GrowableBuffer<TOut> values = new();

    public override void Compute(in AggregationSlice slice)
    {
        TOut result;
        try
        {
            result = function.Compute(slice);
        }
        catch (Exception ex) when (ex is not AggregationException and not OperationCanceledException)
        {
            throw new AggregationException(
                $"Function '{function.Name}' failed: {ex.Message}",
                function: function.Name,
                innerException: ex);
        }

        values.Add(result);
    }

    public override void WriteTo(Writer writer, string label, OutputEncoding encoding)
    {
        var segment = values.Segment;
        if (typeof(TOut) == typeof(double))
        {
            ResultWriter.Write(writer, label, encoding, Unsafe.As<ArraySegment<TOut>, ArraySegment<double>>(ref segment));
        }
        else if (typeof(TOut) == typeof(float))
        {
            ResultWriter.Write(writer, label, encoding, Unsafe.As<ArraySegment<TOut>, ArraySegment<float>>(ref segment));
        }
        else if (typeof(TOut) == typeof(decimal))
        {
            writer.AddDecimal(Unsafe.As<ArraySegment<TOut>, ArraySegment<decimal>>(ref segment), label);
        }
        else if (typeof(TOut) == typeof(int))
        {
            var ints = Unsafe.As<ArraySegment<TOut>, ArraySegment<int>>(ref segment);
            _ = encoding.ValueType == ColumnValueType.Int32Raw ? writer.AddInt32Random(ints, label) : writer.AddInt32(ints, label);
        }
        else if (typeof(TOut) == typeof(long))
        {
            var longs = Unsafe.As<ArraySegment<TOut>, ArraySegment<long>>(ref segment);
            _ = encoding.ValueType == ColumnValueType.Int64Raw ? writer.AddInt64Random(longs, label) : writer.AddInt64(longs, label);
        }
        else if (typeof(TOut) == typeof(DateTime))
        {
            writer.AddTimeUnordered(Unsafe.As<ArraySegment<TOut>, ArraySegment<DateTime>>(ref segment), label, (TimePrecision)encoding.Meta);
        }
        else if (typeof(TOut) == typeof(TimeSpan))
        {
            writer.AddInterval(Unsafe.As<ArraySegment<TOut>, ArraySegment<TimeSpan>>(ref segment), label, (TimePrecision)encoding.Meta);
        }
        else if (typeof(TOut) == typeof(bool))
        {
            writer.AddBool(Unsafe.As<ArraySegment<TOut>, ArraySegment<bool>>(ref segment), label);
        }
        else
        {
            writer.AddCategory(Unsafe.As<ArraySegment<TOut>, ArraySegment<string>>(ref segment), label);
        }
    }

    public override void Dispose() => values.Dispose();
}

/// <summary>Writes floating-point results, range-checking the scaled encodings, which would otherwise saturate silently.</summary>
internal static class ResultWriter
{
    // Exclusive bounds of |scaled value|: 2^31 and 2^63, both exact in double.
    private const double IntLimit = 2147483648d;
    private const double LongLimit = 9223372036854775808d;

    public static void Write(Writer writer, string label, OutputEncoding encoding, ArraySegment<double> values)
    {
        switch (encoding.ValueType)
        {
            case ColumnValueType.Double: writer.AddDouble(values, label); break;
            case ColumnValueType.DoubleRaw: writer.AddDoubleRandom(values, label); break;
            case ColumnValueType.ScaledNumber64:
                CheckScaled(values, label, encoding, LongLimit);
                writer.AddScaledNumber64(values, label, encoding.Meta);
                break;
            case ColumnValueType.ScaledNumber32:
                CheckScaled(values, label, encoding, IntLimit);
                writer.AddScaledNumber32(values, label, encoding.Meta);
                break;
            default: throw Incompatible(label, encoding, typeof(double));
        }
    }

    public static void Write(Writer writer, string label, OutputEncoding encoding, ArraySegment<float> values)
    {
        switch (encoding.ValueType)
        {
            case ColumnValueType.Float: writer.AddFloat(values, label); break;
            case ColumnValueType.FloatRaw: writer.AddFloatRandom(values, label); break;
            case ColumnValueType.ScaledNumber32:
                foreach (var v in values)
                {
                    CheckScaled(v, label, encoding, IntLimit);
                }

                writer.AddScaledNumber32(values, label, encoding.Meta);
                break;
            default: throw Incompatible(label, encoding, typeof(float));
        }
    }

    private static void CheckScaled(ArraySegment<double> values, string label, OutputEncoding encoding, double max)
    {
        foreach (var v in values)
        {
            CheckScaled(v, label, encoding, max);
        }
    }

    // The core writer computes (integer)Math.Round(value * 10^N); NaN becomes 0 and anything else out of range
    // saturates, so both are rejected here.
    private static void CheckScaled(double value, string label, OutputEncoding encoding, double max)
    {
        var scale = Math.Pow(10, encoding.Meta);
        var scaled = Math.Round(value * scale);
        // The integer range is asymmetric: -max (int.MinValue or long.MinValue) is stored exactly, +max is not.
        if (double.IsFinite(scaled) && scaled >= -max && scaled < max)
        {
            return;
        }

        throw new AggregationException(
            $"Function '{label}' produced {value}, which {encoding} cannot store (allowed range is about ±{max / scale:G6}); "
            + "set a wider output encoding with WithOutput(...).",
            function: label);
    }

    private static AggregationException Incompatible(string label, OutputEncoding encoding, Type type)
        => new($"Function '{label}' returns {type.Name}, which cannot be stored as {encoding}.", function: label);
}
