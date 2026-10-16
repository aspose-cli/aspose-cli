using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// set_list sets the number style and the first number of a numbered list, clear_list takes
/// paragraphs out of their list, and a read reports what they set.
/// </summary>
public sealed class WordsListTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void SetList_NumberStyle_RenumbersANumberedListWithLetters()
    {
        JsonNode[] blocks = EditNumberedList("""{"op":"set_list","target":{"blocks":"1-3"},"kind":"number","numberStyle":"letters-lower"}""");

        Assert.Equal(["a.", "b.", "c."], blocks.Select(static block => block["listLabel"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal("letters-lower", block["list"]?["numberStyle"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal(1, block["list"]?["start"]?.GetValue<int>()));
    }

    [Fact]
    public void SetList_Start_NumbersTheListFromIt()
    {
        JsonNode[] blocks = EditNumberedList("""{"op":"set_list","target":{"blocks":"1-3"},"kind":"number","start":5}""");

        Assert.Equal(["5.", "6.", "7."], blocks.Select(static block => block["listLabel"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal("decimal", block["list"]?["numberStyle"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal(5, block["list"]?["start"]?.GetValue<int>()));
    }

    [Fact]
    public void ClearList_LeavesParagraphsInNoList()
    {
        JsonNode[] blocks = EditNumberedList("""{"op":"clear_list","target":{"blocks":"1-3"}}""");

        Assert.Equal(["One", "Two", "Three"], blocks.Select(static block => block["text"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Null(block["list"]));
        Assert.All(blocks, static block => Assert.Null(block["listLabel"]));
    }

    /// <summary>
    /// Applies one operation to a numbered list of three items, which changes all three without
    /// OP_NO_EFFECT and verifies, and returns the blocks a full read reports.
    /// </summary>
    private JsonNode[] EditNumberedList(string operation)
    {
        File.WriteAllText(_workspace.File("source.md"), "1. One\n2. Two\n3. Three\n");
        Assert.Equal(0, _workspace.Run("words", "create", "list.docx", "--from-markdown", "source.md").ExitCode);

        CliResult edited = _workspace.RunWithInput(
            $$"""{"ops":[{{operation}}]}""",
            "words", "edit", "list.docx", "--ops", "-", "--out", "edited.docx", "--verify", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal(3, result["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.DoesNotContain("OP_NO_EFFECT", result["warnings"]?.ToJsonString() ?? string.Empty, StringComparison.Ordinal);
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());

        CliResult read = _workspace.Run("words", "read", "edited.docx", "--content", "full", "--output-format", "json");
        Assert.True(read.ExitCode == 0, read.StdErr);
        return [.. JsonNode.Parse(read.StdOut)!["blocks"]!.AsArray().Select(static block => block!)];
    }
}
