using System.Text;
using Aspose.Cli.Sdk.IO.Input;
using Xunit;

namespace Aspose.Cli.Platform.Tests.IO;

public sealed class SvgReferencesTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Theory]
    [InlineData("""<image xlink:href="C:\secret\s.png"/>""", "C:\\secret\\s.png")]
    [InlineData("""<image href="\\server\share\s.png"/>""", "\\\\server\\share\\s.png")]
    [InlineData("""<image xlink:href="file:///C:/secret/s.png"/>""", "file:///C:/secret/s.png")]
    [InlineData("""<image xlink:href="http://example.test/a.png"/>""", "http://example.test/a.png")]
    [InlineData("""<image xlink:href="s.png"/>""", "s.png")]
    [InlineData("""<image alt='xmlns="' xlink:href='http://example.test/a.png' title='"'/>""", "http://example.test/a.png")]
    [InlineData("""<feImage href="http://example.test/a.png"/>""", "http://example.test/a.png")]
    [InlineData("""<use xlink:href="other.svg#icon"/>""", "other.svg#icon")]
    [InlineData("""<image xlink:href="data:image/svg+xml;base64,PHN2Zy8+"/>""", "data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("""<image xlink:href="data:image/png,%89PNG"/>""", "data:image/png,%89PNG")]
    [InlineData("""<rect style="fill:url(http://example.test/a.png)"/>""", "http://example.test/a.png")]
    [InlineData("""<rect fill="url('file:///C:/s.png')"/>""", "file:///C:/s.png")]
    [InlineData("""<style>@import "http://example.test/a.css";</style>""", "@import \"http://example.test/a.css\";")]
    [InlineData("""<style>rect { fill: u\72l(http://example.test/a.png) }</style>""", "rect { fill: u\\72l(http://example.test/a.png) }")]
    [InlineData("""<g xml:base="file:///C:/secret/"/>""", "xml:base=file:///C:/secret/")]
    [InlineData("""<image xlink:href="#a"><set attributeName="href" to="C:\s.png"/></image>""", "<set>")]
    [InlineData("""<animate attributeName="href" values="C:\s.png"/>""", "<animate>")]
    public void KeepInternal_RemovesEveryReferenceOutsideTheSvg(string content, string reference)
    {
        SvgReferences.Cleaned cleaned = Assert.IsType<SvgReferences.Cleaned>(SvgReferences.KeepInternal(Svg(content)));

        Assert.Contains(reference, cleaned.Removed);
        string kept = Encoding.UTF8.GetString(cleaned.Content);
        Assert.DoesNotContain("example.test", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("s.png", kept, StringComparison.Ordinal);
        Assert.NotNull(SvgReferences.KeepInternal(cleaned.Content));
        Assert.Empty(SvgReferences.KeepInternal(cleaned.Content)!.Removed);
    }

    /// <summary>A data: URI keeps its place only when its bytes are a PNG, JPEG or GIF file, whatever type it declares.</summary>
    [Theory]
    [InlineData("image/png", "89504E470D0A1A0A0000", true)]
    [InlineData("image/jpeg", "FFD8FFE000104A464946", true)]
    [InlineData("image/gif", "4749463839610100", true)]
    [InlineData("image/png", "3C7376672F3E", false)]
    [InlineData("image/bmp", "424D3600000000000000", false)]
    [InlineData("image/webp", "52494646000000005745", false)]
    public void KeepInternal_KeepsADataUriOnlyForARasterFileItHolds(string mediaType, string hex, bool kept)
    {
        string uri = $"data:{mediaType};base64,{Convert.ToBase64String(Convert.FromHexString(hex))}";

        SvgReferences.Cleaned cleaned = SvgReferences.KeepInternal(Svg($"""<image xlink:href="{uri}"/>"""))!;

        Assert.Equal(kept, cleaned.Removed.Count == 0);
    }

    [Fact]
    public void KeepInternal_KeepsAnSvgThatNamesOnlyItselfByteForByte()
    {
        byte[] svg = Svg($"""<defs><rect id="r" width="4" height="4" fill="url(#g)"/></defs><use xlink:href="#r"/><image xlink:href="data:image/png;base64,{Png}"/><a href="#r"><text>t</text></a>""");

        SvgReferences.Cleaned cleaned = SvgReferences.KeepInternal(svg)!;

        Assert.Empty(cleaned.Removed);
        Assert.Same(svg, cleaned.Content);
    }

    [Fact]
    public void KeepInternal_KeepsALinkWithoutItsAddress()
    {
        SvgReferences.Cleaned cleaned = SvgReferences.KeepInternal(Svg("""<a href="http://example.test/"><text>label</text></a>"""))!;

        string kept = Encoding.UTF8.GetString(cleaned.Content);
        Assert.Contains("label", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", kept, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""<!DOCTYPE svg [<!ENTITY e SYSTEM "file:///C:/secret.txt">]><svg xmlns="http://www.w3.org/2000/svg">&e;</svg>""")]
    [InlineData("""<!DOCTYPE svg [<!ENTITY xxe "x">]><svg xmlns="http://www.w3.org/2000/svg"><text>&xxe;</text></svg>""")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg"><g></svg>""")]
    [InlineData("not markup")]
    public void KeepInternal_ReturnsNullForAnSvgThatDoesNotParse(string content) =>
        Assert.Null(SvgReferences.KeepInternal(Encoding.UTF8.GetBytes(content)));

    /// <summary>
    /// A document type, as editors export it, is skipped with its internal subset: the SVG is
    /// kept, without the document type.
    /// </summary>
    [Fact]
    public void KeepInternal_KeepsAnSvgWithADocumentTypeItsContentDoesNotUse()
    {
        byte[] svg = Encoding.UTF8.GetBytes(
            """<?xml version="1.0"?><!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd" [<!ENTITY ns_svg "http://www.w3.org/2000/svg">]><svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><rect id="r" width="4" height="4"/></svg>""");

        SvgReferences.Cleaned? cleaned = SvgReferences.KeepInternal(svg);

        Assert.NotNull(cleaned);
        string kept = Encoding.UTF8.GetString(cleaned.Content);
        Assert.DoesNotContain("DOCTYPE", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("ENTITY", kept, StringComparison.Ordinal);
        Assert.Contains("<rect id=\"r\"", kept, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepInternal_RemovesAStyleSheetInstruction()
    {
        byte[] svg = Encoding.UTF8.GetBytes("""<?xml-stylesheet href="http://example.test/a.css"?><svg xmlns="http://www.w3.org/2000/svg"/>""");

        SvgReferences.Cleaned cleaned = SvgReferences.KeepInternal(svg)!;

        Assert.Single(cleaned.Removed);
        Assert.DoesNotContain("example.test", Encoding.UTF8.GetString(cleaned.Content), StringComparison.Ordinal);
    }

    private static byte[] Svg(string content) => Encoding.UTF8.GetBytes(
        $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="4" height="4">{content}</svg>""");
}
