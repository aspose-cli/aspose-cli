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

    private static string[] Render(TextTable table)
    {
        using var writer = new StringWriter();
        table.WriteTo(writer);
        return writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
