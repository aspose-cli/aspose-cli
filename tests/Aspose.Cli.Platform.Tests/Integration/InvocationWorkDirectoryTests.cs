using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed class InvocationWorkDirectoryTests
{
    [Fact]
    public void RelativeWorkDirectory_AnchorsInputOutputAndLicenseExactlyOnce()
    {
        using var workspace = new TempWorkspace();
        string nested = workspace.File("relative work");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "input.csv"), "Name,Value\nA,42\n");
        CliResult converted = workspace.Run(["cells", "convert", "input.csv", "--to", "xlsx", "--out", "result.xlsx",
            "--workdir", "relative work", "--output-format", "json"]);
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.True(File.Exists(Path.Combine(nested, "result.xlsx")));
        Assert.False(Directory.Exists(Path.Combine(nested, "relative work")));
        CliResult license = workspace.Run(["cells", "inspect", "input.csv", "--license", "missing.lic",
            "--workdir", "relative work", "--output-format", "json"]);
        JsonNode error = JsonNode.Parse(license.StdErr)!["error"]!;
        Assert.Equal("LICENSE_FILE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Equal(Path.Combine(nested, "missing.lic"), error["details"]!["path"]!.GetValue<string>());
    }
}
