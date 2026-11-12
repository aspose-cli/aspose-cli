using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// How a conversion to md takes the links to the pictures its export wrote out of the text, and
/// refuses a picture it cannot take out, given the text and the files as the export writes them.
/// </summary>
public sealed class PdfMarkdownTextTests
{
    /// <summary>
    /// The links the export writes go with their lines, and with the blank line after them, or
    /// before them at the end of the text, as the export writes the text without the pictures.
    /// </summary>
    [Fact]
    public void TakeOutPictures_LeavesTheTextAsTheExportWritesItWithoutThem()
    {
        const string Exported = "# Table\r\n\r\n| Dot |  |\r\n\r\n ![](image_1.png) \r\n\r\nBefore  after.\r\n\r\n ![](image_1.png)  ![](image_2.png) \r\n\r\nLast\r\n\r\n ![](image_2.png) \r\n";

        (string text, int removed) = PdfMarkdownText.TakeOutPictures(Exported, ["image_1.png", "image_2.png"], extraction: null);

        Assert.Equal("# Table\r\n\r\n| Dot |  |\r\n\r\nBefore  after.\r\n\r\nLast\r\n", text);
        Assert.Equal(4, removed);
    }

    /// <summary>
    /// A file the export wrote that no link of the expected form names, or that a reference left
    /// in the text still names, is a picture the conversion cannot take out: it refuses, with the
    /// conversion to txt and the extraction of the pictures as the next steps.
    /// </summary>
    [Theory]
    [InlineData("Text\r\n\r\n ![](image_1.png) \r\n", "image_2.png")]
    [InlineData("Text\r\n\r\n ![a picture](image_1.png) \r\n", null)]
    [InlineData("Text\r\n\r\n ![](image_1.png) \r\n<img src=\"image_1.png\">\r\n", null)]
    public void TakeOutPictures_RefusesAPictureItCannotTakeOut(string exported, string? another)
    {
        const string Extraction = "'aspose-cli pdf extract in.pdf --what images --out-dir out-images --output-format json'";
        string[] pictures = another is null ? ["image_1.png"] : ["image_1.png", another];

        CliException error = Assert.Throws<CliException>(() => PdfMarkdownText.TakeOutPictures(exported, pictures, Extraction));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Contains("--to txt", error.Hint, StringComparison.Ordinal);
        Assert.Contains(Extraction, error.Hint, StringComparison.Ordinal);
    }
}
