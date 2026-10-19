using Aspose.Cli.Product.Words.Engine.Editing;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What each Words operation addresses, one row per sample: the blocks and sections it resolves
/// in the document before the batch, the targets a failed operation reports, whether
/// <c>--track-changes</c> admits it, and whether the verification compares its effect without
/// tracked changes. Every operation of the vocabulary has a row, so a new operation fails here
/// until it is placed.
/// </summary>
public sealed class WordsOpAddressTests
{
    /// <summary>
    /// The sample of every operation, then the variants whose address takes another branch: no
    /// section, a section or block the document lacks, an anchor that matches nothing, a break
    /// other than a page break and a decision on numbered revisions.
    /// </summary>
    private static readonly WordsOp[] Samples =
    [
        .. WordsOperationSample.Batch.Ops,
        new SetPageSetupOp { Orientation = "landscape" },
        new SetHeaderOp { Paragraphs = [new WordsParagraphInput { Text = "Header" }] },
        new SetFooterOp { Markdown = "Footer" },
        new SetPageNumbersOp { Location = "footer" },
        new SetPageSetupOp { Section = 9, Orientation = "landscape" },
        new InsertSectionOp { At = 9, Position = "after" },
        new DeleteSectionOp { Section = 9 },
        new SetTextOp { Target = new WordsTarget { Block = 99 }, Text = "x" },
        new DeleteBlocksOp { Target = new WordsTarget { Blocks = "2-99" } },
        new SetStyleOp { Target = new WordsTarget { Bookmark = "Missing" }, Style = "Heading 2" },
        new AddCommentOp { Target = new WordsTarget { Heading = "No such heading" }, Author = "Reviewer", Text = "x" },
        new FormatTextOp { Target = new WordsTarget { Find = "no such text" }, Bold = true },
        new InsertBreakOp { At = new WordsTarget { Block = 1 }, Position = "after", Kind = WordsBreakKinds.Section },
        new AcceptRevisionsOp { Revisions = [1] },
        new RejectRevisionsOp { Revisions = [5] },
    ];

    /// <summary>
    /// Each row: the operation's name; the blocks and the section numbers it resolved, or the
    /// error its address raised; the targets it reports when it fails; and the targets it reports
    /// when its address resolved nothing.
    /// </summary>
    private static readonly string[] ExpectedAddresses =
    [
        "replace_text | - - | block/all | block/all",
        "set_text | block/1 - | block/1 | block/1",
        "insert_paragraphs | block/2,block/3 - | block/2,block/3 | block/2-3",
        "insert_markdown | block/4 - | block/4 | block/all",
        "delete_blocks | ANCHOR_NOT_FOUND | block/all | block/all",
        "insert_break | block/8 - | block/8 | block/all",
        "insert_image | block/2 - | block/2 | block/2",
        "insert_table | block/3 - | block/3 | block/3",
        "set_table_cell | block/4 - | block/4 | block/4",
        "repeat_table_row | block/5 - | block/5 | block/all",
        "set_table_pagination | block/4 - | block/4 | block/4",
        "insert_toc | block/1 - | block/1 | block/1",
        "insert_bookmark | block/5 - | block/5 | block/5",
        "insert_hyperlink | block/5 - | block/5 | block/5",
        "insert_field | block/6 - | block/6 | block/6",
        "insert_section | - section/1 | section/1 | section/1",
        "delete_section | SECTION_NOT_FOUND | section/3 | section/3",
        "set_page_setup | - section/1 | section/1 | section/1",
        "set_header | - section/1 | section/1 | section/1",
        "set_footer | - section/1 | section/1 | section/1",
        "set_page_numbers | - section/1 | section/1 | section/1",
        "format_text | block/1,block/2 - | block/1,block/2 | block/1-2",
        "set_style | ANCHOR_NOT_FOUND | block/all | block/all",
        "define_style | - - | style/Report Callout | style/Report Callout",
        "set_list | block/7,block/8,block/9 - | block/7,block/8,block/9 | block/7-9",
        "clear_list | block/7,block/8,block/9 - | block/7,block/8,block/9 | block/7-9",
        "set_default_font | - - | style/all | style/all",
        "set_properties | - - | property/all | property/all",
        "set_watermark | - - | watermark/1 | watermark/1",
        "delete_watermark | - - | watermark/1 | watermark/1",
        "protect_document | - - | protection/document | protection/document",
        "unprotect_document | - - | protection/document | protection/document",
        "accept_revisions | - - | block/all | block/all",
        "reject_revisions | - - | block/all | block/all",
        "add_comment | block/2 - | block/2 | block/2",
        "delete_comments | - - | block/all | block/all",
        "import_sections | - - | block/all | block/all",
        "mail_merge | - - | block/all | block/all",
        "refresh_fields | - - | block/all | block/all",
        "set_page_setup | - section/1,section/2 | section/1,section/2 | section/all",
        "set_header | - section/1,section/2 | section/1,section/2 | section/all",
        "set_footer | - section/1,section/2 | section/1,section/2 | section/all",
        "set_page_numbers | - section/1,section/2 | section/1,section/2 | section/all",
        "set_page_setup | SECTION_NOT_FOUND | section/9 | section/9",
        "insert_section | SECTION_NOT_FOUND | section/9 | section/9",
        "delete_section | SECTION_NOT_FOUND | section/9 | section/9",
        "set_text | BLOCK_NOT_FOUND | block/99 | block/99",
        "delete_blocks | BLOCK_NOT_FOUND | block/2-99 | block/2-99",
        "set_style | BOOKMARK_NOT_FOUND | block/all | block/all",
        "add_comment | ANCHOR_NOT_FOUND | block/all | block/all",
        "format_text | ANCHOR_NOT_FOUND | block/all | block/all",
        "insert_break | block/1 - | block/1 | block/1",
        "accept_revisions | block/9 - | block/9 | block/all",
        "reject_revisions | REVISION_NOT_FOUND | block/all | block/all",
    ];

