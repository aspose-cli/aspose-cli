using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A path written as a device path, such as <c>\\?\C:\work\a.xlsx</c>, is the plain path it spells,
/// for an input and for <c>--workdir</c> alike, so the command reads and writes there; one that
/// spells no plain path, such as <c>\\.\PhysicalDrive0</c>, is refused as its parameter's mistake
/// rather than failing inside a command. A plain absolute or relative directory, with a space or
/// without, still holds what the command reads and writes.
/// </summary>
public sealed class DevicePathCliTests
{
    [Theory]
    [InlineData("cells", "a.xlsx")]
    [InlineData("words", "a.docx")]
    [InlineData("slides", "a.pptx")]
    public void ADeviceInput_IsReadAsThePlainPathItSpells(string product, string file)
    {
        using var workspace = Workspace();
        workspace.Run(product, "create", file, "--output-format", "json").Succeeded();

        JsonNode read = workspace.Run(product, "read", @"\\?\" + workspace.File(file), "--output-format", "json").Json();

        Assert.Equal(workspace.File(file), read["input"]!["path"]!.GetValue<string>(), ignoreCase: true);
    }

    [Theory]
    [InlineData(@"\\?\")]
    [InlineData(@"\\.\")]
    public void ADeviceWorkDirectory_HoldsWhatTheCommandReadsAndWrites(string prefix)
    {
        using var workspace = Workspace();

        JsonNode read = workspace.Run("cells", "read", "a.csv", "--workdir", prefix + workspace.Path, "--output-format", "json").Json();
        workspace.Run("cells", "create", "out.xlsx", "--workdir", prefix + workspace.Path, "--output-format", "json").Succeeded();

        Assert.Equal(workspace.File("a.csv"), read["input"]!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.True(File.Exists(workspace.File("out.xlsx")));
    }

    /// <summary>A device path that spells no plain path is refused, as an input and as the work directory, with the same words.</summary>
    [Theory]
    [InlineData("cells", "read", @"\\.\PhysicalDrive0", "file", "argument")]
    [InlineData("cells", "read", "a.csv", "--workdir", "option")]
    public void ADevicePathThatSpellsNoPlainPath_IsRefused(string product, string command, string file, string parameter, string key)
    {
        using var workspace = Workspace();
        string[] args = parameter == "--workdir"
            ? [product, command, file, "--workdir", @"\\.\PhysicalDrive0", "--output-format", "json"]
            : [product, command, file, "--output-format", "json"];

        CliResult refused = workspace.Run(args);

        Assert.True(refused.ExitCode == 2, refused.StdOut + refused.StdErr);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal(("OPTION_INVALID", parameter), (error["code"]!.GetValue<string>(), error["details"]![key]!.GetValue<string>()));
        Assert.Equal($@"Invalid use of {parameter}: a device path that spells no plain path, such as \\.\PhysicalDrive0, is not accepted", error["message"]!.GetValue<string>());
        Assert.Equal(@"Pass the file or directory by its plain path, such as C:\work or \\server\share.", error["hint"]!.GetValue<string>());
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
