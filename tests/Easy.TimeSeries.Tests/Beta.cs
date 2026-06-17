namespace Easy.TimeSeries.Tests;

using Abstractions;

public sealed class Beta
{
    [Column(0)]
    public DateTime Timestamp { get; set; }

    [Column(1)]
    public TimeSpan Elapsed { get; set; }

    [Column(2)]
    public long Counter { get; set; }

    [Column(3)]
    public bool IsValid { get; set; }

    public static Beta[] DataSet1()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return
        [
            new Beta { Timestamp = start, Elapsed = TimeSpan.FromSeconds(1), Counter = 100L, IsValid = true },
            new Beta { Timestamp = start.AddSeconds(30), Elapsed = TimeSpan.FromSeconds(2), Counter = 101L, IsValid = false },
            new Beta { Timestamp = start.AddMinutes(1), Elapsed = TimeSpan.FromSeconds(5), Counter = 200L, IsValid = true },
            new Beta { Timestamp = start.AddMinutes(2), Elapsed = TimeSpan.FromSeconds(5), Counter = 201L, IsValid = true },
            new Beta { Timestamp = start.AddMinutes(5), Elapsed = TimeSpan.FromMinutes(1), Counter = 5_000_000_000L, IsValid = false },
            new Beta { Timestamp = start.AddMinutes(15), Elapsed = TimeSpan.FromMinutes(10), Counter = -42L, IsValid = true },
        ];
    }
}
