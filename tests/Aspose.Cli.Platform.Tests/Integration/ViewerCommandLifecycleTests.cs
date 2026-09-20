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
    [Fact]
    public void Preview_StartsOneServiceAndJoinsItForEveryDocument()
    {
        using var workspace = new TempWorkspace();
        CreateWorkbook(workspace, "book.xlsx");
        CreateDocument(workspace, "report.docx");
        try
        {
            JsonNode first = Start(workspace, "book.xlsx");
            JsonNode again = Start(workspace, "book.xlsx");
            JsonNode other = Start(workspace, "report.docx");

            Assert.False(first["reused"]!.GetValue<bool>());
            Assert.True(again["reused"]!.GetValue<bool>());
            Assert.Equal(first["id"]!.GetValue<string>(), again["id"]!.GetValue<string>());
            Assert.Equal("cells", first["product"]!.GetValue<string>());
            Assert.Equal("words", other["product"]!.GetValue<string>());
            // One service, one process: only the document changes.
            Assert.Equal(first["pid"]!.GetValue<int>(), other["pid"]!.GetValue<int>());
            Assert.NotEqual(first["url"]!.GetValue<string>(), other["url"]!.GetValue<string>());

            JsonNode status = Json(Succeed(workspace.Run("preview", "status", "--output", "json")));
            Assert.Equal(2, status["sessions"]!.AsArray().Count);
        }
        finally
        {
            workspace.Run("preview", "stop", "--all", "--output", "json");
        }
    }

    [Fact]
    public async Task Preview_ServesTheDocumentPageAndStopEndsTheService()
    {
        using var workspace = new TempWorkspace();
        CreateWorkbook(workspace, "book.xlsx");
        try
        {
            JsonNode started = Start(workspace, "book.xlsx");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            HttpResponseMessage page = await client.GetAsync(started["url"]!.GetValue<string>());
            string html = await page.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("definePresenter('cells'", html, StringComparison.Ordinal);

            JsonNode stopped = Json(Succeed(workspace.Run("preview", "stop", "--all", "--output", "json")));
            Assert.Single(stopped["stopped"]!.AsArray());
            Assert.Empty(stopped["sessions"]!.AsArray());
            JsonNode status = Json(Succeed(workspace.Run("preview", "status", "--output", "json")));
            Assert.Empty(status["sessions"]!.AsArray());
        }
        finally
        {
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
        Json(Succeed(workspace.Run("preview", file, "--output", "json")));

    private static void CreateWorkbook(TempWorkspace workspace, string file) =>
        Succeed(workspace.Run("cells", "create", file, "--sheets", "Data", "--output", "json"));

    private static void CreateDocument(TempWorkspace workspace, string file)
    {
        File.WriteAllText(workspace.File("report.md"), "# Report\n\nOne paragraph.\n");
        Succeed(workspace.Run("words", "create", file, "--markdown", "report.md", "--output", "json"));
    }

    private static JsonNode Json(CliResult result) => JsonNode.Parse(result.StdOut)!;

    private static CliResult Succeed(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return result;
    }
}
