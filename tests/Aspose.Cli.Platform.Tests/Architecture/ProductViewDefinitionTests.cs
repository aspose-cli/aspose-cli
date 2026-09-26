using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Views;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductViewDefinitionTests
{
    [Fact]
    public void ViewDefinition_RejectsAnUndeclaredViewBeforeProductDispatch()
    {
        ProductViewDefinition views = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>(),
            "test");
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort>(
                "test",
                static _ => new TestPort());

        CliException failure = Assert.Throws<CliException>(() =>
            views.Render(binding, "file.test", Request("missing"), new RejectingSink()));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }

    [Fact]
    public void ViewDefinition_MissingFontsCannotRemainComplete()
    {
        ProductViewDefinition views = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>(),
            "test");
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort>(
                "test",
                static _ => new FontPort(),
                static _ => new FontPort());
        ViewRenderRequest request = Request("document");

        ViewManifest rendered = views.Render(binding, "file.test", request, new RejectingSink());
        ProductReviewAssessment assessment = views.Assess(binding, "file.test", request, rendered);

        Assert.False(assessment.Complete);
        Assert.Contains(
            assessment.Findings!,
            static finding => finding.Code == "FONTS_MISSING_OR_SUBSTITUTED"
                && finding.Severity == "error");
    }

    [Fact]
    public void ViewDefinition_RequiresProductPrefixedChecksAndReportsOnlyDeclaredFindings()
    {
        var declared = new ReviewCheck("TEST_PAGE_BLANK", ReviewSeverities.Warning, "A page has no content.");
        ProductBinding<ITestPort> binding = ProductBinding.CreateLicenseFree<ITestPort>(
            "test", static _ => new FontPort(), static _ => new FontPort());
        ViewRenderRequest request = Request("document");

        Assert.Throws<InvalidOperationException>(() => ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>([new ReviewCheck("OTHER_PAGE_BLANK", ReviewSeverities.Info, "x")]),
            "test"));
        ProductViewDefinition declaring = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>([declared], [declared.Finding("Page 2 is blank.", "page 2")]),
            "test");
        ProductViewDefinition undeclared = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>(
                [declared], [declared.Finding("x") with { Severity = ReviewSeverities.Error }]),
            "test");

        Assert.Equal(
            ["FONTS_MISSING_OR_SUBSTITUTED", "FONTS_NOT_CHECKED", "TEST_PAGE_BLANK"],
            declaring.Checks.Select(static check => check.Code));
        Assert.Contains(
            declaring.Assess(binding, "file.test", request, declaring.Render(binding, "file.test", request, new RejectingSink())).Findings!,
            static finding => finding.Code == "TEST_PAGE_BLANK");
        Assert.Throws<InvalidOperationException>(() => undeclared.Assess(
            binding, "file.test", request, undeclared.Render(binding, "file.test", request, new RejectingSink())));
        Assert.Throws<ArgumentException>(() => new ReviewCheck("page blank", ReviewSeverities.Info, "x"));
    }

    private static ViewRenderRequest Request(string view) => new()
    {
        View = view,
        MaxParts = 1,
        Purpose = ViewPurpose.Evidence,
    };

    private sealed class RejectingSink : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter) =>
            throw new InvalidOperationException("The test view writes no artifacts.");

        public void WriteText(string relativePath, string content) =>
            throw new InvalidOperationException("The test view writes no artifacts.");
    }

    private interface ITestPort;

    private sealed class TestPort : ITestPort;

    private sealed class FontPort : ITestPort, IFontEnvironment
    {
        public FontListResult ListFonts() => new() { Sources = [] };

        public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) => new()
        {
            Source = new SourceInfo
            {
                Path = filePath,
                Format = "test",
                SizeBytes = 0,
            },
            AllAvailable = false,
            Fonts = [new FontAvailability { Name = "Missing", Available = false }],
        };

        public IDisposable UseFonts(Aspose.Cli.Sdk.Rendering.FontSearchProfile profile) =>
            Aspose.Cli.Sdk.Rendering.FontScope.Enter(profile, static _ => static () => { });
    }
}
