using Aspose.Cli.Host.LocalServices;
using System.Net;
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
    [InlineData("localhost:4680", 4680, false)]
    [InlineData("[::1]:4680", 4680, false)]
    [InlineData("x@127.0.0.1:4680", 4680, false)]
    [InlineData("127.0.0.1.:4680", 4680, false)]
    [InlineData("127.0.0.1:04680", 4680, false)]
    [InlineData("127.0.0.1", 4680, false)]
    [InlineData("127.0.0.1:4681", 4680, false)]
    [InlineData("localhost", 4680, false)]
    [InlineData("evil.example:4680", 4680, false)]
    [InlineData("*:4680", 4680, false)]
    [InlineData("127.0.0.1", 80, true)]
    [InlineData("127.0.0.1:80", 80, false)]
    public void IsExactLoopbackHost_RequiresTheBoundAddressAndPortExactly(
        string host,
        int port,
        bool expected) =>
        Assert.Equal(
            expected,
            LocalHttpRequestSecurity.IsExactLoopbackHost(host, port));

    [Theory]
    [InlineData(80, "http://127.0.0.1:80/home")]
    [InlineData(80, "http://127.0.0.1/")]
    [InlineData(49152, "http://127.0.0.1:49152/d/0123456789abcdef0123456789abcdef/")]
    public void Entry_AppendsATicketToAnAddressOfThisService(int port, string url) =>
        Assert.Matches(
            "^" + System.Text.RegularExpressions.Regex.Escape(url) + @"\?t=[0-9a-f]{64}$",
            new LocalHttpRequestSecurity(port).Entry(url));

    [Theory]
    [InlineData(80, "http://127.0.0.1:8080/")]
    [InlineData(49152, "http://127.0.0.1:49153/")]
    [InlineData(49152, "https://127.0.0.1:49152/")]
    [InlineData(49152, "http://localhost:49152/")]
    [InlineData(49152, "http://127.0.0.1:49152/?t=0")]
    public void Entry_RefusesAnAddressOfAnotherService(int port, string url) =>
        Assert.Throws<ArgumentException>(() => new LocalHttpRequestSecurity(port).Entry(url));

    [Fact]
    public void FixedEquals_RejectsNullAndNearMatches()
    {
        Assert.False(SecretText.FixedEquals(null, "secret"));
        Assert.False(SecretText.FixedEquals("secreu", "secret"));
        Assert.False(SecretText.FixedEquals("secretx", "secret"));
        Assert.True(SecretText.FixedEquals("secret", "secret"));
    }
}
