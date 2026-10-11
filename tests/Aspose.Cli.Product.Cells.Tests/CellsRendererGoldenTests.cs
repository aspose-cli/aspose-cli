using Aspose.Cli.Product.Cells.Commands;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Pins the exact table and text output of the Cells renderers for representative results, so
/// moving their shared paragraphs into the SDK keeps every byte.
/// </summary>
public sealed class CellsRendererGoldenTests
{
    [Fact]
    public void Info_WithEveryDetail_Plain()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(InfoWithDetails(), surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\data\report.xlsx (xlsx, 1,234,567 bytes)
            sheets: 2   vba: no   defined names: 1   encrypted: yes   structure protected: yes

            name         position  used range  rows  columns  hidden  protected  charts  pivots
            Data         0         A1:C9       9     3        no      yes        1       0
            Empty sheet  1         -           0     0        yes     no         0       0

            preview Data:
              Region | Sales | -
              North | 1,200 | x

            names:
            name  refers to
            Rate  =Data!$B$2

            formula errors:
            -

            validations:
            sheet  range  type
            Data   B2:B9  list

            fonts:
            font
            Calibri
            Arial

            tables:
            sheet  name   range
            Data   Sales  A1:C9

            charts:
            sheet  index  name     type
            Data   0      Chart 1  column

            layouts:
            sheet        freeze  row groups                 column groups  filter  print area  titles   page              header  footer
            Data         B2      2:20 L1, 3:4 L2 collapsed  B:C L1         A1:C9   A1:C9       1:1 A:A  landscape 85%     &P      Page &P of &N
            Empty sheet  -       -                          -              -       -           -        portrait fit 1x0  -       -
            """,
            text);
    }

    [Fact]
    public void Info_WithEveryDetail_Markdown()
    {
        string text = RenderedText.Of(surface => InspectCommand.Table(InfoWithDetails(), surface), TableFormat.Markdown);

        RenderedText.Equal(
            """
            C:\data\report.xlsx (xlsx, 1,234,567 bytes)
            sheets: 2   vba: no   defined names: 1   encrypted: yes   structure protected: yes

            | name | position | used range | rows | columns | hidden | protected | charts | pivots |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | Data | 0 | A1:C9 | 9 | 3 | no | yes | 1 | 0 |
            | Empty sheet | 1 | - | 0 | 0 | yes | no | 0 | 0 |

            preview Data:
              Region | Sales | -
              North | 1,200 | x

            ### names

            | name | refers to |
            | --- | --- |
            | Rate | =Data!$B$2 |

            ### formula errors

            -

            ### validations

            | sheet | range | type |
            | --- | --- | --- |
            | Data | B2:B9 | list |

            ### fonts

            | font |
            | --- |
            | Calibri |
            | Arial |

            ### tables

            | sheet | name | range |
            | --- | --- | --- |
            | Data | Sales | A1:C9 |

            ### charts

            | sheet | index | name | type |
            | --- | --- | --- | --- |
            | Data | 0 | Chart 1 | column |

            ### layouts

            | sheet | freeze | row groups | column groups | filter | print area | titles | page | header | footer |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | Data | B2 | 2:20 L1, 3:4 L2 collapsed | B:C L1 | A1:C9 | A1:C9 | 1:1 A:A | landscape 85% | &P | Page &P of &N |
            | Empty sheet | - | - | - | - | - | - | portrait fit 1x0 | - | - |
            """,
            text);
    }

    [Fact]
    public void Info_WithEmptyDetails_SaysMissingUnderEachHeading()
    {
        WorkbookInfoResult result = Info() with
        {
            Workbook = Info().Workbook with
            {
                DefinedNames = [], FormulaErrors = [], Validations = [], Fonts = [], Tables = [], Charts = [], Pivots = [], Layouts = [],
            },
        };

        string text = RenderedText.Of(surface => InspectCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            C:\data\report.xlsx (xlsx, 1,234,567 bytes)
            sheets: 2   vba: no   defined names: 1   encrypted: no   structure protected: yes

            name         position  used range  rows  columns  hidden  protected  charts  pivots
            Data         0         A1:C9       9     3        no      yes        1       0
            Empty sheet  1         -           0     0        yes     no         0       0

            preview Data:
              Region | Sales | -
              North | 1,200 | x

            names:
            -

            formula errors:
            -

            validations:
            -

            fonts:
            -

            tables:
            -

            charts:
            -

            pivots:
            -

            layouts:
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
            C:\data\report.xlsx (xlsx, 1,234,567 bytes)
            sheets: 2   vba: no   defined names: 1   encrypted: no   structure protected: yes

            name         position  used range  rows  columns  hidden  protected  charts  pivots
            Data         0         A1:C9       9     3        no      yes        1       0
            Empty sheet  1         -           0     0        yes     no         0       0

            preview Data:
              Region | Sales | -
              North | 1,200 | x
            """,
            text);
    }

    [Fact]
    public void Read_PrintsTheRangeAsAGrid()
    {
        var result = new WorkbookReadResult
        {
            Input = new InputInfo { Path = "book.xlsx", Format = "xlsx", SizeBytes = 2_048, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            Window = new ResultWindow { Unit = "cell", Returned = 4, Truncated = false },
            Scope = ReadScopes.Values,
            Sheet = new SheetProjection
            {
                Name = "Data",
                Position = 0,
                UsedRange = "A1:C9",
                Range = "B2:C3",
                Cells =
                [
                    [new CellData { V = "Region", T = CellValueTypes.String }, new CellData { V = 1.5, T = CellValueTypes.Number }],
                    [new CellData { V = true, T = CellValueTypes.Boolean }, new CellData { T = CellValueTypes.Empty }],
                ],
            },
        };

        string text = RenderedText.Of(surface => ReadCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            Data B2:C3 (used A1:C9)
               B       C
            2  Region  1.5
            3  TRUE    -
            """,
            text);
    }

    [Fact]
    public void Edit_WithVerification_PrintsOutcomesIssuesAndBackup()
    {
        var result = new EditResult
        {
            Input = new InputInfo { Path = "book.xlsx", Format = "xlsx", SizeBytes = 2_048, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            Output = new OutputInfo { Path = "out.xlsx", Format = "xlsx", SizeBytes = 4_096, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) } },
            DryRun = false,
            Recalculated = true,
            Applied =
            [
                new BoundedOperationOutcome { Id = "op-0001", Op = "set_values", Status = OpStatuses.Ok, ItemsAffected = 3 },
                new BoundedOperationOutcome
                {
                    Id = "total", Op = "set_formula", Status = OpStatuses.Failed, ItemsAffected = 0,
                    Error = new ErrorPayload { Code = "OPS_INVALID", Message = "sheet 'Missing' does not exist", Hint = "Inspect the sheets first." },
                },
            ],
            Backup = new BackupInfo
            {
                Path = "book.backup.xlsx", Created = true, SizeBytes = 2_048,
                LastWriteUtc = DateTimeOffset.UnixEpoch, HoldsReplacedVersion = true,
            },
            Verification = new CellsEditVerification
            {
                Ok = false,
                CheckedOps = ["op-0001"],
                RequestedTargets = [],
                DirectChanges = [new VerifiedCellChange { Sheet = "Data", Cell = "A1" }],
                FormulaResultChanges = [],
                OtherChanges = [],
                FormulaErrors = [new CellError { Sheet = "Data", Cell = "C7", Error = "#REF!" }],
                Truncated = false,
                Issues =
                [
                    VerificationIssues.Of(CellsDiagnostics.FormulaErrors, "C7 evaluates to #REF!", "Data!C7", "Fix the cell."),
                    VerificationIssue.From(new Warning(new WarningCode("RENDER_SKIPPED"), "no render was requested")),
                ],
            },
        };

        string text = RenderedText.Of(surface => EditCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            wrote out.xlsx (xlsx, 4,096 bytes, 1 of 2 op(s) applied, 1 failed)
              [op-0001] set_values: ok (3 item(s))
              [total] set_formula: failed (0 item(s))
                  OPS_INVALID: sheet 'Missing' does not exist
                  hint: Inspect the sheets first.
            backup: book.backup.xlsx (created)
            verification: needs attention; checked ops: op-0001; 1 direct, 0 formula-result, 1 formula error(s)
              FORMULA_ERRORS [Data!C7]: C7 evaluates to #REF!
                hint: Fix the cell.
              RENDER_SKIPPED: no render was requested
            """,
            text);
    }

    [Fact]
    public void Edit_DryRunWithoutVerification_PrintsOnlyTheOutcomes()
    {
        var result = new EditResult
        {
            Input = new InputInfo { Path = "book.xlsx", Format = "xlsx", SizeBytes = 2_048, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            DryRun = true,
            Recalculated = false,
            Applied = [new BoundedOperationOutcome { Id = "op-0001", Op = "set_values", Status = OpStatuses.Ok, ItemsAffected = 1 }],
        };

        string text = RenderedText.Of(surface => EditCommand.Table(result, surface), TableFormat.Plain);

        RenderedText.Equal(
            """
            dry run: 1 of 1 op(s) applied; nothing was written
              [op-0001] set_values: ok (1 item(s))
            """,
            text);
    }

    private static WorkbookInfoResult Info() => new()
    {
        Input = new InputInfo { Path = @"C:\data\report.xlsx", Format = "xlsx", SizeBytes = 1_234_567, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
        Workbook = new WorkbookSummary
        {
            Name = "report.xlsx",
            SheetCount = 2,
            HasVba = false,
            DefinedNameCount = 1,
            StructureProtected = true,
            StructurePasswordProtected = false,
            Sheets =
            [
                new SheetInfo
                {
                    Name = "Data", Position = 0, UsedRange = "A1:C9", RowCount = 9, ColumnCount = 3, Hidden = false,
                    Protected = true, PasswordProtected = false, ChartCount = 1, PivotTableCount = 0,
                    Preview = [["Region", "Sales", null], ["North", "1,200", "x"]],
                },
                new SheetInfo
                {
                    Name = "Empty sheet", Position = 1, RowCount = 0, ColumnCount = 0, Hidden = true,
                    Protected = false, PasswordProtected = false, ChartCount = 0, PivotTableCount = 0,
                },
            ],
        },
    };

    private static WorkbookInfoResult InfoWithDetails()
    {
        WorkbookInfoResult info = Info();
        return info with
        {
            Input = info.Input with { Encrypted = true },
            Workbook = info.Workbook with
            {
                DefinedNames = [new DefinedNameInfo { Name = "Rate", RefersTo = "=Data!$B$2" }],
                FormulaErrors = [],
                Validations = [new ValidationInfo { Sheet = "Data", Range = "B2:B9", Type = "list" }],
                Fonts = ["Calibri", "Arial"],
                Tables = [new TableInfo { Sheet = "Data", Name = "Sales", Range = "A1:C9" }],
                Charts = [new ChartInfo { Sheet = "Data", Index = 0, Name = "Chart 1", Type = "column" }],
                Pivots = null,
                Layouts =
                [
                    new SheetLayoutInfo
                    {
                        Sheet = "Data",
                        FreezePanes = "B2",
                        RowGroups =
                        [
                            new RowGroupInfo { From = 2, To = 20, Level = 1, Collapsed = false },
                            new RowGroupInfo { From = 3, To = 4, Level = 2, Collapsed = true },
                        ],
                        ColumnGroups = [new ColumnGroupInfo { From = "B", To = "C", Level = 1, Collapsed = false }],
                        AutoFilter = "A1:C9",
                        PrintArea = "A1:C9",
                        TitleRows = "1:1",
                        TitleColumns = "A:A",
                        Orientation = "landscape",
                        Scale = 85,
                        Header = "&P",
                        Footer = "Page &P of &N",
                    },
                    new SheetLayoutInfo { Sheet = "Empty sheet", Orientation = "portrait", FitToWidth = 1, FitToHeight = 0 },
                ],
            },
        };
    }
}
