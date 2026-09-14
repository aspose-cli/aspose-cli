using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class ProductPublicationTests
{
    public static TheoryData<string, string, string[], string, string> VerifyCases => new()
    {
        { "words", "docx", ["--text", "source.txt"], """{"ops":[{"op":"set_text","at":{"find":"Anchor"},"text":"Edited"}]}""", ".verify.p1.png" },
        { "slides", "pptx", ["--from-markdown", "source.md"], """{"ops":[{"op":"set_title","slide":1,"text":"Edited"}]}""", ".verify.s1.png" },
        { "pdf", "pdf", ["--from-text", "source.txt"], """{"ops":[{"op":"set_metadata","title":"Edited"}]}""", ".verify.p1.png" },
    };

    [Theory]
    [MemberData(nameof(VerifyCases))]
    public void VerificationReadsTheCandidateInBothExecutionModes(string product, string extension,
        string[] sourceOptions, string ops, string evidenceSuffix)
    {
        foreach (bool supervised in new[] { false, true })
        {
            using var workspace = new TempWorkspace();
            string input = CreateInput(workspace, product, extension, sourceOptions);
            string output = workspace.File("edited." + extension);
            CliResult edited = workspace.Run([product, "edit", input, "--ops", ops, "--out", output,
                "--verify", "--output", "json", "--max-input-bytes", new FileInfo(input).Length.ToString(),
                .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
            Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
            Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
            Assert.True(File.Exists(output));
            Assert.True(File.Exists(output + evidenceSuffix));
        }
    }

    [Theory]
    [MemberData(nameof(VerifyCases))]
    public void InPlaceDoesNotAuthorizeEvidenceOverwrite(string product, string extension,
        string[] sourceOptions, string ops, string evidenceSuffix)
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, product, extension, sourceOptions);
        byte[] original = File.ReadAllBytes(input);
        string evidence = input + evidenceSuffix;
        File.WriteAllText(evidence, "existing user evidence");
        CliResult refused = workspace.Run(product, "edit", input, "--ops", ops,
            "--in-place", "--verify", "--timeout", "30", "--output", "json");
        Assert.Equal(5, refused.ExitCode);
        Assert.Equal(string.Empty, refused.StdOut);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal("existing user evidence", File.ReadAllText(evidence));
        Assert.False(File.Exists(workspace.File("input.backup." + extension)));

        CliResult permitted = workspace.Run(product, "edit", input, "--ops", ops,
            "--in-place", "--verify", "--overwrite", "--timeout", "30", "--output", "json");
        Assert.True(permitted.ExitCode == 0, permitted.StdErr + permitted.StdOut);
        Assert.Equal("89504E470D0A1A0A", Convert.ToHexString(File.ReadAllBytes(evidence).AsSpan(0, 8)));
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
