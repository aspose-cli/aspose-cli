using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// The SDK's one factory of result file objects: every input and output carries an absolute path,
/// its size and its fingerprint, and an output reports encryption exactly when its format can
/// carry a password.
/// </summary>
public sealed class FileObjectsTests : IDisposable
{
    private static readonly FormatDescriptor Protectable =
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".doc") with { Protectable = true };

    private static readonly FormatDescriptor Plain = FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png");

    private readonly TempDirectory _temp = new();
    private readonly SafeFileWriter _writer = TestBudgets.Writer();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Input_MakesThePathAbsolute_AndReadsSizeAndFingerprintFromTheFile(bool encrypted)
    {
        string path = _temp.File("in.doc");
        File.WriteAllText(path, "hello");
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, path);

        InputInfo input = FileObjects.Input(relative, "doc", encrypted);

        Assert.Equal(path, input.Path);
        Assert.True(Path.IsPathFullyQualified(input.Path));
        Assert.Equal("doc", input.Format);
        Assert.Equal(5, input.SizeBytes);
        Assert.Equal(FileFingerprints.Capture(path), input.Fingerprint);
        Assert.Equal(encrypted, input.Encrypted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Output_OfAProtectableFormat_ReportsWhetherItIsEncrypted(bool encrypted)
    {
        string path = _temp.File("out.doc");
        PublishedFile published = _writer.Write(path, overwrite: false, temp => File.WriteAllText(temp, "written"));

        OutputInfo output = FileObjects.Output(published, Protectable, encrypted);

        Assert.Equal(path, output.Path);
        Assert.Equal("doc", output.Format);
        Assert.Equal(7, output.SizeBytes);
        Assert.Equal(FileFingerprints.Capture(path), output.Fingerprint);
        Assert.Equal(encrypted, output.Encrypted);
    }

    [Fact]
    public void Output_OfAFormatWithoutPasswords_OmitsEncrypted()
    {
        PublishedFile published = _writer.Write(_temp.File("page.png"), overwrite: false, temp => File.WriteAllText(temp, "png"));

        Assert.Null(FileObjects.Output(published, Plain, encrypted: true).Encrypted);
    }

    [Fact]
    public void Output_TakesItsFactsFromThePublication_NotFromTheTarget()
    {
        // A supervised worker hands its outputs to the parent, so the target may not exist yet.
        var published = new PublishedFile(_temp.File("absent.png"), 3, new FileFingerprint { Sha256 = new string('a', 64) });

        OutputInfo output = FileObjects.Output(published, Plain, encrypted: false);

        Assert.False(File.Exists(output.Path));
        Assert.Equal(3, output.SizeBytes);
        Assert.Equal(new string('a', 64), output.Fingerprint.Sha256);
    }

    [Fact]
    public void Companion_IsDescribedByItsExtension()
    {
        PublishedFile published = _writer.Write(_temp.File("deck.Script.JS"), overwrite: false, temp => File.WriteAllText(temp, "js"));

        OutputInfo companion = FileObjects.Companion(published);

        Assert.Equal("js", companion.Format);
        Assert.Null(companion.Encrypted);
    }

    [Fact]
    public void OutputItem_CarriesTheFlatFileFactsBesideItsUnit()
    {
        PublishedFile published = _writer.Write(_temp.File("deck.3.png"), overwrite: false, temp => File.WriteAllText(temp, "slide"));
        OutputInfo file = FileObjects.Output(published, Plain, encrypted: false);

        var item = new SlideFile(file) { Slide = 3 };

        Assert.Equal(3, item.Slide);
        Assert.Equal(file.Path, item.Path);
        Assert.Equal(file.Format, item.Format);
        Assert.Equal(file.SizeBytes, item.SizeBytes);
        Assert.Equal(file.Fingerprint, item.Fingerprint);
    }

    /// <summary>A product's item of a flat <c>outputs</c> list: one rendered slide.</summary>
    private sealed record SlideFile : OutputItem
    {
        public SlideFile(OutputInfo file)
            : base(file)
        {
        }

        public required int Slide { get; init; }
    }
}
