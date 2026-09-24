using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The customer installer runs only with a valid signature; a source build reaches it only
/// through the local install, which marks the package as a development package.
/// </summary>
public sealed class InstallerTrustTests
{
    [Fact]
    public void UnsignedCustomerInstaller_StopsBeforeResolvingThePackage()
    {
        Requires.Windows();
        string installRoot = Path.Combine(
            Path.GetTempPath(),
            "aspose-unsigned-installer-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var workspace = new TempWorkspace();
            CliResult result = new CliProcess(
                Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                CliEnvironment.Evaluation(workspace.Path),
                TimeSpan.FromSeconds(120)).Run(
                workspace.Path,
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(RepositoryPaths.Root, "install.ps1"),
                "-PackageRoot", RepositoryPaths.Root,
                "-InstallDirectory", installRoot,
                "-SkipPath", "-SkipSkills", "-SkipMcp", "-SkipLicensePrompt");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "Customer installer Authenticode signature is not valid",
                result.StdErr + result.StdOut,
                StringComparison.Ordinal);
            Assert.False(Directory.Exists(installRoot));
        }
        finally
        {
            if (Directory.Exists(installRoot)) { Directory.Delete(installRoot, recursive: true); }
        }
    }

    [Fact]
    public void LocalInstall_HandsTheDevelopmentPackageToTheTransactionalInstaller()
    {
        string script = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "scripts", "install-local.ps1"));
        Assert.Contains("resolve-project-layout.ps1", script, StringComparison.Ordinal);
        Assert.Contains("DevelopmentPackage = $true", script, StringComparison.Ordinal);
        Assert.Contains("install.ps1", script, StringComparison.Ordinal);
    }
}
