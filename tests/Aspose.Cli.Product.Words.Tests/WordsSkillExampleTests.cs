using Aspose.Words;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>The Words Skill's example, run as its README writes it, from the installed Skill.</summary>
[Category(TestCategory.Slow)]
public sealed class WordsSkillExampleTests
{
    [Fact]
    public void TableExample_BuildsTheTable_ClonesARow_AndWritesThePdf()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, "aspose-cli-words", "fill-a-table").Directory;

        var document = new Document(Path.Combine(directory, "milestones.2.docx"));
        Table table = Assert.Single(document.GetChildNodes(NodeType.Table, true).Cast<Table>());
        Assert.Equal(["Phase\aDate\a\a", "Design\a2026-03-01\a\a", "Build\a2026-05-15\a\a"], table.Rows.Cast<Row>().Select(static row => row.GetText()));
        Assert.True(table.FirstRow.RowFormat.HeadingFormat);
        Assert.Equal("Milestones", document.BuiltInDocumentProperties.Title);
        Assert.True(new FileInfo(Path.Combine(directory, "milestones.pdf")).Length > 0);
    }
}
