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
    public void EditToADocx_PointsToTheWordsConversionThatWritesAnEditableDocument()
    {
        CliResult refused = Run("pdf", "edit", "in.pdf", "--ops", "x.json", "--out", "y.docx");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("docx", error["details"]!["format"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.Equal("Give --out one of these extensions, then run 'aspose-cli words convert <that file> --to docx' for docx.", hint);
        Assert.DoesNotContain("pdf convert", hint, StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("y.docx")));

        // The hint works as written: the same edit to a .pdf, then the conversion it names.
        CliResult kept = Run("pdf", "edit", "in.pdf", "--ops", "x.json", "--out", "kept.pdf");
        Assert.True(kept.ExitCode == 0, kept.StdErr);
        CliResult converted = Run("words", "convert", "kept.pdf", "--to", "docx", "--out", "y.docx");
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal("docx", JsonNode.Parse(converted.StdOut)!["outputs"]![0]!["format"]!.GetValue<string>());
    }

    /// <summary>Every other format the PDF conversion writes is still pointed to it.</summary>
    [Theory]
    [InlineData("y.html", "html")]
    [InlineData("y.xlsx", "xlsx")]
    [InlineData("y.pptx", "pptx")]
    [InlineData("y.md", "md")]
    public void EditToAnotherConvertedFormat_StillPointsToThePdfConversion(string output, string format)
    {
        CliResult refused = Run("pdf", "edit", "in.pdf", "--ops", "x.json", "--out", output);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(format, error["details"]!["format"]!.GetValue<string>());
        Assert.Equal(
            $"Give --out one of these extensions, then run 'aspose-cli pdf convert <that file> --to {format}' for {format}.",
            error["hint"]!.GetValue<string>());
    }

    [Fact]
    public void ConvertToDocx_WarnsLossy_AndNamesTheWordsConversionOfTheInputForAnEditableDocument()
    {
        CliResult converted = Run("pdf", "convert", "in.pdf", "--to", "docx", "--out", "framed.docx");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string command = EditableConversion(converted);
        Assert.StartsWith("aspose-cli words convert ", command, StringComparison.Ordinal);
        Assert.Contains(" --to docx --out ", command, StringComparison.Ordinal);
        Assert.Contains(" --overwrite", command, StringComparison.Ordinal);

        // The shown command runs as written: Words converts this input over the file just written.
        AssertWordsReplaces(_workspace.RunCommandLine(command), "in.pdf", "framed.docx");
    }

    /// <summary>
    /// A conversion that names no output writes beside the input, where the Words conversion
    /// would write too: the shown command names that file and replaces it.
    /// </summary>
    [Fact]
    public void ConvertToDocxBesideTheInput_NamesTheWordsConversionThatReplacesThatFile()
    {
        CliResult converted = Run("pdf", "convert", "in.pdf", "--to", "docx");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal(
            Path.GetFullPath(_workspace.File("in.docx")),
            Path.GetFullPath(JsonNode.Parse(converted.StdOut)!["outputs"]![0]!["path"]!.GetValue<string>()),
            ignoreCase: true);
        string command = EditableConversion(converted);

        // The shown command runs as written: Words writes the same in.docx again.
        AssertWordsReplaces(_workspace.RunCommandLine(command), "in.pdf", "in.docx");
    }

    /// <summary>
    /// The Words conversion of an input opened with the password an environment variable holds
    /// names that variable, so it opens the input as written.
    /// </summary>
    [Fact]
    public void ConvertOfAnEncryptedInputToDocx_NamesTheWordsConversionWithItsPasswordVariable()
    {
        const string Password = "editable-secret";
        Dictionary<string, string?> variables = new(StringComparer.Ordinal) { [PasswordVariable] = Password };
        CliResult encrypted = _workspace.RunWithEnv(
            variables, "pdf", "edit", "in.pdf", "--ops", "x.json", "--out", "locked.pdf", "--encrypt-env", PasswordVariable, "--output-format", "json");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);

        CliResult converted = _workspace.RunWithEnv(
            variables, "pdf", "convert", "locked.pdf", "--password-env", PasswordVariable, "--to", "docx", "--out", "framed.docx", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string command = EditableConversion(converted);
        Assert.StartsWith("aspose-cli words convert ", command, StringComparison.Ordinal);
        Assert.Contains($" --password-env {PasswordVariable} ", command, StringComparison.Ordinal);
        Assert.Contains(" --overwrite", command, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, converted.StdOut, StringComparison.Ordinal);

        // The shown command opens the encrypted input as written and converts it over the file just written.
        AssertWordsReplaces(_workspace.RunCommandLine(command, variables), "locked.pdf", "framed.docx");
    }

    // The command the LOSSY_CONVERSION warning of a conversion shows for an editable document.
    private static string EditableConversion(CliResult converted)
    {
        JsonNode lossy = JsonNode.Parse(converted.StdOut)!["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!;
        string hint = lossy["hint"]!.GetValue<string>();
        const string Shown = "For an editable document, run '";
        int at = hint.IndexOf(Shown, StringComparison.Ordinal);
        Assert.True(at >= 0, hint);
        Assert.EndsWith("' instead.", hint, StringComparison.Ordinal);
        return hint[(at + Shown.Length)..].Split('\'', 2)[0];
    }

    // A Words conversion that succeeded and wrote the named docx from the named input.
    private void AssertWordsReplaces(CliResult rebuilt, string input, string output)
    {
        Assert.True(rebuilt.ExitCode == 0, rebuilt.StdErr);
        JsonNode result = JsonNode.Parse(rebuilt.StdOut)!;
        Assert.EndsWith("/v2/words/convert-result.schema.json", result["schema"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("docx", result["outputs"]![0]!["format"]!.GetValue<string>());
        Assert.Equal(
            Path.GetFullPath(_workspace.File(input)),
            Path.GetFullPath(result["input"]!["path"]!.GetValue<string>()),
            ignoreCase: true);
        Assert.Equal(
            Path.GetFullPath(_workspace.File(output)),
            Path.GetFullPath(result["outputs"]![0]!["path"]!.GetValue<string>()),
            ignoreCase: true);
    }

    private CliResult Run(params string[] arguments) => _workspace.Run([.. arguments, "--output-format", "json"]);
}
