using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The viewer service watches a file, renders it through the warm worker and
/// serves every revision as immutable files behind an exact loopback origin.
/// Editing the file reaches an attached viewer as one update naming the parts
/// that changed.
/// </summary>
public sealed class ViewerServiceHttpTests : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(60);

    private readonly TempWorkspace _workspace = new();
    private readonly RenderWorkerSupervisor _worker;
    private readonly ViewerDocuments _documents;
    private readonly ViewerHttpServer _server;
    private readonly HttpClient _client = new();

    public ViewerServiceHttpTests()
    {
        LocalServiceResourceLimits limits = LocalServiceResourceLimits.Resolve();
        _worker = new RenderWorkerSupervisor(StartInfo, TimeSpan.FromMinutes(2));
        _documents = new ViewerDocuments(_worker, ViewerStorage.Create(), limits);
        _server = new ViewerHttpServer(_documents, requestedPort: 0, limits);
        _server.Start();
        _client.Timeout = EventTimeout;
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        _documents.Dispose();
        _worker.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Shutdown_DoesNotDisposeAnActiveRequestLease()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.Mount((context, _) =>
        {
            entered.SetResult();
            try { release.Wait(); context.Response.Close(); }
            finally { completed.SetResult(); }
            return true;
        });
        Task<HttpResponseMessage> request = Get("/");
        try
        {
            Task winner = await Task.WhenAny(entered.Task, request).WaitAsync(EventTimeout);
            Assert.Same(entered.Task, winner);
            _server.Dispose();
        }
        finally { release.Set(); }
        await completed.Task.WaitAsync(EventTimeout);
        try { using HttpResponseMessage response = await request; } catch (HttpRequestException) { }
    }

    [Fact]
    public async Task Document_IsServedAsAPageWithImmutableRevisionFiles()
    {
        LiveDocument document = OpenWorkbook();

        HttpResponseMessage page = await Get($"/d/{document.Id}/");
        string html = await page.Content.ReadAsStringAsync();
        HttpResponseMessage manifest = await Get($"/d/{document.Id}/r/1/view.json");
        JsonNode view = JsonNode.Parse(await manifest.Content.ReadAsStringAsync())!;
        string file = view["parts"]![0]!["file"]!.GetValue<string>();
        HttpResponseMessage part = await Get($"/d/{document.Id}/r/1/{file}");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("definePresenter('cells'", html, StringComparison.Ordinal);
        Assert.Contains("\"product\":\"cells\"", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, part.StatusCode);
        Assert.Contains("immutable", part.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.True((await part.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task Edit_ReachesTheViewerAsOneUpdateNamingTheChangedParts()
    {
        // The sheet view renders one part per sheet, so a digest tells the
        // viewer exactly which sheet an edit touched.
        LiveDocument document = OpenWorkbook(view: "sheets");
        using var events = new EventStream(await OpenEvents(document));
        JsonNode hello = await events.Next("hello");

        Assert.Equal(1, hello["revision"]!.GetValue<int>());
        Succeed(_workspace.Run("cells", "edit", "book.xlsx", "--in-place",
            "--set", "Second!A1=Changed", "--output", "json"));

        JsonNode update = await events.Next("update");
        Assert.Equal(2, update["revision"]!.GetValue<int>());
        string[] changed = update["changed"]!.AsArray().Select(static id => id!.GetValue<string>()).ToArray();
        Assert.Contains("Second", changed);
        Assert.DoesNotContain("First", changed);
        Assert.Equal(2, document.Current!.Number);
    }

    [Fact]
    public async Task Requests_AreRefusedUnlessTheyAreExactLoopbackReads()
    {
        LiveDocument document = OpenWorkbook();

        var foreignHost = new HttpRequestMessage(HttpMethod.Get, Url($"/d/{document.Id}/"));
        foreignHost.Headers.Host = "example.com";
        HttpResponseMessage rejected = await _client.SendAsync(foreignHost);
        HttpResponseMessage posted = await _client.PostAsync(Url($"/d/{document.Id}/"), new StringContent(""));
        HttpResponseMessage unknown = await Get("/d/unknown-document/");
        HttpResponseMessage retired = await Get($"/d/{document.Id}/r/99/view.json");

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, posted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, retired.StatusCode);
    }

    [Fact]
    public void Opening_TheSameFileTheSameWay_ReusesTheOpenDocument()
    {
        LiveDocument first = OpenWorkbook();
        LiveDocument again = _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder);
        LiveDocument other = _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions { Effect = "demo" }, ViewerDocuments.PreviewHolder);

        Assert.Same(first, again);
        Assert.NotSame(first, other);
        Assert.Equal(2, _documents.All.Count);
    }

    [Fact]
    public void Release_ClosesADocumentOnlyWhenItsLastHolderLetsGo()
    {
        LiveDocument preview = OpenWorkbook();
        var firstTab = new object();
        var secondTab = new object();
        Assert.Same(preview, _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions(), firstTab));
        Assert.Same(preview, _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions(), secondTab));
        Assert.Same(preview, _documents.Open(
            _workspace.File("book.xlsx"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder));

        Assert.True(_documents.Release(preview.Id, ViewerDocuments.PreviewHolder));
        Assert.False(_documents.Release(preview.Id, ViewerDocuments.PreviewHolder));
        Assert.True(_documents.Release(preview.Id, firstTab));
        Assert.Same(preview, _documents.Find(preview.Id));

        Assert.True(_documents.Release(preview.Id, secondTab));
        Assert.Null(_documents.Find(preview.Id));
        Assert.Empty(_documents.All);
    }

    [Fact]
    public void Opening_AnUnreadableDocument_FailsWithTheProductError()
    {
        File.WriteAllText(_workspace.File("broken.xlsx"), "not a workbook");

        Aspose.Cli.Sdk.Errors.CliException error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(
            () => _documents.Open(_workspace.File("broken.xlsx"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder));

        Assert.NotEmpty(error.Code.Name);
        Assert.Empty(_documents.All);
    }

    private LiveDocument OpenWorkbook(string? view = null)
    {
        Succeed(_workspace.Run("cells", "create", "book.xlsx", "--sheets", "First,Second", "--output", "json"));
        Succeed(_workspace.Run("cells", "edit", "book.xlsx", "--in-place",
            "--set", "First!A1=Region", "--set", "Second!A1=Raw", "--output", "json"));
        return _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions { View = view }, ViewerDocuments.PreviewHolder);
    }

    private string Url(string path) => $"http://127.0.0.1:{_server.Port}{path}";

    private Task<HttpResponseMessage> Get(string path) => _client.GetAsync(Url(path));

    private async Task<Stream> OpenEvents(LiveDocument document)
    {
        HttpResponseMessage response = await _client.GetAsync(
            Url($"/d/{document.Id}/events"),
            HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStreamAsync();
    }

    private static void Succeed(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr);

    /// <summary>Reads named events off one server-sent event stream.</summary>
    private sealed class EventStream(Stream stream) : IDisposable
    {
        private readonly StreamReader _reader = new(stream);

        public void Dispose() => _reader.Dispose();

        public async Task<JsonNode> Next(string name)
        {
            var deadline = Stopwatch.StartNew();
            string? current = null;
            while (deadline.Elapsed < EventTimeout)
            {
                string? line = await _reader.ReadLineAsync();
                if (line is null)
                {
                    break;
                }
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    current = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal) && current == name)
                {
                    return JsonNode.Parse(line["data:".Length..].Trim())!;
                }
            }
            throw new TimeoutException($"The '{name}' event did not arrive.");
        }
    }

    private ProcessStartInfo StartInfo()
    {
        var start = new ProcessStartInfo(CliRunner.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.Path,
        };
        CliEnvironment.Evaluation(Directory.GetParent(_workspace.ConfigDirectory)!.FullName)
            .Apply(start.Environment);
        return start;
    }
}
