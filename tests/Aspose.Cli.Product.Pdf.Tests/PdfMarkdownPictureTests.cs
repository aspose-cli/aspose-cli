using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The Markdown export writes each picture it links as a file beside its output, which is
/// published alone: a conversion to md keeps the text as the export writes it without the
/// pictures, writes no picture file, and names the pictures it leaves out as <c>pictures</c> with
/// the <c>pdf extract --what images</c> command that takes them out. The scenario
/// <c>pdf-convert-of-a-pdf-with-a-picture-writes-every-format</c> runs the conversion and its hint
/// in both license modes.
/// </summary>
public sealed partial class PdfMarkdownPictureTests
{
    // A 40 x 40 PNG.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAIAAAADnC86AAAAMElEQVR4nO3NMQ0AAAgDsCmZfynIwgQJT5P+zbQvIhaLxWKxWCwWi8VisVgsFvcsXhHvWVsvWSOBAAAAAElFTkSuQmCC";

    /// <summary>
    /// From the command line: the md holds the heading and both paragraphs and links no picture,
    /// and the command the warning's hint gives, run as written, writes the picture as an image.
    /// </summary>
    [Fact]
    public void ConvertToMarkdown_KeepsTheTextAndItsHintTakesThePictureOut()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllBytes(workspace.File("dot.png"), Convert.FromBase64String(Png));
        File.WriteAllText(workspace.File("pic.md"), "# Report\n\nText before.\n\n![dot](dot.png)\n\nText after.\n");
        CliResult created = workspace.Run("pdf", "create", "pic.pdf", "--from-markdown", "pic.md", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdOut + created.StdErr);

        CliResult converted = workspace.Run("pdf", "convert", "pic.pdf", "--to", "md", "--out", "pic.out.md", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdOut + converted.StdErr);
        string markdown = File.ReadAllText(workspace.File("pic.out.md"));
        // The heading text: in evaluation mode the export joins it to the notice line as bold text.
        Assert.Contains("Report", markdown, StringComparison.Ordinal);
        Assert.Contains("Text before.", markdown, StringComparison.Ordinal);
        Assert.Contains("Text after.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("](", markdown, StringComparison.Ordinal);
        JsonNode lost = Assert.Single(converted.Json()["warnings"]!.AsArray(), static warning => warning!["details"]?["content"]?.ToJsonString() == """["pictures"]""")!;
        Assert.Contains("leaves out the 1 picture", lost["message"]!.GetValue<string>(), StringComparison.Ordinal);

        CliResult extracted = workspace.RunCommandLine(Assert.Single(QuotedCommand().Matches(lost["hint"]!.GetValue<string>())).Groups[1].Value);

        Assert.True(extracted.ExitCode == 0, extracted.StdOut + extracted.StdErr);
        string picture = Assert.Single(extracted.Json()["outputs"]!.AsArray())!["path"]!.GetValue<string>();
        Assert.True(File.Exists(picture), picture);
        Assert.EndsWith(".png", picture, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only the selected pages count: a conversion of a page without a picture names none, and one
    /// of every page names the one picture; either writes the md alone.
    /// </summary>
    [Theory]
    [InlineData("2", 0)]
    [InlineData(null, 1)]
    public void ConvertToMarkdown_NamesThePicturesOfTheSelectedPages(string? pages, int pictures)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("two-pages.pdf");
        using (var document = new Document())
        {
            Page first = document.Pages.Add();
            first.Paragraphs.Add(new TextFragment("First page"));
            first.AddImage(new MemoryStream(Convert.FromBase64String(Png)), new Rectangle(100, 600, 140, 640));
            document.Pages.Add().Paragraphs.Add(new TextFragment("Second page"));
            document.Save(input);
        }

        (PdfConvertResult result, string folder) = ConvertToMarkdown(fixture, input, pages);

        Assert.Equal(["out.md"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName));
        Warning[] named = [.. (result.Warnings ?? []).Where(static warning => warning.Details?["content"]?.ToJsonString() == """["pictures"]""")];
        Assert.Equal(pictures, named.Length);
        Assert.All(named, static warning => Assert.Contains("'pdf extract --what images'", warning.Hint, StringComparison.Ordinal));
    }

    /// <summary>
    /// A picture drawn inline in the page, one a tiling pattern paints, and one drawn from
    /// resources the page inherits are left out like any other, with the page's text kept; the
    /// picture of a stamp annotation is not exported, so nothing is left out.
    /// </summary>
    [Theory]
    [InlineData("inline", 1)]
    [InlineData("pattern", 1)]
    [InlineData("inherit", 1)]
    [InlineData("annot", 0)]
    public void ConvertToMarkdown_LeavesOutEveryKindOfPicture(string kind, int pictures)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File($"{kind}.pdf");
        WritePicturePage(input, kind);

        (PdfConvertResult result, string folder) = ConvertToMarkdown(fixture, input);

        Assert.Equal(["out.md"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName));
        string markdown = File.ReadAllText(Path.Combine(folder, "out.md"));
        Assert.Contains($"{kind} page text", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("](", markdown, StringComparison.Ordinal);
        Assert.Equal(pictures, (result.Warnings ?? []).Count(static warning => warning.Details?["content"]?.ToJsonString() == """["pictures"]"""));
    }

    /// <summary>A picture a form draws, such as the evaluation image of a PDF that Words wrote in evaluation mode, is left out too.</summary>
    [Fact]
    public void ConvertToMarkdown_LeavesOutAPictureAFormDraws()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("form-image.pdf");
        const string Form = "q 100 0 0 100 50 50 cm /Im0 Do Q";
        PdfEngineFixture.WriteRawPdf(input,
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /XObject << /Fm0 5 0 R >> >> /Contents 4 0 R >>",
            "<< /Length 11 >>\nstream\nq /Fm0 Do Q\nendstream",
            $"<< /Type /XObject /Subtype /Form /BBox [0 0 400 400] /Resources << /XObject << /Im0 6 0 R >> >> /Length {Form.Length} >>\nstream\n{Form}\nendstream",
            Gray,
        ]);

