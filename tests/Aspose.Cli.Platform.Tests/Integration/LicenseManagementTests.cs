using Aspose.Cli.TestKit;
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
        CliResult result = workspace.Run("license", "status", "--output-format", "json");
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

    /// <summary>
    /// What a license file holds never reaches a result or a diagnostic, from any source the CLI
    /// reads a license from, in results or in what --verbose adds.
    /// </summary>
    [Fact]
    public void LicenseContents_NeverEnterResultsOrDiagnostics()
    {
        using var workspace = new TempWorkspace();
        string marker = "LICENSE-CONTENT-" + Guid.NewGuid().ToString("N");
        string license =
            $"<License><Data><LicensedTo>{marker}</LicensedTo><SerialNumber>{marker}</SerialNumber>"
            + $"<Products><Product>Aspose.Total for .NET</Product></Products></Data><Signature>{marker}</Signature></License>";
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(license));
        File.WriteAllText(workspace.File("fake.lic"), license);
        File.WriteAllText(workspace.File("input.csv"), "Name,Value\nA,42\n");
        string stored = workspace.File(".aspose/licenses/pdf.lic");
        Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
        File.WriteAllText(stored, license);

        // JSON mode keeps stderr to one envelope, so the diagnostics --verbose adds are read in text mode.
        foreach (string[] mode in new[] { new[] { "--output-format", "json" }, new[] { "--verbose", "--output-format", "text" } })
        {
            Unseen(workspace.Run(["license", "install", "fake.lic", .. mode]));
            Unseen(workspace.Run(["license", "status", .. mode]));
            Unseen(workspace.Run(["cells", "inspect", "input.csv", "--license", "fake.lic", .. mode]));
            Unseen(workspace.RunWithEnv(
                new Dictionary<string, string?> { ["ASPOSE_LICENSE_PATH"] = workspace.File("fake.lic") },
                ["license", "status", .. mode]));
            Unseen(workspace.RunWithEnv(
                new Dictionary<string, string?> { ["ASPOSE_LICENSE_B64"] = base64 },
                ["cells", "inspect", "input.csv", .. mode]));
        }

        void Unseen(CliResult result)
        {
            string output = result.StdOut + result.StdErr;
            Assert.DoesNotContain(marker, output, StringComparison.Ordinal);
            Assert.DoesNotContain(base64[..32], output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InstallOfAFileThatIsNoLicense_StatesTheReasonOnce()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("bad.lic"), "not a license");

        CliResult result = workspace.Run("license", "install", "bad.lic", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("LICENSE_INVALID", error["code"]!.GetValue<string>());
        string message = error["message"]!.GetValue<string>();
        Assert.StartsWith("The license configured via file was rejected: it is not an Aspose license", message, StringComparison.Ordinal);
        Assert.Equal(2, message.Split("was rejected").Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyLicenseOption_IsRefusedAsEmptyInsteadOfNamingTheWorkDirectory(string value)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("input.csv"), "Name,Value\nA,42\n");

        CliResult result = workspace.Run("cells", "inspect", "input.csv", "--license", value, "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--license", error["details"]!["option"]!.GetValue<string>());
        Assert.Contains("value is empty", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void LicenseOptionNamingADirectory_SaysItIsADirectory()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("input.csv"), "Name,Value\nA,42\n");
        string directory = workspace.File("licenses");
        Directory.CreateDirectory(directory);

        CliResult result = workspace.Run("cells", "inspect", "input.csv", "--license", "licenses", "--output-format", "json");

        Assert.Equal(7, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("LICENSE_FILE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Contains("is a directory, not a license file", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(directory, error["details"]!["path"]!.GetValue<string>());
    }

    [Fact]
    public void SelectedConfigurationDirectoryWithForeignFiles_IsRefusedAndLeftUntouched()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.ConfigDirectory);
        string foreign = Path.Combine(workspace.ConfigDirectory, "notes.txt");
        File.WriteAllText(foreign, "user data");

        CliResult result = workspace.Run("preview", "status", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("OPTION_INVALID", result.StdErr, StringComparison.Ordinal);
        Assert.Equal("user data", File.ReadAllText(foreign));
        Assert.False(File.Exists(Path.Combine(workspace.ConfigDirectory, ".aspose-cli-config.json")));
    }

    [LicensedFact]
    public void ValidatedTotalLicense_InstallsAndRemovesAtomicallyThroughWorkers()
    {
        using var workspace = new TempWorkspace();
        string source = TestLicense.Path!;
        string sourceHash = FileHashes.Sha256(source);
        DateTime sourceTime = File.GetLastWriteTimeUtc(source);
        CliResult selected = workspace.Run("license", "install", source, "--product", "words", "--timeout", TestDeadline.CliTimeout, "--output-format", "json");
        Assert.True(selected.ExitCode == 0, selected.StdErr);
        JsonNode selectedStatus = JsonNode.Parse(selected.StdOut)!;
        Assert.All(selectedStatus["products"]!.AsArray(), product =>
            Assert.Equal(product!["product"]!.GetValue<string>() == "words" ? "licensed" : "evaluation",
                product["mode"]!.GetValue<string>()));

        CliResult installed = workspace.Run("license", "install", source, "--timeout", TestDeadline.CliTimeout, "--output-format", "json");
        Assert.True(installed.ExitCode == 0, installed.StdErr);
        Assert.All(JsonNode.Parse(installed.StdOut)!["products"]!.AsArray(), product =>
        {
            Assert.Equal("licensed", product!["mode"]!.GetValue<string>());
            Assert.True(product["userLicenseInstalled"]!.GetValue<bool>());
            Assert.Equal(sourceHash, FileHashes.Sha256(product["path"]!.GetValue<string>()));
        });
        Assert.Equal(sourceHash, FileHashes.Sha256(source));
        Assert.Equal(sourceTime, File.GetLastWriteTimeUtc(source));

        string invalid = workspace.File("invalid.lic");
        File.WriteAllText(invalid, "not a license", Encoding.UTF8);
        CliResult rejected = workspace.Run("license", "install", invalid, "--timeout", TestDeadline.CliTimeout, "--output-format", "json");
        Assert.Equal(7, rejected.ExitCode);
        Assert.All(Directory.GetFiles(Path.Combine(workspace.ConfigDirectory, "licenses")), path =>
            Assert.Equal(sourceHash, FileHashes.Sha256(path)));

        CliResult removed = workspace.Run("license", "remove", "--timeout", TestDeadline.CliTimeout, "--output-format", "json");
        Assert.True(removed.ExitCode == 0, removed.StdErr);
        Assert.All(JsonNode.Parse(removed.StdOut)!["products"]!.AsArray(), product =>
        {
            Assert.Equal("evaluation", product!["mode"]!.GetValue<string>());
            Assert.False(product["userLicenseInstalled"]!.GetValue<bool>());
        });
        Assert.Empty(Directory.GetFiles(Path.Combine(workspace.ConfigDirectory, "licenses"), "*.lic"));
        Assert.Equal(0, workspace.Run("license", "remove", "--output-format", "json").ExitCode);
    }
}
