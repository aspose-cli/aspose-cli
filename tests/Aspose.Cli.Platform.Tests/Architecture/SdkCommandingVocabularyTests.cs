using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

public sealed class SdkCommandingVocabularyTests
{
    private static readonly Regex ProductVocabulary = new(
        "slide|sheet|workbook|presentation|spreadsheet|pdf|docx|xlsx|pptx",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void SdkCommandingCode_TakesEveryDocumentNounFromTheProducts()
    {
        string directory = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Sdk", "Extensibility", "Commanding");
        string[] findings = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(static file => File.ReadLines(file).Select((line, index) => (File: file, Line: line, Number: index + 1)))
            .Where(static entry => ProductVocabulary.IsMatch(entry.Line))
            .Select(static entry => $"{Path.GetFileName(entry.File)}:{entry.Number}: {entry.Line.Trim()}")
            .ToArray();

        Assert.NotEmpty(Directory.EnumerateFiles(directory, "*.cs"));
        Assert.Empty(findings);
    }
}
