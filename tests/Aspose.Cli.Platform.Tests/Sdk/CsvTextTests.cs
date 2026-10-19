using System.Text;
using Aspose.Cli.Sdk.Text;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class CsvTextTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData("a, b", "\"a, b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("one\ntwo", "\"one\ntwo\"")]
    [InlineData("one\r\ntwo", "\"one\ntwo\"")]
    [InlineData("one\rtwo", "\"one\ntwo\"")]
    [InlineData("汇总", "汇总")]
    public void Field_QuotesOnlyWhatNeedsIt(string value, string expected) =>
        Assert.Equal(expected, CsvText.Field(value));

    [Fact]
    public void Encode_EndsEveryRecordWithCrLf()
    {
        string[][] rows = [["Name", "Note, more"], ["Ann", "x\r\ny"], [], ["only"]];

        Assert.Equal(
            Encoding.UTF8.GetBytes("Name,\"Note, more\"\r\nAnn,\"x\ny\"\r\n\r\nonly\r\n"),
            CsvText.Encode(rows, byteOrderMark: false));
    }

    [Fact]
    public void Encode_StartsWithAByteOrderMarkOnlyWhenAsked()
    {
        string[][] rows = [["汇总"]];
        byte[] text = Encoding.UTF8.GetBytes("汇总\r\n");

        Assert.Equal(text, CsvText.Encode(rows, byteOrderMark: false));
        Assert.Equal([0xEF, 0xBB, 0xBF, .. text], CsvText.Encode(rows, byteOrderMark: true));
        Assert.Equal([0xEF, 0xBB, 0xBF], CsvText.Encode([], byteOrderMark: true));
        Assert.Empty(CsvText.Encode([], byteOrderMark: false));
    }
}
