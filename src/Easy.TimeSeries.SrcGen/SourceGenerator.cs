namespace Easy.TimeSeries.SrcGen;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using static Diagnostics;

[Generator]
public class SourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver || receiver.IsEmpty)
        {
            return;
        }

        var sb = new StringBuilder();

        foreach (var cds in receiver.Readers)
        {
            var target = CreateTarget(context, cds);
            if (target is null)
            {
                continue;
            }

            GenerateReader(context, target, sb);
            sb.Clear();
        }

        foreach (var cds in receiver.Writers)
        {
            var target = CreateTarget(context, cds);
            if (target is null)
            {
                continue;
            }

            GenerateWriter(context, target, sb);
            sb.Clear();
        }
    }

    private static TargetClass? CreateTarget(GeneratorExecutionContext context, ClassDeclarationSyntax cds)
    {
        var className = cds.Identifier.ValueText;
        var @namespace = cds.GetNamespace();
        var propertySymbols = cds.GetPropertySymbols(
            context.Compilation,
            filter: symbol => symbol.IsValidColumnProperty(context, className!));
        if (propertySymbols.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(NoEligibleProperties, Location.None, className));
            return null;
        }

        return new TargetClass(className, @namespace, propertySymbols);
    }

    private static void GenerateReader(GeneratorExecutionContext context, TargetClass target, StringBuilder sb)
    {

    }

    private static void GenerateWriter(GeneratorExecutionContext context, TargetClass target, StringBuilder sb)
    {

    }

    private class TargetClass(
        string name,
        string @namespace,
        List<IPropertySymbol> props)
    {
        public string Name => name;

        public string Namespace => @namespace;

        public List<IPropertySymbol> Props => props;
    }
}
