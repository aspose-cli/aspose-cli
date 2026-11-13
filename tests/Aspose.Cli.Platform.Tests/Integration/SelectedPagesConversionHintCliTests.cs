using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The hints of a PDF page selection converted to docx, for an editable file and for an encrypted
/// one, each split the pages into a folder of their own that names nothing when the hint is made,
/// convert the part, and say to delete that folder. Done as they say, one after the other without
/// deleting anything between them, and done as the hint of any error on the way says, they end with
/// the docx of those pages, leave every file and folder the caller had as it was, a file named as the
/// part beside the output and a folder named as the hint's first choice included, and leave nothing
/// else behind. The folder to delete is named by its full path in quotes.
/// </summary>
public sealed partial class SelectedPagesConversionHintCliTests
{
    private const string Variable = "DOC_PASSWORD";
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal) { [Variable] = "hint-secret" };

    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("", "locked", "page", true)]
    [InlineData("my docs/", "plain doc", "out put", false)]
    public void TheHintsOfAPageSelection_LeaveTheCallersFilesAsTheyWere(string folder, string input, string output, bool encrypted)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        Directory.CreateDirectory(workspace.File(folder + "."));
        File.WriteAllText(workspace.File("source.md"), "# One\n\nFirst part.\n");
        Succeeded(workspace.Run("pdf", "create", "page.pdf", "--from-markdown", "source.md", "--output-format", "json"));
        Succeeded(workspace.RunWithEnv(Variables, ["pdf", "merge", "page.pdf", "page.pdf", "page.pdf", "--out", $"{folder}{input}.pdf",
            .. encrypted ? new[] { "--encrypt-env", Variable } : [], "--output-format", "json"]));
        // The caller's own files where a part could go: beside the output, and in a folder named as the hint names its first.
        Dictionary<string, string> callers = new(StringComparer.Ordinal)
        {
            [$"{folder}{input}.1.pdf"] = "the caller's file beside the output",
            [$"{folder}{output}-editable-part/{input}.1.pdf"] = "the caller's file in a folder of theirs",
        };
        foreach ((string path, string text) in callers)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(workspace.File(path))!);
            File.WriteAllText(workspace.File(path), text);
        }

        string[] before = Entries(workspace);
        JsonNode converted = Succeeded(workspace.RunWithEnv(Variables, ["pdf", "convert", $"{folder}{input}.pdf",
            .. encrypted ? new[] { "--password-env", Variable } : [], "--pages", "1-2", "--to", "docx", "--out", $"{folder}{output}.docx", "--output-format", "json"]));

        string[] hints = [.. converted["warnings"]!.AsArray()
            .Where(static warning => warning!["code"]!.GetValue<string>() is "LOSSY_CONVERSION" or "ENCRYPTION_REMOVED")
            .Select(static warning => warning!["hint"]!.GetValue<string>())];
        Assert.Equal(encrypted ? 2 : 1, hints.Length);
        foreach (string hint in hints)
        {
            foreach (Match command in QuotedCommand().Matches(hint))
            {
                Follow(workspace, command.Groups[1].Value);
            }
        }

        foreach (string hint in hints)
        {
            // As an agent deletes what a hint names: a folder with all it holds, or a file.
            string named = Path.Combine(Path.GetDirectoryName(workspace.File($"{folder}{output}.docx"))!, Deleted().Match(hint).Groups[1].Value.Trim('"'));
            if (Directory.Exists(named))
            {
                Directory.Delete(named, recursive: true);
            }
            else
            {
                File.Delete(named);
            }
        }

        Assert.All(callers, caller => Assert.Equal(caller.Value, File.ReadAllText(workspace.File(caller.Key))));
        Assert.Equal([.. before.Append($"{folder}{output}.docx").Order(StringComparer.Ordinal)], Entries(workspace));
        string[] password = encrypted ? ["--password-env", Variable] : [];
        JsonNode inspected = Succeeded(workspace.RunWithEnv(Variables, ["words", "inspect", $"{folder}{output}.docx", .. password, "--output-format", "json"]));
        Assert.Equal(2, inspected["document"]!["pageCount"]!.GetValue<int>());
        if (encrypted)
        {
            Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(workspace.Run("words", "inspect", $"{folder}{output}.docx", "--output-format", "json").StdErr)!["error"]!["code"]!.GetValue<string>());
        }

        // The folder to delete is named by its full path in quotes, so it is the same wherever the agent runs.
        Assert.All(hints, hint => Assert.True(
            Deleted().Match(hint).Groups[1].Value is ['"', .. var path, '"'] && Path.IsPathFullyQualified(path), hint));
    }

    // Runs a command a hint gives as written, and again as its error's hint says when that hint says to pass --overwrite.
    private static void Follow(TempWorkspace workspace, string command)
    {
        CliResult ran = workspace.RunCommandLine(command, Variables);
        if (ran.ExitCode != 0 && JsonNode.Parse(ran.StdErr)?["error"]?["hint"]?.GetValue<string>() is { } hint
            && hint.Contains("--overwrite", StringComparison.Ordinal))
        {
            ran = workspace.RunCommandLine(command + " --overwrite", Variables);
        }

        Assert.True(ran.ExitCode == 0, $"{command}\n{ran.StdErr}");
    }

    private static JsonNode Succeeded(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        return JsonNode.Parse(result.StdOut)!;
    }

    // Every file and folder in the workspace, by its path relative to it with '/', in order.
    private static string[] Entries(TempWorkspace workspace) =>
        [.. Directory.EnumerateFileSystemEntries(workspace.Path, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(workspace.Path, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    [GeneratedRegex("'(aspose-cli [^']*)'")]
    private static partial Regex QuotedCommand();

    [GeneratedRegex("once the conversion has run, delete (\"[^\"]+\"|[^,]+),")]
    private static partial Regex Deleted();
}
