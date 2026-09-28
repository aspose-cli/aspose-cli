using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every command that publishes a set of files into <c>--out-dir</c> treats a file already
/// there as file outputs do: without <c>--overwrite</c> it is refused with
/// <c>OUTPUT_EXISTS</c> and nothing is published, not even the files that did not exist;
/// with it, the file is replaced.
/// </summary>
public sealed class DirectoryOutputContractTests
{
    private static readonly Dictionary<string, (string[] Create, string[] Publish)> Cases = new(StringComparer.Ordinal)
    {
        ["pdf split"] = (["pdf", "create", "input.pdf", "--from-text", "content.txt"],
            ["pdf", "split", "input.pdf", "--every", "1", "--out-dir", "parts"]),
        ["pdf extract"] = (["pdf", "create", "input.pdf", "--from-text", "content.txt"],
            ["pdf", "extract", "input.pdf", "--what", "text", "--out-dir", "parts"]),
        ["words split"] = (["words", "create", "input.docx", "--text", "content.txt"],
            ["words", "split", "input.docx", "--by", "section", "--out-dir", "parts"]),
        ["words extract"] = (["words", "create", "input.docx", "--text", "content.txt"],
            ["words", "extract", "input.docx", "--what", "text", "--out-dir", "parts"]),
        ["slides extract"] = (["slides", "create", "input.pptx", "--from-markdown", "content.md"],
            ["slides", "extract", "input.pptx", "--what", "text", "--out-dir", "parts"]),
    };

    public static TheoryData<string> Commands => [.. Cases.Keys];

    [Category(TestCategory.Slow)]
    [Theory]
    [MemberData(nameof(Commands))]
    public void AnExistingFileIsReplacedOnlyWithOverwrite(string command)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Directory output fixture");
        File.WriteAllText(workspace.File("content.md"), "# Directory output fixture\n\n- One point\n");
        (string[] create, string[] publish) = Cases[command];
        CliResult created = workspace.Run([.. create, "--output", "json"]);
        Assert.True(created.ExitCode == 0, created.StdErr);
        CliResult first = workspace.Run([.. publish, "--output", "json"]);
        Assert.True(first.ExitCode == 0, first.StdErr);
        string[] published = Directory.GetFiles(workspace.File("parts"));
        Assert.NotEmpty(published);
        foreach (string file in published)
        {
            File.WriteAllText(file, "kept");
        }

        CliResult refused = workspace.Run([.. publish, "--output", "json"]);
        Assert.True(refused.ExitCode != 0, refused.StdOut);
        Assert.Equal("OUTPUT_EXISTS", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(published.Order(), Directory.GetFiles(workspace.File("parts")).Order());
        Assert.All(published, static file => Assert.Equal("kept", File.ReadAllText(file)));

        CliResult replaced = workspace.Run([.. publish, "--overwrite", "--output", "json"]);
        Assert.True(replaced.ExitCode == 0, replaced.StdErr);
        Assert.Equal(published.Order(), Directory.GetFiles(workspace.File("parts")).Order());
        Assert.All(published, static file => Assert.NotEqual("kept", File.ReadAllText(file)));
    }

    private static readonly Dictionary<string, (string[] Create, string[] Publish)> MultiFileCases = new(StringComparer.Ordinal)
    {
        ["words split"] = (["words", "create", "input.docx", "--markdown", "content.md"],
            ["words", "split", "input.docx", "--by", "heading1", "--out-dir", "parts"]),
        ["slides extract"] = (["slides", "create", "input.pptx", "--from-markdown", "content.md"],
            ["slides", "extract", "input.pptx", "--what", "text", "--out-dir", "parts"]),
    };

    public static TheoryData<string> MultiFileCommands => [.. MultiFileCases.Keys];

    [Theory]
    [MemberData(nameof(MultiFileCommands))]
    public void AnExistingLastFileRollsBackTheNewFilesBeforeIt(string command)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.md"), "# First\n\nOne\n\n# Second\n\nTwo\n");
        (string[] create, string[] publish) = MultiFileCases[command];
        CliResult created = workspace.Run([.. create, "--output", "json"]);
        Assert.True(created.ExitCode == 0, created.StdErr);
        CliResult first = workspace.Run([.. publish, "--output", "json"]);
        Assert.True(first.ExitCode == 0, first.StdErr);
        string[] published = [.. Directory.GetFiles(workspace.File("parts")).Order(StringComparer.Ordinal)];
        Assert.True(published.Length > 1, first.StdOut);
        string kept = published[^1];
        foreach (string file in published[..^1])
        {
            File.Delete(file);
        }

        File.WriteAllText(kept, "kept");
        CliResult refused = workspace.Run([.. publish, "--output", "json"]);
        Assert.True(refused.ExitCode != 0, refused.StdOut);
        Assert.Equal("OUTPUT_EXISTS", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal([kept], Directory.GetFileSystemEntries(workspace.File("parts")));
        Assert.Equal("kept", File.ReadAllText(kept));
    }
}
