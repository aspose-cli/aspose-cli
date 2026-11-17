using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An output a PDF command does not write points to the command that writes it: Words for an
/// editable docx, which the docx format declares, and the PDF conversion for every other format.
/// </summary>
public sealed class PdfOutputFormatTests : IDisposable
{
    private const string SetTitle = """{"ops":[{"op":"set_properties","title":"Edited"}]}""";
    private const string PasswordVariable = "PDF_INPUT_PASSWORD";
    private readonly TempWorkspace _workspace = new();

    public PdfOutputFormatTests()
    {
        ScenarioLicense.Project(_workspace.Path);
        using (var document = new Aspose.Pdf.Document())
        {
            document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("First part."));
            document.Save(_workspace.File("in.pdf"));
        }

        File.WriteAllText(_workspace.File("x.json"), SetTitle);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EditToADocx_IsRefused()
    {
        CliResult refused = Run("pdf", "edit", "in.pdf", "--ops", "x.json", "--out", "y.docx");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("docx", error["details"]!["format"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("y.docx")));
    }

    /// <summary>Every other format the PDF conversion writes is refused too.</summary>
    [Theory]
    [InlineData("y.html", "html")]
    [InlineData("y.xlsx", "xlsx")]
    [InlineData("y.pptx", "pptx")]
    [InlineData("y.md", "md")]
    public void EditToAnotherConvertedFormat_IsRefused(string output, string format)
    {
        CliResult refused = Run("pdf", "edit", "in.pdf", "--ops", "x.json", "--out", output);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(format, error["details"]!["format"]!.GetValue<string>());
    }

    private CliResult Run(params string[] arguments) => _workspace.Run([.. arguments, "--output-format", "json"]);
}
