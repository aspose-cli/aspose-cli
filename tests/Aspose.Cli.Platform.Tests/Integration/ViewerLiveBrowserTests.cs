using System.Diagnostics;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.TestKit;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The live viewer is the selling point: a real edit reaches the open page in
/// place. The shell, the reading position and everything the edit did not
/// touch stay as they were, and what changed is marked where it is.
/// </summary>
public sealed class ViewerLiveBrowserTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TempWorkspace _workspace = new();
    private readonly RenderWorkerSupervisor _worker;
    private readonly ViewerDocuments _documents;
    private readonly ViewerHttpServer _server;

    public ViewerLiveBrowserTests(ITestOutputHelper output)
    {
        _output = output;
        LocalServiceResourceLimits limits = LocalServiceResourceLimits.Resolve();
        _worker = new RenderWorkerSupervisor(StartInfo, TimeSpan.FromMinutes(2));
        _documents = new ViewerDocuments(_worker, ViewerStorage.Create(), limits);
        _server = new ViewerHttpServer(_documents, requestedPort: 0, limits);
        _server.Start();
    }

    public void Dispose()
    {
        _server.Dispose();
        _documents.Dispose();
        _worker.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public Task Words_AnEditLandsOnItsPageWithoutDisturbingTheRest() =>
        InBrowser("words-live", () =>
        {
            File.WriteAllText(_workspace.File("report.md"), string.Join("\n\n",
                Enumerable.Range(1, 120).Select(index => $"Paragraph {index} of the report.")));
            Succeed(_workspace.Run("words", "create", "report.docx", "--markdown", "report.md", "--output", "json"));
            return _documents.Open(_workspace.File("report.docx"), new LiveDocumentOptions());
        },
        async (page, document) =>
        {
            await Expect(page.Locator(".av-part[data-part-id='page-1']")).ToBeVisibleAsync();
            await page.EvaluateAsync(
                "() => { document.querySelector('.av-app').dataset.probe = 'shell';"
                + " document.querySelector(\"[data-part-id='page-1']\").dataset.probe = 'page'; }");

            Succeed(_workspace.RunWithInput(
                """{"ops":[{"op":"set_text","at":{"block":100},"text":"EDITED_PARAGRAPH"}]}""",
                "words", "edit", "report.docx", "--ops", "-", "--in-place", "--output", "json"));

            await Expect(page.Locator(".av-part[data-changed='true']")).ToHaveCountAsync(1);
            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 2");
            // The shell and the untouched page are the same elements as before.
            await Expect(page.Locator(".av-app[data-probe='shell']")).ToHaveCountAsync(1);
            await Expect(page.Locator("[data-part-id='page-1'][data-probe='page']")).ToHaveCountAsync(1);
            Assert.Equal(2, document.Current!.Number);
        });

    [Fact]
    public Task Cells_AnEditLandsOnTheCellInTheProductGrid() =>
        InBrowser("cells-live", () =>
        {
            Succeed(_workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data,Notes", "--output", "json"));
            Succeed(_workspace.Run("cells", "edit", "book.xlsx", "--in-place",
                "--set", "Data!A1=Region", "--set", "Data!B1=Revenue", "--set", "Data!B2=120", "--output", "json"));
            return _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions());
        },
        async (page, _) =>
        {
            IFrameLocator grid = page.FrameLocator(".cells-grid-frame");
            await Expect(grid.Locator("td[data-cell='B2']").First).ToContainTextAsync("120");
            await Expect(page.Locator(".av-tabstrip .av-tab").First).ToHaveTextAsync("Data");
            await page.EvaluateAsync("() => { document.querySelector('.cells-grid-frame').dataset.probe = 'grid'; }");

            Succeed(_workspace.Run("cells", "edit", "book.xlsx", "--in-place",
                "--set", "Data!B2=999", "--output", "json"));

            await Expect(grid.Locator("td[data-cell='B2']").First).ToContainTextAsync("999");
            // The grid was patched, not reloaded: the frame is the same element.
            await Expect(page.Locator(".cells-grid-frame[data-probe='grid']")).ToHaveCountAsync(1);
            await Expect(grid.Locator("td[data-cell='A1']").First).ToContainTextAsync("Region");
        });

    [Fact]
    public Task Slides_AFailedRenderKeepsTheDeckAndSaysWhy() =>
        InBrowser("slides-live", () =>
        {
            File.WriteAllText(_workspace.File("deck.md"), "# First slide\n\n- One\n\n# Second slide\n\n- Two\n");
            Succeed(_workspace.Run("slides", "create", "deck.pptx", "--markdown", "deck.md", "--output", "json"));
            return _documents.Open(_workspace.File("deck.pptx"), new LiveDocumentOptions());
        },
        async (page, document) =>
        {
            await Expect(page.Locator(".av-stage .av-part")).ToBeVisibleAsync();
            await Expect(page.Locator(".av-live")).ToHaveAttributeAsync("data-state", "live");

            File.WriteAllText(_workspace.File("deck.pptx"), "not a presentation");
            document.Refresh();

            await Expect(page.Locator(".av-banner")).ToBeVisibleAsync();
            await Expect(page.Locator(".av-stage .av-part")).ToBeVisibleAsync();
        });

    private async Task InBrowser(
        string name,
        Func<LiveDocument> arrange,
        Func<IPage, LiveDocument, Task> test)
    {
        LiveDocument document = arrange();
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
        });
        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        var failures = new List<string>();
        page.PageError += (_, error) => failures.Add("Script error: " + error);
        await page.GotoAsync(_server.Url(document.Id));
        try
        {
            await test(page, document);
            Assert.Empty(failures);
        }
        catch
        {
            await CaptureEvidence(name, page);
            throw;
        }
    }

    private async Task CaptureEvidence(string name, IPage page)
    {
        string root = Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "aspose-cli-browser-evidence");
        string evidence = Path.Combine(root, "browser", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        _output.WriteLine("Browser evidence: " + evidence);
        try
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "page.png"), FullPage = true });
            File.WriteAllText(Path.Combine(evidence, "page.html"), await page.ContentAsync());
        }
        catch (Exception exception) { _output.WriteLine("Evidence collection: " + exception.GetType().Name); }
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

    private static void Succeed(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr);
}
