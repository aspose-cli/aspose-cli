using Aspose.Cli.Host.LocalServices;
using System.Net;
using Aspose.Cli.Sdk.Preview;
using Xunit;

namespace Aspose.Cli.Host.Tests.LocalServices;

public sealed class LocalHttpRequestSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.0.2.10", false)]
    [InlineData("2001:db8::10", false)]
    public void IsLoopbackRemote_RequiresAnActualLoopbackEndpoint(
        string address,
        bool expected)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(address), 1234);

        Assert.Equal(
            expected,
            LocalHttpRequestSecurity.IsLoopbackRemote(endpoint));
    }

    [Fact]
    public void IsLoopbackRemote_RejectsAMissingEndpoint() =>
        Assert.False(LocalHttpRequestSecurity.IsLoopbackRemote(null));

    [Theory]
    [InlineData("127.0.0.1:4680", 4680, true)]
    [InlineData("localhost:4680", 4680, true)]
    [InlineData("[::1]:4680", 4680, true)]
    [InlineData("127.0.0.1:4681", 4680, false)]
    [InlineData("localhost", 4680, false)]
    [InlineData("evil.example:4680", 4680, false)]
    [InlineData("*:4680", 4680, false)]
    public void IsExactLoopbackHost_RequiresTheBoundPort(
        string host,
        int port,
        bool expected) =>
        Assert.Equal(
            expected,
            LocalHttpRequestSecurity.IsExactLoopbackHost(host, port));

    [Fact]
    public void FixedEquals_RejectsNullAndNearMatches()
    {
        Assert.False(SecretText.FixedEquals(null, "secret"));
        Assert.False(SecretText.FixedEquals("secreu", "secret"));
        Assert.False(SecretText.FixedEquals("secretx", "secret"));
        Assert.True(SecretText.FixedEquals("secret", "secret"));
    }
}
