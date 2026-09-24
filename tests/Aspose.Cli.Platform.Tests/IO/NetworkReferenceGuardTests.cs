using System.IO.Compression;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Platform.Tests.IO;

public sealed class NetworkReferenceGuardTests
{
    [Theory]
    [InlineData("<img src=\"https://example.test/a.png\">")]
    [InlineData("<img src=\"HTTP://example.test/a.png\">")]
    [InlineData("<img src=\"//example.test/a.png\">")]
    [InlineData("<img src=\"\\\\server\\share\\a.png\">")]
    [InlineData("<img src=\"file://server/share/a.png\">")]
    [InlineData("<img src=\"http:example.test/a.png\">")]
    [InlineData("<img src=\"ht\ntp://example.test/a.png\">")]
    [InlineData("<img src=\"http&#58;&#x2F;&#47;example.test/a.png\">")]
    [InlineData("<style>p { background: url('\\68\\74\\74\\70\\3a\\2f\\2f example.test/a.png') }</style>")]
    [InlineData("<a href=\"ftp://example.test/\">link</a>")]
    [InlineData("Plain text https://example.test")]
    [InlineData("<!DOCTYPE x [<!ENTITY e SYSTEM \"http://example.test/e\">]><x/>")]
    public void EnsureNone_RefusesEveryNetworkAddress(string markup)
    {
        CliException refused = Assert.Throws<CliException>(() =>
            NetworkReferenceGuard.EnsureNone(Encoding.UTF8.GetBytes(markup), "HTML input", "input.html"));

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
    }

    [Theory]
    [InlineData("<img src=\"local.png\"><img src=\"images/a%20b.png\">")]
    [InlineData("<img src=\"file:///C:/work/local.png\">")]
    [InlineData("<img src=\"data:image/png;base64,iVBORw0KGgo=\">")]
    [InlineData("<a href=\"mailto:someone@example.test\">mail</a> 1/2 and a/b")]
    [InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><svg xmlns:xlink='http://www.w3.org/1999/xlink'/></html>")]
    public void EnsureNone_AcceptsLocalReferencesAndIdentifiers(string markup) =>
        NetworkReferenceGuard.EnsureNone(Encoding.UTF8.GetBytes(markup), "HTML input", "input.html");

    [Fact]
    public void EnsureNone_ReadsUtf16WithAndWithoutAByteOrderMark()
    {
        const string markup = "<img src=\"https://example.test/a.png\">";
        foreach (byte[] content in new[] { Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(markup)).ToArray(), Encoding.Unicode.GetBytes(markup), Encoding.BigEndianUnicode.GetBytes(markup) })
        {
            Assert.Throws<CliException>(() => NetworkReferenceGuard.EnsureNone(content, "HTML input", "input.html"));
        }
    }

    [Fact]
    public void EnsureNoneInImage_ChecksSvgAndRefusesCompressedSvg()
    {
        byte[] svg = Encoding.UTF8.GetBytes("\uFEFF  <svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://example.test/a.png\"/></svg>");
        Assert.Throws<CliException>(() => NetworkReferenceGuard.EnsureNoneInImage(new MemoryStream(svg), "remote.svg"));

        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"));
        }
        compressed.Position = 0;
        CliException refused = Assert.Throws<CliException>(() => NetworkReferenceGuard.EnsureNoneInImage(compressed, "image.svgz"));
        Assert.Contains("SVGZ", refused.Message, StringComparison.Ordinal);

        NetworkReferenceGuard.EnsureNoneInImage(
            new MemoryStream(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"1\" height=\"1\"/></svg>")),
            "local.svg");
    }

    [Fact]
    public void EnsureNoneInImage_PassesRasterImagesWithoutScanningTheirData()
    {
        // Compressed raster data can contain any byte sequence, including "//".
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, (byte)' ', (byte)'/', (byte)'/', (byte)'x'];
        NetworkReferenceGuard.EnsureNoneInImage(new MemoryStream(png), "image.png");
        NetworkReferenceGuard.EnsureNoneInImage(new MemoryStream([0xFF, 0xD8, 0xFF, (byte)'/', (byte)'/', (byte)'x']), "image.jpg");
    }
}
