namespace Easy.TimeSeries.Benchmarks;

using BenchmarkDotNet.Attributes;
using static Constants;

/// <summary>
/// Throughput guard for <see cref="Block.CreateBlock32"/> / <see cref="Block.CreateBlock64"/>, which run once per
/// changed value on every XOR + block encode. Both are backed by the <c>BitOperations.TrailingZeroCount</c> /
/// <c>LeadingZeroCount</c> intrinsics (TZCNT / LZCNT); this benchmark exists to catch a regression if that ever
/// changes. See git history for the loop + lookup-table implementation this replaced (~2.6x/4.3x slower).
/// </summary>
[MemoryDiagnoser]
public class BlockBenchmark
{
    private int[] data32 = [];
    private long[] data64 = [];

    [GlobalSetup]
    public void Setup()
    {
        data32 = Generate32();
        data64 = Generate64();
    }

    [Benchmark]
    public int CreateBlock32()
    {
        var acc = 0;
        foreach (var value in data32)
        {
            acc += Block.CreateBlock32(value).BlockSize;
        }

        return acc;
    }

    [Benchmark]
    public int CreateBlock64()
    {
        var acc = 0;
        foreach (var value in data64)
        {
            acc += Block.CreateBlock64(value).BlockSize;
        }

        return acc;
    }

    /// <summary>
    /// Single-bit values sweep trailing-zero counts 0..31, plus a handful of mixed/negative/zero patterns;
    /// tiled to a stable working-set size.
    /// </summary>
    private static int[] Generate32()
    {
        var bases = new List<int>();
        for (var k = 0; k < Size32.MaxBits; k++)
        {
            bases.Add(unchecked(1 << k));
        }

        bases.AddRange([0, -1, 0x7FFF_FFFF, unchecked((int)0xF0F0_F0F0), 0x00FF_FF00, 42, 0x0001_0001]);
        return Tile(bases, 1024);
    }

    private static long[] Generate64()
    {
        var bases = new List<long>();
        for (var k = 0; k < Size64.MaxBits; k++)
        {
            bases.Add(unchecked(1L << k));
        }

        bases.AddRange([0, -1, long.MaxValue, unchecked((long)0xF0F0_F0F0_F0F0_F0F0), 0x00FF_FF00_00FF_FF00, 42, 0x0001_0000_0001]);
        return Tile64(bases, 1024);
    }

    private static int[] Tile(List<int> bases, int count)
    {
        var result = new int[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = bases[i % bases.Count];
        }

        return result;
    }

    private static long[] Tile64(List<long> bases, int count)
    {
        var result = new long[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = bases[i % bases.Count];
        }

        return result;
    }
}
