using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class ContinuationCommandTests
{
    [Fact]
    public void ToString_NamesThisExecutableQuotesValuesAndAsksForJson()
    {
        string command = new ContinuationCommand("pdf", "query", "pages")
            .Argument(@"C:\My Files\report $1.pdf")
            .Option("--pages", "4-9,12")
            .Flag("--notes")
            .Flag("--hidden", enabled: false)
            .Option("--max-chars", 20_000)
            .ToString();

        Assert.Equal(
            DistributionInfo.CommandName
                + " pdf query pages \"C:\\My Files\\report \\$1.pdf\" --pages 4-9,12 --notes --max-chars 20000 --output json",
            command);
    }

    [Theory]
    [InlineData("Sales", "Sales")]
    [InlineData("A1:F50", "A1:F50")]
    [InlineData("", "\"\"")]
    [InlineData("Q1 Sales", "\"Q1 Sales\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("`cmd`", "\"\\`cmd\\`\"")]
    [InlineData("@list", "\"@list\"")]
    public void Quote_LeavesSafeTokensBareAndEscapesShellSpecials(string value, string expected) =>
        Assert.Equal(expected, ContinuationCommand.Quote(value));

    [Fact]
    public void After_ResumesAfterTheLastCompletePartAsRanges()
    {
        ReadContinuation? next = ReadContinuation.After(
            [1, 2, 3, 4, 5, 9, 10],
            [new ReadPart(1, false), new ReadPart(2, false)],
            100);

        Assert.Equal(new ReadContinuation("3-5,9-10", 100), next);
    }

    [Fact]
    public void After_RereadsAPartTheBudgetCutShort()
    {
        ReadContinuation? next = ReadContinuation.After(
            [1, 2, 3],
            [new ReadPart(1, false), new ReadPart(2, true)],
            100);

        Assert.Equal(new ReadContinuation("2-3", 100), next);
    }

    [Fact]
    public void After_DoublesTheBudgetWhenOnePartAloneExceedsIt()
    {
        Assert.Equal(
            new ReadContinuation("2-3", 200),
            ReadContinuation.After([2, 3], [new ReadPart(2, true)], 100));
        Assert.Equal(
            new ReadContinuation("3", ReadContinuation.MaximumCharacters),
            ReadContinuation.After([2, 3], [new ReadPart(2, true)], ReadContinuation.MaximumCharacters));
    }

    [Fact]
    public void After_ReturnsNullWhenTheSelectionIsCovered() =>
        Assert.Null(ReadContinuation.After([1, 2], [new ReadPart(1, false), new ReadPart(2, false)], 100));

    [Fact]
    public void Describe_SpellsTheShortestRangeText()
    {
        Assert.Equal("1-3,7,9-10", PageRange.Describe([9, 1, 2, 3, 7, 10, 2]));
        Assert.Null(PageRange.Describe([]));
    }
}
