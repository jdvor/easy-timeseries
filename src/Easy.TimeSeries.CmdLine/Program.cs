using Easy.TimeSeries;
using Easy.TimeSeries.CmdLine;




return;

var doubles = new double[] { 10009584, 10016879, 10009584 ^ 80016879 };
foreach (var d in doubles)
{
    Dump(d);
}

static void Dump(double d)
{
    var i64 = BitConverter.DoubleToInt64Bits(d);
    var i64Bin = i64.ToBinaryString();
    Console.WriteLine($"{i64Bin} {d}");
}


int lastBits = 0;
for (var i = 0; i < ushort.MaxValue; i++)
{
    var bits = Util.BitsRequiredToStore(i);
    if (lastBits != bits)
    {
        Console.WriteLine($"{i} : {bits}");
    }

    lastBits = bits;
}

Console.WriteLine(Util.MaxValueStoredInBits(15));
Console.WriteLine(short.MaxValue);
