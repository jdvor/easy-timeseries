namespace Easy.TimeSeries.SrcGen;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    private const string Category = "EasyTimeSeries";

    public static readonly DiagnosticDescriptor DuplicateColumnIndex = new(
        "ETS001",
        "Duplicate column index",
        "Column index {0} is used by more than one [Column] property on type '{1}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonContiguousColumnIndexes = new(
        "ETS002",
        "Column indexes must be contiguous",
        "Column indexes on type '{0}' must start at 0 and be contiguous, but found: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        "ETS003",
        "Unsupported column property type",
        "Property '{0}' of type '{1}' cannot be mapped to a column value type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NullableColumnProperty = new(
        "ETS004",
        "Nullable column property",
        "Property '{0}' is nullable; the storage format has no null representation",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatibleValueType = new(
        "ETS005",
        "Incompatible explicit column value type",
        "ValueType '{0}' is not compatible with property '{1}' of type '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingNumberPrecision = new(
        "ETS006",
        "Scaled number requires precision",
        "Property '{0}' mapped to '{1}' requires NumberPrecision set to DecimalPlaces1..DecimalPlaces5",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleAccessor = new(
        "ETS007",
        "Column property accessor not accessible",
        "Property '{0}' must have {1} for {2} generation",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusableTargetType = new(
        "ETS008",
        "Type not usable for generation",
        "Type '{0}' cannot be used for {1} generation: {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IgnoredAttributeOption = new(
        "ETS009",
        "Ignored column attribute option",
        "Attribute option '{0}' has no effect on property '{1}' and is ignored",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoColumns = new(
        "ETS010",
        "No column properties",
        "Type '{0}' has no properties annotated with [Column]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingCoreReference = new(
        "ETS011",
        "Missing Easy.TimeSeries reference",
        "Cannot generate code for type '{0}' because the compilation does not reference Easy.TimeSeries",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
