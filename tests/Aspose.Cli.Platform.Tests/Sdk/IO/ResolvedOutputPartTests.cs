using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// How the files of an output written in parts are named: <c>&lt;stem&gt;.&lt;n&gt;.&lt;ext&gt;</c>, the
/// number padded to the digits of the highest one, beside the output or in an output directory.
/// </summary>
public sealed class ResolvedOutputPartTests
{
    private static readonly FormatDescriptor Png = FormatDescriptor.Declare("png", FormatUse.Render, false, ".png");

    private static readonly ResolvedOutput Report = new(Png, Path.Combine(Path.GetTempPath(), "report.png"));

    private static string[] Beside(params string[] names) =>
        [.. names.Select(static name => Path.Combine(Path.GetTempPath(), name))];

    [Fact]
    public void Parts_OfASinglePartIsTheOutputItself()
    {
        Assert.Equal([Report.Path], Report.Parts([7]));
    }

    [Fact]
    public void Parts_NumberEachPartBeforeTheExtension()
    {
        Assert.Equal(Beside("report.1.png", "report.2.png"), Report.Parts([1, 2]));
    }

    [Fact]
    public void Parts_PadTheNumbersToTheDigitsOfTheHighest()
    {
        Assert.Equal(Beside("report.09.png", "report.10.png", "report.11.png", "report.12.png"), Report.Parts([9, 10, 11, 12]));
        Assert.Equal(Beside("report.01.png", "report.12.png"), Report.Parts([1, 12]));
        Assert.Equal(Beside("report.003.png", "report.100.png"), Report.Parts([3, 100]));
    }

    [Fact]
    public void Part_NamedByALabelKeepsTheExtension()
    {
        Assert.Equal(Path.Combine(Path.GetTempPath(), "report.Summary.png"), Report.Part("Summary"));
    }

    [Fact]
    public void Parts_RefuseANumberBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Report.Parts([0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Report.Parts([1, 0]));
        Assert.Throws<ArgumentException>(() => Report.Part(" "));
    }

    [Fact]
    public void DirectoryParts_NameEachPartAfterTheStemInTheDirectory()
    {
        var directory = new ResolvedDirectory(Path.Combine(Path.GetTempPath(), "parts"));

        Assert.Equal(
            [Path.Combine(directory.Path, "report.1.pdf"), Path.Combine(directory.Path, "report.2.pdf")],
            directory.Parts("report", "pdf", [1, 2]));
        Assert.Equal(
            [Path.Combine(directory.Path, "report.1.docx")],
            directory.Parts("report", ".docx", [1]));
        Assert.Equal(
            [Path.Combine(directory.Path, "report.09.pdf"), Path.Combine(directory.Path, "report.10.pdf")],
            directory.Parts("report", "pdf", [9, 10]));
    }

    [Theory]
    [InlineData("a:b", "a-b")]
    [InlineData("  ", "untitled")]
    [InlineData("name.", "name")]
    public void DirectoryParts_ReplaceWhatAFileNameCannotHold(string stem, string expected)
    {
        var directory = new ResolvedDirectory(Path.GetTempPath());

        Assert.Equal([Path.Combine(directory.Path, $"{expected}.1.pdf")], directory.Parts(stem, "pdf", [1]));
    }
}
