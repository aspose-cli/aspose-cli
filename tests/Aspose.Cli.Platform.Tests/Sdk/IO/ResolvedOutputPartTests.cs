using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>How the files of an output written in parts are named beside the resolved output.</summary>
public sealed class ResolvedOutputPartTests
{
    private static readonly FormatDescriptor Png = FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png");

    private static readonly ResolvedOutput Report = new(Png, Path.Combine(Path.GetTempPath(), "report.png"));

    [Theory]
    [InlineData("p", "report.p3.png")]
    [InlineData("s", "report.s3.png")]
    public void Part_InsertsTheNumberedMarkerBeforeTheExtension(string marker, string expected)
    {
        Assert.Equal(Path.Combine(Path.GetTempPath(), expected), Report.Part(marker, 3, parts: 3));
    }

    [Fact]
    public void Part_OfASinglePartIsTheOutputItself()
    {
        Assert.Equal(Report.Path, Report.Part("p", 1, parts: 1));
    }

    [Fact]
    public void Part_NumbersEveryPartOfSeveral()
    {
        Assert.Equal(
            [
                Path.Combine(Path.GetTempPath(), "report.p1.png"),
                Path.Combine(Path.GetTempPath(), "report.p2.png"),
            ],
            [Report.Part("p", 1, parts: 2), Report.Part("p", 2, parts: 2)]);
    }

    [Fact]
    public void Part_NamedByALabelKeepsTheExtension()
    {
        Assert.Equal(Path.Combine(Path.GetTempPath(), "report.Summary.png"), Report.Part("Summary"));
    }

    [Fact]
    public void Part_RefusesAnInvalidMarkerNumberOrCount()
    {
        Assert.Throws<ArgumentException>(() => Report.Part(" ", 1, parts: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Report.Part("p", 0, parts: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Report.Part("p", 1, parts: 0));
        Assert.Throws<ArgumentException>(() => Report.Part(" "));
    }
}
