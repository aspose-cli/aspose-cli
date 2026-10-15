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
}
