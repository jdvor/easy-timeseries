namespace Easy.TimeSeries.SrcGen.Tests;

public class MaterializerGenerationTests
{
    [Fact]
    public void Generates_materializer_and_reader_for_power_node()
    {
        var run = GeneratorTestHelper.Run(TestSources.PowerNode, ct: TestContext.Current.CancellationToken);

        Assert.Contains("Easy.Sample.PowerNodeMaterializer.g.cs", run.GeneratedHintNames);
        Assert.Contains("Easy.Sample.PowerNodeReader.g.cs", run.GeneratedHintNames);
        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.CompilationErrors);
    }

    [Fact]
    public void Materializer_routes_each_column_index_to_its_property()
    {
        var run = GeneratorTestHelper.Run(TestSources.PowerNode, ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("PowerNodeMaterializer.g.cs");

        // ScaledNumber32 columns hydrate as float, exactly like plain float columns.
        Assert.Contains("if (value is float f)", source);
        Assert.Contains("instance.CapacityMw = f;", source);
        Assert.Contains("instance.PowerNodeId = i64;", source);
        Assert.Contains("instance.StartDuration = ts;", source);
        Assert.Contains("instance.MeasurementTimeUtc = dt;", source);
        Assert.Contains("instance.CountryCode = value;", source);
        Assert.Contains("instance.PrimaryFuel = value;", source);

        // Unmapped column indexes are rejected up front.
        Assert.Contains("if (column is < 0 or > 10)", source);
    }

    [Fact]
    public void Materializer_only_emits_type_branches_the_dto_needs()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("    [Column(0)]\n    public int Total { get; set; }", "[GenerateReader]"),
            ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("DtoMaterializer.g.cs");

        Assert.Contains("if (value is int i32)", source);
        Assert.DoesNotContain("is float", source);
        Assert.DoesNotContain("is decimal", source);
        Assert.Empty(run.CompilationErrors);
    }
}
