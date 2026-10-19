using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class PageRangeTests
{
    [Fact]
    public void Resolve_SortsAndDeduplicatesAnOpenEndedRange() =>
        Assert.Equal([1, 2, 3, 5, 6], PageRange.Parse("5-,1-3,2").Resolve(6));

    [Theory]
    [InlineData("4", 3)]
    [InlineData("2-", 0)]
    public void Resolve_ReportsTheProductsNotFoundErrorForARangePastTheCount(string text, int available)
    {
        ErrorCode slideNotFound = ErrorCode.NotFound("SLIDE_NOT_FOUND");

        CliException error = Assert.Throws<CliException>(() => PageRange.Parse(text).Resolve(
            available, slideNotFound, "slide"));

        Assert.Equal(slideNotFound, error.Code);
        Assert.Equal("slide", error.Details!["subject"]!.GetValue<string>());
        Assert.Equal(PageRange.Parse(text).Text, error.Details["requested"]!.GetValue<string>());
        Assert.Equal(available, error.Details["availableCount"]!.GetValue<int>());
    }

    [Fact]
    public void Resolve_DefaultsToPageNotFoundAndNamesAnEmptyDocument()
    {
        CliException past = Assert.Throws<CliException>(() => PageRange.Parse("2-3").Resolve(2));
        CliException none = Assert.Throws<CliException>(() => PageRange.Parse("1").Resolve(0));

        Assert.Equal(ErrorCodes.PageNotFound, past.Code);
        Assert.Equal("Use a page from 1 through 2.", past.Hint);
        Assert.Equal(ErrorCodes.PageNotFound, none.Code);
        Assert.Contains("no page", none.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void All_SelectsEveryPart()
    {
        PageRange all = PageRange.Parse("all");

        Assert.Same(PageRange.All, all);
        Assert.Equal("1-", all.Text);
        Assert.Equal([1, 2, 3], all.Resolve(3));
        Assert.Same(PageRange.All, PageRange.ParseOption("--pages", " all "));
    }

    [Theory]
    [InlineData("3-x", "'x' is not a positive 1-based number")]
    [InlineData("", "the selection is empty")]
    [InlineData("1,,2", "an empty segment was found")]
    [InlineData("4-2", "segment '4-2' ends before it starts")]
    [InlineData("ALL", "the keyword all is lowercase")]
    [InlineData(" All ", "the keyword all is lowercase")]
    public void ParseOption_ReportsAMalformedValueAsAnOptionMistake(string text, string reason)
    {
        CliException error = Assert.Throws<CliException>(() => PageRange.ParseOption("--slides", text));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal(ExitCode.Usage, error.Code.ExitCode);
        Assert.Equal("--slides", error.Details!["option"]!.GetValue<string>());
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        Assert.Contains("all", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReportsAMalformedOperationFieldAsAnOperationMistakeWithItsReason()
    {
        CliException error = Assert.Throws<CliException>(() => PageRange.Parse("1-x"));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Equal("'x' is not a positive 1-based number", error.Details!["reason"]!.GetValue<string>());
    }

    [Fact]
    public void ErrorCodes_NoLongerDeclareAPageRangeCode() =>
        Assert.DoesNotContain(ErrorCodes.All, static code => code.Name == "PAGE_RANGE_INVALID");

    [Fact]
    public void RenderSelection_DefaultsToEveryPart() =>
        Assert.Equal([1, 2, 3], RenderSelection.Resolve(null, 3, ErrorCodes.PageNotFound, "page"));

    [Fact]
    public void RenderSelection_RefusesMoreThanTwentyPartsAsAnInvalidOption()
    {
        Assert.Equal(20, RenderSelection.Resolve(null, 20, ErrorCodes.PageNotFound, "page").Count);

        CliException error = Assert.Throws<CliException>(
            () => RenderSelection.Resolve(PageRange.Parse("2-"), 22, ErrorCodes.PageNotFound, "slide"));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal(ExitCode.Usage, error.Code.ExitCode);
        Assert.Equal("--slides", error.Details!["option"]!.GetValue<string>());
        Assert.Equal(20, error.Details["limit"]!.GetValue<int>());
        Assert.Equal(21, error.Details["actual"]!.GetValue<int>());
        Assert.Null(error.Details["errors"]);
        Assert.Contains("--slides 1-20", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderSelection_ReportsAPartPastTheCountBeforeTheLimit()
    {
        CliException error = Assert.Throws<CliException>(
            () => RenderSelection.Resolve(PageRange.Parse("30"), 25, ErrorCodes.PageNotFound, "page"));

        Assert.Equal(ErrorCodes.PageNotFound, error.Code);
    }
}
