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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachmentInTheWorkingDirectoryIsEmbedded(bool absolute)
    {
        using var workspace = new TempWorkspace();
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Body"));
            document.Save(workspace.File("source.pdf"));
        }
        File.WriteAllText(workspace.File("vendor-bank.csv"), "bank,account\nICBC,6222\n");
        string path = absolute ? workspace.File("vendor-bank.csv") : "vendor-bank.csv";
        string ops = JsonSerializer.Serialize(new { ops = new[] { new { op = "add_attachment", path } } });

        CliResult result = workspace.Run("pdf", "edit", "source.pdf", "--ops", ops, "--out", "result.pdf", "--output", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
        using var output = new Document(workspace.File("result.pdf"));
        FileSpecification attachment = Assert.Single(output.EmbeddedFiles.Cast<FileSpecification>());
        Assert.Equal("vendor-bank.csv", attachment.Name);
        using var contents = new MemoryStream();
        attachment.Contents.CopyTo(contents);
        Assert.Equal("bank,account\nICBC,6222\n", System.Text.Encoding.UTF8.GetString(contents.ToArray()));
    }
}
