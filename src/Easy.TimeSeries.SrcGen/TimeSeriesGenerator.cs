namespace Easy.TimeSeries.SrcGen;

using Easy.TimeSeries.SrcGen.Emit;
using Easy.TimeSeries.SrcGen.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

/// <summary>
/// Incremental generator producing typed writers ([GenerateWriter] → <c>{Dto}Writer</c>) and
/// readers ([GenerateReader] → <c>{Dto}Materializer</c> + <c>{Dto}Reader</c>) for DTO classes
/// with [Column]-annotated properties.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TimeSeriesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Equatable bool keeps the per-target pipelines cacheable across compilation changes.
        var coreReferenced = context.CompilationProvider
            .Select(static (compilation, _) => compilation.GetTypeByMetadataName(KnownNames.CoreWriterType) is not null);

        var writerTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
            KnownNames.GenerateWriterAttribute,
            predicate: static (node, _) => IsCandidate(node),
            transform: static (ctx, ct) => Parser.Parse(ctx, GenerationKind.Writer, ct));

        var readerTargets = context.SyntaxProvider.ForAttributeWithMetadataName(
            KnownNames.GenerateReaderAttribute,
            predicate: static (node, _) => IsCandidate(node),
            transform: static (ctx, ct) => Parser.Parse(ctx, GenerationKind.Reader, ct));

        context.RegisterSourceOutput(
            writerTargets.Combine(coreReferenced),
            static (spc, pair) => EmitTarget(spc, pair.Left, pair.Right));

        context.RegisterSourceOutput(
            readerTargets.Combine(coreReferenced),
            static (spc, pair) => EmitTarget(spc, pair.Left, pair.Right));
    }

    private static bool IsCandidate(SyntaxNode node)
        => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax;

    private static void EmitTarget(SourceProductionContext context, TargetTypeModel model, bool coreReferenced)
    {
        foreach (var diagnostic in model.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (!coreReferenced)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.MissingCoreReference,
                model.Location?.ToLocation() ?? Location.None,
                model.TypeName));
            return;
        }

        if (model.HasErrors || model.Columns.Count == 0)
        {
            return;
        }

        if (model.Kind == GenerationKind.Writer)
        {
            AddSource(context, WriterEmitter.Emit(model));
        }
        else
        {
            AddSource(context, MaterializerEmitter.Emit(model));
            AddSource(context, ReaderEmitter.Emit(model));
        }
    }

    private static void AddSource(SourceProductionContext context, GeneratedSource source)
        => context.AddSource(source.HintName, SourceText.From(source.Code, Encoding.UTF8));
}
