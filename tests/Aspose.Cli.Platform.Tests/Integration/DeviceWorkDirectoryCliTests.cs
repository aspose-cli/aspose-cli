using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A <c>--workdir</c> written as a device path, such as <c>\\?\C:\work</c>, would make every file
/// below it a device path, which no output takes: it is refused as the option's mistake rather
/// than failing inside a command. A plain absolute or relative directory, with a space or without,
/// still holds what the command reads and writes.
/// </summary>
public sealed class DeviceWorkDirectoryCliTests
{
    /// <summary>The device path of a local directory, of a share (<c>\\?\UNC\server\share</c>) and of a device, each refused with the hint that fits them all.</summary>
    [Theory]
    [InlineData(@"\\?\", true)]
    [InlineData(@"\\.\", true)]
    [InlineData(@"\\?\UNC\server\share", false)]
    public void ADeviceWorkDirectory_IsRefused(string prefix, bool ofTheWorkspace)
    {
        using var workspace = Workspace();

        CliResult refused = workspace.Run("cells", "read", "a.csv", "--workdir", ofTheWorkspace ? prefix + workspace.Path : prefix, "--output-format", "json");

        Assert.True(refused.ExitCode == 2, refused.StdOut + refused.StdErr);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal(("OPTION_INVALID", "--workdir"), (error["code"]!.GetValue<string>(), error["details"]!["option"]!.GetValue<string>()));
        Assert.Equal(@"Invalid use of --workdir: a device path, such as one that starts with \\?\, is not accepted", error["message"]!.GetValue<string>());
        Assert.Equal(@"Pass the directory without the \\?\ prefix, such as C:\work or \\server\share.", error["hint"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("sub dir", true)]
    [InlineData("sub dir", false)]
    [InlineData("sub", true)]
    public void APlainWorkDirectory_HoldsWhatTheCommandReadsAndWrites(string folder, bool absolute)
    {
        using var workspace = Workspace();
        Directory.CreateDirectory(workspace.File(folder));
        File.Copy(workspace.File("a.csv"), workspace.File(Path.Combine(folder, "a.csv")));
        string workDir = absolute ? workspace.File(folder) : folder;

        JsonNode read = workspace.Run("cells", "read", "a.csv", "--workdir", workDir, "--output-format", "json").Json();
        workspace.Run("cells", "create", "out.xlsx", "--workdir", workDir, "--output-format", "json").Succeeded();

        Assert.Equal(workspace.File(Path.Combine(folder, "a.csv")), read["input"]!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.True(File.Exists(workspace.File(Path.Combine(folder, "out.xlsx"))));
    }

    private static TempWorkspace Workspace()
    {
        var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllText(workspace.File("a.csv"), "a,b\n1,2\n");
        return workspace;
    }
}
