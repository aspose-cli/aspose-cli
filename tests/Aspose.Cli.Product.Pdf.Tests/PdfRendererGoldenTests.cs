using Aspose.Cli.Product.Pdf.Commands;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Pins the exact table and text output of the PDF renderers for representative results, so
/// moving their shared paragraphs into the SDK keeps every byte.
/// </summary>
public sealed class PdfRendererGoldenTests
{
    [Fact]
    public void Info_WithEveryDetail_Plain()
    {
        string text = RenderedText.Of(surface => InfoCommand.Table(InfoWithDetails(), surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\in.pdf (pdf, 20,000 bytes)
            pages: 3   version: 1.7   encrypted: no   tagged: yes   PDF/A declared: PDF/A-2b
            form: acroform   attachments: 1   signed: yes   password access: none
            page sizes: 612 x 792 pt (2), 595.28 x 841.89 pt (1)
            page labels: p1 A-roman from 1, p3 decimal from 1
            page  size (pt)        rotation
            1     612 x 792        0
            2     841.89 x 595.28  90

            outline:
            index  title    page
            1      Intro    1
            1.1      Scope··

            forms:
            acroform: 4 field(s), read-only

            attachments:
            name         type      size
            data.csv     text/csv  1,234 bytes
            unknown.bin············

            fonts:
            font            embedded  subset
            Helvetica       no        no
            ABCDEF+Calibri  yes       yes

            permissions:
            open password: no   owner password: yes   owner access: no
            print: yes   copy: no   modify: no   annotate: yes   fill forms: yes   accessibility: yes   assemble: no   high-resolution print: yes

            signatures:
            field       signed  valid
            Signature1  yes     yes
            Signature2  no······

            layers:
            Background, Notes

            metadata:
            property  value
            Title     Report
            Author····
            Creator   Writer
            """,
            text);
    }

    [Fact]
    public void Info_WithEveryDetail_Markdown()
    {
        string text = RenderedText.Of(surface => InfoCommand.Table(InfoWithDetails(), surface), TableFormat.Markdown);

        RenderedText.Equal(
            """
            C:\docs\in.pdf (pdf, 20,000 bytes)
            pages: 3   version: 1.7   encrypted: no   tagged: yes   PDF/A declared: PDF/A-2b
            form: acroform   attachments: 1   signed: yes   password access: none
            page sizes: 612 x 792 pt (2), 595.28 x 841.89 pt (1)
            page labels: p1 A-roman from 1, p3 decimal from 1
            | page | size (pt) | rotation |
            | --- | --- | --- |
            | 1 | 612 x 792 | 0 |
            | 2 | 841.89 x 595.28 | 90 |

            ### outline

            | index | title | page |
            | --- | --- | --- |
            | 1 | Intro | 1 |
            | 1.1 |   Scope |  |

            ### forms

            acroform: 4 field(s), read-only

            ### attachments

            | name | type | size |
            | --- | --- | --- |
            | data.csv | text/csv | 1,234 bytes |
            | unknown.bin |  |  |

            ### fonts

            | font | embedded | subset |
            | --- | --- | --- |
            | Helvetica | no | no |
            | ABCDEF+Calibri | yes | yes |

            ### permissions

            open password: no   owner password: yes   owner access: no
            print: yes   copy: no   modify: no   annotate: yes   fill forms: yes   accessibility: yes   assemble: no   high-resolution print: yes

            ### signatures

            | field | signed | valid |
            | --- | --- | --- |
            | Signature1 | yes | yes |
            | Signature2 | no |  |

            ### layers

            Background, Notes

            ### metadata

            | property | value |
            | --- | --- |
            | Title | Report |
            | Author |  |
            | Creator | Writer |
            """,
            text);
    }

    [Fact]
    public void Info_WithEmptyDetails_SaysNoneUnderEachHeading()
    {
        PdfInfoResult result = Info() with
        {
            Outline = [], Attachments = [], Fonts = [], Signatures = [], Layers = [],
            Metadata = new Dictionary<string, string?>(),
        };

        string text = RenderedText.Of(surface => InfoCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\in.pdf (pdf, 20,000 bytes)
            pages: 3   version: 1.7   encrypted: no   tagged: yes   PDF/A declared: no
            form: acroform   attachments: 1   signed: yes   password access: none
            page sizes: 612 x 792 pt (2), 595.28 x 841.89 pt (1)

            outline:
            none

            attachments:
            none

            fonts:
            none

            signatures:
            none

            layers:
            none

            metadata:
            none
            """,
            text);
    }

    [Fact]
    public void Info_WithoutDetails_PrintsOnlyTheSummary()
    {
        PdfInfoResult info = Info();
        PdfInfoResult result = info with { Pdf = info.Pdf with { DistinctPageSizes = [] } };

        string text = RenderedText.Of(surface => InfoCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\docs\in.pdf (pdf, 20,000 bytes)
            pages: 3   version: 1.7   encrypted: no   tagged: yes   PDF/A declared: no
            form: acroform   attachments: 1   signed: yes   password access: none
            """,
            text);
    }

    [Fact]
    public void Edit_WithVerification_PrintsOutcomesPagesIssuesAndHints()
    {
        var result = new PdfEditResult
        {
            Input = new SourceInfo { Path = "in.pdf", Format = "pdf", SizeBytes = 20_000 },
            Output = new OutputInfo { Path = "in.pdf", Format = "pdf", SizeBytes = 21_500 },
            DryRun = false,
            Applied =
            [
                new BoundedOperationOutcome { Id = "stamp", Index = 0, Op = "add_text", Status = OpStatuses.Ok, ItemsAffected = 1 },
                new BoundedOperationOutcome { Id = "op-0002", Index = 1, Op = "redact", Status = OpStatuses.Ok, ItemsAffected = 4 },
            ],
            Backup = new BackupInfo
            {
                Path = "in.backup.pdf", Created = false, SizeBytes = 20_000,
                LastWriteUtc = new DateTimeOffset(2026, 9, 28, 8, 30, 0, TimeSpan.Zero), HoldsReplacedVersion = true,
            },
            PagesTouched = [2],
            Verification = new PdfEditVerification
            {
                Ok = false,
                CheckedOps = ["stamp"],
                Issues =
                [
                    new VerificationIssue { Code = "TEXT_NOT_FOUND", Message = "the stamp text was not read back", Location = "page 2", Hint = "Check the font." },
                    new VerificationIssue { Code = "REDACTION_PARTIAL", Message = "1 match remains" },
                ],
            },
        };

        string text = RenderedText.Of(surface => EditCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            wrote in.pdf (pdf, 21,500 bytes, 2 of 2 op(s) applied)
              [stamp/0] add_text: ok (1 item(s))
              [op-0002/1] redact: ok (4 item(s))
            backup: in.backup.pdf (kept existing)
            pages touched: 2
            verification: needs attention (1 operation(s) read back: stamp)
              TEXT_NOT_FOUND [page 2]: the stamp text was not read back
                hint: Check the font.
              REDACTION_PARTIAL: 1 match remains
            """,
            text);
    }

    [Fact]
    public void Edit_WithoutReadBack_SaysNoneWereChecked()
    {
        var result = new PdfEditResult
        {
            Input = new SourceInfo { Path = "in.pdf", Format = "pdf", SizeBytes = 20_000 },
            DryRun = true,
            Applied = [new BoundedOperationOutcome { Id = "op-0001", Index = 0, Op = "rotate_pages", Status = OpStatuses.Ok, ItemsAffected = 2 }],
            Verification = new PdfEditVerification { Ok = true, CheckedOps = [], Issues = [] },
        };

        string text = RenderedText.Of(surface => EditCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            dry run: 1 of 1 op(s) applied; nothing was written
              [op-0001/0] rotate_pages: ok (2 item(s))
            verification: ok (0 operation(s) read back: none)
            """,
            text);
    }

    private static PdfInfoResult Info() => new()
    {
        Source = new SourceInfo { Path = @"C:\docs\in.pdf", Format = "pdf", SizeBytes = 20_000 },
        Pdf = new PdfSummary
        {
            PageCount = 3,
            DistinctPageSizes =
            [
                new PdfPageSizeSummary { WidthPoints = 612, HeightPoints = 792, PageCount = 2 },
                new PdfPageSizeSummary { WidthPoints = 595.276, HeightPoints = 841.89, PageCount = 1 },
            ],
            Version = "1.7",
            Encrypted = false,
            Linearized = false,
            Tagged = true,
            FormType = "acroform",
            AttachmentCount = 1,
            Signed = true,
            PasswordType = "none",
        },
    };

    private static PdfInfoResult InfoWithDetails()
    {
        PdfInfoResult info = Info();
        return info with
        {
            Pdf = info.Pdf with { PdfaProfile = "PDF/A-2b" },
            Pages =
            [
                new PdfPageInfo
                {
                    Page = 1, WidthPoints = 612, HeightPoints = 792, Rotation = 0,
                    MediaBox = new PdfBox { Left = 0, Bottom = 0, Right = 612, Top = 792 },
                    CropBox = new PdfBox { Left = 0, Bottom = 0, Right = 612, Top = 792 },
                },
                new PdfPageInfo
                {
                    Page = 2, WidthPoints = 841.89, HeightPoints = 595.276, Rotation = 90,
                    MediaBox = new PdfBox { Left = 0, Bottom = 0, Right = 595.276, Top = 841.89 },
                    CropBox = new PdfBox { Left = 0, Bottom = 0, Right = 595.276, Top = 841.89 },
                },
            ],
            PageLabels =
            [
                new PdfPageLabelInfo { StartPage = 1, Style = "roman", Prefix = "A-", StartingValue = 1 },
                new PdfPageLabelInfo { StartPage = 3, Style = "decimal", StartingValue = 1 },
            ],
            Outline =
            [
                new PdfOutlineItem { Index = "1", Title = "Intro", Level = 1, Page = 1 },
                new PdfOutlineItem { Index = "1.1", Title = "Scope", Level = 2 },
            ],
            Forms = new PdfFormSummary { Type = "acroform", FieldCount = 4, ReadOnly = true },
            Attachments =
            [
                new PdfAttachmentInfo { Name = "data.csv", MimeType = "text/csv", SizeBytes = 1_234 },
                new PdfAttachmentInfo { Name = "unknown.bin" },
            ],
            Fonts =
            [
                new PdfFontInfo { Name = "Helvetica", Embedded = false, Subset = false },
                new PdfFontInfo { Name = "ABCDEF+Calibri", Embedded = true, Subset = true },
            ],
            Permissions = new PdfPermissionInfo
            {
                HasOpenPassword = false, HasOwnerPassword = true, OwnerAccess = false, Print = true, Copy = false,
                Modify = false, Annotate = true, FillForms = true, ExtractAccessibility = true, Assemble = false,
                PrintHighResolution = true,
            },
            Signatures =
            [
                new PdfSignatureInfo { Name = "Signature1", Signed = true, Valid = true },
                new PdfSignatureInfo { Name = "Signature2", Signed = false },
            ],
            Layers = ["Background", "Notes"],
            Metadata = new Dictionary<string, string?> { ["Title"] = "Report", ["Author"] = null, ["Creator"] = "Writer" },
        };
    }
}
