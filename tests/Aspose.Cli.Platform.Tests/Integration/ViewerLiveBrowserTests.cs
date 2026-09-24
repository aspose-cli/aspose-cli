using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.TestKit;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The live viewer is the selling point: a real edit reaches the open page in
/// place. The shell, the reading position and everything the edit did not
/// touch stay as they were, and what changed is marked where it is.
/// </summary>
[Category(TestCategory.Browser)]
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
        _worker = new RenderWorkerSupervisor(() => _workspace.StartInfo(), TimeSpan.FromMinutes(2));
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
            _workspace.Run("words", "create", "report.docx", "--markdown", "report.md", "--output", "json").Succeeded();
            return _documents.Open(_workspace.File("report.docx"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder);
        },
        async (page, document) =>
        {
            await Expect(page.Locator(".av-part[data-part-id='page-1']")).ToBeVisibleAsync();
            await page.EvaluateAsync(
                "() => { document.querySelector('.av-app').dataset.probe = 'shell';"
                + " document.querySelector(\"[data-part-id='page-1']\").dataset.probe = 'page'; }");

            _workspace.RunWithInput(
                """{"ops":[{"op":"set_text","at":{"block":100},"text":"EDITED_PARAGRAPH"}]}""",
                "words", "edit", "report.docx", "--ops", "-", "--in-place", "--output", "json").Succeeded();

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
            _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data,Notes", "--output", "json").Succeeded();
            _workspace.Run("cells", "edit", "book.xlsx", "--in-place",
                "--set", "Data!A1=Region", "--set", "Data!B1=Revenue", "--set", "Data!B2=120", "--output", "json").Succeeded();
            return _documents.Open(_workspace.File("book.xlsx"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder);
        },
        async (page, _) =>
        {
            IFrameLocator grid = page.FrameLocator(".cells-grid-frame");
            await Expect(grid.Locator("td[data-cell='B2']").First).ToContainTextAsync("120");
            await Expect(page.Locator(".av-tabstrip .av-tab").First).ToHaveTextAsync("Data");
            await page.EvaluateAsync("() => { document.querySelector('.cells-grid-frame').dataset.probe = 'grid'; }");
            // The mark is a flash that fades, so what was marked is recorded
            // as it happens rather than looked for afterwards.
            await RecordMarks(grid);

            _workspace.Run("cells", "edit", "book.xlsx", "--in-place",
                "--set", "Data!B2=999", "--output", "json").Succeeded();

            await Expect(grid.Locator("td[data-cell='B2']").First).ToContainTextAsync("999");
            // Replacing the exported content keeps the same frame.
            await Expect(page.Locator(".cells-grid-frame[data-probe='grid']")).ToHaveCountAsync(1);
            await Expect(grid.Locator("td[data-cell='A1']").First).ToContainTextAsync("Region");
            // Only the edited cell is marked, so the mark says where to look.
            Assert.Equal(["B2"], await Marks(grid));

            // A value written where the sheet had nothing grows the grid. The
            // cells the growth adds are empty, and an empty cell is not news.
            // The first mark is given time to fade so the next one stands alone.
            await Expect(grid.Locator(".aspose-cell-changed")).ToHaveCountAsync(0, new() { Timeout = 10_000 });
            await RecordMarks(grid);
            _workspace.Run("cells", "edit", "book.xlsx", "--in-place",
                "--set", "Data!D4=Later", "--output", "json").Succeeded();

            await Expect(grid.Locator("td[data-cell='D4']").First).ToContainTextAsync("Later");
            Assert.Equal(["D4"], await Marks(grid));

            await RecordMarks(grid);
            _workspace.RunWithInput(
                """{"ops":[{"op":"insert_rows","sheet":"Data","at":2},{"op":"set_values","sheet":"Data","range":"A2","values":[["Inserted"]]}]}""",
                "cells", "edit", "book.xlsx", "--ops", "-", "--in-place", "--output", "json").Succeeded();

            await Expect(grid.Locator("td[data-cell='A2']").First).ToHaveTextAsync("Inserted");
            await Expect(grid.Locator("td[data-cell='B3']").First).ToContainTextAsync("999");
            await Expect(grid.Locator("td[data-cell='D5']").First).ToContainTextAsync("Later");
            Assert.Equal(["A2"], await Marks(grid));
        });

    [Fact]
    public Task Cells_OnlyTheNewestCompleteRevisionCommitsAndFailedLoadsCanRetry() =>
        InControlledGrid("cells-live-loading", async page =>
        {
            IFrameLocator grid = page.FrameLocator(".cells-grid-frame");
            await ReplyManifest(page, 1, ok: false);
            await page.GetByRole(AriaRole.Button, new() { Name = "Retry preview" }).ClickAsync();
            await ReplyManifest(page, 1);
            await Expect(grid.Locator("td[data-cell='B2']").First).ToHaveTextAsync("1");

            // A slow manifest may not roll the viewer back after a newer one.
            await Announce(page, 2);
            await Announce(page, 3);
            await ReplyManifest(page, 3);
            await Reply(page, "p/revision-3", GridHtml(3));
            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 3");
            await ReplyManifest(page, 2);
            await Expect(grid.Locator("td[data-cell='B2']").First).ToHaveTextAsync("3");
            Assert.False(await page.EvaluateAsync<bool>("() => window.__requested.includes('p/revision-2')"));

            // Preparing a product snapshot must obey the same ordering rule.
            await Announce(page, 4);
            await ReplyManifest(page, 4);
            await WaitForRequest(page, "p/revision-4");
            await Announce(page, 5);
            await ReplyManifest(page, 5);
            await Reply(page, "p/revision-5", GridHtml(5));
            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 5");
            await Reply(page, "p/revision-4", GridHtml(4));
            await Expect(grid.Locator("td[data-cell='B2']").First).ToHaveTextAsync("5");

            await Announce(page, 6);
            await ReplyManifest(page, 6, ok: false);
            await Expect(page.Locator(".av-banner")).ToContainTextAsync("PREVIEW_LOAD_FAILED");
            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 5");
            await page.GetByRole(AriaRole.Button, new() { Name = "Retry preview" }).ClickAsync();
            await ReplyManifest(page, 6);
            await Reply(page, "p/revision-6", "unavailable", ok: false);
            await Expect(page.Locator(".av-banner")).ToBeVisibleAsync();
            await Expect(grid.Locator("td[data-cell='B2']").First).ToHaveTextAsync("5");
            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 5");

            // Reconnection to the same revision retries the failed artifact.
            await Announce(page, 6, "hello");
            await ReplyManifest(page, 6);
            await Reply(page, "p/revision-6", GridHtml(6));
            await Expect(grid.Locator("td[data-cell='B2']").First).ToHaveTextAsync("6");
            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 6");
            await Expect(page.Locator(".av-banner")).ToBeHiddenAsync();
        });

    [Fact]
    public Task Cells_ACompleteSnapshotUpdatesStylesAndGeometryWithoutLosingReadingState() =>
        InControlledGrid("cells-live-snapshot", async page =>
        {
            await ReplyManifest(page, 1);
            IFrameLocator grid = page.FrameLocator(".cells-grid-frame");
            await Expect(grid.Locator("td[data-cell='B2']").First).ToHaveTextAsync("1");
            await page.GetByRole(AriaRole.Tab, new() { Name = "Notes", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Zoom in (+)", Exact = true }).ClickAsync();
            await page.EvaluateAsync("() => document.querySelector('.cells-grid-frame').dataset.probe = 'kept'");
            await grid.Locator("body").EvaluateAsync("() => window.scrollTo(180, 320)");
            double[] scroll = await grid.Locator("body").EvaluateAsync<double[]>("() => [scrollX, scrollY]");
            Assert.True(scroll[0] > 0 && scroll[1] > 0);

            await RecordMarks(grid);
            await Announce(page, 2);
            await ReplyManifest(page, 2);
            await Reply(page, "p/revision-2", GridHtml(2, restyled: true, addedSheet: true));

            await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision 2");
            await Expect(grid.Locator("[sheetname='New']")).ToHaveAttributeAsync("class", "aspose-cell-changed");
            Assert.Equal(["B2"], await Marks(grid));
            await Expect(page.Locator(".cells-grid-frame[data-probe='kept']")).ToHaveCountAsync(1);
            await Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Notes", Exact = true }))
                .ToHaveAttributeAsync("aria-selected", "true");
            await Expect(grid.Locator("body")).ToHaveAttributeAsync("data-snapshot", "2");
            await Expect(grid.Locator("[sheetname='Notes'] td[data-cell='B2']")).ToHaveCSSAsync("color", "rgb(180, 20, 30)");
            await Expect(grid.Locator("[sheetname='Notes'] tr").Last).ToHaveCSSAsync("height", "90px");
            await Expect(grid.Locator("[sheetname='Notes'] col").First).ToHaveCSSAsync("width", "240px");
            Assert.Equal("1.1", await grid.Locator("html").EvaluateAsync<string>("html => html.style.zoom"));
            Assert.Equal(scroll, await grid.Locator("body").EvaluateAsync<double[]>("() => [scrollX, scrollY]"));
        });

    private Task InControlledGrid(string name, Func<IPage, Task> test) =>
        InBrowser(name, () =>
        {
            File.WriteAllText(_workspace.File("controlled.csv"), "Region,Revenue\nEast,120\n");
            return _documents.Open(_workspace.File("controlled.csv"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder);
        }, (page, _) => test(page), async page =>
        {
            // The production page and presenter run in Chromium. Only transport
            // completion is controlled, so failures and response ordering are deterministic.
            await page.AddInitScriptAsync("""
                window.__requests = Object.create(null);
                window.__requested = [];
                window.fetch = url => {
                    window.__requested.push(url);
                    return new Promise(resolve => {
                        (window.__requests[url] ||= []).push(resolve);
                    });
                };
                window.EventSource = class extends EventTarget {
                    constructor() { super(); window.__events = this; }
                };
                """);
            await page.RouteAsync("**/p/*", route => route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body = GridHtml(1),
            }));
        });

    private static Task Announce(IPage page, int revision, string name = "update") =>
        page.EvaluateAsync("""
            ({revision, name}) => window.__events.dispatchEvent(
                new MessageEvent(name, {data: JSON.stringify({revision, changed: ['workbook']})}))
            """, new { revision, name });

    private static Task WaitForRequest(IPage page, string path) =>
        page.WaitForFunctionAsync("path => window.__requests[path]?.length > 0", path);

    private static async Task Reply(IPage page, string path, object content, bool ok = true)
    {
        await WaitForRequest(page, path);
        await page.EvaluateAsync("""
            ({path, content, ok}) => window.__requests[path].shift()({
                ok, json: () => Promise.resolve(content), text: () => Promise.resolve(content)
            })
            """, new { path, content, ok });
    }

    private static Task ReplyManifest(IPage page, int revision, bool ok = true) =>
        Reply(page, $"r/{revision}/view.json", new
        {
            view = "workbook",
            totalParts = 1,
            parts = new[]
            {
                new
                {
                    id = "workbook", label = "Workbook", kind = "html", file = "workbook.html",
                    digest = $"sha256:revision-{revision}",
                },
            },
        }, ok);

    private static string GridHtml(int revision, bool restyled = false, bool addedSheet = false)
    {
        string Sheet(string name) => $$"""
            <div sheetname="{{name}}">
            <table><colgroup><col style="width:{{(restyled ? 240 : 120)}}px"><col></colgroup>
            <tbody><tr><td data-cell="A1">Region</td><td data-cell="B1">Revenue</td></tr>
            <tr style="height:{{(restyled ? 90 : 40)}}px"><td data-cell="A2">East</td><td data-cell="B2" class="value">{{revision}}</td></tr></tbody>
            </table><div style="width:2400px;height:1800px"></div></div>
            """;
        return $$"""
            <!doctype html><html><head><meta name="aspose-active-sheet" content="Data">
            <style>.value{color:{{(restyled ? "rgb(180,20,30)" : "rgb(20,30,40)")}}}td{padding:0}</style>
            </head><body data-snapshot="{{revision}}">
            {{Sheet("Data")}}{{Sheet("Notes")}}{{(addedSheet ? Sheet("New") : string.Empty)}}
            </body></html>
            """;
    }

    /// <summary>Starts recording which cells the grid marks as changed.</summary>
    private static Task RecordMarks(IFrameLocator grid) =>
        grid.Locator("body").EvaluateAsync(@"body => {
            window.__marks = [];
            if (window.__observer) { window.__observer.disconnect(); }
            window.__observer = new MutationObserver(records => records.forEach(record => {
                const cell = record.target;
                if (cell.matches('td[data-cell].aspose-cell-changed')) {
                    window.__marks.push(cell.getAttribute('data-cell'));
                }
            }));
            window.__observer.observe(body, { subtree: true, attributes: true, attributeFilter: ['class'] });
        }");

    private static async Task<string[]> Marks(IFrameLocator grid) =>
        await grid.Locator("body").EvaluateAsync<string[]>(
            "() => Array.from(new Set(window.__marks || [])).sort()");

    [Fact]
    public Task Slides_AFailedRenderKeepsTheDeckAndSaysWhy() =>
        InBrowser("slides-live", () =>
        {
            File.WriteAllText(_workspace.File("deck.md"), "# First slide\n\n- One\n\n# Second slide\n\n- Two\n");
            _workspace.Run("slides", "create", "deck.pptx", "--markdown", "deck.md", "--output", "json").Succeeded();
            return _documents.Open(_workspace.File("deck.pptx"), new LiveDocumentOptions(), ViewerDocuments.PreviewHolder);
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
        Func<IPage, LiveDocument, Task> test,
        Func<IPage, Task>? configure = null)
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
        if (configure is not null) { await configure(page); }
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
        string evidence = BrowserApp.EvidenceDirectory(name, _output);
        try
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "page.png"), FullPage = true });
            File.WriteAllText(Path.Combine(evidence, "page.html"), await page.ContentAsync());
        }
        catch (Exception exception) { _output.WriteLine("Evidence collection: " + exception.GetType().Name); }
    }
}
