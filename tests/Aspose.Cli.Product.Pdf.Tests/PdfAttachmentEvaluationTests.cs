using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An attachment is copied as the PDF stores it, so its extraction without a license carries
/// nothing evaluation mode adds and is not disclosed.
/// </summary>
public sealed class PdfAttachmentEvaluationTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void ExtractAttachments_InEvaluationMode_IsNotDisclosed()
    {
        PdfEngineFixture.WriteRawPdf(_workspace.File("report.pdf"),
        [
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(data.csv) 5 0 R] >> >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Type /Filespec /F (data.csv) /UF (data.csv) /EF << /F 6 0 R >> >>",
            "<< /Type /EmbeddedFile /Length 9 >>\nstream\nregion,10\nendstream",
        ]);

        CliResult extracted = _workspace.Run("pdf", "extract", "report.pdf", "--what", "attachments", "--out-dir", "files", "--output-format", "json");

        Assert.True(extracted.ExitCode == 0, extracted.StdErr);
        JsonNode result = extracted.Json();
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        Assert.Equal("region,10", File.ReadAllText(Assert.Single(result["outputs"]!.AsArray())!["path"]!.GetValue<string>()));
        Assert.DoesNotContain(
            (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!["code"]!.GetValue<string>()),
            static code => code.StartsWith("EVALUATION_", StringComparison.Ordinal));
    }
}
