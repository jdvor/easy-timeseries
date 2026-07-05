namespace Easy.TimeSeries.SrcGen.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.IO;

internal sealed record GeneratorRun(
    GeneratorDriverRunResult Result,
    Compilation UpdatedCompilation,
    ImmutableArray<Diagnostic> GeneratorDiagnostics)
{
    public IReadOnlyList<Diagnostic> CompilationErrors
        => [.. UpdatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];

    public bool HasDiagnostic(string id) => GeneratorDiagnostics.Any(d => d.Id == id);

    /// <summary>Source text of the single generated tree whose hint name ends with the given suffix.</summary>
    public string GeneratedSource(string hintSuffix)
        => Result.GeneratedTrees.Single(t => t.FilePath.EndsWith(hintSuffix, StringComparison.Ordinal)).ToString();

    public IReadOnlyList<string> GeneratedHintNames
        => [.. Result.GeneratedTrees.Select(t => Path.GetFileName(t.FilePath))];
}

internal static class GeneratorTestHelper
{
    private static readonly Lazy<MetadataReference[]> References = new(LoadReferences);

    public static GeneratorRun Run(string source, bool referenceCore = true, CancellationToken ct = default)
    {
        var driver = CreateDriver(trackSteps: false);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            CreateCompilation(source, referenceCore),
            out var updated,
            out var diagnostics,
            ct);
        return new GeneratorRun(driver.GetRunResult(), updated, diagnostics);
    }

    public static GeneratorDriver CreateDriver(bool trackSteps)
        => CSharpGeneratorDriver.Create(
            [new TimeSeriesGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: trackSteps));

    public static CSharpCompilation CreateCompilation(string source, bool referenceCore = true)
    {
        var references = referenceCore
            ? References.Value
            : [.. References.Value.Where(static r => !IsCoreLibraryReference(r))];

        return CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    public static SyntaxTree ParseSource(string source) => CSharpSyntaxTree.ParseText(source, ParseOptions);

    private static CSharpParseOptions ParseOptions { get; } =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    private static bool IsCoreLibraryReference(MetadataReference reference)
        => reference.Display?.EndsWith($"{Path.DirectorySeparatorChar}Easy.TimeSeries.dll", StringComparison.OrdinalIgnoreCase) == true;

    private static MetadataReference[] LoadReferences()
    {
        // The test host's trusted assemblies include the runtime plus every referenced project
        // output (Easy.TimeSeries.Abstractions, Easy.TimeSeries), which is exactly what the
        // compiled-against-real-assemblies test compilations need.
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return
        [
            .. trusted
                .Split(Path.PathSeparator)
                .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    }
}
