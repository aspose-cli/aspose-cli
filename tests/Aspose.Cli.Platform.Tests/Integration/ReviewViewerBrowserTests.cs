using System.Text.Json.Nodes;
using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The review page is the viewer people open from disk: every product
/// presents its evidence with its own navigation, entirely offline and
/// without script errors.
/// </summary>
[Category(TestCategory.Browser)]
public sealed class ReviewViewerBrowserTests(ITestOutputHelper output)
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    [Fact]
    public Task Words_OutlineTakesTheReaderToTheHeading() =>
        ReviewInBrowser("words-outline", "report.docx", CreateReport, async (page, view) =>
        {
            int headingPage = PartOf(view, static element =>
                element["kind"]!.GetValue<string>() == "heading"
                && element["label"]!.GetValue<string>() == "Chapter 4 results");
            Assert.True(headingPage > 0, "The last chapter must start beyond the first page.");

            await page.Locator(".av-outline-item", new() { HasText = "Chapter 4 results" }).ClickAsync();

            await Expect(page.Locator(".av-outline-item[aria-current='true']")).ToHaveTextAsync("Chapter 4 results");
            await Expect(page.Locator(".av-position")).ToHaveTextAsync(Position("Page", headingPage, view));
            string partId = view["parts"]![headingPage]!["id"]!.GetValue<string>();
            await Expect(page.Locator($".av-part[data-part-id='{partId}'] .av-spotlight")).ToHaveCountAsync(1);
        });

    [Fact]
    public Task Pdf_ThumbnailsNavigateAndReportThePageSize() =>
        ReviewInBrowser("pdf-thumbnails", "report.pdf", workspace =>
        {
            CreateReport(workspace);
            workspace.Run("words", "convert", "report.docx", "--to", "pdf", "--out", "report.pdf", "--output", "json").Succeeded();
        },
        async (page, view) =>
        {
            JsonArray parts = view["parts"]!.AsArray();
            await Expect(page.Locator(".av-thumb")).ToHaveCountAsync(parts.Count);

            await page.Locator(".av-thumb").Nth(1).ClickAsync();

            await Expect(page.Locator(".av-position")).ToHaveTextAsync(Position("Page", 1, view));
            await Expect(page.Locator(".av-thumb").Nth(1)).ToHaveAttributeAsync("aria-current", "true");
            await Expect(page.Locator(".av-status-part"))
                .ToContainTextAsync(parts[1]!["properties"]!["size"]!.GetValue<string>());
        });

    [Fact]
    public Task Slides_KeysStepThroughSlidesWithTheirNotes() =>
        ReviewInBrowser("slides-deck", "deck.pptx", CreateDeck, async (page, view) =>
        {
            JsonArray parts = view["parts"]!.AsArray();
            await Expect(page.Locator(".av-stage .av-part")).ToHaveAttributeAsync(
                "data-part-id", parts[0]!["id"]!.GetValue<string>());
            await Expect(page.Locator(".av-thumb").Nth(2).Locator(".av-hidden-badge")).ToHaveCountAsync(1);

            await page.Keyboard.PressAsync("ArrowRight");

            await Expect(page.Locator(".av-position")).ToHaveTextAsync(Position("Slide", 1, view));
            await Expect(page.Locator(".av-stage .av-part")).ToHaveAttributeAsync(
                "data-part-id", parts[1]!["id"]!.GetValue<string>());
            // Evaluation mode truncates notes, so the pane must show what the view carries.
            string notes = parts[1]!["properties"]!["notes"]!.GetValue<string>();
            Assert.StartsWith("Pause", notes, StringComparison.Ordinal);
            await Expect(page.Locator(".av-notes-text")).ToHaveTextAsync(notes);
            await Expect(page.Locator(".av-thumb").Nth(1)).ToHaveAttributeAsync("aria-current", "true");

            await page.Keyboard.PressAsync("End");

            await Expect(page.Locator(".av-position")).ToHaveTextAsync(Position("Slide", 2, view));
        });

    [Fact]
    public Task Cells_SheetTabsSwitchSheetsAndZoomResizesThem() =>
        ReviewInBrowser("cells-tabs", "book.xlsx", CreateWorkbook, async (page, view) =>
        {
            // Evaluation mode may append warning sheets after the workbook's own.
            await Expect(page.Locator(".av-tab")).ToHaveCountAsync(view["parts"]!.AsArray().Count);
            await Expect(page.Locator(".av-tab").First).ToHaveTextAsync("Summary");
            await Expect(page.Locator(".av-tab").First).ToHaveAttributeAsync("aria-selected", "true");

            await page.Locator(".av-tab", new() { HasText = "Data" }).ClickAsync();

            await Expect(page.Locator(".av-tab").Nth(1)).ToHaveAttributeAsync("aria-selected", "true");
            await Expect(page.Locator(".av-position")).ToHaveTextAsync(Position("Sheet", 1, view));
            ILocator sheet = page.Locator(".av-stage .av-part[data-part-id='Data']");
            await Expect(sheet).ToBeVisibleAsync();
            int width = view["parts"]![1]!["width"]!.GetValue<int>();
            Assert.Equal(width, (await sheet.BoundingBoxAsync())!.Width, 1);

            await page.Keyboard.PressAsync("+");

            await Expect(page.Locator(".av-zoom-label")).ToHaveTextAsync("110%");
            Assert.Equal(Math.Round(width * 1.1), (await sheet.BoundingBoxAsync())!.Width, 1);
        });

    [Fact]
    public Task Slides_OnANarrowScreenTheRailOverlaysTheStage() =>
        ReviewInBrowser("slides-narrow", "deck.pptx", CreateDeck, async (page, view) =>
        {
            Assert.True(await page.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth <= window.innerWidth"),
                "The viewer must fit a narrow screen without horizontal scrolling.");
            await Expect(page.Locator(".av-sidebar")).ToBeHiddenAsync();

            await page.Locator("button[aria-controls='av-sidebar']").ClickAsync();
            await page.Locator(".av-thumb").Nth(1).ClickAsync();

            await Expect(page.Locator(".av-sidebar")).ToBeHiddenAsync();
            await Expect(page.Locator(".av-status-part")).ToContainTextAsync(Position("Slide", 1, view));
        },
        new ViewportSize { Width = 400, Height = 800 });

    [Fact]
    public async Task Review_FindingsTakeTheReaderToTheirEvidence()
    {
        using var temp = new TempDirectory();
        ReviewResult result = WriteReview(
            temp,
            "pages",
            artifacts =>
            {
                artifacts.Write("page-1.png", static stream => stream.Write(Png));
                artifacts.Write("page-2.png", static stream => stream.Write(Png));
            },
            [Page(1), Page(2)],
            new ProductReviewAssessment
            {
                Findings =
                [
                    new ReviewFinding
                    {
                        Code = "TEXT_OVERFLOW",
                        Severity = "error",
                        Message = "Text overflows its frame.",
                        Evidence = ["artifacts/page-2.png"],
                    },
                ],
            });

        await OpenInBrowser("review-findings", result.Index, async page =>
        {
            await Expect(page.Locator(".av-panel")).ToBeVisibleAsync();
            await Expect(page.Locator(".av-finding[data-severity='error']")).ToContainTextAsync("TEXT_OVERFLOW");

            await page.Locator(".av-evidence").ClickAsync();

            await Expect(page.Locator(".av-position")).ToHaveTextAsync("Page 2 of 2");
            await Expect(page.Locator(".av-thumb").Nth(1)).ToHaveAttributeAsync("aria-current", "true");
        });

        static ViewPart Page(int number) => new()
        {
            Id = "page-" + number,
            Label = "Page " + number,
            File = $"page-{number}.png",
            Kind = ViewPartKinds.Image,
            Width = 816,
            Height = 1056,
        };
    }

    [Fact]
    public async Task Review_HtmlPartsFillTheStageWithoutZoom()
    {
        using var temp = new TempDirectory();
        ReviewResult result = WriteReview(
            temp,
            "tabs",
            artifacts => artifacts.WriteText("book.html", "<html><body><p>Region</p></body></html>"),
            [new ViewPart { Id = "book", Label = "Book", File = "book.html", Kind = ViewPartKinds.Html }],
            new ProductReviewAssessment());

        await OpenInBrowser("review-html", result.Index, async page =>
        {
            ILocator frame = page.Locator(".av-stage .av-part[data-kind='html'] iframe");
            await Expect(page.FrameLocator(".av-stage iframe").Locator("body")).ToHaveTextAsync("Region");
            await Expect(page.Locator(".av-zoom-label")).ToBeDisabledAsync();
            Assert.True((await frame.BoundingBoxAsync())!.Height > 600, "An HTML part must fill the stage.");
        });
    }

    [Fact]
    public async Task Review_WithoutScriptsListsEveryPart()
    {
        using var workspace = new TempWorkspace();
        CreateWorkbook(workspace);
        workspace.Run("review", "book.xlsx", "--out", "evidence", "--output", "json").Succeeded();
        int parts = JsonNode.Parse(File.ReadAllText(
            workspace.File(Path.Combine("evidence", "artifacts", "view.json"))))!["parts"]!.AsArray().Count;

        await OpenInBrowser(
            "review-noscript",
            workspace.File(Path.Combine("evidence", "index.html")),
            async page => await Expect(page.Locator(".av-fallback figure img")).ToHaveCountAsync(parts),
            javaScriptEnabled: false);
    }

    /// <summary>Writes review evidence for a test product shown with the given kit layout.</summary>
    private static ReviewResult WriteReview(
        TempDirectory temp,
        string layout,
        Action<IViewArtifactSink> writeParts,
        ViewPart[] parts,
        ProductReviewAssessment assessment) =>
        ReviewEvidenceWriter.Write(
            temp.File("source.test"),
            "test",
            temp.File("review"),
            maxItems: 4,
            visualInspectionRequired: true,
            new ViewPresentation(
                "AsposeViewer.definePresenter('test', { kind: 'Test', glyph: 'T', views: { pages: "
                + $"{{ layout: '{layout}', noun: 'Page', sidebar: 'thumbnails' }} }} }});",
                null),
            artifacts =>
            {
                writeParts(artifacts);
                return new ViewManifest
                {
                    View = "pages",
                    SourceFormat = "test",
                    SourceSizeBytes = 1,
                    TotalParts = parts.Length,
                    Parts = parts,
                };
            },
            _ => assessment,
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]), Aspose.Cli.Sdk.Tests.TestBudgets.Create());

    private async Task ReviewInBrowser(
        string name,
        string file,
        Action<TempWorkspace> arrange,
        Func<IPage, JsonNode, Task> test,
        ViewportSize? viewport = null)
    {
        using var workspace = new TempWorkspace();
        arrange(workspace);
        workspace.Run("review", file, "--out", "evidence", "--output", "json").Succeeded();
        JsonNode view = JsonNode.Parse(File.ReadAllText(
            workspace.File(Path.Combine("evidence", "artifacts", "view.json"))))!;
        await OpenInBrowser(
            name,
            workspace.File(Path.Combine("evidence", "index.html")),
            page => test(page, view),
            viewport: viewport);
    }

    private async Task OpenInBrowser(
        string name,
        string index,
        Func<IPage, Task> test,
        ViewportSize? viewport = null,
        bool javaScriptEnabled = true)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = viewport ?? new ViewportSize { Width = 1400, Height = 900 },
            JavaScriptEnabled = javaScriptEnabled,
        });
        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        var failures = new List<string>();
        page.PageError += (_, error) => failures.Add("Script error: " + error);
        page.Request += (_, request) =>
        {
            if (!request.Url.StartsWith("file:", StringComparison.Ordinal))
            {
                failures.Add("Network request: " + request.Url);
            }
        };
        await page.GotoAsync(new Uri(index).AbsoluteUri);
        try
        {
            await test(page);
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
        string evidence = BrowserApp.EvidenceDirectory(name, output);
        try
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "page.png"), FullPage = true });
            File.WriteAllText(Path.Combine(evidence, "page.html"), await page.ContentAsync());
        }
        catch (Exception exception) { output.WriteLine("Evidence collection: " + exception.GetType().Name); }
    }

    /// <summary>A document with a heading per chapter and enough text to span several pages.</summary>
    private static void CreateReport(TempWorkspace workspace)
    {
        string paragraph = string.Concat(Enumerable.Repeat(
            "The team delivered steady progress across every region and product line. ", 8));
        File.WriteAllText(workspace.File("report.md"), "# Quarterly report\n\n" + string.Concat(
            Enumerable.Range(1, 4).Select(chapter =>
                $"## Chapter {chapter} results\n\n"
                + string.Concat(Enumerable.Repeat(paragraph + "\n\n", 5)))));
        workspace.Run("words", "create", "report.docx", "--markdown", "report.md", "--output", "json").Succeeded();
    }

    /// <summary>Three slides: the second carries speaker notes and the third is hidden.</summary>
    private static void CreateDeck(TempWorkspace workspace)
    {
        File.WriteAllText(workspace.File("deck.md"), string.Concat(
            new[] { "Quarterly results", "Revenue by region", "Appendix" }
                .Select(static title => $"# {title}\n\n- First point\n- Second point\n\n")));
        workspace.Run("slides", "create", "deck.pptx", "--markdown", "deck.md", "--output", "json").Succeeded();
        workspace.RunWithInput(
            """
            {"ops":[
              {"op":"set_notes","slide":2,"text":"Pause on the revenue chart."},
              {"op":"set_slide_hidden","slides":"3","hidden":true}
            ]}
            """,
            "slides", "edit", "deck.pptx", "--ops", "-", "--in-place", "--output", "json").Succeeded();
    }

    private static void CreateWorkbook(TempWorkspace workspace)
    {
        workspace.Run("cells", "create", "book.xlsx", "--sheets", "Summary,Data", "--output", "json").Succeeded();
        workspace.Run("cells", "edit", "book.xlsx", "--in-place",
            "--set", "Summary!A1=Region", "--set", "Summary!B1=Revenue",
            "--set", "Data!A1=Raw", "--set", "Data!B2=42", "--output", "json").Succeeded();
    }

    /// <summary>The index of the part that holds the first element matching the predicate.</summary>
    private static int PartOf(JsonNode view, Func<JsonNode, bool> predicate)
    {
        JsonArray parts = view["parts"]!.AsArray();
        for (int index = 0; index < parts.Count; index++)
        {
            if (parts[index]!["elements"]?.AsArray().Any(element => predicate(element!)) == true)
            {
                return index;
            }
        }
        return -1;
    }

    private static string Position(string noun, int index, JsonNode view) =>
        $"{noun} {index + 1} of {view["totalParts"]!.GetValue<int>()}";
}
