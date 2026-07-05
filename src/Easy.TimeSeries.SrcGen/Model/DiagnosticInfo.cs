namespace Easy.TimeSeries.SrcGen.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Equatable replacement for <see cref="Location"/> so diagnostics can live inside
/// cached incremental models without retaining syntax trees.
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(Location? location)
        => location?.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
}

/// <summary>
/// Equatable, tree-free description of a diagnostic to report from <c>RegisterSourceOutput</c>.
/// Descriptors are static singletons, so holding the reference keeps the record equatable.
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, args);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] args)
        => new(descriptor, location, new EquatableArray<string>(args));
}
