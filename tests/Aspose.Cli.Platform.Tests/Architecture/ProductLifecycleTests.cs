using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductLifecycleTests
{

    [Category(TestCategory.Slow)]
    [Fact]
    public async Task CatalogRowAndProductSlice_AreTheCompleteProductLifecycle()
    {
        using var temp = new TempDirectory();
        string root = temp.Path;
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "eng"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "tests"));
        File.Copy(
            Path.Combine(
                RepositoryPaths.Root,
                "scripts",
                "generate-product-catalog.ps1"),
            Path.Combine(root, "scripts", "generate-product-catalog.ps1"));
        File.Copy(
            Path.Combine(
                RepositoryPaths.Root,
                "scripts",
                "resolve-project-layout.ps1"),
            Path.Combine(root, "scripts", "resolve-project-layout.ps1"));

        File.Copy(
            Path.Combine(RepositoryPaths.Root, "eng", "distribution.json"),
            Path.Combine(root, "eng", "distribution.json"));
        WriteProductSlice(root, "alpha", "Alpha");
        WriteCatalog(root, ("alpha", "Alpha"));
        AssertSuccess(await RunGenerator(root));

        string generatedRoot = Path.Combine(
            root,
            "eng",
            "generated");
        string[] generatedNames = Directory.GetFiles(generatedRoot)
            .Select(static path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "DistributionBuild.props",
                "DistributionInfo.g.cs",
                "ProductComposition.props",
                "ProductVersions.props",
                "RepositoryBuild.props",
            },
            generatedNames);
        AssertCatalogHashes(root);
        using (JsonDocument identity = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "eng", "distribution.json"))))
        {
            string solution = File.ReadAllText(Path.Combine(root, identity.RootElement.GetProperty("solutionName").GetString()!));
            Assert.Contains("src/Aspose.Cli/Aspose.Cli.csproj", solution, StringComparison.Ordinal);
            Assert.Contains("tests/Aspose.Cli.Product.Alpha.Tests", solution, StringComparison.Ordinal);
            Assert.DoesNotContain("../", solution, StringComparison.Ordinal);
            Assert.DoesNotContain("external/", solution, StringComparison.Ordinal);
        }
        RewriteCatalogWithCrLf(root);
        AssertSuccess(await RunGenerator(root, check: true));

        string composition = File.ReadAllText(
            Path.Combine(generatedRoot, "ProductComposition.props"));
        Assert.Contains("Aspose.Cli.Product.Alpha", composition, StringComparison.Ordinal);
        string versions = File.ReadAllText(
            Path.Combine(generatedRoot, "ProductVersions.props"));
        Assert.DoesNotContain("Sample.Alpha", versions, StringComparison.Ordinal);
        string repositoryBuild = File.ReadAllText(
            Path.Combine(generatedRoot, "RepositoryBuild.props"));
        Assert.Contains(
            "<AsposeProductSdkPackageId></AsposeProductSdkPackageId>",
            repositoryBuild,
            StringComparison.Ordinal);
        Assert.Contains("<AsposeProductDisplayOrder>1</AsposeProductDisplayOrder>", repositoryBuild, StringComparison.Ordinal);
        Assert.Contains("<AsposeProductDefaultCandidate>false</AsposeProductDefaultCandidate>", repositoryBuild, StringComparison.Ordinal);

        string protectedFile = Path.Combine(generatedRoot, "notes.txt");
        File.WriteAllText(protectedFile, "authored", Encoding.UTF8);
        GeneratorResult protectedResult = await RunGenerator(root, check: true);
        Assert.NotEqual(0, protectedResult.ExitCode);
        Assert.Contains("Unknown file", protectedResult.Output, StringComparison.Ordinal);
        Assert.Equal("authored", File.ReadAllText(protectedFile));
        protectedResult = await RunGenerator(root);
        Assert.NotEqual(0, protectedResult.ExitCode);
        Assert.Contains("Unknown file", protectedResult.Output, StringComparison.Ordinal);
        Assert.Equal("authored", File.ReadAllText(protectedFile));
        File.Delete(protectedFile);

        string orphanSource = Path.Combine(
            root,
            "src",
            "Aspose.Cli.Product.Orphan");
        Directory.CreateDirectory(orphanSource);
        GeneratorResult orphanResult = await RunGenerator(root, check: true);
        Assert.NotEqual(0, orphanResult.ExitCode);
        Assert.Contains("source catalog drift", orphanResult.Output, StringComparison.Ordinal);
        Directory.Delete(orphanSource);

        string orphanTest = Path.Combine(
            root,
            "tests",
            "Aspose.Cli.Product.Orphan.Tests");
        Directory.CreateDirectory(orphanTest);
        orphanResult = await RunGenerator(root, check: true);
        Assert.NotEqual(0, orphanResult.ExitCode);
        Assert.Contains("test catalog drift", orphanResult.Output, StringComparison.Ordinal);
        Directory.Delete(orphanTest);

        WriteProductSlice(root, "beta", "Beta");
        WriteCatalog(root, ("alpha", "Alpha"), ("beta", "Beta"));
        AssertSuccess(await RunGenerator(root));
        composition = File.ReadAllText(
            Path.Combine(generatedRoot, "ProductComposition.props"));
        Assert.Contains("Aspose.Cli.Product.Alpha", composition, StringComparison.Ordinal);
        Assert.Contains("Aspose.Cli.Product.Beta", composition, StringComparison.Ordinal);

        Directory.Delete(
            Path.Combine(
                root,
                "src",
                "Aspose.Cli.Product.Alpha"),
            recursive: true);
        Directory.Delete(
            Path.Combine(
                root,
                "tests",
                "Aspose.Cli.Product.Alpha.Tests"),
            recursive: true);
        WriteCatalog(root, ("beta", "Beta"));
        AssertSuccess(await RunGenerator(root));

        composition = File.ReadAllText(
            Path.Combine(generatedRoot, "ProductComposition.props"));
        Assert.DoesNotContain("Aspose.Cli.Product.Alpha", composition, StringComparison.Ordinal);
        Assert.Contains("Aspose.Cli.Product.Beta", composition, StringComparison.Ordinal);
        AssertSuccess(await RunGenerator(root, check: true));
    }

    private static void WriteProductSlice(
        string root,
        string id,
        string productName)
    {
        string projectName = $"Aspose.Cli.Product.{productName}";
        string sourceRoot = Path.Combine(root, "src", projectName);
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(
            Path.Combine(sourceRoot, projectName + ".csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(sourceRoot, productName + "Module.cs"),
            $$"""
            namespace {{projectName}};

            public sealed class {{productName}}Module
            {
                public object Define() => new
                {
                    Id = ProductBuildMetadata.ProductId,
                    DisplayName = ProductBuildMetadata.DisplayName,
                    Sdk = ProductBuildMetadata.EngineName,
                    SdkVersion = ProductBuildMetadata.SdkVersion,
                    DisplayOrder = ProductBuildMetadata.DisplayOrder,
                    IsDefaultCandidate = ProductBuildMetadata.IsDefaultCandidate,
                };
            }
            """,
            Encoding.UTF8);

        string testName = projectName + ".Tests";
        string testRoot = Path.Combine(root, "tests", testName);
        Directory.CreateDirectory(testRoot);
        File.WriteAllText(
            Path.Combine(testRoot, testName + ".csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />\n",
            Encoding.UTF8);
    }

    private static void WriteCatalog(
        string root,
        params (string Id, string ProductName)[] products)
    {
        object[] rows = products
            .Select(static product => (object)new Dictionary<string, object?>
            {
                ["id"] = product.Id,
                ["productName"] = product.ProductName,
                ["engine"] = "Sample.Engine",
                ["sdkVersion"] = "1.0.0",
                ["supplementalPackages"] = new Dictionary<string, string>(),
            })
            .ToArray();
        string json = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["products"] = rows,
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(
            Path.Combine(root, "eng", "products.json"),
            json + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void AssertCatalogHashes(string root)
    {
        string catalog = Path.Combine(
            root,
            "eng",
            "products.json");
        string normalized = File.ReadAllText(catalog)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string expectedLf = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        string expectedCrLf = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                normalized.Replace("\n", "\r\n", StringComparison.Ordinal))));
        string projection = File.ReadAllText(
            Path.Combine(
                root,
                "eng",
                "generated",
                "RepositoryBuild.props"));
        Assert.Contains(
            $"<GeneratedProductsLfSha256>{expectedLf}</GeneratedProductsLfSha256>",
            projection,
            StringComparison.Ordinal);
        Assert.Contains(
            $"<GeneratedProductsCrLfSha256>{expectedCrLf}</GeneratedProductsCrLfSha256>",
            projection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneratedProductsRawSha256",
            projection,
            StringComparison.Ordinal);
    }

    private static void RewriteCatalogWithCrLf(string root)
    {
        string catalog = Path.Combine(
            root,
            "eng",
            "products.json");
        string normalized = File.ReadAllText(catalog)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        File.WriteAllText(
            catalog,
            normalized.Replace("\n", "\r\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static async Task<GeneratorResult> RunGenerator(
        string root,
        bool check = false)
    {
        string executable = OperatingSystem.IsWindows()
            ? "powershell.exe"
            : "pwsh";
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        if (OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
        }
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(
            Path.Combine(root, "scripts", "generate-product-catalog.ps1"));
        start.ArgumentList.Add("-RepositoryRoot");
        start.ArgumentList.Add(root);
        if (check)
        {
            start.ArgumentList.Add("-Check");
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start PowerShell.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(timeout.Token))
                .WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            Assert.Fail("Product catalog generator or output drain exceeded the shared 30-second deadline.");
        }
        string stdout = await outputTask;
        string stderr = await errorTask;
        return new GeneratorResult(
            process.ExitCode,
            stdout + Environment.NewLine + stderr);
    }

    private static void AssertSuccess(GeneratorResult result) =>
        Assert.True(result.ExitCode == 0, result.Output);

    private sealed record GeneratorResult(int ExitCode, string Output);
}
