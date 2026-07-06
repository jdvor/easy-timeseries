namespace Easy.TimeSeries.SrcGen.Emit;

using Easy.TimeSeries.SrcGen.Model;
using System.Globalization;

/// <summary>
/// Emits <c>{Dto}Writer : IWriter&lt;Dto&gt;</c>. The writer makes a single pass over the source
/// collection into pooled per-column arrays (cache-friendly, no per-column re-enumeration, no
/// enumerator/closure allocations), then feeds exact-length segments to the columnar
/// <c>Easy.TimeSeries.Writer</c>. Exact length matters: <c>AddCategory</c> enumerates its input
/// fully, so slack from <c>ArrayPool</c> must never be visible to it.
/// </summary>
internal static class WriterEmitter
{
    public static GeneratedSource Emit(TargetTypeModel model)
    {
        var dto = model.FullyQualifiedName;
        var code = EmitterCommon.StartFile(model)
            .Line("/// <summary>")
            .Line($"/// Writes collections of <see cref=\"{model.TypeName}\"/> into columnar storage.")
            .Line("/// Columns are written in ascending column-index order as required by the storage format.")
            .Line("/// The underlying columnar writer requires at least 2 rows.")
            .Line("/// </summary>")
            .GeneratedCodeAttribute()
            .Open($"public sealed partial class {model.TypeName}Writer : {EmitterCommon.AbstractionsNs}.IWriter<{dto}>")
            .Line("/// <inheritdoc/>")
            .Line("public async global::System.Threading.Tasks.Task WriteAsync(")
            .Line($"    global::System.Collections.Generic.ICollection<{dto}> data,")
            .Line($"    {EmitterCommon.AbstractionsNs}.IWriteStorage storage,")
            .Line("    global::System.Threading.CancellationToken cancellationToken = default)")
            .OpenBrace()
            .Open("if (data is null)")
            .Line("throw new global::System.ArgumentNullException(nameof(data));")
            .Close()
            .Line()
            .Line("var rows = data.Count;");

        foreach (var column in model.Columns)
        {
            var clrType = EmitterCommon.ClrType(column.Kind);
            code.Line($"var {Var(column)} = global::System.Buffers.ArrayPool<{clrType}>.Shared.Rent(rows);");
        }

        code.Open("try")
            .Line("var i = 0;")
            .Open("foreach (var row in data)");

        foreach (var column in model.Columns)
        {
            code.Line($"{Var(column)}[i] = row.{column.PropertyName};");
        }

        code.Line("i++;")
            .Close() // foreach
            .Line()
            .Line($"using var writer = new {EmitterCommon.CoreNs}.Writer(rows);");

        foreach (var column in model.Columns)
        {
            code.Line($"writer.{AddCall(column)};");
        }

        code.Line()
            .Line("await writer.WriteToAsync(storage, cancellationToken).ConfigureAwait(false);")
            .Close() // try
            .Open("finally");

        foreach (var column in model.Columns)
        {
            var clrType = EmitterCommon.ClrType(column.Kind);
            var clear = column.Kind == ColumnKind.Category ? ", clearArray: true" : string.Empty;
            code.Line($"global::System.Buffers.ArrayPool<{clrType}>.Shared.Return({Var(column)}{clear});");
        }

        code.Close() // finally
            .Close() // WriteAsync
            .Close(); // class

        return new GeneratedSource(EmitterCommon.HintName(model, "Writer"), code.ToString());
    }

    private static string Var(in ColumnModel column) => "c" + column.Index.ToString(CultureInfo.InvariantCulture);

    private static string AddCall(in ColumnModel column)
    {
        var segment = $"new global::System.ArraySegment<{EmitterCommon.ClrType(column.Kind)}>({Var(column)}, 0, rows)";
        var label = EmitterCommon.Literal(column.Label);
        var precision = $"{EmitterCommon.CoreNs}.TimePrecision.{column.TimePrecisionName}";
        return column.Kind switch
        {
            ColumnKind.Category => $"AddCategory({segment}, {label})",
            ColumnKind.Int32 => $"AddInt32({segment}, {label})",
            ColumnKind.Int64 => $"AddInt64({segment}, {label})",
            ColumnKind.Float => $"AddFloat({segment}, {label})",
            ColumnKind.Double => $"AddDouble({segment}, {label})",
            ColumnKind.FloatRaw => $"AddFloatRandom({segment}, {label})",
            ColumnKind.DoubleRaw => $"AddDoubleRandom({segment}, {label})",
            ColumnKind.Int64Raw => $"AddInt64Random({segment}, {label})",
            ColumnKind.Int32Raw => $"AddInt32Random({segment}, {label})",
            ColumnKind.Decimal => $"AddDecimal({segment}, {label})",
            ColumnKind.Bool => $"AddBool({segment}, {label})",
            ColumnKind.ScaledNumber32 =>
                $"AddScaledNumber32({segment}, {label}, {column.DecimalPlaces.ToString(CultureInfo.InvariantCulture)})",
            ColumnKind.ScaledNumber64 =>
                $"AddScaledNumber64({segment}, {label}, {column.DecimalPlaces.ToString(CultureInfo.InvariantCulture)})",
            ColumnKind.DateTimeOrdered => $"AddTimeOrdered({segment}, {label}, {precision})",
            ColumnKind.DateTimeUnordered => $"AddTimeUnordered({segment}, {label}, {precision})",
            ColumnKind.TimeSpan => $"AddInterval({segment}, {label}, {precision})",
            _ => throw new InvalidOperationException($"No writer call for column kind {column.Kind}."),
        };
    }
}
