using System.Text.Json.Nodes;
using Aspose.Cli.Product.Pdf.Engine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Aspose.PDF evaluation mode reads only the first four pages of a document and throws on a
/// later one. The SDK reports that as the one <c>EVALUATION_LIMIT</c> every product shares; no
/// PDF command works around it or words it itself, and a render keeps its default of every
/// page, which a license then renders.
/// </summary>
public sealed class PdfEvaluationLimitTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void TheEnginesPageLimit_IsReportedByTheSdkInEvaluationMode()
    {
        // What Aspose.PDF in evaluation mode throws when a command reads page 5.
        var refusal = new IndexOutOfRangeException("Only 4 elements of any collection can be viewed in evaluation mode.");

        CliException error = Assert.Throws<CliException>(() => EvaluationLimits.Run<int>(Outputs(LicenseState.Evaluation), () => throw refusal));
        CliException selectable = Assert.Throws<CliException>(() => EvaluationLimits.Run<int>(
            Outputs(LicenseState.Evaluation), () => throw refusal, static unit => unit == "pages" ? "--pages" : null));
        Exception licensed = Assert.Throws<IndexOutOfRangeException>(() => EvaluationLimits.Run<int>(Outputs(LicenseState.Licensed), () => throw refusal));

        EvaluationLimits.AssertShape(error.Code.Name, error.Message, error.Hint, error.Details!.Select(static pair => pair.Key), "Aspose.PDF");
        Assert.Equal(4, error.Details!["limit"]!.GetValue<int>());
        Assert.Equal("pages", error.Details["unit"]!.GetValue<string>());
        Assert.Same(refusal, error.InnerException);
        Assert.Same(refusal, licensed);
        // Only a command whose selection keeps the engine within the limit is offered one.
        Assert.Equal("Apply an Aspose.PDF license.", error.Hint);
        Assert.Equal("Apply an Aspose.PDF license, or pass --pages 1-4.", selectable.Hint);
    }

    [Fact]
    public void EvaluationMode_RenderOfEveryPageOfALongerDocument_ReportsTheEnginesLimit()
    {
        CreateSixPages();

        CliResult render = _workspace.Run(
            "pdf", "render", "six.pdf", "--out", "page.png", "--license-mode", "evaluation", "--output-format", "json");

        Assert.Equal(7, render.ExitCode);
        JsonNode error = JsonNode.Parse(render.StdErr)!["error"]!;
        EvaluationLimits.AssertShape(
            error["code"]!.GetValue<string>(),
            error["message"]!.GetValue<string>(),
            error["hint"]?.GetValue<string>(),
            error["details"]!.AsObject().Select(static pair => pair.Key),
            "Aspose.PDF");
        Assert.Contains("--pages 1-4", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_workspace.Path, "page*.png"));
    }

    public static TheoryData<string> DocumentFormats => ["docx", "html", "xlsx", "pptx", "epub", "xps", "md"];

    /// <summary>
    /// A document export is made from every page it keeps, and keeping only some removes the
    /// others, which reads them too: past the limit it stops whatever --pages selects, rather than
    /// writing the first pages as if they were the document, and offers only a license.
    /// </summary>
    [Theory]
    [MemberData(nameof(DocumentFormats))]
    public void EvaluationMode_ConvertOfALongerDocumentToADocumentFormat_ReportsTheEnginesLimitAndWritesNothing(string format)
    {
        CreateSixPages();

        foreach (string[] selection in new[] { Array.Empty<string>(), ["--pages", "1-4"] })
        {
            CliResult convert = _workspace.Run(
                ["pdf", "convert", "six.pdf", "--to", format, "--out", "six." + format, .. selection, "--license-mode", "evaluation", "--output-format", "json"]);

            Assert.True(convert.ExitCode == 7, $"{format} {string.Join(' ', selection)}: {convert.StdOut}{convert.StdErr}");
            JsonNode error = JsonNode.Parse(convert.StdErr)!["error"]!;
            AssertLimit(error);
            Assert.Equal("Apply an Aspose.PDF license.", error["hint"]!.GetValue<string>());
            Assert.Equal(["six.pdf"], Directory.GetFiles(_workspace.Path).Select(Path.GetFileName));
        }
    }

    /// <summary>Page images and page text read only the pages they show, so a selection within the limit is offered.</summary>
    [Theory]
    [InlineData("png")]
    [InlineData("txt")]
    public void EvaluationMode_ConvertOfEveryPageToPagesOrText_OffersASelectionWithinTheLimit(string format)
    {
        CreateSixPages();

        CliResult every = _workspace.Run(
            "pdf", "convert", "six.pdf", "--to", format, "--out", "six." + format, "--license-mode", "evaluation", "--output-format", "json");
        CliResult first = _workspace.Run(
            "pdf", "convert", "six.pdf", "--to", format, "--out", "six." + format, "--pages", "1-4", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(every.ExitCode == 7, every.StdOut + every.StdErr);
        JsonNode error = JsonNode.Parse(every.StdErr)!["error"]!;
        AssertLimit(error);
        Assert.Equal("Apply an Aspose.PDF license, or pass --pages 1-4.", error["hint"]!.GetValue<string>());
        Assert.True(first.ExitCode == 0, first.StdErr);
    }

    [LicensedFact]
    public void Licensed_RenderOfEveryPageOfTheSameDocument_Succeeds()
    {
        CreateSixPages();
        ScenarioLicense.Project(_workspace.Path);

        CliResult render = _workspace.Run("pdf", "render", "six.pdf", "--out", "page.png", "--output-format", "json");

        Assert.True(render.ExitCode == 0, render.StdErr);
        Assert.Equal(6, render.Json()["outputs"]!.AsArray().Count);
    }

    public void Dispose() => _workspace.Dispose();

    private static void AssertLimit(JsonNode error) => EvaluationLimits.AssertShape(
        error["code"]!.GetValue<string>(),
        error["message"]!.GetValue<string>(),
        error["hint"]?.GetValue<string>(),
        error["details"]!.AsObject().Select(static pair => pair.Key),
        "Aspose.PDF");

    private void CreateSixPages()
    {
        using var fixture = new PdfEngineFixture();
        File.Copy(fixture.CreateRawDocument("six.pdf", pages: 6), _workspace.File("six.pdf"));
    }

    private static OutputPipeline<Aspose.Pdf.Document> Outputs(LicenseState state)
    {
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<PdfModule>();
        return new OutputPipeline<Aspose.Pdf.Document>(EvaluationLimits.Gate(state), new PdfEvaluationProfile(), new SafeFileWriter(budgets));
    }
}
