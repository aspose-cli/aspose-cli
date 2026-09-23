using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Aspose.Cli.Tests;

/// <summary>eng/products.json is the only roster of each product's packages.</summary>
public sealed class CatalogPackageDriftTests : IDisposable
{
    private static readonly string Root = FindRoot();
    private readonly string _root = Directory.CreateTempSubdirectory("aspose-catalog-drift-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void GeneratorRejectsProjectPackagesThatTheCatalogDoesNotDeclare()
    {
        foreach (string script in new[] { "generate-product-catalog.ps1", "resolve-project-layout.ps1" })
        {
            Directory.CreateDirectory(Path.Combine(_root, "scripts"));
            File.Copy(Path.Combine(Root, "scripts", script), Path.Combine(_root, "scripts", script));
        }
        Directory.CreateDirectory(Path.Combine(_root, "eng"));
        File.Copy(Path.Combine(Root, "eng", "distribution.json"), Path.Combine(_root, "eng", "distribution.json"));
        Write("eng/products.json", """
            {"schemaVersion":1,"products":[{"id":"alpha","productName":"Alpha","engine":"Sample.Engine","sdkPackageId":"Sample.Engine","sdkVersion":"1.0.0","supplementalPackages":{"Sample.Native":"2.0.0"}}]}
            """);
        Write("src/Aspose.Cli.Product.Alpha/AlphaModule.cs",
            "// Id = ProductBuildMetadata.ProductId DisplayName = ProductBuildMetadata.DisplayName ProductBuildMetadata.EngineName ProductBuildMetadata.SdkVersion");
        Write("tests/Aspose.Cli.Product.Alpha.Tests/Aspose.Cli.Product.Alpha.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        string project = "src/Aspose.Cli.Product.Alpha/Aspose.Cli.Product.Alpha.csproj";

        Write(project, Project("Sample.Engine", "Sample.Unlisted"));
        (int drifted, string output) = RunGenerator();
        Assert.NotEqual(0, drifted);
        Assert.Contains("drift from eng/products.json", output, StringComparison.Ordinal);
        Assert.Contains("missing: [Sample.Native]", output, StringComparison.Ordinal);
        Assert.Contains("not in the catalog: [Sample.Unlisted]", output, StringComparison.Ordinal);

        Write(project, Project("Sample.Engine", "Sample.Native"));
        (int matching, string matchingOutput) = RunGenerator();
        Assert.True(matching == 0, matchingOutput);
    }

    private static string Project(params string[] packages) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>"
        + string.Concat(packages.Select(package => $"<PackageReference Include=\"{package}\" />"))
        + "</ItemGroup></Project>";

    private void Write(string relative, string content)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private (int ExitCode, string Output) RunGenerator()
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", Path.Combine(_root, "scripts", "generate-product-catalog.ps1"), "-RepositoryRoot", _root })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromSeconds(60)), "The catalog generator did not finish.");
        // Windows PowerShell wraps redirected error text at the console width.
        string text = (output.Result + error.Result)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
        return (process.ExitCode, text);
    }

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
