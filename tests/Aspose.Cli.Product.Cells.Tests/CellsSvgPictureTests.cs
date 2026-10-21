using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// An SVG picture keeps only what it holds itself (CELLS-SVG-EGRESS): the images it names by a
/// path or URL never reach an output, and the images of an HTML or MHTML input are not imported.
/// Each sample names a solid green PNG outside the input's folder, so an output that holds green
/// pixels holds that file.
/// </summary>
public sealed class CellsSvgPictureTests : IDisposable
{
    // The warning code the change adds, by name, so the tests build against the code before it.
    private const string ImagesSkipped = "IMAGES_SKIPPED";
    private static readonly SKColor Secret = new(0, 200, 0);
    private static readonly SKColor Swapped = new(200, 0, 200);
    private readonly TempDirectory _outside = new();
    private readonly string _secret;

    public CellsSvgPictureTests()
    {
        _secret = _outside.File("secret.png");
        File.WriteAllBytes(_secret, Png(Secret));
    }

    public void Dispose() => _outside.Dispose();

    /// <summary>
    /// An ODS whose picture is an SVG without a raster image: the engine rasterizes it when it
    /// loads the workbook and reads the PNG its image names by a bare path.
    /// </summary>
    [Fact]
    public void LoadedSvgPicture_NeverPutsTheFileItNamesIntoRenderPdfOrReview()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllBytes(workspace.File("book.ods"), OdsWithPicture(Svg(Image(_secret))));

        CliResult render = workspace.Run("cells", "render", "book.ods", "--out", "book.png", "--output-format", "json");
        Assert.True(render.ExitCode == 0, render.StdErr);
        Assert.False(HasSecretPixels(workspace.File("book.png")), "the render holds the file the SVG names");
        Assert.True(Warned(render.StdOut, WarningCodes.RemoteResourcesBlocked.Name), render.StdOut);

        CliResult pdf = workspace.Run("cells", "convert", "book.ods", "--to", "pdf", "--out", "book.pdf", "--output-format", "json");
        Assert.True(pdf.ExitCode == 0, pdf.StdErr);
        Assert.False(PdfHasSecretPixels(workspace.File("book.pdf")), "the PDF holds the file the SVG names");
        Assert.True(Warned(pdf.StdOut, WarningCodes.RemoteResourcesBlocked.Name), pdf.StdOut);