        (PdfConvertResult result, string folder) = ConvertToMarkdown(fixture, input);

        Assert.Equal(["out.md"], Directory.GetFileSystemEntries(folder).Select(Path.GetFileName));
        Warning named = Assert.Single(result.Warnings!, static warning => warning.Details?["content"]?.ToJsonString() == """["pictures"]""");
        Assert.Contains("leaves out the 1 picture", named.Message, StringComparison.Ordinal);
    }

    /// <summary>The value of a flattened field, which a form draws, stays in the md beside the picture left out.</summary>
    [Fact]
    public void ConvertToMarkdown_KeepsWhatAFormDrawsBesideAPicture()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("flattened-picture.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Application"));
            document.Form.Add(new Aspose.Pdf.Forms.TextBoxField(page, new Rectangle(100, 700, 300, 720)) { PartialName = "applicant", Value = "Zebra42" }, 1);
            page.AddImage(new MemoryStream(Convert.FromBase64String(Png)), new Rectangle(100, 500, 140, 540));
            document.Form.Flatten();
            document.Save(input);
        }

        (PdfConvertResult result, string folder) = ConvertToMarkdown(fixture, input);

        Assert.Contains("Zebra42", File.ReadAllText(Path.Combine(folder, "out.md")), StringComparison.Ordinal);
        Assert.Single(result.Warnings!, static warning => warning.Details?["content"]?.ToJsonString() == """["pictures"]""");
    }

    /// <summary>
    /// One image the page shows three times, which the export writes as one file it links three
    /// times: every link goes, the text keeps its order, and the warning counts three pictures.
    /// </summary>
    [Fact]
    public void ConvertToMarkdown_LeavesOutEachTimeAPictureIsShown()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("three-times.pdf");
        WriteTextAndImages(input, "BT /F1 18 Tf 72 700 Td (First line) Tj ET q 40 0 0 40 72 620 cm /Im0 Do Q "
            + "BT /F1 18 Tf 72 560 Td (Second line) Tj ET q 40 0 0 40 72 480 cm /Im0 Do Q q 40 0 0 40 200 480 cm /Im0 Do Q "
            + "BT /F1 18 Tf 72 420 Td (Third line) Tj ET");

        (PdfConvertResult result, string folder) = ConvertToMarkdown(fixture, input);

        string markdown = File.ReadAllText(Path.Combine(folder, "out.md"));
        Assert.DoesNotContain("](", markdown, StringComparison.Ordinal);
        Assert.Matches("(?s)First line.*Second line.*Third line", markdown);
        Warning named = Assert.Single(result.Warnings!, static warning => warning.Details?["content"]?.ToJsonString() == """["pictures"]""");
        Assert.Contains("leaves out the 3 pictures", named.Message, StringComparison.Ordinal);
    }

    /// <summary>Text that reads like a link to the picture's file stays: only the export's own links go.</summary>
    [Fact]
    public void ConvertToMarkdown_KeepsTextThatReadsLikeAPictureLink()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("link-text.pdf");
        WriteTextAndImages(input, "BT /F1 18 Tf 72 700 Td (See ![](image_1.png) here) Tj ET q 40 0 0 40 72 600 cm /Im0 Do Q");

        (PdfConvertResult result, string folder) = ConvertToMarkdown(fixture, input);

        string markdown = File.ReadAllText(Path.Combine(folder, "out.md"));
        Assert.Matches(@"See .*image\\?_1\.png.* here", markdown);
        Assert.Single(result.Warnings!, static warning => warning.Details?["content"]?.ToJsonString() == """["pictures"]""");
    }

    // A one-pixel gray image.
    private const string Gray = "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Length 1 >>\nstream\nA\nendstream";

    /// <summary>One page drawing <paramref name="content"/>, with the font /F1 and the image /Im0.</summary>
    private static void WriteTextAndImages(string path, string content) => PdfEngineFixture.WriteRawPdf(path,
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> /XObject << /Im0 6 0 R >> >> /Contents 4 0 R >>",
        $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        Gray,
    ]);

    /// <summary>
    /// One page with the text "<paramref name="kind"/> page text" and a 4 x 4 picture drawn
    /// <c>inline</c> in its content, by a tiling <c>pattern</c>, from the resources it
    /// <c>inherit</c>s from its page tree, or by the appearance of a stamp <c>annot</c>ation.
    /// </summary>
    private static void WritePicturePage(string path, string kind)
    {
        const string Pixels = "AAAAAAAAAAAAAAAA";
        const string Draw = "q 100 0 0 100 72 500 cm /Im0 Do Q";
        const string Paint = "q 20 0 0 20 0 0 cm /Im0 Do Q";
        string text = $"BT /F1 18 Tf 72 700 Td ({kind} page text) Tj ET\n";
        string content = kind switch
        {
            "inline" => text + $"q 100 0 0 100 72 500 cm BI /W 4 /H 4 /CS /G /BPC 8 ID\n{Pixels}\nEI Q",
            "inherit" => text + Draw,
            "pattern" => text + "/Pattern cs /P0 scn 72 400 200 200 re f",
            _ => text,
        };
        string resources = kind switch
        {
            "inline" or "annot" => "/Resources << /Font << /F1 5 0 R >> >>",
            "pattern" => "/Resources << /Font << /F1 5 0 R >> /Pattern << /P0 7 0 R >> >>",
            _ => string.Empty,
        };
        PdfEngineFixture.WriteRawPdf(path,
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [3 0 R] /Count 1 {(kind == "inherit" ? "/Resources << /Font << /F1 5 0 R >> /XObject << /Im0 6 0 R >> >>" : string.Empty)} >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R {resources} {(kind == "annot" ? "/Annots [8 0 R]" : string.Empty)} >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Type /XObject /Subtype /Image /Width 4 /Height 4 /ColorSpace /DeviceGray /BitsPerComponent 8 /Length {Pixels.Length} >>\nstream\n{Pixels}\nendstream",
            kind == "pattern"
                ? $"<< /Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 20 20] /XStep 20 /YStep 20 /Resources << /XObject << /Im0 6 0 R >> >> /Length {Paint.Length} >>\nstream\n{Paint}\nendstream"
                : $"<< /Type /XObject /Subtype /Form /BBox [0 0 100 100] /Resources << /XObject << /Im0 6 0 R >> >> /Length {Draw.Length} >>\nstream\n{Draw}\nendstream",
            "<< /Type /Annot /Subtype /Stamp /Rect [72 400 172 500] /AP << /N 7 0 R >> >>",
        ]);
    }

    private static (PdfConvertResult Result, string Folder) ConvertToMarkdown(PdfEngineFixture fixture, string input, string? pages = null)
    {
        string folder = Directory.CreateDirectory(fixture.File("out")).FullName;
        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest
        {
            Input = input,
            Pages = pages is null ? null : Sdk.Addressing.PageRange.Parse(pages),
            Output = TestOutput.At(Path.Combine(folder, "out.md"), format: "md"),
        });
        return (result, folder);
    }

    [GeneratedRegex("'(aspose-cli [^']+)'")]
    private static partial Regex QuotedCommand();
}
