using Aspose.Cli.Sdk;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class CliRunnerProjectTests
{
    [Fact]
    public void ExecutableResolutionStaysInsideTheSelectedProject()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string executable = CliRunner.GetExecutablePath(root, "Release");
        Assert.Equal(Path.Combine(root, "src", "Aspose.Cli", "bin", "Release", "net10.0",
            DistributionInfo.CommandName + (OperatingSystem.IsWindows() ? ".exe" : "")), executable);
        Assert.Equal([Path.Combine(root, "src")], CliRunner.GetSourceRoots(root));
        Assert.Contains(Path.Combine(root, "Directory.Build.props"), CliRunner.GetBuildConfigurationInputs(root));
    }

    [Fact]
    public void AnExecutableFromAnotherDistributionIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => CliRunner.ValidateExecutable(
            Path.Combine(Path.GetTempPath(), "another-cli" + (OperatingSystem.IsWindows() ? ".exe" : ""))));
    }
}
