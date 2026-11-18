using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// <see cref="OutputPipeline{TDocument}.PublishEdit"/> publishes an edited document as every
/// product edit does: a dry run writes nothing, an in-place edit backs up the file it replaces,
/// the read-back sees the staged file before anything is published, a read-back that fails
/// publishes nothing, and the output reports the encryption the product gave.
/// </summary>
public sealed class OutputPipelineEditPublicationTests
{
    private const string Original = "original";
    private const string Edited = "edited";

    [Fact]
    public void DryRun_WritesNoOutput()
    {
        using var temp = new TempDirectory();
        string input = Input(temp);
        bool saved = false;

        EditPublication<Checked> publication = Pipeline().PublishEdit<Checked>(
            InPlace(input), new object(), FileWritePrecondition.Capture(input), dryRun: true, encrypted: false,
            _ => saved = true, static path => new Checked(File.ReadAllText(path)), "test-edit");

        Assert.Equal(new EditPublication<Checked>(null, null), publication);
        Assert.False(saved);
        Assert.Equal(Original, File.ReadAllText(input));
        Assert.Equal([input], Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void InPlaceEdit_PublishesTheEdit()
    {
        using var temp = new TempDirectory();
        string input = Input(temp);

        EditPublication<Checked> publication = Pipeline().PublishEdit<Checked>(
            InPlace(input), new object(), FileWritePrecondition.Capture(input), dryRun: false, encrypted: false,
            Save, verify: null, "test-edit");

        Assert.Equal(Edited, File.ReadAllText(input));
        Assert.Equal(input, publication.Output!.Path);
        Assert.Null(publication.Verification);
    }

    [Fact]
    public void Verify_ReadsTheStagedOutputBeforeItIsPublished()
    {
        using var temp = new TempDirectory();
        string input = Input(temp);
        string? targetDuringVerify = null;

        EditPublication<Checked> publication = Pipeline().PublishEdit(
            InPlace(input), new object(), FileWritePrecondition.Capture(input), dryRun: false, encrypted: false,
            Save,
            candidate =>
            {
                targetDuringVerify = File.ReadAllText(input);
                return new Checked(File.ReadAllText(candidate));
            },
            "test-edit");

        Assert.Equal(Original, targetDuringVerify);
        Assert.Equal(new Checked(Edited), publication.Verification);
        Assert.Equal(Edited, File.ReadAllText(input));
    }

    [Fact]
    public void FailingVerify_PublishesNothing()
    {
        using var temp = new TempDirectory();
        string input = Input(temp);

        Assert.Throws<InvalidOperationException>(() => Pipeline().PublishEdit<Checked>(
            InPlace(input), new object(), FileWritePrecondition.Capture(input), dryRun: false, encrypted: false,
            Save, static _ => throw new InvalidOperationException("read-back failed"), "test-edit"));

        Assert.Equal(Original, File.ReadAllText(input));
        Assert.Equal([input], Directory.GetFiles(temp.Path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EncryptedFlag_IsReportedOnTheOutputAndDecidesEncryptionRemoved(bool encrypted)
    {
        using var temp = new TempDirectory();
        string input = Input(temp);
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(new InputProtection(true));

        EditPublication<Checked> publication = pipeline.PublishEdit<Checked>(
            InPlace(input), new object(), FileWritePrecondition.Capture(input), dryRun: false, encrypted,
            Save, verify: null, "test-edit");

        Assert.Equal(encrypted, publication.Output!.Encrypted);
        Assert.Equal(encrypted ? null : WarningCodes.EncryptionRemoved, pipeline.EncryptionRemoved()?.Code);
    }

    private sealed record Checked(string Content);

    private static readonly FormatDescriptor Pdf = new("pdf", FormatUse.Input, ".pdf") { Protectable = true };

    private static string Input(TempDirectory temp)
    {
        string input = temp.File("book.pdf");
        File.WriteAllText(input, Original);
        return input;
    }

    private static ResolvedOutput InPlace(string input) =>
        new(Pdf, input, overwrite: true, inPlace: true);

    private static void Save(string path) => File.WriteAllText(path, Edited);

    private static OutputPipeline<object> Pipeline() =>
        new(new LicensedGate(), new NoMarks(), TestBudgets.Writer());

    private sealed class NoMarks : IEvaluationProfile<object>
    {
        public string Engine => "Aspose.Test";

        public EvaluationMarks Inspect(object document) => EvaluationMarks.None;
    }

    private sealed class LicensedGate : ILicenseGate
    {
        public LicenseResolution Resolution => LicenseResolution.None;

        public string Identity => "test";

        public LicenseState EnsureApplied() => LicenseState.Licensed;
    }
}
