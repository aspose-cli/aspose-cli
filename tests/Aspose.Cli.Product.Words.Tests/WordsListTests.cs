using System.Text.Json.Nodes;
using Aspose.Words;
using Aspose.Words.Lists;
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
    public void SetList_NumberFormat_WritesParenthesizedLetters()
    {
        JsonNode[] blocks = EditNumberedList("""{"op":"set_list","target":{"blocks":"1-3"},"kind":"number","numberStyle":"letters-lower","numberFormat":"(%1)"}""");

        Assert.Equal(["(a)", "(b)", "(c)"], blocks.Select(static block => block["listLabel"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal("(%1)", block["list"]?["numberFormat"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal("letters-lower", block["list"]?["numberStyle"]?.GetValue<string>()));
    }

    [Fact]
    public void SetList_NumberFormat_ShowsTheNumbersOfTheLevelsAbove()
    {
        JsonNode[] blocks = EditNumberedList("""{"op":"set_list","target":{"blocks":"1-3"},"kind":"number","level":2,"numberStyle":"decimal","numberFormat":"%1.%2"}""");

        Assert.Equal(["1.1", "1.2", "1.3"], blocks.Select(static block => block["listLabel"]?.GetValue<string>()));
        Assert.All(blocks, static block => Assert.Equal("%1.%2", block["list"]?["numberFormat"]?.GetValue<string>()));
    }

    /// <summary>Without numberStyle, a level takes its style in a new numbered list, as the schema describes.</summary>
    [Theory]
    [InlineData(2, "letters-lower", "a.")]
    [InlineData(3, "roman-lower", "i.")]
    public void SetList_WithoutNumberStyle_UsesTheLevelsStyleInANewList(int level, string style, string first)
    {
        JsonNode[] blocks = EditNumberedList($$"""{"op":"set_list","target":{"blocks":"1-3"},"kind":"number","level":{{level}}}""");

        Assert.Equal(first, blocks[0]["listLabel"]?.GetValue<string>());
        Assert.All(blocks, block => Assert.Equal(style, block["list"]?["numberStyle"]?.GetValue<string>()));
    }

    /// <summary>
    /// The contract clause recipe in document-standards.md, run as it stands on the Markdown it
    /// shows, numbers the sub-clauses 1.1, 1.2; one op per sub-clause numbers each 1.1, and a
    /// later article's number written as text gives 2.1, 2.2.
    /// </summary>
    [Fact]
    public void SetList_ContractClauseRecipeOfTheSkill_NumbersSubClausesUnderTheirArticle()
    {
        string standards = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Product.Words", "Skills", "references", "document-standards.md");
        string recipe = SkillOperationDocuments.Collect(Path.GetDirectoryName(standards)!).Documents
            .Single(document => document.Source == standards && document.Json.Contains("\"set_list\"", StringComparison.Ordinal)).Json;
        const string Markdown = "Agreement\n\n# General\n\n1. Article one\n   1. Clause one\n   2. Clause two\n2. Article two\n";

        Assert.Equal(["1.", "1.1", "1.2", "2."], Labels(Markdown, recipe));
        Assert.Equal(["1.", "1.1", "1.1", "2."], Labels(Markdown, """
            {"ops":[
              {"op":"set_list","target":{"block":4},"kind":"number","level":2,"numberStyle":"decimal","numberFormat":"%1.%2"},
              {"op":"set_list","target":{"block":5},"kind":"number","level":2,"numberStyle":"decimal","numberFormat":"%1.%2"}
            ]}
            """));
        Assert.Equal(["1.", "1.1", "1.2", "2.", "2.1", "2.2"], Labels(Markdown + "   1. Clause one\n   2. Clause two\n", """
            {"ops":[
              {"op":"set_list","target":{"blocks":"4-5"},"kind":"number","level":2,"numberStyle":"decimal","numberFormat":"%1.%2"},
              {"op":"set_list","target":{"blocks":"7-8"},"kind":"number","level":2,"numberStyle":"decimal","numberFormat":"2.%2"}
            ]}
            """));
    }

    /// <summary>
    /// A number format another program wrote, with fixed text and a literal percent sign, reads
    /// as a numberFormat that set_list writes back as the same list.
    /// </summary>
    [Theory]
    [InlineData("Done 100%", "Done 100%%")]
    [InlineData("\u0000 of 100%", "%1 of 100%%")]
    public void SetList_NumberFormatItReads_WritesBackTheSameList(string engineFormat, string notation)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        Aspose.Words.Lists.List list = document.Lists.Add(ListTemplate.NumberDefault);
        list.ListLevels[0].NumberFormat = engineFormat;
        builder.ListFormat.List = list;
        foreach (string text in new[] { "One", "Two", "Three" })
        {
            builder.Writeln(text);
        }

        builder.ListFormat.RemoveNumbers();
        document.Save(_workspace.File("list.docx"), SaveFormat.Docx);

        JsonNode[] items = ListItems("list.docx");
        Assert.Equal(3, items.Length);
        JsonNode read = items[0]["list"]!;
        Assert.Equal(notation, read["numberFormat"]!.GetValue<string>());

        string target = $"{items[0]["block"]}-{items[^1]["block"]}";
        CliResult edited = _workspace.RunWithInput(
            $$"""{"ops":[{"op":"set_list","target":{"blocks":"{{target}}"},"kind":{{read["kind"]!.ToJsonString()}},"level":{{read["level"]}},"numberStyle":{{read["numberStyle"]!.ToJsonString()}},"numberFormat":{{read["numberFormat"]!.ToJsonString()}},"start":{{read["start"]}}}]}""",
            "words", "edit", "list.docx", "--ops", "-", "--out", "edited.docx", "--verify", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal(0, JsonNode.Parse(edited.StdOut)!["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.All(ListItems("edited.docx"), item => Assert.Equal(notation, item["list"]!["numberFormat"]!.GetValue<string>()));
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

    /// <summary>The list labels a full read reports after <paramref name="ops"/> edit a document made from <paramref name="markdown"/>.</summary>
    private IEnumerable<string?> Labels(string markdown, string ops)
    {
        File.WriteAllText(_workspace.File("clauses.md"), markdown);
        Assert.Equal(0, _workspace.Run("words", "create", "clauses.docx", "--from-markdown", "clauses.md", "--overwrite").ExitCode);
        CliResult edited = _workspace.RunWithInput(ops, "words", "edit", "clauses.docx", "--ops", "-", "--out", "clauses.edited.docx", "--overwrite", "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        return [.. ListItems("clauses.edited.docx").Select(static item => item["listLabel"]?.GetValue<string>())];
    }

    /// <summary>The blocks of a document that a full read reports in a list.</summary>
    private JsonNode[] ListItems(string file)
    {
        CliResult read = _workspace.Run("words", "read", file, "--content", "full", "--output-format", "json");
        Assert.True(read.ExitCode == 0, read.StdErr);
        return [.. JsonNode.Parse(read.StdOut)!["blocks"]!.AsArray().Select(static block => block!).Where(static block => block["list"] is not null)];
    }
}
