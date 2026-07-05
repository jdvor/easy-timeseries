namespace Easy.TimeSeries.TestData;

using Easy.TimeSeries.Abstractions;
using System.Diagnostics.CodeAnalysis;

[GenerateReader]
[GenerateWriter]
[SuppressMessage("Style", "IDE1006:Naming Styles")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public sealed class Macro4
{
    [Column(0)]
    public double rgnp { get; set; }

    [Column(1)]
    public double tb3m { get; set; }

    [Column(2)]
    public double lnm1 { get; set; }

    [Column(3)]
    public double gs10 { get; set; }
}
