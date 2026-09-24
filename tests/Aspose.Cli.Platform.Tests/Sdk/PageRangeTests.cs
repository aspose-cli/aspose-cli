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
        CliException error = Assert.Throws<CliException>(() => PageRange.Parse(text).Resolve(
            available,
            static (range, count) => new CliException(ErrorCodes.UsageError, $"{range.Text} of {count}")));

        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Equal($"{PageRange.Parse(text).Text} of {available}", error.Message);
    }

    [Fact]
    public void Resolve_DefaultsToPageNotFoundAndNamesAnEmptyDocument()
    {
        CliException past = Assert.Throws<CliException>(() => PageRange.Parse("2-3").Resolve(2));
        CliException none = Assert.Throws<CliException>(() => PageRange.Parse("1").Resolve(0));

        Assert.Equal(ErrorCodes.PageNotFound, past.Code);
        Assert.Equal(2, past.Details!["available"]!.GetValue<int>());
        Assert.Equal(ErrorCodes.PageNotFound, none.Code);
        Assert.Contains("no pages", none.Hint, StringComparison.Ordinal);
    }
}
