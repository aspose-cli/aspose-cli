using System.Net;
using System.Net.Http;
using System.Reflection;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Viewer;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// The <c>Content-Type</c> the viewer serves a revision file with, by its extension, over a real
/// loopback request. Text types carry their charset; a script, a PDF and plain text are not
/// served as what they are, and neither is any type the viewer does not list (D7).
/// </summary>
public sealed class ViewerHttpServerTests
{
    private static readonly MethodInfo Serve = typeof(ViewerHttpServer)
        .GetMethod("Serve", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("part.html", "text/html; charset=utf-8")]
    [InlineData("part.htm", "text/html; charset=utf-8")]
    [InlineData("part.HTML", "text/html; charset=utf-8")]
    [InlineData("part.css", "text/css; charset=utf-8")]
    [InlineData("part.json", "application/json; charset=utf-8")]
    [InlineData("part.svg", "image/svg+xml")]
    [InlineData("part.png", "image/png")]
    [InlineData("part.jpg", "image/jpeg")]
    [InlineData("part.jpeg", "image/jpeg")]
    [InlineData("part.gif", "image/gif")]
    [InlineData("part.webp", "image/webp")]
    [InlineData("part.js", "application/octet-stream")]
    [InlineData("part.pdf", "application/octet-stream")]
    [InlineData("part.txt", "application/octet-stream")]
    [InlineData("part.bin", "application/octet-stream")]
    [InlineData("part", "application/octet-stream")]
    public async Task Serve_GivesEachRevisionFileItsContentType(string file, string contentType)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("view.json"), "{}");
        File.WriteAllText(temp.File(file), "content");
        var revision = new LiveRevision(
            1, "test", "pages", "licensed",
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            ViewBundleManifest.Validate(temp.Path, "view.json", LocalServiceResourceLimits.Resolve()));
        LoopbackHttpListenerBinding binding = LoopbackHttpListenerBinder.Start(0);
        using HttpListener listener = binding.Listener;
        using var client = new HttpClient();

        Task<HttpResponseMessage> request = client.GetAsync($"http://127.0.0.1:{binding.Port}/{file}");
        HttpListenerContext context = await listener.GetContextAsync();
        Serve.Invoke(null, [context.Response, revision, file]);
        using HttpResponseMessage response = await request;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType!.ToString());
        Assert.Equal("content", await response.Content.ReadAsStringAsync());
    }
}
