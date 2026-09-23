using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Tests;

/// <summary>Keeps eng/distribution.json the only author of the distribution identity.</summary>
public sealed class DistributionIdentityTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void InstallerAndReleaseScriptsReadTheIdentityInsteadOfSpellingIt()
    {
        using JsonDocument identity = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "eng", "distribution.json")));
        string id = identity.RootElement.GetProperty("id").GetString()!;
        string installDirectory = identity.RootElement.GetProperty("installDirectory").GetString()!;
        string[] spellings =
        [
            id,
            id.ToUpperInvariant(),
            installDirectory,
            installDirectory.Replace('/', '\\'),
        ];

        var files = Directory.EnumerateFiles(Path.Combine(Root, "scripts"), "*.ps1")
            .Append(Path.Combine(Root, "install.ps1"));
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            if (Path.GetFileName(file) == "install.ps1")
            {
                // The installer ships alone, so the generator projects the identity into this region.
                text = Regex.Replace(
                    text,
                    "(?s)# <generated-distribution-identity>.*?# </generated-distribution-identity>",
                    string.Empty);
            }
            foreach (string spelling in spellings)
            {
                Assert.False(
                    text.Contains(spelling, StringComparison.Ordinal),
                    $"{Path.GetRelativePath(Root, file)} spells the distribution identity '{spelling}'; read it from the project layout or the generated installer region.");
            }
        }
    }

    [Fact]
    public void BuildStalenessCheckCoversTheDistributionIdentity()
    {
        string projection = File.ReadAllText(Path.Combine(Root, "eng", "generated", "RepositoryBuild.props"));
        Assert.Contains("GeneratedDistributionLfSha256", projection, StringComparison.Ordinal);
        Assert.Contains(@"..\distribution.json", projection, StringComparison.Ordinal);
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
