namespace Easy.TimeSeries.CmdLine;

public static class BoolSandbox
{
    public static void Run()
    {
        var word = 0UL;
        for (var i = 0; i < Data.Length; i++)
        {
            if (Data[i])
            {
                word |= 1UL << i;
            }
        }

        for (var i = 0; i < Data.Length; i++)
        {
            var isSet = (word & (1UL << i)) != 0;
            if (Data[i] != isSet)
            {
                Console.WriteLine($"fail {i}");
            }
        }
    }

    private static readonly bool[] Data =
    {
        true, true, false, false, true, false, true, false,
        true, true, false, false, true, false, true, false,
        false, true, false, false, true, false, true, false,
        true, true, false, false, true, false, true, true,
        true, true, false, false, true, false, true, false,
        true, true, false, false, true, false, true, false,
        false, true, false, false, true, false, true, false,
        true, true, false, false, true, false, true, true,
    };
}