    [Fact]
    public void EveryOperationOfTheVocabularyHasARow()
    {
        Assert.Equal(
            WordsOp.Catalog.Names.Order(StringComparer.Ordinal),
            Samples.Select(static op => WordsOp.Catalog.NameOf(op)).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            WordsOp.Catalog.Names.Order(StringComparer.Ordinal),
            Applied().Select(static op => WordsOp.Catalog.NameOf(op)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Addresses_ResolveAndReportWhatEachOperationNames()
    {
        using var fixture = new WordsFixture();
        string input = TwoSectionReport(fixture);
        using LoadedDocument loaded = fixture.Session.Loader.Open(input, password: (Secret?)null);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        ResolvedWordsOp[] resolved = [.. Samples.Select(op =>
            WordsAnchorResolver.Resolve(loaded.Document, index, new WordsOpsBatch { Ops = [op] }).Single())];
        var model = new WordsEditModel(loaded, resolved, static _ => throw new InvalidOperationException("no operation applies"));
        var unresolved = new WordsEditModel(
            loaded,
            [.. Samples.Select(static op => new ResolvedWordsOp(op, [], [], []) { Failure = new OperationInvalidException("unresolved", "none") })],
            static _ => throw new InvalidOperationException("no operation applies"));

        string[] actual = [.. resolved.Select(item => Row(loaded.Document, item, model, unresolved))];

        Assert.Equal(ExpectedAddresses, actual);
    }

    private static string Row(Document document, ResolvedWordsOp item, WordsEditModel model, WordsEditModel unresolved)
    {
        string resolution = item.Failure switch
        {
            CliException error => error.Code.Name,
            OperationInvalidException => "OPERATION_INVALID",
            _ => $"{Listed(item.Targets)} {Listed([.. item.Sections.Select(section => $"section/{document.Sections.IndexOf(section) + 1}")])}",
        };
        return $"{WordsOp.Catalog.NameOf(item.Op)} | {resolution} | {Listed(model.AttemptedTargets(item.Op))} | {Listed(unresolved.AttemptedTargets(item.Op))}";
    }

    private static string Listed(IReadOnlyList<string> items) => items.Count == 0 ? "-" : string.Join(",", items);

    /// <summary>Each row: the operation's name and whether <c>--track-changes</c> admits it.</summary>
    private static readonly string[] ExpectedTracking =
    [
        "replace_text | tracked",
        "set_text | tracked",
        "insert_paragraphs | tracked",
        "insert_markdown | tracked",
        "delete_blocks | tracked",
        "insert_break | tracked",
        "insert_image | tracked",
        "insert_table | tracked",
        "set_table_cell | tracked",
        "repeat_table_row | tracked",
        "set_table_pagination | refused",
        "insert_toc | tracked",
        "insert_bookmark | tracked",
        "insert_hyperlink | tracked",
        "insert_field | tracked",
        "insert_section | refused",
        "delete_section | refused",
        "set_page_setup | refused",
        "set_header | refused",
        "set_footer | refused",
        "set_page_numbers | refused",
        "format_text | refused",
        "set_style | refused",
        "define_style | refused",
        "set_list | refused",
        "clear_list | refused",
        "set_default_font | refused",
        "set_properties | refused",
        "set_watermark | refused",
        "delete_watermark | refused",
        "protect_document | refused",
        "unprotect_document | refused",
        "accept_revisions | refused",
        "reject_revisions | refused",
        "add_comment | tracked",
        "delete_comments | tracked",
        "import_sections | tracked",
        "mail_merge | refused",
        "refresh_fields | refused",
        "set_page_setup | refused",
        "set_header | refused",
        "set_footer | refused",
        "set_page_numbers | refused",
        "set_page_setup | refused",
        "insert_section | refused",
        "delete_section | refused",
        "set_text | tracked",
        "delete_blocks | tracked",
        "set_style | refused",
        "add_comment | tracked",
        "format_text | refused",
        "insert_break | refused",
        "accept_revisions | refused",
        "reject_revisions | refused",
    ];

    [Fact]
    public void TrackChanges_AdmitsOnlyTheOperationsItRecords()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();

        string[] actual = [.. Samples.Select(op => $"{WordsOp.Catalog.NameOf(op)} | {Tracking(fixture, input, op)}")];

        Assert.Equal(ExpectedTracking, actual);
    }

    private static string Tracking(WordsFixture fixture, string input, WordsOp op)
    {
        try
        {
            WordsEdit.Run(fixture.Session, new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [op] },
                Output = TestOutput.At(fixture.Temp.File($"tracked-{Guid.NewGuid():N}.docx")),
                Options = new EditCommandOptions { BestEffort = true, DryRun = true },
                TrackChanges = true,
                Author = "Reviewer",
            });
            return "tracked";
        }
        catch (CliException error) when (error.Code == ErrorCodes.OptionInvalid && error.Message.Contains("cannot be recorded as tracked changes", StringComparison.Ordinal))
        {
            return "refused";
        }
    }

