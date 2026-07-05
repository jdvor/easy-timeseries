namespace Easy.TimeSeries.SrcGen.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Ets001_duplicate_column_index()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public int A { get; set; }

                [Column(0)]
                public int B { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS001"));
        Assert.Empty(run.GeneratedHintNames);
    }

    [Fact]
    public void Ets002_non_contiguous_column_indexes()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public int A { get; set; }

                [Column(2)]
                public int B { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS002"));
        Assert.Empty(run.GeneratedHintNames);
    }

    [Fact]
    public void Ets003_unsupported_property_type()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public Guid Id { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS003"));
    }

    [Fact]
    public void Ets004_nullable_column_property()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public int? Total { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS004"));
    }

    [Fact]
    public void Ets004_nullable_reference_column_property()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                #nullable enable
                [Column(0)]
                public string? Name { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS004"));
    }

    [Fact]
    public void Ets005_incompatible_explicit_value_type()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0, ValueType = DataValueType.Int32)]
                public string Name { get; set; } = string.Empty;
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS005"));
    }

    [Fact]
    public void Ets006_scaled_number_without_precision()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0, ValueType = DataValueType.ScaledNumber32)]
                public float Capacity { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS006"));
    }

    [Fact]
    public void Ets007_writer_requires_accessible_getter()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public int Total { private get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS007"));
    }

    [Fact]
    public void Ets007_reader_requires_settable_property()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public int Total { get; }
            """,
                "[GenerateReader]"),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS007"));
    }

    [Fact]
    public void Ets007_reader_rejects_init_only_setter()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public int Total { get; init; }
            """,
                "[GenerateReader]"),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS007"));
    }

    [Fact]
    public void Ets008_struct_target()
    {
        const string source = """
            namespace Tests;

            using Easy.TimeSeries.Abstractions;

            [GenerateWriter]
            public struct Dto
            {
                [Column(0)]
                public int Total { get; set; }
            }
            """;
        var run = GeneratorTestHelper.Run(source, ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS008"));
    }

    [Fact]
    public void Ets008_reader_requires_public_parameterless_ctor()
    {
        const string source = """
            namespace Tests;

            using Easy.TimeSeries.Abstractions;

            [GenerateReader]
            public sealed class Dto
            {
                public Dto(int total) => Total = total;

                [Column(0)]
                public int Total { get; set; }
            }
            """;
        var run = GeneratorTestHelper.Run(source, ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS008"));
    }

    [Fact]
    public void Ets009_number_precision_ignored_on_decimal()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("""
                [Column(0)]
                public decimal Price { get; set; }

                [Column(1, NumberPrecision = NumberPrecision.DecimalPlaces2)]
                public decimal Fee { get; set; }
            """),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS009"));

        // A warning must not block generation.
        Assert.Contains("Tests.DtoWriter.g.cs", run.GeneratedHintNames);
        Assert.Contains("""AddDecimal(new global::System.ArraySegment<decimal>(c1, 0, rows), "Fee")""", run.GeneratedSource("DtoWriter.g.cs"));
    }

    [Fact]
    public void Ets010_no_column_properties()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.Dto("    public int Total { get; set; }"),
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS010"));
    }

    [Fact]
    public void Ets011_missing_core_reference()
    {
        var run = GeneratorTestHelper.Run(
            TestSources.PowerNode,
            referenceCore: false,
            ct: TestContext.Current.CancellationToken);

        Assert.True(run.HasDiagnostic("ETS011"));
        Assert.Empty(run.GeneratedHintNames);
    }
}
