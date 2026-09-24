using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// Agents run several CLI processes at once. Publication and Skill installation hold
/// resource-based interprocess locks, so a second process waits or refuses and never
/// leaves a target half written.
/// </summary>
public sealed class CrossProcessLockTests
{
    private const string Skill = "aspose-cli-cells";

    [Fact]
    public void Edit_WhileAnotherProcessHoldsThePublicationLock_PublishesNothingUntilItIsReleased()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        byte[] original = File.ReadAllBytes(workspace.File("book.xlsx"));
        string[] edit = ["cells", "edit", "book.xlsx", "--in-place", "--set", "Data!A1=changed", "--output", "json"];

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
        Assert.Equal(0, workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        string fingerprint = JsonNode.Parse(workspace.Run("cells", "inspect", "book.xlsx", "--output", "json").StdOut)!
            ["source"]!["fingerprint"]!["sha256"]!.GetValue<string>();

        CliResult[] results = await Task.WhenAll(new[] { "first", "second" }.Select(value => Task.Run(() =>
            workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--if-match", fingerprint,
                "--set", $"Data!A1={value}", "--output", "json"))));

        CliResult winner = Assert.Single(results, static result => result.ExitCode == 0);
        CliResult loser = Assert.Single(results, static result => result.ExitCode != 0);
        Assert.Empty(loser.StdOut);
        Assert.Equal("INPUT_CHANGED", ErrorCode(loser));
        string written = JsonNode.Parse(winner.StdOut)!["output"]!["fingerprint"]!["sha256"]!.GetValue<string>();
        Assert.Equal(written, FileHashes.Sha256(workspace.File("book.xlsx")));
        Assert.Equal(["book.xlsx"], Entries(workspace.Path));
    }

    [Fact]
    public void SkillInstall_WhileAnotherProcessHoldsTheTarget_IsRefusedWithoutTouchingIt()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");
        string reference = workspace.File("reference");
        Assert.Equal(0, workspace.Run("skill", "install", Skill, "--target", reference, "--output", "json").ExitCode);
        string installed = Path.GetFullPath(Path.Combine(target, Skill));

        CliResult refused;
        using (LocalServiceOperationLock.Acquire("skill-install",
            OperatingSystem.IsWindows() ? installed.ToUpperInvariant() : installed, TimeSpan.FromSeconds(5)))
        {
            refused = workspace.Run("skill", "install", Skill, "--target", target, "--output", "json");
        }

        Assert.Equal("OUTPUT_EXISTS", ErrorCode(refused));
        Assert.False(Directory.Exists(installed));
        CliResult accepted = workspace.Run("skill", "install", Skill, "--target", target, "--output", "json");
        Assert.True(accepted.ExitCode == 0, accepted.StdErr);
        Assert.Equal(Tree(Path.Combine(reference, Skill)), Tree(installed));
    }

    [Fact]
    public async Task ConcurrentSkillInstalls_IntoOneTarget_LeaveOneCompleteCopy()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");
        string reference = workspace.File("reference");
        Assert.Equal(0, workspace.Run("skill", "install", Skill, "--target", reference, "--output", "json").ExitCode);

        CliResult[] results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            workspace.Run("skill", "install", Skill, "--target", target, "--output", "json"))));

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
