using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What a verified edit says about the operations it did not read back. The commands run with the
/// test license when the run has one, and in evaluation mode otherwise.
/// </summary>
public sealed class PdfVerifyCoverageTests : IDisposable
{
    private readonly PdfEngineFixture _fixture = new();
    private readonly TempWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
        _fixture.Dispose();
    }

    /// <summary>
    /// A rotation has no reliable read-back, so it is named with a reason. An edit it reads back
    /// whole has no such field.
    /// </summary>
    [Fact]
    public void Verify_NamesAnOperationItDoesNotReadBackWithAReason_AndOmitsTheFieldWhenItReadsEveryOne()
    {
        string input = Document();

        JsonNode partial = Verification(Edit(input, """{"ops":[{"id":"turn","op":"rotate_pages","pages":"1","rotation":90},{"id":"name","op":"set_properties","title":"T"}]}"""));
        JsonNode whole = Verification(Edit(input, """{"ops":[{"id":"name","op":"set_properties","title":"T"}]}"""));

        Assert.True(partial["ok"]!.GetValue<bool>());
        Assert.Equal(["name"], partial["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode item = Assert.Single(partial["uncheckedOps"]!.AsArray())!;
        Assert.Equal("turn", item["id"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(item["reason"]!.GetValue<string>()));
        Assert.Null(whole["uncheckedOps"]);
    }

    /// <summary>An operation a later one of the batch set again is named for that reason.</summary>
    [Fact]
    public void Verify_NamesAnOperationALaterOneChangedAgain()
    {
        string input = Document();

        JsonNode verification = Verification(Edit(input, """{"ops":[{"id":"first","op":"set_properties","title":"A"},{"id":"second","op":"set_properties","title":"B"}]}"""));

        Assert.Equal(["second"], verification["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode item = Assert.Single(verification["uncheckedOps"]!.AsArray())!;
        Assert.Equal("first", item["id"]!.GetValue<string>());
        Assert.Contains("later operation", item["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Redacted text left in the output stops the edit before anything is published, so another
    /// operation is named for that and not sent to read an output that does not exist.
    /// </summary>
    [Fact]
    public void Verify_NamesAnOperationARedactionStoppedBeforeItsReadBack_WithoutPointingAtAnOutput()
    {
        string input = _fixture.File("secret.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Customer Secret 42"));
            document.Save(input);
        }

        CliResult edited = Run(input, """{"ops":[{"id":"black","op":"redact_text","find":"Secret"},{"id":"stamp","op":"add_header_text","text":"Secret draft","pages":"1"}]}""");

        Assert.True(edited.ExitCode == 8, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.False(File.Exists(_workspace.File("out.pdf")));
        JsonNode item = Assert.Single(Verification(edited)["uncheckedOps"]!.AsArray(), static item => item!["id"]!.GetValue<string>() == "stamp")!;
        string reason = item["reason"]!.GetValue<string>();
        Assert.Contains("not published", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("read the output", reason, StringComparison.Ordinal);
    }

    private string Document()
    {
        string path = _fixture.File("input.pdf");
        using var document = new Document();
        document.Pages.Add();
        document.Save(path);
        return path;
    }

    private CliResult Edit(string input, string ops)
    {
        CliResult edited = Run(input, ops);
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        return edited;
    }

    private CliResult Run(string input, string ops)
    {
        string[] args = ["pdf", "edit", input, "--ops", ops, "--out", "out.pdf", "--overwrite", "--verify", "--output-format", "json"];
        return _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);
    }

    private static JsonNode Verification(CliResult result) => JsonNode.Parse(result.StdOut)!["verification"]!;
}
