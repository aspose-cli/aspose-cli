using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Serialization;
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

    [Fact]
    public void Write_WhenRendererCoverageIsImpossible_RejectsWithoutPublishing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("review");

        Assert.Throws<InvalidOperationException>(() => ReviewEvidenceWriter.Write(
            temp.File("source.docx"),
            "words",
            "pages",
            output,
            maxItems: 10,
            directory =>
            {
                File.WriteAllText(Path.Combine(directory, "entry.html"), "<html></html>");
                File.WriteAllBytes(Path.Combine(directory, "page.png"), PngHeader);
                return new ProductReviewRenderOutcome("entry.html", "docx", 1)
                {
                    ExpectedItems = 1,
                    RenderedItems = 2,
                    Complete = true,
                };
            },
            LicenseState.NotApplicable,
            new ContractJsonSerializer([])));

        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Write_UsesRendererOwnedUnitCoverageWithoutDroppingArtifacts()
    {
        using var temp = new TempDirectory();
        string output = temp.File("review");

        var result = ReviewEvidenceWriter.Write(
            temp.File("source.docx"),
            "words",
            "pages",
            output,
            maxItems: 2,
            directory =>
            {
                File.WriteAllText(
                    Path.Combine(directory, "entry.html"),
                    "<html><link rel=\"stylesheet\" href=\"review.css\"></html>");
                File.WriteAllText(Path.Combine(directory, "review.css"), "body{}");
                File.WriteAllBytes(Path.Combine(directory, "page-1.png"), PngHeader);
                File.WriteAllBytes(Path.Combine(directory, "page-2.png"), PngHeader);
                return new ProductReviewRenderOutcome("entry.html", "docx", 1)
                {
                    ExpectedItems = 5,
                    RenderedItems = 2,
                    Complete = true,
                };
            },
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]));

        Assert.Equal(5, result.Coverage.DiscoveredItems);
        Assert.Equal(2, result.Coverage.ReportedItems);
        Assert.Equal(5, result.Coverage.ExpectedItems);
        Assert.Equal(2, result.Coverage.RenderedItems);
        Assert.Equal(3, result.Coverage.OmittedItems);
        Assert.True(result.Coverage.Truncated);
        Assert.False(result.Coverage.Complete);
        Assert.Contains(result.Artifacts, artifact => artifact.Path == "artifacts/review.css");
        Assert.Contains(result.Artifacts, artifact => artifact.Path == "artifacts/page-1.png");
        Assert.Contains(result.Artifacts, artifact => artifact.Path == "artifacts/page-2.png");
    }

    [Fact]
    public void Write_WhenVisualEvidenceIsIncomplete_MarksCoverageIncomplete()
    {
        using var temp = new TempDirectory();

        var result = ReviewEvidenceWriter.Write(
            temp.File("source.docx"),
            "words",
            "pages",
            temp.File("review"),
            maxItems: 1,
            directory =>
            {
                File.WriteAllText(Path.Combine(directory, "entry.html"), "<html></html>");
                File.WriteAllBytes(Path.Combine(directory, "page.png"), PngHeader);
                return new ProductReviewRenderOutcome("entry.html", "docx", 1)
                {
                    ExpectedItems = 1,
                    RenderedItems = 1,
                    Complete = false,
                };
            },
            LicenseState.NotApplicable,
            new ContractJsonSerializer([]));

        Assert.False(result.Coverage.Truncated);
        Assert.False(result.Coverage.Complete);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Write_NumberedEvidenceUsesTheSameNaturalOrderInManifestAndGallery()
    {
        using var temp = new TempDirectory();
        string[] pages = Enumerable.Range(1, 12).Select(number => $"frame.{number}.png").ToArray();

        ReviewResult result = WriteArtifacts(temp, pages.Reverse().ToArray());
        string[] expected = pages.Select(path => "artifacts/" + path).ToArray();

        Assert.True(result.Coverage.Complete);
        Assert.Equal(12, result.Coverage.RenderedItems);
        Assert.Equal(expected, result.Artifacts.Where(artifact => artifact.Role == "evidence")
            .Select(artifact => artifact.Path));
        Assert.Equal(Enumerable.Range(0, result.Artifacts.Count), result.Artifacts.Select(artifact => artifact.Sequence));
        Assert.Equal("index.html", result.Artifacts[0].Path);
        Assert.Equal("review.json", result.Artifacts[1].Path);
        Assert.Equal("artifacts/entry.html", result.Artifacts[2].Path);
        Assert.Equal(expected, GalleryPaths(result));
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(result.Manifest))!.AsObject();
        Assert.Equal(expected, manifest["artifacts"]!.AsArray()
            .Where(artifact => artifact!["role"]!.GetValue<string>() == "evidence")
            .Select(artifact => artifact!["path"]!.GetValue<string>()));
    }

    [Fact]
    public void Write_NestedNumbersAndLeadingZeroTiesAreDeterministic()
    {
        using var temp = new TempDirectory();
        string[] expected =
        [
            "set2/frame0.png", "set2/frame00.png",
            "set2/frame001.png", "set2/frame01.png", "set2/frame1.png",
            "set2/frame2a.png", "set2/frame02b.png", "set2/frame10.png",
            "set10/frame2.png",
        ];

        ReviewResult result = WriteArtifacts(temp, expected.Reverse().ToArray());

        Assert.Equal(expected.Select(path => "artifacts/" + path), GalleryPaths(result));
        Assert.Equal(expected.Select(path => "artifacts/" + path), result.Artifacts
            .Where(artifact => artifact.Role == "evidence").Select(artifact => artifact.Path));
    }

    [Fact]
    public void Write_NonnumericAssetsAndEscapedLinksKeepTheirExactSafePaths()
    {
        using var temp = new TempDirectory();
        string[] expected =
        [
            "Legend.png", "Résumé.png", "_notes.txt", "alpha.css", "alpha.png",
            "plates & figures/frame 2's.png", "plates & figures/frame 10's.png",
            "étude.png",
        ];

        ReviewResult result = WriteArtifacts(temp, expected.Reverse().ToArray());

        Assert.Equal(expected.Select(path => "artifacts/" + path), result.Artifacts
            .Where(artifact => artifact.Role == "evidence").Select(artifact => artifact.Path));
        string[] images = expected.Where(path => path.EndsWith(".png", StringComparison.Ordinal))
            .Select(path => "artifacts/" + path).ToArray();
        Assert.Equal(images, GalleryPaths(result));
        string html = File.ReadAllText(result.Index);
        foreach (string path in images)
        {
            Assert.Contains($"href=\"{WebUtility.HtmlEncode(path)}\"", html, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(result.OutputDirectory, path.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(PngHeader, File.ReadAllBytes(Path.Combine(result.OutputDirectory, path)));
        }
        Assert.Contains("plates &amp; figures/frame 2&#39;s.png", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"artifacts/plates & figures", html, StringComparison.Ordinal);
        Assert.Equal("body{}", File.ReadAllText(Path.Combine(result.OutputDirectory, "artifacts/alpha.css")));
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("/outside.html")]
    public void Write_OrderingDoesNotBypassEntryPathValidation(string unsafeEntry)
    {
        using var temp = new TempDirectory();
        string outside = temp.File("outside.html");
        File.WriteAllText(outside, "unchanged");
        string output = temp.File("review");

        Assert.Throws<InvalidDataException>(() => ReviewEvidenceWriter.Write(
            temp.File("source.test"), "test", "items", output, 12,
            directory =>
            {
                File.WriteAllBytes(Path.Combine(directory, "frame10.png"), PngHeader);
                return new ProductReviewRenderOutcome(unsafeEntry, "test", 1)
                {
                    ExpectedItems = 1,
                    RenderedItems = 1,
                    Complete = true,
                };
            }, LicenseState.NotApplicable, new ContractJsonSerializer([])));

        Assert.False(Directory.Exists(output));
        Assert.Equal("unchanged", File.ReadAllText(outside));
        Assert.Empty(Directory.EnumerateDirectories(temp.Path, "*.review.tmp"));
    }

    [Theory]
    [InlineData(null, "", -1)]
    [InlineData("", "a", -1)]
    [InlineData("frame2.png", "frame10.png", -1)]
    [InlineData("frame02.png", "frame2.png", -1)]
    [InlineData("frame00.png", "frame0.png", 1)]
    [InlineData("frame02b.png", "frame2a.png", 1)]
    [InlineData("set02/frame10.png", "set2/frame2.png", 1)]
    [InlineData("same1.png", "same1.png", 0)]
    [InlineData("frame2", "frame2.png", -1)]
    public void PathComparer_UsesNumericValuesAndOrdinalTieBreaks(string? left, string? right, int expected)
    {
        Assert.Equal(expected, Math.Sign(ReviewArtifactPathComparer.Instance.Compare(left, right)));
        Assert.Equal(-expected, Math.Sign(ReviewArtifactPathComparer.Instance.Compare(right, left)));
    }

    [Fact]
    public void PathComparer_HandlesNumericRunsBeyondAnyIntegerWidth()
    {
        string lower = "frame" + new string('9', 10_000) + ".png";
        string higher = "frame1" + new string('0', 10_000) + ".png";
        string padded = "frame" + new string('0', 10_000) + "9.png";

        Assert.True(ReviewArtifactPathComparer.Instance.Compare(lower, higher) < 0);
        Assert.True(ReviewArtifactPathComparer.Instance.Compare(higher, lower) > 0);
        Assert.True(ReviewArtifactPathComparer.Instance.Compare(padded, "frame10.png") < 0);
        Assert.Equal(Math.Sign(string.CompareOrdinal(padded, "frame9.png")),
            Math.Sign(ReviewArtifactPathComparer.Instance.Compare(padded, "frame9.png")));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("zh-CN")]
    public void PathComparer_RetainsOrdinalNonnumericOrderingAcrossCultures(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            string[] names = ["é.png", "a-b.png", "ab.png", "_note.png", "Z.png", "I.png", "ı.png", "frame٢.png"];

            Assert.Equal(names.Order(StringComparer.Ordinal), names.Order(ReviewArtifactPathComparer.Instance));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static ReviewResult WriteArtifacts(TempDirectory temp, params string[] paths) =>
        ReviewEvidenceWriter.Write(
            temp.File("source.test"), "test", "items", temp.File("review"), 32,
            directory =>
            {
                File.WriteAllText(Path.Combine(directory, "entry.html"), "<html></html>");
                foreach (string path in paths)
                {
                    string full = Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    if (path.EndsWith(".png", StringComparison.Ordinal))
                    {
                        File.WriteAllBytes(full, PngHeader);
                    }
                    else
                    {
                        File.WriteAllText(full, "body{}");
                    }
                }
                int pages = paths.Count(path => path.EndsWith(".png", StringComparison.Ordinal));
                return new ProductReviewRenderOutcome("entry.html", "test", 1)
                {
                    ExpectedItems = pages,
                    RenderedItems = pages,
                    Complete = true,
                };
            }, LicenseState.NotApplicable, new ContractJsonSerializer([]));

    private static string[] GalleryPaths(ReviewResult result) =>
        Regex.Matches(File.ReadAllText(result.Index), "<img[^>]+src=\"([^\"]+)\"",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToArray();
}
