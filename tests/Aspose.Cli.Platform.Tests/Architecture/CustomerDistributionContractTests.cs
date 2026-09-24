using System.Xml.Linq;
using System.Text.Json;
using Aspose.Cli.TestKit;
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
    public void SlidesProject_SelectsOnlyItsWindowsX64NativeAssetBeforeBundling()
    {
        using var directory = new TempDirectory();
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host)
            ? host
            : ToolPath.Require("dotnet");
        foreach (string projectName in new[] { "Aspose.Cli.Product.Slides", "Aspose.Cli" })
        {
            string project = Path.Combine(RepositoryPaths.Root, "src", projectName, projectName + ".csproj");
            CliResult result = new CliProcess(dotnet, CliEnvironment.Evaluation(directory.File("config")), TimeSpan.FromSeconds(30))
                .Run(directory.Path, args: ["msbuild", project, "-nologo", "-target:AssignTargetPaths",
                    "-property:AsposeCliPublishRuntimeIdentifier=win-x64", "-getItem:None"]);
            Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
            using JsonDocument evaluated = JsonDocument.Parse(result.StdOut);
            string[] native = evaluated.RootElement.GetProperty("Items").GetProperty("None").EnumerateArray()
                .Select(item => item.GetProperty("Identity").GetString()!)
                .Where(path => path.Contains("net6.0_native", StringComparison.Ordinal))
                .Select(path => Path.GetFileName(path)!).ToArray();
            if (projectName == "Aspose.Cli.Product.Slides")
            {
                Assert.Equal(["aspose.slides.drawing.capi_vc14x64.dll"], native);
            }
            else
            {
                // Its project reference supplies the already selected content.
                Assert.Empty(native);
            }
        }
    }

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
