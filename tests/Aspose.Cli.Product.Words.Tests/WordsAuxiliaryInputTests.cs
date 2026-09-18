using System.Globalization;
using System.Text.Json;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsAuxiliaryInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedImageAbortsTheEntireEdit(bool bestEffort)
    {
        using var workspace = new TempWorkspace();
        var document = new Document();
        new DocumentBuilder(document).Writeln("Body");
        document.Save(workspace.File("source.docx"));
        document.Cleanup();
        byte[] image = new byte[128 * 1024];
        ResourceHttpServer.Image.CopyTo(image, 0);
        File.WriteAllBytes(workspace.File("image.png"), image);
        long limit = Math.Max(16384, new FileInfo(workspace.File("source.docx")).Length + 1024);
        Assert.True(limit < image.Length);
        string ops = JsonSerializer.Serialize(new { ops = new[] { new
        { op = "insert_image", at = new { block = 1 }, position = "after", path = workspace.File("image.png") } } });
        var args = new List<string> { "words", "edit", "source.docx", "--ops", ops, "--out", "result.docx",
            "--max-input-bytes", limit.ToString(CultureInfo.InvariantCulture), "--output", "json" };
        if (bestEffort) { args.AddRange(["--best-effort", "--timeout", "60"]); }
        CliResult result = workspace.Run(args.ToArray());
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("FILE_TOO_LARGE", result.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("result.docx")));
    }
}
