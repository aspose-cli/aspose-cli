using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Saving;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Shared real-engine setup for Words tests. The test license state is applied before any
/// test authors a document; otherwise an evaluation banner written into an input would
/// become its first block.
/// </summary>
public sealed class WordsFixture : IDisposable
{
    public ILicenseGate Gate { get; } = TestLicense.Apply(
        static (resolution, environment) => new WordsLicenseGate(resolution, environment));

    internal WordsEngine Engine =>
        ProductTestBudgets.StartEngine<WordsModule, WordsEngine>(
            (budgets, writer) => new WordsEngine(Gate, budgets, writer));

    internal WordsFontEnvironment Fonts =>
        ProductTestBudgets.StartEngine<WordsModule, WordsFontEnvironment>(
            (budgets, _) => new WordsFontEnvironment(Gate, budgets));

    public TempDirectory Temp { get; } = new();

    public LicenseState LicenseState => Gate.EnsureApplied();

    /// <summary>
    /// Asserts that a section has no primary header of its own and so continues the previous
    /// one. Evaluation mode writes a header holding only its banner into every section.
    /// </summary>
    internal void AssertNoOwnHeader(Section section)
    {
        HeaderFooter? header = section.HeadersFooters[HeaderFooterType.HeaderPrimary];
        if (LicenseState == LicenseState.Licensed)
        {
            Assert.Null(header);
            return;
        }
        Assert.All(header?.Paragraphs.Cast<Paragraph>() ?? [], static paragraph =>
            Assert.True(WordsEvaluation.IsBanner(paragraph) || paragraph.GetText().Trim().Length == 0, paragraph.GetText()));
    }

    /// <summary>The first paragraph a test or the engine wrote, after any evaluation banner.</summary>
    internal static Paragraph FirstAuthoredParagraph(HeaderFooter header) =>
        header.Paragraphs.Cast<Paragraph>().First(static paragraph => !WordsEvaluation.IsBanner(paragraph));

    /// <summary>A plain-text search of one scope with the default hit budget.</summary>
    internal static WordsSearchRequest Search(string pattern, string scope = WordsTextScopes.Body) =>
        new() { Query = new SearchQuery(TextSearch.Create(pattern, regex: false, caseSensitive: false), 100, scope) };

    public string CreateReport(string fileName = "report.docx")
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Quarterly report");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Writeln("Revenue increased by twelve percent.");
        builder.Writeln("Operations remained stable.");
        builder.StartBookmark("Summary");
        builder.Write("Bookmarked summary");
        builder.EndBookmark("Summary");
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Metric");
        builder.InsertCell();
        builder.Write("Value");
        builder.EndRow();
        builder.InsertCell();
        builder.Write("Revenue");
        builder.InsertCell();
        builder.Write("120");
        builder.EndRow();
        Table table = builder.EndTable();
        table.StyleIdentifier = StyleIdentifier.TableGrid;
        string path = Temp.File(fileName);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    public string CreateEncryptedDocument(string password, string fileName = "secret.docx")
    {
        var document = new Document();
        new DocumentBuilder(document).Write("Encrypted portable document");
        string path = Temp.File(fileName);
        document.Save(path, new OoxmlSaveOptions(SaveFormat.Docx) { Password = password });
        return path;
    }

    public string CreateTwoSectionDocument(string fileName = "sections.docx")
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("First section");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Writeln("Second section");
        string path = Temp.File(fileName);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    public void Dispose() => Temp.Dispose();
}
