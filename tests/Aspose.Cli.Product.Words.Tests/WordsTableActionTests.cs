using Aspose.Cli.Product.Words.Engine;
using Aspose.Words;
using Aspose.Words.Fields;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Each action of the Words table makes its one engine call: run on the proposal document, the
/// saved file holds what the engine's call leaves, read back with the engine itself. The engine
/// runs in this process, so the cases run licensed only.
/// </summary>
public sealed class WordsTableActionTests
{
    private const string Body = "section/0/body";

    /// <summary>A replacement under revision tracking, which leaves a deletion and an insertion; "升级客户门户" occurs once.</summary>
    private const string Tracked = $$$"""{"do":"start_track_revisions","args":{"author":"Ann","dateTime":"2026-01-01T00:00:00"}},{"do":"replace","args":{"pattern":"升级客户门户","replacement":"更换门户"}},{"do":"stop_track_revisions"}""";

    private const string Merge = $$$"""{"add":"field","in":"{{{Body}}}/paragraph/2","args":{"fieldCode":"MERGEFIELD Name"}}""";

    public static TheoryData<string> Actions() =>
    [
        "replace_regex", "update_page_layout", "update_list_labels", "update_word_count", "update_field", "accept_all_revisions", "reject_all_revisions",
        "accept_revision", "reject_revision", "protect", "unprotect", "set_text_watermark", "set_image_watermark", "remove_watermark", "append_document",
        "mail_merge", "mail_merge_regions", "insert_break", "set_column_count",
    ];

