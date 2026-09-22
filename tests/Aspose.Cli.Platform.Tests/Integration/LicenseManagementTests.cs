using Aspose.Cli.TestKit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("License storage worker")]
public sealed class LicenseManagementTests
{
    [Fact]
    public void InvalidProductSource_DoesNotHideOtherStatusesOrClaimGlobalSuccess()
    {
        using var workspace = new TempWorkspace();
        string invalid = workspace.File(".aspose/licenses/pdf.lic");
        Directory.CreateDirectory(Path.GetDirectoryName(invalid)!);
        File.WriteAllText(invalid, "not a license");
        CliResult result = workspace.Run("license", "status", "--output", "json");
        Assert.Equal(0, result.ExitCode);
        JsonNode status = JsonNode.Parse(result.StdOut)!;
        Assert.Null(status["mode"]);
        Assert.Null(status["source"]);
        Assert.Null(status["license"]);
        JsonNode pdf = Assert.Single(status["products"]!.AsArray(), product => product!["product"]!.GetValue<string>() == "pdf")!;
        Assert.Equal("invalid", pdf["mode"]!.GetValue<string>());
        Assert.NotNull(pdf["problem"]);
        Assert.All(status["products"]!.AsArray().Where(product => product != pdf),
            product => Assert.Equal("evaluation", product!["mode"]!.GetValue<string>()));
    }

    [LicensedFact]
    public void ValidatedTotalLicense_InstallsAndRemovesAtomicallyThroughWorkers()
    {
        using var workspace = new TempWorkspace();
        string source = Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_LICENSE_PATH")!;
        string sourceHash = Hash(source);
        DateTime sourceTime = File.GetLastWriteTimeUtc(source);
        CliResult selected = workspace.Run("license", "install", source, "--product", "words", "--timeout", "20", "--output", "json");
        Assert.True(selected.ExitCode == 0, selected.StdErr);
        JsonNode selectedStatus = JsonNode.Parse(selected.StdOut)!;
        Assert.All(selectedStatus["products"]!.AsArray(), product =>
            Assert.Equal(product!["product"]!.GetValue<string>() == "words" ? "licensed" : "evaluation",
                product["mode"]!.GetValue<string>()));

        CliResult installed = workspace.Run("license", "install", source, "--timeout", "20", "--output", "json");
        Assert.True(installed.ExitCode == 0, installed.StdErr);
        Assert.All(JsonNode.Parse(installed.StdOut)!["products"]!.AsArray(), product =>
        {
            Assert.Equal("licensed", product!["mode"]!.GetValue<string>());
            Assert.True(product["userLicenseInstalled"]!.GetValue<bool>());
            Assert.Equal(sourceHash, Hash(product["path"]!.GetValue<string>()));
        });
        Assert.Equal(sourceHash, Hash(source));
        Assert.Equal(sourceTime, File.GetLastWriteTimeUtc(source));

        string invalid = workspace.File("invalid.lic");
        File.WriteAllText(invalid, "not a license", Encoding.UTF8);
        CliResult rejected = workspace.Run("license", "install", invalid, "--timeout", "20", "--output", "json");
        Assert.Equal(7, rejected.ExitCode);
        Assert.All(Directory.GetFiles(Path.Combine(workspace.ConfigDirectory, "licenses")), path =>
            Assert.Equal(sourceHash, Hash(path)));

        CliResult removed = workspace.Run("license", "remove", "--timeout", "20", "--output", "json");
        Assert.True(removed.ExitCode == 0, removed.StdErr);
        Assert.All(JsonNode.Parse(removed.StdOut)!["products"]!.AsArray(), product =>
        {
            Assert.Equal("evaluation", product!["mode"]!.GetValue<string>());
            Assert.False(product["userLicenseInstalled"]!.GetValue<bool>());
        });
        Assert.Empty(Directory.GetFiles(Path.Combine(workspace.ConfigDirectory, "licenses"), "*.lic"));
        Assert.Equal(0, workspace.Run("license", "remove", "--output", "json").ExitCode);
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
