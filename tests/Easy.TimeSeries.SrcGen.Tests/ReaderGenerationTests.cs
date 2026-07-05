namespace Easy.TimeSeries.SrcGen.Tests;

public class ReaderGenerationTests
{
    [Fact]
    public void Reader_delegates_to_core_reader_with_generated_materializer()
    {
        var run = GeneratorTestHelper.Run(TestSources.PowerNode, ct: TestContext.Current.CancellationToken);
        var source = run.GeneratedSource("PowerNodeReader.g.cs");

        Assert.Contains("global::Easy.TimeSeries.Reader.ReadFromAsync(", source);
        Assert.Contains("new PowerNodeMaterializer(),", source);
        Assert.Contains("IReader<global::Easy.Sample.PowerNode>", source);
        Assert.Empty(run.CompilationErrors);
    }

    [Fact]
    public void Writer_only_attribute_does_not_generate_reader()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("    [Column(0)]\n    public int Total { get; set; }"),
            ct: TestContext.Current.CancellationToken);

        Assert.Contains("Tests.DtoWriter.g.cs", run.GeneratedHintNames);
        Assert.DoesNotContain("Tests.DtoReader.g.cs", run.GeneratedHintNames);
        Assert.DoesNotContain("Tests.DtoMaterializer.g.cs", run.GeneratedHintNames);
    }
}
