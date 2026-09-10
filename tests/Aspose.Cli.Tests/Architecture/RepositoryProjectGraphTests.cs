using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;

namespace Aspose.Cli.Tests;

/// <summary>Checks the first-party graph of this independent project.</summary>
public sealed class RepositoryProjectGraphTests
{
    private static readonly string Root = FindRoot();

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
        using JsonDocument identity = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "eng", "distribution.json")));
        bool foss = identity.RootElement.GetProperty("edition").GetString() == "foss";
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "eng", "products.json")));
        foreach (JsonElement product in catalog.RootElement.GetProperty("products").EnumerateArray())
        {
            string name = product.GetProperty("productName").GetString()!;
            string id = product.GetProperty("id").GetString()!;
            string project = Path.Combine(Root, "src", $"Aspose.Cli.Product.{name}", $"Aspose.Cli.Product.{name}.csproj");
            string sdk = Path.Combine(Root, "src", "Aspose.Cli.Sdk", "Aspose.Cli.Sdk.csproj");
            string[] references = ProjectReferences(project).ToArray();
            Assert.Contains(sdk, references);
            if (foss)
            {
                string engine = Path.Combine(Root, "src", $"Aspose.{name}.Foss.Engine", $"Aspose.{name}.Foss.Engine.csproj");
                Assert.Equal(new[] { sdk, engine }.Order(StringComparer.Ordinal), references.Order(StringComparer.Ordinal));
                string[] upstream = ProjectReferences(engine).ToArray();
                Assert.NotEmpty(upstream);
                Assert.All(upstream, path => Assert.True(IsUnder(path, Path.Combine(Root, "external", id))));
            }
            else
            {
                Assert.Equal(new[] { sdk }, references);
                string packageId = product.GetProperty("sdkPackageId").GetString()!;
                Assert.Contains(XDocument.Load(project).Descendants("PackageReference"), item => (string?)item.Attribute("Include") == packageId);
            }
        }
        if (!foss)
        {
            Assert.False(Directory.Exists(Path.Combine(Root, "external")));
            Assert.False(File.Exists(Path.Combine(Root, ".gitmodules")));
        }
    }

    [Fact]
    public void PlatformProjectsDoNotReferenceProductsOrEngines()
    {
        foreach (string name in new[] { "Aspose.Cli.Host", "Aspose.Cli.Sdk", "Aspose.Cli.Sdk.Analyzers" })
        {
            string project = Path.Combine(Root, "src", name, name + ".csproj");
            Assert.All(ProjectReferences(project), target => Assert.Equal("Aspose.Cli.Sdk.csproj", Path.GetFileName(target)));
        }
    }

    private static IEnumerable<string> FirstPartyProjects() =>
        new[] { "src", "tests" }.SelectMany(directory =>
            Directory.EnumerateFiles(Path.Combine(Root, directory), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"));

    private static IEnumerable<string> ProjectReferences(string project) =>
        XDocument.Load(project).Descendants("ProjectReference")
            .Select(item => (string?)item.Attribute("Include"))
            .Where(value => value is not null && !value.Contains("$(", StringComparison.Ordinal) && !value.StartsWith("@(", StringComparison.Ordinal))
            .Select(value => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, value!.Replace('\\', Path.DirectorySeparatorChar))));

    private static bool IsUnder(string path, string root) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string FindRoot([CallerFilePath] string file = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(file)!); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "eng", "distribution.json")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Independent project root not found.");
    }
}
