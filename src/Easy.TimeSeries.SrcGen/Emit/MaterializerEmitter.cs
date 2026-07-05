namespace Easy.TimeSeries.SrcGen.Emit;

using Easy.TimeSeries.SrcGen.Model;
using System.Globalization;

/// <summary>
/// Emits <c>{Dto}Materializer : IMaterializer&lt;Dto&gt;</c>. The generic <c>Hydrate</c> uses an
/// <c>is</c>-pattern chain over the value types the DTO actually needs; since the reader calls it
/// with concrete struct type arguments, the JIT specializes each instantiation and folds the type
/// tests away, leaving a plain switch on the column index - no boxing, delegates, or allocations.
/// </summary>
internal static class MaterializerEmitter
{
    public static GeneratedSource Emit(TargetTypeModel model)
    {
        var dto = model.FullyQualifiedName;
        var maxIndex = model.Columns[model.Columns.Count - 1].Index.ToString(CultureInfo.InvariantCulture);
        var code = EmitterCommon.StartFile(model)
            .Line("/// <summary>")
            .Line($"/// Materializes <see cref=\"{model.TypeName}\"/> instances from columnar storage.")
            .Line("/// </summary>")
            .GeneratedCodeAttribute()
            .Open($"public sealed partial class {model.TypeName}Materializer : {EmitterCommon.AbstractionsNs}.IMaterializer<{dto}>")
            .Line($"private {dto}[]? rows;")
            .Line("private int currentColumn = -1;")
            .Line("private int currentRow;")
            .Line()
            .Line("/// <inheritdoc/>")
            .Open("public bool BeginColumn(int column, int rowCount)")
            .Open($"if (column is < 0 or > {maxIndex})")
            .Line("return false;")
            .Close()
            .Line()
            .Line("EnsureRows(rowCount);")
            .Line("currentColumn = column;")
            .Line("currentRow = 0;")
            .Line("return true;")
            .Close()
            .Line();

        EmitStructHydrate(code, model, dto);
        EmitStringHydrate(code, model, dto);

        code.Line("/// <inheritdoc/>")
            .Line($"public {dto}[] GetResult() => rows ?? global::System.Array.Empty<{dto}>();")
            .Line()
            .Open("private void EnsureRows(int rowCount)")
            .Open("if (rows is null)")
            .Line($"rows = new {dto}[rowCount];")
            .Open("for (var i = 0; i < rowCount; i++)")
            .Line($"rows[i] = new {dto}();")
            .Close()
            .Line()
            .Line("return;")
            .Close()
            .Line()
            .Open("if (rowCount > rows.Length)")
            .Line($"var resized = new {dto}[rowCount];")
            .Line("global::System.Array.Copy(rows, resized, rows.Length);")
            .Open("for (var i = rows.Length; i < rowCount; i++)")
            .Line($"resized[i] = new {dto}();")
            .Close()
            .Line()
            .Line("rows = resized;")
            .Close()
            .Close() // EnsureRows
            .Close(); // class

        return new GeneratedSource(EmitterCommon.HintName(model, "Materializer"), code.ToString());
    }

    private static void EmitStructHydrate(CodeBuilder code, TargetTypeModel model, string dto)
    {
        code.Line("/// <inheritdoc/>")
            .Line("public void Hydrate<TPropValue>(TPropValue value)")
            .Line("    where TPropValue : struct")
            .OpenBrace();

        // Group struct columns by their hydrated CLR type, preserving column order.
        var groups = new List<(string ClrType, List<ColumnModel> Columns)>();
        foreach (var column in model.Columns)
        {
            if (column.Kind == ColumnKind.Category)
            {
                continue;
            }

            var clrType = EmitterCommon.ClrType(column.Kind);
            var group = groups.Find(g => g.ClrType == clrType);
            if (group.Columns is null)
            {
                group = (clrType, new List<ColumnModel>());
                groups.Add(group);
            }

            group.Columns.Add(column);
        }

        if (groups.Count > 0)
        {
            code.Line("var instance = rows![currentRow++];");
            var first = true;
            foreach (var (clrType, columns) in groups)
            {
                var variable = HydrateVar(clrType);
                code.Open($"{(first ? "if" : "else if")} (value is {clrType} {variable})")
                    .Open("switch (currentColumn)");
                foreach (var column in columns)
                {
                    code.Line($"case {column.Index.ToString(CultureInfo.InvariantCulture)}:")
                        .Line($"    instance.{column.PropertyName} = {variable};")
                        .Line("    return;");
                }

                code.Close() // switch
                    .Close(); // if / else if
                first = false;
            }

            code.Line();
        }

        code.Line($"throw {EmitterCommon.AbstractionsNs}.TimeSeriesException.UnsupportedHydration(")
            .Line($"    currentColumn, typeof({dto}), typeof(TPropValue));")
            .Close()
            .Line();
    }

    private static void EmitStringHydrate(CodeBuilder code, TargetTypeModel model, string dto)
    {
        var stringColumns = new List<ColumnModel>();
        foreach (var column in model.Columns)
        {
            if (column.Kind == ColumnKind.Category)
            {
                stringColumns.Add(column);
            }
        }

        code.Line("/// <inheritdoc/>")
            .Open("public void Hydrate(string value)");

        if (stringColumns.Count > 0)
        {
            code.Line("var instance = rows![currentRow++];")
                .Open("switch (currentColumn)");
            foreach (var column in stringColumns)
            {
                code.Line($"case {column.Index.ToString(CultureInfo.InvariantCulture)}:")
                    .Line($"    instance.{column.PropertyName} = value;")
                    .Line("    return;");
            }

            code.Close() // switch
                .Line();
        }

        code.Line($"throw {EmitterCommon.AbstractionsNs}.TimeSeriesException.UnsupportedHydration(")
            .Line($"    currentColumn, typeof({dto}), typeof(string));")
            .Close()
            .Line();
    }

    private static string HydrateVar(string clrType)
        => clrType switch
        {
            "int" => "i32",
            "long" => "i64",
            "float" => "f",
            "double" => "d",
            "decimal" => "m",
            "bool" => "b",
            "global::System.DateTime" => "dt",
            "global::System.TimeSpan" => "ts",
            _ => "v",
        };
}
