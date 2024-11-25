namespace Easy.TimeSeries;

public static class Extensions
{
    public static DateTime BeginingOfYearUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginingOfNextYearUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginingOfMonthUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginingOfNextMonthUtc(this DateTime dt)
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

    public static DateTime BeginingOfHourUtc(this DateTime dt)
    {
        dt = dt.ToUniversalTime();
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeginingOfNextHourUtc(this DateTime dt)
        => dt.BeginingOfHourUtc().Add(TimeSpan.FromHours(1));

    public static DateTime ForceUtc(this DateTime dt)
        => DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
