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
            Assert.False(first["reused"]!.GetValue<bool>());
            Assert.Equal("cells", first["product"]!.GetValue<string>());
            Assert.Equal($"/d/{id}/", new Uri(url).AbsolutePath);

            // Every client reads the same address; the viewer sets no cookie to tell them apart.
            using var firstClient = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            using var secondClient = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            foreach (HttpClient client in new[] { firstClient, secondClient, firstClient })
            {
                using HttpResponseMessage page = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                Assert.False(page.Headers.Contains("Set-Cookie"));
                Assert.Contains("definePresenter('cells'", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            }

            JsonNode discovered = Assert.Single(
                workspace.Run("preview", "status", id, "--output", "json").Json()["sessions"]!.AsArray())!;
            Assert.Equal(url, discovered["url"]!.GetValue<string>());

            JsonNode again = Start(workspace, "book.xlsx");
            Assert.True(again["reused"]!.GetValue<bool>());
            Assert.Equal(id, again["id"]!.GetValue<string>());
            Assert.Equal(url, again["url"]!.GetValue<string>());
            using (HttpResponseMessage afterReuse = await secondClient.GetAsync(url))
            {
                Assert.Equal(HttpStatusCode.OK, afterReuse.StatusCode);
            }
            // The viewer answers reads only: nothing can be posted to it.
            using (HttpResponseMessage refused = await secondClient.PostAsync(url, content: null))
            {
                Assert.Equal(HttpStatusCode.MethodNotAllowed, refused.StatusCode);
            }

            JsonNode other = Start(workspace, "report.docx");
            Assert.Equal("words", other["product"]!.GetValue<string>());
            // One service, one process: only the document changes.
            Assert.Equal(first["pid"]!.GetValue<int>(), other["pid"]!.GetValue<int>());
            Assert.NotEqual(url, other["url"]!.GetValue<string>());
            JsonNode status = workspace.Run("preview", "status", "--output", "json").Json();
            Assert.Equal(2, status["sessions"]!.AsArray().Count);

            JsonNode stoppedOne = workspace.Run("preview", "stop", id, "--output", "json").Json();
            Assert.Equal(id, Assert.Single(stoppedOne["stopped"]!.AsArray())!.GetValue<string>());
            JsonNode stoppedAll = workspace.Run("preview", "stop", "--all", "--output", "json").Json();
            Assert.Single(stoppedAll["stopped"]!.AsArray());
            Assert.Empty(stoppedAll["sessions"]!.AsArray());
            Assert.Empty(workspace.Run("preview", "status", "--output", "json").Json()["sessions"]!.AsArray());
        }
        finally
        {
            workspace.Run("preview", "stop", "--all", "--output", "json");
        }
    }

    [Fact]
    public void Preview_LeavesTheCallersDirectoryFreeToMove()
    {
        using var workspace = new TempWorkspace();
        using var documents = new TempDirectory();
        string book = documents.File("book.xlsx");
        workspace.Run("cells", "create", book, "--sheets", "Data", "--output", "json").Succeeded();
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
            workspace.Run("preview", "stop", "--all", "--output", "json");
        }
    }

    [Fact]
    public void Preview_ReportsTheProductFailureOfAnUnreadableDocument()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("broken.docx"), "not a document");
        try
        {
            CliResult result = workspace.Run("preview", "broken.docx", "--output", "json");

            Assert.NotEqual(0, result.ExitCode);
            JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
            Assert.False(string.IsNullOrWhiteSpace(error["code"]!.GetValue<string>()));
            Assert.DoesNotContain("Unhandled", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        finally
        {
            workspace.Run("preview", "stop", "--all", "--output", "json");
        }
    }

    private static JsonNode Start(TempWorkspace workspace, string file) =>
        workspace.Run("preview", file, "--output", "json").Json();

    private static void CreateWorkbook(TempWorkspace workspace, string file) =>
        workspace.Run("cells", "create", file, "--sheets", "Data", "--output", "json").Succeeded();

    private static void CreateDocument(TempWorkspace workspace, string file)
    {
        File.WriteAllText(workspace.File("report.md"), "# Report\n\nOne paragraph.\n");
        workspace.Run("words", "create", file, "--markdown", "report.md", "--output", "json").Succeeded();
    }
}
