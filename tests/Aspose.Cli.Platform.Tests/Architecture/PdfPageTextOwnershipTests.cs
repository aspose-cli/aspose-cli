using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>
/// That the page text a PDF command read leaves out the values of the filled form fields on its
/// pages is said where the command reads that text: <c>PdfPages.TextReading</c>, which reads,
/// joins and searches the text of the pages a command returns or searches. So the readers of page
/// text (<c>PdfPages.ExtractText</c>, <c>PdfPages.DocumentText</c> and
/// <c>PdfTextMatch.MatchText</c>) are reached outside <c>PdfPages</c> only by the members listed
/// here, which return no page text, and <c>FORM_VALUES_NOT_IN_TEXT</c> is built nowhere else: no
/// command gives the warning, or forgets it, with a branch of its own. The rules read these names
/// and the qualified name <c>PdfDiagnostics.FormValuesNotInText</c> only.
/// </summary>
public sealed class PdfPageTextOwnershipTests
{
    private const string Owner = "PdfPages.cs";

    // rule-exemptions: begin
    /// <summary>The members that read page text for another purpose than a result of it, by file and member, with the reason.</summary>
    private static readonly Dictionary<string, string> Readers = new(StringComparer.Ordinal)
    {
        ["PdfTextMatch.cs: MatchText(Page, string, bool, bool, Func<string, Exception>)"] = "The check that a pattern matches no empty string reads the page text and returns none of it.",
        ["PdfMutationHandlers.Content.cs: Apply(RedactTextOp)"] = "redact_text finds the matches it removes and returns no text.",
        ["PdfRedactionAudit.cs: PageText(Page, LicenseState)"] = "The redaction audit searches an edit's output for the text it redacted and returns none of it.",
        ["PdfConvert.cs: ConvertText(PdfSession, Document, IReadOnlyList<int>, PdfConvertRequest)"] = "A conversion names the form values its output leaves out through ContentLoss (D2).",
    };
    // rule-exemptions: end

    [Fact]
    public void FormValuesNotInText_IsBuiltOnlyByPdfPages()
    {
        (OwnedSourceFile File, MemberAccessExpressionSyntax Node)[] uses = [.. PdfFiles()
            .SelectMany(static file => file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Where(static access => access.Name.Identifier.ValueText == "FormValuesNotInText" && access.Expression.ToString() == "PdfDiagnostics")
                .Select(access => (file, access)))];

        Assert.Contains(uses, static use => Path.GetFileName(use.File.Path) == Owner);
        OwnershipSourceFiles.AssertNone(
            uses.Where(static use => Path.GetFileName(use.File.Path) != Owner).Select(static use => use.File.At(use.Node, use.Node.ToString())),
            "FORM_VALUES_NOT_IN_TEXT is built only by PdfPages.TextReading; read the pages whose text a command returns or searches through it:");
    }

    [Fact]
    public void PageText_IsReadForAResultOnlyThroughTextReading()
    {
        OwnershipSourceFiles.AssertNone(
            Reaches().Where(static found => !Readers.ContainsKey(found.Key)).Select(static found => found.File.At(found.Node, found.Key)),
            "A PDF command reads the page text it returns or searches through PdfPages.TextReading; a member that reads it for another purpose is listed with its reason:");
    }

    [Fact]
    public void EveryListedReader_StillReadsPageText()
    {
        HashSet<string> found = [.. Reaches().Select(static found => found.Key)];
        string[] stale = [.. Readers.Keys.Where(key => !found.Contains(key))];
        Assert.True(stale.Length == 0, "Listed readers that no longer read page text: " + string.Join(", ", stale));
    }

    private static IEnumerable<OwnedSourceFile> PdfFiles() => OwnershipSourceFiles.Products.Where(static file => file.Product == "Pdf");

    /// <summary>
    /// Each place outside <c>PdfPages</c> that names a reader of page text (a call, or a method
    /// group), keyed by its file and member.
    /// </summary>
    private static IEnumerable<(OwnedSourceFile File, SyntaxNode Node, string Key)> Reaches() =>
        PdfFiles()
            .Where(static file => Path.GetFileName(file.Path) != Owner)
            .SelectMany(static file => file.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(static name => name.Identifier.ValueText is "ExtractText" or "DocumentText" or "MatchText")
                .Select(node => (file, (SyntaxNode)node, $"{Path.GetFileName(file.Path)}: {MemberName(node)}")));

    // The name of the method that holds a node, with its parameter types; a lambda counts as its enclosing method.
    private static string MemberName(SyntaxNode node) => node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } method
        ? $"{method.Identifier.ValueText}({string.Join(", ", method.ParameterList.Parameters.Select(static parameter => parameter.Type?.ToString()))})"
        : "?";
}
