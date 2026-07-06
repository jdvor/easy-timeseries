namespace Easy.TimeSeries.SrcGen.Tests;

public class WriterGenerationTests
{
    [Fact]
    public void Generates_writer_for_power_node()
    {
        var run = GeneratorTestHelper.Run(TestSources.PowerNode, ct: TestContext.Current.CancellationToken);

        Assert.Contains("Easy.Sample.PowerNodeWriter.g.cs", run.GeneratedHintNames);
        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.CompilationErrors);
    }

    [Fact]
    public void Writer_maps_columns_in_index_order_with_attribute_options_applied()
    {
        var run = GeneratorTestHelper.Run(TestSources.PowerNode, ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("PowerNodeWriter.g.cs");

        Assert.Contains("""writer.AddCategory(new global::System.ArraySegment<string>(c0, 0, rows), "Country");""", source);
        Assert.Contains("""writer.AddScaledNumber32(new global::System.ArraySegment<float>(c1, 0, rows), "Capacity (MW)", 3);""", source);
        Assert.Contains("""writer.AddFloat(new global::System.ArraySegment<float>(c2, 0, rows), "Latitude");""", source);
        Assert.Contains(
            """writer.AddInterval(new global::System.ArraySegment<global::System.TimeSpan>(c6, 0, rows), "Start Duration", global::Easy.TimeSeries.TimePrecision.Seconds);""",
            source);
        Assert.Contains("""writer.AddDecimal(new global::System.ArraySegment<decimal>(c7, 0, rows), "Start Price");""", source);
        Assert.Contains(
            """writer.AddTimeOrdered(new global::System.ArraySegment<global::System.DateTime>(c9, 0, rows), "Measurement Time", global::Easy.TimeSeries.TimePrecision.Milliseconds);""",
            source);
        Assert.Contains(
            """writer.AddTimeUnordered(new global::System.ArraySegment<global::System.DateTime>(c10, 0, rows), "Certified Time", global::Easy.TimeSeries.TimePrecision.Days);""",
            source);

        // Ascending call order: AddCategory(c0) must precede AddScaledNumber32(c1), etc.
        Assert.True(source.IndexOf("(c0, 0, rows)", StringComparison.Ordinal) < source.IndexOf("(c1, 0, rows)", StringComparison.Ordinal));
        Assert.True(source.IndexOf("(c9, 0, rows)", StringComparison.Ordinal) < source.IndexOf("(c10, 0, rows)", StringComparison.Ordinal));
    }

    [Fact]
    public void Writer_returns_pooled_arrays_and_clears_string_columns()
    {
        var run = GeneratorTestHelper.Run(TestSources.PowerNode, ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("PowerNodeWriter.g.cs");

        Assert.Contains("global::System.Buffers.ArrayPool<string>.Shared.Return(c0, clearArray: true);", source);
        Assert.Contains("global::System.Buffers.ArrayPool<float>.Shared.Return(c1);", source);
    }

    [Fact]
    public void Writer_maps_random_distribution_to_raw_encoders()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0, NumberDistribution = NumberDistribution.Random)]
                public float Latitude { get; set; }

                [Column(1, NumberDistribution = NumberDistribution.Random)]
                public double Longitude { get; set; }

                [Column(2, NumberDistribution = NumberDistribution.Random)]
                public long Identifier { get; set; }

                [Column(3, NumberDistribution = NumberDistribution.Random)]
                public int Bucket { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("DtoWriter.g.cs");

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Contains("""writer.AddFloatRandom(new global::System.ArraySegment<float>(c0, 0, rows), "Latitude");""", source);
        Assert.Contains("""writer.AddDoubleRandom(new global::System.ArraySegment<double>(c1, 0, rows), "Longitude");""", source);
        Assert.Contains("""writer.AddInt64Random(new global::System.ArraySegment<long>(c2, 0, rows), "Identifier");""", source);
        Assert.Contains("""writer.AddInt32Random(new global::System.ArraySegment<int>(c3, 0, rows), "Bucket");""", source);
    }

    [Fact]
    public void Label_falls_back_to_property_name()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("    [Column(0)]\n    public int Total { get; set; }"),
            ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("DtoWriter.g.cs");

        Assert.Contains("""writer.AddInt32(new global::System.ArraySegment<int>(c0, 0, rows), "Total");""", source);
        Assert.Empty(run.CompilationErrors);
    }
}
