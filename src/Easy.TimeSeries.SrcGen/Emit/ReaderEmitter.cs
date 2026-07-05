namespace Easy.TimeSeries.SrcGen.Emit;

using Easy.TimeSeries.SrcGen.Model;

/// <summary>
/// Emits <c>{Dto}Reader : IReader&lt;Dto&gt;</c>, a thin dispatcher pairing the columnar
/// <c>Easy.TimeSeries.Reader</c> with the generated materializer.
/// </summary>
internal static class ReaderEmitter
{
    public static GeneratedSource Emit(TargetTypeModel model)
    {
        var dto = model.FullyQualifiedName;
        var code = EmitterCommon.StartFile(model)
            .Line("/// <summary>")
            .Line($"/// Reads arrays of <see cref=\"{model.TypeName}\"/> from columnar storage.")
            .Line("/// </summary>")
            .GeneratedCodeAttribute()
            .Open($"public sealed partial class {model.TypeName}Reader : {EmitterCommon.AbstractionsNs}.IReader<{dto}>")
            .Line("/// <inheritdoc/>")
            .Line($"public global::System.Threading.Tasks.Task<{dto}[]> ReadAsync(")
            .Line($"    {EmitterCommon.AbstractionsNs}.ReadOptions options,")
            .Line($"    {EmitterCommon.AbstractionsNs}.IReadStorage storage,")
            .Line("    global::System.Threading.CancellationToken cancellationToken = default)")
            .Line($"    => {EmitterCommon.CoreNs}.Reader.ReadFromAsync(")
            .Line("        storage,")
            .Line($"        new {model.TypeName}Materializer(),")
            .Line("        options,")
            .Line("        cancellationToken);")
            .Close(); // class

        return new GeneratedSource(EmitterCommon.HintName(model, "Reader"), code.ToString());
    }
}
