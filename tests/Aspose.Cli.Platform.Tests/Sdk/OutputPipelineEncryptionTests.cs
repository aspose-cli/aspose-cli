using Aspose.Cli.Host.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Formats;
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
        pipeline.Opened(new InputProtection(false));
        pipeline.Opened(new InputProtection(true));

        pipeline.Write(temp.File("out.txt"), Txt, false, null, Save);
        pipeline.Write(temp.File("other.txt"), Txt, false, null, Save);
        ResultEnvelope result = pipeline.Disclose(new SchemaListResult { Schemas = [], Warnings = [Other] });

        Assert.Equal([WarningCodes.EncryptionRemoved, Other.Code], result.Warnings!.Select(static warning => warning.Code));
        Assert.Contains("'txt' output is not encrypted", result.Warnings![0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainCopyInAnOutputSet_IsDisclosed()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(new InputProtection(true));

        using (OutputSet<object> set = pipeline.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("part.1.pdf"), Pdf, false, null, Save, encrypted: true);
            set.Stage(temp.File("part.2.pdf"), Pdf, false, null, Save);
            set.Commit();
        }

        Assert.Equal(WarningCodes.EncryptionRemoved, pipeline.EncryptionRemoved()?.Code);
    }

    [Fact]
    public void TextRenderedFromAnEncryptedInput_IsACopy()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(new InputProtection(true));

        pipeline.Write(temp.File("pages.txt"), Txt, false, null, Save, rendering: true);

        Assert.Equal(WarningCodes.EncryptionRemoved, pipeline.EncryptionRemoved()?.Code);
    }

    [Fact]
    public void EncryptedCopiesImagesDataAndPlainInputs_DiscloseNothing()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> encrypted = Pipeline();
        encrypted.Opened(new InputProtection(true));
        encrypted.Write(temp.File("kept.pdf"), Pdf, false, null, Save, encrypted: true);
        encrypted.Write(temp.File("page.png"), Png, false, null, Save, rendering: true);
        // Images saved without rendering, in a render format and in a declared image format,
        // whatever the case of their extension, and an extraction of what the engine derives.
        encrypted.Write(temp.File("sheet.png"), Png, false, null, Save);
        encrypted.Write(temp.File("photo.JPG"), Jpeg, false, null, Save);
        encrypted.Write(temp.File("pages.TIF"), Tiff, false, null, Save);
        using (ExtractionGuard extraction = encrypted.BeginExtraction(temp.File("extracted"), overwrite: false))
        {
            extraction.WriteAllBytes("fields.json", [1]);
            extraction.Commit();
        }

        using (OutputSet<object> set = encrypted.BeginSet([temp.Path], "test"))
        {
            set.Stage(temp.File("page.2.png"), Png, false, null, Save, rendering: true);
            set.Stage(temp.File("page.3.tiff"), Tiff, false, null, Save);
            set.Commit();
        }

        OutputPipeline<object> plain = Pipeline();
        plain.Opened(new InputProtection(false));
        plain.Write(temp.File("plain.pdf"), Pdf, false, null, Save);

        Assert.Null(encrypted.EncryptionRemoved());
        Assert.Null(plain.EncryptionRemoved());
    }

    [Fact]
    public void AFormatNotDeclaredAnImage_IsACopy()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(new InputProtection(true));

        // Only the format the product declares decides; this gif is not declared an image.
        pipeline.Write(temp.File("slide.gif"), Gif, false, null, Save);

        Assert.Contains("'gif'", pipeline.EncryptionRemoved()?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingPublished_DisclosesNothing()
    {
        using var temp = new TempDirectory();
        OutputPipeline<object> pipeline = Pipeline();
        pipeline.Opened(new InputProtection(true));
        using (OutputSet<object> set = pipeline.BeginSet([temp.Path], "test"))
        {
            // A set that does not commit, as a failed or dry run leaves it, publishes nothing.
            set.Stage(temp.File("part.pdf"), Pdf, false, null, Save);
        }

        SchemaListResult result = new() { Schemas = [] };
        Assert.Same(result, pipeline.Disclose(result));
    }

    private static readonly Warning Other = new(WarningCodes.LossyConversion, "Other.");

    private static void Save(string path) => File.WriteAllText(path, "saved");

    private static readonly FormatDescriptor Pdf = new("pdf", FormatUse.Input, ".pdf");

    private static readonly FormatDescriptor Txt = new("txt", FormatUse.Convert, ".txt");

    private static readonly FormatDescriptor Png = new("png", FormatUse.Render, ".png");

    private static readonly FormatDescriptor Jpeg = new("jpeg", FormatUse.Render, ".jpg", ".jpeg");

    private static readonly FormatDescriptor Tiff = new("tiff", FormatUse.Convert, ".tiff", ".tif") { Image = true };

    // An image format a product converts to but does not declare an image.
    private static readonly FormatDescriptor Gif = new("gif", FormatUse.Convert, ".gif");

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
