namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Abstractions;
using Easy.TimeSeries.Storage;

/// <summary>
/// Regression tests for two silent-truncation defects in <c>DateTimeOrderedWriter</c>. Both wrote successfully and
/// read back a plausible but wrong instant, with no exception - the worst possible failure mode for a storage
/// format, and invisible to a round-trip test that only uses well-behaved data.
/// </summary>
public class DateTimeOrderedLimitsTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The widest delta-of-delta bucket was 32 bits, so a gap whose delta-of-delta passed +/-2^31 was truncated.
    /// At millisecond precision that is ~24.86 days, which any sensor going offline for a month would hit.
    /// 24 days used to pass and 25 used to fail; both must round-trip now.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(399)]
    [InlineData(3650)]
    public async Task Large_gaps_round_trip_at_millisecond_precision(int gapDays)
    {
        var ct = TestContext.Current.CancellationToken;
        DateTime[] timestamps = [Start, Start.AddSeconds(1), Start.AddSeconds(1).AddDays(gapDays)];

        var result = await RoundTripAsync(timestamps, TimePrecision.Milliseconds, ct);

        Assert.Equal(timestamps, result);
    }

    /// <summary>Alternating huge forward hops, which swing the delta-of-delta hard in both directions.</summary>
    [Fact]
    public async Task Alternating_large_and_small_gaps_round_trip()
    {
        var ct = TestContext.Current.CancellationToken;
        var timestamps = new DateTime[20];
        var t = Start;
        for (var i = 0; i < timestamps.Length; i++)
        {
            timestamps[i] = t;
            t = i % 2 == 0 ? t.AddDays(90) : t.AddMilliseconds(250);
        }

        var result = await RoundTripAsync(timestamps, TimePrecision.Milliseconds, ct);

        Assert.Equal(timestamps, result);
    }

    /// <summary>Coarser precision was never affected, but it must stay that way.</summary>
    [Theory]
    [InlineData(TimePrecision.Seconds, 5000)]
    [InlineData(TimePrecision.TenthsOfSecond, 500)]
    public async Task Large_gaps_round_trip_at_coarser_precision(TimePrecision precision, int gapDays)
    {
        var ct = TestContext.Current.CancellationToken;
        DateTime[] timestamps = [Start, Start.AddSeconds(1), Start.AddSeconds(1).AddDays(gapDays)];

        var result = await RoundTripAsync(timestamps, precision, ct);

        Assert.Equal(timestamps, result);
    }

    /// <summary>
    /// The 41-bit timestamp field cannot be widened - it is the format. A value past the ceiling used to wrap
    /// silently (2070 read back as 2000), so it must now throw instead.
    /// </summary>
    [Fact]
    public void Timestamp_past_the_41_bit_ceiling_throws_instead_of_wrapping()
    {
        var beyond = new DateTime(2070, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime[] timestamps = [beyond, beyond.AddSeconds(1)];

        using var writer = new Writer(timestamps.Length);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.AddTimeOrdered(timestamps, "ts"));

        Assert.Contains("AddTimeUnordered", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timestamp_just_below_the_ceiling_still_round_trips()
    {
        var ct = TestContext.Current.CancellationToken;
        var nearCeiling = new DateTime(2069, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime[] timestamps = [nearCeiling, nearCeiling.AddSeconds(1)];

        var result = await RoundTripAsync(timestamps, TimePrecision.Milliseconds, ct);

        Assert.Equal(timestamps, result);
    }

    /// <summary>A coarser precision moves the ceiling far out, so the same instant becomes representable.</summary>
    [Fact]
    public async Task Beyond_ceiling_at_milliseconds_is_representable_at_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var far = new DateTime(2100, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        DateTime[] timestamps = [far, far.AddSeconds(1)];

        var result = await RoundTripAsync(timestamps, TimePrecision.Seconds, ct);

        Assert.Equal(timestamps, result);
    }

    /// <summary>The unordered encoder stores full-width values and is the documented escape hatch; verify it is.</summary>
    [Fact]
    public async Task Unordered_column_accepts_values_beyond_the_ordered_ceiling()
    {
        var ct = TestContext.Current.CancellationToken;
        var far = new DateTime(2100, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        DateTime[] timestamps = [far, Start, far.AddDays(365)];

        using var storage = new InMemoryStorage();
        using (var writer = new Writer(timestamps.Length).AddTimeUnordered(timestamps, "ts"))
        {
            await writer.WriteToAsync(storage, ct);
        }

        var rows = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Row>(), null, ct);

        Assert.Equal(timestamps, rows.Select(x => x.Ts));
    }

    private static async Task<DateTime[]> RoundTripAsync(
        DateTime[] timestamps,
        TimePrecision precision,
        CancellationToken ct)
    {
        using var storage = new InMemoryStorage();
        using (var writer = new Writer(timestamps.Length).AddTimeOrdered(timestamps, "ts", precision))
        {
            await writer.WriteToAsync(storage, ct);
        }

        var rows = await Reader.ReadFromAsync(storage, new ReflectionBasedMaterializer<Row>(), null, ct);
        return [.. rows.Select(x => x.Ts)];
    }

    private sealed class Row
    {
        [Column(0)]
        public DateTime Ts { get; init; }
    }
}
