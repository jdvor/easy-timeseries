namespace Easy.TimeSeries.Tests;

using Abstractions;

public sealed class Alpha
{
    [Column(0)]
    public int Age { get; set; }

    [Column(1)]
    public float Radiation { get; set; }

    [Column(2)]
    public double TorqueRatio { get; set; }

    public static Alpha[] DataSet1()
    {
        return
        [
            new Alpha { Age = 26, Radiation = 35_000.50f, TorqueRatio = 1.95 },
            new Alpha { Age = 28, Radiation = 42_000.9f, TorqueRatio = 1.84 },
            new Alpha { Age = 35, Radiation = 20.87f, TorqueRatio = 1.73 },
            new Alpha { Age = 18, Radiation = -895.787f, TorqueRatio = 1.59 },
            new Alpha { Age = 74, Radiation = -145.4f, TorqueRatio = 1.97 },
        ];
    }
}
