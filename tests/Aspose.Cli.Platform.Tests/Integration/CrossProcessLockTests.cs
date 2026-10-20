using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Agents run several CLI processes at once. Publication and Skill installation hold
/// resource-based interprocess locks, so a second process waits or refuses and never
/// leaves a target half written.
/// </summary>
[Category(TestCategory.Slow)]
public sealed class CrossProcessLockTests
{
    private const string Skill = "aspose-cli-cells";

    [Fact]
    public void Edit_WhileAnotherProcessHoldsThePublicationLock_PublishesNothingUntilItIsReleased()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "Data").ExitCode);
        byte[] original = File.ReadAllBytes(workspace.File("book.xlsx"));
        string[] edit = ["cells", "edit", "book.xlsx", "--in-place", "--set", "Data!A1=changed", "--output-format", "json"];

        CliResult blocked;
        using (PublicationDirectoryLease.Acquire(workspace.Path))
        {
            blocked = workspace.Run([.. edit, "--timeout", "3"]);
        }

        Assert.Equal(9, blocked.ExitCode);
        Assert.Equal("OPERATION_TIMEOUT", ErrorCode(blocked));
        Assert.Equal(original, File.ReadAllBytes(workspace.File("book.xlsx")));
        Assert.Equal(["book.xlsx"], Entries(workspace.Path));

        CliResult published = workspace.Run(edit);
        Assert.True(published.ExitCode == 0, published.StdErr);
        Assert.NotEqual(original, File.ReadAllBytes(workspace.File("book.xlsx")));
        Assert.Equal(["book.xlsx"], Entries(workspace.Path));
    }

    [Fact]
    public async Task ConcurrentConditionalEdits_FromTwoProcesses_PublishExactlyOne()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "Data").ExitCode);
        string fingerprint = JsonNode.Parse(workspace.Run("cells", "inspect", "book.xlsx", "--output-format", "json").StdOut)!
            ["input"]!["fingerprint"]!["sha256"]!.GetValue<string>();

        CliResult[] results = await Task.WhenAll(new[] { "first", "second" }.Select(value => Task.Run(() =>
            workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--if-match", fingerprint,
                "--set", $"Data!A1={value}", "--output-format", "json"))));

        CliResult winner = Assert.Single(results, static result => result.ExitCode == 0);
        CliResult loser = Assert.Single(results, static result => result.ExitCode != 0);
        Assert.Empty(loser.StdOut);
        Assert.Equal("INPUT_CHANGED", ErrorCode(loser));
        string written = JsonNode.Parse(winner.StdOut)!["output"]!["fingerprint"]!["sha256"]!.GetValue<string>();
        Assert.Equal(written, FileHashes.Sha256(workspace.File("book.xlsx")));
        // Under load the system can refuse to delete a finished transaction's directory at once;
        // the next publication into the directory recovers it, so only the documents count here.
        Assert.Equal(["book.xlsx"], Entries(workspace.Path)
            .Where(static entry => !entry.StartsWith(".aspose-publication-", StringComparison.Ordinal)));
    }

    // Windows file names ignore case: a lock held under one spelling of the target holds every spelling.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SkillInstall_WhileAnotherProcessHoldsTheTarget_IsRefusedWithoutTouchingIt(bool heldInUpperCase, bool installedInUpperCase)
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");
        string reference = workspace.File("reference");
        Assert.Equal(0, workspace.Run("skill", "install", Skill, "--target", reference, "--output-format", "json").ExitCode);
        string installed = Path.GetFullPath(Path.Combine(target, Skill));

        Directory.CreateDirectory(target);

        CliResult refused;
        using (PublicationDirectoryLease.Acquire(heldInUpperCase ? target.ToUpperInvariant() : target))
        {
            refused = workspace.Run("skill", "install", Skill, "--target", installedInUpperCase ? target.ToUpperInvariant() : target,
                "--output-format", "json", "--timeout", "3");
        }

        Assert.Equal("OPERATION_TIMEOUT", ErrorCode(refused));
        Assert.False(Directory.Exists(installed));
        CliResult accepted = workspace.Run("skill", "install", Skill, "--target", target, "--output-format", "json");
        Assert.True(accepted.ExitCode == 0, accepted.StdErr);
        Assert.Equal(Tree(Path.Combine(reference, Skill)), Tree(installed));
        // The released lock leaves no lock file beside the installed Skill.
        Assert.Equal([Skill], Entries(target));
    }

    [Fact]
    public void SkillInstall_WhoseTimeoutEndsBeforeItPublishes_LeavesTheInstalledSkill()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");
        Assert.Equal(0, workspace.Run("skill", "install", Skill, "--target", target, "--output-format", "json").ExitCode);
        string installed = Path.Combine(target, Skill);
        string[] before = Tree(installed);

        // Under --timeout the install runs in a worker that may only stage; the timeout ends it
        // while the publication waits for the Skills root, so the installed Skill is untouched.
        CliResult timedOut;
        using (PublicationDirectoryLease.Acquire(target))
        {
            timedOut = workspace.Run("skill", "install", Skill, "--target", target, "--output-format", "json", "--timeout", "3");
        }

        Assert.Equal(9, timedOut.ExitCode);
        Assert.Equal("OPERATION_TIMEOUT", ErrorCode(timedOut));
        Assert.Equal(before, Tree(installed));
        Assert.Equal([Skill], Entries(target));
    }

    [Fact]
    public async Task ConcurrentSkillInstalls_IntoOneTarget_LeaveOneCompleteCopy()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");
        string reference = workspace.File("reference");
        Assert.Equal(0, workspace.Run("skill", "install", Skill, "--target", reference, "--output-format", "json").ExitCode);

        CliResult[] results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            workspace.Run("skill", "install", Skill, "--target", target, "--output-format", "json"))));

        // Serialized installs replace one another whole; one that cannot wait is refused.
        Assert.Contains(results, static result => result.ExitCode == 0);
        Assert.All(results, result => Assert.True(result.ExitCode == 0 || ErrorCode(result) == "OUTPUT_EXISTS", result.StdErr));
        Assert.Equal(Tree(Path.Combine(reference, Skill)), Tree(Path.Combine(target, Skill)));
        Assert.Equal([Skill], Entries(target));
    }

    private static string ErrorCode(CliResult result) =>
        JsonNode.Parse(result.StdErr)!["error"]!["code"]!.GetValue<string>();

    private static string[] Entries(string directory) =>
        Directory.EnumerateFileSystemEntries(directory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

    /// <summary>Every file below a directory with its content hash.</summary>
    private static string[] Tree(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path) + ":" + FileHashes.Sha256(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
}
