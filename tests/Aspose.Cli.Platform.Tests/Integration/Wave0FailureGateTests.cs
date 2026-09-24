using System.Text.Json;
using System.Xml.Linq;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Locks the release provenance and artifact ownership gates.</summary>
public sealed class Wave0FailureGateTests
{
    [Fact]
    public void Capabilities_ExposeTheCurrentDeterministicSourceRevision()
    {
        using var workspace = new TempWorkspace();
        CliResult first = workspace.Run("capabilities", "--output", "json");
        CliResult second = workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(first.StdOut, second.StdOut);

        using JsonDocument capabilities = JsonDocument.Parse(first.StdOut);
        string sourceRevision = capabilities.RootElement
            .GetProperty("sourceRevision")
            .GetString()!;

        bool dirty = capabilities.RootElement.GetProperty("buildDirty").GetBoolean();
        if (sourceRevision == "unknown")
        {
            Assert.True(dirty);
        }
        else
        {
            Assert.Matches("^[0-9a-f]{40}$", sourceRevision);
        }
        Assert.True(capabilities.RootElement.TryGetProperty("enginePins", out _));
        string declaredVersion = XDocument.Load(Path.Combine(RepositoryPaths.Root, "Directory.Build.props"))
            .Descendants("Version").Single().Value;
        Assert.Equal(declaredVersion, capabilities.RootElement.GetProperty("cliVersion").GetString());
    }

    [Fact]
    public void UnsignedCustomerInstaller_StopsBeforeResolvingThePackage()
    {
        Requires.Windows();
        string installRoot = Path.Combine(
            Path.GetTempPath(),
            "aspose-unsigned-installer-" + Guid.NewGuid().ToString("N"));
        try
        {
            CliResult result = RunPowerShell(
                [
                    "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                    "-File", Path.Combine(RepositoryPaths.Root, "install.ps1"),
                    "-PackageRoot", RepositoryPaths.Root,
                    "-InstallDirectory", installRoot,
                    "-SkipPath", "-SkipSkills", "-SkipMcp", "-SkipLicensePrompt",
                ],
                new Dictionary<string, string?>());

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "Customer installer Authenticode signature is not valid",
                result.StdErr + result.StdOut,
                StringComparison.Ordinal);
            Assert.False(Directory.Exists(installRoot));
        }
        finally
        {
            DeleteDirectory(installRoot);
        }
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Publish_RefusesToDeleteAnUnownedOutputTree()
    {
        Requires.Windows();
        string parent = Path.Combine(
            RepositoryPaths.Root,
            "artifacts",
            "publish",
            "wave0-unowned-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(parent, "win-x64");
        string sentinel = Path.Combine(output, "customer-owned.txt");
        Directory.CreateDirectory(output);
        File.WriteAllText(sentinel, "must survive", System.Text.Encoding.UTF8);
        string[] before = Directory.GetFileSystemEntries(parent, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();

        try
        {
            CliResult result = RunPowerShell(
                [
                    "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                    "-File", Path.Combine(RepositoryPaths.Root, "scripts", "publish.ps1"),
                    "-Configuration", "Release",
                    "-RuntimeIdentifier", "win-x64",
                    "-OutputRoot", output,
                ],
                new Dictionary<string, string?>(),
                developmentShell: true);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("exists without its ownership marker", result.StdErr, StringComparison.Ordinal);
            Assert.DoesNotContain("NamedParameterNotFound", result.StdErr, StringComparison.Ordinal);
            Assert.False(File.Exists(output + ".aspose-owner.json"));
            Assert.Equal(before, Directory.GetFileSystemEntries(parent, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            Assert.True(File.Exists(sentinel), result.StdOut + result.StdErr);
            Assert.Equal("must survive", File.ReadAllText(sentinel));
        }
        finally
        {
            DeleteDirectory(parent);
        }
    }

    [Fact]
    public void LocalInstallUsesTheProjectIdentityAndTransactionalInstaller()
    {
        string script = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "scripts", "install-local.ps1"));
        Assert.DoesNotContain("$selectedEdition", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-Edition", script, StringComparison.Ordinal);
        Assert.Contains("resolve-project-layout.ps1", script, StringComparison.Ordinal);
        Assert.Contains("DevelopmentPackage = $true", script, StringComparison.Ordinal);
        Assert.Contains("install.ps1", script, StringComparison.Ordinal);
    }

    private static CliResult RunPowerShell(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        bool developmentShell = false)
    {
        string executable = developmentShell
            ? ToolPath.Require("pwsh")
            : Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var workspace = new TempWorkspace();
        return new CliProcess(
            executable,
            CliEnvironment.Evaluation(workspace.Path, environment),
            TimeSpan.FromSeconds(120)).Run(workspace.Path, [.. arguments]);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
