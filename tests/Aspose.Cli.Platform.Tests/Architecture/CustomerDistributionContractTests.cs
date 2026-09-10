using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class CustomerDistributionContractTests
{
    [Fact]
    public void Launcher_DeclaresOnlyVerifiedCustomerRuntimes()
    {
        string project = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli", "Aspose.Cli.csproj");
        string[] runtimes = XDocument.Load(project).Descendants("RuntimeIdentifiers")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["win-x64"], runtimes);
        string program = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli", "Program.cs"));
        Assert.Contains("static () => CompiledProductCatalog.Instance", program, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerPackaging_EnforcesSingleFileChecksumsAndInstallSmoke()
    {
        string publish = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "scripts",
            "publish.ps1"));
        Assert.Contains("--self-contained", publish, StringComparison.Ordinal);
        Assert.Contains("-p:PublishSingleFile=true", publish, StringComparison.Ordinal);
        Assert.Contains("-p:PublishTrimmed=false", publish, StringComparison.Ordinal);

        string package = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "scripts",
            "package.ps1"));
        Assert.Contains("SHA256SUMS", package, StringComparison.Ordinal);
        Assert.Contains("ECDSA-P256-SHA256", package, StringComparison.Ordinal);
        Assert.Contains("ASPOSE_CLI_RELEASE_SIGNING_KEY", package, StringComparison.Ordinal);
        Assert.Contains("Formal customer packaging requires", package, StringComparison.Ordinal);
        Assert.Contains("PACKAGE-SIGNATURE.json", package, StringComparison.Ordinal);
        Assert.DoesNotContain("status = 'unsigned'", package, StringComparison.Ordinal);
        Assert.Contains("AuthenticodeToolPath", package, StringComparison.Ordinal);
        Assert.Contains("Set-AuthenticodeSignature", package, StringComparison.Ordinal);
        Assert.Contains("-ExecutionPolicy AllSigned", package, StringComparison.Ordinal);
        Assert.Contains("if ($PrepareOnly) { @('install.cmd', 'install.ps1') } else { @('install.ps1') }", package, StringComparison.Ordinal);
        Assert.Contains("Customer installer smoke test pass", package, StringComparison.Ordinal);

        string installer = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "install.ps1"));
        Assert.Contains("Checksum mismatch", installer, StringComparison.Ordinal);
        Assert.Contains("$script:MarkerName = '.aspose-cli-install.json'", installer, StringComparison.Ordinal);
        Assert.Contains("$script:PayloadManifestName = '.aspose-cli-payload.json'", installer, StringComparison.Ordinal);
        Assert.Contains("$script:ProductId = 'aspose-cli'", installer, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 2", installer, StringComparison.Ordinal);
        Assert.Contains("Write-Journal", installer, StringComparison.Ordinal);
        Assert.Contains("SpecialFolder]::LocalApplicationData", installer, StringComparison.Ordinal);
        Assert.Contains("user PATH update did not persist", installer, StringComparison.Ordinal);
        Assert.Contains("GetErrorMode()", installer, StringComparison.Ordinal);
        Assert.Contains("-bor 0x00008003", installer, StringComparison.Ordinal);
        Assert.Contains("CreateNoWindow = $true", installer, StringComparison.Ordinal);
        Assert.Contains("Stop-CliChildProcessTree", installer, StringComparison.Ordinal);
        Assert.Contains("SkipMcp", installer, StringComparison.Ordinal);
        Assert.Contains("Register-OwnedMcp", installer, StringComparison.Ordinal);
        Assert.Contains("DevelopmentPackage", installer, StringComparison.Ordinal);
        Assert.Contains("Get-AuthenticodeSignature", installer, StringComparison.Ordinal);
        Assert.Contains("Customer installer Authenticode signature is not valid", installer, StringComparison.Ordinal);
        Assert.Contains("Assert-CustomerPackageTrust", installer, StringComparison.Ordinal);
        Assert.Contains("BoundedWriteStream", installer, StringComparison.Ordinal);
        string mcpCapture = installer.Split("function Invoke-OfficialMcp", 2)[1]
            .Split("function Register-OwnedMcp", 2)[0];
        Assert.DoesNotContain("ReadToEnd", mcpCapture, StringComparison.Ordinal);
        Assert.Contains("mcp','get','aspose-cli", installer, StringComparison.Ordinal);
        Assert.Contains("Invoke-CliChildProcess", installer, StringComparison.Ordinal);
        Assert.Contains("ownsCurrentExecutable", installer, StringComparison.Ordinal);

        string installerCommand = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "install.cmd"));
        Assert.Contains("-ExecutionPolicy Bypass", installerCommand, StringComparison.Ordinal);
        Assert.Contains("-DevelopmentPackage", installerCommand, StringComparison.Ordinal);
        Assert.Contains("Development package installer", installerCommand, StringComparison.Ordinal);
        Assert.Contains("install.ps1", installerCommand, StringComparison.Ordinal);

        string localInstaller = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "scripts",
            "install-local.ps1"));
        Assert.Contains("-PrepareOnly", localInstaller, StringComparison.Ordinal);
        Assert.Contains("SkipMcp", localInstaller, StringComparison.Ordinal);
        Assert.Contains("DevelopmentPackage = $true", localInstaller, StringComparison.Ordinal);
        Assert.DoesNotContain("$env:Path =", localInstaller, StringComparison.Ordinal);


    }

    [Fact]
    public void ExternalArchiveChecksum_CoversInstallerScriptChanges()
    {
        string root = Directory.CreateTempSubdirectory("aspose-archive-trust-").FullName;
        try
        {
            string payload = Path.Combine(root, "payload");
            Directory.CreateDirectory(payload);
            File.Copy(
                Path.Combine(RepositoryPaths.Root, "install.ps1"),
                Path.Combine(payload, "install.ps1"));
            File.WriteAllText(Path.Combine(payload, "aspose-cli.exe"), "payload", Encoding.UTF8);

            string originalArchive = Path.Combine(root, "original.zip");
            ZipFile.CreateFromDirectory(payload, originalArchive, CompressionLevel.NoCompression, includeBaseDirectory: false);
            string externalChecksum = Sha256(originalArchive);

            File.AppendAllText(Path.Combine(payload, "install.ps1"), "# changed", Encoding.UTF8);
            string changedArchive = Path.Combine(root, "changed.zip");
            ZipFile.CreateFromDirectory(payload, changedArchive, CompressionLevel.NoCompression, includeBaseDirectory: false);

            Assert.NotEqual(externalChecksum, Sha256(changedArchive));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    [Fact]
    public void BundledSkillDescriptions_UseSupportedSingleLineFrontMatter()
    {
        string sourceRoot = Path.Combine(RepositoryPaths.Root, "src");
        string[] skills = Directory.GetFiles(
            sourceRoot,
            "SKILL.md",
            SearchOption.AllDirectories);
        Assert.Equal(4, skills.Length);

        foreach (string skill in skills)
        {
            string[] lines = File.ReadAllLines(skill);
            Assert.True(lines.Length >= 4, skill);
            Assert.Equal("---", lines[0]);
            int end = Array.IndexOf(lines, "---", 1);
            Assert.True(end > 1, skill);
            string[] descriptions = lines[1..end]
                .Where(static line => line.StartsWith("description:", StringComparison.Ordinal))
                .ToArray();
            string description = Assert.Single(descriptions);
            string value = description["description:".Length..].Trim();
            Assert.False(string.IsNullOrWhiteSpace(value), skill);
            Assert.DoesNotMatch("^[>|]", value);
        }
    }
}