    [Theory]
    [MemberData(nameof(Actions))]
    public void Action_LeavesWhatItsEngineCallLeaves(string action)
    {
        WordsFixture.RequireLicense();
        using var fixture = new WordsFixture();
        string input = fixture.CreateProposal();
        string other = fixture.CreateProposal("other.docx").Replace("\\", "\\\\", StringComparison.Ordinal);
        string png = fixture.File("mark.png");
        File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        string image = png.Replace("\\", "\\\\", StringComparison.Ordinal);
        (string ops, Func<Document, bool> holds) = action switch
        {
            "replace_regex" => Case("""{"do":"replace_regex","args":{"pattern":"Cont(o)so","replacement":"Fabrikam"}}""", static d => !d.GetText().Contains("Contoso") && d.GetText().Contains("Fabrikam")),
            "update_page_layout" => Case("""{"do":"update_page_layout"}""", static d => d.PageCount >= 1),
            "update_list_labels" => Case("""{"do":"update_list_labels"}""", static _ => true),
            "update_word_count" => Case("""{"do":"update_word_count"}""", static d => d.BuiltInDocumentProperties.Words > 0),
            "update_field" => Case($$$"""{"add":"field","in":"{{{Body}}}/paragraph/2","args":{"fieldCode":"PAGE","fieldValue":"9"}},{"do":"update_field","args":{"field":"{{{Body}}}/paragraph/2/field/0"}}""",
                static d => d.Range.Fields[0].Result == "1"),
            "accept_all_revisions" => Case($$$"""{{{Tracked}}},{"do":"accept_all_revisions"}""", static d => d.Revisions.Count == 0 && d.GetText().Contains("更换门户")),
            "reject_all_revisions" => Case($$$"""{{{Tracked}}},{"do":"reject_all_revisions"}""", static d => d.Revisions.Count == 0 && d.GetText().Contains("升级客户门户")),
            "accept_revision" => Case($$$"""{{{Tracked}}},{"do":"accept_revision","args":{"revision":"revision/0"}}""", static d => d.Revisions.Count == 1),
            "reject_revision" => Case($$$"""{{{Tracked}}},{"do":"reject_revision","args":{"revision":"revision/0"}}""", static d => d.Revisions.Count == 1),
            "protect" => Case("""{"do":"protect","args":{"protectionType":"ReadOnly"}}""", static d => d.ProtectionType == ProtectionType.ReadOnly),
            "unprotect" => Case("""{"do":"protect","args":{"protectionType":"ReadOnly"}},{"do":"unprotect"}""", static d => d.ProtectionType == ProtectionType.NoProtection),
            "set_text_watermark" => Case("""{"do":"set_text_watermark","args":{"text":"DRAFT"}}""", static d => d.Watermark.Type == WatermarkType.Text),
            "set_image_watermark" => Case($$$"""{"do":"set_image_watermark","args":{"image":"{{{image}}}"}}""", static d => d.Watermark.Type == WatermarkType.Image),
            "remove_watermark" => Case("""{"do":"set_text_watermark","args":{"text":"DRAFT"}},{"do":"remove_watermark"}""", static d => d.Watermark.Type == WatermarkType.None),
            "append_document" => Case($$$"""{"do":"append_document","args":{"file":"{{{other}}}","importFormatMode":"KeepSourceFormatting"}}""", static d => d.Sections.Count == 2),
            "mail_merge" => Case($$$"""{{{Merge}}},{"do":"mail_merge","args":{"fieldNames":["Name"],"rows":[["Ann"]]}}""",
                static d => d.GetText().Contains("Ann") && !d.Range.Fields.Any(static field => field.Type == FieldType.FieldMergeField)),
            "mail_merge_regions" => Case($$$"""{"add":"field","in":"{{{Body}}}/paragraph/2","args":{"fieldCode":"MERGEFIELD TableStart:People"}},{{{Merge}}},{"add":"field","in":"{{{Body}}}/paragraph/2","args":{"fieldCode":"MERGEFIELD TableEnd:People"}},{"do":"mail_merge_regions","args":{"tableName":"People","fieldNames":["Name"],"rows":[["Ann"],["Bob"]]}}""",
                static d => d.GetText().Contains("Ann") && d.GetText().Contains("Bob")),
            "insert_break" => Case($$$"""{"do":"insert_break","args":{"paragraph":"{{{Body}}}/paragraph/2","breakType":"PageBreak"}}""", static d => d.FirstSection.Body.Paragraphs[2].GetText().Contains('\f', StringComparison.Ordinal)),
            "set_column_count" => Case("""{"do":"set_column_count","args":{"section":"section/0","count":2}}""", static d => d.FirstSection.PageSetup.TextColumns.Count == 2),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        string output = fixture.File("out.docx");

        WordsEdit.Run(fixture.Session, WordsFixture.Edit(input, output, $$"""{"ops":[{{ops}}]}"""));

        static (string, Func<Document, bool>) Case(string ops, Func<Document, bool> holds) => (ops, holds);

        Assert.True(holds(new Document(output)), $"{action}: the saved file does not hold what the engine's call leaves.");
    }

    /// <summary>A list's numbers come off a paragraph: ListFormat.RemoveNumbers.</summary>
    [Fact]
    public void RemoveListNumbers_TakesTheParagraphOutOfItsList()
    {
        WordsFixture.RequireLicense();
        using var fixture = new WordsFixture();
        string listed = fixture.File("listed.docx");
        WordsEditResult added = WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.CreateProposal(), listed, """{"ops":[{"add":"list","args":{"listTemplate":"NumberDefault"}}]}"""));
        string id = added.Ops[0].At!.Split('/')[1];

        WordsEdit.Run(fixture.Session, WordsFixture.Edit(listed, fixture.File("numbered.docx"), $$$"""{"ops":[{"set":"{{{Body}}}/paragraph/2","props":{"listFormat.list":{{{id}}}}}]}"""));
        WordsEdit.Run(fixture.Session, WordsFixture.Edit(fixture.File("numbered.docx"), fixture.File("out.docx"), $$$"""{"ops":[{"do":"remove_list_numbers","args":{"paragraph":"{{{Body}}}/paragraph/2"}}]}"""));

        Assert.True(new Document(fixture.File("numbered.docx")).FirstSection.Body.Paragraphs[2].ListFormat.IsListItem);
        Assert.False(new Document(fixture.File("out.docx")).FirstSection.Body.Paragraphs[2].ListFormat.IsListItem);
    }
}
