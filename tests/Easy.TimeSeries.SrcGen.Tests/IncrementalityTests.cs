namespace Easy.TimeSeries.SrcGen.Tests;

using Microsoft.CodeAnalysis;

public class IncrementalityTests
{
    [Fact]
    public void Unrelated_edit_reuses_cached_outputs()
    {
        var ct = TestContext.Current.CancellationToken;
        var compilation = GeneratorTestHelper.CreateCompilation(TestSources.PowerNode);
        var driver = GeneratorTestHelper.CreateDriver(trackSteps: true);

        driver = driver.RunGenerators(compilation, ct);

        var unrelated = GeneratorTestHelper.ParseSource("namespace Tests;\n\npublic static class Unrelated;");
        driver = driver.RunGenerators(compilation.AddSyntaxTrees(unrelated), ct);

        var result = Assert.Single(driver.GetRunResult().Results);
        var outputs = result.TrackedOutputSteps
            .SelectMany(static steps => steps.Value)
            .SelectMany(static step => step.Outputs)
            .ToArray();

        Assert.NotEmpty(outputs);
        Assert.All(
            outputs,
            static output =>
                Assert.True(output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
    }
}
