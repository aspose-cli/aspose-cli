using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesHtml5ConversionTests
{
    /// <summary>
    /// An HTML5 deck is a page with the scripts and style sheets the engine writes beside it;
    /// they are published with the page as one set and listed after it.
    /// </summary>
    [Fact]
    public void ConvertToHtml5_PublishesTheDeckWithItsCompanionFiles()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("outline.md"), "# Quarterly review\n\nGrowth and retention");
        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--output", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        Directory.CreateDirectory(workspace.File("site"));

        CliResult converted = workspace.Run(
            "slides", "convert", "deck.pptx", "--to", "html5", "--out", Path.Combine("site", "deck.html"), "--output", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonArray outputs = JsonNode.Parse(converted.StdOut)!["outputs"]!.AsArray();
        Assert.Equal("html5", outputs[0]!["format"]!.GetValue<string>());
        Assert.Equal(workspace.File(Path.Combine("site", "deck.html")), outputs[0]!["path"]!.GetValue<string>());
        string[] published = [.. Directory.GetFiles(workspace.File("site")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        Assert.Equal(
            ["animation.js", "deck.html", "effects.js", "master.css", "navigation.js", "pres.css"],
            published);
        Assert.Equal(published.Length, outputs.Count);
        // The scripts and style sheets are no HTML5 decks: they carry no format.
        Assert.All(outputs.Skip(1), static output => Assert.Null(output!["format"]));
    }
}
