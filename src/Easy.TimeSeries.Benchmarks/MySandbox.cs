namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[Config(typeof(Config))]
public class MySandbox
{
    [Benchmark]
    public int GetLeadingZeros()
    {
        var x = 0;
        for (var i = 0; i < 256; i++)
        {
            var y = GetLeadingZeros((uint)i);
            x += y;
        }

        return x;
    }

    [Benchmark]
    public int BitsRequiredToStore()
    {
        var x = 0;
        for (var i = 0; i < 256; i++)
        {
            var y = 32 - (i == 0 ? 0 : Util.BitsRequiredToStore(i));
            x += y;
        }

        return x;
    }

    [Benchmark]
    public int Lookup()
    {
        var x = 0;
        for (var i = 0; i < 256; i++)
        {
            var y = CountLeadingZeros32Lookup[i];
            x += y;
        }

        return x;
    }

    private static byte GetLeadingZeros(uint n)
    {
        return n switch
        {
            0 => 32,
            1 => 31,
            2 => 30,
            3 => 30,
            <= 7 => 29,
            <= 15 => 28,
            <= 31 => 27,
            <= 63 => 26,
            <= 127 => 25,
            <= 255 => 24,
            _ => throw new ArgumentException("...", nameof(n)),
        };
    }

    private static readonly byte[] CountLeadingZeros32Lookup =
    {
        32, 31, 30, 30, 29, 29, 29, 29,
        28, 28, 28, 28, 28, 28, 28, 28,
        27, 27, 27, 27, 27, 27, 27, 27,
        27, 27, 27, 27, 27, 27, 27, 27,
        26, 26, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 26, 26, 26, 26, 26,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        25, 25, 25, 25, 25, 25, 25, 25,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
        24, 24, 24, 24, 24, 24, 24, 24,
    };
}
