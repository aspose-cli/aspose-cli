using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What a verified edit says about the operations it did not read back. The commands run with the
/// test license when the run has one, and in evaluation mode otherwise.
/// </summary>
public sealed class WordsVerifyCoverageTests : IDisposable
{
    private readonly WordsFixture _fixture = new();
    private readonly TempWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
        _fixture.Dispose();
    }

    /// <summary>
    /// Words read back only the field count, the revision count and the protection, so
    /// <c>set_properties</c> is named with that reason. An edit it reads back whole has no such
    /// field.
    /// </summary>
    [Fact]
    public void Verify_NamesAnOperationItDoesNotReadBackWithTheReason_AndOmitsTheFieldWhenItReadsEveryOne()
    {
        string input = _fixture.CreateReport();

        JsonNode partial = Verification(Edit(input, """{"ops":[{"id":"props","op":"set_properties","title":"T"},{"id":"lock","op":"protect_document","protection":"read-only"}]}"""));
        JsonNode whole = Verification(Edit(input, """{"ops":[{"id":"lock","op":"protect_document","protection":"read-only"}]}"""));

        Assert.True(partial["ok"]!.GetValue<bool>());
        Assert.Equal(["lock"], partial["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode item = Assert.Single(partial["uncheckedOps"]!.AsArray())!;
        Assert.Equal("props", item["id"]!.GetValue<string>());
        Assert.Contains("field count", item["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Null(whole["uncheckedOps"]);
    }

    [Fact]
    public void Verify_InTheTable_ListsAnOperationItDoesNotReadBack()
    {
        string input = _fixture.CreateReport();

        CliResult table = Cli("words", "edit", input, "--ops", """{"ops":[{"id":"props","op":"set_properties","title":"T"}]}""",
            "--out", "table.docx", "--verify", "--output-format", "table");

        Assert.True(table.ExitCode == 0, table.StdErr + table.StdOut);
        Assert.Contains("not checked props: ", table.StdOut, StringComparison.Ordinal);
    }

    private CliResult Edit(string input, string ops)
    {
        CliResult edited = Cli("words", "edit", input, "--ops", ops, "--out", "out.docx", "--overwrite", "--verify", "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        return edited;
    }

    private CliResult Cli(params string[] args) =>
        _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);

    private static JsonNode Verification(CliResult result) => JsonNode.Parse(result.StdOut)!["verification"]!;
}
