using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfPreviewTests
{
    [Fact]
    public void Preview_WritesEveryArtifactThroughSinkAndPreservesAssetUrls()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("preview.pdf", pages: 2);
        var artifacts = new MemoryArtifactSink();

        PreviewRenderer renderer = new PdfPreviewAdapter().CreateRenderer(
            fixture.Engine,
            input,
            new ProductPreviewRequest(PdfPreviewAdapter.PagesView));

        PreviewRenderOutcome result = renderer(new PreviewRenderContext(artifacts));

        Assert.Equal("document.html", result.EntryFileName);
        Assert.Equal("pdf", result.SourceFormatId);
        Assert.Equal(new FileInfo(input).Length, result.SourceSizeBytes);
        Assert.Null(result.Warnings);
        Assert.Equal(
            ["document.html", "page-0001.png", "page-0002.png"],
            artifacts.Paths.Order(StringComparer.Ordinal).ToArray());
        string html = artifacts.Text("document.html");
        Assert.Contains("/asset/page-0001.png", html, StringComparison.Ordinal);
        Assert.Contains("/asset/page-0002.png", html, StringComparison.Ordinal);
        Assert.True(artifacts.IsPng("page-0001.png"));
        Assert.True(artifacts.IsPng("page-0002.png"));
    }

    [Fact]
    public void Preview_UsesPasswordWithoutPublishingItOrPartialArtifactsOnFailure()
    {
        const string password = "preview-password";
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument(
            password,
            "owner-password",
            "protected.pdf");
        var artifacts = new MemoryArtifactSink();

        PreviewRenderOutcome result = fixture.Engine.RenderPreview(
            input,
            new PdfPreviewRequest { Password = password },
            artifacts);

        Assert.True(artifacts.IsPng("page-0001.png"));
        Assert.DoesNotContain(password, artifacts.Text(result.EntryFileName), StringComparison.Ordinal);

        var rejected = new MemoryArtifactSink();
        _ = Assert.Throws<CliException>(() => fixture.Engine.RenderPreview(
            input,
            new PdfPreviewRequest { Password = "wrong-password" },
            rejected));
        Assert.Empty(rejected.Paths);
    }

    private sealed class MemoryArtifactSink : IPreviewArtifactSink
    {
        private static readonly byte[] PngSignature =
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public IEnumerable<string> Paths => _files.Keys;

        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
            _files.Add(relativePath, stream.ToArray());
        }

        public void WriteText(string relativePath, string content) =>
            _files.Add(relativePath, Encoding.UTF8.GetBytes(content));

        public bool IsPng(string relativePath) =>
            _files[relativePath].AsSpan().StartsWith(PngSignature);

        public string Text(string relativePath) =>
            Encoding.UTF8.GetString(_files[relativePath]);
    }
}
