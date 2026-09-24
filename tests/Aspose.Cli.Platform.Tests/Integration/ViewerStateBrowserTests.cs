using System.Text.Json;
using Aspose.Cli.TestKit;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>Exercises the production viewer in Chromium with deterministic revision transport.</summary>
[Category(TestCategory.Browser)]
public sealed class ViewerStateBrowserTests(ITestOutputHelper output)
{
    [Fact]
    public Task LicenseTransitionsKeepExactlyOneEvaluationBadge() =>
        InBrowser("license-transitions", "deck", ["A"], "evaluation", async page =>
        {
            await Expect(page.Locator(".av-badge-evaluation")).ToHaveCountAsync(1);
            await Revise(page, 2, ["A"], license: "licensed");
            await Expect(page.Locator(".av-badge-evaluation")).ToHaveCountAsync(0);
            await Revise(page, 3, ["A"], license: "evaluation");
            await Expect(page.Locator(".av-badge-evaluation")).ToHaveCountAsync(1);
            await Revise(page, 4, ["A"], license: "evaluation");
            await Expect(page.Locator(".av-badge-evaluation")).ToHaveCountAsync(1);
            await Revise(page, 5, ["A"], license: "licensed");
            await Expect(page.Locator(".av-badge-evaluation")).ToHaveCountAsync(0);
        });

