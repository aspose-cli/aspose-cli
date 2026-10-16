using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Aspose.Slides.SlideShow;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The Aspose.Slides conversion defects in KNOWN-ISSUES.md, each reproduced with the SDK alone and
/// passing while the pinned SDK still has it.
/// </summary>
public sealed class SlidesConvertKnownIssueTests
{
    [LicensedFact]
    public void OdpSave_TurnsChartsIntoPicturesAndDropsTransitionsAndSections()
    {
        using var fixture = new SlidesEngineFixture();
        string path = fixture.File("deck.odp");
        using (var deck = new Presentation())
        {
            deck.Slides[0].Shapes.AddChart(ChartType.ClusteredColumn, 50, 50, 300, 200);
            deck.Slides[0].SlideShowTransition.Type = TransitionType.Fade;
            deck.Sections.AddSection("Part", deck.Slides[0]);
            deck.Save(path, SaveFormat.Odp);
        }

        using var reopened = new Presentation(path);
        bool chart = reopened.Slides[0].Shapes.Any(static shape => shape is IChart);
        TransitionType transition = reopened.Slides[0].SlideShowTransition.Type;
        int sections = reopened.Sections.Count;

        KnownIssue.Reproduces(
            "SLIDES-ODP-NATIVE-CONTENT",
            !chart && transition == TransitionType.None && sections == 0,
            $"the reopened ODP has a chart: {chart}, transition {transition}, {sections} section(s)");
    }

    /// <summary>
    /// A body placeholder's paragraphs take their bullets from the master. Markdown export writes
    /// them as plain lines, while the same paragraphs with explicit bullets become a list.
    /// </summary>
    [LicensedFact]
    public void MarkdownSave_IgnoresBulletsInheritedFromTheLayoutAndMaster()
    {
        using var fixture = new SlidesEngineFixture();
        _ = fixture.LicenseState;
        using var deck = new Presentation();
        ISlide slide = deck.Slides.AddEmptySlide(deck.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject));
        IAutoShape body = slide.Shapes.OfType<IAutoShape>()
            .First(static shape => shape.Placeholder?.Type is PlaceholderType.Object or PlaceholderType.Body);
        body.TextFrame.Paragraphs.Clear();
        foreach ((string text, short depth) in new (string, short)[] { ("Alpha", 0), ("Alpha one", 1), ("Beta", 0) })
        {
            var paragraph = new Paragraph { Text = text };
            paragraph.ParagraphFormat.Depth = depth;
            body.TextFrame.Paragraphs.Add(paragraph);
        }

        BulletType effective = body.TextFrame.Paragraphs[0].ParagraphFormat.GetEffective().Bullet.Type;
        string inherited = Export("inherited.md");
        foreach (IParagraph paragraph in body.TextFrame.Paragraphs)
        {
            paragraph.ParagraphFormat.Bullet.Type = BulletType.Symbol;
            paragraph.ParagraphFormat.Bullet.Char = '•';
        }
        string explicitBullets = Export("explicit.md");

        KnownIssue.Reproduces(
            "SLIDES-MD-INHERITED-BULLETS",
            effective == BulletType.Symbol && !inherited.Contains("- Alpha", StringComparison.Ordinal)
                && explicitBullets.Contains("- Alpha", StringComparison.Ordinal),
            $"with bullets of effective type {effective}, the inherited ones export as [{inherited.Trim()}] and explicit ones as [{explicitBullets.Trim()}]");

        string Export(string name)
        {
            string path = fixture.File(name);
            deck.Save(path, [deck.Slides.IndexOf(slide) + 1], SaveFormat.Md, new MarkdownSaveOptions());
            return File.ReadAllText(path);
        }
    }
}
