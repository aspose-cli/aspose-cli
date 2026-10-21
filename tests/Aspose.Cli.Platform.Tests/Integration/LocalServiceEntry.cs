namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>Enters a local service the way a browser does, through the address the CLI returns.</summary>
internal static class LocalServiceEntry
{
    /// <summary>
    /// Opens an address once, without following a redirect, and returns the session cookie the
    /// service set, as <c>name=value</c>, or null when it set none. What the answer must be is
    /// for each test to judge.
    /// </summary>
    internal static async Task<string?> Cookie(string url)
    {
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        using HttpResponseMessage response = await client.GetAsync(url);
        return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.First().Split(';')[0]
            : null;
    }

    /// <summary>
    /// A client of the service <paramref name="url"/> names that holds whatever session opening it
    /// earned and sends the service's own Origin, as the App's page does.
    /// </summary>
    internal static async Task<HttpClient> Client(string url, TimeSpan timeout)
    {
        string? cookie = await Cookie(url);
        var client = new HttpClient(new HttpClientHandler { UseCookies = false })
        {
            BaseAddress = new Uri(new Uri(url).GetLeftPart(UriPartial.Authority)),
            Timeout = timeout,
        };
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", cookie);
        }
        client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.GetLeftPart(UriPartial.Authority));
        return client;
    }
}
