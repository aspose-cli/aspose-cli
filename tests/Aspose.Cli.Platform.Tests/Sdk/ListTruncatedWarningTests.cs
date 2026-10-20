using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Results;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class ListTruncatedWarningTests
{
    /// <summary>
    /// A capped list is a result field, not an item of the document: the warning names it in
    /// <c>details.list</c> with the entries it holds and those that exist, and carries no
    /// location, which is a target address only.
    /// </summary>
    [Fact]
    public void ListTruncated_NamesTheListInDetailsAndHasNoLocation()
    {
        Warning warning = EnvelopeParts.ListTruncated("outline", 100, 250, "Read a narrower range.");

        Assert.Equal(WarningCodes.ListTruncated, warning.Code);
        Assert.Null(warning.Location);
        Assert.Equal("outline", warning.Details!["list"]!.GetValue<string>());
        Assert.Equal(100, warning.Details["limit"]!.GetValue<int>());
        Assert.Equal(250, warning.Details["actual"]!.GetValue<int>());
    }
}
