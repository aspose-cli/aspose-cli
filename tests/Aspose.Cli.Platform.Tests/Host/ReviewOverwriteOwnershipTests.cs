using Aspose.Cli.Host.Review;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.Review;

/// <summary>
/// <c>review --overwrite</c> replaces a directory only when it holds exactly the evidence an
/// earlier review recorded in its <c>review.json</c>, every file with its recorded SHA-256, or
/// nothing at all. Anything else, a <c>review.json</c> beside other files included, is refused
/// and left as it is.
/// </summary>
public sealed class ReviewOverwriteOwnershipTests
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
    public void AReviewJsonBesideOtherFiles_DoesNotMakeTheDirectoryAnEarlierReview()
    {
        using var temp = new TempDirectory();
        string target = temp.File("project");
        Directory.CreateDirectory(Path.Combine(target, "sub"));
        File.WriteAllText(Path.Combine(target, "review.json"), "{}");
        File.WriteAllText(Path.Combine(target, "precious.txt"), "precious");
        File.WriteAllText(Path.Combine(target, "sub", "notes.txt"), "notes");
        string[] before = Tree(target);

        CliException error = Assert.Throws<CliException>(() => Write(temp, target));

        Assert.Equal(ErrorCodes.OutputExists, error.Code);
        Assert.Equal(before, Tree(target));
        Assert.Equal("precious", File.ReadAllText(Path.Combine(target, "precious.txt")));
    }

    /// <summary>How an earlier review's directory can stop being only that review.</summary>
    public static TheoryData<string> Changes => ["added file", "added directory", "changed file", "removed file", "changed index"];

    [Theory]
    [MemberData(nameof(Changes))]
    public void AnEarlierReviewThatWasChanged_IsRefusedAndKept(string change)
    {
        using var temp = new TempDirectory();
        string target = temp.File("review");
        Write(temp, target);
        switch (change)
        {
            case "added file": File.WriteAllText(Path.Combine(target, "artifacts", "sentinel.txt"), "sentinel"); break;
            case "added directory": Directory.CreateDirectory(Path.Combine(target, "mine")); break;
            case "changed file": File.WriteAllBytes(Path.Combine(target, "artifacts", "page.png"), [.. PngHeader, 1]); break;
            case "removed file": File.Delete(Path.Combine(target, "artifacts", "page.png")); break;
            case "changed index": File.AppendAllText(Path.Combine(target, "index.html"), "<!-- mine -->"); break;
        }
        string[] before = Tree(target);

        CliException error = Assert.Throws<CliException>(() => Write(temp, target));

        Assert.Equal(ErrorCodes.OutputExists, error.Code);
        Assert.Equal(before, Tree(target));
    }

    [Fact]
    public void AnUnchangedEarlierReview_IsReplaced()
    {
        using var temp = new TempDirectory();
        string target = temp.File("review");
        Write(temp, target);

        ReviewResult result = Write(temp, target, label: "second");

        Assert.Equal(target, result.OutputDirectory);
        Assert.Contains("second", File.ReadAllText(Path.Combine(target, "index.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyDirectory_IsReplaced()
    {
        using var temp = new TempDirectory();
        string target = temp.File("review");
        Directory.CreateDirectory(target);

        Write(temp, target);

        Assert.True(File.Exists(Path.Combine(target, "review.json")));
    }

    [Fact]
    public void AnEarlierReviewThatHoldsTheInputThroughAJunction_IsRefusedAndKept()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string target = temp.File("review");
        Write(temp, target);
        string junction = temp.File("linked");
        FileSystemLinks.CreateDirectoryLink(junction, target);
        try
        {
            string[] before = Tree(target);
            string input = Path.Combine(junction, "artifacts", "page.png");

            CliException error = Assert.Throws<CliException>(() => Write(temp, target, source: input));

            Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
            Assert.Equal(before, Tree(target));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    // Every file and directory below root with the bytes of each file.
    private static string[] Tree(string root) =>
        [.. Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)
                + (File.Exists(path) ? "=" + Convert.ToHexString(File.ReadAllBytes(path)) : "/"))
            .Order(StringComparer.Ordinal)];

    private static ReviewResult Write(TempDirectory temp, string output, string label = "first", string? source = null)
    {
        source ??= Source(temp);
        ResourceBudgetLedger budgets = Aspose.Cli.Sdk.Tests.TestBudgets.Create();
        budgets.AdmitFile(source);
        return ReviewEvidenceWriter.Write(
            source,
            "test",
            output,
            maxItems: 10,
            visualInspectionRequired: true,
            Presentation,
            artifacts =>
            {
                artifacts.Write("page.png", static stream => stream.Write(PngHeader));
                return Manifest(label);
            },
            _ => new ProductReviewAssessment(),
            LicenseState.Licensed,
            new ContractJsonSerializer([]),
            budgets,
            overwrite: true);
    }

    private static string Source(TempDirectory temp)
    {
        string path = temp.File("source.test");
        File.WriteAllText(path, "source");
        return path;
    }

    private static ViewManifest Manifest(string label) => new()
    {
        View = "pages",
        SourceFormat = "test",
        SourceSizeBytes = 1,
        SourceEncrypted = false,
        TotalPartCount = 1,
        Parts =
        [
            new ViewPart
            {
                Id = "part-0",
                Label = label,
                File = "page.png",
                Kind = ViewPartKinds.Image,
                Width = 1,
                Height = 1,
            },
        ],
    };
}
