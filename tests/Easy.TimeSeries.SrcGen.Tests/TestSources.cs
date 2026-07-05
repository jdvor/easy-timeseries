namespace Easy.TimeSeries.SrcGen.Tests;

internal static class TestSources
{
    /// <summary>Mirror of the Easy.Sample DTO, covering every supported column kind.</summary>
    public const string PowerNode = """
        namespace Easy.Sample;

        using Easy.TimeSeries.Abstractions;
        using System;

        [GenerateReader]
        [GenerateWriter]
        public sealed class PowerNode
        {
            [Column(0, Label = "Country")]
            public string CountryCode { get; set; } = string.Empty;

            [Column(1, Label = "Capacity (MW)", NumberPrecision = NumberPrecision.DecimalPlaces3)]
            public float CapacityMw { get; set; }

            [Column(2)]
            public float Latitude { get; set; }

            [Column(3)]
            public float Longitude { get; set; }

            [Column(4, Label = "Primary Fuel")]
            public string PrimaryFuel { get; set; } = string.Empty;

            [Column(5, Label = "Enabled")]
            public bool IsEnabled { get; set; }

            [Column(6, Label = "Start Duration")]
            public TimeSpan StartDuration { get; set; }

            [Column(7, Label = "Start Price")]
            public decimal StartPrice { get; set; }

            [Column(8, Label = "ID")]
            public long PowerNodeId { get; set; }

            [Column(9, Label = "Measurement Time", DateTimePrecision = DateTimePrecision.Milliseconds, DateTimeSort = DateTimeSort.Ascending)]
            public DateTime MeasurementTimeUtc { get; set; }

            [Column(10, Label = "Certified Time", DateTimePrecision = DateTimePrecision.Days)]
            public DateTime CertifiedTimeUtc { get; set; }
        }
        """;

    /// <summary>Wraps property declarations in an annotated DTO class named 'Dto' in namespace 'Tests'.</summary>
    public static string Dto(string properties, string attributes = "[GenerateWriter]") => $$"""
        namespace Tests;

        using Easy.TimeSeries.Abstractions;
        using System;

        {{attributes}}
        public sealed class Dto
        {
        {{properties}}
        }
        """;
}
