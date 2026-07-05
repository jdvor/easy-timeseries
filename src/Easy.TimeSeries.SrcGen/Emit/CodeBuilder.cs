namespace Easy.TimeSeries.SrcGen.Emit;

using System.Text;

/// <summary>
/// Minimal indentation-aware code emission helper (4 spaces per level).
/// </summary>
internal sealed class CodeBuilder
{
    private const string IndentUnit = "    ";
    private readonly StringBuilder sb = new(4096);
    private int indent;

    public CodeBuilder Line(string text)
    {
        for (var i = 0; i < indent; i++)
        {
            sb.Append(IndentUnit);
        }

        sb.Append(text);
        sb.Append('\n');
        return this;
    }

    public CodeBuilder Line()
    {
        sb.Append('\n');
        return this;
    }

    /// <summary>Emits the given line followed by an opening brace and increases indentation.</summary>
    public CodeBuilder Open(string text)
    {
        Line(text);
        Line("{");
        indent++;
        return this;
    }

    /// <summary>Emits an opening brace and increases indentation (for multi-line headers).</summary>
    public CodeBuilder OpenBrace()
    {
        Line("{");
        indent++;
        return this;
    }

    /// <summary>Decreases indentation and emits a closing brace (with optional suffix, e.g. ";").</summary>
    public CodeBuilder Close(string suffix = "")
    {
        indent--;
        Line("}" + suffix);
        return this;
    }

    public override string ToString() => sb.ToString();
}
