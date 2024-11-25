namespace Easy.TimeSeries;

using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class Expect
{
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNull<T>(T value, [CallerArgumentExpression(nameof(value))] string? argName = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotEmpty(string value, [CallerArgumentExpression(nameof(value))] string? argName = default)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Argument '{argName}' must not be empty string.", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MinLength(
        ReadOnlySpan<byte> span,
        int min,
        [CallerArgumentExpression(nameof(span))] string? argName = default)
    {
        if (span.Length < min)
        {
            throw new ArgumentException($"Buffer must have size equal or greater to {min}.", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MinLength(
        Span<byte> span,
        int min,
        [CallerArgumentExpression(nameof(span))] string? argName = default)
    {
        if (span.Length < min)
        {
            throw new ArgumentException($"Buffer must have size equal or greater to {min}.", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Range<T>(
        T value,
        T min,
        T max,
        [CallerArgumentExpression(nameof(value))] string? argName = default)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(min) == -1 || value.CompareTo(max) == 1)
        {
            throw new ArgumentException($"Argument '{argName}' must within between {min} and {max} (both inclusive).", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EqualOrGreaterThan<T>(
        T value,
        T min,
        [CallerArgumentExpression(nameof(value))] string? argName = default)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(min) == -1)
        {
            throw new ArgumentException($"Argument '{argName}' must be equal or larger than {min}.", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotDefault<T>(T value, [CallerArgumentExpression(nameof(value))] string? argName = default)
        where T : struct
    {
        if (value.Equals(default(T)))
        {
            throw new ArgumentException($"Argument '{argName}' must not equal default value for {typeof(T).Name}.", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Utc(DateTime value, [CallerArgumentExpression(nameof(value))] string? argName = default)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date & time must be in UTC.", argName);
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Utc(DateTimeOffset value, [CallerArgumentExpression(nameof(value))] string? argName = default)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Date & time must be in UTC.", argName);
        }
    }
}
