namespace Easy.TimeSeries.Tests;

using Abstractions;

public sealed class Gamma
{
    [Column(0)]
    public string Sensor { get; set; } = string.Empty;

    [Column(1)]
    public float Pressure { get; set; }

    [Column(2)]
    public double Temperature { get; set; }

    public static Gamma[] DataSet1()
    {
        return
        [
            new Gamma { Sensor = "A1", Pressure = 101.325f, Temperature = 23.4567 },
            new Gamma { Sensor = "A2", Pressure = 101.330f, Temperature = 23.4612 },
            new Gamma { Sensor = "A1", Pressure = 101.318f, Temperature = 23.4501 },
            new Gamma { Sensor = "B1", Pressure = 99.876f, Temperature = 18.2345 },
            new Gamma { Sensor = "B1", Pressure = 99.880f, Temperature = 18.2389 },
            new Gamma { Sensor = "A2", Pressure = 101.412f, Temperature = 23.5012 },
            new Gamma { Sensor = "B1", Pressure = 99.901f, Temperature = 18.2401 },
        ];
    }
}
