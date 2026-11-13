using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A hint's command and a <c>window.next</c> on the invocation's input keep the <c>--workdir</c>
/// the invocation was given, as the absolute directory it resolved to, so, run as written from any
/// directory, they read and write where the invocation would have: a relative input and a relative
/// output resolve against that directory, not the current one. A command line without
/// <c>--workdir</c> gets none.
/// </summary>
public sealed partial class WorkDirectoryHintCliTests
{
    [Theory]
    [InlineData("--to", "png")]
    [InlineData("--out", "w.png")]
    public void AHintOfAnInvocationWithAWorkDirectory_RunsThereFromAnyDirectory(string option, string value)
    {
        using var workspace = Workspace();
        Run(workspace, "slides", "create", "deck.pptx", "--from-markdown", "outline.md").Succeeded();

        CliResult refused = Run(workspace, "slides", "convert", "deck.pptx", option, value);

        string command = QuotedCommand().Match(JsonNode.Parse(refused.StdErr)!["error"]!["hint"]!.GetValue<string>()).Groups[1].Value;
        Assert.Contains($" --workdir \"{workspace.File("sub")}\" ", command, StringComparison.Ordinal);
        using var elsewhere = new TempWorkspace();
        JsonNode rendered = elsewhere.RunCommandLine(command).Json();
        Assert.All(rendered["outputs"]!.AsArray(), output => Assert.StartsWith(workspace.File("sub") + Path.DirectorySeparatorChar,
            output!["path"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.GetFiles(workspace.Path, "*.png"));
    }

    [Fact]
    public void AWindowNextOfAReadWithAWorkDirectory_RunsThereFromAnyDirectory()
    {
        using var workspace = Workspace();
        File.WriteAllLines(workspace.File("sub/grid.csv"), Enumerable.Range(1, 10).Select(static row => $"{row},{row * 2},{row * 3}"));

        string next = Run(workspace, "cells", "read", "grid.csv", "--max-cells", "10").Json()["window"]!["next"]!.GetValue<string>();

        Assert.Contains($" --workdir \"{workspace.File("sub")}\" ", next, StringComparison.Ordinal);
        using var elsewhere = new TempWorkspace();
        Assert.Equal("A4:C6", elsewhere.RunCommandLine(next).Json()["sheet"]!["range"]!.GetValue<string>());
    }

    [Fact]
    public void AHintOfAnInvocationWithoutAWorkDirectory_NamesNone()
    {
        using var workspace = Workspace();
        Run(workspace, "slides", "create", "deck.pptx", "--from-markdown", "outline.md").Succeeded();

        CliResult refused = workspace.Run("slides", "convert", "sub/deck.pptx", "--to", "png", "--output-format", "json");

        Assert.DoesNotContain("--workdir", JsonNode.Parse(refused.StdErr)!["error"]!["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    // A command run with --workdir sub, the relative directory every hint and next must resolve.
    private static CliResult Run(TempWorkspace workspace, params string[] command) =>
        workspace.Run(["--workdir", "sub", .. command, "--output-format", "json"]);

    private static TempWorkspace Workspace()
    {
        var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        Directory.CreateDirectory(workspace.File("sub"));
        File.WriteAllText(workspace.File("sub/outline.md"), "# One\n\nFirst.\n\n# Two\n\nSecond.\n");
        return workspace;
    }

    [GeneratedRegex("'(aspose-cli [^']*)'")]
    private static partial Regex QuotedCommand();
}
