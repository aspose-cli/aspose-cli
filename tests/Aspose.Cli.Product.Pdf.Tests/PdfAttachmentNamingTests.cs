using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// An attachment's name is the document author's, so <c>pdf extract --what attachments</c>
/// never writes a file under it: each attachment is <c>attachment-0001</c> and on, with an
/// extension only from a short list of safe ones (<c>.bin</c> otherwise), and its own name is
/// reported in the result. With <c>--overwrite</c> into the input's own directory, an
/// attachment named like the input, like a parent path or like a shell file leaves the
/// directory as it was apart from the numbered files.
/// </summary>
public sealed class PdfAttachmentNamingTests
{
    /// <summary>A hostile attachment name and the file it must be written as.</summary>
    public static TheoryData<string, string> Names => new()
    {
        { "report.pdf", "attachment-0001.pdf" },
        { @"..\escaped.txt", "attachment-0001.txt" },
        { "desktop.ini", "attachment-0001.bin" },
        { "invoice.pdf.exe", "attachment-0001.bin" },
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void AnAttachment_IsWrittenUnderASequenceNumberAndReportsItsOwnName(string name, string written)
    {
        using var fixture = new PdfEngineFixture();
        string directory = fixture.File("work");
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "report.pdf");
        WriteAttachment(input, name, "attached");
        byte[] original = File.ReadAllBytes(input);

        PdfExtractResult result = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(directory, overwrite: true),
            What = PdfExtractKinds.Attachments,
        });

        Assert.Equal(original, File.ReadAllBytes(input));
        PdfPageOutput output = Assert.Single(result.Outputs);
        Assert.Equal(Path.Combine(directory, written), output.Path);
        Assert.Equal(name, output.Name);
        Assert.Equal("attached", File.ReadAllText(output.Path));
        Assert.Equal(["attachment-0001" + Path.GetExtension(written), "report.pdf"],
            Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(["work"], Directory.EnumerateFileSystemEntries(fixture.Temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void Attachments_AreNumberedInNameOrder()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("report.pdf");
        WriteAttachments(input, ("b.csv", "second"), ("a.txt", "first"));

        PdfExtractResult result = PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("files")),
            What = PdfExtractKinds.Attachments,
        });

        Assert.Equal(["attachment-0001.txt", "attachment-0002.csv"], result.Outputs.Select(static item => Path.GetFileName(item.Path)));
        Assert.Equal(["first", "second"], result.Outputs.Select(static item => File.ReadAllText(item.Path)));
        Assert.Equal(["a.txt", "b.csv"], result.Outputs.Select(static item => item.Name));
    }

    [Fact]
    public void AnAttachmentThatWouldReplaceTheInput_IsRefusedAndTheInputKept()
    {
        using var fixture = new PdfEngineFixture();
        string directory = fixture.File("work");
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "attachment-0001.pdf");
        WriteAttachment(input, "attachment-0001.pdf", "attached");
        byte[] original = File.ReadAllBytes(input);

        CliException error = Assert.Throws<CliException>(() => PdfExtract.Run(fixture.Session, new PdfExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(directory, overwrite: true),
            What = PdfExtractKinds.Attachments,
        }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Equal([input], Directory.EnumerateFileSystemEntries(directory));
    }

    private static void WriteAttachment(string path, string name, string content) => WriteAttachments(path, (name, content));

    // A one-page PDF written without the engine, so the names are stored exactly as given.
    private static void WriteAttachments(string path, params (string Name, string Content)[] attachments)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [" + string.Concat(attachments.Select(
                (item, index) => $"({Literal(item.Name)}) {5 + (index * 2)} 0 R ")) + "] >> >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
        };
        foreach ((string name, string content) in attachments)
        {
            objects.Add($"<< /Type /Filespec /F ({Literal(name)}) /UF ({Literal(name)}) /EF << /F {objects.Count + 2} 0 R >> >>");
            objects.Add($"<< /Type /EmbeddedFile /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        PdfEngineFixture.WriteRawPdf(path, objects);
    }

    private static string Literal(string value) => value.Replace(@"\", @"\\", StringComparison.Ordinal);
}
