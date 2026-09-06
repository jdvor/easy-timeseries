namespace Easy.TimeSeries.SrcGen;

using Easy.TimeSeries.SrcGen.Model;
using Microsoft.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Binds an annotated DTO type symbol to an equatable <see cref="TargetTypeModel"/>,
/// applying the CLR-type/attribute mapping and all ETS validations.
/// </summary>
internal static class Parser
{
    /// <summary>Member names shared by DateTimePrecision, TimeSpanPrecision and core TimePrecision.</summary>
    private static readonly string[] TimePrecisionNames = ["Milliseconds", "TenthsOfSecond", "Seconds", "Days"];

    private const int DefaultDateTimePrecision = 0; // Milliseconds
    private const int DefaultTimeSpanPrecision = 2; // Seconds
    private const int DateTimeSortAscending = 1;
    private const int NumberDistributionRandom = 1;

    public static TargetTypeModel Parse(GeneratorAttributeSyntaxContext ctx, GenerationKind kind, CancellationToken ct)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var location = LocationInfo.From(ctx.TargetNode.GetLocation());
        var diagnostics = new List<DiagnosticInfo>();

        ValidateTargetType(symbol, kind, location, diagnostics);

        var columns = new List<ColumnModel>();
        foreach (var property in GetInstanceProperties(symbol))
        {
            ct.ThrowIfCancellationRequested();
            var attribute = FindColumnAttribute(property);
            if (attribute is null)
            {
                continue;
            }

            if (BindColumn(property, attribute, kind, diagnostics) is { } column)
            {
                columns.Add(column);
            }
        }

        columns.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        ValidateIndexes(symbol, columns, location, diagnostics);

