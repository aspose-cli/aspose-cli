using System.Net;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>What the update commands tell a person when the release feed answers with an error status.</summary>
public sealed class ReleaseFeedAnswerTests
{
    private static readonly Uri Feed = new("https://example.test/releases/latest/download/RELEASE-MANIFEST.json");

    [Fact]
    public void NotFound_SaysNoReleaseIsThereInsteadOfBlamingTheNetwork()
    {
        CliException error = HostDiagnostics.ReleaseFeedAnswered(Feed, HttpStatusCode.NotFound);

        Assert.Equal("RELEASE_FEED_UNAVAILABLE", error.Code.Name);
        Assert.Contains("HTTP 404", error.Message, StringComparison.Ordinal);
        Assert.Contains("no release", error.Hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network", error.Hint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Feed.AbsoluteUri, error.Details!["feed"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void OtherStatuses_PointAtTheNetwork(HttpStatusCode status)
    {
        CliException error = HostDiagnostics.ReleaseFeedAnswered(Feed, status);

        Assert.Equal("RELEASE_FEED_UNAVAILABLE", error.Code.Name);
        Assert.Contains($"HTTP {(int)status}", error.Message, StringComparison.Ordinal);
        Assert.Contains("network", error.Hint, StringComparison.OrdinalIgnoreCase);
    }
}
