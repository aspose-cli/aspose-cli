using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.Architecture.Tests;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Locks the release provenance and artifact ownership gates.</summary>
public sealed class Wave0FailureGateTests
{
    [Fact]
    [Trait("Tier", "Release")]
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
    }

    [Fact]
    [Trait("Tier", "Release")]
    public void PublishAndPackageScripts_RequireOwnedOutputAndHashInstallerScripts()
    {
        string publish = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root, "scripts", "publish.ps1"));
        string package = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root, "scripts", "package.ps1"));

        Assert.Contains("ownership marker", publish, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("install.cmd", package, StringComparison.Ordinal);
        Assert.Contains("install.ps1", package, StringComparison.Ordinal);
        Assert.Contains("Set-AuthenticodeSignature", package, StringComparison.Ordinal);
        Assert.Contains("-ExecutionPolicy AllSigned", package, StringComparison.Ordinal);
        Assert.Contains("else { @('install.ps1') }", package, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "-notin @('install.cmd', 'install.ps1', 'SHA256SUMS')",
            package,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Tier", "Release")]
    public void UnsignedCustomerInstaller_StopsBeforeResolvingThePackage()
    {
        RequireWindows();
        string installRoot = Path.Combine(
            Path.GetTempPath(),
            "aspose-unsigned-installer-" + Guid.NewGuid().ToString("N"));
        try
        {
            ProcessResult result = RunPowerShell(
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

    [Fact]
    [Trait("Tier", "Release")]
    public void Publish_RefusesToDeleteAnUnownedOutputTree()
    {
        RequireWindows();
        string parent = Path.Combine(
            RepositoryPaths.Root,
            "artifacts",
            "publish",
            "free",
            "wave0-unowned-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(parent, "win-x64");
        string sentinel = Path.Combine(output, "customer-owned.txt");
        string shimRoot = Directory.CreateTempSubdirectory("aspose-dotnet-shim-").FullName;
        string shim = Path.Combine(shimRoot, "dotnet.cmd");
        Directory.CreateDirectory(output);
        File.WriteAllText(sentinel, "must survive", System.Text.Encoding.UTF8);
        File.WriteAllText(shim, "@echo off\r\nexit /b 0\r\n", System.Text.Encoding.ASCII);

        try
        {
            ProcessResult result = RunPowerShell(
                [
                    "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                    "-File", Path.Combine(RepositoryPaths.Root, "scripts", "publish.ps1"),
                    "-Edition", "Free",
                    "-Configuration", "Release",
                    "-RuntimeIdentifier", "win-x64",
                    "-OutputRoot", output,
                ],
                new Dictionary<string, string?>
                {
                    ["Path"] = shimRoot + ";" + Environment.GetEnvironmentVariable("Path"),
                });

            Assert.NotEqual(0, result.ExitCode);
            Assert.True(File.Exists(sentinel), result.StdOut + result.StdErr);
            Assert.Equal("must survive", File.ReadAllText(sentinel));
        }
        finally
        {
            DeleteDirectory(parent);
            DeleteDirectory(shimRoot);
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

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Environment-blocked: Wave 0 release gates require Windows x64.");
        }
    }

    private static ProcessResult RunPowerShell(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        foreach ((string name, string? value) in environment)
        {
            start.Environment[name] = value;
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start PowerShell.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(120_000), "PowerShell release gate timed out.");
        return new(process.ExitCode, stdout, stderr);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
