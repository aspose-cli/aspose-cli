using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

[CollectionDefinition("App mutation isolation", DisableParallelization = true)]
public sealed class AppMutationIsolationCollection;

/// <summary>
/// The App answers on the viewer service's origin, so its mutations share one
/// listener with the documents it shows. A request that holds its body open
/// may not stall the pages a person is looking at, and the temporary copies an
/// upload owns must outlive the request that made them.
/// </summary>
[Collection("App mutation isolation")]
public sealed class AppMutationTests
{
    [Fact]
    public async Task SlowRequestBodyDoesNotBlockStatusOrClearAndKeepsItsUploadAlive()
    {
        using var app = new RunningApp();
        await app.Upload("upload.csv", "Heading,Value\nUPLOADED,42\n");
        string uploaded = Assert.Single(Directory.GetFiles(Path.Combine(app.SessionRoot, "uploads", "files")));
        using TcpClient client = await app.BeginSlowRequestBody();
        try
        {
            Assert.Equal("upload.csv", (await app.Status())["file"]!.GetValue<string>());
            await app.Post("/api/local-data/clear");
            // The temporary copy goes; the file opened from disk stays open.
            JsonNode cleared = await app.Status();
            Assert.Equal("original.csv", cleared["file"]!.GetValue<string>());
            Assert.DoesNotContain(
                cleared["documents"]!.AsArray(),
                document => document!["uploadedCopy"]!.GetValue<bool>());
        }
        finally { client.Dispose(); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (File.Exists(uploaded)) { await Task.Delay(20, deadline.Token); }
        Assert.True(File.Exists(app.Original));
    }

    [Fact]
    public async Task CleanupPreservesUnknownFilesAndAnExternalReplacementOfAnOwnedUpload()
    {
        using var app = new RunningApp();
        await app.Upload("upload.csv", "Heading,Value\nUPLOADED,42\n");
        string directory = Path.Combine(app.SessionRoot, "uploads", "files");
        string uploaded = Assert.Single(Directory.GetFiles(directory));
        string replacement = Path.Combine(directory, "external.csv");
        File.WriteAllText(replacement, "EXTERNAL");
        File.Replace(replacement, uploaded, null);
        string unknown = Path.Combine(directory, "unowned.txt");
        File.WriteAllText(unknown, "UNOWNED");

        await app.Post("/api/local-data/clear");

        Assert.Equal("EXTERNAL", File.ReadAllText(uploaded));
        Assert.Equal("UNOWNED", File.ReadAllText(unknown));
        Assert.True(File.Exists(app.Original));
    }

    [Fact]
    public async Task AnAbandonedRequestLeavesTheAppAnsweringTheNextOne()
    {
        using var app = new RunningApp();
        using (TcpClient abandoned = await app.BeginSlowRequestBody()) { }

        await app.Post("/api/recent/clear");

        Assert.Equal("original.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    /// <summary>
    /// One real service with the App mounted on it, reached exactly as the
    /// browser reaches it: same origin, same CSRF token, same routes.
    /// </summary>
    private sealed class RunningApp : IDisposable
    {
        private readonly TempWorkspace _workspace = new();
        private readonly HttpClient _client;

        internal RunningApp()
        {
            Original = _workspace.File("original.csv");
            File.WriteAllText(Original, "Heading,Value\nORIGINAL,1\n");
            // Session directories are named <pid>-<random> in the user's shared temporary root, so a
            // directory an earlier process with the same id left there is told apart by existing
            // before this App started.
            string sessions = PrivateUserStorage.EnsureDirectory(Path.Combine(PrivateUserStorage.TemporaryRoot(), "app"));
            var existing = Directory.GetDirectories(sessions).ToHashSet(StringComparer.OrdinalIgnoreCase);
            CliResult started = _workspace.Run("app", Original, "--no-open", "--output", "json");
            Assert.True(started.ExitCode == 0, started.StdErr);
            JsonNode result = JsonNode.Parse(started.StdOut)!;
            Url = new Uri(result["url"]!.GetValue<string>());
            int pid = result["pid"]!.GetValue<int>();
            SessionRoot = Assert.Single(
                Directory.GetDirectories(sessions, $"{pid}-*"),
                directory => !existing.Contains(directory));
            _client = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                BaseAddress = new Uri(Url.GetLeftPart(UriPartial.Authority)),
                Timeout = TimeSpan.FromSeconds(30),
            };
            string shell = _client.GetStringAsync("/").GetAwaiter().GetResult();
            string csrf = System.Text.RegularExpressions.Regex.Match(
                shell, @"name=""aspose-csrf"" content=""([^""]+)""").Groups[1].Value;
            Assert.NotEmpty(csrf);
            _client.DefaultRequestHeaders.Add("Origin", _client.BaseAddress.GetLeftPart(UriPartial.Authority));
            _client.DefaultRequestHeaders.Add(LocalHttpRequestSecurity.CsrfHeader, csrf);
            Csrf = csrf;
        }

        internal string Original { get; }

        internal string SessionRoot { get; }

        internal Uri Url { get; }

        internal string Csrf { get; }

        internal async Task<JsonNode> Status() =>
            JsonNode.Parse(await _client.GetStringAsync("/api/status").WaitAsync(TimeSpan.FromSeconds(5)))!;

        internal async Task Post(string path)
        {
            HttpResponseMessage response = await _client
                .PostAsync(path, new StringContent(string.Empty))
                .WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        internal async Task Upload(string name, string content)
        {
            var body = new StringContent(content);
            body.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/files/upload") { Content = body };
            request.Headers.Add("X-File-Name", name);
            HttpResponseMessage response = await _client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>Opens a mutation whose body never arrives, and leaves it open.</summary>
        internal async Task<TcpClient> BeginSlowRequestBody()
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync("127.0.0.1", Url.Port);
                string request = $"POST /api/preferences HTTP/1.1\r\nHost: 127.0.0.1:{Url.Port}\r\n"
                    + $"Origin: http://127.0.0.1:{Url.Port}\r\n{LocalHttpRequestSecurity.CsrfHeader}: {Csrf}\r\n"
                    + "Content-Type: application/json\r\nContent-Length: 1\r\n"
                    + "Expect: 100-continue\r\nConnection: close\r\n\r\n";
                await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(request));
                using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string? line = await reader.ReadLineAsync(timeout.Token);
                Assert.Contains("100 Continue", line, StringComparison.OrdinalIgnoreCase);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
                return client;
            }
            catch { client.Dispose(); throw; }
        }

        public void Dispose()
        {
            try
            {
                _workspace.Run("app", "stop", "--output", "json");
                _client.Dispose();
                // The App keeps files it does not own, which some tests plant in its session; a
                // directory still held by the exiting App is left, as the lookup above tolerates.
                try { Directory.Delete(SessionRoot, recursive: true); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
            finally { _workspace.Dispose(); }
        }
    }
}
