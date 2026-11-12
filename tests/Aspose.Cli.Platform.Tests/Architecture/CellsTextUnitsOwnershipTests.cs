using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// Where Cells holds text is one enumeration, <c>CellsTextUnits</c>: the code that finds, replaces
/// or counts text, which is every engine file of the Cells product that matches text with the
/// SDK's <c>TextReplacement</c> or <c>TextSearch</c>, reads that text from the enumeration and never
/// from the workbook itself. So such a file reaches none of the members the engine holds text in, or
/// the collections that hold them (<see cref="Holders"/>). The rule reads member names only: a
/// <c>Text</c> read from a shape or a title the file got some other way is not caught.
/// </summary>
public sealed class CellsTextUnitsOwnershipTests
{
    /// <summary>The members a workbook's text is read through: a cell's text, a comment's, a title, a header or footer, and the sheet's collections of them.</summary>
    private static readonly HashSet<string> Holders = new(StringComparer.Ordinal)
    {
        "StringValue", "DisplayStringValue", "Formula", "Note", "Title", "Comments", "Shapes", "Charts", "PageSetup", "InEffect", "Visible",
    };

    [Fact]
    public void CellsText_IsReadForItselfOnlyThroughTheEnumeration()
    {
        OwnershipSourceFiles.AssertNone(Reaches().Select(static found => found.File.At(found.Node, found.Key)),
            "A Cells command reads the text it finds, replaces or counts from CellsTextUnits, never from the workbook itself:");
    }

    /// <summary>
    /// Each member access to a <see cref="Holders"/> name (<c>cell.StringValue</c>, <c>sheet.Comments</c>,
    /// <c>HeaderFooterText.InEffect</c>) in a Cells engine file other than the enumeration's that names
    /// <c>TextReplacement</c> or <c>TextSearch</c>, keyed by its file and member.
    /// </summary>
    private static IEnumerable<(OwnedSourceFile File, SyntaxNode Node, string Key)> Reaches() =>
        OwnershipSourceFiles.Products
            .Where(static file => file.Product == "Cells"
                && file.Relative.Split(Path.DirectorySeparatorChar).Contains("Engine")
                && Path.GetFileName(file.Path) != "CellsTextUnits.cs"
                && file.Root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static name => name.Identifier.ValueText is "TextReplacement" or "TextSearch"))
            .SelectMany(static file => file.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(static name => Holders.Contains(name.Identifier.ValueText) && name.Parent switch
                {
                    MemberAccessExpressionSyntax access => access.Name == name && access.Expression.ToString() != "TextPart",
                    MemberBindingExpressionSyntax binding => binding.Name == name,
                    _ => false,
                })
                .Select(name => (file, (SyntaxNode)name, $"{Path.GetFileName(file.Path)}: {MemberName(name)}")));

    // The name of the method, property or field that holds a node; a local function or lambda counts as its enclosing member.
    private static string MemberName(SyntaxNode node) => node.Ancestors().FirstOrDefault(static ancestor =>
        ancestor is MethodDeclarationSyntax or PropertyDeclarationSyntax or FieldDeclarationSyntax or ConstructorDeclarationSyntax) switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText
            + "(" + string.Join(", ", method.ParameterList.Parameters.Select(static parameter => parameter.Type?.ToString())) + ")",
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        _ => "?",
    };
}
