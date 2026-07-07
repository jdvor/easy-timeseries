namespace Easy.TimeSeries;

/// <summary>
/// <see cref="DateTime"/> helpers that snap a timestamp to the start (or start of the next) year, month, day, or hour
/// in UTC. Used to bucket time-series data into the file layout produced by <see cref="Paths.PathBuilder"/>. Every
/// method normalizes to UTC first.
/// </summary>
public static class Extensions
{
    public static DateTime BeginningOfYearUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginningOfNextYearUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginningOfMonthUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginningOfNextMonthUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return dt.Month == 12
            ? new DateTime(dt.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(dt.Year, dt.Month + 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime MidnightUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime NextMidnightUtc(this DateTime dt)
        => dt.MidnightUtc().Add(TimeSpan.FromDays(1));

    public static DateTime BeginningOfHourUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginningOfNextHourUtc(this DateTime dt)
        => dt.BeginningOfHourUtc().Add(TimeSpan.FromHours(1));

    public static DateTime ForceUtc(this DateTime dt)
        => DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