    /// <summary>
    /// Each row: the operation's name, its status, and whether the verification of a batch
    /// without tracked changes compared its effect.
    /// </summary>
    private static readonly string[] ExpectedComparison =
    [
        "replace_text | ok | not compared",
        "set_text | ok | not compared",
        "insert_paragraphs | ok | not compared",
        "insert_markdown | ok | not compared",
        "delete_blocks | ok | not compared",
        "insert_break | ok | not compared",
        "insert_image | ok | not compared",
        "insert_table | ok | not compared",
        "set_table_cell | ok | not compared",
        "repeat_table_row | ok | not compared",
        "set_table_pagination | ok | not compared",
        "insert_toc | ok | compared",
        "insert_bookmark | ok | not compared",
        "insert_hyperlink | ok | compared",
        "insert_field | ok | compared",
        "insert_section | ok | not compared",
        "delete_section | ok | not compared",
        "set_page_setup | ok | not compared",
        "set_header | ok | not compared",
        "set_footer | ok | not compared",
        "set_page_numbers | ok | compared",
        "format_text | ok | not compared",
        "set_style | ok | not compared",
        "define_style | ok | not compared",
        "set_list | ok | not compared",
        "clear_list | ok | not compared",
        "set_default_font | ok | not compared",
        "set_properties | ok | not compared",
        "set_watermark | ok | not compared",
        "delete_watermark | ok | not compared",
        "protect_document | ok | compared",
        "unprotect_document | ok | compared",
        "accept_revisions | ok | compared",
        "reject_revisions | ok | compared",
        "add_comment | ok | not compared",
        "delete_comments | ok | not compared",
        "import_sections | ok | not compared",
        "mail_merge | ok | compared",
        "refresh_fields | ok | compared",
    ];

    [Fact]
    public void Verify_ComparesTheStateOfTheOperationsWhoseEffectItReadsBack()
    {
        using var fixture = new WordsFixture();
        string input = TwoSectionReport(fixture);

        string[] actual = [.. Applied(fixture).Select(op => Comparison(fixture, input, op))];

        Assert.Equal(ExpectedComparison, actual);
    }

