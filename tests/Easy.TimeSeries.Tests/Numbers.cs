namespace Easy.TimeSeries.Tests;

internal static class Numbers
{
    private static int seed = Environment.TickCount;
    private static readonly ThreadLocal<Random> Rand = new(() => new Random(Interlocked.Increment(ref seed)));

    internal static IEnumerable<double> RandDoubleSeq(int n, double up, double down = 0, int precision = 0)
    {
        Expect.Range(n, 2, int.MaxValue);
        Expect.Range(precision, 0, 10);

        var random = Rand.Value!;
        var scale = (int)Math.Pow(10, precision);
        var variance = up - down;
        for (var i = 0; i < n; i++)
        {
            var x = down + random.NextDouble() * variance;
            if (precision > 0)
            {
                x = DowngradeToPrecision(x, scale);
            }

            yield return x;
        }
    }

    internal static IEnumerable<float> RandFloatSeq(int n, double up, double down = 0, int precision = 0)
        => RandDoubleSeq(n, up, down, precision).Select(x => (float)x);

    internal static IEnumerable<int> RandIntSeq(int n, double up, double down = 0)
        => RandDoubleSeq(n, up, down).Select(x => (int)x);

    internal static IEnumerable<long> RandLongSeq(int n, double up, double down = 0)
        => RandDoubleSeq(n, up, down).Select(x => (long)x);

    internal static IEnumerable<double> LinearDoubleSeq(int n, double up, double down = 0, int precision = 0)
    {
        Expect.Range(n, 2, int.MaxValue);
        Expect.Range(precision, 0, 10);

        var scale = (int)Math.Pow(10, precision);
        var step = (up - down) / (n - 1);
        for (var i = 0; i < n; i++)
        {
            var x = down + i * step;
            if (precision > 0)
            {
                x = DowngradeToPrecision(x, scale);
            }

            yield return x;
        }
    }

    internal static IEnumerable<float> LinearFloatSeq(int n, double up, double down = 0, int precision = 0)
        => LinearDoubleSeq(n, up, down, precision).Select(x => (float)x);

    internal static IEnumerable<int> LinearIntSeq(int n, double up, double down = 0)
        => LinearDoubleSeq(n, up, down).Select(x => (int)x);

    internal static IEnumerable<long> LinearLongSeq(int n, double up, double down = 0)
        => LinearDoubleSeq(n, up, down).Select(x => (long)x);

    internal static IEnumerable<double> SineDoubleSeq(int n, double up, double down = 0, int precision = 0)
    {
        Expect.Range(n, 2, int.MaxValue);
        Expect.Range(precision, 0, 10);

        var scale = (int)Math.Pow(10, precision);
        var variance = up - down;
        for (var i = 0; i < n; i++)
        {
            var x = down + Math.Abs(Math.Sin(Math.PI / 180 * i)) * variance;
            if (precision > 0)
            {
                x = DowngradeToPrecision(x, scale);
            }

            yield return x;
        }
    }

    internal static IEnumerable<float> SineFloatSeq(int n, double up, double down = 0, int precision = 0)
        => SineDoubleSeq(n, up, down, precision).Select(x => (float)x);

    internal static IEnumerable<int> SineIntSeq(int n, double up, double down = 0)
        => SineDoubleSeq(n, up, down).Select(x => (int)x);

    internal static IEnumerable<long> SineLongSeq(int n, double up, double down = 0)
        => SineDoubleSeq(n, up, down).Select(x => (long)x);

    internal static IEnumerable<double> StairsDoubleSeq(int n, int stepSize, double up, double down = 0, int precision = 0)
    {
        Expect.Range(n, 2, int.MaxValue);
        Expect.Range(precision, 0, 10);

        var scale = (int)Math.Pow(10, precision);
        var step = (up - down) / (n - 1);
        var x = down;
        for (var i = 0; i < n; i++)
        {
            if (i > 0 && i % stepSize == 0)
            {
                x = down + i * step;
                if (precision > 0)
                {
                    x = DowngradeToPrecision(x, scale);
                }
            }

            yield return x;
        }
    }

    internal static IEnumerable<float> StairsFloatSeq(int n, int stepSize, double up, double down = 0, int precision = 0)
        => StairsDoubleSeq(n, stepSize, up, down, precision).Select(x => (float)x);

    internal static IEnumerable<int> StairsIntSeq(int n, int stepSize, double up, double down = 0)
        => StairsDoubleSeq(n, stepSize, up, down).Select(x => (int)x);

    internal static IEnumerable<long> StairsLongSeq(int n, int stepSize, double up, double down = 0)
        => StairsDoubleSeq(n, stepSize, up, down).Select(x => (long)x);

    private static double DowngradeToPrecision(double v, int scale)
        => Math.Floor(v * scale) / scale;

    internal static byte[] RandBytes(int n)
    {
        var result = new byte[n];
        Rand.Value!.NextBytes(result);
        return result;
    }

    internal static bool IsTrue(this Random random)
        => random.Next(0, 2) == 1;
}
