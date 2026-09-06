namespace Easy.TimeSeries.Tests;

using System.IO;

/// <summary>
/// Rewrites the committed golden files from <see cref="GoldenFixtures"/>. Inert unless
/// <c>ETS_REGEN_GOLDEN=1</c> is set, so a normal test run can never silently "fix" a format regression by
/// overwriting the very files that detect it. Invoked by <c>./scripts/regen-golden.sh</c>.
/// </summary>
public class GoldenRegenerationTests
{
    private const string EnvVar = "ETS_REGEN_GOLDEN";

    [Fact]
    public async Task Regenerate_golden_files()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvVar), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"set {EnvVar}=1 to rewrite the golden files (see ./scripts/regen-golden.sh)");
        }

        var ct = TestContext.Current.CancellationToken;
        var dir = GoldenFileTests.GoldenDirectory();

        foreach (var fixture in GoldenFixtures.All)
        {
            var bytes = await GoldenFileTests.ProduceAsync(fixture, ct);
            var path = Path.Combine(dir, $"{fixture.Name}.ets");
            await File.WriteAllBytesAsync(path, bytes, ct);
            TestContext.Current.TestOutputHelper?.WriteLine($"wrote {fixture.Name}.ets ({bytes.Length} B)");
        }
    }
}