        var ns = symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();
        return new TargetTypeModel(
            ns,
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            kind,
            new EquatableArray<ColumnModel>([.. columns]),
            new EquatableArray<DiagnosticInfo>([.. diagnostics]),
            location);
    }

    private static void ValidateTargetType(
        INamedTypeSymbol symbol,
        GenerationKind kind,
        LocationInfo? location,
        List<DiagnosticInfo> diagnostics)
    {
        var kindName = kind == GenerationKind.Writer ? "writer" : "reader";

        void Report(string reason)
            => diagnostics.Add(DiagnosticInfo.Create(Diagnostics.UnusableTargetType, location, symbol.Name, kindName, reason));

        if (symbol.TypeKind != TypeKind.Class)
        {
            Report("it must be a class");
            return;
        }

        if (symbol.Arity > 0)
        {
            Report("it must not be generic");
        }

        if (symbol.ContainingType is not null)
        {
            Report("it must not be nested in another type");
        }

        if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            Report("it must be public or internal");
        }

        if (kind == GenerationKind.Reader)
        {
            if (symbol.IsAbstract)
            {
                Report("it must not be abstract");
            }
            else if (!HasPublicParameterlessCtor(symbol))
            {
                Report("it must have a public parameterless constructor");
            }
        }
    }

    private static bool HasPublicParameterlessCtor(INamedTypeSymbol symbol)
    {
        foreach (var ctor in symbol.InstanceConstructors)
        {
            if (ctor.Parameters.IsEmpty && ctor.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IPropertySymbol> GetInstanceProperties(INamedTypeSymbol symbol)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var type = symbol; type is not null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol { IsStatic: false, IsIndexer: false } property && seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static AttributeData? FindColumnAttribute(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == KnownNames.ColumnAttribute)
            {
                return attribute;
            }
        }

        return null;
    }

    private static ColumnModel? BindColumn(
        IPropertySymbol property,
        AttributeData attribute,
        GenerationKind kind,
        List<DiagnosticInfo> diagnostics)
    {
        var location = LocationInfo.From(property.Locations.Length > 0 ? property.Locations[0] : null);

        var index = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int i ? i : -1;
        var args = ColumnArgs.From(attribute);

        var type = property.Type;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
            || (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NullableColumnProperty, location, property.Name));
            return null;
        }

        var clrKind = ClassifyClrType(type);
        if (clrKind == ColumnKind.None)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.UnsupportedPropertyType, location, property.Name, type.ToDisplayString()));
            return null;
        }

        var kindResolved = ResolveColumnKind(property, clrKind, args, location, diagnostics);
        if (kindResolved is not { } columnKind)
        {
            return null;
        }

        ReportIgnoredOptions(property, columnKind, clrKind, args, location, diagnostics);

        if (!ValidateAccessors(property, kind, location, diagnostics))
        {
            return null;
        }

        var label = string.IsNullOrEmpty(args.Label) ? property.Name : args.Label!;
        var timePrecisionName = columnKind switch
        {
            ColumnKind.DateTimeOrdered or ColumnKind.DateTimeUnordered => TimePrecisionName(args.DateTimePrecision),
            ColumnKind.TimeSpan => TimePrecisionName(args.TimeSpanPrecision),
            _ => string.Empty,
        };
        var decimalPlaces = columnKind is ColumnKind.ScaledNumber32 or ColumnKind.ScaledNumber64 ? args.NumberPrecision : 0;

        return new ColumnModel(index, property.Name, columnKind, label, timePrecisionName, decimalPlaces);
    }

    private static ColumnKind ClassifyClrType(ITypeSymbol type)
        => type.SpecialType switch
        {
            SpecialType.System_String => ColumnKind.Category,
            SpecialType.System_Int32 => ColumnKind.Int32,
            SpecialType.System_Int64 => ColumnKind.Int64,
            SpecialType.System_Single => ColumnKind.Float,
            SpecialType.System_Double => ColumnKind.Double,
            SpecialType.System_Decimal => ColumnKind.Decimal,
            SpecialType.System_Boolean => ColumnKind.Bool,
            SpecialType.System_DateTime => ColumnKind.DateTimeOrdered,
            _ => type.ToDisplayString() == KnownNames.TimeSpanType ? ColumnKind.TimeSpan : ColumnKind.None,
        };

    /// <summary>
    /// Resolves the effective column kind from the CLR-derived kind plus attribute options,
    /// reporting ETS005/ETS006 as needed. Returns null when binding failed.
    /// </summary>
    private static ColumnKind? ResolveColumnKind(
        IPropertySymbol property,
        ColumnKind clrKind,
        ColumnArgs args,
        LocationInfo? location,
        List<DiagnosticInfo> diagnostics)
    {
        ColumnKind resolved;
        if (args.ValueType != 0)
        {
            resolved = (ColumnKind)args.ValueType;
            if (!IsCompatible(resolved, clrKind))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.IncompatibleValueType,
                    location,
                    resolved.ToString(),
                    property.Name,
                    property.Type.ToDisplayString()));
                return null;
            }
        }
        else
        {
            resolved = clrKind switch
            {
                ColumnKind.Float when args.NumberPrecision > 0 => ColumnKind.ScaledNumber32,
                ColumnKind.Double when args.NumberPrecision > 0 => ColumnKind.ScaledNumber64,
                ColumnKind.Float when args.NumberDistribution == NumberDistributionRandom => ColumnKind.FloatRaw,
                ColumnKind.Double when args.NumberDistribution == NumberDistributionRandom => ColumnKind.DoubleRaw,
                ColumnKind.Int64 when args.NumberDistribution == NumberDistributionRandom => ColumnKind.Int64Raw,
                ColumnKind.Int32 when args.NumberDistribution == NumberDistributionRandom => ColumnKind.Int32Raw,
                ColumnKind.DateTimeOrdered when args.DateTimeSort != DateTimeSortAscending => ColumnKind.DateTimeUnordered,
                _ => clrKind,
            };
        }

        if (resolved is ColumnKind.ScaledNumber32 or ColumnKind.ScaledNumber64 && args.NumberPrecision == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.MissingNumberPrecision, location, property.Name, resolved.ToString()));
            return null;
        }

        return resolved;
    }

    private static bool IsCompatible(ColumnKind explicitKind, ColumnKind clrKind)
        => explicitKind switch
        {
            ColumnKind.Category => clrKind == ColumnKind.Category,
            ColumnKind.Int32 => clrKind == ColumnKind.Int32,
            ColumnKind.Int64 => clrKind == ColumnKind.Int64,
            ColumnKind.Float or ColumnKind.ScaledNumber32 => clrKind == ColumnKind.Float,
            ColumnKind.Double or ColumnKind.ScaledNumber64 => clrKind == ColumnKind.Double,
            ColumnKind.Decimal => clrKind == ColumnKind.Decimal,
            ColumnKind.Bool => clrKind == ColumnKind.Bool,
            ColumnKind.DateTimeOrdered or ColumnKind.DateTimeUnordered => clrKind == ColumnKind.DateTimeOrdered,
            ColumnKind.TimeSpan => clrKind == ColumnKind.TimeSpan,
            ColumnKind.FloatRaw => clrKind == ColumnKind.Float,
            ColumnKind.DoubleRaw => clrKind == ColumnKind.Double,
            ColumnKind.Int64Raw => clrKind == ColumnKind.Int64,
            ColumnKind.Int32Raw => clrKind == ColumnKind.Int32,
            _ => false,
        };

    private static void ReportIgnoredOptions(
        IPropertySymbol property,
        ColumnKind columnKind,
        ColumnKind clrKind,
        ColumnArgs args,
        LocationInfo? location,
        List<DiagnosticInfo> diagnostics)
    {
        void Ignored(string option)
            => diagnostics.Add(DiagnosticInfo.Create(Diagnostics.IgnoredAttributeOption, location, option, property.Name));

        var isDateTime = clrKind == ColumnKind.DateTimeOrdered;
        if (args.HasDateTimePrecision && !isDateTime)
        {
            Ignored(KnownNames.ColumnArgs.DateTimePrecision);
        }

        if (args.HasDateTimeSort
            && (!isDateTime || (columnKind == ColumnKind.DateTimeUnordered && args.DateTimeSort == DateTimeSortAscending)))
        {
            Ignored(KnownNames.ColumnArgs.DateTimeSort);
        }

        if (args.HasTimeSpanPrecision && clrKind != ColumnKind.TimeSpan)
        {
            Ignored(KnownNames.ColumnArgs.TimeSpanPrecision);
        }

        if (args.HasNumberPrecision && columnKind is not (ColumnKind.ScaledNumber32 or ColumnKind.ScaledNumber64))
        {
            Ignored(KnownNames.ColumnArgs.NumberPrecision);
        }

        if (args.HasNumberDistribution
            && columnKind is not (ColumnKind.FloatRaw or ColumnKind.DoubleRaw or ColumnKind.Int64Raw or ColumnKind.Int32Raw))
        {
            Ignored(KnownNames.ColumnArgs.NumberDistribution);
        }
    }

    private static bool ValidateAccessors(
        IPropertySymbol property,
        GenerationKind kind,
        LocationInfo? location,
        List<DiagnosticInfo> diagnostics)
    {
        static bool Accessible(IMethodSymbol? accessor)
            => accessor is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal };

        if (kind == GenerationKind.Writer && !Accessible(property.GetMethod))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.InaccessibleAccessor, location, property.Name, "an accessible getter", "writer"));
            return false;
        }

        if (kind == GenerationKind.Reader && (!Accessible(property.SetMethod) || property.SetMethod!.IsInitOnly))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.InaccessibleAccessor, location, property.Name, "an accessible non-init setter", "reader"));
            return false;
        }

        return true;
    }

    private static void ValidateIndexes(
        INamedTypeSymbol symbol,
        List<ColumnModel> columns,
        LocationInfo? location,
        List<DiagnosticInfo> diagnostics)
    {
        if (columns.Count == 0)
        {
            if (diagnostics.Count == 0)
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NoColumns, location, symbol.Name));
            }

            return;
        }

        var duplicates = false;
        for (var i = 1; i < columns.Count; i++)
        {
            if (columns[i].Index == columns[i - 1].Index)
            {
                duplicates = true;
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.DuplicateColumnIndex,
                    location,
                    columns[i].Index.ToString(CultureInfo.InvariantCulture),
                    symbol.Name));
            }
        }

        if (duplicates)
        {
            return;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Index != i)
            {
                var found = string.Join(", ", columns.Select(static c => c.Index.ToString(CultureInfo.InvariantCulture)));
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NonContiguousColumnIndexes, location, symbol.Name, found));
                return;
            }
        }
    }

    private static string TimePrecisionName(int value)
        => value >= 0 && value < TimePrecisionNames.Length ? TimePrecisionNames[value] : TimePrecisionNames[0];

    /// <summary>Named arguments of one ColumnAttribute application, with attribute defaults applied.</summary>
    private readonly struct ColumnArgs
    {
        public int ValueType { get; private init; }

        public string? Label { get; private init; }

        public int DateTimePrecision { get; private init; }

        public int DateTimeSort { get; private init; }

        public int TimeSpanPrecision { get; private init; }

        public int NumberPrecision { get; private init; }

        public int NumberDistribution { get; private init; }

        public bool HasDateTimePrecision { get; private init; }

        public bool HasDateTimeSort { get; private init; }

        public bool HasTimeSpanPrecision { get; private init; }

        public bool HasNumberPrecision { get; private init; }

        public bool HasNumberDistribution { get; private init; }

        public static ColumnArgs From(AttributeData attribute)
        {
            var args = new ColumnArgs
            {
                DateTimePrecision = DefaultDateTimePrecision,
                TimeSpanPrecision = DefaultTimeSpanPrecision,
            };

            foreach (var named in attribute.NamedArguments)
            {
                var value = named.Value.Value;
                args = named.Key switch
                {
                    KnownNames.ColumnArgs.ValueType when value is int v => args with { ValueType = v },
                    KnownNames.ColumnArgs.Label when value is string s => args with { Label = s },
                    KnownNames.ColumnArgs.DateTimePrecision when value is int v
                        => args with { DateTimePrecision = v, HasDateTimePrecision = true },
                    KnownNames.ColumnArgs.DateTimeSort when value is int v
                        => args with { DateTimeSort = v, HasDateTimeSort = true },
                    KnownNames.ColumnArgs.TimeSpanPrecision when value is int v
                        => args with { TimeSpanPrecision = v, HasTimeSpanPrecision = true },
                    KnownNames.ColumnArgs.NumberPrecision when value is int v
                        => args with { NumberPrecision = v, HasNumberPrecision = true },
                    KnownNames.ColumnArgs.NumberDistribution when value is int v
                        => args with { NumberDistribution = v, HasNumberDistribution = true },
                    _ => args,
                };
            }

            return args;
        }
    }
}
