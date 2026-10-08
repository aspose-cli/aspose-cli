using Aspose.Cli.Product.Words.Output;
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
        string text = RenderedText.Of(surface => WordsRenderers.Render(InfoWithDetails(), surface), TableFormat.Plain);

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
            0        footer    first····

            outline:
            block  level  heading
            0      1      Summary
            5      2      Details

            styles:
            Normal, Heading 1

            bookmarks:
            none

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
        string text = RenderedText.Of(surface => WordsRenderers.Render(InfoWithDetails(), surface), TableFormat.Markdown);

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
            | 0 | footer | first |  |

            ### outline

            | block | level | heading |
            | --- | --- | --- |
            | 0 | 1 | Summary |
            | 5 | 2 | Details |

            ### styles

            Normal, Heading 1

            ### bookmarks

            none

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
    public void Info_WithEmptyDetails_SaysNoneUnderEachHeading()
    {
        DocumentInfoResult result = Info() with
        {
            Sections = [], Outline = [], Styles = [], Fields = [], Bookmarks = [], Comments = [], Revisions = [],
            Images = [], Tables = [], Properties = new Dictionary<string, string?>(), Fonts = [],
        };

        string text = RenderedText.Of(surface => WordsRenderers.Render(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\report.docx (docx, 10,240 bytes)
            sections: 2   blocks: 12 (10 paragraphs, 2 tables)   pages: 3   words: 456
            revisions: 1   protection: none   signed: no   macros: yes

            sections:
            none

            outline:
            none

            styles:
            none

            bookmarks:
            none

            fonts:
            none

            fields:
            none

            comments:
            none

            revisions:
            none

            images:
            none

            tables:
            none

            properties:
            none
            """,
            text);
    }

    [Fact]
    public void Info_WithoutDetails_PrintsOnlyTheSummary()
    {
        string text = RenderedText.Of(surface => WordsRenderers.Render(Info(), surface), TableFormat.Plain);

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
        var result = new DocumentReadResult
        {
            Source = new SourceInfo { Path = "report.docx", Format = "docx", SizeBytes = 10_240 },
            Scope = "body",
            BlockCount = 12,
            Blocks =
            [
                new BlockData { Block = 0, Type = "paragraph", Section = 0, Style = "Heading 1", Text = "Summary" },
                new BlockData { Block = 1, Type = "paragraph", Section = 0, Text = "" },
                new BlockData { Block = 2, Type = "table", Section = 0, RowCount = 3, ColumnCount = 2 },
            ],
        };

        string text = RenderedText.Of(surface => WordsRenderers.Render(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            report.docx (scope body, 12 blocks in the document)
            block  type       section  style      text
            0      paragraph  0        Heading 1  Summary
            1      paragraph  0        -··········
            2      table      0        -          [3x2 table]
            """,
            text);
    }

    [Fact]
    public void Edit_WithVerification_PrintsOutcomesPagesAndIssues()
    {
        var result = new WordsEditResult
        {
            Input = new SourceInfo { Path = "report.docx", Format = "docx", SizeBytes = 10_240 },
            Output = new OutputInfo { Path = "out.docx", Format = "docx", SizeBytes = 11_000 },
            DryRun = false,
            Applied =
            [
                new BoundedOperationOutcome { Id = "op-0001", Index = 0, Op = "set_text", Status = OpStatuses.Ok, ItemsAffected = 2 },
            ],
            PagesTouched = [1, 3],
            Verification = new WordsVerification
            {
                Ok = false,
                Issues =
                [
                    new VerificationIssue { Code = "FIELD_STALE", Message = "1 field needs an update", Location = "block 4" },
                ],
            },
        };

        string text = RenderedText.Of(surface => WordsRenderers.Render(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            wrote out.docx (docx, 11,000 bytes, 1 of 1 op(s) applied)
              [op-0001/0] set_text: ok (2 item(s))
            pages touched: 1, 3
            verification: needs attention
              FIELD_STALE: 1 field needs an update
            """,
            text);
    }

    private static DocumentInfoResult Info() => new()
    {
        Source = new SourceInfo { Path = @"C:\docs\report.docx", Format = "docx", SizeBytes = 10_240 },
        Document = new DocumentSummary
        {
            SectionCount = 2, BlockCount = 12, ParagraphCount = 10, TableCount = 2, PageCount = 3, WordCount = 456,
            RevisionsPresent = true, RevisionCount = 1, RevisionAuthors = ["Ann"], CommentCount = 1,
            Protection = "none", Signed = false, HasMacros = true,
        },
    };

    private static DocumentInfoResult InfoWithDetails() => Info() with
    {
        Sections =
        [
            new SectionData
            {
                Section = 0, Orientation = "portrait", WidthPoints = 612, HeightPoints = 792,
                Margins = new MarginData { Top = 72, Right = 89.85, Bottom = 72.004, Left = 0.125 },
                HeadersFooters =
                [
                    new HeaderFooterData { Location = "header", Kind = "primary", Paragraphs = ["Quarterly report", "Draft"] },
                    new HeaderFooterData { Location = "footer", Kind = "first", Paragraphs = [] },
                ],
            },
            new SectionData
            {
                Section = 1, Orientation = "landscape", WidthPoints = 841.89, HeightPoints = 595.276,
                Margins = new MarginData { Top = 36, Right = 36, Bottom = 36, Left = 36 },
                HeadersFooters = [],
            },
        ],
        Outline =
        [
            new OutlineItem { Block = 0, HeadingLevel = 1, Text = "Summary" },
            new OutlineItem { Block = 5, HeadingLevel = 2, Text = "Details" },
        ],
        Styles = ["Normal", "Heading 1"],
        Fields =
        [
            new FieldData { Type = "PAGE", Scope = "footer", Code = " PAGE ", Result = "1" },
            new FieldData { Type = "TOC", Block = 1 },
        ],
        Bookmarks = [],
        Comments = [new CommentData { Author = "Ann", Text = "Check the totals", Block = 3 }],
        Revisions =
        [
            new RevisionData { Revision = 0, Type = "insertion", Author = "Ann", Date = "2026-09-28T08:30:00Z", Block = 2, Text = "new text" },
            new RevisionData { Revision = 1, Type = "formatChange", Author = "Bob", Scope = "header" },
        ],
        Images =
        [
            new ImageData { Block = 4, Name = "Logo", Width = 120.5, Height = 60 },
            new ImageData { Scope = "header", Width = 10, Height = 10.333 },
        ],
        Tables =
        [
            new TableData { Block = 6, RowCount = 3, ColumnCount = 2, Style = "Table Grid" },
            new TableData { Block = 9, RowCount = 1, ColumnCount = 1 },
        ],
        Properties = new Dictionary<string, string?> { ["Title"] = "Report", ["Author"] = "Ann", ["Subject"] = null },
        Fonts = ["Calibri", "Times New Roman"],
    };
}
