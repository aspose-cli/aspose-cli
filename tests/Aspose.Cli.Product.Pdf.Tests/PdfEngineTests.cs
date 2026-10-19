using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Aspose.Pdf;
using CliPageRange = Aspose.Cli.Sdk.Addressing.PageRange;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfEngineTests
{
    [Theory]
    [InlineData(Sdk.Extensibility.Output.TableFormat.Plain)]
    [InlineData(Sdk.Extensibility.Output.TableFormat.Markdown)]
    public void Info_TableAndMarkdown_ShowEveryRequestedDetail(Sdk.Extensibility.Output.TableFormat format)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 2);
        using (var document = new Document(path))
        {
            var chapter = new OutlineItemCollection(document.Outlines) { Title = "Chapter", Destination = new FitExplicitDestination(document.Pages[1]) };
            chapter.Add(new OutlineItemCollection(document.Outlines) { Title = "Section", Destination = new FitExplicitDestination(document.Pages[2]) });
            document.Outlines.Add(chapter);
            document.EmbeddedFiles.Add("data.csv", new FileSpecification(new MemoryStream("a,b\n"u8.ToArray()), "data.csv", "Data") { Name = "data.csv", UnicodeName = "data.csv" });
            document.Info.Title = "Annual report";
            document.Save(path);
        }

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = path,
            Details = ["outline", "links", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "properties"],
        });
        using var writer = new StringWriter();
        Commands.InspectCommand.Table(info, new Sdk.Extensibility.Output.TableSurface(writer, format));
        string text = writer.ToString();

        string heading = format == Sdk.Extensibility.Output.TableFormat.Markdown ? "### " : string.Empty;
        foreach (string section in new[] { "outline", "links", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "properties" })
        {
            Assert.Contains(heading + section, text, StringComparison.Ordinal);
        }

        Assert.Contains("1/1", text, StringComparison.Ordinal);
        Assert.Contains("Section", text, StringComparison.Ordinal);
        Assert.Contains("data.csv", text, StringComparison.Ordinal);
        Assert.Contains(info.Fonts![0].Name, text, StringComparison.Ordinal);
        Assert.Contains("Annual report", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_FromHtml_TakesTheTitleFromTheHtmlAndInventsNoOtherMetadata()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("report.html");
        File.WriteAllText(input, """
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <title>
              2026 Q3 运营报告 &amp; East
            </title><meta name="author" content="Operations"></head>
            <body><h1>Report</h1></body></html>
            """);

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("report.pdf")), Source = new PdfHtmlSource(new SourceFile("--from-html", input), AllowNetworkResources: false) });

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = result.Output.Path, Details = ["properties"] });
        Assert.Equal("2026 Q3 运营报告 & East", info.Properties!["title"]);
        Assert.Null(info.Properties["author"]);
        Assert.Null(info.Properties["subject"]);
        Assert.DoesNotContain(info.Properties.Keys, static key => key.StartsWith("xmp:", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_FromHtml_DrawsTheBoxOfEveryCheckBox()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("form.html");
        File.WriteAllText(input, """
            <html><body><form>
            <input type="checkbox" name="it"/> IT <input type="checkbox" name="office" checked/> Office
            </form></body></html>
            """);

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("form.pdf")), Source = new PdfHtmlSource(new SourceFile("--from-html", input), AllowNetworkResources: false) });

        using var document = new Document(result.Output.Path);
        CheckboxField[] boxes = [.. document.Form.Fields.OfType<CheckboxField>()];
        Assert.Equal(2, boxes.Length);
        Assert.All(boxes, static box => Assert.All(box.AllowedStates, state =>
            Assert.Contains(box.Appearance[$"N.{state}"].Contents, static drawn => drawn is Aspose.Pdf.Operators.ClosePathStroke or Aspose.Pdf.Operators.Stroke)));
    }

    [Fact]
    public void Create_FromHtml_SaysWhichFieldsAreNamedAfterTheirIdAndWhichHaveGeneratedNames()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("agree.html");
        File.WriteAllText(input, """
            <html><body><form>
            <p><input type="checkbox" id="agree" name="agree" value="yes"/> I agree</p>
            <textarea id="notes-box" name="notes"></textarea>
            <p><input type="radio" name="kind" value="a"/> A <input type="radio" name="kind" value="b"/> B</p>
            </form></body></html>
            """);

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("agree.pdf")), Source = new PdfHtmlSource(new SourceFile("--from-html", input), AllowNetworkResources: false) });

        string message = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.LossyConversion).Message;
        Assert.Contains("named after their HTML id attribute and lost their HTML values: 'agree', 'notes-box'", message, StringComparison.Ordinal);
        Assert.Contains("have generated names and lost their HTML values: 'radio'", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The importer drops image and hidden inputs without a field (PDF-HTML-FORM-INPUTS); the
    /// warning names both types and says how to place the image.
    /// </summary>
    [Fact]
    public void Create_FromHtml_NamesTheDroppedImageAndHiddenInputs()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("onboarding.html");
        File.WriteAllText(input, """
            <html><body><form>
            <input type="text" name="employee"/>
            <input type="image" name="photo" src="photo.png" alt="Photo"/>
            <input type="hidden" name="employee_id" value="E-1024"/>
            </form></body></html>
            """);
        string output = fixture.File("onboarding.pdf");

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(output), Source = new PdfHtmlSource(new SourceFile("--from-html", input), AllowNetworkResources: false) });

        Warning lossy = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.Contains("dropped 2 input(s) of type image, hidden", lossy.Message, StringComparison.Ordinal);
        Assert.Contains("add_stamp_image", lossy.Hint, StringComparison.Ordinal);
        using var created = new Document(output);
        Assert.Equal(["employee"], created.Form.Fields.Select(static field => field.FullName));
    }

    /// <summary>
    /// A Markdown line that ends in two spaces breaks the line (a hard break, PDF-8); a line
    /// without them joins the next one, as CommonMark reads a soft break.
    /// </summary>
    [Fact]
    public void Create_FromMarkdown_BreaksALineThatEndsInTwoSpaces()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("breaks.md");
        File.WriteAllText(input, "编制：销售运营部 / Sales Operations  \n日期：2026-10-12\n\nSoft first\nsoft second\n");

        PdfCreateResult created = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("breaks.pdf")), Source = new PdfMarkdownSource(new SourceFile("--from-markdown", input)) });

        // Measured by where the words lie, which an evaluation notice drawn over them leaves alone.
        using var document = new Document(created.Output.Path);
        Assert.True(Top(document, "Operations") - Top(document, "2026-10-12") > 5, "the hard break joined the lines");
        Assert.Equal(Top(document, "first"), Top(document, "second"), 1d);

        static double Top(Document document, string word)
        {
            var absorber = new TextFragmentAbsorber(word);
            document.Pages[1].Accept(absorber);
            return absorber.TextFragments[1].Rectangle.URY;
        }
    }

    [Fact]
    public void Create_FromMarkdown_InventsNoMetadata()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("notes.md");
        File.WriteAllText(input, "# Notes\n\nBody text.\n");

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("notes.pdf")), Source = new PdfMarkdownSource(new SourceFile("--from-markdown", input)) });

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = result.Output.Path, Details = ["properties"] });
        Assert.Null(info.Properties!["title"]);
        Assert.Null(info.Properties["author"]);
        Assert.Null(info.Properties["subject"]);
    }

    [Fact]
    public void Create_FromImages_FitsEachImageUndistortedOnAPageInItsOrientation()
    {
        using var fixture = new PdfEngineFixture();
        string landscape = fixture.File("landscape.bmp");
        File.WriteAllBytes(landscape, Bitmap(200, 100));
        string portrait = fixture.File("portrait.svg");
        File.WriteAllText(portrait, """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="300"><rect width="100" height="300" fill="#246"/></svg>""");

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest
        {
            Output = TestOutput.At(fixture.File("images.pdf")),
            Source = new PdfImageSource([new SourceFile("--from-images", landscape), new SourceFile("--from-images", portrait)]),
            Margins = new PdfMargins(36, 36, 36, 36),
        });

        using var document = new Document(result.Output.Path);
        // A4 turns to landscape for the wide image; each image fills the margin box in one
        // dimension, keeps its aspect ratio and is centred in the other.
        AssertPlaced(document.Pages[1], 842, 595, 2d);
        AssertPlaced(document.Pages[2], 595, 842, 1d / 3);

        static void AssertPlaced(Page page, double width, double height, double ratio)
        {
            Assert.Equal(width, page.Rect.Width, 0);
            Assert.Equal(height, page.Rect.Height, 0);
            Rectangle placed = PlacedImage(page);
            Assert.Equal(ratio, placed.Width / placed.Height, 2);
            Assert.True(
                Math.Abs(placed.Width - (width - 72)) < 1 || Math.Abs(placed.Height - (height - 72)) < 1,
                $"The image {placed.Width}x{placed.Height} does not fill the margin box of {width}x{height}.");
            Assert.Equal(width / 2, (placed.LLX + placed.URX) / 2, 0);
            Assert.Equal(height / 2, (placed.LLY + placed.URY) / 2, 0);
        }
    }

    /// <summary>
    /// A raster image enlarged to fewer than 96 of its pixels per inch of the page warns that it
    /// shows blurred, naming its page and size; an image with pixels enough, or a vector image,
    /// does not.
    /// </summary>
    [Fact]
    public void Create_FromImages_WarnsOfAnImageEnlargedBeyondItsResolution()
    {
        using var fixture = new PdfEngineFixture();
        string small = fixture.File("icon.bmp");
        File.WriteAllBytes(small, Bitmap(40, 30));
        string large = fixture.File("scan.bmp");
        File.WriteAllBytes(large, Bitmap(1200, 900));
        string vector = fixture.File("chart.svg");
        File.WriteAllText(vector, """<svg xmlns="http://www.w3.org/2000/svg" width="40" height="30"><rect width="40" height="30" fill="#246"/></svg>""");

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest
        {
            Output = TestOutput.At(fixture.File("mixed-resolution.pdf")),
            Source = new PdfImageSource([new SourceFile("--from-images", large), new SourceFile("--from-images", small), new SourceFile("--from-images", vector)]),
        });

        Warning warning = Assert.Single(result.Warnings!);
        Assert.Equal("IMAGE_RESOLUTION_LOW", warning.Code.Name);
        Assert.Equal("page 2", warning.Location);
        Assert.Contains("icon.bmp", warning.Message, StringComparison.Ordinal);
        Assert.Contains("40 x 30 pixels", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_FromImages_ChecksTheMarginsForTheOrientationOfEachPage()
    {
        using var fixture = new PdfEngineFixture();
        string portrait = fixture.File("portrait.bmp");
        File.WriteAllBytes(portrait, Bitmap(100, 200));
        string landscape = fixture.File("landscape.bmp");
        File.WriteAllBytes(landscape, Bitmap(200, 100));
        // 600 points of top and bottom margin fit A4 portrait (842 high), not landscape (595).
        var margins = new PdfMargins(300, 36, 300, 36);

        PdfCreateResult created = PdfCreate.Run(fixture.Session, new PdfCreateRequest
        {
            Output = TestOutput.At(fixture.File("portrait.pdf")),
            Source = new PdfImageSource([new SourceFile("--from-images", portrait)]),
            Margins = margins,
        });
        CliException refused = Assert.Throws<CliException>(() => PdfCreate.Run(fixture.Session, new PdfCreateRequest
        {
            Output = TestOutput.At(fixture.File("mixed.pdf")),
            Source = new PdfImageSource([new SourceFile("--from-images", portrait), new SourceFile("--from-images", landscape)]),
            Margins = margins,
        }));

        Assert.True(File.Exists(created.Output.Path));
        Assert.Equal(ErrorCodes.OptionInvalid, refused.Code);
        Assert.Contains("landscape.bmp", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.File("mixed.pdf")));
    }

    /// <summary>
    /// An image that fills its margin box in one dimension, such as a scan with the exact
    /// proportions of its page, stays on its one page, centred in the margin box; the CLI's
    /// --timeout stops the command if the layout keeps moving the image to a new page. Without
    /// margins the room for the image equals the image.
    /// </summary>
    [Theory]
    [InlineData(210, 297, "a4", "36")]
    [InlineData(297, 210, "a4", "36")]
    [InlineData(216, 279, "letter", "36")]
    [InlineData(100, 300, "a4", "36")]
    [InlineData(210, 297, "a4", "0")]
    [InlineData(297, 210, "a4", "0")]
    [InlineData(216, 279, "letter", "0")]
    [InlineData(279, 216, "letter", "0")]
    [InlineData(210, 297, "a4", "10,50,90,20")]
    [InlineData(300, 100, "a4", "10,50,90,20")]
    [InlineData(100, 300, "letter", "10,50,90,20")]
    public void Create_FromImages_KeepsAnImageThatFillsItsMarginBoxOnOnePage(int width, int height, string pageSize, string margins)
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string image = fixture.File("scan.bmp");
        File.WriteAllBytes(image, Bitmap(width, height));
        string output = fixture.File("scan.pdf");

        CliResult created = workspace.Run(
            ["pdf", "create", output, "--from-images", image, "--page-size", pageSize, "--margins", margins,
                "--timeout", TestDeadline.CliTimeout, "--output-format", "json"]);

        Assert.True(created.ExitCode == 0, created.StdOut + created.StdErr);
        double[] sides = margins.Split(',').Select(static side => double.Parse(side, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        (double top, double right, double bottom, double left) = sides.Length == 1
            ? (sides[0], sides[0], sides[0], sides[0])
            : (sides[0], sides[1], sides[2], sides[3]);
        using var document = new Document(output);
        Page page = Assert.Single(document.Pages);
        Rectangle placed = PlacedImage(page);
        double boxWidth = page.Rect.Width - left - right;
        double boxHeight = page.Rect.Height - top - bottom;
        Assert.Equal((double)width / height, placed.Width / placed.Height, 2);
        Assert.True(
            Math.Abs(placed.Width - boxWidth) < 0.5 || Math.Abs(placed.Height - boxHeight) < 0.5,
            $"{placed.Width}x{placed.Height} does not fill the {boxWidth}x{boxHeight} margin box in either dimension.");
        Assert.True(placed.Width < boxWidth + 0.5 && placed.Height < boxHeight + 0.5,
            $"{placed.Width}x{placed.Height} exceeds the {boxWidth}x{boxHeight} margin box.");
        Assert.Equal(left + boxWidth / 2, (placed.LLX + placed.URX) / 2, 0);
        Assert.Equal(bottom + boxHeight / 2, (placed.LLY + placed.URY) / 2, 0);
    }

    [Theory]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" width="300" height="100"><rect width="300" height="100" fill="#246"/></svg>""", 3d)]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 300"><rect width="100" height="300" fill="#246"/></svg>""", 1d / 3)]
    public void Create_FromImages_SizesAnSvgByItsOwnProportions(string svg, double ratio)
    {
        using var fixture = new PdfEngineFixture();
        string image = fixture.File("image.svg");
        File.WriteAllText(image, svg);

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("svg.pdf")), Source = new PdfImageSource([new SourceFile("--from-images", image)]) });

        using var document = new Document(result.Output.Path);
        Page page = document.Pages[1];
        Assert.Equal(ratio > 1, page.Rect.Width > page.Rect.Height);
        Rectangle placed = PlacedImage(page);
        Assert.Equal(ratio, placed.Width / placed.Height, 2);
    }

    /// <summary>
    /// A camera stores a portrait photo as landscape pixels with an EXIF orientation that turns
    /// it a quarter; the engine draws it turned, so its page is portrait.
    /// </summary>
    [Fact]
    public void Create_FromImages_TurnsThePageForAnExifOrientedPhoto()
    {
        using var fixture = new PdfEngineFixture();
        string photo = fixture.File("photo.jpg");
        File.WriteAllBytes(photo, OrientedJpeg(200, 100, orientation: 6));

        PdfCreateResult result = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("photo.pdf")), Source = new PdfImageSource([new SourceFile("--from-images", photo)]) });

        using var document = new Document(result.Output.Path);
        Page page = document.Pages[1];
        Assert.True(page.Rect.Height > page.Rect.Width, $"The page is {page.Rect.Width}x{page.Rect.Height}.");
        Rectangle placed = PlacedImage(page);
        Assert.Equal(0.5, placed.Width / placed.Height, 2);
    }

    /// <summary>
    /// A PDF made from scans with the default margins is still recognized as scanned: each image
    /// keeps its proportions inside the margins, so it covers well under 80% of the page.
    /// </summary>
    [Fact]
    public void Create_FromImages_WithDefaultMarginsReadsAsScannedPages()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string[] images = [fixture.File("a4.bmp"), fixture.File("wide.bmp"), fixture.File("square.bmp"), fixture.File("tall.bmp")];
        File.WriteAllBytes(images[0], Bitmap(210, 297));
        File.WriteAllBytes(images[1], Bitmap(300, 100));
        File.WriteAllBytes(images[2], Bitmap(100, 100));
        File.WriteAllBytes(images[3], Bitmap(100, 300));
        PdfCreateResult created = PdfCreate.Run(fixture.Session, new PdfCreateRequest { Output = TestOutput.At(fixture.File("scans.pdf")), Source = new PdfImageSource([.. images.Select(static path => new SourceFile("--from-images", path))]) });

        PdfReadResult read = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = created.Output.Path });
        CliResult review = workspace.Run(["review", created.Output.Path, "--out", workspace.File("review"), "--output-format", "json"]);

        Assert.Equal([1, 2, 3, 4], read.ScannedPagesSuspected);
        Assert.Contains(read.Warnings!, static warning => warning.Code.Name == "SCANNED_PAGES_SUSPECTED");
        Assert.Equal(
            ["page 1", "page 2", "page 3", "page 4"],
            review.Json()["findings"]!.AsArray()
                .Where(static item => item!["code"]!.GetValue<string>() == "PDF_PAGE_WITHOUT_TEXT_LAYER")
                .Select(static item => item!["location"]!.GetValue<string>()));
    }

    /// <summary>
    /// A scan saved in evaluation mode carries the evaluation notice as its only text and is
    /// still read as a scan; a page with text of its own under the same image is not.
    /// </summary>
    [Fact]
    public void Read_CountsAPageWhoseOnlyTextIsTheEvaluationNoticeAsScanned()
    {
        using var fixture = new PdfEngineFixture();
        using var workspace = new TempWorkspace();
        string scan = fixture.CreateRawDocument("scan.pdf", pages: 1,
            textPages: new HashSet<int>(), imagePages: new HashSet<int> { 1 }, imagePoints: 700);
        string text = fixture.CreateRawDocument("text.pdf", pages: 1,
            imagePages: new HashSet<int> { 1 }, imagePoints: 700);
        string merged = fixture.File("evaluation.pdf");

        CliResult created = workspace.Run(
            ["pdf", "merge", scan, text, "--out", merged, "--license-mode", "evaluation", "--output-format", "json"]);
        PdfReadResult read = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = merged });

        Assert.True(created.ExitCode == 0, created.StdOut + created.StdErr);
        Assert.Contains("Evaluation Only", read.Pages[0].Text, StringComparison.Ordinal);
        Assert.Equal([1], read.ScannedPagesSuspected);
    }

    /// <summary>The rectangle of the one image placed on <paramref name="page"/>.</summary>
    private static Rectangle PlacedImage(Page page)
    {
        var absorber = new ImagePlacementAbsorber();
        page.Accept(absorber);
        return Assert.Single(absorber.ImagePlacements).Rectangle;
    }

    /// <summary>A JPEG of landscape pixels with an EXIF orientation tag.</summary>
    private static byte[] OrientedJpeg(int width, int height, byte orientation)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height);
        bitmap.Erase(SkiaSharp.SKColors.SteelBlue);
        byte[] jpeg = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90).ToArray();
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    private static byte[] Bitmap(int width, int height)
    {
        int stride = (width * 3 + 3) & ~3;
        byte[] bytes = new byte[54 + stride * height];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        BitConverter.GetBytes(stride * height).CopyTo(bytes, 34);
        Array.Fill(bytes, (byte)0x80, 54, stride * height);
        return bytes;
    }

    [Theory]
    [InlineData("D:20261003100000+05'30'", "2026-10-03T04:30:00Z")]
    [InlineData("D:20261003011908Z00'00'", "2026-10-03T01:19:08Z")]
    [InlineData("D:20260930225834-07'00'", "2026-10-01T05:58:34Z")]
    [InlineData("D:20261003", "2026-10-03T00:00:00")]
    [InlineData("3 October 2026", "3 October 2026")]
    public void Info_ReadsADocumentDateAtTheOffsetItStates(string stored, string expected)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 1);
        using (var document = new Document(path))
        {
            document.Info["CreationDate"] = stored;
            document.Save(path);
        }

        PdfInspectResult result = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["properties"] });

        Assert.Equal(expected, result.Properties!["creationDate"]);
    }

    [Fact]
    public void Info_ProjectsAllM1Details()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 2);
        using (var document = new Document(path))
        {
            document.Pages[2].SetPageSize(792, 612);
            document.PageLabels.UpdateLabel(0, new PageLabel
            {
                NumberingStyle = NumberingStyle.NumeralsRomanLowercase,
                Prefix = "A-",
                StartingValue = 1,
            });
            var first = new OutlineItemCollection(document.Outlines)
            {
                Title = "First page",
                Destination = new FitExplicitDestination(document.Pages[1]),
            };
            first.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Website",
                Action = new GoToURIAction("https://example.com/"),
            });
            document.Outlines.Add(first);
            document.Save(path);
        }

        var result = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = path,
            IncludePreview = true,
            Details = ["outline", "links", "forms", "attachments", "fonts", "permissions", "signatures", "layers", "properties"],
        });

        Assert.Equal("pdf", result.Input.Format);
        Assert.Equal(2, result.Pdf.PageCount);
        Assert.Null(result.Pdf.FormType);
        Assert.Equal(2, result.Pdf.DistinctPageSizes.Count);
        Assert.Equal(2, result.Pages!.Count);
        Assert.Collection(
            result.PageLabels!,
            label =>
            {
                Assert.Equal(1, label.StartPage);
                Assert.Equal("roman-lower", label.NumberStyle);
                Assert.Equal("A-", label.Prefix);
                Assert.Equal(1, label.StartingValue);
            });
        Assert.Collection(
            result.Outline!,
            item =>
            {
                Assert.Equal("First page", item.Title);
                Assert.Equal("1", item.Bookmark);
                Assert.Equal(1, item.Page);
            },
            item =>
            {
                Assert.Equal(2, item.Level);
                Assert.Equal("1/1", item.Bookmark);
                Assert.Null(item.Page);
            });
        Assert.Null(result.Forms!.Kind);
        Assert.Null(result.Pdf.FormType);
        Assert.Empty(result.Forms.Fields);
        Assert.Empty(result.Attachments!);
        Assert.NotEmpty(result.Fonts!);
        Assert.True(result.Permissions!.OwnerAccess);
        Assert.Empty(result.Signatures!);
        Assert.Empty(result.Layers!);
        Assert.Contains("producer", result.Properties!.Keys);
    }

    [Fact]
    public void Info_ListsTheFirstFormFieldsWithTheirValuesAndSaysWhenThereAreMore()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 1);
        using (var document = new Document(path))
        {
            for (int index = 0; index <= PdfInspectResult.FieldLimit; index++)
            {
                var field = new TextBoxField(document.Pages[1], new Rectangle(10, 10, 60, 20))
                {
                    PartialName = $"field{index:0000}",
                    Value = $"value {index}",
                };
                document.Form.Add(field, 1);
            }

            document.Save(path);
        }

        PdfInspectResult result = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = [PdfInspectDetails.Forms] });

        Assert.Equal(PdfInspectResult.FieldLimit + 1, result.Forms!.FieldCount);
        Assert.Equal(PdfInspectResult.FieldLimit, result.Forms.Fields.Count);
        Assert.Equal(("field0000", "text", "value 0"), (result.Forms.Fields[0].Name, result.Forms.Fields[0].Kind, result.Forms.Fields[0].Value));
        Warning truncated = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.ListTruncated);
        Assert.Equal("forms.fields", truncated.Location);
        Assert.Contains($"{PdfInspectResult.FieldLimit + 1}", truncated.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("layout")]
    public void Read_ProjectsSelectedPagesInBothContents(string mode)
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 3);

        var result = PdfRead.Run(fixture.Session, new PdfReadRequest
        {
            Input = path,
            Pages = CliPageRange.Parse("2-3"),
            Content = mode,
            MaxCharacters = 20_000,
        });

        Assert.Equal(mode, result.Content);
        Assert.Equal(3, result.PageCount);
        Assert.Equal(new ResultWindow { Unit = "page", Returned = 2, Total = 2, Truncated = false }, result.Window);
        Assert.Contains("Portable PDF page 2", result.Pages[0].Text, StringComparison.Ordinal);
        Assert.Contains("Portable PDF page 3", result.Pages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_StopsAtCharacterBudgetAndMarksTheCutPage()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateDocument(pages: 3);

        var result = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = path, MaxCharacters = 24 });

        Assert.True(result.Window!.Truncated);
        Assert.Equal(24, result.Pages.Sum(static page => page.Text.Length));
        Assert.True(result.Pages[^1].Truncated);
    }

    [Fact]
    public void Passwords_DistinguishRequiredInvalidUserAndOwnerAccess()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("protected.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Protected"));
            document.Encrypt(
                "reader-secret",
                "owner-secret",
                DocumentPrivilege.ForbidAll,
                CryptoAlgorithm.AESx256,
                usePdf20: false);
            document.Save(path);
        }

        CliException required = Assert.Throws<CliException>(
            () => PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path }));
        Assert.Equal(ErrorCodes.PasswordRequired, required.Code);
        CliException invalid = Assert.Throws<CliException>(
            () => PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Password = new Secret("wrong") }));
        Assert.Equal(ErrorCodes.PasswordInvalid, invalid.Code);

        var user = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = path,
            Password = new Secret("reader-secret"),
            Details = ["permissions"],
        });
        Assert.Equal("user", user.Pdf.PasswordType);
        Assert.False(user.Permissions!.OwnerAccess);
        Assert.False(user.Permissions.Copy);

        var owner = PdfInfo.Run(fixture.Session, new PdfInspectRequest
        {
            Input = path,
            Password = new Secret("owner-secret"),
            Details = ["permissions"],
        });
        Assert.Equal("owner", owner.Pdf.PasswordType);
        Assert.True(owner.Permissions!.OwnerAccess);
        // The permissions are the ones the file grants readers, whatever password opened it.
        Assert.False(owner.Permissions.Copy);
    }

    [Fact]
    public void Passwords_OwnerOnlyFixtureOpensNormallyAndRecognizesOwnerAccess()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateEncryptedDocument(
            string.Empty,
            "owner-secret",
            "owner-password-only.pdf");

        var normal = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["permissions"] });
        var owner = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Password = new Secret("owner-secret"), Details = ["permissions"] });

        Assert.True(normal.Pdf.Encrypted);
        // The file has only an owner password, so it opened without one.
        Assert.Null(normal.Pdf.PasswordType);
        Assert.False(normal.Permissions!.HasOpenPassword);
        Assert.True(normal.Permissions.HasOwnerPassword);
        Assert.False(normal.Permissions.OwnerAccess);
        Assert.Equal("owner", owner.Pdf.PasswordType);
        Assert.True(owner.Permissions!.OwnerAccess);
        Assert.Equal(normal.Permissions with { OwnerAccess = true }, owner.Permissions);
    }

    [Fact]
    public void Load_RejectsRenamedNonPdfBytes()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("renamed.pdf");
        File.WriteAllText(path, "not really a PDF");

        CliException exception = Assert.Throws<CliException>(
            () => PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path }));

        Assert.Equal(ErrorCodes.FileCorrupt, exception.Code);
    }

    [Fact]
    public void Read_OneHundredPageFixtureRemainsWindowed()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateRawDocument("hundred-pages.pdf", pages: 100);

        var result = PdfRead.Run(fixture.Session, new PdfReadRequest { Input = path, MaxCharacters = 1 });

        Assert.Equal(new ResultWindow { Unit = "page", Returned = 1, Total = 100, Truncated = true }, result.Window);
        Assert.Single(result.Pages);
    }

    [LicensedFact]
    public void Info_DisclosesThePagePreviewCap()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.CreateRawDocument("many-pages.pdf", pages: 21);

        PdfInspectResult info = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, IncludePreview = true });

        Assert.Equal(20, info.Pages!.Count);
        Warning warning = Assert.Single(info.Warnings!);
        Assert.Equal(WarningCodes.ListTruncated, warning.Code);
        Assert.Equal("pages", warning.Location);
        Assert.Contains("first 20 of 21", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_XfaIsExplicitlyFlaggedReadOnly()
    {
        using var fixture = new PdfEngineFixture();
        string path = fixture.File("xfa.pdf");
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 700, 200, 730))
            {
                PartialName = "seed",
            });
            var xfa = new System.Xml.XmlDocument();
            xfa.LoadXml(
                """
                <xdp:xdp xmlns:xdp="http://ns.adobe.com/xdp/">
                  <template xmlns="http://www.xfa.org/schema/xfa-template/3.3/">
                    <subform name="form1"/>
                  </template>
                  <datasets xmlns="http://www.xfa.org/schema/xfa-data/1.0/">
                    <xfa:data xmlns:xfa="http://www.xfa.org/schema/xfa-data/1.0/"/>
                  </datasets>
                </xdp:xdp>
                """);
            document.Form.AssignXfa(xfa);
            document.Save(path);
        }

        var result = PdfInfo.Run(fixture.Session, new PdfInspectRequest { Input = path, Details = ["forms"] });

        Assert.Equal("xfa", result.Pdf.FormType);
        Assert.Equal("xfa", result.Forms!.Kind);
        Assert.True(result.Forms.ReadOnly);
    }

    [Fact]
    public void Convert_HtmlIsOneAtomicSelfContainedArtifact()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("html-source.pdf", 1);
        string output = fixture.File("self-contained.html");

        var result = PdfConvert.Run(fixture.Session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "html"),
        });

        Assert.Single(result.Outputs);
        Assert.True(File.Exists(output));
        Assert.Equal(
            ["html-source.pdf", "self-contained.html"],
            Directory.GetFiles(fixture.Temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Convert_HtmlTakesThePdfTitleAndSaysHowToCheckIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("titled.pdf", 1);
        using (var document = new Document(input))
        {
            document.Info.Title = "2026年10月 <发票> & 归档";
            document.Save(input);
        }
        string output = fixture.File("titled.html");

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest { Input = input, Output = TestOutput.At(output, format: "html") });

        Warning lossy = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.LossyConversion);
        Assert.StartsWith("Open the HTML in a browser", lossy.Hint, StringComparison.Ordinal);
        System.Text.RegularExpressions.Match title = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(output), "<title>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(title.Success);
        Assert.Equal("2026年10月 <发票> & 归档", System.Net.WebUtility.HtmlDecode(title.Groups[1].Value).Trim());
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("2-3", 2)]
    public void Convert_DocumentFormatsKeepTheDocumentProperties(string? pages, int pageCount)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("titled.pdf", 3);
        using (var document = new Document(input))
        {
            document.Info.Title = "Quarterly title";
            document.Info.Subject = "Quarterly subject";
            document.Save(input);
        }
        string output = fixture.File("titled.xps");

        PdfConvert.Run(fixture.Session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "xps"),
            Pages = pages is null ? null : CliPageRange.Parse(pages),
        });

        using var package = System.IO.Compression.ZipFile.OpenRead(output);
        using var core = new StreamReader(package.GetEntry("docProps/core.xml")!.Open());
        string properties = core.ReadToEnd();
        Assert.Contains("Quarterly title", properties, StringComparison.Ordinal);
        Assert.Contains("Quarterly subject", properties, StringComparison.Ordinal);
        Assert.Equal(pageCount, package.Entries.Count(static entry => entry.FullName.EndsWith(".fpage", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("1-2", true)]
    [InlineData("1-3", false)]
    public void Convert_DocumentFormatsDiscloseNavigationToUnselectedPages(string pages, bool degraded)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("chapters.pdf", 3);
        using (var document = new Document(input))
        {
            document.Outlines.Add(new OutlineItemCollection(document.Outlines)
            {
                Title = "Appendix",
                Destination = new FitExplicitDestination(document.Pages[3]),
            });
            document.Save(input);
        }

        PdfConvertResult result = PdfConvert.Run(fixture.Session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("chapters.docx"), format: "docx"),
            Pages = CliPageRange.Parse(pages),
        });

        Warning? warning = result.Warnings?.SingleOrDefault(static item => item.Code.Name == "NAVIGATION_DEGRADED");
        Assert.Equal(degraded, warning is not null);
        if (warning is not null)
        {
            Assert.StartsWith("1 bookmark(s), 0 link(s)", warning.Message, StringComparison.Ordinal);
            Assert.Contains("--pages did not select", warning.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    [InlineData("epub")]
    [InlineData("txt")]
    [InlineData("md")]
    [InlineData("xps")]
    [InlineData("pdfa-1b")]
    [InlineData("pdfa-2b")]
    [InlineData("pdfa-3b")]
    [InlineData("tiff")]
    public void Convert_ProducesEveryFrozenM1Target(string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 1);
        string output = fixture.File("output" + PdfFormats.Definitions.ExtensionOf(format));

        var result = fixture.Disclosed(session => PdfConvert.Run(session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: format),
        }));

        Assert.NotEmpty(result.Outputs);
        Assert.All(result.Outputs, item =>
        {
            Assert.True(File.Exists(item.Path), item.Path);
            Assert.True(item.SizeBytes > 0);
        });
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains(result.Warnings!, static warning => warning.Code == WarningCodes.EvaluationMode);
        }
    }

    /// <summary>An image output without an extension is refused before anything is read or written.</summary>
    [Theory]
    [InlineData("render", "png")]
    [InlineData("convert", "tiff")]
    public void ImageOutput_WithoutAnExtensionIsRefused(string command, string format)
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument(pages: 2);
        using var workspace = new TempWorkspace();
        File.Copy(input, workspace.File("input.pdf"));

        CliResult refused = workspace.Run("pdf", command, "input.pdf", "--to", format, "--out", "pages", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        Assert.Contains("\"OPTION_INVALID\"", refused.StdErr, StringComparison.Ordinal);
        Assert.Contains("." + format, refused.StdErr, StringComparison.Ordinal);
        Assert.Equal(["input.pdf"], Directory.GetFileSystemEntries(workspace.Path).Select(Path.GetFileName));
    }
}
