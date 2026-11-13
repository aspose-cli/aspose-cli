using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A <c>--workdir</c> that is empty or only white space, as an unset variable in a script gives
/// it, names no directory: it is refused as an empty path is, naming the option, before anything
/// is read or written, rather than taken for the current directory. A directory given still
/// resolves the command's paths.
/// </summary>
public sealed class EmptyWorkDirectoryCliTests
{
    public static TheoryData<string, string> Commands()
    {
        var commands = new TheoryData<string, string>();
        foreach (string workDir in new[] { "", " " })
        {
            commands.Add("cells create out.xlsx", workDir);
            commands.Add("words create out.docx", workDir);
            commands.Add("slides create out.pptx", workDir);
            commands.Add("pdf create out.pdf --from-text text.txt", workDir);
        }

        return commands;
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void AnEmptyWorkDirectory_IsRefused(string command, string workDir)
    {
        using var workspace = Workspace();

        CliResult refused = workspace.Run([.. command.Split(' '), "--workdir", workDir, "--output-format", "json"]);

        Assert.True(refused.ExitCode == 2, refused.StdOut + refused.StdErr);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal(("OPTION_INVALID", "--workdir"), (error["code"]!.GetValue<string>(), error["details"]!["option"]!.GetValue<string>()));
        Assert.False(File.Exists(workspace.File(command.Split(' ')[2])));
    }

    [Fact]
    public void AWorkDirectory_ResolvesTheCommandsPaths()
    {
        using var workspace = Workspace();
        Directory.CreateDirectory(workspace.File("sub"));
        File.Copy(workspace.File("text.txt"), workspace.File("sub/text.txt"));

        workspace.Run("pdf", "create", "out.pdf", "--from-text", "text.txt", "--workdir", "sub", "--output-format", "json").Succeeded();

        Assert.True(File.Exists(workspace.File("sub/out.pdf")));
        Assert.False(File.Exists(workspace.File("out.pdf")));
    }

    private static TempWorkspace Workspace()
    {
        var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllText(workspace.File("text.txt"), "Hello\n");
        return workspace;
    }
}
