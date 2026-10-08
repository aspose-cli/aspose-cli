using System.Text;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class ContainerSignaturesTests
{
    [Fact]
    public void ThePdfHeader_IsFoundWhereAReaderAcceptsIt()
    {
        byte[] leading = [.. new byte[100], .. "%PDF-1.7"u8];
        byte[] late = [.. new byte[ContainerSignatures.PdfHeaderWindow], .. "%PDF-1.7"u8];

        Assert.True(ContainerSignatures.StartsWithPdfHeader("%PDF-1.7"u8));
        Assert.False(ContainerSignatures.StartsWithPdfHeader(leading));
        Assert.True(ContainerSignatures.HasPdfHeader(leading));
        Assert.False(ContainerSignatures.HasPdfHeader(late));
    }

    [Fact]
    public void OleAndZipContainers_AreRecognizedByTheirFirstBytes()
    {
        byte[] ole = Convert.FromBase64String("0M8R4KGxGuEAAAAAAAAAAA==");
        byte[] zip = Convert.FromBase64String("UEsDBAECAwQFBgcICQo=");

        Assert.True(ContainerSignatures.IsOleCompoundFile(ole));
        Assert.False(ContainerSignatures.IsOleCompoundFile(zip));
        Assert.True(ContainerSignatures.IsZip(zip));
        Assert.False(ContainerSignatures.IsZip(ole));
        Assert.False(ContainerSignatures.IsZip("PK"u8));
    }

    [Fact]
    public void APrefix_IsReadWithoutMovingASeekableStream()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("0123456789"));
        stream.Position = 2;

        Assert.Equal("2345"u8.ToArray(), ContainerSignatures.ReadPrefix(stream, 4));
        Assert.Equal(2, stream.Position);
        Assert.Equal("23456789"u8.ToArray(), ContainerSignatures.ReadPrefix(stream, 64));
    }

    [Fact]
    public void AFileThatCannotBeRead_HasAnEmptyPrefix()
    {
        using var temp = new TempDirectory();

        Assert.Empty(ContainerSignatures.ReadPrefix(temp.File("missing.pdf")));
    }
}
