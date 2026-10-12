using Aspose.Cli.Product.Words.Commands;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Pins the exact table and text output of the Words renderers for representative results, so
/// moving their shared paragraphs into the SDK keeps every byte.
/// </summary>
public sealed class WordsRendererGoldenTests
{
    [Fact]
    public void Info_WithEveryDetail_Plain()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(InfoWithDetails(), surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\report.docx (docx, 10,240 bytes)
            sections: 2   blocks: 12 (10 paragraphs, 2 tables)   pages: 3   words: 456
            revisions: 1   protection: none   signed: no   macros: yes

            sections:
            section  orientation  page size           margins (t/r/b/l)
            0        portrait     612 x 792 pt        72/89.85/72/0.13 pt
            1        landscape    841.89 x 595.28 pt  36/36/36/36 pt
            section  location  kind     paragraphs
            0        header    primary  Quarterly report | Draft
            0        footer    first    -

            outline:
            block  level  heading
            0      1      Summary
            5      2      Details

            styles:
            Normal, Heading 1

            bookmarks:
            -

            fonts:
            Calibri, Times New Roman

            fields:
            block  type  code    result
            -      PAGE   PAGE   1
            1      TOC   -       -

            comments:
            block  author  text
            3      Ann     Check the totals

            revisions:
            revision  block  type          author  date                  text
            0         2      insertion     Ann     2026-09-28T08:30:00Z  new text
            1         -      formatChange  Bob     -                     -

            images:
            block  name  size
            4      Logo  120.5 x 60 pt
            -      -     10 x 10.33 pt

            tables:
            block  rows  columns  style
            6      3     2        Table Grid
            9      1     1        -

            properties:
            name     value
            Author   Ann
            Subject  -
            Title    Report
            """,
            text);
    }

    [Fact]
    public void Info_WithEveryDetail_Markdown()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(InfoWithDetails(), surface), TableFormat.Markdown);

        RenderedText.Equal(
            """
            C:\docs\report.docx (docx, 10,240 bytes)
            sections: 2   blocks: 12 (10 paragraphs, 2 tables)   pages: 3   words: 456
            revisions: 1   protection: none   signed: no   macros: yes

            ### sections

            | section | orientation | page size | margins (t/r/b/l) |
            | --- | --- | --- | --- |
            | 0 | portrait | 612 x 792 pt | 72/89.85/72/0.13 pt |
            | 1 | landscape | 841.89 x 595.28 pt | 36/36/36/36 pt |
            | section | location | kind | paragraphs |
            | --- | --- | --- | --- |
            | 0 | header | primary | Quarterly report \| Draft |
            | 0 | footer | first | - |

            ### outline

            | block | level | heading |
            | --- | --- | --- |
            | 0 | 1 | Summary |
            | 5 | 2 | Details |

            ### styles

            Normal, Heading 1

            ### bookmarks

            -

            ### fonts

            Calibri, Times New Roman

            ### fields

            | block | type | code | result |
            | --- | --- | --- | --- |
            | - | PAGE |  PAGE  | 1 |
            | 1 | TOC | - | - |

            ### comments

            | block | author | text |
            | --- | --- | --- |
            | 3 | Ann | Check the totals |

            ### revisions

            | revision | block | type | author | date | text |
            | --- | --- | --- | --- | --- | --- |
            | 0 | 2 | insertion | Ann | 2026-09-28T08:30:00Z | new text |
            | 1 | - | formatChange | Bob | - | - |

            ### images

            | block | name | size |
            | --- | --- | --- |
            | 4 | Logo | 120.5 x 60 pt |
            | - | - | 10 x 10.33 pt |

            ### tables

            | block | rows | columns | style |
            | --- | --- | --- | --- |
            | 6 | 3 | 2 | Table Grid |
            | 9 | 1 | 1 | - |

            ### properties

            | name | value |
            | --- | --- |
            | Author | Ann |
            | Subject | - |
            | Title | Report |
            """,
            text);
    }

    [Fact]
    public void Info_WithEmptyDetails_SaysMissingUnderEachHeading()
    {
        WordsInspectResult result = Info() with
        {
            Sections = [], Outline = [], Styles = [], Fields = [], Bookmarks = [], Comments = [], Revisions = [],
            Images = [], Tables = [], Properties = new Dictionary<string, string?>(), Fonts = [],
        };

        string text = RenderedText.Of(surface => InspectCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\report.docx (docx, 10,240 bytes)
            sections: 2   blocks: 12 (10 paragraphs, 2 tables)   pages: 3   words: 456
            revisions: 1   protection: none   signed: no   macros: yes

            sections:
            -

            outline:
            -

            styles:
            -

            bookmarks:
            -

            fonts:
            -

            fields:
            -

            comments:
            -

            revisions:
            -

            images:
            -

            tables:
            -

            properties:
            -
            """,
            text);
    }

    [Fact]
    public void Info_WithoutDetails_PrintsOnlyTheSummary()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(Info(), surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\report.docx (docx, 10,240 bytes)
            sections: 2   blocks: 12 (10 paragraphs, 2 tables)   pages: 3   words: 456
            revisions: 1   protection: none   signed: no   macros: yes
            """,
            text);
    }

    [Fact]
    public void Read_PrintsOneRowPerBlock()
    {
        var result = new WordsReadResult
        {
            Input = new InputInfo { Path = "report.docx", Format = "docx", SizeBytes = 10_240, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            Window = new ResultWindow { Unit = "block", Returned = 2, Total = 12, Truncated = true },
            Scope = "body",
            BlockCount = 12,
            Blocks =
            [
                new WordsBlockData { Block = 0, Type = "paragraph", Section = 0, Style = "Heading 1", Text = "Summary" },
                new WordsBlockData { Block = 1, Type = "paragraph", Section = 0, Text = "" },
                new WordsBlockData { Block = 2, Type = "table", Section = 0, RowCount = 3, ColumnCount = 2 },
            ],
        };

        string text = RenderedText.Of(surface => ReadCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            report.docx (scope body, 12 blocks in the document)
            block  type       section  style      text
            0      paragraph  0        Heading 1  Summary
            1      paragraph  0        -          -
            2      table      0        -          [3x2 table]
            """,
            text);
    }

    [Fact]
    public void Edit_WithVerification_PrintsOutcomesPagesAndIssues()
    {
        var result = new WordsEditResult
        {
            Input = new InputInfo { Path = "report.docx", Format = "docx", SizeBytes = 10_240, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            Output = new OutputInfo { Path = "out.docx", Format = "docx", SizeBytes = 11_000, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) } },
            DryRun = false,
            Applied =
            [
                new BoundedOperationOutcome { Id = "op-0001", Op = "set_text", Status = OpStatuses.Ok, ItemsAffected = 2 },
            ],
            PagesTouched = [1, 3],
            Verification = new WordsEditVerification
            {
                Ok = false,
                CheckedOps = [],
                Issues =
                [
                    VerificationIssue.From(new Warning(new WarningCode("FIELD_STALE"), "1 field needs an update") { Location = "block 4", Hint = "Update the fields." }),
                ],
            },
        };

        string text = RenderedText.Of(surface => EditCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            wrote out.docx (docx, 11,000 bytes, 1 of 1 op(s) applied)
              [op-0001] set_text: ok (2 item(s))
            pages touched: 1, 3
            verification: needs attention; checked ops: -
              FIELD_STALE [block 4]: 1 field needs an update
                hint: Update the fields.
            """,
            text);
    }

    private static WordsInspectResult Info() => new()
    {
        Input = new InputInfo { Path = @"C:\docs\report.docx", Format = "docx", SizeBytes = 10_240, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
        Document = new WordsDocumentSummary
        {
            SectionCount = 2, BlockCount = 12, ParagraphCount = 10, TableCount = 2, PageCount = 3, WordCount = 456,
            RevisionsPresent = true, RevisionCount = 1, RevisionAuthors = ["Ann"], CommentCount = 1,
            Protection = "none", Signed = false, HasMacros = true,
        },
    };

    private static WordsInspectResult InfoWithDetails() => Info() with
    {
        Sections =
        [
            new WordsSectionData
            {
                Section = 0, Orientation = "portrait", WidthPoints = 612, HeightPoints = 792,
                Margins = new WordsMarginData { Top = 72, Right = 89.85, Bottom = 72.004, Left = 0.125 },
                HeadersFooters =
                [
                    new WordsHeaderFooterData { Location = "header", Kind = "primary", Paragraphs = ["Quarterly report", "Draft"] },
                    new WordsHeaderFooterData { Location = "footer", Kind = "first", Paragraphs = [] },
                ],
            },
            new WordsSectionData
            {
                Section = 1, Orientation = "landscape", WidthPoints = 841.89, HeightPoints = 595.276,
                Margins = new WordsMarginData { Top = 36, Right = 36, Bottom = 36, Left = 36 },
                HeadersFooters = [],
            },
        ],
        Outline =
        [
            new WordsOutlineItem { Block = 0, HeadingLevel = 1, Text = "Summary" },
            new WordsOutlineItem { Block = 5, HeadingLevel = 2, Text = "Details" },
        ],
        Styles = ["Normal", "Heading 1"],
        Fields =
        [
            new WordsFieldData { Type = "PAGE", Scope = "headersFooters", Code = " PAGE ", Result = "1" },
            new WordsFieldData { Type = "TOC", Scope = "body", Block = 1 },
        ],
        Bookmarks = [],
        Comments = [new WordsCommentData { Author = "Ann", Text = "Check the totals", Block = 3 }],
        Revisions =
        [
            new WordsRevisionData { Revision = 0, Type = "insertion", Author = "Ann", Date = "2026-09-28T08:30:00Z", Block = 2, Text = "new text" },
            new WordsRevisionData { Revision = 1, Type = "formatChange", Author = "Bob", Scope = "headersFooters" },
        ],
        Images =
        [
            new WordsImageData { Scope = "body", Block = 4, Name = "Logo", Width = 120.5, Height = 60 },
            new WordsImageData { Scope = "headersFooters", Width = 10, Height = 10.333 },
        ],
        Tables =
        [
            new WordsTableData { Block = 6, RowCount = 3, ColumnCount = 2, Style = "Table Grid" },
            new WordsTableData { Block = 9, RowCount = 1, ColumnCount = 1 },
        ],
        Properties = new Dictionary<string, string?> { ["Title"] = "Report", ["Author"] = "Ann", ["Subject"] = null },
        Fonts = ["Calibri", "Times New Roman"],
    };
}
