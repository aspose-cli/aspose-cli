using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.Review;

public sealed class ReviewEvidenceWriterTests
{
    private static readonly byte[] PngHeader =
    [
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13, 73, 72, 68, 82,
        0, 0, 0, 1, 0, 0, 0, 1,
    ];

    private static readonly ViewPresentation Presentation = new(
        "AsposeViewer.definePresenter('test', { kind: 'Test', glyph: 'T', views: { pages: { layout: 'pages', noun: 'Page' } } });",
        ".av-app[data-product=\"test\"] { --av-accent: #333; }");

    [Fact]
    public void Write_WhenPartsExceedTheirTotal_RejectsWithoutPublishing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("review");

        Assert.Throws<InvalidOperationException>(() => Write(
            temp,
            output,
            maxItems: 10,
            totalPartCount: 1,
            ["page-1.png", "page-2.png"]));

        Assert.False(Directory.Exists(output));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Write_UsesViewCoverageWithoutDroppingParts()
    {
        using var temp = new TempDirectory();

        ReviewResult result = Write(
            temp,
            temp.File("review"),
            maxItems: 2,
            totalPartCount: 5,
            ["page-1.png", "page-2.png"]);

        Assert.Equal(5, result.Coverage.DiscoveredItemCount);
        Assert.Equal(2, result.Coverage.ReportedItemCount);
        Assert.Equal(5, result.Coverage.ExpectedItemCount);
        Assert.Equal(2, result.Coverage.RenderedItemCount);
        Assert.Equal(3, result.Coverage.OmittedItemCount);
        Assert.True(result.Coverage.Truncated);
        Assert.False(result.Coverage.Complete);
        Assert.Contains(result.Artifacts, static artifact =>
            artifact.Path == "artifacts/view.json" && artifact.Role == "entry");
        Assert.Contains(result.Artifacts, static artifact => artifact.Path == "artifacts/page-1.png");
        Assert.Contains(result.Artifacts, static artifact => artifact.Path == "artifacts/page-2.png");
    }

    [Fact]
    public void Write_WhenTheAssessmentIsIncomplete_MarksCoverageIncomplete()
    {
        using var temp = new TempDirectory();

        ReviewResult result = Write(
            temp,
            temp.File("review"),
            maxItems: 1,
            totalPartCount: 1,
            ["page.png"],
            new ProductReviewAssessment { Complete = false });

        Assert.False(result.Coverage.Truncated);
        Assert.False(result.Coverage.Complete);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Write_EvidenceFollowsDocumentOrderInManifestAndGallery()
    {
        using var temp = new TempDirectory();
        string[] parts = ["slide-10.png", "slide-2.png", "nested/slide-1.png"];

        ReviewResult result = Write(temp, temp.File("review"), 8, parts.Length, parts);
        string[] expected = parts.Select(static part => "artifacts/" + part).ToArray();

        Assert.True(result.Coverage.Complete);
        Assert.Equal(
            ["index.html", "review.json", "artifacts/view.json"],
            result.Artifacts.Take(3).Select(static artifact => artifact.Path));
        Assert.Equal(expected, result.Artifacts
            .Where(static artifact => artifact.Role == "evidence")
            .Select(static artifact => artifact.Path));
        Assert.Equal(Enumerable.Range(0, result.Artifacts.Count), result.Artifacts.Select(static artifact => artifact.Sequence));
        Assert.Equal(expected, GalleryPaths(result));
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(result.Manifest))!.AsObject();
        Assert.Equal(expected, manifest["artifacts"]!.AsArray()
            .Where(static artifact => artifact!["role"]!.GetValue<string>() == "evidence")
            .Select(static artifact => artifact!["path"]!.GetValue<string>()));
    }

