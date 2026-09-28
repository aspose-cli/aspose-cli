using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>A source build reaches the installer only as an explicit development package.</summary>
public sealed class InstallerTrustTests
{
    [Fact]
    public void LocalInstall_HandsTheDevelopmentPackageToTheTransactionalInstaller()
    {
        string script = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "scripts", "install-local.ps1"));
        Assert.Contains("resolve-project-layout.ps1", script, StringComparison.Ordinal);
        Assert.Contains("DevelopmentPackage = $true", script, StringComparison.Ordinal);
        Assert.Contains("install.ps1", script, StringComparison.Ordinal);
    }
}
