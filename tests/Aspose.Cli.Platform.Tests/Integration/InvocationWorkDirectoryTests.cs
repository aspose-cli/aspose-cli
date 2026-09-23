using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed class InvocationWorkDirectoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RelativeWorkDirectory_AnchorsInputOutputAndLicenseExactlyOnce(bool timed)
    {
        using var workspace = new TempWorkspace();
        string nested = workspace.File("relative work");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "input.csv"), "Name,Value\nA,42\n");
        string[] options = timed ? ["--timeout", "30"] : [];
        CliResult converted = workspace.Run(["cells", "convert", "input.csv", "--to", "xlsx", "--out", "result.xlsx",
            "--workdir", "relative work", "--output", "json", .. options]);
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.True(File.Exists(Path.Combine(nested, "result.xlsx")));
        Assert.False(Directory.Exists(Path.Combine(nested, "relative work")));
        CliResult license = workspace.Run(["cells", "inspect", "input.csv", "--license", "missing.lic",
            "--workdir", "relative work", "--output", "json", .. options]);
        JsonNode error = JsonNode.Parse(license.StdErr)!["error"]!;
        Assert.Equal("LICENSE_FILE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Equal(Path.Combine(nested, "missing.lic"), error["details"]!["path"]!.GetValue<string>());
    }
}

public sealed partial class McpProtocolTests
{
    [Fact]
    public async Task Execute_RelativeOverrideUsesInheritedWorkDirectoryOnce()
    {
        using var temp = new TempDirectory();
        string work = temp.File("work");
        string selected = Path.Combine(work, "child");
        Directory.CreateDirectory(selected);
        File.WriteAllText(Path.Combine(selected, "input.csv"), "Name,Value\nA,42\n");
        await using var server = await Server.Start(temp.Path, work);
        JsonNode reply = await server.Execute(["cells", "convert", "input.csv", "--to", "xlsx", "--out", "result.xlsx",
            "--workdir", "child", "--output", "json"]);
        AssertSuccess(reply);
        Assert.True(File.Exists(Path.Combine(selected, "result.xlsx")));
        Assert.False(Directory.Exists(Path.Combine(selected, "child")));
    }
}
