using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests;

/// <summary>
/// The write pipeline alone discloses <c>ENCRYPTION_REMOVED</c>: when the command opened an
/// encrypted document and published a copy of it without encryption, in any of the ways a
/// product publishes, and never for an encrypted copy, an image, a data file or a command that
/// published nothing. The output's format decides what a copy is, not how a product publishes it.
/// </summary>
public sealed class OutputPipelineEncryptionTests
{
    [Fact]
    public void PlainCopyOfAnEncryptedInput_IsDisclosedOnceNamingItsFormat()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(encrypted: false);
        pipeline.Opened(encrypted: true);

        pipeline.Write(temp.File("out.txt"), false, null, Save);
        pipeline.Write(temp.File("other.txt"), false, null, Save);
        ResultEnvelope result = pipeline.Disclose(new SchemaListResult { Schemas = [], Warnings = [Other] });

        Assert.Equal([WarningCodes.EncryptionRemoved, Other.Code], result.Warnings!.Select(static warning => warning.Code));
        Assert.Contains("'txt' output is not encrypted", result.Warnings![0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainCopyInAnOutputSet_IsDisclosed()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(encrypted: true);

        using (OutputSet<object> set = pipeline.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("part.1.pdf"), false, null, Save, encrypted: true);
            set.Stage(temp.File("part.2.pdf"), false, null, Save);
            set.Commit();
        }

        Assert.Equal(WarningCodes.EncryptionRemoved, pipeline.EncryptionRemoved()?.Code);
    }

    [Fact]
    public void TextRenderedFromAnEncryptedInput_IsACopy()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(encrypted: true);

        pipeline.Write(temp.File("pages.txt"), false, null, Save, rendering: true);

        Assert.Equal(WarningCodes.EncryptionRemoved, pipeline.EncryptionRemoved()?.Code);
    }

    [Fact]
    public void EncryptedCopiesImagesDataAndPlainInputs_DiscloseNothing()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> encrypted = Pipeline();
        encrypted.Opened(encrypted: true);
        encrypted.Write(temp.File("kept.pdf"), false, null, Save, encrypted: true);
        encrypted.Write(temp.File("page.png"), false, null, Save, rendering: true);
        // Images saved without rendering, in a render format and in a declared image format, and
        // extracted form data.
        encrypted.Write(temp.File("sheet.png"), false, null, Save);
        encrypted.Write(temp.File("photo.JPG"), false, null, Save);
        encrypted.Write(temp.File("pages.TIF"), false, null, Save);
        encrypted.WriteExtracted(temp.File("fields.json"), false, Save);
        using (OutputSet<object> set = encrypted.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("page.2.png"), false, null, Save, rendering: true);
            set.Stage(temp.File("page.3.tiff"), false, null, Save);
            set.Commit();
        }

        OutputPipeline<object> plain = Pipeline();
        plain.Opened(encrypted: false);
        plain.Write(temp.File("plain.pdf"), false, null, Save);

        Assert.Null(encrypted.EncryptionRemoved());
        Assert.Null(plain.EncryptionRemoved());
    }

    [Fact]
    public void AnUndeclaredImageExtension_IsACopy()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(encrypted: true);

        // Only the formats the product declares are known; gif is not among them.
        pipeline.Write(temp.File("slide.gif"), false, null, Save);

        Assert.Contains("'gif'", pipeline.EncryptionRemoved()?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingPublished_DisclosesNothing()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(encrypted: true);
        using (OutputSet<object> set = pipeline.BeginSet([temp.Path], "test"))
        {
            // A set that does not commit, as a failed or dry run leaves it, publishes nothing.
            set.Stage(temp.File("part.pdf"), false, null, Save);
        }

        SchemaListResult result = new() { Schemas = [] };
        Assert.Same(result, pipeline.Disclose(result));
    }

    private static readonly Warning Other = new(WarningCodes.LossyConversion, "Other.");

    private static void Save(string path) => File.WriteAllText(path, "saved");

    private static OutputPipeline<object> Pipeline()
    {
        var pipeline = new OutputPipeline<object>(new LicensedGate(), new NoMarks(), TestBudgets.Writer());
        pipeline.Declare(
        [
            new FormatDescriptor("pdf", FormatUse.Input, ".pdf"),
            new FormatDescriptor("txt", FormatUse.Convert, ".txt"),
            new FormatDescriptor("png", FormatUse.Render, ".png"),
            new FormatDescriptor("jpeg", FormatUse.Render, ".jpg", ".jpeg"),
            new FormatDescriptor("tiff", FormatUse.Convert, ".tiff", ".tif") { Image = true },
        ]);
        return pipeline;
    }

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
