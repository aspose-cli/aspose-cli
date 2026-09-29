using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Known issues SLIDES-CJK-FALLBACK and SLIDES-FALLBACK-STDOUT: every presentation gets one
/// fallback font list for its script, and the engine's standard output noise never reaches the
/// CLI result.
/// </summary>
public sealed class SlidesCjkFallbackTests
{
    [Theory]
    [InlineData("季度经营回顾", "Microsoft YaHei")]
    [InlineData("四半期の業績レビュー", "Yu Gothic")]
    [InlineData("분기 실적 검토", "Malgun Gothic")]
    public void Apply_NamesTheFontsOfThePresentationsScript(string text, string first)
    {
        using var presentation = new Presentation();
        presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 300, 50).TextFrame.Text = text;

        SlidesCjkFallback.Apply(presentation);

        IFontFallBackRule[] rules = presentation.FontsManager.FontFallBackRulesCollection.Cast<IFontFallBackRule>().ToArray();
        Assert.Contains(rules, static rule => rule.RangeStartIndex <= 0x4E00 && rule.RangeEndIndex >= 0x9FFF);
        Assert.All(rules, rule => Assert.Equal(first, rule.ToArray()[0]));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Convert_KeepsTheJsonResultTheOnlyStandardOutput()
    {
        using var workspace = new TempWorkspace();
        using (var presentation = new Presentation())
        {
            presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 600, 80).TextFrame.Text = "问题与对策";
            presentation.Save(workspace.File("deck.pptx"), Aspose.Slides.Export.SaveFormat.Pptx);
        }

        CliResult result = workspace.Run("slides", "convert", "deck.pptx", "--to", "pdf", "--output", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.DoesNotContain("Updating of Inner rules", result.StdOut, StringComparison.Ordinal);
        Assert.NotNull(JsonNode.Parse(result.StdOut));
    }
}
