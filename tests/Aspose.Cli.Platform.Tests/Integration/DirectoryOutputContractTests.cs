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
    /// <summary>
    /// A deck with two different pictures: two renders of a blank slide at different widths, each
    /// added as a picture frame.
    /// </summary>
    private static readonly string[][] SlidesWithTwoPictures =
    [
        ["slides", "create", "blank.pptx"],
        ["slides", "render", "blank.pptx", "--slides", "1", "--width", "64", "--out", "a.png"],
        ["slides", "render", "blank.pptx", "--slides", "1", "--width", "96", "--out", "b.png"],
        ["slides", "edit", "blank.pptx", "--out", "input.pptx", "--ops", """
            {"ops":[
              {"add":"pictureFrame","in":"slide/256","args":{"image":"a.png","shapeType":"Rectangle","x":10,"y":10,"width":40,"height":30}},
              {"add":"pictureFrame","in":"slide/256","args":{"image":"b.png","shapeType":"Rectangle","x":60,"y":10,"width":60,"height":45}}
            ]}
            """],
    ];

    /// <summary>
    /// A document with two different pictures: the two renders of a blank slide, each added at the
    /// end of the last paragraph of the engine's new document, which read gives.
    /// </summary>
    private static readonly string[][] WordsWithTwoPictures =
    [
        .. SlidesWithTwoPictures[..3],
        ["words", "create", "input.docx"],
        ["words", "edit", "input.docx", "--in-place", "--ops", """
            {"ops":[
              {"add":"image","in":"section/0/body/paragraph/0","args":{"image":"a.png"}},
              {"add":"image","in":"section/0/body/paragraph/0","args":{"image":"b.png"}}
            ]}
            """],
    ];

    private static readonly Dictionary<string, (string[][] Create, string[] Publish)> Cases = new(StringComparer.Ordinal)
    {
        ["pdf split"] = ([["pdf", "create", "input.pdf", "--from-text", "content.txt"]],
            ["pdf", "split", "input.pdf", "--by", "pages", "--out-dir", "parts"]),
        ["pdf extract"] = ([["pdf", "create", "input.pdf", "--from-text", "content.txt"]],
            ["pdf", "extract", "input.pdf", "--what", "text", "--out-dir", "parts"]),
        ["words extract"] = (WordsWithTwoPictures,
            ["words", "extract", "input.docx", "--what", "images", "--out-dir", "parts"]),
        ["slides extract"] = (SlidesWithTwoPictures,
            ["slides", "extract", "input.pptx", "--what", "media", "--out-dir", "parts"]),
    };

    private static void Create(TempWorkspace workspace, string[][] steps)
    {
        foreach (string[] step in steps)
        {
            CliResult created = workspace.Run([.. step, "--output-format", "json"]);
            Assert.True(created.ExitCode == 0, created.StdErr);
        }
    }

    public static TheoryData<string> Commands => [.. Cases.Keys];

    [Category(TestCategory.Slow)]
    [Theory]
    [MemberData(nameof(Commands))]
    public void AnExistingFileIsReplacedOnlyWithOverwrite(string command)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Directory output fixture");
        File.WriteAllText(workspace.File("content.md"), "# Directory output fixture\n\n- One point\n");
        (string[][] create, string[] publish) = Cases[command];
        Create(workspace, create);
        CliResult first = workspace.Run([.. publish, "--output-format", "json"]);
        Assert.True(first.ExitCode == 0, first.StdErr);
        string[] published = Directory.GetFiles(workspace.File("parts"));
        Assert.NotEmpty(published);
        foreach (string file in published)
        {
            File.WriteAllText(file, "kept");
        }

        CliResult refused = workspace.Run([.. publish, "--output-format", "json"]);
        Assert.True(refused.ExitCode != 0, refused.StdOut);
        Assert.Equal("OUTPUT_EXISTS", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(published.Order(), Directory.GetFiles(workspace.File("parts")).Order());
        Assert.All(published, static file => Assert.Equal("kept", File.ReadAllText(file)));

        CliResult replaced = workspace.Run([.. publish, "--overwrite", "--output-format", "json"]);
        Assert.True(replaced.ExitCode == 0, replaced.StdErr);
        Assert.Equal(published.Order(), Directory.GetFiles(workspace.File("parts")).Order());
        Assert.All(published, static file => Assert.NotEqual("kept", File.ReadAllText(file)));
    }

    /// <summary>
    /// A directory output named like a file of a format some command writes, such as
    /// <c>parts.pdf</c> or <c>images.png</c>, is refused before anything is created, with the
    /// name without that extension as the fix; a directory name with a dot in it that names no
    /// format still works.
    /// </summary>
    [Theory]
    [InlineData("pdf split input.pdf --by pages --out-dir parts.pdf", "--out-dir", "parts.pdf")]
    [InlineData("pdf extract input.pdf --what images --out-dir images.png", "--out-dir", "images.png")]
    public void ADirectoryNamedLikeAFile_IsRefused(string commandLine, string option, string directory)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Directory output fixture");
        CliResult created = workspace.Run("pdf", "create", "input.pdf", "--from-text", "content.txt", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult result = workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
        Assert.False(Directory.Exists(workspace.File(directory)));
        Assert.False(File.Exists(workspace.File(directory)));
    }

    [Theory]
    [InlineData("pdf split input.pdf --by pages --out-dir parts.v1 --dry-run")]
    public void ADirectoryNameWithADotNamingNoFormat_IsAccepted(string commandLine)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Directory output fixture");
        CliResult created = workspace.Run("pdf", "create", "input.pdf", "--from-text", "content.txt", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult result = workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.True(result.ExitCode == 0, result.StdErr);
    }

    private static readonly Dictionary<string, (string[][] Create, string[] Publish)> MultiFileCases = new(StringComparer.Ordinal)
    {
        ["words extract"] = (WordsWithTwoPictures,
            ["words", "extract", "input.docx", "--what", "images", "--out-dir", "parts"]),
        ["slides extract"] = (SlidesWithTwoPictures,
            ["slides", "extract", "input.pptx", "--what", "media", "--out-dir", "parts"]),
    };

    public static TheoryData<string> MultiFileCommands => [.. MultiFileCases.Keys];

    [Theory]
    [MemberData(nameof(MultiFileCommands))]
    public void AnExistingLastFileRollsBackTheNewFilesBeforeIt(string command)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.md"), "# First\n\nOne\n\n# Second\n\nTwo\n");
        (string[][] create, string[] publish) = MultiFileCases[command];
        Create(workspace, create);
        CliResult first = workspace.Run([.. publish, "--output-format", "json"]);
        Assert.True(first.ExitCode == 0, first.StdErr);
        string[] published = [.. Directory.GetFiles(workspace.File("parts")).Order(StringComparer.Ordinal)];
        Assert.True(published.Length > 1, first.StdOut);
        string kept = published[^1];
        foreach (string file in published[..^1])
        {
            File.Delete(file);
        }

        File.WriteAllText(kept, "kept");
        CliResult refused = workspace.Run([.. publish, "--output-format", "json"]);
        Assert.True(refused.ExitCode != 0, refused.StdOut);
        Assert.Equal("OUTPUT_EXISTS", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal([kept], Directory.GetFileSystemEntries(workspace.File("parts")));
        Assert.Equal("kept", File.ReadAllText(kept));
    }
}
