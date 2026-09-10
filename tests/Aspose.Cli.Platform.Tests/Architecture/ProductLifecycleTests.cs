using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductLifecycleTests
{


















    [Fact]
    public void CatalogRowAndProductSlice_AreTheCompleteProductLifecycle()
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
        AssertSuccess(RunGenerator(root));

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
        AssertSuccess(RunGenerator(root, check: true));

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

        string protectedFile = Path.Combine(generatedRoot, "notes.txt");
        File.WriteAllText(protectedFile, "authored", Encoding.UTF8);
        GeneratorResult protectedResult = RunGenerator(root, check: true);
        Assert.NotEqual(0, protectedResult.ExitCode);
        Assert.Contains("Unknown file", protectedResult.Output, StringComparison.Ordinal);
        Assert.Equal("authored", File.ReadAllText(protectedFile));
        protectedResult = RunGenerator(root);
        Assert.NotEqual(0, protectedResult.ExitCode);
        Assert.Contains("Unknown file", protectedResult.Output, StringComparison.Ordinal);
        Assert.Equal("authored", File.ReadAllText(protectedFile));
        File.Delete(protectedFile);

        string orphanSource = Path.Combine(
            root,
            "src",
            "Aspose.Cli.Product.Orphan");
        Directory.CreateDirectory(orphanSource);
        GeneratorResult orphanResult = RunGenerator(root, check: true);
        Assert.NotEqual(0, orphanResult.ExitCode);
        Assert.Contains("source catalog drift", orphanResult.Output, StringComparison.Ordinal);
        Directory.Delete(orphanSource);

        string orphanTest = Path.Combine(
            root,
            "tests",
            "Aspose.Cli.Product.Orphan.Tests");
        Directory.CreateDirectory(orphanTest);
        orphanResult = RunGenerator(root, check: true);
        Assert.NotEqual(0, orphanResult.ExitCode);
        Assert.Contains("test catalog drift", orphanResult.Output, StringComparison.Ordinal);
        Directory.Delete(orphanTest);

        WriteProductSlice(root, "beta", "Beta");
        WriteCatalog(root, ("alpha", "Alpha"), ("beta", "Beta"));
        AssertSuccess(RunGenerator(root));
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
        AssertSuccess(RunGenerator(root));

        composition = File.ReadAllText(
            Path.Combine(generatedRoot, "ProductComposition.props"));
        Assert.DoesNotContain("Aspose.Cli.Product.Alpha", composition, StringComparison.Ordinal);
        Assert.Contains("Aspose.Cli.Product.Beta", composition, StringComparison.Ordinal);
        AssertSuccess(RunGenerator(root, check: true));
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

    private static GeneratorResult RunGenerator(
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
        start.ArgumentList.Add("-OutputRoot");
        start.ArgumentList.Add(root);
        if (check)
        {
            start.ArgumentList.Add("-Check");
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start PowerShell.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("Product catalog generator timed out.");
        }
        Assert.True(Task.WaitAll([outputTask, errorTask], 5000), "Generator output drain timed out.");
        string stdout = outputTask.GetAwaiter().GetResult();
        string stderr = errorTask.GetAwaiter().GetResult();
        return new GeneratorResult(
            process.ExitCode,
            stdout + Environment.NewLine + stderr);
    }

    private static string[] ResolveBuildArguments(string edition)
    {
        string executable = OperatingSystem.IsWindows()
            ? "powershell.exe"
            : "pwsh";
        string resolver = Path.Combine(
            RepositoryPaths.Root,
            "scripts",
            "resolve-project-layout.ps1");
        string command = $"& {PowerShellLiteral(resolver)} "
            + $"-RepositoryRoot {PowerShellLiteral(RepositoryPaths.Root)} "
            + "| ConvertTo-Json -Compress -Depth 4";
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

        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                "Could not start the edition layout resolver.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        Assert.True(
            process.WaitForExit(30_000),
            "Edition layout resolver timed out.");
        Assert.True(
            process.ExitCode == 0,
            stdout + Environment.NewLine + stderr);

        using JsonDocument document = JsonDocument.Parse(stdout);
        return document.RootElement
            .GetProperty("BuildArguments")
            .EnumerateArray()
            .Select(static argument => argument.GetString() ?? string.Empty)
            .ToArray();
    }

    private static string PowerShellLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string RunGit(string repository, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"safe.directory={repository.Replace('\\', '/')}");
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(repository);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start Git.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"Git timed out in {repository}.");
        }
        Assert.True(process.ExitCode == 0, stderr.GetAwaiter().GetResult());
        return stdout.GetAwaiter().GetResult().Trim();
    }

    private static void AssertSuccess(GeneratorResult result) =>
        Assert.True(result.ExitCode == 0, result.Output);

    private sealed record GeneratorResult(int ExitCode, string Output);
}