    [Fact]
    public void Write_PointsAFindingAtThePartItConcerns()
    {
        using var temp = new TempDirectory();
        var check = new ReviewCheck("TEST_PAGE_ODD", ReviewSeverities.Warning, "Flags a page.");
        var assessment = new ProductReviewAssessment
        {
            Findings =
            [
                check.Finding("Second page.", "page 2", part: "part-1"),
                check.Finding("Whole document."),
                check.Finding("Unrendered page.", "page 3", part: "part-2"),
            ],
        };

        ReviewResult result = Write(temp, temp.File("review"), 2, 3, ["page-1.png", "page-2.png"], assessment);

        Assert.Equal(["artifacts/page-2.png"], result.Findings[0].Evidence);
        Assert.Equal(["artifacts/page-1.png", "artifacts/page-2.png"], result.Findings[1].Evidence);
        Assert.Equal(["artifacts/view.json"], result.Findings[2].Evidence);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(result.Manifest))!.AsObject();
        Assert.Null(manifest["findings"]![0]!["part"]);
    }

    [Fact]
    public void Write_PublishesTheViewManifestWithPartDigests()
    {
        using var temp = new TempDirectory();

        ReviewResult result = Write(temp, temp.File("review"), 4, 2, ["page-1.png", "page-2.png"]);

        string digest = "sha256:" + Convert.ToHexString(SHA256.HashData(PngHeader)).ToLowerInvariant();
        JsonObject view = JsonNode.Parse(File.ReadAllText(
            Path.Combine(result.OutputDirectory, "artifacts", "view.json")))!.AsObject();
        Assert.Equal(CommonSchemaIds.View, view["schema"]!.GetValue<string>());
        Assert.Equal("pages", view["view"]!.GetValue<string>());
        Assert.All(view["parts"]!.AsArray(), part =>
            Assert.Equal(digest, part!["digest"]!.GetValue<string>()));
        Assert.All(result.Artifacts.Where(static artifact => artifact.Role == "evidence"), artifact =>
            Assert.Equal(PngHeader, File.ReadAllBytes(Path.Combine(
                result.OutputDirectory,
                artifact.Path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [Fact]
    public void Write_RejectsFilesTheManifestDoesNotName()
    {
        using var temp = new TempDirectory();
        string output = temp.File("review");

        Assert.Throws<InvalidOperationException>(() => ReviewEvidenceWriter.Write(
            temp.File("source.test"),
            "test",
            output,
            maxItems: 4,
            visualInspectionRequired: true,
            Presentation,
            artifacts =>
            {
                artifacts.Write("page-1.png", static stream => stream.Write(PngHeader));
                artifacts.WriteText("stray.css", "body{}");
                return Manifest(1, ["page-1.png"]);
            },
            static _ => new ProductReviewAssessment(),
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]), Aspose.Cli.Sdk.Tests.TestBudgets.Create()));

        Assert.False(Directory.Exists(output));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("/outside.png")]
    public void Write_RejectsTraversingArtifactPaths(string unsafePath)
    {
        using var temp = new TempDirectory();
        string outside = temp.File("outside.png");
        File.WriteAllText(outside, "unchanged");
        string output = temp.File("review");

        Assert.Throws<InvalidDataException>(() => ReviewEvidenceWriter.Write(
            temp.File("source.test"),
            "test",
            output,
            maxItems: 4,
            visualInspectionRequired: true,
            Presentation,
            artifacts =>
            {
                artifacts.Write(unsafePath, static stream => stream.Write(PngHeader));
                return Manifest(1, [unsafePath]);
            },
            static _ => new ProductReviewAssessment(),
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]), Aspose.Cli.Sdk.Tests.TestBudgets.Create()));

        Assert.False(Directory.Exists(output));
        Assert.Equal("unchanged", File.ReadAllText(outside));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, ".aspose-publication-*"));
    }

    [Theory]
    [InlineData("plates & figures/frame 2's.png")]
    [InlineData("Résumé.png")]
    public void Write_RejectsPartNamesOutsideTheSafeAlphabet(string unsafeName)
    {
        using var temp = new TempDirectory();
        string output = temp.File("review");

        Assert.Throws<InvalidOperationException>(() => Write(temp, output, 4, 1, [unsafeName]));

        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Write_IndexPresentsTheViewAndReviewInTheSharedViewer()
    {
        using var temp = new TempDirectory();

        ReviewResult result = Write(temp, temp.File("review"), 4, 2, ["page-1.png", "page-2.png"]);

        string html = File.ReadAllText(result.Index);
        JsonNode document = ViewerData(html);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(File.ReadAllText(Path.Combine(result.OutputDirectory, "artifacts", "view.json"))),
            document["view"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(result.Manifest)), document["review"]));
        Assert.Contains(Presentation.Script, html, StringComparison.Ordinal);
        Assert.Contains(Presentation.Stylesheet!, html, StringComparison.Ordinal);
        Assert.Contains("AsposeViewer.start({ base: \"artifacts/\" })", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_ReportsWhetherTheSourceIsEncrypted(bool encrypted)
    {
        using var temp = new TempDirectory();

        ReviewResult result = ReviewEvidenceWriter.Write(
            temp.File("source.test"), "test", temp.File("review"), 1, true, Presentation,
            artifacts =>
            {
                artifacts.Write("page.png", static stream => stream.Write(PngHeader));
                return Manifest(1, ["page.png"]) with { SourceEncrypted = encrypted };
            },
            static _ => new ProductReviewAssessment(), LicenseState.NotApplicable,
            new ContractJsonSerializer([]), Aspose.Cli.Sdk.Tests.TestBudgets.Create());

        Assert.Equal(encrypted, result.SourceEncrypted);
        Assert.Equal(encrypted, JsonNode.Parse(File.ReadAllText(result.Manifest))!["sourceEncrypted"]!.GetValue<bool>());
    }

    [Fact]
    public void Write_DocumentTextCannotEndTheViewerDataBlock()
    {
        using var temp = new TempDirectory();
        const string label = "</script><script>window.injected = true;</script><!--";

        ReviewResult result = ReviewEvidenceWriter.Write(
            temp.File("source.test"),
            "test",
            temp.File("review"),
            maxItems: 4,
            visualInspectionRequired: true,
            Presentation,
            artifacts =>
            {
                artifacts.Write("page-1.png", static stream => stream.Write(PngHeader));
                ViewManifest manifest = Manifest(1, ["page-1.png"]);
                return manifest with { Parts = [manifest.Parts[0] with { Label = label }] };
            },
            static _ => new ProductReviewAssessment(),
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]), Aspose.Cli.Sdk.Tests.TestBudgets.Create());

        string html = File.ReadAllText(result.Index);
        Assert.DoesNotContain(label, html, StringComparison.Ordinal);
        Assert.Equal(label, ViewerData(html)["view"]!["parts"]![0]!["label"]!.GetValue<string>());
    }

    [Fact]
    public void Write_WorkerOwnsAllReviewCandidatesUntilParentPublication()
    {
        using var temp = new TempDirectory();
        string workerRoot = Aspose.Cli.Sdk.IO.UserStorage.CreateTemporaryDirectory("worker");
        try
        {
            string manifest = Path.Combine(workerRoot, Aspose.Cli.Sdk.Execution.WorkerOutputSession.ManifestName);
            var worker = new Aspose.Cli.Sdk.Execution.WorkerOutputSession(workerRoot, manifest);
            using var deadline = Aspose.Cli.Sdk.Execution.OperationDeadline.Start(null);
            var budgets = new Aspose.Cli.Sdk.IO.ResourceBudgetLedger(deadline, outputSession: worker);
            string target = temp.File("review");
            ReviewEvidenceWriter.Write(temp.File("source.test"), "test", target, 1, true, Presentation,
                artifacts => { artifacts.Write("page.png", stream => stream.Write(PngHeader)); return Manifest(1, ["page.png"]); },
                _ => new ProductReviewAssessment(), LicenseState.NotApplicable, new ContractJsonSerializer([]), budgets);
            Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
            worker.SealForPublication();
            Aspose.Cli.Sdk.Execution.WorkerOutputSession.Publish(manifest, Aspose.Cli.Sdk.Tests.TestBudgets.Create());
            Assert.True(File.Exists(Path.Combine(target, "index.html")));
            Assert.True(File.Exists(Path.Combine(target, "review.json")));
            Assert.True(File.Exists(Path.Combine(target, "artifacts", "page.png")));
        }
        finally { Assert.True(Aspose.Cli.Sdk.IO.UserStorage.TryDeleteTree(workerRoot)); }
    }

    [Fact]
    public void Write_CancelledAssessmentReclaimsCandidateWithoutPublishing()
    {
        using var temp = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        using var deadline = Aspose.Cli.Sdk.Execution.OperationDeadline.Start(null, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => ReviewEvidenceWriter.Write(
            temp.File("source.test"), "test", temp.File("review"), 1, true, Presentation,
            artifacts => { artifacts.Write("page.png", stream => stream.Write(PngHeader)); return Manifest(1, ["page.png"]); },
            _ => { cancellation.Cancel(); return new ProductReviewAssessment(); },
            LicenseState.NotApplicable, new ContractJsonSerializer([]), new Aspose.Cli.Sdk.IO.ResourceBudgetLedger(deadline)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp.Path));
    }
    private static JsonNode ViewerData(string html)
    {
        Match block = Regex.Match(
            html,
            "<script type=\"application/json\" id=\"aspose-viewer-data\">(.*?)</script>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
        Assert.True(block.Success, "The page must carry the viewer data block.");
        return JsonNode.Parse(block.Groups[1].Value)!;
    }

    private static ReviewResult Write(
        TempDirectory temp,
        string output,
        int maxItems,
        int totalPartCount,
        string[] files,
        ProductReviewAssessment? assessment = null) =>
        ReviewEvidenceWriter.Write(
            temp.File("source.test"),
            "test",
            output,
            maxItems,
            visualInspectionRequired: true,
            Presentation,
            artifacts =>
            {
                foreach (string file in files)
                {
                    artifacts.Write(file, static stream => stream.Write(PngHeader));
                }
                return Manifest(totalPartCount, files);
            },
            _ => assessment ?? new ProductReviewAssessment(),
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]), Aspose.Cli.Sdk.Tests.TestBudgets.Create());

    private static ViewManifest Manifest(int totalPartCount, string[] files) => new()
    {
        View = "pages",
        SourceFormat = "test",
        SourceSizeBytes = 1,
        SourceEncrypted = false,
        TotalPartCount = totalPartCount,
        Parts = files.Select(static (file, index) => new ViewPart
        {
            Id = "part-" + index,
            Label = "Part " + (index + 1),
            File = file,
            Kind = ViewPartKinds.Image,
            Width = 1,
            Height = 1,
        }).ToArray(),
    };

    private static string[] GalleryPaths(ReviewResult result) =>
        Regex.Matches(File.ReadAllText(result.Index), "<img[^>]+src=\"([^\"]+)\"",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(static match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToArray();
}
