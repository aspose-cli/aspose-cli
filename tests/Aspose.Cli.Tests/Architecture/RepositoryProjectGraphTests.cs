using System.Text.Json;
using System.Xml.Linq;

namespace Aspose.Cli.Tests;

/// <summary>Checks the first-party graph of this independent project.</summary>
public sealed class RepositoryProjectGraphTests
{
    private static readonly string Root = RepositoryPaths.Root;

    [Fact]
    public void ProjectReferencesAndBuildInputsAreSelfContained()
    {
        foreach (string project in FirstPartyProjects())
        {
            foreach (string reference in ProjectReferences(project))
            {
                Assert.True(IsUnder(reference, Root), $"Project reference escaped: {project} -> {reference}");
                Assert.True(File.Exists(reference), $"Missing project: {reference}");
            }
        }
        foreach (string name in new[] { "Directory.Build.props", "Directory.Packages.props", "global.json", "nuget.config", "eng/products.json", "eng/distribution.json" })
        {
            Assert.True(File.Exists(Path.Combine(Root, name)), $"Missing independent build input: {name}");
        }
    }

    [Fact]
    public void ProductsUseTheirMatchingEngineAndLocalSdk()
    {
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "eng", "products.json")));
        foreach (JsonElement product in catalog.RootElement.GetProperty("products").EnumerateArray())
        {
            string name = product.GetProperty("productName").GetString()!;
            string project = Path.Combine(Root, "src", $"Aspose.Cli.Product.{name}", $"Aspose.Cli.Product.{name}.csproj");
            string sdk = Path.Combine(Root, "src", "Aspose.Cli.Sdk", "Aspose.Cli.Sdk.csproj");
            string[] references = ProjectReferences(project).ToArray();
            Assert.Equal(new[] { sdk }, references);
            string packageId = product.GetProperty("sdkPackageId").GetString()!;
            Assert.Contains(XDocument.Load(project).Descendants("PackageReference"), item => (string?)item.Attribute("Include") == packageId);
        }
        Assert.False(Directory.Exists(Path.Combine(Root, "external")));
        Assert.False(File.Exists(Path.Combine(Root, ".gitmodules")));
    }

    [Fact]
    public void PlatformProjectsDoNotReferenceProductsOrEngines()
    {
        foreach (string name in new[] { "Aspose.Cli.Host", "Aspose.Cli.Sdk", "Aspose.Cli.Sdk.Analyzers" })
        {
            string project = Path.Combine(Root, "src", name, name + ".csproj");
            foreach ((XElement item, string target) in ProjectReferenceItems(project))
            {
                if (Path.GetFileName(target) == "Aspose.Cli.Sdk.Analyzers.csproj")
                {
                    // The SDK and the host run the contract generator on their own result records,
                    // as an analyzer whose assembly they never reference.
                    Assert.Contains(name, new[] { "Aspose.Cli.Sdk", "Aspose.Cli.Host" });
                    Assert.Equal("Analyzer", (string?)item.Attribute("OutputItemType"));
                    Assert.Equal("false", (string?)item.Attribute("ReferenceOutputAssembly"));
                }
                else
                {
                    Assert.Equal("Aspose.Cli.Sdk.csproj", Path.GetFileName(target));
                }
            }
        }
    }

    private static IEnumerable<string> FirstPartyProjects() =>
        new[] { "src", "tests" }.SelectMany(directory =>
            Directory.EnumerateFiles(Path.Combine(Root, directory), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"));

    private static IEnumerable<string> ProjectReferences(string project) =>
        ProjectReferenceItems(project).Select(static reference => reference.Target);

    private static IEnumerable<(XElement Item, string Target)> ProjectReferenceItems(string project) =>
        XDocument.Load(project).Descendants("ProjectReference")
            .Select(item => (Item: item, Include: (string?)item.Attribute("Include")))
            .Where(reference => reference.Include is { } value && !value.Contains("$(", StringComparison.Ordinal) && !value.StartsWith("@(", StringComparison.Ordinal))
            .Select(reference => (reference.Item, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference.Include!.Replace('\\', Path.DirectorySeparatorChar)))));

    private static bool IsUnder(string path, string root) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
