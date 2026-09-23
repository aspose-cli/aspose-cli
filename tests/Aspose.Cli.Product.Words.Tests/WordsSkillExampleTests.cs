using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsSkillExampleTests
{
    [Fact]
    public void ReportExampleUsesBundledTemplateAndPreservesItsTable()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, "aspose-cli-words", "report-from-markdown").Directory;
        var report = new Document(Path.Combine(directory, "report.docx"));
        var template = new Document(Path.Combine(directory, "../../assets/templates/default-a4.docx"));

        Assert.Contains("Quarterly Report", report.Range.Text, StringComparison.Ordinal);
        Assert.Contains("Summary", report.Range.Text, StringComparison.Ordinal);
        Table table = Assert.Single(report.GetChildNodes(NodeType.Table, true).Cast<Table>());
        Assert.Contains("Revenue", table.Range.Text, StringComparison.Ordinal);
        Assert.Contains("120", table.Range.Text, StringComparison.Ordinal);
        Assert.Contains("Cost", table.Range.Text, StringComparison.Ordinal);
        Assert.Equal(template.FirstSection.PageSetup.PageWidth, report.FirstSection.PageSetup.PageWidth);
        Assert.Equal(template.FirstSection.PageSetup.PageHeight, report.FirstSection.PageSetup.PageHeight);
        Assert.True(File.Exists(Path.Combine(directory, "report.review/review.json")));
    }

    [Fact]
    public void ContractExampleRetainsSourceAndProducesTrackedChangeAndComment()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, "aspose-cli-words", "edit-contract-safely").Directory;
        var original = new Document(Path.Combine(directory, "contract.docx"));
        var changed = new Document(Path.Combine(directory, "contract.review.docx"));

        Assert.Contains("thirty (30) days", original.Range.Text, StringComparison.Ordinal);
        Assert.True(changed.HasRevisions);
        Comment comment = Assert.Single(changed.GetChildNodes(NodeType.Comment, true).Cast<Comment>());
        Assert.Contains("Updated notice period for review.", comment.Range.Text, StringComparison.Ordinal);
        changed.AcceptAllRevisions();
        Assert.Contains("forty-five (45) days", changed.Range.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("thirty (30) days", changed.Range.Text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(directory, "contract.review/review.json")));
    }

    [Fact]
    public void MailMergeExampleFillsBothRecipientsAndRemovesMergeFields()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, "aspose-cli-words", "mail-merge-letters").Directory;
        var template = new Document(Path.Combine(directory, "letter-template.docx"));
        var letters = new Document(Path.Combine(directory, "letters.docx"));

        Assert.Equal(3, template.Range.Fields.Cast<Field>().Count(field => field.Type == FieldType.FieldMergeField));
        Assert.Contains("Ava Stone", letters.Range.Text, StringComparison.Ordinal);
        Assert.Contains("Noah Chen", letters.Range.Text, StringComparison.Ordinal);
        Assert.Contains("125.00", letters.Range.Text, StringComparison.Ordinal);
        Assert.Contains("80.00", letters.Range.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(letters.Range.Fields.Cast<Field>(), field => field.Type == FieldType.FieldMergeField);
        Assert.True(File.Exists(Path.Combine(directory, "letters.review/review.json")));
    }
}