    [Theory]
    [InlineData("deck", false)]
    [InlineData("deck", true)]
    [InlineData("tabs", false)]
    [InlineData("tabs", true)]
    [InlineData("pages", false)]
    [InlineData("pages", true)]
    public Task RevisionsKeepSelectionConsistentThroughReorderDeletionAndEmpty(string layout, bool follow) =>
        InBrowser("selection-" + layout + "-" + follow, layout, ["A", "B", "C", "D"], "licensed", async page =>
        {
            await page.Locator(".av-thumb").Nth(2).ClickAsync();
            if (!follow)
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Follow changes", Exact = true }).ClickAsync();
            }
            await AssertSelection(page, layout, "C", 2, 4);

            await Revise(page, 2, ["B", "C", "A", "D"]);
            await AssertSelection(page, layout, "C", 1, 4);
            await Revise(page, 3, ["C", "A", "D"]);
            await AssertSelection(page, layout, "C", 0, 3);

            // Deleting the current part selects its former position in the new revision.
            await Revise(page, 4, ["A", "D"]);
            await AssertSelection(page, layout, "A", 0, 2);
            await page.Locator(".av-thumb").Nth(1).ClickAsync();
            await Revise(page, 5, ["A"]);
            await AssertSelection(page, layout, "A", 0, 1);

            await Revise(page, 6, []);
            await Expect(page.Locator(".av-stage > .av-empty")).ToBeVisibleAsync();
            await Expect(page.Locator(".av-position")).ToHaveTextAsync("");
            await Expect(page.Locator(".av-thumb[aria-current='true']")).ToHaveCountAsync(0);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Next page", Exact = true })).ToBeDisabledAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Previous page", Exact = true })).ToBeDisabledAsync();

            await Revise(page, 7, ["D", "A"]);
            await AssertSelection(page, layout, "D", 0, 2);
            await Expect(page.Locator(".av-stage > .av-empty")).ToBeHiddenAsync();
            await Revise(page, 8, ["D", "A"], changed: ["A"]);
            await AssertSelection(page, layout, follow ? "A" : "D", follow ? 1 : 0, 2);
        });

    [Theory]
    [InlineData("deck")]
    [InlineData("tabs")]
    [InlineData("pages")]
    public Task InitiallyEmptyViewerBecomesNavigable(string layout) =>
        InBrowser("initially-empty-" + layout, layout, [], "licensed", async page =>
        {
            await Expect(page.Locator(".av-stage > .av-empty")).ToBeVisibleAsync();
            await Revise(page, 2, ["A", "B"]);
            await AssertSelection(page, layout, "A", 0, 2);
            await page.GetByRole(AriaRole.Button, new() { Name = "Next page", Exact = true }).ClickAsync();
            await AssertSelection(page, layout, "B", 1, 2);
            string previousZoom = await page.Locator(".av-zoom-label").InnerTextAsync();
            await page.Keyboard.PressAsync("+");
            await Expect(page.Locator(".av-zoom-label")).Not.ToHaveTextAsync(previousZoom);
        });

    [Fact]
    public Task FitWidthUsesOnlyCurrentPageDimensions() =>
        InBrowser("page-size", "pages", ["A"], "licensed", async page =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Fit width", Exact = true }).ClickAsync();
            double before = await page.Locator(".av-part").EvaluateAsync<double>("part => part.getBoundingClientRect().width");
            await Revise(page, 2, ["A"], width: 480);
            double after = await page.Locator(".av-part").EvaluateAsync<double>("part => part.getBoundingClientRect().width");
            Assert.InRange(after, before - 1, before + 1);
        });

    private static async Task AssertSelection(IPage page, string layout, string id, int index, int total)
    {
        await Expect(page.Locator(".av-position")).ToHaveTextAsync($"Page {index + 1} of {total}");
        await Expect(page.Locator(".av-thumb[aria-current='true']")).ToHaveAttributeAsync("title", $"Page {index + 1}: {id}");
        ILocator next = page.GetByRole(AriaRole.Button, new() { Name = "Next page", Exact = true });
        ILocator previous = page.GetByRole(AriaRole.Button, new() { Name = "Previous page", Exact = true });
        if (index == 0) { await Expect(previous).ToBeDisabledAsync(); }
        else { await Expect(previous).ToBeEnabledAsync(); }
        if (index == total - 1) { await Expect(next).ToBeDisabledAsync(); }
        else { await Expect(next).ToBeEnabledAsync(); }

        if (layout == "pages")
        {
            await Expect(page.Locator($".av-part[data-part-id='{id}']")).ToBeInViewportAsync();
        }
        else
        {
            await Expect(page.Locator(".av-slot .av-part")).ToHaveAttributeAsync("data-part-id", id);
        }
        if (layout == "tabs")
        {
            await Expect(page.Locator(".av-tab[aria-selected='true']")).ToHaveTextAsync(id);
        }
    }

    private static async Task Revise(
        IPage page, int revision, string[] parts, string license = "licensed", string[]? changed = null, int width = 960)
    {
        await page.EvaluateAsync("""
            ({revision, manifest, license, changed}) => {
                window.__manifest = manifest;
                window.__events.dispatchEvent(new MessageEvent('update', {
                    data: JSON.stringify({revision, license, changed})
                }));
            }
            """, new { revision, manifest = Manifest(parts, width), license, changed = changed ?? [] });
        await Expect(page.Locator(".av-status-note")).ToContainTextAsync("revision " + revision);
    }

    private static object Manifest(string[] parts, int width = 960) => new
    {
        view = "test",
        totalParts = parts.Length,
        parts = parts.Select(id => new
        {
            id,
            label = id,
            kind = "image",
            file = id + ".png",
            width,
            height = 540,
            digest = "sha256:" + id + "-" + width,
        }),
    };

    private async Task InBrowser(
        string name, string layout, string[] parts, string license, Func<IPage, Task> test)
    {
        string kit = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "src/Aspose.Cli.Host/Viewer/kit.js"));
        string css = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "src/Aspose.Cli.Host/Viewer/kit.css"));
        string live = JsonSerializer.Serialize(new
        {
            live = new { revision = 1, product = "test", file = "synthetic.test", license },
        });
        string manifest = JsonSerializer.Serialize(Manifest(parts));
        string html = $$"""
            <!doctype html><html><head><meta charset="utf-8"><style>{{css}}</style></head><body>
            <script type="application/json" id="aspose-viewer-data">{{live}}</script>
            <script>
            window.__manifest = {{manifest}};
            window.fetch = async () => ({ok: true, json: async () => window.__manifest});
            window.EventSource = class extends EventTarget {
                constructor() { super(); window.__events = this; }
            };
            </script><script>{{kit}}</script><script>
            AsposeViewer.definePresenter('test', {
                kind: 'Test document', glyph: 'T',
                views: { test: {layout: '{{layout}}', noun: 'Page', sidebar: 'thumbnails'} }
            });
            AsposeViewer.start();
            </script></body></html>
            """;
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
            ReducedMotion = ReducedMotion.Reduce,
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.RouteAsync("**/*", route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.Host != "viewer-state.invalid") { return route.AbortAsync(); }
            return route.FulfillAsync(new()
            {
                ContentType = uri.AbsolutePath == "/" ? "text/html" : "image/svg+xml",
                Body = uri.AbsolutePath == "/" ? html
                    : """<svg xmlns="http://www.w3.org/2000/svg" width="960" height="540"><rect width="960" height="540" fill="#e2e8f0"/></svg>""",
            });
        });
        try
        {
            await page.GotoAsync("http://viewer-state.invalid/");
            await Expect(page.Locator(".av-app")).ToBeVisibleAsync();
            await test(page);
            Assert.Empty(errors);
        }
        catch
        {
            string evidence = BrowserApp.EvidenceDirectory(name, output);
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "page.png"), FullPage = true });
            await context.Tracing.StopAsync(new() { Path = Path.Combine(evidence, "trace.zip") });
            throw;
        }
    }
}
