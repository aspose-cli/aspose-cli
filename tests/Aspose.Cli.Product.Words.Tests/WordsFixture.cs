using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Aspose.Words.Saving;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Shared real-engine setup for Words tests.</summary>
public sealed class WordsFixture : IDisposable
{
    public WordsFixture()
    {
        LicenseResolution resolution = LicenseResolver.Resolve(
            flagPath: null,
            productId: "words",
            Environment.GetEnvironmentVariable,
            Directory.GetCurrentDirectory(),
            LicenseResolver.DefaultUserConfigDirectory());
        Gate = new WordsLicenseGate(resolution, Environment.GetEnvironmentVariable);
    }

    public ILicenseGate Gate { get; }

    internal WordsDocumentEngine Engine =>
        ProductTestBudgets.StartEngine<WordsModule, WordsDocumentEngine>(
            (budgets, writer) => new WordsDocumentEngine(Gate, budgets, writer));

    public TempDirectory Temp { get; } = new();

    public LicenseState LicenseState => Gate.EnsureApplied();

    public string CreateReport(string fileName = "report.docx")
    {
        Gate.EnsureApplied();
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
        Gate.EnsureApplied();
        var document = new Document();
        new DocumentBuilder(document).Write("Encrypted portable document");
        string path = Temp.File(fileName);
        document.Save(path, new OoxmlSaveOptions(SaveFormat.Docx) { Password = password });
        return path;
    }

    public string CreateTwoSectionDocument(string fileName = "sections.docx")
    {
        Gate.EnsureApplied();
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
