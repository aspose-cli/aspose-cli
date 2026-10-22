using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// One viewer service serves everything a person previews: the first preview
/// starts it, later previews join it, and stopping it closes every document.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class ViewerCommandLifecycleTests
{
    [Category(TestCategory.Slow)]
    [Fact]
    public async Task Preview_StartsOneServiceAndJoinsItForEveryDocument()
    {
        using var workspace = new TempWorkspace();
        CreateWorkbook(workspace, "book.xlsx");
        CreateDocument(workspace, "report.docx");
        try
        {
            JsonNode first = Start(workspace, "book.xlsx");
            string id = first["id"]!.GetValue<string>();
            string url = first["url"]!.GetValue<string>();
            string page = Address(url);
            Assert.False(first["reused"]!.GetValue<bool>());
            Assert.Equal("cells", first["product"]!.GetValue<string>());
            Assert.Equal($"/d/{id}/", new Uri(url).AbsolutePath);

            // Status names the same document with a ticket of its own.
            JsonNode discovered = Assert.Single(
                workspace.Run("preview", "status", id, "--output-format", "json").Json()["sessions"]!.AsArray())!;
            string discoveredUrl = discovered["url"]!.GetValue<string>();
            Assert.Equal(page, Address(discoveredUrl));
            Assert.NotEqual(url, discoveredUrl);

            // Each browser enters once through an address the CLI returned; its session serves it after that.
            string firstCookie = await Enter(url);
            string secondCookie = await Enter(discoveredUrl);
            Assert.NotEqual(firstCookie, secondCookie);
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            foreach (string cookie in new[] { firstCookie, secondCookie, firstCookie })
            {
                using HttpResponseMessage shown = await Send(client, HttpMethod.Get, page, cookie);
                Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
                Assert.False(shown.Headers.Contains("Set-Cookie"));
                Assert.Contains("definePresenter('cells'", await shown.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }

            JsonNode again = Start(workspace, "book.xlsx");
            Assert.True(again["reused"]!.GetValue<bool>());
            Assert.Equal(id, again["id"]!.GetValue<string>());
            string againUrl = again["url"]!.GetValue<string>();
            Assert.Equal(page, Address(againUrl));
            Assert.NotEqual(url, againUrl);
            Assert.NotEqual(discoveredUrl, againUrl);
            await Enter(againUrl);
            using (HttpResponseMessage afterReuse = await Send(client, HttpMethod.Get, page, secondCookie))
            {
                Assert.Equal(HttpStatusCode.OK, afterReuse.StatusCode);
            }
            // The viewer answers reads only: nothing can be posted to it.
            using (HttpResponseMessage refused = await Send(client, HttpMethod.Post, page, secondCookie))
            {
                Assert.Equal(HttpStatusCode.MethodNotAllowed, refused.StatusCode);
            }

            JsonNode other = Start(workspace, "report.docx");
            Assert.Equal("words", other["product"]!.GetValue<string>());
            // One service, one process: only the document changes.
            Assert.Equal(first["pid"]!.GetValue<int>(), other["pid"]!.GetValue<int>());
            Assert.NotEqual(page, Address(other["url"]!.GetValue<string>()));
            JsonNode status = workspace.Run("preview", "status", "--output-format", "json").Json();
            Assert.Equal(2, status["sessions"]!.AsArray().Count);

            JsonNode stoppedOne = workspace.Run("preview", "stop", id, "--output-format", "json").Json();
            Assert.Equal(id, Assert.Single(stoppedOne["stopped"]!.AsArray())!.GetValue<string>());
            JsonNode stoppedAll = workspace.Run("preview", "stop", "--output-format", "json").Json();
            Assert.Single(stoppedAll["stopped"]!.AsArray());
            Assert.Empty(stoppedAll["sessions"]!.AsArray());
            Assert.Empty(workspace.Run("preview", "status", "--output-format", "json").Json()["sessions"]!.AsArray());
        }
        finally
        {
            workspace.Run("preview", "stop", "--output-format", "json");
        }
    }

    /// <summary>A service address without its entry ticket.</summary>
    private static string Address(string url)
    {
        int query = url.IndexOf('?', StringComparison.Ordinal);
        Assert.True(query > 0, $"The address {url} carries no entry ticket.");
        return url[..query];
    }

    /// <summary>Opens an entry address as a new browser and returns the session it was admitted with.</summary>
    private static async Task<string> Enter(string url)
    {
        string? cookie = await LocalServiceEntry.Cookie(url);
        Assert.True(cookie is not null, $"The entry address {new Uri(url).AbsolutePath} admitted no browser.");
        return cookie;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string cookie)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }

    [Fact]
    public void PreviewStop_WithoutAnId_ClosesEveryDocumentAndEndsTheService()
    {
        using var workspace = new TempWorkspace();
        CreateWorkbook(workspace, "book.xlsx");
        try
        {
            JsonNode started = Start(workspace, "book.xlsx");
            using Process service = Process.GetProcessById(started["pid"]!.GetValue<int>());

            JsonNode stopped = workspace.Run("preview", "stop", "--output-format", "json").Json();

            Assert.Equal(started["id"]!.GetValue<string>(), Assert.Single(stopped["stopped"]!.AsArray())!.GetValue<string>());
            Assert.Empty(stopped["sessions"]!.AsArray());
            Assert.True(service.WaitForExit(TestDeadline.Hang), "The viewer service kept running after 'preview stop'.");
            Assert.Empty(workspace.Run("preview", "status", "--output-format", "json").Json()["sessions"]!.AsArray());
        }
        finally
        {
            workspace.Run("preview", "stop", "--output-format", "json");
        }
    }

    [Fact]
    public void Preview_LeavesTheCallersDirectoryFreeToMove()
    {
        using var workspace = new TempWorkspace();
        using var documents = new TempDirectory();
        string book = documents.File("book.xlsx");
        workspace.Run("cells", "create", book, "--sheet-names", "Data", "--output-format", "json").Succeeded();
        string moved = workspace.Path + "-moved";
        try
        {
            Start(workspace, book);

            // Neither the service nor its render worker keeps the caller's
            // directory as its own, so the caller can still move it.
            Directory.Move(workspace.Path, moved);
            Directory.Move(moved, workspace.Path);
        }
        finally
        {
            if (Directory.Exists(moved) && !Directory.Exists(workspace.Path))
            {
                Directory.Move(moved, workspace.Path);
            }
            workspace.Run("preview", "stop", "--output-format", "json");
        }
    }

    [Fact]
    public void Preview_ReportsTheProductFailureOfAnUnreadableDocument()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("broken.docx"), "not a document");
        try
        {
            CliResult result = workspace.Run("preview", "open", "broken.docx", "--no-open", "--output-format", "json");

            Assert.NotEqual(0, result.ExitCode);
            JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
            Assert.False(string.IsNullOrWhiteSpace(error["code"]!.GetValue<string>()));
            Assert.DoesNotContain("Unhandled", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        finally
        {
            workspace.Run("preview", "stop", "--output-format", "json");
        }
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Preview_ReportsAWrongPasswordAsTheCommandsDo()
    {
        using var workspace = new TempWorkspace();
        CreateWorkbook(workspace, "plain.xlsx");
        workspace.RunWithEnv(
            new Dictionary<string, string?> { ["PW"] = "right-password" },
            "cells", "convert", "plain.xlsx", "--out", "locked.xlsx", "--encrypt-env", "PW", "--output-format", "json")
            .Succeeded();
        var wrong = new Dictionary<string, string?> { ["PW"] = "wrong-password" };
        try
        {
            CliResult review = workspace.RunWithEnv(
                wrong, "review", "locked.xlsx", "--password-env", "PW", "--output-format", "json");
            CliResult preview = workspace.RunWithEnv(
                wrong, "preview", "open", "locked.xlsx", "--password-env", "PW", "--no-open", "--output-format", "json");

            JsonNode expected = JsonNode.Parse(review.StdErr)!["error"]!;
            JsonNode error = JsonNode.Parse(preview.StdErr)!["error"]!;
            Assert.Equal(review.ExitCode, preview.ExitCode);
            Assert.Equal("PASSWORD_INVALID", error["code"]!.GetValue<string>());
            Assert.Equal(expected["hint"]!.GetValue<string>(), error["hint"]?.GetValue<string>());
            Assert.Equal(workspace.File("locked.xlsx"), error["details"]?["path"]?.GetValue<string>());
        }
        finally
        {
            workspace.Run("preview", "stop", "--output-format", "json");
        }
    }

    private static JsonNode Start(TempWorkspace workspace, string file) =>
        workspace.Run("preview", "open", file, "--no-open", "--output-format", "json").Json();

    private static void CreateWorkbook(TempWorkspace workspace, string file) =>
        workspace.Run("cells", "create", file, "--sheet-names", "Data", "--output-format", "json").Succeeded();

    private static void CreateDocument(TempWorkspace workspace, string file)
    {
        File.WriteAllText(workspace.File("report.md"), "# Report\n\nOne paragraph.\n");
        workspace.Run("words", "create", file, "--from-markdown", "report.md", "--output-format", "json").Succeeded();
    }
}