    private static string Comparison(WordsFixture fixture, string input, WordsOp op)
    {
        WordsEditResult result = fixture.Disclosed(session => WordsEdit.Run(session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [op with { Id = "it" }] },
            Output = TestOutput.At(fixture.Temp.File($"verified-{Guid.NewGuid():N}.docx")),
            Options = new EditCommandOptions { BestEffort = true },
            Verify = true,
        }));
        string compared = result.Verification!.CheckedOps.Contains("it") ? "compared" : "not compared";
        return $"{WordsOp.Catalog.NameOf(op)} | {Assert.Single(result.Applied).Status} | {compared}";
    }

    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    /// <summary>One operation of each kind that applies to <see cref="TwoSectionReport"/>.</summary>
    private static WordsOp[] Applied(WordsFixture? fixture = null)
    {
        string image = "image.png";
        string appendix = "appendix.docx";
        if (fixture is not null)
        {
            image = fixture.Temp.File("image.png");
            File.WriteAllBytes(image, Convert.FromBase64String(Png));
            appendix = fixture.Temp.File("appendix.docx");
            var source = new Document();
            new DocumentBuilder(source).Writeln("Appendix text");
            source.Save(appendix, SaveFormat.Docx);
        }

        return
        [
            new ReplaceTextOp { Find = "twelve", Replace = "ten" },
            new SetTextOp { Target = new WordsTarget { Block = 2 }, Text = "Revenue held." },
            new InsertParagraphsOp { At = new WordsTarget { Block = 2 }, Position = "after", Paragraphs = [new WordsParagraphInput { Text = "Inserted" }] },
            new InsertMarkdownOp { At = new WordsTarget { Block = 3 }, Position = "after", Markdown = "Imported **text**" },
            new DeleteBlocksOp { Target = new WordsTarget { Block = 3 } },
            new InsertBreakOp { At = new WordsTarget { Block = 2 }, Position = "after", Kind = WordsBreakKinds.Page },
            new InsertImageOp { At = new WordsTarget { Block = 2 }, Position = "after", Path = image },
            new InsertTableOp { At = new WordsTarget { Block = 3 }, Position = "after", RowCount = 2, ColumnCount = 2 },
            new SetTableCellOp { Target = new WordsTarget { Block = 5 }, Row = 2, Column = 2, Text = "125" },
            new RepeatTableRowOp { At = new WordsTarget { Find = "{{code}}" }, Row = 2, Items = [new Dictionary<string, object?> { ["code"] = "A-100" }] },
            new SetTablePaginationOp { Target = new WordsTarget { Block = 5 }, KeepTogether = true },
            new InsertTocOp { At = new WordsTarget { Block = 1 }, Position = "before" },
            new InsertBookmarkOp { At = new WordsTarget { Block = 2 }, Name = "Checked" },
            new InsertHyperlinkOp { At = new WordsTarget { Block = 2 }, Position = "after", Text = "Reference", Url = "https://example.com/" },
            new InsertFieldOp { At = new WordsTarget { Block = 2 }, Position = "after", Code = "DATE" },
            new InsertSectionOp { At = 1, Position = "after" },
            new DeleteSectionOp { Section = 2 },
            new SetPageSetupOp { Orientation = "landscape" },
            new SetHeaderOp { Section = 1, Paragraphs = [new WordsParagraphInput { Text = "Header" }] },
            new SetFooterOp { Markdown = "Footer" },
            new SetPageNumbersOp { Location = "footer" },
            new FormatTextOp { Target = new WordsTarget { Block = 2 }, Bold = true },
            new SetStyleOp { Target = new WordsTarget { Block = 3 }, Style = "Heading 2" },
            new DefineStyleOp { Name = "Callout", Bold = true },
            new SetListOp { Target = new WordsTarget { Blocks = "2-3" }, Kind = "bullet" },
            new ClearListOp { Target = new WordsTarget { Block = 2 } },
            new SetDefaultFontOp { Font = "Arial" },
            new SetPropertiesOp { Title = "Checked" },
            new SetWatermarkOp { Text = "DRAFT" },
            new DeleteWatermarkOp(),
            new ProtectDocumentOp { Protection = "read-only" },
            new UnprotectDocumentOp(),
            new AcceptRevisionsOp(),
            new RejectRevisionsOp(),
            new AddCommentOp { Target = new WordsTarget { Block = 2 }, Author = "Reviewer", Text = "Check." },
            new DeleteCommentsOp(),
            new ImportSectionsOp { Path = appendix },
            new MailMergeOp { Items = [new Dictionary<string, object?> { ["FirstName"] = "Ava" }] },
            new RefreshFieldsOp { What = "all" },
        ];
    }

    /// <summary>
    /// A report in two sections with a heading, a bookmark, a table with a placeholder row and
    /// one tracked insertion: blocks 1-7 in section 1, the last one empty, and 8-10 in section 2.
    /// </summary>
    private static string TwoSectionReport(WordsFixture fixture)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Quarterly report");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Writeln("Revenue increased by twelve percent.");
        builder.Writeln("Operations remained stable.");
        builder.StartBookmark("Summary");
        builder.Write("Bookmarked summary");
        builder.EndBookmark("Summary");
        builder.Writeln();
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Code");
        builder.InsertCell();
        builder.Write("Value");
        builder.EndRow();
        builder.InsertCell();
        builder.Write("{{code}}");
        builder.InsertCell();
        builder.Write("120");
        builder.EndRow();
        builder.EndTable();
        builder.Writeln("Totals follow.");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Writeln("Appendix");
        document.StartTrackRevisions("Reviewer");
        builder.Writeln("Tracked note.");
        document.StopTrackRevisions();
        builder.Write("Closing line.");
        string path = fixture.Temp.File($"two-sections-{Guid.NewGuid():N}.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }
}
