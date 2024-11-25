namespace Easy.TimeSeries.SrcGen;

using Easy.TimeSeries.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Diagnostics;

internal static class Extensions
{
    internal static IEnumerable<ClassDeclarationSyntax> GetClassesMarkedWith(
        this SyntaxTree syntaxTree,
        string attributeName)
    {
        var attrName = attributeName.EndsWith("Attribute", StringComparison.Ordinal)
            ? attributeName.Substring(0, -9)
            : attributeName;

        return syntaxTree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(tds => HasAttribute(tds, attrName));
    }

    internal static bool HasAttribute(this TypeDeclarationSyntax tds, string attributeName)
    {
        static bool HasName(AttributeSyntax attr, string name)
            => attr.Name.ToString() == name;

        return tds.AttributeLists.Any(als => als.Attributes.Any(attr => HasName(attr, attributeName)));
    }

    internal static bool ShouldGenerateReader(this ClassDeclarationSyntax cds)
    {
        return cds.AttributeLists.Any(
            als => als.Attributes.Any(attr => attr.Name.ToString() == nameof(GenerateReaderAttribute)));
    }

    internal static bool ShouldGenerateWriter(this ClassDeclarationSyntax cds)
    {
        return cds.AttributeLists.Any(
            als => als.Attributes.Any(attr => attr.Name.ToString() == nameof(GenerateWriterAttribute)));
    }

    internal static List<IPropertySymbol> GetPropertySymbols(
        this ClassDeclarationSyntax cds,
        Compilation compilation,
        Func<IPropertySymbol, bool>? filter = null)
    {
        var result = new List<IPropertySymbol>();
        var properties = cds.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var pds in properties)
        {
            var symbol = (IPropertySymbol)compilation.GetSemanticModel(pds.SyntaxTree).GetDeclaredSymbol(pds)!;
            if (filter is not null && filter(symbol))
            {
                result.Add(symbol);
            }
        }

        return result;
    }

    internal static bool HasInterface(this ClassDeclarationSyntax cds, string interfaceName)
    {
        if (cds.BaseList is null)
        {
            return false;
        }

        var baseTypes = cds.BaseList.Types.Select(x => x);
        return baseTypes.Any(baseType => baseType.ToString() == interfaceName);
    }

    private const string Indent = "    ";

    internal static void AppendIndented(this StringBuilder sb, int level, string message)
    {
        for (var i = 0; i < level; i++)
        {
            sb.Append(Indent);
        }

        sb.Append(message);
    }

    internal static void AppendLineIndented(this StringBuilder sb, int level, string message)
    {
        for (int i = 0; i < level; i++)
        {
            sb.Append(Indent);
        }

        sb.AppendLine(message);
    }

    private static readonly string[] ValidColumnTypes =
    [
        "Float", "Double", "Int32", "Int64", "Boolean", "TimeSpan", "DateTime", "String",
    ];

    internal static bool IsValidColumnProperty(
        this IPropertySymbol symbol,
        GeneratorExecutionContext context,
        string className)
    {
        var hasAttr = symbol.GetAttributes()
            .Select(attr => attr.AttributeClass?.Name).Any(name => name == "ColumnAttribute");
        if (!hasAttr)
        {
            return false;
        }

        var accessibility = symbol.SetMethod?.DeclaredAccessibility;
        var isAccessible = accessibility is Accessibility.Public or Accessibility.Internal;
        if (!isAccessible)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(PropertySetNotAccessible, Location.None, className, symbol.Name));
            return false;
        }

        if (!ValidColumnTypes.Contains(symbol.Type.Name))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(UnsupportedPropertyType, Location.None, symbol.Type.Name, className, symbol.Name));
            return false;
        }

        return true;
    }

    internal static string GetNamespace(this ClassDeclarationSyntax cds)
    {
        return cds.Parent switch
        {
            NamespaceDeclarationSyntax nds => nds.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax fnds => fnds.Name.ToString(),
            _ => string.Empty,
        };
    }
}
