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
    public void BuildCurrencyComparesEachProjectWithItsOwnAssembly()
    {
        using var temp = new TempDirectory();
        string executable = CliRunner.GetExecutablePath(temp.Path, "Release");
        string output = Path.GetDirectoryName(executable)!;
        DateTime past = DateTime.UtcNow.AddHours(-2);
        DateTime edited = DateTime.UtcNow.AddHours(-1);
        Write(Path.Combine(temp.Path, "src", "Aspose.Cli", "Aspose.Cli.csproj"), past);
        Write(Path.Combine(temp.Path, "src", "Product", "Product.csproj"), past);
        string source = Write(Path.Combine(temp.Path, "src", "Product", "Op.cs"), edited);
        Write(executable, past);
        Write(Path.ChangeExtension(executable, ".dll"), past);
        string product = Write(Path.Combine(output, "Product.dll"), DateTime.UtcNow);

        // An incremental build rewrote only the product assembly, not the apphost.
        CliRunner.EnsureCurrentBuild(temp.Path, executable);

        File.SetLastWriteTimeUtc(product, past);
        InvalidOperationException stale = Assert.Throws<InvalidOperationException>(
            () => CliRunner.EnsureCurrentBuild(temp.Path, executable));
        Assert.Contains(Path.GetRelativePath(temp.Path, source), stale.Message, StringComparison.Ordinal);
    }

    /// <summary>A tool under <c>eng</c> built after the launcher leaves the launcher current.</summary>
    [Fact]
    public void BuildOutputUnderEng_IsNoInputOfTheLauncher()
    {
        using var temp = new TempDirectory();
        string executable = Launcher(temp.Path);
        Write(Path.Combine(temp.Path, "eng", "tools", "CodeHealth", "bin", "Release", "net10.0", "CodeHealth.dll"), DateTime.UtcNow);
        Write(Path.Combine(temp.Path, "eng", "tools", "CodeHealth", "obj", "project.assets.json"), DateTime.UtcNow);

        CliRunner.EnsureCurrentBuild(temp.Path, executable);
    }

    /// <summary>A file under <c>eng</c> edited after the launcher was built makes it stale.</summary>
    [Fact]
    public void SourceUnderEng_IsAnInputOfTheLauncher()
    {
        using var temp = new TempDirectory();
        string executable = Launcher(temp.Path);
        Write(Path.Combine(temp.Path, "eng", "tools", "CodeHealth", "Program.cs"), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => CliRunner.EnsureCurrentBuild(temp.Path, executable));
    }

    private static string Launcher(string root)
    {
        string executable = CliRunner.GetExecutablePath(root, "Release");
        DateTime past = DateTime.UtcNow.AddHours(-1);
        Write(Path.Combine(root, "src", "Aspose.Cli", "Aspose.Cli.csproj"), past.AddMinutes(-1));
        Write(executable, past);
        Write(Path.ChangeExtension(executable, ".dll"), past);
        return executable;
    }

    private static string Write(string path, DateTime time)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        File.SetLastWriteTimeUtc(path, time);
        return path;
    }

    [Fact]
    public void AnExecutableFromAnotherDistributionIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => CliRunner.ValidateExecutable(
            Path.Combine(Path.GetTempPath(), "another-cli" + (OperatingSystem.IsWindows() ? ".exe" : ""))));
    }
}
