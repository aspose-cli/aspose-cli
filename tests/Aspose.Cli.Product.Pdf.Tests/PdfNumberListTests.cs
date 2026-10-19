using Aspose.Cli.Product.Pdf.Commands;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>The comma-separated number lists of <c>--margins</c> and <c>--rect</c>.</summary>
public sealed class PdfNumberListTests
{
    [Fact]
    public void Parse_ReturnsTheNumbersInOrder()
    {
        Assert.Equal([1.5, -2, 3e2], PdfNumberList.Parse(" 1.5, -2 ,3e2", "--rect", "hint"));
    }

    [Theory]
    [InlineData("1,abc")]
    [InlineData("NaN")]
    [InlineData("1,Infinity")]
    [InlineData("-Infinity,1")]
    [InlineData("1e400")]
    public void Parse_RefusesAnEntryThatIsNotAFiniteNumber(string text)
    {
        CliException error = Assert.Throws<CliException>(() => PdfNumberList.Parse(text, "--rect", "hint"));

        Assert.Equal("OPTION_INVALID", error.Code.Name);
        Assert.Equal("--rect", error.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void ParseMargins_AcceptsOneOrFourNonNegativeValues()
    {
        Assert.Equal(new PdfMargins(10, 10, 10, 10), CreateCommand.ParseMargins("10"));
        Assert.Equal(new PdfMargins(1, 2, 3, 0), CreateCommand.ParseMargins("1,2,3,0"));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("1,2")]
    [InlineData("-1")]
    [InlineData("1,2,-3,4")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1,2,3,Infinity")]
    public void ParseMargins_RefusesAnythingElse(string text)
    {
        CliException error = Assert.Throws<CliException>(() => CreateCommand.ParseMargins(text));

        Assert.Equal("OPTION_INVALID", error.Code.Name);
        Assert.Equal("--margins", error.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void ParseRect_AcceptsXYAndAPositiveSize()
    {
        Assert.Equal(new PdfSignatureRect(-5, 0, 180, 60), SignCommand.ParseRect("-5,0,180,60"));
    }

    [Theory]
    [InlineData("x,0,1,1")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("0,0,0,10")]
    [InlineData("0,0,10,-1")]
    [InlineData("NaN,NaN,NaN,NaN")]
    [InlineData("NaN,0,10,10")]
    [InlineData("0,0,Infinity,10")]
    [InlineData("-Infinity,0,10,10")]
    public void ParseRect_RefusesAnythingElse(string text)
    {
        CliException error = Assert.Throws<CliException>(() => SignCommand.ParseRect(text));

        Assert.Equal("OPTION_INVALID", error.Code.Name);
        Assert.Equal("--rect", error.Details!["option"]!.GetValue<string>());
    }
}
