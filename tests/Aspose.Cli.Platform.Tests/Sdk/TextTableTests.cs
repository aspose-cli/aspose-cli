using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class TextTableTests
{
    [Fact]
    public void WriteTo_AlignsColumnsByDisplayWidthWithChineseText()
    {
        var table = new TextTable("Sheet", "Rows");
        table.AddRow("汇总", "12");
        table.AddRow("Data", "3");
        table.AddRow("销售明细报表", "40");

        string[] lines = Render(table);

        // Each Chinese character takes two terminal columns, so "销售明细报表" is 12 columns wide.
        Assert.Equal(
        [
            "Sheet         Rows",
            "汇总          12",
            "Data          3",
            "销售明细报表  40",
        ], lines);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("汇总", 4)]
    [InlineData("\uFF21\uFF22", 4)]
    [InlineData("\uD55C\uAE00", 4)]
    [InlineData("\u304B\u306A", 4)]
    [InlineData("e\u0301", 1)]
    [InlineData("a\u200Bb", 2)]
    [InlineData("\U0001F600", 2)]
    [InlineData("\U00020000", 2)]
    [InlineData("\uFF71", 1)]
    public void Of_CountsTerminalColumns(string text, int expected) =>
        Assert.Equal(expected, TextWidth.Of(text));

    [Fact]
    public void PadRight_PadsToTheDisplayWidth()
    {
        Assert.Equal("汇总  ", TextWidth.PadRight("汇总", 6));
        Assert.Equal("汇总", TextWidth.PadRight("汇总", 3));
    }

    private static string[] Render(TextTable table)
    {
        using var writer = new StringWriter();
        table.WriteTo(writer);
        return writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
