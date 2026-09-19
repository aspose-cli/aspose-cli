using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class ProductPublicationTests
{
    public static TheoryData<string, string, string[], string> EditCases => new()
    {
        { "words", "docx", ["--text", "source.txt"], """{"ops":[{"op":"set_text","at":{"find":"Anchor"},"text":"Edited"}]}""" },
        { "slides", "pptx", ["--from-markdown", "source.md"], """{"ops":[{"op":"set_title","slide":1,"text":"Edited"}]}""" },
        { "pdf", "pdf", ["--from-text", "source.txt"], """{"ops":[{"op":"set_metadata","title":"Edited"}]}""" },
    };

    [Theory]
    [MemberData(nameof(EditCases))]
    public void EditPublishesTheCandidateInBothExecutionModes(string product, string extension,
        string[] sourceOptions, string ops)
    {
        foreach (bool supervised in new[] { false, true })
        {
            using var workspace = new TempWorkspace();
            string input = CreateInput(workspace, product, extension, sourceOptions);
            string output = workspace.File("edited." + extension);
            CliResult edited = workspace.Run([product, "edit", input, "--ops", ops, "--out", output,
                "--output", "json", "--max-input-bytes", new FileInfo(input).Length.ToString(),
                .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
            AssertSuccess(edited);
            Assert.True(File.Exists(output));
            Assert.Equal([output], Directory.GetFiles(workspace.File("."), "edited.*").Select(Path.GetFullPath));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WordsVerificationReadsTheCandidateInBothExecutionModes(bool supervised)
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, "words", "docx", ["--text", "source.txt"]);
        CliResult edited = workspace.Run(["words", "edit", input,
            "--ops", """{"ops":[{"op":"set_text","at":{"find":"Anchor"},"text":"Edited"}]}""",
            "--out", workspace.File("edited.docx"), "--verify", "--output", "json",
            .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
        AssertSuccess(edited);
        JsonNode verification = JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.True(verification["semanticChangesDetected"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllSheetsFailureDoesNotLeaveEarlierOutputFiles(bool supervised)
    {
        using var workspace = new TempWorkspace();
        AssertSuccess(workspace.Run("cells", "create", "input.xlsx", "--sheets", "First,Second"));
        AssertSuccess(workspace.Run("cells", "edit", "input.xlsx", "--set", "First!A1=1", "--set", "Second!A1=2", "--in-place"));
        File.WriteAllText(workspace.File("render.Second.png"), "existing second sheet");
        CliResult result = workspace.Run(["cells", "render", "input.xlsx", "--all-sheets", "--out", "render.png", "--output", "json",
            .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
        Assert.Equal(5, result.ExitCode);
        Assert.False(File.Exists(workspace.File("render.First.png")));
        Assert.Equal("existing second sheet", File.ReadAllText(workspace.File("render.Second.png")));
    }

    private static string CreateInput(TempWorkspace workspace, string product, string extension, string[] sourceOptions)
    {
        File.WriteAllText(workspace.File("source.txt"), "Anchor");
        File.WriteAllText(workspace.File("source.md"), "# Anchor\n\nBody");
        string input = workspace.File("input." + extension);
        AssertSuccess(workspace.Run([product, "create", input, .. sourceOptions]));
        return input;
    }

    private static void AssertSuccess(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
}
