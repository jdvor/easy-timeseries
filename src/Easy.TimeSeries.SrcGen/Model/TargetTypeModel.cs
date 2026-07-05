namespace Easy.TimeSeries.SrcGen.Model;

using Microsoft.CodeAnalysis;

/// <summary>
/// Everything the emitters need about one annotated DTO type. Fully equatable and free of
/// symbols/syntax so the incremental pipeline caches it. Namespace is null for the global
/// namespace; FullyQualifiedName includes the <c>global::</c> prefix.
/// </summary>
internal sealed record TargetTypeModel(
    string? Namespace,
    string TypeName,
    string FullyQualifiedName,
    GenerationKind Kind,
    EquatableArray<ColumnModel> Columns,
    EquatableArray<DiagnosticInfo> Diagnostics,
    LocationInfo? Location)
{
    public bool HasErrors
    {
        get
        {
            foreach (var diagnostic in Diagnostics)
            {
                if (diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