        CliResult review = workspace.Run("review", "book.ods", "--out", "review", "--output-format", "json");
        string[] images = Directory.GetFiles(workspace.File("review"), "*.png", SearchOption.AllDirectories);
        Assert.True(images.Length > 0, review.StdOut + review.StdErr);
        Assert.All(images, image => Assert.False(HasSecretPixels(image), $"{image} holds the file the SVG names"));
        Assert.True(Warned(review.StdOut, WarningCodes.RemoteResourcesBlocked.Name), review.StdOut);
    }

    /// <summary>
    /// An xlsx SVG picture that names a file outside the input's folder: the engine draws that
    /// file again when it renders, so a file changed after the workbook was saved would show its
    /// new content. The render shows neither the old nor the new content.
    /// </summary>
    [LicensedFact]
    public void XlsxSvgPicture_RenderNeverDrawsTheFileItNames()
    {
        using var fixture = new CellsFixture();
        using var workspace = new TempWorkspace();
        using (var workbook = new Workbook())
        {
            using var svg = new MemoryStream(Svg(Image(_secret)), writable: false);
            workbook.Worksheets[0].Pictures.Add(1, 1, svg);
            workbook.Save(workspace.File("book.xlsx"));
        }
        File.WriteAllBytes(_secret, Png(Swapped));

        CliResult render = workspace.Run("cells", "render", "book.xlsx", "--out", "book.png", "--output-format", "json");

        Assert.True(render.ExitCode == 0, render.StdErr);
        Assert.False(HasPixels(File.ReadAllBytes(workspace.File("book.png")), IsSwapped), "the render drew the file the SVG names as it is now");
        Assert.False(HasSecretPixels(workspace.File("book.png")), "the render drew the file the SVG names as it was");
        Assert.True(Warned(render.StdOut, WarningCodes.RemoteResourcesBlocked.Name), render.StdOut);
    }

    /// <summary>
    /// A grouped SVG picture that names a file is left out of its group; the group and its other
    /// shapes stay.
    /// </summary>
    [LicensedFact]
    public void Load_LeavesOutAGroupedSvgPictureThatNamesAFileAndKeepsTheGroup()
    {
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("grouped.xlsx");
        using (var workbook = new Workbook())
        {
            Worksheet sheet = workbook.Worksheets[0];
            Aspose.Cells.Drawing.Shape picture = sheet.Shapes.AddSvg(1, 0, 1, 0, 40, 40, Svg(Image(_secret)), null);
            Aspose.Cells.Drawing.Shape first = sheet.Shapes.AddRectangle(1, 0, 3, 0, 40, 40);
            Aspose.Cells.Drawing.Shape second = sheet.Shapes.AddRectangle(1, 0, 5, 0, 40, 40);
            first.Name = "First";
            second.Name = "Second";
            sheet.Shapes.Group([picture, first, second]);
            workbook.Save(input);
        }

        using LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(input, null);

        Worksheet loadedSheet = loaded.Workbook.Worksheets[0];
        Assert.Empty(loadedSheet.Pictures);
        Aspose.Cells.Drawing.GroupShape group = Assert.Single(loadedSheet.Shapes.OfType<Aspose.Cells.Drawing.GroupShape>());
        Assert.Equal(["First", "Second"], group.GetGroupedShapes().Select(static shape => shape.Name).Order());
        Assert.Contains(loaded.Warnings() ?? [], warning => warning.Code.Name == ImagesSkipped && warning.AffectsCompleteness);
        loaded.Workbook.Save(fixture.Temp.File("grouped-out.xlsx"));
    }

    /// <summary>A loaded SVG picture that does not parse as XML is left out, and the result says so.</summary>
    [Fact]
    public void LoadedSvgPicture_ThatDoesNotParseIsLeftOut()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllBytes(workspace.File("book.ods"), OdsWithPicture(Svg(Image(_secret) + "<g>")));

        CliResult converted = workspace.Run("cells", "convert", "book.ods", "--to", "xlsx", "--out", "book.xlsx", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.True(Warned(converted.StdOut, ImagesSkipped), converted.StdOut);
        Assert.False(XlsxHasSecretPixels(workspace.File("book.xlsx")), "the workbook holds the file the SVG names");
    }

    /// <summary>
    /// HTML and MHTML input keeps no images, so nothing an SVG data URI names, a file or an
    /// address, reaches the workbook, and the result says the images were left out. The engine
    /// still requests an http image while it loads, which this check does not cover.
    /// </summary>
    [Theory]
    [InlineData("html", "path")]
    [InlineData("html", "file-uri")]
    [InlineData("html", "http")]
    [InlineData("mht", "path")]
    [InlineData("mht", "file-uri")]
    [InlineData("mht", "http")]
    public async Task HtmlInput_KeepsNoImagesSoNothingAnSvgNamesReachesTheWorkbook(string extension, string reference)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        string href = reference switch
        {
            "path" => _secret,
            "file-uri" => new Uri(_secret).AbsoluteUri,
            _ => server.Url + "/svg-image.png",
        };
        string html = "<html><body><table><tr><td>Data</td><td><img width=\"40\" height=\"40\" src=\"data:image/svg+xml;base64,"
            + Convert.ToBase64String(Svg(Image(href))) + "\"></td></tr></table></body></html>";
        File.WriteAllText(workspace.File("page." + extension), extension == "mht" ? Mhtml(html) : html);

        CliResult converted = workspace.Run("cells", "convert", "page." + extension, "--to", "xlsx", "--out", "page.xlsx", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.DoesNotContain(XlsxImages(workspace.File("page.xlsx")), image => HasPixels(image, static _ => true));
        Assert.False(XlsxHasSecretPixels(workspace.File("page.xlsx")), "the workbook holds the file the SVG names");
        Assert.True(Warned(converted.StdOut, ImagesSkipped), converted.StdOut);
    }

    /// <summary>
    /// insert_image gives the engine the SVG without its references outside itself, the decoy
    /// that hid an address from the old check included, and says so.
    /// </summary>
    [Theory]
    [InlineData("path")]
    [InlineData("file-uri")]
    [InlineData("http")]
    [InlineData("decoy")]
    [InlineData("css")]
    [InlineData("disguised")]
    [InlineData("animation")]
    [InlineData("base")]
    public async Task InsertImage_InsertsAnSvgWithoutWhatItNamesOutsideItself(string reference)
    {
        using var workspace = new TempWorkspace();
        await using var server = new ResourceHttpServer();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
        string image = reference switch
        {
            "path" => Image(_secret),
            "file-uri" => Image(new Uri(_secret).AbsoluteUri),
            "http" => Image(server.Url + "/svg-image.png"),
            "decoy" => $"""<image alt='xmlns="' xlink:href='{server.Url}/decoy.png' title='"' width="40" height="40"/>""",
            // An SVG declared as a PNG.
            "disguised" => Image("data:image/png;base64," + Convert.ToBase64String(Svg(Image(_secret)))),
            "animation" => $"""<image width="40" height="40" xlink:href="#none"><set attributeName="xlink:href" to="{_secret}"/><animate attributeName="href" values="{new Uri(_secret).AbsoluteUri}"/></image>""",
            "base" => $"""<g xml:base="{new Uri(_outside.Path + Path.DirectorySeparatorChar).AbsoluteUri}">{Image("secret.png")}</g>""",
            _ => $"""<style>@import url("{server.Url}/style.css"); rect.f {"{"} fill: url("{new Uri(_secret).AbsoluteUri}") {"}"}</style><rect class="f" width="40" height="40" style="fill:url({server.Url}/fill.png)"/>""",
        };
        // The engine fails on an SVG that draws nothing, so each sample also draws a square.
        File.WriteAllBytes(workspace.File("logo.svg"), Svg("""<rect width="10" height="10" fill="blue"/>""" + image));

        CliResult edited = InsertImage(workspace, "logo.svg");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.True(server.RequestCount == 0, string.Join("; ", server.Requests));
        Assert.False(XlsxHasSecretPixels(workspace.File("result.xlsx")), "the workbook holds the file the SVG names");
        Assert.True(Warned(edited.StdOut, WarningCodes.RemoteResourcesBlocked.Name), edited.StdOut);
    }

    /// <summary>An SVG that names only its own fragments and raster data URIs is inserted as drawn, without a warning.</summary>
    [Fact]
    public void InsertImage_KeepsAnSvgThatNamesOnlyItself()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
        string blue = Convert.ToBase64String(Png(new SKColor(0, 0, 255)));
        File.WriteAllBytes(workspace.File("logo.svg"), Svg(
            $"""<defs><rect id="square" width="20" height="40" fill="#FF8000"/></defs><use xlink:href="#square"/><image x="20" width="20" height="40" xlink:href="data:image/png;base64,{blue}"/>"""));

        CliResult edited = InsertImage(workspace, "logo.svg");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.False(Warned(edited.StdOut, WarningCodes.RemoteResourcesBlocked.Name), edited.StdOut);
        byte[][] media = XlsxImages(workspace.File("result.xlsx"));
        Assert.Contains(media, png => HasPixels(png, color => color.Blue > 200 && color.Red < 60 && color.Green < 60));
        Assert.Contains(media, png => HasPixels(png, color => color.Red > 200 && color.Green is > 100 and < 160 && color.Blue < 60));
    }

    [Fact]
    public void InsertImage_RefusesAnSvgThatDoesNotParse()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheet-names", "Data").ExitCode);
        File.WriteAllText(workspace.File("logo.svg"),
            $"""<!DOCTYPE svg [<!ENTITY secret SYSTEM "{new Uri(_secret).AbsoluteUri}">]><svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><text>&secret;</text></svg>""");

        CliResult edited = InsertImage(workspace, "logo.svg");

        Assert.True(edited.ExitCode == 4, edited.StdErr);
        Assert.Equal("OPS_INVALID", JsonNode.Parse(edited.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(workspace.File("result.xlsx")));
    }

    /// <summary>
    /// A workbook whose SVG picture names a file keeps the picture, at its place and size, without
    /// the reference; one that names nothing outside itself is left as it is.
    /// </summary>
    [LicensedFact]
    public void Load_ReplacesAnSvgPictureThatNamesAFile()
    {
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("book.xlsx");
        using (var workbook = new Workbook())
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Shapes.AddSvg(2, 3, 1, 4, 60, 80, Svg(Image(_secret)), Png(new SKColor(128, 128, 128)));
            Aspose.Cells.Drawing.Picture picture = sheet.Pictures[0];
            picture.Name = "Logo";
            picture.AlternativeText = "Company logo";
            picture.Title = "Logo title";
            picture.Placement = Aspose.Cells.Drawing.PlacementType.FreeFloating;
            picture.RotationAngle = 15;
            Hyperlink link = picture.AddHyperlink("https://example.test/about");
            link.ScreenTip = "About us";
            sheet.Shapes.AddSvg(8, 0, 1, 0, 40, 40, Svg("""<rect width="40" height="40" fill="blue"/>"""), null);
            workbook.Save(input);
        }

        using LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(input, null);

        Aspose.Cells.Drawing.PictureCollection pictures = loaded.Workbook.Worksheets[0].Pictures;
        Assert.Equal(2, pictures.Count);
        Aspose.Cells.Drawing.Picture logo = pictures.Cast<Aspose.Cells.Drawing.Picture>().Single(picture => picture.Name == "Logo");
        Assert.DoesNotContain(_secret, Encoding.UTF8.GetString(logo.Data), StringComparison.Ordinal);
        Assert.Equal((2, 1, 80, 60, "Company logo"), (logo.UpperLeftRow, logo.UpperLeftColumn, logo.Width, logo.Height, logo.AlternativeText));
        Assert.Equal(("Logo title", Aspose.Cells.Drawing.PlacementType.FreeFloating, 15d), (logo.Title, logo.Placement, logo.RotationAngle));
        Assert.Equal(("https://example.test/about", "About us"), (logo.Hyperlink?.Address, logo.Hyperlink?.ScreenTip));
        Assert.Contains(loaded.Warnings() ?? [], warning => warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
    }

    private static CliResult InsertImage(TempWorkspace workspace, string image)
    {
        string ops = System.Text.Json.JsonSerializer.Serialize(new
        {
            ops = new[] { new { op = "insert_image", sheet = "Data", at = "B2", path = workspace.File(image) } },
        });
        return workspace.Run("cells", "edit", "source.xlsx", "--ops", ops, "--out", "result.xlsx", "--output-format", "json");
    }

    private static string Image(string href) => $"""<image xlink:href="{href}" width="40" height="40"/>""";

    private static byte[] Svg(string content) => Encoding.UTF8.GetBytes(
        $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40">{content}</svg>""");

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(40, 40);
        bitmap.Erase(color);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A minimal ODS whose one picture is the SVG, with no raster image beside it.</summary>
    private static byte[] OdsWithPicture(byte[] svg)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add("mimetype", "application/vnd.oasis.opendocument.spreadsheet"u8.ToArray(), CompressionLevel.NoCompression);
            Add("META-INF/manifest.xml", Encoding.UTF8.GetBytes("""
                <?xml version="1.0" encoding="UTF-8"?>
                <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2">
                <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.spreadsheet"/>
                <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/>
                <manifest:file-entry manifest:full-path="Pictures/logo.svg" manifest:media-type="image/svg+xml"/>
                </manifest:manifest>
                """), CompressionLevel.Optimal);
            Add("content.xml", Encoding.UTF8.GetBytes("""
                <?xml version="1.0" encoding="UTF-8"?>
                <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.2">
                <office:body><office:spreadsheet><table:table table:name="Sheet1"><table:table-row>
                <table:table-cell office:value-type="string"><text:p>Logo</text:p></table:table-cell>
                <table:table-cell><draw:frame draw:name="Logo" svg:x="0in" svg:y="0in" svg:width="0.5in" svg:height="0.5in"><draw:image xlink:href="Pictures/logo.svg" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/></draw:frame></table:table-cell>
                </table:table-row></table:table></office:spreadsheet></office:body></office:document-content>
                """), CompressionLevel.Optimal);
            Add("Pictures/logo.svg", svg, CompressionLevel.Optimal);

            void Add(string name, byte[] content, CompressionLevel level)
            {
                using Stream entry = zip.CreateEntry(name, level).Open();
                entry.Write(content);
            }
        }
        return buffer.ToArray();
    }

    private static string Mhtml(string html) =>
        "MIME-Version: 1.0\r\nContent-Type: multipart/related; boundary=svg-test\r\n\r\n"
        + "--svg-test\r\nContent-Type: text/html; charset=utf-8\r\n"
        + "Content-Transfer-Encoding: 8bit\r\n\r\n" + html
        + "\r\n--svg-test--\r\n";

    /// <summary>Whether the result names a warning with this code.</summary>
    private static bool Warned(string stdout, string code) =>
        JsonNode.Parse(stdout)!["warnings"] is JsonArray warnings
        && warnings.Any(warning => warning!["code"]!.GetValue<string>() == code);

    private static bool IsSecret(SKColor color) => color.Green > 150 && color.Red < 60 && color.Blue < 60;

    private static bool IsSwapped(SKColor color) => color.Red > 150 && color.Blue > 150 && color.Green < 60;

    private static bool HasSecretPixels(string png) => HasPixels(File.ReadAllBytes(png), IsSecret);

    private static bool HasPixels(byte[] image, Func<SKColor, bool> match)
    {
        using SKCodec? codec = SKCodec.Create(new SKMemoryStream(image));
        if (codec is null)
        {
            // Not a raster image, such as an SVG part.
            return false;
        }
        using SKBitmap bitmap = SKBitmap.Decode(codec);
        return bitmap.Pixels.Any(match);
    }

    private static byte[][] XlsxImages(string xlsx)
    {
        using ZipArchive zip = ZipFile.OpenRead(xlsx);
        return [.. zip.Entries
            .Where(static entry => entry.FullName.StartsWith("xl/media/", StringComparison.Ordinal))
            .Select(static entry =>
            {
                using var buffer = new MemoryStream();
                using (Stream stream = entry.Open())
                {
                    stream.CopyTo(buffer);
                }
                return buffer.ToArray();
            })];
    }

    private static bool XlsxHasSecretPixels(string xlsx) => XlsxImages(xlsx).Any(image => HasPixels(image, IsSecret));

    /// <summary>Whether any Flate stream of the PDF, such as an image's samples, holds a run of the secret's pixels.</summary>
    private static bool PdfHasSecretPixels(string pdf)
    {
        byte[] content = File.ReadAllBytes(pdf);
        byte[] run = [.. Enumerable.Repeat(new byte[] { Secret.Red, Secret.Green, Secret.Blue }, 4).SelectMany(static pixel => pixel)];
        byte[] marker = "stream"u8.ToArray();
        for (int index = content.AsSpan().IndexOf(marker); index >= 0;)
        {
            int start = index + marker.Length;
            while (start < content.Length && content[start] is (byte)'\r' or (byte)'\n')
            {
                start++;
            }
            int end = content.AsSpan(start).IndexOf("endstream"u8);
            if (end < 0)
            {
                break;
            }
            try
            {
                using var inflated = new MemoryStream();
                using (var zlib = new ZLibStream(new MemoryStream(content, start, end, writable: false), CompressionMode.Decompress))
                {
                    zlib.CopyTo(inflated);
                }
                if (inflated.ToArray().AsSpan().IndexOf(run) >= 0)
                {
                    return true;
                }
            }
            catch (InvalidDataException)
            {
                // Not a Flate stream.
            }
            int next = content.AsSpan(start + end + 9).IndexOf(marker);
            index = next < 0 ? -1 : start + end + 9 + next;
        }
        return false;
    }
}
