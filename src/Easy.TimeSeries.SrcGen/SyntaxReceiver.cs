namespace Easy.TimeSeries.SrcGen;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

public class SyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> Readers { get; } = new();

    public List<ClassDeclarationSyntax> Writers { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is ClassDeclarationSyntax cds)
        {
            if (cds.ShouldGenerateReader())
            {
                Readers.Add(cds);
            }

            if (cds.ShouldGenerateWriter())
            {
                Writers.Add(cds);
            }
        }
    }

    public bool IsEmpty => Readers.Count == 0 && Writers.Count == 0;
}
