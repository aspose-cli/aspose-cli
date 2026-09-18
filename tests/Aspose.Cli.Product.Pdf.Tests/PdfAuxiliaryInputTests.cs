using System.Globalization;
using System.Text.Json;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfAuxiliaryInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedAttachmentAbortsTheEntireEdit(bool bestEffort)
    {
        using var workspace = new TempWorkspace();
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Body"));
            document.Save(workspace.File("source.pdf"));
        }
        File.WriteAllBytes(workspace.File("attachment.bin"), new byte[128 * 1024]);
        long limit = Math.Max(16384, new FileInfo(workspace.File("source.pdf")).Length + 1024);
        Assert.True(limit < 128 * 1024);
        string ops = JsonSerializer.Serialize(new { ops = new[] { new
        { op = "add_attachment", path = workspace.File("attachment.bin") } } });
        var args = new List<string> { "pdf", "edit", "source.pdf", "--ops", ops, "--out", "result.pdf",
            "--max-input-bytes", limit.ToString(CultureInfo.InvariantCulture), "--output", "json" };
        if (bestEffort) { args.AddRange(["--best-effort", "--timeout", "60"]); }
        CliResult result = workspace.Run(args.ToArray());
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("FILE_TOO_LARGE", result.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("result.pdf")));
    }
}
