namespace Easy.TimeSeries.SrcGen;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    internal static readonly DiagnosticDescriptor NoEligibleProperties = new(
        id: "TS1001",
        title: "No eligible properties",
        messageFormat: "No eligible properties on class {0}",
        category: "SourceGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        id: "TS1002",
        title: "Unsupported property type",
        messageFormat: "Unsupported type {0} at property {1}.{2}",
        category: "SourceGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PropertySetNotAccessible = new(
        id: "TS1003",
        title: "Property set method is not accessible",
        messageFormat: "Property {0}.{1} set method is not accessible",
        category: "SourceGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
