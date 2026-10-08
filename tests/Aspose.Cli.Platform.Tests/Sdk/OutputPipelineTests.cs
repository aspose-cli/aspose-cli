using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// The write pipeline discloses evaluation output from what it published: <c>EVAL_MODE</c>
/// with the marks a save added in evaluation mode, <c>EVAL_INPUT_MARKED</c> with the marks a
/// licensed output keeps, and nothing for a command that published nothing.
/// </summary>
public sealed class OutputPipelineTests
{
    [Fact]
    public void NothingPublished_DisclosesNothing()
    {
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Evaluation);

        SchemaListResult result = new() { Schemas = [], Warnings = [Other] };

        Assert.Same(result, pipeline.Disclose(result));
        Assert.Null(pipeline.Disclosure());
    }

    [Fact]
    public void EvaluationOutput_DisclosesEvalModeWithTheMarksItCarries()
    {
        using var temp = new TempDirectory();
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Evaluation);
        var document = new Document(["the banner"]);

        pipeline.Write(temp.File("out.doc"), false, document, path =>
        {
            File.WriteAllText(path, "saved");
            document.Marks.Add("the warning sheet");
        });
        ResultEnvelope result = pipeline.Disclose(new SchemaListResult { Schemas = [], Warnings = [Other] });

        Assert.Equal([WarningCodes.EvalMode, Other.Code], result.Warnings!.Select(static warning => warning.Code));
        Assert.EndsWith("The output carries the banner and the warning sheet.", result.Warnings![0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestedEvaluation_NamesTheRequest()
    {
        using var temp = new TempDirectory();
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Evaluation, LicenseResolution.EvaluationRequested);

        pipeline.Write(temp.File("image.png"), false, null, path => File.WriteAllText(path, "image"));

        Assert.Equal([EnvelopeParts.RequestedEvaluationWatermark], pipeline.Disclosure());
    }

    [Fact]
    public void LicensedOutput_DisclosesTheMarksItKeeps()
    {
        using var temp = new TempDirectory();
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Licensed);
        var clean = new Document([]);
        var marked = new Document(["the watermark"]) { Truncated = true };

        using (OutputSet<Document> set = pipeline.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("clean.doc"), false, clean, path => File.WriteAllText(path, "clean"));
            Assert.Null(pipeline.Disclosure());
            set.Stage(temp.File("marked.doc"), false, marked, path => File.WriteAllText(path, "marked"));
            set.Commit();
        }

        Warning disclosure = Assert.Single(pipeline.Disclosure()!);
        Assert.Equal(WarningCodes.EvalInputMarked, disclosure.Code);
        Assert.Contains("the watermark and the notice that evaluation mode cut the content short", disclosure.Message, StringComparison.Ordinal);
        Assert.True(disclosure.AffectsCompleteness);
    }

    [Fact]
    public void LicensedCleanOutput_DisclosesNothing()
    {
        using var temp = new TempDirectory();
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Licensed);
        var document = new Document(["the watermark"]);

        // The edit removed the input's marks before the save.
        pipeline.Write(temp.File("out.doc"), false, document, path =>
        {
            document.Marks.Clear();
            File.WriteAllText(path, "saved");
        });

        Assert.Null(pipeline.Disclosure());
    }

    [Fact]
    public void RolledBackSet_DisclosesNothing()
    {
        using var temp = new TempDirectory();
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Evaluation);

        using (OutputSet<Document> set = pipeline.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("page.png"), false, null, path => File.WriteAllText(path, "page"));
        }

        Assert.Null(pipeline.Disclosure());
        Assert.False(File.Exists(temp.File("page.png")));
    }

    [Theory]
    [InlineData(LicenseState.Evaluation)]
    [InlineData(LicenseState.Licensed)]
    public void FailedInspection_NeverFailsAPublicationAndLeavesTheMarksUnknown(LicenseState state)
    {
        using var temp = new TempDirectory();
        var pipeline = new OutputPipeline<Document>(new FixedGate(state, LicenseResolution.None), new ThrowingProfile(), TestBudgets.Writer());
        var document = new Document(["the banner"]);

        pipeline.Write(temp.File("one.doc"), false, document, path => File.WriteAllText(path, "one"));
        using (OutputSet<Document> set = pipeline.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("two.doc"), false, document, path => File.WriteAllText(path, "two"));
            set.Commit();
        }

        Assert.True(File.Exists(temp.File("one.doc")));
        Assert.True(File.Exists(temp.File("two.doc")));
        if (state == LicenseState.Evaluation)
        {
            Assert.Equal([EnvelopeParts.EvaluationWatermark], pipeline.Disclosure());
        }
        else
        {
            Assert.Null(pipeline.Disclosure());
        }
    }

    [Fact]
    public void RenderingOfAMarkedSource_SpeaksOfTheSource()
    {
        using var temp = new TempDirectory();
        OutputPipeline<Document> pipeline = Pipeline(LicenseState.Licensed);
        var source = new Document(["the watermark"]);

        using (OutputSet<Document> set = pipeline.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("p1.png"), false, source, path => File.WriteAllText(path, "1"), rendering: true);
            set.Stage(temp.File("p2.png"), false, source, path => File.WriteAllText(path, "2"), rendering: true);
            set.Commit();
        }

        Warning disclosure = Assert.Single(pipeline.Disclosure()!);
        Assert.Equal(WarningCodes.EvalInputMarked, disclosure.Code);
        Assert.StartsWith("The rendered source carries the evaluation marks", disclosure.Message, StringComparison.Ordinal);
        Assert.Contains("the watermark", disclosure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("The output keeps", disclosure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Neutralize_RunsOnlyInEvaluationMode()
    {
        var document = new Document(["the banner"]);

        Pipeline(LicenseState.Licensed).Neutralize(document);
        Assert.Equal(["the banner"], document.Marks);

        Pipeline(LicenseState.Evaluation).Neutralize(document);
        Assert.Empty(document.Marks);
    }

    private static readonly Warning Other = new() { Code = WarningCodes.LossyConversion, Message = "Other." };

    private static OutputPipeline<Document> Pipeline(LicenseState state, LicenseResolution? resolution = null) =>
        new(new FixedGate(state, resolution ?? LicenseResolution.None), new Profile(), TestBudgets.Writer());

    private sealed class Document(IEnumerable<string> marks)
    {
        public List<string> Marks { get; } = [.. marks];

        public bool Truncated { get; init; }
    }

    private sealed class Profile : IEvaluationProfile<Document>
    {
        public EvaluationMarks Inspect(Document document) => new([.. document.Marks], document.Truncated);

        public void Neutralize(Document document) => document.Marks.Clear();
    }

    private sealed class ThrowingProfile : IEvaluationProfile<Document>
    {
        public EvaluationMarks Inspect(Document document) => throw new InvalidOperationException("The engine failed.");
    }

    private sealed class FixedGate(LicenseState state, LicenseResolution resolution) : ILicenseGate
    {
        public bool IsApplicable => true;

        public LicenseResolution Resolution => resolution;

        public string Identity => "test";

        public LicenseState EnsureApplied() => state;
    }
}
